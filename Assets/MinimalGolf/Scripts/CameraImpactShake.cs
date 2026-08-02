using UnityEngine;

namespace MinimalGolf
{
    public sealed class CameraImpactShake : MonoBehaviour
    {
        public static CameraImpactShake Instance { get; private set; }

        [SerializeField] private float cooldown = 0.18f;
        [SerializeField] private float minimumImpact = 0.35f;
        [SerializeField] private float fullImpact = 7.5f;

        [Header("Shake Amount")]
        [SerializeField, Min(0f), Tooltip("Camera offset used for the smallest qualifying impact.")]
        private float minimumAmplitude = 0.0025f;
        [SerializeField, Min(0f), Tooltip("Maximum camera offset produced at or above Full Impact.")]
        private float maximumAmplitude = 0.047f;

        private Vector3 restLocalPosition;
        private float shakeStart;
        private float shakeDuration;
        private float shakeAmplitude;
        private float nextAllowedImpact;

        private void Awake()
        {
            Instance = this;
            restLocalPosition = transform.localPosition;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void LateUpdate()
        {
            if (shakeDuration <= 0f)
                return;

            float elapsed = Time.unscaledTime - shakeStart;
            float normalized = Mathf.Clamp01(elapsed / shakeDuration);
            if (normalized >= 1f)
            {
                shakeDuration = 0f;
                transform.localPosition = restLocalPosition;
                return;
            }

            float envelope = (1f - normalized) * (1f - normalized);
            float phase = elapsed * 75f;
            Vector3 offset = new Vector3(
                Mathf.Sin(phase * 1.17f),
                Mathf.Cos(phase * 1.43f),
                Mathf.Sin(phase * 0.89f)) * (shakeAmplitude * envelope);
            transform.localPosition = restLocalPosition + offset;
        }

        public void RegisterImpact(float collisionVelocity)
        {
            if (collisionVelocity < minimumImpact || Time.unscaledTime < nextAllowedImpact)
                return;

            float strength = Mathf.InverseLerp(minimumImpact, fullImpact, collisionVelocity);
            shakeAmplitude = Mathf.Lerp(minimumAmplitude, maximumAmplitude, strength * strength);
            shakeDuration = Mathf.Lerp(0.065f, 0.16f, strength);
            shakeStart = Time.unscaledTime;
            nextAllowedImpact = Time.unscaledTime + cooldown;
        }

        private void OnValidate()
        {
            minimumAmplitude = Mathf.Max(0f, minimumAmplitude);
            maximumAmplitude = Mathf.Max(minimumAmplitude, maximumAmplitude);
        }

        public void ResetCameraPosition()
        {
            shakeDuration = 0f;
            transform.localPosition = restLocalPosition;
        }
    }
}
