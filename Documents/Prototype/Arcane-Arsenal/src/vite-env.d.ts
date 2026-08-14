/// <reference types="vite/client" />

interface ThreeGameDiagnostics {
  frame: number;
  elapsed: number;
  phase: 'ready' | 'wave' | 'paused' | 'won' | 'lost';
  stage: number;
  wave: number;
  money: number;
  lives: number;
  towers: number;
  enemies: number;
  projectiles: number;
  infusions: number;
  projectileInterceptions: number;
  layerOneEnemyHits: number;
  reactions: number;
  elementalStatuses: number;
  tintedEnemies: number;
  statusIcons: number;
  impactParticles: number;
  draggingTower: boolean;
  pathRibbonMeshes: number;
  tutorialHandVisible: boolean;
  tutorialHandMode: string;
  maxTowerLayer: number;
  layerOneTowerCount: number;
  oppositeRaisedCellCount: number;
  oppositeRaisedTowerCount: number;
  maxLayerOneTowerLaneDistance: number;
  maxEnemyLayer: number;
  maxStageEnemyLayer: number;
  maxBoardLayer: number;
  maxEnemyFacingError: number;
  maxEnemyLaneOffset: number;
  upcomingEnemyCount: number;
  upcomingEnemyKinds: Array<'riftling' | 'runner' | 'brute' | 'wisp' | 'frostRay' | 'warder'>;
  selectedWaveEnemyKind: 'riftling' | 'runner' | 'brute' | 'wisp' | 'frostRay' | 'warder' | null;
  inspectedBuildType: 'foundry' | 'fire' | 'ice' | 'wind' | 'earth' | 'amplifier' | 'lance' | null;
  unlockedTowers: number;
  connections: number;
  linkGuideObjects: number;
  weaponAimGuideObjects: number;
  weaponAimGuideWidth: number;
  weaponAimGuideOpacity: number;
  selectedOutputAngle: number | null;
  tutorialHeadOnDot: number | null;
  blocked: number;
  tutorialStep: number;
  tutorialDirectShots: number;
  elementalTintStrength: number;
  stageStartingMoney: number;
  killRewardMultiplier: number;
  pathLength: number;
  waveCount: number;
  waveThreats: number[];
  requiredTutorialTower: 'amplifier' | 'lance' | null;
  lessonCell: { gx: number; gz: number } | null;
  objectiveProgress: number;
  renderer: {
    calls: number;
    triangles: number;
    geometries: number;
    textures: number;
  };
  canvas: {
    clientWidth: number;
    clientHeight: number;
    width: number;
    height: number;
    dpr: number;
  };
}

interface ThreeGameTestHooks {
  seed(value: number): void;
  setState(name: 'active-play' | 'stress' | 'stage-two-ready' | 'stage-two-wave-three' | 'stage-two-wave-four' | 'tutorial-rotation' | 'tutorial-ready' | 'tutorial-wave' | 'status-fire' | 'status-reaction' | 'reward-stage-one' | 'reward-stage-two' | 'fail' | 'win'): void;
  setPausedForScreenshot(paused: boolean): void;
  setReducedMotion(enabled: boolean): void;
  setSpeed(index: number): void;
  hideDebugUi(hidden: boolean): void;
  getCellClientPoint(gx: number, gz: number): { x: number; y: number } | null;
}

interface Window {
  __THREE_GAME_DIAGNOSTICS__?: ThreeGameDiagnostics;
  __THREE_GAME_TEST_HOOKS__?: ThreeGameTestHooks;
}
