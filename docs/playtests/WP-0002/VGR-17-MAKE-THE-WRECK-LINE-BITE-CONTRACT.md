# VGR-17 — Make the Wreck Line Bite

## Outcome

Give the existing authored road corridor an immediate handling grammar. Sasha's
rig should feel planted on the settlement apron, fight the collapsed shortcut,
bog on the exposed sand, and loosen on the gravel approach without creating a
second driving system or changing expedition authority.

## Player contract

The physical road under the wheels selects one existing fixed road-feel preset:

- `Route Apron` is `Concrete`;
- `Collapsed Short Branch` is `Washboard`;
- `Exposed Long Route A` is `Sand`;
- `Exposed Long Route B` is `Gravel`.

The presets already bound grip and rolling resistance. The four-contact rig
continues to report its dominant contacted surface through existing telemetry.
Default hardpack remains the fail-safe for untagged presentation colliders.

This increment changes local handling only. Rigidbody pose, wheel contacts,
surface kind, grip, rolling resistance, and telemetry never author route
progress, damage, time, cargo, resources, commands, saves, or outcomes.

## Presentation and authority contract

- The authored corridor owns exactly one surface tag on each named driveable
  segment; the standalone Road Feel Lab remains a separate test course.
- The shared chase camera, single Camera and AudioListener, pause suspension,
  fixed-road fallback, and explicit recovery behavior remain unchanged.
- Existing quantized driving commands remain the only road input that reaches
  deterministic simulation.
- No new physical ribs are added until a short canonical-project native smoke
  proves that the selected flat presets are readable and stable on the current
  narrow corridor.

## Acceptance and exclusions

Focused PlayMode coverage proves the exact four tags, contact telemetry on every
segment, canonical-state isolation across physics frames, and unchanged camera
and recovery boundaries. Static/current-source compilation and ordinary
repository checks cover the code-only worktree. Integration runs the relevant
PlayMode fixture and a short native drive with 3–5 city-to-road transitions in
the one canonical Unity project.

This increment adds no `SimulationCore`, `SaveContracts`, command, schema,
migration, scene, package, project-setting, dependency, production asset,
physics tuning, broad driving framework, new scavenging verb, or everyday
performance soak.
