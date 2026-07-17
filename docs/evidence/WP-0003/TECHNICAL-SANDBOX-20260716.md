# WP-0003 technical sandbox candidate

Status: **Unity compile, exact tests, live scene behavior, and technical capture passed**

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
  `68fb887cdc796470c9b76d69c52a63ae5badc2f8a89ac9453a6a0844b09db9e7`.
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

## Unity MCP validation

- The project-scoped client exposed exactly `Unity_ReadConsole`,
  `Unity_RunCommand`, `Unity_ManageEditor`, `Unity_ManageGameObject`, and
  `Unity_Camera_Capture`. `Unity_ReadConsole` was the first Unity call.
- The live target resolved to
  `/Users/sasha/Documents/Codex/sasha-the-land-pirate/Game`; the Editor
  reported version `6000.5.4f1`, was not in Play Mode, and was idle before the
  first refresh.
- Before refresh, the repaired PlayMode source was newer than its stale test
  DLL. The first refresh command was rejected before execution because its
  transient script did not fully qualify `CompilationPipeline`; the corrected
  command requested compilation and `AssetDatabase.Refresh` through MCP.
- After the final `AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate)`, the
  repaired source mtime was `2026-07-16 20:56:33 -0700` and the compiled
  `AC21.Sasha.TechnicalSandbox.PlayModeTests.dll` mtime was
  `2026-07-16 20:57:38 -0700`. The subsequent Error/Warning console read
  returned zero entries.
- `AC21.Sasha.TechnicalSandbox.EditModeTests` ran alone through
  `TestRunnerApi`: 4 passed, 0 failed, 0 skipped, and 0 inconclusive. The four
  passing tests were `ActivationIsBoundedAndInspectable`,
  `NewStateIsNeutralAndNonPersisting`,
  `PersistenceProbeFailsClosedAndWritesZeroBytes`, and
  `ResetClearsPresentationState`.
- The first exact `AC21.Sasha.TechnicalSandbox.PlayModeTests` run failed 0/1
  because the test queried newly transformed primitive colliders before an
  explicit physics synchronization. The native NUnit receipt was
  `/Users/sasha/Library/Application Support/AC-21/Sasha the Atomic Land Pirate/TestResults.xml`,
  SHA-256
  `2d14a02e623c7f3439500038b0529bdc647c765af6dc057d078fc063c6e449ad`;
  it identified `TechnicalSandboxPlayModeTests.cs:70`.
- The minimal repair adds `Physics.SyncTransforms()` before the helper
  raycast. The repaired source is SHA-256
  `bd6efd5997f0733cc4061d14b8c1970c611ce2bad77a5baf6b1a6110c1a96b1c`,
  and the updated overlay manifest is SHA-256
  `68fb887cdc796470c9b76d69c52a63ae5badc2f8a89ac9453a6a0844b09db9e7`.
- The rerun of that exact PlayMode assembly passed 1/1 with 0 failed,
  skipped, or inconclusive. Its regenerated native NUnit receipt ended at
  `2026-07-17T03:58:16Z`, has SHA-256
  `c7dd9b32c8564421e3762303676285d0591b247acda489b6cc0f2a8007b50d77`,
  and names only
  `SceneBootsAndRespondsToInputSystemControls` in
  `AC21.Sasha.TechnicalSandbox.PlayModeTests.dll`.
- The registered `WP0003_TechnicalSandbox` scene opened from its exact asset
  path, entered Play Mode, and exposed one runtime controller and technical
  camera. The bounded live exercise selected probe 2 with one interaction,
  moved the camera focus, returned
  `WP0003_PERSISTENCE_DISABLED` with 0 bytes written, reset the selected index
  to -1 and interactions to 0, then exited Play Mode cleanly.

## Capture result and tool limitation

- `Unity_Camera_Capture` successfully returned a 1920 x 1080 PNG Scene View
  preview of the live technical scene after the bounded debug exercise and
  reset (`213099` bytes), proving the approved capture transport. That image
  is not retained as the reviewable packet screenshot and does not by itself
  satisfy the runtime debug-HUD coverage required by
  `T-TECHNICAL-CAPTURE`.
- The accepted runtime Camera GameObject handle `-7886` and Camera component
  handle `-7890` were each resolved from the live controller. The installed
  capture implementation rejected both with `No GameObject found with
  Instance ID`, even though the component was live and the valid component
  handle was supplied. The first legacy-handle lookup also failed before
  execution because Unity 6000.5 treats direct `GetInstanceID()` use as an
  obsolete-API compile error; the bounded reflection lookup then succeeded.
- Per creator direction, no further capture loop or persistent callback was
  added. The Scene View image is retained as transport evidence only; the
  runtime-camera rejections are an installed-tool limitation, not hidden as a
  successful runtime-camera capture.
- The qualifying reviewable screenshot is retained at
  `docs/evidence/WP-0003/WP0003-TECHNICAL-CAPTURE-20260716.png`. It is a
  `2560 x 1440` PNG of `704963` bytes with SHA-256
  `39031855ba22baf47fbbcc464403e808a387f57b3b9892cbca75365259b83878`.
- Capture method: while the registered scene was live in Play Mode, one
  bounded `Unity_RunCommand` found `TechnicalSandboxController`, activated
  probe 2, verified selected probe `2` and interaction count `1`, then invoked
  `UnityEngine.ScreenCapture.CaptureScreenshot` with supersize `1` and the
  absolute reserved evidence path. Unity scheduled the screenshot for the end
  of the frame; the host polled that exact path until the PNG was nonzero
  before Play Mode stopped.
- Visual inspection of the retained PNG confirms the live technical scene,
  highlighted active probe, and debug HUD/state: `Selected probe: 02`,
  `Presentation interactions: 1`, `Persistence: DISABLED / 0 bytes`, and
  `PROBE ACTIVE - presentation state only`. This is technical proof only, not
  production art or gameplay acceptance.
- After capture, the Editor exited Play Mode, a bounded forced asset refresh
  completed with the Editor idle, and the fresh Error/Warning console read
  returned zero entries.

`T-UNITY-COMPILE`, `T-UNITY-EDITMODE`, `T-UNITY-PLAYMODE`, and
`T-TECHNICAL-CAPTURE` are satisfied by the evidence above. The technical
capture gate is satisfied by the retained Play Mode PNG with visible HUD/state,
not by re-labeling the Scene View transport image. This remains implementation
evidence, not creator or independent acceptance.
