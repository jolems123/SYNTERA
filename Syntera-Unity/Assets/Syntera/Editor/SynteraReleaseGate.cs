#if UNITY_EDITOR
using System;
using Syntera.Core;
using UnityEditor;
using UnityEngine;

namespace Syntera.Editor
{
    // Must run inside a real Unity Editor, not a text-only source audit.
    public static class SynteraReleaseGate
    {
        [MenuItem("SYNTERA/Validate Campaign and Assets Locally")]
        public static void Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Unity is still compiling or importing assets. Retry after completion.");

            var rulesAsset = Resources.Load<TextAsset>("syntera_campaign_rules");
            var dialogueAsset = Resources.Load<TextAsset>("syntera_campaign_dialogue");
            if (rulesAsset == null || dialogueAsset == null)
                throw new InvalidOperationException("Campaign data missing from Assets/Syntera/Resources.");
            var rules = CampaignRules.Load(rulesAsset.text);
            var library = DialogueLibrary.Load(dialogueAsset.text, rules);
            if (library.Chapters.Count != 12) throw new InvalidOperationException("Campaign must have twelve chapters.");

            // Section 14 parity, executed in-editor through the same engine the game uses.
            var sim = CampaignSimulator.RunAll(rules);
            Expect(sim.Paths == 1024, "1,024 simulated paths");
            Expect(sim.CharterPasses["shared"] == 242, "Shared Charter passes on 242 paths");
            Expect(sim.CharterPasses["compact"] == 466, "Optimization Compact passes on 466 paths");
            Expect(sim.CharterPasses["covenant"] == 348, "Human Covenant passes on 348 paths");
            Expect(sim.NoCharterPasses == 280, "280 paths with no passable charter");
            Expect(sim.AllIntendedAffordable == 922, "922 fully affordable paths");
            Expect(sim.UsedCreditLine == 32, "32 credit-line paths");
            Expect(sim.NegativeResourcePaths == 0, "no negative Credits or Treasury");
            Expect(sim.AuditFailures == 0, "every audit chain verifies");

            var required = new[] {
                "Assets/Syntera/Scripts/SynteraGame.cs",
                "Assets/Syntera/Scripts/SynteraMissionObjectives.cs",
                "Assets/Syntera/Scripts/SynteraDrivePod.cs",
                "Assets/Syntera/Scripts/SynteraAudioDirector.cs",
                "Assets/Syntera/Scripts/SynteraCharacterAssets.cs"
            };
            foreach (string path in required)
                if (!System.IO.File.Exists(path))
                    throw new InvalidOperationException("Required gameplay source missing: " + path);

            int missingVoice = 0;
            foreach (var chapter in library.Chapters)
                foreach (var line in AllLines(chapter))
                    if (Resources.Load<AudioClip>("Voice/" + line.Id) == null) missingVoice++;
            foreach (var beat in library.ConditionalBeats)
                foreach (var line in beat.Variants)
                    if (Resources.Load<AudioClip>("Voice/" + line.Id) == null) missingVoice++;

            Debug.Log("[SYNTERA UNITY VALIDATION] PASS: 12 chapters, " + library.LineCount +
                      " dialogue lines, section 14 parity exact (242/466/348, 280 fallback, 922 affordable, 32 credit line); " +
                      "missing voice clips: " + missingVoice + " of " + library.LineCount +
                      " (subtitles remain usable). This does not certify a 90-minute playthrough.");
        }

        private static System.Collections.Generic.IEnumerable<DialogueLine> AllLines(ChapterScript chapter)
        {
            foreach (var l in chapter.Dialogue) yield return l;
            if (chapter.OptionDialogue != null) foreach (var kv in chapter.OptionDialogue) foreach (var l in kv.Value) yield return l;
            if (chapter.CharterDialogue != null) foreach (var kv in chapter.CharterDialogue) foreach (var l in kv.Value) yield return l;
            if (chapter.Endings != null) foreach (var kv in chapter.Endings) foreach (var l in kv.Value.Dialogue) yield return l;
        }

        private static void Expect(bool condition, string what)
        {
            if (!condition) throw new InvalidOperationException("Release gate failed: " + what);
        }
    }
}
#endif
