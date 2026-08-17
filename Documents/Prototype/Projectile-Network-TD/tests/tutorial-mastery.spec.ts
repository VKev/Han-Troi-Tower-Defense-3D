import { expect, test, type Page } from '@playwright/test';

async function boot(page: Page): Promise<void> {
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setReducedMotion(true));
}

test('Cóc Kiện Trời tutorial copies the Link road, authored tower positions, and six-wave mastery curve', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('mastery-ready'));
  const state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state.pathPoints).toEqual([{ x: -11, z: -2 }, { x: -3, z: -2 }, { x: -3, z: 2 }, { x: 7, z: 2 }]);
  expect(state.pathSegmentLengths).toEqual([8, 4, 10]);
  expect(state).toMatchObject({
    waveCount: 6,
    waveEnemyCounts: [4, 6, 7, 12, 16, 21],
    waveHealthMultipliers: [1, 1.08, 1.18, 6, 10, 14],
    masteryWaveCounts: [12, 16, 21],
    masteryWaveHealthMultipliers: [6, 10, 14],
    tutorialMasteryPhase: true,
    masteryCheckpointCaptured: true,
    masteryCheckpointMoney: 340,
    tutorialStartingLives: 3,
    tutorialLeakDamage: 1,
  });
  const authoredPositions = (state.nodes as Array<{ position: number[] }>).map((node) => node.position).sort((a, b) => a[2] - b[2]);
  expect(authoredPositions).toEqual([[-1, 0.62, -6], [-5, 0.62, -4], [-5, 0.62, 0], [-1, 0.62, 4]]);
  const projected = await page.evaluate(() => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__!;
    const generator = hooks.getNodeClientPoint('generator')!;
    const fire = hooks.getNodeClientPoint('fire')!;
    const ice = hooks.getNodeClientPoint('ice')!;
    const fireToGenerator = { x: generator.x - fire.x, y: generator.y - fire.y };
    const fireToIce = { x: ice.x - fire.x, y: ice.y - fire.y };
    const cosine = (fireToGenerator.x * fireToIce.x + fireToGenerator.y * fireToIce.y)
      / (Math.hypot(fireToGenerator.x, fireToGenerator.y) * Math.hypot(fireToIce.x, fireToIce.y));
    return {
      iceBelowGenerator: ice.y - generator.y,
      iceHorizontalOffset: Math.abs(ice.x - generator.x),
      routeAngleAtFire: Math.acos(Math.max(-1, Math.min(1, cosine))) * 180 / Math.PI,
    };
  });
  expect(projected.iceBelowGenerator).toBeGreaterThan(60);
  expect(projected.iceHorizontalOffset).toBeLessThan(35);
  expect(projected.routeAngleAtFire).toBeGreaterThan(20);
  expect(state.nodePurchasePriceMultiplier as number).toBeCloseTo(1.36, 8);
  expect(state.nodePurchasePrices).toMatchObject({ generator: 109, fire: 96, ice: 96 });
  expect(state.fixedNexus).toMatchObject({ kind: 'frog', position: [6.2, 0, 2], visible: true, coreVisible: true, separateFromSoulAnchor: true });
  await expect(page.locator('#tutorial-hand')).toBeHidden();
  await expect(page.locator('.build-card[data-type="wind"]')).toBeDisabled();
  await expect(page.locator('.build-card[data-type="generator"]')).toBeEnabled();
});

test('mastery retry restores the post-reaction checkpoint instead of replaying guidance', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('mastery-fail'));
  await expect(page.locator('#result-overlay')).toBeVisible();
  await expect(page.locator('#result-restart')).toHaveText('Thử lại 3 đợt');
  await page.locator('#result-restart').click();
  const restored = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(restored).toMatchObject({ phase: 'preparation', waveIndex: 3, gold: 340, baseHp: 3, nodeCount: 4, tutorialObjective: 'free-play' });
  expect(restored.masteryCheckpointCaptured).toBe(true);
  await expect(page.locator('#tutorial-hand')).toBeHidden();
});

test('unchanged tutorial chain loses the final mastery wave while an expanded reaction network plus Cóc skill wins', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name.includes('mobile'), 'The deterministic combat outcome is shared by desktop and mobile.');
  await boot(page);
  await page.evaluate(() => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__!;
    hooks.setState('mastery-baseline-final'); hooks.advance(180);
  });
  expect((await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).phase).toBe('lost');

  await page.evaluate(() => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__!;
    hooks.setState('mastery-expanded-final'); hooks.advance(180);
  });
  const expanded = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(expanded).toMatchObject({ phase: 'won', nodeCount: 9, soulCasts: 1 });
  expect(expanded.reactionProcs as number).toBeGreaterThan(0);
});

test('money and first Nexus damage use highlight-only onboarding with no popup', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('intro-currency'));
  let state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state).toMatchObject({ currencyTutorialSeen: true, currencyHighlightActive: true });
  await expect(page.locator('.metric.gold')).toHaveClass(/tutorial-focus/);
  await expect(page.locator('#result-overlay')).toBeHidden();
  await expect(page.locator('#reaction-tutorial-overlay')).toBeHidden();

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('intro-nexus'));
  state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state).toMatchObject({ baseHp: 2, baseTutorialSeen: true, baseHighlightActive: true });
  await expect(page.locator('.metric.base')).toHaveClass(/tutorial-focus/);
  await expect(page.locator('#result-overlay')).toBeHidden();
  await expect(page.locator('#reaction-tutorial-overlay')).toBeHidden();
});

test('first mastery wave teaches dragging from the frog meter to cast Cóc Bắt Mồi on desktop and mobile', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__!;
    hooks.setState('mastery-ready');
    hooks.setPausedForScreenshot(true);
    hooks.startWave();
    hooks.advance(0.25);
  });
  await expect(page.locator('#soul-skill')).toBeEnabled();
  await expect(page.locator('#soul-skill')).not.toHaveClass(/tutorial-focus/);
  await expect(page.locator('#tutorial-hand')).toBeHidden();
  expect(await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.getSoulSkillTargetClientPoint())).toBeNull();
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.advance(0.35));
  await expect(page.locator('#soul-skill')).toHaveClass(/tutorial-focus/);
  await expect(page.locator('#tutorial-hand')).toBeVisible();
  await expect(page.locator('#tutorial-hand')).toHaveAttribute('data-mode', 'drag');

  const button = await page.locator('#soul-skill').boundingBox();
  const handStart = await page.locator('#tutorial-hand').evaluate((hand) => ({
    x: Number.parseFloat((hand as HTMLElement).style.getPropertyValue('--hand-start-x')),
    y: Number.parseFloat((hand as HTMLElement).style.getPropertyValue('--hand-start-y')),
  }));
  const target = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.getSoulSkillTargetClientPoint());
  if (!button || !target) throw new Error('Missing Cóc Bắt Mồi drag endpoints');
  expect(handStart.x).toBeCloseTo(button.x + button.width / 2, 0);
  expect(handStart.y).toBeCloseTo(button.y + button.height / 2, 0);
  await page.mouse.move(button.x + button.width / 2, button.y + button.height / 2);
  await page.mouse.down();
  await page.mouse.move(target.x, target.y, { steps: 10 });
  let snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.soulSkillDrag).toMatchObject({
    active: true, hasPreview: true,
    previewParts: expect.arrayContaining(['tongueImpactPreview', 'tongueCorridorPreview', 'tongueCorridorCorePreview']),
  });
  expect(snapshot.soulSkillTutorialState).toBe('target');
  await page.mouse.up();
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.advance(0.2));

  snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot).toMatchObject({ soulSkillTutorialState: 'complete', soulCasts: 1 });
  expect(snapshot.tongueSkill).toMatchObject({ active: true, phase: 'impact' });
  expect((snapshot.tongueSkill as { impactHits: number }).impactHits).toBeGreaterThan(0);
  await expect(page.locator('#tutorial-hand')).toBeHidden();
});
