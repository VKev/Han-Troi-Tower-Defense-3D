# TowerDefense3D

## Repository layout

```text
TowerDefense3D/
├── Assets/
│   ├── Config/          # Authored ScriptableObject and settings instances
│   ├── Resources/       # Runtime-loadable prefabs, models, materials, and textures
│   ├── Scenes/          # Bootstrap, gameplay levels, and test scenes
│   └── Scripts/         # Project-owned C# source
├── Builds/              # Ignored local player builds
├── Documents/           # Game design, technical specifications, and durable project records
├── Packages/            # Unity package manifest and lock file
└── ProjectSettings/     # Unity project settings
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
│   │   └── Lifecycle/
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
