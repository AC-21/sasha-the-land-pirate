# ADR-0001: Deterministic core before Unity presentation

Status: accepted for WP-0003 bootstrap only

## Context

WP-0003 must prove an engine-independent deterministic seam before the Unity
project exists. Gameplay rules, save bytes, production packages, and engine
ratification remain outside this packet.

## Decision

- `SimulationCore` targets `netstandard2.1`, uses checked integer fixed-step
  transitions, and has no `UnityEngine` dependency.
- Canonical technical state hashes exact ordered little-endian integer bytes
  only as a private determinism oracle. The byte encoder is not a public API
  and is not a save format.
- State, events, transitions, projections, and run results are emitted by the
  kernel; consumers may construct commands but cannot fabricate authoritative
  outputs.
- `SaveContracts` exposes only a disabled persistence capability and result;
  it defines no snapshot, capture, restore, serializer, path, stream, version,
  migration, envelope, or byte-schema API.
- Both libraries are valid local Unity package shells with
  `noEngineReferences`. At ADR-0001 acceptance, `Game` had an empty dependency
  graph and did not link them. ADR-0002 supersedes that baseline package state;
  local links to these libraries still require separate creator approval.
- A package-free console test runner compiles both libraries and verifies a
  frozen golden hash, repeated process output, clean repeated DLL hashes, and
  identical DLLs from two independently initialized checkout roots.
- Local validation uses the already-installed Unity-bundled .NET 8 SDK without
  starting Unity and pins SDK `8.0.318` plus exact launcher and Roslyn hashes.
  CI uses an installed SDK in the explicit `8.0.3xx` compatibility band on
  GitHub's `ubuntu-24.04` image and performs an offline restore with no package
  sources or runtime installation. These are compatible compiler lanes, not a
  claim that local and hosted runner compiler binaries are identical.

## Consequences

Unity presentation can later adapt these assemblies without owning canonical
state. The seam proves deterministic mechanics, not game design, fun, save
compatibility, Unity import, Play Mode, or production performance.

## References

- [Unity custom package layout](https://docs.unity3d.com/6000.0/Documentation/Manual/CustomPackages.html)
- [Unity assembly definition format](https://docs.unity3d.com/6000.0/Documentation/Manual/assembly-definition-file-format.html)
- [GitHub Ubuntu 24.04 runner inventory](https://github.com/actions/runner-images/blob/main/images/ubuntu/Ubuntu2404-Readme.md)
