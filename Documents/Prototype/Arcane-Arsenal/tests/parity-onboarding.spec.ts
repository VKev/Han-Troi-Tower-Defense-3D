import { expect, test } from '@playwright/test';

async function tapCell(page: import('@playwright/test').Page, gx: number, gz: number, pointerType: 'mouse' | 'touch'): Promise<void> {
  const point = await page.evaluate(
    ({ x, z }) => window.__THREE_GAME_TEST_HOOKS__?.getCellClientPoint(x, z),
    { x: gx, z: gz },
  );
  expect(point).not.toBeNull();
  if (!point) return;
  if (pointerType === 'touch') await page.touchscreen.tap(point.x, point.y);
  else await page.mouse.click(point.x, point.y);
}

async function tapTower(page: import('@playwright/test').Page, towerId: number, pointerType: 'mouse' | 'touch'): Promise<void> {
  const point = await page.evaluate((id) => window.__THREE_GAME_TEST_HOOKS__?.getTowerClientPoint(id), towerId);
  expect(point).not.toBeNull();
  if (!point) return;
  if (pointerType === 'touch') await page.touchscreen.tap(point.x, point.y);
  else await page.mouse.click(point.x, point.y);
}

async function rotateToTutorialTarget(page: import('@playwright/test').Page, nextObjective: string, pointerId: number, pointerType: 'mouse' | 'touch'): Promise<void> {
  const direction = await page.evaluate(() => {
    const diagnostics = window.__THREE_GAME_DIAGNOSTICS__;
    const current = diagnostics?.selectedOutputAngle;
    const target = diagnostics?.tutorialRotationTargetAngle;
    if (current === null || current === undefined || target === null || target === undefined) return 0;
    return Math.atan2(Math.sin(target - current), Math.cos(target - current)) < 0 ? -1 : 1;
  });
  expect(direction).not.toBe(0);
  const button = page.locator(direction < 0 ? '#action-left' : '#action-right');
  await expect(button).toBeVisible();
  await button.dispatchEvent('pointerdown', { button: 0, pointerId, pointerType });
  await page.waitForFunction((objective) => window.__THREE_GAME_DIAGNOSTICS__?.tutorialObjective === objective, nextObjective);
  await button.dispatchEvent('pointerup', { button: 0, pointerId, pointerType });
}

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
    await expect(page.locator('#action-left')).toHaveClass(/hidden/);
    await expect(page.locator('#action-right')).toHaveClass(/hidden/);
  } else {
    await expect(page.locator('#action-link')).toHaveClass(/hidden/);
    expect(await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.weaponAimGuideWidth)).toBeGreaterThanOrEqual(0.25);
  }
});

test('rotation tutorial advances by holding a continuous turn control', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chrome', 'Pointer-hold behavior is covered once on desktop.');
  const mode = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.routingMode);
  test.skip(mode !== 'rotation', 'Only applies to the rotation variant.');
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('tutorial-rotation'));
  await page.waitForFunction(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialObjective === 'rotate-fire-lane');
  await rotateToTutorialTarget(page, 'start-wave-2', 41, 'mouse');
  expect(await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.weaponAimGuideObjects)).toBe(0);
});

test('rotation tutorial proves Foundry damage, Fire input dependency, and Fire plus Ice reaction', async ({ page }, testInfo) => {
  test.setTimeout(150_000);
  const mode = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.routingMode);
  test.skip(mode !== 'rotation', 'Only applies to the rotation variant.');
  const pointerType = testInfo.project.name.includes('mobile') ? 'touch' : 'mouse';

  await page.locator('[data-tower-type="foundry"]').click();
  await tapCell(page, 4, 2, pointerType);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialObjective)).toBe('start-wave-1');
  await expect(page.locator('[data-tower-type="fire"]')).toBeDisabled();

  await page.locator('#start-wave').click();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialObjective)).toBe('observe-foundry-kill');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHandVisible)).toBe(false);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialWorldCueObjects)).toBe(0);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialNeutralKillObserved), { timeout: 30_000 }).toBe(true);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialObjective), { timeout: 45_000 }).toBe('place-fire');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('ready');
  await expect(page.locator('[data-tower-type="fire"]')).toBeEnabled();

  await page.locator('[data-tower-type="fire"]').click();
  await tapCell(page, 3, 5, pointerType);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialObjective)).toBe('rotate-fire-lane');
  const [foundryId, fireId] = await page.evaluate(() => Object.keys(window.__THREE_GAME_DIAGNOSTICS__?.towerBuffers ?? {}).map(Number));
  await rotateToTutorialTarget(page, 'start-wave-2', 51, pointerType);

  await page.locator('#start-wave').click();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialObjective)).toBe('observe-fire-idle');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialFireIdleObserved), { timeout: 35_000 }).toBe(true);
  expect(await page.evaluate((id) => window.__THREE_GAME_DIAGNOSTICS__?.projectileLaunchesByTower[id] ?? 0, fireId)).toBe(0);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialCombatHeld)).toBe(true);
  await tapTower(page, foundryId, pointerType);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialObjective)).toBe('rotate-foundry-fire');
  await rotateToTutorialTarget(page, 'finish-wave-2', 52, pointerType);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialCombatHeld)).toBe(false);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialObjective), { timeout: 55_000 }).toBe('place-ice');

  await page.locator('[data-tower-type="ice"]').click();
  await tapCell(page, 2, 4, pointerType);
  await page.waitForFunction(() => Object.keys(window.__THREE_GAME_DIAGNOSTICS__?.towerBuffers ?? {}).length >= 3);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialObjective)).toBe('select-fire');
  await tapTower(page, fireId, pointerType);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialObjective)).toBe('rotate-fire-ice');
  await rotateToTutorialTarget(page, 'start-wave-3', 53, pointerType);
  await page.locator('#start-wave').click();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialObjective)).toBe('await-first-reaction');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHandVisible)).toBe(false);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialWorldCueObjects)).toBe(0);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.reactions), { timeout: 20_000 }).toBeGreaterThan(0);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.discoveryCueTriggerCounts.reaction)).toBe(1);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.reactionTutorialPopupVisible), { timeout: 5_000 }).toBe(true);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('paused');
  await expect(page.locator('#reaction-tutorial-overlay')).toBeVisible();
  await expect(page.locator('#reaction-tutorial-title')).toHaveText('Sốc Nhiệt');
  await page.locator('#reaction-tutorial-continue').click();
  await expect(page.locator('#reaction-tutorial-overlay')).toBeHidden();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('wave');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialObjective)).toBe('complete');
});
