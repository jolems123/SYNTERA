using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Syntera.Core;
using UnityEngine;

namespace Syntera.Core.Tests
{
    // Headless 2026-2028 playthroughs through the same director the Unity game uses.
    public class CampaignDirectorTests
    {
        private static CampaignRules rules;
        private static DialogueLibrary library;

        private static string Resource(string name)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "Syntera/Resources/" + name));
        }

        private static CampaignRules Rules { get { return rules ?? (rules = CampaignRules.Load(Resource("syntera_campaign_rules.json"))); } }
        private static DialogueLibrary Library { get { return library ?? (library = DialogueLibrary.Load(Resource("syntera_campaign_dialogue.json"), Rules)); } }

        private static CampaignDirector NewDirector()
        {
            return new CampaignDirector(new CampaignEngine(Rules), Library);
        }

        // Plays to completion. Intent letters per decision; unaffordable intents take the
        // credit line when offered, then fall back to B (the section 14 policy).
        // When reloadEveryStep is set, the game is saved and reloaded after every action.
        private static CampaignDirector Play(string intent, string charter, bool reloadEveryStep, List<string> spoken = null)
        {
            var d = NewDirector();
            int decision = 0, guard = 0;
            while (d.Phase != DirectorPhase.Complete)
            {
                if (++guard > 2000) Assert.Fail("Playthrough did not finish; stuck in " + d.Phase + " at " + d.Chapter.Id);
                switch (d.Phase)
                {
                    case DirectorPhase.ChapterIntro: d.BeginChapter(); break;
                    case DirectorPhase.FieldWork:
                        if (d.NextObjective != null) Assert.IsTrue(d.CompleteObjective(d.NextObjective.Id));
                        else Assert.IsTrue(d.StartConversation());
                        break;
                    case DirectorPhase.Conversation:
                    case DirectorPhase.Reaction:
                    case DirectorPhase.CharterSpeech:
                    case DirectorPhase.Ending:
                        if (spoken != null) spoken.Add(d.CurrentLine.Id);
                        d.Advance();
                        break;
                    case DirectorPhase.Decision:
                        string want = intent[decision++].ToString();
                        var status = d.Status(want);
                        if (!status.Affordable && status.CreditLineOffered) Assert.IsTrue(d.TakeCreditLine(want).Committed);
                        if (!d.Status(want).Affordable) want = "B";
                        Assert.IsTrue(d.Choose(want).Committed, d.Chapter.Id + " " + want);
                        break;
                    case DirectorPhase.YearSummary: d.ContinueFromYearSummary(); break;
                    case DirectorPhase.Evidence: d.ContinueFromEvidence(); break;
                    case DirectorPhase.CharterChoice: Assert.IsTrue(d.ProposeCharter(charter).Committed); break;
                    case DirectorPhase.VoteResult: d.ContinueFromVote(); break;
                    case DirectorPhase.Recap: Assert.IsNotNull(d.Recap()); d.FinishRecap(); break;
                }
                if (reloadEveryStep)
                {
                    string json = CampaignSave.Write(d, "Tester", guard);
                    var loaded = CampaignSave.Read(json, Rules, Library).Director;
                    Assert.AreEqual(json, CampaignSave.Write(loaded, "Tester", guard), "save round-trip changed state at " + d.Chapter.Id + " " + d.Phase);
                    d = loaded;
                }
            }
            return d;
        }

        [Test]
        public void AllAShared_PlaysToSharedDawn()
        {
            var spoken = new List<string>();
            var d = Play("AAAAAAAAAA", "shared", false, spoken);
            var recap = d.Recap();
            Assert.AreEqual("SharedDawn", recap.Ending.Id);
            Assert.AreEqual(5, recap.Vote.SeatsFor);
            Assert.AreEqual(10, recap.Choices.Count);
            Assert.IsTrue(recap.UsedCreditLine);
            Assert.IsTrue(spoken.Contains("ST01_NOAH_001"), "opening dialogue played");
            Assert.AreEqual("You bought us time to find a better way. Do not waste it.", Library.Line("ST04_NOAH_006").Text);
            Assert.IsTrue(spoken.Contains("ST04_NOAH_006"), "section 8 conditional (protectedWorkers) played in CH04");
            Assert.IsFalse(spoken.Contains("ST04_NOAH_005"), "only one variant of a conditional beat plays");
            Assert.IsTrue(spoken.Contains("ST12_AMARA_001"), "Shared Dawn ending played");
            Assert.IsTrue(d.Engine.Log.Verify());
        }

        [Test]
        public void AllBCompact_PlaysToCorporateSunrise()
        {
            var d = Play("BBBBBBBBBB", "compact", false);
            Assert.AreEqual("CorporateSunrise", d.Recap().Ending.Id);
            Assert.AreEqual(5, d.Recap().Vote.SeatsFor);
        }

        [Test]
        public void FailedCovenantFallsBackToHumanFrontier()
        {
            var d = Play("AAAAAAAAAA", "covenant", false);
            Assert.IsFalse(d.Recap().Vote.Passed);
            Assert.AreEqual("HumanFrontier", d.Recap().Ending.Id);
        }

        [Test]
        public void SaveAndReloadAtEveryStepReproducesTheRun()
        {
            var straight = Play("ABABABABAB", "shared", false);
            var reloaded = Play("ABABABABAB", "shared", true);
            Assert.AreEqual(straight.Engine.Log.HeadHash, reloaded.Engine.Log.HeadHash);
            Assert.AreEqual(straight.Recap().Ending.Id, reloaded.Recap().Ending.Id);
        }

        [Test]
        public void ObjectivesMustBeDoneInOrderBeforeTalking()
        {
            var d = NewDirector();
            d.BeginChapter();
            Assert.IsFalse(d.StartConversation(), "contact waits for objectives");
            Assert.IsFalse(d.CompleteObjective("ST01_OBJ_INSPECT"), "out of order");
            Assert.IsTrue(d.CompleteObjective("ST01_OBJ_RESCUE"));
            Assert.IsTrue(d.CompleteObjective("ST01_OBJ_INSPECT"));
            Assert.IsTrue(d.StartConversation());
            Assert.AreEqual("ST01_CITYANNOUNCER_001", d.CurrentLine.Id);
        }

        [Test]
        public void YearSummaryShowsDividendAndTax()
        {
            var d = NewDirector();
            int guard = 0;
            while (d.Phase != DirectorPhase.YearSummary && ++guard < 500)
            {
                if (d.Phase == DirectorPhase.ChapterIntro) d.BeginChapter();
                else if (d.Phase == DirectorPhase.FieldWork) { if (d.NextObjective != null) d.CompleteObjective(d.NextObjective.Id); else d.StartConversation(); }
                else if (d.Phase == DirectorPhase.Decision) d.Choose("B");
                else d.Advance();
            }
            Assert.AreEqual(DirectorPhase.YearSummary, d.Phase);
            StringAssert.Contains("Loopline dividend", d.YearSummary);
            StringAssert.Contains("City tax receipts", d.YearSummary);
            d.ContinueFromYearSummary();
            Assert.AreEqual("ST05", d.Chapter.Id);
        }

        [Test]
        public void TamperedSaveIsRejected()
        {
            var d = NewDirector();
            d.BeginChapter();
            d.CompleteObjective("ST01_OBJ_RESCUE");
            d.CompleteObjective("ST01_OBJ_INSPECT");
            d.StartConversation();
            while (d.Phase == DirectorPhase.Conversation) d.Advance();
            d.Choose("B");
            string json = CampaignSave.Write(d, "Tester", 10);
            CampaignSave.Read(json, Rules, Library);

            AssertRejected(json.Replace("\"ST01_STRIKE\",\"B\"", "\"ST01_STRIKE\",\"A\""), "rewritten choice");
            AssertRejected(json.Replace("\"phase\":\"Reaction\"", "\"phase\":\"Decision\""), "phase out of step with record");
            AssertRejected(json.Replace("\"format\":2", "\"format\":3"), "unknown format");
        }

        private static void AssertRejected(string json, string why)
        {
            try { CampaignSave.Read(json, Rules, Library); }
            catch (FormatException) { return; }
            Assert.Fail("Tampered save accepted: " + why);
        }
    }
}
