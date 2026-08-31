# Arcane Arsenal

A playable 3D projectile-network tower-defense prototype based only on `RawConcept_2.md`. Towers do not directly acquire targets: ammunition is explicitly linked between tower nodes, can hit several enemies while crossing each link segment, gathers unique elemental payloads, and can charge the terminal Nổ Arcana.

## Included prototype scope

- Seven tower types: Foundry, Fire, Ice, Wind, Earth, Amplifier, and Nổ Arcana.
- Button-free one-output tower links: select a source, then drag from that selected tower to a highlighted receiver and release. Source towers visibly face their current receivers; multi-source FIFO transfer, legal non-reciprocal loops, link range, wall obstruction, and completed-relay locking remain authoritative.
- Fire/Ice/Wind/Earth projectile fusion plus six enemy-field reactions.
- Three sequential stages: **Mạch Đầu Tiên**, a ground-only map with three guided waves followed by three mastery waves; **Khe Nứt Lăng Kính**, a larger two-height encounter with six waves; and **Đại Địa Hợp Lưu**, a 20×14 battlefield with a longer route, two practical Layer 1 plateaus, and ten progressively harder waves.
- Stage 1 onboarding is text-free: no briefing modal or objective card. Animated hand, glow, drag path, placement footprint, and world pointer cues teach the exact select-then-drag gesture for Foundry → Fire, Fire → Ice, and Ice → a terminal Fire across the first three waves. Waves 4–6 remove the hand and forced cells, unlock free placement/move/upgrade/sell for Foundry, Fire, and Ice, and increase enemy count from `12` to `16` to `21` with `1.45×`, `1.85×`, and `2.35×` HP. Stage 1 has three Nexus lives and every leak costs exactly one; a mastery defeat restores the clean post-reaction Wave 4 checkpoint instead of replaying onboarding.
- Ground and Layer 1 firing with matching ground and flying enemies in Stage 2; Stage 1 has no elevated terrain, flying enemies, or air-route guide.
- Free grid building during waves, tower footprints, upgrades, paid movement, selling, explicit relinking, and two Amplifier branches.
- Fixed enemy paths, a bright red ground arrow on each stage's opening corridor, deliberately uneven gaps and lane offsets that permit overlap/side-by-side clusters, piercing physical projectiles, economy, Nexus lives, 22 waves across three stages, stage transitions, win/fail/checkpoint-retry, pause, and 1×/2× speed.
- Economy gain is intentionally scarce: enemy drops use a global `0.6×` multiplier and wave-clear bonuses use `0.65×`. Level 2's authored `1.5×` stage reward factor is retained before the global reduction, so later stages reward stronger enemies without restoring the earlier income rate.
- Every stage uses strictly increasing enemy counts and HP multipliers. Level 2 ramps from `10` to `61` enemies and `1.1×` to `6.2×` HP across six waves; Level 3 ramps from `20` to `148` enemies and `1.3×` to `15.5×` HP across ten waves. Elemental reactions add damage equal to `6%` of the target's maximum HP, while reaction barriers still require their named reaction, so late waves reward correct multi-element routing rather than neutral projectile volume alone.
- Level 2 starts with `220` Arcana, introduces Amplifier before Wave 3 and Nổ Arcana before Wave 4, and uses two practical Layer 1 plateaus beside the lane.
- Level 3 starts with `220` Arcana, expands the logical board from Level 2's 12×9 cells to 20×14, keeps enemy/tower combat on Ground and Layer 1, and introduces **Hộ Vệ Thiên Lăng** (a Layer 1 Sandstorm reaction barrier) plus **Cự Tượng Khe Nứt** (a ground boss with a Crystal Shatter barrier and heavy Nexus damage).
- All projectile travel is tuned to `3×` the original prototype speed, tower production/output cadence to `1.5×`, projectile visuals and hit radius to `2×`, and enemy movement to `0.6×`. Swept-segment collision remains active so faster rounds do not tunnel through enemies along a linked segment.
- The Level 2 Nổ lesson places the special tower beside the route, requires a dedicated additional Foundry, and teaches the player to link that Foundry into Nổ. Every Nổ displays its live ammunition fill as a camera-facing bar and automatically detonates when full and a same-layer enemy enters its exact one-cell radius. The radial hit inherits the stored elements and can damage every same-layer enemy inside that zone. Its transient feedback now fills that exact radius with a ground disc, four expanding shock rings, a two-layer flash core, eighteen radial shards, and a separate hit cue on every affected enemy.
- Continuous-looking terrain over a logical placement grid, persistent network lines, transparent range discs, highlighted link candidates, stronger enemy body hues for active elemental states, grouped build categories, elemental infusion rings/icons, responsive touch UI, and Web Audio SFX. Every build card has an independent eye control that opens price, footprint, range, storage, role, unlock state, and upgrade details without placing or buying the tower. No external assets or API keys are required.
- All player-facing copy is Vietnamese. Before each wave, a compact top-center roster previews the exact enemies with explicit `MẶT ĐẤT` or `BAY · TẦNG 1` movement labels; hover reveals temporary details on desktop, while click/tap pins or closes the redesigned detail popover on every device.
- The mandatory Level 2 Amplifier, Nổ, and dedicated Nổ-feeder Foundry are granted at `0` cost only when placed on the currently highlighted lesson cell. Their build card and catalog detail display `0`, selling a granted tower refunds `0`, and selling it during the lesson reactivates the requirement. The player's retained `35`/`45` Arcana stays below the cheapest optional purchase, while all optional towers retain normal prices and affordability rules.
- The first tower purchase highlights the Arcana HUD value without opening a card or modal. The first Nexus life loss uses the same highlight-only treatment. The first elemental reaction retains its one-time text-free animated explanation on desktop and mobile.

Except for explicit linking versus continuous aiming, this build shares the same maps, waves, enemy HP, movement speed, spawn density/pattern, economy, tower/projectile tuning, VFX, UI, and no-LOD enemy rendering as the rotation prototype.

This is a three-stage vertical slice, not the full campaign/progression and final balance described by the wider concept.

## Play locally

```powershell
npm install
npm run dev
```

Open `http://127.0.0.1:5188`.

Desktop controls:

- Tap/click a build card, then a grid cell.
- Select a source tower once. Then press it again, drag to a highlighted compatible receiver inside its transparent range disc, and release to link.
- Each tower has one output and may receive multiple inputs while it remains terminal. Once a tower has both an input and an output, it rejects additional new inputs. A direct reciprocal link is rejected; longer intentional loops remain legal.
- Projectiles fly only along the authored link segment. A buff tower with no outgoing link stores incoming ammunition and does not fire.
- Drag to pan, right-drag to orbit, and use the wheel to zoom.
- Shortcuts: `1–7` build, `M` move, `U` upgrade, `Space` wave/pause, `Esc` cancel.

Mobile controls:

- Tap UI and grid cells; one-finger drag pans and two-finger gesture zooms/orbits.
- Tap a source tower to select it, then drag from that selected tower to a glowing receiver and release.
- Tap the eye row beneath a tower icon to inspect it without entering placement mode or spending Arcana.
- The portrait layout uses a focused tower inspector. **Cancel** returns attention to the battlefield.

## Static HTML build

```powershell
npm run build
npm run preview
```

The deployable artifact is [`dist/`](dist/). Vite uses `base: './'`, so the directory can be published under any static-host subpath. Preview it at `http://127.0.0.1:4188`; serve the directory through HTTP instead of opening `dist/index.html` through `file://`.

## Verification

```powershell
npm run build
npm test
npm run inspect:canvas -- --url http://127.0.0.1:4188 --state active-play
npm run inspect:canvas -- --url http://127.0.0.1:4188 --mobile --state active-play
```

The Playwright suite covers the text-free three-wave ground tutorial, the three-wave post-reaction mastery curve and Wave 4 checkpoint retry, desktop/mobile select-then-drag linking, valid-target highlights, reciprocal-link and completed-relay rejection, terminal buff flow, linked-segment enemy damage, Stage 1 → 2 → 3 progression, strictly increasing wave counts/HP/threat, the `6%` maximum-HP reaction bonus, all three spawn-direction arrows, Level 3's larger board/route and ten-wave pressure curve, both new enemy profiles and their runtime models, Vietnamese localization, reduced economy gains, exact upcoming-wave rosters, ground/flying movement labels, hover/click enemy inspection, independent tower-detail controls, exact-cell free lesson grants with zero resale value, one-cell same-layer Nổ damage, anchored full-radius radial Nổ VFX, Nổ feeder/ammo feedback, gameplay bot, desktop/mobile canvas and UI checks, stronger elemental enemy hues, pause/resume, fail/restart, and deterministic visual baselines.

Measured production-preview evidence on Google Chrome with a real Intel D3D11 GPU:

| Viewport | Draw calls | Triangles | Geometries | Textures | DPR | Budget |
|---|---:|---:|---:|---:|---:|---|
| Desktop 1280×720 | 134 | 15,122 | 120 | 4 | 1.0 | Pass |
| Mobile 390×664 | 113 | 13,870 | 103 | 4 | 1.5 | Pass |

The explicit-link build is published at [`https://arcane-arsenal-link-network.vercel.app`](https://arcane-arsenal-link-network.vercel.app). Deployment `dpl_3gg2EGX7fXHGM9Q9xzYUHqcYsxT7` passed live desktop mouse and mobile Chrome touch sampling: no Link button existed, dragging the selected source exposed the exact valid highlighted receiver, release created one connection, and the source model reported zero facing error. The responsive mobile inspector docked away from the selected source so its center remained directly touchable. The alias and hashed `index-CyI0mLua.js` / `index-CmmsL3ez.css` assets returned HTTP 200 with no console or page errors.

Latest production release: deployment `dpl_DyvZRfQnKKw2a4Cwj9DVYP7uTuiL` is `READY` at the same stable alias. The root page and hashed `index-ChNhqQXb.js` / `index-Bs7485OH.css` assets return HTTP 200. Live desktop/mobile probes confirm Link routing, the free Level 2 lesson grants, the full-radius Nổ presentation (`2`-unit radius, four rings, eighteen shards, and a per-target cue), nonblank real-GPU rendering, and zero console/page errors.
