# VGR-23 — Service the Scout

## Outcome

Make Sasha's road wear a physical homecoming consequence. After every
higher-priority return obligation is settled, the Field Desk routes the player
to the existing garage bay. A fresh pull on one garage-local service pendant
spends the authored two-part service cost, preserves the authored two-part civic
reserve, and restores the scout from its actual worn condition to 1000 / 1000.

## Player contract

When the authoritative read model exposes `IsVehicleServiceAvailable`, the
Field Desk shows:

`OPEN GARAGE · SERVICE SASHA'S SCOUT`

This button is route-only. It opens the existing `GarageBay` mode, uses the
existing garage camera and listener, frames the dedicated
`INTERACT_SERVICE_SASHA_SCOUT` pendant, and changes no part, condition, command
sequence, save, canonical byte, or hash.

The pendant requires an input release after routing. It then accepts:

- keyboard E;
- gamepad South;
- the exact pendant pointer target.

Acceptance delegates exactly one `ServiceScoutCommand` through the controller.
The interactor owns no eligibility, cost, reserve, state transition, event,
save seam, or canonical authority.

## Return-work order

Scout service is the final return obligation. It remains unavailable while any
of these higher-priority facts are active:

- unfinalized return or an uninstalled turbine repair;
- queued cooperative emergency aid, or delivered-looking aid without the exact
  cooperative completion lineage;
- an unresolved city improvement;
- an unposted fuel bond;
- One Good Batch in progress, complete, or awaiting barter;
- due field-sleeve maintenance;
- an active Hot Shift;
- an unacknowledged Dust Front.

After those facts settle, a worn returned scout exposes
`service-scout-in-garage`. Completing service falls back to the already-derived
terminal objective for that branch. Service never erases or substitutes for a
return obligation.

## Exact physical read

Before acceptance:

- the receipt shows actual `VehicleConditionMilli` against 1000;
- the receipt shows the read-model service cost and the separately preserved
  civic reserve;
- the existing service hoist remains at rest;
- the existing scout condition telltale reads worn;
- the existing garage module worklight becomes the warm service-ready cue.

After acceptance:

- the receipt reads 1000 / 1000;
- the receipt separately records parts spent and parts held in reserve;
- the existing service hoist and cable move to the completed-work position;
- the existing scout condition telltale reads healthy;
- the existing module worklight becomes the brighter accepted-service cue.

The witness is composition-neutral. Existing module, Patchwork Skid Plate,
cargo history, faction memory, city layout, improvement, permit, and future
toll remain exactly as authored.

## Authority, events, save, and autosave

Core owns readiness, including VGR-22's exact delivered-aid completion witness,
the exact two-part cost, the exact two-part minimum post-return reserve, command
sequence, idempotency, and atomic rejection.
Accepted service changes only settlement parts and vehicle condition. It emits,
in this order and under one player-command sequence:

1. `CityResourcesCommitted` for `settlement:last-bearing:parts`;
2. `VehicleConditionChanged` for `vehicle:sasha:service-cell`, ending at 1000.

Presentation autosaves only when that exact paired witness is present. Ordinary
road-edge condition events do not trigger this seam. A ready save reloads into
the existing garage route without accepting service. An accepted save reloads
with 1000 / 1000, the completed hoist/worklight/telltale witness, and all
unrelated return history intact.

This increment adds no saved field, schema, migration, event kind, balance
value, scene, mode, camera, package, dependency, production asset, audio, or
general interaction framework.

## Rejection contract

Held entry input, title, city, wrong mode, stale read-model identity, pending
work, duplicate requests, insufficient parts, an already healthy scout, forged
delivered-aid lineage, and every unresolved higher-priority return obligation
fail closed. Rejection leaves canonical bytes, hash, parts, condition, module,
upgrade, cargo, faction, city, clocks, and save truth unchanged.

## Acceptance evidence

Focused core, source, EditMode, and PlayMode coverage proves:

- the Field Desk delegates route intent and never constructs the core command;
- the controller alone constructs `ServiceScoutCommand`;
- keyboard, gamepad South, and exact pointer input queue one command only after
  release;
- stale, pending, duplicate, wrong-mode, insufficient, healthy, and
  higher-priority states queue none;
- invariant-valid delivered-looking aid states without VGR-22's exact
  completion lineage neither expose the service objective nor accept service;
- the ready receipt shows actual worn condition, exact cost, and exact reserve;
- accepted service shows 1000 / 1000 through the existing hoist, worklight, and
  scout condition telltale;
- the paired service events trigger the critical autosave without broadening
  autosave to ordinary condition damage;
- all colony compositions share the same mechanics;
- existing module, upgrade, cargo, faction, city, and permit state remain;
- ready and accepted saves restore truthfully;
- four CityOverview-to-GarageBay route cycles preserve canonical bytes;
- one shared camera and one `AudioListener` remain.

The gameplay-PR gate is compile, focused deterministic/source/EditMode/PlayMode
coverage, a short native player-path smoke, and three to five repeated
city-to-garage routes. Extended performance phases and the 100-cycle soak remain
milestone/nightly gates.
