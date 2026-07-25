# VGR-31 — Water Shift

## Outcome

Give Last Bearing its first repeatable city-economy choice. The commissioned
service cell can spend scarce fuel and one operator shift on either reclaimed
parts or survival water. This extends the existing machine, clock, staffing,
and pressure rules; it does not create a general job or recipe framework.

## Player contract

At home with the service cell commissioned and a rig plan fitted, the player
may choose one idle work order:

- **Parts Shift:** the existing Hot Shift spends 1 fuel, works for 120
  settlement ticks, draws its existing extra water, and returns 2 reclaimed
  parts.
- **Water Shift:** spends 1 fuel, works for 120 settlement ticks, reserves room
  in Emergency Storage, and returns exactly the existing authored 10.000-water
  cistern amount with no parts.

Both orders retain the fitted route's payable fuel reserve, including any
outstanding caravan toll on a repeat circuit. They use the same assigned human
or utility-robot operator, machine-shop service slot, spindle, and sled. Only
one may run at a time. An active shift preempts Workshop Push; pause and the
existing breached-Dust-Front safety stop freeze progress exactly.

Water Shift is available only when the complete 10.000-water output can be
reserved. While it runs, ordinary positive inflow cannot consume that reserved
headroom. Completion therefore credits the complete output once rather than
silently clamping or spilling the work order.

One-shot water transfers are never sacrificed to that reservation. A queued
water-tender delivery or returned liquid cargo that would cross the reserved
ceiling is rejected atomically and remains at its source until the shift
releases the headroom. Existing full-tank clamp behavior outside Water Shift is
unchanged.

## Authority and saves

- Keep one shared service-shift scheduler and add an explicit active order:
  `None | Parts | Water`.
- Preserve `RunHotShiftCommand` and its current parts behavior.
- Add one exact Water Shift command with expected-completion compare-and-swap.
- A semantic replay never spends fuel or credits water twice; stale
  expectations and invalid capacity fail atomically.
- Schema 11 persists the active order and Water Shift completion count.
- Schema 10 migrates an in-progress legacy Hot Shift to `Parts`; idle legacy
  state migrates to `None`; Water Shift completion count defaults to zero.
- Mid-shift save/load resumes the same order, progress, reserved headroom, and
  result without saving Unity objects or presentation state.

## Presentation

The Field Desk states both complete bargains before commitment. The physical
service cell offers separate Parts Shift and Water Shift controls and derives
its operator, machinery, storage, progress, pause, and safety state from the
canonical read model. Water completion must visibly register at Emergency
Storage. Human-only, robot-only, and mixed colonies follow identical rules.

## Acceptance

- Exact fuel, route reserve, duration, parts, water, and capacity conservation.
- Positive settlement inflow respects the reservation; one-shot aid and cargo
  never disappear into it.
- Mutual exclusion and idempotency for both orders.
- Workshop Push contention, pause, Dust Front stall, and every colony
  composition remain exact.
- Schema-11 round trip and schema-10 migration cover idle, parts-in-progress,
  and Water-Shift-in-progress states.
- Field Desk and physical controls queue only their named order and do not
  mutate canonical state before the authoritative tick.
- A short player-path smoke proves city-to-garage-to-city transitions leave the
  selected work order exact.

## Deliberate exclusions

No new building, resource, population rule, staffing bonus, generalized recipe
graph, inventory UI, power grid, market, faction rule, depot rule, scene,
camera, package, dependency, production asset, audio system, or everyday
performance soak.
