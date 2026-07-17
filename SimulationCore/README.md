# SimulationCore

Engine-independent deterministic rules for WP-0003.

Current scope is deliberately technical and non-gameplay:

- one fixed-step state transition;
- explicit command sequence validation;
- one technical domain event and one read-model projection per transition;
- checked integer arithmetic;
- invariant canonical formatting;
- SHA-256 over an exact 24-byte little-endian technical-state vector;
- kernel-owned state, events, transitions, and run results;
- immutable event history exposed without a castable backing array;
- no `UnityEngine`, rendering, filesystem, network, clock, or randomness
  dependency.

WP-0002 proposes a bounded `Runtime/LastBearing/` extension for the first
playable. If separately accepted and activated, that extension may define the
engine-independent LastBearing commands, deterministic transitions, canonical
checkpoint state, and read models needed by the four preparation/module cases.
It remains free of `UnityEngine`, presentation, filesystem, and persistent-path
dependencies. The proposal does not activate gameplay or authorize work in this
package today.

The folder is a local Unity package shell. WP-0002 proposes linking it from
`Game/Packages/manifest.json` only as `file:../../SimulationCore`; the package
manifest itself remains protected and byte-identical. Every current importable
file and folder has deterministic, hash-frozen Unity metadata. Future scope may
add only the exact `Runtime/LastBearing.meta` sibling and paired deterministic
metadata inside `Runtime/LastBearing/`; other sibling metadata remains denied.
