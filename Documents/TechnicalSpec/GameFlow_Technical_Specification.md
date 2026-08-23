# Game Flow Technical Specification

**Status:** Implemented from the approved plan; physical-device QA remains pending
**Approval date:** 2026-08-15
**Implementation tracker:** `TowerDefense3D-07y`
**Target:** Unity 6000.3.21f1, Android-first, fullscreen landscape, 60 FPS target on a representative mid-range device

## 1. Purpose

This specification defines the first production level-flow, local save, and UI-management architecture for TowerDefense3D. It also defines how the existing Grid Placement UI in `SampleScene` is migrated into the new UI ownership model without losing its touch-first behavior.

The implementation starts with Level 1 and Level 2 and must support additional catalog-authored levels without changing the flow code.

## 2. Approved player behavior

- The application always boots into the Level Menu. It never resumes the most recently played level automatically.
- Every playable level corresponds to one Unity scene.
- Level 1 is initially unlocked. Its first tap starts Level 1.
- For any locked catalog level, the first tap unlocks that selected level and requests an autosave, but does not load the scene.
- The next tap on that now-unlocked level loads it.
- Unlocking is direct selection behavior. It has no prerequisite level and does not require a win condition.
- Loading or entering Level N does not automatically unlock Level N+1.
- Gameplay provides a Return to Level Menu button.
- Scene-load failures expose Retry.
- When both primary and backup saves are unusable, the UI exposes Retry and Start New.
- A save-write failure does not revoke the in-memory unlock or block gameplay. It exposes Retry Save. Unsaved progress may be lost if the application exits before a retry succeeds.

## 3. Scope

### 3.1 In scope

- Persistent Bootstrap scene and application lifetime.
- Native `SceneManager` additive level transitions by full project-relative path.
- Exactly one loaded `Level_###` scene beside Bootstrap.
- Level Catalog authoring and validation.
- One autosave slot containing unlocked level numbers only.
- Primary, backup, and same-directory temporary save recovery.
- Persistent `ApplicationUIManager`.
- Level-scoped `GameplayUIManager`.
- Migration of the current placement instructions, tower selection, cancel action, Safe Area, EventSystem ownership, and mobile frame-rate ownership.
- Initial `Level_001` and `Level_002` scenes and separate Board assets.
- EditMode, PlayMode, serialized-integration, and Android build verification.

### 3.2 Non-goals

- Win, loss, results, or completion progression.
- Automatic Level N+1 unlocking.
- Persistence of placed towers, occupancy, placement candidates, camera state, UI state, or current scene.
- Multiple save slots, cloud save, encryption, or server-authoritative progress.
- Addressables or remote scene delivery.
- Additional session or level `LifetimeScope` layers, a service locator, a global event bus, or a global singleton API. The approved Bootstrap application scope is described in Section 4.
- Pause, settings, shop, thumbnail, or advanced screen-animation systems.
- iOS implementation in this phase.

## 4. Architecture and lifetime ownership

### 4.1 Bootstrap scene

`Assets/Scenes/Bootstrap.unity` is the first enabled player scene and the sole application composition root. It survives every content transition and owns:

- exactly one `ApplicationLifetimeScope`
- exactly one `LevelSceneLoader`
- the persistent `ApplicationUIManager` instance
- `MobileFrameRatePolicy`
- exactly one `EventSystem` with the Input System UI module
- persistent Level Menu, loading, blocking-error, Start New confirmation, save-warning, and input-blocker presentation

`ApplicationLifetimeScope` is the only VContainer composition root. It validates its authored `LevelCatalog`, `LevelSceneLoader`, and `ApplicationUIManager` references, then registers:

- the authored `LevelCatalog` instance;
- one application-owned `LocalSaveRepository` and one singleton `SaveCoordinator`;
- the existing `LevelSceneLoader` and `ApplicationUIManager` components;
- `GameFlowCoordinator` as the sole application entry point through `IStartable` and `IDisposable`.

`GameFlowCoordinator` and `SaveCoordinator` are pure C# objects rather than scene components. VContainer owns their construction and disposal. The application startup order is explicit:

```text
build ApplicationLifetimeScope container
-> construct application services and GameFlowCoordinator
-> invoke GameFlowCoordinator.Start
-> initialize ApplicationUIManager
-> validate LevelCatalog and initialize SaveCoordinator
-> publish the Level Menu or the classified blocking error
```

On application-scope disposal, VContainer invokes `GameFlowCoordinator.Dispose`, which shuts down the application UI once. No second `MonoBehaviour.Start`, `Awake`, or global singleton drives the same application flow.

Bootstrap must not own a gameplay board, placed towers, level camera, level lighting, or Grid Placement controller.

### 4.2 Level scene

Each `Assets/Scenes/Levels/Level_###.unity` scene owns:

- exactly one `LevelSceneContext`
- its camera and `AudioListener`
- its lighting and environment
- its Board asset reference and generated Board geometry
- Grid Placement controller, preview, and placed-object root
- exactly one level-scoped `GameplayUIManager`
- one instance of the shared `GameplayUI.prefab`

A level scene must not contain an `EventSystem`, `MobileFrameRatePolicy`, `ApplicationUIManager`, `SaveCoordinator`, or loading/error UI.

`LevelSceneContext` is the scene readiness and teardown boundary. Its authored participants are initialized in array order:

```text
GridPlacementSceneAdapter
-> GameplayUIManager
```

Shutdown runs in reverse order so the UI unsubscribes and cancels placement before the placement controller releases its runtime state. `LevelSceneContext.OnDestroy` is a safety fallback for scene destruction; it is not an additional application entry point.

### 4.3 Dependency direction

`GameFlowCoordinator` is the only authority that changes the application flow phase or requests a scene transition. UI views publish commands; they do not load scenes or read/write files.

Core save and scene-flow code does not depend on Grid Placement. The gameplay UI integration adapter may depend on the existing Grid Placement runtime assembly so it can forward tower-selection and cancel commands.

Dependencies flow from the Bootstrap composition root into narrow constructors or registered components. Runtime code does not expose the container, call global `Resolve`, publish a mutable static `Instance`, or create another persistent root. `GameFlowCoordinator` depends on the catalog, save coordinator, scene loader, and application UI contract; none of those services depend back on the coordinator.

The architecture is intentionally hybrid. VContainer owns application construction, startup, and disposal. `LevelSceneContext` owns ordered scene activation and reverse shutdown. Unity callbacks remain on engine- or object-local components when Unity itself owns the event: frame pacing in `MobileFrameRatePolicy.Awake`, pointer polling in `GridPlacementController.Update`, camera framing in `BoardCameraFramer.OnEnable`/`LateUpdate`, view subscription cleanup, and destruction fallbacks. These callbacks must not make application phase or scene-transition decisions.

This approved VContainer decision supersedes only the earlier clauses in this specification that excluded VContainer or preferred manual Bootstrap composition. It does not change the approved player flow, save contract, additive-scene policy, UI ownership, or introduce a session/level child scope.

## 5. Application flow state

The application exposes these mutually exclusive primary phases:

- `Booting`
- `LevelMenu`
- `LoadingLevel`
- `Gameplay`
- `BlockingError`

`SaveWarning` is an orthogonal, non-blocking presentation state.

### 5.1 Boot flow

```text
Bootstrap starts
-> load and validate the autosave
-> create UnlockProgress
-> show the Level Menu
```

A missing save is normal first-run behavior. It creates a new runtime state with Level 1 unlocked. It is not treated as corruption.

### 5.2 Locked-level selection

```text
tap locked level
-> unlock that level in UnlockProgress
-> immediately render the level as unlocked
-> request autosave
-> remain in Level Menu
```

The second tap is evaluated against the updated unlocked state and requests scene loading.

### 5.3 Level loading transaction

```text
validate catalog entry and full scene path
-> acquire the single transition gate
-> block UI input and show Loading
-> unload the currently owned level, if any
-> load target scene Additive
-> verify the returned Scene
-> set it as the active scene
-> find and validate exactly one LevelSceneContext
-> initialize the context and its GameplayUIManager
-> publish Gameplay
-> hide Loading and unblock input
```

Gate release and presentation cleanup must run in `finally`.

For duplicate requests:

- a request for the same target joins or returns the active transition result;
- a request for a different target while busy is rejected as Busy;
- button input is blocked during a transition.

Unity's scene-loaded callback is not readiness. Gameplay begins only after `LevelSceneContext.Initialize` completes.

### 5.4 Return to Level Menu

```text
GameplayUIManager cancels active placement
-> publishes Return to Level Menu
-> GameFlowCoordinator shows Loading and blocks input
-> releases LevelSceneContext bindings
-> unloads the level scene
-> sets Bootstrap active
-> refreshes and shows Level Menu
```

Long-lived application objects must not retain references to the unloaded camera, UI, Grid Placement controller, Board presenter, or other scene-owned Unity objects.

### 5.5 Transition failure

Failure must not publish `Gameplay` or modify unlock progress. The persistent blocking-error screen exposes Retry with the same validated request. Partial ownership is cleaned up before retry.

The mobile memory policy is unload-first. This reduces peak memory but means the old level is not retained as rollback content; Bootstrap remains the safe recovery boundary.

## 6. Level Catalog

`LevelCatalog.asset` is the designer-authored source of truth for available levels. Each entry contains:

- positive, unique `LevelNumber`
- player-facing `DisplayName`
- full project-relative `ScenePath`

The first asset version contains Level 1 and Level 2. The Level Menu creates its button list from the catalog, so additional levels do not require flow-code changes.

Editor and runtime validation must reject or report:

- duplicate or non-positive level numbers
- empty, duplicate, or bare-name scene identifiers
- missing scenes
- scenes absent or disabled in the active Build Scene List
- missing or multiple `LevelSceneContext` components
- level-scene ownership violations such as an extra `EventSystem`

## 7. UI architecture

### 7.1 ApplicationUIManager

The persistent application manager owns only application presentation:

- `LevelMenuScreen`
- `LoadingScreen`
- `BlockingErrorScreen`
- `StartNewConfirmation`
- `SaveWarningView`
- `InputBlocker`

It displays immutable or read-only render state and publishes user commands. It must not call `SceneManager`, access save files, mutate `UnlockProgress`, or cache level-scene objects.

`LevelMenuScreen` creates short-lived `LevelButtonView` instances from the Level Catalog. A button has explicit Locked, Unlocked, and Busy presentation states. There is no hover-only interaction.

### 7.2 GameplayUIManager

The level-scoped manager owns the current placement UI wiring:

- placement instructions
- tower-selection views
- cancel-placement button
- Return to Level Menu button

It forwards selection and cancel commands to `GridPlacementController`; it does not own placement rules, occupancy, validation, candidate computation, preview rendering, or tower spawning.

The manager remains a small coordinator. Layout, individual button presentation, and future animations stay in focused view components.

### 7.3 Existing UI migration

| Current object or binding | Migrated ownership |
|---|---|
| `Placement UI` | shared `GameplayUI.prefab` root |
| `Safe Area` | `SafeAreaContent`, retaining `SafeAreaFitter` |
| `Instructions` | `PlacementHud/Instructions`, content preserved |
| `Select Tower` | `TowerSelectionPanel/SelectTowerButton` view |
| `Cancel` | `CancelPlacementButton` managed by `GameplayUIManager` |
| persistent `Cancel -> GridPlacementController.CancelPlacement()` UnityEvent | removed and replaced by symmetric runtime manager binding |
| scene-owned `EventSystem` | removed; Bootstrap owns the only EventSystem |
| scene-owned `MobileFrameRatePolicy` | moved to Bootstrap |
| missing navigation action | new `Navigation/LevelMenuButton` |

`TowerSelectionButton` becomes a focused view containing its `Button` and `TowerDefinition`. It publishes the selected definition and no longer stores a `GridPlacementController` reference.

The gameplay manager subscribes and unsubscribes listeners symmetrically. Before returning to the menu, it calls `CancelPlacement` so no candidate or preview survives transition initiation.

### 7.4 Responsive mobile hierarchy

```text
Bootstrap
|-- Application Systems
|   |-- ApplicationLifetimeScope
|   |-- LevelSceneLoader
|   `-- MobileFrameRatePolicy
|-- EventSystem
`-- ApplicationUI
    |-- MainCanvas
    |   |-- FullBleedBackground
    |   `-- SafeAreaContent
    |       `-- ActiveScreen
    |           `-- LevelMenuScreen
    |               |-- Header
    |               `-- LevelList
    `-- OverlayCanvas
        |-- InputBlocker
        |-- LoadingScreen
        |-- BlockingErrorScreen
        |-- StartNewConfirmation
        `-- SaveWarning
```

The application container additionally owns the non-GameObject `GameFlowCoordinator`, `SaveCoordinator`, and `LocalSaveRepository`. They do not appear as Bootstrap components.

```text
LevelRoot
|-- LevelSceneContext
|   `-- Participants: GridPlacementSceneAdapter, GameplayUIManager
|-- World
|   |-- Main Camera
|   |-- Lighting
|   |-- Board Origin
|   |-- Grid Placement
|   |-- Placement Preview
|   `-- Placed Towers
`-- GameplayUI
    `-- GameplayCanvas
        `-- SafeAreaContent
            |-- PlacementHud
            |   |-- Instructions
            |   |-- TowerSelectionPanel
            |   `-- CancelPlacementButton
            `-- Navigation
                `-- LevelMenuButton
```

The application overlay sorts above gameplay UI during transitions. Canvas splitting must remain purposeful; no Canvas-per-button design is allowed. Inactive screens are disabled rather than hidden only with zero alpha.

## 8. Save contract

### 8.1 Persisted state

The version-one DTO contains:

```text
SaveRootV1
|-- SchemaVersion = 1
|-- SlotId = "autosave"
|-- SavedAtUtc
|-- AppVersion
`-- UnlockedLevelNumbers[]
```

The DTO uses a serializable array rather than persisting the runtime set directly. On load, the data is validated and normalized into `UnlockProgress`.

Level 1 is always present in valid runtime state. Invalid non-positive IDs are rejected. Catalog availability is validated independently so a save cannot provide a fragile scene reference.

### 8.2 Excluded state

The save must not contain:

- last played or currently active level
- scene paths or Unity object references
- placed tower instances or runtime instance IDs
- occupancy owner IDs
- Grid Placement candidate or preview state
- Board configuration
- camera, UI, loading, error, or transition state

### 8.3 Storage layout

```text
Application.persistentDataPath/TowerDefense3D/Saves/
|-- autosave.json
|-- autosave.backup.json
`-- autosave.<unique>.tmp
```

The internal slot ID is fixed and validated. All operations are confined to the exact owned save directory and filename pattern.

### 8.4 Transactional write

```text
capture immutable snapshot
-> serialize
-> write unique same-directory temp file
-> flush where supported
-> read, deserialize, and validate temp
-> preserve the previous primary as backup
-> replace primary, or use a tested same-volume move fallback
-> remove only stale owned temp files through a bounded policy
```

No platform-independent crash-consistency claim is made until the Android implementation is tested on a real device/filesystem.

### 8.5 Load and recovery

```text
valid primary -> apply
invalid primary + valid backup -> recover backup
invalid primary + invalid backup -> BlockingError with Retry and Start New
missing primary and backup -> normal first-run state
unsupported schema -> incompatible outcome; never silently overwrite
```

Load is transactional: temporary data is fully deserialized, validated, and migrated before replacing live state.

### 8.6 Start New and write failure

Start New requires confirmation, deletes only the validated primary, backup, and owned temporary files for the autosave slot, then creates runtime progress containing Level 1.

A failed save keeps the in-memory unlock. `SaveWarningView` exposes Retry Save. Save requests are serialized or coalesced so two writers cannot replace the same slot concurrently.

## 9. Source and asset layout

```text
Assets/Scripts/GameFlow/
|-- Scripts/
|   |-- Application/
|   |   |-- ApplicationLifetimeScope.cs
|   |   `-- GameFlowCoordinator.cs
|   |-- Levels/
|   |-- Save/
|   |-- LevelSceneContext.cs
|   `-- TowerDefense3D.GameFlow.Runtime.asmdef
|-- Editor/
`-- Tests/
    |-- EditMode/
    `-- PlayMode/

Assets/Scripts/UI/               # Single project-owned UI source home
Assets/Config/GameFlow/          # LevelCatalog and other authored configuration
Assets/Resources/Prefabs/        # Shared ApplicationUI, GameplayUI, and LevelButton prefabs

Assets/Scenes/
|-- Bootstrap.unity
`-- Levels/
    |-- Level_001.unity
    `-- Level_002.unity
```

The runtime assembly owns the stable public contracts before parallel implementation begins. Editor and test assemblies reference it without introducing runtime references to editor-only code. UI stays in the existing `Assets/Scripts/UI/` module rather than creating a second UI source tree under GameFlow.

## 10. Serialized migration procedure

The migration is one exclusive Unity Editor lane:

1. Preserve and hash the current dirty `SampleScene.unity`, `Board.asset`, their metadata, and the active Build Settings.
2. Require Unity 6000.3.21f1 in Edit Mode and idle before serialized mutation.
3. Rename `SampleScene.unity` to `Assets/Scenes/Levels/Level_001.unity` through Unity/AssetDatabase semantics so its GUID and current authored content are preserved.
4. Rename `Board.asset` to a Level 1-specific Board name while preserving its GUID.
5. Migrate the current placement UI into `GameplayUI.prefab` and wire `GameplayUIManager`.
6. Add `LevelSceneContext` and the Return to Level Menu action.
7. Create `Bootstrap.unity`, move the exclusive EventSystem and `MobileFrameRatePolicy`, and add application UI.
8. Duplicate Level 1 and its Board for Level 2. Level 2 must reference its own Board asset.
9. Populate `LevelCatalog.asset` with Level 1 and Level 2 full scene paths.
10. Set the enabled build list to Bootstrap, Level 1, and Level 2 in that order.
11. Update tests that intentionally depend on the old `SampleScene` path or UI hierarchy without changing unrelated Grid Placement behavior.

The migration must preserve unrelated current changes in the Level 1 source scene and Board. It must not recreate Level 1 from an older committed state.

## 11. Implementation graph and ownership

| Plan item | Bead | Outcome |
|---|---|---|
| B0 | `TowerDefense3D-07y.1` | Approved specification and recoverable baseline |
| B1 | `TowerDefense3D-07y.2` | Stable contracts and assembly boundary |
| B2 | `TowerDefense3D-07y.3` | Transactional save runtime |
| B3 | `TowerDefense3D-07y.4` | Bootstrap and level scene-flow runtime |
| B4 | `TowerDefense3D-07y.5` | Persistent application UI source |
| B5 | `TowerDefense3D-07y.6` | Level-scoped gameplay UI source migration |
| B6 | `TowerDefense3D-07y.7` | GameFlow authoring validation |
| B7 | `TowerDefense3D-07y.8` | Exclusive serialized integration |
| B8 | `TowerDefense3D-07y.9` | EditMode coverage |
| B9 | `TowerDefense3D-07y.10` | PlayMode integration coverage |
| B10 | `TowerDefense3D-07y.11` | Final Unity, Android, documentation, and context convergence |

B2 through B6 may run in parallel only after B1 freezes their shared contracts and assembly definitions. They must own disjoint files and must not use Unity Editor mutation or Test Runner concurrently. B7 is the exclusive serialized integration lane. B8 and B9 may author tests in parallel, but test execution is serialized in B10.

## 12. Verification matrix

### 12.1 EditMode

- Level Catalog rejects invalid IDs, duplicate paths, missing scenes, disabled build entries, and invalid contexts.
- Level 1 is initially unlocked.
- Tapping a locked level unlocks it without issuing a load request.
- Tapping the resulting unlocked level issues one load request.
- Missing-save initialization and save round-trip are deterministic.
- Primary corruption falls back to backup.
- Primary and backup corruption produce the approved blocking outcome.
- Unknown schema is not overwritten.
- Start New deletes only owned autosave files.
- Save-write failure retains the runtime unlock and permits retry.
- Same-target transition requests join and different-target requests report Busy.
- UI listeners attach and detach symmetrically.

### 12.2 PlayMode and serialized integration

- Player boot shows Level Menu without auto-loading a level.
- Level 1 first tap loads Level 1.
- Level 2 first tap unlocks without loading; its second tap loads Level 2.
- A successful save preserves Level 2 unlock after recreating the application/save state.
- Return to Level Menu unloads the level and restores menu input.
- Repeated transitions do not duplicate the EventSystem, camera, AudioListener, application services, or callbacks.
- Loading failure shows Retry and does not modify progress.
- Existing tower selection, cancel, release-to-place, invalid-candidate retention, UI raycast blocking, preview, and Safe Area behaviors remain valid after migration.
- Level 1 retains the preserved Board and scene content. Level 2 uses its independent Board.

### 12.3 Android and mobile presentation

- Android development build contains Bootstrap, Level 1, and Level 2.
- Fullscreen landscape and Safe Area behavior are verified on representative aspect ratios and a cutout/notch case.
- Touch interaction does not depend on hover, right-click, or keyboard input.
- Loading, Level Menu, gameplay HUD, and error presentation remain readable on a representative mid-range device.
- Transition peak memory, UI rebuild cost, and representative gameplay frame pacing are profiled before claiming the 60 FPS target is met.

Build success, automated tests, Editor inspection, screenshots, device playtest, and performance profiling are reported as separate evidence.

## 13. Known risks and constraints

- `SampleScene.unity` and `Board.asset` already contain user-owned dirty changes; all serialized migration must use the preserved baseline and exact current bytes.
- The existing `TowerDefense3D-bpw` Grid Placement PlayMode issue predates this feature. It must be distinguished from migration regressions and reconciled when updating the old scene/hierarchy test route.
- The repository broadly ignores Markdown. This specification requires an exact `.gitignore` exception rather than weakening the project-wide ignore policy.
- Better Context may be stale. It must not be refreshed while Unity is entering or in Play Mode, or while Play Mode state is unknown.
- Native mobile filesystem replacement behavior must be tested on the Android target; desktop Editor evidence alone is insufficient.

## 14. Completion criteria

The feature is complete only when the approved player behavior is implemented; source and serialized ownership rules hold; relevant EditMode and PlayMode tests pass or any unrelated pre-existing failure is isolated; Android build verification is complete; the specification records actual validation and deviations; the AI collaboration record is current; and Better Context is safely refreshed and verified in Edit Mode idle.

## 15. Implementation and verification evidence

### 15.1 Implemented scope

- Added the `TowerDefense3D.GameFlow.Runtime` assembly with the approved save, scene-flow, level-catalog, application UI, gameplay UI, and composition contracts.
- Added transactional local autosave with primary, backup, and owned temporary-file recovery. Persisted state contains unlocked level numbers only.
- Added Bootstrap-owned application services and UI, level-scoped gameplay UI, native additive scene loading, retry paths, Start New confirmation, and save-warning retry.
- Added the VContainer 1.19.0 application composition root with one `GameFlowCoordinator` entry point, pure C# save/application services, and authored Unity adapters.
- Migrated the existing Grid Placement HUD into the shared gameplay UI ownership model without moving placement rules into UI code.
- Preserved the original scene GUID while moving `SampleScene` to `Level_001`, and preserved the original Board GUID while moving `Board.asset` to `Level_001_Board.asset`.
- Added independent Level 2 scene and Board assets, a two-entry Level Catalog, and Build Settings ordered as Bootstrap, Level 1, and Level 2.

### 15.2 Unity and automated validation

Validation used Unity `6000.3.21f1` with the Android target active.

- GameFlow EditMode: 10 passed, 0 failed.
- GameFlow PlayMode: 4 passed, 0 failed.
- Grid Placement EditMode regression: 39 passed, 0 failed.
- Grid Placement PlayMode regression: 5 passed, 0 failed.
- `LevelCatalogValidator.CollectErrors` returned zero errors for the serialized catalog and scenes.
- Bootstrap inspection confirmed one application coordinator, scene loader, save coordinator, application UI manager, EventSystem, Input System UI module, and mobile frame-rate policy.
- Each level inspection confirmed one matching `LevelSceneContext`, its independent Board reference, one gameplay UI manager, and no application-lifetime service or EventSystem duplication.
- The PlayMode fixture restored the pre-test persistent save directory and left no backup directory or test save behind.
- The previously tracked Grid Placement scene-input issue did not reproduce after updating the intentional scene route and hierarchy assertions.

### 15.3 Android build evidence

An Android Development APK was built successfully on 2026-08-15 from the three enabled scenes.

- Build result: Succeeded.
- Build report: 0 errors and 1 warning.
- Reported build duration: 21 minutes 11.37 seconds.
- APK size: 48,354,304 bytes.
- Transient artifact: `.agent-temp/builds/gameflow-android/TowerDefense3D-GameFlow-Development.apk`.
- Warning: the configured Android SDK command-line tools expose SDK XML version 4 while Unity's detector reports support through version 3. The warning did not fail compilation, Gradle `assembleDebug`, or APK creation.
- Environment recovery: CMake `3.22.1` was installed into the configured Android SDK. The successful build used the Unity installation's matching OpenJDK and NDK. Repository assets generated only for build preprocessing were excluded from the feature changes.

### 15.4 Deviations and pending validation

- No approved gameplay or persistence behavior was changed during implementation.
- The initial Android build attempt exposed missing CMake and unset JDK/NDK paths. This was an environment issue, not a source compilation failure; the final build succeeded after supplying the required toolchain.
- Physical-device smoke testing, cutout and Safe Area visual review, real touch validation, Android filesystem interruption testing, thermal behavior, memory peaks, and representative mid-range 60 FPS profiling remain pending. No device-performance claim is made from Editor tests or APK creation alone.
