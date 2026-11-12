using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Syntera.Core;
using UnityEngine;

namespace Syntera
{
    // Unity presentation layer for the screenplay v1.1 campaign. All campaign state lives in
    // Syntera.Core (CampaignDirector -> CampaignEngine); this class renders the current phase,
    // runs the 3D world and set pieces, and forwards player input. It never changes numbers directly.
    public sealed class SynteraGame : MonoBehaviour
    {
        public static SynteraGame Instance { get; private set; }
        public static bool InputLocked { get; private set; }

        public CampaignDirector Director { get; private set; }
        public string PlayerName { get; private set; } = "Player";
        public float ElapsedSeconds { get; private set; }

        private CampaignRules rules;
        private DialogueLibrary library;
        private ActivityLibrary activities;
        private string fatalError;
        private string notice = "";
        private string toast = "";
        private float toastUntil;
        private bool recoveredFromBackup;

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
        private SynteraSetPieces setPieces;
        private readonly SynteraUi ui = new SynteraUi();

        private bool showMap, showHelp = true, showSettings, showHistory, confirmRestart;
        private bool rebinding;
        private string rebindName;
        private bool introCardPlayed;
        private string decisionMessage = "";
        private int lastChapter = -1;
        private DirectorPhase lastPhase = (DirectorPhase)(-1);
        private string lastVoiceLine;
        private SynteraCharacter speakingActor;
        private Vector2 scroll;
        private ActivitySession activity;
        private string activityObjective;
        private float rescueTimer = -1;
        private const float RescueSeconds = 300f;
        private readonly List<string> history = new List<string>();
        private string headline;
        private bool capturingCard;
        private float fpsSmoothed;

        private const float InteractionRadius = 5f;
        // Tests point saves at a temporary folder so they never touch a player's real save.
        public static string SaveDirectoryOverride { get; set; }
        private static string SaveDir => SaveDirectoryOverride ?? Application.persistentDataPath;
        private static string SavePath => Path.Combine(SaveDir, "syntera-campaign.json");
        private static string LegacySavePath => Path.Combine(SaveDir, "syntera-city-save.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<SynteraGame>() == null)
                new GameObject("SYNTERA - World Controller").AddComponent<SynteraGame>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this && Instance.gameObject.scene.isLoaded) { Destroy(gameObject); return; }
            Instance = this;
            Application.targetFrameRate = 60;
            ui.Settings = SynteraSettings.Load();
            SynteraCinematicDirector.ReduceMotion = ui.Settings.ReduceMotion;
            try
            {
                rules = CampaignRules.Load(LoadResource("syntera_campaign_rules"));
                library = DialogueLibrary.Load(LoadResource("syntera_campaign_dialogue"), rules);
                activities = ActivityLibrary.Load(LoadResource("syntera_activities"), library);
            }
            catch (Exception ex)
            {
                fatalError = "Campaign data failed validation: " + ex.Message;
                Debug.LogError("[SYNTERA] " + fatalError);
                return;
            }
            ui.SpeakerName = k => library.SpeakerNames.TryGetValue(k, out var n) ? n : k;
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
            if (File.Exists(LegacySavePath))
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
            future.Close();
            drivePod.ResetPod();
            drivePod.ConfigureDelivery(null, Vector3.zero);
            lastChapter = -1;
            lastPhase = (DirectorPhase)(-1);
            confirmRestart = false;
            history.Clear();
            headline = null;
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
            foreach (var key in new[] { "NOAH", "AURA", "VANTAGE" })
                if (library.SpeakerNames.TryGetValue(key, out var n) && !cast.Contains(n)) cast.Add(n);
            SynteraWorld.Build(cast);
            setPieces = gameObject.AddComponent<SynteraSetPieces>();
            setPieces.Build();

            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player - " + PlayerName;
            player.transform.position = SynteraWorld.PlacePosition(0) + new Vector3(2, 0, -6);
            Destroy(player.GetComponent<Collider>());
            controller = player.AddComponent<CharacterController>();
            controller.height = 2.25f;
            controller.radius = .38f;
            controller.center = Vector3.zero;
            controller.stepOffset = .45f;
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
            drivePod.name = "E-moto (placeholder)";
            drivePod.RegisterPlayer(player, controller);
            cinematic = gameObject.AddComponent<SynteraCinematicDirector>();
            cinematic.Initialize(followCamera, player.transform);
            audioDirector = gameObject.AddComponent<SynteraAudioDirector>();
            audioDirector.Initialize();
            objectives = gameObject.AddComponent<SynteraMissionObjectives>();
            future = gameObject.AddComponent<SynteraFuturePrototype>();
        }

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

        private void PlaceActor(SynteraCharacter actor, Vector3 at)
        {
            if (actor == null) return;
            var anim = actor.GetComponent<SynteraActorAnimation>();
            if (anim != null) anim.SetHomePosition(at); else actor.transform.position = at;
        }

        private void Teleport(Vector3 at)
        {
            if (drivePod.IsDriving) drivePod.ForceExit();
            controller.enabled = false;
            player.transform.position = at;
            controller.enabled = true;
        }

        private void OnChapterEntered(int previous)
        {
            var chapter = Director.Chapter;
            if (previous >= 0 && previous < Director.ChapterIndex && library.Chapters[previous].Cliffhanger != null)
                Toast(library.Chapters[previous].Cliffhanger);
            audioDirector.SetChapter(Director.ChapterIndex);
            Vector3 place = SynteraWorld.PlacePosition(ChapterPlace(chapter));
            PlaceActor(Contact, new Vector3(place.x + 3.5f, 1.15f, place.z + 3.5f));
            if (chapter.Id == "ST01") PlaceActor(FindActor(library.SpeakerNames["NOAH"]), setPieces.NoahPosition);
            if (Director.Phase == DirectorPhase.ChapterIntro || Director.Phase == DirectorPhase.FieldWork)
            {
                bool drive = chapter.Objectives.Any(o => o.Kind == ObjectiveKind.Drive && !Director.CompletedObjectives.Contains(o.Id));
                if (drive)
                {
                    drivePod.ResetPod();
                    drivePod.SetRoute(setPieces.RideRoute);
                    drivePod.Autopilot = ui.Settings.AutoDrive;
                    Teleport(drivePod.transform.position + new Vector3(3, .4f, 0));
                }
                else Teleport(place + new Vector3(2, 0, -6));
            }
            introCardPlayed = false;
            rescueTimer = -1;
            decisionMessage = "";
        }

        private void OnPhaseEntered()
        {
            scroll = Vector2.zero;
            ui.ResetSelection();
            setPieces.ApplyWorldState(Director);
            if (Director.Phase == DirectorPhase.YearSummary)
                PlayCard("SYNTERA · " + (Director.Chapter.Year + 1) + "\nA year passes. Dividends, taxes and delayed effects arrive.", 4f);
            if (Director.Phase == DirectorPhase.VoteResult)
                PlayCard("The council votes: " + Director.Vote.SeatsFor + " of " + Director.Vote.TotalSeats + " for the " + Director.Vote.CharterTitle, 3f);
        }

        private void PlayCard(string text, float seconds)
        {
            var c = Contact;
            cinematic.PlayAt(text, c != null ? c.transform : null, seconds);
        }

        private void Toast(string text)
        {
            toast = text;
            toastUntil = Time.unscaledTime + 7f;
        }

        private void SyncObjectives()
        {
            var next = Director.Phase == DirectorPhase.FieldWork ? Director.NextObjective : null;
            Vector3 place = SynteraWorld.PlacePosition(ChapterPlace(Director.Chapter));
            Vector3 exact = Vector3.zero;
            bool hasExact = next != null && setPieces.TryMarker(next.Id, out exact);

            objectives.Show(next, player.transform, place, Director.CompletedObjectives.Count(), hasExact ? exact : (Vector3?)null);
            if (next != null && next.Kind == ObjectiveKind.Drive) drivePod.ConfigureDelivery(next.Id, place);
            else if (!drivePod.IsDriving) drivePod.ConfigureDelivery(null, Vector3.zero);
        }

        // ---------------------------------------------------------------- frame loop

        private void Update()
        {
            if (fatalError != null || Director == null) return;
            if (!showHelp && !showMap && !showSettings && Director.Phase != DirectorPhase.Complete)
                ElapsedSeconds += Time.unscaledDeltaTime;
            fpsSmoothed = Mathf.Lerp(fpsSmoothed, 1f / Mathf.Max(Time.unscaledDeltaTime, .0001f), .05f);

            if (Director.ChapterIndex != lastChapter) { int prev = lastChapter; lastChapter = Director.ChapterIndex; OnChapterEntered(prev); }
            if (Director.Phase != lastPhase) { lastPhase = Director.Phase; OnPhaseEntered(); }
            SyncObjectives();
            UpdateVoiceAndSpeaker();

            if (rebinding) return; // OnGUI captures the next key
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.JoystickButton7))
            {
                if (activity != null) CloseActivity();
                else if (confirmRestart) confirmRestart = false;
                else if (showSettings) showSettings = false;
                else if (showHistory) showHistory = false;
                else if (showMap) showMap = false;
                else showHelp = !showHelp;
            }
            if (Input.GetKeyDown(KeyCode.F3)) audioDirector.ToggleMute();
            if (Input.GetKeyDown(KeyCode.F9)) { ui.Settings.ShowPerformance = !ui.Settings.ShowPerformance; ui.Settings.Save(); }
            if (Input.GetKeyDown(ui.Settings.Options)) showSettings = !showSettings;
            if (Input.GetKeyDown(ui.Settings.History)) showHistory = !showHistory;
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) SetTextScale(ui.Settings.TextScale + .1f);
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) SetTextScale(ui.Settings.TextScale - .1f);
            if ((Input.GetKeyDown(ui.Settings.Map) || Input.GetKeyDown(KeyCode.JoystickButton6)) && !cinematic.IsPlaying) showMap = !showMap;

            if (cinematic.IsPlaying) { InputLocked = true; cinematic.UpdateCamera(); return; }

            bool overlay = showMap || showHelp || showSettings || showHistory || confirmRestart || future.IsOpen;
            ui.HandleInput(!Director.ShowingLines || overlay || activity != null);

            if (Director.Phase == DirectorPhase.ChapterIntro && !overlay)
            {
                if (!introCardPlayed)
                {
                    introCardPlayed = true;
                    var ch = Director.Chapter;
                    PlayCard("SYNTERA · " + ch.Year + "\n" + ch.Chapter.ToString("00") + ". " + ch.Title, 3.6f);
                    return;
                }
                Director.BeginChapter();
            }

            bool freeRoam = Director.Phase == DirectorPhase.FieldWork && activity == null;
            InputLocked = overlay || !freeRoam;

            if (Director.ShowingLines && !overlay &&
                (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.JoystickButton0)))
                Director.Advance();

            if (freeRoam && !overlay)
            {
                UpdateRescue();
                UpdateDrive();
                if ((Input.GetKeyDown(ui.Settings.Interact) || Input.GetKeyDown(KeyCode.JoystickButton0)) && !drivePod.IsDriving) Interact();
                if (!drivePod.IsDriving) MovePlayer();
            }
            UpdateCamera();
        }

        private void SetTextScale(float v)
        {
            ui.Settings.TextScale = Mathf.Clamp(v, .8f, 1.8f);
            ui.Settings.Save();
        }

        private void UpdateRescue()
        {
            var next = Director.NextObjective;
            if (next == null || next.Kind != ObjectiveKind.Rescue) { rescueTimer = -1; return; }
            if (rescueTimer < 0) rescueTimer = RescueSeconds;
            rescueTimer -= Time.deltaTime;
            if (rescueTimer > 0) return;
            // Screenplay: a safety drone rescues Noah and the player loses 2 Trust. No dead end.
            Director.ApplyOutcome("ST01_RESCUE_BY_DRONE");
            Director.CompleteObjective(next.Id);
            Toast("A safety drone reached Noah before you did. Trust -2.");
            rescueTimer = -1;
        }

        private void UpdateDrive()
        {
            var next = Director.NextObjective;
            if (next == null || next.Kind != ObjectiveKind.Drive) return;
            if (Input.GetKeyDown(ui.Settings.Drive) || Input.GetKeyDown(KeyCode.JoystickButton3)) drivePod.ToggleDriver();
            if (drivePod.IsDriving)
            {
                if (Input.GetKeyDown(KeyCode.P) && (drivePod.Impacts >= 3 || ui.Settings.AutoDrive)) drivePod.Autopilot = !drivePod.Autopilot;
                if (Input.GetKeyDown(KeyCode.R)) drivePod.ResetToRoadside();
            }
            if (drivePod.RouteComplete)
            {
                Director.CompleteObjective(next.Id);
                Toast("You made it to Lumen Gardens. The drone is over the cake.");
            }
        }

        private void Interact()
        {
            var next = Director.NextObjective;
            if (next != null && next.Kind != ObjectiveKind.Drive && objectives.InRange)
            {
                if (next.Kind == ObjectiveKind.Rescue)
                {
                    Director.CompleteObjective(next.Id);
                    Toast("You pulled the rescue lever. Noah is safe.");
                    rescueTimer = -1;
                    return;
                }
                if (next.Activity != null) { OpenActivity(next); return; }
                Director.CompleteObjective(next.Id);
                notice = "Done: " + next.Label + ".";
                return;
            }
            var contact = Contact;
            if (contact == null || Vector3.Distance(player.transform.position, contact.transform.position) > InteractionRadius) return;
            if (Director.CanTalkToContact) { Director.StartConversation(); notice = ""; }
            else if (next != null) notice = contact.characterName + ": \"First: " + next.Label + ".\"";
        }

        private void OpenActivity(ObjectiveDef objective)
        {
            activity = new ActivitySession(activities.Get(objective.Activity), rules, Director.Engine.State);
            activityObjective = objective.Id;
            ui.ResetSelection();
        }

        private void FinishActivity()
        {
            if (activity != null && activity.Complete) Director.CompleteObjective(activityObjective);
            CloseActivity();
        }

        private void CloseActivity()
        {
            activity = null;
            activityObjective = null;
            ui.ResetSelection();
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
            history.Add(line.Subtitle);
            if (history.Count > 200) history.RemoveAt(0);
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
            float speed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.JoystickButton8) ? 7f : 4.5f;
            if (move.sqrMagnitude > .01f)
            {
                player.transform.rotation = Quaternion.Slerp(player.transform.rotation, Quaternion.LookRotation(move), Time.deltaTime * 12);
                controller.Move(move * speed * Time.deltaTime);
            }
            velocity.y = controller.isGrounded ? -1 : velocity.y - 20f * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
            if (player.transform.position.y < -10) Teleport(SynteraWorld.PlacePosition(ChapterPlace(Director.Chapter)));
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
            float follow = ui.Settings.ReduceMotion ? 30f : 12f;
            followCamera.transform.position = Vector3.Lerp(followCamera.transform.position, focus + orbit * new Vector3(0, 0, -9), follow * Time.deltaTime);
            followCamera.transform.LookAt(focus);
        }

        // ---------------------------------------------------------------- autoplay (PlayMode smoke test)

        private int autoDecision;

        // Advances the real game by one player action. Returns false once the campaign is complete.
        public bool AutoPlayStep(string intent, string charter)
        {
            if (fatalError != null) throw new InvalidOperationException(fatalError);
            showHelp = showMap = showSettings = showHistory = false;
            if (activity != null) { activity.SolveForTest(); FinishActivity(); return true; }
            switch (Director.Phase)
            {
                case DirectorPhase.ChapterIntro: Director.BeginChapter(); break;
                case DirectorPhase.FieldWork:
                    var next = Director.NextObjective;
                    if (next == null)
                    {
                        var c = Contact;
                        if (c == null) throw new InvalidOperationException("Contact missing in world: " + Director.Chapter.ContactName);
                        Teleport(c.transform.position + new Vector3(1.5f, 0, 0));
                        Interact();
                    }
                    else if (next.Kind == ObjectiveKind.Drive) Director.CompleteObjective(next.Id);
                    else if (next.Activity != null) OpenActivity(next);
                    else Director.CompleteObjective(next.Id);
                    break;
                case DirectorPhase.Decision:
                    string want = intent[autoDecision++ % intent.Length].ToString();
                    var status = Director.Status(want);
                    if (!status.Affordable && status.CreditLineOffered) Director.TakeCreditLine(want);
                    if (!Director.Status(want).Affordable) want = "B";
                    Director.Choose(want);
                    break;
                case DirectorPhase.YearSummary: Director.ContinueFromYearSummary(); break;
                case DirectorPhase.Evidence: Director.ContinueFromEvidence(); break;
                case DirectorPhase.CharterChoice: Director.ProposeCharter(charter); break;
                case DirectorPhase.VoteResult: Director.ContinueFromVote(); break;
                case DirectorPhase.Recap: Director.FinishRecap(); break;
                case DirectorPhase.Complete: return false;
                default: Director.Advance(); break;
            }
            return true;
        }

        // ---------------------------------------------------------------- UI (IMGUI, subtitles-first)

        private void OnGUI()
        {
            ui.Styles();
            ui.BeginGui();
            if (fatalError != null)
            {
                ui.Backdrop(new Rect(20, 20, Screen.width - 40, 140));
                GUI.Label(new Rect(34, 30, Screen.width - 68, 120), "SYNTERA cannot start.\n" + fatalError, ui.Paragraph);
                return;
            }
            if (rebinding) { CaptureRebind(); DrawRebind(); return; }
            if (cinematic != null && cinematic.IsPlaying) return;
            if (capturingCard) { DrawStoryCard(); return; }
            DrawHud();
            DrawToast();
            if (ui.Settings.ShowPerformance) DrawPerformance();
            if (showHelp) { DrawHelp(); return; }
            if (showSettings) { DrawSettings(); return; }
            if (showHistory) { DrawHistory(); return; }
            if (showMap) { DrawMap(); return; }
            if (confirmRestart) { DrawConfirmRestart(); return; }
            if (future.IsOpen) { future.Draw(ui.Heading, ui.Paragraph, ui.Button, ui.Settings.TextScale); return; }
            if (activity != null)
            {
                ui.DrawActivity(activity, FinishActivity, CloseActivity, f => setPieces.ShowGridAllocation(f));
                return;
            }

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

        private void DrawHud()
        {
            var s = Director.Engine.State;
            var ch = Director.Chapter;
            float sc = ui.Settings.TextScale;
            float w = Mathf.Min(Screen.width - 150, 660);
            ui.Backdrop(new Rect(12, 12, w, 104 * sc));
            GUI.Label(new Rect(23, 16, w - 20, 34 * sc), "SYNTERA · " + ch.Year + " · " + ch.Chapter.ToString("00") + " " + ch.Title, ui.Heading);
            GUI.Label(new Rect(23, 16 + 36 * sc, w - 20, 64 * sc),
                "Credits " + s.Get("credits") + "   Treasury " + s.Get("treasury") + "   Trust " + s.Get("trust") + "   Jobs " + s.Get("jobs") +
                "\nAutonomy " + s.Get("autonomy") + "   Grid " + s.Get("grid") + "   VANTAGE influence " + s.Get("vantageInfluence") +
                "   " + Mathf.FloorToInt(ElapsedSeconds / 60f) + "m", ui.Small);
            if (GUI.Button(new Rect(Screen.width - 128, 15, 116, 34), "MAP [" + ui.Settings.Map + "]")) showMap = !showMap;
            if (GUI.Button(new Rect(Screen.width - 128, 53, 116, 34), "OPTIONS [" + ui.Settings.Options + "]")) showSettings = !showSettings;
            if (GUI.Button(new Rect(Screen.width - 128, 91, 116, 34), "HELP [Esc]")) showHelp = !showHelp;
        }

        private void DrawToast()
        {
            if (Time.unscaledTime > toastUntil || string.IsNullOrEmpty(toast)) return;
            float w = Mathf.Min(Screen.width - 40, 620);
            var r = new Rect((Screen.width - w) / 2, 130 * ui.Settings.TextScale, w, 60 * ui.Settings.TextScale);
            ui.Backdrop(r);
            GUI.Label(new Rect(r.x + 12, r.y + 6, r.width - 24, r.height - 12), toast, ui.Speaker);
        }

        private void DrawPerformance()
        {
            long mb = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            GUI.Label(new Rect(Screen.width - 260, Screen.height - 30, 250, 24),
                Mathf.RoundToInt(fpsSmoothed) + " FPS · " + (1000f / Mathf.Max(fpsSmoothed, 1)).ToString("0.0") + " ms · " + mb + " MB", ui.Small);
        }

        private void Area(Rect r) { GUILayout.BeginArea(new Rect(r.x + 14, r.y + 10, r.width - 28, r.height - 20)); }

        private void DrawHelp()
        {
            Area(ui.Panel(380));
            GUILayout.Label("Syntera: The Last Human Decision", ui.Heading);
            var k = ui.Settings;
            GUILayout.Label("WASD / left stick: walk or drive · Shift: sprint · Right mouse: camera\n" +
                            k.Interact + " / A: use orange markers and talk to your contact · " + k.Drive + " / Y: enter or exit the e-moto\n" +
                            "Space, Enter or A: next line · Arrows, 1-9 or stick: choose · " + k.Map + ": map · " + k.History + ": dialogue history\n" +
                            k.Options + ": options (text size, high contrast, reduced motion, auto-drive, key remapping) · F3: mute · F9: performance", ui.Paragraph);
            GUILayout.Label("A fictional city. Every number is a game rule, not a forecast. Costs, shortfalls and council votes are always shown in full.", ui.Small);
            if (!string.IsNullOrEmpty(notice)) GUILayout.Label(notice, ui.Small);
            ui.Option(Director.Phase == DirectorPhase.ChapterIntro && Director.ChapterIndex == 0 ? "Start 2026" : "Resume", true, () => showHelp = false, 40);
            GUILayout.EndArea();
        }

        private void DrawSettings()
        {
            var s = ui.Settings;
            Area(ui.Panel(470));
            GUILayout.Label("Options", ui.Heading);
            ui.Option("Text size: " + Mathf.RoundToInt(s.TextScale * 100) + "% · larger", true, () => SetTextScale(s.TextScale + .1f), 30);
            ui.Option("Text size: smaller", true, () => SetTextScale(s.TextScale - .1f), 30);
            ui.Option("High contrast: " + (s.HighContrast ? "ON" : "OFF"), true, () => { s.HighContrast = !s.HighContrast; s.Save(); }, 30);
            ui.Option("Reduced camera motion: " + (s.ReduceMotion ? "ON" : "OFF"), true, () => { s.ReduceMotion = !s.ReduceMotion; SynteraCinematicDirector.ReduceMotion = s.ReduceMotion; s.Save(); }, 30);
            ui.Option("E-moto auto-drive: " + (s.AutoDrive ? "ON" : "OFF"), true, () => { s.AutoDrive = !s.AutoDrive; drivePod.Autopilot = s.AutoDrive; s.Save(); }, 30);
            ui.Option("Performance overlay: " + (s.ShowPerformance ? "ON" : "OFF"), true, () => { s.ShowPerformance = !s.ShowPerformance; s.Save(); }, 30);
            ui.Option("Remap interact (" + s.Interact + ")", true, () => { rebinding = true; rebindName = "interact"; }, 30);
            ui.Option("Remap e-moto (" + s.Drive + ")", true, () => { rebinding = true; rebindName = "drive"; }, 30);
            ui.Option("Remap map (" + s.Map + ")", true, () => { rebinding = true; rebindName = "map"; }, 30);
            ui.Option("Close", true, () => showSettings = false, 30);
            GUILayout.EndArea();
        }

        private void DrawRebind()
        {
            Area(ui.Panel(140));
            GUILayout.Label("Press the new key for " + rebindName + " (Esc cancels).", ui.Heading);
            GUILayout.EndArea();
        }

        private void CaptureRebind()
        {
            var e = Event.current;
            if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;
            var s = ui.Settings;
            if (e.keyCode != KeyCode.Escape)
            {
                var reserved = new[] { KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.Space, KeyCode.Return, KeyCode.F3, KeyCode.F9, KeyCode.P, KeyCode.R };
                if (reserved.Contains(e.keyCode)) Toast(e.keyCode + " is reserved.");
                else
                {
                    if (rebindName == "interact") s.Interact = e.keyCode;
                    if (rebindName == "drive") s.Drive = e.keyCode;
                    if (rebindName == "map") s.Map = e.keyCode;
                    s.Save();
                }
            }
            rebinding = false;
            e.Use();
        }

        private void DrawHistory()
        {
            Area(ui.Panel(Screen.height));
            GUILayout.Label("Dialogue history", ui.Heading);
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var h in history) GUILayout.Label(h, ui.Paragraph);
            GUILayout.EndScrollView();
            ui.Option("Close [" + ui.Settings.History + "]", true, () => showHistory = false, 34);
            GUILayout.EndArea();
        }

        private void DrawMap()
        {
            Area(ui.Panel(180 + 46 * ((SynteraWorld.Places.Length + 1) / 2)));
            GUILayout.Label("Syntera City · this chapter: " + ChapterPlace(Director.Chapter), ui.Heading);
            for (int i = 0; i < SynteraWorld.Places.Length; i++)
            {
                int k = i;
                ui.Option(SynteraWorld.Places[i], true, () =>
                {
                    Teleport(SynteraWorld.PlacePosition(k) + new Vector3(2, 0, -6));
                    notice = "Arrived at " + SynteraWorld.Places[k] + ".";
                    showMap = false;
                }, 30);
            }
            GUILayout.Label("Placeholder procedural city. Production environments are not yet built.", ui.Small);
            GUILayout.EndArea();
        }

        private void DrawFieldWork()
        {
            Area(ui.Panel(185));
            var ch = Director.Chapter;
            GUILayout.Label(ch.Objective, ui.Paragraph);
            var next = Director.NextObjective;
            string task;
            if (next == null)
            {
                var c = Contact;
                string dist = c == null ? "" : " (" + Mathf.CeilToInt(Vector3.Distance(player.transform.position, c.transform.position)) + " m)";
                task = "Talk to " + ch.ContactName + dist + ". Press " + ui.Settings.Interact + " nearby.";
            }
            else if (next.Kind == ObjectiveKind.Drive)
            {
                task = "RIDE: " + next.Label + " · " + (drivePod.IsDriving ? Mathf.CeilToInt(drivePod.DistanceToWaypoint) + " m to the orange beacon. R: back to the road." : "press " + ui.Settings.Drive + " at the cyan e-moto");
                if (drivePod.IsDriving && (drivePod.Impacts >= 3 || ui.Settings.AutoDrive))
                    task += " · P: autopilot " + (drivePod.Autopilot ? "ON" : "OFF");
                else if (drivePod.Impacts > 0) task += " · Bumps: " + drivePod.Impacts;
            }
            else
            {
                task = (next.Kind == ObjectiveKind.Rescue ? "RESCUE: " : "OBJECTIVE: ") + next.Label + " (" + Mathf.CeilToInt(objectives.Distance) + " m). Press " + ui.Settings.Interact + " at the orange marker.";
                if (next.Kind == ObjectiveKind.Rescue && rescueTimer >= 0)
                    task += "\nTIME LEFT: " + Mathf.FloorToInt(rescueTimer / 60) + ":" + Mathf.FloorToInt(rescueTimer % 60).ToString("00") + " · Climb the ramp to the lever.";
            }
            GUILayout.Label(task, ui.Paragraph);
            if (!string.IsNullOrEmpty(notice)) GUILayout.Label(notice, ui.Small);
            GUILayout.EndArea();
        }

        private void DrawLine()
        {
            var line = Director.CurrentLine;
            if (line == null) return;
            Area(ui.Panel(190));
            GUILayout.Label(line.SpeakerName + (string.IsNullOrEmpty(line.Delivery) ? "" : " (" + line.Delivery + ")"), ui.Speaker);
            GUILayout.Label(line.Text, ui.Paragraph);
            GUILayout.FlexibleSpace();
            GUILayout.Label("Space / Enter / A: continue   " + (Director.LineIndex + 1) + " / " + Director.LineCount + "   ·   " + ui.Settings.History + ": history", ui.Small);
            GUILayout.EndArea();
        }

        private void DrawDecision()
        {
            var decision = Director.Decision;
            Area(ui.Panel(440));
            GUILayout.Label("Decision · " + decision.Title, ui.Heading);
            scroll = GUILayout.BeginScrollView(scroll);
            foreach (var option in decision.Options)
            {
                var status = Director.Status(option.Id);
                var o = option;
                GUILayout.Space(6);
                GUILayout.Label(option.Id + " · " + option.Label, ui.Speaker);
                GUILayout.Label(option.Summary, ui.Paragraph);
                GUILayout.Label("Effects: " + DescribeEffects(option), ui.Small);
                ui.Option(status.Affordable ? "Choose " + option.Label : "Cannot afford: " + status.Explanation, status.Affordable, () =>
                {
                    var result = Director.Choose(o.Id);
                    decisionMessage = result.Committed ? "" : result.Message;
                });
                if (status.CreditLineOffered)
                    ui.Option("Accept Naomi's one-time credit line (+" + rules.CreditLineAmount + " Credits, Autonomy -4)" +
                              (status.CreditsShortfallAfterCreditLine > 0 ? " · still " + status.CreditsShortfallAfterCreditLine + " short afterwards" : ""),
                              true, () => decisionMessage = Director.TakeCreditLine(o.Id).Message, 34);
            }
            GUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(decisionMessage)) GUILayout.Label(decisionMessage, ui.Small);
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
            Area(ui.Panel(300));
            GUILayout.Label("A new year in Syntera", ui.Heading);
            foreach (var part in (Director.YearSummary ?? "").Split(new[] { "; " }, StringSplitOptions.RemoveEmptyEntries))
                GUILayout.Label("• " + part, ui.Paragraph);
            GUILayout.FlexibleSpace();
            ui.Option("Continue", true, () => Director.ContinueFromYearSummary(), 42);
            GUILayout.EndArea();
        }

        private void DrawEvidence()
        {
            Area(ui.Panel(390));
            GUILayout.Label("Evidence for the council", ui.Heading);
            foreach (var slot in Director.EvidenceSlots)
                GUILayout.Label((slot.Earned ? "[PRESENTED] " : "[EMPTY] ") + slot.Title + (slot.Earned ? "" : " · earned by: " + slot.EarnedBy), ui.Paragraph);
            GUILayout.Label("Nothing can be invented. Empty slots show the choice that would have earned them.", ui.Small);
            GUILayout.FlexibleSpace();
            ui.Option("Continue to the charter proposal", true, () => Director.ContinueFromEvidence(), 42);
            GUILayout.EndArea();
        }

        private void DrawCharterChoice()
        {
            Area(ui.Panel(390));
            GUILayout.Label("Propose one charter · 5 of 9 seats pass it · no time limit", ui.Heading);
            foreach (var charter in rules.Charters)
            {
                var c = charter;
                GUILayout.Space(6);
                ui.Option(charter.Title + "\n" + charter.Summary, true, () => Director.ProposeCharter(c.Id), 58);
            }
            GUILayout.EndArea();
        }

        private void DrawVote()
        {
            var vote = Director.Vote;
            Area(ui.Panel(420));
            GUILayout.Label(vote.CharterTitle + ": " + vote.SeatsFor + " of " + vote.TotalSeats + " seats · " + (vote.Passed ? "PASSED" : "FAILED"), ui.Heading);
            foreach (var v in vote.Votes) GUILayout.Label(v.BlocName + ": " + v.Reason, ui.Paragraph);
            GUILayout.Label(vote.EndingReason, ui.Small);
            GUILayout.FlexibleSpace();
            ui.Option("Continue to tomorrow morning", true, () => Director.ContinueFromVote(), 42);
            GUILayout.EndArea();
        }

        private IList<string> Headlines(CampaignRecap recap)
        {
            var s = Director.Engine.State;
            return new List<string>
            {
                "Syntera Council " + (recap.Vote.Passed ? "Passes " : "Rejects ") + "the " + recap.Vote.CharterTitle + ": " + recap.Ending.Title,
                s.HasFlag("sharedOwnership") ? "Moto-Taxi Riders Now Co-Own Loopline" : "VANTAGE Takes Over Loopline Operations",
                "Robot Delayed a Tram to Show Us the Sunset"
            };
        }

        private void DrawRecap()
        {
            var recap = Director.Recap();
            if (recap == null) return;
            Area(ui.Panel(Screen.height));
            GUILayout.Label("SYNTERA WILL REMEMBER · " + recap.Ending.Title, ui.Heading);
            scroll = GUILayout.BeginScrollView(scroll);
            if (recap.Script != null)
            {
                GUILayout.Label(recap.Script.Outcome, ui.Paragraph);
                GUILayout.Label("Post-credits: " + recap.Script.PostCreditSting, ui.Small);
            }
            GUILayout.Label("Council: " + recap.Vote.CharterTitle + " " + recap.Vote.SeatsFor + "/" + recap.Vote.TotalSeats + ". " + recap.Vote.EndingReason, ui.Paragraph);
            foreach (var v in recap.Vote.Votes) GUILayout.Label("  " + v.BlocName + ": " + v.Reason, ui.Small);
            GUILayout.Label("Your decisions", ui.Speaker);
            foreach (var c in recap.Choices) GUILayout.Label("  " + c.Chapter + ": " + c.Choice, ui.Small);
            GUILayout.Label("City indicators", ui.Speaker);
            GUILayout.Label("  " + string.Join("   ", recap.Indicators.Select(kv => rules.StatLabel(kv.Key) + " " + kv.Value)), ui.Small);
            GUILayout.Label("Loopline ownership: " + string.Join(", ", recap.Ownership.Select(kv => kv.Key + " " + kv.Value + "%")) +
                            " · Evidence cards " + recap.EvidenceCount + (recap.UsedCreditLine ? " · Naomi's credit line used (repaid in the 2050 continuation)" : ""), ui.Small);
            GUILayout.Label("This ending is one scenario for a fictional city, not a prediction of 2050. Playtime " +
                            Mathf.FloorToInt(ElapsedSeconds / 60f) + "m " + Mathf.FloorToInt(ElapsedSeconds % 60f) + "s.", ui.Small);
            GUILayout.Label("Story card headline", ui.Speaker);
            foreach (var h in Headlines(recap)) { var hh = h; ui.Option((headline == h ? "● " : "") + h, true, () => headline = hh, 30); }
            GUILayout.EndScrollView();
            GUILayout.BeginHorizontal();
            ui.Option("Save story card", headline != null, () => StartCoroutine(CaptureCard()), 44);
            ui.Option("Play again", true, () => confirmRestart = true, 44);
            ui.Option("Continue to 2050 (prototype)", true, () => { Director.FinishRecap(); future.Open(Director.Engine.State); }, 44);
            ui.Option("Exit", true, () => { Director.FinishRecap(); Save(); Application.Quit(); }, 44);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void DrawStoryCard()
        {
            var recap = Director.Recap();
            if (recap == null) return;
            float w = Mathf.Min(Screen.width - 40, 900), h = Mathf.Min(Screen.height - 40, 500);
            var r = new Rect((Screen.width - w) / 2, (Screen.height - h) / 2, w, h);
            ui.Backdrop(r);
            GUILayout.BeginArea(new Rect(r.x + 24, r.y + 20, r.width - 48, r.height - 40));
            GUILayout.Label("SYNTERA · THE LAST HUMAN DECISION", ui.Small);
            GUILayout.Label(headline, ui.Heading);
            GUILayout.Label(recap.Ending.Title + " · " + recap.Ending.Summary, ui.Paragraph);
            foreach (var c in recap.Choices.Take(10)) GUILayout.Label(c.Chapter + ": " + c.Choice, ui.Small);
            GUILayout.Label("A fictional city. Scenarios, not certainties.", ui.Small);
            GUILayout.EndArea();
        }

        private IEnumerator CaptureCard()
        {
            capturingCard = true;
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(Application.persistentDataPath, "syntera-story-card-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
            try
            {
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Destroy(tex);
                Toast("Story card saved: " + path);
            }
            catch (Exception ex) { Toast("Could not save the story card: " + ex.Message); }
            capturingCard = false;
        }

        private void DrawConfirmRestart()
        {
            Area(ui.Panel(200));
            GUILayout.Label("Start a new 2026 run?", ui.Heading);
            GUILayout.Label("Your finished campaign is kept as a backup file. The new run starts from the beginning.", ui.Paragraph);
            GUILayout.BeginHorizontal();
            ui.Option("Yes, start again", true, Restart, 44);
            ui.Option("Cancel", true, () => confirmRestart = false, 44);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
