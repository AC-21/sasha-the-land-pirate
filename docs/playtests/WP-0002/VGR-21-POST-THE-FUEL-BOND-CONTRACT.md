# VGR-21 — Post the Fuel Bond

## Outcome

Close the final unsupported Civic Buffer plus Sealed Range Tank return. After an
adverse depot choice, the Field Desk routes Sasha to the existing claims-wicket
cutaway. Five returned fuel units cross the physical ledger, the route board
changes from `LOCKED` to `PERMIT`, and the depot grievance, withheld aid, and
two-fuel future toll remain visible and authoritative.

Both cooperative module rows are deliberately different: SharedService already
keeps the gate open for Winch Assembly and Sealed Range Tank alike, so neither
exposes a paid fuel-bond order.

## Player contract

When the core read model exposes
`IsDepotAccessRestorationAvailable`, the Field Desk shows:

`OPEN CLAIMS WICKET · POST FUEL BOND`

The button is route-only. It opens the existing One Good Batch / claims-wicket
`BuildingCutaway`, frames the dedicated `INTERACT_POST_FUEL_BOND` ledger stamp,
and changes no resource, command sequence, save, canonical byte, or hash.

The physical control requires an input release after routing. It then accepts:

- keyboard E;
- gamepad South;
- the exact claims-ledger pointer target.

Acceptance delegates one public `RestoreDepotAccessCommand`. The interactor owns
no eligibility, price, state transition, save seam, or canonical event.

## Exact physical read

Before acceptance:

- exactly five returned fuel cans sit on Last Bearing's side of the claims
  ledger;
- the spare-bearing lot is absent;
- the fixed route board reads `LOCKED`;
- the persistent two-fuel future-toll witness remains visible;
- one tungsten task light carries the interaction hierarchy, while the small
  cool ledger mark is the only status accent.

After acceptance:

- the five-can lot sits beyond the custody grating as posted consideration;
- the route board reads `PERMIT`;
- the toll witness remains;
- the completion copy retains grievance and withheld aid.

The saved sealed-tank liquid fields remain expedition-return provenance. They do
not claim that Last Bearing still owns the five spent fuel units after the
authoritative resource debit.

## Authority and autosave

Core owns the exact adverse eligibility and transition. The accepted command
spends exactly the returned five fuel units, changes access from `Closed` to
`PermitRequired`, grants the existing route permit, and clears
`NextCityDecision.RestoreDepotAccess`.

Presentation detects the existing `RoutePermitGranted` event for its completion
recap and reuses the existing route-permit autosave. This increment adds no event
kind, saved field, schema, migration, resource, currency, price model, market, or
general trade framework.

## Cooperative branch

The cooperative field-sleeve outcome uses
`FactionAccessPolicy.SharedService`, already carries access, and clears false
post-return decisions for both modules. Its Permit Job copy says that shared
service remains open and that the maintenance promise is the agreement's cost.
Neither cooperative row shows returned fuel as paid consideration or routes to
the fuel-bond control.

The complete decision matrix is:

- adverse + Winch Assembly → One Good Batch;
- adverse + Sealed Range Tank → Post the Fuel Bond;
- cooperative + Winch Assembly → SharedService, no paid city decision;
- cooperative + Sealed Range Tank → SharedService, no paid city decision.

## Rejection contract

Held entry input, title, garage, city overview without route, wrong cutaway,
stale read-model identity, pending commands, duplicate requests, an already
posted bond, non-fuel cargo, the cooperative SharedService branch, and every
other preparation/module row fail closed. Rejection leaves canonical bytes,
hash, resources, access, grievance, toll, and presentation truthful.

## Acceptance evidence

Focused source and PlayMode coverage proves:

- the Field Desk delegates only route intent and never constructs the core
  command;
- the controller is the only presentation layer that constructs
  `RestoreDepotAccessCommand`;
- keyboard, gamepad South, and exact pointer target queue one command only after
  a release;
- stale, pending, duplicate, wrong-mode, and cooperative requests queue none;
- five cans, no spare-bearing lot, `LOCKED` to `PERMIT`, persistent toll, and
  exact human / utility-robot / mixed worker visibility;
- ready and accepted save/load recovery;
- a real Field Desk button hides for the selected physical cutaway and reopens
  across four city-to-claims cycles;
- a valid nondefault city layout uses the same fixed claims wicket;
- one shared camera and one `AudioListener`.

The gameplay-PR gate is compile, focused core/source/EditMode/PlayMode coverage,
a short native player-path smoke, and three to five mode cycles. The extended
performance phases and 100-cycle soak remain milestone/nightly gates.
