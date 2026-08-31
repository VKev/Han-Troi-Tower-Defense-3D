# AI Collaboration Log — Spawn Presentation, Balance Center, Route Authoring — 29 August 2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Area:** enemy spawn presentation, Game Balance Center window, board route authoring
- **Responsible Claude Code session:** `68fb52ae-62d2-44ba-986c-f6ffe45c05fe`
- **Tracking issue:** none; work was directed conversationally
- **Continued from:** an interrupted Codex session. `RoadPathFactory`, `BoardPainterWindow`,
  `EnemySkillEffectView` and the eight board assets were uncommitted work in progress throughout.

This file records consequential design and implementation decisions without reproducing the raw
conversation. A separate Codex ledger, `AI_Collaboration_Log_Session_29_08_2026.md`, covers the
parallel session that was editing the same repository at the same time.

## Entry 1 — Collapse enemy spawn into a single moment

### Problem being addressed

Spawning an enemy produced two separate presentation moments. The prespawn VFX was instantiated,
and a `pendingSpawns` timer in `EnemyViewPool` started the enemy's scale animation 1.6 seconds
later. Independently, `CombatTimelinePlanner` held the enemy motionless for the same 1.6 seconds
through a `SpawnReadyTick` gate, so an enemy appeared to exist before it did anything.

### Prompt used

The project owner asked that instantiating the prespawn VFX and starting the enemy's 0 → 1 scale
happen in the same frame, with the pending reveal mechanism removed so no second spawn moment
could survive. A follow-up asked that the enemy also move immediately, with no hold at all.

### Important AI response

The AI removed `pendingSpawns`, `pendingSpawnSnapshot` and `EnemyView.StartSpawn`, then removed the
`SpawnReadyTick` field, property and both `tick < SpawnReadyTick` gates from the planner. It
flagged that the second gate lived inside `TickEnemyEffects`, not only `MoveEnemies`, so removing
it also starts every skill and effect clock at spawn.

### Option selected, revised, or rejected

- **Selected:** one event — instantiate the effect and bind the view in the same frame.
- **Selected:** delete the simulation hold outright rather than shorten it.
- **Rejected:** keeping a shortened hold as a compromise.

### Rationale

`ProjectileHitPlanner` never modelled the hold, so trajectory planning and simulation disagreed
about where an enemy would be during its first 1.6 seconds. Removing the hold made them consistent.

### Implementation or verification result

`EnemySpawnPresentationTiming` was reduced to the constants still in use. Enemies now move and can
be hit from their spawn tick. Build clean; EditMode suite unaffected at this stage.

## Entry 2 — Place the prespawn effect by tracking, not prediction

### Problem being addressed

With the enemy moving from frame zero, the effect and the enemy drifted apart. Predicting the
meeting point from `BaseMoveSpeed` left a visible offset that persisted through three attempts.

### Prompt used

The owner reported repeatedly that the enemy was not centred in the ring, and finally asked for the
effect to be pushed ahead so the enemy walks through the ring's centre.

### Important AI response

The AI identified the systematic error: `EnemyView.Render` interpolates
`Lerp(PreviousPosition, Position, alpha)`, so the rendered position at wall-clock `t` is the
simulation position at `t − 0.05s`. Every prediction was one simulation tick long. At a 0.1 second
scale window that is a 50 percent error.

### Option selected, revised, or rejected

- **Selected:** the effect tracks `EnemyView.RenderedRootPosition` until its ring fires, then stays
  put, offset ahead by `BaseMoveSpeed × PrespawnLeadSeconds` along `RenderedMoveDirection`.
- **Revised:** an earlier version froze the effect exactly on the enemy, which centred it for one
  frame only.
- **Rejected:** computing the meeting point in `EnemySystem` and passing it through the spawn
  event; this required widening `EnemySpawned`, `IEnemyViewPool.Spawn` and every subscriber, and
  was still wrong by one tick.

### Rationale

Tracking is immune to the render lag, to speed buffs unknown at spawn, to frame-rate variation and
to corners in the road. The plumbing added for the prediction attempt was reverted, leaving the
simulation untouched.

### Implementation or verification result

`EnemyView` exposes `RenderedRootPosition` and `RenderedMoveDirection`. `EnemyViewPool` keeps a
small list of tracking effects ticked from `TickLifecycle`. Final timing: the enemy stays hidden for
0.25s, scales 0 → 1 in 0.15s, reaches full scale at 0.4s exactly as the ring bursts, and crosses the
ring centre around 0.6s.

`PrespawnRingDelaySeconds` must equal the authored `startDelay` of the `circle`, `flash`, `glow`,
`sparkles` and `lines` systems in `VFX_Prespawn.prefab`. Nothing enforces this; the constant carries
a comment saying so.

## Entry 3 — Rebuild the Game Balance Center around tunable rows

### Problem being addressed

The window listed one asset per row and delegated editing to the default Inspector. A catalog of
eight levels therefore read as a single row whose only column said `8 items`, and tower profiles
made of nested structs showed as `…`. None of the numbers a designer balances were visible.

### Prompt used

The owner asked for a table where enemies line up field by field, then for shorter rows, hideable
columns, separate tabs per content type, sections per level and per enemy, and for the tower tab to
expose every number.

### Important AI response

The AI proposed treating the smallest repeated element as the row rather than the asset file, and
flattening nested structs into columns.

### Option selected, revised, or rejected

- **Selected:** the first list-of-structs at each nesting level becomes rows; plain structs flatten
  into columns; arrays of object references stay a single `n items` cell.
- **Selected:** cells draw their own control per property type. `PropertyField` also draws `[Header]`
  and `[Space]` decorators, which was what made every row three lines tall.
- **Selected:** board definitions are excluded from every tab, and the `TowerDefinition` placement
  assets that share `Config/GridPlacement` were moved into the Towers tab.
- **Rejected:** one union table per tab with the asset as the row.

### Rationale

Wave schedules are the clearest case: rows became one per spawn batch, with the schedule's seed and
the wave's reward carried down as context columns that remain editable from any of their rows.

### Implementation or verification result

Tabs are `Levels`, `Waves`, `Enemies`, `Towers`, `Reactions`. Multi-asset tabs group rows under a
per-asset heading. A `Columns` dropdown hides individual fields.

Two IMGUI layout-group mismatches were found and fixed the same way: changing the number of layout
groups mid-event throws `EndLayoutGroup`. Opening or closing the detail panel is deferred to the end
of the frame, and the panel's placement is latched before the toolbar draws.

## Entry 4 — Author routes as ordered cell walks

### Problem being addressed

Enemies ignored the looping roads drawn on several boards. Reconstructing the road maps from the
board assets showed three distinct failures: Level_004 has two closed loops that touch the main path
at exactly one cell; Level_005 has a parallel bypass; Level_007 has a junction where two routes must
diverge toward two different `RoadEnd` cells. Loop cells carried no exit arrow at all.

### Prompt used

The owner asked that enemies follow the loop instead of the shortest path, and chose the ordered
cell list among the offered models.

### Important AI response

The AI explained that one `RoadExitDirection` per cell cannot express either a lap, which needs the
junction cell to be entered twice and exited differently each time, or a per-route fork.

### Option selected, revised, or rejected

- **Selected:** a route is the ordered walk itself, stored as `BoardRouteDefinition` on the board.
- **Rejected:** a second exit per cell, which covers laps but not per-route forks.
- **Rejected:** repainting the three boards so no loop or fork exists.

### Rationale

An ordered walk is the only model of the three that expresses every case already drawn on the
boards, and it is also the most direct thing to author: drag along the road.

### Implementation or verification result

`RoadPathFactory` prefers authored routes, falls back to exit arrows, then to breadth-first search,
so the eight existing boards are unaffected until routes are drawn. Only edge adjacency is checked;
revisiting a cell is legal and is what closes a lap. The Board Painter gained a `Route` brush that
records the walk, shows step numbers, shows `5/12` on a cell walked twice, and reports jumps or a
route that does not start on a spawn.

Two tests were added: `CreatePaths_AuthoredRouteLapsALoop_WalksTheLoopInsteadOfTheShortcut` and
`CreatePaths_AuthoredRouteSkipsACell_ThrowsClearError`. Routes for Level_004, 005 and 007 were
deliberately not authored, because lap direction and the spawn-to-end mapping are design decisions.

## Entry 5 — Diagnose the missing Chicken skill VFX as a timing collision

### Problem being addressed

The owner reported that the Chicken, the `SpeedSupport` enemy, had lost its skill VFX.

### Prompt used

A single report that the effect was gone.

### Important AI response

Rather than change code on suspicion, the AI ran three probes in the Editor and established that
nothing in the chain was broken: the `FX_Chicken` rig emits through `GlobalEffectEmitterView`, the
prefab wiring is intact with `effectPrefab` and `anchor` resolving to `Bone_008`, and driving the
real path `EnemyViewPool.Spawn` then `Render` with a changed `SkillCastVersion` still emits.

The cause is timing, and it follows directly from Entry 1. `activationDelaySeconds` is 0.5, and the
countdown used to start only after the removed 1.6 second hold, placing the cast at 2.1 seconds.
It now starts at spawn, so the cast lands at 0.5 seconds — inside the spawn burst, which fires at
0.4 seconds and stays lit until roughly 0.8.

### Option selected, revised, or rejected

- **Rejected, after trying it:** adding `PrespawnRingDelaySeconds` to
  `SupportActivationRemainingSeconds`. This immediately failed
  `SpeedSupport_AppliesNearbyBuffWhenSkillCastStarts` and `SpeedSupport_StopsMovingDuringSkillCast`,
  which assert the contract that a cast happens after exactly `ActivationDelaySeconds`. Smuggling
  presentation timing into an authored gameplay number is the wrong place for the fix; the change
  was reverted.
- **Selected:** the delay belongs to the effect, not the cast. The parallel Codex session added
  `playDelaySeconds` to `EnemySkillEffectView` for exactly this, authored per prefab.

### Rationale

The two failing tests are the record of a deliberate contract. A presentation problem should be
solved with a presentation knob.

### Implementation or verification result

No simulation change was kept. The knob is `playDelaySeconds` on the `EnemySkillEffectView`
component: `1.0` on `SpeedSupportEnemy.prefab`, `1.833333` on `SummonerBossEnemy.prefab`.

## Verification state at end of session

- `dotnet build TowerDefense3D.slnx` — 0 warnings, 0 errors.
- EditMode — 212 passed, 1 failed. The failure is
  `ApplicationCompositionTests.ApplicationUiPrefab_AuthorsOneReusableButtonPerCatalogLevel`,
  expecting 8 level buttons and finding 2. It belongs to the in-flight Level Menu work and is
  unrelated to this session.
- `BoardAuthoringTests.PainterOptions_ExposeApprovedBasicAndOverlayGroups` had been failing before
  this session because the `Route Arrow` brush was added without updating it; it was updated and now
  passes alongside the new `Route` brush.
- PlayMode tests were not run. `EnemyViewTests.BindAndDeathScale_InterpolatesAroundFootPivot` was
  rewritten against the new spawn timing but has not been executed.

## Open items

1. Author routes for Level_004, Level_005 and Level_007.
2. Run the PlayMode suite.
3. The asset name column in the Balance Center does not freeze when scrolling horizontally.
4. Each Balance Center cell calls `FindProperty` per repaint; the Waves tab holds roughly 64 rows.

## Concurrency note

A second Codex session edited this repository during this one. `GameBalanceWindow.cs` and
`EnemySkillEffectView.cs` both changed on disk mid-session. Read the file on disk before editing
anything in these areas.
