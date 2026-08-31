# AI Collaboration Log — Hero Tower — 31/08/2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Area:** Hero tower family, unlock progression, authored tower adoption, and build-bar lock state
- **Responsible Claude Code session:** current local session

This record captures the decisions and evidence for turning the Level 7 crab prop into a placeable
Hero tower without reproducing the raw conversation.

## Entry 1 — Merge the crab and the frog stand into one Hero tower

### Problem being addressed

The crab in Level 7 was a loose prop under `Grid Placement/Visuals`. It was not a tower: the network
did not know about it, it could not be selected, and the cells beneath it stayed free for other
towers. The project owner asked for the crab and the FrogStand pedestal to become one prefab, wired
as a special "Hero" tower rather than a Generator, Sink, or Element.

### Prompt used

The project owner asked to merge the crab with the frog stand into a single prefab called a Hero
tower, wire it through the tower system so the player can place it, and give it a default footprint
of 3 on X and Z and 2 on Y.

### Important AI response

The tower system already routes every buildable tower through `TowerCatalog`, `TowerCombatDefinition`,
`TowerDefinition` (placement), and `TowerRuntimeSpecFactory`. Adding a family rather than a bespoke
object meant placement, selection, selling, and occupancy came for free. `TowerRuntimeSpec` validates
by network role, so the hero had to pick one; a source is the only role with no input queue, which
matches a tower that strikes on its own instead of relaying an upstream projectile.

The two source prefabs carried leftover scene offsets in their roots, so their local transforms could
not be reused as-is. Their real bounds were measured in the Editor first: the stand is 1.032 tall once
its authored -90 degree X rotation is applied, and the crab's mesh dips 0.291 below its own pivot.

### Option selected, revised, or rejected

- **Selected:** add `TowerFamily.Hero` and a `HeroTowerDefinition` with `TowerNetworkRole.Source`,
  an `attackRangeMeters` field, and an `unlockAfterClearingLevel` field.
- **Selected:** compose `CrabHero.prefab` as a plain root holding `TowerRuntimeView`, with the stand
  lifted by half its thickness and the crab lifted by the stand's full height plus its own pivot dip,
  so the assembled bounds start at exactly y = 0 and fit a 3x3 footprint.
- **Selected:** author `HeroTower.asset` with footprint `3 x 3 x 2` and `CrabHero.asset` at build cost
  150, attack range 6 m, attack damage 14.
- **Rejected:** a bespoke non-tower crab behaviour, which would have duplicated placement, selection,
  and occupancy logic already owned by the tower systems.
- **Rejected:** hand-writing the prefab YAML, because the two source prefabs are themselves prefab
  instances with authored offsets that are easy to get wrong outside the Editor.

### Rationale

A hero that is a real catalog family inherits every existing tower affordance and stays one asset to
tune. Registering it as a source keeps it inert in the current simulation - valid chains still require
a Generator source and a Soul Nexus sink - so the family can ship before hero combat exists.

### Implementation or verification result

- `TowerDataValidator.CollectErrors` reports zero errors for the six-entry catalog with placement
  definitions required.
- A hero registered into `TowerNetworkManager` resolves to `family=Hero role=Source cycleTicks=24
  out=1 payload=Basic dmg=14` and contributes zero valid chains, as intended.
- `CrabHero.prefab` measures 2.880 x 2.922 x 2.961 with its minimum Y at -0.002.

## Entry 2 — Separate "cleared" from "unlocked" in save progress

### Problem being addressed

The hero has to stay locked until the player beats Level 7, but `UnlockProgress` only tracked unlocked
levels, and `LevelMenuFlow` unlocks a level the moment its menu button is tapped. Nothing in the
project recorded that a level had actually been won, so there was no signal to gate the hero on.

### Prompt used

The project owner asked for the hero to be locked at first and to unlock once the player clears
Level 7.

### Important AI response

Reading "unlocked level 8" as a proxy for "cleared level 7" would break for the last authored level
and would misfire the moment level unlocking changes. A separate cleared set is the honest record.
`LevelOutcomeHudPresenter.Refresh` already detects `WavePhase.Victory` every dirty frame, so it is the
natural place to raise a one-shot clear signal.

### Option selected, revised, or rejected

- **Selected:** add a cleared-level set to `UnlockProgress` alongside the unlocked set, with
  `IsCleared` and `TryMarkCleared`; clearing a level also marks it unlocked.
- **Selected:** add `clearedLevelNumbers` to `SaveSnapshot` as an additive field and keep
  `schemaVersion` at 1, so an existing save deserializes the field as empty instead of failing
  validation and wiping progress.
- **Selected:** raise the clear through an optional `reportLevelCleared` callback on
  `LevelOutcomeHudPresenter.BindLevel`, routed by `LevelLifetimeScope` to
  `GameFlowSystem.ReportLevelCleared` and on to `LevelMenuFlow.MarkLevelCleared`.
- **Rejected:** bumping the save schema version, which would have invalidated every existing save.
- **Rejected:** making `GameFlowSystem.ShowSaveWarning` public so the Application assembly could report
  a failed write directly; keeping the write inside `LevelMenuFlow` leaves the save warning internal.

### Rationale

The menu deliberately unlocks levels on demand, so unlocking cannot mean progression. A second set is
cheap, survives the run, and gives any future gated content one thing to ask. Keeping the schema
version stable matters more than field tidiness because the save is the player's only progress.

### Implementation or verification result

- `SaveSystem.TryMarkClearedAndSave` writes only when the level was not already cleared, so replaying
  a cleared level costs no write.
- `SaveSnapshot.TryValidate` rejects non-positive cleared level numbers.
- The 227 EditMode tests, including the existing save round-trip and corruption-recovery coverage,
  pass unchanged.

## Entry 3 — Adopt the Level 7 crab as a pre-placed tower

### Problem being addressed

The owner wanted the crab in Level 7 to behave as a tower that is already on the board when the level
starts. Nothing in the project could turn a scene-authored object into a tower network node, and an
authored transform does not land on a footprint's own bottom center.

### Prompt used

The project owner asked that in Level 7 the crab act as a tower placed in advance, with an attack
range.

### Important AI response

Adoption needs three things the placement flow normally does: configure the runtime view, claim the
cells under the footprint, and register the node. Gold must not be charged, because the level is
giving the tower away. The crab's authored cell was not buildable, so the hero also had to be moved
to the nearest cell where a 3x3 footprint actually fits.

### Option selected, revised, or rejected

- **Selected:** add `AuthoredTowerView`, a marker component holding the combat definition, and have
  `LevelLifetimeScope` adopt every one in the scene after the level systems have started.
- **Selected:** add `GridPlacementSystem.TryOccupyAuthoredTower`, which validates and commits the
  footprint and hands back the snapped bottom-center position.
- **Selected:** add `FootprintOrigin` and `SetFootprintOrigin` to `ITowerRuntimeView`, so an authored
  tower snaps onto the same grid a dragged one lands on.
- **Selected:** move the hero to board cell `(29, 27)`, three cells from where the crab was staged,
  found by walking outward with the real `PlacementValidator`.
- **Selected:** report an adoption failure as a warning and leave the object as scenery, rather than
  failing the whole level load.
- **Rejected:** having `TowerNetworkSystem` search the scene itself, which would have put
  `FindObjectsByType` inside the System layer.

### Rationale

Adoption through the same occupancy and registration path means the pre-placed hero is selectable,
sellable, and blocks its own cells exactly like a built tower, with no second code path to keep in
sync. Snapping at adoption time means level authors do not have to place authored towers on exact
cell centers by hand.

### Implementation or verification result

- Level 7 now holds `Crab Hero` under `Grid Placement/Placed Towers` at world `(13.800, -2.480,
  22.000)`; the loose `Crab` prop is gone.
- New PlayMode test `LevelSeven_AdoptsItsAuthoredHero_IntoTheTowerNetwork` loads Level 7 through the
  menu and asserts the hero is configured, holds a network node, and carries the 3x3x2 footprint.
- The attack range is data plus a Scene-view gizmo only. Combat does not consume it yet, because a
  hero source forms no valid chain under the current chain rules.

## Entry 4 — Show the locked hero greyed out in the build bar

### Problem being addressed

The build bar renders one button per catalog entry and drives interactability from a single
simulation-running flag. There was no way to show a tower the player has not earned, and
`GameplayHudPrototypeLayout` only warned when a catalog entry had no authored button.

### Prompt used

The project owner asked that the UI load the crab tower too, but render it grey and refuse drag and
drop while it is still locked.

### Important AI response

Hiding the button would make the hero invisible as a goal. Locking is per-definition and cannot change
mid-level, so it can be resolved once when the HUD connects rather than every render. Relying on
Unity's disabled tint alone would make a locked tower look identical to one blocked by a running wave,
so the lock needed its own flat colour and label.

### Option selected, revised, or rejected

- **Selected:** add `ApplyTowerLocks` to `ITowerNetworkHudView`, called once from
  `TowerNetworkHudPresenter.Connect` with the definitions whose `UnlockAfterClearingLevelNumber` is not
  yet cleared.
- **Selected:** add `SetLocked` to `TowerPlacementDragButtonView`; a locked button caches its unlocked
  colours, goes flat grey, reads `LOCKED` instead of a cost, refuses `OnBeginDrag`, and forces
  `SetInteractable` to false.
- **Selected:** teach `GameplayHudPrototypeLayout` to author a build button for any catalog entry that
  has none, instead of logging a warning and skipping it.
- **Selected:** expose the gate generically as `TowerCombatDefinition.UnlockAfterClearingLevelNumber`,
  defaulting to zero, so future gated towers need no new plumbing.
- **Rejected:** hiding locked towers, which removes the progression signal.
- **Rejected:** recomputing lock state every render, since save progress cannot change during a level.

### Rationale

Evaluating unlocks once per level entry also avoids a tower appearing mid-level the instant its own
level is won, which would be a confusing reward moment. Putting the rule on the base definition keeps
the presenter unaware of what a hero is.

### Implementation or verification result

- The rebuilt `GameplayUI.prefab` now carries six tower drag buttons, including the new `Hero` button.
- `AssertMigratedGameplayUi` in the PlayMode suite counts the hero button and asserts it loads locked,
  since those levels run on a fresh save.
- EditMode: 227 passed, 0 failed. PlayMode: 21 passed, 0 failed. The Editor reported zero compilation
  errors and zero warnings after the final change.
