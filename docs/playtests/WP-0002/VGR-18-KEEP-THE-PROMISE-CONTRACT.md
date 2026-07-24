# VGR-18 — Keep the Promise

## Outcome

Turn the cooperative field-sleeve obligation into physical work. When its
maintenance comes due, the Field Desk routes the player to the pump hall. The
player spends the promised parts only by operating a dedicated service control
beside the repaired turbine.

## Player contract

The Field Desk and legacy HUD expose one route-to-work action:

`OPEN PUMP HALL · KEEP THE PROMISE`

That action selects the pump-hall cutaway, frames the field-sleeve collar, two
service parts, and a physical lever, and focuses that lever. Routing does not
queue a command, spend parts, advance time, mutate canonical state, or write a
save. The player must release the route input, then operate the lever with `E`,
gamepad South, or a pointer click.

The control is available only while the existing field-sleeve maintenance is
due, Sasha is home, the expedition transaction is finalized, settlement time is
not paused, no command is pending, and the derived pump-hall control exists.
Held input, wrong mode, stale read-model identity, duplicate operation, unrelated
pending work, title, new game, and load transitions queue no service. Each route
requires a fresh release.

## Authority and conservation contract

- Field Desk and HUD route to the pump hall; neither directly submits service.
- The physical interactor owns only focus, fresh-input arming, pointer targeting,
  and derived presentation.
- The controller remains the sole Unity command adapter and delegates the exact
  existing `ServiceFieldSleeveCommand`.
- Acceptance spends exactly two `PartsUnits`, clears `MaintenanceDue`, and sets
  `NextMaintenanceDueSettlementTick` to the accepted pre-advance settlement tick
  plus the existing 600-tick interval.
- `MaintenanceObligationActive` and
  `MaintenanceRecipe.FieldSleeveService` remain active. The promise is serviced,
  not erased.
- Before the accepting city tick, command queuing changes no canonical bytes,
  tick, balance, or save generation.

## Presentation and save contract

The pump hall owns exactly one derived service target. Due maintenance shows the
field-sleeve witness, lever, two physical parts, focus rail, and explicit text.
After acceptance the parts disappear, the control becomes noninteractive, and
the repaired field-sleeve witness remains with the next service tick legible.
Color is supporting feedback rather than the only state signal.

The existing `MaintenanceServiced` event triggers the fixed-profile autosave.
Save/load preserves the exact parts balance, cleared due flag, next due tick,
and continuing obligation. Focus, input arming, hover, and lever pose are not
saved.

## Acceptance and exclusions

Focused tests cover route-only Field Desk intent, four city-to-pump-hall cycles,
held-input rejection, fresh keyboard and gamepad activation, pointer targeting,
exact command delegation, duplicate suppression, pre-tick immutability,
wrong-mode, stale, pending, and title rejection, exact two-part conservation,
the 600-tick renewal, persistent obligation, physical before/after witnesses,
autosave, and exact reload.

This increment adds no SimulationCore, SaveContracts, schema, migration, balance,
scene, package, dependency, generalized interaction framework, new maintenance
recipe, new faction rule, production asset, audio, or normal-PR performance
soak.
