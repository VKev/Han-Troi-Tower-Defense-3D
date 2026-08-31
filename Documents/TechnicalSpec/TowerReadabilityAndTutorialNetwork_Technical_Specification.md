# Tower Readability and Tutorial Network Technical Specification

- **Status:** Approved
- **Owner approval:** Direct implementation request on 2026-08-17
- **Tracking issue:** `TowerDefense3D-53b`
- **Target:** `Documents/Prototype/Projectile-Network-TD/`

## Scope

Improve the Three.js projectile-network tutorial without changing the established drought theme, grid footprint, economy, projectile balance, enemy balance, or link gesture. The change shall:

1. Give `Lò Đạn`, `Trụ Hỏa`, `Trụ Băng`, `Trụ Phong`, and `Trụ Địa` clearly different silhouettes at the normal gameplay camera distance.
2. Teach the network's source and terminal after both are placed by illuminating `Lò Đạn` and `Trống Gọi Mưa` while the existing icon-only hand demonstrates the link drag.
3. Show the elemental-reaction explanation only once after it has been acknowledged, including after an in-session restart or checkpoint restore.
4. Allow every complete generator-to-terminal route to transport projectiles. Processor-count progression remains a tutorial lesson, not a hidden validity requirement for additional player-built routes.
5. Start Level 1 from the high, oblique camera composition supplied by the owner while preserving pan, orbit, zoom, resize, desktop input, and touch input.

## Non-goals

- No tower cost, damage, interval, range, projectile, enemy, wave, route, or reward changes.
- No new tower types, text tutorial panels, generated external assets, shaders, physics engine, or Unity runtime changes.
- No change to the one-input/one-output rule for regular towers or the two-input/no-output rule for `Trống Gọi Mưa`.
- No automatic links and no removal of the existing drag-to-link interaction.

## Architecture and ownership

- `src/assets/ArtFactory.ts` owns procedural tower silhouettes and named model parts.
- `src/game/Game.ts` owns tutorial state, network validation, endpoint highlights, reaction-tutorial acknowledgement, runtime diagnostics, and camera initialization consumption.
- `src/game/definitions.ts` owns authored stage camera positions and unchanged gameplay definitions.
- `tests/` owns deterministic interaction, regression, camera, and desktop/mobile visual evidence.

The implementation remains inside the independent web prototype and does not edit the Unity `Assets/` tree or either preserved Arcane Arsenal prototype.

## Runtime contracts

### Tower silhouettes

- Generator: a low, broad mechanical foundry/flywheel profile rather than an elemental shrine.
- Fire: an open brazier with a broad circular rim and three tall flame tongues.
- Ice: an asymmetric multi-spire crystal crown on a hexagonal plinth.
- Wind: a wide four-vane rotor with a narrow mast.
- Earth: a squat, stepped square monolith with orbiting stone mass.
- Each family exposes a stable `userData.modelProfile` and named child parts for diagnostics and tests.
- Shared geometry/material roles remain preferred; silhouette detail must remain within the existing mobile render budget.

### Source/terminal onboarding

- During `link-generator-nexus`, both endpoints receive separate animated 3D focus rings and soft vertical beacons.
- The source cue uses warm ammunition gold; the terminal cue uses rain blue.
- The existing text-free tutorial hand remains the interaction instruction.
- Cues disappear immediately after the link succeeds and must not remain during combat.

### Network validity

- A route is active when it starts at a generator, follows valid directed links without a cycle, and ends at `Trống Gọi Mưa`.
- The number of elemental/support processors on a complete route does not determine whether it can transport ammunition.
- The guided Level 1 sequence still controls when and where the first Fire and Ice processors are introduced.
- Both allowed terminal inputs may transport in the same wave without sharing buffers, reservations, or timers.

### Reaction explanation

- A reaction may schedule the explanation only when the acknowledgement flag is false.
- Closing the explanation sets the acknowledgement flag before gameplay resumes.
- Normal reset/checkpoint operations do not clear acknowledgement during the same browser session.
- Test-only deterministic states may explicitly reset the flag when they need to render the explanation baseline.

### Camera

- Level 1 uses the owner-supplied high oblique composition as its authored initial pose.
- Camera target, yaw, pitch, and distance are derived from the stage definition before the first tutorial projection.
- User pan/orbit/zoom remains bounded by existing input rules.

## Interaction flow

1. Drag `Trống Gọi Mưa` to its authored tutorial cell.
2. Drag `Lò Đạn` to its authored tutorial cell.
3. Both models pulse simultaneously; the hand drags from the gold source cue to the blue terminal cue.
4. After a successful link, the endpoint cues disappear and the Start Wave control receives focus.
5. Fire and Ice lessons continue through the existing staged practice flow.
6. The first real elemental reaction pauses the game once; dismissing its panel permanently acknowledges the lesson for the active browser session.
7. In free play, a second complete generator route to the terminal fires independently.

## Compatibility and migration

- Existing `NodeState`, save/checkpoint payloads, link data, URLs, query parameters, and build-card contracts remain compatible.
- The reaction acknowledgement is ephemeral browser-session state and does not create a progression/save migration.
- Existing visual baselines are updated only for states materially changed by the camera or tower silhouettes.

## Verification plan

- Production TypeScript/Vite build.
- Deterministic tests for unique model profiles and gameplay-camera bounds.
- Tutorial test proving both endpoint cues are visible only during the first link lesson.
- Regression test proving the reaction modal cannot reopen after acknowledgement and reset.
- Real transport test with two independent generators and two terminal inputs; both generators must launch projectiles.
- Initial-camera numeric contract plus desktop and mobile screenshots.
- Full Playwright matrix, production preview, canvas inspection, renderer diagnostics, and Vercel route smoke tests.

## Risks and mitigations

- **Silhouettes still collapse on mobile:** spend geometry on broad primary forms, then verify portrait screenshots rather than relying on color.
- **Endpoint highlights compete with link hints:** use distinct low-opacity beacons and remove them as soon as link dragging takes over.
- **Relaxed hidden processor gate changes tutorial difficulty:** retain tutorial step/link restrictions; only free player-built complete routes gain the corrected behavior.
- **Camera pose clips the dock or inspector:** verify desktop and mobile safe areas and keep player orbit/zoom available.

## Deferred work

- Production-authored 3D tower assets, audio replacement, cross-session account progression, and physical-device thermal profiling remain outside this change.

## Implementation result

- Lò Đạn now uses a mechanical foundry silhouette. Hỏa, Băng, Phong, and Địa use distinct wide-brazier, asymmetric-crystal-crown, broad-wind-rotor, and squat-stepped-monolith profiles.
- The first endpoint lesson illuminates the source in warm gold and Trống Gọi Mưa in rain blue while preserving the text-free drag gesture.
- Reaction acknowledgement persists for the browser session, so reset and checkpoint restore cannot reopen the explanation.
- Network validation now treats any complete acyclic generator-to-terminal route as active. A deterministic two-route regression proves that both sources launch into the two-input terminal.
- Level 1 opens at yaw `1.9712`, pitch `0.7844`, distance `32.9841`, aimed at `[-1, 1.2, -0.4]`.
- The production build and complete Playwright matrix passed: `76 passed`, `10 skipped` by intentional viewport routing. All `24` desktop/mobile visual baselines passed.
- Hardware-backed Intel D3D11 probes were nonblank, error-free, and stayed inside budget at `67` draw calls, `10,362–15,286` triangles, `69–70` geometries, and `5` textures for the changed states.
- Vercel deployment `dpl_JDBX1pCCkhRSdS4K2nKyjQpqnSX2` reached `READY`; the stable root and all three level query routes returned HTTP `200`.

## Approved follow-up — persistent endpoint lesson

The 2026-08-17 owner follow-up extends the source/terminal cue beyond the first link step:

- After both authored endpoint towers exist, keep the warm-gold Lò Đạn beacon and rain-blue Trống Gọi Mưa beacon visible through every guided and mastery wave in Level 1.
- Add a camera-facing `ĐẦU` label above Lò Đạn and a `CUỐI` label above Trống Gọi Mưa. These short Vietnamese labels are the only added tutorial text.
- Rebuild the markers after selection, linking, tower replacement, checkpoint restore, and wave transitions so ordinary interaction cannot accidentally remove them.
- Do not add these endpoint markers to Levels 2 or 3.
- Verify persistence after the first link, after Fire/Ice lessons, and in the post-reaction mastery checkpoint on desktop and mobile.

## Approved follow-up — endpoint sockets and reaction repeat balance

- Add a bright gold socket at the exact outgoing link anchor on the `ĐẦU` Lò Đạn and a bright rain-blue socket at the exact incoming link anchor on the `CUỐI` Trống Gọi Mưa. The sockets remain part of the persistent Level 1 endpoint lesson.
- Remove the per-hit progress rewind from the base Wind status. Wind still applies its status icon and may participate in reactions, but ordinary Wind ammunition cannot hold an enemy stationary.
- Reduce Cuồng Phong's one-time displacement to `0.8` path units for regular enemies and `0.35` for bosses.
- Give every enemy an independent `2.25s` cooldown for each reaction key. Repeating the same reaction during its cooldown consumes that projectile's reaction proc without dealing reaction damage or control; a different reaction remains immediately available.
- Keep the existing `6%` maximum-HP reaction damage ratio. The cooldown, rather than a global damage reduction, is the primary anti-spam limiter.
- Expose the cooldown duration and active per-enemy cooldowns in deterministic diagnostics; verify immediate repeat rejection and post-cooldown recovery.

### Follow-up implementation result

- `ĐẦU` and `CUỐI` now use camera-facing labels, persistent animated rings/glyphs, and bright spheres at the exact outgoing and incoming link anchors.
- The markers persist through link replacement, Fire/Ice lessons, reaction acknowledgement, checkpoint capture/restore, and all three mastery waves on desktop and mobile.
- Base Wind progress rewind is `0`. Cuồng Phong displacement is `0.8` for regular enemies and `0.35` for bosses.
- The `2.25s` per-enemy/per-reaction cooldown blocks immediate duplicate procs and recovers after expiry; different reaction keys remain independent.
- The production build passed. The complete Playwright matrix passed `80` tests with `10` intentional viewport skips, including deterministic mastery balance and desktop/mobile visual baselines.
- Hardware-backed production probes were nonblank and error-free at `75` draw calls, `15,966` triangles, `76` geometries, and `7` textures for both endpoint tutorial viewports.
- Vercel deployment `dpl_7NdSE8pk6bu641E3FbAZKAm4omXL` reached `READY`; the stable root and all three level routes returned HTTP `200`.

## Approved follow-up — directional half-link lesson and neutral link style

The 2026-08-17 owner follow-up changes completed-link presentation without changing network rules:

- Every completed network link uses the same translucent white base beam and arrow. Completed links no longer inherit a tower or element color.
- In Level 1, the completed link leaving the persistent `ĐẦU` Lò Đạn receives a warm-gold overlay from the source socket to exactly the segment midpoint.
- Every completed link entering the persistent `CUỐI` Trống Gọi Mưa receives a rain-blue overlay from exactly the segment midpoint to the terminal socket.
- A direct `ĐẦU`-to-`CUỐI` link therefore reads as one complete highlighted connection: gold on its source half and rain blue on its terminal half.
- The white base remains visible beneath both colored halves so direction and continuity are readable at their midpoint boundary.
- During the Level 1 tutorial, endpoint-adjacent links remain visible with the persistent endpoint lesson even when no network is selected. Other completed links retain the existing selected-network visibility rule.
- Link dragging, validation, projectile simulation, targeting, costs, damage, and tower orientation remain unchanged.
- Diagnostics expose base-link style plus each endpoint half-link role, source, target, color, and normalized coverage. Desktop and mobile tests verify direct and chained forms.

### Directional half-link implementation result

- Every completed link now uses a tone-map-independent white `MeshBasicMaterial` with active opacity `0.42` and inactive opacity `0.18`; its beam and direction arrow no longer inherit any node color.
- The first guided Lò Đạn emits a gold three-layer half-link overlay over normalized coverage `[0, 0.5]`. Every Rain Drum input receives the equivalent rain-blue overlay over `[0.5, 1]`.
- Endpoint-adjacent tutorial links remain visible without a selected node; the middle of a longer tutorial network and every non-tutorial link retain the selected-network rule.
- Deterministic diagnostics read the actual completed-link beam material and expose each endpoint overlay. Tests cover the direct split, chained endpoint segments, persistent desktop/mobile mastery state, and Levels 2–3 neutral link style.
- The production build and complete Playwright matrix passed `82` tests with `10` intentional viewport skips. All `24` desktop/mobile visual baselines passed unchanged.
- Hardware-backed Intel D3D11 production-preview probes were nonblank and error-free at `102` draw calls, `17,064` triangles, `103` geometries, and `7` textures on desktop and mobile, within both budgets.
- Vercel deployment `dpl_EEaYaRDznmvPzxG4L5JdSM9gYfMS` reached `READY`. The stable root and all three level query routes returned HTTP `200`, and live desktop/mobile diagnostics confirmed the white base plus `[0, 0.5]` source and `[0.5, 1]` terminal overlays.

## Approved follow-up — label-only endpoints and chain reminder

The owner temporarily removes all endpoint illumination and directional half-link overlays in favor of a quieter text lesson:

- Keep only camera-facing `ĐẦU` and `CUỐI` labels above the Level 1 tutorial endpoint towers. Remove their ground rings, floating glyphs, halos, bright link sockets, and colored half-link overlays.
- Show each world label as soon as its corresponding tower exists; the other endpoint does not have to be placed first.
- In the Level 1 build dock, display `Lò Đạn (ĐẦU)` and `Trống Mưa (CUỐI)`. Levels 2 and 3 retain their ordinary tower names.
- Keep every completed link as the approved translucent white beam and arrow. No completed-link segment receives an endpoint or per-tower color.
- During Level 1 link objectives, show the Vietnamese tutorial sentence `Hãy chắc chắn nối đầu chuỗi vào cuối chuỗi để hoàn thành chuỗi.` directly below the top bar.
- Hide the sentence during placement objectives, waves, mastery free play, reaction explanation, victory, defeat, and Levels 2–3.
- The reminder must wrap intentionally on narrow screens, respect safe areas, avoid the inspector and gameplay controls, and remain non-interactive.
- Diagnostics and desktop/mobile tests verify independent endpoint-label timing, build-dock role names, reminder visibility by tutorial objective, white links, and absence of removed endpoint VFX.

### Approved chain-completion notification

- When a previously incomplete directed generator-to-Rain-Drum route becomes complete, temporarily reveal and brighten every completed link in that route.
- Run a compact LED-like color packet from the generator to the Rain Drum exactly two times, following the authored link segments in order.
- Use the existing tutorial gold and rain-blue notification palette only for this temporary acknowledgement; completed links return to their neutral translucent white style afterward.
- Do not retrigger continuously while the route remains complete. A new completion after a real disconnection or route replacement may trigger again.
- The effect is presentation-only: it does not launch ammunition, deal damage, charge the skill, pause gameplay, alter selection, or change link validity.
- Dispose the previous notification before starting another one, and expose pass count, route node IDs, progress, active LED count, and active state through diagnostics.
- Verify exactly two passes, automatic cleanup, selected-network visibility restoration, route order, desktop/mobile readability, and render budget.

### Approved route-marker removal

- Remove the red enemy spawn-direction arrow from all three campaign levels.
- Preserve the authored trail geometry, enemy spawn point, waypoint order, movement behavior, and victory return route.
- Do not replace the arrow with another marker, icon, label, or animated cue.
- Expose the absence of the marker through deterministic diagnostics and verify all three level routes.

### Label-only and chain-notification implementation result

- Level 1 now creates independent camera-facing `ĐẦU` and `CUỐI` labels as soon as the corresponding endpoint tower is placed. Ground rings, glyphs, halos, sockets, and directional half-link overlays are absent.
- The Level 1 dock identifies `Lò Đạn (ĐẦU)` and `Trống Mưa (CUỐI)`. The concise chain reminder appears only during directed-link tutorial objectives and wraps below the top bar on mobile.
- Completing a previously incomplete generator-to-Rain-Drum route temporarily reveals and brightens its full ordered path. A nine-light gold/rain-blue packet traverses that path exactly twice, then the notification disposes itself and ordinary selected-network visibility resumes.
- Existing complete routes do not continuously retrigger the notification. Replacing or reconnecting a route can produce a new completion acknowledgement.
- The red enemy spawn-direction arrow was removed from Levels 1, 2, and 3 without changing path geometry or enemy movement.
- The production build passed. The complete Playwright matrix passed `88` tests with `10` intentional viewport skips, including `26` desktop/mobile visual baselines.
- Hardware-backed Intel D3D11 probes of the deployed completion state were nonblank and error-free at `90` draw calls, `14,416` triangles, `93` geometries, and `7` textures on desktop and mobile.
- Vercel deployment `dpl_EURa3uUXziKMSGK51CQ9hfU6bZ3q` reached `READY`; the stable root and all three level routes returned HTTP `200` at [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app).

## Approved follow-up — LED-only acknowledgement and live endpoint validity color

- Remove the temporary bright route-segment overlay from the chain-completion notification. The two-pass moving LED packet is the only completion VFX.
- Do not temporarily reveal, brighten, recolor, or change the opacity of completed link beams during the notification. Link visibility continues to depend only on the selected network.
- In Level 1, render both `ĐẦU` and `CUỐI` labels in green only while the authored source belongs to a complete, valid, directed route that reaches the authored terminal.
- If any segment is removed, redirected, invalidated, or otherwise breaks that full route, both labels immediately return to their neutral color even if one or both endpoint towers still retain some local connection.
- Completing a valid route again turns both labels green and may replay the existing two-pass LED acknowledgement.
- Diagnostics and desktop/mobile tests verify zero completion beam overlays, unchanged base-link materials/visibility, green labels for a complete route, and neutral labels immediately after route breakage.

### LED-only and live-validity implementation result

- The completion acknowledgement now contains only nine alternating gold/rain-blue LED lights moving along the ordered route for exactly two passes. It creates no bright beam segment, does not reveal an unselected network, and does not modify the white base-link material or opacity.
- `ĐẦU` and `CUỐI` both use green `#65f7a4` only when the authored source reaches the authored Rain Drum through one complete valid directed route. They both return to neutral `#fff4c9` immediately when any middle segment is removed, even when the generator still has an outgoing link and the Rain Drum still has an incoming link.
- A deterministic broken-chain state preserves those two local endpoint connections while severing the middle route, proving that label color derives from end-to-end validity instead of endpoint degree.
- The production build passed. The complete Playwright matrix passed `90` tests with `10` intentional viewport skips, including desktop/mobile completion, disconnection, visual, and render-budget coverage.
- Hardware-backed Intel D3D11 production probes were nonblank and error-free on desktop and mobile at `87` draw calls, `14,320` triangles, `90` geometries, and `7` textures. Live complete-route diagnostics reported zero bright segments and zero completion beam overlays while both labels were green. Separate live broken-route probes preserved the source output and terminal input but reported zero active routes and both labels neutral.
- Vercel deployment `dpl_BSm3t5my8bsedrpMKyBY7g8jyd7X` reached `READY`; the stable root and all three level routes returned HTTP `200` at [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app).

## Approved follow-up — endpoint color-change pulse and direct drag linking

- Whenever full-route validity changes between incomplete and complete, animate both `ĐẦU` and `CUỐI` labels with one brief attention pulse: scale from normal size to a readable overshoot, then settle back to normal.
- Trigger the pulse for both directions: neutral-to-green on route completion and green-to-neutral on route breakage. Do not loop while validity remains unchanged and do not replay from unrelated selection or UI refreshes.
- Keep the labels camera-facing and preserve the approved green/neutral colors. The pulse must not obscure towers, enemies, links, or tutorial controls; reduced-motion mode may suppress the overshoot.
- In preparation mode, pressing and dragging directly from any link-capable tower immediately starts the live link preview. The source tower does not need to be selected first.
- A short press and release on a tower still selects it for inspection. Dragging empty battlefield space retains camera pan, right-drag retains desktop orbit, and two-pointer input retains mobile orbit/zoom.
- The existing tutorial hand continues to demonstrate one continuous source-to-target drag. Desktop and mobile tests verify direct first-gesture linking, tap-to-inspect, background pan separation, pulse activation on completion and breakage, and stable idle label scale after the pulse.

### Endpoint pulse and direct-drag implementation result

- Both camera-facing labels now use a deterministic `0.46s` sine pulse from `1x` to a `1.34x` peak and back to `1x`. The transition fires once for neutral-to-green completion and once for green-to-neutral breakage; selection refreshes do not retrigger it.
- Reduced-motion and screenshot-stabilization states suppress the overshoot and keep the labels at `1x`. Diagnostics expose pulse activity, remaining time, peak/current scale, transition count, direction, and route-validity state.
- In preparation mode, the first pointer press on any non-terminal tower selects it and immediately starts the live link drag. Releasing without a target preserves ordinary tower inspection, while battlefield pan, desktop orbit, and mobile two-pointer camera input remain separate.
- Tutorial, Level 2 lesson, desktop mouse, and mobile touch helpers now perform one uninterrupted source-to-target drag without a preparatory tap.
- The production build passed and the complete Playwright matrix passed `92` tests with `10` intentional viewport skips. Live desktop/mobile production probes measured a `1.3368x` pulse peak settling to `1x`, proved direct drag active on the first press, and reported no console or page errors.
- Hardware-backed Intel D3D11 canvas probes remained nonblank and within budget at `87` draw calls, `14,320` triangles, `90` geometries, and `7` textures. Vercel deployment `dpl_3pgMx6HuHiNodaW1H8UH9wAjk32Q` reached `READY`; the stable root and all three level routes returned HTTP `200` at [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app).

## Approved follow-up — separate the Ice lesson route in screen space

- Move the authored Level 1 Ice tutorial placement so that, under the approved default camera, the Ice tower appears below the Lò Đạn instead of horizontally beside it.
- The resulting `Hỏa → Băng` segment must visibly diverge from the existing `Lò Đạn → Hỏa` segment. The two lesson links must not read as one collinear line doubling back through Hỏa.
- Preserve the logical grid, all tower footprints, the authored Lò Đạn/Hỏa/Trống Gọi Mưa positions, link-range validation, obstacle validation, enemy-path geometry, and tutorial wave balance.
- Keep `Hỏa → Băng` and `Băng → Trống Gọi Mưa` within their authored connection ranges and ensure the completed three-link network remains valid and able to damage enemies.
- Desktop and mobile tests expose the authored world and projected screen positions, verify Ice is below Lò Đạn with a small horizontal offset, verify a meaningful angle between the two adjacent link segments, and retain the complete tutorial/mastery outcome.

### Ice-route separation implementation result

- The authored Level 1 Băng cell moved from `(-5, -6)` to `(-1, -6)` while Lò Đạn, Hỏa, Trống Gọi Mưa, the logical grid, footprints, enemy path, ranges, and obstacle rules remained unchanged.
- Under the production camera, Băng projects `116.49px` below Lò Đạn with `28.63px` horizontal offset on desktop and `102.41px` below with `25.17px` offset on mobile. The screen-space turn at Hỏa is `28.29°` on both viewports, so the two lesson segments no longer read as one doubled-back line.
- The first-reaction lesson now holds an already-cleared wave for its approved `0.75s` delay when the reaction defeats the final enemy. Dismissing the paused explanation then completes that cleared wave normally, while the session-level single-display rule remains intact.
- The mastery balance check now models the lesson it actually teaches: the unchanged four-node tutorial route loses the final wave, while the expanded reaction network plus one charged Cóc strike wins.
- The production build passed. The complete Playwright matrix passed `92` tests with `10` intentional viewport skips; all `26` visual baselines passed after regenerating only the mobile link-drag composition affected by the authored Băng move.
- Vercel deployment `dpl_9ngSoLM3MKxtaZrWtiQQjgQL21DF` reached `READY`. The stable root and Levels 2–3 routes returned HTTP `200`; live desktop/mobile probes confirmed world position `[-1, 0.62, -6]`, three directed links, one active chain, and no console or page errors at [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app).

## Approved follow-up — route-valid tower brightness

- Every newly placed intermediate tower starts in a clearly dimmed presentation while it does not belong to a complete valid directed route from a Lò Đạn source to Trống Gọi Mưa.
- Lò Đạn (`ĐẦU`) and Trống Gọi Mưa (`CUỐI`) always retain their authored full-color presentation, whether or not they are currently connected.
- When network validation proves that an intermediate tower belongs to at least one complete source-to-terminal route, restore its exact authored base color and emissive appearance.
- If any segment is removed, redirected, sold, looped, or otherwise invalidates that full route, every affected intermediate tower returns to the dimmed state immediately. Local input/output links are insufficient without end-to-end validity.
- The rule applies consistently to Hỏa, Băng, Phong, Địa, Tiếp Sức, and Sấm across Levels 1–3, including two independent routes entering Trống Gọi Mưa.
- Dimming is presentation-only. It must not change graph validity, projectile transport, targeting, upgrades, selection, tower price, combat balance, link visibility, tutorial steps, or endpoint label color.
- Tower-local material instances must prevent one tower's brightness state from recoloring another tower or the shared environment material library.
- Diagnostics and desktop/mobile tests verify unlinked placement, complete-route activation, disconnection regression, endpoint exceptions, multiple routes, actual material color/emissive ratios, visual readability, renderer budget, and release deployment.

### Route-valid tower brightness implementation result

- Newly placed intermediate towers (`Hỏa`, `Băng`, `Phong`, `Địa`, `Tiếp Sức`, `Sấm`) now render at `0.32`× base color, `0.08`× emissive color, and `0.12`× emissive intensity — a near-black silhouette — until they belong to a complete, valid, directed route from a `Lò Đạn` to `Trống Gọi Mưa`. `Lò Đạn` and `Trống Gọi Mưa` stay at their authored full-color presentation regardless of connection state.
- `Game.ts` clones each placed node's materials on `prepareNodeNetworkPresentation` so per-tower dimming can never bleed into another tower or the shared `MaterialLibrary` cache. `refreshNodeNetworkPresentation` re-derives every tower's brightness from live route validity on every placement, link change, sale, and disconnection.
- Deterministic diagnostics expose `networkVisual: { state, reason, materialCount, colorRatio, emissiveRatio, emissiveIntensityRatio }` per node and `balance.networkTowerPresentation` with the three multipliers, so tests assert the actual rendered ratios rather than inferring them.
- The production build and complete Playwright matrix passed `96` tests with `10` intentional viewport skips, including the dedicated dim/complete-route/broken-route/dual-route coverage and refreshed desktop/mobile visual baselines.
- Hardware-backed Intel D3D11 probes of the deployed unlinked-tower state were nonblank and error-free on desktop and mobile at `65` draw calls, `10,342` triangles, `67` geometries, and `5` textures, within both render budgets, and matched the local production build exactly.
- Vercel deployment `dpl_9ahj1vWLNcgEhAPa3EBP5PQWq1Mc` reached `READY`; the stable root and all three level query routes returned HTTP `200` at [https://projectile-network-td-soul.vercel.app](https://projectile-network-td-soul.vercel.app), and live diagnostics confirmed the four unconnected elemental towers at `colorRatio 0.32` while `Lò Đạn`/`Trống Gọi Mưa` stayed at `1`.

## Approved follow-up — light-up particle effect on route completion

- The moment an intermediate tower's `networkVisual.state` flips from `dimmed` to `full` (its route just became complete), play a brief particle cue in that tower's own authored color so the brightening reads as an event, not just a static color swap.
- The cue must not fire on placement (a freshly placed, still-disconnected tower starts dimmed with no cue) or on darkening (route breakage stays a silent color change, per the existing spec).
- Reuse the established VFX vocabulary (ground pulse ring plus rising spark burst) rather than introducing a new visual language, and respect `reducedMotion` exactly like existing bursts.

### Light-up implementation result

- `refreshNodeNetworkPresentation` now remembers each node's previous `dimmed`/`full` state before recomputing it. When a non-endpoint node transitions from `dimmed` to `full`, `spawnNodeLightUpEffect` fires a `1.1`-radius ground pulse ring plus a `1.5`-size rising spark burst at the tower's anchor, tinted with that tower's own `NODE_DEFINITIONS[type].color` (e.g. warm red for Hỏa, cyan for Băng).
- Both effects reuse the existing `spawnPulse`/`spawnBurst` VFX primitives and the shared `updateVfx` fade/dispose loop, so they decay and clean up in under `1.1s` with no new rendering code paths, and automatically respect `reducedMotion`.
- The cue fires only on the dimmed→full transition (verified via `autoBuildMinimumChain`, which triggers it for Hỏa and Băng at the moment their links complete the route) and never on placement or on route breakage.
- The production build and complete Playwright matrix passed `96` tests with `10` intentional viewport skips; all existing desktop/mobile visual baselines remained unchanged, since the cue fully fades before any deterministic screenshot capture.
- A live capture of `autoBuildMinimumChain()` confirmed the cue's tinted pulse/spark visible at `150ms` after route completion and fully disposed (`activeVfxCount` back to its pre-cue baseline) well before `1s`, with no leftover geometry.
