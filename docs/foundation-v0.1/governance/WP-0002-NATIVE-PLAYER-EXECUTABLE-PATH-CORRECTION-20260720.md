# WP-0002 native player executable-path correction

Date: 2026-07-20
Amendment: `A1B-WP-0002-NATIVE-PLAYER-EXECUTABLE-PATH-CORRECTION-20260720`
Required receipt: `RR-WP0002-NATIVE-PLAYER-EXECUTABLE-PATH-CORRECTION-20260720`

## Cause and bounded correction

The authorized native build produced local run
`80dc6eb7f74e-3f2af2409f944563817fc90d36970424`. Its app bundle reports both
`CFBundleExecutable` and `CFBundleName` as `Game`, and its only observed player
executable is the ARM64 Mach-O regular file at
`SashaAtomicLandPirateVGR13.app/Contents/MacOS/Game`.

Dispatcher v3 instead pinned the project product-title executable as
`SashaAtomicLandPirateVGR13.app/Contents/MacOS/Sasha the Atomic Land Pirate`.
The build therefore stopped at the existing exact regular-file check even
though Unity had emitted the fixed `Game` executable.

The correction replaces that one constant with the exact observed relative
path. It accepts exactly one player executable path. It adds no fallback,
search, product-name derivation, caller input, alternate app bundle, or broader
path rule. Existing regular-file, no-reparse, ARM64 Mach-O, executable SHA-256,
app-bundle tree SHA-256, build/run manifest, runtime identity, and source-tree
checks remain unchanged. The build profile and ProjectSettings are unchanged.

## Exact authority and predecessor

The required creator receipt carries only these claims:

- `SUPERSEDE-WP0002-GATE-DISPATCHER-V3-PLAYER-EXECUTABLE-PATH-ONLY`
- `AUTHORIZE-WP0002-NATIVE-PLAYER-EXECUTABLE-PATH-CORRECTION`

It retains:

- packet contract SHA-256
  `ce03ba29c00cec0235bd90c8044237f3286980ccfd7fe9a685aaa2a1e91e75aa`;
- predecessor dispatcher SHA-256
  `aafa9b87455ff8658a82226e57b207be3a8907f7590ec163f144e52e2e50abd0`;
- predecessor duplicate-count receipt SHA-256
  `11bf6d2bd90881fdfcae427c3532694614533089bab16c0353860d4747958dff`;
- predecessor boundary SHA-256
  `640bdbf33ccccd06b04e0ae5b1bf12554e325c36d09101531f23d5f9b9eeba18`;
- unchanged native build profile SHA-256
  `6763b9edcb6ade391b132242c518796eebda444f883917768252a3314bda9249`;
- protected base commit
  `c5cfa463bf2b5ff9714be9483f67287f8180ec05`.

## Exact materialization

Stage 1 changes exactly eight regular repository paths:

1. `Game/Assets/AtomicLandPirate/LastBearing/Editor/WP0002GateDispatcher.cs`
2. `Game/Assets/AtomicLandPirate/LastBearing/Tests/EditMode/LastBearingAdapterTests.cs`
3. `Tests/AtomicLandPirate.CoreTests/LastBearing/GameSourceContract.cs`
4. `docs/foundation-v0.1/governance/WP-0002-NATIVE-PLAYER-EXECUTABLE-PATH-CORRECTION-20260720.md`
5. `docs/foundation-v0.1/governance/a1-boundaries/WP-0002.json`
6. `docs/foundation-v0.1/schemas/local-a1-boundary.schema.json`
7. `docs/foundation-v0.1/tools/validate_foundation.py`
8. `docs/foundation-v0.1/tools/test_validate_wp0002_native_editor_path_correction.py`

Stage 2 adds only the named sealed receipt. The receipt binds the exact base,
Stage-1 commit and tree, deterministic patch, changed-file manifest, sorted
path set, every Stage-1 artifact, predecessor receipt, unchanged native build
profile, and protected creator comment.

The required `validate` and `wp0002-core` checks remain required throughout.
Only `wp0002-policy`, which rejects this bounded control-plane correction by
design, may be temporarily nonrequired for the exact receipt-bound PR. It must
be restored immediately after the protected squash. Strict pull-request,
administrator, conversation, linear-history, no-bypass, no-force-push,
no-deletion, and squash-only settings remain unchanged. The corrected native
gate remains inert until the protected squash and exact receipt are present on
`main`.
