# The Toad Is Heaven's Uncle

## Raw Game Concept

**Status:** Draft  
**Genre:** 3D tower defense  
**Core theme:** Every beast carries both Yin and Yang, and every use of power creates karma.

## High Concept

The land is dying from drought. A small but fearless Toad gathers a company of animals and challenges the order of Heaven to bring rain back to the mortal world.

The player commands this animal formation through a series of tower-defense battles. Each animal tower has two sides:

- **Yang** provides the tower's desirable effects and expresses its intended role.
- **Yin** introduces harmful effects, dangerous trade-offs, or loss of control.

Every tower also has a **Karma bar**, called its **Cycle**. Combat actions fill the bar. When the bar reaches its limit, the tower must discharge its karma and enter its Yin phase. The player cannot rely on tower strength alone: victory comes from arranging the formation, learning every tower's Cycle, and preventing several dangerous Yin phases from overlapping.

The central fantasy is: **turn unavoidable consequences into a deliberate battle plan.**

## Theme and World

The concept is inspired by the Vietnamese folk image of the Toad as the animal bold enough to confront Heaven on behalf of a drought-stricken world.

### Player role

The player is the Toad, acting as commander rather than an ordinary tower. The Toad gathers animals, chooses the formation, manages water and money, and leads the company toward the Celestial Court.

### Possible campaign arc

1. **The Dried Fields** — animals rally around the Toad while defending the last sources of water.
2. **The Mountain Road** — the company fights through increasingly supernatural opposition.
3. **The Gate of Heaven** — enemies pressure both ground and air defenses and punish poor Cycle timing.
4. **The Celestial Court** — the final confrontation forces the player to master overlapping Yin and Yang states.
5. **The Return of Rain** — completing the journey restores water to the land.

This is a thematic direction, not a final narrative outline.

## Design Pillars

### 1. Every tower is a double-edged tool

No animal is permanently safe or permanently harmful. Its value changes with its current phase.

### 2. Timing is part of formation building

Placement is only the first layer. Players must also understand when each tower gains karma, when it enters Yin, and how long it takes to recover.

### 3. Harmful effects can create tactical opportunities

Some Yin effects may interact with another tower's Yang effect. A dangerous state should sometimes be manageable or exploitable, but it should never become automatically better than Yang.

### 4. Readability is essential

The player should be able to understand the current phase, Karma amount, incoming state change, affected area, and active buffs or debuffs without opening a detailed menu.

## Core Battle Loop

1. Choose an animal lineup before the stage.
2. Spend water or money to deploy towers and shape the formation.
3. Defeat enemy waves while towers gain karma through kills, attacks, passive generation, or tower-specific rules.
4. Read each Cycle and prepare for incoming Yin phases.
5. Stagger, isolate, or deliberately combine tower phases to reduce the damage caused by Yin.
6. Survive the final wave and advance toward Heaven.

## Working Karma Cycle

The following is a proposed model inferred from the current idea and still needs confirmation:

1. A tower begins in **Yang** with an empty Karma bar.
2. The tower accumulates karma from its defined sources.
3. Reaching the tower's **Cycle** value triggers its **Yin** phase.
4. During Yin, karma is discharged at the tower's listed rate.
5. When karma reaches zero, the tower returns to Yang and begins the next Cycle.

### Shared terms

| Term | Working meaning |
|---|---|
| **Karma** | The current value accumulated by a tower. |
| **Cycle** | The maximum Karma value that triggers Yin. |
| **Karma gain** | Karma added by kills, attacks, passive generation, or external effects. |
| **Karma discharge** | Karma removed during Yin, expressed as a rate or per-attack amount. |
| **Yang window** | The desirable operating phase. |
| **Yin window** | The dangerous operating phase that the player must plan around. |

## Tower Classes

| Class | Primary purpose | Current towers |
|---|---|---|
| **Physical** | Reliable direct damage and front-line control. | Bear |
| **Magic** | Area damage, poison, and magical scaling. | Bee |
| **Support** | Crowd control, aura effects, detection, and economy. | Crab, Water Tower |
| **Special Grade** | Expensive, highly specialized power with unusual rules. | Fox |

## Tower Roster

### Bear — Physical

**Combat profile**

- Close-range tower.
- Targets ground enemies.
- Generalist physical fighter.

**Yang**

- Attacks deal area-of-effect damage.
- Attacks slow affected enemies.

**Yin**

- Attacks become single-target.
- Enemies inside the Bear's attack range gain bonus movement speed.

**Karma values**

- Cycle: **200**.
- Karma discharge: **20 per second**.
- Estimated Yin duration from a full bar: **10 seconds**, before modifiers.

**Tactical identity**

The Bear is easy to place but dangerous to mistime. Its Yang phase holds a lane together; its Yin phase can suddenly accelerate an entire group past the formation.

### Bee — Magic

**Combat profile**

- Close-range area attacker.
- Targets both ground and flying enemies.

**Yang**

- Uses a wide attack area.
- Deals area-of-effect magic damage.
- Applies poison.
- Deals increased magic damage to an enemy based on that enemy's bonus movement speed above its base movement speed.
- The conversion coefficient or bonus uses a fixed tuning value that has not yet been defined.

**Yin**

- Applies an area buff to enemy physical armor.
- Creates an additional shield for affected enemies.

**Karma values**

- Cycle: **500**.
- Karma discharge: **5 per attack and 5 per second**.
- Exact Yin duration depends on the Bee's attack frequency.

**Tactical identity**

The Bee is a magic area-damage tower that can convert enemy speed bonuses into damage. It is especially useful against mixed ground-and-air waves, but its Yin phase can make the wave much harder for physical towers to finish.

### Fox — Special Grade

**Combat profile**

- Very expensive to purchase.
- Close-range, single-target attacker.
- Targets ground enemies.
- Intended as an elite or high-value-target hunter.

**Yang**

- Each attack generates **1.5 Karma** for the Fox.
- Uses a close-range, single-target attack pattern.

**Yin**

- Bites at an enemy's weak point.
- Prioritizes high-value or high-priority targets.

**Conditional Yin rule**

- If the Fox is debuffed while it is in Yin, it gains attack range and attack speed.
- Its Karma discharge becomes **7 per second** while this condition is active.

**Karma values**

- Cycle: **50**.
- Base Karma discharge: **5 per second**.
- Base Yin duration from a full bar: **10 seconds**, before conditional changes.

**Tactical identity**

The Fox is a premium precision tower for bosses, elites, or dangerous support enemies. Its current Yin description grants better targeting and can grant extra combat power, so its actual harmful Yin cost still needs to be defined.

### Crab — Support

**Combat profile**

- Area crowd-control tower.
- Provides aura effects to other towers within range.

**Yang**

- Applies an area slow to enemies.
- Reveals or removes enemy stealth within range.
- Grants armor penetration to allied towers within range.

**Yin**

- No longer slows enemies.
- No longer reveals stealth.
- No longer grants armor penetration.
- Reduces attack speed. The affected side still needs confirmation; the current concept assumes this penalty applies to allied towers within range.
- Increases the Karma gain of allied towers within range.
- Doubles the income-generation speed of economy towers within range.

**Karma values**

- Cycle: **300**.
- Karma discharge: **10 per second** when no enemy is present.
- Karma discharge: **20 per second** while at least one enemy is present.
- Estimated Yin duration: **30 seconds** without enemies, or **15 seconds** while enemies are present.

**Tactical identity**

The Crab is the formation's rhythm controller. Yang stabilizes the battlefield, while Yin destabilizes nearby tower Cycles in exchange for an economy burst. Its placement determines how much of the formation is exposed to that trade-off.

### Water Tower — Support / Economy

**Combat profile**

- Generates water for the player.
- Builds its own Karma rather than relying only on kills.

**Yang**

- Produces water at a fast, stable rate.
- Passively accumulates its own Karma while producing water.

**Yin**

- Halves the time required to produce water, effectively doubling its production frequency.
- Discharges Karma quickly.

**Karma values**

- Cycle: **100**.
- Karma discharge: **10 per second**.
- Estimated Yin duration from a full bar: **10 seconds**, before modifiers.

**Tactical identity**

The Water Tower connects the drought theme to the economy. Its current Yin effect increases production and shortens the Yin window, so an additional harmful cost may be needed if every Yin phase must be clearly negative.

## Current Tower Summary

| Tower | Class | Targets | Cycle | Karma discharge | Yang identity | Yin danger |
|---|---|---|---:|---|---|---|
| **Bear** | Physical | Ground | 200 | 20/s | Area damage and slow | Single-target; speeds up enemies in range |
| **Bee** | Magic | Ground and air | 500 | 5/attack + 5/s | Wide magic area damage, poison, speed-scaling damage | Grants enemy physical armor and shields |
| **Fox** | Special Grade | Ground | 50 | 5/s; 7/s when condition is active | Expensive single-target hunter; gains 1.5 Karma/attack | High-priority targeting and conditional power currently lack a clear downside |
| **Crab** | Support | Area/aura | 300 | 10/s; 20/s with at least one enemy | Slow, stealth reveal, armor penetration aura | Removes Yang utility, disrupts nearby towers, accelerates economy |
| **Water Tower** | Support/Economy | Not applicable | 100 | 10/s | Stable water production and self-generated Karma | Doubled production currently lacks a clear downside |

## Emergent Formation Ideas

These interactions are promising directions, not locked rules.

### Bear Yin into Bee Yang

The Bear's Yin phase gives enemies bonus movement speed. That is dangerous, but the Bee's Yang damage scales from bonus movement speed. A well-timed overlap could let the Bee punish the exact danger created by the Bear.

### Crab Yin economy window

Placing the Crab near Water Towers could create a strong economy burst during Crab Yin. The cost is that nearby combat towers gain Karma faster, making a chain of Yin phases more likely.

### Crab Yang formation anchor

The Crab's slow, stealth reveal, and armor-penetration aura make it a natural center for a mixed formation during Yang. The player must decide whether the strong aura is worth exposing many towers to Crab Yin.

### Fox priority execution

The Fox can remove bosses, elites, stealth supports after detection, or other high-value enemies before they break the formation. Its high purchase price should make deploying it a strategic commitment.

## Readability and Feedback Needs

Each tower should communicate its Cycle without requiring the player to memorize hidden timers.

- A circular Yin-Yang meter shows current Karma and the Cycle threshold.
- The tower visibly changes posture, color balance, or aura before entering Yin.
- A short warning appears before a Cycle reaches full.
- Range previews show which enemies or allied towers will receive Yin effects.
- Buff and debuff icons distinguish effects applied to enemies from effects applied to allied towers.
- Formation-wide warnings appear when multiple towers are about to enter Yin together.

## Open Design Questions

These questions should be answered before this concept becomes a balance specification:

1. Does reaching full Karma always trigger Yin immediately, or can the player delay or manually release it?
2. Does every tower return to Yang only when Karma reaches zero?
3. How much Karma does a kill generate, and does it depend on enemy value?
4. Can towers gain Karma while already in Yin?
5. Can external effects change Karma discharge speed, or only Karma gain?
6. Is water the main building currency, a separate resource, or another name for money?
7. Does the Crab's Yin attack-speed reduction affect allied towers, enemies, or both?
8. What is the fixed coefficient and maximum bonus for the Bee's movement-speed-based magic damage?
9. What is the Fox's actual Yin drawback? Its current Yin behavior is mostly beneficial.
10. What is the Water Tower's actual Yin drawback if doubled water production and fast discharge are both beneficial?
11. Are flying enemies affected by ground-based area effects such as the Crab's auras or the Bear's slow?
12. Can a tower be upgraded to alter its Cycle size, Karma gain, discharge rate, or Yin effect?

## Concept Promise

The game's identity is not simply animal towers fighting waves. It is a tower-defense game about **rhythm, consequence, and formation discipline**. The Toad wins against Heaven because the player learns when every ally is powerful, when every ally becomes dangerous, and how one animal's weakness can become another animal's opportunity.
