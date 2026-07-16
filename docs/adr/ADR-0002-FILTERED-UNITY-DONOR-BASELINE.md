# ADR-0002: Use the existing Unity project as a filtered donor

Status: **candidate under WP-0003; creator review required**

## Context

The creator selected the existing `AC-21/Sashas` Unity project as the donor for
the canonical `Game` project and authorized a curated initial package candidate.
The exact dependency diff remains creator-review-gated. The
donor is a Unity URP template with useful version, package, project-linkage, and
render-pipeline evidence, but it also contains tutorial/sample content, broad
template dependencies, generated local state, and ten dirty changes while open
in Unity.

A wholesale copy would import unrelated decisions and mutable local state. A
fresh template would discard the creator's selected donor and repeat the same
package/bootstrap work.

## Decision

Use the donor only as a hash-bound, filtered seed:

1. Read source bytes from the exact Git commit, plus a stable selected patch;
   never copy caches or mutable live Editor output.
2. Keep only the PC URP renderer/pipeline/global settings and generic project
   settings needed to open the project.
3. Normalize title, company, bundle identity, text serialization, build scene
   list, services, cache, collaboration, and retained URP references.
4. Replace the donor product GUID with a fixed temporary development
   GUID and clear donor cloud-project and organization IDs. Durable save bytes
   remain disabled while identity design is open.
5. Pin the curated direct package set and its exact reachable donor-lock
   closure. Exclude speculative or template-only packages.
6. Apply URP 17.5's deterministic renderer-data migration from asset version 2
   to 3 and remove its obsolete probe/native-render-pass fields without opening
   Unity.
7. Keep SimulationCore and SaveContracts unlinked until their license and local
   package link receive separate creator review.
8. Bind every `Game` file in a manifest and validate the closure in the existing
   WP-0003 required check.

## Consequences

- The canonical repository remains the sole implementation repository; the
  donor remains untouched and independently reversible.
- The first canonical Unity open has a smaller, inspectable import surface.
- The Assistant preview and its module coupling remain explicit risks.
- Project analytics/cloud flags are off, but global Unity Editor Analytics is
  outside project YAML and remains a first-use verification item.
- The filtered lock is a candidate until Unity opens the canonical project and
  proves import/compile behavior.
- No scene, input asset, camera, debug UI, or gameplay decision is smuggled into
  this baseline; those remain later WP-0003 work.
- A package, identity, or project-settings change must update the manifest and
  pass creator review rather than silently normalizing in Git.
