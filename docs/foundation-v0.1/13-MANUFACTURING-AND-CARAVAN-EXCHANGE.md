# Manufacturing and the Caravan Stock Exchange

Version: 0.1 direction draft\
Creator boundary: D-0040 and D-0041 are ratified\
Milestone boundary: D-0046 provisionally stages the full exchange after the vertical slice

## 1. Economic promise

The economy grows from **survival → competence → surplus → interdependence**.

Scavenging keeps the first workshop alive. Manufacturing lets a settlement reproduce capability instead of merely consuming ruins. Trade lets different societies specialize. Once the world is populated enough to sustain recurring demand, caravaners open a shared market in physical stock: pumps, bearings, tools, food, fuel, cloth, machine parts, vehicle modules, and other goods that somebody actually made, stored, and moved.

This is not a company-share market. It has no equities, ownership percentages, stock tickers for corporations, or money created from imaginary inventory. “Stock” means goods on hand.

## 2. Authority boundary

Creator-ratified under D-0040:

- manufacturing and trading belong in the eventual game;
- stock means physical inventory, goods, and manufacturable things;
- it never means shares in companies.

Creator-ratified under D-0041:

- the exchange arrives after an aggregate world-population threshold;
- caravaners open and administer it;
- the threshold is a civilization milestone rather than a day-one menu.

Still open under D-0044:

- the exact threshold and which populations count;
- whether the trigger also needs contact, route, trust, or infrastructure;
- the unit of account, barter rules, matching algorithm, taxes, credit, and contract law;
- who the caravaners are, why settlements trust them, and how neutral they remain;
- when the full system enters the production schedule.

## 3. Player-facing vocabulary

| Term | Exact meaning |
|---|---|
| **Stock** | A counted physical good in storage, staging, transit, or delivery custody |
| **Lot** | A bounded quantity of one item/grade/origin offered together |
| **Order** | A request to buy or sell one or more physical lots under declared terms |
| **Contract** | A matched obligation with quantity, quality, price, route, deadline, custody, and failure terms |
| **Shipment** | The real cargo assigned to a caravan journey |
| **Quote** | A visible estimate with goods price, caravan fee, handling, distance, risk, and policy components |
| **Exchange** | The caravaner-administered matching and settlement institution |

Never use `stock`, `position`, `volume`, or `market value` without showing the underlying item and quantity. The interface may use a world-appropriate name such as **The Stock**, **Stockyard**, or **Caravan Exchange** only after narrative review.

## 4. Progression ladder

### Phase E0 — Salvage and barter

- Recover finite materials and repair existing systems.
- Make direct, local trades with known people or factions.
- Prices are offers with reasons, not a universal number.

### Phase E1 — Workshop manufacturing

- Turn raw salvage into standardized parts and useful finished goods.
- Learn recipes, tooling, quality control, packaging, and maintenance.
- Production chains remain short enough that every stoppage is legible.

### Phase E2 — Regional contracts

- Factions and settlements post recurring needs.
- Caravan visits create scheduled, capacity-limited trade.
- The player can specialize, but civic essentials still compete with export demand.

### Phase E3 — Caravan Stock Exchange

- Crossing the accepted world-population milestone triggers a visible caravaner arrival/opening event.
- The exchange aggregates physical offers and needs across connected participants.
- The player can post lots, reserve imports, compare delivered quotes, and plan production around caravan schedules.

### Phase E4 — Industrial interdependence

- Standards, high-grade goods, route institutions, embargoes, disaster demand, and faction doctrine reshape production.
- No settlement should become a spreadsheet island: every major market move remains visible in factories, warehouses, roads, caravans, neighborhoods, and relationships.

The phase labels are design structure, not a promise that all five appear in the first release.

## 5. Constitutional economy loop

```text
read demand → choose what home can spare → acquire inputs → manufacture a physical lot
→ store and grade it → post or accept terms → stage cargo → caravan transports it
→ delivery settles once → city/faction/world visibly changes → new demand appears
```

Every market action must affect at least two of production, logistics, population, road access, faction relationships, civic policy, or expedition planning. A detached price minigame fails review.

## 6. Manufacturing contract

A manufacturable good declares:

- immutable item and recipe IDs plus content versions;
- physical unit, mass, volume, storage class, grade/condition, and perishability if any;
- input lots, quantities, accepted substitutes, and provenance rules;
- required tooling, building state, labor/capability, power, time, and knowledge;
- output lot, byproducts, waste, emissions, and quality calculation;
- visible idle, waiting, working, blocked, output-full, and damaged states;
- civic uses, export uses, repair uses, and shortage consequences;
- save/migration behavior and one-interaction stall explanation.

Manufacturing earns a place only when it creates a choice: use the good at home, install it in a vehicle, fulfill a promise, sell it, hold it against forecast demand, or transform it again. Chains that exist only to add clicks are cut.

## 7. World-population milestone

The threshold is authoritative data, not a hidden script. The accepted version must define:

- one population metric and why it represents enough demand for an exchange;
- which settlements, humans, robots, cohorts, fleets, visitors, or dependents contribute;
- eligibility, discovery, and double-count prevention;
- threshold value, comparison operator, evaluation cadence, and content version;
- whether falling below the threshold closes, degrades, or leaves the exchange open;
- a player-facing progress explanation and forecast;
- deterministic behavior across save/load and content migration.

Recommended default: count the eligible population of known participating settlements, expose every contribution, fire the opening once, and never revoke the institution merely because a crisis temporarily lowers population. Whether robots count by head, civic standing, workload equivalent, or another rule remains blocked on robot-society authorship.

## 8. Caravaner role

Caravaners are a faction/institution with physical capacity, schedules, interests, and memory—not a magical auction API. The proposed minimum role is:

- certify item classes, grades, measures, and sealed lots;
- publish bids, offers, delivery windows, and route conditions;
- match compatible orders deterministically;
- reserve goods and consideration;
- provide or subcontract vehicles, storage, handling, and guards;
- record custody changes, delays, losses, delivery, disputes, and settlement;
- charge visible fees and react to trust, access, promises, and faction pressure.

Their administration should create story: neutrality can be useful, compromised, contested, funny, bureaucratic, or expensive. Their exact doctrine and political power remain open.

## 9. Physical-lot and shipment law

No trade may create, teleport, or duplicate goods. A proposed lifecycle is:

```text
available → reserved → staged → loaded → in-transit → delivered → settled
                    ↘ cancelled          ↘ delayed / damaged / lost / disputed
```

- A posted sell lot points to real inventory and cannot be consumed elsewhere while reserved.
- Loading debits the seller exactly once and creates one shipment custody record.
- Transit consumes route time and caravan capacity and can change condition or quantity only through recorded events.
- Delivery credits the buyer exactly once; settlement of payment/consideration is idempotent.
- Cancellation, partial fill, loss, rejection, and retry have explicit ownership and refund paths.
- Save/load during every custody phase resumes the recorded phase without duplicating stock or consideration.
- Aggregate distant simulation may hide individual wagons visually, but it cannot alter physical conservation or arrival time.

Conservation is an accounting equation, not a claim that destroyed cargo remains live: opening live or recoverable stock plus recorded production/import must equal closing live or recoverable stock plus recorded consumption/export/destruction/spillage/unrecoverable loss for each item and lineage. Every live or recoverable lot has exactly one custody location. A terminal sink keeps quantity, lineage, cause, tick, and event ID for audit but has no live owner or invented storage location.

Title transfer, loss allocation, insurance, credit, and dispute judgment remain open contract decisions. Custody and economic ownership must never be inferred from a visual object.

## 10. Price and matching hypothesis

A delivered quote should be explainable as:

```text
physical-goods terms + caravan service + handling/storage + route/time/risk + policy/faction effects
```

Recommended starting model:

- regional order books rather than one omniscient global price;
- deterministic price-time matching for compatible physical lots;
- partial fills only in declared lot increments;
- finite demand and supply with visible price impact;
- historical delivered ranges rather than a promise of future value;
- authored emergency orders and faction policies that still obey conservation;
- one explicit unit of account or barter basket selected later.

No day-one leverage, short selling, options, synthetic commodities, high-frequency trading, or abstract financial portfolio. Delivery contracts may exist because they move goods; they cannot become unbacked casino instruments.

## 11. Player decisions and UI

The exchange screen must answer:

1. What exact good, grade, quantity, and origin is this?
2. Where is it now: available, reserved, staged, or in transit?
3. Who wants it, why, and by when?
4. What will home lose if I export it?
5. What is the delivered price/consideration and each component?
6. Which caravan, route, capacity, risk, and arrival window apply?
7. What can interrupt delivery, and who bears that consequence?
8. What changed after the trade?

Required views: local stock, civic reserve, committed stock, incoming/outgoing shipments, regional wants/offers, caravan schedule, route constraints, contract history, and a causal price explanation. Charts support decisions but never replace crates, warehouses, loading, caravan motion, factory changes, and resident consequences in the world.

## 12. Strategy and anti-exploit law

- No same-tick buy/sell loop, free cancellation duplication, or settlement retry duplication.
- Storage, packaging, handling, distance, delay, spoilage, damage, capacity, and fees prevent frictionless teleport arbitrage.
- AI demand has budgets, needs, inventory, substitute rules, and response delays; it cannot be an infinite buyer.
- Price changes use bounded, versioned integer/fixed-point math and remain reproducible.
- Cornering a good may be a permitted strategy only if physical hoarding, civic scarcity, faction response, and political consequence are real.
- Market access, embargo, subsidy, rationing, reserve policy, and price intervention must disclose causes and affected lots.
- A market can fail or seize up, but the player receives causal warnings and multiple responses rather than an opaque collapse.

## 13. Cross-system consequences

| System | Exchange effect |
|---|---|
| Settlement | factories, storage, civic reserves, pollution, power, labor, and neighborhood identity |
| Population | work, needs, migration, inequality, maintenance, and who benefits from specialization |
| Vehicles/road | caravan chassis, cargo tools, route upgrades, escort/rescue, fuel, wear, and breakdowns |
| Factions | standards, access, sanctions, preferred goods, dependencies, favors, and disputes |
| Crises | shortages, demand surges, blocked routes, damaged stock, relief contracts, and profiteering choices |
| Progression | new recipes, institutions, measures, markets, and diplomatic leverage |
| Art/story | visible stockyards, loading rituals, factory motion, commodity signage, clerks, drivers, and persistent trade scars |

Human-only, robot-only, and mixed colonies must all be able to participate without a hidden required human. Their manufacturing strengths, consumption, legal standing, and market treatment remain conditional on D-0039 and later robot-society decisions.

## 14. Proof sequence

### Paper and headless proof

- threshold fires once at the exact accepted population state and survives reload;
- every order is backed by real stock or reserved consideration;
- randomized cancellation/delay/loss/retry sequences balance live/recoverable goods and consideration against explicit consumed, destroyed, spilled, exported, or unrecoverably lost sink records;
- no infinite buyer, negative stock, orphan contract, stuck custody, or unbounded price appears in long runs;
- at least three viable economic strategies survive identical seeds: civic-first, specialist exporter, and diversified trader.

### Graybox proof

- the player manufactures one surplus good, sees the home opportunity cost, posts it, watches it load, and receives one delayed consequence;
- a route closure changes delivered price and arrival without rewriting the goods price invisibly;
- a caravaner dispute has at least two viable resolutions with different civic/faction effects;
- save/load at reservation, loading, transit, delivery, and settlement is exact.

### Art and story proof

- the exchange reads as a physical stockyard and civic institution, not a modern brokerage app;
- commodity identity is readable through silhouette, storage, motion, sound, tags, and lighting;
- caravaner administration produces one sincere need, one institutional tension, and one state-dependent comic beat;
- nighttime exchange lighting obeys tungsten-over-neon and remains readable without bloom or color.

## 15. Scope law

The Last Bearing may show a workshop, direct barter, a caravaner rumor, or a physical claims office. It does **not** implement the full exchange. The founder playable may prove manufacturing and scheduled bilateral trade. The population-gated order book, multi-settlement price formation, market crises, and industrial specialization require a dedicated post-slice packet and evidence suite.

This phase boundary protects the mechanic from being reduced to a decorative menu and protects the vertical slice from becoming a campaign-economy prototype.
