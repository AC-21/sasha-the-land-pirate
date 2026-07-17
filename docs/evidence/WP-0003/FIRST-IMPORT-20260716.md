# WP-0003 canonical Unity first-import evidence

Status: **candidate handoff evidence; Unity MCP first use is not yet complete**

Captured at `2026-07-16T23:30:07Z` against protected-main commit
`522c57835214ec621ba1864889d268abd378ccc3`.

## Outcome

The canonical project at
`/Users/sasha/Documents/Codex/sasha-the-land-pirate/Game` completed its first
Unity import with Editor `6000.5.4f1 (d550df8bd089)` running native ARM64.
An agent, acting under the creator's direction, drove the Unity Hub UI and
indirectly caused the Editor to launch the canonical project. The successor
manifest conservatively records both Hub and Editor as agent-invoked and
preserves the packet conflict as `DEV-WP0003-AGENT-HUB-UI-20260716`; the action
cannot satisfy the creator-opened first-use precondition. No Editor executable,
CLI, batchmode command, or Unity MCP tool was called directly. The final
compiler pass succeeded, assemblies reloaded, and no tracked `Game` file
changed during import. This is a compile observation only; it does not satisfy
the creator-opened `T-UNITY-COMPILE` acceptance oracle.

After import settled, the Unity Console was cleared and manually observed for
12 seconds. The operator observed zero logs, zero warnings, and zero errors;
the capture corroborates the final instant. It is
[first-import-console-zero-20260716.jpeg](first-import-console-zero-20260716.jpeg)
with SHA-256
`7964759028c75dfa25ae606d88ea322cfacf4bece128f0e365e83246161a6d61`
and size `62,969` bytes.
The visible `Untitled` scene was Unity's unsaved in-memory default scene; no
scene asset was created or added to the tracked project.

## Import and target evidence

- Editor log recorded `Running under Rosetta: NO`.
- Initial synchronous asset refresh completed in `61.852` seconds.
- The final Tundra compilation succeeded at Editor log line `22230`.
- The final assembly reload succeeded at Editor log line `22248`.
- Point-in-time ignored `Game/Logs/Editor.log` SHA-256 at evidence capture:
  `9005819fe04afc5afa813c0e6a69366e216a7d88f122ff110b1a0441b299f7e5`.
- Bridge receipt:
  `/Users/sasha/.unity/mcp/connections/bridge-81b1b2ca-32476.json`.
- Receipt project path:
  `/Users/sasha/Documents/Codex/sasha-the-land-pirate/Game`.
- Receipt connection: named pipe protocol `2.0`, Editor PID `32476`, socket
  `/tmp/unity-mcp-81b1b2ca-32476`.
- The host restarted after capture. That PID, socket, and receipt are historical
  evidence only and must not be reused; the next session requires a fresh
  Editor process and Bridge receipt.

Tracked baseline hashes after import:

- `Game/Packages/manifest.json`:
  `a03550abdd361adae5be637197a38b606dc5531cdcdf9a1ce779de5bc6bc77b1`
- `Game/Packages/packages-lock.json`:
  `9840ac57af6738620a6efc6c50bfbaf2f758a71e7fd8fd148fed5fae202ebf5b`
- `Game/ProjectSettings/ProjectVersion.txt`:
  `dc39b76877cad51588645e7b18e8c1c90e462ddcec948e3d9e60214dee3c09fc`
- Successor `docs/manifests/WP-0003-unity-canonical-v2.json`:
  `d7b9d48c1669ed1eb59a1cc435f22f12f1054298c2b33c3ade61517c0bd5a587`

The audit compared every canonical package-cache directory tree byte-for-byte
with the exact donor cache: all 34 of 34 matched. Those ignored cache bytes are
observational and were not retained as candidate artifacts. The expected
Assistant, MCP, AI Navigation, Input System, URP, and Test Framework assemblies
were present.

## Transient and deferred observations

Transient compiler and immutable-package messages occurred during, and were
consistent with, incremental package hydration before the final successful
compiler pass. No C# error occurred after the final success and reload. Shader
platform warnings, shader-worker recycle, and relay disconnect/reconnect
messages were non-blocking import activity. The live ignored Editor log
continued changing after the point-in-time hash, and its raw contents were not
committed because runtime logs may contain sensitive account material.

Unity Assistant account requests produced a timeout and no-entitlement `404`
responses. Editor licensing, local compilation, and the MCP Bridge remained
operational; Assistant cloud generation is therefore deferred and is not
claimed by this evidence.

Unity created one machine-local Assistant checkpoint-preference file at
`Game/ProjectSettings/Packages/com.unity.ai.assistant/Settings.json`. It
contained no secret at capture and is excluded by one exact `Game/.gitignore`
rule. Its future ignored contents are not attested. The successor canonical
manifest records that tracked hygiene change without rewriting the donor
manifest. The baseline validator validates the exact Git index closure, then
physically evaluates the `Game` tree, prunes only exact top-level Unity cache
roots, allows only that one local settings file, and rejects every other
unexpected file regardless of Git ignore state.

## Remaining first-use step

The Codex task used for this import was created outside the canonical repository
before its project-scoped Unity MCP entry was enabled, so it exposed no Unity
tools. No Unity MCP call was made. After the host restart, global Codex trust
for `/Users/sasha/Documents/Codex/sasha-the-land-pirate` remains present, and
no global Unity MCP server is configured. After this handoff and the separate
project-scoped MCP configuration change are merged, Codex must be restarted in
a task rooted exactly at the canonical repository so that server loads. A fresh
Bridge receipt, its exact target, and creator approval must then be confirmed;
the first call must be the allowed read-only `Unity_ReadConsole` action.

## Handoff validation

- `python3 docs/foundation-v0.1/tools/validate_foundation.py`: pass.
- `python3 Tools/run_wp0003_core_tests.py`: pass with 0 build warnings, 0 build
  errors, 11 of 11 tests passing twice, byte-identical repeated process output,
  byte-identical repeated build hashes, and identical DLL hashes across two
  independent checkout roots.
- A temporary `Game/Assets/ignored-validator-probe.cs` was demonstrably hidden
  by a machine-level Git exclude and still failed the physical baseline closure
  as expected; the script and temporary exclude file were then deleted.
- A forced-tracked `Game/Library/tracked-validator-probe.cs` failed the staged
  Git index closure before physical cache pruning; it was unstaged and deleted.
- A mixed-case `Game/TeMp` probe remained visible under the exact Git ignore
  semantics and failed the physical generated-path boundary; it was deleted.
- The final full validator run injected bogus `GIT_DIR`, `GIT_WORK_TREE`,
  `GIT_INDEX_FILE`, and `GIT_CONFIG_GLOBAL` values. The pinned repository and
  both reproducibility fixtures remained contained and produced the same
  deterministic results.
- A temporary symlinked artifact-root probe was rejected before any cleanup or
  write and was removed without touching its external target.
- `git diff --check`: pass.
- The first Python syntax check wrote an ignored `Tools/__pycache__` artifact,
  causing the physical Tools closure to fail as designed. The artifact was
  deleted and later checks disabled repository bytecode writes.
- Sandboxed .NET restore and the first concurrent foundation lint were
  interrupted after bounded waits because local IPC/filesystem work stalled.
  Both validators were rerun outside that sandbox and passed; no source change
  was made to conceal either interrupted attempt.
