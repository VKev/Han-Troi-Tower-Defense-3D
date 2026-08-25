# AI Collaboration Log — Tower Presentation — 25 August 2026

## Session continuity

- **Project:** `TowerDefense3D`
- **Responsible Codex session:** `01a0135e-7cb3-7490-827f-cf7d34d7b651`
- **Related record:** `AI_Collaboration_Log_TowerNetwork_22_08.md`
- **Implementation issue:** `TowerDefense3D-t9ag`
- **Documentation issue:** `TowerDefense3D-q817`

This file records the consequential projectile-presentation and tower-placement corrections completed on 25 August. It
summarizes the verified work without reproducing the raw transcript.

## Entry 1 — Keep projectile VFX visible and point each authored effect along its travel direction

### Problem being addressed

Projectile prefabs were assigned to the tower definitions, but their effects could appear invisible during flight. After the
visibility problem was corrected, directional effects still faced the wrong way instead of following the shot trajectory.

### Prompt used

The project owner asked the AI to diagnose the invisible projectile effects, make them visible during travel, and correct their
orientation without changing the authored VFX assets unnecessarily.

### Important AI response

The projectile pool had been replaying particle systems and trails on every position update. The AI changed the lease behavior
so an effect starts only when its pooled view is first activated; later simulation updates move the existing effect without
restarting it. The projectile view now rotates the prefab's authored local `+Z` axis toward each non-zero movement direction.
Zero-distance updates retain the previous direction, and returning a view to the pool restores its authored local rotation.

### Option selected, revised, or rejected

- **Selected:** preserve the original third-party projectile prefabs and their materials.
- **Selected:** orient the pooled presentation from the actual movement vector rather than from tower-to-target data cached at
  spawn time.
- **Selected:** retain the last valid rotation when a simulation update has no displacement.
- **Rejected:** scaling the effects or repeatedly restarting their particle systems as a visibility workaround.

### Implementation or verification result

A runtime direction probe reported matching forward and travel vectors of `(1, 0, 0)`. The projectile effect remained visible
while moving and faced the shot direction. The later combined focused EditMode run covered the direction and pooling contracts
and passed with the rest of the related fixtures.

## Entry 2 — Ground every tower from renderer bounds and launch projectiles from the visible model center

### Problem being addressed

Projectile effects spawned close to the ground because tower simulation nodes used the prefab root pivot. Water and Earth also
appeared sunk into the board after drag placement because their imported FBX pivots did not coincide with the bottom of their
rendered geometry.

### Prompt used

The project owner requested that effects originate from the middle of each tower, that Water and Earth stand correctly on the
painted cell, and that every other tower be checked for the same issue.

### Important AI response

The AI measured the combined enabled-renderer bounds instead of assuming that all imported pivots share one convention.
`TowerInstanceFactory` now moves a newly instantiated tower only on Y until the bottom of its combined renderer bounds matches
the placement surface. `TowerRuntimeView` derives a `ProjectileOrigin` from the center of the combined renderer bounds, while
keeping the separate top-biased presentation anchor. `TowerNetworkSystem` registers the runtime tower node at that projectile
origin, and the obsolete root-pivot `WorldPosition` contract was removed.

Before correction, the renderer-bottom offsets relative to the prefab root were `0.000` for Generator, Fire, Wind, and Sink,
`-1.015` for Water, and `-0.852` for Earth. The generic bounds correction therefore leaves already-grounded prefabs unchanged
while lifting only the models that require it.

### Option selected, revised, or rejected

- **Selected:** one renderer-bounds rule for all tower prefabs rather than per-prefab hard-coded offsets.
- **Selected:** use the visual center for projectile simulation and the visual top for the existing presentation anchor.
- **Selected:** preserve each prefab's X/Z placement, scale, authored hierarchy, and asset data.
- **Rejected:** editing the imported Water and Earth FBX pivots or adding one-off placement exceptions.

### Implementation or verification result

A six-prefab numeric probe placed each tower on a test surface at `Y = 10`. All renderer bottoms resolved to `10.000`.
Generator, Fire, Wind, and Sink required no lift; Water received `1.015`, and Earth received `0.852`. Their projectile origins
resolved to the visible renderer centers: Generator `11.179`, Fire `10.892`, Water `11.015`, Wind `10.828`, Earth `10.854`,
and Sink `10.971`.

Real Play Mode drag placement in `Level_001` also verified the two affected prefabs. Water matched surface
`Y = -3.230` with projectile-origin `Y = -2.215`; Earth matched the same surface with projectile-origin `Y = -2.376`.

## Entry 3 — Verify the correction across all six tower types

### Problem being addressed

The visible defect was reported first on Water and Earth, but a shared placement and projectile-origin change could regress any
tower family if verified only against those two prefabs.

### Prompt used

The project owner explicitly asked the AI to check the other towers as well.

### Important AI response

The AI exercised Generator, Fire, Water, Wind, Earth, and Sink through the same renderer-bound calculation and kept the fix in
project-owned generic code. No prefab or third-party VFX asset needed a compensating modification.

### Option selected, revised, or rejected

- **Selected:** extend the existing placement and presentation contract tests with all tower families.
- **Selected:** verify actual Water and Earth drag placement in Play Mode in addition to deterministic bounds tests.
- **Rejected:** treating the correction as complete after a single prefab or Scene-view-only inspection.

### Implementation or verification result

The focused EditMode fixtures passed `28/28`, the GridPlacement PlayMode fixture passed `2/2`, and the Unity Console contained
zero errors. Better Context was refreshed after the implementation; its source hash was `afa2a29f11d6`. The implementation
issue `TowerDefense3D-t9ag` was closed after verification.

## Entry 4 — Let projectile trails finish and hide tower links during an active Wave

**Responsible session:** `01a02a90-cb3e-7523-97dd-8f9f705f3685`

### Problem being addressed

Projectile views returned to their pool immediately after reaching a destination, so the object deactivated before its trail
could visually catch up. The tower-link lines also remained visible throughout combat even though topology cannot be edited
during an active Wave.

### Prompt used

The project owner asked for a short projectile despawn delay so authored trails can finish, and requested that links between
towers no longer be visualized after entering a Wave.

### Important AI response

The AI kept the delay designer-driven by reading the longest `TrailRenderer.time` authored on each projectile prefab.
`TowerProjectileView` stops particle emission at retirement without clearing the trail. `TowerProjectilePoolView` removes the
view from the active projectile map, advances its remaining retirement time through the existing level `LateTick`, then resets
and releases it to its prefab-specific pool. No `MonoBehaviour.Update`, coroutine, or fixed magic delay was added.

`TowerLinkPresentationSystem` now reads the active state through `IWaveSystem`. While the Wave is running it renders no link
items; the underlying network topology remains unchanged and the links are reconstructed from the current model once the game
returns to Preparation.

### Option selected, revised, or rejected

- **Selected:** derive the release delay from the prefab's longest authored trail duration.
- **Selected:** advance retirement from the existing single level lifecycle entry point.
- **Selected:** hide only tower-link presentation during `WavePhase.Running`; preserve the network model.
- **Rejected:** a hard-coded delay, per-projectile coroutine, component-owned `Update`, or deleting links when combat starts.

### Implementation or verification result

The added pool contract test confirms a trail-bearing projectile stays active until the authored duration expires and only then
becomes reusable. Unity compiled with zero errors. The complete EditMode suite passed `282/282`, the complete PlayMode suite
passed `14/14`, and the only remaining Console warning came from the Unity AI package waiting for its external Account API.
The local tracking issue is `TowerDefense3D-5ah3`; no Player build or remote push was performed.

## Entry 5 — Tune the authored projectile speed and visual scale

**Responsible session:** `01a02a90-cb3e-7523-97dd-8f9f705f3685`

### Problem being addressed

After the prefab-specific projectile presentation was playable, the project owner found that shots travelled too slowly and
the imported VFX occupied too much screen space.

### Prompt used

The project owner asked how to make projectiles fly faster, then authored additional projectile and configuration adjustments
before requesting that all current work be committed and recorded.

### Important AI response

Projectile travel speed remains data-driven in `TowerCombatRules.asset`; the authored value is now `20` metres per second. The
root scale of the Default, Fire, Stone, Water, and Wind projectile prefabs is now `0.5`, preserving their internal particle and
trail composition while reducing the complete effect consistently.

### Option selected, revised, or rejected

- **Selected:** tune travel speed in the shared combat-rules asset.
- **Selected:** tune each authored projectile prefab at its root instead of adding a runtime scale multiplier.
- **Rejected:** hard-coding speed or scale inside projectile presentation code.

### Implementation or verification result

The authored values participated in the same Unity compile, EditMode `282/282`, and PlayMode `14/14` verification reported in
Entry 4. They are grouped with the prefab-specific projectile presentation feature commit; no remote push was performed.
