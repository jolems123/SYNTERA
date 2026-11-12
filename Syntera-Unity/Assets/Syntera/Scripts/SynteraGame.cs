using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Syntera.Core;
using UnityEngine;

namespace Syntera
{
    // Unity presentation layer for the screenplay v1.1 campaign. All campaign state lives in
    // Syntera.Core (CampaignDirector -> CampaignEngine); this class renders the current phase,
    // runs the 3D world, and forwards player input. It never changes numbers directly.
    public sealed class SynteraGame : MonoBehaviour
    {
        public static SynteraGame Instance { get; private set; }
        public static bool InputLocked { get; private set; }

        public CampaignDirector Director { get; private set; }
        public string PlayerName { get; private set; } = "Player";
        public float ElapsedSeconds { get; private set; }

        private CampaignRules rules;
        private DialogueLibrary library;
        private string fatalError;
        private string notice = "";
        private bool recoveredFromBackup;
        private bool legacySaveFound;

        private GameObject player;
        private Camera followCamera;
        private CharacterController controller;
        private Vector3 velocity;
        private float cameraYaw = 35f;
        private float cameraPitch = 23f;
        private SynteraDrivePod drivePod;
        private SynteraCinematicDirector cinematic;
        private SynteraAudioDirector audioDirector;
        private SynteraMissionObjectives objectives;
        private SynteraFuturePrototype future;

        private bool showMap;
        private bool showHelp = true;
        private bool confirmRestart;
        private bool introCardPlayed;
        private string decisionMessage = "";
        private int lastChapter = -1;
        private DirectorPhase lastPhase = (DirectorPhase)(-1);
        private string lastVoiceLine;
        private SynteraCharacter speakingActor;
        private float textScale = 1f;
        private Vector2 scroll;
        private GUIStyle heading, paragraph, small, action, speaker;
        private float stylesScale = -1f;

        private const float InteractionRadius = 5f;
        private static string SavePath => Path.Combine(Application.persistentDataPath, "syntera-campaign.json");
        private static string LegacySavePath => Path.Combine(Application.persistentDataPath, "syntera-city-save.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<SynteraGame>() == null)
                new GameObject("SYNTERA - World Controller").AddComponent<SynteraGame>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Application.targetFrameRate = 60;
            try
            {
                rules = CampaignRules.Load(LoadResource("syntera_campaign_rules"));
                library = DialogueLibrary.Load(LoadResource("syntera_campaign_dialogue"), rules);
            }
            catch (Exception ex)
            {
                fatalError = "Campaign data failed validation: " + ex.Message;
                Debug.LogError("[SYNTERA] " + fatalError);
                return;
            }
            LoadOrStart();
            CreateWorld();
        }

        private static string LoadResource(string name)
        {
            var asset = Resources.Load<TextAsset>(name);
            if (asset == null) throw new FileNotFoundException("Missing Resources/" + name + ".json");
            return asset.text;
        }

        // ---------------------------------------------------------------- save / load

        private void LoadOrStart()
        {
            legacySaveFound = File.Exists(LegacySavePath);
            foreach (string path in new[] { SavePath, SavePath + ".bak" })
            {
                if (!File.Exists(path)) continue;
                try
                {
                    var loaded = CampaignSave.Read(File.ReadAllText(path), rules, library);
                    UseDirector(loaded.Director);
                    PlayerName = loaded.PlayerName;
                    ElapsedSeconds = (float)loaded.ElapsedSeconds;
                    recoveredFromBackup = path.EndsWith(".bak");
                    if (recoveredFromBackup)
                    {
                        notice = "Your latest save was damaged, so the previous checkpoint was restored.";
                        Debug.LogWarning("[SYNTERA] Recovered campaign from backup save.");
                    }
                    showHelp = false;
                    return;
                }
                catch (Exception ex) { Debug.LogWarning("[SYNTERA] Rejected save " + path + ": " + ex.Message); }
            }
            UseDirector(new CampaignDirector(new CampaignEngine(rules), library));
            if (legacySaveFound)
                notice = "A save from the earlier prototype was found. It uses old rules and was left untouched; this is a new campaign.";
        }

        private void UseDirector(CampaignDirector director)
        {
            if (Director != null) Director.Checkpoint -= Save;
            Director = director;
            Director.Checkpoint += Save;
        }

        public void Save()
        {
            if (Director == null) return;
            try
            {
                string temp = SavePath + ".tmp";
                string backup = SavePath + ".bak";
                File.WriteAllText(temp, CampaignSave.Write(Director, PlayerName, ElapsedSeconds));
                if (recoveredFromBackup)
                {
                    // Keep the known-good backup; never overwrite it with the damaged primary.
                    if (File.Exists(SavePath)) File.Delete(SavePath);
                    File.Move(temp, SavePath);
                    recoveredFromBackup = false;
                }
                else if (File.Exists(SavePath))
                {
                    try { File.Replace(temp, SavePath, backup); }
                    catch (PlatformNotSupportedException)
                    {
                        File.Copy(SavePath, backup, true);
                        File.Delete(SavePath);
                        File.Move(temp, SavePath);
                    }
                }
                else File.Move(temp, SavePath);
            }
            catch (Exception ex) { Debug.LogWarning("[SYNTERA] Save failed: " + ex.Message); }
        }

        private void Restart()
        {
            // Only reached after explicit confirmation. The finished run stays in .bak.
            Save();
            if (File.Exists(SavePath)) File.Copy(SavePath, SavePath + ".bak", true);
            if (File.Exists(SavePath)) File.Delete(SavePath);
            UseDirector(new CampaignDirector(new CampaignEngine(rules), library));
            ElapsedSeconds = 0;
            if (future != null) future.Close();
            if (drivePod != null) { drivePod.ResetPod(); drivePod.ConfigureDelivery(null, Vector3.zero); }
            if (player != null)
            {
                controller.enabled = false;
                player.transform.position = SynteraWorld.PlacePosition(0) + new Vector3(2, 0, -6);
                controller.enabled = true;
            }
            lastChapter = -1;
            lastPhase = (DirectorPhase)(-1);
            introCardPlayed = false;
            confirmRestart = false;
            notice = "";
            Save();
        }

        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnApplicationQuit() => Save();

        // ---------------------------------------------------------------- world

        private void CreateWorld()
        {
            if (FindFirstObjectByType<Light>() == null)
            {
                var sun = new GameObject("Sun");
                var light = sun.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(52, -35, 0);
            }
            RenderSettings.ambientLight = new Color(0.57f, 0.69f, 0.79f);

            var cast = new List<string>();
            foreach (var ch in library.Chapters)
                if (!cast.Contains(ch.ContactName)) cast.Add(ch.ContactName);
            foreach (var key in new[] { "NOAH", "AURA", "VANTAGE", "PIP04" })
                if (library.SpeakerNames.TryGetValue(key, out var n) && !cast.Contains(n)) cast.Add(n);
            SynteraWorld.Build(cast);

            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player - " + PlayerName;
            player.transform.position = SynteraWorld.PlacePosition(0) + new Vector3(2, 0, -6);
            Destroy(player.GetComponent<Collider>());
            controller = player.AddComponent<CharacterController>();
            controller.height = 2.25f;
            controller.radius = .38f;
            controller.center = Vector3.zero;
            player.GetComponent<Renderer>().material.color = new Color(.22f, .85f, .73f);
            SynteraCharacterAssets.Attach(player, PlayerName, true);
            player.AddComponent<SynteraPlayerPresentation>();

            followCamera = Camera.main;
            if (followCamera == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                followCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }
            followCamera.fieldOfView = 65;
            followCamera.nearClipPlane = .08f;

            drivePod = SynteraDrivePod.Spawn();
            drivePod.RegisterPlayer(player, controller);
            cinematic = gameObject.AddComponent<SynteraCinematicDirector>();
            cinematic.Initialize(followCamera, player.transform);
            audioDirector = gameObject.AddComponent<SynteraAudioDirector>();
            audioDirector.Initialize();
            objectives = gameObject.AddComponent<SynteraMissionObjectives>();
            future = gameObject.AddComponent<SynteraFuturePrototype>();
        }

        // Chapter location -> one of the procedural place beacons.
        private string ChapterPlace(ChapterScript chapter)
        {
            string loc = chapter.Location;
            if (loc.StartsWith("Loopline")) return "Loopline HQ";
            if (loc.Contains("Council")) return "Civic Hall";
            string best = null;
            int bestIndex = int.MaxValue;
            foreach (var place in SynteraWorld.Places)
            {
                int i = loc.IndexOf(place, StringComparison.Ordinal);
                if (i >= 0 && i < bestIndex) { best = place; bestIndex = i; }
            }
            return best ?? "Nexus Junction";
        }

        private SynteraCharacter FindActor(string displayName)
        {
            foreach (var actor in FindObjectsByType<SynteraCharacter>(FindObjectsSortMode.None))
                if (actor.characterName == displayName) return actor;
            return null;
        }

        private SynteraCharacter Contact => Director == null ? null : FindActor(Director.Chapter.ContactName);

        private void OnChapterEntered()
        {
            var chapter = Director.Chapter;
            audioDirector.SetChapter(Director.ChapterIndex);
            Vector3 place = SynteraWorld.PlacePosition(ChapterPlace(chapter));
            var contact = Contact;
            if (contact != null)
            {
                Vector3 home = new Vector3(place.x + 3.5f, 1.15f, place.z + 3.5f);
                var anim = contact.GetComponent<SynteraActorAnimation>();
                if (anim != null) anim.SetHomePosition(home);
                else contact.transform.position = home;
            }
            introCardPlayed = false;
            decisionMessage = "";
        }

        private void SyncObjectives()
        {
            var next = Director.Phase == DirectorPhase.FieldWork ? Director.NextObjective : null;
            Vector3 place = SynteraWorld.PlacePosition(ChapterPlace(Director.Chapter));
            int sequence = Director.CompletedObjectives.Count();
            objectives.Show(next, player.transform, place, sequence);
            if (next != null && next.Kind == ObjectiveKind.Drive) drivePod.ConfigureDelivery(next.Id, place);
            else if (!drivePod.IsDriving) drivePod.ConfigureDelivery(null, Vector3.zero);
        }

        // ---------------------------------------------------------------- frame loop

        private void Update()
        {
            if (fatalError != null || Director == null) return;
            if (!showHelp && !showMap && Director.Phase != DirectorPhase.Complete)
                ElapsedSeconds += Time.unscaledDeltaTime;

            if (Director.ChapterIndex != lastChapter) { lastChapter = Director.ChapterIndex; OnChapterEntered(); }
            if (Director.Phase != lastPhase) { lastPhase = Director.Phase; scroll = Vector2.zero; }
            SyncObjectives();
            UpdateVoiceAndSpeaker();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (confirmRestart) confirmRestart = false;
                else if (showMap) showMap = false;
                else showHelp = !showHelp;
            }
            if (Input.GetKeyDown(KeyCode.F3)) audioDirector.ToggleMute();
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) textScale = Mathf.Min(1.8f, textScale + .1f);
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) textScale = Mathf.Max(.8f, textScale - .1f);
            if (Input.GetKeyDown(KeyCode.M) && !cinematic.IsPlaying) showMap = !showMap;

            if (cinematic.IsPlaying) { InputLocked = true; cinematic.UpdateCamera(); return; }

            if (Director.Phase == DirectorPhase.ChapterIntro && !showHelp)
            {
                if (!introCardPlayed)
                {
                    introCardPlayed = true;
                    var ch = Director.Chapter;
                    cinematic.PlayAt("SYNTERA · " + ch.Year + "\n" + ch.Chapter.ToString("00") + ". " + ch.Title, Contact != null ? Contact.transform : null, 3.6f);
                    return;
                }
                Director.BeginChapter();
            }

            bool freeRoam = Director.Phase == DirectorPhase.FieldWork;
            InputLocked = showMap || showHelp || confirmRestart || !freeRoam || future.IsOpen;

            if (Director.ShowingLines && !showHelp && !showMap &&
                (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
                Director.Advance();

            if (freeRoam && !InputLocked)
            {
                if (Input.GetKeyDown(KeyCode.F)) drivePod.ToggleDriver();
                var next = Director.NextObjective;
                if (next != null && next.Kind == ObjectiveKind.Drive && drivePod.RouteComplete)
                {
                    Director.CompleteObjective(next.Id);
                    notice = "Arrived. " + (Director.NextObjective != null ? "Next: " + Director.NextObjective.Label : "Find " + Director.Chapter.ContactName + ".");
                }
                if (Input.GetKeyDown(KeyCode.E) && !drivePod.IsDriving) Interact();
                if (!drivePod.IsDriving) MovePlayer();
            }
            UpdateCamera();
        }

        private void Interact()
        {
            var next = Director.NextObjective;
            if (next != null && next.Kind == ObjectiveKind.Interact && objectives.InRange)
            {
                Director.CompleteObjective(next.Id);
                notice = "Done: " + next.Label + ".";
                return;
            }
            var contact = Contact;
            if (contact == null || Vector3.Distance(player.transform.position, contact.transform.position) > InteractionRadius) return;
            if (Director.CanTalkToContact) { Director.StartConversation(); notice = ""; }
            else if (next != null) notice = contact.characterName + ": \"" + "First: " + next.Label + ".\"";
        }

        private void UpdateVoiceAndSpeaker()
        {
            var line = Director.CurrentLine;
            string id = line != null ? line.Id : null;
            if (id == lastVoiceLine) { AnimateSpeaker(); return; }
            lastVoiceLine = id;
            SetSpeaking(speakingActor, false);
            speakingActor = null;
            if (line == null) { audioDirector.StopVoice(); return; }
            audioDirector.PlayVoice(line.Id);
            speakingActor = FindActor(line.SpeakerName);
            SetSpeaking(speakingActor, true);
        }

        private void AnimateSpeaker()
        {
            if (speakingActor == null) return;
            var anim = speakingActor.GetComponent<SynteraActorAnimation>();
            if (anim != null) anim.SetSpeaking(true, audioDirector.VoicePlaying ? audioDirector.DialogueLevel : .35f);
        }

        private static void SetSpeaking(SynteraCharacter actor, bool speaking)
        {
            if (actor == null) return;
            actor.GetComponent<SynteraActorAnimation>()?.SetSpeaking(speaking, .35f);
            actor.GetComponent<SynteraHumanoidMotionBridge>()?.SetTalking(speaking);
        }

        private void MovePlayer()
        {
            float x = Input.GetAxisRaw("Horizontal"), z = Input.GetAxisRaw("Vertical");
            Vector3 input = Vector3.ClampMagnitude(new Vector3(x, 0, z), 1);
            Vector3 move = Quaternion.Euler(0, cameraYaw, 0) * input;
            float speed = Input.GetKey(KeyCode.LeftShift) ? 7f : 4.5f;
            if (move.sqrMagnitude > .01f)
            {
                player.transform.rotation = Quaternion.Slerp(player.transform.rotation, Quaternion.LookRotation(move), Time.deltaTime * 12);
                controller.Move(move * speed * Time.deltaTime);
            }
            velocity.y = controller.isGrounded ? -1 : velocity.y - 20f * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }

        private void UpdateCamera()
        {
            if (Input.GetMouseButton(1) && !showMap && !showHelp)
            {
                cameraYaw += Input.GetAxis("Mouse X") * 3f;
                cameraPitch = Mathf.Clamp(cameraPitch - Input.GetAxis("Mouse Y") * 2f, 10f, 65f);
            }
            Vector3 focus = (drivePod.IsDriving ? drivePod.FollowTarget.position : player.transform.position) + Vector3.up * 1.4f;
            Quaternion orbit = Quaternion.Euler(cameraPitch, cameraYaw, 0);
            followCamera.transform.position = Vector3.Lerp(followCamera.transform.position, focus + orbit * new Vector3(0, 0, -9), 12f * Time.deltaTime);
            followCamera.transform.LookAt(focus);
        }

        // ---------------------------------------------------------------- UI (IMGUI, subtitles-first)

        private void InitStyles()
        {
            if (Mathf.Approximately(stylesScale, textScale)) return;
            stylesScale = textScale;
            heading = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(23 * textScale), fontStyle = FontStyle.Bold, wordWrap = true };
            paragraph = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(17 * textScale), wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(14 * textScale), wordWrap = true };
            speaker = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(15 * textScale), fontStyle = FontStyle.Bold, wordWrap = true };
            action = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(15 * textScale), wordWrap = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 6, 6) };
        }

        private void OnGUI()
        {
            InitStyles();
            if (fatalError != null)
            {
                GUI.Box(new Rect(20, 20, Screen.width - 40, 140), "");
                GUI.Label(new Rect(34, 30, Screen.width - 68, 120), "SYNTERA cannot start.\n" + fatalError, paragraph);
                return;
            }
            if (cinematic != null && cinematic.IsPlaying) return;
            DrawHud();
            if (showHelp) { DrawHelp(); return; }
            if (showMap) { DrawMap(); return; }
            if (confirmRestart) { DrawConfirmRestart(); return; }
            if (future != null && future.IsOpen) { future.Draw(heading, paragraph, action, textScale); return; }

            switch (Director.Phase)
            {
                case DirectorPhase.FieldWork: DrawFieldWork(); break;
                case DirectorPhase.Conversation:
                case DirectorPhase.Reaction:
                case DirectorPhase.CharterSpeech:
                case DirectorPhase.Ending: DrawLine(); break;
                case DirectorPhase.Decision: DrawDecision(); break;
                case DirectorPhase.YearSummary: DrawYearSummary(); break;
                case DirectorPhase.Evidence: DrawEvidence(); break;
                case DirectorPhase.CharterChoice: DrawCharterChoice(); break;
                case DirectorPhase.VoteResult: DrawVote(); break;
                case DirectorPhase.Recap:
                case DirectorPhase.Complete: DrawRecap(); break;
            }
        }

        private float PanelWidth => Mathf.Min(Screen.width - 24, 720 * Mathf.Max(1f, textScale * .9f));

        private Rect Panel(float height)
        {
            height *= textScale;
            float y = Mathf.Max(130, Screen.height - height - 12);
            var r = new Rect(12, y, PanelWidth, Mathf.Min(height, Screen.height - 142));
            GUI.Box(r, "");
            return r;
        }

        private void DrawHud()
        {
            var s = Director.Engine.State;
            var ch = Director.Chapter;
            float w = Mathf.Min(Screen.width - 150, 640);
            GUI.Box(new Rect(12, 12, w, 104 * textScale), "");
            GUI.Label(new Rect(23, 16, w - 20, 34 * textScale), "SYNTERA · " + ch.Year + " · " + ch.Chapter.ToString("00") + " " + ch.Title, heading);
            GUI.Label(new Rect(23, 16 + 36 * textScale, w - 20, 64 * textScale),
                "Credits " + s.Get("credits") + "   Treasury " + s.Get("treasury") + "   Trust " + s.Get("trust") + "   Jobs " + s.Get("jobs") +
                "\nAutonomy " + s.Get("autonomy") + "   Grid " + s.Get("grid") + "   VANTAGE influence " + s.Get("vantageInfluence") +
                "   " + Mathf.FloorToInt(ElapsedSeconds / 60f) + "m", small);
            if (GUI.Button(new Rect(Screen.width - 128, 15, 116, 36), "MAP [M]")) showMap = !showMap;
            if (GUI.Button(new Rect(Screen.width - 128, 57, 116, 36), "HELP [Esc]")) showHelp = !showHelp;
        }

        private void DrawHelp()
        {
            var r = Panel(330);
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 20));
            GUILayout.Label("Syntera: The Last Human Decision", heading);
            GUILayout.Label("WASD walk or drive · Shift sprint · Right mouse orbit camera\n" +
                            "E interact with orange objective markers and your contact · F enter/exit the e-boda pod\n" +
                            "Space or Enter: next line · M map · F3 mute · + / - text size · Esc help", paragraph);
            GUILayout.Label("A fictional city. Every number is a game rule, not a forecast. Costs, shortfalls and council votes are always shown in full.", small);
            if (!string.IsNullOrEmpty(notice)) GUILayout.Label(notice, small);
            if (GUILayout.Button(Director.Phase == DirectorPhase.ChapterIntro && Director.ChapterIndex == 0 ? "Start 2026" : "Resume", action, GUILayout.Height(40 * textScale)))
                showHelp = false;
            GUILayout.EndArea();
        }

        private void DrawMap()
        {
            var r = Panel(150 + 50 * ((SynteraWorld.Places.Length + 1) / 2));
            GUI.Label(new Rect(r.x + 14, r.y + 10, r.width - 28, 34 * textScale), "Syntera City", heading);
            float bw = (r.width - 42) / 2;
            for (int i = 0; i < SynteraWorld.Places.Length; i++)
            {
                var rect = new Rect(r.x + 14 + (i % 2) * (bw + 14), r.y + 52 * textScale + (i / 2) * 46 * textScale, bw, 40 * textScale);
                if (GUI.Button(rect, SynteraWorld.Places[i], action))
                {
                    if (drivePod.IsDriving) drivePod.ForceExit();
                    controller.enabled = false;
                    player.transform.position = SynteraWorld.PlacePosition(i) + new Vector3(2, 0, -6);
                    controller.enabled = true;
                    notice = "Arrived at " + SynteraWorld.Places[i] + ".";
                    showMap = false;
                }
            }
            GUI.Label(new Rect(r.x + 14, r.yMax - 34 * textScale, r.width - 28, 30 * textScale), "Placeholder procedural city. Production environments are not yet built.", small);
        }

        private void DrawFieldWork()
        {
            var r = Panel(170);
            var ch = Director.Chapter;
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 20));
            GUILayout.Label(ch.Objective, paragraph);
            var next = Director.NextObjective;
            string task;
            if (next == null)
            {
                var c = Contact;
                string dist = c == null ? "" : " (" + Mathf.CeilToInt(Vector3.Distance(player.transform.position, c.transform.position)) + " m)";
                task = "Talk to " + ch.ContactName + dist + ". Press E nearby.";
            }
            else if (next.Kind == ObjectiveKind.Drive)
                task = "DRIVE: " + next.Label + " · " + (drivePod.IsDriving ? Mathf.CeilToInt(drivePod.DistanceToWaypoint) + " m to the orange beacon" : "press F at the cyan pod");
            else
                task = "OBJECTIVE: " + next.Label + " (" + Mathf.CeilToInt(objectives.Distance) + " m). Press E at the orange marker.";
            GUILayout.Label(task, paragraph);
            if (!string.IsNullOrEmpty(notice)) GUILayout.Label(notice, small);
            GUILayout.EndArea();
        }

        private void DrawLine()
        {
            var line = Director.CurrentLine;
            if (line == null) return;
            var r = Panel(190);
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 20));
            GUILayout.Label(line.SpeakerName + (string.IsNullOrEmpty(line.Delivery) ? "" : " (" + line.Delivery + ")"), speaker);
            GUILayout.Label(line.Text, paragraph);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Continue  [Space]   " + (Director.LineIndex + 1) + " / " + Director.LineCount, action, GUILayout.Height(42 * textScale)))
                Director.Advance();
            GUILayout.EndArea();
        }

        private void DrawDecision()
        {
            var decision = Director.Decision;
            var r = Panel(420);
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 20));
            GUILayout.Label("Decision · " + decision.Title, heading);
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var option in decision.Options)
            {
                var status = Director.Status(option.Id);
                GUILayout.Space(6);
                GUILayout.Label(option.Id + " · " + option.Label, speaker);
                GUILayout.Label(option.Summary, paragraph);
                GUILayout.Label("Effects: " + DescribeEffects(option), small);
                GUI.enabled = status.Affordable;
                if (GUILayout.Button(status.Affordable ? "Choose " + option.Label : "Cannot afford: " + status.Explanation, action, GUILayout.MinHeight(38 * textScale)))
                {
                    var result = Director.Choose(option.Id);
                    decisionMessage = result.Committed ? "" : result.Message;
                }
                GUI.enabled = true;
                if (status.CreditLineOffered &&
                    GUILayout.Button("Accept Naomi's one-time credit line (+" + rules.CreditLineAmount + " Credits, Autonomy -4)" +
                                     (status.CreditsShortfallAfterCreditLine > 0 ? " — still " + status.CreditsShortfallAfterCreditLine + " short afterwards" : ""),
                                     action, GUILayout.MinHeight(34 * textScale)))
                    decisionMessage = Director.TakeCreditLine(option.Id).Message;
            }
            GUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(decisionMessage)) GUILayout.Label(decisionMessage, small);
            GUILayout.EndArea();
        }

        private string DescribeEffects(OptionDef option)
        {
            var parts = new List<string>();
            foreach (var e in option.Effects) parts.Add(rules.StatLabel(e.Stat) + " " + (e.Delta > 0 ? "+" : "") + e.Delta);
            return string.Join(", ", parts);
        }

        private void DrawYearSummary()
        {
            var r = Panel(300);
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 20));
            GUILayout.Label("A new year in Syntera", heading);
            foreach (var part in (Director.YearSummary ?? "").Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries))
                GUILayout.Label("• " + part, paragraph);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Continue", action, GUILayout.Height(42 * textScale))) Director.ContinueFromYearSummary();
            GUILayout.EndArea();
        }

        private void DrawEvidence()
        {
            var r = Panel(380);
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 20));
            GUILayout.Label("Evidence for the council", heading);
            foreach (var slot in Director.EvidenceSlots)
                GUILayout.Label((slot.Earned ? "[PRESENTED] " : "[EMPTY] ") + slot.Title + (slot.Earned ? "" : " — earned by: " + slot.EarnedBy), paragraph);
            GUILayout.Label("Nothing can be invented. Empty slots show the choice that would have earned them.", small);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Continue to the charter proposal", action, GUILayout.Height(42 * textScale))) Director.ContinueFromEvidence();
            GUILayout.EndArea();
        }

        private void DrawCharterChoice()
        {
            var r = Panel(380);
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 20));
            GUILayout.Label("Propose one charter · 5 of 9 seats pass it · no time limit", heading);
            foreach (var charter in rules.Charters)
            {
                GUILayout.Space(6);
                if (GUILayout.Button(charter.Title + "\n" + charter.Summary, action, GUILayout.MinHeight(58 * textScale)))
                    Director.ProposeCharter(charter.Id);
            }
            GUILayout.EndArea();
        }

        private void DrawVote()
        {
            var vote = Director.Vote;
            var r = Panel(400);
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 20));
            GUILayout.Label(vote.CharterTitle + ": " + vote.SeatsFor + " of " + vote.TotalSeats + " seats · " + (vote.Passed ? "PASSED" : "FAILED"), heading);
            foreach (var v in vote.Votes) GUILayout.Label(v.BlocName + ": " + v.Reason, paragraph);
            GUILayout.Label(vote.EndingReason, small);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Continue to tomorrow morning", action, GUILayout.Height(42 * textScale))) Director.ContinueFromVote();
            GUILayout.EndArea();
        }

        private void DrawRecap()
        {
            var recap = Director.Recap();
            if (recap == null) return;
            var r = Panel(Screen.height);
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 20));
            GUILayout.Label("SYNTERA WILL REMEMBER · " + recap.Ending.Title, heading);
            scroll = GUILayout.BeginScrollView(scroll);
            if (recap.Script != null)
            {
                GUILayout.Label(recap.Script.Outcome, paragraph);
                GUILayout.Label("Post-credits: " + recap.Script.PostCreditSting, small);
            }
            GUILayout.Label("Council: " + recap.Vote.CharterTitle + " " + recap.Vote.SeatsFor + "/" + recap.Vote.TotalSeats + ". " + recap.Vote.EndingReason, paragraph);
            foreach (var v in recap.Vote.Votes) GUILayout.Label("  " + v.BlocName + ": " + v.Reason, small);
            GUILayout.Label("Your decisions", speaker);
            foreach (var c in recap.Choices) GUILayout.Label("  " + c.Chapter + ": " + c.Choice, small);
            GUILayout.Label("City indicators", speaker);
            GUILayout.Label("  " + string.Join("   ", recap.Indicators.Select(kv => rules.StatLabel(kv.Key) + " " + kv.Value)), small);
            GUILayout.Label("Loopline ownership: " + string.Join(", ", recap.Ownership.Select(kv => kv.Key + " " + kv.Value + "%")) +
                            " · Evidence cards " + recap.EvidenceCount + (recap.UsedCreditLine ? " · Naomi's credit line used (repaid in the 2050 continuation)" : ""), small);
            GUILayout.Label("This ending is one scenario for a fictional city, not a prediction of 2050. Playtime " +
                            Mathf.FloorToInt(ElapsedSeconds / 60f) + "m " + Mathf.FloorToInt(ElapsedSeconds % 60f) + "s.", small);
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Play again", action, GUILayout.Height(44 * textScale))) confirmRestart = true;
            if (GUILayout.Button("Continue to 2050 (prototype)", action, GUILayout.Height(44 * textScale)))
            {
                Director.FinishRecap();
                future.Open(Director.Engine.State);
            }
            if (GUILayout.Button("Exit", action, GUILayout.Height(44 * textScale))) { Director.FinishRecap(); Save(); Application.Quit(); }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawConfirmRestart()
        {
            var r = Panel(200);
            GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 20));
            GUILayout.Label("Start a new 2026 run?", heading);
            GUILayout.Label("Your finished campaign is kept as a backup file. The new run starts from the beginning.", paragraph);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Yes, start again", action, GUILayout.Height(44 * textScale))) Restart();
            if (GUILayout.Button("Cancel", action, GUILayout.Height(44 * textScale))) confirmRestart = false;
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
