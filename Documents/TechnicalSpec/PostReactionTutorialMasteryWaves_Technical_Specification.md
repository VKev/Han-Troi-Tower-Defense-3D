# Post-Reaction Tutorial Mastery Waves — Technical Specification

## Scope

This change extends Stage 1 in both Arcane Arsenal web prototypes. The first three waves remain the authored, routing-specific tutorial. Three new waves then remove the guided hand and ask the player to prove that they can build and route an elemental network independently.

The Link and Rotation variants must use the same Stage 1 roster, health multipliers, spawn timings, rewards, Nexus rules, and retry checkpoint. Their only intended difference is how a player routes ammunition between towers.

## Player promise

After the game demonstrates Fire + Ice and the first elemental reaction, the player receives a short, fair mastery exam: read the incoming roster, spend earned Arcana, extend the network, and use reactions to protect a fragile Nexus.

The intended feeling is a transition from supported discovery to tactical ownership. Failure should create an immediate “try a better network” response, not force the player to repeat lessons already learned.

## Core loop

Preview the next roster, spend Arcana on a compact elemental network, start the wave, observe projectile paths and reactions, then adapt the network before or during the next denser wave.

## Encounter structure

| Wave | Purpose | Roster | Health multiplier | Clear bonus |
|---|---|---|---:|---:|
| 1–3 | Existing guided onboarding | Existing authored roster | Existing | Existing |
| 4 — Tự Xây Mạch | First independent build decision | 9 Riftlings, 3 Runners | 6 | 105 |
| 5 — Áp Lực Kép | Sustained mixed pressure | 10 Riftlings, 5 Runners, 1 Brute | 10 | 125 |
| 6 — Chứng Nhận Mạch | Final dense reaction check | 12 Riftlings, 7 Runners, 2 Brutes | 14 | 150 |

All Stage 1 enemies remain on ground layer 0. Spawn windows overlap and become denser each wave. The health curve is intentionally steep enough that the exact tutorial checkpoint network loses the final mastery wave. A second affordable Foundry → Ice → Fire reaction branch placed on useful lane intersections clears that same wave in both routing variants, making network expansion—not passive reuse—the intended answer.

## Tutorial boundary and free play

- The guided tutorial ends when Wave 3 is cleared after the first reaction lesson.
- Wave 4 begins with no tutorial hand, forced placement cell, forced routing target, or start-wave gate.
- Foundry, Fire, and Ice remain the Stage 1 tower set. The player may freely place, move, sell, upgrade, link, or rotate those towers during Waves 4–6.
- Wind, Earth, Amplifier, and the special tower remain reserved for later stages.
- The incoming-enemy preview remains visible before every mastery wave.

## Nexus and failure rules

- Stage 1 starts with 3 Nexus lives.
- Every Stage 1 leak removes exactly 1 life, regardless of the enemy's normal later-stage Nexus damage.
- The third leaked enemy ends the run.
- Other stages retain their existing starting lives and per-enemy Nexus damage.

## Mastery checkpoint

A clean checkpoint is captured after Wave 3 is fully resolved and its clear reward is granted, immediately before Wave 4 becomes ready. It contains:

- Arcana balance and 3 restored Nexus lives;
- every tutorial tower's type, cell, layer-derived placement, level, investment, ammo, routing target or aim angle, timers, and amplifier branch;
- next runtime identifiers and discovered tutorial cues needed to avoid replaying onboarding;
- Wave 4 as the next wave, with no live enemies, projectiles, transient VFX, selection, or tutorial pointer.

If the player loses during Waves 4–6, the result action restores this checkpoint. Towers or upgrades purchased after the checkpoint are intentionally discarded so every retry starts from the same fair tactical decision point. Losing during Waves 1–3 keeps the existing full Stage 1 restart behavior.

## Feedback and UI

- Wave counter shows six total Stage 1 waves.
- Wave 4–6 use normal free-play interaction feedback rather than tutorial-only silence.
- The defeat action reads as retrying the mastery challenge when the checkpoint is available.
- Enemy detail reports a one-life leak penalty during Stage 1.
- Diagnostics expose whether the guided phase is complete, whether the mastery checkpoint exists, the checkpoint balance, mastery wave counts/health/density, and the one-life tutorial leak rule.

## Verification

- Both projects build successfully.
- Automated desktop and mobile checks confirm six Stage 1 waves and strictly increasing Wave 4–6 count, health, density, and threat.
- The tutorial hand is absent and free placement/actions are enabled at Wave 4.
- Two Stage 1 leaks leave one life; the third leak loses.
- Restart after a mastery loss restores Wave 4, 3 lives, the captured Arcana balance, and the captured routing/aim state.
- Link and Rotation diagnostics report identical Stage 1 wave balance data.
- A deterministic desktop/mobile pressure test confirms that the unchanged tutorial network loses Wave 6 while the checkpoint network plus one affordable three-tower Ice + Fire branch wins it.

## Non-goals

- No new map, tower, enemy model, elemental reaction, permanent progression, or Stage 2/3 rebalance.
- No mid-wave checkpointing.
- No change to the distinction between Link routing and Rotation aiming.
