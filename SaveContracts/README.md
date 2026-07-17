# SaveContracts

WP-0003 defines only a disabled persistence capability:

- report `SaveCapability.Disabled`;
- reject persistence with `WP0003_PERSISTENCE_DISABLED`;
- report zero bytes written.

This package currently contains no file, directory, stream, serializer,
database, cloud, slot, envelope, migration, or byte-layout implementation.

WP-0002 proposes one disposable, versioned `Runtime/LastBearing/` exception for
the first playable. If separately accepted and activated, its exact profile is
`last-bearing-dev-v1`: the Game-side Unity-runtime adapter exposes no arbitrary
root constructor, derives only that fixed child beneath
`UnityEngine.Application.persistentDataPath`, and then delegates persistent
writes through the engine-independent SaveContracts seam. Loading may discover
only `current` and `last-good` inside that child. Sibling enumeration, path
traversal, direct agent/host writes, unknown-version reinterpretation,
migration, and rewriting WP-0001 fixtures all fail closed. The profile makes no
durable-envelope or production-compatibility promise.

These are proposed constraints, not active implementation authority. WP-0002
proposes linking this package from `Game/Packages/manifest.json` only as
`file:../../SaveContracts`; the package manifest itself remains protected and
byte-identical. Every current importable file and folder has deterministic,
hash-frozen Unity metadata. Future scope may add only the exact
`Runtime/LastBearing.meta` sibling and paired deterministic metadata inside
`Runtime/LastBearing/`; other sibling metadata remains denied.
