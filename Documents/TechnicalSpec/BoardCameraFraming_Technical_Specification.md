# Board Camera Framing Technical Specification

| Field | Value |
|---|---|
| Status | Implemented and verified |
| Specification version | 1.0 |
| Approved | 14 August 2026 |
| Unity version | 6000.3.21f1 |
| Owning module | `Assets/_Project/GridPlacement/` |
| Runtime namespace | `TowerDefense3D.GridPlacement` |
| Runtime assembly | `TowerDefense3D.GridPlacement.Runtime` |
| Beads feature | `TowerDefense3D-8m9` |

## 1. Purpose

Board Camera Framing keeps the lowest playable Board level fully visible after the authored Board size, authored cells, Board origin, or mobile viewport changes. It preserves the current perspective composition while moving the scene camera to the closest valid snap position that contains the approved Board footprint.

## 2. Approved scope

- Use the lowest in-bounds Y level that contains at least one `SupportsPlacement` cell.
- Build the horizontal footprint from all in-bounds `SupportsPlacement` or `StaticBlocker` cells on that selected level.
- Ignore all cells on higher levels when calculating camera distance.
- Add one authored cell of horizontal world padding around the footprint so the Board edge remains visible.
- Preserve the assigned Camera rotation, perspective field of view, viewport, and clipping configuration.
- Snap the Camera immediately; do not animate or blend the framing change.
- Respect the current Board transform, including a translated or rotated `Board Origin`.
- Fit within a configurable composition rectangle inside the device safe area.
- Reframe after Editor Board synchronization, at runtime startup, and when relevant viewport, safe-area, Camera-lens, or Board-transform inputs change.
- Keep deterministic bounds selection and perspective-frustum calculations testable without MonoBehaviour lifecycle code.

## 3. Non-goals

- Cinemachine Brain, Cinemachine Camera, priority, blend, or Timeline integration.
- Player-controlled camera pan, zoom, orbit, drag, or gesture input.
- Orthographic-camera framing.
- Camera collision, occlusion avoidance, shake, confining volumes, or cutscene ownership.
- Including towers, enemies, generated renderers, higher Board levels, or arbitrary scene objects in the framing bounds.
- Changing `BoardDefinition`, `BoardCellFlags`, Board geometry generation, Camera field of view, near clip, or far clip automatically.
- Smooth transition or DOTween animation.

## 4. Selected architecture and ownership

| Owner | Lifetime | Responsibility |
|---|---|---|
| Lowest-level bounds calculator | One calculation | Select the approved Board level, merge duplicate authored flags logically, ignore invalid cells, and return integer X/Z extents. |
| Perspective framing solver | One calculation | Convert four world corners and a safe viewport rectangle into the minimum valid Camera position while preserving rotation and field of view. |
| `BoardCameraFramer` | Scene | Own serialized references and tuning, observe relevant runtime input changes, calculate a pose, and apply a snap only when the pose changes. |
| `BoardSceneSynchronizer` | Editor synchronization | Recalculate matching Camera framers after Board visualization synchronization and record Camera movement through Unity Undo. |
| `Main Camera` | Scene | Remain the only output Camera and receive the opt-in `BoardCameraFramer` component. |

The feature uses the existing Camera directly because SampleScene has no Cinemachine owner. It must not add a second transform writer or silently introduce Cinemachine.

## 5. Data contracts

### 5.1 Lowest playable level

1. Inspect `BoardDefinition.Cells` and ignore coordinates outside `BoardDefinition.Dimensions`.
2. Find the minimum Y containing `SupportsPlacement`.
3. On that exact Y only, union coordinates containing `SupportsPlacement` or `StaticBlocker`.
4. Return inclusive minimum X/Z and exclusive maximum X/Z extents.
5. Duplicate coordinates are equivalent to bitwise-OR merged flags.

If no in-bounds `SupportsPlacement` cell exists, framing fails without moving the Camera. The scene component reports one clear warning for that failed framing attempt.

### 5.2 World footprint

- Convert integer bounds to Board-local cell edges, not cell centers.
- Expand minimum and maximum X/Z by the configured cell padding before world conversion.
- Use `BoardDefinition.CellSize` for horizontal scale and the selected level multiplied by `HeightUnit` for local Y.
- Convert all four corners with the `BoardScenePresenter` transform so Board translation, rotation, hierarchy, and scale are respected.
- Do not inspect `Board Visualization` renderer or collider bounds.

### 5.3 Camera framing settings

`BoardCameraFramer` owns:

- an explicit perspective `Camera` reference;
- an explicit `BoardScenePresenter` reference;
- `edgePaddingCells`, default `1.0`;
- a normalized composition rectangle inside the safe area, default `(x: 0.05, y: 0.08, width: 0.90, height: 0.84)`.

Settings are scene configuration, not new ScriptableObject assets.

## 6. Perspective framing contract

The solver receives Camera rotation, vertical field of view, aspect ratio, near clip, the normalized safe framing rectangle, and four world-space Board corners.

It must:

1. Use the average of the four corners as the framing center.
2. Derive horizontal and vertical frustum slopes from field of view and aspect ratio.
3. Account for asymmetric left, right, bottom, and top framing limits.
4. Solve the minimum forward distance satisfying every corner against all four frustum boundaries and the near plane.
5. Offset the Camera along its right and up axes so the Board center projects to the composition rectangle center.
6. Return only a position; Camera rotation and lens settings remain unchanged.
7. Reject invalid perspective, field-of-view, aspect, safe-rectangle, or clipping inputs without applying a partial pose.

The applied pose must place every padded footprint corner in front of the Camera and inside the approved framing rectangle.

## 7. Runtime flow

1. `BoardCameraFramer` validates its serialized Camera and Board presenter references.
2. At runtime startup it calculates and immediately applies the framing position.
3. It caches the inputs that can invalidate the result: Camera aspect/lens, Camera rotation, pixel rectangle, device safe area, Board transform, Board definition, and framing settings.
4. It recalculates only when one of those inputs changes.
5. Idle checks must not allocate or rewrite the Camera transform.
6. A failed recalculation preserves the last valid Camera position.

The feature does not observe generated renderer changes and does not mutate gameplay time or placement state.

## 8. Editor authoring flow

1. Board authoring commits through the existing `BoardChangeScheduler`.
2. `BoardSceneSynchronizer` creates, reuses, or updates Board visualization geometry.
3. It finds loaded `BoardCameraFramer` components that reference the synchronized `BoardScenePresenter`.
4. It calculates the approved snap position after geometry synchronization.
5. It records and applies only a changed Camera transform through Unity Undo and marks the scene dirty.

Editor synchronization must remain disabled while entering Play Mode or compiling. A framing failure must not invalidate or roll back successful Board geometry synchronization.

## 9. Folder and assembly boundaries

```text
Assets/_Project/GridPlacement/
├── Scripts/
│   ├── Board/              Lowest playable-level bounds contract and calculation
│   └── Presentation/       Perspective solver and BoardCameraFramer
├── Editor/BoardAuthoring/  Existing synchronization integration
└── Tests/
    ├── EditMode/           Bounds, solver, and Editor synchronization tests
    └── PlayMode/           Runtime startup and viewport-change tests
```

All new player-build code remains in `TowerDefense3D.GridPlacement.Runtime`. No new runtime assembly is required. Runtime code must not reference `UnityEditor` or Cinemachine APIs.

## 10. Serialized integration

`Assets/Scenes/SampleScene.unity` remains the integration scene.

- Add `BoardCameraFramer` to `Main Camera`.
- Reference the Camera on the same GameObject.
- Reference `Grid Placement/Board Origin` and its `BoardScenePresenter`.
- Preserve the user's current Camera rotation, field of view, clipping planes, and existing scene edits.
- Preserve the existing `GridPlacementController.worldCamera` reference.

No generated Board child is a serialized dependency of the Camera framer.

## 11. Compatibility and migration

- Target only Unity `6000.3.21f1` and the current URP scene.
- Cinemachine `3.1.7` remains installed but unused by this feature.
- Preserve the existing namespace, runtime assembly name, GUID-backed Board asset, and scene references.
- The current Camera is perspective. An orthographic Camera produces a controlled failure instead of an implicit mode change.
- Scene mutation must preserve the pre-existing user-owned `SampleScene.unity` changes.

## 12. Verification plan

### 12.1 Edit Mode

- Select the lowest support level while ignoring higher platforms.
- Include same-level static blockers and ignore out-of-bounds cells.
- Handle duplicate flags, sparse footprints, translated/rotated Board origins, and no-support failure.
- Verify `1x1`, `20x20`, wide, and deep footprints.
- Verify every padded corner lies in the requested rectangle at landscape `16:9`, `20:9`, and `4:3` aspects.
- Verify near-plane containment and invalid projection inputs.
- Verify Board synchronization reframes only matching framers and records a changed Camera pose.

### 12.2 Play Mode

- Verify startup framing uses the serialized scene references.
- Verify a viewport or safe-area input change causes one snap recalculation.
- Verify stable inputs do not continuously rewrite the Camera.

### 12.3 Unity and visual evidence

- Compile in Unity `6000.3.21f1` with no new Console errors.
- Run the complete Grid Placement Edit Mode and Play Mode suites.
- Capture representative Game views and confirm the lowest-level edge remains visible without clipping or UI-safe-area overlap.
- Report the pre-existing `TowerDefense3D-bpw` Play Mode failure separately if it remains; do not hide it or attribute it to camera framing without evidence.

## 13. Risks and mitigations

| Risk | Mitigation |
|---|---|
| A higher platform expands renderer bounds and zooms out | Calculate only from authored cells on the selected lowest level. |
| Board origin changes invalidate absolute test points | Convert Board-local edges through the authored presenter transform. |
| Mobile cutouts or UI obscure the Board | Compose inside `Screen.safeArea` and an additional normalized inner rectangle. |
| Multiple Camera owners fight over transform | Keep SampleScene on one direct Camera owner and do not add Cinemachine. |
| Empty or invalid Board moves Camera unpredictably | Fail without mutation and preserve the last valid pose. |
| Editor synchronization dirties unrelated scenes | Match framers by explicit presenter reference and write only changed transforms. |

## 14. Deferred work

- Smooth Camera transitions.
- Player pan, zoom, orbit, and gesture controls.
- Orthographic mode.
- Cinemachine integration if a future camera-mode coordinator is approved.
- Dynamic framing that includes combat units, towers, projectiles, or effects.
- Physical Android device composition and performance acceptance.

## 15. Implementation status

Implementation completed on 14 August 2026 with no approved-scope deviation.

### Implemented files

- `Assets/_Project/GridPlacement/Scripts/Board/LowestBoardLevelBounds.cs` selects the lowest playable level and its deterministic horizontal footprint.
- `Assets/_Project/GridPlacement/Scripts/Presentation/BoardCameraFramingSolver.cs` constructs transformed Board corners and solves the minimum perspective Camera position.
- `Assets/_Project/GridPlacement/Scripts/Presentation/BoardCameraFramer.cs` owns scene references, safe-area composition, startup snap, and changed-input observation.
- `Assets/_Project/GridPlacement/Editor/BoardAuthoring/BoardSceneSynchronizer.cs` reframes matching loaded Camera components through Unity Undo after Board synchronization.
- `Assets/_Project/GridPlacement/Tests/EditMode/BoardCameraFramingTests.cs` covers level selection, transformed corners, safe-area composition, projection inputs, and landscape aspect ratios.
- `Assets/_Project/GridPlacement/Tests/EditMode/BoardSceneAuthoringTests.cs` verifies matching-presenter Editor synchronization and corner containment.
- `Assets/_Project/GridPlacement/Tests/PlayMode/BoardCameraFramingPlayModeTests.cs` verifies startup snap, stable-input behavior, and a changed mobile viewport aspect.
- `Assets/Scenes/SampleScene.unity` assigns `Main Camera` and `Grid Placement/Board Origin` to `BoardCameraFramer` with one-cell padding and the approved inner composition rectangle.

### Verification evidence

- Unity `6000.3.21f1` compiled the final implementation with zero Console errors.
- Grid Placement Edit Mode passed 24 of 24 tests.
- The new Board Camera Framing Play Mode test passed. The complete Grid Placement Play Mode run passed 3 tests and retained one unrelated pre-existing failure in `GridPlacementSceneInputTests.EditorMouseRelease_PlacesOnceThenRetainsInvalidCandidate`, tracked as `TowerDefense3D-bpw`.
- A direct 1920x1080 Camera capture showed every lowest-level Board edge visible inside the frame.
- The saved `Main Camera` position is approximately `(-5.73, 26.10, -13.06)` while its pre-existing rotation `(59.15, 0.10, 0)`, field of view `43`, near clip `0.1`, and far clip `200` remain unchanged.
- Better Context refreshed all managed maps and verified source hash `38556005203c` after implementation.

### Known limitations

- Physical Android cutout and safe-area acceptance remains deferred to device QA.
- The feature intentionally fails without moving the Camera when references, perspective projection, or the playable Board footprint are invalid.
