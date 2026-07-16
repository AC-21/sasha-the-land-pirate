# Sasha the Atomic Land Pirate — Agent Contract

This file is the practical front door for every coding, art, QA, and planning agent in this repository. Read it before acting. More specific `AGENTS.md` files may add local rules but cannot weaken this one.

## Mission

Build an endearing, real-time, post-apocalyptic city-builder and resource-management game in which Sasha turns wasteland salvage into a home worth returning to.

Ratified identity:

- The title is **Sasha the Atomic Land Pirate**; Sasha is the protagonist.
- Colonies may be human-only, humanoid-utility-robot-only, or mixed.
- The game includes city building, scavenging, manufacturing, physical-goods trading, upgradeable travel vehicles, distinct factions, real-time play, no zombies, durable save/load, and strong art direction. “Stock” never means company shares; after an authored aggregate world-population threshold, caravaners open/administer the physical-goods exchange. Exact market law remains D-0044.
- The visual north star is **Texas iron × brutalist opera**, with **tungsten over neon**; named references are functional coordinates, never copying authority.
- It must perform well on the creator's MacBook Pro and eventually support safe background-agent work.

Original robot design and exclusion of Tesla names, marks, silhouettes, surfaces, and trade dress are production/IP safeguards, not inferred lore. Whether the three colony compositions require distinct capabilities and dependencies is the open D-0039 composition-mechanics decision.

The current thesis, pillars, loop, “storybook salvage” phrase, mechanics, lore, and technical choices remain provisional wherever the foundation says so. Do not promote recommendations into facts.

## Current authority: A0

This repository is currently **documentation/control-plane work only**.

- Unity Hub 3.19.5, Rosetta 2, and Unity Editor 6000.5.4f1 ARM64 are present locally, and the creator reports an active Unity license. This is not the creator-approved WP-0001 D-0047 candidate: 6000.3.19f1 ARM64 + Mac Build Support (IL2CPP), Xcode 26.3, URP 17.3, and Test Framework 1.6; full Xcode and the standalone .NET SDK were not detected. The 6000.5 onboarding project is observation evidence, not Sasha's project or a production-version choice.
- D-0051 supersedes D-0049 and selects `UNITY-MCP-EXTERNAL`: Codex is the external MCP client, and Unity interaction may travel only through the receipt-bound Unity MCP Bridge plus exact Unity-installed relay. This is not the OpenAI API-key Gateway route. No agent or CI runner may directly invoke Unity Hub, Editor, executable, CLI, or batchmode.
- WP-0001 and D-0050 authorize only the creator-operated setup phase, including installation/opening of the exact D-0047 candidate and temporary project identity. No gameplay, production asset, agent-operated installation, direct agent/CI Unity process, further AI Unity tool call, or autonomous integration is authorized yet. The pre-D-0051 read-only console smoke is a recorded deviation, not activation evidence.
- Repository creation does not advance autonomy.
- Historical sealed receipts `RR-D0049-20260715` and `RR-WP0001-ACCEPT-20260715` remain immutable. Owner-authenticated receipts `RR-D0051-20260716` and `RR-WP0001-ROUTE-20260716` now protect the direct-MCP successor and bind `UNITY-MCP-EXTERNAL` to unchanged WP-0001 contract `eed333603affe6aa1dd2b16b26ae702d9f561cc653fa319da02abfe008faeda3`. WP-0001 may enter A1 only after schema-v4 evidence binds the creator-created protected empty seed and registry-only package graph, exact D-0047 tuple, assigned seat/linkage, actual detached quarantine, exact client/relay/Editor/socket/config/FD identity, creator-claimed allowlist and code identities, three exact raw collectors, revoked zero-tool preflight, and distinct live activation session. Its fresh external-protected creator receipt must carry every packet-specific activation claim.
- At most one A1 packet may be active. A1 output is manually inspected and imported or rejected by the creator; agents do not merge it.
- D-0052 is protected and ratified, and WP-0003 is protected and accepted. Its lean local-development boundary remains inactive until a distinct creator receipt activates it; until then every current A0 prohibition above still wins.

If requested work exceeds A0, stop and name the missing authorization. Do not “prepare ahead” by creating implementation scaffolding.

## Source of truth

When sources conflict, the higher source wins:

1. Authenticated creator receipts bound to the active ratified decision head
2. The exact constitutional artifact hash named by those receipts
3. Accepted system, feature, save, and work-packet contracts
4. Content and asset manifests
5. Automated tests and accepted reference captures
6. Implementation
7. Generated documentation and summaries

The foundation lives at `docs/foundation-v0.1/`. Start with:

- `docs/foundation-v0.1/README.md` — map, status language, and change law
- `docs/foundation-v0.1/00-GAME-CONSTITUTION.md` — identity, loop, pillars, invariants, open decisions
- `docs/foundation-v0.1/01-DECISION-LEDGER.md` and `docs/foundation-v0.1/ledger/decisions.jsonl` — decision history
- `docs/foundation-v0.1/02-SYSTEM-MAP.md` — authoritative domains and state exchange
- `docs/foundation-v0.1/03-VERTICAL-SLICE.md` — first proof and hard cuts
- `docs/foundation-v0.1/04-TECHNICAL-ARCHITECTURE.md` — runtime, saves, performance, repository shape
- `docs/foundation-v0.1/05-ART-BIBLE.md` — visual grammar and asset law
- `docs/foundation-v0.1/06-AGENT-OPERATING-MODEL.md`, `docs/foundation-v0.1/07-QUALITY-GATES.md`, and `docs/foundation-v0.1/11-TRUST-AND-ENFORCEMENT.md` — autonomy and proof
- `docs/foundation-v0.1/13-MANUFACTURING-AND-CARAVAN-EXCHANGE.md` and `docs/foundation-v0.1/14-CREATIVE-DIRECTION.md` — later economy and creative-direction contracts
- `docs/foundation-v0.1/governance/ratification-state.json` — current draft entry gates
- `docs/foundation-v0.1/work-packets/proposed/` — bootstrap packet records; each packet's internal status and sealed receipts control authority, never the legacy directory name

Any mismatch between a creator source/active decision and its constitutional materialization is a hard stop; precedence never licenses silent repair. Conflict, ambiguity, or an open creator-owned decision that the active packet depends on, would resolve, or would encode is a stop condition. Propose a decision; never choose silently in code, content, balance, art, or lore.

At A0, an explicit current creator request may authorize a bounded draft documentation/control-plane edit. Capture it as unsealed evidence; do not mislabel it protected authority or use it to begin game/tool/asset implementation.

## Orient before editing

From the repository root:

```bash
git status --short --branch
git log -5 --oneline --decorate
rg --files -g 'AGENTS.md' -g '!Library/**' -g '!Temp/**'
python3 docs/foundation-v0.1/tools/validate_foundation.py
```

Then read this file, the relevant foundation sections, the canonical JSON work packet, and any narrower `AGENTS.md`. Inspect the real baseline before planning. The validator is bootstrap lint, not protected approval or a release gate.

No Unity build, test, scenario-runner, or asset-export command is accepted yet. WP-0001 must create, pin, document, and prove those commands. Never invent a command and report it as operational.

Commands named by a proposed packet are planned interface contracts only. Their presence makes the future gate reproducible; it does not mean the tool exists, has run, or is authorized before the packet is accepted and its A1 quarantine is receipt-bound.

## Planned repository seams

Create or edit these only when an accepted packet declares the exact path:

- `Game/` — future Unity presentation/authoring project; never canonical game state
- `SimulationCore/` — future engine-independent deterministic C# rules
- `SaveContracts/` — versioned envelopes, sections, migrations, and compatibility
- `ContentSource/Blender/` — canonical modeled sources
- `ContentSource/Incoming/<packet-id>/` — quarantined generated or external assets
- `Tools/` — pinned build, validation, scenario, and asset tools
- `Tests/` — scenarios, golden saves/vectors, performance, and validation fixtures
- `BuildArtifacts/<packet-id>/` and `docs/evidence/<packet-id>/` — bounded outputs and evidence

Unity-generated `Library/`, `Temp/`, `Logs/`, `Obj/`, user settings, caches, and local builds never enter Git.

## Work packet and reservation law

Implementation requires one accepted, schema-valid work packet with a reproducible baseline, exact scope/non-goals, decisions and contracts, save impact, tests, budgets, rollback, and creator/integrator approval appropriate to risk.

Before editing:

1. Reproduce the packet baseline; stop if it differs.
2. Confirm packet status and approval receipt, not merely its filename.
3. Acquire the required reservation: exact base commit, paths, state domains/content IDs, lease, expiry, heartbeat, and fencing token.
4. Work only in the packet's standalone disposable clone/sandbox with an independent `.git` directory, or its isolated asset package. Only a fully ratified, accepted, and activated WP-0003 may instead use its exact creator-attested durable-repository `agent/*` branch.
5. Touch only declared paths and interfaces. Ask to amend scope before crossing them.

Generic A1 uses read-only/hash-checked foundation inputs, exact reserved outputs, manifest-bound scratch, and a disposable runtime HOME/private temp with no merge/release credentials. Activated WP-0003 instead uses protected `main`, required checks, creator-controlled merge, its exact local boundary, and conditional first Unity MCP use; it grants no governance, credential, install, publishing, release, or self-merge authority. A folder named `sandbox` is never proof of a boundary.

## Engineering laws

- Keep the authoritative simulation in plain C# with no `UnityEngine` or presentation dependency.
- Use commands → deterministic state transitions → domain events → read models.
- Use fixed ticks, stable IDs, injected clocks/storage/logging, and named pinned RNG streams. Render-frame delta is never the sole gameplay clock.
- Unity scenes, GameObjects, animation, UI, and raw physics are presentation/authoring state unless an accepted contract says otherwise.
- Presentation tiers must not change authoritative outcomes. Quantize or event-encode physics-derived outcomes before they enter canonical state.
- Prefer inspectable text content and manifests over binary asset truth.
- Add no dependency, service, network requirement, or license obligation without packet authority and a removal plan.
- Implement the smallest coherent change. No speculative framework, broad cleanup, or opportunistic refactor.

## Save and migration laws

- Save/load is part of every playable milestone, not end-stage polish.
- Preserve the byte-exact Save Envelope/root/pointer/protected-retention-anchor v1 contract once WP-0001 freezes it.
- Never serialize a runtime object graph as canonical state or silently reinterpret an existing field/version.
- New sections need a manifest schema, criticality, deterministic absence default, and explicit version. Changed sections need an explicit migration.
- Unknown authoritative sections make a save read-incompatible and prohibit rewriting it.
- Writes use immutable generations and crash-safe pointer/anchor rules. Never mutate the only good generation.
- Migrations are explicit, idempotent, observable, tested on verified copies, and covered by golden saves plus compatibility and rollback evidence.
- S2/S3 changes are human-gated at every autonomy level. A save-gate failure blocks rollout.

## Proof, performance, and evidence

Tests accompany implementation. Green unit tests are necessary, never sufficient.

- Reuse registered scenario IDs, seeds, fixture hashes, oracles, canonical-state versions, and exact inputs. Changing one creates a new revision; do not overwrite history.
- Capture commands, exit results, commit/toolchain/environment, hashes, metrics, logs, captures, actual changed paths, and known limits in a content-addressed evidence manifest.
- Report every failed, aborted, and rolled-back attempt. Never improve a denominator by omission.
- The implementer cannot accept their own evidence. At A2–A4, implementer, verifier, and integrator are pairwise-distinct trusted principals for every packet.

Target-Mac spike budgets are provisional gates, not tuning suggestions: native ARM64; five-minute warm-up plus 30 measured minutes; whole-frame p95 ≤ 16.7 ms and p99 ≤ 25 ms; simulation tick p95 < 4 ms; process RSS < 6 GB; tracked Metal allocation < 3.5 GB; managed allocation p95 0 B/frame and average < 1,024 B/frame; 30-minute frame degradation < 10% and retained-memory growth < 5%. Do not loosen a gate to pass a build.

## Art and generated assets

- Optimize for silhouette → function → history and strategy-camera truth with representative UI visible.
- Translate Texas iron into working infrastructure, brutalist opera into civic scale/staging, and tungsten-over-neon into a measurable lighting hierarchy; reject cowboy-theme-park, generic cyberpunk, and recognizable franchise motifs.
- `1 Blender unit = 1 metre`; preserve declared pivots, axes, sockets, materials, LODs, collision, and naming.
- The accepted `.blend` is the editable canonical source; an interchange file is a derived artifact.
- Tripo and similar outputs begin as concepts/blockouts, never shipping assets.
- Keep external/AI output in `ContentSource/Incoming/<packet-id>/` until provenance, exact terms/license, safety, geometry, render-sheet, art-direction, engine-import, and performance gates pass.
- Open unknown/generated files only in an isolated OS sandbox with factory settings, script/auto-execution disabled, no credentials/network, read-only inputs, and scratch outputs.
- Preserve prompts, inputs, raw outputs, edits, tool/model/version, timestamps, terms, manifests, and hashes. The generator cannot be the sole art verifier.

## Git and change hygiene

- Preserve user work and unrelated changes. Never use destructive reset or discard another agent's diff.
- Keep diffs surgical and reviewable; every changed line must trace to the packet or explicit request.
- Do not commit secrets, credentials, generated caches, unlicensed inputs, or sole binary sources.
- Do not push, merge, release, rewrite history, or create a remote without explicit authority. A0/A1 agents cannot integrate.
- Use descriptive conventional commits only when an accepted packet or explicit creator-authorized A0 documentation task permits them: `feat:`, `fix:`, `refactor:`, `docs:`, `test:`, `chore:`.
- Before handoff, inspect `git diff`, rerun every relevant available gate, and state what was not run.

## Handoff contract

Hand off outcomes, not confidence. Include:

1. packet ID and objective;
2. baseline reproduced and exact scope completed;
3. changed paths and state/save/content impact;
4. commands and exact pass/fail results;
5. evidence, scenario, save, build, and asset hashes/locations;
6. performance deltas and visual review status;
7. known limits, open decisions, incidents, and failed attempts;
8. rollback procedure and clean next action.

Never label a candidate accepted, ratified, verified, or release-ready without the required independent/protected receipt.

## Stop and escalate

Stop before changing course when any of these occurs: constitutional conflict; unclear or open design authority; unreproducible baseline; missing approval/quarantine/reservation; undeclared path or scope growth; nondeterministic authoritative result; save or migration uncertainty; performance regression without accepted tradeoff; missing asset provenance/license; protected-state edit; new install/network/remote/credential need; or inability to explain the resulting state.

After three equivalent failures, return the packet to planning with preserved evidence. Ask before assuming. State the evidence, the exact blocker, the smallest decision needed, and the reversible options.
