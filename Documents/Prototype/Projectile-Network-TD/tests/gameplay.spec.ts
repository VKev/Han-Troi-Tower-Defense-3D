import { expect, test, type Page } from '@playwright/test';

async function boot(page: Page, path = '/'): Promise<void> {
  await page.goto(path);
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setReducedMotion(true));
}

test('Level 2 and Level 3 preserve Link pressure curves on ground-only drought battlefields', async ({ page }) => {
  for (const [level, expectedWaves, expectedGold, expectedHealth, expectedRainCharge] of [
    [2, 6, 220, [1.8, 2.4, 3.5, 5, 7.5, 10.5], 0.5],
    [3, 10, 220, [2, 2.8, 4, 5.8, 8, 11, 14.5, 19, 24.5, 31], 0.35],
  ] as const) {
    await boot(page, `/?level=${level}`);
    const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
    expect(snapshot).toMatchObject({ theme: 'coc-kien-troi-drought', stageIndex: level - 1, waveCount: expectedWaves, highGroundPlatformCount: 0 });
    expect((snapshot.balance as { startingGold: number }).startingGold).toBe(expectedGold);
    expect(snapshot.waveLayerOneEnemyCounts).toEqual(Array(expectedWaves).fill(0));
    expect(snapshot.waveHealthMultipliers).toEqual(expectedHealth);
    expect((snapshot.balance as { rainChargeMultiplier: number }).rainChargeMultiplier).toBe(expectedRainCharge);
    const chargedSoul = await page.evaluate(() => {
      const hooks = window.__THREE_GAME_TEST_HOOKS__!;
      hooks.setSoul(0);
      hooks.creditRainChargeHits(10);
      return hooks.snapshot().soul;
    });
    expect(chargedSoul).toBeCloseTo(10 * expectedRainCharge, 5);
    expect(snapshot.highGroundSlots).toEqual([]);
    await expect(page.locator(`#stage-select [data-stage="${level - 1}"]`)).toHaveAttribute('aria-current', 'page');
  }
});

test('former air archetypes remain full-detail ground threats on Level 2 and Level 3', async ({ page }) => {
  for (const level of [2, 3]) {
    await boot(page, `/?level=${level}`);
    const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
    const enemies = (snapshot.balance as { enemies: Record<string, { layer: number }> }).enemies;
    expect(enemies.wisp.layer).toBe(0);
    expect(enemies.skyWarder.layer).toBe(0);
    expect(snapshot.highGroundPlatformCount).toBe(0);
  }
});

test('enemy trails have no spawn-direction arrow on any campaign level', async ({ page }) => {
  for (const level of [1, 2, 3]) {
    await boot(page, `/?level=${level}`);
    const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
    expect(snapshot.pathVisual).toMatchObject({
      layers: ['shoulder', 'raised-edge', 'textured-surface'],
      spawnDirectionMarkerCount: 0,
    });
  }
});

test('completed links use one translucent white style outside the endpoint tutorial', async ({ page }) => {
  for (const level of [2, 3]) {
    await boot(page, `/?level=${level}`);
    await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.autoBuildMinimumChain());
    const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
    expect(snapshot.completedLinkStyle).toEqual({ color: '#ffffff', opacityActive: 0.42, opacityInactive: 0.18 });
    expect(snapshot.tutorialEndpointLinkHighlights).toEqual([]);
    expect(snapshot.completedLinkVisuals as Array<{ color: string; opacity: number; transparent: boolean }>).toEqual([
      expect.objectContaining({ color: '#ffffff', opacity: 0.42, transparent: true }),
      expect.objectContaining({ color: '#ffffff', opacity: 0.42, transparent: true }),
      expect.objectContaining({ color: '#ffffff', opacity: 0.42, transparent: true }),
    ]);
  }
});

test('Dồn Dập and Trọng Đạn explain cadence and damage in the upgrade UI', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name.includes('mobile'), 'The branch copy is renderer-independent.');
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.autoBuildMinimumChain());
  const generator = await nodePoint(page, 'generator');
  await page.mouse.click(generator.x, generator.y);
  await expect(page.locator('#branch-a')).toContainText('Dồn Dập');
  await expect(page.locator('#branch-a')).toContainText('0,68 giây');
  await expect(page.locator('#branch-a')).toContainText('12 sát thương');
  await expect(page.locator('#branch-b')).toContainText('Trọng Đạn');
  await expect(page.locator('#branch-b')).toContainText('1,35 giây');
  await expect(page.locator('#branch-b')).toContainText('26 sát thương');
});

test('Trống Gọi Mưa offers the approved Quét Rộng and Đớp Mạnh frog-skill branches', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name.includes('mobile'), 'The branch copy is renderer-independent.');
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.autoBuildMinimumChain());
  const nexus = await nodePoint(page, 'nexus');
  await page.mouse.click(nexus.x, nexus.y);
  await expect(page.locator('#branch-a')).toContainText('Quét Rộng');
  await expect(page.locator('#branch-a')).toContainText('3,1');
  await expect(page.locator('#branch-b')).toContainText('Đớp Mạnh');
  await expect(page.locator('#branch-b')).toContainText('2,5');
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.tongueSkill).toMatchObject({ branch: 'base', radius: 2.7, flatDamage: 220, maxHpRatio: 0.18, maxHpCap: 500 });
});

test('Cóc Bắt Mồi deals one non-stacking impact, lighter corridor damage, and captures impact kills', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__!;
    hooks.setState('skill-feedback');
    hooks.setPausedForScreenshot(true);
  });
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.advance(0.08));
  const extending = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  const extension = extending.tongueSkill as { phase: string; origin: number[]; visualLength: number; targetDistance: number };
  expect(extension.phase).toBe('outbound');
  expect(extension.visualLength).toBeGreaterThan(0.5);
  expect(extension.visualLength).toBeLessThan(extension.targetDistance);
  expect(extension.origin).toEqual((extending.fixedNexus as { mouthPosition: number[] }).mouthPosition);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.advance(0.11));
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot).toMatchObject({ soulCasts: 1 });
  expect(snapshot.tongueSkill).toMatchObject({
    active: true, phase: 'impact', radius: 2.7, corridorDamageRatio: 0.2,
    impactHits: 3, corridorHits: 1, capturedKills: 3, carrying: 3,
    presentation: {
      modelType: 'solid-3d-tapered', bodyGeometry: 'tapered-cylinder', bodyMaterial: 'MeshStandardMaterial',
      rootRadius: 0.2, bodyTipRadius: 0.34, tipGeometry: 'SphereGeometry', tipRadius: 0.68,
      tipHighlight: true, capturedEnemyScale: 0.62, usesGlow: false, glowMeshCount: 0,
      impactDiscOpacity: 0.06, dirtParticleCount: 14, activeDirtParticles: 14,
    },
  });
  expect(snapshot.activeVfxCount as number).toBeGreaterThanOrEqual(4);
  expect(snapshot.enemyCount as number).toBe(1);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.advance(0.5));
  expect((await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).tongueSkill).toMatchObject({ active: false, carrying: 0 });
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
  const endpointLesson = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(endpointLesson.tutorialObjective).toBe('link-generator-nexus');
  expect(endpointLesson.tutorialEndpointLabels).toEqual([
    expect.objectContaining({ name: 'tutorialSourceLabelMarker', role: 'source', label: 'ĐẦU', connected: false, color: '#fff4c9' }),
    expect.objectContaining({ name: 'tutorialTerminalLabelMarker', role: 'terminal', label: 'CUỐI', connected: false, color: '#fff4c9' }),
  ]);
  expect(endpointLesson.tutorialEndpointPresentation).toEqual({ rings: 0, glyphs: 0, halos: 0, sockets: 0, linkHalves: 0 });
  expect(endpointLesson.tutorialEndpointBuildLabels).toEqual([
    { type: 'generator', text: 'Lò Đạn (ĐẦU)' },
    { type: 'nexus', text: 'Trống Mưa (CUỐI)' },
  ]);
  expect(endpointLesson.tutorialChainReminder).toMatchObject({ visible: true });
  await expect(page.locator('#tutorial-chain-reminder')).toBeVisible();
  await dragLink(page, await nodePoint(page, 'generator'), await nodePoint(page, 'nexus'));
  const directLink = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(directLink.tutorialEndpointLabels).toHaveLength(2);
  expect(directLink.tutorialEndpointLabels).toEqual([
    expect.objectContaining({ role: 'source', connected: true, color: '#65f7a4' }),
    expect.objectContaining({ role: 'terminal', connected: true, color: '#65f7a4' }),
  ]);
  expect(directLink.completedLinkStyle).toEqual({ color: '#ffffff', opacityActive: 0.42, opacityInactive: 0.18 });
  expect(directLink.completedLinkVisuals).toEqual([
    expect.objectContaining({ color: '#ffffff', opacity: 0.42, transparent: true, visible: true }),
  ]);
  expect(directLink.tutorialEndpointLinkHighlights).toEqual([]);
  expect(directLink.tutorialChainReminder).toMatchObject({ visible: false });
  expect(directLink.chainCompletionNotice).toMatchObject({
    active: true, passesTotal: 2, currentPass: 1, brightSegmentCount: 0, beamOverlayCount: 0,
  });
  await expect(page.locator('#start-wave')).toBeEnabled();
  await page.locator('#start-wave').click();
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.advance(90));
  let state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state).toMatchObject({ phase: 'preparation', waveIndex: 1, tutorialObjective: 'place-fire' });
  expect(state.tutorialEndpointLabels).toHaveLength(2);

  await dragBuild(page, 'fire', await gridCellId(page, -5, 0));
  await dragLink(page, await nodePoint(page, 'generator'), await nodePoint(page, 'fire'));
  await dragLink(page, await nodePoint(page, 'fire'), await nodePoint(page, 'nexus'));
  await page.locator('#start-wave').click();
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.advance(120));
  state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state).toMatchObject({ phase: 'preparation', waveIndex: 2, tutorialObjective: 'place-ice' });
  expect(state.tutorialEndpointLabels).toHaveLength(2);

  await dragBuild(page, 'ice', await gridCellId(page, -5, -6));
  await dragLink(page, await nodePoint(page, 'fire'), await nodePoint(page, 'ice'));
  await dragLink(page, await nodePoint(page, 'ice'), await nodePoint(page, 'nexus'));
  const chainedLink = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  const chainedNodes = chainedLink.nodes as Array<{ id: number; type: string }>;
  expect(chainedLink.tutorialEndpointLinkHighlights).toEqual([]);
  expect(chainedLink.chainCompletionNotice).toMatchObject({
    active: true, passesTotal: 2, brightSegmentCount: 0, beamOverlayCount: 0,
    routeNodeIds: ['generator', 'fire', 'ice', 'nexus'].map((type) => chainedNodes.find((node) => node.type === type)!.id),
  });
  expect((chainedLink.completedLinkVisuals as Array<{ color: string; transparent: boolean }>)).toEqual([
    expect.objectContaining({ color: '#ffffff', transparent: true }),
    expect.objectContaining({ color: '#ffffff', transparent: true }),
    expect.objectContaining({ color: '#ffffff', transparent: true }),
  ]);
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
  expect(mastery.tutorialEndpointLabels).toEqual([
    expect.objectContaining({ role: 'source', label: 'ĐẦU', connected: true, color: '#65f7a4' }),
    expect.objectContaining({ role: 'terminal', label: 'CUỐI', connected: true, color: '#65f7a4' }),
  ]);
  expect((state.nodes as Array<{ type: string; slotId: number }>).find((node) => node.type === 'nexus')?.slotId).toBe(nexusSlot);
  expect(errors).toEqual([]);
});

test('source and terminal labels persist into mastery on desktop and mobile', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('mastery-ready'));
  const state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state).toMatchObject({ stageIndex: 0, tutorialMasteryPhase: true });
  expect(state.tutorialEndpointLabels).toEqual([
    expect.objectContaining({ role: 'source', label: 'ĐẦU', connected: true, color: '#65f7a4' }),
    expect.objectContaining({ role: 'terminal', label: 'CUỐI', connected: true, color: '#65f7a4' }),
  ]);
  expect(state.tutorialEndpointPresentation).toEqual({ rings: 0, glyphs: 0, halos: 0, sockets: 0, linkHalves: 0 });
  expect(state.completedLinkStyle).toEqual({ color: '#ffffff', opacityActive: 0.42, opacityInactive: 0.18 });
  expect(state.tutorialEndpointLinkHighlights).toEqual([]);
});

test('intermediate towers are dim until they belong to a complete source-to-terminal route', async ({ page }) => {
  await boot(page);
  const presentation = (node: { networkVisual: unknown }) => node.networkVisual as {
    state: string; reason: string; materialCount: number;
    colorRatio: number; emissiveRatio: number; emissiveIntensityRatio: number;
  };

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('element-models'));
  let state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  const looseElements = state.nodes as Array<{ type: string; active: boolean; networkVisual: unknown }>;
  expect((state.balance as { networkTowerPresentation: unknown }).networkTowerPresentation).toEqual({
    dimColorMultiplier: 0.32,
    dimEmissiveMultiplier: 0.08,
    dimEmissiveIntensityMultiplier: 0.12,
  });
  expect(looseElements.map(({ type }) => type).sort()).toEqual(['earth', 'fire', 'ice', 'wind']);
  for (const node of looseElements) {
    expect(node.active).toBe(false);
    const visual = presentation(node);
    expect(visual).toMatchObject({ state: 'dimmed', reason: 'incomplete-route' });
    expect(visual.materialCount).toBeGreaterThan(0);
    expect(visual.colorRatio).toBeCloseTo(0.32, 5);
    expect(visual.emissiveRatio).toBeCloseTo(0.08, 5);
    expect(visual.emissiveIntensityRatio).toBeCloseTo(0.12, 5);
  }

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('mastery-ready'));
  state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  const completeRoute = state.nodes as Array<{ type: string; active: boolean; networkVisual: unknown }>;
  for (const node of completeRoute) {
    const visual = presentation(node);
    expect(visual.state).toBe('full');
    expect(visual.reason).toBe(node.type === 'generator' || node.type === 'nexus' ? 'endpoint' : 'complete-route');
    expect(visual.colorRatio).toBeCloseTo(1, 5);
    expect(visual.emissiveRatio).toBeCloseTo(1, 5);
    expect(visual.emissiveIntensityRatio).toBeCloseTo(1, 5);
  }

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('broken-chain-labels'));
  state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  const brokenRoute = state.nodes as Array<{ type: string; active: boolean; networkVisual: unknown }>;
  for (const node of brokenRoute) {
    const visual = presentation(node);
    if (node.type === 'generator' || node.type === 'nexus') {
      expect(visual).toMatchObject({ state: 'full', reason: 'endpoint' });
      expect(visual.colorRatio).toBeCloseTo(1, 5);
    } else {
      expect(node.active).toBe(false);
      expect(visual).toMatchObject({ state: 'dimmed', reason: 'incomplete-route' });
      expect(visual.colorRatio).toBeCloseTo(0.32, 5);
    }
  }

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('dual-terminal-network'));
  state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  const dualRoute = state.nodes as Array<{ networkVisual: unknown }>;
  expect(dualRoute).toHaveLength(5);
  expect(dualRoute.every((node) => presentation(node).state === 'full')).toBe(true);
});

test('a newly completed chain announces itself with exactly two LED passes then restores normal link visibility', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('chain-complete-notice'));
  let state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state.chainCompletionNotice).toMatchObject({
    active: true, passesTotal: 2, currentPass: 1, brightSegmentCount: 0, beamOverlayCount: 0,
  });
  expect((state.chainCompletionNotice as { activeLedCount: number }).activeLedCount).toBeGreaterThan(0);

  const emptyPoint = await point(page, 'getSlotClientPoint', await gridCellId(page, 5, -6));
  await page.mouse.click(emptyPoint.x, emptyPoint.y);
  state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state.chainCompletionNotice).toMatchObject({ active: true, beamOverlayCount: 0 });
  expect(state.visibleCompletedLinks).toEqual([]);

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.advance(1.55));
  state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state.chainCompletionNotice).toMatchObject({ active: true, passesTotal: 2, currentPass: 2, completedPasses: 1 });

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.advance(1.5));
  state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state.chainCompletionNotice).toMatchObject({ active: false, passesTotal: 2, ledCount: 0, brightSegmentCount: 0 });
  expect((state.completedLinkVisuals as Array<{ visible: boolean }>).every((link) => !link.visible)).toBe(true);
});

test('endpoint labels are green only while the full directed chain remains valid', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('chain-complete-notice'));
  let labels = (await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).tutorialEndpointLabels as Array<{
    connected: boolean; color: string;
  }>;
  expect(labels).toHaveLength(2);
  expect(labels.every((label) => label.connected && label.color === '#65f7a4')).toBe(true);

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('broken-chain-labels'));
  const broken = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  labels = broken.tutorialEndpointLabels as Array<{ connected: boolean; color: string }>;
  expect(broken.activeChains).toBe(0);
  expect(labels).toHaveLength(2);
  expect(labels.every((label) => !label.connected && label.color === '#fff4c9')).toBe(true);
});

test('endpoint labels pulse once whenever full-route validity changes', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => {
    window.__THREE_GAME_TEST_HOOKS__!.setReducedMotion(false);
    window.__THREE_GAME_TEST_HOOKS__!.setState('chain-complete-notice');
  });
  await expect.poll(async () => {
    const pulse = (await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).tutorialEndpointPulse as {
      active: boolean; currentScale: number; direction: string; transitions: number;
    };
    return pulse.active && pulse.direction === 'connected' && pulse.transitions === 1 && pulse.currentScale > 1.12;
  }).toBe(true);
  await expect.poll(async () => {
    const pulse = (await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).tutorialEndpointPulse as {
      active: boolean; currentScale: number;
    };
    return !pulse.active && Math.abs(pulse.currentScale - 1) < 0.001;
  }).toBe(true);

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('broken-chain-labels'));
  await expect.poll(async () => {
    const pulse = (await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).tutorialEndpointPulse as {
      active: boolean; currentScale: number; direction: string; transitions: number;
    };
    return pulse.active && pulse.direction === 'disconnected' && pulse.transitions === 2 && pulse.currentScale > 1.12;
  }).toBe(true);
  await expect.poll(async () => {
    const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
    const labels = snapshot.tutorialEndpointLabels as Array<{ scale: number }>;
    const pulse = snapshot.tutorialEndpointPulse as { active: boolean };
    return !pulse.active && labels.length === 2 && labels.every((label) => Math.abs(label.scale - 1) < 0.001);
  }).toBe(true);
});

test('Wind cannot stall enemies and repeated reactions respect a per-enemy cooldown', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setElementStatusDemo('wind'));
  let state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect((state.activeEnemies as Array<{ progress: number; windTime: number }>)[0]).toMatchObject({ progress: 4, windTime: 1.8 });

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('reaction-cooldown'));
  state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state).toMatchObject({ reactionProcs: 2, blockedReactionProcs: 1 });
  expect(state.reactionBalance).toMatchObject({
    repeatCooldown: 2.25,
    baseWindProgressRewind: 0,
    tempestProgressRewind: { regular: 0.8, boss: 0.35 },
  });
  let cooldowns = (state.reactionBalance as { activeEnemyCooldowns: Array<{ cooldowns: Record<string, number> }> }).activeEnemyCooldowns[0].cooldowns;
  expect(cooldowns.tempest).toBeGreaterThan(2);
  expect(cooldowns.tempest).toBeLessThanOrEqual(2.25);
  expect(cooldowns.shatter).toBeGreaterThan(2);
  expect(cooldowns.shatter).toBeLessThanOrEqual(2.25);

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.procTestReaction('tempest'));
  state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state).toMatchObject({ reactionProcs: 2, blockedReactionProcs: 2 });

  await page.evaluate(() => {
    window.__THREE_GAME_TEST_HOOKS__!.advance(2.3);
    window.__THREE_GAME_TEST_HOOKS__!.procTestReaction('tempest');
  });
  state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state).toMatchObject({ reactionProcs: 3, blockedReactionProcs: 2 });
  cooldowns = (state.reactionBalance as { activeEnemyCooldowns: Array<{ cooldowns: Record<string, number> }> }).activeEnemyCooldowns[0].cooldowns;
  expect(cooldowns.tempest).toBeGreaterThan(2);
  expect(cooldowns.tempest).toBeLessThanOrEqual(2.25);
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

test('two independent generator routes both transport into the two-input Rain Drum', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('dual-terminal-network'));
  let snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.activeChains).toBe(2);
  const nodesBefore = snapshot.nodes as Array<{ type: string; active: boolean; nexusInputs: number[] }>;
  expect(nodesBefore.filter(({ type, active }) => type === 'generator' && active)).toHaveLength(2);
  expect(nodesBefore.find(({ type }) => type === 'nexus')?.nexusInputs).toHaveLength(2);
  await page.evaluate(() => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__!;
    hooks.startWave();
    hooks.advance(2.5);
  });
  snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  const generators = (snapshot.nodes as Array<{ type: string; launches: number }>).filter(({ type }) => type === 'generator');
  expect(generators).toHaveLength(2);
  expect(generators.every(({ launches }) => launches > 0)).toBe(true);
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

  const touchLink = async (source: { x: number; y: number }, target: { x: number; y: number }) => touchDrag(source, target);
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
    await client.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ ...source, id: 1 }] });
    expect((await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).linkDrag).toMatchObject({ active: true });
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
    await page.mouse.move(source.x, source.y);
    await page.mouse.down();
    const started = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
    expect(started.linkDrag).toMatchObject({ active: true });
    expect(started.selectedNodeId).toBe((started.linkDrag as { sourceId: number }).sourceId);
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
  // The repositioned Ice route can defeat the final enemy before the delayed lesson opens;
  // dismissing the lesson then correctly completes the already-cleared wave.
  expect((await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).phase).toBe('preparation');
  await page.evaluate(() => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__!;
    hooks.setState('tutorial-reaction'); hooks.startWave(); hooks.advance(30);
  });
  const replay = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(replay.reactionProcs as number).toBeGreaterThan(0);
  expect(replay.reactionTutorialPopupVisible).toBe(false);
  await expect(page.locator('#reaction-tutorial-overlay')).toBeHidden();
});

test('enemy elemental tint and icon are visible on desktop and mobile', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setElementStatusDemo('fire'));
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.statusIcons).toBe(1);
  await expect(page.locator('#game-canvas')).toBeVisible();
});

test('completed links appear only for the selected network after the completion notice ends', async ({ page }, testInfo) => {
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
  expect(snapshot.visibleCompletedLinks).toEqual([]);

  await page.mouse.click((await nodePoint(page, 'generator')).x, (await nodePoint(page, 'generator')).y);
  snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.visibleCompletedLinks).toHaveLength(3);

  const emptyPoint = await point(page, 'getSlotClientPoint', await gridCellId(page, 5, -6));
  await page.mouse.click(emptyPoint.x, emptyPoint.y);
  snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.selectedNodeId).toBeNull();
  expect(snapshot.visibleCompletedLinks).toEqual([]);
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

test('Cóc Kiện Trời prototype preserves the Link prototype combat balance contract', async ({ page }) => {
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
