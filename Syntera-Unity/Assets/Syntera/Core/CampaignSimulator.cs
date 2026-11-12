using System;
using System.Collections.Generic;
using System.Text;

namespace Syntera.Core
{
    public sealed class PathOutcome
    {
        public string Intent { get; internal set; }
        public string Actual { get; internal set; }
        public bool AllIntendedAffordable { get; internal set; }
        public bool UsedCreditLine { get; internal set; }
        public bool ResourcesNeverNegative { get; internal set; }
        public bool AuditVerified { get; internal set; }
        public CampaignState FinalState { get; internal set; }
        public IDictionary<string, CouncilResult> CouncilByCharter { get; internal set; }
    }

    public sealed class SimulationSummary
    {
        public int Paths { get; internal set; }
        public int AllIntendedAffordable { get; internal set; }
        public int UsedCreditLine { get; internal set; }
        public int NegativeResourcePaths { get; internal set; }
        public int AuditFailures { get; internal set; }
        public int NoCharterPasses { get; internal set; }
        public IDictionary<string, int> CharterPasses { get; internal set; }
        public IDictionary<string, int> EndingCounts { get; internal set; }
        public int MaxIntendedOptionsTaken { get; internal set; }
    }

    // Plays intended choice paths through the real engine (screenplay section 14).
    // Policy for an intended option that cannot be afforded: accept Naomi's credit line
    // the first time it is offered, then take the first affordable alternative option.
    public static class CampaignSimulator
    {
        public static PathOutcome RunIntent(CampaignRules rules, string intent)
        {
            int decisionCount = rules.Decisions.Count;
            if (intent == null || intent.Length != decisionCount)
                throw new ArgumentException("Intent must have one letter per decision (" + decisionCount + ")");

            var engine = new CampaignEngine(rules);
            var actual = new StringBuilder();
            bool allAffordable = true, nonNegative = true;
            int d = 0;

            while (engine.CurrentStepKind == CampaignStepKind.Decision || engine.CurrentStepKind == CampaignStepKind.YearTransition)
            {
                string step = engine.CurrentStepId;
                if (engine.CurrentStepKind == CampaignStepKind.YearTransition)
                {
                    Require(engine.AdvanceYear(step));
                }
                else
                {
                    string want = intent[d].ToString();
                    d++;
                    var status = engine.Evaluate(step, want);
                    if (!status.Affordable && status.CreditLineOffered)
                    {
                        Require(engine.TakeCreditLine(step, want));
                        status = engine.Evaluate(step, want);
                    }
                    string chosen = want;
                    if (!status.Affordable)
                    {
                        allAffordable = false;
                        chosen = null;
                        foreach (var opt in rules.FindDecision(step).Options)
                        {
                            if (opt.Id == want) continue;
                            if (engine.Evaluate(step, opt.Id).Affordable) { chosen = opt.Id; break; }
                        }
                        if (chosen == null) throw new InvalidOperationException("Softlock: no affordable option at " + step + " for intent " + intent);
                    }
                    Require(engine.Choose(step, chosen));
                    actual.Append(chosen);
                }
                if (engine.State.Get("credits") < 0 || engine.State.Get("treasury") < 0) nonNegative = false;
            }

            var council = new Dictionary<string, CouncilResult>(StringComparer.Ordinal);
            foreach (var c in rules.Charters) council[c.Id] = engine.PreviewCharter(c.Id);

            return new PathOutcome
            {
                Intent = intent,
                Actual = actual.ToString(),
                AllIntendedAffordable = allAffordable,
                UsedCreditLine = engine.CreditLineUsed,
                ResourcesNeverNegative = nonNegative,
                AuditVerified = engine.Log.Verify(),
                FinalState = engine.State,
                CouncilByCharter = council
            };
        }

        public static IEnumerable<string> AllIntents(CampaignRules rules, string letters)
        {
            int n = rules.Decisions.Count;
            int b = letters.Length;
            long total = 1;
            for (int i = 0; i < n; i++) total *= b;
            for (long k = 0; k < total; k++)
            {
                var chars = new char[n];
                long x = k;
                for (int i = n - 1; i >= 0; i--) { chars[i] = letters[(int)(x % b)]; x /= b; }
                yield return new string(chars);
            }
        }

        public static SimulationSummary RunAll(CampaignRules rules)
        {
            var passes = new Dictionary<string, int>(StringComparer.Ordinal);
            var endings = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var c in rules.Charters) passes[c.Id] = 0;
            foreach (var e in rules.Endings) endings[e.Id] = 0;
            int paths = 0, affordable = 0, credit = 0, negative = 0, auditFail = 0, none = 0, maxIntended = 0;

            foreach (var intent in AllIntents(rules, "AB"))
            {
                var o = RunIntent(rules, intent);
                paths++;
                if (o.AllIntendedAffordable) affordable++;
                if (o.UsedCreditLine) credit++;
                if (!o.ResourcesNeverNegative) negative++;
                if (!o.AuditVerified) auditFail++;
                bool any = false;
                foreach (var kv in o.CouncilByCharter)
                {
                    if (kv.Value.Passed) { passes[kv.Key]++; any = true; }
                    endings[kv.Value.EndingId]++;
                }
                if (!any) none++;
                int a = 0;
                foreach (char ch in o.Actual) if (ch == 'A') a++;
                if (a > maxIntended) maxIntended = a;
            }

            return new SimulationSummary
            {
                Paths = paths,
                AllIntendedAffordable = affordable,
                UsedCreditLine = credit,
                NegativeResourcePaths = negative,
                AuditFailures = auditFail,
                NoCharterPasses = none,
                CharterPasses = passes,
                EndingCounts = endings,
                MaxIntendedOptionsTaken = maxIntended
            };
        }

        private static void Require(TransactionResult r)
        {
            if (!r.Committed) throw new InvalidOperationException("Simulation transaction rejected at " + r.StepId + ": " + r.Message);
        }
    }
}
