using System;
using System.Collections.Generic;
using Syntera.Core;
using UnityEngine;

namespace Syntera
{
    // Player settings, persisted per machine (accessibility: screenplay section 10).
    public sealed class SynteraSettings
    {
        public float TextScale = 1f;
        public bool HighContrast, ReduceMotion, AutoDrive, ShowPerformance;
        public KeyCode Interact = KeyCode.E, Drive = KeyCode.F, Map = KeyCode.M, History = KeyCode.H, Options = KeyCode.O;

        public static SynteraSettings Load()
        {
            var s = new SynteraSettings();
            s.TextScale = Mathf.Clamp(PlayerPrefs.GetFloat("syntera.textScale", 1f), .8f, 1.8f);
            s.HighContrast = PlayerPrefs.GetInt("syntera.highContrast", 0) == 1;
            s.ReduceMotion = PlayerPrefs.GetInt("syntera.reduceMotion", 0) == 1;
            s.AutoDrive = PlayerPrefs.GetInt("syntera.autoDrive", 0) == 1;
            s.ShowPerformance = PlayerPrefs.GetInt("syntera.showPerformance", 0) == 1;
            s.Interact = Key("interact", KeyCode.E); s.Drive = Key("drive", KeyCode.F); s.Map = Key("map", KeyCode.M);
            s.History = Key("history", KeyCode.H); s.Options = Key("options", KeyCode.O);
            return s;
        }

        private static KeyCode Key(string name, KeyCode fallback)
        {
            int v = PlayerPrefs.GetInt("syntera.key." + name, (int)fallback);
            return Enum.IsDefined(typeof(KeyCode), v) ? (KeyCode)v : fallback;
        }

        public void Save()
        {
            PlayerPrefs.SetFloat("syntera.textScale", TextScale);
            PlayerPrefs.SetInt("syntera.highContrast", HighContrast ? 1 : 0);
            PlayerPrefs.SetInt("syntera.reduceMotion", ReduceMotion ? 1 : 0);
            PlayerPrefs.SetInt("syntera.autoDrive", AutoDrive ? 1 : 0);
            PlayerPrefs.SetInt("syntera.showPerformance", ShowPerformance ? 1 : 0);
            PlayerPrefs.SetInt("syntera.key.interact", (int)Interact); PlayerPrefs.SetInt("syntera.key.drive", (int)Drive);
            PlayerPrefs.SetInt("syntera.key.map", (int)Map); PlayerPrefs.SetInt("syntera.key.history", (int)History);
            PlayerPrefs.SetInt("syntera.key.options", (int)Options);
            PlayerPrefs.Save();
        }
    }

    // IMGUI helper: styles, panels and keyboard/controller menu navigation. Actions picked with
    // the mouse, arrows/stick + Enter/A, or number keys run in Update, never mid-GUI-pass.
    public sealed class SynteraUi
    {
        public SynteraSettings Settings;
        public GUIStyle Heading, Paragraph, Small, Button, Speaker;
        private float stylesScale = -1f;
        private bool stylesContrast;
        private readonly List<Action> actions = new List<Action>();
        private readonly List<bool> enabledFlags = new List<bool>();
        private int counter;
        private int selected;
        private Action pending;
        private bool axisHeld;
        private static Texture2D solid;

        public bool HasMenu => actions.Count > 0;

        public void Styles()
        {
            if (Mathf.Approximately(stylesScale, Settings.TextScale) && stylesContrast == Settings.HighContrast && Heading != null) return;
            stylesScale = Settings.TextScale;
            stylesContrast = Settings.HighContrast;
            float s = Settings.TextScale;
            Heading = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(23 * s), fontStyle = FontStyle.Bold, wordWrap = true };
            Paragraph = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * s), wordWrap = true };
            Small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * s), wordWrap = true };
            Speaker = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * s), fontStyle = FontStyle.Bold, wordWrap = true };
            Button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(15 * s), wordWrap = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 6, 6) };
            if (Settings.HighContrast)
            {
                foreach (var st in new[] { Heading, Paragraph, Small, Speaker }) st.normal.textColor = Color.white;
                Speaker.normal.textColor = new Color(1f, .9f, .2f);
                Button.normal.textColor = Color.white;
                Button.fontStyle = FontStyle.Bold;
            }
        }

        public void BeginGui()
        {
            counter = 0;
            if (Event.current.type == EventType.Layout) { actions.Clear(); enabledFlags.Clear(); }
        }

        public Rect Panel(float height, float width = 720)
        {
            height *= Settings.TextScale;
            float w = Mathf.Min(Screen.width - 24, width * Mathf.Max(1f, Settings.TextScale * .9f));
            float y = Mathf.Max(130, Screen.height - height - 12);
            var r = new Rect(12, y, w, Mathf.Min(height, Screen.height - 142));
            Backdrop(r);
            return r;
        }

        public void Backdrop(Rect r)
        {
            if (Settings.HighContrast)
            {
                if (solid == null) { solid = new Texture2D(1, 1); solid.SetPixel(0, 0, Color.black); solid.Apply(); }
                GUI.DrawTexture(r, solid);
            }
            GUI.Box(r, "");
        }

        // A selectable menu option. Returns nothing: the action runs on the next Update.
        public void Option(string label, bool enabled, Action onActivate, float minHeight = 38)
        {
            int index = counter++;
            if (Event.current.type == EventType.Layout) { actions.Add(onActivate); enabledFlags.Add(enabled); }
            bool focus = index == selected;
            string shortcut = index < 9 ? "[" + (index + 1) + "] " : "";
            GUI.enabled = enabled;
            if (GUILayout.Button((focus ? "▶ " : "") + shortcut + label, Button, GUILayout.MinHeight(minHeight * Settings.TextScale)) && enabled)
                pending = onActivate;
            GUI.enabled = true;
        }

        // Called from Update. Arrow keys / W,S / left stick move; Enter / controller A confirms; 1-9 pick directly.
        public void HandleInput(bool allowSubmitKeys)
        {
            if (actions.Count > 0)
            {
                float v = Input.GetAxisRaw("Vertical");
                bool up = Input.GetKeyDown(KeyCode.UpArrow) || (!axisHeld && v > .6f);
                bool down = Input.GetKeyDown(KeyCode.DownArrow) || (!axisHeld && v < -.6f);
                axisHeld = Mathf.Abs(v) > .3f;
                if (up) selected = (selected - 1 + actions.Count) % actions.Count;
                if (down) selected = (selected + 1) % actions.Count;
                selected = Mathf.Clamp(selected, 0, actions.Count - 1);
                if (allowSubmitKeys && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.JoystickButton0))
                    && enabledFlags[selected]) pending = actions[selected];
                for (int n = 0; n < 9 && n < actions.Count; n++)
                    if (Input.GetKeyDown(KeyCode.Alpha1 + n) && enabledFlags[n]) { selected = n; pending = actions[n]; }
            }
            if (pending != null)
            {
                var a = pending;
                pending = null;
                selected = 0;
                a();
            }
        }

        public void ResetSelection() { selected = 0; }

        // ------------------------------------------------------------- activities
        public void DrawActivity(ActivitySession s, Action onDone, Action onClose, Action<IList<float>> onAllocation)
        {
            var d = s.Def;
            var r = Panel(470);
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 20));
            GUILayout.Label(d.Title, Heading);
            GUILayout.Label(d.Intro, Small);
            switch (d.Type)
            {
                case ActivityType.Documents:
                    for (int i = 0; i < d.Pages.Count; i++)
                    {
                        int k = i;
                        Option((s.IsRead(i) ? "[read] " : "") + d.Pages[i].Title, true, () => { s.Read(k); pageShown = k; }, 32);
                    }
                    if (pageShown >= 0 && pageShown < d.Pages.Count) GUILayout.Label(s.PageText(pageShown), Paragraph);
                    break;
                case ActivityType.Rotate:
                    for (int i = 0; i < d.Nodes.Count; i++)
                    {
                        int k = i;
                        string now = ActivitySession.Directions[s.Rotation(i)];
                        string need = ActivitySession.Directions[d.Nodes[i].Target];
                        Option(d.Nodes[i].Label + ": facing " + now + " " + Arrow(s.Rotation(i)) + (s.NodeAligned(i) ? "  ✓ ALIGNED" : "  (needs " + need + ")"), !s.Complete, () => s.Rotate(k), 32);
                    }
                    break;
                case ActivityType.Match:
                    if (d.Hint != null && s.Failures == 0) GUILayout.Label(d.Hint, Small);
                    for (int i = 0; i < d.Items.Count; i++)
                    {
                        GUILayout.Label(d.Items[i].Label + "  →  " + (s.Assigned(i) ?? "?"), Paragraph);
                        GUILayout.BeginHorizontal();
                        foreach (var opt in d.Options)
                        {
                            int k = i; string o = opt;
                            Option((s.Assigned(i) == opt ? "● " : "") + opt, !s.Complete, () => s.Assign(k, o), 30);
                        }
                        GUILayout.EndHorizontal();
                    }
                    if (!s.Complete) Option("Submit", AllAssigned(s), () => s.SubmitMatch());
                    break;
                case ActivityType.Select:
                    for (int i = 0; i < d.Entries.Count; i++)
                    {
                        int k = i;
                        Option((s.Selected(i) ? "[FLAGGED] " : "[ ] ") + d.Entries[i].Text, !s.Complete, () => s.Toggle(k), 28);
                    }
                    if (!s.Complete) Option("Submit review", true, () => s.SubmitSelect());
                    break;
                case ActivityType.Allocate:
                    GUILayout.Label("Allocated " + s.Allocated + " of " + d.Capacity, Paragraph);
                    for (int i = 0; i < d.Sectors.Count; i++)
                    {
                        int k = i;
                        var sec = d.Sectors[i];
                        GUILayout.Label(sec.Label + (sec.Required ? " (must be full)" : "") + ": " + s.Allocation(i) + " / " + sec.Demand, Paragraph);
                        GUILayout.BeginHorizontal();
                        Option("− " + d.Step, !s.Complete, () => { s.Adjust(k, -1); onAllocation(Fractions(s)); }, 28);
                        Option("+ " + d.Step, !s.Complete, () => { s.Adjust(k, 1); onAllocation(Fractions(s)); }, 28);
                        GUILayout.EndHorizontal();
                    }
                    if (!s.Complete) Option("Commit plan", true, () => s.SubmitAllocation());
                    break;
                case ActivityType.Quiz:
                    if (!s.Complete)
                    {
                        var q = d.Questions[s.QuestionIndex];
                        GUILayout.Label("DRONE: " + q.Question, Paragraph);
                        for (int i = 0; i < q.Answers.Count; i++) { int k = i; Option(q.Answers[i], true, () => s.Answer(k), 30); }
                    }
                    break;
                case ActivityType.Speech:
                    if (s.ChosenSpeech < 0)
                        for (int i = 0; i < d.Speeches.Count; i++)
                        {
                            int k = i;
                            bool ok = s.SpeechAvailable(i);
                            Option(d.Speeches[i].Label + (ok ? "" : " (your record does not support this)"), ok, () => s.ChooseSpeech(k), 32);
                        }
                    else
                        foreach (var line in d.Speeches[s.ChosenSpeech].Lines) GUILayout.Label(SpeakerName(line[0]) + ": " + line[1], Paragraph);
                    break;
            }
            if (!string.IsNullOrEmpty(s.Feedback)) GUILayout.Label(s.Feedback, Speaker);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (s.Complete) Option("Done", true, () => { pageShown = -1; onDone(); });
            if (s.AssistAvailable) Option("Assist (after 3 tries)", true, () => s.Assist());
            if (!s.Complete) Option("Step away [Esc]", true, () => { pageShown = -1; onClose(); });
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        public Func<string, string> SpeakerName = k => k;
        private int pageShown = -1;

        private static bool AllAssigned(ActivitySession s)
        {
            for (int i = 0; i < s.Def.Items.Count; i++) if (s.Assigned(i) == null) return false;
            return true;
        }

        private static IList<float> Fractions(ActivitySession s)
        {
            var f = new List<float>();
            for (int i = 0; i < s.Def.Sectors.Count; i++) f.Add(s.Def.Sectors[i].Demand == 0 ? 0 : s.Allocation(i) / (float)s.Def.Sectors[i].Demand);
            return f;
        }

        private static string Arrow(int r) { return r == 0 ? "↑" : r == 1 ? "→" : r == 2 ? "↓" : "←"; }
    }
}
