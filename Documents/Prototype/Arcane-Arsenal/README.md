# Arcane Arsenal

A playable 3D projectile-network tower-defense prototype based only on `RawConcept_2.md`. Towers do not directly acquire targets: ammunition is physically routed between tower nodes, can hit several enemies in transit, gathers unique elemental payloads, and either leaves an aimed elemental output or charges the Nexus Lance.

## Included prototype scope

- Seven tower types: Foundry, Fire, Ice, Wind, Earth, Amplifier, and Nexus Lance.
- Continuous tower rotation, free-flight ammunition, physical support-tower interception, multi-source FIFO buffers, backpressure, legal aimed loops, shot range, and wall obstruction.
- Fire/Ice/Wind/Earth projectile fusion plus six enemy-field reactions.
- Two sequential stages: **First Circuit**, a guided ground-only map with three light live waves, followed by the larger two-height **Prismatic Breach** encounter with six progressively harder waves.
- Stage 1 onboarding is text-free: no briefing modal or objective card. Animated hand, glow, drag path, placement footprint, and world pointer cues teach the three-wave sequence. After Fire is placed, the player opens Wave 2 first; when an enemy enters range and the Foundry actually fires, combat holds briefly while the visual cues teach Foundry → Fire routing and Fire's head-on output.
- Ground and Layer 1 firing with matching ground and flying enemies in Stage 2; Stage 1 has no elevated terrain, flying enemies, or air-route guide.
- Free grid building during waves, tower footprints, upgrades, paid movement, selling, continuous aiming, and two Amplifier branches.
- Fixed enemy paths, side-by-side movement, piercing physical projectiles, economy, Nexus lives, nine waves across two stages, stage transition, win/fail/restart, pause, and 1×/2× speed.
- Level 2 starts with `160` Arcana, grants `1.5×` kill rewards, introduces Amplifier before Wave 3 and Nexus Lance before Wave 4, and uses two practical Layer 1 plateaus beside the lane.
- Continuous-looking terrain over a logical placement grid, visibly rotating tower aim, a temporary selected-tower shot line, stronger enemy body hues for active elemental states, grouped build categories, elemental infusion rings/icons, responsive touch UI, and Web Audio SFX. No external assets or API keys are required.
- All player-facing copy is Vietnamese. Before each wave, a compact top-center roster previews the exact enemies; hover reveals temporary details on desktop, while click/tap pins or closes the detail popover on every device.

This is a two-stage vertical slice, not the full campaign/progression and final balance described by the wider concept.

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

The Playwright suite covers the text-free three-wave ground tutorial, the live enemy-range Fire lesson, physical projectile-to-tower interception, selected-tower shot preview, head-on terminal aim, Stage 1 → Stage 2 transition, Vietnamese localization, stage-specific rewards, exact upcoming-wave rosters, hover/click enemy inspection, gameplay bot, desktop/mobile canvas and UI checks, stronger elemental enemy hues, pause/resume, fail/restart, and deterministic visual baselines.

Measured production-preview evidence on Google Chrome with a real Intel D3D11 GPU:

| Viewport | Draw calls | Triangles | Geometries | Textures | DPR | Budget |
|---|---:|---:|---:|---:|---:|---|
| Desktop 1280×720 | 142 | 15,200 | 121 | 5 | 1.0 | Pass |
| Mobile 390×664 | 104 | 11,874 | 94 | 5 | 1.5 | Pass |

Canvas sampling passed with no console/page errors. Desktop/mobile color entropy measured 4.92/5.03 bits, edge density 0.289/0.265, and luminance contrast 152.6/153.6. The automated tutorial playtest still wins all three waves and confirms projectile interception, elemental infusion, and a Fire + Ice reaction.
