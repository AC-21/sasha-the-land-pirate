# WP-0003 Unity package inventory and removal plan

Status: **local-development candidate; not a distribution/license acceptance**

All external packages in the candidate come from Unity's registry at
`https://packages.unity.com`; the remaining entries are built into Editor
`6000.5.4f1`. No Git, tarball, file, embedded, or scoped-registry package is
allowed.

The direct package inventory is bound in
`docs/manifests/WP-0003-unity-donor-v1.json`. This document does not interpret
or accept distribution terms. Before any public build or release, the creator
must review the exact Unity/package terms in force for the selected versions.

Removal plan:

- AI Assistant and its compile-support modules can be removed together after
  the Unity MCP development route is replaced or no longer needed.
- AI Navigation can be removed until a bounded navigation slice consumes it.
- Input System can be removed only with a replacement input contract.
- URP can be removed only with a separately reviewed render-pipeline migration.
- Test Framework remains development-only and can be excluded from a player
  build after test assembly boundaries are proven.
- Built-in modules are re-audited after every Assistant version change; a
  module with no reachable compile/runtime consumer is removed.

`SimulationCore` and `SaveContracts` remain unlinked. Their package license and
distribution posture require a separate creator decision before a `file:`
dependency is added.
