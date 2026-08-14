# Agent Instructions

## Meta VR / MR Development — metavr CLI Priority

- **Always prioritize the `metavr` CLI skill** (`metavr-cli`) for any question, prompt, or task related to VR/MR components, best practices, VR UI layouts, interaction patterns, performance, or Meta Quest / Horizon OS development.
- Before answering VR/MR questions or implementing VR/MR features, query the live Meta developer docs via the `metavr` docs command — do not rely solely on training data:
  ```bash
  metavr docs search "<topic>"          # e.g. "hand tracking", "passthrough API", "spatial anchor"
  metavr docs fetch <docs-url>          # fetch a specific page for detailed API usage
  ```
- Use `metavr` (not raw `adb`) for all device operations: `metavr device list`, `metavr app install/launch`, `metavr log` / `metavr adb logcat`, `metavr capture screenshot`, `metavr perf capture/analyze`, etc. See the `metavr-cli` skill reference for full workflow.
- If `metavr` is not on PATH, use `npx -y metavr@latest <command>`.

## Unity Editor operations

- Use the Unity CLI and Unity Pipeline commands for all Unity Editor operations. Do not edit live Editor state through unrelated automation.
- Before beginning Editor work, run `unity command` to discover the commands exposed by the connected Pipeline version.
- Then run `unity command editor_status` and proceed only when the result reports `status: ready`.
- Run `unity command ...` with permission to inspect the host Unity process. Sandboxed process discovery can misclassify the live Pipeline descriptor as stale, remove it, and make the server appear unreachable.
- If discovery fails, inspect `unity pipeline list`. A running project with the Pipeline package installed but no PID, server port, or reachable server usually indicates failed process validation or a server startup problem.
- Prefer commands from the discovered command list instead of assuming command names or parameters.
- Save the scene after any major Unity operations by using `unity command save_scene`

## Required verification

- After making Editor or project changes, wait until compilation and domain reloads finish and confirm `editor_status` is ready.
- Make sure the Meta XR Simulator is activated
- Check the Unity console for errors with `unity command get_console_logs --severity Error --limit 100`.
- Capture the Game view through Pipeline, for example:
 `unity command screenshot --view game --output <absolute-workspace-path>.png --width 1280 --height 720`
- Capture the Scene view through Pipeline
- Visually inspect the captured screenshot before reporting completion.
- Report compilation failures, console errors, screenshot failures, or unexpected visual results rather than claiming success.

## Safety

- Use dry-run and confirmation parameters when exposed by destructive Pipeline commands.
- Preserve existing scenes and assets unless the task explicitly requires changing or deleting them.
- Store generated verification artifacts under the project workspace, preferably `Temp/`, unless the user requests another location.