using UnityEngine;

namespace Syntera
{
    // Lightweight skippable camera beat with captions. No external video or audio assets.
    public sealed class SynteraCinematicDirector : MonoBehaviour
    {
        public bool IsPlaying { get; private set; }
        private Camera sceneCamera;
        private Transform player;
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
            duration = Mathf.Max(0.5f, seconds);
            remaining = duration;
            IsPlaying = true;
        }

        private void Update()
        {
            if (!IsPlaying) return;
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                IsPlaying = false;
                return;
            }
            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0) IsPlaying = false;
        }

        public void UpdateCamera()
        {
            if (!IsPlaying || sceneCamera == null || player == null) return;
            float progress = 1f - Mathf.Clamp01(remaining / duration);
            float orbit = progress * 30f + 30f;
            Vector3 focus = player.position + Vector3.up * 1.3f;
            Quaternion rotation = Quaternion.Euler(18,orbit,0);
            sceneCamera.transform.position = focus + rotation * new Vector3(0, 1.2f, -14);
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
