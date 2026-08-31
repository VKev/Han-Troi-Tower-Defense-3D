# Link Tower Facing Technical Specification

- **Status:** Approved
- **Approved by:** Project owner
- **Approval date:** 2026-08-16
- **Tracking issue:** `TowerDefense3D-ucg`
- **Target:** `Documents/Prototype/Arcane-Arsenal-Link`

## Objective

Make every ammunition-emitting source tower in the explicit-link prototype visually face its currently linked receiver so the tower silhouette reinforces the authored projectile route.

## Approved Scope

- Apply only to `Documents/Prototype/Arcane-Arsenal-Link`.
- On a successful link, rotate the source tower's visual root around the vertical axis so its local positive-X facing axis points toward the receiver.
- Recompute the facing after relinking and after either linked tower moves.
- Reset an unlinked source to the default zero-angle orientation when its link is removed, invalidated, or its receiver is sold.
- Preserve projectile endpoints, network lines, link validation, tower stats, topology rules, and all Rotation prototype behavior.
- Publish deterministic per-link facing error and cover the real desktop/mobile link interaction.
- Deploy the Link production alias after verification passes.

## Design Contract

Linking remains the player's primary routing verb. The tower turn is immediate visual confirmation of the same source-to-target relationship already represented by the persistent network line; it does not add aiming, travel time, or a new control.

## Runtime Contract

- `facingAngle = atan2(target.z - source.z, target.x - source.x)`.
- The existing model convention uses local `+X` as forward, therefore `group.rotation.y = -facingAngle`.
- Link-mode orientation is derived from `outputTargetId`; it is not independently authored state.
- A missing or invalid link sets `outputTargetId = null`, `aimAngle = 0`, and visual yaw to zero.
- Rotation-mode code paths remain unchanged.
- Diagnostics expose each valid link's absolute wrapped facing error in radians.

## Verification Plan

1. Run the TypeScript/Vite production build.
2. Link a source to a highlighted receiver through mouse and touch input and assert facing error is below `0.001` radians.
3. Verify deterministic multi-link layouts publish the same bound for every source.
4. Verify reciprocal/completed-relay rejection still preserves existing links and their facing.
5. Run affected gameplay, onboarding, bot, and visual regression checks.
6. Deploy the Link Vercel project and repeat the desktop/mobile facing diagnostic against the production alias.

## Non-Goals

- Smooth turn interpolation or rotation speed tuning.
- Rotating Amplifier or Nổ, which do not emit linked ammunition.
- Any change to the Rotation prototype.
