# VGR-19 — Read the Dry Line

## Outcome

Let the player read the Dust Front before it arrives. The Field Desk projects
whether the current water draw crosses Last Bearing's existing recoverable
reserve, while Emergency Storage carries the same pressure as a physical gauge
readable from the strategy camera.

## Player contract

While the Dust Front is unresolved, the Field Desk shows:

`FRONT IN N TICKS · DRY LINE 60.000 · PROJECTED HELD/BREACHED IF CURRENT DRAW CONTINUES`

The projection updates on the accepted city tick when water, current draw, or
front distance changes. Pumping the existing emergency cistern raises the
projected reserve immediately. A working Hot Shift adds its existing draw
immediately. Pausing freezes the inputs and therefore freezes the projection.

This is explicitly a constant-current-draw forecast, not a new simulation
promise. Later player actions can change it.

## Projection contract

The presenter derives:

`projected reserve = clamp(current water + current trend × front ticks, 0, capacity)`

The existing Dust Front rule remains exact: a non-failing turbine projects
Held; otherwise projected reserve must be strictly greater than `60.000` to
project Held. Exactly `60.000` projects Breached.

The calculation uses only the current read model and existing V0 balance
constants. Saturating bounds prevent arithmetic overflow. It submits no
command, advances no clock, mutates no canonical byte, and writes no save.

Once the Dust Front resolves, the forecast disappears. The Field Desk reuses
the existing durable Held or Breached witness instead of recalculating history.

## Physical presentation contract

Placed Emergency Storage owns one derived, noninteractive gauge:

- its water column follows current authoritative water against existing
  capacity;
- its dry-line marker remains fixed at `60.000 / 180.000`;
- its approaching-front telltale and short text remain legible from strategy
  zoom while the outcome is unresolved;
- resolution removes the approaching telltale and leaves only `FRONT HELD` or
  `FRONT BREACHED`, alongside the existing durable relay witness.

The gauge uses generated runtime primitives with no collider. It owns no
interaction, rule, state, save field, or production asset. Label and silhouette
carry meaning; color is supporting feedback only.

## Stability and acceptance

Focused tests cover:

- the starting `120.000` reserve at `-0.010` for `6000` ticks projecting
  exactly `60.000` and therefore Breached;
- the existing `+10.000` cistern fill changing the projection to Held;
- a working Hot Shift changing current draw and the projection on acceptance;
- fixed marker position and authoritative water-column movement;
- pause and exact save/load stability;
- four city-to-garage-to-city cycles;
- resolved Held and Breached witnesses replacing the forecast;
- presenter, retained UI binding, and physical gauge calls preserving canonical
  bytes and hash.

This increment adds no command, SimulationCore behavior, SaveContracts field,
schema, migration, balance value, scene, package, dependency, production asset,
audio, generalized forecast framework, or normal-PR performance soak.
