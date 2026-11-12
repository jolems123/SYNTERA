using System;
using System.Collections.Generic;
using System.Globalization;

namespace Syntera.Core
{
    // One clause of a rule: a stat threshold (min and/or max, inclusive) or a flag test.
    public sealed class Condition
    {
        public string Stat { get; private set; }
        public int? Min { get; private set; }
        public int? Max { get; private set; }
        public string Flag { get; private set; }
        public bool FlagValue { get; private set; }

        internal static Condition Parse(object raw, string context)
        {
            var o = Json.Obj(raw, context);
            var c = new Condition();
            c.Stat = Json.OptStr(o, "stat");
            c.Flag = Json.OptStr(o, "flag");
            if ((c.Stat == null) == (c.Flag == null)) throw new FormatException(context + ": condition needs exactly one of 'stat' or 'flag'");
            if (c.Stat != null)
            {
                if (Json.Has(o, "min")) c.Min = Json.Int(o, "min", context);
                if (Json.Has(o, "max")) c.Max = Json.Int(o, "max", context);
                if (!c.Min.HasValue && !c.Max.HasValue) throw new FormatException(context + ": stat condition needs min or max");
            }
            else
            {
                object v;
                if (!o.TryGetValue("is", out v) || !(v is bool)) throw new FormatException(context + ": flag condition needs boolean 'is'");
                c.FlagValue = (bool)v;
            }
            return c;
        }

        public bool IsMet(CampaignRules rules, CampaignState state)
        {
            if (Flag != null) return state.HasFlag(Flag) == FlagValue;
            int value = rules.ReadStat(state, Stat);
            if (Min.HasValue && value < Min.Value) return false;
            if (Max.HasValue && value > Max.Value) return false;
            return true;
        }

        // Human-readable statement of this clause against the current state, e.g. "Trust 67 >= 65 (met)".
        public string Describe(CampaignRules rules, CampaignState state)
        {
            if (Flag != null)
                return Flag + (FlagValue ? " is set" : " is not set") + (IsMet(rules, state) ? " (met)" : " (not met)");
            int value = rules.ReadStat(state, Stat);
            string label = rules.StatLabel(Stat) + " " + value.ToString(CultureInfo.InvariantCulture);
            var parts = new List<string>();
            if (Min.HasValue) parts.Add(label + (value >= Min.Value ? " >= " : " < ") + Min.Value.ToString(CultureInfo.InvariantCulture));
            if (Max.HasValue) parts.Add(label + (value <= Max.Value ? " <= " : " > ") + Max.Value.ToString(CultureInfo.InvariantCulture));
            return string.Join(" and ", parts.ToArray());
        }
    }

    // Disjunction of conjunctions. An empty inner group is always true; an empty outer list is never true.
    public sealed class ConditionSet
    {
        private readonly List<List<Condition>> groups = new List<List<Condition>>();

        public IList<List<Condition>> Groups { get { return groups; } }

        internal static ConditionSet ParseAny(List<object> raw, string context)
        {
            var set = new ConditionSet();
            for (int i = 0; i < raw.Count; i++)
            {
                var inner = raw[i] as List<object>;
                if (inner == null) throw new FormatException(context + ": whenAny entries must be arrays");
                var group = new List<Condition>();
                for (int j = 0; j < inner.Count; j++) group.Add(Condition.Parse(inner[j], context));
                set.groups.Add(group);
            }
            return set;
        }

        internal static ConditionSet ParseAll(List<object> raw, string context)
        {
            var set = new ConditionSet();
            var group = new List<Condition>();
            for (int j = 0; j < raw.Count; j++) group.Add(Condition.Parse(raw[j], context));
            set.groups.Add(group);
            return set;
        }

        public static ConditionSet Always()
        {
            var set = new ConditionSet();
            set.groups.Add(new List<Condition>());
            return set;
        }

        public bool IsMet(CampaignRules rules, CampaignState state)
        {
            return MatchingGroup(rules, state) >= 0;
        }

        public int MatchingGroup(CampaignRules rules, CampaignState state)
        {
            for (int i = 0; i < groups.Count; i++)
            {
                bool all = true;
                foreach (var c in groups[i]) { if (!c.IsMet(rules, state)) { all = false; break; } }
                if (all) return i;
            }
            return -1;
        }

        public string DescribeGroup(CampaignRules rules, CampaignState state, int index)
        {
            var g = groups[index];
            if (g.Count == 0) return "always";
            var parts = new List<string>();
            foreach (var c in g) parts.Add(c.Describe(rules, state));
            return string.Join(" and ", parts.ToArray());
        }

        public string DescribeAll(CampaignRules rules, CampaignState state)
        {
            var parts = new List<string>();
            for (int i = 0; i < groups.Count; i++) parts.Add(DescribeGroup(rules, state, i));
            return string.Join(" or ", parts.ToArray());
        }
    }

    public sealed class Effect
    {
        public string Stat { get; private set; }
        public int Delta { get; private set; }

        internal static Effect Parse(object raw, string context)
        {
            var o = Json.Obj(raw, context);
            var e = new Effect();
            e.Stat = Json.Str(o, "stat", context);
            e.Delta = Json.Int(o, "delta", context);
            return e;
        }
    }

    public sealed class ConditionalFlag
    {
        public string Flag { get; private set; }
        public ConditionSet When { get; private set; }

        internal static ConditionalFlag Parse(object raw, string context)
        {
            var o = Json.Obj(raw, context);
            var f = new ConditionalFlag();
            f.Flag = Json.Str(o, "flag", context);
            f.When = ConditionSet.ParseAll(Json.Arr(o, "whenAll", context), context);
            return f;
        }
    }

    public sealed class OwnershipTransfer
    {
        public string From { get; private set; }
        public string To { get; private set; }
        public int Percent { get; private set; }

        internal static OwnershipTransfer Parse(object raw, string context)
        {
            var o = Json.Obj(raw, context);
            var t = new OwnershipTransfer();
            t.From = Json.Str(o, "from", context);
            t.To = Json.Str(o, "to", context);
            t.Percent = Json.Int(o, "percent", context);
            return t;
        }
    }

    public sealed class OptionDef
    {
        public string Id { get; private set; }
        public string Label { get; private set; }
        public string Summary { get; private set; }
        public IList<Effect> Effects { get; private set; }
        public IList<string> SetFlags { get; private set; }
        public IList<ConditionalFlag> ConditionalFlags { get; private set; }
        public OwnershipTransfer Ownership { get; private set; }

        public int Cost(string stat)
        {
            int cost = 0;
            foreach (var e in Effects) if (e.Stat == stat && e.Delta < 0) cost -= e.Delta;
            return cost;
        }

        internal static OptionDef Parse(object raw, string context)
        {
            var o = Json.Obj(raw, context);
            var opt = new OptionDef();
            opt.Id = Json.Str(o, "id", context);
            string ctx = context + "/" + opt.Id;
            opt.Label = Json.Str(o, "label", ctx);
            opt.Summary = Json.Str(o, "summary", ctx);
            var effects = new List<Effect>();
            foreach (var e in Json.Arr(o, "effects", ctx)) effects.Add(Effect.Parse(e, ctx));
            opt.Effects = effects.AsReadOnly();
            var flags = new List<string>();
            foreach (var f in Json.OptArr(o, "setFlags"))
            {
                var s = f as string;
                if (s == null) throw new FormatException(ctx + ": setFlags must be strings");
                flags.Add(s);
            }
            opt.SetFlags = flags.AsReadOnly();
            var cflags = new List<ConditionalFlag>();
            foreach (var cf in Json.OptArr(o, "conditionalFlags")) cflags.Add(ConditionalFlag.Parse(cf, ctx));
            opt.ConditionalFlags = cflags.AsReadOnly();
            object own;
            if (o.TryGetValue("ownershipTransfer", out own) && own != null) opt.Ownership = OwnershipTransfer.Parse(own, ctx);
            return opt;
        }
    }

    public sealed class DecisionDef
    {
        public string Id { get; private set; }
        public int Chapter { get; private set; }
        public int Year { get; private set; }
        public string Title { get; private set; }
        public IList<OptionDef> Options { get; private set; }

        public OptionDef Option(string optionId)
        {
            foreach (var o in Options) if (o.Id == optionId) return o;
            return null;
        }

        internal static DecisionDef Parse(object raw)
        {
            var o = Json.Obj(raw, "decision");
            var d = new DecisionDef();
            d.Id = Json.Str(o, "id", "decision");
            d.Chapter = Json.Int(o, "chapter", d.Id);
            d.Year = Json.Int(o, "year", d.Id);
            d.Title = Json.Str(o, "title", d.Id);
            var opts = new List<OptionDef>();
            foreach (var x in Json.Arr(o, "options", d.Id)) opts.Add(OptionDef.Parse(x, d.Id));
            if (opts.Count < 2) throw new FormatException(d.Id + ": a decision needs at least two options");
            d.Options = opts.AsReadOnly();
            return d;
        }
    }

    public sealed class YearRule
    {
        public string Label { get; private set; }
        public string Stat { get; private set; }
        public int Delta { get; private set; }
        public ConditionSet When { get; private set; }

        internal static YearRule Parse(object raw, string context)
        {
            var o = Json.Obj(raw, context);
            var r = new YearRule();
            r.Label = Json.Str(o, "label", context);
            r.Stat = Json.Str(o, "stat", context);
            r.Delta = Json.Int(o, "delta", context);
            r.When = Json.Has(o, "whenAll") ? ConditionSet.ParseAll(Json.Arr(o, "whenAll", context), context) : ConditionSet.Always();
            return r;
        }
    }

    public sealed class YearTransitionDef
    {
        public string Id { get; private set; }
        public int Year { get; private set; }
        public IList<YearRule> Rules { get; private set; }

        internal static YearTransitionDef Parse(object raw)
        {
            var o = Json.Obj(raw, "yearTransition");
            var y = new YearTransitionDef();
            y.Id = Json.Str(o, "id", "yearTransition");
            y.Year = Json.Int(o, "year", y.Id);
            var rules = new List<YearRule>();
            foreach (var r in Json.Arr(o, "rules", y.Id)) rules.Add(YearRule.Parse(r, y.Id));
            y.Rules = rules.AsReadOnly();
            return y;
        }
    }

    public sealed class EvidenceCardDef
    {
        public string Id { get; private set; }
        public string Title { get; private set; }
        public string EarnedBy { get; private set; }
        public ConditionSet When { get; private set; }

        internal static EvidenceCardDef Parse(object raw)
        {
            var o = Json.Obj(raw, "evidenceCard");
            var c = new EvidenceCardDef();
            c.Id = Json.Str(o, "id", "evidenceCard");
            c.Title = Json.Str(o, "title", c.Id);
            c.EarnedBy = Json.Str(o, "earnedBy", c.Id);
            c.When = ConditionSet.ParseAny(Json.Arr(o, "whenAny", c.Id), c.Id);
            return c;
        }
    }

    public sealed class BlocDef
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public int Seats { get; private set; }

        internal static BlocDef Parse(object raw)
        {
            var o = Json.Obj(raw, "bloc");
            var b = new BlocDef();
            b.Id = Json.Str(o, "id", "bloc");
            b.Name = Json.Str(o, "name", b.Id);
            b.Seats = Json.Int(o, "seats", b.Id);
            return b;
        }
    }

    public sealed class SeatTier
    {
        public int Seats { get; private set; }
        public ConditionSet When { get; private set; }

        internal static SeatTier Parse(object raw, string context)
        {
            var o = Json.Obj(raw, context);
            var t = new SeatTier();
            t.Seats = Json.Int(o, "seats", context);
            t.When = ConditionSet.ParseAny(Json.Arr(o, "whenAny", context), context);
            return t;
        }
    }

    public sealed class CharterDef
    {
        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Ending { get; private set; }
        public string Summary { get; private set; }
        // Tiers per bloc, highest first; the first tier whose conditions are met decides the bloc's seats.
        public IDictionary<string, IList<SeatTier>> Votes { get; private set; }

        internal static CharterDef Parse(object raw)
        {
            var o = Json.Obj(raw, "charter");
            var c = new CharterDef();
            c.Id = Json.Str(o, "id", "charter");
            c.Title = Json.Str(o, "title", c.Id);
            c.Ending = Json.Str(o, "ending", c.Id);
            c.Summary = Json.Str(o, "summary", c.Id);
            var votes = new Dictionary<string, IList<SeatTier>>(StringComparer.Ordinal);
            foreach (var kv in Json.Obj(o["votes"], c.Id + "/votes"))
            {
                var tiers = new List<SeatTier>();
                var list = kv.Value as List<object>;
                if (list == null) throw new FormatException(c.Id + "/" + kv.Key + ": tiers must be an array");
                foreach (var t in list) tiers.Add(SeatTier.Parse(t, c.Id + "/" + kv.Key));
                votes[kv.Key] = tiers.AsReadOnly();
            }
            c.Votes = votes;
            return c;
        }
    }

    public sealed class FallbackDef
    {
        public string Ending { get; private set; }
        public string Reason { get; private set; }
        public ConditionSet When { get; private set; }

        internal static FallbackDef Parse(object raw)
        {
            var o = Json.Obj(raw, "fallback");
            var f = new FallbackDef();
            f.Ending = Json.Str(o, "ending", "fallback");
            f.Reason = Json.Str(o, "reason", f.Ending);
            f.When = ConditionSet.ParseAny(Json.Arr(o, "whenAny", f.Ending), f.Ending);
            return f;
        }
    }

    public sealed class EndingDef
    {
        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Summary { get; private set; }

        internal static EndingDef Parse(object raw)
        {
            var o = Json.Obj(raw, "ending");
            var e = new EndingDef();
            e.Id = Json.Str(o, "id", "ending");
            e.Title = Json.Str(o, "title", e.Id);
            e.Summary = Json.Str(o, "summary", e.Id);
            return e;
        }
    }

    // Immutable, validated rule table loaded from syntera_campaign_rules.json.
    public sealed class CampaignRules
    {
        public const string EvidenceStat = "evidence";
        public const string CouncilStepId = "ST11_COUNCIL";

        public int SchemaVersion { get; private set; }
        public string ScriptVersion { get; private set; }
        public IDictionary<string, int> StartStats { get; private set; }
        public IDictionary<string, int> StartOwnership { get; private set; }
        public ICollection<string> ClampedStats { get; private set; }
        public ICollection<string> FloorZeroStats { get; private set; }
        public int ClampMin { get; private set; }
        public int ClampMax { get; private set; }
        public int CreditLineAmount { get; private set; }
        public IList<Effect> CreditLineEffects { get; private set; }
        public IList<string> CreditLineFlags { get; private set; }
        public IList<string> Sequence { get; private set; }
        public IList<DecisionDef> Decisions { get; private set; }
        public IList<YearTransitionDef> YearTransitions { get; private set; }
        public IList<EvidenceCardDef> EvidenceCards { get; private set; }
        public IList<BlocDef> Blocs { get; private set; }
        public IList<CharterDef> Charters { get; private set; }
        public IList<FallbackDef> Fallbacks { get; private set; }
        public IList<EndingDef> Endings { get; private set; }
        public int PassSeats { get; private set; }
        private IDictionary<string, string> statLabels;

        public static CampaignRules Load(string json)
        {
            var root = Json.Obj(MiniJson.Parse(json), "rules");
            var r = new CampaignRules();
            r.SchemaVersion = Json.Int(root, "schemaVersion", "rules");
            if (r.SchemaVersion != 1) throw new FormatException("Unsupported rules schemaVersion " + r.SchemaVersion);
            r.ScriptVersion = Json.Str(root, "scriptVersion", "rules");

            var start = Json.Obj(root["start"], "start");
            r.StartStats = IntMap(Json.Obj(start["stats"], "start.stats"), "start.stats");
            r.StartOwnership = IntMap(Json.Obj(start["ownership"], "start.ownership"), "start.ownership");

            var limits = Json.Obj(root["limits"], "limits");
            r.ClampedStats = new HashSet<string>(StrList(Json.Arr(limits, "clampedStats", "limits")), StringComparer.Ordinal);
            r.FloorZeroStats = new HashSet<string>(StrList(Json.Arr(limits, "floorZeroStats", "limits")), StringComparer.Ordinal);
            r.ClampMin = Json.Int(limits, "clampMin", "limits");
            r.ClampMax = Json.Int(limits, "clampMax", "limits");

            r.statLabels = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in Json.Obj(root["statLabels"], "statLabels")) r.statLabels[kv.Key] = (string)kv.Value;

            var cl = Json.Obj(root["creditLine"], "creditLine");
            r.CreditLineAmount = Json.Int(cl, "amount", "creditLine");
            var clEffects = new List<Effect>();
            foreach (var e in Json.Arr(cl, "effects", "creditLine")) clEffects.Add(Effect.Parse(e, "creditLine"));
            r.CreditLineEffects = clEffects.AsReadOnly();
            r.CreditLineFlags = StrList(Json.OptArr(cl, "setFlags")).AsReadOnly();

            r.Sequence = StrList(Json.Arr(root, "sequence", "rules")).AsReadOnly();

            var decisions = new List<DecisionDef>();
            foreach (var d in Json.Arr(root, "decisions", "rules")) decisions.Add(DecisionDef.Parse(d));
            r.Decisions = decisions.AsReadOnly();

            var years = new List<YearTransitionDef>();
            foreach (var y in Json.Arr(root, "yearTransitions", "rules")) years.Add(YearTransitionDef.Parse(y));
            r.YearTransitions = years.AsReadOnly();

            var cards = new List<EvidenceCardDef>();
            foreach (var c in Json.Arr(root, "evidenceCards", "rules")) cards.Add(EvidenceCardDef.Parse(c));
            r.EvidenceCards = cards.AsReadOnly();

            var council = Json.Obj(root["council"], "council");
            r.PassSeats = Json.Int(council, "passSeats", "council");
            var blocs = new List<BlocDef>();
            foreach (var b in Json.Arr(council, "blocs", "council")) blocs.Add(BlocDef.Parse(b));
            r.Blocs = blocs.AsReadOnly();
            var charters = new List<CharterDef>();
            foreach (var c in Json.Arr(council, "charters", "council")) charters.Add(CharterDef.Parse(c));
            r.Charters = charters.AsReadOnly();
            var fallbacks = new List<FallbackDef>();
            foreach (var f in Json.Arr(council, "fallbacks", "council")) fallbacks.Add(FallbackDef.Parse(f));
            r.Fallbacks = fallbacks.AsReadOnly();

            var endings = new List<EndingDef>();
            foreach (var e in Json.Arr(root, "endings", "rules")) endings.Add(EndingDef.Parse(e));
            r.Endings = endings.AsReadOnly();

            r.Validate();
            return r;
        }

        private void Validate()
        {
            var known = new HashSet<string>(StartStats.Keys, StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in Sequence)
            {
                if (!seen.Add(step)) throw new FormatException("Sequence repeats step " + step);
                if (step == CouncilStepId) continue;
                if (FindDecision(step) == null && FindYear(step) == null) throw new FormatException("Sequence step " + step + " has no decision or year transition");
            }
            if (Sequence.Count == 0 || Sequence[Sequence.Count - 1] != CouncilStepId) throw new FormatException("Sequence must end with " + CouncilStepId);
            foreach (var d in Decisions)
            {
                if (!seen.Contains(d.Id)) throw new FormatException("Decision " + d.Id + " is not in the sequence");
                foreach (var o in d.Options)
                    foreach (var e in o.Effects)
                        if (!known.Contains(e.Stat)) throw new FormatException(d.Id + "/" + o.Id + ": unknown stat " + e.Stat);
            }
            foreach (var y in YearTransitions)
                foreach (var rule in y.Rules)
                    if (!known.Contains(rule.Stat)) throw new FormatException(y.Id + ": unknown stat " + rule.Stat);
            foreach (var e in CreditLineEffects)
                if (!known.Contains(e.Stat)) throw new FormatException("creditLine: unknown stat " + e.Stat);
            int seats = 0;
            foreach (var b in Blocs) seats += b.Seats;
            if (PassSeats <= seats / 2 || PassSeats > seats) throw new FormatException("passSeats must be a majority of council seats");
            foreach (var c in Charters)
            {
                if (FindEnding(c.Ending) == null) throw new FormatException(c.Id + ": unknown ending " + c.Ending);
                foreach (var b in Blocs)
                {
                    IList<SeatTier> tiers;
                    if (!c.Votes.TryGetValue(b.Id, out tiers)) throw new FormatException(c.Id + ": no vote rule for bloc " + b.Id);
                    foreach (var t in tiers)
                        if (t.Seats < 0 || t.Seats > b.Seats) throw new FormatException(c.Id + "/" + b.Id + ": tier seats exceed bloc size");
                }
            }
            if (Fallbacks.Count == 0) throw new FormatException("council needs at least one fallback");
            foreach (var f in Fallbacks)
                if (FindEnding(f.Ending) == null) throw new FormatException("fallback: unknown ending " + f.Ending);
        }

        public DecisionDef FindDecision(string id)
        {
            foreach (var d in Decisions) if (d.Id == id) return d;
            return null;
        }

        public YearTransitionDef FindYear(string id)
        {
            foreach (var y in YearTransitions) if (y.Id == id) return y;
            return null;
        }

        public CharterDef FindCharter(string id)
        {
            foreach (var c in Charters) if (c.Id == id) return c;
            return null;
        }

        public EndingDef FindEnding(string id)
        {
            foreach (var e in Endings) if (e.Id == id) return e;
            return null;
        }

        public string StatLabel(string stat)
        {
            string label;
            return statLabels.TryGetValue(stat, out label) ? label : stat;
        }

        public int CountEvidence(CampaignState state)
        {
            int n = 0;
            foreach (var c in EvidenceCards) if (c.When.IsMet(this, state)) n++;
            return n;
        }

        public int ReadStat(CampaignState state, string stat)
        {
            if (stat == EvidenceStat) return CountEvidence(state);
            return state.Get(stat);
        }

        public int Bound(string stat, int value)
        {
            if (ClampedStats.Contains(stat)) return Math.Max(ClampMin, Math.Min(ClampMax, value));
            if (FloorZeroStats.Contains(stat)) return Math.Max(0, value);
            return value;
        }

        private static Dictionary<string, int> IntMap(Dictionary<string, object> o, string context)
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var kv in o) map[kv.Key] = Json.Int(o, kv.Key, context);
            return map;
        }

        private static List<string> StrList(List<object> raw)
        {
            var list = new List<string>();
            foreach (var x in raw)
            {
                var s = x as string;
                if (s == null) throw new FormatException("expected a list of strings");
                list.Add(s);
            }
            return list;
        }

        internal static string Num(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
