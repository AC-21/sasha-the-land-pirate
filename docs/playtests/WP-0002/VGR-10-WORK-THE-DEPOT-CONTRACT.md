# VGR-10 — Work the Depot Contract

Status: creator-directed playable milestone; release remains conditional on
independent review, exact Unity dispatcher gates, protected checks, and
transparent creator-delegated manual release.

Baseline: protected `main` commit
`cf135abe0d1acae414d18ec08d8b63f593d1aa37`.

## Proof

Make the depot outcome a physical custody handoff rather than an invisible
teleport. Resolving the encounter creates the already-authored repair cargo at
its truthful source. The player must then load that cargo onto Sasha's scout
before the return payload can be frozen.

This slice uses the existing repair-cargo kind and custody fields, source
custodies, vehicle cargo sockets, transaction, route, faction consequences,
event kind, codec layout, and disposable `last-bearing-dev-v1` profile. It adds
one narrow `LoadDepotRepairCargoCommand`; it does not add a general inventory,
trade, hauling, interaction, or vehicle-upgrade system.

## Player sequence

1. Reach the existing depot recovery point and resolve the encounter.
2. Cooperation stages the `FieldSleeve` in `Faction` custody at the claims
   counter. Taking the bearing stages the `CeramicBearing` at its truthful
   pre-resolution source: `Depot` custody while it remains unclaimed, or
   `Faction` custody once the faction claim is established. Trust, grievance,
   memory, access, aid, and maintenance consequences commit at resolution.
   `TakenBySasha` disposition and depleted depot control wait for the accepted
   physical load.
3. A physical source marker and Sasha's scout cargo socket show the pending
   handoff in the sole shared depot camera.
4. Use the contextual load action (`E`, gamepad south, or its semantic HUD
   button) to queue exactly one `LoadDepotRepairCargoCommand`.
5. Only the accepted command transfers the exact repair cargo from its source
   custody to `Vehicle` custody. The physical marker moves to the scout and the
   transition autosaves.
6. Select existing tank cargo when required, freeze the payload, drive home,
   credit/finalize the return, and install the existing turbine repair exactly
   as before.

## Canonical custody law

- `ResolveDepotCommand(Cooperate)` creates `FieldSleeve/Faction`; it no longer
  creates repair cargo directly in the vehicle.
- `ResolveDepotCommand(TakeBearing)` creates `CeramicBearing/Depot` when the
  bearing disposition was `AtDepot`, or `CeramicBearing/Faction` when it was
  `FactionHeld`; it no longer creates repair cargo directly in the vehicle.
- `LoadDepotRepairCargoCommand` is available only at the resolved depot during
  the existing road-owned transaction, before return freeze, with the exact
  source kind/custody implied by the encounter outcome.
- An accepted load emits the existing `RepairCargoTransferred` event with the
  exact source and destination custody, occupies the existing one ordinary
  cargo unit, and advances the command sequence once. Loading a taken bearing
  also commits `TakenBySasha` disposition and depleted depot control in that
  same atomic transition.
- Repeating the load while the same cargo is already in vehicle custody at the
  depot is an idempotent replay. Early, unresolved, mismatched, empty, home,
  returning, returned, finalized, installed, and forged requests fail closed.
- `FreezeReturnPayloadCommand` requires repair cargo in `Vehicle` custody and
  its one ordinary unit occupied, in addition to the existing module-specific
  return cargo requirements. An otherwise exact legacy development checkpoint
  with already-loaded vehicle cargo and zero used units may normalize that
  occupancy during freeze; source custody can never use this compatibility
  seam.
- The existing `RepairCargoCustody.Depot` and `.Faction` numeric values and
  canonical field layout are retained. No codec field, enum renumbering, save
  version, migration, balance, quantity, capacity, or timer is added.

Old valid development saves whose resolved repair cargo is already in
`Vehicle` custody remain valid and can continue directly to the existing
return preparation. New source-custody and loaded checkpoints round-trip
byte-exactly.

## Depot presentation

- Retain the sole shared camera, canonical vehicle pose, and existing
  `DepotEncounter` mode. Add no second camera, cutaway, avatar, walking mode,
  trigger-authored action, or physics-to-core seam.
- Show only the repair cargo implied by the read model: at the claims counter
  for `Faction` custody or bearing cradle for `Depot` custody; on the existing
  scout cargo socket for vehicle custody; nowhere at the depot after
  turbine/consumed custody.
- Source and stowed markers are primitive C0 presentation with distinct
  silhouettes and tungsten/faction-signal treatment. Text remains available;
  color or emissive light is never the sole state carrier.
- The view owns no input, command, clock, save, filesystem, canonical state,
  Rigidbody, collision, trigger, or raycast authority. Controller/HUD semantic
  intent remains the only presentation-to-command seam.
- `E`/gamepad south invokes load only while the read model reports the exact
  load interaction available. It retains the existing Wreck Line and depot
  recovery priority and is a no-op in every other mode or state.

## Save and lifecycle boundary

- Encounter resolution retains its existing critical autosave, now preserving
  the truthful source-custody checkpoint.
- The accepted source-to-vehicle transfer is also a critical autosave via the
  existing `RepairCargoTransferred` event.
- Save/load between resolution and loading must preserve source kind, custody,
  transaction identity, faction consequence, clocks, and interaction
  availability exactly. Load must not infer or auto-complete the handoff.
- Title, new game, failed load, and successful load derive presentation only
  from the resulting read model; there is no transient cargo intent to clear or
  persist.

## Authority boundary

- Add only the narrow source-to-vehicle command and the minimum deterministic,
  read-model, invariant, adapter, presentation, test, and playtest-contract
  changes required to prove it.
- Preserve exact encounter choices, faction outcomes, route rules, liquid and
  heavy cargo behavior, repair outcome, city consequence, quantities, costs,
  timing, transaction identity, composition parity, and all stable IDs.
- Do not add generalized pickup/drop, cargo selection, weight, slot allocation,
  theft, barter, caravan exchange, manufacturing stock, market law, free-form
  interaction, on-foot play, production assets, dependencies, scenes, packages,
  or project settings.
- D-0030 city grammar, D-0039 composition differentiation, D-0044 physical-goods
  exchange law, D-0045, and D-0046 remain open and unchanged.

## Acceptance

- both encounter outcomes first create repair cargo at their truthful source,
  including both legal ceramic-bearing source custodies;
- neither source-custody state can freeze or begin the return;
- the one contextual action transfers exactly the expected kind from exactly
  the expected source to vehicle custody, occupies one ordinary unit, commits
  taken-bearing physical consequences when applicable, and emits one exact
  transfer event;
- successful, repeated, early, unresolved, mismatched, post-freeze, home, and
  forged load cases satisfy the command-sequence and fail-closed laws;
- source and loaded checkpoints round-trip byte-exactly and restore the same
  interaction availability; the load transition autosaves before fallible
  presentation refresh;
- source and scout-socket markers are mutually exclusive, semantically named,
  physics-free, core-isolated, and readable in the sole depot camera;
- keyboard, gamepad, and semantic HUD routes queue the same command only when
  canonical availability permits it;
- human-only, robot-only, and mixed colonies retain identical custody rules,
  commands, outcomes, timing, and viability;
- existing protected scenarios, deterministic/source tests, save gates,
  foundation lint, Unity compile, EditMode, PlayMode, technical capture,
  protected checks, and diff checks pass.

Green gates prove truthful custody and containment, not final depot drama.
Creator play must still judge whether the handoff reads clearly, feels
deliberate, and makes Sasha's rig meaningfully present in the encounter.
