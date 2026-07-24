# Sasha Scout first-look source

This package is a **prototype-only C1 first look** for Sasha's four-wheel scout.
When generation succeeds, `veh_sasha_scout_firstlook.blend` is the canonical
source; the GLB, turntable, camera reads, and validation report under
`Derived/Quarantine/` are generated review evidence, not shipping authority.
The current commit intentionally contains no binary outputs: Blender 5.1.2
crashed twice in Metal-device detection before the generator ran, so that
harness path was quarantined and the source manifest fails closed.

The silhouette is an original compact field-service machine: a wide maintained
chassis, a clean physical cargo bed, and a front recovery **proscenium** that
reads as Sasha's unmistakable tool without becoming a weapon, decorative spike
array, or copied crane. A single ochre repair panel gives the vehicle a human
history without strategy-camera clutter.

## Axis and semantic contract

- `1 Blender unit = 1 metre`.
- Blender source: `+Y` forward, `+Z` up.
- Runtime intent: `+Z` forward, `+Y` up.
- Runtime `(x, y, z)` maps to Blender `(x, z, y)`.
- Root pivot: ground center between the axles at `(0, 0, 0)`.
- Contact stations, steering/hub pivots, suspension transforms, cargo/tool/
  upgrade sockets, `DOOR_DRIVER`, and module roots match
  `SashaScoutSemanticContract`.
- Collision is four simple render-disabled boxes, independent of presentation
  geometry.

## Regenerate and validate

From the repository root:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --factory-startup --python \
  ContentSource/Blender/SashaScout/generate_sasha_scout.py

/Applications/Blender.app/Contents/MacOS/Blender --background \
  ContentSource/Blender/SashaScout/veh_sasha_scout_firstlook.blend \
  --python ContentSource/Blender/SashaScout/generate_sasha_scout.py -- \
  --validate-existing
```

The second command reopens the saved `.blend`, validates names and local
transforms, enforces triangle/material/collision bounds, checks the GLB header,
clears the unsaved validation process, reimports the GLB without global-name
collisions, and verifies semantic names survive export.

## Deliberate hard cuts

- No Unity integration or runtime change.
- No final UVs, textures, baked maps, decals, wear, LOD1/LOD2, module art,
  animation, or production suspension rig.
- No Tripo mesh was extracted or imported.
- The user-provided PNG concepts informed only broad functions: recovery,
  cargo, wheel contact, and affectionate repair. Their repeated weapons, spikes,
  skulls, flags, cranes, branded expression, and maximal clutter were rejected.
- This source still needs creator art review, gameplay-camera review in Unity,
  reference-removal review, and target-Mac runtime/performance approval.
