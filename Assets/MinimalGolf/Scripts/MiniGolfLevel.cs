using UnityEngine;

namespace MinimalGolf
{
    public sealed class MiniGolfLevel : MonoBehaviour
    {
        [Header("Level Identity")]
        public string levelName = "THE WARM UP";
        public int par = 2;
        public float cameraSize = 6.5f;

        [Header("Authored References")]
        public Rigidbody ball;
        public Transform ballSpawn;
        public Transform holeCenter;
        public LevelRevealAnimator revealAnimator;

        [Header("Local Course Bounds")]
        public float courseWidth = 5f;
        public float courseLength = 12f;

        [Header("Runtime Placement")]
        [Tooltip("All authored level roots are moved here when Play mode begins.")]
        public Vector3 runtimeLocalPosition = Vector3.zero;

        private Quaternion authoredLocalRotation;
        private bool authoredStateCached;

        public bool IsRevealing => revealAnimator != null && revealAnimator.IsPlaying;

        public void CacheAuthoredState()
        {
            if (authoredStateCached)
                return;

            authoredLocalRotation = transform.localRotation;
            if (revealAnimator == null)
                revealAnimator = GetComponent<LevelRevealAnimator>();
            authoredStateCached = true;
        }

        public void RestoreRuntimeTransform()
        {
            CacheAuthoredState();
            transform.localPosition = runtimeLocalPosition;
            transform.localRotation = authoredLocalRotation;
        }

        public bool IsOutsideCourse(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            return local.y < -2.25f ||
                   Mathf.Abs(local.x) > courseWidth * 0.5f + 2.5f ||
                   Mathf.Abs(local.z) > courseLength * 0.5f + 2.5f;
        }
    }
}
