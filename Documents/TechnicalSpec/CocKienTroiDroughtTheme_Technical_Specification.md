# Cóc Kiện Trời Drought Theme Technical Specification

Status: Approved  
Approval date: 2026-08-17  
Responsible session: `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`  
Target: `Documents/Prototype/Projectile-Network-TD/`

## 1. Approved Product Direction

The existing Projectile Network TD Soul prototype will become a stylized, low-detail Vietnamese folk-fantasy interpretation of **Cóc kiện trời** (The Toad Who Sued Heaven). The current directed Link interaction, projectile simulation, elemental order, economy, tutorial cadence, mobile controls, and tuned difficulty remain the gameplay authority.

The fixed path-end Nexus becomes a pale light-blue frog representing Cóc. The unique movable two-input terminal tower becomes **Trống Gọi Mưa** (Rain-Calling Drum). Existing Arcane-Arsenal Link and Rotation prototypes remain unchanged and keep their existing deployments.

## 2. Player-Facing Contract

### 2.1 Player promise

Build a chain of elemental projectile towers across a drought-stricken Vietnamese folk landscape, protect Cóc on the road to Heaven, and charge the Rain-Calling Drum to unleash a restoring storm against the forces of drought.

### 2.2 Target feeling

Readable, tactical, lively, and culturally distinct. The battlefield should feel dry and pressured without becoming visually muddy or dark. Elemental projectiles, links, reactions, hit feedback, and tutorial gestures remain the clearest moving signals.

### 2.3 Core loop

Place towers on the logical grid, drag directed links between valid towers, route projectiles across enemy paths, earn Gold, expand elemental chains, trigger reactions, charge the Rain-Calling Drum, drag the rain skill over enemy clusters, and survive increasingly dense waves before enemies reach Cóc.

### 2.4 Skill expression

A stronger player places links so projectile segments cross long portions of the route, orders elemental towers to produce useful reactions, chooses purchases under rising tower prices, positions Support and explosion towers deliberately, and targets the rain field at dense enemy clusters.

### 2.5 Failure and retry

Enemies reaching the path endpoint damage Cóc. Tutorial onboarding keeps three lives and preserves the existing post-reaction mastery checkpoint. Later stages preserve their current base-health and retry contracts.

## 3. Scope

### 3.1 Included

- Retheme all world, tower, enemy, UI, VFX, skill, result, and tutorial presentation from soul/dark fantasy to drought/Cóc kiện trời folk fantasy.
- Replace the fixed Nexus model with a readable pale light-blue frog.
- Replace the movable terminal presentation and terminology with Trống Gọi Mưa while preserving its two-input, no-output, zero-cost contract.
- Keep Fire, Ice, Wind, and Earth towers visually elemental and mechanically unchanged.
- Add stylized procedural cracked earth, dusty road, dry rocks, dead trees, and sparse withered grass.
- Replace soul-purple UI surfaces with drought parchment, terracotta, dry-gold, and rain-blue accents while preserving current responsive layout and safe-area behavior.
- Remove all flying enemies and all raised terrain/build slots from Levels 2 and 3.
- Replace former flying wave entries with ground archetypes of equivalent role and preserve counts, spawn timing, health multipliers, rewards, and density pressure.
- Change Level 2 staged lessons to free Support on Wave 3 and free Special/explosion on Wave 4; remove the anti-air lesson.
- Preserve desktop and mobile pan, zoom, bounded camera orbit, drag placement, drag linking, contextual link visibility, and x1/x2/x3 simulation speed.
- Update deterministic diagnostics, Playwright tests, visual baselines, documentation, AI collaboration log, and the separate Vercel deployment.

### 3.2 Non-goals

- No change to the Link interaction model.
- No change to projectile speed, radius, visual scale, tower cadence, enemy global speed, economy multipliers, reaction damage rules, or purchase-price growth unless required to restore verified parity after removing altitude.
- No new progression, save system, campaign map, voice-over, external asset dependency, or production Unity integration.
- No photorealism or high-detail imported models.
- No changes to `Documents/Prototype/Arcane-Arsenal-Link/` or `Documents/Prototype/Arcane-Arsenal/`.

## 4. Approved Terminology and Presentation

Vietnamese runtime copy remains authoritative.

| Current role | Approved drought presentation | Contract retained |
| --- | --- | --- |
| Fixed Nexus | Cóc, a pale light-blue frog | Receives leak damage; not placeable |
| Tỏa Hồn terminal | Trống Gọi Mưa | Unique, free, movable, two inputs, no output |
| Giếng Hồn generator | Lò Đạn | Neutral projectile source |
| Hỏa/Băng/Phong/Địa | Hỏa/Băng/Phong/Địa | Element rewrite and status behavior |
| Hỗ Trợ | Trụ Tiếp Sức | Charge-based buff/debuff support |
| Xung Hồn | Trụ Sấm | Buffered local AOE explosion |
| Soul meter | Mưa | Charge accumulated at Trống Gọi Mưa |
| Linh Vực | Mưa Rào | Draggable persistent AOE field |

Branch and reaction mechanics remain unchanged. Their Vietnamese labels may be rewritten to remove soul-specific wording while keeping exact numerical descriptions visible where they already exist.

Enemy identifiers remain stable in code for compatibility, but names, silhouettes, materials, and lore become ground-based drought or celestial adversaries. Former `wisp` and `skyWarder` data entries become Layer 0 ground enemies rather than being deleted, so wave composition and balance can remain comparable.

## 5. Level and Encounter Plan

### 5.1 Level 1 — tutorial

- Preserve the copied Link route, current suggested tower cells, six-wave onboarding/mastery cadence, three lives, and post-reaction checkpoint.
- Retheme only presentation and copy.
- Wave 1 teaches Trống Gọi Mưa, Lò Đạn, one link, and Start Wave.
- Wave 2 teaches Hỏa and rewiring.
- Wave 3 teaches Băng and the first elemental reaction.
- Waves 4–6 remain unguided mastery waves that require network expansion and reaction use.
- Mưa Rào retains the existing drag-to-cast tutorial when charged during a live wave.

### 5.2 Level 2 — six waves

- Preserve current route dimensions, starting Gold, health, wave counts, spawn windows, health multipliers, rewards, and price growth.
- Remove all high-ground platforms and high-tier slots.
- All enemies are Layer 0.
- Support stays locked until Wave 3. Wave 3 grants its required placement and links for zero cost, zero resale value, and no paid-tower price inflation.
- Special stays locked until Wave 4. Wave 4 grants its required placement and links under the same free-lesson rules.
- Wave Start remains blocked until the current mandatory lesson network is active.

### 5.3 Level 3 — ten waves

- Preserve current map size, route, starting Gold, health, wave counts, health multipliers, density, rewards, reaction barriers, resistance, immunity, vulnerability, and boss pressure.
- Remove all high-ground platforms and high-tier slots.
- Convert former Layer 1 enemies into distinct Layer 0 ground archetypes with comparable stats and counterplay.
- No altitude badge or anti-air requirement remains in the wave preview.

## 6. Architecture and Ownership

### 6.1 `src/game/definitions.ts`

- Owns renamed node/enemy/stage/wave copy.
- Sets every enemy layer to `0`.
- Replaces former flying wave roles with ground presentation without changing order counts or spawn timestamps.
- Exposes no active high-ground platforms; build-slot generation remains low-tier only.
- Preserves combat constants and numerical balance.

### 6.2 `src/assets/MaterialLibrary.ts`

- Owns a shared drought material kit: cracked clay, dusty path, pale path edge, weathered stone, dry wood, withered grass, bronze, rain-blue signal, shadow, link validity, and elemental materials.
- Keeps named shared materials and intentional roughness/emissive contrast.
- Disposes all generated textures and materials.

### 6.3 `src/assets/ArtFactory.ts`

- Owns the pale frog Nexus, Rain-Calling Drum terminal, elemental/support/special tower silhouettes, ground enemy family, rain field, and shared procedural prop models.
- Keeps gameplay collision and interaction ownership in `Game.ts`; generated meshes remain presentation only.
- Uses recognizable low-poly silhouettes at the active camera distance.

### 6.4 `src/game/Game.ts`

- Owns drought lighting/fog, cracked-ground and road composition, dry prop placement, fixed frog placement, lesson flow, HUD state binding, VFX events, and diagnostics.
- Removes active high-ground construction and anti-air lesson planning.
- Retains deterministic seeded randomness and test hooks.
- Rethemes Mưa Rào visuals to an obvious blue rain AOE with repeated enemy damage feedback.

### 6.5 `index.html` and `src/styles.css`

- Own Vietnamese UI terminology, theme color, accessible labels, responsive parchment/terracotta panels, rain-blue skill state, drought-gold focus state, and mobile safe-area layout.
- Preserve existing touch targets and text-free hand/tutorial interaction.

### 6.6 Tests and diagnostics

- Tests assert zero Layer 1 enemies and zero high-ground platforms/slots in every stage.
- Level 2 lesson tests assert Support at Wave 3 and Special at Wave 4, including free placement, zero resale, no price inflation, link completion, and desktop/mobile input.
- Tutorial, reaction, skill, camera, link, economy, bot, and visual tests remain active.
- Deterministic hooks may be renamed from obsolete altitude states, but compatibility aliases can remain only when they do not expose removed gameplay as active content.

## 7. Runtime-State and Compatibility Contracts

- `NodeType`, stable internal enemy kind identifiers, projectile payloads, reaction keys, saved tutorial checkpoint data, and link graph rules remain compatible.
- Regular processors keep unlimited forwarding capacity; only Special keeps its capacity-eight buffer.
- Trống Gọi Mưa consumes projectiles immediately and charges the Mưa meter.
- A node already owning both an input and output cannot accept another source; cycles and reciprocal links remain rejected.
- Contextual finished-link visibility, live source-to-pointer guide, connection ranges, obstructions, and grid placement remain unchanged.
- All active build slots use `tier: 'low'` and the ground firing layer.

## 8. Visual and Technical-Art Contract

### 8.1 Art direction

- Shapes: chunky rounded low-poly towers, drum/bronze motifs, squat frog silhouette, faceted rocks, forked dead trees, sparse triangular grass clumps, readable cracked soil.
- Palette: orange-yellow clay, ochre dust, dark brown dry wood, muted olive grass, pale blue frog/rain, bright elemental accents, red danger, green valid-link state.
- Lighting: hot sun key, warm fill, cool rain-blue practical accent, readable contact shadows, light dusty distance fog.
- UI/world motifs: rounded folk-painted borders, drum rings, rain-drop accents, cracked-earth line work, restrained panel count.
- VFX: dust on impact and spawn, rain-blue Mưa Rào disc/pulses, visible local thunder explosion, existing elemental trails/status/reaction feedback.

### 8.2 Asset sourcing

Credential probes on 2026-08-17 returned:

- `TRIPO_API_KEY=MISSING`
- `GEMINI_API_KEY=MISSING`
- `ELEVENLABS_API_KEY=MISSING`

All runtime assets therefore use original procedural Three.js geometry, canvas textures, CSS, and the existing runtime Web Audio system. No secret, temporary provider URL, or external licensed asset is added.

### 8.3 Render budget

| Metric | Desktop target | Mobile target |
| --- | ---: | ---: |
| Draw calls | <= 300 | <= 150 |
| Triangles | <= 750,000 | <= 300,000 |
| Geometries | <= 300 | <= 200 |
| Textures | <= 60 | <= 40 |
| Shadow-casting lights | <= 2 | 1 |
| DPR cap | 2 | 1.5–2 |
| Additional post passes | 0–1 | 0 |

Repeated rocks, grass, and crack marks should share geometry/materials or use instancing where the active count warrants it. No enemy LOD is introduced.

## 9. Verification Plan

1. Run TypeScript/Vite production build.
2. Run targeted Level 2 lesson and campaign-composition tests.
3. Run the complete Playwright suite on desktop and mobile projects.
4. Regenerate only intentionally changed visual baselines and compare them again.
5. Capture active preparation and combat states on desktop and mobile.
6. Verify nonblank canvas, color variance, console/page/network cleanliness, and renderer diagnostics.
7. Exercise real drag placement, real drag linking, Start Wave, reaction popup, Mưa Rào drag, leak highlight, failure/retry, stage selection, and x1/x2/x3 speed.
8. Run deterministic bot playtests for progression, pressure, restart, and softlock evidence.
9. Verify all stages report zero Layer 1 enemy counts, zero high-ground platforms, and zero high-tier slots.
10. Build production output, deploy the separate Vercel project, and verify root, all three level query routes, and hashed assets over HTTP.

## 10. Risks and Mitigations

- Removing altitude could reduce difficulty. Mitigation: preserve the old archetype stats and every wave order while changing only layer and presentation; compare threat/density diagnostics before and after.
- Warm drought colors could reduce Fire readability. Mitigation: reserve saturated red/orange emissive and trail treatment for Fire while ground stays matte ochre.
- Decorative cracks and props could obscure placement or links. Mitigation: keep props outside lanes and critical suggested cells; grid/range overlays remain dominant while dragging.
- A frog made from simple shapes could read as a generic blob. Mitigation: use a squat body, broad head, large eyes, visible folded legs, mouth line, toe forms, and pale blue material contrast.
- Full retheme could regress mobile UI. Mitigation: preserve layout geometry and interaction sizes; change material language and copy within existing responsive constraints.

## 11. Deferred Work

- Final production illustrations, externally generated models, recorded Vietnamese voice-over, and authored music.
- Unity production port and save/progression integration.
- Additional Vietnamese folktale chapters, bosses, and narrative cutscenes.
- Physical-device performance validation beyond browser emulation.

## 12. Implementation Result

Implemented and verified on 2026-08-17.

- The Three.js runtime now presents a warm, stylized drought battlefield with deterministic cracked-earth and dusty-road canvas textures, procedural dry rocks, dead trees, withered grass, hot-sun lighting, and rain-blue gameplay accents.
- The fixed path endpoint is a pale light-blue procedural frog. The free movable two-input terminal is the horizontal **Trống Gọi Mưa** drum. Fire, Ice, Wind, Earth, Support, and Special silhouettes remain mechanically readable.
- Every enemy archetype is Layer 0. Levels 2 and 3 report zero high-ground platforms, zero high-tier build slots, and zero Layer 1 wave entries while preserving the approved wave orders, counts, timing, health multipliers, resistance, immunity, reaction-barrier, density, and economy values.
- Level 2 now unlocks and teaches a free Hỗ Trợ insertion in Wave 3 and a free Trụ Sấm insertion in Wave 4 on desktop and mobile. Both grants retain zero invested value and do not inflate later tower prices.
- **Mưa Rào** retains drag targeting and persistent AOE damage, with a blue filled preview, repeated field pulses, enemy hit flashes, particles, and damage numbers.
- The production build succeeded. The complete Playwright matrix finished with `51 passed` and `7` intentional project-specific skips. Fourteen updated visual baselines passed on desktop and mobile.
- Deterministic canvas inspection reported no console or page errors. The largest measured state, Level 3 combat, used `164` desktop / `142` mobile draw calls, `15,802` / `13,184` triangles, `165` / `149` geometries, and `5` textures, all within the approved budgets.
- Vercel deployment `dpl_5ku8sRnKLgxQx9nYEEPhNWtHjS5S` reached `READY` and is aliased to [`https://projectile-network-td-soul.vercel.app`](https://projectile-network-td-soul.vercel.app). The root, `?level=1`, `?level=2`, `?level=3`, and hashed JS/CSS assets returned HTTP 200.
