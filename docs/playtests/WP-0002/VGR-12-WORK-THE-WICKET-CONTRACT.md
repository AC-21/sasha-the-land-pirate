# VGR-12 — Work the Wicket Contract

Status: creator-directed playable milestone; release remains conditional on
independent review, exact Unity dispatcher gates, protected checks, and
transparent creator-delegated manual release.

Baseline: protected `main` commit
`ba39285cd722a8ec95a4bd7d63f00cf4b8df7f2f`.

## Proof

Finish the implemented Permit Job through its physical workplace instead of
two global HUD actions. On the exact existing golden branch, the player enters
the fixed machine-shop and claims-wicket cutaway, starts the one approved
spare-bearing batch at its input/machine line, watches the existing canonical
work advance, then hands the completed physical lot across the claims wicket
for the existing depot-corridor route permit.

This slice reuses `StartSpareBearingBatchCommand`,
`BarterSpareBearingLotCommand`, the exact One Good Batch eligibility, recipe,
quantity, custody, 120-settlement-tick duration, adverse faction history,
two-fuel future toll, route permit, critical-transition autosaves, fixed
workshop/claims cutaway, and disposable `last-bearing-dev-v1` profile. It adds
no core command, state, event, resource, price, save field, exchange rule,
recipe framework, or generalized interaction system.

## Player sequence

1. Complete the existing `Civic Buffer + Winch + Take Bearing` return, check in
   the loaded scout, and seat the ceramic bearing at the pump hall.
2. The Permit Job directs the player to the existing machine-shop and claims-
   wicket dollhouse. Opening it is a presentation-only route: it selects the
   fixed cutaway and shared-camera pose without queuing a command or changing
   canonical or save bytes.
3. At the selected workshop, use `E`, gamepad south, or the semantic HUD action
   to start the batch. Exactly one existing `StartSpareBearingBatchCommand` is
   queued.
4. Only command acceptance debits the existing two parts, retains the existing
   civic reserve, starts the existing 120-tick job, and changes the physical
   input stock into the existing machine workpiece.
5. Unpaused canonical settlement ticks advance the job exactly as before. The
   fixed spindle, task light, workers, and workpiece are derived feedback only.
6. Completion creates exactly one existing spare-bearing lot in
   `WorkshopOutput` custody at the output anchor. It does not grant a permit.
7. From the same selected cutaway, use the contextual action again to barter.
   Exactly one existing `BarterSpareBearingLotCommand` is queued.
8. Only acceptance transfers the lot to `LastBearingClaimsCounter`, raises the
   existing route permit, and preserves the depot grievance and future toll.

## Contextual action law

- Batch start is presentation-available only when the canonical read model
  exposes `IsSpareBearingBatchStartAvailable`, no command is pending, the
  active mode is `BuildingCutaway`, and the One Good Batch cutaway is selected.
- Barter is presentation-available only when the canonical read model exposes
  `IsSpareBearingBarterAvailable` under the same no-pending, active-mode, and
  selected-cutaway conditions.
- If either canonical action is eligible outside the selected workshop, the HUD
  may offer only a presentation-only open/route action. It must not start or
  settle the batch from city overview, garage, pump hall, title, or another
  mode.
- `E`/gamepad south retains the existing Wreck Line, depot recovery, depot
  cargo, return check-in, and pump-hall repair priorities. Workshop actions are
  reachable only in their disjoint finalized, repaired, at-home states.
- Early, stale, wrong-cutaway, wrong-mode, in-progress, already-complete,
  settled, duplicate, title, or lifecycle requests queue no command and
  preserve canonical and save bytes.
- The core remains the final authority and retains its existing atomic failure
  and idempotent replay behavior.

## Physical stock and presentation law

- The existing fixed input, work, output, claims, and permit anchors are the
  only presentation sites. They do not become canonical locations, inventory
  slots, logistics nodes, trade offers, or placement sockets.
- Before start, only the exact two-part input stock is shown. During work, only
  the machine workpiece is shown. At completion, exactly one solid lot is shown
  at workshop output. After barter, that same lot is shown at the claims
  counter with the route permit granted.
- No animation, collider, trigger, raycast, Rigidbody, camera pose, HUD state,
  or worker pose may create, move, settle, price, or duplicate the lot.
- Custody, quantity, batch phase, elapsed ticks, permit state, grievance, and
  toll are projected only from the canonical read model. Text and silhouette,
  not color alone, distinguish ready, working, complete, and settled states.
- The view owns no input, commands, clocks, save access, or state. The controller
  alone translates an available contextual action into the existing command.

## Save and lifecycle boundary

- Existing batch-start, midpoint, completion, lot-created, barter, and permit
  events retain their current autosave behavior. No new autosave event or save
  field is introduced.
- Ready-to-start, in-progress, complete-at-output, and settled-at-claims
  checkpoints round-trip byte-exactly through the existing profile store.
- Loading derives stock visibility, machine motion, lot custody, permit state,
  open-workshop guidance, and contextual availability solely from canonical
  state. A workshop selection or camera route is never restored from saved
  transient intent.
- Title, new game, failed load, successful load, cutaway changes, and repeated
  open/close actions clear or re-derive presentation state under the existing
  lifecycle law; none persists workshop transient intent or changes profile
  generations or save bytes on its own.
- While paused, paused ticks do not advance settlement, faction, crisis, road,
  or batch clocks. Existing command-processing `GlobalTick` and command-sequence
  advancement remain unchanged; the in-progress workpiece stays visible while
  presentation motion stops.
- The codec layout, profile version, migration boundary, current/last-good
  generation law, slot identity, and save root remain unchanged.

## Composition parity

- Human-only, humanoid-utility-robot-only, and mixed colonies use the same
  eligibility, commands, parts, duration, lot, custody transfer, barter,
  autosaves, permit, and completion oracle.
- The cutaway shows only the exact resident kinds present in the canonical
  roster. No staffing bonus, hidden human, robot substitute, mixed synergy, or
  composition-specific recipe or trade capability is added.
- Worker movement and machine tending are presentation feedback only and cannot
  authorize or accelerate canonical work.

## Golden-path Unity smoke

The Unity acceptance must exercise the actual player-facing controller and save
adapter from title without direct canonical-state injection or manual
`view.Apply` staging:

1. choose a colony, inspect the waterworks, complete one reversible city trial,
   activate the same infrastructure, and commit `Civic Buffer + Winch` in the
   garage;
2. wait for readiness, depart, drive, operate Wreck Line and depot recovery,
   take and load the ceramic bearing, freeze the return, and drive home;
3. check in at the apron, repair in the pump hall, open the workshop, start the
   batch, advance its exact canonical duration, and barter at the claims wicket;
4. assert the physical lot progresses input to work to output to claims, the
   permit is granted once, adverse history and two-fuel toll remain, and one
   shared camera/listener remains throughout;
5. save, return to title, load, and assert the exact pre-title canonical hash,
   colony, vehicle, cargo, faction, crisis, batch, lot custody, route permit,
   cutaway projection, and contextual availability are recovered.

Run the complete smoke for all three compositions, or run one full composition
plus exact human/robot/mixed parity smokes that cover every workshop action and
save checkpoint. Test acceleration may advance the fixed simulation ticks but
must not fabricate a state or bypass a player-facing command seam.

## Authority boundary and exclusions

- Add only the minimum workshop-selection predicate, derived controller action
  availability, presentation-only open/route seam, contextual input/HUD
  routing, tests, and playtest documentation required for this proof.
- Preserve exact commands, sequence law, resources, recipe, duration, quantity,
  custody, contract identity, faction memory, toll, permit, balance, clocks,
  stable IDs, saves, and composition viability.
- Do not add or alter a core command, canonical field/event, codec, save
  version, migration, dependency, scene, package, project setting, asset,
  production art, audio, physics authority, or camera/listener count.
- Do not add a second recipe or lot, arbitrary inventory, pickup/drop, conveyor,
  carrier, logistics network, order book, currency, share market, global price,
  dynamic pricing, population threshold, matching, fee, loss, dispute, buyer,
  regional market, or caravan exchange.
- Do not redesign the temporary IMGUI, add a retained UI package, create a quest
  or interaction framework, or select D-0030, D-0039, or D-0044.

## Acceptance

- start and barter are available only in the selected active One Good Batch
  cutaway and each queues exactly one unchanged existing command;
- wrong-mode, wrong-cutaway, early, stale, duplicate, and lifecycle requests
  are no-ops that preserve pending commands, canonical bytes, and save files;
- accepted start debits inputs once and produces exactly one physical lot after
  the existing duration; accepted barter transfers it once and grants only the
  existing fixed permit while preserving adverse history and toll;
- ready, in-progress, complete, and settled checkpoints load byte-exactly and
  re-derive truthful stock, motion, custody, permit, guidance, and action state;
- the full Unity golden-path save-title-load smoke and composition parity pass;
- existing deterministic/source/save tests, foundation lint, Unity compile,
  EditMode, PlayMode, technical capture, protected checks, and diff checks pass.

Green gates prove a contained physical manufacture-and-barter handoff, not a
broader market, retained UI, final pacing, art acceptance, or fun. Creator play
must still judge whether the wait reads as productive work and whether handing
over the thing makes the permit feel earned.
