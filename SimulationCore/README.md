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

Gameplay domains enter only through later accepted packets and decisions.
The folder is also a local Unity package shell, but it is not linked into
`Game` until the creator approves that offline project dependency.
