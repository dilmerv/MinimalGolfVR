using UnityEngine;
using UnityEngine.UI;

namespace MinimalGolf
{
    /// <summary>
    /// Simplified VR UI — Inspector-driven.
    /// All positions/scales/sizes are controlled by moving the RectTransforms in the hierarchy.
    /// This script only updates text/visibility (strokes, par, progress, power, feedback, course complete).
    /// Canvases are expected to already exist under VRCourseAnchor/VR_UI (created once in scene).
    /// </summary>
    public sealed class VRGolfUI : MonoBehaviour
    {
        [Header("References")]
        public MinimalGolfGame game;
        public Font uiFont; // kept for compatibility, not used for auto-layout
        public Transform vrCourseAnchor; // optional, auto-found
        public Transform vrUIAnchor; // optional, auto-found (now same as vrCourseAnchor/VR_UI parent)

        [Header("World Space Canvases (assign in Inspector, or auto-found)")]
        public Canvas gamePlayCanvas;
        public Canvas powerCanvas;
        public Canvas feedbackCanvas;
        public Canvas courseCompleteCanvas;

        // Internal bindings — found automatically if null
        private Text identityTitle;
        private Text identityCourse;
        private Text identityLevel;
        private Text statsLabelStrokes;
        private Text statsStrokes;
        private Text statsLabelPar;
        private Text statsPar;
        private Text progressLabel;
        private Image[] progressPips;
        private Text powerLabel;
        private Text powerPercent;
        private Image[] powerSegments;
        private Text feedbackText;
        private CanvasGroup feedbackGroup;
        private Text courseCompleteTitle;
        private Text courseCompleteSub;
        private Text courseCompleteTotal;
        private Text courseCompleteTotalLabel;
        private Text courseCompleteButtonLabel;
        private Button playAgainButton;

        private static readonly Color PanelColor = new Color(0.055f, 0.14f, 0.19f, 0.90f);
        private static readonly Color Seafoam = new Color32(0x8B, 0xCB, 0xA8, 0xFF);
        private static readonly Color Orange = new Color32(0xE1, 0x82, 0x2F, 0xFF);
        private static readonly Color Gold = new Color32(0xF3, 0xC9, 0x6B, 0xFF);
        private static readonly Color PaleText = new Color32(0xFA, 0xF1, 0xD2, 0xFF);

        private void Awake() => CacheReferences();
        private void OnEnable() => CacheReferences();
        private void Start() => BindExistingReferences();

        private void Update()
        {
            if (game == null) CacheReferences();
            BindExistingReferences();
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
            if (vrCourseAnchor == null) vrCourseAnchor = GameObject.Find("VRCourseAnchor")?.transform;
            if (vrUIAnchor == null && game != null) vrUIAnchor = game.vrUIAnchor;
            if (vrUIAnchor == null) vrUIAnchor = vrCourseAnchor;
            // Auto-find canvases if not assigned
            Transform uiRoot = vrCourseAnchor != null ? vrCourseAnchor.Find("VR_UI") : null;
            if (uiRoot == null && vrUIAnchor != null) uiRoot = vrUIAnchor.Find("VR_UI");
            if (uiRoot == null) uiRoot = GameObject.Find("VR_UI")?.transform;
            if (gamePlayCanvas == null && uiRoot != null) gamePlayCanvas = uiRoot.Find("GamePlayCard")?.GetComponent<Canvas>();
            if (powerCanvas == null && uiRoot != null) powerCanvas = uiRoot.Find("PowerMeter")?.GetComponent<Canvas>();
            if (feedbackCanvas == null && uiRoot != null) feedbackCanvas = uiRoot.Find("FeedbackToast")?.GetComponent<Canvas>();
            if (courseCompleteCanvas == null && uiRoot != null) courseCompleteCanvas = uiRoot.Find("CourseComplete")?.GetComponent<Canvas>();
        }

        private void BindExistingReferences()
        {
            if (gamePlayCanvas != null)
            {
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
                        if (pip == null) pip = gamePlayCanvas.transform.Find($"Pip{i}")?.GetComponent<Image>();
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
                var btnLabel = courseCompleteCanvas.transform.Find("PlayAgain/Label")?.GetComponent<Text>();
                if (btnLabel != null) courseCompleteButtonLabel = btnLabel;
                if (playAgainButton == null) playAgainButton = courseCompleteCanvas.transform.Find("PlayAgain")?.GetComponent<Button>();
                if (playAgainButton != null && playAgainButton.onClick.GetPersistentEventCount() == 0)
                    playAgainButton.onClick.AddListener(() => game?.SendMessage("RestartCourse", SendMessageOptions.DontRequireReceiver));
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
            foreach (Transform child in root)
            {
                var found = FindTextRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void UpdateIdentity()
        {
            if (identityCourse == null || game == null || game.CurrentLevel == null) return;
            identityCourse.text = game.CurrentLevel.levelName;
            if (identityLevel != null) identityLevel.text = $"LEVEL {game.CurrentLevelIndex + 1} / {game.AllLevels.Length}";
        }

        private void UpdateStats()
        {
            if (statsStrokes == null || game == null || game.CurrentLevel == null) return;
            statsStrokes.text = game.LevelStrokes.ToString();
            if (statsPar != null) statsPar.text = game.CurrentLevel.par.ToString();
        }

        private void UpdateProgress()
        {
            if (progressPips == null || game == null || game.AllLevels == null) return;
            for (int i = 0; i < progressPips.Length && i < game.AllLevels.Length; i++)
            {
                if (progressPips[i] == null) continue;
                bool current = i == game.CurrentLevelIndex;
                Color col;
                if (i < game.CurrentLevelIndex) col = Seafoam;
                else if (current) col = Orange;
                else col = new Color(PaleText.r, PaleText.g, PaleText.b, 0.18f);
                progressPips[i].color = col;
                var rt = progressPips[i].rectTransform;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, current ? 10f : 6f);
            }
        }

        private void UpdatePower()
        {
            if (powerCanvas == null || game == null) return;
            bool show = game.IsAiming;
            powerCanvas.gameObject.SetActive(show);
            if (!show) return;
            float power = game.ShotPower;
            if (powerLabel != null) powerLabel.text = "PUTT STRENGTH";
            if (powerPercent != null) powerPercent.text = Mathf.RoundToInt(power * 100f) + "%";
            if (powerSegments == null) return;
            Color col = power < 0.55f ? Color.Lerp(new Color32(0x89, 0xE0, 0xB3, 0xFF), new Color32(0xF3, 0xC9, 0x6B, 0xFF), power / 0.55f)
                                      : Color.Lerp(new Color32(0xF3, 0xC9, 0x6B, 0xFF), new Color32(0xE1, 0x82, 0x2F, 0xFF), (power - 0.55f) / 0.45f);
            for (int i = 0; i < powerSegments.Length; i++)
            {
                if (powerSegments[i] == null) continue;
                bool filled = power >= (i + 1f) / powerSegments.Length;
                powerSegments[i].color = filled ? col : new Color(PaleText.r, PaleText.g, PaleText.b, 0.13f);
            }
        }

        private void UpdateFeedback()
        {
            if (feedbackCanvas == null || feedbackGroup == null || game == null) return;
            bool show = Time.unscaledTime < game.FeedbackUntil && !string.IsNullOrEmpty(game.CurrentFeedback);
            feedbackCanvas.gameObject.SetActive(show);
            if (!show) return;
            if (feedbackText != null) feedbackText.text = game.CurrentFeedback;
            feedbackGroup.alpha = Mathf.Clamp01((game.FeedbackUntil - Time.unscaledTime) * 3.2f);
        }

        private void UpdateCourseComplete()
        {
            if (courseCompleteCanvas == null || game == null) return;
            bool show = game.IsCourseComplete;
            courseCompleteCanvas.gameObject.SetActive(show);
            if (!show) return;
            if (courseCompleteTotal != null) courseCompleteTotal.text = game.TotalStrokes.ToString();
        }
    }
}
