import { expect, test } from '@playwright/test';

test.beforeEach(async ({ page }) => {
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
});

for (const cue of ['currency', 'reaction', 'nexus'] as const) {
  test(`first ${cue} event uses a text-free visual cue`, async ({ page }, testInfo) => {
    await page.evaluate((kind) => window.__THREE_GAME_TEST_HOOKS__?.setState(`intro-${kind}` as 'intro-currency'), cue);
    if (cue === 'currency' || cue === 'nexus') {
      await page.waitForFunction((kind) => window.__THREE_GAME_DIAGNOSTICS__?.discoveryCueTriggerCounts[kind] === 1, cue);
      await expect(page.locator(cue === 'currency' ? '.metric.money' : '.metric.lives')).toHaveClass(/discovery-target/);
      if (cue === 'nexus') {
        const screenshotPath = testInfo.outputPath('nexus-highlight.png');
        await page.screenshot({ fullPage: true, path: screenshotPath });
        await testInfo.attach('nexus-highlight', { path: screenshotPath, contentType: 'image/png' });
      }
      await expect(page.locator('#discovery-cue')).toBeHidden();
      await expect(page.locator('#discovery-card')).toBeHidden();
      await expect(page.locator('#discovery-card')).toBeEmpty();
      await expect(page.locator('#reaction-tutorial-overlay')).toBeHidden();
      if (cue === 'nexus') {
        await expect(page.locator('#toast')).toBeHidden();
        await expect(page.locator('#toast')).toBeEmpty();
      }
      const diagnostics = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
      expect(diagnostics?.discoveryCueHighlightOnly).toBe(true);
      expect(diagnostics?.reactionTutorialPopupVisible).toBe(false);
      expect(diagnostics?.phase).not.toBe('paused');
      return;
    }
    await page.waitForFunction((kind) => {
      const diagnostics = window.__THREE_GAME_DIAGNOSTICS__;
      return diagnostics?.discoveryCueVisible && diagnostics.discoveryCueKind === kind;
    }, cue);
    const card = page.locator('#discovery-card');
    await expect(card).toBeVisible();
    const pictogram = (await card.textContent()) ?? '';
    expect(pictogram).not.toMatch(/[A-Za-zÀ-ỹ]/);
    const diagnostics = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
    expect(diagnostics?.discoveryCueTriggerCounts[cue]).toBe(1);
  });
}

test('only the configured routing controls are exposed', async ({ page }) => {
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('tutorial-ready'));
  const mode = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.routingMode);
  expect(mode === 'link' || mode === 'rotation').toBeTruthy();
  if (mode === 'link') {
    await expect(page.locator('#action-link')).toHaveCount(0);
    await expect(page.locator('#action-left')).toHaveClass(/hidden/);
    await expect(page.locator('#action-right')).toHaveClass(/hidden/);
  } else {
    await expect(page.locator('#action-link')).toHaveCount(0);
    expect(await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.weaponAimGuideWidth)).toBeGreaterThanOrEqual(0.25);
  }
});

test('Link tutorial pauses on its first elemental reaction and resumes after the matching explanation', async ({ page }, testInfo) => {
  const mode = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.routingMode);
  test.skip(mode !== 'link', 'Only applies to the explicit-link variant.');
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('tutorial-ready'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialObjective)).toBe('start-wave-3');
  await page.locator('#start-wave').click();
  await expect.poll(
    () => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.reactionTutorialPopupVisible),
    { timeout: 25_000 },
  ).toBe(true);
  await expect(page.locator('#reaction-tutorial-overlay')).toBeVisible();
  await expect(page.locator('#reaction-tutorial-title')).toHaveText('Sốc Nhiệt');
  await expect(page.locator('.reaction-formula')).toHaveText('◆+✦→✹');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('paused');
  const screenshotPath = testInfo.outputPath('link-reaction-tutorial-modal.png');
  await page.screenshot({ fullPage: true, path: screenshotPath });
  await testInfo.attach('link-reaction-tutorial-modal', { path: screenshotPath, contentType: 'image/png' });
  await page.locator('#reaction-tutorial-continue').click();
  await expect(page.locator('#reaction-tutorial-overlay')).toBeHidden();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('wave');
});

test('rotation tutorial advances by holding a continuous turn control', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chrome', 'Pointer-hold behavior is covered once on desktop.');
  const mode = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.routingMode);
  test.skip(mode !== 'rotation', 'Only applies to the rotation variant.');
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('tutorial-rotation'));
  await page.waitForFunction(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialObjective === 'rotate-foundry-fire');
  const button = page.locator('#action-right');
  await expect(button).toBeVisible();
  await button.dispatchEvent('pointerdown', { button: 0, pointerId: 41, pointerType: 'mouse' });
  await page.waitForFunction(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialObjective === 'start-wave-1');
  await button.dispatchEvent('pointerup', { button: 0, pointerId: 41, pointerType: 'mouse' });
  expect(await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.weaponAimGuideObjects)).toBe(1);
});
