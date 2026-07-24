# VGR-22 — Receive the Water Tender

## Outcome

Make cooperative emergency aid a physical homecoming instead of an invisible
resource credit. After the cooperative field sleeve is installed, the Field
Desk routes Sasha to a procedural water tender beside canonical Emergency
Storage. A fresh physical input accepts the authored 10.000-milli offer, current
storage capacity clamps what enters, and the empty tender remains as a truthful
receipt while Shared Service and its maintenance promise continue.

## Player contract

When the authoritative read model exposes
`IsEmergencyAidReceptionAvailable`, the Field Desk shows:

`OPEN EMERGENCY STORAGE · RECEIVE WATER TENDER`

This button is route-only. It keeps the player in `CityOverview`, frames the
dedicated `INTERACT_RECEIVE_EMERGENCY_AID` valve beside the placed Emergency
Storage, and changes no water, policy, command sequence, save, canonical byte,
or hash.

The valve requires an input release after routing. It then accepts:

- keyboard E;
- gamepad South;
- the exact tender-valve pointer target.

Acceptance delegates one public `ReceiveEmergencyAidCommand`. The interactor
owns no eligibility, quantity, capacity clamp, state transition, save seam, or
canonical event.

## Exact physical read

Before acceptance:

- one Texas-iron water tender sits outside the working service cell beside the
  actual Emergency Storage pad, including nondefault placements and rotations;
- its tank carries the fixed 10.000-milli cooperative-water witness;
- a small Shared Service mark is the only cool status accent;
- one tungsten valve handle carries the interaction hierarchy;
- the delivery hose is not presented as a completed receipt.

After acceptance:

- the tender remains visibly empty;
- its hose is coiled;
- the receipt says the authored 10.000-milli offer was received;
- the receipt separately shows actual stored water against current effective
  capacity, so a partial or zero-capacity-clamped credit is never described as
  10.000 newly stored;
- Shared Service remains open and the field-sleeve maintenance promise remains.

The tender is composition-neutral and identical for human, utility-robot, and
mixed colonies. Winch Assembly and Sealed Range Tank cooperative rows use the
same receipt. Adverse rows never show it.

## Independent city-work ordering

Emergency-aid receipt is independent of `NextCityDecision`. Workshop Push
cooperative rows may expose a city improvement at the same time.

The Field Desk gives the queued water tender first priority. After receipt, the
existing auxiliary-pump or emergency-cistern improvement becomes the current
order. If the player installs that improvement first through another valid
physical route, the water-tender order remains available and uses the resulting
effective capacity when accepted.

No improvement resource, placement, handwheel, socket, or balance rule moves
into the aid interaction.

## Authority, save, and autosave

Core owns the exact cooperative lineage, authored amount, capacity clamp, policy
transition, idempotency, and rejection. The accepted command changes
`FactionAidPolicy.EmergencyWaterQueued` to
`FactionAidPolicy.EmergencyWaterDelivered`, retains
`EmergencyAidWaterMilli == 10.000` as provenance, and emits the existing
`EmergencyAidDelivered` event.

Presentation adds that existing event to the critical autosave list. Queued
saves reload to the CityOverview tender route without accepting it. Delivered
saves reload with the empty tender, coiled hose, stored-water/capacity receipt,
Shared Service, and maintenance obligation intact.

This increment adds no saved field, schema, migration, event kind, balance
value, scene, mode, camera, package, dependency, production asset, audio, or
general interaction framework.

## Placement and input safety

The tender derives its position from the authoritative Emergency Storage pad
and uses that pad's authored unobstructed service-yard edge. Its body and exact
collider must not clip inherited Emergency Storage or overlap the emergency
pump / Dust Front relay, dry-line gauge, emergency-cistern expansion handwheel,
Hot Shift control, or any building interaction target in default or covered
nondefault rotated layouts.

The tender uses the one shared city camera and listener. While its valve is
focused, both the controller and camera rig yield E / gamepad South to the
tender; receiving water cannot also rotate the strategy camera or trigger
another global city verb.

## Rejection contract

Held entry input, title, garage, wrong mode, pre-repair return, stale read-model
identity, pending work, duplicate requests, already delivered aid, withheld
aid, forged cooperative lineage, and every adverse row fail closed. Rejection
leaves canonical bytes, hash, water, capacity, policy, access, maintenance,
improvement, and presentation truthful.

## Acceptance evidence

Focused core, source, EditMode, and PlayMode coverage proves:

- the Field Desk delegates only route intent and never constructs the core
  command;
- the controller alone constructs `ReceiveEmergencyAidCommand`;
- keyboard, gamepad South, and exact pointer input queue exactly one command
  only after release;
- focused E does not rotate the city camera;
- stale, pending, duplicate, wrong-mode, pre-repair, and adverse requests queue
  none;
- the queued 10.000-milli tank becomes an empty tender with coiled hose and
  capacity-honest receipt;
- receipt-before-improvement and improvement-before-receipt both work;
- both modules and all colony compositions share the same mechanics;
- ready and delivered saves restore truthfully;
- four CityOverview-to-tender route cycles preserve canonical bytes;
- nondefault rotated placement avoids other city controls;
- one shared camera and one `AudioListener` remain.

The gameplay-PR gate is compile, focused core/source/EditMode/PlayMode coverage,
a short native player-path smoke, and three to five repeated tender routes. The
extended performance phases and 100-cycle soak remain milestone/nightly gates.
