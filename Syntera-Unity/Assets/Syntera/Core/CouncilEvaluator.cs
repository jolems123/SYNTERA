using System;
using System.Collections.Generic;

namespace Syntera.Core
{
    public sealed class BlocVote
    {
        public string BlocId { get; internal set; }
        public string BlocName { get; internal set; }
        public int SeatsAvailable { get; internal set; }
        public int SeatsFor { get; internal set; }
        // The exact condition that decided this bloc's vote, for the transparent seat display.
        public string Reason { get; internal set; }
    }

    public sealed class CouncilResult
    {
        public string CharterId { get; internal set; }
        public string CharterTitle { get; internal set; }
        public IList<BlocVote> Votes { get; internal set; }
        public int SeatsFor { get; internal set; }
        public int TotalSeats { get; internal set; }
        public int PassSeats { get; internal set; }
        public bool Passed { get; internal set; }
        public int EvidenceCount { get; internal set; }
        public string EndingId { get; internal set; }
        public string EndingReason { get; internal set; }
    }

    // Deterministic council vote (screenplay section 6). No randomness, no live model.
    public static class CouncilEvaluator
    {
        public static CouncilResult Evaluate(CampaignRules rules, CampaignState state, string charterId)
        {
            var charter = rules.FindCharter(charterId);
            if (charter == null) throw new ArgumentException("Unknown charter " + charterId);

            var votes = new List<BlocVote>();
            int seatsFor = 0, total = 0;
            foreach (var bloc in rules.Blocs)
            {
                total += bloc.Seats;
                var tiers = charter.Votes[bloc.Id];
                var vote = new BlocVote { BlocId = bloc.Id, BlocName = bloc.Name, SeatsAvailable = bloc.Seats };
                bool decided = false;
                foreach (var tier in tiers)
                {
                    int g = tier.When.MatchingGroup(rules, state);
                    if (g < 0) continue;
                    vote.SeatsFor = tier.Seats;
                    vote.Reason = tier.Seats + " of " + bloc.Seats + " for: " + tier.When.DescribeGroup(rules, state, g);
                    decided = true;
                    break;
                }
                if (!decided)
                {
                    vote.SeatsFor = 0;
                    var unmet = new List<string>();
                    foreach (var tier in tiers) unmet.Add(tier.Seats + " seat(s) needs " + tier.When.DescribeAll(rules, state));
                    vote.Reason = "0 of " + bloc.Seats + " against. " + (unmet.Count == 0 ? "This bloc never supports this charter." : "Would need: " + string.Join("; ", unmet.ToArray()));
                }
                seatsFor += vote.SeatsFor;
                votes.Add(vote);
            }

            var result = new CouncilResult
            {
                CharterId = charter.Id,
                CharterTitle = charter.Title,
                Votes = votes.AsReadOnly(),
                SeatsFor = seatsFor,
                TotalSeats = total,
                PassSeats = rules.PassSeats,
                Passed = seatsFor >= rules.PassSeats,
                EvidenceCount = rules.CountEvidence(state)
            };

            if (result.Passed)
            {
                result.EndingId = charter.Ending;
                result.EndingReason = charter.Title + " passed with " + seatsFor + " of " + total + " seats.";
            }
            else
            {
                foreach (var f in rules.Fallbacks)
                {
                    int g = f.When.MatchingGroup(rules, state);
                    if (g < 0) continue;
                    result.EndingId = f.Ending;
                    result.EndingReason = charter.Title + " failed with " + seatsFor + " of " + total + " seats (needs " + rules.PassSeats + "). " + f.Reason;
                    break;
                }
                if (result.EndingId == null) throw new InvalidOperationException("No council fallback matched; rules need an unconditional fallback.");
            }
            return result;
        }
    }
}
