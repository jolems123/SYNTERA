using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Syntera.Core
{
    public sealed class DialogueLine
    {
        public string Id { get; internal set; }
        public string Speaker { get; internal set; }
        public string SpeakerName { get; internal set; }
        public string Delivery { get; internal set; }
        public string Text { get; internal set; }
        public ConditionSet When { get; internal set; }

        // Subtitle form with speaker label (accessibility: every line is labelled).
        public string Subtitle
        {
            get { return SpeakerName + (string.IsNullOrEmpty(Delivery) ? "" : " (" + Delivery + ")") + ": " + Text; }
        }
    }

    public sealed class StagingBeat
    {
        public string Start { get; internal set; }
        public string End { get; internal set; }
        public string Text { get; internal set; }
    }

    public enum ObjectiveKind { Interact, Drive, Rescue }

    public sealed class ObjectiveDef
    {
        public string Id { get; internal set; }
        public string Label { get; internal set; }
        public ObjectiveKind Kind { get; internal set; }
        // Optional set piece (syntera_activities.json) opened at the objective marker.
        public string Activity { get; internal set; }
    }

    public sealed class EndingScript
    {
        public string EndingId { get; internal set; }
        public IList<DialogueLine> Dialogue { get; internal set; }
        public string Outcome { get; internal set; }
        public string PostCreditSting { get; internal set; }
    }

    public sealed class ChapterScript
    {
        public string Id { get; internal set; }
        public int Chapter { get; internal set; }
        public int Year { get; internal set; }
        public string Title { get; internal set; }
        public string Location { get; internal set; }
        public string WindowStart { get; internal set; }
        public string WindowEnd { get; internal set; }
        public string Objective { get; internal set; }
        public IList<string> Quests { get; internal set; }
        public string YearTransitionBefore { get; internal set; }
        public string DecisionId { get; internal set; }
        public string Contact { get; internal set; }
        public string ContactName { get; internal set; }
        public IList<ObjectiveDef> Objectives { get; internal set; }
        public string Cliffhanger { get; internal set; }
        public IList<StagingBeat> Beats { get; internal set; }
        public IList<DialogueLine> Dialogue { get; internal set; }
        public IDictionary<string, IList<DialogueLine>> OptionDialogue { get; internal set; }
        public IDictionary<string, IList<DialogueLine>> CharterDialogue { get; internal set; }
        public IDictionary<string, EndingScript> Endings { get; internal set; }
        public string Consequence { get; internal set; }
        public string Implementation { get; internal set; }
    }

    public sealed class ConditionalBeat
    {
        public string ChapterId { get; internal set; }
        public string Beat { get; internal set; }
        public IList<DialogueLine> Variants { get; internal set; }
    }

    // Authored screenplay dialogue with stable line IDs. Dialogue never changes
    // Treasury, Credits or ownership: it only reads state to pick variants.
    public sealed class DialogueLibrary
    {
        private static readonly Regex LineIdPattern = new Regex("^ST(0[1-9]|1[0-2])_[A-Z0-9]+_[0-9]{3}$");

        public IDictionary<string, string> SpeakerNames { get; private set; }
        public IList<ChapterScript> Chapters { get; private set; }
        public IList<ConditionalBeat> ConditionalBeats { get; private set; }
        private readonly Dictionary<string, DialogueLine> byId = new Dictionary<string, DialogueLine>(StringComparer.Ordinal);

        public int LineCount { get { return byId.Count; } }

        public DialogueLine Line(string id)
        {
            DialogueLine l;
            return byId.TryGetValue(id, out l) ? l : null;
        }

        public ChapterScript Chapter(string id)
        {
            foreach (var c in Chapters) if (c.Id == id) return c;
            return null;
        }

        public ChapterScript ChapterForDecision(string decisionId)
        {
            foreach (var c in Chapters) if (c.DecisionId == decisionId) return c;
            return null;
        }

        // Exactly one applicable variant, or null when none applies (the beat is skipped).
        public DialogueLine SelectConditional(CampaignRules rules, CampaignState state, string chapterId, string beat)
        {
            foreach (var b in ConditionalBeats)
            {
                if (b.ChapterId != chapterId || b.Beat != beat) continue;
                foreach (var v in b.Variants) if (v.When.IsMet(rules, state)) return v;
                return null;
            }
            throw new ArgumentException("Unknown conditional beat " + chapterId + "/" + beat);
        }

        public IList<DialogueLine> EndingDialogue(CampaignRules rules, CampaignState state, string endingId)
        {
            var result = new List<DialogueLine>();
            foreach (var c in Chapters)
            {
                if (c.Endings == null) continue;
                EndingScript e;
                if (!c.Endings.TryGetValue(endingId, out e)) continue;
                foreach (var l in e.Dialogue) if (l.When.IsMet(rules, state)) result.Add(l);
                return result.AsReadOnly();
            }
            throw new ArgumentException("No ending script for " + endingId);
        }

        public static DialogueLibrary Load(string json, CampaignRules rules)
        {
            if (rules == null) throw new ArgumentNullException("rules");
            var root = Json.Obj(MiniJson.Parse(json), "dialogue");
            if (Json.Int(root, "schemaVersion", "dialogue") != 1) throw new FormatException("Unsupported dialogue schemaVersion");
            if (Json.Str(root, "scriptVersion", "dialogue") != rules.ScriptVersion)
                throw new FormatException("Dialogue script version does not match rules version " + rules.ScriptVersion);

            var lib = new DialogueLibrary();
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in Json.Obj(root["speakers"], "speakers"))
                names[kv.Key] = Json.Str(Json.Obj(kv.Value, kv.Key), "name", kv.Key);
            lib.SpeakerNames = names;
            var knownFlags = KnownFlags(rules);

            var chapters = new List<ChapterScript>();
            foreach (var raw in Json.Arr(root, "chapters", "dialogue"))
            {
                var o = Json.Obj(raw, "chapter");
                var c = new ChapterScript();
                c.Id = Json.Str(o, "id", "chapter");
                c.Chapter = Json.Int(o, "chapter", c.Id);
                c.Year = Json.Int(o, "year", c.Id);
                c.Title = Json.Str(o, "title", c.Id);
                c.Location = Json.Str(o, "location", c.Id);
                var window = Json.Arr(o, "window", c.Id);
                c.WindowStart = (string)window[0];
                c.WindowEnd = (string)window[1];
                c.Objective = Json.Str(o, "objective", c.Id);
                var quests = new List<string>();
                foreach (var q in Json.Arr(o, "quests", c.Id)) quests.Add((string)q);
                c.Quests = quests.AsReadOnly();
                c.YearTransitionBefore = Json.OptStr(o, "yearTransitionBefore");
                c.DecisionId = Json.OptStr(o, "decision");
                c.Contact = Json.Str(o, "contact", c.Id);
                string contactName;
                if (!names.TryGetValue(c.Contact, out contactName)) throw new FormatException(c.Id + ": unknown contact " + c.Contact);
                c.ContactName = contactName;
                var objectives = new List<ObjectiveDef>();
                foreach (var ob in Json.Arr(o, "objectives", c.Id))
                {
                    var oo = Json.Obj(ob, c.Id + "/objective");
                    var def = new ObjectiveDef { Id = Json.Str(oo, "id", c.Id), Label = Json.Str(oo, "label", c.Id) };
                    string kind = Json.Str(oo, "kind", def.Id);
                    if (kind == "interact") def.Kind = ObjectiveKind.Interact;
                    else if (kind == "drive") def.Kind = ObjectiveKind.Drive;
                    else if (kind == "rescue") def.Kind = ObjectiveKind.Rescue;
                    else throw new FormatException(def.Id + ": unknown objective kind " + kind);
                    if (!def.Id.StartsWith(c.Id + "_OBJ_", StringComparison.Ordinal)) throw new FormatException(def.Id + " must start with " + c.Id + "_OBJ_");
                    foreach (var existing in objectives) if (existing.Id == def.Id) throw new FormatException("Duplicate objective " + def.Id);
                    def.Activity = Json.OptStr(oo, "activity");
                    objectives.Add(def);
                }
                c.Objectives = objectives.AsReadOnly();
                c.Cliffhanger = Json.OptStr(o, "cliffhanger");
                c.Consequence = Json.Str(o, "consequence", c.Id);
                c.Implementation = Json.Str(o, "implementation", c.Id);

                var beats = new List<StagingBeat>();
                foreach (var b in Json.Arr(o, "beats", c.Id))
                {
                    var bo = Json.Obj(b, c.Id + "/beat");
                    beats.Add(new StagingBeat { Start = Json.Str(bo, "start", c.Id), End = Json.Str(bo, "end", c.Id), Text = Json.Str(bo, "text", c.Id) });
                }
                c.Beats = beats.AsReadOnly();
                c.Dialogue = lib.Lines(Json.Arr(o, "dialogue", c.Id), c.Id, knownFlags);

                if (c.DecisionId != null)
                {
                    var decision = rules.FindDecision(c.DecisionId);
                    if (decision == null) throw new FormatException(c.Id + ": unknown decision " + c.DecisionId);
                    c.OptionDialogue = lib.Keyed(o, "optionDialogue", c.Id, knownFlags);
                    foreach (var opt in decision.Options)
                        if (!c.OptionDialogue.ContainsKey(opt.Id)) throw new FormatException(c.Id + ": no dialogue for option " + opt.Id);
                    foreach (var key in c.OptionDialogue.Keys)
                        if (decision.Option(key) == null) throw new FormatException(c.Id + ": dialogue for unknown option " + key);
                }
                if (Json.Has(o, "charterDialogue"))
                {
                    c.CharterDialogue = lib.Keyed(o, "charterDialogue", c.Id, knownFlags);
                    foreach (var ch in rules.Charters)
                        if (!c.CharterDialogue.ContainsKey(ch.Id)) throw new FormatException(c.Id + ": no dialogue for charter " + ch.Id);
                    foreach (var key in c.CharterDialogue.Keys)
                        if (rules.FindCharter(key) == null) throw new FormatException(c.Id + ": dialogue for unknown charter " + key);
                }
                if (Json.Has(o, "endings"))
                {
                    var endings = new Dictionary<string, EndingScript>(StringComparer.Ordinal);
                    foreach (var kv in Json.Obj(o["endings"], c.Id + "/endings"))
                    {
                        if (rules.FindEnding(kv.Key) == null) throw new FormatException(c.Id + ": script for unknown ending " + kv.Key);
                        var eo = Json.Obj(kv.Value, c.Id + "/" + kv.Key);
                        endings[kv.Key] = new EndingScript
                        {
                            EndingId = kv.Key,
                            Dialogue = lib.Lines(Json.Arr(eo, "dialogue", kv.Key), c.Id, knownFlags),
                            Outcome = Json.Str(eo, "outcome", kv.Key),
                            PostCreditSting = Json.Str(eo, "postCreditSting", kv.Key)
                        };
                    }
                    foreach (var e in rules.Endings)
                        if (!endings.ContainsKey(e.Id)) throw new FormatException(c.Id + ": no script for ending " + e.Id);
                    c.Endings = endings;
                }
                chapters.Add(c);
            }
            lib.Chapters = chapters.AsReadOnly();
            foreach (var d in rules.Decisions)
                if (lib.ChapterForDecision(d.Id) == null) throw new FormatException("No chapter script for decision " + d.Id);

            var conditional = new List<ConditionalBeat>();
            foreach (var raw in Json.Arr(root, "conditionalBeats", "dialogue"))
            {
                var o = Json.Obj(raw, "conditionalBeat");
                var b = new ConditionalBeat { ChapterId = Json.Str(o, "chapter", "conditionalBeat"), Beat = Json.Str(o, "beat", "conditionalBeat") };
                if (lib.Chapter(b.ChapterId) == null) throw new FormatException("Conditional beat in unknown chapter " + b.ChapterId);
                b.Variants = lib.Lines(Json.Arr(o, "variants", b.Beat), b.ChapterId, knownFlags);
                foreach (var v in b.Variants)
                    if (v.When.Groups.Count == 1 && v.When.Groups[0].Count == 0)
                        throw new FormatException(b.Beat + ": conditional variant " + v.Id + " has no condition");
                conditional.Add(b);
            }
            lib.ConditionalBeats = conditional.AsReadOnly();
            return lib;
        }

        private IDictionary<string, IList<DialogueLine>> Keyed(Dictionary<string, object> o, string key, string chapterId, ICollection<string> flags)
        {
            var map = new Dictionary<string, IList<DialogueLine>>(StringComparer.Ordinal);
            foreach (var kv in Json.Obj(o[key], chapterId + "/" + key))
            {
                var list = kv.Value as List<object>;
                if (list == null) throw new FormatException(chapterId + "/" + key + "/" + kv.Key + ": expected array");
                map[kv.Key] = Lines(list, chapterId, flags);
            }
            return map;
        }

        private IList<DialogueLine> Lines(List<object> raw, string chapterId, ICollection<string> flags)
        {
            var list = new List<DialogueLine>();
            foreach (var r in raw)
            {
                var o = Json.Obj(r, chapterId + "/line");
                var l = new DialogueLine();
                l.Id = Json.Str(o, "id", chapterId);
                if (!LineIdPattern.IsMatch(l.Id)) throw new FormatException("Bad line ID " + l.Id);
                if (!l.Id.StartsWith(chapterId + "_", StringComparison.Ordinal)) throw new FormatException(l.Id + " is filed under " + chapterId);
                l.Speaker = Json.Str(o, "speaker", l.Id);
                string name;
                if (!SpeakerNames.TryGetValue(l.Speaker, out name)) throw new FormatException(l.Id + ": unknown speaker " + l.Speaker);
                if (l.Id.Split('_')[1] != l.Speaker) throw new FormatException(l.Id + ": ID speaker does not match " + l.Speaker);
                l.SpeakerName = name;
                l.Text = Json.Str(o, "text", l.Id);
                if (l.Text.Trim().Length == 0) throw new FormatException(l.Id + ": empty text");
                l.Delivery = Json.OptStr(o, "delivery");
                l.When = Json.Has(o, "whenAll") ? ConditionSet.ParseAll(Json.Arr(o, "whenAll", l.Id), l.Id) : ConditionSet.Always();
                foreach (var g in l.When.Groups)
                    foreach (var c in g)
                    {
                        if (c.Flag != null && !flags.Contains(c.Flag)) throw new FormatException(l.Id + ": unknown flag " + c.Flag);
                        if (c.Flag == null) throw new FormatException(l.Id + ": dialogue conditions may only test flags");
                    }
                if (byId.ContainsKey(l.Id)) throw new FormatException("Duplicate line ID " + l.Id);
                byId[l.Id] = l;
                list.Add(l);
            }
            return list.AsReadOnly();
        }

        private static HashSet<string> KnownFlags(CampaignRules rules)
        {
            var flags = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in rules.Decisions)
                foreach (var o in d.Options)
                {
                    foreach (var f in o.SetFlags) flags.Add(f);
                    foreach (var cf in o.ConditionalFlags) flags.Add(cf.Flag);
                }
            foreach (var f in rules.CreditLineFlags) flags.Add(f);
            return flags;
        }
    }
}
