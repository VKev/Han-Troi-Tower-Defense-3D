# AI Collaboration Log — Tower Network — 22 August 2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Area:** tower placement, linking, simulation presentation, and temporary Gameplay HUD
- **Responsible Codex session:** `01a01b18-6923-78f1-b1d2-534f6c460c4d`
- **Tracking issue:** `TowerDefense3D-ji9e`

This file records consequential design and implementation decisions without reproducing the raw conversation.

## Entry 1 — Preserve tower combat identity through placement

**Responsible session:** `01a01b18-6923-78f1-b1d2-534f6c460c4d`

### Problem being addressed

The placement system previously selected only a footprint-and-prefab `TowerDefinition`. Four Element towers can share the Basic Tower placeholder, so a spawned prefab cannot be reverse-mapped reliably to its authored combat family or network role.

### Prompt used

The project owner asked to implement the authored tower ScriptableObjects in gameplay, starting with temporary HUD placement and directed tower links. The supplied documents were canonical; prototype material was context only.

### Important AI response

The AI recommended carrying the selected `TowerCombatDefinition` through the existing placement flow while continuing to use `Core.PlacementDefinition` for footprint and prefab data. A successful placement should publish both definitions plus the spawned root, cell, and occupancy owner ID. The level adapter can then register the exact combat definition with the pure C# network manager.

### Option selected, revised, or rejected

- **Selected:** preserve combat identity explicitly in `TowerPlacementRecord` and `TowerRuntimeView`.
- **Selected:** author separate Generator and Soul Nexus placement assets while retaining the Basic Tower placeholder for Elements.
- **Rejected:** infer tower family from prefab name, prefab identity, model, or reverse catalog lookup.
- **Rejected:** combine placement and combat ScriptableObjects into a new monolithic data asset.

### Rationale

Explicit identity keeps the data-driven contract stable when visuals are shared or replaced. It also preserves the existing board placement responsibility and avoids coupling placement code to tower-network algorithms.

### Implementation or verification result

`GridPlacementController` now accepts a combat definition, places its authored placement definition, attaches and configures one `TowerRuntimeView`, and publishes `TowerPlacementRecord`. Generator and Soul Nexus placement assets reference their dedicated prefabs. Tower network EditMode coverage verifies that the registered runtime node retains the selected combat definition and placement identity.

## Entry 2 — Use the hybrid lifecycle for linking and fixed-tick simulation

**Responsible session:** `01a01b18-6923-78f1-b1d2-534f6c460c4d`

### Problem being addressed

The feature needed one authoritative graph manager, deterministic catch-up simulation, touch linking, pooled projectile visuals, and predictable level startup without adding competing `MonoBehaviour` entry points or a second VContainer scope.

### Prompt used

The project owner approved a `0.05` second accumulator loop, Generator backpressure with no Generator input queue, direct payload replacement at processors, immediate rewire, selection-based Unlink, and a test-only Start Wave action gated by at least one valid Generator-to-Soul-Nexus chain. Projectile travel must not use physics, raycasts, or colliders.

### Important AI response

The AI mapped pure graph and simulation state to an application-scoped `TowerNetworkManager`, while assigning level-scoped Unity concerns to an explicit `TowerNetworkSceneAdapter`, `TowerSimulationDriver`, input controller, and presenters. The manager remains a service rather than an entry point. The adapter starts and ends its bounded level session through `LevelSceneContext`.

### Option selected, revised, or rejected

- **Selected:** one pure C# manager registered by `ApplicationLifetimeScope`, with no Unity callbacks.
- **Selected:** screen-space touch picking for placed towers and position-based manager projectile arrival.
- **Selected:** a scene-scoped `ObjectPool<TowerProjectileView>` keyed by manager projectile IDs.
- **Selected:** processor output replaces the incoming payload instead of merging it.
- **Rejected:** global singleton access, a level `LifetimeScope`, physics projectiles, collision arrival, reaction state, and payload merging.

### Rationale

The hybrid split keeps startup and shutdown order explicit while retaining engine callbacks only where frame time, input, transforms, and rendering require them. Fixed manager ticks remain deterministic under frame stalls, and visual pooling cannot mutate simulation state.

### Implementation or verification result

Both authored levels now initialize placement, tower network, and Gameplay UI in that order and shut them down in reverse. The manager executes every accumulated `0.05` second tick, validates directed acyclic chains, applies backpressure and payload replacement, and exposes immutable snapshots. Link and projectile presenters contain no simulation authority, and projectile views contain no collider.

## Entry 3 — Keep the temporary HUD centralized and verify the playable slice

**Responsible session:** `01a01b18-6923-78f1-b1d2-534f6c460c4d`

### Problem being addressed

The first playable slice needed clear placement, linking, queue, validation, Unlink, and Start Wave controls without recreating the earlier scattered `UI` folder structure or committing to final HUD art before the rules were proven.

### Prompt used

The project owner requested temporary controls inside Gameplay UI, required UI source to remain centralized under `Assets/Scripts/UI/Scripts/`, and asked implementation to continue through documentation and cleanup.

### Important AI response

The AI recommended keeping `GameplayUIManager` as a small coordinator and building one temporary tower-network panel below the existing Safe Area at runtime. Catalog definitions generate the six tower buttons; a state object drives selected tower, chain count, queue details, feedback, Unlink availability, and Start Wave gating.

### Option selected, revised, or rejected

- **Selected:** centralize the temporary HUD source in `Assets/Scripts/UI/Scripts/` and parent its runtime hierarchy under the existing Safe Area.
- **Selected:** retain the old placement button only as serialized compatibility and hide it while the temporary network HUD is active.
- **Selected:** keep final art, localization, and persistent network layouts deferred.
- **Rejected:** create UI source folders below GameFlow, Tower, or feature-specific nested `UI` directories.

### Rationale

The temporary runtime panel makes the full rule set testable now without multiplying prefabs or fragmenting UI ownership. A small coordinator also keeps graph validation out of presentation code.

### Implementation or verification result

Unity `6000.3.21f1` compiled with no project-owned errors. The Tower Network and Grid Placement EditMode assembly passed `204/204`; GameFlow EditMode passed `29/29`, including the HUD hierarchy and action mapping; GameFlow PlayMode passed `3/3`; and Grid Placement PlayMode passed `8/8`. Both PlayMode suites restored a clean Bootstrap scene. Physical-device touch validation and subjective portrait/landscape review remain owner acceptance steps. No commit or push was performed in this implementation session.
