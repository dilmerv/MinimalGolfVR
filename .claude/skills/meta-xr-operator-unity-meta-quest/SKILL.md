---
name: hz-meta-xr-operator-unity-meta-quest
description: Quest-specific gotchas for using Meta XR Operator to test a Unity app on a Meta Quest headset — the required OpenXR controller interaction profile, Development-build requirement, the OVRRaycaster ray aim offset, head-pose limits, capture consent, the device sysprops to set, and verbose input tracing for debugging clicks.
allowed-tools:
  - Bash(adb:*)
---

# Meta XR Operator on Meta Quest (Unity)

Things to know when using Meta XR Operator to drive and validate **your own Unity app** (built with the Meta XR Core SDK) **on a Meta Quest headset**. Follow **hz-meta-xr-operator** / **hz-meta-xr-operator-unity-workflow** for general runtime interaction, and **hz-meta-xr-operator-coordinates** for coordinate math; this skill is the Quest-on-device delta.

## 1. Enable a controller interaction profile for the Android build (REQUIRED)

The most common cause of "the simulated controller does nothing." Unity's OpenXR settings frequently enable controller interaction profiles **only for Standalone (PC), not for Android**. On a Quest build with **no** controller profile, the runtime binds none, so **neither a physical nor a simulated controller's input reaches your actions**. The pose, ray, and hover still work (the Meta XR Operator layer supplies the simulated pose), which **masks** the problem — only *clicks/button presses* silently do nothing.

- **Fix:** Project Settings → XR Plug-in Management → OpenXR → **Android** tab → *Interaction Profiles* → add a Touch controller profile (e.g. **Oculus Touch Controller Profile**; the Meta XR plugin surfaces it to the runtime as `/interaction_profiles/meta/touch_controller_plus`). Enable the same profile(s) you already have under Standalone.
- **Verify at runtime:** `openxr_get_active_interaction_profile` must return a controller profile (non-`null`) once a controller — real or simulated — is active. `null` means no profile is enabled/bound and input cannot register.

## 2. Build must be a Development build

The Meta XR Operator API layer is bundled **only in Development builds**. A Release build strips it and the `openxr_*` / `unity_*` MCP tools will not be available. Enable *Development Build* before building the APK.

## 3. Aim: the OVRRaycaster ray offset

If the UI uses **OVRRaycaster / OVRInputModule**, the rendered ray emerges noticeably **below** the controller pose's forward (observed roughly **50–55°** in testing). Pointing the aim pose straight at a UI element therefore lands the reticle well below it and the click misses — even though the aim "looks" correct.

- Pitch the pose **up** from the straight-line direction to the target (≈50–55° for OVRRaycaster), and set **both** the `grip` and `aim` poses (the ray rides the device/grip pose; setting only `aim` leaves a stale grip pose).
- **Verify the reticle is on the target with `openxr_capture_composited_image` immediately before clicking.** The raycast is from the controller pose and is independent of head orientation.
- XRI / `XRUIInputModule` ray interactors may use a different offset — always confirm the reticle visually rather than assuming.

## 4. Head pose cannot be simulated on the headset

`openxr_set_head_pose` is not available on a Quest headset — move the **physical** headset to change the view. The simulated controller's raycast is world-anchored, so head movement does not change where its ray lands (you can click an off-screen element); use captures to confirm the reticle when the panel is in view.

## 5. Screen capture needs one-time consent

`openxr_capture_composited_image` uses Android MediaProjection, which requires a one-time, in-headset consent dialog. Front-load it so it doesn't interrupt an action later:
`adb shell setprop debug.meta_xr_operator.request_capture_permission 1` (then approve the dialog in-headset).

## 6. Device sysprops to set each session

`debug.` props are not persisted across reboot — re-set them per session:

```bash
adb shell setprop debug.oculus.experimentalEnabled 1          # enable the agentic path
adb shell setprop debug.meta_xr_operator.request_capture_permission 1  # front-load capture consent
adb forward tcp:8720 tcp:8720                                  # reach the MCP server
# optional, for input debugging (see section 8):
adb shell setprop debug.meta_xr_operator.verbose 1
```

If the MCP server is unreachable, re-run `adb forward tcp:8720` and make sure the app is foregrounded (session must reach `FOCUSED`).

## 7. Simulating a controller suppresses the physical controllers

While the agent drives the simulated controller (conformance automation), the runtime **suppresses physical controller tracking** for that session — relaunch the app to hand control back to physical controllers. If the headset is stationary/off-head and loses positional tracking, Horizon OS shows a **"Finding position in room"** dialog that intercepts XR input; wear the headset or give its cameras a textured view to clear it.

## 8. Debug clicks with verbose input tracing

Set `adb shell setprop debug.meta_xr_operator.verbose 1` **before launch** to raise the layer to DEBUG and trace per-action input reads (zero overhead when off). During a simulated trigger, `adb logcat -s AgenticXR` shows:

```
[DEBUG] [inputdiag] float action=0x.. cur=1 active=1   # injected value reached the app's action
```

Map an action handle to its component via the `[inputdiag] suggest action=0x.. path=...` lines. This tells you whether a failed click is **aim** (the trigger action reads `cur=1` — input is fine, re-aim) or **input routing** (`cur=0` throughout — e.g. the missing interaction profile in section 1).
