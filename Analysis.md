# Hand-Feature Prompts — Session Analysis

Window: since 20:00 local (entire session of 2026-09-02 falls inside; session ran ~20:05–22:10 local).
Durations are approximate, reconstructed from session timestamps.
Model: Muse Code powered by Meta Muse Spark 1.3 · Reasoning effort: ultra.

## 1. "Add hand tracking support, use metavr CLI and the XR simulator to verify"

- Duration: ~37 min.
- What happened: implementation plan, 8-round decision grill (pinch parity, either hand, first-pinch-wins, unified cross-modality lockout, simulator-green), then implementation.
- Files modified: `Assets/MinimalGolf/Scripts/VRGolfClub.cs`, `Assets/MinimalGolf/Scenes/MinimalGolf.unity` (2 OVRHand rigs added).
- Files generated: `Assets/Tests/PlayMode/HandAimLogicTests.cs` (6 tests: aim lock + edge evaluation).
- Tools used: `workflow`, `read_file`, `search`, `bash` (`unity` pipeline, `metavr docs`, `git`, `python3`), `write_file`, `edit_file`, `write_todos`.

## 2. "Hide the proximity spheres unless controllers are active — toggle based on input"

- Duration: ~8 min.
- What happened: modality-aware sphere visibility (debounced hide while hand tracked, instant reappear for controllers) plus 2 unit tests.
- Files modified: `Assets/MinimalGolf/Scripts/VRGolfClub.cs`, `Assets/Tests/PlayMode/HandAimLogicTests.cs`.
- Files generated: none.
- Tools used: `edit_file`, `bash` (`unity` recompile).

## 3. "Keep spheres invisible; activate revealVolume on a fist at the hand's center"

- Duration: ~50 min (longest; includes 4 live simulator Play sessions, fist attempts with screenshots, transition diagnostics, and rewiring fist detection from pinch-strength to joint-curl after probe data showed strengths read zero).
- Files modified: `Assets/MinimalGolf/Scripts/VRGolfClub.cs`, `Assets/MinimalGolf/Scripts/ProximityRevealVolume.cs`, `Assets/MinimalGolf/Scenes/MinimalGolf.unity`, `Assets/Tests/PlayMode/HandAimLogicTests.cs` (fist/palm tests).
- Files generated: none (tests extended in place).
- Tools used: `edit_file`, `read_file` (screenshot inspection), `bash` (`unity` play/screenshot/console/package inspection, `metavr docs`), `bash_input` (cancelled background test run).

## 4. "RevealVolume sits under ControllerAnchor — shouldn't it be on the hands?" (two hierarchy questions)

- Duration: ~15 min, inside prompt 3's window.
- What happened: volumes migrated from under ProximitySphere to the club roots (hiding the sphere had silently disabled all reveal), then palm position-follow added in `LateUpdate` instead of runtime reparenting.
- Files modified: `Assets/MinimalGolf/Scripts/VRGolfClub.cs`, `Assets/MinimalGolf/Scripts/ProximityRevealVolume.cs`, `Assets/MinimalGolf/Scenes/MinimalGolf.unity`.
- Files generated: none.
- Tools used: `edit_file`, `bash` (`unity`).

## 5. "Toon material on hands, near-white with outlines"

- Duration: ~13 min.
- What happened: two-pass URP shader (stepped toon + inverted-hull outline) applied at runtime via the SDK material slot; verified live in the simulator.
- Files modified: `Assets/MinimalGolf/Scripts/VRGolfClub.cs`.
- Files generated: `Assets/MinimalGolf/Resources/ToonHand.shader`.
- Tools used: `write_file`, `edit_file`, `read_file` (screenshot inspection), `bash` (`unity`).

## Totals

- Combined prompt time: **~2 hours** (37 + 8 + 50 + 15 + 13 ≈ 123 min; prompts 1–5, with prompt 4's work overlapping prompt 3's window, so wall clock for features is closer to ~1h50). Adjacent session work outside these prompts (crash forensics, `AGENTS.md` edits, sim activation, restart recovery) adds roughly another ~30 min, for ~2.5 h wall clock total. All durations approximate.
- Files modified: 3 (`VRGolfClub.cs`, `ProximityRevealVolume.cs`, `MinimalGolf.unity`).
- Files generated: 2 (`ToonHand.shader`, `HandAimLogicTests.cs`).
- Verification artifacts: ~10 `Temp/` screenshots (kept out of the deliverable).
- Adjacent non-hand edits in the same window (excluded from the above): two `AGENTS.md` sections (Editor stability, simulator activation); `Playful Ball Physics.asset` tweak was not made by the agent.

## Unity crashes with the Meta XR Simulator

Prompt: "check all the unity crashes, why has it been crashing so much?" (~15 min, read-only forensics).

### Verified findings

- The Editor did not segfault — it **hung and was killed by a watchdog**. `Logs/Editor-prev.log` ends with `Mono process hang detected, sending kill signal to pid 91224`.
- No Unity crash file exists in macOS DiagnosticReports (a native crash would leave one). Unity Bug Reporter launched at 21:35:39, ~2 seconds after the kill timestamp, and the report was cancelled, so Meta received nothing.
- Timing matches an `editor_stop` call timing out after 30s: the hang happened while **exiting Play Mode with the external XR Simulator runtime connected**.
- Restart count: **4** this session (Editor PIDs observed in order: 80084 → 90069 → 90647 → 91224 → 91776). Of those, **1 is forensically confirmed abnormal** (PID 91224: hang + watchdog kill, Bug Reporter launched). The other 3 share the same signature — Pipeline server unreachable immediately after Play/recompile operations, then recovery under a new PID — but were not individually confirmed, and a user-initiated restart can't be ruled out for them.

### Likely mechanism (inference, not proven)

- Play Mode teardown (destroy XR session, unload domains, restore edit-mode scene) deadlocks or times out against the sim's external OpenXR session; the main thread blocks and the watchdog fires.
- Contributing load, all present: repeated recompile-during-Play cycles (domain reload under a live XR session), full PlayMode suite runs (scene reloads under XR), GPU pressure (172 `Destroy texture queue full` asserts in the log buffer).
- Ruled out: C# exceptions (console showed 0 errors throughout) and game frame-path code (nothing there blocks).

### Mitigations (recorded in `AGENTS.md`, Editor stability section)

- Never recompile while in Play Mode — stop, edit, compile, then play.
- `save_scene` BEFORE entering Play Mode or running tests (a kill loses unsaved in-memory state).
- Prefer filtered `run_tests --filter` over the full PlayMode suite.
- Revert Editor-side collateral after Play/test cycles (`PerformanceTestRun*.json*` deletions, `Settings`/`ProjectSettings` churn, `_Recovery/`).
- If it recurs: capture an Activity Monitor Spin Dump of the hung process before relaunching, and submit the Bug Reporter once so Meta gets a specimen.
