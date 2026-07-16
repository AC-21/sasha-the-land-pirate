# WP-0003 filtered Unity donor baseline evidence

Status: **candidate on a protected-PR branch; not creator-merged or Unity-verified**

## Objective and authority

This change replaces the empty `Game` seed with a text-inspectable Unity
`6000.5.4f1` baseline derived from the creator's existing project while keeping
the canonical repository at `AC-21/sasha-the-land-pirate`.

The creator's explicit 2026-07-16 instruction was:

> Use Sashas as the donor project with the curated package set. - And yes use
> the packages we need initially.

That instruction authorizes this dependency candidate and filtered donor
import. It does not authorize dependency auto-merge; creator review remains
required before this branch enters protected `main`.

## Frozen source and provenance

- Donor repository: `https://github.com/AC-21/Sashas.git`
- Donor commit: `496c60b978741c03c476860ab83d1aadc215c961`
- Donor commit tree: `da74bb0ccee61bd9b38abb09e67bc8e9eb4ef42e`
- Donor path during the immutable snapshot: `/Users/sasha/Sasha the Atomic Land Pirate`
- The same path existed at the final pre-commit provenance check; local paths
  are observational and non-authoritative.
- Editor tuple: `6000.5.4f1 (d550df8bd089)`
- Immutable selected-source tar SHA-256:
  `a9d40bbf99125faf6b7c4abe09844d64acdc6d3ead38fe562e9c9569c48037ec`
- Immutable selected-source tar size: `225280` bytes
- Full dirty-worktree diff SHA-256:
  `85752c18d7645dd5fccbbea53aeb05df3d80a0ef4504622748d2f736888c8fac`
- Selected stable patch SHA-256:
  `e8c6050453fa60825a1e2695227d540a587e528adbcff94f3989fe8f1ff75a5c`
- Donor manifest SHA-256:
  `bc0a9207b56a53b5dc00045eb057dcf10bec4558bec7ae97039b3c56ae1d2e59`
- Donor lock SHA-256:
  `6a97aea36ac4663cdfa912cf7067a9d98d3c9d9291cffce285a9ca391011bd00`

The donor was open in Unity during inspection. No live project file was copied
directly into the candidate. The import came from `git archive` at the exact
commit plus a selected dirty patch whose bytes matched two consecutive reads.
The original donor working tree was not modified, reset, staged, or committed.

The archive reproduces byte-for-byte with:

```text
git archive --format=tar --output=<OUTPUT> 496c60b978741c03c476860ab83d1aadc215c961 -- Assets/InputSystem_Actions.inputactions Assets/InputSystem_Actions.inputactions.meta Assets/Settings.meta Assets/Settings Packages ProjectSettings
```

The six selected immutable paths, exact command, byte count, and digest are
also machine-bound in the manifest.

## Imported and normalized

The candidate keeps only:

- standard Unity project settings, normalized for the canonical project;
- the PC URP renderer, PC pipeline asset, and URP global settings with their
  `.meta` GUIDs;
- a curated Unity manifest and the exact reachable closure filtered from the
  donor lock; and
- a distinct temporary development identity with no donor cloud-project or
  organization linkage.

Identity is development-only:

- company: `AC-21`
- product/project: `Sasha the Atomic Land Pirate`
- product GUID: `a6426107030e53cca527036b78d7a7e3`
- bundle identifier: `com.ac21.sasha.atomiclandpirate`
- cloud project: empty
- organization: empty
- status: temporary development only; durable save bytes remain disabled

The candidate removes donor identity/linkage, template identity, sample-scene
references, generic input actions, missing volume-profile references, the
mobile render-pipeline asset, template/console platform identifiers, and the
stale `SENTIS_ANALYTICS_ENABLED` define.
It applies URP 17.5's documented renderer migration from asset version 2 to 3:
the opaque mask is copied to the prepass mask, and the obsolete probe-resource
and native-render-pass fields are removed. Both quality tiers point at the
retained PC URP asset; the Mobile tier is excluded from Standalone and is not a
performance claim.

## Initial direct package set

| Package | Version | Reason |
| --- | --- | --- |
| `com.unity.ai.assistant` | `2.14.0-pre.1` | Candidate Codex/Unity assistant integration; pinned preview |
| `com.unity.ai.navigation` | `2.0.13` | Initial navigation authoring/runtime seam |
| `com.unity.inputsystem` | `1.19.0` | Technical camera and interaction input |
| `com.unity.render-pipelines.universal` | `17.5.0` | Mac-first render pipeline |
| `com.unity.test-framework` | `1.7.0` | EditMode/PlayMode test seam |
| eight `com.unity.modules.*` entries | `1.0.0` | Exact Assistant/technical-slice compile closure |

The eight direct built-in modules are audio, image conversion, IMGUI, 3D
physics, 2D physics, Unity analytics assembly support, UnityWebRequest audio,
and video. Project analytics services are disabled; the module is present only
because Assistant assemblies reference it. Global Unity Editor Analytics is
account/Editor state outside this project YAML and remains a first-use check.

AI Inference, Collab, IDE integrations, Multiplayer Center, Timeline, uGUI,
Visual Scripting, XR, and the donor template's remaining broad module list are
excluded. The repository-local SimulationCore and SaveContracts packages are
also still unlinked.

Candidate package lock SHA-256:
`9840ac57af6738620a6efc6c50bfbaf2f758a71e7fd8fd148fed5fae202ebf5b`.
Canonical direct-package-map SHA-256:
`45d64e2107723c13d0bba45beba73dbc54e02ba826f3bf30219cde9ddc0c4f3e`.
It is a deterministic shortest-path closure filtered from the donor lock, not
yet an Editor-regenerated or compile-verified lock.

## Explicit exclusions

No donor `.git`, GitHub binding, tutorial/readme/layout asset, sample scene,
generic input asset, mobile renderer/pipeline, volume profile, Assistant-local
checkpoint settings, resolver script, generated IDE project, cache, log,
`Library`, `Temp`, `UserSettings`, relay state, or MCP connection state is in
the candidate.

## Validation boundary

`docs/manifests/WP-0003-unity-donor-v1.json` binds every `Game` file by path,
size, and SHA-256. `Tools/run_wp0003_core_tests.py` now fails closed on:

- file-closure, hash, symlink, path traversal, case collision, or generated
  Unity-state drift;
- missing/duplicate project `.meta` GUIDs, missing retained project URP
  references, or reintroduction of the seven obsolete unresolved probe GUIDs;
- template/donor paths, identities, excluded GUIDs, or sample content;
- any package map outside the exact 13-package digest, non-Unity/scoped
  registries, URL/path package values, malformed lock entries, incompatible
  dependency versions, unreachable lock entries, depth drift, or unapproved
  prereleases;
- identity, text serialization, diagnostics, cache, version-control, and
  project cloud-service drift; and
- manifest narrative/attestation drift through an independent manifest digest
  pinned in the validator.

Manifest SHA-256:
`556717ee9e1829dce251d73a08aba79cc8b7a4091313103968a3c5218637dc1a`.

Local validation results:

- Unity-baseline file, package, GUID, identity, and generated-state checks:
  passed.
- Independent static GUID audit: 176/176 retained references resolved against
  the four project metas, donor PackageCache entries for the exact locked
  versions observed during this audit, or built-in zero GUIDs after the
  obsolete renderer block was removed. Those PackageCache bytes are not
  candidate-hash-bound; this is not Editor import or compile proof.
- Deterministic technical tests: 11 passed, 0 failed.
- Offline restore/build: passed with 0 warnings and 0 errors.
- Repeated-process output: byte-identical.
- Same-root clean-build DLL hashes: byte-identical.
- Independent-checkout source commit:
  `6446d291e6015032386871f6f4bb395ba8c790d1` in both roots.
- Independent-checkout DLL hashes: byte-identical.
- Foundation lint: passed.
- Control-plane tests: 124 passed, 1 skipped.
- `AGENTS.md`: 192 lines, below the 200-line gate.
- JSON parsing and `git diff --check`: passed. The repository-bound
  `Game/.gitattributes` recognizes Unity's empty-scalar YAML convention only
  for `.asset` and `.meta`; all other candidate paths retain normal checks.

An early pre-hardening full-suite attempt failed after 120 seconds because the
sandbox denied MSBuild's local named-pipe bind. The preserved failure showed
`System.Net.Sockets.SocketException (13): Permission denied`; no source gate
failed. Its immediate unchanged rerun passed outside that IPC restriction. The
final hardened staged validator was then rerun in that permitted local-IPC lane
and passed. No package, SDK, or tool was installed.

Same-root DLL SHA-256 values:

- SimulationCore:
  `76cc709b93c9187e1c2064ceb7d93c8cff4d83620feca4f362628665c4c3471f`
- SaveContracts:
  `aa0d12104167d8d21ebf47fbc4eb74083df5e8606ac4ff50944fb11fdb61ee6c`
- CoreTests:
  `360d76b9992d5b316599e1292c8d8c76fa5fb2149d797e573d8a7ce930cb49be`

Independent-checkout DLL SHA-256 values:

- SimulationCore:
  `32795101594f1ae167192a91600db2d32e8cb43e32fd9668f7a32692978afb19`
- SaveContracts:
  `40cdac7349697c6b2be03056f67a637d1bf3176aeb3d4edeeecac8554a2b998a`
- CoreTests:
  `72ca270053b895e10a7ab2b6fe5a50799c175098f81b06d7c37088e853ecbc3b`

No agent invoked Unity Hub, Editor, relay, or Unity MCP for this candidate.
Unity compilation, import, EditMode, PlayMode, scene, camera, debug UI, and
screenshots remain unverified. Global Unity Editor Analytics state is also not
proved by project files. The first-use gate remains closed.

## Rollback and next action

Rollback is an ordinary revert to
`75514781219ceed101a96409913bf483ff0b38b2` or deletion of this unmerged branch.
No donor mutation or history rewrite is required.

After creator review and merge, the creator opens exactly the canonical
`Game` folder. Package resolution and any Editor-owned normalization are then
reviewed as a new diff before the first Unity MCP call. The six first-use
conditions still apply to that canonical target.
