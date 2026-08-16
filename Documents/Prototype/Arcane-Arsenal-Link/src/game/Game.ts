import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { RoomEnvironment } from 'three/addons/environments/RoomEnvironment.js';
import { ArtFactory } from '../assets/ArtFactory';
import { MaterialLibrary } from '../assets/MaterialLibrary';
import { Loop } from '../core/Loop';
import { createRenderer, resizeRenderer } from '../core/Renderer';
import { AudioSystem } from '../systems/AudioSystem';
import { createSeededRandom } from '../utils/random';
import {
  CELL_SIZE,
  ELEMENT_COLORS,
  ELEMENT_NAMES,
  ENEMY_DEFINITIONS,
  ENEMY_REWARD_MULTIPLIER,
  FIXED_STEP,
  LAYER_HEIGHTS,
  MAX_TOWER_LEVEL,
  REACTION_PAIRS,
  SELL_REFUND,
  STAGES,
  STARTING_LIVES,
  STARTING_MONEY,
  TOWER_DEFINITIONS,
  WAVE_CLEAR_REWARD_MULTIPLIER,
  gridKey,
  isAmmoEmitter,
  isAmmoReceiver,
  uniqueElements,
  type Element,
  type EnemyKind,
  type EnemyState,
  type GamePhase,
  type InteractionMode,
  type ProjectileState,
  type Round,
  type TowerState,
  type TowerType,
} from './definitions';
import { ROUTING_MODE } from './routingMode';

interface CellState {
  readonly gx: number;
  readonly gz: number;
  readonly layer: 0 | 1 | 2;
  readonly buildable: boolean;
}

interface BlockerState {
  readonly layer: 0 | 1 | 2;
  readonly minX: number;
  readonly maxX: number;
  readonly minZ: number;
  readonly maxZ: number;
}

interface VfxState {
  readonly object: THREE.Object3D;
  readonly maxLife: number;
  readonly rises: boolean;
  readonly scales?: boolean;
  life: number;
}

interface ToastState {
  text: string;
  tone: 'info' | 'good' | 'bad' | 'reaction';
}

interface BuildDragState {
  readonly pointerId: number;
  readonly type: TowerType;
  readonly button: HTMLButtonElement;
  readonly origin: THREE.Vector2;
  dragging: boolean;
  cell: CellState | null;
  valid: boolean;
  reason: string;
}

interface ImpactParticleState {
  readonly position: THREE.Vector3;
  readonly velocity: THREE.Vector3;
  readonly color: THREE.Color;
  readonly maxLife: number;
  life: number;
}

type DiscoveryCueKind = 'currency' | 'reaction' | 'nexus';

interface DiscoveryCueRequest {
  readonly kind: DiscoveryCueKind;
  readonly html: string;
  readonly targetSelector?: string;
  readonly worldPosition?: THREE.Vector3;
  readonly highlightOnly: boolean;
  readonly duration: number;
}

interface MasteryTowerSnapshot {
  readonly id: number;
  readonly type: TowerType;
  readonly gx: number;
  readonly gz: number;
  readonly level: number;
  readonly totalInvested: number;
  readonly buffer: readonly { readonly id: number; readonly damage: number; readonly elements: readonly Element[] }[];
  readonly outputTargetId: number | null;
  readonly aimAngle: number;
  readonly produceTimer: number;
  readonly outputTimer: number;
  readonly skillTimer: number;
  readonly amplifierBranch: TowerState['amplifierBranch'];
}

interface MasteryCheckpoint {
  readonly money: number;
  readonly towers: readonly MasteryTowerSnapshot[];
  readonly nextTowerId: number;
  readonly nextRoundId: number;
  readonly discoveredCues: readonly DiscoveryCueKind[];
}

const BUILD_ORDER: readonly TowerType[] = ['foundry', 'fire', 'ice', 'wind', 'earth', 'amplifier', 'lance'];
const BUILD_GROUPS: readonly { label: string; types: readonly TowerType[] }[] = [
  { label: 'Trụ sinh đạn', types: ['foundry'] },
  { label: 'Hỗ trợ đạn', types: ['fire', 'ice', 'wind', 'earth'] },
  { label: 'Hỗ trợ trụ', types: ['amplifier'] },
  { label: 'Trụ đặc biệt', types: ['lance'] },
];
const TUTORIAL_STEP_COUNT = 16;
const TUTORIAL_TOWER_CELLS = {
  foundry: { gx: 2, gz: 1 },
  fire: { gx: 4, gz: 5 },
  ice: { gx: 2, gz: 0 },
  terminalFire: { gx: 2, gz: 3 },
} as const;
const TUTORIAL_WAVE_START_STEPS = [5, 10, 15] as const;
const ROTATION_TUTORIAL_WAVE_START_STEPS = [4, 8, 14] as const;
const TUTORIAL_REACTION_POPUP_DELAY = 0.9;
const MAX_ENEMY_LANE_OFFSET = 0.55;
const GAME_SPEEDS = [1, 2] as const;
const ELEMENT_STATUS_TINT = 0.94;
const MULTI_ELEMENT_STATUS_TINT = 0.97;
const ELEMENT_STATUS_EMISSIVE_BOOST = 0.9;
const PROJECTILE_SPEED_MULTIPLIER = 3;
const TOWER_FIRE_RATE_MULTIPLIER = 1.5;
const PROJECTILE_COLLISION_RADIUS = 0.84;
const PROJECTILE_VISUAL_SCALE = 2;
const ENEMY_SPEED_MULTIPLIER = 0.6;
const REACTION_MAX_HP_DAMAGE_RATIO = 0.06;
const ROTATION_SPEED = THREE.MathUtils.degToRad(105);
const SELECTED_AIM_GUIDE_RADIUS = 0.13;
const SELECTED_AIM_GUIDE_OPACITY = 0.38;
const LANCE_AMMO_BAR_WIDTH = 1.62;
const EXPLOSION_RADIUS = CELL_SIZE;
const ENEMY_GLYPHS: Readonly<Record<EnemyKind, string>> = {
  riftling: '◆',
  runner: '➤',
  brute: '⬢',
  wisp: '✦',
  frostRay: '◇',
  warder: '⬡',
  arcaneBulwark: '▣',
  skyWarder: '✧',
  colossus: '⬣',
};

function pointSegmentDistance(point: THREE.Vector2, start: THREE.Vector2, end: THREE.Vector2): number {
  const segment = end.clone().sub(start);
  const lengthSq = segment.lengthSq();
  if (lengthSq === 0) return point.distanceTo(start);
  const t = THREE.MathUtils.clamp(point.clone().sub(start).dot(segment) / lengthSq, 0, 1);
  return point.distanceTo(start.clone().addScaledVector(segment, t));
}

function segmentAabbEntry(
  start: THREE.Vector3,
  end: THREE.Vector3,
  blocker: BlockerState,
): number | null {
  const dx = end.x - start.x;
  const dz = end.z - start.z;
  let tMin = 0;
  let tMax = 1;
  const axes: readonly [number, number, number, number][] = [
    [start.x, dx, blocker.minX, blocker.maxX],
    [start.z, dz, blocker.minZ, blocker.maxZ],
  ];
  for (const [origin, direction, min, max] of axes) {
    if (Math.abs(direction) < 0.00001) {
      if (origin < min || origin > max) return null;
      continue;
    }
    const inverse = 1 / direction;
    let near = (min - origin) * inverse;
    let far = (max - origin) * inverse;
    if (near > far) [near, far] = [far, near];
    tMin = Math.max(tMin, near);
    tMax = Math.min(tMax, far);
    if (tMin > tMax) return null;
  }
  return tMin >= 0 && tMin <= 1 ? tMin : null;
}

function segmentSphereEntry(start: THREE.Vector3, end: THREE.Vector3, center: THREE.Vector3, radius: number): number | null {
  const direction = end.clone().sub(start);
  const fromCenter = start.clone().sub(center);
  const a = direction.lengthSq();
  if (a <= 0.000001) return fromCenter.lengthSq() <= radius * radius ? 0 : null;
  const c = fromCenter.lengthSq() - radius * radius;
  if (c <= 0) return 0;
  const b = 2 * fromCenter.dot(direction);
  const discriminant = b * b - 4 * a * c;
  if (discriminant < 0) return null;
  const entry = (-b - Math.sqrt(discriminant)) / (2 * a);
  return entry >= 0 && entry <= 1 ? entry : null;
}

function elementList(elements: readonly Element[]): string {
  return elements.length === 0 ? 'Trung tính' : elements.map((element) => ELEMENT_NAMES[element]).join(' + ');
}

export class Game {
  private readonly renderer: THREE.WebGLRenderer;
  private readonly scene = new THREE.Scene();
  private readonly camera = new THREE.PerspectiveCamera(44, 1, 0.1, 300);
  private readonly controls: OrbitControls;
  private readonly loop = new Loop((delta, elapsed) => this.update(delta, elapsed), () => this.render());
  private readonly materials = new MaterialLibrary();
  private readonly art = new ArtFactory(this.materials);
  private readonly audio = new AudioSystem();
  private readonly raycaster = new THREE.Raycaster();
  private readonly pointer = new THREE.Vector2();
  private readonly boardGroup = new THREE.Group();
  private readonly networkGroup = new THREE.Group();
  private readonly selectionGroup = new THREE.Group();
  private readonly placementPreviewGroup = new THREE.Group();
  private readonly tutorialCueGroup = new THREE.Group();
  private readonly statusIconGroup = new THREE.Group();
  private readonly effectsGroup = new THREE.Group();
  private readonly towerPickables: THREE.Object3D[] = [];
  private readonly cellPickables: THREE.Object3D[] = [];
  private readonly cells = new Map<string, CellState>();
  private readonly occupied = new Map<string, number>();
  private readonly blockers: BlockerState[] = [];
  private readonly towers: TowerState[] = [];
  private readonly enemies: EnemyState[] = [];
  private readonly projectiles: ProjectileState[] = [];
  private readonly effects: VfxState[] = [];
  private readonly statusIconMeshes = new Map<Element, THREE.InstancedMesh>();
  private readonly statusIconBackdrop: THREE.InstancedMesh;
  private readonly impactParticles: THREE.Points;
  private readonly impactParticleStates: ImpactParticleState[] = [];
  private readonly nexus: THREE.Group;
  private readonly buildList = this.getElement('#build-list');
  private readonly toastElement = this.getElement('#toast');
  private readonly inspectorElement = this.getElement('#tower-inspector');
  private readonly resultElement = this.getElement('#result-overlay');
  private readonly tutorialHandElement = this.getElement('#tutorial-hand');
  private readonly discoveryCueElement = this.getElement('#discovery-cue');
  private readonly discoveryCardElement = this.getElement('#discovery-card');
  private readonly reactionTutorialElement = this.getElement('#reaction-tutorial-overlay');
  private readonly pointerStart = new THREE.Vector2();

  private phase: GamePhase = 'ready';
  private interactionMode: InteractionMode = 'inspect';
  private selectedBuildType: TowerType | null = null;
  private selectedTowerId: number | null = null;
  private inspectedBuildType: TowerType | null = null;
  private selectedWaveEnemyKind: EnemyKind | null = null;
  private hoveredWaveEnemyKind: EnemyKind | null = null;
  private stageIndex = 0;
  private pathXZ = STAGES[0].path.map(([x, z]) => new THREE.Vector2(x, z));
  private pathSegmentLengths = this.pathXZ.slice(1).map((point, index) => point.distanceTo(this.pathXZ[index]));
  private pathTotalLength = this.pathSegmentLengths.reduce((sum, length) => sum + length, 0);
  private tutorialStep = 0;
  private reactionTutorialPopupDelay = -1;
  private reactionTutorialPopupVisible = false;
  private linkSourceTowerId: number | null = null;
  private linkDragPointerId: number | null = null;
  private linkDragSourceTowerId: number | null = null;
  private rotationPointerId: number | null = null;
  private rotationPointerDirection: -1 | 0 | 1 = 0;
  private readonly heldRotationKeys = new Set<'q' | 'e'>();
  private lastLinkAttempt: { sourceId: number; targetId: number; result: string } | null = null;
  private linkedProjectileLaunches = 0;
  private unlinkedProjectileLaunches = 0;
  private terminalBuffProjectileLaunches = 0;
  private linkedSegmentEnemyHits = 0;
  private linkedSegmentDamage = 0;
  private readonly projectileLaunchesByTower = new Map<number, number>();
  private stageTwoAmplifierIntroduced = false;
  private stageTwoLanceIntroduced = false;
  private stageTwoLanceFeederIntroduced = false;
  private stageTwoRotationLessonPair: { lance: { gx: number; gz: number }; feeder: { gx: number; gz: number } } | null = null;
  private readonly stageTwoLessonCells = new Map<'foundry' | 'amplifier' | 'lance', { gx: number; gz: number }>();
  private stageCleared = false;
  private money = STARTING_MONEY;
  private lives = STARTING_LIVES;
  private waveIndex = 0;
  private waveElapsed = 0;
  private spawnCursor = 0;
  private fixedAccumulator = 0;
  private elapsed = 0;
  private uiTimer = 0;
  private frame = 0;
  private nextTowerId = 1;
  private nextEnemyId = 1;
  private nextRoundId = 1;
  private nextProjectileId = 1;
  private infusionCount = 0;
  private projectileInterceptionCount = 0;
  private layerOneEnemyHitCount = 0;
  private reactionCount = 0;
  private lastReactionBonusDamage = 0;
  private impactParticleBursts = 0;
  private lanceVfxMaxAnchorError = 0;
  private lanceVfxMaxScaleError = 0;
  private lastExplosionHitCount = 0;
  private lastExplosionDamage = 0;
  private lastExplosionOutsideDamage = 0;
  private lastExplosionOtherLayerDamage = 0;
  private lastExplosionTargetCueCount = 0;
  private speedIndex = 0;
  private rng = createSeededRandom(20260814);
  private pausedForScreenshot = false;
  private reducedMotion = false;
  private lastToast: ToastState = { text: '', tone: 'info' };
  private pointerMoved = false;
  private buildDrag: BuildDragState | null = null;
  private placementPreviewKey = '';
  private tutorialCueKey = '';
  private discoveryCue: DiscoveryCueRequest | null = null;
  private discoveryCueElapsed = 0;
  private readonly discoveryCueQueue: DiscoveryCueRequest[] = [];
  private readonly discoveredCues = new Set<DiscoveryCueKind>();
  private readonly discoveryCueTriggerCounts: Record<DiscoveryCueKind, number> = { currency: 0, reaction: 0, nexus: 0 };
  private waveIntelRosterKey = '';
  private waveIntelDetailKey = '';
  private masteryCheckpoint: MasteryCheckpoint | null = null;
  private suppressBuildClickUntil = 0;
  private readonly onKeyDownBound = (event: KeyboardEvent) => this.onKeyDown(event);
  private readonly onKeyUpBound = (event: KeyboardEvent) => this.onKeyUp(event);
  private readonly onVisibilityBound = () => this.onVisibilityChange();
  private readonly onBuildPointerDownBound = (event: PointerEvent) => this.onBuildPointerDown(event);
  private readonly onBuildPointerMoveBound = (event: PointerEvent) => this.onBuildPointerMove(event);
  private readonly onBuildPointerUpBound = (event: PointerEvent) => this.onBuildPointerUp(event);
  private readonly onBuildPointerCancelBound = (event: PointerEvent) => this.onBuildPointerCancel(event);
  private readonly onWindowBlurBound = () => {
    this.cancelBuildDrag(false);
    this.stopHeldRotation();
  };

  constructor(private readonly canvas: HTMLCanvasElement) {
    document.documentElement.dataset.routing = ROUTING_MODE;
    this.renderer = createRenderer(canvas);
    this.statusIconBackdrop = this.createStatusIconBackdrop();
    this.impactParticles = this.createImpactParticlePool();
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5));
    this.renderer.shadowMap.type = THREE.PCFShadowMap;
    this.renderer.toneMappingExposure = 1.08;
    this.scene.add(
      this.boardGroup,
      this.networkGroup,
      this.selectionGroup,
      this.placementPreviewGroup,
      this.tutorialCueGroup,
      this.statusIconGroup,
      this.effectsGroup,
    );
    this.createStatusIconMeshes();
    this.statusIconGroup.add(this.statusIconBackdrop);
    this.effectsGroup.add(this.impactParticles);

    const pmrem = new THREE.PMREMGenerator(this.renderer);
    this.scene.environment = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
    this.scene.environmentIntensity = 0.9;
    pmrem.dispose();

    this.camera.position.set(19, 22, 21);
    this.controls = new OrbitControls(this.camera, this.canvas);
    this.controls.target.set(0, 1.7, -0.8);
    this.controls.enableDamping = true;
    this.controls.dampingFactor = 0.08;
    this.controls.minDistance = 14;
    this.controls.maxDistance = 48;
    this.controls.minPolarAngle = 0.52;
    this.controls.maxPolarAngle = 1.18;
    this.controls.maxTargetRadius = 12;
    this.controls.mouseButtons.LEFT = THREE.MOUSE.PAN;
    this.controls.mouseButtons.MIDDLE = THREE.MOUSE.DOLLY;
    this.controls.mouseButtons.RIGHT = THREE.MOUSE.ROTATE;
    this.controls.touches.ONE = THREE.TOUCH.PAN;
    this.controls.touches.TWO = THREE.TOUCH.DOLLY_ROTATE;
    this.controls.update();

    this.createLightingAndSky();
    this.createBattlefield();
    this.nexus = this.art.createNexus();
    this.positionNexus();
    this.scene.add(this.nexus);
    this.createBuildButtons();
    this.installUiEvents();
    this.installInput();
    this.installTestHooks();
    this.resetRun();
    resizeRenderer(this.renderer, this.camera, 1.5);
    this.updateUi(true);
    this.publishDiagnostics();
  }

  start(): void {
    this.loop.start();
  }

  dispose(): void {
    this.loop.stop();
    for (const tower of this.towers) this.disposeLanceAmmoBar(tower);
    this.controls.dispose();
    this.audio.dispose();
    this.art.dispose();
    this.materials.dispose();
    window.removeEventListener('keydown', this.onKeyDownBound);
    window.removeEventListener('keyup', this.onKeyUpBound);
    window.removeEventListener('pointermove', this.onBuildPointerMoveBound);
    window.removeEventListener('pointerup', this.onBuildPointerUpBound);
    window.removeEventListener('pointercancel', this.onBuildPointerCancelBound);
    window.removeEventListener('blur', this.onWindowBlurBound);
    document.removeEventListener('visibilitychange', this.onVisibilityBound);
    this.buildList.removeEventListener('pointerdown', this.onBuildPointerDownBound);
    this.clearPlacementPreview();
    this.clearTutorialCue();
    this.hideDiscoveryCue();
    this.disposeStatusIconMeshes();
    this.impactParticles.geometry.dispose();
    (this.impactParticles.material as THREE.Material).dispose();
    this.canvas.replaceWith(this.canvas.cloneNode(true));
    this.renderer.dispose();
    window.__THREE_GAME_DIAGNOSTICS__ = undefined;
    window.__THREE_GAME_TEST_HOOKS__ = undefined;
  }

  private createStatusIconBackdrop(): THREE.InstancedMesh {
    const mesh = new THREE.InstancedMesh(
      new THREE.CircleGeometry(0.25, 18),
      new THREE.MeshBasicMaterial({ color: 0x102033, transparent: true, opacity: 0.72, depthWrite: false }),
      96,
    );
    mesh.count = 0;
    mesh.frustumCulled = false;
    mesh.renderOrder = 7;
    return mesh;
  }

  private createElementIconGeometry(element: Element): THREE.ShapeGeometry {
    const shape = new THREE.Shape();
    if (element === 'fire') {
      shape.moveTo(0, 0.27);
      shape.bezierCurveTo(0.18, 0.12, 0.24, -0.05, 0.1, -0.24);
      shape.bezierCurveTo(0.02, -0.1, -0.08, -0.07, -0.12, -0.23);
      shape.bezierCurveTo(-0.3, -0.02, -0.2, 0.16, 0, 0.27);
    } else if (element === 'ice') {
      for (let index = 0; index < 12; index += 1) {
        const angle = Math.PI / 2 + index / 12 * Math.PI * 2;
        const radius = index % 2 === 0 ? 0.27 : 0.1;
        const x = Math.cos(angle) * radius;
        const y = Math.sin(angle) * radius;
        if (index === 0) shape.moveTo(x, y);
        else shape.lineTo(x, y);
      }
      shape.closePath();
    } else if (element === 'wind') {
      shape.moveTo(0.28, 0);
      shape.lineTo(-0.14, 0.23);
      shape.lineTo(-0.05, 0.06);
      shape.lineTo(-0.28, 0.06);
      shape.lineTo(-0.28, -0.06);
      shape.lineTo(-0.05, -0.06);
      shape.lineTo(-0.14, -0.23);
      shape.closePath();
    } else {
      for (let index = 0; index < 6; index += 1) {
        const angle = Math.PI / 6 + index / 6 * Math.PI * 2;
        const x = Math.cos(angle) * 0.25;
        const y = Math.sin(angle) * 0.25;
        if (index === 0) shape.moveTo(x, y);
        else shape.lineTo(x, y);
      }
      shape.closePath();
    }
    return new THREE.ShapeGeometry(shape, 1);
  }

  private createStatusIconMeshes(): void {
    for (const element of ['fire', 'ice', 'wind', 'earth'] as const) {
      const mesh = new THREE.InstancedMesh(
        this.createElementIconGeometry(element),
        new THREE.MeshBasicMaterial({
          color: ELEMENT_COLORS[element],
          transparent: true,
          opacity: 0.98,
          depthWrite: false,
          toneMapped: false,
        }),
        24,
      );
      mesh.count = 0;
      mesh.frustumCulled = false;
      mesh.renderOrder = 8;
      this.statusIconMeshes.set(element, mesh);
      this.statusIconGroup.add(mesh);
    }
  }

  private disposeStatusIconMeshes(): void {
    this.statusIconBackdrop.geometry.dispose();
    (this.statusIconBackdrop.material as THREE.Material).dispose();
    for (const mesh of this.statusIconMeshes.values()) {
      mesh.geometry.dispose();
      (mesh.material as THREE.Material).dispose();
    }
    this.statusIconMeshes.clear();
  }

  private createImpactParticlePool(): THREE.Points {
    const capacity = 160;
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.BufferAttribute(new Float32Array(capacity * 3), 3));
    geometry.setAttribute('color', new THREE.BufferAttribute(new Float32Array(capacity * 3), 3));
    geometry.setDrawRange(0, 0);
    const material = new THREE.PointsMaterial({
      size: 0.19,
      vertexColors: true,
      transparent: true,
      opacity: 0.94,
      depthWrite: false,
      blending: THREE.AdditiveBlending,
      toneMapped: false,
    });
    const points = new THREE.Points(geometry, material);
    points.frustumCulled = false;
    points.renderOrder = 6;
    return points;
  }

  private createLightingAndSky(): void {
    const sky = new THREE.Mesh(
      new THREE.SphereGeometry(150, 32, 16),
      new THREE.ShaderMaterial({
        side: THREE.BackSide,
        depthWrite: false,
        uniforms: {
          uTop: { value: new THREE.Color(0x25477b) },
          uHorizon: { value: new THREE.Color(0xb8d9c9) },
          uSunColor: { value: new THREE.Color(0xffe6a3) },
          uSunDir: { value: new THREE.Vector3(-0.4, 0.34, 0.6).normalize() },
        },
        vertexShader: 'varying vec3 vDir; void main(){ vDir = normalize(position); gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }',
        fragmentShader: 'varying vec3 vDir; uniform vec3 uTop, uHorizon, uSunColor, uSunDir; void main(){ float h=clamp(vDir.y*0.5+0.5,0.0,1.0); vec3 col=mix(uHorizon,uTop,pow(h,0.6)); float d=clamp(dot(normalize(vDir),normalize(uSunDir)),0.0,1.0); col+=uSunColor*(pow(d,800.0)+pow(d,8.0)*0.25); gl_FragColor=vec4(col,1.0); }',
      }),
    );
    sky.frustumCulled = false;
    this.scene.add(sky);
    this.scene.fog = new THREE.Fog(0x6e9b8f, 32, 72);

    const hemisphere = new THREE.HemisphereLight(0xeafff3, 0x253342, 1.9);
    this.scene.add(hemisphere);
    const sun = new THREE.DirectionalLight(0xffedbd, 3.15);
    sun.position.set(-12, 25, 12);
    sun.castShadow = true;
    sun.shadow.mapSize.set(1024, 1024);
    sun.shadow.camera.near = 1;
    sun.shadow.camera.far = 65;
    sun.shadow.camera.left = -23;
    sun.shadow.camera.right = 23;
    sun.shadow.camera.top = 20;
    sun.shadow.camera.bottom = -20;
    sun.shadow.bias = -0.00035;
    this.scene.add(sun);
    const rim = new THREE.DirectionalLight(0x8cbcff, 1.1);
    rim.position.set(13, 10, -14);
    this.scene.add(rim);
  }

  private createBattlefield(): void {
    const board = this.activeStage().board;
    const islandMaterial: THREE.Material = this.stageIndex === 2
      ? new THREE.MeshBasicMaterial({ color: 0x315a5b })
      : this.materials.groundContact;
    if (this.stageIndex === 2) islandMaterial.userData.stageOwned = true;
    const island = new THREE.Mesh(
      new THREE.CylinderGeometry(board.islandRadius, board.islandRadius + 1.8, 1.25, 12),
      islandMaterial,
    );
    island.name = 'battlefield-island';
    island.position.y = -0.78;
    island.receiveShadow = true;
    this.boardGroup.add(island);

    this.createPathGeometry();
    this.createGridGeometry();
    this.createBlockers();
    this.createWorldProps();
  }

  private activeStage() {
    return STAGES[this.stageIndex];
  }

  private isGuidedTutorialActive(): boolean {
    return this.activeStage().tutorial && this.waveIndex < 3;
  }

  private isTutorialMasteryPhase(): boolean {
    return this.stageIndex === 0 && this.waveIndex >= 3;
  }

  private currentWaveHealthMultiplier(): number {
    const waves = this.activeStage().waves;
    return waves[Math.min(this.waveIndex, waves.length - 1)]?.healthMultiplier ?? 1;
  }

  private enemyKillReward(kind: EnemyKind): number {
    const definition = ENEMY_DEFINITIONS[kind];
    return Math.max(1, Math.round(definition.reward * this.activeStage().killRewardMultiplier * ENEMY_REWARD_MULTIPLIER));
  }

  private waveClearReward(baseReward: number): number {
    return Math.max(0, Math.round(baseReward * WAVE_CLEAR_REWARD_MULTIPLIER));
  }

  private gridToWorld(gx: number, gz: number, layer: 0 | 1 | 2): THREE.Vector3 {
    const board = this.activeStage().board;
    return new THREE.Vector3(
      board.originX + gx * CELL_SIZE,
      LAYER_HEIGHTS[layer],
      board.originZ + gz * CELL_SIZE,
    );
  }

  private configureStagePath(): void {
    this.pathXZ = this.activeStage().path.map(([x, z]) => new THREE.Vector2(x, z));
    this.pathSegmentLengths = this.pathXZ.slice(1).map((point, index) => point.distanceTo(this.pathXZ[index]));
    this.pathTotalLength = this.pathSegmentLengths.reduce((sum, length) => sum + length, 0);
  }

  private positionNexus(): void {
    const end = this.pathXZ[this.pathXZ.length - 1];
    this.nexus.position.set(end.x - 0.8, 0, end.y);
  }

  private rebuildBattlefield(): void {
    this.boardGroup.traverse((child) => {
      if (!(child instanceof THREE.Mesh || child instanceof THREE.Line)) return;
      child.geometry.dispose();
      const materials = Array.isArray(child.material) ? child.material : [child.material];
      for (const material of materials) {
        if (material.userData.stageOwned) material.dispose();
      }
    });
    this.boardGroup.clear();
    this.cells.clear();
    this.cellPickables.length = 0;
    this.blockers.length = 0;
    this.createBattlefield();
    this.positionNexus();
  }

  private switchStage(index: number): void {
    this.stageIndex = THREE.MathUtils.clamp(index, 0, STAGES.length - 1);
    this.configureStagePath();
    this.resetRun();
    this.rebuildBattlefield();
    this.updateUi(true);
  }

  private createPathGeometry(): void {
    const border = new THREE.Mesh(this.createPathRibbonGeometry(1.94), this.materials.pathTrim);
    border.name = 'enemy-path-border';
    border.position.y = 0.452;
    border.receiveShadow = true;
    this.boardGroup.add(border);

    const surface = new THREE.Mesh(this.createPathRibbonGeometry(1.66), this.materials.path);
    surface.name = 'enemy-path-surface';
    surface.position.y = 0.468;
    surface.receiveShadow = true;
    this.boardGroup.add(surface);
    this.createSpawnDirectionMarker();
  }

  private createSpawnDirectionMarker(): void {
    const start = this.pathXZ[0];
    const next = this.pathXZ[1];
    if (!start || !next) return;
    const direction = next.clone().sub(start).normalize();
    const createArrowMaterial = (): THREE.MeshBasicMaterial => {
      const material = new THREE.MeshBasicMaterial({
        color: 0xff263f,
        depthTest: false,
        depthWrite: false,
        fog: false,
        toneMapped: false,
      });
      material.userData.stageOwned = true;
      return material;
    };
    const shaft = new THREE.Mesh(new THREE.BoxGeometry(1.75, 0.18, 0.56), createArrowMaterial());
    shaft.name = 'enemy-spawn-direction-arrow-shaft';
    shaft.position.x = -0.35;
    shaft.renderOrder = 30;

    const head = new THREE.Mesh(new THREE.ConeGeometry(0.76, 1.3, 4), createArrowMaterial());
    head.name = 'enemy-spawn-direction-arrow-head';
    head.position.x = 0.82;
    head.rotation.z = -Math.PI / 2;
    head.renderOrder = 30;

    const marker = new THREE.Group();
    marker.name = 'enemy-spawn-direction';
    const markerDistance = Math.min(start.distanceTo(next) * 0.82, 14);
    marker.position.set(start.x + direction.x * markerDistance, 0.78, start.y + direction.y * markerDistance);
    marker.rotation.y = Math.atan2(-direction.y, direction.x);
    marker.userData.directionX = direction.x;
    marker.userData.directionZ = direction.y;
    marker.add(shaft, head);
    this.boardGroup.add(marker);
  }

  private createPathRibbonGeometry(width: number): THREE.BufferGeometry {
    const halfWidth = width / 2;
    const positions: number[] = [];
    const normals: number[] = [];
    const uvs: number[] = [];
    const indices: number[] = [];
    let distance = 0;

    for (let index = 0; index < this.pathXZ.length; index += 1) {
      const point = this.pathXZ[index];
      const previous = this.pathXZ[Math.max(0, index - 1)];
      const next = this.pathXZ[Math.min(this.pathXZ.length - 1, index + 1)];
      const previousDirection = point.clone().sub(previous).normalize();
      const nextDirection = next.clone().sub(point).normalize();
      const direction = index === 0 ? nextDirection : index === this.pathXZ.length - 1 ? previousDirection : previousDirection.clone().add(nextDirection).normalize();
      const previousNormal = new THREE.Vector2(-previousDirection.y, previousDirection.x);
      const nextNormal = new THREE.Vector2(-nextDirection.y, nextDirection.x);
      let miter = index === 0 ? nextNormal : index === this.pathXZ.length - 1 ? previousNormal : previousNormal.clone().add(nextNormal).normalize();
      if (miter.lengthSq() < 0.0001) miter = new THREE.Vector2(-direction.y, direction.x);
      const denominator = Math.max(0.42, Math.abs(miter.dot(index === 0 ? nextNormal : previousNormal)));
      const miterLength = Math.min(halfWidth * 2.2, halfWidth / denominator);
      const left = point.clone().addScaledVector(miter, miterLength);
      const right = point.clone().addScaledVector(miter, -miterLength);
      if (index > 0) distance += point.distanceTo(this.pathXZ[index - 1]);
      positions.push(left.x, 0, left.y, right.x, 0, right.y);
      normals.push(0, 1, 0, 0, 1, 0);
      uvs.push(0, distance / 2, 1, distance / 2);
      if (index < this.pathXZ.length - 1) {
        const current = index * 2;
        const following = current + 2;
        indices.push(current, following, current + 1, following, following + 1, current + 1);
      }
    }

    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
    geometry.setAttribute('normal', new THREE.Float32BufferAttribute(normals, 3));
    geometry.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
    geometry.setIndex(indices);
    geometry.computeBoundingSphere();
    return geometry;
  }

  private createGridGeometry(): void {
    type TileEntry = { key: string; center: THREE.Vector3 };
    const groups = new Map<number, { layer: 0 | 1 | 2; entries: TileEntry[] }>();
    const board = this.activeStage().board;
    for (let gx = 0; gx < board.width; gx += 1) {
      for (let gz = 0; gz < board.depth; gz += 1) {
        const layer = this.layerForCell(gx, gz);
        const logicalCenter = this.gridToWorld(gx, gz, layer);
        const buildable = !this.isPathPosition(logicalCenter.x, logicalCenter.z) && logicalCenter.x < board.buildMaxX;
        const visualLayer = buildable ? layer : 0;
        const center = this.gridToWorld(gx, gz, visualLayer);
        const key = gridKey(gx, gz);
        const group = groups.get(visualLayer) ?? { layer: visualLayer, entries: [] };
        group.entries.push({ key, center });
        groups.set(visualLayer, group);
        this.cells.set(key, { gx, gz, layer, buildable });
      }
    }

    const matrix = new THREE.Matrix4();
    for (const group of groups.values()) {
      const height = Math.max(0.18, LAYER_HEIGHTS[group.layer] - 0.08);
      const layerColor = group.layer === 0 ? 0x356f62 : group.layer === 1 ? 0x4c7180 : 0x59678f;
      const material = new THREE.MeshStandardMaterial({
        color: layerColor,
        roughness: 0.82,
        metalness: 0.04,
      });
      material.userData.stageOwned = true;
      // Cells stay individually raycastable for logical grid placement, while a
      // slight overlap removes the visual seams between adjacent terrain cells.
      const tiles = new THREE.InstancedMesh(new THREE.BoxGeometry(2.04, height, 2.04), material, group.entries.length);
      tiles.name = `ground-layer-${group.layer}`;
      tiles.userData.cellKeys = group.entries.map((entry) => entry.key);
      tiles.receiveShadow = true;
      group.entries.forEach((entry, index) => {
        matrix.makeTranslation(entry.center.x, height / 2 - 0.1, entry.center.z);
        tiles.setMatrixAt(index, matrix);
      });
      tiles.instanceMatrix.needsUpdate = true;
      this.boardGroup.add(tiles);
      this.cellPickables.push(tiles);
    }
  }

  private createBlockers(): void {
    if (!this.activeStage().hasElevation) return;
    const definitions: readonly { x: number; z: number; width: number; depth: number; height: number; layer: 0 | 1 | 2 }[] = [
      // Stage 2 intentionally has no line-of-fire walls. Its decisions come
      // from the longer route, limited economy and two firing heights.
    ];
    for (const definition of definitions) {
      const wall = this.art.createWall(definition.width, definition.depth, definition.height);
      wall.position.set(definition.x, LAYER_HEIGHTS[definition.layer] - 0.1, definition.z);
      this.boardGroup.add(wall);
      this.blockers.push({
        layer: definition.layer,
        minX: definition.x - definition.width / 2,
        maxX: definition.x + definition.width / 2,
        minZ: definition.z - definition.depth / 2,
        maxZ: definition.z + definition.depth / 2,
      });
    }
  }

  private createWorldProps(): void {
    const crystalGeometry = new THREE.ConeGeometry(0.34, 1.5, 5);
    const rockGeometry = new THREE.DodecahedronGeometry(0.48, 0);
    const crystalCount = 26;
    const crystals = new THREE.InstancedMesh(crystalGeometry, this.materials.element('ice'), crystalCount);
    const rocks = new THREE.InstancedMesh(rockGeometry, this.materials.rock, crystalCount);
    crystals.name = 'world-prop-crystals';
    rocks.name = 'world-prop-rocks';
    const matrix = new THREE.Matrix4();
    const quaternion = new THREE.Quaternion();
    const scale = new THREE.Vector3();
    const position = new THREE.Vector3();
    const propRadius = this.activeStage().board.islandRadius * 0.9;
    for (let index = 0; index < crystalCount; index += 1) {
      const angle = index / crystalCount * Math.PI * 2;
      const radius = propRadius + (index % 4) * 0.55;
      position.set(Math.cos(angle) * radius, 0.05 + (index % 3) * 0.08, Math.sin(angle) * radius * 0.62);
      quaternion.setFromEuler(new THREE.Euler(0, angle, ((index % 5) - 2) * 0.08));
      scale.setScalar(0.62 + (index % 4) * 0.1);
      matrix.compose(position, quaternion, scale);
      crystals.setMatrixAt(index, matrix);
      position.y = -0.1;
      position.x *= 0.97;
      position.z *= 0.97;
      scale.set(1.4, 0.8, 1.15);
      matrix.compose(position, quaternion, scale);
      rocks.setMatrixAt(index, matrix);
    }
    crystals.instanceMatrix.needsUpdate = true;
    rocks.instanceMatrix.needsUpdate = true;
    this.boardGroup.add(rocks, crystals);

    const pylonBaseGeometry = new THREE.CylinderGeometry(0.45, 0.62, 1.25, 7);
    const pylonBeaconGeometry = new THREE.OctahedronGeometry(0.25, 0);
    for (let index = 0; index < 8; index += 1) {
      const pylon = new THREE.Group();
      pylon.name = `world-prop-pylon-${index}`;
      const base = new THREE.Mesh(pylonBaseGeometry, this.materials.bodySecondary);
      base.position.y = 0.62;
      pylon.add(base);
      const beacon = new THREE.Mesh(pylonBeaconGeometry, index % 2 === 0 ? this.materials.element('wind') : this.materials.reward);
      beacon.position.y = 1.55;
      pylon.add(beacon);
      const angle = index / 8 * Math.PI * 2;
      pylon.position.set(Math.cos(angle) * propRadius * 0.9, -0.12, Math.sin(angle) * propRadius * 0.6);
      this.boardGroup.add(pylon);
    }
  }

  private layerForCell(gx: number, gz: number): 0 | 1 | 2 {
    if (!this.activeStage().hasElevation) return 0;
    if (this.stageIndex === 2) {
      const board = this.activeStage().board;
      const x = board.originX + gx * CELL_SIZE;
      const z = board.originZ + gz * CELL_SIZE;
      const leftFlightPlateau = x >= -13 && x <= -7 && z >= -5 && z <= 3;
      const leftOppositePlateau = x >= -3 && x <= 1 && z >= -5 && z <= 3;
      const rightFlightPlateau = x >= 10 && x <= 16 && z >= -3 && z <= 5;
      const rightOppositePlateau = x >= 4 && x <= 6 && z >= -3 && z <= 5;
      return leftFlightPlateau || leftOppositePlateau || rightFlightPlateau || rightOppositePlateau ? 1 : 0;
    }
    // Keep both raised build areas beside active lanes so Layer 1 networks can
    // spend their range on flying enemies instead of crossing empty ground.
    // The smaller plateau sits across the left vertical lane from the central
    // plateau, placing it on the open screen-right flank in the authored view.
    const centralPlateau = gx >= 5 && gx <= 8 && gz >= 3 && gz <= 5;
    const oppositePlateau = gx >= 1 && gx <= 2 && gz >= 4 && gz <= 5;
    if (centralPlateau || oppositePlateau) return 1;
    return 0;
  }

  private isPathPosition(x: number, z: number): boolean {
    return this.distanceToEnemyPath(x, z) < 1.48;
  }

  private distanceToEnemyPath(x: number, z: number): number {
    const point = new THREE.Vector2(x, z);
    let distance = Number.POSITIVE_INFINITY;
    for (let index = 0; index < this.pathXZ.length - 1; index += 1) {
      distance = Math.min(distance, pointSegmentDistance(point, this.pathXZ[index], this.pathXZ[index + 1]));
    }
    return distance;
  }

  private createBuildButtons(): void {
    this.buildList.replaceChildren();
    for (const buildGroup of BUILD_GROUPS) {
      const section = document.createElement('section');
      section.className = 'build-category';
      section.dataset.category = buildGroup.label.toLowerCase().replaceAll(' ', '-');
      const heading = document.createElement('h3');
      heading.className = 'build-category-title';
      heading.textContent = buildGroup.label;
      section.append(heading);
      for (const type of buildGroup.types) {
        const definition = TOWER_DEFINITIONS[type];
        const item = document.createElement('div');
        item.className = 'build-item';
        item.dataset.towerItem = type;
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'build-button';
        button.dataset.towerType = type;
        button.title = `${buildGroup.label}: ${definition.name}`;
        button.setAttribute('aria-label', `${buildGroup.label}: ${definition.name}, giá ${definition.cost}`);
        button.innerHTML = `<span class="tower-glyph" style="--tower-color:#${definition.color.toString(16).padStart(6, '0')}">${definition.icon}</span><span class="build-copy"><strong>${definition.shortName}</strong><b>${definition.cost}</b></span>`;
        const detailButton = document.createElement('button');
        detailButton.type = 'button';
        detailButton.className = 'tower-info-button';
        detailButton.dataset.towerInfo = type;
        detailButton.title = `Xem chi tiết ${definition.name}`;
        detailButton.setAttribute('aria-label', `Xem chi tiết ${definition.name}`);
        detailButton.setAttribute('aria-pressed', 'false');
        detailButton.innerHTML = '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M2.5 12s3.5-6 9.5-6 9.5 6 9.5 6-3.5 6-9.5 6-9.5-6-9.5-6Z"/><circle cx="12" cy="12" r="3.1"/></svg>';
        item.append(button, detailButton);
        section.append(item);
      }
      this.buildList.append(section);
    }
  }

  private installUiEvents(): void {
    this.buildList.addEventListener('click', (event) => {
      const detailTarget = (event.target as HTMLElement).closest<HTMLButtonElement>('[data-tower-info]');
      if (detailTarget) {
        event.preventDefault();
        event.stopPropagation();
        this.showTowerDefinition(detailTarget.dataset.towerInfo as TowerType);
        return;
      }
      if (performance.now() < this.suppressBuildClickUntil) {
        event.preventDefault();
        return;
      }
      const target = (event.target as HTMLElement).closest<HTMLButtonElement>('[data-tower-type]');
      if (!target) return;
      const type = target.dataset.towerType as TowerType;
      this.selectBuild(type);
    });
    this.getElement('#start-wave').addEventListener('click', () => this.startWave());
    this.getElement('#wave-enemies').addEventListener('click', (event) => {
      const button = (event.target as HTMLElement).closest<HTMLButtonElement>('[data-enemy-kind]');
      if (!button || this.phase !== 'ready') return;
      const kind = button.dataset.enemyKind as EnemyKind;
      this.selectedWaveEnemyKind = this.selectedWaveEnemyKind === kind ? null : kind;
      if (this.selectedWaveEnemyKind !== null) this.clearToast();
      this.renderWaveIntel();
      this.audio.ui('select');
    });
    this.getElement('#wave-enemies').addEventListener('pointerover', (event) => {
      if (!window.matchMedia('(hover: hover) and (pointer: fine)').matches || event.pointerType !== 'mouse' || this.phase !== 'ready') return;
      const button = (event.target as HTMLElement).closest<HTMLButtonElement>('[data-enemy-kind]');
      if (!button) return;
      this.hoveredWaveEnemyKind = button.dataset.enemyKind as EnemyKind;
      this.renderWaveIntel(true);
    });
    this.getElement('#wave-panel').addEventListener('pointerleave', () => {
      this.hoveredWaveEnemyKind = null;
      this.renderWaveIntel(true);
    });
    this.getElement('#wave-enemy-detail').addEventListener('click', (event) => {
      if (!(event.target as HTMLElement).closest('[data-close-wave-intel]')) return;
      this.selectedWaveEnemyKind = null;
      this.hoveredWaveEnemyKind = null;
      this.renderWaveIntel();
    });
    this.getElement('#pause-button').addEventListener('click', () => this.togglePause());
    this.getElement('#restart-button').addEventListener('click', () => this.resetRun());
    this.getElement('#speed-button').addEventListener('click', () => {
      this.speedIndex = (this.speedIndex + 1) % GAME_SPEEDS.length;
      this.updateUi(true);
    });
    this.getElement('#sound-button').addEventListener('click', () => {
      const muted = this.audio.toggleMute();
      this.showToast(muted ? 'Đã tắt âm thanh.' : 'Đã bật âm thanh.', 'info');
      this.updateUi(true);
    });
    this.getElement('#result-restart').addEventListener('click', () => {
      if (this.stageCleared && this.stageIndex < STAGES.length - 1) this.switchStage(this.stageIndex + 1);
      else if (this.masteryCheckpoint && this.isTutorialMasteryPhase()) this.restoreMasteryCheckpoint();
      else this.resetRun();
    });
    this.getElement('#reaction-tutorial-continue').addEventListener('click', () => this.dismissReactionTutorialPopup());
    this.getElement('#cancel-action').addEventListener('click', () => this.cancelInteraction());
    this.getElement('#inspector-close-detail').addEventListener('click', () => {
      this.inspectedBuildType = null;
      this.updateUi(true);
      this.audio.ui('select');
    });
    this.getElement('#action-upgrade').addEventListener('click', () => this.upgradeSelected());
    this.getElement('#action-move').addEventListener('click', () => this.beginMove());
    this.getElement('#action-sell').addEventListener('click', () => this.sellSelected());
    this.getElement('#branch-power').addEventListener('click', () => this.setAmplifierBranch('power'));
    this.getElement('#branch-throughput').addEventListener('click', () => this.setAmplifierBranch('throughput'));
  }

  private installInput(): void {
    this.buildList.addEventListener('pointerdown', this.onBuildPointerDownBound);
    window.addEventListener('pointermove', this.onBuildPointerMoveBound, { passive: false });
    window.addEventListener('pointerup', this.onBuildPointerUpBound);
    window.addEventListener('pointercancel', this.onBuildPointerCancelBound);
    window.addEventListener('blur', this.onWindowBlurBound);
    this.canvas.addEventListener('pointerdown', (event) => {
      this.pointerStart.set(event.clientX, event.clientY);
      this.pointerMoved = false;
      if (ROUTING_MODE !== 'link' || event.button !== 0) return;
      const source = this.selectedTower();
      const pressedTowerId = this.findTowerAt(event.clientX, event.clientY);
      if (!source || !isAmmoEmitter(source.type)
        || (pressedTowerId !== source.id && !this.isClientNearTower(source, event.clientX, event.clientY))) return;
      this.linkDragPointerId = event.pointerId;
      this.linkDragSourceTowerId = source.id;
      this.controls.enabled = false;
      event.preventDefault();
      try { this.canvas.setPointerCapture(event.pointerId); } catch { /* Synthetic input need not own capture. */ }
    }, { capture: true });
    this.canvas.addEventListener('pointermove', (event) => {
      const moved = this.pointerStart.distanceTo(new THREE.Vector2(event.clientX, event.clientY)) > 7;
      if (moved) this.pointerMoved = true;
      if (this.linkDragPointerId !== event.pointerId || !moved) return;
      if (this.interactionMode !== 'link') this.beginLink();
      this.updateLinkDragHover(event.clientX, event.clientY);
      event.preventDefault();
    }, { capture: true });
    this.canvas.addEventListener('pointerup', (event) => {
      if (this.buildDrag?.dragging) return;
      if (this.linkDragPointerId === event.pointerId) {
        const sourceId = this.linkDragSourceTowerId;
        const wasDragging = this.interactionMode === 'link' && sourceId !== null;
        const targetId = wasDragging ? this.findTowerAt(event.clientX, event.clientY) : null;
        this.finishLinkDrag(event.pointerId);
        if (wasDragging && targetId !== null && targetId !== sourceId) this.tryLinkTowers(sourceId, targetId);
        if (this.interactionMode === 'link') {
          this.clearLinkMode();
          this.refreshSelectionVisual();
          this.updateUi(true);
        }
        event.preventDefault();
        if (wasDragging || this.pointerMoved) return;
      }
      if (this.pointerMoved) return;
      this.handleCanvasTap(event.clientX, event.clientY);
    }, { capture: true });
    this.canvas.addEventListener('pointercancel', (event) => {
      if (this.linkDragPointerId !== event.pointerId) return;
      this.finishLinkDrag(event.pointerId);
      this.clearLinkMode();
      this.refreshSelectionVisual();
      this.updateUi(true);
    }, { capture: true });
    this.canvas.addEventListener('contextmenu', (event) => event.preventDefault());
    this.installRotationHold('#action-left', -1);
    this.installRotationHold('#action-right', 1);
    window.addEventListener('keydown', this.onKeyDownBound);
    window.addEventListener('keyup', this.onKeyUpBound);
    document.addEventListener('visibilitychange', this.onVisibilityBound);
  }

  private installRotationHold(selector: string, direction: -1 | 1): void {
    const button = this.getButton(selector);
    button.addEventListener('pointerdown', (event) => {
      if (ROUTING_MODE !== 'rotation' || event.button !== 0 || !this.prepareHeldRotation()) return;
      event.preventDefault();
      this.rotationPointerId = event.pointerId;
      this.rotationPointerDirection = direction;
      try { button.setPointerCapture(event.pointerId); } catch { /* Synthetic input need not own capture. */ }
      button.classList.add('pressed');
    });
    const release = (event: PointerEvent) => {
      if (this.rotationPointerId !== event.pointerId) return;
      this.stopHeldRotation();
    };
    button.addEventListener('pointerup', release);
    button.addEventListener('pointercancel', release);
  }

  private onBuildPointerDown(event: PointerEvent): void {
    if (event.button !== 0 || this.buildDrag) return;
    const button = (event.target as HTMLElement).closest<HTMLButtonElement>('[data-tower-type]');
    if (!button || button.disabled) return;
    const type = button.dataset.towerType as TowerType;
    this.buildDrag = {
      pointerId: event.pointerId,
      type,
      button,
      origin: new THREE.Vector2(event.clientX, event.clientY),
      dragging: false,
      cell: null,
      valid: false,
      reason: 'Kéo vào một ô có thể xây.',
    };
  }

  private onBuildPointerMove(event: PointerEvent): void {
    const drag = this.buildDrag;
    if (!drag || drag.pointerId !== event.pointerId) return;
    if (!drag.dragging) {
      const distance = drag.origin.distanceTo(new THREE.Vector2(event.clientX, event.clientY));
      if (distance < 6) return;
      if (!this.isTowerUnlocked(drag.type) || !this.canPurchaseTower(drag.type)) {
        this.cancelBuildDrag(false);
        return;
      }
      this.selectBuild(drag.type);
      if (this.selectedBuildType !== drag.type || this.interactionMode !== 'build') {
        this.cancelBuildDrag(false);
        return;
      }
      drag.dragging = true;
      drag.button.classList.add('dragging');
      document.body.classList.add('is-build-dragging');
      this.controls.enabled = false;
      if (!this.isGuidedTutorialActive()) this.showToast(`Kéo ${TOWER_DEFINITIONS[drag.type].shortName} vào vùng đặt màu xanh.`, 'info');
    }
    if (event.cancelable) event.preventDefault();
    const hit = this.findCellAt(event.clientX, event.clientY);
    drag.cell = hit?.cell ?? null;
    if (drag.cell) {
      const placement = this.validateFootprint(drag.type, drag.cell.gx, drag.cell.gz, null);
      drag.valid = placement.valid;
      drag.reason = placement.valid ? '' : placement.reason;
    } else {
      drag.valid = false;
      drag.reason = 'Hãy thả trụ bên trong chiến trường.';
    }
    this.refreshPlacementPreview(drag.type, drag.cell, drag.valid);
  }

  private onBuildPointerUp(event: PointerEvent): void {
    const drag = this.buildDrag;
    if (!drag || drag.pointerId !== event.pointerId) return;
    if (!drag.dragging) {
      this.buildDrag = null;
      return;
    }
    this.suppressBuildClickUntil = performance.now() + 400;
    const cell = drag.cell;
    const placed = Boolean(cell && drag.valid && this.tryPlaceTower(drag.type, cell.gx, cell.gz));
    if (!placed) {
      if (!this.isGuidedTutorialActive()) this.showToast(drag.reason || 'Không thể đặt vùng trụ tại đây.', 'bad');
      this.audio.ui('error');
    }
    this.cancelBuildDrag(true);
  }

  private onBuildPointerCancel(event: PointerEvent): void {
    if (this.buildDrag?.pointerId !== event.pointerId) return;
    this.cancelBuildDrag(false);
  }

  private cancelBuildDrag(placed: boolean): void {
    const drag = this.buildDrag;
    if (drag?.dragging && !placed && !this.isGuidedTutorialActive()) this.showToast('Đã hủy đặt trụ. Không mất Arcana.', 'info');
    drag?.button.classList.remove('dragging');
    document.body.classList.remove('is-build-dragging');
    this.buildDrag = null;
    this.controls.enabled = true;
    this.clearPlacementPreview();
  }

  private onKeyDown(event: KeyboardEvent): void {
    const key = event.key.toLowerCase();
    if (ROUTING_MODE === 'rotation' && (key === 'q' || key === 'e')) {
      if (!event.repeat && this.prepareHeldRotation()) this.heldRotationKeys.add(key);
      return;
    }
    if (event.repeat) return;
    if (/^[1-7]$/.test(event.key)) {
      this.selectBuild(BUILD_ORDER[Number(event.key) - 1]);
      return;
    }
    if (event.key === 'Escape') this.cancelInteraction();
    else if (event.key.toLowerCase() === 'm') this.beginMove();
    else if (event.key.toLowerCase() === 'u') this.upgradeSelected();
    else if (event.key === 'Delete' || event.key === 'Backspace') this.sellSelected();
    else if (event.code === 'Space') {
      event.preventDefault();
      if (this.phase === 'ready') this.startWave();
      else this.togglePause();
    }
  }

  private onKeyUp(event: KeyboardEvent): void {
    const key = event.key.toLowerCase();
    if (key === 'q' || key === 'e') this.heldRotationKeys.delete(key);
  }

  private onVisibilityChange(): void {
    if (!document.hidden) return;
    this.stopHeldRotation();
    if (this.phase === 'wave') this.setPhase('paused');
  }

  private isTowerUnlocked(type: TowerType): boolean {
    if (this.isTutorialMasteryPhase()) return type === 'foundry' || type === 'fire' || type === 'ice';
    if (this.stageIndex === 1) {
      if (type === 'amplifier') return this.waveIndex >= 2;
      if (type === 'lance') return this.waveIndex >= 3;
      return true;
    }
    if (!this.activeStage().tutorial) return true;
    if (type === 'foundry') return true;
    if (type === 'fire') return this.tutorialStep >= 1;
    if (type === 'ice') return this.tutorialStep >= 6;
    return false;
  }

  private stageTwoRequiredTower(): 'foundry' | 'amplifier' | 'lance' | null {
    if (this.stageIndex !== 1 || this.phase !== 'ready') return null;
    if (this.waveIndex === 2 && !this.stageTwoAmplifierIntroduced) return 'amplifier';
    if (this.waveIndex === 3) {
      if (!this.stageTwoLanceIntroduced) return 'lance';
      const feeder = this.towers.find((tower) => tower.group.userData.stageTwoLanceFeeder === true);
      if (!feeder) return 'foundry';
    }
    return null;
  }

  private isStageTwoLessonWave(): boolean {
    return this.stageIndex === 1 && this.phase === 'ready' && (this.waveIndex === 2 || this.waveIndex === 3);
  }

  private isMandatoryLessonPurchase(type: TowerType): boolean {
    return this.stageTwoRequiredTower() === type;
  }

  private towerPurchaseCost(type: TowerType): number {
    return this.isMandatoryLessonPurchase(type) ? 0 : TOWER_DEFINITIONS[type].cost;
  }

  private isMandatoryLessonPlacement(type: TowerType, gx: number, gz: number): boolean {
    if (!this.isMandatoryLessonPurchase(type)) return false;
    const lessonCell = this.findStageTwoLessonCell(type as 'foundry' | 'amplifier' | 'lance');
    return lessonCell?.gx === gx && lessonCell.gz === gz;
  }

  private canPurchaseTower(type: TowerType): boolean {
    return this.money >= this.towerPurchaseCost(type);
  }

  private refreshTutorialProgress(): void {
    if (!this.activeStage().tutorial || this.tutorialStep >= TUTORIAL_STEP_COUNT) return;
    let advanced = false;
    while (this.tutorialStep < TUTORIAL_STEP_COUNT) {
      const foundry = this.findTutorialTower('foundry');
      const fire = this.findTutorialTower('fire');
      const ice = this.findTutorialTower('ice');
      const terminalFire = this.findTutorialTower('terminalFire');
      const complete = ROUTING_MODE === 'link'
        ? this.tutorialStep === 0 ? Boolean(foundry)
          : this.tutorialStep === 1 ? Boolean(fire)
            : this.tutorialStep === 2 ? this.selectedTowerId === foundry?.id
              : this.tutorialStep === 3 ? this.interactionMode === 'link' && this.linkSourceTowerId === foundry?.id
                : this.tutorialStep === 4 ? foundry?.outputTargetId === fire?.id
                  : this.tutorialStep === 6 ? Boolean(ice)
                    : this.tutorialStep === 7 ? this.selectedTowerId === fire?.id
                      : this.tutorialStep === 8 ? this.interactionMode === 'link' && this.linkSourceTowerId === fire?.id
                        : this.tutorialStep === 9 ? fire?.outputTargetId === ice?.id
                          : this.tutorialStep === 11 ? Boolean(terminalFire)
                            : this.tutorialStep === 12 ? this.selectedTowerId === ice?.id
                              : this.tutorialStep === 13 ? this.interactionMode === 'link' && this.linkSourceTowerId === ice?.id
                                : this.tutorialStep === 14 ? ice?.outputTargetId === terminalFire?.id
                                  : false
        : this.tutorialStep === 0 ? Boolean(foundry)
          : this.tutorialStep === 1 ? Boolean(fire)
            : this.tutorialStep === 2 ? this.selectedTowerId === foundry?.id
              : this.tutorialStep === 3 ? Boolean(foundry && fire && this.isTowerAimedAt(foundry, fire))
                : this.tutorialStep === 5 ? Boolean(ice)
                  : this.tutorialStep === 6 ? this.selectedTowerId === fire?.id
                    : this.tutorialStep === 7 ? Boolean(fire && ice && this.isTowerAimedAt(fire, ice))
                      : this.tutorialStep === 9 ? Boolean(terminalFire)
                        : this.tutorialStep === 10 ? this.selectedTowerId === ice?.id
                          : this.tutorialStep === 11 ? Boolean(ice && terminalFire && this.isTowerAimedAt(ice, terminalFire))
                            : this.tutorialStep === 12 ? this.selectedTowerId === terminalFire?.id
                              : this.tutorialStep === 13 ? Boolean(terminalFire && this.isAngleAligned(terminalFire.aimAngle, 0))
                                : false;
      if (!complete) break;
      this.tutorialStep += 1;
      advanced = true;
    }
    if (advanced) {
      this.updateUi(true);
    }
  }

  private handleCanvasTap(clientX: number, clientY: number): void {
    const allowNearbyTower = this.interactionMode === 'inspect' || this.interactionMode === 'link';
    const towerId = this.findTowerAt(clientX, clientY, allowNearbyTower);
    if (towerId !== null) {
      this.handleTowerTap(towerId);
      return;
    }

    const cellHit = this.findCellAt(clientX, clientY);
    if (cellHit) {
      this.handleCellTap(cellHit.cell, cellHit.point);
      return;
    }
    this.cancelInteraction();
  }

  private findTowerAt(clientX: number, clientY: number, allowNearby = true): number | null {
    this.setRayFromClient(clientX, clientY);
    const towerHit = this.raycaster.intersectObjects(this.towerPickables, true)[0];
    if (towerHit) {
      const towerId = this.findTowerId(towerHit.object);
      if (towerId !== null) return towerId;
    }
    return allowNearby ? this.findNearbyTowerAt(clientX, clientY) : null;
  }

  private findNearbyTowerAt(clientX: number, clientY: number): number | null {
    const coarsePointer = window.matchMedia('(pointer: coarse)').matches;
    const radius = coarsePointer ? 38 : 24;
    let nearest: { id: number; distance: number } | null = null;
    for (const tower of this.towers) {
      const point = this.worldToClient(tower.group.position.clone().add(new THREE.Vector3(0, 0.7, 0)));
      if (!point) continue;
      const distance = Math.hypot(point.x - clientX, point.y - clientY);
      if (distance > radius || (nearest && distance >= nearest.distance)) continue;
      nearest = { id: tower.id, distance };
    }
    return nearest?.id ?? null;
  }

  private isClientNearTower(tower: TowerState, clientX: number, clientY: number): boolean {
    const point = this.worldToClient(tower.group.position.clone().add(new THREE.Vector3(0, 0.7, 0)));
    if (!point) return false;
    const radius = window.matchMedia('(pointer: coarse)').matches ? 38 : 24;
    return Math.hypot(point.x - clientX, point.y - clientY) <= radius;
  }

  private setRayFromClient(clientX: number, clientY: number): void {
    const rect = this.canvas.getBoundingClientRect();
    this.pointer.set(
      ((clientX - rect.left) / rect.width) * 2 - 1,
      -((clientY - rect.top) / rect.height) * 2 + 1,
    );
    this.raycaster.setFromCamera(this.pointer, this.camera);
  }

  private findCellAt(clientX: number, clientY: number): { cell: CellState; point: THREE.Vector3 } | null {
    this.setRayFromClient(clientX, clientY);
    const cellHit = this.raycaster.intersectObjects(this.cellPickables, false)[0];
    if (cellHit) {
      const cellKeys = cellHit.object.userData.cellKeys as string[] | undefined;
      const cellKeyValue = cellHit.instanceId === undefined
        ? cellHit.object.userData.cellKey as string | undefined
        : cellKeys?.[cellHit.instanceId];
      const cell = cellKeyValue ? this.cells.get(cellKeyValue) : undefined;
      if (cell) return { cell, point: cellHit.point.clone() };
    }
    return null;
  }

  private handleTowerTap(towerId: number): void {
    this.clearLinkMode();
    this.selectedBuildType = null;
    this.inspectedBuildType = null;
    this.interactionMode = 'inspect';
    this.selectedTowerId = towerId;
    this.audio.ui('select');
    this.refreshTutorialProgress();
    this.refreshSelectionVisual();
    this.updateUi(true);
  }

  private handleCellTap(cell: CellState, _point: THREE.Vector3): void {
    if (this.interactionMode === 'build' && this.selectedBuildType) {
      this.tryPlaceTower(this.selectedBuildType, cell.gx, cell.gz);
      return;
    }
    if (this.interactionMode === 'move' && this.selectedTowerId !== null) {
      this.tryMoveTower(this.selectedTowerId, cell.gx, cell.gz);
      return;
    }
    if (this.interactionMode === 'link') {
      if (!this.isGuidedTutorialActive()) this.showToast('Chạm vào một trụ đang phát sáng để tạo liên kết.', 'info');
      return;
    }
    this.selectedTowerId = null;
    this.inspectedBuildType = null;
    this.interactionMode = 'inspect';
    this.refreshSelectionVisual();
    this.updateUi(true);
  }

  private showTowerDefinition(type: TowerType): void {
    this.clearLinkMode();
    this.cancelBuildDrag(false);
    this.selectedBuildType = null;
    this.selectedTowerId = null;
    this.inspectedBuildType = type;
    this.interactionMode = 'inspect';
    this.clearToast();
    this.audio.ui('select');
    this.refreshSelectionVisual();
    this.updateUi(true);
  }

  private selectBuild(type: TowerType): void {
    const definition = TOWER_DEFINITIONS[type];
    const purchaseCost = this.towerPurchaseCost(type);
    if (!this.isTowerUnlocked(type)) {
      if (!this.activeStage().tutorial) this.showToast('Hoàn thành hướng dẫn hiện tại để mở khóa trụ này.', 'bad');
      this.audio.ui('error');
      return;
    }
    if (!this.canPurchaseTower(type)) {
      this.showToast(`Cần thêm ${purchaseCost - this.money} Arcana để mua ${definition.shortName}.`, 'bad');
      this.audio.ui('error');
      return;
    }
    this.clearLinkMode();
    this.inspectedBuildType = null;
    this.selectedBuildType = type;
    this.selectedTowerId = null;
    this.interactionMode = 'build';
    if (!this.isGuidedTutorialActive()) this.showToast(`Đặt ${definition.name} · kích thước ${definition.footprint[0]}×${definition.footprint[1]} ô.`, 'info');
    this.audio.ui('select');
    this.refreshSelectionVisual();
    this.updateUi(true);
  }

  private tryPlaceTower(type: TowerType, gx: number, gz: number): boolean {
    const definition = TOWER_DEFINITIONS[type];
    if (!this.canPurchaseTower(type)) return false;
    const mandatoryLessonPurchase = this.isMandatoryLessonPurchase(type);
    const lessonGrant = this.isMandatoryLessonPlacement(type, gx, gz);
    if (mandatoryLessonPurchase && !lessonGrant) {
      this.showToast(`Đặt ${definition.shortName} vào vùng phát sáng để nhận trụ miễn phí.`, 'info');
      this.audio.ui('error');
      return false;
    }
    const paidCost = lessonGrant ? 0 : definition.cost;
    if (this.money < paidCost) return false;
    const placement = this.validateFootprint(type, gx, gz, null);
    if (!placement.valid || placement.layer === null) {
      if (!this.isGuidedTutorialActive()) this.showToast(placement.reason, 'bad');
      this.audio.ui('error');
      return false;
    }
    const cells = this.footprintKeys(type, gx, gz);
    const worldPositions = cells.map((key) => {
      const cell = this.cells.get(key);
      if (!cell) throw new Error(`Missing cell ${key}`);
      return this.gridToWorld(cell.gx, cell.gz, cell.layer);
    });
    const center = worldPositions.reduce((sum, value) => sum.add(value), new THREE.Vector3()).multiplyScalar(1 / worldPositions.length);
    const group = this.art.createTower(type);
    group.userData.lessonGrant = lessonGrant;
    group.userData.lessonGrantValue = lessonGrant ? definition.cost : 0;
    if (type === 'lance') group.add(this.createLanceAmmoBar());
    group.position.copy(center);
    group.scale.setScalar(0.9);
    const tower: TowerState = {
      id: this.nextTowerId,
      type,
      group,
      gx,
      gz,
      layer: placement.layer,
      cells,
      level: 1,
      totalInvested: paidCost,
      buffer: [],
      outputTargetId: null,
      aimAngle: 0,
      produceTimer: 0.28 / TOWER_FIRE_RATE_MULTIPLIER,
      outputTimer: 0.16 / TOWER_FIRE_RATE_MULTIPLIER,
      skillTimer: 0,
      blockedReason: '',
      amplifierBranch: 'throughput',
      pulse: 0,
    };
    if (
      ROUTING_MODE === 'rotation'
      && this.isGuidedTutorialActive()
      && type === 'fire'
      && gx === TUTORIAL_TOWER_CELLS.terminalFire.gx
      && gz === TUTORIAL_TOWER_CELLS.terminalFire.gz
    ) tower.aimAngle = Math.PI / 2;
    this.applyTowerAimVisual(tower);
    this.nextTowerId += 1;
    for (const key of cells) this.occupied.set(key, tower.id);
    group.traverse((child) => {
      child.userData.towerId = tower.id;
      this.towerPickables.push(child);
    });
    this.towers.push(tower);
    this.scene.add(group);
    this.money -= paidCost;
    if (paidCost > 0) this.queueDiscoveryCue('currency', '', { targetSelector: '.metric.money', highlightOnly: true });
    if (this.stageIndex === 1 && type === 'amplifier') this.stageTwoAmplifierIntroduced = true;
    if (this.stageIndex === 1 && type === 'lance') this.stageTwoLanceIntroduced = true;
    if (this.stageIndex === 1 && this.waveIndex === 3 && type === 'foundry' && this.stageTwoLanceIntroduced) {
      const lance = this.towers.find((candidate) => candidate.type === 'lance');
      if (lance) {
        tower.group.userData.stageTwoLanceFeeder = true;
        this.stageTwoLanceFeederIntroduced = false;
      }
    }
    this.selectedTowerId = tower.id;
    this.selectedBuildType = null;
    this.inspectedBuildType = null;
    this.interactionMode = 'inspect';
    this.spawnBurst(center.clone().add(new THREE.Vector3(0, 0.5, 0)), definition.color, 0.42);
    this.audio.build();
    if (!this.isGuidedTutorialActive()) this.showToast(`Đã đặt ${definition.name} ở tầng ${tower.layer}.`, 'good');
    else this.clearToast();
    this.refreshTutorialProgress();
    this.refreshNetworkVisuals();
    this.refreshSelectionVisual();
    this.updateUi(true);
    return true;
  }

  private validateFootprint(type: TowerType, gx: number, gz: number, movingTowerId: number | null): { valid: boolean; reason: string; layer: 0 | 1 | 2 | null } {
    if (movingTowerId === null && this.isMandatoryLessonPurchase(type)) {
      const lessonCell = this.stageTwoLessonCells.get(type as 'foundry' | 'amplifier' | 'lance');
      if (lessonCell && (lessonCell.gx !== gx || lessonCell.gz !== gz)) {
        return { valid: false, reason: 'Đặt trụ vào vùng phát sáng để nhận miễn phí.', layer: null };
      }
    }
    const keys = this.footprintKeys(type, gx, gz);
    if (keys.length !== TOWER_DEFINITIONS[type].footprint[0] * TOWER_DEFINITIONS[type].footprint[1]) {
      return { valid: false, reason: 'Vùng đặt trụ vượt ra ngoài chiến trường.', layer: null };
    }
    if (this.isGuidedTutorialActive() && movingTowerId === null && (type === 'foundry' || type === 'fire' || type === 'ice')) {
      const tutorialPlacement = type === 'fire' && this.findTutorialTower('fire') ? 'terminalFire' : type;
      const expected = TUTORIAL_TOWER_CELLS[tutorialPlacement];
      if (gx !== expected.gx || gz !== expected.gz) {
        return {
          valid: false,
          reason: `Hướng dẫn: đặt ${TOWER_DEFINITIONS[type].shortName} vào vị trí phát sáng.`,
          layer: null,
        };
      }
    }
    let layer: 0 | 1 | 2 | null = null;
    for (const key of keys) {
      const cell = this.cells.get(key);
      if (!cell || !cell.buildable) return { valid: false, reason: 'Không thể đặt trụ trên đường địch hoặc địa hình bị chặn.', layer: null };
      if (layer === null) layer = cell.layer;
      else if (layer !== cell.layer) return { valid: false, reason: 'Toàn bộ vùng đặt phải nằm trên cùng một tầng bắn.', layer: null };
      const occupyingTower = this.occupied.get(key);
      if (occupyingTower !== undefined && occupyingTower !== movingTowerId) {
        return { valid: false, reason: 'Một trụ khác đã chiếm vùng đặt này.', layer: null };
      }
    }
    if (this.isGuidedTutorialActive() && movingTowerId === null && (type === 'fire' || type === 'ice')) {
      const firstFireAlreadyPlaced = type === 'fire' && Boolean(this.findTutorialTower('fire'));
      const source = firstFireAlreadyPlaced
        ? this.findTutorialTower('ice')
        : type === 'fire' ? this.findTutorialTower('foundry') : this.findTutorialTower('fire');
      if (source) {
        const center = keys
          .map((key) => this.cells.get(key))
          .filter((cell): cell is CellState => Boolean(cell))
          .map((cell) => this.gridToWorld(cell.gx, cell.gz, cell.layer))
          .reduce((sum, position) => sum.add(position), new THREE.Vector3())
          .multiplyScalar(1 / keys.length);
        if (source.group.position.distanceTo(center) > this.connectionRange(source)) {
          return { valid: false, reason: `Hướng dẫn: đặt ${TOWER_DEFINITIONS[type].shortName} trong tầm liên kết hiển thị.`, layer: null };
        }
      }
    }
    return { valid: true, reason: '', layer };
  }

  private footprintKeys(type: TowerType, gx: number, gz: number): string[] {
    const [width, depth] = TOWER_DEFINITIONS[type].footprint;
    const keys: string[] = [];
    for (let x = 0; x < width; x += 1) {
      for (let z = 0; z < depth; z += 1) {
        if (gx + x >= this.activeStage().board.width || gz + z >= this.activeStage().board.depth) continue;
        keys.push(gridKey(gx + x, gz + z));
      }
    }
    return keys;
  }

  private beginLink(): void {
    const source = this.selectedTower();
    if (!source || !isAmmoEmitter(source.type)) {
      if (!this.isGuidedTutorialActive()) this.showToast('Chọn một trụ có đầu ra để tạo liên kết.', 'bad');
      this.audio.ui('error');
      return;
    }
    if (this.interactionMode === 'link' && this.linkSourceTowerId === source.id) return;
    this.selectedBuildType = null;
    this.inspectedBuildType = null;
    this.interactionMode = 'link';
    this.linkSourceTowerId = source.id;
    this.canvas.classList.add('link-mode-active');
    if (!this.isGuidedTutorialActive()) this.showToast('Kéo đến một trụ phát sáng rồi thả để nối đầu ra.', 'info');
    this.audio.ui('select');
    this.refreshTutorialProgress();
    this.refreshSelectionVisual();
    this.updateUi(true);
  }

  private clearLinkMode(): void {
    this.stopHeldRotation();
    this.linkSourceTowerId = null;
    if (this.interactionMode === 'link') this.interactionMode = 'inspect';
    this.canvas.classList.remove('link-mode-active', 'link-target-valid', 'link-target-invalid');
  }

  private updateLinkDragHover(clientX: number, clientY: number): void {
    this.canvas.classList.remove('link-target-valid', 'link-target-invalid');
    const source = this.linkDragSourceTowerId === null ? null : this.findTower(this.linkDragSourceTowerId);
    const targetId = this.findTowerAt(clientX, clientY);
    const target = targetId === null ? null : this.findTower(targetId);
    if (!source || !target || target.id === source.id) return;
    this.canvas.classList.add(this.validateLink(source, target).valid ? 'link-target-valid' : 'link-target-invalid');
  }

  private finishLinkDrag(pointerId: number): void {
    this.linkDragPointerId = null;
    this.linkDragSourceTowerId = null;
    this.controls.enabled = true;
    try { this.canvas.releasePointerCapture(pointerId); } catch { /* Synthetic input need not own capture. */ }
  }

  private linkedReceiver(source: TowerState): TowerState | null {
    if (ROUTING_MODE === 'rotation') return this.findAimedReceiver(source)?.tower ?? null;
    if (source.outputTargetId === null) return null;
    return this.findTower(source.outputTargetId) ?? null;
  }

  private setLinkedReceiver(source: TowerState, target: TowerState | null): void {
    source.outputTargetId = target?.id ?? null;
    if (ROUTING_MODE !== 'link') return;
    source.aimAngle = target ? this.angleToTower(source, target) : 0;
    this.applyTowerAimVisual(source);
  }

  private validateLink(source: TowerState, target: TowerState): { valid: boolean; reason: string } {
    if (!isAmmoEmitter(source.type)) return { valid: false, reason: 'Trụ này không có đầu ra đạn.' };
    if (source.id === target.id) return { valid: false, reason: 'Một trụ không thể tự liên kết với chính nó.' };
    if (!isAmmoReceiver(target.type)) return { valid: false, reason: 'Trụ đích không nhận đạn.' };
    if (source.layer !== target.layer) return { valid: false, reason: 'Hai trụ phải ở cùng tầng bắn.' };
    if (target.outputTargetId === source.id) return { valid: false, reason: 'Không thể tạo liên kết ngược trực tiếp.' };
    const alreadyLinked = source.outputTargetId === target.id;
    const targetHasInput = this.towers.some((candidate) => candidate.outputTargetId === target.id);
    if (!alreadyLinked && targetHasInput && target.outputTargetId !== null) {
      return { valid: false, reason: 'Trụ đích đã có đầu vào và đầu ra; không thể nhận thêm liên kết.' };
    }
    const start = this.towerPort(source);
    const end = this.towerPort(target);
    const distance = start.distanceTo(end);
    if (distance > this.connectionRange(source) + 0.001) return { valid: false, reason: 'Trụ đích nằm ngoài tầm liên kết.' };
    if (this.firstBlockerHit(start, end, source.layer) !== null) return { valid: false, reason: 'Địa hình đang chặn đường liên kết.' };
    return { valid: true, reason: '' };
  }

  private validLinkTargets(source: TowerState): TowerState[] {
    return this.towers.filter((target) => this.validateLink(source, target).valid);
  }

  private tryLinkTowers(sourceId: number, targetId: number): boolean {
    const source = this.findTower(sourceId);
    const target = this.findTower(targetId);
    if (!source || !target) return false;
    const result = this.validateLink(source, target);
    this.lastLinkAttempt = { sourceId, targetId, result: result.valid ? 'linked' : result.reason };
    if (!result.valid) {
      this.canvas.classList.remove('link-target-valid');
      this.canvas.classList.add('link-target-invalid');
      if (!this.isGuidedTutorialActive()) this.showToast(result.reason, 'bad');
      this.audio.ui('error');
      this.refreshSelectionVisual();
      this.updateUi(true);
      return false;
    }
    this.setLinkedReceiver(source, target);
    source.outputTimer = Math.max(source.outputTimer, 0.12);
    if (source.group.userData.stageTwoLanceFeeder === true) {
      this.stageTwoLanceFeederIntroduced = target.type === 'lance';
    }
    this.clearLinkMode();
    this.audio.ui('confirm');
    if (!this.isGuidedTutorialActive()) {
      this.showToast(`Đã nối ${TOWER_DEFINITIONS[source.type].shortName} → ${TOWER_DEFINITIONS[target.type].shortName}.`, 'good');
    }
    this.refreshTutorialProgress();
    this.refreshNetworkVisuals();
    this.refreshSelectionVisual();
    this.updateUi(true);
    return true;
  }

  private connectTowers(source: TowerState, target: TowerState): boolean {
    const result = this.validateLink(source, target);
    if (!result.valid) return false;
    this.setLinkedReceiver(source, target);
    if (source.group.userData.stageTwoLanceFeeder === true) this.stageTwoLanceFeederIntroduced = target.type === 'lance';
    return true;
  }

  private routeTowers(source: TowerState, target: TowerState): boolean {
    if (ROUTING_MODE === 'link') return this.connectTowers(source, target);
    source.aimAngle = this.angleToTower(source, target);
    source.outputTargetId = null;
    this.applyTowerAimVisual(source);
    this.refreshNetworkVisuals();
    return this.linkedReceiver(source)?.id === target.id;
  }

  private sanitizeLinks(): void {
    if (ROUTING_MODE === 'rotation') {
      for (const tower of this.towers) this.applyTowerAimVisual(tower);
      const feeder = this.towers.find((tower) => tower.group.userData.stageTwoLanceFeeder === true);
      this.stageTwoLanceFeederIntroduced = Boolean(feeder && this.linkedReceiver(feeder)?.type === 'lance');
      return;
    }
    for (const source of this.towers) {
      const target = this.linkedReceiver(source);
      if (!target || !this.validateLink(source, target).valid) this.setLinkedReceiver(source, null);
      else this.setLinkedReceiver(source, target);
    }
    const feeder = this.towers.find((tower) => tower.group.userData.stageTwoLanceFeeder === true);
    this.stageTwoLanceFeederIntroduced = Boolean(feeder && this.linkedReceiver(feeder)?.type === 'lance');
  }

  private prepareHeldRotation(): boolean {
    if (ROUTING_MODE !== 'rotation') return false;
    const tower = this.selectedTower();
    if (!tower || (!isAmmoEmitter(tower.type) && tower.type !== 'lance')) {
      if (!this.isGuidedTutorialActive()) this.showToast('Chọn một trụ có đầu ra để điều khiển luồng đạn.', 'bad');
      return false;
    }
    const tutorialTower = this.tutorialRotationTower();
    if (this.isGuidedTutorialActive() && tutorialTower?.id !== tower.id) return false;
    tower.outputTimer = Math.max(tower.outputTimer, 0.18);
    this.interactionMode = 'inspect';
    this.updateUi(true);
    return true;
  }

  private stopHeldRotation(): void {
    this.rotationPointerId = null;
    this.rotationPointerDirection = 0;
    this.heldRotationKeys.clear();
    this.getButton('#action-left').classList.remove('pressed');
    this.getButton('#action-right').classList.remove('pressed');
  }

  private tutorialRotationTower(): TowerState | null {
    if (!this.isGuidedTutorialActive()) return null;
    if (this.tutorialStep === 3) return this.findTutorialTower('foundry') ?? null;
    if (this.tutorialStep === 7) return this.findTutorialTower('fire') ?? null;
    if (this.tutorialStep === 11) return this.findTutorialTower('ice') ?? null;
    if (this.tutorialStep === 13) return this.findTutorialTower('terminalFire') ?? null;
    return null;
  }

  private tutorialRotationTarget(tower: TowerState): number | null {
    const foundry = this.findTutorialTower('foundry');
    const fire = this.findTutorialTower('fire');
    const ice = this.findTutorialTower('ice');
    const terminalFire = this.findTutorialTower('terminalFire');
    if (this.tutorialStep === 3 && tower.id === foundry?.id && fire) return this.angleToTower(tower, fire);
    if (this.tutorialStep === 7 && tower.id === fire?.id && ice) return this.angleToTower(tower, ice);
    if (this.tutorialStep === 11 && tower.id === ice?.id && terminalFire) return this.angleToTower(tower, terminalFire);
    if (this.tutorialStep === 13 && tower.id === terminalFire?.id) return 0;
    return null;
  }

  private angleToTower(source: TowerState, target: TowerState): number {
    return Math.atan2(target.group.position.z - source.group.position.z, target.group.position.x - source.group.position.x);
  }

  private isTowerAimedAt(source: TowerState, target: TowerState): boolean {
    return this.isAngleAligned(source.aimAngle, this.angleToTower(source, target));
  }

  private isAngleAligned(angle: number, target: number, tolerance = THREE.MathUtils.degToRad(1.8)): boolean {
    return Math.abs(Math.atan2(Math.sin(target - angle), Math.cos(target - angle))) <= tolerance;
  }

  private updateHeldRotation(delta: number): void {
    if (ROUTING_MODE !== 'rotation') return;
    const keyboardDirection = (this.heldRotationKeys.has('e') ? 1 : 0) - (this.heldRotationKeys.has('q') ? 1 : 0);
    const direction = (this.rotationPointerDirection || Math.sign(keyboardDirection)) as -1 | 0 | 1;
    if (direction === 0) return;
    const tower = this.selectedTower();
    if (!tower || (!isAmmoEmitter(tower.type) && tower.type !== 'lance')) {
      this.stopHeldRotation();
      return;
    }
    const tutorialTarget = this.isGuidedTutorialActive() ? this.tutorialRotationTarget(tower) : null;
    if (this.isGuidedTutorialActive() && tutorialTarget === null) {
      this.stopHeldRotation();
      return;
    }
    const rotationDelta = direction * ROTATION_SPEED * delta;
    let nextAngle = Math.atan2(Math.sin(tower.aimAngle + rotationDelta), Math.cos(tower.aimAngle + rotationDelta));
    if (tutorialTarget !== null) {
      const remaining = Math.atan2(Math.sin(tutorialTarget - tower.aimAngle), Math.cos(tutorialTarget - tower.aimAngle));
      if (Math.sign(remaining) === direction && Math.abs(remaining) <= Math.abs(rotationDelta)) {
        nextAngle = tutorialTarget;
      }
    }
    tower.aimAngle = nextAngle;
    tower.outputTimer = Math.max(tower.outputTimer, 0.18);
    this.applyTowerAimVisual(tower);
    this.refreshNetworkVisuals();
    this.updateSelectedAimGuide(tower);
    if (tower.group.userData.stageTwoLanceFeeder === true) {
      this.stageTwoLanceFeederIntroduced = this.linkedReceiver(tower)?.type === 'lance';
      if (this.stageTwoLanceFeederIntroduced) {
        this.stopHeldRotation();
        this.audio.ui('confirm');
        this.updateUi(true);
        return;
      }
    }
    if (tutorialTarget !== null && this.isAngleAligned(tower.aimAngle, tutorialTarget)) {
      this.stopHeldRotation();
      this.refreshTutorialProgress();
      this.audio.ui('confirm');
      this.updateUi(true);
    }
  }

  private beginMove(): void {
    if (this.isGuidedTutorialActive()) {
      this.showToast('Di chuyển có phí được mở khóa ở màn 2.', 'info');
      return;
    }
    const tower = this.selectedTower();
    if (!tower) return;
    this.clearLinkMode();
    const cost = TOWER_DEFINITIONS[tower.type].moveCost;
    if (this.money < cost) {
      this.showToast(`Cần thêm ${cost - this.money} Arcana để di chuyển trụ này.`, 'bad');
      return;
    }
    this.interactionMode = 'move';
    this.showToast(`Chọn vùng đặt mới. Phí di chuyển: ${cost}. Các liên kết hợp lệ được giữ nguyên.`, 'info');
    this.updateUi(true);
  }

  private tryMoveTower(towerId: number, gx: number, gz: number): void {
    const tower = this.findTower(towerId);
    if (!tower) return;
    const definition = TOWER_DEFINITIONS[tower.type];
    const placement = this.validateFootprint(tower.type, gx, gz, tower.id);
    if (!placement.valid || placement.layer === null) {
      this.showToast(placement.reason, 'bad');
      return;
    }
    if (this.money < definition.moveCost) return;
    for (const key of tower.cells) this.occupied.delete(key);
    const newCells = this.footprintKeys(tower.type, gx, gz);
    const positions = newCells.map((key) => {
      const cell = this.cells.get(key);
      if (!cell) throw new Error(`Missing cell ${key}`);
      return this.gridToWorld(cell.gx, cell.gz, cell.layer);
    });
    const center = positions.reduce((sum, value) => sum.add(value), new THREE.Vector3()).multiplyScalar(1 / positions.length);
    for (const key of newCells) this.occupied.set(key, tower.id);
    tower.cells.splice(0, tower.cells.length, ...newCells);
    Object.assign(tower, { gx, gz, layer: placement.layer });
    tower.group.position.copy(center);
    tower.outputTimer = 0.45;
    this.sanitizeLinks();
    this.money -= definition.moveCost;
    this.interactionMode = 'inspect';
    this.spawnBurst(center.clone().add(new THREE.Vector3(0, 0.5, 0)), definition.color, 0.38);
    this.showToast(`Đã chuyển ${definition.shortName} tới tầng ${tower.layer}; các liên kết được kiểm tra lại.`, 'good');
    this.refreshNetworkVisuals();
    this.refreshSelectionVisual();
    this.updateUi(true);
  }

  private upgradeSelected(): void {
    if (this.isGuidedTutorialActive()) {
      this.showToast('Nâng cấp trụ được mở khóa ở màn 2.', 'info');
      return;
    }
    const tower = this.selectedTower();
    if (!tower) return;
    if (tower.level >= MAX_TOWER_LEVEL) {
      this.showToast('Trụ này đã đạt cấp tối đa của bản thử.', 'info');
      return;
    }
    const definition = TOWER_DEFINITIONS[tower.type];
    const cost = definition.upgradeCost + (tower.level - 1) * 28;
    if (this.money < cost) {
      this.showToast(`Cần thêm ${cost - this.money} Arcana để nâng cấp.`, 'bad');
      return;
    }
    this.money -= cost;
    tower.totalInvested += cost;
    tower.level += 1;
    tower.group.scale.setScalar(0.9 + (tower.level - 1) * 0.075);
    tower.pulse = 0.5;
    this.spawnBurst(tower.group.position.clone().add(new THREE.Vector3(0, 1, 0)), definition.color, 0.55);
    this.audio.ui('upgrade');
    this.showToast(`${definition.shortName} đã lên cấp ${tower.level}.`, 'good');
    this.refreshNetworkVisuals();
    this.refreshSelectionVisual();
    this.updateUi(true);
  }

  private setAmplifierBranch(branch: 'power' | 'throughput'): void {
    const tower = this.selectedTower();
    if (!tower || tower.type !== 'amplifier') return;
    tower.amplifierBranch = branch;
    this.showToast(`Bộ Khuếch Đại đang tăng ${branch === 'power' ? 'sát thương và sức mạnh nguyên tố' : 'nhịp truyền đạn'}.`, 'good');
    this.audio.ui('confirm');
    this.updateUi(true);
  }

  private sellSelected(): void {
    if (this.isGuidedTutorialActive()) {
      this.showToast('Bán trụ được mở khóa sau màn hướng dẫn.', 'info');
      return;
    }
    const tower = this.selectedTower();
    if (!tower) return;
    const refund = Math.floor(tower.totalInvested * SELL_REFUND);
    this.clearLinkMode();
    for (const key of tower.cells) this.occupied.delete(key);
    const index = this.towers.indexOf(tower);
    if (index >= 0) this.towers.splice(index, 1);
    for (const source of this.towers) {
      if (source.outputTargetId === tower.id) this.setLinkedReceiver(source, null);
    }
    if (this.stageIndex === 1 && this.waveIndex === 3) {
      if (tower.type === 'lance') {
        this.stageTwoLanceIntroduced = false;
        this.stageTwoLanceFeederIntroduced = false;
      } else if (tower.group.userData.stageTwoLanceFeeder === true) {
        this.stageTwoLanceFeederIntroduced = false;
      }
    }
    if (this.stageIndex === 1 && this.waveIndex === 2 && tower.type === 'amplifier') {
      this.stageTwoAmplifierIntroduced = false;
    }
    tower.group.traverse((child) => {
      const pickableIndex = this.towerPickables.indexOf(child);
      if (pickableIndex >= 0) this.towerPickables.splice(pickableIndex, 1);
    });
    this.disposeLanceAmmoBar(tower);
    this.scene.remove(tower.group);
    this.money += refund;
    this.selectedTowerId = null;
    this.interactionMode = 'inspect';
    this.spawnBurst(tower.group.position.clone().add(new THREE.Vector3(0, 0.5, 0)), 0xffd377, 0.35);
    this.audio.ui('sell');
    this.showToast(`Đã bán ${TOWER_DEFINITIONS[tower.type].shortName} được ${refund} Arcana. Đạn trong kho bị mất.`, 'info');
    this.refreshNetworkVisuals();
    this.refreshSelectionVisual();
    this.updateUi(true);
  }

  private cancelInteraction(): void {
    this.clearLinkMode();
    this.selectedBuildType = null;
    this.inspectedBuildType = null;
    this.interactionMode = 'inspect';
    this.showToast(this.selectedTowerId === null ? 'Chọn một trụ hoặc xây trụ mới.' : 'Đã hủy thao tác.', 'info');
    this.refreshSelectionVisual();
    this.updateUi(true);
  }

  private startWave(): void {
    if (this.phase === 'paused') {
      this.setPhase('wave');
      return;
    }
    const waves = this.activeStage().waves;
    if (this.phase !== 'ready' || this.waveIndex >= waves.length) return;
    if (this.isGuidedTutorialActive()) {
      const expectedStep = (ROUTING_MODE === 'link' ? TUTORIAL_WAVE_START_STEPS : ROTATION_TUTORIAL_WAVE_START_STEPS)[this.waveIndex];
      if (expectedStep === undefined || this.tutorialStep !== expectedStep) {
        this.audio.ui('error');
        return;
      }
    }
    if (this.stageTwoRequiredTower() !== null || (this.stageIndex === 1 && this.waveIndex === 3 && !this.stageTwoLanceFeederIntroduced)) {
      this.audio.ui('error');
      return;
    }
    if (this.towers.length === 0) {
      this.showToast('Hãy xây ít nhất một mạng đạn trước khi mở khe nứt.', 'bad');
      return;
    }
    this.waveElapsed = 0;
    this.spawnCursor = 0;
    this.selectedWaveEnemyKind = null;
    this.hoveredWaveEnemyKind = null;
    const finalTutorialStartStep = ROUTING_MODE === 'link' ? 15 : 14;
    if (this.isGuidedTutorialActive() && this.waveIndex === 2 && this.tutorialStep === finalTutorialStartStep) this.tutorialStep = TUTORIAL_STEP_COUNT;
    if (window.matchMedia('(max-width: 700px) and (orientation: portrait)').matches) {
      this.selectedTowerId = null;
      this.refreshSelectionVisual();
    }
    this.setPhase('wave');
    this.audio.wave();
    this.showToast(`Đợt ${this.waveIndex + 1}: ${waves[this.waveIndex].title}`, 'reaction');
  }

  private togglePause(): void {
    if (this.reactionTutorialPopupVisible) return;
    if (this.phase === 'wave') this.setPhase('paused');
    else if (this.phase === 'paused') this.setPhase('wave');
  }

  private setPhase(phase: GamePhase): void {
    this.phase = phase;
    this.audio.setPaused(phase === 'paused');
    this.updateUi(true);
  }

  private update(delta: number, elapsed: number): void {
    this.frame += 1;
    resizeRenderer(this.renderer, this.camera, 1.5);
    this.controls.update();
    if (this.pausedForScreenshot) {
      this.publishDiagnostics();
      return;
    }
    this.elapsed = elapsed;
    const realDelta = Math.min(delta, 0.05);
    this.updateHeldRotation(realDelta);
    this.updateDiscoveryCue(realDelta);
    this.updateReactionTutorialPopup(realDelta);
    if (this.phase === 'wave') {
      this.fixedAccumulator += realDelta * GAME_SPEEDS[this.speedIndex];
      while (this.fixedAccumulator >= FIXED_STEP && this.phase === 'wave') {
        this.simulate(FIXED_STEP);
        this.fixedAccumulator -= FIXED_STEP;
      }
    }
    this.animateWorld(this.reducedMotion ? 0 : realDelta, this.reducedMotion ? 0 : elapsed);
    this.updateLanceAmmoBars();
    this.updateEnemyStatusPresentation();
    this.updateImpactParticles(realDelta);
    this.updateEffects(realDelta);
    this.uiTimer -= realDelta;
    if (this.uiTimer <= 0) {
      this.uiTimer = 0.12;
      this.updateUi(false);
    }
    this.publishDiagnostics();
  }

  private simulate(delta: number): void {
    this.waveElapsed += delta;
    this.updateSpawns();
    this.updateEnemies(delta);
    this.updateTowers(delta);
    this.updateProjectiles(delta);
    this.checkWaveEnd();
  }

  private updateSpawns(): void {
    const wave = this.activeStage().waves[this.waveIndex];
    while (this.spawnCursor < wave.orders.length && wave.orders[this.spawnCursor].at <= this.waveElapsed) {
      const order = wave.orders[this.spawnCursor];
      this.spawnEnemy(order.kind, order.sideOffset);
      this.spawnCursor += 1;
    }
  }

  private spawnEnemy(kind: EnemyKind, sideOffset: number): void {
    const definition = ENEMY_DEFINITIONS[kind];
    const scaledHp = Math.round(definition.hp * this.currentWaveHealthMultiplier());
    const group = this.art.createEnemy(kind);
    const laneOffset = THREE.MathUtils.clamp(sideOffset + (this.rng() - 0.5) * 0.06, -MAX_ENEMY_LANE_OFFSET, MAX_ENEMY_LANE_OFFSET);
    const position = this.pathPosition(0, laneOffset, definition.layer);
    group.position.copy(position);
    this.orientEnemy(group, position, this.pathPosition(0.35, laneOffset, definition.layer));
    const enemy: EnemyState = {
      id: this.nextEnemyId,
      kind,
      group,
      hp: scaledHp,
      maxHp: scaledHp,
      progress: 0,
      sideOffset: laneOffset,
      speedMultiplier: 1,
      dead: false,
      reachedNexus: false,
      burn: 0,
      burnDps: 0,
      chilled: 0,
      frozen: 0,
      gale: 0,
      cracked: 0,
      reactionBarrier: definition.reactionBarrier ?? null,
      barrierBroken: false,
      hitFlash: 0,
    };
    this.nextEnemyId += 1;
    this.enemies.push(enemy);
    this.scene.add(group);
    this.spawnBurst(position.clone().add(new THREE.Vector3(0, 0.5, 0)), definition.color, 0.28);
  }

  private orientEnemy(group: THREE.Group, position: THREE.Vector3, ahead: THREE.Vector3): void {
    group.lookAt(ahead.x, position.y, ahead.z);
    group.rotateY(-Math.PI / 2);
    const desired = ahead.clone().sub(position).setY(0).normalize();
    const facing = new THREE.Vector3(1, 0, 0).applyQuaternion(group.quaternion).setY(0).normalize();
    group.userData.facingError = desired.lengthSq() > 0 && facing.lengthSq() > 0 ? facing.angleTo(desired) : 0;
  }

  private updateEnemies(delta: number): void {
    for (let index = this.enemies.length - 1; index >= 0; index -= 1) {
      const enemy = this.enemies[index];
      const definition = ENEMY_DEFINITIONS[enemy.kind];
      enemy.burn = Math.max(0, enemy.burn - delta);
      enemy.chilled = Math.max(0, enemy.chilled - delta);
      enemy.frozen = Math.max(0, enemy.frozen - delta);
      enemy.gale = Math.max(0, enemy.gale - delta);
      enemy.cracked = Math.max(0, enemy.cracked - delta);
      enemy.hitFlash = Math.max(0, enemy.hitFlash - delta);
      if (enemy.burn > 0) {
        enemy.hp -= enemy.burnDps * delta;
        if (enemy.hp <= 0) {
          this.killEnemy(index, 'Thiêu đốt');
          continue;
        }
      }
      const slow = enemy.frozen > 0 ? 0 : enemy.chilled > 0 ? 0.7 : 1;
      const armorRush = enemy.barrierBroken ? definition.speedAfterBarrierBreak ?? 1 : 1;
      enemy.speedMultiplier = slow * armorRush;
      enemy.progress += definition.speed * ENEMY_SPEED_MULTIPLIER * enemy.speedMultiplier * delta;
      if (enemy.progress >= this.pathTotalLength) {
        enemy.reachedNexus = true;
        const nexusDamage = this.activeStage().tutorial ? 1 : definition.nexusDamage;
        this.lives = Math.max(0, this.lives - nexusDamage);
        const isFirstNexusHit = !this.discoveredCues.has('nexus');
        this.queueDiscoveryCue('nexus', '', { targetSelector: '.metric.lives', highlightOnly: true });
        this.audio.leak();
        this.spawnBurst(this.nexus.position.clone().add(new THREE.Vector3(0, 1.4, 0)), 0xff4f66, 0.68);
        if (!isFirstNexusHit) this.showToast(`${definition.name} đã lọt vào Nexus · −${nexusDamage} mạng`, 'bad');
        this.scene.remove(enemy.group);
        this.disposeEnemy(enemy);
        this.enemies.splice(index, 1);
        if (this.lives <= 0) {
          this.endRun(false);
          return;
        }
        continue;
      }
      const position = this.pathPosition(enemy.progress, enemy.sideOffset, definition.layer);
      enemy.group.position.copy(position);
      const ahead = this.pathPosition(Math.min(this.pathTotalLength, enemy.progress + 0.35), enemy.sideOffset, definition.layer);
      this.orientEnemy(enemy.group, position, ahead);
      enemy.group.scale.setScalar(1 + Math.sin(this.elapsed * 8 + enemy.id) * 0.025);
    }
  }

  private activeEnemyElements(enemy: EnemyState): Element[] {
    const elements: Element[] = [];
    if (enemy.burn > 0) elements.push('fire');
    if (enemy.chilled > 0 || enemy.frozen > 0) elements.push('ice');
    if (enemy.gale > 0) elements.push('wind');
    if (enemy.cracked > 0) elements.push('earth');
    return elements;
  }

  private updateEnemyStatusPresentation(): void {
    const iconCounts = new Map<Element, number>([['fire', 0], ['ice', 0], ['wind', 0], ['earth', 0]]);
    const matrix = new THREE.Matrix4();
    const iconRotation = this.camera.quaternion.clone();
    const iconScale = new THREE.Vector3(1, 1, 1);
    const screenRight = new THREE.Vector3(1, 0, 0).applyQuaternion(iconRotation).normalize();
    const white = new THREE.Color(0xffffff);
    let backdropCount = 0;

    for (const enemy of this.enemies) {
      if (enemy.dead) continue;
      const elements = this.activeEnemyElements(enemy);
      const bodyMaterial = enemy.group.userData.bodyMaterial as THREE.MeshStandardMaterial | undefined;
      if (bodyMaterial) {
        const baseColor = bodyMaterial.userData.baseColor as THREE.Color | undefined;
        const baseEmissive = bodyMaterial.userData.baseEmissive as THREE.Color | undefined;
        const baseIntensity = bodyMaterial.userData.baseEmissiveIntensity as number | undefined;
        if (baseColor) bodyMaterial.color.copy(baseColor);
        if (baseEmissive) bodyMaterial.emissive.copy(baseEmissive);
        bodyMaterial.emissiveIntensity = baseIntensity ?? 0.1;
        if (elements.length > 0) {
          const statusColor = new THREE.Color(0x000000);
          for (const element of elements) statusColor.add(new THREE.Color(ELEMENT_COLORS[element]));
          statusColor.multiplyScalar(1 / elements.length);
          const hsl = { h: 0, s: 0, l: 0 };
          statusColor.getHSL(hsl);
          statusColor.setHSL(hsl.h, Math.max(0.98, hsl.s), THREE.MathUtils.clamp(hsl.l, 0.44, 0.56));
          bodyMaterial.color.lerp(statusColor, elements.length > 1 ? MULTI_ELEMENT_STATUS_TINT : ELEMENT_STATUS_TINT);
          bodyMaterial.emissive.copy(statusColor);
          bodyMaterial.emissiveIntensity = (baseIntensity ?? 0.1) + ELEMENT_STATUS_EMISSIVE_BOOST;
        }
        if (enemy.hitFlash > 0) {
          const flash = THREE.MathUtils.clamp(enemy.hitFlash / 0.14, 0, 1);
          bodyMaterial.color.lerp(white, flash * 0.92);
          bodyMaterial.emissive.copy(white);
          bodyMaterial.emissiveIntensity = (baseIntensity ?? 0.1) + 2.1 * flash;
        }
      }

      if (elements.length === 0) continue;
      const definition = ENEMY_DEFINITIONS[enemy.kind];
      const top = enemy.group.position.clone().add(new THREE.Vector3(0, definition.radius + 0.92 + Math.sin(this.elapsed * 4 + enemy.id) * 0.06, 0));
      elements.forEach((element, elementIndex) => {
        const iconMesh = this.statusIconMeshes.get(element);
        const iconIndex = iconCounts.get(element) ?? 0;
        if (!iconMesh || iconIndex >= iconMesh.instanceMatrix.count || backdropCount >= this.statusIconBackdrop.instanceMatrix.count) return;
        const position = top.clone().addScaledVector(screenRight, (elementIndex - (elements.length - 1) / 2) * 0.58);
        matrix.compose(position, iconRotation, iconScale);
        iconMesh.setMatrixAt(iconIndex, matrix);
        this.statusIconBackdrop.setMatrixAt(backdropCount, matrix);
        iconCounts.set(element, iconIndex + 1);
        backdropCount += 1;
      });
    }

    for (const [element, mesh] of this.statusIconMeshes) {
      mesh.count = iconCounts.get(element) ?? 0;
      mesh.instanceMatrix.needsUpdate = true;
    }
    this.statusIconBackdrop.count = backdropCount;
    this.statusIconBackdrop.instanceMatrix.needsUpdate = true;
  }

  private updateTowers(delta: number): void {
    for (const tower of this.towers) {
      tower.blockedReason = '';
      tower.pulse = Math.max(0, tower.pulse - delta);
      const throughput = this.throughputMultiplier(tower);
      const effectiveCapacity = this.capacity(tower);
      if (tower.type === 'foundry') {
        if (ROUTING_MODE === 'link' && !this.linkedReceiver(tower)) {
          tower.buffer.length = 0;
          tower.blockedReason = 'Chưa liên kết đầu ra';
          tower.produceTimer = Math.min(tower.produceTimer, 0);
          continue;
        }
        tower.produceTimer -= delta;
        if (tower.produceTimer <= 0) {
          const available = effectiveCapacity - tower.buffer.length;
          if (available > 0) {
            const count = tower.level >= 3 ? Math.min(2, available) : 1;
            for (let index = 0; index < count; index += 1) tower.buffer.push(this.createNeutralRound(tower));
            tower.produceTimer += 1.65 / TOWER_FIRE_RATE_MULTIPLIER / throughput / (1 + (tower.level - 1) * 0.16);
            tower.pulse = 0.18;
          } else {
            tower.blockedReason = 'Kho đạn đầy';
            tower.produceTimer = Math.min(tower.produceTimer, 0);
          }
        }
      }
      if (tower.type === 'lance') {
        tower.skillTimer = Math.max(0, tower.skillTimer - delta);
        const threshold = this.lanceThreshold(tower);
        if (tower.buffer.length >= threshold && tower.skillTimer <= 0) this.fireExplosion(tower, threshold);
        continue;
      }
      if (!isAmmoEmitter(tower.type)) continue;
      if (ROUTING_MODE === 'link' && !this.linkedReceiver(tower)) {
        tower.buffer.length = 0;
        tower.blockedReason = 'Chưa liên kết đầu ra';
        continue;
      }
      tower.outputTimer -= delta;
      if (tower.outputTimer <= 0 && tower.buffer.length > 0) this.tryEmit(tower, throughput);
    }
  }

  private createNeutralRound(tower: TowerState): Round {
    const damage = 17 * (1 + (tower.level - 1) * 0.26);
    return { id: this.nextRoundId++, damage, elements: [] };
  }

  private tryEmit(source: TowerState, throughput: number): void {
    const definition = TOWER_DEFINITIONS[source.type];
    const interval = 1 / Math.max(0.1, definition.cadence * TOWER_FIRE_RATE_MULTIPLIER * throughput * (1 + (source.level - 1) * 0.13));
    const receiver = this.linkedReceiver(source);
    if (ROUTING_MODE === 'link' && (!receiver || !this.validateLink(source, receiver).valid)) {
      this.setLinkedReceiver(source, null);
      source.blockedReason = 'Chưa liên kết đầu ra';
      this.refreshNetworkVisuals();
      return;
    }
    if (receiver && receiver.buffer.length >= this.capacity(receiver)) {
      source.blockedReason = `Kho đạn của ${TOWER_DEFINITIONS[receiver.type].shortName} đã đầy`;
      return;
    }
    const round = source.buffer.shift();
    if (!round) return;
    this.launchProjectile(source, ROUTING_MODE === 'link' ? receiver : null, round);
    source.outputTimer += interval;
  }

  private launchProjectile(source: TowerState, target: TowerState | null, sourceRound: Round): void {
    const round: Round = {
      id: sourceRound.id,
      damage: sourceRound.damage * this.powerMultiplier(source),
      elements: [...sourceRound.elements],
    };
    const start = this.towerPort(source);
    const end = target
      ? this.towerPort(target)
      : start.clone().addScaledVector(new THREE.Vector3(Math.cos(source.aimAngle), 0, Math.sin(source.aimAngle)), this.connectionRange(source));
    if (!target) {
      const blockerHit = this.firstBlockerHit(start, end, source.layer);
      if (blockerHit !== null) end.lerpVectors(start, end, Math.max(0, blockerHit - 0.015));
    }
    const mesh = this.art.createProjectile(round.elements);
    mesh.scale.setScalar(PROJECTILE_VISUAL_SCALE * (1 + Math.min(0.42, round.elements.length * 0.1)));
    mesh.position.copy(start);
    this.scene.add(mesh);
    const trailElements: readonly (Element | null)[] = round.elements.length > 0 ? round.elements : [null];
    const trailPositions = new Float32Array(trailElements.length * 6);
    const trailColors = new Float32Array(trailElements.length * 6);
    trailElements.forEach((element, index) => {
      const color = new THREE.Color(element ? ELEMENT_COLORS[element] : 0xffe7a5);
      color.toArray(trailColors, index * 6);
      color.toArray(trailColors, index * 6 + 3);
    });
    const trailGeometry = new THREE.BufferGeometry();
    trailGeometry.setAttribute('position', new THREE.BufferAttribute(trailPositions, 3));
    trailGeometry.setAttribute('color', new THREE.BufferAttribute(trailColors, 3));
    const trail = new THREE.LineSegments(
      trailGeometry,
      new THREE.LineBasicMaterial({
        vertexColors: true,
        transparent: true,
        opacity: round.elements.length > 1 ? 0.98 : 0.76,
        blending: THREE.AdditiveBlending,
        depthWrite: false,
        toneMapped: false,
      }),
    );
    trail.frustumCulled = false;
    this.scene.add(trail);
    const speedBonus = round.elements.includes('wind') ? 1.38 : 1;
    this.projectiles.push({
      id: this.nextProjectileId++,
      mesh,
      trail,
      round,
      sourceTowerId: source.id,
      targetTowerId: target?.id ?? null,
      start,
      end,
      layer: source.layer,
      hitEnemyIds: new Set<number>(),
      progress: 0,
      speed: (8.5 + source.level * 0.7) * speedBonus * PROJECTILE_SPEED_MULTIPLIER,
    });
    if (target) this.linkedProjectileLaunches += 1;
    else this.unlinkedProjectileLaunches += 1;
    if (ROUTING_MODE === 'rotation' && TOWER_DEFINITIONS[source.type].element && !this.linkedReceiver(source)) {
      this.terminalBuffProjectileLaunches += 1;
    }
    this.projectileLaunchesByTower.set(source.id, (this.projectileLaunchesByTower.get(source.id) ?? 0) + 1);
    this.audio.shot(round.elements.length);
    source.pulse = 0.1;
  }

  private updateProjectiles(delta: number): void {
    for (let index = this.projectiles.length - 1; index >= 0; index -= 1) {
      const projectile = this.projectiles[index];
      const previousPosition = projectile.mesh.position.clone();
      const distance = projectile.start.distanceTo(projectile.end);
      projectile.progress += distance <= 0.001 ? 1 : projectile.speed * delta / distance;
      const t = Math.min(1, projectile.progress);
      projectile.mesh.position.lerpVectors(projectile.start, projectile.end, t);
      projectile.mesh.rotation.x += delta * 6;
      projectile.mesh.rotation.y += delta * 9;
      const towerHit = ROUTING_MODE === 'rotation'
        ? this.findTowerIntersection(projectile.sourceTowerId, projectile.layer, previousPosition, projectile.mesh.position)
        : null;
      const collisionLimit = towerHit?.entry ?? 1;

      for (const enemy of this.enemies) {
        if (enemy.dead || projectile.hitEnemyIds.has(enemy.id)) continue;
        const definition = ENEMY_DEFINITIONS[enemy.kind];
        if (definition.layer !== projectile.layer) continue;
        const enemyEntry = segmentSphereEntry(
          previousPosition,
          projectile.mesh.position,
          enemy.group.position,
          definition.radius + PROJECTILE_COLLISION_RADIUS,
        );
        if (enemyEntry === null || enemyEntry > collisionLimit) continue;
        projectile.hitEnemyIds.add(enemy.id);
        const hitPosition = previousPosition.clone().lerp(projectile.mesh.position, enemyEntry);
        const hpBefore = enemy.hp;
        this.applyProjectileHit(projectile.round, enemy, hitPosition);
        if (ROUTING_MODE === 'link') {
          this.linkedSegmentEnemyHits += 1;
          this.linkedSegmentDamage += Math.max(0, hpBefore - enemy.hp);
        }
      }

      if (towerHit) {
        projectile.mesh.position.lerpVectors(previousPosition, projectile.mesh.position, towerHit.entry);
        this.interceptProjectile(towerHit.tower, projectile.round);
        this.removeProjectile(index);
        continue;
      }

      const direction = projectile.end.clone().sub(projectile.start).normalize();
      const side = new THREE.Vector3().crossVectors(direction, new THREE.Vector3(0, 1, 0)).normalize();
      const positions = projectile.trail.geometry.getAttribute('position') as THREE.BufferAttribute;
      const trailCount = Math.max(1, projectile.round.elements.length);
      const trailLength = 0.72 + Math.max(0, trailCount - 1) * 0.24;
      for (let trailIndex = 0; trailIndex < trailCount; trailIndex += 1) {
        const offset = (trailIndex - (trailCount - 1) / 2) * 0.11;
        const end = projectile.mesh.position.clone().addScaledVector(side, offset);
        const trailStart = end.clone().addScaledVector(direction, -trailLength);
        positions.setXYZ(trailIndex * 2, trailStart.x, trailStart.y, trailStart.z);
        positions.setXYZ(trailIndex * 2 + 1, end.x, end.y, end.z);
      }
      positions.needsUpdate = true;
      if (projectile.progress < 1) continue;
      const target = projectile.targetTowerId === null ? null : this.findTower(projectile.targetTowerId);
      if (target && this.towerPort(target).distanceTo(projectile.end) < 0.2) this.interceptProjectile(target, projectile.round);
      this.removeProjectile(index);
    }
  }

  private interceptProjectile(target: TowerState, sourceRound: Round): void {
    if (target.buffer.length >= this.capacity(target)) {
      target.blockedReason = 'Kho đạn đầy — viên đạn đến đã tan biến';
      target.pulse = Math.max(target.pulse, 0.2);
      this.spawnBurst(this.towerPort(target), 0xff6f67, 0.22);
      return;
    }
    const arrivedRound: Round = {
      id: sourceRound.id,
      damage: sourceRound.damage,
      elements: [...sourceRound.elements],
    };
    const element = TOWER_DEFINITIONS[target.type].element;
    if (element && !arrivedRound.elements.includes(element)) {
      arrivedRound.elements.push(element);
      arrivedRound.damage += 3 + target.level * 1.5;
      this.playInfusionFeedback(target, element, arrivedRound.elements.length);
    }
    if (ROUTING_MODE === 'link' && target.type !== 'lance' && !this.linkedReceiver(target)) {
      target.buffer.length = 0;
      target.blockedReason = 'Chưa liên kết đầu ra — đạn đã tiêu tán';
      target.pulse = Math.max(target.pulse, 0.2);
      this.projectileInterceptionCount += 1;
      return;
    }
    target.buffer.push(arrivedRound);
    target.pulse = Math.max(target.pulse, 0.2);
    this.projectileInterceptionCount += 1;
  }

  private applyProjectileHit(round: Round, enemy: EnemyState, position: THREE.Vector3): void {
    const definition = ENEMY_DEFINITIONS[enemy.kind];
    if (definition.layer === 1) this.layerOneEnemyHitCount += 1;
    const incoming = round.elements;
    const existing = new Set<Element>();
    if (enemy.burn > 0) existing.add('fire');
    if (enemy.chilled > 0 || enemy.frozen > 0) existing.add('ice');
    if (enemy.gale > 0) existing.add('wind');
    if (enemy.cracked > 0) existing.add('earth');
    let reaction = REACTION_PAIRS.find((pair) =>
      (existing.has(pair.a) && incoming.includes(pair.b)) || (existing.has(pair.b) && incoming.includes(pair.a)),
    );

    const pairCount = REACTION_PAIRS.filter((pair) => incoming.includes(pair.a) && incoming.includes(pair.b)).length;
    let direct = round.damage * (1 + Math.min(0.72, pairCount * 0.16));
    if (incoming.length > 0) {
      let elementalMultiplier = 0;
      for (const element of incoming) {
        if (definition.immune?.includes(element)) continue;
        let factor = 1;
        if (definition.resist?.includes(element)) factor *= 0.55;
        if (definition.vulnerable?.includes(element)) factor *= 1.35;
        elementalMultiplier += factor / incoming.length;
      }
      direct *= 0.35 + elementalMultiplier * 0.65;
    }
    if (enemy.cracked > 0) direct *= 1.18;
    if (enemy.reactionBarrier !== null && reaction?.name !== enemy.reactionBarrier) {
      direct *= definition.barrierDamageMultiplier ?? 0.22;
    }

    if (reaction) {
      this.resolveReaction(reaction.name, reaction.color, round, enemy, position);
      if (enemy.reactionBarrier === reaction.name) {
        enemy.reactionBarrier = null;
        enemy.barrierBroken = true;
        const barrier = enemy.group.getObjectByName('barrier');
        if (barrier) barrier.visible = false;
        const armorShell = enemy.group.getObjectByName('armorShell');
        if (armorShell) armorShell.visible = false;
        direct *= 1.55;
        if (definition.speedAfterBarrierBreak) {
          enemy.speedMultiplier = definition.speedAfterBarrierBreak;
          this.showToast(`${reaction.name} phá giáp ${definition.name} · Tăng tốc!`, 'reaction');
        } else {
          this.showToast(`${reaction.name} đã phá vỡ lá chắn Hộ Vệ!`, 'reaction');
        }
      }
      this.clearElementState(enemy, reaction.a);
      this.clearElementState(enemy, reaction.b);
    }

    this.applyDamage(enemy, direct, position, incoming.length > 1 ? elementList(incoming) : '', this.mixedColor(incoming));
    if (enemy.dead) return;
    const hadIce = enemy.chilled > 0;
    for (const element of incoming) {
      if (definition.immune?.includes(element)) continue;
      if (element === 'fire') {
        enemy.burn = Math.max(enemy.burn, 3.5);
        enemy.burnDps = Math.max(enemy.burnDps, 4.5 + round.damage * 0.08);
      } else if (element === 'ice') {
        enemy.chilled = Math.max(enemy.chilled, 3.4);
        if (hadIce) enemy.frozen = Math.max(enemy.frozen, ['brute', 'warder', 'skyWarder', 'colossus'].includes(definition.kind) ? 0.3 : 0.75);
      } else if (element === 'wind') enemy.gale = Math.max(enemy.gale, 3.2);
      else enemy.cracked = Math.max(enemy.cracked, 4.5);
    }

    if (pairCount > 0) this.applyFusionPayload(round, enemy, position);
  }

  private resolveReaction(name: string, color: number, round: Round, enemy: EnemyState, position: THREE.Vector3): void {
    this.reactionCount += 1;
    const reactionPair = REACTION_PAIRS.find((pair) => pair.name === name);
    const isLinkTutorialReaction = ROUTING_MODE === 'link'
      && this.activeStage().tutorial
      && this.waveIndex === 2
      && this.tutorialStep === TUTORIAL_STEP_COUNT
      && !this.discoveredCues.has('reaction');
    if (reactionPair && !isLinkTutorialReaction) {
      this.queueDiscoveryCue(
        'reaction',
        `<i data-cue-element="${reactionPair.a}">${this.elementCueGlyph(reactionPair.a)}</i><b>+</b><i data-cue-element="${reactionPair.b}">${this.elementCueGlyph(reactionPair.b)}</i><b>→</b><i>✹</i>`,
        { worldPosition: position.clone().add(new THREE.Vector3(0, 2.2, 0)), duration: 2.8 },
      );
    }
    if (isLinkTutorialReaction) {
      this.discoveredCues.add('reaction');
      this.discoveryCueTriggerCounts.reaction += 1;
      this.reactionTutorialPopupDelay = TUTORIAL_REACTION_POPUP_DELAY;
      this.updateUi(true);
    }
    const bonus = 18 + round.damage * 0.38 + enemy.maxHp * REACTION_MAX_HP_DAMAGE_RATIO;
    this.lastReactionBonusDamage = bonus;
    if (name === 'Sốc Nhiệt') {
      this.applyDamage(enemy, bonus, position, name, color);
      enemy.frozen = Math.max(enemy.frozen, 0.18);
    } else if (name === 'Hỏa Hoạn') {
      this.damageArea(enemy, 2.4, bonus * 0.5, 'fire');
    } else if (name === 'Phun Trào') {
      this.damageArea(enemy, 2.1, bonus * 0.68, 'earth');
    } else if (name === 'Đông Cứng Nhanh') {
      enemy.frozen = Math.max(enemy.frozen, 1.2);
      this.damageArea(enemy, 2.6, bonus * 0.34, 'ice');
    } else if (name === 'Vỡ Tinh Thể') {
      enemy.cracked = Math.max(enemy.cracked, 6);
      this.damageArea(enemy, 2.1, bonus * 0.58, 'ice');
    } else {
      enemy.cracked = Math.max(enemy.cracked, 5);
      enemy.progress = Math.max(0, enemy.progress - 0.7);
      this.damageArea(enemy, 2.6, bonus * 0.42, 'wind');
    }
    this.spawnReactionVfx(position, color, name);
    this.audio.reaction();
  }

  private updateReactionTutorialPopup(delta: number): void {
    if (this.reactionTutorialPopupDelay < 0 || this.reactionTutorialPopupVisible) return;
    this.reactionTutorialPopupDelay -= delta;
    if (this.reactionTutorialPopupDelay > 0) return;
    this.reactionTutorialPopupDelay = -1;
    this.reactionTutorialPopupVisible = true;
    this.reactionTutorialElement.classList.remove('hidden');
    this.reactionTutorialElement.setAttribute('aria-hidden', 'false');
    this.setPhase('paused');
  }

  private dismissReactionTutorialPopup(): void {
    if (!this.reactionTutorialPopupVisible) return;
    this.reactionTutorialPopupVisible = false;
    this.reactionTutorialElement.classList.add('hidden');
    this.reactionTutorialElement.setAttribute('aria-hidden', 'true');
    this.tutorialStep = TUTORIAL_STEP_COUNT;
    if (this.phase === 'paused') this.setPhase('wave');
    else this.updateUi(true);
  }

  private applyFusionPayload(round: Round, enemy: EnemyState, position: THREE.Vector3): void {
    if (round.elements.includes('fire') && round.elements.includes('wind')) {
      this.damageArea(enemy, 1.8, round.damage * 0.14, 'fire');
    }
    if (round.elements.includes('fire') && round.elements.includes('earth')) {
      this.damageArea(enemy, 1.55, round.damage * 0.17, 'earth');
    }
    if (round.elements.includes('ice') && round.elements.includes('wind')) enemy.chilled = Math.max(enemy.chilled, 4.4);
    if (round.elements.includes('ice') && round.elements.includes('earth')) enemy.cracked = Math.max(enemy.cracked, 5.2);
    this.spawnBurst(position, this.mixedColor(round.elements), 0.22);
  }

  private damageArea(center: EnemyState, radius: number, damage: number, sourceElement: Element): void {
    const layer = ENEMY_DEFINITIONS[center.kind].layer;
    for (const enemy of this.enemies) {
      if (enemy.dead || ENEMY_DEFINITIONS[enemy.kind].layer !== layer) continue;
      if (enemy.group.position.distanceTo(center.group.position) > radius) continue;
      const definition = ENEMY_DEFINITIONS[enemy.kind];
      const adjusted = definition.immune?.includes(sourceElement) ? 0 : definition.resist?.includes(sourceElement) ? damage * 0.55 : damage;
      if (adjusted > 0) this.applyDamage(enemy, adjusted, enemy.group.position, 'Diện rộng', ELEMENT_COLORS[sourceElement]);
    }
  }

  private applyDamage(enemy: EnemyState, damage: number, position: THREE.Vector3, label: string, impactColor = 0xffe9a8): void {
    if (enemy.dead || damage <= 0) return;
    enemy.hp -= damage;
    enemy.hitFlash = 0.14;
    this.spawnImpactParticles(position.clone().add(new THREE.Vector3(0, 0.35, 0)), impactColor);
    this.spawnDamageText(position.clone().add(new THREE.Vector3(0, 0.62, 0)), Math.max(1, Math.round(damage)), label);
    this.audio.hit();
    if (enemy.hp <= 0) {
      const index = this.enemies.indexOf(enemy);
      if (index >= 0) this.killEnemy(index, label);
    }
  }

  private spawnImpactParticles(position: THREE.Vector3, color: number): void {
    this.impactParticleBursts += 1;
    const particleColor = new THREE.Color(color);
    const count = this.reducedMotion ? 4 : 8;
    for (let index = 0; index < count; index += 1) {
      const angle = index / count * Math.PI * 2 + (this.frame % 7) * 0.13;
      const speed = 1.25 + (index % 3) * 0.34;
      this.impactParticleStates.push({
        position: position.clone(),
        velocity: new THREE.Vector3(Math.cos(angle) * speed, 0.7 + (index % 2) * 0.55, Math.sin(angle) * speed),
        color: particleColor.clone(),
        life: 0.28,
        maxLife: 0.28,
      });
    }
    if (this.impactParticleStates.length > 160) {
      this.impactParticleStates.splice(0, this.impactParticleStates.length - 160);
    }
  }

  private updateImpactParticles(delta: number): void {
    for (let index = this.impactParticleStates.length - 1; index >= 0; index -= 1) {
      const particle = this.impactParticleStates[index];
      particle.life -= delta;
      if (particle.life <= 0) {
        this.impactParticleStates.splice(index, 1);
        continue;
      }
      particle.velocity.y -= delta * 3.8;
      particle.position.addScaledVector(particle.velocity, delta);
    }
    const positionAttribute = this.impactParticles.geometry.getAttribute('position') as THREE.BufferAttribute;
    const colorAttribute = this.impactParticles.geometry.getAttribute('color') as THREE.BufferAttribute;
    this.impactParticleStates.forEach((particle, index) => {
      const ratio = particle.life / particle.maxLife;
      positionAttribute.setXYZ(index, particle.position.x, particle.position.y, particle.position.z);
      colorAttribute.setXYZ(index, particle.color.r * ratio, particle.color.g * ratio, particle.color.b * ratio);
    });
    this.impactParticles.geometry.setDrawRange(0, this.impactParticleStates.length);
    positionAttribute.needsUpdate = true;
    colorAttribute.needsUpdate = true;
  }

  private killEnemy(index: number, cause: string): void {
    const enemy = this.enemies[index];
    if (!enemy || enemy.dead) return;
    enemy.dead = true;
    const definition = ENEMY_DEFINITIONS[enemy.kind];
    const reward = this.enemyKillReward(enemy.kind);
    this.money += reward;
    this.spawnBurst(enemy.group.position.clone().add(new THREE.Vector3(0, 0.6, 0)), definition.color, 0.62);
    if (cause && cause !== 'Diện rộng') this.showToast(`Đã hạ ${definition.name}${cause ? ` · ${cause}` : ''} · +${reward} Arcana`, 'good');
    this.audio.destroy();
    this.scene.remove(enemy.group);
    this.disposeEnemy(enemy);
    this.enemies.splice(index, 1);
  }

  private disposeEnemy(enemy: EnemyState): void {
    enemy.group.traverse((child) => {
      if (child instanceof THREE.Mesh) child.geometry.dispose();
    });
    const material = enemy.group.userData.bodyMaterial as THREE.Material | undefined;
    material?.dispose();
    const barrier = enemy.group.getObjectByName('barrier');
    if (barrier instanceof THREE.Mesh) {
      const barrierMaterial = barrier.material;
      if (barrierMaterial instanceof THREE.Material) barrierMaterial.dispose();
    }
  }

  private clearElementState(enemy: EnemyState, element: Element): void {
    if (element === 'fire') enemy.burn = 0;
    else if (element === 'ice') {
      enemy.chilled = 0;
      enemy.frozen = 0;
    } else if (element === 'wind') enemy.gale = 0;
    else enemy.cracked = 0;
  }

  private fireExplosion(tower: TowerState, threshold: number): void {
    const targets = this.enemies.filter((enemy) => {
      if (enemy.dead || ENEMY_DEFINITIONS[enemy.kind].layer !== tower.layer) return false;
      const dx = enemy.group.position.x - tower.group.position.x;
      const dz = enemy.group.position.z - tower.group.position.z;
      return Math.hypot(dx, dz) <= EXPLOSION_RADIUS;
    });
    if (targets.length === 0) {
      tower.blockedReason = 'Đợi kẻ địch trong vùng nổ';
      return;
    }
    const consumed = tower.buffer.splice(0, threshold);
    const elements = uniqueElements(consumed.flatMap((round) => round.elements));
    const averageDamage = consumed.reduce((sum, round) => sum + round.damage, 0) / Math.max(1, consumed.length);
    const round: Round = {
      id: this.nextRoundId++,
      damage: averageDamage * (2.2 + tower.level * 0.42),
      elements,
    };
    const center = tower.group.position.clone();
    center.y += 0.18;
    this.createExplosionVfx(center, elements);
    let totalDamage = 0;
    for (const enemy of targets) {
      const hpBefore = enemy.hp;
      this.applyProjectileHit(round, enemy, enemy.group.position.clone());
      this.spawnBurst(enemy.group.position.clone().add(new THREE.Vector3(0, 0.12, 0)), this.mixedColor(elements), 0.7);
      totalDamage += Math.max(0, hpBefore - enemy.hp);
    }
    this.lastExplosionHitCount = targets.length;
    this.lastExplosionTargetCueCount = targets.length;
    this.lastExplosionDamage = totalDamage;
    tower.skillTimer = 2.2 / TOWER_FIRE_RATE_MULTIPLIER;
    tower.pulse = 0.6;
    this.audio.special();
    this.showToast(`Nổ Arcana phát nổ với ${elementList(elements)}.`, 'reaction');
  }

  private createExplosionVfx(center: THREE.Vector3, elements: readonly Element[]): void {
    const color = this.mixedColor(elements);
    const group = new THREE.Group();
    group.position.copy(center);
    group.userData.effectKind = 'explosion';
    group.userData.anchorStart = center.clone();
    group.userData.radius = EXPLOSION_RADIUS;
    const zone = new THREE.Mesh(
      new THREE.CircleGeometry(EXPLOSION_RADIUS, 40),
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.3, side: THREE.DoubleSide, blending: THREE.AdditiveBlending, depthWrite: false }),
    );
    zone.name = 'explosion-zone';
    zone.rotation.x = -Math.PI / 2;
    zone.position.y = 0.035;
    zone.scale.setScalar(0.18);
    zone.userData.vfxRole = 'zone';
    zone.userData.baseOpacity = 0.3;
    group.add(zone);

    const outerCore = new THREE.Mesh(
      new THREE.SphereGeometry(0.78, 16, 10),
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.82, blending: THREE.AdditiveBlending, depthWrite: false }),
    );
    outerCore.name = 'explosion-core';
    outerCore.position.y = 0.5;
    outerCore.userData.vfxRole = 'core';
    outerCore.userData.baseOpacity = 0.82;
    group.add(outerCore);
    const innerCore = new THREE.Mesh(
      new THREE.SphereGeometry(0.38, 14, 9),
      new THREE.MeshBasicMaterial({ color: 0xfff8dd, transparent: true, opacity: 0.96, blending: THREE.AdditiveBlending, depthWrite: false }),
    );
    innerCore.name = 'explosion-core';
    innerCore.position.y = 0.52;
    innerCore.userData.vfxRole = 'core';
    innerCore.userData.baseOpacity = 0.96;
    group.add(innerCore);

    const ringRadii = [0.68, 1.1, 1.52, 1.94] as const;
    for (let index = 0; index < ringRadii.length; index += 1) {
      const radius = ringRadii[index];
      const ring = new THREE.Mesh(
        new THREE.TorusGeometry(radius, 0.115 - index * 0.012, 7, 36),
        new THREE.MeshBasicMaterial({ color: index === ringRadii.length - 1 ? 0xffefae : color, transparent: true, opacity: 0.92 - index * 0.08, blending: THREE.AdditiveBlending, depthWrite: false }),
      );
      ring.name = 'explosion-ring';
      ring.rotation.x = Math.PI / 2;
      ring.position.y = 0.08 + index * 0.045;
      ring.scale.setScalar(0.2);
      ring.userData.vfxRole = 'ring';
      ring.userData.delay = index * 0.045;
      ring.userData.baseOpacity = 0.92 - index * 0.08;
      group.add(ring);
    }
    for (let index = 0; index < 18; index += 1) {
      const angle = index / 18 * Math.PI * 2;
      const shard = new THREE.Mesh(
        new THREE.OctahedronGeometry(0.13 + (index % 3) * 0.025, 0),
        new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.92, blending: THREE.AdditiveBlending, depthWrite: false }),
      );
      shard.name = 'explosion-shard';
      shard.position.set(Math.cos(angle) * 0.5, 0.3 + (index % 3) * 0.12, Math.sin(angle) * 0.5);
      shard.userData.vfxRole = 'shard';
      shard.userData.baseOpacity = 0.92;
      shard.userData.direction = new THREE.Vector3(Math.cos(angle) * 0.92, 0.16 + (index % 2) * 0.12, Math.sin(angle) * 0.92);
      group.add(shard);
    }
    this.effectsGroup.add(group);
    this.effects.push({ object: group, life: 0.92, maxLife: 0.92, rises: false, scales: false });
  }

  private checkWaveEnd(): void {
    if (this.phase !== 'wave') return;
    const waves = this.activeStage().waves;
    const wave = waves[this.waveIndex];
    if (this.spawnCursor < wave.orders.length || this.enemies.length > 0) return;
    const clearReward = this.waveClearReward(wave.clearBonus);
    this.money += clearReward;
    this.waveIndex += 1;
    if (this.waveIndex >= waves.length) {
      this.endRun(true);
      return;
    }
    if (this.stageIndex === 0 && this.waveIndex === 3) {
      this.tutorialStep = TUTORIAL_STEP_COUNT;
      this.lives = this.activeStage().startingLives;
      this.captureMasteryCheckpoint();
      this.selectedTowerId = null;
      this.clearLinkMode();
      this.interactionMode = 'inspect';
      this.refreshSelectionVisual();
    } else if (this.isGuidedTutorialActive()) {
      this.tutorialStep = ROUTING_MODE === 'link'
        ? this.waveIndex === 1 ? 6 : 11
        : this.waveIndex === 1 ? 5 : 9;
      this.selectedTowerId = null;
      this.clearLinkMode();
      this.interactionMode = 'inspect';
      this.refreshSelectionVisual();
    }
    this.setPhase('ready');
    this.showToast(`Đã dọn sạch đợt · +${clearReward} Arcana.`, 'good');
    this.audio.waveClear();
  }

  private captureMasteryCheckpoint(): void {
    if (this.stageIndex !== 0 || this.waveIndex !== 3) return;
    this.masteryCheckpoint = {
      money: this.money,
      towers: this.towers.map((tower) => ({
        id: tower.id,
        type: tower.type,
        gx: tower.gx,
        gz: tower.gz,
        level: tower.level,
        totalInvested: tower.totalInvested,
        buffer: tower.buffer.map((round) => ({ id: round.id, damage: round.damage, elements: [...round.elements] })),
        outputTargetId: tower.outputTargetId,
        aimAngle: tower.aimAngle,
        produceTimer: tower.produceTimer,
        outputTimer: tower.outputTimer,
        skillTimer: tower.skillTimer,
        amplifierBranch: tower.amplifierBranch,
      })),
      nextTowerId: this.nextTowerId,
      nextRoundId: this.nextRoundId,
      discoveredCues: [...this.discoveredCues],
    };
  }

  private restoreMasteryCheckpoint(): void {
    const checkpoint = this.masteryCheckpoint;
    if (!checkpoint) {
      this.resetRun();
      return;
    }
    this.resetRun();
    this.masteryCheckpoint = checkpoint;
    this.tutorialStep = TUTORIAL_STEP_COUNT;
    this.waveIndex = 3;
    this.money = Number.MAX_SAFE_INTEGER;
    const restoredIds = new Map<number, number>();
    for (const snapshot of checkpoint.towers) {
      const priorCount = this.towers.length;
      if (!this.tryPlaceTower(snapshot.type, snapshot.gx, snapshot.gz) || this.towers.length === priorCount) continue;
      const tower = this.towers[this.towers.length - 1];
      restoredIds.set(snapshot.id, tower.id);
      tower.level = snapshot.level;
      tower.totalInvested = snapshot.totalInvested;
      tower.buffer = snapshot.buffer.map((round) => ({ id: round.id, damage: round.damage, elements: [...round.elements] }));
      tower.aimAngle = snapshot.aimAngle;
      tower.produceTimer = snapshot.produceTimer;
      tower.outputTimer = snapshot.outputTimer;
      tower.skillTimer = snapshot.skillTimer;
      tower.amplifierBranch = snapshot.amplifierBranch;
      this.applyTowerAimVisual(tower);
    }
    checkpoint.towers.forEach((snapshot) => {
      const restoredSourceId = restoredIds.get(snapshot.id);
      const source = restoredSourceId === undefined ? undefined : this.findTower(restoredSourceId);
      if (!source) return;
      source.outputTargetId = snapshot.outputTargetId === null ? null : restoredIds.get(snapshot.outputTargetId) ?? null;
      this.applyTowerAimVisual(source);
    });
    this.clearTransientEffects();
    this.nextTowerId = checkpoint.nextTowerId;
    this.nextRoundId = checkpoint.nextRoundId;
    this.money = checkpoint.money;
    this.lives = this.activeStage().startingLives;
    this.waveElapsed = 0;
    this.spawnCursor = 0;
    this.fixedAccumulator = 0;
    this.discoveredCues.clear();
    checkpoint.discoveredCues.forEach((cue) => this.discoveredCues.add(cue));
    this.hideDiscoveryCue();
    this.discoveryCueQueue.length = 0;
    this.selectedTowerId = null;
    this.selectedBuildType = null;
    this.inspectedBuildType = null;
    this.interactionMode = 'inspect';
    this.phase = 'ready';
    this.resultElement.classList.add('hidden');
    this.clearLinkMode();
    this.refreshNetworkVisuals();
    this.refreshSelectionVisual();
    this.showToast('Thử lại từ đợt tự xây mạch. Hãy đổi chiến thuật phản ứng nguyên tố.', 'info');
    this.updateUi(true);
  }

  private endRun(won: boolean): void {
    this.phase = won ? 'won' : 'lost';
    this.stageCleared = won;
    this.resultElement.classList.remove('hidden');
    this.getElement('#result-kicker').textContent = won ? (this.stageIndex === 0 ? 'MẠCH ĐÃ ĐƯỢC CHỨNG NHẬN' : 'MẠNG LƯỚI ỔN ĐỊNH') : 'NEXUS SỤP ĐỔ';
    this.getElement('#result-title').textContent = won ? (this.stageIndex === 0 ? 'Hoàn thành mạch hướng dẫn' : 'Arcane Arsenal trụ vững') : 'Khe nứt đã xuyên thủng phòng tuyến';
    this.getElement('#result-copy').textContent = won
      ? `Đã hoàn thành ${this.activeStage().title} với ${this.lives} mạng Nexus và ${this.towers.length} trụ.`
      : 'Hãy xây đường truyền ngắn hơn, giải phóng kho đạn bị nghẽn và dành mạng riêng cho từng tầng bị đe dọa.';
    this.getButton('#result-restart').textContent = won && this.stageIndex < STAGES.length - 1
      ? `Vào màn ${this.stageIndex + 2}`
      : !won && this.masteryCheckpoint && this.isTutorialMasteryPhase() ? 'Thử lại 3 đợt'
        : 'Chơi lại màn';
    if (won) this.audio.win();
    else this.audio.lose();
    this.updateUi(true);
  }

  private resetRun(): void {
    this.cancelBuildDrag(true);
    this.stopHeldRotation();
    this.hideDiscoveryCue();
    this.clearTransientEffects();
    this.discoveryCueQueue.length = 0;
    for (const projectile of [...this.projectiles]) {
      const index = this.projectiles.indexOf(projectile);
      if (index >= 0) this.removeProjectile(index);
    }
    for (const enemy of this.enemies) {
      this.scene.remove(enemy.group);
      this.disposeEnemy(enemy);
    }
    this.enemies.length = 0;
    for (const tower of this.towers) {
      this.disposeLanceAmmoBar(tower);
      this.scene.remove(tower.group);
    }
    this.towers.length = 0;
    this.towerPickables.length = 0;
    this.occupied.clear();
    this.networkGroup.clear();
    this.selectionGroup.clear();
    this.masteryCheckpoint = null;
    this.money = this.activeStage().startingMoney;
    this.lives = this.activeStage().startingLives;
    this.waveIndex = 0;
    this.waveElapsed = 0;
    this.spawnCursor = 0;
    this.fixedAccumulator = 0;
    this.nextTowerId = 1;
    this.nextEnemyId = 1;
    this.nextRoundId = 1;
    this.nextProjectileId = 1;
    this.infusionCount = 0;
    this.projectileInterceptionCount = 0;
    this.layerOneEnemyHitCount = 0;
    this.reactionCount = 0;
    this.lastReactionBonusDamage = 0;
    this.impactParticleBursts = 0;
    this.lanceVfxMaxAnchorError = 0;
    this.lanceVfxMaxScaleError = 0;
    this.lastExplosionTargetCueCount = 0;
    this.impactParticleStates.length = 0;
    this.impactParticles.geometry.setDrawRange(0, 0);
    for (const mesh of this.statusIconMeshes.values()) mesh.count = 0;
    this.statusIconBackdrop.count = 0;
    this.tutorialStep = 0;
    this.reactionTutorialPopupDelay = -1;
    this.reactionTutorialPopupVisible = false;
    this.reactionTutorialElement.classList.add('hidden');
    this.reactionTutorialElement.setAttribute('aria-hidden', 'true');
    this.linkSourceTowerId = null;
    this.lastLinkAttempt = null;
    this.linkedProjectileLaunches = 0;
    this.unlinkedProjectileLaunches = 0;
    this.terminalBuffProjectileLaunches = 0;
    this.linkedSegmentEnemyHits = 0;
    this.linkedSegmentDamage = 0;
    this.projectileLaunchesByTower.clear();
    this.stageTwoAmplifierIntroduced = false;
    this.stageTwoLanceIntroduced = false;
    this.stageTwoLanceFeederIntroduced = false;
    this.stageTwoRotationLessonPair = null;
    this.stageTwoLessonCells.clear();
    this.canvas.classList.remove('link-mode-active', 'link-target-valid', 'link-target-invalid');
    this.stageCleared = false;
    this.selectedTowerId = null;
    this.selectedBuildType = null;
    this.inspectedBuildType = null;
    this.selectedWaveEnemyKind = null;
    this.hoveredWaveEnemyKind = null;
    this.interactionMode = 'inspect';
    this.phase = 'ready';
    this.resultElement.classList.add('hidden');
    const board = this.activeStage().board;
    this.controls.target.set(...board.cameraTarget);
    this.camera.position.set(...board.cameraPosition);
    this.controls.maxDistance = Math.max(48, board.islandRadius * 1.9);
    this.controls.update();
    this.audio.reset();
    if (this.activeStage().tutorial) this.clearToast();
    else this.showToast('Màn mới: tự do xây trước hoặc trong khi đợt địch diễn ra.', 'info');
    this.updateUi(true);
  }

  private animateWorld(delta: number, elapsed: number): void {
    const core = this.nexus.getObjectByName('nexusCore');
    const halo = this.nexus.getObjectByName('nexusHalo');
    if (core) {
      core.rotation.y += delta * 1.2;
      core.position.y = 1.62 + Math.sin(elapsed * 1.8) * 0.12;
    }
    if (halo) {
      halo.rotation.z += delta * 0.72;
      halo.rotation.x = Math.PI / 2 + Math.sin(elapsed * 0.8) * 0.12;
    }
    const spawnMarker = this.boardGroup.getObjectByName('enemy-spawn-direction');
    if (spawnMarker) {
      const pulse = 1 + (Math.sin(elapsed * 4.2) * 0.5 + 0.5) * 0.09;
      spawnMarker.scale.setScalar(pulse);
    }
    for (const tower of this.towers) {
      const spinner = tower.group.getObjectByName('spinner');
      if (spinner) spinner.rotation.z += delta * (tower.blockedReason ? 0.35 : 1.4);
      const coreObject = tower.group.getObjectByName('elementCore') ?? tower.group.getObjectByName('amplifierCore');
      if (coreObject) coreObject.rotation.y += delta * 1.7;
      const scale = 0.9 + (tower.level - 1) * 0.075 + tower.pulse * 0.08;
      tower.group.scale.lerp(new THREE.Vector3(scale, scale, scale), Math.min(1, delta * 12));
    }
    for (const enemy of this.enemies) {
      const spinner = enemy.group.getObjectByName('spinner');
      if (spinner) spinner.rotation.z += delta * 2.4;
      const tails = enemy.group.children.filter((child) => child.name === 'tail');
      tails.forEach((tail, index) => { tail.rotation.y = Math.sin(elapsed * 5 + index) * 0.5; });
    }
    for (const cue of this.tutorialCueGroup.children) {
      const baseY = cue.userData.baseY as number | undefined;
      if (baseY !== undefined) cue.position.y = baseY + Math.sin(elapsed * 4.6) * 0.12;
      const pulse = 1 + (Math.sin(elapsed * 5.2) * 0.5 + 0.5) * 0.12;
      cue.scale.setScalar(pulse);
      cue.rotation.y += delta * 0.45;
    }
  }

  private spawnBurst(position: THREE.Vector3, color: number, size: number): void {
    const group = new THREE.Group();
    const ring = new THREE.Mesh(
      new THREE.RingGeometry(size * 0.35, size * 0.48, 24),
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.86, side: THREE.DoubleSide, depthWrite: false }),
    );
    ring.rotation.x = -Math.PI / 2;
    group.add(ring);
    for (let index = 0; index < 6; index += 1) {
      const shard = new THREE.Mesh(
        new THREE.TetrahedronGeometry(size * 0.14, 0),
        new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.9, depthWrite: false }),
      );
      const angle = index / 6 * Math.PI * 2;
      shard.position.set(Math.cos(angle) * size * 0.35, 0.08, Math.sin(angle) * size * 0.35);
      shard.userData.direction = new THREE.Vector3(Math.cos(angle), 0.45, Math.sin(angle));
      group.add(shard);
    }
    group.position.copy(position);
    this.effectsGroup.add(group);
    this.effects.push({ object: group, life: 0.48, maxLife: 0.48, rises: false });
  }

  private spawnReactionVfx(position: THREE.Vector3, color: number, name: string): void {
    this.spawnBurst(position, color, 0.9);
    this.spawnDamageText(position.clone().add(new THREE.Vector3(0, 1.1, 0)), 0, name);
  }

  private playInfusionFeedback(tower: TowerState, element: Element, stackSize: number): void {
    const position = this.towerPort(tower);
    const color = ELEMENT_COLORS[element];
    this.spawnInfusionRing(position, color, 0.36 + Math.min(3, stackSize) * 0.08);
    this.spawnDamageText(
      position.clone().add(new THREE.Vector3(0, 0.62, 0)),
      0,
      stackSize > 1 ? `KẾT HỢP ×${stackSize}` : `+ ${ELEMENT_NAMES[element].toUpperCase()}`,
    );
    tower.pulse = Math.max(tower.pulse, 0.28);
    this.infusionCount += 1;
    this.audio.infuse(stackSize);
  }

  private spawnInfusionRing(position: THREE.Vector3, color: number, size: number): void {
    const ring = new THREE.Mesh(
      new THREE.RingGeometry(size * 0.38, size * 0.58, 24),
      new THREE.MeshBasicMaterial({
        color,
        transparent: true,
        opacity: 0.92,
        side: THREE.DoubleSide,
        depthWrite: false,
        blending: THREE.AdditiveBlending,
      }),
    );
    ring.rotation.x = -Math.PI / 2;
    ring.position.copy(position);
    this.effectsGroup.add(ring);
    this.effects.push({ object: ring, life: 0.62, maxLife: 0.62, rises: false });
  }

  private spawnDamageText(position: THREE.Vector3, damage: number, label: string): void {
    const canvas = document.createElement('canvas');
    canvas.width = 256;
    canvas.height = 96;
    const context = canvas.getContext('2d');
    if (!context) return;
    context.font = label && damage === 0 ? '700 28px Segoe UI' : '900 36px Segoe UI';
    context.textAlign = 'center';
    context.textBaseline = 'middle';
    context.lineWidth = 8;
    context.strokeStyle = 'rgba(15,20,30,0.86)';
    const text = damage > 0 ? `−${damage}${label ? ` ${label}` : ''}` : label;
    context.strokeText(text, 128, 48);
    context.fillStyle = damage > 0 ? '#fff2bd' : '#ffffff';
    context.fillText(text, 128, 48);
    const texture = new THREE.CanvasTexture(canvas);
    texture.colorSpace = THREE.SRGBColorSpace;
    const material = new THREE.SpriteMaterial({ map: texture, transparent: true, depthWrite: false });
    const sprite = new THREE.Sprite(material);
    sprite.position.copy(position);
    sprite.scale.set(2.8, 1.05, 1);
    this.effectsGroup.add(sprite);
    this.effects.push({ object: sprite, life: 0.72, maxLife: 0.72, rises: true });
  }

  private updateEffects(delta: number): void {
    for (let index = this.effects.length - 1; index >= 0; index -= 1) {
      const effect = this.effects[index];
      effect.life -= delta;
      const ratio = Math.max(0, effect.life / effect.maxLife);
      const progress = 1 - ratio;
      const isExplosion = effect.object.userData.effectKind === 'explosion';
      if (effect.rises) effect.object.position.y += delta * 1.25;
      if (!isExplosion && effect.scales !== false) effect.object.scale.setScalar(1 + progress * 0.6);
      effect.object.traverse((child) => {
        if (!(child instanceof THREE.Mesh || child instanceof THREE.Sprite)) return;
        const material = child.material;
        const direction = child.userData.direction as THREE.Vector3 | undefined;
        if (!isExplosion) {
          if (material instanceof THREE.Material) material.opacity = ratio;
          if (direction) child.position.addScaledVector(direction, delta * 1.7);
          return;
        }
        const baseOpacity = Number(child.userData.baseOpacity ?? 1);
        const fade = progress < 0.68 ? 1 : THREE.MathUtils.clamp((1 - progress) / 0.32, 0, 1);
        const role = child.userData.vfxRole as string | undefined;
        if (role === 'zone') {
          const expansion = THREE.MathUtils.smoothstep(progress, 0, 0.34);
          child.scale.setScalar(0.18 + expansion * 0.82);
          if (material instanceof THREE.Material) material.opacity = baseOpacity * fade * (0.86 + Math.sin(progress * Math.PI * 4) * 0.14);
        } else if (role === 'ring') {
          const delay = Number(child.userData.delay ?? 0);
          const localProgress = THREE.MathUtils.clamp((progress - delay) / 0.46, 0, 1);
          child.scale.setScalar(0.2 + THREE.MathUtils.smoothstep(localProgress, 0, 1) * 0.8);
          if (material instanceof THREE.Material) material.opacity = baseOpacity * fade * (1 - localProgress * 0.42);
        } else if (role === 'core') {
          const corePulse = Math.sin(THREE.MathUtils.clamp(progress / 0.4, 0, 1) * Math.PI);
          child.scale.setScalar(0.82 + corePulse * 0.9);
          if (material instanceof THREE.Material) material.opacity = baseOpacity * fade;
        } else {
          if (material instanceof THREE.Material) material.opacity = baseOpacity * fade;
          if (direction) child.position.addScaledVector(direction, delta * 1.35);
          child.rotation.x += delta * 5;
          child.rotation.y += delta * 6;
        }
      });
      if (isExplosion) {
        const anchor = effect.object.userData.anchorStart as THREE.Vector3 | undefined;
        if (anchor) this.lanceVfxMaxAnchorError = Math.max(this.lanceVfxMaxAnchorError, effect.object.position.distanceTo(anchor));
        const zone = effect.object.getObjectByName('explosion-zone');
        const visualRadius = zone ? EXPLOSION_RADIUS * zone.scale.x : 0;
        this.lanceVfxMaxScaleError = Math.max(this.lanceVfxMaxScaleError, Math.max(0, visualRadius - EXPLOSION_RADIUS));
      }
      if (effect.life > 0) continue;
      this.effectsGroup.remove(effect.object);
      effect.object.traverse((child) => {
        if (child instanceof THREE.Sprite) {
          const material = child.material;
          material.map?.dispose();
          material.dispose();
        } else if (child instanceof THREE.Mesh) {
          child.geometry.dispose();
          const material = child.material;
          if (Array.isArray(material)) material.forEach((entry) => entry.dispose());
          else material.dispose();
        }
      });
      this.effects.splice(index, 1);
    }
  }

  private clearTransientEffects(): void {
    for (const effect of this.effects.splice(0)) {
      this.effectsGroup.remove(effect.object);
      effect.object.traverse((child) => {
        if (child instanceof THREE.Sprite) {
          child.material.map?.dispose();
          child.material.dispose();
        } else if (child instanceof THREE.Mesh) {
          child.geometry.dispose();
          const material = child.material;
          if (Array.isArray(material)) material.forEach((entry) => entry.dispose());
          else material.dispose();
        }
      });
    }
  }

  private refreshNetworkVisuals(): void {
    this.networkGroup.traverse((child) => {
      if (child instanceof THREE.Line || child instanceof THREE.Mesh) {
        child.geometry.dispose();
        const material = child.material;
        if (Array.isArray(material)) material.forEach((entry) => entry.dispose());
        else material.dispose();
      }
    });
    this.networkGroup.clear();
    for (const tower of this.towers) {
      if (ROUTING_MODE === 'rotation' && (isAmmoEmitter(tower.type) || tower.type === 'lance')) this.applyTowerAimVisual(tower);
      const target = this.linkedReceiver(tower);
      if (ROUTING_MODE === 'link') {
        if (!target || !this.validateLink(tower, target).valid) {
          if (tower.outputTargetId !== null || tower.aimAngle !== 0) this.setLinkedReceiver(tower, null);
          continue;
        }
        this.setLinkedReceiver(tower, target);
      }
      if (!target) continue;
      const start = this.towerPort(tower);
      const end = this.towerPort(target);
      const available = target.buffer.length < this.capacity(target);
      const color = available ? this.towerSignalColor(tower) : 0xff3f55;
      const geometry = new THREE.BufferGeometry().setFromPoints([start, end]);
      const material = new THREE.LineBasicMaterial({ color, transparent: true, opacity: available ? 0.58 : 0.88 });
      const line = new THREE.Line(geometry, material);
      line.name = `tower-link-line-${tower.id}-${target.id}`;
      this.networkGroup.add(line);
      const arrow = new THREE.Mesh(new THREE.ConeGeometry(0.17, 0.52, 7), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.84 }));
      arrow.name = `tower-link-arrow-${tower.id}-${target.id}`;
      arrow.position.lerpVectors(start, end, 0.62);
      arrow.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), end.clone().sub(start).normalize());
      arrow.rotateX(Math.PI / 2);
      this.networkGroup.add(arrow);
    }
  }

  private refreshPlacementPreview(type: TowerType, cell: CellState | null, valid: boolean): void {
    const key = `${type}:${cell ? gridKey(cell.gx, cell.gz) : 'off-grid'}:${valid}:${this.towers.length}`;
    if (key === this.placementPreviewKey) return;
    this.clearPlacementPreview();
    this.placementPreviewKey = key;

    const gridGeometry = new THREE.PlaneGeometry(1.72, 1.72);
    const gridMaterial = new THREE.MeshBasicMaterial({
      vertexColors: true,
      transparent: true,
      opacity: 0.2,
      side: THREE.DoubleSide,
      depthWrite: false,
    });
    const grid = new THREE.InstancedMesh(gridGeometry, gridMaterial, this.cells.size);
    const rotation = new THREE.Quaternion().setFromEuler(new THREE.Euler(-Math.PI / 2, 0, 0));
    const matrix = new THREE.Matrix4();
    let gridIndex = 0;
    for (const candidate of this.cells.values()) {
      const position = this.gridToWorld(candidate.gx, candidate.gz, candidate.layer).add(new THREE.Vector3(0, 0.055, 0));
      matrix.compose(position, rotation, new THREE.Vector3(1, 1, 1));
      grid.setMatrixAt(gridIndex, matrix);
      grid.setColorAt(gridIndex, new THREE.Color(candidate.buildable ? 0x66e8bf : 0x26394b));
      gridIndex += 1;
    }
    grid.instanceMatrix.needsUpdate = true;
    if (grid.instanceColor) grid.instanceColor.needsUpdate = true;
    grid.renderOrder = 2;
    this.placementPreviewGroup.add(grid);

    for (const tower of this.towers) {
      const radius = tower.type === 'amplifier'
        ? this.amplifierRange(tower)
        : tower.type === 'lance' ? EXPLOSION_RADIUS : this.connectionRange(tower);
      if (radius <= 0) continue;
      const disc = this.createRangeDisc(radius, TOWER_DEFINITIONS[tower.type].color, 0.09);
      disc.position.copy(tower.group.position).add(new THREE.Vector3(0, 0.04, 0));
      this.placementPreviewGroup.add(disc);
    }

    if (!cell) return;
    const keys = this.footprintKeys(type, cell.gx, cell.gz);
    const footprintGeometry = new THREE.PlaneGeometry(1.82, 1.82);
    const footprintMaterial = new THREE.MeshBasicMaterial({
      color: valid ? 0x77f3bd : 0xff5b63,
      transparent: true,
      opacity: 0.58,
      side: THREE.DoubleSide,
      depthWrite: false,
    });
    const footprint = new THREE.InstancedMesh(footprintGeometry, footprintMaterial, Math.max(1, keys.length));
    const positions: THREE.Vector3[] = [];
    keys.forEach((cellKeyValue, index) => {
      const footprintCell = this.cells.get(cellKeyValue);
      if (!footprintCell) return;
      const position = this.gridToWorld(footprintCell.gx, footprintCell.gz, footprintCell.layer).add(new THREE.Vector3(0, 0.075, 0));
      positions.push(position);
      matrix.compose(position, rotation, new THREE.Vector3(1, 1, 1));
      footprint.setMatrixAt(index, matrix);
    });
    footprint.count = positions.length;
    footprint.instanceMatrix.needsUpdate = true;
    footprint.renderOrder = 3;
    this.placementPreviewGroup.add(footprint);
    if (positions.length === 0) return;

    const center = positions.reduce((sum, position) => sum.add(position), new THREE.Vector3()).multiplyScalar(1 / positions.length);
    const ghost = this.createPlacementGhost(type, valid);
    ghost.position.copy(center);
    this.placementPreviewGroup.add(ghost);
    const definition = TOWER_DEFINITIONS[type];
    const previewRange = type === 'lance' ? EXPLOSION_RADIUS : definition.connectionRange;
    if (previewRange > 0) {
      const disc = this.createRangeDisc(previewRange, valid ? definition.color : 0xff5b63, 0.16);
      disc.position.copy(center).add(new THREE.Vector3(0, 0.045, 0));
      this.placementPreviewGroup.add(disc);
    }
  }

  private createPlacementGhost(type: TowerType, valid: boolean): THREE.Group {
    const definition = TOWER_DEFINITIONS[type];
    const [width, depth] = definition.footprint;
    const color = valid ? definition.color : 0xff5b63;
    const material = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.56, depthWrite: false });
    const accent = new THREE.MeshBasicMaterial({ color: valid ? 0xffffff : 0xffb2b2, transparent: true, opacity: 0.78, depthWrite: false });
    const group = new THREE.Group();
    const base = new THREE.Mesh(new THREE.BoxGeometry(width * 1.46, 0.34, depth * 1.46), material);
    base.position.y = 0.18;
    const core = new THREE.Mesh(new THREE.OctahedronGeometry(0.38 + Math.max(width, depth) * 0.08, 0), accent);
    core.position.y = 0.82;
    group.add(base, core);
    group.renderOrder = 4;
    return group;
  }

  private createRangeDisc(radius: number, color: number, opacity: number): THREE.Mesh {
    const disc = new THREE.Mesh(
      new THREE.CircleGeometry(radius, 56),
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity, side: THREE.DoubleSide, depthWrite: false }),
    );
    disc.rotation.x = -Math.PI / 2;
    disc.renderOrder = 1;
    return disc;
  }

  private clearPlacementPreview(): void {
    this.placementPreviewGroup.traverse((child) => {
      if (!(child instanceof THREE.Mesh || child instanceof THREE.Line || child instanceof THREE.Points)) return;
      child.geometry.dispose();
      const material = child.material;
      if (Array.isArray(material)) material.forEach((entry) => entry.dispose());
      else material.dispose();
    });
    this.placementPreviewGroup.clear();
    this.placementPreviewKey = '';
  }

  private refreshSelectionVisual(): void {
    this.selectionGroup.traverse((child) => {
      if (child instanceof THREE.Line || child instanceof THREE.Mesh) {
        child.geometry.dispose();
        const material = child.material;
        if (Array.isArray(material)) material.forEach((entry) => entry.dispose());
        else material.dispose();
      }
    });
    this.selectionGroup.clear();
    const tower = this.selectedTower();
    if (!tower) return;
    const radius = tower.type === 'amplifier'
      ? this.amplifierRange(tower)
      : tower.type === 'lance' ? EXPLOSION_RADIUS : this.connectionRange(tower);
    if (radius > 0) {
      const disc = this.createRangeDisc(radius, TOWER_DEFINITIONS[tower.type].color, 0.15);
      disc.position.copy(tower.group.position).add(new THREE.Vector3(0, 0.035, 0));
      this.selectionGroup.add(disc);
      const points: THREE.Vector3[] = [];
      for (let index = 0; index < 64; index += 1) {
        const angle = index / 64 * Math.PI * 2;
        points.push(new THREE.Vector3(Math.cos(angle) * radius, 0.05, Math.sin(angle) * radius));
      }
      const line = new THREE.LineLoop(
        new THREE.BufferGeometry().setFromPoints(points),
        new THREE.LineBasicMaterial({ color: 0xffe58e, transparent: true, opacity: 0.78 }),
      );
      line.position.copy(tower.group.position);
      this.selectionGroup.add(line);
    }
    if (ROUTING_MODE === 'link' && this.interactionMode === 'link' && this.linkSourceTowerId === tower.id) {
      for (const target of this.validLinkTargets(tower)) {
        const highlight = new THREE.Mesh(
          new THREE.RingGeometry(0.98, 1.24, 32),
          new THREE.MeshBasicMaterial({ color: 0x55ffc7, transparent: true, opacity: 0.94, side: THREE.DoubleSide, depthWrite: false }),
        );
        highlight.name = `link-target-valid-${target.id}`;
        highlight.rotation.x = -Math.PI / 2;
        highlight.position.copy(target.group.position).add(new THREE.Vector3(0, 0.08, 0));
        highlight.renderOrder = 13;
        this.selectionGroup.add(highlight);
      }
    }
    if (ROUTING_MODE === 'rotation' && (isAmmoEmitter(tower.type) || tower.type === 'lance')) {
      const aimGuide = new THREE.Mesh(
        new THREE.CylinderGeometry(SELECTED_AIM_GUIDE_RADIUS, SELECTED_AIM_GUIDE_RADIUS, 1, 12, 1, true),
        new THREE.MeshBasicMaterial({
          color: 0xff3344,
          transparent: true,
          opacity: SELECTED_AIM_GUIDE_OPACITY,
          depthTest: false,
          depthWrite: false,
          toneMapped: false,
        }),
      );
      aimGuide.name = 'weapon-aim-selected';
      aimGuide.renderOrder = 12;
      aimGuide.frustumCulled = false;
      this.selectionGroup.add(aimGuide);
      this.updateSelectedAimGuide(tower);
    }
    const marker = new THREE.Mesh(new THREE.RingGeometry(0.92, 1.05, 28), new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.84, side: THREE.DoubleSide }));
    marker.rotation.x = -Math.PI / 2;
    marker.position.copy(tower.group.position).add(new THREE.Vector3(0, 0.04, 0));
    this.selectionGroup.add(marker);
  }

  private updateSelectedAimGuide(tower: TowerState): void {
    if (ROUTING_MODE !== 'rotation' || tower.id !== this.selectedTowerId) return;
    const guide = this.selectionGroup.getObjectByName('weapon-aim-selected');
    if (!(guide instanceof THREE.Mesh)) return;
    const start = this.towerPort(tower);
    const end = start.clone().addScaledVector(
      new THREE.Vector3(Math.cos(tower.aimAngle), 0, Math.sin(tower.aimAngle)),
      this.connectionRange(tower),
    );
    const blockerHit = this.firstBlockerHit(start, end, tower.layer);
    if (blockerHit !== null) end.lerpVectors(start, end, Math.max(0, blockerHit - 0.015));
    const length = start.distanceTo(end);
    guide.visible = length > 0.001;
    if (!guide.visible) return;
    guide.position.lerpVectors(start, end, 0.5);
    guide.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), end.clone().sub(start).normalize());
    guide.scale.set(1, length, 1);
  }

  private updateUi(_force: boolean): void {
    const waves = this.activeStage().waves;
    this.getElement('#lives-value').textContent = String(this.lives).padStart(2, '0');
    this.getElement('#money-value').textContent = String(this.money).padStart(4, '0');
    this.getElement('#wave-value').textContent = `${Math.min(this.waveIndex + 1, waves.length)} / ${waves.length}`;
    this.getElement('#enemy-value').textContent = String(this.enemies.length).padStart(2, '0');
    this.canvas.classList.toggle('link-mode-active', this.interactionMode === 'link');
    document.body.classList.toggle('link-mode-active', this.interactionMode === 'link');
    this.getElement('#layer-value').textContent = this.selectedTower() ? String(this.selectedTower()?.layer) : '—';
    const waveButton = this.getButton('#start-wave');
    const tutorialStartStep = (ROUTING_MODE === 'link' ? TUTORIAL_WAVE_START_STEPS : ROTATION_TUTORIAL_WAVE_START_STEPS)[this.waveIndex];
    waveButton.disabled = this.phase !== 'ready'
      || (this.isGuidedTutorialActive() && (tutorialStartStep === undefined || this.tutorialStep !== tutorialStartStep))
      || this.stageTwoRequiredTower() !== null
      || (this.stageIndex === 1 && this.waveIndex === 3 && !this.stageTwoLanceFeederIntroduced);
    waveButton.textContent = this.phase === 'ready' ? `Bắt đầu đợt ${this.waveIndex + 1}` : this.phase === 'wave' ? 'Đợt đang diễn ra' : this.phase === 'paused' ? 'Đã tạm dừng' : 'Màn đã hoàn thành';
    this.getButton('#pause-button').textContent = this.phase === 'paused' ? '▶' : 'Ⅱ';
    this.getButton('#speed-button').textContent = `×${GAME_SPEEDS[this.speedIndex]}`;
    this.getButton('#sound-button').textContent = this.audio.isMuted() ? '🔇' : '◖))';
    this.toastElement.textContent = this.lastToast.text;
    this.toastElement.dataset.tone = this.lastToast.tone;
    this.toastElement.classList.toggle('hidden', this.lastToast.text.length === 0);
    for (const button of this.buildList.querySelectorAll<HTMLButtonElement>('[data-tower-type]')) {
      const type = button.dataset.towerType as TowerType;
      const definition = TOWER_DEFINITIONS[type];
      const lessonFree = this.isMandatoryLessonPurchase(type);
      const purchaseCost = this.towerPurchaseCost(type);
      const unlocked = this.isTowerUnlocked(type);
      button.disabled = !unlocked || !this.canPurchaseTower(type);
      button.classList.toggle('locked', !unlocked);
      button.dataset.locked = unlocked ? 'false' : 'true';
      button.dataset.lessonFree = String(lessonFree);
      button.classList.toggle('selected', this.interactionMode === 'build' && this.selectedBuildType === type);
      button.querySelector<HTMLElement>('.build-copy b')!.textContent = String(purchaseCost);
      const groupLabel = BUILD_GROUPS.find((group) => group.types.includes(type))?.label ?? definition.role;
      button.setAttribute('aria-label', `${groupLabel}: ${definition.name}, giá ${purchaseCost}`);
    }
    for (const button of this.buildList.querySelectorAll<HTMLButtonElement>('[data-tower-info]')) {
      const type = button.dataset.towerInfo as TowerType;
      const active = this.inspectedBuildType === type;
      button.classList.toggle('active', active);
      button.setAttribute('aria-pressed', String(active));
    }
    this.renderInspector();
    this.renderWaveIntel();
    this.renderTutorialObjective();
  }

  private renderInspector(): void {
    const tower = this.selectedTower();
    const portraitMobile = window.matchMedia('(max-width: 700px) and (orientation: portrait)').matches;
    const selectedTowerPoint = portraitMobile && tower
      ? this.worldToClient(tower.group.position.clone().add(new THREE.Vector3(0, 0.7, 0)))
      : null;
    document.body.classList.toggle('inspector-dock-left', Boolean(selectedTowerPoint && selectedTowerPoint.x >= window.innerWidth * 0.5));
    const rotateLeftButton = this.getButton('#action-left');
    const rotateRightButton = this.getButton('#action-right');
    const ammoMagazine = this.getElement('#ammo-magazine');
    const catalogDefinition = tower || this.inspectedBuildType === null ? null : TOWER_DEFINITIONS[this.inspectedBuildType];
    const detailStats = this.getElement('#tower-detail-stats');
    const closeDetail = this.getButton('#inspector-close-detail');
    const catalogView = catalogDefinition !== null;
    this.inspectorElement.classList.toggle('tutorial-stage', this.isGuidedTutorialActive());
    this.inspectorElement.classList.toggle('catalog-view', catalogView);
    this.inspectorElement.classList.toggle('empty', !tower && !catalogView);
    this.getElement('#inspector-heading').textContent = catalogView ? 'CHI TIẾT TRỤ' : 'NÚT ĐẠN';
    detailStats.classList.toggle('hidden', !catalogView);
    closeDetail.classList.toggle('hidden', !catalogView);
    if (catalogDefinition && this.inspectedBuildType) {
      rotateLeftButton.classList.add('hidden');
      rotateRightButton.classList.add('hidden');
      ammoMagazine.classList.add('hidden');
      const unlocked = this.isTowerUnlocked(this.inspectedBuildType);
      const range = this.inspectedBuildType === 'lance' ? EXPLOSION_RADIUS : catalogDefinition.connectionRange;
      const capacityStat = this.inspectedBuildType === 'lance'
        ? `<div><dt>KHO</dt><dd>${catalogDefinition.capacity} ô</dd></div>`
        : '';
      this.getElement('#inspector-name').textContent = catalogDefinition.name;
      this.getElement('#inspector-role').textContent = `${catalogDefinition.role} · ${unlocked ? 'Đã mở' : 'Chưa mở'}`;
      detailStats.innerHTML = `<div><dt>GIÁ</dt><dd>${this.towerPurchaseCost(this.inspectedBuildType)}</dd></div><div><dt>VÙNG ĐẶT</dt><dd>${catalogDefinition.footprint[0]}×${catalogDefinition.footprint[1]}</dd></div><div><dt>TẦM</dt><dd>${range.toFixed(1)}</dd></div>${capacityStat}`;
      this.getElement('#inspector-detail').textContent = `${catalogDefinition.description}\n\nNâng cấp: ${this.towerUpgradeSummary(this.inspectedBuildType)}`;
      this.getElement('#branch-controls').classList.add('hidden');
      return;
    }
    if (!tower) {
      ammoMagazine.classList.add('hidden');
      this.getElement('#inspector-name').textContent = 'Chưa chọn trụ';
      this.getElement('#inspector-role').textContent = 'Chạm vào trụ để xem mạng liên kết.';
      this.getElement('#buffer-fill').style.width = '0%';
      this.getElement('#buffer-text').textContent = '0 / 0';
      this.getElement('#inspector-detail').textContent = 'Có thể xây khi đợt đang diễn ra. Nhấn 1–7 để chọn nhanh trụ.';
      this.getElement('#branch-controls').classList.add('hidden');
      rotateLeftButton.classList.add('hidden');
      rotateRightButton.classList.add('hidden');
      return;
    }
    const definition = TOWER_DEFINITIONS[tower.type];
    const showsAmmo = tower.type === 'lance';
    const capacity = showsAmmo ? this.capacity(tower) : 0;
    const occupancy = showsAmmo ? tower.buffer.length : 0;
    ammoMagazine.classList.toggle('hidden', !showsAmmo);
    this.getElement('#inspector-name').textContent = `${definition.name} · C${tower.level}`;
    this.getElement('#inspector-role').textContent = `${definition.role} · Tầng ${tower.layer}`;
    this.getElement('#buffer-fill').style.width = `${capacity === 0 ? 0 : Math.min(100, occupancy / capacity * 100)}%`;
    this.getElement('#buffer-fill').style.setProperty('--buffer-color', `#${this.towerSignalColor(tower).toString(16).padStart(6, '0')}`);
    this.getElement('#buffer-text').textContent = showsAmmo ? `${occupancy} / ${capacity}` : '';
    const receiver = this.linkedReceiver(tower);
    const head = showsAmmo ? tower.buffer[0] : undefined;
    const state = tower.blockedReason ? `BỊ CHẶN: ${tower.blockedReason}` : this.phase === 'paused' ? 'Đã tạm dừng' : 'Luồng đạn sẵn sàng';
    const angle = Math.round(THREE.MathUtils.radToDeg(tower.aimAngle));
    const output = ROUTING_MODE === 'link'
      ? receiver
        ? `Liên kết → ${TOWER_DEFINITIONS[receiver.type].shortName}`
        : isAmmoEmitter(tower.type) ? 'Chưa có liên kết đầu ra'
          : tower.type === 'lance' ? 'Tự nổ khi đầy và địch cùng tầng vào vùng' : 'Hào quang hỗ trợ'
      : receiver
        ? `Đường đạn đi xuyên → ${TOWER_DEFINITIONS[receiver.type].shortName}`
        : isAmmoEmitter(tower.type) ? `Bắn tự do · ${angle}°`
          : tower.type === 'lance' ? 'Vụ nổ bán kính một ô' : 'Hào quang hỗ trợ';
    const ammoDetail = showsAmmo ? `\nĐạn tích: ${head ? elementList(head.elements) : 'trống'}` : '';
    const rangeLabel = tower.type === 'lance' ? 'Bán kính nổ' : ROUTING_MODE === 'link' ? 'Tầm liên kết' : 'Tầm bắn';
    const displayedRange = tower.type === 'lance' ? EXPLOSION_RADIUS : this.connectionRange(tower);
    this.getElement('#inspector-detail').textContent = `${state}\n${output}${ammoDetail}\n${rangeLabel}: ${displayedRange.toFixed(1)} · ${definition.description}`;
    const branchControls = this.getElement('#branch-controls');
    branchControls.classList.toggle('hidden', tower.type !== 'amplifier' || this.isGuidedTutorialActive());
    this.getButton('#branch-power').classList.toggle('active', tower.amplifierBranch === 'power');
    this.getButton('#branch-throughput').classList.toggle('active', tower.amplifierBranch === 'throughput');
    const tutorialRotationStep = this.tutorialRotationTower()?.id === tower.id;
    const canRotate = isAmmoEmitter(tower.type) || tower.type === 'lance';
    rotateLeftButton.classList.toggle('hidden', ROUTING_MODE !== 'rotation' || !canRotate || (this.isGuidedTutorialActive() && !tutorialRotationStep));
    rotateRightButton.classList.toggle('hidden', ROUTING_MODE !== 'rotation' || !canRotate || (this.isGuidedTutorialActive() && !tutorialRotationStep));
    rotateLeftButton.disabled = !canRotate;
    rotateRightButton.disabled = !canRotate;
    const upgradeCost = definition.upgradeCost + (tower.level - 1) * 28;
    this.getButton('#action-upgrade').disabled = this.isGuidedTutorialActive() || tower.level >= MAX_TOWER_LEVEL || this.money < upgradeCost;
    this.getButton('#action-upgrade').textContent = tower.level >= MAX_TOWER_LEVEL ? 'Cấp tối đa' : `Nâng ${upgradeCost}`;
    this.getButton('#action-move').textContent = `Dời ${definition.moveCost}`;
    this.getButton('#action-move').disabled = this.isGuidedTutorialActive() || this.money < definition.moveCost;
    this.getButton('#action-sell').textContent = `Bán ${Math.floor(tower.totalInvested * SELL_REFUND)}`;
    this.getButton('#action-sell').disabled = this.isGuidedTutorialActive();
  }

  private towerUpgradeSummary(type: TowerType): string {
    if (type === 'foundry') return 'tăng sát thương, tốc độ sinh đạn và tầm liên kết; cấp 3 sinh hai viên mỗi nhịp.';
    if (type === 'amplifier') return 'mở rộng hào quang và tăng hiệu lực của nhánh Sức Mạnh hoặc Tốc Độ.';
    if (type === 'lance') return 'tăng sát thương và giảm số đạn cần để kích hoạt; bán kính nổ luôn là một ô.';
    return 'tăng tầm liên kết, nhịp truyền và lực nguyên tố cộng vào viên đạn.';
  }

  private renderWaveIntel(preserveRoster = false): void {
    const waves = this.activeStage().waves;
    const wave = waves[Math.min(this.waveIndex, waves.length - 1)];
    const intel = this.getElement('#wave-intel');
    const detail = this.getElement('#wave-enemy-detail');
    const visible = this.phase === 'ready';
    intel.classList.toggle('hidden', !visible);
    document.body.classList.toggle('wave-intel-ready', visible);
    if (!visible) {
      document.body.classList.remove('wave-enemy-detail-open');
      detail.classList.add('hidden');
      return;
    }

    const counts = new Map<EnemyKind, number>();
    for (const order of wave.orders) counts.set(order.kind, (counts.get(order.kind) ?? 0) + 1);
    const kinds = [...counts.keys()];
    if (this.selectedWaveEnemyKind !== null && !counts.has(this.selectedWaveEnemyKind)) this.selectedWaveEnemyKind = null;
    if (this.hoveredWaveEnemyKind !== null && !counts.has(this.hoveredWaveEnemyKind)) this.hoveredWaveEnemyKind = null;
    const detailKind = this.selectedWaveEnemyKind ?? this.hoveredWaveEnemyKind;
    const roster = this.getElement('#wave-enemies');
    const rosterKey = `${this.stageIndex}:${this.waveIndex}:${kinds.map((kind) => `${kind}:${counts.get(kind) ?? 0}`).join('|')}`;
    if (!preserveRoster && this.waveIntelRosterKey !== rosterKey) {
      roster.innerHTML = kinds.map((kind) => {
        const definition = ENEMY_DEFINITIONS[kind];
        const selected = detailKind === kind;
        const count = counts.get(kind) ?? 0;
        const movement = definition.layer === 0 ? 'MẶT ĐẤT' : `BAY · TẦNG ${definition.layer}`;
        const movementLabel = definition.layer === 0 ? 'mặt đất' : `bay ở tầng ${definition.layer}`;
        return `<button class="wave-enemy-chip${selected ? ' active' : ''}" type="button" data-enemy-kind="${kind}" aria-expanded="${selected}" aria-controls="wave-enemy-detail" aria-label="Xem ${definition.name}, ${count} kẻ sắp tới, ${movementLabel}"><i style="--enemy-color:#${definition.color.toString(16).padStart(6, '0')}">${ENEMY_GLYPHS[kind]}</i><span>${definition.name}</span><b>×${count}</b><em data-flight="${definition.layer === 0 ? 'ground' : 'flying'}">${movement}</em></button>`;
      }).join('');
      this.waveIntelRosterKey = rosterKey;
    }
    for (const button of roster.querySelectorAll<HTMLButtonElement>('[data-enemy-kind]')) {
      const active = button.dataset.enemyKind === detailKind;
      button.classList.toggle('active', active);
      button.setAttribute('aria-expanded', String(active));
    }

    if (detailKind === null) {
      this.waveIntelDetailKey = '';
      document.body.classList.remove('wave-enemy-detail-open');
      detail.classList.add('hidden');
      detail.replaceChildren();
      return;
    }

    const definition = ENEMY_DEFINITIONS[detailKind];
    document.body.classList.add('wave-enemy-detail-open');
    const color = `#${definition.color.toString(16).padStart(6, '0')}`;
    const profiles: string[] = [];
    if (definition.vulnerable?.length) profiles.push(`<span data-tone="weak">Yếu · ${definition.vulnerable.map((element) => ELEMENT_NAMES[element]).join(' / ')}</span>`);
    if (definition.resist?.length) profiles.push(`<span data-tone="resist">Kháng · ${definition.resist.map((element) => ELEMENT_NAMES[element]).join(' / ')}</span>`);
    if (definition.immune?.length) profiles.push(`<span data-tone="immune">Miễn nhiễm · ${definition.immune.map((element) => ELEMENT_NAMES[element]).join(' / ')}</span>`);
    if (definition.reactionBarrier) profiles.push(`<span data-tone="barrier">Phá bằng · ${definition.reactionBarrier}</span>`);
    if (definition.speedAfterBarrierBreak) profiles.push(`<span data-tone="weak">Sau vỡ giáp · Tăng tốc ×${definition.speedAfterBarrierBreak.toFixed(2)}</span>`);
    if (profiles.length === 0) profiles.push('<span data-tone="neutral">Không có kháng tính nguyên tố</span>');
    const reward = this.enemyKillReward(detailKind);
    const effectiveHp = Math.round(definition.hp * this.currentWaveHealthMultiplier());
    const movementBadge = definition.layer === 0 ? 'MẶT ĐẤT' : 'BAY TRÊN KHÔNG';
    const movementDetail = definition.layer === 0 ? 'Di chuyển trên đường bộ' : `Tầng bay ${definition.layer}`;
    const detailKey = `${this.stageIndex}:${this.waveIndex}:${detailKind}:${effectiveHp}:${reward}`;
    if (this.waveIntelDetailKey !== detailKey) {
      const displayedNexusDamage = this.activeStage().tutorial ? 1 : definition.nexusDamage;
      detail.innerHTML = `<div class="enemy-detail-title"><i style="--enemy-color:${color}">${ENEMY_GLYPHS[definition.kind]}</i><div><span class="enemy-movement-badge" data-flight="${definition.layer === 0 ? 'ground' : 'flying'}">${movementBadge}</span><strong>${definition.name}</strong><small>${movementDetail}</small></div></div><dl class="enemy-detail-stats"><div><dt>MÁU</dt><dd>${effectiveHp}</dd></div><div><dt>TỐC</dt><dd>${(definition.speed * ENEMY_SPEED_MULTIPLIER).toFixed(2)}</dd></div><div><dt>MẠNG</dt><dd>−${displayedNexusDamage}</dd></div><div><dt>THƯỞNG</dt><dd>+${reward}</dd></div></dl><div class="enemy-detail-profile">${profiles.join('')}</div><button class="enemy-detail-close" type="button" data-close-wave-intel aria-label="Đóng chi tiết kẻ địch">×</button>`;
      this.waveIntelDetailKey = detailKey;
    }
    detail.classList.remove('hidden');
  }

  private renderTutorialObjective(): void {
    const visible = (this.activeStage().tutorial || this.isStageTwoLessonWave())
      && this.phase !== 'won' && this.phase !== 'lost';
    this.updateTutorialCue(visible);
  }

  private updateTutorialCue(visible: boolean): void {
    for (const element of document.querySelectorAll<HTMLElement>('.tutorial-focus')) {
      element.classList.remove('tutorial-focus');
      element.removeAttribute('data-tutorial-focus');
    }
    if (this.stageIndex === 1) {
      this.updateStageTwoTutorialCue(visible);
      return;
    }
    if (!visible || this.tutorialStep >= TUTORIAL_STEP_COUNT) {
      this.hideTutorialHand();
      this.clearTutorialCue();
      return;
    }

    let focusSelector = '';
    let worldPosition: THREE.Vector3 | null = null;
    let dragPlacement = false;
    const foundry = this.findTutorialTower('foundry');
    const fire = this.findTutorialTower('fire');
    const ice = this.findTutorialTower('ice');
    const terminalFire = this.findTutorialTower('terminalFire');
    if (ROUTING_MODE === 'link') {
      if (this.tutorialStep === 0) {
        focusSelector = '[data-tower-type="foundry"]';
        worldPosition = this.findTutorialPlacementWorld('foundry');
        dragPlacement = true;
      } else if (this.tutorialStep === 1) {
        focusSelector = '[data-tower-type="fire"]';
        worldPosition = this.findTutorialPlacementWorld('fire');
        dragPlacement = true;
      } else if (this.tutorialStep === 2) worldPosition = foundry?.group.position.clone() ?? null;
      else if (this.tutorialStep === 3) {
        worldPosition = fire?.group.position.clone() ?? null;
        dragPlacement = true;
      }
      else if (this.tutorialStep === 4) worldPosition = fire?.group.position.clone() ?? null;
      else if (this.tutorialStep === 5) focusSelector = '#start-wave';
      else if (this.tutorialStep === 6) {
        focusSelector = '[data-tower-type="ice"]';
        worldPosition = this.findTutorialPlacementWorld('ice');
        dragPlacement = true;
      } else if (this.tutorialStep === 7) worldPosition = fire?.group.position.clone() ?? null;
      else if (this.tutorialStep === 8) {
        worldPosition = ice?.group.position.clone() ?? null;
        dragPlacement = true;
      }
      else if (this.tutorialStep === 9) worldPosition = ice?.group.position.clone() ?? null;
      else if (this.tutorialStep === 10) focusSelector = '#start-wave';
      else if (this.tutorialStep === 11) {
        focusSelector = '[data-tower-type="fire"]';
        worldPosition = this.findTutorialPlacementWorld('terminalFire');
        dragPlacement = true;
      } else if (this.tutorialStep === 12) worldPosition = ice?.group.position.clone() ?? null;
      else if (this.tutorialStep === 13) {
        worldPosition = terminalFire?.group.position.clone() ?? null;
        dragPlacement = true;
      }
      else if (this.tutorialStep === 14) worldPosition = terminalFire?.group.position.clone() ?? null;
      else if (this.tutorialStep === 15) focusSelector = '#start-wave';
    } else {
      if (this.tutorialStep === 0) {
        focusSelector = '[data-tower-type="foundry"]';
        worldPosition = this.findTutorialPlacementWorld('foundry');
        dragPlacement = true;
      } else if (this.tutorialStep === 1) {
        focusSelector = '[data-tower-type="fire"]';
        worldPosition = this.findTutorialPlacementWorld('fire');
        dragPlacement = true;
      } else if (this.tutorialStep === 2) worldPosition = foundry?.group.position.clone() ?? null;
      else if (this.tutorialStep === 3) focusSelector = '#action-right';
      else if (this.tutorialStep === 4) focusSelector = '#start-wave';
      else if (this.tutorialStep === 5) {
        focusSelector = '[data-tower-type="ice"]';
        worldPosition = this.findTutorialPlacementWorld('ice');
        dragPlacement = true;
      } else if (this.tutorialStep === 6) worldPosition = fire?.group.position.clone() ?? null;
      else if (this.tutorialStep === 7) focusSelector = '#action-left';
      else if (this.tutorialStep === 8) focusSelector = '#start-wave';
      else if (this.tutorialStep === 9) {
        focusSelector = '[data-tower-type="fire"]';
        worldPosition = this.findTutorialPlacementWorld('terminalFire');
        dragPlacement = true;
      } else if (this.tutorialStep === 10) worldPosition = ice?.group.position.clone() ?? null;
      else if (this.tutorialStep === 11) focusSelector = '#action-right';
      else if (this.tutorialStep === 12) worldPosition = terminalFire?.group.position.clone() ?? null;
      else if (this.tutorialStep === 13) focusSelector = '#action-left';
      else if (this.tutorialStep === 14) focusSelector = '#start-wave';
    }

    const cueKey = worldPosition
      ? `${this.tutorialStep}:${worldPosition.x.toFixed(2)}:${worldPosition.y.toFixed(2)}:${worldPosition.z.toFixed(2)}`
      : '';
    const linkDragStart = ROUTING_MODE === 'link'
      ? this.tutorialStep === 3 ? foundry?.group.position.clone() ?? null
        : this.tutorialStep === 8 ? fire?.group.position.clone() ?? null
          : this.tutorialStep === 13 ? ice?.group.position.clone() ?? null
            : null
      : null;
    this.presentTutorialCue(focusSelector, worldPosition, dragPlacement, cueKey, linkDragStart);
  }

  private updateStageTwoTutorialCue(visible: boolean): void {
    if (!visible || !this.isStageTwoLessonWave()) {
      this.hideTutorialHand();
      this.clearTutorialCue();
      return;
    }
    const required = this.stageTwoRequiredTower();
    const feeder = this.towers.find((tower) => tower.group.userData.stageTwoLanceFeeder === true) ?? null;
    const lance = this.towers.find((tower) => tower.type === 'lance') ?? null;
    let focusSelector = required ? `[data-tower-type="${required}"]` : '#start-wave';
    let worldPosition = required ? this.findStageTwoLessonPlacementWorld(required) : null;
    if (!required && this.waveIndex === 3 && feeder && lance && !this.stageTwoLanceFeederIntroduced) {
      if (this.selectedTowerId !== feeder.id) {
        focusSelector = '';
        worldPosition = feeder.group.position.clone();
      } else if (ROUTING_MODE === 'link') {
        focusSelector = '';
        worldPosition = lance.group.position.clone();
      } else {
        const remaining = Math.atan2(
          Math.sin(this.angleToTower(feeder, lance) - feeder.aimAngle),
          Math.cos(this.angleToTower(feeder, lance) - feeder.aimAngle),
        );
        focusSelector = remaining >= 0 ? '#action-right' : '#action-left';
        worldPosition = null;
      }
    }
    const cueKey = worldPosition
      ? `stage2:${this.waveIndex}:${required}:${worldPosition.x.toFixed(2)}:${worldPosition.y.toFixed(2)}:${worldPosition.z.toFixed(2)}`
      : '';
    const linkDragStart = ROUTING_MODE === 'link' && !required && feeder && lance && !this.stageTwoLanceFeederIntroduced
      ? feeder.group.position.clone()
      : null;
    this.presentTutorialCue(focusSelector, worldPosition, required !== null || linkDragStart !== null, cueKey, linkDragStart);
  }

  private presentTutorialCue(
    focusSelector: string,
    worldPosition: THREE.Vector3 | null,
    dragPlacement: boolean,
    cueKey: string,
    dragStartWorld: THREE.Vector3 | null = null,
  ): void {
    if (focusSelector) {
      const focus = document.querySelector<HTMLElement>(focusSelector);
      focus?.classList.add('tutorial-focus');
      focus?.setAttribute('data-tutorial-focus', 'true');
    }
    this.updateTutorialHand(focusSelector, worldPosition, dragPlacement, dragStartWorld);
    if (cueKey === this.tutorialCueKey) return;
    this.clearTutorialCue();
    if (!worldPosition) return;
    this.tutorialCueKey = cueKey;
    const cue = new THREE.Group();
    const ring = new THREE.Mesh(
      new THREE.RingGeometry(0.82, 1.08, 32),
      new THREE.MeshBasicMaterial({ color: 0xffef84, transparent: true, opacity: 0.9, side: THREE.DoubleSide, depthWrite: false }),
    );
    ring.rotation.x = -Math.PI / 2;
    const arrow = new THREE.Mesh(
      new THREE.ConeGeometry(0.25, 0.62, 8),
      new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.94, depthWrite: false }),
    );
    arrow.position.y = 1.65;
    arrow.rotation.z = Math.PI;
    cue.add(ring, arrow);
    cue.position.copy(worldPosition).add(new THREE.Vector3(0, 0.11, 0));
    cue.userData.baseY = cue.position.y;
    this.tutorialCueGroup.add(cue);
  }

  private findStageTwoLessonCell(type: 'foundry' | 'amplifier' | 'lance'): CellState | null {
    const cached = this.stageTwoLessonCells.get(type);
    if (cached) {
      const cell = this.cells.get(gridKey(cached.gx, cached.gz));
      if (cell && this.validateFootprint(type, cell.gx, cell.gz, null).valid) return cell;
      this.stageTwoLessonCells.delete(type);
    }
    if (ROUTING_MODE === 'rotation' && this.stageIndex === 1 && this.waveIndex === 3 && (type === 'lance' || type === 'foundry')) {
      this.stageTwoRotationLessonPair ??= this.findStageTwoRotationLessonPair();
      const authored = type === 'lance' ? this.stageTwoRotationLessonPair?.lance : this.stageTwoRotationLessonPair?.feeder;
      if (authored) {
        const cell = this.cells.get(gridKey(authored.gx, authored.gz));
        if (cell && this.validateFootprint(type, cell.gx, cell.gz, null).valid) {
          this.stageTwoLessonCells.set(type, { gx: cell.gx, gz: cell.gz });
          return cell;
        }
      }
    }
    let best: { cell: CellState; score: number } | null = null;
    const board = this.activeStage().board;
    for (let gx = 0; gx < board.width; gx += 1) {
      for (let gz = 0; gz < board.depth; gz += 1) {
        const placement = this.validateFootprint(type, gx, gz, null);
        const cell = this.cells.get(gridKey(gx, gz));
        if (!placement.valid || placement.layer === null || !cell) continue;
        const clientPoint = this.worldToClient(this.gridToWorld(cell.gx, cell.gz, cell.layer));
        if (!clientPoint) continue;
        const visibleHit = this.findCellAt(clientPoint.x, clientPoint.y);
        const topElement = document.elementFromPoint(clientPoint.x, clientPoint.y);
        if (!visibleHit || visibleHit.cell.gx !== cell.gx || visibleHit.cell.gz !== cell.gz || topElement !== this.canvas) continue;
        const keys = this.footprintKeys(type, gx, gz);
        const positions = keys.map((key) => {
          const footprintCell = this.cells.get(key);
          return footprintCell ? this.gridToWorld(footprintCell.gx, footprintCell.gz, footprintCell.layer) : null;
        }).filter((position): position is THREE.Vector3 => position !== null);
        if (positions.length !== keys.length) continue;
        const center = positions.reduce((sum, position) => sum.add(position), new THREE.Vector3()).multiplyScalar(1 / positions.length);
        let score = -center.length() * 0.18;
        if (type === 'lance') {
          const laneDistance = this.distanceToEnemyPath(center.x, center.z);
          if (placement.layer !== 0 || laneDistance > EXPLOSION_RADIUS) continue;
          score += 220 - laneDistance * 70;
        }
        if (type === 'foundry') {
          const lance = this.towers.find((tower) => tower.type === 'lance');
          if (!lance || lance.layer !== placement.layer) continue;
          const toLance = lance.group.position.clone().sub(center);
          const distance = toLance.length();
          if (distance > TOWER_DEFINITIONS.foundry.connectionRange) continue;
          const start = center.clone().add(new THREE.Vector3(0, 0.68, 0));
          if (this.firstBlockerHit(start, this.towerPort(lance), placement.layer) !== null) continue;
          if (ROUTING_MODE === 'rotation') {
            const firstReceiver = this.findTowerIntersection(-1, placement.layer, start, this.towerPort(lance));
            if (firstReceiver?.tower.id !== lance.id) continue;
          }
          score += 150 - distance * 2 - Math.abs(toLance.z) * 4;
        }
        for (const tower of this.towers) {
          if (tower.layer !== placement.layer) continue;
          const distance = tower.group.position.distanceTo(center);
          if (type === 'amplifier' && distance <= TOWER_DEFINITIONS.amplifier.connectionRange) {
            score += 100 - distance;
          } else if (type === 'lance' && isAmmoEmitter(tower.type) && distance <= this.connectionRange(tower)) {
            score += 90 - distance;
          }
        }
        if (!best || score > best.score) best = { cell, score };
      }
    }
    if (best) this.stageTwoLessonCells.set(type, { gx: best.cell.gx, gz: best.cell.gz });
    return best?.cell ?? null;
  }

  private findStageTwoRotationLessonPair(): { lance: { gx: number; gz: number }; feeder: { gx: number; gz: number } } | null {
    const board = this.activeStage().board;
    let best: { lance: { gx: number; gz: number }; feeder: { gx: number; gz: number }; score: number } | null = null;
    const centerFor = (type: TowerType, gx: number, gz: number) => {
      const positions = this.footprintKeys(type, gx, gz).map((key) => {
        const cell = this.cells.get(key);
        return cell ? this.gridToWorld(cell.gx, cell.gz, cell.layer) : null;
      }).filter((position): position is THREE.Vector3 => position !== null);
      return positions.length === 0 ? null : positions.reduce((sum, position) => sum.add(position), new THREE.Vector3()).multiplyScalar(1 / positions.length);
    };
    for (let lanceGx = 0; lanceGx < board.width; lanceGx += 1) {
      for (let lanceGz = 0; lanceGz < board.depth; lanceGz += 1) {
        const lancePlacement = this.validateFootprint('lance', lanceGx, lanceGz, null);
        const lanceCenter = centerFor('lance', lanceGx, lanceGz);
        if (!lancePlacement.valid || lancePlacement.layer === null || !lanceCenter) continue;
        if (!this.isLessonCellVisible(lanceGx, lanceGz)) continue;
        const lanceKeys = new Set(this.footprintKeys('lance', lanceGx, lanceGz));
        for (let feederGx = 0; feederGx < board.width; feederGx += 1) {
          for (let feederGz = 0; feederGz < board.depth; feederGz += 1) {
            const feederPlacement = this.validateFootprint('foundry', feederGx, feederGz, null);
            if (!feederPlacement.valid || feederPlacement.layer !== lancePlacement.layer) continue;
            if (!this.isLessonCellVisible(feederGx, feederGz)) continue;
            if (this.footprintKeys('foundry', feederGx, feederGz).some((key) => lanceKeys.has(key))) continue;
            const feederCenter = centerFor('foundry', feederGx, feederGz);
            if (!feederCenter) continue;
            const distance = feederCenter.distanceTo(lanceCenter);
            if (distance > TOWER_DEFINITIONS.foundry.connectionRange) continue;
            const start = feederCenter.clone().add(new THREE.Vector3(0, 0.68, 0));
            const end = lanceCenter.clone().add(new THREE.Vector3(0, 0.68, 0));
            if (this.firstBlockerHit(start, end, lancePlacement.layer) !== null) continue;
            if (this.findTowerIntersection(-1, lancePlacement.layer, start, end) !== null) continue;
            const score = -distance - lanceCenter.length() * 0.08 - feederCenter.length() * 0.04;
            if (!best || score > best.score) {
              best = { lance: { gx: lanceGx, gz: lanceGz }, feeder: { gx: feederGx, gz: feederGz }, score };
            }
          }
        }
      }
    }
    return best ? { lance: best.lance, feeder: best.feeder } : null;
  }

  private isLessonCellVisible(gx: number, gz: number): boolean {
    const cell = this.cells.get(gridKey(gx, gz));
    if (!cell) return false;
    const point = this.worldToClient(this.gridToWorld(cell.gx, cell.gz, cell.layer));
    if (!point) return false;
    const hit = this.findCellAt(point.x, point.y);
    return hit?.cell.gx === gx && hit.cell.gz === gz && document.elementFromPoint(point.x, point.y) === this.canvas;
  }

  private findStageTwoLessonPlacementWorld(type: 'foundry' | 'amplifier' | 'lance'): THREE.Vector3 | null {
    const cell = this.findStageTwoLessonCell(type);
    return cell ? this.gridToWorld(cell.gx, cell.gz, cell.layer) : null;
  }

  private findTutorialPlacementWorld(type: keyof typeof TUTORIAL_TOWER_CELLS): THREE.Vector3 | null {
    const target = TUTORIAL_TOWER_CELLS[type];
    const cell = this.cells.get(gridKey(target.gx, target.gz));
    return cell ? this.gridToWorld(cell.gx, cell.gz, cell.layer) : null;
  }

  private findTutorialTower(type: keyof typeof TUTORIAL_TOWER_CELLS): TowerState | undefined {
    const target = TUTORIAL_TOWER_CELLS[type];
    return this.towers.find((tower) => tower.gx === target.gx && tower.gz === target.gz);
  }

  private updateTutorialHand(
    focusSelector: string,
    worldPosition: THREE.Vector3 | null,
    dragPlacement: boolean,
    dragStartWorld: THREE.Vector3 | null = null,
  ): void {
    const worldTarget = worldPosition ? this.worldToClient(worldPosition.clone().add(new THREE.Vector3(0, 0.45, 0))) : null;
    const worldStart = dragStartWorld ? this.worldToClient(dragStartWorld.clone().add(new THREE.Vector3(0, 0.45, 0))) : null;
    const focus = focusSelector ? document.querySelector<HTMLElement>(focusSelector) : null;
    const focusRect = focus?.getBoundingClientRect();
    const uiTarget = focusRect ? {
      x: focusRect.left + focusRect.width * 0.72,
      y: focusRect.top + focusRect.height * 0.42,
    } : null;
    const start = dragPlacement ? worldStart ?? uiTarget : worldTarget ?? uiTarget;
    const end = dragPlacement ? worldTarget : worldTarget ?? uiTarget;
    if (!start || !end) {
      this.hideTutorialHand();
      return;
    }
    const clampPoint = (point: { x: number; y: number }) => ({
      x: THREE.MathUtils.clamp(point.x, 26, window.innerWidth - 26),
      y: THREE.MathUtils.clamp(point.y, 26, window.innerHeight - 58),
    });
    const safeStart = clampPoint(start);
    const safeEnd = clampPoint(end);
    this.tutorialHandElement.style.setProperty('--hand-start-x', `${safeStart.x}px`);
    this.tutorialHandElement.style.setProperty('--hand-start-y', `${safeStart.y}px`);
    this.tutorialHandElement.style.setProperty('--hand-end-x', `${safeEnd.x}px`);
    this.tutorialHandElement.style.setProperty('--hand-end-y', `${safeEnd.y}px`);
    this.tutorialHandElement.classList.remove('hidden', 'tap', 'drag');
    this.tutorialHandElement.classList.add(dragPlacement ? 'drag' : 'tap');
    this.tutorialHandElement.dataset.mode = dragPlacement ? 'drag' : 'tap';
  }

  private worldToClient(position: THREE.Vector3): { x: number; y: number } | null {
    this.camera.updateMatrixWorld();
    const projected = position.project(this.camera);
    if (projected.z < -1 || projected.z > 1) return null;
    const rect = this.canvas.getBoundingClientRect();
    return {
      x: rect.left + (projected.x * 0.5 + 0.5) * rect.width,
      y: rect.top + (-projected.y * 0.5 + 0.5) * rect.height,
    };
  }

  private hideTutorialHand(): void {
    this.tutorialHandElement.classList.add('hidden');
    this.tutorialHandElement.classList.remove('tap', 'drag');
    delete this.tutorialHandElement.dataset.mode;
  }

  private clearTutorialCue(): void {
    this.tutorialCueGroup.traverse((child) => {
      if (!(child instanceof THREE.Mesh || child instanceof THREE.Line)) return;
      child.geometry.dispose();
      const material = child.material;
      if (Array.isArray(material)) material.forEach((entry) => entry.dispose());
      else material.dispose();
    });
    this.tutorialCueGroup.clear();
    this.tutorialCueKey = '';
  }

  private showToast(text: string, tone: ToastState['tone']): void {
    this.lastToast = { text, tone };
    this.toastElement.textContent = text;
    this.toastElement.dataset.tone = tone;
    this.toastElement.classList.remove('hidden');
  }

  private clearToast(): void {
    this.lastToast = { text: '', tone: 'info' };
    this.toastElement.textContent = '';
    this.toastElement.classList.add('hidden');
  }

  private elementCueGlyph(element: Element): string {
    if (element === 'fire') return '◆';
    if (element === 'ice') return '✦';
    if (element === 'wind') return '➤';
    return '⬟';
  }

  private queueDiscoveryCue(
    kind: DiscoveryCueKind,
    html: string,
    options: { targetSelector?: string; worldPosition?: THREE.Vector3; highlightOnly?: boolean; duration?: number } = {},
    force = false,
  ): void {
    if (!force && this.discoveredCues.has(kind)) return;
    this.discoveredCues.add(kind);
    this.discoveryCueTriggerCounts[kind] += 1;
    const request: DiscoveryCueRequest = {
      kind,
      html,
      targetSelector: options.targetSelector,
      worldPosition: options.worldPosition?.clone(),
      highlightOnly: options.highlightOnly ?? false,
      duration: options.duration ?? 2.6,
    };
    if (this.discoveryCue) this.discoveryCueQueue.push(request);
    else this.showDiscoveryCue(request);
  }

  private showDiscoveryCue(request: DiscoveryCueRequest): void {
    this.discoveryCue = request;
    this.discoveryCueElapsed = 0;
    this.discoveryCardElement.innerHTML = request.html;
    this.discoveryCueElement.dataset.kind = request.kind;
    this.discoveryCueElement.classList.toggle('hidden', request.highlightOnly);
    if (request.targetSelector) {
      document.querySelector<HTMLElement>(request.targetSelector)?.classList.add('discovery-target');
    }
    if (!request.highlightOnly) this.positionDiscoveryCue(request);
  }

  private positionDiscoveryCue(request: DiscoveryCueRequest): void {
    let point: { x: number; y: number } | null = null;
    if (request.worldPosition) point = this.worldToClient(request.worldPosition);
    else if (request.targetSelector) {
      const target = document.querySelector<HTMLElement>(request.targetSelector);
      if (target) {
        const rect = target.getBoundingClientRect();
        point = { x: rect.left + rect.width * 0.5, y: rect.bottom + 18 };
      }
    }
    if (!point) point = { x: window.innerWidth * 0.5, y: window.innerHeight * 0.5 };
    const x = THREE.MathUtils.clamp(point.x, 62, Math.max(62, window.innerWidth - 62));
    const y = THREE.MathUtils.clamp(point.y, 92, Math.max(92, window.innerHeight - 70));
    this.discoveryCueElement.style.setProperty('--cue-x', `${x}px`);
    this.discoveryCueElement.style.setProperty('--cue-y', `${y}px`);
  }

  private updateDiscoveryCue(delta: number): void {
    if (!this.discoveryCue) return;
    this.discoveryCueElapsed += delta;
    if (!this.discoveryCue.highlightOnly) this.positionDiscoveryCue(this.discoveryCue);
    if (this.discoveryCueElapsed < this.discoveryCue.duration) return;
    this.hideDiscoveryCue();
    const next = this.discoveryCueQueue.shift();
    if (next) this.showDiscoveryCue(next);
  }

  private hideDiscoveryCue(): void {
    if (this.discoveryCue?.targetSelector) {
      document.querySelector<HTMLElement>(this.discoveryCue.targetSelector)?.classList.remove('discovery-target');
    }
    this.discoveryCue = null;
    this.discoveryCueElapsed = 0;
    this.discoveryCueElement.classList.add('hidden');
    this.discoveryCardElement.innerHTML = '';
  }

  private pathPosition(progress: number, sideOffset: number, layer: 0 | 1 | 2): THREE.Vector3 {
    let remaining = THREE.MathUtils.clamp(progress, 0, this.pathTotalLength);
    for (let index = 0; index < this.pathSegmentLengths.length; index += 1) {
      const length = this.pathSegmentLengths[index];
      if (remaining <= length || index === this.pathSegmentLengths.length - 1) {
        const t = length <= 0 ? 0 : remaining / length;
        const start = this.pathXZ[index];
        const end = this.pathXZ[index + 1];
        const direction = end.clone().sub(start).normalize();
        const perpendicular = new THREE.Vector2(-direction.y, direction.x);
        const xz = start.clone().lerp(end, t).addScaledVector(perpendicular, sideOffset);
        const bob = layer === 0 ? 0 : Math.sin(this.elapsed * 2.8 + progress * 0.4) * 0.12;
        return new THREE.Vector3(xz.x, LAYER_HEIGHTS[layer] + bob, xz.y);
      }
      remaining -= length;
    }
    const last = this.pathXZ[this.pathXZ.length - 1];
    return new THREE.Vector3(last.x, LAYER_HEIGHTS[layer], last.y);
  }

  private towerPort(tower: TowerState): THREE.Vector3 {
    return tower.group.position.clone().add(new THREE.Vector3(0, 0.68, 0));
  }

  private projectileReceiverRadius(tower: TowerState): number {
    const footprint = TOWER_DEFINITIONS[tower.type].footprint;
    return 0.72 + (Math.max(footprint[0], footprint[1]) - 1) * 0.38;
  }

  private findTowerIntersection(
    sourceTowerId: number,
    layer: 0 | 1 | 2,
    start: THREE.Vector3,
    end: THREE.Vector3,
  ): { tower: TowerState; entry: number } | null {
    let earliest: { tower: TowerState; entry: number } | null = null;
    for (const tower of this.towers) {
      if (tower.id === sourceTowerId || tower.layer !== layer || !isAmmoReceiver(tower.type)) continue;
      const entry = segmentSphereEntry(start, end, this.towerPort(tower), this.projectileReceiverRadius(tower));
      if (entry === null || (earliest && entry >= earliest.entry)) continue;
      earliest = { tower, entry };
    }
    return earliest;
  }

  private findAimedReceiver(source: TowerState): { tower: TowerState; entry: number } | null {
    if (!isAmmoEmitter(source.type)) return null;
    const start = this.towerPort(source);
    const direction = new THREE.Vector3(Math.cos(source.aimAngle), 0, Math.sin(source.aimAngle));
    const end = start.clone().addScaledVector(direction, this.connectionRange(source));
    const blockerHit = this.firstBlockerHit(start, end, source.layer);
    if (blockerHit !== null) end.lerpVectors(start, end, Math.max(0, blockerHit - 0.015));
    return this.findTowerIntersection(source.id, source.layer, start, end);
  }

  private applyTowerAimVisual(tower: TowerState): void {
    tower.group.rotation.y = -tower.aimAngle;
  }

  private createLanceAmmoBar(): THREE.Group {
    const bar = new THREE.Group();
    bar.name = 'lanceAmmoBar';
    bar.position.y = 2.42;
    const createMaterial = (color: number, opacity: number) => new THREE.MeshBasicMaterial({
      color,
      transparent: true,
      opacity,
      depthTest: false,
      depthWrite: false,
      toneMapped: false,
    });
    const backplate = new THREE.Mesh(new THREE.PlaneGeometry(1.92, 0.34), createMaterial(0x071421, 0.88));
    backplate.name = 'lanceAmmoBackplate';
    const track = new THREE.Mesh(new THREE.PlaneGeometry(1.76, 0.18), createMaterial(0x294257, 0.94));
    track.name = 'lanceAmmoTrack';
    track.position.z = 0.01;
    const fill = new THREE.Mesh(new THREE.PlaneGeometry(LANCE_AMMO_BAR_WIDTH, 0.12), createMaterial(0xffdf75, 1));
    fill.name = 'lanceAmmoFill';
    fill.position.set(-LANCE_AMMO_BAR_WIDTH / 2, 0, 0.02);
    fill.scale.x = 0.001;
    fill.visible = false;
    for (const mesh of [backplate, track, fill]) mesh.renderOrder = 40;
    bar.add(backplate, track, fill);
    bar.userData.ratio = 0;
    return bar;
  }

  private updateLanceAmmoBars(): void {
    const parentWorldQuaternion = new THREE.Quaternion();
    for (const tower of this.towers) {
      if (tower.type !== 'lance') continue;
      const bar = tower.group.getObjectByName('lanceAmmoBar');
      const fill = bar?.getObjectByName('lanceAmmoFill');
      if (!(bar instanceof THREE.Group) || !(fill instanceof THREE.Mesh)) continue;
      tower.group.getWorldQuaternion(parentWorldQuaternion);
      bar.quaternion.copy(parentWorldQuaternion.invert().multiply(this.camera.quaternion));
      const ratio = THREE.MathUtils.clamp(tower.buffer.length / Math.max(1, this.capacity(tower)), 0, 1);
      bar.userData.ratio = ratio;
      fill.visible = ratio > 0;
      fill.scale.x = Math.max(0.001, ratio);
      fill.position.x = -LANCE_AMMO_BAR_WIDTH * (1 - ratio) * 0.5;
      const material = fill.material;
      if (material instanceof THREE.MeshBasicMaterial) material.color.setHex(this.towerSignalColor(tower));
    }
  }

  private disposeLanceAmmoBar(tower: TowerState): void {
    const bar = tower.group.getObjectByName('lanceAmmoBar');
    if (!bar) return;
    bar.traverse((child) => {
      if (!(child instanceof THREE.Mesh)) return;
      child.geometry.dispose();
      const materials = Array.isArray(child.material) ? child.material : [child.material];
      for (const material of materials) material.dispose();
    });
    tower.group.remove(bar);
  }

  private firstBlockerHit(start: THREE.Vector3, end: THREE.Vector3, layer: 0 | 1 | 2): number | null {
    let earliest: number | null = null;
    for (const blocker of this.blockers) {
      if (blocker.layer !== layer) continue;
      const hit = segmentAabbEntry(start, end, blocker);
      if (hit !== null && (earliest === null || hit < earliest)) earliest = hit;
    }
    return earliest;
  }

  private connectionRange(tower: TowerState): number {
    const definition = TOWER_DEFINITIONS[tower.type];
    return definition.connectionRange * (1 + (tower.level - 1) * 0.15);
  }

  private capacity(tower: TowerState): number {
    if (tower.type !== 'lance') return Number.POSITIVE_INFINITY;
    const definition = TOWER_DEFINITIONS[tower.type];
    let value = definition.capacity + (tower.level - 1);
    for (const amplifier of this.affectingAmplifiers(tower, 'throughput')) value += 1 + Math.floor(amplifier.level / 2);
    return value;
  }

  private throughputMultiplier(tower: TowerState): number {
    let value = 1;
    for (const amplifier of this.affectingAmplifiers(tower, 'throughput')) value += 0.18 + amplifier.level * 0.05;
    return value;
  }

  private powerMultiplier(tower: TowerState): number {
    let value = 1;
    for (const amplifier of this.affectingAmplifiers(tower, 'power')) value += 0.2 + amplifier.level * 0.06;
    return value;
  }

  private affectingAmplifiers(tower: TowerState, branch: 'power' | 'throughput'): TowerState[] {
    return this.towers.filter((candidate) =>
      candidate.type === 'amplifier' &&
      candidate.amplifierBranch === branch &&
      candidate.layer === tower.layer &&
      candidate.group.position.distanceTo(tower.group.position) <= this.amplifierRange(candidate),
    );
  }

  private amplifierRange(tower: TowerState): number {
    return TOWER_DEFINITIONS.amplifier.connectionRange * (1 + (tower.level - 1) * 0.18);
  }

  private lanceThreshold(tower: TowerState): number {
    return Math.max(5, TOWER_DEFINITIONS.lance.capacity - (tower.level - 1));
  }

  private towerSignalColor(tower: TowerState): number {
    const head = tower.buffer[0];
    if (head?.elements.length) return this.mixedColor(head.elements);
    return TOWER_DEFINITIONS[tower.type].color;
  }

  private mixedColor(elements: readonly Element[]): number {
    if (elements.length === 0) return 0xffe7a5;
    const color = new THREE.Color(0x000000);
    for (const element of elements) color.add(new THREE.Color(ELEMENT_COLORS[element]));
    color.multiplyScalar(1 / elements.length);
    return color.getHex();
  }

  private removeProjectile(index: number): void {
    const projectile = this.projectiles[index];
    if (!projectile) return;
    this.scene.remove(projectile.mesh, projectile.trail);
    const core = projectile.mesh.getObjectByName('projectileCore');
    if (core instanceof THREE.Mesh) {
      core.geometry.dispose();
      const material = core.material;
      if (material instanceof THREE.Material) material.dispose();
    }
    projectile.trail.geometry.dispose();
    const trailMaterial = projectile.trail.material;
    if (trailMaterial instanceof THREE.Material) trailMaterial.dispose();
    this.projectiles.splice(index, 1);
  }

  private findTower(id: number): TowerState | undefined {
    return this.towers.find((tower) => tower.id === id);
  }

  private selectedTower(): TowerState | undefined {
    return this.selectedTowerId === null ? undefined : this.findTower(this.selectedTowerId);
  }

  private findTowerId(object: THREE.Object3D): number | null {
    let current: THREE.Object3D | null = object;
    while (current) {
      if (typeof current.userData.towerId === 'number') return current.userData.towerId as number;
      current = current.parent;
    }
    return null;
  }

  private render(): void {
    this.renderer.render(this.scene, this.camera);
  }

  private installTestHooks(): void {
    window.__THREE_GAME_TEST_HOOKS__ = {
      seed: (value: number) => { this.rng = createSeededRandom(value); },
      setState: (name: string) => {
        const preservesTutorialReaction = name === 'tutorial-link' || name === 'tutorial-ready' || name === 'tutorial-wave';
        if (!name.startsWith('intro-')) {
          this.hideDiscoveryCue();
          this.discoveryCueQueue.length = 0;
          this.discoveredCues.add('currency');
          if (preservesTutorialReaction) this.discoveredCues.delete('reaction');
          else this.discoveredCues.add('reaction');
          this.discoveredCues.add('nexus');
        }
        if (name === 'active-play') this.createDeterministicDemo(0);
        else if (name === 'stress') this.createDeterministicDemo(5);
        else if (name === 'stage-two-ready') this.switchStage(1);
        else if (name === 'stage-three-ready') this.switchStage(2);
        else if (name === 'stage-three-final') {
          this.switchStage(2);
          this.waveIndex = 9;
          this.updateUi(true);
        } else if (name === 'stage-three-final-active') {
          this.switchStage(2);
          this.money = 9999;
          this.tryPlaceTower('foundry', 10, 2);
          this.waveIndex = 9;
          this.money = this.activeStage().startingMoney;
          this.startWave();
        }
        else if (name === 'stage-two-wave-three') this.createStageTwoLessonDemo(2);
        else if (name === 'stage-two-wave-four') this.createStageTwoLessonDemo(3);
        else if (name === 'tutorial-link') this.createTutorialDemo(false, false);
        else if (name === 'tutorial-rotation') this.createTutorialDemo(false, false);
        else if (name === 'tutorial-ready') this.createTutorialDemo(false, true);
        else if (name === 'tutorial-wave') this.createTutorialDemo(true, true);
        else if (name === 'mastery-ready') this.createMasteryCheckpointDemo(0);
        else if (name === 'mastery-two-leaks') this.createMasteryCheckpointDemo(2);
        else if (name === 'mastery-fail') this.createMasteryCheckpointDemo(3);
        else if (name === 'mastery-baseline-final') this.createMasteryFinalDemo(false);
        else if (name === 'mastery-expanded-final') this.createMasteryFinalDemo(true);
        else if (name === 'stage-two-baseline-final') this.createStageTwoBalanceDemo(false);
        else if (name === 'stage-two-expanded-final') this.createStageTwoBalanceDemo(true);
        else if (name === 'stage-three-baseline-late') this.createStageThreeBalanceDemo(false);
        else if (name === 'stage-three-expanded-late') this.createStageThreeBalanceDemo(true);
        else if (name === 'terminal-flow') this.createTerminalFlowDemo();
        else if (name === 'status-fire') this.createElementStatusDemo(false);
        else if (name === 'status-reaction') this.createElementStatusDemo(true);
        else if (name === 'reaction-scaling') this.createReactionScalingDemo();
        else if (name === 'armored-intact') this.createArmoredBreakDemo(false);
        else if (name === 'armored-break') this.createArmoredBreakDemo(true);
        else if (name === 'reward-stage-one') this.createRewardDemo(0);
        else if (name === 'reward-stage-two') this.createRewardDemo(1);
        else if (name === 'intro-currency') this.createDiscoveryDemo('currency');
        else if (name === 'intro-reaction') this.createDiscoveryDemo('reaction');
        else if (name === 'intro-nexus') this.createDiscoveryDemo('nexus');
        else if (name === 'explosion-vfx') {
          if (this.stageIndex !== 0) this.switchStage(0);
          else this.resetRun();
          const center = new THREE.Vector3(0, LAYER_HEIGHTS[0] + 0.18, 0);
          this.createExplosionVfx(center, ['fire', 'ice']);
          this.publishDiagnostics();
        }
        else if (name === 'explosion-skill') this.createExplosionSkillDemo();
        else if (name === 'relay-lock') this.createRelayLockDemo();
        else if (name === 'fail') {
          this.resetRun();
          this.lives = 0;
          this.endRun(false);
        } else if (name === 'win') {
          this.resetRun();
          this.endRun(true);
        } else console.warn(`Unknown test state: ${name}`);
      },
      setPausedForScreenshot: (paused: boolean) => { this.pausedForScreenshot = paused; },
      setReducedMotion: (enabled: boolean) => { this.reducedMotion = enabled; },
      setSpeed: (index: number) => {
        this.speedIndex = THREE.MathUtils.clamp(Math.round(index), 0, GAME_SPEEDS.length - 1);
        this.updateUi(true);
      },
      advance: (seconds: number) => {
        const steps = Math.min(18_000, Math.max(0, Math.ceil(seconds / FIXED_STEP)));
        for (let index = 0; index < steps && this.phase === 'wave'; index += 1) {
          this.simulate(FIXED_STEP);
          this.updateImpactParticles(FIXED_STEP);
          this.updateEffects(FIXED_STEP);
        }
        this.updateLanceAmmoBars();
        this.updateEnemyStatusPresentation();
        this.updateUi(true);
        this.publishDiagnostics();
      },
      hideDebugUi: () => undefined,
      getCellClientPoint: (gx: number, gz: number) => this.getCellClientPoint(gx, gz),
      getTowerClientPoint: (id: number) => this.getTowerClientPoint(id),
    };
  }

  private getTowerClientPoint(id: number): { x: number; y: number } | null {
    const tower = this.findTower(id);
    if (!tower) return null;
    return this.worldToClient(tower.group.position.clone().add(new THREE.Vector3(0, 0.7, 0)));
  }

  private getCellClientPoint(gx: number, gz: number): { x: number; y: number } | null {
    const cell = this.cells.get(gridKey(gx, gz));
    if (!cell) return null;
    this.camera.updateMatrixWorld();
    const projected = this.gridToWorld(cell.gx, cell.gz, cell.layer).project(this.camera);
    const rect = this.canvas.getBoundingClientRect();
    return {
      x: rect.left + (projected.x * 0.5 + 0.5) * rect.width,
      y: rect.top + (-projected.y * 0.5 + 0.5) * rect.height,
    };
  }

  private createElementStatusDemo(triggerReaction: boolean): void {
    if (this.stageIndex !== 0) this.switchStage(0);
    else this.resetRun();
    this.spawnEnemy('riftling', 0);
    const enemy = this.enemies[0];
    if (!enemy) return;
    enemy.progress = this.pathTotalLength * 0.46;
    enemy.group.position.copy(this.pathPosition(enemy.progress, 0, 0));
    const position = enemy.group.position.clone();
    this.applyProjectileHit({ id: this.nextRoundId++, damage: 4, elements: ['fire'] }, enemy, position);
    if (triggerReaction && !enemy.dead) {
      this.applyProjectileHit({ id: this.nextRoundId++, damage: 4, elements: ['ice'] }, enemy, position);
    }
    enemy.hitFlash = 0;
    this.updateEnemyStatusPresentation();
    this.updateUi(true);
  }

  private createReactionScalingDemo(): void {
    if (this.stageIndex !== 2) this.switchStage(2);
    else this.resetRun();
    this.waveIndex = 9;
    this.spawnEnemy('colossus', 0);
    const enemy = this.enemies[0];
    if (!enemy) return;
    enemy.progress = this.pathTotalLength * 0.46;
    enemy.group.position.copy(this.pathPosition(enemy.progress, 0, 0));
    const position = enemy.group.position.clone();
    this.applyProjectileHit({ id: this.nextRoundId++, damage: 4, elements: ['ice'] }, enemy, position);
    if (!enemy.dead) this.applyProjectileHit({ id: this.nextRoundId++, damage: 4, elements: ['earth'] }, enemy, position);
    enemy.hitFlash = 0;
    this.updateEnemyStatusPresentation();
    this.updateUi(true);
    this.publishDiagnostics();
  }

  private createArmoredBreakDemo(breakArmor: boolean): void {
    if (this.stageIndex !== 2) this.switchStage(2);
    else this.resetRun();
    this.waveIndex = 2;
    this.spawnEnemy('arcaneBulwark', 0);
    const enemy = this.enemies[0];
    if (!enemy) return;
    enemy.progress = this.pathTotalLength * 0.46;
    enemy.group.position.copy(this.pathPosition(enemy.progress, 0, 0));
    const position = enemy.group.position.clone();
    this.applyProjectileHit({ id: this.nextRoundId++, damage: 40, elements: ['fire'] }, enemy, position);
    if (breakArmor && !enemy.dead) {
      this.applyProjectileHit({ id: this.nextRoundId++, damage: 40, elements: ['ice'] }, enemy, position);
      this.updateEnemies(0);
    }
    enemy.hitFlash = 0;
    this.updateEnemyStatusPresentation();
    this.updateUi(true);
    this.publishDiagnostics();
  }

  private createRewardDemo(stageIndex: 0 | 1): void {
    if (this.stageIndex !== stageIndex) this.switchStage(stageIndex);
    else this.resetRun();
    this.money = 0;
    this.spawnEnemy('riftling', 0);
    this.killEnemy(0, '');
    this.updateUi(true);
    this.publishDiagnostics();
  }

  private createDiscoveryDemo(kind: DiscoveryCueKind): void {
    if (this.stageIndex !== 0) this.switchStage(0);
    else this.resetRun();
    this.hideDiscoveryCue();
    this.discoveryCueQueue.length = 0;
    this.discoveredCues.clear();
    for (const other of ['currency', 'reaction', 'nexus'] as const) {
      if (other !== kind) this.discoveredCues.add(other);
    }
    if (kind === 'currency') {
      this.money = 9999;
      this.tryPlaceTower('foundry', TUTORIAL_TOWER_CELLS.foundry.gx, TUTORIAL_TOWER_CELLS.foundry.gz);
    } else if (kind === 'reaction') {
      this.createElementStatusDemo(true);
    } else {
      this.spawnEnemy('riftling', 0);
      const enemy = this.enemies[0];
      if (enemy) {
        enemy.progress = this.pathTotalLength;
        this.updateEnemies(0.01);
      }
    }
    this.updateUi(true);
    this.publishDiagnostics();
  }

  private createTutorialDemo(startWave: boolean, completeLinks: boolean): void {
    if (this.stageIndex !== 0) this.switchStage(0);
    else this.resetRun();
    this.money = 9999;
    this.tryPlaceTower('foundry', TUTORIAL_TOWER_CELLS.foundry.gx, TUTORIAL_TOWER_CELLS.foundry.gz);
    this.tryPlaceTower('fire', TUTORIAL_TOWER_CELLS.fire.gx, TUTORIAL_TOWER_CELLS.fire.gz);
    const foundry = this.findTutorialTower('foundry');
    const fire = this.findTutorialTower('fire');
    if (!completeLinks) {
      this.selectedTowerId = foundry?.id ?? null;
      this.interactionMode = 'inspect';
      this.tutorialStep = 2;
      this.refreshTutorialProgress();
      this.updateUi(true);
      return;
    }
    if (foundry && fire) this.routeTowers(foundry, fire);
    this.tutorialStep = 6;
    this.tryPlaceTower('ice', TUTORIAL_TOWER_CELLS.ice.gx, TUTORIAL_TOWER_CELLS.ice.gz);
    const ice = this.findTutorialTower('ice');
    if (fire && ice) this.routeTowers(fire, ice);
    this.tutorialStep = 11;
    this.tryPlaceTower('fire', TUTORIAL_TOWER_CELLS.terminalFire.gx, TUTORIAL_TOWER_CELLS.terminalFire.gz);
    const terminalFire = this.findTutorialTower('terminalFire');
    if (ice && terminalFire) this.routeTowers(ice, terminalFire);
    if (ROUTING_MODE === 'rotation' && terminalFire) {
      terminalFire.aimAngle = 0;
      this.applyTowerAimVisual(terminalFire);
    }
    if (startWave && foundry) {
      foundry.buffer.push(this.createNeutralRound(foundry));
    }
    this.tutorialStep = ROUTING_MODE === 'link' ? 15 : 14;
    this.selectedTowerId = window.matchMedia('(max-width: 700px) and (orientation: portrait)').matches ? null : ice?.id ?? null;
    this.waveIndex = 2;
    this.money = 200;
    this.refreshNetworkVisuals();
    this.refreshSelectionVisual();
    this.updateUi(true);
    if (startWave) {
      this.speedIndex = 1;
      this.startWave();
    }
  }

  private createMasteryCheckpointDemo(leaks: number): void {
    this.createTutorialDemo(false, true);
    this.tutorialStep = TUTORIAL_STEP_COUNT;
    this.waveIndex = 3;
    this.money = 340;
    this.lives = this.activeStage().startingLives;
    this.discoveredCues.add('reaction');
    this.captureMasteryCheckpoint();
    this.selectedTowerId = null;
    this.interactionMode = 'inspect';
    this.refreshNetworkVisuals();
    this.refreshSelectionVisual();
    this.updateUi(true);
    this.lives = Math.max(0, this.lives - Math.max(0, Math.floor(leaks)));
    if (this.lives === 0) this.endRun(false);
    else this.updateUi(true);
  }

  private createMasteryFinalDemo(expanded: boolean): void {
    this.createMasteryCheckpointDemo(0);
    if (expanded) {
      const branchCells = {
        foundry: { gx: 3, gz: 0 },
        fire: { gx: 5, gz: 5 },
        ice: { gx: 1, gz: 1 },
      } as const;
      this.tryPlaceTower('foundry', branchCells.foundry.gx, branchCells.foundry.gz);
      this.tryPlaceTower('fire', branchCells.fire.gx, branchCells.fire.gz);
      this.tryPlaceTower('ice', branchCells.ice.gx, branchCells.ice.gz);
      const branchFoundry = this.towers.find((tower) => tower.gx === branchCells.foundry.gx && tower.gz === branchCells.foundry.gz);
      const branchFire = this.towers.find((tower) => tower.gx === branchCells.fire.gx && tower.gz === branchCells.fire.gz);
      const branchIce = this.towers.find((tower) => tower.gx === branchCells.ice.gx && tower.gz === branchCells.ice.gz);
      const terminalFire = this.findTutorialTower('terminalFire');
      if (branchFoundry && branchIce) this.routeTowers(branchFoundry, branchIce);
      if (branchIce && branchFire) this.routeTowers(branchIce, branchFire);
      if (branchFire && terminalFire) this.routeTowers(branchFire, terminalFire);
    }
    this.waveIndex = 5;
    this.speedIndex = 1;
    this.selectedTowerId = null;
    this.interactionMode = 'inspect';
    this.refreshNetworkVisuals();
    this.refreshSelectionVisual();
    this.updateUi(true);
    this.startWave();
  }

  private createStageTwoBalanceDemo(expanded: boolean): void {
    if (expanded) {
      this.createDeterministicDemo(5);
      this.money = 9999;
      const branchStart = this.towers.length;
      const branchLayout: readonly [TowerType, number, number][] = [
        ['foundry', 10, 2], ['fire', 9, 2], ['ice', 5, 2], ['wind', 1, 2],
      ];
      for (const [type, gx, gz] of branchLayout) this.tryPlaceTower(type, gx, gz);
      const [branchFoundry, branchFire, branchIce, branchWind] = this.towers.slice(branchStart);
      if (branchFoundry && branchFire) this.routeTowers(branchFoundry, branchFire);
      if (branchFire && branchIce) this.routeTowers(branchFire, branchIce);
      if (branchIce && branchWind) this.routeTowers(branchIce, branchWind);
      for (const tower of this.towers) {
        if (tower.type === 'amplifier') continue;
        tower.level = 3;
        tower.totalInvested += TOWER_DEFINITIONS[tower.type].upgradeCost * 2;
      }
      this.money = 120;
      this.refreshNetworkVisuals();
      this.updateUi(true);
      return;
    }
    if (this.stageIndex !== 1) this.switchStage(1);
    else this.resetRun();
    this.money = 9999;
    this.tryPlaceTower('foundry', 0, 2);
    this.tryPlaceTower('fire', 2, 2);
    this.tryPlaceTower('ice', 2, 0);
    const [foundry, fire, ice] = this.towers;
    if (foundry && fire) this.routeTowers(foundry, fire);
    if (fire && ice) this.routeTowers(fire, ice);
    this.waveIndex = 5;
    this.money = 0;
    this.speedIndex = 1;
    this.selectedTowerId = null;
    this.interactionMode = 'inspect';
    this.refreshNetworkVisuals();
    this.refreshSelectionVisual();
    this.updateUi(true);
    this.startWave();
  }

  private createStageThreeBalanceDemo(expanded: boolean): void {
    if (this.stageIndex !== 2) this.switchStage(2);
    else this.resetRun();
    this.money = 9999;
    const groundPrimary: readonly [TowerType, number, number][] = [
      ['foundry', 11, 8], ['ice', 11, 7], ['fire', 11, 5], ['wind', 11, 2],
    ];
    const groundEarly: readonly [TowerType, number, number][] = [
      ['foundry', 6, 3], ['ice', 5, 3], ['fire', 3, 3], ['wind', 0, 3],
    ];
    const airLeft: readonly [TowerType, number, number][] = [
      ['foundry', 3, 7], ['ice', 4, 7], ['fire', 6, 7], ['wind', 8, 7],
    ];
    const airRight: readonly [TowerType, number, number][] = [
      ['foundry', 17, 7], ['ice', 16, 7], ['fire', 15, 7], ['wind', 12, 7],
    ];
    const groundLate: readonly [TowerType, number, number][] = [
      ['foundry', 5, 13], ['ice', 5, 12], ['earth', 5, 11], ['wind', 5, 9],
    ];
    const airRightLate: readonly [TowerType, number, number][] = [
      ['foundry', 17, 6], ['wind', 16, 6], ['earth', 15, 6], ['ice', 12, 6],
    ];
    const branches = expanded
      ? [groundPrimary, groundEarly, groundLate, airLeft, airRight, airRightLate]
      : [groundPrimary];
    for (const branch of branches) {
      const start = this.towers.length;
      for (const [type, gx, gz] of branch) this.tryPlaceTower(type, gx, gz);
      const branchTowers = this.towers.slice(start, start + branch.length);
      for (let index = 0; index < branchTowers.length - 1; index += 1) {
        this.routeTowers(branchTowers[index], branchTowers[index + 1]);
      }
    }
    for (const tower of this.towers) {
      tower.level = 3;
      tower.totalInvested += TOWER_DEFINITIONS[tower.type].upgradeCost * 2;
    }
    this.waveIndex = 5;
    this.money = expanded ? 160 : 0;
    this.speedIndex = 1;
    this.selectedTowerId = null;
    this.interactionMode = 'inspect';
    this.refreshNetworkVisuals();
    this.refreshSelectionVisual();
    this.updateUi(true);
    this.startWave();
  }

  private createTerminalFlowDemo(): void {
    if (this.stageIndex !== 0) this.switchStage(0);
    else this.resetRun();
    this.money = 9999;
    this.tryPlaceTower('foundry', TUTORIAL_TOWER_CELLS.foundry.gx, TUTORIAL_TOWER_CELLS.foundry.gz);
    this.tryPlaceTower('fire', TUTORIAL_TOWER_CELLS.fire.gx, TUTORIAL_TOWER_CELLS.fire.gz);
    const foundry = this.findTutorialTower('foundry');
    const fire = this.findTutorialTower('fire');
    if (foundry && fire) this.routeTowers(foundry, fire);
    this.tutorialStep = ROUTING_MODE === 'link' ? 15 : 14;
    this.waveIndex = 2;
    this.money = 200;
    this.speedIndex = 1;
    this.refreshNetworkVisuals();
    this.updateUi(true);
    this.startWave();
  }

  private createDeterministicDemo(waveIndex: number): void {
    if (this.stageIndex !== 1) this.switchStage(1);
    else this.resetRun();
    this.money = 9999;
    const layout: readonly [TowerType, number, number][] = [
      ['foundry', 0, 2], ['fire', 2, 2], ['ice', 2, 0], ['lance', 0, 0],
      ['amplifier', 6, 1], ['foundry', 1, 4], ['wind', 5, 4],
      ['foundry', 6, 3], ['earth', 2, 5], ['ice', 6, 5],
    ];
    for (const [type, gx, gz] of layout) this.tryPlaceTower(type, gx, gz);
    const [groundFoundry, fire, groundIce, lance, amplifier, airFoundry, wind, highFoundry, earth, airIce] = this.towers;
    if (groundFoundry && fire) this.routeTowers(groundFoundry, fire);
    if (fire && groundIce) this.routeTowers(fire, groundIce);
    if (groundIce && lance) this.routeTowers(groundIce, lance);
    if (ROUTING_MODE === 'rotation' && lance) {
      const laneTarget = this.pathXZ[1] ?? this.pathXZ[0];
      lance.aimAngle = Math.atan2(laneTarget.y - lance.group.position.z, laneTarget.x - lance.group.position.x);
      this.applyTowerAimVisual(lance);
    }
    if (airFoundry && wind) this.routeTowers(airFoundry, wind);
    if (highFoundry && earth) this.routeTowers(highFoundry, earth);
    if (wind && earth) this.routeTowers(wind, earth);
    if (earth && airIce) this.routeTowers(earth, airIce);
    if (airIce && wind) this.routeTowers(airIce, wind);
    if (amplifier) amplifier.amplifierBranch = 'throughput';
    this.waveIndex = waveIndex;
    this.money = 640;
    this.refreshNetworkVisuals();
    const portraitMobile = window.matchMedia('(max-width: 700px) and (orientation: portrait)').matches;
    this.selectedTowerId = portraitMobile ? null : fire?.id ?? null;
    this.refreshSelectionVisual();
    this.startWave();
  }

  private createStageTwoLessonDemo(waveIndex: 2 | 3): void {
    if (this.stageIndex !== 1) this.switchStage(1);
    else this.resetRun();
    this.money = 9999;
    this.tryPlaceTower('foundry', 0, 2);
    this.tryPlaceTower('fire', 2, 2);
    this.tryPlaceTower('ice', 2, 0);
    const foundry = this.towers.find((tower) => tower.type === 'foundry');
    const fire = this.towers.find((tower) => tower.type === 'fire');
    const ice = this.towers.find((tower) => tower.type === 'ice');
    if (foundry && fire) this.routeTowers(foundry, fire);
    if (fire && ice) this.routeTowers(fire, ice);
    if (waveIndex === 3) this.tryPlaceTower('amplifier', 6, 1);
    this.waveIndex = waveIndex;
    this.money = waveIndex === 2 ? 35 : 45;
    this.selectedTowerId = null;
    this.interactionMode = 'inspect';
    this.refreshNetworkVisuals();
    this.refreshSelectionVisual();
    this.updateUi(true);
  }

  private createExplosionSkillDemo(): void {
    if (this.stageIndex !== 1) this.switchStage(1);
    else this.resetRun();
    this.waveIndex = 3;
    this.money = 9999;
    this.stageTwoLanceIntroduced = true;
    this.tryPlaceTower('lance', 0, 0);
    const explosion = this.towers.find((tower) => tower.type === 'lance');
    if (!explosion) return;
    for (let index = 0; index < this.lanceThreshold(explosion); index += 1) {
      explosion.buffer.push({ id: this.nextRoundId++, damage: 17, elements: index % 2 === 0 ? ['fire'] : ['ice'] });
    }
    this.spawnEnemy('riftling', 0);
    this.spawnEnemy('riftling', 0);
    this.spawnEnemy('wisp', 0);
    const [inside, outside, otherLayer] = this.enemies;
    if (!inside || !outside || !otherLayer) return;
    inside.group.position.copy(explosion.group.position).add(new THREE.Vector3(EXPLOSION_RADIUS * 0.6, 0.45, 0));
    outside.group.position.copy(explosion.group.position).add(new THREE.Vector3(EXPLOSION_RADIUS + 0.35, 0.45, 0));
    otherLayer.group.position.copy(explosion.group.position).add(new THREE.Vector3(0.5, LAYER_HEIGHTS[1] - LAYER_HEIGHTS[0] + 0.45, 0));
    const outsideHp = outside.hp;
    const otherLayerHp = otherLayer.hp;
    this.fireExplosion(explosion, this.lanceThreshold(explosion));
    this.lastExplosionOutsideDamage = Math.max(0, outsideHp - outside.hp);
    this.lastExplosionOtherLayerDamage = Math.max(0, otherLayerHp - otherLayer.hp);
    this.updateEnemyStatusPresentation();
    this.updateUi(true);
    this.publishDiagnostics();
  }

  private createRelayLockDemo(): void {
    if (this.stageIndex !== 1) this.switchStage(1);
    else this.resetRun();
    this.money = 9999;
    this.tryPlaceTower('foundry', 0, 2);
    this.tryPlaceTower('fire', 2, 2);
    this.tryPlaceTower('ice', 2, 0);
    const source = this.towers.find((tower) => tower.type === 'foundry');
    const relay = this.towers.find((tower) => tower.type === 'fire');
    const output = this.towers.find((tower) => tower.type === 'ice');
    if (!source || !relay || !output) return;
    this.connectTowers(source, relay);
    this.connectTowers(relay, output);
    const board = this.activeStage().board;
    let extra: TowerState | null = null;
    for (let gx = 0; gx < board.width && !extra; gx += 1) {
      for (let gz = 0; gz < board.depth && !extra; gz += 1) {
        const placement = this.validateFootprint('foundry', gx, gz, null);
        if (!placement.valid || placement.layer !== relay.layer) continue;
        const start = this.gridToWorld(gx, gz, placement.layer).add(new THREE.Vector3(0, 0.68, 0));
        const end = this.towerPort(relay);
        if (start.distanceTo(end) > TOWER_DEFINITIONS.foundry.connectionRange
          || this.firstBlockerHit(start, end, placement.layer) !== null) continue;
        const priorCount = this.towers.length;
        if (this.tryPlaceTower('foundry', gx, gz) && this.towers.length > priorCount) extra = this.towers[this.towers.length - 1] ?? null;
      }
    }
    if (!extra) return;
    this.selectedTowerId = extra.id;
    this.interactionMode = 'inspect';
    this.beginLink();
    this.refreshNetworkVisuals();
    this.refreshSelectionVisual();
    this.updateUi(true);
    this.publishDiagnostics();
  }

  private publishDiagnostics(): void {
    const info = this.renderer.info;
    const towerLinks = this.towers.flatMap((source) => {
      const target = this.linkedReceiver(source);
      if (!target) return [];
      return [{
        sourceId: source.id,
        targetId: target.id,
        distance: this.towerPort(source).distanceTo(this.towerPort(target)),
        range: this.connectionRange(source),
        facingError: Math.abs(Math.atan2(
          Math.sin(source.group.rotation.y + this.angleToTower(source, target)),
          Math.cos(source.group.rotation.y + this.angleToTower(source, target)),
        )),
      }];
    });
    const connections = towerLinks.length;
    const blocked = this.towers.filter((tower) => tower.blockedReason.length > 0).length;
    const elementalStatuses = this.enemies.reduce((count, enemy) => count + this.activeEnemyElements(enemy).length, 0);
    const tintedEnemies = this.enemies.filter((enemy) => this.activeEnemyElements(enemy).length > 0).length;
    const statusIcons = [...this.statusIconMeshes.values()].reduce((count, mesh) => count + mesh.count, 0);
    const linkSource = this.linkSourceTowerId === null ? null : this.findTower(this.linkSourceTowerId) ?? null;
    const linkCandidates = linkSource ? this.towers
      .filter((tower) => tower.id !== linkSource.id)
      .map((tower) => {
        const validation = this.validateLink(linkSource, tower);
        return {
          towerId: tower.id,
          valid: validation.valid,
          highlighted: this.selectionGroup.getObjectByName(`link-target-valid-${tower.id}`) !== undefined,
          reason: validation.reason,
        };
      }) : [];
    const terminalBuffTowerIds = this.towers.filter((tower) =>
      TOWER_DEFINITIONS[tower.type].element !== undefined
      && this.linkedReceiver(tower) === null
      && this.towers.some((source) => this.linkedReceiver(source)?.id === tower.id),
    ).map((tower) => tower.id);
    const finiteAmmoTowerIds = this.towers.filter((tower) => Number.isFinite(this.capacity(tower))).map((tower) => tower.id);
    const capacityBlockedTowerIds = this.towers.filter((tower) => tower.blockedReason.includes('Kho đạn')).map((tower) => tower.id);
    const { originX, originZ } = this.activeStage().board;
    const gridError = (value: number, origin: number) => Math.abs((value - origin) / CELL_SIZE - Math.round((value - origin) / CELL_SIZE));
    const pathGridAlignmentError = this.pathXZ.reduce((max, point) => Math.max(max, gridError(point.x, originX), gridError(point.y, originZ)), 0);
    const pathAxisAlignmentError = this.pathXZ.slice(1).reduce((max, point, index) => {
      const delta = point.clone().sub(this.pathXZ[index]);
      return Math.max(max, Math.min(Math.abs(delta.x), Math.abs(delta.y)));
    }, 0);
    const linkTutorialObjectives = [
      'place-foundry', 'place-fire', 'select-foundry', 'drag-foundry-fire', 'release-foundry-fire', 'start-wave-1',
      'place-ice', 'select-fire', 'drag-fire-ice', 'release-fire-ice', 'start-wave-2',
      'place-terminal-fire', 'select-ice', 'drag-ice-terminal', 'release-ice-terminal', 'start-wave-3', 'complete',
    ] as const;
    const rotationTutorialObjectives = [
      'place-foundry', 'place-fire', 'select-foundry', 'rotate-foundry-fire', 'start-wave-1',
      'place-ice', 'select-fire', 'rotate-fire-ice', 'start-wave-2',
      'place-terminal-fire', 'select-ice', 'rotate-ice-terminal', 'select-terminal-fire', 'rotate-terminal-lane', 'start-wave-3', 'complete',
    ] as const;
    const tutorialObjectives = ROUTING_MODE === 'link' ? linkTutorialObjectives : rotationTutorialObjectives;
    const tutorialObjective = this.isGuidedTutorialActive()
      ? tutorialObjectives[Math.min(this.tutorialStep, tutorialObjectives.length - 1)]
      : this.stageIndex === 1 && this.waveIndex === 3 && !this.stageTwoLanceFeederIntroduced ? 'link-foundry-lance' : '';
    const requiredTutorialTower = this.stageTwoRequiredTower();
    const lessonCell = requiredTutorialTower ? this.findStageTwoLessonCell(requiredTutorialTower) : null;
    const lessonPositions = requiredTutorialTower && lessonCell
      ? this.footprintKeys(requiredTutorialTower, lessonCell.gx, lessonCell.gz).map((key) => {
        const cell = this.cells.get(key);
        return cell ? this.gridToWorld(cell.gx, cell.gz, cell.layer) : null;
      }).filter((position): position is THREE.Vector3 => position !== null)
      : [];
    const lessonCenter = lessonPositions.length > 0
      ? lessonPositions.reduce((sum, position) => sum.add(position), new THREE.Vector3()).multiplyScalar(1 / lessonPositions.length)
      : null;
    const lance = this.towers.find((tower) => tower.type === 'lance') ?? null;
    const lanceAmmoBar = lance?.group.getObjectByName('lanceAmmoBar') ?? null;
    const lanceFeeder = this.towers.find((tower) => tower.group.userData.stageTwoLanceFeeder === true) ?? null;
    let overlappingEnemyPairs = 0;
    for (let first = 0; first < this.enemies.length; first += 1) {
      for (let second = first + 1; second < this.enemies.length; second += 1) {
        const a = this.enemies[first];
        const b = this.enemies[second];
        if (ENEMY_DEFINITIONS[a.kind].layer !== ENEMY_DEFINITIONS[b.kind].layer) continue;
        const overlapDistance = (ENEMY_DEFINITIONS[a.kind].radius + ENEMY_DEFINITIONS[b.kind].radius) * 0.9;
        if (a.group.position.distanceTo(b.group.position) <= overlapDistance) overlappingEnemyPairs += 1;
      }
    }
    const stageWaves = this.activeStage().waves;
    const waveThreats = stageWaves.map((wave) => Math.round(wave.orders.reduce((total, order) => {
      const enemy = ENEMY_DEFINITIONS[order.kind];
      return total + enemy.hp * wave.healthMultiplier * (1 + enemy.speed * ENEMY_SPEED_MULTIPLIER * 0.15) + enemy.nexusDamage * 15;
    }, 0)));
    const waveSpawnDensities = stageWaves.map((wave) => {
      const firstSpawn = wave.orders.reduce((min, order) => Math.min(min, order.at), Number.POSITIVE_INFINITY);
      const lastSpawn = wave.orders.reduce((max, order) => Math.max(max, order.at), 0);
      const spawnWindow = Math.max(0.75, lastSpawn - firstSpawn + 0.75);
      return Math.round(wave.orders.length / spawnWindow * 100) / 100;
    });
    const waveSpawnWindows = stageWaves.map((wave) => {
      const firstSpawn = wave.orders.reduce((min, order) => Math.min(min, order.at), Number.POSITIVE_INFINITY);
      const lastSpawn = wave.orders.reduce((max, order) => Math.max(max, order.at), 0);
      return Math.round(Math.max(0.75, lastSpawn - firstSpawn + 0.75) * 100) / 100;
    });
    const waveFlyingEnemyCounts = stageWaves.map((wave) => wave.orders.filter(
      (order) => ENEMY_DEFINITIONS[order.kind].layer > 0,
    ).length);
    const waveBarrierEnemyCounts = stageWaves.map((wave) => wave.orders.filter(
      (order) => ENEMY_DEFINITIONS[order.kind].reactionBarrier !== undefined,
    ).length);
    const waveResistantEnemyCounts = stageWaves.map((wave) => wave.orders.filter((order) => {
      const enemy = ENEMY_DEFINITIONS[order.kind];
      return (enemy.resist?.length ?? 0) > 0 || (enemy.immune?.length ?? 0) > 0;
    }).length);
    const lanceEffects = this.effects.filter((effect) => effect.object.userData.effectKind === 'explosion');
    const activeExplosion = lanceEffects[0]?.object ?? null;
    const explosionZone = activeExplosion?.getObjectByName('explosion-zone') ?? null;
    const explosionRingCount = activeExplosion?.getObjectsByProperty('name', 'explosion-ring').length ?? 0;
    const explosionShardCount = activeExplosion?.getObjectsByProperty('name', 'explosion-shard').length ?? 0;
    const spawnDirectionMarker = this.boardGroup.getObjectByName('enemy-spawn-direction');
    let spawnDirectionError = Math.PI;
    let spawnDirectionInView = false;
    let spawnDirectionViewportX = 0;
    let spawnDirectionViewportY = 0;
    if (spawnDirectionMarker) {
      const expected = new THREE.Vector2(
        Number(spawnDirectionMarker.userData.directionX) || 0,
        Number(spawnDirectionMarker.userData.directionZ) || 0,
      ).normalize();
      const rotation = spawnDirectionMarker.getWorldQuaternion(new THREE.Quaternion());
      const actual3 = new THREE.Vector3(1, 0, 0).applyQuaternion(rotation);
      const actual = new THREE.Vector2(actual3.x, actual3.z).normalize();
      spawnDirectionError = Math.acos(THREE.MathUtils.clamp(expected.dot(actual), -1, 1));
      const projected = spawnDirectionMarker.getWorldPosition(new THREE.Vector3()).project(this.camera);
      spawnDirectionViewportX = projected.x;
      spawnDirectionViewportY = projected.y;
      spawnDirectionInView = projected.z >= -1 && projected.z <= 1
        && Math.abs(projected.x) <= 0.92 && Math.abs(projected.y) <= 0.92;
    }
    window.__THREE_GAME_DIAGNOSTICS__ = {
      frame: this.frame,
      routingMode: ROUTING_MODE,
      elapsed: this.elapsed,
      phase: this.phase,
      stage: this.stageIndex + 1,
      wave: this.waveIndex + 1,
      money: this.money,
      lives: this.lives,
      towers: this.towers.length,
      enemies: this.enemies.length,
      projectiles: this.projectiles.length,
      infusions: this.infusionCount,
      projectileInterceptions: this.projectileInterceptionCount,
      layerOneEnemyHits: this.layerOneEnemyHitCount,
      reactions: this.reactionCount,
      elementalStatuses,
      tintedEnemies,
      statusIcons,
      impactParticles: this.impactParticleStates.length,
      impactParticleBursts: this.impactParticleBursts,
      draggingTower: this.buildDrag?.dragging ?? false,
      pathRibbonMeshes: this.boardGroup.children.filter((child) => child.name.startsWith('enemy-path-')).length,
      spawnDirectionMarkerCount: spawnDirectionMarker ? 1 : 0,
      spawnDirectionError,
      spawnDirectionInView,
      spawnDirectionViewportX,
      spawnDirectionViewportY,
      tutorialHandVisible: !this.tutorialHandElement.classList.contains('hidden'),
      tutorialHandMode: this.tutorialHandElement.dataset.mode ?? '',
      discoveryCueKind: this.discoveryCue?.kind ?? '',
      discoveryCueVisible: !this.discoveryCueElement.classList.contains('hidden'),
      discoveryCueHighlightOnly: this.discoveryCue?.highlightOnly ?? false,
      discoveryCueTriggerCounts: { ...this.discoveryCueTriggerCounts },
      maxTowerLayer: this.towers.reduce((max, tower) => Math.max(max, tower.layer), 0),
      layerOneTowerCount: this.towers.filter((tower) => tower.layer === 1).length,
      oppositeRaisedCellCount: [...this.cells.values()].filter((cell) => cell.layer === 1 && cell.gx <= 2).length,
      oppositeRaisedTowerCount: this.towers.filter((tower) => tower.layer === 1 && tower.gx <= 2).length,
      maxLayerOneTowerLaneDistance: this.towers.reduce((max, tower) => tower.layer === 1
        ? Math.max(max, this.distanceToEnemyPath(tower.group.position.x, tower.group.position.z))
        : max, 0),
      maxEnemyLayer: this.enemies.reduce((max, enemy) => Math.max(max, ENEMY_DEFINITIONS[enemy.kind].layer), 0),
      activeEnemyKinds: [...new Set(this.enemies.map((enemy) => enemy.kind))],
      maxStageEnemyLayer: this.activeStage().waves.reduce((max, wave) => Math.max(max, ...wave.orders.map((order) => ENEMY_DEFINITIONS[order.kind].layer)), 0),
      maxBoardLayer: [...this.cells.values()].reduce((max, cell) => Math.max(max, cell.layer), 0),
      boardWidth: this.activeStage().board.width,
      boardDepth: this.activeStage().board.depth,
      buildableCellCount: [...this.cells.values()].filter((cell) => cell.buildable).length,
      maxEnemyFacingError: this.enemies.reduce((max, enemy) => Math.max(max, Number(enemy.group.userData.facingError) || 0), 0),
      maxEnemyLaneOffset: this.enemies.reduce((max, enemy) => Math.max(max, Math.abs(enemy.sideOffset)), 0),
      upcomingEnemyCount: this.phase === 'ready' ? this.activeStage().waves[Math.min(this.waveIndex, this.activeStage().waves.length - 1)].orders.length : 0,
      upcomingEnemyKinds: this.phase === 'ready' ? [...new Set(this.activeStage().waves[Math.min(this.waveIndex, this.activeStage().waves.length - 1)].orders.map((order) => order.kind))] : [],
      selectedWaveEnemyKind: this.selectedWaveEnemyKind,
      inspectedBuildType: this.inspectedBuildType,
      unlockedTowers: BUILD_ORDER.filter((type) => this.isTowerUnlocked(type)).length,
      connections,
      interactionMode: this.interactionMode,
      selectedTowerId: this.selectedTowerId,
      selectedOutputAngle: this.selectedTower()?.aimAngle ?? null,
      linkSourceTowerId: this.linkSourceTowerId,
      towerLinks,
      maxLinkedTowerFacingError: towerLinks.reduce((max, link) => Math.max(max, link.facingError), 0),
      linkCandidates,
      lastLinkAttempt: this.lastLinkAttempt,
      towerConnectionRanges: Object.fromEntries(this.towers.map((tower) => [tower.id, this.connectionRange(tower)])),
      towerBuffers: Object.fromEntries(this.towers.map((tower) => [tower.id, tower.buffer.length])),
      finiteAmmoTowerIds,
      capacityBlockedTowerIds,
      projectileLaunchesByTower: Object.fromEntries(this.projectileLaunchesByTower),
      linkedProjectileLaunches: this.linkedProjectileLaunches,
      unlinkedProjectileLaunches: this.unlinkedProjectileLaunches,
      terminalBuffTowerIds,
      terminalBuffProjectileLaunches: this.terminalBuffProjectileLaunches,
      linkedSegmentEnemyHits: this.linkedSegmentEnemyHits,
      linkedSegmentDamage: this.linkedSegmentDamage,
      linkGuideObjects: this.networkGroup.children.filter((child) => child.name.startsWith('tower-link-')).length,
      weaponAimGuideObjects: this.selectionGroup.getObjectByName('weapon-aim-selected') ? 1 : 0,
      weaponAimGuideWidth: SELECTED_AIM_GUIDE_RADIUS * 2,
      weaponAimGuideOpacity: SELECTED_AIM_GUIDE_OPACITY,
      projectileSpeedMultiplier: PROJECTILE_SPEED_MULTIPLIER,
      towerFireRateMultiplier: TOWER_FIRE_RATE_MULTIPLIER,
      projectileCollisionRadius: PROJECTILE_COLLISION_RADIUS,
      projectileVisualScale: PROJECTILE_VISUAL_SCALE,
      enemySpeedMultiplier: ENEMY_SPEED_MULTIPLIER,
      lanceAmmoBarCount: this.towers.filter((tower) => tower.type === 'lance' && tower.group.getObjectByName('lanceAmmoBar') !== undefined).length,
      lanceAmmoRatio: lanceAmmoBar ? Number(lanceAmmoBar.userData.ratio ?? 0) : 0,
      lanceFeederConnected: Boolean(lance && lanceFeeder && this.linkedReceiver(lanceFeeder)?.id === lance.id),
      overlappingEnemyPairs,
      blocked,
      tutorialStep: this.tutorialStep,
      tutorialObjective,
      tutorialHeadOnDot: Math.cos(this.findTutorialTower('terminalFire')?.aimAngle ?? 0),
      tutorialDirectShots: this.unlinkedProjectileLaunches,
      reactionTutorialPopupVisible: this.reactionTutorialPopupVisible,
      elementalTintStrength: ELEMENT_STATUS_TINT,
      stageStartingMoney: this.activeStage().startingMoney,
      killRewardMultiplier: this.activeStage().killRewardMultiplier,
      enemyRewardMultiplier: ENEMY_REWARD_MULTIPLIER,
      waveClearRewardMultiplier: WAVE_CLEAR_REWARD_MULTIPLIER,
      pathLength: this.pathTotalLength,
      pathPoints: this.pathXZ.map((point) => ({ x: point.x, z: point.y })),
      pathSegmentLengths: [...this.pathSegmentLengths],
      pathGridAlignmentError,
      pathAxisAlignmentError,
      maxHorizontalPathSegment: this.pathXZ.slice(1).reduce((max, point, index) => Math.abs(point.y - this.pathXZ[index].y) < 0.001
        ? Math.max(max, Math.abs(point.x - this.pathXZ[index].x)) : max, 0),
      maxVerticalPathSegment: this.pathXZ.slice(1).reduce((max, point, index) => Math.abs(point.x - this.pathXZ[index].x) < 0.001
        ? Math.max(max, Math.abs(point.y - this.pathXZ[index].y)) : max, 0),
      waveCount: stageWaves.length,
      waveThreats,
      waveEnemyCounts: stageWaves.map((wave) => wave.orders.length),
      waveMaxEnemyLayers: stageWaves.map((wave) => wave.orders.reduce(
        (maxLayer, order) => Math.max(maxLayer, ENEMY_DEFINITIONS[order.kind].layer),
        0,
      )),
      waveHealthMultipliers: stageWaves.map((wave) => wave.healthMultiplier),
      waveSpawnDensities,
      waveSpawnWindows,
      waveFlyingEnemyCounts,
      waveBarrierEnemyCounts,
      waveResistantEnemyCounts,
      guidedTutorialComplete: this.activeStage().tutorial && !this.isGuidedTutorialActive(),
      tutorialMasteryPhase: this.isTutorialMasteryPhase(),
      masteryCheckpointCaptured: this.masteryCheckpoint !== null,
      masteryCheckpointMoney: this.masteryCheckpoint?.money ?? null,
      tutorialStartingLives: STAGES[0].startingLives,
      tutorialLeakDamage: 1,
      masteryWaveCounts: stageWaves.slice(3, 6).map((wave) => wave.orders.length),
      masteryWaveHealthMultipliers: stageWaves.slice(3, 6).map((wave) => wave.healthMultiplier),
      masteryWaveSpawnDensities: waveSpawnDensities.slice(3, 6),
      masteryWaveThreats: waveThreats.slice(3, 6),
      currentWaveHealthMultiplier: this.currentWaveHealthMultiplier(),
      reactionMaxHpDamageRatio: REACTION_MAX_HP_DAMAGE_RATIO,
      lastReactionBonusDamage: this.lastReactionBonusDamage,
      activeReactionBarriers: this.enemies.filter((enemy) => enemy.reactionBarrier !== null).length,
      activeArmoredEnemies: this.enemies.filter((enemy) => enemy.kind === 'arcaneBulwark').length,
      armoredRushingEnemies: this.enemies.filter((enemy) => enemy.kind === 'arcaneBulwark' && enemy.barrierBroken).length,
      visibleArmorShells: this.enemies.filter((enemy) => enemy.kind === 'arcaneBulwark'
        && enemy.group.getObjectByName('armorShell')?.visible !== false).length,
      maxEnemySpeedMultiplier: this.enemies.reduce((max, enemy) => Math.max(max, enemy.speedMultiplier), 0),
      visibleDetailedEnemies: this.enemies.filter((enemy) => enemy.group.visible).length,
      activeEnemyMaxHp: this.enemies.reduce((max, enemy) => Math.max(max, enemy.maxHp), 0),
      lanceVfxCount: lanceEffects.length,
      lanceVfxAnchorError: this.lanceVfxMaxAnchorError,
      lanceVfxScaleError: this.lanceVfxMaxScaleError,
      explosionRadius: EXPLOSION_RADIUS,
      explosionHits: this.lastExplosionHitCount,
      explosionDamage: this.lastExplosionDamage,
      explosionOutsideDamage: this.lastExplosionOutsideDamage,
      explosionOtherLayerDamage: this.lastExplosionOtherLayerDamage,
      explosionVisualRadius: explosionZone ? EXPLOSION_RADIUS * explosionZone.scale.x : 0,
      explosionRingCount,
      explosionShardCount,
      explosionTargetCueCount: this.lastExplosionTargetCueCount,
      requiredTutorialTower,
      lessonCell: lessonCell ? { gx: lessonCell.gx, gz: lessonCell.gz } : null,
      lessonCellLaneDistance: lessonCenter ? this.distanceToEnemyPath(lessonCenter.x, lessonCenter.z) : null,
      objectiveProgress: this.waveIndex / this.activeStage().waves.length,
      renderer: {
        calls: info.render.calls,
        triangles: info.render.triangles,
        geometries: info.memory.geometries,
        textures: info.memory.textures,
      },
      canvas: {
        clientWidth: this.canvas.clientWidth,
        clientHeight: this.canvas.clientHeight,
        width: this.canvas.width,
        height: this.canvas.height,
        dpr: Math.min(window.devicePixelRatio || 1, 1.5),
      },
    };
  }

  private getElement(selector: string): HTMLElement {
    const element = document.querySelector<HTMLElement>(selector);
    if (!element) throw new Error(`Missing element: ${selector}`);
    return element;
  }

  private getButton(selector: string): HTMLButtonElement {
    const element = document.querySelector<HTMLButtonElement>(selector);
    if (!element) throw new Error(`Missing button: ${selector}`);
    return element;
  }
}
