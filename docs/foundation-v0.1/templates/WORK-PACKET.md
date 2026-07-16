# Work Packet Authoring Worksheet

This Markdown file is not authoritative. Before acceptance, the proposal must be encoded as versioned JSON conforming to [`../schemas/work-packet.schema.json`](../schemas/work-packet.schema.json). The trusted gatekeeper records approval, reservation, events, evidence, verification, and release state.

## Identity and authority

- Proposed ID / UUID:
- Class:
- Created on:
- Objective:
- Player or production value:
- Requested by:
- Required approver:
- Immutable packet-contract SHA-256 (canonical projection v1):
- Constitution / decision / system links:

## Risk

- Declared risk:
- Save risk:
- Expected diff-derived risk surfaces:
- Required distinct implementer / verifier / integrator:

## Baseline evidence

For each item: stable evidence ID, type, URI, SHA-256, build/seed/environment.

## Scope

### In

-

### Non-goals

-

### Affected seams

- State domains:
- Stable IDs / content IDs:
- Commands / events:
- Interfaces:
- Declared paths:
- Dependencies:

## Reservation

- Base commit:
- Paths/domains/content IDs:
- Lease owner / expiry / heartbeat:
- Fencing token:
- Scarce runner/license/GPU resources:

## A1 boundary (when required)

- Boundary manifest ID / safe foundation-relative path / raw SHA-256:
- Exact toolchain and environment tuple:
- Current constitution / decision-ledger hashes and last creator receipt:
- Read-only protected paths and exact writable paths:
- Credential denial and creator-operated import-or-reject boundary:
- Explicit local-observation exception evidence IDs:

## Save impact

- S0/S1/S2/S3:
- Schema changes:
- Migration IDs:
- Read/write compatibility:
- Golden scenarios:
- Immutable backup and recovery tool:

## Acceptance tests

For each: test ID, kind, exact command, oracle, required/optional.

## Performance and health metrics

For each: metric, scenario, unit, baseline, target, regression ceiling, comparator.

## Visual / interaction evidence

For each: evidence ID, camera/input path, URI, SHA-256, approval owner.

## Rollout

- Required autonomy:
- Feature flag and persistent-data behavior:
- Ordered stages:
- Cohort/profile:
- Minimum runs/play-hours and duration:
- Health thresholds and failure triggers:
- Required approver:

## Rollback

- Exact target build:
- Compatibility with forward saves:
- Trigger conditions:
- Ordered steps:
- Save recovery and possible progress loss:
- Required drill and evidence:

## Candidate evidence

- Actual changed paths:
- Content-addressed evidence manifest:
- Known limits:
- Diff artifact ID, artifact-manifest ID, and command-log artifact ID:

## Independent verification

- Distinct verifier principal:
- Gate results:
- Adversarial cases:
- Evidence IDs:

## Result

The protected event ledger records every proposed, accepted, active, aborted, failed, candidate, released, rejected, rolled-back, or superseded transition. A worksheet cannot declare itself released.

The `ACCEPT-WP-####` receipt binds the packet contract hash, not the mutable packet JSON file. The contract projection covers identity, declared risk/save risk, scope, dependencies, scenario pins, tests, rollout, and rollback while excluding lifecycle state, receipts, reservations, actor assignments, generated evidence, and release fields. A creator-accepted completion receipt is separate from acceptance and activation.
