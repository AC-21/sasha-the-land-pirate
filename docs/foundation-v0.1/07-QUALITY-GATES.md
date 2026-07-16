# Quality Gates

Version: 0.1 draft\
Rule: green implementation tests are necessary but never sufficient evidence of a good game.

## Gate 0 — Constitutional alignment

Pass when:

- the work packet cites affected pillars/invariants and active decisions;
- the feature strengthens at least two steps or systems in the causal loop, unless explicitly tooling/presentation-only;
- no open decision is silently resolved in code;
- non-goals and scope are explicit;
- any conflict has a creator-approved decision record.

## Gate 1 — Build and source integrity

Pass when:

- clean clone restores pinned dependencies;
- code compiles without new warnings in governed assemblies;
- content/ledger/work-packet/save/asset instances validate through a pinned Draft 2020-12 validator with format checking, positive fixtures, deliberately invalid fixtures, and semantic governance checks;
- no generated caches, secrets, sole binary sources, or unlicensed inputs enter the change;
- dependency/license inventory is current;
- D-0049's selected `UNITY-AI-GATEWAY-CODEX` path is receipt-bound and its exact seat, linked project, Gateway, bundled client, scope/rate-limit, initiation-identity, and revocation evidence is verified; selecting another route requires a superseding decision and revised packet;
- no agent or CI directly invoked a Unity process; before A1, only D-0050's receipt-bound creator-operated setup may install/open the exact candidate, and it must record zero AI Unity tool calls or implementation work;
- native Apple Silicon build succeeds.

## Gate 2 — Simulation correctness

Pass when:

- unit and scenario tests pass;
- inventories, capacities, identities, and command ordering preserve invariants;
- named random streams and seeds reproduce the scenario;
- no orphan IDs, negative resources, impossible recipe states, or non-terminating crises appear;
- long-run economy reports sinks, sources, stalls, and equilibrium/collapse causes;
- manufacturing inputs, outputs, physical-lot lineage, reservations, custody, shipment, and settlement preserve exact conservation;
- population milestones and market clearing remain deterministic across insertion order, save/load, and presentation tiers;
- presentation tier changes do not change authoritative outcomes.

## Gate 3 — Save integrity

Pass when:

- save → load produces the expected authoritative state hash;
- all supported mode/transition slots round-trip;
- current build migrates every golden save or rejects it safely with an explicit reason;
- corrupted/truncated writes do not replace the last good save;
- migrations are idempotent and leave evidence;
- release compatibility matrix proves forward-read, downgrade migration, or immutable-generation restoration before any S2/S3 rollout;
- loading never duplicates cargo, construction cost, expedition results, or commands.

Any Gate 3 failure blocks rollout regardless of autonomy level.

## Gate 4 — Performance and stability

Pass when the declared scenario meets the exact benchmark protocol and numeric budget in `04-TECHNICAL-ARCHITECTURE.md` in a native build:

- frame-time distribution, not only average FPS;
- simulation tick time;
- managed/native/GPU memory;
- draw/triangle/shadow/particle counts;
- path request queue and worst wait;
- save/load time and size;
- scene/mode transition time;
- 30-minute thermal stability;
- no unbounded allocations or log floods.

Regressions must fit the packet's explicit ceiling and tradeoff. A faster average cannot conceal worse stutter or incorrect simulation.

## Gate 5 — Interaction and readability

Pass when:

- the player can identify the next relevant need/opportunity;
- stopped buildings explain why in one interaction;
- actions show cost, target, timing, and consequence before commitment where appropriate;
- city, road, and garage controls do not fight one another;
- keyboard/mouse and controller critical paths are complete for the slice;
- pause, speed, remapping, readable text, color-independent status, and basic motion/volume controls are represented in the design even if polish is staged;
- the normal camera with representative UI remains readable;
- manufacturing shows input, work, output, storage, and stoppage in the world;
- every trade view distinguishes home reserve, available, committed, staged, in-transit, and delivered physical goods;
- delivered terms separate goods, handling, freight, route/risk, and policy causes; no equity-market vocabulary obscures the physical system.

## Gate 6 — Art and asset integrity

Pass when:

- silhouette, function, and state read at normal and overview cameras;
- the asset follows scale, pivot, socket, material, LOD, collision, and naming contracts;
- wear and repair are causally placed;
- faction language is structural rather than a hue swap;
- Texas iron reads as working infrastructure rather than costume, and brutalist opera reads through civic scale/staging rather than copied motifs;
- tungsten practicals dominate inhabited night identity while neon remains rare semantic punctuation;
- grayscale, no-bloom, emissive-disabled, faction-without-color, and reference-removal reviews pass;
- the asset fits the golden lineup and performance envelope;
- source and generated artifacts are reproducible;
- AI-assisted provenance and commercial-rights review are complete;
- art direction receives independent human approval until explicitly delegated.

## Gate 7 — Vertical-slice playability

Pass when:

- a new player understands the first city bottleneck inside five minutes;
- the creator-selected road-control embodiment feels understandable and pleasurable inside 30 seconds;
- both retained vehicle modules cause distinct, obvious non-numeric preparation, traversal/command, and return differences;
- all four preparation/module cases run on the same bound base, at least two remain viable, and at least two returns change the next city decision;
- preparation creates a visible home opportunity cost;
- the expedition returns at least three persistent cross-domain consequences;
- faction memory changes two visible behaviors;
- waiting without departure lets the telegraphed faction intent autonomously claim the depot and persist a world plus city/economic consequence;
- human-only, robot-only, and mixed colonies each complete one identical bounded city → commitment → expedition → return → save smoke path with no hidden human dependency, without multiplying the 2×2 matrix;
- only after D-0039 is creator-bound to `MECHANICAL-COMPOSITIONS`, the separate one-cohort/one-unit matrix proves the declared human dependency/capability, robot dependency/capability, and mixed complementary shared-power choice;
- return creates an emotional/visual sense of home changed;
- the 20–30 minute loop completes without external instruction or recovery command;
- players describe the central fantasy as building/caring for a home, not merely collecting loot.

## Gate 7A — Manufacturing and caravan exchange proof

This post-slice gate does not block The Last Bearing. It passes only when a dedicated accepted packet proves:

- the authored aggregate world-population threshold is transparent, fires exactly once, survives reload, rejects double-counting, and does not hide a mandatory human;
- human-only, robot-only, and mixed colonies can reach and use the institution under the accepted composition and robot-civic rules;
- manufacturing consumes inputs once, creates output lots once, and preserves quantity, grade, origin, condition, and lineage through split/merge;
- every sell is backed by owned physical stock and every buy by accepted reserved consideration and capacity;
- deterministic matching produces the same fills and prices after insertion shuffle, pause, save/load, and retry;
- reservation, cancellation, partial fill, loading, transit, delay, damage, loss, delivery, dispute, and settlement preserve exact accounting without duplication; every live/recoverable lot has one custody location and every destroyed/spilled/unrecoverable quantity has one terminal sink event;
- caravan time, mass/volume, storage, fees, route state, and risk prevent zero-cost teleport arbitrage;
- NPC demand and supply arise from finite inventories, needs, reserves, production, and means rather than infinite sources or sinks;
- price movement and caravaner access or sanctions have player-visible causes;
- a 30-day headless run produces no negative stock, phantom demand, runaway price, orphan order, stuck shipment, or non-terminating dispute;
- the exchange reads as a physical stockyard/institution in world, obeys tungsten-over-neon, and creates persistent civic, faction, road, and story consequences.

## Gate 8 — Release and rollback

Pass when:

- change is mapped to a release manifest and feature flag where required;
- at A2–A4, every accepted packet records pairwise-distinct trusted implementer, verifier, and integrator principals, regardless of declared risk or change class;
- acceptance evidence is attached and reproducible;
- known limitations are explicit;
- rollback is tested against the declared build/save compatibility path and does not endanger the only live generation;
- observation window and health signals are defined;
- trusted release evaluator and protected ledger receive the result; a packet-authored `pass` cannot promote itself.

## Standard scenarios

| Scenario ID | Purpose |
|---|---|
| `SCN_NEW_TOWN_30M` | Complete first city loop with no hidden assistance |
| `SCN_BEARING_COOPERATE` | Faction obligation path and behavior change |
| `SCN_BEARING_TAKE` | Adverse faction-memory path |
| `SCN_RETURN_LATE_DAMAGED` | Recoverable consequence and crisis path |
| `SCN_COMPOSITION_LOOP_SMOKE` | End-to-end human-only, robot-only, and mixed loop without a 3×4 matrix |
| `SCN_COLONY_COMPOSITION_MATRIX` | Conditional D-0039 differentiated one-cohort/one-unit composition proof |
| `SCN_PREPARATION_MODULE_MATRIX` | All four preparation/module cases and viability |
| `SCN_FACTION_WAIT_CLAIM` | Autonomous faction intent while Sasha waits |
| `SCN_TIME_POLICY` | Supported clock policies and selected D-0008 value |
| `SCN_SAVE_TRANSITIONS` | Save before/during/after city-road transitions |
| `SCN_ECONOMY_24H` | Detect impossible/stalled/degenerate economy |
| `SCN_CITY_CAPACITY` | Profile maximum declared city/crowd/logistics load |
| `SCN_ROAD_WORST_CASE` | Profile vehicle, weather, VFX, salvage tool, and encounter |
| `SCN_MIGRATION_MATRIX` | Load and compare every golden historical save |
| `SCN_ROLLBACK_DRILL` | Deliberately fail a canary and prove recovery |

Each scenario definition and run result are separate immutable records. A run captures runner version, commit, engine/packages/toolchain, build/content hashes, allocator/RNG algorithm and counters, input and starting-save hashes, OS/hardware, locale, quality/backbuffer, fixed-step/physics settings, worker count, canonical-hash version, metrics, logs, and captures. Presentation/raw physics are excluded from exact state hash unless explicitly quantized; physics-derived outcomes use authoritative events or declared tolerances.

Exchange proof scenarios are deliberately not registered in this A0 revision: their exact threshold, population semantics, consideration, matching rule, and work packet do not yet exist. The later accepted packet must register immutable unlock, all-composition access, lot-conservation, custody, clearing, invalid-order, delivery-recovery, route-arbitrage, faction-shock, and 30-day stability fixtures before code claims Gate 7A.

## Definition of done by change class

### Bug fix

Reproduction fixture, root cause, smallest fix, regression test, save review, and no relevant gate regression.

### Feature

Accepted contract, flag, tests, UI/feedback, save behavior, performance evidence, play proof, docs, and rollback.

### Optimization

Measured baseline, semantic-equivalence envelope, identical scenario comparison, meaningful improvement, no hidden quality transfer, and cleanup plan.

### Balance/content

State hypothesis, bounded data diff, scenario distribution, no impossible paths, migration/content-ID review, and player-facing rationale.

### Asset

Canonical source, provenance, validation report, camera sheet, lineup comparison, engine check, performance check, and art acceptance.

### Architecture/dependency

ADR, prototype evidence, migration plan, dependency/license record, clean-clone proof, rollback/removal cost, and all affected gates.

## Stop conditions

Stop integration immediately for:

- constitution conflict;
- unreproducible baseline;
- missing source/provenance;
- save corruption or untested migration;
- nondeterministic scenario failure that cannot be explained;
- budget regression without an accepted tradeoff;
- work expanding beyond its packet;
- at A2–A4, one principal occupying more than one of implementer, verifier, or integrator for any accepted packet;
- risk declared below the trusted diff-derived floor;
- an untrusted candidate attempting to modify its gatekeeper and use that modification for the same acceptance.
