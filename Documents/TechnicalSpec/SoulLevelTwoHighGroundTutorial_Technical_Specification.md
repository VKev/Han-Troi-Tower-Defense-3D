# Soul Level 2 High-Ground Tutorial — Technical Specification

**Status:** Approved  
**Approval date:** 2026-08-17  
**Tracking issue:** `TowerDefense3D-w33`

## Scope

This change revises Level 2 onboarding in the independent Three.js Soul prototype at `Documents/Prototype/Projectile-Network-TD/`. It inserts a mandatory high-ground anti-air lesson before the existing Hỗ Trợ and Xung Hồn lessons.

The approved lesson sequence is:

1. Wave 3 preparation: build and connect a functional high-ground anti-air branch.
2. Wave 4 preparation: insert Hỗ Trợ into the established ground network.
3. Wave 5 preparation: insert Xung Hồn into the established ground network.

## Player promise

The first flying wave must teach the spatial rule through action: towers on raised terrain defend Layer 1 only when their linked projectile segments cross the flying route. The player receives a complete free branch, performs every placement and link gesture, then observes the branch damage airborne enemies in combat.

## Interaction contract

- The lesson remains text-free and uses the existing animated hand, card highlight, grid/footprint preview, link-drag beam, valid-target feedback, and Start button highlight.
- Wave 3 grants one Giếng Hồn, one Hỏa, and one Băng tower on high terrain in that order.
- Suggested high-ground cells are chosen as a three-node route whose two internal links are legal, whose final Băng-to-Tỏa-Hồn link is legal, and whose projectile geometry crosses the Layer 1 enemy path.
- The player must drag each highlighted free tower card to its highlighted high-ground cell.
- After placement, the player must create `Giếng Hồn -> Hỏa -> Băng -> Tỏa Hồn` through three guided link drags.
- Wave 3 cannot start until the high-ground branch is complete and active.
- Hỗ Trợ remains locked until Wave 4. Xung Hồn remains locked until Wave 5.
- The Hỗ Trợ and Xung Hồn insertion lessons retain their existing two-link inline rewiring behavior and Start gating.

## Economy contract

- Every tower placed as part of the three mandatory Level 2 lessons costs `0` Gold.
- Tutorial grants have zero invested value, yield zero sale profit, and do not contribute to the paid-tower price-growth multiplier.
- Unrelated player purchases retain their normal dynamic prices.
- Selling a mandatory grant during preparation makes the missing lesson step available again for free.

## Runtime state and diagnostics

- A cached high-ground lesson plan records the three exact high-tier slot identifiers for the current run.
- Granted high-ground nodes record their lesson role independently from Hỗ Trợ/Xung Hồn insertion grants.
- Tutorial diagnostics expose the required node, required slot, complete state, high-ground plan, granted-node state, crossing distance, and active high-ground chain.
- Deterministic test hooks expose Level 2 Wave 3, Wave 4, and Wave 5 preparation states.

## Compatibility and non-goals

- Level 1 tutorial behavior, Level 3 behavior, projectile balance, enemy balance, camera controls, Soul Field behavior, art style, and the directed drag-link interaction remain unchanged.
- This change does not add a new tower type, enemy type, altitude, popup, text tutorial, or permanent progression.
- Existing unrestricted logical-grid placement remains available outside the mandatory lesson cells.

## Verification plan

- Build and typecheck the production bundle.
- Run real desktop mouse and mobile touch flows for all three Level 2 lessons.
- Verify Wave 3 Start remains disabled before the complete high-ground branch and becomes enabled afterward.
- Verify all mandatory grants preserve Gold, have zero invested/resale value, and do not increase paid-tower prices.
- Start Wave 3 and verify the taught high-ground segment registers real damage against a Layer 1 enemy.
- Verify Hỗ Trợ is introduced on Wave 4 and Xung Hồn on Wave 5.
- Run the complete Playwright suite, deterministic bot playtest, visual regression suite, desktop/mobile canvas inspection, production build preview, and public deployment smoke checks.

## Implementation result

Implemented and verified on 2026-08-17.

- Runtime planning selects a legal three-slot high-ground route with an internal segment no farther than `0.7` world units from the enemy path; the verified authored state crosses at distance `0`.
- Wave 3 grants and guides Giếng Hồn, Hỏa, and Băng before permitting the first flying wave. Hỗ Trợ and Xung Hồn now unlock and teach on Waves 4 and 5 respectively.
- Guided nodes preserve zero Gold cost, zero invested/resale value, and the pre-lesson paid-tower price multiplier.
- A high-ground/ground-chain separation fix prevents later Hỗ Trợ and Xung Hồn insertion cells from being selected on the raised branch.
- The complete Playwright matrix passed `51` tests with `7` intentional viewport skips. Real desktop and mobile flows placed and linked every grant, started Wave 3, and registered Layer 1 projectile hits.
- Public Intel D3D11 probes were nonblank and error-free at `58` desktop and `48` mobile draw calls.
- Production implementation deployment `dpl_ACLFVUw54J9m68RRPi9Ui7ZumiJA` reached `READY` at the stable Soul URL.
