using UnityEngine;

namespace Syntera
{
    // Two searchable scene objectives per chapter (non-driving chapters).
    // Each marker requires physically approaching it and pressing E.
    public sealed class SynteraMissionObjectives : MonoBehaviour
    {
        private static readonly string[,] Tasks =
        {
            {"Inspect striking dispatch robots", "Retrieve the fleet's charging log"},
            {"Review the independence contract", "Collect evidence of VANTAGE ownership"},
            {"Inspect the forged resignation terminal", "Collect signatures for an election"},
            {"Visit the school resource terminal", "Review teacher and AI cost projections"},
            {"Secure wedding guests", "Deliver emergency supplies"},
            {"Survey the hospital power grid", "Inspect the neighborhood substation"},
            {"Record residents' housing concerns", "Inspect the redevelopment site"},
            {"Trace the missing ledger transfer", "Authenticate the archive evidence"},
            {"Check the public assembly route", "Verify the emergency surveillance orders"},
            {"Inspect Kato's contract", "Speak with affected delivery workers"},
            {"Review the proposed AI charter", "Check public oversight safeguards"},
            {"Visit the neighborhood council", "Inspect the city's new decision record"}
        };
        private GameObject marker;
        private int chapter = -1;
        private int stage;
        private Transform player;
        private Vector3 anchor;

        public bool Complete => stage >= 2;
        public int Stage => stage;
        public float Distance => marker == null || player == null ? 0f :
            Vector3.Distance(player.position, marker.transform.position);
        public string Task => chapter < 0 || Complete ? "Investigation complete" : Tasks[chapter, stage];

        public void Configure(int chapterNumber, int savedStage, Transform playerTransform)
        {
            if (marker != null) Destroy(marker);
            marker = null;
            chapter = Mathf.Clamp(chapterNumber, 0, 11);
            stage = Mathf.Clamp(savedStage, 0, 2);
            player = playerTransform;
            anchor = player == null ? Vector3.zero : player.position;
            if (!Complete) ShowMarker();
        }

        private void ShowMarker()
        {
            if (player == null) return;
            // Place investigation points on walkable open ground near the player.
            Vector3 offset = stage == 0 ? new Vector3(-7, 0, -6) : new Vector3(8, 0, -9);
            Vector3 position = anchor + offset;
            position.y = 1.25f;
            marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Mission investigation: " + Tasks[chapter, stage];
            marker.transform.position = position;
            marker.transform.localScale = new Vector3(.85f, 1.25f, .85f);
            var collider = marker.GetComponent<Collider>();
            collider.isTrigger = true;
            var renderer = marker.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Standard"));
            material.color = new Color(1f, .74f, .16f);
            renderer.material = material;
        }

        public bool TryInteract()
        {
            if (Complete || marker == null || player == null || Distance > 3.25f) return false;
            stage++;
            Destroy(marker);
            marker = null;
            if (!Complete) ShowMarker();
            return true;
        }

        public void MarkComplete()
        {
            stage = 2;
            if (marker != null) Destroy(marker);
            marker = null;
        }
    }
}
