# Board Road Cell Technical Specification

**Status:** Approved
**Approved:** 18 August 2026 (project owner approved the implementation plan in chat; this document records that approved plan before implementation begins, per the repository's technical-spec workflow)
**Implementation status:** Implemented and verified
**Target Unity version:** 6000.3.21f1
**Tracking Bead:** created alongside this specification (see "Verification plan")

## Purpose

Let a board author paint three new cell states on a `BoardDefinition` through the Board Painter: **Road** (the enemy's walkable path), **Road Spawn** (where enemies appear), and **Road End** (where enemies arrive). Painted cells must render in a distinct color in the Board Painter's 2D grid, and must also appear as a distinct colored overlay in the live 3D scene (mirroring how `SupportsPlacement`/`StaticBlocker` cells already appear as generated geometry). These three cell states currently carry **no gameplay behavior** — no enemy pathing, no spawn/despawn logic. This specification covers data model, authoring UI, and visualization only.

## Approved scope

- Add three new bits to `TowerDefense3D.GridPlacement.BoardCellFlags` (`Assets/Scripts/Board/Scripts/BoardCellDefinition.cs`): `Road`, `RoadSpawn`, `RoadEnd`. Reuses the existing per-cell flags storage (`BoardCellDefinition.flags`, part of `BoardDefinition.cells`, stored as a raw `int`). No new serialized field or asset type.
- The three bits are **mutually exclusive with each other** on a given cell (a cell is Road, or Road Spawn, or Road End, never more than one of the three), but **orthogonal to every existing flag** (`SupportsPlacement`, `Buildable`, `StaticBlocker`, `CameraFocus`) — a cell may combine a road role with any existing preset or Camera Focus, matching how `CameraFocus` already composes freely with presets.
- Add an independent "Road Brush" to the Board Painter (`Assets/Scripts/Board/Editor/BoardAuthoring/BoardPainterWindow.cs`) with three selectable modes (Road / Spawn / End) plus an eraser, mutually exclusive with the existing preset palette and the existing Camera Focus brush for a given stroke (only one brush mode paints per click/drag).
- In the Board Painter's 2D grid, a cell carrying any road-role bit renders in that role's distinct fill color, taking priority over its preset fill color (the preset bits are preserved in data even though hidden in this view).
- Add three new colors, with no Y-level restriction (unlike Camera Focus, road cells may be painted on any level) and no cap on how many Road/Road Spawn/Road End cells a board may have.
- Add `Road`, `RoadSpawn`, `RoadEnd` to the known-flags mask in `BoardAuthoringDocument.Validate()` so painted road cells no longer report as an "unknown flag" warning, plus two new soft (non-blocking) warnings: zero `RoadSpawn` cells on the board, and zero `RoadEnd` cells on the board.
- Extend the existing 3D scene visualization pipeline (`BoardGeometryPlan.cs`, `BoardGeometryPlanner.cs`, `BoardSceneSynchronizer.cs`) to generate one thin, collider-less, distinctly colored overlay per contiguous rectangle of Road / Road Spawn / Road End cells per level, using the same rectangle-decomposition algorithm already used for `SupportsPlacement`/`StaticBlocker` geometry.
- Add three new material assets (`Assets/Resources/Materials/Road.mat`, `RoadSpawn.mat`, `RoadEnd.mat`) with colors matching the Board Painter's road-role colors.
- Preserve backward compatibility: a board asset with no road-role bit set anywhere produces byte-for-byte the same painter rendering and scene geometry as before this feature.

## Non-goals

- No enemy pathing, no wave/spawn system, no runtime consumption of `Road`/`RoadSpawn`/`RoadEnd` anywhere outside the Editor authoring/visualization pipeline described here. `GridBoard`, `PlacementValidator`, and gameplay code are not touched.
- No enforced relationship between road-role bits and `SupportsPlacement`/`Buildable`/`StaticBlocker`. A cell may be both `Buildable` and `Road` at the data level; the Board Painter only warns softly (see "Data and runtime-state contracts") and never blocks or auto-clears either bit.
- No hard limit on the number of `RoadSpawn` or `RoadEnd` cells per board. Multiple spawns and multiple ends are allowed; the only feedback is the two soft warnings above when a board has zero of either.
- No restriction of road-role painting to the lowest playable level or any other level; unlike `CameraFocus`, the Road Brush is enabled on every level.
- No change to `BoardCameraFramingSolver`, `BoardCameraFocusRegionCalculator`, or any Camera Focus behavior. Road cells never affect camera framing.
- No change to the existing preset palette (`BoardPaintPreset`, its five presets, or `BoardAuthoringDocument.Paint`). The Road Brush is additive and independent, mirroring how the Camera Focus toggle is independent of the preset brush.
- No runtime (non-Editor) road-authoring API. Painting is an Editor-only workflow through `BoardPainterWindow`, exactly like every other board authoring surface today.

## Architecture and ownership

1. **Data storage.** `Road`, `RoadSpawn`, `RoadEnd` are three new bits on `BoardCellFlags`, reusing `BoardCellDefinition.flags`. No new serialized field or asset is introduced.
2. **Mutual exclusivity within the road-role group, orthogonality with everything else.** The three road-role bits behave as a second, independent "preset-like" group: painting one clears the other two road-role bits on that cell (so a cell is never simultaneously Road and Road Spawn, for example) but never touches `SupportsPlacement`, `Buildable`, `StaticBlocker`, or `CameraFocus` on that same cell. This mirrors decision 3 of the Camera Focus specification (`BoardCameraFocusRegion_Technical_Specification.md`) in spirit — an independent brush that does not route through `BoardAuthoringDocument.Paint`/`BoardPaintPresetUtility` — but adds mutual exclusivity *within* the new group, which Camera Focus (a single independent bit) did not need.
3. **Independent painter brush, three modes.** The Board Painter gets a "Road Brush" section, structurally similar to the existing Camera Focus toggle section but offering three colored mode buttons (Road / Spawn / End) instead of one toggle, plus the existing right-click-to-erase convention. Selecting a road mode deselects the preset brush and the Camera Focus brush for that stroke, and vice versa.
4. **Rendering priority in the 2D grid.** `BoardPainterWindow.DrawCells` fills a cell with its road-role color when any road-role bit is set, instead of its preset color. The Camera Focus corner accent (top-right) remains layered on top, unchanged. `BoardPaintPresetUtility.GetClosestPreset`'s existing flag-masking (already masks out `CameraFocus`) must also mask out the three road-role bits, and `DrawCells`' own separate mismatch check (line ~352, `(flags & ~BoardCellFlags.CameraFocus) != BoardPaintPresetUtility.GetFlags(preset)`) must do the same — otherwise every road-flagged preset cell incorrectly renders a "?" mismatch label. This is the same category of defect the Camera Focus specification's addendum found and fixed for its own bit; this specification requires masking three additional bits at both of those exact two sites.
5. **3D geometry reuses rectangle decomposition, not the single-region overlay pattern.** Unlike the Camera Focus region (one bounding rectangle for the whole board), Road cells can form an arbitrary scattered/linear path, so this specification extends `BoardGeometryPlanner`'s existing per-level rectangle-decomposition loop (`OrderedKinds`, `GetRequiredFlag`, `BuildRectangles`) with three additional `BoardGeometryKind` values, one per road-role bit, instead of adding a second `LowestBoardLevelBounds?`-style single-region field to `BoardGeometryPlan`.
6. **Road overlays carry no collider.** Because these cells have no gameplay function yet, their generated geometry must not introduce a `BoxCollider` (which could otherwise be mistaken for a physical obstacle by future raycast code, or double up with a `SupportsPlacement`/`StaticBlocker` collider already on the same cell). `BoardSceneSynchronizer.CreateRectangle` currently always adds an enabled `BoxCollider` to every generated cube; this specification requires a per-kind branch that skips (destroys) the collider for the three new kinds, mirroring the explicit collider-destruction already done for the Camera Focus overlay's `Quad` primitive.

### Component ownership

- **`BoardCellFlags`** (`Assets/Scripts/Board/Scripts/BoardCellDefinition.cs`) gains three members, continuing the existing bit sequence:

  ```csharp
  [Flags]
  public enum BoardCellFlags
  {
      None = 0,
      SupportsPlacement = 1 << 0,
      Buildable = 1 << 1,
      StaticBlocker = 1 << 2,
      CameraFocus = 1 << 3,
      Road = 1 << 4,
      RoadSpawn = 1 << 5,
      RoadEnd = 1 << 6
  }
  ```

  `BoardCellDefinition` gains three matching read-only properties, consistent with its existing `IsCameraFocus`:

  ```csharp
  public bool IsRoad => (flags & BoardCellFlags.Road) != 0;
  public bool IsRoadSpawn => (flags & BoardCellFlags.RoadSpawn) != 0;
  public bool IsRoadEnd => (flags & BoardCellFlags.RoadEnd) != 0;
  ```

- **A new `RoadPaintMode` enum and utility** own road-role-to-flags/label/color mapping, structurally parallel to `BoardPaintPreset`/`BoardPaintPresetUtility` but kept in its own new file, `Assets/Scripts/Board/Editor/BoardAuthoring/BoardRoadPaintMode.cs` (same assembly, `TowerDefense3D.GridPlacement.BoardAuthoring.Editor`), so the original preset file stays untouched and focused on its own five presets:

  ```csharp
  public enum RoadPaintMode
  {
      None,
      Road,
      Spawn,
      End
  }

  public static class RoadPaintModeUtility
  {
      public static BoardCellFlags GetFlags(RoadPaintMode mode) => mode switch
      {
          RoadPaintMode.Road => BoardCellFlags.Road,
          RoadPaintMode.Spawn => BoardCellFlags.RoadSpawn,
          RoadPaintMode.End => BoardCellFlags.RoadEnd,
          _ => BoardCellFlags.None
      };

      public static string GetLabel(RoadPaintMode mode) => mode switch
      {
          RoadPaintMode.Road => "Road",
          RoadPaintMode.Spawn => "Spawn",
          RoadPaintMode.End => "End",
          _ => "Erase"
      };

      public static Color GetColor(RoadPaintMode mode) => mode switch
      {
          RoadPaintMode.Road => new Color(0.55f, 0.40f, 0.20f, 1f),   // earthy brown
          RoadPaintMode.Spawn => new Color(0.20f, 0.55f, 0.95f, 1f),  // bright blue
          RoadPaintMode.End => new Color(0.90f, 0.20f, 0.55f, 1f),    // magenta/pink
          _ => Color.clear
      };

      internal const BoardCellFlags RoadRoleMask =
          BoardCellFlags.Road | BoardCellFlags.RoadSpawn | BoardCellFlags.RoadEnd;

      public static RoadPaintMode GetRoadRole(BoardCellFlags flags)
      {
          BoardCellFlags masked = flags & RoadRoleMask;
          if ((masked & BoardCellFlags.RoadSpawn) != 0) return RoadPaintMode.Spawn;
          if ((masked & BoardCellFlags.RoadEnd) != 0) return RoadPaintMode.End;
          if ((masked & BoardCellFlags.Road) != 0) return RoadPaintMode.Road;
          return RoadPaintMode.None;
      }
  }
  ```

  These three colors are chosen to be visually distinct from all five existing preset colors and from the Camera Focus accent (`0.15, 0.85, 0.95`); the implementer must double-check contrast against `Assets/Scripts/Board/Editor/BoardAuthoring/BoardPaintPreset.cs`'s `GetColor` values before finalizing and adjust if any pair reads as ambiguous at small cell sizes.

- **`BoardAuthoringDocument`** (`Assets/Scripts/Board/Editor/BoardAuthoring/BoardAuthoringDocument.cs`) gains one new public method, parallel to but independent of `Paint` and `SetCameraFocus`:

  ```csharp
  public void SetRoadRole(GridCell coordinate, RoadPaintMode mode)
  {
      BoardCellFlags current = GetFlags(coordinate);
      BoardCellFlags updated =
          (current & ~RoadPaintModeUtility.RoadRoleMask) | RoadPaintModeUtility.GetFlags(mode);

      if (updated == BoardCellFlags.None)
      {
          cells.Remove(coordinate);
      }
      else
      {
          cells[coordinate] = updated;
      }
  }
  ```

  This clears exactly the three road-role bits before OR-ing in the requested mode's bit (or none, for erase), preserving every other bit on that cell exactly as `SetCameraFocus` already preserves preset bits.

  `Validate()`'s `knownFlags` constant gains the three new bits:

  ```csharp
  const BoardCellFlags knownFlags = BoardCellFlags.SupportsPlacement
      | BoardCellFlags.Buildable
      | BoardCellFlags.StaticBlocker
      | BoardCellFlags.CameraFocus
      | BoardCellFlags.Road
      | BoardCellFlags.RoadSpawn
      | BoardCellFlags.RoadEnd;
  ```

  `Validate()` also gains two new soft warnings, counted during the existing per-cell loop, each added to `issues` only when its count is zero **and** at least one road-role cell exists at all (so an all-empty board with no road authored yet does not spam two warnings it hasn't earned):

  ```csharp
  if (roadCellCount > 0 && roadSpawnCount == 0)
  {
      issues.Add("Board has Road cells but no Road Spawn cell.");
  }

  if (roadCellCount > 0 && roadEndCount == 0)
  {
      issues.Add("Board has Road cells but no Road End cell.");
  }
  ```

  where `roadCellCount` counts cells with any of the three road-role bits set. A third soft warning notes overlap with `Buildable`, per the approved default of warning rather than blocking:

  ```csharp
  if ((pair.Value & RoadPaintModeUtility.RoadRoleMask) != 0
      && (pair.Value & BoardCellFlags.Buildable) != 0)
  {
      roadBuildableOverlapCount++;
  }
  // ...
  if (roadBuildableOverlapCount > 0)
  {
      issues.Add($"{roadBuildableOverlapCount} cells are both Road and Buildable.");
  }
  ```

  `Reload()` and `Commit()` require no changes: both already round-trip a cell's full `flags` value as a raw `int` through `SerializedProperty.intValue`, so the three new bits are preserved automatically.

- **`BoardPainterWindow`** (`Assets/Scripts/Board/Editor/BoardAuthoring/BoardPainterWindow.cs`) owns the Road Brush UI and input handling. See "Interaction flow" for the precise control flow. It also owns the two masking fixes called out in "Architecture and ownership" point 4.

- **`BoardGeometryPlan.cs` / `BoardGeometryPlanner.cs`** own extending the rectangle-decomposition pipeline with three new kinds. No new field on `BoardGeometryPlan` is needed (unlike Camera Focus's `FocusRegion` field) because road rectangles flow through the existing `Rectangles` list alongside placement/blocker rectangles, tagged by their `Kind`.

- **`BoardSceneSynchronizer.cs`** owns generating the actual `GameObject`s for road rectangles, using the three new materials, and owns skipping colliders for them.

## Data and runtime-state contracts

- `BoardCellFlags.Road = 1 << 4` (16), `RoadSpawn = 1 << 5` (32), `RoadEnd = 1 << 6` (64) — the next three free bits after `CameraFocus = 1 << 3`.
- The three road-role bits are mutually exclusive by construction: the only production code path that sets them (`BoardAuthoringDocument.SetRoadRole`) always clears all three before applying at most one. A cell found with more than one road-role bit set (e.g. from hand-edited YAML) is not defended against beyond `Validate()`'s general "unknown flag" style reporting; this is consistent with how the codebase already trusts its own single writer path for each flag group.
- Road-role bits compose freely with `SupportsPlacement`, `Buildable`, `StaticBlocker`, and `CameraFocus` at the data level. `Validate()` only warns (never blocks) on a `Road`/`RoadSpawn`/`RoadEnd` + `Buildable` combination.
- No count limit: any non-negative number of `RoadSpawn` or `RoadEnd` cells is valid data. Zero of either only produces a soft warning, never a validation failure or paint-blocking behavior.
- No Y-level restriction: `SetRoadRole` and the Road Brush operate on `selectedLevel` exactly like the preset brush, with no lowest-level gating (contrast with `TryGetLowestPlayableLevel`/`cameraFocusAllowed`, which stay Camera-Focus-only).
- `GridBoard`, `PlacementValidator`, and `BoardGeometryPlanner`'s existing `PlacementSurface`/`StaticBlocker` rectangle kinds only test `SupportsPlacement`/`Buildable`/`StaticBlocker` via bitwise `AND`; none enumerate exhaustively over `BoardCellFlags`. Adding three more bits is inert to them by construction, matching the same guarantee already established for `CameraFocus`.

## Interaction flow

### Authoring (Board Painter)

1. The author opens the Board Painter (`Tools > Tower Defense > Board Painter`) as today.
2. Below the existing Camera Focus Brush section, a new "Road Brush" section shows three colored mode buttons (`Road`, `Spawn`, `End`) using `RoadPaintModeUtility.GetColor`/`GetLabel`, plus the implicit erase-via-right-click convention already used everywhere else in this window. Selecting a mode sets a `roadBrushActive = true` flag and a `selectedRoadMode` field, and clears `cameraFocusBrushActive` and deselects the preset palette's highlighted button (mirroring how selecting a preset already clears `cameraFocusBrushActive` at `BoardPainterWindow.cs:220`). Selecting a preset or the Camera Focus toggle must symmetrically clear `roadBrushActive`.
3. `HandleGridInput` gains a third branch: when `roadBrushActive`, left-click/drag calls a new brush-size-aware helper and right-click/drag calls it with `RoadPaintMode.None` (erase), parallel to how `useCameraFocusBrush` is already threaded through the same method:

   ```csharp
   internal static bool PaintRoadBrush(
       BoardAuthoringDocument targetDocument,
       GridCell center,
       int size,
       RoadPaintMode mode)
   ```

   mirroring `PaintCameraFocusBrush`'s radius/clipping loop but calling `targetDocument.SetRoadRole(coordinate, mode)` per cell.
4. Stroke lifecycle (`strokeActive`, `strokeChanged`, `lastPaintedCell`, `CommitStroke`) is reused unchanged; `CommitStroke` uses a distinct undo name, e.g. `"Paint Road Cells"`, when the active brush is the Road Brush (the existing `strokeIsCameraFocus` bool becomes, or is joined by, an enum/second bool capturing which of the three brushes was active for that stroke — implementer's choice, as long as the three undo names stay distinct: `"Paint Board Cells"`, `"Toggle Camera Focus"`, `"Paint Road Cells"`).
5. `DrawCells` renders a cell's road-role color (via `RoadPaintModeUtility.GetColor(RoadPaintModeUtility.GetRoadRole(flags))`) in place of its preset color whenever `GetRoadRole(flags) != RoadPaintMode.None`; otherwise it renders the preset color exactly as today. The Camera Focus corner accent and the `StaticBlocker` "X" label continue to render on top, unchanged, in either case. Both `BoardPaintPresetUtility.GetClosestPreset`'s internal mask and `DrawCells`' own mismatch check (`BoardPainterWindow.cs:352`) must additionally mask out `RoadPaintModeUtility.RoadRoleMask`, or every road-flagged preset cell will incorrectly show the "?" mismatch label.
6. `Validate()` continues to run on every `OnGUI` pass; with the known-flags mask and the two/three new soft warnings added, road cells never report as "unknown flag", and boards with Road cells but no Spawn/End surface an explicit, non-blocking reminder.

### Visualizing (3D scene, Editor)

1. `BoardSceneSynchronizer.Synchronize` calls `BoardGeometryPlanner.Create(board)` exactly as today; `Create` now also decomposes `Road`/`RoadSpawn`/`RoadEnd` cells into rectangles via the same per-level, per-kind loop already used for `PlacementSurface`/`StaticBlocker`, by adding three entries to `OrderedKinds` and three branches to `GetRequiredFlag`.
2. `BuildSignature` already incorporates every rectangle's `Kind`/`X`/`Y`/`Z`/`Width`/`Depth` generically, so no change is needed there beyond the enum growing — road rectangles participate in resync-detection automatically.
3. `BoardSceneSynchronizer.CreateRectangle` gains a per-kind branch: for the three new kinds, it uses the corresponding new material (`Road.mat`/`RoadSpawn.mat`/`RoadEnd.mat`), a thin slab like the existing `PlacementSurface` case (`SurfaceThickness`-derived height, positioned just above the level's ground plane, e.g. reusing the same `CameraFocusOverlayLift`-style small offset to avoid z-fighting with any placement-surface slab on the same cell), and explicitly destroys the generated `BoxCollider` immediately after creation (mirroring `CreateCameraFocusRegion`'s existing `Collider` destruction for its `Quad`).
4. `HasMatchingGeometry`'s per-child validation loop is extended so that, for the three new kinds, it requires a `MeshRenderer` and explicitly requires **no** `Collider` (the inverse of its existing requirement for `PlacementSurface`/`StaticBlocker` children), matching the existing special case already carved out for the Camera Focus overlay child.
5. Visibility of the new road overlays follows `BoardDefinition.VisualizeInScene` exactly like every other generated child, through the existing unchanged `ApplyComponentState` method.
6. When no cell of a given road role exists, `BuildRectangles` naturally produces zero rectangles of that kind (the existing scan-and-skip behavior already used for `PlacementSurface`/`StaticBlocker`) — no special "no rectangles" branch is required, unlike the single-region Camera Focus overlay which needed an explicit nullable field.

## Folder and assembly boundaries

- `BoardCellFlags`/`BoardCellDefinition` changes stay in `Assets/Scripts/Board/Scripts/BoardCellDefinition.cs`, assembly `TowerDefense3D.GridPlacement.Runtime`.
- The new `BoardRoadPaintMode.cs` is added at `Assets/Scripts/Board/Editor/BoardAuthoring/BoardRoadPaintMode.cs`, assembly `TowerDefense3D.GridPlacement.BoardAuthoring.Editor`, alongside `BoardPaintPreset.cs`.
- `BoardAuthoringDocument.cs`, `BoardPainterWindow.cs`, `BoardGeometryPlan.cs`, `BoardGeometryPlanner.cs`, `BoardSceneSynchronizer.cs` changes all stay in their existing files inside `Assets/Scripts/Board/Editor/BoardAuthoring/`, same assembly. No new Editor assembly is introduced.
- New materials go to `Assets/Resources/Materials/` (`Road.mat`, `RoadSpawn.mat`, `RoadEnd.mat`), matching where `BoardSurface.mat`, `Blocker.mat`, and `CameraFocusRegion.mat` already live, per this repository's shared-asset-root convention (`Assets/Resources/<Category>/`).
- New tests are added inside the existing `Assets/Scripts/Board/Tests/EditMode/` folder as new methods/files; no new test assembly is introduced. No `PlayMode` test is required (see "Verification plan") since this feature has no runtime behavior.
- This keeps the feature entirely inside the existing `GridPlacement` feature root; no cross-feature dependency is introduced.

## Serialized integration

- `BoardDefinition.cells` (`BoardCellDefinition[]`) is the only serialized surface touched, and only through its existing `flags` field's numeric range. No field is added to `BoardDefinition`, `BoardCellDefinition`, or any other asset.
- `BoardAuthoringDocument.Reload()`/`Commit()` already round-trip a cell's full `flags` value as a raw `int`, so the three new bits round-trip automatically once the enum defines them — no code change to either method.
- Existing serialized `BoardDefinition` assets that never used bits 4–6 deserialize with all three road-role bits unset on every cell, which is exactly the "no road painted" case that produces zero road rectangles and identical 2D/3D rendering to before this feature. No migration step or asset upgrade pass is required.
- `Road.mat`, `RoadSpawn.mat`, `RoadEnd.mat` are new, version-controlled project assets, created once and referenced by path (`GroundMaterialPath`-style constants) from `BoardSceneSynchronizer`, exactly like the three existing materials it already loads.
- Undo/dirty-state handling for the new brush reuses `BoardAuthoringDocument.Commit(string undoName)`, `Undo.RegisterCompleteObjectUndo`, and `EditorUtility.SetDirty`, exactly as the existing preset and Camera Focus brushes already do.
- `BoardChangeScheduler.Queue(Asset)` (invoked at the end of `Commit()`) is unaffected; a road-role-only commit is just another committed change to the same `cells` array, and the existing resync flow already re-synchronizes scene geometry after any committed change.

## Compatibility and migration constraints

- Backward compatibility is required and structurally guaranteed: any `BoardDefinition` with zero road-role bits set produces byte-identical Board Painter rendering and identical `BoardGeometryPlan.Rectangles` output to the pre-feature code, because `BuildRectangles` finds zero cells matching the three new required flags and therefore emits zero road rectangles.
- `Level_001_Board.asset` and `Level_002_Board.asset` are not modified by this feature's implementation; both continue to render with zero road cells until a designer paints some.
- `GridBoard`, `PlacementValidator` (`Assets/Scripts/Board/Scripts/GridBoard.cs`, `Assets/Scripts/Placement/Scripts/PlacementValidator.cs`) only test `SupportsPlacement`/`Buildable`/`StaticBlocker` via bitwise `AND`; they require no change and are unaffected by the three new bits.
- `BoardPaintPresetUtility.GetFlags` continues to never return any road-role bit for any preset; presets and road roles remain orthogonal, so no existing preset's meaning changes.
- No renamed, removed, or reordered public API on `BoardCameraFramingSolver`, `BoardCameraFocusRegionCalculator`, or any Camera Focus type; this feature does not touch camera framing at all.

## Verification plan

1. **EditMode test — `BoardAuthoringDocument` road-role behavior.** New file `Assets/Scripts/Board/Tests/EditMode/BoardAuthoringDocumentRoadTests.cs`, mirroring the structure of `BoardAuthoringDocumentCameraFocusTests.cs`, covering: `SetRoadRole` setting exactly one of the three bits and clearing the other two on repeated calls with different modes on the same cell; `SetRoadRole` preserving unrelated preset/`CameraFocus` bits on the same cell; `SetRoadRole(coordinate, RoadPaintMode.None)` removing the cell entry when no other bits remain; `Validate()` reporting zero "unknown flag" issues for cells combining road-role bits with known preset/CameraFocus bits; the two soft warnings appearing only when road cells exist and the corresponding Spawn/End count is zero, and disappearing once at least one exists; the Buildable-overlap soft warning appearing only when a cell combines a road-role bit with `Buildable`; and `Commit()`/`Reload()` round-tripping all three bits through `SerializedObject` with no code change required in either method.
2. **EditMode test — Board Painter brush helpers.** Extend or add alongside `BoardAuthoringTests.cs`: `BoardPainterWindow.PaintRoadBrush`'s brush-size and edge-clipping behavior (mirroring the existing `PaintBrush`/`PaintCameraFocusBrush` coverage) for all three modes and erase; `RoadPaintModeUtility.GetRoadRole` returning the correct mode for each bit combination and `None` when no road-role bit is set.
3. **EditMode test — geometry planner/synchronizer.** Extend `Assets/Scripts/Board/Tests/EditMode/BoardSceneAuthoringTests.cs` (the file the Camera Focus addendum already extended) covering: `BoardGeometryPlanner.Create` emitting one rectangle per contiguous run of each road role per level, using the same width/depth-merging behavior already proven for `PlacementSurface`/`StaticBlocker`; zero rectangles emitted for a board with no road-role cells; `BoardSceneSynchronizer` generating road overlay children with a `MeshRenderer` and zero `Collider` components at the expected transform; overlay `MeshRenderer.enabled` following `VisualizeInScene`; and overlays being removed/regenerated correctly when road cells are cleared or the plan's signature otherwise changes (the existing full-regenerate-on-signature-change flow, unchanged, now also covers road content).
4. **Live-Editor verification.** After (1)–(3) are implemented and green: open `Tools > Tower Defense > Board Painter` on `Level_001_Board.asset` and `Level_002_Board.asset`, confirm no new "unknown flag" warnings appear and the existing "Active cells"/dimensions status is unchanged; paint a short Road strip with one Spawn and one End cell on a scratch board (not committed to either shipping asset unless the project owner asks for it), confirm the three colors render distinctly in the 2D grid, confirm the corresponding overlays appear in the live scene with the correct colors and no collider, and confirm erasing removes them; confirm the Unity Console shows zero new compile errors/warnings.

Implementation is expected to be split across dependency-ordered Beads matching the four steps above (data model → authoring document → painter UI → geometry/scene synchronizer → tests → live verification), tracked in `bd`.

## Risks

- The Road Brush is a third mutually-exclusive brush mode layered onto a window that already juggles two (preset vs. Camera Focus). If the three modes are implemented as independent booleans instead of a single exclusive selection, an author could end up with more than one brush "active" at once and an ambiguous click target. `roadBrushActive`/`cameraFocusBrushActive`/preset-selection must be kept mutually exclusive by every code path that sets any one of them.
- `GetClosestPreset`'s exact-equality matching and `DrawCells`' own separate mismatch check both need the same new mask applied in two different places; missing either one reintroduces the "?" mismatch label defect the Camera Focus addendum already had to fix once for its own bit. Both sites must be updated together.
- Choosing colors that read as distinct at the smallest zoom level (`MinimumCellSize = 18f`) matters more with eight total colors in play (five presets, Camera Focus accent, three road roles) than it did with seven; a human visual check during live-Editor verification should confirm no two colors are hard to tell apart at minimum zoom, and the exact RGB values above may be adjusted at implementation time if so.
- Skipping the `BoxCollider` on road-rectangle children changes `HasMatchingGeometry`'s per-child expectations; if that method's road-kind branch is not updated in the same change as `CreateRectangle`'s collider-skip branch, every synchronize pass will detect a false "mismatch" and endlessly regenerate road geometry (or vice versa, silently accept stale geometry). Both must ship together and be covered by the EditMode test in item 3 above.

## Deferred work

- No enemy pathing, spawn/wave logic, or any runtime consumption of Road/Spawn/End cells. This is explicitly the next feature layered on top of this one, not part of this specification.
- No UI affordance to list/count/jump to authored Spawn/End cells beyond the two soft `Validate()` warnings; richer authoring ergonomics (e.g. a "jump to next Spawn" button) are deferred until there is a concrete need.
- No enforcement or auto-clearing of `Buildable`/`StaticBlocker` overlap with road roles; the soft warning is considered sufficient for this iteration, per the approved default.
- No programmatic (non-Editor) API for setting road roles at runtime; this feature is an Editor-authoring-time and visualization-time concern only, matching every other board-authoring feature in this codebase.

## Implementation status

Implementation completed and verified on 18 August 2026, through the approved Beads graph (`TowerDefense3D-udff`, `-7wyb`, `-98hi`, `-pdh5`, `-xlvq`, `-kdb2`), with no approved-scope deviation. Two implementation details the specification left to the implementer's judgment were resolved as follows: the three generated overlay children are named `Road Area`, `Road Spawn Area`, `Road End Area` (mirroring the existing `Placeable Area`/`Blocked Area` convention), and the per-stroke brush-kind tracking needed for `CommitStroke`'s three distinct undo names was implemented as a second `strokeIsRoadBrush` bool alongside the existing `strokeIsCameraFocus` bool (the "joined by a second bool" option the specification explicitly allowed). `BoardPainterWindow.SetBoard` was additionally extended to reset `roadBrushActive = false` when switching board assets, matching the existing reset of `cameraFocusBrushActive` on the same line; this is a defensive consistency addition, not a scope change.

### Implemented files

- `Assets/Scripts/Board/Scripts/BoardCellDefinition.cs` — adds `BoardCellFlags.Road = 1 << 4`, `RoadSpawn = 1 << 5`, `RoadEnd = 1 << 6`, and the `IsRoad`/`IsRoadSpawn`/`IsRoadEnd` accessors.
- `Assets/Scripts/Board/Editor/BoardAuthoring/BoardRoadPaintMode.cs` (new) — `RoadPaintMode` enum and `RoadPaintModeUtility` (`GetFlags`/`GetLabel`/`GetColor`/`GetRoadRole`/`RoadRoleMask`), exactly matching the specification's contract.
- `Assets/Scripts/Board/Editor/BoardAuthoring/BoardAuthoringDocument.cs` — adds `SetRoadRole` (clears all three road-role bits then ORs in at most one, never touching preset/`CameraFocus` bits) and extends `Validate()`'s `knownFlags` mask plus the two Spawn/End soft warnings and the Road+Buildable overlap soft warning.
- `Assets/Scripts/Board/Editor/BoardAuthoring/BoardPaintPreset.cs` — masks `RoadPaintModeUtility.RoadRoleMask` (in addition to the existing `CameraFocus` mask) out of `GetClosestPreset`'s equality comparison.
- `Assets/Scripts/Board/Editor/BoardAuthoring/BoardPainterWindow.cs` — adds the independent, mutually-exclusive Road Brush section (three mode buttons + right-click erase), `PaintRoadBrush`, road-role-aware fill color in `DrawCells` (taking priority over the preset color), the matching second mask fix on `DrawCells`' own mismatch check, and the distinct `"Paint Road Cells"` undo name in `CommitStroke`.
- `Assets/Scripts/Board/Editor/BoardAuthoring/BoardGeometryPlan.cs` — adds `BoardGeometryKind.RoadSurface`/`RoadSpawnSurface`/`RoadEndSurface`.
- `Assets/Scripts/Board/Editor/BoardAuthoring/BoardGeometryPlanner.cs` — adds the three new kinds to `OrderedKinds` and their flag mapping to `GetRequiredFlag`, reusing the existing rectangle-decomposition loop unchanged.
- `Assets/Scripts/Board/Editor/BoardAuthoring/BoardSceneSynchronizer.cs` — loads the three new materials, extends `CreateRectangle` with a thin-slab-above-ground branch for the three new kinds (lifted by the existing `CameraFocusOverlayLift` offset to avoid z-fighting with any placement-surface slab on the same cell) that explicitly destroys the generated `BoxCollider`, and extends `HasMatchingGeometry` to require a `MeshRenderer` and **no** `Collider` for those three kinds (the inverse of its existing `BoxCollider`-required check for every other kind).
- `Assets/Resources/Materials/Road.mat`, `Assets/Resources/Materials/RoadSpawn.mat`, `Assets/Resources/Materials/RoadEnd.mat` (new) — URP/Lit, Transparent surface, colors matching `RoadPaintModeUtility.GetColor`, mirroring `Blocker.mat`/`BoardSurface.mat`'s existing transparent-material settings.
- `Assets/Scripts/Board/Tests/EditMode/BoardAuthoringDocumentRoadTests.cs` (new) — 12 tests on `SetRoadRole` and the three `Validate()` road-related warnings.
- `Assets/Scripts/Board/Tests/EditMode/BoardAuthoringTests.cs` — 3 new tests on `PaintRoadBrush` and `RoadPaintModeUtility.GetRoadRole`.
- `Assets/Scripts/Board/Tests/EditMode/BoardSceneAuthoringTests.cs` — 7 new tests on `BoardGeometryPlanner`'s road-role rectangles and `BoardSceneSynchronizer`'s road overlays (collider-less generation, material/name distinctness, `VisualizeInScene` following, resync reuse, and removal on erase).

### Verification evidence

- Unity `6000.3.21f1` compiled the implementation with zero new Console errors or warnings at every implementation step (`AssetDatabase.Refresh` + `Unity_ReadConsole` after each Bead), confirmed live via Unity MCP against the connected Editor instance.
- Full Grid Placement EditMode suite: **78 of 78 passed** (56 pre-existing + 22 new: 12 `BoardAuthoringDocumentRoadTests` + 3 `BoardAuthoringTests` + 7 `BoardSceneAuthoringTests`), run twice via `UnityEditor.TestTools.TestRunner.Api.TestRunnerApi` against the `TowerDefense3D.GridPlacement.EditModeTests` assembly — once after the test Bead landed and once again as the final regression check for this verification pass. No pre-existing test was modified or broken.
- `Level_001_Board.asset` and `Level_002_Board.asset` were opened through `Tools > Tower Defense > Board Painter` (`BoardPainterWindow.Open`) with no exception; `BoardAuthoringDocument.Validate()` against both reported **zero issues** (no "unknown flag" warning, no road soft warning, since neither board has any road-role cell), and `ActiveCellCount`/`Dimensions` were unchanged (4800 cells, 80x60x8, on both) — confirming this feature is inert on the two shipping boards, per the backward-compatibility guarantee.
- A live, in-memory scratch `BoardDefinition` (created in a temporary additive Editor scene, never written to disk) was painted with a 3-cell Road run, one Spawn cell, and one End cell, then committed. The real, unmodified `BoardChangeScheduler` → `BoardSceneSynchronizer.Synchronize` pipeline generated exactly three overlay children — `Road Area` (merged into one width-3 rectangle), `Road Spawn Area`, `Road End Area` — each with a `MeshRenderer` using the correct new material (`Road`/`RoadSpawn`/`RoadEnd`) and **zero** `Collider` components, positioned just above the level's ground plane. A `Unity_SceneView_CaptureMultiAngleSceneView` screenshot confirmed the three overlays render in three visually distinct colors (brown, blue, magenta) in both the isometric and top-down views. Erasing all five cells and re-committing removed all three overlay children (root child count returned to 0), confirming the resync/removal path.
- The scratch scene, presenter `GameObject`, and scratch `BoardDefinition` were destroyed and the temporary scene closed at the end of verification; `git status` after cleanup shows no stray files and no change to either shipping board asset.
- Console remained clean throughout (only a pre-existing, unrelated warning from the Unity AI Assistant/MCP package's own account-API check appeared; it is unconnected to this feature and predates this implementation).

### Known limitations

- As with the Camera Focus Region feature before it, no available tool can drive Unity's custom IMGUI `BoardPainterWindow` at the mouse-click/pixel level (Unity MCP's capture tools cover only Scene/Game camera views and cannot screenshot an arbitrary `EditorWindow`'s IMGUI content, and `System.Reflection` is blocked inside the sandboxed command-execution tool). The Road Brush's UI wiring (button mutual exclusivity, right-click erase, undo naming) was verified by compiling and running the real `BoardPainterWindow` code with no exceptions and by calling its exact underlying production methods (`SetRoadRole`, `PaintRoadBrush`, `RoadPaintModeUtility.GetRoadRole`) directly, both in EditMode tests and against a live `BoardAuthoringDocument`. A brief human click-through of the Board Painter's new Road Brush section is recommended before it is used to author a shipping level, though it exercises no code path beyond what was already verified here.
- Color-contrast review against the five existing preset colors and the Camera Focus accent was done numerically (comparing the approved RGB values) and visually via the Scene View screenshot of the 3D overlays, not via a screenshot of the 2D painter grid itself (blocked by the same IMGUI-capture limitation above); the approved colors were used unchanged, as they read as clearly distinct in both checks.

## Addendum: Road/Spawn/End cells are not buildable (18 August 2026)

Added after initial implementation, at the project owner's explicit request, following the same approval-then-implement discipline as the rest of this specification. This addendum **supersedes** the original "no enforced relationship" decision recorded in "Non-goals" (second bullet), "Compatibility and migration constraints" (third bullet, "`GridBoard`, `PlacementValidator` ... require no change and are unaffected"), and "Deferred work" (third bullet, "No enforcement or auto-clearing of `Buildable`/`StaticBlocker` overlap with road roles"). Those bullets are left in place above as a historical record of the originally approved scope; this addendum is now authoritative wherever it conflicts with them.

### Approved scope (addendum)

- A cell carrying any of `Road`, `RoadSpawn`, or `RoadEnd` is never buildable at runtime, **regardless of whether its `Buildable` bit is also set**. A tower footprint that overlaps such a cell on its base level fails placement with `PlacementFailureFlags.NotBuildable`, exactly the same failure a plain non-`Buildable` cell already produces.
- This is enforced at the single existing runtime enforcement point, `PlacementValidator.Evaluate`, not by auto-clearing the `Buildable` bit during authoring. Authoring-time data (`BoardCellDefinition.flags`) is unchanged by this addendum: a cell can still be painted with both `Buildable` and a road role, and the Board Painter's existing soft "cells are both Road and Buildable" warning (added in the base specification) continues to fire for that case — it is now a stronger hint that the `Buildable` bit is misleading, not just informational.
- No change to the Board Painter UI, the 2D grid rendering, or the 3D scene overlays; those already show a cell's road-role color in preference to its preset color, so this addendum changes runtime behavior to match what a board author already sees.

### Non-goals (addendum)

- No auto-clearing of `Buildable` when `SetRoadRole` paints a road role, and no auto-clearing of a road role when `Paint` paints a `Buildable`-implying preset over an existing road cell. Both bits may coexist in data; only placement evaluation treats the combination as not buildable.
- No change to `GridBoard.IsBuildable(GridCell)`, which continues to reflect only the raw `Buildable` bit (it has no callers outside one existing regression test and is not part of the gameplay placement path); only `PlacementValidator.Evaluate`'s `PlacementFailureFlags` output changes.
- No new `PlacementFailureFlags` member. Road/Spawn/End overlap reuses the existing `NotBuildable` flag rather than introducing a distinct failure reason, since from the placer's perspective the outcome is identical ("you cannot build here").

### Architecture and ownership (addendum)

- `PlacementValidator` (`Assets/Scripts/Placement/Scripts/PlacementValidator.cs`) gains a private constant `RoadRoleFlags = BoardCellFlags.Road | BoardCellFlags.RoadSpawn | BoardCellFlags.RoadEnd` and one additional check inside `Evaluate`'s existing base-footprint loop, alongside the existing `SupportsPlacement`/`Buildable` checks:

  ```csharp
  if ((baseFlags & RoadRoleFlags) != 0)
  {
      failures |= PlacementFailureFlags.NotBuildable;
  }
  ```

  This lives in the Runtime assembly (`TowerDefense3D.GridPlacement.Runtime`), the same assembly `BoardCellFlags` itself lives in, so no Editor-only type (e.g. `RoadPaintModeUtility.RoadRoleMask`, which lives in the Editor assembly and cannot be referenced from Runtime code) is referenced; the mask is redeclared locally in terms of the three public `BoardCellFlags` bits.
- No change to `GridBoard`, `GridOccupancy`, `PlacementResult`, or `PlacementFailureFlags`.

### Verification plan (addendum)

- EditMode tests added to `Assets/Scripts/Placement/Tests/EditMode/GridPlacementRulesTests.cs`: `Validator_TreatsRoadRoleCellsAsNotBuildableEvenWhenBuildableIsSet` (parameterized over `Road`/`RoadSpawn`/`RoadEnd`) proves a `SupportsPlacement | Buildable | <road role>` cell fails with `NotBuildable`; `Validator_AllowsPlacementOnBuildableCellWithNoRoadRole` is an explicit regression proof that a plain `SupportsPlacement | Buildable` cell with no road role still succeeds, so this addendum only narrows placement, it does not also narrow it for unrelated cells.
- Full Grid Placement EditMode suite must be re-run and confirmed green after this change, alongside the pre-existing `Validator_ChecksEveryBaseSupportAndFullBlockedVolume` and `Validator_RejectsMissingSupportWithoutMutatingOccupancy` tests, which exercise `PlacementValidator` without any road-role bit and must continue to pass unmodified.
- Live-Editor verification: none of this addendum's code touches Editor-only authoring or visualization paths already verified above; a functional check (attempting tower placement on a Road/Spawn/End cell in Play Mode) is deferred until this project has a placement UI/flow exercised against a board with road cells, since none of the two shipping boards currently has any.

### Verification evidence (addendum)

Tracked through `TowerDefense3D-vyhq` (closed). Unity `6000.3.21f1` compiled with zero new Console errors/warnings (only the pre-existing, unrelated Unity AI Assistant/MCP account-API warning remained). The full `TowerDefense3D.GridPlacement.EditModeTests` suite was run via the project's own `Tools > Tower Defense > Tests > Run Grid Placement EditMode` bridge (`GridPlacementTestRunnerBridge.cs`) and passed **82 of 82** (78 pre-existing/prior-addendum + 4 new: 3 `TestCase` variants of `Validator_TreatsRoadRoleCellsAsNotBuildableEvenWhenBuildableIsSet` plus `Validator_AllowsPlacementOnBuildableCellWithNoRoadRole`), reproduced consistently across three separate runs in the same session. The pre-existing `Validator_ChecksEveryBaseSupportAndFullBlockedVolume` and `Validator_RejectsMissingSupportWithoutMutatingOccupancy` tests passed unmodified, confirming this addendum narrows placement only for road-role cells. No code fix was needed beyond the change as originally written.

## Addendum: remove the Road+Buildable overlap soft warning (18 August 2026)

Added after the "not buildable" addendum above, at the project owner's explicit request. This addendum **supersedes** the sentence in that addendum's "Approved scope" bullet stating the Board Painter's "cells are both Road and Buildable" warning "continues to fire" and "is now a stronger hint" — the project owner found the warning confusing once placement is unconditionally blocked regardless of the `Buildable` bit, and asked for it to be removed rather than kept as a hint.

### Approved scope (addendum)

- `BoardAuthoringDocument.Validate()` no longer reports a "N cells are both Road and Buildable" warning. The two Spawn/End soft warnings (zero `RoadSpawn` cells, zero `RoadEnd` cells) are unaffected and still fire exactly as specified in the base specification.
- No change to the underlying data model or to `PlacementValidator`: a cell may still carry both `Buildable` and a road-role bit in its serialized `flags`, and `PlacementValidator.Evaluate` still unconditionally treats any road-role bit as `NotBuildable` regardless of the `Buildable` bit, exactly as the prior addendum established. Only the authoring-time Board Painter hint is removed; the data model and runtime enforcement are untouched.

### Non-goals (addendum)

- No auto-clearing of the `Buildable` bit when a road role is painted (that alternative was offered and explicitly declined in favor of simply removing the warning).
- No new or renamed `Validate()` warning to replace it.

### Architecture and ownership (addendum)

- `BoardAuthoringDocument.Validate()` (`Assets/Scripts/Board/Editor/BoardAuthoring/BoardAuthoringDocument.cs`) drops the `roadBuildableOverlapCount` counter, its accumulation inside the per-cell loop, and the `issues.Add(...)` call that reported it.

### Verification plan (addendum) and evidence

- `Assets/Scripts/Board/Tests/EditMode/BoardAuthoringDocumentRoadTests.cs`: `Validate_RoadAndBuildableOverlap_ReportsOverlapWarning` (which asserted the now-removed warning's presence) was renamed to `Validate_RoadAndBuildableOverlap_ReportsNoOverlapWarning` and now asserts no issue mentions "Buildable" for a cell combining `Road` and `Buildable`, alongside the pre-existing `Validate_RoadWithoutBuildableOverlap_ReportsNoOverlapWarning` regression test.
- Unity `6000.3.21f1` compiled cleanly (`AssetDatabase.Refresh`, zero new Console errors/warnings) and the full `TowerDefense3D.GridPlacement.EditModeTests` suite passed with the updated test count, confirmed via `Tools > Tower Defense > Tests > Run Grid Placement EditMode`.

## Addendum: production straight-road prefab visualization (19 August 2026)

**Status:** Approved

**Approved:** 19 August 2026

**Implementation status:** Complete

**Tracking Bead:** `TowerDefense3D-lux1`

This addendum supersedes only the base specification's treatment of the generated Road / Road Spawn / Road End geometry as the final scene artwork. The existing colored rectangle overlays remain as authoring/debug geometry controlled by `BoardDefinition.VisualizeInScene`; a separate, always-visible production road-art layer is added from the already-authored road-role cells.

### Approved scope

- One eligible painted `Road`, `RoadSpawn`, or `RoadEnd` cell generates one `RoadStraightCell.prefab` instance. The prefab root contains exactly two quad children representing the two road edges. One edge is flipped 180 degrees so the opaque texture edges meet at the road center and the transparent texture edges fade outward.
- The four manually placed quads currently under `Grid Placement/Visuals` in `Level_001` are two sample straight segments, not one four-quad prefab. They are removed only after their generated replacements are visually verified.
- The production prefab uses the exact existing material asset `Assets/Resources/Textures/New Material.mat` and its current seamless RGBA texture. The scene-only `New Material (Instance)` currently referenced by the four sample quads is not retained.
- Material instancing is prohibited. Production code must never access `Renderer.material`, construct `new Material`, or clone a material. The prefab's child renderers reference the original asset through `sharedMaterial`; the synchronizer instantiates only the prefab and never replaces or modifies its materials.
- Production road art is always visible. `BoardDefinition.VisualizeInScene` continues to control only the colored generated debug geometry (`Road Area`, `Road Spawn Area`, `Road End Area`, placement surfaces, blockers, and Camera Focus overlay).
- `Road`, `RoadSpawn`, and `RoadEnd` use the same straight-road prefab and material. Their distinct roles remain preserved in board data and the Board Painter's colored grid/debug overlays; no production-art cell is left empty solely because its role is Spawn or End.

### Straight topology contract

Road-neighbor checks include all three road roles and are restricted to the same authored Y level.

- A cell with any X neighbor (left or right) and no Z neighbor uses the X axis.
- A cell with any Z neighbor (forward or backward) and no X neighbor uses the Z axis.
- A cell with no road neighbor is an isolated cell and uses the X axis so the author receives immediate visual feedback after the first paint action.
- A cell with at least one X neighbor and at least one Z neighbor is a corner, T-junction, or cross-junction for this scope. It generates no production visual and emits no warning.
- Results are ordered deterministically by Y, then Z, then X. Straight orientation is represented by root Y rotation: 0 degrees for X and 90 degrees for Z.

`Level_001_Board.asset` currently contains 28 road-role cells: 16 X-straight cells, 11 Z-straight cells, and one corner at `(34, 28, 0)` that intentionally produces no production visual under this contract. The expected production result is therefore 27 prefab instances and 54 child quad renderers.

### Architecture and ownership

- `BoardGeometryPlan.cs` gains an immutable per-cell road-visual descriptor containing the cell coordinate, road role, and X/Z axis, plus a deterministically ordered descriptor list and a road-visual signature independent of `VisualizeInScene`.
- `BoardGeometryPlanner.Create` continues producing the existing merged rectangles for colored debug overlays and additionally derives the straight-road descriptors from the same bounds-filtered per-cell flag array. Invalid corner/junction descriptors are omitted silently.
- `BoardScenePresenter.cs` owns serialized references to the `RoadStraightCell` prefab and a dedicated generated production-road root/signature. It continues applying `VisualizeInScene` only to the existing `Board Visualization` root.
- `BoardSceneSynchronizer.cs` keeps the existing `Board Visualization` root and debug-rectangle synchronization intact, and independently synchronizes a sibling root named `Generated Road Visuals`. This root is the only root the production-road synchronizer may rebuild; it must never reuse or clear the manual `Grid Placement/Visuals` root.
- Production instances are created through `PrefabUtility.InstantiatePrefab`, parented under `Generated Road Visuals`, positioned at the board cell center, rotated from the descriptor axis, and uniformly scaled by `BoardDefinition.CellSize`. The prefab contains the approved surface offset and edge geometry, so the synchronizer does not know about individual child transforms or materials.
- The production-road signature includes ordered descriptors, cell size, height unit, a generation-contract version, and the serialized prefab asset/dependency identity. Repeated synchronization with the same signature and valid hierarchy must preserve existing instance IDs.

### Prefab contract

```text
RoadStraightCell
├── LeftEdge   (MeshFilter, MeshRenderer)
└── RightEdge  (MeshFilter, MeshRenderer; flipped 180 degrees)
```

- Both children use Unity's quad mesh and the exact shared material asset `Assets/Resources/Textures/New Material.mat`.
- Both children have no `Collider`, cast no shadows, and receive no shadows.
- For the prefab's canonical X orientation, each child occupies one half of the two-unit-wide visual road: local Z positions are `+0.5` and `-0.5`; both remain one cell unit long along local X. Rotating the prefab root by 90 degrees produces the canonical Z orientation.
- The child surface offset preserves the verified sample placement approximately 0.01 world units above the current `Level_001` ground at `CellSize = 1`, preventing z-fighting without adding a scene-specific material or material property block.

### Serialized integration and migration

- New asset: `Assets/Resources/Prefabs/RoadStraightCell.prefab` plus its Unity-generated `.meta` file.
- `Level_001.unity` and `Level_002.unity` receive a serialized `straightRoadPrefab` reference on their existing `BoardScenePresenter` so future Board Painter road edits use the same art.
- Existing serialized references to `generatedRoot` / `generatedSignature` remain the debug-geometry state. New serialized production-road root/signature fields are separate and hidden in the Inspector.
- Migration deletes only the four exact sample objects named `Quad`, `Quad (1)`, `Quad (2)`, and `Quad (3)` under `Grid Placement/Visuals`, and only after the generated road has been inspected. No Frog, Ground, rock, bush, twig, or other manual visual is reparented, regenerated, or removed.
- The old `Road.mat`, `RoadSpawn.mat`, and `RoadEnd.mat` assets remain the colored debug-overlay materials. They are not assigned to the production prefab.

### Undo, idempotency, and failure behavior

- The existing `BoardAuthoringDocument.Commit` and `BoardChangeScheduler` flow remains the authoring entry point and retains its current Undo behavior.
- Generated root/instance creation, deletion, and assignment use Unity Undo APIs and mark only the matching loaded scene dirty.
- Paint, erase, road-role changes, Undo/Redo, and repeated synchronization must leave exactly one prefab root per eligible straight cell and no duplicates.
- A missing prefab reference produces no production instances and never damages board data, debug geometry, or manual visuals. Corner/junction omission is silent by explicit owner decision.

### Verification and acceptance

- Planner tests cover X runs, Z runs, isolated-X behavior, Spawn/End inclusion, same-level neighbor isolation, and silent omission of corner/T/cross cells.
- Synchronizer tests cover one prefab per eligible cell, correct position/rotation/scale, dedicated-root ownership, idempotent reuse, erase/removal, and independence from `VisualizeInScene`.
- A regression test proves every child renderer uses the exact original `New Material.mat` asset through `sharedMaterial`, and that neither prefab creation nor synchronization leaves scene-only material instances.
- A regression test proves both quad children are colliderless and have shadow casting/receiving disabled.
- Unity `6000.3.21f1` must compile cleanly and the full GridPlacement EditMode suite must pass.
- Live `Level_001` inspection must show 27 generated straight-road prefabs, the corner cell intentionally blank, no colored debug overlay while `VisualizeInScene` is false, no seam/black alpha edge between consecutive cells, and no changes to unrelated manual visuals.

### Verification evidence

- Unity `6000.3.21f1` compiled the implementation successfully. The full `TowerDefense3D.GridPlacement.EditModeTests` suite passed **88 of 88** with zero failures, skips, or inconclusive results in 2.745 seconds after the final scene migration.
- `Level_001` generated exactly 27 `RoadStraightCell` prefab instances and 54 child `MeshRenderer` components from its 28 road-role cells. The one X/Z corner at `(34, 28, 0)` remained intentionally empty and synchronization emitted no warning or error.
- All 54 generated renderers reference the exact asset `Assets/Resources/Textures/New Material.mat` through `sharedMaterial`; none has an `(Instance)` material name. The generated hierarchy contains zero colliders, all renderers cast and receive no shadows, and all production renderers remain enabled while all six debug renderers are disabled by `VisualizeInScene = false`.
- The four exact manual sample objects `Quad`, `Quad (1)`, `Quad (2)`, and `Quad (3)` under `Grid Placement/Visuals` were removed only after the generated hierarchy passed the live audit. `Level_002` received the same prefab reference; its current board has no road-role cells, so it correctly generates no production-road root.
- A focused multi-angle Scene View capture verified the complete L-shaped route from isometric, front, top, and right views. The top view showed continuous straight sections with the corner handled by the approved silent omission and no visible center seam or black alpha artifact. After clearing the Unity Console and synchronizing `Level_001` again, the focused smoke test reported 27 cells / 54 renderers and the Console contained zero warnings or errors.

### Non-goals and deferred work

- Corner/curve prefabs, T-junctions, cross-junctions, procedural mesh combining, runtime road editing, enemy pathfinding, and new Spawn/End art are outside this addendum.
- Material variants, per-instance material property blocks, and generated material assets are prohibited for this implementation.
- Draw-call or mesh-combining optimization is deferred until mobile profiling shows that the current shared-material prefab approach requires it.

## Approved addendum — Generic GridPlaceable prefab layer (19 August 2026)

This addendum supersedes only the production-art coupling described above. The
gameplay `Road` / `RoadSpawn` / `RoadEnd` flags and their colored debug overlays
remain unchanged.

### Owner-approved data contract

- `BoardDefinition.gridPlaceables` is a separate serialized layer from
  `BoardDefinition.cells`. Painting or erasing a prefab never adds, clears, or
  replaces any gameplay flag.
- A full `GridCell` coordinate (X, Z, and Y) holds at most one prefab. Painting
  a different prefab replaces it; right-click erases it.
- Only prefab assets whose root owns a `GridPlaceable` component are eligible.
  The component owns display name, cell-relative position offset, base
  rotation, scale multiplier, neighbor-rotation policy, unsupported-junction
  behavior, and renderer sorting order.
- The first migration paints `RoadStraightCell` only on the 26 ordinary
  `Road` cells in `Level_001_Board`. The `RoadSpawn` and `RoadEnd` cells do not
  receive prefab placements. `Level_002_Board` starts with an empty prefab
  layer.

### Rendering and ownership

- `BoardSceneSynchronizer` owns prefab instances under a dedicated
  `Generated Grid Placeables` root, never under the hand-authored `Visuals`
  root and never under the debug `Board Visualization` root.
- The planner compares neighboring cells only when they contain the same
  prefab on the same Y level. `RoadStraightCell` rotates 90 degrees for a Z
  run, uses X when isolated, and silently emits no instance at a corner or
  junction until matching corner art exists.
- Generated instances preserve the prefab's original shared material assets.
  The synchronizer does not access `Renderer.material`, create material
  variants, or install per-instance material property blocks.
- Transparent draw order is explicit: every Board Visualization renderer uses
  sorting order `-100`; each generated prefab uses its `GridPlaceable`
  renderer order (RoadStraightCell uses `0`). Board debug transparency is
  therefore submitted before the road transparency.
- `VisualizeInScene` controls only colored Board Visualization renderers.
  GridPlaceable production art remains enabled independently.

### Acceptance

- Board Painter exposes a Prefab brush with a root-`GridPlaceable` asset field,
  1x1/3x3/5x5 painting, orange occupancy accent, replace-on-paint, and
  right-click erase.
- Document commit/reload, resize cleanup, validation, deterministic ordering,
  Undo scheduling, and generated-scene signatures include the separate prefab
  layer.
- Tests must prove road flags remain unchanged after prefab paint/erase,
  Spawn/End do not implicitly generate art, original shared materials remain
  shared, Board sorting order is lower than road sorting order, unchanged sync
  is idempotent, and clearing only the prefab layer removes only its generated
  root.
- Live Unity verification must compile, pass the full GridPlacement EditMode
  suite, resynchronize `Level_001`, save the generic field/root migration, and
  visually inspect the transparent layering. This evidence replaces the old
  27-instance acceptance count; the new expected result is 25 generated
  straight instances from 26 prefab placements because the one L-corner is
  intentionally hidden.

## Approved addendum — GridPlaceable road topology variants (19 August 2026)

**Status:** Approved and implemented

**Tracking Bead:** `TowerDefense3D-oatt`

This addendum extends the generic `GridPlaceable` layer with production art
for corners, three-way junctions, and four-way junctions. It does not couple
prefab placement back to the gameplay Road flags.

### Owner-approved contract

- Neighbor classification uses the four same-Y orthogonal cells and only
  connects cells containing the exact same root paint prefab.
- The topology set is `Isolated`, `End`, `Straight`, `Corner`, `ThreeWay`, and
  `FourWay`. A single `GridPlaceablePlacement` still owns each cell; topology
  only chooses which visual prefab is instantiated for that placement.
- `RoadStraightCell` remains the paintable root asset. Its `GridPlaceable`
  component references optional corner, T-junction, and cross variants. The
  variant prefabs do not carry `GridPlaceable`, so they cannot be selected as
  independent Board Painter brushes.
- Missing optional variants remain silent. With
  `hideAtCornerOrJunction = true`, an unsupported complex topology produces no
  visual; disabling that setting falls back to the root prefab.
- No material is instantiated. Straight, corner, T-junction, and cross
  renderers all reference the exact existing
  `Assets/Resources/Textures/New Material.mat` asset through
  `sharedMaterial`.

### Geometry and orientation

- `RoadCornerCell.prefab` uses a low-cost twelve-segment quarter-turn mesh.
  Its solid inner fan samples the opaque half of the existing road texture,
  while the outer ring expands to radius `1.5` and samples the transparent
  edge. This fills the inside bend, matches the full two-unit width of the
  adjacent straight prefab, and preserves the trail fade only on the outside
  of the curve.
- `RoadTJunctionCell.prefab` uses the full edge texture on one quad. Its
  canonical orientation omits negative Z; rotating the root exposes all four
  missing-branch directions.
- `RoadCrossCell.prefab` uses an opaque interior crop of the same texture on
  one quad because a four-way center has no outer road edge inside the cell.
- Corner canonical rotation is zero for positive X plus positive Z; the other
  three corners use 90, 180, and 270 degrees. T-junction canonical rotation is
  zero when negative Z is missing; the other missing directions also resolve
  to 90-degree increments. Cross rotation is zero.
- All variant renderers are colliderless, cast and receive no shadows, use
  sorting order `0`, and stay above Board Visualization sorting order `-100`.

### Verification evidence

- Unity `6000.3.21f1` compiled the final source with zero Console errors.
- The final full Grid Placement EditMode suite passed **91 of 91** with zero
  failures, skips, or inconclusive results. Tests cover every corner rotation,
  every T-junction rotation, the four-way topology, exact variant selection,
  the corner's filled inner bend and matching straight-road width, mesh
  contracts, absence of paint markers/colliders, and exact shared-material
  identity.
- `Level_001` was resynchronized and saved with 26 generated GridPlaceable
  instances: 25 `RoadStraightCell` instances and one
  `RoadCornerCell` instance. Its current board contains no T or cross
  placement, so those variants are verified by exhaustive planner tests and
  prefab inspection rather than by adding artificial level content.
- Live Scene View inspection confirmed the road remains visible while colored
  Board Visualization is disabled, and temporarily enabling the debug layer
  confirmed its transparent geometry renders underneath the road art. The
  setting was restored to disabled before the final save.
- A regression inspection from the gameplay top view found that the original
  corner was one cell wide and mapped transparent texels into the inside bend,
  which produced triangular holes and made the turn appear narrower than its
  straight neighbors. Bead `TowerDefense3D-e1pq` records the corrective mesh
  pass. The final top-down capture shows a filled quarter bend with continuous
  width, no triangular gap, and the original shared material; `Level_001`
  remained clean after verification.

## Approved addendum — Basic Cell and Overlay Cell authoring (19 August 2026)

**Status:** Approved and implemented

**Tracking Bead:** `TowerDefense3D-kro5`

### Owner-approved Board Painter contract

- Board Painter exposes exactly two top-level brush groups: `Basic Cell` and
  `Overlay Cell`.
- `Basic Cell` exposes only `Empty`, `Buildable`, and `No-Build`. The obsolete
  `Blocked Surface` and `Volume Blocker` editor presets are removed. Existing
  serialized `StaticBlocker` flags remain readable and visible but cannot be
  newly painted through Board Painter.
- `Overlay Cell` exposes `Prefab`, `Camera Focus`, `Road`, `Road Spawn`, and
  `Road End` in one selector. These remain distinct data meanings; the merge
  is an authoring-UI organization, not a flag or save-format collapse.
- Basic and overlay data coexist on the same full X/Z/Y coordinate. Painting
  or erasing a Basic Cell changes only the basic flag mask and preserves Road,
  Road Spawn, Road End, Camera Focus, and the separate GridPlaceable prefab
  layer. Painting or erasing any overlay preserves the Basic Cell state.
- Road roles remain mutually exclusive. Camera Focus retains its existing
  lowest-playable-level restriction. Prefab selection still accepts only a
  prefab asset whose root carries `GridPlaceable`.

### Acceptance

- The live Board Painter shows only the two approved group labels and the
  exact approved options under each group.
- EditMode tests prove the exposed option lists and bidirectional independence
  between Basic Cell and every overlay kind, while legacy blocker data still
  reloads without migration or loss.
- Unity `6000.3.21f1` compiles with zero Console errors, the complete Grid
  Placement EditMode suite passes, and live UI inspection confirms the merged
  authoring layout.

### Verification evidence

- The final Unity compile completed with zero Console errors. The full Grid
  Placement EditMode suite passed **93 of 93** with zero failures, skips, or
  inconclusive results in 3.047 seconds.
- Computer Use inspection of the live Board Painter confirmed the top-level
  selector contains only `Basic Cell` and `Overlay Cell`. The Basic selector
  contains only `Empty`, `Buildable`, and `No-Build`; the Overlay selector
  contains exactly `Prefab`, `Camera Focus`, `Road`, `Road Spawn`, and
  `Road End`.
- Regression tests verify Basic painting replaces only the basic flag mask and
  preserves Road, Road Spawn, Road End, Camera Focus, and GridPlaceable data.
  Existing overlay tests continue to verify the inverse direction.
- The obsolete blocker preset enum values and mapping branches were removed;
  runtime `StaticBlocker` support remains because existing board data and
  placement validation still use it. Legacy blocker cells remain visible with
  an `X` marker and are cleared only when a new Basic Cell value is painted.
- Final live checks reported `Level_001` clean and `Level_001_Board` not dirty,
  with its existing 4,800 cell entries and 26 GridPlaceable entries unchanged.

## Approved corrective addendum — T-junction texture continuity (19 August 2026)

**Status:** Approved and implemented

**Tracking Bead:** `TowerDefense3D-78wh`

### Owner-approved correction

- Keep the existing topology planner, four canonical T rotations,
  `RoadTJunctionCell.prefab`, and exact shared
  `Assets/Resources/Textures/New Material.mat` reference.
- Replace the insufficient one-cell center quad with one non-overlapping
  `1 x 1.5` mesh aligned to the canonical through-road axis. It extends only
  from the center toward the missing-branch outer edge; the connected side
  stops at the `+0.5` cell boundary where the neighboring straight prefab
  begins. Together they restore the full two-unit through-road width without
  coplanar overlap.
- Split the mesh at the road center: the canonical missing-branch side fades
  from opaque center texels to the texture's transparent outer edge, while
  the connected-branch side samples only the opaque texture region.
- Do not add a material instance, extra renderer, collider, overlapping
  coplanar geometry, or topology-specific runtime code.

### Acceptance

- A top-down T route has a continuous center with no transparent or diagonal
  gap or rectangular indentation. The through road retains its full two-unit
  width, the missing-branch side fades outward, and the three connected
  branches remain continuous.
- The T prefab remains colliderless and uses the original shared road
  material at sorting order `0`.
- A focused EditMode regression verifies the `1 x 1.5` footprint and the
  asymmetric missing-side/connected-side UV contract. The complete Grid
  Placement EditMode suite passes in Unity `6000.3.21f1`.

### Verification evidence

- The regression failed on the previous `V=0..1` mapping, reporting the two
  transparent-region coordinates `(0, 1)` and `(1, 1)`, then passed after the
  mesh UVs changed to the opaque `V=0.05..0.55` crop.
- The intermediate Grid Placement EditMode suite passed **94 of 94** with zero
  failures, skips, or inconclusive results in 1.744 seconds. Unity reported no
  Console errors.
- A temporary, non-saved top-down composition of the T prefab and its three
  straight neighbors showed that the previous diagonal alpha gap was gone,
  but this framing did not expose the full-width indentation later found in
  the authored level. The preview was removed after capture.
- Intermediate inspection confirmed the UV-only state still had four
  vertices, two triangles, and one-cell bounds. That geometry evidence was
  later used to diagnose why the road remained indented.
- Owner visual QA then exposed a remaining rectangular indentation: the
  one-cell center did not cover the full two-unit width of the through road.
  Live inspection of the authored 90-degree T reproduced the defect. The
  corrective revision above supersedes the one-cell footprint while retaining
  the successful opaque-center fix and original shared-material contract.
- The revised geometry regression failed on the intermediate mesh with
  **92 passed / 2 failed**: the T still had four instead of six vertices and
  stopped at local `Z=-0.5` instead of the required missing-side `Z=-1`.
- The final mesh has six vertices and four non-overlapping triangles, with
  bounds from `(-0.5, -0.059, -1)` to `(0.5, -0.059, 0.5)`. Its missing side
  maps from opaque center `V=0.05` to transparent edge `V=1`, while the
  connected boundary stays in the opaque crop at `V=0.55`.
- The complete final Grid Placement EditMode suite passed **94 of 94** with
  zero failures, skips, or inconclusive results in 3.066 seconds. Unity
  reported zero Console errors.
- Before/after top-down captures of the actual authored 90-degree T in
  `Level_001` reproduced and then removed the rectangular indentation. The
  final prefab remains colliderless, sorting order `0`, and references the
  exact non-instanced `Assets/Resources/Textures/New Material.mat`; the scene
  remained clean.

## Approved corrective addendum — static Prefab Brush instances (19 August 2026)

**Status:** Implemented and verified

**Tracking Bead:** `TowerDefense3D-j8f0`

### Owner-approved correction

- Every prefab instance generated from the Board Painter Prefab Brush must
  report `GameObject.isStatic == true` on its root and every descendant,
  thereby enabling all static flags supported by the current Unity version.
- `BoardSceneSynchronizer` owns this Editor-only scene state because the brush
  persists prefab selections to `BoardDefinition`, while the synchronizer
  performs the actual prefab instantiation.
- Matching generated instances that predate this rule must be repaired in
  place during synchronization. Correct prefab instances must retain their
  instance IDs instead of being destroyed and rebuilt solely to set flags.
- Preserve prefab links, transform overrides, shared materials, renderer
  sorting, board flags, road topology, and Prefab Brush paint/erase behavior.

### Non-goals

- Do not modify source prefab assets or their authored static flags.
- Do not mark unrelated scene objects, board visualization rectangles, or
  manually placed objects static.
- Do not add runtime code, serialized configuration, material instances, or a
  new Board Painter option for this mandatory behavior.

### Verification plan

- Extend the existing generated GridPlaceable contract test to require
  `GameObject.isStatic == true` on the full instantiated hierarchy.
- Clear those flags in the test, synchronize again, and verify the same prefab
  instance is retained while all static flags are restored.
- Run the complete Grid Placement EditMode suite in Unity `6000.3.21f1` and
  confirm the final Console contains no new errors.
