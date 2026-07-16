# WP-0001 schema-v5 adoption sequence proposal

Status: **A0 / proposed / unresolved**

This document grants no authority, activates no packet, and authorizes no
Unity, Hub, Editor, relay, Codex, MCP, provider, or collector invocation. It
resolves a governance sequencing problem only.

## Problem

The proposed code-identity-context provider requires:

- a schema-v5 successor;
- selected provider and verification route;
- exact validator, parser, collector, and system-tool identities;
- final source and artifact hashes;
- a creator receipt binding those exact values.

Final implementation hashes cannot exist before implementation. But allowing
unratified implementation to produce activation-eligible evidence would let the
candidate authorize itself.

The safe sequence is two protected transactions around one isolated,
non-activation implementation lane, followed by a separate activation
transaction.

## Transaction A: design and implementation authority

Protected authority first ratifies the semantic contract and opens a bounded
A0 governance-change reservation. This is not WP-0001's packet activation
reservation and can never satisfy its required `held` A1 reservation.

Transaction A cannot exist under the current A0 law by receipt alone. Before it
is issued, the creator must ratify a matching decision-ledger event and
synchronized `AGENTS.md`/autonomy-law successor that explicitly permits this
narrow governance-tool implementation lane while preserving every prohibition
below. Without that higher-authority change, A0's implementation hard stop
wins and agents remain proposal-only.

Proposed claim:

`AUTHORIZE-WP0001-SCHEMA-V5-IMPLEMENTATION`

Transaction A must be a creator-issued, sealed, external-protected receipt. Its
issuer role is `creator`; its subject is the WP-0001 schema-v5 governance
implementation lane; its resolver/signature reference must be externally
authenticated; and it must bind the accepted packet contract, exact base
commit, predecessor receipt, and claim below.

The design receipt must bind:

- the ratified autonomy-law successor decision/event and hashes;
- predecessor receipt and exact protected base commit;
- packet contract;
- one closed `raw_evidence_authority_design` object with four named domains:
  protocol, network, policy attachment, and code-identity context;
- selected verification route and independent provider design inside each
  applicable domain;
- hashes of `RAW-CAPTURE-PROVIDER-DECISION.md`,
  `CODE-IDENTITY-CONTEXT-PROVIDER-PROPOSAL.md`, and this sequence;
- schema-v5 target version and exact authority-object shape;
- allowed implementation paths and affected domains;
- governance-reservation ID/type, with
  `reservation_scope: a0-governance-only`;
- temporary implementer identity;
- lease, fencing token, expiry, and review owners;
- permitted CI/test commands;
- forbidden target processes, real provider captures, credentials, protected
  receipt writes, activation evidence, and Unity tool calls;
- `activation_evidence_eligible: false`;
- `agent_can_activate: false`;
- `agent_can_accept_final_hashes: false`;
- `creator_finalization_required: true`.

This receipt authorizes implementation of the already-ratified design. It does
not pre-approve source hashes that do not yet exist.

Transaction A is a narrow governance-only exception to the runbook's later
project-seed sequencing. It authorizes no project or packet implementation.
The creator-created protected seed and setup commit must still exist in the
exact finalized base before Transaction B.

## Non-activation implementation lane

After Transaction A, agents may work only inside the reserved paths.

Proposed minimum path set:

- `docs/foundation-v0.1/schemas/a1-boundary-manifest.schema.json`;
- WP-0001 activation/evidence schemas;
- `docs/foundation-v0.1/tools/validate_foundation.py`;
- new source-hashed provider adapter/collector and deterministic parser paths
  named by the design receipt;
- fixtures and tests;
- creator runbook and evidence indexes;
- no game, Unity project, asset, package, or implementation path.

Permitted work:

- schema and validator implementation;
- deterministic normalization;
- synthetic positive fixtures;
- negative fixtures for every fail-closed case;
- unit, bootstrap, static type/compile, and whitespace checks;
- independent code and threat-model review;
- draft PR publication through protected CI.

Forbidden work:

- invoking Unity, Hub, Editor, relay, Codex MCP, or a Unity tool;
- connecting to the selected provider against live A1 targets;
- producing or importing real activation captures;
- creating a boundary manifest, held activation reservation, or activation
  receipt;
- changing the provider, route, threat model, semantic contract, authority
  object, or allowed paths without a replacement design receipt;
- claiming that synthetic fixtures prove provider independence or activation.

The implementation branch and CI output are always A0 and
activation-ineligible.

## Creator review and merge

Before merge, protected reviewers must verify:

- diff stays inside the reservation;
- schema is closed and versioned as a successor, not a silent v4 mutation;
- provider-native raw bytes remain distinct from normalized output;
- every required cross-binding and negative case has an executable test;
- failed, aborted, and secret-bearing attempts remain preservable;
- no real target/provider capture occurred;
- source hashes and generated fixture hashes are reproducible;
- foundation validation and the complete regression suite pass;
- independent review has no unresolved P0/P1/P2 finding.

Only the creator merges the implementation transaction. Merge does not
activate the packet.

## Transaction B: final hash and provider ratification

After the reviewed implementation is merged and the creator-created protected
seed/setup commit exists, protected authority freezes one exact protected base
and performs a fresh finalization transaction.

Transaction B must be issued as an external-protected authority record against
the exact implementation merge commit. It may not modify the finalized base it
authorizes. Committing a mirror or pointer before candidate creation changes
the base and requires Transaction B to be reissued.

The final receipt must:

- reference the Transaction-A receipt;
- bind the exact finalized protected base containing the merged schema-v5
  implementation and protected seed/setup commit;
- bind the complete schema-v5 authority object SHA-256;
- bind the closed four-domain raw-evidence authority object and each exact
  provider, verifier-boundary, parser, collector, validator, `codesign`, and
  `spctl` identity/hash;
- bind the immutable attempt-ledger/quarantine design;
- bind the exact receipt claim required by schema v5, including
  the existing raw-collector claim and
  `AUTHORIZE-WP0001-CODE-IDENTITY-CONTEXT`;
- issue a finalization challenge root from which the authenticated provider or
  creator derives unique single-use child challenges per attempt;
- bind the final test/review receipts;
- declare the approved activation-evidence paths;
- set an expiry and revocation rule;
- preserve `agent_can_activate: false`;
- require a fresh detached candidate from the finalized base.

No branch, candidate, or capture prepared before Transaction B may be reused as
activation evidence.

Any Transaction-B-bound provider, system-tool, policy, seed, schema, validator,
parser, collector, authority object, or finalized-base input change invalidates
Transaction B and requires a new finalization receipt.

The later activation/state transaction may mirror the external finalization
receipt while continuing to bind the candidate to the earlier finalized
implementation base. That append-only state transaction does not invalidate B
when it changes only the authorized boundary/evidence/receipt/gate/reservation
state and references the unchanged finalized base. It may not retroactively
redefine that candidate base or any B-bound input.

## Separate activation transaction

Only after Transaction B may the creator:

1. create the fresh detached, remote-free candidate;
2. establish the exact physical A1 boundary;
3. run the selected provider and authorized collectors;
4. preserve every attempt;
5. validate the complete raw evidence;
6. issue the separate activation receipt;
7. atomically move WP-0001 from `accepted`/A0 to `active`/A1.

Every attempt receives a unique provider-authenticated child challenge bound to
the Transaction-B receipt ID, challenge root, attempt ID, and monotonic attempt
sequence. Starting an attempt consumes only that child challenge; a failed or
aborted attempt does not invalidate Transaction B, but the child may never be
reused.

Every provider attempt-start record and retained capture must bind its child
challenge. The boundary manifest, activation-evidence manifest, and activation
receipt must bind the Transaction-B receipt ID, raw-evidence authority-object
SHA-256, challenge root, complete attempt census, and the selected successful
attempt's child challenge. The adopted schema/gate must reject any missing,
reused, or mismatched predecessor link.

Implementation authority, final hash authority, and activation authority are
three different states. None implies the next.

## Fail-closed rules

Reject the sequence if:

- Transaction A does not predate implementation;
- provider or route is still unresolved when pass-producing adapter work
  begins;
- implementation escapes reserved paths or changes the semantic contract;
- agents or CI invoke target processes or create real captures;
- Transaction B binds a branch rather than the merged protected commit;
- Transaction B is committed into, or otherwise mutates, the base it claims to
  finalize;
- Transaction B's finalized base lacks the protected creator seed/setup commit;
- any raw-evidence domain, exact provider, or required claim is omitted;
- final hashes are accepted by the implementation agent;
- the finalized base changes before candidate creation;
- a pre-finalization candidate or capture is reused;
- activation occurs without its own sealed receipt;
- a capture, boundary, evidence manifest, or activation receipt omits the
  Transaction-B receipt/hash/challenge-root predecessor;
- an attempt child challenge is missing, reused, mismatched, or not bound to
  the exact attempt ID and monotonic sequence;
- any stage collapses design, implementation, finalization, and activation into
  one self-authorizing transaction.

## Current disposition

No transaction or claim in this proposal exists. Keep A0, keep WP-0001
`accepted`, keep the protected repo MCP entry disabled, and keep Unity tool
calls at zero.

Recommended next protected action: ratify or reject the four-domain
provider/route design, then issue Transaction A over an exact base and a
separate A0 governance-change reservation.
