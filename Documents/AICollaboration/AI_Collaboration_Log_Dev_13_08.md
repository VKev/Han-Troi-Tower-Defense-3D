# AI Collaboration Log — Development — 13/08/2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Responsible Codex sessions:**
  - `019ff86e-fd4b-7f03-adae-89a3908ac6f5` — Set up and verify the portable Unity/Codex toolchain, resolve user-controlled setup decisions, validate the installed AI tools, and establish the first AI collaboration record.
  - `019ff9b2-7ed8-7aa2-b3c3-253151a6c459` — Design, implement, verify, and version the Android-first grid placement feature and its prototype while removing the deferred Feel dependency.
- **Tracking issues:** `TowerDefense3D-769`, `TowerDefense3D-zl2`, `TowerDefense3D-do0`, `TowerDefense3D-xrk`, `TowerDefense3D-azy`, `TowerDefense3D-3xt`, `TowerDefense3D-49k`
- **Security note:** A Voyage API key was pasted into the earlier session. Its value is intentionally omitted; the exposed key was rejected and rotation was required.

This file summarizes consequential decisions from the responsible sessions. It is not a verbatim transcript.

## Entry 1 — Start the setup workflow with setup-agents

**Responsible session:** `019ff86e-fd4b-7f03-adae-89a3908ac6f5`

### Problem being addressed

The project needed a complete Unity/Codex workflow setup without losing existing work, committing or pushing prematurely, or taking control of the Unity Editor lifecycle away from the user.

### Prompt used

The user invoked the `setup-agents` specialist and requested that it perform the setup.

### Important AI response

The AI delegated the work to `setup-agents`, performed preflight checks on the exact checkout, confirmed Unity `6000.3.21f1`, Git and Beads state, inspected the ignore policy, and established checkpoints that required user confirmation.

### Option selected, revised, or rejected

- **Selected:** an idempotent phased setup with explicit Editor-open and Editor-closed checkpoints.
- **Selected:** the deterministic Beads prefix `TowerDefense3D`.
- **Rejected:** automatically launching, closing, or restarting Unity and automatically committing or pushing.

### Rationale

This preserved project state, prevented package import races, and kept important lifecycle decisions under user control.

### Implementation or verification result

Git was initialized with an unborn `HEAD` and no staged files, commits, or pushes. The ignore policy, Beads, Serena, Video Analyzer, package manifest, archive preparation, and setup checkpoints were created or verified.

## Entry 2 — Protect the Voyage credential and select the embedding model

**Responsible session:** `019ff86e-fd4b-7f03-adae-89a3908ac6f5`

### Problem being addressed

CocoIndex Code required a Voyage credential, a key had been pasted directly into chat, and the project needed a model suitable for code search.

### Prompt used

The user supplied a Voyage key, later requested `voyage-code-4` with fallback only if unavailable, and then confirmed that the resulting CocoIndex configuration should be retained. The credential value is omitted from this record.

### Important AI response

The AI warned that a credential exposed in chat must be treated as compromised and must not be written to project files, commands, or logs. It requested rotation, user-level storage of the replacement, and bounded verification of the approved model sequence.

### Option selected, revised, or rejected

- **Selected:** store the replacement credential only in user-level CocoIndex settings.
- **Selected:** use `voyage/voyage-code-4` for CocoIndex Code.
- **Revised:** remove the idea of assigning a separate Voyage model to Docling because Docling was used only as a document converter.
- **Rejected:** reuse or persist the key that appeared in chat.

### Rationale

The exposed credential was no longer safe. `voyage-code-4` was appropriate for code retrieval and passed the indexing and query compatibility gates.

### Implementation or verification result

`voyage/voyage-code-4` passed isolated doctor, real-configuration doctor, and semantic-query checks at dimension `1024`, so no fallback was needed. CocoIndex completed with 7 files, 40 chunks, and zero indexing errors. Docling `2.119.0` was separately verified for PDF, DOCX, XLSX, PPTX, and Markdown conversion.

## Entry 3 — Activate and verify Unity MCP after restarting Codex

**Responsible session:** `019ff86e-fd4b-7f03-adae-89a3908ac6f5`

### Problem being addressed

The user-level relay configuration was correct, but the existing Codex process did not expose Unity MCP tools, preventing safe continuation of Editor-dependent setup.

### Prompt used

The user reported that Unity was idle, continued setup, restarted Codex when requested, and resumed the same task.

### Important AI response

The AI instructed the user to keep Unity open, restart Codex once, and resume the existing task. After restart, it verified the exact project root, Unity process and version, Editor idle state, and live MCP registry.

### Option selected, revised, or rejected

- **Selected:** restart Codex without restarting Unity.
- **Selected:** continue Editor-dependent work only after confirming the correct project and idle state.
- **Rejected:** assuming that configuration files alone proved the runtime MCP connection was active.

### Rationale

MCP registration required a new Codex process, while keeping Unity open avoided unnecessary package resolution and import work.

### Implementation or verification result

Codex exposed all 54 Unity MCP tools. The registry reported 54 registered, 54 enabled, and 54 advertised tools for the correct project without another restart.

## Entry 4 — Repair exact export of the 54 MCP schemas

**Responsible session:** `019ff86e-fd4b-7f03-adae-89a3908ac6f5`

### Problem being addressed

The initial registry exporter serialized some adapted schemas as C# debug text, which prevented the strict JSON and schema-equality gate from passing.

### Prompt used

The user authorized the exporter repair, test stub, and bridge schema repair in sequence.

### Important AI response

Two direct serialization approaches failed because the dynamic compiler could not access the required namespace or assembly. The AI proposed using the public `UnityMCPBridge.PrintToolSchemas()` surface, temporarily snapshotting and restoring clipboard and logger state, and merging evidence by exact tool name.

### Option selected, revised, or rejected

- **Selected:** bridge-based schema capture and the three approved setup-tool repairs.
- **Revised:** the test harness to emulate the public bridge, clipboard, and logger surfaces.
- **Rejected:** reflection, `System.ComponentModel`, and direct Newtonsoft calls inside the dynamic command.

### Rationale

The public bridge used the same runtime serialization path as Unity Assistant without adding a package or changing game code.

### Implementation or verification result

Exporter tests passed 4 of 4 and catalog tests passed 3 of 3. Live export and a genuine relay-client capture both returned 54 tools with no duplicates, disabled entries, or schema mismatches. Clipboard and logger state were restored, and catalog equality completed with `complete/exact=true`.

## Entry 5 — Install packages and handle Script Updating Consent

**Responsible session:** `019ff86e-fd4b-7f03-adae-89a3908ac6f5`

### Problem being addressed

The project required ZLinq, VContainer, and ten serialized asset imports. Unity repeatedly displayed Script Updating Consent for Legs Animator files that had already received hash-verified compatibility patches.

### Prompt used

The user reported choosing **No** on the dialog, requested continuation, and later prohibited Computer Use.

### Important AI response

The AI recommended **No** to prevent Unity from mutating vendor files and invalidating registered hashes. After Computer Use was prohibited, subsequent checks used CLI and Unity MCP while the user handled any modal dialogs manually.

### Option selected, revised, or rejected

- **Selected:** import packages serially and verify hashes, compilation, and Console state after each boundary.
- **Selected:** let the user choose **No** on consent dialogs.
- **Rejected:** choosing a **Yes** option, automatic source updating, evidence-free reimport, or later UI automation.

### Rationale

Compatibility outputs had already been content-addressed and tested; API Updater mutations could create unreviewed changes outside that profile.

### Implementation or verification result

NuGetForUnity `4.5.0`, ZLinq and ZLinq.Unity `1.5.6`, VContainer `1.19.0`, and the pinned UPM graph were verified. Ten asset packages were imported and checked, DOTween setup converged deterministically, and Legs and Retarget hashes remained unchanged after the dialogs were rejected.

## Entry 6 — Functionally verify each AI context tool

**Responsible session:** `019ff86e-fd4b-7f03-adae-89a3908ac6f5`

### Problem being addressed

The project needed evidence that its context and indexing tools returned real project-owned results, beyond merely exposing the correct Unity MCP schemas.

### Prompt used

The user asked whether every tool worked and requested individual testing for any tool that had not been functionally checked.

### Important AI response

The AI explained that destructive, generation, cloud, and profiler operations should not be run solely as smoke tests. It then performed focused read-only checks for Beads, Better Context, CodeGraph, CocoIndex, and Serena around the same `Readme` source area.

### Option selected, revised, or rejected

- **Selected:** comparable read-only checks on one project-owned source region.
- **Selected:** CocoIndex queries with `refresh_index=false`.
- **Rejected:** destructive calls, credit-consuming generation, profiler queries without runtime data, and unauthorized Serena onboarding memories.

### Rationale

This demonstrated working integrations without mutating game code, consuming cloud credits, or inventing unavailable Play Mode evidence.

### Implementation or verification result

Beads commands succeeded with a valid empty ready queue. Better Context returned the `Readme` and nested `Section` after a bounded retry. CodeGraph found 14 symbols and the correct dependency. CocoIndex returned three project-owned semantic hits. Serena returned the live symbol overview and created only ignored local metadata.

## Entry 7 — Establish documentation and AI collaboration traceability

**Responsible session:** `019ff86e-fd4b-7f03-adae-89a3908ac6f5`

### Problem being addressed

The project lacked a root documentation guide and a defined place for game design, technical specifications, approved plans, and consequential AI decisions.

### Prompt used

The user requested a root README, `Documents/AICollaboration`, a dated log, six required decision fields per entry, and the responsible session ID.

### Important AI response

The AI proposed `Documents` as the canonical human-authored documentation root, `Documents/AICollaboration` for concise decision records, session IDs instead of raw transcripts, and mandatory secret redaction.

### Option selected, revised, or rejected

- **Selected:** root `README.md` and `Documents/AICollaboration/AI_Collaboration_Log_Dev_13_08.md`.
- **Selected:** session `019ff86e-fd4b-7f03-adae-89a3908ac6f5` as the owner of the initial entries.
- **Rejected:** committing raw Codex JSONL or machine-specific transcript paths.

### Rationale

A concise decision log is reviewable and traceable, while a raw transcript contains tool noise, machine-local paths, and potentially sensitive content.

### Implementation or verification result

The root documentation guide and first AI collaboration log were created with session responsibility, security policy, and the required entry structure. Markdown links and a generic secret scan were checked before handoff.

## Entry 8 — Approve the Android-first grid placement design

**Responsible session:** `019ff9b2-7ed8-7aa2-b3c3-253151a6c459`

### Problem being addressed

The project needed an implementation-ready design for placing towers on an XZ ground grid with discrete vertical Y levels, configurable three-dimensional footprints, occupancy protection, and candidate-only feedback suitable for a landscape mobile game.

### Prompt used

The user requested a complete plan with the technical lead, clarified that footprint width and depth are horizontal while height is vertical, selected the recommended architecture choices, changed confirmation to release-to-place, approved Android first, set the application ID to `vng.khanghv2.nextgen.towerdefense3d`, required fullscreen landscape, and targeted 60 FPS on mid-range devices.

### Important AI response

The AI proposed a feature-oriented `Assets/_Project/GridPlacement` module with deterministic plain-C# mapping, validation, occupancy, and reservation rules behind thin Unity input and presentation components. Authored board and tower data would use ScriptableObjects, occupancy would cover the full three-dimensional volume, and the preview would use one combined translucent green or red candidate instead of rendering the full grid.

### Option selected, revised, or rejected

- **Selected:** touch-first drag and release confirmation, Editor mouse fallback, invalid candidate retention, Safe Area Cancel, Android Back cancellation, XZ ground coordinates, and independent vertical height units.
- **Selected:** atomic reserve, spawn, commit, and rollback behavior.
- **Revised:** confirmation changed from a button to immediate revalidation and placement on release.
- **Rejected:** full-grid rendering, per-cell preview renderers, stacking, rotation, path validation, selling, moving, and iOS work in this feature.

### Rationale

The selected design keeps domain rules deterministic and testable while limiting runtime allocations, draw calls, coupling, and touch ambiguity on mobile hardware.

### Implementation or verification result

The approved contract and dependency-safe B1-B10 implementation graph were recorded in `README.md`. B11 was explicitly removed, leaving physical Android build, profiling, thermal testing, and final 60 FPS convergence as deferred work.

## Entry 9 — Implement and verify the grid placement feature

**Responsible session:** `019ff9b2-7ed8-7aa2-b3c3-253151a6c459`

### Problem being addressed

The approved feature needed to be implemented without losing work during a Unity crash, while respecting the user's request to parallelize independent Beads and serialize shared Unity import and compilation boundaries.

### Prompt used

The user instructed the technical lead to read the root README, update documentation, spawn Unity developers, implement the approved plan, avoid Git commits until completion, continue after Unity reopened, and run B2/B3, B4/B5, and B6/B7 in parallel whenever Editor ownership allowed it.

### Important AI response

The technical lead converted the plan into durable Beads, preserved the warning baseline, isolated source ownership between workers, serialized Unity import and domain reload steps, retired non-progressing workers, and reassigned bounded implementation work when necessary. After Unity restarted, work resumed from files already written instead of restarting the feature.

### Option selected, revised, or rejected

- **Selected:** parallel source implementation for independent Beads with one serialized Unity compile and verification lane.
- **Selected:** replacement workers after bounded non-progress audits, without reverting or duplicating existing edits.
- **Selected:** a scene-scoped controller, three-dimensional occupancy, ScriptableObject definitions, combined footprint preview, Edit Mode tests, Play Mode tests, and a generated integration scene.
- **Rejected:** concurrent Unity Editor mutation, unbounded worker exploration, and commits before feature acceptance.

### Rationale

Unity uses a shared AssetDatabase, compiler, and domain-reload lane even when source files are disjoint. Parallelizing file ownership while serializing Editor verification preserved throughput without introducing import races.

### Implementation or verification result

The implementation produced the runtime contracts, board mapping, occupancy and reservation logic, controller, preview, mobile frame-rate policy, test assemblies, authored definitions, tower prefab, materials, and `SampleScene` integration. Final Unity validation reported an idle Editor, zero Console errors and warnings, six prefabs with zero missing scripts or managed-reference failures, and an intact active scene. Better Context was regenerated and verified.

## Entry 10 — Remove More Mountains Feel from the project

**Responsible session:** `019ff9b2-7ed8-7aa2-b3c3-253151a6c459`

### Problem being addressed

The project still contained More Mountains legacy Post Processing and the broader Feel package even though the grid placement feature did not depend on them, creating unnecessary size and maintenance overhead.

### Prompt used

The user first requested removal of More Mountains legacy Post Processing, then explicitly requested removal of the entire Feel package. When Unity displayed the unrelated Script Updating Consent dialog for FImpossible Legs Animator files, the user selected **No** and reported that Unity was compiling.

### Important AI response

The AI audited the package before deletion, measured 5,019 files and 2,755 asset GUIDs, found no serialized GUID references outside Feel, removed stale Nice Vibrations scripting defines through Unity Player Settings, and deleted `Assets/Plugins/Feel` through the Unity AssetDatabase.

### Option selected, revised, or rejected

- **Selected:** remove the complete Feel package and its folder metadata after dependency evidence showed it was unused.
- **Selected:** reject the unrelated API Updater mutation by choosing **No**.
- **Rejected:** retaining legacy post-processing, editing unrelated FImpossible vendor sources, or deleting through an unsafe shell operation while Unity owned the AssetDatabase.

### Rationale

Unity AssetDatabase deletion preserved asset bookkeeping, while the pre-delete GUID and source audits reduced the risk of leaving broken project-owned references.

### Implementation or verification result

Feel and `Feel.meta` were removed, the stale `MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED` define was absent, and project-owned searches found no remaining More Mountains, MMFeedbacks, Nice Vibrations, or Lofelt dependency. Unity completed compilation with zero errors and warnings; the remaining prefabs and `SampleScene` had zero missing scripts and zero missing managed references.

## Entry 11 — Create an atomic three-commit project history

**Responsible session:** `019ff9b2-7ed8-7aa2-b3c3-253151a6c459`

### Problem being addressed

The repository had an unborn `master` branch and all project files were untracked, but the user required a reviewable history that separated the initial mobile project, the grid feature, and the prototype plus documentation.

### Prompt used

The user requested commits made with the configured account, split atomically into initialization before grid implementation, grid placement implementation, and prototype plus documents. Commit subjects had to follow Conventional Commit prefixes with an uppercase first letter after the prefix.

### Important AI response

The AI verified the configured identity as `Khang. Huỳnh Vương (2) <khanghv2@vng.com.vn>`, audited ignored documentation and large files, staged explicit ownership boundaries, kept generated AI maps and local Vercel metadata out of Git, and created three commits without pushing.

### Option selected, revised, or rejected

- **Selected:** `feat: Initialize mobile project` for the Unity/mobile foundation without grid assets or documentation.
- **Selected:** `feat: Implement grid placement` for the feature module, integration scene, tests, and Build Settings.
- **Selected:** `feat: Add prototype and documents` for the browser prototype and human-authored records.
- **Rejected:** one monolithic initial commit, including `.vercel`, committing ignored generated agent maps, or pushing without a configured remote.

### Rationale

The three boundaries allow reviewers to inspect the base project independently from gameplay implementation and supporting design artifacts while preserving the user's requested chronology.

### Implementation or verification result

Commits `0a47592`, `8649c31`, and `8c7445e` were created in order with the configured author. The prototype smoke test passed, the final Git worktree was clean, Feel was absent from the committed tree, and no remote push occurred.

## Entry 12 — Design and implement visual BoardDefinition authoring

**Responsible session:** `019ff9b2-7ed8-7aa2-b3c3-253151a6c459`

### Problem being addressed

The serialized `BoardDefinition` cell list was difficult for a Game Designer to understand and edit because it did not expose the board as visible cells arranged by horizontal coordinates and vertical level.

### Prompt used

The user requested a visual matrix whose Width and Depth are designer-controlled, where painting a cell assigns its gameplay type and semantic color, with a selector for the active Y level. The user approved the proposed plan and requested implementation through the technical-lead workflow.

### Important AI response

The AI proposed an Editor-only Board Painter that presents one Width-by-Depth XZ matrix for the selected Y level while preserving the existing sparse runtime `BoardCellDefinition[]` schema. A separate document/model would handle deterministic serialization, validation, resize preservation, crop confirmation, and Undo-aware stroke commits, while a thin `EditorWindow` and custom `BoardDefinition` Inspector would coordinate the UI.

### Option selected, revised, or rejected

- **Selected:** five fixed semantic paint presets: Empty, Buildable, No-Build, Blocked Surface, and Volume Blocker.
- **Selected:** click-drag painting, erasing, a Y-level selector, intersection-preserving resize, destructive-crop confirmation, Undo/Redo reload, and deterministic Y-Z-X sparse output.
- **Selected:** return an existing `DemoBoard.asset` before any seed assignment so authored cells cannot be overwritten by the demo builder.
- **Rejected:** changing the runtime board schema, adding runtime grid rendering, introducing a color picker, or deriving authoring data from terrain and colliders.

### Rationale

Keeping the tool in an Editor-only assembly gives designers a direct visual workflow without increasing player-build size or coupling runtime placement logic to Unity Editor APIs. Preserving the sparse runtime format avoids a migration, while fixed presets make cell meaning consistent and testable across levels.

### Implementation or verification result

The Board Painter window, custom Inspector entry point, preset mapping, document/model, Editor assembly, demo-builder preservation guard, and nine Edit Mode regression tests were added and accepted through static source review. Final Unity Test Runner execution, live painter interaction, save/reopen and Undo verification, `DemoBoard.asset` checksum confirmation, and the final Better Context refresh remain tracked by `TowerDefense3D-3xt`.

## Entry 13 — Prevent Better Context from exporting during Play Mode

**Responsible session:** `019ff9b2-7ed8-7aa2-b3c3-253151a6c459`

### Problem being addressed

Entering or leaving Unity Play Mode caused Better Context to export a large Editor snapshot again, interrupting gameplay and adding unnecessary Editor work.

### Prompt used

The user requested that Better Context stop starting automatically when Unity enters Play Mode.

### Important AI response

The AI traced the behavior to the Editor bridge polling a persistent `editor-request.json` while remembering the processed nonce only in a static field. Domain reload reset that field, so the old request appeared new. The pinned package and its current official `main` source exposed no setting for this behavior.

### Option selected, revised, or rejected

- **Selected:** embed the approved pinned Better Context Editor package through Unity Package Manager.
- **Selected:** suspend request polling while Unity is playing or about to enter Play Mode.
- **Selected:** preserve the last processed nonce with `UnityEditor.SessionState` and delete the default request after a successful response.
- **Rejected:** editing the transient `Library/PackageCache` copy, disabling Better Context completely, or running a refresh during gameplay.

### Rationale

An embedded package makes the local compatibility fix durable and reviewable. The Play Mode guard prevents expensive asset scans during gameplay, while nonce persistence and one-shot request cleanup prevent the same request from being replayed after domain reload.

### Implementation or verification result

The package was embedded from the approved `1.6.0` source and patched. Unity recompiled successfully with zero Console errors or warnings. A live enter-and-exit Play Mode test left `editor-request.json` absent and preserved the exact Editor snapshot timestamp and SHA-256 both during and after gameplay, with no Better Context export log. The root agent instructions now prohibit Better Context refreshes while Unity is entering or running Play Mode.

## Entry 14 — Synchronize Board-authored geometry into the scene

**Responsible session:** `019ff9b2-7ed8-7aa2-b3c3-253151a6c459`

### Problem being addressed

Changing the renamed `Board.asset` did not update the visual and collider geometry under `Board Origin`, and the old one-shot demo rebuild tool could overwrite or drift from designer-authored board data.

### Prompt used

The user requested that every Board update synchronize the children of `Board Origin` to the current dimensions, cells, and vertical levels. The user also requested a `Visualize In Scene` toggle that controls renderers while retaining colliders for placement, and removal of the legacy grid demo rebuild tool.

### Important AI response

The AI proposed an Editor-only delayed synchronizer backed by a pure deterministic geometry planner. The planner merges authored cells into rectangles per Y level and geometry kind, while a small runtime presenter owns the Board reference and renderer visibility. The synchronizer owns only a dedicated generated child root, preserves manual siblings, remains idempotent through a content signature, and defers work during Play Mode or compilation.

### Option selected, revised, or rejected

- **Selected:** use `Board.asset` as the source of truth and `Board Origin` only as the coordinate anchor.
- **Selected:** generate merged placement surfaces and blocker volumes with persistent project materials, enabled colliders, and renderer state driven by `Visualize In Scene`.
- **Selected:** synchronize after Board Painter commits, Inspector edits, Undo/Redo, and Board asset imports through a coalesced delayed callback.
- **Rejected:** per-cell GameObjects, runtime `Update` polling, disabling colliders when visualization is off, replacing manual scene children, or retaining the legacy rebuild menu.

### Rationale

Deterministic rectangle merging keeps the authored scene readable and reduces GameObject, renderer, and collider counts for the Android-first target. Separating pure planning, Editor synchronization, and runtime visibility keeps player builds free of Editor code and prevents scene serialization work during gameplay.

### Implementation or verification result

`BoardScenePresenter`, the geometry planner, scene synchronizer, delayed change scheduler, Inspector and Board Painter hooks, and focused regression tests were implemented. `SampleScene` now contains one generated Board geometry root under `Board Origin`, wired to the existing `Board.asset` GUID, with five merged geometry objects for the current 20-by-8 board and authored Y levels. Live Unity checks confirmed that disabling visualization leaves colliders enabled and raycastable, repeated synchronization reuses existing geometry, and the legacy rebuild menu is absent. The focused suites passed with 15 Edit Mode tests and 3 Play Mode tests, with zero failures.

## Entry 15 — Improve delegated context and Board visualization clarity

**Responsible session:** `019ff9b2-7ed8-7aa2-b3c3-253151a6c459`

### Problem being addressed

Spawned agents were receiving too little durable project context, and the generated Board surfaces and blockers still appeared opaque even though they were intended as optional scene visualization. The completed implementation also needed an auditable, atomic Git history without restoring the removed Feel package.

### Prompt used

The user requested a README policy requiring several Better Context summaries for both reading and writing, slightly transparent Board visualization materials, an updated AI collaboration record, and commits following the repository convention. The user also confirmed that Feel must remain removed.

### Important AI response

The AI established a path-scoped summary policy and stored seven summaries through the Better Context CLI for the GridPlacement feature, its runtime, Editor tooling, BoardAuthoring, data, tests, and scene integration. It identified that changing color alpha alone was insufficient because `Ground.mat` and `Blocker.mat` still used URP Opaque rendering, and it audited the dirty Git state before defining atomic commit boundaries.

### Option selected, revised, or rejected

- **Selected:** require parent agents and technical leads to include relevant Better Context map and summary paths in spawned-worker briefs.
- **Selected:** convert only `Ground.mat` and `Blocker.mat` to URP Transparent Alpha blending at opacity `0.7`, render queue `3000`, and `ZWrite` disabled.
- **Selected:** preserve `PreviewTransparent.mat`, the legacy height materials, collider behavior, and shared-material usage.
- **Selected:** remove the stale `MOREMOUNTAINS_NICEVIBRATIONS_INSTALLED` define and keep the deleted Feel package absent.
- **Rejected:** hand-editing generated Better Context maps, changing alpha without changing the URP surface mode, restoring Feel-related configuration, or creating one monolithic commit.

### Rationale

Several concise summaries give delegated agents durable feature, ownership, and integration context without copying source code into prompts. Correct URP transparent state makes the designer-only geometry readable while retaining collision behavior. Separate fix, feature, visualization, and documentation commits keep review and rollback boundaries clear.

### Implementation or verification result

The README now documents the summary and handoff policy, and Better Context stored seven path-scoped summaries with a final `is_stale: false` verification. Live Unity inspection confirmed five generated renderers using the shared translucent materials while all Board colliders remained enabled; Scene View showed background and overlap blending, and the Console remained at zero errors and warnings. Feel and its scripting define were absent. The implementation was recorded in commits `488393a` (`fix: Ngăn Better Context chạy trong Play Mode`), `5376cc4` (`feat: Thêm Board authoring và đồng bộ scene`), and `90da521` (`feat: Làm trong suốt Board visualization`); this entry and the README policy are recorded in the following atomic documentation commit.
