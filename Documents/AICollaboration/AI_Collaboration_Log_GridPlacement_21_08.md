# AI Collaboration Log — GridPlacement — 21 August 2026

## Entry 1 — Preserve cast shadows across the road overlay at distant camera views

**Responsible session:** `01a0135e-7cb3-7490-827f-cf7d34d7b651`

### Problem being addressed

The transparent road artwork rendered over the opaque terrain and visually covered the shadow cast by FrogStand. The loss was most noticeable when the camera moved farther away, even though the terrain outside the road still showed the shadow.

### Prompt used

The project owner supplied a Unity screenshot, reported that the road covered the shadow only at distant camera views, and asked the AI to fix the rendering defect and record the result in the AI collaboration log.

### Important AI response

Live Unity inspection found that all straight, corner, T-junction, and cross road renderers share the original `Assets/Resources/Materials/Path.mat`. That material used URP Lit in Transparent queue `3000` with `ZWrite` disabled. An A/B render proved that changing the road renderers' `receiveShadows` flag changed zero pixels, so enabling that flag would have been dead configuration rather than a fix. The same test showed that the transparent pass did not expose usable main-light shadow attenuation. The accepted shader therefore renders the road as a shadow-aware alpha-tested overlay: it keeps the original texture, tint, tiling, material asset, and real alpha mask; uses a stable shared material instead of material instances; writes depth; converts partial alpha into screen-space coverage; and applies main-light shadow attenuation before output.

### Option selected, revised, or rejected

- **Selected:** add the project-owned `FrogGod/Road Shadow Overlay` shader and assign it to the existing shared `Path.mat`.
- **Selected:** keep one original material reference across every road prefab and preserve the texture scale `(0.4, 1.0)` and offset `(0, 0)`.
- **Selected:** use alpha-test queue `2450`, minimum alpha `0.02`, dithered partial-alpha coverage, and shadow strength `0.65` so the road keeps its fade while shadows remain readable.
- **Rejected:** enabling `MeshRenderer.receiveShadows`, because measured output was identical with the flag off and on.
- **Rejected:** enabling URP Opaque Texture or adding a renderer feature, because the mobile renderer currently has neither enabled and the extra full-screen texture cost was unnecessary for this local road fix.
- **Rejected:** material cloning or per-renderer material instances.

### Rationale

Transparent alpha blending occurs after the opaque terrain and can paint a bright road color over an already-shadowed ground pixel. Moving the road to a depth-writing alpha-tested pass lets it participate in the main-light shadow path. Dithered coverage preserves the intentionally soft transparent edge without requiring MSAA, which is disabled in the current mobile URP asset. Applying the correction in the single shared material fixes every road topology consistently and avoids redundant prefab overrides.

### Implementation or verification result

`Assets/Shaders/RoadShadowOverlay.shader` was created with one mobile-compatible URP pass, and `Assets/Resources/Materials/Path.mat` now references it. The shader compiled successfully, is supported by the current `Mobile_RPAsset`, and reported zero compiler messages. A render comparison between shadow strength `0` and `1` changed 22,580 of 518,400 pixels, confirming that road pixels now respond to the main-light shadow map; the previous transparent implementation and the renderer-flag experiment both changed zero pixels. Unity camera captures verified the FrogStand shadow on the road at the authored camera position and again after moving the test camera 15 world units farther away, then restoring the exact original camera transform. All 103 scene road renderers still use the same original `Path.mat`, the four road prefab assets have no retained renderer override from the rejected experiment, the active `Level_001` scene remains clean, and the Unity Console contains zero errors. Bead `TowerDefense3D-bac1` tracks this fix.
