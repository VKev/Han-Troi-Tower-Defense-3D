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
Assets/_Project/<FeatureName>/
├── Scripts/     # Player-build source
├── Data/        # Authored data assets
├── Editor/      # Editor-only tooling
└── Tests/       # Automated tests
```

- Add responsibility-based subfolders only when the feature needs them; do not create empty layers to match an example.
- Keep source definitions separate from authored data instances.
- Introduce additional assembly boundaries only when a clear dependency, platform, or test boundary justifies them.
- Preserve stable namespaces and assembly names during folder-only reorganizations unless a separate approved change explicitly alters those contracts.

## Commit message convention

- Use Conventional Commit prefixes such as `feat:`, `fix:`, `docs:`, `test:`, or `chore:`.
- Write the subject in Vietnamese and capitalize only its first letter. Do not use Title Case.
- Keep established technical keywords, feature names, API names, and product terminology in English when translating them would reduce clarity.
- Keep the subject concise, imperative, and without a trailing period.

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
