# AI Collaboration Log — System Lifecycle — 24 August 2026

## Entry 1 — Converge the project on one VContainer lifecycle entry point

**Responsible session:** `01a02a90-cb3e-7523-97dd-8f9f705f3685` (Codex Desktop)

### Problem being addressed

The project had one VContainer `IStartable` application coordinator but still distributed system work across many
`MonoBehaviour` callbacks, scene participants, adapters, presenters, UI managers, and drivers. Folder ownership also remained
fragmented across feature roots and redundant `Scripts` subfolders. This made lifecycle order, mutable-state lifetime,
dependency direction, and the reason each class inherited `MonoBehaviour` harder to see and debug.

### Prompt used

The project owner asked for the entire project to be reconsidered from a single-entry-point perspective, using every lifecycle
VContainer can own while leaving object-specific Unity callbacks local. The requested plan had to inventory systems, split
Unity components from logic, define the complete `Assets/Scripts` tree and dependency graph, cover every source file and
serialized asset, remove impossible conditions, follow repository line-wrapping and commit rules, preserve the dirty baseline,
compile only after each complete feature, create local feature commits, and never push.

### Important AI response

The recommended architecture uses `ApplicationEntryPoint` as the only project type implementing `IAsyncStartable`,
`ITickable`, `ILateTickable`, and `IDisposable`. It explicitly dispatches an `ApplicationSystemGroup` and the one
`LevelSystemGroup` attached through `ActiveLevelSystemSlot`. Each additive level owns a child `LevelLifetimeScope`; mutable
placement, tower, simulation, input, and HUD state dies with that scope. Plain systems own logic and focused authored views,
input sources, and factories own Unity APIs. Object callbacks such as enable/disable, pointer/drag, collision, and destruction
remain local because Unity owns those events.

The response also rejected a speculative lifecycle `Core`, generic tick interfaces, automatic container enumeration, custom
PlayerLoop code, `Ports` folders, version suffixes in type names, `[FormerlySerializedAs]`, and `[MovedFrom]`. It proposed six
acyclic assemblies and a feature-based local commit sequence, with one compile/test gate after each complete feature slice.

### Option selected, revised, or rejected

- **Selected:** root source folders `Application`, `System`, `Components`, `Editor`, and `Tests`.
- **Selected:** explicit tick order for input, placement, tower interaction, simulation, HUD refresh, link presentation,
  projectile presentation, and board camera framing.
- **Selected:** one child `LevelLifetimeScope` per loaded level and one attach/detach slot in the application scope.
- **Selected:** focused MVP boundaries for application and gameplay UI; authored prefab layout replaces runtime hierarchy build.
- **Selected:** keep current dirty files as baseline and integrate them into their owning feature commits.
- **Selected:** audit every changed feature for conditions proven impossible or duplicated by a stronger boundary invariant.
- **Revised:** `Core` is not created now; a narrowly named shared primitive may be extracted only after two systems need it.
- **Revised:** commit atomicity is feature-based, not one commit per move, rename, assembly edit, or serialized asset.
- **Revised:** compile and tests run after a whole feature slice, not after each internal operation.
- **Rejected:** remote Git/Beads synchronization, automatic push, one giant migration commit, and versioned source names.

### Rationale

The entry point makes system order explicit without pretending Unity object events belong to one global loop. Child scopes give
level state a container-enforced lifetime and remove manual service passing. Separating `System` from `Components` reveals why
a class needs Unity, while the six-assembly graph prevents reverse dependencies. Feature commits keep each intermediate state
buildable and reviewable without creating deliberately broken move-only snapshots. Boundary-proven guard removal improves the
main path while retaining checks for actual Unity, asynchronous, save-I/O, authored-data, and user-input failures.

### Implementation or verification result

The project owner explicitly approved the plan on 24 August 2026 and authorized implementation plus local feature commits.
The nine-path dirty worktree was confirmed unchanged as the baseline, Unity MCP resolved the exact TowerDefense3D project and
reported the Editor idle outside Play Mode, and Bead `TowerDefense3D-fetc` was claimed. Implementation and validation evidence
will be appended after the migration; no remote push or sync is authorized.
