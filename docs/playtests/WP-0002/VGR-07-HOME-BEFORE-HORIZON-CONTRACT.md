# VGR-07 — Home Before Horizon Contract

Status: released on protected `main` in
`b35157a07a7442f6b02ed179201cd59e89368996` after independent review, exact
Unity dispatcher gates, protected checks, and transparent creator-delegated
manual release.

Baseline: protected `main` commit
`8f2b973e10682a77339b28bde67eb7a9ff5679bf`.

## Proof

Let a player perform one small city-building and local-logistics task under both
authorized D-0030 hypotheses without saving a placement, ranking a winner, or
turning presentation state into simulation authority.

Both trials stage the same bounded service cell:

- one recycler;
- one machine shop;
- three equivalent authored sockets;
- one local recycler-output to workshop-input path;
- one empty calibration sled;
- the exact provisional D-0022 comparison camera; and
- identical colony representation, costs, inventory, and canonical state.

The empty sled is an observation prop. It moves no stock or inventory.

## Trial A — individual buildings

1. Place the recycler on one of three restrained snap pads.
2. Switch the active building and place the workshop on a different pad.
3. Rotate or move either building without mutating the simulation.
4. Connect the recycler output to the workshop input with one visible service
   link.
5. Advance the empty calibration sled from source, through the link, to the
   workshop.
6. Record `PATH READS CLEAR` or `PATH READS UNCLEAR` without scoring it.

An occupied-pad conflict does not connect. Moving or rotating a building after
the run begins invalidates the delivery and prior path observation.

## Trial B — district stamp

1. Place the complete recycler, workshop, and shared logistics apron on one of
   three equivalent authored anchors.
2. Rotate or restamp the whole service cell without mutating the simulation.
3. Advance the same empty calibration sled across the internal apron to the
   workshop.
4. Record the same clear/unclear path observation without scoring it.

Restamping or rotating after the run begins invalidates that delivery and prior
path observation.

## Comparison and lifecycle

- Switching A/B retains each local layout and observation for direct visual
  comparison under the same camera.
- `RESET ACTIVE TRIAL` clears only the visible hypothesis.
- `CLEAR BOTH TRIALS` clears both layouts and observations.
- Leaving the lab hides it while retaining the current in-memory comparison.
- new game, title, load, or expedition departure clears the entire transient
  comparison.
- Evidence reports raw action counts and both clear/unclear observations. It
  contains no recommendation, composite score, automatic winner, or decision.
- Evidence writes no file, `PlayerPrefs`, save field, or canonical state.

## Canonical convergence

After either observation is complete, the existing
`ActivateSliceInfrastructureCommand` may bring the recycler and machine shop
online. Both trial routes queue that identical command and produce identical
canonical bytes from the same starting state. The command records only the
existing `SliceInfrastructureActive` fact; it records no layout, path,
hypothesis, observation, or D-0030 selection.

This convergence keeps the playable job moving while the observed comparison
remains neutral. It is not evidence that either grammar won.

## Authority boundary

- Add no canonical command, field, event, balance, inventory, population,
  logistics architecture, codec, save version, dependency, scene, asset, or
  project-setting change.
- The comparison may use Unity transforms and temporary IMGUI only.
- Trial verbs may never queue a command. The already-existing infrastructure
  activation is the sole canonical seam and is gated by a completed local
  observation.
- D-0030 remains open: this trial covers placement granularity and local path
  legibility only, not population granularity or production city scale.
- D-0039, D-0044, D-0045, and D-0046 remain open and unchanged.

## Acceptance

- both exact trial sequences complete and remain independently inspectable;
- invalid layout, premature delivery, duplicate delivery, and premature path
  observations fail without canonical mutation;
- A/B selection, movement, rotation, connection, delivery, observation,
  reset, leave, and session clear preserve the canonical hash and queue no
  command;
- both completed observations converge on the same existing infrastructure
  command and byte-identical resulting canonical state;
- camera pose and setup remain identical across both hypotheses;
- human-only, robot-only, and mixed colonies retain identical mechanics;
- no comparison source references kernel, state, command, save, filesystem,
  network, `PlayerPrefs`, NavMesh, or package APIs;
- deterministic/source, foundation, Unity compile, EditMode, PlayMode,
  technical-capture, protected, and diff gates pass.

Observed creator play remains required before D-0030 can be selected or
superseded. A green test suite proves containment and convergence, not fun or a
winner.
