# WP-0002 native IL2CPP ARM64 performance-gate successor

Date: 2026-07-19
Amendment: `A1B-WP-0002-NATIVE-IL2CPP-ARM64-PERFORMANCE-GATES-20260719`
Required receipt: `RR-WP0002-NATIVE-IL2CPP-ARM64-PERFORMANCE-GATES-20260719`

## State and authority

Stage-1 is a local draft and grants no executable authority. It is structurally
valid while the named receipt is absent only because every native gate remains
fail-closed on that absence. The draft does not authorize its own merge, use of
the new gates, or any relaxation of the existing WP-0002 boundary.

The receipt must carry both exact claims:

- `SUPERSEDE-WP0002-GATE-DISPATCHER-V1-ONLY`
- `AUTHORIZE-WP0002-NATIVE-IL2CPP-ARM64-PERFORMANCE-GATES`

The supersession is limited to the fixed dispatcher contract. It does not
supersede WP-0002, its reservation, its creator-controlled release gate, or any
other local-operator, protection, package, save, or evidence control.

## Exact two-stage materialization

Stage-1 changes exactly these fourteen regular repository paths:

1. `Game/Assets/AtomicLandPirate/LastBearing/Editor/WP0002GateDispatcher.cs`
2. `Game/Assets/AtomicLandPirate/LastBearing/BuildProfiles.meta`
3. `Game/Assets/AtomicLandPirate/LastBearing/BuildProfiles/WP0002NativeIl2CppArm64Performance.asset`
4. `Game/Assets/AtomicLandPirate/LastBearing/BuildProfiles/WP0002NativeIl2CppArm64Performance.asset.meta`
5. `Game/Assets/AtomicLandPirate/LastBearing/Tests/EditMode/LastBearingAdapterTests.cs`
6. `Tests/AtomicLandPirate.CoreTests/LastBearing/GameSourceContract.cs`
7. `docs/foundation-v0.1/governance/WP-0002-NATIVE-IL2CPP-ARM64-PERFORMANCE-GATES-20260719.md`
8. `docs/foundation-v0.1/governance/a1-boundaries/WP-0002.json`
9. `docs/foundation-v0.1/schemas/local-a1-boundary.schema.json`
10. `docs/foundation-v0.1/tools/validate_foundation.py`
11. `docs/foundation-v0.1/tools/test_validate_local_a1_boundary.py`
12. `docs/foundation-v0.1/tools/test_validate_wp0002_reservation_renewal.py`
13. `docs/foundation-v0.1/tools/test_validate_wp0002_native_performance_gate_successor.py`
14. `docs/foundation-v0.1/work-packets/proposed/WP-0002.json`

The creator receipt must bind the immutable WP-0002 packet contract, prior
boundary SHA-256, exact Stage-1 base, head, tree, deterministic patch SHA-256,
canonical changed-file manifest SHA-256, the sorted fourteen-path manifest, and
the SHA-256 of every Stage-1 artifact. The receipt must have one unique
protected `refs/remotes/origin/main` first-parent introduction, and that
introduction must bind the exact fourteen artifact blobs. A missing, duplicate,
non-linear, differently based, differently modeled, or blob-mismatched
introduction fails closed.

Stage-2 adds exactly the one named sealed creator receipt and no other path.
The final protected squash contains exactly the fourteen Stage-1 paths plus
that receipt. `wp0002-policy` may be temporarily nonrequired only for that
exact receipt-bound transaction while `validate` and `wp0002-core` remain
required. Strict PR, admin, conversation-resolution, and linear-history
enforcement remain; bypass allowances remain empty; force push and deletion
remain disabled; squash-only merging remains. `wp0002-policy` is restored
immediately after merge.

The three native gates cannot validate or accept their own control PR.

## Fixed gate surface

Dispatcher v2 retains the four existing gate IDs and adds only:

- `wp0002-native-il2cpp-arm64-build`
- `wp0002-native-il2cpp-arm64-performance-start`
- `wp0002-native-il2cpp-arm64-performance-collect`

No caller-supplied path, executable, filter, process, argument, duration,
resolution, build setting, package, network action, credential action, or
ProjectSettings mutation is authorized. `DEVELOPER_DIR` is fixed per process
and system `xcode-select` is not mutated.

The fixed toolchain is Unity `6000.5.4f1`, Standalone OSX ARM64-only, IL2CPP,
development build, Xcode `26.3` (`17C529`), macOS SDK `26.2`, and
`/Applications/Xcode.app/Contents/Developer`. Outputs are confined to
`BuildArtifacts/WP-0002/local-only/native-il2cpp-arm64`.

## VGR-13 report scope

VGR-13 is exactly 300 seconds warmup, 300 seconds paused unchanged city,
300 seconds representative unpaused city, 100 city/garage cycles, and fixed
2560x1600. The runtime report makes only a request/runtime identity match; the
collector independently authenticates source and executable evidence.

No `representative_v0`, RSS, Metal, drift, or full-V0 performance claim is
authorized. In particular, this successor does not authorize or imply an
1800-second full-V0 measurement.
