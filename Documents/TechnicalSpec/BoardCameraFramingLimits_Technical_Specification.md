# Board Camera Framing Limits Technical Specification

**Status:** Approved
**Implementation status:** Implemented; focused verification passed
**Target Unity version:** 6000.3.21f1

## Purpose

Allow a board designer to limit how many horizontal grid cells the existing perspective board camera frames, without shrinking or otherwise changing the board itself. The approved behavior is a camera-only cap centered on the complete lowest-level playable bounds, applied independently on the two horizontal grid axes.

## Scope

- Add two non-negative authored camera framing limits to `BoardDefinition`: Camera Grid X and Camera Grid Y.
- Expose both values in the Board Painter using designer-facing Grid X and Grid Y terminology.
- Apply each enabled limit to the lowest board level's framing bounds before the existing camera padding and perspective framing calculation.
- Keep the existing direct `Camera` workflow and authoring synchronization.
- Preserve current behavior for existing board assets through an Unlimited default.

## Non-goals

- Cropping, deleting, resizing, or regenerating board geometry.
- Changing colliders, coordinate mapping, occupancy, placement validation, or tower placement availability.
- Preventing gameplay or authored content outside the camera-framed subset.
- Adding camera movement, panning, zoom controls, dynamic tracking, or runtime framing-limit changes.
- Replacing the current camera with Cinemachine or introducing any other camera package.
- Changing the perspective camera's authored rotation, field of view, projection mode, or Safe Area policy.

## Terminology and coordinate mapping

- **Grid X** is the board's horizontal X axis and maps to world X.
- **Grid Y** is the designer-facing name for the board's second horizontal axis. It maps to the existing board depth axis and world Z.
- World Y remains vertical board height and is not controlled by these limits.
- Bounds are expressed as half-open cell intervals: the minimum cell is included and the maximum edge is excluded.

This document uses **Grid Y** in designer-facing labels even when implementation APIs use depth or Z terminology. Implementations must not map the Camera Grid Y value to world Y.

## Authored data contract

`BoardDefinition` owns two serialized integer values:

- **Camera Grid X:** maximum framed cell span along Grid X.
- **Camera Grid Y:** maximum framed cell span along designer Grid Y, which maps to world Z.

Both values have an authored minimum of `0`.

- `0` means **Unlimited**. The full available span on that axis is framed.
- A positive value caps that axis to the smaller of the available span and the authored value.
- A positive value equal to or greater than the available span produces the same framing span as Unlimited.
- The axes are independent; either one may be Unlimited while the other is capped.

The Board Painter must label and describe `0` as Unlimited so designers do not need to infer the sentinel meaning.

## Framing semantics

The cap is centered on the complete lowest playable bounds. For an available half-open interval `[minimum, maximumExclusive)` and a positive limit `limit`, the effective interval is:

```text
fullSpan = maximumExclusive - minimum
effectiveSpan = min(fullSpan, limit)
fullCenter = (minimum + maximumExclusive) / 2
cappedInterval = [fullCenter - effectiveSpan / 2, fullCenter + effectiveSpan / 2)
```

Unlimited uses the original interval unchanged. The camera-only interval may use half-cell edges when the full span and effective span have different parity. This preserves the exact center without changing integer cell coordinates, authored data, geometry, colliders, or placement.

Example: if the lowest level occupies Grid X interval `[0, 80)` and Camera Grid X is `40`, the camera frames `[20, 60)`. If painting expands the full interval to `[0, 100)`, the camera frames `[30, 70)` and its center moves from `40` to `50`. If the full interval is `[0, 81)` with the same limit, the exact centered camera interval is `[20.5, 60.5)`.

The same rule applies independently to designer Grid Y/world Z. Painting toward either horizontal edge changes the complete bounds center and therefore moves the capped camera window to remain centered.

### Edge padding order

The existing `edgePaddingCells` is applied **after** the Grid X and Grid Y caps produce their centered framing rectangle. Padding expands the camera solution around that rectangle; it does not increase the authored cell cap, change its center, or alter which board cells are considered overflow.

### Overflow behavior

Board cells outside the capped camera rectangle remain normal board cells. Their geometry, renderers, colliders, authored flags, coordinate mapping, occupancy, placement validation, preview behavior, and tower placement remain unchanged. Overflow may be outside the initial camera view, but it is neither removed nor made invalid by this feature.

## Camera behavior and compatibility

The existing perspective camera remains directly owned and configured by the scene. Framing may adjust only the position/distance already controlled by the current framing flow. The implementation must preserve:

- the camera's authored rotation;
- the camera's authored field of view;
- perspective projection;
- the existing Safe Area-aware framing calculation;
- the existing `edgePaddingCells` behavior, subject only to its approved post-cap ordering.

No Cinemachine camera, virtual camera, brain, composer, or package dependency is permitted for this feature.

## Ownership and data flow

- **`BoardDefinition`** owns and serializes the two authored camera limit values and exposes their read-only runtime contract.
- **Board Painter** owns designer editing, Grid X/Grid Y labels, Unlimited guidance, non-negative authoring constraints, Undo/dirty handling, and persistence through `BoardDefinition`.
- **`BoardCameraFramingSolver`** owns the deterministic, Unity-scene-independent framing math. It creates exact centered camera-only bounds, including half-cell edges where required, then accounts for padding and the existing perspective/Safe Area inputs.
- **`BoardCameraFramer`** owns runtime scene references and lifecycle. It reads the board definition and lowest-level bounds, gathers the current camera and Safe Area inputs, invokes the solver, and applies the solved camera position without changing rotation or field of view.
- **`BoardSceneSynchronizer`** owns Editor authoring synchronization for the existing scene framing components. It propagates the board reference and preserves the established synchronization behavior; it must not duplicate solver policy.

The flow is `BoardDefinition` authored values -> `BoardCameraFramer` inputs -> `BoardCameraFramingSolver` capped solution -> direct perspective `Camera` position. Board generation, collider generation, and placement systems do not consume the camera limits.

## Serialization and migration

- The new serialized values default to `0`, so every existing `BoardDefinition` asset migrates to Unlimited on both axes and retains its prior framing behavior until a designer explicitly sets a positive limit.
- Existing serialized fields and references must be preserved. No rename or destructive migration is required.
- The Board Painter must modify the definition through Unity serialization with normal Undo and dirty-state support.
- Scene and prefab references remain direct serialized references; no service locator, singleton, runtime registry, or dependency-injection container is introduced.
- A missing value in an older serialized asset is equivalent to `0` and therefore Unlimited.

## Failure and boundary behavior

- An Unlimited or oversized limit must not reduce the available framing bounds.
- A cap must never extend beyond the available maximum-exclusive edge.
- If existing framing prerequisites are unavailable or invalid, the current framer failure behavior remains authoritative; this feature must not fabricate board bounds.
- Extreme aspect ratios or Safe Areas may require more camera distance, but must not change which cells form the capped rectangle.

## Verification plan

Implementation verification must cover these layers independently:

1. Compile the project in Unity `6000.3.21f1` and confirm no new Console errors or warnings are introduced.
2. Run Edit Mode tests for Unlimited values, one-axis and two-axis caps, oversized caps, non-zero and negative minimum coordinates, exact centering across odd/even parity, Grid Y-to-world-Z mapping, camera movement when one edge grows, and padding-after-cap ordering.
3. Verify Board Painter editing, labels, Unlimited guidance, serialization, Undo, and dirty-state behavior.
4. Verify `BoardSceneSynchronizer` preserves and synchronizes the expected board/framer references without changing camera policy.
5. Run relevant Play Mode tests to confirm the direct perspective camera keeps its rotation and field of view, respects Safe Area inputs, and uses the capped rectangle.
6. Regress board geometry, colliders, coordinate mapping, occupancy, placement validation, previews, and placement in overflow cells to confirm they are unaffected.
7. Inspect the serialized integration scene and board definition after implementation, separately from compilation and automated tests.

## Risks and deferred work

- Exact centering can produce half-cell camera bounds when the full span and cap have different parity. Those fractional edges are camera math only and must never be rounded back into grid-cell ownership.
- Post-cap padding can reveal space beyond the logical capped rectangle; tests must distinguish the framed cell span from the padded visual margin.
- Overflow content remains interactive and valid even when it is outside the initial view. Navigation to that content is outside this feature.
- Very narrow aspect ratios, display cutouts, and device-specific Safe Areas can expose composition issues that Editor tests do not prove.
- Physical Android device validation, device screenshots, thermal/performance profiling, and human visual QA are deferred. They must be reported separately and must not be inferred from compilation, Edit Mode tests, Play Mode tests, or Editor inspection.

## Implementation record

The camera-limit architecture, Board Painter contract, and centered-bounds correction are implemented. On 2026-08-14, the project owner rejected the earlier minimum-edge anchor after observing that painting additional cells on the right did not move the camera. `BoardCameraFramingBounds` now preserves the complete playable-bounds center with floating-point camera edges, including half-cell edges when span parity differs. The affected pure, Editor synchronization, and Play Mode tests now require the camera position to move when only the maximum X or designer Grid Y/world-Z edge grows. `BoardDefinition`, Board Painter fields, generated geometry, colliders, placement rules, scene references, camera rotation, and field of view were not changed by this correction.

Unity `6000.3.21f1` imported and compiled the correction with zero Console errors. The Grid Placement Edit Mode suite passed 39 of 39 tests. Both Board Camera Play Mode tests passed, including `Framer_CappedPoseRecentersWhenOnlyMaximumEdgesGrow`. The broader Play Mode suite passed 4 tests and retained the pre-existing `GridPlacementSceneInputTests.EditorMouseRelease_PlacesOnceThenRetainsInvalidCandidate` failure because that test still searches for the removed `Grid Placement Demo/Placed Towers` hierarchy path; this is tracked separately as `TowerDefense3D-bpw` and is not caused by the camera correction. A live 1920x1080 capture confirmed the Camera reframed the current authored Board in Play Mode while retaining rotation `(59.15, 0.10, 0)`.
