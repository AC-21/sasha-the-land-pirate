# WP-0002 V0 Playable Ledger

Status: **provisional execution ledger**

Baseline: `2ddbc3f90c14a5fa9bec2858ce9ff06b350a80db`

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
garage blockout, workshop-local manufacture and claims-wicket handoff, and
atomic `last-bearing-dev-v1` save/load with current and last-good generations.

The current experience is still a constitutional toy:

| Area | Current state | V0 gap |
|---|---|---|
| City | Water need, camera, working service cell, one-shot emergency-cistern reserve, physical return check-in and pump-hall repair, and one fixed-socket auxiliary-pump improvement | City production remains narrow; auxiliary-pump installation is still one fixed authored socket |
| Driving | Real local physics shadowing deterministic progress/lateral commands, with explicit Wreck Line and depot-recovery verbs | Brake/reverse remain presentation-only; one corridor and two interactions are still thin |
| Garage | Stable C0 scout sockets, fixed dollhouse bay, and garage-local module commitment | Module assembly remains one authored operation; no generalized rig-upgrade path |
| Depot/scavenging | Recovery gate, faction choice, heavy/liquid cargo fields, exact custody, and one operated repair-cargo handoff | Range-tank liquid selection and return sealing remain HUD-led; one depot and its interactions are still thin |
| Colony | Exact typed rosters and visible human/robot primitives | No lived work feedback; mechanical differentiation remains forbidden while D-0039 is open |
| Manufacturing/trade | One conserved spare-bearing batch, physical lot, workshop-local start, claims-wicket barter, and persistent corridor permit | The physical handoffs still use the temporary legacy HUD inside one fixed cutaway; no broader exchange by design while D-0044 remains open |
| Save | Exact disposable profile, fault-tested atomic store, and critical-transition autosaves | One dev slot; no production compatibility promise |
| Presentation | Temporary IMGUI, procedural primitives, fixed cutaways, and a readable Permit Job rail | The released baseline still lacks retained UI, sound language, accepted meshes, textures, LODs, or audio |
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

Status: released on protected `main` in `b35157a`.

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

## VGR-08 — Stay With Sasha

Objective: make the released road leg feel attached to Sasha's physical rig by
integrating the existing tested chase behavior and one explicit recovery verb.

Status: released on protected `main` in `ab5da82` after independent review,
exact Unity dispatcher gates, protected checks, and transparent
creator-delegated manual release.

Scope:

- Put exactly one existing `RoadFeelChaseCamera` and the sole `AudioListener` on
  the sole shared Last Bearing camera; add no second camera or listener.
- Let chase own transform and field of view only while the road physics
  presentation is actively running under canonical Driving mode.
- Preserve the existing strategy, D-0030 comparison, garage, and building
  cutaway camera poses, and restore their field of view when chase yields.
- Keep existing chase orbit, recenter, speed look-ahead, horizon, and collision
  behavior presentation-only.
- Add `R`/gamepad north manual recovery during active driving only: clear local
  controls and motion, synchronize the physics shadow to the current canonical
  vehicle pose, preserve derived load/condition, and recenter chase.
- Add no boundary, inversion, timer, or telemetry-triggered automatic recovery
  to Last Bearing.

Acceptance:

- every mode retains one shared camera and one unambiguous pose owner;
- active outbound and return driving use the existing chase behavior;
- pause, interaction holds, faults, and non-road modes cannot invoke manual
  recovery and restore the appropriate camera ownership;
- valid and repeated recovery preserve canonical bytes, pending commands, save
  data, load/condition, and interaction availability;
- no presentation condition invokes recovery without explicit player input;
- deterministic, Unity, visual, save, and protected gates remain green.

Exclude: physics-authored progress, automatic anti-stuck behavior, a second
camera, camera/recovery save state, new route or interaction rules, core
telemetry, dependencies, assets, and any D-0030, D-0039, or D-0044 selection.

Detailed contract:
`docs/playtests/WP-0002/VGR-08-STAY-WITH-SASHA-CONTRACT.md`.

## VGR-09 — Rig the Scout

Objective: move the existing preparation/module commitment into Sasha's fixed
garage cutaway so the expedition begins around the physical rig rather than a
global four-button plan panel.

Status: released on protected `main` in `cf135abe` after independent review,
exact Unity dispatcher gates, protected checks, and transparent
creator-delegated manual release.

Scope:

- Choose Workshop Push or Civic Buffer as an uncommitted presentation intent,
  then open the existing garage cutaway.
- Commit Winch Assembly or Sealed Range Tank only from the garage and queue the
  existing preparation/install command pair unchanged.
- Keep both module stands, a physical preparation marker, the scout sockets,
  and the existing assembly gauge readable in the sole shared-camera frame.
- Clear the transient intent on cancel, title, new game, load, or successful
  commit; never save or infer it.
- Preserve all four canonical preparation/module outcomes and all composition
  parity.

Acceptance:

- the player-facing flow is city posture -> garage inspection -> module commit;
- intent-only actions queue no command and preserve canonical/save bytes;
- valid garage commitment is byte-equivalent to the existing composite action;
- early, stale, non-garage, away, duplicate, and invalid commits fail closed;
- no canonical, save, dependency, scene, asset, or open-decision change exists;
- deterministic, Unity, visual, save, and protected gates remain green.

Exclude: a generalized upgrade system, free-form hardpoints, a third module,
crafting, on-foot interaction, production assets, or physics-authored state.

Detailed contract:
`docs/playtests/WP-0002/VGR-09-RIG-THE-SCOUT-CONTRACT.md`.

## VGR-10 — Work the Depot

Objective: make the repair-cargo outcome a physical, player-controlled custody
handoff from its truthful depot source to Sasha's scout.

Status: released on protected `main` in `b87a353` after independent review,
exact Unity dispatcher gates, protected checks, and transparent
creator-delegated manual release.

Scope:

- Resolve cooperation to Field Sleeve/Faction custody and taking the bearing to
  Ceramic Bearing at its truthful pre-resolution Depot or Faction custody.
- Commit trust, grievance, and memory at resolution; commit taken-bearing
  disposition and depleted control only when the bearing is physically loaded.
- Load the exact staged cargo into Vehicle custody with one narrow contextual
  command before return freeze.
- Show mutually exclusive physical source and scout-socket markers through the
  sole shared depot camera.
- Autosave both the resolved source-custody checkpoint and accepted load;
  preserve old already-loaded development saves.
- Keep all three colony compositions mechanically identical.

Acceptance:

- source custody is canonical, byte-stable, visible, and required before load,
  including faction-held and unclaimed sources for the ceramic bearing;
- only the exact source-to-vehicle action unlocks the existing return flow;
- replay is harmless and invalid or mistimed loads fail closed;
- save/load restores exact custody and interaction availability;
- no generalized inventory, trade, exchange, on-foot, dependency, asset,
  scene, package, project-setting, balance, or open-decision change exists;
- deterministic, Unity, visual, save, and protected gates remain green.

Exclude: arbitrary pickup/drop, cargo selection, weight/slot systems, theft,
barter, caravan exchange, manufacturing stock, production assets, or physics-
authored state.

Detailed contract:
`docs/playtests/WP-0002/VGR-10-WORK-THE-DEPOT-CONTRACT.md`.

## VGR-11 — Seat the Repair

Objective: make the loaded homecoming and turbine repair one physical city-road
handoff instead of two global HUD actions.

Status: released on protected `main` in `ba39285` after independent review,
exact Unity dispatcher gates, protected checks, and transparent
creator-delegated manual release.

Scope:

- Arrive in the existing `CityReturn` mode with the exact repair cargo still
  visible on Sasha's scout at one fixed home service apron.
- Use one contextual check-in action to queue the existing city-credit and
  transaction-finalize command pair unchanged.
- Keep repair cargo in `Vehicle` custody through check-in; do not teleport,
  stage, consume, or hide it before the repair command is accepted.
- Open, or direct the player into, the existing fixed pump-hall cutaway and
  frame the scout, cargo, failing turbine, service access, exact residents, and
  exit route through the sole shared camera.
- Use one pump-hall contextual action to queue the existing turbine-install
  command unchanged, then show ceramic-bearing `Turbine` custody or consumed
  field-sleeve custody and the existing repaired-city consequence.
- Re-derive both actions, framing, and markers from canonical state after save,
  load, title, new game, and mode changes; add no saved transient intent.
- Keep all three colony compositions mechanically identical and show only the
  exact resident kinds present in the canonical roster.

Acceptance:

- check-in is available only in the exact returned/return-pending CityReturn
  state and is byte-equivalent to the existing credit/finalize composite;
- accepted check-in finishes at `AtHome`/`Finalized` while leaving repair kind,
  `Vehicle` custody, ordinary-cargo occupancy, and turbine condition unchanged;
- the install action is available only in the selected pump-hall cutaway with
  the finalized, failing-turbine, vehicle-cargo state and queues exactly one
  existing install command;
- ceramic repair transfers `Vehicle` to `Turbine`; field-sleeve repair transfers
  `Vehicle` to `Consumed`; no early hide, duplicate cargo, or stale scout marker
  exists;
- early, stale, wrong-mode, invalid-state, repaired, duplicate, and lifecycle
  requests queue no command and preserve canonical/save bytes;
- arrival, finalized-before-repair, and repaired checkpoints round-trip
  byte-exactly and restore the same derived mode, markers, and availability
  without a codec field, save-version change, or migration;
- the fixed apron and pump-hall target remain presentation-only sockets and do
  not select D-0030; composition mechanics and exchange law remain unchanged;
- one shared camera/listener and physics-free views preserve exact
  human/robot/mixed parity, and all deterministic, Unity, visual, save, and
  protected gates remain green.

Exclude: a new core command, canonical field/event, free-form placement or
logistics, arbitrary unload/carry/pickup, repair recipe or minigame, avatar,
walking mode, generalized interaction framework, production asset, dependency,
scene, package, project setting, audio asset, composition differentiation, or
caravan exchange.

Detailed contract:
`docs/playtests/WP-0002/VGR-11-SEAT-THE-REPAIR-CONTRACT.md`.

## VGR-12 — Work the Wicket

Objective: finish the implemented Permit Job through the physical machine-shop
and claims-wicket cutaway instead of two global HUD actions.

Status: released on protected `main` in `3812bbe` after independent review,
exact Unity dispatcher gates, protected checks, and transparent
creator-delegated manual release.

Scope:

- Open or route to the existing One Good Batch cutaway without queuing a
  command, mutating canonical state, or writing a save.
- Start the batch only from the selected active workshop with `E`, gamepad
  south, or the equivalent semantic HUD action, and queue exactly the existing
  `StartSpareBearingBatchCommand`.
- Preserve the existing two-part cost, retained reserve, fixed recipe, one-lot
  output, and 120 unpaused settlement ticks unchanged.
- Show the existing canonical sequence at the fixed anchors: two input parts,
  machine workpiece, one workshop-output lot, then the same lot at the claims
  counter.
- Barter only from the selected active workshop/claims cutaway and queue exactly
  the existing `BarterSpareBearingLotCommand`.
- Preserve the depot grievance, future two-fuel toll, one-off contract, and
  fixed route permit exactly; do not imply that the caravan exchange is open.
- Re-derive view guidance, markers, motion, custody, permit state, and action
  availability after save, load, title, new game, and cutaway changes without a
  saved transient intent.
- Keep all three colony compositions mechanically identical and show only the
  exact resident kinds present in the canonical roster.

Acceptance:

- batch start and barter are available only in the selected active One Good
  Batch cutaway with their exact canonical eligibility and no pending command;
- each valid contextual request queues one unchanged existing command, while
  early, stale, wrong-mode, wrong-cutaway, duplicate, and lifecycle requests
  queue nothing and preserve canonical and save bytes;
- accepted start debits inputs once, advances only on the existing canonical
  clock, and creates exactly one lot in `WorkshopOutput` custody;
- accepted barter transfers that lot once to `LastBearingClaimsCounter`, grants
  only the existing depot-corridor permit, and preserves adverse history and
  toll;
- ready, in-progress, complete, and settled checkpoints round-trip byte-exactly
  and restore truthful physical stock, machine motion, custody, permit,
  guidance, and contextual availability;
- a Unity golden-path smoke runs from title through `Civic Buffer + Winch +
  Take Bearing`, return check-in, pump-hall repair, manufacture, barter, manual
  save, title, and exact load without direct state injection or manual view
  staging;
- human-only, robot-only, and mixed parity, one shared camera/listener, and all
  deterministic, Unity, visual, save, and protected gates remain green.

Exclude: a second recipe or lot, arbitrary inventory or logistics, generalized
interaction or quest frameworks, retained-UI redesign, currency, prices, order
books, matching, population gates, global or regional exchange, production
assets, dependencies, scenes, packages, project settings, save or codec
changes, and any D-0030, D-0039, or D-0044 selection.

Detailed contract:
`docs/playtests/WP-0002/VGR-12-WORK-THE-WICKET-CONTRACT.md`.

## VGR-13 — Field Desk

Objective: give the normal strategy city view its first retained, player-facing
interface without changing the implemented game or removing the legacy HUD.

Status: released on protected `main` in `7f105741` after deterministic, Unity,
protected, visual, and creator-delegated release gates. The final native soak was
quarantined before performance start after Unity reproduced the same Build
Profile source-serialization defect on its one authorized retry; no current-head
native performance claim is made.

Scope:

- Build one retained UI Toolkit Field Desk only for an active exact
  `CityOverview`; title, return, cutaways, garage, road, and depot retain the
  existing legacy IMGUI surface.
- Present the existing Permit Job docket, civic pressure, current city order,
  reversible D-0030 survey board, service controls, save status, and subordinate
  audit line from existing read-only projections.
- Delegate every valid city intent to one named existing
  `LastBearingGameController` method; never construct a command or touch the
  kernel, state, world, mode coordinator, or save boundary directly.
- Fail open to the legacy HUD if the retained surface cannot initialize, and
  prevent simultaneous surfaces, duplicate callbacks, stale clicks, and
  one-frame double actions during mode or lifecycle changes.
- Preserve canonical and save bytes for rendering, focus, collapse, inspection,
  trial, routing, and no-op interaction; keep explicit save and existing
  accepted command effects exactly unchanged.
- Express Texas iron x brutalist opera through civic hierarchy and workmanlike
  panels, storybook salvage through warmth and readable care, and tungsten over
  neon through warm normal light with rare semantic color only.

Acceptance:

- retained and legacy surfaces are mutually exclusive under the exact mode
  matrix, including a usable legacy failure fallback in city overview;
- the Field Desk is projection-only, delegates at most once per valid action,
  and preserves pending commands, canonical hash, and save bytes on every stale,
  duplicate, wrong-mode, and lifecycle request;
- Permit Job output and D-0030 evidence remain exact, composition-neutral, and
  free of a new score, recommendation, authority, or persistence seam;
- one stable retained tree meets the zero-p95 allocation and repeated-entry
  memory gates without a package, scene, project-setting, production-art, core,
  or save change;
- the full title-to-permit-save-load golden path, three composition smokes,
  visual captures, Unity gates, protected checks, and rollback proof pass.

Exclude: retained UI outside exact city overview, legacy-HUD removal, a broad UI
framework, commands or gameplay rules, production art/audio, dependencies,
scenes, packages, project settings, save/codec changes, and any D-0030, D-0039,
or D-0044 selection.

Detailed contract:
`docs/playtests/WP-0002/VGR-13-FIELD-DESK-CONTRACT.md`.

## VGR-14 — Working Service Cell

Objective: replace the reversible city comparison with one small, canonical,
saved city-building and local-logistics task that a first-time player can finish.

Status: released on protected `main`; the current baseline includes direct
physical service-cell operation.

Scope:

- Place a recycler, machine shop, and emergency storage on five authored pads
  using quarter-turn orientation and existing reclaimed parts.
- Commit one recycler-to-shop service link, which locks the layout.
- Staff one typed machine-shop slot with an eligible resident from the selected
  human, robot, or mixed roster without composition modifiers.
- Move one visible aggregate sled through three deterministic stages and credit
  exactly 2 reclaimed parts once on delivery.
- Persist the completed city state in schema v4 with one exact v3 migration.
- Reuse the Field Desk, existing city objects, camera, resources, clock, and
  zero-allocation simulation hot path.

Acceptance:

- costs are 2/3/1 parts for buildings and 1 part for the committed link;
- preview/cancel are free, pre-link repositioning is free, and no demolition or
  refund exists in this bounded slice;
- invalid, duplicate, premature, unaffordable, and replayed commands fail closed;
- water pressure continues to fall while the player builds;
- all three colony compositions complete the same service-cell task;
- save, title, load, v3 migration, v4 round trip, and exact continuation pass;
- compile, targeted deterministic tests, relevant EditMode/PlayMode tests, and a
  3–5 transition native smoke cover the changed dependency surface.

Exclude: a generalized building kernel, freeform terrain, NavMesh logistics,
composition bonuses, broad production chains, market rules, production art,
new scenes/packages/dependencies, or a normal-PR 100-cycle soak.

Detailed contract:
`docs/playtests/WP-0002/VGR-14-WORKING-SERVICE-CELL-CONTRACT.md`.

## VGR-15 — Emergency Cistern

Objective: turn the placed emergency-storage building into one clear survival
decision before Sasha leaves.

Status: released on protected `main` in PR #113.

Scope:

- Make the Field Desk and legacy fallback route the player to a physical pump
  lever beside Emergency Storage; routing alone changes no resources.
- Require a fresh `E`, gamepad South, or pointer activation at the lever before
  spending exactly 1 fuel and adding exactly 10,000 `WaterMilli` once.
- Preserve the planned route fuel reserve and reject partial fill or spill.
- Require the commissioned service cell, placed storage, available operator,
  committed rig plan, Sasha home, unresolved Dust Front, and inactive Hot Shift.
- Persist one explicit charged flag in schema 9 with deterministic schema-8
  default `false`.
- Autosave completion and derive a full marker on emergency storage.
- Keep human, utility-robot, and mixed colonies mechanically identical.

Acceptance:

- invalid and repeated attempts conserve fuel and water exactly;
- the base Dust Front brink changes from `Breached` without the fill to `Held`
  with it;
- current round trip, schema-8 migration, canonical mechanical hash, physical
  command delegation, fresh-input arming, and derived presentation are directly
  tested;
- no new scene, package, dependency, asset, generalized storage system, or
  everyday performance soak is introduced.

Detailed contract:
`docs/playtests/WP-0002/VGR-15-EMERGENCY-CISTERN-CONTRACT.md`.

## VGR-16 — Face the Dust Front

Objective: turn the resolved Dust Front verdict into one physical colony action
instead of a direct Field Desk acknowledgement.

Status: released on protected `main` in PR #114.

Scope:

- Make the Field Desk route to a physical Dust Front relay beside Emergency
  Storage without queuing a command, mutating canonical state, or writing a
  save.
- Require a fresh `E`, gamepad South, or pointer activation at the focused relay
  before queuing exactly the existing `AcknowledgeDustFrontCommand`.
- Show a Held verdict as a full tungsten signal and resume existing settlement
  clocks after acknowledgement.
- Show a Breached verdict as a stop-red signal with the physical safety shutter
  closed; preserve the existing Hot Shift stall until the existing turbine
  repair.
- Retain the existing acknowledgement fallback outside city overview or when
  the derived physical relay control is genuinely unmaterialized, preventing
  a global pause deadlock without treating stale presentation as missing.
- Derive relay placement and verdict presentation from Emergency Storage and
  the current read model; persist no focus or input state.

Acceptance:

- route-only actions preserve pending commands, canonical hash, resource state,
  and save bytes;
- held, stale, wrong-mode, duplicate, and lifecycle requests fail closed;
- a missing-control route fails closed while the explicit fallback remains
  reachable and queues exactly once;
- the fresh physical action delegates exactly one existing acknowledgement
  command, and the accepted tick retains the existing autosave behavior;
- Held and Breached presentations remain distinct by label and silhouette as
  well as color after acknowledgement and reload, and the Breached Hot Shift
  stall remains exact; after repair, the historical stop-red witness remains
  while its label and shutter truthfully show that the repair holds;
- save/load, four city-to-garage transitions, collision-safe placement, pointer
  targeting, fallback availability, and input ownership are directly tested;
- no SimulationCore, SaveContracts, schema, migration, balance, scene, package,
  dependency, production asset, or everyday performance-soak change is
  introduced.

Detailed contract:
`docs/playtests/WP-0002/VGR-16-FACE-THE-DUST-FRONT-CONTRACT.md`.

## VGR-17 — Make the Wreck Line Bite

Objective: make the existing Wreck Line corridor communicate route choice
through the rig's existing four-contact handling and telemetry.

Status: current implementation target on the V0 feature branch.

Scope:

- Tag the launch apron, collapsed shortcut, and exposed long route with the
  existing fixed concrete, washboard, sand, and gravel presets.
- Keep the existing rig, input adapter, chase camera, canonical driving
  commands, authored corridor geometry, and scavenging interactions.
- Prove each actual corridor segment reaches dominant-surface telemetry while
  physics remains presentation-only.
- Preserve camera ownership, explicit recovery, deterministic state, and save
  boundaries.

Acceptance:

- the apron reads concrete, the collapsed branch reads washboard, and the
  exposed long route reads sand then gravel under Sasha's grounded rig;
- surface sampling cannot change canonical state, command sequence, saves, or
  recovery availability;
- the shared camera and AudioListener remain singular;
- focused PlayMode tests and a short native drive cover the changed dependency
  surface.

Exclude: simulation or save changes, new commands, route balance, vehicle
physics tuning, generalized terrain, new scenes/packages/dependencies,
production art, new scavenging interactions, or a normal-PR performance soak.

Detailed contract:
`docs/playtests/WP-0002/VGR-17-MAKE-THE-WRECK-LINE-BITE-CONTRACT.md`.

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

## Validation ladder

Use only the gate tier affected by the changed dependency surface:

1. **Inner loop:** compile, targeted deterministic tests, and relevant EditMode
   or PlayMode tests.
2. **Gameplay PR:** the inner loop plus a short native ARM64 smoke, 3–5 mode
   cycles, and direct verification of the changed mechanic.
3. **Milestone/nightly:** full native build, five-minute warmup, paused and
   representative unpaused performance phases, and the 100-cycle soak. The
   broader 30-minute representative V0 profile remains a release milestone.

Documentation, UI copy, isolated content, and unrelated assets do not invalidate
an existing complete performance proof. Long gates run in parallel with product
work. Build, start, and collect use one clean Unity session. Zero-test results,
duplicate TypeDB registration, stale attestation, or editor corruption receive
one automatic retry; the same infrastructure failure twice is recorded as a
harness defect and fixed or quarantined instead of retried unchanged.

Release thresholds remain whole-frame p95 <= 16.7 ms and p99 <= 25 ms,
simulation tick p95 < 4 ms, process RSS < 6 GB, tracked Metal allocation < 3.5
GB, managed allocation p95 = 0 B/frame and average < 1,024 B/frame, frame-time
degradation < 10 percent, retained-memory growth < 5 percent, slice save < 25
MB, snapshot pause < 50 ms, save < 2 seconds, and load to interaction < 3
seconds. Do not loosen a threshold or hide a failed run.

## Decision and reservation gates

- **D-0030 — city grammar:** creator-delegated `HYBRID-CITY` is selected for the
  V0 implementation in VGR-14: restrained individual placement, physical local
  links, visible nearby/aggregate delivery, cohort population plus typed
  specialists, and one evolving city. Broader capacity remains evidence-gated.
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
