# Hạn Trời — Tower Defense 3D

A mobile tower defense game built in Unity 6.3 (URP, Android). Towers do not shoot on their
own: you **wire them into a chain**, and a projectile travels along that chain, being
transformed by every tower it passes through before it reaches the sink. The network itself
is the weapon, so the order and the geometry of your towers are the tactical decision.

## Showcase

| | |
| --- | --- |
| [Placement.mp4](Showcase/Placement.mp4) | Placing towers on the grid and wiring a chain |
| [UI.mp4](Showcase/UI.mp4) | HUD, tower actions, upgrade and sell flow |
| [Crab.mp4](Showcase/Crab.mp4) | The Crab hero — fights alone, stuns what it hits |
| [BossFight.mp4](Showcase/BossFight.mp4) | Level 10 standing boss and the final wave |

## Gameplay

- **10 levels**, each a hand-authored board with its own path, wave schedule and camera bounds
- **Chain rule** — a valid chain runs `Generator → elemental towers → Soul Nexus`. Anything
  not part of a complete chain simply does not fire
- **Elements react** — fire, water and wind leave marks on enemies; pairs of marks trigger
  reactions such as burn, lift and thermal shock
- **Crab hero** — the one tower that fights by itself, stuns on hit, and is capped at one per level
- **Stars** — clear a level without losing frog health for 3 stars, above half for 2, below for 1

## Technical summary

Condensed from the full technical specification.

### Architecture

- **Layered** — `Application` (VContainer composition) → `Components` (MonoBehaviour boundary)
  → `System` (plain C# rules). `System` contains the whole simulation and never references
  Unity UI, VContainer or the editor. Enforced by **6 assembly definitions**, dependencies acyclic
- **Single entry point** — one `IAsyncStartable` / `ITickable` dispatches an explicit tick order:
  input → placement → tower interaction → simulation → HUD → link → projectile → camera
- **Two DI scopes** — an application scope (20 singletons: save, audio, flow) and a per-level
  scope (33 scoped systems). Leaving a level disposes the child scope, so no level state leaks
  into the next one and no `Reset()` has to be maintained by hand
- **MVP** — 7 presenters read systems, build an immutable state struct, and call `view.Render(state)`.
  Views never query a system, which keeps every presenter testable with a ~15-line stub
- **Observer** — 24 `event Action<T>` in the `System` layer; audio, VFX and HUD subscribe instead
  of being called

### Simulation

- **Fixed tick of 0.05 s** drives waves, enemies, towers and projectiles in one explicit order
- **Precomputed combat timeline** — the whole wave is simulated ahead of time and replayed, so
  combat is deterministic, frame-rate independent, and the tutorial can look ahead at events
  before they happen
- **Chains are not stored.** The graph keeps one outgoing link per tower; a chain is walked on
  demand, and only the set of towers currently inside a complete chain is cached

### Data

- **13 ScriptableObject types** hold every rule — levels, towers, enemies, waves, boards,
  element reactions, sounds, camera gestures. Designers tune numbers without touching code
- **Assets self-validate at load**, not mid-game: bad data refuses to boot and says why
- **Crash-safe save** — write to a temp file, read it back, then swap it in with an atomic
  `File.Replace`. A primary and a backup copy are kept, and a stale `schemaVersion` is rejected

### Graphics and performance

- URP mobile pipeline, Vulkan first, render scale 0.8, real-time shadows off, baked lighting
- **Two toon shaders on purpose** — objects that need per-object tint use a
  `UNITY_INSTANCING_BUFFER` variant and GPU instancing; everything else keeps all properties in
  `CBUFFER UnityPerMaterial` and stays on the SRP Batcher. The two are mutually exclusive
- **Static batching by default**, with any prop repeating 6+ times in a level pulled out to
  instancing instead — 151 objects across the 10 levels
- **3 sprite atlases split by screen** so menu sprites do not sit in RAM during a level
- Pooling for projectiles, enemies and audio; a shared particle rig for one-shot effects; VFX and
  shader prewarm at boot so the first use of an effect does not cost a frame spike

Measured on a **Samsung Galaxy A04s** (Exynos 850, Mali-G52, 720×1600) via Android Studio device
streaming: **~45 FPS** on a light board, **~25–30 FPS** with 9 enemies and overlapping transparent
VFX. The gap is fill rate, not logic — the simulation cost is precomputed and does not scale with
effects. Build is a **149.7 MB** universal APK; roughly 31 MB of that is the second ABI, which an
AAB would drop.

## Repository layout

```text
TowerDefense3D/
├── Assets/
│   ├── Config/          # Authored ScriptableObject and settings instances
│   ├── Resources/       # Runtime-loadable animations, prefabs, models, materials, and textures
│   ├── Scenes/          # Bootstrap, gameplay levels, and test scenes
│   └── Scripts/         # Project-owned C# source
├── Builds/              # Ignored local player builds
├── Documents/           # Game design, technical specifications, and durable project records
├── Packages/            # Unity package manifest and lock file
├── ProjectSettings/     # Unity project settings
├── Recordings/          # Ignored raw capture footage
└── Showcase/            # Short demo clips linked from this README
```

## Source layout

Project-owned C# source uses technical boundaries at the root and feature ownership below `System`, `Components`, `Editor`, and `Tests`.

```text
Assets/Scripts/
├── Application/         # VContainer composition, entry point, scopes, and scene integration
│   ├── EntryPoint/
│   ├── Scenes/
│   └── Scopes/
├── System/              # Plain C# systems, rules, state, contracts, and definitions
│   ├── Core/            # Proven mechanisms shared by multiple System features
│   │   ├── Numerics/
│   │   ├── Simulation/
│   │   └── StateMachine/
│   └── <FeatureName>/
├── Components/          # MonoBehaviour boundaries authored in scenes and prefabs
│   ├── Core/            # Unity-facing mechanisms shared by multiple component features
│   │   ├── Lifecycle/
│   │   └── Pooling/
│   └── <FeatureName>/
├── Editor/              # Editor-only authoring, validation, and project test tools
│   └── <FeatureName>/
└── Tests/
    ├── EditMode/
    │   └── <FeatureName>/
    └── PlayMode/
        └── <FeatureName>/
```

### Dependency direction

- `Application` may depend on `Components`, `System`, and VContainer.
- `Components` may depend on `System` and Unity runtime APIs. It must not own application or system lifecycle.
- `System` contains player-build logic but must not depend on `Application`, `Components`, VContainer, `UnityEditor`, or test assemblies.
- `Editor` may depend on the exact runtime assemblies required by its tools.
- `Tests` may depend on the exact runtime assemblies required by each fixture.
- Dependencies must remain one-way and acyclic.

### Assembly paths

| Path | Assembly |
| --- | --- |
| `Assets/Scripts/Application/` | `TowerDefense3D.Application.Runtime` |
| `Assets/Scripts/System/` | `TowerDefense3D.System.Runtime` |
| `Assets/Scripts/Components/` | `TowerDefense3D.Components.Runtime` |
| `Assets/Scripts/Editor/` | `TowerDefense3D.Editor` |
| `Assets/Scripts/Tests/EditMode/` | `TowerDefense3D.EditModeTests` |
| `Assets/Scripts/Tests/PlayMode/` | `TowerDefense3D.PlayModeTests` |

## Asset and document paths

```text
Assets/Config/<FeatureName>/       # Authored ScriptableObject/settings instances
Assets/Resources/Animations/       # Runtime-loadable animation clips and controllers
Assets/Resources/Prefabs/          # Runtime-loadable prefabs
Assets/Resources/Models/           # Runtime-loadable models grouped by asset name
Assets/Resources/Materials/        # Runtime-loadable materials
Assets/Resources/Textures/         # Runtime-loadable textures
Assets/Scenes/Bootstrap.unity      # Persistent application composition scene
Assets/Scenes/Levels/Level_###.unity
Assets/Scenes/Tests/               # Test-only scenes
Documents/GameDesign/              # Game design documents
Documents/TechnicalSpec/           # Approved or proposed technical specifications
Documents/AICollaboration/         # Concise AI-assisted decision records
Builds/                            # Ignored local build output
```

## Folder and path rules

- Put plain gameplay logic in `Assets/Scripts/System/<FeatureName>/` and matching Unity-facing code in `Assets/Scripts/Components/<FeatureName>/`.
- Keep small features flat. Add responsibility folders such as `Definitions`, `Models`, `Rules`, `Views`, or `Presenters` only when multiple current files share that role.
- Do not add a redundant `Scripts` child beneath any source root.
- Put only proven cross-feature plain C# mechanisms in `System/Core`; Core must not depend on a gameplay feature.
- Put only proven cross-feature Unity-facing mechanisms in `Components/Core`; gameplay rules remain in `System` features.
- Do not create speculative `Common`, `Helpers`, `Ports`, or additional `Core` folders without multiple concrete consumers.
- Keep boundary interfaces beside the system that owns the requirement unless a real cross-system module justifies another location.
- Use role-revealing postfixes for peers at the same level, such as `*System`, `*View`, `*Presenter`, `*Source`, and `*Factory`.
- Store ScriptableObject type definitions in their owning source feature and store authored `.asset` instances under `Assets/Config/<FeatureName>/`.
- Keep general-purpose loadable assets under the matching `Assets/Resources/<Category>/` folder. Do not create singular alternatives such as `Assets/Resources/Model/`.
- Use stable descriptive file and asset names. Do not append opaque labels such as `V1`, `V2`, `Latest`, or `Final` unless they are part of an explicit compatibility contract.
- Preserve `.meta` files and GUIDs when moving Unity files. Update literal `Resources.Load` or `AssetDatabase.LoadAssetAtPath` paths together with the move.
- Preserve stable namespaces during folder-only moves unless a separately approved change updates the namespace contract.

## C# line wrapping

- Keep method signatures, calls, assignments, declarations, and conditions on one readable line while they remain reasonably short. About 120 characters is a readability target, not a hard limit.
- Wrap only when a line becomes materially difficult to scan.
- Break at logical argument or condition groups and indent continuation lines consistently.
- Do not place every argument, operand, or assignment fragment on a separate line merely because an expression contains several items.
- Apply formatting cleanup only to files already touched by the current change; do not create unrelated formatting churn.

## Commit message convention

- Use Conventional Commit prefixes such as `feat:`, `fix:`, `docs:`, `test:`, or `chore:`.
- Write the subject in Vietnamese and capitalize only its first letter. Do not use Title Case.
- Keep established technical keywords, feature names, API names, and product terminology in English when translating them would reduce clarity.
- Keep the subject concise, imperative, and without a trailing period.
- Keep the commit message as a single-line subject; do not add a body or bullet list.
- Do not append a `Co-Authored-By` trailer or any other AI-attribution line.

Examples:

```text
feat: Thêm chức năng mới
fix: Sửa lỗi tương tác
docs: Cập nhật tài liệu dự án
```
