# VGR-20 — Expand Emergency Cistern

## Outcome

Turn the returned sealed range tank into durable settlement capacity. After a
Workshop Push water run and turbine repair, the Field Desk routes Sasha to one
physical expansion handwheel beside Emergency Storage. The accepted installation
adds a clear pair of saddle tanks and raises the authoritative water ceiling from
`180.000` to `210.000`.

## Player contract

When `NextCityDecision.ExpandEmergencyCistern` is exactly installable, the Field
Desk exposes:

`OPEN EMERGENCY STORAGE · EXPAND CISTERN`

This action only focuses the physical, non-HUD
`INTERACT_EMERGENCY_CISTERN_EXPANSION` control in `CityOverview`. Routing changes
no command sequence, resource, clock, save, canonical byte, or hash. It never
opens the pump hall and never reuses the existing cistern-pump / Dust Front relay.

The focused control requires a fresh release, then accepts E, gamepad South, or
its exact pointer target. Acceptance delegates the existing
`InstallCityImprovementCommand` with:

- `NextCityDecision.ExpandEmergencyCistern`;
- `LastBearingState.EmergencyStorageExpansionSocketId`;
- `LastBearingState.EmergencyStorageExpansionOrientationQuarterTurns`.

Held entry input, stale models, pending or duplicate commands, title, garage,
building cutaway, missing Emergency Storage, the auxiliary-pump branch, and forged
Workshop Push fuel all fail closed.

## Exact eligibility and consequences

Core remains authoritative. The supported branch is Workshop Push plus Sealed
Range Tank, one returned `Water` load in settlement custody, finalized return,
repaired turbine, and the exact post-install parts reserve. Fuel is not a second
Workshop Push choice and is not presented as one.

The accepted command spends exactly `2` parts, preserves the returned liquid and
current water, consumes the pending decision, installs
`CityImprovementKind.ExpandedEmergencyCistern`, and changes effective
`WaterCapacityMilli` from `180000` to `210000`.

## Physical and forecast presentation

Before acceptance, Emergency Storage owns one distinct handwheel/cradle target
placed away from its existing pump/front relay. After acceptance, the target
stows and Emergency Storage gains two derived saddle-tank cylinders, a service
spine, and an oxide witness band. These runtime primitives have no authoritative
state and their visual colliders are disabled.

The VGR-19 water column and fixed `60.000` dry-line marker normalize against the
effective read-model `WaterCapacityMilli`. The Field Desk forecast also clamps
against that effective capacity. Installation therefore updates the gauge and
projection on the accepted city tick while preserving VGR-15 pumping and the
auxiliary-pump route.

## Acceptance

Focused core, source-contract, and PlayMode tests collectively cover:

- route-only Field Desk intent and no pump-hall transition;
- one exact command from fresh keyboard, gamepad, and pointer input;
- held, stale, pending, duplicate, wrong-mode, wrong-branch, and forged-fuel
  rejection;
- the one reachable Water return path;
- presentation-neutral behavior for human, utility-robot, and mixed colonies;
- saddle witness, effective-capacity gauge/marker, and same-tick forecast update;
- title/load durability and four city-to-garage-to-city cycles;
- one shared camera and one `AudioListener`;
- unchanged canonical bytes and hash before the authoritative tick.

This increment adds no new presentation command, save field, schema, migration,
independent numeric tuning value, scene, package, dependency, production asset,
audio, generalized interaction framework, or normal-PR performance soak. Its
named cost and capacity aliases reuse existing authored balance constants.
