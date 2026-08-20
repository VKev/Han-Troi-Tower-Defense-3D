# AI Collaboration Log — Blender Models — 20 August 2026

## Entry 1 — Clean and smooth the active Blender model without changing its UV layout

**Responsible session:** `01a0135e-7cb3-7490-827f-cf7d34d7b651`

### Problem being addressed

The active Blender model needed a conservative optimization pass before game use. The requirements were to remove duplicate or unused mesh data, keep the existing texturing intact, and smooth curved surfaces without erasing deliberate hard edges or changing the silhouette. The Blender add-on was listening on port `9875`, while the Codex Blender client initially attempted its default port `9876`.

### Prompt used

The project owner asked the AI to use Blender to optimize the models, preserve UV mapping and texture quality, clean duplicate vertices and other mesh problems, and apply smooth normals selectively to curved areas. The owner then clarified that the work must use Blender MCP on port `9875`.

### Important AI response

The AI first connected to the requested Blender add-on port and inspected the saved `dsd.blend` file before modifying it. The scene contained one active mesh object, `mesh_node`, with 560 vertices, 1,478 edges, 964 triangular polygons, one UV layer containing 2,892 UV loops, and one assigned material. The audit found no coincident vertices, loose vertices, loose edges, or zero-area faces, so welding or decimation would have provided no useful optimization and could have damaged the UV layout or silhouette. The mesh did contain imported custom normals, 281 sharp edges, one orphan eight-vertex mesh datablock, and two material datablocks that were no longer used after the orphan mesh was removed.

The AI therefore used a topology-preserving cleanup: it removed only the orphan mesh and unused materials, removed the imported custom-normal attribute, kept every polygon smooth, and regenerated sharp edges from the actual face angle using an 89-degree threshold. This smooths curved transitions while preserving approximately right-angle structural edges. A full copy of the original blend file was created before the change.

### Option selected, revised, or rejected

- **Selected:** preserve the complete mesh topology, UV coordinates, material assignment, object dimensions, and packed textures.
- **Selected:** remove only zero-user datablocks (`Cube`, `Dots Stroke`, and `Material`) and recalculate sharp edges from an 89-degree geometric threshold.
- **Rejected:** merge-by-distance, because the audit found zero coincident vertices in the active mesh.
- **Rejected:** decimation or remeshing, because the model was already only 964 triangles and either operation would add UV, silhouette, and texture distortion risk without a meaningful mobile-performance benefit.
- **Deferred:** closing or otherwise changing the 64 boundary/non-manifold edges. They were left unchanged because topology repair was not necessary for rendering and could alter intended open surfaces or UV borders without an approved visual redesign.

### Rationale

The useful optimization was data and normal cleanup rather than polygon reduction. The model was already inexpensive for a mobile prop, while its UV and material mapping carried much more visual value than the small triangle reduction a decimator could achieve. Rebuilding sharp-edge flags from face angles gives predictable smooth shading on curved regions and preserves hard architectural transitions. Avoiding all vertex and face changes also makes UV verification exact rather than subjective.

### Implementation or verification result

Blender MCP completed the change and saved `dsd.blend` successfully. The model remained at 560 vertices, 1,478 edges, and 964 polygons. Geometry SHA-256, UV SHA-256, UV-layer loop count, material assignment, and object dimensions were identical before and after the operation. Sharp edges changed from 281 imported flags to 277 angle-derived flags, and the imported custom-normal attribute was removed. Blender reported one mesh and one material after cleanup, no missing external files, and a clean saved state. File size decreased from 5,948,016 bytes to 5,937,507 bytes.

The reversible backup is `dsd.pre-model-optimize-20260820-000112.blend`. The active model and its textures were visually rechecked in the Blender material-preview viewport after saving; curved ring and ornamental surfaces remained smoothly shaded while the platform's structural edges stayed defined. No Unity project model or prefab was imported, replaced, or otherwise changed during this entry.

## Entry 2 — Verify the optimized model through a direct port 9875 connection after restart

**Responsible session:** `01a0135e-7cb3-7490-827f-cf7d34d7b651`

### Problem being addressed

The project owner restarted Codex after the Blender MCP port configuration was changed and asked for the Blender models to be optimized again. The important uncertainty was whether the restarted MCP client was now connecting directly to Blender on port `9875`, and whether the active model still had any remaining safe cleanup work.

### Prompt used

The project owner confirmed the restart and asked the AI to optimize the models in Blender.

### Important AI response

The AI first stopped the previous temporary `9876` compatibility bridge after verifying its exact process identity, leaving Blender as the only listener on port `9875`. Blender MCP continued to respond after the bridge was removed, proving that the restarted client was connected directly to the requested port. The AI then inspected every mesh in the active `dsd.blend` scene and found the same single `mesh_node` object already in its optimized state: 560 vertices, 1,478 edges, 964 triangles, one 2,892-loop UV map, one material, 964 smooth faces, 277 angle-derived sharp edges, and no imported custom-normal attribute.

The post-restart audit found zero coincident vertices, zero zero-area faces, zero loose vertices or edges, zero orphan mesh or material datablocks, and four packed texture images. The material-preview viewport also showed the model and textures rendering normally.

### Option selected, revised, or rejected

- **Selected:** treat the cleanup as idempotent and leave the already-optimized mesh unchanged.
- **Rejected:** run merge-by-distance, decimation, remeshing, or another normals rewrite merely because the optimization request was repeated after restart.
- **Selected:** keep the 64 existing boundary edges unchanged, consistent with the earlier conservative topology decision.

### Rationale

The direct audit showed that all requested cleanup had already been completed and persisted in the saved blend file. Applying geometry operations again would not reduce any measured defect and would introduce avoidable UV, topology, or silhouette risk. Proving the direct `9875` connection and verifying the persisted invariants was therefore the correct completion action.

### Implementation or verification result

Blender MCP remained operational after the temporary bridge was removed, with only Blender listening on port `9875`. The active file was saved and clean, all material and texture references remained available, and the mesh audit reported no remaining actionable cleanup. No Blender datablock, Unity asset, project script, scene, prefab, or importer setting was changed during this verification pass.

## Entry 3 — Optimize hidden FrogStand geometry, repair wall holes, and export to Unity

**Responsible session:** `01a0135e-7cb3-7490-827f-cf7d34d7b651`

### Problem being addressed

The FrogStand model still contained underside and internal geometry that a top-down tower-defense camera would never show. The original outer wall also contained several malformed openings that appeared as black triangular holes. The model needed a measured polygon reduction, the holes needed to be closed, and the final FBX had to preserve the existing UV layout, custom normals, material, texture imports, and Unity asset identity.

### Prompt used

The project owner asked whether significantly more vertices and triangles could be removed without breaking UVs, specifically allowing the underside and interior of the stand to be deleted or simplified. After approving the proposed optimization, the owner identified several holes in the original wall and asked for them to be patched. The owner then clarified that additional hidden underside geometry should be removed because the asset is used in a top-down tower-defense game.

### Important AI response

The AI treated every reduction candidate as a visual-gated change. Broad center-ray visibility removal, looser normal thresholds, and full deletion of low disconnected components were previewed but rejected because they removed visible top surfaces, outer rails, or lower trim. The accepted topology pass removed 98 conservative bottom faces, then removed 15 interior low faces and three large malformed triangles that crossed the model's interior. The wall audit isolated a 97-face outer shell with four abnormal boundary paths: three triangular notches and one quadrilateral notch. Those paths were filled with three triangles and one quad, using a small valid UV patch sampled from the existing red wall region.

Every destructive mesh step was performed transactionally from a backup. Retained corner positions and UVs were matched before and after each operation, and the existing custom split normals were restored for retained faces. The final model was rendered from top, four elevated diagonal views, four low horizontal views, and the bottom before export.

### Option selected, revised, or rejected

- **Selected:** remove only bottom-facing plates and internal/folded faces proven invisible from all approved top-down gameplay views.
- **Selected:** preserve the lower outer rim and ornamental edge components because they remain visible from a shallow gameplay camera.
- **Selected:** repair the four original wall openings with minimal new faces and a non-degenerate UV patch sampled from the existing red wall texture region.
- **Rejected:** aggressive visibility deletion that predicted roughly half the original triangles, because overlapping and flipped source normals produced false hidden-face classifications and visibly damaged the model.
- **Rejected:** deleting all seven low disconnected components, because preview renders showed missing exterior rails and lower trim.
- **Rejected:** decimation or remeshing, because the retained geometry already had low complexity and those operations introduced unnecessary UV and silhouette risk.

### Rationale

For a top-down mobile prop, removing genuinely hidden underside surfaces provides useful savings, but the lower silhouette can still enter the camera frustum at shallow angles. Multi-angle visual comparison was therefore a safer acceptance criterion than normals or ray tests alone. Patching only the abnormal boundary paths corrected the original holes without rebuilding the complete wall or changing its authored texture layout. Exact retained-UV hashes provided stronger evidence than visual texture inspection alone.

### Implementation or verification result

The saved Blender source `C:\Users\VNG\Downloads\dsd.blend` now contains one active mesh with 547 source vertices, 852 polygons, 853 export triangles, one unchanged `UVMap`, and retained custom normals. This is a reduction from 560 vertices and 964 triangles to 547 vertices and 853 triangles while also adding the four necessary wall patches. Retained UV hashes matched before and after every applied topology pass with a maximum UV and position delta of `0.0`.

The reversible backups are `dsd.pre-frogstand-hidden-surface-opt-20260820-002806.blend`, `dsd.pre-wall-hole-patch-20260820.blend`, and `dsd.pre-internal-prune-20260820.blend`. The optimized FBX replaced `Assets/Resources/Models/FrogStand/FrogStand.fbx` while preserving GUID `626abd177b3c746409002718081467b7`. Unity imported one mesh with 853 triangles and 1,541 split render vertices; UV0, normals, and tangents each contain 1,541 entries. The existing `FrogStand.mat` remap was restored through the model importer without changing that material's shader or texture assignments. Base color remained sRGB, the normal map remained a NormalMap with sRGB disabled, metallic/roughness/packed maps remained linear, and the final Unity Console contained zero errors.

## Entry 4 — Restore the exposed lower-rail top faces and re-export FrogStand

**Responsible session:** `01a0135e-7cb3-7490-827f-cf7d34d7b651`

### Problem being addressed

After the hidden-underside optimization, a visible opening remained along the FrogStand's lower gray rail. From a shallow view, the missing top-facing rail surfaces exposed thin internal bars. This was not an intended hollow detail; the previous bottom-face filter had also selected some shallow, outward-facing rail faces because they were below the model origin.

### Prompt used

The project owner supplied a close Blender screenshot and reported that there was still a hole directly on the lower rail, then asked the AI to continue and refresh the Better Context Markdown after completing the repair.

### Important AI response

The AI compared the optimized mesh with the untouched pre-optimization backup and identified 26 original rail faces that had been removed by the broad underside predicate. It first evaluated a custom eight-quad cover strip, but topology validation found five edges shared by three faces. That candidate was rejected before export. The accepted repair rebuilt the mesh from the clean pre-repair backup and restored only the 26 matching original faces, including their original winding, material index, smooth state, and per-loop UV coordinates. Six source vertices that no longer existed in the optimized mesh were restored with those faces.

### Option selected, revised, or rejected

- **Selected:** restore the exact original lower-rail faces from the baseline blend file.
- **Selected:** keep all 852 previously accepted optimized faces and verify their semantic face/UV signatures remained unchanged.
- **Rejected:** retain the custom eight-quad cover strip, because it closed the visible opening but produced five overfull non-manifold edges.
- **Rejected:** undo the complete underside optimization, because only the lower-rail top surfaces were visually required.

### Rationale

Restoring authored faces is safer than inventing a new cap over a complex joined mesh. It closes the visible gap with the original silhouette and UV mapping, avoids overlapping surfaces, and retains most of the earlier hidden-geometry reduction. Rebuilding from the pre-repair backup also prevents any topology from the rejected cover-strip experiment from leaking into the final model.

### Implementation or verification result

The saved Blender source `C:\Users\VNG\Downloads\dsd.blend` now contains 553 source vertices, 878 polygons, and 879 export triangles. It retains one `UVMap`, the original material, and custom normals. All 852 pre-existing face/UV signatures matched exactly after repair; topology checks found zero overfull edges and zero degenerate faces. Sixteen final textured renders covering eight azimuths at both shallow and top-down elevations showed the lower rail closed without exposing the internal bars.

The FBX at `Assets/Resources/Models/FrogStand/FrogStand.fbx` was re-exported while preserving GUID `626abd177b3c746409002718081467b7`. Unity imported one mesh with 879 triangles and 1,595 split render vertices; UV0, normals, and tangents each contain 1,595 entries. The renderer still uses `Assets/Resources/Models/FrogStand/FrogStand.mat`, the importer remains in `LegacyImport` material mode, and the Unity Console contained zero errors. The reversible pre-repair backup is `C:\Users\VNG\Downloads\dsd.pre-lower-rim-gap-fix-20260820-014551.blend`.

Before refreshing project context, Unity MCP confirmed that the Editor was not playing, compiling, or updating. `better-context-unity agents` completed with 75 managed maps unchanged and no stale maps; its offline asset-coverage warning was retained because the CLI did not locate the installed Unity Editor. `better-context-unity verify` then confirmed that context is current at source hash `ad8ac6b47f49`.
