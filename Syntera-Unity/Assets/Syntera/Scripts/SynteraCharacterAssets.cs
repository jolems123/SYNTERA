using UnityEngine;

namespace Syntera
{
    // Licensed FBX/glTF-derived prefabs are expected under Assets/Resources/Characters/.
    // Prefer an Avatar with Humanoid mapping and one Animator per imported character.
    // Missing prefabs use the procedural actor until production assets are supplied.
    public static class SynteraCharacterAssets
    {
        public static bool Attach(GameObject host, string character, bool player)
        {
            string key = character.Replace(' ', '_');
            GameObject prefab = Resources.Load<GameObject>("Characters/" + key);
            if (prefab == null)
                prefab = Resources.Load<GameObject>("Characters/" + (player ? "Player" : "NPC_Default"));
            if (prefab == null) return false;
            GameObject model = Object.Instantiate(prefab, host.transform, false);
            model.name = "Licensed rigged visual - " + character;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            foreach (var collider in model.GetComponentsInChildren<Collider>())
                collider.enabled = false; // Host CharacterController / NPC capsule owns collision.
            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman || animator.runtimeAnimatorController == null)
            {
                Debug.LogWarning("SYNTERA character '" + character + "' has no verified Humanoid Avatar; using fallback.");
                Object.Destroy(model);
                return false;
            }
            Renderer primitive = host.GetComponent<Renderer>();
            if (primitive != null) primitive.enabled = false;
            var bridge = host.AddComponent<SynteraHumanoidMotionBridge>();
            bridge.Bind(animator);
            return true;
        }
    }

    public sealed class SynteraHumanoidMotionBridge : MonoBehaviour
    {
        private Animator animator;
        private Vector3 lastPosition;
        private bool speedParameter;
        private bool talkingParameter;

        public void Bind(Animator humanoid)
        {
            animator = humanoid;
            foreach (var parameter in animator.parameters)
            {
                if (parameter.name == "Speed" && parameter.type == AnimatorControllerParameterType.Float)
                    speedParameter = true;
                if (parameter.name == "Talking" && parameter.type == AnimatorControllerParameterType.Bool)
                    talkingParameter = true;
            }
            lastPosition = transform.position;
        }

        public void SetTalking(bool state)
        {
            if (animator != null && talkingParameter) animator.SetBool("Talking", state);
        }

        private void Update()
        {
            if (animator == null) return;
            float speed = Vector3.Distance(transform.position, lastPosition) / Mathf.Max(Time.deltaTime, .01f);
            lastPosition = transform.position;
            if (speedParameter) animator.SetFloat("Speed", speed, .12f, Time.deltaTime);
        }
    }
}
