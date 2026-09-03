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
- Always activate the Meta XR Simulator BEFORE entering Play Mode — playing without the active runtime renders a garbage Game view (camera inside geometry) and yields false results. Activation is a GUI action outside automation reach, so: 1) confirm the sim app is running (`pgrep -af MetaXRSimulator`), 2) if it is not running or a previous Game capture came back corrupted, STOP and ask the user to launch it and press **Meta > Meta XR Simulator > Activate** (or the Play-button icon) — do not run Play verification until they confirm, 3) after `editor_play`, sanity-check the first Game screenshot before trusting any result.
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

## Editor stability (learned 2026-09-02)

- The Editor has hung on Play Mode teardown while connected to the external XR Simulator runtime (watchdog kill: `Mono process hang detected` in `Logs/Editor-prev.log`, no native crash report). Treat any `editor_stop` timeout or unreachable Pipeline server after Play as a suspected hang: wait, check `unity pipeline list`, and inspect `Logs/Editor-prev.log` before assuming a crash.
- Never trigger a script recompile while in Play Mode — stop Play first, then edit/recompile, then play. Recompile-during-Play preceded every hang observed.
- Save the scene with `unity command save_scene` BEFORE entering Play Mode or running tests. A watchdog kill loses unsaved in-memory scene state (test/scene reloads also discard it).
- Prefer filtered `run_tests` (`--filter`) over the full PlayMode suite; every full run costs domain reloads under a live XR session.
- Revert Editor-side collateral after Play/test cycles if it appears unprompted (`Assets/Resources/PerformanceTestRun*.json*` deletions, `Assets/Settings/*` or `ProjectSettings/*` churn, `Assets/_Recovery/`).