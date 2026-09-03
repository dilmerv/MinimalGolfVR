---
name: hz-meta-xr-operator-coordinates
description: Converts between Unity, OpenXR, and tracking-origin coordinates so AI agents can position the head and controllers correctly in Meta Quest and Horizon OS apps, covering tracking origin matching, Unity-to-OpenXR conversion, controller positioning, and UI interaction via aim pose.
allowed-tools:
  - Bash(hzdb:*)
tags:
  - agentic-xr
  - openxr
  - unity
  - camera
  - controller
  - movement
  - coordinates
  - aiming
  - aim-pose
  - ui
---

# Meta XR Operator Coordinates & Movement

Coordinate math for moving the VR head/camera, positioning controllers, and aiming at objects at runtime. The Unity app must have an OVRCameraRig in the scene.

For aiming **grabbed objects** (guns, tools, etc.), see the **hz-meta-xr-operator-grabbed-objects** skill, which covers offset calibration and precise aiming.

## Key Concept: Tracking Origin

`unity_get_world_pose` returns a `tracking_origin` field that tells you which OpenXR reference space matches Unity's coordinate system. **Always use `tracking_origin.OpenXR` as the `base_space` parameter** when setting poses — this eliminates manual Y-offset hacks.

| OVR Setting | OpenXR base_space | Description |
|---|---|---|
| EyeLevel | `local` | Origin at headset eye level |
| FloorLevel | `local_floor` | Origin at floor level |
| Stage | `stage` | Origin at stage/room center |

Re-query `unity_get_world_pose` if the scene or project changes — the tracking origin may differ.

## World-to-OpenXR Position Formula

```
eye = unity_get_world_pose("OVRCameraRig/TrackingSpace/CenterEyeAnchor")

openxr_x =   (target_world_x - eye_x)
openxr_y =   (target_world_y - eye_y)
openxr_z = -( target_world_z - eye_z)
```

Use `tracking_origin.OpenXR` as `base_space`. No hardcoded offset needed — the correct reference space handles alignment.

## Coordinate Conversion (Unity ↔ OpenXR)

| Component | Conversion |
|-----------|-----------|
| Position X | Same |
| Position Y | Same |
| Position Z | **Negate** |
| Quaternion X | **Negate** |
| Quaternion Y | **Negate** |
| Quaternion Z | Same |
| Quaternion W | Same |

## Steps: Face an Object (Head)

1. **Get positions** (parallel): `unity_get_world_pose("TargetObject")` + `unity_get_world_pose("CenterEyeAnchor")`
2. **Direction in Unity space**: `math_sub(target_pos, eye_pos)`
3. **Convert to OpenXR**: negate Z component
4. **Build quaternion**: `math_build_quat(front_direction: [dir.x, dir.y, dir.z])` — include Y to look up/down, or zero Y for horizontal view
5. **Set head pose**: `openxr_set_head_pose(orientation, base_space: tracking_origin.OpenXR)`
6. **Verify**: `openxr_capture_composited_image()`

## Steps: Position a Controller at a Target

**IMPORTANT: Head movement shifts controller world positions.** Always: face the target **first**, then requery all positions before computing the controller pose. Using stale pre-head-movement positions will place the controller in the wrong location.

1. **Face the target** using the head steps above — the target must be visible to the camera, just as a real user would look at what they're interacting with
2. **Requery positions** (parallel): `unity_get_world_pose("TargetObject")` + `unity_get_world_pose("CenterEyeAnchor")`
3. **Apply the formula** above to compute the controller position in OpenXR
4. **Set controller pose**: `openxr_set_controller_pose(hand, position, base_space: tracking_origin.OpenXR)`
5. **Verify**: `unity_get_world_pose("OVRCameraRig/TrackingSpace/RightHandAnchor")` to confirm placement

## Grip vs Aim Pose

- **Grip pose** (`pose_type: grip`, default) — where the hand holds the controller. Use for positioning and grabbing.
- **Aim pose** (`pose_type: aim`) — pointing direction. Use for shooting, pointing, and UI interaction.
- **Typical workflow**: position with grip → grab → switch to aim pose for orientation control.
- **Grip-to-aim pitch offset is ~60°** on Quest controllers. To aim horizontally with grip pose, tilt grip up ~60°. Prefer using aim pose directly to avoid this complexity.

## Interacting with UI Elements

Use the aim pose to point at and click UI elements. Position the controller **within 1 unit of the UI target** (if possible) for realistic interaction.

1. **Discover UI**: `unity_find_canvases()` → `unity_find_interactables("CanvasPath")`
2. **Get target pose**: `unity_get_world_pose("Canvas/Panel/Button")` → note `tracking_origin.OpenXR`
3. **Face the target** — turn head toward the UI element first
4. **Requery positions** after head movement
5. **Position controller** ~0.5–0.7m from the element along the line from the eye:
   ```
   target_openxr = convert(target_unity - eye_unity)
   controller_pos = target_openxr - 0.7 * normalize(target_openxr)
   direction = target_openxr - controller_pos
   orientation = math_build_quat(direction)
   ```
6. **Set aim pose**: `openxr_set_controller_pose(hand: right, pose_type: aim, position, orientation, base_space: tracking_origin.OpenXR)`
7. **Click**: `openxr_set_controller_input(Trigger, 1)` then `(Trigger, 0)`
8. **Verify**: `openxr_capture_composited_image()`

## Tips

- **Smooth movement**: `duration_seconds: 1` for rotations, `1.5-2` for position+rotation, `0` for instant.
- **Never guess positions**: always use `unity_get_world_pose` for both target and eye.
- **Hold inputs across frames** — setting an input to 1 and immediately to 0 may be missed by `Update()`.

## Common Pitfalls

1. **Aiming at targets not in view** — always turn the head to face the target first. Aiming at something behind the camera produces unrealistic and unreliable results.
2. **Using stale positions after head movement** — see the controller positioning section above.
3. **Using wrong base_space** — must match `tracking_origin.OpenXR` from `unity_get_world_pose`. Using a mismatched space produces incorrect Y offsets.
4. **Forgetting to negate Z** — Unity Z forward vs OpenXR -Z forward.
5. **Using world position directly as OpenXR position** — must subtract eye/rig position first.
6. **Not verifying with unity_get_world_pose** — always read back the anchor's world position to confirm.
