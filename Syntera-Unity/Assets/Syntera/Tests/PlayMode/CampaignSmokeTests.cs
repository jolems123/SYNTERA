using System.Collections;
using System.IO;
using NUnit.Framework;
using Syntera.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Syntera.Tests
{
    // Plays the whole 2026-2028 campaign through the real game scene (world, set pieces, UI,
    // e-moto, saves) one player action per frame. Any error logged fails the test.
    public class CampaignSmokeTests
    {
        private string saveDir;

        [SetUp]
        public void UseTemporarySaves()
        {
            saveDir = Path.Combine(Path.GetTempPath(), "syntera-playmode-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveDir);
            SynteraGame.SaveDirectoryOverride = saveDir;
        }

        [TearDown]
        public void Cleanup()
        {
            SynteraGame.SaveDirectoryOverride = null;
            try { Directory.Delete(saveDir, true); } catch (IOException) { }
        }

        private static IEnumerator PlayThrough(string intent, string charter, string expectedEnding)
        {
            SceneManager.LoadScene("City");
            yield return null;
            yield return null;
            var game = SynteraGame.Instance;
            Assert.IsNotNull(game, "SynteraGame did not start");
            float worst = 0;
            long startMemory = System.GC.GetTotalMemory(false);
            int steps = 0;
            while (game.AutoPlayStep(intent, charter))
            {
                Assert.Less(++steps, 3000, "campaign did not finish; stuck in " + game.Director.Phase + " at " + game.Director.Chapter.Id);
                yield return null;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
            }
            Assert.AreEqual(expectedEnding, game.Director.Engine.State.EndingId);
            Assert.IsTrue(game.Director.Engine.Log.Verify(), "audit chain");
            Assert.IsTrue(File.Exists(Path.Combine(SynteraGame.SaveDirectoryOverride, "syntera-campaign.json")), "game saved");
            long grew = (System.GC.GetTotalMemory(false) - startMemory) / (1024 * 1024);
            Debug.Log("[SYNTERA PLAYMODE] " + intent + "/" + charter + " -> " + expectedEnding + " in " + steps + " actions; worst frame " +
                      (worst * 1000f).ToString("0") + " ms; managed memory change " + grew + " MB");
        }

        [UnityTest]
        public IEnumerator AllA_SharedCharter_ReachesSharedDawn() { return PlayThrough("AAAAAAAAAA", "shared", "SharedDawn"); }

        [UnityTest]
        public IEnumerator AllB_Compact_ReachesCorporateSunrise() { return PlayThrough("BBBBBBBBBB", "compact", "CorporateSunrise"); }

        [UnityTest]
        public IEnumerator AllA_FailedCovenant_FallsBackToHumanFrontier() { return PlayThrough("AAAAAAAAAA", "covenant", "HumanFrontier"); }

        [UnityTest]
        public IEnumerator ReloadMidCampaignResumesSameChapter()
        {
            SceneManager.LoadScene("City");
            yield return null; yield return null;
            var game = SynteraGame.Instance;
            for (int i = 0; i < 400 && game.Director.ChapterIndex < 5; i++) { game.AutoPlayStep("ABABABABAB", "shared"); yield return null; }
            string chapter = game.Director.Chapter.Id;
            string head = game.Director.Engine.Log.HeadHash;
            game.Save();
            SceneManager.LoadScene("City");
            yield return null; yield return null;
            Assert.AreEqual(chapter, SynteraGame.Instance.Director.Chapter.Id);
            Assert.AreEqual(head, SynteraGame.Instance.Director.Engine.Log.HeadHash);
        }
    }
}
