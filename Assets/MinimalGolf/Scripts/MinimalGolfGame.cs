using System.Collections;
using UnityEngine;

namespace MinimalGolf
{
    public sealed class MinimalGolfGame : MonoBehaviour
    {
        [Header("Authored Scene References")]
        public MiniGolfLevel[] levels;
        public Camera gameCamera;
        public LineRenderer aimingLine;
        public Font uiFont;

        [Header("VR References")]
        public OVRCameraRig ovrRig;
        public Transform vrCourseAnchor;
        public Transform vrCourseLevels;
        [Tooltip("VR course anchor local position relative to TrackingSpace. Beneath eye level, in front.")]
        public Vector3 vrAnchorLocalPosition = new Vector3(0f, 0.75f, 0.65f);
        public Vector3 vrAnchorLocalScale = new Vector3(1f, 1f, 1f);
        public Vector3 vrCourseLevelsLocalScale = new Vector3(0.042f, 0.042f, 0.042f);
        public float thumbstickRotationSpeed = 70f;

        [Header("Shot Tuning")]
        [SerializeField] private float maximumImpulse = 7.6f;
        [SerializeField] private float maximumDragDistance = 3.1f;
        [SerializeField] private float playableSpeed = 0.32f;

        [Header("Cup Assist")]
        [SerializeField] private float assistRadius = 1.15f;
        [SerializeField, Tooltip("Ball-center distance at which the final cup animation begins. Keep this within the dark cup so the ball is fully supported visually.")]
        private float captureRadius = 0.012f;
        [SerializeField] private float maximumAssistedSpeed = 3.5f;
        [SerializeField] private float maximumCaptureSpeed = 2.5f;
        [SerializeField] private float minimumPullAcceleration = 1.25f;
        [SerializeField] private float maximumPullAcceleration = 5.5f;

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
                level.CacheAuthoredState();

            EnsureVRRig();

            if (aimingLine != null)
            {
                aimingLine.enabled = false;
                aimingLine.useWorldSpace = true;
                aimingLine.positionCount = 2;
                aimingLine.startWidth = 0.045f;
                aimingLine.endWidth = 0.045f;
            }
        }

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
                    // Ensure VR UI exists
                    if (FindFirstObjectByType<VRGolfUI>(FindObjectsInactive.Include) == null)
                    {
                        var ui = gameObject.AddComponent<VRGolfUI>();
                        ui.game = this;
                        ui.vrCourseAnchor = vrCourseAnchor;
                        ui.uiFont = uiFont;
                        Debug.Log("[MinimalGolfGame] Created VRGolfUI at runtime.");
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
            if (dragging || vrCourseAnchor == null) return;
            if (!CanTakeAction()) return;
            // Right thumbstick X rotates course per grill decision (thumbstick yaw)
            Vector2 thumb = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
            if (thumb.sqrMagnitude < 0.25f)
                thumb = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            if (Mathf.Abs(thumb.x) > 0.6f)
            {
                float yaw = thumb.x * thumbstickRotationSpeed * Time.deltaTime;
                // Rotate anchor around up in world, but keep position
                vrCourseAnchor.Rotate(Vector3.up, yaw, Space.World);
                // Also need to sync physics if ball is kinematic? We keep ball physics stable
                Physics.SyncTransforms();
            }
        }

        private void FixedUpdate()
        {
            if (currentLevel == null || currentLevel.ball == null || currentLevel.IsRevealing || capturing || levelComplete || courseComplete)
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

                velocity = ball.linearVelocity;
                float damping = Mathf.Lerp(0.995f, 0.91f, closeness);
                velocity.x *= damping;
                velocity.z *= damping;
                ball.linearVelocity = velocity;

                if (distance <= captureRadius && horizontalSpeed <= maximumCaptureSpeed && Mathf.Abs(ball.position.y - hole.y) < 1.25f)
                {
                    StartCoroutine(CaptureBall());
                    return;
                }
            }

            if (!dragging && horizontalSpeed < 0.085f)
            {
                velocity = ball.linearVelocity;
                velocity.x = 0f;
                velocity.z = 0f;
                ball.linearVelocity = velocity;
                ball.angularVelocity *= 0.82f;
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
            return true;
        }

        public void UpdateVRAim(Vector3 currentWorld)
        {
            if (!dragging) return;
            currentWorld.y = dragStartWorld.y;
            Vector3 pull = dragStartWorld - currentWorld;
            pull.y = 0f;
            float distance = Mathf.Min(pull.magnitude, maximumDragDistance);
            shotPower = Mathf.Clamp01(distance / maximumDragDistance);
            aimDirection = pull.sqrMagnitude > 0.0001f ? pull.normalized : Vector3.zero;
            UpdateAimingLine();
        }

        public bool TryEndVRAimAndShoot()
        {
            if (!dragging) return false;
            dragging = false;
            if (aimingLine != null) aimingLine.enabled = false;
            if (shotPower < 0.035f || aimDirection.sqrMagnitude < 0.1f || !CanTakeAction())
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

        public void CancelAim()
        {
            dragging = false;
            if (aimingLine != null) aimingLine.enabled = false;
            shotPower = 0f;
            aimDirection = Vector3.zero;
        }

        public void UpdateAimingLine()
        {
            if (aimingLine == null || currentLevel == null)
                return;

            Vector3 ballCenter = currentLevel.ball.worldCenterOfMass;
            float displayLength = Mathf.Lerp(0.35f, 3.2f, shotPower);
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
            if (power < 0.035f) return false;
            Rigidbody ball = currentLevel.ball;
            Vector3 velocity = ball.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            ball.linearVelocity = velocity;
            ball.angularVelocity *= 0.15f;
            ball.AddForce(direction * Mathf.Lerp(1.1f, maximumImpulse, power), ForceMode.Impulse);
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

        private void ResetBall(bool penalty)
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
            ball.isKinematic = true;
            ball.interpolation = RigidbodyInterpolation.Interpolate;
            ball.transform.SetPositionAndRotation(currentLevel.ballSpawn.position, currentLevel.ballSpawn.rotation);
            ball.transform.localScale = Vector3.one;
            Physics.SyncTransforms();
            ball.isKinematic = false;
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
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
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
            ball.isKinematic = true;
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
            ResetBall(false);
            Physics.SyncTransforms();
            currentLevel.revealAnimator?.PlayReveal();
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
