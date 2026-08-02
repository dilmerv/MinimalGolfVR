using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MinimalGolf
{
    public sealed class MinimalGolfGame : MonoBehaviour
    {
        [Header("Authored Scene References")]
        public MiniGolfLevel[] levels;
        public Camera gameCamera;
        public LineRenderer aimingLine;
        public Font uiFont;

        [Header("Shot Tuning")]
        [SerializeField] private float maximumImpulse = 7.6f;
        [SerializeField] private float maximumDragDistance = 3.1f;
        [SerializeField] private float playableSpeed = 0.32f;

        [Header("Cup Assist")]
        [SerializeField] private float assistRadius = 1.15f;
        [SerializeField] private float captureRadius = 0.48f;
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

        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;
        private GUIStyle centeredStyle;
        private GUIStyle hugeStyle;
        private GUIStyle eyebrowStyle;
        private GUIStyle statLabelStyle;
        private GUIStyle statValueStyle;
        private GUIStyle darkCenteredStyle;
        private GUIStyle roundedPanelStyle;
        private Texture2D whiteTexture;
        private Texture2D roundedTexture;

        private static readonly Color BackgroundColor = new Color32(0x77, 0x9E, 0xBE, 0xFF);
        private static readonly Color PanelColor = new Color(0.055f, 0.14f, 0.19f, 0.90f);
        private static readonly Color PanelSoftColor = new Color(0.055f, 0.14f, 0.19f, 0.72f);
        private static readonly Color PaleText = new Color32(0xFA, 0xF1, 0xD2, 0xFF);
        private static readonly Color Accent = new Color32(0xA9, 0xDB, 0xE8, 0xFF);
        private static readonly Color Coral = new Color32(0xE9, 0x84, 0x68, 0xFF);
        private static readonly Color Seafoam = new Color32(0x8B, 0xCB, 0xA8, 0xFF);
        private static readonly Color Ink = new Color32(0x16, 0x35, 0x3D, 0xFF);
        private static readonly Color WarmCream = new Color32(0xFF, 0xED, 0xBF, 0xFF);
        private static readonly Color Gold = new Color32(0xF3, 0xC9, 0x6B, 0xFF);

        public int CurrentLevelIndex => currentLevelIndex;

        private void Awake()
        {
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 1;
            whiteTexture = Texture2D.whiteTexture;
            roundedTexture = CreateRoundedTexture(32, 9f);

            if (levels == null || levels.Length == 0)
                levels = FindObjectsByType<MiniGolfLevel>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (MiniGolfLevel level in levels)
                level.CacheAuthoredState();

            if (gameCamera == null)
                gameCamera = Camera.main;

            if (aimingLine != null)
            {
                aimingLine.enabled = false;
                aimingLine.useWorldSpace = true;
                aimingLine.positionCount = 2;
                aimingLine.startWidth = 0.045f;
                aimingLine.endWidth = 0.045f;
            }
        }

        private void Start()
        {
            LoadLevel(0, true);
        }

        private void Update()
        {
            if (courseComplete)
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
                    RestartCourse();
                return;
            }

            if (currentLevel == null || levelComplete)
                return;

            if (currentLevel.IsRevealing)
                return;

            HandleKeyboard();
            HandlePointer();

            Rigidbody ball = currentLevel.ball;
            if (!capturing && currentLevel.IsOutsideCourse(ball.position))
            {
                ResetBall(false);
                ShowFeedback("BALL RETURNED");
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

        private void HandleKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.rKey.wasPressedThisFrame)
                ResetLevelWithPenalty();

            if (keyboard.leftArrowKey.wasPressedThisFrame)
                TryRotateLevel(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                TryRotateLevel(1);
        }

        private void HandlePointer()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || capturing)
                return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (!CanTakeAction())
                {
                    ShowFeedback("WAIT FOR THE BALL");
                    return;
                }

                if (TryGetPointerWorld(mouse.position.ReadValue(), out Vector3 world))
                {
                    dragging = true;
                    dragStartWorld = world;
                    aimDirection = Vector3.zero;
                    shotPower = 0f;
                    aimingLine.enabled = true;
                    UpdateAimingLine();
                }
            }

            if (dragging && mouse.leftButton.isPressed && TryGetPointerWorld(mouse.position.ReadValue(), out Vector3 currentWorld))
            {
                Vector3 pull = dragStartWorld - currentWorld;
                pull.y = 0f;
                float distance = Mathf.Min(pull.magnitude, maximumDragDistance);
                shotPower = Mathf.Clamp01(distance / maximumDragDistance);
                aimDirection = pull.sqrMagnitude > 0.0001f ? pull.normalized : Vector3.zero;
                UpdateAimingLine();
            }

            if (dragging && mouse.leftButton.wasReleasedThisFrame)
                ReleaseShot();
        }

        private bool TryGetPointerWorld(Vector2 screenPosition, out Vector3 world)
        {
            Plane aimPlane = new Plane(Vector3.up, currentLevel.ball.position);
            Ray ray = gameCamera.ScreenPointToRay(screenPosition);
            if (aimPlane.Raycast(ray, out float distance))
            {
                world = ray.GetPoint(distance);
                return true;
            }

            world = default;
            return false;
        }

        private void UpdateAimingLine()
        {
            if (aimingLine == null || currentLevel == null)
                return;

            Vector3 ballCenter = currentLevel.ball.worldCenterOfMass;
            float displayLength = Mathf.Lerp(0.35f, 3.2f, shotPower);
            aimingLine.SetPosition(0, ballCenter);
            aimingLine.SetPosition(1, ballCenter + aimDirection * displayLength);

            Color low = new Color32(0x89, 0xE0, 0xB3, 0xFF);
            Color middle = Gold;
            Color high = new Color32(0xF2, 0x77, 0x67, 0xFF);
            Color color = shotPower < 0.55f
                ? Color.Lerp(low, middle, shotPower / 0.55f)
                : Color.Lerp(middle, high, (shotPower - 0.55f) / 0.45f);
            aimingLine.startColor = color;
            aimingLine.endColor = color;
        }

        private void ReleaseShot()
        {
            dragging = false;
            aimingLine.enabled = false;

            if (shotPower < 0.035f || aimDirection.sqrMagnitude < 0.1f || !CanTakeAction())
                return;

            Rigidbody ball = currentLevel.ball;
            Vector3 velocity = ball.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            ball.linearVelocity = velocity;
            ball.angularVelocity *= 0.15f;
            ball.AddForce(aimDirection * Mathf.Lerp(1.1f, maximumImpulse, shotPower), ForceMode.Impulse);
            levelStrokes++;
            totalStrokes++;
            AudioManager.Instance?.PlayShotSfx();
        }

        private bool CanTakeAction()
        {
            return currentLevel != null && !currentLevel.IsRevealing && !capturing && !levelComplete && currentLevel.ball.linearVelocity.magnitude <= playableSpeed;
        }

        private void TryRotateLevel(int direction)
        {
            if (dragging)
            {
                dragging = false;
                aimingLine.enabled = false;
            }

            if (!CanTakeAction())
            {
                ShowFeedback("WAIT FOR THE BALL");
                return;
            }

            Rigidbody ball = currentLevel.ball;
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
            ball.isKinematic = true;
            currentLevel.transform.Rotate(Vector3.up, direction * 45f, Space.World);
            Physics.SyncTransforms();
            ball.isKinematic = false;
            AudioManager.Instance?.PlayRotationSfx();
            ShowFeedback(direction < 0 ? "ROTATED LEFT" : "ROTATED RIGHT");
        }

        private void ResetBall(bool penalty)
        {
            StopAllCoroutines();
            capturing = false;
            levelComplete = false;
            dragging = false;
            if (aimingLine != null)
                aimingLine.enabled = false;

            Rigidbody ball = currentLevel.ball;
            ball.isKinematic = true;
            ball.transform.SetPositionAndRotation(currentLevel.ballSpawn.position, currentLevel.ballSpawn.rotation);
            ball.transform.localScale = Vector3.one;
            Physics.SyncTransforms();
            ball.isKinematic = false;
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
            CameraImpactShake.Instance?.ResetCameraPosition();

            if (penalty)
            {
                levelStrokes++;
                totalStrokes++;
            }
        }

        private void ResetLevelWithPenalty()
        {
            ResetBall(true);
            Physics.SyncTransforms();
            currentLevel.revealAnimator?.PlayReveal();
            ShowFeedback("RESET  +1 STROKE");
        }

        private IEnumerator CaptureBall()
        {
            if (capturing)
                yield break;

            capturing = true;
            dragging = false;
            aimingLine.enabled = false;
            Rigidbody ball = currentLevel.ball;
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
            ball.isKinematic = true;
            AudioManager.Instance?.PlayHoleSfx();

            Vector3 startPosition = ball.position;
            Vector3 startScale = ball.transform.localScale;
            Vector3 targetPosition = currentLevel.holeCenter.position + Vector3.down * 0.32f;
            const float duration = 0.62f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                ball.position = Vector3.Lerp(startPosition, targetPosition, t);
                ball.transform.localScale = Vector3.Lerp(startScale, startScale * 0.08f, t);
                yield return null;
            }

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

            for (int i = 0; i < levels.Length; i++)
            {
                levels[i].RestoreRuntimeTransform();
                levels[i].gameObject.SetActive(i == currentLevelIndex);
            }

            currentLevel = levels[currentLevelIndex];
            currentLevel.gameObject.SetActive(true);
            gameCamera.orthographicSize = currentLevel.cameraSize;
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

        private void OnDestroy()
        {
            if (roundedTexture != null)
                Destroy(roundedTexture);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null)
                return;

            Font font = uiFont != null ? uiFont : GUI.skin.font;
            titleStyle = CreateLabelStyle(font, 22, FontStyle.Bold, PaleText, TextAnchor.MiddleLeft);
            headingStyle = CreateLabelStyle(font, 13, FontStyle.Bold, Accent, TextAnchor.MiddleLeft);
            bodyStyle = CreateLabelStyle(font, 13, FontStyle.Normal, PaleText, TextAnchor.MiddleLeft);
            smallStyle = CreateLabelStyle(font, 9, FontStyle.Normal, new Color(PaleText.r, PaleText.g, PaleText.b, 0.76f), TextAnchor.MiddleLeft);
            centeredStyle = CreateLabelStyle(font, 11, FontStyle.Normal, PaleText, TextAnchor.MiddleCenter);
            hugeStyle = CreateLabelStyle(font, 38, FontStyle.Bold, PaleText, TextAnchor.MiddleCenter);
            eyebrowStyle = CreateLabelStyle(font, 9, FontStyle.Bold, new Color(PaleText.r, PaleText.g, PaleText.b, 0.62f), TextAnchor.MiddleLeft);
            statLabelStyle = CreateLabelStyle(font, 8, FontStyle.Bold, new Color(PaleText.r, PaleText.g, PaleText.b, 0.58f), TextAnchor.MiddleCenter);
            statValueStyle = CreateLabelStyle(font, 21, FontStyle.Bold, PaleText, TextAnchor.MiddleCenter);
            darkCenteredStyle = CreateLabelStyle(font, 11, FontStyle.Bold, Ink, TextAnchor.MiddleCenter);
            roundedPanelStyle = new GUIStyle
            {
                normal = { background = roundedTexture },
                border = new RectOffset(11, 11, 11, 11)
            };
        }

        private void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.height / 600f, 1.2f, 1.8f);
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float width = Screen.width / scale;
            float height = Screen.height / scale;

            if (courseComplete)
            {
                DrawCourseComplete(width, height);
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                return;
            }

            if (currentLevel == null)
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
                return;
            }

            DrawIdentityCard();
            DrawProgressCard(width);
            DrawStatsCard(width);

            if (dragging)
                DrawPowerMeter(width, height);

            if (Time.unscaledTime < feedbackUntil)
                DrawFeedback(width, height);

            Rect legend = new Rect(width * 0.5f - 248, height - 42, 496, 27);
            DrawPanel(legend, PanelSoftColor);
            GUI.Label(legend, "DRAG  PUTT     ← →  ROTATE COURSE     R  RESET", centeredStyle);

            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }

        private void DrawIdentityCard()
        {
            Rect card = new Rect(18, 16, 250, 78);
            DrawPanel(card, PanelColor);
            DrawRect(new Rect(card.x + 9, card.y + 12, 5, 54), Coral);
            GUI.Label(new Rect(card.x + 27, card.y + 10, 205, 29), "MINIMAL GOLF", titleStyle);
            GUI.Label(new Rect(card.x + 28, card.y + 39, 205, 13), "CURRENT COURSE", eyebrowStyle);
            GUI.Label(new Rect(card.x + 28, card.y + 52, 205, 18), currentLevel.levelName, headingStyle);
        }

        private void DrawProgressCard(float width)
        {
            Rect card = new Rect(width * 0.5f - 99, 18, 198, 48);
            DrawPanel(card, PanelSoftColor);
            GUI.Label(new Rect(card.x, card.y + 3, card.width, 14), "COURSE PROGRESS", statLabelStyle);

            const float blockWidth = 24f;
            const float gap = 7f;
            float totalWidth = levels.Length * blockWidth + Mathf.Max(0, levels.Length - 1) * gap;
            float startX = card.x + (card.width - totalWidth) * 0.5f;
            for (int i = 0; i < levels.Length; i++)
            {
                bool current = i == currentLevelIndex;
                float blockHeight = current ? 8f : 5f;
                Color color = i < currentLevelIndex
                    ? Seafoam
                    : current
                        ? Coral
                        : new Color(PaleText.r, PaleText.g, PaleText.b, 0.18f);
                DrawRect(new Rect(startX + i * (blockWidth + gap), card.y + 29f - blockHeight * 0.5f, blockWidth, blockHeight), color);
            }
        }

        private void DrawStatsCard(float width)
        {
            Rect card = new Rect(width - 304, 16, 286, 78);
            DrawPanel(card, PanelColor);
            const float cellWidth = 88f;
            DrawStat(new Rect(card.x + 7, card.y + 7, cellWidth, 64), "STROKES", levelStrokes.ToString(), Coral);
            DrawRect(new Rect(card.x + 99, card.y + 15, 1, 48), new Color(PaleText.r, PaleText.g, PaleText.b, 0.13f));
            DrawStat(new Rect(card.x + 101, card.y + 7, cellWidth, 64), "PAR", currentLevel.par.ToString(), Gold);
            DrawRect(new Rect(card.x + 193, card.y + 15, 1, 48), new Color(PaleText.r, PaleText.g, PaleText.b, 0.13f));
            DrawStat(new Rect(card.x + 195, card.y + 7, cellWidth, 64), "HOLE", (currentLevelIndex + 1) + " / " + levels.Length, Accent);
        }

        private void DrawStat(Rect rect, string label, string value, Color valueColor)
        {
            GUI.Label(new Rect(rect.x, rect.y + 2, rect.width, 14), label, statLabelStyle);
            Color previousColor = statValueStyle.normal.textColor;
            statValueStyle.normal.textColor = valueColor;
            GUI.Label(new Rect(rect.x, rect.y + 17, rect.width, 38), value, statValueStyle);
            statValueStyle.normal.textColor = previousColor;
        }

        private void DrawPowerMeter(float width, float height)
        {
            Rect panel = new Rect(width * 0.5f - 165, height - 118, 330, 62);
            DrawPanel(panel, PanelColor);
            GUI.Label(new Rect(panel.x + 15, panel.y + 7, 180, 15), "PUTT STRENGTH", eyebrowStyle);

            GUIStyle percentageStyle = new GUIStyle(eyebrowStyle) { alignment = TextAnchor.MiddleRight };
            percentageStyle.normal.textColor = PaleText;
            GUI.Label(new Rect(panel.x + 220, panel.y + 7, 94, 15), Mathf.RoundToInt(shotPower * 100f) + "%", percentageStyle);

            const int segments = 12;
            const float gap = 4f;
            float available = panel.width - 30f;
            float segmentWidth = (available - gap * (segments - 1)) / segments;
            Color powerColor = shotPower < 0.55f
                ? Color.Lerp(Seafoam, Gold, shotPower / 0.55f)
                : Color.Lerp(Gold, Coral, (shotPower - 0.55f) / 0.45f);
            for (int i = 0; i < segments; i++)
            {
                bool filled = shotPower >= (i + 1f) / segments;
                Color color = filled ? powerColor : new Color(PaleText.r, PaleText.g, PaleText.b, 0.13f);
                DrawRect(new Rect(panel.x + 15 + i * (segmentWidth + gap), panel.y + 32, segmentWidth, 12), color);
            }
        }

        private void DrawFeedback(float width, float height)
        {
            float alpha = Mathf.Clamp01((feedbackUntil - Time.unscaledTime) * 3.2f);
            Rect messageRect = new Rect(width * 0.5f - 190, height * 0.19f, 380, 44);
            DrawPanel(messageRect, new Color(WarmCream.r, WarmCream.g, WarmCream.b, 0.96f * alpha));
            DrawRect(new Rect(messageRect.x + 8, messageRect.y + 8, 5, messageRect.height - 16), new Color(Coral.r, Coral.g, Coral.b, alpha));
            Color previous = darkCenteredStyle.normal.textColor;
            darkCenteredStyle.normal.textColor = new Color(Ink.r, Ink.g, Ink.b, alpha);
            GUI.Label(new Rect(messageRect.x + 18, messageRect.y, messageRect.width - 26, messageRect.height), feedback, darkCenteredStyle);
            darkCenteredStyle.normal.textColor = previous;
        }

        private void DrawCourseComplete(float width, float height)
        {
            DrawRect(new Rect(0, 0, width, height), new Color(Ink.r, Ink.g, Ink.b, 0.94f));
            Rect card = new Rect(width * 0.5f - 245, height * 0.5f - 142, 490, 284);
            DrawPanel(card, new Color(0.075f, 0.19f, 0.22f, 0.98f));

            float blockX = card.x + card.width * 0.5f - 64f;
            DrawRect(new Rect(blockX, card.y + 25, 36, 7), Seafoam);
            DrawRect(new Rect(blockX + 46, card.y + 25, 36, 7), Gold);
            DrawRect(new Rect(blockX + 92, card.y + 25, 36, 7), Coral);
            GUI.Label(new Rect(card.x + 20, card.y + 48, card.width - 40, 54), "COURSE COMPLETE", hugeStyle);
            GUI.Label(new Rect(card.x + 20, card.y + 112, card.width - 40, 18), "FIVE SMALL COURSES • ONE GRAND SCORE", statLabelStyle);

            Rect score = new Rect(card.x + 142, card.y + 146, card.width - 284, 58);
            DrawPanel(score, WarmCream);
            GUIStyle scoreStyle = new GUIStyle(statValueStyle) { alignment = TextAnchor.MiddleCenter };
            scoreStyle.normal.textColor = Ink;
            GUI.Label(new Rect(score.x, score.y + 1, score.width, 34), totalStrokes.ToString(), scoreStyle);
            GUIStyle darkLabelStyle = new GUIStyle(statLabelStyle);
            darkLabelStyle.normal.textColor = Ink;
            GUI.Label(new Rect(score.x, score.y + 32, score.width, 17), "TOTAL STROKES", darkLabelStyle);

            Rect prompt = new Rect(card.x + 88, card.y + 228, card.width - 176, 32);
            DrawPanel(prompt, Coral);
            GUI.Label(prompt, "ENTER OR SPACE  •  PLAY AGAIN", darkCenteredStyle);
        }

        private GUIStyle CreateLabelStyle(Font font, int fontSize, FontStyle fontStyle, Color color, TextAnchor alignment)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = fontSize,
                fontStyle = fontStyle,
                alignment = alignment,
                clipping = TextClipping.Clip
            };
            style.normal.textColor = color;
            return style;
        }

        private Texture2D CreateRoundedTexture(int size, float radius)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Minimal Golf Rounded UI",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float edgeX = Mathf.Min(x + 0.5f, size - x - 0.5f);
                    float edgeY = Mathf.Min(y + 0.5f, size - y - 0.5f);
                    float cornerX = Mathf.Max(radius - edgeX, 0f);
                    float cornerY = Mathf.Max(radius - edgeY, 0f);
                    float distance = Mathf.Sqrt(cornerX * cornerX + cornerY * cornerY);
                    float alpha = Mathf.Clamp01(radius + 0.75f - distance);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void DrawPanel(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Box(rect, GUIContent.none, roundedPanelStyle);
            GUI.color = previous;
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, whiteTexture);
            GUI.color = previous;
        }
    }
}
