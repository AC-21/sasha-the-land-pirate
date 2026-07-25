# VGR-32 — Pull the Lever, Pass the Lot

Status: product-first V0 gameplay increment.

## Player outcome

One Good Batch happens at a place, not through a global abstraction. The player
opens the fixed workshop cutaway, operates the input-machine control to start
the existing spare-bearing batch, watches the physical work progress, then
operates the finished lot at the claims wicket to complete the existing
bilateral permit bargain.

## Authority boundary

- Reuse `StartSpareBearingBatchCommand` and
  `BarterSpareBearingLotCommand` unchanged.
- Reuse the existing recipe, two-part debit, 120 settlement ticks, one stable
  lot, custody transfer, route permit, grievance, two-fuel toll, autosaves, and
  schema.
- The interactor owns only transient focus, input arming, raycasts, highlights,
  labels, and rejection feedback.
- The controller remains the only presentation seam that may queue a command.
- The cutaway remains a projection. It cannot manufacture, move, transfer, or
  settle canonical goods.

## Physical interaction

- The start control is attached to the existing input-machine site and is
  visible only while the selected Building Cutaway can start the batch.
- The handoff control follows the existing physical lot at Workshop Output and
  is operable only while the selected Building Cutaway can barter it.
- Keyboard `E`, gamepad South, and an exact pointer hit operate the focused
  control.
- Entering or returning to the cutaway requires a fresh release before held
  input can operate a control.
- Pointer input blocked by the Field Desk or HUD does not reach the workshop.
- Labels and highlights use text and form, not color alone, and state the
  concrete bargain without implying an open caravan exchange.

## Fail-closed lifecycle

Early, stale, wrong-mode, wrong-cutaway, pending, duplicate, in-progress,
already-settled, title, new-game, failed-load, and successful-load requests
queue nothing. Leaving the cutaway clears focus and arming. Loading derives
ready, working, completed, and settled targets only from canonical state.

The legacy global shortcut and temporary HUD actions may remain only as a
missing-control fallback. An existing but unfocused or unavailable target is
not a reason to bypass the physical interaction.

## Acceptance

- One fresh physical start queues exactly one existing start command.
- One fresh physical handoff queues exactly one existing barter command.
- The complete batch still conserves parts, lot quantity, custody, permit,
  grievance, and toll.
- Ready, midpoint, completed, and settled saves round-trip byte-exactly and
  rederive the truthful physical target.
- Human-only, utility-robot-only, and mixed colonies use identical mechanics
  and show only their canonical workers.
- Three city-to-workshop cycles preserve one interactor, camera, listener, lot,
  and unchanged canonical bytes before operation.
- Focused deterministic/source tests, relevant EditMode and PlayMode tests,
  Unity compile, short native gameplay smoke, and protected checks pass.

## Exclusions

No new core command, field, event, codec, migration, balance value, recipe,
resource, lot, price, buyer, order book, population rule, exchange law,
generalized interaction framework, scene, camera, package, dependency,
production asset, audio system, or everyday performance soak.
