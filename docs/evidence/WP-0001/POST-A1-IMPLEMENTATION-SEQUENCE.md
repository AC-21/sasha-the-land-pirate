# WP-0001 post-A1 implementation sequence

Status: **planning evidence only**

Use this sequence only after a valid protected activation transaction has
placed WP-0001 in A1. It does not authorize implementation.

## Sequence

### 0. Activation intake

Verify:

- sole active packet `WP-0001`;
- `active_autonomy = A1`;
- exact accepted contract;
- passed mapped gate;
- sealed activation receipt;
- valid boundary and route evidence;
- held unexpired reservation;
- exact detached base and creator-seed manifest.
- exact creator-bound live MCP activation session; any disconnect or process,
  target, config, or allowlist drift closes A1.

Rollback: any mismatch means no implementation. Revoke the route, preserve the
failure, and destroy the candidate/runtime.

### 1. Reproducible static skeleton

Deliver:

- `global.json`
- `Directory.Build.props`
- `Sasha.WP0001.sln`
- exact `Game/Packages/` and `Game/ProjectSettings/`
- dependency/license/known-issue/rollback records
- static-only `.github/workflows/wp0001-ci.yml`
- initial `Tools/Build/` and `Tools/Validation/`

Proof:

- `T-SPIKE-IDENTITY`
- locked restore
- no package-file rewrite
- CI has read-only permissions and cannot start Unity

### 2. Full schema validator

Deliver:

- pinned full Draft 2020-12 validator;
- meta-validation;
- positive and adversarial packet/save/pointer/anchor/asset/scenario fixtures;
- `Tools/Validation/validate_all.py`.

Proof:

- `T-SCHEMA-VALIDATOR`
- `BuildArtifacts/WP-0001/schema-validation.json`

Rollback: replace the validator behind the packet interface before freezing
save fixtures; retain version/license/removal evidence.

### 3. Engine-independent deterministic core

Deliver:

- dual-use `SimulationCore`;
- `netstandard2.1`, C# 9-or-lower Unity-consumed source;
- stable IDs and persisted allocators;
- fixed ticks;
- checked integer/fixed-point policy;
- injected clock, counter/key RNG, storage, and logging;
- commands, state transitions, events, read models;
- canonical state encoding;
- pure scenario runner;
- no `UnityEngine` dependency.

Proof:

- `T-STATE-CORE`
- pure-C# half of `T-DETERMINISM-VECTOR`
- dependency-direction and static checks

Do not freeze the golden vector until Unity standalone agrees.

### 4. Save and exact-ownership contracts

Deliver:

- `SaveContracts`;
- Envelope/root/pointer/protected-retention-anchor v1;
- immutable generations;
- bounded reader and recovery scan;
- atomic filesystem abstraction;
- fault injector;
- pruning and anchor advance;
- rollback fork and lineage provenance;
- save inspector;
- synthetic ownership phases:
  `prepared -> city-debited -> road-owned -> return-pending -> city-credited -> finalized`;
- golden saves.

Proof:

- `T-SAVE-FAULTS`
- `T-POINTER-RECOVERY`
- `T-RETENTION-ANCHOR`
- `T-OWNERSHIP`
- rerun schema validation
- run pinned migration and rollback scenarios as supporting evidence

Hard stop: any duplicate, lost, or orphaned unit. Never edit a failing golden
generation or anchor in place.

### 5. Unity adapter and native smoke

Deliver:

- local Unity packages compiling the same source without copying;
- thin presentation/bootstrap adapters;
- exact temporary identity;
- EditMode and PlayMode tests;
- creator/manual-or-MCP-routed build wrapper;
- machine-readable launch smoke.

Proof:

- initial `T-BATCH-BUILD`
- parsed NUnit XML and Editor logs
- `ENABLE_IL2CPP`
- ARM64 application executable and `GameAssembly`
- direct-player smoke
- completed cross-runtime `T-DETERMINISM-VECTOR`

Any route, seat, target, version, scope, or policy drift closes quarantine.

### 6. Technical renderer and calibration

Deliver only neutral benchmark fixtures:

- 12 technical archetypes;
- 36 authored LOD meshes;
- 24 technical textures;
- 8 shared materials;
- 512 placed instances;
- per-instance technical state;
- exact lights, probes, shadows, and submission rules;
- hash-bound camera loop;
- one canonical Blender calibration `.blend`;
- export, manifest, prefab, provenance, and contact sheet.

Proof:

- `T-DYNAMIC-CITY-RENDER`
- `T-BLENDER-CALIBRATION`
- content-addressed visual evidence

These assets are disposable technical fixtures. They do not decide city
grammar, production art, Texas-iron interpretation, or gameplay.

### 7. Native acceptance bundle

Run:

- final `T-CLEAN-CLONE`;
- final `T-BATCH-BUILD`;
- every required packet test;
- five-minute warm-up plus 30 measured minutes;
- forced-failure recovery;
- exact environment and thermal capture;
- at least three complete gate bundles under the conservative interpretation
  of the accepted rollout minimum.

Never loosen a budget to pass.

### 8. Creator candidate handoff

Deliver:

- content-addressed complete diff;
- artifact manifest;
- command log;
- actual-path inventory;
- known limits;
- failed/aborted attempt ledger;
- Godot fallback assessment;
- engine decision proposal;
- candidate control-plane proposals only under
  `BuildArtifacts/WP-0001/candidate-control-plane/`.

The creator manually imports or rejects the bounded candidate. No A1 agent
merges or accepts its own evidence.

## What can precede technical art

Everything through phase 5:

- schema/adversarial validation;
- deterministic state core;
- save, pointer, anchor, migration, rollback, and inspection tooling;
- synthetic ownership recovery;
- static CI and dependency locks;
- Unity package adapters;
- empty native IL2CPP ARM64 player;
- cross-runtime determinism proof.

The renderer manifest and camera tooling may be prepared before meshes. Only
the render and Blender calibration gates require technical assets.

## Contract ambiguities that remain protected blockers

### Project seed bytes

Boundary schema v4 requires a creator-created protected setup commit and rejects
a dirty candidate seed, embedded package code, non-registry sources, and
non-empty Assets. The physical seed and its committed evidence still need to be
created before activation.

### Renderer artifacts

The technical architecture requires per-mesh/texture hashes and every camera
pose sample. The pinned `SCN_SPIKE_SLICE` artifacts currently express counts,
imports, and a camera generator/checksum rather than that complete explicit
inventory. An A1 agent may propose a revision only under
`candidate-control-plane/`; it cannot rewrite the protected scenario.

### Migration and rollback acceptance

`SCN_MIGRATION_MATRIX` and `SCN_ROLLBACK_DRILL` are pinned/golden and rollback
drill is required, but no named acceptance-test record directly invokes either
scenario. Run them as mandatory supporting evidence. Changing the accepted
test list requires protected contract authority.

### Candidate verification principal

A sibling A1 agent is advisory, not a trusted verifier. Candidate transition
must name the creator or another externally trusted principal.

### Minimum runs

Until protected clarification says otherwise, treat `minimum_runs = 3` as
three complete acceptance bundles, including native performance evidence.

## Completion authority

WP-0001 is complete only after:

- creator manual import;
- protected engine decision ratifying or superseding D-0013 and D-0033;
- sealed creator `packet-completion` receipt with
  `ACCEPT-COMPLETION-WP-0001`;
- protected status transition to `released`.

Release of WP-0001 does not itself activate WP-0002.
