# VGR-06 — The Permit Job Contract

Status: released on protected `main` in
`8f2b973e10682a77339b28bde67eb7a9ff5679bf` after independent review, exact
Unity dispatcher gates, protected checks, and transparent creator-delegated
manual release.

Baseline: protected `main` commit
`bea6dacaee95879f3f329d8faf478e8d0036fea2`.

## Proof

Turn the already-authoritative Last Bearing loop into one legible, finishable
job. A first-time player can understand what Sasha is trying to accomplish,
why the recommended choices expose the longest V0 arc, how long preparation
and manufacturing will take, and what the route permit actually cost.

This is a presentation proof. It adds no canonical command, state, event,
balance, save field, codec version, route, market, or dependency.

## Player arc

The HUD presents nine readable steps:

1. inspect the failing water system;
2. prepare home and the rig;
3. drive out and operate the Wreck Line;
4. resolve the depot encounter;
5. drive the frozen consequence home;
6. credit the return and repair the turbine;
7. manufacture One Good Batch;
8. barter the physical lot at the claims wicket;
9. read the consequence and route-permit finale.

The objective rail derives entirely from `LastBearingReadModel` plus the
existing local city-inspection flag. Raw internal objective identifiers remain
available for diagnostics but are never the normal player-facing fallback.

## Guidance without false choice

All existing preparation, module, and depot choices remain valid. The UI marks
`Civic Buffer + Winch`, followed by `Take the claimed bearing`, as the
recommended first run because that pair reaches the complete manufacturing and
permit continuation. It does not change eligibility, cost, timing, outcome, or
canonical hashes.

Other branches close honestly as alternate conclusions:

- cooperation returns a field sleeve and maintenance promise;
- `Workshop Push + Winch` can install the recovered auxiliary-pump rotor;
- the two range-tank branches leave their already-authored city decision open;
- each alternate ending invites replay without claiming a permit was won.

## Progress and finale

- preparation displays exact elapsed, required, and remaining settlement ticks;
- outbound and return display exact canonical route progress;
- One Good Batch displays exact elapsed and required settlement ticks;
- binary steps may display a bounded zero-to-one completion bar;
- progress is read-only projection and never a clock or authority seam.

The permit finale must state all four consequences together:

- water is restored or recovering;
- the one physical lot moved to the Last Bearing claims counter;
- the depot-corridor permit is granted;
- the depot remains aggrieved and the future route toll remains two fuel.

## Presentation boundary

- Keep the current shared-camera, temporary IMGUI, and graybox seams.
- Add no retained UI framework, input package, audio system, asset dependency,
  production art, or generalized quest framework.
- The presenter may not reference `UnityEngine`, `LastBearingState`, the kernel,
  commands, save contracts, or filesystem APIs.
- The presenter and HUD may issue no gameplay command; existing context buttons
  remain the sole semantic-intent seam through `LastBearingGameController`.
- Human-only, robot-only, and mixed colonies receive identical guidance and
  mechanics.

## Decision containment

- D-0030 remains open. This milestone does not select a city placement grammar.
- D-0039 remains open. It adds no composition mechanics or asymmetric needs.
- D-0044 and D-0046 remain open. The one-off physical-lot barter stays bounded;
  there is no exchange, population threshold, price, currency, or order book.
- D-0045 remains open. No production-art fan-out is authorized.
- The later `Home Before Horizon` observed comparison may test two reversible
  D-0030 city-grammar trials, but cannot persist placement or select a winner
  without separate creator direction.

## Acceptance

- the complete golden path receives a chapter, headline, consequence detail,
  action label, and measured progress where canonical progress exists;
- the four material alternate outcomes never display a false permit finale;
- recommendation copy changes no canonical state and remains optional;
- no normal HUD fallback renders `model.NextObjective` directly;
- preparation progress advances exactly with the canonical read model and
  reaches zero remaining at readiness;
- deterministic scenarios, source contracts, Unity compile, EditMode,
  PlayMode, technical capture, protected checks, and diff checks pass;
- save version and byte-exact canonical outcomes remain unchanged.
