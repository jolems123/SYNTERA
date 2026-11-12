using UnityEngine;

namespace Syntera
{
    // Lightweight skippable camera beat with captions. No external video or audio assets.
    public sealed class SynteraCinematicDirector : MonoBehaviour
    {
        public bool IsPlaying { get; private set; }
        // Accessibility: static shot instead of moving camera.
        public static bool ReduceMotion { get; set; }
        private Camera sceneCamera;
        private Transform player;
        private Transform sceneFocus;
        private string caption = "";
        private float remaining;
        private float duration;
        private GUIStyle captionStyle;

        public void Initialize(Camera camera, Transform playerTransform)
        {
            sceneCamera = camera;
            player = playerTransform;
            Play("SYNTERA CITY · 2026\nThe future is already running late.", 4.5f);
        }

        public void Play(string text, float seconds = 2.8f)
        {
            caption = text;
            sceneFocus = player;
            duration = Mathf.Max(0.5f, seconds);
            remaining = duration;
            IsPlaying = true;
        }

        public void PlayAt(string text, Transform subject, float seconds = 4f)
        {
            Play(text, seconds);
            sceneFocus = subject != null ? subject : player;
        }

        private void Finish()
        {
            IsPlaying = false;
            if (sceneCamera != null) sceneCamera.fieldOfView = 65f;
        }

        private void Update()
        {
            if (!IsPlaying) return;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.JoystickButton0))
            {
                Finish();
                return;
            }
            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0) Finish();
        }

        public void UpdateCamera()
        {
            if (!IsPlaying || sceneCamera == null || player == null) return;
            if (ReduceMotion)
            {
                Transform t = sceneFocus != null ? sceneFocus : player;
                sceneCamera.fieldOfView = 60f;
                sceneCamera.transform.position = t.position + new Vector3(0, 6, -12);
                sceneCamera.transform.LookAt(t.position + Vector3.up * 1.4f);
                return;
            }
            float progress = 1f - Mathf.Clamp01(remaining / duration);
            Transform target = sceneFocus != null ? sceneFocus : player;
            Vector3 focus = target.position + Vector3.up * 1.45f;
            // Three distinct shots: establish the city, close in on the speaker,
            // and reveal the player and surrounding civic space.
            float yaw;
            float pitch;
            float distance;
            float fieldOfView;
            if (progress < .34f)
            {
                float u = Mathf.SmoothStep(0f, 1f, progress / .34f);
                yaw = Mathf.Lerp(18f, 30f, u);
                pitch = 25f;
                distance = Mathf.Lerp(19f, 14f, u);
                fieldOfView = 62f;
            }
            else if (progress < .72f)
            {
                float u = Mathf.SmoothStep(0f, 1f, (progress - .34f) / .38f);
                yaw = Mathf.Lerp(42f, 52f, u);
                pitch = 11f;
                distance = Mathf.Lerp(7.5f, 5.8f, u);
                fieldOfView = 43f;
            }
            else
            {
                float u = Mathf.SmoothStep(0f, 1f, (progress - .72f) / .28f);
                yaw = Mathf.Lerp(52f, 78f, u);
                pitch = Mathf.Lerp(14f, 22f, u);
                distance = Mathf.Lerp(11f, 15f, u);
                fieldOfView = 58f;
            }
            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0);
            sceneCamera.fieldOfView = fieldOfView;
            sceneCamera.transform.position = focus + orbit * new Vector3(0, 0, -distance);
            sceneCamera.transform.LookAt(focus);
        }

        private void OnGUI()
        {
            if (!IsPlaying) return;
            if (captionStyle == null)
            {
                captionStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.Clamp(Screen.height / 38, 16, 32),
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true,
                    fontStyle = FontStyle.Bold
                };
                captionStyle.normal.textColor = Color.white;
            }
            GUI.Box(new Rect(0, 0, Screen.width, 46), "");
            GUI.Box(new Rect(0, Screen.height-116, Screen.width, 116), "");
            GUI.Label(new Rect(24, Screen.height-109, Screen.width-48, 76), caption, captionStyle);
            GUI.Label(new Rect(24, Screen.height-35, Screen.width-48, 25), "SPACE / ENTER · Skip", GUI.skin.label);
        }
    }
}
