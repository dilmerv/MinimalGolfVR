using UnityEngine;

namespace MinimalGolf
{
    /// <summary>
    /// VR club-in-ball interaction. Place controller tip inside ball trigger volume,
    /// hold PrimaryIndexTrigger to start pull, drag to set power/direction, release to shoot.
    /// Mirrors the flat-screen drag logic (pull = start - current, clamped to maxDragDistance).
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class VRGolfClub : MonoBehaviour
    {
        public OVRInput.Controller controller = OVRInput.Controller.RTouch;
        public MinimalGolfGame game;
        [Tooltip("Tip collider radius. Small sphere at controller forward.")]
        public float overlapRadius = 0.12f;
        public float triggerThreshold = 0.45f;

        private bool overlappingBall;
        private bool aiming;
        private Vector3 aimStartWorld;
        private Rigidbody ballRigidbody;

        private SphereCollider tipCollider;

        private void Awake()
        {
            tipCollider = GetComponent<SphereCollider>();
            tipCollider.isTrigger = true;
            tipCollider.radius = 0.06f;
            if (game == null) game = FindFirstObjectByType<MinimalGolfGame>();
        }

        private void Update()
        {
            if (game == null || game.CurrentLevel == null || game.CurrentLevel.ball == null)
                return;

            ballRigidbody = game.CurrentLevel.ball;

            // Determine overlap via distance check (more reliable than trigger when ball moves fast)
            float dist = Vector3.Distance(transform.position, ballRigidbody.worldCenterOfMass);
            overlappingBall = dist < overlapRadius + 0.08f;

            bool triggerHeld = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller) || IsMouseTriggerHeldForEditor();
            bool triggerDown = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controller) || IsMouseTriggerDownForEditor();
            bool triggerUp = OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, controller) || IsMouseTriggerUpForEditor();

            // Update prev for OVRInput mock
            // Note: OVRInput stub needs external UpdatePrev call? We'll just rely on mouse fallback for editor

            if (!aiming && overlappingBall && triggerDown)
            {
                if (!game.CanTakeAction())
                {
                    // Will show WAIT FOR THE BALL inside BeginVRAim
                    game.BeginVRAim(ProjectToBallPlane(transform.position));
                    // Immediately cancel if not allowed to keep UI consistent
                    if (!game.IsAiming)
                        return;
                    // If Begin failed, still consider aiming false
                    if (game.IsAiming)
                    {
                        aiming = true;
                        aimStartWorld = ProjectToBallPlane(transform.position);
                        OVRInput.SetControllerVibration(0.3f, 0.5f, controller);
                    }
                }
                else
                {
                    aiming = game.BeginVRAim(ProjectToBallPlane(transform.position));
                    if (aiming)
                    {
                        aimStartWorld = ProjectToBallPlane(transform.position);
                        OVRInput.SetControllerVibration(0.3f, 0.5f, controller);
                    }
                }
            }

            if (aiming && triggerHeld)
            {
                Vector3 cur = ProjectToBallPlane(transform.position);
                game.UpdateVRAim(cur);
                // subtle haptics based on power
                float power = game.ShotPower;
                if (power > 0.05f)
                    OVRInput.SetControllerVibration(0.1f, power * 0.3f, controller);
            }

            if (aiming && triggerUp)
            {
                bool shot = game.TryEndVRAimAndShoot();
                aiming = false;
                if (shot)
                    OVRInput.SetControllerVibration(0.6f, 0.8f, controller);
                else
                    OVRInput.SetControllerVibration(0.2f, 0.3f, controller);
            }

            // If we drift away or ball becomes not playable while aiming, cancel
            if (aiming && !triggerHeld && !triggerUp)
            {
                // Edge case: trigger released without GetUp detection in stub
                if (!overlappingBall && game.ShotPower < 0.01f)
                {
                    // keep aiming until explicit release
                }
            }

            if (aiming && game.CurrentLevel.IsRevealing)
            {
                aiming = false;
                game.CancelAim();
            }
        }

        private Vector3 ProjectToBallPlane(Vector3 world)
        {
            if (game.CurrentLevel != null && game.CurrentLevel.ball != null)
                world.y = game.CurrentLevel.ball.position.y;
            return world;
        }

        // Editor fallbacks so tests and mouse play in editor without headset
#if UNITY_EDITOR
        private bool IsMouseTriggerHeldForEditor()
        {
            if (controller != OVRInput.Controller.RTouch) return false;
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null) return mouse.leftButton.isPressed;
#endif
            try { return Input.GetMouseButton(0); } catch { return false; }
        }
        private bool IsMouseTriggerDownForEditor()
        {
            if (controller != OVRInput.Controller.RTouch) return false;
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null) return mouse.leftButton.wasPressedThisFrame && overlappingBall;
#endif
            try { return Input.GetMouseButtonDown(0) && overlappingBall; } catch { return false; }
        }
        private bool IsMouseTriggerUpForEditor()
        {
            if (controller != OVRInput.Controller.RTouch) return false;
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null) return mouse.leftButton.wasReleasedThisFrame;
#endif
            try { return Input.GetMouseButtonUp(0); } catch { return false; }
        }
#else
        private bool IsMouseTriggerHeldForEditor() => false;
        private bool IsMouseTriggerDownForEditor() => false;
        private bool IsMouseTriggerUpForEditor() => false;
#endif

        private void OnTriggerEnter(Collider other)
        {
            if (other.attachedRigidbody == ballRigidbody) overlappingBall = true;
        }
        private void OnTriggerStay(Collider other)
        {
            if (other.attachedRigidbody == ballRigidbody) overlappingBall = true;
        }
        private void OnTriggerExit(Collider other)
        {
            if (other.attachedRigidbody == ballRigidbody) overlappingBall = false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = overlappingBall ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, overlapRadius);
        }
    }
}
