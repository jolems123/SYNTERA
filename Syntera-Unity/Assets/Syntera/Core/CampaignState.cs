using System;
using System.Collections.Generic;

namespace Syntera.Core
{
    // Mutable campaign state. Only CampaignEngine transactions should change it.
    public sealed class CampaignState
    {
        private readonly Dictionary<string, int> stats = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> ownership = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> flags = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<KeyValuePair<string, string>> choices = new List<KeyValuePair<string, string>>();

        public int StepIndex { get; internal set; }
        public string CharterId { get; internal set; }
        public string EndingId { get; internal set; }

        public static CampaignState New(CampaignRules rules)
        {
            var s = new CampaignState();
            foreach (var kv in rules.StartStats) s.stats[kv.Key] = kv.Value;
            foreach (var kv in rules.StartOwnership) s.ownership[kv.Key] = kv.Value;
            return s;
        }

        public int Get(string stat)
        {
            int v;
            if (!stats.TryGetValue(stat, out v)) throw new KeyNotFoundException("Unknown stat " + stat);
            return v;
        }

        internal void Set(string stat, int value)
        {
            if (!stats.ContainsKey(stat)) throw new KeyNotFoundException("Unknown stat " + stat);
            stats[stat] = value;
        }

        public bool HasFlag(string flag) { return flags.Contains(flag); }
        internal void AddFlag(string flag) { flags.Add(flag); }

        public int OwnershipOf(string holder)
        {
            int v;
            return ownership.TryGetValue(holder, out v) ? v : 0;
        }

        internal void SetOwnership(string holder, int percent) { ownership[holder] = percent; }

        internal void RecordChoice(string decisionId, string optionId)
        {
            choices.Add(new KeyValuePair<string, string>(decisionId, optionId));
        }

        public IEnumerable<string> StatNames { get { return stats.Keys; } }
        public IEnumerable<string> Flags { get { return flags; } }
        public IDictionary<string, int> Ownership { get { return new Dictionary<string, int>(ownership); } }
        public IList<KeyValuePair<string, string>> Choices { get { return choices.AsReadOnly(); } }

        public string ChoiceFor(string decisionId)
        {
            foreach (var c in choices) if (c.Key == decisionId) return c.Value;
            return null;
        }

        public CampaignState Clone()
        {
            var s = new CampaignState();
            foreach (var kv in stats) s.stats[kv.Key] = kv.Value;
            foreach (var kv in ownership) s.ownership[kv.Key] = kv.Value;
            foreach (var f in flags) s.flags.Add(f);
            s.choices.AddRange(choices);
            s.StepIndex = StepIndex;
            s.CharterId = CharterId;
            s.EndingId = EndingId;
            return s;
        }
    }
}
