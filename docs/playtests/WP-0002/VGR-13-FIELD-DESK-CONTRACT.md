# VGR-13 — Field Desk Contract

Status: creator-directed presentation milestone; release remains conditional on
independent review, exact Unity dispatcher gates, protected checks, and
transparent creator-delegated manual release.

Baseline: protected `main` commit
`2ddbc3f90c14a5fa9bec2858ce9ff06b350a80db`.

This contract is implementation guidance. It is not governance, ratification,
a work-packet amendment, an art-asset acceptance, or authority to resolve an
open decision.

## Proof

Give the normal strategy city view its first retained, player-facing interface:
the **Field Desk**. The player should be able to read Last Bearing's immediate
water pressure, understand the current Permit Job chapter, take the exact
already-implemented city action, and reach the next physical workplace without
searching a developer instrument dump.

The Field Desk uses Unity UI Toolkit already present in the selected Editor. It
is a presentation and audit projection only. It adds no command, eligibility
rule, canonical state, save field, balance, clock, gameplay outcome, package,
scene object, project setting, or production-art dependency.

## Exact surface boundary

For this slice, **city-only** means exactly all of the following are true:

- an active Last Bearing game and read model exist;
- the mode coordinator has an active mode; and
- that mode is `LastBearingPresentationMode.CityOverview`.

Only then may the retained Field Desk replace the legacy IMGUI panel. Title,
`CityReturn`, `BuildingCutaway`, `GarageBay`, driving, depot, and any inactive or
unknown mode continue to use `LastBearingHud` unchanged. The workshop and
claims-wicket actions released in VGR-12 therefore remain on the known legacy
surface inside their fixed cutaway.

The two surfaces never draw together. The legacy HUD is suppressed in city
overview only after the retained surface reports that it initialized
successfully. If UI Toolkit setup, document loading, or binding fails, the
legacy HUD fails open in city overview too; the player must never receive a
blank or actionless screen.

## Field Desk composition

Build one retained element tree once and update its existing labels, classes,
progress values, and enabled states only when their backing projection changes.
At normal city zoom it contains:

1. **Nameplate** — game title, `THE LAST BEARING`, composition, and pause state.
2. **Civic instruments** — water amount and signed trend, parts, fuel, turbine
   condition, and plainly worded pressure/recovery state. State never depends
   on color alone.
3. **Permit Job docket** — the exact output of
   `LastBearingPermitJobPresenter.Present(readModel, CityNeedInspected)`:
   chapter, step, headline, detail, measured progress, completion or alternate
   conclusion, and the existing first-run cue.
4. **Current order** — one truthful primary city action or a concise waiting
   state. Raw `NextObjective` identifiers, object names, and enum dumps are not
   normal player copy.
5. **Survey board** — the existing reversible D-0030 comparison controls and
   raw `clear` / `unclear` evidence while that trial is relevant. It contains no
   score, recommendation, winner, or persisted selection.
6. **Service strip** — pause, save, load, return to title, save status, and a
   collapsible development audit line. The canonical hash may appear only in
   this subordinate audit line, not as the visual headline.

The Field Desk does not reproduce every legacy instrument. Road telemetry,
depot custody controls, workshop stock handling, garage module stands, and
building-cutaway verbs remain with their physical views and legacy fallback.

## Existing-controller delegation

The retained surface may read `LastBearingReadModel`,
`LastBearingPermitJobPresentation`, and the controller's existing public
presentation properties. A click handler re-checks the active city predicate
and its current derived availability, disables the originating control for the
remainder of that dispatch, and calls at most one existing public controller
method.

| Field Desk intent | Existing controller delegation |
|---|---|
| Assign the missing default lead | `AssignDefaultLeadResident()` |
| Inspect the failing water system | `InspectCityNeed()` |
| Stage trial A or B | `SelectCityGrammarHypothesis(RestrainedSnapGrid)` or `SelectCityGrammarHypothesis(DistrictStamp)` |
| Place/move or restamp the active trial | `ManipulateCityGrammarPrimary()` |
| Rotate the active trial | `RotateCityGrammarPrimary()` |
| Switch recycler/workshop in trial A | `ToggleCityGrammarTrialPiece()` |
| Connect the existing trial service link | `ConnectCityGrammarLogistics()` |
| Dispatch/advance the empty sled | `AdvanceCityGrammarDelivery()` |
| Record the raw path observation | `RecordCityGrammarPathRead(true)` or `RecordCityGrammarPathRead(false)` |
| Reset, leave, or clear comparison evidence | `ResetActiveCityGrammarTrial()`, `LeaveCityGrammarComparison()`, or `ResetCityGrammarComparison()` |
| Bring the same bounded service cell online | `ActivateInfrastructure()` |
| Pencil in Workshop Push or Civic Buffer and enter the garage | `BeginGaragePlan(WorkshopPush)` or `BeginGaragePlan(CivicBuffer)` |
| Open the fixed garage for inspection | `OpenGarageBay()` |
| Commit the ready manifest and depart | `CommitExpedition()` |
| Route a ready repair into the pump hall | `OpenPumpHallRepair()` |
| Route a relevant batch or lot into One Good Batch | `OpenOneGoodBatchWorkshop()` |
| Install the already-authorized auxiliary pump | `InstallCityImprovement()` |
| Service the existing field-sleeve obligation | `ServiceFieldSleeve()` |
| Pause/resume, save, load, or leave | `TogglePause()`, `Save()`, `Load()`, or `ReturnToTitle()` |

The Field Desk never calls the kernel, `Queue`, a command constructor, save
adapter, profile store, `LastBearingState`, `World`, or `ModeCoordinator`
directly. It does not start or barter One Good Batch from city overview; those
verbs remain valid only in the selected VGR-12 cutaway through the existing
controller predicates.

## Availability and no-op law

- The current order is derived from existing controller predicates, read-model
  eligibility, Permit Job projection, city-trial presentation state, and exact
  phase/mode. The Field Desk adds no broader eligibility rule.
- A handler re-reads the controller at invocation time. A stale, hidden,
  detached, disabled, wrong-mode, title, post-load, post-new-game, or duplicate
  request invokes no controller method and queues no command.
- One pointer, keyboard-submit, or gamepad-submit event produces at most one
  delegation. Controls cannot remain actionable through the same-frame mode
  change caused by `BeginGaragePlan`, `OpenGarageBay`, `CommitExpedition`,
  `OpenPumpHallRepair`, or `OpenOneGoodBatchWorkshop`.
- Controller and core validation remain final authority. The Field Desk must
  render a rejected or failed-closed controller status honestly and must not
  infer success from a click, animation, focus change, or button disable.
- Presentation-only city inspection, trial, routing, collapse, scroll, focus,
  and audit actions preserve canonical bytes and existing save-file bytes.
  Explicit `Save()` and already-existing accepted canonical commands retain
  their current effects; the Field Desk creates no implicit save.

## Lifecycle and save boundary

- Construct and register retained callbacks once; unregister them on destroy.
  Re-entering city overview, loading, returning to title, or starting a new game
  cannot accumulate documents, callbacks, panels, or duplicate dispatches.
- City overview entry re-derives every label, class, progress value, action, and
  enabled state. Exit hides and deactivates the retained surface before legacy
  fallback becomes interactive.
- Title, new game, successful load, failed load, return to title, active-mode
  changes, and controller reinitialization clear transient focus, press latch,
  scroll, expansion, and stale presentation references. None is serialized.
- The disposable `last-bearing-dev-v1` profile, current/last-good generation,
  root and pointer law, codec bytes, autosave event set, compatibility boundary,
  and migrations remain unchanged.
- Human-only, humanoid-utility-robot-only, and mixed colonies receive the same
  controls and outcomes. Composition changes only truthful roster copy and
  represented residents; D-0039 remains open.

## Visual grammar

The Field Desk should feel like a civic work surface built by the same people
who keep the pumps alive, not a generic sci-fi HUD:

- **Texas iron — scale of work:** compact plate-like panels, strong section
  edges, service labels, gauges, bolts-as-spacing rhythm, and clear physical
  grouping. Do not use cowboy stars, saloon typography, longhorns, or costume
  western decoration.
- **Brutalist opera — scale of civilization:** one strong docket hierarchy,
  deliberate negative space, axial alignment, and a framed current order that
  treats maintenance as civic ritual. Avoid a wall of equally loud boxes.
- **Storybook salvage — humane filter:** warm, specific language; patched-tab
  grouping; small signs of care; readable silhouettes; and restrained
  irregularity achievable with layout and border treatment alone. Do not add a
  custom font, texture, icon pack, joke system, or faux-random distress.
- **Tungsten over neon — light hierarchy:** iron charcoal, warm concrete, bone
  enamel, and tungsten amber carry the normal surface. Rare signal cyan may
  identify the permit/legacy docket; hazard vermilion may mark the failing
  waterworks. Colored emissive is never the only state carrier and never turns
  the desk into cyberpunk wallpaper.

The art-bible palette values remain calibration suggestions, not accepted
production colors. Use UI Toolkit layout, type hierarchy, flat color, borders,
and restrained opacity only: no texture, custom font, shader, blur, bloom,
animation package, or imported production art. Review at 1920x1200 with the
normal city camera and at the creator-machine native target; also verify a
1280x720 minimum without hiding the primary order or service strip.

## Performance and allocation budget

- Build one document/panel and one stable visual tree. No element-tree rebuild,
  style-sheet recreation, callback registration, `Q()` traversal, LINQ,
  reflection, texture creation, or closure creation occurs per frame.
- Refresh at no more than the canonical 10 Hz presentation cadence, and assign
  text, value, class, display, or enabled state only when that value changes.
- In a paused, unchanged city overview after warm-up, the Field Desk contributes
  `0 B/frame` managed allocation at p95 and no retained-object growth across
  five minutes.
- In a five-minute representative unpaused city pass, the whole application
  retains the packet gate of managed-allocation p95 `0 B/frame` and average
  `< 1,024 B/frame`; UI work cannot be excluded from that measurement.
- Opening/closing city overview 100 times leaves one live retained panel, one
  callback set, and no monotonic managed- or native-memory growth attributable
  to the Field Desk.
- The Field Desk adds no camera, `AudioListener`, render texture, scheduled
  background work, network call, or frame-clock gameplay behavior.

## Acceptance

- an active `CityOverview` shows one operational retained Field Desk and no
  legacy IMGUI panel; every other mode and title show the legacy panel and no
  interactive Field Desk;
- forced retained-UI initialization failure leaves the legacy HUD usable in
  city overview, with no blank screen or command loss;
- the docket projection is field-for-field equal to the existing Permit Job
  presenter for every chapter, finale, alternate conclusion, and composition;
- every tabled city intent delegates once to the named existing controller
  method, and no Field Desk code constructs or queues a core command;
- stale, duplicate, lifecycle, and wrong-mode UI requests are no-ops that
  preserve pending commands, canonical hash, and every existing save-file byte;
- the complete D-0030 A/B trial remains reversible and in-memory, records only
  `clear` or `unclear`, and exposes no score, winner, persistence, or canonical
  result;
- city-to-garage, city-to-road, city-to-pump-hall, city-to-workshop, load, and
  title transitions hand off between retained and legacy surfaces exactly once
  without a one-frame double action;
- the golden `Civic Buffer + Winch + Take Bearing` route remains byte-equivalent
  through VGR-12 manufacture, claims-wicket barter, save, title, and load for
  human, robot, and mixed colonies;
- reference captures pass normal-camera readability, grayscale, color-not-
  alone, no-bloom, and 1280x720/1920x1200/native-Mac layout checks;
- the allocation, repeated-entry, one-camera/listener, deterministic, source,
  save, Unity compile, EditMode, PlayMode, technical capture, protected, and
  diff gates pass.

Green gates prove one retained city desk and a safe legacy handoff. They do not
prove final UI architecture, production art, accessibility completion, fun, or
a decision on city grammar, colony differentiation, or exchange law.

## Exclusions

- no `SimulationCore/` or `SaveContracts/` change; no command, state, event,
  codec, migration, balance, clock, resource, custody, recipe, permit, faction,
  or composition-mechanics change;
- no package, dependency, scene, project setting, input asset, production art,
  texture, font, icon, audio, shader, localization system, animation framework,
  generalized UI framework, quest system, or notification system;
- no retained road, depot, return, garage, building-cutaway, workshop, claims,
  or title redesign; no removal of the legacy HUD;
- no city placement authority, grammar recommendation, persisted trial, human/
  robot differentiation, caravan exchange, price, currency, population gate,
  stock market, order book, or D-0030, D-0039, or D-0044 selection.

## Rollback

Remove the Field Desk presenter/view, its bounded UI Toolkit authoring files and
tests, the runtime attachment, and the single legacy-HUD city suppression seam.
`LastBearingHud` then renders every mode as it did at the baseline. Because this
slice adds no canonical or saved data, rollback requires no migration, profile
rewrite, generation cleanup, or gameplay-state repair.
