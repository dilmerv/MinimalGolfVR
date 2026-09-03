---
name: hz-meta-xr-operator
description: Drives Meta Quest and Horizon OS XR apps from an AI agent via the Meta XR Operator OpenXR API layer and MCP tools, including setup, head-pose control, controller input, and runtime verification.
allowed-tools:
  - Bash(hzdb:*)
tags:
  - agentic-xr
  - openxr
  - mcp
  - vr
  - ar
  - runtime
---

# Meta XR Operator Usage

Meta XR Operator is an OpenXR API Layer that gives AI agents the ability to perceive, understand, and manipulate VR/AR applications at runtime via MCP tools.

**Key constraint:** Meta XR Operator tools are ONLY available when the VR/XR application is actively running. Tools will return errors if the app is not active.

## Setup

Meta XR Operator is available as an MCP server. To setup:

- **If HZDB v1.3.0+ is installed as an MCP server but the Meta XR Operator tools (e.g., `openxr_*`) are not available**, run the **"install meta xr operator mcp proxy"** tool exposed by HZDB.
- Otherwise, prompt the user to follow steps within the Meta XR Core SDK's AI Tools window in Unity to set up Meta XR Operator.

## When to Use

Use Meta XR Operator to **interact with a running XR application at runtime**:
- **Iteration** — Launch the app, verify features, interact, then exit to make changes.
- **Debugging** — Reproduce and investigate runtime bugs by inspecting poses, scene state, and visuals.
- **Test Automation** — Programmatically verify features. See the **hz-meta-xr-operator-unity-test-mechanics** skill for more details.

## Orientation & Navigation

- **Start by orienting yourself.** Use `openxr_get_head_pose` + `openxr_capture_composited_image` to see where you are.
- **Use data before vision.** Query scene data (`get_scene_root_objects`, `get_children`, `get_world_pose`) to understand layout via coordinates, then confirm visually with screenshots.
- **Don't guess positions** — always use `get_world_pose` for both target and rig. Check positions relative to the camera rig, not world origin.
- **If you can't find something visually**, step back to get a wider field of view. If still not found after 2-3 screenshots, use data tools to get coordinates and navigate there directly.

For detailed coordinate math and controller positioning, see **hz-meta-xr-operator-coordinates**. For aiming grabbed objects (guns, tools), see **hz-meta-xr-operator-grabbed-objects**.

## Verification

- **Verify both via data AND visually.** Confirm results using coordinate/scene-data tools AND compositor screenshots.
- **Take screenshots after key interactions** to verify visual state matches expectations.
- **Check for visual artifacts** (aliasing, noise) by capturing screenshots from multiple angles.

## Input Simulation

- **Smooth movements:** Use the `duration` parameter on `openxr_set_head_pose` and `openxr_set_controller_pose` for realistic motion.
- **Controller input values:** Buttons are 0/1, Trigger/Grip are 0.0–1.0, Thumbstick is -1.0 to 1.0 on each axis.
- **Hold inputs across frames** — setting an input to 1 and immediately to 0 may be missed by `Update()`.

## Common Pitfalls

- **Tools only work when the app is running.** Connection errors mean the app may have stopped.
- **Coordinate systems matter.** OpenXR is right-handed (-Z forward). Unity is left-handed (Z forward). See the **hz-meta-xr-operator-coordinates** skill for conversion.
