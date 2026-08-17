/// <reference types="vite/client" />

interface ThreeGameTestHooks {
  snapshot(): Record<string, unknown>;
  reset(): void;
  seed(seed: number): void;
  setReducedMotion(enabled: boolean): void;
  setPausedForScreenshot(paused: boolean): void;
  setState(name: 'mastery-ready' | 'mastery-two-leaks' | 'mastery-fail' | 'mastery-baseline-final' | 'mastery-expanded-final' | 'tutorial-reaction' | 'intro-currency' | 'intro-nexus' | 'stage-two-wave-three' | 'stage-two-wave-four' | 'skill-feedback' | 'soul-field-damage-demo' | 'element-models' | 'victory-travel' | 'victory-transition' | 'dual-terminal-network' | 'reaction-cooldown' | 'chain-complete-notice' | 'broken-chain-labels'): void;
  autoBuildMinimumChain(): void;
  startWave(): void;
  dismissReactionTutorial(): void;
  procTestReaction(reaction: 'hellfire' | 'deepFreeze' | 'tempest' | 'shatter' | 'firestorm' | 'sandstorm' | 'permafrost' | 'steamBurst'): void;
  setSoul(value: number): void;
  creditRainChargeHits(hitCount: number): void;
  setElementStatusDemo(element: 'fire' | 'ice' | 'wind' | 'earth'): void;
  setLinkDragPointerWorld(sourceType: string, x: number, z: number): void;
  advance(seconds: number): void;
  getSlotClientPoint(slotId: number): { x: number; y: number } | null;
  getGridCellIdAt(x: number, z: number, tier?: 'low' | 'high'): number | null;
  getNodeClientPoint(type: string, slotId?: number): { x: number; y: number } | null;
  getSoulSkillTargetClientPoint(): { x: number; y: number } | null;
}

interface Window {
  __THREE_GAME_DIAGNOSTICS__?: Record<string, unknown>;
  __THREE_GAME_TEST_HOOKS__?: ThreeGameTestHooks;
}
