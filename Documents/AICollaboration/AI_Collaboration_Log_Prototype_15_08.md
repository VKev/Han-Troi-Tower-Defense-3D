# AI Collaboration Log — Browser Prototype — 15/08/2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Area:** `Documents/Prototype`
- **Responsible Codex sessions:** `019ffa0d-09cb-7df2-b2e2-cd1e72bd2a74`, `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Responsible ChatGPT chats:** `6a7e858a-80fc-8322-9e39-ebfb4fcaa7db`, `6a7c3d0c-d31c-8323-9f15-4baea55ecb54`
- **Tracking issues represented in these entries:** `TowerDefense3D-mi2`, `TowerDefense3D-afi`, `TowerDefense3D-41t`, `TowerDefense3D-xpg`, `TowerDefense3D-ajv`, `TowerDefense3D-ajv.1`, `TowerDefense3D-ajv.2`, `TowerDefense3D-ajv.3`, `TowerDefense3D-ajv.4`, `TowerDefense3D-ajv.5`, `TowerDefense3D-3zl`, `TowerDefense3D-dcu`, `TowerDefense3D-2yg`, `TowerDefense3D-5rc`, `TowerDefense3D-4nn`, `TowerDefense3D-dyn`, `TowerDefense3D-dyn.1`, `TowerDefense3D-dyn.2`, `TowerDefense3D-dyn.3`, `TowerDefense3D-vtk`
- **Legacy Toad production prototype:** [tower-defense-am-duong.vercel.app](https://tower-defense-am-duong.vercel.app)
- **Rotation prototype:** [`Documents/Prototype/Arcane-Arsenal/`](../Prototype/Arcane-Arsenal/) — [production](https://arcane-arsenal-tower-defense.vercel.app)
- **Explicit-link prototype:** [`Documents/Prototype/Arcane-Arsenal-Link/`](../Prototype/Arcane-Arsenal-Link/) — [production](https://arcane-arsenal-link-network.vercel.app)

This file records consequential prototype decisions from the responsible sessions and chats. It summarizes decisions and verification evidence rather than reproducing raw transcripts.

## Entry 14 — Accelerate combat feedback and add a ten-wave Level 3

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issues:** `TowerDefense3D-mi2`, `TowerDefense3D-afi`

### Problem being addressed

Projectile travel and tower output felt slow, collision feedback was too small, enemy formations read as an evenly spaced queue, the Nexus Lance lesson did not explain how to feed ammunition into the special tower, and the prototype stopped after Level 2.

### Prompt used

The user requested `3×` projectile speed, `1.5×` tower firing cadence, `2×` projectile hitboxes with matching visual size, `40%` slower enemies, uneven side-by-side and overlapping formations, a Lance tutorial that adds another Foundry and shows ammunition above the tower, and a larger Level 3 with one raised combat height, ten waves, and new enemy types derived from Concept 2.

### Important AI response

The AI kept swept-segment collision so faster projectiles could not tunnel through enemies or receiver towers. Formation variety was authored through nonuniform spawn times and lateral offsets rather than random path changes. Level 3 was built as a larger 20×14 logical board with a 106-unit route, two practical Layer 1 plateaus, ten waves, and two reaction-barrier enemies: the flying Sky Warder and ground Rift Colossus.

### Option selected, revised, or rejected

- **Selected:** global projectile speed `3×`, production/output cadence `1.5×`, projectile visual scale `2×`, collision radius `2×`, and enemy speed `0.6×`.
- **Selected:** nonuniform gaps, lateral staggering, adjacent movement, and intentional overlap while retaining the fixed authored route.
- **Selected:** an additional Foundry as the required Nexus Lance feeder, plus a camera-facing in-world Lance ammunition bar.
- **Selected:** Level 3 with Ground and Layer 1 only, ten strictly escalating waves, Sky Warder and Rift Colossus reaction barriers, and full tower availability.
- **Rejected:** restoring even single-file spacing, adding Layer 2 to Level 3, or explaining the Lance only through another text modal.

### Rationale

The requested speed changes make routing decisions resolve quickly while slower enemies preserve enough observation time on mobile. Uneven formations create real projectile-line opportunities. A visible feeder and ammunition bar teach the special tower through cause and effect, while the larger third map supplies enough space and wave count to exercise the complete tower roster.

### Implementation or verification result

Runtime diagnostics report all five global speed/size multipliers, intentional overlapping enemy pairs, a connected Lance feeder, and a live Lance ammo ratio. Level 3 is larger than Level 2 in board dimensions, route length, buildable cells, wave count, and enemy variety. Desktop/mobile browser tests exercise the new map, both new enemy profiles, their live models, touch dragging, and the Lance lesson without console or page errors.

## Entry 15 — Tighten the economy and make late waves reaction-driven

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-41t`

### Problem being addressed

Arcana income still made purchase choices too forgiving, later waves needed both more enemies and substantially tougher health scaling, the Nexus Lance beam appeared to drift as it disappeared, and players needed a clear per-level indication of the enemy entry direction.

### Prompt used

The user requested lower enemy money drops across the game, increasingly larger and tougher waves, especially tanky late enemies that pressure elemental reactions, a fix for the offset Lance beam, and a red direction arrow in every level showing where enemies enter and travel.

### Important AI response

The AI centralized reward reduction instead of rewriting every enemy, added explicit health multipliers to every wave, scaled reaction damage with target maximum health, anchored Lance VFX in local firing space, and built a fog-independent 3D arrow aligned to each stage's first path segment. The arrow was verified visually after a flat fog-affected version proved technically present but unreadable.

### Option selected, revised, or rejected

- **Selected:** enemy rewards at global `0.6×` and wave-clear rewards at `0.65×`; starting money and tower prices remain unchanged so guided placements still work.
- **Selected:** strictly increasing enemy counts and HP multipliers in every stage; Level 2 reaches 38 enemies at `2.5×` HP and Level 3 reaches 68 enemies at `4.6×` HP.
- **Selected:** reaction bonus damage equal to `6%` of target maximum HP in addition to the existing projectile-derived bonus; named barriers still require their correct reaction.
- **Selected:** Lance beam geometry positioned relative to the firing anchor and faded without scaling the whole VFX group.
- **Revised:** replace the first flat red arrow with raised box-and-cone geometry using fog-independent, untone-mapped red materials so it remains visible on large maps and mobile cameras.
- **Rejected:** reducing tutorial purchasing power, solving late waves through neutral HP damage alone, or leaving the arrow as a diagnostic-only object that blended into fog.

### Rationale

Central multipliers preserve authored enemy and stage identities while making money scarce. Strict count and HP curves create predictable escalation, and maximum-HP reaction damage keeps multi-element routing relevant against late bosses. Local-space Lance VFX removes visual drift, while a bright physical arrow communicates spawn direction without additional tutorial text.

### Implementation or verification result

The final-wave Rift Colossus reaches `4,508` HP and a deterministic Crystal Shatter test records a `290`-damage reaction bonus while removing its barrier. Lance diagnostics hold maximum anchor and scale errors at `0`. Each stage reports exactly one correctly aligned, viewport-visible spawn arrow. The gameplay bot reaches Wave 2 with 19 Nexus lives and 743 Arcana under the reduced economy.

## Entry 16 — Preserve mobile playability and release the final static build

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-41t`

### Problem being addressed

The final balance and visibility changes had to remain playable on phones, and the user requested a production Vercel deployment after documentation and verification were complete.

### Prompt used

The user explicitly asked that the web prototype remain suitable for mobile play and requested a fresh Vercel deployment after the work was finished.

### Important AI response

The AI treated mobile as a separate release target rather than a scaled desktop screenshot. It retained portrait safe-area layout, touch drag/drop and tap inspection, ran the full desktop/mobile Playwright matrix, inspected the built `dist` on an iPhone 13 viewport, then repeated canvas inspection against the live production alias.

### Option selected, revised, or rejected

- **Selected:** a 390×664 mobile viewport with DPR capped at `1.5`, touch-specific placement assertions, 44-pixel touch targets, portrait HUD bounds, and mobile visual baselines.
- **Selected:** deploy the already verified `dist` directory with relative asset paths to the existing production project and alias.
- **Rejected:** validating mobile only through CSS width changes, opening `index.html` through `file://`, or deploying the TypeScript source tree directly.

### Rationale

Mobile interaction, layout, and render cost fail in different ways from desktop. Testing the actual production artifact and then the public alias catches static-path, CORS, viewport, touch, and WebGL-budget regressions before handoff.

### Implementation or verification result

`npm run build` passed, `npm audit` reported zero vulnerabilities, and the full Playwright suite passed 33 tests with 9 intentional cross-viewport skips. Live Vercel inspection at 390×664 passed with 113 draw calls, 13,870 triangles, 103 geometries, four textures, no console errors, and no page errors; the separate live Level 3 mobile state also remained within budget and visibly displayed its red entry arrow. Deployment `dpl_7rThe2VMZNZ5uDiyyma3JoEPPqWB` reached `READY`, the production alias and hashed JS/CSS assets returned HTTP 200, and the release is available at `https://arcane-arsenal-tower-defense.vercel.app`.

## Entry 17 — Escalate Levels 2–3 and introduce Fire–Ice armor breaking

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-xpg`

### Problem being addressed

Level 2 and Level 3 did not escalate sharply enough between waves. The user wanted every later wave to increase both enemy durability and formation density, plus a new armored Level 3 enemy whose armor can only be broken by the Fire + Ice reaction and that becomes faster after the break. Raising the final wave to 120 simultaneous enemies also introduced a mobile rendering risk that had to be resolved before release.

### Prompt used

The user requested harder Level 2 and Level 3 wave curves with more HP and density in every successive wave, a Level 3 armored enemy breakable only by Fire + Ice that speeds up after losing armor, then a production deployment and an AI Collaboration update.

### Important AI response

The AI separated escalation into three measurable curves: enemy count, authored HP multiplier, and spawn density in enemies per second. It represented armor as a named `Sốc Nhiệt` reaction barrier with a dedicated protected-damage multiplier and post-break speed multiplier, then added deterministic intact/broken armor states. When live stress inspection showed the original detailed models exceeded the mobile reference budget, the AI added an instanced crowd LOD that preserves per-enemy color, elemental tint, hit flash, layer, scale, and active barrier state while batching dense formations. It also stopped rebuilding unchanged wave-detail DOM every 120 ms so touch clicks no longer race against detached elements.

### Option selected, revised, or rejected

- **Selected:** keep the three tutorial waves unchanged.
- **Selected:** Level 2 counts `[10, 14, 19, 25, 32, 40]`, HP multipliers `[1.1, 1.35, 1.65, 2.05, 2.5, 3.1]`, and measured spawn densities `[1.26, 2.65, 3.43, 5.52, 7.5, 8.79]`.
- **Selected:** Level 3 counts `[20, 27, 35, 44, 54, 65, 77, 90, 104, 120]`, HP multipliers `[1.2, 1.5, 1.9, 2.35, 2.85, 3.4, 4, 4.7, 5.5, 6.4]`, and measured spawn densities `[2.84, 3.84, 4.35, 7.1, 8.78, 9.79, 12.32, 16.62, 20.06, 33.07]`.
- **Selected:** introduce `Vệ Binh Hợp Kim` from Level 3 Wave 3 onward with 520 base HP, a `Sốc Nhiệt` armor requirement, only `4%` normal protected damage, and `1.85×` movement after the barrier breaks.
- **Selected:** visually remove the armor shell on break, show the exact reaction and rush behavior in the enemy detail UI, and preserve the existing reaction VFX, toast, tint, icons, flash, and hit particles.
- **Revised:** switch formations of 12 or more active enemies to two instanced crowd batches and temporarily hide decorative world props; detailed models return automatically below the threshold.
- **Revised:** replace transient active-particle-only assertions with a cumulative particle-burst diagnostic and cache unchanged wave roster/detail DOM.
- **Rejected:** increasing only raw HP, allowing neutral or single-element damage to break the new armor, changing the tutorial curve, or shipping the 800-plus-draw-call dense wave to mobile.

### Rationale

Separate monotonic count, HP, and density curves make every wave observably harder instead of relying on one opaque threat score. Requiring `Sốc Nhiệt` makes the new defense a routing problem rather than another health sponge, while the post-break rush creates a deliberate risk/reward timing change. Instancing preserves the requested 120-enemy pressure without sacrificing phone playability or removing the elemental state feedback needed to understand combat.

### Implementation or verification result

TypeScript and Vite production compilation passed. Deterministic desktop/mobile tests verify the exact Level 2 and Level 3 curves, strict monotonic spawn density, the Wave 10 roster, the alloy enemy detail profile, Fire alone leaving armor intact, Fire followed by Ice removing the barrier and armor shell, and the resulting speed multiplier above `1.0`. The final gameplay bot and ten desktop/mobile visual baselines passed `11` tests with one intentional mobile bot skip; the focused final gameplay/UI/armor suites passed `8/8` and the final Level 3 performance gate passed `2/2`. The full regression run's three timing-sensitive failures were corrected and each failed case passed on both desktop and mobile in the rerun.

Production deployment `dpl_6WWyQJGFT2BHTDwobZPBH3JoCgNp` reached `READY` at `https://arcane-arsenal-tower-defense.vercel.app`. The alias, `index-1mH7Rtby.js`, and `index-CjNFrjtn.css` returned HTTP 200. Direct live Wave 10 inspection rendered all 120 enemies with no console or page errors; mobile measured 81 draw calls, 19,204 triangles, 145 geometries, and four textures, while desktop measured 166 draw calls, 21,072 triangles, 217 geometries, and four textures, all within the documented reference budgets. An accidental temporary Vercel project named `dist` created during relinking was verified as new and removed; the intended production project binding remained authoritative.

## Entry 18 — Remove the Crowd LOD that could render enemies black

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-7ya`

### Problem being addressed

At high enemy density, the crowd-optimization path could replace detailed enemies with black or incorrectly shaded instances. This obscured elemental tints, armor states, and enemy silhouettes—the exact combat feedback the prototype depended on.

### Prompt used

The user reported that all enemies sometimes turned black, asked whether it was a bug, and explicitly requested removing the LOD rather than repairing or retaining it.

### Important AI response

The AI traced the behavior to the instanced Crowd LOD presentation path, removed that path completely, kept every active enemy on the detailed renderer, and added high-density verification so future performance work cannot silently reintroduce black replacement models.

### Option selected, revised, or rejected

- **Selected:** remove the Crowd LOD and always render the detailed enemy presentation.
- **Selected:** preserve strong elemental hue, status icons, armor-shell visibility, hit flash, and particles at every density.
- **Selected:** add a deterministic high-density assertion that the number of visible detailed enemies matches the active enemy count.
- **Accepted:** the higher browser rendering cost because the user explicitly prioritized correct enemy appearance over LOD optimization.
- **Rejected:** repairing or retuning the instanced Crowd LOD, raising its threshold, or keeping it as a mobile-only fallback.

### Rationale

The black models removed combat readability and invalidated the elemental-feedback promise. Since the user explicitly rejected LOD, retaining two presentation paths would add complexity while preserving the same class of visual risk.

### Implementation or verification result

The Crowd LOD was fully removed. TypeScript/Vite compilation and targeted high-density Playwright checks passed on desktop and mobile. Production deployment `dpl_9BEe64mjEWGgb9ykXxkKYsSHYkas` reached `READY`; direct Wave 10 inspection kept all detailed models visible. The documented no-LOD tradeoff was 882 draw calls and 855 geometries on desktop, and 811 draw calls and 793 geometries on mobile. This deployment was later superseded by the variant split recorded in Entry 21.

## Entry 19 — Replace rotation with explicit source-to-target links

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issues:** `TowerDefense3D-ajv`, `TowerDefense3D-ajv.1`, `TowerDefense3D-ajv.2`, `TowerDefense3D-ajv.3`, `TowerDefense3D-ajv.4`, `TowerDefense3D-ajv.5`, `TowerDefense3D-3zl`

### Problem being addressed

Continuous tower rotation made it difficult to author a precise projectile network on touch screens. The enemy route also contained long segments that were awkward to cross with tower links, while terminal buff towers and reciprocal links needed explicit, predictable rules.

### Prompt used

The user requested removal of tower rotation in favor of a Link action, valid in-range tower highlighting, rejection of direct reciprocal links, larger connection ranges, a shorter grid-aligned tutorial route, and tutorial placements whose link segments meaningfully cross the enemy lane. The user also clarified that a terminal buff tower with an incoming link must do nothing until it has an outgoing link.

### Important AI response

The AI modeled every normal connection as an explicit directed edge from one tower to one target. It proposed a touch-friendly Link state, exact valid/invalid candidate feedback, `1.5×` connection ranges, direct A-to-B then B-to-A rejection, and projectile damage only along linked segments. It also replaced the tutorial's long route with short orthogonal grid-aligned segments and authored placements that create useful lane intersections.

### Option selected, revised, or rejected

- **Selected:** select a source tower, enter Link mode, then tap one highlighted valid target.
- **Selected:** preserve one outgoing edge per tower, allow several producers to feed one receiver, and prevent only the immediate reciprocal edge.
- **Selected:** make non-special terminal buff towers inactive until linked onward; the Nexus Lance remains a terminal special tower whose output is its skill.
- **Selected:** increase every nonzero connection range by 50% and keep walls, height rules, and range checks authoritative.
- **Selected:** use a shorter orthogonal tutorial path and move the guided tower cells so the linked projectile segments cross live enemies.
- **Rejected:** hidden auto-linking, free directional firing from the last tower, rotation controls in the link version, and long non-grid-aligned enemy segments.

### Rationale

Explicit links make the network state inspectable and deterministic on both mouse and touch. Directed-edge rules preserve the intended routing puzzle, while the shorter map and wider ranges let the tutorial demonstrate the mechanic without solving it through arbitrary long shots.

### Implementation or verification result

The full explicit-link suite passed 37 tests with 7 intentional cross-viewport skips and no failures across desktop and mobile. Coverage includes real link interaction, valid highlighting, reciprocal rejection, range boundaries, terminal tower inactivity, linked-segment enemy damage, Layer 1 combat, the shortened tutorial route, and mobile Lance targeting. Production deployment `dpl_3KEAPHu3QbVAa4s9FhPPF68iLbXg` passed live desktop/mobile Link interaction with HTTP 200 assets and no console or page errors. This link implementation was later preserved as the independent `Arcane-Arsenal-Link` variant in Entry 21.

## Entry 20 — Remove non-special ammunition blocking while preserving Lance storage

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issues:** `TowerDefense3D-dcu`, `TowerDefense3D-2yg`

### Problem being addressed

Removing the visible ammunition magazine from buff towers did not remove their hidden finite capacity. Once that queue filled, the upstream network still stopped firing, contradicting the requested rule that normal towers should transmit continuously. Only the Nexus Lance was intended to accumulate a finite payload.

### Prompt used

The user first requested removal of ammunition-slot stacks from every buff tower except the Lance, then clarified from live gameplay that the hidden full-slot condition still stopped the tower and explicitly required continuous firing.

### Important AI response

The AI distinguished presentation cleanup from the runtime flow rule. It first removed player-facing magazine and capacity copy from Foundry and support towers, then revised the simulation so every non-Lance tower has no finite capacity block. A terminal buff tower now consumes and dissipates an incoming round instead of storing it indefinitely, while the Lance retains finite storage, its in-world ammunition bar, and automatic skill release.

### Option selected, revised, or rejected

- **Selected:** show ammunition storage only for a placed Nexus Lance and show Lance capacity only in its catalog details.
- **Revised:** replace the original hidden finite safeguard with unlimited non-Lance flow after the user confirmed that the safeguard still interrupted firing.
- **Selected:** keep transfer ordering for connected towers, but never reject a normal round because a non-special queue is full.
- **Selected:** clear arrivals at an unlinked terminal buff so upstream sources continue their normal cadence.
- **Rejected:** hiding the magazine while retaining the five-round stopping behavior, and removing finite storage from the Lance.

### Rationale

The gameplay rule must match the information shown to the player. If normal towers expose no magazine, an invisible capacity cannot silently halt the network. The Lance is different because ammunition accumulation is its readable special-skill contract.

### Implementation or verification result

The final link build passed compilation and six focused desktop/mobile network tests. A deterministic continuous-flow case launched more than the former five-round limit, retained zero rounds in the terminal Fire tower, reported no finite-ammo normal towers, and recorded no capacity-blocked tower. Live production sampling later reached nine launches with `terminalBuffer = 0`, `finiteAmmoTowerIds = []`, and `capacityBlockedTowerIds = []`. The final production URL and deployment are recorded in Entry 21.

## Entry 21 — Preserve separate rotation and explicit-link prototype releases

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issues:** `TowerDefense3D-5rc`, `TowerDefense3D-4nn`

### Problem being addressed

After validating the explicit-link redesign, the user wanted to retain it without losing the earlier press-and-hold rotation experiment. A single folder and Vercel alias could not represent both interaction models at the same time.

### Prompt used

The user requested copying the current prototype to another folder, restoring the original folder to the earlier rotatable-tower version, deploying the preserved link build at a different path, and receiving one public URL for each mechanic.

### Important AI response

The AI proposed two independent static applications and Vercel projects. It preserved the latest fixed link network in `Documents/Prototype/Arcane-Arsenal-Link`, restored `Documents/Prototype/Arcane-Arsenal` from verified rotation snapshot `67ed446`, kept deployment metadata and local environment files out of source control, and validated each variant against its own interaction contract before publishing.

### Option selected, revised, or rejected

- **Selected:** `Arcane-Arsenal` is the earlier rotation snapshot and continues to use the primary `arcane-arsenal-tower-defense` Vercel project.
- **Selected:** `Arcane-Arsenal-Link` preserves the newer explicit-link, no-LOD, three-level build and the continuous non-special flow fix in a distinct `arcane-arsenal-link-network` Vercel project.
- **Selected:** verify the rotation build by holding the left/right control on desktop and mobile, and verify the link build by checking its Link-only UI and sustained terminal flow.
- **Selected:** ignore `.vercel` and `.env*` in both prototype folders and avoid copying local dependencies, build output, test artifacts, or deployment linkage into the new source folder.
- **Rejected:** overwriting one mechanic with the other on every deployment, using the same production alias for both builds, or copying Vercel credentials into the repository.

### Rationale

The two mechanics represent different design experiments and must remain directly comparable. Separate folders, build outputs, Vercel projects, and public aliases prevent future changes or deployments in one variant from silently replacing the other.

### Implementation or verification result

The rotation build passed TypeScript/Vite compilation, five focused gameplay/UI tests with three intentional viewport skips, ten desktop/mobile visual baselines, and the gameplay bot. Live press-and-hold smoke changed the selected output angle from `0` to approximately `-3.017` on both desktop and mobile, exposed both rotation controls, and exposed no Link control. Deployment `dpl_2BbazR36R5E5ajkcoBSK8chzP7fV` reached `READY` at [arcane-arsenal-tower-defense.vercel.app](https://arcane-arsenal-tower-defense.vercel.app).

The independent link build passed compilation, six focused desktop/mobile link and continuous-flow tests, and `npm audit` with zero vulnerabilities. Live smoke exposed one Link control and no rotation controls, reached nine launches through a terminal Fire support, retained zero terminal rounds, and reported no capacity block. Deployment `dpl_BXJgP928kLQNKodgg3GYiU5XE4Ba` reached `READY` at [arcane-arsenal-link-network.vercel.app](https://arcane-arsenal-link-network.vercel.app). Both aliases and their hashed JavaScript/CSS assets returned HTTP 200 with no console, page, or HTTP errors. No Git commit or push was requested or performed for this split.

## Entry 22 — Synchronize both variants and teach first-time resources without text

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issues:** `TowerDefense3D-dyn`, `TowerDefense3D-dyn.1`, `TowerDefense3D-dyn.2`, `TowerDefense3D-dyn.3`

### Problem being addressed

The preserved rotation prototype had fallen behind the explicit-link build in maps, wave balance, enemy health, movement, spawn density, projectile tuning, VFX, UI, and Level 3 content. Both variants also lacked causal, text-free introductions for Arcana, elemental reactions, and Nexus life.

### Prompt used

The user requested icon/animation-only first-time introductions after the first tower purchase, first elemental reaction, and first Nexus life loss. The user then clarified that the rotation build must match the link build in projectile speed and appearance, enemy HP, enemy speed, enemy concentration, density, overlap, and every other gameplay/content value; only rotation versus explicit linking may differ. Both variants then had to be deployed and this collaboration log updated.

### Important AI response

The AI made the explicit-link build the authoritative baseline and reconciled the rotation build onto the same shared source. A single `routingMode.ts` flag now selects either explicit directed links or continuous press-and-hold aiming/physical tower interception. Event-driven discovery cues queue a text-free pictogram plus animated pointer after the three requested first-time events without pausing or blocking touch input.

### Option selected, revised, or rejected

- **Selected:** keep two independent folders and Vercel projects, but make all common source identical.
- **Selected:** preserve Link mode, valid target highlighting, reciprocal-link rejection, and inactive unlinked terminal buff towers only in the link variant.
- **Selected:** preserve smooth hold-to-rotate controls, the wide translucent red shot guide, physical support-tower interception, and free projectile continuation only in the rotation variant.
- **Selected:** share all stage definitions, wave orders, enemy HP multipliers, spawn timing/density/lateral offsets, enemy speed `0.6×`, projectile speed `3×`, tower cadence `1.5×`, projectile visual scale `2×`, hit radius `0.84`, economy, reactions, Level 3 barriers, mobile UI, and no-LOD detailed rendering.
- **Selected:** show `currency`, `reaction`, and `Nexus` concepts once per fresh run through icons, element colors, arrows, and hand/pointer animation rather than explanatory copy.
- **Rejected:** maintaining two independently drifting balance copies, changing enemy movement to make the rotation test faster, or restoring text tutorial modals.

### Rationale

A single gameplay/content baseline makes the two prototypes a valid interaction comparison: observed difficulty differences now come from routing itself rather than hidden wave or tuning drift. Causal pictograms introduce information at the moment it becomes relevant and remain language-independent, compact, and touch-safe.

### Implementation or verification result

Both TypeScript/Vite production builds passed. A source comparison reports exactly one line difference under `src/`: `routingMode.ts` exports `link` or `rotation`. Live desktop/mobile diagnostics on both production aliases returned identical Level 2 counts `[10,14,19,25,32,40]`, HP `[1.1,1.35,1.65,2.05,2.5,3.1]`, spawn densities `[1.26,2.65,3.43,5.52,7.5,8.79]`, Level 3 counts `[20,27,35,44,54,65,77,90,104,120]`, HP `[1.2,1.5,1.9,2.35,2.85,3.4,4,4.7,5.5,6.4]`, and densities `[2.84,3.84,4.35,7.1,8.78,9.79,12.32,16.62,20.06,33.07]`. The same checks confirmed enemy speed `0.6×`, projectile speed `3×`, cadence `1.5×`, radius `0.84`, visual scale `2×`, and no console/page errors.

The link Playwright matrix passed 47 tests with 10 intentional skips; the stabilized reciprocal-link check then passed 2/2 on desktop/mobile. The rotation matrix passed 44 tests with 13 intentional skips; its bot passed after widening only the test wait budget for the intentionally slower enemy traversal. Production deployments `dpl_8qaLGCdEdwu6T19bY2cApZeeFeZu` and `dpl_FbVU23vMH4mr5pfftj4oLfEfy5qc` reached `READY`. Their aliases and final hashed JS/CSS assets returned HTTP 200 at [arcane-arsenal-link-network.vercel.app](https://arcane-arsenal-link-network.vercel.app) and [arcane-arsenal-tower-defense.vercel.app](https://arcane-arsenal-tower-defense.vercel.app).

## Entry 23 — Rebuild the rotation tutorial around visible failure and elemental stacking

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-vtk`

### Problem being addressed

The rotation prototype taught Fire too early, before the player saw why a neutral Foundry was insufficient. Its Arcana discovery card also covered the balance it was meant to introduce. The tutorial stopped after a single Fire infusion instead of teaching a two-element stack and showing the first actual elemental reaction.

### Prompt used

The user requested that the rotation tutorial place Foundry first, start Wave 1, wait until the neutral shot fails to handle an enemy, then teach placing Fire, rotating Foundry into Fire, and rotating Fire toward the lane. Afterward it must add Ice, teach the Fire + Ice stack, and introduce elemental reactions on the first real reaction. The user also requested a text-free correction for the overlapping money tutorial.

### Important AI response

The AI changed the tutorial into a causal live-combat sequence. A surviving neutral hit or expired neutral projectile records the failure; Wave 1 then holds without blocking input while the player completes Foundry → Fire → enemy and Foundry → Fire → Ice → enemy rotations. Combat resumes only after Ice is aimed, and the existing text-free reaction pictogram fires on the first real reaction. The Arcana discovery cue now opens below its highlighted HUD target.

### Option selected, revised, or rejected

- **Selected:** unlock only Foundry at the start and require Wave 1 to begin before Fire appears.
- **Selected:** use an observable neutral failure as the trigger for the Fire lesson instead of an arbitrary timer.
- **Selected:** freeze combat progression while preserving selection and smooth hold-to-rotate input, preventing enemies from escaping during the explanation.
- **Selected:** teach Fire as the first interception, then Ice as a second interception, and resume combat for a live Fire + Ice reaction.
- **Selected:** auto-dismiss the portrait inspector when the next objective targets a different tower, keeping the battlefield tappable on mobile.
- **Selected:** keep all tutorial communication text-free and move the Arcana cue below the metric rather than hiding the balance.
- **Rejected:** opening Fire before the first wave, explaining reactions with a modal, or changing the explicit-link prototype.

### Rationale

The player now experiences the problem before receiving the solution, then extends the same learned action into elemental stacking. Holding the live wave preserves causality while preventing tutorial pacing from punishing slower mouse or touch input. The HUD cue remains readable because it points at, but no longer obscures, the value being taught.

### Implementation or verification result

The rotation build passed TypeScript/Vite compilation and the complete Playwright matrix with 47 passes and 13 intentional mode/viewport skips. A full desktop/mobile input test placed all three towers, observed the neutral failure, completed every hold-to-rotate objective, resumed combat, and detected the first Fire + Ice reaction plus its one-time pictogram. Ten deterministic visual baselines passed. Production deployment `dpl_8P2KUZBgjNtaQngmP69zyMmPpAia` reached `READY` at [arcane-arsenal-tower-defense.vercel.app](https://arcane-arsenal-tower-defense.vercel.app); the alias and hashed JavaScript/CSS assets returned HTTP 200, and live desktop/mobile smoke reported rotation mode with no console or page errors. The independent link folder and deployment were not modified.
