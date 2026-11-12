using System;
using System.Collections.Generic;

namespace Syntera.Core
{
    public sealed class LoadedCampaign
    {
        public CampaignDirector Director { get; internal set; }
        public string PlayerName { get; internal set; }
        public double ElapsedSeconds { get; internal set; }
    }

    // Save format 2. The save stores the player's committed transactions, not the numbers.
    // Loading replays them through the real engine, so a save can never hold a state the
    // rules would not allow; the audit head hash must match or the save is rejected.
    public static class CampaignSave
    {
        public const int FormatVersion = 2;

        public static string Write(CampaignDirector director, string playerName, double elapsedSeconds)
        {
            var tx = new List<object>();
            foreach (var t in director.Engine.Transactions)
                tx.Add(new List<object> { t.Kind, t.StepId, t.OptionId });
            var objectives = new List<object>();
            foreach (var o in director.CompletedObjectives) objectives.Add(o);
            var doc = new Dictionary<string, object>
            {
                { "format", FormatVersion },
                { "scriptVersion", director.Rules.ScriptVersion },
                { "playerName", playerName ?? "Player" },
                { "elapsedSeconds", Math.Max(0, elapsedSeconds) },
                { "transactions", tx },
                { "auditHead", director.Engine.Log.HeadHash },
                { "director", new Dictionary<string, object>
                    {
                        { "chapter", director.ChapterIndex },
                        { "phase", director.Phase.ToString() },
                        { "line", director.LineIndex },
                        { "objectives", objectives }
                    }
                }
            };
            return MiniJson.Serialize(doc);
        }

        public static int ReadFormat(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            object v;
            if (root == null || !root.TryGetValue("format", out v) || !(v is double)) return 0;
            return (int)(double)v;
        }

        public static LoadedCampaign Read(string json, CampaignRules rules, DialogueLibrary library)
        {
            var root = Json.Obj(MiniJson.Parse(json), "save");
            if (Json.Int(root, "format", "save") != FormatVersion) throw new FormatException("Unsupported save format");
            if (Json.Str(root, "scriptVersion", "save") != rules.ScriptVersion) throw new FormatException("Save was made with different campaign rules");

            var engine = new CampaignEngine(rules);
            foreach (var raw in Json.Arr(root, "transactions", "save"))
            {
                var t = raw as List<object>;
                if (t == null || t.Count != 3) throw new FormatException("Bad transaction record");
                string kind = t[0] as string, step = t[1] as string, option = t[2] as string;
                TransactionResult r;
                switch (kind)
                {
                    case "decision": r = engine.Choose(step, option); break;
                    case "creditLine": r = engine.TakeCreditLine(step, option); break;
                    case "year": r = engine.AdvanceYear(step); break;
                    case "council": r = engine.ProposeCharter(option); break;
                    default: throw new FormatException("Unknown transaction kind " + kind);
                }
                if (!r.Committed) throw new FormatException("Saved transaction no longer valid at " + step + ": " + r.Message);
            }
            if (engine.Log.HeadHash != Json.Str(root, "auditHead", "save"))
                throw new FormatException("Save audit chain does not match its transactions");

            var d = Json.Obj(root["director"], "save.director");
            DirectorPhase phase;
            try { phase = (DirectorPhase)Enum.Parse(typeof(DirectorPhase), Json.Str(d, "phase", "save.director")); }
            catch (ArgumentException) { throw new FormatException("Unknown director phase"); }
            var objectives = new List<string>();
            foreach (var o in Json.Arr(d, "objectives", "save.director"))
            {
                var s = o as string;
                if (s == null) throw new FormatException("Objective IDs must be strings");
                objectives.Add(s);
            }

            var director = new CampaignDirector(engine, library);
            director.Restore(Json.Int(d, "chapter", "save.director"), phase, Json.Int(d, "line", "save.director"), objectives);

            object elapsed;
            double seconds = root.TryGetValue("elapsedSeconds", out elapsed) && elapsed is double ? (double)elapsed : 0;
            if (double.IsNaN(seconds) || seconds < 0) throw new FormatException("Bad elapsed time");
            string name = Json.OptStr(root, "playerName");
            if (string.IsNullOrEmpty(name) || name.Length > 40) name = "Player";

            return new LoadedCampaign { Director = director, PlayerName = name, ElapsedSeconds = seconds };
        }
    }
}
