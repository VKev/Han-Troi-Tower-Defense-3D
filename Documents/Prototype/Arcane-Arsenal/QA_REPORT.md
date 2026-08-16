# Arcane Arsenal Prototype QA

Date: 2026-08-16
Result: **Pass** for the scoped three-stage Concept 2 vertical slice.

## 2026-08-16 free lesson grants addendum

- The required Level 2 Amplifier, Nexus Lance, and dedicated Lance-feeder Foundry now cost exactly `0` only on the currently highlighted lesson cell. The card price and catalog price update to `0`; optional towers keep the Rotation variant's tower-count-scaled prices and remain unaffordable from the retained `35`/`45` Arcana lesson bankrolls.
- Granted towers record zero invested Arcana, so their sale refund is `0`. Selling the granted Amplifier during its lesson reactivates the placement gate, preventing resale profit and lesson bypass. The highlighted placement cell is cached for the whole lesson step so desktop/mobile UI movement cannot change the free target mid-gesture.
- Both Vite/TypeScript builds passed. The free Amplifier/Lance/feeder flow passed `2/2` across desktop and mobile. Rotation bot, campaign balance, onboarding, mastery, and visual regression passed `30` cases with four intentional skips; the current enemy preview and reaction-scaling assertions then passed focused desktop/mobile reruns. Production deployment `dpl_CtZEuqgN3TTxoaRPh9Pe3oQof8Aq` reached `READY` at [`https://arcane-arsenal-tower-defense.vercel.app`](https://arcane-arsenal-tower-defense.vercel.app); the alias and hashed `index-Ckmc2jXz.js` / `index-ZWjKVt1v.css` assets returned HTTP 200.

## 2026-08-16 Level 2–3 strategic-pressure addendum

- The Rotation and Link variants now share the same Level 2 and Level 3 enemy rosters, HP multipliers, spawn windows, layer mix, reaction barriers, and resistance assignments; aiming versus explicit routing and the variant-specific special tower remain their intended mechanical differences.
- Level 2 now escalates across six waves from `10` enemies at `1.1×` HP to `61` at `6.2×` HP. Level 3 escalates across ten waves from `20` enemies at `1.3×` HP to `148` at `15.5×` HP. Counts, HP, spawn density, and the calculated threat score increase strictly from one wave to the next.
- Waves 1–2 remain ground-only. Flying enemies begin at Wave 3; later waves increasingly mix Layer 1 groups, reaction barriers, armor, and elemental resistances. Separate Fire + Ice, Ice + Earth, and Wind + Earth firing solutions are therefore required by composition rather than raw HP alone.
- Level 3 gained two opposing Layer 1 firing bands so same-layer shots can cross both air lanes. This keeps the harder flying formations answerable in the Rotation layout as well as the Link layout.
- Deterministic real-combat checks prove the intended difficulty boundary in both variants: the stale three-tower Level 2 circuit loses the final wave while a fourteen-tower upgraded reaction network wins; the stale four-tower Level 3 network leaks on Wave 6 while a twenty-four-tower six-branch reaction network clears it with `20/20` Nexus lives and destroys every active reaction barrier.
- Both TypeScript/Vite builds passed. The campaign balance suite passed `2/2` applicable desktop cases per variant with two intentional mobile skips; the exact Level 3 roster/terrain/preview check passed `2/2` on desktop and mobile per variant. Real Intel D3D11 canvas probes reported no console or page errors, and every visible enemy remained full detail with no LOD. The sampled Rotation Level 3 scenes remained below the documented high-density override.
- Production deployment `dpl_GPqm5SaErTNo59wiCqTB2SFWygLL` reached `READY` at [`https://arcane-arsenal-tower-defense.vercel.app`](https://arcane-arsenal-tower-defense.vercel.app). The stable alias and hashed `index-BtuZBgX4.js` / `index-Dm8guICf.css` assets returned HTTP 200. Live desktop and mobile Stage 3 probes confirmed Rotation routing, the accepted ten-wave count and HP arrays, real Intel D3D11 rendering, nonblank canvases, and zero console/page errors.

## 2026-08-16 Nexus first-damage highlight addendum

- The first Nexus life loss now uses the same highlight-only discovery path as the first Arcana spend: `.metric.lives` receives the animated `discovery-target` state while the discovery cue/card remain hidden and empty.
- The first leak keeps its VFX, audio, and damage value but suppresses the transient combat toast as well as every tutorial card/modal; later leaks retain the ordinary toast, and gameplay never enters the paused phase for this cue.
- TypeScript/Vite build passed. The complete Rotation onboarding suite passed `11` tests with `1` intentional desktop-only control skip across desktop and mobile. Dedicated desktop/mobile screenshots were inspected and show only the compact Nexus HUD highlight with no discovery card.
- This correction is included in production deployment `dpl_CtZEuqgN3TTxoaRPh9Pe3oQof8Aq`.

## 2026-08-16 Mastery difficulty retune addendum

- Waves 4–6 keep enemy counts `[12, 16, 21]` but now use HP multipliers `[6, 10, 14]`, up from the previously deployed `[1.45, 1.85, 2.35]`. Waves 1–3, rewards, spawn timing, Nexus rules, and the checkpoint are unchanged.
- A new deterministic pressure state runs the real final wave twice. The unchanged three-tower Rotation tutorial network loses; adding one affordable Foundry → Ice → Fire branch at authored lane intersections increases the network from three to six towers and wins.
- The complete mastery suite passed 6/6 across desktop and mobile, including curve/free-build assertions, baseline-loss versus expanded-win playtests, and three-leak checkpoint restoration. The shared Vietnamese/economy/curve diagnostic check also passed.
- Production deployment `dpl_Eqb3NTFLarXQBX4rcSVMQ41iChUB` reached `READY` at [`https://arcane-arsenal-tower-defense.vercel.app`](https://arcane-arsenal-tower-defense.vercel.app). The stable alias and hashed `index-AOsJCpot.js` / `index-Dm8guICf.css` assets returned HTTP 200. Live `mastery-ready` desktop and mobile probes confirmed Rotation routing, Wave 4 ready, three Nexus lives, mastery counts `[12, 16, 21]`, HP multipliers `[6, 10, 14]`, a captured retry checkpoint, nonblank canvases, renderer budgets within limits, and zero console/page errors.

## 2026-08-16 Post-reaction mastery-wave addendum

- Stage 1 now contains six waves: the existing three guided Rotation lessons followed by three independent mastery waves with enemy counts `[12, 16, 21]` and HP multipliers `[1.45, 1.85, 2.35]`. Spawn density and total threat increase strictly across the mastery set.
- Guidance, forced cells, and rotation locks end before Wave 4. Foundry, Fire, and Ice remain the available Stage 1 toolkit, with normal free placement, movement, upgrade, sale, and continuous rotation during Waves 4–6.
- Stage 1 has exactly three Nexus lives and every tutorial enemy costs one life when leaked. Clearing Wave 3 captures a clean network/economy checkpoint; a Wave 4–6 defeat restores Wave 4, three lives, the captured Arcana, towers, buffers, and exact aim angles without replaying onboarding.
- The complete Rotation onboarding/gameplay matrix passed 40 checks with 12 intentional viewport/mode skips, including the full causal three-wave tutorial on desktop and mobile. New mastery checks passed 4/4. Nine unchanged visual baselines passed; the intentional Stage 1 lives/wave-count change in the mobile tower-detail baseline was inspected, regenerated, and rerun successfully. TypeScript/Vite build passed.
- Production deployment `dpl_8AURegixvqSz6yhBTEmikhCN9KMS` reached `READY` at [`https://arcane-arsenal-tower-defense.vercel.app`](https://arcane-arsenal-tower-defense.vercel.app). The alias and hashed `index-CHffP_-f.js` / `index-Dm8guICf.css` assets returned HTTP 200. Live `mastery-ready` desktop and mobile probes confirmed Rotation routing, Wave 4 ready, three Nexus lives, mastery counts `[12, 16, 21]`, HP multipliers `[1.45, 1.85, 2.35]`, a captured retry checkpoint, nonblank canvases, render budgets within limits, and zero console/page errors.

## Quality gates

- `npm run build`: pass with Vite 8.2.1; static assets use relative paths.
- Vercel production deployment `dpl_8P2KUZBgjNtaQngmP69zyMmPpAia`: [`https://arcane-arsenal-tower-defense.vercel.app`](https://arcane-arsenal-tower-defense.vercel.app) returned HTTP 200; deployed `index-DzInudN9.js` and `index-Ck7w5jSM.css` also returned HTTP 200.
- Full Playwright matrix: 47 pass and 13 intentional cross-viewport/mode skips. The gameplay bot advanced 237 frames, completed Wave 1 into Wave 2 ready, and reported no runtime, console, page, or HTTP errors.
- First-time onboarding: currency after the first tower purchase, elemental reaction after the first reaction, and Nexus life after the first leak each use a queued, non-blocking, text-free animated pictogram and pointer. The Arcana card is explicitly placed below the HUD metric and remains viewport-bounded. All three pass on desktop and mobile.
- Cross-variant production diagnostics match exactly for projectile speed (`3×`), tower cadence (`1.5×`), projectile radius (`0.84`), projectile visual scale (`2×`), enemy speed (`0.6×`), and every Level 2/3 enemy-count, HP, and spawn-density value. The only source difference under `src/` is `routingMode.ts`.
- Guided Stage 1: the player places Foundry and starts Wave 1 before Fire unlocks; a surviving neutral hit proves why infusion is needed, then combat holds while Foundry is rotated into Fire and Fire toward the lane. Ice unlocks afterward, extends the physical-interception circuit to Foundry → Fire → Ice, and the first live Fire + Ice reaction introduces elemental reactions. The full causal sequence passes on desktop and mobile.
- Level 2: six waves, `160` starting Arcana, two usable Layer 1 plateaus, flying enemies, Amplifier introduction before Wave 3, and Nexus Lance introduction before Wave 4 are covered by deterministic browser assertions. The required Amplifier, Nexus Lance, and feeder Foundry are free only on their indicated cells, preserve the `35`/`45` Arcana lesson balances, refund `0`, and leave optional purchases at their scaled prices.
- Level 3: a 20×14 logical board, Ground plus Layer 1 combat, two raised firing plateaus, 10 waves, and the Sky Warder/Rift Colossus reaction barriers match the link build.
- Economy and difficulty curves are deterministic: enemy drops use `0.6×`, wave-clear rewards use `0.65×`, Level 2 reaches `61` enemies at `6.2×` HP, and Level 3 reaches `148` enemies at `15.5×` HP.
- Combat tuning diagnostics assert `3×` projectile travel, `1.5×` tower cadence, `2×` projectile visuals, `2×` projectile hit radius, and `0.6×` enemy movement. Enemy formation diagnostics confirm the same nonuniform gaps, lateral staggering, overlap, and wave density as the link build.
- Gameplay bot: eight samples over 239 advanced frames observed hostiles and projectiles, economy/Nexus progression, eight physical aim connections, pause/resume, Wave 1 resolving into Wave 2 ready with 19 lives and 763 Arcana, and no console/page errors.
- Vietnamese localization and the exact upcoming-wave roster are covered on desktop and mobile, including the compact top-center layout, explicit ground/flying-height labels, hover inspection, click/tap pinning, and a viewport-bounded enemy detail layout.
- Seven independent tower eye controls are covered on desktop and mobile, including locked-tower inspection, catalog stats, no accidental build selection, no Arcana spending, and 44-pixel mobile touch targets.
- Deterministic visual harness: active-play, wave-intel, tower-detail, fail, and win states pass on desktop Chrome and mobile Chrome emulation.
- `npm audit`: 0 vulnerabilities, including dev tooling.
- Audio: gesture unlock, synthesized event SFX, pause suppression, mute, reset, and disposal are implemented without external files.

## Production canvas evidence

GPU: real Intel D3D11 through ANGLE; `softwareRendered: false`.

| Evidence | Desktop 1280×720 | Mobile 390×664 |
|---|---:|---:|
| Draw calls | 139 / 300 | 120 / 150 |
| Triangles | 15,662 / 750,000 | 14,446 / 300,000 |
| Geometries | 114 / 300 | 99 / 200 |
| Textures | 7 / 60 | 7 / 40 |
| DPR | 1.0 | 1.5 cap |
| Color entropy | 4.74 bits | 5.04 bits |
| Edge density | 0.258 | 0.272 |
| Luminance contrast | 142.8 | 149.5 |
| Dominant color share | 0.195 | 0.190 |

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
- VFX/motion: N/A → **2** — physical bullet trails, visible infusion rings and element labels, hit damage, spawn/destroy bursts, status/reaction feedback, network flow pulses, and the special AOE release are event-driven.
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
