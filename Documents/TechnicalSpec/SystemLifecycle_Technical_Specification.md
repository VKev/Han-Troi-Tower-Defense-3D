# System Lifecycle Technical Specification

**Status:** Approved; implementation in progress
**Approval date:** 2026-08-24
**Implementation tracker:** `TowerDefense3D-fetc`
**Target:** Unity 6000.3.21f1, VContainer 1.19.0, Android-first mobile runtime

## 1. Purpose

This specification defines the approved project-wide migration from feature-owned `MonoBehaviour` lifecycle loops to one
VContainer entry point, plain C# systems, and focused Unity component boundaries. It also defines the final source roots,
assembly dependency graph, serialized-asset migration rules, condition-simplification rules, verification gates, and local
feature-commit sequence.

This document supersedes lifecycle, source-layout, manager-ownership, and assembly-layout clauses in older GameFlow, Grid
Placement, Board Camera, and Tower Network specifications. Their player behavior, authored data, save behavior, simulation
rules, and visual contracts remain in force unless this document explicitly changes ownership.

## 2. Goals

- Exactly one project-owned VContainer lifecycle entry point dispatches application and active-level system work.
- Plain C# systems own game logic, state, rules, deterministic simulation, and presentation decisions.
- Authored `MonoBehaviour` components own only Unity references, engine events, input sampling, and rendering/application of
  system output.
- Level-owned mutable state is constructed and disposed with one child `LevelLifetimeScope` per additive level scene.
- Source ownership is visible from `Application`, `System`, `Components`, `Editor`, and `Tests` roots without redundant
  `Scripts` folders.
- Peer types use role-revealing postfixes such as `*System`, `*View`, `*Presenter`, `*Source`, and `*Factory`.
- Dependency direction is acyclic and enforced by six assembly definitions.
- Existing scene, prefab, ScriptableObject, gameplay, touch-first UI, and save behavior is preserved.
- Proven impossible or redundant conditions are removed at the feature boundary where their invariant is established.

## 3. Non-goals

- A custom Unity PlayerLoop or replacement for VContainer lifecycle integration.
- Project-wide lifecycle interfaces, a tick registry, `SystemTickContext`, or an automatic `IEnumerable` dispatch mechanism.
- A speculative `Core`, `Ports`, `Common`, `Helpers`, or generic `Utils` folder.
- Namespace normalization merely because files move.
- `[FormerlySerializedAs]`, `[MovedFrom]`, versioned source filenames, or versioned type names.
- Changes to tower balance, board rules, input semantics, save progression, camera composition, or UI visual design.
- A service locator, mutable global singleton, or application service retaining objects owned by a disposed level scene.
- Remote Git or Beads synchronization. This implementation creates local commits only and never pushes.

## 4. Runtime architecture

### 4.1 Application scope

`Assets/Scenes/Bootstrap.unity` owns one persistent `ApplicationLifetimeScope`. It registers authored application views and
configuration, creates application systems, owns `ActiveLevelSystemSlot`, and registers exactly one entry point:

```text
ApplicationEntryPoint
├── ApplicationSystemGroup
└── ActiveLevelSystemSlot
    └── LevelSystemGroup?  # attached only while one additive level is active
```

`ApplicationEntryPoint` implements only these VContainer lifecycle contracts:

- `IAsyncStartable` for asynchronous boot.
- `ITickable` for explicit application and active-level tick dispatch.
- `ILateTickable` for explicit active-level late-tick dispatch.
- `IDisposable` for ordered shutdown.

No other project-owned type implements a VContainer lifecycle interface.

### 4.2 Level scope

Every `Level_###` scene owns one `LevelLifetimeScope`, parented to the application scope during additive loading. The level
scope registers authored components from its scene, constructs level-scoped systems and mutable state, and exposes one
`LevelSystemGroup` aggregate. The scene gateway attaches that aggregate to `ActiveLevelSystemSlot` only after successful
construction. It detaches the same aggregate before unloading the scene and disposing the child scope.

Tower topology, placement state, simulation queues, runtime view registries, projectiles, and gameplay UI state are all
level-scoped. Returning to the Level Menu cannot retain those objects.

### 4.3 Ordered dispatch

Application order is explicit inside `ApplicationSystemGroup`; it is not discovered from container enumeration. Active-level
order is explicit inside `LevelSystemGroup`:

```text
Tick(deltaTime)
1. GameplayInputSystem
2. GridPlacementSystem
3. TowerInteractionSystem
4. TowerSimulationSystem
5. GameplayUISystem.RefreshIfDirty

LateTick(deltaTime)
1. TowerLinkPresentationSystem
2. TowerProjectilePresentationSystem
3. BoardCameraSystem
```

The tower simulation continues to accumulate `Time.deltaTime` and advance its own configured fixed tick. It does not move to
Unity `FixedUpdate`, because the domain simulation interval is an authored combat rule rather than Unity physics cadence.

### 4.4 Allowed local Unity callbacks

Unity callbacks remain local when Unity owns the event or object lifetime:

- `OnEnable`, `OnDisable`, `OnDestroy`, and `OnValidate`.
- `IPointer*`, `IBeginDragHandler`, `IDragHandler`, `IEndDragHandler`, and button callbacks.
- Trigger, collision, animation, rendering, and other object-specific engine callbacks.
- Reading authored scene/prefab references and applying system output to Unity objects.

These components must not make application-phase decisions, advance a system simulation independently, or duplicate the
entry point's tick order.

## 5. Source and file layout

The following is the approved complete target tree for project-owned code. A brief comment states each file's responsibility.
Files are omitted only when implementation proves the responsibility unnecessary rather than merely inconvenient.

```text
Assets/Scripts/
├── Application/                                      # VContainer composition and lifetime integration only
│   ├── EntryPoint/
│   │   ├── ApplicationEntryPoint.cs                 # Sole VContainer lifecycle entry point
│   │   ├── ApplicationSystemGroup.cs                # Explicit application startup/tick/shutdown order
│   │   ├── LevelSystemGroup.cs                      # Explicit active-level tick and late-tick order
│   │   └── ActiveLevelSystemSlot.cs                 # Attach/detach guard for the one active level group
│   ├── Scopes/
│   │   ├── ApplicationLifetimeScope.cs              # Persistent Bootstrap composition root
│   │   └── LevelLifetimeScope.cs                    # Child scope composition for one additive level
│   ├── Scenes/
│   │   ├── BootstrapSceneActivator.cs               # Restores Bootstrap as active scene when required
│   │   └── VContainerLevelSceneGateway.cs           # Loads/unloads scenes and transfers level scope ownership
│   └── TowerDefense3D.Application.Runtime.asmdef
│
├── System/                                           # Plain C# logic; no MonoBehaviour
│   ├── AssemblyInfo.cs                              # Runtime internals exposed only to exact test assemblies
│   ├── ApplicationUI/
│   │   ├── ApplicationUISystem.cs                   # Application UI state and callback projection
│   │   ├── IApplicationUIView.cs                    # Narrow application-view boundary
│   │   └── LevelMenuItemState.cs                    # Immutable level-menu presentation state
│   ├── Board/
│   │   ├── BoardSystem.cs                           # Board initialization and view projection
│   │   ├── IBoardView.cs                            # Board rendering boundary
│   │   ├── Definitions/
│   │   │   ├── BoardDefinition.cs                   # Authored board dimensions and cells
│   │   │   ├── BoardCellDefinition.cs               # Immutable authored cell definition
│   │   │   └── GridPlaceableDefinition.cs           # Authored placeable-cell marker contract
│   │   └── Models/
│   │       ├── GridBoard.cs                         # Runtime board lookup and occupancy surface
│   │       ├── GridCell.cs                          # Integer X/Y/Z board coordinate
│   │       ├── GridCoordinateMapper.cs              # Cell/world coordinate conversion
│   │       ├── GridDimensions.cs                    # Immutable board extents
│   │       ├── GridPlaceablePlacement.cs            # Authored placeable pose data
│   │       └── LowestBoardLevelBounds.cs            # Lowest-level framing bounds calculation
│   ├── BoardCamera/
│   │   ├── BoardCameraSystem.cs                     # Late-tick camera/safe-area framing orchestration
│   │   ├── IBoardCameraView.cs                      # Camera input/output boundary
│   │   ├── BoardCameraFocusRegionCalculator.cs      # Focus-region calculation from board data
│   │   └── BoardCameraFramingSolver.cs              # Deterministic perspective pose solver
│   ├── FramePacing/
│   │   └── FramePacingSystem.cs                     # Applies target frame rate and v-sync policy once
│   ├── GameFlow/
│   │   ├── GameFlowSystem.cs                        # Application flow state and routing facade
│   │   ├── GameFlowState.cs                         # Application phase enum
│   │   ├── Flows/
│   │   │   ├── ApplicationBootFlow.cs               # Catalog/save boot and blocking recovery
│   │   │   ├── LevelMenuFlow.cs                     # Menu state and level selection
│   │   │   ├── LevelTransitionFlow.cs               # Load, retry, and return transitions
│   │   │   └── SaveRecoveryFlow.cs                  # Retry-save warning behavior
│   │   └── Levels/
│   │       ├── LevelCatalog.cs                      # Authored ordered level catalog
│   │       └── LevelCatalogEntry.cs                 # One immutable catalog entry
│   ├── GameplayInput/
│   │   ├── GameplayInputSystem.cs                   # Samples one touch-first input snapshot per tick
│   │   ├── GameplayInputMode.cs                     # Explicit placement/linking input ownership
│   │   ├── GameplayInputSnapshot.cs                 # Immutable per-frame gameplay input
│   │   └── IGameplayInputSource.cs                  # Unity input sampling boundary
│   ├── GameplayUI/
│   │   ├── GameplayUISystem.cs                      # Level HUD lifecycle and dirty refresh
│   │   ├── IGameplayUIView.cs                       # Root gameplay-view boundary
│   │   └── Tower/
│   │       ├── IPlacementHudView.cs                 # Placement HUD rendering/input boundary
│   │       ├── ITowerNetworkHudView.cs              # Tower-network HUD rendering boundary
│   │       ├── Models/
│   │       │   └── TowerNetworkHudState.cs          # Immutable HUD state snapshot
│   │       └── Presenters/
│   │           └── TowerNetworkHudPresenter.cs      # Converts tower state and UI commands to view calls
│   ├── GridPlacement/
│   │   ├── GridPlacementSystem.cs                   # Placement state machine and per-frame command handling
│   │   ├── IGridPlacementView.cs                    # Placement feedback boundary
│   │   ├── ITowerInstanceFactory.cs                 # Tower prefab instantiation boundary
│   │   ├── Definitions/
│   │   │   ├── TowerDefinition.cs                   # Authored placeable tower definition
│   │   │   └── TowerFootprint.cs                    # Tower footprint dimensions
│   │   ├── Models/
│   │   │   ├── FootprintEnumerator.cs               # Enumerates occupied footprint cells
│   │   │   ├── GridOccupancy.cs                     # Runtime occupancy map
│   │   │   ├── GridPlacementModel.cs                # Placement candidate and committed records
│   │   │   ├── PlacementReservation.cs              # Atomic occupancy reservation
│   │   │   ├── PlacementResult.cs                   # Placement outcome contract
│   │   │   └── TowerPlacementRecord.cs              # Placed tower identity, pose, and definition
│   │   └── Rules/
│   │       └── PlacementValidator.cs                 # Pure placement-rule validation
│   ├── LevelScene/
│   │   ├── LevelSceneSystem.cs                      # Scene-transition facade used by GameFlow
│   │   ├── ILevelSceneGateway.cs                    # Unity scene-operation boundary
│   │   ├── Contracts/
│   │   │   ├── LevelLoadRequest.cs                  # Level number and scene path request
│   │   │   ├── LevelSceneHandle.cs                  # Loaded level identity and scope token
│   │   │   └── LevelTransitionResult.cs             # Success/failure transition result
│   │   └── Internal/
│   │       ├── ActiveLevelState.cs                   # Current transition/scene bookkeeping
│   │       ├── LevelLoadSequence.cs                  # Ordered async load transaction
│   │       └── LevelUnloadSequence.cs                # Ordered detach/unload transaction
│   ├── SafeArea/
│   │   ├── SafeAreaSystem.cs                        # Calculates safe-area anchors only when inputs change
│   │   └── ISafeAreaView.cs                         # Screen/safe-area input and anchor boundary
│   ├── Save/
│   │   ├── SaveSystem.cs                            # Progress ownership and save/retry commands
│   │   ├── ISaveRepository.cs                       # Persistent-storage boundary
│   │   ├── Models/
│   │   │   ├── SaveSnapshot.cs                      # Complete persisted snapshot with internal schema version
│   │   │   ├── SaveLoadResult.cs                    # Load outcome contract
│   │   │   ├── SaveWriteResult.cs                   # Write outcome contract
│   │   │   └── UnlockProgress.cs                    # In-memory unlocked-level set
│   │   └── Persistence/
│   │       └── LocalSaveRepository.cs               # Primary/backup/temp transactional JSON storage
│   ├── TowerNetwork/
│   │   ├── TowerNetworkSystem.cs                    # Level-scoped tower-network facade
│   │   ├── Catalogs/
│   │   │   ├── TowerCatalog.cs                      # Authored tower data catalog
│   │   │   └── TowerCombatRules.cs                  # Global authored network/simulation rules
│   │   ├── Contracts/
│   │   │   ├── TowerNetworkSnapshots.cs             # Immutable network state snapshots
│   │   │   ├── TowerNodeContracts.cs                # Node identifiers and world positions
│   │   │   └── TowerProjectileContracts.cs          # Projectile payload and queue contracts
│   │   ├── Definitions/
│   │   │   ├── EarthTowerDefinition.cs              # Earth tower authored data
│   │   │   ├── FireTowerDefinition.cs               # Fire tower authored data
│   │   │   ├── GeneratorTowerDefinition.cs          # Generator authored data
│   │   │   ├── SoulNexusDefinition.cs               # Sink authored data
│   │   │   ├── TowerAuthoringProfiles.cs            # Shared authored tower enums/profiles
│   │   │   ├── TowerCombatDefinition.cs             # Base combat definition
│   │   │   ├── WaterTowerDefinition.cs              # Water tower authored data
│   │   │   └── WindTowerDefinition.cs               # Wind tower authored data
│   │   ├── Effects/
│   │   │   └── CombatEffectProfiles.cs              # Damage/effect stacking profiles
│   │   ├── Interaction/
│   │   │   ├── TowerInteractionSystem.cs            # Consumes routed link-selection input
│   │   │   ├── TowerRuntimeViewRegistry.cs           # Node-to-runtime-view registry
│   │   │   └── ITowerRuntimeView.cs                  # Runtime tower pose/receiver boundary
│   │   ├── Model/
│   │   │   ├── TowerNetworkState.cs                 # Nodes, links, projectiles, and tick state
│   │   │   ├── TowerTopologyModel.cs                # Registration, links, and graph mutation
│   │   │   └── TowerChainEvaluator.cs               # Valid generator-to-sink chain evaluation
│   │   ├── Presentation/
│   │   │   ├── TowerLinkPresentationSystem.cs       # Late-tick link geometry projection
│   │   │   ├── ITowerLinkView.cs                    # Link-view pool/render boundary
│   │   │   ├── TowerProjectilePresentationSystem.cs # Late-tick projectile interpolation
│   │   │   ├── TowerProjectilePresentationTrack.cs  # Per-projectile interpolation state
│   │   │   └── ITowerProjectileViewPool.cs           # Projectile-view pool boundary
│   │   ├── Progression/
│   │   │   └── TowerProgressionProfiles.cs          # Element upgrade-cost profiles
│   │   ├── Runtime/
│   │   │   ├── TowerRuntimeSpec.cs                  # Immutable runtime tower specification
│   │   │   └── TowerRuntimeSpecFactory.cs           # Definition-to-runtime-spec conversion
│   │   ├── Simulation/
│   │   │   ├── TowerInputBuffer.cs                  # Bounded projectile input queues
│   │   │   ├── TowerSimulationSystem.cs             # Accumulator and deterministic tick dispatch
│   │   │   ├── TowerSimulationModel.cs              # Pure one-tick simulation state changes
│   │   │   ├── TowerGeneratorSimulation.cs          # Generator emission rules
│   │   │   ├── TowerProcessorSimulation.cs          # Processor transform rules
│   │   │   ├── TowerProjectileSimulation.cs         # Projectile travel and delivery rules
│   │   │   └── TowerSinkSimulation.cs               # Soul Nexus consumption rules
│   │   └── Validation/
│   │       └── TowerDataValidator.cs                # Authored tower/catalog validation
│   └── TowerDefense3D.System.Runtime.asmdef
│
├── Components/                                       # Authored Unity runtime boundaries
│   ├── ApplicationUI/Views/
│   │   ├── ApplicationUIView.cs                     # Root application UI component
│   │   ├── BlockingErrorView.cs                     # Blocking-error modal rendering/callbacks
│   │   ├── LevelMenuView.cs                         # Level-list rendering and selection forwarding
│   │   ├── LoadingView.cs                           # Loading state rendering
│   │   ├── LevelButtonView.cs                       # One authored level button
│   │   ├── SaveWarningView.cs                       # Retry-save warning rendering/callbacks
│   │   └── StartNewConfirmationView.cs              # Start-new confirmation rendering/callbacks
│   ├── Board/
│   │   ├── BoardView.cs                             # Applies board visibility/collider state
│   │   └── GridPlaceableAuthoring.cs                # Authored placeable-cell Unity marker
│   ├── BoardCamera/
│   │   └── BoardCameraView.cs                       # Camera, transform, screen, and safe-area bridge
│   ├── GameplayInput/
│   │   └── GameplayInputSource.cs                   # Touch-first Input System sampling
│   ├── GameplayUI/
│   │   ├── GameplayUIView.cs                        # Root gameplay HUD component
│   │   └── Tower/Views/
│   │       ├── PlacementHudView.cs                  # Placement instructions/cancel rendering
│   │       ├── TowerNetworkHudView.cs               # Network status rendering
│   │       └── TowerPlacementDragButtonView.cs       # Pointer/drag event source for tower placement
│   ├── GridPlacement/
│   │   ├── GridPlacementView.cs                     # Placement preview and feedback renderer
│   │   └── TowerInstanceFactory.cs                  # Unity tower prefab instantiation
│   ├── SafeArea/
│   │   └── SafeAreaView.cs                          # RectTransform and Screen safe-area bridge
│   ├── TowerNetwork/Views/
│   │   ├── TowerRuntimeView.cs                      # Authored tower anchors and pose boundary
│   │   ├── TowerLinkView.cs                         # Link collection renderer
│   │   ├── TowerLinkLineView.cs                     # One pooled link line
│   │   ├── TowerProjectilePoolView.cs               # Projectile GameObject pool boundary
│   │   └── TowerProjectileView.cs                   # One projectile renderer
│   └── TowerDefense3D.Components.Runtime.asmdef
│
├── Editor/                                           # Centralized Editor-only code
│   ├── AssemblyInfo.cs                              # Editor internals exposed to EditMode tests when required
│   ├── Board/Authoring/
│   │   ├── BoardAuthoringDocument.cs                # Mutable Editor authoring document
│   │   ├── BoardChangeScheduler.cs                  # Coalesces scene synchronization
│   │   ├── BoardDefinitionEditor.cs                 # BoardDefinition custom inspector
│   │   ├── BoardGeometryPlan.cs                     # Planned authored hierarchy geometry
│   │   ├── BoardGeometryPlanner.cs                  # Creates geometry plans from board data
│   │   ├── BoardPainterWindow.cs                    # Board paint tool window
│   │   ├── BoardPaintPreset.cs                      # Cell-paint presets
│   │   ├── BoardRoadPaintMode.cs                    # Road-role paint mode
│   │   └── BoardSceneSynchronizer.cs                # Writes authored board hierarchy to scenes
│   ├── BoardCamera/
│   │   └── BoardCameraAuthoringSynchronizer.cs      # Writes authored camera pose
│   ├── GameFlow/
│   │   └── LevelCatalogValidator.cs                 # Build/editor level-catalog validation
│   └── TowerDefense3D.Editor.asmdef
│
└── Tests/                                            # Centralized tests and test assemblies
    ├── EditMode/
    │   ├── Architecture/                            # Assembly direction and lifecycle ownership tests
    │   ├── Application/                             # Entry point/group/slot/composition tests
    │   ├── ApplicationUI/                           # Application UI projection tests
    │   ├── Board/                                   # Board model and authoring tests
    │   ├── BoardCamera/                             # Camera calculator/solver tests
    │   ├── GameFlow/                                # Boot/menu/transition state tests
    │   ├── GameplayInput/                           # Input snapshot/ownership tests
    │   ├── GameplayUI/                              # HUD presenter/state tests
    │   ├── GridPlacement/                           # Placement rules/model tests
    │   ├── LevelScene/                              # Load/unload ordering tests
    │   ├── Save/                                    # Save recovery and schema tests
    │   ├── TowerNetwork/                            # Network, simulation, and presentation tests
    │   └── TowerDefense3D.EditModeTests.asmdef
    └── PlayMode/
        ├── Application/                             # Bootstrap and scope integration tests
        ├── Board/                                   # Board collider/render integration tests
        ├── BoardCamera/                             # Runtime framing tests
        ├── GameFlow/                                # Level load/return/retry tests
        ├── GameplayInput/                           # Touch/mouse fallback tests
        ├── GridPlacement/                           # Runtime placement/view tests
        ├── TowerNetwork/                            # Scene-leaf tower integration tests
        └── TowerDefense3D.PlayModeTests.asmdef
```

The PlayMode host scene moves to `Assets/Scenes/Tests/GameFlowPlayModeTestHost.unity`. It is a serialized test asset rather
than source code and therefore does not live below `Assets/Scripts/Tests`.

## 6. Assembly graph

The final project-owned assembly graph contains exactly six assembly definitions and no assembly references:

```text
TowerDefense3D.System.Runtime
        ↑
TowerDefense3D.Components.Runtime
        ↑
TowerDefense3D.Application.Runtime

TowerDefense3D.Editor         -> exact runtime assemblies
TowerDefense3D.EditModeTests  -> exact runtime/editor assemblies
TowerDefense3D.PlayModeTests  -> exact runtime assemblies
```

Allowed dependencies:

- `Application.Runtime` -> `Components.Runtime`, `System.Runtime`, VContainer.
- `Components.Runtime` -> `System.Runtime`, Unity Input System, uGUI, TextMeshPro when used.
- `System.Runtime` -> Unity modules required by data contracts and ScriptableObjects, but never project `Application` or
  `Components` assemblies and never `MonoBehaviour` inheritance.
- `Editor`, `EditModeTests`, and `PlayModeTests` -> only assemblies directly exercised by their code.

## 7. Serialized integration

- Move every `.cs` together with its existing `.meta` where a move preserves the type and filename.
- Do not change namespaces merely because folders move.
- Do not rename serialized fields during the architectural migration.
- Do not use `[FormerlySerializedAs]` or `[MovedFrom]`.
- When a class/file is intentionally renamed, update code references and save every affected scene, prefab, and
  ScriptableObject through Unity in the same feature slice.
- `Bootstrap.unity` replaces `LevelSceneLoader` and `MobileFrameRatePolicy` components with the application scope/entry-point
  composition while preserving one EventSystem.
- `Level_001.unity` and `Level_002.unity` replace `LevelSceneContext`, adapters, and system-like presenters/drivers with one
  `LevelLifetimeScope` plus focused board, camera, input, placement, UI, and tower views.
- `ApplicationUI.prefab` contains authored screen/view hierarchy and `SafeAreaView`; it never constructs layout at runtime.
- `GameplayUI.prefab` contains authored HUD/view hierarchy, `TowerPlacementDragButtonView`, and `SafeAreaView`; it removes the
  forwarding-only tower-selection/input-router layer.
- BasicTower, Generator, and Soul Nexus prefabs author `TowerRuntimeView` and required anchors; runtime code never adds the
  component dynamically.

## 8. Condition-simplification contract

Every feature slice audits changed production code before its verification gate:

1. Trace all constructors, VContainer registrations, public callers, state transitions, scene/prefab wiring, and authored-data
   validation relevant to a guard.
2. Validate an invariant once at its meaningful boundary: composition, constructor, command entry, deserialization, or
   authoring validation.
3. Remove repeated null/state checks guaranteed by that invariant, duplicate nested conditions, branches made unreachable by
   earlier guards, and catch-all fallbacks that hide programmer errors.
4. Keep checks at real external and lifetime boundaries: missing or invalid authored data, destroyed `UnityEngine.Object`
   references, async scene load/unload failure or cancellation, unload races, save I/O, optional data, user input, and public
   API misuse.
5. Add focused tests when an invariant would otherwise exist only as an assumption.

Condition cleanup is part of the owning feature commit. It is not a separate cleanup pass and must not introduce speculative
abstractions.

## 9. Feature migration and local commits

The implementation is divided into independently working feature slices. A slice may contain code changes, moves/renames,
`.meta` files, assembly updates, serialized wiring, condition cleanup, and related tests when all are required for that feature.

1. `docs: Cập nhật đặc tả kiến trúc lifecycle`
2. `feat: Thêm single entry point cho application lifecycle`
3. `chore: Chuyển Board và camera sang system lifecycle`
4. `chore: Chuyển gameplay input và placement sang system lifecycle`
5. `chore: Chuyển TowerNetwork sang system lifecycle`
6. `chore: Chuyển Gameplay UI sang MVP`
7. `chore: Chuyển save progress sang SaveSystem`
8. `chore: Chuyển Application UI sang MVP`
9. `chore: Chuyển GameFlow và level scene sang VContainer scope`
10. `chore: Hoàn tất source layout và assembly graph`

The dirty worktree present at approval is the implementation baseline and must be integrated into the relevant Tower Network,
Gameplay UI, and GameFlow slices. It must not be reset or stashed. Every commit stages explicit paths only; `git add -A` is
forbidden. No `git push`, `bd dolt push`, pull-request publication, or other remote synchronization is permitted.

## 10. Verification

Compile and test only after the complete feature slice is assembled. Do not compile after individual edits, moves, renames,
assembly changes, or serialized-asset changes inside the slice.

Each feature gate includes:

- Unity 6000.3.21f1 compile with no new errors.
- Relevant EditMode tests.
- Relevant PlayMode tests when the feature owns scene/prefab integration.
- Missing-script and serialized-reference inspection for every changed scene/prefab.
- Console delta review rather than relying on an old clean Console.
- `git diff --check` and an explicit staged-path review before the local commit.

Final verification includes a full compile, all project EditMode and PlayMode suites, representative boot/load/return gameplay
flow, Level 1 and Level 2 scope teardown, catalog validation, no missing scripts, final assembly-dependency audit, and an Android
development build. Physical-device performance and visual QA remain separately reported boundaries.

Unity MCP is the primary Editor integration. CLI/project tooling is the next fallback. Computer Use is restricted to modal or
Editor recovery that MCP cannot complete, including Reload/Don't Save prompts, warnings, a blocked Editor, connection recovery,
or reopening the exact project after a crash. Ambiguous destructive dialogs require user direction.

## 11. Completion evidence

This section is updated after implementation with commit identifiers, compile/test counts, scene/prefab inspection results,
Android build output, approved deviations, and remaining physical-device or subjective visual QA.
