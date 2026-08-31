# Link Tower Drag Gesture — Technical Specification

Status: Implemented and verified  
Issue: `TowerDefense3D-4s6`  
Scope: `Documents/Prototype/Arcane-Arsenal-Link` only

## Interaction contract

1. A normal click or tap selects a tower and remains an inspection action.
2. Pressing the already-selected ammo-emitting tower arms a link gesture.
3. Moving beyond the existing seven-pixel drag threshold enters transient `link` mode, disables camera controls, and highlights every receiver accepted by the authoritative `validateLink` rules.
4. Releasing over a valid highlighted receiver calls the existing link transaction and exits link mode.
5. Releasing over empty space or an invalid receiver exits link mode without changing topology.
6. The Link inspector button and `L` shortcut are removed. Rotation controls and the Rotation prototype remain unchanged.

Desktop mouse and mobile touch use the same pointer-event state machine. Mobile release may use the existing coarse-pointer proximity tolerance.

## Tutorial contract

The text-free hand animation runs from the selected source tower to its intended receiver. Stage 1 keeps its authored progression and wave gates, but the former button-activation step is completed when the source drag actually crosses the threshold. The Level 2 Nổ feeder lesson demonstrates the same Foundry-to-Nổ drag.

## Verification

- TypeScript/Vite production build and dependency audit.
- Real mouse and emulated touchscreen: select source, drag to target, observe valid highlights, release, and assert the exact link and zero facing error.
- Invalid and cancelled drags do not mutate links.
- Existing routing, reciprocal-link, completed-relay, terminal-buffer, gameplay-bot, onboarding, and visual-regression coverage remains green.
- Vercel production deploy, hashed-asset HTTP smoke, and live desktop/mobile gesture smoke.

## Result

Implemented in the Link prototype and deployed as `dpl_3gg2EGX7fXHGM9Q9xzYUHqcYsxT7`. Desktop mouse and mobile Chrome touch probes each exposed the exact valid highlighted receiver and created one link on release with zero facing error. The mobile inspector was narrowed and dynamically docked away from the selected source after live testing found the original panel could cover its touch point. Build, audit, gameplay, onboarding, bot, and visual verification passed; one intentionally changed mobile tower-detail baseline was visually reviewed and regenerated.
