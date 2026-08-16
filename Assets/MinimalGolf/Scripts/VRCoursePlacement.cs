using UnityEngine;

namespace MinimalGolf
{
    /// <summary>
    /// Helper to keep VRCourseAnchor at a comfortable tabletop pose.
    /// Attached to VRCourseAnchor; can be tuned in inspector without code.
    /// </summary>
    public sealed class VRCoursePlacement : MonoBehaviour
    {
        [Tooltip("Forward distance from TrackingSpace origin (meters).")]
        public float forwardDistance = 0.65f;
        [Tooltip("Vertical offset below eye level (positive down).")]
        public float heightBelowEye = 0.85f;
        [Tooltip("Uniform scale for tabletop courses (applied to VRCourseLevels).")]
        public float tableScale = 0.042f;

        private void OnValidate()
        {
            Apply();
        }

        private void Awake() => Apply();

        public void Apply()
        {
            float eyeHeight = 1.6f;
            var rig = FindFirstObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
            if (rig != null && rig.centerEyeAnchor != null)
            {
                eyeHeight = rig.centerEyeAnchor.localPosition.y;
                if (eyeHeight < 0.1f) eyeHeight = 1.6f;
            }
            float y = eyeHeight - heightBelowEye;
            if (y < 0.4f) y = 0.75f;
            transform.localPosition = new Vector3(0f, y, forwardDistance);
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
            Transform levels = transform.Find("VRCourseLevels");
            if (levels != null)
                levels.localScale = Vector3.one * tableScale;
            Debug.Log($"[VRCoursePlacement] anchor {transform.localPosition} levels scale {tableScale} eyeHeight {eyeHeight:F2}");
        }
    }
}
