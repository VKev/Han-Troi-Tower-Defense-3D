# Projectile Network TD — Cóc Kiện Trời QA Report

Date: 2026-08-17  
Responsible session: `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`  
Tracking issue: `TowerDefense3D-rjb`  
Target: `Documents/Prototype/Projectile-Network-TD/`

## Outcome

Pass. The prior Soul prototype is now a complete stylized drought/Cóc kiện trời presentation while retaining the directed Link gameplay and verified combat/economy contract. The fixed endpoint is a pale light-blue frog, the unique terminal is Trống Gọi Mưa, Mưa Rào has explicit blue persistent AOE feedback, and Levels 2/3 contain no flying enemies or raised terrain.

Production: [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app)

## Acceptance matrix

| Requirement | Result | Evidence |
| --- | --- | --- |
| Drought/Cóc kiện trời world | Pass | Cracked clay, dusty road, rocks, dead trees, withered grass, hot-sun lighting, warm fog |
| Pale light-blue Cóc Nexus | Pass | Procedural frog at the fixed route endpoint; diagnostic `fixedNexus.kind = frog` |
| Trống Gọi Mưa terminal | Pass | Free movable horizontal drum, two inputs, no output, no resale, immediate consumption |
| Elemental tower identity | Pass | Fire, Ice, Wind, Earth colors, trails, icons, status tint, reactions, and hit feedback retained |
| Mưa Rào skill | Pass | Drag preview, filled radius, initial AOE, repeated `0.82s` ticks, pulses, particles, flash, damage numbers |
| Ground-only Levels 2/3 | Pass | Zero high-ground platforms, zero high-tier slots, every enemy layer `0`, no altitude badge |
| Level 2 lessons | Pass | Free Hỗ Trợ Wave 3; free Trụ Sấm Wave 4; real desktop/mobile placement, linking, and Start gating |
| Link/balance parity | Pass | Projectile, cadence, enemy speed, HP, density, reward, resistance, barrier, and price-growth contracts unchanged |
| Responsive interaction | Pass | Desktop and mobile placement, linking, camera pan/zoom/orbit, skill drag, and x1/x2/x3 controls tested |
| No enemy LOD | Pass | Full-detail procedural enemies remain active at every distance |
| Deployment | Pass | Vercel deployment `dpl_5ku8sRnKLgxQx9nYEEPhNWtHjS5S` is `READY` |

## Gameplay verification

### Tutorial

- The copied Link route and suggested cells remain unchanged.
- Wave 1 teaches Trống Gọi Mưa, Lò Đạn, one directed link, and immediate practice.
- Wave 2 teaches Hỏa rewiring.
- Wave 3 teaches Băng and the first elemental reaction. The explanation pauses the game and resumes after dismissal.
- Waves 4–6 are unguided mastery waves. The unchanged four-tower chain loses the final wave; a nine-tower expanded reaction network wins deterministically.
- The mastery checkpoint restores `340` Gold and three Cóc lives after the first reaction lesson.
- First purchase and first leak use highlight-only onboarding with no modal.
- Mưa Rào waits for a full meter, a running wave, and a living enemy before showing its drag hand.

### Level 2

- Six ground-only waves, `220` starting Gold, `20` health.
- Wave 3 enables and teaches Hỗ Trợ. The instructed tower is free, zero-investment, zero-resale, and does not change the paid-tower price multiplier.
- Wave 4 enables and teaches Trụ Sấm under the same rules.
- Each lesson selects a visible legal ground cell, guides placement and two links, and blocks Start until the inserted chain is active.

### Level 3

- Ten ground-only waves on the largest map.
- Existing wave order counts, spawn timing, health multipliers, density, reward pressure, resistance, immunity, vulnerabilities, reaction barriers, and boss escalation remain intact.
- Internal `wisp` and `skyWarder` identifiers remain for compatibility, but both are Layer 0 and use new ground silhouettes.

## Balance evidence

| Contract | Verified value |
| --- | ---: |
| Projectile speed | `27.6` |
| Projectile collision radius | `0.84` |
| Projectile visual scale | `2` |
| Tower fire-rate multiplier | `1.5` |
| Enemy speed multiplier | `0.6` |
| Enemy reward multiplier | `0.6` |
| Wave-clear reward multiplier | `0.65` |
| Paid-tower price growth | `0.12` per paid tower |
| Tutorial mastery Gold | `340` |
| Tutorial health | `3` |
| Level 2/3 starting Gold | `220` |
| Level 2/3 starting health | `20` |

Tutorial wave counts remain `[4, 6, 7, 12, 16, 21]` with health multipliers `[1, 1.08, 1.18, 6, 10, 14]`. The final three waves retain spawn densities `[2.252, 2.943, 3.831]` enemies per second and total base-health threats `[4212, 11200, 22288]`.

## Automated verification

Commands:

```powershell
npm run build
npm test
npx playwright test tests/visual.spec.ts --update-snapshots
npm run inspect:canvas -- --state preparation --seed 20260817 --out artifacts/coc-kien-troi-drought/level1
npm run inspect:canvas -- --url http://127.0.0.1:5188/?level=2 --state combat --seed 20260817 --out artifacts/coc-kien-troi-drought/level2
npm run inspect:canvas -- --url http://127.0.0.1:5188/?level=3 --state combat --seed 20260817 --out artifacts/coc-kien-troi-drought/level3
```

Results:

- TypeScript/Vite production build passed.
- Full Playwright matrix: `67 passed`, `9` intentional project-specific viewport skips.
- Visual regression suite: `20 passed` after inspecting and intentionally updating the affected drought-world baselines.
- Canvas inspection: every captured state was nonblank, with zero console errors and zero page errors.
- HTTP verification: root, `?level=1`, `?level=2`, `?level=3`, hashed JavaScript, and hashed CSS returned `200`.

## World-polish and transition evidence

- The first visible cold-load tutorial-hand frame already targets the stable authored cell on desktop and mobile; the synchronous camera-matrix race is fixed without a loading delay.
- Fire, Ice, Wind, and Earth report the distinct variants `terracotta-brazier`, `crystal-shrine`, `wind-pinwheel`, and `stepped-monolith`.
- The unchanged Level 1 path still has segment lengths `[8, 4, 10]` while its visual is now a raised three-layer trail with `16` deterministic edge clods.
- Mưa Rào uses `32` instanced falling drops, three ground ripples, and no opaque beam; repeated AOE damage ticks still execute.
- The victory frog reaches a measured hop height of `0.50`, follows the enemy path in reverse, transitions Level 1 to 2 and Level 2 to 3, and shows the final result only after the Level 3 journey.
- Dedicated desktop/mobile victory captures used `38/31` draw calls, `3,524/3,320` triangles, `43/41` geometries, and `5/5` textures, all inside budget.

## Drought perimeter decoration and batching

- Level 1 adds `34` perimeter rocks, `42` grass patches (`168` blades), `24` forked twigs, and `9` dead trees.
- Level 2 scales to `48` rocks, `60` grass patches (`240` blades), `34` twigs, and `13` dead trees.
- Level 3 scales to `62` rocks, `78` grass patches (`312` blades), `44` twigs, and `17` dead trees.
- Decoration remains outside route/tutorial clearance. Prop placement is deterministic and uses the same density on desktop and mobile; no LOD or viewport-specific hiding was introduced.
- All static props inside a battlefield are merged to one vertex-colored mesh, and the entire outer perimeter kit is merged to one more. Thus hundreds of source pieces cost two world-decoration draw calls per level.
- The first unmerged Level 3 mobile measurement reached `154` draw calls and failed the `150` target. Full static batching reduced the verified production state to `133` draw calls while retaining the authored density.

## Render evidence

| State | Draw calls | Triangles | Geometries | Textures | Budget result |
| --- | ---: | ---: | ---: | ---: | --- |
| Level 1 preparation desktop | `79` | `13,082` | `80` | `5` | Pass |
| Level 1 preparation mobile | `79` | `13,082` | `80` | `5` | Pass |
| Level 2 combat desktop | `111` | `19,892` | `112` | `5` | Pass |
| Level 2 combat mobile | `93` | `17,436` | `98` | `5` | Pass |
| Level 3 combat desktop | `151` | `26,480` | `152` | `5` | Pass |
| Level 3 combat mobile | `133` | `24,024` | `138` | `5` | Pass |

Desktop budgets are `300` calls, `750,000` triangles, `300` geometries, and `60` textures. Mobile budgets are `150`, `300,000`, `200`, and `40`. Every measured state remains inside its budget.

## Visual review

Direct inspection confirmed:

- the frog reads clearly from the active camera through its broad head, protruding eyes, pale belly, folded legs, feet, mouth line, and rain-drop accent;
- Trống Gọi Mưa reads as a separate horizontal drum tower rather than a second base;
- warm terrain does not swallow Fire because Fire keeps saturated emissive red/orange while links and rain use cyan-blue contrast;
- the road remains a single continuous surface without visible logical grid seams;
- props stay outside the route and do not obstruct tutorial cells;
- desktop and portrait mobile preserve readable HUD hierarchy and usable touch targets;
- the reaction modal, drag-link guide, placement grid, enemy elemental status, combat, and bounded camera orbit remain visually legible.

Evidence:

- `artifacts/coc-kien-troi-drought/level1/desktop-preparation.png`
- `artifacts/coc-kien-troi-drought/level1/mobile-preparation.png`
- `artifacts/coc-kien-troi-drought/level2/desktop-combat.png`
- `artifacts/coc-kien-troi-drought/level2/mobile-combat.png`
- `artifacts/coc-kien-troi-drought/level3/desktop-combat.png`
- `artifacts/coc-kien-troi-drought/level3/mobile-combat.png`
- `tests/visual.spec.ts-snapshots/`
- `artifacts/perimeter-decoration-2026-08-17/`

## Asset provenance

Credential probes returned `TRIPO_API_KEY=MISSING`, `GEMINI_API_KEY=MISSING`, and `ELEVENLABS_API_KEY=MISSING`. No provider-generated or externally licensed runtime assets were added. Geometry, deterministic canvas textures, CSS, and audio are project-local procedural work.

## Deployment

- Project: `projectile-network-td-soul`
- Deployment ID: `dpl_ABQ68TEJGVzEtN51p98Q6mCm4Js5`
- State: `READY`
- Stable URL: [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app)
- Immutable URL: [https://projectile-network-td-soul-hqtidqi9s-vkevs-projects.vercel.app](https://projectile-network-td-soul-hqtidqi9s-vkevs-projects.vercel.app)

## Remaining boundary

Physical-device performance, production illustration/audio, narrative cutscenes, save/progression integration, and a Unity production port remain outside this browser-prototype scope.
