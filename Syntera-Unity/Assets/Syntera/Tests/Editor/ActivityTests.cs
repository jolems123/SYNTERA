using System;
using System.IO;
using NUnit.Framework;
using Syntera.Core;
using UnityEngine;

namespace Syntera.Core.Tests
{
    public class ActivityTests
    {
        private static CampaignRules rules;
        private static DialogueLibrary dialogue;
        private static ActivityLibrary activities;

        private static string Resource(string name)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "Syntera/Resources/" + name));
        }

        private static void Load()
        {
            if (activities != null) return;
            rules = CampaignRules.Load(Resource("syntera_campaign_rules.json"));
            dialogue = DialogueLibrary.Load(Resource("syntera_campaign_dialogue.json"), rules);
            activities = ActivityLibrary.Load(Resource("syntera_activities.json"), dialogue);
        }

        private static ActivitySession Session(string id)
        {
            Load();
            return new ActivitySession(activities.Get(id), rules, CampaignState.New(rules));
        }

        [Test]
        public void EveryObjectiveActivityExistsAndSolves()
        {
            Load();
            int linked = 0;
            foreach (var ch in dialogue.Chapters)
                foreach (var ob in ch.Objectives)
                {
                    if (ob.Activity == null) continue;
                    linked++;
                    var s = Session(ob.Activity);
                    s.SolveForTest();
                    Assert.IsTrue(s.Complete, ob.Activity + " did not solve: " + s.Feedback);
                }
            Assert.AreEqual(activities.Count, linked, "every activity is used by exactly one objective");
            Assert.IsTrue(linked >= 19);
        }

        [Test]
        public void WrongAnswersCountAndUnlockAssist()
        {
            var s = Session("ST04_ACT_MATCH");
            for (int i = 0; i < 3; i++) { s.Assign(0, "Customer service"); Assert.IsFalse(s.SubmitMatch()); }
            Assert.IsTrue(s.AssistAvailable, "assist after three failures");
            s.Assign(1, "Motor maintenance");
            s.Assign(2, "Customer service");
            s.Assist();
            Assert.IsTrue(s.SubmitMatch(), "assist fixed the misread learner");
        }

        [Test]
        public void ClauseReviewRejectsFlaggingEverything()
        {
            var s = Session("ST07_ACT_CLAUSES");
            for (int i = 0; i < s.Def.Entries.Count; i++) s.Toggle(i);
            Assert.IsFalse(s.SubmitSelect());
            StringAssert.Contains("Too many", s.Feedback);
        }

        [Test]
        public void GridAllocationMustPowerHospitalWithinCapacity()
        {
            var s = Session("ST06_ACT_ALLOCATE");
            s.Adjust(1, 5);
            Assert.IsFalse(s.SubmitAllocation(), "hospital unpowered");
            s.Adjust(0, 3);
            s.Adjust(2, 3);
            s.Adjust(3, 2);
            Assert.AreEqual(130, s.Allocated);
            Assert.IsFalse(s.SubmitAllocation(), "over capacity");
            s.Adjust(3, -2);
            s.Adjust(2, -1);
            Assert.IsTrue(s.SubmitAllocation(), s.Feedback);
        }

        [Test]
        public void RallySpeechesDependOnRecord()
        {
            Load();
            var def = activities.Get("ST09_ACT_RALLY");
            var fresh = new ActivitySession(def, rules, CampaignState.New(rules));
            Assert.IsFalse(fresh.SpeechAvailable(0), "riders speech needs protectedWorkers");
            Assert.IsTrue(fresh.SpeechAvailable(3), "honest speech always available");
            var allA = CampaignSimulator.RunIntent(rules, "AAAAAAAAAA").FinalState;
            Assert.IsTrue(new ActivitySession(def, rules, allA).SpeechAvailable(0));
        }

        [Test]
        public void RiderBudgetPagesReflectEarlierChoices()
        {
            Load();
            var def = activities.Get("ST10_ACT_BUDGET");
            var a = new ActivitySession(def, rules, CampaignSimulator.RunIntent(rules, "AAAAAAAAAA").FinalState);
            var b = new ActivitySession(def, rules, CampaignSimulator.RunIntent(rules, "BBBBBBBBBB").FinalState);
            StringAssert.Contains("kept their shift", a.PageText(0));
            StringAssert.Contains("lost their shifts", b.PageText(0));
        }

        [Test]
        public void RescueByDroneCostsTrustOnceAndSurvivesReload()
        {
            Load();
            var d = new CampaignDirector(new CampaignEngine(rules), dialogue);
            d.BeginChapter();
            Assert.IsTrue(d.ApplyOutcome("ST01_RESCUE_BY_DRONE").Committed);
            Assert.AreEqual(48, d.Engine.State.Get("trust"));
            Assert.IsFalse(d.ApplyOutcome("ST01_RESCUE_BY_DRONE").Committed, "applies once");
            d.CompleteObjective("ST01_OBJ_RESCUE");
            var reloaded = CampaignSave.Read(CampaignSave.Write(d, "T", 1), rules, dialogue).Director;
            Assert.AreEqual(48, reloaded.Engine.State.Get("trust"));
        }
    }
}
