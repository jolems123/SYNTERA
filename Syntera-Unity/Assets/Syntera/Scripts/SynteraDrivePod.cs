using UnityEngine;

namespace Syntera
{
    // Offline, player-controlled city pod. Physical forces and Rigidbody collisions are
    // intentionally modest: it is an arcade handling foundation, not certified wheel physics.
    public sealed class SynteraDrivePod : MonoBehaviour
    {
        public bool IsDriving { get; private set; }
        public bool RouteComplete { get; private set; }
        public Transform FollowTarget => transform;
        public string Status => IsDriving ? "DRIVING · WASD steer / accelerate · Space brake · F exit" : "Press F near the cyan pod to drive";
        private Rigidbody body;
        private GameObject player;
        private CharacterController playerController;
        private Renderer[] playerRenderers;
        private int routeChapter = -1;
        private Vector3 routeTarget;
        private GameObject destination;
        private float throttle;
        private float steering;
        private bool brake;

        public static SynteraDrivePod Spawn()
        {
            GameObject pod = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pod.name = "Playable rescue and delivery pod";
            pod.transform.position = new Vector3(-8, 0.9f, -11);
            pod.transform.localScale = new Vector3(2.5f, 1.35f, 3.8f);
            var material = new Material(Shader.Find("Standard"));
            material.color = new Color(.07f,.79f,.94f);
            pod.GetComponent<Renderer>().material = material;
            var rigidbody = pod.AddComponent<Rigidbody>();
            rigidbody.mass = 850;
            rigidbody.drag = .65f;
            rigidbody.angularDrag = 2f;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            return pod.AddComponent<SynteraDrivePod>();
        }

        public void RegisterPlayer(GameObject objectPlayer, CharacterController character)
        {
            player = objectPlayer;
            playerController = character;
            playerRenderers = player.GetComponentsInChildren<Renderer>();
        }

        public void ConfigureDelivery(int chapter)
        {
            if (routeChapter == chapter) return;
            routeChapter = chapter;
            RouteComplete = false;
            if (destination != null) Destroy(destination);
            if (!SynteraCampaignContent.NeedsDelivery(chapter)) return;
            routeTarget = chapter == 4 ? new Vector3(32, 0.5f, 0) : new Vector3(-43, 0.5f, 23);
            destination = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            destination.name = "Mission route finish";
            destination.transform.position = routeTarget;
            destination.transform.localScale = new Vector3(2, 2, 2);
            var r = destination.GetComponent<Renderer>();
            r.material.color = new Color(1f,.77f,.18f);
            var c = destination.GetComponent<Collider>();
            c.isTrigger = true;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
        }

        public bool NearPod(float distance = 5f) =>
            player != null && Vector3.Distance(player.transform.position, transform.position) <= distance;

        public void ToggleDriver()
        {
            if (player == null) return;
            if (!IsDriving && !NearPod()) return;
            if (IsDriving)
            {
                if (body.velocity.magnitude > 2f) return;
                Vector3 exit = transform.position + transform.right * 3.1f;
                if (Physics.CheckCapsule(exit + Vector3.up*.25f, exit + Vector3.up*1.5f, .42f, ~0, QueryTriggerInteraction.Ignore))
                    return;
                playerController.enabled = false;
                player.transform.position = exit + Vector3.up * 1.2f;
                playerController.enabled = true;
            }
            IsDriving = !IsDriving;
            playerController.enabled = !IsDriving;
            foreach (var r in playerRenderers) r.enabled = !IsDriving;
        }

        public void ForceExit()
        {
            if (!IsDriving || player == null) return;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            IsDriving = false;
            playerController.enabled = false;
            player.transform.position = transform.position + transform.right * 4f + Vector3.up;
            playerController.enabled = true;
            foreach (var renderer in playerRenderers) renderer.enabled = true;
        }

        private void Update()
        {
            if (!IsDriving) return;
            throttle = Input.GetAxisRaw("Vertical");
            steering = Input.GetAxisRaw("Horizontal");
            brake = Input.GetKey(KeyCode.Space);
        }

        private void FixedUpdate()
        {
            if (!IsDriving || SynteraGame.InputLocked) { throttle = 0; steering = 0; brake = true; }
            Vector3 local = transform.InverseTransformDirection(body.velocity);
            float forward = local.z;
            float targetSpeed = throttle * 14f;
            float force = Mathf.Clamp(targetSpeed-forward, -8f, 8f) * body.mass * 1.5f;
            body.AddForce(transform.forward * force, ForceMode.Force);
            body.AddForce(-body.velocity * (brake ? 7f : .15f), ForceMode.Acceleration);
            if (Mathf.Abs(forward) > .25f)
            {
                float turn = steering * Mathf.Sign(forward) * 75f * Time.fixedDeltaTime *
                             Mathf.Clamp01(Mathf.Abs(forward) / 3f);
                body.MoveRotation(body.rotation * Quaternion.Euler(0,turn,0));
            }
            // Basic anti-flip assistance, not a substitute for wheel/suspension physics.
            if (transform.position.y < .5f) body.MovePosition(new Vector3(transform.position.x,.7f,transform.position.z));
            if (destination != null && !RouteComplete &&
                Vector3.Distance(transform.position, routeTarget) < 5.5f)
            {
                RouteComplete = true;
                Destroy(destination);
                destination = null;
            }
        }
    }
}
