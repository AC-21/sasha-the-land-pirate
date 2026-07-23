# Sasha the Atomic Land Pirate — Agent Guide

This is the practical operating guide for every engineering, design, art, and
QA agent. Build the game, protect player state, and keep the critical path moving.

## Mission

Build an endearing real-time post-apocalyptic city-builder in which Sasha turns wasteland salvage into a home worth returning to.

The game is:

- an isometric settlement builder and resource-management game;
- a third-person driving and vehicle-upgrade game;
- a wasteland scavenging game whose expeditions change the settlement;
- a colony story supporting humans, original humanoid utility robots, or both;
- a manufacturing and physical-goods trading game, with a caravan-administered goods exchange unlocked by world population;
- a native Apple Silicon Mac game with durable save/load.

The visual north star is **Texas iron × brutalist opera**, lit with **tungsten
over neon**. Aim for endearing, purposeful, weathered forms—not generic cyberpunk,
cowboy kitsch, or recognizable franchise imitation.

## Product north star

The current priority is the smallest complete playable loop:

1. Start or load a colony.
2. Place, connect, and staff useful buildings.
3. Produce and consume resources under visible pressure.
4. Enter the garage and upgrade Sasha's rig.
5. Drive to a wasteland site.
6. Scavenge cargo while facing a cost, hazard, or decision.
7. Return, unload, and materially improve the colony.
8. Save, quit, reload, and continue correctly.

Build this loop thinly before adding breadth. A rough complete loop is more valuable
than polished disconnected systems. Infrastructure exists to support the game.

Do not stop because a ticket, PR, or named milestone ends. Continue toward the
active user goal while a safe, useful next step remains.

## How agents work

- Lead with the intended player or developer outcome.
- Inspect the real baseline before planning or editing.
- Use the smallest coherent change; avoid speculative frameworks and broad cleanup.
- Make reversible decisions autonomously and record meaningful assumptions.
- Ask only when a choice is irreversible, materially changes the game's identity,
  risks user data, spends money, expands external access, or needs a secret.
- Push back when a simpler path produces the same outcome.
- Keep long validation off the critical path; continue independent product work.
- Parallel agents use isolated worktrees and disjoint file ownership. One integrator owns the final result.
- Do not create governance, receipt, schema, or control work unless it directly
  protects or unblocks the current product increment.

## Voice and code style

- Communicate directly, from first principles, with concise actionable updates.
- Take clear positions, explain tradeoffs, and push back when a simpler path wins.
- Prefer plain readable code, explicit dependencies, and names from the game domain.
- Avoid god objects, vague `Manager` abstractions, clever indirection, and premature generalization.
- Comments explain intent or constraints, never restate syntax.
- Tests assert player-visible behavior and authoritative state, not private implementation.
- Follow existing C# and Unity conventions unless changing them is the task.

## Load context progressively

Read only what the task needs:

- `docs/foundation-v0.1/00-GAME-CONSTITUTION.md` — identity and invariants
- `docs/foundation-v0.1/02-SYSTEM-MAP.md` — domain ownership
- `docs/foundation-v0.1/03-VERTICAL-SLICE.md` — slice and hard cuts
- `docs/foundation-v0.1/04-TECHNICAL-ARCHITECTURE.md` — runtime and saves
- `docs/foundation-v0.1/05-ART-BIBLE.md` — visual grammar
- `docs/foundation-v0.1/01-DECISION-LEDGER.md` — durable product decisions
- `docs/playtests/` — current feature contracts and playtest notes

Historical packets and receipts are audit material, not the default execution
path. Do not read the governance archive before ordinary implementation.

When sources conflict: current user instruction wins, followed by the game
constitution, accepted decisions, contracts, tests, implementation, and summaries.

## Repository map

- `Game/` — Unity presentation, authoring, input, camera, UI, and adapters
- `SimulationCore/` — engine-independent deterministic game rules
- `SaveContracts/` — save envelopes, sections, migrations, and compatibility
- `ContentSource/Blender/` — editable canonical 3D sources
- `ContentSource/Incoming/` — quarantined generated or external assets
- `Tests/` — scenarios, contracts, golden saves, and validation fixtures
- `Tools/` — focused build and validation tools
- `BuildArtifacts/` — generated local evidence; not source truth

Unity-generated `Library/`, `Temp/`, `Logs/`, `Obj/`, local builds, recovery files, and user settings do not enter Git.

## Engineering laws

- Keep authoritative simulation in plain C# without `UnityEngine`.
- Use commands → deterministic transitions → domain events → read models.
- Use fixed ticks, stable IDs, injected time/storage, and seeded named RNG streams.
  Render-frame delta is never authoritative gameplay time.
- Unity scenes, GameObjects, animation, UI, and raw physics are presentation; quantize physics outcomes before they enter canonical state.
- Prefer explicit data and inspectable content over hidden scene or binary
  truth.
- Add dependencies only when they materially reduce risk or implementation
  cost; document license and removal path.
- Optimize after measurement, not anticipation, except for known hot loops.
- Treat warnings introduced by the change as defects.

## Saves are gameplay

- Every playable milestone includes save/load; it is not end-stage polish.
- Never serialize a Unity runtime object graph as canonical state.
- Preserve previous readable generations and use crash-safe atomic writes.
- New authoritative fields require explicit defaults and versioning.
- Changed formats require deterministic, idempotent migrations and golden-save coverage.
- Unknown critical data fails closed without overwriting the last good save.
- Test saving at meaningful partial-progress points, not only ideal endpoints.

## Validation ladder

Use evidence proportional to the changed surface:

1. **Inner loop:** compile, static checks, and targeted tests for changed behavior.
2. **Gameplay PR:** relevant suites, short native smoke, player-path verification, and 3–5 repeated mode transitions.
3. **Milestone/nightly:** full native build, extended performance, save compatibility, and 100-cycle transition soak.

Rules:

- Run the full soak only after the milestone's final source hash, not after
  every edit.
- Documentation, copy, isolated content, and unrelated assets do not trigger
  unrelated performance gates.
- Do not weaken a valid threshold to obtain green.
- Give infrastructure failure one clean retry. On recurrence, fix or quarantine
  the harness; do not blindly repeat the soak.
- Preserve failed-attempt evidence, but do not let evidence production become
  the product.
- Validate on the target Mac at milestones. Quality budgets do not belong in the everyday edit loop.

## Unity workflow

- Prefer the official Unity CLI and fixed project commands for reproducible operations.
- Use visible Editor control only when the CLI cannot perform the required
  action.
- Before a gate, verify the exact project path, branch, Unity version, and
  active scene.
- Open only `/Users/sasha/Documents/Sasha the Atomic Land Pirate/Development/sasha-the-land-pirate/Game` in Unity or Unity Hub; keep one Editor session and all parallel Git worktrees code-only.
- On zero tests, duplicate TypeDB registration, or stale attestation, restart
  once in a clean session and rerun only the affected gate.
- Commit `ProjectSettings` changes only when intentional and reviewed.
- Never expose credentials or broaden tool access beyond this project.

## Art and generated assets

- Design silhouette → function → history; assets must read from the strategy camera.
- Texas iron means fabricated, load-bearing, heat-scarred utility. Brutalist opera means civic scale, procession, rhythm, and theatrical framing—not concrete boxes.
- Tungsten is warm functional light; neon is rare status, warning, or desire. Preserve darkness and material contrast instead of filling the frame with glow.
- Keep wit, tenderness, and human eccentricity inside the hardship; avoid brown-mush misery and anonymous military-industrial sameness.
- Use `1 Blender unit = 1 metre` and preserve pivots, axes, sockets, materials,
  LODs, collision, and naming.
- An accepted `.blend` is the editable canonical source; interchange files are
  derived.
- Tripo output remains a blockout until provenance, license, topology, art direction, optimization, and Unity import pass.
- Preserve prompts, raw outputs, edits, tool/model/version, license, and hashes.
- Never ship recognizable third-party marks, characters, silhouettes, or trade
  dress.
- Named inspirations describe function and feeling; never copy protected expression.

## Git and change hygiene

- Preserve user and other-agent work. Never destructively reset or discard unrelated changes.
- Branch from current `main`; use an isolated worktree for parallel work.
- Keep commits logical and use descriptive conventional commit messages.
- Push a complete logical unit rather than triggering CI after every edit.
- Never commit secrets, caches, local builds, generated recovery files, or
  unlicensed assets.
- Do not push directly to protected `main`; merge through the repository's
  required checks.
- Before handoff, inspect the diff, run the relevant validation tier, and state
  what was not run.

## Completion and handoff

Report:

1. the player-visible outcome;
2. changed files and authoritative state/save impact;
3. exact tests and observed results;
4. native/visual/performance checks that ran;
5. known imperfections and deferred work;
6. the next highest-value product step.

Stop only for credible risk of data loss, conflicting user work, irreversible
product direction, missing legal authority, unavailable credentials, or repeated
failure requiring a different approach. Otherwise choose a reversible default,
state it, and keep building.
