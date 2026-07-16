# Sasha the Atomic Land Pirate

An endearing, real-time, post-apocalyptic city-building and resource-management game about Sasha, the titular Atomic Land Pirate. Build a human colony, a humanoid-utility-robot colony, or a mixed society; upgrade a beloved wasteland vehicle; scavenge the road; grow workshops into manufacturing and physical-goods trade; and return home with consequences. The title is ratified; its exact in-world job, ethics, and origin remain deliberately open.

## Repository status

**A0 — foundation and control-plane documentation only.**

The approved durable checkout is `/Users/sasha/Documents/Codex/sasha-the-land-pirate`, backed by `https://github.com/AC-21/sasha-the-land-pirate`. Foundation CI was established at commit `a07411199c5ab4600cfcce60fb8e4e9e4daea9f1`; Foundation run `29465142421` completed successfully. `main` now requires pull requests plus strict, up-to-date `validate` and `Cursor Approval Agent: Pull Request Approver` checks, covers administrators, resolves conversations, enforces linear history, and blocks force-pushes/deletion. Squash-only auto-merge is enabled and source branches are deleted after merge; Actions defaults to read-only. The drift-aware [`repository enforcement snapshot`](docs/evidence/REPOSITORY-ENFORCEMENT-20260716.md) records the observed external settings. This remains A0 setup only: the repository still contains no game implementation, Sasha Unity project, installed project dependency, production asset, or authorized autonomous integration.

The creator expressed a Unity-first preference and reports a local license. Inspection found Hub 3.19.5, Rosetta 2, and Editor 6000.5.4f1 ARM64; full Xcode and a standalone .NET SDK were absent. The creator-approved WP-0001 installation candidate is Unity 6000.3.19f1 ARM64 with Mac Build Support (IL2CPP), Xcode 26.3, URP 17.3, and Unity Test Framework 1.6. Mono is the iteration backend; IL2CPP ARM64 is the acceptance-build authority. D-0051 supersedes D-0049 and selects `UNITY-MCP-EXTERNAL`—Codex through Unity's MCP Bridge and exact Unity-installed relay—while D-0050 permits only creator-operated pre-A1 setup; direct agent/CI Unity process invocation remains prohibited. The secret-free [`pre-A1 readiness snapshot`](docs/evidence/WP-0001/pre-a1-readiness-20260716.json) records the current stack, route, three preserved deviations, and thirteen blockers as point-in-time A0 evidence.

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
4. Check [`ratification-state.json`](docs/foundation-v0.1/governance/ratification-state.json) and the exact proposed work packet before acting.

Validate the bootstrap foundation from the repository root:

```sh
python3 docs/foundation-v0.1/tools/validate_foundation.py
```

Expected current result: the foundation lint passes with ten receipts: five intentionally unsealed bootstrap creator-source captures and five sealed receipts for D-0049, D-0050, D-0051, WP-0001 acceptance, and the WP-0001 direct-MCP route successor.

## Planned seams

The accepted technical packet may create `Game/`, `SimulationCore/`, `SaveContracts/`, `ContentSource/`, `Tools/`, `Tests/`, `BuildArtifacts/`, and bounded evidence directories. Their absence today is intentional. The canonical proposed layout and path authority live in the technical architecture and work packets, not in empty scaffolding.

## Next gate

Sealed historical receipts accept WP-0001 contract `eed333603affe6aa1dd2b16b26ae702d9f561cc653fa319da02abfe008faeda3`, its temporary identity, repository location, creator-operated installation of the exact D-0047 candidate, and the now-superseded Gateway route. Owner-authenticated D-0051 and packet route-successor receipts now protect `UNITY-MCP-EXTERNAL` against that unchanged contract. The packet is `accepted`, not `active`: the assigned Unity seat and same-organization packet project, exact clean Bridge/relay/client/target/connection profile, installed toolchain, and standalone quarantine/manual-import boundary still require evidence and a separate activation receipt. The A1 boundary schema permits only exact reserved outputs plus seven exact repository-ignored Unity scratch roots, and separately requires an isolated ephemeral runtime HOME/private temp for Unity, relay, license, log, and connection state. All scratch is destroyed on close and never enters the reservation or creator import. No further agent Unity interaction is authorized, and autonomy remains A0.

Schema v4 remains the formal machine gate. Later A0 evidence proposes an exact
allowlist, four independent provider decisions, and a schema-v5
code-identity-context successor. Those proposals are unratified and do not
silently amend current authority, but the creator runbook remains fail-closed
until they are ratified or rejected in favor of an exact protected alternative.
