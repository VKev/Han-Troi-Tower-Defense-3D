import { expect, test, type Page } from '@playwright/test';

async function boot(page: Page, level = 1): Promise<void> {
  await page.goto(`/?level=${level}`);
  await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setReducedMotion(false));
}

test('element towers use distinct authored models and the unchanged enemy route has layered trail art', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('element-models'));
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.pathPoints).toEqual([
    { x: -11, z: -2 }, { x: -3, z: -2 }, { x: -3, z: 2 }, { x: 7, z: 2 },
  ]);
  expect(snapshot.pathVisual).toMatchObject({
    layers: ['shoulder', 'raised-edge', 'textured-surface'],
    texture: 'dust-ruts-footprints-pebbles',
  });
  expect((snapshot.pathVisual as { edgeClodCount: number }).edgeClodCount).toBeGreaterThanOrEqual(16);
  expect(snapshot.elementalTowerModels).toEqual([
    { type: 'fire', model: 'wide-brazier' },
    { type: 'ice', model: 'asymmetric-crystal-crown' },
    { type: 'wind', model: 'broad-wind-rotor' },
    { type: 'earth', model: 'squat-stepped-monolith' },
  ]);
  const profiles = snapshot.towerModelProfiles as Array<{ type: string; profile: string; size: number[]; namedParts: string[] }>;
  expect(profiles.map(({ profile }) => profile)).toEqual([
    'wide-brazier', 'asymmetric-crystal-crown', 'broad-wind-rotor', 'squat-stepped-monolith',
  ]);
  expect(new Set(profiles.map(({ profile }) => profile)).size).toBe(4);
  expect(profiles.find(({ type }) => type === 'fire')?.namedParts).toContain('fireBrazier');
  expect(profiles.find(({ type }) => type === 'ice')?.namedParts).toContain('iceShardCrown');
  expect(profiles.find(({ type }) => type === 'wind')?.namedParts).toContain('windSpinner');
  expect(profiles.find(({ type }) => type === 'earth')?.namedParts).toContain('earthMonolith');
  expect(Math.max(...profiles.map(({ size }) => size[1])) - Math.min(...profiles.map(({ size }) => size[1]))).toBeGreaterThan(0.45);
  expect(snapshot.perimeterDecoration).toMatchObject({
    rocks: 34,
    grassPatches: 42,
    grassBlades: 168,
    twigs: 24,
    twigSegments: 48,
    deadTrees: 9,
    branches: 27,
    instancedGroups: 0,
    mergedMeshes: 1,
  });
  expect(snapshot.battlefieldDecoration).toMatchObject({ mergedMeshes: 1 });
  const battlefieldDecoration = snapshot.battlefieldDecoration as { rocks: number; grassPatches: number; twigs: number; deadTrees: number };
  expect(battlefieldDecoration.rocks + battlefieldDecoration.grassPatches + battlefieldDecoration.twigs + battlefieldDecoration.deadTrees).toBeGreaterThanOrEqual(14);
  expect(battlefieldDecoration.grassPatches).toBeGreaterThan(0);
  expect(battlefieldDecoration.twigs).toBeGreaterThan(0);
});

test('Cóc Bắt Mồi has a fast visible tongue, one-shot AOE, capture, and camera impact feedback', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => {
    const hooks = window.__THREE_GAME_TEST_HOOKS__!;
    hooks.setState('skill-feedback');
    hooks.setPausedForScreenshot(true);
    hooks.advance(0.19);
  });
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot).toMatchObject({ soulCasts: 1 });
  expect(snapshot.tongueSkill).toMatchObject({
    active: true, phase: 'impact', outboundDuration: 0.16, holdDuration: 0.08, retractDuration: 0.28,
    impactHits: 3, corridorHits: 1, capturedKills: 3, carrying: 3,
    presentation: {
      modelType: 'solid-3d-tapered', bodyMaterial: 'MeshStandardMaterial',
      tipGeometry: 'SphereGeometry', tipRadius: 0.68, tipHighlight: true,
      capturedEnemyScale: 0.62, usesGlow: false, glowMeshCount: 0,
      impactDiscOpacity: 0.06, dirtParticleCount: 14, activeDirtParticles: 14,
    },
  });
  expect((snapshot.tongueSkill as { cameraShakeRemaining: number }).cameraShakeRemaining).toBeGreaterThan(0);
});

test('frog hops backward along the enemy path and reset restores the fixed Nexus actor', async ({ page }) => {
  await boot(page);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('victory-travel'));
  const start = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(start).toMatchObject({ phase: 'victoryTravel' });
  expect((start.victoryTravel as { active: boolean }).active).toBe(true);
  const startPosition = (start.victoryTravel as { frogPosition: number[] }).frogPosition;

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.advance(0.37));
  const moving = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  const travel = moving.victoryTravel as { active: boolean; progress: number; hopHeight: number; maxHopHeight: number; frogPosition: number[]; destination: number[] };
  expect(travel.active).toBe(true);
  expect(travel.progress).toBeGreaterThan(0.03);
  expect(travel.progress).toBeLessThan(0.3);
  expect(travel.maxHopHeight).toBeGreaterThan(0.4);
  expect(Math.hypot(travel.frogPosition[0] - startPosition[0], travel.frogPosition[2] - startPosition[2])).toBeGreaterThan(0.6);
  expect(travel.destination).toEqual([-11, -2]);
  await expect(page.locator('#tutorial-hand')).toBeHidden();
  await expect(page.locator('#result-overlay')).toBeHidden();

  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.reset());
  const reset = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(reset).toMatchObject({ phase: 'preparation' });
  expect((reset.victoryTravel as { active: boolean }).active).toBe(false);
  const frog = (reset.victoryTravel as { frogPosition: number[] }).frogPosition;
  const nexus = (reset.fixedNexus as { position: number[] }).position;
  expect(frog[0]).toBeCloseTo(nexus[0], 4);
  expect(frog[2]).toBeCloseTo(nexus[2], 4);
  await expect(page.locator('body')).not.toHaveClass(/level-transitioning/);
});

test('victory travel automatically transitions Level 1 to 2 and Level 2 to 3', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name.includes('mobile'), 'Navigation logic is viewport-independent; mobile hop presentation is covered separately.');
  for (const [from, to] of [[1, 2], [2, 3]] as const) {
    await boot(page, from);
    await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('victory-transition'));
    await page.waitForURL((url) => url.searchParams.get('level') === String(to), { timeout: 5_000 });
    await page.waitForFunction(() => Boolean(window.__THREE_GAME_TEST_HOOKS__));
    const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
    expect(snapshot.stageIndex).toBe(to - 1);
  }
});

test('Level 3 completes on the final result only after the frog reaches the spawn entrance', async ({ page }, testInfo) => {
  test.skip(testInfo.project.name.includes('mobile'), 'Final campaign state is viewport-independent.');
  await boot(page, 3);
  await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.setState('victory-transition'));
  await page.waitForFunction(() => window.__THREE_GAME_DIAGNOSTICS__?.phase === 'won');
  const snapshot = await page.evaluate(() => window.__THREE_GAME_TEST_HOOKS__!.snapshot());
  expect(snapshot.stageIndex).toBe(2);
  expect((snapshot.victoryTravel as { progress: number; nextStage: number | null }).progress).toBeCloseTo(1, 3);
  expect((snapshot.victoryTravel as { nextStage: number | null }).nextStage).toBeNull();
  await expect(page.locator('#result-overlay')).toBeVisible();
});
