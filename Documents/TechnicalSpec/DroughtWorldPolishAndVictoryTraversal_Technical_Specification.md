# Drought World Polish And Victory Traversal — Technical Specification

Status: Approved  
Date: 2026-08-17  
Prototype: `Documents/Prototype/Projectile-Network-TD`  
Bead: `TowerDefense3D-8dm`

## Player-facing contract

- Player promise: build a readable projectile network that helps the blue frog cross the drought-struck road to petition Heaven.
- Target feeling: tactical during waves, then celebratory and purposeful when the frog advances to the next battlefield.
- Primary verb: place and link towers so projectiles cross the enemy route.
- Objective and pressure: defeat increasingly durable enemy waves before they drain the frog Nexus lives.
- Reward/progression: elemental reactions, tower growth, Mưa Rào, and a visible frog journey to the next level after victory.
- Failure/retry: retain the existing fast retry and tutorial-mastery checkpoint rules.
- Skill expression: route geometry, element order, reaction timing, economy, and Mưa Rào placement.
- Non-goals: no balance, path-coordinate, grid, enemy-stat, economy, input, or tutorial-flow changes.

## Scope

### Elemental tower models

Replace the shared column-plus-rune treatment of the four elemental processors with authored, low-detail, drought-fantasy silhouettes. Preserve their transforms, footprints, selection hit behavior, output ports, element colors, and gameplay data.

- Fire: a terracotta brazier/volcanic crown with a layered flame core.
- Ice: a pale stone shrine with a tall crystal prism and surrounding shard fins.
- Wind: a wind shrine with four broad pinwheel vanes and an open central eye.
- Earth: a stepped stone monolith with heavy offset plates and orbiting rock fragments.
- `Trống Gọi Mưa`, `Lò Đạn`, support, and special models remain behaviorally unchanged. The rain-calling drum model is already approved.
- Important moving or testable children receive stable names; repeated detail shares or merges geometry where practical.

### Enemy trail

Keep `ENEMY_PATH` and all enemy movement/collision calculations unchanged. Upgrade only presentation:

- a darker, wider packed-earth shoulder below the route;
- a slightly raised central footpath so the edge has visible depth;
- a deterministic procedural texture containing dusty color variation, two worn ruts, small pebbles, scuffs, footprints, and broken edge marks;
- restrained raised edge stones/clods sampled from the authored path at a low, deterministic density;
- no grid-like lane markings and no visual obstruction of enemies or projectile crossings.

### Mưa Rào field

Remove the opaque beam/disc composition that reads like a UFO. The skill remains the same AOE with the same radius, duration, slow/conduction behavior, damage, tick cadence, and cost.

- Use one instanced raindrop mesh with deterministic horizontal positions, fall phases, and speeds.
- Drops fall vertically from several world units above the field to the ground and loop for the field duration.
- A subtle wet ground stain, perimeter ripple, and three timed ground ripples communicate the AOE without filling it with light.
- Damage ticks add a brief splash/ripple cue while preserving enemy readability.
- Reduced-motion mode lowers drop count and disables unnecessary secondary motion, but keeps the damaging area visible.
- Runtime update must remain allocation-light and use simulation/elapsed time rather than wall-clock randomness.

### Frog victory journey and level transition

Completing the final wave enters a dedicated `victoryTravel` game phase rather than immediately opening the result modal.

1. Disable wave/build/link/skill interaction and clear transient combat fields.
2. Detach only the frog actor from the stationary Nexus pedestal.
3. Move the frog from the Nexus to the path endpoint, then backward over the exact enemy path to the spawn entrance.
4. Animate short deterministic hop arcs, face the current travel direction, and add readable takeoff/landing squash plus small dust/ripple landings.
5. On arrival, run a short full-screen drought-color fade.
6. Level 1 navigates to `?level=2`; Level 2 navigates to `?level=3` while preserving the current pathname; Level 3 shows the existing final victory result after the journey.

Reset/retry must reattach the frog actor to its Nexus, restore its transform and visibility, clear the fade, and remove any victory particles. Navigation must occur only once.

## Initialization regression

Keep the camera-matrix fix from `TutorialHandInitialization_Technical_Specification.md`: after camera position and `lookAt`, call `camera.updateMatrixWorld(true)` before tutorial world-to-screen projection. Do not add a fixed loading delay because no asynchronous asset blocks this path.

## Diagnostics and deterministic test hooks

Expose enough state to prove behavior without relying on timing guesses:

- elemental model variant names/count;
- path visual layer and deterministic trail-detail counts;
- active rain field drop count, vertical span, and ripple count;
- victory travel active/progress/hop height/current position/destination stage/navigation state;
- existing renderer counts and gameplay metrics remain intact.

Add a deterministic `victory-travel` test state. Extend `advance(seconds)` so it advances both wave simulation and the victory sequence. Tests may suppress only the final browser navigation when they need to inspect arrival; a separate browser test must prove the real `level=1 -> level=2` route.

## Technical-art budget

- Desktop: at most 300 draw calls, 750k triangles, 300 geometries, 60 textures, DPR cap 1.8.
- Mobile: target at most 150 draw calls, 300k triangles, 200 geometries, 40 textures, DPR cap 1.35.
- The rain field uses instancing; trail detail uses merged or instanced geometry; tower details share materials.
- No LOD. The project deliberately uses authored low-detail forms at all supported camera distances.
- Shadows remain limited to the existing light and bounded caster policy.

## Verification

- `npm run build`.
- Cold-load tutorial-hand Playwright test on desktop and mobile.
- Targeted tests for tower model identities, unchanged path data, rain-drop animation/damage, victory hop progress, reset cleanup, and Level 1 to Level 2 navigation.
- Existing gameplay, tutorial, Stage 2 lesson, visual, bot, and regression suites.
- Production preview on desktop and mobile with clean console/page/network output.
- Canvas inspection and renderer diagnostics in an active combat/rain state and a victory-travel state.
- Update intentional visual baselines only after inspection.
- Deploy the production build to the existing Projectile Network TD drought Vercel project and verify root plus `?level=2` and `?level=3`.
- Add a dated six-field entry to `Documents/AICollaboration/AI_Collaboration_Log_Prototype_17_08.md` with session ID `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`.

## Implementation and verification result

Completed on 2026-08-17. The four elemental tower variants, raised packed-earth trail, instanced falling-rain field, and reverse-path frog victory journey are implemented without changing the approved gameplay path, tower footprints, wave balance, or Mưa Rào damage contract. The full Playwright matrix passed `67` tests with `9` intentional viewport skips, the production build passed, and deterministic desktop/mobile canvas inspections were nonblank, error-free, hardware-rendered, and within budget. Vercel deployment `dpl_7Eub542kho8yoFt3cijKUgC7yZpg` reached `READY`; the stable root, all three level routes, and hashed JavaScript/CSS assets returned HTTP `200`.
