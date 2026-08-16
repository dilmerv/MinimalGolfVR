using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MinimalGolf
{
    /// <summary>
    /// Drives World Space Canvases for VR. Replaces the old OnGUI Draw* methods.
    /// Canvases are created dynamically under VRCourseAnchor if not assigned.
    /// Hierarchy preview supported via ExecuteAlways + previewInEditMode.
    /// Per-canvas + global scale + font size exposed for editor tuning.
    /// IdentityCard + StatsCard + ProgressCard combined into single GamePlayCard.
    /// </summary>
    [ExecuteAlways]
    public sealed class VRGolfUI : MonoBehaviour
    {
        public MinimalGolfGame game;
        public Font uiFont;
        public Transform vrCourseAnchor;

        [Header("World Space Canvas Refs (auto-created if null)")]
        [Tooltip("Combined gameplay HUD (was IdentityCard + StatsCard + ProgressCard)")]
        public Canvas gamePlayCanvas;
        public Canvas powerCanvas;
        public Canvas feedbackCanvas;
        public Canvas courseCompleteCanvas;

        // Deprecated - kept for migration, hidden in inspector (old separate canvases)
        [HideInInspector] public Canvas identityCanvas;
        [HideInInspector] public Canvas statsCanvas;
        [HideInInspector] public Canvas progressCanvas;

        [Header("Preview")]
        [Tooltip("Show VR UI in Edit Mode hierarchy for preview without entering Play.")]
        public bool previewInEditMode = true;

        [Header("Scaling (applied in Edit & Play)")]
        [Tooltip("Global multiplier applied to all World Space canvases. 1 = default size.")]
        [Range(0.2f, 4f)]
        public float globalScale = 1f;

        [Tooltip("Per-canvas world scales. Final localScale = perCanvasScale * globalScale.")]
        [Range(0.0005f, 0.01f)] public float gamePlayScale = 0.0022f;
        [Range(0.0005f, 0.01f)] public float powerScale = 0.0025f;
        [Range(0.0005f, 0.01f)] public float feedbackScale = 0.0025f;
        [Range(0.0005f, 0.01f)] public float courseCompleteScale = 0.0028f;

        [Header("Layout — Positions & Sizes (VR_UI local)")]
        [Tooltip("GamePlayCard: combined Identity + Stats + Progress. Local position relative to VR_UI.")]
        public Vector3 gamePlayCardPosition = new Vector3(0f, 0.65f, -0.7f);
        [Tooltip("GamePlayCard size in canvas pixels (width, height). 1450×190 ≈ 3.19m × 0.42m at 0.0022 scale.")]
        public Vector2 gamePlayCardSize = new Vector2(1450, 190);
        [Tooltip("PowerMeter: putt strength meter.")]
        public Vector3 powerMeterPosition = new Vector3(0f, 0.15f, -0.2f);
        public Vector2 powerMeterSize = new Vector2(560, 110);
        [Tooltip("FeedbackToast: ball returned / hole complete.")]
        public Vector3 feedbackToastPosition = new Vector3(0f, 0.35f, 0.2f);
        public Vector2 feedbackToastSize = new Vector2(560, 70);
        [Tooltip("CourseComplete: final total-strokes screen.")]
        public Vector3 courseCompletePosition = new Vector3(0f, 0.6f, 0.4f);
        public Vector2 courseCompleteSize = new Vector2(760, 500);

        [Header("GamePlayCard — Group Positions (inside GamePlayCard)")]
        [Tooltip("Identity group (MINIMAL GOLF / THE WARM UP) anchoredPosition inside GamePlayCard.")]
        public Vector2 identityGroupPosition = new Vector2(-480, -10);
        [Tooltip("Stats group (STROKES / PAR) anchoredPosition inside GamePlayCard.")]
        public Vector2 statsGroupPosition = new Vector2(480, -10);
        [Tooltip("Progress group (COURSE PROGRESS + pips) anchoredPosition inside GamePlayCard.")]
        public Vector2 progressGroupPosition = new Vector2(0, 52);

        [Header("Font Sizes (base, multiplied by Font Scale)")]
        [Tooltip("Global font scale multiplier. 1 = base sizes.")]
        [Range(0.5f, 2.5f)] public float fontScale = 1f;
        [Header("GamePlayCard Fonts")]
        public int titleFontSize = 22;
        public int courseFontSize = 13;
        public int levelFontSize = 9;
        public int labelFontSize = 8;
        public int strokesFontSize = 28;
        public int parFontSize = 28;
        public int progressLabelFontSize = 8;
        [Header("Other Canvas Fonts")]
        public int powerLabelFontSize = 9;
        public int powerPercentFontSize = 9;
        public int feedbackFontSize = 11;
        public int completeTitleFontSize = 32;
        public int completeSubFontSize = 8;
        public int completeTotalFontSize = 28;
        public int completeTotalLabelFontSize = 8;
        public int completeButtonFontSize = 11;

        // GamePlayCard internal refs
        private Text identityTitle;
        private Text identityCourse;
        private Text identityLevel;
        private Text statsLabelStrokes;
        private Text statsStrokes;
        private Text statsLabelPar;
        private Text statsPar;
        private Text progressLabel;
        private Image[] progressPips;
        // Power
        private Text powerLabel;
        private Text powerPercent;
        private Image[] powerSegments;
        // Feedback
        private Text feedbackText;
        private CanvasGroup feedbackGroup;
        // CourseComplete
        private Text courseCompleteTitle;
        private Text courseCompleteSub;
        private Text courseCompleteTotal;
        private Text courseCompleteTotalLabel;
        private Text courseCompleteButtonLabel;
        private Button playAgainButton;

        private Transform _uiRoot;

        private static readonly Color PanelColor = new Color(0.055f, 0.14f, 0.19f, 0.90f);
        private static readonly Color PanelSoft = new Color(0.055f, 0.14f, 0.19f, 0.72f);
        private static readonly Color PaleText = new Color32(0xFA, 0xF1, 0xD2, 0xFF);
        private static readonly Color Accent = new Color32(0xA9, 0xDB, 0xE8, 0xFF);
        private static readonly Color Orange = new Color32(0xE1, 0x82, 0x2F, 0xFF);
        private static readonly Color Seafoam = new Color32(0x8B, 0xCB, 0xA8, 0xFF);
        private static readonly Color Gold = new Color32(0xF3, 0xC9, 0x6B, 0xFF);
        private static readonly Color WarmCream = new Color32(0xFF, 0xED, 0xBF, 0xFF);
        private static readonly Color Ink = new Color32(0x16, 0x35, 0x3D, 0xFF);

        private bool _deferredPreviewPending;

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
#if UNITY_EDITOR
            if (!Application.isPlaying && previewInEditMode)
            {
                DeferPreviewRebuild();
            }
#endif
        }

        private void OnValidate()
        {
            CacheReferences();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (previewInEditMode)
                {
                    DeferPreviewRebuild();
                }
                if (!_deferredPreviewPending)
                {
                    ApplyScaling();
                    ApplyLayout();
                    ApplyFontSizes();
                }
                if (previewInEditMode)
                {
                    EditorUtility.SetDirty(this);
                    if (vrCourseAnchor != null) EditorUtility.SetDirty(vrCourseAnchor);
                    if (_uiRoot != null) EditorUtility.SetDirty(_uiRoot);
                }
            }
            else
            {
                ApplyScaling();
                ApplyLayout();
                ApplyFontSizes();
            }
#else
            ApplyScaling();
            ApplyLayout();
            ApplyFontSizes();
#endif
        }

#if UNITY_EDITOR
        private void DeferPreviewRebuild()
        {
            if (_deferredPreviewPending) return;
            _deferredPreviewPending = true;
            EditorApplication.delayCall += HandleDeferredPreview;
        }

        private void HandleDeferredPreview()
        {
            _deferredPreviewPending = false;
            if (this == null) return;
            if (Application.isPlaying) return;
            if (!previewInEditMode) return;
            CacheReferences();
            EnsureCanvases();
            BindExistingReferences();
            ApplyScaling();
            ApplyLayout();
            ApplyFontSizes();
            // Mark dirty after deferred creation
            EditorUtility.SetDirty(this);
            if (_uiRoot != null) EditorUtility.SetDirty(_uiRoot);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif

        private void Start()
        {
            EnsureCanvases();
            BindExistingReferences();
            ApplyScaling();
            ApplyLayout();
            ApplyFontSizes();
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (!previewInEditMode) return;
                if (gamePlayCanvas != null || powerCanvas != null)
                {
                    ApplyScaling();
                    ApplyLayout();
                    ApplyFontSizes();
                }
                return;
            }
#endif
            if (game == null) return;
            EnsureCanvases();
            BindExistingReferences();
            ApplyScaling();
            ApplyLayout();
            ApplyFontSizes();
            UpdateIdentity();
            UpdateStats();
            UpdateProgress();
            UpdatePower();
            UpdateFeedback();
            UpdateCourseComplete();
        }

        private void CacheReferences()
        {
            if (game == null) game = FindFirstObjectByType<MinimalGolfGame>();
            if (vrCourseAnchor == null && game != null) vrCourseAnchor = game.vrCourseAnchor;
            if (vrCourseAnchor == null)
            {
                var go = GameObject.Find("VRCourseAnchor");
                if (go != null) vrCourseAnchor = go.transform;
            }
            if (vrCourseAnchor == null)
            {
                var rig = FindFirstObjectByType<OVRCameraRig>();
                if (rig != null)
                {
                    if (rig.trackingSpace != null)
                    {
                        var t = rig.trackingSpace.Find("VRCourseAnchor");
                        if (t != null) vrCourseAnchor = t;
                    }
                    if (vrCourseAnchor == null)
                    {
                        var fallback = GameObject.Find("VRCourseAnchor");
                        if (fallback != null) vrCourseAnchor = fallback.transform;
                    }
                }
            }
            if (uiFont == null && game != null) uiFont = game.uiFont;
            if (uiFont == null)
            {
                uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            if (vrCourseAnchor != null)
            {
                _uiRoot = vrCourseAnchor.Find("VR_UI");
            }
        }

        private void BindExistingReferences()
        {
            if (gamePlayCanvas != null)
            {
                // Try to rebind if null (supports edit-mode persistence)
                if (identityTitle == null) identityTitle = FindTextRecursive(gamePlayCanvas.transform, "Title");
                if (identityCourse == null) identityCourse = FindTextRecursive(gamePlayCanvas.transform, "Course");
                if (identityLevel == null) identityLevel = FindTextRecursive(gamePlayCanvas.transform, "Level");
                if (statsLabelStrokes == null) statsLabelStrokes = FindTextRecursive(gamePlayCanvas.transform, "LabelStrokes");
                if (statsStrokes == null) statsStrokes = FindTextRecursive(gamePlayCanvas.transform, "Strokes");
                if (statsLabelPar == null) statsLabelPar = FindTextRecursive(gamePlayCanvas.transform, "LabelPar");
                if (statsPar == null) statsPar = FindTextRecursive(gamePlayCanvas.transform, "Par");
                if (progressLabel == null) progressLabel = FindTextRecursive(gamePlayCanvas.transform, "ProgressLabel");
                if (progressPips == null)
                {
                    var pips = new Image[8];
                    bool foundAny = false;
                    for (int i = 0; i < 8; i++)
                    {
                        var pip = gamePlayCanvas.transform.Find($"ProgressGroup/Pip{i}")?.GetComponent<Image>();
                        if (pip == null) pip = gamePlayCanvas.transform.Find($"Pip{i}")?.GetComponent<Image>(); // fallback old flat
                        pips[i] = pip;
                        if (pip != null) foundAny = true;
                    }
                    if (foundAny) progressPips = pips;
                }
            }
            if (powerCanvas != null)
            {
                if (powerLabel == null) powerLabel = powerCanvas.transform.Find("Label")?.GetComponent<Text>();
                if (powerPercent == null) powerPercent = powerCanvas.transform.Find("Percent")?.GetComponent<Text>();
                if (powerSegments == null)
                {
                    var segs = new Image[12];
                    bool foundAny = false;
                    for (int i = 0; i < 12; i++)
                    {
                        var seg = powerCanvas.transform.Find($"Seg{i}")?.GetComponent<Image>();
                        segs[i] = seg;
                        if (seg != null) foundAny = true;
                    }
                    if (foundAny) powerSegments = segs;
                }
            }
            if (feedbackCanvas != null)
            {
                if (feedbackText == null) feedbackText = FindTextRecursive(feedbackCanvas.transform, "Feedback");
                if (feedbackGroup == null) feedbackGroup = feedbackCanvas.GetComponent<CanvasGroup>();
            }
            if (courseCompleteCanvas != null)
            {
                if (courseCompleteTitle == null) courseCompleteTitle = FindTextRecursive(courseCompleteCanvas.transform, "Title");
                if (courseCompleteSub == null) courseCompleteSub = FindTextRecursive(courseCompleteCanvas.transform, "Sub");
                var box = courseCompleteCanvas.transform.Find("ScoreBox");
                if (courseCompleteTotal == null && box != null) courseCompleteTotal = box.Find("Total")?.GetComponent<Text>();
                if (courseCompleteTotalLabel == null && box != null) courseCompleteTotalLabel = box.Find("Label")?.GetComponent<Text>();
                if (courseCompleteButtonLabel == null) courseCompleteButtonLabel = FindTextRecursive(courseCompleteCanvas.transform, "Label"); // PlayAgain/Label
                // More specific: PlayAgain/Label
                var btnLabel = courseCompleteCanvas.transform.Find("PlayAgain/Label")?.GetComponent<Text>();
                if (btnLabel != null) courseCompleteButtonLabel = btnLabel;
                if (playAgainButton == null) playAgainButton = courseCompleteCanvas.transform.Find("PlayAgain")?.GetComponent<Button>();
            }
        }

        private Text FindTextRecursive(Transform root, string name)
        {
            var t = root.Find(name);
            if (t != null)
            {
                var txt = t.GetComponent<Text>();
                if (txt != null) return txt;
            }
            // Deep search
            foreach (Transform child in root)
            {
                var found = FindTextRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void ApplyScaling()
        {
            if (gamePlayCanvas != null) gamePlayCanvas.transform.localScale = Vector3.one * gamePlayScale * globalScale;
            if (powerCanvas != null) powerCanvas.transform.localScale = Vector3.one * powerScale * globalScale;
            if (feedbackCanvas != null) feedbackCanvas.transform.localScale = Vector3.one * feedbackScale * globalScale;
            if (courseCompleteCanvas != null) courseCompleteCanvas.transform.localScale = Vector3.one * courseCompleteScale * globalScale;
        }

        private void ApplyLayout()
        {
            if (gamePlayCanvas != null)
            {
                gamePlayCanvas.transform.localPosition = gamePlayCardPosition;
                var rt = gamePlayCanvas.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = gamePlayCardSize;
            }
            if (powerCanvas != null)
            {
                powerCanvas.transform.localPosition = powerMeterPosition;
                var rt = powerCanvas.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = powerMeterSize;
            }
            if (feedbackCanvas != null)
            {
                feedbackCanvas.transform.localPosition = feedbackToastPosition;
                var rt = feedbackCanvas.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = feedbackToastSize;
            }
            if (courseCompleteCanvas != null)
            {
                courseCompleteCanvas.transform.localPosition = courseCompletePosition;
                var rt = courseCompleteCanvas.GetComponent<RectTransform>();
                if (rt != null) rt.sizeDelta = courseCompleteSize;
            }
            ApplyGroupPositions();
        }

        private void ApplyGroupPositions()
        {
            if (gamePlayCanvas == null) return;
            var idRt = gamePlayCanvas.transform.Find("IdentityGroup") as RectTransform;
            if (idRt != null) idRt.anchoredPosition = identityGroupPosition;
            var statsRt = gamePlayCanvas.transform.Find("StatsGroup") as RectTransform;
            if (statsRt != null) statsRt.anchoredPosition = statsGroupPosition;
            var progRt = gamePlayCanvas.transform.Find("ProgressGroup") as RectTransform;
            if (progRt != null) progRt.anchoredPosition = progressGroupPosition;
        }

        // Save hierarchy edits back to component fields — call from Inspector button or ContextMenu
        public void SaveLayoutFromHierarchy()
        {
            if (gamePlayCanvas != null)
            {
                gamePlayCardPosition = gamePlayCanvas.transform.localPosition;
                var rt = gamePlayCanvas.GetComponent<RectTransform>();
                if (rt != null) gamePlayCardSize = rt.sizeDelta;
            }
            if (powerCanvas != null)
            {
                powerMeterPosition = powerCanvas.transform.localPosition;
                var rt = powerCanvas.GetComponent<RectTransform>();
                if (rt != null) powerMeterSize = rt.sizeDelta;
            }
            if (feedbackCanvas != null)
            {
                feedbackToastPosition = feedbackCanvas.transform.localPosition;
                var rt = feedbackCanvas.GetComponent<RectTransform>();
                if (rt != null) feedbackToastSize = rt.sizeDelta;
            }
            if (courseCompleteCanvas != null)
            {
                courseCompletePosition = courseCompleteCanvas.transform.localPosition;
                var rt = courseCompleteCanvas.GetComponent<RectTransform>();
                if (rt != null) courseCompleteSize = rt.sizeDelta;
            }
            SaveGroupPositionsFromHierarchy();
        }

        public void SaveGroupPositionsFromHierarchy()
        {
            if (gamePlayCanvas == null) return;
            var idRt = gamePlayCanvas.transform.Find("IdentityGroup") as RectTransform;
            if (idRt != null) identityGroupPosition = idRt.anchoredPosition;
            var statsRt = gamePlayCanvas.transform.Find("StatsGroup") as RectTransform;
            if (statsRt != null) statsGroupPosition = statsRt.anchoredPosition;
            var progRt = gamePlayCanvas.transform.Find("ProgressGroup") as RectTransform;
            if (progRt != null) progressGroupPosition = progRt.anchoredPosition;
        }

        [ContextMenu("Save Hierarchy Positions/Sizes to Component")]
        public void SaveHierarchyToComponent()
        {
            SaveLayoutFromHierarchy();
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log($"[VRGolfUI] Saved hierarchy → component: GamePlay {gamePlayCardPosition} {gamePlayCardSize}, Power {powerMeterPosition} {powerMeterSize}, Feedback {feedbackToastPosition} {feedbackToastSize}, Complete {courseCompletePosition} {courseCompleteSize}, Groups I{identityGroupPosition} S{statsGroupPosition} P{progressGroupPosition}");
#endif
        }

        [ContextMenu("Save GamePlay Groups From Hierarchy")]
        public void SaveGroupsToComponent()
        {
            SaveGroupPositionsFromHierarchy();
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            Debug.Log($"[VRGolfUI] Saved groups: Identity {identityGroupPosition}, Stats {statsGroupPosition}, Progress {progressGroupPosition}");
#endif
        }

        private void ApplyFontSizes()
        {
            // Guard against null fonts
            if (identityTitle != null) identityTitle.fontSize = Mathf.RoundToInt(titleFontSize * fontScale);
            if (identityCourse != null) identityCourse.fontSize = Mathf.RoundToInt(courseFontSize * fontScale);
            if (identityLevel != null) identityLevel.fontSize = Mathf.RoundToInt(levelFontSize * fontScale);
            if (statsLabelStrokes != null) statsLabelStrokes.fontSize = Mathf.RoundToInt(labelFontSize * fontScale);
            if (statsLabelPar != null) statsLabelPar.fontSize = Mathf.RoundToInt(labelFontSize * fontScale);
            if (statsStrokes != null) statsStrokes.fontSize = Mathf.RoundToInt(strokesFontSize * fontScale);
            if (statsPar != null) statsPar.fontSize = Mathf.RoundToInt(parFontSize * fontScale);
            if (progressLabel != null) progressLabel.fontSize = Mathf.RoundToInt(progressLabelFontSize * fontScale);
            if (powerLabel != null) powerLabel.fontSize = Mathf.RoundToInt(powerLabelFontSize * fontScale);
            if (powerPercent != null) powerPercent.fontSize = Mathf.RoundToInt(powerPercentFontSize * fontScale);
            if (feedbackText != null) feedbackText.fontSize = Mathf.RoundToInt(feedbackFontSize * fontScale);
            if (courseCompleteTitle != null) courseCompleteTitle.fontSize = Mathf.RoundToInt(completeTitleFontSize * fontScale);
            if (courseCompleteSub != null) courseCompleteSub.fontSize = Mathf.RoundToInt(completeSubFontSize * fontScale);
            if (courseCompleteTotal != null) courseCompleteTotal.fontSize = Mathf.RoundToInt(completeTotalFontSize * fontScale);
            if (courseCompleteTotalLabel != null) courseCompleteTotalLabel.fontSize = Mathf.RoundToInt(completeTotalLabelFontSize * fontScale);
            if (courseCompleteButtonLabel != null) courseCompleteButtonLabel.fontSize = Mathf.RoundToInt(completeButtonFontSize * fontScale);
        }

        /// <summary>Call from editor button or context menu to force rebuild at current scales.</summary>
        [ContextMenu("Rebuild VR UI Preview")]
        public void RebuildPreview()
        {
            CacheReferences();
            EnsureCanvases(forceRecreate: false);
            BindExistingReferences();
            ApplyScaling();
            ApplyLayout();
            ApplyFontSizes();
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            if (_uiRoot != null) EditorUtility.SetDirty(_uiRoot);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        [ContextMenu("Remove Preview Canvases (VR_UI)")]
        public void RemovePreviewCanvases()
        {
            if (vrCourseAnchor == null) CacheReferences();
            Transform uiRoot = vrCourseAnchor != null ? vrCourseAnchor.Find("VR_UI") : null;
            if (uiRoot != null)
            {
#if UNITY_EDITOR
                Undo.DestroyObjectImmediate(uiRoot.gameObject);
#else
                DestroyImmediate(uiRoot.gameObject);
#endif
            }
            gamePlayCanvas = powerCanvas = feedbackCanvas = courseCompleteCanvas = null;
            identityCanvas = statsCanvas = progressCanvas = null;
            identityTitle = identityCourse = identityLevel = null;
            statsLabelStrokes = statsLabelPar = statsStrokes = statsPar = null;
            progressLabel = null;
            progressPips = null;
            powerLabel = powerPercent = null;
            powerSegments = null;
            feedbackText = null;
            feedbackGroup = null;
            courseCompleteTitle = courseCompleteSub = courseCompleteTotal = courseCompleteTotalLabel = courseCompleteButtonLabel = null;
            playAgainButton = null;
            _uiRoot = null;
        }

        private void EnsureCanvases(bool forceRecreate = false)
        {
            if (vrCourseAnchor == null) return;

            Transform uiRoot = vrCourseAnchor.Find("VR_UI");
            // Migrate / clean old separate canvases if they exist
            if (uiRoot != null)
            {
                // Destroy deprecated separate canvases if still present
                var oldIdentity = uiRoot.Find("IdentityCard");
                if (oldIdentity != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) Undo.DestroyObjectImmediate(oldIdentity.gameObject);
                    else DestroyImmediate(oldIdentity.gameObject);
#else
                    DestroyImmediate(oldIdentity.gameObject);
#endif
                    identityCanvas = null;
                }
                var oldStats = uiRoot.Find("StatsCard");
                if (oldStats != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) Undo.DestroyObjectImmediate(oldStats.gameObject);
                    else DestroyImmediate(oldStats.gameObject);
#else
                    DestroyImmediate(oldStats.gameObject);
#endif
                    statsCanvas = null;
                }
                var oldProgress = uiRoot.Find("ProgressCard");
                if (oldProgress != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) Undo.DestroyObjectImmediate(oldProgress.gameObject);
                    else DestroyImmediate(oldProgress.gameObject);
#else
                    DestroyImmediate(oldProgress.gameObject);
#endif
                    progressCanvas = null;
                }
                // Clear deprecated serialized refs if they point to destroyed objects
                if (identityCanvas != null && identityCanvas.transform.parent != uiRoot) identityCanvas = null;
                if (statsCanvas != null && statsCanvas.transform.parent != uiRoot) statsCanvas = null;
                if (progressCanvas != null && progressCanvas.transform.parent != uiRoot) progressCanvas = null;
            }

            if (uiRoot != null)
            {
                if (gamePlayCanvas == null) gamePlayCanvas = uiRoot.Find("GamePlayCard")?.GetComponent<Canvas>();
                if (powerCanvas == null) powerCanvas = uiRoot.Find("PowerMeter")?.GetComponent<Canvas>();
                if (feedbackCanvas == null) feedbackCanvas = uiRoot.Find("FeedbackToast")?.GetComponent<Canvas>();
                if (courseCompleteCanvas == null) courseCompleteCanvas = uiRoot.Find("CourseComplete")?.GetComponent<Canvas>();
            }

            if (uiRoot == null)
            {
                GameObject root = new GameObject("VR_UI");
#if UNITY_EDITOR
                if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(root, "Create VR_UI");
#endif
                root.transform.SetParent(vrCourseAnchor, false);
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                uiRoot = root.transform;
                _uiRoot = uiRoot;
            }
            else
            {
                _uiRoot = uiRoot;
            }

            if (gamePlayCanvas == null) CreateGamePlayCard(uiRoot);
            if (powerCanvas == null) CreatePower(uiRoot);
            if (feedbackCanvas == null) CreateFeedback(uiRoot);
            if (courseCompleteCanvas == null) CreateCourseComplete(uiRoot);
        }

        private Canvas CreateWorldCanvas(Transform parent, string name, Vector3 localPos, Vector2 size, float scale)
        {
            float finalScale = scale * globalScale;
            GameObject go = new GameObject(name);
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
#endif
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * finalScale;
            Canvas c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.worldCamera = Camera.main;
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig != null && rig.centerEyeAnchor != null)
            {
                var cc = rig.centerEyeAnchor.GetComponent<Camera>();
                if (cc != null) c.worldCamera = cc;
            }
            c.sortingOrder = 10;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10;
            scaler.referencePixelsPerUnit = 100;

            go.AddComponent<GraphicRaycaster>();

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.pivot = new Vector2(0.5f, 0.5f);

            GameObject bg = new GameObject("BG");
            bg.transform.SetParent(go.transform, false);
            Image bgImg = bg.AddComponent<Image>();
            bgImg.color = PanelColor;
            RectTransform bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero;
            bgRt.offsetMax = Vector2.zero;

            return c;
        }

        private Text CreateText(Transform parent, string name, string content, int fontSize, FontStyle style, Color color, TextAnchor anchor, Vector2 anchoredPos, Vector2 size)
        {
            int scaledSize = Mathf.RoundToInt(fontSize * fontScale);
            GameObject go = new GameObject(name);
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(go, $"Create Text {name}");
#endif
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = content;
            t.fontSize = scaledSize;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform rt = t.rectTransform;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
            return t;
        }

        private void CreateGamePlayCard(Transform root)
        {
            // Use exposed position/size so user can control per-component layout
            gamePlayCanvas = CreateWorldCanvas(root, "GamePlayCard", gamePlayCardPosition, gamePlayCardSize, gamePlayScale);
            // Hide full-canvas background — use per-group panels instead so combined HUD doesn't become a massive wall (see screenshot)
            var bg = gamePlayCanvas.transform.Find("BG")?.GetComponent<Image>();
            if (bg != null)
            {
                // Make fully transparent; keep object for layout but not visible
                bg.color = new Color(0, 0, 0, 0);
                bg.raycastTarget = false;
            }

            // Create groups to preserve original relative layout while fitting single canvas
            // Groups are centered at positions that map original world offsets into canvas pixels
            // Original Identity at -1.1m -> -500px, Stats at +1.1m -> +500px, Progress at 0, +0.10m -> +45px up

            // Identity Group at (-500, -12) — give its own panel like original IdentityCard
            GameObject idGroup = new GameObject("IdentityGroup");
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(idGroup, "Create IdentityGroup");
#endif
            idGroup.transform.SetParent(gamePlayCanvas.transform, false);
            RectTransform idRt = idGroup.AddComponent<RectTransform>();
            idRt.anchorMin = idRt.anchorMax = new Vector2(0.5f, 0.5f);
            idRt.pivot = new Vector2(0.5f, 0.5f);
            idRt.anchoredPosition = identityGroupPosition;
            idRt.sizeDelta = new Vector2(520, 140);
            GameObject idBg = new GameObject("BG");
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(idBg, "Create Identity BG");
#endif
            idBg.transform.SetParent(idGroup.transform, false);
            idBg.transform.SetAsFirstSibling();
            Image idBgImg = idBg.AddComponent<Image>();
            idBgImg.color = PanelColor;
            RectTransform idBgRt = idBgImg.rectTransform;
            idBgRt.anchorMin = Vector2.zero;
            idBgRt.anchorMax = Vector2.one;
            idBgRt.offsetMin = Vector2.zero;
            idBgRt.offsetMax = Vector2.zero;

            identityTitle = CreateText(idGroup.transform, "Title", "MINIMAL GOLF", titleFontSize, FontStyle.Bold, PaleText, TextAnchor.MiddleLeft, new Vector2(0, 30), new Vector2(460, 40));
            identityCourse = CreateText(idGroup.transform, "Course", "THE WARM UP", courseFontSize, FontStyle.Bold, Accent, TextAnchor.MiddleLeft, new Vector2(-100, 5), new Vector2(240, 20));
            identityLevel = CreateText(idGroup.transform, "Level", "LEVEL 1 / 8", levelFontSize, FontStyle.Bold, new Color(PaleText.r, PaleText.g, PaleText.b, 0.62f), TextAnchor.MiddleRight, new Vector2(110, 5), new Vector2(200, 20));
            GameObject bar = new GameObject("Bar");
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(bar, "Create Bar");
#endif
            bar.transform.SetParent(idGroup.transform, false);
            Image barImg = bar.AddComponent<Image>();
            barImg.color = Orange;
            RectTransform barRt = barImg.rectTransform;
            barRt.anchoredPosition = new Vector2(-230, 0);
            barRt.sizeDelta = new Vector2(8, 90);

            // Stats Group at (+480, -12) — own panel like original StatsCard
            GameObject statsGroup = new GameObject("StatsGroup");
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(statsGroup, "Create StatsGroup");
#endif
            statsGroup.transform.SetParent(gamePlayCanvas.transform, false);
            RectTransform statsRt = statsGroup.AddComponent<RectTransform>();
            statsRt.anchorMin = statsRt.anchorMax = new Vector2(0.5f, 0.5f);
            statsRt.pivot = new Vector2(0.5f, 0.5f);
            statsRt.anchoredPosition = statsGroupPosition;
            statsRt.sizeDelta = new Vector2(380, 140);
            GameObject statsBg = new GameObject("BG");
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(statsBg, "Create Stats BG");
#endif
            statsBg.transform.SetParent(statsGroup.transform, false);
            statsBg.transform.SetAsFirstSibling();
            Image statsBgImg = statsBg.AddComponent<Image>();
            statsBgImg.color = PanelColor;
            RectTransform statsBgRt = statsBgImg.rectTransform;
            statsBgRt.anchorMin = Vector2.zero;
            statsBgRt.anchorMax = Vector2.one;
            statsBgRt.offsetMin = Vector2.zero;
            statsBgRt.offsetMax = Vector2.zero;

            statsLabelStrokes = CreateText(statsGroup.transform, "LabelStrokes", "STROKES", labelFontSize, FontStyle.Bold, new Color(PaleText.r, PaleText.g, PaleText.b, 0.58f), TextAnchor.MiddleCenter, new Vector2(-80, 30), new Vector2(140, 20));
            statsStrokes = CreateText(statsGroup.transform, "Strokes", "0", strokesFontSize, FontStyle.Bold, Orange, TextAnchor.MiddleCenter, new Vector2(-80, -5), new Vector2(140, 50));
            statsLabelPar = CreateText(statsGroup.transform, "LabelPar", "PAR", labelFontSize, FontStyle.Bold, new Color(PaleText.r, PaleText.g, PaleText.b, 0.58f), TextAnchor.MiddleCenter, new Vector2(80, 30), new Vector2(140, 20));
            statsPar = CreateText(statsGroup.transform, "Par", "2", parFontSize, FontStyle.Bold, Gold, TextAnchor.MiddleCenter, new Vector2(80, -5), new Vector2(140, 50));
            GameObject div = new GameObject("Divider");
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(div, "Create Divider");
#endif
            div.transform.SetParent(statsGroup.transform, false);
            Image divImg = div.AddComponent<Image>();
            divImg.color = new Color(PaleText.r, PaleText.g, PaleText.b, 0.13f);
            RectTransform divRt = divImg.rectTransform;
            divRt.anchoredPosition = Vector2.zero;
            divRt.sizeDelta = new Vector2(2, 80);

            // Progress Group at (0, 48) - slightly above
            GameObject progGroup = new GameObject("ProgressGroup");
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(progGroup, "Create ProgressGroup");
#endif
            progGroup.transform.SetParent(gamePlayCanvas.transform, false);
            RectTransform progRt = progGroup.AddComponent<RectTransform>();
            progRt.anchorMin = progRt.anchorMax = new Vector2(0.5f, 0.5f);
            progRt.pivot = new Vector2(0.5f, 0.5f);
            progRt.anchoredPosition = progressGroupPosition;
            progRt.sizeDelta = new Vector2(420, 90);
            // Soften background for progress section? Add subtle panel
            GameObject progBg = new GameObject("ProgressBG");
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(progBg, "Create ProgressBG");
#endif
            progBg.transform.SetParent(progGroup.transform, false);
            progBg.transform.SetAsFirstSibling();
            Image progBgImg = progBg.AddComponent<Image>();
            progBgImg.color = PanelSoft;
            RectTransform progBgRt = progBgImg.rectTransform;
            progBgRt.anchorMin = Vector2.zero;
            progBgRt.anchorMax = Vector2.one;
            progBgRt.offsetMin = Vector2.zero;
            progBgRt.offsetMax = Vector2.zero;

            progressLabel = CreateText(progGroup.transform, "ProgressLabel", "COURSE PROGRESS", progressLabelFontSize, FontStyle.Bold, new Color(PaleText.r, PaleText.g, PaleText.b, 0.58f), TextAnchor.MiddleCenter, new Vector2(0, 22), new Vector2(300, 20));
            int count = 8;
            progressPips = new Image[count];
            float gap = 6f;
            float w = 18f;
            float total = count * w + (count - 1) * gap;
            float startX = -total * 0.5f + w * 0.5f;
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject($"Pip{i}");
#if UNITY_EDITOR
                if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(go, $"Create Pip{i}");
#endif
                go.transform.SetParent(progGroup.transform, false);
                Image img = go.AddComponent<Image>();
                img.color = new Color(PaleText.r, PaleText.g, PaleText.b, 0.18f);
                RectTransform rt = img.rectTransform;
                rt.anchoredPosition = new Vector2(startX + i * (w + gap), -10);
                rt.sizeDelta = new Vector2(w, 8);
                progressPips[i] = img;
            }
        }

        private void CreatePower(Transform root)
        {
            powerCanvas = CreateWorldCanvas(root, "PowerMeter", powerMeterPosition, powerMeterSize, powerScale);
            powerCanvas.sortingOrder = 20;
            powerLabel = CreateText(powerCanvas.transform, "Label", "PUTT STRENGTH", powerLabelFontSize, FontStyle.Bold, new Color(PaleText.r, PaleText.g, PaleText.b, 0.62f), TextAnchor.MiddleLeft, new Vector2(-200, 30), new Vector2(200, 20));
            powerPercent = CreateText(powerCanvas.transform, "Percent", "0%", powerPercentFontSize, FontStyle.Bold, PaleText, TextAnchor.MiddleRight, new Vector2(200, 30), new Vector2(120, 20));
            int seg = 12;
            powerSegments = new Image[seg];
            float gap = 4f;
            float totalW = 500f;
            float segW = (totalW - gap * (seg - 1)) / seg;
            float startX = -totalW * 0.5f + segW * 0.5f;
            for (int i = 0; i < seg; i++)
            {
                GameObject go = new GameObject($"Seg{i}");
#if UNITY_EDITOR
                if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(go, $"Create Seg{i}");
#endif
                go.transform.SetParent(powerCanvas.transform, false);
                Image img = go.AddComponent<Image>();
                img.color = new Color(PaleText.r, PaleText.g, PaleText.b, 0.13f);
                RectTransform rt = img.rectTransform;
                rt.anchoredPosition = new Vector2(startX + i * (segW + gap), -10);
                rt.sizeDelta = new Vector2(segW, 18);
                powerSegments[i] = img;
            }
            powerCanvas.gameObject.SetActive(false);
        }

        private void CreateFeedback(Transform root)
        {
            feedbackCanvas = CreateWorldCanvas(root, "FeedbackToast", feedbackToastPosition, feedbackToastSize, feedbackScale);
            feedbackCanvas.sortingOrder = 30;
            var bg = feedbackCanvas.transform.Find("BG")?.GetComponent<Image>();
            if (bg != null) bg.color = new Color(WarmCream.r, WarmCream.g, WarmCream.b, 0.96f);
            feedbackText = CreateText(feedbackCanvas.transform, "Feedback", "BALL RETURNED", feedbackFontSize, FontStyle.Bold, Ink, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(500, 50));
            feedbackGroup = feedbackCanvas.gameObject.AddComponent<CanvasGroup>();
            feedbackCanvas.gameObject.SetActive(false);
        }

        private void CreateCourseComplete(Transform root)
        {
            courseCompleteCanvas = CreateWorldCanvas(root, "CourseComplete", courseCompletePosition, courseCompleteSize, courseCompleteScale);
            courseCompleteCanvas.sortingOrder = 40;
            var bg = courseCompleteCanvas.transform.Find("BG")?.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.075f, 0.19f, 0.22f, 0.98f);
            courseCompleteTitle = CreateText(courseCompleteCanvas.transform, "Title", "COURSE COMPLETE", completeTitleFontSize, FontStyle.Bold, PaleText, TextAnchor.MiddleCenter, new Vector2(0, 110), new Vector2(700, 60));
            courseCompleteSub = CreateText(courseCompleteCanvas.transform, "Sub", "EIGHT SMALL COURSES • ONE GRAND SCORE", completeSubFontSize, FontStyle.Bold, new Color(PaleText.r, PaleText.g, PaleText.b, 0.58f), TextAnchor.MiddleCenter, new Vector2(0, 70), new Vector2(700, 20));
            GameObject box = new GameObject("ScoreBox");
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(box, "Create ScoreBox");
#endif
            box.transform.SetParent(courseCompleteCanvas.transform, false);
            Image boxImg = box.AddComponent<Image>();
            boxImg.color = WarmCream;
            RectTransform boxRt = boxImg.rectTransform;
            boxRt.anchoredPosition = new Vector2(0, -10);
            boxRt.sizeDelta = new Vector2(260, 90);
            courseCompleteTotal = CreateText(box.transform, "Total", "0", completeTotalFontSize, FontStyle.Bold, Ink, TextAnchor.MiddleCenter, new Vector2(0, 10), new Vector2(200, 40));
            courseCompleteTotalLabel = CreateText(box.transform, "Label", "TOTAL STROKES", completeTotalLabelFontSize, FontStyle.Bold, Ink, TextAnchor.MiddleCenter, new Vector2(0, -22), new Vector2(200, 20));
            GameObject btnGO = new GameObject("PlayAgain");
#if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RegisterCreatedObjectUndo(btnGO, "Create PlayAgain");
#endif
            btnGO.transform.SetParent(courseCompleteCanvas.transform, false);
            Image btnImg = btnGO.AddComponent<Image>();
            btnImg.color = Orange;
            Button btn = btnGO.AddComponent<Button>();
            RectTransform btnRt = btnImg.rectTransform;
            btnRt.anchoredPosition = new Vector2(0, -140);
            btnRt.sizeDelta = new Vector2(340, 50);
            playAgainButton = btn;
            courseCompleteButtonLabel = CreateText(btnGO.transform, "Label", "PLAY AGAIN  •  TRIGGER", completeButtonFontSize, FontStyle.Bold, Ink, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(300, 30));
            btn.onClick.AddListener(() => game?.SendMessage("RestartCourse", SendMessageOptions.DontRequireReceiver));
            courseCompleteCanvas.gameObject.SetActive(false);
        }

        private void UpdateIdentity()
        {
            if (identityTitle == null || game.CurrentLevel == null) return;
            identityCourse.text = game.CurrentLevel.levelName;
            identityLevel.text = $"LEVEL {game.CurrentLevelIndex + 1} / {game.AllLevels.Length}";
        }

        private void UpdateStats()
        {
            if (statsStrokes == null || game.CurrentLevel == null) return;
            statsStrokes.text = game.LevelStrokes.ToString();
            statsPar.text = game.CurrentLevel.par.ToString();
        }

        private void UpdateProgress()
        {
            if (progressPips == null || game.AllLevels == null) return;
            for (int i = 0; i < progressPips.Length && i < game.AllLevels.Length; i++)
            {
                if (progressPips[i] == null) continue;
                bool current = i == game.CurrentLevelIndex;
                float h = current ? 10f : 6f;
                Color col;
                if (i < game.CurrentLevelIndex) col = Seafoam;
                else if (current) col = Orange;
                else col = new Color(PaleText.r, PaleText.g, PaleText.b, 0.18f);
                progressPips[i].color = col;
                var rt = progressPips[i].rectTransform;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, h);
            }
        }

        private void UpdatePower()
        {
            if (powerCanvas == null) return;
            bool show = game.IsAiming;
            powerCanvas.gameObject.SetActive(show);
            if (!show) return;
            float power = game.ShotPower;
            if (powerPercent != null) powerPercent.text = Mathf.RoundToInt(power * 100f) + "%";
            Color col = power < 0.55f ? Color.Lerp(Seafoam, Gold, power / 0.55f) : Color.Lerp(Gold, Orange, (power - 0.55f) / 0.45f);
            if (powerSegments == null) return;
            for (int i = 0; i < powerSegments.Length; i++)
            {
                if (powerSegments[i] == null) continue;
                bool filled = power >= (i + 1f) / powerSegments.Length;
                powerSegments[i].color = filled ? col : new Color(PaleText.r, PaleText.g, PaleText.b, 0.13f);
            }
        }

        private void UpdateFeedback()
        {
            if (feedbackCanvas == null || feedbackGroup == null) return;
            if (game == null) return;
            bool show = Time.unscaledTime < game.FeedbackUntil && !string.IsNullOrEmpty(game.CurrentFeedback);
            feedbackCanvas.gameObject.SetActive(show);
            if (!show) return;
            if (feedbackText != null) feedbackText.text = game.CurrentFeedback;
            float alpha = Mathf.Clamp01((game.FeedbackUntil - Time.unscaledTime) * 3.2f);
            feedbackGroup.alpha = alpha;
        }

        private void UpdateCourseComplete()
        {
            if (courseCompleteCanvas == null) return;
            if (game == null) return;
            bool show = game.IsCourseComplete;
            courseCompleteCanvas.gameObject.SetActive(show);
            if (!show) return;
            if (courseCompleteTotal != null) courseCompleteTotal.text = game.TotalStrokes.ToString();
        }
    }
}
