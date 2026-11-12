using System;
using System.IO;
using Syntera.Core;
using UnityEngine;

namespace Syntera
{
    // "Continue to 2050" - explicitly a PROTOTYPE (screenplay v1.1 section 12, CH12).
    // It works on its own copy of the final 2028 indicators and its own save file, so it can
    // never change the canonical campaign record. Outcomes are scenarios, not predictions.
    public sealed class SynteraFuturePrototype : MonoBehaviour
    {
        [Serializable]
        private sealed class FutureState
        {
            public int year = 2028;
            public int trust, jobs, autonomy, grid, vantageInfluence;
            public bool creditLineRepaid;
            public bool usedCreditLine;
        }

        private FutureState state;
        public bool IsOpen { get; private set; }
        private static string PathOnDisk => Path.Combine(Application.persistentDataPath, "syntera-2050-prototype.json");

        public void Open(CampaignState final)
        {
            state = TryLoad() ?? new FutureState
            {
                trust = final.Get("trust"), jobs = final.Get("jobs"), autonomy = final.Get("autonomy"),
                grid = final.Get("grid"), vantageInfluence = final.Get("vantageInfluence"),
                usedCreditLine = final.HasFlag("creditLineUsed")
            };
            IsOpen = true;
            Persist();
        }

        public void Close()
        {
            IsOpen = false;
            state = null;
            try { if (File.Exists(PathOnDisk)) File.Delete(PathOnDisk); } catch (IOException) { }
        }

        private void Step(bool automate)
        {
            if (state.year >= 2050) return;
            state.year++;
            state.vantageInfluence = Mathf.Clamp(state.vantageInfluence + (automate ? 3 : -1), 0, 100);
            state.jobs = Mathf.Clamp(state.jobs + (automate ? -1 : 1), 0, 100);
            state.trust = Mathf.Clamp(state.trust + (automate ? -1 : 1), 0, 100);
            state.autonomy = Mathf.Clamp(state.autonomy + (automate ? -2 : 1), 0, 100);
            state.grid = Mathf.Clamp(state.grid + (automate ? 1 : 0), 0, 100);
            if (state.usedCreditLine && !state.creditLineRepaid && state.year == 2030)
            {
                state.creditLineRepaid = true;
                state.autonomy = Mathf.Clamp(state.autonomy - 2, 0, 100);
            }
            Persist();
        }

        public void Draw(GUIStyle heading, GUIStyle paragraph, GUIStyle action, float scale)
        {
            float w = Mathf.Min(Screen.width - 24, 720);
            float h = 300 * scale;
            var r = new Rect(12, Mathf.Max(130, Screen.height - h - 12), w, h);
            GUI.Box(r, "");
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 20));
            GUILayout.Label("Toward 2050 · " + state.year + " · PROTOTYPE", heading);
            GUILayout.Label("Trust " + state.trust + " · Jobs " + state.jobs + " · Autonomy " + state.autonomy +
                            " · Grid " + state.grid + " · VANTAGE influence " + state.vantageInfluence, paragraph);
            if (state.usedCreditLine)
                GUILayout.Label(state.creditLineRepaid ? "Naomi's credit line was repaid in 2030." : "Naomi's credit line falls due in 2030.", paragraph);
            if (state.year >= 2050)
                GUILayout.Label("2050 reached. SCENARIO, NOT A CERTAINTY: this simplified prototype does not predict the future.", paragraph);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUI.enabled = state.year < 2050;
            if (GUILayout.Button("Invest in citizens", action, GUILayout.Height(44 * scale))) Step(false);
            if (GUILayout.Button("Expand AI automation", action, GUILayout.Height(44 * scale))) Step(true);
            GUI.enabled = true;
            if (GUILayout.Button("Back to recap", action, GUILayout.Height(44 * scale))) IsOpen = false;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void Persist()
        {
            try { File.WriteAllText(PathOnDisk, JsonUtility.ToJson(state)); }
            catch (Exception ex) { Debug.LogWarning("[SYNTERA] 2050 prototype save failed: " + ex.Message); }
        }

        private static FutureState TryLoad()
        {
            try
            {
                if (!File.Exists(PathOnDisk)) return null;
                var s = JsonUtility.FromJson<FutureState>(File.ReadAllText(PathOnDisk));
                return s != null && s.year >= 2028 && s.year <= 2050 ? s : null;
            }
            catch (Exception) { return null; }
        }
    }
}
