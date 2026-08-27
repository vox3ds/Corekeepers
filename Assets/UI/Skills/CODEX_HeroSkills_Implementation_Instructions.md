# CoreKeepers — Hero Skills Implementation Instructions for Codex

## 0. Goal

Implement the complete hero skill system for the four playable classes:

- Warrior
- Mage
- Builder
- Healer

The game is a **fantasy / magical tower-defence action game**, not sci-fi.

The system must integrate with the **existing CoreKeepers project**, especially:

- existing player/hero controller,
- existing combat and damage system,
- existing health/death/revive system,
- existing enemy system,
- existing building/tower/repair system,
- existing mining/resource system,
- existing wave/mission system,
- existing multiplayer/networking implementation,
- existing Input System / player input implementation,
- existing UI hierarchy in `DebugScene`.

**Do not create parallel replacements for systems that already exist.**
Before implementing a new interface/component/system, search the project and reuse or extend the existing one whenever possible.

---

# 1. Existing UI

The UI is already prepared in:

`DebugScene -> Core Gameplay Canvas`

Do **not** redesign or rebuild the panel.

Use the existing hierarchy.

## SkillsPanel

Existing objects:

- `Skill1Icon`
- `Skill2Icon`
- `Skill3Icon`
- `Skill4Icon`

- `Skill1Timer`
- `Skill2Timer`
- `Skill3Timer`
- `Skill4Timer`

- `Skill1Lock`
- `Skill2Lock`
- `Skill3Lock`
- `Skill4Lock`

- `Skill1Cooldown`
- `Skill2Cooldown`
- `Skill3Cooldown`
- `Skill4Cooldown`

- `Skill1Selected`
- `Skill2Selected`
- `Skill3Selected`
- `Skill4Selected`

### Icons

Skill icons are already placed under:

`Assets/UI/Skills/Icons`

Use:

- **64x64 icons** in `SkillXIcon`
- **256x256 icons** in the skill upgrade popup

Search recursively inside `Assets/UI/Skills/Icons`.

Do not load icon files manually from disk at runtime.

Prefer serialized `Sprite` references stored in skill definition assets/data.

If the project does not already have skill definition ScriptableObjects, create them.

---

# 2. Skill slots

There are exactly four active skill slots.

## Slot 1

Contains the hero's **Basic Attack**.

It is available immediately when the mission begins.

`Skill1Lock` must be disabled from the start.

## Slot 2

Contains the active skill selected after defeating Wave 1.

At mission start:

`Skill2Lock = enabled`

After Wave 1 is defeated:

`Skill2Lock = disabled`

The selected Wave 1 active skill is assigned to Slot 2.

## Slot 3

Contains the active skill selected after defeating Wave 3.

At mission start:

`Skill3Lock = enabled`

After Wave 3 is defeated:

`Skill3Lock = disabled`

The selected Wave 3 active skill is assigned to Slot 3.

## Slot 4

Contains the active skill selected after defeating Wave 5.

At mission start:

`Skill4Lock = enabled`

After Wave 5 is defeated:

`Skill4Lock = disabled`

The selected Wave 5 active skill is assigned to Slot 4.

---

# 3. Skill progression

Every player starts the mission with only the Basic Attack.

After each defeated wave from 1 through 6, that player gets one upgrade choice.

The pattern is:

| Moment | Choice |
|---|---|
| Start | Basic Attack |
| After Wave 1 | Active Skill: choose 1 of 2 |
| After Wave 2 | Passive Skill: choose 1 of 2 |
| After Wave 3 | Active Skill: choose 1 of 2 |
| After Wave 4 | Passive Skill: choose 1 of 2 |
| After Wave 5 | Active Skill: choose 1 of 2 |
| After Wave 6 | Passive Skill: choose 1 of 2 |
| Wave 7 defeated | Victory / end of mission |

The player therefore finishes the mission with:

- 1 Basic Attack
- 3 selected additional Active Skills
- 3 selected Passive Skills

The 3 passive skills do **not** occupy the four active skill slots.

Progression must reset when a new mission/run begins.

---

# 4. Skill selection controls

Only one skill slot can be selected at a time.

The currently selected slot determines which active skill is triggered by gameplay LMB.

Exactly one corresponding object should be active:

- `Skill1Selected`
- `Skill2Selected`
- `Skill3Selected`
- `Skill4Selected`

Never allow two Selected frames at once.

## Selection methods

The player must be able to select a skill by:

### Mouse

Clicking the corresponding skill slot/icon in the UI.

### Keyboard

- `1` -> Slot 1
- `2` -> Slot 2
- `3` -> Slot 3
- `4` -> Slot 4

Do nothing when trying to select:

- a locked slot,
- an empty slot.

### Mouse wheel

Mouse wheel cycles through available active skills.

Requirements:

- skip locked slots,
- skip empty slots,
- wrap around at the first/last available slot.

Example:

If only Slots 1 and 2 are available:

`1 -> 2 -> 1 -> 2`

Do not cycle through Slots 3 or 4.

---

# 5. LMB / RMB and UI input blocking

This is important.

Gameplay uses mouse buttons.

LMB is used to activate the currently selected skill.

RMB may be used by the building system.

### Requirement

Clicking the UI with LMB or RMB must **never**:

- activate a hero skill,
- attack,
- place a building,
- trigger a build action,
- trigger another world interaction.

Before processing gameplay mouse actions, check whether the pointer is over UI.

Integrate this with the project's existing input architecture.

If the project uses Unity `EventSystem`, a typical check is equivalent to:

`EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()`

Do not blindly paste this check into many unrelated scripts.

Prefer one central gameplay-input gate/helper if the existing architecture allows it.

This rule applies to:

- LMB skill use,
- RMB building,
- other mouse gameplay interactions that would accidentally fire when clicking the HUD.

---

# 6. Cooldown UI

Each active skill slot uses:

- `SkillXCooldown`
- `SkillXTimer`

## SkillXCooldown

This should be a Unity UI `Image` using radial Fill.

`fillAmount` meaning:

- `0.0` = skill has just been used
- `0.5` = half of cooldown elapsed
- `1.0` = skill is ready

The animation must therefore move:

`0 -> 1`

over the cooldown duration.

When a skill is ready:

`fillAmount = 1`

Do not animate from 1 to 0.

## SkillXTimer

Displays remaining cooldown time.

Examples:

- `8`
- `3.4`
- `1.2`

The exact formatting can be clean and readable.

Recommended:

- >= 10 seconds: integer
- < 10 seconds: one decimal place

When the skill is ready:

`SkillXTimer` must be disabled/hidden.

It should not display `0`.

Basic Attack also uses the same cooldown system.

---

# 7. Using active skills

LMB uses the active skill from the currently selected slot.

A skill can activate only when:

- slot is unlocked,
- slot contains a skill,
- cooldown is ready,
- hero is alive and able to act,
- gameplay input is not blocked,
- pointer is not over UI,
- any skill-specific requirements are satisfied.

After use:

- start cooldown,
- set `SkillXCooldown.fillAmount = 0`,
- show `SkillXTimer`,
- update fill and timer until ready.

Do not allow a cooldown to start when the skill failed to execute because the target/location was invalid.

Use existing project targeting rules where applicable.

---

# 8. Skill Upgrade Popup

Existing hierarchy:

`Skill Upgrade Popup`

Children include:

- `WaveXDefetedText`
- `ChooseYourSkillText`
- `Option1`
  - `Label`
- `Option2`
  - `Label`

Use the existing UI.

Do not redesign the popup.

## When to show it

Show it once for the local player after Waves:

- 1
- 2
- 3
- 4
- 5
- 6

Do not show it after Wave 7.

Each player makes their own choice.

In multiplayer, do not use `Time.timeScale = 0` to pause the entire game.

Use the existing between-wave/game-state logic and block only the local player's relevant gameplay input while the popup is open.

## Wave text

Set:

`WaveXDefetedText`

to:

`Wave {waveNumber} defeted`

Examples:

- `Wave 1 defeted`
- `Wave 3 defeted`
- `Wave 6 defeted`

Keep this spelling for now because it matches the current requested UI copy.

## ChooseYourSkillText

Recommended:

For Waves 1, 3, 5:

`Choose your active skill`

For Waves 2, 4, 6:

`Choose your passive skill`

## Options

Each option must display:

- corresponding **256x256 icon**
- skill name in its existing `Label`

If the existing `Option1` / `Option2` root already contains an appropriate Image component, reuse it.

If there is no independent Image suitable for the skill icon, add a child `Icon` only if required, without changing the visual layout.

Do not replace the button background graphic with the icon if doing so destroys the existing UI styling.

## Clicking an option

### Active choice

Waves 1, 3, 5:

Assign the selected skill to:

- Wave 1 -> Slot 2
- Wave 3 -> Slot 3
- Wave 5 -> Slot 4

Then close the popup.

### Passive choice

Waves 2, 4, 6:

Apply/activate that passive for the rest of the current mission.

Then close the popup.

A choice can only be made once for that wave.

Prevent double-clicking from granting both choices.

---

# 9. Data-driven architecture

Implement skills in a maintainable, data-driven way.

Preferred structure if the project does not already have an equivalent:

## HeroSkillDefinition

A ScriptableObject or equivalent data object containing at minimum:

- stable skill ID
- display name
- hero class
- skill type:
  - Basic
  - Active
  - Passive
- unlock wave
- cooldown
- icon64
- icon256
- description
- configurable values required by that skill
- reference/type identifying its runtime behaviour

Avoid huge switch statements such as:

`if skillName == "Fireball"...`

Use IDs/types/components/strategies.

## Runtime

Separate:

- static skill definition/data,
- runtime cooldown state,
- runtime passive state,
- UI presentation.

The UI must not contain combat logic.

---

# 10. Icon assignment

Icons exist under:

`Assets/UI/Skills/Icons`

Use filenames/content to match the corresponding skills.

The current expected skills are listed below.

Search recursively rather than assuming one exact subfolder hierarchy.

For each skill definition assign:

- 64x64 Sprite -> HUD slot
- 256x256 Sprite -> Upgrade Popup

If an icon cannot be matched automatically, log a clear editor warning and leave the field serialized so it can be assigned manually.

Do not silently use an unrelated icon.

---

# 11. Multiplayer requirements

CoreKeepers supports multiplayer.

Integrate skill execution with the project's existing network model.

Do not create a second networking layer.

Important:

- HUD is local-player UI only.
- Upgrade popup is shown to each local player independently.
- Player skill choices belong to that player's hero/run.
- Damage/healing/repair effects must follow the project's existing authoritative networking pattern.
- Do not implement damage only on the client.
- Do not spawn gameplay effects in a way that causes duplicates on host/client.
- Passive stat modifiers must affect the authoritative gameplay state.
- Do not use global `Time.timeScale` for the popup.

If the project already uses server RPCs / NetworkVariables / authoritative combat helpers, reuse them.

---

# 12. Hero class roles

## Warrior

Role:

- frontline
- melee
- tank
- crowd control
- survivability

## Mage

Role:

- main DPS
- ranged
- highest damage potential
- AoE
- lowest HP of all heroes

Mage should feel powerful but fragile.

Do not compensate for his low HP by giving him strong defensive barriers.

## Builder

Role:

- melee fighter using a hammer
- building
- repairing
- mining
- strengthening towers/buildings
- fantasy craftsman / rune builder

Avoid sci-fi mechanics and terminology.

## Healer

Role:

- healing
- support
- Core healing
- anti-undead damage
- cleanse / status protection
- revival

Healer is especially effective against undead enemies.

---

# 13. WARRIOR SKILLS

## Basic — Sword Slash

Cooldown:

`1.2 s`

Behaviour:

- melee sword arc in front of Warrior,
- can hit multiple enemies inside the arc,
- **no knockback**.

---

## Wave 1 Active A — Whirlwind

Cooldown:

`9 s`

Behaviour:

- Warrior spins with the sword,
- damages enemies around himself.

---

## Wave 1 Active B — Shield Bash

Cooldown:

`10 s`

Behaviour:

- shield attack in front of Warrior,
- damage,
- strong knockback,
- short stun.

---

## Wave 2 Passive A — Iron Skin

Effect:

`+25% Max HP`

Apply correctly to current/max HP using the project's existing stat rules.

---

## Wave 2 Passive B — Sharpened Blade

Effect:

`+20% Basic Attack damage`

Only the Basic Attack is modified by this passive.

---

## Wave 3 Active A — Battle Charge

Cooldown:

`14 s`

Behaviour:

- Warrior charges forward,
- damages enemies crossed by the charge,
- pushes/knocks enemies away.

Use the existing movement/collision system.

---

## Wave 3 Active B — Taunting Roar

Cooldown:

`16 s`

Behaviour:

- taunts nearby enemies,
- affected enemies should prefer Warrior as their target for a short duration,
- Warrior receives temporary damage reduction during the effect.

Expose radius, duration and damage reduction as tunable serialized values.

---

## Wave 4 Passive A — Berserker

Trigger:

Warrior HP below `30%`.

Effect while active:

- `+40% damage`
- `+25% attack speed`

Remove the bonus when HP rises above the threshold.

Do not repeatedly stack the modifier every frame.

---

## Wave 4 Passive B — Unbreakable

Trigger:

Warrior HP below `30%`.

Effect:

`+40% damage resistance`

Remove it when HP rises above the threshold.

Do not stack the modifier repeatedly.

---

## Wave 5 Active A — Earthshatter

Cooldown:

`26 s`

Behaviour:

- centered around Warrior,
- short magical/physical shockwave around the character,
- low damage,
- main purpose is crowd control,
- stuns nearby enemies for several seconds,
- **no knockback**.

Recommended initial tunable values:

- radius: `5`
- stun duration: `3 s`

Expose these values in data/Inspector.

---

## Wave 5 Active B — Last Stand

Cooldown:

`30 s`

Temporary effect:

- very high damage reduction,
- increased damage,
- immunity to crowd-control such as stun/slow/knockback for the duration.

Expose duration and modifiers as tunable values.

---

## Wave 6 Passive A — Against the Horde

Trigger:

At least `10` enemies are inside the Warrior's detection radius.

Effect:

`+35% damage`

When enemy count drops below 10:

keep the bonus for `3 s`, then remove it.

Recommended initial radius:

`7`

Expose radius and enemy threshold in data.

---

## Wave 6 Passive B — Executioner

Effect:

Warrior deals increased damage against enemies below:

`25% HP`

Expose the bonus multiplier in data.

Recommended starting bonus:

`+50% damage`

---

# 14. MAGE SKILLS

Mage has the **lowest Max HP** of all four heroes.

Mage Basic Attack has the **highest base damage** among all Basic Attacks.

---

## Basic — Arcane Bolt

Cooldown:

`1.5 s`

Behaviour:

- ranged projectile,
- single target,
- **no splash damage**,
- highest Basic Attack base damage among Warrior / Mage / Builder / Healer.

---

## Wave 1 Active A — Fireball

Cooldown:

`8 s`

Behaviour:

- ranged projectile,
- explodes,
- AoE damage,
- applies Burning.

Use the existing status-effect implementation if one exists.

---

## Wave 1 Active B — Frost Nova

Cooldown:

`10 s`

Behaviour:

- AoE around Mage,
- damage,
- strong slow.

Use the existing Slow/Freeze systems.

---

## Wave 2 Passive A — Arcane Power

Effect:

`+20% Active Skill damage`

Does not increase Basic Attack damage.

---

## Wave 2 Passive B — Quick Casting

Effect:

`-20% Active Skill cooldown`

Apply to additional active skills.

Do not reduce cooldown below any global minimum if the project already has one.

---

## Wave 3 Active A — Chain Lightning

Cooldown:

`14 s`

Behaviour:

- initial target,
- lightning jumps to additional nearby enemies,
- each target should only be hit once per cast.

Expose:

- jump count
- jump radius
- damage falloff if used

---

## Wave 3 Active B — Arcane Blink

Cooldown:

`15 s`

Behaviour:

- short-range teleport to a valid target position,
- leaves a magical rune at Mage's starting position,
- rune slows enemies inside it for `3 s`.

Important:

- no shield,
- no damage immunity,
- no strong defensive barrier.

Validate destination against the project's movement/navigation rules.

---

## Wave 4 Passive A — Arcane Exposure

When Mage hits an enemy with an Active Skill:

mark that enemy for:

`5 s`

While marked:

the enemy receives:

`+15% damage`

from **other heroes**.

Mage's own damage should not receive this bonus unless explicitly desired later.

This passive is intended as multiplayer/team synergy.

Do not stack the mark infinitely.

Refreshing duration is acceptable.

---

## Wave 4 Passive B — Glass Cannon

Effect:

- `+25% Active Skill damage`
- `-15% Max HP`

This reinforces Mage as the main DPS / lowest-HP hero.

Apply Max HP reduction safely.

---

## Wave 5 Active A — Meteor Strike

Cooldown:

`28 s`

Behaviour:

- targeted ground AoE,
- visible warning/telegraph before impact,
- very high AoE damage,
- Burning.

Reuse any existing ground-targeting framework.

---

## Wave 5 Active B — Gravity Vortex

Cooldown:

`26 s`

Behaviour:

- creates a magical vortex,
- pulls enemies toward the center,
- deals damage over time,
- allows Warrior/towers/other heroes to attack grouped enemies.

Do not permanently break NavMesh/enemy movement after the effect ends.

---

## Wave 6 Passive A — Arcane Mastery

Effect:

Using an Active Skill temporarily increases the damage of the next Active Skill.

The bonus may stack.

Implement using an explicit stack count.

Consume/reset stacks according to the final chosen rule.

Recommended starting implementation:

- each active cast grants 1 stack,
- max 3 stacks,
- each stack gives +10% damage,
- when an Active Skill deals damage, current stacks are consumed after damage calculation.

Keep these values easy to tune.

---

## Wave 6 Passive B — Elemental Detonation

When an enemy dies while affected by:

- Burning / Fire
- Frost / Slow from Mage
- Lightning marker if available

the enemy creates a small magical explosion.

Requirements:

- AoE damage,
- cannot recursively create infinite explosion chains.

Use an internal flag/event guard if required.

---

# 15. BUILDER SKILLS

Builder fights in melee with a **hammer**.

The visual/mechanical theme is:

- fantasy construction,
- runes,
- stone,
- wood,
- metal,
- magical craftsmanship.

Avoid sci-fi generators/lasers/electric machinery.

---

## Basic — Hammer Strike

Cooldown:

`1.0 s`

Behaviour:

- melee hammer attack,
- strong close-range hit,
- small knockback is acceptable.

---

## Wave 1 Active A — Repair Burst

Cooldown:

`10 s`

Behaviour:

- magical/runic pulse around Builder,
- instantly repairs nearby player-built structures.

Do not heal enemies or unrelated scenery.

Use the existing building HP/repair system.

---

## Wave 1 Active B — Construction Rush

Cooldown:

`30 s`

Duration:

`10 s`

Base radius:

`7`

Behaviour:

Builder creates a temporary aura.

All players inside the aura receive:

- `2x mining speed`
- `2x building speed`
- `2x repair speed`

Requirements:

- effect applies only while the player is inside the aura,
- leaving the aura removes the bonus,
- re-entering restores it while aura is active,
- modifiers must not permanently remain after the aura ends,
- multiplayer-safe.

This is a team-support skill.

---

## Wave 2 Passive A — Master Craftsman

Effect for Builder only:

`+30%`

to:

- mining speed
- building speed
- repair speed

Do not call this passive "Power Tools".

---

## Wave 2 Passive B — Expanded Backpack

Effect:

increase Builder's resource carrying capacity.

Use the existing backpack/inventory capacity system.

Expose the bonus as data.

Recommended initial value:

`+50% capacity`

---

## Wave 3 Active A — Warforge Blessing

Cooldown:

`16 s`

Fantasy tower buff.

Nearby towers temporarily receive:

- increased attack speed
- increased damage

Use magical/runic visual language.

Expose radius, duration and modifiers.

---

## Wave 3 Active B — Stone Ward

Cooldown:

`18 s`

Nearby player buildings temporarily receive significant damage reduction.

Expose:

- radius
- duration
- reduction amount

Use the existing damage pipeline rather than modifying building HP every frame.

---

## Wave 4 Passive A — Reinforced Masonry

Effect:

Buildings constructed/repaired by Builder receive increased Max HP.

Avoid repeatedly stacking the bonus every time a repair tick occurs.

Define clearly when ownership/Builder contribution marks the building for this passive.

Prefer the simplest rule compatible with the existing building system.

---

## Wave 4 Passive B — Prospector

Effect:

Mining has a chance to produce additional Ore.

Use the existing resource generation pipeline.

Expose chance and bonus amount.

---

## Wave 5 Active A — Runic Empowerment

Cooldown:

`30 s`

Duration:

`12 s`

Creates a large runic area.

Buildings inside receive:

- `+25% damage`
- `+35% attack speed`
- `+15% range`

Bonuses must be removed correctly when the effect ends.

Do not permanently mutate base tower stats.

---

## Wave 5 Active B — Emergency Repairs

Cooldown:

`28 s`

Behaviour:

- large repair pulse,
- restores a significant amount of nearby building HP,
- then applies short building HP regeneration.

Use the building repair system.

---

## Wave 6 Passive A — Mending Runes

Effect:

Nearby buildings slowly regenerate HP while Builder is close.

Expose:

- radius
- healing per second

Do not heal above Max HP.

---

## Wave 6 Passive B — Master Builder

Aura around Builder.

Nearby buildings receive small permanent-while-nearby bonuses to:

- damage
- attack speed
- damage resistance

Bonuses must appear/disappear when buildings enter/leave the aura.

Do not permanently modify base stats.

---

# 16. HEALER SKILLS

Healer is support but also specializes in fighting **Undead**.

The project needs a reliable way to know whether an enemy is Undead.

Before creating a new system, search for existing:

- enemy type,
- enemy faction,
- enemy tags,
- ScriptableObject enemy definition,
- enum/category.

Reuse the existing type system.

If there is no usable existing mechanism, add a minimal explicit enemy category such as:

`EnemyType.Undead`

Do not identify undead only by GameObject name.

---

## Basic — Light Bolt

Cooldown:

`1.4 s`

Behaviour:

- ranged single-target holy projectile.

Damage:

normal damage against normal enemies.

Against Undead:

`+75% damage`

This multiplier can later combine with `Undead Bane`.

---

## Wave 1 Active A — Healing Circle

Cooldown:

`10 s`

Behaviour:

Creates an area on the ground.

Players inside:

- regenerate HP over time.

Undead inside:

- receive holy damage over time.

Normal enemies:

- are not damaged by the healing effect unless another system explicitly says otherwise.

---

## Wave 1 Active B — Holy Pulse

Cooldown:

`10 s`

Behaviour:

- instant AoE around Healer,
- heals nearby players.

Normal enemies:

- no damage.

Undead:

- large holy damage.

---

## Wave 2 Passive A — Healing Aura

Nearby players slowly regenerate HP.

Expose:

- aura radius
- healing per second.

Do not heal dead/downed players unless the existing game rules allow it.

---

## Wave 2 Passive B — Empowering Aura

Nearby heroes gain a small damage bonus.

Expose:

- radius
- damage bonus

Recommended initial value:

`+10% damage`

Remove bonus when leaving aura.

---

## Wave 3 Active A — Sanctified Ward

Cooldown:

`20 s`

Duration:

`8 s`

Players in range receive protection from:

### Crowd Control

- Slow
- Freeze
- Stun

### Damage-over-time/status

On activation:

cleanse:

- Poison
- Burning

During effect:

prevent reapplication of:

- Poison
- Burning

Also prevent new:

- Slow
- Freeze
- Stun

Do not grant generic damage immunity.

Use the existing status-effect architecture.

---

## Wave 3 Active B — Core Mend

Cooldown:

`18 s`

Behaviour:

- directly heals the Core,
- temporarily increases Core damage resistance.

Healer is the class specialized in healing the Core.

Use the existing Core HP system.

---

## Wave 4 Passive A — Guardian Angel

When a nearby ally drops below:

`25% HP`

automatically trigger a small heal.

Each player should have their own internal cooldown for this passive.

Recommended initial cooldown:

`20 s per player`

Do not trigger every frame while the player remains below 25%.

---

## Wave 4 Passive B — Undead Bane

Effect:

All Healer damage against Undead:

`+35%`

This stacks with the innate Undead bonus from Healer skills.

Implement modifiers through the damage system rather than duplicating damage calculation in every skill.

---

## Wave 5 Active A — Divine Sanctuary

Cooldown:

`28 s`

Creates a large holy area.

Players inside:

- receive healing over time,
- receive temporary damage resistance.

Undead inside:

- receive continuous holy damage.

Normal enemies:

do not need to receive damage.

---

## Wave 5 Active B — Divine Intervention

Cooldown:

`45 s`

Large emergency AoE.

Players in range:

- receive a large instant heal.

All downed/dead-but-revivable players in range:

- are revived,
- return with `40% HP`.

Important:

- **no shield**
- revive all valid players in range, not just one.

Use the project's existing downed/revive implementation.

Do not bypass permanent-death rules if such rules exist.

---

## Wave 6 Passive A — Second Chance

When Healer receives lethal damage:

instead of dying:

- remain at `1 HP`,
- immediately receive a strong heal.

This passive requires an internal cooldown.

Recommended initial cooldown:

`90 s`

Do not trigger while already on cooldown.

Integrate at the damage/death decision point so death is actually prevented.

---

## Wave 6 Passive B — Beacon of Hope

Healing becomes stronger on low-HP allies.

Recommended curve:

- ally >75% HP -> normal healing
- ally <50% HP -> `+20% healing`
- ally <25% HP -> `+50% healing`

Implement through a shared healing modifier calculation so all appropriate Healer healing skills benefit consistently.

---

# 17. Status-effect integration

Several skills require:

- Stun
- Slow
- Freeze
- Burning
- Poison
- Taunt
- CC immunity
- status cleanse

Search the project first.

If the project already has a status-effect system, use it.

Do not create separate implementations of Slow/Burning inside each skill.

If no central system exists, create a minimal reusable status-effect architecture rather than one-off booleans in each skill.

Requirements:

- timed duration,
- safe refresh/removal,
- no permanent leftover modifiers,
- network-compatible,
- supports immunities,
- supports cleanse.

---

# 18. Stat modifiers

Passives and auras modify values such as:

- damage
- max HP
- attack speed
- cooldown
- movement/control state
- mining speed
- building speed
- repair speed
- tower damage
- tower attack speed
- tower range
- damage resistance
- backpack capacity
- healing

Do not directly overwrite base values and forget the original value.

Prefer a modifier system:

`finalValue = baseValue * additive/multiplicative modifiers`

or reuse the project's existing stat system.

Modifiers must have stable IDs/sources so they can be:

- added once,
- refreshed,
- removed,
- not accidentally stacked every Update.

---

# 19. Wave integration

Find the existing authoritative signal/event for:

`Wave defeated / Wave completed`

Do not poll scene text or guess the wave from UI.

When Wave N finishes:

1. update unlock state if N is 1, 3 or 5,
2. prepare the two appropriate skill options,
3. populate popup,
4. show popup to local player,
5. wait for that player's choice,
6. apply choice,
7. hide popup.

Each player must receive exactly one choice per Wave 1-6.

Protect against the same wave event firing twice.

---

# 20. Mission reset

At new mission/run:

Reset:

- selected active upgrades,
- passive upgrades,
- cooldowns,
- temporary skill state,
- modifier stacks,
- passive internal cooldowns,
- unlocked slot state,
- HUD icons,
- locks,
- Selected frame.

Initial state:

### Slot 1

- unlocked
- Basic Attack assigned
- selected
- cooldown ready
- lock hidden

### Slots 2-4

- empty
- locked
- Selected hidden
- cooldown UI in neutral/ready state
- Timer hidden

---

# 21. UI lifecycle

Do not rely on repeated `GameObject.Find` every frame.

Cache references.

Prefer a component such as:

`HeroSkillsUI`

with serialized references to the prepared scene UI objects.

It may internally use a small `SkillSlotUI` structure/class containing:

- Icon Image
- Timer TMP_Text
- Lock GameObject
- Cooldown Image
- Selected GameObject
- clickable Button

The popup may be handled by a separate:

`SkillUpgradePopupUI`

Again, reuse current project naming conventions if equivalent components already exist.

---

# 22. TextMeshPro

The Timer and popup labels should use the existing text component type.

If the scene uses TextMeshPro:

use `TMP_Text` / `TextMeshProUGUI`.

Do not replace existing TMP components with legacy `UnityEngine.UI.Text`.

---

# 23. Input architecture

Search the project to determine whether it uses:

- Unity Input System,
- PlayerInput,
- custom input manager,
- legacy Input.

Integrate with the existing approach.

Do not create a second competing input system.

Required actions:

- SelectSkill1
- SelectSkill2
- SelectSkill3
- SelectSkill4
- Next/Previous skill via mouse wheel
- Use selected skill via LMB

If actions already exist, reuse them.

Remember:

gameplay LMB/RMB must be blocked when pointer is over UI.

---

# 24. Visual effects / animation

The main priority in this task is functional gameplay.

Reuse existing:

- projectiles,
- hit VFX,
- status VFX,
- AoE indicators,
- particles

where possible.

If a skill needs a temporary placeholder effect, keep the code independent of the final VFX.

Do not make skill logic depend on a specific particle prefab.

Expose optional VFX prefab references in skill data.

---

# 25. Audio

If there is an existing audio event/system:

expose optional skill SFX references.

Do not introduce a new audio framework.

Skill gameplay must function with missing SFX.

---

# 26. Expected implementation structure

Adapt names to the existing project.

A reasonable target would be something equivalent to:

```text
Assets/
  Scripts/
    Skills/
      HeroSkillDefinition.cs
      HeroSkillController.cs
      HeroSkillRuntime.cs
      HeroPassiveController.cs
      HeroSkillsUI.cs
      SkillUpgradePopupUI.cs
      Skills/
        Warrior/
        Mage/
        Builder/
        Healer/

  Data/
    Skills/
      Warrior/
      Mage/
      Builder/
      Healer/
```

This is guidance, not a requirement.

If the repository already has an established folder structure, follow it.

---

# 27. Do not hardcode scene hierarchy everywhere

The exact existing names are provided so the initial scene can be wired.

Do not scatter code such as:

```csharp
GameObject.Find("Skill1Icon")
```

through gameplay logic.

Use serialized scene references / setup component.

If an automatic editor binding helper is useful, it may bind by these names once in Editor, but runtime gameplay should not depend on repeated hierarchy searches.

---

# 28. Error handling

If a required reference is missing:

- log one clear error identifying the missing reference,
- disable only the affected UI/skill feature if possible,
- do not spam errors every frame.

If an icon is missing:

- skill gameplay should still function,
- log a clear warning,
- use an existing placeholder sprite if the project has one.

---

# 29. Debug tools

Because this is being implemented/tested in `DebugScene`, add lightweight debug support if it fits the existing project.

Useful optional debug controls:

- simulate Wave 1-6 defeated,
- reset hero skill progression,
- force all active cooldowns ready,
- print currently selected skills/passives.

Do not leave cheat controls enabled in production builds unless the existing debug architecture already handles this.

Prefer editor/development-build-only debug functionality.

---

# 30. Acceptance criteria

The implementation is complete only when all of the following are true.

## HUD

- Slot 1 contains correct Basic Attack icon.
- Slots 2-4 begin locked.
- Skill2Lock hides after Wave 1.
- Skill3Lock hides after Wave 3.
- Skill4Lock hides after Wave 5.
- 64x64 icons are used in slots.
- exactly one Selected frame is active.
- clicking slot selects it.
- keyboard 1-4 selects valid slots.
- mouse wheel cycles valid slots.
- locked/empty slots are skipped.
- cooldown radial animates 0 -> 1.
- timer counts remaining time.
- timer hides when ready.

## Mouse/UI

- LMB on HUD does not attack/use skill.
- RMB on HUD does not build.
- clicking upgrade popup does not trigger world gameplay actions.

## Popup

- appears after Waves 1-6.
- never appears after Wave 7.
- uses two correct skill choices for the hero and wave.
- uses 256x256 icons.
- Label shows skill name.
- wave text displays `Wave N defeted`.
- active choices populate the correct next slot.
- passive choices apply without using a HUD slot.
- double-click cannot grant both options.

## Gameplay

- Basic Attack works immediately.
- selected slot determines LMB skill.
- cooldown prevents repeated use.
- all active skills work.
- all passive skills work.
- buffs/debuffs are removed correctly.
- no stat modifier stacks accidentally every frame.
- no permanent aura bonuses remain after leaving range.
- mission reset returns skill progression to initial state.

## Multiplayer

- each player chooses their own upgrades.
- local UI only controls local player.
- skill damage/healing/repair follows existing authoritative network rules.
- no duplicate projectiles/effects caused by host/client execution.
- popup does not pause the entire multiplayer session.

---

# 31. Implementation workflow for Codex

Follow this order.

## Step 1 — Inspect existing project

Before writing new systems, locate:

- hero/player controller,
- hero classes/class selection,
- health/damage code,
- enemy controller,
- enemy type metadata,
- wave manager,
- building system,
- mining system,
- repair system,
- Core HP system,
- Input implementation,
- multiplayer/network code,
- existing status effects,
- existing stat modifiers,
- `DebugScene` SkillsPanel,
- Skill Upgrade Popup.

Summarize internally how the new system will integrate before changing code.

## Step 2 — Build reusable skill data/runtime architecture

Implement definitions, cooldown runtime state and passive application.

## Step 3 — Implement HUD

Wire the existing SkillsPanel.

Do not redesign it.

## Step 4 — Implement input/select/use

Add keyboard, wheel, clicks and UI blocking.

## Step 5 — Implement wave upgrade popup

Connect to the real wave-completed event.

## Step 6 — Implement skills class by class

Recommended order:

1. Warrior
2. Mage
3. Builder
4. Healer

## Step 7 — Multiplayer validation

Test host and client.

## Step 8 — DebugScene validation

Run through simulated Waves 1-7 and confirm the full progression.

---

# 32. Final output expected from Codex

After implementation, provide:

1. List of files added.
2. List of files modified.
3. Short architecture summary.
4. Any Inspector references that still need manual assignment.
5. Any icon assets that could not be matched automatically.
6. Any existing project systems that prevented exact implementation.
7. Exact steps for testing the full system in `DebugScene`.
8. Confirmation that host/client multiplayer behaviour was considered.
