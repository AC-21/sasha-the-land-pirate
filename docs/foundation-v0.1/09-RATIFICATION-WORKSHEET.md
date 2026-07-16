# Ratification Worksheet

This is the shortest path from foundation draft to engine spike. Answer in prose or with the compact codes at the bottom.

Already ratified and not being re-asked: the game is **Sasha the Atomic Land Pirate**; Sasha is the protagonist; and the colony may be human-only, humanoid-utility-robot-only, or mixed. The worksheet choices below may shape how those facts play, but cannot erase them. The mechanical differences between compositions are deliberately asked in item 13 rather than inferred from that availability fact.

## 1. Vehicle embodiment

**Recommended: direct driving.** Control one weighty, arcade-readable utility vehicle in compact authored road spaces. The city remains strategy-camera play; escorts come later if earned.

Alternative: indirect convoy command preserves a pure strategy interface and is cheaper, but weakens the “fun cars” promise.

Decision: `DRIVE` or `COMMAND`

## 2. Travel topology

**Recommended: regional route graph plus compact authored road spaces.** The graph carries distance, access, faction control, and strategic travel; selected road spaces carry tactile vehicle play and scavenging. Seamless regional travel remains a later expansion if the road loop earns its cost.

Alternative: a seamless open region increases continuity but multiplies streaming, content, save, navigation, and vehicle-world production before the central loop is proven.

Decision: `ROUTE+ROAD` or describe another topology.

Packet effect: current WP-0002 is compatible with `ROUTE+ROAD`; another topology is valid but branches to a revised topology-specific packet.

## 3. City time on the road

**Recommended: slowed continuation.** Home continues at a predictable reduced speed; forecasts and optional auto-pause prevent surprise catastrophe. This preserves the home-vs-horizon cost.

Alternatives: full pause makes trips easier to reason about but less consequential; full-speed continuation risks split-attention punishment.

Decision: `SLOWED`, `PAUSED`, or `FULL`

## 4. Cruelty ceiling

**Recommended: scars, rarely annihilation.** Shortages, injuries, damaged buildings, migration, missed opportunities, and broken trust persist. Named death and total vehicle loss are uncommon.

Alternatives: harsher permanent loss creates stronger dread but teaches detachment and reload behavior; softer consequences may dissolve survival pressure.

Decision: `SCARS`, `HARSH`, or describe another ceiling.

Packet effect: current WP-0002 proves `SCARS`. `HARSH` or a custom ceiling is valid but branches to a packet with explicit irreversible-loss evidence.

## 5. Faction role

**Recommended: competing cultures.** Factions are societies to trade with, depend on, resist, hybridize with, ally with, or sometimes displace. War is possible but not the default interface.

Alternative: conquest-forward factions create clearer enemies and victory conditions but flatten the civic premise toward conventional 4X.

Decision: `CULTURES` or `CONQUEST`

Packet effect: current WP-0002 proves `CULTURES`; `CONQUEST` branches to a conquest-compatible faction packet.

## 6. Vehicular combat

**Recommended: high-energy accent.** Navigation, weather, hauling, rescue, scavenging, and negotiation carry most road play; combat punctuates it and may deepen later.

Alternatives: no vehicular combat keeps the road focused on terrain, rescue, hauling, and human negotiation; pillar-level combat requires enemy archetypes, weapons, damage, AI, encounter balance, and much more animation/VFX in the first arc.

Decision: `ACCENT`, `NONE`, or `PILLAR`

Packet effect: current WP-0002 supports `ACCENT` and `NONE`; `PILLAR` branches to a combat-pillar packet and production envelope.

## 7. Product boundary

**Recommended: single-player and offline-capable for the first production arc.** No service dependency, multiplayer simulation, accounts, or live-ops architecture.

Alternative: designing multiplayer now changes nearly every simulation and save contract.

Decision: `SOLO-OFFLINE` or describe the required connected mode.

Packet effect: current WP-0002 assumes `SOLO-OFFLINE`; connected play branches to a network/save architecture packet before gameplay implementation.

## 8. City-building comparison authorization

**Recommended: hybrid city grammar.** Place individual functional buildings on a restrained/hidden snap grid, connect physical roads, show nearby carriers while aggregating offscreen deliveries, simulate households/cohorts plus named specialists, and grow one deeply evolving city.

This is the starting hypothesis for an ugly comparison spike, not a preselected result or a demand that presentation look grid-bound. Freeform and district alternatives are compared before the kernel is generalized.

Decision: `AUTHORIZE CITY COMPARISON`, optionally adding what you most want to manipulate directly. The comparison result later ratifies D-0030.

## 9. Thesis wording

Recommended thesis: **Build a home worth returning to.**

Decision: `RATIFY THESIS`, or rewrite it in one sentence.

## 10. Constitutional core

Recommended: ratify the constitutional loop, P1–P6, INV-004 through INV-010, INV-015, and INV-016 as one core. Technical/production invariants INV-011 through INV-014 are not smuggled into this claim, and independently ratified INV-017 does not need to be re-ratified. This is distinct from approving only the thesis sentence. The receipt must bind the exact accepted constitution hash.

Decision: `RATIFY CORE`, or name the loop, pillar, or invariant to revise.

## 11. Vertical-slice proof

Recommended: ratify **The Last Bearing**—Sasha, one town, one road with two paths, one autonomous faction, one water-system failure, one vehicle, two exact module verbs, two city-preparation states, one dust front, exact save/load/recovery, and a separate bounded human-only/robot-only/mixed staffing proof that does not multiply the four gameplay permutations.

Decision: `RATIFY SLICE`, or state the one scenario element that must change.

## 12. Vertical-slice cuts

Recommended: separately ratify D-0018 and D-0019 as **milestone cuts**, not permanent bans. D-0018 removes a seamless procedural open world from the first production arc. D-0019 removes from The Last Bearing: FPS interiors or on-foot combat, convoy escorts, multiple cities or colonies, full faction-war simulation, deep vehicular combat, terrain deformation, a large tech tree, sprawling dialogue trees, multiplayer or online dependency, final campaign lore, production-scale citizen counts or per-citizen full simulation, full 4X conquest, and a final marketing-scale volume of polished assets.

Decision: `RATIFY CUTS`, or name the cut to restore/change. `RATIFY SLICE` alone does not approve these cuts.

## 13. Colony-composition mechanics

**Recommended: mechanically distinct compositions.** Humans, humanoid utility robots, and mixed colonies remain capable of completing the constitutional loop, but do so through legible differences in needs, capabilities, maintenance, policies, relationships, and complementary assignments. None is a cosmetic skin or a strictly superior difficulty setting.

Alternative: composition changes resident representation and fiction while sharing almost all mechanics. This is cheaper, but WP-0002's current differentiated staffing proof would be invalid and must branch.

Decision: `MECHANICAL COMPOSITIONS` or `REPRESENTATIONAL COMPOSITIONS`.

Packet effect: current WP-0002 requires `MECHANICAL COMPOSITIONS`; the representational choice is valid but branches to a reduced composition-state packet.

## Compact response

You can respond with:

`DRIVE / ROUTE+ROAD / SLOWED / SCARS / CULTURES / ACCENT / SOLO-OFFLINE / AUTHORIZE CITY COMPARISON / RATIFY THESIS / RATIFY CORE / RATIFY SLICE / RATIFY CUTS / MECHANICAL COMPOSITIONS`

Receipt claims use the same codes in machine form with spaces normalized to hyphens—for example, `RATIFY CORE` becomes `RATIFY-CORE`. No semantic inference is permitted during normalization.

The narrowly scoped, gameplay-neutral technical spike can be authorized separately through WP-0001 before this identity pass is complete. Gameplay grayboxing begins only after the applicable identity/core receipt claims are recorded and match WP-0002's branch constraints. The engine itself remains provisional until the spike passes.
