# VGR-33 — The Dry Bell

Status: product-first V0 gameplay increment.

## Player outcome

Last Bearing can now be lost. If its authoritative water reaches exactly zero
while the turbine is still failing, the current view holds, the city rings the
Dry Bell, and ordinary work stops. The player sees what happened and can load
the existing pre-failure checkpoint, return to title, or start a new colony.

This turns the already implemented construction, staffing, Parts Shift, Water
Shift, cistern, expedition clock, and turbine repair into one survival system.
It does not add a second crisis or a new economy.

## Terminal authority

Settlement loss is the pure canonical projection:

`WaterMilli == 0 && TurbineCondition == TurbineCondition.Failing`

- `WaterMilli == 1` is alive.
- Zero water with a repaired bearing or sleeve is not terminal.
- The projection is evaluated after a complete accepted tick. If an already
  active Water Shift completes above zero on that same tick, the settlement
  survives.
- Once the projection is true, authoritative steps and gameplay commands
  preserve the exact state and command sequence. No road, faction, crisis,
  workshop, or settlement clock advances.
- Failure while Sasha is away preserves exact vehicle, cargo, transaction,
  faction, workshop, lot, promise, and custody state.

The simulation read model exposes the projection and one stable failure reason.
Unity may present it but may not manufacture, clear, or persist it.

## Recovery and saving

The Dry Bell is not a new saved phase.

- Schema 11 remains current and no field or migration is added.
- Encoding and decoding zero-water/failing-turbine state re-derives the same
  terminal projection byte-exactly.
- The transition emits no new event and creates no failure autosave.
- Manual save is unavailable after failure, so a failed state cannot overwrite
  the latest recoverable checkpoint.
- Existing checkpoint load, title, and new-colony paths remain available.
- A successfully loaded active state clears the presentation naturally because
  it no longer satisfies the projection.

## Presentation

- The current camera and mode remain visible; no new scene or game-over mode is
  introduced.
- In city overview, the Field Desk replaces normal work orders with the Dry
  Bell recap and recovery actions.
- In garage, road, depot, return, cutaway, or retained-UI fallback states, the
  legacy HUD shows the same causal recap and recovery actions.
- The message names both facts: the settlement has no water and the failing
  turbine cannot recover it.
- Input, pointer focus, world interactions, mode routes, and queued gameplay
  work fail closed while terminal.
- The presentation uses text, hierarchy, and stopped motion; color alone never
  carries the failure.

## Acceptance

- One water unit remains playable; zero plus a failing turbine is terminal;
  zero plus a repaired turbine is not.
- A Water Shift that completes above zero on the dry tick prevents failure.
- Existing emergency water, returned water, or accepted turbine repair before
  the terminal state prevents failure without a special exception.
- Empty ticks and every rejected gameplay command after failure preserve
  clocks, sequence, resources, custody, and canonical bytes.
- Human-only, utility-robot-only, and mixed colonies use the same rule.
- Failure at home and on the road preserves every unrelated subsystem.
- Manual save cannot write after failure; existing checkpoint load, title, and
  new colony remain usable.
- Schema-11 active and failed states round-trip byte-exactly and rederive the
  truthful projection.
- Field Desk and legacy HUD expose no ordinary action through the terminal
  overlay.
- Focused deterministic, EditMode, and PlayMode tests cover the changed path,
  including three to five city-to-garage cycles before failure.

## Rollback

Reverting this increment removes the terminal projection and presentation.
Because no authoritative field, schema, migration, command, event, balance
value, or content identifier is added, existing schema-11 saves remain
readable before and after rollback.

## Exclusions

No saved failure flag, schema, migration, command, event, resource, recipe,
building, balance value, population mechanic, composition differentiation,
random death, permanent resident loss, second crisis, new mode, scene, camera,
package, dependency, production asset, audio system, generalized game-over
framework, or everyday performance soak.
