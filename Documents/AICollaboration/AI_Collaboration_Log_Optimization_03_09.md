# AI Collaboration Log — Rendering and Runtime Cost — 03/09/2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Area:** Baked lighting, mobile render pipeline budgets, post-processing cost, runtime UI layout
  cost, and frame-rate instrumentation
- **Responsible Claude Code session:** current local session
- **Scene used for every measurement:** `Assets/Scenes/Levels/Level_001.unity`

This record collects the optimization work and evidence from one session. The through-line is that
most of the session's conclusions were reversals of earlier assumptions, and each reversal came from
a measurement rather than from reasoning. The counts below were read out of the live Editor, not
estimated.

## Entry 1 — Undoing the Level 1 bake, and why the scene stayed darker afterwards

### Problem being addressed

The project owner had baked lighting into Level 1 and wanted the scene to stop using it. Removing the
bake through the API did not work: neither `Lightmapping.ClearLightingDataAsset()` nor writing
`m_LightingDataAsset` through `SerializedObject` cleared the reference. After a partial revert the
scene was still visibly darker than it had been before the bake.

### Prompt used

The project owner asked to make Level 1 stop using baked lighting, then reported that the scene was
still darker than it used to be and had not returned to its previous state.

### Important AI response

The darkening is not a setting that was left on. It is the mechanism: **a lightmapped renderer samples
the lightmap instead of the ambient spherical-harmonics probe.** Lightmap indirect light is occluded
and computed, so it is smaller than the flat, unoccluded skybox ambient the same object was getting
before. That darkens shadowed *and* lit areas at once, which is why the scene reads as uniformly
dimmer rather than as "shadows got stronger". Turning the bake off is therefore not a toggle — the
baked data has to be gone.

### Option selected, revised, or rejected

- **Selected:** `git checkout` of the whole scene file, then reopen the scene in the Editor.
- **Rejected:** `Lightmapping.ClearLightingDataAsset()` — did not clear the reference.
- **Rejected:** clearing `m_LightingDataAsset` through `SerializedObject` — same result.

### Rationale

The scene file is the authority on which lighting data it points at, and it was already committed in
a known-good state. Reverting the file is exact where the API calls were not.

### Implementation or verification result

- Level 1 was restored bit-for-bit to its pre-bake commit.
- One statement made during this entry was wrong and is recorded so it is not repeated: **"Receive GI
  = Light Probes" and "remove Contribute GI" are not equivalent.** Receive GI = Light Probes keeps the
  object contributing bounce light, so neighbours do not change. Removing Contribute GI stops its
  bounce and *does* change nearby objects.

## Entry 2 — Choosing a bake mode that preserves the current look

### Problem being addressed

The owner then wanted the opposite: bake the static geometry for cost, have dynamic objects read
probes, and change the visuals as little as possible.

### Prompt used

The project owner asked how to bake lighting without changing the visuals, with static objects baked
for optimization and dynamic objects reading from probes, then asked for a deeper comparison of the
options, then how to fix the dark shadows and darker scene that one of them produced, and separately
what Ambient Occlusion is and what turning it on or off affects.

### Important AI response

The four Mixed Lighting modes differ in what they move off the realtime path:

- **Baked Indirect** — bakes bounce only; direct shadows stay fully realtime, so it saves no shadow
  work.
- **Shadowmask** — bakes static-to-static direct shadow into a mask texture and keeps near shadows
  realtime. Saves shadow-pass work only for casters that are themselves lightmapped static.
- **Distance Shadowmask** — the same, with the realtime/baked crossover pushed out to the shadow
  distance.
- **Subtractive** — bakes shadow flat into the lightmap and gives up realtime shadow shaping.

Ambient Occlusion is contact darkening in creases and where surfaces meet, computed from geometry
during the bake and written into the lightmap. At runtime it is free, but it is not neutral: it visibly
darkens the scene, which is exactly what this bake was supposed to avoid.

### Option selected, revised, or rejected

Settings landed in `Assets/Config/Rendering/New Lighting Settings.lighting`:

| Setting | Value | Why |
|---|---|---|
| `m_MixedBakeMode` | `2` (Shadowmask) | keeps near shadow realtime, so shape is preserved |
| `m_LightmapsBakeMode` | `0` (Non-Directional) | half the texture; no shader in the project reads directionality |
| `m_AO` / `m_ExtractAO` | `0` / `0` | AO changes the look, and preserving the look was the requirement |
| `m_IndirectOutputScale` | `1.5` | compensates the ambient the lightmap replaced (Entry 1) |
| `m_AlbedoBoost` | `1` | left neutral |
| `m_BakeResolution` | `14` px/unit | low enough for mobile texture budget |
| `m_LightmapMaxSize` | `1024` | caps atlas pages |
| `m_PVRBounces` / `m_PVRSampleCount` | `2` / `512` | bake-time quality only, no runtime cost |

- **Rejected:** Subtractive — bakes shadow flat and loses realtime shadow shaping.
- **Rejected:** Directional lightmaps — double the texture for information nothing samples.

### Rationale

Shadowmask is the only mode that can retire shadow-pass work while leaving near-camera shadows looking
the way they already did. Raising indirect scale to 1.5 is the direct counter to the mechanism in
Entry 1 rather than a taste adjustment.

### Implementation or verification result

- Levels 001, 002 and 003 all reference the same lighting settings asset
  (`guid: d198a75c3ff3b644b88a93ba18c0e1dc`), so the owner's question about sharing one lighting
  settings asset across levels with only the baking set differing is already true structurally.
- A diagnostic error worth recording: when the first bake still came out dark, Subtractive mode and AO
  were both blamed. Reading the owner's actual `.lighting` file disproved both — it was already
  Shadowmask with AO off. The real causes were 24 `m_CastShadows: 1` overrides plus the
  lightmap-replaces-ambient mechanism from Entry 1. Two hypotheses, both wrong, both cheap to check
  against the file.

## Entry 3 — Cutting the mobile render pipeline budgets

### Problem being addressed

The active pipeline for builds is `Mobile_RPAsset`, while the project default was `PC_RPAsset` — so
mobile-relevant settings needed checking on the asset that actually ships.

### Prompt used

The project owner noted that the default render pipeline is `PC_RPAsset` while the current one is
`Mobile_RPAsset`, asked for that to be checked, and later asked what else could be optimized given the
bake had not changed the visuals much.

### Option selected, revised, or rejected

Applied in `Assets/Config/Rendering/Mobile_RPAsset.asset`, verified by reading the file back:

| Setting | Value | Effect |
|---|---|---|
| `m_MainLightShadowmapResolution` | `2048` → `1024` | quarter of the shadowmap memory and fill |
| `m_ShadowDistance` | `50` → `38.7` | fewer casters in the shadow pass per frame |
| `m_SoftShadowQuality` | `2` → `1` | cheaper shadow filter taps |
| `m_AdditionalLightsRenderingMode` | `1` → `0` (Disabled) | removes the additional-light loop entirely |
| `m_ShadowCascadeCount` | `1` | one cascade for a fixed-camera board |
| `m_RenderScale` | `0.8` | 64% of the pixels shaded |
| `m_MSAA` | `1` (off) | no resolve cost |
| `m_RequireDepthTexture` / `m_RequireOpaqueTexture` | `0` / `0` | no extra full-screen copies |
| `m_UseSRPBatcher` | `1` | on |
| `m_ColorGradingMode` | `0` (LDR) | cheaper grading path |
| `m_LightProbeSystem` | `1` (APV) | see Entry 7 — this one turned out not to pay off |

### Rationale

A fixed-camera tower defence board does not need cascades, per-pixel additional lights, or a 2048
shadowmap; those are the largest per-frame wins available without touching content. Disabling
additional lights is the biggest single cut, because it removes work per fragment rather than per
frame.

### Implementation or verification result

All values above were confirmed by reading `Mobile_RPAsset.asset` back after the edits. The APV
setting is the one item in this table that later measurement showed to be a net cost — Entry 7.

## Entry 4 — Reducing bloom cost

### Problem being addressed

Bloom on the post-processing profile ran at full resolution with the default iteration count.

### Option selected, revised, or rejected

In `Assets/Config/Rendering/SampleSceneProfile.asset`:

- **Selected:** `downscale` overridden to `1` (Quarter) — the blur chain starts at a quarter
  resolution.
- **Selected:** `maxIterations` overridden to `4` — caps the mip chain.
- **Selected:** `highQualityFiltering` left un-overridden (`m_OverrideState: 0`, value `0`) — off.
- Intensity `1.37` and threshold `0.72` kept as authored.

### Rationale

Bloom is bandwidth-bound on mobile, and its blur is the part that scales with resolution. Quarter
downscale plus a capped chain keeps the visible glow while removing most of the sampling.

## Entry 5 — Measuring what the bake actually reaches

### Problem being addressed

The owner asked why the road tiles receive no baked shadow, and whether the baked shadow on screen is
only saving shadow-pass calls. Answering that needed a shader-by-shader audit rather than an opinion,
because baked lighting only works if the shader sampling the surface asks for it.

### Prompt used

The project owner asked why the paths cannot read the shadow texture or are not baked correctly, then
observed that most of the project's shaders read neither lightmap nor shadowmask and asked whether the
baked shadow showing on screen is therefore only reducing shadow-pass calls.

### Important AI response

Read the shaders. `Assets/Shaders/RoadShadowOverlay.shader` declares
`struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; }` — **no lightmap UV in
`TEXCOORD1`** — compiles only the `_MAIN_LIGHT_SHADOWS*` keywords, and calls the realtime-only
overload `GetMainLight(shadowCoord)`. It has a single `UniversalForward` pass, `FallBack Off`, and
**no ShadowCaster pass at all**. `Assets/Art/TheVayuputra/ToonShader/Shader/ToonShader.shader` calls
`MainLightRealtimeShadow(shadowCoord)` and `SampleSH(N)`, with no lightmap or shadowmask keywords.
Neither can consume a bake.

### Implementation or verification result

Measured in the live Editor across all 229 renderers in Level 1:

| Shader | Renderers | Reads lightmap | Reads shadowmask | Reads `SampleSH` |
|---|---|---|---|---|
| `FrogGod/Road Shadow Overlay` | 140 | no | no | no |
| `TheVayuputra/ToonShader` | 56 (41 mesh + 15 skinned) | no | no | **yes** |
| `Universal Render Pipeline/Unlit` | 29 | no | no | no |
| `Universal Render Pipeline/Lit` | 4 (2 mesh + 2 skinned) | **yes** | **yes** | **yes** |

What the bake produces, and who consumes it:

| Bake product | Consumers | Count |
|---|---|---|
| Lightmap colour (1024² + 512²) | URP/Lit only | **2** — although **183 of 229** renderers occupy lightmap space |
| Shadowmask (1024² + 512²) | no shader in the project | **0** |
| Shadow-pass saving | casters that are lightmapped | **0 of 45 casters** |
| Reflection probe | URP/Lit | 0 probes exist in the scene |

Conclusion: **the bake is technically correct and reaches 2 of 229 renderers.** 181 renderers pay for
lightmap atlas space and bake time while their shaders have neither the UV channel nor the keywords to
read it. Shadowmask retires zero draw calls because no caster is lightmapped. On-screen shadows are
therefore 100% realtime, and the answer to the owner's question is that the bake is not even saving
shadow-pass calls. Cost currently carried for that: four textures (1024² colour + 1024² mask, 512²
colour + 512² mask) plus bake time.

A separate consequence of `RoadShadowOverlay` having no ShadowCaster pass: its 140 renderers **cannot
cast a shadow at all**, whatever their Cast Shadows checkbox says.

## Entry 6 — Why disabling Cast Shadows can leave the shadow on screen

### Problem being addressed

With on-screen shadows established as realtime, the owner reported that turning off Cast Shadows on a
mesh renderer did not remove the shadow they were looking at.

### Prompt used

The project owner asked why, if the current shadows are realtime, a shadow still appears after
disabling shadow casting on the mesh renderer.

### Important AI response

Disabling Cast Shadows removes the object from the realtime shadow map only. It does not remove: (1)
shadow already baked into a lightmap or shadowmask, which needs a re-bake; (2) shadow painted into the
texture, which is art; (3) a fake shadow object such as a dim quad or decal; (4) a shadow cast by a
*different* object than the one toggled.

### Implementation or verification result

A prior caster count given in this session was wrong and the correction matters: it covered only
`MeshRenderer` and reported **28 casters**. Auditing every `Renderer` type found:

```
MeshRenderer         212 total /  28 casting
SkinnedMeshRenderer   17 total /  17 casting   <- missed by the earlier count
                                  --
casters overall                   45
ShadowsOnly renderers              0
renderer Off but a descendant still casts   0
casters that are also lightmapped           0
```

So the real caster count is **45**, not 28, and 17 of them are animated characters that the earlier
figure omitted entirely.

That data rules out three of the four causes for this scene: cause (1) is impossible (0 lightmapped
casters and 0 shadowmask readers), and the `ShadowsOnly` and parent-off/child-still-casting cases are
both zero. What remains is a shadow painted into the art, or a shadow belonging to another object.
Supporting evidence for the former: the project contains two shaders whose names and properties exist
precisely to draw shadow-looking art — `FrogGod/Road Shadow Overlay` and `FrogGod/Grass Shadow Cutout`
(`Assets/Shaders/TransparentShadowOverlay.shader`), both carrying a `_ShadowStrength` property.

Diagnostic handed to the owner: switch the Directional Light off in the Scene view. Whatever darkness
remains is not a shadow, and no renderer setting will remove it.

## Entry 7 — Probes: why APV is currently a cost with no benefit

### Problem being addressed

The owner asked how to set up probes for dynamic objects on an already-baked scene, showed an Adaptive
Probe Volumes configuration for review, and asked whether the setup was in the right place.

### Prompt used

The project owner asked to be shown how to set up probes without any tooling, using Unity's built-in
support, then asked whether the shown settings were correct and whether the APV setup was in the right
place.

### Important AI response

The distinction that decides this: **Light Probe Groups populate the per-renderer `unity_SH*`
constants, which is what a raw `SampleSH(N)` call reads. Adaptive Probe Volumes do not** — APV requires
the shader to sample APV explicitly, and does not route through `unity_SH*`. A shader written against
`SampleSH` therefore gains nothing from APV while the volume still consumes its memory budget.

### Implementation or verification result

Measured:

```
renderers reading SampleSH:  56   (41 MeshRenderer + 15 SkinnedMeshRenderer, all ToonShader)
their Light Probes setting:  BlendProbes on all 56
Light Probe Group data:      0 probes
Ambient mode:                Skybox
Reflection probes in scene:  0
APV budget reserved:         m_ProbeVolumeMemoryBudget 512 + blending 256, SH bands L1
```

All 56 renderers are asking for probe data (`BlendProbes`), no probe data exists, so every one of them
falls back to a **single flat skybox ambient colour**. A character walking into shade stays as bright
as one standing in the open. This is simultaneously the project's clearest visual shortcoming from
lighting and the one place where baking would pay real dividends, since probe data is an SH array
rather than a texture.

Three options were put to the owner; **none is applied yet, the choice is theirs:**

- **A (recommended)** — set Light Probe System back to Light Probe Groups, place a probe group, set the
  183 lightmapped renderers to Receive GI = Light Probes, re-bake. Lightmaps shrink to near nothing,
  probe data becomes real, and the 56 `SampleSH` renderers gain position-dependent ambient.
- **B** — drop baking entirely: Baked GI off, delete the lighting data. Two renderers change on
  screen; four textures and the bake time come back.
- **C** — teach `RoadShadowOverlay` to sample the shadowmask. This is the only change that makes the
  bake meaningful for the 140 road tiles and could cut the 45-caster shadow pass down to animated
  characters. It needs a lightmap UV added to `Attributes` plus the `LIGHTMAP_ON`,
  `SHADOWS_SHADOWMASK` and `LIGHTMAP_SHADOW_MIXING` keywords, and the built-in Quad mesh has
  `uv2Count = 0`, which needs handling. Largest effort, largest performance upside.

## Entry 8 — Removing runtime layout work from the journey backdrop

### Problem being addressed

The journey backdrop is built from painted bands, each tiled sideways so the parallax has room to
slide. On a canvas of a different width the tiles cracked apart and stretched vertically. A runtime
layout component was driving their sizes.

### Prompt used

The project owner asked for the UI prefab to be fixed so the assembled pieces neither crack nor stretch
vertically on other screen widths, with the sky still allowed to stretch, then said that adjusting in
the scene was sufficient and that the script should be turned off, and finally confirmed only the
parallax script should remain.

### Option selected, revised, or rejected

- **Selected:** bake the layout into the prefab — each band sized to `height × sprite native aspect`,
  each tile offset an exact whole multiple of the band width, alternate tiles mirrored 180° about Y.
- **Selected:** delete `JourneyBackdropBandView`, `BackdropBandLayout`,
  `JourneyBackdropBandLayoutTool` and `BackdropBandLayoutTests`.
- **Selected:** keep only `JourneyParallaxView` and `ParallaxDepthRamp`, and remove the width warning
  the former used to log.
- **Rejected:** keeping a component that recomputes band geometry, since the geometry never changes
  after authoring.

### Rationale

Layout that is fixed at author time should cost nothing at run time. Authored values also cannot drift
on a device aspect nobody tested, which a computed layout can.

### Implementation or verification result

- `JourneyBackdropBandPlayModeTests` guards the authored numbers at canvas shapes 1920×1080,
  1663×1247 and 2400×1080: drawn aspect within 0.01 of native aspect, tile size equal to band size,
  tile offset a whole multiple of band width, and every odd-numbered tile rotated 180° about Y.
- Three bugs in the deleted driver are recorded because they generalize: `LateUpdate` is the wrong
  layout driver in the Editor (Edit Mode only ticks on repaint, so values went stale); a stale-guard
  that wrote one field and read another meant tiles were never laid out when Unity sent the rect
  message before `OnEnable`; and `Mathf.Approximately` scales its tolerance to magnitude and is far too
  loose at canvas sizes, so comparisons moved to an absolute 0.01.
- Two rounds of duplicate objects were cleaned up during the prefab work — 9 duplicated layers, then 7
  duplicated tiles created by `ApplyPrefabInstance` pushing added GameObjects into the prefab without
  removing the instance copies.

## Entry 9 — Frame-rate instrumentation

### Problem being addressed

Every cost claim in the entries above is a claim about frames, and the project had no on-screen frame
rate to check any of them against on a device.

### Prompt used

The project owner asked for an FPS display in the bottom-left corner, visible throughout the game from
the level menu and bootstrap through to gameplay.

### Option selected, revised, or rejected

- **Selected:** `Assets/Scripts/System/FramePacing/FrameRateSampler.cs` — window-averaged sampling;
  `Add(deltaSeconds)` rejects values `<= 0`, NaN and Infinity; `TryTakeAverage(out float)` returns and
  resets once the window has elapsed.
- **Selected:** `Assets/Scripts/System/FramePacing/FrameRateHealthScale.cs` — `Poor`/`Fair`/`Good`
  graded at 0.6 and 0.9 of `FramePacingSystem.TargetFrameRate` (60).
- **Selected:** `Assets/Scripts/Components/FramePacing/FpsCounterView.cs` — reads
  `Time.unscaledDeltaTime`, so a paused game still reports, and tints the label by health.
- **Selected:** authored by `Tools/Tower Defense/Rebuild FPS Counter` at `Safe Area/Fps Counter`,
  position `(14, 10)`, size 200×28, last sibling, Text plus `Outline` and no backing panel.

### Rationale

Unscaled delta time is the right clock for a diagnostic, because a counter that freezes with the
simulation cannot tell you why the simulation froze. Grading against the pacing system's own target
rather than a hard-coded 60 keeps the readout honest if the target changes.

### Implementation or verification result

The counter is authored and live. One defect is outstanding: it must draw above the gameplay HUD, and
an earlier attempt left the level-menu backdrop covering the HUD because `menu.Hide()` was not called.

## Open items

- **Owner's decision pending** on Entry 7 options A, B or C. Nothing in that entry has been applied.
- If A is chosen: switch `m_LightProbeSystem` back to Light Probe Groups, set the 183 lightmapped
  renderers to Receive GI = Light Probes, then re-measure lightmap size and probe count to confirm.
- If C is chosen: verify by rendering before and after and diffing pixels, since the whole point is
  that the visual must not change.
- Levels 002–010 have not had Mixed lighting or the 1.5 bounce scale applied; they already share the
  lighting settings asset, so only their baking sets differ.
- The FPS counter's draw order over the gameplay HUD is unresolved.
- Both test suites need a re-run after the four backdrop-band files were deleted. Last measured before
  deletion: EditMode 282/282, PlayMode 31/31.
