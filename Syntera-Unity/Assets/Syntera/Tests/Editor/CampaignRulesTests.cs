using System.IO;
using NUnit.Framework;
using Syntera.Core;
using UnityEngine;

namespace Syntera.Core.Tests
{
    // Screenplay v1.1 section 14: the in-engine results must match the published simulation.
    public class CampaignRulesTests
    {
        private static CampaignRules rules;

        private static CampaignRules Rules
        {
            get
            {
                if (rules == null)
                {
                    string path = Path.Combine(Application.dataPath, "Syntera/Resources/syntera_campaign_rules.json");
                    rules = CampaignRules.Load(File.ReadAllText(path));
                }
                return rules;
            }
        }

        [Test]
        public void RulesLoadAndValidate()
        {
            Assert.AreEqual("1.1", Rules.ScriptVersion);
            Assert.AreEqual(10, Rules.Decisions.Count);
            Assert.AreEqual(9, Rules.Blocs[0].Seats + Rules.Blocs[1].Seats + Rules.Blocs[2].Seats + Rules.Blocs[3].Seats);
            Assert.AreEqual(5, Rules.PassSeats);
        }

        [Test]
        public void Section14_ExhaustiveSimulationMatchesScreenplay()
        {
            var s = CampaignSimulator.RunAll(Rules);
            Assert.AreEqual(1024, s.Paths);
            Assert.AreEqual(242, s.CharterPasses["shared"], "Shared Charter passes");
            Assert.AreEqual(466, s.CharterPasses["compact"], "Optimization Compact passes");
            Assert.AreEqual(348, s.CharterPasses["covenant"], "Human Covenant passes");
            Assert.AreEqual(280, s.NoCharterPasses, "paths where no charter can pass");
            Assert.AreEqual(922, s.AllIntendedAffordable, "paths where every intended choice was affordable");
            Assert.AreEqual(32, s.UsedCreditLine, "paths that use Naomi's credit line");
            Assert.AreEqual(9, s.MaxIntendedOptionsTaken, "most A options affordable on one path");
            Assert.AreEqual(0, s.NegativeResourcePaths, "no path produces negative Credits or Treasury");
            Assert.AreEqual(0, s.AuditFailures, "every path's audit chain verifies");
        }

        [Test]
        public void Section14_AllAIntent()
        {
            var o = CampaignSimulator.RunIntent(Rules, "AAAAAAAAAA");
            Assert.AreEqual("AAAAAABAAB", o.Actual, "CH07 housing privacy and CH10 co-ownership are forced to B");
            Assert.IsTrue(o.UsedCreditLine);
            AssertCouncil(o, "shared", 5, "SharedDawn");
            AssertCouncil(o, "compact", 3, "HumanFrontier");
            AssertCouncil(o, "covenant", 2, "HumanFrontier");
        }

        [Test]
        public void Section14_AllBIntent()
        {
            var o = CampaignSimulator.RunIntent(Rules, "BBBBBBBBBB");
            Assert.AreEqual("BBBBBBBBBB", o.Actual);
            AssertCouncil(o, "shared", 3, "CorporateSunrise");
            AssertCouncil(o, "compact", 5, "CorporateSunrise");
            AssertCouncil(o, "covenant", 2, "CorporateSunrise");
        }

        [Test]
        public void UnaffordableOptionIsRejectedWithExactShortfall()
        {
            var engine = new CampaignEngine(Rules);
            Play(engine, "AAAA");               // CH01-CH04 all A
            Assert.IsTrue(engine.AdvanceYear("Y2027").Committed);
            Assert.IsTrue(engine.Choose("ST05_INTRO", "A").Committed);
            Assert.IsTrue(engine.Choose("ST06_BLACKOUT", "A").Committed);
            var status = engine.Evaluate("ST07_HOUSING", "A");
            Assert.IsFalse(status.Affordable);
            Assert.AreEqual(10, status.TreasuryShortfall);
            Assert.IsFalse(status.CreditLineOffered, "the credit line never covers Treasury");
            StringAssert.Contains("short 10", status.Explanation);
            var rejected = engine.Choose("ST07_HOUSING", "A");
            Assert.IsFalse(rejected.Committed);
            Assert.AreEqual("ST07_HOUSING", engine.CurrentStepId, "a rejected choice does not advance the mission");
        }

        [Test]
        public void CreditLineIsOfferedOnceAndCostsAutonomy()
        {
            var engine = new CampaignEngine(Rules);
            Play(engine, "AAAA");
            engine.AdvanceYear("Y2027");
            engine.Choose("ST05_INTRO", "A");
            engine.Choose("ST06_BLACKOUT", "A");
            engine.Choose("ST07_HOUSING", "B");
            engine.Choose("ST08_LEDGER", "A");
            engine.Choose("ST09_RALLY", "A");
            engine.AdvanceYear("Y2028");
            var status = engine.Evaluate("ST10_FRIEND", "A");
            Assert.IsTrue(status.CreditLineOffered);
            int autonomy = engine.State.Get("autonomy");
            Assert.IsTrue(engine.TakeCreditLine("ST10_FRIEND", "A").Committed);
            Assert.AreEqual(autonomy - 4, engine.State.Get("autonomy"));
            Assert.IsFalse(engine.Evaluate("ST10_FRIEND", "A").CreditLineOffered, "offered once per run");
            Assert.IsFalse(engine.TakeCreditLine("ST10_FRIEND", "A").Committed);
        }

        [Test]
        public void OutOfOrderTransactionsAreRejected()
        {
            var engine = new CampaignEngine(Rules);
            Assert.IsFalse(engine.Choose("ST02_CONSENT", "A").Committed);
            Assert.IsFalse(engine.AdvanceYear("Y2027").Committed);
            Assert.IsFalse(engine.ProposeCharter("shared").Committed);
            Assert.AreEqual(150, engine.State.Get("credits"), "rejected transactions change nothing");
        }

        [Test]
        public void RiderCoOwnershipMovesTheLedgerAndReconcilesKato()
        {
            var o = CampaignSimulator.RunIntent(Rules, "AAABBBBBBA");
            Assert.AreEqual('A', o.Actual[9]);
            Assert.AreEqual(50, o.FinalState.OwnershipOf("ridersTrust"));
            Assert.AreEqual(0, o.FinalState.OwnershipOf("outsideInvestors"));
            Assert.AreEqual(o.FinalState.Get("kato") >= 55, o.FinalState.HasFlag("katoReconciled"));
        }

        [Test]
        public void AuditChainDetectsTampering()
        {
            var engine = new CampaignEngine(Rules);
            engine.Choose("ST01_STRIKE", "A");
            Assert.IsTrue(engine.Log.Verify());
            var rows = new System.Collections.Generic.List<string[]>();
            foreach (var e in engine.Log.Events) rows.Add(new[] { e.Kind, e.StepId, e.Detail });
            var restored = AuditLog.Restore(rows);
            Assert.AreEqual(engine.Log.HeadHash, restored.HeadHash, "same events reproduce the same chain");
            rows[1][2] = rows[1][2].Replace("credits -60", "credits +60");
            Assert.AreNotEqual(engine.Log.HeadHash, AuditLog.Restore(rows).HeadHash, "edited history changes the head hash");
        }

        [Test]
        public void FailedVoteExplainsEveryBloc()
        {
            var o = CampaignSimulator.RunIntent(Rules, "AAAAAAAAAA");
            var covenant = o.CouncilByCharter["covenant"];
            Assert.IsFalse(covenant.Passed);
            Assert.AreEqual(4, covenant.Votes.Count);
            foreach (var v in covenant.Votes) Assert.IsFalse(string.IsNullOrEmpty(v.Reason), v.BlocName + " has no reason");
            StringAssert.Contains("defaults to human control", covenant.EndingReason);
        }

        private static void Play(CampaignEngine engine, string letters)
        {
            foreach (char c in letters)
                Assert.IsTrue(engine.Choose(engine.CurrentStepId, c.ToString()).Committed, "choice at " + engine.CurrentStepId);
        }

        private static void AssertCouncil(PathOutcome o, string charter, int seats, string ending)
        {
            var r = o.CouncilByCharter[charter];
            Assert.AreEqual(seats, r.SeatsFor, charter + " seats");
            Assert.AreEqual(ending, r.EndingId, charter + " ending");
        }
    }
}
