# VGR-15 — Emergency Cistern

## Outcome

Make emergency storage matter before Sasha leaves. Once the working service cell
and rig plan exist, the player may spend one fuel to put one full 10.000-water
reserve into the colony. At the base Dust Front brink, that reserve changes the
verdict from `Breached` to `Held`.

## Player contract

The Field Desk and legacy fallback expose:

`PUMP EMERGENCY CISTERN · 1 FUEL · +10.000 WATER · ONE FILL`

The action is available only when:

- the service cell is commissioned, emergency storage is placed, and its typed
  operator is available;
- Sasha is home and a rig plan is committed;
- the Dust Front is unresolved and Hot Shift is inactive;
- the complete 10.000-water fill fits without spill;
- one fuel can be spent while retaining the planned route fuel reserve; and
- the cistern has not already received its one authorized fill.

The command is atomic. It debits exactly one fuel, adds exactly 10,000
`WaterMilli`, emits `EmergencyCisternPumped`, and sets
`EmergencyCisternCharged`. Invalid attempts change nothing. A semantic replay
adds no fuel cost or water.

Human-only, robot-only, and mixed colonies use identical rules. No composition
bonus or penalty is introduced.

## Save and presentation contract

- Schema 9 persists `EmergencyCisternCharged`.
- Schema 8 migrates deterministically to `false`; prior Dust Front verdict and
  acknowledgement state remain exact.
- The charged flag participates in the canonical mechanical projection.
- Completion autosaves through the existing fixed profile boundary.
- Emergency storage derives one visible full marker from canonical state. It
  adds no scene, asset, package, physics, or second source of truth.

## Acceptance and exclusions

Focused tests cover exact conservation, replay, every prerequisite,
composition neutrality, the base `Breached` to `Held` comparison, schema-9
round trip, and schema-8 migration. Relevant Field Desk and service-cell tests
cover command delegation and the derived fill marker.

This increment does not add partial filling, draining, repeated pumping, new
storage capacity, new survival resources, population effects, production art,
audio, a generalized job system, or a normal-PR performance soak.
