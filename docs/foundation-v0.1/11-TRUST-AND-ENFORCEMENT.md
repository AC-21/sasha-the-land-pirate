# Trust and Enforcement Boundary

Version: 0.1 draft\
Current autonomy: **A0 — observe/design only**\
Current enforcement status: **specification, not a trusted control plane**

## 1. Threat model

Markdown cannot constrain an agent that has the same filesystem and Git credentials as the integrator. Such an agent can edit the constitution, schema, validator, receipt, test, or ledger and then report success. Hashes stored beside the files detect accidental drift only if a trusted party controls the accepted hash; they do not create authority by themselves.

Therefore:

- this foundation defines the laws and required machinery;
- the current local validator is **bootstrap lint**, not a security or release gate;
- no agent may merge, promote a flag, rewrite a save, or release a build autonomously at A0/A1;
- A2 or higher is forbidden until a gatekeeper outside ordinary agent credentials enforces the laws.

Direct creator prompt or clarification facts may be recorded as semantically ratified at A0 with an unsealed source capture; D-0001 through D-0005 and D-0036 through D-0037 are in that state. They are not protected authority for an entry gate until a creator-controlled receipt binds the exact source, active decision event, accepted artifact hashes, and commit. Any post-bootstrap artifact ratification or constitutional supersession requires that protected receipt rather than another editable local capture.

## 2. Required trust boundary

Before A2, the production repository needs:

1. protected `main` and protected governance paths;
2. agents restricted to standalone disposable clones/sandboxes with independent `.git` directories and quarantine packages without merge/release credentials, except for the separately ratified and activated WP-0003 local-development lane described below;
3. a trusted integrator/gatekeeper identity that alone can allocate accepted decision sequence numbers, merge, write release events, and advance rollout state;
4. creator-controlled approval for constitution, autonomy, high-risk/save migration, art authority, and release exceptions;
5. pairwise-distinct trusted implementer, verifier, and integrator principals for every accepted A2–A4 packet, regardless of declared risk or change class;
6. signed or platform-authenticated receipts bound to exact commit and SHA-256 artifact hashes;
7. server-side/orchestrator-side checks that an agent cannot modify in the same candidate change;
8. immutable release artifacts and save backups outside the candidate sandbox.

Sibling agents under the same parent credential count as one principal unless the orchestration layer proves independent credentials and non-colluding approval.

### Principal-separation matrix

This table is authoritative wherever another document is ambiguous:

| Autonomy | Implementation | Verification | Acceptance/integration |
|---|---|---|---|
| A0 | analysis plus creator-requested documentation/control-plane edits only; no agent game/tool/asset implementation or installs; D-0050's receipt-bound creator-operated setup is outside agent authority | advisory review only | creator only; agent edits cannot authorize themselves |
| A1 | one principal may implement inside the exact activated packet boundary: normally a standalone disposable clone/sandbox; WP-0003 alone may use its protected durable-repository `agent/*` branch | another agent may provide advisory review, but shared credentials do not make it trusted verification | creator manually imports/rejects generic A1 output or merges/rejects WP-0003's protected PR; no agent acceptance or protected merge |
| A2–A4 | accepted packets only | every accepted packet has a verifier principal distinct from implementer | trusted integrator is distinct from both for every accepted packet; creator remains required where the authority matrix says so |

Governance work is never exempt. Pairwise role fields in a proposed A1 packet describe the future accepted record; they do not turn sibling agents into independent principals.

## 3. Canonical records required at bootstrap

| Registry | Purpose | Current state |
|---|---|---|
| Decision events + creator receipts | authority, supersession, constitution | draft JSONL exists; protected receipt/hash chain not implemented |
| Ratification state | exact entry gates and active autonomy | [`governance/ratification-state.json`](governance/ratification-state.json) |
| Work-packet events | proposal, approval, lease, implementation, verification, result | packet schema exists; trusted event store not implemented |
| Reservations | atomic domain/path/content-ID/build-runner leases with fencing tokens | required before parallel integration |
| Scenario definitions and run evidence | reproducible inputs, environment, oracle, hashes, metrics | draft immutable registry exists; protected acceptance/run store not implemented |
| Save/migration compatibility | read/write matrix, migrations, backups, recovery tool | envelope contract described; registry not implemented |
| Content IDs/tombstones | immutable definitions and retired-ID migrations | required with first content manifest |
| Asset/provenance | source, hashes, licenses, quarantine and art receipts | asset schema exists; protected approvals not implemented |
| Dependency/SBOM | exact engine/packages/tools/licenses/removal path | required in M1 |
| Feature flags | owner, default, stages, persistent effects, removal condition | required before first flagged feature |
| Release/observation events | build checksum, cohort, thresholds, rollback target | required before A3/A4 |
| Incidents/rollbacks | failure evidence, severity, recovery and regression fixture | required before autonomy promotion |

The absence of a required registry is a stop condition, not permission to encode the fact in a commit message.

## 4. Authority law

- `ratified` is valid only with creator source authority or a creator receipt bound to the accepted event and commit.
- `rejected` is valid only with authority sufficient for that decision class; design recommendations remain provisional exclusions.
- An agent proposes with a UUID/ULID. The trusted integrator assigns human display sequence at acceptance; parallel agents never self-allocate the next canonical number.
- A design or technical recommendation cannot supersede any creator-authority record. It may open a separately identified proposal for creator review, but it does not become the active head.
- A constitutional record that replaces a creator-authority record must use exactly `creator-ratification` authority and name a protected creator receipt bound to the successor's exact canonical event hash and accepted commit. A wrong-authority, missing, unknown, unsealed, subject-mismatched, or event-hash-mismatched receipt makes the supersession invalid.
- A decision has one active supersession head. Forks stop for creator resolution.
- The human ledger is a generated/read-only view of canonical events once the gatekeeper exists.

The positive and deliberately invalid machine fixtures for this law are [`governance/fixtures/supersession-authority.valid.json`](governance/fixtures/supersession-authority.valid.json) and [`governance/fixtures/supersession-authority.invalid.json`](governance/fixtures/supersession-authority.invalid.json). Bootstrap validation must evaluate every case; the protected gatekeeper must repeat the negative cases outside candidate write authority.

## 5. Derived risk and role separation

The gatekeeper calculates `effective risk = max(declared risk, diff-derived risk)`. These surfaces automatically derive high risk:

- constitution, governance, validators, gates, CI, autonomy, credentials;
- save schemas, migrations, content IDs, random algorithms, identity/ownership;
- releases, feature flags, rollback, external dependencies;
- global economy rules and faction doctrine;
- executable or script-bearing asset inputs;
- security/privacy/network behavior.

Only a creator receipt may lower derived risk. At A2–A4, implementer, verifier, and integrator are pairwise distinct for every accepted packet, including low/medium work; high/constitutional work additionally requires the class-specific creator authority above. At A1, no shared-credential review can accept or integrate output.

## 6. Save-safe rollout boundary

- S2 and S3 writers are human-gated at every autonomy level in this draft.
- Canaries use cloned profiles, never the sole live slot.
- Every release declares a read/write compatibility matrix, exact rollback build, migration direction, backup retention, recovery-tool version, and whether the old build can read forward saves.
- Automatic rollout requires either dual-read/write compatibility, a tested downgrade migration, or guaranteed restoration of an immutable pre-migration generation.
- If rollback restores an older save generation, possible loss of post-migration progress must be explicit; an agent cannot silently call that “successful rollback.”
- Disabling a feature flag must account for persistent data already written by the feature.

## 7. Gatekeeper proof before A2

The trusted gatekeeper is accepted only after tests deliberately attempt to:

- forge creator ratification;
- rewrite an accepted decision;
- make a design/technical recommendation the active supersession head of a creator-authority record;
- replace a creator constitutional record without a protected creator receipt bound to the successor;
- lower declared risk below diff-derived risk;
- collapse any two of implementer, verifier, or integrator onto the same principal for an accepted A2–A4 packet;
- submit a released packet without evidence/approval;
- touch undeclared paths or expired reservations;
- accept an asset with missing provenance or failing art clearance;
- promote a build that fails one precommitted metric;
- roll an old build onto an unreadable forward save;
- edit the gatekeeper rules in the same candidate change;
- hide an aborted/failed packet from promotion history.

Every attack must be rejected by a trusted check outside the candidate's write authority. A deliberate canary failure must then prove rollback and incident capture.

## 8. Current entry gates

[`governance/ratification-state.json`](governance/ratification-state.json) is the single draft statement of what can start:

- WP-0001 remains accepted at its immutable contract, while owner-authenticated D-0051 and packet route-successor receipts protect `UNITY-MCP-EXTERNAL`; D-0050 permits only creator-operated candidate setup, and A1 still requires the protected empty seed, exact seat/project/D-0047 tuple, actual candidate quarantine, direct-MCP process/socket/config/allowlist profile, revoked zero-tool preflight, and distinct fresh live activation session to be physically verified and separately activated;
- WP-0003 is an accepted parallel local-development packet under protected ratified D-0052. It remains inert until another distinct creator receipt activates its compact local boundary;
- the ugly gameplay toy requires the listed identity decisions, its own explicit acceptance, and a separate packet-specific WP-0002 quarantine receipt;
- WP-0002 cannot advance from proposal until WP-0001 is `released` and a sealed creator `packet-completion` receipt binds `ACCEPT-COMPLETION-WP-0001` plus WP-0001's immutable packet-contract hash;
- the slice kernel additionally requires the city grammar decision;
- production content and autonomous integration have later, stricter gates.

Repository creation alone never promotes autonomy. `packet_entry_gates` in the canonical state maps each executable packet to exactly one gate; WP-0001 maps to `technical_spike`, WP-0002 maps to `ugly_gameplay_toy`, and WP-0003 maps to `local_development`. The canonical `a1_max_active_packets` value is `1`, so no second A1 packet may start until the active packet has ended and its status is recorded.

Unity Terms §17.2(ff) remains an independent hard stop. D-0051 selects the documented external-MCP Authorized Agentic Access profile: Codex connects through Unity's MCP Bridge and the exact Unity-installed relay, while no agent or CI credential may directly start Unity Hub, Editor, executable, CLI, or batchmode. Global MCP configuration is outside the approved boundary. Any activated packet must bind an explicit project target through its own schema; WP-0001 uses the standalone quarantine, while accepted but inactive WP-0003 uses its conditional first-use local boundary. Creator permission to proceed cannot waive third-party terms or substitute for the packet's required entitlement, connection, and boundary evidence.

One read-only `Unity_ReadConsole` smoke call occurred before D-0051 and before A1 activation. It returned no console entries and produced no known project mutation, but it crossed WP-0001's zero-tool-call setup boundary. The event is retained as a control-plane deviation, cannot be treated as the required handshake or activation evidence, and does not authorize another Unity call. A clean creator-operated connection cycle with zero tool invocations is still required before the separate activation receipt.

### A1 quarantine minimum

The generic quarantine below governs every A1 packet except an activated
WP-0003. WP-0003's creator-ratified local-development exception is defined by
[`15-LEAN-A1-LOCAL-DEVELOPMENT.md`](15-LEAN-A1-LOCAL-DEVELOPMENT.md) and
[`schemas/local-a1-boundary.schema.json`](schemas/local-a1-boundary.schema.json).
It may use only a valid non-`main` `agent/*` branch whose real checkpoint exists
in protected-`main` ancestry. Its protected activation receipt binds the exact
contract, foundation state, reservation, allowed/denied actions, and local
boundary bytes. Repository/bootstrap work may then begin, but Unity MCP remains
closed until the creator-opened exact `Game` project, licensed Editor, running
Bridge, approved Codex client, exact target, and requested action all satisfy
the first-use gate. This exception grants no direct Unity invocation,
installation, credential, account, publishing, release, or self-merge
authority.

For every other A1 packet, before any command runs, a creator-controlled process must materialize `governance/a1-boundaries/<packet-id>.json` under [`schemas/a1-boundary-manifest.schema.json`](schemas/a1-boundary-manifest.schema.json). The packet stores its safe path and raw SHA-256; the one `packet-activation` receipt must bind that exact hash, the packet contract, and all of the following:

1. a standalone disposable clone with an independent `.git` directory plus a dedicated ephemeral OS user or equivalent hash-bound sandbox, pinned to an exact approved base commit; shared Git worktrees and ambient-user execution are forbidden;
2. no writable mount or credential capable of changing protected `main`, governance, receipts, or releases;
3. the foundation mounted read-only or copied as a hash-checked input, never edited as spike output;
4. only the exact reserved packet-declared outputs, manifest-listed repository scratch, and isolated runtime HOME/private-temp roots writable, with no network or external credentials except the exact hash-bound policy permits;
5. an exact approved toolchain and environment tuple, plus the command log, complete diff, and artifact manifest required for manual review;
6. a creator-operated diff/import boundary—A1 agents cannot merge, accept their own evidence, or advance autonomy.

The manifest also binds the current constitution and decision-ledger hashes, the last sealed creator receipt, exact lease/fence/expiry/paths/domains, read-only protected paths, denied merge/release/governance/receipt credentials, and any named local-observation baseline exceptions. Scratch is boundary infrastructure, not packet output: it is never included in the reservation or creator import, must be disjoint from every protected/reserved path, and is destroyed when the quarantine closes. WP-0001 permits exactly `Game/Library/`, `Game/Temp/`, `Game/Logs/`, `Game/Obj/`, `Game/UserSettings/`, `Game/MemoryCaptures/`, and `Game/Recordings/` as repository-ignored scratch; build evidence goes directly to declared `BuildArtifacts/WP-0001/` paths rather than `Game/Builds/`.

Mutable Unity, relay, licensing, package-cache, log, and MCP connection state outside the repository must resolve only inside an exact disposable runtime HOME and private temp namespace. The manifest binds their absolute roots, runtime principal/UID, environment-variable bindings, creator-home/shared-temp default-denial roots, exact packet-required shared-temp exceptions, symlink-free/no-escape attestation, destroy-on-close and non-importability, plus raw SHA-256 hashes of the exact sandbox and network policies. WP-0001 permits only the exact resolved `/private/tmp/unity-mcp-<project-hash>-<editor-pid>` socket exception, with matching owner UID and mode `0600`. The runtime HOME/private temp are not reservation paths or packet evidence; any retained evidence must first be copied to a declared output and recorded. An active baseline may never retain a `pending://` URI. A non-content-addressed baseline is allowed only as an explicit manifest-attested evidence ID with a concrete `local-observation://` URI.

WP-0001 schema v4 also requires a creator-created protected setup commit with
no implementation, embedded package tree, local/Git/file dependency, or
non-empty Assets content; the complete Unity-registry package graph and exact
D-0047 profile are content-addressed. Direct MCP activation binds sanitized
client-visible tool names, their canonical digest, and the creator claim
`AUTHORIZE-WP0001-MCP-ALLOWLIST`; arbitrary C# execution, package mutation, and
external-model import tools are excluded. A disconnected/revoked zero-tool
preflight is followed by a distinct connected activation session with exact
PIDs, process starts, boot-session-bound birth IDs, config/allowlist hashes,
isolated Codex/XDG/Git environment, effective-server-inventory hash, and
connection/session hashes. Fixed raw protocol/listener/probe captures and the
read-only live route collector must agree. The route capture proves the
Editor/relay Unix FD peer plus reciprocal client/relay stdin/stdout pipes and
derives the session hash from that FD graph. The raw captures must name exact
repository collector paths, fixed invocations, and source hashes authorized by
`AUTHORIZE-WP0001-RAW-COLLECTORS`; copied summaries or unbound collector code
cannot substitute. Network probes use exact policy-bound targets, and a third
collector binds sandbox/network attachment to each process birth identity.
The creator receipt must follow that capture within five minutes. Disconnect
or collector/config/process drift requires a new receipt.

The same packet-specific receipt binds the packet activation claim plus its exact boundary claim: `A1-QUARANTINE-BOUNDARY-VERIFIED` for WP-0001/WP-0002 or `A1-LOCAL-BOUNDARY-VERIFIED` for WP-0003. WP-0001's receipt additionally carries `AUTHORIZE-WP0001-MCP-ALLOWLIST` for the exact digest-bound list in that same boundary. Combining these claims records one approval of one verified packet boundary; it does not invent another independent approval.
WP-0001 also carries `AUTHORIZE-WP0001-RAW-COLLECTORS` for the exact
content-addressed protocol, network, and policy-attachment collectors named by
that boundary.
It additionally carries `AUTHORIZE-WP0001-CODE-IDENTITIES` for the exact
strict-signature tuples of the client, relay, and Editor; internal equality
without that creator authority is not sufficient.

WP-0001's packet-acceptance receipt also binds `AUTHORIZE-TEMP-WP0001-IDENTITY` to the exact disposable company, product, bundle, and dev/test profile values inside the packet. This authorizes only its non-shipping spike namespace and does not resolve durable D-0038.

Under the generic boundary, if the executing credential can rewrite the trusted checkout or approval records, A1 quarantine has not been established. WP-0003 instead relies on protected `main`, required checks, creator-controlled merge, and distinct protected receipts: the agent may propose branch changes but cannot make them trusted state. A local folder named “sandbox” is never sufficient evidence.

The draft gate state stores the packet-to-gate mapping, exact one-packet A1 concurrency cap, value-sensitive decision constraints, and unbound packet-specific receipt requirements. Receipt claims are bound per subject, not stored in one ambiguous flat list. A decision gate resolves only when the bound receipt equals the active supersession head's `approval_receipt_id`, binds exactly one allowed value to that head, and satisfies any required kind, role, and resolver. Receipt requirements may additionally constrain receipt kind, issuer role, resolver type, and exact per-subject contract hash. WP-0003 requires pairwise-distinct decision-ratification, packet-acceptance, and packet-activation receipts even when one authenticated owner comment is their common source. A gate cannot become `ready` or `passed` while any required receipt ID is null, while a decision is not ratified, while its authenticated claim is outside `allowed_claims`, or while starting it would exceed `a1_max_active_packets`.

Before an A1 packet enters `active`, `verifying`, or `candidate`, its mapped gate must be `passed` and fully sealed/resolved; canonical `active_autonomy` must be `A1`; its reservation must be `held` with exact base commit, lease, fencing token, non-null expiry later than the activation event, paths, and domains; its full event chain must preserve the acceptance and sole activation receipts; and the one-packet cap must still hold. `verifying` and `candidate` additionally require actual paths plus immutable diff, artifact-manifest, and command-log evidence. The future protected event model must derive these fields; editable JSON remains descriptive at A0/A1.

The dependency-free bootstrap validator recursively enforces the JSON Schema keywords used by this pack, including local references, nested object/array constraints, unions, conditionals, formats, and uniqueness. It is intentionally not represented as the pinned full Draft 2020-12 validator required outside candidate write authority.
