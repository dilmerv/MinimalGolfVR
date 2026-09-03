---
name: hz-meta-xr-operator-unity-test-mechanics
description: Runs AI-driven test attempts in Unity Meta Quest and Horizon OS projects using a bounded 3-attempt retry policy with an explicit understand → set up → execute → evaluate → report flow.
allowed-tools:
  - Bash(hzdb:*)
tags:
  - agentic-xr
  - unity
  - openxr
  - mcp
  - testing
  - retry
  - vr
---

# Unity Test Mechanics

Test Unity game mechanics using Meta XR Operator and reach a clear pass/fail conclusion quickly without excessive iteration.

## Retry Policy

Do NOT spend excessive time iterating on a behavior that may be broken. Your goal is to reach a clear conclusion quickly:

- **Attempt 1**: Follow the user's instructions directly.
- **Attempt 2** (if needed): Adjust approach — different trigger method, timing, or parameters.
- **Attempt 3** (absolute maximum): Meaningfully different strategy — isolate the mechanic, try alternative verification.

**Stop early** if an attempt reveals an obvious root cause (missing component, compilation error, null reference) - 3 tries is the **maximum**, not the requirement. The user needs a fast answer, not an exhaustive search.

## Workflow

1. **Understand**: Identify the behavior to test, expected outcome, and how to trigger it. Clarify with the user if needed.
2. **Set up**: Verify preconditions (correct scene, editor state, required objects/scripts present). Capture baseline state (i.e. with a screenshot) if useful.
3. **Execute**: Enter Unity play mode (if in editor), then trigger the necessary behavior and actions to test behaviors. Observe via screenshots, console output, object state (inspect GameObjects, components, transforms), and OpenXR tools (controller poses, inputs).
4. **Evaluate**: Compare observed result to expected outcome. On pass, report with evidence. On fail, retry or conclude per the policy above.
5. **Report** (pass or fail): What you tested, what you expected, what happened, any console errors, and your assessment of why (if failing).

## Example

```
User: "Test if the player takes damage when touching the lava"

Attempt 1: Move player to lava, check health → unchanged. FAIL.
Attempt 2: Check console → "LavaDamage script missing collider". Lava has no Collider. Root cause found.

Conclusion: Lava damage broken — missing Collider (trigger) on lava GameObject, so OnTriggerEnter never fires.
```

## Additional Rules

- Follow the user's instructions as closely as possible on the first attempt.
- Use the most direct verification method available.
- Check the console for errors after each attempt.
- Never fix issues without telling the user (unless they asked you to fix it).
- Do not make project changes during testing unless necessary for test setup.
- Each retry **must** use a different approach — never repeat the same method, and never exceed 3 total attempts.
- Report what you observe (not just pass/fail), and include evidence (screenshots, logs, object state) in your report.
- Load additional Meta XR Operator skills as needed for critical context on specific mechanics and features.
