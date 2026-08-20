# TowerDefense3D

## Project overview

TowerDefense3D is a **mobile-first 3D tower-defense game** built with Unity. Touch devices are the primary target; mouse input in the Unity Editor is a development fallback rather than the main interaction model.

Project decisions should therefore preserve these constraints:

- Design gameplay input for touch first, including tap, drag, placement confirmation, and cancellation.
- Keep controls, placement feedback, and UI readable on small screens and inside device safe areas.
- Avoid interactions that depend on hover, right-click, or a hardware keyboard.
- Treat mobile CPU, GPU, memory, battery, thermal limits, and allocation pressure as production constraints.
- Validate gameplay, presentation, builds, and performance on representative mobile aspect ratios and physical devices before release.

## Project documentation

`Documents/` is the canonical location for human-authored project documents. Store durable, reviewable material here, including:

- Game Design Documents (GDDs).
- Technical specifications and architecture notes.
- Approved implementation plans and decision records.
- Test plans, QA reports, release notes, and operational guides.
- Research or design references that materially affect the project.

Do not use `Documents/` for generated caches, temporary agent output, Unity-generated files, credentials, or raw chat transcripts. A document should clearly state its status when relevant, such as `Draft`, `Under Review`, `Approved`, or `Superseded`.

## Technical specification workflow

`Documents/TechnicalSpec/` is the canonical location for feature-level technical specifications. Use one English Markdown file per feature with the filename format `<FeatureName>_Technical_Specification.md`.

When the project owner explicitly approves an implementation plan, the responsible agent must:

1. Create or update the feature's technical specification before changing implementation files.
2. Mark the specification `Approved` only when the project owner explicitly approved the plan. Otherwise keep it `Draft` or `Under Review`.
3. Record the approved scope, non-goals, architecture and ownership, data and runtime-state contracts, interaction flow, folder and assembly boundaries, serialized integration, compatibility or migration constraints, verification plan, risks, and deferred work.
4. Implement against the specification. Do not silently expand scope or replace an approved decision; obtain approval for material changes and update the specification before continuing.
5. After implementation, update the same file with the actual status, validation evidence, known limitations, and any approved deviation from the original plan.
6. Record consequential AI-assisted decisions in `Documents/AICollaboration/` and keep execution tasks in the project's issue tracker rather than turning the specification into a task list.

Technical specifications are durable project records and should be reviewed and version-controlled according to repository policy.

## Documentation language

All human-authored project documentation must be written in English. This requirement applies to filenames, titles, headings, body text, field labels, tables, captions, and review notes in `Documents/`, as well as documentation files at the repository root.

Non-English names, source phrases, or direct quotations may be retained only when they are necessary for cultural or technical accuracy, and they must include a nearby English explanation.

## Feature source layout

Organize project-owned features under a stable feature root. `<FeatureName>` is a placeholder rather than a literal folder name.

```text
Assets/Scripts/<FeatureName>/
├── Scripts/     # Player-build source
├── Editor/      # Editor-only tooling
└── Tests/       # Automated tests
```

- Add responsibility-based subfolders only when the feature needs them; do not create empty layers to match an example.
- Introduce additional assembly boundaries only when a clear dependency, platform, or test boundary justifies them.
- Preserve stable namespaces and assembly names during folder-only reorganizations unless a separate approved change explicitly alters those contracts.
- Do not store authored ScriptableObject/settings instances or general-purpose loadable assets (textures, materials, prefabs, models) inside a feature folder; see "Shared asset roots" below.

### One source home per responsibility

- Reuse an existing feature root before creating another folder for the same responsibility. For example, all project-owned UI scripts belong in `Assets/Scripts/UI/Scripts/`; do not create another `UI` tree under `GameFlow`, `Tower`, or another feature.
- Keep small modules flat. Do not add `Application`, `Gameplay`, `Presentation`, or similar category folders unless current files establish a real ownership, lifecycle, Editor/runtime, test, or assembly boundary.
- Apply the same rule to every feature: move a script to its existing owning module instead of creating a second home with the same name.
- Preserve `.meta` files and GUIDs during moves so scene, prefab, and ScriptableObject references remain intact.
- `Scripts`, `Editor`, and `Tests` are the standard feature boundaries. Use another boundary name only when it has a concrete technical meaning that the standard layout cannot express.

### Stable filenames and explicit versions

- Use stable, descriptive filenames and `CreateAssetMenu.fileName` values. Do not append opaque balance or iteration labels such as `_V0_3`, `_v2`, `Latest`, or `Final` to ordinary source or asset names.
- Do not hardcode a balance/revision version merely to identify the current data iteration. Track normal balance evolution in source control and the relevant design documentation.
- A version identifier is allowed only when it is part of an explicit compatibility or migration contract, such as a save schema or external API. Its owner, supported values, and migration behavior must be clear in code or documentation.
- Rename versioned assets through Unity's Asset Database and update any literal load paths; preserve GUID-based serialized references.

## Shared asset roots

Two root-level folders under `Assets/` centralize instance data instead of scattering it per feature:

- `Assets/Config/<FeatureName>/` stores every authored ScriptableObject/settings instance owned by that feature (for example board definitions, tower definitions, level catalogs). Non-feature-specific engine or render-pipeline settings live under a category folder instead, such as `Assets/Config/Rendering/`.
- `Assets/Resources/<Category>/` stores general-purpose loadable assets (textures, materials, prefabs, models) organized by asset type rather than by feature. Vendor assets that specifically require Unity's `Resources` folder behavior (for example `DOTweenSettings.asset`) also live here at the root; do not relocate a vendor-required entry out of this folder.
- Assets inside `Assets/TextMesh Pro/Resources/` remain vendor-owned and out of scope for this convention, per the vendor/third-party boundary already in effect for that folder.
- When code loads one of these assets by a literal string path (`AssetDatabase.LoadAssetAtPath`, `Resources.Load`), update that path alongside any move; a direct serialized-field reference needs no code change since Unity tracks it by GUID.
- Because the project owner drops new assets into `Assets/Resources/` directly and often, refresh Better Context (`better-context-unity scan` then `agents` then `verify`) before every commit that touches `Assets/Resources/` or `Assets/Config/`, so the generated maps stay accurate for the next agent or session.

## Commit message convention

- Use Conventional Commit prefixes such as `feat:`, `fix:`, `docs:`, `test:`, or `chore:`.
- Write the subject in Vietnamese and capitalize only its first letter. Do not use Title Case.
- Keep established technical keywords, feature names, API names, and product terminology in English when translating them would reduce clarity.
- Keep the subject concise, imperative, and without a trailing period.
- Keep the commit message a single-line subject; do not add a body or bullet list.
- Do not append a `Co-Authored-By` trailer or any other AI-attribution line. The commit author is the project owner's configured Git identity.

Examples:

```text
feat: Thêm chức năng mới
fix: Sửa lỗi tương tác
docs: Cập nhật tài liệu dự án
```

## AI collaboration records

`Documents/AICollaboration/` stores concise records of consequential collaboration with AI assistants. These records preserve decisions and validation evidence without copying an entire raw transcript.

Use the filename format:

```text
AI_Collaboration_Log_<Area>_dd_mm.md
```

Existing records are available in [`Documents/AICollaboration/`](Documents/AICollaboration/).

Every entry must include:

1. **Problem being addressed** — the problem or uncertainty being addressed.
2. **Prompt used** — the relevant user prompt, summarized when it contains sensitive or repetitive content.
3. **Important AI response** — the important recommendation, evidence, or warning returned by the AI.
4. **Option selected, revised, or rejected** — the option selected, changed, or rejected.
5. **Rationale** — why that decision was made.
6. **Implementation or verification result** — the implementation or verification result.

Each log must also record the responsible chat/session ID. When several sessions contribute, identify the responsible session for each entry. Store the session ID rather than a raw transcript or machine-specific transcript path.

## AI-assisted project work

- Follow the applicable instructions in `AGENTS.md` before changing project files.
- Treat generated maps, indexes, and summaries as navigation aids rather than substitutes for current source, assets, runtime state, compilation, or tests.
- Keep tool-specific setup, commands, and maintenance procedures in agent instructions or dedicated operational documentation.
- Record consequential decisions and validation outcomes in `Documents/AICollaboration/`.

## Security and traceability

- Redact API keys, credentials, personal data, and other secrets.
- Do not mark a plan as approved unless the user or project owner explicitly approved it.
- Link validation evidence where practical, but keep generated caches and transient setup output out of this documentation hierarchy.
