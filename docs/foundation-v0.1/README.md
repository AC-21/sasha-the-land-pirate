# Sasha the Atomic Land Pirate — Foundation v0.1

Status: **constitution draft for review**\
Date: 2026-07-15\
Title: **Sasha the Atomic Land Pirate**\
Protagonist: **Sasha**

This is the foundation specification for a future protected control plane for an endearing, real-time, post-apocalyptic city-builder in which Sasha builds a human, humanoid-robot, or mixed colony, launches scavenging expeditions, upgrades road vehicles, grows manufacturing and physical-goods trade, and deals with persistent factions. Its visual north star is Texas iron × brutalist opera under tungsten-led light. It is not a conventional pitch deck, and it is not yet an enforceable autonomous system. It separates the game's durable identity from replaceable implementations so human and software agents can later work in parallel without slowly changing what game they are building.

## Read this first

1. [`00-GAME-CONSTITUTION.md`](00-GAME-CONSTITUTION.md) defines the player promise, core loop, product pillars, invariants, and non-goals.
2. [`01-DECISION-LEDGER.md`](01-DECISION-LEDGER.md) is the human-readable decision register. The append-only-by-policy draft representation lives in [`ledger/decisions.jsonl`](ledger/decisions.jsonl); protected canonical authority does not exist yet.
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

Machine-readable schemas, value-sensitive entry gates, proposed work packets, and hash-registered immutable scenarios sit beside these documents. [`schemas/a1-boundary-manifest.schema.json`](schemas/a1-boundary-manifest.schema.json) defines the exact creator-attested quarantine boundary required for any future A1 activation. They are draft control-plane inputs, not evidence that the protected control plane already exists.

Current A0 activation preparation uses the creator-approved durable checkout `/Users/sasha/Documents/Codex/sasha-the-land-pirate` and remote `https://github.com/AC-21/sasha-the-land-pirate`. Foundation run `29465142421` passed for commit `a07411199c5ab4600cfcce60fb8e4e9e4daea9f1`. `RR-CREATOR-20260715-04` preserves that greenlight but is unsealed; it authorizes A0 preparation for the WP-0001 transition but does not protected-accept the amended packet, approve Unity installation, choose D-0048's legal operating path, establish the A1 boundary, or promote autonomy.

## Source-of-truth order

When two artifacts conflict, the higher item wins:

1. Authenticated creator receipts bound to the active ratified decision head
2. The exact constitutional artifact hash named by those receipts
3. Accepted system and feature contracts
4. Content and asset manifests
5. Automated tests and reference captures
6. Implementation
7. Generated documentation or summaries

At A0, unsealed creator-source captures are evidence of the creator's words but are not yet protected authority. Any mismatch between a creator source/active decision and its constitutional materialization is a hard stop; precedence never authorizes an agent to silently rewrite either artifact. The agent opens a change proposal instead of choosing whichever source is convenient.

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
