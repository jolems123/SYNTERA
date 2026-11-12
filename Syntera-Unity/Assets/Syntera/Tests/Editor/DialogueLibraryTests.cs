using System.IO;
using NUnit.Framework;
using Syntera.Core;
using UnityEngine;

namespace Syntera.Core.Tests
{
    public class DialogueLibraryTests
    {
        private static CampaignRules rules;
        private static DialogueLibrary library;

        private static string Resource(string name)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "Syntera/Resources/" + name));
        }

        private static CampaignRules Rules
        {
            get { return rules ?? (rules = CampaignRules.Load(Resource("syntera_campaign_rules.json"))); }
        }

        private static DialogueLibrary Library
        {
            get { return library ?? (library = DialogueLibrary.Load(Resource("syntera_campaign_dialogue.json"), Rules)); }
        }

        [Test]
        public void AllTwelveChaptersLoadWithStableIds()
        {
            Assert.AreEqual(12, Library.Chapters.Count);
            Assert.AreEqual(210, Library.LineCount, "line count changed: new lines need new IDs, never renumbering");
            Assert.AreEqual("Hey! You! They locked the maintenance bridge while I was still on it!", Library.Line("ST01_NOAH_001").Text);
            Assert.AreEqual("A cactus has been nominated. Its name is Management.", Library.Line("ST01_PIP04_002").Text);
            Assert.AreEqual("Kato Mercer", Library.Line("ST01_KATO_001").SpeakerName);
        }

        [Test]
        public void EveryDecisionHasChapterAndBothOptionReactions()
        {
            foreach (var d in Rules.Decisions)
            {
                var chapter = Library.ChapterForDecision(d.Id);
                Assert.IsTrue(chapter != null, d.Id);
                foreach (var o in d.Options) Assert.IsTrue(chapter.OptionDialogue[o.Id].Count > 0, d.Id + "/" + o.Id);
            }
        }

        [Test]
        public void ConditionalBeatsPickExactlyOneVariant()
        {
            var o = CampaignSimulator.RunIntent(Rules, "AAAAAAAAAA");
            Assert.AreEqual("You bought us time to find a better way. Do not waste it.",
                Library.SelectConditional(Rules, o.FinalState, "ST04", "noahLayoff").Text);
            Assert.AreEqual("You traded privacy for keys. Some of them were happy to. I am not.",
                Library.SelectConditional(Rules, o.FinalState, "ST10", "amaraHousing").Text, "all-A is forced out of housing privacy");
            Assert.IsTrue(Library.SelectConditional(Rules, o.FinalState, "ST07", "mamaSarahFlat") == null, "no neutral line is authored");

            var b = CampaignSimulator.RunIntent(Rules, "BBBBBBBBBB");
            Assert.AreEqual("A clinic courier argues with a drone that delivered vaccines to a wedding hall.",
                Library.SelectConditional(Rules, b.FinalState, "ST09", "clinicDrone").Text);
        }

        [Test]
        public void EndingDialogueRespectsConditions()
        {
            var reconciled = CampaignSimulator.RunIntent(Rules, "AAABBBBBBA").FinalState;
            Assert.IsTrue(reconciled.HasFlag("katoReconciled"));
            var shared = Library.EndingDialogue(Rules, reconciled, "SharedDawn");
            Assert.IsTrue(ContainsId(shared, "ST12_KATO_001"), "reconciled Kato speaks in Shared Dawn");
            Assert.IsFalse(ContainsId(shared, "ST12_MAMASARAH_001"), "Mama Sarah's flat line needs housingPrivacy");

            var frontier = Library.EndingDialogue(Rules, reconciled, "HumanFrontier");
            foreach (var l in frontier) Assert.AreNotEqual("I hope what we lost was worth protecting.", l.Text);
        }

        [Test]
        public void SubtitlesAlwaysCarrySpeakerLabels()
        {
            Assert.AreEqual("Mama Sarah (phone): My insulin is up there. Three customers are waiting at my stall right now.",
                Library.Line("ST01_MAMASARAH_001").Subtitle);
        }

        private static bool ContainsId(System.Collections.Generic.IList<DialogueLine> lines, string id)
        {
            foreach (var l in lines) if (l.Id == id) return true;
            return false;
        }
    }
}
