# Plan: VR Club Pull-Only-On-Release, Club Sphere Visual, Aiming Line Width

## Goal
Fix the VR pull mechanic so controller-near-ball drag only impulses on **release** of `PrimaryIndexTrigger`, add a tunable proximity sphere (size + opacity) to `VRGolfClub` (both `VR Club Left` / `VR Club Right`), and expose aiming-line width on `MinimalGolfGame` under `Aiming Line` header.

## Success Criteria
- With either controller tip inside the ball proximity volume, holding `PrimaryIndexTrigger` starts aiming (drag direction/power derived from pull vector) but applies **zero** force until the matching `GetUp` event; releasing below the minimum power/direction thresholds cancels without impulse.
- `VR Club Left` and `VR Club Right` each render a semi-transparent sphere at the tip whose radius and opacity are Inspector-editable on `VRGolfClub` and persist through scene rebuilds; gizmo fallback still works in `OnDrawGizmosSelected`.
- `MinimalGolfGame` exposes `aimingLineWidth` under an `Aiming Line` inspector section; it drives `LineRenderer.startWidth`/`endWidth` at `Awake`, `BeginVRAim`/`UpdateAimingLine`/`CancelAim`, and in `MinimalGolfSceneBuilder.Build()` / `CreateVRClubs`.
- Existing PlayMode tests (`Assets/Tests/PlayMode/MinimalGolfPlayModeTests.cs`) still pass; no new hard dependency on headset hardware for Editor smoke tests (mouse fallback preserved).

## Context And Current Facts
- **Current VR interaction** — `Assets/MinimalGolf/Scripts/VRGolfClub.cs:11-115`:
  - `overlapRadius` (default `0.12f`, scene/builder override `0.11f`) + distance check `dist < overlapRadius + 0.08f` sets `overlappingBall` (`:42-43`). `triggerThreshold:17` is declared but never read.
  - State machine: `!aiming && overlappingBall && triggerDown -> BeginVRAim` (`:52-78`), `aiming && triggerHeld -> UpdateVRAim` (`:80-88`), `aiming && triggerUp -> TryEndVRAimAndShoot` (`:90-98`). Shoot path in `MinimalGolfGame` is `TryEndVRAimAndShoot` (`Assets/MinimalGolf/Scripts/MinimalGolfGame.cs:372-387`) -> `TryApplyShot`; `BeginVRAim` only sets `dragging=true` and shows the line.
  - Reported bug is that force appears to apply "as soon as I press" — current code already intends release-only, so defect is likely one of: (a) stale flat-screen drag path re-added, (b) `OVRInput` stub `Get`/`GetDown`/`GetUp` timing or missing `UpdatePrev` causing `triggerHeld` true on same frame as `triggerUp` miss, (c) duplicate `OnTrigger*` toggling `overlappingBall` concurrently with distance check, or (d) the Editor mouse fallback firing for both controllers. No flat-screen mouse drag handling exists in `MinimalGolfGame.Update` (`:229-258`), so VR path is the only shooter.
  - `aimStartWorld` stored at `:65,74` but unused after `BeginVRAim`; pull vector is recomputed from `dragStartWorld - currentWorld` in `MinimalGolfGame.UpdateVRAim` (`:360-369`).
- **Sphere visual** — `MinimalGolfSceneBuilder.cs:484-524` comment `No visible TipVisual - controller model is the visual, club is invisible collider`. At runtime `MinimalGolfGame.CreateClub` (`:200-213`) and in Editor `CreateVRClubs` only add a tiny `SphereCollider(radius 0.035)` + `VRGolfClub`. Visual feedback today is only `OnDrawGizmosSelected` yellow/green wire sphere at `overlapRadius` (`VRGolfClub.cs:172-176`), so not visible in Game view or on device and not tunable beyond the collider radius.
- **Aiming line** — `MinimalGolfGame.cs:11,91-98,397-414` hard-codes `startWidth=endWidth=0.045f` at `Awake`; `UpdateAimingLine` only sets positions/colors, not width. `MinimalGolfSceneBuilder.cs:119-124` hard-codes the same. Inspector has no width field; request is to place it under `Aiming Line` options on `MinimalGolfGame`.
- **Scene instantiation** — Clubs parented to `leftControllerAnchor/rightControllerAnchor` (fallback to `leftHandAnchor/rightHandAnchor`) in both `MinimalGolfGame.EnsureClubsNextFrame` and `SceneBuilder.CreateVRClubs`. Any new sphere child must be under the club GO so it follows the anchor without extra tracking.

## Constraints And Non-goals
- **Constraints:** Keep `OVRInput` controller-specific routing (`LTouch` vs `RTouch`); preserve mouse fallback in `UNITY_EDITOR` for tests/editor without headset. Preserve `CaptureExistingComponentSettings<MinimalGolfGame>` JSON round-trip in `SceneBuilder.Build` so existing inspector values survive rebuilds. Use `unity command` / `metavr` flows for verification (see `AGENTS.md`).
- **Non-goals:** Re-introducing flat-screen mouse drag shooting, changing shot tuning (`maximumImpulse`, `maximumDragDistance`, `playableSpeed`), changing cup assist/capture, re-parenting the course or rig, or adding full club mesh art.

## Key Decisions
1. **State-machine fix is a guard, not a rewrite** — Recommended: keep `BeginVRAim`/`UpdateVRAim`/`TryEndVRAimAndShoot` API. Harden `VRGolfClub.Update` so `triggerDown` never calls `TryApplyShot`/`AddForce`, and `triggerUp` is the sole call-site for `TryEndVRAimAndShoot`. Remove dead `triggerThreshold` or wire it to `OVRInput.Get(Axis1D.PrimaryIndexTrigger) > threshold` replacing the unused bool (decision: deprecate field with `[FormerlySerializedAs]` if we replace it, otherwise document as legacy).
   - Alternative rejected: moving pull logic into `MinimalGolfGame.Update` with raycasts — would duplicate controller routing and break the per-club tip model already in place.
2. **Sphere rendering: child mesh + transparent material vs gizmo-only** — Recommended: add a child `Sphere` primitive (or instanced quad) with a dedicated `Material` (URP unlit/transparent, instanced per club) whose scale = `sphereRadius * 2` and alpha = `sphereOpacity`. Keep `OnDrawGizmosSelected` wire overlay for Scene view. Also retain the trigger `SphereCollider` for physics overlap; decouple visual radius from collider/physics `overlapRadius` to avoid changing shot feel.
   - Alternative rejected: using only `Gizmos`/ `OnDrawGizmos` — invisible in Game/MR view on-device, fails the request to "draw a sphere" the player sees.
   - Alternative rejected: shader-graph-only outline on controller model — more art coupling, not Inspector-tunable per club.
3. **Width exposure location** — Recommended: new `[Header("Aiming Line")] [SerializeField] float aimingLineWidth = 0.045f` (plus `OnValidate` / `Awake` apply to `aimingLine.startWidth/endWidth`). Use same field in `SceneBuilder` when constructing the `LineRenderer`. Clamp to `(0.005f, 0.2f)` to avoid invisible or course-obscuring lines at `vrCourseLevels` scale `0.042`. Header name matches user request "Aiming Line options".
   - Alternative rejected: per-club width — aiming line is owned by `MinimalGolfGame`, not per hand.

## Recommended Approach
1. **Harden pull-on-release (`VRGolfClub.cs`)**
   - Gate `triggerDown` with `overlappingBall && CanTakeAction()` and ensure it only enters `aiming` via `BeginVRAim`; never call `TryEndVRAimAndShoot` there.
   - Hold `aiming` across frames independent of `overlappingBall` (hand can pull back outside the ball); only `triggerHeld` streams `UpdateVRAim`. Add explicit `if (!aiming) return` guard before any shot path.
   - On `triggerUp`, call `TryEndVRAimAndShoot` exactly once, clear `aiming`, run haptics. Add fallback: if `OVRInput.GetUp` missed in stub (hold released without `GetUp`), detect `aiming && !triggerHeld && Time` since last `triggerHeld` to still fire `TryEndVRAimAndShoot` — or log and rely on mouse `wasReleasedThisFrame`. Ensure `aiming && CurrentLevel.IsRevealing -> CancelAim` resets locally.
   - Decide on `triggerThreshold`: either delete and mark deprecated, or implement `float triggerValue = OVRInput.Get(Axis1D.PrimaryIndexTrigger, controller); bool held = triggerValue > triggerThreshold` (threshold field already serialized). Keep editor mouse path controller-gated to `RTouch` as today (`:128-152`) to avoid double-fire.
2. **Club proximity sphere (`VRGolfClub.cs` + builder/runtime)**
   - Add fields to `VRGolfClub`:
     ```csharp
     [Header("Proximity Sphere Visual")]
     public float sphereRadius = 0.08f; // Inspector-tunable, maps to visual sphere scale
     [Range(0f,1f)] public float sphereOpacity = 0.35f;
     public Color sphereColor = new Color(0.3f, 0.9f, 0.6f, 1f);
     public bool showSphere = true;
     ```
     Keep `overlapRadius` for gameplay hit-test separate; optionally add `[FormerlySerializedAs]` migration if renaming.
   - In `Awake`/`OnValidate`/`OnEnable`, ensure child `ProximitySphere` exists: `GameObject.CreatePrimitive(PrimitiveType.Sphere)` or `MeshFilter` + `SphereMesh`, strip its `Collider`, assign a transparent material instance (`Shader.Find("Universal Render Pipeline/Unlit")` or `Standard` with `Transparent` queue). Drive `localScale = Vector3.one * sphereRadius * 2f`, `material.color = sphereColor with alpha sphereOpacity`, enabled = `showSphere`. Call each frame only when inspector values change or in `OnValidate` + after aim state if pulsing by power is desired (optional: lerp alpha with `game.ShotPower`).
   - Reduce scene gizmo confusion: keep `OnDrawGizmosSelected` wire at `sphereRadius` (or both radii) and add `OnDrawGizmos` (non-selected) thin wire if `showSphere` false so designers can still locate the tip.
   - Update `MinimalGolfGame.CreateClub` and `MinimalGolfSceneBuilder.CreateVRClubs` to set defaults (`sphereRadius = 0.08f`, `sphereOpacity = 0.3f`) and to ensure the primitive/material is built there too for baked scenes. Existing scenes with `overlapRadius 0.11` must not be clobbered — new fields get independent defaults.
3. **Aiming line width (`MinimalGolfGame.cs` + builder)**
   - Add under new header:
     ```csharp
     [Header("Aiming Line")]
     public LineRenderer aimingLine;
     [SerializeField, Range(0.005f, 0.2f)] private float aimingLineWidth = 0.045f;
     ```
     Relocate `aimingLine` from `Authored Scene References` to this header (keep `[FormerlySerializedAs]` if needed to preserve serialization).
   - Apply width in `Awake` (`aimingLine.startWidth = aimingLine.endWidth = aimingLineWidth`), in `UpdateAimingLine` (so width reacts if designer tweaks at runtime), and in `OnValidate` editor-only sync.
   - In `MinimalGolfSceneBuilder.CreateMaterials`/`Build`, set `line.startWidth = line.endWidth = game.aimingLineWidth` instead of literal `0.045f`. Capture/restore path already handles the new field via `EditorJsonUtility`.

## Work Plan
1. **Discovery — confirm shooting-on-press reproducer** *(read-only)*
   - Search call sites of `TryApplyShot`/`TryEndVRAimAndShoot`/`AddForce` to prove no hidden shooter; inspect `OVRInput` stub (`Assets/Plugins/.../OVRInput.cs` or similar) for `Get/GetDown/GetUp` semantics and `Axis1D` support. Capture `unity command editor_status` ready.
   - Owner: `MinimalGolf/Scripts` | Depends: none

2. **Slice A — Fix VR pull to shoot only on release** *(code: `VRGolfClub.cs`, optional `MinimalGolfGame.cs` guard)*
   - Edit `VRGolfClub.Update` per §Recommended Approach(1); remove/implement `triggerThreshold`; keep `aimStartWorld` or drop if unused. Ensure `aiming` cleared on `IsRevealing`/`CancelAim`.
   - Depends: 1

3. **Slice B — Club sphere visual with radius + opacity** *(code: `VRGolfClub.cs`; integration: `MinimalGolfGame.cs:CreateClub`, `MinimalGolfSceneBuilder.cs:CreateVRClubs`)*
   - Implement child sphere creation, material instancing, `sphereRadius`/`sphereOpacity`/`sphereColor`/`showSphere`, `OnValidate` sync, preserved serialization.
   - Depends: 1

4. **Slice C — Aiming line width exposure** *(code: `MinimalGolfGame.cs`, `MinimalGolfSceneBuilder.cs:117-130`)*
   - Add `aimingLineWidth` header/field, wire to `LineRenderer` at all lifecycle points, update builder to use field not literal.
   - Depends: 1

5. **Scene & prefab migration**
   - Re-run `unity command` build (or `Minimal Golf/Build Authored Course`) and verify existing `MinimalGolf.unity` `VR Club Left/Right` retain `overlapRadius 0.11` while new `sphereRadius`/`sphereOpacity`/`aimingLineWidth` appear. No destructive scene deletes.
   - Depends: 2,3,4

6. **Docs**
   - Note new Inspector fields in any `MinimalGolf` readme / tooltips; record `triggerThreshold` deprecation if removed.
   - Depends: 5

## Validation Plan
- **Static / compilation** — `unity command editor_status` → `ready`; `unity command get_console_logs --severity Error --limit 100` → no errors after slices A-C. *Expected:* zero compilation errors, warnings only for deprecated `triggerThreshold` if kept.
- **PlayMode suite** — Run `Assets/Tests/PlayMode/MinimalGolfPlayModeTests.cs` (T1-T3 etc.) via Unity Test Runner. *Expected:* same pass as baseline; validates that width/sphere changes don't affect ball physics.
- **VR state-machine unit** — Editor smoke with mouse fallback (`VRGolfClub` on `RTouch`):
  - Place controller tip at `ball.position`, press LMB (`triggerDown`) → `game.IsAiming == true`, `ShotPower` grows while dragging, `ball.linearVelocity` unchanged.
  - Drag 1.5m on ball plane, still holding → `ShotPower ~0.48`, no impulse.
  - Release (`triggerUp`) → impulse applied, `LevelStrokes` increments, `aimingLine.enabled == false`.
  - Press where `dist > overlapRadius` → no `BeginVRAim`.
  - Release with `shotPower < 0.035` or zero `aimDirection` → `TryEndVRAimAndShoot` returns false, no stroke.
- **Sphere visual** — Scene view + Game view screenshots:
  - `unity command screenshot --view game --output .tmp/sphere_game.png` and `--view scene` while selecting `VR Club Right`. *Expected:* Game view shows translucent sphere at controller tip, radius tracks `sphereRadius`; opacity slider moves alpha 0→1; both clubs honor independent values.
  - Inspector: tweaking `sphereRadius` updates both collider gizmo and mesh scale immediately via `OnValidate` (no play needed).
- **Width visual** — Inspector on `GAME SYSTEMS → MinimalGolfGame → Aiming Line → Aiming Line Width` slider; during aim, `unity command screenshot --view game` at narrow (`0.01`) vs wide (`0.12`) shows visibly thinner/thicker line. *Expected:* values survive `Build` round-trip (check `aimingLine.startWidth` in built scene matches `aimingLineWidth`).
- **MR/on-device (if `metavr` available)** — `metavr device list`, `metavr app install/launch`, `metavr capture screenshot` with HMD, validate haptics on `BeginVRAim`/`TryEndVRAimAndShoot`. Long-term: `metavr perf capture`.

## Risks / Rollback
- **Risk: visual sphere interferes with physics** — child sphere accidentally leaves its `SphereCollider` enabled, blocking ball. Mitigation: `DestroyImmediate(child.GetComponent<Collider>())` and keep only the trigger `SphereCollider` on the club root. Rollback: disable `showSphere` or delete child GO.
- **Risk: `aimingLineWidth` mis-scaled in VR** — course is at `0.042` scale under `VRCourseLevels`; a `0.045` world-space width may look hairline or blocky. Mitigation: test at 0.02–0.08 range and clamp; allow designer iteration. Rollback: reset to `0.045f` default.
- **Risk: `OVRInput` stub divergence** — Editor smoke passes but device `GetUp` timing differs. Mitigation: keep both digital `Button` and analog `Axis1D` paths, and add frame-delayed fallback for missed `GetUp`. Rollback: revert `VRGolfClub.Update` to previous version (single file).
- **Rollback overall:** slices are additive and per-file; revert commits for `VRGolfClub.cs` / `MinimalGolfGame.cs` / `MinimalGolfSceneBuilder.cs` independently; rebuilt scene can be restored from `Assets/Scenes/MinimalGolf.unity` git checkout.

## Open Questions
- Keep or remove `triggerThreshold`? Field exists but unused. Proposal: implement analog threshold (recommended) or mark `[Obsolete]` and keep serialized value for backwards compat — needs your call. (Default `0.45f` in scene matches typical Oculus trigger half-press.)
- Should proximity sphere radius default-track `overlapRadius` (e.g., `sphereRadius = overlapRadius`) or be fully independent? Proposal: independent with initial `sphereRadius = 0.08f` (slightly smaller than `overlapRadius 0.11`) so visual doesn't overstate hit volume. Confirm.
- Should sphere pulse or change color with `ShotPower` (green→amber→red like aiming line) or stay constant? Proposal: constant for now, with optional power-modulated alpha as follow-up to avoid visual noise.
- `sphereOpacity` range — `0..1` or narrower `0.05..0.6` to prevent fully opaque ball-obscuring sphere? Proposal: `0..1` with default `0.35`.
