# VGR-34 — Earn the Miles

Status: product-first V0 gameplay increment.

## Player outcome

Sasha earns route miles by moving the physical Scout over credible road, not by
holding the throttle while the rig is stationary, airborne, reversing, or on
the wrong branch. The settlement apron gives either fitted module room to get
moving. Beyond it, the winch rig must bite into the Washboard shortcut, while
the range-tank rig must cross Sand or Gravel.

The road still feels continuous: physical controls reach the rig every frame,
but only route-correct forward traction admits the existing deterministic drive
command that advances expedition progress.

## Traction admission

One presentation-only quantizer evaluates the latest bounded road evidence.
Canonical progress is eligible only when all of these are true:

- the road adapter is active and healthy;
- the rig is not in recovery;
- at least two wheel contacts are grounded;
- finite forward speed is at least `0.75 m/s`; and
- the dominant contacted surface fits the selected route.

Accepted surfaces are exact:

| Fitted route | Apron | Route surface |
| --- | --- | --- |
| Winch / Collapsed Short Branch | `Concrete` | `Washboard` |
| Range Tank / Exposed Long Route | `Concrete` | `Sand` or `Gravel` |

`Hardpack`, an unknown route, and every cross-route surface fail closed.
Airborne means zero grounded contacts; one grounded contact is still
insufficient. Zero or reverse forward speed is not traction evidence.

## Authority and saving

Raw Rigidbody pose, speed, contacts, and surfaces remain presentation data.
They do not choose the route, author distance, set damage, advance time, change
cargo, or enter a save.

The quantized verdict has one narrow authority: it admits or rejects the
already bounded `DriveVehicleCommand` created from current player input. Once
admitted, the deterministic kernel remains the sole author of the fixed
progress, steering, condition, event, and sequence transition. This contract
supersedes VGR-17 only where that contract prohibited telemetry from gating
command admission; every other VGR-17 authority boundary remains intact.

One legacy interaction remains explicit: at the armed first-run frame-rail
choice, `W / RT` means **leave the steel**, not ordinary road throttle. That
choice retains its existing `DriveVehicleCommand` transition so the player can
still decline the cargo while the physical rig is held. It does not establish
a general traction bypass. A dedicated non-drive choice command is deferred
until that interaction is revised in SimulationCore.

A rejected drive input preserves:

- canonical bytes and canonical hash;
- next command sequence and route progress;
- the complete pending-command list; and
- every protected save-generation byte.

No field, schema, migration, command, event, balance value, or save payload is
added. Save/load therefore restores canonical route state exactly and derives
fresh traction evidence only from the live road presentation.

## Presentation and failure behavior

- Physical throttle, brake, steering, and handbrake continue to reach the
  Scout even before canonical admission, allowing the rig to establish real
  traction.
- Outbound and returning travel obey the same evidence rule.
- Pause, ordinary module-point holds, recovery holds, depot gates, terminal
  settlement loss, and existing command eligibility remain authoritative and
  fail closed before a drive command is queued. The armed first-run
  frame-rail leave choice is the single existing interaction exception
  described above.
- Missing, inactive, faulted, recovering, or unsupported evidence sources
  expose a stable bounded reason and admit no canonical drive command.
- A later valid reading can advance normally; rejection creates no hidden
  latch or debt.
- The Road Desk may explain the bounded reason, but text and styling remain
  derived from the verdict and never become a second source of truth.

## Acceptance

- Winch plus `Concrete` and Winch plus `Washboard` each advance an outbound
  Collapsed Short Branch.
- Range Tank plus `Concrete`, `Sand`, and `Gravel` each advance an outbound
  Exposed Long Route.
- Valid traction advances both outbound and returning travel through the
  existing deterministic command.
- Wrong surface, zero contacts, one contact, stationary speed, reverse speed,
  recovery, inactive adapter, and faulted adapter each queue no drive command
  and preserve canonical bytes, sequence, progress, pending commands, and
  protected save bytes.
- A valid reading immediately after each rejected class can be admitted
  without restart or canonical repair.
- The armed first-run frame-rail `W / RT` leave choice remains available while
  every other held-road input fails closed.
- Four city-to-garage cycles remain presentation-only with stable canonical
  bytes and one active mode.
- Focused EditMode tests cover the pure evidence matrix. Focused PlayMode tests
  cover the controller admission boundary, both routes, both travel
  directions, protected saves, and the four mode cycles.

## Validation

Use the gameplay-PR tier: compile, focused EditMode and PlayMode tests, a short
native ARM64 smoke, three to five city-to-garage cycles, and direct verification
of both fitted routes. The extended performance phases and 100-cycle soak remain
nightly or milestone gates.

## Rollback

Reverting this increment restores input-only canonical driving. Because no
canonical field, save schema, or migration changes, schema-11 saves remain
readable before and after rollback.

## Exclusions

No new route, road segment, resource, cargo, hazard, repair, building,
manufacturing rule, faction rule, physics tuning, vehicle model, scene, camera,
input map, package, dependency, production asset, audio system, generalized
telemetry bus, generalized command policy, or normal-PR performance soak.
