---
name: hz-meta-xr-operator-interaction-grab
description: "How to grab and manipulate Meta XR Interaction SDK grabbable interactables in Unity using Meta XR Operator. Covers identifying grabbable types (close-range HandGrab/GrabInteractable, distance grab, hinged interactables like lids/doors/drawers), performing the grab via aim pose, choosing controller motions that move the target as intended, and identifying which of the three movement providers (relative-to-hand, manipulate-in-place, pull-to-hand) an interactable uses."
allowed-tools:
  - Bash(hzdb:*)
tags:
  - agentic-xr
  - openxr
  - unity
  - meta
  - interaction-sdk
  - grab
  - hand-grab
  - distance-grab
  - movement
  - interactable
---

# Meta XR Operator Interaction Grab

How to grab and manipulate Meta XR Interaction SDK grabbables — covers close-range hand/controller grabs, distance grabs, and constrained interactables (lids, doors, drawers). See **meta-xr-operator-grabbed-objects** for post-grab aiming, **meta-xr-operator-coordinates** for coordinate conversion.

## Identifying Grabbables

**Identify by components, never by GameObject name.** Use Unity MCP to enumerate components:

| Component | Meaning |
|---|---|
| `HandGrabInteractable` / `GrabInteractable` | Close-range hand or controller grab (most common). |
| `DistanceHandGrabInteractable` | Adds distance-grab capability to the same interactable. |
| `Grabbable` | Base grab target the interactable drives. |
| `IMovementProvider` impl | How the object moves while held: `MoveFromTargetProvider` → relative-to-hand, `MoveAtSourceProvider` → manipulate-in-place, `MoveTowardsTargetProvider` → pull-to-hand snap. |

The interactor lives under the camera rig — find it by `ControllerDistanceGrabInteractor` (or `DistanceHandGrabInteractor` for hand tracking), not a hard-coded path. Its selection volume is a `SelectionFrustum` cone (~10–15° half-angle), so aim only needs to be approximately on-target. If component inspection isn't available, fall back to the empirical test below — and say so.

## Grab Sequence

**Always use aim pose for both position and orientation.** Do not set grip pose — the Meta interaction system reads aim. (Setting aim also moves the derived grip pose at a ~5–8 cm offset, which is why aim-pose-only also works for close-range grabs.)

1. `set_controller_pose(hand, aim, position, orientation, base_space)` aimed at the target. For predictable downstream math, prefer **identity orientation** (`[0,0,0,1]`) from in front of the target — this leaves source_rotation ≈ identity so subsequent translations move the target 1:1.
2. `set_controller_input(hand, Grip, 1)`. Hold across frames; an immediate `0` may be missed. The grab latches on the frame grip is processed, only if a grabbable is hovered.
3. Move/rotate the **aim pose** (not grip) to manipulate.
4. `set_controller_input(hand, Grip, 0)` to release. Released objects often hover (no gravity in many sample scenes) — plan where to drop.

Note: the visible controller mesh follows grip pose, so it may look stationary or odd after aim-only updates — cosmetic, the grab system still reads aim.

### Close-range grab (HandGrab / GrabInteractable)

The most common case. Same aim-pose-at-target-with-identity-orientation pattern works. If the collider is small and the aim-to-grip offset misses, fall back to setting grip pose directly at the target.

### Distance grab (DistanceHandGrabInteractable)

Same sequence; the SelectionFrustum cone reaches the target from afar. Aim approximately at the object — exact alignment isn't required.

### Constrained interactables (lids, doors, drawers)

Grab anywhere on the movable surface (the far end from the pivot has the largest collider). Move the controller's aim pose roughly in the direction the object should travel — the constraint guides the path, so it doesn't have to be exact. Reverse direction to close. If a small move produces no change, you grabbed a non-movable part — re-grab on the movable surface.

## The Three Movement Providers

- **`MoveFromTargetProvider` (relative-to-hand)** — target tracks controller 1:1. Rotating the controller pivots the target *around the controller*, so distant targets swing through wide arcs.
- **`MoveAtSourceProvider` (manipulate-in-place)** — target stays anchored at grab-time position. Rotating the controller rotates the target *around itself*. Translating the controller still translates the target (rotated by the original grab orientation).
- **Pull-to-hand snap (`MoveTowardsTargetProvider` and similar)** — target snaps to the controller on grab, then follows it.

### Identifying the provider empirically (when components aren't readable)

Translation alone does NOT distinguish relative-to-hand from manipulate-in-place. Procedure:

1. **Grab and check pose immediately.** Position jumped to controller → **pull-to-hand**. Position unchanged but rotation matches controller's grab orientation → **manipulate-in-place**. Nothing changed → ambiguous, do step 2.
2. **Rotate controller in place** (no translation) by ~90° yaw and re-query target pose. Position barely moves → manipulate-in-place. Position swings dramatically (~controller-to-target distance × √2) → relative-to-hand.

Visual fallback for duplicate-named objects (where `unity_get_world_pose` only resolves the first): pull-to-hand makes the target jump to the controller; relative-to-hand swings it off-screen on rotate; manipulate-in-place keeps it roughly in place.

## Placing a Target at a Destination

| Provider | Strategy |
|---|---|
| Pull-to-hand | Move controller aim to destination; target follows within ~cm. |
| Relative-to-hand | `controller_target = destination − grab_offset` where `grab_offset = target_grab_pos − controller_grab_pos`. Keep orientation identity to avoid swing. |
| Manipulate-in-place | With identity-orientation grab: `controller_target = controller_grab_pos + (destination − target_grab_pos)`. Rotate aim in place to rotate without moving. |

For orientation: pull-to-hand and relative-to-hand rotate with the controller (relative-to-hand swings at distance); manipulate-in-place is safest for precise in-place rotation.

## Common Pitfalls

1. **Setting grip pose** — don't. Always `pose_type: aim`.
2. **Pressing grip without aiming first** — the SelectionFrustum must be hovering a grabbable on the grip-press frame. Set aim, take a screenshot (gives the engine a frame), then press grip.
3. **Not holding grip across frames** — `Grip=1` immediately followed by `Grip=0` is often missed.
4. **"Nothing happened, grab failed"** — relative-to-hand and close-range HandGrab produce *no visible change* until the controller moves. Don't release and retry; nudge aim by ~0.1 m and re-query target pose. If it moved with you, the grab worked.
5. **Aim line passes through another grabbable** — the closer one wins. If a previously-grabbed (floating) object is between you and the target, approach from a different angle.
6. **Manipulate-in-place rotates target on grab** — non-identity grab orientation snaps the target to that orientation. Use identity to preserve original rotation.
7. **Translation-only provider test** — ambiguous between relative-to-hand and manipulate-in-place. Always do the rotation-only test.
8. **Duplicate-named root objects** — `unity_get_world_pose("Name")` returns only the first sibling. Use screenshots or instance-ID enumeration for the others.
