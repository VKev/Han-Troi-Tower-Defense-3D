# Arcane Arsenal Prototype QA

Date: 2026-08-15
Result: **Pass** for the scoped two-stage Concept 2 vertical slice.

## Quality gates

- `npm run build`: pass with Vite 8.2.1; static assets use relative paths.
- Vercel production: [`https://arcane-arsenal-tower-defense.vercel.app`](https://arcane-arsenal-tower-defense.vercel.app) returned HTTP 200; the deployed hashed JS and CSS bundles also returned HTTP 200 and matched the final local production build.
- `npm test`: 30 pass, 8 intentional cross-viewport skips. No failed tests.
- Guided Stage 1: ground-only terrain, the Foundry → Fire → Ice physical-interception circuit, live enemies, elemental infusion, progressive unlocks, a Fire + Ice reaction, and Stage 1 → Stage 2 transition are covered by deterministic browser assertions.
- Level 2: six waves, `160` starting Arcana, `1.5×` kill rewards, two usable Layer 1 plateaus, flying enemies, Amplifier introduction before Wave 3, and Nexus Lance introduction before Wave 4 are covered by deterministic browser assertions. Required lesson purchases were explicitly tested from `35` and `45` Arcana respectively and consumed the remaining balance to exactly zero.
- Gameplay bot: eight samples over 285 advanced frames observed hostiles and projectiles, economy/Nexus state progressed, five physical network connections were active, pause/resume worked, Wave 1 resolved into the Wave 2 ready state with 16 lives and 758 Arcana, Wave 2 started, and no console/page errors occurred.
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
- Obstacles/enemies: N/A → **2** — six enemy forms, three altitudes, defense cues, barrier state, hit response, and movement silhouettes are implemented.
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
