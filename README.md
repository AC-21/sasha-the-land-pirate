# Sasha the Atomic Land Pirate

An endearing, real-time, post-apocalyptic city-building and resource-management game about Sasha, the titular Atomic Land Pirate. Build a human colony, a humanoid-utility-robot colony, or a mixed society; upgrade a beloved wasteland vehicle; scavenge the road; grow workshops into manufacturing and physical-goods trade; and return home with consequences. The title is ratified; its exact in-world job, ethics, and origin remain deliberately open.

## Repository status

**A0 — foundation and control-plane documentation only.**

This local repository candidate contains no game implementation, Unity project, installed project dependency, production asset, or authorized autonomous integration. Repository creation does not approve WP-0001, Unity installation, the repository's production location, or a higher autonomy level.

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

Expected current result: the foundation lint passes; all three local creator-source receipts remain intentionally unsealed pending protected repository/receipt infrastructure.

## Planned seams

The accepted technical packet may create `Game/`, `SimulationCore/`, `SaveContracts/`, `ContentSource/`, `Tools/`, `Tests/`, `BuildArtifacts/`, and bounded evidence directories. Their absence today is intentional. The canonical proposed layout and path authority live in the technical architecture and work packets, not in empty scaffolding.

## Next gate

WP-0001 remains blocked on four creator-controlled approvals: accept the exact packet, approve this repository location, authorize the Unity Hub/Editor installation, and verify the physical A1 quarantine/manual-import boundary. Until those are receipt-bound, work remains A0.
