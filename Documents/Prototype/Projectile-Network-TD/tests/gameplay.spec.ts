import { expect, test, type Page } from '@playwright/test';

async function boot(page: Page, path = '/'): Promise<void> {
  await page.goto(path);
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setReducedMotion(true));
}

test('Level 2 and Level 3 mirror Link pressure curves and introduce flying enemies from wave 3', async ({ page }) => {
  for (const [level, expectedWaves, expectedPlatforms, expectedGold] of [[2, 6, 2, 220], [3, 10, 4, 220]] as const) {
    await boot(page, `/?level=${level}`);
    const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
    expect(snapshot).toMatchObject({ stageIndex: level - 1, waveCount: expectedWaves, highGroundPlatformCount: expectedPlatforms });
    expect((snapshot.balance as { startingGold: number }).startingGold).toBe(expectedGold);
    const layerOne = snapshot.waveLayerOneEnemyCounts as number[];
    expect(layerOne.slice(0, 2)).toEqual([0, 0]);
    expect(layerOne[2]).toBeGreaterThan(0);
    expect((snapshot.highGroundSlots as unknown[]).length).toBeGreaterThanOrEqual(level === 2 ? 16 : 50);
    await expect(page.locator(`#stage-select [data-stage="${level - 1}"]`)).toHaveAttribute('aria-current', 'page');
  }
});

test('paired high-ground networks physically hit the flying lane on Level 2 and Level 3', async ({ page }) => {
  for (const level of [2, 3]) {
    await boot(page, `/?level=${level}`);
    await page.evaluate(() => {
      const hooks = window.__THREE_GAME_TEST_HOOKS__!;
      hooks.setState('elevated-hit-demo');
      hooks.advance(16);
    });
    const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
    expect(snapshot.layerOneEnemyHits as number).toBeGreaterThan(0);
    const nodes = snapshot.nodes as Array<{ position: number[]; active: boolean }>;
    expect(nodes).toHaveLength(4);
    expect(nodes.every((node) => node.position[1] > 3 && node.active)).toBe(true);
  }
});

test('Dồn Dập and Trọng Hồn explain cadence and damage in the upgrade UI', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name.includes('mobile'), 'The branch copy is renderer-independent.');
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.autoBuildMinimumChain());
  const generator = await nodePoint(page, 'generator');
  await page.mouse.click(generator.x, generator.y);
  await expect(page.locator('#branch-a')).toContainText('Dồn Dập');
  await expect(page.locator('#branch-a')).toContainText('0,68 giây');
  await expect(page.locator('#branch-a')).toContainText('12 sát thương');
  await expect(page.locator('#branch-b')).toContainText('Trọng Hồn');
  await expect(page.locator('#branch-b')).toContainText('1,35 giây');
  await expect(page.locator('#branch-b')).toContainText('26 sát thương');
});

test('Soul Field deals repeated AOE ticks with layered impact and floating damage feedback', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('skill-feedback'));
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.advance(2.1));
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot).toMatchObject({ soulCasts: 1, activeSoulFields: 1 });
  expect(snapshot.soulFieldDamageTicks as number).toBeGreaterThanOrEqual(2);
  expect(snapshot.soulFieldDamageEvents as number).toBeGreaterThanOrEqual(6);
  expect(snapshot.activeVfxCount as number).toBeGreaterThanOrEqual(4);
  expect(snapshot.enemyCount as number).toBe(3);
  await expect(page.locator('#game-canvas')).toBeVisible();
});

async function point(page: Page, getter: string, id: number): Promise<{ x: number; y: number }> {
  const result = await page.evaluate(({ getter, id }) => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__! as unknown as Record<string, (value: number) => { x: number; y: number } | null>;
    return hooks[getter](id);
  }, { getter, id });
  if (!result) throw new Error(`Missing projected point from ${getter}(${id})`);
  return result;
}

async function nodePoint(page: Page, type: string, slotId?: number): Promise<{ x: number; y: number }> {
  const result = await page.evaluate(({ type, slotId }) => window.__THREE_GAME_TEST_HOOKS__!.getNodeClientPoint(type, slotId), { type, slotId });
  if (!result) throw new Error(`Missing node point for ${type}`);
  return result;
}

async function gridCellId(page: Page, x: number, z: number, tier: 'low' | 'high' = 'low'): Promise<number> {
  const id = await page.evaluate(({ x, z, tier }) => window.__THREE_GAME_TEST_HOOKS__!.getGridCellIdAt(x, z, tier), { x, z, tier });
  if (id === null) throw new Error(`Missing ${tier} grid cell at (${x}, ${z})`);
  return id;
}

async function dragLink(page: Page, source: { x: number; y: number }, target: { x: number; y: number }): Promise<void> {
  await page.mouse.click(source.x, source.y);
  await page.mouse.move(source.x, source.y);
  await page.mouse.down();
  await page.mouse.move(target.x, target.y, { steps: 8 });
  await page.mouse.up();
}

async function dragBuild(page: Page, type: string, slotId: number): Promise<void> {
  const card = page.locator(`.build-card[data-type="${type}"]`);
  await card.scrollIntoViewIfNeeded();
  const box = await card.boundingBox();
  if (!box) throw new Error(`Missing build card for ${type}`);
  const target = await point(page, 'getSlotClientPoint', slotId);
  const source = { x: box.x + box.width * 0.72, y: box.y + box.height * 0.45 };
  await page.mouse.move(source.x, source.y);
  await page.mouse.down();
  await page.mouse.move(target.x, target.y, { steps: 10 });
  await expect(page.locator('body')).toHaveClass(/is-build-dragging/);
  const preview = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect((preview.buildDrag as { dragging: boolean; slotId: number }).dragging).toBe(true);
  expect((preview.buildDrag as { slotId: number }).slotId).toBe(slotId);
  expect(preview.placementPreviewCount as number).toBeGreaterThan(0);
  expect(preview.gridCellCount as number).toBeGreaterThan(40);
  await page.mouse.up();
}

test('desktop tutorial alternates unrestricted placement, linking, and immediate practice waves', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name.includes('mobile'), 'The mobile project has a dedicated touch-input contract.');
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await boot(page);

  const nexusSlot = await gridCellId(page, -1, 4);
  const generatorSlot = await gridCellId(page, -5, -4);
  await dragBuild(page, 'nexus', nexusSlot);
  await dragBuild(page, 'generator', generatorSlot);
  expect((await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).tutorialObjective).toBe('link-generator-nexus');
  await dragLink(page, await nodePoint(page, 'generator'), await nodePoint(page, 'nexus'));
  await expect(page.locator('#start-wave')).toBeEnabled();
  await page.locator('#start-wave').click();
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.advance(90));
  let state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state).toMatchObject({ phase: 'preparation', waveIndex: 1, tutorialObjective: 'place-fire' });

  await dragBuild(page, 'fire', await gridCellId(page, -5, 0));
  await dragLink(page, await nodePoint(page, 'generator'), await nodePoint(page, 'fire'));
  await dragLink(page, await nodePoint(page, 'fire'), await nodePoint(page, 'nexus'));
  await page.locator('#start-wave').click();
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.advance(120));
  state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state).toMatchObject({ phase: 'preparation', waveIndex: 2, tutorialObjective: 'place-ice' });

  await dragBuild(page, 'ice', await gridCellId(page, -5, -6));
  await dragLink(page, await nodePoint(page, 'fire'), await nodePoint(page, 'ice'));
  await dragLink(page, await nodePoint(page, 'ice'), await nodePoint(page, 'nexus'));
  await page.locator('#start-wave').click();
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.advance(30));
  state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state.reactionTutorialPopupVisible).toBe(true);
  expect(state.reactionProcs as number).toBeGreaterThan(0);
  await expect(page.locator('#reaction-tutorial-overlay')).toBeVisible();
  await page.locator('#reaction-tutorial-continue').click();
  expect((await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).tutorialObjective).toBe('free-play');
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.advance(120));
  const mastery = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(mastery).toMatchObject({ phase: 'preparation', waveIndex: 3, gold: 340, baseHp: 3, masteryCheckpointCaptured: true });
  expect((state.nodes as Array<{ type: string; slotId: number }>).find((node) => node.type === 'nexus')?.slotId).toBe(nexusSlot);
  expect(errors).toEqual([]);
});

test('active links deal real segment damage and trigger a terminal reaction', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__!;
    hooks.setState('tutorial-reaction');
    hooks.startWave();
    hooks.advance(12);
    hooks.dismissReactionTutorial();
    hooks.advance(48);
  });
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.directHits as number).toBeGreaterThan(0);
  expect(snapshot.reactionProcs as number).toBeGreaterThan(0);
  expect(snapshot.killedEnemies as number).toBeGreaterThan(0);
  expect((snapshot.camera as { orbitEnabled: boolean }).orbitEnabled).toBe(true);
});

test('unlinked tutorial source cannot start the first practice wave', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name.includes('mobile'), 'The mobile project has a dedicated touch-input contract.');
  await boot(page);
  await dragBuild(page, 'nexus', await gridCellId(page, -9, 4));
  await dragBuild(page, 'generator', await gridCellId(page, -9, -6));
  await expect(page.locator('#start-wave')).toBeDisabled();
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.activeChains).toBe(0);
});

test('mobile touch places on unrestricted cells, links, and starts practice immediately', async ({ page }, testInfo) => {
  test.skip(!testInfo.project.name.includes('mobile'), 'Touch path is specific to the mobile project.');
  await boot(page);
  const client = await page.context().newCDPSession(page);
  const touchDrag = async (source: { x: number; y: number }, target: { x: number; y: number }) => {
    await client.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: source.x, y: source.y, id: 1 }] });
    for (let step = 1; step <= 8; step += 1) {
      await client.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: source.x + (target.x - source.x) * step / 8, y: source.y + (target.y - source.y) * step / 8, id: 1 }] });
    }
    await client.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  };
  for (const [type, slotId] of [
    ['nexus', await gridCellId(page, -7, 0)], ['generator', await gridCellId(page, -7, -4)],
  ] as const) {
    const card = page.locator(`.build-card[data-type="${type}"]`);
    await card.scrollIntoViewIfNeeded();
    const box = await card.boundingBox();
    if (!box) throw new Error(`Missing mobile build card for ${type}`);
    await touchDrag({ x: box.x + box.width * 0.7, y: box.y + box.height * 0.45 }, await point(page, 'getSlotClientPoint', slotId));
  }

  const touchLink = async (source: { x: number; y: number }, target: { x: number; y: number }) => {
    await page.touchscreen.tap(source.x, source.y);
    await touchDrag(source, target);
  };
  await touchLink(await nodePoint(page, 'generator'), await nodePoint(page, 'nexus'));
  await expect(page.locator('body')).not.toHaveClass(/is-link-dragging/);
  await expect(page.locator('#start-wave')).toBeEnabled();
  await page.locator('#start-wave').tap();
  await expect.poll(async () => (await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).phase).toBe('wave');
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.advance(90));
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.links).toHaveLength(1);
  expect(snapshot).toMatchObject({ phase: 'preparation', waveIndex: 1, tutorialObjective: 'place-fire' });
  expect((snapshot.camera as { orbitEnabled: boolean }).orbitEnabled).toBe(true);
});

test('drag-link renders a live high-contrast guide on desktop and mobile', async ({ page }, testInfo) => {
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.autoBuildMinimumChain());
  const source = await nodePoint(page, 'generator');
  const target = await nodePoint(page, 'fire');

  if (testInfo.project.name.includes('mobile')) {
    const client = await page.context().newCDPSession(page);
    await page.touchscreen.tap(source.x, source.y);
    await client.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ ...source, id: 1 }] });
    for (let step = 1; step <= 8; step += 1) {
      await client.send('Input.dispatchTouchEvent', {
        type: 'touchMove',
        touchPoints: [{ x: source.x + (target.x - source.x) * step / 8, y: source.y + (target.y - source.y) * step / 8, id: 1 }],
      });
    }
    await expect(page.locator('body')).toHaveClass(/is-link-dragging/);
    const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
    expect(snapshot.linkDrag).toMatchObject({ active: true, state: 'valid' });
    expect((snapshot.linkDrag as { previewLength: number }).previewLength).toBeGreaterThan(2);
    await expect(page.locator('#game-canvas')).toHaveClass(/link-target-valid/);
    await client.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  } else {
    await page.mouse.click(source.x, source.y);
    expect((await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).selectedNodeId).not.toBeNull();
    await page.mouse.move(source.x, source.y);
    await page.mouse.down();
    expect((await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).linkDrag).toMatchObject({ active: true });
    await page.mouse.move(target.x, target.y, { steps: 8 });
    await expect(page.locator('body')).toHaveClass(/is-link-dragging/);
    const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
    expect(snapshot.linkDrag).toMatchObject({ active: true, state: 'valid' });
    expect((snapshot.linkDrag as { previewLength: number }).previewLength).toBeGreaterThan(2);
    await expect(page.locator('#game-canvas')).toHaveClass(/link-target-valid/);
    await page.mouse.up();
  }
});

test('camera orbit preserves pan and zoom on desktop and mobile', async ({ page }, testInfo) => {
  await boot(page);
  const initial = (await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).camera as {
    yaw: number; pitch: number; distance: number; target: number[]; orbitEnabled: boolean;
  };
  expect(initial.orbitEnabled).toBe(true);

  if (testInfo.project.name.includes('mobile')) {
    const client = await page.context().newCDPSession(page);
    await client.send('Input.dispatchTouchEvent', {
      type: 'touchStart',
      touchPoints: [{ x: 78, y: 250, id: 1 }, { x: 150, y: 250, id: 2 }],
    });
    for (let step = 1; step <= 6; step += 1) {
      await client.send('Input.dispatchTouchEvent', {
        type: 'touchMove',
        touchPoints: [
          { x: 78 + step * 8, y: 250 + step * 5, id: 1 },
          { x: 150 + step * 8, y: 250 + step * 5, id: 2 },
        ],
      });
    }
    await client.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    const orbited = (await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).camera as typeof initial;
    expect(Math.abs(orbited.yaw - initial.yaw)).toBeGreaterThan(0.15);
    expect(Math.abs(orbited.pitch - initial.pitch)).toBeGreaterThan(0.08);
    expect(orbited.pitch).toBeGreaterThanOrEqual((orbited as unknown as { minPitch: number }).minPitch);
    expect(orbited.pitch).toBeLessThanOrEqual((orbited as unknown as { maxPitch: number }).maxPitch);

    await client.send('Input.dispatchTouchEvent', {
      type: 'touchStart',
      touchPoints: [{ x: 82, y: 255, id: 3 }, { x: 148, y: 255, id: 4 }],
    });
    await client.send('Input.dispatchTouchEvent', {
      type: 'touchMove',
      touchPoints: [{ x: 48, y: 255, id: 3 }, { x: 182, y: 255, id: 4 }],
    });
    await client.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    const zoomed = (await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).camera as typeof initial;
    expect(zoomed.distance).not.toBeCloseTo(orbited.distance, 2);

    const beforePan = zoomed.target;
    await client.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: 92, y: 290, id: 5 }] });
    await client.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: 128, y: 310, id: 5 }] });
    await client.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    const panned = (await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).camera as typeof initial;
    expect(Math.hypot(panned.target[0] - beforePan[0], panned.target[2] - beforePan[2])).toBeGreaterThan(0.1);
  } else {
    await page.mouse.move(620, 330);
    await page.mouse.down({ button: 'right' });
    await page.mouse.move(760, 390, { steps: 8 });
    await page.mouse.up({ button: 'right' });
    const orbited = (await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).camera as typeof initial & { minPitch: number; maxPitch: number };
    expect(Math.abs(orbited.yaw - initial.yaw)).toBeGreaterThan(0.3);
    expect(Math.abs(orbited.pitch - initial.pitch)).toBeGreaterThan(0.15);
    expect(orbited.pitch).toBeGreaterThanOrEqual(orbited.minPitch);
    expect(orbited.pitch).toBeLessThanOrEqual(orbited.maxPitch);

    await page.mouse.wheel(0, 400);
    const zoomed = (await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).camera as typeof initial;
    expect(zoomed.distance).not.toBeCloseTo(orbited.distance, 2);

    const panStart = await point(page, 'getSlotClientPoint', await gridCellId(page, -9, 4));
    await page.mouse.move(panStart.x, panStart.y);
    await page.mouse.down();
    await page.mouse.move(panStart.x + 55, panStart.y + 28, { steps: 5 });
    await page.mouse.up();
    const panned = (await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).camera as typeof initial;
    expect(Math.hypot(panned.target[0] - zoomed.target[0], panned.target[2] - zoomed.target[2])).toBeGreaterThan(0.1);
  }
});

test('first-reaction icon and modal match Link feedback', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__!;
    hooks.setState('tutorial-reaction'); hooks.startWave(); hooks.advance(30);
  });
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.reactionProcs as number).toBeGreaterThan(0);
  expect(snapshot.reactionTutorialPopupVisible).toBe(true);
  await expect(page.locator('#reaction-tutorial-overlay')).toBeVisible();
  await expect(page.locator('#reaction-tutorial-title')).not.toBeEmpty();
  await page.locator('#reaction-tutorial-continue').click();
  await expect(page.locator('#reaction-tutorial-overlay')).toBeHidden();
  expect((await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).phase).toBe('wave');
});

test('enemy elemental tint and icon are visible on desktop and mobile', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setElementStatusDemo('fire'));
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.statusIcons).toBe(1);
  await expect(page.locator('#game-canvas')).toBeVisible();
});

test('completed link lines are visible only for the selected network', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name.includes('mobile'), 'Network visibility is renderer-shared; mobile link-drag coverage runs separately.');
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.autoBuildMinimumChain());

  let snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.selectedNodeId).not.toBeNull();
  expect(snapshot.visibleCompletedLinks).toHaveLength(3);

  const isolatedSlot = await gridCellId(page, -9, 6);
  await dragBuild(page, 'earth', isolatedSlot);
  snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect((snapshot.nodes as Array<{ id: number; type: string; slotId: number }>).find((node) => node.type === 'earth' && node.slotId === isolatedSlot)?.id).toBe(snapshot.selectedNodeId);
  expect(snapshot.visibleCompletedLinks).toHaveLength(0);

  await page.mouse.click((await nodePoint(page, 'generator')).x, (await nodePoint(page, 'generator')).y);
  snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.visibleCompletedLinks).toHaveLength(3);

  const emptyPoint = await point(page, 'getSlotClientPoint', await gridCellId(page, 5, -6));
  await page.mouse.click(emptyPoint.x, emptyPoint.y);
  snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.selectedNodeId).toBeNull();
  expect(snapshot.visibleCompletedLinks).toHaveLength(0);
});

test('x1 x2 x3 controls accelerate wave simulation on desktop and mobile', async ({ page }) => {
  await boot(page);
  const speedButtons = page.locator('#speed-controls button');
  await expect(speedButtons).toHaveCount(3);
  await expect(page.locator('#speed-controls [data-speed="1"]')).toHaveAttribute('aria-pressed', 'true');
  await page.evaluate(() => { window.__THREE_GAME_TEST_HOOKS__!.autoBuildMinimumChain(); window.__THREE_GAME_TEST_HOOKS__!.startWave(); });
  await page.locator('#speed-controls [data-speed="3"]').click();
  await expect(page.locator('#speed-controls [data-speed="3"]')).toHaveAttribute('aria-pressed', 'true');
  await page.waitForTimeout(450);
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.gameSpeed).toBe(3);
  expect(snapshot.waveClock as number).toBeGreaterThan(0.75);
  for (const button of await speedButtons.all()) {
    const box = await button.boundingBox();
    expect(box?.height ?? 0).toBeGreaterThanOrEqual(38);
    if (test.info().project.name.includes('mobile')) expect(box?.width ?? 0).toBeGreaterThanOrEqual(44);
  }
  await page.locator('#speed-controls [data-speed="2"]').click();
  await expect(page.locator('#speed-controls [data-speed="2"]')).toHaveAttribute('aria-pressed', 'true');
});

test('Soul prototype exposes the Link prototype combat balance contract', async ({ page }) => {
  await boot(page);
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  const balance = snapshot.balance as {
    projectileSpeed: number; projectileRadius: number; projectileVisualScale: number;
    towerFireRateMultiplier: number; enemySpeedMultiplier: number; startingGold: number; startingBaseHp: number;
    nodes: Record<string, { cost: number; interval: number; connectionRange: number }>;
    enemies: Record<string, { hp: number; speed: number; radius: number }>;
  };
  expect(balance).toMatchObject({ projectileSpeed: 27.6, projectileRadius: 0.84, projectileVisualScale: 2, towerFireRateMultiplier: 1.5, enemySpeedMultiplier: 0.6, startingGold: 420, startingBaseHp: 3 });
  expect(balance.nodes.generator).toMatchObject({ cost: 80, interval: 0.92, connectionRange: 12.6 });
  expect(balance.nodes.fire).toMatchObject({ cost: 70, interval: 0.72, connectionRange: 12 });
  expect(balance.enemies.swarm).toMatchObject({ hp: 54, speed: 2.05, radius: 0.5 });
  expect(balance.enemies.boss).toMatchObject({ hp: 980, speed: 0.72, radius: 1.18 });
  expect((snapshot.balance as { waveClearRewardMultiplier: number; purchasePriceGrowthPerTower: number }).waveClearRewardMultiplier).toBe(0.65);
  expect((snapshot.balance as { waveClearRewardMultiplier: number; purchasePriceGrowthPerTower: number }).purchasePriceGrowthPerTower).toBe(0.12);
  await expect(page.locator('#tutorial-hand')).toHaveAttribute('data-mode', 'drag');
});
