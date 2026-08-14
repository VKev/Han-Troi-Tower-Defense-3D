import { expect, test } from '@playwright/test';

type Snapshot = {
  frame: number;
  phase: string;
  money: number;
  lives: number;
  enemies: number;
  projectiles: number;
  connections: number;
};

test('tower-defense bot network produces, fights, pauses and keeps progressing', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chrome', 'One deterministic desktop run is sufficient for the simulation bot.');
  test.setTimeout(45_000);
  const consoleErrors: string[] = [];
  const pageErrors: string[] = [];
  page.on('console', (message) => { if (message.type() === 'error') consoleErrors.push(message.text()); });
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('response', (response) => { if (response.status() >= 400) consoleErrors.push(`HTTP ${response.status()} ${response.url()}`); });

  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await page.evaluate(() => {
    window.__THREE_GAME_TEST_HOOKS__?.seed(4711);
    window.__THREE_GAME_TEST_HOOKS__?.setState('active-play');
  });
  await page.waitForFunction(() => window.__THREE_GAME_DIAGNOSTICS__?.phase === 'wave');
  await page.locator('#speed-button').click();

  const samples: Snapshot[] = [];
  for (let index = 0; index < 8; index += 1) {
    await page.waitForTimeout(650);
    const snapshot = await page.evaluate(() => {
      const d = window.__THREE_GAME_DIAGNOSTICS__;
      if (!d) return null;
      return { frame: d.frame, phase: d.phase, money: d.money, lives: d.lives, enemies: d.enemies, projectiles: d.projectiles, connections: d.connections };
    });
    if (snapshot) samples.push(snapshot);
  }

  await page.locator('#pause-button').click();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('paused');
  const pausedFrame = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.frame ?? 0);
  await page.waitForTimeout(250);
  expect(await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.frame ?? 0)).toBeGreaterThan(pausedFrame);
  await page.locator('#pause-button').click();

  const before = samples[0];
  const after = samples[samples.length - 1];
  await page.waitForFunction(() => {
    const phase = window.__THREE_GAME_DIAGNOSTICS__?.phase;
    return phase === 'ready' || phase === 'lost';
  }, undefined, { timeout: 20_000 });
  const betweenWaves = await page.evaluate(() => ({
    phase: window.__THREE_GAME_DIAGNOSTICS__?.phase,
    wave: window.__THREE_GAME_DIAGNOSTICS__?.wave,
    money: window.__THREE_GAME_DIAGNOSTICS__?.money,
    lives: window.__THREE_GAME_DIAGNOSTICS__?.lives,
  }));
  await page.locator('#start-wave').click();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('wave');
  const secondWave = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.wave);
  const report = {
    seed: 4711,
    samples: samples.length,
    framesAdvanced: after.frame - before.frame,
    sawHostiles: samples.some((sample) => sample.enemies > 0),
    sawProjectiles: samples.some((sample) => sample.projectiles > 0),
    economyOrNexusChanged: after.money !== before.money
      || after.lives !== before.lives
      || betweenWaves.money !== before.money
      || betweenWaves.lives !== before.lives,
    connections: after.connections,
    betweenWaves,
    secondWave,
    consoleErrors,
    pageErrors,
  };
  await testInfo.attach('tower-defense-bot-report', { body: JSON.stringify(report, null, 2), contentType: 'application/json' });
  console.log(`tower defense bot: ${JSON.stringify(report)}`);

  expect(report.framesAdvanced).toBeGreaterThan(120);
  expect(report.sawHostiles).toBe(true);
  expect(report.sawProjectiles).toBe(true);
  expect(report.economyOrNexusChanged).toBe(true);
  expect(report.connections).toBeGreaterThanOrEqual(3);
  expect(report.betweenWaves.phase).toBe('ready');
  expect(report.betweenWaves.wave).toBe(2);
  expect(report.betweenWaves.lives).toBeGreaterThan(0);
  expect(report.secondWave).toBe(2);
  expect(consoleErrors).toEqual([]);
  expect(pageErrors).toEqual([]);
});
