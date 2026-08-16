using UnityEditor;
using UnityEngine;

namespace MinimalGolf
{
    [CustomEditor(typeof(VRGolfUI))]
    public sealed class VRGolfUIEditor : Editor
    {
        private SerializedProperty _preview;
        private SerializedProperty _global;
        private SerializedProperty _gamePlayScale;
        private SerializedProperty _power;
        private SerializedProperty _feedback;
        private SerializedProperty _complete;
        private SerializedProperty _fontScale;
        // Layout positions & sizes
        private SerializedProperty _gamePlayPos;
        private SerializedProperty _gamePlaySize;
        private SerializedProperty _powerPos;
        private SerializedProperty _powerSize;
        private SerializedProperty _feedbackPos;
        private SerializedProperty _feedbackSize;
        private SerializedProperty _courseCompletePos;
        private SerializedProperty _courseCompleteSize;
        // GamePlayCard group positions
        private SerializedProperty _identityGroupPos;
        private SerializedProperty _statsGroupPos;
        private SerializedProperty _progressGroupPos;

        private void OnEnable()
        {
            _preview = serializedObject.FindProperty("previewInEditMode");
            _global = serializedObject.FindProperty("globalScale");
            _gamePlayScale = serializedObject.FindProperty("gamePlayScale");
            _power = serializedObject.FindProperty("powerScale");
            _feedback = serializedObject.FindProperty("feedbackScale");
            _complete = serializedObject.FindProperty("courseCompleteScale");
            _fontScale = serializedObject.FindProperty("fontScale");
            _gamePlayPos = serializedObject.FindProperty("gamePlayCardPosition");
            _gamePlaySize = serializedObject.FindProperty("gamePlayCardSize");
            _powerPos = serializedObject.FindProperty("powerMeterPosition");
            _powerSize = serializedObject.FindProperty("powerMeterSize");
            _feedbackPos = serializedObject.FindProperty("feedbackToastPosition");
            _feedbackSize = serializedObject.FindProperty("feedbackToastSize");
            _courseCompletePos = serializedObject.FindProperty("courseCompletePosition");
            _courseCompleteSize = serializedObject.FindProperty("courseCompleteSize");
            _identityGroupPos = serializedObject.FindProperty("identityGroupPosition");
            _statsGroupPos = serializedObject.FindProperty("statsGroupPosition");
            _progressGroupPos = serializedObject.FindProperty("progressGroupPosition");
        }

        public override void OnInspectorGUI()
        {
            var t = (VRGolfUI)target;

            DrawDefaultInspector();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Preview: VR_UI hierarchy appears under VRCourseAnchor in Edit Mode when 'Preview In Edit Mode' is on. " +
                "GamePlayCard replaces IdentityCard+StatsCard+ProgressCard. Adjust Global + per-canvas scales and Font Scale. " +
                "Use Rebuild Preview if anchors changed.",
                MessageType.Info);

            serializedObject.Update();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Scaling — World Space Canvases", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_preview, new GUIContent("Preview In Edit Mode"));
            EditorGUILayout.PropertyField(_global, new GUIContent("Global Scale (×)"));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Per-Canvas Scales (× Global)", EditorStyles.miniBoldLabel);
            EditorGUILayout.PropertyField(_gamePlayScale, new GUIContent("GamePlayCard (Combined HUD)"));
            EditorGUILayout.PropertyField(_power, new GUIContent("Power Meter (Putt Strength)"));
            EditorGUILayout.PropertyField(_feedback, new GUIContent("Feedback Toast"));
            EditorGUILayout.PropertyField(_complete, new GUIContent("Course Complete"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Layout — Positions & Sizes (VR_UI local)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Each canvas localPosition (meters, VR_UI space) and size (pixels). Change here or directly on RectTransform. Values applied live in Edit & Play.", MessageType.None);
            EditorGUILayout.PropertyField(_gamePlayPos, new GUIContent("GamePlayCard Position"));
            EditorGUILayout.PropertyField(_gamePlaySize, new GUIContent("GamePlayCard Size (w×h)"));
            EditorGUILayout.PropertyField(_powerPos, new GUIContent("Power Meter Position"));
            EditorGUILayout.PropertyField(_powerSize, new GUIContent("Power Meter Size"));
            EditorGUILayout.PropertyField(_feedbackPos, new GUIContent("Feedback Toast Position"));
            EditorGUILayout.PropertyField(_feedbackSize, new GUIContent("Feedback Toast Size"));
            EditorGUILayout.PropertyField(_courseCompletePos, new GUIContent("Course Complete Position"));
            EditorGUILayout.PropertyField(_courseCompleteSize, new GUIContent("Course Complete Size"));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset Layout Defaults"))
                {
                    _gamePlayPos.vector3Value = new Vector3(0, 0.65f, -0.7f);
                    _gamePlaySize.vector2Value = new Vector2(1450, 190);
                    _powerPos.vector3Value = new Vector3(0, 0.15f, -0.2f);
                    _powerSize.vector2Value = new Vector2(560, 110);
                    _feedbackPos.vector3Value = new Vector3(0, 0.35f, 0.2f);
                    _feedbackSize.vector2Value = new Vector2(560, 70);
                    _courseCompletePos.vector3Value = new Vector3(0, 0.6f, 0.4f);
                    _courseCompleteSize.vector2Value = new Vector2(760, 500);
                }
                if (GUILayout.Button("Narrow GamePlay (1000)"))
                {
                    _gamePlaySize.vector2Value = new Vector2(1000, 170);
                }
                if (GUILayout.Button("Tall GamePlay (260)"))
                {
                    _gamePlaySize.vector2Value = new Vector2(_gamePlaySize.vector2Value.x, 260);
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("GamePlayCard — Group Positions (inside GamePlayCard)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Each group anchoredPosition inside GamePlayCard. Edit here or drag groups in Hierarchy/Scene view, then Save to keep manual moves.", MessageType.None);
            EditorGUILayout.PropertyField(_identityGroupPos, new GUIContent("Identity Group Pos"));
            EditorGUILayout.PropertyField(_statsGroupPos, new GUIContent("Stats Group Pos"));
            EditorGUILayout.PropertyField(_progressGroupPos, new GUIContent("Progress Group Pos"));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset Groups Default"))
                {
                    _identityGroupPos.vector2Value = new Vector2(-480, -10);
                    _statsGroupPos.vector2Value = new Vector2(480, -10);
                    _progressGroupPos.vector2Value = new Vector2(0, 52);
                }
                if (GUILayout.Button("Save Groups From Hierarchy"))
                {
                    serializedObject.ApplyModifiedProperties();
                    t.SaveGroupPositionsFromHierarchy();
                    EditorUtility.SetDirty(t);
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Save Manual Hierarchy Edits", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("If you move/resize canvases or groups directly in Hierarchy/Scene (RectTool), click Save to write those hierarchy values back into the component fields so they persist.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save All Positions/Sizes From Hierarchy"))
                {
                    serializedObject.ApplyModifiedProperties();
                    t.SaveHierarchyToComponent();
                    EditorUtility.SetDirty(t);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                }
                if (GUILayout.Button("Save Groups Only"))
                {
                    serializedObject.ApplyModifiedProperties();
                    t.SaveGroupsToComponent();
                    EditorUtility.SetDirty(t);
                }
            }
            // Also available as ContextMenu on component: "Save Hierarchy Positions/Sizes to Component"

            EditorGUILayout.Space(6);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("0.7× Smaller")) ApplyPreset(t, 0.7f);
                if (GUILayout.Button("1× Default")) ApplyPreset(t, 1f);
                if (GUILayout.Button("1.5× Larger")) ApplyPreset(t, 1.5f);
                if (GUILayout.Button("2× Huge")) ApplyPreset(t, 2f);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Font Sizes — Global Multiplier", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_fontScale, new GUIContent("Font Scale (× base)"));
            EditorGUILayout.HelpBox("Base font sizes are on the component (Title 22, Course 13, Level 9, Labels 8, Strokes/Par 28, etc.). Adjust bases there, use Font Scale here to scale all canvases at once.", MessageType.None);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("0.8×")) { _fontScale.floatValue = 0.8f; }
                if (GUILayout.Button("1×")) { _fontScale.floatValue = 1f; }
                if (GUILayout.Button("1.25×")) { _fontScale.floatValue = 1.25f; }
                if (GUILayout.Button("1.5×")) { _fontScale.floatValue = 1.5f; }
            }

            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed)
            {
                EditorUtility.SetDirty(t);
                if (t.vrCourseAnchor != null) EditorUtility.SetDirty(t.vrCourseAnchor);
            }

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rebuild Preview"))
                {
                    Undo.RecordObject(t, "Rebuild VR UI Preview");
                    t.RebuildPreview();
                    EditorUtility.SetDirty(t);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                }
                if (GUILayout.Button("Remove Preview"))
                {
                    Undo.RecordObject(t, "Remove VR UI Preview");
                    t.RemovePreviewCanvases();
                    EditorUtility.SetDirty(t);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                }
            }

            if (t.vrCourseAnchor != null)
            {
                var uiRoot = t.vrCourseAnchor.Find("VR_UI");
                if (uiRoot != null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.HelpBox($"Hierarchy: {t.vrCourseAnchor.name}/VR_UI ({uiRoot.childCount} canvases: GamePlayCard, PowerMeter, FeedbackToast, CourseComplete) • Select any to tweak.", MessageType.None);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Select VR_UI")) Selection.activeTransform = uiRoot;
                        if (GUILayout.Button("Select VRCourseAnchor")) Selection.activeTransform = t.vrCourseAnchor;
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("GamePlay")) Ping(t.gamePlayCanvas);
                        if (GUILayout.Button("Power")) Ping(t.powerCanvas);
                    }
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Feedback")) Ping(t.feedbackCanvas);
                        if (GUILayout.Button("Complete")) Ping(t.courseCompleteCanvas);
                    }
                }
                else if (t.previewInEditMode)
                {
                    EditorGUILayout.HelpBox("VR_UI not found under VRCourseAnchor — click Rebuild Preview.", MessageType.Warning);
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Computed Final Scales", EditorStyles.miniBoldLabel);
            if (_gamePlayScale != null) EditorGUILayout.LabelField($"GamePlay: {_gamePlayScale.floatValue * _global.floatValue:F4}  ({_gamePlayScale.floatValue:F4} × {_global.floatValue:F2})");
            EditorGUILayout.LabelField($"Power: {_power.floatValue * _global.floatValue:F4}  ({_power.floatValue:F4} × {_global.floatValue:F2})");
            EditorGUILayout.LabelField($"Feedback: {_feedback.floatValue * _global.floatValue:F4}  ({_feedback.floatValue:F4} × {_global.floatValue:F2})");
            EditorGUILayout.LabelField($"Complete: {_complete.floatValue * _global.floatValue:F4}  ({_complete.floatValue:F4} × {_global.floatValue:F2})");
            EditorGUILayout.LabelField($"Font Effective: ×{_fontScale.floatValue:F2} (Title {Mathf.RoundToInt(22*_fontScale.floatValue)} etc.)");
        }

        private static void Ping(Canvas c)
        {
            if (c != null) { Selection.activeGameObject = c.gameObject; EditorGUIUtility.PingObject(c.gameObject); }
        }

        private static void ApplyPreset(VRGolfUI t, float global)
        {
            Undo.RecordObject(t, $"Set Global Scale {global}×");
            t.globalScale = global;
            t.RebuildPreview();
            EditorUtility.SetDirty(t);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
        }
    }
}
