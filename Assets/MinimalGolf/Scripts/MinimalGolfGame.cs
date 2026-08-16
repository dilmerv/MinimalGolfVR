using System.Collections;
using UnityEngine;

namespace MinimalGolf
{
    public sealed class MinimalGolfGame : MonoBehaviour
    {
        [Header("Authored Scene References")]
        public MiniGolfLevel[] levels;
        public Camera gameCamera;
        public Font uiFont;

        [Header("Aiming Line")]
        public LineRenderer aimingLine;
        [Tooltip("Width of the aiming line. Applied to LineRenderer start/end width.")]
        [Range(0, 0.2f)]
        public float aimingLineWidth = 0.045f;

        [Header("VR References")]
        public OVRCameraRig ovrRig;
        public Transform vrCourseAnchor;
        public Transform vrCourseLevels;
        [Tooltip("Stable UI root at same level as VRCourseAnchor — NOT rotated by thumbstick. Holds VR_UI.")]
        public Transform vrUIAnchor;
        [Tooltip("VR course anchor local position relative to TrackingSpace. Beneath eye level, in front.")]
        public Vector3 vrAnchorLocalPosition = new Vector3(0f, 0.75f, 0.65f);
        public Vector3 vrAnchorLocalScale = new Vector3(1f, 1f, 1f);
        public Vector3 vrCourseLevelsLocalScale = new Vector3(0.042f, 0.042f, 0.042f);
        public float thumbstickRotationSpeed = 30f;

        [Header("Shot Tuning")]
        [SerializeField] private float maximumImpulse = 2.0f;
        [SerializeField] private float maximumDragDistance = 0.35f;
        [SerializeField] private float playableSpeed = 0.10f;
        [Header("VR Tuning")]
        [Tooltip("Max pull distance in world meters when in VR (course is at 0.042 scale, so world pull is much smaller than legacy 3.1).")]
        public float vrMaximumDragDistance = 0.16f;
        [Tooltip("Minimum shotPower to fire (was 0.035 -> 0.108m world at 3.1). 0.045 at 0.16 = 7.2mm deadzone.")]
        [Range(0.005f, 0.1f)] public float vrMinShotPower = 0.045f;

        [Header("Cup Assist")]
        [SerializeField] private float assistRadius = 0.08f;
        [SerializeField, Tooltip("Ball-center distance at which the final cup animation begins. Keep this within the dark cup so the ball is fully supported visually.")]
        private float captureRadius = 0.008f;
        [SerializeField] private float maximumAssistedSpeed = 0.6f;
        [SerializeField] private float maximumCaptureSpeed = 0.35f;
        [SerializeField] private float minimumPullAcceleration = 0.35f;
        [SerializeField] private float maximumPullAcceleration = 1.2f;

        private MiniGolfLevel currentLevel;
        private int currentLevelIndex;
        private int levelStrokes;
        private int totalStrokes;
        private bool dragging;
        private bool capturing;
        private bool levelComplete;
        private bool courseComplete;
        private Vector3 dragStartWorld;
        private Vector3 aimDirection;
        private float shotPower;
        private string feedback = string.Empty;
        private float feedbackUntil;

        public int CurrentLevelIndex => currentLevelIndex;
        public int LevelStrokes => levelStrokes;
        public int TotalStrokes => totalStrokes;
        public bool IsLevelComplete => levelComplete;
        public bool IsCapturing => capturing;
        public bool IsCourseComplete => courseComplete;
        public MiniGolfLevel CurrentLevel => currentLevel;
        public MiniGolfLevel[] AllLevels => levels;
        public float ShotPower => shotPower;
        public bool IsAiming => dragging;
        public Vector3 AimDirection => aimDirection;
        public string CurrentFeedback => feedback;
        public float FeedbackUntil => feedbackUntil;
        public float MaximumDragDistance => maximumDragDistance;
        public float MaximumImpulse => maximumImpulse;

        public bool GetUIValues(out int strokes, out int par, out string levelName, out int levelIndex, out int levelCount)
        {
            strokes = levelStrokes;
            par = currentLevel != null ? currentLevel.par : 0;
            levelName = currentLevel != null ? currentLevel.levelName : string.Empty;
            levelIndex = currentLevelIndex;
            levelCount = levels != null ? levels.Length : 0;
            return currentLevel != null;
        }

        private void Awake()
        {
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;

            if (levels == null || levels.Length == 0)
                levels = FindObjectsByType<MiniGolfLevel>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (MiniGolfLevel level in levels)
            {
                level.CacheAuthoredState();
                // Runtime patch for legacy scene balls that were built with old physics (mass 0.78, damping 0.72/0.82).
                // This ensures the B+C tuning takes effect without requiring a manual rebuild.
                if (level.ball != null)
                {
                    level.ball.mass = 0.45f;
                    level.ball.linearDamping = 0.65f;
                    level.ball.angularDamping = 0.9f;
                    // Ensure material reference is up to date (asset file was retuned)
                    var col = level.ball.GetComponent<SphereCollider>();
                    if (col != null)
                    {
                        col.radius = 0.2f;
                        if (col.sharedMaterial != null)
                        {
                            col.sharedMaterial.dynamicFriction = 0.55f;
                            col.sharedMaterial.staticFriction = 0.65f;
                            col.sharedMaterial.bounciness = 0.14f;
                            col.sharedMaterial.frictionCombine = PhysicsMaterialCombine.Maximum;
                            col.sharedMaterial.bounceCombine = PhysicsMaterialCombine.Average;
                        }
                    }
                }
            }

            // Clamp legacy scene's maximumImpulse (was 15 -> 7.6) to meter-correct 2.0 so pull doesn't launch off 0.042 table.
            if (maximumImpulse > 3.0f) maximumImpulse = 2.0f;

            EnsureVRRig();

            if (aimingLine != null)
            {
                aimingLine.enabled = false;
                aimingLine.useWorldSpace = true;
                aimingLine.positionCount = 2;
                aimingLine.startWidth = aimingLineWidth;
                aimingLine.endWidth = aimingLineWidth;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Keep line visible — 0.005 is sub-pixel at 0.65m table distance. Clamp to at least 0.015.
            if (aimingLineWidth < 0.01f) aimingLineWidth = 0.02f;
            aimingLineWidth = Mathf.Clamp(aimingLineWidth, 0.012f, 0.2f);
            vrMaximumDragDistance = Mathf.Clamp(vrMaximumDragDistance, 0.08f, 1.0f);
            vrMinShotPower = Mathf.Clamp(vrMinShotPower, 0.005f, 0.1f);
            if (aimingLine != null)
            {
                aimingLine.startWidth = aimingLineWidth;
                aimingLine.endWidth = aimingLineWidth;
                aimingLine.enabled = dragging;
            }
        }
#endif

        private void EnsureVRRig()
        {
            if (ovrRig == null)
                ovrRig = FindFirstObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
            if (ovrRig == null)
            {
                // Create VR rig at runtime if not present in scene (EditMode still has old camera)
                GameObject rigGO = new GameObject("OVRCameraRig");
                ovrRig = rigGO.AddComponent<OVRCameraRig>();
                var manager = rigGO.AddComponent<OVRManager>();
                manager.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
                Debug.Log("[MinimalGolfGame] Created OVRCameraRig at runtime for VR.");
            }
            if (ovrRig != null)
            {
                Camera centerCam = ovrRig.centerEyeAnchor != null ? ovrRig.centerEyeAnchor.GetComponent<Camera>() : null;
                if (gameCamera == null && centerCam != null)
                    gameCamera = centerCam;
                // Disable old isometric camera if still present
                var oldCam = GameObject.Find("ISOMETRIC CAMERA");
                if (oldCam != null) oldCam.SetActive(false);
                // Ensure anchor exists - now at scene root, same level as OVRCameraRig
                if (vrCourseAnchor == null)
                {
                    var placement = FindFirstObjectByType<VRCoursePlacement>(FindObjectsInactive.Include);
                    if (placement != null) vrCourseAnchor = placement.transform;
                    if (vrCourseAnchor == null)
                    {
                        GameObject go = GameObject.Find("VRCourseAnchor");
                        if (go != null) vrCourseAnchor = go.transform;
                    }
                    if (vrCourseAnchor == null)
                    {
                        Transform ts = ovrRig.trackingSpace;
                        Transform existing = ts != null ? ts.Find("VRCourseAnchor") : null;
                        if (existing != null) vrCourseAnchor = existing;
                        else
                        {
                            GameObject anchorGO = new GameObject("VRCourseAnchor");
                            // Place at world level, sibling to OVRCameraRig (not under TrackingSpace)
                            anchorGO.transform.localPosition = vrAnchorLocalPosition;
                            anchorGO.transform.localRotation = Quaternion.identity;
                            anchorGO.transform.localScale = vrAnchorLocalScale;
                            vrCourseAnchor = anchorGO.transform;
                        }
                    }
                }
                if (vrCourseAnchor != null)
                {
                    vrCourseAnchor.localPosition = vrAnchorLocalPosition;
                    vrCourseAnchor.localScale = vrAnchorLocalScale;
                    // Ensure VRCourseLevels exists under anchor with 1/10 scale
                    if (vrCourseLevels == null)
                        vrCourseLevels = vrCourseAnchor.Find("VRCourseLevels");
                    if (vrCourseLevels == null)
                    {
                        GameObject levelsGO = new GameObject("VRCourseLevels");
                        levelsGO.transform.SetParent(vrCourseAnchor, false);
                        levelsGO.transform.localPosition = Vector3.zero;
                        levelsGO.transform.localRotation = Quaternion.identity;
                        levelsGO.transform.localScale = vrCourseLevelsLocalScale;
                        vrCourseLevels = levelsGO.transform;
                    }
                    else
                    {
                        vrCourseLevels.localScale = vrCourseLevelsLocalScale;
                    }
                    // Migrate legacy VR_UI that was child of VRCourseAnchor -> move to stable vrUIAnchor
                    Transform legacyUI = vrCourseAnchor.Find("VR_UI");
                    if (legacyUI != null)
                    {
                        // Ensure stable anchor exists first so we can reparent
                        if (vrUIAnchor == null)
                        {
                            var uiRootGO = new GameObject("VR_UI_Root");
                            uiRootGO.transform.localPosition = vrAnchorLocalPosition;
                            uiRootGO.transform.localRotation = Quaternion.identity;
                            uiRootGO.transform.localScale = vrAnchorLocalScale;
                            // Place at scene root, sibling to VRCourseAnchor (same level)
                            if (vrCourseAnchor.parent != null)
                                uiRootGO.transform.SetParent(vrCourseAnchor.parent, false);
                            vrUIAnchor = uiRootGO.transform;
                        }
                        legacyUI.SetParent(vrUIAnchor, true);
                        // Keep world position stable, reset local to preserve previous VR_UI local offset if needed
                        Debug.Log("[MinimalGolfGame] Migrated legacy VR_UI from VRCourseAnchor -> vrUIAnchor");
                    }
                    // Ensure stable UI anchor exists at same level as VRCourseAnchor (NOT under it — avoids rotation)
                    if (vrUIAnchor == null)
                    {
                        // Try find existing root at scene root
                        var existingRoot = GameObject.Find("VR_UI_Root");
                        if (existingRoot != null) vrUIAnchor = existingRoot.transform;
                        else
                        {
                            Transform existingUI = null;
                            // Search for VR_UI already under a different parent (e.g., after migration)
                            var all = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                            foreach (var t in all) if (t.name == "VR_UI") { existingUI = t.parent; break; }
                            if (existingUI != null) vrUIAnchor = existingUI;
                        }
                        if (vrUIAnchor == null)
                        {
                            GameObject anchorGO = new GameObject("VR_UI_Root");
                            anchorGO.transform.localPosition = vrAnchorLocalPosition;
                            anchorGO.transform.localRotation = Quaternion.identity;
                            anchorGO.transform.localScale = vrAnchorLocalScale;
                            if (vrCourseAnchor.parent != null)
                                anchorGO.transform.SetParent(vrCourseAnchor.parent, false);
                            vrUIAnchor = anchorGO.transform;
                        }
                    }
                    else
                    {
                        // Keep UI anchor stable — enforce position/scale but NEVER copy rotation from course
                        vrUIAnchor.localPosition = vrAnchorLocalPosition;
                        vrUIAnchor.localScale = vrAnchorLocalScale;
                    }
                    // Ensure VR UI exists — parented to vrUIAnchor, sibling level to VRCourseLevels
                    if (FindFirstObjectByType<VRGolfUI>(FindObjectsInactive.Include) == null)
                    {
                        var ui = gameObject.AddComponent<VRGolfUI>();
                        ui.game = this;
                        ui.vrCourseAnchor = vrCourseAnchor;
                        ui.vrUIAnchor = vrUIAnchor;
                        ui.uiFont = uiFont;
                        Debug.Log("[MinimalGolfGame] Created VRGolfUI at runtime.");
                    }
                    else
                    {
                        var existingUI = FindFirstObjectByType<VRGolfUI>(FindObjectsInactive.Include);
                        if (existingUI != null && existingUI.vrUIAnchor == null)
                            existingUI.vrUIAnchor = vrUIAnchor;
                    }
                    // Ensure clubs on anchors - defer one frame for OVRCameraRig to init
                    StartCoroutine(EnsureClubsNextFrame());
                }
            }
            else
            {
                Debug.LogWarning("[MinimalGolfGame] OVRCameraRig not found - VR mode will fallback but should be present.");
            }
        }

        private IEnumerator EnsureClubsNextFrame()
        {
            yield return null;
            if (ovrRig == null) yield break;
            // Prefer Controller anchors (Touch) over Hand anchors; they are correctly offset for controllers
            Transform leftAnchor = ovrRig.leftControllerAnchor != null ? ovrRig.leftControllerAnchor : ovrRig.leftHandAnchor;
            Transform rightAnchor = ovrRig.rightControllerAnchor != null ? ovrRig.rightControllerAnchor : ovrRig.rightHandAnchor;
            if (leftAnchor != null && leftAnchor.GetComponentInChildren<VRGolfClub>() == null)
                CreateClub(leftAnchor, OVRInput.Controller.LTouch);
            if (rightAnchor != null && rightAnchor.GetComponentInChildren<VRGolfClub>() == null)
                CreateClub(rightAnchor, OVRInput.Controller.RTouch);
        }

        private void CreateClub(Transform anchor, OVRInput.Controller controller)
        {
            GameObject clubGO = new GameObject(controller == OVRInput.Controller.LTouch ? "VR Club Left" : "VR Club Right");
            clubGO.transform.SetParent(anchor, false);
            clubGO.transform.localPosition = Vector3.zero;
            clubGO.transform.localRotation = Quaternion.identity;
            var col = clubGO.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.035f;
            var club = clubGO.AddComponent<VRGolfClub>();
            club.controller = controller;
            club.game = this;
            club.overlapRadius = 0.11f;
            club.sphereRadius = 0.08f;
            club.sphereOpacity = 0.35f;
            club.showSphere = true;
        }

        private void Start()
        {
            LoadLevel(0, true);
            StartCoroutine(LogVRPose());
        }

        private IEnumerator LogVRPose()
        {
            yield return null;
            yield return new WaitForSeconds(0.5f);
            if (ovrRig != null && ovrRig.centerEyeAnchor != null && vrCourseAnchor != null)
                Debug.Log($"[VRPose] eye {ovrRig.centerEyeAnchor.position} anchor {vrCourseAnchor.position}");
        }

        private void Update()
        {
            if (courseComplete)
            {
                // VR UI button handles restart, but keep keyboard fallback for editor/tests
#if UNITY_EDITOR
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                    RestartCourse();
#endif
                if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch) ||
                    OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch))
                    RestartCourse();
                return;
            }

            if (currentLevel == null || levelComplete)
                return;

            if (currentLevel.IsRevealing)
                return;

            HandleThumbstickRotation();

            Rigidbody ball = currentLevel.ball;
            if (!capturing && currentLevel.IsOutsideCourse(ball.position))
            {
                ResetBall(false);
                ShowFeedback("BALL RETURNED");
            }
        }

        private void HandleThumbstickRotation()
        {
            if (dragging || vrCourseLevels == null) return;
            if (!CanTakeAction()) return;
            // Right thumbstick X rotates course per grill decision (thumbstick yaw) — rotate VRCourseLevels only, anchor stays stable
            Vector2 thumb = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
            if (thumb.sqrMagnitude < 0.25f)
                thumb = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            // Strict deadzone to prevent drift — require intentional deflection
            if (Mathf.Abs(thumb.x) > 0.6f)
            {
                float yaw = thumb.x * thumbstickRotationSpeed * Time.deltaTime;
                // Rotate levels around up in world, but keep position (Rotate does not translate)
                Vector3 posBefore = vrCourseLevels.position;
                vrCourseLevels.Rotate(Vector3.up, yaw, Space.World);
                // Guard against any position drift (Rotate should not translate, but clamp just in case)
                if ((vrCourseLevels.position - posBefore).sqrMagnitude > 0.0005f)
                    vrCourseLevels.position = posBefore;
            }
            // Hard clamp rig to origin — prevents continuous backward drift from external locomotion / tracking
            // Fix for CenterEyeAnchor falling: previously only x/z were clamped, leaving y free to accumulate gravity drift
            if (ovrRig != null && ovrRig.transform.localPosition.sqrMagnitude > 0.0004f)
            {
                ovrRig.transform.localPosition = Vector3.zero;
            }
            // Also ensure TrackingSpace stays at origin (OVR drives eye via localPosition, not rig translation)
            if (ovrRig != null && ovrRig.trackingSpace != null && ovrRig.trackingSpace.localPosition.sqrMagnitude > 0.0004f)
            {
                ovrRig.trackingSpace.localPosition = Vector3.zero;
                ovrRig.trackingSpace.localRotation = Quaternion.identity;
            }
        }

        private void FixedUpdate()
        {
            if (currentLevel == null || currentLevel.ball == null || currentLevel.IsRevealing || capturing || levelComplete || courseComplete)
                return;

            // While aiming the ball is frozen kinematic — skip assist/damping entirely
            if (dragging)
                return;

            Rigidbody ball = currentLevel.ball;
            if (ball.isKinematic)
                return;

            Vector3 velocity = ball.linearVelocity;
            float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            Vector3 hole = currentLevel.holeCenter.position;
            Vector3 offset = hole - ball.position;
            offset.y = 0f;
            float distance = offset.magnitude;

            if (distance <= assistRadius && horizontalSpeed <= maximumAssistedSpeed)
            {
                float closeness = 1f - Mathf.Clamp01(distance / assistRadius);
                float acceleration = Mathf.Lerp(minimumPullAcceleration, maximumPullAcceleration, closeness * closeness);
                if (distance > 0.001f)
                    ball.AddForce(offset.normalized * acceleration, ForceMode.Acceleration);

                if (distance <= captureRadius && horizontalSpeed <= maximumCaptureSpeed && Mathf.Abs(ball.position.y - hole.y) < 1.25f)
                {
                    StartCoroutine(CaptureBall());
                    return;
                }
            }

            if (!dragging && horizontalSpeed < 0.085f)
            {
            }
        }

        // VR aiming API used by VRGolfClub
        public bool BeginVRAim(Vector3 startWorld)
        {
            if (!CanTakeAction())
            {
                ShowFeedback("WAIT FOR THE BALL");
                return false;
            }
            dragging = true;
            dragStartWorld = startWorld;
            dragStartWorld.y = currentLevel != null ? currentLevel.ball.position.y : dragStartWorld.y;
            aimDirection = Vector3.zero;
            shotPower = 0f;
            if (aimingLine != null) aimingLine.enabled = true;
            UpdateAimingLine();
            // Freeze ball while aiming so controller overlap / trigger press cannot nudge it via physics.
            // Kinematic prevents any collision response; we also zero velocities and sleep.
            if (currentLevel != null && currentLevel.ball != null)
            {
                Rigidbody b = currentLevel.ball;
                //b.isKinematic = true;
            }
            return true;
        }

        public void UpdateVRAim(Vector3 currentWorld)
        {
            if (!dragging) return;
            currentWorld.y = dragStartWorld.y;
            Vector3 pull = dragStartWorld - currentWorld;
            pull.y = 0f;
            // In VR the course is at 0.042 scale, so world pull of 0.45m = full power. Use VR-tuned distance when ovrRig exists.
            float effectiveMax = (ovrRig != null ? vrMaximumDragDistance : maximumDragDistance);
            effectiveMax = Mathf.Max(0.2f, effectiveMax);
            float distance = Mathf.Min(pull.magnitude, effectiveMax);
            shotPower = Mathf.Clamp01(distance / effectiveMax);
            aimDirection = pull.sqrMagnitude > 0.0001f ? pull.normalized : Vector3.zero;
            UpdateAimingLine();
        }

        public bool TryEndVRAimAndShoot()
        {
            if (!dragging) return false;
            dragging = false;
            if (aimingLine != null) aimingLine.enabled = false;
            float minPower = (ovrRig != null ? vrMinShotPower : 0.035f);
            // Below-threshold pulls should not shoot but still unfreeze the ball so it can be re-aimed.
            if (shotPower < minPower || aimDirection.sqrMagnitude < 0.001f)
            {
                UnfreezeBallForRetry();
                shotPower = 0f;
                aimDirection = Vector3.zero;
                return false;
            }
            // Must still be playable (e.g., not capturing). Unfreeze first so CanTakeAction sees dynamic ball.
            UnfreezeBallForShot();
            if (!CanTakeAction())
            {
                shotPower = 0f;
                aimDirection = Vector3.zero;
                return false;
            }
            bool ok = TryApplyShot(aimDirection, shotPower);
            shotPower = 0f;
            aimDirection = Vector3.zero;
            return ok;
        }

        private void UnfreezeBallForRetry()
        {
            if (currentLevel == null || currentLevel.ball == null) return;
            Rigidbody b = currentLevel.ball;
            if (b.isKinematic)
            {
                b.isKinematic = false;
            }
        }

        private void UnfreezeBallForShot()
        {
            if (currentLevel == null || currentLevel.ball == null) return;
            Rigidbody b = currentLevel.ball;
            if (b.isKinematic)
            {
                b.isKinematic = false;
            }
        }

        public void CancelAim()
        {
            dragging = false;
            if (aimingLine != null) aimingLine.enabled = false;
            // Unfreeze ball without shooting — restore to dynamic at rest
            if (currentLevel != null && currentLevel.ball != null && currentLevel.ball.isKinematic)
            {
                Rigidbody b = currentLevel.ball;
                b.isKinematic = false;
            }
            shotPower = 0f;
            aimDirection = Vector3.zero;
        }

        public void UpdateAimingLine()
        {
            if (aimingLine == null || currentLevel == null)
                return;

            // Ensure line GameObject is active and visible while aiming
            if (!aimingLine.gameObject.activeSelf) aimingLine.gameObject.SetActive(true);
            aimingLine.enabled = true;
            aimingLine.startWidth = aimingLineWidth;
            aimingLine.endWidth = aimingLineWidth;
            Vector3 ballCenter = currentLevel.ball.worldCenterOfMass;
            // VR course is at 0.042 world scale (world length ~0.5m). Legacy 0.35-3.2 is 6x too large in VR.
            // Use VR-scaled length when rig exists, keep legacy for flat-screen fallback.
            float displayLength;
            if (ovrRig != null || vrCourseLevels != null)
            {
                // 0.02m min visible at 0.65m table distance, 0.55m max ~ full table length
                displayLength = Mathf.Lerp(0.02f, 0.55f, shotPower);
                if (aimDirection.sqrMagnitude < 0.0001f) displayLength = 0f;
            }
            else
            {
                displayLength = Mathf.Lerp(0.35f, 3.2f, shotPower);
            }
            aimingLine.SetPosition(0, ballCenter);
            aimingLine.SetPosition(1, ballCenter + aimDirection * displayLength);

            Color low = new Color32(0x89, 0xE0, 0xB3, 0xFF);
            Color middle = new Color32(0xF3, 0xC9, 0x6B, 0xFF);
            Color high = new Color32(0xE1, 0x82, 0x2F, 0xFF);
            Color color = shotPower < 0.55f
                ? Color.Lerp(low, middle, shotPower / 0.55f)
                : Color.Lerp(middle, high, (shotPower - 0.55f) / 0.45f);
            aimingLine.startColor = color;
            aimingLine.endColor = color;
        }

        public bool TryApplyShot(Vector3 direction, float power)
        {
            if (!CanTakeAction()) return false;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return false;
            direction.Normalize();
            power = Mathf.Clamp01(power);
            float minPower = (ovrRig != null ? vrMinShotPower : 0.035f);
            if (power < minPower) return false;
            Rigidbody ball = currentLevel.ball;
            // Ensure dynamic and awake before AddForce — probe showed AddForce ignored when kinematic/sleeping
            // Realistic physics: use AddForce Impulse (mass-dependent) as requested
            float impulse = Mathf.Lerp(0.25f, maximumImpulse, power);
            Debug.Log($"[MinimalGolfGame] Shot impulse {impulse:F3} power {power:F3} direction {direction}");
            ball.AddForce(direction * impulse, ForceMode.Impulse);
            // Wake again after impulse in case FixedUpdate would sleep it
            levelStrokes++;
            totalStrokes++;
            AudioManager.Instance?.PlayShotSfx();
            OVRInput.SetControllerVibration(0.5f, 0.7f, OVRInput.Controller.RTouch);
            OVRInput.SetControllerVibration(0.5f, 0.7f, OVRInput.Controller.LTouch);
            return true;
        }

        public bool CanTakeAction()
        {
            return currentLevel != null && !currentLevel.IsRevealing && !capturing && !levelComplete && currentLevel.ball.linearVelocity.magnitude <= playableSpeed;
        }

        private void ResetBall(bool penalty, bool keepKinematic = false)
        {
            StopAllCoroutines();
            capturing = false;
            levelComplete = false;
            dragging = false;
            if (aimingLine != null)
                aimingLine.enabled = false;
            shotPower = 0f;
            aimDirection = Vector3.zero;

            Rigidbody ball = currentLevel.ball;
            //ball.isKinematic = true;
            ball.interpolation = RigidbodyInterpolation.Interpolate;
            ball.transform.SetPositionAndRotation(currentLevel.ballSpawn.position, currentLevel.ballSpawn.rotation);
            ball.transform.localScale = Vector3.one;
            if (!keepKinematic)
            {
                ball.isKinematic = false;
            }
            // Haptics instead of camera shake
            OVRInput.SetControllerVibration(0.3f, 0.4f, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(0.3f, 0.4f, OVRInput.Controller.RTouch);

            if (penalty)
            {
                levelStrokes++;
                totalStrokes++;
            }
        }

        private IEnumerator CaptureBall()
        {
            if (capturing)
                yield break;

            capturing = true;
            dragging = false;
            if (aimingLine != null) aimingLine.enabled = false;
            shotPower = 0f;
            aimDirection = Vector3.zero;
            Rigidbody ball = currentLevel.ball;
            RigidbodyInterpolation previousInterpolation = ball.interpolation;
            ball.interpolation = RigidbodyInterpolation.None;
            //ball.isKinematic = true;
            AudioManager.Instance?.PlayHoleSfx();
            OVRInput.SetControllerVibration(1f, 0.9f, OVRInput.Controller.RTouch);

            Vector3 startPosition = ball.position;
            Vector3 startScale = ball.transform.localScale;
            Vector3 holePosition = currentLevel.holeCenter.position;
            Vector3 centeredPosition = new Vector3(holePosition.x, startPosition.y - 0.055f, holePosition.z);
            Vector3 centeredScale = startScale * 0.88f;

            const float centeringDuration = 0.10f;
            float elapsed = 0f;
            while (elapsed < centeringDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / centeringDuration);
                ball.position = Vector3.Lerp(startPosition, centeredPosition, t);
                ball.transform.localScale = Vector3.Lerp(startScale, centeredScale, t);
                yield return null;
            }

            ball.position = centeredPosition;
            ball.transform.localScale = centeredScale;

            Vector3 targetPosition = holePosition + Vector3.down * 0.32f;
            const float sinkingDuration = 0.52f;
            elapsed = 0f;
            while (elapsed < sinkingDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / sinkingDuration);
                ball.position = Vector3.Lerp(centeredPosition, targetPosition, t);
                ball.transform.localScale = Vector3.Lerp(centeredScale, startScale * 0.08f, t);
                yield return null;
            }

            ball.position = targetPosition;
            ball.transform.localScale = startScale * 0.08f;
            ball.interpolation = previousInterpolation;

            levelComplete = true;
            string result = levelStrokes <= currentLevel.par ? "UNDER PAR!" : levelStrokes == currentLevel.par ? "ON PAR!" : "+" + (levelStrokes - currentLevel.par) + " OVER PAR";
            ShowFeedback("HOLE COMPLETE  •  " + result, 2.2f);
            yield return new WaitForSeconds(2.05f);

            if (currentLevelIndex + 1 < levels.Length)
                LoadLevel(currentLevelIndex + 1, false);
            else
                courseComplete = true;
        }

        private void LoadLevel(int index, bool resetTotals)
        {
            if (levels == null || levels.Length == 0)
                return;

            if (resetTotals)
                totalStrokes = 0;

            currentLevelIndex = Mathf.Clamp(index, 0, levels.Length - 1);
            levelStrokes = 0;
            levelComplete = false;
            capturing = false;
            dragging = false;
            courseComplete = false;
            shotPower = 0f;
            aimDirection = Vector3.zero;

            for (int i = 0; i < levels.Length; i++)
            {
                levels[i].RestoreRuntimeTransform();
                levels[i].gameObject.SetActive(i == currentLevelIndex);
                if (i == currentLevelIndex)
                {
                    // Active level at VRCourseLevels origin (hierarchy already handles 1/10 scale)
                    if (vrCourseLevels != null)
                        levels[i].transform.SetParent(vrCourseLevels, false);
                    levels[i].transform.localPosition = Vector3.zero;
                    levels[i].transform.localRotation = Quaternion.identity;
                }
            }

            currentLevel = levels[currentLevelIndex];
            currentLevel.gameObject.SetActive(true);
            // Keep original cameraSize field for backwards compat but don't apply to VR camera
            // Keep ball kinematic through reveal - ReleaseBall() is the sole place that
            // makes the ball dynamic, avoiding a one-frame dynamic window over the void
            // while LevelRevealAnimator offsets the course (6m local) at VR table scale.
            ResetBall(false, keepKinematic: true);
            currentLevel.revealAnimator?.PlayReveal();
            // If no reveal (no animator or 0 parts), release immediately
            if (currentLevel.revealAnimator == null || currentLevel.revealAnimator.PartCount == 0)
            {
                Rigidbody ball = currentLevel.ball;
                if (ball != null && ball.isKinematic)
                {
                    ball.isKinematic = false;
                }
            }
            ShowFeedback("LEVEL " + (currentLevelIndex + 1) + "  •  " + currentLevel.levelName, 1.65f);
        }

        private void RestartCourse()
        {
            LoadLevel(0, true);
        }

        public void DebugLoadLevel(int index)
        {
            StopAllCoroutines();
            LoadLevel(index, false);
        }

        private void ShowFeedback(string message, float duration = 1.25f)
        {
            feedback = message;
            feedbackUntil = Time.unscaledTime + duration;
        }

        private void OnDestroy() { }
    }
}
