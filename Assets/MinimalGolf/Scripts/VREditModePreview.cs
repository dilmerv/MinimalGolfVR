using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace MinimalGolf
{
    // Scene references this as VRPreviewMode (guid 3beb765d9cc874e649d15892f2b02719);
    // keep VREditModePreview as alias for backwards compat.
    // Must run BEFORE OVRCameraRig (which has no explicit order, default 0) so that
    // preview positions and controller model toggles are not overwritten by
    // OVRCameraRig.EnsureGameObjectIntegrity / OVRControllerHelper.InitializeControllerModels.
    [DefaultExecutionOrder(-10000)]
    [ExecuteAlways]
    public class VRPreviewMode : MonoBehaviour
    {
        [SerializeField]
        private Vector3 previewCenterPos = new Vector3(0f, 1.65f, -0.05f);
        [SerializeField]
        private Quaternion previewCenterRot = Quaternion.Euler(35f, 0f, 0f);
        [SerializeField]
        private Vector3 previewLeftPos = new Vector3(-0.20f, 1.25f, 0.3f);
        [SerializeField]
        private Quaternion previewLeftRot = Quaternion.Euler(0f, 30f, 0f);
        [SerializeField]
        private Vector3 previewRightPos = new Vector3(0.20f, 1.25f, 0.3f);
        [SerializeField]
        private Quaternion previewRightRot = Quaternion.Euler(0f, -30f, 0f);
        [SerializeField, Tooltip("When enabled, the preview auto-applies in edit mode (including after editor restart and play sessions) and re-applies itself if the rig overwrites it. The Restore button pauses auto-apply until Reapply, replay, or re-checking this. When disabled, the preview is only applied manually via Reapply Preview.")]
        private bool persistentPreview = true;

        private Transform centerEye;
        private Transform leftAnchor;
        private Transform rightAnchor;
        private Transform vrAnchor;

        private readonly Vector3 vrCenterPos = new Vector3(0f, 0f, 0f);
        private readonly Quaternion vrCenterRot = Quaternion.identity;
        private readonly Vector3 vrLeftPos = new Vector3(0f, 0f, 0f);
        private readonly Quaternion vrLeftRot = Quaternion.identity;
        private readonly Vector3 vrRightPos = new Vector3(0f, 0f, 0f);
        private readonly Quaternion vrRightRot = Quaternion.identity;
    
        private GameObject leftControllerRoot;
        private GameObject rightControllerRoot;
        private bool isPreviewActive;
        // Set when the user explicitly clicks Restore in edit mode: honor it and don't
        // let persistent auto-apply undo it until Reapply, a play cycle, or re-checking Persistent.
        private bool suppressAutoPreview;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            if (!isActiveAndEnabled) return;
            // Toggling Persistent back on (or editing values with it on) re-enforces the preview.
            if (persistentPreview && !isPreviewActive)
            {
                suppressAutoPreview = false;
                TryApplyPreview();
            }
        }

        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            if (Application.isPlaying)
            {
                isPreviewActive = false;
                return;
            }
            if (persistentPreview && !suppressAutoPreview)
                TryApplyPreview();
            else
                isPreviewActive = false;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
        }

        // Fires after the scene finishes loading — the deterministic hook for editor
        // restarts, where OnEnable can run before scene-wide lookup resolves.
        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (Application.isPlaying) return;
            if (!persistentPreview || suppressAutoPreview) return;
            TryApplyPreview();
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                RestoreForPlay();
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                if (persistentPreview)
                {
                    suppressAutoPreview = false;
                    TryApplyPreview();
                }
            }
        }

        private double nextRetryTime;

        private void Update()
        {
            if (Application.isPlaying) return;
            if (!persistentPreview || suppressAutoPreview) return;
            if (!isPreviewActive || centerEye == null)
            {
                // Not applied yet — e.g. OnEnable ran before the rig was resolvable
                // during editor/scene load. Keep retrying (throttled) until it sticks.
                if (EditorApplication.timeSinceStartup >= nextRetryTime)
                {
                    nextRetryTime = EditorApplication.timeSinceStartup + 0.5;
                    TryApplyPreview();
                }
                return;
            }
            if ((centerEye.localPosition - previewCenterPos).sqrMagnitude > 0.001f)
                TryApplyPreview();
        }

        // Returns true once the rig and its key anchors are resolved.
        private bool CacheReferences()
        {
            // Prefer the rig on this GameObject: valid even during scene load, when a
            // scene-wide lookup may not see everything yet. Fall back to scene search.
            var rig = GetComponent<OVRCameraRig>();
            if (rig == null)
                rig = GetComponentInParent<OVRCameraRig>();
            if (rig == null)
                rig = FindFirstObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
            if (rig != null)
            {
                centerEye = rig.centerEyeAnchor;
                leftAnchor = rig.leftControllerAnchor != null ? rig.leftControllerAnchor : rig.leftHandAnchor;
                rightAnchor = rig.rightControllerAnchor != null ? rig.rightControllerAnchor : rig.rightHandAnchor;
            }
            var placement = FindFirstObjectByType<VRCoursePlacement>(FindObjectsInactive.Include);
            if (placement != null) vrAnchor = placement.transform;
            else
            {
                var go = GameObject.Find("VRCourseAnchor");
                if (go != null) vrAnchor = go.transform;
            }
            if (leftAnchor != null)
            {
                var helper = leftAnchor.GetComponentInChildren<OVRControllerHelper>(true);
                if (helper != null) leftControllerRoot = helper.gameObject;
            }
            if (rightAnchor != null)
            {
                var helper = rightAnchor.GetComponentInChildren<OVRControllerHelper>(true);
                if (helper != null) rightControllerRoot = helper.gameObject;
            }
            return centerEye != null;
        }

        [ContextMenu("Reapply Preview — ApplyPreview")]
        public void ApplyPreview() => TryApplyPreview();

        // Applies the preview; returns false when the rig/anchors aren't resolvable yet
        // (e.g. very early in scene load) so callers can retry instead of giving up.
        private bool TryApplyPreview()
        {
            suppressAutoPreview = false;
            if (!CacheReferences())
            {
                isPreviewActive = false;
                return false;
            }
            isPreviewActive = true;
            if (centerEye != null)
            {
                Transform ts = centerEye.parent;
                if (ts != null)
                {
                    centerEye.localPosition = previewCenterPos;
                    centerEye.localRotation = previewCenterRot;
                }
            }
            if (leftAnchor != null)
            {
                leftAnchor.localPosition = previewLeftPos;
                leftAnchor.localRotation = previewLeftRot;
            }
            if (rightAnchor != null)
            {
                rightAnchor.localPosition = previewRightPos;
                rightAnchor.localRotation = previewRightRot;
            }
            ApplyControllerModelsPreview(true);
            return true;
        }

        // Inspector button wrapper — also used by CustomEditor button.
        // Marks the restore as an explicit user choice so persistent auto-apply
        // doesn't immediately undo it; cleared on Reapply, replay, or re-check.
        public void RestoreForPlayManual()
        {
            RestoreForPlay();
            if (!Application.isPlaying)
                suppressAutoPreview = true;
        }

        [ContextMenu("Restore For Play — RestoreForPlay")]
        public void RestoreForPlayInspector() => RestoreForPlayManual();

        [ContextMenu("Restore For Play")]
        public void RestoreForPlayPublic() => RestoreForPlayManual();

        private void RestoreForPlay()
        {
            isPreviewActive = false;
            // Revert everything to original VR positions — unconditional, as requested.
            // Execution order (-10000) ensures this runs BEFORE OVRCameraRig.EnsureGameObjectIntegrity
            // so OVR's later initialization can correctly drive eye at ~1.70 without fighting preview offset.
            if (centerEye != null) { centerEye.localPosition = vrCenterPos; centerEye.localRotation = vrCenterRot; }
            if (leftAnchor != null) { leftAnchor.localPosition = vrLeftPos; leftAnchor.localRotation = vrLeftRot; }
            if (rightAnchor != null) { rightAnchor.localPosition = vrRightPos; rightAnchor.localRotation = vrRightRot; }
            // Also ensure VRCourseAnchor preview state is cleared if it was touched (currently not offset in preview, but keep symmetric)
            if (vrAnchor != null)
            {
                // VRCoursePlacement will drive this in play, but ensure no leftover preview transform
                // Keep as-is — VRCoursePlacement.Apply() in Awake will set correct (0, y, forwardDistance)
            }
            ApplyControllerModelsPreview(false);
        }

        private void ApplyControllerModelsPreview(bool isPreview)
        {
            void SetModels(GameObject root, bool preview, bool isLeftController)
            {
                if (root == null) return;
                // Restore for Play (isPreview==false) must enable ALL controllers under each parent OVRController
                if (!preview)
                {
                    // Just SetActive — no OVRControllerHelper handling needed
                    root.SetActive(true);
                    foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                        t.gameObject.SetActive(true);
                    return;
                }
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.parent != root.transform) continue;
                    string n = child.name;
                    // Only the correct handed Quest 3 model should be visible in preview
                    // Left controller should show only MetaQuestTouchPlus_Left, hide MetaQuestTouchPlus_Right
                    // Right controller should show only MetaQuestTouchPlus_Right, hide MetaQuestTouchPlus_Left
                    bool isCorrectQuest3 = isLeftController ? n == "MetaQuestTouchPlus_Left" : n == "MetaQuestTouchPlus_Right";
                    child.gameObject.SetActive(isCorrectQuest3);
                }
                root.SetActive(true);
            }
            SetModels(leftControllerRoot, isPreview, true);
            SetModels(rightControllerRoot, isPreview, false);
            // Fallback: ensure EVERY OVRControllerHelper in scene has ALL children enabled
            // (covers case where cached left/right roots were stale, or additional helpers exist)
            if (!isPreview)
            {
                // Restore for Play — enable ALL controllers under each parent OVRController
                // Use scene-wide search so no helper is missed — just SetActive
                var allHelpers = FindObjectsByType<OVRControllerHelper>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var h in allHelpers)
                {
                    if (h == null) continue;
                    h.gameObject.SetActive(true);
                    foreach (Transform t in h.GetComponentsInChildren<Transform>(true))
                        t.gameObject.SetActive(true);
                }
                // Also ensure the anchor helpers themselves are found via rig (older fallback kept for safety)
                var rig = FindFirstObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
                if (rig != null)
                {
                    var leftH = rig.leftControllerAnchor != null ? rig.leftControllerAnchor.GetComponentInChildren<OVRControllerHelper>(true) : null;
                    var rightH = rig.rightControllerAnchor != null ? rig.rightControllerAnchor.GetComponentInChildren<OVRControllerHelper>(true) : null;
                    void ForceAllActive(OVRControllerHelper hh)
                    {
                        if (hh == null) return;
                        hh.gameObject.SetActive(true);
                        foreach (Transform tt in hh.GetComponentsInChildren<Transform>(true))
                            tt.gameObject.SetActive(true);
                    }
                    ForceAllActive(leftH);
                    ForceAllActive(rightH);
                }
            }
        }
#endif
    }

    // Backcompat alias — file is VREditModePreview.cs but scene expects VRPreviewMode
    [DefaultExecutionOrder(-10000)]
    public class VREditModePreview : VRPreviewMode { }

#if UNITY_EDITOR
    [CustomEditor(typeof(VRPreviewMode), true)]
    public class VRPreviewModeEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var t = target as VRPreviewMode;
            if (t == null) return;
            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox("Preview positions are edit-mode only. Persistent (checked) auto-applies on load/restart, after play, and re-enforces the preview; Restore pauses it until Reapply, replay, or re-check. Unchecked means manual-only via the buttons. Execution order is -10000 so this runs before OVRCameraRig.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reapply Preview (ApplyPreview)"))
                {
                    t.ApplyPreview();
                    EditorUtility.SetDirty(t);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                }
                if (GUILayout.Button("Restore For Play"))
                {
                    // Same revert ExitingEditMode uses, plus pause persistent auto-apply
                    // until Reapply, replay, or re-checking Persistent.
                    t.RestoreForPlayManual();
                    EditorUtility.SetDirty(t);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                }
            }
        }
    }
#endif
}
