---
name: hz-meta-xr-operator-interaction-poke
description: "How to poke Meta XR Interaction SDK poke interactables in Unity using Meta XR Operator — physical 3D buttons, tilted touchpad/keypad surfaces, UI buttons, and any other PokeInteractable. Covers locating the press point, computing the surface normal for any tilt, positioning the controller via aim pose along that normal, performing the press motion, and verifying via the appropriate signal (transform depression for physical buttons; UI state change for canvas buttons)."
allowed-tools:
  - Bash(hzdb:*)
tags:
  - agentic-xr
  - openxr
  - unity
  - meta
  - interaction-sdk
  - poke
  - button
  - interactable
---

# Meta XR Operator Interaction Poke

How to poke Meta XR Interaction SDK pokeables. See **meta-xr-operator-interaction-grab** for grab interactions, **meta-xr-operator-coordinates** for coordinate conversion.

## Step 1 — Identify the pokeable type

**You MUST classify the pokeable before designing the test.** Each type has a different verification signal AND a different test-design protocol. Skipping this step leads to picking the wrong signal and getting an ambiguous result.

Classify by the interactable's components, not its GameObject name:

| Type | Tell-tale | Verification signal | Test-design rules |
|---|---|---|---|
| **Physical 3D button** | `PokeInteractable` + a child `…/Visuals/ButtonVisual` whose transform moves under press | Re-query the depressing visual's world pose | Standard press, any camera angle |
| **Material-only** (glow / color / opacity) | `PokeInteractable` but the visual's transform does NOT move under contact | Screenshot comparison only | **See § Material-only test design — non-negotiable** |
| **UI Button / Toggle** | UI `Button` or `Toggle` on a `Canvas` with `PointableCanvasModule` | `unity_get_toggle_state` or side-effect query (NOT screenshot, NOT mid-push) | Always lift before checking state |

If you cannot tell physical vs material-only by inspection, do a probe press, re-query the visual transform, and decide from the delta.

## Step 2 — Pre-press checklist

Before sending the first `set_controller_pose`, confirm each line:

- [ ] Pokeable type identified (physical / material-only / UI)
- [ ] Found the **depressing visual** (typically `…/Visuals/ButtonVisual`) and its world pose
- [ ] Computed the press **normal** in OpenXR coords
- [ ] Decided which **verification signal** matches the type
- [ ] **For material-only:** camera staged per § Material-only test design, side button chosen, baseline captured

## Step 3 — Press mechanics (universal)

**Two non-negotiable rules:**

1. **Use the aim pose, not grip.** The PokeInteractor tip rides on aim; grip is offset by several cm and the press misses.
2. **Start outside the surface and push in.** The SDK detects presses from the entry transition (outside → inside). Setting the initial aim at or past the target skips that transition; touch-state pokeables won't fire at all.

Sequence:

1. **Find the press point** — `unity_get_world_pose` on the depressing visual.
2. **Compute the press normal in OpenXR** (see § Surface normal). Face-up button: `(0, 1, 0)`.
3. **Convert target to OpenXR** — negate Z; X and Y unchanged.
4. **Position aim ~10–20 cm out along +normal**, oriented so controller forward (-Z OpenXR) aligns with -normal.
5. **Push aim ~3–5 cm past the surface along -normal** with `duration_seconds: 0.5–1.0`. Instant teleport (`0`) can skip the SDK's entry frames.
6. **Lift aim back along +normal** to release. Some interactables (UI Buttons, Toggles) only fire on release.

No grip/trigger input — poke is purely positional.

## Surface normal & orientation

The press normal is the parent's local +Z (Unity convention) rotated into world space, then Z-negated for OpenXR.

- **Face-up** (parent rotation 0): normal = `(0, 1, 0)` OpenXR.
- **X-tilted by `θ`**: normal = `(0, sin θ, cos θ)` OpenXR. For 30°: `(0, 0.5, 0.866)`.
- **Empirical check**: query the visual's offset from its parent — that direction *is* the local face direction (the visual sits proud of the parent in -press direction).

For controller orientation along normal `n`: rotate so forward (-Z OpenXR) aligns with -n. For X-tilt `θ`: quaternion `[-sin(θ/2), 0, 0, cos(θ/2)]`. Re-query aim pose afterward to sanity-check.

## Verification by type

### Physical 3D buttons

**Re-query the depressing visual's world pose.** It moves along -normal by ~1–3 cm while held and springs back on release. Authoritative — works even when the button is dim, off-screen, or the camera angle hides it. On tilted surfaces the depression appears in multiple axes; check the delta direction matches -normal.

Don't rely on screenshots: many sample scenes render pokeables in a "ghost" state until directly hovered, so they look unchanged mid-press.

### Material-only test design — non-negotiable

Material-only pokeables have no transform feedback — only a material parameter changes. **Screenshots are your only signal**, and the signal is subtle. Three rules govern whether you'll see it:

1. **Pick a side button, never the middle one.** When several pokeables sit side-by-side, the middle one frequently renders with a baseline rendering offset (z-fighting, staggered z-offsets, ghost states). Testing the middle button means your "did it change?" comparison fights an existing asymmetry. A side button gives you two clean unlit neighbors as in-frame baselines.
2. **Lock head pose close.** Head along -n at ~30–60 cm so each button fills a meaningful portion of the frame. Standing-eye distance hides glow deltas in pixel noise. Offset slightly off-axis (or use a top-down angle for face-up panels) so the controller body doesn't sit between camera and target.
3. **Capture a same-pose baseline before you touch anything.** Both controllers off-camera. Then move controller into test pose and re-screenshot at the *same* head pose. Compare same-button before/after AND target vs. neighbors in the same frame.

If you find yourself hedging with phrases like "subtle but visible" — stop. Reset to a side button at close range and try again. Ambiguity is a redesign signal, not a result.

### Hover vs touch reactivity

To tell whether a pokeable reacts on proximity or only on contact, stage the controller in three phases and verify after each:

1. **Far** — both controllers off-camera. Baseline.
2. **Hover** — ~5–10 cm outside the surface along +n. Proximity-reactive pokeables fire here; touch-only ones don't.
3. **Contact** — ~5 cm past the surface along -n. Touch-only pokeables fire here.

When several adjacent pokeables of unknown type sit side by side, test one at a time — the others act as in-frame baselines.

### UI buttons on a canvas (PointableCanvasModule + UI Button/Toggle)

The transform check doesn't apply — UI elements don't physically move.

- **The click fires on release, not push.** `onClick` / `onValueChanged` fires when you lift the poke point back out. Querying state mid-push returns the pre-press value. **Always lift before checking.**
- **Verify via state.** `unity_get_toggle_state(path)` for `Toggle`; for `Button`, query the side effect (dialog opening, content change). Screenshots are a useful supplement but state is authoritative.
- **Radio-style toggle groups** (dropdown lists): selecting one flips others off. Check both your target and the previously-selected sibling to confirm a real swap.

## When verification is ambiguous

1. Re-query the aim pose — interpolation from a prior `set_controller_pose` may not have completed. Re-set with `duration_seconds: 0` and re-query.
2. Re-confirm the surface normal from the visual's offset relative to its parent.
3. Push deeper and slower.
4. **For material-only:** if you didn't follow the test-design rules above, that's the problem. Reset and follow them.

## Common Pitfalls

1. **Confusing the info panel with the interactable** — sample scenes often render a "Poke" info card next to the button. The real interactable is at the position returned by `unity_get_world_pose`, not where your eye is drawn.
2. **Targeting the wrong canvas** — multiple canvases can have similarly-named toggles (`DropDownListButton_…_Toggle (20)` vs `(0)` on a different canvas). Check parent positions. A horizontal row of toggles all sharing the same Y is a button bar (e.g. a scrubber), not the scrollable list.
3. **Assuming all pokeables physically depress** — material-only ones don't move the transform. The transform-delta check returns "nothing changed" mid-press. Switch to the screenshot comparison protocol.
4. **Testing the middle button of a row** — see § Material-only test design.
5. **Batching multiple pokeable tests in one camera frame** — efficient-feeling, but mixes states across screenshots and creates ambiguity. Reset to baseline between tests.
