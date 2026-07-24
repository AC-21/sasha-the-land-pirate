# VGR-24 — Run the Wreck Line Again Core Contract

## Player outcome

After Sasha finishes every urgent first-return job and services the Scout to
1.000 condition, the colony can send the same rig down the Wreck Line again.
The repeat circuit spends the existing route fuel plus any persistent authored
future-route toll, takes the existing route condition loss, recovers one
frame-rail bundle, and credits exactly four parts on return. A second circuit
requires another completed Scout service.

## Authority

- `PrepareRepeatExpeditionTransactionCommand` is a compare-and-swap launch:
  it names the exact finalized predecessor transaction and a fresh repeat
  transaction.
- Repeat identities reserve `tx:repeat:<command.Sequence>` and
  `fp:repeat:<command.Sequence>` using the invariant-culture decimal command
  sequence. Ordinary preparation, mismatched tokens, and immediate or
  nonadjacent identity reuse fail before simulation mutation.
- Launch is available only at `AtHome/Finalized`, after the exact turbine
  repair, resolved city/aid/batch/bond/maintenance/dust work, full 1.000 Scout
  condition, the retained two-part reserve, and enough fuel for the selected
  module's existing route cost plus any persistent authored toll.
- The repeat command resets only transaction identity/phase, route progress,
  movement/lateral/action, return-freeze, arrival snapshot, current cargo/tow
  occupancy, and the frame-rail custody witness.
- Every relaxed invariant delegates to one
  `LastBearingRepeatExpedition.IsLineage` predicate. It requires the reserved
  repeat identity, exact permanent cooperative or adverse first-return history,
  and phase-specific route, condition, salvage, occupancy, and tow witnesses.
  Installed city improvements remain valid permanent history. A repaired
  turbine alone is not a repeat witness.

## Existing path reused

The repeat uses the existing prepare/debit/depart, drive, Wreck Line module,
frame-rail recovery, depot recovery point, freeze, return, city credit, and
finalize transitions. It creates no second faction decision, repair cargo,
pump rotor, or liquid return. Existing events report transaction preparation,
fuel debit, route use, salvage custody, condition loss, city credit, and
finalization.

The four-part bundle reuses `WreckLineFrameRailSalvagePartsUnits`; fuel uses
`RouteFuelCost` plus the already-saved `FutureRouteTollFuelUnits`, and condition
uses `RouteConditionLoss`. No balance value changes.

## Persistence and replay

Schema 9 and the existing canonical fields are unchanged. Ready, prepared,
outbound, depot, returning, and finalized repeat states round-trip to identical
canonical bytes. Duplicate, stale-predecessor, active-transaction, unserviced,
urgent-work, and insufficient-fuel launches fail atomically. A finalized repeat
must be serviced before another fresh compare-and-swap transaction can begin.
Schema 9 has no accumulated road-edge-damage witness, so active and credited
lineage accepts only the deterministic condition range reachable from a 1.000
service, route progress, edge-loss rate, and the exact fixed return charge.

## Hard cuts

No saved field, schema, migration, event kind, balance constant, generalized
expedition framework, second faction choice, second repair cargo, second rotor,
Unity presentation, scene, asset, package, or dependency.
