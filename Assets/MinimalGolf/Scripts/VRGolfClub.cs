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

        [Header("Proximity Sphere Visual")]
        [Tooltip("Visible sphere radius at controller tip.")]
        public float sphereRadius = 0.08f;
        [Range(0f, 1f)]
        [Tooltip("Opacity of the proximity sphere (0 = invisible, 1 = opaque).")]
        public float sphereOpacity = 0.35f;
        public Color sphereColor = new Color(0.3f, 0.9f, 0.6f, 1f);
        public bool showSphere = true;

        private bool overlappingBall;
        private bool aiming;
        private bool wasTriggerHeld;
        private Vector3 aimStartWorld;
        private Rigidbody ballRigidbody;

        private SphereCollider tipCollider;
        private GameObject sphereVisual;
        private Renderer sphereRenderer;
        private Material sphereMaterialInstance;

        private void Awake()
        {
            tipCollider = GetComponent<SphereCollider>();
            tipCollider.isTrigger = true;
            tipCollider.radius = 0.06f;
            if (game == null) game = FindFirstObjectByType<MinimalGolfGame>();
            EnsureSphereVisual();
            UpdateSphereVisual();
        }

        private void OnEnable()
        {
            EnsureSphereVisual();
            UpdateSphereVisual();
        }

        private void OnValidate()
        {
            sphereRadius = Mathf.Max(0.01f, sphereRadius);
            sphereOpacity = Mathf.Clamp01(sphereOpacity);
            overlapRadius = Mathf.Max(0.02f, overlapRadius);
            triggerThreshold = Mathf.Clamp01(triggerThreshold);
            // Tip collider radius is fixed physical trigger; visual sphere is independent
            if (tipCollider == null) tipCollider = GetComponent<SphereCollider>();
            if (tipCollider != null)
            {
                tipCollider.isTrigger = true;
            }
            EnsureSphereVisual();
            UpdateSphereVisual();
        }

        private void EnsureSphereVisual()
        {
            if (!showSphere)
            {
                if (sphereVisual != null)
                    sphereVisual.SetActive(false);
                return;
            }

            if (sphereRenderer != null && sphereVisual != null)
            {
                sphereVisual.SetActive(true);
                return;
            }

            Transform existing = transform.Find("ProximitySphere");
            if (existing != null)
            {
                sphereVisual = existing.gameObject;
                sphereRenderer = sphereVisual.GetComponent<Renderer>();
                if (sphereRenderer != null)
                    sphereMaterialInstance = sphereRenderer.sharedMaterial;
                sphereVisual.SetActive(true);
                if (sphereRenderer == null || sphereMaterialInstance == null)
                    CreateSpherePrimitive(existing);
                return;
            }

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ProximitySphere";
            // Remove collider - we keep only the trigger on the parent
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(col);
                else Destroy(col);
#else
                Destroy(col);
#endif
            }
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create ProximitySphere");
#endif
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            sphereVisual = go;
            sphereRenderer = go.GetComponent<Renderer>();
            CreateSphereMaterial();
        }

        private void CreateSpherePrimitive(Transform existing)
        {
            sphereRenderer = existing.GetComponent<Renderer>();
            if (sphereRenderer == null)
            {
                var mf = existing.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                {
                    // Recreate primitive properly
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(existing.gameObject);
                    else Destroy(existing.gameObject);
#else
                    Destroy(existing.gameObject);
#endif
                    EnsureSphereVisual();
                    return;
                }
            }
            CreateSphereMaterial();
        }

        private void CreateSphereMaterial()
        {
            if (sphereRenderer == null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            Material mat = null;
            if (shader != null)
                mat = new Material(shader);
            else
                mat = new Material(Shader.Find("Hidden/Internal-Colored"));

            mat.name = "VRGolfClubSphereMat_" + controller.ToString();
            // Configure for transparency
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", sphereColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", sphereColor);
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", null);

            // URP Unlit transparent setup
            if (shader != null && shader.name.Contains("Universal Render Pipeline"))
            {
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else if (shader != null && shader.name == "Standard")
            {
                mat.SetFloat("_Mode", 3f); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            // Ensure vertex color not needed
            Color c = sphereColor;
            c.a *= Mathf.Clamp01(sphereOpacity);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);

            sphereMaterialInstance = mat;
            sphereRenderer.sharedMaterial = sphereMaterialInstance;
        }

        private void UpdateSphereVisual()
        {
            if (sphereVisual == null || sphereRenderer == null) return;
            sphereVisual.SetActive(showSphere);
            if (!showSphere) return;

            sphereVisual.transform.localPosition = Vector3.zero;
            float diameter = Mathf.Max(0.02f, sphereRadius) * 2f;
            sphereVisual.transform.localScale = new Vector3(diameter, diameter, diameter);

            if (sphereMaterialInstance == null)
            {
                sphereMaterialInstance = sphereRenderer.sharedMaterial;
                if (sphereMaterialInstance == null) CreateSphereMaterial();
            }
            if (sphereMaterialInstance != null)
            {
                Color c = sphereColor;
                c.a *= Mathf.Clamp01(sphereOpacity);
                if (sphereMaterialInstance.HasProperty("_BaseColor")) sphereMaterialInstance.SetColor("_BaseColor", c);
                if (sphereMaterialInstance.HasProperty("_Color")) sphereMaterialInstance.SetColor("_Color", c);
                // Also support _UnlitColor for some pipelines
                if (sphereMaterialInstance.HasProperty("_UnlitColor")) sphereMaterialInstance.SetColor("_UnlitColor", c);
                sphereRenderer.sharedMaterial = sphereMaterialInstance;
            }
        }

        private void Update()
        {
            if (game == null || game.CurrentLevel == null || game.CurrentLevel.ball == null)
                return;

            ballRigidbody = game.CurrentLevel.ball;

            // Determine overlap via distance check (more reliable than trigger when ball moves fast)
            float dist = Vector3.Distance(transform.position, ballRigidbody.worldCenterOfMass);
            overlappingBall = dist < overlapRadius + 0.08f;

            // Trigger state: support both digital Button and analog Axis1D threshold with edge detection
            // This fixes the case where GetDown/GetUp miss due to analog-only squeeze or stub timing.
            float triggerValue = 0f;
            try { triggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller); } catch { triggerValue = 0f; }
            bool digitalHeld = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller);
            bool analogHeld = triggerValue > triggerThreshold;
            bool triggerHeld = (digitalHeld || analogHeld) || IsMouseTriggerHeldForEditor();
            // Edge detection: newly pressed / newly released (handles analog squeeze that never hits digital threshold)
            bool heldDownEdge = triggerHeld && !wasTriggerHeld;
            bool heldUpEdge = !triggerHeld && wasTriggerHeld;
            // Also honor discrete OVR events for haptics timing, but edge is the reliable gate
            bool digitalDown = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controller);
            bool digitalUp = OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, controller);
            bool triggerDown = (heldDownEdge || digitalDown) || IsMouseTriggerDownForEditor();
            bool triggerUp = (heldUpEdge || digitalUp) || IsMouseTriggerUpForEditor();

            // Keep proximity sphere visual in sync (handles inspector tweaks at runtime)
            UpdateSphereVisual();

            // --- Press: only START aiming, never shoot ---
            // Use edge so analog squeeze that never hits digital threshold still starts aiming
            if (!aiming && overlappingBall && triggerDown && triggerHeld)
            {
                // BeginVRAim internally gates on CanTakeAction and shows feedback
                bool started = game.BeginVRAim(ProjectToBallPlane(transform.position));
                if (started && game.IsAiming)
                {
                    aiming = true;
                    aimStartWorld = ProjectToBallPlane(transform.position);
                    OVRInput.SetControllerVibration(0.3f, 0.5f, controller);
                }
                else
                {
                    // Ensure we never enter aiming if Begin failed
                    aiming = false;
                }
            }

            // --- Hold: update pull vector, no impulse ---
            if (aiming && triggerHeld)
            {
                Vector3 cur = ProjectToBallPlane(transform.position);
                game.UpdateVRAim(cur);
                // subtle haptics based on power
                float power = game.ShotPower;
                if (power > 0.05f)
                    OVRInput.SetControllerVibration(0.1f, power * 0.3f, controller);
            }

            // --- Release: the ONLY place that can apply impulse ---
            if (aiming && triggerUp)
            {
                bool shot = game.TryEndVRAimAndShoot();
                aiming = false;
                if (shot)
                    OVRInput.SetControllerVibration(0.6f, 0.8f, controller);
                else
                    OVRInput.SetControllerVibration(0.2f, 0.3f, controller);
            }

            if (aiming && game.CurrentLevel.IsRevealing)
            {
                aiming = false;
                game.CancelAim();
            }

            // Store held state for edge detection next frame (must be after all use of wasTriggerHeld)
            wasTriggerHeld = triggerHeld;
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

        private void OnDrawGizmos()
        {
            if (!showSphere)
            {
                Gizmos.color = overlappingBall ? new Color(0f, 1f, 0f, 0.25f) : new Color(1f, 0.92f, 0.016f, 0.25f);
                Gizmos.DrawWireSphere(transform.position, sphereRadius);
            }
        }

        private void OnDrawGizmosSelected()
        {
            // Gameplay overlap radius (yellow/green) + visual sphere radius (cyan)
            Gizmos.color = overlappingBall ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, overlapRadius);
            if (showSphere)
            {
                Color c = sphereColor;
                c.a = Mathf.Clamp01(sphereOpacity) * 0.9f;
                Gizmos.color = c;
                Gizmos.DrawWireSphere(transform.position, sphereRadius);
                // Solid transparent preview
                Gizmos.color = new Color(c.r, c.g, c.b, c.a * 0.18f);
                Gizmos.DrawSphere(transform.position, sphereRadius);
            }
        }
    }
}
