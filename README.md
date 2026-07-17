# Sasha the Atomic Land Pirate

An endearing, real-time, post-apocalyptic city-building and resource-management game about Sasha, the titular Atomic Land Pirate. Build a human colony, a humanoid-utility-robot colony, or a mixed society; upgrade a beloved wasteland vehicle; scavenge the road; grow workshops into manufacturing and physical-goods trade; and return home with consequences. The title is ratified; its exact in-world job, ethics, and origin remain deliberately open.

## Repository status

**A0 — WP-0003 is released; WP-0002 is accepted but not active.**

The approved durable checkout is `/Users/sasha/Documents/Codex/sasha-the-land-pirate`, backed by `https://github.com/AC-21/sasha-the-land-pirate`. Protected `main` contains the canonical Unity project skeleton, deterministic plain-C# seams, non-persisting save boundary, tests, and one non-gameplay technical sandbox released by WP-0003. That release is a technical foundation, not gameplay authority. WP-0002 may not write game code, assets, or Unity state until its separate protected activation transaction binds the exact local scope and held reservation.

The creator expressed a Unity-first preference and reports a local license. Inspection found Hub 3.19.5, Rosetta 2, and Editor 6000.5.4f1 ARM64; full Xcode and a standalone .NET SDK were absent. The creator-approved WP-0001 installation candidate is Unity 6000.3.19f1 ARM64 with Mac Build Support (IL2CPP), Xcode 26.3, URP 17.3, and Unity Test Framework 1.6. Mono is the iteration backend; IL2CPP ARM64 is the acceptance-build authority. D-0051 supersedes D-0049 and selects `UNITY-MCP-EXTERNAL`—Codex through Unity's MCP Bridge and exact Unity-installed relay—while D-0050 permits only creator-operated pre-A1 setup; direct agent/CI Unity process invocation remains prohibited. The secret-free [`pre-A1 readiness snapshot`](docs/evidence/WP-0001/pre-a1-readiness-20260716.json) records the current stack, route, three preserved deviations, and thirteen blockers as point-in-time A0 evidence.

The creator-ratified [`lean A1 local-development successor`](docs/foundation-v0.1/15-LEAN-A1-LOCAL-DEVELOPMENT.md) remains the operating model. WP-0003 is released under `RR-WP0003-COMPLETE-20260717`; its reservation is released and grants no continuing write authority. WP-0002 is creator-accepted under `RR-WP0002-ACCEPT-20260717`, but remains A0, unreserved, and unactivated.

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

Expected current result: the foundation lint passes with 64 decision records and 19 receipts, including five intentionally unsealed historical bootstrap captures.

## Planned seams

Released WP-0003 established `Game/`, `SimulationCore/`, `SaveContracts/`, `Tools/`, `Tests/`, and its bounded documentation/evidence paths. Those retained files are a baseline, not an active reservation. Other planned seams remain separately gated.

## Next gate

Sealed historical receipts accept WP-0001 contract `eed333603affe6aa1dd2b16b26ae702d9f561cc653fa319da02abfe008faeda3`, its temporary identity, repository location, creator-operated installation of the exact D-0047 candidate, and the now-superseded Gateway route. Owner-authenticated D-0051 and packet route-successor receipts protect `UNITY-MCP-EXTERNAL` against that unchanged contract. WP-0001 remains `accepted`, not `active`. Canonical autonomy is A0. The next executable gate is WP-0002's distinct protected activation; until it lands on protected `main`, no Unity MCP call or gameplay implementation is authorized.

Schema v4 remains the formal machine gate. Later A0 evidence proposes an exact
allowlist, four independent provider decisions, and a schema-v5
code-identity-context successor. Those proposals are unratified and do not
silently amend current authority, but the creator runbook remains fail-closed
until they are ratified or rejected in favor of an exact protected alternative.
