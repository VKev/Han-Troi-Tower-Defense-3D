import { expect, test, type Page } from '@playwright/test';

type LessonType = 'support' | 'special';

async function boot(page: Page): Promise<void> {
  await page.goto('/?level=2');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setReducedMotion(true));
}

async function touchDrag(page: Page, source: { x: number; y: number }, target: { x: number; y: number }): Promise<void> {
  const client = await page.context().newCDPSession(page);
  await client.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ ...source, id: 1 }] });
  for (let step = 1; step <= 8; step += 1) {
    await client.send('Input.dispatchTouchEvent', {
      type: 'touchMove',
      touchPoints: [{ x: source.x + (target.x - source.x) * step / 8, y: source.y + (target.y - source.y) * step / 8, id: 1 }],
    });
  }
  await client.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
}

async function drag(page: Page, source: { x: number; y: number }, target: { x: number; y: number }, mobile: boolean): Promise<void> {
  if (mobile) {
    await touchDrag(page, source, target);
    return;
  }
  await page.mouse.move(source.x, source.y);
  await page.mouse.down();
  await page.mouse.move(target.x, target.y, { steps: 8 });
  await page.mouse.up();
}

async function nodePoint(page: Page, type: string, slotId?: number): Promise<{ x: number; y: number }> {
  const point = await page.evaluate(({ nodeType, nodeSlotId }) => window.__THREE_GAME_TEST_HOOKS__!.getNodeClientPoint(nodeType, nodeSlotId), { nodeType: type, nodeSlotId: slotId });
  if (!point) throw new Error(`Missing node point for ${type} at ${slotId ?? 'any slot'}`);
  return point;
}

async function placeLessonNode(page: Page, type: LessonType, mobile: boolean): Promise<number> {
  const state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  const slotId = state.stageTwoLessonSlotId as number | null;
  if (slotId === null) throw new Error(`Missing lesson slot for ${type}`);
  const target = await page.evaluate((id) => window.__THREE_GAME_TEST_HOOKS__!.getSlotClientPoint(id), slotId);
  const card = page.locator(`.build-card[data-type="${type}"]`);
  await card.scrollIntoViewIfNeeded();
  const box = await card.boundingBox();
  if (!box || !target) throw new Error(`Missing drag endpoints for ${type}`);
  await drag(page, { x: box.x + box.width * 0.7, y: box.y + box.height * 0.45 }, target, mobile);
  return slotId;
}

async function linkNodes(page: Page, sourceType: string, targetType: string, mobile: boolean, sourceSlotId?: number, targetSlotId?: number): Promise<void> {
  const source = await nodePoint(page, sourceType, sourceSlotId);
  const target = await nodePoint(page, targetType, targetSlotId);
  await drag(page, source, target, mobile);
}

test('Level 2 teaches free Hỗ Trợ on Wave 3 and Trụ Sấm on Wave 4', async ({ page }, testInfo) => {
  const mobile = testInfo.project.name.includes('mobile');
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await boot(page);
  await expect(page.locator('.build-card[data-type="support"]')).toBeDisabled();
  await expect(page.locator('.build-card[data-type="special"]')).toBeDisabled();

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('stage-two-wave-three'));
  await expect(page.locator('#tutorial-hand')).toBeVisible();
  await expect(page.locator('#tutorial-hand')).toHaveAttribute('data-mode', 'drag');
  let state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state).toMatchObject({
    waveIndex: 2, gold: 45, requiredStageTwoNode: 'support', tutorialObjective: 'place-support',
    tutorialHandMode: 'drag', stageTwoHighGroundGrantedTypes: [], stageTwoHighGroundActive: false,
  });
  expect(state.stageTwoHighGroundPlan).toBeNull();
  expect(state.highGroundPlatformCount).toBe(0);
  expect(state.waveLayerOneEnemyCounts).toEqual([0, 0, 0, 0, 0, 0]);
  const priceMultiplierBefore = state.nodePurchasePriceMultiplier as number;
  await expect(page.locator('.build-card[data-type="support"]')).toBeEnabled();
  await expect(page.locator('.build-card[data-type="support"]')).toHaveAttribute('data-lesson-free', 'true');
  await expect(page.locator('.build-card[data-type="support"] .build-cost')).toHaveText('0');
  await expect(page.locator('.build-card[data-type="special"]')).toBeDisabled();
  await expect(page.locator('#start-wave')).toBeDisabled();

  const supportSlot = await placeLessonNode(page, 'support', mobile);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot().tutorialObjective)).toBe('link-chain-support');
  await linkNodes(page, 'ice', 'support', mobile, undefined, supportSlot);
  await expect.poll(() => page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot().tutorialObjective)).toBe('link-support-nexus');
  await linkNodes(page, 'support', 'nexus', mobile, supportSlot);
  state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state).toMatchObject({
    gold: 45, stageTwoLessonComplete: true, stageTwoHighGroundActive: false,
    stageTwoHighGroundGrantedTypes: [], tutorialObjective: 'start-wave-3',
  });
  expect(state.nodePurchasePriceMultiplier).toBe(priceMultiplierBefore);
  expect((state.nodes as Array<{ type: string; lessonGrant: boolean; totalInvested: number }>).find((node) => node.type === 'support')).toMatchObject({ lessonGrant: true, totalInvested: 0 });
  await expect(page.locator('#start-wave')).toBeEnabled();
  await expect(page.locator('#start-wave')).toHaveClass(/tutorial-focus/);

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('stage-two-wave-four'));
  state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state).toMatchObject({ waveIndex: 3, gold: 55, requiredStageTwoNode: 'special', tutorialObjective: 'place-special', stageTwoHighGroundActive: false });
  expect(state.stageTwoLessonSlotLaneDistance as number).toBeLessThanOrEqual(3.2);
  await expect(page.locator('.build-card[data-type="special"]')).toBeEnabled();
  await expect(page.locator('.build-card[data-type="special"]')).toHaveAttribute('data-lesson-free', 'true');
  await expect(page.locator('.build-card[data-type="special"] .build-cost')).toHaveText('0');
  const specialSlot = await placeLessonNode(page, 'special', mobile);
  await linkNodes(page, 'support', 'special', mobile, undefined, specialSlot);
  state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state.tutorialObjective, JSON.stringify({ selectedNodeId: state.selectedNodeId, linkDrag: state.linkDrag, links: state.links, nodes: state.nodes })).toBe('link-special-nexus');
  await linkNodes(page, 'special', 'nexus', mobile, specialSlot);
  state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state).toMatchObject({ gold: 55, stageTwoLessonComplete: true, tutorialObjective: 'start-wave-4' });
  expect((state.nodes as Array<{ type: string; lessonGrant: boolean; totalInvested: number }>).find((node) => node.type === 'special')).toMatchObject({ lessonGrant: true, totalInvested: 0 });
  await expect(page.locator('#start-wave')).toBeEnabled();
  expect(errors).toEqual([]);
});

test('the free lesson tower highlights Nâng cấp until upgraded, and an unaffordable branch still reports Không đủ Vàng', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('stage-two-wave-four'));
  const state = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(state.gold).toBe(55);
  const support = (state.nodes as Array<{ type: string; slotId: number; branch: string | null; stageTwoLessonType: string | null }>)
    .find((node) => node.type === 'support');
  expect(support).toMatchObject({ branch: null, stageTwoLessonType: 'support' });

  const point = await nodePoint(page, 'support', support!.slotId);
  await page.mouse.click(point.x, point.y);
  await expect(page.locator('#branch-controls')).toHaveClass(/tutorial-focus/);
  await expect(page.locator('#action-upgrade')).toHaveClass(/tutorial-focus/);
  await expect(page.locator('#branch-a')).toBeEnabled();
  await expect(page.locator('#branch-a')).toHaveClass(/unaffordable/);

  await page.locator('#branch-a').click();
  await expect(page.locator('#toast')).not.toHaveClass(/hidden/);
  await expect(page.locator('#toast')).toContainText('Không đủ Vàng');
  const afterFailedClick = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(afterFailedClick.gold).toBe(55);
  expect((afterFailedClick.nodes as Array<{ type: string; branch: string | null }>).find((n) => n.type === 'support')?.branch).toBeNull();
});
