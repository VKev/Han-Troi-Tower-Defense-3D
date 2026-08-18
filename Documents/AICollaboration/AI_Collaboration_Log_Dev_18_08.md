# AI Collaboration Log — Development — 18 August 2026

## Entry 1 — Rebuild the frog hind-leg rig and create matching jump and idle animations

**Responsible session:** `01a0135e-7cb3-7490-827f-cf7d34d7b651`

### Problem being addressed

The frog's original jump animation exposed several connected rigging problems: the thigh and lower leg visually separated during frames 7–20, the legs did not extend naturally behind the body, the top and side silhouettes did not follow the original frog anatomy, the arms were stiff, the three toes were hidden or offset, and the feet twisted incorrectly in the air. Rebuilt leg geometry also needed UVs compatible with the original texture. A looping idle animation was required whose first pose exactly matched the end of the jump for a smooth in-game transition.

### Prompt used

Use the original frog pose and anatomy as the reference. Rebuild bones or meshes where necessary, inspect the result from top, side, and front views, and compare how rotation and position changes affect the pose. Preserve the bent-knee structure and three-toe foot shape, make the airborne legs extend correctly, fix the detached joints and foot rotation, map the rebuilt geometry to the original texture, and create an idle animation that begins at the final jump pose.

### Important AI response

Inspection showed that pose adjustments alone could not reliably fix the disconnected low-poly leg pieces. The work therefore moved to a non-destructive rebuilt hind-leg rig with clean FK rotation chains, connected joint coverage, separate toe controls, and replacement leg meshes based on the original proportions. Multi-view visual checks were combined with numeric joint-gap, symmetry, foot-angle, loop, and transition checks. The foot twist was traced to a rest-axis mismatch between the palm mesh and the `HindFoot` bone. The first UV transfer method also produced white and streaked faces, so it was replaced with per-face transfer from the nearest triangle on the original textured mesh.

### Option selected, revised, or rejected

- **Selected:** Keep the original rig and intermediate animation actions as rollback references, while using `FrogRig_Rebuilt_v1` for the corrected result.
- **Selected:** Use `Frog_Jump_Rebuilt_v9` for the final 36-frame jump and `Frog_Idle_Rebuilt_v1` for the 49-frame looping idle, both at 24 fps.
- **Selected:** Add independent inner, middle, and outer toe bones on each hind foot, then reposition and narrow the toe meshes so all three toes remain visible.
- **Selected:** Repair the `HindFoot` rest axis to follow the actual palm direction and animate the limbs with FK rotations while reserving translation for the body/root.
- **Selected:** Reuse the original material and texture atlas, with per-low-poly-face UV transfer from the nearest original surface triangle.
- **Revised:** The original rig and early pose-only corrections were superseded because they continued to expose separated joints and incorrect anatomy.
- **Revised:** The initial nearest-vertex or interpolated UV transfer was superseded after visual inspection exposed texture streaking and white faces.
- **Revised:** Earlier jump actions, including v7 and v8, were retained only as backups after the foot-axis problem was identified and corrected in v9.
- **Rejected:** Deleting the original rig or backup actions, and moving leg meshes independently without corresponding bone control.

### Rationale

The rebuilt hierarchy keeps each physical limb segment connected throughout the jump and makes the effect of each bone rotation predictable from every inspected camera axis. Independent toe bones preserve the original three-toe silhouette instead of treating the foot as one rigid wedge. Reusing the original material and atlas keeps the new geometry visually consistent with the source model. Exact endpoint matching avoids a visible pose snap when gameplay changes from jump to idle, while preserved originals and intermediate actions provide a practical rollback path.

### Implementation or verification result

- Rebuilt and adjusted both hind thighs, shins, hip joints, palms, and all three toes per side.
- Added `HindToeInner`, `HindToeMiddle`, and `HindToeOuter` bones for both left and right feet.
- Verified zero visible/numeric joint gap, zero left-right pose symmetry error, and a maximum airborne foot-to-shin angle error of 0 degrees across frames 7–20.
- Verified that all 14 rebuilt hind-leg mesh parts contain a `UVMap`, use the original `material`, and have no missing UV loops or degenerate UV triangles after the final transfer.
- Verified jump range 1–36, idle range 1–49, zero jump-loop error, zero idle-loop error, and zero planted hind-foot drift during idle.
- Verified that jump frame 36 and idle frame 1 match within approximately `1.19e-7` matrix difference.
- Marked the final and backup animation actions with fake users so Blender retains them after saving, and removed temporary helper objects.
- Preserved rollback data, including `Frog_Jump_Rebuilt_v8` and `FrogRig_Rebuilt_v1_PreFootAxis_v9_Data`.
- The live Blender scene in `Frog.blend` was still dirty at final verification and required an explicit save to persist the changes.
- Unity import, Animator setup, runtime transition testing, and in-game visual QA were not performed in this session.

## Entry 2 — Finalize animation actions and add mouth and cheek controls

**Responsible session:** `01a0135e-7cb3-7490-827f-cf7d34d7b651`

### Problem being addressed

The Blender file still contained 17 superseded test and backup actions in addition to the approved jump and idle. The frog also needed separate mouth-open and mouth-close clips suitable for a top-down game, with visible head movement rather than relying only on the mouth silhouette. The idle needed a symmetric cheek-breathing motion. An initial mouth-cavity mesh protruded through the closed mouth and appeared as an incorrect dark object from the side.

### Prompt used

Remove unnecessary Blender actions and retain the final jump and idle. Create separate open-mouth and close-mouth animations, make the head rotate upward clearly during the opening for a top-down camera, add a larger-and-smaller cheek cycle to idle, and correct any dark geometry protruding from the mouth. Refine the idle afterward so the cheeks deform without scaling the eyes, with slower inflate/deflate phases separated by an observable rest interval.

### Important AI response

The facial animation was implemented on `FrogRig_Rebuilt_v1` with dedicated head, jaw, and cheek controls so all four clips remain skeletal actions on one Unity-compatible rig. The mouth received a small UV-mapped interior cavity and tongue. The cavity was then reduced and moved behind the lip line after front and side inspection showed that its first placement protruded through the face. A temporary whole-`Head` puff made the eyes expand, so the final revision restored neutral `Head` scale and added lower `CheekPuff.L` and `CheekPuff.R` bones dedicated to breathing. The coordinate-based weight falloff assigns identical values to coincident low-poly vertices and reaches zero before the eye region, allowing the new cheek bones to move outward and forward without deforming either eyeball. The higher `Cheek.L` and `Cheek.R` controls remain in the compatible hierarchy but are neutral and unweighted.

### Option selected, revised, or rejected

- **Selected:** Keep only `Frog_Jump_Rebuilt_v9`, `Frog_Idle_Rebuilt_v1`, `Frog_Mouth_Open_v1`, and `Frog_Mouth_Close_v1` in the main file.
- **Selected:** Animate the mouth clips on the rebuilt armature with a 14-degree jaw rotation and a clearly visible 8.5-degree upward head pitch.
- **Selected:** Keep `Head.scale` neutral and drive both cheeks with lower dedicated `CheekPuff.L`/`CheekPuff.R` bones, reaching the two puff peaks at frames 21 and 69.
- **Selected:** Use a 97-frame rhythm: slowly inflate from frame 1 to 21, deflate by 33, hold neutral through 49, inflate again through 69, deflate by 81, then hold neutral through frame 97.
- **Selected:** Add Cycles modifiers to every idle F-curve, enable the action's cyclic/manual range flags, and set both scene and preview ranges to frames 1–97.
- **Selected:** Store a separate `Frog_before_mouth_cleanup.blend` rollback file before deleting the superseded actions.
- **Revised:** The mouth cavity was made smaller, moved deeper into the head, and lightened to a deep red so it only appears through the open mouth.
- **Revised:** The temporary unified `Head` deformation was superseded by coordinate-based cheek weights after review showed that whole-head scaling also enlarged the eyes.
- **Rejected:** Shape-key-only facial clips because separate skeletal actions on the existing rig provide a clearer Unity import path.

### Rationale

Keeping all gameplay clips on one armature gives the Animator a consistent bone hierarchy and prevents facial transforms from persisting unintentionally when actions change. Head pitch makes the mouth-open event readable from the intended overhead camera. Neutral first and last facial keys preserve transition compatibility. A recessed mouth cavity supplies depth without intersecting the lips. Coordinate-driven cheek falloff preserves coincident low-poly panels and keeps eye-region weights at zero. Placing the dedicated cheek bones from local Z 1.12 to 1.27 keeps their controls visibly below the eyes while still making the side cheeks expand from both front and overhead views.

### Implementation or verification result

- Removed 17 superseded actions and verified that exactly the four approved actions remain, all protected by fake users.
- Verified ranges of frames 1–36 for jump, 1–97 for idle, and 1–13 for each mouth clip.
- Verified jump frame 36 to idle frame 1 within `1.19e-7` matrix difference.
- Verified exact matches for idle frame 1 to open frame 1, open frame 13 to close frame 1, close frame 13 to idle frame 1, and idle frame 1 to frame 97.
- Verified mouth-open final rotations of -8.5 degrees on `Head` and 14 degrees on `Jaw`.
- Verified that the final localized idle puff increases visible cheek width by approximately 6.74%, with visual comparison from front and top-down cameras.
- Verified 0% eye-size change on both sides at both puff peaks, with `Head.scale` remaining neutral.
- Verified an exact idle endpoint match, exact frame 2/frame 98 cyclic repetition, and Cycles modifiers on all 244 idle F-curves after adding the two dedicated cheek-bone bindings.
- Verified that both dedicated cheek-bone tails end at local Z 1.27 while the measured eye region begins near Z 1.495, leaving approximately 0.225 units of vertical clearance.
- Verified zero separation across all 46 coincident cheek-region vertex clusters during both idle puffs, jump frame 18, mouth-open frame 7, and mouth-close frame 7.
- Verified that `FrogMouthInterior_v1` and `FrogTongue_v1` each contain complete `UVMap` loop data, use the rebuilt armature modifier, and are weighted to `Head` and `Jaw` respectively.
- Verified from front and side renders that the corrected mouth cavity no longer protrudes outside the closed lips.
- Replaced the hard `Head`/`FrogBody` boundary with a continuous neck-to-back weight gradient after the upward head pitch exposed separated low-poly panels; rear, rear-quarter, and side renders showed a continuous silhouette, and 34 duplicate-vertex clusters reported a maximum separation of 0.
- Saved the final result to `C:\Users\VNG\Downloads\Frog.blend`; preserved `Frog_before_idle_cheek_eye_fix_20260818.blend` and `Frog_before_dedicated_cheek_bones_20260818.blend` as rollback files for the two refinement stages.
- Unity import, Animator Controller setup, and runtime top-down gameplay testing remain outside this Blender session.

## Entry 3 — Recover and strengthen the isolated frog cheek-puff animation

**Responsible session:** `01a0135e-7cb3-7490-827f-cf7d34d7b651`

### Problem being addressed

After reconnecting Blender MCP, the visible idle animation still enlarged and separated the eyes instead of isolating the cheek puff. Live inspection showed that the saved `Frog.blend` did not contain the previously reported `CheekPuff.L` and `CheekPuff.R` bones or vertex groups. Its active idle was the older 49-frame revision, whose `Head` scale reached `1.12` on X and `1.06` on Z, so every head-weighted vertex, including both eyes, expanded together. The first recovered cheek-only revision was technically correct but the user requested a visibly larger puff.

### Prompt used

Fix the frog cheek animation because the previous cheek result did not work after Blender was reconnected, then make the isolated cheek puff substantially larger in both horizontal and vertical directions while keeping the eyes unaffected.

### Important AI response

The AI compared the live armature, action channel bags, vertex groups, evaluated mesh positions, and front/top-down viewport images rather than relying on the prior report. It identified whole-`Head` scaling as the actual saved-file cause, rebuilt two lower cheek controls, and used a symmetric coordinate falloff restricted below local Z `1.32`. Vertices influenced by `Jaw` were excluded, coincident low-poly vertices received identical weights, and `Head` weights were reduced by exactly the amount transferred to each dedicated cheek group. After the earlier revisions still appeared too subtle, each cheek pivot was kept at the center of its weighted mesh volume and the final peak was increased to `(1.60, 1.75, 1.58)`, producing a clearly larger puff on both the horizontal and vertical axes.

### Option selected, revised, or rejected

- **Selected:** Recreate `CheekPuff.L` and `CheekPuff.R` as deform bones parented to `Head`, spanning local Z `1.12–1.27`.
- **Selected:** Assign only lower lateral cheek vertices to the new controls, exclude jaw-weighted vertices, and force all vertices at or above local Z `1.34` to remain outside the cheek groups.
- **Selected:** Keep the slow 97-frame rhythm with puff peaks at frames 21 and 69, neutral returns at frames 33 and 81, and a neutral loop endpoint at frame 97.
- **Selected:** Center the cheek pivots near local Z `1.19` and use the final peak scale `(1.60, 1.75, 1.58)`, with bone-local Y aligned to world vertical.
- **Revised:** The recovered `(1.20, 1.06, 1.18)` scale was too subtle, `(1.32, 1.10, 1.30)` expanded mainly sideways, and `(1.38, 1.50, 1.38)` was still smaller than requested; all are retained through rollback copies.
- **Rejected:** Retaining or compensating for the old whole-`Head` scale curves, because any such scaling necessarily moves the eyes.

### Rationale

The eye and cheek geometry share the `Head` deformation hierarchy, so a whole-head scale cannot satisfy the requirement regardless of timing. Dedicated lower controls provide an explicit Unity-compatible skeletal channel and make the deformation boundary measurable. A smooth coordinate mask preserves the low-poly face panels without introducing cracks, while a hard upper safety boundary guarantees that stronger cheek values cannot leak into the eyes. Separate pre-change and pre-strengthening backups preserve both the original and subtler recovered versions.

### Implementation or verification result

- Flattened every `Head.scale` key in `Frog_Idle_Rebuilt_v1` to `(1, 1, 1)`.
- Recreated `CheekPuff.L` and `CheekPuff.R`; assigned 33 left and 38 right lower-cheek vertices with a maximum transferred weight of `0.9`.
- Extended the idle manual/cyclic range to frames `1–97`, retained Cycles modifiers, and left exactly the four approved actions in the file.
- Verified zero displacement for all 215 high head/eye safety vertices at both puff peaks.
- Verified that the final puff increases vertical cheek span by approximately `20.83%`, horizontal span by `11.28%`, and depth by `14.51%`; both requested visible axes are substantially larger while vertical growth remains the strongest component.
- Verified exact neutral returns at frame 33 and frame 97, and zero separation across 22 coincident cheek-vertex clusters.
- Compared frame 1 and frame 21 from the existing front viewport and a temporary top-down viewport; the larger lower-cheek expansion is visible while both eyes retain identical centers and bounds.
- Saved the final neutral frame to `C:\Users\VNG\Downloads\Frog.blend`.
- Preserved `C:\Users\VNG\Downloads\Frog_before_cheek_rebuild_20260818_194643.blend`, `C:\Users\VNG\Downloads\Frog_before_larger_cheek_puff_20260818_195125.blend`, `C:\Users\VNG\Downloads\Frog_before_rounder_vertical_cheek_puff_20260818_195715.blend`, and `C:\Users\VNG\Downloads\Frog_before_extra_large_cheek_puff_20260818_200030.blend` as rollback copies.
- Unity import and runtime Animator verification remain outside this Blender-only correction.

## Entry 4 — Add independent lower-cheek puff deformation

**Responsible session:** `01a0135e-7cb3-7490-827f-cf7d34d7b651`

### Problem being addressed

Visual review of the stronger idle puff showed that only the upper cheek volume expanded. The lower cheek panels under the mouth remained almost static because the existing `CheekPuff.L/R` weights started near local Z `1.08`, while the lower outer panels were assigned either to `Jaw` or to the unanimated `Head`/`FrogBody` transition.

### Prompt used

Fix the idle animation so the lower cheek also visibly inflates instead of only the upper cheek.

### Important AI response

The AI inspected group weights, mesh connectivity, duplicate low-poly panels, and evaluated positions across all four approved actions. It added dedicated `LowerCheekPuff.L` and `LowerCheekPuff.R` deform bones beneath `Jaw`, transferred only the outer jaw-corner panels to those controls, and extended the existing upper-cheek falloff into the adjacent lower `Head` transition. Parenting the new controls to `Jaw` preserves mouth-opening motion while allowing an independent idle puff.

### Option selected, revised, or rejected

- **Selected:** Add two lower-cheek controls parented to `Jaw`, using the same idle peaks at frames 21 and 69 and the same peak scale `(1.60, 1.75, 1.58)` as the upper cheeks.
- **Selected:** Transfer 15 left and 12 right outer jaw-panel vertices to the new controls, and extend 34 neighboring head-transition vertices with a smooth coordinate falloff.
- **Selected:** Add explicit neutral scale bindings for all four cheek controls to jump, mouth-open, and mouth-close actions so facial scale cannot persist between clips.
- **Rejected:** Assign the lower jaw panels directly to the existing `Head`-child cheek bones, because that would alter their expected jaw-following behavior during mouth animation.

### Rationale

The lower cheek includes panels from two deformation branches. Separating its jaw-owned portion into jaw-child controls preserves the original jaw transform at neutral scale, while the adjacent head-owned portion can continue using the existing head-child cheek controls. This keeps the idle puff continuous without coupling eye, central lip, or mouth motion to the cheek scale.

### Implementation or verification result

- Added `LowerCheekPuff.L` and `LowerCheekPuff.R` and verified that both are deform bones parented to `Jaw`.
- The revised lower-cheek region moves by an average of approximately `0.108` Blender units at frame 21 and expands approximately `13.46%` horizontally; its vertical extent changes visibly rather than remaining static.
- Verified zero movement across all 215 upper eye/head safety vertices, exact neutral returns at frames 33 and 97, and zero separation across 39 affected coincident low-poly clusters at both puff peaks.
- Verified that jump frame 18 is unchanged and that mouth-open/mouth-close frame 7 differ by no more than `8.43e-8` Blender units after the jaw-weight transfer.
- Kept exactly the four approved actions and added explicit cheek scale curves to each; the idle action retains cyclic cheek curves.
- Preserved the existing mesh topology and complete `UVMap` data with 2,088 UV loops.
- Saved the neutral idle frame to `C:\Users\VNG\Downloads\Frog.blend` and preserved `C:\Users\VNG\Downloads\Frog_before_lower_cheek_puff_20260818_200900.blend` for rollback.
- Unity import and runtime Animator verification remain outside this Blender-only correction.

## Entry 5 — Export the final textured frog and clean animation clips to Unity

**Responsible session:** `01a0135e-7cb3-7490-827f-cf7d34d7b651`

### Problem being addressed

The final frog rig and animations existed only in `C:\Users\VNG\Downloads\Frog.blend`. The four approved Blender actions still used revision suffixes, and Unity did not yet have a production FBX, explicit textures, clean clip names, or material remaps under the Frog Resources folder.

### Prompt used

Rename all actions and export the FBX with textures into the Unity Resources model Frog folder.

### Important AI response

The AI renamed the four Blender actions, exported only the final rebuilt rig and 17 renderable meshes, excluded hidden source rigs and superseded foot meshes, and baked every action into one binary FBX. It unpacked the body textures into the Unity project, generated a Unity-compatible metallic-smoothness map from the source packed channels, then used Unity's `ModelImporter` to expose clean clip names and remap three URP materials.

### Option selected, revised, or rejected

- **Selected:** Rename the actions to `Frog_Idle`, `Frog_Jump`, `Frog_Mouth_Open`, and `Frog_Mouth_Close`.
- **Selected:** Export one `Frog.fbx` containing the Generic rig, all final meshes, and all four baked actions; disable leaf bones and exclude hidden/source objects.
- **Selected:** Export the Unity-ready Base Color, Normal, and Metallic-Smoothness textures; keep the source Metallic-Roughness data packed in the Blender/FBX source rather than duplicating an unused runtime texture.
- **Selected:** Create and remap `Frog_Body`, `Frog_MouthInterior`, and `Frog_Tongue` URP materials, and configure the normal/metallic texture import settings explicitly.
- **Revised:** Blender's FBX stack names included the armature prefix despite clean action names, so Unity clip overrides were added to expose the exact requested names.

### Rationale

Keeping all clips on one FBX gives Unity one consistent skeleton and prevents retarget mismatches. Explicit external textures and material remaps are more reliable than relying on Unity to infer Blender node graphs or embedded combined PBR channels. A separate metallic-smoothness texture converts the source Blue metallic and Green roughness channels into Unity's Red metallic and Alpha smoothness layout.

### Implementation or verification result

- Saved the renamed Blender actions to `C:\Users\VNG\Downloads\Frog.blend`; preserved `C:\Users\VNG\Downloads\Frog_before_unity_export_20260818_201826.blend` for rollback.
- Exported `Assets/Resources/Models/Frog/Frog.fbx` with 17 skinned renderers and 27 deform bones per renderer.
- Exported three Unity-ready 2048x2048 textures and created three external URP material assets in the same folder.
- Verified Unity clips `Frog_Idle` frames 0–96, `Frog_Jump` frames 0–35, `Frog_Mouth_Open` frames 0–12, and `Frog_Mouth_Close` frames 0–12 at 24 FPS; only Idle loops.
- Verified Generic rig import, animation enabled, all renderer material remaps, Base/Normal/Metallic-Smoothness bindings, normal-map import type, and linear metallic texture import.
- Unity's final aggregate import check returned true and the Console reported zero errors.

## Entry 6 — Establish the Drought vegetation concept direction for mobile assets

**Responsible shared chat:** `6a8472be-25ac-83ec-88c1-5dc52e1d5f2f`

### Problem being addressed

Vegetation concepts for the Drought environment needed to remain recognizable from a small mobile gameplay camera without spending geometry or texture detail on features the player would not perceive. Early agave and tree concepts also contained too many spikes and too much leaf/trunk detail.

### Prompt used

Create isolated stylized 3D-game-asset concept images for Drought vegetation, emphasizing strong silhouettes, chunky primary forms, limited matte colors, restrained surface detail, and efficient mobile readability. Subsequent review requested fewer agave spikes, substantially simpler tree foliage and trunk treatment, blob-like leaves, and a separate leaf/succulent element suitable for placement along a rock edge.

### Important AI response

The shared ChatGPT conversation generated successive concept-image groups for an olive-gold bush, a muted olive-and-mustard mound, an agave rosette, a sparse forked tree, a simplified low-poly olive canopy tree, and a sage-green succulent/rock-edge plant. The recurring recommendation was to derive quality from silhouette, proportion, color hierarchy, and material separation rather than micro-geometry, realistic wear, or high-frequency texture noise.

### Option selected, revised, or rejected

- **Selected:** Use a friendly cartoon-stylized Drought aesthetic with large primary shapes, clear medium secondary shapes, soft simplified edges, cohesive muted colors, and matte or lightly rough materials.
- **Selected:** Keep vegetation readable at small gameplay scale and concentrate detail only where it materially improves recognition.
- **Revised:** Reduce the agave's spike count after the initial concept appeared overly thorny.
- **Revised:** Replace detailed tree leaves with larger foliage blobs and simplify the trunk after the first sparse-tree concept remained too detailed.
- **Selected:** Treat the rock-edge leaf/succulent as a separate placeable asset rather than embedding it into a rock model.
- **Rejected:** Photorealistic rendering, micro-detail, dense ornamentation, thin fragile geometry, realistic scratches/dirt, noisy surfaces, and cluttered silhouettes.

### Rationale

TowerDefense3D is mobile-first, and these environment props will normally occupy limited screen space. Large readable masses and restrained materials therefore provide more gameplay value and a more consistent art style than costly geometry or texture features that disappear at normal camera distance. Separate rock-edge foliage also improves placement reuse and composition flexibility.

### Implementation or verification result

- The shared conversation produced seven concept-image response groups and two explicit visual simplification passes covering thorn count and tree detail.
- The final named concept directions included `Low-Poly Olive Canopy Tree` and `Stylized Sage Green Succulent Asset`, alongside the earlier bush, mound, agave, and forked-tree explorations.
- These outputs are visual concept references only; the shared conversation did not create, optimize, import, or validate any 3D model, texture package, prefab, collider, LOD, or runtime Unity asset.
- Source conversation: `https://chatgpt.com/share/6a8472be-25ac-83ec-88c1-5dc52e1d5f2f`.
