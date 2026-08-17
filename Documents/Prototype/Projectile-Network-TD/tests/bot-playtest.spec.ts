import { expect, test } from '@playwright/test';

test('deterministic bot proves pressure, reward, and clean retry', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name.includes('mobile'), 'Deterministic simulation is shared; mobile interaction is covered by visual and gameplay tests.');
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  const metrics = await page.evaluate(() => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__!;
    hooks.setReducedMotion(true);
    hooks.seed(20260816);
    hooks.autoBuildMinimumChain();
    hooks.startWave();
    hooks.advance(12);
    hooks.dismissReactionTutorial();
    hooks.advance(60);
    return hooks.snapshot();
  });
  expect(metrics.directHits as number).toBeGreaterThan(0);
  expect((metrics.killedEnemies as number) + (metrics.leakedEnemies as number)).toBeGreaterThan(0);
  expect(metrics.gold as number).not.toBe(200);

  const reset = await page.evaluate(() => {
    window.__THREE_GAME_TEST_HOOKS__!.reset();
    return window.__THREE_GAME_TEST_HOOKS__!.snapshot();
  });
  expect(reset.phase).toBe('preparation');
  expect(reset.nodeCount).toBe(0);
  expect(reset.projectileCount).toBe(0);
  expect(reset.enemyCount).toBe(0);
  expect(reset.gold).toBe(420);
  expect(reset.baseHp).toBe(3);
});
