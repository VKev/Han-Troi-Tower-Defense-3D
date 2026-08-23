# Tower Network Technical Specification

Status: Implemented — focused visual, device, and subjective QA pending

Owner: Project owner

Issues: `TowerDefense3D-ji9e`, `TowerDefense3D-jk8f`, `TowerDefense3D-yzrq`

Architecture migration: lifecycle, source-layout, manager-ownership, and assembly-layout clauses are superseded by the
approved `SystemLifecycle_Technical_Specification.md`. Tower rules, authored data, and simulation behavior remain authoritative.

## Purpose

This specification defines the first playable tower-network slice. A player places authored Generator, Element, and Soul Nexus towers, links them into directed chains, validates those chains, and starts a fixed-tick test simulation from the Gameplay HUD.

The authored tower documents and ScriptableObject data are canonical. Prototype documents provide external context only and do not override the decisions recorded here.

## Approved scope

- Present the six authored tower families in a temporary, touch-friendly Gameplay HUD.
- Preserve the selected `TowerCombatDefinition` through placement so every runtime tower has the correct family, role, and simulation data.
- Register and unregister placed towers with the level's `TowerNetworkManager`.
- Select towers by touch or Editor mouse input.
- Drag from a source tower to a target tower to create a link.
- Rewire immediately when a source or a single-input target already owns a conflicting link.
- Allow one outgoing link per source. The manager assigns available target input ports.
- Reject self-links, cycles, incompatible ports, and links outside the authored maximum range.
- Selecting a tower exposes an Unlink action. Unlink removes both its outgoing link and every incoming link.
- Enable Start Wave only when at least one valid `Generator -> zero or more Elements -> Soul Nexus` chain exists.
- Start Wave only starts tower simulation for testing. It does not start an enemy wave system.
- Run simulation at the authored `0.05` second tick. `TowerSimulationDriver.Update` accumulates frame delta time and executes every missed tick with a `while` loop.
- Mirror active projectile snapshots with pooled, non-physics visuals. Projectile arrival remains owned by manager position checks.
- Interpolate projectile visuals every rendered frame, one simulation tick behind authoritative logic, without moving simulation state from the `0.05` second tick.
- Place a tower by dragging its temporary Gameplay HUD button onto the board. The pointer reveals the existing footprint preview over valid board space, and release places exactly once or cancels.
- Show selected, linking, invalid-chain, valid-chain, queue, and running-state feedback clearly enough for temporary gameplay testing.

## Non-goals

- Enemy spawning, wave sequencing, progression, win, loss, rewards, or save data for links.
- Element reactions.
- Projectile payload merging. A processor replaces the incoming projectile payload with its own authored output payload.
- Physics, raycasts, or colliders for projectile travel or arrival.
- Per-projectile `Update`, visual extrapolation ahead of authoritative logic, or a custom Unity PlayerLoop hook.
- A production-quality final HUD or final visual effects.
- A second application entry point, a global manager singleton, or a level `LifetimeScope`.
- Click-to-arm placement from the temporary tower-network buttons. A short tap does not retain a tower selection for later world placement.

## Architecture and ownership

The feature follows the project's Hybrid VContainer + Explicit Scene Lifecycle pattern.

```text
ApplicationLifetimeScope
`-- TowerNetworkManager                pure C# graph and simulation authority

LevelSceneContext
|-- GridPlacementSceneAdapter          placement lifecycle
|-- TowerNetworkSceneAdapter           level registry and feature orchestration
|   |-- TowerSimulationDriver          fixed-tick Update adapter
|   |-- TowerNetworkInputController    touch and Editor mouse gestures
|   |-- TowerLinkPresenter             link and selection presentation
|   `-- TowerProjectilePresenter       pooled projectile presentation
`-- GameplayUIManager                  temporary HUD coordinator
```

`TowerNetworkManager` owns node IDs, runtime specs, links, valid-chain calculation, input queues, fixed-tick simulation state, and projectile snapshots. It does not reference UI or scene objects.

`GridPlacementController` owns board mapping, occupancy, placement input, prefab instantiation, and placement facts. It does not validate tower chains or start simulation.

`TowerNetworkSceneAdapter` owns the level-scoped mapping between placed `TowerRuntimeView` components and manager node IDs. It subscribes to placement facts, registers or unregisters nodes, and initializes the scene-owned input and presentation components.

`GameplayUIManager` owns HUD subscriptions and forwards authored tower selection, Unlink, and Start Wave requests. It does not contain graph algorithms.

The input and presenter components are authored `MonoBehaviour` leaves because they depend on touch/mouse polling, cameras, transforms, renderers, and scene lifetime. They are not application entry points.

## Runtime contracts

### Placement identity

The HUD selects a `TowerCombatDefinition`. Its `Core.PlacementDefinition` supplies the prefab and footprint. A successful placement publishes the combat definition, placement definition, spawned instance, anchor cell, and occupancy owner ID. The network adapter registers only placements with a valid combat definition.

### Runtime tower view

Every networked placed instance owns one `TowerRuntimeView` on its root. The view stores its combat definition and manager-assigned node ID for the current level session. It exposes a stable world anchor for linking and projectile presentation. Runtime IDs are not serialized or persisted.

### Link gesture

Pointer press on a registered tower selects it and starts a possible link gesture. Dragging beyond the configured threshold displays a preview. Releasing over another registered tower calls `TryRewire(source, target)`. Releasing without a valid target keeps the source selected and reports the reason without changing the graph.

Input picking is scene presentation logic. Projectile movement remains independent from this picking mechanism.

### Simulation

The manager starts only when `HasValidChain` is true. Topology and placement are locked while simulation is running. Generator towers own no input queue; if the downstream queue cannot atomically reserve the full output batch, generation pauses without losing cycle readiness. Processor output completely replaces the incoming payload. Soul Nexus consumes its authored input batch and order.

### Presentation

Link lines and tower highlights are derived from manager snapshots and the scene registry. Projectile visuals buffer the previous and current authoritative positions for each projectile ID. `TowerProjectilePresenter.LateUpdate` interpolates those positions using the simulation driver's remaining accumulator fraction. A scene-scoped pool reuses projectile view objects; release clears payload color, transform, and active state. Presentation never advances simulation or commits arrivals.

## Folder and assembly boundaries

- Tower network contracts, manager logic, runtime views, and scene presentation remain under `Assets/Scripts/Tower/Network/` using existing responsibility folders only when needed.
- Placement source remains under `Assets/Scripts/Placement/`.
- Tower HUD source is grouped under `Assets/Scripts/UI/Gameplay/Tower/` by UI-facing `Models`, `Presenters`, and `Views`.
- `GridPlacementSceneAdapter` remains under `Assets/Scripts/GameFlow/Levels/Adapter/`; the Tower-specific `TowerNetworkSceneAdapter` lives with the Tower HUD models.
- Combat ScriptableObject instances remain under `Assets/Config/Towers/`; placement-only tower definitions remain under the existing `Assets/Config/GridPlacement/` home.
- Prefabs remain under `Assets/Resources/Prefabs/`.
- Existing assemblies are reused. No assembly boundary is added unless a verified dependency requires it.

## Serialized integration

- `GameplayUIManager` builds the temporary tower buttons, selected/chain/queue status, Unlink, and Start Wave controls under the existing Gameplay UI Safe Area at runtime. The temporary slice does not add another UI prefab or nested UI source folder.
- Both authored level scenes retain participant order: placement adapter, tower-network adapter, gameplay UI.
- Each level keeps `TowerNetworkSceneAdapter`, `TowerSimulationDriver`, `TowerNetworkInputController`, `TowerLinkPresenter`, and `TowerProjectilePresenter` together on `Grid Placement/Systems`; the adapter resolves and validates those same-object dependencies.
- The application composition root and single application entry point remain unchanged.
- No `FormerlySerializedAs` attribute is introduced. Serialized changes use final descriptive field names and are resaved through Unity.

## Verification

- Compile in Unity `6000.3.21f1` with no project-owned errors.
- Run focused Tower Network EditMode tests for placement identity, registry, link input, Start Wave gating, backpressure, payload overwrite, snapshot presentation, and pooling reset.
- Run GameFlow EditMode and PlayMode suites.
- Run Grid Placement EditMode and PlayMode suites.
- Verify both levels initialize and shut down in authored participant order without duplicate managers or stale runtime nodes.
- Verify the temporary HUD at representative mobile portrait and landscape resolutions inside the Safe Area.
- Verify touch placement, link drag, rewire, select, Unlink, invalid link feedback, and Start Wave on a physical device when available. Physical-device and subjective visual QA remain owner acceptance steps when no device is connected.

## Risks and deferred work

- All six tower definitions currently may share a placement asset or placeholder visual. Combat identity must therefore come from the selected combat definition, not from prefab appearance or reverse lookup.
- Runtime-generated temporary visual primitives are acceptable for this slice but should be replaced by authored art later.
- Final HUD art, accessibility polish, localization, and persistent network layouts are deferred.

## Approved smooth projectile presentation update

Status: Implemented — verification partially complete

Issue: `TowerDefense3D-jk8f`

Approval date: 22 August 2026

### Scope and ownership

- `TowerNetworkManager` remains the only projectile simulation authority. Movement, launch delay, arrival, queue reservation, queue commitment, payload replacement, and catch-up behavior remain fixed-tick rules.
- `TowerSimulationDriver.Update` keeps the scaled `Time.deltaTime` accumulator and executes every crossed `0.05` second tick. It exposes the remaining-tick interpolation fraction and announces each completed logic tick only after manager state is consistent.
- `TowerProjectilePresenter` owns scene-scoped presentation tracks and the existing projectile view pool. It captures authoritative samples on completed ticks and renders one frame in `LateUpdate` after simulation `Update` has finished.
- `TowerProjectileView` owns only renderer configuration, payload color, rendered position, activation, and pool reset. It receives no simulation clock or manager reference.

### Presentation state and flow

Each presentation track is keyed by the manager-assigned projectile ID and stores the previous position, current position, payload, target node, launch delay, and terminal-release state. This is transient scene presentation state; it is neither authored ScriptableObject data nor persistent save data.

The rendered position is `Lerp(previousPosition, currentPosition, interpolationAlpha)`, where `interpolationAlpha` is the driver's accumulator remainder divided by `0.05`. The visual therefore follows authoritative logic by one tick, with at most `50 ms` of intentional presentation latency. Extrapolation is rejected because it can move a visual beyond an arrival or corrected route before the manager authorizes that state.

When a long frame executes multiple catch-up ticks, each completed tick replaces the buffered pair. The next rendered frame uses only the final adjacent pair and does not replay historical visual ticks in slow motion. Projectiles with remaining launch delay stay hidden until their authored sequence delay reaches zero. When an authoritative projectile disappears because it arrived, its presentation track interpolates to the target anchor, renders the endpoint, and then returns the view to the scene-owned pool.

### Integration, folders, and compatibility

- `TowerSimulationDriver` initializes before `TowerProjectilePresenter`; shutdown unsubscribes and clears the presenter before shutting down the driver.
- The presentation track remains in the existing `Assets/Scripts/Tower/Network/Presentation/` folder. No additional folder, manager, ScriptableObject, assembly, physics component, or serialized scene reference is introduced.
- Existing projectile snapshot APIs remain compatible. Presentation may use a reusable destination collection to avoid allocating a new snapshot array every rendered frame.
- Existing `0.05` second combat data, authored tower assets, link rules, queue rules, projectile IDs, payload rules, scenes, prefabs, and save data remain unchanged.

### Verification plan

- EditMode tests verify accumulator remainder and interpolation alpha, including a `0.12` second frame producing two ticks and alpha `0.4`.
- EditMode presentation tests verify previous/current interpolation, delayed clone visibility, endpoint retirement, pooled reset, and shutdown cleanup.
- Catch-up tests verify that several ticks in one frame retain only the final adjacent presentation pair while manager tick count and arrivals remain unchanged.
- PlayMode verification confirms projectile transforms move between rendered frames while manager tick state remains authoritative, and that stopping or unloading the level leaves no active view or stale subscription.
- Unity compilation, focused Tower Network tests, relevant GameFlow tests, Console inspection, and owner visual review complete the update. Physical-device profiling remains required before claiming final mobile performance.

### Risks and deferred work

- One-tick interpolation intentionally adds up to `50 ms` of visual latency. The project owner approved this tradeoff for stable, correction-free presentation.
- Runtime primitive projectile art remains temporary. Trails, impacts, authored meshes, and final VFX are deferred.
- No claim of zero allocation or improved device performance is made until target-device profiling verifies the steady-state implementation.

## Approved direct tower-button drag update

Status: Approved — implementation in progress

Issue: `TowerDefense3D-yzrq`

Approval date: 22 August 2026

### Scope and interaction contract

- The temporary tower buttons support the same direct gesture on touch devices and with the Editor mouse: press a tower button, cross the EventSystem drag threshold, move onto the board, inspect the authored footprint, and release.
- A short tap on a tower button performs no placement action. The previous click-to-arm flow is removed from these temporary buttons rather than retained as a second interaction mode.
- While the pointer remains over the HUD or cannot map to the board, the footprint is hidden. Releasing over UI, outside the board, or on an invalid footprint cancels without changing occupancy.
- Releasing on a valid footprint places exactly one tower, preserves its combat definition and authored prefab rotation, then clears the placement selection and preview.
- Escape, application pause, focus loss, HUD shutdown, level return, or a topology lock cancels the active drag.

### Ownership and event flow

- A focused `TowerPlacementDragButton` component in `Assets/Scripts/UI/Gameplay/Tower/Views/` translates uGUI `IBeginDragHandler`, `IDragHandler`, and `IEndDragHandler` callbacks into definition, pointer ID, screen position, and UI-occlusion data. It owns no placement validation.
- `TowerNetworkHudView` publishes drag begin, move, end, and cancel commands. It no longer publishes tower-button click commands.
- `GameplayUIManager` remains the small UI coordinator and forwards commands to `TowerNetworkSceneAdapter`.
- `TowerNetworkSceneAdapter` applies the existing topology-edit gate, clears link selection, and owns user feedback before delegating to `GridPlacementController`.
- `GridPlacementController` remains the only board mapping, footprint validation, occupancy reservation, prefab spawn, and placement-event owner. A distinct UI-drag pointer kind prevents its normal touch or Editor-mouse polling from processing the same physical pointer twice.

### Folder, compatibility, and verification constraints

- No UI subfolder, second placement controller, new manager, ScriptableObject, scene component, prefab reference, physics input, or raycast rule is introduced. The runtime-built HUD receives the focused drag component when it creates each tower button.
- Legacy serialized tower-selection controls remain hidden and unchanged. `SelectTower` remains available for existing placement tests and non-HUD callers, while the temporary network buttons use only direct drag.
- EditMode tests execute the uGUI drag handlers and verify definition, pointer, position, cancellation, disabled state, and absence of click selection.
- PlayMode tests verify direct mouse-equivalent drag placement, invalid release cancellation, single placement, cleared preview and selection, unchanged prefab rotation, and no duplicate processing from the controller's normal Editor-mouse loop.
- Final acceptance still requires a real touch gesture on a representative physical mobile device. Compile and automated tests do not prove finger ergonomics or every Safe Area shape.

## Implementation record

The implemented slice keeps graph and simulation state in the application-scoped pure C# `TowerNetworkManager`. `ApplicationLifetimeScope` registers the manager without registering another entry point. `GameFlowCoordinator` passes it into each `LevelSceneRuntimeContext`; `TowerNetworkSceneAdapter` begins and ends the bounded level session in the authored `LevelSceneContext` participant order.

Placement now carries the selected `TowerCombatDefinition` through `TowerPlacementRecord` and attaches `TowerRuntimeView` to the spawned tower root. Separate placement assets were authored for Generator and Soul Nexus, while the four Element definitions continue to use the existing Basic Tower placeholder visual. Runtime combat identity therefore comes from the combat definition rather than prefab appearance.

The Gameplay HUD is authored in the Gameplay UI prefab and driven by `TowerNetworkHudView` under `Assets/Scripts/UI/Gameplay/Tower/Views/`. It exposes all six catalog definitions, selection and chain state, queue state, Unlink, and the gated Start Wave action. `GameplayUIManager` remains the small coordinator between the HUD, placement controller, and initialized network adapter.

Touch-first tower picking, drag-link/rewire, selection, and feedback are owned by `TowerNetworkInputController`. It performs screen-space nearest-anchor picking and does not require tower colliders. `TowerLinkPresenter` derives selection, preview, and completed lines from registry snapshots. `TowerProjectilePresenter` buffers the final adjacent authoritative samples for each projectile ID and updates pooled `TowerProjectileView` transforms in `LateUpdate` using the simulation driver's accumulator fraction. Delayed clones stay hidden until their sequence delay expires, and retiring tracks render the target endpoint before pool release. Projectile views contain a `LineRenderer` and no collider. Arrival and catch-up simulation remain manager-owned and use the approved `0.05` second fixed tick.

Both `Level_001` and `Level_002` were saved through Unity with the network input, link, and projectile presenters on `Grid Placement/Systems`. The GameFlow editor validator requires the complete same-object component set and preserves the existing level participant order.

Validation on Unity `6000.3.21f1` completed with no project-owned compile error:

- Tower Network and Grid Placement EditMode assembly: `211/211` passed after adding interpolation, catch-up tick-notification, launch-delay, and terminal-release coverage.
- GameFlow EditMode: `29/29` passed, including the runtime-built HUD hierarchy, state rendering, and action mapping.
- GameFlow PlayMode: `3/3` passed after the interpolation update, verified scene-leaf lifecycle, and restored the clean Bootstrap scene.
- Grid Placement PlayMode: `8/8` passed and restored the clean Bootstrap scene.

Focused PlayMode verification of projectile transform movement between two rendered frames, target-device allocation profiling, physical-device touch testing, and subjective portrait/landscape visual review remain pending. Enemy waves, reactions, final HUD art, localization, saved links, and distinct final Element tower prefabs remain intentionally deferred.
