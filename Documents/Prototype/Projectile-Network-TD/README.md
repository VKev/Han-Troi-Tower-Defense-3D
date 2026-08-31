# Projectile Network TD — Cóc Kiện Trời Prototype

This independent Three.js prototype retells **Cóc kiện trời** (The Toad Who Sued Heaven) as a stylized projectile-network tower-defense game. The runtime UI is Vietnamese. Players route elemental projectiles across a drought-stricken battlefield, protect a pale light-blue Cóc at the end of the road, and charge the **Trống Gọi Mưa** (Rain-Calling Drum) to ready **Cóc Bắt Mồi**.

The implementation lives entirely under `Documents/Prototype/Projectile-Network-TD/`. It does not modify the Arcane-Arsenal Link/Rotation prototypes or the Unity production project.

## Play

Production: [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app)

- Level 1: `?level=1`
- Level 2: `?level=2`
- Level 3: `?level=3`

### Local run

```powershell
npm install
npm run dev
```

Open the URL printed by Vite. A production-equivalent check is:

```powershell
npm run build
npm run preview -- --port 5188 --strictPort
```

The game must be served over HTTP. Opening `index.html` through `file://` will block the TypeScript module through browser CORS rules.

## Current design

### Core interaction

1. Drag towers from the build dock onto any valid logical grid cell.
2. Press and drag directly from a tower to a highlighted valid destination; no preselection click is required.
3. A live source-to-pointer guide previews the attempted link. Finished links use one translucent white style instead of inheriting tower colors and appear only while a tower in that network is selected.
4. Start the wave and let physical projectiles travel along the directed links. A segment that crosses the enemy route can hit multiple enemies.
5. Order Fire, Ice, Wind, and Earth processors to create useful elemental reactions.
6. Charge Trống Gọi Mưa, then drag from Cóc onto a live enemy cluster to preview and release the single-impact Cóc Bắt Mồi attack.

Regular towers have one input and one output. A regular tower that already owns both cannot accept another source. Reciprocal links, cycles, out-of-range links, and obstructed links are rejected. Trống Gọi Mưa is unique, free, accepts two independent inputs, has no output, consumes incoming projectiles immediately, and does not backpressure the network. Every complete directed route from a Lò Đạn to Trống Gọi Mưa transports ammunition; no hidden minimum processor count disables a shorter second route. Only the Special tower retains a capacity-eight projectile buffer.

### Theme and presentation

- Warm orange-yellow cracked clay, dusty roads, dry rocks, forked dead trees, sparse withered grass, hot-sun lighting, and light dusty fog.
- A pale light-blue procedural frog is the fixed path-end Nexus and receives leak damage.
- The movable terminal is a horizontal terracotta-and-bronze Rain-Calling Drum.
- Lò Đạn uses a mechanical foundry profile, while Hỏa, Băng, Phong, and Địa use wide-brazier, asymmetric-crystal, broad-rotor, and squat-monolith silhouettes that remain distinct on mobile.
- UI uses terracotta, parchment, dry gold, and rain blue while preserving the existing responsive desktop/mobile layout and safe-area behavior.
- Enemy status tint, elemental icon, projectile trail, impact flash, particles, damage numbers, reaction burst, Special pulse, and Cóc Bắt Mồi impact feedback remain explicit.
- Enemy models always use full-detail procedural geometry; no LOD system is active.

No external art-generation credential was available. Runtime art is original procedural Three.js geometry, deterministic CanvasTexture work, CSS, and the existing Web Audio synthesis.

## Towers

| Runtime name | Role | Core contract |
| --- | --- | --- |
| Lò Đạn | Projectile source | Emits neutral physical rounds |
| Hỏa | Element processor | Adds Fire and burn pressure |
| Băng | Element processor | Adds Ice and slow pressure |
| Phong | Element processor | Adds Wind and spread pressure |
| Địa | Element processor | Adds Earth and armor pressure |
| Cột Tiếp Sức | Support | Charge-based buff/debuff utility |
| Trụ Sấm | Special | Capacity-eight local AOE pulse tower |
| Trống Gọi Mưa | Unique terminal | Free, movable, two inputs, no output, charges Cóc Bắt Mồi |

The Lò Đạn upgrades are:

- **Dồn Dập:** one round every `0.68s`, `12` physical damage.
- **Trọng Đạn:** one round every `1.35s`, `26` physical damage.

## Elemental reactions

| Elements | Reaction | Tactical result |
| --- | --- | --- |
| Fire + Fire | Hỏa Ngục | Strong burning burst |
| Ice + Ice | Băng Phong | Deep-freeze burst |
| Wind + Wind | Cuồng Phong | Wind burst and spread pressure |
| Earth + Earth | Toái Địa | Earth shatter burst |
| Fire + Wind | Bão Lửa | Firestorm burst |
| Wind + Earth | Bão Cát | Sandstorm burst |
| Earth + Ice | Băng Địa | Permafrost burst |
| Ice + Fire | Bộc Hơi | Steam burst |

Enemies may be neutral, resistant, immune, vulnerable, armored, or protected by a reaction barrier. The later waves preserve the existing counterplay and pressure values from the balanced Link prototype.

Ordinary Wind ammunition now marks enemies without rewinding their path progress. Cuồng Phong applies one smaller displacement (`0.8` path units, or `0.35` for a boss). Each enemy has an independent `2.25s` cooldown for each reaction type: repeating the same reaction during that window is suppressed, while a different reaction can still trigger immediately.

## Tutorial and campaign

### Level 1 — Đường Lên Cửa Trời

- Wave 1: place Trống Gọi Mưa and Lò Đạn; plain camera-facing `CUỐI` and `ĐẦU` labels introduce the two network endpoints before the icon-only direct link drag.
- The labels persist through all six tutorial waves without rings, glyphs, sockets, or colored half-link overlays. Both turn green only while one complete directed route connects them and return to neutral when any middle segment breaks.
- Every validity color change triggers one `0.46s` attention pulse up to `1.34x` scale before settling to normal. Completing a route separately sends the approved gold/rain-blue LED packet along it exactly twice without brightening the white link beams.
- Wave 2: add Hỏa and rebuild the route through it.
- Wave 3: add Băng at world cell `(-1, -6)`, visually below Lò Đạn under the approved default camera. The `28.29°` screen-space turn at Hỏa keeps `Lò Đạn → Hỏa` and `Hỏa → Băng` from reading as one doubled-back line, then the wave triggers the first elemental reaction and its paused visual explanation.
- Waves 4–6: three unguided mastery waves. The unchanged tutorial chain loses the final wave; an expanded reaction network using the learned Cóc skill wins deterministically.
- The mastery section restores three Cóc lives and `340` Gold. Failure restarts from the post-reaction checkpoint instead of replaying onboarding.
- The first charged live-wave skill shows an icon-only drag gesture from Cóc to a real enemy cluster.
- The elemental-reaction explanation pauses only for the first acknowledged reaction in the active browser session; reset and checkpoint restore do not reopen it.
- The first paid purchase highlights Gold without a modal. The first leak highlights Cóc health without a modal.

### Level 2 — Đồng Nứt Khô Hạn

- Six waves, `220` starting Gold, `20` Cóc health.
- All terrain and enemies are ground-level; there is no flying or high-ground system.
- Hỗ Trợ is locked until Wave 3. Its required placement and two links are free, have zero resale value, and do not raise the paid-tower price multiplier.
- Trụ Sấm is locked until Wave 4 and uses the same free-lesson rules.
- Wave Start stays blocked until the active lesson network is complete.

### Level 3 — Sân Trời Cuối Hạn

- Ten waves on the largest ground-only map, `220` starting Gold, and `20` Cóc health.
- Keeps the tuned Link-derived counts, spawn windows, health multipliers, density, reward pressure, resistance, immunity, vulnerabilities, reaction barriers, and boss escalation.
- Former air identifiers remain stable internally for compatibility but are presented and simulated as distinct Layer 0 ground enemies.

## Balance authority

The drought retheme preserves the verified Link prototype contract:

- Projectile speed `27.6`, collision radius `0.84`, visual scale `2`.
- Tower fire-rate multiplier `1.5`.
- Global enemy speed multiplier `0.6`.
- Enemy reward multiplier `0.6`; wave-clear reward multiplier `0.65`.
- Paid tower price grows by `0.12` per tower up to the existing cap; free lesson towers and Trống Gọi Mưa do not inflate it.
- Regular processors forward continuously without a projectile slot cap.
- Levels 2 and 3 preserve wave order counts, timestamps, health multipliers, and threat composition while converting all enemy layers to `0`.

## Controls

### Desktop

- Drag a card to place a tower.
- Press and drag directly from a tower to a valid target to link; a short click still selects it for inspection.
- Left-drag empty ground to pan.
- Right-drag to orbit the bounded camera.
- Mouse wheel to zoom.
- Use `x1`, `x2`, or `x3` for simulation speed.

### Mobile

- Touch-drag cards to place.
- Touch-drag directly from a tower to a highlighted target; a short tap still selects it for inspection.
- One-finger drag empty ground to pan.
- Two-finger gesture to orbit and zoom.
- All essential actions work without hover.

## Verification

```powershell
npm run build
npm test
npm run verify:visual
```

Verified on 2026-08-17:

- Production TypeScript/Vite build passed.
- Full Playwright matrix: `92 passed`, `10` intentional project-specific viewport skips.
- Twenty-six desktop/mobile visual baselines passed, including the distinct tower silhouettes, separated Băng lesson route, source/terminal tutorial labels, packed-earth trail, frog-tongue impact, victory hop, and drought perimeter dressing.
- Deterministic canvas inspection reported no console or page errors.
- The tutorial hand's first visible cold-load frame now points directly at the stable authored cell; no artificial loading delay is needed.
- Lò Đạn, Hỏa, Băng, Phong, and Địa now expose distinct procedural model profiles and broad silhouettes while preserving their footprints and combat data.
- The enemy route keeps its exact gameplay coordinates but renders as a raised three-layer packed-earth trail with ruts, footprints, pebbles, and broken edges.
- Cóc Bắt Mồi uses a non-glowing solid tongue, rounded tip, subtle AOE preview, soil impact particles, and captured-kill retraction.
- After a level victory, the frog hops backward over the exact enemy route to the spawn entrance; Levels 1 and 2 then transition automatically to the next level.
- The dark terrain surrounding every level now carries deterministic dry-grass clusters, faceted rocks, forked twigs, and dead trees. Static props inside the battlefield and around its perimeter are each merged to one vertex-colored mesh per level.
- The LED-only completed-chain state stays inside both budgets at `87` draw calls, `14,320` triangles, `90` geometries, and `7` textures. Hardware-backed Intel D3D11 probes were nonblank and reported no console or page errors.
- Completed links in all three levels report actual white transparent beam materials and selected-network-only visibility. Deterministic desktop/mobile coverage verifies direct first-gesture linking, short-tap inspection, camera gestures, the two-pass LED notification, full-route endpoint color, and the completion/breakage pulse.
- Regression coverage proves that ordinary Wind cannot rewind or stall enemies, duplicate reactions are rejected during the `2.25s` per-enemy cooldown, different reactions remain independent, and the same reaction becomes available after the timer expires.
- Level 1 now opens at yaw `1.9712`, pitch `0.7844`, distance `32.9841`, aimed at `[-1, 1.2, -0.4]`, matching the approved high-oblique composition while retaining player orbit and zoom.
- Two independent complete routes into Trống Gọi Mưa are regression-tested; both Lò Đạn sources launch without a hidden processor-count gate.
- Level 1–3 report zero high-ground platforms, zero high-tier slots, and zero Layer 1 wave entries.
- Live production projection places Băng `116.49px` below and `28.63px` horizontally from Lò Đạn on desktop, and `102.41px` below with `25.17px` horizontal offset on mobile. Both report one valid active chain, three directed links, and no console or page errors.

Evidence is stored under `artifacts/coc-kien-troi-drought/`, `artifacts/perimeter-decoration-2026-08-17/`, `artifacts/tower-readability-2026-08-17/`, `artifacts/deploy-2026-08-17-led-only/`, `artifacts/deploy-2026-08-17-direct-link-pulse/`, `artifacts/deploy-2026-08-17-ice-below/`, and in `QA_REPORT.md`.

## Deployment

Vercel project: `projectile-network-td-soul`  
Stable production URL: [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app)  
Deployment: `dpl_9ngSoLM3MKxtaZrWtiQQjgQL21DF` (`READY`)

The root, all three level query routes, `assets/index-B779jQaT.js`, and `assets/index-BZlIPGM6.css` returned HTTP 200 after deployment. Live desktop/mobile probes confirmed the separated Băng composition, one valid active chain, green source/terminal labels, and no console or page errors. Renderer diagnostics remained within budget at `94` calls / `12,396` triangles on desktop and `91` calls / `12,256` triangles on mobile.
