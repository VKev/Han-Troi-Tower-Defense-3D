# AI Collaboration Log — Blender Models — 25 August 2026

## Session continuity

- **Project:** `TowerDefense3D`
- **Responsible Codex session:** `01a0135e-7cb3-7490-827f-cf7d34d7b651`
- **Related record:** `AI_Collaboration_Log_BlenderModels_20_08.md`
- **Tracking issues:** `TowerDefense3D-0uv4`, `TowerDefense3D-5k6i`, and `TowerDefense3D-2sjk`

This file records the consequential Blender-to-Unity decisions from the current model-import conversation. It summarizes the
verified work without reproducing the raw transcript.

## Entry 1 — Preserve Pebble Pal rig and both animations while importing the optimized enemy

### Problem being addressed

The Pebble Pal enemy supplied by Meshy contained a 24-bone Generic rig and two animations that had to remain functional after
mesh cleanup. The model also needed correct Unity texture imports and a runtime-loadable location consistent with the project's
plural `Assets/Resources/Models/` convention.

### Prompt used

The project owner asked the AI to optimize the model, remove duplicate vertices or faces, repair normals without affecting its
rigged animations, merge the two animation takes into one FBX, remove the redundant source FBXs, and export the result into the
Unity enemy-model folder. When the initially requested singular `Resources/Model/Enemy/Normal` path conflicted with the root
README, the owner approved the recommended `Assets/Resources/Models/Enemies/Normal` path.

### Important AI response

The AI treated the armature, vertex groups, UVs, and animation curves as protected data. Blender verification found 250 logical
mesh vertices, 432 triangles, 24 bones, and 22 weighted vertex groups. It found no coincident vertices, duplicate faces,
degenerate faces, loose geometry, boundary edges, or inward closed components. Re-importing the final FBX preserved topology,
UVs, weights, and the evaluated poses with a maximum measured pose difference below `0.000002`.

The final FBX contains `Idle` and `Walking` at 30 FPS. Unity imports it as a Generic rig with animation compression disabled;
both clips are renamed cleanly and configured as looping clips. The source textures remain external. A URP/Lit material uses the
base-color and normal textures plus a generated metallic-smoothness map, with metallic stored in red and
`smoothness = 1 - roughness` stored in alpha.

### Option selected, revised, or rejected

- **Selected:** preserve the already-clean 432-triangle topology rather than decimate a rigged mobile enemy without evidence.
- **Selected:** keep `Idle` and `Walking` in one FBX, configure both as loops, and disable animation keyframe compression.
- **Selected:** use external URP textures and a packed metallic-smoothness map.
- **Revised:** use `Assets/Resources/Models/Enemies/Normal` instead of the singular `Resources/Model` path.
- **Rejected:** `Optimize Game Objects`, remeshing, or another weld pass because each could change bone access, deformation, or
  protected mesh data without reducing a measured defect.

### Implementation or verification result

The optimized FBX is `Assets/Resources/Models/Enemies/Normal/Meshy_AI_Pebble_Pal_biped_Optimized.fbx`. Unity reports 432
triangles, 24 bind poses, and 1,248 render vertices; the larger Unity vertex count is expected splitting at UV, normal, and
other render seams rather than duplicate source geometry. The material resolves the expected Base, Normal, and
Metallic-Smoothness maps. Unity Console contained zero errors, and Bead `TowerDefense3D-0uv4` was closed.

## Entry 2 — Externalize Bamboo Stilt House textures and repair one inverted closed component

### Problem being addressed

The Bamboo Stilt House FBX was approximately 6.29 MB even though its geometry was already extremely small. Inspection showed
that four 2048-by-2048 PBR textures were packed inside the FBX. The model needed UV-safe cleanup, correct smooth shading, a much
smaller geometry file, and a complete Unity material without copying unnecessary source maps into `Resources`.

### Prompt used

The project owner approved `Assets/Resources/Models/BambooStiltHouse` and a conservative optimization pass. The owner then
clarified that "remove textures from the model" meant stripping embedded image payloads from the FBX while retaining external
textures so the rendered appearance would not change.

### Important AI response

Blender inspection found 280 vertices and 416 triangles, with no duplicate vertices or faces, loose geometry, degenerate faces,
or open boundaries. Because polygon reduction had no useful performance value, the AI preserved every vertex and face. It
flipped one inward closed component containing six faces, removed packed-image ownership, relinked the material to external
textures, and rebuilt sharp-edge flags from a 50-degree angle threshold. This reduced sharp edges from 572 to 415 while keeping
all 416 faces smooth and retaining hard architectural transitions.

The UV signature and topology counts matched exactly before and after the operation. A clean re-import of the exported FBX
reported 37 closed components, zero inward components, zero duplicate or invalid geometry, 416 triangles, and no packed images.
Matched before/after renders showed the same silhouette and texture placement.

### Option selected, revised, or rejected

- **Selected:** topology-preserving normal cleanup and external texture references.
- **Selected:** export with `embed_textures = false` and keep only Base Color, Normal, and a packed Metallic-Smoothness texture
  in Unity.
- **Rejected:** decimation or remeshing because the source was already only 416 triangles.
- **Rejected:** copying separate metallic and roughness maps into `Resources`; Unity uses their packed URP representation instead.

### Implementation or verification result

The FBX decreased from 6,291,084 bytes to 70,732 bytes and was imported as
`Assets/Resources/Models/BambooStiltHouse/BambooStiltHouse.fbx`. Unity imports 416 triangles and 862 split render vertices with
UV0 present for every render vertex, imported normals, Mikk tangents, no animation, and CPU readability disabled. The external
URP/Lit material resolves all three expected texture assets. A final filesystem audit caught missing Normal and
Metallic-Smoothness files after an intermediate refresh; they were restored from the verified staging data, reassigned, and
checked again before handoff. Unity Console contained zero errors, and Bead `TowerDefense3D-5k6i` was closed.

## Entry 3 — Preserve intentional Bamboo Grove leaf cards and render them double-sided

### Problem being addressed

The Bamboo Grove source FBX was approximately 5.15 MB because it also embedded four PBR textures. Its mesh contained several
open components that initially appeared as non-manifold boundaries, so the optimization needed to distinguish intentional leaf
cards from actual model damage before modifying topology.

### Prompt used

The project owner asked the AI to optimize the Bamboo Grove and import it into Unity with the same workflow used for the
preceding models. The established convention placed it in `Assets/Resources/Models/BambooGrove` with textures outside the FBX.

### Important AI response

Blender measured only 134 vertices and 141 triangles, with zero duplicate vertices or faces, loose elements, or degenerate
faces. Component analysis identified one closed inward component containing four faces and 12 deliberately open components
used as single-sided leaf cards. The AI flipped only the invalid closed component and preserved every leaf card. A 75-degree
smooth-by-angle threshold reduced sharp edges from 187 to 151 so the low-sided bamboo stalks shade smoothly while cap and
structural edges remain defined.

Because the leaves are open cards, the Unity URP/Lit material was configured with culling disabled and double-sided GI enabled.
This prevents leaves from disappearing when viewed from different top-down camera directions without duplicating their
geometry. Base Color and Normal remain external, while Metallic and Roughness are packed into one external URP map.

### Option selected, revised, or rejected

- **Selected:** preserve all 141 triangles and the exact UV mapping.
- **Selected:** repair only the closed inward component and retain all 12 intentional open leaf components.
- **Selected:** render the Unity material two-sided instead of duplicating leaf-card triangles.
- **Rejected:** closing the 39 boundary edges because that would turn intentional leaf cards into incorrect solid geometry.
- **Rejected:** decimation or remeshing because the asset was already far below a consequential mobile prop budget.

### Implementation or verification result

The FBX decreased from 5,152,940 bytes to 38,268 bytes and was imported as
`Assets/Resources/Models/BambooGrove/BambooGrove.fbx`. A clean Blender re-import retained 134 vertices, 141 triangles, 423 UV
loops, zero duplicate or degenerate geometry, zero inward closed components, and no packed images. Unity imports 141 triangles
and 295 split render vertices, remaps the external URP/Lit material, and verifies culling is off with double-sided GI enabled.
Unity Console contained zero errors, Better Context was refreshed after both asset imports, and Bead `TowerDefense3D-2sjk` was
closed.
