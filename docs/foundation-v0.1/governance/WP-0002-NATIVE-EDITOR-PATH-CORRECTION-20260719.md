# WP-0002 native Editor application-path correction

Date: 2026-07-19
Amendment: `A1B-WP-0002-NATIVE-EDITOR-PATH-CORRECTION-20260719`
Required receipt: `RR-WP0002-NATIVE-EDITOR-PATH-CORRECTION-20260719`

## State and cause

Stage-1 is a local draft and grants no executable authority. The corrected
dispatcher remains fail-closed while the named receipt is absent. It cannot
validate its own control transaction and must not be invoked before the exact
receipt-only Stage-2 is merged to protected `main`.

At source commit `054efead1e30860810c914c552416b8b18ae2e62`, the native
build gate failed before build scheduling with
`unity-editor-executable-path-mismatch`. Unity reported the macOS Editor
application identity as the exact bundle
`/Applications/Unity/Hub/Editor/6000.5.4f1/Unity.app`; dispatcher v2 compared
that value to the nested executable
`Contents/MacOS/Unity`. The nested executable remains a regular file with the
previously authorized SHA-256
`5dcf81c5df5a9ff35006ee05832a1a6194c60fc4a386df652b9f49ea3a2a238b`.

The correction also materializes the exact `dispatcher_sha256` boundary token
already required by the native verifier. The predecessor boundary exposed only
`authorized_dispatcher_sha256`, so omitting this token would leave the native
gates fail-closed immediately after the application-path correction.

## Exact authority

The creator receipt must carry both exact claims:

- `SUPERSEDE-WP0002-GATE-DISPATCHER-V2-EDITOR-PATH-CHECK-ONLY`
- `AUTHORIZE-WP0002-NATIVE-EDITOR-BUNDLE-EXECUTABLE-IDENTITY-CORRECTION`

The supersession is limited to dispatcher v2's Editor application-path check
and its receipt chain. It neither supersedes nor expands WP-0002, the native
gate IDs, process surface, fixed arguments, build profile, toolchain, output
root, timings, resolution, evidence protocol, reservation, save boundary, or
creator-controlled release gate.

The receipt must retain these predecessor bindings:

- packet contract SHA-256
  `ce03ba29c00cec0235bd90c8044237f3286980ccfd7fe9a685aaa2a1e91e75aa`;
- dispatcher v2 SHA-256
  `2aa5b1351e808d6c38819581c637bf171896ec2888633acbd8102ce6f1662392`;
- predecessor native-gate receipt SHA-256
  `832348ad6c772a95dd5e98a4dc569de170707e8ffe4cb35c69f079dac7ecc484`;
- predecessor boundary SHA-256
  `e0de86facc529e6dc1d6be38244408c8463083b27bd45e41034496e382954d4b`.

## Corrected identity contract

Dispatcher v3 performs three separate fail-closed checks:

1. compare `EditorApplication.applicationPath`, after `Path.GetFullPath`, to
   the exact fixed `.app` bundle with ordinal comparison;
2. reject missing or reparse-point components in that bundle and derive only
   `Contents/MacOS/Unity` beneath it;
3. require that derived executable to be a regular non-reparse file and retain
   the exact previously authorized executable SHA-256.

The executable, bundle parent, alternate version, case variant, empty value,
or caller-supplied path cannot substitute for the exact application bundle.

## Exact two-stage materialization

Stage-1 changes exactly these eight regular repository paths:

1. `Game/Assets/AtomicLandPirate/LastBearing/Editor/WP0002GateDispatcher.cs`
2. `Game/Assets/AtomicLandPirate/LastBearing/Tests/EditMode/LastBearingAdapterTests.cs`
3. `Tests/AtomicLandPirate.CoreTests/LastBearing/GameSourceContract.cs`
4. `docs/foundation-v0.1/governance/WP-0002-NATIVE-EDITOR-PATH-CORRECTION-20260719.md`
5. `docs/foundation-v0.1/governance/a1-boundaries/WP-0002.json`
6. `docs/foundation-v0.1/schemas/local-a1-boundary.schema.json`
7. `docs/foundation-v0.1/tools/validate_foundation.py`
8. `docs/foundation-v0.1/tools/test_validate_wp0002_native_editor_path_correction.py`

The creator comment and receipt bind the exact protected base, Stage-1 head,
tree, deterministic patch SHA-256, canonical changed-file manifest SHA-256,
sorted eight-path set, every Stage-1 artifact SHA-256, and predecessor receipt.
The receipt artifact map also retains the unchanged build profile at
`Game/Assets/AtomicLandPirate/LastBearing/BuildProfiles/WP0002NativeIl2CppArm64Performance.asset`
with SHA-256
`6763b9edcb6ade391b132242c518796eebda444f883917768252a3314bda9249`;
that retained artifact is not a Stage-1 changed path.
Stage-2 adds exactly the one named sealed receipt and no other path. The final
protected squash contains exactly the eight Stage-1 paths plus that receipt.

`wp0002-policy` may be temporarily nonrequired only for the exact receipt-bound
final head. `validate` and `wp0002-core`, strict pull-request/admin/
conversation-resolution/linear-history enforcement, empty bypass allowances,
no force push or deletion, and squash-only merging remain. `wp0002-policy` is
restored and verified within 600 seconds after the squash merge.

The packet JSON, predecessor native governance record, and predecessor receipt
remain byte-identical. No corrected native gate may run until the protected
correction squash is an ancestor of the source commit and its exact receipt
bytes match protected `main`.
