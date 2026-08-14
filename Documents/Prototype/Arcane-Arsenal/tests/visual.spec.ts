import { expect, test, type Page } from '@playwright/test';
import { PNG } from 'pngjs';

type CanvasSample = {
  ok: boolean;
  variance: number;
  colorBuckets: number;
};

async function watchErrors(page: Page): Promise<{ consoleErrors: string[]; pageErrors: string[] }> {
  const result = { consoleErrors: [] as string[], pageErrors: [] as string[] };
  page.on('console', (message) => {
    if (message.type() === 'error') result.consoleErrors.push(message.text());
  });
  page.on('pageerror', (error) => result.pageErrors.push(error.message));
  page.on('response', (response) => {
    if (response.status() >= 400) result.consoleErrors.push(`HTTP ${response.status()} ${response.url()}`);
  });
  return result;
}

async function sampleCanvas(page: Page): Promise<CanvasSample> {
  const buffer = await page.locator('#game-canvas').screenshot();
  const png = PNG.sync.read(buffer);
  let min = 255;
  let max = 0;
  const buckets = new Set<string>();
  const stride = Math.max(1, Math.floor((png.width * png.height) / 4096));
  for (let pixel = 0; pixel < png.width * png.height; pixel += stride) {
    const offset = pixel * 4;
    const r = png.data[offset];
    const g = png.data[offset + 1];
    const b = png.data[offset + 2];
    min = Math.min(min, r, g, b);
    max = Math.max(max, r, g, b);
    buckets.add(`${r >> 4},${g >> 4},${b >> 4}`);
  }
  return { ok: max - min > 24 && buckets.size > 24, variance: max - min, colorBuckets: buckets.size };
}

const TUTORIAL_CELLS = {
  foundry: { gx: 8, gz: 3 },
  fire: { gx: 9, gz: 2 },
  ice: { gx: 10, gz: 3 },
} as const;
const TUTORIAL_FIRE_HEAD_ON_ANGLE = Math.atan2(1, -8);
const TUTORIAL_ICE_HEAD_ON_ANGLE = Math.atan2(-1, -8);

async function clickTutorialCell(page: Page, type: keyof typeof TUTORIAL_CELLS): Promise<void> {
  const cell = TUTORIAL_CELLS[type];
  const point = await page.evaluate(({ gx, gz }) => window.__THREE_GAME_TEST_HOOKS__?.getCellClientPoint(gx, gz), cell);
  expect(point).not.toBeNull();
  if (point) await page.mouse.click(point.x, point.y);
}

async function placeTutorialTower(page: Page, type: keyof typeof TUTORIAL_CELLS): Promise<void> {
  await page.locator(`[data-tower-type="${type}"]`).click();
  await clickTutorialCell(page, type);
}

async function holdTutorialRotation(
  page: Page,
  direction: 'left' | 'right',
  expectedStep: number,
  pointerId: number,
): Promise<void> {
  const button = page.locator(`#action-${direction}`);
  await button.dispatchEvent('pointerdown', {
    pointerId, pointerType: 'touch', isPrimary: true, button: 0,
    bubbles: true, cancelable: true,
  });
  await expect(button).toHaveClass(/pressed/);
  await page.waitForFunction((step) => window.__THREE_GAME_DIAGNOSTICS__?.tutorialStep === step, expectedStep, { timeout: 4_000 });
  await button.dispatchEvent('pointerup', {
    pointerId, pointerType: 'touch', isPrimary: true, button: 0,
    bubbles: true, cancelable: true,
  });
}

async function holdTutorialRotationUntilAngle(
  page: Page,
  direction: 'left' | 'right',
  targetAngle: number,
  pointerId: number,
): Promise<void> {
  const button = page.locator(`#action-${direction}`);
  await button.dispatchEvent('pointerdown', {
    pointerId, pointerType: 'touch', isPrimary: true, button: 0,
    bubbles: true, cancelable: true,
  });
  await expect(button).toHaveClass(/pressed/);
  await page.waitForFunction((target) => {
    const diagnostics = window.__THREE_GAME_DIAGNOSTICS__;
    const angle = diagnostics?.selectedOutputAngle;
    return angle !== null && angle !== undefined
      && Math.abs(Math.atan2(Math.sin(target - angle), Math.cos(target - angle))) < 0.01;
  }, targetAngle, { timeout: 4_000 });
  await button.dispatchEvent('pointerup', {
    pointerId, pointerType: 'touch', isPrimary: true, button: 0,
    bubbles: true, cancelable: true,
  });
}

async function openActiveNetwork(page: Page, state: 'active-play' | 'stress' = 'active-play'): Promise<void> {
  await page.goto('/');
  await page.waitForFunction(() => (window.__THREE_GAME_DIAGNOSTICS__?.frame ?? 0) > 10);
  await page.evaluate((name) => {
    window.__THREE_GAME_TEST_HOOKS__?.seed(20260814);
    window.__THREE_GAME_TEST_HOOKS__?.setReducedMotion(true);
    window.__THREE_GAME_TEST_HOOKS__?.setState(name);
  }, state);
  await page.waitForFunction(() => {
    const diagnostics = window.__THREE_GAME_DIAGNOSTICS__;
    return diagnostics?.phase === 'wave' && diagnostics.towers >= 9;
  });
}

test('active projectile network renders and reports bounded scene complexity', async ({ page }, testInfo) => {
  const errors = await watchErrors(page);
  await openActiveNetwork(page, 'active-play');
  await page.waitForTimeout(1200);

  const canvas = page.locator('#game-canvas');
  await expect(canvas).toBeVisible();
  const canvasBox = await canvas.boundingBox();
  expect(canvasBox?.width).toBeGreaterThan(300);
  expect(canvasBox?.height).toBeGreaterThan(400);

  const sample = await sampleCanvas(page);
  expect(sample, JSON.stringify(sample)).toMatchObject({ ok: true });
  await expect(page.locator('#build-list .build-button')).toHaveCount(7);
  await expect(page.locator('#build-list .build-category')).toHaveCount(4);
  await expect(page.locator('#build-list .build-category-title')).toHaveText([
    'Trụ sinh đạn', 'Hỗ trợ đạn', 'Hỗ trợ trụ', 'Trụ đặc biệt',
  ]);
  if (testInfo.project.name.includes('mobile')) await expect(page.locator('#tower-inspector')).toBeHidden();
  else await expect(page.locator('#tower-inspector')).toBeVisible();

  const diagnostics = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(diagnostics?.stage).toBe(2);
  expect(diagnostics?.maxTowerLayer).toBe(1);
  expect(diagnostics?.layerOneTowerCount).toBe(4);
  expect(diagnostics?.oppositeRaisedCellCount).toBe(4);
  expect(diagnostics?.oppositeRaisedTowerCount).toBe(2);
  expect(diagnostics?.maxLayerOneTowerLaneDistance).toBeLessThanOrEqual(5.01);
  expect(diagnostics?.maxBoardLayer).toBe(1);
  expect(diagnostics?.maxStageEnemyLayer).toBe(1);
  expect(diagnostics?.pathLength).toBeGreaterThanOrEqual(56);
  expect(diagnostics?.waveCount).toBe(6);
  expect(diagnostics?.maxEnemyFacingError).toBeLessThan(0.02);
  expect(diagnostics?.maxEnemyLaneOffset).toBeLessThanOrEqual(0.281);
  expect(diagnostics?.connections).toBeGreaterThanOrEqual(3);
  expect(diagnostics?.linkGuideObjects).toBeGreaterThanOrEqual(6);
  expect(diagnostics?.weaponAimGuideObjects).toBe(testInfo.project.name.includes('mobile') ? 0 : 1);
  if (!testInfo.project.name.includes('mobile')) {
    expect(diagnostics?.weaponAimGuideWidth).toBeGreaterThanOrEqual(0.16);
    expect(diagnostics?.weaponAimGuideOpacity).toBeCloseTo(0.42, 2);
  }
  expect(diagnostics?.infusions).toBeGreaterThan(0);
  expect(diagnostics?.projectileInterceptions).toBeGreaterThan(0);
  expect(diagnostics?.renderer.calls).toBeLessThanOrEqual(testInfo.project.name.includes('mobile') ? 150 : 300);
  expect(diagnostics?.renderer.triangles).toBeLessThanOrEqual(testInfo.project.name.includes('mobile') ? 300_000 : 750_000);
  expect(diagnostics?.canvas.dpr).toBeLessThanOrEqual(1.5);

  await testInfo.attach(`${testInfo.project.name}-active-network`, {
    body: await page.screenshot({ fullPage: true }),
    contentType: 'image/png',
  });
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('both Layer 1 plateaus support an active anti-air network beside the lane', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chrome', 'Desktop simulation proves Layer 1 combat; mobile covers the same authored layout visually.');
  test.setTimeout(40_000);
  const errors = await watchErrors(page);
  await openActiveNetwork(page, 'stress');
  await page.locator('#speed-button').click();
  await expect.poll(
    () => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.layerOneEnemyHits ?? 0),
    { timeout: 25_000 },
  ).toBeGreaterThan(0);
  const diagnostics = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(diagnostics?.layerOneTowerCount).toBe(4);
  expect(diagnostics?.oppositeRaisedCellCount).toBe(4);
  expect(diagnostics?.oppositeRaisedTowerCount).toBe(2);
  expect(diagnostics?.maxLayerOneTowerLaneDistance).toBeLessThanOrEqual(5.01);
  expect(diagnostics?.maxEnemyLayer).toBe(1);
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('guided stage unlocks a complete ground-only circuit before live enemies', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chrome', 'One deterministic desktop tutorial run covers progression logic.');
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await expect(page.locator('#briefing')).toHaveCount(0);
  await expect(page.locator('#help-button')).toHaveCount(0);
  await expect(page.locator('#tutorial-card')).toHaveCount(0);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('ready');
  await expect(page.locator('#tutorial-hand')).toBeVisible();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHandMode)).toBe('drag');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.pathRibbonMeshes)).toBe(2);
  await expect(page.locator('#build-list .build-button:not(:disabled)')).toHaveCount(1);
  await expect(page.locator('[data-tower-type="foundry"]')).toHaveClass(/tutorial-focus/);

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('tutorial-ready'));
  await page.waitForFunction(() => {
    const diagnostics = window.__THREE_GAME_DIAGNOSTICS__;
    return diagnostics?.stage === 1 && diagnostics.phase === 'ready' && diagnostics.tutorialStep === 9;
  });
  const diagnostics = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(diagnostics?.towers).toBe(3);
  expect(diagnostics?.connections).toBe(2);
  expect(diagnostics?.linkGuideObjects).toBe(4);
  expect(diagnostics?.weaponAimGuideObjects).toBe(1);
  expect(diagnostics?.maxTowerLayer).toBe(0);
  expect(diagnostics?.unlockedTowers).toBe(3);
  expect(diagnostics?.tutorialStep).toBe(9);
  expect(diagnostics?.tutorialHeadOnDot).toBeLessThan(-0.99);
  expect(diagnostics?.waveCount).toBe(3);
  await expect(page.locator('#start-wave')).toHaveClass(/tutorial-focus/);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHandMode)).toBe('tap');
  const routedTutorialPath = testInfo.outputPath('tutorial-physical-routing.png');
  await page.screenshot({ fullPage: true, path: routedTutorialPath });
  await testInfo.attach('tutorial-physical-routing', { path: routedTutorialPath, contentType: 'image/png' });

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('win'));
  await expect(page.locator('#result-restart')).toHaveText('Vào màn 2');
  await page.locator('#result-restart').click();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.stage)).toBe(2);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('ready');
  const stageTwo = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(stageTwo?.money).toBe(160);
  expect(stageTwo?.stageStartingMoney).toBe(160);
  expect(stageTwo?.waveCount).toBe(6);
  expect(stageTwo?.pathLength).toBeGreaterThanOrEqual(56);
  expect(stageTwo?.maxBoardLayer).toBe(1);
  expect(stageTwo?.maxStageEnemyLayer).toBe(1);
  expect(stageTwo?.unlockedTowers).toBe(5);
  expect(stageTwo?.waveThreats).toHaveLength(6);
  expect(stageTwo?.waveThreats.every((value, index, values) => index === 0 || value > values[index - 1])).toBe(true);
  await expect(page.locator('[data-tower-type="amplifier"]')).toBeDisabled();
  await expect(page.locator('[data-tower-type="lance"]')).toBeDisabled();
  await expect(page.locator('#result-overlay')).toBeHidden();
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('Vietnamese UI and the Level 2 kill reward bonus remain deterministic', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chrome', 'One deterministic desktop check covers shared localization and economy data.');
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));

  await expect(page.locator('html')).toHaveAttribute('lang', 'vi');
  await expect(page.locator('#build-dock')).toContainText('LẮP TRỤ');
  await expect(page.locator('#wave-panel')).toContainText('ĐỢT ĐỊCH SẮP TỚI');
  await expect(page.locator('#start-wave')).toContainText('Bắt đầu đợt');
  await expect(page.locator('[data-tower-type="foundry"]')).toHaveAttribute('aria-label', /Lò Đúc Đạn, giá 80/);

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('reward-stage-one'));
  const tutorialReward = await page.evaluate(() => ({
    money: window.__THREE_GAME_DIAGNOSTICS__?.money,
    multiplier: window.__THREE_GAME_DIAGNOSTICS__?.killRewardMultiplier,
  }));
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('reward-stage-two'));
  const levelTwoReward = await page.evaluate(() => ({
    money: window.__THREE_GAME_DIAGNOSTICS__?.money,
    multiplier: window.__THREE_GAME_DIAGNOSTICS__?.killRewardMultiplier,
  }));
  expect(tutorialReward).toEqual({ money: 11, multiplier: 1 });
  expect(levelTwoReward).toEqual({ money: 17, multiplier: 1.5 });
  expect(levelTwoReward.money).toBeGreaterThan(tutorialReward.money ?? 0);
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('ready waves preview exact enemy rosters and reveal inline enemy details', async ({ page }, testInfo) => {
  test.setTimeout(60_000);
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));

  await expect(page.locator('#wave-intel')).toBeVisible();
  await expect(page.locator('#wave-enemy-detail')).toBeHidden();
  const topBarBox = await page.locator('#top-bar').boundingBox();
  const wavePanelBox = await page.locator('#wave-panel').boundingBox();
  const initialViewport = page.viewportSize();
  expect(topBarBox).not.toBeNull();
  expect(wavePanelBox).not.toBeNull();
  expect(initialViewport).not.toBeNull();
  if (topBarBox && wavePanelBox && initialViewport) {
    expect(wavePanelBox.y).toBeGreaterThanOrEqual(topBarBox.y + topBarBox.height - 1);
    expect(wavePanelBox.y).toBeLessThan(initialViewport.height * 0.36);
    expect(Math.abs(wavePanelBox.x + wavePanelBox.width / 2 - initialViewport.width / 2)).toBeLessThan(90);
  }
  await expect(page.locator('[data-enemy-kind="riftling"]')).toContainText('×5');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.upcomingEnemyCount)).toBe(5);
  expect(await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.upcomingEnemyKinds)).toEqual(['riftling']);

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('tutorial-ready'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.wave)).toBe(3);
  await expect(page.locator('[data-enemy-kind="riftling"]')).toContainText('×5');
  await expect(page.locator('[data-enemy-kind="runner"]')).toContainText('×2');
  if (!testInfo.project.name.includes('mobile')) {
    await page.locator('[data-enemy-kind="runner"]').hover();
    await expect(page.locator('#wave-enemy-detail')).toBeVisible();
    await page.mouse.move(10, 400);
    await expect(page.locator('#wave-enemy-detail')).toBeHidden();
  }
  await page.locator('[data-enemy-kind="runner"]').click();
  const runnerDetail = page.locator('#wave-enemy-detail');
  await expect(runnerDetail).toBeVisible();
  await expect(runnerDetail).toContainText('Kẻ Chạy Arcana');
  await expect(runnerDetail.locator('.enemy-detail-stats dd')).toContainText(['72', '3.35', '−1', '+15']);
  await expect(runnerDetail.locator('[data-tone="weak"]')).toContainText('Băng');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.selectedWaveEnemyKind)).toBe('runner');
  const expandedWavePanelBox = await page.locator('#wave-panel').boundingBox();
  expect(expandedWavePanelBox).not.toBeNull();
  if (wavePanelBox && expandedWavePanelBox) expect(Math.abs(expandedWavePanelBox.height - wavePanelBox.height)).toBeLessThan(2);

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('stage-two-wave-four'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.wave)).toBe(4);
  await expect(page.locator('[data-enemy-kind="riftling"]')).toContainText('×14');
  await expect(page.locator('[data-enemy-kind="runner"]')).toContainText('×8');
  await expect(page.locator('[data-enemy-kind="wisp"]')).toContainText('×6');
  await page.locator('[data-enemy-kind="wisp"]').click();
  const wispDetail = page.locator('#wave-enemy-detail');
  await expect(wispDetail).toContainText('Linh Hỏa');
  await expect(wispDetail.locator('.enemy-detail-stats dd')).toContainText(['92', '2.25', '−2', '+30']);
  await expect(wispDetail.locator('[data-tone="immune"]')).toContainText('Lửa');
  await expect(wispDetail.locator('[data-tone="weak"]')).toContainText('Băng');

  const detailBox = await wispDetail.boundingBox();
  const viewport = page.viewportSize();
  expect(detailBox).not.toBeNull();
  expect(viewport).not.toBeNull();
  if (detailBox && viewport) {
    expect(detailBox.x).toBeGreaterThanOrEqual(0);
    expect(detailBox.y).toBeGreaterThanOrEqual(0);
    expect(detailBox.x + detailBox.width).toBeLessThanOrEqual(viewport.width + 1);
    expect(detailBox.y + detailBox.height).toBeLessThanOrEqual(viewport.height + 1);
  }
  const previewPath = testInfo.outputPath(`wave-intel-${testInfo.project.name}.png`);
  await page.screenshot({ fullPage: true, path: previewPath });
  await testInfo.attach('wave-intel-responsive-preview', { path: previewPath, contentType: 'image/png' });

  await wispDetail.locator('[data-close-wave-intel]').click();
  await expect(wispDetail).toBeHidden();
  await page.evaluate(() => {
    window.__THREE_GAME_TEST_HOOKS__?.setState('tutorial-wave');
    window.__THREE_GAME_TEST_HOOKS__?.setPausedForScreenshot(true);
  });
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('wave');
  await expect(page.locator('#wave-intel')).toBeHidden();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.upcomingEnemyCount)).toBe(0);
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('Level 2 visually introduces Amplifier before Wave 3 and Lance before Wave 4', async ({ page }, testInfo) => {
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('stage-two-wave-three'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.requiredTutorialTower)).toBe('amplifier');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHandMode)).toBe('drag');
  await expect(page.locator('[data-tower-type="amplifier"]')).toHaveClass(/tutorial-focus/);
  await expect(page.locator('[data-tower-type="amplifier"]')).toBeEnabled();
  await expect(page.locator('[data-tower-type="lance"]')).toBeDisabled();
  await expect(page.locator('#start-wave')).toBeDisabled();
  const amplifierLesson = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(amplifierLesson?.wave).toBe(3);
  expect(amplifierLesson?.unlockedTowers).toBe(6);
  expect(amplifierLesson?.lessonCell).not.toBeNull();
  const amplifierLessonPath = testInfo.outputPath('level-2-wave-3-amplifier-cue.png');
  await page.screenshot({ fullPage: true, path: amplifierLessonPath });
  await testInfo.attach('level-2-wave-3-amplifier-cue', { path: amplifierLessonPath, contentType: 'image/png' });

  if (amplifierLesson?.lessonCell) {
    const target = await page.evaluate(
      ({ gx, gz }) => window.__THREE_GAME_TEST_HOOKS__?.getCellClientPoint(gx, gz),
      amplifierLesson.lessonCell,
    );
    expect(target).not.toBeNull();
    await page.locator('[data-tower-type="amplifier"]').click();
    if (target) await page.mouse.click(target.x, target.y);
  }
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.requiredTutorialTower)).toBeNull();
  await expect(page.locator('#start-wave')).toBeEnabled();
  await expect(page.locator('#start-wave')).toHaveClass(/tutorial-focus/);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHandMode)).toBe('tap');

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('stage-two-wave-four'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.requiredTutorialTower)).toBe('lance');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHandMode)).toBe('drag');
  await expect(page.locator('[data-tower-type="lance"]')).toHaveClass(/tutorial-focus/);
  await expect(page.locator('[data-tower-type="lance"]')).toBeEnabled();
  await expect(page.locator('#start-wave')).toBeDisabled();
  const lanceLesson = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(lanceLesson?.wave).toBe(4);
  expect(lanceLesson?.unlockedTowers).toBe(7);
  expect(lanceLesson?.lessonCell).not.toBeNull();
  const lanceLessonPath = testInfo.outputPath('level-2-wave-4-lance-cue.png');
  await page.screenshot({ fullPage: true, path: lanceLessonPath });
  await testInfo.attach('level-2-wave-4-lance-cue', { path: lanceLessonPath, contentType: 'image/png' });

  if (lanceLesson?.lessonCell) {
    const target = await page.evaluate(
      ({ gx, gz }) => window.__THREE_GAME_TEST_HOOKS__?.getCellClientPoint(gx, gz),
      lanceLesson.lessonCell,
    );
    expect(target).not.toBeNull();
    await page.locator('[data-tower-type="lance"]').click();
    if (target) await page.mouse.click(target.x, target.y);
  }
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.requiredTutorialTower)).toBeNull();
  await expect(page.locator('#start-wave')).toBeEnabled();
  await expect(page.locator('#start-wave')).toHaveClass(/tutorial-focus/);
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('rotate buttons use press-and-hold motion and tutorial stops at the winning angle', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chrome', 'One deterministic desktop rotation run covers continuous control logic.');
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('tutorial-rotation'));

  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialStep)).toBe(8);
  await expect(page.locator('#action-aim')).toHaveCount(0);
  await expect(page.locator('#action-left')).toBeVisible();
  await expect(page.locator('#action-right')).toBeVisible();
  await expect(page.locator('#action-left')).toHaveClass(/tutorial-focus/);
  await expect(page.locator('#tutorial-card')).toHaveCount(0);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHandMode)).toBe('tap');
  await expect(page.locator('#action-link')).toHaveCount(0);

  await holdTutorialRotation(page, 'left', 9, 31);

  const diagnostics = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(diagnostics?.selectedOutputAngle).toBeCloseTo(TUTORIAL_ICE_HEAD_ON_ANGLE, 3);
  expect(diagnostics?.tutorialHeadOnDot).toBeLessThan(-0.99);
  expect(diagnostics?.connections).toBe(2);
  expect(diagnostics?.linkGuideObjects).toBe(4);
  expect(diagnostics?.weaponAimGuideObjects).toBe(1);
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('authored tutorial introduces Foundry, Fire and Ice across three winning waves', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name !== 'desktop-chrome', 'One deterministic desktop run proves the complete Stage 1 lesson flow.');
  test.setTimeout(100_000);
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await page.evaluate(() => {
    window.__THREE_GAME_TEST_HOOKS__?.seed(20260814);
    window.__THREE_GAME_TEST_HOOKS__?.setSpeed(1);
  });

  await placeTutorialTower(page, 'foundry');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialStep)).toBe(1);
  await expect(page.locator('#action-link')).toHaveCount(0);
  expect(await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHeadOnDot ?? 1)).toBeLessThan(-0.99);
  await page.locator('#start-wave').click();
  await page.waitForFunction(() => {
    const diagnostics = window.__THREE_GAME_DIAGNOSTICS__;
    return diagnostics?.stage === 1 && diagnostics.phase === 'ready' && diagnostics.wave === 2;
  }, undefined, { timeout: 25_000 });
  const livesAfterFirstWave = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.lives ?? 0);
  expect(livesAfterFirstWave).toBeGreaterThan(0);
  expect(await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialDirectShots ?? 0)).toBeGreaterThan(0);
  expect(await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.projectileInterceptions ?? -1)).toBe(0);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialStep)).toBe(2);
  await expect(page.locator('[data-tower-type="fire"]')).toHaveClass(/tutorial-focus/);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHandMode)).toBe('drag');

  await placeTutorialTower(page, 'fire');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialStep)).toBe(3);
  await expect(page.locator('#start-wave')).toHaveClass(/tutorial-focus/);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHandMode)).toBe('tap');
  await page.locator('#start-wave').click();
  await page.waitForFunction(() => {
    const diagnostics = window.__THREE_GAME_DIAGNOSTICS__;
    return diagnostics?.phase === 'paused' && diagnostics.tutorialStep === 4
      && diagnostics.enemies > 0 && diagnostics.projectiles > 0;
  }, undefined, { timeout: 20_000 });
  await expect(page.locator('#tutorial-card')).toHaveCount(0);
  const liveFireLessonPath = testInfo.outputPath('text-free-live-fire-rotation-cue.png');
  await page.screenshot({ fullPage: true, path: liveFireLessonPath });
  await testInfo.attach('text-free-live-fire-rotation-cue', { path: liveFireLessonPath, contentType: 'image/png' });
  await clickTutorialCell(page, 'foundry');
  await holdTutorialRotation(page, 'right', 5, 41);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.connections)).toBe(1);
  await clickTutorialCell(page, 'fire');
  await holdTutorialRotationUntilAngle(page, 'right', TUTORIAL_FIRE_HEAD_ON_ANGLE, 42);
  expect(await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHeadOnDot ?? 1)).toBeLessThan(-0.99);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialStep)).toBe(5);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('wave');
  await page.waitForFunction(() => {
    const diagnostics = window.__THREE_GAME_DIAGNOSTICS__;
    return diagnostics?.stage === 1 && diagnostics.phase === 'ready' && diagnostics.wave === 3;
  }, undefined, { timeout: 25_000 });
  const livesAfterFireWave = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.lives ?? 0);
  expect(livesAfterFireWave).toBeGreaterThan(0);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialStep)).toBe(6);

  await placeTutorialTower(page, 'ice');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialStep)).toBe(7);
  await clickTutorialCell(page, 'fire');
  await holdTutorialRotation(page, 'left', 8, 51);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialStep)).toBe(8);
  await clickTutorialCell(page, 'ice');
  await holdTutorialRotation(page, 'left', 9, 52);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.connections)).toBe(2);
  expect(await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHeadOnDot ?? 1)).toBeLessThan(-0.99);

  await page.locator('#start-wave').click();
  await page.waitForFunction(() => {
    const phase = window.__THREE_GAME_DIAGNOSTICS__?.phase;
    return phase === 'won' || phase === 'lost';
  }, undefined, { timeout: 30_000 });
  const result = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  await testInfo.attach('tutorial-solution-report', {
    body: JSON.stringify({ livesAfterFirstWave, livesAfterFireWave, result }, null, 2),
    contentType: 'application/json',
  });
  expect(result?.phase).toBe('won');
  expect(result?.lives).toBeGreaterThan(0);
  expect(result?.infusions).toBeGreaterThan(0);
  expect(result?.projectileInterceptions).toBeGreaterThan(0);
  expect(result?.reactions).toBeGreaterThan(0);
  expect(result?.tutorialStep).toBe(11);
  expect(result?.waveCount).toBe(3);
  expect(result?.weaponAimGuideObjects).toBe(1);
  await expect(page.locator('#result-title')).toContainText('Hoàn thành mạch hướng dẫn');
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('tower cards drag onto the logical grid with mouse and touch pointer events', async ({ page }, testInfo) => {
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await expect(page.locator('#briefing')).toHaveCount(0);
  await expect(page.locator('#tutorial-card')).toHaveCount(0);

  const target = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.getCellClientPoint(8, 3));
  const button = page.locator('[data-tower-type="foundry"]');
  const buttonBox = await button.boundingBox();
  expect(target).not.toBeNull();
  expect(buttonBox).not.toBeNull();
  if (!target || !buttonBox) return;
  const start = { x: buttonBox.x + buttonBox.width / 2, y: buttonBox.y + buttonBox.height / 2 };
  const startingMoney = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.money);
  await page.waitForTimeout(420);
  const tutorialScreenshotPath = testInfo.outputPath('tutorial-drag-guidance.png');
  await page.screenshot({ fullPage: true, path: tutorialScreenshotPath });
  await testInfo.attach('tutorial-drag-guidance', { path: tutorialScreenshotPath, contentType: 'image/png' });

  if (!testInfo.project.name.includes('mobile')) {
    const wrongTarget = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.getCellClientPoint(6, 0));
    expect(wrongTarget).not.toBeNull();
    if (wrongTarget) {
      await button.click();
      await page.mouse.click(wrongTarget.x, wrongTarget.y);
      await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.towers)).toBe(0);
      await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.money)).toBe(startingMoney);
      await expect(page.locator('#toast')).toBeHidden();
    }

    await button.dispatchEvent('pointerdown', {
      pointerId: 16, pointerType: 'mouse', isPrimary: true, button: 0,
      clientX: start.x, clientY: start.y, bubbles: true, cancelable: true,
    });
    await page.evaluate(() => {
      window.dispatchEvent(new PointerEvent('pointermove', {
        pointerId: 16, pointerType: 'mouse', isPrimary: true, button: 0,
        clientX: -40, clientY: -40, bubbles: true, cancelable: true,
      }));
      window.dispatchEvent(new PointerEvent('pointerup', {
        pointerId: 16, pointerType: 'mouse', isPrimary: true, button: 0,
        clientX: -40, clientY: -40, bubbles: true, cancelable: true,
      }));
    });
    await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.towers)).toBe(0);
    await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.money)).toBe(startingMoney);
  }

  if (testInfo.project.name.includes('mobile')) {
    await button.dispatchEvent('pointerdown', {
      pointerId: 17, pointerType: 'touch', isPrimary: true, button: 0,
      clientX: start.x, clientY: start.y, bubbles: true, cancelable: true,
    });
    await page.evaluate((point) => {
      window.dispatchEvent(new PointerEvent('pointermove', {
        pointerId: 17, pointerType: 'touch', isPrimary: true, button: 0,
        clientX: point.x, clientY: point.y, bubbles: true, cancelable: true,
      }));
    }, target);
    await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.draggingTower)).toBe(true);
    await page.evaluate((point) => {
      window.dispatchEvent(new PointerEvent('pointerup', {
        pointerId: 17, pointerType: 'touch', isPrimary: true, button: 0,
        clientX: point.x, clientY: point.y, bubbles: true, cancelable: true,
      }));
    }, target);
  } else {
    await page.mouse.move(start.x, start.y);
    await page.mouse.down();
    await page.mouse.move(target.x, target.y, { steps: 8 });
    await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.draggingTower)).toBe(true);
    await page.mouse.up();
  }

  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.towers)).toBe(1);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialStep)).toBe(1);
  await expect(page.locator('[data-tower-type="fire"]')).toBeDisabled();
  await expect(page.locator('#start-wave')).toHaveClass(/tutorial-focus/);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHandMode)).toBe('tap');
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('enemy elemental tint and icons persist, then Fire to Ice triggers a reaction', async ({ page }, testInfo) => {
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('status-fire'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tintedEnemies)).toBe(1);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.statusIcons)).toBe(1);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.elementalStatuses)).toBe(1);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.elementalTintStrength ?? 0)).toBeGreaterThanOrEqual(0.9);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.impactParticles ?? 0)).toBeGreaterThan(0);
  if (!testInfo.project.name.includes('mobile')) {
    await page.waitForTimeout(180);
    const fireStatusPath = testInfo.outputPath('strong-fire-status-hue.png');
    await page.screenshot({ fullPage: true, path: fireStatusPath });
    await testInfo.attach('strong-fire-status-hue', { path: fireStatusPath, contentType: 'image/png' });
  }

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('status-reaction'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.reactions)).toBe(1);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tintedEnemies)).toBe(1);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.statusIcons)).toBe(1);
  if (!testInfo.project.name.includes('mobile')) {
    await page.waitForTimeout(220);
    const screenshotPath = testInfo.outputPath('fire-to-ice-reaction-feedback.png');
    await page.screenshot({ fullPage: true, path: screenshotPath });
    await testInfo.attach('fire-to-ice-reaction-feedback', {
      path: screenshotPath,
      contentType: 'image/png',
    });
  }
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('pause, resume, fail and restart remain reachable', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name.includes('mobile'), 'Desktop covers the state flow; mobile layout has its own assertions.');
  const errors = await watchErrors(page);
  await openActiveNetwork(page);

  await page.locator('#pause-button').click();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('paused');
  await page.locator('#pause-button').click();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('wave');

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('fail'));
  await expect(page.locator('#result-overlay')).toBeVisible();
  await expect(page.locator('#result-title')).toContainText('Khe nứt đã xuyên thủng');
  await page.locator('#result-restart').click();
  await expect(page.locator('#briefing')).toHaveCount(0);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('ready');

  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('mobile HUD respects safe layout and touch target size', async ({ page }, testInfo) => {
  test.skip(!testInfo.project.name.includes('mobile'), 'Mobile-only layout assertion.');
  const errors = await watchErrors(page);
  await page.goto('/');
  await expect(page.locator('#briefing')).toHaveCount(0);
  await expect(page.locator('#help-button')).toHaveCount(0);
  await expect(page.locator('#tutorial-card')).toHaveCount(0);
  await expect(page.locator('#tutorial-hand')).toBeVisible();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('ready');
  await expect(page.locator('#build-dock')).toBeVisible();
  const buttons = await page.locator('#build-list .build-button').all();
  expect(buttons).toHaveLength(7);
  for (const button of buttons) {
    const box = await button.boundingBox();
    expect(box?.height).toBeGreaterThanOrEqual(44);
  }
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('tutorial-rotation'));
  await expect(page.locator('#action-left')).toBeVisible();
  await expect(page.locator('#action-right')).toBeVisible();
  expect((await page.locator('#action-left').boundingBox())?.height).toBeGreaterThanOrEqual(44);
  expect((await page.locator('#action-right').boundingBox())?.height).toBeGreaterThanOrEqual(44);
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});
