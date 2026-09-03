using UnityEngine;

namespace MinimalGolf
{
    /// <summary>
    /// Cross-instance aim ownership: unified first-input-wins across both clubs and
    /// both modalities (controller trigger + either-hand pinch). Whoever starts the
    /// aim owns it until release or cancel; all other inputs are ignored meanwhile.
    /// </summary>
    public static class GolfAimLock
    {
        private static object s_owner;

        public static bool TryAcquire(object owner)
        {
            if (owner == null) return false;
            if (s_owner != null && !ReferenceEquals(s_owner, owner)) return false;
            s_owner = owner;
            return true;
        }

        public static void Release(object owner)
        {
            if (owner != null && ReferenceEquals(s_owner, owner)) s_owner = null;
        }

        public static bool IsHeldByOther(object owner)
        {
            return s_owner != null && !ReferenceEquals(s_owner, owner);
        }
    }

    /// <summary>
    /// Rising/falling edge evaluation for a held signal (pinch, trigger).
    /// </summary>
    public static class GolfInputEdges
    {
        public static void Evaluate(bool held, ref bool wasHeld, out bool down, out bool up)
        {
            down = held && !wasHeld;
            up = !held && wasHeld;
            wasHeld = held;
        }
    }

    /// <summary>
    /// Proximity-sphere visibility rule: the sphere belongs to the controller, so it hides
    /// once the hand has been solidly tracked for <paramref name="delaySeconds"/> (debounced
    /// against tracking flicker) and reappears the moment the hand is gone.
    /// </summary>
    public static class GolfSphereVisibility
    {
        public static bool ShouldHide(bool hideEnabled, float continuousTrackedTime, float delaySeconds)
        {
            if (!hideEnabled) return false;
            if (delaySeconds <= 0f) return continuousTrackedTime > 0f;
            return continuousTrackedTime >= delaySeconds;
        }
    }

    /// <summary>
    /// Hand-pose helpers shared by pinch aiming and fist reveal. Pure functions of explicit
    /// inputs so they stay testable without a headset.
    /// </summary>
    public static class GolfHandPoses
    {
        /// <summary>
        /// PIP-joint flexion angle in degrees: 0 when the finger is straight, ~90+ in a
        /// tight fist. Pure function of three joint positions (proximal, joint, distal).
        /// </summary>
        public static float JointAngle(Vector3 proximal, Vector3 joint, Vector3 distal)
        {
            Vector3 a = joint - proximal;
            Vector3 b = distal - joint;
            if (a.sqrMagnitude < 1e-10f || b.sqrMagnitude < 1e-10f) return 0f;
            return Vector3.Angle(a, b);
        }

        /// <summary>
        /// Fist: all four fingers curled past the threshold. A precision index pinch
        /// (other fingers extended) correctly reads as not-a-fist.
        /// </summary>
        public static bool IsFistCurl(float indexDeg, float middleDeg, float ringDeg, float pinkyDeg, float thresholdDeg)
        {
            return indexDeg >= thresholdDeg
                && middleDeg >= thresholdDeg
                && ringDeg >= thresholdDeg
                && pinkyDeg >= thresholdDeg;
        }

        /// <summary>
        /// Palm-center estimate: average of the given joint positions (wrist + knuckles).
        /// </summary>
        public static bool TryAverageCenter(System.Collections.Generic.IList<Vector3> points, out Vector3 center)
        {
            center = default;
            if (points == null || points.Count == 0) return false;
            Vector3 sum = default;
            for (int i = 0; i < points.Count; i++) sum += points[i];
            center = sum / points.Count;
            return true;
        }
    }

    /// <summary>
    /// VR club-in-ball interaction. Place controller tip inside ball trigger volume,
    /// hold PrimaryIndexTrigger (or same-side hand index pinch) to start pull,
    /// drag to set power/direction, release to shoot.
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

        [Header("Hand Tracking")]
        [Tooltip("Allow the same-side tracked hand to drive aim/shoot via index pinch, mirroring the trigger.")]
        public bool enableHandInput = true;
        [Tooltip("Automatically hide the proximity sphere while the same-side hand is tracked (it sits on the controller anchor, so it would float in the wrong place). Reappears as soon as controllers take over.")]
        public bool hideSphereWhenHandTracked = true;
        [Tooltip("How long the hand must be continuously tracked before the sphere hides. Prevents flicker on tracking dropouts; reappear is immediate.")]
        public float handHideDelay = 0.25f;

        [Header("Hand Reveal")]
        [Tooltip("A fist with the tracked same-side hand activates the reveal volume at the palm, mirroring controller grip-reveal.")]
        public bool enableHandReveal = true;
        [Range(10f, 120f)]
        [Tooltip("PIP-joint flexion in degrees required on ALL four fingers to count as a fist. Lower = looser fists accepted.")]
        public float fistCurlThreshold = 50f;

        [Header("Hand Visuals")]
        [Tooltip("Near-white toon look applied to the same-side hand mesh at runtime (loaded from Resources/ToonHand).")]
        public bool applyHandToonMaterial = true;
        public Color handBaseColor = new Color(0.957f, 0.961f, 0.969f);
        public Color handShadeColor = new Color(0.788f, 0.804f, 0.831f);
        public Color handOutlineColor = new Color(0.137f, 0.149f, 0.169f);
        [Range(0f, 0.01f)]
        public float handOutlineWidth = 0.0025f;

        private bool overlappingBall;
        private bool aiming;
        private bool aimingWithHand;
        private bool wasTriggerHeld;
        private bool wasPinchHeld;
        private float handContinuouslyTrackedTime;
        private bool hideSphereForHand;
        private bool wasFistDebug;
        private float handDebugTimer;
        private Vector3 aimStartWorld;
        private Rigidbody ballRigidbody;
        private OVRHand resolvedHand;
        private bool handToonApplied;

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
            EnsureRevealVolume();
        }

        private void OnEnable()
        {
            EnsureSphereVisual();
            UpdateSphereVisual();
            EnsureRevealVolume();
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
            EnsureRevealVolume();
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
                // Ensure ProximitySphere has trigger collider (no physical collision)
                var existingCol = sphereVisual.GetComponent<Collider>();
                if (existingCol == null)
                {
                    var sc = sphereVisual.AddComponent<SphereCollider>();
                    sc.isTrigger = true;
                    sc.radius = 0.5f;
                }
                else
                {
                    existingCol.isTrigger = true;
                    if (existingCol is SphereCollider esc) esc.radius = 0.5f;
                }
                if (sphereRenderer == null || sphereMaterialInstance == null)
                    CreateSpherePrimitive(existing);
                else
                    UpdateSphereVisual(); // ensure scale matches sphereRadius
                return;
            }

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ProximitySphere";
            // Keep trigger on ProximitySphere itself — detect only, no physics collision
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                col.isTrigger = true;
                if (col is SphereCollider sc) sc.radius = 0.5f; // unit sphere, scaled via transform
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
            bool visible = showSphere && !hideSphereForHand;
            sphereVisual.SetActive(visible);
            if (!visible) return;

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

            // One-time toon look for the same-side hand mesh.
            ApplyHandToonMaterial(ResolveHand());

            // Hand pinch state (same-side hand, fail-safe: ignored unless tracked with a valid pointer pose)
            bool pinchHeld = SamplePinchHold(out Vector3 pinchPoint, out bool pinchValid);
            GolfInputEdges.Evaluate(pinchHeld, ref wasPinchHeld, out bool pinchDown, out bool pinchUp);

            // Sphere follows the active modality: hide once the hand is solidly tracked
            // (debounced), show again the instant it is gone (controllers take over).
            // Uses live tracking (not pointer-pose validity) so the sphere hides as soon
            // as the hand appears, even before the first pinch.
            bool handLive = HandIsLive();
            if (handLive)
                handContinuouslyTrackedTime += Time.deltaTime;
            else
                handContinuouslyTrackedTime = 0f;
            hideSphereForHand = GolfSphereVisibility.ShouldHide(
                hideSphereWhenHandTracked, handContinuouslyTrackedTime, handHideDelay);

            // Transition-only fist diagnostics: proves fist detection against live data
            // and reports the raw strengths for threshold tuning. Fires at most on change.
            bool fistNow = IsHandFist();
            if (fistNow != wasFistDebug)
            {
                wasFistDebug = fistNow;
                Debug.Log($"[VRGolfClub] {controller} fist={fistNow} strengths={DescribeHandStrengths()}");
            }
            // TEMPORARY sim-tuning probe (remove once the fist threshold is validated):
            // 1 Hz raw strengths while the hand is live, regardless of fist state.
            handDebugTimer += Time.deltaTime;
            if (handDebugTimer >= 1f)
            {
                handDebugTimer = 0f;
                string strengths = DescribeHandStrengths();
                if (strengths != "untracked")
                    Debug.Log($"[VRGolfClub] {controller} probe strengths={strengths} fist={fistNow}");
            }

            // Active tip: live pinch point while pinching, otherwise the controller tip.
            // When hands are absent this is exactly transform.position, preserving trigger behavior.
            Vector3 activeTip = (pinchHeld && pinchValid) ? pinchPoint : transform.position;

            // Determine overlap via distance check (more reliable than trigger when ball moves fast)
            float dist = Vector3.Distance(activeTip, ballRigidbody.worldCenterOfMass);
            overlappingBall = dist < overlapRadius + 0.08f;

            // Trigger state: support both digital Button and analog Axis1D threshold with edge detection
            // This fixes the case where GetDown/GetUp miss due to analog-only squeeze or stub timing.
            float triggerValue = 0f;
            try { triggerValue = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, controller); } catch { triggerValue = 0f; }
            bool digitalHeld = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller);
            bool analogHeld = triggerValue > triggerThreshold;
            bool rawTriggerHeld = (digitalHeld || analogHeld) || IsMouseTriggerHeldForEditor();
            // Unified input: trigger OR same-side pinch drive one combined signal (first-input-wins
            // ownership across instances is enforced by GolfAimLock at aim start).
            bool triggerHeld = rawTriggerHeld || pinchHeld;
            // Edge detection: newly pressed / newly released (handles analog squeeze that never hits digital threshold)
            bool heldDownEdge = triggerHeld && !wasTriggerHeld;
            bool heldUpEdge = !triggerHeld && wasTriggerHeld;
            // Also honor discrete OVR events for haptics timing, but edge is the reliable gate
            bool digitalDown = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controller);
            bool digitalUp = OVRInput.GetUp(OVRInput.Button.PrimaryIndexTrigger, controller);
            bool triggerDown = (heldDownEdge || digitalDown || pinchDown) || IsMouseTriggerDownForEditor();
            bool triggerUp = (heldUpEdge || digitalUp || pinchUp) || IsMouseTriggerUpForEditor();

            // Keep proximity sphere visual in sync (handles inspector tweaks at runtime)
            UpdateSphereVisual();

            // --- Press: only START aiming, never shoot ---
            // Use edge so analog squeeze that never hits digital threshold still starts aiming
            if (!aiming && overlappingBall && triggerDown && triggerHeld)
            {
                // Unified first-input-wins: another club/modality may already own the aim.
                if (!GolfAimLock.IsHeldByOther(this) && GolfAimLock.TryAcquire(this))
                {
                    // BeginVRAim internally gates on CanTakeAction and shows feedback
                    bool started = game.BeginVRAim(ProjectToBallPlane(activeTip));
                    if (started && game.IsAiming)
                    {
                        aiming = true;
                        aimingWithHand = pinchHeld && pinchValid;
                        aimStartWorld = ProjectToBallPlane(activeTip);
                        OVRInput.SetControllerVibration(0.3f, 0.5f, controller);
                    }
                    else
                    {
                        // Ensure we never enter aiming if Begin failed
                        GolfAimLock.Release(this);
                        aiming = false;
                    }
                }
            }

            // --- Hold: update pull vector, no impulse ---
            if (aiming && triggerHeld)
            {
                Vector3 cur = ProjectToBallPlane(activeTip);
                game.UpdateVRAim(cur);
                // subtle haptics based on power (controllers only; hands use visual/audio feedback)
                float power = game.ShotPower;
                if (power > 0.05f)
                    OVRInput.SetControllerVibration(0.1f, power * 0.3f, controller);
            }

            // --- Release: the ONLY place that can apply impulse ---
            if (aiming && triggerUp)
            {
                bool shot = game.TryEndVRAimAndShoot();
                aiming = false;
                aimingWithHand = false;
                GolfAimLock.Release(this);
                if (shot)
                    OVRInput.SetControllerVibration(0.6f, 0.8f, controller);
                else
                    OVRInput.SetControllerVibration(0.2f, 0.3f, controller);
            }

            if (aiming && game.CurrentLevel.IsRevealing)
            {
                aiming = false;
                aimingWithHand = false;
                GolfAimLock.Release(this);
                game.CancelAim();
            }

            // A pinch-driven aim requires a live hand: cancel on tracking loss
            // rather than risk a stuck aim on a stale pinch point.
            if (aiming && aimingWithHand && !pinchValid)
            {
                aiming = false;
                aimingWithHand = false;
                GolfAimLock.Release(this);
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

        private void OnDisable()
        {
            // Never leak aim ownership: a disabled club must not block the other club/hand.
            if (aiming && game != null)
            {
                try { if (game.IsAiming) game.CancelAim(); } catch { /* best effort */ }
            }
            aiming = false;
            aimingWithHand = false;
            GolfAimLock.Release(this);
        }

        /// <summary>
        /// Samples the same-side hand's index pinch. Returns the raw pinch state; <paramref name="valid"/>
        /// is true only when the hand is tracked with a usable pointer pose. Fail-safe: any doubt
        /// yields (false, invalid) so untracked hands can never start or steer an aim.
        /// </summary>
        private bool SamplePinchHold(out Vector3 pinchPoint, out bool valid)
        {
            pinchPoint = default;
            valid = false;
            if (!enableHandInput) return false;
            try
            {
                OVRHand hand = ResolveHand();
                if (hand == null || !hand.IsTracked || !hand.IsDataValid || !hand.IsPointerPoseValid)
                    return false;
                pinchPoint = hand.PointerPose.position;
                valid = true;
                return hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
            }
            catch { return false; }
        }

        /// <summary>
        /// True while the same-side hand makes a fist (all four PIP joints flexed past
        /// threshold). Curl-based rather than thumb-pinch-based: pinch strengths read zero
        /// on runtimes that only stream skeleton poses. Fail-safe: any doubt yields false.
        /// </summary>
        public bool IsHandFist()
        {
            if (!enableHandReveal) return false;
            try
            {
                if (!TryGetFingerCurls(out Vector4 curls)) return false;
                return GolfHandPoses.IsFistCurl(curls.x, curls.y, curls.z, curls.w, fistCurlThreshold);
            }
            catch { return false; }
        }

        /// <summary>
        /// PIP flexion per finger in degrees (x=index, y=middle, z=ring, w=pinky), from live
        /// skeleton joints in either the legacy OVR or OpenXR bone family. False unless the
        /// hand is tracked with a complete joint triple for every finger.
        /// </summary>
        public bool TryGetFingerCurls(out Vector4 curls)
        {
            curls = default;
            try
            {
                OVRHand hand = ResolveHand();
                if (hand == null || !hand.IsTracked || !hand.IsDataValid) return false;
                var skeleton = hand.GetComponent<OVRSkeleton>();
                if (skeleton == null || skeleton.Bones == null) return false;
                // One family per skeleton — never mix: the two families reuse numeric IDs,
                // so a blended triple measures phantom joints (e.g. a 165-degree middle
                // finger on an open hand).
                var skeletonType = skeleton.GetSkeletonType();
                bool xr = skeletonType == OVRSkeleton.SkeletonType.XRHandLeft
                    || skeletonType == OVRSkeleton.SkeletonType.XRHandRight;
                var pos = new System.Collections.Generic.Dictionary<int, Vector3>(32);
                foreach (var bone in skeleton.Bones)
                {
                    if (bone == null || bone.Transform == null) continue;
                    pos[(int)bone.Id] = bone.Transform.position;
                }
                if (!TryFingerCurl(pos, xr,
                        OVRSkeleton.BoneId.Hand_Index1, OVRSkeleton.BoneId.Hand_Index2, OVRSkeleton.BoneId.Hand_Index3,
                        OVRSkeleton.BoneId.XRHand_IndexProximal, OVRSkeleton.BoneId.XRHand_IndexIntermediate, OVRSkeleton.BoneId.XRHand_IndexDistal,
                        out float index)) return false;
                if (!TryFingerCurl(pos, xr,
                        OVRSkeleton.BoneId.Hand_Middle1, OVRSkeleton.BoneId.Hand_Middle2, OVRSkeleton.BoneId.Hand_Middle3,
                        OVRSkeleton.BoneId.XRHand_MiddleProximal, OVRSkeleton.BoneId.XRHand_MiddleIntermediate, OVRSkeleton.BoneId.XRHand_MiddleDistal,
                        out float middle)) return false;
                if (!TryFingerCurl(pos, xr,
                        OVRSkeleton.BoneId.Hand_Ring1, OVRSkeleton.BoneId.Hand_Ring2, OVRSkeleton.BoneId.Hand_Ring3,
                        OVRSkeleton.BoneId.XRHand_RingProximal, OVRSkeleton.BoneId.XRHand_RingIntermediate, OVRSkeleton.BoneId.XRHand_RingDistal,
                        out float ring)) return false;
                if (!TryFingerCurl(pos, xr,
                        OVRSkeleton.BoneId.Hand_Pinky1, OVRSkeleton.BoneId.Hand_Pinky2, OVRSkeleton.BoneId.Hand_Pinky3,
                        OVRSkeleton.BoneId.XRHand_LittleProximal, OVRSkeleton.BoneId.XRHand_LittleIntermediate, OVRSkeleton.BoneId.XRHand_LittleDistal,
                        out float pinky)) return false;
                curls = new Vector4(index, middle, ring, pinky);
                return true;
            }
            catch { return false; }
        }

        private static bool TryFingerCurl(
            System.Collections.Generic.Dictionary<int, Vector3> pos, bool xrFamily,
            OVRSkeleton.BoneId l0, OVRSkeleton.BoneId l1, OVRSkeleton.BoneId l2,
            OVRSkeleton.BoneId x0, OVRSkeleton.BoneId x1, OVRSkeleton.BoneId x2,
            out float degrees)
        {
            degrees = 0f;
            OVRSkeleton.BoneId b0 = xrFamily ? x0 : l0;
            OVRSkeleton.BoneId b1 = xrFamily ? x1 : l1;
            OVRSkeleton.BoneId b2 = xrFamily ? x2 : l2;
            if (pos.TryGetValue((int)b0, out Vector3 a)
                && pos.TryGetValue((int)b1, out Vector3 b)
                && pos.TryGetValue((int)b2, out Vector3 c))
            {
                degrees = GolfHandPoses.JointAngle(a, b, c);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Raw hand state for diagnostics ("untracked" when no live hand): PIP curls in
        /// degrees plus the runtime index-pinch bit (drives aim) — shows which signals a
        /// given runtime actually streams.
        /// </summary>
        private string DescribeHandStrengths()
        {
            try
            {
                OVRHand hand = ResolveHand();
                if (hand == null || !hand.IsTracked || !hand.IsDataValid) return "untracked";
                string curls = TryGetFingerCurls(out Vector4 c)
                    ? $"curl i={c.x:0}m={c.y:0}r={c.z:0}p={c.w:0}"
                    : "curl n/a";
                bool idxPinch = false;
                try { idxPinch = hand.GetFingerIsPinching(OVRHand.HandFinger.Index); } catch { }
                return $"{curls} idxPinch={idxPinch} thr={fistCurlThreshold:0}";
            }
            catch { return "error"; }
        }

        /// <summary>
        /// Palm-center estimate for the same-side hand (wrist + knuckle average, pointer-pose
        /// fallback). Fail-safe: false unless the hand is tracked with usable data.
        /// </summary>
        public bool TryGetHandCenter(out Vector3 center)
        {
            center = default;
            if (!enableHandReveal) return false;
            try
            {
                OVRHand hand = ResolveHand();
                if (hand == null || !hand.IsTracked || !hand.IsDataValid) return false;
                var skeleton = hand.GetComponent<OVRSkeleton>();
                if (skeleton != null && skeleton.Bones != null)
                {
                    var skeletonType = skeleton.GetSkeletonType();
                    bool xr = skeletonType == OVRSkeleton.SkeletonType.XRHandLeft
                        || skeletonType == OVRSkeleton.SkeletonType.XRHandRight;
                    var points = new System.Collections.Generic.List<Vector3>(6);
                    foreach (var bone in skeleton.Bones)
                    {
                        if (bone == null || bone.Transform == null) continue;
                        if (IsPalmLandmark(bone.Id, xr)) points.Add(bone.Transform.position);
                    }
                    if (GolfHandPoses.TryAverageCenter(points, out center)) return true;
                }
                if (hand.IsPointerPoseValid)
                {
                    center = hand.PointerPose.position;
                    return true;
                }
                return false;
            }
            catch { return false; }
        }

        /// <summary>
        /// Wrist + knuckle landmarks whose average estimates the palm center, from exactly
        /// one skeleton family (the two families reuse numeric IDs, so they must never mix).
        /// </summary>
        private static bool IsPalmLandmark(OVRSkeleton.BoneId id, bool xrFamily)
        {
            int v = (int)id;
            if (xrFamily)
            {
                return v == (int)OVRSkeleton.BoneId.XRHand_Palm
                    || v == (int)OVRSkeleton.BoneId.XRHand_Wrist
                    || v == (int)OVRSkeleton.BoneId.XRHand_IndexMetacarpal
                    || v == (int)OVRSkeleton.BoneId.XRHand_MiddleMetacarpal
                    || v == (int)OVRSkeleton.BoneId.XRHand_RingMetacarpal
                    || v == (int)OVRSkeleton.BoneId.XRHand_LittleMetacarpal;
            }
            return v == (int)OVRSkeleton.BoneId.Hand_WristRoot
                || v == (int)OVRSkeleton.BoneId.Hand_Index1
                || v == (int)OVRSkeleton.BoneId.Hand_Middle1
                || v == (int)OVRSkeleton.BoneId.Hand_Ring1
                || v == (int)OVRSkeleton.BoneId.Hand_Pinky1;
        }

        /// <summary>
        /// True while the same-side hand is tracked with usable data (pointer pose not required).
        /// </summary>
        private bool HandIsLive()
        {
            if (!enableHandInput) return false;
            try
            {
                OVRHand hand = ResolveHand();
                return hand != null && hand.IsTracked && hand.IsDataValid;
            }
            catch { return false; }
        }

        /// <summary>
        /// Applies the near-white toon material to the hand mesh once, via the SDK's own
        /// material slot so gesture swaps keep working. Fail-safe: silent no-op.
        /// </summary>
        private void ApplyHandToonMaterial(OVRHand hand)
        {
            if (!applyHandToonMaterial || handToonApplied || hand == null) return;
            handToonApplied = true;
            try
            {
                Shader shader = Resources.Load<Shader>("ToonHand");
                if (shader == null) return;
                var material = new Material(shader);
                material.hideFlags = HideFlags.DontSave;
                material.SetColor("_BaseColor", handBaseColor);
                material.SetColor("_ShadeColor", handShadeColor);
                material.SetColor("_OutlineColor", handOutlineColor);
                material.SetFloat("_OutlineWidth", handOutlineWidth);
                var meshRenderer = hand.GetComponent<OVRMeshRenderer>();
                if (meshRenderer != null) meshRenderer.SetMaterial(material);
                else
                {
                    var skinned = hand.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (skinned != null) skinned.sharedMaterial = material;
                }
            }
            catch { }
        }

        private OVRHand ResolveHand()
        {
            if (resolvedHand != null) return resolvedHand;
            try
            {
                bool wantRight = controller == OVRInput.Controller.RTouch;
                var hands = FindObjectsByType<OVRHand>(FindObjectsSortMode.None);
                foreach (var hand in hands)
                {
                    var skeleton = hand.GetComponent<OVRSkeleton>();
                    var type = skeleton != null
                        ? skeleton.GetSkeletonType()
                        : OVRSkeleton.SkeletonType.None;
                    // Accept both legacy OVR and OpenXR skeleton variants; OVRHand
                    // self-reconciles the skeleton type from its HandType at startup.
                    bool isRight = type == OVRSkeleton.SkeletonType.HandRight
                        || type == OVRSkeleton.SkeletonType.XRHandRight;
                    bool isLeft = type == OVRSkeleton.SkeletonType.HandLeft
                        || type == OVRSkeleton.SkeletonType.XRHandLeft;
                    if ((wantRight && isRight) || (!wantRight && isLeft))
                    {
                        resolvedHand = hand;
                        break;
                    }
                }
            }
            catch { resolvedHand = null; }
            return resolvedHand;
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

        private void EnsureRevealVolume()
        {
            // The RevealVolume lives on the club root, NOT under ProximitySphere: the sphere
            // may be hidden (it is, by design) while reveal must stay live for both controller
            // grip and hand fist. Existing sphere-parented volumes are migrated up.
            var existing = GetComponentInChildren<ProximityRevealVolume>(true);
            if (existing != null)
            {
                if (existing.transform.parent != transform)
                {
                    existing.transform.SetParent(transform, false);
                    existing.transform.localPosition = Vector3.zero;
                    existing.transform.localRotation = Quaternion.identity;
                    existing.transform.localScale = Vector3.one;
                }
                return;
            }
            var go = new GameObject("RevealVolume");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var vol = go.AddComponent<ProximityRevealVolume>();
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create RevealVolume");
#endif
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
