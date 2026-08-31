# Frog Tongue Skill Technical Specification

Status: Approved — Implemented and verified

## Scope

Replace the persistent Rain Shower field in the `Projectile-Network-TD` web prototype with a one-shot frog tongue attack. The skill remains charged by the Rain Drum network endpoint, but its meter is projected directly above the frog. The player drags from that meter to a battlefield point to cast.

The approved base attack uses a 0.16-second extension, 0.08-second hold, and 0.28-second retraction. Enemies crossed by the tongue outside the impact area take 20% of the impact damage. The target area has radius 2.7 and deals `220 + min(18% of target max HP, 500)` magic damage once. Enemies inside the target area do not also receive path damage. Enemies killed by the target impact are carried back to the frog mouth. Impact produces a short pulse, damage feedback, and a light 0.18-second camera shake.

## Upgrade branches

- `Quét Rộng`: radius 3.1, `190 + min(16% max HP, 450)` magic damage.
- `Đớp Mạnh`: radius 2.5, `300 + min(22% max HP, 700)` magic damage.

The Rain Shower field, repeated damage ticks, slow field, and projectile amplification field are removed.

## Interaction and tutorial

- The meter follows a projected point above the frog on desktop and mobile.
- A valid cast starts only by dragging the ready meter and releasing on the battlefield.
- While dragging, show the impact circle and the tongue corridor from the frog mouth to the pointer.
- In the first mastery wave, once the meter is full and living enemies are present, the text-free tutorial hand repeatedly drags from the meter to the densest valid enemy cluster.
- The tutorial is complete only after the player releases a valid cast. Cancelled drags do not complete it.

## Approved three-dimensional tongue presentation revision

The owner supplied a frog reference and approved replacing the line-like tongue presentation with a procedural solid model. The tongue body must visibly stretch from the mouth instead of appearing at full length, use lit three-dimensional material and a tapered organic silhouette, and terminate in a clearly larger rounded bulb. The tongue must not use additive glow or emissive treatment like a projectile. The AOE impact area must use a brief, low-opacity ring and ground tint, while low-poly drought-soil particles provide the main impact response by launching upward and outward from the contact point. Damage, timing, targeting, capture, tutorial, and branch balance remain unchanged.

## Runtime ownership

- `Game.ts` owns targeting, damage resolution, capture state, animation timing, camera shake, diagnostics, and tutorial completion.
- `ArtFactory.ts` owns the frog mouth presentation anchor.
- `styles.css` and `index.html` own the frog-following meter presentation and Vietnamese labels.
- Existing projectile, enemy, wave, and link-network balance remains unchanged.

## Compatibility and migration

Existing branch enum values `suppression` and `conduction` remain serialized/runtime-compatible, but their Vietnamese names and effects change to `Quét Rộng` and `Đớp Mạnh`. Existing test-state aliases remain callable while diagnostics move from rain-field counters to tongue-attack counters.

## Verification

- TypeScript/Vite production build.
- Playwright desktop and mobile interaction test for the complete tutorial drag.
- Deterministic tests for path-only damage, non-stacking target damage, branch profiles, captured kills, and skill reset.
- Visual regression or focused screenshot inspection of the ready meter, drag preview, full tongue extension, impact feedback, and mobile layout.
- Diagnostics and behavior tests must identify the solid model, rounded tip, model thickness, and exact mouth-to-target extension length.
- Production Vercel deployment and live smoke check.

## Risks and deferred work

The procedural tongue is intentionally low-poly and does not use skeletal animation. Audio remains the existing special-skill cue. Production Unity implementation is outside this web-prototype scope.

## Implementation status and validation evidence

Implementation is complete with no deviation from the approved interaction or damage contract. The tongue now grows continuously from the named frog-mouth anchor to the released battlefield point as an opaque tapered `MeshStandardMaterial` model, ends in a radius-0.68 spherical bulb with a lit highlight, holds at full extension, and retracts to the mouth while carrying enemies killed by the impact. Captured enemies are scaled and distributed around the bulb so they do not hide it. The tongue has no emissive or additive glow mesh. Fourteen deterministic low-poly soil particles launch ballistically from the impact, while the ground disc remains at opacity 0.06. The ready meter follows the projected frog position, and the text-free tutorial performs the same real drag interaction required from the player.

Validation completed on 17 August 2026:

- `npm run build` passed with the production Vite bundle.
- The complete Playwright matrix passed 70 tests with 10 intentional viewport-specific skips.
- Desktop and mobile visual baselines passed for the drag preview, tongue extension, impact, capture, camera feedback, and tutorial cue.
- Hardware-backed Intel D3D11 canvas probes were nonblank and error-free on desktop and mobile. The final deployed skill-feedback state used 109 draw calls, 10,740 triangles, 110 geometries, and 9 textures, within both render budgets. Diagnostics confirmed zero tongue glow meshes and fourteen active soil particles.
- Vercel deployment `dpl_3Ncv1T1t2gVEU8XYy2HWykvig1u4` reached `READY` and was aliased to `https://projectile-network-td-soul.vercel.app`.
