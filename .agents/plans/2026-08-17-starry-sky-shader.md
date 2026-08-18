# Plan: Starry Sky Shader with Configurable Environment Controller + Subtle Procedural Comets

## Goal
Create a fully procedural starry sky shader that covers the entire sky, is fully configurable via a controller component on a GameObject named `Environment`, includes subtle procedurally generated comets, and animates over time (twinkle + slow drift / comet streaks). Must work in current URP setup (PC_RPAsset / Mobile_RPAsset, Unity 6000+ blank URP template) and in VR (OVRCameraRig / XR stereo).

## Success Criteria
- New shader `Assets/MinimalGolf/Shaders/StarrySky.shader` renders stars across the whole sky dome (no textures required, fully procedural) and integrates with URP skybox pipeline or large inverted sphere fallback.
- New `EnvironmentController.cs` (`Assets/MinimalGolf/Scripts/EnvironmentController.cs`) lives on a GameObject named `Environment` in `MinimalGolf.unity`, exposes all star/comet/sky parameters in the Inspector, and drives the material at runtime + in edit mode.
- Stars twinkle and drift/rotate slowly; comets appear procedurally, travel across the sky with a faint tail, very subtle (low frequency, low brightness, short lifetime).
- Material `Assets/MinimalGolf/Materials/StarrySky.mat` is created and assigned as scene skybox (or dome) — visible in Game view without manual setup steps beyond opening the scene.
- No console errors, works on both PC and Mobile URP renderers, preserves existing `MinimalGolfToon.shader` behavior, and does not break VR instancing.

## Context And Current Facts
- Project: `MinimalisticGolf` Unity 6000, URP blank template (`com.unity.template.urp-blank@17.0.14`). [ProjectSettings/GraphicsSettings.asset:58](ProjectSettings/GraphicsSettings.asset:58) points to `UniversalRenderPipelineGlobalSettings` + `PC_RPAsset`. [Assets/Settings/PC_RPAsset.asset](Assets/Settings/PC_RPAsset.asset:1) is URP 17.x, Forward, SRP Batcher on, MSAA 1x, HDR on.
- Only shader today: [Assets/MinimalGolf/Shaders/MinimalGolfToon.shader](Assets/MinimalGolf/Shaders/MinimalGolfToon.shader:1) — Tags `RenderType=Opaque`, `RenderPipeline=UniversalPipeline`, URP Forward. LOD 200, Fog, instancing, stereo correct. Establishes project shader conventions (CBUFFER UnityPerMaterial, Core.hlsl/Lighting.hlsl, stereoinstancing macros).
- Renderers: [Assets/Settings/PC_Renderer.asset](Assets/Settings/PC_Renderer.asset:1) and `Mobile_Renderer.asset` — both Forward, standard URP renderer. Custom outline render feature present but unrelated.
- Scene: [Assets/Scenes/MinimalGolf.unity](Assets/Scenes/MinimalGolf.unity:29) has `m_SkyboxMaterial: {fileID: 10304, guid: 0000000000000000}` (null). Scene lighting uses no skybox; cameras clear to solid colors (`0.46,0.61,0.74` and `0.19,0.30,0.47`). There is **no GameObject named `Environment`** today — `grep m_Name` found `GAME SYSTEMS`, `PLAYER`, `COURSE`, `WARM SUN`, etc., but zero `Environment`. This is a gap the plan must close.
- Target object: user requires a GO named `Environment` to host the controller. Current `GAME SYSTEMS` holds `MinimalGolfGame` + `VRGolfUI`; `WARM SUN` is a directional light. The Environment controller must be additive and not move existing anchors.
- VR: `OVRCameraRig` / `TrackingSpace` / `CenterEyeAnchor` present [Assets/Scenes/MinimalGolf.unity:4670](Assets/Scenes/MinimalGolf.unity:4670). Shader must handle stereo instancing (`UNITY_VERTEX_OUTPUT_STEREO`, `UNITY_SETUP_INSTANCE_ID`) like the Toon shader does.
- No existing sky geometry or skybox material to extend — greenfield within URP.
- Assumption: URP Skybox path (`RenderType=Background`, `Queue=Background`, `LightMode=UniversalForward` or skybox) is preferred over a custom dome mesh to avoid horizon seams and extra draw calls. Fallback dome (inverted sphere) is kept as documented alternative if user prefers mesh control.
- Assumption: Full configurability means Inspector sliders/colors for star field + comet field + sky gradient + animation, all forwarded to shader globals or single shared material via `MaterialPropertyBlock` / `Renderer.sharedMaterial` + `RenderSettings.skybox`.

## Constraints And Non-goals
- Constraints: URP only (no HDRP, no built-in). Must not fork PC/Mobile renderer config; shader must compile under both. Must support Single-Pass Instanced VR. Keep mobile friendly (no expensive raymarching per frame, no compute shader, no extra camera). Must not rename or rewire existing `GAME SYSTEMS`, `WARM SUN`, `COURSE`, `PLAYER`.
- Non-goals: No texture-based star cubemap import workflow; no day/night cycle or sun-disk physics beyond a subtle horizon gradient; no comet gameplay coupling; no editor tooling beyond the controller; no addressables or asset bundles.

## Key Decisions
| Decision | Recommended | Alternatives Rejected & Why |
|---|---|---|
| **Sky coverage technique** | URP Skybox shader (`Shader "MinimalGolf/StarrySky"` with `Tags {"Queue"="Background" "RenderType"="Background"}`) applied to a skybox Material assigned to `RenderSettings.skybox` (and Lighting window Environment) | Inverted sphere mesh: adds geometry, z-fighting with far clip, needs extra GO and scale management, harder to drive via RenderSettings. Fullscreen post-process blit: expensive on Quest, interferes with passthrough/composition layers. Skybox is URP canonical, zero geometry, works in both eyes for free. |
| **Star generation** | Pure procedural HLSL: hash-based Voronoi/cell noise on normalized direction vector (spherical mapping), layered sparkle with `frac(sin(dot(...))*43758.55)` + smoothstep mask + per-star random twinkle via `sin(_Time.y * freq + hash)` | Texture lookup: requires authoring, tiling artifacts, extra memory. Shader Graph: adds dependency, harder to hand-tune comet tails, generates verbose code in this repo which currently uses hand-written `.shader`. |
| **Comet implementation** | Same shader, second procedural layer: very low spawn probability band, elongated streak field. Uses a moving UV strip driven by `_Time.y * CometSpeed` + hash-selected trajectory, `smoothstep` tail fade, additive. Frequency kept ~0.02–0.05 so comets are rare and subtle by default. | Particle system: would need shuriken + extra draw calls, not shader-integrated, harder to keep “very subtle” and globally controllable. Compute shader: overkill, not needed for subtlety, mobile cost. |
| **Animation model** | Stars: two motions — (a) slow sky drift via rotating the spherical coordinate space around Y (`_StarRotationSpeed` deg/sec, ~0.1–0.5 default) and (b) per-star twinkle (`_TwinkleSpeed` + `_TwinkleAmount` modulating brightness). Comets: translate comet UVs linearly across dome (`_CometSpeed`) with lifecycle fade and occasional spawn gap via hash threshold. All driven by ` _Time.y` | Baking animation on CPU: would need script to animate material offset per frame with less visual richness. Vertex animation: irrelevant for skybox. |
| **Controller ↔ material binding** | `EnvironmentController` (`ExecuteAlways`, OnValidate + Update) holds serialized fields, owns a `Material` reference (the skybox instance). On enable/validate it calls `mat.SetFloat/SetColor` for every shader property, and assigns `RenderSettings.skybox = skyboxMaterial` if null. Exposes `skyDomeScale` only if fallback mesh path chosen. Uses `sharedMaterial` in editor to persist; uses instance in play mode to avoid leaking. | Direct material editing by user: error-prone, no time driving, no clamping. ScriptableObject global settings: extra indirection, user asked for GO controller. Shader globals `Shader.SetGlobal*`: spills to all materials, less explicit. |
| **Horizon / atmosphere** | Simple ground-to-sky gradient lerp on `direction.y` with `_HorizonColor`, `_ZenithColor`, `_HorizonHeight`, `_HorizonFalloff`. Stars masked by `saturate(direction.y - horizon)` so no stars below horizon, and comets also culled below horizon | Complex scattering: too heavy, not needed for minimalistic golf aesthetic. |
| **VR stereo correctness** | Mirror `MinimalGolfToon.shader` patterns: `#pragma multi_compile_instancing`, `UNITY_VERTEX_INPUT_INSTANCE_ID`, `UNITY_SETUP_INSTANCE_ID`, `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO`. No view-dependent stars beyond twinkle, so no divergence between eyes. | Ignoring stereo: causes double image / discomfort on Quest. |

## Recommended Approach
1. **Shader — `StarrySky.shader` (hand-written URP HLSL, like Toon):**
   - `Properties` expose: `HorizonColor`, `ZenithColor`, `HorizonHeight`, `HorizonFalloff`, `StarDensity` (0.5–4), `StarSharpness`, `StarIntensity`, `StarColor`, `StarColorVariation`, `TwinkleSpeed`, `TwinkleAmount`, `StarRotationSpeed`, `CometColor`, `CometIntensity` (very low default), `CometSpeed`, `CometLength`, `CometSharpness`, `CometFrequency`, `GlobalTimeScale`.
   - SubShader `Tags { Queue=Background RenderType=Background PreviewType=Skybox RenderPipeline=UniversalPipeline }`, `Cull Off ZWrite Off`, `Pass { Tags { LightMode=UniversalForward } HLSLPROGRAM }` inclusive of `Core.hlsl`.
   - Vertex: pass world direction (`positionOS` normalized) or `viewDir` from skybox vertex (standard skybox trick: `positionOS * 2`). Fragment: compute `dir = normalize(i.dir)`, `horizonMask = smoothstep(_HorizonHeight, _HorizonHeight + _HorizonFalloff, dir.y)`, lerp gradient, then procedural star field:
     - Use spherical UV: `uv = dir.xz / (dir.y + 1)` or octahedral — pick octahedral or equirect for uniform density; implement `hash2(p)` then Voronoi-style nearest distance per cell. `star = pow(saturate(1 - dist * _StarSharpness), 8) * hashBrightness`.
     - Twinkle: `tw = 1 - _TwinkleAmount + _TwinkleAmount * sin(_Time.y * _TwinkleSpeed + hash*6.28)`.
     - Rotate sky slowly: rotate `dir` around Y by `_Time.y * _StarRotationSpeed` before sampling.
   - Comet layer: generate streak field:
     - `cometUV = float2(dot(dir, cometDir), dir.y)` or use 2D noise scrolling: `p = dir.xz * 2 + _Time.y * _CometSpeed * cometVec`.
     - `hash = frac(sin(dot(cell, 127.1))*43758.55)`, threshold `if hash > (1 - _CometFrequency*0.01)` render comet. Tail via `exp(-distAlongTail * _CometSharpness) * smoothstep` and fade head.
     - Keep default `CometFrequency = 0.015`, `CometIntensity = 0.25` so effect is very subtle; document that raising >0.05 floods sky.
   - Output: `gradient + stars*intensity*horizonMask + comets*intensity*horizonMask`. No lighting, no shadows, no fog. Add `UNITY_SETUP_INSTANCE_ID`/`STEREO` macros.
2. **Material — `StarrySky.mat`:** created with shader `MinimalGolf/StarrySky`, defaults: deep navy zenith, warm horizon, `Density ~1.2`, `TwinkleSpeed ~1.2`, `TwinkleAmount ~0.35`, `RotationSpeed ~0.08`, comet subtle defaults. Assigned as `RenderSettings.skybox`.
3. **Controller — `EnvironmentController.cs`:** `namespace MinimalGolf`, `ExecuteAlways`, `[DisallowMultipleComponent]`.
   - `[Header]` groups: Sky Gradient, Stars, Animation, Comets, Advanced.
   - Serialized fields mirror shader properties with `Range` + `ColorUsage` + tooltips; defaults match material.
   - Fields: `skyboxMaterial`, `autoAssignSkybox`, `horizonColor`, `zenithColor`, `horizonHeight (0..0.4)`, `horizonFalloff (0.01..0.5)`, `starDensity (0.5..4)`, `starSharpness (10..200)`, `starIntensity (0..2)`, `starColor`, `colorVariation (0..1)`, `twinkleSpeed (0..5)`, `twinkleAmount (0..1)`, `rotationSpeed (-1..1)`, `cometColor`, `cometIntensity (0..1)`, `cometSpeed (0..2)`, `cometLength (0.1..4)`, `cometSharpness (1..20)`, `cometFrequency (0..0.1)`, `timeScale (0..2)`, `pauseAnimation`.
   - Logic: `OnEnable/OnValidate/Update` → `ApplyToMaterial()` which does `material.SetColor/SetFloat` + optional `Shader.SetGlobalFloat("_StarryGlobalTime", scaledTime)` if shader uses custom time. Also ensures GO name enforcement warning if not `Environment`. Also drives `RenderSettings.skybox` assignment and `DynamicGI.UpdateEnvironment()`.
   - Time: uses `Application.isPlaying ? Time.time : (float)EditorApplication.timeSinceStartup` in editor so stars animate in Scene view.
   - Validation: clamps density/frequency to keep Quest fill-rate bounded; logs warning once if `cometFrequency > 0.08`.
4. **Scene wiring:** Create empty GO `Environment` at root (if missing) in `MinimalGolf.unity`, add `EnvironmentController`, assign `StarrySky.mat`. Document in tooltip that users tune via `Environment` GO — no need to find material.

## Work Plan
### Phase 0 — Groundwork (no code, verify assumptions)
- Re-confirm no `Environment` GO today (done via `grep m_Name`) and that `RenderSettings.skybox` is null. Open `MinimalGolf.unity` in editor once to screenshot current sky color as baseline.

### Phase 1 — Shader (authoritative asset)
- **File:** `Assets/MinimalGolf/Shaders/StarrySky.shader` (+ `.meta` GUID).
- Tasks: Implement Properties, CBUFFER, vertex/fragment as above; add `Fallback Off`; test compile on PC_RPAsset. Keep shader under 150 variants (avoid multi_compile explosion; use only `multi_compile_instancing` + `multi_compile_fog` if needed, but skybox likely no fog).
- Dependency: Must `#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"` — matches [MinimalGolfToon.shader:41](Assets/MinimalGolf/Shaders/MinimalGolfToon.shader:41).
- Done when: Inspector shows all properties, shader compiles without errors on PC and Mobile renderers.

### Phase 2 — Material
- **File:** `Assets/MinimalGolf/Materials/StarrySky.mat` (+ `.meta`).
- Tasks: Create material referencing `MinimalGolf/StarrySky`, set defaults above. Ensure `RenderPipeline` tag correct.
- Dependency: Phase 1.

### Phase 3 — Controller Script
- **File:** `Assets/MinimalGolf/Scripts/EnvironmentController.cs` (+ `.meta`).
- Tasks: Implement `EnvironmentController` as spec'd; add `[ExecuteAlways]` so Scene view animates; add `RequireComponent` none; add `HelpBox`-style tooltip on `Environment` name mismatch; ensure SRP Batcher-friendly property names match shader (`_HorizonColor` etc.). Add `Reset()` to set defaults. Add `OnDestroy` to avoid leaking material instance in play mode.
- Dependency: Phase 1/2 (needs property IDs).

### Phase 4 — Scene Integration
- **File:** `Assets/Scenes/MinimalGolf.unity` (modify).
- Tasks: Add `GameObject: Environment` at root with `Transform` at (0,0,0) and `EnvironmentController` component referencing `StarrySky.mat`; leave existing hierarchy untouched. Optionally set `RenderSettings.m_SkyboxMaterial = StarrySky.mat` via scene serialized `RenderSettings` block if controller's `autoAssignSkybox` is false for persistence. Validate OVRCameraRig cameras still use `ClearFlags: Skybox` or `SolidColor` correctly — if skybox material assigned, ensure main cameras are set to `ClearFlags = Skybox` so skybox renders; for VR, both eyes inherit.
- Dependency: Phases 2/3.

### Phase 5 — Polish & Docs
- Tasks: Add inline comments on all public fields; add header tooltip “Fully configurable via Environment GO”. Update `AGENTS.md` not required. Optional: small `EnvironmentControllerEditor` custom editor to group sliders visually (defer if time — plain Inspector is acceptable for plan acceptance).
- No new tests required (visual feature), but keep repo green.

## Validation Plan
- **Compilation & console:** `unity command editor_status` → `ready`; `unity command get_console_logs --severity Error --limit 100` → zero errors after shader import. Highest-risk check.
- **Shader compile:** Open `StarrySky.mat` Inspector — all properties visible; frame debugger shows skybox draw with `MinimalGolf/StarrySky` on both `PC_Renderer` and `Mobile_Renderer` (switch via Graphics Settings preview if needed).
- **Controller wiring:** In Play mode, `Environment` GO present, `EnvironmentController` shows ~18 tunable fields; dragging `Star Density` from 0.5 to 4 visibly changes sky; `Twinkle Amount` 0 → 1 adds shimmer; toggling `Pause Animation` freezes motion; `Comet Frequency` 0 → stars only, 0.015 → occasional faint streak, 0.08 → warning logged and noticeably over-busy (verify clamping).
- **Animation:** `unity command screenshot --view game --output Temp/starry_before.png --width 1280 --height 720` then wait 2 sec, second screenshot; pixel diff confirms stars/comets moved (not static). Additionally, observe in Scene view with `ExecuteAlways` — stars animate without entering Play.
- **VR path:** `OVRCameraRig` present; verify shader file contains `UNITY_VERTEX_OUTPUT_STEREO` and `UNITY_SETUP_INSTANCE_ID` (grep check), and that building for Android (Quest) does not strip skybox variant (check `PC_RPAsset`/`Mobile_RPAsset` have skybox enabled — they do via forward renderer).
- **Regression:** Existing Toon materials still render correctly; `MinimalGolf.unity` still loads `GAME SYSTEMS` + `COURSE` + `PLAYER` hierarchy unchanged; no rename of existing GOs.

## Risks / Rollback
- **Risk: Skybox not visible because cameras use SolidColor clear.** Mitigation: Controller's `autoAssignSkybox` sets `RenderSettings.skybox` and can optionally flip `Camera.main.clearFlags` to `Skybox` on start (guarded behind `updateCameraClearFlags` bool, default false, documented). Rollback: leave cameras on `SolidColor` and use inverted sphere fallback — plan notes fallback dome variant (scale 500, `Cull Front`) if skybox path is rejected in review.
- **Risk: Star density too high kills Quest fill-rate.** Mitigation: defaults tuned low (`Density 1.2`, `Sharpness 40`, single Voronoi layer), `Star Density` max 4, `CometFrequency` max 0.1 with warning; shader uses `clip`/`saturate` early outs. Rollback: reduce layers by commenting comet pass behind `COMET_ON` keyword (kept as `shader_feature_local`).
- **Risk: Comet tail looks like artifact / too obvious.** Mitigation: defaults `CometIntensity 0.25`, `Length 1.5`, `Frequency 0.015`, additive blend with `* horizonMask * 0.5`; document “very subtle” preset. Rollback: set `Enable Comets` toggle false → `clip(-1)` the comet layer.
- **Risk: Edit-mode animation flicker / material leak.** Mitigation: `ExecuteAlways` + `Application.isPlaying` branch; editor time via `EditorApplication.timeSinceStartup`; use `sharedMaterial` in edit, instantiated copy only in play. Rollback: disable `ExecuteAlways` if instability observed.
- **Rollback path:** Delete `StarrySky.*` files, remove `Environment` GO, clear `RenderSettings.skybox` → scene reverts to prior solid-color sky. No data migration needed.

## Open Questions
- **Skybox vs. dome preference:** User asked for “shader that generates stars all over the sky” — plan assumes URP skybox material (zero geometry). If team prefers explicit dome mesh for horizon control/parallax, confirm — fallback dome can be added in Phase 4 as child sphere under `Environment` without changing shader/controller.
- **Separate horizon light (`WARM SUN`) interaction:** Should zenith/horizon colors track the `WARM SUN` directional light color automatically, or stay fully manual? Plan keeps manual for full configurability; auto-sync could be added later as an optional `syncWithSun` bool.
- **Comet direction:** Plan animates comets along a fixed world-space diagonal with per-cell random rotation. If truly random orbital directions per comet are required, confirm — adds one hash lookup, negligible cost.
- None of these block planning; defaults are reversible and low-risk.

---
*Save location: `.agents/plans/2026-08-17-starry-sky-shader.md`*
