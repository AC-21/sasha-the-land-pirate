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
2. agents restricted to isolated branches/worktrees and quarantine packages without merge/release credentials;
3. a trusted integrator/gatekeeper identity that alone can allocate accepted decision sequence numbers, merge, write release events, and advance rollout state;
4. creator-controlled approval for constitution, autonomy, high-risk/save migration, art authority, and release exceptions;
5. pairwise-distinct trusted implementer, verifier, and integrator principals for every accepted A2–A4 packet, regardless of declared risk or change class;
6. signed or platform-authenticated receipts bound to exact commit and SHA-256 artifact hashes;
7. server-side/orchestrator-side checks that an agent cannot modify in the same candidate change;
8. immutable release artifacts and save backups outside the candidate worktree.

Sibling agents under the same parent credential count as one principal unless the orchestration layer proves independent credentials and non-colluding approval.

### Principal-separation matrix

This table is authoritative wherever another document is ambiguous:

| Autonomy | Implementation | Verification | Acceptance/integration |
|---|---|---|---|
| A0 | analysis plus creator-requested draft documentation/control-plane edits only; no game/tool/asset implementation or installs | advisory review only | creator only; draft edits cannot authorize themselves |
| A1 | one sandbox principal may implement in an isolated disposable worktree | another agent may provide advisory review, but shared credentials do not make it trusted verification | creator manually inspects the diff/artifacts and imports or rejects them; no agent acceptance or merge |
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

- a narrow technical spike can enter A1 only after WP-0001, D-0048's Unity operating boundary, the exact D-0047 tool installation, and WP-0001's packet-specific quarantine are explicitly approved; an unsealed repository/CI greenlight is evidence, not protected packet acceptance;
- the ugly gameplay toy requires the listed identity decisions, its own explicit acceptance, and a separate packet-specific WP-0002 quarantine receipt;
- WP-0002 cannot advance from proposal until WP-0001 is `released` and a sealed creator `packet-completion` receipt binds `ACCEPT-COMPLETION-WP-0001` plus WP-0001's immutable packet-contract hash;
- the slice kernel additionally requires the city grammar decision;
- production content and autonomous integration have later, stricter gates.

Repository creation alone never promotes autonomy. `packet_entry_gates` in the canonical state maps each executable packet to exactly one gate; WP-0001 maps to `technical_spike` and WP-0002 maps to `ugly_gameplay_toy`. The canonical `a1_max_active_packets` value is `1`, so no second A1 packet may start until the active packet has ended and its status is recorded.

Unity Terms §17.2(ff) is an independent hard stop. Until D-0048 is protected, no agent or CI credential may start Unity Hub, Editor, or CLI. The compliant branches are documented Unity-granted Authorized Agentic Access for the exact identity/runner/use; a human initiating every Unity action while agents only prepare inputs and analyze outputs; or a Godot fallback that supersedes/revises WP-0001. Creator permission to create CI or proceed at A0 cannot waive third-party terms.

### A1 quarantine minimum

Before any A1 packet command runs, a creator-controlled process must materialize `governance/a1-boundaries/<packet-id>.json` under [`schemas/a1-boundary-manifest.schema.json`](schemas/a1-boundary-manifest.schema.json). The packet stores its safe path and raw SHA-256; the one `packet-activation` receipt must bind that exact hash, the packet contract, and all of the following:

1. a disposable branch/worktree or standalone sandbox pinned to an exact approved base commit;
2. no writable mount or credential capable of changing protected `main`, governance, receipts, or releases;
3. the foundation mounted read-only or copied as a hash-checked input, never edited as spike output;
4. only packet-declared output paths writable, with no network or external credentials except explicitly approved installers;
5. an exact approved toolchain and environment tuple, plus the command log, complete diff, and artifact manifest required for manual review;
6. a creator-operated diff/import boundary—A1 agents cannot merge, accept their own evidence, or advance autonomy.

The manifest also binds the current constitution and decision-ledger hashes, the last sealed creator receipt, exact lease/fence/expiry/paths/domains, read-only protected paths, denied merge/release/governance/receipt credentials, and any named local-observation baseline exceptions. An active baseline may never retain a `pending://` URI. A non-content-addressed baseline is allowed only as an explicit manifest-attested evidence ID with a concrete `local-observation://` URI.

The same packet-specific receipt binds the physical boundary claim `A1-QUARANTINE-BOUNDARY-VERIFIED` and the packet activation claim (`ACTIVATE-A1-WP-0001` or `ACTIVATE-A1-WP-0002`). Combining those claims records one approval of one verified boundary; it does not invent a fifth independent approval.

WP-0001's packet-acceptance receipt also binds `AUTHORIZE-TEMP-WP0001-IDENTITY` to the exact disposable company, product, bundle, and dev/test profile values inside the packet. This authorizes only its non-shipping spike namespace and does not resolve durable D-0038.

If the executing credential can also rewrite the trusted checkout or approval records, A1 quarantine has not been established. A local folder named “sandbox” is not sufficient evidence.

The draft gate state stores the packet-to-gate mapping, exact one-packet A1 concurrency cap, value-sensitive decision constraints, and unbound packet-specific receipt requirements. Receipt claims are bound per subject, not stored in one ambiguous flat list. A decision gate resolves only when the bound receipt equals the active supersession head's `approval_receipt_id` and binds exactly one allowed value to that head. A gate cannot become `ready` or `passed` while any required receipt ID is null, while a decision is not ratified, while its authenticated claim is outside `allowed_claims`, or while starting it would exceed `a1_max_active_packets`.

Before an A1 packet enters `active`, `verifying`, or `candidate`, its mapped gate must be `passed` and fully sealed/resolved; canonical `active_autonomy` must be `A1`; its reservation must be `held` with exact base commit, lease, fencing token, non-null expiry later than the activation event, paths, and domains; its full event chain must preserve the acceptance and sole activation receipts; and the one-packet cap must still hold. `verifying` and `candidate` additionally require actual paths plus immutable diff, artifact-manifest, and command-log evidence. The future protected event model must derive these fields; editable JSON remains descriptive at A0/A1.

The dependency-free bootstrap validator recursively enforces the JSON Schema keywords used by this pack, including local references, nested object/array constraints, unions, conditionals, formats, and uniqueness. It is intentionally not represented as the pinned full Draft 2020-12 validator required outside candidate write authority.
