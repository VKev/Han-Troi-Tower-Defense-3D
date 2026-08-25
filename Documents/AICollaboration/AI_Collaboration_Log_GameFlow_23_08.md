# AI Collaboration Log — GameFlow — 23 August 2026

This session also included non-code-changing exchanges explaining the `Application/Flows` split, `LevelSceneLoader` pipeline, facade versus interface, the VContainer-injected `Internal/` collaborators, the level participant flow, UI trade-offs, and the distinctions among screens, views, controls, presenters, and adapters. They are summarized with the consequential decisions below rather than reproduced as a raw transcript.

## Entry 1 — Split LevelSceneLoader into focused collaborators and reorganize the Levels folder

**Responsible session:** `5c506956-73be-4876-ac30-2e3fccb81468` (Claude Code)

### Problem being addressed

`LevelSceneLoader.cs` combined transition bookkeeping (busy guard, completion-waiter queue), the additive load pipeline, the unload pipeline, and bootstrap-scene fallback in a single ~410-line `MonoBehaviour`, mirroring the multi-responsibility problem the existing `Application/Flows` split had already solved for `GameFlowCoordinator`. The `Levels` folder also mixed level-lifecycle internals with the two scene-participant adapters.

### Prompt used

The user asked how the `Application` coordinator/flow split was typically done and whether `LevelSceneLoader` could be split the same way, approved the proposed plan, then asked to execute it and to move the two scene adapters into their own `Adapter` folder.

### Important AI response

Recommended extracting `ActiveLevelState` (single source of truth for the active level scene/context), `BootstrapSceneActivator` (bootstrap-scene fallback), `LevelLoadSequence`, and `LevelUnloadSequence` as plain C# collaborators driven by coroutines that the `MonoBehaviour` starts, keeping `LevelSceneLoader` itself as a thin shell holding only the busy-guard/waiter-queue bookkeeping and the public API. Recommended against extracting the short, single-use `FindLevelContexts` helper into its own file.

### Option selected, revised, or rejected

- **Selected:** extract four focused plain C# classes; keep `LevelSceneLoader` as a thin coroutine-starting shell.
- **Selected:** move `GridPlacementSceneAdapter.cs`/`TowerNetworkSceneAdapter.cs` into `Levels/Adapter/` via `git mv`, preserving `.meta` GUIDs.
- **Revised:** the four extracted classes were later relocated by the project owner into `Levels/Internal/` and wired through VContainer `[Inject]` method injection (registered as singletons in `ApplicationLifetimeScope`) instead of being constructed manually in `Awake()`.
- **Rejected:** keeping `FindLevelContexts` as its own file.

### Rationale

Matches the existing `Application/Flows` precedent (thin orchestrator, focused plain-C# workers) and the project's stated preference against interface layers with no polymorphic need. The later move to `Internal/` plus VContainer injection brings `LevelSceneLoader`'s wiring mechanism in line with how the rest of the composition root resolves its collaborators.

### Implementation or verification result

`LevelSceneLoader.cs` shrank from ~413 lines to a thin shell; `ActiveLevelState.cs`, `BootstrapSceneActivator.cs`, `LevelLoadSequence.cs`, `LevelUnloadSequence.cs` now live under `Levels/Internal/`; the adapters live under `Levels/Adapter/`. All moves used `git mv` with new `.meta` GUIDs generated only for genuinely new files. Editor diagnostics reported zero errors on every touched file after each edit. No automated test run was performed in this session; Unity Editor re-import/recompile remains owner-pending.

## Entry 2 — Move GameFlowState and LevelSceneContext into their owning folders

**Responsible session:** `5c506956-73be-4876-ac30-2e3fccb81468` (Claude Code)

### Problem being addressed

`GameFlowState.cs` and `LevelSceneContext.cs` still sat at the flat `GameFlow/Scripts/` root left over from before the `Application/Flows` and `Levels/Internal` splits, even though each is now exclusively owned by one of those areas.

### Prompt used

The user asked to move `GameFlowState` into the `Application` folder and `LevelSceneContext` into `Internal`.

### Important AI response

Confirmed both types are referenced only within their target area's namespace (same `TowerDefense3D.GameFlow` namespace throughout, so the move is organizational only) before moving, and flagged that `LevelSceneContext` must stay `public` even inside an `Internal` folder because it is added via the Inspector in every level scene and referenced cross-assembly by `LevelCatalogValidator` (Editor assembly) and by EditMode/PlayMode tests — folder placement does not change C# accessibility rules or assembly boundaries.

### Option selected, revised, or rejected

- **Selected:** `git mv` `GameFlowState.cs`(`.meta`) into `Application/`.
- **Selected:** `git mv` `LevelSceneContext.cs`(`.meta`) into `Levels/Internal/`.
- **Revised:** the project owner subsequently relocated `LevelSceneContext.cs` from `Levels/Internal/` back to `Levels/` directly, which this session treated as the deliberate final placement rather than reverting it.
- **Rejected:** changing `LevelSceneContext`'s access modifier to `internal` to match the folder name, since no `InternalsVisibleTo` exists between the runtime assembly and the Editor/test assemblies that reference it by type.

### Rationale

Folder location in this codebase is purely organizational (namespaces are stable across the move), so relocating both files to the folder that actually owns them improves navigability without any behavioral or compilation risk, provided `.meta` GUIDs are preserved through `git mv`.

### Implementation or verification result

Both moves preserved their original `.meta` GUIDs. Editor diagnostics reported zero errors on `Application/GameFlowState.cs` and on `LevelSceneContext.cs` at its final location. No code changes were needed since the namespace was unchanged.

## Entry 3 — Remove defensive conditions unreachable given the current call graph

**Responsible session:** `5c506956-73be-4876-ac30-2e3fccb81468` (Claude Code)

### Problem being addressed

Across `LevelSceneLoader`, `LevelLoadSequence`, `BootstrapSceneActivator`, `LevelSceneContext`, and `GameplayUIManager`, several `if` guards protected against caller-misuse scenarios (invalid arguments, re-entrant calls, double initialization) that no current caller in the project can actually trigger, adding branching without a corresponding real risk.

### Prompt used

The user asked to remove a check only when it is certain, given the current project code, that the guarded case cannot happen — explicitly rejecting "what if someone calls it wrong" reasoning — and repeated the same instruction in later turns specifically for `LevelLoadSequence` and again for `GameplayUIManager`.

### Important AI response

For each guard, traced every actual caller in the codebase (not a hypothetical one) before deciding. `LevelSceneLoader`'s busy-guard/waiter-queue system is unreachable because `GameFlowCoordinator`'s `GameFlowState` gate always serializes calls before any coroutine yields. `LevelLoadSequence`'s "foreign scene already loaded" check is unreachable because `LevelSceneLoader` is the sole additive loader for level scenes in the project. `LevelSceneContext.TryInitialize`'s re-entry and `LevelSceneRuntimeContext.IsValid` guards are unreachable because the struct is constructed once, is always valid, and each context instance is initialized exactly once per level-load lifecycle. The identical `IsValid` check and a redundant pre-init `Shutdown()` call in `GameplayUIManager.Initialize` follow the same proof, as does a redundant guard at the top of `RefreshTowerNetworkHud` once every call site was traced. Checks guarding real, still-possible failure modes — Build Settings misconfiguration, missing/duplicate `LevelSceneContext`, participant-array authoring mistakes, and any result of an asynchronous Unity engine call (`LoadSceneAsync`/`UnloadSceneAsync`/`SetActiveScene`) — were explicitly kept and justified individually, since mobile scene-load failures under memory/IO pressure are a real production risk, not a hypothetical one.

### Option selected, revised, or rejected

- **Selected:** remove `LevelSceneLoader`'s entire transition/waiter-queue bookkeeping and its upfront parameter validation in `LoadLevel`.
- **Selected:** remove `LevelLoadSequence`'s foreign-scene-ownership check; consolidate its remaining repeated `new LevelTransitionResult(status, request.LevelNumber, message)` construction into a local `Fail(...)` helper once none of its eight remaining checks could be proven unreachable.
- **Selected:** remove `BootstrapSceneActivator`'s "bootstrap not loaded" precheck and `CleanupFailedTarget`'s scene-validity precheck.
- **Selected:** remove `LevelSceneContext`'s double-init and `IsValid` guards; simplify `ShutdownInitializedParticipants`'s type check to a direct cast, since only already-verified indices are ever iterated.
- **Selected:** remove `GameplayUIManager`'s `IsValid` guard, redundant pre-init `Shutdown()` call, and `RefreshTowerNetworkHud`'s redundant null/initialized guard.
- **Rejected:** removing `try`/`catch` blocks and post-await result checks around Unity scene APIs, since those guard engine/content unpredictability rather than caller misuse.

### Rationale

The distinction the user drew — reachable given the actual call graph versus merely "someone might call this wrong" — is a sound, provable bar: every removed check was traced to zero possible callers under the current architecture, while every retained check maps to a real content-authoring gap or an unproven third-party engine outcome. Keeping the latter matches the project's mobile-first constraints, where scene-load failures under memory/thermal pressure are realistic, not speculative.

### Implementation or verification result

`LevelSceneLoader.cs` reduced from ~165 to 46 lines with the busy-guard subsystem removed. `LevelLoadSequence.cs`, `BootstrapSceneActivator.cs`, `LevelSceneContext.cs`, and `GameplayUIManager.cs` each lost one to three unreachable branches with no behavior change on any retained path, confirmed by an exhaustive caller search across `Assets/Scripts` showing no test or production call site exercises any removed branch. Editor diagnostics reported zero errors after each edit. `LevelTransitionStatus.Busy`/`InvalidLevel`/`Cancelled` are now unused enum members in `LevelFlowContracts.cs`; left untouched as out of scope for this change. No automated test run was performed in this session.

## Entry 4 — Replace UI god classes and runtime layout construction with authored screen/view composition

**Responsible session:** `01a02a90-cb3e-7523-97dd-8f9f705f3685` (Codex)

### Problem being addressed

`GameplayUIManager` and `ApplicationUIManager` had accumulated hierarchy construction, serialized reference ownership, input wiring, state projection, screen visibility, navigation, and lifecycle work. The tower HUD layout was also constructed in code, making the actual runtime hierarchy difficult to inspect or edit in Unity and encouraging the managers to grow into god classes.

### Prompt used

The user asked how to refactor the oversized `GameplayUIManager`, approved the trade-offs, required every extracted script to be placed in the correct folder, and required the tower layout to be built in the scene or prefab rather than in code. The user then requested the same treatment for `ApplicationUIManager`, asked what a screen means, and extended the impossible-condition audit to the related Application and Levels scripts while excluding adapters.

### Important AI response

Recommended treating each manager as a small lifecycle or presentation facade. A screen is one coherent page or modal root, such as the level menu, loading state, or blocking error; a view is a smaller rendering or interaction component within a screen. Gameplay HUD state projection belongs in a plain C# presenter and immutable state snapshot, while Unity rendering and pointer handling belong in focused `MonoBehaviour` views and controls. Application UI should delegate to authored screen roots and smaller views instead of owning their internal behavior. Runtime layout builders should be removed so the prefabs and their serialized references remain authoritative.

### Option selected, revised, or rejected

- **Selected:** keep `ApplicationUIManager` as the persistent Bootstrap-owned `IApplicationUIController` facade.
- **Selected:** move `LevelMenuScreen`, `LoadingScreen`, and `BlockingErrorScreen` under `Application/Screens/`, with `LevelButtonView`, `SaveWarningView`, and `StartNewConfirmation` under `Application/Views/`.
- **Selected:** keep `GameplayUIManager` as the level-scoped lifecycle coordinator and extract `TowerNetworkHudState`, `TowerNetworkHudPresenter`, and focused Unity-facing components.
- **Selected:** author `ApplicationUI.prefab` and `GameplayUI.prefab` directly and remove the runtime tower HUD layout builder.
- **Selected:** retain role-specific names: composite regions use `*View`, atomic input components use `*Button`, full UI roots use `*Screen`, and state transformation uses `*Presenter`.
- **Selected:** remove a guard only after the actual caller graph proves its state unreachable; retain guards for real asynchronous scene results, teardown order, simulation state, selection state, and touch/pointer transitions.
- **Rejected:** force every presentation component to use the `*View` suffix or let either manager own layout creation, every interaction, every screen, and gameplay decisions.

### Rationale

Authored layout makes hierarchy, anchors, references, and Safe Area placement visible in Unity. Focused screens and views keep Unity concerns near the objects they control, while small managers expose stable lifecycle or facade APIs. Role-specific suffixes communicate responsibility more accurately than folder-wide naming, and call-graph-based condition removal improves readability without weakening real runtime failure handling.

### Implementation or verification result

Application UI now contains one facade, three screen components, and three focused views. Gameplay UI contains one level-scoped manager, one HUD state snapshot, one presenter, and four Unity-facing view/control components. `TowerNetworkHudLayoutBuilder.cs` and its `.meta` were removed, and both UI prefabs own their authored hierarchies and serialized bindings. The relevant Application, Levels excluding adapters, and gameplay UI scripts were reviewed for unreachable branches; no speculative null or connection guard was added to the final presenter.

## Entry 5 — Converge gameplay HUD communication on View–Presenter–Adapter

**Responsible session:** `01a02a90-cb3e-7523-97dd-8f9f705f3685` (Codex)

**Tracking issues:** `TowerDefense3D-os09`, `TowerDefense3D-ug3i`

### Problem being addressed

The first gameplay UI split used `GameplayUIInputRouter` for the input direction and `TowerNetworkHudPresenter` for the output direction. This made both directions explicit, but most router handlers only forwarded one command, and the user preferred the conventional MVP relationship with fewer classes.

### Prompt used

The user worked through the responsibilities of manager, presenter, view, drag button, and selection button; asked why Presenter and InputRouter had different names; questioned whether the router was unnecessary complexity; compared the design with conventional Model–View–Presenter; and finally selected `View ↔ TowerNetworkHudPresenter ↔ Adapter`.

### Important AI response

Clarified that the view or its child controls receive Unity input and publish intent, while the Presenter translates that intent into Adapter commands. In the opposite direction, the Adapter exposes runtime state and `StateChanged`; the Presenter converts that state into one `TowerNetworkHudState`, then asks `TowerNetworkHudView` to render it. `TowerPlacementDragButton` and `TowerSelectionButton` are focused controls rather than composite views, so keeping `*Button` is more precise than renaming them to `*View`.

### Option selected, revised, or rejected

- **Selected:** `TowerNetworkHudPresenter` subscribes to drag, Unlink, Start Wave, Cancel Placement, and Return to Menu input and invokes the corresponding Adapter/application command.
- **Selected:** the same Presenter observes Adapter state, creates the complete HUD state, and calls `TowerNetworkHudView.Render`.
- **Selected:** `GameplayUIManager` only constructs, connects, updates, shows, shuts down, and releases the Presenter and views.
- **Selected:** keep `*View`, `*Button`, `*Presenter`, and `*State` suffixes aligned with actual roles rather than applying one suffix to every file in the presentation layer.
- **Revised:** replace the temporary InputRouter plus output-only Presenter split with one conventional two-way Presenter.
- **Rejected:** let `TowerNetworkHudView` call `TowerNetworkSceneAdapter` directly or move all UI event handlers back into `GameplayUIManager`.

### Rationale

The final dependency direction is easier to teach and trace while preserving a passive Unity-facing view and a small lifecycle manager. The Presenter owns synchronous UI commands and presentation decisions; the Adapter remains the boundary to level-scoped gameplay. Removing a forwarding-only router reduces ceremony without coupling Unity views directly to gameplay systems.

### Implementation or verification result

`GameplayUIInputRouter.cs` and its `.meta` were removed. `TowerNetworkHudPresenter` now owns symmetric input and state subscriptions, Adapter commands, HUD snapshot construction, and rendering. Unity completed a Tundra compile with zero Console errors, and Better Context was regenerated and verified current before this documentation update. A fresh Unity test run was not claimed because the Unity MCP relay did not reconnect after domain reload; historical Test Runner lines were deliberately excluded from the result. No commit or push was performed.
