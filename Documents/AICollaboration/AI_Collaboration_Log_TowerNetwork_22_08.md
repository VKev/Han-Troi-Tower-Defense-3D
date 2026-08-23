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

The project owner requested temporary controls inside Gameplay UI, required UI source to remain centralized under `Assets/Scripts/UI/`, and asked implementation to continue through documentation and cleanup.

### Important AI response

The AI recommended keeping `GameplayUIManager` as a small coordinator and building one temporary tower-network panel below the existing Safe Area at runtime. Catalog definitions generate the six tower buttons; a state object drives selected tower, chain count, queue details, feedback, Unlink availability, and Start Wave gating.

### Option selected, revised, or rejected

- **Selected:** centralize the temporary HUD source in `Assets/Scripts/UI/Gameplay/Tower/` and parent its runtime hierarchy under the existing Safe Area.
- **Selected:** retain the old placement button only as serialized compatibility and hide it while the temporary network HUD is active.
- **Selected:** keep final art, localization, and persistent network layouts deferred.
- **Rejected:** create UI source folders below GameFlow, Tower, or feature-specific nested `UI` directories.

### Rationale

The temporary runtime panel makes the full rule set testable now without multiplying prefabs or fragmenting UI ownership. A small coordinator also keeps graph validation out of presentation code.

### Implementation or verification result

Unity `6000.3.21f1` compiled with no project-owned errors. The Tower Network and Grid Placement EditMode assembly passed `204/204`; GameFlow EditMode passed `29/29`, including the HUD hierarchy and action mapping; GameFlow PlayMode passed `3/3`; and Grid Placement PlayMode passed `8/8`. Both PlayMode suites restored a clean Bootstrap scene. Physical-device touch validation and subjective portrait/landscape review remain owner acceptance steps. No commit or push was performed in this implementation session.

## Entry 4 — Preserve authored tower prefab orientation during placement

**Responsible session:** `01a01b18-6923-78f1-b1d2-534f6c460c4d`

### Problem being addressed

Generator and Soul Nexus appeared to lie on the ground after placement even though the Sink prefab already contained an upright axis correction.

### Prompt used

The project owner reported that the Generator and Sink models were rotated incorrectly when their prefabs were placed and required both towers to stand upright on the ground.

### Important AI response

The AI compared the imported mesh axes, authored prefab rotations, and placement spawn call. Both Meshy models use local Z as their vertical axis. `GridPlacementController` instantiated every tower with `Quaternion.identity`, discarding Sink's authored `X = -90°` correction. Generator also lacked that X-axis correction and retained only its authored yaw.

### Option selected, revised, or rejected

- **Selected:** preserve each prefab's authored rotation when the placement controller instantiates it.
- **Selected:** add the same `X = -90°` Z-up-to-Y-up correction to Generator while preserving its yaw.
- **Rejected:** change model importer axes, rotate every Element tower, or special-case Generator and Soul Nexus inside placement code.

### Rationale

Prefab orientation is presentation data and should remain authoritative at instantiation. The generic spawn rule fixes Sink, Generator, and future rotated prefabs without changing the four Element prefabs, whose authored rotation is zero.

### Implementation or verification result

Generator now places at Euler `(270°, 201.99°, 0°)` and Sink at `(270°, 168.03°, 0°)`. A Unity preview-scene probe confirmed that both prefab transforms map their model Z-up axis exactly to world Y-up. Unity compiled with zero errors, Grid Placement PlayMode passed `8/8`, and the Test Framework restored a clean Bootstrap scene.

## Entry 5 — Interpolate projectile presentation between authoritative ticks

**Responsible session:** `01a01b18-6923-78f1-b1d2-534f6c460c4d`

### Problem being addressed

Projectile logic correctly advanced on the approved `0.05` second fixed tick, but visual transforms also changed only on those ticks. The resulting twenty visual position updates per second looked stepped even when Unity rendered more frames.

### Prompt used

The project owner required projectile visuals to update through Unity's rendered-frame loop or interpolation while keeping all gameplay logic on the existing catch-up tick.

### Important AI response

The AI recommended one-tick-behind interpolation. `TowerSimulationDriver` retains the existing scaled-delta accumulator and publishes a notification after every completed authoritative tick. `TowerProjectilePresenter` buffers only the final adjacent position pair for each projectile ID and interpolates pooled views in `LateUpdate` with the remaining-tick fraction.

### Option selected, revised, or rejected

- **Selected:** interpolate presentation one authoritative tick behind, adding at most `50 ms` of visual latency.
- **Selected:** retain only the latest adjacent sample pair after multi-tick catch-up rather than replaying a visual backlog.
- **Selected:** keep delayed clones hidden until their launch-delay tick reaches zero and render the target endpoint before pool release.
- **Rejected:** extrapolate ahead of manager state, move simulation into render updates, use per-projectile `Update`, add physics arrival, or install a custom PlayerLoop hook.

### Rationale

Interpolation produces smooth rendered motion without guessing future arrivals or changing queue, payload, timing, and catch-up rules. One presenter-owned `LateUpdate` also preserves the hybrid architecture and avoids a Unity callback on every projectile view.

### Implementation or verification result

Unity `6000.3.21f1` imported the new presentation track and compiled all runtime and test assemblies successfully. The Tower Network and Grid Placement EditMode assembly passed `211/211`; GameFlow EditMode passed `29/29`; GameFlow PlayMode passed `3/3`, verified scene-leaf lifecycle, and restored a clean Bootstrap scene. New coverage verifies interpolation alpha, notification of every caught-up tick, adjacent-pair retention, delayed visibility, terminal target rendering, and projectile identity protection. Focused PlayMode visual interpolation verification, device allocation profiling, and owner subjective review remain pending; no zero-allocation or device-performance claim has been made.

## Entry 6 — Split GameFlowCoordinator into focused concrete flows

**Responsible session:** `01a02931-f99b-73e0-827d-2f8aeb333ca7`

**Tracking issue:** `TowerDefense3D-tu5w`

### Problem being addressed

`GameFlowCoordinator` mixed application lifecycle, boot validation, save recovery, level-menu selection, scene transitions, and asynchronous completion callbacks in one class. Although `Start` and `Dispose` were the only application lifecycle entry points, following the nested calls made startup order and feature ownership harder to read.

### Prompt used

The project owner requested one folder of consistently named `*Flow.cs` modules. `GameFlowCoordinator.Start` should call the modules explicitly in a visible order, each module should own its related callbacks, and the coordinator must remain the sole application entry point. After reviewing the first interface-based implementation, the owner removed the proposed `IGameFlow` abstraction and requested direct concrete calls.

### Important AI response

The AI separated the existing behavior into `ApplicationBootFlow`, `LevelMenuFlow`, `LevelTransitionFlow`, and `SaveRecoveryFlow`. The coordinator now directly initializes those concrete flows in order, starts boot only after all four are ready, shuts them down in reverse order, retains the authoritative `GameFlowState`, and exposes only internal routing methods used by the flows.

### Option selected, revised, or rejected

- **Selected:** four pure C# singleton flow modules under `Assets/Scripts/GameFlow/Application/Flows/`.
- **Selected:** direct concrete constructor injection and direct calls from `GameFlowCoordinator`.
- **Selected:** callbacks remain inside the flow that owns the corresponding behavior.
- **Revised:** the initial shared `IGameFlow` lifecycle and specialized flow interfaces were removed.
- **Rejected:** multiple `IStartable` entry points, a numeric flow-order property, automatic iteration over flows, and direct flow-to-flow dependencies.

### Rationale

Only one concrete implementation exists for each application responsibility, and the coordinator must call every module in a specific visible order. Interfaces would not provide polymorphic substitution or an interchangeable collection in this design, so direct concrete dependencies are simpler and make the startup sequence easier to trace. The existing `IApplicationUIController` remains because it is a real boundary between pure application logic and the Unity UI component.

### Implementation or verification result

`ApplicationLifetimeScope` registers the four concrete flows as application singletons and retains `GameFlowCoordinator` as the only VContainer entry point. Existing coordinator behavior tests now construct the concrete modules, while transition-failure coverage targets the callback now owned by `LevelTransitionFlow`. The README hybrid-lifecycle diagram and folder rule were updated. Unity refreshed outside Play Mode, reported no Console errors, and `TowerDefense3D.GameFlow.EditModeTests.csproj` compiled successfully with zero warnings and zero errors. Better Context regenerated the affected maps and passed its freshness verification; its CLI used offline asset coverage even though the official Unity MCP connection was live. The live Unity MCP registry did not expose a Test Runner command, so the Unity EditMode suite was not executed in this session.
