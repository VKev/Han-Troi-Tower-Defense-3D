# Arcane Arsenal

A playable 3D projectile-network tower-defense prototype based only on `RawConcept_2.md`. Towers do not directly acquire targets: ammunition is physically routed between tower nodes, can hit several enemies in transit, gathers unique elemental payloads, and either leaves an aimed elemental output or charges the Nexus Lance.

## Included prototype scope

- Seven tower types: Foundry, Fire, Ice, Wind, Earth, Amplifier, and Nexus Lance.
- Continuous tower rotation, free-flight ammunition, physical support-tower interception, multi-source FIFO buffers, backpressure, legal aimed loops, shot range, and wall obstruction.
- Fire/Ice/Wind/Earth projectile fusion plus six enemy-field reactions.
- Three sequential stages: **Mạch Đầu Tiên**, a ground-only map with three guided waves followed by three mastery waves; **Khe Nứt Lăng Kính**, a larger two-height encounter with six waves; and **Đại Địa Hợp Lưu**, a 20×14 battlefield with a longer route, two practical Layer 1 plateaus, and ten progressively harder waves.
- Stage 1 begins with a three-wave causal tutorial without a briefing or objective card. Wave 1 places only the Foundry at a head-on lane position and proves it can shoot and kill. After the wave clears, Fire is placed and aimed toward the future route. Wave 2 then leaves Fire visibly idle for `1.6` seconds while an enemy is in range because it has no input ammunition; combat holds only while the player rotates Foundry into Fire. Wave 3 adds Ice, teaches Fire → Ice, and pauses `0.9` seconds after the first live Fire + Ice reaction to show a dismissible **Thermal Shock** explanation before resuming the wave. Waves 4–6 then remove all hand/placement/rotation locks, allow free Foundry/Fire/Ice strategy, and increase enemy count from `12` to `16` to `21` with `1.45×`, `1.85×`, and `2.35×` HP. Stage 1 has three Nexus lives and every leak costs one; a mastery defeat restores the clean post-reaction Wave 4 checkpoint.
- Ground and Layer 1 firing with matching ground and flying enemies in Stage 2; Stage 1 has no elevated terrain, flying enemies, or air-route guide.
- Free grid building during waves, tower footprints, upgrades, paid movement, selling, continuous aiming, and two Amplifier branches.
- Fixed enemy paths, a bright red ground arrow at each spawn, deliberately uneven gaps and lane offsets that permit overlap/side-by-side clusters, piercing physical projectiles, economy, Nexus lives, 22 waves across three stages, stage transitions, win/fail/checkpoint-retry, pause, and 1×/2× speed.
- Economy gain is intentionally scarce: enemy drops use a global `0.6×` multiplier and wave-clear bonuses use `0.65×`.
- Every stage uses strictly increasing enemy counts and HP multipliers. Level 2 ramps from `10` to `61` enemies and `1.1×` to `6.2×` HP across six waves; Level 3 ramps from `20` to `148` enemies and `1.3×` to `15.5×` across ten waves. Elemental reactions add damage equal to `6%` of target maximum HP, so late waves reward correct multi-element routing.
- Level 2 starts with `160` Arcana, introduces Amplifier before Wave 3 and Nexus Lance before Wave 4, and uses two practical Layer 1 plateaus beside the lane.
- Level 3 starts with `220` Arcana, expands the logical board to 20×14, keeps combat on Ground and Layer 1, and introduces the flying Sandstorm-barrier Sky Warder plus the ground Crystal-Shatter-barrier Rift Colossus.
- Projectile travel is `3×` the original speed, tower production/output cadence is `1.5×`, projectile visuals and hit radius are `2×`, and enemy movement is `0.6×`. Swept-segment collision prevents tunnelling.
- Continuous-looking terrain over a logical placement grid, visibly rotating tower aim, a temporary selected-tower shot line, stronger enemy body hues for active elemental states, grouped build categories, elemental infusion rings/icons, responsive touch UI, and Web Audio SFX. Every build card has an independent eye control that opens price, footprint, range, storage, role, unlock state, and upgrade details without placing or buying the tower. No external assets or API keys are required.
- All player-facing copy is Vietnamese. Before each wave, a compact top-center roster previews the exact enemies with explicit `MẶT ĐẤT` or `BAY · TẦNG 1` movement labels; hover reveals temporary details on desktop, while click/tap pins or closes the redesigned detail popover on every device.
- The mandatory Level 2 Amplifier, Nexus Lance, and dedicated Lance-feeder Foundry are granted at `0` cost only when placed on the currently highlighted lesson cell. Their build card and catalog detail display `0`, selling a granted tower refunds `0`, and selling it during the lesson reactivates the requirement. The player's retained `35`/`45` Arcana stays below the cheapest optional purchase even with the Rotation variant's tower-count price growth, while all optional towers retain their current scaled prices and affordability rules.
- The first tower purchase highlights the Arcana HUD value without opening any card or modal. The first Nexus life loss uses the same highlight-only treatment. The first tutorial Fire + Ice reaction uses the paused explanation described above.

Except for routing controls and projectile interception behavior, this rotation build shares the same maps, waves, enemy HP, movement speed, spawn density/pattern, economy, tower/projectile tuning, VFX, UI, and no-LOD enemy rendering as `Arcane-Arsenal-Link`.

This is a three-stage vertical slice, not the full campaign/progression and final balance described by the wider concept.

## Play locally

```powershell
npm install
npm run dev
```

Open `http://127.0.0.1:5188`.

Desktop controls:

- Tap/click a build card, then a grid cell.
- Select any ammunition tower and hold **Left** or **Right** to rotate its output smoothly.
- The selected ammunition tower shows one wide, translucent red shot guide through its current physical range; the guide updates continuously while rotating and disappears when the tower is deselected.
- If its physical projectile path crosses a compatible support tower, the round enters that tower's buffer, gains its buff, and is fired again along the receiver's own angle. Otherwise it keeps flying freely until shot range ends or a wall stops it.
- Drag to pan, right-drag to orbit, and use the wheel to zoom.
- Shortcuts: `1–7` build, `M` move, `U` upgrade, `Q/E` rotate, `Space` wave/pause, `Esc` cancel.

Mobile controls:

- Tap UI and grid cells; one-finger drag pans and two-finger gesture zooms/orbits.
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

The Playwright suite covers the wave-gated three-wave ground tutorial, the three-wave post-reaction mastery curve and Wave 4 checkpoint retry, highlight-only currency onboarding, the Fire idle proof, the paused Thermal Shock explanation, physical projectile-to-tower interception, selected-tower shot preview, hold-to-rotate controls, Stage 1 → 2 → 3 progression, shared HP/density/speed curves, exact upcoming-wave rosters, enemy details, Level 3 barriers, Lance feedback, gameplay bot, desktop/mobile canvas and UI checks, stronger elemental enemy hues, pause/resume, fail/restart, and deterministic visual baselines.

Measured production-preview evidence on Google Chrome with a real Intel D3D11 GPU:

| Viewport | Draw calls | Triangles | Geometries | Textures | DPR | Budget |
|---|---:|---:|---:|---:|---:|---|
| Desktop 1280×720 | 142 | 15,200 | 121 | 5 | 1.0 | Pass |
| Mobile 390×664 | 104 | 11,874 | 94 | 5 | 1.5 | Pass |

Canvas sampling passed with no console/page errors. Desktop/mobile color entropy measured 4.92/5.03 bits, edge density 0.289/0.265, and luminance contrast 152.6/153.6. The automated tutorial playtest still wins all three waves and confirms projectile interception, elemental infusion, and a Fire + Ice reaction.

Latest production release: deployment `dpl_CtZEuqgN3TTxoaRPh9Pe3oQof8Aq` is `READY` at [`https://arcane-arsenal-tower-defense.vercel.app`](https://arcane-arsenal-tower-defense.vercel.app). The root page and hashed `index-Ckmc2jXz.js` / `index-ZWjKVt1v.css` assets return HTTP 200. Live desktop/mobile probes confirm Rotation routing, the required Level 2 Amplifier lesson at the retained `35` Arcana state, nonblank real-GPU rendering, renderer totals within the standard budgets, and zero console/page errors.
