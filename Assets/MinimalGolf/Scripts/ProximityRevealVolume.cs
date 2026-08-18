using UnityEngine;

namespace MinimalGolf
{
    /// <summary>
    /// Invisible reveal volume placed inside ProximitySphere (under VR Club Left/Right).
    /// Any renderer whose GameObject has the configured tag is made invisible when
    /// inside <see cref="revealRadius"/> and visible outside, with soft feathering
    /// over <see cref="edgeSoftness"/> meters.
    /// The actual fading is driven by <see cref="RevealVolumeManager"/> via
    /// MaterialPropertyBlock (fallback) and global shader properties (per-pixel path).
    /// </summary>
    [ExecuteAlways]
    public sealed class ProximityRevealVolume : MonoBehaviour
    {
        [Header("Reveal Shape")]
        [Tooltip("Radius in world meters. Inside this sphere, tagged objects become invisible.")]
        [Range(0.05f, 3f)]
        public float revealRadius = 0.6f;

        [Tooltip("Soft feather width at the boundary. 0 = hard edge.")]
        [Range(0f, 0.8f)]
        public float edgeSoftness = 0.12f;

        [Header("Filtering")]
        [Tooltip("Only objects with this tag are affected. Leave empty to affect none (disabled).")]
        public string targetTag = "RevealOccluder";

        [Tooltip("When true, objects outside are invisible and inside are visible (invert). Default false = inside invisible.")]
        public bool invertInside = false;

        [Header("State")]
        [Tooltip("Master toggle for this volume.")]
        public bool enabledReveal = true;

        [Header("Activation")]
        [Tooltip("When true, reveal only while grip (PrimaryHandTrigger) is held. Prevents always-on x-ray.")]
        public bool requireGrip = true;
        [Range(0f, 1f)]
        [Tooltip("Analog grip threshold (0-1) to consider held.")]
        public float gripThreshold = 0.55f;

        private VRGolfClub _parentClub;

        public bool IsGripHeld
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) return true; // always show in edit mode for gizmo preview
#endif
                var ctrl = Controller;
                try
                {
                    float v = 0f;
                    try { v = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, ctrl); } catch { v = 0f; }
                    if (v > gripThreshold) return true;
                    if (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, ctrl)) return true;
                    // Fallback: Grip button alias
                    if (OVRInput.Get(OVRInput.Button.PrimaryHandTrigger, ctrl)) return true;
                }
                catch { }
#if UNITY_EDITOR
                // Editor fallback: hold G or leftShift to simulate grip for testing without headset
                try
                {
                    if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.G)) return true;
                    if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftShift)) return true;
                }
                catch { }
#endif
                return false;
            }
        }

        public bool IsRevealActive => enabledReveal && (!requireGrip || IsGripHeld) && gameObject.activeInHierarchy && enabled;

        private OVRInput.Controller Controller
        {
            get
            {
                if (_parentClub == null) _parentClub = GetComponentInParent<VRGolfClub>();
                if (_parentClub != null) return _parentClub.controller;
                // Infer from hierarchy name if no club found
                if (transform.parent != null && transform.parent.parent != null)
                {
                    var gp = transform.parent.parent.name;
                    if (gp.Contains("Left")) return OVRInput.Controller.LTouch;
                    if (gp.Contains("Right")) return OVRInput.Controller.RTouch;
                }
                return OVRInput.Controller.RTouch;
            }
        }

        public Vector3 WorldCenter => transform.position;
        public float WorldRadius => Mathf.Max(0.01f, revealRadius * Mathf.Max(transform.lossyScale.x, Mathf.Max(transform.lossyScale.y, transform.lossyScale.z)));
        // Use explicit radius field primarily; keep scale=1 for predictability.
        public float EffectiveRadius => Mathf.Max(0.01f, revealRadius);
        public float EffectiveSoftness => Mathf.Clamp(edgeSoftness, 0f, EffectiveRadius);

        private void OnEnable()
        {
            RevealVolumeManager.Register(this);
        }

        private void OnDisable()
        {
            RevealVolumeManager.Unregister(this);
        }

        private void OnValidate()
        {
            revealRadius = Mathf.Clamp(revealRadius, 0.05f, 3f);
            edgeSoftness = Mathf.Clamp(edgeSoftness, 0f, 0.8f);
            // Clamp softness to avoid exceeding radius (keeps smoothstep sane)
            if (edgeSoftness > revealRadius) edgeSoftness = revealRadius;
            // Auto-register in edit mode when value changes
            if (isActiveAndEnabled)
                RevealVolumeManager.Register(this);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!enabled) return;
            Color c = new Color(0.2f, 0.7f, 1f, 0.22f);
            if (!enabledReveal) c = new Color(0.6f, 0.6f, 0.6f, 0.12f);
            else if (requireGrip && Application.isPlaying && !IsGripHeld) c = new Color(0.6f, 0.6f, 0.6f, 0.10f);
            Gizmos.color = c;
            // Fill for soft edge preview
            Gizmos.DrawSphere(WorldCenter, EffectiveRadius);
            // Wire for hard radius
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.9f);
            Gizmos.DrawWireSphere(WorldCenter, EffectiveRadius);
            if (EffectiveSoftness > 0.001f)
            {
                Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.35f);
                Gizmos.DrawWireSphere(WorldCenter, EffectiveRadius + EffectiveSoftness);
            }
        }

        private void OnDrawGizmos()
        {
            // Light hint when not selected
            if (!enabledReveal) return;
            Gizmos.color = new Color(0.2f, 0.7f, 1f, 0.08f);
            Gizmos.DrawWireSphere(WorldCenter, EffectiveRadius);
        }
#endif
    }
}
