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
        private bool showCredits;
        private string notice = "Welcome to Syntera City.";
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
            "Forgive Kato with conditions", "Human oversight of AI", "Start another run"
        };
        private static readonly string[] rightChoices = {
            "Replace the robot workforce", "Accept VANTAGE's financing", "Let an AI manage the city",
            "Automate the school", "Sell drone footage rights", "Power businesses first",
            "Approve smart redevelopment", "Sell the evidence", "Give SENTINEL emergency control",
            "Fire Kato immediately", "Transfer authority to AI", "Continue toward 2050"
        };
        private const int MaxChapters = 12;
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
            player.transform.position = new Vector3(0, 1.3f, 0);
            Destroy(player.GetComponent<Collider>());
            controller = player.AddComponent<CharacterController>();
            controller.height = 2.25f;
            controller.radius = .38f;
            controller.center = Vector3.zero;
            Renderer r = player.GetComponent<Renderer>();
            r.material.color = new Color(.22f, .85f, .73f);
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
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape)) showHelp = !showHelp;
            if (Input.GetKeyDown(KeyCode.M)) showMap = !showMap;
            InputLocked = showMap || State.finished || showHelp;
            if (!InputLocked) MovePlayer();
            UpdateCamera();
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
            Vector3 focus = player.transform.position + Vector3.up * 1.4f;
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
            float width = Mathf.Min(Screen.width - 24, 620);
            GUI.Box(new Rect(12, 12, Mathf.Min(Screen.width - 24, 470), 97), "");
            GUI.Label(new Rect(23, 18, 450, 33), "SYNTERA · " + State.year, heading);
            GUI.Label(new Rect(23, 57, 460, 44), "Credits " + State.credits + "  |  Trust " + State.trust + "  |  Jobs " + State.jobs + "  |  AI " + State.aiInfluence, small);
            if (GUI.Button(new Rect(Screen.width - 120, 15, 108, 36), "MAP [M]")) showMap = !showMap;
            if (GUI.Button(new Rect(Screen.width - 120, 57, 108, 36), "HELP [Esc]")) showHelp = !showHelp;
            if (showHelp)
            {
                GUI.Box(new Rect(12, 124, width, 165), "");
                GUI.Label(new Rect(26, 136, width - 24, 35), "Syntera City Controls", heading);
                GUI.Label(new Rect(26, 174, width - 34, 110), "WASD or arrows: walk     Shift: sprint\nRight mouse drag: orbit camera\nM: district overview     Esc: close help\nResolve each city crisis to progress to 2028.", paragraph);
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
                GUI.Label(new Rect(26, panelY - 33, width - 40, 112), EndingText(), paragraph);
                if (GUI.Button(new Rect(26, panelY + 102, width / 2 - 26, 52), "Replay 2026–2028")) Restart();
                if (GUI.Button(new Rect(width / 2 + 8, panelY + 102, width / 2 - 10, 52), "Continue to 2050")) { State.finished = false; State.year = 2029; Save(); }
                return;
            }
            GUI.Box(new Rect(12, panelY, width, 244), "");
            GUI.Label(new Rect(26, panelY + 8, width - 30, 32), (State.chapter + 1) + ". " + titles[Mathf.Min(State.chapter, MaxChapters - 1)], heading);
            GUI.Label(new Rect(26, panelY + 45, width - 36, 90), State.chapter < MaxChapters ? summaries[State.chapter] : notice, paragraph);
            if (GUI.Button(new Rect(25, panelY + 139, width / 2 - 24, 74), leftChoices[Mathf.Min(State.chapter, 11)], action)) Choose(false);
            if (GUI.Button(new Rect(width / 2 + 7, panelY + 139, width / 2 - 22, 74), rightChoices[Mathf.Min(State.chapter, 11)], action)) Choose(true);
        }

        private void Choose(bool right)
        {
            if (State.chapter >= MaxChapters) return;
            int i = State.chapter;
            if (i == 11)
            {
                if (right) { State.aiInfluence += 10; State.trust -= 8; }
                else { State.trust += 6; State.aiInfluence -= 5; }
                State.decisions[i] = right ? rightChoices[i] : leftChoices[i];
                State.finished = true;
                State.year = 2028;
                Save();
                return;
            }
            if (right)
            {
                State.credits += 60;
                State.aiInfluence = Mathf.Clamp(State.aiInfluence + 5, 0, 100);
                State.jobs = Mathf.Clamp(State.jobs - 2, 0, 100);
                State.trust = Mathf.Clamp(State.trust - 3, 0, 100);
            }
            else
            {
                if (State.credits >= 25) State.credits -= 25;
                State.trust = Mathf.Clamp(State.trust + 4, 0, 100);
                State.jobs = Mathf.Clamp(State.jobs + 2, 0, 100);
                State.aiInfluence = Mathf.Clamp(State.aiInfluence - 1, 0, 100);
            }
            State.environment = Mathf.Clamp(State.environment + (right ? -2 : 2), 0, 100);
            State.decisions[i] = right ? rightChoices[i] : leftChoices[i];
            State.chapter++;
            State.year = State.chapter < 4 ? 2026 : State.chapter < 8 ? 2027 : 2028;
            notice = "Your decision changed the city.";
            Save();
        }

        private string EndingText()
        {
            if (State.aiInfluence > 55) return "THE AUTOMATED CITY: AI governance accelerates. Prosperity rises, but who can challenge the machines? Your decisions shaped the outcome.";
            if (State.trust >= 65) return "THE COOPERATIVE CITY: Human institutions and AI agree to share power with transparent public oversight. You built trust during the crisis.";
            return "THE DIVIDED CITY: Businesses, people and AI factions remain in uneasy competition. Syntera's future is still unwritten.";
        }

        private void Restart()
        {
            int next = State.version + 1;
            State = new CityState { version = next };
            Save();
            showHelp = false;
        }

        public void Save()
        {
            try
            {
                string temp = SavePath + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(State, true));
                if (File.Exists(SavePath)) File.Delete(SavePath);
                File.Move(temp, SavePath);
            }
            catch (Exception ex) { Debug.LogWarning("SYNTERA save failed: " + ex.Message); }
        }

        private void Load()
        {
            try
            {
                if (File.Exists(SavePath))
                {
                    var parsed = JsonUtility.FromJson<CityState>(File.ReadAllText(SavePath));
                    if (parsed != null && parsed.decisions != null && parsed.decisions.Length == 12 && parsed.chapter >= 0 && parsed.chapter < 12)
                    {
                        State = parsed;
                        return;
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning("Save recovery: " + ex.Message); }
            State = new CityState();
        }

        private void OnApplicationQuit() => Save();
    }
}
