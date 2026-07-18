# VGR-08 — Stay With Sasha Contract

Status: creator-directed presentation milestone; release remains conditional on
independent review, exact Unity dispatcher gates, protected checks, and
transparent creator-delegated manual release.

Baseline: protected `main` commit
`b35157a07a7442f6b02ed179201cd59e89368996`.

## Proof

Make the released Last Bearing road leg feel like driving Sasha's rig without
creating another camera, another vehicle truth, or an automatic escape from a
bad local physics pose.

The existing `RoadFeelChaseCamera` becomes the road-pose owner on the sole
shared Last Bearing camera. A player may explicitly recover the presentation
rig to its current canonical route pose when local physics becomes unhelpful.
Neither camera motion nor recovery can author route progress, outcomes, cargo,
damage, clocks, or save data.

## One camera, explicit ownership

- The Last Bearing runtime retains exactly one `Camera` and one `AudioListener`.
- That shared camera carries the existing `LastBearingCameraRig` and exactly one
  `RoadFeelChaseCamera`; no child, fallback, transition, or recovery camera is
  created.
- Strategy, comparison, garage, and building-cutaway presentation continue to
  own their existing poses through `LastBearingCameraRig`.
- The chase component owns camera transform and field of view only while the
  mode coordinator has an available, non-faulted road adapter and the road
  physics presentation is actively running in canonical `Driving` mode.
- The strategy/fixed-road rig must not write a competing pose while chase
  ownership is active. On pause, canonical interaction hold, adapter fault,
  mode change, title, new game, or load, chase ownership ends and that shared
  rig restores the authorized pose and field of view.
- Entering active driving, resuming it, and completing a manual recovery snap
  the chase behind the presentation rig once. Chase position is never reset on
  every frame or every canonical drive tick.

The reused chase behavior remains presentation-only: speed look-ahead, elastic
distance, collision contraction, world-up horizon, mouse/right-stick orbit,
idle recenter, and explicit recenter may read Unity transforms, input, and
Rigidbody velocity but may not feed any value back into the simulation.

## Manual rig recovery

`R` or gamepad north requests one presentation recovery only when all of these
conditions hold:

- a game is active;
- canonical mode is `Driving`;
- the road presentation is actively running rather than paused or held at the
  Wreck Line or depot recovery interaction;
- the road adapter is attached and has not faulted; and
- the current canonical vehicle pose is available.

An accepted request:

1. clears local throttle, steering, brake, handbrake, linear velocity, and
   angular velocity;
2. synchronizes the presentation Rigidbody to the current canonical vehicle
   position and rotation through the existing adapter boundary;
3. preserves the already-derived presentation cargo mass and damage band;
4. recenters the chase camera behind the recovered rig; and
5. reports a concise presentation-only status.

An unavailable request is a no-op. It queues no command and must not alter the
canonical hash, sequence, pending command count, save bytes, active mode,
camera owner, or canonical interaction availability.

## No automatic recovery

The Last Bearing runtime does not copy the Road Feel Lab's boundary or
inversion watcher. Falling, rolling, leaving the authored road, settling
upside-down, losing camera line of sight, idling, or exceeding a timer may
never trigger recovery. The local rig remains where physics left it until the
player requests recovery or an already-existing canonical mode/interaction
transition synchronizes presentation.

This preserves player agency and prevents presentation telemetry from becoming
an implicit simulation command. It does not prevent a future separately
authorized accessibility or anti-stuck design.

## Lifecycle and failure containment

- Wreck Line and depot interaction holds keep their existing exact canonical
  snap, suspended physics, and interaction verbs. Manual recovery is unavailable
  during either hold.
- Pause suspends the road presentation and disables manual recovery; resume
  re-synchronizes through the existing presentation boundary before chase
  ownership returns.
- Leaving Driving, clearing the session, title, new game, and load clear local
  controls and return camera ownership without persisting camera orbit or rig
  pose.
- A chase-camera or adapter failure must fail closed to the existing canonical
  vehicle presentation. It cannot block canonical driving, interaction,
  save/load, or mode progression.
- Recovery replay is harmless presentation reset behavior, not semantic
  idempotency evidence for a gameplay command.

## Authority boundary

- Add no canonical command, field, event, balance, route rule, interaction,
  inventory, population, logistics, codec, save version, dependency, scene,
  asset, or project-setting change.
- Do not reference Rigidbody pose, velocity, camera state, obstruction hits,
  inversion, course bounds, or recovery counts from `SimulationCore` or
  `SaveContracts`.
- Do not add an adapter output, telemetry callback, physics-to-core seam,
  filesystem write, network call, `PlayerPrefs`, checkpoint, or recovery save
  field.
- Existing quantized `DriveVehicleCommand` input remains the only road-progress
  authority. Existing computed Wreck Line and depot gates remain the only
  interaction authority.
- D-0030, D-0039, D-0044, D-0045, and D-0046 remain open and unchanged.
- The standalone Road Feel Lab may retain its authored course instruments and
  automatic lab recovery; those lab-only behaviors are not imported into the
  Last Bearing runtime.

## Acceptance

- the Last Bearing scene contains exactly one enabled camera, one audio
  listener, one camera rig, and one chase component through every mode;
- strategy, comparison, garage, and building-cutaway poses remain unchanged,
  and the strategy rig never competes with active chase ownership;
- active outbound and return driving use the existing chase orbit, recenter,
  speed look-ahead, horizon, collision, and field-of-view behavior;
- exiting active driving restores the correct non-road pose and field of view;
- a valid manual recovery resets local motion and controls, preserves derived
  load and condition, snaps to the current canonical pose, and recenters chase;
- recovery attempts at title, city, depot, return presentation, pause,
  interaction hold, missing adapter, or faulted adapter fail closed;
- no time, bounds, inversion, velocity, grounding, or camera condition can
  invoke automatic recovery in Last Bearing;
- valid, invalid, repeated, paused, held, save/load, and adapter-fault recovery
  tests preserve canonical bytes and create no pending command;
- human-only, robot-only, and mixed colonies retain identical mechanics;
- deterministic/source, foundation, Unity compile, EditMode, PlayMode,
  technical-capture, protected, and diff gates pass.

The green gates prove camera/recovery containment, not final road fun. Creator
play must still judge framing, motion comfort, recovery clarity, and whether
the drive stays visually attached to Sasha's rig.
