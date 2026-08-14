# Grid Placement Technical Specification

| Field | Value |
|---|---|
| Status | Implemented from an approved plan; one known Play Mode verification failure remains |
| Specification version | 1.0 |
| Last verified | 14 August 2026 |
| Unity version | 6000.3.21f1 |
| Owning module | `Assets/_Project/GridPlacement/` |
| Runtime namespace | `TowerDefense3D.GridPlacement` |
| Runtime assembly | `TowerDefense3D.GridPlacement.Runtime` |

## 1. Purpose

Grid Placement provides the authored board model, deterministic three-dimensional placement rules, mobile-first pointer interaction, atomic occupancy mutation, candidate feedback, Editor authoring tools, and SampleScene integration required to place tower prefabs on an XZ board with discrete Y levels.

This document describes the implemented baseline and is the technical contract for future Grid Placement changes. It is grounded in the approved Grid Placement decision record, the B1-B10 implementation graph, current project source, serialized assets, and live Unity inspection.

## 2. Feature boundary

### 2.1 Goals

- Represent board width on X, depth on Z, and vertical levels on Y.
- Support independently authored horizontal `CellSize` and vertical `HeightUnit`.
- Author support, buildability, and static blockers per cell and per Y level.
- Validate every base cell and the complete occupied volume of a tower footprint.
- Reserve, spawn, commit, or roll back occupancy as one placement transaction.
- Use touch as the primary input and Editor mouse as a development fallback.
- Ignore placement gestures that begin over UI and prevent secondary-pointer interference.
- Preserve an invalid candidate for repositioning or cancellation.
- Show one combined footprint preview and one ghost volume with clear valid or invalid color feedback.
- Keep placement controls inside the device safe area.
- Provide an Editor Board Painter and deterministic scene-geometry synchronization.
- Keep deterministic rules testable outside MonoBehaviour lifecycle code.

### 2.2 Non-goals

The implemented feature does not own tower economy, path blocking, rotation, selling, moving, dynamic tower stacking, save/load, network replication, combat behavior, waves, targeting, pooling, or final art. Android physical-device profiling, thermal validation, and measured 60 FPS convergence are also deferred.

## 3. Selected architecture

The feature uses a feature-oriented module with these combined approaches:

- Immutable ScriptableObject definitions for designer-authored Board and tower configuration.
- Plain C# value types and services for mapping, validation, footprint enumeration, occupancy, and transactions.
- Thin scene-scoped MonoBehaviour adapters for input, GameObject creation, UI wiring, and presentation.
- A separate Editor assembly for Board authoring and synchronized scene visualization.
- Separate Edit Mode and Play Mode test assemblies.

Direct references are preferred for owned one-to-one collaboration. The feature does not introduce a singleton, service locator, dependency-injection container, global event bus, Jobs/Burst pipeline, ECS architecture, or pooling framework.

### 3.1 Ownership and lifetime

| Owner | Lifetime | Responsibility |
|---|---|---|
| `BoardDefinition` and `TowerDefinition` assets | Project asset | Immutable authoring configuration. |
| `GridPlacementController` | Scene | Creates and owns `GridBoard`, `GridOccupancy`, and `PlacementValidator`; owns pointer state and placement orchestration. |
| `GridBoard` | Scene feature session | Immutable runtime copy of authored Board flags plus coordinate mapping. |
| `GridOccupancy` | Scene feature session | Mutable ownership state for the full three-dimensional board volume. |
| `PlacementReservation` | One placement operation | Owns a temporary reservation until commit, explicit rollback, or disposal. |
| `GridPlacementPreview` | Scene | Owns its two runtime meshes and destroys them on teardown. |
| `BoardScenePresenter` | Scene | Controls generated Board renderer visibility without disabling placement colliders. |
| Board authoring tools | Editor session | Edit Board assets and synchronize generated scene geometry with Undo support. |

## 4. Project structure

```text
Assets/_Project/GridPlacement/
├── Scripts/
│   ├── Definitions/        ScriptableObject definition types
│   ├── Board/              Coordinates, dimensions, mapping, and immutable Board state
│   ├── Placement/          Footprints, validation, occupancy, reservations, and results
│   ├── Interaction/        Scene input and UI selection adapters
│   ├── Presentation/       Board visibility, placement preview, and Safe Area fitting
│   └── TowerDefense3D.GridPlacement.Runtime.asmdef
├── Data/                   Board.asset and BasicTower.asset
├── Editor/BoardAuthoring/  Board Painter, validation, planning, and synchronization
├── Tests/EditMode/         Deterministic rules and Editor-authoring tests
├── Tests/PlayMode/         Scene, input, collider, and preview tests
├── Materials/              Board, blocker, tower, and transparent preview materials
└── Prefabs/                BasicTower.prefab
```

Responsibility folders do not create separate runtime assemblies. All children of `Scripts/` compile into the existing runtime assembly.

## 5. Data contracts

### 5.1 Coordinates and dimensions

- `GridCell` stores `(X, Z, Y)` integer coordinates.
- `GridDimensions` stores positive `Width`, `Depth`, and `Height`.
- `TowerFootprint` stores positive `Width`, `Depth`, and `Height` for occupied volume.
- The flat-array index is `X + Z * Width + Y * Width * Depth`.
- World-to-cell mapping floors X and Z after subtracting the Board origin, and rounds Y by `HeightUnit`.
- Cell-to-world mapping returns the horizontal cell center and the exact authored Y level.

### 5.2 Board cell flags

`BoardCellFlags` is a combinable flags enum:

| Flag | Meaning |
|---|---|
| `SupportsPlacement` | The base of a footprint may be supported by this cell. |
| `Buildable` | The player may place a tower base on this cell. |
| `StaticBlocker` | The cell blocks any part of an occupied tower volume. |

Duplicate authored coordinates are merged with bitwise OR when `GridBoard` builds its immutable runtime copy.

### 5.3 ScriptableObject definitions

- `BoardDefinition` owns dimensions, `CellSize`, `HeightUnit`, scene-visualization visibility, and the authored cell list.
- `TowerDefinition` owns a prefab reference and a `TowerFootprint`.
- Definition classes live in `Scripts/Definitions/`; authored `.asset` instances live in `Data/`.
- Runtime placement must not mutate the source ScriptableObject assets.

### 5.4 Current SampleScene configuration

The current authored sample is not a permanent API constant:

- Board dimensions: `20 × 20 × 8`.
- `CellSize`: `1`.
- `HeightUnit`: `1`.
- Authored cells: `418`.
- Placement support: `400` cells at Y0, `9` at Y1, and `9` at Y2.
- Buildable cells: `418`.
- Static blockers: `2`.
- Basic tower footprint: `2 × 2 × 2`.

## 6. Placement rules

### 6.1 Footprint anchoring

The candidate cell is the centered anchor. For odd dimensions, the footprint is symmetric. For an even width or depth, the unmatched remainder extends toward positive X or positive Z. The same convention is used by enumeration, validation, preview placement, and prefab spawn positioning.

### 6.2 Validation

`PlacementValidator.Evaluate` accumulates all applicable failure flags without mutating occupancy.

| Failure | Condition |
|---|---|
| `OutOfBounds` | Any required base or volume cell is outside Board dimensions, or the footprint dimensions are invalid. |
| `MissingSupport` | Any base cell lacks `SupportsPlacement`. |
| `NotBuildable` | Any base cell lacks `Buildable`. |
| `StaticBlocker` | Any cell in the complete occupied volume is a static blocker. |
| `Occupied` | Any cell in the complete occupied volume is already owned or reserved. |
| `SpawnFailed` | Reserved contract value for spawn failure reporting; the current controller handles spawn failure through rollback and candidate refresh rather than returning this flag publicly. |

Support and buildability apply to every base cell. Static blockers and occupancy apply to the full `Width × Depth × Height` volume.

### 6.3 Occupancy transaction

`GridOccupancy` uses one managed `int[]` for the complete Board volume:

- `0` means free.
- A negative token means temporarily reserved.
- A positive value is the placed tower owner ID.

The placement transaction is:

1. Revalidate the current candidate.
2. Enumerate and verify the complete volume.
3. Mark every volume cell with one negative reservation token.
4. Instantiate the selected tower prefab at the footprint bottom center.
5. Commit every reserved cell to one positive owner ID.
6. If instantiation or commit fails, destroy the partial instance and roll back all matching reserved cells.

`PlacementReservation.Dispose` rolls back automatically unless the reservation committed successfully. `GridOccupancy.ReleaseOwner` is the supported release seam for a future selling or removal feature.

## 7. Interaction flow

### 7.1 Tower selection

`TowerSelectionButton` uses Inspector references to call `GridPlacementController.SelectTower`. Selecting a definition updates the preview configuration. A selected tower remains active after a successful placement to support rapid repeated mobile placement.

### 7.2 Pointer state

The controller uses a small internal state machine:

- `Idle`: no active primary pointer.
- `Tracking`: one accepted touch or Editor mouse pointer controls the candidate.
- `IgnoredUntilRelease`: the gesture began over UI or while no tower was selected.

Touchscreen primary touch has priority. Editor mouse handling is compiled only in the Unity Editor and runs only when no touch interaction is active. Gestures that start over `EventSystem` UI are ignored until release.

### 7.3 Candidate update and release

1. Screen position is converted to a world ray.
2. The raycast uses the configured placement-surface mask and ignores triggers.
3. The hit point maps to a `GridCell` through `GridCoordinateMapper`.
4. Validation updates candidate state and preview color.
5. Releasing over a valid mapped candidate revalidates and attempts the atomic transaction.
6. Releasing over an invalid candidate does not mutate occupancy and keeps the candidate visible for repositioning or cancellation.

### 7.4 Cancellation and lifecycle safety

- The Safe Area Cancel button calls `GridPlacementController.CancelPlacement` through a serialized UnityEvent.
- Escape or Android Back uses the same cancellation path.
- Cancellation clears pointer state, selected tower, candidate state, and preview visibility.
- Application pause and focus loss cancel the active pointer so a resumed or stale gesture cannot place a tower unexpectedly.

## 8. Presentation contract

### 8.1 Candidate preview

`GridPlacementPreview` owns exactly two reusable renderers:

- One combined per-cell footprint mesh at the placement surface.
- One combined box mesh for the complete ghost volume.

The preview rebuilds meshes only when footprint dimensions, `CellSize`, or `HeightUnit` change. It updates color with `MaterialPropertyBlock`, disables shadows, light probes, reflection probes, and motion vectors, and does not create one renderer per grid cell or display the full grid.

Valid candidates are translucent green. Invalid candidates are translucent red. The ghost volume uses the same color with reduced alpha.

### 8.2 Board visualization

`BoardScenePresenter` controls only generated `MeshRenderer` visibility from `BoardDefinition.VisualizeInScene`. Generated `BoxCollider` components remain enabled so placement raycasts continue to work when visualization is hidden.

### 8.3 Safe Area

`SafeAreaFitter` converts `Screen.safeArea` into normalized anchors, reapplies it when resolution or safe area changes, and clears offsets. It is an `ExecuteAlways` component so layout can be inspected in the Editor.

## 9. Editor authoring

- `BoardDefinitionEditor` exposes Board summary data and opens the Board Painter.
- `BoardPainterWindow` edits one Y level at a time, supports paint presets, resize, metric updates, validation feedback, and continuous paint strokes.
- `BoardAuthoringDocument` merges duplicate flags, removes empty entries, preserves in-bounds data during resize, validates authoring errors, sorts serialized cells deterministically by Y/Z/X, and commits with Unity Undo/Redo support.
- `BoardChangeScheduler` batches Board asset changes and does not synchronize while entering Play Mode or compiling.
- `BoardGeometryPlanner` merges authored cells into deterministic rectangles and computes a hidden signature from dimensions, metrics, visibility, and rectangle data.
- `BoardSceneSynchronizer` owns the `Board Visualization` root and semantic `Placeable Area` and `Blocked Area` children. Generated render geometry uses shared Ground or Blocker materials and always retains enabled colliders.
- Synchronization reuses matching generated geometry and rebuilds only when the hidden signature or required components no longer match.

## 10. Assembly boundaries

| Assembly | Platform | Dependencies | Responsibility |
|---|---|---|---|
| `TowerDefense3D.GridPlacement.Runtime` | Player and Editor | `Unity.InputSystem`, `Unity.ugui` | All code under `Scripts/`. |
| `TowerDefense3D.GridPlacement.BoardAuthoring.Editor` | Editor only | Runtime assembly | Board asset authoring and scene synchronization. |
| `TowerDefense3D.GridPlacement.EditModeTests` | Editor tests | Runtime, Editor authoring, Unity test runners | Deterministic rules and authoring coverage. |
| `TowerDefense3D.GridPlacement.PlayModeTests` | Play Mode tests | Runtime, Unity test runner, Input System test framework | Scene lifecycle, input, preview, and collider coverage. |

Runtime code must not reference `UnityEditor` or test assemblies. Folder responsibility changes do not justify additional runtime assemblies by themselves.

## 11. Serialized integration

`Assets/Scenes/SampleScene.unity` is the integration scene. Current project-owned component paths are:

| Path | Component responsibility |
|---|---|
| `Grid Placement/Systems` | `GridPlacementController`; owns runtime placement state and references Board, camera, surface mask, preview, placed-object root, and initial tower. |
| `Grid Placement/Board Origin` | `BoardScenePresenter`; owns Board visualization origin and generated root reference. |
| `Grid Placement/Board Origin/Board Visualization` | Synchronized placeable and blocked geometry. |
| `Grid Placement/Placement Preview` | `GridPlacementPreview` and the two combined renderers. |
| `Placement UI/Safe Area` | `SafeAreaFitter` and touch-safe controls. |
| `Placement UI/Safe Area/Select Tower` | `TowerSelectionButton` wired to `BasicTower.asset`. |
| `Placement UI/Safe Area/Cancel` | Button UnityEvent wired to `GridPlacementController.CancelPlacement`. |

The Board and tower ScriptableObjects, tower prefab, preview material, and moved runtime scripts resolve through Unity GUIDs. File moves must preserve their `.meta` files.

## 12. Performance constraints

- Board flags and occupancy use flat managed arrays with O(1) indexed access.
- Validation is proportional to footprint base area plus occupied volume.
- Preview renderers and meshes are reused; meshes rebuild only when dimensions or metrics change.
- Candidate color changes use property blocks rather than cloned materials.
- Runtime does not display the full grid and does not create per-cell GameObjects.
- `GridOccupancy.TryReserve` currently allocates one `GridCell[]` per placement attempt that reaches reservation. This is acceptable for the current prototype and must be profiled on target Android hardware before adding pooling or native containers.
- The 60 FPS mobile target is a product goal, not current physical-device proof.

## 13. Failure handling

- A missing `BoardDefinition` logs an error and disables the controller during `Awake`.
- A missing tower prefab makes the candidate invalid.
- Invalid release performs no occupancy or spawn mutation.
- Reservation failure refreshes the current candidate without partial ownership.
- Spawn or commit failure destroys the partial instance and rolls back reservation state.
- Instantiation exceptions are logged and leave occupancy recoverable.
- Pause or focus loss cannot complete an in-progress pointer operation.

## 14. Verification

### 14.1 Required checks for future changes

- Compile in Unity `6000.3.21f1` and inspect the Console delta.
- Run the complete Grid Placement Edit Mode suite.
- Run the complete Grid Placement Play Mode suite.
- Verify runtime, Editor, and test assembly membership and dependency direction.
- Verify `.meta` GUIDs and serialized scene, ScriptableObject, prefab, material, and UnityEvent references after file moves.
- Inspect valid and invalid preview feedback, placement, repeated placement, cancellation, UI-start rejection, pause/focus behavior, and collider-only Board operation.
- Refresh and verify Better Context only while Unity is idle and outside Play Mode.

### 14.2 Current evidence

- Unity compilation after the `Runtime` to `Scripts` migration completed with zero Console errors.
- All 18 runtime scripts compile into `TowerDefense3D.GridPlacement.Runtime`.
- Scene and ScriptableObject dependencies resolve to the moved script paths.
- All moved script, assembly, and `.meta` GUIDs were preserved.
- Edit Mode result: 15 passed, 0 failed.
- Play Mode result: 2 passed, 1 failed in `GridPlacementSceneInputTests.EditorMouseRelease_PlacesOnceThenRetainsInvalidCandidate` because the expected retained candidate was null. The same result occurred before and after the folder migration, so it is tracked as the pre-existing follow-up bug `TowerDefense3D-bpw` rather than a migration regression.
- Better Context refreshed and verified source hash `d8e97e099a94` after the folder migration.

The current Play Mode failure prevents claiming a fully green verification suite until `TowerDefense3D-bpw` is resolved.

## 15. Change and approval workflow

For any material Grid Placement change:

1. Obtain explicit project-owner approval for the implementation plan.
2. Update this specification with the approved scope and decisions before implementation.
3. Implement only the approved scope and preserve GUID, namespace, assembly, and serialized contracts where practical.
4. Record approved deviations before continuing rather than silently changing the contract.
5. Run the verification in section 14.
6. Update implementation status, evidence, known limitations, and AI collaboration records.
7. Track executable tasks, blockers, and follow-up defects in Beads.

## 16. Supported extension seams

These are extension points, not implemented scope:

- Selling or removal can release committed cells through `GridOccupancy.ReleaseOwner` once stable tower IDs and gameplay ownership are defined.
- Rotation requires one approved anchor convention shared by enumeration, validation, preview, and spawn placement.
- Economy and path validation should gate placement before reservation and must not leave occupancy on failure.
- Moving a tower requires an atomic old-volume/new-volume transaction rather than release followed by an unsafe independent placement.
- Persistence requires versioned save data with stable Board and tower identities; ScriptableObject assets are not save files.
- Additional tower definitions can reuse the current data and selection boundaries without adding new assemblies.

## 17. References

- `README.md` — approved decision record, source-layout convention, and documentation workflow.
- `Assets/_Project/GridPlacement/AGENTS.md` — generated module map and ownership summary.
- `Assets/_Project/GridPlacement/Scripts/` — runtime implementation.
- `Assets/_Project/GridPlacement/Editor/BoardAuthoring/` — Editor authoring implementation.
- `Assets/_Project/GridPlacement/Tests/` — focused automated verification.
- `Assets/Scenes/SampleScene.unity` — serialized integration scene.
- Beads B1-B10 — approved dependency-safe implementation graph and verification history.
