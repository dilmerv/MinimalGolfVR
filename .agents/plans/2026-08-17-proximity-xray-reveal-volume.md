## Goal
Create an invisible, radius-configurable reveal volume inside each `ProximitySphere` (children of `VR Club Left` and `VR Club Right`) that makes any object with a chosen tag invisible when inside the radius and visible when outside, with adjustable soft-edge fading. This lets the player push the golf ball through tunnels/occluders and locate it when stuck.

## Success Criteria
- A new component is present on (or inside) both `ProximitySphere` objects under `VR Club Left` (`797155400`) and `VR Club Right` (`716491609`) in `Assets/Scenes/MinimalGolf.unity`.
- Inspector exposes: `radius`, `edge softness/feather`, `target tag` (string popup), optional `invert` and enable toggle. Changing `radius` at runtime/Editor immediately changes the visible hole.
- Any `Renderer` whose `GameObject` (or parent) carries the selected tag is clipped/faded based on world-space distance to the nearest active reveal center: `distance < radius` => fully invisible, `radius .. radius+softness` => smooth fade, `> radius+softness` => fully opaque. Objects without the tag are unaffected.
- Works for both controllers independently and together (two simultaneous spheres). No physical collision introduced (trigger-only).
- Works on URP 17.5.0 / Single-Pass Instanced (Quest) without breaking SRP Batcher for unaffected objects. Frame cost is bounded (O(N_tagged) per frame).
- Console shows 0 errors after domain reload.

## Context And Current Facts
- `VRGolfClub` (`Assets/MinimalGolf/Scripts/VRGolfClub.cs:11`) drives a `ProximitySphere` primitive (`PrimitiveType.Sphere`, radius 0.5 scaled via transform, isTrigger) created in `EnsureSphereVisual()` (`:86`, `:114`). Inspector fields: `sphereRadius` (0.05), `sphereOpacity`, `sphereColor`, `showSphere`. Visual updates in `UpdateSphereVisual()` (`:207`) via `localScale = diameter`.
- Scene `Assets/Scenes/MinimalGolf.unity:11142` and `:26142` define two `ProximitySphere` GOs (scale 0.1) parented to `VR Club Right` (`716491609`) and `VR Club Left` (`797155400`). Both parents have `VRGolfClub` with controller `RTouch=2` / `LTouch=1`.
- No existing x-ray/reveal shader or tag-filtered culling. `MinimalGolfToon.shader` is opaque URP Toon (`RenderType=Opaque`, `Queue=Geometry`, `UniversalForward` + `ShadowCaster`). Golf ball prefab `Assets/MinimalGolf/Prefabs/GolfBall.prefab` and course meshes use this or URP Lit. Tag list (`ProjectSettings/TagManager.asset`) currently has only QDS/Quest default tags; a new tag like `RevealOccluder` will need to be added.
- Quest target uses URP 17.5.0 (`Packages/manifest.json`), Stereo Single-Pass Instanced required.

## Constraints And Non-goals
- Constraints: Must not create physical collision (stay `isTrigger`). Must work in `Meta XR Simulator` and on device. Must not require replacing every course material manually if possible; prefer additive approach. Must be stereo-safe (use `GetWorldSpaceViewDir`, no screen-space tricks that break SPI).
- Non-goals: No secondary camera, stencil-mask window, or depth-peeling. No change to `VRGolfClub` pull/shoot logic. No persistent modification of shared materials on disk (runtime MPB or global properties only). No auto-discovery of tunnel meshes beyond tag filter.

## Key Decisions
| # | Decision | Recommended | Alternatives Rejected | Why |
|---|----------|-------------|----------------------|-----|
| 1 | Where volume lives | New `ProximityRevealVolume` MonoBehaviour on an invisible child of each `ProximitySphere` (e.g., `RevealVolume`), referencing parent `VRGolfClub` transform for center. Keeps `ProximitySphere` visual untouched. | Extend `VRGolfClub` directly; put component on `ProximitySphere` itself. | Separation: visual sphere (`sphereRadius` 0.05 gameplay hint) vs reveal radius (0.3-1.5 m for tunnel x-ray) — independent tuning. Child GO allows zero renderer, pure logical volume, and preserves existing `EnsureSphereVisual` without churn. |
| 2 | Rendering technique | **Dual-track:** (A) Per-pixel clipping in a shader variant for high quality + (B) CPU fallback via `MaterialPropertyBlock` alpha for shaders that don't support variant. Global shader vectors `_RevealCenter0/1`, `_RevealRadiusSoftness0/1` drive (A); a central `RevealVolumeManager` drives (B) by setting `_BaseColor.a`/`_Color.a` via MPB based on distance to renderer bounds center. | Stencil buffer window; URP Renderer Feature full-screen mask; per-object replacement material. | Stencil requires Renderer Feature + extra draw + breaks SRP Batcher; fullscreen feature is overkill for two small spheres and hard to limit by tag. Replacement materials break batching and asset workflow. Global vector + keyword works on Quest and keeps unaffected objects batched. |
| 3 | Tag filtering | `string targetTag` with `TagField` + `CompareTag` early-out. Manager caches `Renderer[]` for tag via `FindGameObjectsWithTag` on enable + optional `Register` for dynamic spawns. Invert flag allows "inside visible / outside invisible" if requested later. | Layer-based filtering; shader-only filtering with no C# tag check. | User explicitly requested tag. Tag check is cheapest and avoids occupying a scarce layer. Layer approach would collide with existing `Water/UI` layers. |
| 4 | Softness model | `edgeSoftness` (m) in world space; shader uses `smoothstep(radius, radius+softness, dist)` to compute `revealMask`; fragment alpha = 1 - mask (inverted for inside-invisible). Fallback MPB lerps same value to material alpha. Range clamped `[0, radius]` with default 0.12 m. | Hard clip (`clip(dist - radius)`), screen-space feather, dither fade. | Smoothstep gives artist-friendly soft edge without dither noise; matches user request verbatim. Hard clip looks harsh in VR. |
| 5 | Inside vs outside | Default: `inside invisible` (`distance < radius` => alpha 0) to reveal stuck ball through tunnel wall. Expose `bool invertInside` for opposite. | Single fixed mode. | User wording: "only what is outside this radius will render, anything inside will become invisible" — default matches. Invert covers future "spotlight reveal" variant. |
| 6 | Invisible volume representation | Empty GO with no `Renderer`, optional `SphereCollider(isTrigger)` radius 0.5 scaled to `revealRadius` for gizmo/debugging only; actual culling driven by shader/math, not collider. | Use the `ProximitySphere` mesh itself scaled to reveal radius. | Keeps gameplay proximity sphere small (0.05 m) while reveal sphere can be 0.5-1.0 m without coupling. |

## Recommended Approach
**New files (3-4):**
1. `Assets/MinimalGolf/Scripts/ProximityRevealVolume.cs` — per-controller volume. Fields: `[Range 0.1, 3.0] revealRadius`, `[Range 0, 0.5] edgeSoftness`, `string targetTag = "RevealOccluder"`, `bool enabledReveal = true`, `bool invertInside = false`, `bool affectInactive = false`. On `OnEnable/OnDisable/OnValidate` registers with singleton `RevealVolumeManager`. Gizmos: wire sphere + feathered alpha preview (reuses `OnDrawGizmosSelected` pattern from `VRGolfClub:379`). No `Renderer` created; if child lacks collider, add trigger `SphereCollider` scaled via `transform.localScale` only for debug picking.

2. `Assets/MinimalGolf/Scripts/RevealVolumeManager.cs` — singleton, `ExecuteAlways` so Editor preview works. Each `Update` (or `LateUpdate`) pushes globals: `Shader.SetGlobalVector("_RevealCenter0", leftPos)`, `Shader.SetGlobalFloat("_RevealRadius0", r0)`, `Shader.SetGlobalVector("_RevealSoftness0", soft0)`, same for `_...1` and `_RevealCount`. Also iterates cached tagged renderers and sets MPB alpha for fallback path: `dist = min(distance(renderer.bounds.center, center0), distance(...,center1))`, `mask = smoothstep(r, r+soft, dist)`, `alpha = invert ? mask : 1-mask`. Uses `MaterialPropertyBlock` per renderer (no `sharedMaterial` leak). Caches renderers on `OnEnable` + `Transform.hasChanged` scan every 0.5 s or via `RegisterOccluder` call for runtime spawns.

3. `Assets/MinimalGolf/Shaders/RevealClip.shader` or variant include `RevealClip.hlsl` injected into `MinimalGolfToon.shader` via `#pragma shader_feature_local _REVEAL_CLIP` + fragment early-clip. Minimal change: add `CBUFFER` globals, compute `float d0 = distance(positionWS, _RevealCenter0.xyz)` (and d1), `float mask = smoothstep(_RevealRadius, _RevealRadius+_RevealSoftness, min(d0,d1))`, then `if (_RevealEnabled) alpha *= ( _Invert ? mask : 1-mask )` and `clip(alpha - 0.001)` or lerp to `0`. For SRP Batcher compatibility, globals are `CBUFFER(UnityPerFrame)` not per-material. For tagged-only effect, shader still runs on all objects but early-out `if (!_RevealAffectsTag) return baseAlpha`; the C# manager sets `_RevealAffectsTag` via keyword or global bool per material — simpler: manager sets per-renderer keyword `_REVEAL_CLIP` only on tagged renderers (`renderer.material.EnableKeyword` via MPB keyword? On URP 17, MPB keywords are per-renderer). Alternative: manager sets `renderer.tag` check controls MPB alpha only, shader controls per-pixel softness for tagged objects that use the Reveal variant; untagged objects use normal variant without cost.

4. `Assets/MinimalGolf/Editor/ProximityRevealVolumeEditor.cs` — custom inspector: radius slider with scene handle (`Handles.RadiusHandle`), softness slider constrained to `<= radius`, Tag popup (`EditorGUI.TagField`), preview toggle.

**Patch existing:**
- `VRGolfClub.cs` — optional 5-line hook: after `EnsureSphereVisual()`, call `EnsureRevealVolume()` that creates/gets `ProximityRevealVolume` child if missing (mirrors existing pattern `:86`). Guarded with `#if UNITY_EDITOR` for Undo similar to `:123`. No change to aiming logic.
- Optionally add tag `RevealOccluder` to `ProjectSettings/TagManager.asset` (or document manual add) and tag tunnel/rail meshes in scene (e.g., children under `COURSE` and `RAILS` Transforms `:721425388`, `:792012494`) — not auto-retagged by code, left to authoring.

**Why dual-track:** Quest URP batching; many Kenney kit meshes share one material. Forcing a shader variant on all of them would break batching even when effect disabled. So per-tag keyword limits variant to occluders only; untagged objects never pay the cost. Fallback MPB path ensures even Standard/URP Lit occluders fade correctly without requiring shader recompile.

## Work Plan
**Phase 1 — Core volume & manager (no shader yet, MPB alpha only)**
- Step 1.1: Create `ProximityRevealVolume.cs` with fields, gizmo, registration API, `GetWorldCenter()` = `transform.position` (works with parent scale). Add `RequireComponent` none, hide renderer.
- Step 1.2: Create `RevealVolumeManager.cs` singleton, tag cache (`Dictionary<string, List<Renderer>>`), MPB update loop, `Shader.SetGlobal*` pushes, edit-mode support (`[ExecuteAlways]`).
- Step 1.3: Extend `VRGolfClub.EnsureSphereVisual/EnsureRevealVolume` to auto-create `RevealVolume` child under `ProximitySphere` on both clubs (same pattern as `CreatePrimitive`). Validate `editor_status ready`.
- Dependencies: 1.3 after 1.1.
- Validation: Play mode with a cube tagged `RevealOccluder` placed over ball, verify alpha fade as club approaches.

**Phase 2 — Shader per-pixel soft clip**
- Step 2.1: Create `RevealClip.hlsl` include (distance + smoothstep + invert + count). Add `MinimalGolfToon_Reveal` variant or patch `MinimalGolfToon.shader` to `#include "RevealClip.hlsl"` in ForwardLit fragment before `return`. Add globals `float4 _RevealCenter0/1; float _RevealRadius0/1, _RevealSoftness0/1; int _RevealCount; int _RevealEnabled;`.
- Step 2.2: Wire manager to enable ` _REVEAL_CLIP` keyword only on tagged renderers; set global enable flag. Ensure `ShadowCaster` pass respects same clip (so shadows of hidden occluder don't render).
- Dependencies: Phase 1.
- Validation: Visual compare — rotating sphere near tagged tunnel wall shows soft circular hole, not pop.

**Phase 3 — Authoring & polish**
- Step 3.1: Add `RevealOccluder` tag to `TagManager` and tag actual tunnel/rail prefabs in scene (or provide context menu `Tools/MinimalGolf/Tag Selected as RevealOccluder`). Document in inspector tooltip.
- Step 3.2: Editor handle + custom inspector for radius/softness, plus `TestDashboard` entry if present.
- Step 3.3: Performance pass: throttle manager to `WaitForSeconds 0.016` equivalent, early-out if `_RevealEnabled==0`, use `Bounds` distance not `Renderer.bounds` per frame for static occluders.
- Dependencies: Phase 2.
- Validation: On Quest, capture `metavr perf capture` or Profile, verify no SRP Batcher break on untagged objects.

## Validation Plan
- Compile check: `unity command editor_status` must report `status: ready`; `unity command get_console_logs --severity Error --limit 100` shows 0 errors.
- Scene check: Open `Assets/Scenes/MinimalGolf.unity`, select `VR Club Left/ProximitySphere/RevealVolume` and `VR Club Right/ProximitySphere/RevealVolume`, inspector shows `ProximityRevealVolume` with radius/softness/tag fields.
- Manual play: In `Meta XR Simulator`, move either controller near a tagged wall segment; wall fades within radius, hard edge softened by `edgeSoftness`. Move away — wall returns fully opaque. Untagged floor stays opaque. Ball (if not tagged) remains visible; if ball is tagged, invert toggle tested.
- Two-controller test: Both spheres active simultaneously; closest distance wins (pillar hidden if near either hand).
- Fallback test: Assign a Standard material cube with tag — MPB alpha path still fades (no shader variant).
- Performance: Capture Game view `unity command screenshot --view game --output Temp/reveal_validation.png --width 1280 --height 720`, inspect hole feathering visually before report.

## Risks / Rollback
- **SRP Batcher invalidation:** Per-renderer keywords break batching for tagged objects only — acceptable. Mitigation: keep shader variant keyword local, not global; untagged objects never enable it. Rollback: disable manager or clear globals, materials revert (MPB cleared on disable).
- **Tag missing:** If `targetTag` not defined, `CompareTag` throws. Mitigation: `OnValidate` checks `InternalEditorUtility.tags` and warns; manager silently no-ops if tag has 0 renderers.
- **Shader compile failure on Quest (GLES/Vulkan):** New `distance`/`smoothstep` in fragment is trivial, but keyword explosion could strip variant. Mitigation: shader_feature_local, not multi_compile.
- **Invisible but still casts shadow / occludes:** Must clip `ShadowCaster` pass too. Mitigation: apply same reveal mask there.
- **Rollback:** Delete `ProximityRevealVolume` components and `RevealVolumeManager` GO; set `_RevealEnabled=0` globally; reimport `MinimalGolfToon.shader` from git.

## Open Questions
- None blocking. Defaults assumed: `targetTag = "RevealOccluder"` (new tag), `revealRadius = 0.6 m`, `edgeSoftness = 0.15 m`, `invertInside = false` (inside invisible). Confirm with requester if preference is opposite (inside visible) or different tag name.
- Confirm whether the golf ball itself should ever be tagged (request says "any object with a specific tag" — likely tunnel/rails, not ball). Ball fading inside radius would make debugging harder; recommend occluders only.
- Confirm whether radius should follow `ProximitySphere` `sphereRadius` or be independent — plan proposes independent, larger.

## Sources
- No external URLs inspected for this plan — decisions based on workspace files (`VRGolfClub.cs`, `MinimalGolf.unity`, `MinimalGolfToon.shader`, `TagManager.asset`, `Packages/manifest.json` URP 17.5.0) verified via reads during this session.
