import { expect, test } from '@playwright/test';

test.beforeEach(async ({ page }) => {
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
});

test('Level 2 final wave defeats the stale circuit but the earned expansion survives', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chrome', 'One deterministic desktop combat run covers campaign fairness.');
  test.setTimeout(45_000);
  await page.evaluate(() => {
    window.__THREE_GAME_TEST_HOOKS__?.seed(260816);
    window.__THREE_GAME_TEST_HOOKS__?.setState('stage-two-baseline-final');
  });
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.advance(160));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('lost');
  const baseline = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(baseline?.towers).toBe(3);

  await page.evaluate(() => {
    window.__THREE_GAME_TEST_HOOKS__?.seed(260816);
    window.__THREE_GAME_TEST_HOOKS__?.setState('stage-two-expanded-final');
    window.__THREE_GAME_TEST_HOOKS__?.setSpeed(2);
  });
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.towers)).toBe(14);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('wave');
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.advance(160));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).not.toBe('wave');
  const expanded = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(expanded?.towers).toBe(14);
  expect(expanded?.money).toBeGreaterThanOrEqual(0);
  expect(expanded?.reactions).toBeGreaterThan(0);
  expect(expanded?.phase, JSON.stringify(expanded)).toBe('won');
});

test('Level 3 Wave 6 requires separate reaction branches for ground and both air lanes', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chrome', 'One deterministic desktop combat run covers the late-game strategy gate.');
  test.setTimeout(60_000);
  await page.evaluate(() => {
    window.__THREE_GAME_TEST_HOOKS__?.seed(260817);
    window.__THREE_GAME_TEST_HOOKS__?.setState('stage-three-baseline-late');
  });
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.stage)).toBe(3);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.towers)).toBe(4);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('wave');
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.advance(180));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.lives)).toBeLessThan(20);
  const baseline = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(baseline?.towers).toBe(4);
  expect(['wave', 'lost']).toContain(baseline?.phase);

  await page.evaluate(() => {
    window.__THREE_GAME_TEST_HOOKS__?.seed(260817);
    window.__THREE_GAME_TEST_HOOKS__?.setState('stage-three-expanded-late');
  });
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.towers)).toBe(24);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('wave');
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.advance(180));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).not.toBe('wave');
  const expanded = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(expanded?.maxTowerLayer).toBe(1);
  expect(expanded?.reactions).toBeGreaterThan(0);
  expect(expanded?.activeReactionBarriers).toBe(0);
  expect(expanded?.lives).toBe(20);
  expect(expanded?.wave).toBe(7);
  expect(expanded?.phase, JSON.stringify(expanded)).toBe('ready');
});
