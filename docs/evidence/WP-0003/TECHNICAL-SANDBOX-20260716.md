# WP-0003 technical sandbox candidate

Status: **implementation candidate; Unity validation not yet claimed**

Creator direction in the current authenticated task:

> You can start building the game, there has been a lot of setup. Can we get
> this moving now?

This candidate begins the bounded WP-0003 deliverable without expanding into
gameplay decisions. It adds one inspectable, primitive-based technical scene,
strategy-camera controls, a bounded selectable probe grid, a debug HUD, and
EditMode/PlayMode test source. The visual blockout lightly exercises the
ratified tungsten-over-neon hierarchy but is not production art.

## Boundary

- The probe state is presentation-only and explicitly non-authoritative.
- No city, colony, resident, robot, vehicle, faction, manufacturing, market,
  economy, narrative, or balance rule is encoded.
- Persistence fails closed with WP0003_PERSISTENCE_DISABLED and reports zero
  bytes written. No file or save API is called.
- No package, SDK, service, network dependency, or production asset is added.
- Dynamic development materials are Editor-sandbox only; no player-build or
  production-shader retention claim is made by this packet.
- The repository-local SimulationCore and SaveContracts packages remain
  unlinked pending an explicit dependency-link decision.
- No Unity executable, CLI, batchmode command, or Unity MCP action was invoked
  to create this source candidate.

## Completed non-Unity verification

- The pinned WP-0003 validator passed all 11 deterministic-core checks in
  three executions with zero build warnings and zero build errors.
- Repeated process output and clean-build DLL hashes were byte-identical.
  Independent checkout roots also produced identical DLL hashes.
- The validator reconstructed the immutable historical Unity baseline, then
  composed and verified the exact staged technical-sandbox overlay.
- The immutable canonical manifest remains SHA-256
  `d7b9d48c1669ed1eb59a1cc435f22f12f1054298c2b33c3ade61517c0bd5a587`.
  The technical-sandbox overlay is pinned at SHA-256
  `52ed698c1117273c7488ec8cd31486611f849d748a6af620d59403bb6be25d3e`.
- Traversal, case-collision, boolean-size, closure-mismatch, tree-identity,
  and hostile ambient-`GIT_*` probes all failed closed.
- Foundation validation, staged and unstaged diff checks, assembly-definition
  JSON parsing, and independent static C# plus scope/security review passed.

## Failed and interrupted attempts retained

- The original core seam rejected the new Game files with
  `Game file closure drifted` before immutable-baseline plus exact-overlay
  validation was implemented.
- A concurrent source revision then produced the intended fail-closed
  `TechnicalSandboxController.cs` size-drift rejection until the overlay was
  re-snapshotted, re-pinned, and the exact candidate was staged.
- One validator run received a sandbox `PermissionError` while cleaning
  ignored `BuildArtifacts`. The identical command passed after receiving
  repository-local write access.
- The first static review rejected a PlayMode test that did not load the real
  scene or exercise real input, and found global RenderSettings cleanup gaps.
  The test now loads the registered scene and drives Input System devices, and
  controller teardown restores the captured render state.
- The follow-up review rejected an `AfterSceneLoad` bootstrap that only
  covered Unity's first scene. The frozen candidate instead installs an
  idempotent `SceneManager.sceneLoaded` handler before scene loading.

## Planned verification

1. Read the Unity Console through the approved direct MCP connection.
2. Run the WP-0003 EditMode and PlayMode tests through Unity MCP.
3. Enter the technical scene in Play Mode and exercise select, reset, camera,
   and persistence-gate controls.
4. Capture one technical screenshot and record all outcomes, including any
   failed or interrupted attempt.

Until those steps pass, this file is candidate evidence only and does not
satisfy T-UNITY-COMPILE, T-UNITY-EDITMODE, T-UNITY-PLAYMODE, or
T-TECHNICAL-CAPTURE.
