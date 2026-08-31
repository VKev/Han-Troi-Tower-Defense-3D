# AI Collaboration Log — Element Presentation — 27 August 2026

## Session continuity

- **Project:** `TowerDefense3D`
- **Responsible Claude Code session:** `0b76a022-cd6b-4cdd-a9f7-c19b848c7086`
- **Related record:** `AI_Collaboration_Log_WaveEnemy_25_08.md`
- **Implementation issue:** `TowerDefense3D-zss5` (closed after verification)

This file records the element-presentation work completed on 27 August, together with the tower, rendering, and repository
decisions taken in the same session. It summarizes the verified work without reproducing the raw transcript.

## Entry 1 — Bake the element icon UVs so each atlas tile reads upright

### Problem being addressed

The element status feature was already largely built: a 2048x2048 four-tile atlas, a four-quad FBX sharing one material, the
runtime view, and the enemy prefab wiring. The remaining task was the UV bake. Rendering the prefab showed all four icons
drawn **rotated 180 degrees** — the flame pointed down, the wind swirl sat on the wrong side.

### Prompt used

The project owner asked the AI to continue the in-progress bead covering the element icon UV bake.

### Important AI response

The AI verified the atlas itself was correct (Fire top-left, Water top-right, Earth bottom-left, Wind bottom-right) and that
the FBX UVs already mapped each quad to its own tile. The defect came from Unity mirroring the X axis when importing the
Blender export, combined with the `-90` degree X rotation the exporter places on the model root: together they flip both the
horizontal and the vertical axis of the drawn image.

The AI established that this could not be corrected in the billboard. Rotating `iconRoot` also rotates the positional axis the
reaction pair uses, which would swap the left/right order of the two icons. The correction therefore had to live in the UV
bake itself.

### Option selected, revised, or rejected

- **Selected:** rotate each quad's UVs 180 degrees inside its own atlas tile in Blender, then re-export the FBX.
- **Selected:** reproduce the original export settings exactly (`apply_scale_options=FBX_SCALE_ALL`, `axis_forward=-Z`,
  `axis_up=Y`), confirmed by the re-export matching the previous file byte size and emitting no stray `Lcl Scaling`.
- **Rejected:** compensating in `EnemyElementStatusView`'s billboard, which would have reversed the reaction pair order.
- **Rejected:** editing the atlas image or adding per-quad rotation overrides in the prefab.

### Implementation or verification result

The `.meta` GUID was untouched, so the prefab kept every mesh and material reference. Rendering the prefab through a URP
render request with the runtime billboard applied showed four upright icons, with left-to-right order matching
`localPosition.x`, which confirms `ShowReaction` places its first element on the left. A new EditMode test,
`IconMeshes_BakeTheirOwnAtlasTileUpright`, locks the vertex-to-UV mapping per element so a future re-export cannot silently
regress it. EditMode passed `316/316` and PlayMode passed `15/15`.

## Entry 2 — Keep the icon the same size on every enemy and resolve the billboard once

### Problem being addressed

Icons appeared at different sizes per enemy, and the camera-facing code ran its full billboard computation every frame.

### Prompt used

The project owner asked for a uniform icon size across enemies and, separately, that the camera-facing script only run at
start.

### Important AI response

Enemy root scales range from `1.0` to `2.0`, so the icon's world scale ranged from `0.55` to `1.10`. `Configure` now sets
`iconRoot.localScale` to cancel the parent's `lossyScale`, pinning the world size to a serialized `iconWorldScale`.

The AI reported that a literal once-only billboard call would break: `EnemyView.Render` rotates the **enemy root** to face its
movement direction, so an icon whose world rotation is set once would turn with the enemy and go edge-on. Because the board
camera never moves, the billboard rotation is a constant, so the AI moved the camera lookup and `LookRotation` into
`Configure` and left `LateUpdate` holding a single cached quaternion assignment. Removing the `worldCamera` field also removed
a latent null dereference.

### Option selected, revised, or rejected

- **Selected:** normalise the icon's world scale once in `Configure`, exposed as `iconWorldScale` for one-place tuning.
- **Selected:** cache the constant camera-facing rotation in `Configure`; keep a one-line re-assignment in `LateUpdate`.
- **Rejected:** a literal once-only rotation, which would let the rotating enemy root drag the icons with it.
- **Rejected:** detaching the icon root from the enemy, which trades one quaternion write for per-frame position tracking.

### Implementation or verification result

A render at enemy scales `1.0`, `1.48`, `1.69`, and `2.0` produced four identically sized icons, with `iconRoot.lossyScale`
measured at `0.55` in every case. A new EditMode test asserts the world scale stays constant under a scaled parent.

## Entry 3 — Raise the reaction cooldown and the icon dwell time to 0.5 seconds

### Problem being addressed

The reaction cooldown and the reaction icon display time were both `0.2` seconds, which the project owner found too short.

### Prompt used

The project owner asked for a `0.5` second element reaction cooldown and a `0.5` second icon despawn delay.

### Important AI response

`reactionCooldownSeconds` in `ElementReactionCatalog.asset` and `reactionDisplaySeconds` on the status view prefab both moved
to `0.5`. The two values now coincide, so the reaction icon pair stays visible for exactly the cooldown and no blank gap
appears before the enemy returns to `Ready`.

### Option selected, revised, or rejected

- **Selected:** tune both values in authored data rather than in code; the class defaults follow the prefab.
- **Selected:** update the dependent test expectations rather than loosen them, including the tick arithmetic in
  `EnemyElementReactionStateTests` (`0.5` s is ten ticks at the `0.05` s simulation tick).

### Implementation or verification result

EditMode passed `317/317` and PlayMode passed `15/15` after the change.

## Entry 4 — Show two icons for a same-element reaction

### Problem being addressed

A same-element reaction such as Fire + Fire displayed only one icon, offset to the right instead of a centred pair.

### Prompt used

The project owner asked that same-element reactions show two identical icons side by side.

### Important AI response

This was a real defect, not a missing feature. `ShowReaction` called `GetIcon` for both halves of the pair; when both elements
match, both calls return the **same** `Transform`, so the second `Show` merely overwrote the first one's position.

There is only one quad per element, so the second copy has to come from somewhere. The AI made every show assign the quad's
`sharedMesh` explicitly, then let a same-element pair borrow an idle quad and point it at the same mesh. Because the mesh is
re-assigned on every show, the borrowed quad can never keep a stale icon, so no restore bookkeeping is needed.

### Option selected, revised, or rejected

- **Selected:** borrow an idle quad and re-point its mesh; keeps four renderers on one shared atlas material.
- **Rejected:** authoring a fifth duplicate quad, which would need an FBX re-export and a prefab structure change.
- **Rejected:** rebuilding the view around two generic slots, a larger refactor of working, tested code.

### Implementation or verification result

Rendering the real prefab produced two flames for Fire + Fire, two droplets for Water + Water, and one of each for
Fire + Water, with the mesh swap confirmed in the log output. EditMode passed `318/318` and PlayMode passed `15/15`.

## Entry 5 — Record the authored element reaction matrix

### Problem being addressed

The project owner needed a readable reference for which element reacts with which.

### Prompt used

The project owner asked for the reaction matrix.

### Important AI response

All ten pairs are authored, so every element reacts with every element including itself. Same-element pairs give Blue Fire,
Water Pressure, Cyclone, and Stone Shatter; cross pairs give Thermal Shock, Firestorm, Sandstorm, and Quagmire, with Quagmire
the only reaction that creates a lingering field.

The consequential finding concerns the two `PureRewrite` pairs, Fire + Earth and Water + Wind. Despite the name, the state
machine treats them exactly like any other reaction: the mark is cleared and the enemy enters the cooldown, but nothing is
applied. In build terms **Fire/Earth and Water/Wind are anti-combos** — placing those two towers on the same projectile path
cancels each other's elemental output. A test named `PureRewritePairs_AreTerminalAndHaveNoSpecialEffect` confirms this is
intended behaviour rather than a defect.

### Option selected, revised, or rejected

- **Selected:** report the matrix from the authored catalog assets rather than from the enum or from memory.
- **Rejected:** treating the `PureRewrite` naming as evidence that the mark is re-applied.

### Implementation or verification result

No code or asset changed. The matrix was read from `ElementReactionCatalog.asset` and its ten `ElementReactionDefinition`
assets.

## Entry 6 — Return the Fire tower to a single projectile per cycle

### Problem being addressed

The Fire tower emitted three projectiles per cycle while every other element tower emitted one. Investigating the reaction
behaviour showed the second projectile triggers Blue Fire and the third is then swallowed by the cooldown, contributing
damage but no elemental effect.

### Prompt used

The project owner first asked whether the three-projectile Fire tower triggers Blue Fire, then asked that the Fire tower emit
a single projectile like the other towers.

### Important AI response

The three projectiles did not come from authored throughput. `TowerRuntimeSpecFactory` carried a Fire-only special case that
read `TierOne.OutputProjectileCount` even at base tier, bypassing `throughput.BatchSize`. Because no tier system is wired into
the spec factory, the Fire tower always ran tier-one behaviour.

Removing that special case, along with its reservation-count and sequence-spacing siblings, was enough: the Fire asset already
declares the same throughput as Water and Wind (`0.85` s cycle, batch size `1`, zero spacing). No asset needed editing, and
the `tierOne` data stays intact for when tier upgrades are implemented.

### Option selected, revised, or rejected

- **Selected:** route Fire through the shared throughput path and leave the tier-one authoring data untouched.
- **Rejected:** editing the asset to `outputProjectileCount: 1`, which fights the definition's own validation contract and
  destroys the tier-one design data.
- **Noted, not changed:** the payload still carries `DirectDamage x TierOne.DirectFireDamageMultiplier` (`6` magic), so Fire
  drops from `18` to `6` damage per cycle, close to Water and Wind at `5`. The AI also found that
  `conservesTotalFireDamageAcrossClones` was never implemented — each of the three clones previously dealt full damage.

### Implementation or verification result

`BlockedFireProcessor_KeepsInputAndReady` had to be rewritten rather than adjusted. Its back-pressure only occurred **because**
of the three-projectile burst; with one projectile the nexus consumes faster than the chain produces and never blocks. The
fixture now accepts a nexus cycle override to starve the sink, and the test steps until the blocked state is genuinely reached
instead of asserting on a hard-coded tick. EditMode passed `318/318` and PlayMode passed `15/15`.

## Entry 7 — Explain why some enemies never display an element icon

### Problem being addressed

The project owner reported that some enemies show no element icon.

### Prompt used

The project owner asked why some enemies were not displaying the icon.

### Important AI response

The AI audited the asset side and ruled it out: all seven enemy definitions carry the status view with
`ElementStatusEffectMultiplier = 1`, the prefab hierarchies are identical with the view as a direct child of the enemy root,
and the planner writes `ElementPhase` for every enemy on every tick. The cause is therefore gameplay-conditional, and two
code paths explain it:

- **Stealth while hidden.** `ResolveDirectHit` guards element application with `!wasHidden`, and the reveal assignment sits
  *after* that guard, so the projectile that reveals a Stealth enemy marks nothing. A second elemental hit must land inside
  the `2` second reveal window for an icon to ever appear.
- **Generator projectiles carry no element.** `TryGetElement` returns false for `ProjectilePayloadKind.Physical`, so an enemy
  that only crosses generator links is never marked regardless of its type.

### Option selected, revised, or rejected

- **Selected:** verify the asset wiring across all seven prefabs before proposing any change.
- **Rejected:** changing the Stealth reveal ordering unilaterally; moving the reveal before the element check is a design
  decision and was left for the project owner.

### Implementation or verification result

No change was made. The findings were reported with the specific call sites, and the project owner was asked which enemy and
which icon type is missing to separate the two paths.

## Entry 8 — Restore the Cartoon FX effects on the Mobile URP tier

### Problem being addressed

Every JMO Assets Cartoon FX Remaster effect rendered fully invisible.

### Prompt used

The project owner asked whether the invisible JMO effect prefabs could be fixed.

### Important AI response

The first suspicion, that the `.cfxrshader` files were compiled for the Built-In pipeline, proved to be a red herring: their
`.meta` files do record `Built-In Render Pipeline`, but the compiled shader carries proper `UniversalForward` passes, so the
label is merely stale.

The real cause is **soft particles**. Almost every CFXR material sets `_FADING_ON`, which samples `_CameraDepthTexture`.
`Mobile_RPAsset` has `supportsCameraDepthTexture = False` and is the active quality tier, so the depth sample returns zero,
the fade factor collapses, and the particle is drawn at zero alpha with no error or warning. The single effect that still
rendered, `CFXR2 Sparks Rain`, uses a `nosp` (no soft particles) material with `_FADING_ON = False`.

The AI proved the diagnosis by rendering one prefab on both quality tiers: `0` lit pixels on Mobile with depth off, `4570` on
PC with depth on.

### Option selected, revised, or rejected

- **Selected:** enable JMO's own escape hatch, `#define GLOBAL_DISABLE_SOFT_PARTICLES` in `CFXR_SETTINGS.cginc`, chosen by the
  project owner over the alternative.
- **Rejected:** enabling Depth Texture on `Mobile_RPAsset`, which adds a depth prepass to the tier that exists to avoid it.
- **Noted, not changed:** the CFXR `ScriptedImporter` caches its artifact by file hash and importer version, not by render
  pipeline, so a future pipeline switch requires a manual reimport of the four `.cfxrshader` files or the effects will go
  invisible again the same way.

### Implementation or verification result

After reimporting the four shaders on the Mobile tier: Explosion 1 `8950` lit pixels, Electrified 3 `16564`, WW Enemy
Explosion `2173`, Skull Head Alt `144`. None remained at zero. The accepted trade-off is a hard edge where particles
intersect geometry instead of a soft fade, which is negligible on stylized cartoon effects.

## Entry 9 — Diagnose the stalled Android Gradle build

### Problem being addressed

An Android build appeared frozen at `:unityLibrary:extractReleaseAnnotations` for more than seventeen minutes.

### Prompt used

The project owner asked whether that was normal.

### Important AI response

The task itself normally completes in seconds, so the duration was not normal, but the build was not hung. The Gradle daemon
was network-bound, not deadlocked: it held roughly ninety sockets to Google and Cloudflare hosts on port 443, was opening new
connections seconds before the check, and used under two percent of one core. The displayed task name was misleading because
`org.gradle.parallel=true` lets dependency resolution run alongside the last reported task.

The decisive measurement was the Gradle module cache: `119` MB written in twelve minutes, with `kotlin-compiler-32.0.0.jar`
(50 MB) landing during the check. The build was genuinely downloading a cold dependency cache over a slow link.

### Option selected, revised, or rejected

- **Selected:** let the build finish so the cache warms; subsequent builds reuse it.
- **Noted, not changed:** `android.enableJetifier=true` is set and the Android Gradle Plugin itself warns it is deprecated with
  a current default of `false`. No `.aar` or `.jar` exists under `Assets`, so no legacy support library justifies it, and
  disabling it would remove a per-artifact transform from every build.

### Implementation or verification result

No change was made. The build was confirmed to be progressing rather than stalled.

## Entry 10 — Group the session's work into feature commits

### Problem being addressed

The working tree mixed several features with pre-existing edits and Unity re-serialization churn.

### Prompt used

The project owner asked the AI to read the README and commit by feature, following the project standard.

### Important AI response

Seven commits were created on `master` following the README convention: Conventional Commit prefix, single-line Vietnamese
subject with only the first letter capitalized, no body, and no `Co-Authored-By` trailer. The AI followed the README over its
own default trailer behaviour.

The element status feature stayed as one `feat` commit of 45 files rather than being split by asset, script, and test,
because the split parts would not stand alone — the prefabs without their script would carry missing references.

### Option selected, revised, or rejected

- **Selected:** commit the wave asset change separately. It alters Level 001 wave 1 from one SummonerBoss to eight Basic
  enemies, was authored outside this session, and is trivially revertible on its own.
- **Selected:** isolate the Unity serialization churn (material `m_LockedProperties` and `m_AllowLocking`,
  `ParticleSystemRenderer` version `6` to `7`) in a final `chore` commit so it can be dropped without touching real work.
- **Rejected:** committing `Assets/Resources/Prefabs/VFX/VFX_Fire.prefab`, which appeared mid-session, is referenced only by a
  plugin demo scene, and belongs to the project owner's in-progress work.

### Implementation or verification result

Both deletions were checked before committing: the removed `SummonerBoss.prefab` has no remaining references, and the removed
archway texture is referenced only by a material that nothing else uses. No remote push was performed, as the README and
`AGENTS.md` require explicit authority for that.

## Entry 11 — Start the per-element mark effect with Fire

### Problem being addressed

Each element needs its own effect while it marks an enemy, beginning with Fire.

### Prompt used

The project owner asked that an enemy marked with the Fire element burn, using
`Assets/Resources/Prefabs/VFX/VFX_Fire.prefab`.

### Important AI response

The AI added `EnemyElementEffectView` alongside the existing status view, holding one slot per element so the remaining three
can be filled later without further structural change, and wired it into `EnemyView`'s `Bind`, `Render`, and `Release`. The
effect stops with `StopEmittingAndClear` so a pooled view cannot hand a frozen burst to the next enemy.

Authoring the VFX into the seven enemy prefabs was done through `PrefabUtility` rather than by hand, because each prefab
already contains a nested prefab instance. Two overrides were required on the source prefab's settings: `playOnAwake` from
`1` to `0`, otherwise every enemy burns from spawn, and `scalingMode` from `Local` to `Hierarchy` so the fire grows with the
enemy — bodies range from `1.27` to `3.0` units tall, so a fixed-size flame would be lost on a MiniBoss. This deliberately
differs from the icons, which the project owner asked to keep at a uniform size.

### Option selected, revised, or rejected

- **Selected:** a separate component rather than extending `EnemyElementStatusView`, which owns icons.
- **Selected:** author the effect into the prefabs and toggle it, consistent with the pre-authored, no-runtime-instantiation
  approach already used for the icons.
- **Selected:** scale the fire with the enemy while the icons stay uniform.

### Implementation or verification result

Unity compiled with zero errors and all seven prefabs were wired and verified as `playOnAwake=False`, `loop=True`,
`scaling=Hierarchy`. **Verification was not completed in this session** — the test suites were not re-run for this entry and
the visual check was still in progress when the session moved on. The component and its tests were subsequently extended
outside this session's verified scope.
