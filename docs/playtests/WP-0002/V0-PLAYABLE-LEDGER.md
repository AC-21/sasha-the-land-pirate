# WP-0002 V0 Playable Ledger

Status: **provisional execution ledger**

Baseline: `8f2b973e10682a77339b28bde67eb7a9ff5679bf`

Purpose: keep the next playable work vertically integrated, testable, and honest.

This ledger is implementation guidance, not a receipt, work-packet amendment, or
decision ratification. It does not expand WP-0002, select D-0030, resolve D-0039
or D-0044, authorize production art, or change save compatibility. Higher
constitutional sources and the active packet always win. An agent stops when a
slice depends on an unresolved decision or an unreserved path.

## V0 outcome

V0 is one 15–25 minute, saveable loop:

1. Choose a human-only, utility-robot-only, or mixed colony.
2. Read the failing water system from the strategy city view.
3. Inspect Sasha's rig in the garage and commit one of the two existing modules.
4. Depart, drive an authored corridor, and operate the module at a physical road
   interaction.
5. Scavenge or load physical cargo, seat the depot recovery bridle, and resolve
   the caravaner encounter.
6. Drive home under the visible load.
7. Credit the return and make one visible city improvement.
8. Manufacture exactly one physical workshop lot and, only under an accepted
   one-off contract, settle one bilateral caravaner barter.
9. Save, quit to title, load, and recover the exact city, colony, vehicle,
   cargo, faction, crisis, and workshop state.

This is a first playable proof, not a production alpha. It does not require the
population-gated caravan exchange, a broad building catalog, production art,
combat, or a campaign.

## Honest baseline and gaps

The stack already has a deterministic fixed-tick core, typed resident rosters,
human/robot/mixed loop coverage, one-scene mode routing, strategy and chase
cameras, a presentation-only road-physics rig, two vehicle modules, exact cargo
custody, an autonomous faction claim, a depot recovery gate, a C0 scout and
garage blockout, and atomic `last-bearing-dev-v1` save/load with current and
last-good generations.

The current experience is still a constitutional toy:

| Area | Current state | V0 gap |
|---|---|---|
| City | Water need, camera, reversible D-0030 comparison, and one fixed-socket auxiliary-pump improvement | No authoritative placement or logistics path |
| Driving | Real local physics shadowing deterministic progress/lateral commands, with explicit Wreck Line and depot-recovery verbs | Brake/reverse remain presentation-only; one corridor and two interactions are still thin |
| Garage | Stable C0 scout sockets and fixed dollhouse bay | Inspection only; module work is still a global HUD action |
| Depot/scavenging | Recovery gate, faction choice, heavy/liquid cargo fields and exact custody | Cargo appears through buttons; no operated salvage/load staging |
| Colony | Exact typed rosters and visible human/robot primitives | No lived work feedback; mechanical differentiation remains forbidden while D-0039 is open |
| Manufacturing/trade | One conserved spare-bearing batch, physical lot, one-off caravaner barter, and persistent corridor permit | No broader exchange by design; D-0044 remains open |
| Save | Exact disposable profile, fault-tested atomic store, and critical-transition autosaves | One dev slot; no production compatibility promise |
| Presentation | Temporary IMGUI, procedural primitives, and fixed cutaways | The released baseline still lacks a readable job arc, retained HUD, sound language, accepted meshes, textures, LODs, or audio |
| Mac proof | Lightweight URP project on the selected Editor | No clean native build or target-Mac thermal/frame/memory soak evidence |

## View and interaction constitution

### City

- Use a stable, isometric-leaning strategy view: provisional 35–45 degree
  elevation and 35–45 degree perspective FOV.
- Preserve building function, operating state, logistics, and roof silhouette at
  normal zoom. “Tilt-shift” describes the staged miniature-like composition; it
  does not authorize gameplay depth of field.
- A major improvement changes silhouette, motion, light, civic routine, or
  several of these. A button or color swap alone is not city growth.
- Canonical placement waits for D-0030. Until then, a fixed civic upgrade socket
  may prove the return consequence without selecting a city grammar.

### Driving

- The road view is an ordinary third-person chase camera emphasizing wheel
  contact, suspension, cargo, tool reach, and horizon.
- Quantized commands and the deterministic core author route progress, damage,
  cargo, time, and outcomes. Rigidbody pose and telemetry never do.
- Local physics may improve feel, braking, reversing, dust, suspension, and
  camera motion. Every canonical interaction ends at a computed gate and can
  suspend/snap the presentation to the canonical pose.

### Buildings

- Use fixed dollhouse cutaways with the shared camera: garage, pump hall, and
  workshop. No first-person/on-foot interior mode is part of V0.
- Cutaway inspection is presentation-only. Authoritative work occurs only
  through explicit core commands.
- Keep entrances, service access, input/output, residents, vehicle, and exit
  route readable together.

## VGR-03 — Wreck Line

Objective: make the first 60–90 seconds of road play pleasurable and turn each
installed module into an explicit verb.

Status: released on protected `main`.

Scope:

- Add one computed mid-route interaction gate without a new persisted gate
  field. Route progress cannot cross it until the matching module is operated.
- Winch path: deploy the winch and recover the existing pump rotor into vehicle
  custody. Move rotor creation from return freeze to this exact operation.
- Range-tank path: seal the tank and cross the dust exposure; its depot water or
  fuel choice remains unchanged.
- Derive presentation cargo mass and damage band from canonical read-model
  values and feed `RoadFeelVehicleController.SetLoad`.
- Add one exact road-interaction view, module deployment feedback, and cargo
  staging on the scout's existing sockets.
- Route `E`/gamepad south only while the computed gate is available. Preserve
  city-camera `E` behavior.
- Expose brake, reverse, and handbrake to the local road rig without creating a
  physics-to-core return channel.

Primary seams:

- `SimulationCore/Runtime/LastBearing/LastBearingCommands.cs`
- `SimulationCore/Runtime/LastBearing/LastBearingKernel.cs`
- `SimulationCore/Runtime/LastBearing/LastBearingReadModel.cs`
- `SimulationCore/Runtime/LastBearing/LastBearingOwnershipTransaction.cs`
- `Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingGameController.cs`
- `Game/Assets/AtomicLandPirate/LastBearing/Runtime/LastBearingModeCoordinator.cs`
- `Game/Assets/AtomicLandPirate/LastBearing/Runtime/RoadFeel/`
- `Game/Assets/AtomicLandPirate/LastBearing/Runtime/Vehicle/`

Acceptance:

- Both modules stop at the exact gate and cannot bypass it by driving, saving,
  loading, pausing, or presentation failure.
- Early, wrong-module, duplicate, and downstream replays are atomic and
  idempotent.
- Winch creates exactly one pump rotor; range tank creates no heavy cargo.
- Save/load preserves gate, module operation, cargo custody, route position, and
  faction timing byte-exactly.
- Physics pose/telemetry never changes canonical progress, damage, custody, or
  faction state.
- Keyboard and controller driving are understandable and worth continuing
  within 30 seconds.

Exclude: combat, a second road or POI, a third module, open-world streaming,
physics-authored outcomes, and production assets.

## VGR-04 — Bring It Home

Objective: close the emotional city-road loop with visible cargo, a satisfying
homecoming, one real city improvement, building cutaways, and recoverable saves.

Status: released on protected `main`.

Scope:

- Show canonical cargo on the road scout and let its load affect presentation
  handling only.
- Add an exact pump-hall cutaway using the established garage inspection-pose
  pattern; do not add an avatar controller.
- Turn the existing `NextCityDecision` into one bounded installation command.
  Use a fixed civic socket while D-0030 is open. If D-0030 is separately
  ratified, use only its accepted placement grammar and exact bounded pads.
- Conserve returned cargo and city resources exactly when committing the
  improvement.
- Make the installed improvement change city silhouette, machinery, water
  behavior, resident work, and tungsten light.
- Add a small autosave policy after successful critical domain events:
  departure, road-module operation, depot recovery, depot resolution, return
  freeze, home arrival, city credit, and city installation.
- Human, robot, and mixed colonies show their exact residents doing the same
  work. Costs, timing, viability, and commands remain identical.

Primary seams:

- `LastBearingState`, `LastBearingTypes`, commands, kernel, read model,
  invariants, balance, and canonical codec
- `LastBearingWorldBuilder`, `LastBearingCameraRig`, `LastBearingModeCoordinator`
- `LastBearingSaveAdapter` and `SaveContracts/Runtime/LastBearing/`
- EditMode, PlayMode, composition, ownership, and save tests

Acceptance:

- Returned cargo is credited and installed exactly once; invalid phase, pad,
  orientation, custody, and replay fail atomically.
- All three colony compositions complete the same city-road-return-install loop
  without a hidden human.
- Save/load at every critical transition restores the exact mode, cargo,
  composition, upgrade, clocks, and faction consequence.
- Cutaways use one shared camera, contain no player controller, and cannot write
  canonical state through inspection.
- A player reads the initial water need within five minutes and reads the return
  transformation within three seconds at normal strategy zoom.

Exclude: a generalized building kernel, free-form roads, multiple new buildings,
on-foot interiors, per-citizen simulation, differentiated composition mechanics,
and production save slots or compatibility promises.

## VGR-05 — One Good Batch

Objective: prove manufacturing and physical-goods trade with one conserved lot,
without implying that the population-gated caravan exchange is open.

Status: released on protected `main` at
`bea6dacaee95879f3f329d8faf478e8d0036fea2` under the exact bounded contract
`docs/playtests/WP-0002/VGR-05-ONE-GOOD-BATCH-CONTRACT.md`.

Scope:

- After the appropriate return, machine exactly one stable-ID spare-bearing lot.
- Store only the minimum authoritative batch state: recipe, phase, quantity,
  custody, and deterministic elapsed/required ticks. Any grade/condition field
  requires an explicit demonstrated use.
- Debit inputs exactly once at batch start; complete autonomously; show physical
  inputs, active machinery, and output in the workshop cutaway.
- Perform the accepted one-off barter: one spare-bearing lot for the fixed
  depot-corridor route permit while preserving the aggrieved history and
  two-fuel future toll.
- Transfer lot custody and consideration atomically, record faction memory, and
  show one caravaner claims counter/ledger with the physical lot and route board.
- Preserve identical recipe, timing, cost, and viability for all compositions.
- Replace or bound temporary IMGUI allocations if target-Mac profiling shows the
  HUD prevents the allocation gate.

Acceptance:

- Inputs, lot, custody, and consideration are conserved; no negative inventory,
  double lot, retry duplication, quality laundering, or post-transfer barter.
- Start, completion, and barter replays are idempotent.
- Save/load is exact during every batch and custody phase and fails closed on an
  unknown state version.
- Human-only, robot-only, and mixed colonies complete the same proof.
- No order book, global price, currency, population threshold, infinite buyer,
  self-match, or global-exchange claim exists in code, UI, or prose.

Exclude: recipe frameworks, factory trees, industrial specialization, multiple
lots, dynamic pricing, a regional market, or the global caravan exchange.

## VGR-06 — The Permit Job

Objective: make the released city-road-manufacturing loop readable and
finishable before adding another system.

Status: released on protected `main` in `8f2b973`.

Scope:

- Derive one presentation-only nine-step objective rail from the existing read
  model and city-inspection flag.
- Mark `Civic Buffer + Winch` and `Take the claimed bearing` as an optional
  first-run path to the longest implemented continuation.
- Expose exact preparation, route, and batch progress without adding authority.
- Replace the normal raw-objective fallback with authored player language.
- End the permit path with a concise consequence recap and close all other
  material outcomes as honest alternate endings.
- Preserve the current temporary IMGUI and shared-camera implementation so this
  slice stays removable when retained UI is separately authorized.

Acceptance:

- the golden path is understandable without reading internal identifiers;
- every alternate branch remains mechanically valid and never claims a permit;
- the presenter cannot mutate canonical state, issue commands, or touch saves;
- canonical bytes, codec version, balance, composition parity, and eligibility
  are unchanged;
- deterministic, Unity, visual, save, and protected gates remain green.

Exclude: a quest framework, retained UI package, new route, market, building
kernel, city placement, composition mechanics, production art, or save change.

Detailed contract:
`docs/playtests/WP-0002/VGR-06-THE-PERMIT-JOB-CONTRACT.md`.

## VGR-07 — Home Before Horizon

Objective: turn the D-0030 comparison into a small, playable city-building and
local-logistics observation without selecting a grammar.

Status: current implementation candidate; not released until independent,
Unity, protected, and creator-delegated release gates pass.

Scope:

- Hold one service-cell task, three authored sockets, colony presentation, and
  the exact provisional D-0022 camera constant.
- Trial A places the recycler and workshop separately on distinct restrained
  snap pads, then connects one physical service link.
- Trial B places the complete recycler/workshop/shared-apron district stamp on
  one authored anchor.
- Move the same empty calibration sled from recycler to workshop in both
  trials, then record only `clear` or `unclear` path legibility.
- Retain A/B layouts and raw observations in memory for reversible comparison;
  save and canonical state remain untouched.
- After either observation, converge on the already-existing infrastructure
  activation command without recording which presentation produced it.

Acceptance:

- both interactions complete, switch, reset, and clear exactly;
- invalid layouts and premature/duplicate logistics actions fail closed;
- trial verbs queue no command and preserve canonical hashes;
- either observed trial yields the same canonical infrastructure bytes;
- session boundaries clear transient trial state;
- evidence contains no score, recommendation, winner, or persistence;
- D-0030 remains open and all existing save/Unity/protected gates stay green.

Exclude: canonical placement, inventory delivery, population/scale tests,
NavMesh, production assets, retained UI, dependencies, save changes, or a
city-grammar recommendation.

Detailed contract:
`docs/playtests/WP-0002/VGR-07-HOME-BEFORE-HORIZON-CONTRACT.md`.

## Queued after VGR-07 — Stay With Sasha

The next bounded driving pass should integrate the existing tested chase camera
into the one shared Last Bearing camera and add presentation-only rig recovery.
It must not add core telemetry, save state, or a second camera. Exact scope and
contract remain unimplemented until VGR-07 releases.

## Visual constitution

- **Texas iron is the scale of work:** cast housings, plate steel, oilfield,
  rail, water, cable, canvas, service access, and repairs made with leverage.
- **Brutalist opera is the scale of civilization:** inherited concrete mass,
  deep apertures, cantilevers, axial approaches, and machinery staged as civic
  ritual. Player construction remains modular and maintainable.
- **Storybook salvage is the humane filter:** readable silhouettes, patched
  shade, personal tokens, paint, gardens, jokes, rituals, and visible care.
- **Tungsten stays over neon:** warm practicals mark inhabited work and safety;
  rare colored emissive marks legacy systems, faction claims, hazards, or
  commerce. Neon never carries state alone or becomes wallpaper.
- Read order at gameplay distance is mass, function, operating state, history.
- Reject cowboy-theme-park shorthand, random spikes/skulls, generic cyberpunk,
  pristine sci-fi panels, franchise-coded silhouettes, and detail that fails at
  the real gameplay camera.

## Tripo, Blender, and LOD policy

- Tripo output is concept/blockout evidence, never a shipping asset. Do not
  import the existing million-triangle FBX files into the runtime project.
- A 40–80k triangle Tripo regeneration may be useful only as isolated retopology
  reference. Runtime acceptance follows the game budget, not generator output.
- Sasha's current scout contract targets 24–28k triangles for base LOD0,
  32k maximum with a module, 9–12k for LOD1, and 3–5k for LOD2, with no more
  than three production material slots and a 2k texture set.
- Quads help editable topology; Unity renders triangles. Clean silhouette,
  deformation, normals, UVs, sockets, collision, and authored LODs matter more
  than an arbitrary quad-only export.
- Shipping candidates require a canonical `.blend`, provenance and terms,
  retopology, originality review, stable pivots/sockets, simple collision,
  authored LODs, gameplay-camera sheets, and target-Mac evidence.
- WP-0002 does not reserve `ContentSource/` and does not authorize production
  art. A separate accepted C1 asset packet is required before asset fan-out.

## Target-Mac gate

Use native ARM64. After a five-minute warm-up, measure 30 continuous minutes of
representative city, driving, depot, return, cutaway, save, and load play:

- whole-frame p95 <= 16.7 ms and p99 <= 25 ms;
- simulation tick p95 < 4 ms;
- process RSS < 6 GB;
- tracked Metal allocation < 3.5 GB;
- managed allocation p95 = 0 B/frame and average < 1,024 B/frame;
- frame-time degradation < 10 percent over the run;
- retained-memory growth < 5 percent;
- slice save < 25 MB, snapshot pause < 50 ms, save < 2 seconds, and load to
  interaction < 3 seconds.

Do not loosen a threshold, omit a loading/save interval, or hide a failed run.
Primitive performance is not evidence that later unbounded art will pass.

## Decision and reservation gates

- **D-0030 — city grammar:** open. Canonical placement cannot begin until the
  creator selects or supersedes a tested hypothesis. A fixed civic socket may
  prove one return upgrade without resolving it.
- **D-0039 — composition mechanics:** open. V0 keeps human, robot, and mixed
  colonies mechanically identical; do not add food/power/maintenance bonuses,
  staffing advantages, or mixed synergies.
- **D-0044 — exchange law:** open. VGR-05 may proceed only under an accepted
  exact one-off barter. The population metric, threshold, consideration,
  matching, fees, loss, disputes, and fall-below behavior remain future work.
- **Reservation expiry:** the current WP-0002 reservation records
  `2026-07-19T16:06:11Z`. Before work continues past that instant, verify a
  valid renewal/heartbeat and fencing token. Expiry is a stop condition, not an
  invitation to edit governance from an implementation branch.

## Agent execution rules

1. Start each slice from the accepted protected baseline on a fresh `agent/*`
   branch and stay inside the active reservation.
2. Keep canonical state in plain C# and presentation derived. Physics, camera,
   animation, UI, and previews never become a second truth.
3. Prefer existing state and custody seams. A new saved field needs an explicit
   default, codec/invariant coverage, save impact, and round-trip tests.
4. Every player command is phase-gated, atomic, idempotent on semantic replay,
   and covered against early, duplicate, save/load, and fault paths.
5. Preserve exact human/robot/mixed rosters and the same golden path.
6. Add no dependency, service, network need, production asset, or broad
   framework inside these slices.
7. Foundation scenario pins remain immutable. Candidate scenario/control-plane
   revisions go only under `BuildArtifacts/WP-0002/candidate-control-plane/`
   until separately accepted.
8. Run the headless suite, foundation/package/boundary checks, Unity compile,
   EditMode, PlayMode, visual captures, and `git diff --check` appropriate to the
   slice. Report every unrun or failed gate.
9. A green test suite does not establish fun, art acceptance, Mac performance,
   or creator ratification. Preserve observed-play evidence and honest limits.

V0 is complete only when the entire loop above works from title through exact
load on the creator Mac, the three composition smokes pass, the 2x2 preparation
and module matrix remains separate and green, every physical good is conserved,
the visual direction reads at gameplay camera, and all applicable protected and
human gates pass.
