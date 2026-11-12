using System;
using System.Collections.Generic;

namespace Syntera.Core
{
    public enum DirectorPhase
    {
        ChapterIntro,   // title card and staging summary
        FieldWork,      // playable objectives, then talk to the chapter contact
        Conversation,   // primary dialogue (with section 8 conditional beats)
        Decision,       // two options with costs, shortfalls and credit line
        Reaction,       // option dialogue
        YearSummary,    // dividends, taxes and delayed effects, shown transparently
        Evidence,       // CH11 evidence cards, including empty slots
        CharterChoice,  // CH11 proposal, no time limit
        CharterSpeech,  // CH11 charter dialogue
        VoteResult,     // seat-by-seat council result
        Ending,         // CH12 ending script
        Recap,          // honest recap
        Complete
    }

    public sealed class EvidenceSlot
    {
        public string Id { get; internal set; }
        public string Title { get; internal set; }
        public string EarnedBy { get; internal set; }
        public bool Earned { get; internal set; }
    }

    public sealed class RecapEntry
    {
        public string Chapter { get; internal set; }
        public string Choice { get; internal set; }
    }

    public sealed class CampaignRecap
    {
        public EndingDef Ending { get; internal set; }
        public EndingScript Script { get; internal set; }
        public CouncilResult Vote { get; internal set; }
        public IList<RecapEntry> Choices { get; internal set; }
        public IDictionary<string, int> Indicators { get; internal set; }
        public IDictionary<string, int> Ownership { get; internal set; }
        public int EvidenceCount { get; internal set; }
        public bool UsedCreditLine { get; internal set; }
    }

    // Drives the 12-chapter flow on top of CampaignEngine. Holds no economy of its own:
    // every state change goes through an engine transaction. Unity renders Phase and
    // forwards input; this class has no UnityEngine dependency so it is fully testable.
    public sealed class CampaignDirector
    {
        public CampaignEngine Engine { get; private set; }
        public DialogueLibrary Library { get; private set; }
        public CampaignRules Rules { get { return Engine.Rules; } }
        public int ChapterIndex { get; private set; }
        public DirectorPhase Phase { get; private set; }
        public int LineIndex { get; private set; }
        public CouncilResult Vote { get; private set; }
        public string YearSummary { get; private set; }

        private IList<DialogueLine> lines = new List<DialogueLine>();
        private readonly HashSet<string> objectivesDone = new HashSet<string>(StringComparer.Ordinal);

        // Raised whenever a save checkpoint should be written.
        public event Action Checkpoint;

        public CampaignDirector(CampaignEngine engine, DialogueLibrary library)
        {
            if (engine == null) throw new ArgumentNullException("engine");
            if (library == null) throw new ArgumentNullException("library");
            Engine = engine;
            Library = library;
            Phase = DirectorPhase.ChapterIntro;
        }

        public ChapterScript Chapter { get { return Library.Chapters[Math.Min(ChapterIndex, Library.Chapters.Count - 1)]; } }
        public DecisionDef Decision { get { return Chapter.DecisionId == null ? null : Rules.FindDecision(Chapter.DecisionId); } }
        public bool IsCouncilChapter { get { return Chapter.CharterDialogue != null; } }
        public bool IsEndingChapter { get { return Chapter.Endings != null; } }

        // ---------------------------------------------------------------- intro and field work

        public void BeginChapter()
        {
            if (Phase != DirectorPhase.ChapterIntro) return;
            Phase = DirectorPhase.FieldWork;
            Save();
        }

        public IList<ObjectiveDef> PendingObjectives
        {
            get
            {
                var pending = new List<ObjectiveDef>();
                foreach (var o in Chapter.Objectives) if (!objectivesDone.Contains(o.Id)) pending.Add(o);
                return pending.AsReadOnly();
            }
        }

        public IEnumerable<string> CompletedObjectives { get { return objectivesDone; } }

        public bool ObjectivesComplete { get { return PendingObjectives.Count == 0; } }

        // Objectives complete in authored order so the world only ever shows one marker.
        public ObjectiveDef NextObjective
        {
            get
            {
                var p = PendingObjectives;
                return p.Count == 0 ? null : p[0];
            }
        }

        public bool CompleteObjective(string objectiveId)
        {
            var next = NextObjective;
            if (Phase != DirectorPhase.FieldWork || next == null || next.Id != objectiveId) return false;
            objectivesDone.Add(objectiveId);
            Save();
            return true;
        }

        // Consequence of how an objective was played (e.g. the CH01 rescue timed out). Field work only.
        public TransactionResult ApplyOutcome(string outcomeId)
        {
            if (Phase != DirectorPhase.FieldWork) return TransactionResult.Reject(outcomeId, "Outcomes apply during field work only.");
            var r = Engine.ApplyOutcome(outcomeId);
            if (r.Committed) Save();
            return r;
        }

        public bool CanTalkToContact { get { return Phase == DirectorPhase.FieldWork && ObjectivesComplete; } }

        public bool StartConversation()
        {
            if (!CanTalkToContact) return false;
            Phase = DirectorPhase.Conversation;
            lines = BuildConversation();
            LineIndex = 0;
            if (lines.Count == 0) FinishLines();
            Save();
            return true;
        }

        // ---------------------------------------------------------------- dialogue lines

        public bool ShowingLines
        {
            get
            {
                return Phase == DirectorPhase.Conversation || Phase == DirectorPhase.Reaction ||
                       Phase == DirectorPhase.CharterSpeech || Phase == DirectorPhase.Ending;
            }
        }

        public DialogueLine CurrentLine { get { return ShowingLines && LineIndex < lines.Count ? lines[LineIndex] : null; } }
        public int LineCount { get { return ShowingLines ? lines.Count : 0; } }

        public void Advance()
        {
            if (!ShowingLines) return;
            LineIndex++;
            if (LineIndex >= lines.Count) FinishLines();
            Save();
        }

        private void FinishLines()
        {
            switch (Phase)
            {
                case DirectorPhase.Conversation:
                    if (Decision != null) Phase = DirectorPhase.Decision;
                    else if (IsCouncilChapter) Phase = DirectorPhase.Evidence;
                    else if (IsEndingChapter) EnterEnding();
                    else NextChapter();
                    break;
                case DirectorPhase.Reaction:
                    AfterDecision();
                    break;
                case DirectorPhase.CharterSpeech:
                    Phase = DirectorPhase.VoteResult;
                    break;
                case DirectorPhase.Ending:
                    Phase = DirectorPhase.Recap;
                    break;
            }
            if (!ShowingLines) LineIndex = 0;
        }

        // ---------------------------------------------------------------- decisions

        public OptionStatus Status(string optionId)
        {
            return Engine.Evaluate(Chapter.DecisionId, optionId);
        }

        public TransactionResult TakeCreditLine(string optionId)
        {
            if (Phase != DirectorPhase.Decision) return TransactionResult.Reject(Chapter.DecisionId, "No decision is open.");
            var r = Engine.TakeCreditLine(Chapter.DecisionId, optionId);
            if (r.Committed) Save();
            return r;
        }

        public TransactionResult Choose(string optionId)
        {
            if (Phase != DirectorPhase.Decision) return TransactionResult.Reject(Chapter.DecisionId, "No decision is open.");
            var r = Engine.Choose(Chapter.DecisionId, optionId);
            if (!r.Committed) return r;
            Phase = DirectorPhase.Reaction;
            lines = Chapter.OptionDialogue[optionId];
            LineIndex = 0;
            if (lines.Count == 0) FinishLines();
            Save();
            return r;
        }

        private void AfterDecision()
        {
            if (Engine.CurrentStepKind == CampaignStepKind.YearTransition)
            {
                var r = Engine.AdvanceYear(Engine.CurrentStepId);
                if (!r.Committed) throw new InvalidOperationException("Year transition rejected: " + r.Message);
                YearSummary = r.Message;
                Phase = DirectorPhase.YearSummary;
                return;
            }
            NextChapter();
        }

        public void ContinueFromYearSummary()
        {
            if (Phase != DirectorPhase.YearSummary) return;
            NextChapter();
            Save();
        }

        // ---------------------------------------------------------------- council

        public IList<EvidenceSlot> EvidenceSlots
        {
            get
            {
                var slots = new List<EvidenceSlot>();
                foreach (var c in Rules.EvidenceCards)
                    slots.Add(new EvidenceSlot { Id = c.Id, Title = c.Title, EarnedBy = c.EarnedBy, Earned = c.When.IsMet(Rules, Engine.State) });
                return slots.AsReadOnly();
            }
        }

        public void ContinueFromEvidence()
        {
            if (Phase != DirectorPhase.Evidence) return;
            Phase = DirectorPhase.CharterChoice;
            Save();
        }

        public TransactionResult ProposeCharter(string charterId)
        {
            if (Phase != DirectorPhase.CharterChoice) return TransactionResult.Reject(CampaignRules.CouncilStepId, "The council is not taking proposals.");
            var r = Engine.ProposeCharter(charterId);
            if (!r.Committed) return r;
            Vote = r.Council;
            Phase = DirectorPhase.CharterSpeech;
            lines = Chapter.CharterDialogue[charterId];
            LineIndex = 0;
            if (lines.Count == 0) FinishLines();
            Save();
            return r;
        }

        public void ContinueFromVote()
        {
            if (Phase != DirectorPhase.VoteResult) return;
            NextChapter();
            Save();
        }

        // ---------------------------------------------------------------- ending and recap

        private void EnterEnding()
        {
            Phase = DirectorPhase.Ending;
            lines = BuildEnding();
            LineIndex = 0;
            if (lines.Count == 0) Phase = DirectorPhase.Recap;
        }

        public CampaignRecap Recap()
        {
            var state = Engine.State;
            if (state.EndingId == null) return null;
            var choices = new List<RecapEntry>();
            foreach (var c in state.Choices)
            {
                var d = Rules.FindDecision(c.Key);
                choices.Add(new RecapEntry { Chapter = "CH" + d.Chapter.ToString("00") + " " + d.Title, Choice = d.Option(c.Value).Label });
            }
            var indicators = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var s in state.StatNames) indicators[s] = state.Get(s);
            EndingScript script = null;
            foreach (var ch in Library.Chapters)
                if (ch.Endings != null) ch.Endings.TryGetValue(state.EndingId, out script);
            return new CampaignRecap
            {
                Ending = Rules.FindEnding(state.EndingId),
                Script = script,
                Vote = Vote ?? CouncilEvaluator.Evaluate(Rules, state, state.CharterId),
                Choices = choices.AsReadOnly(),
                Indicators = indicators,
                Ownership = state.Ownership,
                EvidenceCount = Rules.CountEvidence(state),
                UsedCreditLine = Engine.CreditLineUsed
            };
        }

        public void FinishRecap()
        {
            if (Phase != DirectorPhase.Recap) return;
            Phase = DirectorPhase.Complete;
            Save();
        }

        // ---------------------------------------------------------------- internals

        private void NextChapter()
        {
            ChapterIndex++;
            objectivesDone.Clear();
            Phase = ChapterIndex >= Library.Chapters.Count ? DirectorPhase.Complete : DirectorPhase.ChapterIntro;
            ChapterIndex = Math.Min(ChapterIndex, Library.Chapters.Count - 1);
            LineIndex = 0;
            lines = new List<DialogueLine>();
        }

        private IList<DialogueLine> BuildConversation()
        {
            var result = new List<DialogueLine>();
            if (!IsEndingChapter) AddConditionalBeats(result);
            result.AddRange(Chapter.Dialogue);
            return result.AsReadOnly();
        }

        private IList<DialogueLine> BuildEnding()
        {
            var result = new List<DialogueLine>(Library.EndingDialogue(Rules, Engine.State, Engine.State.EndingId));
            AddConditionalBeats(result);
            return result.AsReadOnly();
        }

        private void AddConditionalBeats(List<DialogueLine> into)
        {
            foreach (var beat in Library.ConditionalBeats)
            {
                if (beat.ChapterId != Chapter.Id) continue;
                var line = Library.SelectConditional(Rules, Engine.State, beat.ChapterId, beat.Beat);
                if (line != null) into.Add(line);
            }
        }

        private void Save()
        {
            var handler = Checkpoint;
            if (handler != null) handler();
        }

        // ---------------------------------------------------------------- restore (used by CampaignSave)

        internal void Restore(int chapterIndex, DirectorPhase phase, int lineIndex, IEnumerable<string> objectives)
        {
            if (chapterIndex < 0 || chapterIndex >= Library.Chapters.Count) throw new FormatException("Save chapter out of range");
            ChapterIndex = chapterIndex;
            Phase = phase;
            objectivesDone.Clear();
            foreach (var o in objectives)
            {
                bool known = false;
                foreach (var def in Chapter.Objectives) if (def.Id == o) known = true;
                if (!known) throw new FormatException("Save names unknown objective " + o);
                objectivesDone.Add(o);
            }
            ValidateAgainstEngine();

            lines = new List<DialogueLine>();
            switch (phase)
            {
                case DirectorPhase.Conversation: lines = BuildConversation(); break;
                case DirectorPhase.Reaction: lines = Chapter.OptionDialogue[Engine.State.ChoiceFor(Chapter.DecisionId)]; break;
                case DirectorPhase.CharterSpeech: lines = Chapter.CharterDialogue[Engine.State.CharterId]; break;
                case DirectorPhase.Ending: lines = BuildEnding(); break;
            }
            if (ShowingLines && (lineIndex < 0 || lineIndex >= lines.Count)) throw new FormatException("Save line index out of range");
            LineIndex = ShowingLines ? lineIndex : 0;

            if (phase == DirectorPhase.YearSummary)
            {
                var events = Engine.Log.Events;
                YearSummary = events.Count > 0 && events[events.Count - 1].Kind == "year" ? events[events.Count - 1].Detail : null;
                if (YearSummary == null) throw new FormatException("Year summary phase without a year transition");
            }
            if (Engine.State.CharterId != null) Vote = CouncilEvaluator.Evaluate(Rules, Engine.State, Engine.State.CharterId);
        }

        private void ValidateAgainstEngine()
        {
            var kind = Engine.CurrentStepKind;
            string decision = Chapter.DecisionId;
            bool decided = decision != null && Engine.State.ChoiceFor(decision) != null;
            bool ok;
            switch (Phase)
            {
                case DirectorPhase.ChapterIntro:
                case DirectorPhase.FieldWork:
                case DirectorPhase.Conversation:
                    ok = decision != null ? Engine.CurrentStepId == decision
                        : IsCouncilChapter ? kind == CampaignStepKind.Council
                        : kind == CampaignStepKind.Complete;
                    break;
                case DirectorPhase.Decision: ok = decision != null && Engine.CurrentStepId == decision; break;
                case DirectorPhase.Reaction: ok = decided; break;
                case DirectorPhase.YearSummary: ok = decided; break;
                case DirectorPhase.Evidence:
                case DirectorPhase.CharterChoice: ok = IsCouncilChapter && kind == CampaignStepKind.Council; break;
                case DirectorPhase.CharterSpeech:
                case DirectorPhase.VoteResult: ok = IsCouncilChapter && Engine.State.CharterId != null; break;
                case DirectorPhase.Ending:
                case DirectorPhase.Recap:
                case DirectorPhase.Complete: ok = IsEndingChapter && Engine.State.EndingId != null; break;
                default: ok = false; break;
            }
            if (!ok) throw new FormatException("Save phase " + Phase + " in " + Chapter.Id + " does not match the campaign record");
        }
    }
}
