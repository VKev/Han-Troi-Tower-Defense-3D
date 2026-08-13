# TowerDefense3D

## Product target

TowerDefense3D is an **Android-first 3D tower-defense game** built with Unity `6000.3.21f1`. Android touch devices are the current primary target; iOS support is deferred. Mouse input in the Unity Editor is a development fallback, not the main interaction model.

Project decisions should therefore preserve these constraints:

- Design gameplay input for touch first, including tap, drag, placement confirmation, and cancellation.
- Keep controls, placement feedback, and UI readable on small screens and inside device safe areas.
- Avoid interactions that depend on hover, right-click, or a hardware keyboard.
- Treat mobile CPU, GPU, memory, battery, thermal limits, and allocation pressure as production constraints.
- Validate gameplay and presentation on representative Android landscape aspect ratios. Physical-device performance and build convergence remain required before release, but are explicitly outside the current Grid Placement implementation run.

## Approved Grid Placement decision record

Status: **Approved for implementation**

### Platform and presentation contract

- Target Unity `6000.3.21f1`; do not add an older-Editor compatibility branch.
- Target Android only for this implementation. iOS, application icons, and custom splash artwork are deferred.
- Company Name: `nextgen.khanghv2.vng`.
- Android Application ID: `vng.khanghv2.nextgen.towerdefense3d`.
- Run fullscreen in landscape. Permit Landscape Left and Landscape Right, disable portrait orientations, and disable Android resizable or multi-window behavior.
- Target 60 FPS on a mid-range Android device. This run establishes only an evidence-based project baseline; it does not include physical-device build, profiling, or performance convergence.

### Placement interaction contract

- Touch is the primary input and the Unity Editor provides a mouse fallback.
- Selecting a tower begins placement. Touch or drag moves one candidate. Releasing over a valid candidate immediately revalidates and atomically places the tower; there is no Confirm button.
- Releasing over an invalid candidate performs no gameplay mutation and keeps the red candidate available for repositioning or cancellation.
- A Safe Area Cancel control and Android Back both cancel placement.
- A placement gesture that starts over UI is ignored. Only one primary pointer controls placement; secondary pointers are ignored.
- Pausing or losing application focus cancels or suspends the gesture safely and must never place a tower.

### Grid and validation contract

- The board uses an XZ ground plane with discrete vertical Y levels. Code and documentation use `Width` for X, `Depth` for Z, and `Height` for Y.
- Horizontal `CellSize` and vertical `HeightUnit` are independently configurable.
- Authored support and buildability exist per level, including demo levels 0, 1, and 2. Every base cell under a candidate footprint must have valid support at the candidate base level.
- Occupancy is fully three-dimensional across `Width × Depth × Height`. Static blockers occupy authored cells.
- Stacking, rotation, selling, moving, economy checks, path validation, save/load, and other tower-defense systems are outside this feature.
- A centered anchor determines the footprint. For even dimensions, the unmatched remainder extends toward positive X and positive Z.
- Placement performs atomic reserve, spawn, and rollback. A failed spawn must not leave occupancy behind.

### Architecture and feedback contract

- Use a feature-oriented module under `Assets/_Project/GridPlacement`, with mobile runtime policy under `Assets/_Project/Mobile/Runtime`.
- Keep Unity lifecycle, input, scene references, and GameObject ownership in thin `MonoBehaviour` shells; keep deterministic mapping, validation, and occupancy rules in plain C#.
- Use immutable `ScriptableObject` definitions for authored tower and board data, direct serialized references for local collaboration, a small enum for placement state, and managed flat arrays for board storage.
- Use intentional runtime and test assembly definitions with a one-way acyclic dependency graph.
- Do not introduce a singleton, dependency-injection container, event bus, Jobs/Burst, or a pooling framework for this feature.
- Show only one combined translucent candidate footprint/ghost: green when valid and red when invalid. Never render the full grid or create one renderer object per cell.
- `Assets/Scenes/SampleScene.unity` is the integration scene. `Assets/Plugins` and Unity-generated `.csproj`, `.sln`, and `.slnx` files are read-only.

## Current third-party asset boundary

The project intentionally keeps only **DOTween Pro** under `Assets/Plugins`, at `Assets/Plugins/Demigiant`. Its `DOTWEEN*` scripting defines and setup assets must be preserved unless the project owner explicitly approves removing DOTween.

The following imported asset packages are intentionally absent and must not be restored, referenced, or added to scripting defines without new explicit approval:

- More Mountains Feel.
- FImpossible Creations packages, including Legs Animator, Optimizers, Spine Animator, and Tail Animator.
- KINEMATION Retarget Pro.
- RootMotion Final IK.
- Sirenix Odin Inspector and Serializer.
- Technie Collider Creator.

This boundary applies to imported asset packages under `Assets/Plugins`; it does not authorize removing Unity Package Manager dependencies from `Packages/manifest.json`. Future agents must audit current project references and obtain approval before adding or restoring any third-party package.

### Approved implementation graph

The durable Beads graph uses these dependency-safe work packages:

1. **B1 — Mobile and identity baseline plus this decision record.** Establish the approved Android-only product constants and documentation baseline.
2. **B2 — Android Player Settings and frame-rate policy.** Apply identity, orientation, fullscreen, and non-resizable settings and add the small `MobileFrameRatePolicy` runtime owner.
3. **B3 — Grid contracts, definitions, and assembly boundaries.** Establish stable value types, immutable authored definitions, and runtime/test assembly contracts.
4. **B4 — Board mapping and authored surfaces.** Implement XZ/Y coordinate mapping, per-level support/buildability, centered footprint enumeration, and static blockers.
5. **B5 — Validation, 3D occupancy, and atomic reservation.** Validate full `Width × Depth × Height` footprints and provide reserve/spawn/rollback semantics.
6. **B6 — Touch placement controller and cancellation.** Implement primary touch with Editor mouse fallback, drag/release placement, UI-start rejection, invalid-release retention, Safe Area Cancel, Android Back, and pause/focus safety.
7. **B7 — Candidate footprint and ghost presentation.** Implement the one combined translucent green/red preview without a full-grid or per-cell renderer architecture.
8. **B8 — Serialized SampleScene integration.** Author and wire the demo board levels 0/1/2, definitions, prefab, input, materials, Safe Area UI, and scene references.
9. **B9 — Evidence-based mobile render and quality baseline.** Inspect the integrated scene and change existing render-quality settings only when current evidence justifies the change.
10. **B10 — Consolidated verification.** Verify compilation and Console state, run relevant Edit Mode and Play Mode tests, inspect Player Settings and serialized scene wiring, exercise placement behavior in the Editor where available, and refresh and verify Better Context.

Dependencies: B1 precedes B2 and B3; B3 precedes B4 and B5; B2, B4, and B5 precede B6; B3 and B5 precede B7; B2, B4, B5, B6, and B7 precede B8; B8 precedes B9; B9 precedes B10.

There is deliberately no B11 in this run. Android physical-device build, profiling, thermal validation, and 60 FPS convergence are deferred rather than inferred from Editor evidence.

## Project documentation

`Documents/` is the canonical location for human-authored project documents. Store durable, reviewable material here, including:

- Game Design Documents (GDDs).
- Technical specifications and architecture notes.
- Approved implementation plans and decision records.
- Test plans, QA reports, release notes, and operational guides.
- Research or design references that materially affect the project.

Do not use `Documents/` for generated caches, temporary agent output, Unity-generated files, credentials, or raw chat transcripts. A document should clearly state its status when relevant, such as `Draft`, `Under Review`, `Approved`, or `Superseded`.

## Documentation language

All human-authored project documentation must be written in English. This requirement applies to filenames, titles, headings, body text, field labels, tables, captions, and review notes in `Documents/`, as well as documentation files at the repository root.

Non-English names, source phrases, or direct quotations may be retained only when they are necessary for cultural or technical accuracy, and they must include a nearby English explanation.

## Commit message convention

- Use Conventional Commit prefixes such as `feat:`, `fix:`, `docs:`, `test:`, or `chore:`.
- Write the subject in Vietnamese and capitalize only its first letter. Do not use Title Case.
- Keep established technical keywords, feature names, API names, and product terminology in English when translating them would reduce clarity.
- Keep the subject concise, imperative, and without a trailing period.

Examples:

```text
feat: Thêm tutorial cho prototype
fix: Sửa grid placement trên mobile
docs: Cập nhật AI collaboration log
```

## AI collaboration records

`Documents/AICollaboration/` stores concise records of consequential collaboration with AI assistants. These records preserve decisions and validation evidence without copying an entire raw transcript.

Use the filename format:

```text
AI_Collaboration_Log_<Area>_dd_mm.md
```

Example: [`AI_Collaboration_Log_Dev_13_08.md`](Documents/AICollaboration/AI_Collaboration_Log_Dev_13_08.md).

Every entry must include:

1. **Problem being addressed** — the problem or uncertainty being addressed.
2. **Prompt used** — the relevant user prompt, summarized when it contains sensitive or repetitive content.
3. **Important AI response** — the important recommendation, evidence, or warning returned by the AI.
4. **Option selected, revised, or rejected** — the option selected, changed, or rejected.
5. **Rationale** — why that decision was made.
6. **Implementation or verification result** — the implementation or verification result.

Each log must also record the responsible chat/session ID. When several sessions contribute, identify the responsible session for each entry.

## Better Context summaries and agent handoff

Use Better Context as the first navigation layer for project work. The current generated maps are intentionally bounded, so a single broad root summary is not sufficient context for an agent that is spawned to work inside a feature.

- Prefer several concise, path-scoped summaries over one large project summary. At minimum, summarize the durable responsibility of the relevant feature folder and any important runtime, Editor, test, data, or integration boundary that is not already clear from generated evidence.
- Before reading or writing within a feature, follow the managed `AGENTS.md` chain from the repository root to the target and read every relevant stored summary along that path. A map or summary omission is not evidence that a file, asset, or behavior does not exist.
- Before spawning a worker, the parent agent or technical lead must identify the relevant Better Context map and summary paths in the worker brief. The worker must read that context before implementation so it does not begin with only the immediate filename or Bead description.
- Keep each summary factual, stable, and at most 240 characters. Describe durable responsibility or ownership; do not store temporary task status, guesses, secrets, full method inventories, or raw chat content.
- Add summaries only where they materially improve future navigation. Do not summarize every file, and do not use summaries as a substitute for inspecting current source, serialized assets, live Unity state, compilation, or tests.
- Audit existing summaries before changing them. Preserve unrelated entries, update a summary when its responsibility changes, remove obsolete paths after a verified deletion or rename, and never hand-edit `.ctx-summaries.json` or the managed blocks in `AGENTS.md`.

Write several related summaries in one batched refresh and then verify the generated maps:

```powershell
better-context-unity --root <repository-root> agents `
  --summary 'Assets/_Project/GridPlacement=Owns authored board data and mobile grid placement.' `
  --summary 'Assets/_Project/GridPlacement/Runtime=Runtime mapping, validation, occupancy, input, and presentation.' `
  --summary 'Assets/_Project/GridPlacement/Editor/BoardAuthoring=Editor-only visual Board authoring and scene synchronization.'
better-context-unity --root <repository-root> verify -v --json
```

Never run Better Context refresh, verification, generation, or Editor snapshot work while Unity is entering Play Mode or is already in Play Mode. Wait until Unity has returned to Edit Mode and is idle, then perform one batched refresh after the task's changes.

## Security and traceability

- Redact API keys, credentials, personal data, and other secrets.
- Store the session ID, not the raw transcript or a machine-specific transcript path.
- Do not mark a plan as approved unless the user or project owner explicitly approved it.
- Link validation evidence where practical, but keep generated caches and transient setup output out of this documentation hierarchy.
