# Tutorial Hand Initialization Technical Specification

Status: Approved  
Approval basis: Owner reported the first-load tutorial-hand misalignment and authorized a loading treatment if initialization was incomplete.  
Date: 2026-08-17  
Responsible session: `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`  
Tracking issue: `TowerDefense3D-c4c`  
Target: `Documents/Prototype/Projectile-Network-TD/`

## Problem

On a cold web load, the icon-only tutorial hand becomes visible for one frame with its placement endpoint clamped near the viewport edge, then points to the correct authored grid cell on the following frame.

Measured desktop cold-load sequence:

1. Hand hidden; canvas drawing buffer remains the browser default `300 x 150`.
2. Canvas drawing buffer becomes `1280 x 720`; hand becomes visible with endpoint `x = 26`, `y = 662`.
3. Next animation frame; endpoint becomes the stable authored projection near `x = 543`, `y = 428`.

Mobile reproduces the same sequence: the first visible endpoint is clamped to `x = 26`, then stabilizes near `x = 106`, `y = 395`.

## Root cause

`Game.update()` resizes the renderer and calculates the tutorial endpoint before the first `renderer.render()` call. `updateCamera()` sets camera position and calls `lookAt()`, but does not explicitly update the camera world matrix. Three.js normally updates that matrix during rendering. Therefore the first `worldToClient()` call uses a stale camera world/inverse matrix; the next frame is correct because the first render has completed.

The game has no asynchronous model or texture load on this path. A loading screen would mask one incorrect frame without fixing the projection owner.

## Approved fix

- Update the camera world matrix immediately after every `updateCamera()` transform change.
- Keep the tutorial hand hidden until its normal first update; do not add an arbitrary timeout.
- Do not add a loading screen for this synchronous one-frame matrix race.
- Add deterministic desktop and mobile cold-load tests that observe every animation frame from document creation and assert:
  - the hand is hidden while the canvas still has its default buffer;
  - the first visible hand frame already uses the stable authored endpoint;
  - the endpoint does not jump materially during subsequent frames;
  - console and page errors remain empty.
- Preserve the text-free tutorial, animation timing, authored placement cell, responsive UI, and all gameplay behavior.

## Verification

1. Run the new cold-load test on desktop and mobile.
2. Run the production build.
3. Run the full Playwright suite and visual regression suite.
4. Capture or log the first visible frame endpoint before and after the fix.
5. Deploy the existing Vercel project and verify the stable URL and hashed assets.

## Implementation result

Implemented on 2026-08-17. `updateCamera()` now commits the camera world matrix immediately after `lookAt()`, before any tutorial world-to-screen projection. A cold-load Playwright regression samples the first visible hand frame on desktop and mobile and compares it with the stable authored endpoint. The targeted initialization suite and the full Playwright matrix passed; no loading overlay or fixed delay was introduced because the path has no asynchronous asset dependency.
