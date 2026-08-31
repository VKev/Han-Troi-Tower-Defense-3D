# Link Explosion Tower Technical Specification

- **Status:** Approved
- **Approved by:** Project owner
- **Approval date:** 2026-08-16
- **Tracking issue:** `TowerDefense3D-0ra`
- **Target:** `Documents/Prototype/Arcane-Arsenal-Link`

## Objective

Replace the Link prototype's directional Nexus Lance with a compact special tower named Nổ. Nổ stores inherited rounds, automatically detonates when charged and a same-layer enemy enters its one-cell blast radius, and provides a clear radial world-space payoff. Tighten explicit routing so a relay that already has an input and an output cannot accept another new input.

## Approved Scope

- Keep the internal `lance` tower identifier for compatibility, while replacing all player-facing Lance naming and directional behavior with Nổ.
- Preserve the special tower's ammunition storage, inherited elements, automatic activation, cost, footprint, and upgrade threshold progression.
- Set the blast radius to exactly one logical grid cell (`CELL_SIZE`, currently `2` world units), measured horizontally from the tower center.
- Affect every living enemy on the same firing layer whose center is inside that radius.
- Wait at full charge until at least one valid enemy enters the blast radius instead of wasting the stored skill.
- Replace the barrel silhouette and beam with a radial reactor silhouette, charge feedback, concentric shockwave, flash, and radial particles.
- Revise Level 2 Wave 4 guidance so Nổ is placed beside the route, then charged by a dedicated Foundry through an explicit link.
- Reject a new link into a target that already has at least one incoming link and a non-null outgoing link. Existing links remain valid, and a target with no outgoing link may still receive multiple inputs.
- Update localized UI copy, diagnostics, deterministic desktop/mobile tests, player documentation, and the AI collaboration log.
- Deploy the Link prototype only after verification passes.

## Non-Goals

- Do not modify the Rotation prototype.
- Do not migrate the internal `TowerType` key from `lance` in this iteration.
- Do not change tower economy, enemy waves, enemy movement, elemental reaction formulas, projectile speed, or other tower mechanics.
- Do not add LOD or external art assets.

## Design Contract

The player places Nổ beside a chokepoint and routes valuable elemental rounds into it. Full charge creates anticipation; the first same-layer enemy that enters the one-cell zone triggers a readable radial burst that rewards lane coverage and dense enemy timing. A completed relay can no longer become an unlimited merge point, so network topology remains legible and deliberate.

## Runtime Contracts

### Nổ activation and damage

- `EXPLOSION_RADIUS = CELL_SIZE`.
- Activation requires `buffer.length >= threshold`, cooldown ready, and at least one living same-layer enemy with horizontal center distance `<= EXPLOSION_RADIUS`.
- Activation consumes the threshold's oldest rounds.
- Skill elements are the unique union of consumed-round elements.
- Skill damage preserves the existing special multiplier: average consumed-round damage multiplied by `2.2 + level × 0.42`.
- Every valid enemy in the radius receives one hit through the existing projectile-hit pipeline, preserving elemental status, reaction, armor, flash, particles, and rewards.
- Cooldown remains `2.2 / TOWER_FIRE_RATE_MULTIPLIER`.
- Upgrade levels increase damage and reduce required charge; radius remains one cell.

### Incoming-link lock

- A target is considered a completed relay when it has at least one incoming link and one outgoing link.
- If the source is not already linked to that target, validation rejects a new incoming link to a completed relay.
- The same validation powers candidate highlighting, pointer/touch linking, programmatic routing, and runtime link checks.
- Existing incoming links are not invalidated when the target later gains an output.
- Direct reciprocal links, layer, range, receiver, and terrain rules continue to apply.
- Localized rejection reason: `Trụ đích đã có đầu vào và đầu ra; không thể nhận thêm liên kết.`

### Tutorial contract

- Level 2 Wave 4 names and previews introduce Nổ rather than Thương.
- The suggested Nổ footprint is visible, on layer 0, legal, and close enough to the route for its one-cell radius to cover the ground lane.
- The required dedicated Foundry is placed on the same layer and within a clear valid link range of Nổ.
- The tutorial starts the wave only after that Foundry is linked into Nổ.
- Nổ's world-space charge bar remains visible and its first charged detonation is readable without relying on text.

## Architecture and Ownership

- `src/game/definitions.ts`: Nổ player-facing definition and wave copy.
- `src/game/Game.ts`: link constraint, radial skill, tutorial placement scoring, UI copy, VFX, diagnostics, and hooks.
- `src/assets/ArtFactory.ts`: procedural Nổ tower silhouette.
- `src/vite-env.d.ts`: deterministic diagnostic and hook contracts.
- `tests/visual.spec.ts`, `tests/bot-playtest.spec.ts`, and visual snapshots: regression coverage.
- `README.md` and `Documents/AICollaboration/`: release and decision records.

## Verification Plan

1. Run the production TypeScript/Vite build.
2. Verify desktop and mobile source-target linking rejects a new input to a completed relay while preserving its existing input/output.
3. Verify terminal Nổ stores rounds and emits no projectile.
4. Trigger a deterministic full charge with inside-radius, outside-radius, and different-layer enemies; assert only the same-layer inside-radius set takes damage.
5. Verify the world charge bar, one-cell blast radius, anchored radial VFX, and distinct procedural tower model.
6. Complete the Level 2 Amplifier/Nổ tutorial flow on desktop and mobile and verify its suggested Nổ placement covers the lane.
7. Run gameplay bot, responsive canvas/UI checks, and affected visual regression baselines.
8. Deploy the Link Vercel project, verify the production alias and hashed assets return HTTP 200, then live-smoke the diagnostics.

## Risks and Mitigations

- **Tutorial placement too far from lane:** enforce a lane-distance constraint and deterministic test metric instead of relying only on screenshots.
- **Existing networks invalidated by the new relay rule:** exempt already-established source-to-target links during validation and test link preservation.
- **Radial effect obscures enemies:** keep the effect short, transparent, ground-aligned, and no larger than the mechanical radius.
- **Special wastes charge:** require an in-radius same-layer enemy before consumption.

## Deferred Work

- A new internal tower key replacing `lance`.
- Radius-changing upgrades or alternate Nổ skill modes.
- Final audio asset production and device-based haptic tuning.
