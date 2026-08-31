# Arcane Arsenal

## Raw Game Concept

**Status:** Draft  
**Document type:** Raw concept, not an implementation or balance specification  
**Genre:** 3D elemental projectile-routing tower defense  
**Primary platform:** Android landscape, touch-first  
**Visual direction:** Colorful stylized fantasy  
**Camera:** Isometric 3D with touch pan, zoom, and rotation

## Concept Boundary

This is a new game direction. It does not use the Toad, Yin-Yang, Karma, Water economy, characters, or world from `RawConcept_1.md`.

Rules explicitly confirmed for this concept are treated as its foundation. Tower names, elemental reactions, enemy examples, upgrade branches, and numerical values are working designs that may change after prototyping.

## High Concept

The player is an **Arcane Architect** defending an **Arcane Nexus** from creatures emerging through magical rifts.

Towers do not behave as independent weapons. Ammunition-producing towers create physical arcane rounds and fire them along player-controlled angles. When a real projectile trajectory crosses an elemental tower, that tower catches the round, transforms it with Fire, Ice, Wind, or Earth, then fires it again along its own angle. Every aimed route is also a live attack line: a projectile can pierce enemies before entering the first compatible support tower it physically strikes.

The player wins by designing a network whose geometry, height, throughput, storage, elements, and firing directions all match the current enemy wave.

The central fantasy is:

> **Build a magical ammunition circuit, then aim every physical route in that circuit as a weapon.**

## Core Game Promise

Arcane Arsenal is a formation puzzle expressed through real-time tower defense.

- Placement determines where towers can stand.
- Continuous aim determines where ammunition travels and which tower catches it.
- Buffers determine when the network flows or jams.
- Elements determine which effects and reactions each round can produce.
- Terrain and height determine which networks can reach which enemies.
- Rotation determines every relay route and the final discharge direction.
- Re-aiming allows the player to adapt while a wave is already moving.

The strongest formation is not automatically the one with the most towers. It is the one whose routes repeatedly cross valuable enemy lanes without exceeding the network's storage and output capacity.

## Design Pillars

### 1. The network is the weapon

An aimed route is not only resource transport. Every projectile physically travels along it, can strike several overlapping or side-by-side enemies, and enters a support tower only if the projectile actually intersects it.

### 2. Geometry creates damage

Tower position, shot range, rotation angle, walls, terrain, firing height, and final output direction shape every attack line. A short efficient network processes ammunition reliably; a longer route may cross more enemies but demands more precise placement and line of sight.

### 3. Throughput is as important as damage

Every ammunition-handling tower has finite storage. Multiple producers may feed one elemental tower, but that tower still releases rounds sequentially. A powerful network can therefore fail because its buffers fill faster than its outputs discharge.

### 4. Elemental order creates tactical choices

Fire, Ice, Wind, and Earth change both the projectile and the enemies it hits. Pre-composed elemental ammunition has its own payload effects, while elements applied across separate hits can cause field reactions on enemies.

### 5. Re-aiming is active strategy

Building remains available while waves are running. The player may rotate any ammunition tower, break an aimed loop, redirect an output, move a tower for a fee, or change which lane receives the most ammunition.

### 6. Complexity must remain readable on mobile

The player must be able to read predicted interceptions, firing layer, buffer pressure, stored elements, invalid line of sight, output direction, and enemy reactions from the battlefield without opening several menus.

## Theme and World

Arcane Arsenal takes place in a colorful fantasy world where artificers learned to load spellcraft into physical ammunition.

The **Arcane Nexus** stabilizes the surrounding land and seals the rifts beneath it. Rift creatures follow fixed invasion routes toward the Nexus. They do not attack towers; their only objective is to reach the destination and consume Nexus lives.

The player acts as an Arcane Architect rather than a conventional commander. Their power comes from arranging magical machinery into a working circuit:

- Foundries create neutral ammunition.
- Infusers and conduits add elemental signatures.
- Amplifiers improve nearby machinery.
- A terminal arcane weapon converts stored ammunition into a directional spell.

The art direction should favor bold silhouettes, saturated elemental colors, readable rune patterns, and clear projectile trails over realistic military machinery.

## Stage Objective and Failure State

Each stage contains a fixed set of enemy paths, buildable grid cells, terrain, walls, and authored height levels.

- The player wins by defeating every wave in the stage.
- The Arcane Nexus has a limited number of lives.
- An enemy reaching the Nexus removes one or more lives according to its threat value.
- The player loses when the Nexus reaches zero lives.
- Enemies never damage, disable, or destroy towers or ballistic routes.

## Core Battle Loop

1. Inspect the stage paths, obstacles, available firing layers, enemy preview, and starting money.
2. Place one or more Ammunition Foundries on buildable grid cells.
3. Place elemental ammunition-support towers where rotated projectile trajectories can physically intersect them.
4. Rotate each relay output and aim the final elemental output across an enemy route, or aim ammunition into a Special tower.
5. Start or continue the wave while the network produces, stores, transforms, and releases ammunition.
6. Watch buffer pressure, elemental states, reactions, blocked trajectories, and wasted shots.
7. Spend money from defeated enemies to build, upgrade, move, sell, or redirect towers during the wave.
8. Adapt the network to new enemy defenses, route density, and firing layers.
9. Clear the final wave while preserving at least one Nexus life.

## Battlefield Structure

### Grid and placement

- Towers are placed freely on valid grid cells rather than fixed tower pads.
- Enemy paths are fixed and cannot be occupied or blocked by towers.
- Towers may use footprints larger than one cell.
- Placement must validate the tower's entire footprint.
- Walls and terrain can block projectile routes.
- Tower placement, selling, moving, upgrading, linking, and rotation are available while a wave is running.

### Enemy path density

Enemies are not required to travel in a single-file line.

- A route may have enough width for enemies to move side by side.
- Enemies may visually or physically overlap during dense waves.
- Several routes may pass through the same visible combat space.
- A correctly aligned projectile can therefore hit many enemies during one flight segment.

This density is important to the game's value proposition: the player is rewarded for routing projectiles through contested space rather than only aiming at the first enemy in a lane.

### Three firing layers

The initial game supports exactly three discrete firing layers:

| Layer | Working battlefield role |
|---:|---|
| **0** | Ground paths and low ground-level networks. |
| **1** | Raised terrain, bridges, and low-flying enemies. |
| **2** | High platforms and high-flying enemies. |

Confirmed initial restrictions:

- Every ammunition-handling tower in one connected chain must operate on the same layer.
- A projectile cannot transfer from a lower layer to a higher layer or the reverse.
- A tower cannot hit an enemy on another layer.
- Flying enemies are assigned to a specific flight layer rather than one universal Air category.
- Cross-layer relays and cross-layer targeting are deferred future possibilities, not initial rules.

### Camera and navigation

The battlefield uses an isometric 3D camera. Touch controls support:

- one-finger pan when the gesture does not begin on a tower or UI control;
- pinch zoom;
- a deliberate two-finger rotation gesture;
- automatic focus on a selected tower or blocked ballistic route when useful;
- safe-area-aware controls suitable for Android landscape screens.

## Projectile Network Rules

### Physical aim routing

There is no manual Link command and no stored source-to-destination connection. The player selects an ammunition-emitting tower and rotates its output continuously.

- Every emitted round travels along the tower's current straight aim direction.
- If that physical trajectory intersects a compatible receiving tower, the round enters its buffer.
- If the trajectory does not intersect a receiver, the round continues freely and may hit enemies until its shot range ends.
- A receiver must be on the same firing layer and inside the source tower's shot range.
- Walls and terrain stop the projectile before any receiver behind them.
- When several compatible receivers overlap one trajectory, the first one struck receives the round.
- A readable route line appears only when the current angle predicts a real tower interception; it is feedback, not a persistent link.

### Ports and routing

An ammunition-handling tower has:

- one logical input buffer that can accept rounds from several incoming trajectories;
- one rotatable output direction;
- a finite ammunition capacity;
- an output cadence that releases stored rounds sequentially.

Several Foundries may therefore aim through the same elemental tower. Their intercepted rounds merge into one first-in, first-out queue. One shot travels along one direction and is caught by only the first compatible tower, so an output cannot split into several branches.

There is no separate global or per-route cap on simultaneous projectiles. An output does not wait for its previous round to arrive before launching the next one. The practical limit emerges from production cadence, output cadence, buffer capacity, and projectile travel time. For example, two Foundries aimed through one Ember Infuser can produce two Fire rounds that leave the Infuser sequentially and remain in flight at the same time.

The Tower Support category does not carry ammunition and therefore does not use these ammunition ports. The Special tower accepts ammunition but replaces a normal ammunition output with its skill.

### Projectile travel and collision

- Projectiles physically travel along straight routes.
- A projectile may hit any number of enemies during one segment.
- Each projectile can damage a particular enemy at most once during that segment.
- Hitting an enemy does not consume the projectile.
- Enemy hits do not interrupt tower interception: after damaging enemies earlier on its route, the round can still enter the first compatible tower it strikes.
- Enemies may be side by side or overlap, so one projectile can affect a dense group.
- A projectile only collides with enemies on its own firing layer.
- Terrain and walls stop the projectile and invalidate or interrupt the route.

### Aimed output, interception, and intentional waste

Every ammunition-emitting tower fires its stored ammunition continuously along the direction chosen by the player. This includes the Foundry and every elemental ammunition-support tower.

- It does not wait for an enemy to enter the line.
- A round that misses every enemy is lost.
- The firing direction therefore creates both opportunity and risk.
- Rotating an output at the wrong moment can empty a valuable buffer into unused space.
- When a round enters an elemental tower, it receives that element once, enters the finite buffer, and is later re-fired along the receiving tower's own angle.
- Repeated passage through the same elemental type does not duplicate its elemental signature.

### Buffer pressure and backpressure

- A full tower cannot accept another round.
- A source whose current predicted trajectory is aimed into a full receiver holds its next round, creating readable backpressure. A round already in flight may still be lost if another source fills the final slot first.
- If the source also becomes full, pressure propagates backward through the network.
- A full Foundry stops producing until it has storage again.
- Multiple producers can increase total supply, but they do not bypass a downstream tower's sequential output rate.

### Intersections

Two projectile routes may cross without interacting.

- Projectiles do not collide, merge, or block each other at an intersection.
- Each projectile preserves its own elemental state and physical trajectory.
- Enemies located at the intersection may be struck by projectiles from both routes and can receive elemental reactions from those separate hits.

### Re-aiming

The player may rotate any ammunition output during a wave.

- Re-aiming does not cost money.
- Rotation is smooth while the player holds Left or Right; it is not snapped between preset directions.
- Stored ammunition remains in each tower.
- Existing in-flight rounds preserve the direction they had when fired.
- A moved or sold tower immediately changes which future trajectories can intercept it, but existing rounds continue naturally.
- A route blocked by terrain visibly ends at the obstruction. Rotating away from a receiver immediately turns later shots into free-flight ammunition.

### Loops

Cycles are legal. For example, a Wind tower may aim through an Earth tower whose output points back through the Wind tower.

A loop can repeatedly pass rounds across attack lines while free buffer slots remain. It is not a permanent engine:

- producers can continue filling remaining loop capacity;
- once every receiver in the loop is full, predicted routes stop emitting through backpressure;
- two rounds racing for the last free slot can cause the later in-flight round to dissipate at the full receiver;
- repeated elemental towers do not add duplicate elemental signatures;
- the player releases stored rounds by rotating one output away from the loop and toward enemies or a new receiver;
- the time spent physically rotating creates the vulnerable transition before the loop can discharge.

Loops are intended as risky temporary storage and repeated-route setups, not a source of unlimited capacity.

## Initial Tower Categories

| Category | Purpose | Initial roster |
|---|---|---|
| **Ammunition Producer** | Creates neutral arcane rounds over time. | Ammunition Foundry |
| **Ammunition Support** | Adds an element and forwards or fires ammunition. | Ember Infuser, Frost Prism, Gale Conduit, Terra Forge |
| **Tower Support** | Improves nearby tower performance without carrying ammunition. | Arcane Amplifier |
| **Special** | Stores processed rounds and automatically converts them into a directional skill. | Nexus Lance |

The initial concept therefore contains seven tower types: one Producer, four elemental Ammunition Support towers, one Tower Support tower, and one Special tower.

## Working Tower Roster

The following names and upgrade branches are working designs. Exact costs, rates, ranges, capacities, damage values, and percentages remain tuning variables.

### Ammunition Foundry — Producer

**Base role**

- Creates neutral arcane rounds at a fixed production interval.
- Stores created rounds in its own buffer.
- Fires rounds sequentially along its current rotation angle.
- Stops production when its buffer is full; it also holds output when the predicted receiving tower is full.

**Upgrade directions**

- **Expanded Magazine:** increases buffer capacity and shot range.
- **Rapid Foundry:** reduces production interval and output cooldown.
- **Twin Casting:** creates two rounds per production cycle, increasing downstream pressure and rewarding a high-throughput network.
- **Dense Core:** increases the neutral damage carried by every produced round.

### Ember Infuser — Fire Ammunition Support

**Base role**

- Adds Fire to an incoming round if Fire is not already present.
- Increases direct damage and applies Burning on hit.
- Processes stored rounds sequentially.

**Upgrade directions**

- **Hot Chamber:** increases processing and output speed.
- **Intense Burn:** improves Burning damage and duration.
- **Blast Furnace:** increases the area of Fire fusion payloads.
- **Long Feed:** increases shot range and buffer capacity.

### Frost Prism — Ice Ammunition Support

**Base role**

- Adds Ice to an incoming round if Ice is not already present.
- Applies Chill, reducing enemy movement speed.
- Repeated Ice hits on an enemy build toward a short Freeze, subject to resistance.

**Upgrade directions**

- **Deep Chill:** improves slow strength and Freeze build-up.
- **Crystal Focus:** improves damage against shields and armored targets.
- **Cold Relay:** increases processing speed and shot range.
- **Stable Lattice:** increases capacity so the tower can accept several upstream streams.

### Gale Conduit — Wind Ammunition Support

**Base role**

- Adds Wind to an incoming round if Wind is not already present.
- Improves projectile travel speed.
- Applies a Gale Mark that improves Wind reaction spread and permits a small backward displacement on susceptible enemies.

**Upgrade directions**

- **Jetstream:** increases projectile speed, output cadence, and shot range.
- **Cyclone Wake:** widens Wind fusion effects around the travel line.
- **Force Pulse:** improves reaction displacement against non-heavy enemies.
- **Flow Chamber:** increases buffer capacity and reduces congestion.

### Terra Forge — Earth Ammunition Support

**Base role**

- Adds Earth to an incoming round if Earth is not already present.
- Increases impact damage.
- Applies Cracked, temporarily reducing armor and reaction resistance.

**Upgrade directions**

- **Heavy Core:** increases direct impact, stagger, and armor damage.
- **Erosion Mix:** strengthens Cracked and Sandstorm effects.
- **Granular Feed:** increases processing speed and buffer capacity.
- **Surveyed Route:** increases shot range while clearly previewing terrain blockage.

### Arcane Amplifier — Tower Support

**Base role**

- Does not receive, store, transform, or fire ammunition.
- Applies an aura to all compatible towers within range.
- May support several towers at once.

**Mutually exclusive working branches**

- **Power branch:** increases direct projectile damage, elemental potency, and Special skill power.
- **Throughput branch:** increases production rate, processing speed, output speed, and buffer capacity.

Its placement creates a formation trade-off: clustering towers gains more aura value, while spreading towers may create better attack lines and line-of-sight access.

### Nexus Lance — Special

**Base role**

- Accepts ammunition from one or more incoming routes.
- Stores rounds until it reaches its skill threshold.
- Has no normal ammunition output; its directional output is the skill itself.
- Automatically fires an area-of-effect laser in its player-selected direction when full.
- Operates only on its assigned firing layer.
- The laser stops at blocking walls or terrain.

**Element inheritance**

The laser inherits the union of elements carried by its consumed rounds. It can apply the relevant elemental states and reaction payloads to every enemy struck. Its damage, width, duration, and elemental potency scale from the amount and quality of ammunition consumed, subject to a bounded power budget.

**Upgrade directions**

- **Focused Lance:** narrower beam, longer reach, stronger damage against bosses and elites.
- **Prismatic Sweep:** wider beam and stronger elemental application against dense waves.
- **Resonant Chamber:** greater storage and better scaling from multi-element ammunition.
- **Quick Discharge:** lower activation threshold and faster firing at reduced power per beam.

The Nexus Lance is functionally a terminal tower. Players may place it elsewhere, but its skill output cannot feed another tower, so a network is normally optimized by placing it at the end of one or more chains.

## Upgrade and Unlock Structure

### Stage-local upgrades

- Tower upgrades are purchased with stage money.
- All tower levels and branches reset after the stage ends.
- Shot-range upgrades are available as part of relevant tower upgrade paths.
- Upgrade choices should create meaningful power-versus-throughput trade-offs rather than only increasing every statistic.

### Permanent unlocks

- Tower types unlock gradually across the campaign.
- Once unlocked, a tower type remains available permanently.
- There is no permanent numerical stat progression.
- Campaign progression expands the player's toolset rather than making old stages trivial through accumulated percentages.

### Working teaching sequence

1. Introduce the Ammunition Foundry, one Fire tower, manual linking, output rotation, and intentional missed shots.
2. Unlock Ice and teach both projectile fusion and Fire-Ice field reactions.
3. Unlock Wind alongside wider, denser routes and flying enemies.
4. Unlock Earth alongside armor, shields, walls, and stricter line-of-sight puzzles.
5. Unlock the Arcane Amplifier and introduce throughput bottlenecks, multi-source inputs, and controlled loops.
6. Unlock the Nexus Lance and require deliberate charging, inherited elements, aim, and firing-layer planning.
7. Combine all tower categories against mixed resistances, reaction barriers, several paths, and several heights.

## Elemental Projectile Model

The following is a working ruleset intended to keep complex chains predictable.

### Neutral round

A round begins as a neutral physical arcane projectile. It carries base damage, an owner, a firing layer, and no elemental signature.

### Unique element memory

- A projectile can remember Fire, Ice, Wind, and Earth, up to all four unique elements.
- Passing through an elemental tower adds that element only when it is absent.
- Duplicate elements do not stack or create a second copy.
- `Wind → Earth → Wind` therefore remains a Wind-Earth round.
- A duplicate-element tower may still forward the round, but it does not add another elemental payload.
- The element order is retained for presentation and future tuning, but the initial pair identities are order-independent.

### Bounded fusion power

Every unique elemental pair present on a projectile can contribute a fusion modifier. A three- or four-element round gains broader utility, but its total bonus uses a bounded fusion budget so the number of pair combinations does not multiply damage without limit.

Projectile fusion and enemy field reactions are related but distinct:

- **Projectile fusion** is built into one multi-element round before impact.
- **Field reaction** occurs when an incoming element meets a compatible elemental state that was already active on the enemy before that collision.
- Elements contained in the same projectile do not recursively trigger field reactions with each other on the same hit.
- One projectile triggers at most one field reaction per enemy per segment.
- The reaction consumes or transforms the two participating enemy states; other incoming elements may still apply their normal states afterward.

This limit keeps a four-element round readable and prevents one collision from recursively producing every possible reaction.

## Base Element Identities

| Element | Direct projectile identity | Enemy state |
|---|---|---|
| **Fire** | Higher damage and aggressive area pressure. | **Burning:** damage over time. |
| **Ice** | Control and shield pressure. | **Chilled:** movement slow; repeated application builds toward a short Freeze. |
| **Wind** | Faster travel, wider reaction reach, and light displacement. | **Gale Marked:** improves reaction spread and makes susceptible enemies easier to push backward. |
| **Earth** | Heavy impact, armor damage, and stagger. | **Cracked:** temporarily reduces armor and reaction resistance. |

## Working Elemental Fusion and Reaction Matrix

These reactions are design proposals for the first prototype, not final balance commitments.

| Pair | Multi-element projectile payload | Field reaction on an already affected enemy |
|---|---|---|
| **Fire + Ice** | **Thermal Core:** concentrated burst damage with reduced pure damage-over-time duration. | **Thermal Shock:** consumes Burning/Chilled, deals burst damage, and briefly staggers. |
| **Fire + Wind** | **Firestorm Round:** widens the damaging wake and spreads Burning around struck enemies. | **Wildfire:** consumes the participating state and spreads Burning to nearby enemies on the same layer. |
| **Fire + Earth** | **Magma Slug:** heavy impact that leaves a short-lived burning patch on the same firing layer. | **Eruption:** consumes Burning/Cracked and creates an area burst plus a brief molten hazard. |
| **Ice + Wind** | **Blizzard Round:** creates a wide cold wake with strong Chill and Freeze build-up. | **Flash Freeze:** consumes Chilled/Gale Marked and briefly freezes or heavily slows a nearby cluster. |
| **Ice + Earth** | **Crystal Round:** gains strong armor and shield damage and releases short-range shards. | **Crystal Shatter:** consumes Chilled/Cracked, breaks armor or shields, and damages nearby same-layer enemies. |
| **Wind + Earth** | **Sandstorm Round:** creates a wide abrasive wake that erodes defenses. | **Sandblast:** consumes Gale Marked/Cracked, strips armor and elemental resistance, and slightly pushes susceptible enemies backward. |

### Reaction safeguards

- Heavy and boss enemies may reduce or ignore displacement and Freeze without ignoring all reaction damage.
- Reactions use a short per-enemy cooldown to prevent dense crossing lines from producing unreadable continuous bursts.
- Area propagation remains on the enemy's firing layer.
- Immunity to one element blocks that element's state but does not automatically erase other valid elements on the projectile.
- Exact durations, radii, damage coefficients, resistance reductions, and cooldowns remain balance variables.

## Enemy Defense Model

An enemy can use one or more elemental defense profiles depending on stage difficulty:

| Profile | Working behavior |
|---|---|
| **Neutral** | Receives ordinary damage and elemental states. |
| **Vulnerable** | Takes increased damage or reaction potency from the listed element or pair. |
| **Resistant** | Takes reduced damage and shorter status duration from the listed element. |
| **Immune** | Ignores the listed elemental damage and state while still receiving other valid damage types. |
| **Reaction Barrier** | A shield or armor layer can only be efficiently broken by its specified elemental reaction. After it breaks, ordinary damage works normally. |

Easy stages should emphasize Neutral and Vulnerable enemies. Higher difficulty can introduce Resistances, Immunities, mixed waves, and Reaction Barriers without making every enemy a hard counter puzzle.

## Working Enemy Roster

Names and exact values are provisional.

| Enemy | Layer | Role | Elemental or routing lesson |
|---|---:|---|---|
| **Riftling Pack** | 0 | Numerous low-health enemies moving side by side and overlapping. | Rewards long piercing routes and wide reactions. |
| **Arcane Runner** | 0 | Fast enemy with low durability. | Punishes slow output and wasted final shots. |
| **Stonebound Brute** | 0 | Slow, armored, high-health enemy. | Earth resistance but vulnerable to Crystal Shatter and armor erosion. |
| **Ember Wisp** | 1 | Low-flying Fire creature. | Fire immunity; teaches a dedicated Layer 1 Ice or mixed network. |
| **Frost Ray** | 2 | High-flying enemy that arrives in broad formations. | Ice resistance; rewards Fire, Wind, and accurate Layer 2 routes. |
| **Gale Manta** | 2 | Fast high-air enemy with displacement resistance. | Wind resistance; encourages Earth reactions and higher projectile throughput. |
| **Prismatic Warder** | 0, 1, or 2 | Support-like enemy protected by a reaction barrier. It still never attacks towers. | Requires the displayed reaction to remove its barrier efficiently. |
| **Rift Colossus** | Authored per stage | Boss with high health and multiple Nexus-life damage. | Tests sustained throughput, resistance adaptation, and a well-aimed inherited-element laser. |

Enemy compositions may mix layers and defenses, but each wave must telegraph enough information for the player to build an appropriate network.

## Economy and Tower Management

### Money

- Money is the only stage-building currency.
- Defeating enemies grants money.
- There is no economy tower.
- Money pays for placement, upgrades, movement, and other stage-local tower actions.

### Selling

- Selling removes the tower, so future ballistic trajectories can no longer be intercepted by it.
- Buffered ammunition in that tower is lost.
- The player receives a partial refund based on the tower and its stage-local upgrades.
- The refund rate is a tuning value.

### Moving

- Moving a tower costs money.
- The tower temporarily stops operating during relocation.
- Its buffered ammunition is preserved.
- Incoming and outgoing trajectories are recalculated from physical positions and current aim angles.
- The new footprint and firing layer are validated before the move completes; shot range and line of sight then determine its new routes.

The movement fee prevents constant free repositioning from replacing careful initial placement, while still allowing recovery from a flawed network.

## Touch Interaction Model

### Placement

- Select a tower from the build interface.
- Drag or tap to position its footprint on the grid.
- Show valid and invalid placement feedback before spending money.
- Prevent placement on enemy paths, blocked cells, or unsupported footprints.

### Text-free onboarding

- The first stage begins directly on the battlefield without an instructional modal, help popup, tutorial title, objective text, or confirmation copy.
- Teach placement with an animated hand that travels from the glowing tower icon to an authored world footprint, plus a pulsing footprint and world pointer at the destination.
- Teach taps and held rotation with the same hand pointer and a glow on the single actionable control.
- After the Fire tower is placed, cue the player to open the next wave before asking them to rotate anything.
- Wait until an enemy is inside the Foundry's physical shot range and the Foundry actually launches a projectile; hold the encounter at that readable moment, then visually cue Foundry selection and held rotation into Fire.
- Next cue Fire selection and held rotation against enemy movement. Resume the encounter automatically when the head-on angle is reached.
- Keep ordinary combat HUD, state icons, hit feedback, and result feedback available; only the onboarding instructions themselves are text-free.

### Output rotation

- Select any Foundry, elemental ammunition tower, or the Nexus Lance.
- Press and hold Left or Right to rotate its output smoothly without angle snapping.
- Show shot range as a transparent ground area while placing or inspecting.
- While an ammunition-emitting tower is selected, show one wide, translucent red straight-shot guide from its output port to its current range or first wall. Update it continuously during rotation and remove it on deselection.
- Do not permanently draw weapon aim lines for unselected towers.
- When the current trajectory intersects a compatible tower, show a clear route line and direction arrow between those towers.
- Releasing the control keeps the current angle.

### Rotation protection

- Gestures beginning over UI do not rotate the camera or alter tower aim.
- Camera gestures do not silently select or rotate towers.
- Pointer release, cancel, focus loss, and app visibility loss stop held rotation so controls never stick.
- Android Back and a visible Safe Area control cancel the current placement, move, or rotation action.

## Readability and Feedback Requirements

### Tower state

Every ammunition-handling tower should communicate:

- current buffer occupancy;
- predicted incoming and outgoing physical route state;
- output cooldown and processing speed;
- current firing layer;
- shot range;
- stored projectile elements;
- full-buffer backpressure;
- blocked or invalid line of sight;
- current upgrade branch.

### Route state

- Use a readable route preview without hiding the battlefield under permanent opaque lines.
- Color and iconography should distinguish neutral, Fire, Ice, Wind, Earth, fused, blocked, full, and rotating states.
- A blocked route should visibly terminate at the wall or terrain that blocks it.
- Direction arrows should make the emitting tower and predicted receiving tower unambiguous.
- Crossed routes should remain visually separable even though they do not interact.

### Projectile state

- Projectile trails should show their unique elemental composition.
- A repeated element should not create another icon or misleading intensity tier.
- Fusion payloads need silhouettes and effects that remain legible when several projectiles overlap.
- A round that reaches a receiver which became full during flight should dissipate with clear impact feedback rather than vanish without explanation.

### Enemy state

- Elemental states use compact icons plus a strong, saturated body hue so Fire, Ice, Wind, Earth, and mixed states remain obvious while enemies move.
- Damage flash briefly overrides the status hue, then cleanly returns to the strong persistent elemental color.
- Resistance, immunity, vulnerability, and reaction barriers are previewed before or during the wave.
- Reaction names and bursts should be visible without covering the route.
- Flying-layer indicators must clearly distinguish Layer 1 from Layer 2 enemies.

### Intentional waste

When an output fires into empty space, the game should make the loss understandable through sound, trail fade, and buffer decrement rather than treating it like a bug.

## Example Tactical Formations

### Dual Foundry into Fire throughput line

Two Foundries aim through one Ember Infuser. The Infuser produces two Fire rounds in sequence rather than simultaneously. A nearby Throughput Amplifier prevents the shared buffer from becoming the bottleneck. The final Fire output is aimed along a dense ground route.

This formation produces strong Burning pressure but can waste ammunition quickly if the enemy spacing changes.

### Fire-Ice crossing reaction

One Fire route and one Ice route cross the same enemy lane from different angles. Fire applies Burning first; a later Ice projectile triggers Thermal Shock. Because routes do not interact with each other, the player must align timing through output cadence and path length rather than merging the projectiles at the crossing.

### Wind-Earth storage loop

A Gale Conduit and Terra Forge form a legal loop. Wind-Earth rounds repeatedly travel through two useful attack segments while the loop has capacity. Sending the rounds through Wind again does not add a second Wind signature; they remain Wind-Earth.

As producers fill the last available slots, the loop eventually deadlocks. The player rotates one output away from the cycle and releases the stored Sandstorm rounds toward the wave.

### Multi-layer defense

A Layer 0 network handles ground Riftlings, while a separate Layer 2 Fire-Wind network handles Frost Rays. The networks cannot exchange ammunition. Money spent improving one altitude therefore weakens the other, and camera rotation helps the player inspect both sets of straight routes.

### Elemental Nexus Lance

Several producers and elemental towers feed a Nexus Lance with mixed ammunition. Once its storage threshold is reached, it automatically fires along its chosen direction. The beam inherits the stored elements, applies the bounded fusion package, and strikes every same-layer enemy in its area until terrain stops it.

Poor aim or poor timing can discharge the entire investment into an empty lane.

## Campaign Progression

The campaign is stage-based.

- Stages introduce tower types and mechanics gradually.
- Unlocked tower types remain permanently available.
- Stage-local money, tower placement, tower levels, upgrade branches, buffers, and aim angles reset between stages.
- There is no permanent stat progression.
- Later stages increase difficulty through route geometry, height, obstacles, enemy density, speed, defenses, and reaction requirements rather than permanent player percentages.

This structure keeps the game focused on learning and applying the network system.

## Difficulty and Balance Levers

The concept deliberately leaves final numbers undefined. Primary tuning levers include:

- tower purchase, upgrade, move, and sell values;
- production interval and output cadence;
- buffer capacity and in-flight race pressure;
- projectile travel speed, width, and base damage;
- shot range and upgrade amount;
- elemental potency, duration, resistance, and reaction cooldown;
- loop capacity and rotation speed;
- enemy health, density, formation width, speed, layer, and Nexus-life damage;
- wall placement, terrain occlusion, path spacing, and available build cells;
- Nexus lives and money income per wave.

Balance should preserve three constraints:

1. Adding producers cannot solve a downstream throughput bottleneck by itself.
2. Longer routes must offer meaningful enemy contact without always outperforming shorter reliable routes.
3. Multi-element ammunition must expand tactical utility without scaling damage combinatorially beyond readable control.

## Initial Scope Boundaries

The initial concept includes:

- seven tower types across four categories;
- aim-driven physical projectile networks with multi-source input buffers;
- legal loops, backpressure, and intentional ammunition waste;
- Fire, Ice, Wind, and Earth projectile fusion and enemy reactions;
- fixed enemy paths with side-by-side and overlapping movement;
- three discrete firing layers;
- blocked straight routes through terrain and walls;
- in-wave building, upgrading, selling, moving, and continuous output rotation;
- a lives-based Arcane Nexus;
- gradual permanent tower unlocks and stage-local upgrade resets.

The initial concept excludes:

- the world, resources, towers, and Karma systems from `RawConcept_1.md`;
- Water or any separate economy resource;
- economy towers;
- enemies attacking or disabling towers;
- output branching;
- projectile-to-projectile collisions at route intersections;
- cross-layer projectile transfer or targeting;
- permanent stat upgrades;
- final balance numbers;
- implementation architecture or production schedule.

## Prototype Questions and Validation Needs

The following are tuning and prototype questions rather than missing concept foundations:

1. What buffer capacities and output cadences make congestion understandable without causing constant deadlock?
2. What continuous rotation speed makes mid-wave rerouting risky but still usable on touch devices?
3. How should several incoming physical trajectories visually enter one logical buffer without becoming unreadable?
4. What fusion-power budget keeps three- and four-element rounds useful without making two-element routes obsolete?
5. Which reaction priority is easiest to predict when an enemy already holds several elemental states?
6. How wide should routes and projectile collision volumes be when enemies overlap visually?
7. How should line-of-sight previews communicate a wall or terrain block from an isometric camera angle?
8. What Nexus-life count and enemy leak values create recoverable mistakes instead of immediate failure?
9. Which stage first introduces Layer 1 and Layer 2 without overwhelming the elemental tutorial?
10. How many persistent route and projectile effects can a mid-range Android device render while maintaining the target frame rate?

## Concept Promise

Arcane Arsenal is not a tower-defense game where every tower independently searches for a target. It is a game about **building the attack path itself**.

The player produces ammunition, routes it through magic, stores it, loops it, risks congestion, crosses enemy lanes, reacts elements, separates defenses by height, and finally chooses where that accumulated power is released. Victory should feel like watching a carefully designed magical machine survive contact with a chaotic battlefield.
