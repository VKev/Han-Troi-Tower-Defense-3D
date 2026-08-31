# Tower Upgrade Feedback and Level Two Onboarding Technical Specification

- **Status:** Approved
- **Owner approval:** Direct implementation request on 2026-08-18
- **Target:** `Documents/Prototype/Projectile-Network-TD/`

## Scope

The player-reported "upgrade does not work" issue was diagnosed as three compounding UX gaps in the existing (functionally correct) branch-upgrade purchase flow, not a broken purchase mechanic:

1. The standalone `⇧ Nâng cấp` button next to the branch cards performed no purchase and gave no feedback when the real blocker (insufficient gold) applied — it silently did nothing.
2. The two branch cards (`#branch-a` / `#branch-b`) were hard-`disabled` whenever gold was insufficient, which blocks the click event entirely, so the existing "Không đủ Vàng." error toast in `purchaseBranch` never had a chance to fire. The player saw a greyed-out button with no explanation.
3. Nothing in the game ever taught the player that towers can be upgraded at all, or where to look, especially around the Level 2 (Cóc Kiện Trời stage two) lesson waves that grant a free `Tiếp Sức`/`Sấm` tower.

The fix is presentation-only: it does not change branch effects, upgrade costs, gold economy, or the underlying `purchaseBranch` purchase logic, which was already correct.

## Non-goals

- No change to branch effects (`conduit`/`resonance`/`rapid`/`heavy`/etc.), upgrade costs, or gold economy.
- No mandatory gate blocking wave start on purchasing an upgrade — Level 2's granted gold at the lesson wave (45/55) is intentionally below every upgrade cost (62-110), so forcing a purchase would risk soft-locking a player who spent gold elsewhere.
- No new tutorial popup or text panel; the fix reuses the existing highlight-only onboarding pattern already used for the currency and base-damage lessons.

## Architecture and ownership

- `src/game/Game.ts` owns `purchaseBranch`, `focusBranchChoice`, and `renderInspector` (branch card/upgrade button presentation).
- `src/styles.css` owns the `.unaffordable` and `.branch-controls.tutorial-focus` / `.node-actions button.tutorial-focus` presentation rules.
- `tests/gameplay.spec.ts` and `tests/stage-two-lessons.spec.ts` own deterministic coverage.

## Runtime contracts

- `focusBranchChoice` (bound to `#action-upgrade`) now checks gold before animating the branch cards; if the node's `upgradeCost` exceeds current gold it calls the existing `error('Không đủ Vàng.')` toast instead of doing nothing.
- `renderInspector`'s branch-card `disabled` condition no longer includes the gold check — only `phase !== 'preparation'` or an already-assigned `node.branch` disables a card. Insufficient gold instead toggles a new `.unaffordable` class (dimmed, still clickable) so clicking it reaches `purchaseBranch`'s existing gold check and its toast.
- A node counts as the active "upgrade lesson" when `node.group.userData.stageTwoLessonType` is set (the existing marker for the free-granted Level 2 tower) and `node.branch` is still `null`. While true, `renderInspector` toggles `.tutorial-focus` on `#branch-controls` and `#action-upgrade`, reusing the existing pulse animation already used for build-card, HUD-metric, and soul-skill onboarding. The highlight clears the moment a branch is purchased or a different node is selected.
- Diagnostics: each node in `snapshot().nodes` now also exposes `stageTwoLessonType` (`'support' | 'special' | null`) so tests and future tooling can identify the lesson tower without re-deriving the highlight condition.

## Interaction flow

1. Player selects any branchable tower. Branch cards are visible as before; if affordable they behave exactly as before.
2. If gold is short, both branch cards are dimmed (`.unaffordable`) but remain clickable, and the `⇧ Nâng cấp` button also responds — either path now surfaces "Không đủ Vàng." on click instead of nothing happening.
3. In Level 2, once the free lesson tower (`Tiếp Sức` on Wave 3, `Sấm` on Wave 4) exists and is selected, its branch-controls area pulses with the same attention-highlight style already used elsewhere, until the player purchases either branch. This is a passive nudge, not a blocking requirement.

## Verification plan

- Production TypeScript build.
- `tests/gameplay.spec.ts`: a sufficient-gold branch purchase via a real click clears the highlight and updates node/gold state; regression-checked against the pre-existing branch-copy test.
- `tests/stage-two-lessons.spec.ts`: the Level 2 lesson tower shows `.tutorial-focus` on `#branch-controls`/`#action-upgrade` and `.unaffordable` (not `disabled`) on `#branch-a` at the deterministic wave-four gold level (55 < 85), and clicking it surfaces the "Không đủ Vàng." toast without mutating `branch` or `gold`.
- Full Playwright matrix (desktop + mobile).

## Implementation result

- `focusBranchChoice` and the branch-card `disabled`/`unaffordable` logic in `renderInspector` were updated in [Game.ts](../../Documents/Prototype/Projectile-Network-TD/src/game/Game.ts) exactly as specified above; `purchaseBranch` itself was unchanged (it already handled the gold check and toast correctly).
- Added `.unaffordable` and `.branch-controls.tutorial-focus, .node-actions button.tutorial-focus` rules to [styles.css](../../Documents/Prototype/Projectile-Network-TD/src/styles.css), reusing the existing `tutorial-focus-pulse` keyframe.
- Added `stageTwoLessonType` to the per-node diagnostics snapshot.
- Manual real-click verification (Playwright against a production preview, not just test hooks) confirmed: a fully-funded Level 1 Hỏa tower purchase still works unchanged; the Level 2 wave-four `Tiếp Sức` tower (gold 55 vs. cost 85) shows the pulsing highlight and the dimmed-but-clickable branch card, and clicking it produces the "Không đủ Vàng." toast with no state mutation.
- New deterministic tests added to `tests/gameplay.spec.ts` and `tests/stage-two-lessons.spec.ts`. The full Playwright matrix passed `99` tests with `11` intentional skips (10 pre-existing viewport routing skips plus one new desktop-only skip for the branch-purchase-by-direct-click test, matching the pre-existing skip pattern for the adjacent branch-copy test). No visual baseline changed.
