# WP-0003 deterministic-core bootstrap evidence

Status: validated implementation candidate

Created: 2026-07-16T20:55:59Z

## Git and authority

- Protected base: `c4513cd660dc3a522eb93ccec32f4f9b12c06489`
- Candidate branch: `agent/wp0003-bootstrap-001`
- Active autonomy: `A1`
- Active packet: `WP-0003`
- Reservation: held through `2026-08-15T19:38:29Z`
- Every candidate path is inside the sealed WP-0003 reservation.

## Implemented slice

- Empty Unity `6000.5.4f1` project seed with no package dependencies.
- Engine-independent C# 9 / `netstandard2.1` deterministic core.
- Explicit command sequence guard.
- Kernel-owned command -> transition -> event -> read-model flow.
- Immutable event history without a castable backing array.
- Private canonical byte encoder used only by the technical hash oracle.
- Disabled save capability that writes zero bytes and defines no save schema.
- Offline, package-free test runner with parsed and exact input boundaries.
- CI that always reports, uses a full-history credentialless checkout, and
  checks whitespace across the complete pull-request range.

## Local toolchain

- Unity-bundled .NET SDK: `8.0.318`
- `dotnet` SHA-256:
  `635898abd14a453117adbdbb45460fb0a3a55dd2b99eb38982bddd12b8d0649b`
- Roslyn `csc.dll` SHA-256:
  `27d007d0b5a269c9b9549ba0faeafaae5df693114465f4a551207a780689b9a2`
- CI compatibility lane: installed SDK matching exact `8.0.3xx` feature band.
- NuGet package sources: empty.
- Restore/build command timeout: 120 seconds per command.

## Determinism evidence

- Golden technical-state SHA-256:
  `e53cb4f293f17fcfaa2a2717cc1c6730f54b2c41b6bbc105f488cfb92d3db65f`
- Technical tests: `11 passed, 0 failed`.
- Repeated process output: byte-identical.

Two clean builds in the durable checkout produced identical DLL hashes:

- SimulationCore:
  `b76ce3abca8e1ae17c779c8b8a6ddf42e18af946f5f6c940159774776585d1ac`
- SaveContracts:
  `8900837a35496dab403fc46666a815923852c27513efd5e133e5bf48c9017939`
- CoreTests:
  `8a96d3e8ad7bd006b6796ced5a8460b05a890e0451f51cc698dd0d800a03d382`

Two independently initialized fixture checkouts produced the identical source
commit `6446d291e6015032386871f6f4bb395ba8c790d1` and identical DLL hashes:

- SimulationCore:
  `32795101594f1ae167192a91600db2d32e8cb43e32fd9668f7a32692978afb19`
- SaveContracts:
  `40cdac7349697c6b2be03056f67a637d1bf3176aeb3d4edeeecac8554a2b998a`
- CoreTests:
  `72ca270053b895e10a7ab2b6fe5a50799c175098f81b06d7c37088e853ecbc3b`

The durable-checkout and fixture hashes differ because their Git source
commits differ. Reproducibility is asserted between builds with identical
source bytes and identical source commit.

## Repository validation

- Foundation bootstrap lint: pass.
- Foundation control-plane tests: `124 passed, 1 skipped`.
- JSON parse: pass.
- GitHub workflow YAML parse: pass.
- Candidate whitespace scan: pass.
- Package roots contain no `bin`, `obj`, DLL, generated C#, or symlink.
- Generated .NET output remains under ignored
  `BuildArtifacts/WP-0003/local-only/`.

Three advisory agents independently re-reviewed architecture, tooling/CI, and
the Unity/governance boundary. Each returned no current blocker.

## Explicitly not claimed

- Unity, Unity Hub, Editor, relay, and MCP were not invoked.
- The local packages are not linked into `Game`.
- No dependency installation or package resolution occurred.
- No package lock, full generated ProjectSettings, `.meta` files, scene,
  camera, debug UI, EditMode test, or PlayMode test is claimed.
- No save bytes, slot, serializer, version, migration, or frozen schema exists.
- Package license metadata remains creator-selected before linking or
  distribution.
- This is a safe first bootstrap slice, not WP-0003 completion.

The creator must open the existing exact `Game` folder, not create a new
template project, and all six first-use conditions must be freshly satisfied
before any Unity MCP call.
