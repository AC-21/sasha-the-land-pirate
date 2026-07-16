# WP-0003 bootstrap known limits

- `Game` now contains only a text seed for Editor `6000.5.4f1` and an empty
  package dependency graph; it has not been opened.
- No package lock, generated project settings, `.meta` files, scene, prefab,
  material, camera, debug UI, EditMode test, or PlayMode test is claimed.
- No Unity, Hub, Editor executable, relay, or MCP tool has been invoked.
- The installed project observed elsewhere is not copied and does not select
  Sasha's package graph.
- The creator must open the exact repository `Game` project, review the
  generated lock/settings/meta files, and satisfy all six first-use conditions
  before Unity MCP work begins.
- The local SimulationCore and SaveContracts packages are not yet linked into
  the project; that dependency change remains creator-gated.
- Package license metadata is intentionally unresolved; the creator must select
  it before either local package is linked, distributed, or published.
- The technical canonical string and hash are test oracles, not save bytes.
- Save capture, restore, serialization, migration, slots, paths, and bytes are
  deliberately undefined; the only concrete port rejects persistence.
- GitHub-hosted runner software can drift; the workflow reports its SDK and
  fails rather than installing a replacement.
