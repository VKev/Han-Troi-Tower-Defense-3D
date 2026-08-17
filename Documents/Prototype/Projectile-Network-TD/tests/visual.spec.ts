import { expect, test } from '@playwright/test';

test.beforeEach(async ({ page }) => {
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await page.evaluate(() => {
    window.__THREE_GAME_TEST_HOOKS__!.setReducedMotion(true);
    window.__THREE_GAME_TEST_HOOKS__!.autoBuildMinimumChain();
    window.__THREE_GAME_TEST_HOOKS__!.setPausedForScreenshot(true);
  });
});

test('authored preparation state remains visually stable', async ({ page }, testInfo) => {
  await expect(page.locator('#game-canvas')).toBeVisible();
  await expect(page.locator('#top-bar')).toBeVisible();
  await expect(page.locator('#start-wave')).toBeEnabled();
  await expect(page).toHaveScreenshot(`preparation-${testInfo.project.name}.png`, {
    animations: 'disabled',
    maxDiffPixelRatio: 0.025,
  });
});

test('active combat state remains visually stable', async ({ page }, testInfo) => {
  await page.evaluate(() => {
    window.__THREE_GAME_TEST_HOOKS__!.setPausedForScreenshot(false);
    window.__THREE_GAME_TEST_HOOKS__!.startWave();
    window.__THREE_GAME_TEST_HOOKS__!.advance(6);
    window.__THREE_GAME_TEST_HOOKS__!.dismissReactionTutorial();
    window.__THREE_GAME_TEST_HOOKS__!.advance(2);
    window.__THREE_GAME_TEST_HOOKS__!.setPausedForScreenshot(true);
  });
  await expect(page).toHaveScreenshot(`combat-${testInfo.project.name}.png`, {
    animations: 'disabled',
    maxDiffPixelRatio: 0.035,
  });
});

test('unlinked elemental tower silhouettes remain dim but readable beside the raised drought trail', async ({ page }, testInfo) => {
  await page.evaluate(() => {
    window.__THREE_GAME_TEST_HOOKS__!.setState('element-models');
    window.__THREE_GAME_TEST_HOOKS__!.setPausedForScreenshot(true);
  });
  await expect(page).toHaveScreenshot(`element-towers-trail-${testInfo.project.name}.png`, {
    animations: 'disabled',
    maxDiffPixelRatio: 0.025,
  });
});

test('broken route keeps source and terminal bright while intermediate towers dim', async ({ page }, testInfo) => {
  await page.evaluate(() => {
    window.__THREE_GAME_TEST_HOOKS__!.setState('broken-chain-labels');
    window.__THREE_GAME_TEST_HOOKS__!.setPausedForScreenshot(true);
  });
  const state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  const nodes = state.nodes as Array<{ type: string; networkVisual: { state: string; reason: string } }>;
  expect(nodes.filter(({ type }) => type === 'generator' || type === 'nexus').every(({ networkVisual }) => networkVisual.state === 'full' && networkVisual.reason === 'endpoint')).toBe(true);
  expect(nodes.filter(({ type }) => type !== 'generator' && type !== 'nexus').every(({ networkVisual }) => networkVisual.state === 'dimmed' && networkVisual.reason === 'incomplete-route')).toBe(true);
  await expect(page).toHaveScreenshot(`broken-route-tower-brightness-${testInfo.project.name}.png`, {
    animations: 'disabled',
    maxDiffPixelRatio: 0.025,
  });
});

test('source and Rain Drum endpoint lesson labels both network anchors without highlight clutter', async ({ page }, testInfo) => {
  await page.evaluate(() => {
    window.__THREE_GAME_TEST_HOOKS__!.setState('intro-currency');
    window.__THREE_GAME_TEST_HOOKS__!.setPausedForScreenshot(true);
  });
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.tutorialObjective).toBe('link-generator-nexus');
  expect(snapshot.tutorialEndpointLabels).toEqual([
    expect.objectContaining({ role: 'source', label: 'ĐẦU', connected: false, color: '#fff4c9' }),
    expect.objectContaining({ role: 'terminal', label: 'CUỐI', connected: false, color: '#fff4c9' }),
  ]);
  expect(snapshot.tutorialEndpointPresentation).toEqual({ rings: 0, glyphs: 0, halos: 0, sockets: 0, linkHalves: 0 });
  expect(snapshot.tutorialChainReminder).toMatchObject({ visible: true });
  await expect(page.locator('#tutorial-hand')).toHaveAttribute('data-mode', 'drag');
  await expect(page).toHaveScreenshot(`tutorial-source-terminal-${testInfo.project.name}.png`, {
    animations: 'disabled',
    maxDiffPixelRatio: 0.02,
  });
});

test('completed chain LED notification remains visually stable', async ({ page }, testInfo) => {
  await page.evaluate(() => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__!;
    hooks.setState('chain-complete-notice');
    hooks.advance(0.42);
    hooks.setPausedForScreenshot(true);
  });
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.chainCompletionNotice).toMatchObject({
    active: true, passesTotal: 2, currentPass: 1, brightSegmentCount: 0, beamOverlayCount: 0,
  });
  expect(snapshot.tutorialEndpointLabels).toEqual([
    expect.objectContaining({ role: 'source', connected: true, color: '#65f7a4' }),
    expect.objectContaining({ role: 'terminal', connected: true, color: '#65f7a4' }),
  ]);
  await expect(page).toHaveScreenshot(`chain-complete-notice-${testInfo.project.name}.png`, {
    animations: 'disabled',
    maxDiffPixelRatio: 0.025,
  });
});

test('Cóc Bắt Mồi solid tongue model, rounded tip, and subtle impact remain visually stable', async ({ page }, testInfo) => {
  await page.evaluate(() => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__!;
    hooks.setState('skill-feedback');
    hooks.setPausedForScreenshot(true);
    hooks.advance(0.19);
  });
  await expect(page).toHaveScreenshot(`frog-tongue-${testInfo.project.name}.png`, {
    animations: 'disabled',
    maxDiffPixelRatio: 0.035,
  });
});

test('text-free frog skill tutorial shows the drag origin and both target previews', async ({ page }, testInfo) => {
  await page.evaluate(() => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__!;
    hooks.setState('mastery-ready');
    hooks.setPausedForScreenshot(true);
    hooks.startWave();
    hooks.advance(0.6);
  });
  const button = await page.locator('#soul-skill').boundingBox();
  const target = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.getSoulSkillTargetClientPoint());
  if (!button || !target) throw new Error('Missing Cóc Bắt Mồi preview endpoints');
  await expect(page.locator('#tutorial-hand')).toBeVisible();
  await expect(page.locator('#tutorial-hand')).toHaveAttribute('data-mode', 'drag');
  await page.mouse.move(button.x + button.width / 2, button.y + button.height / 2);
  await page.mouse.down();
  await page.mouse.move(target.x, target.y, { steps: 8 });
  await expect(page).toHaveScreenshot(`frog-tongue-preview-${testInfo.project.name}.png`, {
    animations: 'disabled',
    maxDiffPixelRatio: 0.035,
  });
  await page.mouse.up();
});

test('frog victory hop remains visually stable before the level fade', async ({ page }, testInfo) => {
  await page.evaluate(() => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__!;
    hooks.setState('victory-travel');
    hooks.setPausedForScreenshot(false);
    hooks.advance(1.2);
    hooks.setPausedForScreenshot(true);
  });
  await expect(page).toHaveScreenshot(`frog-victory-hop-${testInfo.project.name}.png`, {
    animations: 'disabled',
    maxDiffPixelRatio: 0.025,
  });
});

test('first elemental reaction explanation remains visually stable', async ({ page }, testInfo) => {
  await page.evaluate(() => {
    window.__THREE_GAME_TEST_HOOKS__!.setState('tutorial-reaction');
    window.__THREE_GAME_TEST_HOOKS__!.setPausedForScreenshot(false);
    window.__THREE_GAME_TEST_HOOKS__!.startWave();
    window.__THREE_GAME_TEST_HOOKS__!.advance(30);
  });
  await expect(page.locator('#reaction-tutorial-overlay')).toBeVisible();
  await expect(page).toHaveScreenshot(`reaction-tutorial-${testInfo.project.name}.png`, {
    animations: 'disabled',
    maxDiffPixelRatio: 0.025,
  });
});

test('enemy elemental tint and icon remain visually stable', async ({ page }, testInfo) => {
  await page.evaluate(() => {
    window.__THREE_GAME_TEST_HOOKS__!.setElementStatusDemo('fire');
    window.__THREE_GAME_TEST_HOOKS__!.setPausedForScreenshot(true);
  });
  expect((await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).statusIcons).toBe(1);
  await expect(page).toHaveScreenshot(`element-status-${testInfo.project.name}.png`, {
    animations: 'disabled',
    maxDiffPixelRatio: 0.025,
  });
});

test('unrestricted logical grid and footprint remain visually stable while dragging', async ({ page }, testInfo) => {
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.reset());
  const card = page.locator('.build-card[data-type="nexus"]');
  await card.scrollIntoViewIfNeeded();
  const box = await card.boundingBox();
  if (!box) throw new Error('Missing Trống Gọi Mưa build card');
  const target = await page.evaluate(() => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__!; const id = hooks.getGridCellIdAt(-7, 0);
    return id === null ? null : hooks.getSlotClientPoint(id);
  });
  if (!target) throw new Error('Missing unrestricted grid target');
  const source = { x: box.x + box.width * 0.7, y: box.y + box.height * 0.45 };
  if (testInfo.project.name.includes('mobile')) {
    const client = await page.context().newCDPSession(page);
    await client.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ ...source, id: 1 }] });
    for (let step = 1; step <= 8; step += 1) {
      await client.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: source.x + (target.x - source.x) * step / 8, y: source.y + (target.y - source.y) * step / 8, id: 1 }] });
    }
  } else {
    await page.mouse.move(source.x, source.y); await page.mouse.down(); await page.mouse.move(target.x, target.y, { steps: 10 });
  }
  await expect(page.locator('body')).toHaveClass(/is-build-dragging/);
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.gridCellCount as number).toBeGreaterThan(40);
  expect((snapshot.buildDrag as { dragging: boolean }).dragging).toBe(true);
  await expect(page).toHaveScreenshot(`placement-grid-${testInfo.project.name}.png`, {
    animations: 'disabled',
    maxDiffPixelRatio: 0.025,
  });
});

test('live drag-link guide remains clear from source tower to pointer', async ({ page }, testInfo) => {
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setLinkDragPointerWorld('generator', -7, 0));
  await expect(page.locator('body')).toHaveClass(/is-link-dragging/);
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.linkDrag).toMatchObject({ active: true, state: 'aiming' });
  expect((snapshot.linkDrag as { previewLength: number }).previewLength).toBeGreaterThan(2);
  await expect(page).toHaveScreenshot(`link-drag-guide-${testInfo.project.name}.png`, {
    animations: 'disabled',
    maxDiffPixelRatio: 0.006,
  });
});

test('orbited camera keeps the battlefield and HUD readable', async ({ page }, testInfo) => {
  const initial = (await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).camera as { yaw: number; pitch: number };
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
          { x: 78 + step * 8, y: 250 + step * 4, id: 1 },
          { x: 150 + step * 8, y: 250 + step * 4, id: 2 },
        ],
      });
    }
    await client.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  } else {
    await page.mouse.move(620, 330);
    await page.mouse.down({ button: 'right' });
    await page.mouse.move(760, 380, { steps: 8 });
    await page.mouse.up({ button: 'right' });
  }
  const orbited = (await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).camera as { yaw: number; pitch: number };
  expect(Math.abs(orbited.yaw - initial.yaw)).toBeGreaterThan(0.15);
  expect(Math.abs(orbited.pitch - initial.pitch)).toBeGreaterThan(0.08);
  await expect(page).toHaveScreenshot(`camera-orbit-${testInfo.project.name}.png`, {
    animations: 'disabled',
    maxDiffPixelRatio: 0.025,
  });
});
