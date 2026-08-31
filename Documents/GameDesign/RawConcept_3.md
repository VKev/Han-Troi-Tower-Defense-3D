# Projectile Network TD

## Raw Game Concept and Prototype Gameplay Contract

**Status:** Approved  
**Document type:** Clarified raw concept plus deterministic prototype gameplay contract  
**Source:** [Projectile_Network_TD_Prototype_GDD_Technical_Contract_V0_2.docx](../Raw/Projectile_Network_TD_Prototype_GDD_Technical_Contract_V0_2.docx)  
**Source version:** V0.2 Dev Prototype  
**Source revision date:** 16 August 2026  
**Owner approval date:** 16 August 2026  
**Intended users:** Game Design, development, and prototype QA  
**Primary platform:** Android, landscape, touch-first  
**Engine target:** Unity 3D  
**Camera:** Fixed three-quarter orientation with pan and zoom; no player-controlled orbit  
**Prototype slogan:** **Build the shot, not the gun.**

## Contents

The source order is preserved: contract, systems, data, validation, testing, and handoff.

- 0. Authority and Decision Discipline
- 1. Pitch and Creative Spine
- 2. Prototype Scope
- 3. Player Loop and Phases
- 4. Graph Grammar and Activation
- 5. Node Roster
- 6. Projectile Model and Lifecycle
- 7. Damage Contract
- 8. Elements and Reactions
- 9. Charge Systems
- 10. FIFO, Capacity, Reservation, and Backpressure
- 11. Upgrade and Sell Contract
- 12. Economy V0
- 13. V0 Balance Configuration
- 14. Enemy Contract V0
- 15. 3D Level and Camera
- 16. UX and Readability
- 17. Technical Architecture Guidance
- 18. Resolution Order
- 19. Acceptance Criteria
- 20. Edge-Case Contract
- Worked Gameplay Examples added by this clarification pass
- 21. Playtest Plan
- 22. Implementation Milestones
- 23. Open Decisions
- 24. Developer Handoff Summary

## Concept Boundary

This is a third, independent game direction. It does not replace or silently merge with `RawConcept_1.md` or `RawConcept_2.md`.

The source DOCX describes an implementation-ready greybox prototype. This Markdown version preserves its gameplay rules, balance tables, test plan, and handoff material while making the player experience and technical terms explicit. Statements such as “build,” “implement,” or “test” inside this document are specification content for a future prototype; they are not evidence that the feature already exists.

The source DOCX refers to `docs/00_assignment_contract.md` and `docs/10_projectile_network_td_pitch_prototype_brief.md`. Those references are retained as source provenance, but their presence and current authority must be verified in the repository that will implement this concept.

This concept does not automatically inherit the Toad, Yin-Yang, Karma, Water-economy, Bear, Bee, Fox, or Crab proposals from earlier directions. A Toad or spiritual narrative wrapper remains an open creative choice. The greybox mechanic must be readable without it.

## Decision Labels

- **DECIDED:** The human game owner has confirmed the rule. In this clarified document, a decided gameplay rule is part of the prototype contract.
- **CONTRACT RULE:** Required prototype behavior. Changing it changes the mechanic and requires an owner decision.
- **V0 DEFAULT:** A recommended starting value or behavior so implementation and testing can proceed. It must be data-driven and may be tuned after playtesting.
- **OPEN / source UNKNOWN:** A decision that is not settled. It must not be presented as a production rule.
- **OUT OF SCOPE:** Deliberately excluded from this prototype.

Unless a paragraph is explicitly labeled **V0 DEFAULT**, **OPEN**, or **OUT OF SCOPE**, the rules in Sections 4-19 are treated as the prototype gameplay contract derived from the source DOCX.

## The Game in Sixty Seconds

Projectile Network TD is a 3D tower-defense game in which towers do not aim at enemies.

The player places nodes and connects them into one or two directed chains. A Generator creates a projectile. Each connected Processor receives that same logical projectile, may change its payload or charge a local ability, queues it, and fires it toward the next node. The straight 3D line between two nodes is both a transport route and an attack segment. Any enemy collider genuinely crossed by the projectile can be hit.

Every valid chain ends at one unique **Soul Nexus**. The Soul Nexus is not a normal attacking tower and does not fire another projectile. It instantly consumes arriving projectiles, reads how many different enemies each projectile directly hit during its journey, converts that count into **Soul**, and fills a shared Soul Meter. When the meter is full, the player can cast a field skill at a chosen location.

The strategic puzzle has four interacting layers:

1. **Geometry:** Which 3D segments physically cross the enemy path?
2. **Payload:** In what order should Element Processors rewrite the projectile?
3. **Throughput:** Can every Processor accept and forward projectiles without causing backpressure?
4. **Timing:** When should the player spend the full Soul Meter on a battlefield field effect?

The player wins by clearing all three prototype waves while keeping Base HP above zero. The player loses when Base HP reaches zero.

## What the Soul Nexus Is

**Soul Nexus** is the English gameplay name for the network's unique terminal node. It is separate from the enemy leak destination and does not own Base HP.

The Soul Nexus has five jobs:

1. It provides the mandatory endpoint that makes a chain valid.
2. It receives up to two independent chains through two input ports.
3. It instantly consumes every arriving projectile instead of storing or forwarding it.
4. It converts the projectile's unique direct-hit count into Soul.
5. It owns the shared Soul Meter and the player-cast Soul Skill.

The Soul Nexus is free, unique, cannot be sold, and can only be placed on one of two Endpoint Pads. It has no output port, no ammunition queue, no capacity limit, and no processing cooldown. A full Soul Meter never blocks incoming projectiles. Excess Soul is deliberately lost, creating a timing tradeoff: cast early for charge efficiency, or hold the skill for a more valuable combat moment.

Example: one projectile directly hits Enemy A, Enemy B, and Enemy A again before reaching the Soul Nexus. The repeated contact with Enemy A is ignored because one projectile can direct-hit the same enemy only once. The projectile therefore grants 2 Soul. If its reaction also damages Enemy C through an area effect, Enemy C grants no Soul because that was not a direct projectile hit.

## Player-Facing Mental Model

The simplest correct explanation for a new player is:

> Connect a Generator to at least two Processors and then to the Soul Nexus. Every connection becomes a real shot line. Place the nodes so those lines cross enemies, choose the Processor order to shape the shot, and prevent slow Processors from jamming the chain.

The player is not expected to think in engine terms such as reservations or stable IDs. Those systems must be visualized as readable battlefield states:

- A bright directional line means “this node will fire there.”
- A trajectory preview crossing an enemy means “this shot can direct-hit that enemy.”
- Filled queue pips mean “this Processor is storing ammunition.”
- A reserved incoming pip means “a projectile already in flight owns this future slot.”
- A highlighted upstream route means “this downstream Processor is the bottleneck.”
- Element shape and trail mean “the projectile currently carries this element or reaction.”
- The large Soul Meter means “successful direct hits are charging the active field skill.”

## Core Vocabulary

| Term | Clear meaning in this concept | What it does not mean |
| --- | --- | --- |
| Node | A placed network object with defined input and output ports. | It is not automatically a tower that targets enemies. |
| Normal node | Generator, Element Processor, Support Processor, or Special Pulse Processor. | It is not the Soul Nexus. |
| Processor | A normal node that receives, handles, queues, and forwards projectiles. | It does not create the original projectile. |
| Generator | The chain's source node. It creates neutral projectiles with Physical Damage. | It does not require an input and does not target enemies. |
| Soul Nexus | The unique terminal node that consumes projectiles and converts their direct-hit history into Soul. | It is not the enemy Base, a normal Processor, buffer, or projectile weapon. |
| Endpoint Pad | One of two authored locations where the free Soul Nexus may be placed. | It is not a normal paid build slot or a second Soul Nexus. |
| Base | The separate level objective whose HP is reduced when enemies leak through the route. | It is not the movable Soul Nexus. |
| Port | A legal graph connection point. Outputs connect to inputs. | It is not a physical inventory slot. |
| Link | A directed edge from one node's output to the next node's input. | It is not only a visual cable. |
| Firing segment | The straight 3D projectile route created by one link. | It is not a screen-space line or guaranteed damage. |
| Chain | One complete directed path from a Generator through at least two Processors to the Soul Nexus. | It is not any disconnected group of nodes. |
| Active chain | A chain that passes every graph and range validation rule and may emit projectiles during a wave. | An incomplete or invalid chain is never partially active. |
| Projectile | A persistent logical shot with one current target, damage data, element/reaction state, and lifetime hit history. | It does not seek enemies or collide with other projectiles. |
| Direct hit | A swept 3D projectile collision with an enemy not yet in that projectile's direct-hit set. | Reaction area damage and tower pulses are not direct hits. |
| Payload | The projectile's Physical Damage, Magic Damage, Base Element, reaction state, and proc availability. | It does not include the Processor's local Charge. |
| Rewrite | Replacing the projectile's Magic payload and elemental state at an Element Processor. | Magic Damage does not accumulate across every Element Processor. |
| Reaction | A temporary terminal projectile state created by a valid pair of consecutive elements. | It is not an unlimited combo chain. |
| Reaction proc | The reaction's one special secondary effect on its first valid downstream direct hit. | It does not repeat on every later hit. |
| Queue | A Processor's first-in-first-out storage for arrived projectiles waiting to emit. | It does not include the Soul Nexus. |
| Capacity | The maximum total of queued projectiles plus reserved incoming projectiles at a Processor. | It is not only the number currently visible in the queue. |
| Reservation | Ownership of a future Processor slot granted before a projectile is emitted toward it. | It is not a second projectile or a permanent lock. |
| Backpressure | Upstream firing stops because the next Processor has no reservable capacity. | Full Soul or full local Charge does not cause backpressure. |
| Internal Charge | A Support Processor's local energy, gained on projectile arrival and drained while its aura is active. | It is not Soul and is not stored on the projectile. |
| Pulse Charge | A Special Processor's local counter that automatically triggers a pulse at threshold. | It is not manually cast and grants no Soul. |
| Soul | Shared active-skill energy earned from unique direct hits recorded by consumed projectiles. | It is not damage, currency, or Processor capacity. |
| Soul Field | The chosen battlefield area affected by the Soul Skill. | It is not a permanent aura and does not block projectiles. |
| Status | A timed enemy effect such as Burn, Slow, Freeze, Armor Break, or Resistance Break. | It is not the same as projectile reaction state. |
| Field | A world-space area with a duration and radius. | A field is not attached to a projectile after the projectile leaves it. |
| Physical Armor | Defense applied only to Physical Damage. | It does not mitigate Magic Damage. |
| Magic Resistance (MR) | Defense applied only to Magic Damage. | It does not mitigate Physical Damage. |
| Preparation | The between-wave editing phase. | The network does not fire during this phase. |
| Wave | The locked combat phase in which the network runs automatically. | Building, selling, upgrading, and relinking are not allowed. |

# 0. Authority and Decision Discipline

## 0.1 Design authority

- The source DOCX identifies `docs/00_assignment_contract.md` as the formal assignment source. Verify that file in the implementation repository before relying on the claim.
- This document is the clarified gameplay source of truth for the Projectile Network TD V0 prototype only.
- The source DOCX states that it supersedes older rules in `docs/10_projectile_network_td_pitch_prototype_brief.md` concerning Support and Special consumers, the previous two-element limit, Soul-full endpoint backpressure, and an incomplete reaction matrix.
- Older Karma, Yin-Yang, Water Pillar, Toad, Bear, Bee, Fox, and Crab proposals are not inherited automatically.
- **OWNER-DECIDED:** The title remains **Projectile Network TD**. The approved presentation direction is stylized soul-powered dark fantasy with a slightly dark tone rather than horror.
- **OWNER-DECIDED:** Prototype interface copy is Vietnamese. The tutorial itself uses no instructional prose: only hand gestures, icons, highlights, and animation.
- **OWNER-DECIDED:** The web prototype preserves the Link prototype's direct drag-to-link interaction. The camera permits pan and zoom but never player-controlled orbit.

## 0.2 Prototype invariants

- Towers never auto-target enemies and never fire directly at an enemy target.
- A directed node link is the projectile's real 3D travel path and the primary source of combat damage.
- A direct hit occurs only when the projectile's swept collision volume intersects a real enemy collider in world space.
- Each normal node has at most one input and one output.
- The Soul Nexus is the only multi-input node and has no output.
- Every valid chain begins at one Generator and ends at the one Soul Nexus.
- All chains charge the same Soul Meter.
- Every balance number is stored in data or configuration rather than hard-coded into runtime logic.

# 1. Pitch and Creative Spine

## 1.1 One-line pitch

The player does not place towers that shoot enemies; the player connects nodes to draw real 3D projectile routes through the battlefield.

## 1.2 Unique selling proposition

> **Build the shot, not the gun.**

In conventional tower defense, each tower searches for enemies inside a range. In Projectile Network TD, a tower sends a projectile only to its connected receiver node. The positions of two nodes create a firing segment. The order of Processors creates the payload. Processing speed creates bottlenecks. Damage happens only where the real projectile path intersects enemy geometry.

## 1.3 Creative spine

The player should feel that they are engineering a living defensive circuit: routing, transforming, charging, and finally releasing projectiles through a battlefield that reacts directly to the network's geometry and timing.

## 1.4 Intended emotional rhythm

- **Before a wave:** calculate, place, predict, and commit.
- **During a wave:** watch shots hit or miss, observe reaction procs, diagnose charge rhythms, and decide when to cast Soul.
- **After success:** understand that the result came from arrangement, sequence, and upgrade choices.
- **After failure:** identify which segment missed, which queue jammed, or which enemy defense countered the damage packet.

## 1.5 Design hypothesis

If each link is both transport and weapon, one placement can change trajectory, hit opportunity, elemental order, local charge rate, and throughput at the same time. Human playtesting must prove or reject whether this creates readable, meaningful decisions. A functioning build alone does not prove the game is fun or understandable.

## 1.6 Approved presentation and interaction direction

- **Title:** Projectile Network TD.
- **Theme:** stylized soul-powered dark fantasy, atmospheric and slightly dark, but colorful enough to preserve combat readability.
- **World language:** ancient soul channels, ritual stones, spectral processors, cursed enemies, and a Soul Nexus replace mechanical or arcane-industrial presentation without changing the network rules.
- **Prototype art route:** authored procedural Three.js geometry and effects at the same production scope as the two existing web prototypes. External hero assets are not required for this V0.
- **Language:** all player-facing prototype UI copy is Vietnamese.
- **Tutorial:** text-free hand, icon, glow, pulse, and trajectory animation. No instructional sentences or modal lesson copy.
- **Link gesture:** select or press a source node, drag toward a highlighted legal receiver, and release to confirm. The same gesture works for touch and pointer input.
- **Camera:** fixed three-quarter yaw and pitch. The player may pan and zoom, but may not orbit or rotate the view.

# 2. Prototype Scope

## 2.1 Included

- One stylized procedural dark-fantasy map with one authored enemy route and a fixed three-quarter camera orientation.
- Two elevation tiers: Low and High.
- Ten normal build slots and two Endpoint Pads.
- One free Soul Nexus placed by the player on either Endpoint Pad.
- Two Soul Nexus input ports and at most two independent active chains.
- Four purchasable combat-node types: Generator, Element Processor, Support Processor, and Special Pulse Processor.
- Four elements: Fire, Ice, Wind, and Earth.
- One elemental ring, four self reactions, four mixed reactions, and two opposite-pair rewrites.
- Projectile travel time, piercing, and one direct hit per enemy per projectile lifetime.
- FIFO queues, finite Processor capacity, incoming slot reservation, and Processor backpressure.
- Parallel Physical and Magic damage channels.
- Separate Physical Armor and Magic Resistance.
- Level-local build, upgrade, and sell economy with no meta progression.
- One mutually exclusive upgrade branch choice per node instance.
- Support base buff with Buff and Debuff specializations.
- Special Processor automatic Pulse Charge and pulse release.
- Base Soul slow field with Soul Suppression and Conduction Field branches.
- Five enemy test archetypes including a boss.
- Three waves, win, fail, and clean retry.
- Mobile-readable HUD and a prototype debug overlay.

## 2.2 Out of scope

- Final production art, cinematics, voice-over, or long-form narrative.
- Network cycles, branching between Processors, or merging before the Soul Nexus.
- More than one Soul Nexus in a level.
- Editing links, building, selling, or upgrading during a wave.
- Projectile gravity, homing, ricochet, or projectile-projectile collision.
- Meta progression, persistent numerical upgrades, gacha, monetization, or LiveOps.
- A full ten-level content set; the prototype is one vertical-slice level.
- Online services or account systems.

## 2.3 What prototype success means

Prototype success means the core mechanic is understandable and creates deliberate choices. It does not approve final balance, final fun, onboarding quality, market appeal, art direction, or Android performance. Those require separate evidence.

# 3. Player Loop and Phases

## 3.1 Core loop

1. Read the next wave preview, route timing, and enemy defenses.
2. Place the free Soul Nexus on one Endpoint Pad.
3. Build a Generator and at least two Processors.
4. Connect them into a complete chain ending at an unused Soul Nexus input port.
5. Inspect the actual 3D trajectory preview and correct intended hits or misses.
6. Choose affordable upgrade branches during Preparation.
7. Start the wave.
8. Observe direct hits, misses, statuses, queue occupancy, reservations, local Charge, reactions, and Soul gain.
9. Cast the Soul Skill when the meter is full and the timing is valuable.
10. Receive kill income and the wave-clear reward.
11. During the next Preparation phase, sell, rebuild, relink, reposition the Soul Nexus, or upgrade.
12. Clear the final wave while Base HP remains above zero.

## 3.2 Phase contract

### Preparation

The player may build, connect, relink, upgrade, sell, and reposition the Soul Nexus between its two Endpoint Pads. Enemies do not move. Generators do not fire. Internal Charge, Pulse Charge, and Soul do not increase or drain.

The UI must validate every chain immediately. A player must never need to start a wave to discover that a network is inactive.

**OWNER-DECIDED:** The Start Wave control is enabled only after the player has placed the one Soul Nexus and created at least one active chain. An invalid or incomplete chain does not satisfy this gate.

### Wave

The network runs automatically from timers and deterministic rules. The player may inspect nodes and cast the Soul Skill. Building, selling, upgrading, relinking, and Soul Nexus repositioning are locked.

### Result

- **Win:** every configured wave is cleared and Base HP is greater than zero.
- **Fail:** Base HP reaches zero at any time.
- **Retry:** reload the level's initial state. Do not preserve currency, purchases, branches, queues, reservations, projectiles, statuses, local Charge, Soul, or active fields.

### Preserved state between waves

**OWNER-DECIDED:** When a wave ends, the simulation enters Preparation without clearing its combat state.

- Preserve every in-flight projectile at its current position and segment progress.
- Preserve every Processor queue in FIFO order.
- Preserve every incoming reservation and its projectile ownership.
- Preserve Internal Charge, Pulse Charge, and Soul.
- Freeze projectile movement, node timers, queue emission, Charge gain, and Charge drain throughout Preparation.
- Resume preserved simulation state when the next wave begins.
- Delete every active Soul Field and every timed enemy status when the wave ends. Neither fields nor statuses survive into Preparation or resume in the next wave.

### Graph edits during Preparation

**OWNER-DECIDED:** Relinking a connection, repositioning the Soul Nexus, or selling a normal node affects every chain whose topology or projectile trajectory uses that edited node or link.

- Despawn every in-flight projectile owned by each affected chain.
- Clear every Processor queue owned by each affected chain.
- Release every incoming reservation owned by each affected chain exactly once.
- Preserve Internal Charge and Pulse Charge on surviving nodes in the affected chain.
- Preserve the shared Soul Meter.
- Destroy all local state belonging to a sold node. A rebuilt or replacement node starts as a clean instance.
- Do not clear or modify the frozen projectile, queue, reservation, or Charge state of an unaffected chain.

This mutation cleanup occurs only when the player edits an affected chain during Preparation. It does not replace the general wave-boundary preservation policy above.

## 3.3 Meaningful decisions

- Which segment crosses the enemy route at the most valuable time and height?
- Which Processor order creates the desired base status or reaction on the desired segment?
- Should the player improve a downstream bottleneck or increase payoff damage?
- Is a second chain worth more than upgrading the first chain?
- Should Support improve Processor throughput or replace that benefit with enemy defense reduction?
- Should the Soul Skill be cast immediately to avoid overflow, or held for a dangerous group?
- Which Endpoint Pad creates the best final segment and two-chain routing geometry?

# 4. Graph Grammar and Activation

## 4.1 Node topology

| Node | Inputs | Outputs | Network responsibility |
| --- | ---: | ---: | --- |
| Generator | 0 | 1 | Creates the original projectile. |
| Element Processor | 1 | 1 | Rewrites Magic payload and resolves element pairing. |
| Support Processor | 1 | 1 | Gains Internal Charge, maintains an aura, and forwards the projectile. |
| Special Pulse Processor | 1 | 1 | Gains Pulse Charge, auto-pulses, and forwards the projectile. |
| Soul Nexus | 2 in V0 | 0 | Instantly consumes projectiles, grants Soul, and owns the Soul Skill. |

## 4.2 Valid chain

**CONTRACT RULE - OWNER DECIDED:** Every chain requires at least two Processors. A shorter route is invalid and cannot emit. This prevents a Generator-to-Soul-Nexus shortcut from bypassing the mechanic being tested.

Minimum valid example:

```text
Generator -> Processor -> Processor -> Soul Nexus
```

Two chains may run in parallel and terminate at separate Soul Nexus input ports:

```text
Generator A -> Fire Processor -> Support Processor --\
                                                       > Soul Nexus
Generator B -> Earth Processor -> Special Processor --/
```

The two chains do not share normal nodes and do not merge anywhere before the Soul Nexus.

## 4.3 Activation validation

A chain is **ACTIVE** only when all conditions are true:

- it begins at a Generator;
- it contains at least two Processors;
- it ends at an unused Soul Nexus input port;
- every link is within Maximum Link Range;
- it contains no cycle;
- no normal node exceeds one input or one output;
- every referenced node and port exists and is enabled.

An incomplete or invalid chain emits no projectile at all. The UI must highlight the invalid node or link and show a short reason such as `MISSING ENDPOINT`, `NEEDS 2 PROCESSORS`, `INPUT OCCUPIED`, `OUT OF RANGE`, or `CYCLE NOT ALLOWED`.

## 4.4 Link rules

- A link is a directed edge from an output port to an input port.
- The output target may be changed only during Preparation.
- The runtime segment is the straight world-space path from the emitter muzzle anchor to the receiver anchor.
- Maximum Link Range is measured between emitter and receiver anchors on the XZ plane. Vertical separation does not increase the range measurement.
- Authored walls and terrain block a link. Obstruction validation runs during trajectory preview; a blocked link is invalid and cannot be confirmed.
- Preview and runtime must use the same anchors, range calculation, and collision radius.
- Selling a normal node removes adjacent links and revalidates affected chains.
- Repositioning the Soul Nexus retains its existing links, revalidates them from the new position, and deletes each link that becomes invalid. The game must not silently move or create normal nodes to repair a chain.

## 4.5 Deliberately forbidden topologies

- No normal node has two inputs.
- No normal node has two outputs.
- No output branches to two receivers.
- No two chains merge at a Processor.
- No cycle or loop is legal.
- No chain ends at Support or Special.
- No chain is active without a Soul Nexus endpoint.

# 5. Node Roster

## 5.1 Generator Tower

**Verb:** Generate.

- Has no input.
- Creates one projectile whenever its Fire Interval is ready, even if no enemy is present.
- Emits only when its output belongs to an active chain and the next Processor has reservable capacity.
- Creates a neutral payload: configured Physical Damage, zero Magic Damage, `BaseElement = Neutral`, and `ReactionState = None`.
- May have multiple projectiles in flight if each target Processor slot was successfully reserved.
- Wasted geometry is possible by design: a projectile may hit no enemy and still continue through the chain.

### Upgrade branches

- **Rapid:** shorter Fire Interval and lower Physical Damage per projectile. Creates more hit, Charge, Soul, and downstream-pressure opportunities.
- **Heavy:** longer Fire Interval and higher Physical Damage per projectile. Produces less pressure but more Physical value per successful direct hit.

## 5.2 Element Processor

**Verb:** Transform.

- Resolves element and reaction state immediately when a projectile arrives.
- Never changes Physical Damage.
- Rewrites rather than adds Magic Damage.
- Enqueues the transformed projectile in FIFO order.
- Emits the first queued projectile when its Process Interval is ready and the downstream receiver is available.
- Each instance has exactly one configured element: Fire, Ice, Wind, or Earth.

### Upgrade branches

- **Conduit:** shorter Process Interval and greater capacity, with lower Magic and reaction potency.
- **Resonance:** longer Process Interval and smaller capacity, with higher Magic and reaction potency.

## 5.3 Support Processor

**Verb:** Sustain.

- On projectile arrival, gains 1 Internal Charge, then enqueues and forwards the unchanged payload.
- Internal Charge is local to that Support instance and cannot exceed Max Charge.
- Charge above the cap is discarded, but the projectile is still accepted and forwarded.
- The aura is active only while Charge is above zero and at least one eligible target is in range.
- An active aura drains a fixed amount of Charge per second. The drain rate does not change with the number of affected targets.
- A Support Processor never buffs itself, another Support Processor, or the Soul Nexus.

### Base effect

Every eligible Element Processor and Special Pulse Processor inside the aura receives a small Process Interval reduction. The base aura does not change Generator Fire Interval and does not affect any Support Processor.

### Upgrade branches

- **Buff:** retains and strengthens the Processor-speed aura, with improved radius and efficiency.
- **Debuff:** completely removes the Processor-speed benefit and replaces it with an enemy aura that reduces both Physical Armor and Magic Resistance.

**OWNER-DECIDED:** A Base or Buff speed aura affects every eligible Element or Special Pulse Processor in its radius. Multiple speed auras stack through multiplicative percentage modifiers:

```text
EffectiveProcessInterval = BaseProcessInterval
                         * product(1 - AuraReduction[i])
```

For example, two `-10%` auras produce `Base * 0.9 * 0.9 = Base * 0.81`, not `Base * 0.80`. Apply the configured positive minimum interval after all multipliers.

The Debuff aura is a live radius effect rather than a timed lingering status. An enemy receives the Armor and Magic Resistance reduction only while it is inside the active aura. The effect is removed immediately when the enemy leaves the radius or the aura turns off. If multiple Debuff Support auras cover the same enemy, use only the strongest flat Armor reduction and the strongest flat Magic Resistance reduction; do not add their flat values.

## 5.4 Special Pulse Processor

**Verb:** Burst.

- On projectile arrival, gains 1 Pulse Charge, then enqueues and forwards the unchanged payload.
- When Pulse Charge reaches its threshold, it automatically creates one area pulse centered on the Special Processor and resets Pulse Charge to zero.
- A pulse is independent tower damage. It does not grant Soul and does not add enemies to any projectile's direct-hit set.
- Downstream backpressure can delay forwarding but does not cancel a pulse already triggered by a valid arrival.

### Upgrade branches

- **Rapid Pulse:** lower threshold, smaller radius, lower damage, more frequent pulses.
- **Impact Pulse:** higher threshold, larger radius, higher damage, less frequent pulses.

## 5.5 Soul Nexus

**Verb:** Terminate and Release.

- Is one unique core node, not a purchasable normal tower type.
- Is granted free once per level.
- Can be placed only on an Endpoint Pad.
- Cannot be sold.
- Accepts at most two linked inputs in V0.
- Instantly consumes arriving projectiles.
- Has no projectile queue, capacity, processing timer, reservation requirement, or output.
- Gains Soul equal to the arriving projectile's number of unique direct-hit enemy IDs.
- Still consumes projectiles while the Soul Meter is full.
- Discards overflow Soul and surfaces that loss as feedback.
- Allows targeting only when the Soul Meter is full.
- Resets the meter only after a valid skill placement is confirmed.

### Base Soul Skill

The player uses global targeting to select any ground point inside the authored map bounds. The cast is not limited by distance from the Soul Nexus. A temporary Soul Field slows enemies inside its radius. Canceling targeting preserves the full meter. A valid confirmation creates the field and resets the meter to zero.

An existing Soul Field does not prevent another cast. Multiple fields retain independent positions and durations and may coexist during a wave. When fields of the same type overlap, only the strongest magnitude applies to an affected target; percentages do not add. Different field types may apply their distinct effects at the same location. Every active Soul Field is deleted when the wave ends.

### Upgrade branches

- **Soul Suppression:** retains the slow and increases radius, duration, and slow strength.
- **Conduction Field:** removes the slow completely. Direct projectile hits whose world-space hit points are inside the field gain a damage multiplier and flat Physical and Magic Penetration.

Conduction Field modifies only the direct Physical and Magic packet. It does not buff reaction secondary effects, Support auras, Special pulses, or other Soul Skills. A projectile carries no Conduction Field tag after leaving the field; eligibility is evaluated again at each direct-hit point.

**OWNER-DECIDED AREA RULE:** Reaction area effects, Special Pulse, and Soul Fields determine affected targets only by their configured radius. Authored walls and terrain do not block these area effects and do not require a line-of-sight check.

# 6. Projectile Model and Lifecycle

## 6.1 Persistent runtime data

| Field | Meaning |
| --- | --- |
| `ProjectileId` | Stable unique identifier used for deterministic ordering. |
| `PhysicalDamage` | Original Physical component created by the Generator and preserved through Element Processors. |
| `MagicDamage` | Current Magic component rewritten by the latest Element resolution. |
| `Speed` | World-space travel speed. |
| `Radius` | Swept collision radius used by preview and runtime. |
| `BaseElement` | The element of the latest Element Processor that resolved the projectile. |
| `ReactionState` | Current terminal reaction or `None`. |
| `ReactionProcAvailable` | Whether the reaction's one secondary effect is still available. |
| `DirectHitEnemyIds` | Lifetime set preventing duplicate direct hits and determining Soul gain. |
| `SourceNodeId` | Node that emitted the current segment. |
| `TargetNodeId` | Receiver node of the current segment. |
| `ReservedSlotToken` | Ownership token for capacity at a normal receiver. |

The same logical projectile persists through the whole chain. A Processor temporarily stores it and later changes its source and target for the next segment. Its `ProjectileId` and `DirectHitEnemyIds` remain intact until the Soul Nexus consumes it or the level removes it.

## 6.2 Projectile invariants

- A projectile in flight has exactly one receiver target.
- It never targets an enemy.
- It never collides with another projectile.
- It pierces every eligible enemy collider it genuinely intersects.
- It direct-hits one enemy at most once across its entire chain lifetime.
- Every collider belonging to one enemy resolves to that enemy's single stable `EnemyId` before direct-hit deduplication. If one projectile intersects several colliders belonging to that enemy, the contacts produce only one direct hit, one damage-and-status resolution, and one ID in `DirectHitEnemyIds`.
- `DirectHitEnemyIds` belongs to one projectile. A different projectile may direct-hit the same enemy once through its own independent hit set.
- It is not destroyed by an enemy hit and continues toward its receiver.
- Reaction area effects and tower pulses do not add IDs to `DirectHitEnemyIds` and do not grant Soul.
- Multiple projectiles may travel from the same node at once if receiver reservations exist.

## 6.3 Lifecycle

1. An emitter becomes ready.
2. Validate that its chain and next receiver remain active.
3. If the receiver is a normal Processor, reserve one future slot.
4. A Generator creates a projectile, or a Processor dequeues its first projectile.
5. Spawn the projectile and reset the emitter's interval.
6. Move it through world space using swept collision.
7. For each newly intersected enemy, resolve one direct hit and any available reaction proc.
8. Continue to the receiver without being destroyed by enemy contact.
9. On normal Processor arrival, consume the reservation, apply node-specific arrival behavior, and enqueue the projectile.
10. On Soul Nexus arrival, grant Soul, record overflow, consume the projectile, and release any transient state.

# 7. Damage Contract

## 7.1 Two parallel damage channels

Generators create Physical Damage. Elements and reactions create Magic Damage. A single direct hit can apply both channels.

```text
PhysicalFinal = PhysicalDamage * PhysicalMitigation(EffectivePhysicalArmor)
MagicFinal    = MagicDamage    * MagicMitigation(EffectiveMagicResistance)
FinalDamage  = PhysicalFinal + MagicFinal
```

## 7.2 Defense formula

**V0 DEFAULT** for non-negative defense:

```text
MitigationMultiplier(Defense) = 100 / (100 + Defense)

EffectivePhysicalArmor = max(
    0,
    PhysicalArmor - ArmorReduction - PhysicalPenetration
)

EffectiveMagicResistance = max(
    0,
    MagicResistance - ResistanceReduction - MagicPenetration
)
```

Element and reaction Armor Reduction or Resistance Reduction may be timed enemy statuses according to their configuration. Support Debuff is a live aura modifier with no lingering duration. Conduction Field penetration exists only during the hit calculation and does not place a persistent status on the enemy.

## 7.3 Direct-hit resolution order inside Conduction Field

1. Read the projectile's direct Physical and Magic packet.
2. Test whether the world-space hit point is inside an active Conduction Field.
3. If it is, multiply both direct damage components by the field's Direct Damage Multiplier.
4. Subtract active Armor Reduction and Resistance Reduction.
5. Subtract flat Conduction Field Physical and Magic Penetration.
6. Clamp each effective defense to zero.
7. Apply each mitigation multiplier separately.
8. Add the two final components.
9. Apply base-element status and then any one-time reaction effect.

# 8. Elements and Reactions

## 8.1 Base element verbs

| Element | Primary verb | Direct-hit behavior |
| --- | --- | --- |
| Fire | Burn | Magic hit plus damage over time. |
| Ice | Slow | Magic hit plus movement slow. |
| Wind | Push | Magic hit plus backward path-progress displacement. |
| Earth | Break | Magic hit plus Physical Armor reduction. |

Wind changes authored path progress only. It never applies an unrestricted physics force and never pushes an enemy off its route.

## 8.2 Elemental ring

```text
Fire <-> Wind <-> Earth <-> Ice <-> Fire
```

- Two identical consecutive elements create a self reaction.
- Two adjacent elements in the ring create a mixed reaction.
- Fire plus Earth and Ice plus Wind are opposite pairs. They do not react; the receiver simply rewrites the projectile to its own element.
- Mixed reaction effect is symmetric, so Fire then Wind and Wind then Fire create the same reaction effect.
- The resulting `BaseElement` always becomes the receiver Element Processor's element, so downstream rewriting remains deterministic.

## 8.3 Rewrite rule

Physical Damage is preserved. Magic Damage never accumulates across Element Processors.

When a projectile arrives at Element Processor `E`:

1. If the incoming state is already a reaction, clear it and force a pure `E` state.
2. Otherwise, if the incoming pure element and `E` form a valid self or adjacent pair, create that reaction.
3. Otherwise, force a pure `E` state.
4. Set `BaseElement = E`.
5. Replace `MagicDamage` with the configured value for the resulting pure element or reaction.
6. Enqueue the projectile.

## 8.4 Terminal reaction rule

A reaction lasts only until the next Element Processor. It may produce its special effect once on the first valid downstream direct hit. If it reaches another Element Processor before proccing, it is discarded. The next Element Processor does not combine with an existing reaction to create a third-order reaction.

## 8.5 Reaction matrix

| Pair | Working name | One-time special effect |
| --- | --- | --- |
| Fire + Fire | Hellfire | Stronger Burn with longer duration. |
| Ice + Ice | Deep Freeze | Short Freeze. |
| Wind + Wind | Tempest | Stronger pushback. |
| Earth + Earth | Shatter | Stronger Physical Armor Break. |
| Fire + Wind | Firestorm | Area Burn around the proc target. |
| Wind + Earth | Sandstorm | Area Physical Armor Break. |
| Earth + Ice | Permafrost | Slow field at the hit point. |
| Ice + Fire | Steam Burst | Area Magic burst. |
| Fire + Earth | Receiver rewrite | No reaction. |
| Ice + Wind | Receiver rewrite | No reaction. |

All names except Hellfire are **V0 DEFAULT** working names.

## 8.6 One-proc rule

- A new reaction starts with `ReactionProcAvailable = true`.
- Its first valid downstream direct hit applies the normal direct Physical and Magic packet, the base-element status, and the configured reaction effect.
- The flag is then cleared.
- Later new enemies can still receive the projectile's direct Physical and Magic damage and current base-element status, but not the special reaction effect.
- Enemies touched only by a reaction area effect do not grant Soul.
- If the reaction is rewritten at the next Element Processor before a valid direct hit, its unused proc is lost.

## 8.7 Status stacking

- **Burn:** stronger Burn replaces weaker Burn; equal Burn refreshes duration. Burn magnitude does not add without limit.
- **Slow:** only the strongest magnitude affects movement; individual durations may still be tracked.
- **Freeze:** hard control overrides Slow while active. Boss duration is reduced.
- **Armor Break / Resistance Break:** strongest magnitude applies; equal magnitude refreshes duration.
- **Push:** immediate path-progress displacement with no stored stack.
- **Overlapping fields of the same type:** strongest magnitude applies rather than percentage addition.
- **CONTRACT RULE - OWNER DECIDED:** One reaction proc applies its secondary effect at most once to each enemy inside that proc's area, even if colliders overlap or the enemy has multiple colliders. The area may affect the primary direct-hit target and enemies that the projectile direct-hit earlier. A qualified proc resolves from its target snapshot even when the triggering direct hit kills the primary target.

# 9. Charge Systems

## 9.1 Support Internal Charge

```text
Projectile arrives
-> Charge = min(MaxCharge, Charge + 1)
-> Projectile is enqueued and forwarded normally
-> If Charge > 0 and at least one eligible aura target exists, aura is active
-> Active aura drains a fixed Charge amount per second, independent of target count
-> At Charge = 0, aura turns off
```

Charge depends on projectile arrival, not enemy hits or element state. This makes Support uptime a throughput decision the player can predict.

## 9.2 Special Pulse Charge

```text
Projectile arrives
-> PulseCharge += 1
-> If PulseCharge reaches threshold, create one pulse and reset to 0
-> Projectile is enqueued and forwarded normally
```

The base pulse deals Magic area damage around the Special Processor. Pulse damage is not a projectile direct hit and grants no Soul.

If several ordered arrivals occur in one frame, evaluate each arrival in stable order. Each threshold crossing creates one pulse; later arrivals in the same frame begin filling the next cycle.

## 9.3 Soul Charge

```text
Projectile arrives at Soul Nexus
-> SoulGained = count(DirectHitEnemyIds)
-> ActualGain = min(SoulGained, MaxSoul - CurrentSoul)
-> WastedSoul = SoulGained - ActualGain
-> Projectile is consumed even when the meter is full
```

- A reaction-only victim grants no Soul.
- Two different projectiles can each grant one Soul for directly hitting the same enemy.
- Soul overflow is lost.
- Full Soul never blocks either input.
- The skill can be cast only at full Soul.
- Canceling target selection preserves full Soul.
- Valid confirmation resets Soul to zero.

# 10. FIFO, Capacity, Reservation, and Backpressure

## 10.1 FIFO

Element, Support, and Special Processors emit projectiles in arrival order. They never prioritize a higher-damage projectile, rare element, unused reaction, or older visual effect over FIFO order.

When arrivals have the same timestamp, sort them by `ProjectileId` before enqueueing.

## 10.2 Effective capacity

```text
EffectiveOccupancy = QueuedProjectiles + ReservedIncoming

Emitter may send to a normal Processor only when:
EffectiveOccupancy < AmmoCapacity
```

A reservation is created before spawn. It prevents two upstream emitters from claiming the same final slot. On arrival, the reservation is converted into actual queue occupancy. On despawn, retry, invalidation, or failure to arrive, the reservation must be released exactly once.

## 10.3 Blocked-ready behavior

When the next Processor is full:

1. The upstream emitter enters `BLOCKED_READY` if its own interval is already complete.
2. Its ready timer remains complete instead of restarting every frame.
3. When a downstream slot becomes available, stable emitter ordering chooses who reserves it.
4. The chosen emitter fires immediately and then resets its interval.
5. Other blocked emitters remain ready and wait for a later slot.

## 10.4 Soul Nexus exception

The Soul Nexus consumes instantly, has unlimited arrival capacity, and requires no reservation. Its two input ports limit how many chains may be linked, not how many projectiles may be in flight.

## 10.5 Backpressure boundary

Only Processor throughput and capacity create backpressure. None of the following blocks an arriving or upstream projectile:

- full Soul;
- full Support Internal Charge;
- a completed Special Pulse threshold;
- a Soul Field already active;
- an enemy not being present on the segment.

# 11. Upgrade and Sell Contract

## 11.1 Level-local progression

- No meta progression is part of this prototype.
- Currency, construction, and upgrades reset after the level.
- Upgrades may be purchased only during Preparation.
- Each node instance chooses exactly one mutually exclusive branch.
- The branch remains locked on that instance until it is sold.
- Selling is Preparation-only and returns less than the total amount spent.
- The Soul Nexus cannot be sold.
- Once purchased, the Soul Nexus branch remains locked for the rest of the level.

## 11.2 Upgrade language

Every base node must be functional and readable before upgrading. A branch changes behavior or creates a tradeoff without breaking graph grammar.

| Node | Branch A | Branch B |
| --- | --- | --- |
| Generator | Rapid: volume and charge pressure. | Heavy: greater Physical value per projectile. |
| Element | Conduit: throughput and capacity. | Resonance: Magic and reaction potency. |
| Support | Buff: stronger Processor aura. | Debuff: replace Processor buff with enemy dual-defense reduction. |
| Special | Rapid Pulse: smaller, frequent pulse. | Impact Pulse: larger, slower pulse. |
| Soul Nexus | Soul Suppression: stronger slow field. | Conduction Field: direct damage and dual-penetration field. |

# 12. Economy V0

## 12.1 Economy goal

Starting Gold must buy one minimum valid chain but not every option. Each wave reward must create a choice between adding a node or second chain and upgrading existing infrastructure; it should not automatically fund both.

## 12.2 Starting values

All values in this section are **V0 DEFAULT** and must live in data configuration.

| Item | Build cost | Upgrade cost | Sell refund |
| --- | ---: | ---: | ---: |
| Generator | 90 | 80 | 70% of total spent. |
| Element Processor | 70 | 70 | 70% of total spent. |
| Support Processor | 90 | 90 | 70% of total spent. |
| Special Pulse Processor | 120 | 110 | 70% of total spent. |
| Soul Nexus | 0 | 120 | Cannot be sold. |

`StartingGold = 400`.

A minimum Generator plus two Element Processor chain costs 230, leaving 170 for one meaningful additional choice.

## 12.3 Income

- Kill income comes from each enemy's configuration.
- Each non-leaking enemy grants its configured kill reward exactly once when it transitions from alive to dead, regardless of whether the lethal source is a direct hit, Burn, Reaction AOE, or Special Pulse. Multiple lethal events resolved against the same enemy cannot grant duplicate rewards.
- An enemy removed as a leak is not a kill and grants no kill reward.
- Wave 1 clear reward is 100.
- Wave 2 clear reward is 130.
- No currency is generated outside a wave.
- No interest, passive income, or economy tower exists in this prototype.

# 13. V0 Balance Configuration

Every table in this section is a tunable starting point, not production balance.

## 13.1 Global

| Config | V0 value |
| --- | ---: |
| Maximum Link Range | 12 m |
| Projectile Speed | 10 m/s |
| Projectile Radius | 0.18 m |
| Build / Sell / Upgrade | Preparation only |
| Normal Node Capacity | 3 |
| Soul Nexus Input Ports | 2 |
| Sell Refund | 70% |

## 13.2 Node baseline

| Node | Interval | Capacity | Output or effect |
| --- | ---: | ---: | --- |
| Generator | 1.00 s | N/A | 8 Physical Damage. |
| Element | 0.85 s | 3 | 5 base Magic Damage plus status. |
| Support | 0.85 s | 3 | +1 Internal Charge per arrival. |
| Special | 0.85 s | 3 | +1 Pulse Charge per arrival. |
| Soul Nexus | Instant | Unlimited | Soul equals unique direct-hit count. |

## 13.3 Support baseline

| Config | Base | Buff branch | Debuff branch |
| --- | ---: | ---: | ---: |
| Radius | 4.0 m | 4.5 m | 4.0 m |
| Max Charge | 6 | 8 | 8 |
| Drain | 0.75/s | 0.75/s | 0.75/s |
| Effect | Processor interval -10% | Processor interval -25% | Enemy Armor and MR -8 |

Support never buffs any Support Processor. Process Interval has a configured positive minimum clamp to prevent zero or negative timing.

## 13.4 Special baseline

| Config | Base | Rapid Pulse | Impact Pulse |
| --- | ---: | ---: | ---: |
| Charge Threshold | 5 | 3 | 7 |
| Radius | 3.0 m | 2.5 m | 4.0 m |
| Magic Damage | 14 | 9 | 28 |

## 13.5 Soul baseline

| Config | Base | Soul Suppression | Conduction Field |
| --- | ---: | ---: | ---: |
| Max Soul | 50 | 50 | 50 |
| Field Radius | 3.5 m | 4.0 m | 3.5 m |
| Duration | 4.5 s | 6.0 s | 5.0 s |
| Slow | 35% | 50% | 0% |
| Direct Damage Multiplier | 1.0 | 1.0 | 1.30 |
| Physical / Magic Penetration | 0 / 0 | 0 / 0 | 5 / 5 |

## 13.6 Element baseline

| Element | Magic Damage | Status |
| --- | ---: | --- |
| Fire | 5 | Burn 2/s for 3 s. |
| Ice | 5 | Slow 25% for 2.5 s. |
| Wind | 5 | Push back 0.5 m of path progress. |
| Earth | 5 | Physical Armor -6 for 3 s. |

## 13.7 Reaction baseline

| Reaction | Direct Magic | One-time proc |
| --- | ---: | --- |
| Hellfire | 11 | Burn 4/s for 4 s. |
| Deep Freeze | 9 | Freeze for 0.8 s. |
| Tempest | 9 | Push back 1.5 m. |
| Shatter | 9 | Physical Armor -18 for 4 s. |
| Firestorm | 9 | Area Burn, 2 m radius, 2.5/s for 3 s. |
| Sandstorm | 8 | Area Physical Armor -10, 2.5 m radius, 4 s. |
| Permafrost | 8 | Slow field, 2.5 m radius, 35%, 4 s. |
| Steam Burst | 12 | Area 10 Magic Damage, 2 m radius. |

# 14. Enemy Contract V0

## 14.1 Enemy path

- Enemies move along a known authored path.
- Wind Push changes scalar path progress without removing the enemy from its path or NavMesh membership.
- Slow changes a movement-speed multiplier rather than base configuration.
- Enemy collider height and width must make Low and High trajectory hits or misses intentional and readable.

## 14.2 Archetypes

| Enemy | HP | Armor | MR | Speed | Test purpose |
| --- | ---: | ---: | ---: | ---: | --- |
| Swarm | 35 | 0 | 0 | 1.2 | Piercing and area effects. |
| Runner | 45 | 5 | 5 | 2.2 | Timing, Ice, and Wind. |
| Armored | 120 | 60 | 10 | 0.8 | Magic, Earth, and Support Debuff. |
| Warded | 100 | 10 | 60 | 0.9 | Physical and Heavy Generator. |
| Boss | 450 | 35 | 35 | 0.6 | Soul timing and mixed counterplay. |

The Boss receives 50% of normal Slow and Freeze duration and 50% of normal Push distance. It is not fully immune, so control feedback remains visible.

## 14.3 Waves

### Wave 1 - Read the line

Twelve Swarm and four Runners. Teach geometry, direct hits, piercing, and Soul gain.

### Wave 2 - Read defense

Six Armored, six Warded, and eight Swarm. Teach Physical versus Magic damage, Element order, and Support branch value.

### Wave 3 - Combine

One Boss, four Armored, four Warded, and six Runners with deliberate cadence gaps. Test reaction-proc placement, Special pulses, and Soul Skill timing.

`BaseHP = 10`. A normal leak removes 1 HP. A Boss leak removes 5 HP.

## 14.4 Leak commitment

When an enemy reaches or crosses the Base endpoint, mark its leak as committed for that simulation frame. A committed leak has priority over lethal damage resolved in the same frame:

- Apply the enemy's configured leak damage to Base HP.
- Remove the enemy as a leak rather than as a death.
- Grant no kill reward, even if direct damage, Burn, Reaction AOE, or Special Pulse also reduces that enemy to zero HP during the same frame.

This is the owner-selected **leak-first** rule. Damage that killed the enemy in an earlier frame prevents a later leak normally.

# 15. 3D Level and Camera

## 15.1 Geometry proof

- Use a fixed three-quarter camera orientation. Pan and zoom are allowed; player-controlled orbit and camera rotation are disabled on desktop and mobile.
- Normal build slots exist on Low and High elevation tiers.
- Low-to-Low links should reliably intersect suitable ground enemies.
- High-to-High links may travel over enemy colliders and miss.
- Diagonal links create different hit windows from horizontal links.
- A line that appears to cross an enemy on the screen is not sufficient. Runtime uses world-space collision.
- Trajectory preview must communicate world height, direction, radius, and expected path intersection rather than drawing only a flat screen overlay.

## 15.2 Endpoint placement

- The level contains two Endpoint Pads.
- The player chooses and places the free Soul Nexus before spending Gold.
- Because Soul Field targeting is global, Endpoint Pad choice must create its tradeoff through final firing-segment geometry and two-chain routing rather than skill access.
- If nearly every playtester chooses the same pad for the same reason, the endpoint-placement decision is not yet meaningful.

## 15.3 Anti-dominant-layout guardrails

- Maximum Link Range limits arbitrary long segments.
- Finite build slots limit unrestricted zigzag routes.
- Support and Special local areas reward useful coverage rather than only maximum line length.
- Mixed enemy speed and defense change which segment is valuable.
- Do not add an artificial line-length damage penalty until observation proves it is needed.

# 16. UX and Readability

## 16.1 Build flow

1. Tap an Endpoint Pad and place the free Soul Nexus.
2. Tap a normal build slot.
3. Inspect available node types, costs, and base statistics.
4. Select a node and confirm placement.
5. Press a source node again and begin a drag gesture.
6. Highlight only legal receiver nodes within the exact runtime range and line-of-sight rules.
7. Drag toward a receiver while previewing trajectory, direction, range, and chain validity.
8. Release on the highlighted receiver to confirm the directed link; release elsewhere to cancel without mutating the graph.
9. See the chain become active or read the exact invalid reason in the normal inspection UI.

Touch targets must be sized for small screens and must respect safe areas. Mouse input is an Editor fallback, not the primary interaction contract.

## 16.2 Inspect panel

Selecting a node must expose:

- node type, element, and selected branch;
- connected input and output targets;
- queued count, capacity, and reserved incoming count;
- Fire or Process countdown;
- active, blocked-ready, or inactive state with reason;
- current Internal Charge, Pulse Charge, or Soul;
- link range, aura radius, or field radius;
- the Physical and Magic packet of the first queued projectile;
- the first projectile's Base Element, reaction state, and proc availability.

## 16.3 Visual grammar

- Neutral, Fire, Ice, Wind, and Earth projectiles must differ by shape or trail as well as color.
- Reaction projectiles require a distinct silhouette or effect, not only higher brightness.
- Directional arrow flow on each link communicates source and receiver.
- Queue pips represent occupied slots.
- Empty pips with incoming markers represent reservations.
- A full Processor shows a stable warning, and the blocked upstream route highlights back to the bottleneck.
- Support shows a battery-like Charge state and whether its aura is active.
- Special shows a ring-like Pulse Charge state and threshold.
- The Soul Nexus shows a large Soul Meter, `SKILL READY`, and `SOUL LOST` feedback when full arrivals overflow.
- Soul Suppression and Conduction Field require clearly different shapes and effects because their gameplay functions differ.
- A projected miss caused by elevation must be visually explainable before the wave, not discovered as an apparent bug.

## 16.4 Action-based tutorial

Do not front-load a long tutorial. Teach one action at a time without instructional text, subtitles, or lesson-copy modals:

1. Place the Soul Nexus.
2. Complete one valid chain.
3. Preview one expected hit and one expected miss.
4. Observe one Element rewrite.
5. Create one reaction and predict its first proc target.
6. Choose one upgrade branch.
7. Cast the Soul Skill at full meter.

Each visual prompt uses only an animated hand, an action icon, a pulsing target highlight, and where necessary a moving trajectory preview. It disappears after the player completes the action. A compact visual Help recap may replay the same icon-and-animation sequence, but it must not introduce instructional prose.

# 17. Technical Architecture Guidance

This section preserves the source DOCX's suggested implementation boundaries. It is not proof that these classes or assets already exist.

## 17.1 Data-driven assets

- `TowerNodeConfig`: cost, interval, capacity, link range, local Charge, aura, and branches.
- `ElementConfig`: base Magic Damage, base status, and visual key.
- `ReactionConfig`: pair key, Magic Damage, proc behavior, radius, duration, and visual key.
- `EnemyConfig`: HP, defenses, speed, collider profile, control resistance, and reward.
- `WaveConfig`: spawn entries, cadence, and clear reward.
- `SoulSkillConfig`: threshold, field, and branch behavior.
- `LevelConfig`: build slots, Endpoint Pads, enemy path, starting Gold, and waves.

## 17.2 Suggested runtime responsibilities

- `NetworkGraphValidator`
- `NodeRuntime`
- `ProjectileRuntime`
- `ReceiverReservationService`
- `ElementResolver`
- `DamageResolver`
- `StatusEffectController`
- `ChargeController`
- `SoulCoreController`
- `WaveController`
- `EconomyController`
- `PrototypeDebugOverlay`

These names are responsibility labels, not a mandatory one-class-per-name architecture. Implementation should keep simulation rules deterministic and presentation replaceable.

## 17.3 Determinism

- Resolve every child collider to its owning enemy's stable `EnemyId` before hit deduplication or sorting. Enemy hits produced by one swept movement step then sort by travel distance and stable `EnemyId`.
- Same-time arrivals at one Processor sort by arrival timestamp and then `ProjectileId`.
- Ready emitters resolve in stable `NodeId` order.
- The Soul Nexus consumes every same-frame arrival, sums requested Soul, clamps once at the end of that resolution batch, and records the overflow count.
- Queue, proc target, damage order, and reservation decisions must never depend on unordered collection iteration.

## 17.4 Object lifecycle

- Pool projectile presentation and runtime objects where appropriate.
- Restart and despawn release every reservation token exactly once.
- A Preparation edit atomically clears in-flight projectiles, queues, and reservations for every affected chain while preserving Internal Charge and Pulse Charge on surviving nodes and preserving shared Soul. Unaffected chains remain frozen and unchanged.
- Selling a node removes that node's links, timers, branch state, and local Charge. A rebuilt or replacement node inherits no state from the sold instance.
- Retry and level load retain no static runtime state or stale event subscription.
- Pooled projectiles reset IDs, target data, payload, reaction flags, hit history, and reservation references before reuse.

# 18. Resolution Order

## 18.1 High-level simulation order

1. Update wave state and enemy path progress.
2. Update timed statuses, Support auras, Soul Fields, and Charge drain.
3. Update node Fire and Process timers.
4. Resolve ready emitters in stable `NodeId` order.
5. Reserve normal receiver capacity before spawning.
6. Spawn or dequeue projectiles and reset successful emitter timers.
7. Move projectiles with swept collision to reduce tunneling.
8. Sort and resolve new direct enemy hits by travel distance and stable `EnemyId`.
9. Evaluate Conduction Field eligibility at each hit point.
10. Resolve Physical and Magic damage separately.
11. Apply the current Base Element status.
12. If `ReactionProcAvailable`, apply the one-time special effect and clear the flag.
13. Add the direct enemy ID to `DirectHitEnemyIds`.
14. Resolve projectile arrival.
15. At a normal Processor, convert reservation to occupancy, apply node-specific arrival behavior, and enqueue.
16. At the Soul Nexus, sum Soul, clamp, record overflow, and consume.
17. Resolve automatic Special Pulse triggers created by ordered arrivals.
18. Commit and resolve leaks first. Then resolve remaining deaths, exactly-once kill rewards, and wave completion.

Damage and reaction effects use an event snapshot so a valid reaction proc still resolves if the primary direct hit kills its target. A destroyed target must not receive a duplicate application from the same proc.

Kill reward ownership is attached to a non-leaking enemy's single alive-to-dead transition, not to a damage-source category. Direct damage, Burn, Reaction AOE, and Special Pulse therefore share the same exactly-once reward path. If an enemy crosses the Base and receives lethal damage in the same simulation frame, leak commitment wins: Base HP is reduced, the enemy is removed as a leak, and no kill reward is granted.

## 18.2 Soul Skill cast order

1. Enter targeting only if the Soul Meter is full.
2. Canceling preserves the meter and creates no field.
3. A valid confirmation creates the branch-configured field.
4. Reset the Soul Meter to zero.
5. Projectile arrivals resolved later in the same frame charge the new meter.

# 19. Acceptance Criteria

## 19.1 Graph

- A normal node cannot exceed one input or one output.
- The Soul Nexus accepts at most two chains.
- Cycles are rejected.
- A chain without the Soul Nexus or with fewer than two Processors is inactive.
- Every inactive state has a visible reason.
- Links can be edited only during Preparation.
- Start Wave is disabled until the Soul Nexus is placed and at least one active chain exists.
- Editing one chain during Preparation clears that chain's in-flight projectiles, queues, and reservations while preserving Charge on surviving nodes and shared Soul. An unaffected chain remains unchanged.

## 19.2 Projectile and 3D geometry

- A valid Generator emits by interval even when no enemy is present.
- Every in-flight projectile targets only its receiver node.
- Multiple projectiles may coexist in flight.
- Projectiles pierce enemies and ignore other projectiles.
- One projectile direct-hits one enemy at most once during its lifetime.
- Low and High routes create real, reproducible hits and misses.
- Runtime trajectory and preview use the same geometry.
- All child colliders of one enemy resolve to one stable `EnemyId`. One projectile can direct-hit that enemy only once, while a different projectile has its own independent hit allowance.

## 19.3 Queue and reservation

- FIFO matches deterministic arrival order.
- Effective occupancy never exceeds capacity.
- Every in-flight projectile targeting a normal Processor owns one valid reservation.
- Multiple sources cannot reserve the same final slot.
- A full receiver blocks upstream and automatically resumes it when a slot opens.
- Full Soul never blocks a Soul Nexus input.

## 19.4 Damage

- Physical and Magic channels resolve separately through Armor and MR.
- Element Processors never change Physical Damage.
- Magic Damage is replaced rather than accumulated.
- Conduction Field modifies only direct hits inside the field.
- Flat penetration never reduces effective defense below zero.
- Reaction AOE, Special Pulse, and Soul Fields use radius only and ignore walls and terrain.
- Every non-leaking enemy death grants exactly one configured kill reward, regardless of lethal damage source.
- A same-frame Base crossing has priority over lethal damage: apply leak damage and grant no kill reward.

## 19.5 Element and reaction

- The elemental ring produces correct self, mixed, and rewrite results.
- Mixed reaction effect is symmetric and output Base Element equals the receiver's element.
- A reaction is cleared by the next Element Processor.
- A reaction procs exactly once on the first valid downstream direct hit.
- An unused proc is lost when the reaction is cleared.
- A reaction-only victim grants no Soul.

## 19.6 Support and Special

- Support gains Charge on arrival and still forwards the projectile.
- Base and Buff Support affect Element and Special Pulse Processors only; they never affect any Support Processor.
- Debuff branch completely removes the Processor-speed buff.
- Charge drains only while at least one eligible target makes the aura active.
- Active Support drain is fixed per second and does not scale with the number of targets.
- Overlapping Debuff Support auras use the strongest flat Armor and MR reductions rather than adding them.
- Special auto-pulses at the configured threshold and still forwards the projectile.
- Pulse damage grants no Soul.

## 19.7 Soul Nexus

- It is unique, free, and cannot be sold.
- It instantly consumes simultaneous arrivals from both inputs.
- Soul equals each consumed projectile's unique direct-hit count.
- Overflow is lost, but the projectile is still consumed.
- The skill cannot be cast below full meter.
- Canceling targeting preserves Soul.
- Valid confirmation resets Soul to zero.
- Every ground point inside the authored map bounds is a valid global cast location.
- Active Soul Fields are deleted when the wave ends.
- Soul Suppression slows; Conduction Field replaces slow with direct damage and penetration.

## 19.8 Economy and restart

- Upgrades are between waves and branches are mutually exclusive.
- Sell refund is calculated from total build and upgrade spend.
- Retry resets Gold, nodes, branches, queues, reservations, projectiles, statuses, all Charge, Soul, and fields.
- Win, fail, and retry leave no stale subscription or pooled state.

# 20. Edge-Case Contract

| Scenario | Expected V0 behavior |
| --- | --- |
| Two emitters try to reserve the final receiver slot in one frame. | Stable `NodeId` order grants one reservation. The other emitter remains `BLOCKED_READY`. Capacity is never exceeded. |
| A projectile overlaps one enemy across several physics ticks. | The first valid sweep adds the enemy ID. Later overlaps by the same projectile cause no second direct hit. |
| One projectile intersects the body, head, and shield colliders of the same enemy. | Resolve all three colliders to one stable `EnemyId`; apply one direct hit and add one ID. A different projectile may later direct-hit that enemy through its own hit set. |
| A reaction projectile crosses an enemy already in its direct-hit set, then a new enemy. | The old enemy is ignored. The new enemy receives the direct packet and consumes the one available reaction proc. |
| An unprocced reaction reaches another Element Processor. | The old reaction and its proc opportunity are discarded before resolving the new pure element. |
| Reaction area overlaps the primary target and an enemy hit earlier by the projectile. | The proc applies its secondary effect once per eligible enemy in that proc snapshot. It does not modify direct-hit history or Soul. |
| Soul is 49/50 and same-frame projectiles request 4 and 6 Soul. | Sum requested gain as 10, finish at 50, and record 9 Wasted Soul. Both projectiles are consumed. |
| A full Soul Nexus receives several same-frame projectiles. | Consume all of them, grant 0 actual Soul, and record all requested Soul as wasted. |
| The player confirms a Soul Skill in the same frame as projectile arrival. | Valid cast creates the field and resets to zero; arrivals later in the defined frame order charge the new meter. |
| Support is already at Max Charge when a projectile arrives. | Discard excess local Charge, but accept, queue, and forward the projectile. |
| Debuff Support reaches zero Charge while enemies remain in radius. | Remove that aura's live Armor and MR reduction immediately. No lingering timed debuff remains. |
| Special reaches pulse threshold while its downstream receiver is blocked. | Trigger the pulse from the valid arrival. The projectile remains queued until downstream capacity opens. |
| A sold tower is rebuilt with a different branch. | Refund the sold instance, remove its state and links, and create a clean new instance with no inherited branch or Charge. |
| The player relinks one preserved chain during Preparation. | Despawn that chain's in-flight projectiles, clear its queues, release its reservations exactly once, and preserve Charge on surviving nodes plus shared Soul. Leave an unaffected second chain unchanged. |
| Soul Nexus repositioning invalidates both chains. | Treat both chains as affected and clear their transport state while preserving surviving-node Charge and shared Soul. Revalidate retained links, delete each invalid link, mark both chains inactive, and show exact reasons. Do not auto-repair topology. |
| A High projectile appears to cross a lane on screen but misses the collider in world space. | It misses. Preview and height cues must make the reason understandable before combat. |
| Wind Push occurs near the beginning or end of the path. | Apply reduced Boss distance where relevant and clamp path progress to the authored path bounds. |
| Multiple Slow, Armor Break, or same-type fields overlap. | Use strongest magnitude rules and deterministic duration tracking; do not add percentages without limit. |
| Retry occurs while a reaction, pulse, and Soul Field are active. | Cancel all active effects, clear event queues, release reservations, reset pools, and restore initial level data. |
| A direct hit kills the primary target while its reaction proc is pending. | Resolve the already-qualified reaction event from its snapshot, then resolve deaths without double-applying to the destroyed target. |
| An enemy crosses the Base and also reaches zero HP in the same simulation frame. | Leak commitment wins. Apply the configured Base damage, remove the enemy as a leak, and grant no kill reward. |

# Worked Gameplay Examples

## Example 1 - A minimum chain

```text
Generator -> Fire -> Ice -> Soul Nexus
```

1. The Generator creates a neutral projectile with 8 Physical Damage.
2. The first segment can direct-hit enemies before the Fire Processor.
3. Fire arrival preserves 8 Physical, rewrites Magic to Fire, and enqueues.
4. The second segment deals the current Physical plus Fire Magic and can apply Burn.
5. Ice arrival sees pure Fire followed by adjacent Ice, creating Steam Burst and rewriting Magic to the reaction value.
6. The final segment can direct-hit a new enemy, apply direct Physical and Magic damage, and trigger Steam Burst once.
7. The Soul Nexus consumes the projectile and grants one Soul for every unique enemy directly hit across all three segments.

This example shows why node order and geometry are inseparable: changing Fire and Ice order preserves the same mixed reaction effect but changes the Base Element leaving the second Processor and therefore the base status applied on the final segment.

## Example 2 - A bottleneck

```text
Rapid Generator -> Conduit Fire -> Resonance Earth -> Soul Nexus
```

The Rapid Generator and Conduit Fire may produce projectiles faster than Resonance Earth can process them. Earth reaches `Queued + Reserved = Capacity`. Fire becomes `BLOCKED_READY`, and the Generator eventually becomes blocked by Fire's own filling capacity. The player can diagnose the slowdown from queue pips and upstream highlights.

Possible fixes include changing Earth to Conduit, choosing Heavy Generator, replacing the node order, or using Buff Support within range. Adding damage to an already-full queue does not solve throughput.

## Example 3 - Two chains and shared Soul

```text
Generator A -> Fire -> Support -> Soul Nexus input 1
Generator B -> Earth -> Special -> Soul Nexus input 2
```

The chains have independent queues and backpressure but share one Soul Meter. A projectile from either chain grants Soul based on its own direct-hit history. A full meter blocks neither chain. The player may hold the skill for Wave 3, but every later arrival wastes potential Soul until the skill is cast.

## Example 4 - Soul Suppression versus Conduction Field

- Use **Soul Suppression** when keeping Runners or the Boss inside valuable firing segments longer is worth more than burst damage.
- Use **Conduction Field** when a dense direct-hit window crosses high-defense enemies and the player can place the field over those hit points.
- Conduction Field does not improve Special Pulse or reaction area damage, so placing it over a Special Processor without direct projectile hits has no benefit.

# 21. Playtest Plan

## 21.1 Test sequence

### Test A - Geometry comprehension

Ask the player to predict which enemies a Low or High link will hit before starting the wave.

### Test B - Graph comprehension

Leave one chain incomplete and observe whether the player can repair it from immediate feedback.

### Test C - Element order

Provide all four elements and ask the player to create one self reaction, one mixed reaction, and one opposite-pair rewrite.

### Test D - Reaction proc

Ask which enemy will consume the reaction's one proc and why.

### Test E - Throughput

Create a low-capacity bottleneck and observe whether the player changes the correct node rather than only buying damage.

### Test F - Upgrade and Soul timing

Ask the player to choose Support and Soul Nexus branches and explain the goal. Observe whether they understand Soul overflow and make a deliberate cast-timing choice.

## 21.2 Success signals

- After onboarding, the player predicts most intended hits and misses correctly.
- The player explains why a chain is inactive or a Processor is blocked.
- The player changes element order because of effect and segment geometry, not only a larger number.
- The player understands that a reaction has one proc and deliberately aims its first downstream hit.
- The player creates at least two defensible layouts for different waves.
- Upgrade branches change actions or placement and neither branch always wins.
- The player understands that full Soul does not block the network but can waste future charge.

## 21.3 Failure signals

- The player always draws the longest zigzag without considering anything else.
- Element order feels like random visual effects.
- Queue occupancy and reservations are unreadable.
- Support, Special, and Soul Nexus are mistaken for interchangeable endpoints.
- Soul Skill becomes a button tax that is always pressed immediately.
- One damage channel or one reaction solves every enemy.
- Elevation causes misses that players interpret as collision bugs.

## 21.4 Decision gate

- **CONTINUE:** geometry, element order, and throughput all cause deliberate adjustments.
- **ENHANCE:** the mechanic is understandable, but feedback, branches, or reactions do not create meaningful choices.
- **PIVOT:** players understand the rules, but optimal play collapses into longest-chain or node-spam behavior.
- **STOP:** players cannot predict hits and misses or do not connect outcomes to placement.

# 22. Implementation Milestones

These milestones are planning content from the source, not work already completed.

## Milestone 0 - Data and shell

Unity project, Android smoke build, configuration schemas, fixed camera, and clean level restart.

## Milestone 1 - Graph and geometry

Build slots, Endpoint Pads, Soul Nexus placement, link validation, trajectory preview, one Generator, one Processor, projectile movement, and lifetime direct-hit set.

## Milestone 2 - Queue and backpressure

FIFO, capacity, reservation, blocked-ready resume, and readable queue pips.

## Milestone 3 - Damage channels

Physical and Magic packet, Armor and MR formula, and five enemy configurations.

## Milestone 4 - Four elements

Elemental ring, rewrite behavior, terminal reactions, one-proc rule, and status stacking.

## Milestone 5 - Charge nodes and Soul

Support, Special, multi-input Soul Nexus, Soul overflow, and targetable Soul Field.

## Milestone 6 - Economy and upgrades

Build costs, rewards, sell refund, mutually exclusive branches, and Preparation lock.

## Milestone 7 - Vertical slice

Three waves, HUD, action tutorial, result/retry, and debug overlay.

## Milestone 8 - Evidence

Android profiling, acceptance regression suite, human playtest, and a documented `CONTINUE`, `ENHANCE`, `PIVOT`, or `STOP` decision.

# 23. Open Decisions

## 23.1 Source open items that do not block the first prototype

- Final production English names for elements, reactions, and upgrades; all current labels are working names.
- Final production animation and audio assets. The prototype palette and material direction are already constrained by the approved stylized soul-powered dark-fantasy theme.
- Exact balance after human playtesting, including Rapid/Heavy and Conduit/Resonance values, Support clamps, and per-enemy kill rewards.
- Whether a production Soul Nexus may have more than two input ports.
- Final ten-level campaign, second map, and authored beat chart.
- Whether final compliance requires a base node plus two sequential paid upgrade levels, rather than the prototype's one mutually exclusive branch choice.

## 23.2 Owner review status

No unresolved implementation-rule questions remain from this clarification pass. Preparation mutation cleanup, multi-collider ownership, same-frame leak priority, kill rewards, global Soul Field placement, Support stacking, AOE wall behavior, the Start Wave gate, title, theme, language, tutorial grammar, link gesture, procedural art scope, and camera controls are all owner-decided. The campaign, final production-asset, production-compliance, and balance items in Section 23.1 remain intentionally open or data-driven.

Developers may tune V0 DEFAULT values in configuration to create test scenarios. They must not silently change graph grammar, resolution order, decision labels, or ownership boundaries.

# 24. Developer Handoff Summary

Build a greybox 3D tower-defense prototype in which primary damage comes from projectiles traveling between connected nodes. Normal nodes allow at most one input and one output. One unique Soul Nexus accepts at most two chains and instantly consumes projectiles. An incomplete chain never emits. A Generator creates fixed Physical Damage; four Element Processors rewrite Magic Damage through an elemental ring; a terminal reaction produces its secondary effect once on the first valid downstream direct hit. Projectiles pierce enemies, directly hit each enemy at most once over their full chain lifetime, and grant Soul only from those unique direct hits. Processors use FIFO queues, finite capacity, and incoming reservations to create readable backpressure. Support and Special nodes gain local Charge on arrival while still forwarding projectiles. Full Soul never blocks arrival; overflow is lost; the Soul Skill requires a full meter and resets it after valid confirmation. The prototype includes level-local branches, separate Physical and Magic defenses, two elevation tiers, three waves, five enemy archetypes, readable mobile UI and debug state, deterministic ordering, clean restart, and data-driven balance.

## Current recommendation

**APPROVED - CONTINUE TO PROTOTYPE.** The primary risk is readability. The secondary risk is a dominant longest-zigzag strategy. Build the approved V0 vertical slice, then use human playtesting to evaluate those two risks before expanding content volume.
