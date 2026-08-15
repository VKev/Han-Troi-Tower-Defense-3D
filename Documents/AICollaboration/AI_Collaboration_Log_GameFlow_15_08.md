# AI Collaboration Log — GameFlow — 15 August 2026

## Entry 1 — Add level scenes, local unlock saves, and UI managers

**Responsible session:** `019ff9b2-7ed8-7aa2-b3c3-253151a6c459`

### Problem being addressed

The project had one gameplay scene and scene-owned placement UI, but no application-level entry flow, designer-authored level catalog, persistent unlock progress, retryable scene transitions, or clear lifetime boundary between application UI and gameplay UI. The existing Grid Placement UI also needed to migrate without changing placement behavior.

### Prompt used

The user requested a technical-lead design and implementation for a scene-per-level system, a local save system that stores unlocked levels, and UI managers that migrate the existing placement UI. The approved interaction requires the game to open on a Level Menu, Level 1 to load on its first selection, and a locked later level to unlock on its first selection and load on its second. The user also required Retry for load or save failures, Start New for unrecoverable save data, no win or loss progression yet, Android-first verification, Beads tracking, Technical Spec updates, and atomic commits by system.

### Important AI response

The AI and TechLead recommended a persistent Bootstrap composition root plus exactly one additive `Level_###` scene, a catalog addressed by full scene paths, unload-first transitions for mobile memory, a transactional autosave with primary and backup recovery, and two UI lifetimes. `ApplicationUIManager` owns the persistent Level Menu and error/loading overlays; `GameplayUIManager` owns only the current level's placement HUD and forwards commands to Grid Placement. Stable contracts and assembly boundaries were implemented before serialized scene migration and tests.

### Option selected, revised, or rejected

- **Selected:** Bootstrap is the first scene and the only owner of application services, EventSystem, mobile frame-rate policy, and application UI.
- **Selected:** each level has one scene, one `LevelSceneContext`, one independent Board asset, and one level-scoped gameplay UI manager.
- **Selected:** Level 1 starts unlocked and loads on its first tap; a locked later level unlocks and autosaves on its first tap, then loads on its next tap.
- **Selected:** save only unlocked level numbers using one local autosave slot with primary, backup, and same-directory temporary files.
- **Selected:** expose Retry for transition failures, Retry Save for write failures, and confirmed Start New when both primary and backup data are unusable.
- **Selected:** migrate tower selection, cancel placement, instructions, Safe Area, and Return to Level Menu into focused UI views managed by the new UI coordinators.
- **Selected:** use native `SceneManager` additive loading and explicit serialized composition without Addressables, VContainer, a service locator, or a global event bus.
- **Rejected:** automatic resume, automatic next-level unlock, persisted tower or camera state, win/loss progression, multiple save slots, cloud save, and iOS work in this phase.

### Rationale

Bootstrap provides a stable recovery boundary while level scenes remain disposable and self-contained. Unload-first transitions reduce mobile peak memory. A catalog prevents scene identifiers from spreading through UI code, and transactional save replacement avoids applying partially read or partially written progress. Splitting persistent and level-scoped UI ownership prevents stale references after scene unload while keeping Grid Placement rules in their existing gameplay module. The selected behavior also satisfies the user's explicit two-step unlock interaction without inventing a completion system.

### Implementation or verification result

The GameFlow runtime, editor validation, Bootstrap, Level 1 and Level 2 scenes, independent Board assets, shared UI prefabs, and automated tests were implemented against the approved specification and tracked through `TowerDefense3D-07y`. Unity `6000.3.21f1` passed 10 GameFlow EditMode tests, 4 GameFlow PlayMode tests, 39 Grid Placement EditMode regression tests, and 5 Grid Placement PlayMode regression tests. Serialized validation found zero catalog errors and confirmed exclusive application ownership in Bootstrap plus independent level ownership.

An Android Development APK containing Bootstrap, Level 1, and Level 2 built successfully with 0 errors and 1 non-blocking Android SDK XML-version warning. The APK is 48,354,304 bytes. CMake `3.22.1` was installed to resolve the initial environment blocker, and the successful build used Unity's matching embedded OpenJDK and NDK. Physical-device touch, Safe Area/cutout, filesystem interruption, memory, thermal, and 60 FPS validation remain explicitly pending and are not inferred from Editor tests or build success.
