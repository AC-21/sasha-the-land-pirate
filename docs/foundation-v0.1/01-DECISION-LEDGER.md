# Decision Ledger

Version: 0.1 draft\
Canonical log: [`ledger/decisions.jsonl`](ledger/decisions.jsonl)\
Schema: [`schemas/decision.schema.json`](schemas/decision.schema.json)

This view is for humans. The JSONL log is the **draft canonical representation** used by bootstrap tools and is append-only by policy; it does not become enforceable authority until protected storage and the trusted gatekeeper exist. A changed accepted decision receives a new record with `supersedes`; old records remain intact.

## Decision classes

- `constitutional`: changes what game is being made;
- `experience`: changes how the player inhabits the game;
- `scope`: changes what is included in a milestone;
- `technical`: changes implementation contracts or dependencies;
- `art`: changes visual/audio/content grammar;
- `production`: changes how humans and agents work.

## Current ledger

| ID | Class | Status | Decision | Authority / next action |
|---|---|---|---|---|
| D-0001 | constitutional | Ratified | Post-apocalyptic city building and resource management are the center. | Creator prompt |
| D-0002 | constitutional | Ratified | The game includes scavenging, upgradeable travel vehicles, and different factions with distinct styles. | Creator prompt |
| D-0003 | constitutional | Ratified | No zombies; the game is not turn-based. | Creator prompt |
| D-0004 | technical | Ratified | It must run on the creator's MacBook Pro and include robust save/load. | Creator prompt |
| D-0005 | production | Ratified | The project must mature into a safe background-agent optimization and feature rollout loop. | Creator clarification |
| D-0006 | constitutional | Provisional | Thesis: “Build a home worth returning to.” | Ratify or revise wording |
| D-0007 | experience | Open | Directly drive one lead vehicle vs command a convoy. | Recommendation: direct driving |
| D-0008 | experience | Open | City time behavior during road expeditions. | Recommendation: slowed, forecastable continuation |
| D-0009 | experience | Open | Cruelty ceiling and frequency of irreversible loss. | Recommendation: rare permanent loss |
| D-0010 | constitutional | Open | Factions as competing cultures vs conquest targets. | Recommendation: competing cultures |
| D-0011 | experience | Open | Vehicular combat as pillar, accent, or absent. | Recommendation: accent |
| D-0012 | scope | Provisional | First proof is one town, one road corridor, one faction, one failing water system. | Ratify vertical slice |
| D-0013 | technical | Provisional | Unity 6.3 LTS is the engine candidate; exact patch is chosen only after the spike. | Run engine spike before ratification |
| D-0014 | technical | Provisional | Authoritative fixed-tick simulation is separated from 60 fps presentation. | Validate in simulation spike |
| D-0015 | technical | Provisional | Saves use stable IDs and versioned engine-independent DTOs with migrations. | Implement from first playable |
| D-0016 | art | Provisional | “Storybook salvage” is the working visual grammar. | Approve golden building and vehicle |
| D-0017 | production | Provisional | Blender is canonical; generated 3D enters quarantine and never ships directly. | Ratify pipeline |
| D-0018 | scope | Provisional | Exclude a seamless procedural open world from the first production arc. | Creator must ratify milestone cut |
| D-0019 | scope | Provisional | Exclude the exact non-proof feature/content list named by The Last Bearing from the vertical slice. | Creator must ratify milestone cuts |
| D-0020 | production | Provisional | Agents may optimize implementations but may not amend the constitution or rewrite ledger history. | Ratify agent authority |
| D-0021 | experience | Open | Single-player/offline as launch boundary. | Recommendation: yes for first production arc |
| D-0022 | experience | Open | Strategic camera controls, comfort, and information legibility. | Recommendation: 3D perspective with rotation and zoom |
| D-0023 | constitutional | Open | Cause/age of collapse and supernatural boundary. | Defer until systems proof; no monsters by default |
| D-0024 | technical | Open | Lowest supported shipping Mac. | Profile provisional M1 Pro/16 GB target |
| D-0025 | technical | Provisional | Creator machine target: 2560×1600 render target at 60 fps with scalable quality tiers. | Establish benchmark scene |
| D-0026 | experience | Provisional | Progress expands verbs, access, resilience, and relationships more than raw stat inflation. | Validate through slice |
| D-0027 | constitutional | Provisional | City, road, factions, people, and crises must exchange persistent state; disconnected minigames fail review. | Ratify as invariant |
| D-0028 | production | Provisional | A feature cannot roll out without acceptance tests, save review, performance evidence, and rollback. | Ratify as release law |
| D-0029 | experience | Open | Regional travel topology. | Recommendation: route graph plus compact authored road spaces |
| D-0030 | experience | Open | City manipulation grammar: placement, logistics, population granularity, and scale. | Resolve through an ugly city-grammar spike; D-0022 owns camera presentation |
| D-0031 | scope | Open | Production envelope: time, money, human availability, distribution, and pivot threshold. | Required before setting 1.0 content volume |
| D-0032 | technical | Open | Durable macOS company/product/bundle identity and save-root migration. | Ratify before durable user saves |
| D-0033 | technical | Provisional | URP Forward+ is the renderer candidate. | Prove representative dynamic-city compatibility |
| D-0034 | technical | Provisional | Start the simulation in plain deterministic C#; adopt Jobs/Burst/Entities only through measured ADRs. | Profile before adding ECS complexity |
| D-0035 | constitutional | Open | Ratify or revise the loop, P1–P6, INV-004–010, INV-015, and INV-016 as one hash-bound core. | Recommendation: `RATIFY CORE` after review |
| D-0036 | constitutional | Ratified | The game is titled **Sasha the Atomic Land Pirate**, and Sasha is the protagonist. | Creator clarification |
| D-0037 | constitutional | Ratified | Sasha can build a human-only, humanoid-utility-robot-only, or mixed colony. | Creator clarification |
| D-0038 | technical | Open | D-0036 resolves the public title; company name, internal product ID, bundle ID, and save-root migration remain open. | Supersedes D-0032's unresolved bundle |
| D-0039 | constitutional | Open | Mechanical composition differences vs primarily representational human/robot/mixed colonies. | Recommendation: mechanically distinct but equally viable |

## Immediate ratification queue

The first creator review should answer only these high-leverage issues:

1. Ratify or rewrite the thesis.
2. Direct driving or indirect convoy command?
3. Route graph plus authored road spaces, or another travel topology?
4. Does the city keep running during road trips?
5. How cruel can irreversible consequences be?
6. Are factions mainly societies or opponents to conquer?
7. Is car combat a pillar, an accent, or absent from the first production arc?
8. Is single-player/offline the first production boundary?
9. Authorize the bounded city-grammar comparison rather than preselecting its result.
10. Ratify or revise the constitutional loop and pillars as one bound core.
11. Ratify or revise the Last Bearing proof scenario.
12. Separately ratify or revise its milestone cuts.
13. Choose whether colony compositions are mechanically distinct or primarily representational.

All other open choices can remain deferred until a graybox or profiling spike produces evidence.

## Ledger write rules

1. Agents submit parallel proposals with UUID/ULID proposal IDs; only the trusted integrator allocates the next display `D-####` sequence when accepting a record.
2. Record one decision per line in JSONL.
3. State authority, rationale, consequences, and revisit trigger.
4. Use `provisional` when evidence or approval is missing; reserve `rejected` for explicit creator/integrator authority appropriate to the decision class.
5. Until the creator seals this bootstrap draft, audit corrections may regenerate the draft chain only when the change and receipt hashes are updated together; this is not accepted-history mutation.
6. After sealing or downstream acceptance, never edit a prior event: supersede with a new decision and retain both IDs.
7. Constitution changes require `authority: creator-ratification`.
8. Generated summaries may never infer `ratified` from implementation state.
