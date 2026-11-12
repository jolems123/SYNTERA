using System;
using System.IO;
using UnityEngine;

namespace Syntera
{
    // Built-player self test. Launch SYNTERA.exe with -syntera-smoke: the game plays the whole
    // campaign by itself (temporary saves), logs "[SYNTERA SMOKE] PASS|FAIL" and exits 0 or 1.
    // Catches player-only problems the editor hides, such as shaders stripped from the build.
    public sealed class SynteraSmokeRunner : MonoBehaviour
    {
        private static string firstError;
        private float started;
        private int steps;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-syntera-smoke") < 0) return;
            string dir = Path.Combine(Path.GetTempPath(), "syntera-smoke-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            SynteraGame.SaveDirectoryOverride = dir;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && firstError == null)
                    firstError = message + "\n" + stack;
            };
            var go = new GameObject("SYNTERA smoke runner");
            DontDestroyOnLoad(go);
            go.AddComponent<SynteraSmokeRunner>();
        }

        private void Start() { started = Time.realtimeSinceStartup; }

        private void Update()
        {
            if (firstError != null) { Finish(false, firstError); return; }
            if (Time.realtimeSinceStartup - started > 240) { Finish(false, "timed out after " + steps + " actions"); return; }
            var game = SynteraGame.Instance;
            if (game == null || game.Director == null) { if (Time.realtimeSinceStartup - started > 20) Finish(false, "game did not start"); return; }
            try
            {
                steps++;
                if (!game.AutoPlayStep("AAAAAAAAAA", "shared"))
                    Finish(game.Director.Engine.State.EndingId == "SharedDawn", "ending " + game.Director.Engine.State.EndingId + " after " + steps + " actions");
            }
            catch (Exception ex) { Finish(false, ex.ToString()); }
        }

        private void Finish(bool pass, string detail)
        {
            Debug.Log("[SYNTERA SMOKE] " + (pass ? "PASS" : "FAIL") + ": " + detail);
            enabled = false;
            Application.Quit(pass ? 0 : 1);
        }
    }
}
