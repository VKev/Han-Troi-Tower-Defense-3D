# AI Collaboration Log — Wave and Enemy — 25 August 2026

## Session continuity

- **Project:** `TowerDefense3D`
- **Responsible Codex session:** `01a02a90-cb3e-7523-97dd-8f9f705f3685`
- **Related records:** `AI_Collaboration_Log_GameFlow_23_08.md` and
  `AI_Collaboration_Log_SystemLifecycle_24_08.md`
- **Tracking issue:** `TowerDefense3D-dobs`

This file records the Wave and Enemy portion of the same architecture session. The related records contain the earlier
GameFlow, UI, folder-layout, MVP, and single-entry-point decisions. Together, the three files preserve the consequential
decisions from the session without reproducing the raw transcript.

## Entry 1 — Define the eight-enemy roster with an 80/20 data model

**Responsible session:** `01a02a90-cb3e-7523-97dd-8f9f705f3685`

### Problem being addressed

The design specified Basic, Armored, Magic Resistant, Stealth, Speed Support, Shortcut Builder, Mini-boss, and Summoner Boss
enemies. The project needed designer-authored data before runtime code, but a separate code type for every roster member would
duplicate common health, movement, resistance, CC, and reward fields.

### Prompt used

The project owner requested a data-first plan using the 80/20 principle, asked for shared behavior to be represented once,
required `Base*` names for base stats, renamed both defense fields to `BasePhysicalResistance` and `BaseMagicResistance`, and
removed the proposed `WaveDescription` field.

### Important AI response

The AI recommended one extensible `EnemyDefinition` for identity, rank, base stats, received-effect multipliers, and rewards.
Stat-only variants use the base asset. Only behavior with unique authored parameters receives a derived definition:
`StealthEnemyDefinition`, `SpeedSupportEnemyDefinition`, `ShortcutBuilderEnemyDefinition`, and
`SummonerBossEnemyDefinition`.

### Option selected, revised, or rejected

- **Selected:** `BaseMaxHealth`, `BaseMoveSpeed`, `BaseHitRadius`, `BasePhysicalResistance`, and
  `BaseMagicResistance` as common stats.
- **Selected:** independent multipliers for element status, slow strength/duration, stun, levitate, and push.
- **Selected:** eight authored assets plus one `EnemyCatalog` under `Assets/Config/Enemies/`.
- **Revised:** Physical Armor and Magic Resistance share the same reduction mechanism and use consistent Resistance naming.
- **Rejected:** one subclass per roster row, absolute damage immunity, `WaveDescription`, and speculative behavior graphs.

### Rationale

Most enemy differences are data. A small base asset covers those differences without hiding the few mechanics that genuinely
need typed configuration. This keeps the authoring surface understandable while leaving room for later enemy behaviors.

### Implementation or verification result

The common and specialized definition types, validation, catalog, and all eight roster assets were implemented. Basic,
Armored, Magic Resistant, and Mini-boss remain data-only variants; the four special mechanics retain focused definition types.
Enemy data tests and editor validation were added before runtime behavior was introduced.

## Entry 2 — Keep Wave authoring minimal, seeded, and deterministic

**Responsible session:** `01a02a90-cb3e-7523-97dd-8f9f705f3685`

### Problem being addressed

Designers needed to choose the number of waves, enemy composition, quantity, and spawn timing. A batch such as three enemies
starting at one second over a half-second window had to produce random-looking but reproducible individual spawn times.

### Prompt used

The project owner requested only the data-driven ScriptableObject layer first and explicitly deferred the runtime Wave system.
The schema had to follow the 80/20 rule and expose a random seed without adding premature routing or encounter systems.

### Important AI response

The AI reduced the authoring model to `WaveScheduleDefinition`, `WaveDefinition`, and `EnemySpawnBatchDefinition`. A schedule
owns one seed and an ordered wave list. Each batch owns an enemy reference, count, start time, and spawn window. The runtime
planner combines the schedule seed with the wave index, samples one time per enemy, and applies a stable time/sequence sort.

### Option selected, revised, or rejected

- **Selected:** `randomSeed`, ordered waves, and batches containing `enemy`, `count`, `startTimeSeconds`, and
  `spawnWindowSeconds`.
- **Selected:** zero spawn window means every member spawns exactly at the start time.
- **Selected:** deterministic plans for repeated runs with the same asset and seed.
- **Rejected:** Wave descriptions, per-wave route overrides, difficulty curves, spawn formations, nested random tables, and
  runtime systems in the initial data slice.

### Rationale

These four values express the current design examples and remain easy for a designer to inspect. Seeded expansion turns compact
authored batches into exact runtime orders without serializing generated timestamps or making tests nondeterministic.

### Implementation or verification result

The Wave data types, validation, seeded planner, and planner tests were implemented. `Level_001_Waves.asset` and
`Level_002_Waves.asset` now provide authored schedules, while every generated order is reproducible from schedule seed and
wave index.

## Entry 3 — Run Wave, Enemy, Tower, and projectile logic in one fixed-step level simulation

**Responsible session:** `01a02a90-cb3e-7523-97dd-8f9f705f3685`

### Problem being addressed

The former Tower simulation action was not an appropriate owner for starting and ending waves. Enemies also needed to move on
authored roads without colliders, share the tower projectile tick, return to a pool after death or leaking, and remain under
the single VContainer lifecycle entry point.

### Prompt used

The project owner requested a complete Wave system that owns Preparation, Start Wave, spawning, completion, preview, and
Victory. Enemy movement and projectile travel had to share a `0.05` second tick. Shared Core mechanisms were allowed only when
multiple real systems needed them; State Machine, Event Bus, and Pool Manager were examples rather than mandatory abstractions.

### Important AI response

The AI placed one `GameplaySimulationSystem` in the level system graph. Each fixed step executes spawn scheduling, tower
simulation, enemy road movement, projectile/enemy hit resolution, and wave-completion evaluation in explicit order.
`WaveSystem` owns `Preparation`, `Running`, and `Victory`; `EnemySystem` owns stable IDs, health, road progress, and lifecycle.
Only `FixedStepClock` was extracted as proven shared simulation infrastructure.

### Option selected, revised, or rejected

- **Selected:** `ApplicationEntryPoint` remains the only VContainer lifecycle entry point.
- **Selected:** one level-scoped simulation aggregate and explicit `0.05` second catch-up steps.
- **Selected:** designer-authored road cells are converted once into a `RoadPath`; enemies move as plain C# state.
- **Selected:** a focused `EnemyViewPool` presents snapshots without owning simulation.
- **Rejected:** a global Event Bus, generic Pool Manager, generic State Machine framework, per-enemy `Update`, physics movement,
  and a second entry point.

### Rationale

The explicit step order gives deterministic outcomes and makes Wave completion depend on both exhausted spawn orders and zero
living enemies. Focused runtime classes remain easier to debug than a generic framework introduced before a second use case
exists.

### Implementation or verification result

Commit `1413bd5` added `FixedStepClock`, seeded Wave runtime, road-path movement, Enemy runtime state, snapshots, and focused
tests. Wave start is gated by a valid Tower chain, the Tower simulation stops between waves, and reset returns the level to its
initial Preparation state.

## Entry 4 — Resolve piercing projectile hits from moving trajectories on XZ

**Responsible session:** `01a02a90-cb3e-7523-97dd-8f9f705f3685`

### Problem being addressed

Projectile and enemy positions both change during a fixed tick. Sampling only their final distance could miss a crossing, but
querying physics or fully precomputing every future hit would conflict with dynamic slow, push, CC, skills, and route changes.
Projectiles must pierce enemies and directly damage each enemy at most once.

### Prompt used

The project owner challenged per-enemy queries and explored full precomputation from known speeds and launch times, then chose
the trajectory approach after distinguishing geometry-changing effects from health-only effects. The requested plan also had
to include Physical, Magic, and True damage resolution.

### Important AI response

The selected algorithm treats the projectile segment and enemy segment during one tick as relative motion on XZ. It finds the
closest normalized time between the moving centers and compares squared separation with the squared combined radii. Y is
ignored. A per-projectile set of enemy IDs prevents duplicate direct hits while allowing the projectile to continue travelling.
Damage then resolves against the matching resistance; True damage bypasses resistance.

### Option selected, revised, or rejected

- **Selected:** moving-segment relative-motion intersection on XZ with no `Collider`, raycast, or projectile destruction.
- **Selected:** precompute seeded spawn orders and static road geometry, then resolve dynamic motion per fixed step.
- **Selected:** recompute future motion after speed, path, slow, push, or CC changes; health-only changes do not invalidate
  trajectory geometry.
- **Rejected:** endpoint-only overlap, all-wave hit-event baking, one physics query per projectile, and vertical-axis checks.

### Rationale

The relative-motion test catches crossings at any time inside the tick and naturally consumes current movement snapshots after
gameplay changes. Selective precomputation preserves determinism without pretending future interactive state is immutable.

### Implementation or verification result

Commit `6068e1b` added `TrajectoryHitCalculator`, `ProjectileHitSystem`, `EnemyDamageResolver`, motion snapshots, and the fixed
simulation coordinator. EditMode tests cover moving crossings, misses, resistance reduction, True damage, and fixed-step order.

## Entry 5 — Implement current special enemies without expanding into deferred systems

**Responsible session:** `01a02a90-cb3e-7523-97dd-8f9f705f3685`

### Problem being addressed

The prototype needed enough roster behavior to prove target priority and throughput, but implementing every CC, currency,
shortcut, and Soul mechanic simultaneously would obscure the Wave/Enemy slice and introduce dependencies that were not ready.

### Prompt used

The project owner requested the full Wave/Enemy plan but explicitly removed `SoulSkillSystem` from the current scope. Shortcut
routes had to remain authored rather than procedural, and shared abstractions could be introduced only after concrete reuse.

### Important AI response

The AI scoped runtime behavior to mechanics supported by current systems: the strongest non-stacking Speed Support aura,
Mini-boss reduced aura bonus, Boss aura exclusion, Stealth reveal on a direct hit, and health-phase Summoner Boss spawns.
Shortcut Builder configuration remains authored data until a Construction Point and alternate route exist.

### Option selected, revised, or rejected

- **Selected:** Speed Support, Stealth reveal, and Summoner Boss phases in `EnemySystem`.
- **Selected:** summoned enemies inherit the boss's current road position and are marked as summoned.
- **Selected:** keep Mini-boss identity and resistance/CC multipliers data-driven.
- **Deferred:** Construction Point channel/interrupt behavior and the authored shortcut route under `TowerDefense3D-k0x1`.
- **Rejected for this slice:** `SoulSkillSystem`, procedural roads, a universal enemy State Machine, and speculative currency
  farming logic.

### Rationale

This boundary makes the Wave prototype playable and validates the most distinct runtime behaviors while keeping missing CC,
shortcut, Soul, and reward authorities visible instead of faking them inside `EnemySystem`.

### Implementation or verification result

Commit `fcca368` implemented and tested Speed Support selection, Stealth reveal duration, phased Boss summons, summoned flags,
and corresponding presentation state. The authored shortcut-route task remains open rather than being hidden behind a fallback.

## Entry 6 — Wire a playable level slice and preserve honest verification evidence

**Responsible session:** `01a02a90-cb3e-7523-97dd-8f9f705f3685`

### Problem being addressed

Plain systems and data were insufficient unless the level scenes, enemy prefab, pool, Gameplay UI, Wave preview, Start Wave,
return-to-menu flow, and lifecycle teardown were connected. Test output also contained unused-event warnings and later a scene
open error caused by a missing Meshy prefab reference in the dirty baseline.

### Prompt used

The project owner requested a prototype Enemy prefab, complete scene/UI wiring, a playable game, warning cleanup, feature-level
local commits, no push, and no Player build during the time-critical refactor. After testing successfully, the owner authorized
the remaining Unity tests and later fixed the missing Meshy model reference directly.

### Important AI response

The AI added a collider-free Enemy prefab and material, a scene-owned `EnemyViewPool`, authored Wave schedules on both level
scopes, a focused Wave HUD view/presenter, and level-system wiring. It preserved unrelated Earth/Wind/VFX dirty assets and
separated runtime, trajectory damage, scene/UI integration, and special-enemy behavior into feature commits.

### Option selected, revised, or rejected

- **Selected:** authored UI and scene references; no runtime hierarchy builder.
- **Selected:** pooled Enemy views driven by snapshots; death and leak return views to the pool.
- **Selected:** Wave preview reads the next authored batches and Start Wave moves from Tower simulation ownership to Wave UI.
- **Selected:** local commits `1413bd5`, `6068e1b`, `bc918f6`, and `fcca368`; no remote push.
- **Rejected:** hiding scene-open errors with `LogAssert.Expect`, staging unrelated dirty assets, and claiming a build that was
  not run.

### Rationale

Scene-authored dependencies make the feature inspectable and playable in Unity. Separate feature commits preserve rollback
boundaries, while honest test reporting distinguishes project failures, dirty-baseline asset errors, and flaky test behavior.

### Implementation or verification result

Level 001 completed Wave 1 and returned Enemy views to the pool; Level 002 loaded cleanly. After the owner repaired the Meshy
reference, the final verification passed EditMode `266/266`, PlayMode `12/12`, and reported zero Console errors. One PlayMode
mouse-input test failed once at `HasPointerInput` and passed on immediate rerun; follow-up `TowerDefense3D-abmh` records that
flakiness. `TowerDefense3D-dobs` and the Meshy issue `TowerDefense3D-b7wk` are closed. No Player build or remote push occurred.

## Entry 7 — Rotate enemy presentation along each road segment

**Responsible session:** `01a02a90-cb3e-7523-97dd-8f9f705f3685`

### Problem being addressed

Enemy views moved through road corners but retained their spawn orientation, so the model slid sideways after changing road
direction.

### Prompt used

The project owner reported that enemies did not rotate when turning at road corners.

### Important AI response

`EnemyView` now derives a horizontal movement vector from the current and previous simulation snapshots. A non-zero vector
drives `Quaternion.LookRotation`, multiplied by the prefab's authored rotation offset so imported models keep their intended
forward-axis correction. Stationary snapshots retain the last valid facing direction.

### Option selected, revised, or rejected

- **Selected:** rotate presentation from fixed-step snapshot motion on XZ.
- **Selected:** preserve the prefab's authored local rotation as an import offset.
- **Rejected:** adding colliders, NavMesh steering, or another per-enemy lifecycle method.

### Implementation or verification result

Commit `d71d12b` added the runtime correction and a PlayMode test covering a road turn. The change remained inside enemy
presentation; deterministic road progress and movement ownership were unchanged.

## Entry 8 — Give Armored its own animated prefab

**Responsible session:** `01a02a90-cb3e-7523-97dd-8f9f705f3685`

### Problem being addressed

Enemy definitions need their own visual prefabs because roster members use different models. Armored still referenced the old
shared ToyWarrior asset instead of an Armored-owned prefab and animation override.

### Prompt used

The project owner requested one prefab per enemy type, supplied the Armored model/avatar information, and asked that the shared
Idle and Move animation contract remain extensible for enemy-specific animations later.

### Important AI response

The Armored model assets were moved under the Armored feature folder with their Unity metadata preserved where applicable. A
dedicated `ArmoredEnemy.prefab` and `ArmoredEnemy.overrideController` now map the shared Idle and Move states to the imported
Armored clips. `Armored.asset` references that prefab directly and authors the model's `0.35` hit radius.

### Option selected, revised, or rejected

- **Selected:** one prefab and one override controller per enemy visual identity.
- **Selected:** keep Idle and Move as the shared Animator contract while allowing later controllers to add special states.
- **Rejected:** choosing a prefab by enemy type in code or keeping every enemy on the old shared model.

### Implementation or verification result

Unity imported the replacement model, prefab, and controller without compile errors. Authored catalog validation was included in
the complete EditMode `282/282` and PlayMode `14/14` passes. The old ToyWarrior asset path is removed as part of the same local
feature commit, and no remote push was performed.
