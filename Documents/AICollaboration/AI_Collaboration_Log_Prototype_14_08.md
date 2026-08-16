# AI Collaboration Log — Browser Prototype — 14/08/2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Area:** `Documents/Prototype`
- **Responsible Codex sessions:** `019ffa0d-09cb-7df2-b2e2-cd1e72bd2a74`, `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Responsible ChatGPT chats:** `6a7e858a-80fc-8322-9e39-ebfb4fcaa7db`, `6a7c3d0c-d31c-8323-9f15-4baea55ecb54`
- **Tracking issues represented in these entries:** `TowerDefense3D-ndx`, `TowerDefense3D-aj8`, `TowerDefense3D-ayz`, `TowerDefense3D-ar3`, `TowerDefense3D-29j`, `TowerDefense3D-r2g`, `TowerDefense3D-985`, `TowerDefense3D-01t`, `TowerDefense3D-7f3`, `TowerDefense3D-5d9`, `TowerDefense3D-7zd`, `TowerDefense3D-yya`, `TowerDefense3D-b69`, `TowerDefense3D-clw`, `TowerDefense3D-cri`, `TowerDefense3D-9en`, `TowerDefense3D-6do`
- **Legacy Toad production prototype:** [tower-defense-am-duong.vercel.app](https://tower-defense-am-duong.vercel.app)
- **Rotation prototype:** [`Documents/Prototype/Arcane-Arsenal/`](../Prototype/Arcane-Arsenal/) — [production](https://arcane-arsenal-tower-defense.vercel.app)
- **Explicit-link prototype:** [`Documents/Prototype/Arcane-Arsenal-Link/`](../Prototype/Arcane-Arsenal-Link/) — [production](https://arcane-arsenal-link-network.vercel.app)

This file records consequential prototype decisions from the responsible sessions and chats. It summarizes decisions and verification evidence rather than reproducing raw transcripts.

## Entry 1 — Expand the prototype into a guided campaign

- **Responsible Codex session:** `019ffa0d-09cb-7df2-b2e2-cd1e72bd2a74`

### Problem being addressed

The initial browser prototype needed a full-screen campaign structure that introduced towers progressively while remaining easy to replay, skip, and test.

### Prompt used

The user requested five levels that unlock Bear, Bee, Fox, Crab, and Water Tower in order; three waves for the Bear tutorial; six waves for later levels; in-game controls; guided placement and upgrade steps; selectable levels; and a refresh behavior that restarts the tutorial.

### Important AI response

The AI proposed a state-driven tutorial whose pauses are triggered by real gameplay events, including buying, placing, upgrading, selling, starting waves, waiting for enemies to enter range, and observing Yang or Yin behavior. It also separated one-run campaign state from persistent browser storage.

### Option selected, revised, or rejected

- **Selected:** a five-level full-screen campaign with contextual tutorial focus, animated click cues, wave-specific teaching, and a level selector.
- **Selected:** allow any level to be selected so the tutorial can be skipped for testing or direct play.
- **Revised:** progression no longer persists in `localStorage`; refreshing the page restarts Level 1 and its tutorial.
- **Rejected:** advancing tutorial explanations before the relevant enemy or tower event occurs.

### Rationale

Event-driven steps keep the explanation synchronized with visible gameplay, while selectable levels support rapid testing. Restarting from Level 1 makes every fresh browser session reproduce the intended onboarding path.

### Implementation or verification result

The prototype now provides five selectable levels, three Bear tutorial waves, six waves per later level, in-game menus, contextual focus cues, and pause/resume behavior tied to tutorial and enemy introductions. The smoke test verifies level counts, tutorial gates, tower placement positions, unlock order, and refresh-state behavior.

## Entry 2 — Make combat rules and feedback readable

- **Responsible Codex session:** `019ffa0d-09cb-7df2-b2e2-cd1e72bd2a74`

### Problem being addressed

Players could not reliably understand tower range, damage changes, status effects, enemy traits, altar damage, or whether combat timers continued between waves.

### Prompt used

The user requested range previews on tower selection, deselection by tapping the ground, visible damage numbers, clearer buff and debuff feedback, enemy introductions, hoverable status icons, animated wings for flying enemies, a visible altar health bar, water-funded upgrades, stronger altar damage, and complete tower inactivity between waves.

### Important AI response

The AI recommended causal feedback close to the affected unit: floating damage values, status badges on enemies, an active status legend, range circles, first-seen enemy overlays, and explicit health-bar changes. It kept physical armor and magic resistance as separate defensive systems rather than removing armor when magic damage is applied.

### Option selected, revised, or rejected

- **Selected:** tower range visualization, ground-tap deselection, floating physical, magic, and poison damage, enemy status badges, and first-seen enemy introductions.
- **Selected:** magic bypasses physical armor, physical damage bypasses magic resistance, and native resistance still reduces matching damage.
- **Selected:** tower production, cooldowns, Karma discharge, and attacks stop between waves.
- **Selected:** damage, attack speed, and range upgrades use Water; Crab and Water Tower retain specialized upgrade choices.
- **Revised:** altar damage was doubled, while enemy kill rewards were reduced to preserve economy pressure.

### Rationale

Visible cause-and-effect lets players learn the system without relying on dense text. Separate defense channels preserve meaningful enemy traits, and freezing all tower work between waves prevents hidden economy or timing advantages.

### Implementation or verification result

The prototype displays range, damage, poison ticks, status icons, flying-wing animation, enemy introductions, and altar health feedback. Smoke coverage verifies armor and resistance behavior, invisible-enemy reveal, flying-enemy animation, doubled altar damage, Water upgrades, and frozen tower timers between waves.

## Entry 3 — Refine Yang–Yin mechanics and prototype balance

- **Responsible Codex session:** `019ffa0d-09cb-7df2-b2e2-cd1e72bd2a74`

### Problem being addressed

Tower identities and late-wave balance needed clearer mechanical outcomes, especially for haste, poison, Elite enemies, Fox Yin, and Crab Yin.

### Prompt used

The user requested that Bee Yang deal triple damage to fast enemies without presenting the rule as a named Bear–Bee combo; Bear Yin haste should remain for two seconds outside its range; poison should visibly deal at least five damage and scale with speed; all enemy health should fall to 60% of the earlier values; Fox Yin should attack five times faster; and Crab Yin should replace its 75% Karma-gain bonus with slower Yin discharge while retaining the attack-speed penalty.

### Important AI response

The AI separated each rule into a reusable status interaction: Bee checks whether a target is hasted regardless of the haste source, Bear stores a timed haste after aura exit, poison derives its damage from current movement speed, and Crab modifies the discharge rate of nearby Yin towers rather than their Yang Karma gain.

### Option selected, revised, or rejected

- **Selected:** Bee Yang deals exactly three times its direct damage to any hasted enemy; the player-facing Vietnamese feedback reads `ONG DƯƠNG · x3` (Bee Yang · x3) without naming a tower combination.
- **Selected:** Bear Yin grants 45% haste that persists for two seconds after leaving the aura.
- **Selected:** poison starts at five damage per second and increases when movement exceeds base speed; Bee Yin applies armor and shielding without applying new poison.
- **Selected:** all enemy and Elite health uses a global 0.6 multiplier.
- **Selected:** Fox Yin uses a five-times attack-speed multiplier.
- **Selected:** Crab Yin halves nearby towers' Yin discharge and retains its 20% attack-speed penalty.
- **Rejected:** the previous Crab Yin 75% Yang Karma-gain bonus and explicit Bear-times-Bee labels.

### Rationale

Status-based rules are easier to understand and reuse than hard-coded named combinations. The health reduction keeps the expanded campaign playable, while stronger Yin identities make phase management and aura placement more consequential.

### Implementation or verification result

Automated smoke checks verify Bee's exact triple direct damage, speed-scaled poison, Bear's two-second haste persistence, Elite health scaling, Fox Yin cooldown at five times base attack speed, Crab Yin discharge at half speed, removal of the old 75% Karma multiplier, and retention of Crab's 20% attack-speed penalty. Each verified iteration was deployed to the production Vercel alias.

## Entry 4 — Simplify and rebalance Arcane Arsenal Level 2

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-ndx`

### Problem being addressed

The Three.js Arcane Arsenal prototype's second level was too visually complex, exposed three firing heights, started with an excessive 920 Arcana, and contained only three waves. It did not provide the requested longer enemy route or staged introductions for the Amplifier and Nexus Lance.

### Prompt used

The user requested a simpler and larger Level 2 with ground enemies plus only one flying height, a longer enemy route, six increasingly difficult waves, exactly 160 starting Arcana, and text-free Amplifier and Lance lessons before Waves 3 and 4 respectively.

### Important AI response

The AI proposed keeping Layer 0 ground combat and a single Layer 1 flying/build tier while removing Layer 2 from all Level 2 terrain and wave compositions. It also proposed a 56-unit route with four clear turns, a strictly increasing six-wave threat curve, and reuse of the existing animated hand, glow, and placement-ring language for the two unlock lessons.

### Option selected, revised, or rejected

- **Selected:** exactly 160 starting Arcana instead of 920.
- **Selected:** a larger continuous battlefield with a 56-unit route and one raised Layer 1 plateau.
- **Selected:** six authored waves whose measured threat budgets rise from `685` to `6953`.
- **Selected:** Amplifier remains locked until the preparation for Wave 3; Nexus Lance remains locked until the preparation for Wave 4.
- **Selected:** both tower introductions use drag-hand, card glow, placement ring, and start-wave pointer cues without tutorial text.
- **Rejected:** Layer 2 terrain, Layer 2 Frost Ray spawns, three-height encounter messaging, and line-of-fire walls in this level.

### Rationale

The reduced height model makes routing and enemy coverage readable while the longer path gives projectile networks more room to operate. The 160-Arcana opening forces an immediate Foundry-plus-support purchasing decision, and the later support unlocks introduce new strategic roles only after the basic two-height defense has been established.

### Implementation or verification result

Level 2 now reports `startingMoney: 160`, `pathLength: 56`, `waveCount: 6`, `maxBoardLayer: 1`, and `maxStageEnemyLayer: 1`. Desktop and mobile automation verifies both visual unlock lessons, real placement, locked-state progression, and the increasing threat curve. The production build passed; the complete Playwright suite passed `20` tests with `6` intentional platform skips; production-preview canvas inspection reported no console or page errors and stayed within budget at `141` desktop / `111` mobile draw calls and `15,676` desktop / `12,562` mobile triangles.

Further Level 2 terrain-accessibility revisions are recorded in Entry 9.

## Entry 5 — Define Concept 2 as a projectile-routing tower defense game

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-aj8`

### Problem being addressed

The project needed a complete second raw concept that was independent from the Toad, Yin–Yang, and Karma design. The central idea—towers firing ammunition through other towers instead of directly acquiring enemies—still contained unresolved rules for routing, buffers, elements, elevation, enemies, economy, progression, and loss conditions.

### Prompt used

The user described four tower categories: ammunition producers, ammunition-support towers, tower-support towers, and special towers that store ammunition for a skill. The user then answered successive design questions about manual touch linking, one input buffer and one output, many producers feeding one processor, non-branching outputs, legal loops and buffer backpressure, elemental stacking and reactions, three firing layers, fixed enemy paths, free grid building during waves, economy, movement, upgrades, stage progression, permanent tower unlocks, and Nexus lives.

### Important AI response

The AI converted the confirmed answers into a structured English raw concept rather than filling material gaps with assumptions. It separated projectile payload composition from elemental states on enemies, documented multi-source FIFO behavior and full-buffer rejection, treated loops as legal but congestion-prone, and marked prototype questions separately from confirmed rules.

### Option selected, revised, or rejected

- **Selected:** `Arcane Arsenal`, a new colorful 3D fantasy concept combining mechanical ammunition systems with magic.
- **Selected:** seven initial tower roles: Foundry, Fire, Ice, Wind, Earth, Amplifier, and Nexus Lance.
- **Selected:** one logical output per ammunition tower, multiple producers feeding one receiver, sequential buffer discharge, non-branching output, crossed trajectories that do not interfere, and projectiles that can damage multiple enemies in flight.
- **Selected:** Fire, Ice, Wind, and Earth payload combinations plus compatible elemental reactions on enemies; repeated elements do not duplicate a payload.
- **Selected:** firing layers `0`, `1`, and `2`, with a chain restricted to one layer and flying enemies assigned to flight layers.
- **Selected:** fixed enemy paths, multi-cell tower footprints, building during waves, kill income, no economy tower, paid movement, selling, upgrades, stage-local reset, permanent tower unlocks, and Nexus lives.
- **Rejected:** reusing the prior Toad world, Yin–Yang system, Karma cycle, or theme.

### Rationale

The relay network makes geometry and tower order the primary strategic verbs. Explicit buffer, layer, and reaction rules prevent the concept from becoming a conventional target-acquisition tower defense game with decorative elemental effects.

### Implementation or verification result

[`RawConcept_2.md`](../GameDesign/RawConcept_2.md) was created as a `Draft` English concept with the confirmed core loop, tower roster, routing and backpressure rules, element and reaction matrices, enemy defense profiles, progression, touch UX, examples, scope boundaries, and remaining prototype questions. Structural and UTF-8 checks passed. No Unity implementation was changed for this documentation task.

## Entry 6 — Establish the stylized magitech visual and combat-feedback direction

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`

### Problem being addressed

Early representative images did not communicate the intended network clearly: towers appeared as disconnected pairs, routes did not consistently cross enemy lanes, regular ammunition looked like lasers, and excessive projectile counts obscured the tactical idea.

### Prompt used

The user requested a colorful, stylized 3D mechanical-magic theme with chunky mobile-game readability similar in broad visual language to Brawl Stars. The user repeatedly clarified that towers must form a readable network, regular attacks must be physical projectiles rather than lasers, only one or two bullets need to be visible at once, trails should communicate motion, projectile paths should cut across enemy movement, and damaged enemies should visibly react.

### Important AI response

The AI revised the representative composition around a single readable producer-to-support network, sparse projectile timing, short trails, enemy-lane intersections, and direct hit feedback. It kept the special Lance skill distinct from ordinary projectile routing and treated the images as visual direction rather than production assets.

### Option selected, revised, or rejected

- **Selected:** stylized mechanical fantasy, saturated colors, rounded chunky silhouettes, and a readable top-down/isometric 3D presentation.
- **Selected:** sparse physical ammunition with trails, network order cues, enemy hit flash, and visible damage response.
- **Revised:** disconnected tower pairs became one continuous routed network that crosses enemy formations.
- **Rejected:** continuous laser-like regular attacks, projectile spam, and decorative routes that miss the enemy lane.

### Rationale

The core mechanic must be understandable from a screenshot or short clip. Sparse projectiles and visible enemy reactions preserve cause and effect, while the magitech style distinguishes the concept without copying protected characters or game assets.

### Implementation or verification result

Representative concept images were iterated to show networked towers, physical rounds, trails, lane crossings, and enemy damage. They remained external concept previews and were not imported as project assets. The same readability goals later informed the Three.js projectile trails, hit particles, elemental hues, and selected-shot guide.

## Entry 7 — Keep the raw concept authoritative and build a separate Three.js prototype

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issues:** `TowerDefense3D-ayz`, `TowerDefense3D-ar3`

### Problem being addressed

The supplied `Projectile_Network_TD_Pitch_Prototype_Brief.docx` overlapped with Concept 2 but contradicted several confirmed rules. After the comparison, the project needed a playable browser prototype without overwriting the legacy Toad prototype or silently treating the pitch brief as the new source of truth.

### Prompt used

The user first asked for a conflict and gap analysis between the pitch brief and `RawConcept_2.md`, then explicitly decided not to merge them. The user requested a complete Three.js 3D prototype based on Concept 2 and allowed the old prototype to be moved into a separate folder.

### Important AI response

The AI identified direct conflicts in support-tower identity, elemental payload representation, cross-layer transfer, in-wave rewiring, loop legality, unlinked output behavior, producer buffering, hit-once lifetime, and fixed-slot versus free-grid placement. It recommended using `RawConcept_2.md` as the product authority and treating the brief only as optional V0 acceptance/playtest material.

### Option selected, revised, or rejected

- **Selected:** do not merge the pitch brief into Concept 2.
- **Selected:** build the new prototype under `Documents/Prototype/Arcane-Arsenal/` and preserve the previous game under `Documents/Prototype/Legacy-Toad-TD/`.
- **Selected:** Three.js, Vite, deterministic fixed-step projectile collision, responsive touch-first UI, static relative-path HTML output, and automated browser QA.
- **Selected:** grid placement, buffers and backpressure, four elements and reactions, layered enemies, economy, Nexus lives, upgrades, paid movement, selling, Amplifier branches, and Nexus Lance discharge.
- **Rejected:** modifying the legacy prototype in place or importing the pitch brief's conflicting constraints as confirmed rules.

### Rationale

Separate folders preserve both prototypes and prevent the unrelated themes and mechanics from contaminating each other. A browser vertical slice can test the geometric projectile-network promise quickly while leaving full-campaign balance and permanent progression outside scope.

### Implementation or verification result

The Arcane Arsenal prototype received a production-ready `dist/` build with relative assets. Initial verification passed the production build, `11` Playwright tests with `3` intentional viewport skips, deterministic desktop/mobile visual baselines, a gameplay bot that progressed into Wave 2, and real-GPU canvas inspection within the documented budgets. The legacy Toad prototype remained preserved. No Arcane Arsenal deployment or Git commit was performed in this responsible session.

## Entry 8 — Rework Stage 1 into a text-free three-wave physical-routing tutorial

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issues:** `TowerDefense3D-29j`, `TowerDefense3D-r2g`, `TowerDefense3D-985`, `TowerDefense3D-01t`, `TowerDefense3D-7f3`, `TowerDefense3D-5d9`, `TowerDefense3D-7zd`, `TowerDefense3D-yya`, `TowerDefense3D-b69`

### Problem being addressed

The first playable version began with too much complexity, used elevated enemies too early, relied on modal text and manual linking, prescribed weak tower positions, showed enemies walking with incorrect orientation or lane offsets, and did not provide enough feedback for placement, projectile infusion, elemental states, reactions, or damage.

### Prompt used

Across successive playtest screenshots, the user requested a simpler ground-only first map, continuous terrain over a logical grid, animated drag-and-pointer teaching, exact winning placement positions, Foundry then Fire then Ice across three waves, enemies interleaved with the tutorial, no modal or tutorial text, press-and-hold smooth tower rotation, head-on shooting against enemy travel, removal of manual links, free-flying projectiles that are buffed only when physically crossing a support tower, stronger elemental hues/icons, hit flash and particles, and a thick translucent red projectile guide when a tower is selected.

### Important AI response

The AI iteratively moved Stage 1 from explicit source-target links to continuous-angle physical projectile interception. It used a tutorial-only direct Foundry discharge for Wave 1, taught Wave 2 only after Fire placement and live enemy engagement, then introduced Ice and an elemental reaction in Wave 3. Automated playtests were used to reject tutorial layouts that looked plausible but could not actually win.

### Option selected, revised, or rejected

- **Selected:** a separate ground-only Stage 1 with exactly three live tutorial waves: Foundry, Fire, then Ice.
- **Selected:** text-free guidance through card glow, animated hand, drag path, world marker, placement footprint, wave-button pointer, and held-rotation cue.
- **Selected:** drag-and-drop placement with click fallback, temporary logical-grid visualization during dragging, valid/invalid footprints, and translucent range fills.
- **Selected:** smooth press-and-hold rotation; release, cancel, or focus loss stops rotation immediately.
- **Revised:** terminal shots are aimed nearly opposite enemy movement along a long lane instead of crossing the lane perpendicularly for a short collision window.
- **Revised:** manual source-target links were removed. Ammunition now free-flies along tower aim, enters the first compatible same-layer tower it physically intersects, gains that tower's buff, and otherwise continues through enemies until range or a blocker ends the shot.
- **Selected:** predicted network visualization only for real interception, plus a temporary `0.17`-unit-wide red selected-shot guide at `0.42` opacity.
- **Selected:** stronger persistent elemental body hues and icons, multi-element projectile trails, hit flash, bounded particles, and compatible field reactions.
- **Rejected:** briefing/help modals, tutorial instruction cards, snap rotation, permanent grid seams, an Aim button, weak non-winning placements, and regular projectile lines that resemble lasers.

### Rationale

The visual-only sequence teaches one new verb per wave and demonstrates the game's actual physical-routing rule. Head-on projectile travel creates a longer and more reliable collision corridor, while explicit feedback makes each placement, interception, buff, hit, state, and reaction readable on mobile.

### Implementation or verification result

Stage 1 now starts ready without a briefing, teaches the complete Foundry → Fire → Ice circuit through visual cues, and can be won through the authored interaction flow. Enemy facing uses the correct model axis and lane offset is bounded to `0.28`. The terrain appears continuous while retaining logical placement cells. Final tutorial and interaction verification passed the production build, the full desktop/mobile Playwright suite used at each milestone, deterministic tutorial victory with elemental reactions, and desktop/mobile visual baselines without console or page errors. The current README documents physical interception and the selected-shot guide.

## Entry 9 — Move Level 2 highlands into practical lane coverage

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issues:** `TowerDefense3D-clw`, `TowerDefense3D-cri`

### Problem being addressed

After the six-wave Level 2 simplification, the only Layer 1 plateau sat too far from useful enemy-lane coverage. Elevated towers spent most of their shot range crossing empty ground. A later review also showed that the open right side lacked a raised tactical build option.

### Prompt used

The user identified the remote plateau in a gameplay screenshot and asked for high ground closer to the lane. After the central plateau was moved, the user clarified that the open ground on the right should also contain some high land while keeping the level to one raised height.

### Important AI response

The AI measured tower-to-path distance instead of moving scenery by eye. It relocated the main `4×3` Layer 1 plateau beneath the long central lane, then added a second `2×2` Layer 1 plateau on the right flank near the final bend. Deterministic demos and diagnostics were changed so both highlands had functional projectile chains.

### Option selected, revised, or rejected

- **Selected:** central plateau cells `gx 5..8`, `gz 3..5`, with demonstrated elevated towers no farther than `5` world units from a lane.
- **Selected:** a second right-side plateau at `gx 9..10`, `gz 2..3`.
- **Selected:** a Foundry → Earth chain on the right plateau aimed back across the lane.
- **Selected:** retain Layer 0 ground plus Layer 1 flying/build terrain only.
- **Rejected:** the remote southeast plateau and any return of Layer 2 terrain or Layer 2 enemies.

### Rationale

Raised terrain must create a tactical firing choice rather than consume range before projectiles reach enemies. Two compact highlands support alternative Layer 1 networks while preserving the simplified two-height readability established for Level 2.

### Implementation or verification result

Wave 6 automation proves that elevated towers damage Layer 1 flying enemies from the relocated terrain. The final deterministic layout places four Layer 1 towers across the two raised regions, including the right Foundry → Earth chain. `npm run build` passed; the complete Playwright suite passed `21` tests with `7` intentional platform skips; desktop/mobile active-play baselines were refreshed and visually checked; no console or page errors were reported. Physical-device mobile testing remains outside this browser QA evidence.

## Entry 10 — Research a distinctive tower-defense core mechanic

- **Responsible ChatGPT chat:** `6a7e858a-80fc-8322-9e39-ebfb4fcaa7db`

### Problem being addressed

The game needed a simple, marketable core-mechanic keyword that changes what the player repeatedly does, rather than only adding another tower roster or passive modifier system.

### Prompt used

The user requested unusual tower-defense mechanic keywords comparable in clarity to merge, random, or offense systems, then asked for more ideas informed by other games. The user subsequently focused the visual prototype on towers passing ammunition through one network whose straight and zigzag routes intersect as many enemies as possible.

### Important AI response

The AI compared ideas such as Orbit, Relay, Echo, Magnet, Swap, Core, Stack, String, Traffic, Record, Fuse, Catch, and others. It defined a good core mechanic as one that is explainable in one sentence, used continuously, applicable across the tower roster, and capable of producing decisions rather than ornament. `Relay` was described as towers passing ammunition or energy through one another, with tower order changing the projectile.

### Option selected, revised, or rejected

- **Selected for Concept 2 direction:** `RELAY`—the tower network itself is the weapon.
- **Selected:** straight physical segments, a single continuous chain or zigzag network, and route placement that maximizes enemy intersections.
- **Revised:** disconnected pairs and incorrect branches became one readable multi-tower route.
- **Not carried forward:** the AI's separate personal recommendation of `ORBIT`; it remained research rather than an approved project direction.

### Rationale

Relay directly supports the user's projectile-chain concept and makes order, angle, and geometry strategically meaningful. It is visible in screenshots and can expand through tower-specific buffs without changing the primary verb.

### Implementation or verification result

This chat produced research and visual-layout direction only. Its Relay conclusions are consistent with `RawConcept_2.md` and the Arcane Arsenal physical-routing prototype, but no code, asset import, or project file change is attributed to the ChatGPT chat itself.

## Entry 11 — Research licensable stylized 3D asset sources

- **Responsible ChatGPT chat:** `6a7c3d0c-d31c-8323-9f15-4baea55ecb54`

### Problem being addressed

The project needed candidate 3D assets with readable chibi proportions, rounded stylized forms, clean colors, and mobile-friendly top-down silhouettes, without copying or redistributing Brawl Stars or CookieRun intellectual property.

### Prompt used

The user requested a deep web search for free 3D assets in a broad Brawl Stars and CookieRun-like stylized direction.

### Important AI response

The AI shortlisted Meshtint toon assets, a Unity chibi character pack, KayKit characters, enemies, animations, nature and dungeon packs, CraftPix defence towers and medieval environment packs, Quaternius characters, monsters and environment kits, Kenney mini and tower-defense assets, a Fab casual-character pack, and the Synty starter pack. It recommended using one coherent ecosystem, a shared toon shader, smooth normals, and a controlled palette. It also warned against ripped or exact franchise models and noted that licenses must be checked per source and per asset.

### Option selected, revised, or rejected

- **Shortlisted, not approved:** KayKit characters, skeletons, animations, and forest assets; Meshtint or CraftPix towers; and a limited matching environment set.
- **Alternative shortlisted:** a coherent CC0-only combination from KayKit, Quaternius, and Kenney.
- **Rejected:** ripped, exact, or deceptively relabeled Brawl Stars and CookieRun models.
- **Deferred:** final asset-pack selection, download, import, attribution review, shader unification, and production art integration.

### Rationale

A coherent licensed asset family is safer legally and more visually consistent than mixing unrelated free models or copying protected characters. The shortlisted packs also offer mobile-oriented polygon counts, atlases, rigs, and animation coverage suitable for later evaluation.

### Implementation or verification result

The chat is recorded as research only. No listed asset was downloaded, imported, approved for production, or added to the repository during this collaboration. License terms and current availability must be revalidated before any future use.

## Entry 12 — Localize the prototype and make wave intelligence compact

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-9en`

### Problem being addressed

Players could not inspect an upcoming wave before committing to it, Level 2's economy was too restrictive, and the remaining English interface conflicted with the intended Vietnamese playtest audience. The first wave-intelligence layout also occupied too much battlefield space when enemy details were always visible.

### Prompt used

The user requested an exact pre-wave enemy preview from the tutorial through Level 2, inspectable enemy details, stronger Level 2 kill income, top-center placement for the preview, and Vietnamese localization of all player-facing content. After reviewing the first layout, the user requested a smaller preview whose details appear only on hover or click.

### Important AI response

The AI implemented the preview as a compact roster strip tied to the authored upcoming wave rather than a generic enemy encyclopedia. Desktop hover gives a temporary detail popover, click pins or closes it, and touch devices use click only. The detail layer is positioned independently so opening it does not resize the HUD. A stage-level reward multiplier keeps authored enemy values stable while allowing Level 2 balance to change independently.

### Option selected, revised, or rejected

- **Selected:** show the exact next-wave roster before the wave starts in both the tutorial and Level 2.
- **Selected:** place the compact roster at the top center, below the primary HUD, with responsive offsets around the side docks.
- **Selected:** reveal enemy health, speed, reward, height, resistances, immunity, and role only through hover or click; clicking pins the popover until it is toggled or closed.
- **Selected:** increase Level 2 kill rewards by `50%` through `killRewardMultiplier = 1.5`; keep Stage 1 at `1.0`.
- **Selected:** localize all player-facing HTML, tower data, enemy data, waves, controls, statuses, reactions, results, accessibility labels, and tooltips into Vietnamese while retaining the product names `Arcane Arsenal`, `Arcana`, and `Nexus`.
- **Revised:** replace the initially large always-open enemy detail panel with a compact non-reflowing popover.
- **Rejected:** a bottom-center roster and permanently visible enemy details that obscured the battlefield.

### Rationale

The player needs enough information to make a purchase decision without sacrificing combat visibility. A compact roster communicates wave composition at a glance, while on-demand details preserve depth for mouse and touch users. The stage multiplier eases Level 2 without duplicating every enemy definition, and complete localization makes the prototype coherent for the intended playtest.

### Implementation or verification result

The localized interface, exact roster preview, hover/click detail behavior, and Level 2 reward multiplier were implemented in the Arcane Arsenal prototype. Deterministic tests confirm that a Riftling reward is `11` Arcana in Stage 1 and rounds to `17` Arcana in Level 2. The production build passed, and the final Playwright run passed `26` tests with `8` intentional platform skips, including the gameplay bot, tutorial victory, reward/localization checks, desktop and mobile wave inspection, elemental reactions, and eight refreshed visual baselines. No console or page errors were reported; the only browser output was a non-failing WebGL shader precision warning from Three.js.

## Entry 13 — Simplify combat planning UI and protect mandatory Level 2 lessons

- **Responsible Codex session:** `019ffe7b-5f24-7130-8ff7-e26a9fdc8b71`
- **Tracking issue:** `TowerDefense3D-6do`

### Problem being addressed

The wave panel repeated a title and explanatory text that consumed battlefield space. Tower cards exposed purchase actions but no safe way to inspect a tower before spending. Level 2 enemy chips used ambiguous `T0` and `T1` labels, the enemy popover was visually cramped and could extend outside the portrait viewport, and the mandatory Amplifier and Nexus Lance tutorials could become impossible when the player had less Arcana than the listed price.

### Prompt used

The user requested less unnecessary UI text, an eye icon on every tower to reveal its details, clearer Level 2 information about whether an enemy flies and at which height, a visual redesign of the enemy detail UI, and special handling so the required Amplifier and Lance tutorial purchases remain possible with insufficient money and leave the balance at zero. The user also requested production deployment, AI collaboration logging, and a Git commit.

### Important AI response

The AI separated tower inspection from tower purchase instead of making locked cards clickable as build actions. It reused the existing right-side inspector for catalog details, kept the eye control available for locked towers, replaced numeric layer shorthand with movement-language badges, and gave the enemy popover its own responsive stat-card layout. The affordability exception was restricted to the currently required lesson tower, while recorded investment uses only the Arcana actually paid.

### Option selected, revised, or rejected

- **Selected:** remove the redundant pre-wave label, wave title, and hint while retaining the exact enemy roster and the start-wave button.
- **Selected:** add one independent eye button to each of the seven tower cards; viewing details does not select placement mode, place a tower, or spend Arcana.
- **Selected:** show tower price, footprint, range, storage behavior, role, unlock state, description, and upgrade summary in the existing inspector instead of a new modal.
- **Selected:** replace `T0` and `T1` with `MẶT ĐẤT` and `BAY · TẦNG 1`, plus `BAY TRÊN KHÔNG` and `Tầng bay 1` inside the detailed profile.
- **Revised:** turn the enemy profile into a darker bounded popover with a movement badge, compact stat cards, resistance chips, and a portrait-specific fixed inset so no content clips beyond the viewport.
- **Selected:** allow only the currently mandatory Amplifier or Nexus Lance lesson purchase to use the player's remaining Arcana and reduce it to exactly zero; normal purchases still require the full price.
- **Selected:** record the discounted tutorial tower's `totalInvested` from the amount actually paid so the fallback cannot create a later sell-profit exploit.
- **Rejected:** restoring explanatory wave copy, using another modal, enabling every unaffordable purchase, or continuing to represent flight with bare numeric layer codes.

### Rationale

Planning information should be available on demand without competing with the battlefield or changing game state. Movement-language labels explain the tactical rule directly, while restricting the affordability exception to the active forced lesson preserves normal economy decisions. Tracking actual payment keeps the tutorial safeguard economically neutral.

### Implementation or verification result

The final production build passed TypeScript and Vite compilation, `npm audit` reported zero vulnerabilities, and the full Playwright suite passed `30` tests with `8` intentional cross-viewport skips. Ten deterministic desktop/mobile visual baselines now include tower-detail and redesigned wave-intel states. The tower eye controls, locked-tower details, no-spend behavior, explicit ground/flying labels, portrait bounds, and `35 → 0` Amplifier plus `45 → 0` Lance lesson cases are covered by browser assertions. Vercel deployment `dpl_22NYUfazxiuTKW5mAW3sLRm1QGTB` reached `READY` and was aliased to `https://arcane-arsenal-tower-defense.vercel.app`; the page and its final hashed JS/CSS bundles returned HTTP 200. Direct production canvas inspection passed on desktop and mobile with real Intel D3D11 rendering, within all documented budgets, and with no console or page errors.
