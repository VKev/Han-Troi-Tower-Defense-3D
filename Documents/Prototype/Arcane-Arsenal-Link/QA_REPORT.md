# Arcane Arsenal Prototype QA

Date: 2026-08-16
Result: **Pass** for the scoped three-stage Concept 2 vertical slice.

## 2026-08-16 free lesson grants and Nổ feedback addendum

- The required Level 2 Amplifier, Nổ, and dedicated Nổ-feeder Foundry now cost exactly `0` only on the currently highlighted lesson cell. The card price and catalog price update to `0`; optional towers retain their normal prices and remain unaffordable from the retained `35`/`45` Arcana lesson bankrolls.
- Granted towers record zero invested Arcana, so their sale refund is `0`. Selling the granted Amplifier during its lesson reactivates the placement gate, preventing both resale profit and lesson bypass. The highlighted placement cell is cached for the whole lesson step so desktop/mobile UI movement cannot change the free target mid-gesture.
- Nổ feedback now fills the exact `2`-unit gameplay radius with a ground disc, four shock rings, a two-layer flash core, eighteen radial shards, and a separate burst on each hit enemy. Damage rules remain unchanged: one same-layer inside target was hit, while the outside-radius and other-layer targets received zero damage.
- Both Vite/TypeScript builds passed. The new lesson flow passed `4/4` Link and `2/2` Rotation desktop/mobile cases, including retained money, optional pricing, zero resale, and required replacement. The complete Link run produced `57` passes before exposing four stale balance assertions; all four were updated to the already accepted Level 2–3 curve and reran `4/4`. Rotation bot/campaign/onboarding/mastery/visual-regression coverage passed `30` cases with four intentional skips, and the two stale reaction checks reran `2/2` after alignment with the accepted curve.
- Real Intel D3D11 Nổ probes were nonblank with zero console/page errors. Desktop measured `109` draw calls, `10,079` triangles, `94` geometries, and `5` textures; mobile measured `94` calls, `9,071` triangles, `94` geometries, and `5` textures. Both remain within the standard renderer budgets and use full-detail effects without LOD. Production deployment `dpl_DyvZRfQnKKw2a4Cwj9DVYP7uTuiL` reached `READY` at [`https://arcane-arsenal-link-network.vercel.app`](https://arcane-arsenal-link-network.vercel.app); the alias and hashed `index-ChNhqQXb.js` / `index-Bs7485OH.css` assets returned HTTP 200.

## 2026-08-16 Level 2–3 strategic-pressure addendum

- The Link and Rotation variants now share the same Level 2 and Level 3 enemy rosters, HP multipliers, spawn windows, layer mix, reaction barriers, and resistance assignments; routing and the Link-only Nổ identity remain their intended mechanical differences.
- Level 2 now escalates across six waves from `10` enemies at `1.1×` HP to `61` at `6.2×` HP. Level 3 escalates across ten waves from `20` enemies at `1.3×` HP to `148` at `15.5×` HP. Counts, HP, spawn density, and the calculated threat score increase strictly from one wave to the next.
- Waves 1–2 remain ground-only. Flying enemies begin at Wave 3; later waves increasingly mix Layer 1 groups, reaction barriers, armor, and elemental resistances. This makes separate Fire + Ice, Ice + Earth, and Wind + Earth solutions materially useful instead of allowing one generic damage line to coast through the campaign.
- Level 3 gained two opposing Layer 1 firing bands so valid same-layer projectile routes can cross both air lanes. This preserves strategic counterplay as the air roster grows.
- Deterministic real-combat checks prove the intended difficulty boundary in both variants: the stale three-tower Level 2 circuit loses the final wave while a fourteen-tower upgraded reaction network wins; the stale four-tower Level 3 network leaks on Wave 6 while a twenty-four-tower six-branch reaction network clears it with `20/20` Nexus lives and destroys every active reaction barrier.
- Both TypeScript/Vite builds passed. The campaign balance suite passed `2/2` applicable desktop cases per variant with two intentional mobile skips; the exact Level 3 roster/terrain/preview check passed `2/2` on desktop and mobile per variant. Real Intel D3D11 canvas probes reported no console or page errors. The Link final-wave freeze rendered all `148` enemies at full detail with no LOD at `997` desktop draw calls / `987` geometries and `977` mobile draw calls / `984` geometries, inside the documented high-density override of `1000`.
- Production deployment `dpl_Eo1Lh6UGKMVBSLNa6vJ2RyoQLtLq` reached `READY` at [`https://arcane-arsenal-link-network.vercel.app`](https://arcane-arsenal-link-network.vercel.app). The stable alias and hashed `index-bSJL43Q-.js` / `index-CmmsL3ez.css` assets returned HTTP 200. Live desktop and mobile Stage 3 probes confirmed Link routing, the accepted ten-wave count and HP arrays, real Intel D3D11 rendering, nonblank canvases, and zero console/page errors.

## 2026-08-16 Nexus first-damage highlight addendum

- The first Nexus life loss now uses the same highlight-only discovery path as the first Arcana spend: `.metric.lives` receives the animated `discovery-target` state while the discovery cue/card remain hidden and empty.
- The first leak keeps its VFX, audio, and damage value but suppresses the transient combat toast as well as every tutorial card/modal; later leaks retain the ordinary toast, and gameplay never enters the paused phase for this cue.
- TypeScript/Vite build passed. The complete Link onboarding suite passed `10` tests with `2` intentional Rotation-only skips across desktop and mobile. Dedicated desktop/mobile screenshots were inspected and show only the compact Nexus HUD highlight with no discovery card.
- This correction is included in production deployment `dpl_DyvZRfQnKKw2a4Cwj9DVYP7uTuiL`.

## 2026-08-16 Mastery difficulty retune addendum

- Waves 4–6 keep enemy counts `[12, 16, 21]` but now use HP multipliers `[6, 10, 14]`, up from the previously deployed `[1.45, 1.85, 2.35]`. Waves 1–3, rewards, spawn timing, Nexus rules, and the checkpoint are unchanged.
- A new deterministic pressure state runs the real final wave twice. The unchanged four-tower Link tutorial network loses; adding one affordable Foundry → Ice → Fire branch at authored lane intersections increases the network from four to seven towers and wins.
- The complete mastery suite passed 6/6 across desktop and mobile, including curve/free-build assertions, baseline-loss versus expanded-win playtests, and three-leak checkpoint restoration. The shared Vietnamese/economy/curve diagnostic check also passed.
- Production deployment `dpl_7aJfzKH9NAjFVHmG9dQ8W9wT4UFa` reached `READY` at [`https://arcane-arsenal-link-network.vercel.app`](https://arcane-arsenal-link-network.vercel.app). The stable alias and hashed `index-C4TujDFd.js` / `index-CmmsL3ez.css` assets returned HTTP 200. Live `mastery-ready` desktop and mobile probes confirmed Link routing, Wave 4 ready, three Nexus lives, mastery counts `[12, 16, 21]`, HP multipliers `[6, 10, 14]`, a captured retry checkpoint, nonblank canvases, renderer budgets within limits, and zero console/page errors.

## 2026-08-16 Post-reaction mastery-wave addendum

- Stage 1 now contains six waves: the existing three guided Link lessons followed by three independent mastery waves with enemy counts `[12, 16, 21]` and HP multipliers `[1.45, 1.85, 2.35]`. Spawn density and total threat increase strictly across the mastery set.
- Guidance, forced cells, and start-wave gates end before Wave 4. Foundry, Fire, and Ice remain the available Stage 1 toolkit, with normal free placement, movement, upgrade, sale, and button-free relinking during Waves 4–6.
- Stage 1 has exactly three Nexus lives and every tutorial enemy costs one life when leaked. Clearing Wave 3 captures a clean network/economy checkpoint; a Wave 4–6 defeat restores Wave 4, three lives, the captured Arcana, towers, buffers, and exact links without replaying onboarding.
- Both new desktop/mobile mastery checks passed (4/4). The broader Link onboarding/gameplay matrix produced 43 passes with nine intentional skips; its only stale Wave-3 completion expectation was revised to Wave 4 ready and rerun 2/2. All ten deterministic visual baselines passed. TypeScript/Vite build passed.
- Production deployment `dpl_Bm5r3JeE8fYkSWPkS2YVdMVXwueF` reached `READY` at [`https://arcane-arsenal-link-network.vercel.app`](https://arcane-arsenal-link-network.vercel.app). The alias and hashed `index-BmpfbDS8.js` / `index-CmmsL3ez.css` assets returned HTTP 200. Live `mastery-ready` desktop and mobile probes confirmed Link routing, Wave 4 ready, three Nexus lives, mastery counts `[12, 16, 21]`, HP multipliers `[1.45, 1.85, 2.35]`, a captured retry checkpoint, nonblank canvases, render budgets within limits, and zero console/page errors.

## 2026-08-16 Button-free Link gesture release addendum

- Production deployment `dpl_3gg2EGX7fXHGM9Q9xzYUHqcYsxT7` reached `READY` at [`https://arcane-arsenal-link-network.vercel.app`](https://arcane-arsenal-link-network.vercel.app); the alias and hashed `index-CyI0mLua.js` / `index-CmmsL3ez.css` assets returned HTTP 200.
- The inspector Link button and `L` shortcut were removed. A normal tap selects a source; a second press-and-drag from that selected emitter enters transient Link mode, highlights only authoritative valid receivers, and links on release.
- Stage 1 and the Level 2 Nổ feeder lesson now animate the same source-to-receiver drag. Link range, layer, obstruction, reciprocal-link, and completed-relay rules remain unchanged, and every successful source still faces its receiver with measured facing error `0`.
- The mobile portrait inspector is narrower and docks opposite the selected tower. A deterministic viewport assertion and a live Chrome touch probe confirmed that the source center remains on the canvas and can start the gesture instead of being covered by UI.
- Final gates: Vite/TypeScript build and dependency audit passed; the complete visual/gameplay suite passed 31 cases with seven intentional viewport skips; supporting onboarding, bot, and visual baselines passed, with only the intentionally changed mobile tower-detail baseline inspected and regenerated. Live desktop mouse and mobile Chrome touch probes each created exactly one link with the correct highlighted candidate, zero facing error, and no console or page errors.

## 2026-08-16 Link Nổ release addendum

- Production deployment `dpl_C5cvZHqQF2f7G6geBTy2iaCuDuZ1` reached `READY` at [`https://arcane-arsenal-link-network.vercel.app`](https://arcane-arsenal-link-network.vercel.app); the alias and hashed `index-C8o8n0Wv.js` / `index-C5TAnp2c.css` assets returned HTTP 200.
- Nexus Lance was replaced player-facing and mechanically by Nổ Arcana: an automatic, finite-charge, same-layer radial skill with exact one-cell (`2` world-unit) center radius. Deterministic coverage recorded one inside hit with positive damage, zero outside-radius damage, zero other-layer damage, and one anchored radial VFX.
- Level 2 Wave 4 now suggests a legal visible ground placement whose Nổ radius covers the enemy lane, then requires a dedicated Foundry link and exposes the live world charge bar.
- A target with an existing input and output is now invalid and unhighlighted for every additional new incoming link. Desktop/mobile interaction tests preserved its two established links after the rejected attempt.
- Final gates: Vite/TypeScript build passed, `npm audit` found zero vulnerabilities, desktop visual interactions passed 18 with one intentional skip, mobile passed 13 with six intentional skips, visual baselines passed 10/10, onboarding parity passed 10 with three intentional skips, and the gameplay bot completed Wave 1 into Wave 2 with no runtime errors.

## Quality gates

- `npm run build`: pass with Vite 8.2.1; static assets use relative paths.
- Vercel production deployment `dpl_8qaLGCdEdwu6T19bY2cApZeeFeZu`: [`https://arcane-arsenal-link-network.vercel.app`](https://arcane-arsenal-link-network.vercel.app) returned HTTP 200; deployed `index-Cr1w04n9.js` and `index-CKKm-Ef6.css` also returned HTTP 200.
- Full Playwright matrix: 47 pass and 10 intentional cross-viewport/mode skips. A diagnostic publication race in the reciprocal-link assertion was stabilized; its focused desktop/mobile rerun passed 2/2.
- First-time onboarding: currency after the first tower purchase, elemental reaction after the first reaction, and Nexus life after the first leak each use a queued, non-blocking, text-free animated pictogram and pointer. All three pass on desktop and mobile.
- Cross-variant production diagnostics match exactly for projectile speed (`3×`), tower cadence (`1.5×`), projectile radius (`0.84`), projectile visual scale (`2×`), enemy speed (`0.6×`), and every Level 2/3 enemy-count, HP, and spawn-density value. The only source difference under `src/` is `routingMode.ts`.
- Guided Stage 1: ground-only terrain, the Foundry → Fire → Ice physical-interception circuit, live enemies, elemental infusion, progressive unlocks, a Fire + Ice reaction, and Stage 1 → Stage 2 transition are covered by deterministic browser assertions.
- Level 2: six waves, `220` starting Arcana, an authored `1.5×` stage reward factor before the global `0.6×` income reduction, two usable Layer 1 plateaus, flying enemies from Wave 3, Amplifier introduction before Wave 3, and Nổ introduction before Wave 4 are covered by deterministic browser assertions. The required Amplifier, Nổ, and feeder Foundry are free only on their indicated cells, preserve the `35`/`45` Arcana lesson balances, refund `0`, and leave optional purchases priced normally.
- Level 2 Nổ lesson: placing Nổ immediately introduces a required additional Foundry, verifies that it is explicitly linked into Nổ, and exercises the in-world camera-facing ammunition bar until it visibly fills.
- Level 3: a 20×14 logical board larger than Level 2's 12×9 board, a longer authored route, Ground plus Layer 1 combat, two raised firing plateaus, 10 strictly increasing wave-threat scores, and Stage 2 → Stage 3 progression are covered on desktop and mobile.
- Economy and difficulty curves are deterministic: enemy drops use a global `0.6×` multiplier, wave-clear rewards use `0.65×`, every stage's enemy count and HP multiplier increase strictly by wave, Level 2 reaches `61` enemies at `6.2×` HP, and Level 3 reaches `148` enemies at `15.5×` HP.
- Reaction scaling adds `6%` of the target's maximum HP to reaction damage. The current high-pressure scaling state exposes `15,190` active maximum HP, records at least `900` reaction bonus damage on its target, and proves that the named reaction removes its barrier.
- New Level 3 enemies: Hộ Vệ Thiên Lăng exposes a Layer 1 Sandstorm reaction barrier, while Cự Tượng Khe Nứt exposes a ground Crystal Shatter barrier, dual resistances/vulnerabilities, and 8 Nexus damage. Both preview/detail profiles and live runtime models are exercised.
- Combat tuning diagnostics assert `3×` projectile travel speed, `1.5×` tower fire/production rate, `2×` projectile visual scale, `2×` projectile collision radius, and `0.6×` enemy movement speed. Swept segment collision remains enabled at a fixed 60 Hz simulation step.
- Enemy formation diagnostics confirm nonuniform spawn gaps, lateral staggering, side-by-side movement, and intentional overlapping pairs rather than evenly spaced single-file rows.
- Gameplay bot: eight samples over 195 advanced frames observed hostiles, projectiles, linked launches, economy/Nexus progression, eight network connections, pause/resume, Wave 1 resolving into Wave 2 ready, and no console/page errors.
- Spawn communication: each stage owns one bright red, fog-independent 3D direction arrow aligned with the first enemy-path segment. Direction, viewport projection, desktop visibility, and Level 3 mobile visibility are covered.
- Nexus Lance VFX is authored in local space at the firing anchor and fades without whole-group scaling. Diagnostics held both maximum anchor error and scale error at `0` through the complete effect lifetime.
- Vietnamese localization and the exact upcoming-wave roster are covered on desktop and mobile, including the compact top-center layout, explicit ground/flying-height labels, hover inspection, click/tap pinning, and a viewport-bounded enemy detail layout.
- Seven independent tower eye controls are covered on desktop and mobile, including locked-tower inspection, catalog stats, no accidental build selection, no Arcana spending, and 44-pixel mobile touch targets.
- Deterministic visual harness: active-play, wave-intel, tower-detail, fail, and win states pass on desktop Chrome and mobile Chrome emulation.
- `npm audit`: 0 vulnerabilities, including dev tooling.
- Audio: gesture unlock, synthesized event SFX, pause suppression, mute, reset, and disposal are implemented without external files.

## Production canvas evidence

GPU: real Intel D3D11 through ANGLE; `softwareRendered: false`.

| Evidence | Desktop 1280×720 | Mobile 390×664 |
|---|---:|---:|
| Draw calls | 134 / 300 | 113 / 150 |
| Triangles | 15,122 / 750,000 | 13,870 / 300,000 |
| Geometries | 120 / 300 | 103 / 200 |
| Textures | 4 / 60 | 4 / 40 |
| DPR | 1.0 | 1.5 cap |
| Color entropy | 4.79 bits | 5.08 bits |
| Edge density | 0.266 | 0.260 |
| Luminance contrast | 143.0 | 149.3 |
| Dominant color share | 0.190 | 0.188 |

Technical-art tradeoffs: logical grid cells remain instanced but overlap slightly so the terrain reads as continuous; per-cell altitude runes and air-path guides were removed; infusion feedback uses one short-lived additive ring plus a label; one simplified shadow caster is used per authored object group; DPR is capped at 1.5; one 1024 shadow light is used; there is no post-processing chain.

## Visual scorecard

There is no equivalent pre-implementation Concept 2 screenshot, so before scores are `N/A`.

- Art direction: N/A → **3** — mechanical magic changes tower silhouettes, elemental cores, UI glyphs, materials, world props, and feedback rather than only recoloring the scene.
- Hero/player: N/A → **2** — the player's “hero” is the authored nine-node weapon network, with distinct tower construction, selection/range state, buffer state, aim state, and collision proxies.
- Obstacles/enemies: N/A → **2** — eight enemy forms, Ground/Layer 1 combat heights, defense cues, barrier state, hit response, and movement silhouettes are implemented.
- Rewards/interactables: N/A → **2** — seven buildable forms, grid highlighting, selectable nodes, buffer meters, upgrade/move/sell/branch states, and gold feedback communicate value.
- World/environment: N/A → **2** — a layered floating-island board, authored enemy lane, altitude platforms, blockers, pylons, crystals, rocks, and Nexus create foreground/midground/background scale cues.
- Materials/textures: N/A → **2** — shared named material roles, element emissives, glass, metal/roughness separation, runes, trim, lane panels, and four measured textures form a consistent low-poly material language.
- Lighting/render: N/A → **2** — ACES tone mapping, controlled exposure, environment lighting, key/fill/rim, contact shadows, sky gradient, depth fog, and capped DPR remain readable.
- VFX/motion: N/A → **2** — physical bullet trails, visible infusion rings and element labels, hit damage, spawn/destroy bursts, status/reaction feedback, network flow pulses, anchored Lance release, and red path-entry arrows are event-driven.
- UI/HUD: N/A → **3** — four explicit build categories, independent tower-detail controls, text-free tutorial cues, node inspector, buffer/backpressure state, explicit ground/flying wave intel, safe areas, responsive portrait focus behavior, touch targets, and result states form one hierarchy.
- Performance evidence: N/A → **3** — production build/browser QA, real-GPU diagnostics, desktop/mobile captures, before/after optimization decision, deterministic baselines, and explicit budgets are recorded.

Average: **2.3 / 3.0**. Every category is at least 2.

Automatic failures remaining: **none observed**. The mobile empty inspector is hidden so it does not cover the lane; selecting a node deliberately opens the focused inspector and Cancel dismisses it.

## Adversarial fresh-eyes review

Subagents were not used, so each category received the required strongest visible argument for a score of 1 before assigning the reconciled score above.

- Art direction could be 1 because the palette carries substantial identity and there is no external concept-art texture set.
- Hero/player could be 1 because every tower is still assembled from procedural low-poly geometry rather than a sculpted production asset.
- Obstacles/enemies could be 1 because most enemy motion is bob/scale/spinner motion and lacks full anticipation animation.
- Rewards/interactables could be 1 because there are no separate collectible reward objects; value is mainly expressed through gold and tower UI.
- World/environment could be 1 because the island still exposes a repeated grid and the distant background is intentionally sparse.
- Materials/textures could be 1 because the style relies on PBR parameters and emissive roles instead of authored wear, normal, and decal maps.
- Lighting/render could be 1 because there is no post-processing or dynamic cinematic lighting; it is disciplined functional lighting.
- VFX/motion could be 1 because a still active screenshot may show only trails and damage, not every elemental reaction or Lance burst.
- UI/HUD could be 1 because rectangular panels dominate the frame even though their hierarchy and game-specific states are authored.
- Performance evidence could be 1 because browser emulation is not a real-phone thermal soak or long-duration FPS capture.

The score remains 2–3 where implementation evidence, full state captures, and measured diagnostics overcome those objections. No showcase/AAA claim is made.

## Asset sourcing ledger

- `TRIPO_API_KEY=MISSING`
- `GEMINI_API_KEY=MISSING`
- `ELEVENLABS_API_KEY=MISSING`
- Towers, enemies, projectiles, blockers, Nexus, and world props: authored procedural Three.js geometry.
- UI icons and visual motifs: authored HTML/CSS glyph treatment.
- Audio: runtime Web Audio synthesis.
- No API key, temporary provider URL, or external binary asset is present in source or build output.
