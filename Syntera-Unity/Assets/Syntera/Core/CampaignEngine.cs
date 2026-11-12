using System;
using System.Collections.Generic;

namespace Syntera.Core
{
    public enum CampaignStepKind { Decision, YearTransition, Council, Complete }

    public sealed class OptionStatus
    {
        public string DecisionId { get; internal set; }
        public string OptionId { get; internal set; }
        public int CreditsCost { get; internal set; }
        public int TreasuryCost { get; internal set; }
        public int CreditsShortfall { get; internal set; }
        public int TreasuryShortfall { get; internal set; }
        public bool Affordable { get { return CreditsShortfall == 0 && TreasuryShortfall == 0; } }
        // Naomi's once-per-run line: only for a Credits shortfall, never for Treasury.
        public bool CreditLineOffered { get; internal set; }
        public int CreditsShortfallAfterCreditLine { get; internal set; }
        // Plain-language reason shown on a greyed-out option. Empty when affordable.
        public string Explanation { get; internal set; }
    }

    public sealed class TransactionResult
    {
        public bool Committed { get; internal set; }
        public string StepId { get; internal set; }
        public string Message { get; internal set; }
        public AuditEvent Audit { get; internal set; }
        public CouncilResult Council { get; internal set; }

        internal static TransactionResult Reject(string stepId, string message)
        {
            return new TransactionResult { Committed = false, StepId = stepId, Message = message };
        }
    }

    // The only path by which campaign state changes. Follows screenplay section 10:
    // validate mission, verify funds, commit one deterministic action, append a
    // hash-linked audit event, then notify listeners (save checkpoint, reactions).
    public sealed class CampaignEngine
    {
        public CampaignRules Rules { get; private set; }
        public CampaignState State { get; private set; }
        public AuditLog Log { get; private set; }

        // Raised after every committed transaction; the Unity layer saves a checkpoint here.
        public event Action<TransactionResult> Committed;

        public CampaignEngine(CampaignRules rules)
        {
            if (rules == null) throw new ArgumentNullException("rules");
            Rules = rules;
            State = CampaignState.New(rules);
            Log = new AuditLog();
            Log.Append("start", string.Empty, "Campaign started on screenplay rules v" + rules.ScriptVersion);
        }

        public CampaignEngine(CampaignRules rules, CampaignState state, AuditLog log)
        {
            if (rules == null) throw new ArgumentNullException("rules");
            if (state == null) throw new ArgumentNullException("state");
            if (log == null) throw new ArgumentNullException("log");
            if (!log.Verify()) throw new InvalidOperationException("Audit log failed hash verification");
            Rules = rules;
            State = state;
            Log = log;
        }

        public string CurrentStepId
        {
            get { return State.StepIndex < Rules.Sequence.Count ? Rules.Sequence[State.StepIndex] : null; }
        }

        public CampaignStepKind CurrentStepKind
        {
            get
            {
                string id = CurrentStepId;
                if (id == null) return CampaignStepKind.Complete;
                if (id == CampaignRules.CouncilStepId) return CampaignStepKind.Council;
                if (Rules.FindYear(id) != null) return CampaignStepKind.YearTransition;
                return CampaignStepKind.Decision;
            }
        }

        public OptionStatus Evaluate(string decisionId, string optionId)
        {
            var decision = Rules.FindDecision(decisionId);
            if (decision == null) throw new ArgumentException("Unknown decision " + decisionId);
            var option = decision.Option(optionId);
            if (option == null) throw new ArgumentException("Unknown option " + decisionId + "/" + optionId);

            var s = new OptionStatus { DecisionId = decisionId, OptionId = optionId };
            s.CreditsCost = option.Cost("credits");
            s.TreasuryCost = option.Cost("treasury");
            s.CreditsShortfall = Math.Max(0, s.CreditsCost - State.Get("credits"));
            s.TreasuryShortfall = Math.Max(0, s.TreasuryCost - State.Get("treasury"));
            s.CreditLineOffered = s.CreditsShortfall > 0 && s.TreasuryShortfall == 0 && !CreditLineUsed;
            s.CreditsShortfallAfterCreditLine = s.CreditLineOffered
                ? Math.Max(0, s.CreditsShortfall - Rules.CreditLineAmount)
                : s.CreditsShortfall;

            var reasons = new List<string>();
            if (s.CreditsShortfall > 0) reasons.Add("Needs " + s.CreditsCost + " Credits; you have " + State.Get("credits") + " (short " + s.CreditsShortfall + ").");
            if (s.TreasuryShortfall > 0) reasons.Add("Needs " + s.TreasuryCost + " Treasury; the city has " + State.Get("treasury") + " (short " + s.TreasuryShortfall + "). The city budget has no credit line.");
            if (s.CreditLineOffered)
                reasons.Add(s.CreditsShortfallAfterCreditLine == 0
                    ? "Naomi's one-time credit line (+" + Rules.CreditLineAmount + " Credits, Autonomy -4) would cover it."
                    : "Even Naomi's one-time credit line (+" + Rules.CreditLineAmount + ") leaves you " + s.CreditsShortfallAfterCreditLine + " short.");
            s.Explanation = string.Join(" ", reasons.ToArray());
            return s;
        }

        public bool CreditLineUsed
        {
            get
            {
                foreach (var f in Rules.CreditLineFlags) if (State.HasFlag(f)) return true;
                return false;
            }
        }

        public TransactionResult TakeCreditLine(string decisionId, string optionId)
        {
            if (CurrentStepId != decisionId) return TransactionResult.Reject(decisionId, "Not the current mission.");
            var status = Evaluate(decisionId, optionId);
            if (!status.CreditLineOffered) return TransactionResult.Reject(decisionId, "Naomi's credit line is not available for this choice.");

            var detail = new List<string>();
            AddStat("credits", Rules.CreditLineAmount, detail);
            foreach (var e in Rules.CreditLineEffects) AddStat(e.Stat, e.Delta, detail);
            foreach (var f in Rules.CreditLineFlags) State.AddFlag(f);
            return Commit("creditLine", decisionId, "Naomi's credit line accepted for option " + optionId + ": " + string.Join(", ", detail.ToArray()));
        }

        public TransactionResult Choose(string decisionId, string optionId)
        {
            if (CurrentStepKind != CampaignStepKind.Decision || CurrentStepId != decisionId)
                return TransactionResult.Reject(decisionId, "Not the current mission.");
            var decision = Rules.FindDecision(decisionId);
            var option = decision.Option(optionId);
            if (option == null) return TransactionResult.Reject(decisionId, "Unknown option " + optionId + ".");
            var status = Evaluate(decisionId, optionId);
            if (!status.Affordable) return TransactionResult.Reject(decisionId, status.Explanation);

            var detail = new List<string>();
            foreach (var e in option.Effects) AddStat(e.Stat, e.Delta, detail);
            foreach (var f in option.SetFlags) { State.AddFlag(f); detail.Add("sets " + f); }
            if (option.Ownership != null)
            {
                int moved = Math.Min(option.Ownership.Percent, State.OwnershipOf(option.Ownership.From));
                State.SetOwnership(option.Ownership.From, State.OwnershipOf(option.Ownership.From) - moved);
                State.SetOwnership(option.Ownership.To, State.OwnershipOf(option.Ownership.To) + moved);
                detail.Add("ownership " + moved + "% " + option.Ownership.From + " -> " + option.Ownership.To);
            }
            foreach (var cf in option.ConditionalFlags)
                if (cf.When.IsMet(Rules, State)) { State.AddFlag(cf.Flag); detail.Add("sets " + cf.Flag); }

            State.RecordChoice(decisionId, optionId);
            State.StepIndex++;
            return Commit("decision", decisionId, option.Id + " " + option.Label + ": " + string.Join(", ", detail.ToArray()));
        }

        public TransactionResult AdvanceYear(string yearId)
        {
            if (CurrentStepKind != CampaignStepKind.YearTransition || CurrentStepId != yearId)
                return TransactionResult.Reject(yearId, "Not the current year transition.");
            var year = Rules.FindYear(yearId);
            var detail = new List<string>();
            foreach (var rule in year.Rules)
            {
                if (!rule.When.IsMet(Rules, State)) continue;
                var parts = new List<string>();
                AddStat(rule.Stat, rule.Delta, parts);
                detail.Add(rule.Label + " (" + parts[0] + ")");
            }
            State.StepIndex++;
            return Commit("year", yearId, year.Year + ": " + string.Join("; ", detail.ToArray()));
        }

        // Preview never changes state; use it for the live seat display before the player commits.
        public CouncilResult PreviewCharter(string charterId)
        {
            return CouncilEvaluator.Evaluate(Rules, State, charterId);
        }

        public TransactionResult ProposeCharter(string charterId)
        {
            if (CurrentStepKind != CampaignStepKind.Council)
                return TransactionResult.Reject(CampaignRules.CouncilStepId, "The council is not in session.");
            if (Rules.FindCharter(charterId) == null)
                return TransactionResult.Reject(CampaignRules.CouncilStepId, "Unknown charter " + charterId + ".");
            var council = CouncilEvaluator.Evaluate(Rules, State, charterId);
            State.CharterId = charterId;
            State.EndingId = council.EndingId;
            State.StepIndex++;
            var result = Commit("council", CampaignRules.CouncilStepId, council.CharterTitle + " " + council.SeatsFor + "/" + council.TotalSeats + " -> " + council.EndingId + ". " + council.EndingReason);
            result.Council = council;
            return result;
        }

        private void AddStat(string stat, int delta, List<string> detail)
        {
            int before = State.Get(stat);
            int after = Rules.Bound(stat, before + delta);
            State.Set(stat, after);
            detail.Add(stat + " " + (delta >= 0 ? "+" : "") + delta + " (" + before + "->" + after + ")");
        }

        private TransactionResult Commit(string kind, string stepId, string detail)
        {
            var result = new TransactionResult { Committed = true, StepId = stepId, Message = detail };
            result.Audit = Log.Append(kind, stepId, detail);
            var handler = Committed;
            if (handler != null) handler(result);
            return result;
        }
    }
}
