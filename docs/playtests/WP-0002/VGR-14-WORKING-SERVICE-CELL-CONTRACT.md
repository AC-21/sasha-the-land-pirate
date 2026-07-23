# VGR-14 — Working Service Cell

## Outcome

Turn Last Bearing's reversible city-grammar comparison into the first canonical,
saved city-building interaction. A new player can place three useful buildings,
connect them, staff the workshop with a resident from the chosen colony, move one
visible delivery, and feel the water emergency continue while doing it.

This is the creator-delegated operational selection of `HYBRID-CITY` for the V0
slice:

- individual functional buildings use a restrained hidden snap grid;
- physical links communicate local logistics;
- nearby deliveries may be visible while the simulation remains aggregate;
- cohorts remain the population baseline, with named or typed specialist slots;
- Last Bearing grows as one deeply evolving city.

The selection resolves the V0 implementation question without authorizing a
general city framework, production-scale capacity, or composition bonuses.

## Player contract

The first city task has one district and five authored pads. The player may:

1. preview, rotate by quarter turns, and cancel without changing canonical state;
2. place a recycler for 2 reclaimed parts;
3. place a machine shop for 3 reclaimed parts;
4. place emergency storage for 1 reclaimed part;
5. reposition placed buildings for free before the service link is committed;
6. connect recycler to machine shop for 1 reclaimed part, permanently locking
   the service-cell layout;
7. assign the machine shop's one operator slot to an eligible human or utility
   robot already present in the selected colony roster;
8. advance one calibration sled from `AtRecycler` to `InTransit` and then
   `DeliveredToWorkshop`;
9. receive exactly 2 reclaimed parts once when the delivery completes.

The full commitment is 7 parts before the 2-part delivery recovery. Preview,
cancel, rotation, selection, camera movement, and failed actions never charge.
There is no demolition or refund in this slice.

## Canonical rules

- `PlaceCityBuildingCommand` is the only placement authority.
- `ConnectCityServiceLinkCommand` is the only link and layout-lock authority.
- `AssignCityServiceResidentCommand` is the only staffing authority.
- `AdvanceCityServiceSledCommand` is the only delivery authority.
- Commands are phase-gated, atomic, and reject invalid pads, overlap, duplicate
  buildings, insufficient parts, premature linking, ineligible residents,
  premature delivery, and semantic replay without partial mutation.
- Human-only, robot-only, and mixed colonies can complete the exact same task.
  D-0039 remains unresolved; resident kind has no modifier here.
- Water pressure continues to fall under the existing deterministic clock.
- The service cell uses the existing reclaimed-parts resource. It does not add
  a second economy, inventory, or cargo truth.
- The existing `StepInto` hot path remains allocation-free after warmup.

## Save contract

Canonical save schema v4 persists only:

- each building's placed pad and quarter-turn orientation;
- service-link connected/locked state;
- assigned operator stable ID;
- sled delivery stage and one-time reward completion.

Schema v3 migrates exactly once:

- inactive legacy infrastructure becomes an empty service cell;
- active legacy infrastructure becomes the fixed completed layout on pads 0, 1,
  and 2, orientation 0, linked and locked, with the existing assigned resident
  or first canonical roster resident, and delivery already complete;
- migration never retroactively debits or credits parts.

Preview state, selected pad, camera pose, Unity objects, physics state, and visual
animation are never saved. Existing generation envelope, slot pointer, profile
root, checksums, atomic replacement, and fallback behavior stay unchanged.

## Presentation contract

- Replace the reversible survey controls in the Field Desk with a compact build
  order, five pad choices, rotate/cancel, link, operator, and sled actions.
- Reuse the existing service-cell pads, line, sled, and city camera. Do not add a
  second scene, camera, state store, or interaction framework.
- State the cost and consequence before every canonical action.
- Show insufficient resources and rejected actions without hiding the reason.
- Show the selected human or robot as the operator, but do not imply a bonus.
- Keep the primary task above diagnostics and retain the legacy fail-open path.

## Acceptance

- A fresh human-only, robot-only, and mixed colony can complete the task through
  public controller entry points.
- Costs, layout lock, staffing, delivery, reward, and falling pressure are exact.
- Save, title, load, and continuation reproduce the same canonical city state.
- V3 migration, V4 round trip, corrupted-generation fallback, and semantic replay
  remain deterministic and fail closed.
- Existing garage, driving, depot, return, faction, manufacturing, and trade
  state remains byte-for-byte compatible except for the explicit schema version.
- The changed dependency surface passes core tests, compile, relevant EditMode
  and PlayMode tests, and a 3–5 transition native smoke.

## Deliberate exclusions

No freeform placement, terrain editing, roads beyond the one link, demolition,
refunds, NavMesh carriers, generalized recipes, power grid, housing simulation,
composition bonuses, market, faction AI, procedural districts, production art,
new package, scene, service, or dependency. The 100-cycle soak is a nightly or
release milestone gate, not a VGR-14 gameplay gate.
