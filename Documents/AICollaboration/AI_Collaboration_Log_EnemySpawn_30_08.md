# AI Collaboration Log — Enemy Spawn Presentation — 30/08/2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Area:** enemy pooling, spawn presentation, deterministic movement, and board route repair
- **Responsible Codex session:** current local session

This record captures the decisions and evidence for the enemy spawn alignment work without reproducing the raw conversation.

## Entry 1 — Align pooled enemy scale, VFX, and Board Painter spawn

### Problem being addressed

Pooled enemies could visually appear to travel between the pool and road. The Basic enemy was the most visible case because its MoonlitMouse skinned-model bounds are evaluated by the Animator. The pre-spawn VFX was also intentionally led ahead of the enemy, so it did not match the RoadSpawn cell painted in Board Painter.

### Prompt used

The project owner requested that the VFX, the enemy scale `0 -> 1` presentation, and the Board Painter spawn marker all use the same location. The owner then approved holding enemy movement through the spawn presentation.

### Important AI response

The investigation separated three locations that had drifted: the Board path starts at the grid cell center, the VFX had a lead-and-follow offset, and the view scale anchor was derived from prefab-renderer bounds. The Basic prefab also has a reusable TrailRenderer, which must not contribute stale bounds to the scale pivot.

### Option selected, revised, or rejected

- **Selected:** create the pre-spawn VFX at the exact `RoadPath.Start` world position and keep it there.
- **Selected:** treat the rendered road position as the enemy body bottom-center anchor, including during scale-in, steady movement, and scale-out.
- **Selected:** activate pooled enemies hidden for one rendered frame before capturing skinned bounds; exclude TrailRenderer and particle renderer bounds from the body pivot.
- **Selected:** keep every enemy stationary for `0.55s` while the spawn presentation runs, with the same delay in runtime movement, combat planning, and trajectory-horizon calculation.
- **Rejected:** visual-only catch-up after spawn, which would have introduced a visible jump and diverged from gameplay state.

### Rationale

One authored cell center is the only stable spawn contract. Pinning the visible body footprint to that point removes prefab-root offsets, while pausing the authoritative movement prevents the simulation from moving away before the scale animation completes. Sharing the delay with deterministic planning keeps projectile and combat timing consistent with what the player sees.

### Implementation or verification result

- `EnemyViewPool` no longer offsets or follows the pre-spawn VFX.
- `EnemyView` waits for Animator bounds before starting scale and keeps the body bottom-center on the rendered road point; the Basic prefab regression test includes a stale TrailRenderer.
- `EnemySystem`, `CombatTimelinePlanner`, and `ProjectileHitPlanner` use `SpawnMovementDelaySeconds` for regular and summoned enemies.
- EditMode coverage includes spawn-delay behavior; the System, Components, EditMode, and PlayMode assemblies compiled with zero errors and zero warnings. Live visual re-verification remains an owner check.

## Entry 2 — Repair authored routes blocking Levels 5 and 6

### Problem being addressed

Level 5 and Level 6 could stop before camera rendering because their board route authoring disagreed with the road cells.

### Prompt used

The project owner reported the runtime route errors while entering Level 5 and Level 6.

### Important AI response

The errors were data failures, not camera or pooling failures. Level 5 had two authored routes that stepped past the RoadEnd onto a CameraFocus-only cell. Level 6 relied on authored exit arrows and its RoadSpawn cell had no exit direction even though its only road neighbor was to the east.

### Option selected, revised, or rejected

- **Selected:** remove the trailing non-road `(49, 25, 0)` cell from both Level 5 routes so both end at RoadEnd `(49, 26, 0)`.
- **Selected:** set Level 6 RoadSpawn `(30, 32, 0)` to `East`, reaching `(31, 32, 0)`.
- **Rejected:** relaxing route validation or treating CameraFocus cells as roads.

### Rationale

The route validator correctly protects gameplay from invalid authoring. Repairing the exact authored data preserves the intended roads and keeps future invalid cells visible as errors.

### Implementation or verification result

- Level 5 routes now end on RoadEnd.
- The Level 6 arrow chain reaches RoadEnd `(49, 25, 0)` in 26 steps with no missing direction or non-road exit.
- `TowerDefense3D.System.Runtime` compiled successfully after both board repairs.
