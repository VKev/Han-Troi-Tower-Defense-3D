import { expect, test, type Page } from '@playwright/test';

type BaselineState = 'active-play' | 'wave-intel' | 'tower-detail' | 'fail' | 'win';

async function prepare(page: Page, state: BaselineState): Promise<void> {
  await page.goto('/');
  await page.waitForFunction(() => (window.__THREE_GAME_DIAGNOSTICS__?.frame ?? 0) > 10);
  await page.evaluate((stateName) => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__;
    if (!hooks) throw new Error('Deterministic screenshot hooks are unavailable.');
    hooks.seed(20260814);
    hooks.setReducedMotion(true);
    hooks.hideDebugUi(true);
    hooks.setState(stateName === 'wave-intel' || stateName === 'tower-detail' ? 'tutorial-ready' : stateName);
  }, state);
  if (state === 'active-play') {
    await page.waitForFunction(() => {
      const diagnostics = window.__THREE_GAME_DIAGNOSTICS__;
      return diagnostics?.phase === 'wave' && diagnostics.towers === 9 && diagnostics.enemies >= 1;
    });
    await page.waitForTimeout(550);
  } else if (state === 'wave-intel') {
    await expect(page.locator('[data-enemy-kind="runner"]')).toContainText('×2');
    await page.locator('[data-enemy-kind="runner"]').click();
    await expect(page.locator('#wave-enemy-detail')).toBeVisible();
  } else if (state === 'tower-detail') {
    await page.locator('[data-tower-info="fire"]').click();
    await expect(page.locator('#tower-inspector.catalog-view')).toBeVisible();
  } else {
    await expect(page.locator('#result-overlay')).toBeVisible();
  }
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setPausedForScreenshot(true));
  await page.waitForTimeout(100);
}

for (const state of ['active-play', 'wave-intel', 'tower-detail', 'fail', 'win'] as const) {
  test(`${state} visual baseline`, async ({ page }, testInfo) => {
    await prepare(page, state);
    await expect(page).toHaveScreenshot(`${state}-${testInfo.project.name}.png`, {
      fullPage: true,
      maxDiffPixelRatio: state === 'active-play' ? 0.025 : state === 'wave-intel' || state === 'tower-detail' ? 0.02 : 0.01,
    });
  });
}
