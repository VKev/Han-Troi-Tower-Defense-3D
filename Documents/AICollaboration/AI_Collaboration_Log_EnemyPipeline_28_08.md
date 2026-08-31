# AI Collaboration Log — Enemy Pipeline — 28 August 2026

## Session continuity

- **Project:** `TowerDefense3D`
- **Responsible Claude Code session:** `a513a583-5dc6-42dc-a36d-047532c1f1e0`
- **Related records:** `AI_Collaboration_Log_WaveEnemy_25_08.md`, `AI_Collaboration_Log_BlenderModels_25_08.md`
- **Implementation issue:** none created; the work was requested and verified conversationally
- **Documentation issue:** none created

This file records a single long session that replaced every placeholder enemy view with an authored model, authored three
new animations in Blender, removed one enemy type entirely, reclaimed roughly 8 MB of repository weight, and closed with an
architecture review. It summarizes the verified work and the wrong turns taken along the way without reproducing the raw
transcript.

## Entry 1 — Wire the Gecko model into the Stealth enemy

### Problem being addressed

`Stealth 1.prefab` was an empty nested instance of the Gecko FBX with no `Animator` and no `EnemyView`, and
`Stealth.asset` still pointed at a capsule placeholder. The Stealth enemy therefore rendered as a primitive.

### Prompt used

The project owner asked the AI to wire the Gecko model into Stealth the same way Basic, Armored and Magic Resistant were
already wired.

### Important AI response

The AI followed the existing override-controller pattern rather than inventing a new one: it created
`StealthEnemy.overrideController` over the shared `EnemyLocomotion.controller`, mapped `EnemyIdle` and `EnemyMove` to the
Gecko clips, added `Animator` plus `EnemyView` to the prefab root, and repointed the definition's `viewPrefab`.

An untracked draft script, `Assets/Scripts/Editor/EnemyAuthoring/StealthEnemyPrefabWiring.cs`, already existed for this
task but matched clip names by exact string `"Idle"` and `"Walk"`. The Gecko clips are actually named
`UniRigArmature|Idle` and `UniRigArmature|Walk`, so running that script would have thrown. The AI reported the defect and
performed the wiring directly rather than silently repairing or deleting a file that predated the session.

### Option selected, revised, or rejected

- **Selected:** reuse the shared `EnemyLocomotion.controller` with a per-enemy override controller.
- **Selected:** leave the pre-existing draft editor script untouched and report its bug instead.
- **Rejected:** authoring a separate animator controller for Stealth, or hand-editing the prefab YAML.

### Implementation or verification result

`Stealth.asset` resolves to the wired prefab, and `EnemyCatalog` reported no validation errors. The material had already
been resolved to `Gecko_Material` by the FBX importer, so no material override was required.

## Entry 2 — Author Idle and Walk for the chicken rig

### Problem being addressed

The project owner had a rigged rooster open in Blender with no animation. The Speed Support enemy needed locomotion clips
matching the project's `Idle` / `Walk` naming contract.

### Prompt used

The project owner asked the AI not to model anything, only to animate the existing rig, and confirmed a rig was already
present.

### Important AI response

The AI had begun building a placeholder chicken from primitives before the clarification arrived; it deleted that work,
purged the orphaned meshes and materials, and worked only on `UniRigArmature`.

Because the 22 bones carried auto-generated names (`Bone_000`…`Bone_021`), the AI did not guess their roles. It rotated
each candidate bone by a fixed angle and measured the resulting world displacement of the bone tip, which identified the
root, chest, neck, head, tail fan, two wings and two five-segment legs, and established that `+X` is the pitch axis.

`Idle` was authored at 48 frames and `Walk` at 24 frames, both at 24 fps, with the legs explicitly keyed neutral in `Idle`
so the pose cannot inherit a leftover stride.

### Option selected, revised, or rejected

- **Selected:** derive bone roles from measured displacement rather than from names.
- **Selected:** key legs to neutral explicitly in `Idle` instead of relying on unkeyed channels.
- **Rejected:** modelling a replacement chicken; the owner's rig was authoritative.

### Implementation or verification result

Both actions were created with fake users so they survive a file reload. Knee flexion was masked to the forward swing
phase only, so the planted leg stays straight during stance.

## Entry 3 — Author the Crow animation, then intensify it

### Problem being addressed

The Speed Support enemy needed a skill animation: the rooster raises its head and crows, with no beak articulation
required, returning to a pose that blends back into Idle or Walk.

### Prompt used

The project owner requested the crow animation, then after reviewing it asked for a noticeably fiercer version.

### Important AI response

The first pass produced a 60-frame clip whose beak swung from `-27°` to `+54°`. For the fiercer revision the AI swept the
neck angle and found that beak elevation **peaks near 55° of neck rotation and then declines** — `55°` produced a rise of
`0.527` while `66°` produced only `0.509`, because beyond that the head tips backward instead of higher. The final peak was
therefore set at `52°`, just below the curve's maximum.

Body pitch was deliberately excluded. Measurement showed `Bone_006` is the pelvis and parents both the legs and the neck;
rotating it by only `3°` slid both feet sideways by `0.022`, and a three-bone leg-chain solve reduced the error from
`0.0619` to `0.0618` — effectively nothing, because the rotation *translates* the hip rather than rotating it.

### Option selected, revised, or rejected

- **Selected:** cap the neck at `52°`, measured against the elevation curve rather than chosen by eye.
- **Selected:** drive the whole performance from neck, head, tail and wings, all of which are free of the leg chain.
- **Rejected:** body pitch through `Bone_006`; its axis is roughly `45°` off the sagittal plane and cannot be compensated.

### Implementation or verification result

Total beak travel rose from `0.69` to `1.044` (+51%), the anticipation dip deepened from `-27°` to `-54°`, syllable pulse
depth reached `19.7°`, and the attack tightened from 8 frames to 6. `max_foot_drift` remained `0` across all 60 frames.

## Entry 4 — Import the chicken into Unity as the Speed Support enemy

### Problem being addressed

The authored clips had to reach Unity under the naming contract already used by the Gecko rig, and the Speed Support
definition still referenced a capsule.

### Prompt used

The project owner asked the AI to export the model into the enemy models folder, build a prefab, and wire it to Speed
Support.

### Important AI response

Export produced `UniRigArmature|Idle`, `|Walk` and `|Crow`, matching the existing convention. The AI measured rather than
assumed both transform values: prefab scale `0.74` was derived from a table of existing enemy world heights so the chicken
lands at `1.26`, between Basic at `1.185` and Stealth at `1.344`; yaw `180°` was derived from the head-bone direction in
root-local space.

### Option selected, revised, or rejected

- **Selected:** compute scale from the measured heights of the existing roster.
- **Selected:** compute yaw from bone geometry rather than copying another prefab's value.
- **Rejected:** wiring the `Crow` clip; the shared controller exposes only `IsMoving`, and inventing a new animator
  architecture was outside the request.

### Implementation or verification result

`SpeedSupportEnemy.prefab` carries `Animator`, override controller and `EnemyView`; `SpeedSupport.asset` resolves to it.
The `Crow` clip is imported and available but intentionally not wired.

## Entry 5 — Remove the Shortcut Builder enemy completely

### Problem being addressed

The project owner decided to drop the Shortcut Builder enemy type entirely, including code.

### Prompt used

The project owner asked the AI to delete the enemy and everything related to it.

### Important AI response

The AI surveyed the footprint before deleting anything and found six files plus six reference sites. Deletion order
mattered: references were cleared first so nothing was left pointing at a missing asset.

Two wave schedules contained the enemy. The AI removed only the Shortcut Builder batch from each wave and kept the
accompanying Basic batch, so neither wave became empty.

Attempting `AssetDatabase.DeleteAsset` on the C# file was refused by the MCP bridge as requiring user interaction, so the
files were removed with `git rm`, which also staged the deletion for recovery.

### Option selected, revised, or rejected

- **Selected:** clear references first, delete assets second.
- **Selected:** preserve the surviving spawn batch in each affected wave.
- **Selected:** `git rm` so the removal is recoverable.
- **Rejected:** deleting the definition first and leaving null references in the catalog and wave schedules.

### Implementation or verification result

The catalog went from eight definitions to seven with zero validation errors. Reflection confirmed no type matching
`ShortcutBuilder` remained loaded in any assembly, `WaveHudPresenter` still resolved (proving its assembly compiled), a
GUID sweep returned no remaining references, and both wave schedules kept eight waves with `nullEnemyRefs = 0`. Six
auto-generated `AGENTS.md` files still mention the enemy; these are Better Context artifacts with their own staleness
tracking and were deliberately not hand-edited.

## Entry 6 — Correct two stale assertions in the enemy data fixture

### Problem being addressed

Two assertions in `ApprovedEnemyAnimationAssets_UseSharedLocomotionContract` failed. Both predated the session.

### Prompt used

The AI reported the two failures without fixing them, because it could not tell whether the tests or an earlier model swap
were wrong. The project owner then confirmed the clip rename was correct and the humanoid assertion was wrong.

### Important AI response

`git` history showed the assertions came from commit `48b94a6` while the model swap to `MoonlitMouse.fbx` came from the
later `9cd7afe`; the tests had simply not been updated. `MoonlitMouse.fbx` names its clip `Walk`, not `Walking`, and imports
as a `Generic` rig, so `isHuman` is correctly `false`.

For the second assertion the AI changed the expectation to `Is.False` rather than deleting it, so the fixture still fails
if an enemy model is ever imported as Humanoid by accident.

### Option selected, revised, or rejected

- **Selected:** ask the project owner which side was wrong instead of guessing and masking a real defect.
- **Selected:** invert the humanoid assertion rather than removing it.
- **Rejected:** silently editing tests to match observed values.

### Implementation or verification result

All `73` replicated assertions across the three fixtures passed.

## Entry 7 — Merge and slim the Stone Sentinel, wire it as Mini-boss

### Problem being addressed

The source asset shipped as two FBX files, each carrying one animation and a full skin, plus four PNG textures.

### Prompt used

The project owner asked the AI to merge the two animations into one FBX, remove textures from the FBX, optimise the mesh
and remove duplicate vertices, then import to Unity as the Mini-boss.

### Important AI response

Blender was closed, so the AI ran the merge headless through `blender --background --python`. Both rigs were verified
identical — 24 bones, matching names, root `Hips` — before transferring the action.

Two findings changed the plan and were reported rather than glossed over. First, **the mesh had nothing to optimise**:
283 vertices and 510 polygons with `0` duplicate vertices, `0` loose vertices and `0` loose edges. The entire size problem
was a packed 2048² base-colour texture. Second, the project's `ToonShader` exposes only `_BaseMap` and `_BaseColor`, so
the normal, metallic and roughness maps — 5.4 MB — were deliberately not imported because the shader cannot read them.

Yaw was measured from the rig's own `headfront` marker bone, which returned `(0.08, 0, 0.997)`; the model already faced
`+Z`, so yaw stayed `0` — unlike the chicken and gecko, which needed `180°`.

### Option selected, revised, or rejected

- **Selected:** headless Blender for a deterministic batch job.
- **Selected:** import only the base colour, and state plainly that the mesh needed no optimisation.
- **Rejected:** decimating a 510-triangle mesh; further reduction would break the silhouette.

### Implementation or verification result

`10.32 MB` across two files became `0.85 MB` in one, a `91.7%` reduction, verified by re-importing the exported file and
confirming two clips, 24 bones and zero images. In Unity the prefab reached world height `2.72`, the tallest of the roster.
`39` checks passed with zero failures.

## Entry 8 — Strip embedded textures from the Chicken and Gecko models

### Problem being addressed

`Chicken.fbx` was `4.91 MB` and `Gecko.fbx` was `4.60 MB`, both far larger than their geometry justified.

### Prompt used

The project owner instructed the AI to clean both up.

### Important AI response

This entry records a diagnostic mistake. The AI first stated the size came from embedded textures, then reversed that
after Unity's `LoadAllAssetsAtPath` reported `embedded textures = 0`, then reversed again once a Blender audit proved the
original claim correct. **Unity does not surface embedded FBX textures as sub-assets, so its count is not evidence of
absence.** The Blender audit found `base_color` at 2.61 MB plus `normal` at 1.70 MB inside the chicken and 3.60 MB inside
the gecko. The Stone Sentinel, with more keyframes than either but no images, was the control that settled it.

The gecko round-trip carried real risk: re-exporting could change sub-asset fileIDs and break `StealthEnemy.overrideController`
and `Stealth 1.prefab`. The AI backed both files up first and normalised imported action names so a re-export would not
accumulate another `UniRigArmature|` prefix.

### Option selected, revised, or rejected

- **Selected:** trust the Blender audit over Unity's sub-asset count.
- **Selected:** back up both FBX files before the round-trip.
- **Rejected:** hand-editing the FBX binaries; string scanning failed because array payloads are zlib-compressed.

### Implementation or verification result

Chicken fell from `4.91 MB` to `0.60 MB` (−87.8%) and Gecko from `4.60 MB` to `1.00 MB` (−78.3%); total enemy FBX weight
went from `11.89 MB` to `3.97 MB`. Nothing broke — Unity preserved every fileID by name, so both override controllers and
both prefabs still resolved, with Stealth keeping scale `1.31` and yaw `184.7`. The chicken had been rendering with an
untextured URP Lit material and now uses `ToonShader` with an extracted base colour.

## Entry 9 — Delete the orphaned capsule placeholder prefabs

### Problem being addressed

The enemies folder still held primitive capsule prefabs from before the models existed.

### Prompt used

The project owner asked the AI to delete all the empty capsules.

### Important AI response

The AI classified every prefab by whether it used a built-in primitive mesh and counted references to each GUID. Because
these prefabs live under `Assets/Resources/`, a GUID sweep alone is insufficient — they could be loaded by string path. The
AI therefore also confirmed that `Resources.Load` appears nowhere in `Assets/Scripts`.

`SummonerBoss.prefab` was a capsule but still referenced by `SummonerBoss.asset`, so it was kept and flagged.

### Option selected, revised, or rejected

- **Selected:** verify both GUID references and string-path loading before deleting from a `Resources` folder.
- **Selected:** keep the one placeholder still in use and report it.
- **Rejected:** deleting `RoadStraightCell`, `RockTexture`, `BasicTower` and `Lake`; these use primitive meshes by design
  and carry 16, 8, 1 and 1 references respectively.

### Implementation or verification result

Seven prefabs — fourteen files including `.meta` — were removed with `git rm`. `39` checks passed; the enemies folder now
holds exactly seven prefabs matching the seven catalog entries.

## Entry 10 — Author Idle and Screech for the Royal Shadow Rat

### Problem being addressed

The project owner imported a rigged quadruped rat that appeared far too small, carrying only a walking animation. The
Summoner Boss needed an idle plus a skill in which the rat rears onto its hind legs, calls out, and returns to a pose that
blends into Idle or Walk.

### Prompt used

The project owner described the desired skill, noted the model looked too small, and confirmed a rig already existed.

### Important AI response

The reported size problem was not the model: the armature object carried `scale = 0.01` from the FBX unit mismatch. Setting
it to `1.0` gave correct proportions. The imported action was renamed from `Armature|Armature|Unreal Take|baselayer` to
`Walk`.

Unlike the chicken, this rig's `Hips` proved to be a **clean pitch axis** — its measured displacement had an `X` component
of exactly `0.000` — so hind-leg compensation was viable here. A coordinate-descent solve ran at every keyframe.

### Option selected, revised, or rejected

- **Selected:** solve the hind-leg chain per keyframe rather than interpolating one compensation ratio.
- **Selected:** start and end `Screech` at the exact neutral pose so it blends both ways.
- **Rejected:** reusing the chicken's approach; the two rigs differ in whether the pitch axis is compensable.

### Implementation or verification result

`Idle` runs 60 frames at 30 fps with a loop error of `0`. In `Screech` the spine swings `53°` from `-11.6°` to `+41.2°`,
the head rises `1.32` — the rat's entire body length — the front paws leave the ground, and the hind feet stay planted
within `0.004`.

## Entry 11 — Keep the reared pose out of the floor and stop the belly collapsing

### Problem being addressed

The project owner reported the tail sinking through the ground while the rat was reared, and separately that the belly
looked squashed in the same pose.

### Prompt used

The project owner reported each defect as they saw it and gave permission to realign bones or edit weights if needed.

### Important AI response

The tail was a sign error: rotating `Hips` already carried the tail base downward, and the AI had added a further `-22°`.
A solver now computes the minimum positive lift that clears every tail joint, with a `0.062` margin measured from the
tail mesh's own thickness.

Two hidden causes surfaced while chasing the remaining floor contact. First, **the foot bone tips sit `0.144` below the
mesh sole**, so planting a bone tip does not plant the foot. Second, the `Hips` pose bone carried `scale = 1.3547` left
over from import; zeroing rotation and location but not scale meant the AI's "rest" pose was not the rest pose, which
broke its rest-space mapping by `0.355` and caused an earlier attempt to fail.

For the belly, the AI measured mesh volume rather than judging by eye. Volume fell `36.6%` in the reared pose, and
isolating each bone group showed the **shoulder alone accounted for `31.5%`**. Tapering the shoulder weights moved the
result only from `-36.6%` to `-36.4%`, proving the smeared weights were not the cause — this is inherent linear blend
skinning collapse, and Blender's "Preserve Volume" cannot help because Unity does not use dual-quaternion skinning.

The fix came from the pose. A sweep of 36 shoulder/elbow/wrist combinations showed paw height is nearly independent of
shoulder angle while collapse is proportional to it.

### Option selected, revised, or rejected

- **Selected:** a weighted objective prioritising no-slide and no-penetration over foot flatness.
- **Selected:** shoulder `-12°`, elbow `14°`, wrist `50°`, chosen from measured data.
- **Rejected:** weight editing, which was tried and measured as ineffective.
- **Rejected:** enabling Preserve Volume, which would look correct in Blender and wrong in Unity.

### Implementation or verification result

The tail moved from `0.32`–`0.46` below ground to `0.039` above it. Worst mesh penetration across all 91 frames fell from
`0.458` to `0.036`, on a single interpolated landing frame. Belly volume loss fell from `-36.4%` to `-1.3%` while paw lift
*improved* from `0.621` to `0.795` — better on both axes.

## Entry 12 — Import the rat into Unity as the Summoner Boss

### Problem being addressed

The rat had to reach Unity as the boss enemy with looping locomotion, a toon material and mobile-appropriate texture
settings.

### Prompt used

The project owner asked for the import, looping Idle and Walk, wiring to the boss rather than the mini-boss, a toon
material with gloss disabled to match the others, ETC2 4-bit compression at max size 512, and the skill clip left for later.

### Important AI response

A survey found every existing enemy material already at `gloss(0/0)`, so no material needed correcting — the request was
already satisfied project-wide. The survey did reveal that `Chicken_BaseColor.png`, created earlier in this same session,
had **no Android override at all** while every other texture had one; the AI reported and fixed its own omission.

### Option selected, revised, or rejected

- **Selected:** apply the requested `512` rather than the `128`–`256` used elsewhere, and flag the difference.
- **Selected:** wire only `EnemyIdle` and `EnemyMove`, leaving `Screech` unwired as with the chicken's `Crow`.
- **Rejected:** silently normalising the other textures to 512.

### Implementation or verification result

`64` checks passed with zero failures. This also retired the last capsule placeholder in the project.

## Entry 13 — Three defects found once the boss reached the level

### Problem being addressed

The project owner reported, in sequence: the boss did not appear in the wave; it spawned at the right size then shrank
after one frame; and its walk animation did not play.

### Prompt used

The project owner reported each symptom from play testing and supplied a backup copy of the source walk animation in case
it was needed.

### Important AI response

**Invisible.** Wiring, console output, mesh normals, face winding, shader validity and material assignment were all ruled
out, and an isolated render proved the model drew correctly at 30,303 pixels — more than the mini-boss. The cause was
position: the rat's feet sat `0.279` below the prefab origin while every other enemy sat within `0.075` of it, so the
model was buried. The origin had to be corrected twice, because an intermediate fix left the model in the wrong unit
space.

**Shrinking.** The `Walk` action carried nine **object-level** F-curves — `location`, `rotation_euler` and `scale`,
including the original `0.01` — while `Idle` and `Screech` carried none. The exported clips therefore disagreed about the
child transform, `100.0` against `1.0`, and the Animator overwrote the prefab scale on the first frame. Removing those
nine curves made all three clips agree.

**Static walk.** This was the AI's own regression. In its first command on the rig it had set `rotation_mode = 'XYZ'` on
all 27 bones to author in Euler, but the imported `Walk` stored `rotation_quaternion` — 108 curves that Blender then
ignored entirely. Only the mode-independent `Hips.location` survived export, which is why the rat rigidly bobbed by
`0.012`. The data was never lost; the AI temporarily restored quaternion mode, sampled the true pose every frame, and
rewrote it as Euler using `euler_compat` to avoid gimbal flips. The owner's backup was therefore not needed.

### Option selected, revised, or rejected

- **Selected:** correct the model origin at source in Blender rather than offsetting the Unity prefab.
- **Selected:** normalise every action's ground contact through a `Hips.location` offset in the animation, not by moving
  the object origin — moving the origin is what produced this chain of defects.
- **Selected:** convert the quaternion curves rather than reverting the whole rig to quaternion mode, which would have
  broken the two Euler-authored clips.
- **Rejected:** re-importing from the owner's backup; the existing data was intact once read correctly.

### Implementation or verification result

Feet now sit at `0.012` in idle and `0.001` in walk, sampled at nine points per clip rather than at frame zero only. All
three clips drive an identical child scale. Walk leg swing rose from `0.000` to `0.251` with a loop seam of `0.0069`.
Prefab scale was set to `2` at the owner's request. The AI noted explicitly that verifying only `loop=True` and foot
height — never whether a clip actually moved — is what allowed this defect to reach play testing.

## Entry 14 — Architecture review and the runtime wiring map

### Problem being addressed

With the asset work finished, the project owner asked a series of questions about the runtime architecture and then
requested a complete diagram of it.

### Prompt used

The project owner asked whether enemies were pooled, what `TowerNetworkSystem` does, whether it ticks, how it differs from
`TowerNetworkManager`, for a full diagram of the code, and finally how many scripts the Bootstrap scene carries.

### Important AI response

The pooling premise was incorrect and the AI said so with evidence: enemies, projectiles and hit effects all share one
`ComponentPool<T>` wrapper over `UnityEngine.Pool.ObjectPool<T>` with identical capacity settings. The only unpooled
`Instantiate` is `TowerInstanceFactory`, which is defensible because towers persist once placed.

On the facade question the AI established that `TowerNetworkSystem` has no tick at all and is absent from
`LevelSystemGroup.Tick`; `GameplaySimulationSystem` takes `TowerNetworkManager` directly, so the hot path bypasses the
facade. The split is explained by lifetime: the manager is a `Singleton` in the application scope while the system is
`Scoped` per level, which is why the system owns `BeginLevelSession` and `EndLevelSession`. All ten manager partials
contain no `using UnityEngine`, and positions cross the boundary as a plain `TowerWorldPosition` struct rather than a
`Vector3`.

For the Bootstrap scene the AI's first answer would have been wrong: a YAML grep hard-coded `fileID: 11500000` and so
counted only project scripts. Enumerating live components in Unity gave the real figures.

### Option selected, revised, or rejected

- **Selected:** contradict the pooling premise with file and line references rather than adding redundant pooling.
- **Selected:** derive the dependency graph from constructor fields across every system, excluding test fixtures.
- **Selected:** publish the diagram as a shareable artifact with a consistent colour encoding for the Unity boundary.
- **Rejected:** the initial YAML-derived script count, which undercounted package components.

### Implementation or verification result

The wiring map was published at `https://claude.ai/code/artifact/f06e88d9-66e9-4115-96fe-5b6960c07eb4` with eight sections
and six hand-authored SVG diagrams covering boot flow, scope lifetimes, the Unity boundary, frame order, the simulation
spine, the presentation layer and the facade split.

The Bootstrap scene holds `37` GameObjects, `56` MonoBehaviour instances and `18` distinct types, with no missing scripts.
Ten instances across nine types are project scripts; the entire composition root is a single `ApplicationLifetimeScope`
on one childless GameObject. The two authored level buttons are correct rather than incomplete: `LevelCatalog.asset`
declares only two levels, and `LevelMenuView` uses an authored array rather than spawning buttons, so `Level_003` through
`Level_008` exist as scenes but are unreachable from the menu until both are extended.

## Outstanding at session end

- Neither skill clip is playable. `Chicken|Crow` and `RoyalShadowRat|Screech` are imported and correct but unwired,
  because `EnemyLocomotion.controller` exposes only the `IsMoving` boolean and `EnemyView` sets only that. All seven
  enemies share this controller, so adding a skill state is a single change for the whole roster.
- The Summoner Boss was moved to wave 1 in `Level_001_Waves.asset` by the project owner for testing and should be
  returned to wave 8.
- `magic-resistant` penetrates the floor by `0.203` during its walk clip. This predates the session and was reported, not
  changed.
- Six auto-generated `AGENTS.md` files still reference the removed Shortcut Builder enemy and will correct themselves on
  the next Better Context scan.
