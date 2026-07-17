# Sasha the Atomic Land Pirate — Foundation v0.1

Status: **constitution draft for review**\
Date: 2026-07-15\
Title: **Sasha the Atomic Land Pirate**\
Protagonist: **Sasha**

This is the foundation specification for a future protected control plane for an endearing, real-time, post-apocalyptic city-builder in which Sasha builds a human, humanoid-robot, or mixed colony, launches scavenging expeditions, upgrades road vehicles, grows manufacturing and physical-goods trade, and deals with persistent factions. Its visual north star is Texas iron × brutalist opera under tungsten-led light. It is not a conventional pitch deck, and it is not yet an enforceable autonomous system. It separates the game's durable identity from replaceable implementations so human and software agents can later work in parallel without slowly changing what game they are building.

## Read this first

1. [`00-GAME-CONSTITUTION.md`](00-GAME-CONSTITUTION.md) defines the player promise, core loop, product pillars, invariants, and non-goals.
2. [`01-DECISION-LEDGER.md`](01-DECISION-LEDGER.md) is the human-readable decision register. The append-only-by-policy draft representation lives in [`ledger/decisions.jsonl`](ledger/decisions.jsonl); protected canonical A1 authority exists only where a sealed receipt binds the exact protected tree, while the trusted A2 gatekeeper/event store does not yet exist.
3. [`02-SYSTEM-MAP.md`](02-SYSTEM-MAP.md) defines how city, road, vehicle, faction, people, crisis, and progression systems exchange state.
4. [`03-VERTICAL-SLICE.md`](03-VERTICAL-SLICE.md) defines the first proof, its hard cuts, and its acceptance tests.
5. [`04-TECHNICAL-ARCHITECTURE.md`](04-TECHNICAL-ARCHITECTURE.md) defines the replaceable technical architecture, save contract, data boundaries, and performance envelope.
6. [`05-ART-BIBLE.md`](05-ART-BIBLE.md) defines the visual grammar and the Blender/AI-assisted asset pipeline.
7. [`06-AGENT-OPERATING-MODEL.md`](06-AGENT-OPERATING-MODEL.md) defines what background agents may change and how work is proposed, tested, rolled out, and rolled back.
8. [`07-QUALITY-GATES.md`](07-QUALITY-GATES.md) defines proof gates for simulation, playability, art, performance, saving, and release.
9. [`08-BUILD-ROADMAP.md`](08-BUILD-ROADMAP.md) sequences proof, implementation, agent-loop promotion, and eventual production scope.
10. [`09-RATIFICATION-WORKSHEET.md`](09-RATIFICATION-WORKSHEET.md) reduces the creator review to explicit decisions before gameplay grayboxing and golden-art production.
11. [`10-FIRST-WORK-PACKET.md`](10-FIRST-WORK-PACKET.md) points to the separately gated technical spike and ugly gameplay toy.
12. [`11-TRUST-AND-ENFORCEMENT.md`](11-TRUST-AND-ENFORCEMENT.md) states the hostile-agent threat model and the protected gatekeeper required before autonomous integration.
13. [`12-FOUNDATION-AUDIT.md`](12-FOUNDATION-AUDIT.md) records what independent design, technical, and governance audits corrected—and what honestly remains blocked.
14. [`13-MANUFACTURING-AND-CARAVAN-EXCHANGE.md`](13-MANUFACTURING-AND-CARAVAN-EXCHANGE.md) defines the phased physical-stock economy, population milestone hypothesis, caravaner institution, conservation laws, and proof boundary.
15. [`14-CREATIVE-DIRECTION.md`](14-CREATIVE-DIRECTION.md) translates Texas iron × brutalist opera, tungsten over neon, reference roles, and comedy/story energy into original visual and narrative laws.
16. [`15-LEAN-A1-LOCAL-DEVELOPMENT.md`](15-LEAN-A1-LOCAL-DEVELOPMENT.md) defines the creator-ratified practical local-development successor that preserves Git/MCP safety while removing the disproportionate WP-0001 certification ceremony.

Machine-readable schemas, value-sensitive entry gates, proposed work packets, and hash-registered immutable scenarios sit beside these documents. [`schemas/a1-boundary-manifest.schema.json`](schemas/a1-boundary-manifest.schema.json) defines the exact creator-attested quarantine boundary required for any future A1 activation, including a strict distinction between reserved packet outputs, repository-ignored project scratch, and a separately isolated ephemeral runtime HOME/private temp for Unity and relay state. For WP-0001, schema v4 additionally binds the protected empty project seed, exact D-0047 profile, registry-only package graph, direct-MCP process/socket/config/allowlist profile, isolated Codex/XDG/Git environment, exact effective-server inventory, disconnected/revoked preflight, fresh live activation session, raw-source-backed evidence, and physical candidate record. [`tools/validate_wp0001_a1_live.py`](tools/validate_wp0001_a1_live.py) and [`tools/validate_wp0001_mcp_live.py`](tools/validate_wp0001_mcp_live.py) are non-Unity, read-only collectors for the physical quarantine and already-running route; strict raw event/listener/probe parsers reject blank restatements. [`tools/inspect_wp0001_toolchain_static.py`](tools/inspect_wp0001_toolchain_static.py) separately performs an A0-only, filesystem-only D-0047 diagnostic without starting Unity, Hub, Xcode, dotnet, or any other external process; [`schemas/wp0001-static-host-toolchain-observation.schema.json`](schemas/wp0001-static-host-toolchain-observation.schema.json) keeps that output structurally non-authoritative and incompatible with activation evidence. [`schemas/wp0001-a1-activation-evidence.schema.json`](schemas/wp0001-a1-activation-evidence.schema.json) and [`schemas/wp0001-a1-evidence-record.schema.json`](schemas/wp0001-a1-evidence-record.schema.json) define the activation evidence chain. All non-output state is destroy-on-close and non-importable. These are draft control-plane inputs, not evidence that the protected control plane already exists.

WP-0001's A0 activation preparation uses the creator-approved durable checkout `/Users/sasha/Documents/Codex/sasha-the-land-pirate` and remote `https://github.com/AC-21/sasha-the-land-pirate`. The original sealed receipts still accept the exact packet contract, temporary identity, repository, and creator-operated installation of the D-0047 candidate. Owner-authenticated `RR-D0051-20260716` and `RR-WP0001-ROUTE-20260716` protect D-0051's `UNITY-MCP-EXTERNAL` successor—Codex as external client through Unity's MCP Bridge and exact Unity-installed relay, not the API-key Gateway path—and bind it to the unchanged packet contract.

The machine-readable snapshot at `docs/evidence/WP-0001/pre-a1-readiness-20260716.json` records WP-0001 as **blocked** without secrets. The installed Editor/tool/package tuple is not D-0047; the observed project is not the packet project; an eligible Unity AI seat is unverified; the inherited client approval, 54-tool handshakes, non-loopback relay listener, earlier console call, and later direct Editor/Hub probes cannot satisfy the required clean connection cycle; and no standalone physical quarantine exists. The snapshot is A0 observation evidence only. WP-0001 remains accepted but inactive.

The protected Stage-C state is canonical **A1**, with WP-0002 as the sole active packet.
`WP-0003` completed its ratified D-0052/
`RR-WP0003-ACTIVATE-20260716` lifecycle and is released; its reservation is
released as well. `WP-0002` is creator-accepted and activated under
`RR-WP0002-ACTIVATE-20260717`; its exact Last Bearing reservation is held.
Implementation is authorized only within that reservation through protected
`agent/*` pull requests. Governance, production content, dependency expansion,
publishing, direct Unity invocation, and autonomy beyond A1 remain closed.

## Source-of-truth order

When two artifacts conflict, the higher item wins:

1. Authenticated creator receipts bound to the active ratified decision head
2. The exact constitutional artifact hash named by those receipts
3. Accepted system and feature contracts
4. Content and asset manifests
5. Automated tests and reference captures
6. Implementation
7. Generated documentation or summaries

At A1, the sealed activation receipt and exact packet/boundary hashes in the same protected tree control executable scope; status prose alone never does. Unsealed creator-source captures remain evidence of words, not protected authority. Any mismatch between a creator source/active decision and its constitutional materialization is a hard stop; precedence never authorizes an agent to silently rewrite either artifact.

## Status language

- **Ratified**: explicitly stated by the creator or explicitly approved later. An agent cannot alter it.
- **Provisional**: the recommended default; implementation may test it, but it is not yet constitutional.
- **Open**: a consequential choice that must not be hidden inside code or content.
- **Rejected**: intentionally outside the current game under sufficient explicit authority, with rationale preserved. A design recommendation alone is provisional.
- **Superseded**: replaced by a later record; never deleted from history.

## Change law

- Constitutional changes require creator ratification and a ledger entry.
- Architecture changes require an accepted ADR, migration/rollback plan, and passing gates.
- Feature work requires a bounded work packet and explicit acceptance tests.
- Every save-affecting change requires a migration decision and round-trip tests.
- Generated assets remain quarantined until provenance, visual, geometry, and runtime gates pass.
- Once sealed or used by accepted downstream work, ledger history is append-only. Corrections supersede prior entries; they do not rewrite them. Pre-seal bootstrap corrections regenerate the draft chain and its receipt hashes transparently.

## Ratification sequence

For gameplay, this draft should be ratified in three passes:

1. **Identity pass**: begin from the ratified title, protagonist, human/robot/mixed colony boundary, manufacturing/physical-goods boundary, and visual north star; then decide promise, tone layering, vehicle embodiment, travel topology, time, severity, faction role, combat role, composition-mechanics depth, product boundary, and city-grammar comparison authorization.
2. **Proof pass**: the vertical-slice scenario and hard scope cuts.
3. **Build pass**: engine, target hardware, repository location, and agent autonomy levels.

No production-scale content work begins before the identity and proof passes. The narrow technical spike and gameplay toy have different entry gates; [`governance/ratification-state.json`](governance/ratification-state.json) is the single draft authority for both.

The gameplay-neutral technical spike may be separately authorized before the identity/proof passes finish, but only after its repository, tool installation, packet, and physical A1 quarantine boundary are all receipt-bound.

## Validation

Run `python3 tools/validate_foundation.py` from this directory. It performs bootstrap lint for JSON/schema syntax; the recursively used schema subset; decision/event/receipt authority; sequential draft IDs; human/JSONL ledger agreement; decision references; local links; immutable packet contracts; lifecycle continuity; A1 boundary, dependency, and evidence rules; and registered scenario hashes/references. It is explicitly not the pinned full Draft 2020-12 validator, a protected approval system, or a release gate. The full validator and adversarial checks remain a WP-0001 exit gate; protected approvals, derived event state, and release gating are the explicit M1C gatekeeper workstream before A2.
