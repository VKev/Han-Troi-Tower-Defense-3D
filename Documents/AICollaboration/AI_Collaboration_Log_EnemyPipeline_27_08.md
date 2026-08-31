# AI Collaboration Log — Enemy Pipeline — 27 August 2026

## Session continuity

- **Project:** `TowerDefense3D`
- **Responsible Claude Code session:** `a513a583-5dc6-42dc-a36d-047352c1f1e0`
- **Related records:** `AI_Collaboration_Log_BlenderModels_20_08.md`, `AI_Collaboration_Log_BlenderModels_25_08.md`, `AI_Collaboration_Log_WaveEnemy_25_08.md`
- **Tracking issues:** none were claimed in Beads for this session
- **Tooling used:** Blender MCP (interactive) and Blender `--background` CLI at `D:\Apps\Blender\blender.exe`; Unity MCP against Unity `6000.3.21f1`, never in Play Mode
- **Published reference:** [TowerDefense3D Wiring Map](https://claude.ai/code/artifact/f06e88d9-66e9-4115-96fe-5b6960c07eb4)

This file records the consequential enemy-asset and architecture decisions from the current conversation. It summarizes verified
work and the corrections made along the way rather than reproducing the raw transcript. Several entries record AI errors that were
found and fixed inside the same session; they are kept because the diagnosis path is the reusable part.

---

## Entry 1 — Wire the Gecko rig into the Stealth enemy and reject a draft script with a clip-matching defect

### Problem being addressed

`Assets/Resources/Prefabs/Enemies/Stealth 1.prefab` was an unconfigured nested instance of the Gecko FBX: no `Animator`, no
`EnemyView`, and `Stealth.asset` still pointed at the placeholder capsule `Stealth.prefab`.

### Prompt used

The owner asked to wire the Gecko into Stealth the same way Basic, Armored and Magic Resistant were already wired.

### Important AI response

The AI read the three working enemies first and extracted the shared contract: one `AnimatorOverrideController` per enemy, based
on `EnemyLocomotion.controller`, overriding `EnemyIdle` and `EnemyMove`, with `EnemyView` and `Animator` on the prefab root. It
found an untracked draft, `Assets/Scripts/Editor/EnemyAuthoring/StealthEnemyPrefabWiring.cs`, that would have thrown: it matched
clips by the exact names `"Idle"` and `"Walk"`, while the Gecko FBX exposes `UniRigArmature|Idle` and `UniRigArmature|Walk`. The
AI performed the wiring directly through Unity MCP using suffix matching instead of running the draft.

### Option selected, revised, or rejected

- **Selected:** create `StealthEnemy.overrideController` and map `EnemyIdle → UniRigArmature|Idle`, `EnemyMove → UniRigArmature|Walk`.
- **Selected:** add `Animator` and `EnemyView` to the prefab root and repoint `Stealth.asset.viewPrefab`.
- **Rejected:** running the pre-existing draft script, and deleting or silently rewriting it — it predated the session, so the defect
  was reported to the owner instead.

### Rationale

The Gecko material was already resolved correctly by the FBX importer, so no material override was needed. Matching clips by
suffix rather than exact name makes the wiring robust against Blender's `Armature|Action` take naming.

### Implementation or verification result

`EnemyCatalog` reported the Stealth definition resolving to a prefab carrying `EnemyView`, an `Animator` and a controller. No
console errors. `Stealth.asset` guid `b111ebd2a63e5374791b768be64fe3ee` was already registered in the catalog, so no catalog edit
was required.

---

## Entry 2 — Author Idle and Walk on the owner's existing rooster rig after discarding a scratch-built model

### Problem being addressed

The owner's open Blender file contained a rigged low-poly rooster (`UniRigArmature`, 22 bones, root `Bone_000`) with no animation.

### Prompt used

The owner first asked for a walking and idle animation "for a chicken". The AI began modelling one from primitives; the owner
interrupted to say a rigged chicken already existed in the file and that nothing should be re-created.

### Important AI response

The AI deleted the twelve primitive objects, the `Chicken` collection and the four scratch materials it had just created, then
worked only on the supplied rig. Because the rig used auto-generated bone names (`Bone_000`…`Bone_021`), the AI derived the bone
roles empirically: it rotated each candidate bone by a known angle and measured the resulting world displacement of its tail,
producing a mapping for root, chest, neck, head, tail fan, wings and both five-segment legs.

### Option selected, revised, or rejected

- **Selected:** discard the scratch model entirely and animate the supplied rig.
- **Selected:** identify bone roles by measured displacement rather than by guessing from names.
- **Selected:** Idle at 48 frames and Walk at 24 frames, both loop-safe, at the file's authored 24 fps.
- **Selected:** key the leg bones explicitly to neutral inside Idle so Idle cannot inherit a leftover mid-stride pose from Walk.
- **Rejected:** re-modelling, and inferring bone function from ordinal names.

### Rationale

An auto-rigged skeleton carries no semantic naming, so any assumption about which bone is a knee is unverified. Measuring the
actual tail displacement per axis is cheap and produces a mapping that can be re-derived later.

### Implementation or verification result

Both actions were created with fake users. Walk alternates the legs 180° out of phase with the knee bending only during its swing
phase, so the stance leg stays straight. Verified visually in an orthographic side view at several frames.

---

## Entry 3 — Author a crowing animation with the feet pinned, and establish why body rotation was unusable on this rig

### Problem being addressed

The owner asked for a crow animation: the chicken stands, raises its head and calls, without opening the beak.

### Prompt used

"tôi cần 1 anim gà gáy, khi con gà đứng, ngẩng mặt lên trên, gáy (không cần hả họng, chỉ cẩn nó ngẫn đầu lên là được)". A later
prompt asked for a fiercer version.

### Important AI response

The AI attempted to add a body pitch for weight, then measured that `Bone_006` — the pelvis — parents both the legs and the neck.
Rotating it 3° slid both feet sideways by `0.022`. A three-bone leg-chain solve reduced the drift only from `0.0619` to `0.0618` at
8°, because the rotation *translates* the hip joint and leg rotations cannot produce that translation. The AI abandoned body
rotation and drove the crow entirely from neck, head, tail and wings, which do not parent the legs.

For the fiercer revision, the AI swept peak neck angles and found the beak's height gain peaks near 55° and then declines — beyond
that the head tips backward without rising — so the peak was set just below the maximum rather than at the largest angle.

### Option selected, revised, or rejected

- **Selected:** neck 52° / head 30° at peak, anticipation dip to −54°, four syllable pulses averaging 19.7° of beak swing.
- **Selected:** no body pitch at all; `max_foot_drift` measured `0` across all 60 frames.
- **Revised:** the first pass used neck 46°/head 10°, which raised the beak well but set the head back `0.417`; a measured sweep of
  30 combinations replaced it with neck 30°/head 30° at nearly identical beak lift and `0.315` setback, and the fierce revision then
  raised both.
- **Rejected:** pitching the body for weight, and pushing the neck past the point where beak height stops increasing.

### Rationale

A rig whose pelvis parents both the legs and the spine cannot express a body lean without an IK setup this project does not have.
Measuring where the beak-height curve turns over converts "how fierce" from a taste argument into a bounded number.

### Implementation or verification result

Final clip: 60 frames at 24 fps, beak travel `1.044` units from `−0.52` to `+0.524`, `max_foot_drift = 0`. The Blender file was saved
to `C:\Users\VNG\Downloads\Chicken.blend` with fake users on all three actions, after the owner reported losing earlier unsaved work.

---

## Entry 4 — Import the chicken as the Speed Support enemy and delete the Shortcut Builder enemy type completely

### Problem being addressed

Speed Support still used a placeholder capsule, and the owner wanted the Shortcut Builder enemy removed from the project entirely.

### Prompt used

The owner asked to import the chicken to Unity as a Speed Support enemy, wire it, and to delete Shortcut Builder completely —
"code liên quan, mọi thứ".

### Important AI response

Before deleting anything the AI mapped the full footprint by GUID rather than by name, finding two references that a name search
would have reported ambiguously: `Level_001_Waves.asset` and `Level_002_Waves.asset` each spawned two Shortcut Builders. It removed
those batches while keeping the Basic batch in the same wave so no wave became empty, then removed the catalog entry, the
`WaveHudPresenter` branch, and the test assertions, and only then deleted the three assets with `git rm`.

### Option selected, revised, or rejected

- **Selected:** remove references first, delete assets last.
- **Selected:** delete via `git rm` so the removal is staged and recoverable.
- **Selected:** keep the surviving Basic spawn batch in both affected waves.
- **Selected:** update `WaveHudViewTests` to use the `[SPEED AURA]` marker, since its Shortcut Builder string was only a literal.
- **Rejected:** hand-editing the generated `AGENTS.md` folder maps that still mention the type — they are Better Context output with
  a `staleness.json` tracker and regenerate on the next scan.

### Rationale

Deleting a ScriptableObject that wave schedules still reference would leave null spawn entries that fail at runtime rather than at
import. GUID search is the only reliable way to find serialized references.

### Implementation or verification result

Six files removed. Reflection over all loaded assemblies confirmed zero types matching `ShortcutBuilder`; `WaveHudPresenter`
resolved, proving its assembly still compiled. Both wave schedules kept 8 waves and 12 batches with `nullEnemyRefs = 0`. The
catalog dropped to 7 definitions with 0 validation errors.

---

## Entry 5 — Correct two stale test assertions after confirming they predated the session

### Problem being addressed

`ApprovedEnemyAnimationAssets_UseSharedLocomotionContract` failed on two assertions that the AI had not touched.

### Prompt used

The AI reported both failures with evidence and declined to fix them unattended, because either the test or the model swap could be
the error. The owner replied that the clip rename was correct and the `isHuman` expectation was wrong.

### Important AI response

The AI proved the failures were pre-existing before proposing anything: `git status` showed no modification to any BasicEnemy file,
and `git log` showed the assertions came from commit `48b94a6` while the model swap to `MoonlitMouse.fbx` came later in `9cd7afe`.
It then changed `"Walking"` to `"Walk"` and `isHuman` from `Is.True` to `Is.False`.

### Option selected, revised, or rejected

- **Selected:** ask the owner which side was wrong rather than guessing.
- **Selected:** change `isHuman` to `Is.False` rather than deleting the assertion, since every enemy rig in the project is Generic
  and the inverted check still catches an accidental Humanoid import.
- **Rejected:** fixing the tests unattended, which could have masked a genuine model-import mistake.

### Rationale

A failing assertion has two possible authors. Choosing one without the owner risks silencing a real defect.

### Implementation or verification result

73 of 73 replicated assertions passed. The Unity Test Runner could not be driven through MCP — it requires user interaction — so the
assertions were re-executed as an equivalent script and that limitation was stated rather than implied.

---

## Entry 6 — Merge the Stone Sentinel takes, strip its embedded texture, and wire it as the Mini-boss

### Problem being addressed

The Stone Sentinel arrived from Meshy as two 5 MB FBX files, each carrying one animation and a duplicate skinned mesh.

### Prompt used

The owner asked to merge the two animations into one FBX, remove textures from the FBX to reduce size, optimize the mesh and remove
duplicate vertices, then import as a Mini-boss enemy and wire it.

### Important AI response

The AI verified the two rigs were identical (24 bones, matching names, root `Hips`) before transferring the action, then reported
that the requested mesh optimization had nothing to do: the mesh was 283 vertices and 510 polygons with **0 duplicate vertices, 0
loose vertices and 0 loose edges**. The entire size reduction came from the embedded 2048² base colour, not from geometry. It also
found the project's ToonShader exposes only `_BaseMap` and `_BaseColor`, so it imported one of the four supplied textures and left
the normal, metallic and roughness maps out.

### Option selected, revised, or rejected

- **Selected:** export one FBX with `path_mode='STRIP'` and `embed_textures=False`; verify by re-importing the exported file.
- **Selected:** import only the base colour, because the shader physically cannot read the other three.
- **Selected:** prefab scale `1.6` and yaw `0`, both derived from measurement — the rig's own `headfront` marker bone showed the model
  already faced `+Z`, unlike the Gecko and chicken which needed 180°.
- **Rejected:** decimating a 510-triangle mesh, and reporting a mesh optimization that did not occur.

### Rationale

Reporting "optimized the mesh" when the measured change is zero would be false. Naming the real cause makes the same fix reusable.

### Implementation or verification result

10.32 MB across two files became 0.85 MB in one, a 91.7% reduction, with both clips and all 24 bones intact on re-import. World
height `2.72`, the tallest enemy. 39 of 39 checks passed.

---

## Entry 7 — Correct a wrong self-correction about embedded textures, then strip the Chicken and Gecko FBXs

### Problem being addressed

`Chicken.fbx` was 4.91 MB and `Gecko.fbx` 4.60 MB for very low-poly meshes.

### Prompt used

The owner said "sửa luôn, dọn luôn".

### Important AI response

This entry records an AI error. The AI first stated the size was embedded textures, then reversed that position because Unity's
`AssetDatabase.LoadAllAssetsAtPath` reported `embedded textures = 0` for both files. A Blender audit proved the first statement had
been right and the reversal wrong: Chicken carried a packed 2048² base colour of 2.61 MB plus a packed normal of 1.70 MB, and Gecko
carried 2.11 MB plus 1.49 MB. Unity does not surface embedded FBX textures as sub-assets, so its count is not evidence of absence.
The Stone Sentinel, with `0` images and 83,913 keyframes — more than either of the other two — was the control that proved animation
data was not the cause.

### Option selected, revised, or rejected

- **Selected:** treat the Blender audit as authoritative and Unity's sub-asset count as unreliable for this question.
- **Selected:** re-export the chicken from its authored `.blend`, and round-trip the Gecko through Blender since no source file exists.
- **Selected:** back up both FBXs before the round trip, and normalize the imported action names so a second export could not
  accumulate another `Armature|` prefix.
- **Selected:** give the chicken a proper ToonShader material — it had been running on an untextured URP Lit material.
- **Rejected:** trusting a single tool's report over a direct measurement.

### Rationale

The reversal was caused by accepting a convenient signal without checking what it actually measures. The lesson recorded here is
that Unity's embedded-texture reporting answers a different question than the one being asked.

### Implementation or verification result

Chicken 4.91 → 0.60 MB (−87.8%), Gecko 4.60 → 1.00 MB (−78.3%); total enemy FBX weight 11.89 → 3.97 MB. The feared breakage did not
occur: Unity preserved every sub-asset fileID by name, so `StealthEnemy.overrideController`, `Stealth 1.prefab` and the chicken's
Crow clip all survived, with Stealth's authored scale `1.31` and yaw `184.7` unchanged.

---

## Entry 8 — Delete orphaned placeholder prefabs after a two-layer reference check

### Problem being addressed

The enemy prefab folder still held capsule placeholders from before the real models existed.

### Prompt used

"ok xóa tất cả các capsule trống đi".

### Important AI response

The AI checked references in two layers rather than one. A GUID scan found seven placeholders with zero references, but because
they live under `Assets/Resources/` they could also be loaded by string path, which a GUID scan cannot see. A second search
established the project contains **no `Resources.Load` call at all**, making the deletion safe. It also found one placeholder that
had to be kept.

### Option selected, revised, or rejected

- **Selected:** delete `Armored`, `Basic`, `Enemy`, `MagicResistant`, `MiniBoss`, `SpeedSupport` and `Stealth` placeholders via `git rm`.
- **Selected:** keep `SummonerBoss.prefab` — a capsule, but still the live `viewPrefab` of `SummonerBoss.asset`.
- **Rejected:** deleting `RoadStraightCell`, `RockTexture`, `BasicTower` and `Lake`; they use primitive meshes by design and carry
  16, 8, 1 and 1 references respectively.

### Rationale

"Empty capsule" describes an unfinished placeholder, not any prefab built from a primitive. Reference count distinguishes the two.

### Implementation or verification result

Fourteen files removed, staged in git. Catalog still 7 definitions with 0 validation errors; the seven paths confirmed absent from
the asset database and `SummonerBoss.prefab` confirmed present.

---

## Entry 9 — Author the Royal Shadow Rat rear-up skill and solve four separate ground-contact problems

### Problem being addressed

The owner imported a quadruped rat rig and reported the model was too small. It shipped with only a walking animation; an idle and a
rear-up-and-screech skill were required, ending in a pose that blends back into idle or walk.

### Prompt used

The owner asked for an idle and a skill animation in which the rat stands on two legs and calls like the chicken, then lowers back
down, noting the rig already existed. Later prompts reported the tail sinking into the ground and the belly collapsing, and granted
permission to realign bones or weights if needed.

### Important AI response

The apparent size problem was an FBX unit mismatch — `armature scale = 0.01` — not a small model. Unlike the chicken, the rat's
`Hips` proved to be a clean pitch axis (measured X-component `0.000`), so hind-leg compensation worked and the rear-up was possible.

Four ground problems were solved in sequence, each diagnosed by measurement:

1. **Hind feet sliding.** A per-keyframe coordinate-descent solve on the hind chain reduced drift from `0.550` to `0.012` at 40° pitch.
2. **Tail through the floor.** Caused by an AI sign error: the `Hips` rotation already dropped the tail base, and the AI had added a
   further `−22°`. A solver computing the minimum lift that clears the floor replaced it, moving the tail tip from `0.32` below ground
   to `0.039` above.
3. **Belly collapsing 36%.** Isolated by adding one bone group at a time and measuring mesh volume: body plus hind legs cost 5.2%,
   the head 1.3%, and the **front legs 30 points**. Narrowed further, the shoulder alone caused −31.5%. A weight taper was tried and
   rejected — it moved the number only from −36.6% to −36.4%, proving the smeared weights were not the cause and that this is
   inherent linear-blend-skinning collapse. Blender's Preserve Volume was rejected because Unity does not use dual-quaternion skinning.
   A 36-combination sweep found beak-tuck height barely depends on shoulder angle while collapse scales with it directly.
4. **Bone tips below the sole.** The foot bone tips sit `0.144` *below* the mesh sole, so pinning a bone tip does not pin the foot. The
   fix required tracking a contact point taken from the mesh, which first failed with a `0.39` error until the AI found `Hips` carried
   a leftover pose scale of `1.3547`, making its "rest" reference not actually rest.

### Option selected, revised, or rejected

- **Selected:** shoulder `−12°`, elbow `14°`, wrist `50°` — volume loss `−1.3%` with the paws *higher* (`0.795`) than the original
  `0.621` at `−36.4%`; better on both axes.
- **Selected:** solve foot planting against mesh-derived sole points, not bone tips.
- **Revised:** an earlier fix that lifted the whole rig to clear the floor was withdrawn — it removed penetration but floated the
  hind feet by `0.10`, treating the symptom.
- **Rejected:** weight editing, which the owner had permitted but which measurement showed would not help.

### Rationale

Every one of these was found by measuring, not by looking. The volume-isolation pass in particular contradicted the intuitive
suspect — the deeply bent hind legs — and pointed at the shoulder instead.

### Implementation or verification result

Screech: 90 frames at 30 fps; spine sweeps 53°, head rises `1.32` — a full body length — front paws leave the ground, hind feet stay
planted. Worst volume loss across the clip `−3.7%`, and during the reared hold the volume is `+1.3%` to `+1.8%`. Idle loop error `0`.
Saved to `C:\Users\VNG\Downloads\RoyalShadowRat.blend`.

---

## Entry 10 — Wire the rat as the Boss and fix three successive runtime defects

### Problem being addressed

After import the boss did not appear in-game, then appeared at the wrong size, then did not animate.

### Prompt used

Three separate reports: "tôi ko thấy chuột boss trong wave 8", then "nó có spawn nhưng nó bị tàng hình", then "chuột ko giữ được
size... sau 1 tick hay 1 frame nó resize thành nhỏ xíu", then "chuột chưa thực hiện walk? hay chưa loop?".

### Important AI response

Each defect was isolated by elimination and measurement rather than by inspection.

- **Invisible.** Wiring, console, mesh normals, winding, shader and material were all cleared first; an offscreen render then proved
  the prefab drew 30,303 pixels — more than the working Mini-boss. Comparing foot height against every other enemy showed the rat
  alone sat `−0.279` below the prefab origin. It was buried, not invisible.
- **Shrinking.** The imported `Walk` action carried **nine object-level fcurves** — `location`, `rotation_euler` and `scale` on the
  armature object itself — that `Idle` and `Screech` did not. The clips therefore baked different child scales, 1.0 against 100.0,
  and the Animator overwrote the prefab transform on the first frame. Removing those nine curves made all three clips agree.
- **Not animating.** Caused by the AI's own first command in this rig's work: it had set `rotation_mode = 'XYZ'` on all 27 bones to
  author euler keys, while the supplied `Walk` stored `rotation_quaternion`. Blender silently ignores quaternion curves on euler-mode
  bones, so 108 rotation curves were dropped at export and only `Hips.location` survived — the rigid `0.012` bob the owner saw. No
  data was lost; the AI re-read the source in quaternion mode, sampled every frame, and rewrote it as euler using `euler_compat` to
  avoid gimbal flips.

### Option selected, revised, or rejected

- **Selected:** fix the sunk pivot at source in Blender rather than offsetting the prefab child.
- **Selected:** normalize each action separately so its lowest point sits on the ground, applied through `Hips.location` in the
  animation — not through the object origin, whose earlier movement had caused the unit confusion.
- **Selected:** prefab scale `2.0`, as the owner requested after seeing the corrected model.
- **Rejected:** the owner's offered backup FBX; the data was intact and only needed to be read in the right rotation mode.

### Rationale

The shrink and the dead walk were both caused by mixing an imported action with hand-authored ones without checking that they used
the same rotation representation and the same object-level channels. That check is now part of the verification list below.

### Implementation or verification result

Final state at scale 2: `idle lowY = 0.012`, `walk lowY = 0.001`, walk leg swing `0.251` against `0.000` before the fix, walk loop
seam `0.0069`, all three clips driving an identical child scale of `100.00`. 6 of 6 checks passed and the other six enemies were
re-verified unchanged. `SummonerBoss.asset` now points at `SummonerBossEnemy.prefab`, retiring the project's last placeholder.

Texture settings were applied as instructed: ETC2_RGB4 at max size 512 on both the default and Android platforms. The AI also
corrected `Chicken_BaseColor.png`, which it had left with no Android override in Entry 7 while every other enemy texture had one.

### Verification gap recorded

For several rounds the AI verified only that clips were marked looping and that feet met the ground, and never that a clip actually
moved. That is why the dead walk survived three review passes and was found by the owner instead. Animation verification in this
project should measure per-bone motion range, and should sample across the clip rather than at frame 0.

---

## Entry 11 — Document the runtime architecture and correct a mistaken premise about pooling

### Problem being addressed

The owner asked what `TowerNetworkSystem` does, how it differs from `TowerNetworkManager`, where the tick happens, and stated that
bullets and VFX were pooled but enemies were not.

### Prompt used

A sequence of questions ending in a request for a diagram of everything: the entry point, the places that talk to Unity, and how the
systems connect — "đúng trong codes".

### Important AI response

The AI corrected the pooling premise with file references: enemies *are* pooled, through `EnemyViewPool` using the same
`ComponentPool<T>` wrapper, the same capacity settings and the same one-pool-per-prefab dictionary as projectiles and hit VFX, and
registered at `LevelLifetimeScope.cs:70`. The only unpooled `Instantiate` is `TowerInstanceFactory`, and towers persist once placed.

On the architecture questions it established that `TowerNetworkSystem` has no tick at all and is absent from `LevelSystemGroup.Tick`;
the simulation is driven by `GameplaySimulationSystem`, which takes `TowerNetworkManager` directly and bypasses the facade. The split
exists because the manager is an application `Singleton` that outlives the level while the facade is `Scoped` per level and owns
`BeginLevelSession` / `EndLevelSession`. All ten manager partials contain no `using UnityEngine`, and positions cross the boundary as
a plain `TowerWorldPosition(float x, y, z)` rather than a `Vector3`.

### Option selected, revised, or rejected

- **Selected:** publish the map as an artifact with six hand-authored SVG schematics, each carrying one claim.
- **Selected:** encode the Unity boundary as a colour used consistently across prose, tables and diagrams, so it can be scanned.
- **Selected:** exclude test files from the dependency graph and state that exclusion in the footer.
- **Rejected:** describing the nine manager partials' internals, which were read only at their public API surface; that limit was
  stated rather than glossed.

### Rationale

The dependency graph was read from `private readonly` constructor fields across `Assets/Scripts/System` and
`Assets/Scripts/Application`, so the diagram reflects the code rather than an intended design.

### Implementation or verification result

Published at [claude.ai/code/artifact/f06e88d9](https://claude.ai/code/artifact/f06e88d9-66e9-4115-96fe-5b6960c07eb4). Three facts
surfaced that are not obvious from reading files individually: `ApplicationSystemGroup.Tick()` calls only `SafeAreaSystem.Tick()`;
application systems have no `LateTick` at all; and `GameplayUISystem` redraws through `RefreshIfDirty()` rather than every frame.

---

## Entry 12 — Audit the Bootstrap scene

### Problem being addressed

The owner asked how many scripts are attached to components in the Bootstrap scene.

### Prompt used

"trong bootstrap scene hiện đang có mấy script gắn vào component trong scene".

### Important AI response

An initial YAML grep undercounted at 15 because it matched only `fileID: 11500000`, which covers project scripts and misses every
component from a Unity package. Unity's own component enumeration gave the correct figures and was used instead.

The scene holds **37 GameObjects, 56 MonoBehaviour instances, 18 distinct types**: 9 project types across 10 instances, and 9 package
types across 46 instances, of which `Image` ×19, `Text` ×13 and `Button` ×7 dominate. There are three root objects, and the entire
composition root is one component — `ApplicationLifetimeScope` on an empty, childless `Application Systems` object. No missing
scripts were found.

### Option selected, revised, or rejected

- **Selected:** report Unity's live enumeration and state that the earlier grep undercounted.
- **Rejected:** acting on the level-catalog observation below; the owner explicitly deferred it.

### Rationale

A scene-YAML grep cannot see package components without also matching their differing script fileIDs, so it is the wrong instrument
for a total count.

### Implementation or verification result

Verified against the live scene. One observation was raised and, at the owner's instruction, **not acted on**: the menu shows two
level buttons because `LevelMenuView` uses an authored `LevelButtonView[]` array rather than spawning at runtime, and
`LevelCatalog.asset` declares only Level 1 and Level 2. `Level_003` through `Level_008` exist as scenes but are not reachable from
the menu; adding one requires editing both the catalog asset and the scene.

---

## Carried forward

- Two skill clips are imported but cannot play: `Chicken|Crow` and `RoyalShadowRat|Screech`. `EnemyLocomotion.controller` exposes only
  `Idle` and `Move` driven by an `IsMoving` bool, and `EnemyView` sets only that bool. All seven enemies share this controller, so
  adding a skill state is one change for the whole roster.
- `MagicResistant 1.prefab` sinks `−0.203` below ground during its walk clip — deeper than the boss defect fixed in Entry 10. Pre-existing.
- `Assets/Scripts/Editor/EnemyAuthoring/StealthEnemyPrefabWiring.cs` remains untracked and still carries the clip-matching defect from Entry 1.
- The generated `AGENTS.md` folder maps still reference Shortcut Builder until the next Better Context scan.
- `Level_001_Waves.asset` currently spawns the boss in **wave 1**, set by the owner for testing; it should return to wave 8.
