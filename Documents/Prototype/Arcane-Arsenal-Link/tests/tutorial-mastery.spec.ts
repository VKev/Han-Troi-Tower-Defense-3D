import { expect, test } from '@playwright/test';

test.beforeEach(async ({ page }) => {
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
});

test('post-reaction mastery adds three strictly harder free-build waves', async ({ page }) => {
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('mastery-ready'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.wave)).toBe(4);
  const diagnostics = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);

  expect(diagnostics?.stage).toBe(1);
  expect(diagnostics?.wave).toBe(4);
  expect(diagnostics?.waveCount).toBe(6);
  expect(diagnostics?.phase).toBe('ready');
  expect(diagnostics?.lives).toBe(3);
  expect(diagnostics?.tutorialStartingLives).toBe(3);
  expect(diagnostics?.tutorialLeakDamage).toBe(1);
  expect(diagnostics?.guidedTutorialComplete).toBe(true);
  expect(diagnostics?.tutorialMasteryPhase).toBe(true);
  expect(diagnostics?.masteryCheckpointCaptured).toBe(true);
  expect(diagnostics?.masteryCheckpointMoney).toBe(340);
  expect(diagnostics?.masteryWaveCounts).toEqual([12, 16, 21]);
  expect(diagnostics?.masteryWaveHealthMultipliers).toEqual([6, 10, 14]);
  expect(diagnostics?.unlockedTowers).toBe(3);
  expect(diagnostics?.tutorialHandVisible).toBe(false);
  expect(diagnostics?.tutorialObjective).toBe('');

  const density = diagnostics?.masteryWaveSpawnDensities ?? [];
  const threat = diagnostics?.masteryWaveThreats ?? [];
  expect(density).toHaveLength(3);
  expect(threat).toHaveLength(3);
  expect(density[1]).toBeGreaterThan(density[0]);
  expect(density[2]).toBeGreaterThan(density[1]);
  expect(threat[1]).toBeGreaterThan(threat[0]);
  expect(threat[2]).toBeGreaterThan(threat[1]);
  await expect(page.locator('#start-wave')).toBeEnabled();
  await expect(page.locator('#tutorial-hand')).toBeHidden();
});

test('final mastery wave defeats the unchanged tutorial network but an affordable second reaction branch wins', async ({ page }) => {
  test.setTimeout(60_000);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('mastery-baseline-final'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.wave)).toBe(6);
  const baseline = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(baseline?.wave).toBe(6);
  expect(baseline?.towers).toBe(4);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase), { timeout: 30_000 }).toBe('lost');

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('mastery-expanded-final'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.towers)).toBe(7);
  const expanded = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(expanded?.wave).toBe(6);
  expect(expanded?.towers).toBe(7);
  expect(expanded?.connections).toBe(6);
  expect(expanded?.money).toBeGreaterThanOrEqual(0);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase), { timeout: 30_000 }).not.toBe('wave');
  const expandedResult = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(expandedResult?.phase, JSON.stringify(expandedResult)).toBe('won');
});

test('three tutorial leaks lose and retry restores the post-reaction Wave 4 checkpoint', async ({ page }) => {
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('mastery-two-leaks'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.lives)).toBe(1);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('ready');

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('mastery-fail'));
  const failed = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(failed?.lives).toBe(0);
  expect(failed?.phase).toBe('lost');
  const checkpointLinks = failed?.towerLinks.map(({ sourceId, targetId }) => ({ sourceId, targetId })) ?? [];
  const checkpointTowerCount = failed?.towers;
  await expect(page.locator('#result-restart')).toHaveText('Thử lại 3 đợt');

  await page.locator('#result-restart').click();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('ready');
  const restored = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(restored?.wave).toBe(4);
  expect(restored?.lives).toBe(3);
  expect(restored?.money).toBe(340);
  expect(restored?.towers).toBe(checkpointTowerCount);
  expect(restored?.towerLinks.map(({ sourceId, targetId }) => ({ sourceId, targetId }))).toEqual(checkpointLinks);
  expect(restored?.masteryCheckpointCaptured).toBe(true);
  expect(restored?.tutorialHandVisible).toBe(false);
  await expect(page.locator('#result-overlay')).toBeHidden();
  await expect(page.locator('#start-wave')).toBeEnabled();
});
