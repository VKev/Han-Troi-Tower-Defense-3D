# AI Collaboration Log — Complete TowerDefense3D Session — 29 August 2026

## Session scope

This ledger records the full connected implementation session, across all chats in the session,
rather than treating each chat as an isolated task. The repository was
`D:/Repos/Bai-Tap/TowerDefense3D`, Unity `6000.3.21f1`, product `FrogGod`. Changes were left
unstaged, uncommitted, and unpushed.

## User requirements and decisions

- Investigate and repair the global-emitter VFX migration, including missing projectile hit and
  elemental reaction effects.
- Correct Water Knock placement and keep only the intended ripple layer.
- Keep mobile particle effects transparent without requiring opaque/depth textures.
- Add stealth toon support with a silhouette while stealth is active, smooth transitions, and
  reveal/re-hide behavior after a Water hit.
- Delay Chicken speed-buff and Mouse boss skill animations; stop the enemy while its skill plays.
- Play Chicken skill VFX at the Chicken head and Mouse boss VFX at the Mouse head.
- Use fixed arrows in Board Painter for road junction decisions.
- Use Wave 2 as the seed for Waves 3–8.
- Support multiple road routes (decision B): each Road Spawn follows authored arrows to a Road End;
  wave enemies rotate deterministically across routes and boss summons retain the boss route.
- Add a designer-facing Balance Center and wire all levels into gameplay.
- Make Balance Center sections and details readable, keep detail on the right, and add authoring
  actions such as Add, Copy, and Delete.
- Consolidate confirmed game-owned runtime VFX roots into `Assets/Resources` while preserving
  vendor/plugin boundaries.
- Keep prespawn VFX and enemy spawn visually synchronized.
- Publish elemental reactions before lethal enemy removal so a final Water → Fire hit still shows
  its reaction.

## Implemented runtime and VFX work

- `GlobalEffectEmitterView` now supports both authored Burst emission and rate-over-time particle
  systems. Rate effects emit an initial particle immediately and continue for the authored duration;
  this restored the main `FX_Chicken` particle, which had rate-over-time but no Burst.
- `CombatTimelineSystem` publishes reaction events before applying lethal frames, allowing reaction
  VFX to run while the enemy view is still active.
- `EnemySkillEffectView` gained a serialized `playDelaySeconds` and schedules the effect at an
  animation-relative time while retaining the authored head anchor.
- Chicken configuration: `SpeedSupport.asset` activation delay is `2.0s`; its
  `SpeedSupportEnemy.prefab` head VFX delay is `1.0s`.
- Mouse boss configuration: `SummonerBossEnemy.prefab` uses `VFX_Boss`, anchor `headend`, and
  `playDelaySeconds = 1.833333s`. The `RoyalShadowRat|Screech` clip is 3s and the sampled head
  peak occurs at approximately 1.833333s.
- Prespawn visual timing is centralized in `EnemySpawnPresentationTiming.cs`: VFX lifetime `2s`,
  ring delay `0.4s`, scale delay `0.25s`, scale duration `0.15s`, and prespawn lead `0.2s`.
  Enemy scale begins with the prespawn event instead of waiting for the effect to finish; gameplay
  frames remain synchronized at the route start.
- Fixed authored road direction data was added to `BoardCellDefinition`, `BoardAuthoringDocument`,
  and `BoardPainterWindow` with a visible Route Arrow overlay.
- `RoadPathSet` and `RoadPathFactory.CreatePaths` support multiple fixed routes. `EnemySystem`,
  `CombatTimelinePlanner`, and `ProjectileHitPlanner` use route-aware movement. VContainer level
  composition explicitly constructs the route-aware systems to avoid selecting legacy constructors.

## Level and authoring work

- Levels 3–8 received Wave 2 seed assets at `Assets/Config/Waves/Level_003_Waves.asset` through
  `Level_008_Waves.asset`.
- Level 3–8 scenes received their `LevelLifetimeScope.levelNumber` and `waveSchedule` references.
- `LevelCatalog.asset` and `ProjectSettings/EditorBuildSettings.asset` now contain Levels 1–8.
- Existing disconnected road data was repaired with the shortest grid bridges: four added Road cells
  in Level 5 and one in Level 7. Fixed arrows were then generated and every board validated as a
  route set: L1–4 = 1 route, L5–7 = 2 routes, L8 = 4 routes.
- `GameBalanceWindow` was reshaped into separate Levels, Waves, Enemies, Towers, and Reactions
  tables. Single-row assets no longer receive confusing section headers; compatible Tower combat
  definitions are kept together, while catalog/rules/placement assets are available through Tower
  Settings. Selected detail always docks to the right.
- Balance Center now exposes Add menus, Wave → Spawn Batch insertion, per-row Copy/Delete with
  Undo, column hiding, validation, and an Open Board Painter action.
- The 8 configured projectile/hit VFX roots were moved with GUIDs preserved into
  `Assets/Resources/Prefabs/VFX/Projectiles/{Fire,Water,Wind,Generator}`. Vendor materials,
  textures, shaders, and scripts remain under `Assets/Plugins` as internal dependencies.

## Verification evidence

- Unity MCP reported the intended Unity editor and verified Edit Mode state before editor mutations.
- Unity compilation/import checks passed after source and asset changes.
- `dotnet build TowerDefense3D.slnx --no-restore` passed with `0 errors` and `0 warnings` on the
  completed builds in this session (one later build output also contained unrelated vendor warnings
  before the final clean build).
- Unity Console checks returned zero project errors after the VFX, route, level, and timing fixes.
- Unity route validation passed for all eight authored boards and game-flow validation passed for
  catalog, scope, scene, and Build Settings wiring.
- The full EditMode suite was started once but the MCP call exceeded its five-minute timeout before
  returning a final result. This is recorded as an incomplete test-run result, not as a pass.
- Better Context was refreshed/verified attempts were made after mutations; the saved map remained
  stale because the checkout contained a large amount of unrelated dirty work. No managed map file
  was hand-edited.

## Important boundaries and follow-up

- Vendor/plugin content under `Assets/Plugins` was not broadly relocated. Only confirmed game-owned
  runtime prefab roots were moved; moving every transitive vendor material/texture/shader would risk
  breaking package ownership and update boundaries.
- Balance Center uses existing Unity inspectors and serialized assets; it does not invent new game
  balance values beyond using Level 2 as the requested seed for Levels 3–8.
- Visual human QA of every level and every skill remains the final designer check, especially the
  shortest-grid bridge repairs in Levels 5 and 7.
