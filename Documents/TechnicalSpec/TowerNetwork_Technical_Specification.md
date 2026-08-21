# Tower Network Technical Specification

Status: Implemented — owner device and subjective visual QA pending  
Owner: Project owner  
Issue: `TowerDefense3D-ji9e`

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
- Show selected, linking, invalid-chain, valid-chain, queue, and running-state feedback clearly enough for temporary gameplay testing.

## Non-goals

- Enemy spawning, wave sequencing, progression, win, loss, rewards, or save data for links.
- Element reactions.
- Projectile payload merging. A processor replaces the incoming projectile payload with its own authored output payload.
- Physics, raycasts, or colliders for projectile travel or arrival.
- A production-quality final HUD or final visual effects.
- A second application entry point, a global manager singleton, or a level `LifetimeScope`.

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

Link lines and tower highlights are derived from manager snapshots and the scene registry. Projectile visuals are derived from projectile IDs and positions in `CreateProjectileSnapshot()`. A scene-scoped pool reuses projectile view objects; release clears payload color, transform, and active state. Presentation never advances simulation or commits arrivals.

## Folder and assembly boundaries

- Tower network contracts, manager logic, runtime views, and scene presentation remain under `Assets/Scripts/Tower/Scripts/Network/` using existing responsibility folders only when needed.
- Placement source remains under `Assets/Scripts/Placement/Scripts/`.
- All HUD source remains under `Assets/Scripts/UI/Scripts/`; no nested `UI` folder is added elsewhere.
- GameFlow lifecycle adapters remain under `Assets/Scripts/GameFlow/Scripts/Levels/`.
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
- Snapshot polling is intentionally simple for the prototype scale. Profile before replacing it with incremental presentation events.
- Final HUD art, accessibility polish, localization, and persistent network layouts are deferred.

## Implementation record

The implemented slice keeps graph and simulation state in the application-scoped pure C# `TowerNetworkManager`. `ApplicationLifetimeScope` registers the manager without registering another entry point. `GameFlowCoordinator` passes it into each `LevelSceneRuntimeContext`; `TowerNetworkSceneAdapter` begins and ends the bounded level session in the authored `LevelSceneContext` participant order.

Placement now carries the selected `TowerCombatDefinition` through `TowerPlacementRecord` and attaches `TowerRuntimeView` to the spawned tower root. Separate placement assets were authored for Generator and Soul Nexus, while the four Element definitions continue to use the existing Basic Tower placeholder visual. Runtime combat identity therefore comes from the combat definition rather than prefab appearance.

The temporary Gameplay HUD is implemented by `TowerNetworkHudView` and `TowerNetworkHudLayoutBuilder` under `Assets/Scripts/UI/Scripts/`. It exposes all six catalog definitions, selection and chain state, queue state, Unlink, and the gated Start Wave action. `GameplayUIManager` remains the small coordinator between the HUD, placement controller, and initialized network adapter.

Touch-first tower picking, drag-link/rewire, selection, and feedback are owned by `TowerNetworkInputController`. It performs screen-space nearest-anchor picking and does not require tower colliders. `TowerLinkPresenter` derives selection, preview, and completed lines from registry snapshots. `TowerProjectilePresenter` mirrors manager projectile IDs and positions through a scene-scoped `ObjectPool<TowerProjectileView>`; projectile views contain a `LineRenderer` and no collider. Arrival and catch-up simulation remain manager-owned and use the approved `0.05` second fixed tick.

Both `Level_001` and `Level_002` were saved through Unity with the network input, link, and projectile presenters on `Grid Placement/Systems`. The GameFlow editor validator requires the complete same-object component set and preserves the existing level participant order.

Validation on Unity `6000.3.21f1` completed with no project-owned compile error:

- Tower Network and Grid Placement EditMode assembly: `204/204` passed.
- GameFlow EditMode: `29/29` passed, including the runtime-built HUD hierarchy, state rendering, and action mapping.
- GameFlow PlayMode: `3/3` passed and restored the clean Bootstrap scene.
- Grid Placement PlayMode: `8/8` passed and restored the clean Bootstrap scene.

The remaining owner acceptance work is physical-device touch testing and subjective portrait/landscape visual review. Enemy waves, reactions, final HUD art, localization, saved links, and distinct final Element tower prefabs remain intentionally deferred.
