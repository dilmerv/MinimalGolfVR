using UnityEngine;
using UnityEngine.UI;

namespace MinimalGolf
{
    /// <summary>
    /// Drives World Space Canvases for VR. Replaces the old OnGUI Draw* methods.
    /// Canvases are created dynamically under VRCourseAnchor if not assigned.
    /// Keeps GetUIValues contract for tests; UI is visual only.
    /// </summary>
    public sealed class VRGolfUI : MonoBehaviour
    {
        public MinimalGolfGame game;
        public Font uiFont;
        public Transform vrCourseAnchor;

        [Header("World Space Canvas Refs (auto-created if null)")]
        public Canvas identityCanvas;
        public Canvas statsCanvas;
        public Canvas progressCanvas;
        public Canvas powerCanvas;
        public Canvas feedbackCanvas;
        public Canvas courseCompleteCanvas;

        private Text identityTitle;
        private Text identityCourse;
        private Text identityLevel;
        private Text statsStrokes;
        private Text statsPar;
        private Image[] progressPips;
        private Text powerPercent;
        private Image[] powerSegments;
        private Text feedbackText;
        private CanvasGroup feedbackGroup;
        private Text courseCompleteTotal;
        private Button playAgainButton;

        private static readonly Color PanelColor = new Color(0.055f, 0.14f, 0.19f, 0.90f);
        private static readonly Color PanelSoft = new Color(0.055f, 0.14f, 0.19f, 0.72f);
        private static readonly Color PaleText = new Color32(0xFA, 0xF1, 0xD2, 0xFF);
        private static readonly Color Accent = new Color32(0xA9, 0xDB, 0xE8, 0xFF);
        private static readonly Color Orange = new Color32(0xE1, 0x82, 0x2F, 0xFF);
        private static readonly Color Seafoam = new Color32(0x8B, 0xCB, 0xA8, 0xFF);
        private static readonly Color Gold = new Color32(0xF3, 0xC9, 0x6B, 0xFF);
        private static readonly Color WarmCream = new Color32(0xFF, 0xED, 0xBF, 0xFF);
        private static readonly Color Ink = new Color32(0x16, 0x35, 0x3D, 0xFF);

        private void Awake()
        {
            if (game == null) game = FindFirstObjectByType<MinimalGolfGame>();
            if (vrCourseAnchor == null && game != null) vrCourseAnchor = game.vrCourseAnchor;
            if (vrCourseAnchor == null)
            {
                var rig = FindFirstObjectByType<OVRCameraRig>();
                if (rig != null && rig.trackingSpace != null)
                {
                    var t = rig.trackingSpace.Find("VRCourseAnchor");
                    if (t != null) vrCourseAnchor = t;
                }
            }
            if (uiFont == null && game != null) uiFont = game.uiFont;
        }

        private void Start()
        {
            EnsureCanvases();
        }

        private void Update()
        {
            if (game == null) return;
            EnsureCanvases();
            UpdateIdentity();
            UpdateStats();
            UpdateProgress();
            UpdatePower();
            UpdateFeedback();
            UpdateCourseComplete();
        }

        private void EnsureCanvases()
        {
            if (vrCourseAnchor == null) return;
            Transform uiRoot = vrCourseAnchor.Find("VR_UI");
            if (uiRoot == null)
            {
                GameObject root = new GameObject("VR_UI");
                root.transform.SetParent(vrCourseAnchor, false);
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                uiRoot = root.transform;
            }

            if (identityCanvas == null) CreateIdentity(uiRoot);
            if (statsCanvas == null) CreateStats(uiRoot);
            if (progressCanvas == null) CreateProgress(uiRoot);
            if (powerCanvas == null) CreatePower(uiRoot);
            if (feedbackCanvas == null) CreateFeedback(uiRoot);
            if (courseCompleteCanvas == null) CreateCourseComplete(uiRoot);
        }

        private Canvas CreateWorldCanvas(Transform parent, string name, Vector3 localPos, Vector2 size, float scale = 0.0025f)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * scale;
            Canvas c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.worldCamera = Camera.main;
            // Try center eye
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

            // Background panel
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
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text t = go.AddComponent<Text>();
            t.font = uiFont != null ? uiFont : Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = content;
            t.fontSize = fontSize;
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

        private void CreateIdentity(Transform root)
        {
            identityCanvas = CreateWorldCanvas(root, "IdentityCard", new Vector3(-1.1f, 0.55f, -0.7f), new Vector2(520, 140), 0.0022f);
            // Title
            identityTitle = CreateText(identityCanvas.transform, "Title", "MINIMAL GOLF", 22, FontStyle.Bold, PaleText, TextAnchor.MiddleLeft, new Vector2(0, 30), new Vector2(460, 40));
            identityCourse = CreateText(identityCanvas.transform, "Course", "THE WARM UP", 13, FontStyle.Bold, Accent, TextAnchor.MiddleLeft, new Vector2(-100, 5), new Vector2(240, 20));
            identityLevel = CreateText(identityCanvas.transform, "Level", "LEVEL 1 / 8", 9, FontStyle.Bold, new Color(PaleText.r, PaleText.g, PaleText.b, 0.62f), TextAnchor.MiddleRight, new Vector2(110, 5), new Vector2(200, 20));
            // accent bar
            GameObject bar = new GameObject("Bar");
            bar.transform.SetParent(identityCanvas.transform, false);
            Image img = bar.AddComponent<Image>();
            img.color = Orange;
            RectTransform rt = img.rectTransform;
            rt.anchoredPosition = new Vector2(-230, 0);
            rt.sizeDelta = new Vector2(8, 90);
        }

        private void CreateStats(Transform root)
        {
            statsCanvas = CreateWorldCanvas(root, "StatsCard", new Vector3(1.1f, 0.55f, -0.7f), new Vector2(380, 140), 0.0022f);
            // STROKES
            CreateText(statsCanvas.transform, "LabelStrokes", "STROKES", 8, FontStyle.Bold, new Color(PaleText.r, PaleText.g, PaleText.b, 0.58f), TextAnchor.MiddleCenter, new Vector2(-80, 30), new Vector2(140, 20));
            statsStrokes = CreateText(statsCanvas.transform, "Strokes", "0", 28, FontStyle.Bold, Orange, TextAnchor.MiddleCenter, new Vector2(-80, -5), new Vector2(140, 50));
            // PAR
            CreateText(statsCanvas.transform, "LabelPar", "PAR", 8, FontStyle.Bold, new Color(PaleText.r, PaleText.g, PaleText.b, 0.58f), TextAnchor.MiddleCenter, new Vector2(80, 30), new Vector2(140, 20));
            statsPar = CreateText(statsCanvas.transform, "Par", "2", 28, FontStyle.Bold, Gold, TextAnchor.MiddleCenter, new Vector2(80, -5), new Vector2(140, 50));
            // divider
            GameObject div = new GameObject("Divider");
            div.transform.SetParent(statsCanvas.transform, false);
            Image img = div.AddComponent<Image>();
            img.color = new Color(PaleText.r, PaleText.g, PaleText.b, 0.13f);
            RectTransform rt = img.rectTransform;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(2, 80);
        }

        private void CreateProgress(Transform root)
        {
            progressCanvas = CreateWorldCanvas(root, "ProgressCard", new Vector3(0f, 0.75f, -0.7f), new Vector2(420, 90), 0.0022f);
            var bg = progressCanvas.transform.Find("BG")?.GetComponent<Image>();
            if (bg != null) bg.color = PanelSoft;
            CreateText(progressCanvas.transform, "Label", "COURSE PROGRESS", 8, FontStyle.Bold, new Color(PaleText.r, PaleText.g, PaleText.b, 0.58f), TextAnchor.MiddleCenter, new Vector2(0, 22), new Vector2(300, 20));
            // pips
            int count = 8;
            progressPips = new Image[count];
            float gap = 6f;
            float w = 18f;
            float total = count * w + (count - 1) * gap;
            float startX = -total * 0.5f + w * 0.5f;
            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject($"Pip{i}");
                go.transform.SetParent(progressCanvas.transform, false);
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
            powerCanvas = CreateWorldCanvas(root, "PowerMeter", new Vector3(0f, 0.15f, -0.2f), new Vector2(560, 110), 0.0025f);
            powerCanvas.sortingOrder = 20;
            CreateText(powerCanvas.transform, "Label", "PUTT STRENGTH", 9, FontStyle.Bold, new Color(PaleText.r, PaleText.g, PaleText.b, 0.62f), TextAnchor.MiddleLeft, new Vector2(-200, 30), new Vector2(200, 20));
            powerPercent = CreateText(powerCanvas.transform, "Percent", "0%", 9, FontStyle.Bold, PaleText, TextAnchor.MiddleRight, new Vector2(200, 30), new Vector2(120, 20));
            // segments
            int seg = 12;
            powerSegments = new Image[seg];
            float gap = 4f;
            float totalW = 500f;
            float segW = (totalW - gap * (seg - 1)) / seg;
            float startX = -totalW * 0.5f + segW * 0.5f;
            for (int i = 0; i < seg; i++)
            {
                GameObject go = new GameObject($"Seg{i}");
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
            feedbackCanvas = CreateWorldCanvas(root, "FeedbackToast", new Vector3(0f, 0.35f, 0.2f), new Vector2(560, 70), 0.0025f);
            feedbackCanvas.sortingOrder = 30;
            var bg = feedbackCanvas.transform.Find("BG")?.GetComponent<Image>();
            if (bg != null) bg.color = new Color(WarmCream.r, WarmCream.g, WarmCream.b, 0.96f);
            feedbackText = CreateText(feedbackCanvas.transform, "Feedback", "BALL RETURNED", 11, FontStyle.Bold, Ink, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(500, 50));
            feedbackGroup = feedbackCanvas.gameObject.AddComponent<CanvasGroup>();
            feedbackCanvas.gameObject.SetActive(false);
        }

        private void CreateCourseComplete(Transform root)
        {
            courseCompleteCanvas = CreateWorldCanvas(root, "CourseComplete", new Vector3(0f, 0.6f, 0.4f), new Vector2(760, 500), 0.0028f);
            courseCompleteCanvas.sortingOrder = 40;
            var bg = courseCompleteCanvas.transform.Find("BG")?.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.075f, 0.19f, 0.22f, 0.98f);
            CreateText(courseCompleteCanvas.transform, "Title", "COURSE COMPLETE", 32, FontStyle.Bold, PaleText, TextAnchor.MiddleCenter, new Vector2(0, 110), new Vector2(700, 60));
            CreateText(courseCompleteCanvas.transform, "Sub", "EIGHT SMALL COURSES • ONE GRAND SCORE", 8, FontStyle.Bold, new Color(PaleText.r, PaleText.g, PaleText.b, 0.58f), TextAnchor.MiddleCenter, new Vector2(0, 70), new Vector2(700, 20));
            // total strokes box
            GameObject box = new GameObject("ScoreBox");
            box.transform.SetParent(courseCompleteCanvas.transform, false);
            Image boxImg = box.AddComponent<Image>();
            boxImg.color = WarmCream;
            RectTransform boxRt = boxImg.rectTransform;
            boxRt.anchoredPosition = new Vector2(0, -10);
            boxRt.sizeDelta = new Vector2(260, 90);
            courseCompleteTotal = CreateText(box.transform, "Total", "0", 28, FontStyle.Bold, Ink, TextAnchor.MiddleCenter, new Vector2(0, 10), new Vector2(200, 40));
            CreateText(box.transform, "Label", "TOTAL STROKES", 8, FontStyle.Bold, Ink, TextAnchor.MiddleCenter, new Vector2(0, -22), new Vector2(200, 20));
            // Play again button
            GameObject btnGO = new GameObject("PlayAgain");
            btnGO.transform.SetParent(courseCompleteCanvas.transform, false);
            Image btnImg = btnGO.AddComponent<Image>();
            btnImg.color = Orange;
            Button btn = btnGO.AddComponent<Button>();
            RectTransform btnRt = btnImg.rectTransform;
            btnRt.anchoredPosition = new Vector2(0, -140);
            btnRt.sizeDelta = new Vector2(340, 50);
            playAgainButton = btn;
            CreateText(btnGO.transform, "Label", "PLAY AGAIN  •  TRIGGER", 11, FontStyle.Bold, Ink, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(300, 30));
            btn.onClick.AddListener(() => game?.SendMessage("RestartCourse", SendMessageOptions.DontRequireReceiver));
            // Also add collider for ray? For now button click via OVR ray not needed; trigger press will restart via MinimalGolfGame Update
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
            powerPercent.text = Mathf.RoundToInt(power * 100f) + "%";
            Color col = power < 0.55f ? Color.Lerp(Seafoam, Gold, power / 0.55f) : Color.Lerp(Gold, Orange, (power - 0.55f) / 0.45f);
            for (int i = 0; i < powerSegments.Length; i++)
            {
                bool filled = power >= (i + 1f) / powerSegments.Length;
                powerSegments[i].color = filled ? col : new Color(PaleText.r, PaleText.g, PaleText.b, 0.13f);
            }
        }

        private void UpdateFeedback()
        {
            if (feedbackCanvas == null || feedbackGroup == null) return;
            bool show = Time.unscaledTime < game.FeedbackUntil && !string.IsNullOrEmpty(game.CurrentFeedback);
            feedbackCanvas.gameObject.SetActive(show);
            if (!show) return;
            feedbackText.text = game.CurrentFeedback;
            float alpha = Mathf.Clamp01((game.FeedbackUntil - Time.unscaledTime) * 3.2f);
            feedbackGroup.alpha = alpha;
        }

        private void UpdateCourseComplete()
        {
            if (courseCompleteCanvas == null) return;
            bool show = game.IsCourseComplete;
            courseCompleteCanvas.gameObject.SetActive(show);
            if (!show) return;
            courseCompleteTotal.text = game.TotalStrokes.ToString();
        }
    }
}
