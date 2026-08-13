# AI Collaboration Log — Browser Prototype — 14/08/2026

## Session metadata

- **Project:** `TowerDefense3D`
- **Area:** `Documents/Prototype`
- **Responsible Codex session:** `019ffa0d-09cb-7df2-b2e2-cd1e72bd2a74`
- **Tracking issues:** `TowerDefense3D-z67`, `TowerDefense3D-ga7`, `TowerDefense3D-2eg`, `TowerDefense3D-4pm`, `TowerDefense3D-0o5`, `TowerDefense3D-11i`
- **Production prototype:** [tower-defense-am-duong.vercel.app](https://tower-defense-am-duong.vercel.app)

This file records consequential prototype decisions from the responsible session. It summarizes decisions and verification evidence rather than reproducing the raw transcript.

## Entry 1 — Expand the prototype into a guided campaign

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
