using System;
using System.Collections.Generic;

namespace Syntera.Core
{
    public enum ActivityType { Documents, Rotate, Match, Select, Allocate, Quiz, Speech }

    public sealed class ActivityPage { public string Title; public string Text; public IList<KeyValuePair<ConditionSet, string>> Variants; }
    public sealed class ActivityNode { public string Label; public int Start; public int Target; }
    public sealed class ActivityItem { public string Label; public string Answer; }
    public sealed class ActivityEntry { public string Text; public bool Correct; }
    public sealed class ActivitySector { public string Label; public int Demand; public bool Required; }
    public sealed class ActivityQuestion { public string Question; public IList<string> Answers; public string Response; }
    public sealed class ActivitySpeech { public string Label; public ConditionSet When; public IList<string[]> Lines; }

    public sealed class ActivityDef
    {
        public string Id; public ActivityType Type; public string Title; public string Intro; public string Hint;
        public IList<ActivityPage> Pages = new List<ActivityPage>();
        public IList<ActivityNode> Nodes = new List<ActivityNode>();
        public IList<string> Options = new List<string>();
        public IList<ActivityItem> Items = new List<ActivityItem>();
        public IList<ActivityEntry> Entries = new List<ActivityEntry>();
        public int Required, MaxWrong, Capacity, Step = 10;
        public IList<ActivitySector> Sectors = new List<ActivitySector>();
        public IList<ActivityQuestion> Questions = new List<ActivityQuestion>();
        public IList<ActivitySpeech> Speeches = new List<ActivitySpeech>();
    }

    // Authored set-piece puzzles (syntera_activities.json). Puzzles never change campaign numbers;
    // finishing one only completes the chapter objective that opened it.
    public sealed class ActivityLibrary
    {
        private readonly Dictionary<string, ActivityDef> byId = new Dictionary<string, ActivityDef>(StringComparer.Ordinal);
        public ActivityDef Get(string id) { ActivityDef a; return id != null && byId.TryGetValue(id, out a) ? a : null; }
        public int Count { get { return byId.Count; } }

        public static ActivityLibrary Load(string json, DialogueLibrary dialogue)
        {
            var root = Json.Obj(MiniJson.Parse(json), "activities");
            if (Json.Int(root, "schemaVersion", "activities") != 1) throw new FormatException("Unsupported activities schemaVersion");
            var lib = new ActivityLibrary();
            foreach (var raw in Json.Arr(root, "activities", "activities"))
            {
                var o = Json.Obj(raw, "activity");
                var a = new ActivityDef { Id = Json.Str(o, "id", "activity") };
                string c = a.Id;
                a.Title = Json.Str(o, "title", c);
                a.Intro = Json.Str(o, "intro", c);
                a.Hint = Json.OptStr(o, "hint");
                switch (Json.Str(o, "type", c))
                {
                    case "documents":
                        a.Type = ActivityType.Documents;
                        foreach (var p in Json.Arr(o, "pages", c))
                        {
                            var po = Json.Obj(p, c);
                            var page = new ActivityPage { Title = Json.Str(po, "title", c), Text = Json.OptStr(po, "text") };
                            if (Json.Has(po, "variants"))
                            {
                                var v = new List<KeyValuePair<ConditionSet, string>>();
                                foreach (var x in Json.Arr(po, "variants", c))
                                {
                                    var vo = Json.Obj(x, c);
                                    v.Add(new KeyValuePair<ConditionSet, string>(ConditionSet.ParseAll(Json.Arr(vo, "whenAll", c), c), Json.Str(vo, "text", c)));
                                }
                                if (v.Count == 0 || v[v.Count - 1].Key.Groups[0].Count != 0) throw new FormatException(c + ": last page variant must be unconditional");
                                page.Variants = v;
                            }
                            else if (page.Text == null) throw new FormatException(c + ": page needs text or variants");
                            a.Pages.Add(page);
                        }
                        if (a.Pages.Count == 0) throw new FormatException(c + ": no pages");
                        break;
                    case "rotate":
                        a.Type = ActivityType.Rotate;
                        foreach (var n in Json.Arr(o, "nodes", c))
                        {
                            var no = Json.Obj(n, c);
                            var node = new ActivityNode { Label = Json.Str(no, "label", c), Start = Json.Int(no, "start", c), Target = Json.Int(no, "target", c) };
                            if (node.Start < 0 || node.Start > 3 || node.Target < 0 || node.Target > 3) throw new FormatException(c + ": rotation must be 0-3");
                            a.Nodes.Add(node);
                        }
                        bool anyWrong = false;
                        foreach (var n in a.Nodes) if (n.Start != n.Target) anyWrong = true;
                        if (!anyWrong) throw new FormatException(c + ": rotate puzzle starts solved");
                        break;
                    case "match":
                        a.Type = ActivityType.Match;
                        foreach (var x in Json.Arr(o, "options", c)) a.Options.Add((string)x);
                        foreach (var x in Json.Arr(o, "items", c))
                        {
                            var io = Json.Obj(x, c);
                            var item = new ActivityItem { Label = Json.Str(io, "label", c), Answer = Json.Str(io, "answer", c) };
                            if (!a.Options.Contains(item.Answer)) throw new FormatException(c + ": answer '" + item.Answer + "' is not an option");
                            a.Items.Add(item);
                        }
                        break;
                    case "select":
                        a.Type = ActivityType.Select;
                        a.Required = Json.Int(o, "required", c);
                        a.MaxWrong = Json.Int(o, "maxWrong", c);
                        int correct = 0;
                        foreach (var x in Json.Arr(o, "entries", c))
                        {
                            var eo = Json.Obj(x, c);
                            object b; eo.TryGetValue("correct", out b);
                            var e = new ActivityEntry { Text = Json.Str(eo, "text", c), Correct = b is bool && (bool)b };
                            if (e.Correct) correct++;
                            a.Entries.Add(e);
                        }
                        if (a.Required < 1 || a.Required > correct) throw new FormatException(c + ": required exceeds correct entries");
                        break;
                    case "allocate":
                        a.Type = ActivityType.Allocate;
                        a.Capacity = Json.Int(o, "capacity", c);
                        a.Step = Json.Int(o, "step", c);
                        int needed = 0;
                        foreach (var x in Json.Arr(o, "sectors", c))
                        {
                            var so = Json.Obj(x, c);
                            object r; so.TryGetValue("required", out r);
                            var s = new ActivitySector { Label = Json.Str(so, "label", c), Demand = Json.Int(so, "demand", c), Required = r is bool && (bool)r };
                            if (s.Required) needed += s.Demand;
                            a.Sectors.Add(s);
                        }
                        if (needed > a.Capacity) throw new FormatException(c + ": required sectors exceed capacity");
                        break;
                    case "quiz":
                        a.Type = ActivityType.Quiz;
                        foreach (var x in Json.Arr(o, "questions", c))
                        {
                            var qo = Json.Obj(x, c);
                            var q = new ActivityQuestion { Question = Json.Str(qo, "q", c), Response = Json.Str(qo, "response", c), Answers = new List<string>() };
                            foreach (var ans in Json.Arr(qo, "answers", c)) q.Answers.Add((string)ans);
                            a.Questions.Add(q);
                        }
                        break;
                    case "speech":
                        a.Type = ActivityType.Speech;
                        foreach (var x in Json.Arr(o, "options", c))
                        {
                            var so = Json.Obj(x, c);
                            var sp = new ActivitySpeech { Label = Json.Str(so, "label", c), When = ConditionSet.ParseAll(Json.Arr(so, "whenAll", c), c), Lines = new List<string[]>() };
                            foreach (var l in Json.Arr(so, "lines", c))
                            {
                                var pair = l as List<object>;
                                if (pair == null || pair.Count != 2) throw new FormatException(c + ": speech line must be [speaker, text]");
                                string speaker = (string)pair[0];
                                if (!dialogue.SpeakerNames.ContainsKey(speaker)) throw new FormatException(c + ": unknown speaker " + speaker);
                                sp.Lines.Add(new[] { speaker, (string)pair[1] });
                            }
                            a.Speeches.Add(sp);
                        }
                        bool fallback = false;
                        foreach (var s in a.Speeches) if (s.When.Groups[0].Count == 0) fallback = true;
                        if (!fallback) throw new FormatException(c + ": a speech needs one option available to everyone");
                        break;
                    default: throw new FormatException(c + ": unknown activity type");
                }
                if (lib.byId.ContainsKey(a.Id)) throw new FormatException("Duplicate activity " + a.Id);
                lib.byId[a.Id] = a;
            }
            foreach (var ch in dialogue.Chapters)
                foreach (var ob in ch.Objectives)
                    if (ob.Activity != null && lib.Get(ob.Activity) == null) throw new FormatException(ob.Id + ": unknown activity " + ob.Activity);
            return lib;
        }
    }

    // One attempt at one activity. Pure logic: the Unity panel renders it and forwards input.
    public sealed class ActivitySession
    {
        public const int AssistAfterFailures = 3;
        public ActivityDef Def { get; private set; }
        public bool Complete { get; private set; }
        public int Failures { get; private set; }
        public string Feedback { get; private set; }
        public bool AssistAvailable { get { return !Complete && Failures >= AssistAfterFailures; } }

        private readonly CampaignRules rules;
        private readonly CampaignState state;
        private readonly bool[] read;
        private readonly int[] rotation;
        private int moves;
        private readonly string[] assigned;
        private readonly bool[] selected;
        private readonly int[] allocation;
        private int question;
        private int speech = -1;

        public ActivitySession(ActivityDef def, CampaignRules rules, CampaignState state)
        {
            Def = def;
            this.rules = rules;
            this.state = state;
            read = new bool[def.Pages.Count];
            rotation = new int[def.Nodes.Count];
            for (int i = 0; i < rotation.Length; i++) rotation[i] = def.Nodes[i].Start;
            assigned = new string[def.Items.Count];
            selected = new bool[def.Entries.Count];
            allocation = new int[def.Sectors.Count];
            Feedback = "";
        }

        // ---- documents
        public string PageText(int i)
        {
            var p = Def.Pages[i];
            if (p.Variants == null) return p.Text;
            foreach (var v in p.Variants) if (v.Key.IsMet(rules, state)) return v.Value;
            return p.Variants[p.Variants.Count - 1].Value;
        }
        public bool IsRead(int i) { return read[i]; }
        public void Read(int i)
        {
            Require(ActivityType.Documents);
            read[i] = true;
            Complete = Array.TrueForAll(read, r => r);
        }

        // ---- rotate (0=North 1=East 2=South 3=West; labelled in text for colour independence)
        public static readonly string[] Directions = { "North", "East", "South", "West" };
        public int Rotation(int i) { return rotation[i]; }
        public bool NodeAligned(int i) { return rotation[i] == Def.Nodes[i].Target; }
        public void Rotate(int i)
        {
            Require(ActivityType.Rotate);
            if (Complete) return;
            rotation[i] = (rotation[i] + 1) % 4;
            moves++;
            Complete = true;
            for (int n = 0; n < rotation.Length; n++) if (!NodeAligned(n)) Complete = false;
            if (!Complete && moves % 8 == 0) Failures++;
            Feedback = Complete ? "All aligned." : CountAligned() + " of " + rotation.Length + " aligned.";
        }
        private int CountAligned() { int c = 0; for (int n = 0; n < rotation.Length; n++) if (NodeAligned(n)) c++; return c; }

        // ---- match
        public string Assigned(int item) { return assigned[item]; }
        public void Assign(int item, string option)
        {
            Require(ActivityType.Match);
            if (!Def.Options.Contains(option)) throw new ArgumentException("Unknown option " + option);
            assigned[item] = option;
        }
        public bool SubmitMatch()
        {
            Require(ActivityType.Match);
            int right = 0;
            for (int i = 0; i < assigned.Length; i++) if (assigned[i] == Def.Items[i].Answer) right++;
            Complete = right == assigned.Length;
            if (!Complete) Failures++;
            Feedback = Complete ? "Correct." : right + " of " + assigned.Length + " correct. Try again." + (Failures == 1 && Def.Hint != null ? " " + Def.Hint : "");
            return Complete;
        }

        // ---- select
        public bool Selected(int i) { return selected[i]; }
        public void Toggle(int i) { Require(ActivityType.Select); selected[i] = !selected[i]; }
        public bool SubmitSelect()
        {
            Require(ActivityType.Select);
            int right = 0, wrong = 0;
            for (int i = 0; i < selected.Length; i++)
                if (selected[i]) { if (Def.Entries[i].Correct) right++; else wrong++; }
            Complete = right >= Def.Required && wrong <= Def.MaxWrong;
            if (!Complete) Failures++;
            Feedback = Complete ? "Review accepted: " + right + " found." :
                (right < Def.Required ? "Found " + right + " of the " + Def.Required + " needed." : "") +
                (wrong > Def.MaxWrong ? " Too many harmless items flagged (" + wrong + ")." : "");
            return Complete;
        }

        // ---- allocate
        public int Allocation(int i) { return allocation[i]; }
        public int Allocated { get { int t = 0; foreach (var a in allocation) t += a; return t; } }
        public void Adjust(int i, int delta)
        {
            Require(ActivityType.Allocate);
            int v = allocation[i] + delta * Def.Step;
            allocation[i] = Math.Max(0, Math.Min(Def.Sectors[i].Demand, v));
        }
        public bool SubmitAllocation()
        {
            Require(ActivityType.Allocate);
            var problems = new List<string>();
            if (Allocated > Def.Capacity) problems.Add("Allocated " + Allocated + " but only " + Def.Capacity + " is available.");
            for (int i = 0; i < allocation.Length; i++)
                if (Def.Sectors[i].Required && allocation[i] < Def.Sectors[i].Demand) problems.Add(Def.Sectors[i].Label + " must be fully powered.");
            Complete = problems.Count == 0;
            if (!Complete) Failures++;
            Feedback = Complete ? "Plan committed. " + (Def.Capacity - Allocated) + " units held in reserve." : string.Join(" ", problems.ToArray());
            return Complete;
        }

        // ---- quiz (flavour: every answer is accepted)
        public int QuestionIndex { get { return question; } }
        public void Answer(int answer)
        {
            Require(ActivityType.Quiz);
            if (Complete) return;
            Feedback = Def.Questions[question].Response;
            question++;
            Complete = question >= Def.Questions.Count;
        }

        // ---- speech
        public bool SpeechAvailable(int i) { return Def.Speeches[i].When.IsMet(rules, state); }
        public int ChosenSpeech { get { return speech; } }
        public bool ChooseSpeech(int i)
        {
            Require(ActivityType.Speech);
            if (!SpeechAvailable(i)) return false;
            speech = i;
            Complete = true;
            return true;
        }

        // ---- assist: after three failed attempts, solve one step (screenplay section 9)
        public void Assist()
        {
            if (!AssistAvailable) return;
            switch (Def.Type)
            {
                case ActivityType.Rotate:
                    for (int i = 0; i < rotation.Length; i++) if (!NodeAligned(i)) { rotation[i] = Def.Nodes[i].Target; break; }
                    Complete = CountAligned() == rotation.Length;
                    Feedback = "Assist aligned one relay.";
                    break;
                case ActivityType.Match:
                    for (int i = 0; i < assigned.Length; i++) if (assigned[i] != Def.Items[i].Answer) { assigned[i] = Def.Items[i].Answer; break; }
                    Feedback = "Assist placed one item correctly.";
                    break;
                case ActivityType.Select:
                    for (int i = 0; i < selected.Length; i++)
                        if (selected[i] != Def.Entries[i].Correct) { selected[i] = Def.Entries[i].Correct; break; }
                    Feedback = "Assist corrected one highlight.";
                    break;
                case ActivityType.Allocate:
                    for (int i = 0; i < allocation.Length; i++) allocation[i] = Def.Sectors[i].Required ? Def.Sectors[i].Demand : 0;
                    Feedback = "Assist powered the hospital and cleared the rest. Share out what remains.";
                    break;
            }
        }

        // Test and autoplay support: solve the activity the intended way.
        public void SolveForTest()
        {
            switch (Def.Type)
            {
                case ActivityType.Documents: for (int i = 0; i < read.Length; i++) Read(i); break;
                case ActivityType.Rotate: for (int i = 0; i < rotation.Length; i++) while (!NodeAligned(i)) Rotate(i); break;
                case ActivityType.Match: for (int i = 0; i < assigned.Length; i++) Assign(i, Def.Items[i].Answer); SubmitMatch(); break;
                case ActivityType.Select: for (int i = 0; i < selected.Length; i++) selected[i] = Def.Entries[i].Correct; SubmitSelect(); break;
                case ActivityType.Allocate:
                    int left = Def.Capacity;
                    for (int i = 0; i < allocation.Length; i++) if (Def.Sectors[i].Required) { allocation[i] = Def.Sectors[i].Demand; left -= allocation[i]; }
                    for (int i = 0; i < allocation.Length; i++) if (!Def.Sectors[i].Required) { allocation[i] = Math.Min(Def.Sectors[i].Demand, left - left % Def.Step); left -= allocation[i]; }
                    SubmitAllocation();
                    break;
                case ActivityType.Quiz: while (!Complete) Answer(0); break;
                case ActivityType.Speech: for (int i = Def.Speeches.Count - 1; i >= 0; i--) if (SpeechAvailable(i)) { ChooseSpeech(i); break; } break;
            }
        }

        private void Require(ActivityType t)
        {
            if (Def.Type != t) throw new InvalidOperationException(Def.Id + " is a " + Def.Type + " activity, not " + t);
        }
    }
}
