# Vertical Slice: The Last Bearing

Version: 0.1 draft\
Target playtime: 20–30 minutes for a first-time player\
Purpose: prove the constitutional loop, not approximate the full campaign.

## 1. Scenario

The settlement's water turbine is failing before a dust front. A replacement ceramic bearing exists at a ruined transit depot down a recently discovered road. A neighboring faction also needs it.

The player must make home functional enough to survive their absence, build a useful vehicle module, travel to the depot, resolve the competing claim, return, and live with the result.

The proposed slice represents the player as **Sasha** throughout: colony decision-maker, expedition leader, save addressee, and addressee for consequences. Those are provisional slice roles authorized only if the creator issues `RATIFY-SLICE`; they do not settle Sasha's campaign-wide civic authority, embodiment, job, or lore. The proof does not require an on-foot avatar, final voice, final backstory, or final character model. “Atomic Land Pirate” remains a title whose exact in-world meaning is deliberately open.

## 2. Content boundary

The slice contains exactly:

- one small settlement district;
- one compact road corridor with one alternate path;
- one scavenging destination, the transit depot;
- one neighboring faction and one embodied representative;
- one human work cohort and one original humanoid utility-robot archetype, sufficient to exercise human-only, robot-only, and mixed staffing fixtures;
- one expedition vehicle chassis, directly driven or commanded under D-0007;
- two precisely defined, mutually exclusive module choices;
- one environmental hazard, a dust front;
- six stored resources at most;
- three short production chains at most;
- one civic failure with at least two viable resolutions;
- a complete save/load path.

Anything else needs to displace an item above, not simply join it.

## 3. Play sequence

### Beat 1 — Read home, 0–5 minutes

The player can inspect the turbine, water reserve, work queue, and forecast. They learn why production is failing without reading a tutorial wall. They place or activate a recycler and machine shop, connect a short logistics path, and assign labor.

Proof: city interaction is understandable and satisfying inside five minutes.

### Beat 2 — Commit, 5–10 minutes

Before departure, the player makes both a city configuration choice and a vehicle choice.

City configuration:

- **Workshop push**: route power/fuel and the specialist to the machine shop, finish the module early, and accept tighter water rationing at home.
- **Civic buffer**: keep pumping/filling emergency storage, finish the module later, and accept that the faction scout may reach the depot first.

Vehicle module:

- **Winch assembly**: high parts and specialist time; opens the collapsed short branch and can tow one heavy object, but consumes a hardpoint and reduces ordinary cargo capacity.
- **Sealed range tank**: high fuel commitment but fewer precision parts; opens the exposed dust route, extends range, and can return with a bounded liquid reserve, but takes longer and cannot tow the heavy cache.

Both modules must change preparation, traversal/command, and the return economy. Crew and fuel committed to the trip visibly leave gaps at home.

Proof: the loadout is a meaningful plan, not a gear-score screen.

### Beat 3 — Venture, 10–18 minutes

Under the recommended D-0007 default, the player directly drives the lead vehicle; under a command decision, the same route must provide immediate, weighty convoy control. The 30-second control/feel gate applies to the selected embodiment. Terrain, weather, route, and the chosen module create a readable difference. Scavenging requires positioning or deploying a tool, not clicking identical boxes.

Proof: road play is tactile and causally connected to preparation.

### Beat 4 — Encounter, 18–23 minutes

The faction's need and intention are telegraphed before departure. Its scout advances toward the depot on the simulation clock without waiting for the player; the Workshop Push may arrive first, while the Civic Buffer may meet an established claim. At the depot, the player can:

- cooperate and accept an alternate repair or future obligation;
- bargain using cargo, a promise, or route access;
- evade and take a costlier secondary component;
- seize it, if the final combat/confrontation boundary allows.

Every option must be strategically viable in at least one state. The faction records what happened, changes behavior, and offers or withholds a doctrine-shaped low-tech bearing/maintenance solution that changes a subsequent city recipe or obligation.

Proof: factions are societies with needs and memory, not dialogue vending machines.

### Beat 5 — Return and transform, 23–30 minutes

The player returns before or during the dust front and resolves the water crisis. The city visibly changes: water availability, building operation, residents, lighting/decoration, sound, faction access/pricing, and the next opportunity reflect the chosen outcome.

The player saves, quits to title, reloads, and sees the exact city, vehicle, cargo, crisis, and faction consequence restored.

Proof: the loop closes and the player wants to continue.

## 4. Systemic permutation matrix

The slice cannot pass as one scripted fetch quest. Automated and observed play must cover at least this 2×2 matrix without adding another map:

| Home preparation | Road capability | Required systemic difference |
|---|---|---|
| Workshop push | Winch assembly | early/short arrival, worse home buffer, tow/heavy-salvage choice |
| Workshop push | Sealed range tank | early departure but longer route, flexible liquid return, no heavy tow |
| Civic buffer | Winch assembly | safer home, later contested claim, short route, limited ordinary cargo |
| Civic buffer | Sealed range tank | safest home, faction likely established, longest trip, alternate water/fuel leverage |

At least two configurations must remain strategically viable after balance tuning. At least two return outcomes must force a different next city production, maintenance, policy, or faction choice—not merely alter text or a reputation number.

### Provisional colony-composition proof, kept orthogonal

D-0037 requires all three compositions to be representable. The differentiated dependencies below are the recommended D-0039 hypothesis and enter WP-0002 only after the creator ratifies that value. The proof is its own bounded three-case matrix and is **not** multiplied across the four preparation/module permutations:

| Fixture | Residents available | Required proof |
|---|---|---|
| Human-only | one human work cohort, no robot units | the bounded workshop/water staffing bottleneck remains solvable and human needs/capabilities are causal |
| Robot-only | one humanoid utility-robot unit, no human cohort | the same bounded bottleneck remains solvable without a hidden human dependency and robot power/maintenance/capabilities are causal |
| Mixed | one human work cohort and one humanoid utility-robot unit | at least one complementary assignment and one shared bottleneck produce a choice not present in either single-composition fixture |

All three cases use the same seed, tick schedule, content revision, building layout, recipe objective, and observation window. Each case must reach its declared completion oracle without negative inventory, orphan staffing, or an undeclared substitute resident. The classification and exact resident set must survive save/load. This test proves composition plumbing and a real tradeoff; it does not claim campaign balance, robot lore, or three full content economies.

## 5. Required state paths

At minimum, automated and manual tests cover:

1. all four preparation/module permutations above;
2. repair with the bearing;
3. accept the faction alternative and carry an obligation/maintenance recipe home;
4. return late or damaged but recoverably;
5. save before departure, at every supported ownership-transition checkpoint, after the encounter, and after repair;
6. reload each save and compare canonical authoritative state;
7. crash/retry each ownership phase without duplicating cargo, crew, time, or results;
8. abandon/cancel construction and expedition preparation without resource duplication;
9. wait without departing and observe the faction's autonomous claim alter the depot/world state;
10. run the settlement long enough to detect a stalled or impossible economy;
11. run the isolated human-only, robot-only, and mixed colony-composition fixtures, save/reload each resident set, and compare canonical state.

## 6. Proof gates

The slice is accepted only if all are true:

- A new player identifies the main city bottleneck without external explanation.
- The player completes one production chain and understands why any stalled building is stalled.
- The selected road-control embodiment reaches the 30-second control/feel bar in a native Mac build.
- Both retained modules make an obvious, distinct non-numeric difference to preparation, route/action, and return.
- At least two city-preparation configurations are viable and visibly change expedition timing or encounter state.
- Expedition preparation creates a visible opportunity cost at home.
- The road outcome produces at least three persistent state changes across city, vehicle, people, or faction domains.
- The faction completes one telegraphed autonomous action if the player waits.
- Faction memory changes behavior in two visible ways and its doctrine changes one city recipe, institution, obligation, or logistics choice.
- The return produces a clear visual and acoustic improvement or scar.
- Save/load round trips the full outcome without duplication, loss, or broken references.
- Human-only, robot-only, and mixed staffing each pass the isolated composition loop, preserve the exact resident set through save/load, and contain no hidden mandatory human. If and only if D-0039 is ratified as `MECHANICAL-COMPOSITIONS`, the separate differentiated matrix must also prove a real human dependency/capability, a real robot dependency/capability, and mixed complementarity plus a shared bottleneck.
- The native build meets the provisional performance and memory budget.
- At least three observers independently describe the fantasy as caring about or improving a home, not merely gathering loot.

## 7. Provisional hard cuts

Proposed as not in this slice, pending creator ratification. The first item is D-0018; every remaining item is the exact D-0019 inventory:

- procedural open world;
- FPS interiors or on-foot combat;
- convoy escorts;
- multiple cities or colonies;
- full faction war simulation;
- deep vehicular combat;
- terrain deformation;
- large tech tree;
- sprawling dialogue trees;
- multiplayer or online dependency;
- final campaign lore;
- production-scale citizen counts or per-citizen full simulation;
- full 4X conquest;
- a final marketing-scale volume of polished assets.

D-0040 and D-0041 make manufacturing, physical-goods trade, and the population-gated caravaner exchange part of the eventual game, but the full exchange is outside this slice under provisional scope decision D-0046; this sentence does not alter D-0019's exact inventory. The slice may prove one workshop batch, one direct barter/obligation, physical cargo, or a caravaner claims-office tease. It may not add a regional order book, abstract global price, industrial specialization tree, or multi-settlement market simulation.

## 8. Build stages and stop/go tests

### VS-0 — Paper/state proof

Headless simulation of resources, turbine failure, module crafting, departure costs, encounter outcomes, return, and save migration. No art.

Go when the loop is solvable by at least two strategies and no resource can become negative or duplicate.

### VS-1 — Graybox proof

One blockout district and one road corridor using primitives. Real controls, transitions, UI skeleton, and saves.

Go when the selected city grammar is legible, the selected road-control mode is fun enough to iterate, and the modes exchange exact state.

### VS-2 — First-look proof

One golden vehicle, water building, recycler/workshop kit, human survivor, original humanoid utility robot, road kit, depot, faction dressing, weather, lighting, and audio language. The first-look proof follows the [`C0–C2 slice direction gate`](14-CREATIVE-DIRECTION.md#slice-direction-gate-c0c2): Texas iron working machinery inside brutalist inherited infrastructure, tungsten-led inhabited light, rare semantic neon, and storybook salvage as the humane filter. C3 manufacturing and C4 exchange art remain separately gated and out of slice.

Go when the normal gameplay camera communicates function and tone without beauty-render excuses; grayscale, no-bloom, emissive-disabled, faction-without-color, and reference-removal reviews pass.

### VS-3 — Vertical slice

Integrated 20–30 minute experience with onboarding, consequences, performance capture, save/load, and playtest evidence.

Go to production only after creator acceptance of the loop, not merely green automated tests.

## 9. Explicit non-proof

This slice does not prove long-campaign balance, content variety, late-game scale, broad faction diplomacy, or final combat. Those need later proof milestones. Claiming them from this slice is forbidden.
