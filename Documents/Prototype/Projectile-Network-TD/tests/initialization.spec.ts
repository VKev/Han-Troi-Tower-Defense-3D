import { expect, test } from '@playwright/test';

interface ColdFrame {
  readonly time: number;
  readonly hidden: boolean;
  readonly endX: number | null;
  readonly endY: number | null;
  readonly canvasBuffer: readonly [number, number];
}

test('first visible tutorial-hand frame already targets the stable authored cell', async ({ page }) => {
  const consoleErrors: string[] = [];
  const pageErrors: string[] = [];
  page.on('console', (message) => { if (message.type() === 'error') consoleErrors.push(message.text()); });
  page.on('pageerror', (error) => pageErrors.push(error.message));

  await page.addInitScript(() => {
    const frames: ColdFrame[] = [];
    Object.defineProperty(window, '__TUTORIAL_COLD_FRAMES__', { value: frames, configurable: true });
    const start = performance.now();
    const readPixel = (value: string): number | null => {
      const parsed = Number.parseFloat(value);
      return Number.isFinite(parsed) ? parsed : null;
    };
    const sample = (): void => {
      const hand = document.querySelector<HTMLElement>('#tutorial-hand');
      const canvas = document.querySelector<HTMLCanvasElement>('#game-canvas');
      if (hand && canvas) {
        const style = getComputedStyle(hand);
        frames.push({
          time: performance.now() - start,
          hidden: hand.classList.contains('hidden'),
          endX: readPixel(style.getPropertyValue('--hand-end-x')),
          endY: readPixel(style.getPropertyValue('--hand-end-y')),
          canvasBuffer: [canvas.width, canvas.height],
        });
      }
      if (performance.now() - start < 1_200) requestAnimationFrame(sample);
    };
    requestAnimationFrame(sample);
  });

  await page.goto('/', { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await page.waitForTimeout(1_250);

  const evidence = await page.evaluate(() => {
    const frames = (window as typeof window & { __TUTORIAL_COLD_FRAMES__?: ColdFrame[] }).__TUTORIAL_COLD_FRAMES__ ?? [];
    const visible = frames.filter((frame) => !frame.hidden && frame.endX !== null && frame.endY !== null);
    const hand = document.querySelector<HTMLElement>('#tutorial-hand');
    const handStyle = hand ? getComputedStyle(hand) : null;
    const stable = handStyle ? {
      endX: Number.parseFloat(handStyle.getPropertyValue('--hand-end-x')),
      endY: Number.parseFloat(handStyle.getPropertyValue('--hand-end-y')),
    } : null;
    const slotId = window.__THREE_GAME_TEST_HOOKS__!.getGridCellIdAt(-1, 4);
    const authored = slotId === null ? null : window.__THREE_GAME_TEST_HOOKS__!.getSlotClientPoint(slotId);
    return { frames, visible, stable, authored };
  });

  expect(evidence.frames.some((frame) => frame.hidden && frame.canvasBuffer[0] === 300 && frame.canvasBuffer[1] === 150)).toBe(true);
  expect(evidence.authored).not.toBeNull();
  expect(evidence.visible.length).toBeGreaterThanOrEqual(1);
  const first = evidence.visible[0];
  expect(evidence.stable).not.toBeNull();
  expect(Math.hypot(first.endX! - evidence.stable!.endX, first.endY! - evidence.stable!.endY)).toBeLessThan(1.5);
  expect(first.endX).toBeGreaterThan(26);
  expect(first.endY).toBeLessThan(page.viewportSize()!.height - 58);
  expect(consoleErrors).toEqual([]);
  expect(pageErrors).toEqual([]);
});

test('Level 1 opens in the approved high oblique camera composition', async ({ page }) => {
  await page.goto('/');
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  const camera = (await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot())).camera as {
    yaw: number; pitch: number; distance: number; target: number[];
  };
  expect(camera.yaw).toBeGreaterThan(1.85);
  expect(camera.yaw).toBeLessThan(2.1);
  expect(camera.pitch).toBeGreaterThan(0.72);
  expect(camera.pitch).toBeLessThan(0.86);
  expect(camera.distance).toBeGreaterThan(32);
  expect(camera.distance).toBeLessThan(34.5);
  expect(camera.target).toEqual([-1, 1.2, -0.4]);
});
