# AI Collaboration Log — ToonShader — 19 August 2026

## Entry 1 — Stop the outline pass from costing GPU work at Stroke Width 0, then split outline support into a separate shader

**Responsible session:** `e5befab6-5736-430f-acde-e046d61eda52`

### Problem being addressed

The imported vendor toon shader (`Assets/TheVayuputra/ToonShader/Shader/ToonShader.shader`, `TheVayuputra/ToonShader`) draws its outline as an unconditional second `Pass` (`Name "OutlinePass"`, `Tags { "LightMode"="SRPDefaultUnlit" }`) on every material. At `_StrokeWidth = 0` the outline geometry collapses to the same position as the base mesh, so it should be invisible, but the project owner found it was still costing a real draw call every frame — and, on further inspection, was in some cases still visibly rendering a white silhouette (most obviously on the `SpikeBush` prop, whose thin double-sided leaf geometry made the coincident-depth outline shell z-fight through). All 11 of the project's own visual materials under `Assets/Resources/Models/` had `_StrokeWidth: 0`, so this affected essentially every environment prop in the game.

### Prompt used

The project owner asked for the outline to be fully hidden and free of draw-call cost when Stroke is 0. After an initial fix, they reported (with a screenshot) that a white outline was still visibly rendering around `SpikeBush` despite Stroke Width showing 0 in the Inspector. After a second, shader-level fix resolved that, the project owner then asked to go further: strip outline support out of the shader every visual currently uses, and keep a separate copy of the shader with full outline support for future use.

### Important AI response

The first fix attempt used `Material.SetShaderPassEnabled("OutlinePass", false)` — Unity's documented API for disabling a named pass — driven by a new `ToonShaderGUI : ShaderGUI` (`Assets/TheVayuputra/ToonShader/Editor/ToonShaderGUI.cs`) that keeps the flag in sync with `_StrokeWidth` whenever a material is edited, plus a one-time script pass that applied it to all 21 existing materials using the shader. This was verified to persist correctly in each `.mat`'s serialized `disabledShaderPasses` list. Despite that, the project owner's screenshot proved the outline was still rendering — `SetShaderPassEnabled` does not reliably suppress a pass tagged `LightMode="SRPDefaultUnlit"`, since URP treats that tag as an automatic fallback-rendering path for shaders without a recognized URP `LightMode`, and that path does not appear to honor the per-material pass-enable flag. This was confirmed empirically (not assumed) by re-reading the live material's `GetShaderPassEnabled` state — it was correctly `false` — while the Scene View screenshot still showed the white outline.

The actual fix was made in the shader itself: the outline pass's vertex shader now sets `o.pos = float4(0, 0, 0, 0)` when `_StrokeWidth <= 0.0001`, which clips the triangle to a single point before rasterization (a well-established "cull via w=0" technique), guaranteeing zero fragments regardless of whether the SRP honors the pass-enable flag. This was verified visually via `Unity_SceneView_CaptureMultiAngleSceneView` focused on the `SpikeBush` GameObject in `Level_001`, before and after — the white outline was confirmed present before the shader fix and fully gone after, from all four captured angles.

Finally, at the project owner's request to remove outline capability from the shader every visual uses, the AI split the shader in two: the original `TheVayuputra/ToonShader` (same name and file, so every existing material keeps referencing it with no per-material rework needed) had its `OutlinePass` and `_StrokeWidth`/`_StrokeColor` properties removed entirely; a new file, `Assets/TheVayuputra/ToonShader/Shader/ToonShaderOutline.shader` (`TheVayuputra/ToonShader Outline`), keeps the original content including the outline pass and the w=0 zero-width fix. Because stripping the property from the base shader makes `Material.HasProperty`/`GetFloat` unable to read it any more (they query the currently assigned shader's declared properties, not the raw serialized data), the AI read each material's still-present serialized `_StrokeWidth` value directly via `SerializedObject.FindProperty("m_SavedProperties.m_Floats")` to correctly identify which materials actually needed outline support before reassigning shaders — avoiding a naive check that would have silently found zero matches (which is exactly what happened on the first, naive attempt, caught before being reported as done).

### Option selected, revised, or rejected

- **Selected:** shader-level vertex clipping (`o.pos = float4(0,0,0,0)`) as the mechanism that reliably hides the outline and minimizes its GPU cost, rather than relying solely on `Material.SetShaderPassEnabled`.
- **Rejected:** keeping `SetShaderPassEnabled` as the sole/primary fix — it was empirically proven insufficient for this shader's `SRPDefaultUnlit`-tagged pass in this project's URP setup. The `ToonShaderGUI`/pass-toggle mechanism was left in place (harmless) but is no longer relied upon as the correctness guarantee.
- **Selected:** split into two shader assets — the original name/file kept as the no-outline shader (zero rework for the 11 project materials and the 2 vendor demo materials already at Stroke Width 0, since they already reference it), and a new `ToonShaderOutline.shader` copy kept the outline pass intact for the 8 vendor demo materials that actually use a nonzero Stroke Width, which were reassigned to it with their Stroke Width value preserved.
- **Rejected:** deleting outline support outright — the project owner explicitly wants it kept available as a separate shader for future use, not removed from the project.

### Rationale

A shader-level fix that doesn't depend on whether the current SRP honors a particular API is the only reliable way to guarantee "Stroke 0 means fully hidden" across Scene View, Game View, and builds alike. Keeping the base shader's name and file identical avoided having to touch any of the 11 real visual materials at all — they were already all Stroke Width 0, so the fix is exactly a no-op for them, matching the project owner's "all shaders in visuals now use the non-outline shader" requirement with zero risk of misassigning them. Splitting outline into a separate shader (rather than, say, a shader keyword toggle) keeps both shaders simple, keeps the vendor demo materials that actually demonstrate the outline effect working unchanged, and gives the project a clearly-named path (`TheVayuputra/ToonShader Outline`) to opt back into outline rendering later without re-adding complexity to the shader every prop currently uses.

### Implementation or verification result

Files changed: `Assets/TheVayuputra/ToonShader/Shader/ToonShader.shader` (outline pass and Stroke properties removed), `Assets/TheVayuputra/ToonShader/Shader/ToonShaderOutline.shader` (new, full outline copy), `Assets/TheVayuputra/ToonShader/Editor/ToonShaderGUI.cs` (new, pass-enable sync, kept as a harmless secondary measure). No project-owned GridPlacement files were touched.

Verified live via Unity MCP against the connected `6000.3.21f1` Editor at every step: `AssetDatabase.Refresh` reported successful compilation with zero new Console errors/warnings after each shader edit. After the final split, a full material audit (`AssetDatabase.FindAssets("t:Material")` filtered by shader) confirmed exactly the intended assignment: all 11 `Assets/Resources/Models/*` materials plus `5_RimGlow.mat`/`7_CartoonFlat.mat` (Stroke Width 0) remained on `TheVayuputra/ToonShader`; the 8 vendor demo materials with nonzero Stroke Width (`1_BasicToon` 0.254, `2_Animestyle` 0.2, `3_GlossyToon` 0.12, `4_SoftToon` 0.03, `6_MetallicToon` 0.04, `8_Outline` 0.2, `9_DarkStylized` 0.06, `10_Highlight` 0.08) moved to `TheVayuputra/ToonShader Outline` with their Stroke Width preserved. A final `Unity_SceneView_CaptureMultiAngleSceneView` capture of the `SpikeBush` GameObject in `Level_001` (Iso/Front/Top/Right) showed correct rendering with no broken/pink-shader materials and no outline artifact.

Known limitation: whether `Material.SetShaderPassEnabled` actually reduces CPU-side draw-call submission counts (as opposed to GPU-side fragment cost) was not conclusively profiled in this session — no Profiler/Frame Debugger scripting was run to measure SetPass call counts before/after, since the shader-level fix was established as the correctness-critical one regardless. If true CPU-side draw-call reduction becomes a measured concern later, profiling that specifically (or moving to a LOD/second-material-slot approach) is the recommended next step.

## Entry 2 — Fix warped/blocky main-light shadows on toon-shaded props when the camera crosses a shadow cascade boundary

**Responsible session:** `e5befab6-5736-430f-acde-e046d61eda52`

### Problem being addressed

The project owner reported (with four screenshots at increasing zoom levels) that the self-shadow on `BigRock` visibly changed shape — becoming blocky/jagged and shifting — as the Scene View camera was zoomed in and out, and confirmed this also reproduces in Play Mode with the real gameplay camera, not just as an Editor-authoring artifact.

### Prompt used

The project owner asked the AI to identify what was happening and why it was a bug, then specifically asked whether the same cascade behavior affects an object using a standard `Universal Render Pipeline/Lit` material, and if not, why the toon shader is affected.

### Important AI response

Inspecting the project's URP asset live confirmed 4 shadow cascades (`shadowCascadeCount=4`, `shadowDistance=50`, splits at `(0.12, 0.29, 0.54)`, i.e. boundaries at roughly 6/14.5/27/50 world units from the rendering camera) with `_MAIN_LIGHT_SHADOWS_CASCADE` enabled and `_MAIN_LIGHT_SHADOWS_SCREEN` disabled at runtime — so shadows are resolved through the cascade path, not URP's screen-space shadow pass.

Answering the direct question (does `Lit` have this too): no, not to the same degree, because of a specific difference in how the two shaders read the shadow coordinate. `Assets/TheVayuputra/ToonShader/Shader/ToonShader.shader`'s `ForwardPass` computed `shadowCoord = TransformWorldToShadowCoord(positionWS)` **in the vertex shader** and interpolated the already-transformed shadow-space coordinate across each triangle before sampling it in the fragment shader. URP's own `Lit` shader only does this for the single-cascade, non-screen-space case; once `_MAIN_LIGHT_SHADOWS_CASCADE` is defined (as it is in this project), URP's convention is to compute the shadow coordinate **per-fragment** from the already-interpolated world position instead, because cascade selection is a function of world position and must be evaluated per pixel — a vertex-computed-then-interpolated shadow-space coordinate is only valid when a single triangle stays within one cascade, and produces a geometrically meaningless blend once a triangle spans (or the camera dolly moves the object across) a cascade boundary. `ToonShader.shader` never branched on that keyword and always used the vertex path, which is exactly what produced the warped, resolution-jumping shadow silhouette the screenshots show. `Lit`-shaded objects are far less visibly affected by the same underlying cascade-boundary aliasing because their continuous PBR shading (soft rolloff, ambient, specular) perceptually masks a slightly mis-shaped soft shadow edge, whereas the toon shader's hard banded shading gives the shadow boundary a sharp, high-contrast edge with nothing to blend the artifact into.

### Option selected, revised, or rejected

- **Selected:** move the shadow-coordinate calculation from the vertex shader to the fragment shader in both `ToonShader.shader` and `ToonShaderOutline.shader` (`float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS); float shadow = MainLightRealtimeShadow(shadowCoord);`), reusing the `positionWS` interpolant that was already present and safe to interpolate linearly (unlike a shadow-space coordinate).
- **Rejected:** tuning URP-level settings (raising `mainLightShadowmapResolution`, shrinking `shadowDistance`/cascade splits to the gameplay camera's actual range, or reducing cascade count) as the primary fix — those would reduce how *visible* the aliasing is at a cascade boundary but do not address the actual shader defect (per-vertex shadow coordinate interpolation across cascades), and were not applied in this session. They remain available as a secondary quality/perf tuning pass if any residual cascade-boundary softness is still noticeable after this fix.

### Rationale

The per-vertex shadow-coordinate calculation was a straightforward divergence from URP's own established convention (`REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR`-style branching in `Lit`), not a stylistic choice, so correcting it to match URP's per-fragment cascade path is the direct fix rather than a workaround. It costs one extra `TransformWorldToShadowCoord` call per fragment instead of per vertex, which is the same cost model URP's own `Lit` shader already pays under cascades, so it is not a meaningful regression for this mobile-first project's budget on the object counts involved (individual props, not a dense particle system).

### Implementation or verification result

Files changed: `Assets/TheVayuputra/ToonShader/Shader/ToonShader.shader` and `Assets/TheVayuputra/ToonShader/Shader/ToonShaderOutline.shader` — removed the `shadowCoord` field from `Varyings` (and its vertex-stage assignment) in both files' `ForwardPass`, and added the per-fragment `TransformWorldToShadowCoord(input.positionWS)` call directly in `frag()` before sampling `MainLightRealtimeShadow`.

Verified live via Unity MCP against the connected `6000.3.21f1` Editor: confirmed the project's actual cascade/shadow settings and keyword state before diagnosing (rather than assuming), and confirmed `AssetDatabase.Refresh` compiled both shader files with zero new Console errors/warnings after the edit. A `Unity_SceneView_CaptureMultiAngleSceneView` capture of `BigRock` in `Level_001` after the fix showed correct, unbroken toon shading and self-shadowing from all four angles (Iso/Front/Top/Right), matching the project owner's original screenshot's rock at a stable framing.

Known limitation: reproducing the exact "dolly across a specific cascade boundary" comparison from the project owner's four zoom screenshots was not scripted in this session (the available Scene View capture tool auto-frames the target rather than accepting an explicit camera distance), so the fix was verified by compilation correctness and by matching URP's own documented per-fragment convention, not by a pixel-diffed before/after at a controlled distance. The project owner should re-check by zooming across the previously-affected range (roughly 6–27 world units from camera) in both Scene View and Play Mode to confirm the shape no longer jumps.

## Entry 3 — Apply material Tiling and Offset in both toon shaders

**Responsible session:** `01a02a90-cb3e-7523-97dd-8f9f705f3685`

### Problem being addressed

Changing the Base Map Tiling or Offset in a toon material had no visible effect because both shader variants sampled the raw
mesh UV and never applied Unity's generated `_BaseMap_ST` transform.

### Prompt used

The project owner asked to fix Tiling and Offset in the toon shader.

### Important AI response

Both the base and outline variants now declare `_BaseMap_ST` in `UnityPerMaterial` and pass mesh UV through
`TRANSFORM_TEX(input.uv, _BaseMap)` before sampling. This follows Unity's standard material texture transform path and keeps
the two shader variants consistent.

### Option selected, revised, or rejected

- **Selected:** use Unity's built-in `_BaseMap_ST` convention and `TRANSFORM_TEX` macro.
- **Rejected:** custom duplicate Tiling/Offset properties or per-material scripts.

### Implementation or verification result

Commit `176a9f2` updated `ToonShader.shader` and `ToonShaderOutline.shader`. Unity compiled the shaders without errors; the
change remains local and was not pushed.
