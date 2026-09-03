---
name: hz-meta-xr-operator-grabbed-objects
description: Aims and positions objects currently held by simulated controllers in Meta Quest and Horizon OS XR apps, including pose-offset calibration and quaternion math for grip and aim poses.
allowed-tools:
  - Bash(hzdb:*)
tags:
  - agentic-xr
  - openxr
  - unity
  - aiming
  - grip
  - aim
  - grabbed
  - orientation
  - position
  - offset
---

# Meta XR Operator Grabbed Object Control

How to precisely aim grabbed objects (guns, tools, pointers) so a reference point (muzzle, tip) points at a target.

## Core Concept: Pose Offset

When grabbed, the object's reference point doesn't align with the controller pose. There are two offsets — **rotation** and **position** — that are constant after a grab. Measure them once, then apply their inverses to aim or position the object.

Offsets depend on which **pose type** (aim vs grip) you use. **Always calibrate — never skip based on assumptions about the grab code.** The OpenXR-to-object mapping always has a residual offset.

## Step 1: Grab the Object

Position the controller at the object and press grip (see coordinates skill for world-to-OpenXR conversion).

## Step 2: Calibrate Offsets (once per grab)

Set the aim pose to identity, then read the reference point's pose:

```
set_controller_pose(aim, position: [0,0,0], orientation: [0,0,0,1])
get_world_pose("Controller/Anchor")    → anchor_pos
get_world_pose("Object/RefPoint")      → ref_pos, rotation (rx, ry, rz)
```

**Rotation offset** — the reference point's yaw at identity reveals the offset:
```
forward_openxr = (sin(ry°), 0, -cos(ry°))     // for yaw-only offset
Q_offset = math_build_quat(forward_openxr)
Q_offset_inv = [-Q.x, -Q.y, -Q.z, Q.w]        // quaternion conjugate
```

**Position offset** — vector from anchor to reference point in Unity world space:
```
P_offset = ref_pos - anchor_pos
```

**Note:** Recalibrate the position offset if the object's scale or model changes. The rotation offset is stable across those changes.

After computing the offsets, the controller and grabbed object should be **moved to a default position in view** (unless specified otherwise by the user or game).
Keep held objects far enough from the camera so they don't fill the screen. In `local` (EyeLevel) space, recommended ranges from the camera are:
  - **Z** (forward): minimum -0.4, closer makes objects too large
  - **Y** (below eyes): -0.2 to -0.4
  - **X** (lateral): ±0.1 to ±0.2 (positive = right hand, negative = left hand)

## Step 3: Aim at a Target

1. **Turn head** to face the target (see coordinates skill).
2. **Requery positions** — head movement **shifts** the controller in world space, so it is **critical** to requery positions of objects after head movement:
   ```
   get_world_pose("Object/RefPoint")  → ref_pos
   get_world_pose("TargetObject")     → target_pos
   ```
3. **Compute aim orientation** and apply the inverse offset:
   ```
   dir = target_pos - ref_pos
   Q_desired = math_build_quat([dir.x, dir.y, -dir.z])
   Q_aim = math_multiply_quat(Q_desired, Q_offset_inv)
   set_controller_pose(aim, orientation: Q_aim)
   ```
4. **Refine** — the rotation shifts the reference point slightly. Requery ref_pos, recompute, and re-set once for better accuracy.
5. **Act and verify**:
   ```
   set_controller_input(Trigger, 1)
   openxr_capture_composited_image()           // visual check
   get_world_pose("TargetObject")              // "not found" = hit
   ```

## Positioning the Reference Point

To place the reference point at an exact world position, subtract P_offset before converting to OpenXR:

```
anchor_pos = desired_pos - P_offset
openxr_pos = world_to_openxr(anchor_pos)       // see coordinates skill
set_controller_pose(aim, position: openxr_pos)
```

To position AND orient in one call, combine with the aim quaternion from Step 3.

## Common Pitfalls

1. **Not calibrating after grab** — measure offsets after each grab.
2. **Using stale positions** — always requery after any head or controller movement.
3. **Not turning head first** — head rotation moves the controller. Turn head → requery → compute.
4. **Mixing pose types** — calibrate and aim with the same pose type (both aim or both grip).
5. **Forgetting the inverse** — `Q_aim = Q_desired * Q_offset_inv`, not `Q_desired * Q_offset`.
6. **Aiming from object origin instead of fire point** — use the actual muzzle/fire point child, not the object's transform origin.
