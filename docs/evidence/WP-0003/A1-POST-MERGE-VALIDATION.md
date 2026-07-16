# WP-0003 A1 post-merge validation

- Protected main commit: `c4513cd660dc3a522eb93ccec32f4f9b12c06489`
- Active autonomy: `A1`
- Active packet: `WP-0003`
- Entry gate: `local_development = passed`
- Reservation: `held`
- Boundary manifest SHA-256: `f495286de7c1ef4b762de8fb8e4603e59c5eaee7b0cf0c27bb6c73268760722b`
- Activation receipt: `RR-WP0003-ACTIVATE-20260716`
- Foundation workflow: `29530167032` — success
- Local foundation lint: pass
- Local control-plane tests: 124 passed, 1 skipped
- `AGENTS.md`: 192 lines
- Local compiler: Unity-bundled .NET SDK `8.0.318`
- Compiler launcher SHA-256: `635898abd14a453117adbdbb45460fb0a3a55dd2b99eb38982bddd12b8d0649b`
- Roslyn `csc.dll` SHA-256: `27d007d0b5a269c9b9549ba0faeafaae5df693114465f4a551207a780689b9a2`

No Unity, Unity Hub, Editor executable, relay, or MCP tool was invoked during
activation or this post-merge validation. Repository/bootstrap work may begin.
The first Unity MCP call remains blocked until all six manifest preconditions
are freshly satisfied for the exact `Game` project.
