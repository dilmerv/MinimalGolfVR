using System.Collections.Generic;
using UnityEngine;

namespace MinimalGolf
{
    /// <summary>
    /// Central manager that drives the x-ray reveal effect.
    /// - Pushes global shader properties for the per-pixel shader path (_RevealCenter0/1, radius, softness).
    /// - Falls back to MaterialPropertyBlock alpha-fading for any shader (URP Lit, Standard, Toon) for tagged renderers.
    /// Two volumes (Left/Right) are supported; effect is the union (closest distance wins).
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(100)]
    public sealed class RevealVolumeManager : MonoBehaviour
    {
        private static readonly List<ProximityRevealVolume> Volumes = new List<ProximityRevealVolume>(4);
        private static RevealVolumeManager _instance;

        // Shader global IDs
        private static readonly int ID_RevealCenter0 = Shader.PropertyToID("_RevealCenter0");
        private static readonly int ID_RevealRadius0 = Shader.PropertyToID("_RevealRadius0");
        private static readonly int ID_RevealSoftness0 = Shader.PropertyToID("_RevealSoftness0");
        private static readonly int ID_RevealCenter1 = Shader.PropertyToID("_RevealCenter1");
        private static readonly int ID_RevealRadius1 = Shader.PropertyToID("_RevealRadius1");
        private static readonly int ID_RevealSoftness1 = Shader.PropertyToID("_RevealSoftness1");
        private static readonly int ID_RevealCount = Shader.PropertyToID("_RevealCount");
        private static readonly int ID_RevealEnabled = Shader.PropertyToID("_RevealEnabled");
        private static readonly int ID_RevealInvert = Shader.PropertyToID("_RevealInvert");

        // Material property IDs for MPB fallback
        private static readonly int ID_BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ID_Color = Shader.PropertyToID("_Color");
        private static readonly int ID_BaseMap = Shader.PropertyToID("_BaseMap");

        private readonly Dictionary<Renderer, MaterialPropertyBlock> _mpbByRenderer = new Dictionary<Renderer, MaterialPropertyBlock>();
        private readonly Dictionary<Renderer, Color> _originalBaseColor = new Dictionary<Renderer, Color>();
        private readonly List<Renderer> _taggedRenderers = new List<Renderer>(128);
        private string _cachedTag = "";
        private float _scanTimer;

        public static void Register(ProximityRevealVolume v)
        {
            if (v == null) return;
            if (!Volumes.Contains(v)) Volumes.Add(v);
            EnsureInstance();
        }

        public static void Unregister(ProximityRevealVolume v)
        {
            if (v == null) return;
            Volumes.Remove(v);
            if (_instance != null) _instance.CleanupRenderer(v);
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;
            _instance = FindFirstObjectByType<RevealVolumeManager>();
            if (_instance != null) return;
            var go = new GameObject("~RevealVolumeManager");
            go.hideFlags = HideFlags.DontSaveInEditor;
            _instance = go.AddComponent<RevealVolumeManager>();
            // Don't keep in scene file when created in edit mode via Register
            if (Application.isPlaying) DontDestroyOnLoad(go);
        }

        private void OnEnable()
        {
            _instance = this;
            RefreshTaggedCache(force: true);
        }

        private void OnDisable()
        {
            // Restore all MPBs and keywords
            foreach (var kv in _mpbByRenderer)
            {
                if (kv.Key != null)
                    kv.Key.SetPropertyBlock(null);
            }
            foreach (var r in _taggedRenderers)
                SetRevealKeyword(r, false);
            _mpbByRenderer.Clear();
            _originalBaseColor.Clear();
            Shader.SetGlobalInt(ID_RevealEnabled, 0);
            Shader.SetGlobalInt(ID_RevealCount, 0);
            if (_instance == this) _instance = null;
        }

        private void OnValidate()
        {
            _scanTimer = 0f;
        }

        private void Update()
        {
            // Ensure instance ref
            if (_instance == null) _instance = this;

            // Collect active volumes - grip-gated via IsRevealActive
            var active = new List<ProximityRevealVolume>(2);
            for (int i = Volumes.Count - 1; i >= 0; i--)
            {
                var v = Volumes[i];
                if (v == null) { Volumes.RemoveAt(i); continue; }
                if (!v.IsRevealActive) continue;
                if (string.IsNullOrEmpty(v.targetTag)) continue;
                active.Add(v);
            }

            // Push shader globals (always, even if no tagged renderers, for shader path)
            PushGlobals(active);

            if (active.Count == 0)
            {
                // Restore any previously faded renderers when no volumes active
                if (_taggedRenderers.Count > 0 && _scanTimer > 0.5f)
                    RestoreAll();
                return;
            }

            // Tag cache refresh (tag may differ per volume; union of tags)
            // For simplicity, we use the first volume's tag as primary, but also handle mixed tags by scanning all.
            string primaryTag = active[0].targetTag;
            bool tagChanged = primaryTag != _cachedTag;
            _scanTimer += Time.deltaTime;
            if (tagChanged || _scanTimer > 0.5f || _taggedRenderers.Count == 0)
            {
                RefreshTaggedCache(force: tagChanged);
                _scanTimer = 0f;
            }

            // Apply MPB fading per renderer
            ApplyMPB(active);
        }

        private void PushGlobals(List<ProximityRevealVolume> active)
        {
            int count = Mathf.Min(active.Count, 2);
            Shader.SetGlobalInt(ID_RevealCount, count);
            Shader.SetGlobalInt(ID_RevealEnabled, count > 0 ? 1 : 0);
            // Invert is taken from first volume (assume both same); if mixed, OR
            int invert = 0;
            foreach (var v in active) if (v.invertInside) { invert = 1; break; }
            Shader.SetGlobalInt(ID_RevealInvert, invert);

            if (count > 0)
            {
                var v0 = active[0];
                Shader.SetGlobalVector(ID_RevealCenter0, v0.WorldCenter);
                Shader.SetGlobalFloat(ID_RevealRadius0, v0.EffectiveRadius);
                Shader.SetGlobalFloat(ID_RevealSoftness0, v0.EffectiveSoftness);
            }
            else
            {
                Shader.SetGlobalVector(ID_RevealCenter0, new Vector4(0, -1000, 0, 0));
                Shader.SetGlobalFloat(ID_RevealRadius0, 0f);
                Shader.SetGlobalFloat(ID_RevealSoftness0, 0f);
            }

            if (count > 1)
            {
                var v1 = active[1];
                Shader.SetGlobalVector(ID_RevealCenter1, v1.WorldCenter);
                Shader.SetGlobalFloat(ID_RevealRadius1, v1.EffectiveRadius);
                Shader.SetGlobalFloat(ID_RevealSoftness1, v1.EffectiveSoftness);
            }
            else
            {
                Shader.SetGlobalVector(ID_RevealCenter1, new Vector4(0, -1000, 0, 0));
                Shader.SetGlobalFloat(ID_RevealRadius1, 0f);
                Shader.SetGlobalFloat(ID_RevealSoftness1, 0f);
            }
        }

        private void RefreshTaggedCache(bool force)
        {
            // Collect union of tags from grip-gated active volumes
            var tags = new HashSet<string>();
            foreach (var v in Volumes)
            {
                if (v != null && !string.IsNullOrEmpty(v.targetTag) && v.IsRevealActive)
                    tags.Add(v.targetTag);
            }
            if (tags.Count == 0)
            {
                // Disable keyword on previously tagged
                foreach (var prev in _taggedRenderers)
                    SetRevealKeyword(prev, false);
                _taggedRenderers.Clear(); _cachedTag = ""; return;
            }

            // Primary tag for change detection
            string first = "";
            foreach (var t in tags) { first = t; break; }
            if (!force && first == _cachedTag && _taggedRenderers.Count > 0) return;
            _cachedTag = first;

            // Keep previous set to diff keywords
            var previous = new HashSet<Renderer>(_taggedRenderers);
            // Rebuild list: find all renderers whose GameObject has any of the tags (including children with tag on parent)
            _taggedRenderers.Clear();
            var allRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in allRenderers)
            {
                if (r == null) continue;
                // Skip the proximity sphere visuals themselves
                if (r.GetComponent<ProximityRevealVolume>() != null) continue;
                // Check tag on renderer GO or any parent up to root (for tagged groups like COURSE)
                Transform t = r.transform;
                bool matched = false;
                while (t != null)
                {
                    foreach (var tag in tags)
                    {
                        if (t.CompareTag(tag)) { matched = true; break; }
                    }
                    if (matched) break;
                    t = t.parent;
                }
                if (matched)
                    _taggedRenderers.Add(r);
            }
            // Enable keyword on new tagged, disable on removed
            var current = new HashSet<Renderer>(_taggedRenderers);
            foreach (var r in current)
                if (!previous.Contains(r)) SetRevealKeyword(r, true);
            foreach (var r in previous)
                if (!current.Contains(r)) SetRevealKeyword(r, false);
        }

        private static void SetRevealKeyword(Renderer r, bool enable)
        {
            if (r == null) return;
            if (!Application.isPlaying) return; // avoid material leak in edit mode - gizmos show preview only
            // Use material instances so only tagged renderers get the variant + double-sided when revealed
            var mats = r.materials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                if (m == null) continue;
                bool kw = m.IsKeywordEnabled("_REVEAL_CLIP");
                if (enable)
                {
                    if (!kw) { m.EnableKeyword("_REVEAL_CLIP"); changed = true; }
                    if (m.HasProperty("_CullMode") && m.GetFloat("_CullMode") != 0f) { m.SetFloat("_CullMode", 0f); changed = true; } // Off = 0
                }
                else
                {
                    if (kw) { m.DisableKeyword("_REVEAL_CLIP"); changed = true; }
                    if (m.HasProperty("_CullMode") && m.GetFloat("_CullMode") != 2f) { m.SetFloat("_CullMode", 2f); changed = true; } // Back = 2
                }
            }
            if (changed) r.materials = mats;
        }

        private void ApplyMPB(List<ProximityRevealVolume> active)
        {
            // For each tagged renderer, compute min distance to any active volume
            foreach (var r in _taggedRenderers)
            {
                if (r == null) continue;
                // Use bounds center for large meshes; worldCenter is more stable for many small meshes
                Vector3 pos = r.bounds.center;
                // For skinned / zero bounds fallback to transform
                if (r.bounds.size == Vector3.zero) pos = r.transform.position;

                bool anyInvert = false;
                foreach (var v in active) if (v.invertInside) anyInvert = true;

                float bestAlpha;
                if (anyInvert)
                {
                    // Inside visible: alpha = 1-mask, visible if inside ANY
                    bestAlpha = 0f;
                    foreach (var v in active)
                    {
                        float d = Vector3.Distance(pos, v.WorldCenter);
                        float rad = v.EffectiveRadius;
                        float soft = v.EffectiveSoftness;
                        float mask = soft <= 0.001f ? (d < rad ? 0f : 1f) : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - rad) / soft));
                        float a = 1f - mask;
                        bestAlpha = Mathf.Max(bestAlpha, a);
                    }
                }
                else
                {
                    // Inside invisible: alpha = mask, invisible if inside ANY => min
                    bestAlpha = 1f;
                    foreach (var v in active)
                    {
                        float d = Vector3.Distance(pos, v.WorldCenter);
                        float rad = v.EffectiveRadius;
                        float soft = v.EffectiveSoftness;
                        float mask = soft <= 0.001f ? (d < rad ? 0f : 1f) : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - rad) / soft));
                        bestAlpha = Mathf.Min(bestAlpha, mask);
                    }
                }

                // Fully visible (far from any volume) -> restore original, no block
                if (bestAlpha >= 0.995f)
                {
                    if (_mpbByRenderer.ContainsKey(r))
                        r.SetPropertyBlock(null);
                    // ensure shadow on
                    if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off)
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    continue;
                }

                // Apply to MPB only when actually fading/hidden
                if (!_mpbByRenderer.TryGetValue(r, out var mpb))
                {
                    mpb = new MaterialPropertyBlock();
                    _mpbByRenderer[r] = mpb;
                    // Cache original color
                    Color orig = Color.white;
                    bool has = false;
                    var mat = r.sharedMaterial;
                    if (mat != null)
                    {
                        if (mat.HasProperty(ID_BaseColor)) { orig = mat.GetColor(ID_BaseColor); has = true; }
                        else if (mat.HasProperty(ID_Color)) { orig = mat.GetColor(ID_Color); has = true; }
                    }
                    if (!has) orig = Color.white;
                    _originalBaseColor[r] = orig;
                }
                r.GetPropertyBlock(mpb);
                Color baseCol = _originalBaseColor.TryGetValue(r, out var oc) ? oc : Color.white;
                baseCol.a *= Mathf.Clamp01(bestAlpha);
                // Transparent fade: set both _BaseColor and _Color if present (only RGB unchanged)
                if (r.sharedMaterial != null)
                {
                    if (r.sharedMaterial.HasProperty(ID_BaseColor)) mpb.SetColor(ID_BaseColor, baseCol);
                    if (r.sharedMaterial.HasProperty(ID_Color)) mpb.SetColor(ID_Color, baseCol);
                }
                else
                {
                    mpb.SetColor(ID_BaseColor, baseCol);
                    mpb.SetColor(ID_Color, baseCol);
                }
                r.SetPropertyBlock(mpb);

                // Optional: disable shadow casting when fully invisible to avoid shadow leak
                // We keep it simple: if alpha < 0.01, disable shadow; else enable.
                // Only affect if renderer is not already configured to not cast.
                if (bestAlpha < 0.02f)
                {
                    if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                else
                {
                    if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off)
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
        }

        private void RestoreAll()
        {
            foreach (var r in _taggedRenderers)
            {
                if (r == null) continue;
                r.SetPropertyBlock(null);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                SetRevealKeyword(r, false);
            }
        }

        private void CleanupRenderer(ProximityRevealVolume v)
        {
            // When a volume is removed, we will refresh cache next frame
            _scanTimer = 10f;
        }
    }
}
