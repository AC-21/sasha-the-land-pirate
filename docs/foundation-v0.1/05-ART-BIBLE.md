# Art Bible and Asset Pipeline

Version: 0.1 draft\
Working visual phrase: **storybook salvage**\
Installed tool observed: Blender 5.1.2

## 1. Visual promise

The world should look harsh enough to demand ingenuity and warm enough to deserve saving.

The city begins as exposed survival machinery and gradually becomes a lived-in civic place: patched shade, laundry, planted food, painted signs, lamps, water, workshops, music, personal objects, and faction influences. The road remains broad, dangerous, and beautiful. Vehicles look like maintained companions with jobs and histories.

Human-only, robot-only, and mixed colonies must each read as inhabited. Robots are original, endearing humanoid utility machines whose design communicates task, upkeep, adaptation, and individual history at the gameplay camera. Tesla Optimus is a category reference supplied by the creator, not a model sheet, brand license, naming source, or silhouette target.

## 2. Six visual laws

### A. Silhouette → function → history

At the normal camera, first read what an object is, then what it does, then how it has been repaired or personalized. Detail that reverses this hierarchy is noise.

### B. Maintained, not randomly ruined

Repairs explain continued function: braces carry loads, hoses connect systems, plates cover damage, welds follow seams, soot follows exhaust, dust follows wind. Uniform edge wear and random decals are forbidden.

### C. Chunky mechanical legibility

Use slightly exaggerated proportions, visible weight, strong joints, readable tanks, belts, intakes, rotors, cargo, and moving mechanisms. The physicality may feel like a crafted miniature, but not plastic or weightless.

### D. Hope accumulates visibly

Prosperity adds life rather than only enlarging buildings: color, plants, cloth, light, gathering places, art, maintained roads, clean water, and purposeful motion.

### E. Factions are structural

Each faction receives a matrix of shape language, palette, materials, construction logic, repair logic, motif, and motion. Shared gameplay footprints and sockets may be dressed differently; a hue swap alone never counts as a faction variant.

### F. Strategy-camera truth

Assets are approved in the actual game camera with UI visible. A Blender beauty render cannot overrule failed readability, LOD, lighting, collision, or performance.

## 3. Provisional visual grammar

- sun-bleached earth, concrete, faded paint, and warm dust as the environmental base;
- selectively saturated repair panels, cloth, signs, water, plants, and lights;
- rounded or softened human-touch forms against more severe ruined infrastructure;
- roofs and top-facing surfaces communicate function because the player sees them often;
- face-like vehicle silhouettes may create affection, but literal cartoon faces are not assumed;
- danger uses scale, motion, weather, sound, emptiness, and material contrast more than gore;
- night preserves function hierarchy with practical lights and restrained emissive color.

Avoid ubiquitous spikes, skulls, brown sludge, random cyberpunk neon, pristine sci-fi panels, toy-scale clutter, franchise-coded silhouettes, and unreadable photoreal microdetail.

## 4. Camera contract

Lock and version four reference cameras before detailed asset production:

1. normal city gameplay;
2. maximum city overview;
3. close inspection;
4. vehicle garage.

Starting hypothesis: 35–45° elevation and 35–45° perspective FOV, tuned in-engine. Every asset is reviewed at 1920×1200 with representative UI plus the creator-machine native target.

At normal zoom:

- a standard building's function survives a roughly 64–128 px silhouette;
- doors, tanks, cargo, hazards, and interactable mechanisms use distinct value/shape grouping;
- status never depends on hue alone—pair color with motion, emissive, smoke, flags, fill level, or silhouette;
- small bolts, scratches, and wires belong in textures unless they change silhouette;
- upgrade stages remain recognizable as the same building while visibly changing capability.

## 5. Modular kit contract

- `1 Blender unit = 1 metre`.
- Start with a 2 m construction module; gameplay placement may use larger multiples.
- Building pivot: documented southwest grid corner at ground height.
- Prop pivot: bottom center.
- Vehicle pivot: ground center between axles, with forward-axis calibration asset.
- Apply transforms and validate normals, UVs, tangent space, and triangulation before export.
- Author foundations, straight walls, corners, doors, roof center/edge, pipes, gantries, ladders, awnings, and upgrade/damage overlays.
- Reuse simulation footprints and sockets where faction variation does not require mechanical difference.

Socket examples:

- `SOCKET_FRONT`
- `SOCKET_ROAD_01`
- `SOCKET_WORKER_01`
- `SOCKET_INPUT_01`
- `SOCKET_OUTPUT_01`
- `SOCKET_VFX_SMOKE`
- `SOCKET_UPGRADE_ROOF`
- `SOCKET_MODULE_WINCH`

Collision/navigation helpers:

- `COL_*` simple box or convex proxies;
- `NAV_BLOCKER_*` placement/navigation footprint;
- explicit entrances and worker standing points;
- simple vehicle chassis hull and wheel-contact metadata;
- `OCC_*` large occlusion proxies where the engine path benefits.

## 6. Materials

Start with four shared shader families:

1. opaque trim-sheet/atlas PBR;
2. decals and projected markings;
3. alpha-clipped foliage/cloth/fences;
4. glass/emissive/status materials.

Rules:

- shared 2k trim/atlas sheets per kit;
- 1k typical props, 2k vehicles/hero buildings, 4k only for a justified landmark;
- packed occlusion/roughness/metallic maps where supported;
- vertex or mask channels for faction paint, dust, soot, wetness, and condition;
- alpha clip over alpha blend whenever possible;
- baked normals/AO for rivets, welds, seams, and panel relief;
- engine-side weather/status overlays; do not bake every state into a unique material;
- agents cannot create new master shader families without an accepted proposal.

### Original humanoid-robot law

- No Tesla wordmarks, `T` marks, Optimus name, product proportions, face/display treatment, surface breakup, promotional colorway, or other recognizable trade dress enters prompts or shipped assets.
- Begin from the game's own chunky, field-repairable shape language: replaceable shells, visible service access, job-specific hands/tools, dust protection, patched soft goods, painted civic marks, and readable condition.
- A robot's silhouette first communicates its colony role, then its chassis family, then its accumulated repairs and affiliation.
- Human and robot residents share the world's warmth and authored wear without making robots metal humans or humans palette-swapped robots.
- Any generated robot remains quarantined until an originality review compares it against branded humanoid-robot references and records the result.

## 7. Starting mesh budgets

| Class | LOD0 | LOD1 | LOD2 | Typical material rule |
|---|---:|---:|---:|---|
| Small prop | 0.5–3k tris | 0.2–1k | cull or 0.1–0.3k | one atlas material |
| Modular piece | 2–6k | 0.8–2.5k | 0.2–0.8k | shared kit material |
| Standard building | 8–20k | 3–8k | 0.8–2.5k | one or two shared materials |
| Hero building | 30–60k | 12–25k | 3–8k | maximum three materials |
| Vehicle | 20–35k | 8–15k | 2.5–6k | one 2k set, two or three materials |
| Character | 10–18k | 5–9k | 1.5–4k | one 2k set |

These are intake warnings, not permanent truth. Captured GPU/CPU frames on the target Mac decide final budgets.

## 8. Vehicle and animation contract

The first vehicle rig contains:

- root;
- four wheel bones or stable wheel transforms;
- steering pivots;
- axle/suspension controls;
- door/tool transforms;
- stable cargo and upgrade sockets.

Runtime owns wheel rotation, steering, suspension, dust, damage response, and physics. Authored clips cover idle vibration, doors, deployable tools, and other deliberate mechanisms.

The initial shared human-survivor rig needs only:

- `Idle_Loop`
- `Walk_Loop`
- `Carry_Loop`
- `Work_Loop`
- `Celebrate`
- `Weather_Idle`

The initial robot rig may share clip semantics but not an assumed human skeleton. Its minimum set is `Robot_Idle_Loop`, `Robot_Walk_Loop`, `Robot_Carry_Loop`, `Robot_Work_Loop`, `Robot_Service_Loop`, and one readable condition/failure state. Tool endpoints and service-panel sockets are explicit, stable transforms.

Simulation-controlled characters use in-place locomotion by default. Buildings use transform/bone animation for pumps, fans, shutters, cranes, and belts; particles and materials remain engine-side.

## 9. Canonical pipeline

```text
brief → blockout → gameplay-camera gate → modeled source → UV/material → rig/LOD/collision
→ scripted export → quarantine import → automated validation → engine lineup scene
→ art review → runtime/performance gate → accepted manifest
```

- Blender `.blend` is the editable canonical source.
- `.glb` is the preferred portable interchange candidate because it carries PBR materials, transforms, and animation; the engine spike must ratify the exact importer. FBX may remain a tested fallback for rigged assets.
- Generated interchange, thumbnails, and reports are never hand-edited.
- Pin one production Blender version and one scripted export preset after the calibration spike. The installed 5.1.2 is not automatically ratified merely because it is present.
- Convert/bake unsupported procedural geometry and shader behavior before export.

Blender's glTF exporter supports core metallic/roughness PBR, animations, and custom-property extras, but not every Blender procedural feature transfers. [Blender glTF documentation](https://docs.blender.org/manual/en/latest/addons/import_export/scene_gltf2.html)

## 10. Asset package

Every accepted package contains:

- canonical `.blend`;
- generated interchange artifact;
- `.asset.json` manifest;
- validation report;
- normal/overview/inspection/garage renders as applicable;
- source texture files and licenses;
- engine prefab/definition generated from the manifest;
- content ID, semantic version, owner, and acceptance record.

Every source, texture, license, raw AI output, prompt record, generated artifact, report, and camera sheet carries a SHA-256 digest, byte size, and media type. Manifest paths are package-relative, cannot contain `..`, and are resolved inside a fixed package root; symlinks and path escapes are rejected. Acceptance is derived from protected provenance/art/runtime receipts rather than an agent-set label.

Naming examples:

- `bld_water_condenser_a_lod0`
- `veh_scout_buggy_a_lod1`
- `prop_scrap_crate_b`
- `COL_bld_water_condenser_a`
- `SOCKET_UPGRADE_CARGO_01`

## 11. AI-assisted asset law

Tripo and similar tools default to **concept/blockout**, not shipping authority. An output can ship only after:

- input and output provenance review;
- commercial-rights and account-tier review captured at generation time;
- retopology and silhouette authorship;
- UV and material normalization;
- removal of franchise or brand resemblance;
- collision, sockets, LODs, and animation cleanup;
- gameplay-camera lineup review;
- performance validation;
- human art-direction approval.

For every AI-assisted asset, preserve:

- tool, model/version, account tier, date, prompt, seed/task ID;
- untouched raw output;
- every input image and its source/license;
- terms URL and archived terms hash;
- human author and modification notes;
- `prototype-only` or `ship-cleared` status.

Do not use living-artist names, franchise designs, branded vehicles, or unlicensed references in prompts. Do not upload confidential concepts until service privacy terms are accepted.

Unknown `.blend`/generated files are processed in an OS sandbox with factory settings, script/auto-execution disabled, no inherited network access or credentials, read-only input, bounded CPU/RAM/time, and scratch-only output. Reject symlinks and external-path dependencies before an accepted source tree is touched. Tripo's current terms distinguish rights by plan and can change; record the exact terms rather than relying on memory. [Tripo terms](https://www.tripo3d.ai/terms)

## 12. Drift control for asset agents

Before parallel asset production:

1. Approve one golden building and one golden vehicle.
2. Publish palette, material, decal, wear, camera, scale, pivot, socket, and budget IDs.
3. Give each agent a bounded brief with required silhouettes and reference views.
4. Require blockout review before detail work.
5. Compare every submission in the same engine lineup scene and contact sheet.
6. Reject new master materials or faction rules created inside an asset task.
7. Preserve every accepted version; never destructively replace the only source.

The art director evaluates silhouette, function read, value structure, wear logic, faction fit, animation purpose, and normal-camera appeal—not only isolated render polish.

## 13. First 12 asset packages

1. Settlement core / civic beacon with three operating states.
2. Modular settlement shell kit yielding shelter, storehouse, and canteen.
3. Water turbine/condenser with fill and failure states.
4. Scrap sorting yard with input/output states.
5. Compact growhouse.
6. Salvage-fuel generator/processor.
7. Garage and machine shop with upgrade bay.
8. Road and settlement-yard kit.
9. Scout utility vehicle with driving rig and three condition states.
10. Vehicle module kit: winch assembly and sealed range tank, each with installed/deployed/condition states.
11. Resident pair: one human survivor worker with two outfit variants and one original humanoid utility robot with job-tool variants; each has the bounded locomotion/work clips needed to prove human-only, robot-only, and mixed staffing.
12. Ruined transit depot with tool interactions, hazard, faction meeting point, and depletion states.

The first faction receives a small structural/dressing kit applied to this slice. Do not build multiple complete faction architecture libraries before the loop is accepted.
