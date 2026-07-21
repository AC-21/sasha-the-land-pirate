# WP-0002 native boundary duplicate-count correction

Date: 2026-07-20
Amendment: `A1B-WP-0002-NATIVE-BOUNDARY-DUPLICATE-COUNT-CORRECTION-20260720`
Required receipt: `RR-WP0002-NATIVE-BOUNDARY-DUPLICATE-COUNT-CORRECTION-20260720`

## Cause and bounded correction

The first authorized native build attempt stopped before scheduling with
`native-boundary-binding-missing-or-ambiguous`. The boundary intentionally
contains two exact copies of both the Unity Editor binary SHA-256 token and the
Editor-path amendment identifier, while dispatcher v3 required every exact
token to occur once.

The correction keeps ordinal, non-overlapping matching and changes the private
binding helper to require an explicit exact occurrence count, defaulting to
one. Only those two existing duplicated tokens opt into exactly two. Zero, one,
three, or more copies remain fail-closed. Direct EditMode tests cover the
0/1/2/3 cases and the default of one.

No accepted path, toolchain identity, hash, signature, gate ID, process
surface, build profile, native protocol, runtime source pin, output root,
timing, save boundary, or release authority changes.

## Exact authority and predecessor

The creator-authorized receipt carries only these claims:

- `SUPERSEDE-WP0002-GATE-DISPATCHER-V3-BOUNDARY-DUPLICATE-COUNT-CHECK-ONLY`
- `AUTHORIZE-WP0002-NATIVE-BOUNDARY-DUPLICATE-COUNT-CORRECTION`

It retains:

- packet contract SHA-256
  `ce03ba29c00cec0235bd90c8044237f3286980ccfd7fe9a685aaa2a1e91e75aa`;
- dispatcher v3 SHA-256
  `bd0764bebc486ac6f20354582ddfee1dfd3c1d95541f1be25def60a281783dfc`;
- predecessor path-correction receipt SHA-256
  `703bd05d1454c548d43f9d745ae3d0723dcb38f48428dc139b87140f7d273e97`;
- predecessor boundary SHA-256
  `cdeb0000873ce27c257e4aeea1ba9c573ca9b90ad50a8a93a4a52d10c5c36959`;
- protected base commit
  `efff7181f4ece24bb2101bad30921b072ee3ab90`.

## Exact materialization

Stage 1 changes exactly eight regular repository paths:

1. `Game/Assets/AtomicLandPirate/LastBearing/Editor/WP0002GateDispatcher.cs`
2. `Game/Assets/AtomicLandPirate/LastBearing/Tests/EditMode/LastBearingAdapterTests.cs`
3. `Tests/AtomicLandPirate.CoreTests/LastBearing/GameSourceContract.cs`
4. `docs/foundation-v0.1/governance/WP-0002-NATIVE-BOUNDARY-DUPLICATE-COUNT-CORRECTION-20260720.md`
5. `docs/foundation-v0.1/governance/a1-boundaries/WP-0002.json`
6. `docs/foundation-v0.1/schemas/local-a1-boundary.schema.json`
7. `docs/foundation-v0.1/tools/validate_foundation.py`
8. `docs/foundation-v0.1/tools/test_validate_wp0002_native_editor_path_correction.py`

Stage 2 adds only the named sealed receipt. The receipt binds the exact base,
Stage-1 commit and tree, deterministic patch, changed-file manifest, sorted
path set, every Stage-1 artifact, predecessor receipt, unchanged native build
profile, and protected creator comment.

The required `validate`, `wp0002-core`, and `wp0002-policy` checks remain
required throughout. Strict pull-request, administrator, conversation,
linear-history, no-bypass, no-force-push, no-deletion, and squash-only settings
remain unchanged. The corrected native gate remains inert until the protected
squash and exact receipt are present on `main`.
