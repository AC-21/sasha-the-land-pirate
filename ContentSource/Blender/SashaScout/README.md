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
- C1 source keeps the stable content ID but deliberately uses
  `LOD0_FIRSTLOOK` and inserts `SUSPENSION_*` transforms. Its manifest binds an
  explicit, not-yet-integrated compatibility map to the current C0 runtime root
  (`veh_sasha_scout_a [C0 Blockout]`), LOD
  (`LOD0_C0_BLOCKOUT`), and wheel-name binding. No drop-in Unity compatibility
  is claimed.
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

The first command generates only under `Derived/Quarantine/Staging/Current`,
reopens the staged `.blend`, validates its manifest and generator/README hashes,
binds the compatibility map to the current runtime C# contract, reimports the
staged GLB, and promotes the complete package with the manifest last. A failed
attempt remains quarantined; the next attempt moves it under
`Derived/Quarantine/Failed/` before starting clean. Published outputs therefore
cannot be mistaken for a valid mixed generation after an interrupted run.

The second command independently reopens the published `.blend`, binds its
content ID/source version/root properties and current generator hash to the
manifest, then repeats the export proof. It verifies semantic names, parent
hierarchy, local transforms, material and triangle counts, plus all four named
12-triangle collision proxies after GLB reimport. Revalidation clears only its
unsaved background process.

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
