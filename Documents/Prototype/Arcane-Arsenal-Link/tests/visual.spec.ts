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

async function tapTowerById(page: Page, towerId: number, pointerType: 'mouse' | 'touch'): Promise<void> {
  const point = await page.evaluate((id) => window.__THREE_GAME_TEST_HOOKS__?.getTowerClientPoint(id), towerId);
  expect(point).not.toBeNull();
  if (!point) return;
  if (pointerType === 'mouse') {
    await page.mouse.click(point.x, point.y);
    return;
  }
  await page.touchscreen.tap(point.x, point.y);
}

async function tapCell(page: Page, gx: number, gz: number, pointerType: 'mouse' | 'touch'): Promise<void> {
  const point = await page.evaluate(
    ({ x, z }) => window.__THREE_GAME_TEST_HOOKS__?.getCellClientPoint(x, z),
    { x: gx, z: gz },
  );
  expect(point).not.toBeNull();
  if (!point) return;
  if (pointerType === 'mouse') {
    await page.mouse.click(point.x, point.y);
    return;
  }
  await page.touchscreen.tap(point.x, point.y);
}

async function prepareTutorialLinkAction(page: Page, pointerType: 'mouse' | 'touch'): Promise<{ foundryId: number; fireId: number }> {
  await page.locator('[data-tower-type="foundry"]').click();
  await tapCell(page, 2, 1, pointerType);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialStep)).toBe(1);
  await page.locator('[data-tower-type="fire"]').click();
  await tapCell(page, 4, 5, pointerType);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialStep)).toBe(2);
  const ids = await page.evaluate(() => Object.keys(window.__THREE_GAME_DIAGNOSTICS__?.towerBuffers ?? {}).map(Number));
  expect(ids).toHaveLength(2);
  const [foundryId, fireId] = ids;
  if (pointerType === 'touch') {
    // Entering then cancelling build mode clears the portrait inspector so the
    // source tower is actually exposed to a real viewport tap.
    await page.locator('[data-tower-type="foundry"]').tap();
    await page.keyboard.press('Escape');
    await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.selectedTowerId)).toBeNull();
  }
  await tapTowerById(page, foundryId, pointerType);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialStep)).toBe(3);
  return { foundryId, fireId };
}

async function dragSelectedTowerTo(
  page: Page,
  sourceTowerId: number,
  targetTowerId: number,
  pointerType: 'mouse' | 'touch',
  whileDragging?: () => Promise<void>,
): Promise<void> {
  const points = await page.evaluate(({ sourceId, targetId }) => ({
    source: window.__THREE_GAME_TEST_HOOKS__?.getTowerClientPoint(sourceId) ?? null,
    target: window.__THREE_GAME_TEST_HOOKS__?.getTowerClientPoint(targetId) ?? null,
  }), { sourceId: sourceTowerId, targetId: targetTowerId });
  expect(points.source).not.toBeNull();
  expect(points.target).not.toBeNull();
  if (!points.source || !points.target) return;
  const midpoint = {
    x: points.source.x + (points.target.x - points.source.x) * 0.35,
    y: points.source.y + (points.target.y - points.source.y) * 0.35,
  };
  if (pointerType === 'mouse') {
    await page.mouse.move(points.source.x, points.source.y);
    await page.mouse.down();
    await page.mouse.move(midpoint.x, midpoint.y, { steps: 4 });
  } else {
    await page.locator('#game-canvas').dispatchEvent('pointerdown', {
      pointerId: 31, pointerType: 'touch', isPrimary: true, button: 0,
      clientX: points.source.x, clientY: points.source.y,
    });
    await page.locator('#game-canvas').dispatchEvent('pointermove', {
      pointerId: 31, pointerType: 'touch', isPrimary: true, button: 0,
      clientX: midpoint.x, clientY: midpoint.y,
    });
  }
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.interactionMode)).toBe('link');
  if (whileDragging) await whileDragging();
  if (pointerType === 'mouse') {
    await page.mouse.move(points.target.x, points.target.y, { steps: 4 });
    await page.mouse.up();
  } else {
    await page.locator('#game-canvas').dispatchEvent('pointermove', {
      pointerId: 31, pointerType: 'touch', isPrimary: true, button: 0,
      clientX: points.target.x, clientY: points.target.y,
    });
    await page.locator('#game-canvas').dispatchEvent('pointerup', {
      pointerId: 31, pointerType: 'touch', isPrimary: true, button: 0,
      clientX: points.target.x, clientY: points.target.y,
    });
  }
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
  expect(diagnostics?.layerOneTowerCount).toBe(5);
  expect(diagnostics?.oppositeRaisedCellCount).toBe(4);
  expect(diagnostics?.oppositeRaisedTowerCount).toBe(2);
  expect(diagnostics?.maxLayerOneTowerLaneDistance).toBeLessThanOrEqual(5.01);
  expect(diagnostics?.maxBoardLayer).toBe(1);
  expect(diagnostics?.maxStageEnemyLayer).toBe(1);
  expect(diagnostics?.pathLength).toBeGreaterThanOrEqual(56);
  expect(diagnostics?.waveCount).toBe(6);
  expect(diagnostics?.maxEnemyFacingError).toBeLessThan(0.02);
  expect(diagnostics?.maxEnemyLaneOffset).toBeLessThanOrEqual(0.551);
  expect(diagnostics?.projectileSpeedMultiplier).toBe(3);
  expect(diagnostics?.towerFireRateMultiplier).toBe(1.5);
  expect(diagnostics?.projectileCollisionRadius).toBe(0.84);
  expect(diagnostics?.projectileVisualScale).toBe(2);
  expect(diagnostics?.enemySpeedMultiplier).toBe(0.6);
  expect(diagnostics?.connections).toBeGreaterThanOrEqual(3);
  expect(diagnostics?.linkGuideObjects).toBeGreaterThanOrEqual(6);
  expect(diagnostics?.towerConnectionRanges).toEqual({
    1: 12.6,
    2: 12,
    3: 12,
    4: 0,
    5: 8.1,
    6: 12.6,
    7: 12.45,
    8: 12.6,
    9: 11.7,
    10: 12,
  });
  expect(diagnostics?.unlinkedProjectileLaunches).toBe(0);
  expect(diagnostics?.towerLinks.every((link) => link.distance <= link.range + 0.001)).toBe(true);
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
  expect(diagnostics?.layerOneTowerCount).toBe(5);
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
    return diagnostics?.stage === 1 && diagnostics.phase === 'ready' && diagnostics.tutorialStep === 15;
  });
  const diagnostics = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(diagnostics?.towers).toBe(4);
  expect(diagnostics?.connections).toBe(3);
  expect(diagnostics?.linkGuideObjects).toBe(6);
  expect(diagnostics?.maxTowerLayer).toBe(0);
  expect(diagnostics?.unlockedTowers).toBe(3);
  expect(diagnostics?.tutorialStep).toBe(15);
  expect(diagnostics?.tutorialObjective).toBe('start-wave-3');
  expect(diagnostics?.terminalBuffTowerIds).toHaveLength(1);
  expect(diagnostics?.pathLength).toBe(22);
  expect(diagnostics?.pathSegmentLengths).toEqual([8, 4, 10]);
  expect(diagnostics?.pathGridAlignmentError).toBe(0);
  expect(diagnostics?.pathAxisAlignmentError).toBe(0);
  expect(diagnostics?.maxHorizontalPathSegment).toBe(10);
  expect(diagnostics?.maxVerticalPathSegment).toBe(4);
  expect(diagnostics?.waveCount).toBe(6);
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
  expect(stageTwo?.money).toBe(220);
  expect(stageTwo?.stageStartingMoney).toBe(220);
  expect(stageTwo?.waveCount).toBe(6);
  expect(stageTwo?.pathLength).toBeGreaterThanOrEqual(56);
  expect(stageTwo?.maxBoardLayer).toBe(1);
  expect(stageTwo?.maxStageEnemyLayer).toBe(1);
  expect(stageTwo?.unlockedTowers).toBe(5);
  expect(stageTwo?.waveThreats).toHaveLength(6);
  expect(stageTwo?.waveThreats.every((value, index, values) => index === 0 || value > values[index - 1])).toBe(true);
  expect(stageTwo?.waveEnemyCounts).toEqual([10, 14, 24, 34, 46, 61]);
  expect(stageTwo?.waveMaxEnemyLayers.slice(0, 3)).toEqual([0, 0, 1]);
  expect(stageTwo?.waveEnemyCounts.every((value, index, values) => index === 0 || value > values[index - 1])).toBe(true);
  expect(stageTwo?.waveHealthMultipliers).toEqual([1.1, 1.4, 2, 3, 4.4, 6.2]);
  expect(stageTwo?.waveHealthMultipliers.every((value, index, values) => index === 0 || value > values[index - 1])).toBe(true);
  expect(stageTwo?.waveSpawnDensities.every((value, index, values) => index === 0 || value > values[index - 1])).toBe(true);
  expect(stageTwo?.waveFlyingEnemyCounts).toEqual([0, 0, 6, 8, 16, 12]);
  expect(stageTwo?.waveBarrierEnemyCounts).toEqual([0, 0, 0, 0, 0, 2]);
  expect(stageTwo?.waveResistantEnemyCounts).toEqual([0, 0, 6, 8, 28, 24]);
  expect(stageTwo?.spawnDirectionMarkerCount).toBe(1);
  expect(stageTwo?.spawnDirectionError).toBeLessThan(0.0001);
  expect(stageTwo?.spawnDirectionInView).toBe(true);
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
  await expect(page.locator('#wave-panel .wave-copy')).toHaveCount(0);
  await expect(page.locator('#wave-title')).toHaveCount(0);
  await expect(page.locator('#wave-hint')).toHaveCount(0);
  await expect(page.locator('#start-wave')).toContainText('Bắt đầu đợt');
  await expect(page.locator('[data-tower-type="foundry"]')).toHaveAttribute('aria-label', /Lò Đúc Đạn, giá 80/);

  const tutorialCurve = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(tutorialCurve?.waveEnemyCounts).toEqual([4, 6, 7, 12, 16, 21]);
  expect(tutorialCurve?.waveHealthMultipliers).toEqual([1, 1.08, 1.18, 6, 10, 14]);
  expect(tutorialCurve?.spawnDirectionMarkerCount).toBe(1);
  expect(tutorialCurve?.spawnDirectionError).toBeLessThan(0.0001);
  expect(tutorialCurve?.spawnDirectionInView).toBe(true);

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('reward-stage-one'));
  const tutorialReward = await page.evaluate(() => ({
    money: window.__THREE_GAME_DIAGNOSTICS__?.money,
    multiplier: window.__THREE_GAME_DIAGNOSTICS__?.killRewardMultiplier,
    rewardRate: window.__THREE_GAME_DIAGNOSTICS__?.enemyRewardMultiplier,
    clearRate: window.__THREE_GAME_DIAGNOSTICS__?.waveClearRewardMultiplier,
  }));
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('reward-stage-two'));
  const levelTwoReward = await page.evaluate(() => ({
    money: window.__THREE_GAME_DIAGNOSTICS__?.money,
    multiplier: window.__THREE_GAME_DIAGNOSTICS__?.killRewardMultiplier,
    rewardRate: window.__THREE_GAME_DIAGNOSTICS__?.enemyRewardMultiplier,
    clearRate: window.__THREE_GAME_DIAGNOSTICS__?.waveClearRewardMultiplier,
  }));
  expect(tutorialReward).toEqual({ money: 7, multiplier: 1, rewardRate: 0.6, clearRate: 0.65 });
  expect(levelTwoReward).toEqual({ money: 10, multiplier: 1.5, rewardRate: 0.6, clearRate: 0.65 });
  expect(levelTwoReward.money).toBeGreaterThan(tutorialReward.money ?? 0);
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('Nổ radial blast stays anchored, respects one-cell same-layer damage, and fades cleanly', async ({ page }, testInfo) => {
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('explosion-skill'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.lanceVfxCount)).toBe(1);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.explosionVisualRadius)).toBeGreaterThan(1.8);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setPausedForScreenshot(true));
  const explosionPreview = testInfo.outputPath(`no-arcana-explosion-${testInfo.project.name}.png`);
  await page.screenshot({ fullPage: true, path: explosionPreview });
  await testInfo.attach('no-arcana-explosion', { path: explosionPreview, contentType: 'image/png' });
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.lanceVfxAnchorError)).toBeLessThan(0.0001);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.lanceVfxScaleError)).toBeLessThan(0.0001);
  const blast = await page.evaluate(() => ({
    radius: window.__THREE_GAME_DIAGNOSTICS__?.explosionRadius,
    hits: window.__THREE_GAME_DIAGNOSTICS__?.explosionHits,
    damage: window.__THREE_GAME_DIAGNOSTICS__?.explosionDamage,
    outsideDamage: window.__THREE_GAME_DIAGNOSTICS__?.explosionOutsideDamage,
    otherLayerDamage: window.__THREE_GAME_DIAGNOSTICS__?.explosionOtherLayerDamage,
    visualRadius: window.__THREE_GAME_DIAGNOSTICS__?.explosionVisualRadius,
    rings: window.__THREE_GAME_DIAGNOSTICS__?.explosionRingCount,
    shards: window.__THREE_GAME_DIAGNOSTICS__?.explosionShardCount,
    targetCues: window.__THREE_GAME_DIAGNOSTICS__?.explosionTargetCueCount,
  }));
  expect(blast.radius).toBe(2);
  expect(blast.hits).toBe(1);
  expect(blast.damage).toBeGreaterThan(0);
  expect(blast.outsideDamage).toBe(0);
  expect(blast.otherLayerDamage).toBe(0);
  expect(blast.visualRadius).toBeGreaterThan(1.8);
  expect(blast.visualRadius).toBeLessThanOrEqual(2);
  expect(blast.rings).toBe(4);
  expect(blast.shards).toBe(18);
  expect(blast.targetCues).toBe(1);

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setPausedForScreenshot(false));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.lanceVfxCount), { timeout: 1_500 }).toBe(0);
  const completedBlast = await page.evaluate(() => ({
    anchorError: window.__THREE_GAME_DIAGNOSTICS__?.lanceVfxAnchorError,
    scaleError: window.__THREE_GAME_DIAGNOSTICS__?.lanceVfxScaleError,
  }));
  expect(completedBlast).toEqual({ anchorError: 0, scaleError: 0 });
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('Level 3 expands the battlefield, spans 10 waves, and previews new elemental enemies', async ({ page }, testInfo) => {
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('stage-two-ready'));
  const stageTwo = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('win'));
  await expect(page.locator('#result-restart')).toHaveText('Vào màn 3');
  await page.locator('#result-restart').click();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.stage)).toBe(3);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('ready');

  const stageThree = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(stageThree?.money).toBe(220);
  expect(stageThree?.waveCount).toBe(10);
  expect(stageThree?.pathLength).toBeGreaterThan(stageTwo?.pathLength ?? 0);
  expect(stageThree?.boardWidth).toBeGreaterThan(stageTwo?.boardWidth ?? 0);
  expect(stageThree?.boardDepth).toBeGreaterThan(stageTwo?.boardDepth ?? 0);
  expect(stageThree?.buildableCellCount).toBeGreaterThan(stageTwo?.buildableCellCount ?? 0);
  expect(stageThree?.maxBoardLayer).toBe(1);
  expect(stageThree?.maxStageEnemyLayer).toBe(1);
  expect(stageThree?.unlockedTowers).toBe(7);
  expect(stageThree?.waveThreats).toHaveLength(10);
  expect(stageThree?.waveThreats.every((value, index, values) => index === 0 || value > values[index - 1])).toBe(true);
  expect(stageThree?.waveEnemyCounts).toEqual([20, 29, 46, 60, 76, 92, 108, 124, 136, 148]);
  expect(stageThree?.waveMaxEnemyLayers.slice(0, 3)).toEqual([0, 0, 1]);
  expect(stageThree?.waveEnemyCounts.every((value, index, values) => index === 0 || value > values[index - 1])).toBe(true);
  expect(stageThree?.waveHealthMultipliers).toEqual([1.3, 1.7, 2.4, 3.3, 4.5, 6, 7.8, 10, 12.5, 15.5]);
  expect(stageThree?.waveHealthMultipliers.every((value, index, values) => index === 0 || value > values[index - 1])).toBe(true);
  expect(stageThree?.waveSpawnDensities.every((value, index, values) => index === 0 || value > values[index - 1])).toBe(true);
  expect(stageThree?.waveFlyingEnemyCounts.slice(0, 2)).toEqual([0, 0]);
  expect(stageThree?.waveFlyingEnemyCounts.slice(2).every((count) => count > 0)).toBe(true);
  expect(stageThree?.waveBarrierEnemyCounts).toEqual([0, 0, 4, 16, 10, 28, 36, 40, 56, 54]);
  expect(stageThree?.waveResistantEnemyCounts.at(-1)).toBeGreaterThan(stageThree?.waveResistantEnemyCounts[0] ?? 0);
  expect(stageThree?.spawnDirectionMarkerCount).toBe(1);
  expect(stageThree?.spawnDirectionError).toBeLessThan(0.0001);
  expect(stageThree?.spawnDirectionInView).toBe(true);

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('stage-three-final'));
  await expect(page.locator('[data-enemy-kind="skyWarder"]')).toContainText('Hộ Vệ Thiên Lăng');
  await expect(page.locator('[data-enemy-kind="skyWarder"]')).toContainText('BAY · TẦNG 1');
  await expect(page.locator('[data-enemy-kind="colossus"]')).toContainText('Cự Tượng Khe Nứt');
  await expect(page.locator('[data-enemy-kind="arcaneBulwark"]')).toContainText('Vệ Binh Hợp Kim');
  await expect(page.locator('[data-enemy-kind="arcaneBulwark"]')).toContainText('×26');
  await page.locator('[data-enemy-kind="skyWarder"]').click();
  await expect(page.locator('#wave-enemy-detail')).toContainText('Phá bằng · Bão Cát');
  await expect(page.locator('#wave-enemy-detail')).toContainText('Tầng bay 1');
  await page.locator('[data-enemy-kind="colossus"]').click();
  await expect(page.locator('#wave-enemy-detail')).toContainText('Phá bằng · Vỡ Tinh Thể');
  await expect(page.locator('#wave-enemy-detail')).toContainText('MẶT ĐẤT');
  await page.locator('[data-enemy-kind="arcaneBulwark"]').click();
  await expect(page.locator('#wave-enemy-detail')).toContainText('Phá bằng · Sốc Nhiệt');
  await expect(page.locator('#wave-enemy-detail')).toContainText('Sau vỡ giáp · Tăng tốc ×1.85');
  await page.locator('[data-close-wave-intel]').click();
  await expect(page.locator('#wave-enemy-detail')).toBeHidden();

  const screenshot = testInfo.outputPath(`level-3-final-preview-${testInfo.project.name}.png`);
  await page.screenshot({ fullPage: true, path: screenshot });
  await testInfo.attach('level-3-final-preview', { path: screenshot, contentType: 'image/png' });
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('stage-three-final-active'));
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setSpeed(1));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('wave');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.activeEnemyKinds), { timeout: 12_000 })
    .toEqual(expect.arrayContaining(['arcaneBulwark', 'skyWarder', 'colossus']));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.maxEnemyLayer)).toBe(1);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.activeEnemyMaxHp), { timeout: 12_000 }).toBeGreaterThanOrEqual(6_272);
  await expect.poll(() => page.evaluate(() => {
    const diagnostics = window.__THREE_GAME_DIAGNOSTICS__;
    return diagnostics ? diagnostics.visibleDetailedEnemies === diagnostics.enemies : false;
  }), { timeout: 12_000 }).toBe(true);
  // Full-detail enemy models intentionally stay active at high density; this guards
  // against runaway scene growth without reintroducing the removed Crowd LOD.
  const renderLimits = { calls: 1_000, geometries: 1_000 };
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.renderer.calls)).toBeLessThan(renderLimits.calls);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.renderer.geometries)).toBeLessThan(renderLimits.geometries);
  const activeScreenshot = testInfo.outputPath(`level-3-final-active-${testInfo.project.name}.png`);
  await page.screenshot({ fullPage: true, path: activeScreenshot });
  await testInfo.attach('level-3-final-active', { path: activeScreenshot, contentType: 'image/png' });
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('tower eye controls reveal details without selecting or purchasing a tower', async ({ page }) => {
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  const startingMoney = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.money);

  await expect(page.locator('[data-tower-info]')).toHaveCount(7);
  await page.locator('[data-tower-info="fire"]').click();
  await expect(page.locator('#tower-inspector')).toBeVisible();
  await expect(page.locator('#tower-inspector')).toHaveClass(/catalog-view/);
  await expect(page.locator('#inspector-name')).toHaveText('Trụ Truyền Hỏa');
  await expect(page.locator('#inspector-role')).toContainText('Chưa mở');
  await expect(page.locator('#tower-detail-stats dd')).toHaveText(['70', '1×1', '12.0']);
  await expect(page.locator('#ammo-magazine')).toBeHidden();
  await expect(page.locator('#inspector-detail')).toContainText('Nâng cấp:');
  await expect(page.locator('.build-button.selected')).toHaveCount(0);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.money)).toBe(startingMoney);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.towers)).toBe(0);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.inspectedBuildType)).toBe('fire');

  await page.locator('[data-tower-info="lance"]').click();
  await expect(page.locator('#inspector-name')).toHaveText('Nổ Arcana');
  await expect(page.locator('#tower-detail-stats dd')).toHaveText(['180', '2×1', '2.0', '8 ô']);
  await expect(page.locator('#ammo-magazine')).toBeHidden();

  await page.locator('#inspector-close-detail').click();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.inspectedBuildType)).toBeNull();
  await expect(page.locator('#tower-inspector')).toBeHidden();
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('only a placed Nổ exposes the ammo magazine', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name.includes('mobile'), 'Desktop world taps cover the shared inspector behavior deterministically.');
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('active-play'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.towers)).toBe(10);

  await tapTowerById(page, 2, 'mouse');
  await expect(page.locator('#inspector-name')).toContainText('Trụ Truyền Hỏa');
  await expect(page.locator('#ammo-magazine')).toBeHidden();
  await expect(page.locator('#inspector-detail')).not.toContainText('Đạn tích:');

  await tapTowerById(page, 4, 'mouse');
  await expect(page.locator('#inspector-name')).toContainText('Nổ Arcana');
  await expect(page.locator('#ammo-magazine')).toBeVisible();
  await expect(page.locator('#inspector-detail')).toContainText('Đạn tích:');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.finiteAmmoTowerIds)).toEqual([4]);
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('non-special towers keep firing beyond the removed ammo capacity', async ({ page }, testInfo) => {
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('terminal-flow'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('wave');
  const link = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.towerLinks[0] ?? null);
  expect(link).not.toBeNull();
  if (!link) return;

  await expect.poll(
    () => page.evaluate((sourceId) => window.__THREE_GAME_DIAGNOSTICS__?.projectileLaunchesByTower[sourceId] ?? 0, link.sourceId),
    { timeout: 20_000 },
  ).toBeGreaterThan(8);
  const diagnostics = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(diagnostics?.towerBuffers[link.targetId]).toBe(0);
  expect(diagnostics?.finiteAmmoTowerIds).toEqual([]);
  expect(diagnostics?.capacityBlockedTowerIds).toEqual([]);
  expect(diagnostics?.unlinkedProjectileLaunches).toBe(0);
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
  if (!testInfo.project.name.includes('mobile')) {
    const screenshot = testInfo.outputPath('continuous-terminal-flow.png');
    await page.screenshot({ fullPage: true, path: screenshot });
    await testInfo.attach('continuous-terminal-flow', { path: screenshot, contentType: 'image/png' });
  }
});

test('ready waves preview exact enemy rosters and reveal inline enemy details', async ({ page }, testInfo) => {
  test.setTimeout(120_000);
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
  await expect(page.locator('[data-enemy-kind="riftling"]')).toContainText('×4');
  await expect(page.locator('[data-enemy-kind="riftling"]')).toContainText('MẶT ĐẤT');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.upcomingEnemyCount)).toBe(4);
  expect(await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.upcomingEnemyKinds)).toEqual(['riftling']);

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('tutorial-ready'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.wave)).toBe(3);
  await expect(page.locator('[data-enemy-kind="riftling"]')).toContainText('×5');
  await expect(page.locator('[data-enemy-kind="runner"]')).toContainText('×2');
  if (!testInfo.project.name.includes('mobile')) {
    await page.locator('[data-enemy-kind="runner"]').hover();
    await expect(page.locator('#wave-enemy-detail')).toBeVisible();
    await page.locator('#pause-button').hover();
    await expect(page.locator('#wave-enemy-detail')).toBeHidden();
  }
  await page.locator('[data-enemy-kind="runner"]').click();
  const runnerDetail = page.locator('#wave-enemy-detail');
  await expect(runnerDetail).toBeVisible();
  await expect(runnerDetail).toContainText('Kẻ Chạy Arcana');
  await expect(runnerDetail.locator('.enemy-detail-stats dd')).toContainText(['85', '2.01', '−1', '+9']);
  await expect(runnerDetail.locator('[data-tone="weak"]')).toContainText('Băng');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.selectedWaveEnemyKind)).toBe('runner');
  const expandedWavePanelBox = await page.locator('#wave-panel').boundingBox();
  expect(expandedWavePanelBox).not.toBeNull();
  if (wavePanelBox && expandedWavePanelBox) expect(Math.abs(expandedWavePanelBox.height - wavePanelBox.height)).toBeLessThan(2);

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('stage-two-wave-four'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.wave)).toBe(4);
  await expect(page.locator('[data-enemy-kind="riftling"]')).toContainText('×16');
  await expect(page.locator('[data-enemy-kind="runner"]')).toContainText('×10');
  await expect(page.locator('[data-enemy-kind="wisp"]')).toContainText('×8');
  await expect(page.locator('[data-enemy-kind="wisp"]')).toContainText('BAY · TẦNG 1');
  await page.locator('[data-enemy-kind="wisp"]').click();
  const wispDetail = page.locator('#wave-enemy-detail');
  await expect(wispDetail).toContainText('Linh Hỏa');
  await expect(wispDetail).toContainText('BAY TRÊN KHÔNG');
  await expect(wispDetail).toContainText('Tầng bay 1');
  await expect(wispDetail.locator('.enemy-detail-stats dd')).toContainText(['276', '1.35', '−2', '+18']);
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

  await wispDetail.locator('[data-close-wave-intel]').click({ force: true });
  await expect(wispDetail).toBeHidden();
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('tutorial-wave'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('wave');
  await expect(page.locator('#wave-intel')).toBeHidden();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.upcomingEnemyCount)).toBe(0);
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('Level 2 introduces Amplifier, then Nổ beside the lane with a dedicated Foundry feeder and world ammo bar', async ({ page }, testInfo) => {
  const errors = await watchErrors(page);
  const pointerType = testInfo.project.name.includes('mobile') ? 'touch' : 'mouse';
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('stage-two-wave-three'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.requiredTutorialTower)).toBe('amplifier');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHandMode)).toBe('drag');
  await expect(page.locator('[data-tower-type="amplifier"]')).toHaveClass(/tutorial-focus/);
  await expect(page.locator('[data-tower-type="amplifier"]')).toBeEnabled();
  await expect(page.locator('[data-tower-type="amplifier"]')).toHaveAttribute('data-lesson-free', 'true');
  await expect(page.locator('[data-tower-type="amplifier"] .build-copy b')).toHaveText('0');
  await expect(page.locator('[data-tower-type="lance"]')).toBeDisabled();
  await expect(page.locator('#start-wave')).toBeDisabled();
  const amplifierLesson = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(amplifierLesson?.wave).toBe(3);
  expect(amplifierLesson?.money).toBe(35);
  expect(amplifierLesson?.unlockedTowers).toBe(6);
  expect(amplifierLesson?.lessonCell).not.toBeNull();
  const amplifierLessonPath = testInfo.outputPath('level-2-wave-3-amplifier-cue.png');
  await page.screenshot({ fullPage: true, path: amplifierLessonPath });
  await testInfo.attach('level-2-wave-3-amplifier-cue', { path: amplifierLessonPath, contentType: 'image/png' });

  if (amplifierLesson?.lessonCell) {
    await page.locator('[data-tower-type="amplifier"]').click();
    await tapCell(page, amplifierLesson.lessonCell.gx, amplifierLesson.lessonCell.gz, pointerType);
  }
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.requiredTutorialTower)).toBeNull();
  await expect(page.locator('#start-wave')).toBeEnabled();
  await expect(page.locator('#start-wave')).toHaveClass(/tutorial-focus/);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHandMode)).toBe('tap');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.money)).toBe(35);
  await expect(page.locator('#action-sell')).toHaveText('Bán 0');
  await page.evaluate(() => (document.querySelector('#action-sell') as HTMLButtonElement | null)?.click());
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.requiredTutorialTower)).toBe('amplifier');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.money)).toBe(35);
  await expect(page.locator('[data-tower-type="amplifier"]')).toHaveAttribute('data-lesson-free', 'true');
  if (amplifierLesson?.lessonCell) {
    await page.locator('[data-tower-type="amplifier"]').click();
    await tapCell(page, amplifierLesson.lessonCell.gx, amplifierLesson.lessonCell.gz, pointerType);
  }
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.requiredTutorialTower)).toBeNull();
  await expect(page.locator('[data-tower-type="fire"] .build-copy b')).toHaveText('70');
  await expect(page.locator('[data-tower-type="fire"]')).toHaveAttribute('data-lesson-free', 'false');
  await expect(page.locator('[data-tower-type="fire"]')).toBeDisabled();

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('stage-two-wave-four'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.requiredTutorialTower)).toBe('lance');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHandMode)).toBe('drag');
  await expect(page.locator('[data-tower-type="lance"]')).toHaveClass(/tutorial-focus/);
  await expect(page.locator('[data-tower-type="lance"]')).toBeEnabled();
  await expect(page.locator('[data-tower-type="lance"]')).toHaveAttribute('data-lesson-free', 'true');
  await expect(page.locator('[data-tower-type="lance"] .build-copy b')).toHaveText('0');
  await expect(page.locator('#start-wave')).toBeDisabled();
  const lanceLesson = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(lanceLesson?.wave).toBe(4);
  expect(lanceLesson?.money).toBe(45);
  expect(lanceLesson?.unlockedTowers).toBe(7);
  expect(lanceLesson?.lessonCell).not.toBeNull();
  expect(lanceLesson?.lessonCellLaneDistance).not.toBeNull();
  expect(lanceLesson?.lessonCellLaneDistance ?? Number.POSITIVE_INFINITY).toBeLessThanOrEqual(2);
  const lanceLessonPath = testInfo.outputPath('level-2-wave-4-lance-cue.png');
  await page.screenshot({ fullPage: true, path: lanceLessonPath });
  await testInfo.attach('level-2-wave-4-lance-cue', { path: lanceLessonPath, contentType: 'image/png' });

  if (lanceLesson?.lessonCell) {
    await page.locator('[data-tower-type="lance"]').click();
    await tapCell(page, lanceLesson.lessonCell.gx, lanceLesson.lessonCell.gz, pointerType);
  }
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.selectedTowerId ?? null)).not.toBeNull();
  const lanceTowerId = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.selectedTowerId ?? null);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.requiredTutorialTower)).toBe('foundry');
  await expect(page.locator('[data-tower-type="foundry"]')).toHaveClass(/tutorial-focus/);
  await expect(page.locator('[data-tower-type="foundry"]')).toBeEnabled();
  await expect(page.locator('[data-tower-type="foundry"]')).toHaveAttribute('data-lesson-free', 'true');
  await expect(page.locator('[data-tower-type="foundry"] .build-copy b')).toHaveText('0');
  await expect(page.locator('#start-wave')).toBeDisabled();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.money)).toBe(45);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.lanceAmmoBarCount)).toBe(1);
  const feederLesson = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(feederLesson?.lessonCell).not.toBeNull();
  if (feederLesson?.lessonCell) {
    await page.locator('[data-tower-type="foundry"]').click();
    await tapCell(page, feederLesson.lessonCell.gx, feederLesson.lessonCell.gz, pointerType);
  }
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.selectedTowerId ?? null)).not.toBeNull();
  const feederTowerId = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.selectedTowerId ?? null);
  if (feederTowerId !== null && lanceTowerId !== null) {
    await dragSelectedTowerTo(page, feederTowerId, lanceTowerId, pointerType);
  }
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.requiredTutorialTower)).toBeNull();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.money)).toBe(45);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.lanceFeederConnected)).toBe(true);
  await expect.poll(() => page.evaluate(({ sourceId, targetId }) => window.__THREE_GAME_DIAGNOSTICS__?.towerLinks.some(
    (link) => link.sourceId === sourceId && link.targetId === targetId,
  ) ?? false, { sourceId: feederTowerId, targetId: lanceTowerId })).toBe(true);
  const feederFacingError = await page.evaluate(({ sourceId, targetId }) => window.__THREE_GAME_DIAGNOSTICS__?.towerLinks.find(
    (link) => link.sourceId === sourceId && link.targetId === targetId,
  )?.facingError ?? Number.POSITIVE_INFINITY, { sourceId: feederTowerId, targetId: lanceTowerId });
  expect(feederFacingError).toBeLessThan(0.001);
  await expect(page.locator('#start-wave')).toBeEnabled();
  await expect(page.locator('#start-wave')).toHaveClass(/tutorial-focus/);
  await page.locator('#start-wave').click();
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.lanceAmmoRatio), { timeout: 10_000 }).toBeGreaterThan(0);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.overlappingEnemyPairs), { timeout: 10_000 }).toBeGreaterThan(0);
  const activeFeederPath = testInfo.outputPath(`level-2-wave-4-lance-feeder-${testInfo.project.name}.png`);
  await page.screenshot({ fullPage: true, path: activeFeederPath });
  await testInfo.attach('level-2-wave-4-lance-feeder-active', { path: activeFeederPath, contentType: 'image/png' });
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('a completed relay rejects another incoming link on desktop and mobile', async ({ page }, testInfo) => {
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('relay-lock'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.interactionMode)).toBe('link');
  const before = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  const relayLink = before?.towerLinks.find((incoming) => before.towerLinks.some((outgoing) => outgoing.sourceId === incoming.targetId));
  expect(relayLink).toBeDefined();
  expect(before?.towerLinks).toHaveLength(2);
  if (!relayLink) return;
  const candidate = before?.linkCandidates.find((entry) => entry.towerId === relayLink.targetId);
  expect(candidate).toMatchObject({ valid: false, highlighted: false });
  expect(candidate?.reason).toContain('đã có đầu vào và đầu ra');
  const sourceId = before?.selectedTowerId ?? null;
  expect(sourceId).not.toBeNull();
  if (sourceId !== null) {
    await dragSelectedTowerTo(page, sourceId, relayLink.targetId, testInfo.project.name.includes('mobile') ? 'touch' : 'mouse');
  }
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.lastLinkAttempt?.result ?? '')).toContain('đã có đầu vào và đầu ra');
  const after = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(after?.towerLinks).toEqual(before?.towerLinks);
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('desktop and mobile link a highlighted target and reject a direct reciprocal link', async ({ page }, testInfo) => {
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  const pointerType = testInfo.project.name.includes('mobile') ? 'touch' : 'mouse';
  const { foundryId, fireId } = await prepareTutorialLinkAction(page, pointerType);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialObjective)).toBe('drag-foundry-fire');
  await expect(page.locator('#action-link')).toHaveCount(0);
  await expect(page.locator('#tutorial-hand')).toHaveAttribute('data-mode', 'drag');
  await dragSelectedTowerTo(page, foundryId, fireId, pointerType, async () => {
    await expect(page.locator('#game-canvas')).toHaveClass(/link-mode-active/);
    const initial = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
    expect(initial?.linkSourceTowerId).toBe(foundryId);
    expect(initial?.linkCandidates.find((candidate) => candidate.towerId === fireId)).toMatchObject({ valid: true, highlighted: true });
    if (pointerType === 'mouse') {
      const dragHighlightPath = testInfo.outputPath('link-drag-highlight.png');
      await page.locator('#game-canvas').screenshot({ path: dragHighlightPath });
      await testInfo.attach('link-drag-highlight', { path: dragHighlightPath, contentType: 'image/png' });
    }
  });
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.connections)).toBe(1);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.lastLinkAttempt?.result)).toBe('linked');
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.linkSourceTowerId)).toBeNull();
  const linkedFacing = await page.evaluate(({ sourceId, targetId }) => window.__THREE_GAME_DIAGNOSTICS__?.towerLinks.find(
    (link) => link.sourceId === sourceId && link.targetId === targetId,
  )?.facingError ?? Number.POSITIVE_INFINITY, { sourceId: foundryId, targetId: fireId });
  expect(linkedFacing).toBeLessThan(0.001);

  if (pointerType === 'mouse') {
    const markedPositionsPath = testInfo.outputPath('tutorial-marked-positions.png');
    await page.locator('#game-canvas').screenshot({ path: markedPositionsPath });
    await testInfo.attach('tutorial-marked-positions', { path: markedPositionsPath, contentType: 'image/png' });
    await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('active-play'));
    await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.stage)).toBe(2);
    await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.maxLinkedTowerFacingError ?? Number.POSITIVE_INFINITY)).toBeLessThan(0.001);
    const readyLinks = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.towerLinks ?? []);
    const reciprocalBase = readyLinks.find((link) =>
      readyLinks.some((previous) => previous.targetId === link.sourceId)
      && readyLinks.some((next) => next.sourceId === link.targetId));
    expect(reciprocalBase).toBeDefined();
    if (reciprocalBase) {
      await tapTowerById(page, reciprocalBase.targetId, pointerType);
      await dragSelectedTowerTo(page, reciprocalBase.targetId, reciprocalBase.sourceId, pointerType);
      await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.lastLinkAttempt?.result ?? null)).toContain('ngược trực tiếp');
      const rejected = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.lastLinkAttempt);
      expect(rejected).toMatchObject({ sourceId: reciprocalBase.targetId, targetId: reciprocalBase.sourceId });
      expect(rejected?.result).toContain('ngược trực tiếp');
      expect(await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.connections)).toBe(readyLinks.length);
    }
  }
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('only linked projectile segments deal damage and a terminal buff never launches', async ({ page }) => {
  test.setTimeout(85_000);
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('tutorial-wave'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.phase)).toBe('wave');
  await expect.poll(
    () => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.linkedSegmentEnemyHits ?? 0),
    { timeout: 25_000 },
  ).toBeGreaterThan(0);
  const diagnostics = await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__);
  expect(diagnostics?.linkedSegmentDamage).toBeGreaterThan(0);
  expect(diagnostics?.linkedProjectileLaunches).toBeGreaterThan(0);
  expect(diagnostics?.unlinkedProjectileLaunches).toBe(0);
  expect(diagnostics?.terminalBuffTowerIds).toHaveLength(1);
  expect(diagnostics?.terminalBuffProjectileLaunches).toBe(0);
  const terminalId = diagnostics?.terminalBuffTowerIds[0];
  if (terminalId !== undefined) expect(diagnostics?.projectileLaunchesByTower[terminalId] ?? 0).toBe(0);
  await page.waitForFunction(() => {
    const diagnostics = window.__THREE_GAME_DIAGNOSTICS__;
    return (diagnostics?.phase === 'ready' && diagnostics.wave === 4) || diagnostics?.reactionTutorialPopupVisible === true;
  }, undefined, { timeout: 35_000 });
  if (await page.locator('#reaction-tutorial-overlay').isVisible()) {
    await page.locator('#reaction-tutorial-continue').click();
  }
  await expect.poll(
    () => page.evaluate(() => ({
      phase: window.__THREE_GAME_DIAGNOSTICS__?.phase,
      wave: window.__THREE_GAME_DIAGNOSTICS__?.wave,
    })),
    { timeout: 35_000 },
  ).toEqual({ phase: 'ready', wave: 4 });
  expect(await page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.lives ?? 0)).toBeGreaterThan(0);
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('tower cards drag onto the logical grid with mouse and touch pointer events', async ({ page }, testInfo) => {
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await expect(page.locator('#briefing')).toHaveCount(0);
  await expect(page.locator('#tutorial-card')).toHaveCount(0);

  const target = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.getCellClientPoint(2, 1));
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
  await expect(page.locator('[data-tower-type="fire"]')).toBeEnabled();
  await expect(page.locator('[data-tower-type="fire"]')).toHaveClass(/tutorial-focus/);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.tutorialHandMode)).toBe('drag');
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
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.impactParticleBursts ?? 0)).toBeGreaterThan(0);
  if (!testInfo.project.name.includes('mobile')) {
    await page.waitForTimeout(180);
    const fireStatusPath = testInfo.outputPath('strong-fire-status-hue.png');
    await page.screenshot({ fullPage: true, path: fireStatusPath });
    await testInfo.attach('strong-fire-status-hue', { path: fireStatusPath, contentType: 'image/png' });
  }

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('status-reaction'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.reactions)).toBe(1);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.reactionMaxHpDamageRatio)).toBe(0.06);
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

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('reaction-scaling'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.activeEnemyMaxHp)).toBe(15_190);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.lastReactionBonusDamage)).toBeGreaterThanOrEqual(900);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.activeReactionBarriers)).toBe(0);
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test('Level 3 alloy armor requires Fire plus Ice and rushes after breaking', async ({ page }, testInfo) => {
  const errors = await watchErrors(page);
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('armored-intact'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.activeArmoredEnemies)).toBe(1);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.activeReactionBarriers)).toBe(1);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.armoredRushingEnemies)).toBe(0);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.visibleArmorShells)).toBe(1);

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__?.setState('armored-break'));
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.reactions)).toBe(1);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.activeReactionBarriers)).toBe(0);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.armoredRushingEnemies)).toBe(1);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.visibleArmorShells)).toBe(0);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_DIAGNOSTICS__?.maxEnemySpeedMultiplier)).toBeGreaterThan(1);

  const screenshot = testInfo.outputPath(`alloy-armor-broken-${testInfo.project.name}.png`);
  await page.screenshot({ fullPage: true, path: screenshot });
  await testInfo.attach('alloy-armor-broken-rush', { path: screenshot, contentType: 'image/png' });
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
  const infoButtons = await page.locator('#build-list .tower-info-button').all();
  expect(infoButtons).toHaveLength(7);
  for (const button of infoButtons) {
    const box = await button.boundingBox();
    expect(box?.height).toBeGreaterThanOrEqual(44);
  }
  await prepareTutorialLinkAction(page, 'touch');
  await expect(page.locator('#action-left')).toHaveClass(/hidden/);
  await expect(page.locator('#action-right')).toHaveClass(/hidden/);
  await expect(page.locator('#action-link')).toHaveCount(0);
  await expect(page.locator('#tutorial-hand')).toHaveAttribute('data-mode', 'drag');
  const selectedSourceIsReachable = await page.evaluate(() => {
    const sourceId = window.__THREE_GAME_DIAGNOSTICS__?.selectedTowerId;
    if (sourceId === null || sourceId === undefined) return false;
    const point = window.__THREE_GAME_TEST_HOOKS__?.getTowerClientPoint(sourceId);
    return Boolean(point && document.elementFromPoint(point.x, point.y) === document.querySelector('#game-canvas'));
  });
  expect(selectedSourceIsReachable).toBe(true);
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});
