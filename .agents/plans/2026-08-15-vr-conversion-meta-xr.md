## Goal

Convert MinimalisticGolf (Unity 6000.5.6f1, URP 17.5.0, 8 authored courses) from standalone flat-screen (isometric orthographic camera + mouse-drag + IMGUI `OnGUI` + `CustomGameCursor` + keyboard rotation) to a VR-only game using **Meta XR All-in-One SDK**, with `OVRCameraRig`, controller tracking, a tabletop course placed in front/below the player, a physical “club-in-ball” swing, and all UI on World Space canvases.

## Success Criteria

- Meta XR All-in-One SDK imported (UPM) and **Meta XR Project Setup Tool shows 0 errors/warnings for Android and Standalone** — all fix recommendations applied (XR Plug-in Management, color space, graphics APIs, UPM, manifest).
- Scene boots on Quest/Link and in XR Simulator: head tracking + left/right controller poses visible; no console errors.
- Course loads centered **~1.0–1.5 m in front of the TrackingSpace, ~0.6–0.9 m below HMD eye height** (waist-height tabletop), readable without crouching. All 8 levels share that anchor; switching levels does not shift the player.
- Swing: placing either controller collider inside the ball’s trigger volume + holding trigger starts a pull/drag preview (aiming line + power meter); releasing trigger applies `AddForce(dir * Lerp(1.1, maximumImpulse, power), Impulse)` identical to current `ReleaseShot()` tuning. Ball at rest check (`playableSpeed`) still gates shots. Haptics pulse on grab/release/hole.
- No standalone interaction remains: no `Cursor.SetCursor`, no `Mouse.current`/`ScreenPointToRay`/drag plane, no `Keyboard.current` R/arrows/Enter, no orthographic `ISOMETRIC CAMERA`, no `OnGUI` path.
- All former `OnGUI` surfaces reappear as **World Space Canvases** (UGUI + TextMeshPro or UGUI Text with `Inter-Regular`): Minimal Golf identity card, `LEVEL x/y` + course name, `STROKES`/`PAR`, `COURSE PROGRESS` pips, `PUTT STRENGTH` bar, transient `BALL RETURNED`/`HOLE COMPLETE` feedback, `COURSE COMPLETE` end card, and a subtle legend. Canvases are anchored to the course anchor, billboard-light or fixed, readable at 1 m.
- Existing `MinimalGolfGame.GetUIValues` / `DebugLoadLevel` contract still passes existing `Assets/Tests/PlayMode/MinimalGolfPlayModeTests.cs` T1–T4 (shots are applied directly in tests, so VR input must not break core simulation).
- Audio remains functional with listener on `CenterEyeAnchor`; no duplicate `AudioListener`.
- `MinimalGolfSceneBuilder.Build()` (“Minimal Golf/Build Authored Course”) regenerates a VR-correct scene (rig + anchor + canvases) and preserves inspector tuning via `EditorJsonUtility` capture.

## Context And Current Facts

**Project** — `Unity 6000.5.6f1`, `com.unity.render-pipelines.universal@17.5.0`, `com.unity.inputsystem@1.20.0`, `com.unity.pipeline@0.4.0-exp.1`. `/Users/dilmerv/Code/MinimalisticGolf/Packages/manifest.json` lists `com.unity.modules.xr` but no OVR/OpenXR plugin. `ProjectSettings/XRSettings.asset` is empty legacy; `ProjectSettings/ProjectSettings.asset:productName=Minimal Golf`. URP has `PC_RPAsset`/`Mobile_RPAsset` + `PC_Renderer`/`Mobile_Renderer` with existing `Minimal Golf Outlines` `RenderObjects` feature — must stay enabled after rig switch.

**Authored content** — `Assets/Scenes/MinimalGolf.unity` built solely by `Assets/MinimalGolf/Editor/MinimalGolfSceneBuilder.cs:CreateEnvironment` + `CreateLevelRoot` (x8). At edit time levels are splayed at `x = 0,14,30,46,64,84,104,126`; at runtime `MiniGolfLevel.RestoreRuntimeTransform()` in `MinimalGolfGame.LoadLevel` collapses all to `runtimeLocalPosition` (default `Vector3.zero`). Camera is `ISOMETRIC CAMERA` at `(5.1,5.65,-6.9)` euler `(29.9,321,0)`, `orthographicSize=6.4`, `UniversalAdditionalCameraData.renderShadows=false`. `GAME SYSTEMS` holds `MinimalGolfGame`, `CustomGameCursor`, `LineRenderer` aiming line (`Kenney Colormap Toon`), and `AUDIO MANAGER` (`AudioManager` + `AudioListener`).

**Gameplay code (ground truth)**:
- `Assets/MinimalGolf/Scripts/MinimalGolfGame.cs:207-298` — `HandlePointer` drives the shot: `Mouse.leftButton.wasPressed -> TryGetPointerWorld -> dragging=true -> dragStartWorld`; while held `pull = dragStartWorld - currentWorld (y=0)`, `shotPower = clamp01(dist/maximumDragDistance)`, `aimDirection = pull.normalized`, `UpdateAimingLine()` (length `Lerp(0.35,3.2,power)`, color lerp seafoam→gold→orange); `ReleaseShot` applies `AddForce(aimDirection * Lerp(1.1, maximumImpulse, power), Impulse)` and increments strokes. Guard `CanTakeAction()` = not revealing/capturing/levelComplete && `ball.linearVelocity.magnitude <= playableSpeed (0.32)`.
- `TryGetPointerWorld` does `Plane(Vector3.up, ball.position)` + `gameCamera.ScreenPointToRay(screenPos)`. Cup assist: `Assets/MinimalGolf/Scripts/MinimalGolfGame.cs:146-190` `FixedUpdate` pulls toward `holeCenter` inside `assistRadius=1.15` with speed gate, damps, and fires `CaptureBall()`.
- `Assets/MinimalGolf/Scripts/MiniGolfLevel.cs:24-55` — `IsOutsideCourse` via `InverseTransformPoint` bounds; `CacheAuthoredState/RestoreRuntimeTransform`.
- `Assets/MinimalGolf/Scripts/CameraImpactShake.cs:37-82` — shakes `transform.localPosition` per impact; tied to the isometric camera.
- `Assets/MinimalGolf/Scripts/CustomGameCursor.cs:6-44` — `Cursor.SetCursor` — VR must delete.
- `Assets/MinimalGolf/Scripts/MinimalGolfGame.cs:507-548` — entire HUD is `OnGUI` with `GUI.matrix = Scale(Screen.height/500...)`, `DrawIdentityCard/DrawProgressCard/DrawStatsCard/DrawPowerMeter/DrawFeedback/DrawCourseComplete/Legend`. `GetUIValues` exists purely for tests.
- `Assets/InputSystem_Actions.inputactions` has Mouse/Keyboard/Gamepad/Joystick/XR bindings but gameplay code bypasses it and reads `Mouse.current`/`Keyboard.current` directly.

**Tests** — `Assets/Tests/PlayMode/MinimalGolfPlayModeTests.cs` T1–T4 call `game.DebugLoadLevel(i)` and inject shots via `Rigidbody.AddForce` + reflection bumping `levelStrokes/totalStrokes`. They assert `GetUIValues` mirrors internal state and that level completion/advancement still works (with a teleport fallback). Any VR refactor must keep `DebugLoadLevel`, `GetUIValues`, `CurrentLevelIndex/LevelStrokes/IsLevelComplete/IsCapturing` stable.

**No VR present today** — `grep -r OVR Assets` returns nothing. No `OVRManager`, `OVRCameraRig`, interaction components, or OpenXR settings.

## Constraints And Non-goals

- **Use Meta XR All-in-One SDK** via UPM (`com.meta.xr.sdk.all-in-one`). Follow `metavr` skill: use `metavr docs search <topic>` for live API, `metavr device/app/log/capture` for device ops, never raw `adb` for device work. SDK version: latest compatible with Unity 6000.5 (at write ~v70–72); pin exact version after verifying with `metavr docs fetch`.
- **Project Setup Tool must be green for all platforms.** Apply fixes with the tool’s “Apply” — do not hand-edit `ProjectSettings/XRPluginManagementSettings` etc. outside the tool except where tool explicitly expects manual review (e.g., signing). Verify in Unity: `Meta > Tools > Project Setup Tool`.
- **VR-only.** Remove standalone branching — no fallback to mouse/keyboard at runtime. Keep an `UNITY_EDITOR` debug shortcut behind `#if UNITY_EDITOR` only if needed for CI tests; never expose to player.
- **Performance envelope:** Quest 2/3/3S. Single Pass Instanced, `Vulkan` (Android), no MSAA on forward+? Keep `PC_Renderer`/`Mobile_Renderer` outlines working. Maintain 72–90 Hz — URP settings already low shadows (`UniversalAdditionalCameraData.renderShadows=false`, fog linear 15–34).
- **Non-goals:** hand tracking, passthrough/MR, multiplayer, haptics beyond simple pulses, new art, re-tuning course geometry, migrating from URP, switching to XR Interaction Toolkit instead of Interaction SDK (Interaction SDK is the Meta-recommended path for OVRCameraRig).
- **Must not break** `LevelRevealAnimator` (staggered reveal) — it makes ball kinematic during `IsPlaying`; VR code must respect `IsRevealing`/`capturing` gates same as before.

## Key Decisions

| Decision | Recommended | Alternatives rejected | Why |
|---|---|---|---|
| SDK import | `com.meta.xr.sdk.all-in-one` via Package Manager (scoped registry or tarball) + Meta XR Utility package. Use `OVRCameraRig` prefab (or Building Blocks “Camera Rig” block, which spawns the same prefab). | Importing only `com.meta.xr.sdk.core`/`interaction` piecemeal; using OpenXR XR Plug-in without OVRPlugin; using legacy `Oculus XR Plugin` alone | All-in-One is the requested package; it brings OVRPlugin, OVRManager, Interaction SDK, and setup tool together with vetted dependencies. Piecemeal imports drift from docs. |
| XR back-end | **Oculus provider via XR Plug-in Management** enabled for Android (Quest) and optionally Standalone (Link). Tool enables it. | OpenXR Meta feature group only | Project Setup Tool expects Oculus provider for Quest and wires `OVRManager`↔`XRPlug-in`. OpenXR would need extra feature flags and doesn’t give OVRInput. |
| Rig | Single `OVRCameraRig` at origin, `TrackingSpace` origin at floor. Add `OVRManager` on rig (tracking origin = Floor, use recommended `InputFocusAwareness`). Keep `CenterEyeAnchor` as audio listener host. | Using “XR Origin (VR)” + XR Interaction Toolkit | Rider requirement says “OVR Camera Rig, controllers support and tracking” — that’s the OVR prefab. Mixing XRI adds duplicate rig/device management. |
| Controller tracking | OVR-provided anchors `LeftHandAnchor`/`RightHandAnchor` + optional `OVRControllerHelper` or Interaction SDK `Controller` prefabs + `GrabInteractor` visuals. Drive swing logic from the hand anchor’s `Transform`. | Polling `XRNode`/`InputDevices` directly | `OVRInput` + anchors is canonical for OVRCameraRig and gives trigger/grip/haptics without extra XRI. |
| Interaction model | New `VRGolfClub` (or `VRBallInteractor`) per hand: trigger volume (small SphereCollider isTrigger) at club tip, `OnTriggerStay`/`OnTriggerEnter` with ball’s collider. While `OVRInput.Get(Button.PrimaryIndexTrigger, controller)` held **and** overlap → `isAiming=true`, record `aimStartPoint = controller.position` projected to XZ near ball, accumulate `pull = aimStartPoint - currentPoint (y=0)`, clamp to `maximumDragDistance`, compute `shotPower` & `aimDirection` exactly like mouse path, drive existing aiming line. On trigger up → call existing `TryApplyShot(dir,power)` (extracted from `ReleaseShot`). | 3D drag with grabbed Rigidbody + spring joint; ray-based telekinesis | Spec: “position controller within ball, if collision is detected and trigger held then start pull routine, once trigger released force applied, similar to how the game works already.” Overlap+trigger mirrors the flat drag (`pull = start - current`) with minimal physics complexity. |
| Level placement | Add empty **`VRCourseAnchor`** as child of `TrackingSpace` at e.g. `localPos (0, -0.85, 1.3)` and `localScale 0.35–0.5` (tabletop). `MinimalGolfGame` sets each `MiniGolfLevel.transform` parent to the anchor (or copies `runtimeLocalPosition = anchor.TransformPoint(Vector3.zero)`) and removes the old isometric camera. `cameraSize` no longer used; keep field but ignore in VR. Player re-centers via OVR’s recenter, not level rotation. | Keep world at floor scale 1:1 around player | 1:1 putts would need room-scale walking; tabletop “in front beneath eye level” is the asked placement and makes World Space UI legible. Anchor approach re-uses existing `RestoreRuntimeTransform` collapse. |
| Course rotation | Remove `TryRotateLevel`/`← →` entirely. If a VR rotate is desired, map to thumbstick yaw on the anchor (`anchor.Rotate(Y)`), but spec says “Remove any features specific to standalone and focus on VR” → delete rotation unless playtesting proves need. | Port rotation to right thumbstick | Deleting is cleanest per spec; adding a rotation drifts from “remove standalone features.” Thumbstick rotate can be added later behind a flag if comfort testing demands it. |
| UI | Replace `OnGUI` with **World Space Canvases**: one root `VR_UI` under `VRCourseAnchor` (or `TrackingSpace`) with children `IdentityCard`, `ProgressCard`, `StatsCard`, `PowerMeter`, `FeedbackToast`, `CourseCompleteCard`. Each is `Canvas(renderMode=WorldSpace, worldCamera = CenterEyeAnchor camera)`, `CanvasScaler(dynamicPixelsPerUnit=10)`, `GraphicRaycaster` with `OVR Ray Interactor` for the end-card “Play Again” button. Styles reuse `Inter-Regular` + existing palette (PanelColor, OrangeAccent, etc.). Power meter segments driven by `shotPower`. Keep `GetUIValues` for tests; remove `EnsureStyles/OnGUI/Draw*`. | Keep OnGUI alongside VR; convert to Screen Space Overlay in VR | Overlay has no depth in headset; `OnGUI` is incompatible with VR instancing and must be fully removed per “All UI should be converted to VR world space canvases.” |
| Camera shake | Replace `CameraImpactShake` (camera `localPosition` offset) with haptics (`OVRInput.SetControllerVibration`) plus optional subtle rig offset on `TrackingSpace`. Keep class but rewrite to pulse controllers; detach from `CenterEyeAnchor` to avoid fighting OVR’s camera pose. | Keep shaking `CenterEyeAnchor` | Eye anchor is driven by OVR pose each frame; mutating its `localPosition` is overridden or causes double transform drift. |
| Scene build pipeline | Extend `MinimalGolfSceneBuilder.Build()` to: remove/create `OVRCameraRig`, create `VRCourseAnchor`, delete `ISOMETRIC CAMERA` + `CustomGameCursor`, replace `OnGUI` wiring with World Space canvas prefabs, rewire `MinimalGolfGame.gameCamera = CenterEyeAnchor.camera` (or null out and use anchor directly). Preserve `CaptureExistingComponentSettings<MinimalGolfGame>` pattern. | Make VR setup a manual scene edit outside the builder | Builder is the single source of truth for the scene; manual edits diverge and get overwritten next `Build`. |
| Input System | Keep `InputSystem` package (Interaction SDK depends on it) but remove direct `Mouse.current`/`Keyboard.current` reads from `MinimalGolfGame`. If tests need editor drive, gate any sim input behind `UNITY_EDITOR && ENABLE_INPUT_SYSTEM`. | Migrate everything to Input System actions | The current game bypasses actions; a full actions migration is scope without benefit since OVRInput is the runtime input. |

## Recommended Approach

**Phase A — Import & project health (editor-only, reversible):**
1. `metavr docs search "All-in-One SDK install unity 6000"` + fetch install page. Add scoped registry if needed and `Packages/manifest.json` entry `com.meta.xr.sdk.all-in-one : <latest>`; run `Unity -quit -batchmode -executeMethod ...` or just open Editor to resolve. Verify `Assets/Oculus` / `Packages/com.meta.xr.sdk.*` present.
2. Open `Meta > Tools > Project Setup Tool`. Select **All platforms** (Android + Standalone). Click **Apply All** / per-issue Apply. Fixes to expect: enable Oculus XR Plug-in for Android/Standalone, set Android `Minimum API 32`, `Target API Auto`, `IL2CPP`, `ARM64`, `Linear color space`, `Graphics APIs = Vulkan or Vulkan+GLES3`, `Single Pass Instanced`, `V2 signing`, `Install location Automatic`, `Strip Engine Code` handling, add `INTERNET_ACCESS`, etc. Re-run until green.
3. Enable XR Simulator (`Window > XR > XR Simulation` or Meta XR Simulator) per `AGENTS.md` verification.

**Phase B — Rig & anchor (one-time scene work, drives everything):**
4. Delete `ISOMETRIC CAMERA` GameObject. Instantiate `OVRCameraRig` prefab (or Building Blocks > Camera Rig). Configure `OVRManager`: `Tracking Origin = Floor Level`, `Use Recommended MSAA`, `Color Gamut = Quest`, `Hand Tracking Suport = Controllers Only` (hand tracking optional later). Move `AudioListener` from `AUDIO MANAGER` to `CenterEyeAnchor`; remove `RequireComponent(AudioListener)` or tolerate duplicate removal.
5. Create `TrackingSpace/VRCourseAnchor` `GameObject` with transform `pos (0, 0.95, 1.25)` relative to TrackingSpace, `scale (0.42,0.42,0.42)` (tune so `courseLength 10–21m` becomes ~4–9 m table). Add a small `Canvas` backplate beneath for grounding/outline consistency.
6. Patch `MiniGolfLevel.runtimeLocalPosition` handling: either set anchor as parent of each level root after `RestoreRuntimeTransform`, or offset levels by `VRCourseAnchor.position`. Keep `courseWidth/courseLength/IsOutsideCourse` math in level local space (already correct). Keep fog (`RenderSettings.fogStart/End`) but re-tune `fogEndDistance` closer (~8–12 m at table scale) or disable if it washes table.

**Phase C — Swing mechanic (new gameplay code, reuses tuning):**
7. Extract shot impulse logic: refactor `MinimalGolfGame.ReleaseShot()` into `bool TryApplyShot(Vector3 dir, float power)` that does `CanTakeAction()` gate, zeroes ball XZ velocity, `AddForce(dir * Lerp(1.1, maximumImpulse, power), Impulse)`, increments strokes, plays SFX. Both old and new paths call it (old path deleted after).
8. New scripts (under `Assets/MinimalGolf/Scripts/`):
   - `VRGolfClub : MonoBehaviour` — per hand. Refs: `OVRInput.Controller controller (LTouch/RTouch)`, `SphereCollider tipTrigger` (isTrigger, radius ~0.06 m at controller forward), `MinimalGolfGame game`, `LineRenderer aimingLine`. Internally: `bool overlappingBall`, `bool aiming`, `Vector3 aimStartPoint`, `float shotPower`, `Vector3 aimDirection`. Flow:
     ```csharp
     OnTriggerEnter/Stay(other) if other.attachedRigidbody == level.ball => overlappingBall=true
     Update():
       if !CanTakeAction() => cancel aiming
       if overlappingBall && OVRInput.GetDown(PrimaryIndexTrigger, controller) && !aiming) { aiming=true; aimStartPoint = ProjectToBallPlane(controller.position); }
       if aiming && OVRInput.Get(PrimaryIndexTrigger, controller) { curr=Project..; pull=aimStart-start; pull.y=0; dist=min(pull.mag, maxDragDistance); shotPower=dist/maxDragDistance; aimDirection=pull.normalized; UpdateAimingLine(); haptics subtle }
       if aiming && OVRInput.GetUp(PrimaryIndexTrigger, controller) { aiming=false; rangingLine.enabled=false; if shotPower>=0.035f) game.TryApplyShot(aimDirection, shotPower); }
     ```
   - Keeps `maximumImpulse/maximumDragDistance/playableSpeed` as `[SerializeField]` on `MinimalGolfGame` (existing values).
9. Add `SphereCollider` trigger to ball (or rely on ball’s existing `SphereCollider` — just check `overlap` via `Physics.OverlapSphere(tipPos, 0.08)` if preferred). Choose tip trigger so no ball prefab mutation needed beyond a tag/layer.
10. Keep `FixedUpdate` cup assist and `CaptureBall()` identical — they already operate in world space.
11. Remove `HandlePointer`/`HandleKeyboard`/`TryGetPointerWorld`/`TryRotateLevel`/`ResetLevelWithPenalty` keyboard path; `ResetBall` penalty now triggered by UI button only (`R` removed). Update `Update()` to poll only `courseComplete` “Play Again” via UI.

**Phase D — World Space UI:**
12. Delete `OnGUI`, `EnsureStyles`, `Draw*`, `CreateRoundedTexture`, `whiteTexture/roundedTexture/GUIStyle` fields from `MinimalGolfGame` (keep `GetUIValues`). Alternative: keep class but conditional-compile `OnGUI` out with `#if !UNITY_EDITOR` removal; recommended: delete outright then add `VRGolfUI` MonoBehaviour.
13. New `VRGolfUI : MonoBehaviour` drives 5–6 World Space canvases:
    - Each canvas: `WorldSpace`, `Rect 1200x...`, `scale 0.003` (~360 DPI at 1 m), `Sort order` via URP. Use `Assets/MinimalGolf/Fonts/Inter-Regular.otf` via `TextMeshPro` or `Text` with matching `fontSize` from old `CreateLabelStyle` sizes (22 title, 13 heading, 21 stat). Colors: `PanelColor (0.055,0.14,0.19,0.90)`, `PaleText #FAF1D2`, `OrangeAccent #E1822F`, `Gold #F3C96B`, etc.
    - Layout: `IdentityCard` anchored to anchor’s front-left (`-0.9, 0.45, -0.6` local), `StatsCard` front-right, `ProgressCard` top-center, `PowerMeter` bottom-center (visible only while `VRGolfClub.aiming`), `FeedbackToast` center-high with fade `alpha=(feedbackUntil - time)*3.2`, `CourseCompleteCard` centered `0,0,0` with `OVR Ray` + `Button` “Play Again” calling `RestartCourse()`. Provide `OVRRayInteractor` + `Canvas` `GraphicRaycaster` for interaction.
    - Wiring: `MinimalGolfGame` exposes `event Action onUIRefresh` or `VRGolfUI` polls `GetUIValues` + `game.shotPower` (make internal getter). No polling cost concerns at 90 Hz (simple UGUI).
14. `LineRenderer` aiming line stays under `GAME SYSTEMS` but its positions are now driven by `VRGolfClub` (or still via `MinimalGolfGame.UpdateAimingLine` called by the club).

**Phase E — Cleanup & builder finalization:**
15. Delete `CustomGameCursor` component and `Assets/MinimalGolf/UI/MinimalGolfCursor.png` usage. Remove `Cursor.lockState/visible` calls.
16. Rewrite `CameraImpactShake` to call `OVRInput.SetControllerVibration(1, amp, controller)` or simply delete if haptics covered in `VRGolfClub`.
17. Update `AudioManager`: set `sfxSource.spatialBlend = 1.0` (3D) or keep 0 for UI SFX, and ensure `AudioListener` is only on `CenterEyeAnchor`. Handle `DontDestroyOnLoad` persistence across rig recreation.
18. Update `MinimalGolfSceneBuilder.cs` to perform all of the above procedurally: create rig, anchor, UI, club interactors, delete old camera/cursor, set `game.gameCamera = rig.CenterEyeAnchor.GetComponent<Camera>()` (needed for any remaining screen math — now disabled). Keep material/physics/renderer outline creation unchanged.
19. Remove `InputSystem_Actions` mouse/keyboard-driven gameplay assumptions; keep the asset for editor but do not depend on it.

## Work Plan

**Unit 1 — SDK import & green Project Setup Tool (prereq for all)**
- Files: `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/XRPlugInManagementSettings.asset`, `ProjectSettings/ProjectSettings.asset`, `Assets/Oculus/*` (imported).
- Steps: query live Meta docs (`metavr docs search "All-in-One SDK requirements"`), install SDK, open Project Setup Tool, Apply all for Android+Standalone, commit only manifest + ProjectSettings delta.
- Dependencies: none.
- Done when: setup tool shows green checks + console clean.

**Unit 2 — OVR Camera Rig + tracking + course anchor placement**
- Files: `Assets/Scenes/MinimalGolf.unity`, `Assets/MinimalGolf/Editor/MinimalGolfSceneBuilder.cs`, new prefab override `OVRCameraRig`.
- Steps: instantiate rig, configure `OVRManager`, create `VRCourseAnchor`, reparent/offset levels, disable old camera, enable XR Simulator, smoke-test head/controller pose.
- Depends on: Unit 1.

**Unit 3 — VR swing (“club in ball” trigger pull)**
- Files: `Assets/MinimalGolf/Scripts/MinimalGolfGame.cs` (extract `TryApplyShot`, remove `HandlePointer/HandleKeyboard/TryGetPointerWorld/TryRotateLevel`), new `Assets/MinimalGolf/Scripts/VRGolfClub.cs` (+ optional `VRBallInteractor.cs`), `Assets/MinimalGolf/Scripts/MiniGolfLevel.cs` (no logic change), `Assets/MinimalGolf/Scripts/GolfBallImpact.cs` (keep).
- Steps: add tip triggers to both hand anchors, overlap check, trigger-hold pull → `shotPower/aimDirection` → `UpdateAimingLine` → release → `AddForce`, haptics, respect `playableSpeed`/`IsRevealing`/`capturing`.
- Depends on: Unit 2 (needs anchors).

**Unit 4 — Remove standalone features (net deletion)**
- Files: `Assets/MinimalGolf/Scripts/CustomGameCursor.cs` (delete or stub), `Assets/MinimalGolf/Scripts/CameraImpactShake.cs` (rewrite or delete), `Assets/MinimalGolf/Scripts/MinimalGolfGame.cs` (remove `OnGUI` scaffolding, `uiFont` if now owned by UI, `CustomGameCursor` creation), `Assets/InputSystem_Actions.inputactions` (optional prune), `MinimalGolfSceneBuilder.CreateEnvironment` (remove old camera code).
- Steps: grep for `Mouse`/`Keyboard`/`Cursor`/`OnGUI`/`orthographic` and remove; ensure no `Screen.width` usage remains.
- Depends on: Units 2–3 (otherwise no replacement).

**Unit 5 — World Space UI canvases**
- Files: new `Assets/MinimalGolf/Scripts/VRGolfUI.cs`, new prefabs `Assets/MinimalGolf/UI/VR_*.prefab`, `Assets/MinimalGolf/Materials/*` if new UI materials needed, updated `MinimalGolfSceneBuilder.cs` (spawn canvases), `Assets/MinimalGolf/Scripts/MinimalGolfGame.cs` (expose `ShotPower`/`Feedback` events).
- Steps: build 6 canvases with TMP, wire to anchor, implement power meter segments, feedback fade, course-complete button via ray interactor, keep `GetUIValues`.
- Depends on: Unit 2 (anchor) and Unit 3 (power signal).

**Unit 6 — Final integration, tuning & builder hardening**
- Files: `Assets/Settings/*` (fog, URP), `ProjectSettings/QualitySettings.asset` (vSync off for VR), builder final polish, docs.
- Steps: tune anchor distance/height/scale for Quest comfort (test with seated + standing), adjust `maximumImpulse/maximumDragDistance` if table scale changes effective putt feel, verify `LevelRevealAnimator`/`WindmillRotor` unaffected, run full play-through 1–8.

Execution order: 1 → 2 → 3 → 4 → 5 → 6. Units 4 and 5 can overlap after 3.

## Validation Plan

- **Editor health (after each unit):** `unity command editor_status` → `status: ready`; `unity command get_console_logs --severity Error --limit 100` → 0 errors; structure visual: `unity command screenshot --view game --output <workspace>/Temp/vr_<unit>.png --width 1920 --height 1080` + scene view screenshot; inspect for rig + course + UI.

- **Project Setup Tool (Unit 1 gate):** Open `Meta > Tools > Project Setup Tool`, screenshot all-green for Android & Standalone. Run `metavr docs fetch` live check that no additional manual fix is skipped. Highest-risk step — SDK import can regenerate `ProjectSettings` and break URP outlines; verify `PC_Renderer/Mobile_Renderer` still has `Minimal Golf Outlines` after apply.

- **XR tracking (Unit 2):** With Meta XR Simulator active, verify `OVRCameraRig/TrackingSpace/CenterEyeAnchor` pose drives camera, `LeftHandAnchor/RightHandAnchor` follow controllers. `metavr log` or `metavr adb logcat -s Unity` shows `OVRManager` initialized. Course anchor visible in Game view without manual reposition.

- **Swing (Unit 3):** Manual QA matrix (in headset or Link):
  - Ball at rest → controller tip enters ball trigger volume → squeeze trigger → line appears, power meter fills with pull distance → release → ball receives impulse proportional to pull, strokes increment.
  - Pull < `0.035` threshold → no shot.
  - Ball moving faster than `playableSpeed (0.32)` → trigger hold does nothing, feedback “WAIT FOR THE BALL”.
  - Overlap without trigger → no aiming; trigger without overlap → no aiming.
  - Verify strokes/par update via UI and via `GetUIValues`.

- **Standalone removal (Unit 4):** Grep pass: `grep -R "Mouse\.|Keyboard\.|OnGUI|CustomGameCursor|ISOMETRIC" Assets --include="*.cs" --include="*.asset"` returns 0 (except historical tests). Game view shows no cursor, no keyboard response.

- **World Space UI (Unit 5):** In VR, verify all 6 canvases legible at 1 m, not clipped, with correct data. Check `legend` removed (or replaced by floating hint). Verify power meter appears only while aiming.

- **Automated tests (Units 3–6):** Run existing PlayMode suite unmodified:
  ```
  Unity -runTests -testFilter MinimalGolfPlayModeTests -testResults Temp/results.xml -batchmode
  ```
  or via `Window > General > Test Runner`. Expected: T1 (hole-in-one), T2 (miss does not complete but strokes++), T3 (Garden clears or teleport fallback), T4 (weak shot blocked) all pass. `GetUIValues` mirroring asserts will catch broken UI wiring. Do not add `-k 'not ...'`.

- **Full course flow:** `game.DebugLoadLevel(0..7)` each level playable, `IsCapturing` → hole animation → auto-advance → `courseComplete` card with “Play Again” via ray press.

## Risks / Rollback

- **SDK import churn.** All-in-One can overwrite `manifest.json`, `ProjectSettings`, and add 200+ assets. Mitigation: commit before import (`git stash`/`branch`), import in a single commit, review `git status` before push; rollback by reverting that commit. Never `git clean -fdx` — `.gitignore` already excludes `Library/Temp/Logs/UserSettings`.
- **URP outline regression.** Setup Tool may touch `GraphicsSettings`/`URP Global Settings`. Verify `PC_Renderer`/`Mobile_Renderer` outlines re-created if missing (builder already handles this).
- **Scale/tuning drift.** Table scale changes effective `maximumImpulse` feel (physics is scale-invariant for forces, but visual power mapping is not). Keep tuning values stock initially; only retune in Unit 6 after 3 play-throughs, storing new defaults via builder’s `CaptureExistingComponentSettings<MinimalGolfGame>()`.
- **Duplicate AudioListeners.** OVR rig brings its own. Keep exactly one on `CenterEyeAnchor`; delete `AUDIO MANAGER` listener. Symptoms: Unity warning + silent audio.
- **XR Simulator requirement.** `AGENTS.md` requires Meta XR Simulator activated for verification — if unavailable, block on setup check before manual QA.
- **Test fragility.** Tests use teleport fallback for T3 — VR changes to ball placement must not fight `Physics.SyncTransforms` ordering. Ensure `ResetBall` and `CaptureBall` still call `SyncTransforms` as before.

## Open Questions

- **SDK exact version pin:** Which `com.meta.xr.sdk.all-in-one` version to pin for `Unity 6000.5.6f1 / URP 17.5.0`? Answer via `metavr docs fetch https://developers.meta.com/horizon/documentation/unity/unity-all-in-one-sdk` at implementation time — current assumption is latest v72.x, but confirm compatibility table before import.
- **Table scale & anchor pose:** Is 0.42 scale @ (0, -0.85, 1.30) the right comfort default for Quest 3 seated play? Needs headset tuning — expose `VRCourseAnchor` pose as serialized fields on a new `VRCoursePlacement` component for designer adjustment without code edits.
- **Controller visuals:** Use built-in `OVRControllerPrefab` hands, Meta Interaction SDK `TouchController` models, or minimal sphere tip only? Tip-only is lightest; confirm artist preference.
- **Rotation after removal:** User says “Remove any features that are specific to standalone and focus on VR.” That implies deleting `← →` course rotation. If playtesters need reorientation, should we remap to thumbstick or keep a UI “Recenter” button instead of rotation?
- **Feedback duration:** Current IMGUI feedback fades via `feedbackUntil`. Keep same timing on toast or shorten for VR?

