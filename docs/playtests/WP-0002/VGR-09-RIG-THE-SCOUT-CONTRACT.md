# VGR-09 — Rig the Scout Contract

Status: creator-directed playable milestone; release remains conditional on
independent review, exact Unity dispatcher gates, protected checks, and
transparent creator-delegated manual release.

Baseline: protected `main` commit
`ab5da82ea51718227db775d99dacbac9cdf60256`.

## Proof

Make expedition preparation happen around Sasha's physical rig in the existing
fixed garage cutaway. The player first chooses the city's preparation posture,
then inspects the two already-authored module stands and commits exactly one
module from the garage before the existing preparation clock begins.

This slice reuses `SelectPreparationCommand` and
`InstallVehicleModuleCommand`. It adds no canonical plan field, new vehicle
capability, balance, save data, on-foot mode, or second interaction truth.

## Player sequence

1. After reading the water need and activating the bounded service cell, choose
   `Workshop Push` or `Civic Buffer` as a local, uncommitted planning intent.
2. The shared camera opens the existing Sasha Scout garage cutaway. Both module
   stands, the vehicle dock, service access, and preparation gauge remain in one
   readable frame.
3. Commit `Winch Assembly` or `Sealed Range Tank` from that garage view.
4. The controller queues the existing preparation and module-install commands
   together. Only their accepted canonical result starts work.
5. The existing physical module stand, scout socket, assembly gauge, clocks,
   costs, route, and return consequences continue to derive from the read model.

`Civic Buffer + Winch Assembly` may retain the existing recommended-first-run
cue. All four preparation/module combinations remain valid.

## Transient planning boundary

- The preparation intent exists only in the Unity presentation controller.
- Choosing an intent queues no command and preserves the canonical hash,
  command sequence, save bytes, clocks, inventory, and vehicle state.
- A module can be committed only while the intent is active, Sasha is at home,
  the canonical preparation is still unselected, and the garage cutaway is the
  active mode.
- Cancel returns to the city without queuing a command.
- Title, new game, load, and successful commit clear the intent. Departure also
  fails closed and clears any impossible stale intent.
- Saving while an intent is uncommitted saves the unchanged canonical state;
  loading never recreates or infers the transient intent.

## Garage presentation

- Retain the sole shared camera and existing fixed dollhouse garage pose.
- Show both module stands while awaiting commitment and a physical plan marker
  for the selected preparation posture. The marker is presentation evidence,
  not state authority and may not be the only textual explanation.
- After commitment, the existing module stand/vehicle socket and assembly gauge
  show canonical planned module and preparation progress.
- The garage view owns no input, command, clock, save, filesystem, physics, or
  canonical-state reference. Controller/HUD semantic intent remains the only
  presentation-to-command seam.

## Authority boundary

- Add no canonical command, field, event, balance, route rule, cargo rule,
  interaction outcome, codec, save version, dependency, scene, asset, or
  project-setting change.
- Preserve the existing four preparation/module outcomes, exact costs/timing,
  composition parity, route verbs, and return consequences.
- Do not add free-form module installation, hardpoints, upgrade inventory,
  crafting, a third module, on-foot interaction, raycast-authored state, or a
  generalized garage/vehicle framework.
- D-0030, D-0039, D-0044, D-0045, and D-0046 remain open and unchanged.

## Acceptance

- the four global composite plan buttons are replaced by a city-preparation
  choice followed by a garage-context module commitment;
- both preparation intents open the same fixed garage and both module choices
  are available under each intent;
- valid commitment queues exactly the existing two commands and produces the
  same canonical bytes as the equivalent pre-slice composite action;
- choosing, switching, canceling, saving, title, new game, and loading an
  uncommitted intent preserve canonical bytes and queue no command;
- early, stale, non-garage, away-from-home, duplicate, and invalid module
  commitments fail closed;
- both module stands, the selected physical plan marker, installed module, and
  preparation gauge remain readable through the sole shared camera;
- human-only, robot-only, and mixed colonies retain identical commands, costs,
  timing, viability, and garage interaction;
- deterministic/source, foundation, Unity compile, EditMode, PlayMode,
  technical-capture, protected, and diff gates pass.

Green gates prove containment and mechanical equivalence, not that the garage
interaction is satisfying. Creator play must still judge framing, clarity, and
whether committing work on Sasha's rig feels like part of the city-road loop.
