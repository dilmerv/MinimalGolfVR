using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MinimalGolf
{
    [DisallowMultipleComponent]
    public sealed class LevelRevealAnimator : MonoBehaviour
    {
        [Header("Reveal Parts")]
        [SerializeField] private Transform[] animatedParts;
        [SerializeField] private Vector3[] revealDirections =
        {
            Vector3.left,
            Vector3.right,
            Vector3.up,
            Vector3.down,
            Vector3.forward,
            Vector3.back
        };

        [Header("Timing")]
        [SerializeField, Min(0f)] private float startDelay = 0.08f;
        [SerializeField, Min(0.05f)] private float partDuration = 0.55f;
        [SerializeField, Min(0f)] private float partStagger = 0.055f;
        [SerializeField] private AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Motion")]
        [SerializeField, Min(0f)] private float revealDistance = 6f;

        private MiniGolfLevel level;
        private Vector3[] authoredLocalPositions;
        private Vector3[] revealLocalPositions;
        private Coroutine revealRoutine;
        private bool stateCached;

        public bool IsPlaying { get; private set; }
        public float TotalDuration => startDelay + partDuration + Mathf.Max(0, PartCount - 1) * partStagger;
        public int PartCount => animatedParts != null ? animatedParts.Length : 0;

        private void Awake()
        {
            level = GetComponent<MiniGolfLevel>();
            CacheAuthoredState();
        }

        public void Configure(Transform[] parts)
        {
            animatedParts = parts;
            stateCached = false;
        }

        public void PlayReveal()
        {
            CacheAuthoredState();
            CancelReveal();

            if (PartCount == 0)
                return;

            PrepareBall();
            IsPlaying = true;

            for (int i = 0; i < animatedParts.Length; i++)
            {
                Transform part = animatedParts[i];
                if (part == null)
                    continue;

                revealLocalPositions[i] = authoredLocalPositions[i] + GetRevealOffset(i);
                part.localPosition = revealLocalPositions[i];
            }

            Physics.SyncTransforms();
            revealRoutine = StartCoroutine(RevealParts());
        }

        private IEnumerator RevealParts()
        {
            float elapsed = 0f;
            float totalDuration = TotalDuration;

            while (elapsed < totalDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                for (int i = 0; i < animatedParts.Length; i++)
                {
                    Transform part = animatedParts[i];
                    if (part == null)
                        continue;

                    float partElapsed = elapsed - startDelay - i * partStagger;
                    float normalized = Mathf.Clamp01(partElapsed / Mathf.Max(0.05f, partDuration));
                    float eased = easing != null ? easing.Evaluate(normalized) : normalized;
                    part.localPosition = Vector3.LerpUnclamped(revealLocalPositions[i], authoredLocalPositions[i], eased);
                }

                yield return null;
            }

            RestoreParts();
            Physics.SyncTransforms();
            ReleaseBall();
            IsPlaying = false;
            revealRoutine = null;
        }

        private void CacheAuthoredState()
        {
            if (stateCached)
                return;

            if (animatedParts == null || animatedParts.Length == 0)
                animatedParts = FindVisibleParts();

            authoredLocalPositions = new Vector3[animatedParts.Length];
            revealLocalPositions = new Vector3[animatedParts.Length];
            for (int i = 0; i < animatedParts.Length; i++)
            {
                if (animatedParts[i] != null)
                    authoredLocalPositions[i] = animatedParts[i].localPosition;
            }

            stateCached = true;
        }

        private Transform[] FindVisibleParts()
        {
            var parts = new List<Transform>();
            for (int groupIndex = 0; groupIndex < transform.childCount; groupIndex++)
            {
                Transform group = transform.GetChild(groupIndex);
                for (int childIndex = 0; childIndex < group.childCount; childIndex++)
                {
                    Transform child = group.GetChild(childIndex);
                    if (child.GetComponentInChildren<Renderer>(true) != null)
                        parts.Add(child);
                }
            }

            return parts.ToArray();
        }

        private Vector3 GetRevealOffset(int index)
        {
            if (revealDirections == null || revealDirections.Length == 0)
                return Vector3.left * revealDistance;

            Vector3 direction = revealDirections[index % revealDirections.Length];
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.left;

            return direction.normalized * revealDistance;
        }

        private void PrepareBall()
        {
            if (level == null)
                level = GetComponent<MiniGolfLevel>();
            if (level == null || level.ball == null)
                return;

            level.ball.linearVelocity = Vector3.zero;
            level.ball.angularVelocity = Vector3.zero;
            level.ball.isKinematic = true;
        }

        private void ReleaseBall()
        {
            if (level == null || level.ball == null)
                return;

            level.ball.linearVelocity = Vector3.zero;
            level.ball.angularVelocity = Vector3.zero;
            level.ball.isKinematic = false;
        }

        private void RestoreParts()
        {
            if (!stateCached || animatedParts == null)
                return;

            for (int i = 0; i < animatedParts.Length; i++)
            {
                if (animatedParts[i] != null)
                    animatedParts[i].localPosition = authoredLocalPositions[i];
            }
        }

        private void CancelReveal()
        {
            bool wasPlaying = IsPlaying;
            if (revealRoutine != null)
                StopCoroutine(revealRoutine);

            revealRoutine = null;
            RestoreParts();
            IsPlaying = false;

            if (wasPlaying)
                ReleaseBall();
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
                CancelReveal();
        }

        private void OnValidate()
        {
            startDelay = Mathf.Max(0f, startDelay);
            partDuration = Mathf.Max(0.05f, partDuration);
            partStagger = Mathf.Max(0f, partStagger);
            revealDistance = Mathf.Max(0f, revealDistance);
            stateCached = false;
        }
    }
}
