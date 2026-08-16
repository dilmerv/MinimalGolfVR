using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MinimalGolf
{
    [ExecuteAlways]
    public class VREditModePreview : MonoBehaviour
    {
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

        private readonly Vector3 previewCenterPos = new Vector3(0f, 1.65f, -0.05f);
        private readonly Quaternion previewCenterRot = Quaternion.Euler(35f, 0f, 0f);
        private readonly Vector3 previewLeftPos = new Vector3(-0.20f, 1.25f, 0.3f);
        private readonly Quaternion previewLeftRot = Quaternion.Euler(0f, 30f, 0f);
        private readonly Vector3 previewRightPos = new Vector3(0.20f, 1.25f, 0.3f);
        private readonly Quaternion previewRightRot = Quaternion.Euler(0f, -30f, 0f);

        private GameObject leftControllerRoot;
        private GameObject rightControllerRoot;

#if UNITY_EDITOR
        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            CacheReferences();
            if (!Application.isPlaying)
                ApplyPreview();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
                RestoreForPlay();
            else if (state == PlayModeStateChange.EnteredEditMode)
                ApplyPreview();
        }

        private void Update()
        {
            if (Application.isPlaying) return;
            if (centerEye != null && (centerEye.localPosition - previewCenterPos).sqrMagnitude > 0.001f)
                ApplyPreview();
        }

        private void CacheReferences()
        {
            var rig = FindFirstObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
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
        }

        private void ApplyPreview()
        {
            CacheReferences();
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
        }

        private void RestoreForPlay()
        {
            if (centerEye != null) { centerEye.localPosition = vrCenterPos; centerEye.localRotation = vrCenterRot; }
            if (leftAnchor != null) { leftAnchor.localPosition = vrLeftPos; leftAnchor.localRotation = vrLeftRot; }
            if (rightAnchor != null) { rightAnchor.localPosition = vrRightPos; rightAnchor.localRotation = vrRightRot; }
            ApplyControllerModelsPreview(false);
        }

        private void ApplyControllerModelsPreview(bool isPreview)
        {
            void SetModels(GameObject root, bool preview, bool isLeftController)
            {
                if (root == null) return;
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.parent != root.transform) continue;
                    string n = child.name;
                    // Only the correct handed Quest 3 model should be visible in preview
                    // Left controller should show only MetaQuestTouchPlus_Left, hide MetaQuestTouchPlus_Right
                    // Right controller should show only MetaQuestTouchPlus_Right, hide MetaQuestTouchPlus_Left
                    bool isCorrectQuest3 = isLeftController ? n == "MetaQuestTouchPlus_Left" : n == "MetaQuestTouchPlus_Right";
                    if (preview)
                        child.gameObject.SetActive(isCorrectQuest3);
                    else
                        child.gameObject.SetActive(true);
                }
                if (root != null) root.SetActive(true);
            }
            SetModels(leftControllerRoot, isPreview, true);
            SetModels(rightControllerRoot, isPreview, false);
        }
#endif
    }
}
