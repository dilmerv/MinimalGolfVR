using UnityEditor;
using UnityEngine;

namespace MinimalGolf
{
    [CustomEditor(typeof(ProximityRevealVolume))]
    public sealed class ProximityRevealVolumeEditor : Editor
    {
        private void OnSceneGUI()
        {
            var vol = (ProximityRevealVolume)target;
            if (!vol.enabledReveal) return;
            EditorGUI.BeginChangeCheck();
            Handles.color = new Color(0.2f, 0.7f, 1f, 0.9f);
            float newRadius = Handles.RadiusHandle(Quaternion.identity, vol.WorldCenter, vol.EffectiveRadius, false);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(vol, "Change Reveal Radius");
                vol.revealRadius = Mathf.Clamp(newRadius, 0.05f, 3f);
            }
            // Softness handle (outer)
            if (vol.edgeSoftness > 0.001f)
            {
                Handles.color = new Color(0.2f, 0.7f, 1f, 0.25f);
                Handles.DrawWireDisc(vol.WorldCenter, Vector3.up, vol.EffectiveRadius + vol.EffectiveSoftness);
                Handles.DrawWireDisc(vol.WorldCenter, Vector3.forward, vol.EffectiveRadius + vol.EffectiveSoftness);
            }
        }

        public override void OnInspectorGUI()
        {
            var vol = (ProximityRevealVolume)target;
            EditorGUILayout.HelpBox("Invisible x-ray volume. Tagged objects inside radius become invisible (outside visible). Attach is under ProximitySphere/RevealVolume.", MessageType.None);

            DrawDefaultInspector();

            EditorGUILayout.Space(4);
            if (string.IsNullOrEmpty(vol.targetTag))
                EditorGUILayout.HelpBox("No tag set — effect disabled. Choose RevealOccluder or your tunnel tag.", MessageType.Warning);

            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("Tag Selected Objects as RevealOccluder"))
                {
                    foreach (var go in Selection.gameObjects)
                    {
                        Undo.RecordObject(go, "Tag RevealOccluder");
                        go.tag = "RevealOccluder";
                        EditorUtility.SetDirty(go);
                    }
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox($"Center: {vol.WorldCenter:F2}  Radius: {vol.EffectiveRadius:F2}  Softness: {vol.EffectiveSoftness:F2}", MessageType.None);
        }
    }
}
