---
name: hz-meta-xr-operator-unity-workflow
description: Iterates on Unity projects targeting Meta Quest and Horizon OS via the Meta XR Operator MCP server, covering Play Mode gating, scene editing safety, scene hierarchy navigation, UI interaction, and HZDB documentation lookup.
allowed-tools:
  - Bash(hzdb:*)
tags:
  - agentic-xr
  - unity
  - openxr
  - mcp
  - vr
  - development
  - iteration
---

# Meta XR Operator + Unity Development

Using Meta XR Operator within a **Unity + XR Simulator** development workflow. Follow the **hz-meta-xr-operator** skill for general runtime interaction guidance.

## The Iteration Loop

1. Plan feature/fix (use HZDB for Oculus VR docs if available)
2. Modify Unity project (use Unity MCP to edit scripts, scenes, etc.)
3. Validate scene in Editor (take editor screenshot for layout understanding)
4. Enter Play Mode (app starts, Meta XR Operator becomes available)
5. Interact & verify at runtime (use Meta XR Operator MCP tools)
6. Exit Play Mode (Meta XR Operator disconnects)
7. Repeat from step 2

## Critical Rules

- Unity MUST be in **Play Mode** for Meta XR Operator tools to work. They return errors otherwise.
- **Exit Play Mode** before making persistent changes to scripts, scenes, or project settings via Unity MCP.
- If Unity MCP becomes unresponsive, Unity is likely **compiling code** — wait and retry.

## Before Entering Play Mode

- **Take an editor screenshot** to understand the initial layout.
- **Query the scene hierarchy** via Unity MCP to understand what GameObjects and components exist.

## Navigating the Scene Hierarchy

1. `get_scene_root_objects` — see top-level objects
2. `get_children("path")` — drill down (e.g. `"Canvas/Panel/StartButton"`)
3. `get_world_pose("path")` — get coordinates, then convert to OpenXR (see coordinates skill)

## Interacting with UI

1. `find_canvases` — locate UI canvases
2. `find_interactables("CanvasPath")` — discover buttons, sliders, etc.
3. `get_world_pose` on the interactable → convert to OpenXR coordinates
4. `openxr_set_controller_pose` — move controller to that position
5. `openxr_set_controller_input(Trigger, 1)` then `(Trigger, 0)` — simulate click
6. `openxr_capture_composited_image` — verify

## General Unity MCP Tips

- **Script types aren't available immediately** — after creating C# scripts, Unity needs to compile. `AssetDatabase.Refresh()` triggers compilation but disconnects MCP. Wait, then use `Type.GetType("ClassName, Assembly-CSharp")` to add components.
- **RunCommand can't reference Assembly-CSharp types directly** — use `Type.GetType()` + `AddComponent(type)` and `SerializedObject` for wiring references.
- **Update serialized values in the editor, not just code defaults** — public fields are serialized in the scene. Use `Unity_RunCommand` with `EditorUtility.SetDirty()` to update live values.
- **Reimport shaders/scripts after editing** — `AssetDatabase.ImportAsset(..., ImportAssetOptions.ForceUpdate)` and `SceneView.RepaintAll()`. `Shader.Find()` returns null for unimported shaders.
- **Reassign shader to material** after adding/removing properties to avoid stale bindings.

### Colliders & Physics

- **Check `isTrigger`** — `OnCollisionEnter` only fires on non-trigger colliders; triggers require `OnTriggerEnter`. When in doubt, implement both.
- **Use `ContinuousDynamic` for fast projectiles** — default `Discrete` mode lets fast objects tunnel through colliders.
- **Never spawn GameObjects from `OnDestroy`** — Unity calls it during scene teardown. Move spawn logic to the caller.

## Using HZDB (if available)

Query HZDB for Oculus VR documentation on VR-specific features (hand tracking, passthrough, spatial anchors, etc.). If unavailable, proceed with general VR knowledge.
