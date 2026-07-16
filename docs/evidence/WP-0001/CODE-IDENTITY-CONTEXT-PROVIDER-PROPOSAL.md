# WP-0001 code-identity-context provider proposal

Status: **A0 / proposed / unresolved**

This document grants no authority, activates no packet, and authorizes no
Unity, Hub, Editor, relay, Codex, or MCP invocation. It defines the smallest
machine-enforced successor needed after
`COMPONENT-SIGNATURE-RECHECK-20260716.md` exposed context-dependent signing
results.

## Problem

The current contract binds target paths, executable hashes, process births, and
signing tuples. `validate_wp0001_mcp_live.py` also runs strict `codesign`, but
reduces the result to a boolean attached to each target.

It does not bind:

- the verifier's executable, PID, parent, UID, or process birth;
- the verifier's sandbox and network-policy attachment;
- the `codesign` or `spctl` child-process identity and policy attachment;
- the verifier's filesystem/mount view of the executable and app bundle;
- exact command argv, exit status, timestamp, or retained stdout/stderr;
- a common verification context across client, relay, and Editor;
- bundle-appropriate macOS execute-policy assessment;
- an independent evidence provider for those facts.

The existing policy-attachment capture covers exactly the client, relay, and
Editor. It does not cover the verifier or its children. A host-context
`codesign` pass can therefore satisfy the current tuple gate even if the exact
A1 execution context fails.

## Protected verification-route choice

Protected authority must select exactly one route. Neither is selected here.

### A. In-boundary verification

Run the collector and system signing commands inside the exact A1 principal,
filesystem view, sandbox/network policy boundary, and the exact protected
process-ancestry records for the verifier and each component.

### B. Independent external verifier

Use a separately authenticated read-only provider outside the A1 principal
only through a distinct closed-schema branch. A protected contract must:

- identify and hash the complete external principal, process, policy,
  filesystem, system-tool, and provider boundary;
- explicitly redefine that boundary as authoritative; and
- include independently authenticated object/view mappings from every external
  observation to the exact A1 component path, vnode, hash, process birth,
  bundle resource, and policy-attachment record.

A normal host-shell pass, including the A0 diagnostic already recorded, is
ineligible under either route.

## Proposed provider domain

Add a fourth raw-evidence domain named `code-identity-context` beside
`protocol`, `network`, and `policy-attachment`.

The provider must independently expose the verifier context and exact system
command results. The collector may copy and package those bytes, but may not
invent its own context claims and then attest to them.

Proposed fixed paths:

| Role | Proposed path |
| --- | --- |
| collector source | `docs/foundation-v0.1/tools/capture_wp0001_code_identity_context.py` |
| immutable attempt root | `docs/evidence/WP-0001/a1-activation/commands/code-identity-context/<attempt-id>/` |
| normalized, secret-free payload | `<attempt-root>/capture-payload.json` |
| retained provider completion record | `<attempt-root>/attempt-completion.json` |
| normalized attempt-manifest mirror | `docs/evidence/WP-0001/a1-activation/commands/code-identity-context/attempts.json` |

These paths are proposals, not authorized implementation locations.

## Protected expected authority

Schema v5 must add a `code_identity_context_authority` object outside the raw
artifact. It must bind:

- selected route;
- exact provider identity, version, executable/API/source hash,
  authentication principal and method, output schema/version, and retained
  artifact rules;
- exact verifier boundary or protected external-boundary identity;
- expected `codesign` and `spctl` paths, hashes, signing identities, ownership,
  modes, and OS build;
- route-specific context schema and required A1 object mappings;
- collector and parser paths/hashes;
- immutable attempt-path pattern and attempt-manifest path;
- authoritative provider/creator-operated attempt-ledger identity, writer
  principal, monotonic sequence rule, hash-chain rule, and completeness
  attestation;
- freshness and timeout limits;
- exact receipt authority claim.

Proposed claim:

`AUTHORIZE-WP0001-CODE-IDENTITY-CONTEXT`

The creator receipt must bind the canonical SHA-256 of the complete authority
object. Raw `provider`, `verification_route`, tool, and path values must equal
those protected expected values; they may not be accepted by comparing the raw
artifact to itself. This authority receipt must predate capture. Its receipt ID,
authority-object SHA-256, and provider-authenticated one-use challenge must be
embedded in every provider record and attempt. The activation receipt must name
that predecessor receipt.

## Proposed raw artifact

The raw artifact should be a closed-schema object with these top-level fields:

```text
schema_version
capture_kind
packet_id
attempt_id
provider_attempt_sequence
previous_attempt_sha256
attempt_start_record_sha256
authority_receipt_id
authority_sha256
authority_challenge
status
eligible_for_activation
started_at
captured_at
provider
provider_artifacts
collector
verification_route
verification_context
verification_context_sha256
components
capture_complete
secret_material_included
```

`capture_kind` must equal `code-identity-context`; `packet_id` must equal
`WP-0001`. `status` must be one of `success`, `failure`, or `aborted`.
Every attempt must be retained at a new immutable attempt path and listed in
the normalized attempt-manifest mirror.

Only a `success` record with `capture_complete: true`,
`eligible_for_activation: true`, and `secret_material_included: false` may be
referenced by the activation boundary. Failed and aborted attempts must remain
preserved with `eligible_for_activation: false`; a retry may not overwrite
them.

Every repository `capture-payload.json` must have
`secret_material_included: false`.
Secret-bearing raw failures use the protected quarantine/redaction path below,
not an exception to the Git evidence rule.

Attempt completeness must come from a protected external or creator-operated
ledger outside candidate write authority, or from provider-authenticated
monotonic attempt IDs plus a completeness checkpoint. The ledger uses two
closed record types:

1. `attempt-start`, durably written before provider initialization or any
   signing command, binds attempt ID, authority receipt/hash, one-use challenge,
   writer identity, sequence, previous-ledger digest, and start time;
2. `attempt-completion`, written after termination, binds the start-record
   digest, immutable `capture-payload.json` SHA-256,
   success/failure/aborted status, finish time, writer identity, and provider
   completeness checkpoint.

Each record participates in the provider hash chain. If the start record cannot
be committed, the attempt may not begin. The repository manifest is a verified
mirror, not the source of append-only truth, and must reproduce both records for
every attempt through the signed completeness checkpoint.

The hash projection is deliberately non-circular:

1. `capture-payload.json` includes `attempt_start_record_sha256` but contains no
   payload-self hash or completion-record hash;
2. `attempt-completion.json` binds `capture_payload_sha256`;
3. the attempt-manifest mirror binds the start-record SHA-256, payload SHA-256,
   and completion-record SHA-256.

The activation evidence manifest and receipt must bind all three artifacts.

The attempt schema must use a status-specific closed `oneOf`:

- `success` requires the complete provider, collector, context, three
  components, target commands, and retained provider artifacts defined below;
- `failure` and `aborted` require an immutable minimal envelope containing the
  authority binding, ledger sequence/hash link, start/finish time, failed stage,
  reason code, available partial artifacts, explicit `not_reached` stages,
  redaction state, and `eligible_for_activation: false`.

An abort before provider or component initialization must therefore remain
representable without fabricating fields that were never reached.

If a failed or aborted attempt contains secret material, raw bytes must remain
in a protected non-repository quarantine controlled by the creator/provider.
Git stores only a reviewed redaction record, raw byte length/SHA-256, incident
reference, protected-vault locator, and provider-authenticated ledger entry.
Such an attempt is permanently ineligible for activation.

### Provider and collector

`provider` must bind:

- provider type and stable identity;
- version and executable/API/source hash;
- authentication or independent-attestation method;
- trust principal and privileges;
- output format/version;
- whether the provider changes the direct route or threat model.

For a successful attempt, `provider_artifacts` must be a non-empty list of
retained provider-native records. A failure/abort may contain only the partial
records produced before its failed stage. Each item must bind:

- record ID and provider identity;
- repository evidence path or exact retained byte range;
- media type and canonical encoding;
- decoded byte length and SHA-256;
- capture timestamp and source object/process identity;
- provider authentication signature, certificate/attestation chain, or exact
  protected equivalent;
- deterministic parser name, version, path, and SHA-256.

The packaged normalized `capture-payload.json` is not itself provider-native
evidence. Validation must hash and parse the retained provider bytes, and must
prove that every normalized fact is derived from an identified record or byte
range.

`collector` must bind:

- name, version, repository path, and SHA-256;
- exact invocation;
- collector PID, parent, UID, executable path/SHA-256, and process birth;
- a declaration that provider bytes were copied without semantic rewriting.

Provider independence requires all of:

- provider-authenticated raw bytes or provider-native records;
- a distinct protected trust principal or externally readable OS/managed
  attachment facility outside candidate write authority;
- collector inability to configure, rewrite, or forge provider output;
- deterministic parsing from retained bytes;
- separate protected identities for provider, collector, and normalizer.

If no facility satisfies that predicate, schema v5 fails closed.
Self-attestation is rejected unconditionally. Any weaker trust model requires a
separately versioned successor and may not be labeled provider-independent or
satisfy this schema.

### Verification context

`verification_context` must bind:

- a closed-schema `oneOf` selected route:
  `in-a1-boundary` or `protected-external-verifier`;
- `a1_runtime_principal` with principal ID, real/effective UID,
  real/effective GID, and sorted supplementary-group IDs/hash;
- `verifier_principal` with the same fields observed from the verifier process;
- separate `a1_boot_session_sha256` and
  `verifier_boot_session_sha256`;
- verifier PID, parent PID, process birth, executable path, executable
  SHA-256, and parent-chain hash;
- separate `a1_policy_context` and `verifier_policy_context`, each binding
  exact sandbox/network policy hashes and provider-native attachment records;
- provider-native sandbox and network attachment modes and handle hashes for
  the verifier;
- the same attachment facts for every `codesign` and `spctl` child;
- verifier cwd, environment-name hash, fixed locale, allowlisted environment
  values, and forbidden-credential absence;
- separate `a1_filesystem_view` and `verifier_filesystem_view`, each binding
  view identifier, mount source, filesystem type, volume identity, and
  mount-option hash;
- ancestor-symlink state and normalized path/vnode snapshot for every
  executable, bundle root, and signature resource used by verification.

For `in-a1-boundary`, the principal, boot, HOME/temp roots, policies, and
attachment records must equal the protected `runtime_boundary` and
`approved_environment`; `verifier_principal` must equal
`a1_runtime_principal`, and the A1/verifier boot, policy, and filesystem-view
records must be equal.

For `protected-external-verifier`, the context must equal a separately
protected external-boundary identity and include authenticated
`a1_object_mappings` for every observed A1 component, process birth, vnode,
bundle resource, and policy record. `verifier_principal` must equal the
protected external principal while `a1_runtime_principal` continues to bind the
mapped A1 objects. A1 and verifier boot, policy, and filesystem-view records may
differ, but every difference and mapping must be independently authenticated.
Creator ratification without those exact closed-schema mappings is
insufficient.

`verification_context_sha256` must be the canonical SHA-256 of that complete
context object. Every component and command result must repeat the same value.

The protected sanitized subprocess environment must bind exact values,
including at minimum:

```text
HOME=/var/empty
XDG_CONFIG_HOME=/var/empty
PATH=/usr/bin:/bin:/usr/sbin:/sbin
LANG=C
LC_ALL=C
```

Any approved temp-root variables must equal protected boundary values. Duplicate
or unexpected variables, `DYLD_*` injection, and forbidden credential
variables fail closed.

### Components

For a successful attempt, `components` must contain exactly:

1. `client`;
2. `relay`;
3. `editor`.

Each component must bind:

- role;
- executable path, SHA-256, device, inode, size, mode, owner, and modification
  time before and after verification;
- complete before/after process snapshots containing PID, parent PID, UID,
  start time, cwd, normalized argv hash, environment-name hash, allowlisted
  environment values, forbidden-variable absence, sandbox/network attachment
  handles, and the existing process-birth hash;
- ordered parent-chain records and a canonical parent-chain hash for each
  component; no common parent is assumed;
- app-bundle path and signature-resource paths when applicable;
- closed bundle/signature-resource tree manifest and digest, including file
  bytes, resource forks, extended-attribute names/value digests, ACL digest,
  file flags, ownership, modes, symlinks, and
  `_CodeSignature/CodeResources`;
- a non-empty `targets` list separating executable and app-bundle records;
  every target has its own expected and observed Identifier, TeamIdentifier,
  CDHash, designated-requirement hash, authorities hash, target kind, and exact
  ordered command records;
- the shared `verification_context_sha256`.

Target command records are the sole canonical command representation.
Component-level summaries may contain only hashes/references to those target
records, never duplicate normalized command objects.

The before/after records must prove stable target and process facts. They are
not sufficient by themselves to exclude a transient pathname swap while a
system tool opens the target. The selected provider must additionally supply
either:

- provider-native open/vnode/resource audit records binding each signing-tool
  child to the exact objects it read; or
- a proven immutable/read-only mount or write-denial boundary covering the
  complete component and bundle trees from authenticated `attempt-start`
  through activation-receipt sealing, including independent proof that no
  writer process could mutate them and a final snapshot immediately before the
  receipt.

### Command results

Every command result must retain:

- sequence;
- purpose;
- absolute system-tool path, SHA-256, device/inode, owner, mode, signing
  identity, and OS-build binding before and after execution;
- exact argv;
- child PID, parent PID, UID, and process-birth hash;
- observed child cwd, environment-name hash, exact allowlisted environment
  values, duplicate-name list, unexpected-name list, `DYLD_*` presence, and
  forbidden-credential presence;
- start and finish timestamps;
- protected timeout seconds;
- termination kind: `exit`, `signal`, `timeout`, or `launch-error`;
- integer exit status only for `exit`, signal number only for `signal`,
  `timed_out: true` only for `timeout`, and a retained launch-error record only
  for `launch-error`;
- raw stdout and stderr encoded as canonical base64, plus decoded byte lengths
  and SHA-256 values computed over decoded bytes;
- sandbox/network attachment handles and policy hashes for the child;
- target path/vnode/SHA-256;
- the shared `verification_context_sha256`;
- a deterministic outcome classification.

The canonical JSON/hash algorithm must match the foundation validator:
UTF-8 JSON with `ensure_ascii=false`, lexicographically sorted keys, and
separators `,` and `:` with no extra whitespace. Record hashes exclude only
their own hash field. Validators must decode base64, recompute byte lengths and
SHA-256 values over decoded bytes, parse retained outputs, and recompute every
classification.

`capture_payload_sha256` is computed over the complete payload bytes and is
stored only in the completion record and mirror. It is never embedded in the
payload itself.

Required commands:

- `/usr/bin/codesign --verify --strict --verbose=4` for every executable and
  applicable app bundle;
- `/usr/bin/codesign -d --verbose=4` for every target used to derive the
  displayed signing tuple;
- `/usr/bin/codesign -d --verbose=4 -r-` for every target used to derive the
  designated requirement;
- `/usr/sbin/spctl --assess --type execute --verbose=4` for app bundles.

The protected boundary must declare expected path, SHA-256, signing identity,
root ownership, non-writable mode, and OS build for `codesign` and `spctl`.
Observed tool facts must equal those protected expectations.

Apple sealed-system platform tools require a dedicated
`platform_binary_identity`, not the app-oriented `codeIdentity` shape. It must
bind the Mach-O platform identifier, Identifier, CDHash and full digest,
designated-requirement hash, sealed-system-volume identity, vnode, OS build, and
expected `TeamIdentifier`/Authority absence or values. A missing ten-character
TeamIdentifier is valid only when the protected platform-tool identity expects
it.

A nested command-line client may return `the code is valid but does not seem to
be an app` when assessed directly with `spctl`. That result is
`valid-non-app-rejection`, not an accepted app-bundle assessment and not a
strict-signature failure. The enclosing app bundle must still pass its required
macOS execute-policy assessment.

Direct `spctl` assessment of a nested non-app executable is forbidden by
default. It is permitted only when the protected authority object explicitly
enumerates it in that target's exact command sequence and expected
`valid-non-app-rejection` classification; otherwise it is an unexpected
command.

For app bundles, the allowed macOS execute-policy outcome is exactly an
exit-zero assessment whose retained output classifies to
`source=Notarized Developer ID`, unless a later protected decision names a
different exact source. User/local overrides, disabled assessment, internal
errors, unrecognized sources, and missing policy state are ineligible. Bind the
relevant quarantine/provenance xattrs, assessment-policy state, and override
state. This document uses `macOS execute-policy assessment` for that OS control;
it is unrelated to the foundation's trusted governance gatekeeper.

## Cross-bindings

The validator must require:

- verifier principal and boot identity match the selected protected route;
- authority receipt and challenge match the protected authority object, and
  the authority receipt predates `started_at`;
- every component's full before/after process snapshot and parent-chain record
  match the same activation session and MCP live capture;
- policy-attachment evidence includes the verifier and every system-tool child,
  not only client, relay, and Editor;
- target paths, vnodes, hashes, and signing tuples match the boundary manifest;
- app-bundle paths and signature resources are observed through the same
  filesystem view;
- all component and command records share one
  `verification_context_sha256`;
- every normalized fact is traceable to retained provider-native bytes;
- system-tool identities match protected expected identities;
- every command occurs in one boot and capture interval, after the target
  process birth and policy attachment, no more than 15 minutes before the
  activation receipt; the code-identity command interval may not exceed five
  minutes, and the activation session must remain within its existing
  five-minute receipt window;
- provider monitoring or independent write denial continuously covers component
  trees, system tools, policy/override state, and filesystem views from
  authenticated `attempt-start` through receipt sealing, with a final snapshot
  immediately before the receipt;
- every command has the protected timeout and signal outcome, with no duplicate,
  missing, reordered, or unexpected invocation;
- every child cwd/environment observation equals the protected sanitized
  subprocess environment;
- the authoritative external/provider ledger proves a complete monotonic,
  hash-chained attempt census with paired start/completion records; the
  repository mirror matches the start record, payload, and completion record;
  and the boundary references exactly one eligible successful attempt;
- start-record, payload, completion-record, mirror, and retained provider
  artifact SHA-256/byte-size facts are bound into the activation evidence
  manifest and activation receipt.

The current `strict_verified` boolean in `validate_wp0001_mcp_live.py` must not
independently satisfy activation after this successor becomes binding.

## Fail-closed cases

Reject activation if any of these occurs:

- no protected verification route is selected;
- a bare host diagnostic is supplied;
- provider identity or collector source is missing, mutable, or unauthorized;
- authority receipt does not predate capture, authority hash/challenge differs,
  or provider records omit the predecessor binding;
- provider-native bytes, byte ranges, authentication, or parser bindings are
  missing;
- verifier UID, ancestry, process birth, or policy attachment differs;
- observed verifier real/effective UID, GID, groups, or principal differs from
  the selected route;
- route-specific context fields or authenticated A1 object mappings are
  incomplete or mismatched;
- a `codesign` or `spctl` child lacks exact process and policy binding;
- child cwd or sanitized environment differs, or duplicate/unexpected,
  `DYLD_*`, or credential variables are present;
- a system-tool path, hash, vnode, ownership, mode, signing identity, or OS
  build differs from protected expectations;
- filesystem/mount view differs across components or commands;
- neither exact open/vnode audit nor complete immutability/write-denial proof is
  present;
- monitoring/write denial ends before receipt sealing or the final pre-receipt
  snapshot is absent;
- bundle tree, resource fork, xattr, ACL, file-flag, or signature-resource
  digest differs;
- a target vnode, hash, process birth, or signing tuple changes during capture;
- raw stdout/stderr is absent, lossy, reordered, or only summarized;
- context hashes differ;
- an app bundle fails strict verification or the required macOS execute-policy
  assessment;
- macOS execute-policy source is not the protected accepted source, an override
  is active, assessment is disabled, or policy/quarantine facts are missing;
- a nested non-app execute-policy result is misclassified as an accepted app;
- the provider is self-attesting;
- a failed or aborted attempt is overwritten or omitted, the protected ledger
  writer is not independent of candidate authority, sequence/digest chaining
  breaks, or the completeness checkpoint is missing;
- an attempt begins before its authenticated external `attempt-start` record;
- an attempt lacks a completion record, or its completion record does not bind
  the start-record digest, capture-payload digest, status, finish time, and
  completeness checkpoint;
- payload, completion record, or mirror introduces a circular/self-referential
  hash projection, or the mirror's three hashes differ;
- a status-specific record fabricates not-reached fields or omits its required
  minimal failure/abort envelope;
- secret-bearing raw bytes enter Git, or the quarantine/redaction/hash/incident
  record is incomplete;
- timeout, signal, duplicate, unexpected-command, or provider-attestation
  mismatch occurs;
- capture is incomplete, ambiguous, outside the exact five/fifteen-minute
  windows, or contains secret material.

## Machine-enforcement work

A protected schema-v5 successor must:

1. add `code_identity_context` plus the protected
   `code_identity_context_authority` object and exact receipt claim;
2. add a route-specific `oneOf`, protected external-boundary mapping schema,
   expected app and Apple platform signing-tool identities, status-specific
   attempt branches, and provider-native artifact references;
3. add immutable attempt paths, the attempt manifest, the selected normalized
   payload, retained completion record, and all provider-native artifacts to
   activation-evidence requirements;
4. bind the protected predecessor authority receipt/challenge, authoritative
   external/provider two-phase attempt ledger, completeness checkpoint, and
   secret-bearing failure quarantine/redaction contract;
5. extend policy-attachment subjects to the verifier and signing-tool children;
6. implement `validate_wp0001_code_identity_context_capture()` in
   `validate_foundation.py`;
7. cross-bind the artifact to boundary, session, MCP-live, and evidence-manifest
   facts;
8. add positive fixtures plus negative tests for every fail-closed case;
9. update the creator runbook and protected receipt claims;
10. authorize exact provider, parser, collector, and system-tool identities only
   after independent review.

No pass-producing collector should be implemented before the protected
provider choice and schema successor are ratified.

## Current disposition

Keep A0. Keep WP-0001 blocked. Keep the protected repo MCP entry disabled. Keep
Unity tool calls at zero.

Recommended next protected decision: select the verification route and
independent provider, then ratify the schema/collector successor as one
content-addressed transaction.

This successor closes only the on-disk verifier-context evidence gap. It does
not prove kernel-loaded-image identity or in-memory/cloud-managed Codex
requirements, which remain separate blockers in the creator runbook.
