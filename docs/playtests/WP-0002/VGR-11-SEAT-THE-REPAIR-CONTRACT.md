# VGR-11 — Seat the Repair Contract

Status: creator-directed playable milestone; release remains conditional on
independent review, exact Unity dispatcher gates, protected checks, and
transparent creator-delegated manual release.

Baseline: protected `main` commit
`b87a3530c733d959a5b57849105784a46494e314`.

## Proof

Close the physical homecoming seam without adding another simulation system.
Sasha returns with the exact depot repair cargo still visible on the scout,
checks the existing road-owned outcome into Last Bearing at one fixed service
apron, then enters the existing pump-hall cutaway and seats the repair in the
failing civic organ.

This slice reuses the existing canonical return transaction,
`CreditCityReturnCommand`, `FinalizeExpeditionTransactionCommand`,
`InstallTurbineRepairCommand`, repair-cargo kind and custody, scout cargo socket,
critical-transition autosaves, pump-hall cutaway, and disposable
`last-bearing-dev-v1` profile. It adds no canonical command, state, event,
resource, balance, save field, placement rule, logistics rule, repair minigame,
or generalized interaction framework.

## Player sequence

1. Freeze the loaded depot payload and drive the authored corridor home with
   the repair cargo visible on Sasha's scout.
2. Arrival selects the existing `CityReturn` mode. The sole shared camera frames
   the canonical scout at its fixed home service apron, the exact cargo on
   `SOCKET_CARGO_01`, a tungsten check-in marker, the pump-hall approach, and the
   route back out.
3. Use the contextual check-in action (`E`, gamepad south, or its semantic HUD
   button). It queues the existing city-credit and transaction-finalize command
   pair unchanged, with the existing transaction identity and fingerprint.
4. Only the accepted pair credits heavy and liquid return goods, vehicle wear,
   faction consequences, and the next city decision exactly as before. It does
   not transfer, consume, stage, or hide the repair cargo; that cargo remains in
   `Vehicle` custody on the scout.
5. The presentation opens, or directs the player into, the existing fixed
   pump-hall dollhouse. The shared camera keeps the scout, its cargo, the failing
   turbine, service access, the exact resident workers, and the exit route
   readable together.
6. Use the contextual install action from that pump-hall view. It queues exactly
   the existing `InstallTurbineRepairCommand`.
7. Only the accepted command transfers the ceramic bearing from `Vehicle` to
   `Turbine`, or transfers the field sleeve from `Vehicle` to `Consumed`, and
   activates the existing repaired-turbine water, motion, resident-work, and
   tungsten-light consequence.

## Return check-in law

- Check-in is presentation-available only while the canonical state is
  `ExpeditionPhase.Returned` and `TransactionPhase.ReturnPending`, with the
  frozen repair cargo in `Vehicle` custody, and while `CityReturn` is the active
  presentation mode.
- An accepted check-in invokes the existing composite controller seam: one
  `CreditCityReturnCommand` followed by one
  `FinalizeExpeditionTransactionCommand`. Their order, transaction identity,
  command sequencing, calculations, events, and canonical bytes are unchanged.
- Check-in advances the transaction through `CityCredited` to `Finalized` and
  returns the expedition to `AtHome`. It does not change repair-cargo kind,
  custody, ordinary-cargo occupancy, turbine condition, or depot history.
- Early, outbound, returning, depot, wrong-mode, already-finalized, repaired,
  stale, or duplicate presentation requests queue no command. Core replay law
  remains the final defense if a previously accepted command is retried.
- `E`/gamepad south retains the existing Wreck Line, depot recovery, and depot
  cargo-load priorities. Check-in is reachable only in the disjoint
  `CityReturn` state and is a no-op elsewhere.

## Pump-hall install law

- Install is presentation-available only while Sasha is `AtHome`, the return
  transaction is `Finalized`, the turbine is still failing, the exact repair
  cargo is in `Vehicle` custody, and the pump-hall cutaway is the selected active
  building view.
- The action queues the existing `InstallTurbineRepairCommand` unchanged. The
  view cannot issue commands, mutate state, advance clocks, save, inspect the
  filesystem, or infer an outcome from animation, trigger, raycast, collision,
  or Rigidbody state.
- Before acceptance, the one solid repair-cargo representation remains on the
  scout's canonical presentation socket. An empty keyed target or service line
  may explain the destination, but no second solid cargo proxy may imply that
  custody has already changed.
- After acceptance, a ceramic bearing is shown at the turbine and no longer on
  the scout. A field sleeve is consumed by the repair and no longer shown as
  vehicle cargo. Text and silhouette distinguish the two outcomes; color or
  emissive light is never the only carrier.
- The fixed apron, existing pump-hall cutaway, and repair target are bounded
  presentation sockets. They do not select a D-0030 placement or logistics
  grammar and never enter canonical state or saves.

## Save and lifecycle boundary

- Existing `VehicleReturned` autosave preserves the arrival checkpoint before
  check-in, including exact transaction, cargo, colony, clocks, vehicle, crisis,
  and faction state.
- Existing `CityReturnCredited` autosave runs only after the unchanged composite
  command step completes; the saved state must contain the same tick's
  `Finalized` transaction and `AtHome` expedition state while repair cargo
  remains in `Vehicle` custody.
- Existing `TurbineRepaired` autosave preserves the accepted installed or
  consumed custody and the exact repaired city consequence.
- Loading an arrival checkpoint re-derives `CityReturn` and check-in
  availability. Loading a finalized-before-repair checkpoint re-derives the
  pump-hall route and install availability. Loading a repaired checkpoint shows
  no stale check-in, cargo, target, or install action.
- Title, new game, failed load, successful load, and repeated mode changes derive
  every marker and action from the read model. No transient check-in, repair
  intent, camera pose, or interaction state is persisted or inferred.
- The canonical codec layout, profile version, compatibility boundary,
  migration set, current/last-good generation law, and save root remain
  unchanged.

## Composition parity

- Human-only, humanoid-utility-robot-only, and mixed colonies use the same
  check-in and install commands, phases, costs, timing, custody transitions,
  autosaves, and completion oracles.
- The pump hall shows only the exact resident kinds present in the canonical
  roster. No hidden human, robot substitute, staffing advantage, maintenance
  rule, mixed synergy, or composition-specific repair capability is added.
- Resident motion and work poses are derived presentation feedback only. They
  cannot authorize or accelerate the repair.

## Authority boundary

- Add only the minimum derived presentation availability, fixed return-apron
  staging, pump-hall framing/repair feedback, controller/HUD routing, tests, and
  playtest documentation required to prove this flow.
- Preserve exact transaction math, cargo quantities and capacity, repair
  outcomes, depot and faction consequences, vehicle condition, liquid/heavy
  cargo rules, next-city decision, auxiliary-pump improvement, manufacturing,
  barter, route permit, clocks, balance, stable IDs, and composition viability.
- Do not add or alter a core command, canonical field/event, read-write state,
  codec, save version, migration, free-form placement, carrier, inventory,
  pickup/drop, cargo slot, repair recipe, minigame, avatar, walking mode,
  animation-authored outcome, generalized interaction system, dependency,
  scene, package, project setting, production asset, or audio asset.
- D-0030 city grammar, D-0039 composition differentiation, D-0044
  physical-goods exchange law, D-0045, and D-0046 remain open and unchanged.

## Acceptance

- every valid return arrives in `CityReturn` with the exact canonical scout and
  vehicle-owned ceramic bearing or field sleeve visibly present at the fixed
  home apron;
- check-in is available only in the exact returned/return-pending CityReturn
  state and queues the existing credit/finalize pair once, in order, with the
  existing transaction identity;
- the accepted pair is byte-equivalent to the pre-slice composite action,
  finishes in `AtHome`/`Finalized`, and leaves repair kind, `Vehicle` custody,
  ordinary-cargo occupancy, and turbine condition unchanged;
- early, stale, wrong-mode, invalid-state, repaired, duplicate, title, new-game,
  and load lifecycle requests queue no command and preserve canonical/save
  bytes;
- the pump-hall action is available only in the selected pump-hall cutaway with
  the exact finalized, failing-turbine, vehicle-cargo state and queues exactly
  one existing install command;
- ceramic repair transfers `Vehicle` to `Turbine`; field-sleeve repair transfers
  `Vehicle` to `Consumed`; neither outcome duplicates, hides early, or leaves a
  stale scout marker;
- the sole shared camera keeps service access, scout, one cargo truth, turbine,
  exact residents, and exit route readable, with no second camera/listener,
  avatar, physics authority, trigger, raycast, or solid duplicate cargo proxy;
- arrival, finalized-before-repair, and repaired checkpoints round-trip
  byte-exactly and restore the same derived mode, markers, and action
  availability without a new save field or migration;
- human-only, robot-only, and mixed colonies retain identical mechanics and show
  only their exact canonical resident sets;
- existing scenario and deterministic/source tests, save gates, foundation
  lint, Unity compile, EditMode, PlayMode, technical capture, protected checks,
  and diff checks pass.

Green gates prove a truthful, contained homecoming and repair handoff, not final
emotion or art. Creator play must still judge whether checking in Sasha's loaded
rig feels deliberate, whether the pump-hall repair reads without explanation,
and whether the visible civic recovery makes the road journey feel worthwhile.
