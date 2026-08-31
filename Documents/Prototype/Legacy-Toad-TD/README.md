# Browser Prototype

**Status:** Playable
**Source concept:** [Raw game concept](../GameDesign/Raw_Game_Concept_The_Toad_Is_Heavens_Uncle.md)

## Run

Open [`index.html`](index.html) in a browser. No installation or network connection is required.

## Campaign

- Level 1: Bear, three waves, and a guided Bear tutorial.
- Level 2: Bee, six waves, and a guided lesson for Bee Yang's triple damage against hasted enemies.
- Levels 3–5: six waves each, unlocking Fox, Crab, then Water Tower.
- All five levels are selectable. `Choose Level` in the tutorial skips it for the selected level.
- Progress lasts only for the current run. Refreshing restarts Level 1 and the Bear tutorial.

The game uses a fullscreen battlefield with in-game controls, a level menu, contextual tower inspection, range previews, damage numbers, status feedback, water upgrades, and pausing between waves.

## Prototype Rules

- Each tower cycles from Yang to Yin and back as Karma fills and discharges.
- Bear, Bee, and Fox can upgrade damage, attack speed, or range.
- Crab upgrades its aura range; Water Tower upgrades water production.
- Magic damage ignores physical armor. Physical damage ignores magic resistance.
- Bear Yin hastes enemies by 45%; the haste remains for two seconds after they leave its range.
- Bee Yang deals triple direct damage to hasted enemies, and its poison scales with movement speed.
- Fox Yin attacks five times faster. Crab Yin halves nearby towers' Yin discharge while retaining its 20% attack-speed penalty.
- Towers pause between waves. Selling is available only between waves and refunds 60% of invested water.
- All enemy health values, including Elite health, use 60% of the earlier prototype balance.
- Enemy rewards are intentionally low, making wave bonuses and Water Tower production important.

Values are provisional and exist only to test the gameplay loop.
