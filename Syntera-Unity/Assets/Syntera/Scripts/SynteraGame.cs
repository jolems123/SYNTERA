using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Syntera
{
    [Serializable]
    public class CityState
    {
        public int version = 1;
        public string playerName = "Explorer";
        public int year = 2026;
        public int chapter;
        public int credits = 650;
        public int treasury = 1500;
        public int jobs = 58;
        public int trust = 52;
        public int automation = 18;
        public int environment = 65;
        public int aiInfluence = 20;
        public int reputation = 45;
        public string district = "Nexus Central";
        public string[] decisions = new string[12];
        public bool finished;
        public int completedRouteMask;
        public string lastOutcome = "";
        public float elapsedSeconds;
        public int[] investigationStages = new int[12];
    }

    public sealed class SynteraGame : MonoBehaviour
    {
        public static SynteraGame Instance { get; private set; }
        public static bool InputLocked { get; private set; }
        public CityState State { get; private set; }
        private GameObject player;
        private Camera followCamera;
        private CharacterController controller;
        private Vector3 velocity;
        private float cameraYaw = 35f;
        private float cameraPitch = 23f;
        private bool showMap;
        private bool showHelp;
        private bool dialogueOpen;
        private SynteraCharacter missionCharacter;
        private SynteraDrivePod drivePod;
        private SynteraCinematicDirector cinematic;
        private SynteraAudioDirector audioDirector;
        private SynteraMissionObjectives investigations;
        private int dialogueIndex;
        private string responseText = "";
        private bool showCredits;
        private string notice = "Welcome to Syntera City.";
        private bool recoveredFromBackup;
        private readonly string[] districts = {"Nexus Central", "Horizon Commons", "Quantum Valley", "Nova Heights", "Helix Medical", "Forge Industrial", "Eden Gardens", "The Underground"};
        private static readonly string[] titles = {
            "The Strike That Stopped the Sky", "Company for Sale", "The Mayor Who Quit", "School of Tomorrow",
            "The Impossible Wedding", "Blackout at Nexus", "A Street for Sale", "Ghost in the Ledger",
            "Sunset Rebellion", "The Friend You Fire", "The Last Human Decision", "Tomorrow Morning"
        };
        private static readonly string[] summaries = {
            "Kato's delivery robots strike, demanding charging breaks and a company pet.",
            "VANTAGE offers to acquire your struggling company while AURA warns about control.",
            "Mayor Cross vanishes mid-speech and leaves an AI-generated resignation.",
            "Grace Kim's school must choose between a teacher and an automated classroom.",
            "Autonomous wedding drones have mistaken the entire city for the bride.",
            "A blackout forces you to choose who receives scarce energy.",
            "A developer wants to buy a community street and replace its residents with sensors.",
            "Raven Black discovers suspicious transfers hidden in the city ledger.",
            "Citizens gather outside the AI Council to demand accountability.",
            "Kato admits he signed a secret deal that weakened your company.",
            "AURA and VANTAGE ask you to decide who should govern Syntera.",
            "The city wakes to the consequences of your decision."
        };
        private static readonly string[] leftChoices = {
            "Negotiate with the robots", "Protect company independence", "Demand a public election",
            "Fund human teachers", "Rescue the wedding guests", "Power homes first",
            "Preserve neighborhood housing", "Expose the transfers", "Protect the protesters",
            "Forgive Kato with conditions", "Human oversight of AI", "Create an independent citizen council"
        };
        private static readonly string[] rightChoices = {
            "Replace the robot workforce", "Accept VANTAGE's financing", "Let an AI manage the city",
            "Automate the school", "Sell drone footage rights", "Power businesses first",
            "Approve smart redevelopment", "Sell the evidence", "Give SENTINEL emergency control",
            "Fire Kato immediately", "Transfer authority to AI", "Give corporations emergency powers"
        };
        private const int MaxChapters = 12;
        private static readonly string[] missionContacts = {"Kato Mercer", "VANTAGE", "Mayor Adrian Cross", "Grace Kim", "Kato Mercer", "AURA", "Amara Reed", "Raven Black", "Amara Reed", "Kato Mercer", "AURA", "Amara Reed"};
        private const float InteractionRadius = 5f;
        private string SavePath => Path.Combine(Application.persistentDataPath, "syntera-city-save.json");
        private GUIStyle heading, paragraph, action, small;

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
            Load();
            Application.targetFrameRate = 60;
            if (FindFirstObjectByType<Light>() == null)
            {
                GameObject sun = new GameObject("Sun");
                Light light = sun.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                sun.transform.rotation = Quaternion.Euler(52, -35, 0);
            }
            RenderSettings.ambientLight = new Color(0.57f, 0.69f, 0.79f);
            CreateWorld();
        }

        private void CreateWorld()
        {
            SynteraWorld.Build();
            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player - " + State.playerName;
            player.transform.position = new Vector3(-9.5f, 1.3f, -7.5f);
            Destroy(player.GetComponent<Collider>());
            controller = player.AddComponent<CharacterController>();
            controller.height = 2.25f;
            controller.radius = .38f;
            controller.center = Vector3.zero;
            Renderer r = player.GetComponent<Renderer>();
            r.material.color = new Color(.22f, .85f, .73f);
            SynteraCharacterAssets.Attach(player, State.playerName, true);
            player.AddComponent<SynteraPlayerPresentation>();
            followCamera = Camera.main;
            if (followCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                followCamera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }
            followCamera.fieldOfView = 65;
            followCamera.nearClipPlane = .08f;
            drivePod = SynteraDrivePod.Spawn();
            drivePod.RegisterPlayer(player, controller);
            drivePod.ConfigureDelivery(State.chapter);
            if ((State.completedRouteMask & (1 << State.chapter)) != 0)
                drivePod.RestoreCompletedRoute();
            cinematic = gameObject.AddComponent<SynteraCinematicDirector>();
            cinematic.Initialize(followCamera, player.transform);
            audioDirector = gameObject.AddComponent<SynteraAudioDirector>();
            audioDirector.Initialize();
            audioDirector.SetChapter(State.chapter);
            investigations = gameObject.AddComponent<SynteraMissionObjectives>();
            if (State.investigationStages == null || State.investigationStages.Length != 12)
                State.investigationStages = new int[12];
            investigations.Configure(Mathf.Min(State.chapter, 11),
                State.investigationStages[Mathf.Min(State.chapter, 11)], player.transform);
            if (SynteraCampaignContent.NeedsDelivery(State.chapter)) investigations.MarkComplete();
            PositionMissionContact();
        }

        private void Update()
        {
            if (!showHelp && !showMap && !State.finished)
                State.elapsedSeconds += Time.unscaledDeltaTime;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (dialogueOpen) { dialogueOpen = false; if (audioDirector != null) audioDirector.StopVoice();
                    NotifySpeaking(missionCharacter, false); }
                else if (showMap) showMap = false;
                else showHelp = !showHelp;
            }
            if (Input.GetKeyDown(KeyCode.F3) && audioDirector != null) audioDirector.ToggleMute();
            if (Input.GetKeyDown(KeyCode.M) && (cinematic == null || !cinematic.IsPlaying)) showMap = !showMap;
            if (cinematic != null && cinematic.IsPlaying) { InputLocked = true; cinematic.UpdateCamera(); return; }
            if (Input.GetKeyDown(KeyCode.F) && !InputLocked && drivePod != null) drivePod.ToggleDriver();
            if (drivePod != null && drivePod.RouteComplete && SynteraCampaignContent.NeedsDelivery(State.chapter) &&
                (State.completedRouteMask & (1 << State.chapter)) == 0)
            {
                State.completedRouteMask |= 1 << State.chapter;
                responseText = "Delivery completed. Find your mission contact.";
                PositionMissionContact(true);
                Save();
            }
            InputLocked = showMap || State.finished || showHelp || dialogueOpen || (cinematic != null && cinematic.IsPlaying);
            if (Input.GetKeyDown(KeyCode.E) && !InputLocked && (drivePod == null || !drivePod.IsDriving) && State.chapter < MaxChapters)
            {
                if (investigations != null && !SynteraCampaignContent.NeedsDelivery(State.chapter) &&
                    investigations.TryInteract())
                {
                    State.investigationStages[State.chapter] = investigations.Stage;
                    responseText = investigations.Complete ? "Evidence gathered. Return to your contact." :
                        "Evidence collected. Find the next orange marker.";
                    Save();
                }
                else
                {
                var nearby = GetMissionCharacter();
                if (nearby != null && Vector3.Distance(player.transform.position, nearby.transform.position) <= InteractionRadius)
                {
                    missionCharacter = nearby;
                    dialogueIndex = 0;
                    dialogueOpen = true;
                    if (audioDirector != null) audioDirector.PlayVoice(0);
                    NotifySpeaking(nearby, true);
                }
                }
            }
            if (dialogueOpen && missionCharacter != null)
                NotifySpeaking(missionCharacter, true,
                    audioDirector != null && audioDirector.VoicePlaying ? audioDirector.DialogueLevel : .35f);
            if (!InputLocked && (drivePod == null || !drivePod.IsDriving)) MovePlayer();
            UpdateCamera();
        }

        private static void NotifySpeaking(SynteraCharacter actor, bool speaking, float level = .35f)
        {
            if (actor == null) return;
            actor.GetComponent<SynteraActorAnimation>()?.SetSpeaking(speaking, level);
            actor.GetComponent<SynteraHumanoidMotionBridge>()?.SetTalking(speaking);
        }

        private SynteraCharacter GetMissionCharacter()
        {
            if (State.chapter >= MaxChapters) return null;
            string target = missionContacts[State.chapter];
            foreach (var actor in FindObjectsByType<SynteraCharacter>(FindObjectsSortMode.None))
                if (actor.characterName == target) return actor;
            return null;
        }

        private bool IsRouteSatisfied(int chapter) => !SynteraCampaignContent.NeedsDelivery(chapter) ||
            (State.completedRouteMask & (1 << chapter)) != 0;

        private void PositionMissionContact(bool nearVehicle = false)
        {
            var npc = GetMissionCharacter();
            if (npc == null || player == null) return;
            Vector3 target = ((nearVehicle && drivePod != null) ? drivePod.transform.position : player.transform.position) + new Vector3(3.5f, -.15f, -1.5f);
            Vector3 position = new Vector3(target.x, 1.15f, target.z);
            var animator = npc.GetComponent<SynteraActorAnimation>();
            if (animator != null) animator.SetHomePosition(position);
            else npc.transform.position = position;
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
            if (Input.GetMouseButton(1) && !InputLocked)
            {
                cameraYaw += Input.GetAxis("Mouse X") * 3f;
                cameraPitch = Mathf.Clamp(cameraPitch - Input.GetAxis("Mouse Y") * 2f, 10f, 65f);
            }
            Vector3 focus = ((drivePod != null && drivePod.IsDriving) ? drivePod.FollowTarget.position : player.transform.position) + Vector3.up * 1.4f;
            Quaternion orbit = Quaternion.Euler(cameraPitch, cameraYaw, 0);
            followCamera.transform.position = Vector3.Lerp(followCamera.transform.position, focus + orbit * new Vector3(0, 0, -9), 12f * Time.deltaTime);
            followCamera.transform.LookAt(focus);
        }

        private void InitStyles()
        {
            if (heading != null) return;
            heading = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold, wordWrap = true };
            paragraph = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            small = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            action = new GUIStyle(GUI.skin.button) { fontSize = 15, wordWrap = true };
        }

        private void OnGUI()
        {
            InitStyles();
            if (cinematic != null && cinematic.IsPlaying) return;
            float width = Mathf.Min(Screen.width - 24, 620);
            GUI.Box(new Rect(12, 12, Mathf.Min(Screen.width - 24, 470), 97), "");
            GUI.Label(new Rect(23, 18, 450, 33), "SYNTERA · " + State.year, heading);
            GUI.Label(new Rect(23, 57, 460, 44), "Credits " + State.credits + " | Trust " + State.trust + " | Jobs " + State.jobs + " | AI " + State.aiInfluence +
                " | " + Mathf.FloorToInt(State.elapsedSeconds / 60f) + "m", small);
            if (GUI.Button(new Rect(Screen.width - 120, 15, 108, 36), "MAP [M]")) showMap = !showMap;
            if (GUI.Button(new Rect(Screen.width - 120, 57, 108, 36), "HELP [Esc]")) showHelp = !showHelp;
            if (showHelp)
            {
                GUI.Box(new Rect(12, 124, width, 165), "");
                GUI.Label(new Rect(26, 136, width - 24, 35), "Syntera City Controls", heading);
                GUI.Label(new Rect(26, 174, width - 34, 110), "WASD: walk or drive · Shift: sprint\nF near cyan pod: drive/exit · Space: brake\nE: interact with mission NPC · M: map\nEsc: close dialogue or help. · F3: mute audio", paragraph);
                if (GUI.Button(new Rect(20, 293, 140, 35), "Start playing")) showHelp = false;
                return;
            }
            if (showMap)
            {
                GUI.Box(new Rect(12, 124, width, 435), "");
                GUI.Label(new Rect(26, 135, width - 25, 34), "Districts of Syntera City", heading);
                for (int i = 0; i < districts.Length; i++)
                {
                    int index = i;
                    if (GUI.Button(new Rect(25 + (i % 2) * (width / 2 - 3), 180 + (i / 2) * 58, width / 2 - 19, 48), districts[i], action))
                    {
                        State.district = districts[index];
                        player.transform.position = new Vector3((index % 4 - 1.5f) * 32, 1.3f, (index / 4 - .5f) * 34);
                        notice = "Arrived at " + State.district;
                        showMap = false;
                        Save();
                    }
                }
                GUI.Label(new Rect(26, 430, width - 35, 80), "The complete district simulation develops across the campaign. The environment is an early playable 3D prototype.", small);
                return;
            }
            float panelY = Mathf.Max(170, Screen.height - 255);
            if (State.finished)
            {
                GUI.Box(new Rect(12, panelY - 85, width, 340), "");
                GUI.Label(new Rect(26, panelY - 72, width - 32, 35), "Your Syntera ending", heading);
                GUI.Label(new Rect(26, panelY - 33, width - 40, 112), EndingText() + "\nPlaytime: " + Mathf.FloorToInt(State.elapsedSeconds / 60f) + "m " + Mathf.FloorToInt(State.elapsedSeconds % 60f) + "s", paragraph);
                if (GUI.Button(new Rect(26, panelY + 102, width / 2 - 26, 52), "Replay 2026–2028")) Restart();
                if (GUI.Button(new Rect(width / 2 + 8, panelY + 102, width / 2 - 10, 52), "Continue to 2050")) { State.finished = false; State.year = 2029; State.chapter = 12; Save(); }
                return;
            }
            if (State.chapter >= MaxChapters)
            {
                GUI.Box(new Rect(12, panelY, width, 244), "");
                GUI.Label(new Rect(26, panelY + 10, width - 30, 34), "Beyond 2028 · " + State.year, heading);
                GUI.Label(new Rect(26, panelY + 57, width - 35, 90), "Your city keeps evolving. Decide whether to fund citizens or speed up AI automation as you approach 2050.", paragraph);
                if (GUI.Button(new Rect(25, panelY + 140, width / 2 - 24, 72), "Invest in citizens", action)) AdvanceFuture(false);
                if (GUI.Button(new Rect(width / 2 + 7, panelY + 140, width / 2 - 22, 72), "Expand AI automation", action)) AdvanceFuture(true);
                return;
            }
            GUI.Box(new Rect(12, panelY, width, 244), "");
            GUI.Label(new Rect(26, panelY + 8, width - 30, 32), (State.chapter + 1) + ". " + titles[State.chapter], heading);
            var contact = GetMissionCharacter();
            float distance = contact == null ? float.PositiveInfinity : Vector3.Distance(player.transform.position, contact.transform.position);
            if (!dialogueOpen)
            {
                string routeObjective = !IsRouteSatisfied(State.chapter) ? SynteraCampaignContent.DeliveryDescription(State.chapter) + " • Orange beacon: " + (drivePod == null ? "?" : Mathf.CeilToInt(drivePod.DistanceToWaypoint).ToString()) + "m.\\n" : "";
                string investigationHint = investigations != null && !SynteraCampaignContent.NeedsDelivery(State.chapter) &&
                    !investigations.Complete ? "INVESTIGATE: " + investigations.Task +
                    " (" + Mathf.CeilToInt(investigations.Distance) + "m). Press E at orange marker.\n" : "";
                GUI.Label(new Rect(26, panelY + 50, width - 38, 76), investigationHint + routeObjective + "Objective: Talk to " + missionContacts[State.chapter] + "  (" + (float.IsInfinity(distance) ? "missing" : Mathf.CeilToInt(distance) + " m") + ")\nExplore the city. Press E when you are near your mission contact.", paragraph);
                GUI.Label(new Rect(26, panelY + 142, width - 38, 72), responseText.Length > 0 ? responseText : "Your choices will influence Syntera's economic and political future.", small);
                return;
            }
            GUI.Label(new Rect(26, panelY + 39, width - 36, 24), missionCharacter != null ? missionCharacter.characterName + ":" : "Mission contact:", small);
            string[] dialogue = SynteraCampaignContent.Dialogue[State.chapter];
            if (dialogueIndex < dialogue.Length)
            {
                GUI.Label(new Rect(26, panelY + 66, width - 36, 67), dialogue[dialogueIndex], paragraph);
                if (GUI.Button(new Rect(26, panelY + 149, width - 52, 56), "Continue dialogue (" + (dialogueIndex + 1) + "/" + dialogue.Length + ")", action))
                {
                    dialogueIndex++;
                    if (audioDirector != null)
                    {
                        if (dialogueIndex < dialogue.Length) audioDirector.PlayVoice(dialogueIndex);
                        else audioDirector.StopVoice();
                    }
                }
                return;
            }
            if (!IsRouteSatisfied(State.chapter) || (investigations != null && !SynteraCampaignContent.NeedsDelivery(State.chapter) && !investigations.Complete))
            {
                GUI.Label(new Rect(26, panelY + 146, width - 40, 70), "Complete the orange-marked investigation or driving objective before deciding. Esc to close.", paragraph);
                return;
            }
            GUI.Label(new Rect(26, panelY + 66, width - 36, 67), responseText.StartsWith("Not enough") ? responseText : summaries[State.chapter], paragraph);
            if (GUI.Button(new Rect(25, panelY + 139, width / 2 - 24, 74), leftChoices[State.chapter], action)) Choose(false);
            if (GUI.Button(new Rect(width / 2 + 7, panelY + 139, width / 2 - 22, 74), rightChoices[State.chapter], action)) Choose(true);
        }

        private void AdvanceFuture(bool automate)
        {
            if (State.year >= 2050) { State.finished = true; Save(); return; }
            State.year++;
            State.credits += automate ? 90 : 35;
            State.aiInfluence = Mathf.Clamp(State.aiInfluence + (automate ? 4 : -1), 0, 100);
            State.jobs = Mathf.Clamp(State.jobs + (automate ? -1 : 2), 0, 100);
            State.trust = Mathf.Clamp(State.trust + (automate ? -2 : 2), 0, 100);
            State.environment = Mathf.Clamp(State.environment + (automate ? -1 : 2), 0, 100);
            if (State.year >= 2050) State.finished = true;
            Save();
        }

        private void Choose(bool right)
        {
            if (State.chapter >= MaxChapters || !dialogueOpen) return;
            int i = State.chapter;
            if (dialogueIndex < SynteraCampaignContent.Dialogue[i].Length || !IsRouteSatisfied(i) ||
                (investigations != null && !SynteraCampaignContent.NeedsDelivery(i) && !investigations.Complete)) return;

            var outcome = SynteraCampaignContent.GetOutcome(i, right);
            if (State.credits + outcome.credits < 0 || State.treasury + outcome.treasury < 0)
            {
                responseText = "Not enough credits or public funds for that decision.";
                return;
            }
            dialogueOpen = false;
            dialogueIndex = 0;
            if (audioDirector != null) audioDirector.StopVoice();
            NotifySpeaking(missionCharacter, false);
            missionCharacter = null;
            State.credits += outcome.credits;
            State.treasury += outcome.treasury;
            State.jobs = Mathf.Clamp(State.jobs + outcome.jobs, 0, 100);
            State.trust = Mathf.Clamp(State.trust + outcome.trust, 0, 100);
            State.aiInfluence = Mathf.Clamp(State.aiInfluence + outcome.aiInfluence, 0, 100);
            State.environment = Mathf.Clamp(State.environment + outcome.environment, 0, 100);
            State.reputation = Mathf.Clamp(State.reputation + outcome.reputation, 0, 100);
            State.decisions[i] = right ? rightChoices[i] : leftChoices[i];
            State.lastOutcome = outcome.consequence;
            responseText = outcome.consequence;

            if (i == 11)
            {
                State.finished = true;
                State.year = 2028;
                Save();
                return;
            }
            State.chapter++;
            if (drivePod != null)
            {
                drivePod.ConfigureDelivery(State.chapter);
                if ((State.completedRouteMask & (1 << State.chapter)) != 0)
                    drivePod.RestoreCompletedRoute();
            }
            if (investigations != null)
            {
                investigations.Configure(State.chapter, State.investigationStages[State.chapter], player.transform);
                if (SynteraCampaignContent.NeedsDelivery(State.chapter)) investigations.MarkComplete();
            }
            if (audioDirector != null) audioDirector.SetChapter(State.chapter);
            PositionMissionContact();
            State.year = State.chapter < 4 ? 2026 : State.chapter < 8 ? 2027 : 2028;
            if (cinematic != null)
                cinematic.PlayAt("SYNTERA · " + State.year + "\n" + titles[Mathf.Min(State.chapter, 11)], GetMissionCharacter()?.transform, 3.6f);
            Save();
        }

        private string EndingText()
        {
            if (State.year >= 2050) return "SYNTERA 2050: Twenty-four years of decisions have shaped the city. Trust " + State.trust + ", jobs " + State.jobs + ", AI influence " + State.aiInfluence + ". The future remains open.";
            if (State.aiInfluence > 55) return "THE AUTOMATED CITY: AI governance accelerates. Prosperity rises, but who can challenge the machines? Your decisions shaped the outcome.";
            if (State.trust >= 65) return "THE COOPERATIVE CITY: Human institutions and AI agree to share power with transparent public oversight. You built trust during the crisis.";
            return "THE DIVIDED CITY: Businesses, people and AI factions remain in uneasy competition. Syntera's future is still unwritten.";
        }

        private void Restart()
        {
            int next = State.version + 1;
            State = new CityState { version = next };
            if (drivePod != null)
            {
                drivePod.ResetPod();
                drivePod.ConfigureDelivery(0);
            }
            if (audioDirector != null) { audioDirector.StopVoice(); audioDirector.SetChapter(0); }
            if (player != null)
            {
                controller.enabled = false;
                player.transform.position = new Vector3(-9.5f, 1.3f, -7.5f);
                controller.enabled = true;
            }
            if (investigations != null) investigations.Configure(0, 0, player.transform);
            PositionMissionContact();
            showHelp = false;
            showMap = false;
            dialogueOpen = false;
            dialogueIndex = 0;
            responseText = "";
            Save();
        }

        public void Save()
        {
            try
            {
                string temp = SavePath + ".tmp";
                string backup = SavePath + ".bak";
                File.WriteAllText(temp, JsonUtility.ToJson(State, true));
                if (recoveredFromBackup)
                {
                    // Preserve the last known-good backup. Never overwrite it with a corrupt primary.
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
            catch (Exception ex) { Debug.LogWarning("SYNTERA save failed: " + ex.Message); }
        }

        private void Load()
        {
            foreach (string path in new[] { SavePath, SavePath + ".bak" })
            {
                if (!File.Exists(path)) continue;
                try
                {
                    CityState parsed = JsonUtility.FromJson<CityState>(File.ReadAllText(path));
                    if (parsed == null || parsed.decisions == null || parsed.decisions.Length != 12 ||
                        parsed.chapter < 0 || parsed.chapter > 12 || parsed.year < 2026 || parsed.year > 2050 ||
                        parsed.credits < 0 || parsed.trust < 0 || parsed.trust > 100 ||
                        parsed.aiInfluence < 0 || parsed.aiInfluence > 100 || parsed.jobs < 0 || parsed.jobs > 100 ||
                        parsed.environment < 0 || parsed.environment > 100 || parsed.treasury < 0 ||
                        float.IsNaN(parsed.elapsedSeconds) || parsed.elapsedSeconds < 0)
                        continue;
                    State = parsed;
                    recoveredFromBackup = path.EndsWith(".bak");
                    if (State.investigationStages == null || State.investigationStages.Length != 12)
                        State.investigationStages = new int[12];
                    for (int n = 0; n < 12; n++)
                        State.investigationStages[n] = Mathf.Clamp(State.investigationStages[n], 0, 2);
                    responseText = parsed.lastOutcome ?? "";
                    if (path.EndsWith(".bak")) Debug.LogWarning("Recovered SYNTERA from backup save.");
                    return;
                }
                catch (Exception ex) { Debug.LogWarning("Invalid SYNTERA save: " + ex.Message); }
            }
            State = new CityState();
        }

        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnApplicationQuit() => Save();
    }
}
