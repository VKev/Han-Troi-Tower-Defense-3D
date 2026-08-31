# Rotation Prototype Difficulty and Economy Technical Specification

- **Status:** Approved
- **Approved by:** Project owner
- **Approval date:** 2026-08-16
- **Tracking issue:** `TowerDefense3D-612`
- **Target:** `Documents/Prototype/Arcane-Arsenal`

## Objective

Make the rotation prototype demand sustained elemental routing from wave 3 onward in Levels 2 and 3. Later waves must last longer, enemies must retain meaningful health after elemental reactions, and expanding the tower field must incur escalating purchase costs.

## Approved Scope

- Leave the three-wave tutorial difficulty unchanged.
- In Levels 2 and 3, increase enemy counts from wave 3 onward so spawn duration grows from approximately 25% to 40% while existing spawn cadence remains continuous.
- Multiply existing wave health values by a progressive late-wave factor that begins at `1.10×` on wave 3 and reaches `1.45×` on the final wave.
- Reduce the rotation prototype's reaction damage contribution based on enemy maximum health from `6%` to `3.5%`.
- Increase the current purchase price of every tower type by `12%` per tower already placed, capped at `+120%`.
- Display the current dynamic purchase price in each build card and use the same value for affordability checks and payment.
- Publish deterministic diagnostics and regression tests for wave counts, health, spawn duration, reaction scaling, and tower price inflation.
- Deploy only after desktop/mobile verification passes.

## Non-Goals

- Do not change the link prototype.
- Do not change tutorial wave composition, tutorial health, or tutorial tower flow.
- Do not change base tower prices, upgrade costs, move costs, sell values, kill rewards, wave-clear rewards, enemy movement speed, projectile tuning, tower cadence, map geometry, or routing controls.
- Do not add enemy LOD or simplify detailed enemy rendering.

## Design Contract

The player rotates and chains towers to sustain elemental coverage while increasingly durable and longer-running enemy formations pressure the Nexus. Reactions remain strategically necessary but no longer remove a disproportionate share of high-health enemies, and each additional tower becomes a progressively more expensive commitment.

## Data and Runtime Contracts

### Wave pressure

- Wave 1 and wave 2 retain authored counts and health multipliers.
- Wave 3+ sequence counts are rounded from `baseCount × durationFactor`.
- Level 2 duration factors: `[1.00, 1.00, 1.25, 1.30, 1.35, 1.40]`.
- Level 3 duration factors: `[1.00, 1.00, 1.25, 1.27, 1.29, 1.31, 1.33, 1.35, 1.37, 1.40]`.
- Existing per-sequence spawn intervals and formation patterns remain unchanged, so additional units extend the spawn window instead of introducing artificial idle gaps.
- Level 2 health factors: `[1.00, 1.00, 1.10, 1.22, 1.34, 1.45]`.
- Level 3 health factors: `[1.00, 1.00, 1.10, 1.15, 1.20, 1.25, 1.30, 1.35, 1.40, 1.45]`.

### Reaction scaling

- `REACTION_MAX_HP_DAMAGE_RATIO` becomes `0.035` in the rotation prototype only.
- Flat and projectile-derived reaction bonus components remain unchanged.

### Dynamic tower prices

- `priceMultiplier = 1 + min(1.20, placedTowerCount × 0.12)`.
- `currentPrice = ceil(basePrice × priceMultiplier)`.
- The price is sampled when placement begins and revalidated when placement completes.
- The build UI always renders the current price from the same pricing function.
- Selling, moving, and upgrading retain their existing calculations.

## Architecture and Ownership

- `src/game/definitions.ts`: authored wave counts and health multipliers.
- `src/game/Game.ts`: reaction ratio, dynamic purchase-price function, affordability/payment integration, UI price rendering, and diagnostics.
- `src/vite-env.d.ts`: diagnostic type contracts.
- `tests/visual.spec.ts` and `tests/bot-playtest.spec.ts`: deterministic economy and difficulty assertions.
- `Documents/AICollaboration/`: decision and verification record.

No new runtime module or dependency is required.

## Compatibility and Migration

- No save-data migration is required; the browser prototype resets per run.
- Existing test hooks and Vercel static-hosting assumptions remain compatible.
- The Link folder and production alias must remain untouched during this implementation.

## Verification Plan

1. Run TypeScript/Vite production build.
2. Assert tutorial wave arrays are unchanged.
3. Assert Level 2 and Level 3 wave 3+ counts, health multipliers, and derived spawn windows match the approved factor-based authored values; assert density remains progressive between waves.
4. Place towers through real pointer/touch input and verify displayed/current prices rise by 12% per tower and cap at +120%.
5. Verify a reaction reports a `3.5%` maximum-health component.
6. Run affected desktop/mobile Playwright tests and the gameplay bot.
7. Deploy the rotation Vercel project, verify alias and hashed assets return HTTP 200, then probe live diagnostics.

## Risks and Mitigations

- **Mobile crowd cost:** longer waves increase detailed enemy counts. Preserve cadence rather than compressing spawns, then inspect stress diagnostics and active play before release.
- **Tutorial affordability:** inflation also applies to tutorial purchases. The existing 420 Arcana budget must be verified against the four required purchases.
- **Stale UI pricing:** all price labels and purchase checks must use one pricing function and refresh after placement, sale, reset, and stage switch.
- **Test duration:** extended waves can exceed prior bot timeouts. Increase only bounded test budgets when the game continues making measurable progress.

## Deferred Work

- Final economy tuning from player telemetry.
- Per-tower-family inflation curves.
- Refunds based on historical purchase price.
- Hardware-device thermal and long-session performance profiling.
