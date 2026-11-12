#if UNITY_EDITOR
using System;
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
            if (SynteraCampaignContent.Dialogue.Length != 12)
                throw new InvalidOperationException("Campaign must have twelve authored chapter conversations.");
            int lines = 0;
            for (int chapter = 0; chapter < 12; chapter++)
            {
                string[] sequence = SynteraCampaignContent.Dialogue[chapter];
                if (sequence == null || sequence.Length < 3)
                    throw new InvalidOperationException("Chapter " + chapter + " is missing required dialogue.");
                lines += sequence.Length;
                for (int choice = 0; choice < 2; choice++)
                {
                    var outcome = SynteraCampaignContent.GetOutcome(chapter, choice == 1);
                    if (string.IsNullOrWhiteSpace(outcome.consequence))
                        throw new InvalidOperationException("Chapter " + chapter + " has an empty consequence.");
                }
            }
            if (!SynteraCampaignContent.NeedsDelivery(4) || !SynteraCampaignContent.NeedsDelivery(7))
                throw new InvalidOperationException("Two delivery chapters must remain defined.");
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
            for (int chapter = 0; chapter < 12; chapter++)
                for (int line = 0; line < SynteraCampaignContent.Dialogue[chapter].Length; line++)
                    if (Resources.Load<AudioClip>("Voice/Chapter_" + chapter.ToString("00") + "_Line_" + line.ToString("00")) == null)
                        missingVoice++;
            Debug.Log("[SYNTERA UNITY VALIDATION] PASS: 12 chapters, " + lines +
                      " dialogue lines, 24 authored consequences; missing voice clips: " + missingVoice +
                      " (subtitles remain usable). This does not certify a 90-minute playthrough.");
        }
    }
}
#endif
