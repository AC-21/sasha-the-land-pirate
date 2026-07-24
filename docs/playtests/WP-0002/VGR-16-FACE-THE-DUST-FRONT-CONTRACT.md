# VGR-16 — Face the Dust Front

## Outcome

Make the Dust Front verdict a physical moment in the colony. When the front
resolves, the Field Desk routes the player to a relay beside Emergency Storage.
Only a fresh activation at that relay acknowledges the existing verdict and
resumes settlement clocks.

## Player contract

The Field Desk exposes one route-to-work action:

`OPEN EMERGENCY STORAGE · FACE DUST FRONT`

That action focuses a physical Dust Front relay beside the placed Emergency
Storage. It does not acknowledge the verdict, queue a command, mutate canonical
state, or write a save. After releasing the route input, the player operates the
relay with `E`, gamepad South, or a pointer click. Only that fresh physical
action may queue the existing `AcknowledgeDustFrontCommand`.

The relay is available only in exact city overview when a Dust Front verdict is
waiting for acknowledgement, Emergency Storage and its derived physical control
are present, and no command is pending. Held input, stale focus, wrong mode,
missing control, duplicate activation, and lifecycle transitions queue nothing.
Re-routing always requires another release before activation.

Acknowledging `Held` turns the relay to a full tungsten signal and resumes the
existing settlement clocks. Acknowledging `Breached` turns it stop-red and
closes the physical safety shutter; Hot Shift remains stalled by the existing
Dust Front consequence until the existing turbine repair clears it. The relay
does not change the verdict, resources, population, repair rules, or balance.

## Fallback and authority contract

- The Field Desk and exact city-overview legacy action route to the relay; they
  never submit `AcknowledgeDustFrontCommand` directly.
- If the player is outside city overview or the derived relay is genuinely
  unavailable because its control object was not materialized, the existing
  acknowledgement fallback remains usable so the global pause cannot deadlock
  the run. A merely stale presentation is not treated as missing.
- The controller remains the only Unity command adapter. It validates the
  current read-model identity and exact acknowledgement requirement before
  queuing one existing command.
- The interactor owns only focus, release arming, pointer targeting, and derived
  relay presentation. It does not construct commands, touch simulation state,
  or write saves.
- The existing `DustFrontAcknowledged` event continues to trigger the existing
  fixed-profile autosave. No saved transient focus or input state is introduced.

## Presentation contract

Emergency Storage owns the derived relay and places it on a collision-safe side
of the building. The pending-verdict relay, focused pose, queued pose, held tungsten
signal, breached stop-red signal, and safety shutter are presentation derived
from the current read model and local focus state. Color never carries the
verdict alone: label, silhouette, and shutter position remain legible from the
strategy camera. After acknowledgement, the relay becomes noninteractive but
keeps the accepted Held or Breached witness through save/load. A repaired
Breached run keeps the historical stop-red witness but changes its label to
`REPAIR HOLDS` as the physical shutter opens.

The relay takes ownership of `E` and gamepad South while focused so camera or
other city verbs cannot win the same frame. Pointer activation respects the
Field Desk blocking boundary.

## Acceptance and exclusions

Focused tests cover routing without mutation, fresh-input arming, exact command
delegation, pre-tick immutability, wrong-mode and stale-state rejection,
duplicate suppression, pointer targeting, collision-safe placement, four
city-to-garage transitions, Held acknowledgement and resumed clocks, Breached
acknowledgement with the existing Hot Shift stall, outside-city and genuinely
missing-control fallback, durable verdict signals, autosave, and exact reload.
Headless source contracts require the route-only Field Desk seam, controller
authority, physical relay ownership, retained fallback, and materialized test
fixture.

This increment adds no SimulationCore or SaveContracts behavior, schema,
migration, balance change, scene, package, dependency, production asset,
generalized interaction framework, new crisis, new repair path, audio, or
normal-PR performance soak.
