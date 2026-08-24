# PROJECT\_CONTEXT.md

## Project summary

Stylized top-down arena game for Steam.

### Modes

* Online multiplayer: up to 4 players
* Couch multiplayer: up to 4 players on one screen
* PvE
* PvP

### Core gameplay

Players defend a magical living Heart Core in compact arenas. They fight, gather resources, deposit them into the Core, build defenses, upgrade structures, repair, revive teammates and survive enemy waves.

The design should stay indie-friendly:

* no Diablo-style loot system
* no large inventory
* no complex stat spreadsheets
* no huge skill trees
* no complicated humanoid rigs
* complexity should come from combinations of simple systems

\---

# Visual direction

## World

* stylized chunky 3D
* top-down readability
* maps built mostly from modular blocks, boxes, cliffs and simple props
* references: Minecraft Dungeons / LEGO Fortnite direction rather than realism
* bright, readable silhouettes
* avoid overly dark/demonic presentation

## Character construction

Existing enemies use a simplified construction:

* large head / central form
* detached floating hands
* no legs required
* minimal rigging and skinning

Preferred animation approach:

* bobbing/floating
* transform-based hand animation
* squash/stretch
* recoil
* hit shake
* simple weapon swing
* rigidbody breakup on death

Player heroes should follow the same general production philosophy.

\---

# Main PvE loop

1. Prepare around the Core
2. Gather resources
3. Return to the Core
4. Deposit resources into the shared pool
5. Build / upgrade defenses
6. Enemy wave starts
7. Fight + repair + heal + maintain defense
8. Survive wave
9. Gain resources / Core Shards
10. Improve defenses or Core
11. Repeat
12. Boss / final wave

Suggested standard mission length: roughly 15–25 minutes.

\---

# Shared player abilities

Every class can:

* fight
* gather
* carry resources
* deposit resources
* build
* repair normal structures
* revive allies

Classes specialize in these actions but do not lock them away.

A team should never become unable to play because a specific class is missing.

\---

# Hero classes

There are 4 basic classes. No equipment system is required.

Each class should have:

* clear passive identity
* basic attack
* a few active skills
* simple upgrade progression
* optional alternative skills later

## 1\. Warrior / Tank

Role:

* frontline
* survivability
* taunt
* crowd control
* protecting teammates

Weapon concept:

* sword
* shield

Key abilities:

* War Cry: taunts nearby enemies
* Ground Slam: AoE knockback + short stun
* possible ultimate: Fortress / Iron Stand with high damage reduction and automatic taunt

Warrior should have the highest HP and low ranged capability.

## 2\. Mage / DPS

Role:

* high damage
* AoE
* ranged priority-target killing
* glass cannon

Key abilities:

* Fireball: explosive AoE projectile
* Arcane Beam: long-range focused single-target attack
* possible ultimate: Meteor

Mage should have the lowest HP but the strongest offensive potential.

## 3\. Builder / Scavenger

Role:

* economy
* resource gathering
* building
* upgrading
* repair

Tools:

* hammer
* pickaxe

Advantages:

* faster mining
* faster building
* faster repair
* larger resource bag
* better salvage efficiency

Important building mechanic:
Structures are not instant. Player places a ghost structure and builds it over several seconds, visually using the hammer. Builder performs this faster.

Key abilities:

* Overdrive: temporarily boosts a tower
* Repair Burst: repairs nearby structures
* possible ultimate: Rapid Construction

## 4\. Medic / Support

Role:

* healing
* buffs
* revive
* anti-undead niche

Key abilities:

* Healing Pulse
* Blessing
* Holy Ground: heals players, damages undead
* faster revive / possible resurrection skill

Important rule:
**Medic is the only class that can directly heal the Heart Core.**

Medic does not repair normal towers and barricades. Builder handles structure repair.

\---

# Downed / revive system

When player HP reaches 0:

* player enters Downed state
* limited revive timer
* any player can revive
* Medic revives much faster

Avoid forcing a dead couch-coop player to spectate for a long time.

Possible rule:

* fully eliminated players return after the current wave

\---

# Resource system

Players gather resources into a limited personal bag.

Example:

* normal class: smaller capacity
* Builder: larger capacity

Resources are not immediately available for building.

Players must return to the Heart Core and deposit them.

Deposited resources enter a **shared team pool**.

Example:

* Player A deposits 15
* Player B deposits 20
* team pool = 35

If a player is downed while carrying resources, only part of the bag should drop, e.g. 20–30%.

\---

# Building philosophy

Main rule:

> Few base buildings, many meaningful upgrades.

Planned base categories:

1. Small Tower
2. Heavy Tower
3. Barricade
4. Trap Plate
5. Support Pylon

Upgrades should change gameplay, not just add +20% damage.

Upgrades themselves can also take construction time.

\---

# Small Tower

Primary modular turret family.

Art construction:

* reusable base
* mounting socket
* detachable weapon module
* optional upgrade attachments

Weapon modules should have very different silhouettes for top-down readability.

Upgrade tree:

```text
Small Tower
├── Fire Tower
│   ├── Flamethrower
│   └── Fireball
│
├── Frost Tower
│   ├── Cryo Beam
│   └── Ice Shard
│
└── Storm Tower
    ├── Tesla
    └── Thunder Cannon
```

## Small Tower base

* cheap
* quick to build
* neutral projectile
* average range / damage / speed

## Fire Tower

Identity:

* burn
* damage
* AoE

### Flamethrower

* short range
* continuous area damage
* wide nozzle
* fuel / furnace silhouette

### Fireball

* slower shots
* large explosive AoE
* spherical combustion chamber / mortar-like silhouette

## Frost Tower

Identity:

* slow
* freeze
* control

### Cryo Beam

* continuous focused beam
* stronger slow over time
* possible freeze
* good vs elites / bosses

### Ice Shard

* shard projectiles
* piercing or multi-shot
* jagged crystal silhouette

## Storm Tower

Identity:

* electricity
* chain attacks
* stun / disruption

### Tesla

* chain lightning
* anti-horde
* open coil / arc-node silhouette

### Thunder Cannon

* slower heavy electrical hit
* stun / interruption
* oversized heavy electric-cannon silhouette

\---

# Heavy Tower

Separate base building, not simply a larger Small Tower.

Possible tree:

```text
Heavy Tower
├── Cannon
│   ├── Siege Cannon
│   └── Cluster Cannon
│
└── Ballista
    ├── Piercer
    └── Harpoon
```

Possible roles:

* Siege Cannon: elite / boss single-target damage
* Cluster Cannon: explosive AoE
* Piercer: projectile penetrates several enemies
* Harpoon: pulls/slows enemies into traps

\---

# Barricade

Possible tree:

```text
Barricade
├── Fortified Wall
│   ├── Iron Wall
│   └── Regenerator
│
└── Defensive Wall
    ├── Spike Wall
    └── Shock Wall
```

\---

# Trap Plate

Possible tree:

```text
Trap Plate
├── Spike Trap
│   ├── Impaler
│   └── Sawblade
│
├── Oil Trap
│   ├── Tar Pit
│   └── Oil Pool
│
└── Spring Trap
    ├── Launcher
    └── Slammer
```

Physics and interactions are important.

Examples:

* Spring launches enemy off a cliff
* Oil + Fire = burning ground

\---

# Support Pylon

Possible tree:

```text
Support Pylon
├── Amplifier
│   ├── Rapid
│   └── Long Range
│
└── Gravity Pylon
    ├── Magnet
    └── Repulsor
```

Avoid infinite buff stacking.

\---

# Defense synergies

Examples:

## Oil + Fire

Oil Trap + Fire Tower / Flamethrower
→ burning area

## Frost + heavy hit

Freeze enemy
→ Heavy Cannon
→ optional Shatter bonus

## Magnet + AoE

Magnet groups enemies
→ Fireball / Meteor

## Harpoon + trap

Harpoon moves elite enemy
→ spikes / oil / spring / kill zone

The game should reward combinations instead of spamming one best tower.

\---

# Heart Core

The Heart Core is the main PvE objective.

It should not look like a generic crystal.

Core concept:

> A magical living heart suspended inside a constructed shrine / magical-mechanical structure.

The heart visibly beats.

Possible beat feedback:

* slight scale pulse
* subtle deformation
* synchronized light pulse
* heartbeat sound
* surrounding rings react to each beat

Core functions:

* main objective
* team resource deposit point
* shared resource storage
* Core upgrade location
* possible respawn/checkpoint anchor

Important:
**Medic is the only hero who can directly heal the Heart Core.**

Builder cannot repair it as a normal structure.

\---

# Heart Core visual direction

Rejected direction:

* overly dark
* demonic
* black fortress
* horror shrine
* excessive spikes

Current direction:

* bright heroic fantasy
* living red/ruby magical heart
* ivory / light gray stone
* warm gold / brass
* red crystal accents
* elegant magical rings
* valuable and alive rather than evil

\---

# Heart Core modular asset approach

Build the Core from separate parts:

1. Base platform
2. Heart
3. Rings
4. Support arms / attachments
5. Upgrade ornaments

This allows visible progression without remodeling the entire Core.

## Current base platform direction

* circular
* ivory/light stone
* gold/brass trim
* red gems
* central heart cradle/socket
* ring attachment points
* bright heroic style

## Heart variants being explored

### Organic Heart

* recognizable anatomical heart
* stylized, not gruesome
* magical red glow

### Guardian Heart

* fuller / stronger
* protected
* gold accents
* stable glow

### Pulse Heart

* more magical
* stronger energy veins
* clear pulsing identity

### Overcharge Heart

* highest energy
* partial crystalline qualities
* vivid ruby/pink energy

Final heart art direction is not locked yet.

## Ring variants being explored

### Simple Containment Ring

* clean circular gold ring
* a few red gems
* simple silhouette

### Runic Ring

* illuminated red runes
* stronger magical identity

### Segmented Ring

* separated floating arcs
* visually dynamic

### Halo / Double Ring

* multiple concentric rings
* ceremonial high-level appearance

Rings may rotate independently.

\---

# Heart Core upgrade system

Only **3 Core levels**.

At every upgrade, team chooses between 2 options.

Current tree:

```text
Level 1
Heart Core
│
├── Level 2: Guardian Heart
│   ├── Level 3: Sanctuary Heart
│   └── Level 3: Wrath Heart
│
└── Level 2: Pulse Heart
    ├── Level 3: Repulsor Heart
    └── Level 3: Overcharge Heart
```

## Level 1 — Heart Core

Functions:

* main objective
* deposit point
* shared resource pool
* healable only by Medic

Visual:

* simple base
* one heart
* one basic ring
* calm heartbeat

## Level 2 — Guardian Heart

Defensive branch.

Possible gameplay:

* increased max HP
* incoming damage reduction
* better survivability

Visual:

* stronger frame
* thicker protective pieces
* stable/heavy heartbeat

## Level 2 — Pulse Heart

Active-control branch.

Possible gameplay:

* periodic energy pulse
* pushes enemies away
* interrupts pressure near Core

Visual:

* more active ring
* brighter pulse
* stronger heart glow

## Level 3 — Sanctuary Heart

Guardian final branch.

Possible effects:

* defensive aura near Core
* player damage reduction
* faster revive
* optional mild regeneration

Purpose:

* safe last-defense zone

## Level 3 — Wrath Heart

Guardian final branch.

Possible effects:

* retaliation after Core takes damage
* shockwave
* stun
* AoE damage

Purpose:

* punish enemies reaching Core

## Level 3 — Repulsor Heart

Pulse final branch.

Possible effects:

* stronger pulse
* larger radius
* shorter cooldown
* strong knockback
* optional slow

Purpose:

* crowd control

## Level 3 — Overcharge Heart

Pulse final branch.

Possible effects:

* buffs nearby towers and/or players
* increased fire rate
* increased attack speed
* improved cooldown recovery

Purpose:

* offensive support

\---

# Core Shards

Rare strategic resource used for Core upgrades.

Possible sources:

* elites
* minibosses
* bosses
* optional events
* exploration rewards

Possible design tension:

* spend Core Shards on the Heart Core
* or reserve them for high-tier defense upgrades

Exact economy is not finalized.

\---

# Healing the Core

Preferred Medic interaction:

**Channel Heal**

Medic:

* stands near Core
* channels healing
* heart reacts visually
* Core HP slowly regenerates

This creates a cooperative vulnerable moment:

* Warrior protects
* Medic channels
* Builder repairs defenses
* Mage clears threats

\---

# Core low-health feedback

Core condition should be readable without only watching UI.

As HP falls:

* heartbeat speeds up
* glow becomes unstable
* heart pulses more aggressively
* surrounding structure/rings flicker
* heartbeat audio gets louder
* optional magical cracks appear
* warning UI becomes stronger

\---

# Enemies

A large enemy model set already exists.

Visual construction generally supports:

* oversized central/head form
* detached floating hands
* minimal rigging
* strong top-down silhouette

Gameplay variety should come mainly from reusable AI behaviors rather than writing unique AI architecture for every enemy.

Possible reusable behaviors:

* MoveToCore
* AttackPlayer
* AttackBuilding
* RangedAttack
* Charge
* JumpObstacle
* Fly
* BuffNearby
* HealNearby
* SpawnEnemies
* StealResources
* Explode
* LeavePoison
* Teleport
* PhaseThroughWalls

Enemy types should be data/config combinations of these behaviors.

\---

# Wave design

Do not scale difficulty only with HP.

Prefer combinations of roles.

Example:

* Wave 1: basic melee
* Wave 2: melee + ranged
* Wave 3: melee + structure breaker
* Wave 4: structure breaker + support
* Wave 5: support + disruptor + ranged

Difficulty should emerge from target priority and interactions.

\---

# PvP direction

Reuse the same:

* heroes
* skills
* buildings
* traps
* physics

Strong candidate mode:

## Core Wars

Each team owns a Heart Core.

Players:

* gather
* build
* fight
* attack enemy defenses/Core

Possible additional mechanic:
Spend team resources to send PvE enemies toward the opposing Core.

PvP design is still conceptual.

\---

# Campaign structure

Avoid a huge open world.

Preferred:

* world map
* themed biomes
* compact arenas

Possible mission objectives:

* Core Defense
* Escort Core
* Two Cores
* Repair
* Moving Core
* Extraction
* Boss Hunt

Exact campaign scope is not finalized.

\---

# Production strategy

## Phase 1 — Vertical Slice

Implement only enough to validate the core loop:

* one arena
* one hero class initially
* movement/combat
* Heart Core
* one resource
* resource bag
* deposit system
* Small Tower
* Barricade
* one Trap
* build-time mechanic
* one tower upgrade
* basic wave system
* a few enemy archetypes
* revive system

Main question to validate:

> Is gather → deposit → build → upgrade → defend → repair → survive actually fun?

## Phase 2

* all 4 classes
* more Small Tower upgrades
* more enemy behaviors
* 3-level Core upgrade system
* first boss
* first polished biome

## Phase 3

* online multiplayer
* couch multiplayer
* broader building tree
* campaign progression
* PvP experiments

Networking constraints should be considered early even if full multiplayer arrives later.

\---

# Technical principles for Codex

When generating code:

1. Prefer modular systems over monolithic scripts.
2. Use data-driven configs / ScriptableObjects where appropriate.
3. Keep gameplay parameters editable in the Inspector.
4. Separate gameplay logic from presentation/VFX.
5. Avoid hard-coding every enemy type.
6. Reuse modular enemy behaviors.
7. Keep local and online multiplayer compatibility in mind.
8. Prefer reusable building prefabs with modular weapon attachments.
9. Represent upgrade trees with clear configs/enums/data rather than nested hard-coded logic.
10. Keep architecture realistic for a solo/small-team indie project.
11. Avoid unnecessary abstraction before the basic gameplay loop is proven.
12. Favor simple, inspectable systems that are easy to debug.

# Game name

CoreKeepers

# Current priority order

1. Player feel
2. Cooperation
3. Heart Core defense loop
4. Building feel
5. Top-down readability
6. Resource loop
7. Enemy combinations
8. Class synergy
9. Meaningful simple upgrades
10. Scope control

Avoid spending large amounts of time early on:

* complex inventory
* loot rarity
* large skill trees
* detailed humanoid rigs
* huge maps
* dozens of buildings
* elaborate meta-progression

\---

# Open questions

Not finalized:

* final resource names
* exact costs
* number of waves
* class cooldowns
* Core HP/stats
* tower balance
* exact Core upgrade balance
* final Heart visual design
* networking solution
* exact PvP modes
* meta progression
* achievements
* price / monetization

\---

# Core design principle

> Players fight, gather, build, upgrade, repair, heal and protect a living magical Heart Core.

Complexity should come from interactions between simple systems, not from inventory management or large stat systems.

