using Syntera.Core;
using UnityEngine;

namespace Syntera
{
    // Shows one world marker for the director's next "interact" objective. The player
    // must physically reach it and press E. Objective content lives in the dialogue data.
    public sealed class SynteraMissionObjectives : MonoBehaviour
    {
        public const float InteractRange = 3.25f;
        private GameObject marker;
        private string objectiveId;
        private Transform player;

        public ObjectiveDef Current { get; private set; }

        public float Distance => marker == null || player == null ? 0f :
            Vector3.Distance(player.position, marker.transform.position);

        public void Show(ObjectiveDef objective, Transform playerTransform, Vector3 anchor, int sequence, Vector3? exact = null)
        {
            player = playerTransform;
            string id = objective != null && objective.Kind != ObjectiveKind.Drive ? objective.Id : null;
            if (id == objectiveId) return;
            Clear();
            objectiveId = id;
            Current = id == null ? null : objective;
            if (id == null || player == null) return;

            // Place on open ground at the chapter location's road crossing, alternating sides per step.
            float roadX = Mathf.Round(anchor.x / 32f) * 32f;
            float roadZ = Mathf.Round(anchor.z / 32f) * 32f;
            bool even = sequence % 2 == 0;
            marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Objective: " + objective.Label;
            marker.transform.position = exact ?? new Vector3(roadX + (even ? 0f : 5f), 1.25f, roadZ + (even ? -5f : 0f));
            marker.transform.localScale = new Vector3(.85f, 1.25f, .85f);
            marker.GetComponent<Collider>().isTrigger = true;
            var material = SynteraMaterials.Make(new Color(1f, .74f, .16f));
            marker.GetComponent<Renderer>().material = material;
        }

        public bool InRange => marker != null && player != null && Distance <= InteractRange;

        public void Clear()
        {
            if (marker != null) Destroy(marker);
            marker = null;
            objectiveId = null;
            Current = null;
        }
    }
}
