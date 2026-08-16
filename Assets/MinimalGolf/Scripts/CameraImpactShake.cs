using UnityEngine;

namespace MinimalGolf
{
    public sealed class CameraImpactShake : MonoBehaviour
    {
        public static CameraImpactShake Instance { get; private set; }

        [SerializeField] private float cooldown = 0.18f;
        [SerializeField] private float minimumImpact = 0.35f;
        [SerializeField] private float fullImpact = 7.5f;
        [SerializeField, Min(0f)] private float minimumAmplitude = 0.0025f;
        [SerializeField, Min(0f)] private float maximumAmplitude = 0.047f;

        private float nextAllowedImpact;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // VR: replace camera offset with controller haptics.
        public void RegisterImpact(float collisionVelocity)
        {
            if (collisionVelocity < minimumImpact || Time.unscaledTime < nextAllowedImpact)
                return;
            float strength = Mathf.InverseLerp(minimumImpact, fullImpact, collisionVelocity);
            float amp = Mathf.Lerp(minimumAmplitude, maximumAmplitude, strength * strength);
            // Map to haptics amplitude 0.2..0.9
            float haptic = Mathf.Lerp(0.2f, 0.9f, strength);
            OVRInput.SetControllerVibration(0.3f, haptic, OVRInput.Controller.LTouch);
            OVRInput.SetControllerVibration(0.3f, haptic, OVRInput.Controller.RTouch);
            nextAllowedImpact = Time.unscaledTime + cooldown;
        }

        public void ResetCameraPosition()
        {
            // No-op in VR - eye pose driven by OVR
        }
    }
}
