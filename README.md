# Sasha the Atomic Land Pirate

An endearing, real-time, post-apocalyptic city-building and resource-management game about Sasha, the titular Atomic Land Pirate. Build a human colony, a humanoid-utility-robot colony, or a mixed society; upgrade a beloved wasteland vehicle; scavenge the road; grow workshops into manufacturing and physical-goods trade; and return home with consequences. The title is ratified; its exact in-world job, ethics, and origin remain deliberately open.

## Repository status

**A1 — WP-0003 local repository/bootstrap development is active from protected `main`; Unity first-use remains gated.**

The approved durable checkout is `/Users/sasha/Documents/Codex/sasha-the-land-pirate`, backed by `https://github.com/AC-21/sasha-the-land-pirate`. Foundation CI was established at commit `a07411199c5ab4600cfcce60fb8e4e9e4daea9f1`; Foundation run `29465142421` completed successfully. `main` now requires pull requests plus strict, up-to-date `validate` and `Cursor Approval Agent: Pull Request Approver` checks, covers administrators, resolves conversations, enforces linear history, and blocks force-pushes/deletion. Squash-only auto-merge is enabled and source branches are deleted after merge; Actions defaults to read-only. The drift-aware [`repository enforcement snapshot`](docs/evidence/REPOSITORY-ENFORCEMENT-20260716.md) records the observed external settings. The repository still contains no game implementation or `Game` project, but protected repository/bootstrap work is now authorized only through active WP-0003.

The creator expressed a Unity-first preference and reports a local license. Inspection found Hub 3.19.5, Rosetta 2, and Editor 6000.5.4f1 ARM64; full Xcode and a standalone .NET SDK were absent. The creator-approved WP-0001 installation candidate is Unity 6000.3.19f1 ARM64 with Mac Build Support (IL2CPP), Xcode 26.3, URP 17.3, and Unity Test Framework 1.6. Mono is the iteration backend; IL2CPP ARM64 is the acceptance-build authority. D-0051 supersedes D-0049 and selects `UNITY-MCP-EXTERNAL`—Codex through Unity's MCP Bridge and exact Unity-installed relay—while D-0050 permits only creator-operated pre-A1 setup; direct agent/CI Unity process invocation remains prohibited. The secret-free [`pre-A1 readiness snapshot`](docs/evidence/WP-0001/pre-a1-readiness-20260716.json) records the current stack, route, three preserved deviations, and thirteen blockers as point-in-time A0 evidence.

The creator-ratified [`lean A1 local-development successor`](docs/foundation-v0.1/15-LEAN-A1-LOCAL-DEVELOPMENT.md) and WP-0003 are now active under the distinct protected receipt `RR-WP0003-ACTIVATE-20260716`. The exact local boundary authorizes repository/bootstrap work while keeping the first Unity MCP call conditional.

Current creator-ratified facts:

- title: **Sasha the Atomic Land Pirate**;
- protagonist: **Sasha**;
- playable colony composition: human-only, original humanoid-utility-robot-only, or mixed;
- real-time post-apocalyptic city building, resource management, scavenging, upgradeable travel vehicles, distinct factions, no zombies, durable save/load, ambitious endearing art, Mac playability, and an eventual safe background-agent loop;
- eventual manufacturing and trading in physical stock/goods, never company shares;
- a caravaner-administered physical-goods exchange that opens after an authored aggregate world-population threshold;
- visual north star: **Texas iron × brutalist opera**, with **tungsten over neon**.

The exchange identity is ratified; its exact metric, number, population eligibility, access, matching, consideration, loss/dispute law, and production proof remain open under D-0044. The functional reference matrix and phased production translation remain provisional.

Everything marked provisional or open in the foundation remains so.

## Start here

1. Read [`AGENTS.md`](AGENTS.md) before changing anything.
2. Read the [`foundation v0.1 map`](docs/foundation-v0.1/README.md).
3. Read the [`game constitution`](docs/foundation-v0.1/00-GAME-CONSTITUTION.md) and [`decision ledger`](docs/foundation-v0.1/01-DECISION-LEDGER.md).
4. Check [`ratification-state.json`](docs/foundation-v0.1/governance/ratification-state.json) and the exact work packet before acting.

Validate the bootstrap foundation from the repository root:

```sh
python3 docs/foundation-v0.1/tools/validate_foundation.py
```

Expected current result: the foundation lint passes with thirteen receipts: five intentionally unsealed bootstrap creator-source captures and eight sealed receipts, including the distinct D-0052 ratification, WP-0003 acceptance, and WP-0003 activation records.

## Planned seams

Active WP-0003 may create only its declared `Game/`, `SimulationCore/`, `SaveContracts/`, `Tools/`, `Tests/`, `BuildArtifacts/WP-0003/`, and bounded documentation/evidence paths. Their absence at activation is intentional. Other planned seams remain separately gated.

## Next gate

Sealed historical receipts accept WP-0001 contract `eed333603affe6aa1dd2b16b26ae702d9f561cc653fa319da02abfe008faeda3`, its temporary identity, repository location, creator-operated installation of the exact D-0047 candidate, and the now-superseded Gateway route. Owner-authenticated D-0051 and packet route-successor receipts protect `UNITY-MCP-EXTERNAL` against that unchanged contract. WP-0001 remains `accepted`, not `active`: its assigned-seat, project, D-0047, connection, and standalone-quarantine evidence is still missing. Canonical autonomy is nevertheless A1 for WP-0003 alone. No Unity MCP call is authorized until the active WP-0003 first-use gate is freshly satisfied for the exact `Game` target.

Schema v4 remains the formal machine gate. Later A0 evidence proposes an exact
allowlist, four independent provider decisions, and a schema-v5
code-identity-context successor. Those proposals are unratified and do not
silently amend current authority, but the creator runbook remains fail-closed
until they are ratified or rejected in favor of an exact protected alternative.
