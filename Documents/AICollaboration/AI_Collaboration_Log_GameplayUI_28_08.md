# AI Collaboration Log — Gameplay UI — 27–28 August 2026

## Entry 1 — Verify the level economy rules and repair the Cóc HP bar fill

**Responsible session:** `9665e4b3-5f40-4094-8fe2-83e69e41ad45`

### Problem being addressed

Three gameplay rules were requested as if missing: an enemy that reaches the end of the road should reduce the Cóc's HP, buying a tower should cost Gold, and killing an enemy should award Gold. The working tree already contained an uncommitted economy layer, so the first question was whether these rules existed and actually executed on every code path, not whether they needed to be written.

### Prompt used

Enemy reaching the end of the road must cost Cóc HP, buying a tower must cost money, and Gold must increase on every enemy kill.

### Important AI response

Tracing the systems showed all three rules were already implemented and reachable. `EnemySystem.PublishEnemyLeaked` calls `LevelBaseHealthSystem.TakeDamage(LeakDamage)` and `EnemySystem.PublishEnemyKilled` calls `LevelGoldSystem.Add(GoldOnDeath)`; both publish methods are the single funnel for the two removal paths that exist, the direct `EnemySystem.Step` movement path and the planned `CombatTimelinePlanner` path applied through `ApplyPlannedFrame`, so neither outcome can bypass the economy. `TowerNetworkSystem` checks `CanAfford` when a placement drag begins, calls `TrySpend` when the drag is released, and refunds the build cost when the placement fails.

Inspecting the HUD prefab did surface one real defect that made the Cóc HP rule look broken on screen: `Health Fill` was an `Image` with `type = Filled` but `sprite = null`. Unity's `Image.OnPopulateMesh` returns the plain base quad whenever the active sprite is null, so `fillAmount` was ignored entirely and the bar never shrank even though the underlying HP value decreased correctly.

### Option selected, revised, or rejected

- **Selected:** Report the three rules as already implemented, with the authored values, rather than rewriting working systems.
- **Selected:** Assign the builtin `UI/Skin/UISprite.psd` sprite to `Health Fill` so `fillAmount` renders, and apply the same treatment to the new wave-progress fill.
- **Rejected:** Adding a second damage or reward call site, which would have double-charged the Cóc or double-paid Gold on the planned combat path.

### Rationale

The requested behaviour was already correct in the simulation layer, so the only defect worth changing was the presentation bug that hid it. Routing both removal paths through the same publish methods is what makes the single fix sufficient for every enemy type and both movement paths.

### Implementation or verification result

- Confirmed authored values: `leakDamage` 1 for every enemy and 5 for `SummonerBoss`; `goldOnDeath` 10/14/15/18/60/180; starting Cóc HP 10; starting Gold 400 (Level 1) and 500 (Level 2).
- Confirmed tower build costs: Generator 90, the four element towers 70 each, Soul Nexus 0.
- Fixed `Health Fill` so the bar visually tracks HP loss.
- Verification of the economy rules was by code path tracing plus the existing EditMode suite; no Play Mode session was run.

## Entry 2 — Re-author the gameplay HUD into the requested prototype screen layout

**Responsible session:** `9665e4b3-5f40-4094-8fe2-83e69e41ad45`

### Problem being addressed

The HUD packed the tower build buttons, wave controls, selection status, chain status, queue status, and wave preview into one 600x560 panel in the bottom-left corner. A reference screenshot asked for those elements spread across the screen: status cluster top-left, wave panel top-centre, pause top-right, wave preview bottom-left, selection strip and build bar bottom-centre, and wave controls bottom-right. The request was explicitly for prototype layout only, with no generated art and no production UI.

### Prompt used

Lay the UI out like the attached reference image. Layout only — no generated images, no production UI, prototype only.

### Important AI response

Editing 4,000 lines of prefab YAML by hand was rejected in favour of a checked-in Editor tool, `GameplayHudPrototypeLayout`, exposed as **Tools → Tower Defense → Rebuild Gameplay HUD Prototype Layout**. The tool re-authors rects, colours, sprites, and serialized references idempotently, so the layout can be re-tuned by editing numbers and re-running the menu item instead of by hand-editing the asset.

Two hierarchy constraints shaped the result. `TowerNetworkHudViewTests` asserts that `Tower Buttons`, `Unlink`, `Start Wave`, `Selected Status`, and `Chain Status` are direct children of `Tower Network HUD`, and that the panel contains exactly the catalog tower count plus two buttons. `Transform.Find` with a bare name only matches direct children. `Tower Network HUD` was therefore converted from a bottom-left panel into a full-screen transparent container whose named children keep their positions as direct children while backdrop panels sit behind them at lower sibling indices. Its `Image` and `CanvasRenderer` were removed so a full-screen graphic could not swallow board raycasts.

The reference image also implied two systems that do not exist. There is no auto-start countdown, so the "NEXT WAVE IN 0:14" cell became a wave-progress cell driven by the real phase and a `(CurrentWaveNumber - 1) / WaveCount` bar. There is no early-start bonus, so "+40 EARLY BONUS" became "+N CLEAR BONUS" fed by the authored `WaveDefinition.ClearGoldReward`, surfaced through a new `WaveState.NextWaveClearGold`.

### Option selected, revised, or rejected

- **Selected:** Author the layout through a repeatable Editor tool rather than by hand-editing prefab YAML.
- **Selected:** Keep `Tower Network HUD` as the owner of its test-asserted named children and make it a full-screen container, preserving the existing EditMode contract.
- **Selected:** Split `WaveHudState` into explicit slots (wave counter, status, progress, enemies left, start-wave label, bonus, preview) so the view stays a dumb renderer and all formatting stays in `WaveHudPresenter`.
- **Selected:** Drive each build button's cost label from `definition.Core.Economy.BuildCost` through a new `ApplyDefinitionLabels`, so the printed price cannot drift from the tower config.
- **Selected:** Order `Tower Buttons` children by `TowerCatalog.Definitions` order, which both matches the reference image and satisfies the existing test that maps child 0 to definition 0.
- **Selected:** Keep the project's own "CÓC HP" label instead of the reference image's "SOUL CORE".
- **Selected:** Keep `MENU` and `CANCEL` buttons that the reference image does not show, and move `Cancel` into the right-hand control column.
- **Revised:** `Cancel` was first placed at the right end of the bottom-centre selection strip; a geometry check showed it collided with the feedback text, so it moved above `Unlink`, which also freed the strip to 430px of feedback width.
- **Revised:** Backdrop panels were initially appended at the end of the child list and would have drawn over their own content; an explicit sibling ordering pass now puts every backdrop before the text and controls above it.
- **Rejected:** Implementing a wave countdown timer or an early-start bonus, since both are new gameplay and the request was layout only.
- **Rejected:** Setting text on `Text` components from `OnValidate`, which risks Unity's import-time SendMessage warnings.

### Rationale

An Editor tool keeps the prototype layout cheap to iterate, which matters more than a one-off asset edit when the layout is explicitly a prototype. Honouring the existing hierarchy assertions avoided rewriting passing tests to fit a new asset shape. Substituting real data for the two invented widgets keeps the panel honest instead of displaying a timer and a bonus that nothing computes.

### Implementation or verification result

- Added `Assets/Scripts/Editor/GameplayUIAuthoring/GameplayHudPrototypeLayout.cs` and rebuilt `Assets/Resources/Prefabs/GameplayUI.prefab` through it.
- Changed `WaveState`, `WaveSystem`, `WaveHudState`, `WaveHudPresenter`, `WaveHudView`, and `TowerPlacementDragButtonView`; deleted the `Instructions` bar and the `Title` label and repointed `PlacementHudView.root` at `Selected Panel`.
- Verified draw order, sibling order, serialized wiring, and the catalog-ordered button row by parsing the saved prefab; verified the six cost labels read 90/70/70/70/70/0 from the tower configs.
- Rendered an SVG preview computed from the saved prefab's own rects at 1920x1080 to confirm no element overlapped or left the screen; this validates geometry only and not Unity's real font metrics.
- 340 of 340 EditMode tests passed after the relayout, including the updated `WaveHudViewTests`.
- No Play Mode session was run, so the layout was not confirmed against live font rendering or a real device safe area.

## Entry 3 — Add a shared level outcome panel for victory and defeat

**Responsible session:** `9665e4b3-5f40-4094-8fe2-83e69e41ad45`

### Problem being addressed

Clearing every wave or losing all Cóc HP only changed a HUD label. The player had no way to replay the level, advance to the next level, or return to level selection without leaving through the menu button. Victory was requested first; defeat with replay and level-select was requested immediately afterwards.

### Prompt used

On win, show a victory UI with buttons to play again, continue to the next level, or return to level selection. Then: show a defeat panel too, with play again or level select.

### Important AI response

`GameFlowSystem.BeginLevelLoad` was gated on `State == GameFlowState.LevelMenu`, so no level could be started from inside gameplay even though the state machine already allowed `Gameplay -> LoadingLevel`. Relaxing that guard was enough, because `LevelLoadSequence` already unloads the active level before loading the requested one; no separate unload step was needed. Each new entry point re-checks `State == Gameplay`, so a second click cannot start a second transition once the first has moved the state to `LoadingLevel`.

Victory was implemented first as a `VictoryHud*` set. When defeat was requested, that set was generalised into a single `LevelOutcomeHud*` panel carrying a `LevelOutcome` enum, rather than duplicating a second card, presenter, view, and set of button wiring. The next-level button is hidden rather than disabled so the `HorizontalLayoutGroup` re-flows the two remaining buttons, and `LevelOutcome` is passed to the view so the accent colour stays a presentation concern instead of leaking `Color` into the system layer.

### Option selected, revised, or rejected

- **Selected:** Allow `BeginLevelLoad` from `Gameplay` as well as `LevelMenu`, and add `RequestReplayLevel` and `RequestPlayNextLevel` guarded on the gameplay state.
- **Selected:** Add `LevelCatalog.TryGetNextLevel`, returning the lowest authored level number above the current one, so play order does not depend on list order.
- **Selected:** Route next-level through `LevelMenuFlow.PlayLevel`, which unlocks and saves the target level first and raises the existing save warning on a write failure instead of blocking play.
- **Selected:** Drive the panel from the existing `waveSystem.StateChanged` dirty flag through `GameplayUISystem.RefreshIfDirty`, so there is no per-frame work.
- **Selected:** Put the panel under `Safe Area` rather than under `Tower Network HUD`, keeping the existing button-count assertion valid, and give the dim overlay `raycastTarget = true` to block board input while it is up.
- **Selected:** Keep the view owner `Outcome HUD` active and toggle only its `Outcome Root` child, so `RegisterComponentInHierarchy` can still resolve the view.
- **Revised:** The victory-only `VictoryHudView`, `IVictoryHudView`, `VictoryHudState`, and `VictoryHudPresenter` were superseded by the shared `LevelOutcomeHud*` set once defeat was requested.
- **Revised:** The next-level button changed from disabled-when-unavailable to hidden-when-unavailable so the button row re-flows on defeat and on the final level.
- **Rejected:** A second parallel defeat view and presenter, which would have duplicated the card, the three button wirings, and the flow calls.

### Rationale

One outcome panel with an outcome enum expresses the difference between winning and losing as data rather than as duplicated presentation. Reusing `LevelMenuFlow` for the unlock-then-load path keeps progression rules in the flow that already owns them. Reusing the existing dirty-flag refresh avoids introducing a polling system for an event that fires at most once per level attempt.

### Implementation or verification result

- Added `LevelOutcomeHudView`, `ILevelOutcomeHudView`, `LevelOutcomeHudState` with the `LevelOutcome` enum, and `LevelOutcomeHudPresenter`.
- Changed `GameFlowSystem`, `LevelMenuFlow`, `LevelCatalog`, `GameplayUISystem`, `LevelLifetimeScope`, `LevelCatalogValidator`, and the HUD layout builder.
- The victory stage was verified before the defeat generalisation: 344 of 344 EditMode tests passed and `Validate Game Flow` passed against both level scenes.
- The defeat stage was blocked for a period because Unity will not reload the script domain while a test assembly fails to compile, so the prefab still held the superseded `Victory HUD` node. Entry 5 records how that was cleared.
- After the unblock, the prefab holds `Outcome HUD` with `Outcome Root` inactive, the stale `Victory HUD` node is gone, `Validate Game Flow` passes, and 187 of 187 EditMode tests pass, including the six new `LevelOutcomeHudTests` cases covering hidden-while-playable, victory with next level, defeat without next level, next level hidden on the final level, the prefab wiring, and `TryGetNextLevel` ordering.
- No Play Mode session was run, so the panel was not observed live; the outcome path is covered by presenter and prefab tests only.

## Entry 4 — Session working-tree and commit status

**Responsible session:** `9665e4b3-5f40-4094-8fe2-83e69e41ad45`

### Problem being addressed

A request to commit the session's work feature by feature could not be satisfied cleanly, because the repository working tree already held a large uncommitted change set from earlier sessions before this session began.

### Prompt used

Commit each feature and write to the AI collaboration log. Then: just write this whole chat session to the AI collaboration log, never mind that task.

### Important AI response

At session start the working tree already contained roughly 86,000 uncommitted insertions across 70 tracked files, including `Assets/Scenes/Levels/Level_001.unity`, the enemy element-effect and VFX work, and the untracked `Assets/Scripts/System/Economy/` folder. That pre-existing work overlaps the same files this session edited, including `WaveSystem.cs`, `GameplayUISystem.cs`, `LevelLifetimeScope.cs`, `LevelCatalog.cs`, and `GameplayUI.prefab`, so no per-feature commit could be produced without either mixing in unreviewed prior work or performing hunk-level surgery on a scene and a prefab.

### Option selected, revised, or rejected

- **Rejected:** Committing the whole tree under this session's feature messages, which would have attributed unrelated in-flight work to these features.
- **Selected:** Skip committing entirely, per the follow-up instruction, and record the whole session here instead.

### Rationale

Feature commits are only useful if each one describes what it actually contains. With this session's edits interleaved with a much larger pre-existing change set in the same files, any grouping would have been misleading, and unpicking it later would cost more than leaving the tree as found.

### Implementation or verification result

- No commit was created and nothing was staged; the working tree was left as found apart from this session's own edits.
- Entries 1 to 3 above record the session's changes.

## Entry 5 — Reconcile the HUD with the Fire/Water/Wind combat reduction

**Responsible session:** `9665e4b3-5f40-4094-8fe2-83e69e41ad45`

### Problem being addressed

Two failures surfaced that this session had not caused. `EnemyDataDefinitionTests` could not compile against `EnemyDefinition.PushDistanceMultiplier`, which blocked every script reload and therefore blocked the HUD builder. Once that cleared, `TowerNetworkHudViewTests` failed with "Expected: 7, But was: 8" on the tower-panel button count.

### Prompt used

Fix the current errors. Then: read the recent beads for more detail.

### Important AI response

Reading the Beads history explained both. Closed issue `TowerDefense3D-df28`, "Rút gọn combat còn Fire Water Wind", trimmed the combat model: `Assets/Config/Towers/Definitions/Elements/Earth.asset` was deleted and `TowerCatalog` dropped to five definitions, while `EnemyDefinition` lost its resistance and received-effect multiplier block in favour of `thermalShockHitsToBreakShield`. Both failures were the untouched leftovers of that change.

A repository-wide search confirmed the two stale assertions in `EnemyDataDefinitionTests` were the only remaining references to `PushDistanceMultiplier` in any script or asset, so the removal was complete and deliberate and the test, not the definition, was the stale side.

The button-count failure was a real defect in this session's builder rather than a stale test. `BuildTowerBar` ordered and restyled every button whose definition is still in the catalog, but never removed buttons whose definition had left it, and never rewrote `TowerNetworkHudView.towerDragButtons`. The prefab therefore kept an `Earth` button holding a now-null definition, and the serialized array still listed six entries.

### Option selected, revised, or rejected

- **Selected:** Replace the two stale assertions with `LeakDamage` assertions on the mini-boss and summoner boss, preserving the test's purpose of pinning authored per-enemy threat values.
- **Selected:** Make the builder prune any `Tower Buttons` child left past the catalog-ordered run, and rewrite `towerDragButtons` from the catalog, so the build bar is self-healing when the catalog changes.
- **Rejected:** Restoring `PushDistanceMultiplier` and the surrounding resistance and multiplier fields, which would have resurrected a model that `df28` deliberately removed.
- **Rejected:** Relaxing the `catalog.Definitions.Count + 2` assertion, which was correctly reporting a stale prefab.

### Rationale

The failing assertion was doing its job: it caught a dead build button that would have dragged a null tower definition onto the board. Fixing the builder rather than the assertion keeps that guarantee, and pruning by catalog membership means a future tower addition or removal needs one menu run instead of manual prefab surgery.

### Implementation or verification result

- Edited `EnemyDataDefinitionTests` lines 45 and 47 to assert `miniBoss.LeakDamage == 1` and `summonerBoss.LeakDamage == 5`, matching the authored configs.
- Added catalog-membership pruning and `SetObjectReferenceArray` to `GameplayHudPrototypeLayout`; the rebuild logged the removal of the stale `Earth` button.
- Verified the prefab now has five build buttons ordered Generator, Fire, Water, Wind, Soul Nexus, with `towerDragButtons` listing the same five and no `Earth` object anywhere.
- 187 of 187 EditMode tests pass and `Validate Game Flow` passes.
- The EditMode total fell from 344 to 187 across this session because `df28` removed the resistance and multiplier test coverage; that reduction is not attributable to this session's changes.

## Entry 6 — Fix the permanent lift lock that made Start Wave dead

**Responsible session:** `9665e4b3-5f40-4094-8fe2-83e69e41ad45`

### Problem being addressed

In Play Mode on Level 1 wave 4, clicking START WAVE did nothing and repeatedly threw `InvalidOperationException: Combat planning exceeded its deterministic horizon of 1756 ticks` from `CombatTimelinePlanner.Create`. The HUD showed "Tower simulation started." while wave progress still read READY TO START, so the board was also wedged: towers could no longer be edited, and every further click threw again.

### Prompt used

Cannot click start wave, clicking does not start the wave, is something wrong? (with the exception text and a screenshot of wave 04/08.)

### Important AI response

The planner simulates the whole wave before it starts and gives up after `lastMovementTick * 4 + 200` ticks. It exits early only when every enemy has died or leaked, so the horizon can only be exceeded if an enemy stops making progress.

The numbers identified the stall. `WaterWind_Lift` lifts a regular enemy for `1.5` seconds and deals `0` damage, while every element tower has `cycleIntervalSeconds` of `0.85`. A Water into Wind chain therefore re-applies the lift roughly every 0.85 seconds on top of a 1.5 second lift, so the enemy is permanently airborne. `MoveEnemies` skips any enemy while `tick < LiftEndTick`, so it never advances, never leaks, and never dies because the lift does no damage. Worse, being frozen inside tower range guarantees it keeps being re-lifted. Wave 4 is eight Stealth enemies, rank Regular, which become eligible for reactions each time a direct hit reveals them.

The wedged board was a second, separate defect. `WaveSystem.TryStartWave` calls `towerNetworkSystem.TryStartSimulation` before raising `WavePlanCreated`, and the planner exception escaped between those two steps, so the simulation stayed started while the wave phase never advanced to Running.

### Option selected, revised, or rejected

- **Selected:** Add an authored `liftImmunitySeconds` to `ElementReactionDefinition`, defaulting to `1.5`, and make the planner refuse a new lift until the previous lift plus its immunity window has elapsed.
- **Selected:** Reject an authored lift that has no immunity window in `CollectValidationErrors`, so the configuration cannot reintroduce the lock.
- **Selected:** Catch the planning failure inside `TryStartWave`, stop the simulation, clear the plan, and return the message through the existing `out string error` contract so the HUD reports it instead of the click throwing.
- **Rejected:** Raising the planning horizon, which only delays an unbounded loop rather than ending it.
- **Rejected:** Treating a stalled enemy as leaked inside the planner, which would hide a real design hole behind a silent rule.
- **Rejected:** Removing the lift or setting its duration to zero, which is a balance decision that belongs to the designer.

### Rationale

Lift is the only crowd control that both stops movement and deals no damage, so it is the one effect that can produce an enemy which never resolves. An immunity window is the standard diminishing-returns answer and guarantees forward progress for any positive value, which is what makes planning terminate. Keeping the number in the reaction asset rather than in code leaves the balance in the designer's hands. Separately, a failed `TryStartWave` must leave the board exactly as it was; leaking a started simulation on the failure path is what turned a recoverable error into a stuck session.

### Implementation or verification result

- Added `liftImmunitySeconds` to `ElementReactionDefinition` with validation, authored `1.5` on `WaterWind_Lift`, and `0` on the two reactions that do not lift.
- Replaced the inline lift branch in `CombatTimelinePlanner.ApplyReactionToEnemy` with `ApplyLift`, tracking `LiftImmuneUntilTick` per shadow enemy. With a 1.5 second lift and a 1.5 second window, lift uptime is capped near 50% instead of 100%.
- Made `TryStartWave` roll back the simulation and report planning failures instead of throwing out of the button callback.
- Added `ElementReactionAssetTests` covering catalog validation and the rule that every lifting reaction grants immunity.
- 189 of 189 EditMode tests pass.
- The fix was derived from the authored numbers and the planner's movement rule, not reproduced in a Play Mode session; wave 4 with a Water into Wind chain still needs a live retest.

## Entry 7 — Make lift outclass plain wind knockback, on a progress guarantee

**Responsible session:** `9665e4b3-5f40-4094-8fe2-83e69e41ad45`

### Problem being addressed

The lift immunity window added in Entry 6 stopped the hang but capped lift uptime at 50%, which left the Water into Wind reaction looking weak next to simply firing a Wind tower. A Water into Wind chain costs two towers and a link, so it has to be clearly better than the 0.5 metre knockback a single Wind tower already applies for free.

### Prompt used

Balance it so the lift carries more advantage than a normal wind knockback. Chosen direction: increase the lift duration.

### Important AI response

Checking the knockback baseline before tuning exposed a second instance of the same failure class. A Wind tower pushes 0.5 metres every 0.85 seconds, which is 0.588 metres per second of denied ground per tower, and `ApplyPush` had no ceiling. Three Wind towers firing at one Armored enemy, which walks at 1.7 metres per second, drag it backwards permanently. That stalls combat planning exactly like the lift lock did, with no lift involved, so any lift number chosen in isolation could still be broken by building more Wind towers.

The order therefore had to be structural first: guarantee forward progress, then tune balance freely on top. Knockback now spends a budget that refills at a fraction of the enemy's own move speed, so any number of pushing towers together can never exceed that fraction. Lift uptime is already bounded by its immunity window. The two bounds multiply into a guaranteed progress fraction, and the planning horizon is now derived from that fraction instead of the hardcoded multiplier of four, so a legal but heavily controlled wave gets planned rather than rejected.

With the floor in place the lift could be lengthened as requested: 1.5 to 2.5 seconds against an unchanged 1.5 second immunity window, raising uptime from 50% to 62.5% and making each stop visibly longer.

### Option selected, revised, or rejected

- **Selected:** Cap knockback speed at `maximumPushSpeedFraction` of the enemy's own move speed, authored at `0.4` on `TowerCombatRules`, with the budget capped at one second's allowance so an unharassed enemy cannot be shoved a long way at once.
- **Selected:** Derive the planning horizon from `(1 - liftUptime) * (1 - pushCeiling)` and throw a named configuration error if that product reaches zero, replacing the magic multiplier of four.
- **Selected:** Raise `WaterWind_Lift.liftDurationSeconds` from `1.5` to `2.5`, keeping immunity at `1.5`.
- **Rejected:** Adding airborne damage vulnerability or a lift-specific knockback, which the designer did not pick.
- **Rejected:** Tuning the lift without the push ceiling, which would have left the stall reachable through Wind towers alone.

### Rationale

A balance number is only meaningful if the simulation is guaranteed to terminate whatever the player builds; otherwise every value is one extra tower away from a crash. Capping knockback by a fraction of the target's own speed scales per enemy without a table, and keeps a single Wind tower unaffected because 0.588 is already below the 0.8 ceiling a 2 metres per second enemy allows. The ceiling also does the balance work the designer asked for: knockback now saturates as Wind towers are added, while lift is time-based and does not, so the reaction is the better investment by structure and not only by tuning.

### Implementation or verification result

- Added `maximumPushSpeedFraction` to `TowerCombatRules`, exposed it through `TowerNetworkManager`, and made `CombatTimelinePlanner.ApplyPush` spend a per-enemy refilling budget.
- Replaced the hardcoded horizon multiplier with `CalculateGuaranteedProgressFraction`.
- Authored `liftDurationSeconds: 2.5` on `WaterWind_Lift` and `maximumPushSpeedFraction: 0.4` on `TowerCombatRules`.
- Resulting guarantee: lift uptime 62.5%, push ceiling 40%, so an enemy always keeps 22.5% of its move speed and the horizon grew from 4x to about 4.44x the unimpeded travel time.
- Resulting advantage for a 2 metres per second enemy: lift denies about 1.25 metres per second of ground versus 0.588 for a plain Wind hit, roughly 2.1x, and the lift hit still applies its own knockback on top.
- Added an asset test asserting the authored lift and push ceilings leave a positive progress fraction; 190 of 190 EditMode tests pass.
- Still not reproduced in Play Mode; wave 4 with a Water into Wind chain needs a live retest.

## Entry 8 — Strip the thermal shock shadow and give Firestorm a wind swirl effect

**Responsible session:** `9665e4b3-5f40-4094-8fe2-83e69e41ad45`

### Problem being addressed

The thermal shock effect cast a visible shadow on the ground, unlike the other reaction effects. Separately, the Fire plus Wind reaction had no authored effect at all, so Firestorm played nothing on the enemy.

### Prompt used

The thermal shock effect currently has a shadow, remove it. Then: give the fire and wind reaction the vfx_WindSwirl effect, placed at the enemy's effect spawn position.

### Important AI response

`VFX_SocNhiet` holds four particle systems, of which the root and `Smoke` had `m_CastShadows: 1` while `Circle` and `Sparks small` were already off. Comparing against `VFX_Fire` and `VFX_FirePurple`, which have no shadow-casting renderers at all, showed thermal shock was the outlier rather than the convention, so switching the two renderers off matched the effect family instead of inventing a new rule. The prefab has no Light component, which confirmed the shadow came from the particle renderers.

The Firestorm wiring was not a simple asset assignment. `EnemyElementEffectView.reactionEffects` stores each effect as a GameObject that lives inside the enemy prefab as a nested prefab instance, which the view toggles and repositions rather than instantiating on demand. Every enemy therefore needs its own nested copy of `vfx_WindSwirl`, and hand-authoring nested `PrefabInstance` blocks across seven prefabs in YAML would have been both tedious and easy to corrupt. An idempotent Editor tool was written instead, mirroring the approach already used for the HUD layout.

Placement `EnemyPosition` was selected because it is the value that reads `reactionEffectSpawnPoint`, which is what the request asked for; the effect detaches at that point rather than following the enemy.

### Option selected, revised, or rejected

- **Selected:** Set `m_CastShadows` to `0` on the two `VFX_SocNhiet` renderers that still cast, leaving `m_ReceiveShadows` untouched because all four were already `0`.
- **Selected:** Add `EnemyReactionEffectWiring` as **Tools → Tower Defense → Wire Firestorm Wind Swirl Effect**, reusing whatever parent the existing reaction effects sit under so each enemy keeps its authored rig.
- **Selected:** Author the Firestorm entry with placement `EnemyPosition` and `durationSeconds` 2, matching `FireWind_Firestorm.burnDurationSeconds`.
- **Rejected:** Hand-editing nested prefab instances across seven enemy prefabs in YAML.
- **Rejected:** Changing the fire effects to match thermal shock, since they already followed the intended no-shadow convention.

### Rationale

Reaction effects are per-enemy nested instances by existing design, so the wiring belongs in a repeatable tool that can be re-run when enemies are added or the effect is swapped. Deriving the parent from the already authored entries avoids assuming a fixed hierarchy path that differs between enemy rigs. The runtime prefers the event's own duration over the authored fallback, so the authored two seconds only matters if the reaction ever fires without a burn duration.

### Implementation or verification result

- `VFX_SocNhiet.prefab` diff is exactly two lines; all four particle systems now report `cast=0 recv=0`, and Unity reimported without errors.
- The wiring tool reported all seven enemy prefabs wired: ArmoredEnemy, BasicEnemy, MagicResistant 1, MiniBossEnemy, SpeedSupportEnemy, Stealth 1, SummonerBossEnemy.
- Verified per prefab that `reactionEffects` now holds reaction `0` at placement `2` with duration `0.5` and reaction `1` at placement `2` with duration `2`, each pointing at a distinct in-prefab object.
- Verified on `BasicEnemy` that the nested `vfx_WindSwirl` instance is named correctly, starts inactive, and shares the same parent transform as the other five nested effect instances.
- 190 of 190 EditMode tests pass.
- Neither change was observed in Play Mode; the shadow removal and the Firestorm effect both still need a visual check in game.

## Entry 9 — Match the Firestorm radius to its effect, and add lift and shield feedback

**Responsible session:** `9665e4b3-5f40-4094-8fe2-83e69e41ad45`

### Problem being addressed

Four related presentation gaps. The Firestorm radius was authored at 2 metres with no relation to how far its effect actually reaches, and the visible reach depends on the particle material and texture rather than the shape module, so it had to be measured rather than derived. The Water plus Wind reaction had no effect and no visual sign that the enemy had been thrown. The Magic Resistant enemy had gained a shield mesh with no behaviour: it never faded, never reacted to the thermal shock that breaks it, and never switched off.

### Prompt used

Make the Firestorm radius match the effect's reach, and do it visually because the effect's radius is not the particle radius, the material matters too. Then: the water and wind reaction should use the water splash effect played where the enemy was hit, and should throw the enemy up visually. The Magic Resistant enemy now has a shield effect; switch it off when the shield breaks, flash its colour while a thermal shock is damaging it, and fade it to represent shield health, half opacity after the first hit and gone after the second.

### Important AI response

Measuring by render exposed a blocking problem first. The same effect covered wildly different ground per enemy - world scale 1.54 on Basic through 3.08 on the Summoner Boss - because each nested copy kept the prefab's local scale while the enemy rigs it was parented into are scaled differently. That variation was accidental rather than authored, and while it stood no single radius could match what the player sees, so the wiring tool now cancels the parent scale and lands every copy on one shared world size.

The first measurement attempt reported an identical 4.23 metres at every brightness threshold, which is the half-diagonal of the capture frame: the effect overflowed the camera. Widening the frame and switching from absolute cutoffs to fractions of the effect's own peak brightness, plus the radius containing 90 per cent of all emitted light, produced a stable reading.

For the lift, the enemy's vertical offset is computed in the planner rather than the view, matching how every other enemy property already reaches presentation. The planner is deterministic and precomputed, so the arc is part of the plan and the view stays a renderer. Both the lift height and the remaining shield hits had to be added to `EnemySnapshot`, which is what the view actually receives; shield hits already reached `EnemyInstance` but stopped there.

### Option selected, revised, or rejected

- **Selected:** Normalise every nested reaction effect to world scale 1.54, the artist's authored size, before measuring anything.
- **Selected:** Set `radiusMeters` to 4.45, the radius containing 90 per cent of the effect's light, which tracks the edge of the dense swirl rather than its faint outer haze.
- **Selected:** Carry the lift arc as `LiftHeightMeters` from planner through frame, instance, and snapshot, using `4t(1-t)` so the enemy leaves and lands on the ground cleanly.
- **Selected:** Play the water splash at `ReactionPosition`, the hit point, and the wind swirl at `EnemyPosition`, the enemy's effect spawn point.
- **Selected:** Drive shield opacity from remaining hits over maximum hits, which gives exactly the requested half then gone on a two-hit shield without a second authored number.
- **Selected:** Fold the shield wiring into the same enemy effect tool, attaching the view only where a shield mesh exists and removing it where one does not.
- **Revised:** The first capture used a 6 metre frame and absolute brightness cutoffs; both were replaced after the readings turned out to be measuring the frame instead of the effect.
- **Rejected:** Reading the radius from the particle shape module or renderer bounds, since neither accounts for transparent padding in the texture.
- **Rejected:** Shrinking the effect to the authored 2 metre radius, which is the opposite direction to the one requested.

### Rationale

Deriving the gameplay radius from a render is the only honest way to match what the player sees when the material decides the visible extent. Normalising scale first is what makes a single authored radius meaningful across enemies. Computing the lift arc in the planner keeps the simulation the single source of truth for enemy position, and expressing shield opacity as a ratio of remaining hits means the fade automatically follows the authored hit count instead of hard-coding two steps.

### Implementation or verification result

- Confirmed all seven enemies now carry the effect at world scale 1.54.
- Measured at that scale: 50 per cent of peak brightness at 4.41 metres, 90 per cent of total light at 4.55 metres, 25 per cent of peak at 4.93 metres, 10 per cent at 5.22 metres. `FireWind_Firestorm.radiusMeters` set to 4.45, up from 2, which is roughly five times the area.
- Wired all seven enemies with Firestorm at `EnemyPosition` for 2 seconds and WaterLift at `ReactionPosition` for 1 second.
- `EnemyThermalShieldView` attached to Magic Resistant only, which is also the only enemy authored with a non-zero `thermalShockHitsToBreakShield`.
- Added `EnemyThermalShieldViewTests` covering the prefab wiring, the full then half then hidden fade, and unshielded enemies; 193 of 193 EditMode tests pass.
- The measurement image with the authored radius drawn over the effect was produced for review.
- No Play Mode session was run, so the lift arc height, the splash placement, and the shield flash colour and timing have not been seen in motion.

## Entry 10 — Move the Water plus Wind effect to the water knock played at the enemy's feet

**Responsible session:** `9665e4b3-5f40-4094-8fe2-83e69e41ad45`

### Problem being addressed

The Water plus Wind reaction had just been given the water splash effect at the hit position. The requested effect is the water knock instead, spawned at the enemy's feet.

### Prompt used

Change it: water and wind should now use the water knock effect, spawned under the enemy's feet.

### Important AI response

None of the three existing placements expressed "at the enemy's feet". `ReactionPosition` uses the projectile hit point, which is wherever the shot connected rather than the ground. `EnemyPosition` uses the authored `reactionEffectSpawnPoint`, which is a raised anchor. `FollowEnemy` keeps the effect parented, which is actively wrong here: the lift added in the previous entry raises the enemy view, so a parented effect would fly upwards with it instead of staying in the water it splashed out of.

A fourth placement, `EnemyFeet`, was added. It reads the enemy root's position, which sits on the ground, and then detaches like the other detached placements. Because the reaction fires on the first tick of the lift, when the arc height is still zero, that read lands exactly at ground level and the effect stays there while the enemy is thrown up. The value was appended to the end of the enum so the placement integers already serialised in the enemy prefabs keep their meaning.

The now-unused water splash instances would have stayed behind as dead nested prefabs, so the wiring tool gained a retired-effect list that deletes them.

### Option selected, revised, or rejected

- **Selected:** Add `EnemyFeet` as a fourth placement, reading the enemy root position and detaching.
- **Selected:** Append the new enum value rather than inserting it, so existing serialised placements are unaffected.
- **Selected:** Have the wiring tool delete retired effect objects, listing `VFX_WaterSplash` as the first entry.
- **Rejected:** Reusing `FollowEnemy`, which would drag the ground splash upwards with the lifted enemy.
- **Rejected:** Reusing `EnemyPosition`, whose authored anchor is raised off the ground.

### Implementation or verification result

- `EnemyElementEffectPlacement` gained `EnemyFeet`; `PositionReactionEffect` now resolves through a switch rather than a two-way ternary.
- All seven enemy prefabs verified: WaterLift now points at `VFX_WaterKnock` with placement `EnemyFeet`, and no `VFX_WaterSplash` object remains in any of them.
- Thermal shock and Firestorm placements are unchanged at `EnemyPosition`.
- 193 of 193 EditMode tests pass.
- Not seen in Play Mode; whether the knock reads as being under the feet at the authored world scale still needs a visual check.

## Entry 11 — Confirm reaction effects are already pooled with the enemy, and lock the invariant

**Responsible session:** `9665e4b3-5f40-4094-8fe2-83e69e41ad45`

### Problem being addressed

A request to pool the reaction effects, reasoning that if the lift, Firestorm and thermal shock effects each live inside every enemy then pooling the enemy pools the effects too.

### Prompt used

I need to pool my VFX. Should the lift effect be in each enemy? Firestorm and thermal shock too, so that pooling the enemy pools the effects as well.

### Important AI response

That is already exactly how it works, so the answer was to verify the whole chain rather than build anything. `EnemyViewPool` keeps a `ComponentPool<EnemyView>` per enemy definition and calls `Instantiate` only inside `CreateView`, on a pool miss. Every reaction effect is a nested prefab instance inside the enemy view prefab, so it is created once with the pooled enemy and reused for its lifetime. Playback is `SetActive` plus particle play, never instantiate or destroy: a repository search for `Instantiate` and `Destroy` across the enemy components and systems returns only the two calls inside `CreateView`.

The one mechanism that could leak an effect out of the pooled hierarchy is the detached placements. `PositionReactionEffect` calls `SetParent(null, true)` for anything not following the enemy, which is required so a ground effect stays put while the lift throws the enemy upwards. `Deactivate` reverses it, restoring the authored parent, local position, rotation and scale, and `EnemyElementEffectView.Release` runs `ResetEffects`, which deactivates the element roots, the active and pending reaction roots, and every entry in the overlay reaction list before clearing it. So a detached effect is always pulled home before the enemy returns to the pool.

That chain is easy to break silently, so it is now covered by tests rather than left as an observation.

### Option selected, revised, or rejected

- **Selected:** Verify and document the existing design instead of adding a second pooling layer that would duplicate what `EnemyViewPool` already provides.
- **Selected:** Add regression tests for the two properties the design depends on: every authored reaction effect is a child of the enemy prefab, and a detached effect returns under its authored parent when the enemy is released.
- **Rejected:** A separate scene-level effect pool, which would decouple effect lifetime from enemy lifetime but duplicate pooling and lose the per-enemy authored placement and scale.

### Implementation or verification result

- Confirmed the only runtime `Instantiate`/`Destroy` of enemy visuals is in `EnemyViewPool.CreateView`.
- Added `EnemyReactionEffectPoolingTests`, which fails if any reaction effect is assigned an object outside the enemy prefab, or if a detached effect is not re-parented and switched off on release.
- 195 of 195 EditMode tests pass.
- Known limitation, not changed: `Despawn` starts the death animation and releases the view when it finishes, roughly 0.2 seconds later, and release resets every effect. A reaction whose effect is longer than that, such as the 2 second Firestorm, is cut short when the enemy it is attached to dies. Fixing that needs effect lifetime decoupled from enemy lifetime, which is the scene-level pool that was rejected above.
