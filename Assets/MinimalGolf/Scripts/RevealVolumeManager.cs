using System.Collections.Generic;
using System.Linq;
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
        private readonly HashSet<Renderer> _keywordEnabled = new HashSet<Renderer>();
        private bool _leakCleared;
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
            _leakCleared = false;
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
            foreach (var r in _keywordEnabled)
                if (r != null) SetRevealKeyword(r, false);
            foreach (var r in _taggedRenderers)
                if (r != null && !_keywordEnabled.Contains(r)) SetRevealKeyword(r, false);
            _keywordEnabled.Clear();
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
            // One-time leak cleanup: clear any renderer that has _REVEAL_CLIP ON but is not tagged
            // (covers edit-mode leaked instances from prior patches)
            if (active.Count > 0 && !_leakCleared && Application.isPlaying)
            {
                var allForLeak = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                var currentForLeak = new HashSet<Renderer>(_taggedRenderers);
                // If _taggedRenderers not yet built this frame, build it
                if (currentForLeak.Count == 0 && _keywordEnabled.Count == 0)
                {
                    // Force refresh to populate _taggedRenderers
                    RefreshTaggedCache(force: true);
                    currentForLeak = new HashSet<Renderer>(_taggedRenderers);
                }
                foreach (var r in allForLeak)
                {
                    if (r == null) continue;
                    if (r.GetComponent<ProximityRevealVolume>() != null) continue;
                    if (currentForLeak.Contains(r)) continue;
                    // Check instance keyword and clear
                    bool hasKw = false;
                    foreach (var m in r.sharedMaterials) if (m != null && m.IsKeywordEnabled("_REVEAL_CLIP")) { hasKw = true; break; }
                    if (!hasKw)
                    {
                        try {
                            var mats = r.materials;
                            foreach (var m in mats) if (m != null && m.IsKeywordEnabled("_REVEAL_CLIP")) { hasKw = true; break; }
                        } catch {}
                    }
                    if (hasKw) SetRevealKeyword(r, false);
                }
                _leakCleared = true;
            }

            if (active.Count == 0)
            {
                // Immediately restore when no grip/volumes active. The previous
                // delayed restore (0.5s) kept whole-object MPB alpha at 0 while
                // shader globals were already disabled, causing sticky invisibility
                // and masking the per-pixel clip.
                if (_taggedRenderers.Count > 0 || _mpbByRenderer.Count > 0)
                    RestoreAll();
                _scanTimer = 0f;
                return;
            }

            // Tag cache refresh - use joined sorted tags for change detection
            var tagsForUpdate = new HashSet<string>();
            foreach (var v in active) if (!string.IsNullOrEmpty(v.targetTag)) tagsForUpdate.Add(v.targetTag);
            var sortedUpd = new List<string>(tagsForUpdate);
            sortedUpd.Sort();
            string joinedUpd = string.Join(",", sortedUpd);
            bool tagChanged = joinedUpd != _cachedTag;
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

            // Use sorted joined tags for change detection (supports multi-tag union)
            var sorted = new List<string>(tags);
            sorted.Sort();
            string joined = string.Join(",", sorted);
            if (!force && joined == _cachedTag && _taggedRenderers.Count > 0) return;
            _cachedTag = joined;

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
            // Exclusive gating: ONLY tagged renderers get _REVEAL_CLIP.
            if (!Application.isPlaying)
                return;
            var current = new HashSet<Renderer>(_taggedRenderers);
            // Brute-force: for every renderer, force keyword to match tag state.
            // This clears any leaked instances from prior edit-mode r.materials calls.
            foreach (var r in allRenderers)
            {
                if (r == null) continue;
                if (r.GetComponent<ProximityRevealVolume>() != null) continue;
                bool should = current.Contains(r);
                // Force sync - SetRevealKeyword internally checks kw vs should and only writes if changed,
                // but we must call it for untagged to guarantee they are OFF even if never tracked.
                SetRevealKeyword(r, should);
                if (should) _keywordEnabled.Add(r);
                else _keywordEnabled.Remove(r);
            }
            _keywordEnabled.RemoveWhere(r => r == null);
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

        private static bool UsesRevealClipShader(Renderer r)
        {
            if (r == null) return false;
            // MinimalGolfToon is the per-pixel path; for those we must not apply
            // whole-object MPB alpha (bounds.center) which hides the entire mesh
            // when its center enters the sphere instead of clipping per fragment.
            var mats = r.sharedMaterials;
            if (mats == null || mats.Length == 0) return false;
            foreach (var m in mats)
            {
                if (m == null || m.shader == null) continue;
                if (m.shader.name == "Minimal Golf/Toon") return true;
                // Fallback: if keyword was successfully enabled, shader supports it
                if (m.IsKeywordEnabled("_REVEAL_CLIP")) return true;
            }
            return false;
        }

        private void ApplyMPB(List<ProximityRevealVolume> active)
        {
            // For each tagged renderer, compute min distance to any active volume
            foreach (var r in _taggedRenderers)
            {
                if (r == null) continue;
                // Per-pixel shader path (MinimalGolfToon + _REVEAL_CLIP) already does
                // correct fragment clipping. Skip MPB for those renderers to avoid
                // whole-object hiding based on bounds.center.
                if (UsesRevealClipShader(r))
                {
                    // Ensure any previous MPB from before the fix is cleared
                    if (_mpbByRenderer.ContainsKey(r))
                    {
                        r.SetPropertyBlock(null);
                        _mpbByRenderer.Remove(r);
                        _originalBaseColor.Remove(r);
                    }
                    if (r.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off)
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    continue;
                }
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
            }
            foreach (var r in _keywordEnabled)
            {
                if (r == null) continue;
                r.SetPropertyBlock(null);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                SetRevealKeyword(r, false);
            }
            // Also clear any leftover MPB blocks from renderers that may have
            // been removed from _taggedRenderers but still have faded alpha
            foreach (var kv in _mpbByRenderer)
            {
                if (kv.Key != null && !_taggedRenderers.Contains(kv.Key) && !_keywordEnabled.Contains(kv.Key))
                {
                    kv.Key.SetPropertyBlock(null);
                    kv.Key.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
            }
            _taggedRenderers.Clear();
            _keywordEnabled.Clear();
            _mpbByRenderer.Clear();
            _originalBaseColor.Clear();
            _cachedTag = "";
        }

        private void CleanupRenderer(ProximityRevealVolume v)
        {
            // When a volume is removed, we will refresh cache next frame
            _scanTimer = 10f;
        }
    }
}
