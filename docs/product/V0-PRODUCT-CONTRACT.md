# Sasha the Atomic Land Pirate — V0 Product Contract

Status: creator-directed candidate  
Product milestone: **V0 / The Last Bearing Vertical Slice**  
Roadmap mapping: **M5 / VS-3**  
Next milestone: **Founder Playable / M7**

## 1. Purpose

V0 is the first build that can be handed to a new player without explanation and
experienced as a small, coherent game. It is not a campaign alpha, a feature
union of the reference games, or a technical graybox. It proves that the city,
the road, and the return home form one compelling loop under the project's
ratified identity and visual direction.

The governing promise is:

> **Build a home worth returning to.**

Foundation v0.1 is the constitutional document version. It is not the game V0.

## 2. Player-facing promise

In one polished 20–30 minute scenario, the player is Sasha and must:

1. read a failing settlement water system;
2. build, activate, staff, or reprioritize a short production chain;
3. choose between a workshop push and a civic water buffer;
4. equip either a winch assembly or sealed range tank;
5. directly drive a weighty utility vehicle along one of two authored routes;
6. scavenge and resolve a competing faction claim at the transit depot;
7. return with cargo, damage, knowledge, or obligation;
8. visibly repair or scar the settlement;
9. save, quit, reload, and see the exact consequence persist.

The player should finish wanting to see what Last Bearing becomes next.

## 2A. Play-mode and camera contract

V0 has two primary embodied modes:

### City mode

- elevated three-quarter/isometric view of the encampment and inherited civic
  infrastructure;
- 35–45° starting pitch, readable rotation, smooth pan, and semantic zoom;
- a restrained tilt-shift/miniature character may support warmth and scale, but
  depth of field must never blur the active build area, warnings, routes,
  residents, or UI;
- this is the mode for building, placement, staffing, logistics, production,
  policy, upgrades, and observing daily life;
- the city remains alive while inspected: residents, machines, cargo, lights,
  weather, and faction interventions communicate state through motion.

### Driving mode

- conventional third-person vehicle/chase presentation at road scale;
- physics-informed arcade handling rather than simulation punishment or
  weightless steering;
- mass, suspension, traction, terrain, cargo load, damage, weather, camera,
  audio, and controller feedback make the rig pleasurable before combat;
- upgrades change visible geometry and player verbs: winch, range, towing,
  water recovery, cargo, rescue, and route access;
- transitions preserve the same authoritative time, crew, cargo, damage,
  vehicle, faction, and city state.

### Building interiors

V0 does not add free-roaming on-foot interiors. Selecting a building enters a
focused dollhouse/cutaway inspection state within the city-control language:

- roof and obstructing walls peel, fade, or section away;
- machinery, workers, robots, inputs, outputs, storage, damage, and upgrades
  become legible as a living diorama;
- the player manipulates jobs, priorities, repairs, recipes, and upgrade choices
  through the building and its contextual UI;
- the garage/workshop receives the richest treatment: an orbitable rig bay for
  inspecting and installing physical modules;
- authored walkable interiors are deferred until a later proof demonstrates a
  unique systemic or emotional benefit worth the third-mode cost.

## 3. V0 content boundary

V0 contains:

- one settlement district centered on a failing waterworks;
- one road corridor with one alternate path;
- one transit-depot scavenging destination;
- one neighboring faction, doctrine, representative, need, and memory loop;
- Sasha as player-facing protagonist and expedition leader;
- human-only, humanoid-utility-robot-only, and mixed colony starts using one
  coherent mechanics path;
- one field-service vehicle chassis;
- winch and range-tank modules with different verbs and consequences;
- at most six stored resources and three short production chains;
- one dust-front crisis;
- at least two viable preparation/module strategies;
- at least two materially different return outcomes;
- complete new game, pause/speed, settings needed for V0, save, load, failure
  recovery, and end-of-slice state;
- a native creator-Mac build.

New scope must displace an item above or move to Founder Playable.

## 4. Explicit V0 cuts

V0 excludes:

- procedural open world or seamless regional traversal;
- on-foot/FPS play;
- convoy escorts and deep vehicular combat;
- a second city or colony;
- full faction war, conquest, or broad diplomacy simulation;
- large technology trees and production-scale citizen counts;
- generalized campaign manufacturing;
- the population-gated Caravan Stock Exchange;
- multiplayer, online dependency, or live service;
- final campaign lore and marketing-scale content volume.

V0 may show one workshop batch, physical cargo, direct barter or obligation,
and a caravaner claims-office tease. Founder Playable adds bounded
manufacturing and bilateral caravan trade. The full physical-goods exchange is
a later proof milestone.

## 5. Experience acceptance

V0 passes only when all are true:

- a new player identifies and acts on the water bottleneck within five minutes;
- city placement, staffing, production, and logistics feedback are legible;
- road control feels understandable and pleasurable within 30 seconds;
- both vehicle modules change preparation, traversal, and return play;
- at least two strategies remain viable;
- expedition commitments create a visible opportunity cost at home;
- the faction acts autonomously if the player waits;
- faction memory changes at least two later behaviors;
- the return changes skyline or layout, light, routine, sound, and next choice;
- human-only, robot-only, and mixed colonies can complete the loop without a
  hidden mandatory human;
- save/load round-trips all authoritative V0 state without loss or duplication;
- three first-time observers can complete the experience with bounded help;
- at least three observers describe the fantasy as caring for and improving a
  home rather than merely collecting loot;
- the creator accepts the loop as a game worth continuing.

Green automated tests are required but cannot substitute for these observations.

## 6. Visual and audio acceptance

V0 must look authored at the normal gameplay camera. The direction is
**Texas iron × brutalist opera**, with **tungsten over neon** and a humane,
endearing filter.

Before asset fan-out, approve the C0–C2 direction set:

1. midday settlement;
2. the same settlement at tungsten-led night;
3. road and hero vehicle;
4. inherited brutalist landmark occupied by modular settlement life;
5. human, original humanoid utility robot, and mixed-work scenes;
6. one state-dependent Last Bearing comedy/story beat.

The C1 golden kit contains the waterworks, workshop, field-service vehicle,
human/robot calibration pair, concrete world bone, and road kit. C2 adds the
depot, first faction intervention, weather, return transformation, UI, sound,
and feedback language.

Every applicable target must pass with representative UI, in grayscale,
without bloom, with emissives disabled, without faction color, after reference
removal, and on the target Mac. A beauty render cannot overrule failed gameplay
readability.

## 7. Technical acceptance

- deterministic engine-independent authoritative simulation;
- fixed-tick clocks and explicit state transitions;
- durable, versioned V0 save identity and tested migrations;
- keyboard/mouse and controller paths for the complete loop;
- no Unity console errors in the accepted build;
- clean native Mac build and launch;
- whole-frame p95 at or below 16.7 ms and p99 at or below 25 ms on the creator
  Mac under the accepted V0 scenario;
- simulation tick p95 below 4 ms;
- bounded memory and no material warm-run degradation;
- clean-clone reproduction and rollback evidence.

## 8. Design decisions to close before production fan-out

The creator must accept or supersede:

- city grammar: placement, logistics abstraction, population granularity, and
  settlement scale;
- normal strategy camera and close-inspection behavior;
- exact driving and scavenging interaction language;
- whether colony compositions remain representational in V0 or gain bounded
  mechanical differentiation;
- whether “storybook salvage” remains the emotional filter;
- first faction doctrine and representative;
- Sasha's bounded V0 voice and civic role;
- durable product/bundle/save-root identity;
- V0 production envelope and pivot threshold.

## 9. Delivery epics

V0 is delivered through these ordered epics:

1. **V0 contract and decision closure**
2. **City grammar, camera, and information architecture**
3. **C0 visual direction and story tone**
4. **City construction, staffing, production, and logistics**
5. **Vehicle feel, authored road, scavenging, and module verbs**
6. **Last Bearing faction, Sasha, crisis, and return content**
7. **C1/C2 golden asset kit and governed asset factory**
8. **UI, onboarding, audio, feedback, settings, and accessibility**
9. **Durable save, Mac build, performance, and recovery**
10. **Integrated VS-3 playtest, balance, acceptance, and release candidate**

An epic is not complete when code exists. It is complete when its acceptance
evidence is attached and its downstream dependency is unblocked.

## 10. Subagent development model

The lead integrator owns scope, architectural coherence, shared interfaces,
creator decisions, GitHub state, and final acceptance. Subagents accelerate
bounded work; they do not silently decide the game.

Rules:

1. Every subagent receives one concrete outcome, exact allowed paths, explicit
   non-goals, acceptance tests, and a handoff format.
2. Parallel agents receive disjoint paths or read-only analysis scopes. Shared
   authoritative files have one writer at a time.
3. Design lanes produce comparisons and evidence; they do not promote a
   preference to canon without creator acceptance.
4. Art lanes begin with C0/C1 briefs and quarantined assets. No uncontrolled
   asset fan-out precedes a golden-kit decision.
5. Implementation and verification are separated for high-risk simulation,
   saves, economy, and performance changes.
6. Each PR maps to one issue and one bounded work packet or explicit planning
   transaction. PRs do not become the backlog.
7. Every handoff reports changed paths, tests, captures, performance, known
   limits, rollback, and the next dependency.
8. Weekly progress is measured by playable builds and accepted evidence, not
   commits, line count, or agent activity.

## 11. Tracking system

- Constitution and decision ledger: durable laws and creator decisions.
- This contract: exact V0 promise and cuts.
- GitHub V0 umbrella issue: ordered delivery index and current status.
- GitHub epic issues: owner, dependencies, acceptance evidence, and next action.
- Work packets: bounded authority for implementation and asset production.
- Pull requests: candidate changes only.
- Build artifacts and playtest records: proof.

The umbrella issue is updated after every merged V0 PR. At any time it must be
possible to answer: what is playable, what is blocked, what evidence is
missing, and what the next three executable tasks are.

## 12. V0 exit

V0 exits only when the complete Last Bearing loop is playable in a clean native
Mac build, satisfies the experience, visual/audio, save, and performance gates,
has three or more observed first-time playtests, and receives explicit creator
acceptance.

The next milestone is Founder Playable / M7: 60–90 minutes of repeatable play,
two civic stages, two road spaces, two factions, two crises, bounded workshop
manufacturing, bilateral caravan trade, and the complete replay shell.
