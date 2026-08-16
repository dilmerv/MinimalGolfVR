using UnityEditor;
using UnityEngine;

namespace MinimalGolf
{
    [CustomEditor(typeof(VRGolfUI))]
    public sealed class VRGolfUIEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var t = (VRGolfUI)target;

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "Simplified: All positions/scales/sizes are controlled by moving the VR_UI canvases in the hierarchy (VRCourseAnchor/VR_UI/GamePlayCard etc.). " +
                "This script only updates text/visibility. Assign canvases above or leave empty to auto-find.",
                MessageType.Info);

            if (t.vrCourseAnchor != null)
            {
                var uiRoot = t.vrCourseAnchor.Find("VR_UI");
                if (uiRoot != null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.HelpBox($"Hierarchy: {t.vrCourseAnchor.name}/VR_UI ({uiRoot.childCount} canvases) — select any to move/scale in Scene view.", MessageType.None);
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
                else
                {
                    EditorGUILayout.HelpBox("VR_UI not found under VRCourseAnchor — ensure hierarchy VRCourseAnchor/VR_UI exists (created automatically at runtime if missing).", MessageType.Warning);
                }
            }
        }

        private static void Ping(Canvas c)
        {
            if (c != null) { Selection.activeGameObject = c.gameObject; EditorGUIUtility.PingObject(c.gameObject); }
        }
    }
}
