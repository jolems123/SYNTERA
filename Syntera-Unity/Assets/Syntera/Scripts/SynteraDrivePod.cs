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
        public float DistanceToWaypoint => destination == null ? 0 : Vector3.Distance(transform.position, routeTarget);
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
        private const float SuspensionHeight = 1.05f;
        private const float SuspensionForce = 18000f;
        private const float SuspensionDamping = 2000f;
        private readonly Vector3[] wheelOffsets = {
            new Vector3(-.8f,0f,1.15f), new Vector3(.8f,0f,1.15f),
            new Vector3(-.8f,0f,-1.15f), new Vector3(.8f,0f,-1.15f)
        };
        public float SpeedKph => body == null ? 0 : body.velocity.magnitude * 3.6f;

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
            rigidbody.linearDamping = .15f;
            rigidbody.angularDamping = 2.2f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.centerOfMass = new Vector3(0f,-.25f,0f);
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
            routeTarget = chapter == 4 ? new Vector3(32, 0.5f, 0) : new Vector3(-32, 0.5f, 32);
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

        public void RestoreCompletedRoute()
        {
            RouteComplete = true;
            if (destination != null) Destroy(destination);
            destination = null;
        }

        public void ResetPod()
        {
            ForceExit();
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = new Vector3(-8f, .9f, -11f);
            body.rotation = Quaternion.identity;
            transform.position = body.position;
            transform.rotation = body.rotation;
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

        // Four wheel-ground probes supply spring/damper forces. Decorative wheels can
        // be replaced later with rigged suspension meshes without changing the drive API.
        private int ApplySuspension()
        {
            int onGround = 0;
            foreach (Vector3 offset in wheelOffsets)
            {
                Vector3 position = transform.TransformPoint(offset);
                Vector3 rayOrigin = position + Vector3.up * .12f;
                var hits = Physics.RaycastAll(rayOrigin, Vector3.down, SuspensionHeight + .75f,
                                               ~0, QueryTriggerInteraction.Ignore);
                float shortest = float.MaxValue;
                foreach (var hit in hits)
                {
                    if (hit.rigidbody == body) continue;
                    if (hit.distance < shortest) shortest = hit.distance;
                }
                if (shortest == float.MaxValue) continue;
                onGround++;
                float distance = Mathf.Max(0f, shortest - .12f);
                float compression = Mathf.Clamp(SuspensionHeight - distance, 0, SuspensionHeight);
                float verticalSpeed = Vector3.Dot(body.GetPointVelocity(position), Vector3.up);
                float push = Mathf.Clamp(compression * SuspensionForce -
                                        verticalSpeed * SuspensionDamping, 0f, 16000f);
                body.AddForceAtPosition(Vector3.up * push, position, ForceMode.Force);
            }
            return onGround;
        }

        private void FixedUpdate()
        {
            int groundedWheels = ApplySuspension();
            if (!IsDriving || SynteraGame.InputLocked) { throttle = 0; steering = 0; brake = true; }
            Vector3 local = transform.InverseTransformDirection(body.velocity);
            float forward = local.z;
            float targetSpeed = throttle * 15f;
            float force = Mathf.Clamp(targetSpeed-forward, -7f, 7f) * body.mass * 1.25f;
            if (groundedWheels > 0)
            {
                body.AddForce(transform.forward * force, ForceMode.Force);
                // Lateral tyre friction suppresses sliding without falsely cancelling forward speed.
                body.AddForce(-transform.right * local.x * (brake ? 10f : 6f), ForceMode.Acceleration);
            }
            if (brake) body.AddForce(-body.velocity * 7f, ForceMode.Acceleration);
            if (groundedWheels > 0 && Mathf.Abs(forward) > .25f)
            {
                float turn = steering * Mathf.Sign(forward) * 75f * Time.fixedDeltaTime *
                             Mathf.Clamp01(Mathf.Abs(forward) / 3f);
                body.MoveRotation(body.rotation * Quaternion.Euler(0,turn,0));
            }
            // Stabilized arcade handling; collisions remain physical, not teleported.
            if (IsDriving && destination != null && !RouteComplete &&
                Vector3.Distance(transform.position, routeTarget) < 5.5f)
            {
                RouteComplete = true;
                Destroy(destination);
                destination = null;
            }
        }
    }
}
