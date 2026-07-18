# VGR-05 — One Good Batch Contract

Status: proposed creator-delegated direction; non-executable until an authenticated creator-authorization receipt binding this contract's exact SHA-256 is merged to protected `main`. Implementation and release then require independent review, protected checks, exact Unity dispatcher gates, and transparent creator-delegated manual release.

Schema: `wp0002-vgr05-one-good-batch-contract-v1`.

Authority: the creator explicitly authorized Codex in the active development task to ratify this milestone and continue stewarding development. Codex records and transmits that direction; it does not impersonate the creator or create a reusable authority. The eventual protected pull request must bind this file's exact SHA-256, base, head, and changed-path set before release.

Baseline: protected `main` commit `fb9fb14670f5a6158104bc84680a18515f756ed7`.

## Proof

After the adverse Last Bearing expedition, the repaired settlement machines exactly one spare bearing from two parts while preserving a two-part reserve. After 120 subsequent unpaused settlement ticks, one physical lot appears at the workshop output. The player performs one immediate authored barter at the Last Bearing claims counter: the lot changes custody and Sasha receives a persistent depot-corridor route permit.

This is a manufacturing-and-consideration proof, not a market. The granted permit is a durable promise and route-board state; permitted traversal is not playable in VGR-05.

## Exact eligibility

The start verb is available only when all of the following are true:

- `DepotResolution.TakeBearing`
- `PreparationChoice.CivicBuffer`
- `VehicleModule.WinchAssembly`
- `ExpeditionPhase.AtHome`
- `TransactionPhase.Finalized`
- `TurbineCondition.BearingRepaired`
- `NextCityDecision.MachineSpareBearing`
- `PartsUnits >= 4`
- no batch, lot, or permit already exists

The command debits exactly two parts once and leaves at least two. Every invalid or forged intent fails atomically.

## Fixed identities and balance

- recipe: `recipe:last-bearing:spare-bearing:0001`
- manufacturing job: `world:last-bearing:manufacturing-job:0001`
- lot: `world:last-bearing:lot:0001`
- bilateral contract: `world:last-bearing:trade-contract:0001`
- persistent permit promise: `world:last-bearing:promise:0001`
- workshop output: `settlement:last-bearing:workshop-output`
- claims counter: `site:last-bearing-claims-counter`
- route board: `board:last-bearing:depot-corridor`
- presentation content: `bld_machine_shop_claims_wicket_a`
- input cost: 2 parts
- minimum pre-start parts: 4
- minimum retained reserve: 2 parts
- output quantity: 1
- required duration: 120 subsequent unpaused settlement ticks
- one autosave checkpoint: elapsed tick 60

No identity is generated, selected, priced, graded, matched, or user-authored.

## Minimum canonical state

Add only:

- recipe: `None | SpareBearingOneGoodBatch`
- phase: `None | InProgress | Complete | Settled`
- elapsed settlement ticks
- required settlement ticks
- lot quantity
- lot custody: `None | WorkshopOutput | LastBearingClaimsCounter`

The stable IDs above are constants derived from the one allowed enum values; they are not arbitrary saved strings. Existing `RoutePermitGranted` stores the consideration outcome. Do not add grade, condition, price, value, currency, buyer, seller, order, offer, shipment, or population fields.

## Commands and transitions

Two non-generic commands exist:

1. `StartSpareBearingBatchCommand`
   - validates the exact eligibility tuple;
   - debits two parts once;
   - sets recipe, `InProgress`, elapsed `0`, required `120`, quantity `0`, custody `None`;
   - leaves `NextCityDecision.MachineSpareBearing` pending until completion.
2. `BarterSpareBearingLotCommand`
   - is available only for the completed fixed lot at `WorkshopOutput`;
   - atomically sets phase `Settled`, custody `LastBearingClaimsCounter`, `RoutePermitGranted = true`, and `FactionAccessPolicy = PermitRequired`;
   - changes nothing else.

Autonomous unpaused settlement ticks advance only a batch that was already in progress when the step began; the command step that starts the batch does not count. At elapsed tick 60, emit the one in-progress checkpoint. At elapsed tick 120, set phase `Complete`, quantity `1`, custody `WorkshopOutput`, and consume `NextCityDecision` to `None`.

New commands are one-shot and retry-safe. Repeating an already completed start or already settled barter emits the existing idempotent audit event and changes no conserved value. A mismatched-state retry fails closed.

## Consequence preservation

The barter must preserve all adverse history:

- `DepotControl.Depleted`
- current `DepotBearingDisposition.InstalledAtTurbine`, while preserving the exact adverse take-bearing lineage in `DepotResolution.TakeBearing` and the existing faction memory
- `FactionClaimState.Aggrieved`
- the existing singleton `FactionMemoryRecord`
- faction trust
- faction grievance
- faction aid policy and emergency-aid amount
- depot access fee
- `PendingFactionOutcome.Adverse`
- `FutureRouteTollFuelUnits == 2`
- every prior expedition, cargo, repair, maintenance, and clock field

Do not overwrite or append faction memory. The exact batch/barter state records settlement. `FactionAccessPolicy.PermitRequired` plus Sasha's granted permit is not shared service, restored depot control, or forgiveness.

## Persistence and autosave

- bump Last Bearing canonical state and codec to version 3;
- version 2 and every other version fail with the unknown-version result;
- provide no migration, reinterpretation, or compatibility promise;
- round-trip exact state at start, elapsed tick 60, completion, and settlement;
- autosave on batch start, the single tick-60 checkpoint, completion, and barter settlement;
- faulted publication and replay tests cover those four phases.

## Presentation

Extend the existing single-camera `BuildingCutaway` into one authored machine-shop-and-claims-wicket dollhouse. Read left to right:

`two parts in` → `one guarded machine` → `one output tray` → `one claims wicket` → `one route board`

Use current primitive/material seams: Texas-iron plate and cast housings, brutalist civic mass, tungsten task light as the hierarchy, and only tiny cool signal accents. The physical lot must move from the output tray to the claims counter, and the route board must visibly change from denied to permitted while still showing the two-fuel future toll. Human, robot, and mixed colonies show their exact residents performing identical work. No new camera, avatar controller, recipe browser, inventory grid, price UI, market screen, or neon-led cyberpunk treatment.

## Decision containment

- D-0030 remains open: every anchor is a fixed authored socket; no placement or city grammar is selected.
- D-0039 remains open: every composition uses identical eligibility, cost, duration, output, barter, and benefit.
- D-0044 remains open: this creates no Exchange, caravan market, population gate, price law, currency, order, reusable barter rule, certification, fee, loss, cancellation, or dispute system.
- D-0046 is neither ratified nor superseded; this bounded direct barter is consistent with, but does not accept, that proposed direction.

The existing WP-0002 reserved paths and `settlement`, `logistics`, `faction`, `audit`, `presentation`, and `save` domains are sufficient. No reservation expansion or foundation/governance edit is authorized by this contract.

The authorizing protected transaction adds only the append-only existing-schema candidate receipt `BuildArtifacts/WP-0002/candidate-control-plane/RR-WP0002-VGR05-ONE-GOOD-BATCH-20260718.json`, with claims `AUTHORIZE-WP0002-VGR05-ONE-GOOD-BATCH` and `AUTHORIZE-WP0002-VGR05-SPARE-BEARING-FOR-DEPOT-CORRIDOR-PERMIT`. The active packet reserves that candidate-control-plane path and forbids A1 from editing protected foundation, governance, or receipt paths. This transaction does not amend the WP-0002 packet, boundary, reservation, domains, or lifecycle.

## Acceptance

- exact conservation and atomic failure at every transition;
- 120 subsequent unpaused settlement ticks, with pause and replay proof;
- no double debit, duplicate lot, post-transfer barter, negative inventory, or forged state;
- exact save/load and autosave proof for start, midpoint, completion, and settlement;
- identical mechanical projection for all colony compositions;
- one physical, legible cutaway handoff and persistent route-board permit/toll state;
- full deterministic scenarios, core tests, source contracts, Unity EditMode and PlayMode tests, compile refresh, and technical capture pass;
- implementation contains none of the excluded market or generalized-production concepts.
