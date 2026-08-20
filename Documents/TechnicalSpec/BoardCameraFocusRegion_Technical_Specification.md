# Board Camera Focus Region Technical Specification

**Status:** Approved
**Approved:** 17 August 2026
**Implementation status:** Implemented and verified
**Target Unity version:** 6000.3.21f1
**Tracking Bead:** TowerDefense3D-tnh (this specification); approval gate TowerDefense3D-bw6

## Purpose

Let a board author paint (and erase) a new **Camera Focus** cell flag on the board's lowest playable level, and have `BoardCameraFramingSolver` narrow the camera framing region to the union of focus-flagged cells — instead of the full lowest-level footprint — before applying the existing Grid X/Y span cap and edge padding. When no cell is flagged, framing is unchanged from today's full-footprint behavior.

This document records four architecture decisions already approved by the project owner (see "Architecture and ownership" below) and specifies the concrete contracts, file locations, and composition order needed to implement them without re-deciding the architecture. The project owner reviewed this drafted content itself and approved it on 17 August 2026 (see `TowerDefense3D-bw6`).

## Approved scope

- Add a `CameraFocus` bit to `TowerDefense3D.GridPlacement.BoardCellFlags` (`Assets/Scripts/Board/Scripts/BoardCellDefinition.cs`), reusing the existing per-cell flags storage (`BoardCellDefinition.flags`, part of `BoardDefinition.cells`). No new serialized field or asset.
- Scope focus-region cells to the same "lowest playable level" that `LowestBoardLevelBoundsCalculator.TryCalculate` (`Assets/Scripts/Board/Scripts/LowestBoardLevelBounds.cs`) already computes.
- Add an independent bit-toggle paint/erase brush for `CameraFocus` to the Board Painter (`Assets/Scripts/Board/Editor/BoardAuthoring/BoardPainterWindow.cs`) that does not route through the existing preset-paint call (`BoardAuthoringDocument.Paint` / `BoardPaintPresetUtility.GetFlags`). It must independently OR-in/AND-away only the `CameraFocus` bit while preserving whatever preset flags (`SupportsPlacement`, `Buildable`, `StaticBlocker`) the cell already has.
- Change `BoardCameraFramingSolver`'s composition order (`Assets/Scripts/Camera/Scripts/BoardCameraFramingSolver.cs`) to: (a) focus-region selection first, (b) then the existing Grid X/Y span cap (`maxCameraGridXSpan` / `maxCameraGridYSpan`), (c) then the existing edge padding (`edgePaddingCells`). The cap and padding math itself is unchanged.
- Add `CameraFocus` to the known-flags mask in `BoardAuthoringDocument.Validate()` so painted focus cells no longer report as an "unknown flag" warning.
- Preserve backward compatibility: a board asset with no `CameraFocus` bit set anywhere produces byte-for-byte the same framing behavior as before this feature.

## Non-goals

- No new serialized field, ScriptableObject, or authored asset. `CameraFocus` is a `BoardCellFlags` bit only.
- No change to board geometry, colliders, coordinate mapping, occupancy, or placement validation (`GridBoard`, `PlacementValidator`, `BoardGeometryPlanner`). Those types only test the specific bits they already care about (`SupportsPlacement`, `Buildable`, `StaticBlocker`) via bitwise masks, so a new bit is inert to them by construction; this specification does not change them.
- No change to the existing preset brush, its five presets (`Empty`, `Buildable`, `NoBuild`, `BlockedSurface`, `VolumeBlocker`), or `BoardAuthoringDocument.Paint(GridCell, BoardPaintPreset)`. The `CameraFocus` toggle brush is additive and independent.
- No change to `BoardCameraFramingBounds.TryCreate`'s span-cap math or `BoardCameraFramingPlane.TryCreate`'s padding math. Both keep their current formulas; only the bounds value fed into the cap step changes.
- No runtime (non-Editor) camera-focus authoring UI, no player-facing camera controls, no multi-level focus regions, and no per-level independent focus sets. Focus is defined once, scoped to the single lowest playable level.
- No change to `BoardCameraFramer`'s public contract, `BoardSceneSynchronizer`, or the direct perspective `Camera` workflow (rotation, field of view, Safe Area handling) established by `BoardCameraFraming_Technical_Specification.md` and `BoardCameraFramingLimits_Technical_Specification.md`.
- Does not implement any code change; this Bead is documentation only.

## Architecture and ownership

The following four points are owner-approved and must be implemented as described, not redesigned. Later sections give the exact contracts each point requires.

1. **Data storage.** `CameraFocus` is a new bit on `BoardCellFlags`, reusing `BoardCellDefinition.flags` (part of `BoardDefinition.cells`). No new serialized field or asset is introduced.
2. **Level scoping.** Focus-region cells are scoped only to the same "lowest playable level" that `LowestBoardLevelBoundsCalculator` already computes (the lowest Y level containing any cell with `SupportsPlacement`). Focus painting/selection and the solver's focus union only ever consider cells at that one Y level.
3. **Independent painter brush.** The Board Painter gets an independent bit-toggle paint/erase brush for `CameraFocus` that does **not** route through the existing preset-paint call (`BoardAuthoringDocument.Paint` / `BoardPaintPreset`). It independently ORs in or ANDs away only the `CameraFocus` bit, preserving whatever preset flags already exist on that cell.
4. **Solver composition order.** In the camera-framing solver: (a) focus-region selection first — if any cell at the lowest level has `CameraFocus` set, narrow the base bounds to the union of focus-flagged cells instead of the full lowest-level footprint; if none, fall back to today's full-footprint behavior (backward compatible) — then (b) the existing Grid X/Y span cap — then (c) the existing edge padding. The cap/padding math itself is not reordered or changed.

### Component ownership

- **`BoardCellFlags`** (`Assets/Scripts/Board/Scripts/BoardCellDefinition.cs`) owns the bit definition. It gains one member:

  ```csharp
  [Flags]
  public enum BoardCellFlags
  {
      None = 0,
      SupportsPlacement = 1 << 0,
      Buildable = 1 << 1,
      StaticBlocker = 1 << 2,
      CameraFocus = 1 << 3
  }
  ```

  `BoardCellDefinition` gains a matching read-only convenience member `IsCameraFocus => (flags & BoardCellFlags.CameraFocus) != 0`, consistent with its existing `SupportsPlacement`, `IsBuildable`, `IsStaticBlocker` properties.

- **A new focus-region calculator** owns computing the union of focus-flagged cells at the lowest level. It is a new static class, `BoardCameraFocusRegionCalculator`, in a new file `Assets/Scripts/Camera/Scripts/BoardCameraFocusRegion.cs` (same folder and assembly as `LowestBoardLevelBounds.cs`, i.e. `TowerDefense3D.GridPlacement.Runtime`). It reuses the existing `LowestBoardLevelBounds` struct as its result shape (no new bounds type):

  ```csharp
  public static class BoardCameraFocusRegionCalculator
  {
      public static bool TryCalculate(
          BoardDefinition board,
          LowestBoardLevelBounds lowestLevelBounds,
          out LowestBoardLevelBounds focusBounds);
  }
  ```

  `TryCalculate` scans `board.Cells`, keeps only cells within board bounds whose `Coordinate.Y == lowestLevelBounds.Level` and whose `Flags` include `CameraFocus`, and unions their X/Z extents into a `LowestBoardLevelBounds` with the same `Level`. It returns `false` (leaving `focusBounds` as `default`) when no such cell exists, so callers fall back to `lowestLevelBounds` unchanged. This mirrors the existing two-pass scan style already used by `LowestBoardLevelBoundsCalculator.TryCalculate`.

- **`BoardCameraFramingPlane.TryCreate`** (`Assets/Scripts/Camera/Scripts/BoardCameraFramingSolver.cs`) owns wiring the focus-region step into the existing pipeline. Its internal composition becomes:

  ```csharp
  if (!LowestBoardLevelBoundsCalculator.TryCalculate(board, out LowestBoardLevelBounds lowestLevelBounds))
  {
      return false; // unchanged failure path
  }

  LowestBoardLevelBounds baseBounds =
      BoardCameraFocusRegionCalculator.TryCalculate(board, lowestLevelBounds, out LowestBoardLevelBounds focusBounds)
          ? focusBounds
          : lowestLevelBounds;

  if (!BoardCameraFramingBounds.TryCreate(baseBounds, maxGridXSpan, maxGridYSpan, out BoardCameraFramingBounds framingBounds))
  {
      return false; // unchanged failure path
  }
  // edge padding / world-space plane construction below is unchanged
  ```

  `BoardCameraFramingBounds.TryCreate`'s signature, span-cap math, and centering formula are unchanged; it simply receives `baseBounds` (focus-narrowed or full) instead of always receiving the full lowest-level bounds directly. The edge-padding and `boardOrigin.TransformPoint` steps after it are untouched. This satisfies decision 4's ordering: focus selection narrows the input to the cap, the cap still applies to whatever bounds it receives, and padding still applies after the cap.

- **`BoardAuthoringDocument`** (`Assets/Scripts/Board/Editor/BoardAuthoring/BoardAuthoringDocument.cs`) owns the independent toggle mutation and the known-flags validation mask. It gains one new public method, parallel to but separate from `Paint`:

  ```csharp
  public void SetCameraFocus(GridCell coordinate, bool enabled)
  {
      BoardCellFlags current = GetFlags(coordinate);
      BoardCellFlags updated = enabled
          ? current | BoardCellFlags.CameraFocus
          : current & ~BoardCellFlags.CameraFocus;

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

  This does not call `Paint` and does not use `BoardPaintPresetUtility.GetFlags`, satisfying decision 3. It reads the cell's current flags (which may already include preset bits), sets or clears only the `CameraFocus` bit, and writes back — preserving `SupportsPlacement` / `Buildable` / `StaticBlocker` on that cell exactly as they were.

  `Validate()`'s `knownFlags` constant changes from:

  ```csharp
  const BoardCellFlags knownFlags = BoardCellFlags.SupportsPlacement
      | BoardCellFlags.Buildable
      | BoardCellFlags.StaticBlocker;
  ```

  to:

  ```csharp
  const BoardCellFlags knownFlags = BoardCellFlags.SupportsPlacement
      | BoardCellFlags.Buildable
      | BoardCellFlags.StaticBlocker
      | BoardCellFlags.CameraFocus;
  ```

  No other `Validate()` behavior changes. `Reload()` and `Commit()` require no changes: both already round-trip a cell's full `flags` value as a raw `int` through `SerializedProperty.intValue` (see "Serialized integration" below), so the new bit is preserved automatically once the enum itself defines it.

- **`BoardPainterWindow`** (`Assets/Scripts/Board/Editor/BoardAuthoring/BoardPainterWindow.cs`) owns the independent brush UI and input handling. See "Interaction flow" for the precise control flow.

- **`BoardPaintPresetUtility`** (`Assets/Scripts/Board/Editor/BoardAuthoring/BoardPaintPreset.cs`) owns preset-to-color/label mapping and is not extended with a `CameraFocus` preset (decision 3 keeps it independent of the preset system). `GetClosestPreset(BoardCellFlags flags)` must mask out `CameraFocus` before comparing against preset flag combinations (`GetClosestPreset(flags & ~BoardCellFlags.CameraFocus)`'s equivalent inline mask), so a cell that is both, e.g., `Buildable` and `CameraFocus` still resolves to the `Buildable` preset for coloring instead of falling through to `Empty` with a "?" overlay. This is required because `GetClosestPreset` currently matches by exact flag equality (`GetFlags(preset) == flags`), and no existing preset combination includes `CameraFocus`.

## Data and runtime-state contracts

- `BoardCellFlags.CameraFocus = 1 << 3` (decimal `8`). It is the next free bit after `StaticBlocker = 1 << 2`.
- `BoardCellDefinition` remains an immutable, serializable struct; `CameraFocus` is read through the same `Flags` property as the other three bits, plus the new `IsCameraFocus` convenience property.
- `CameraFocus` composes freely with the three existing bits on the same cell (e.g. `SupportsPlacement | Buildable | CameraFocus` is a valid, expected combination: a normal buildable cell that also anchors the camera framing region).
- `BoardCameraFocusRegionCalculator.TryCalculate` is a pure function of `BoardDefinition.Cells` and a previously computed `LowestBoardLevelBounds`; it holds no state and performs no allocation beyond its own return value.
- The union produced by `BoardCameraFocusRegionCalculator` uses the same half-open interval convention as `LowestBoardLevelBounds` (`MinX`/`MinZ` inclusive, `MaxXExclusive`/`MaxZExclusive` exclusive), so it can be passed directly into `BoardCameraFramingBounds.TryCreate` without any conversion.
- No new runtime (non-Editor) state is introduced. `BoardCameraFramer` and `BoardScenePresenter` are unaffected; they continue to read `BoardDefinition` and invoke `BoardCameraFramingPlane.TryCreate` exactly as today.

## Interaction flow

### Authoring (Board Painter)

1. The author opens the Board Painter (`Tools > Tower Defense > Board Painter`) on a `BoardDefinition` asset, as today.
2. `BoardPainterWindow` gains a second, independent brush toggle next to the existing preset palette (`DrawPalette`), e.g. a `GUILayout.Toggle` labeled "Camera Focus". This toggle selects a `cameraFocusBrushActive` boolean brush mode, distinct from `selectedPreset`. Only one brush mode is active for a given stroke; the existing preset palette and the `CameraFocus` toggle are mutually exclusive input modes so left/right-click never has to disambiguate which bit a click intends.
3. While the Camera Focus brush is active, `HandleGridInput` routes left-click/drag to `SetCameraFocus(coordinate, true)` and right-click/drag to `SetCameraFocus(coordinate, false)`, through a new brush-size-aware helper parallel to the existing `PaintBrush`:

   ```csharp
   internal static bool PaintCameraFocusBrush(
       BoardAuthoringDocument targetDocument,
       GridCell center,
       int size,
       bool enabled)
   ```

   This mirrors `PaintBrush`'s radius/clipping loop but calls `targetDocument.SetCameraFocus(coordinate, enabled)` per cell instead of `targetDocument.Paint(coordinate, preset)`.
4. To honor decision 2 (level scoping) at the point of authoring rather than only at solve time, `BoardPainterWindow` computes the current lowest playable level from the in-memory (possibly uncommitted) document state and disables the Camera Focus toggle, with an explanatory tooltip, whenever the currently selected editing level (`selectedLevel`) is not that lowest level. `LowestBoardLevelBoundsCalculator.TryCalculate` operates on the committed `BoardDefinition` asset (via `board.Cells`), not on `BoardAuthoringDocument`'s uncommitted in-memory dictionary, so `BoardAuthoringDocument` gains a small helper that mirrors only the lowest-level scan (not the X/Z union) against its live `cells` dictionary:

   ```csharp
   public bool TryGetLowestPlayableLevel(out int level)
   ```

   returning the lowest `Y` among in-memory cells with `SupportsPlacement` set, or `false` if none exist yet (in which case the Camera Focus brush stays disabled, since there is no lowest level to scope to).
5. Stroke lifecycle (`strokeActive`, `strokeChanged`, `lastPaintedCell`, `CommitStroke`) is reused unchanged; `CommitStroke` calls `document.Commit(...)` with a distinct undo name, e.g. `"Toggle Camera Focus"`, when the active brush mode is Camera Focus, versus `"Paint Board Cells"` for the preset brush.
6. `DrawCells` renders the existing preset color and any `StaticBlocker`/mismatch overlay unchanged (after masking `CameraFocus` out of the preset match, per "Architecture and ownership"), then draws one additional small visual marker (e.g. a corner accent rect or an "F" label) on any cell where `IsCameraFocus` is true, so focus cells remain visually distinguishable from their preset color.
7. `Validate()` continues to run on every `OnGUI` pass (`DrawStatus`); with the known-flags mask updated, cells that only combine already-known bits (including `CameraFocus`) no longer report as "unknown flag" cells.

### Solving (runtime and Editor preview)

1. `BoardCameraFramer.TryCalculatePosition` calls `BoardCameraFramingPlane.TryCreate(board, boardOrigin, edgePaddingCells, maxGridXSpan, maxGridYSpan, out plane)` exactly as today; its own signature and call site do not change.
2. Inside `TryCreate`, `LowestBoardLevelBoundsCalculator.TryCalculate` still establishes the lowest playable level and the full lowest-level footprint first (this remains a prerequisite, since the focus calculator needs to know which level is "lowest").
3. `BoardCameraFocusRegionCalculator.TryCalculate` then attempts to narrow that footprint to the union of `CameraFocus`-flagged cells at that same level.
   - If at least one such cell exists, the narrowed bounds become the base bounds fed to the span cap.
   - If none exists, the full lowest-level bounds are used unchanged — this is exactly today's behavior, so an unmodified board asset (no `CameraFocus` bit ever set) produces identical framing to before this feature.
4. `BoardCameraFramingBounds.TryCreate` applies the existing `maxCameraGridXSpan` / `maxCameraGridYSpan` cap to whichever base bounds it received, using its existing math, unchanged.
5. The existing edge-padding and world-space plane construction (`edgePaddingCells`, `boardOrigin.TransformPoint`) apply after the cap, unchanged.
6. `BoardCameraFramingSolver.TryCalculatePosition` consumes the resulting `BoardCameraFramingPlane` exactly as today; it has no awareness of `CameraFocus` at all.

## Folder and assembly boundaries

- `BoardCellFlags` / `BoardCellDefinition` changes stay in `Assets/Scripts/Board/Scripts/BoardCellDefinition.cs`, assembly `TowerDefense3D.GridPlacement.Runtime`.
- The new `BoardCameraFocusRegionCalculator` is added at `Assets/Scripts/Camera/Scripts/BoardCameraFocusRegion.cs`, same assembly (`TowerDefense3D.GridPlacement.Runtime`), following the existing `Scripts/Board/` convention used by `LowestBoardLevelBounds.cs` and `GridCell.cs`. No new assembly definition is introduced.
- `BoardCameraFramingSolver.cs` changes stay in
  `Assets/Scripts/Camera/Scripts/`, same assembly, unchanged file.
- `BoardAuthoringDocument.cs`, `BoardPainterWindow.cs`, and `BoardPaintPreset.cs` changes stay in `Assets/Scripts/Board/Editor/BoardAuthoring/`, assembly `TowerDefense3D.GridPlacement.BoardAuthoring.Editor`. No new Editor assembly is introduced.
- No change to `TowerDefense3D.GridPlacement.EditModeTests` or
  `TowerDefense3D.GridPlacement.PlayModeTests` assembly boundaries. Board
  authoring tests live under `Assets/Scripts/Board/Tests/`; camera solver and
  runtime tests live under `Assets/Scripts/Camera/Tests/`; both join the
  existing test assemblies through GUID-backed `.asmref` files.
- The Board and Camera source roots remain inside the established Grid
  Placement assemblies; no dependency on `GameFlow` or another feature is
  introduced.

## Serialized integration

- `BoardDefinition.cells` (`BoardCellDefinition[]`) is the only serialized surface touched, and only through its existing `flags` field's numeric range. No field is added to `BoardDefinition`, `BoardCellDefinition`, or any other asset.
- `BoardAuthoringDocument.Reload()` reads each cell's `flags` via `(BoardCellFlags)element.FindPropertyRelative("flags").intValue`; `Commit()` writes it back via `element.FindPropertyRelative("flags").intValue = (int)ordered[i].Value`. Both already operate on the raw `int` bit pattern, so once `CameraFocus = 1 << 3` exists on the enum, it round-trips through the existing serialization path with no code change to `Reload()` or `Commit()`.
- Existing serialized `BoardDefinition` assets that never used bit 3 deserialize with `CameraFocus` unset on every cell, which is exactly the "no focus cell" case that falls back to full lowest-level framing (decision 4). No migration step, default-value backfill, or asset upgrade pass is required.
- `Undo`/dirty-state handling for the new brush reuses `BoardAuthoringDocument.Commit(string undoName)`, `Undo.RegisterCompleteObjectUndo`, and `EditorUtility.SetDirty`, exactly as the existing preset brush does; no new Undo integration is introduced.
- `BoardChangeScheduler.Queue(Asset)` (invoked at the end of `Commit()`) is unaffected; it already re-synchronizes the scene after any committed change to `cells`, and a `CameraFocus`-only toggle is just another committed change to that same array.

## Compatibility and migration constraints

- Backward compatibility is required and structurally guaranteed by decision 4's fallback: any `BoardDefinition` with zero `CameraFocus` bits set produces byte-identical `BoardCameraFramingBounds`/`BoardCameraFramingPlane` output to the pre-feature solver, because `BoardCameraFocusRegionCalculator.TryCalculate` returns `false` and the pipeline uses the untouched full lowest-level bounds.
- `GridBoard`, `PlacementValidator`, and `BoardGeometryPlanner` (`Assets/Scripts/Board/Scripts/GridBoard.cs`, `Assets/Scripts/Placement/Scripts/PlacementValidator.cs`, `Assets/Scripts/Board/Editor/BoardAuthoring/BoardGeometryPlanner.cs`) only test `SupportsPlacement`, `Buildable`, or `StaticBlocker` via bitwise `AND` checks against `BoardCellFlags`; none of them enumerate or switch exhaustively over all flag values. Adding `CameraFocus` is therefore inert to placement rules, occupancy, and generated board geometry/colliders by construction, and none of those files require a change for this feature.
- `BoardPaintPresetUtility.GetFlags` continues to never return `CameraFocus` for any preset; presets and the focus bit remain orthogonal, so no existing preset's meaning changes.
- No change to `BoardCameraFramer`, `BoardScenePresenter`, or `BoardSceneSynchronizer` public members; their existing serialized references and Editor synchronization behavior established by `BoardCameraFraming_Technical_Specification.md` and `BoardCameraFramingLimits_Technical_Specification.md` remain authoritative and unchanged.
- No renamed, removed, or reordered public API on `BoardCameraFramingBounds`, `BoardCameraFramingPlane`, or `BoardCameraFramingSolver`; `BoardCameraFramingPlane.TryCreate`'s two existing overloads keep their current signatures.

## Verification plan

Implementation is split across four downstream Beads (not created by this Bead), each validating one layer of this specification. This document exists so each of those Beads can implement its assigned layer without re-deciding the architecture above:

1. **EditMode test Bead — focus-region bounds calculator and solver composition.** Adds `EditMode` tests under `Assets/Scripts/Camera/Tests/EditMode/`, alongside the existing `BoardCameraFramingTests.cs`, covering the bounds calculator, fallback, level filtering, and composition order.
2. **EditMode test Bead — CameraFocus toggle brush/document behavior.** Adds `EditMode` tests under `Assets/Scripts/Camera/Tests/EditMode/` and `Assets/Scripts/Board/Tests/EditMode/`, covering focus-region calculations plus Board authoring, serialization, validation, and brush behavior.
3. **PlayMode test Bead — end-to-end camera framing.** Adds or extends `PlayMode` tests under `Assets/Scripts/Camera/Tests/PlayMode/`, alongside `BoardCameraFramingPlayModeTests.cs`, covering focused and fallback framing with the existing cap, padding, camera, and Safe Area contracts.
4. **Final live-Editor verification Bead.** After the above are implemented and green, verifies live in the Unity `6000.3.21f1` Editor: painting/erasing `CameraFocus` cells with the Board Painter's new independent brush persists correctly (Undo, dirty-state, `SerializedObject` round-trip) without disturbing existing preset flags on the same cells; the Camera Focus brush is disabled outside the lowest playable level as authored; the scene camera visibly reframes to the focus region in Play Mode when focus cells are painted and reframes back to the full footprint when they are all erased; and the Console shows zero new errors/warnings from compiling and exercising this feature.

This Bead's own verification is source inspection only: confirm this file exists at the required path with `Status: Draft`, and that it names every symbol/file listed in the assigning Bead's Context section by its real path.

## Risks

- The new `BoardPainterWindow` brush-mode toggle (preset vs. Camera Focus) adds a second exclusive input mode to a window that currently has only one; if implemented as a naive second toggle group instead of a genuinely exclusive mode, an author could end up unsure which brush a click will apply. The Interaction flow section above requires the two modes to be mutually exclusive for a given stroke to avoid this ambiguity.
- `GetClosestPreset`'s exact-equality matching against preset flag combinations is a preexisting design (not introduced by this feature); combining `CameraFocus` with a preset requires the mask fix specified above, or the visual "?" overlay will incorrectly suggest an unknown/invalid flag combination on every focus-flagged preset cell. This is called out explicitly so the PAINTER-UI implementation does not skip it.
- `LowestBoardLevelBoundsCalculator.TryCalculate` reads the committed `BoardDefinition` asset, while `BoardAuthoringDocument` holds uncommitted in-memory edits during a paint session. `TryGetLowestPlayableLevel` must scan the document's own in-memory `cells` dictionary (not re-invoke the asset-based calculator) so the Camera Focus brush's enabled/disabled state reflects the author's current uncommitted edits, not stale committed state.
- Authoring `CameraFocus` on a level that is the lowest playable level at authoring time, but stops being so after a later edit (e.g. a lower `SupportsPlacement` cell is painted afterward), leaves "orphaned" focus cells on what is now a non-lowest level. Per decision 4, the solver already ignores focus cells outside the (recomputed) lowest level, so this degrades safely to the full-footprint fallback rather than producing incorrect framing; it is called out here as an authoring UX rough edge, not a correctness risk.

## Deferred work

- A `Validate()` warning specifically for `CameraFocus` cells authored outside the current lowest playable level (distinct from the generic "unknown flag" warning it already reports for genuinely unrecognized bits) is not required by the approved architecture and is deferred; the solver's existing level-scoping fallback already makes such cells harmless.
- Any UI affordance to jump the Board Painter's selected level directly to the current lowest playable level (to reduce friction around the brush being disabled on other levels) is deferred; the approved architecture only requires the toggle to be disabled outside that level, not any convenience navigation.
- Any programmatic (non-Editor) API for setting `CameraFocus` at runtime is out of scope; this feature is an Editor-authoring-time and camera-solve-time concern only.
- Extending `BoardPaintPreset`/`BoardPaintPresetUtility` to expose a sixth "preset" that bundles a base preset with `CameraFocus` is deferred; decision 3 explicitly keeps the two brushes independent.

## Implementation status

Implementation completed and verified on 17 August 2026, through the approved Beads graph (`TowerDefense3D-ji6`, `-8sk`, `-8xv`, `-or6`, `-rs2`, `-7jl`, `-3v2`, `-wr4`), with no approved-scope deviation.

### Implemented files

- `Assets/Scripts/Board/Scripts/BoardCellDefinition.cs` — adds `BoardCellFlags.CameraFocus = 1 << 3` and the `IsCameraFocus` accessor.
- `Assets/Scripts/Camera/Scripts/BoardCameraFocusRegion.cs` (new) — `BoardCameraFocusRegionCalculator.TryCalculate`, unioning `CameraFocus`-flagged cells at the lowest playable level.
- `Assets/Scripts/Camera/Scripts/BoardCameraFramingSolver.cs` — `BoardCameraFramingPlane.TryCreate` now narrows to the focus-region result before the existing Grid X/Y span cap when one exists, falling back to the full lowest-level footprint otherwise; the cap and edge-padding math are unchanged.
- `Assets/Scripts/Board/Editor/BoardAuthoring/BoardAuthoringDocument.cs` — adds `SetCameraFocus` (independent bit-toggle, never routes through `Paint`), `TryGetLowestPlayableLevel`, and extends `Validate()`'s known-flags mask.
- `Assets/Scripts/Board/Editor/BoardAuthoring/BoardPaintPreset.cs` — masks `CameraFocus` out of `GetClosestPreset`'s equality comparison.
- `Assets/Scripts/Board/Editor/BoardAuthoring/BoardPainterWindow.cs` — adds the independent, mutually-exclusive Camera Focus brush toggle, `PaintCameraFocusBrush`, a corner-accent visual marker for focus-flagged cells, and masks `CameraFocus` out of `DrawCells`' own separate preset-mismatch check (a second masking fix beyond `GetClosestPreset`, found necessary during live testing).
- `Assets/Scripts/Camera/Tests/EditMode/BoardCameraFocusRegionCalculatorTests.cs` (new) — 4 unit tests on the bounds calculator.
- `Assets/Scripts/Camera/Tests/EditMode/BoardCameraFramingTests.cs` — 4 new integration tests, including an explicit composition-order proof.
- `Assets/Scripts/Camera/Tests/EditMode/BoardAuthoringDocumentCameraFocusTests.cs` (new) — 5 tests on the toggle brush/document behavior.
- `Assets/Scripts/Camera/Tests/PlayMode/BoardCameraFramingPlayModeTests.cs` — 2 new end-to-end tests through real `BoardScenePresenter`/`Camera`/`BoardCameraFramer` components.

### Verification evidence

- Unity `6000.3.21f1` compiled the final implementation with zero new Console errors or warnings at every implementation step, confirmed live via Unity MCP.
- Full Grid Placement EditMode suite: **52 of 52 passed** (39 pre-existing + 8 focus-region/solver tests + 5 authoring-document tests), re-run and confirmed directly in this final verification pass via `GridPlacementTestRunnerBridge.GetStatus()`.
- Full Grid Placement PlayMode suite: **7 of 7 passed** (5 pre-existing + 2 focus-region framing tests), re-run and confirmed directly in this final verification pass through the project's `Tools/Tower Defense/Tests/Run Grid Placement PlayMode` bridge.
- A fresh, live scratch-`BoardDefinition` check (created and destroyed only in memory, no asset written to disk) in this final verification pass confirmed: `SetCameraFocus` sets/clears only the `CameraFocus` bit while preserving `SupportsPlacement`/`Buildable` set by the existing `Paint` preset flow; `TryGetLowestPlayableLevel` correctly detects the lowest `SupportsPlacement` level; `Validate()` reports zero issues for a cell combining `CameraFocus` with known preset bits; `Commit()` completes without exception.
- The end-to-end PlayMode test (`Framer_FocusRegionCellsNarrowFramingBelowFullFootprint`) proves, through real `BoardScenePresenter`/`Camera`/`BoardCameraFramer` components at identical field of view and rotation, that a focus-narrowed region produces both a different framing-plane center and a strictly shorter camera framing distance than the full footprint — the "visibly narrower" acceptance criterion.
- The backward-compatibility guarantee is proven at three layers: the bounds-calculator unit tests, an explicit `BoardCameraFramingPlane` integration test, and an explicit PlayMode test — each confirming a board with zero `CameraFocus`-flagged cells produces the exact same framing result as before this feature. All 39 pre-existing EditMode tests and all 5 pre-existing PlayMode tests continued to pass completely unmodified throughout.
- `Level_001_Board.asset` and `Level_002_Board.asset` were not modified by this feature's implementation; both continue to exercise the no-focus-cells fallback path unchanged.

### Known limitations

- Live pixel-level mouse-click verification of the Board Painter's new brush toggle and corner-accent marker was not possible in this environment: no available tool can drive Unity's custom IMGUI `EditorWindow` at the mouse-click level (Unity MCP's capture tools cover only Scene/Game camera views, and reflection/raw file I/O are blocked inside the sandboxed command-execution tool). The brush was instead verified by calling its exact underlying production code paths (`SetCameraFocus`, `TryGetLowestPlayableLevel`, `PaintCameraFocusBrush`'s clip logic) directly against a live `BoardAuthoringDocument`, and by compiling and running the real `BoardPainterWindow` code with no exceptions. A human visual click-through of the Board Painter window is recommended before this brush is used on a shipping level, though it changes no code path beyond what was already exercised.
- Physical Android device build, profiling, and performance acceptance remain outside this feature's scope, consistent with the sibling `BoardCameraFraming`/`BoardCameraFramingLimits` specifications.

## Addendum: scene visualization of the Camera Focus region (17 August 2026)

Added after initial implementation, at the project owner's explicit request, following the same approval-then-implement discipline as the rest of this specification.

### Approved scope (addendum)

- When a board has one or more `CameraFocus`-flagged cells at the lowest playable level, `BoardSceneSynchronizer` generates one additional child under the existing `Board Visualization` root (alongside the `Placeable Area`/`Blocked Area` geometry): `Camera Focus Region`, a flat translucent overlay spanning the exact focus-region bounds.
- The overlay's visibility follows `BoardDefinition.VisualizeInScene` exactly like the existing placement/blocker geometry (the same designer-facing "Visualize In Scene" tick already on the board asset), reusing the existing `ApplyComponentState` toggle path with no change to that method's contract.
- The overlay is pure visual: no collider of any kind. It does not participate in placement raycasting, occupancy, or any gameplay system.
- When no `CameraFocus` cell exists, no overlay is generated, and an existing overlay is removed automatically the next time the board's authored content changes (via the existing full-regenerate-on-signature-change flow), matching this feature's established no-focus-cells backward-compatibility principle.

### Non-goals (addendum)

- No change to the `Placeable Area`/`Blocked Area` geometry, their colliders, or their own visibility toggling.
- No new authoring UI for the overlay itself; it is a read-only reflection of whatever `CameraFocus` cells are already painted through the existing Board Painter brush.
- No runtime (Play Mode gameplay) visibility of this overlay beyond whatever `VisualizeInScene` already controls for the rest of the debug geometry; it is an authoring/Editor visualization aid, not a gameplay HUD element.

### Architecture and ownership (addendum)

- `BoardGeometryPlan` (`Assets/Scripts/Board/Editor/BoardAuthoring/BoardGeometryPlan.cs`) gains a `LowestBoardLevelBounds? FocusRegion` property, populated alongside the existing `Rectangles`.
- `BoardGeometryPlanner.Create` (`BoardGeometryPlanner.cs`) computes `FocusRegion` by composing the already-existing `LowestBoardLevelBoundsCalculator.TryCalculate` and `BoardCameraFocusRegionCalculator.TryCalculate` — the identical composition already used by `BoardCameraFramingPlane.TryCreate` — and folds it into the plan's signature hash so a focus-only authoring change (with no geometry-rectangle change) still triggers a resync.
- `BoardSceneSynchronizer.HasMatchingGeometry` treats the overlay as one additional expected child (`plan.Rectangles.Count + (plan.FocusRegion.HasValue ? 1 : 0)`), requiring a `MeshRenderer` and explicitly requiring **no** `Collider` on that child — the inverse of its existing requirement that every rectangle child **must** have a `BoxCollider`.
- `BoardSceneSynchronizer.CreateCameraFocusRegion` creates the overlay as a `GameObject.CreatePrimitive(PrimitiveType.Quad)`, flat-rotated and scaled to the focus region's world span, positioned a small fixed offset (`CameraFocusOverlayLift`, 0.01 world units) above the lowest level's ground surface to avoid z-fighting, using a new dedicated material asset `Assets/Resources/Materials/CameraFocusRegion.mat` (URP/Lit, Transparent surface, translucent cyan, mirroring the existing `Ground.mat`'s transparent-material settings).
- **Verified defect caught by testing, not assumed:** Unity `6000.3.21f1`'s `GameObject.CreatePrimitive(PrimitiveType.Quad)` attaches a `MeshCollider` by default (unlike the historically documented Quad behavior). `CreateCameraFocusRegion` explicitly destroys any `Collider` component immediately after creation to satisfy the no-collider requirement; the automated test `Synchronizer_GeneratesColliderlessCameraFocusOverlayWhenFocusCellsExist` failed against the first implementation attempt (found a `MeshCollider`) and passed once this fix was applied, then stayed green as a regression guard.

### Serialized integration (addendum)

- No new field on `BoardDefinition` or `BoardCellDefinition`; this addendum consumes the existing `CameraFocus` flag exactly as the rest of this specification defines it.
- `Assets/Resources/Materials/CameraFocusRegion.mat` is a new, version-controlled project asset.
- `Level_001_Board.asset`/`Level_002_Board.asset` are unaffected until a designer paints `CameraFocus` cells on them.

### Verification plan (addendum) and evidence

- New EditMode tests added to `Assets/Scripts/Board/Tests/EditMode/BoardSceneAuthoringTests.cs`: overlay is generated with a `MeshRenderer` and zero `Collider` components at the exact expected transform when focus cells exist; overlay is omitted when none exist; overlay is removed on resync when focus cells are cleared; overlay's `MeshRenderer.enabled` follows `VisualizeInScene` across a resync.
- Full Grid Placement EditMode suite re-run and confirmed: **56 of 56 passed** (52 prior + 4 new).
- Full Grid Placement PlayMode suite re-run and confirmed unaffected: **7 of 7 passed**.
- Unity `6000.3.21f1` compiled the addendum with zero new Console errors or warnings, confirmed live via Unity MCP.
- Live-Editor mouse-click verification of the overlay's on-screen appearance was not performed for the same IMGUI/IMGUI-adjacent tooling reason recorded in "Known limitations" above; a human visual check in the Scene view is recommended before relying on this overlay for level-design decisions.
