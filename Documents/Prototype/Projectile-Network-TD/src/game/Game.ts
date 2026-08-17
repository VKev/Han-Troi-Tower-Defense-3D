import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';
import { ArtFactory } from '../assets/ArtFactory';
import { MaterialLibrary } from '../assets/MaterialLibrary';
import { Loop } from '../core/Loop';
import { createRenderer, resizeRenderer } from '../core/Renderer';
import { AudioSystem } from '../systems/AudioSystem';
import { createSeededRandom } from '../utils/random';
import {
  ACTIVE_STAGE, ACTIVE_STAGE_INDEX, BRANCH_DESCRIPTIONS, BRANCH_NAMES, BUILD_GRID_SPACING, BUILD_ORDER, BUILD_SLOTS, ELEMENT_COLORS, ENEMY_DEFINITIONS, ENEMY_REWARD_MULTIPLIER,
  ENEMY_SPEED_MULTIPLIER,
  ENEMY_PATH, FIXED_STEP, HIGH_GROUND_PLATFORMS, MAP_BOUNDS, MAX_LINK_RANGE, MAX_SOUL, NODE_DEFINITIONS,
  MASTERY_CHECKPOINT_GOLD, PROJECTILE_RADIUS, PROJECTILE_SPEED, PROJECTILE_VISUAL_SCALE, REACTIONS,
  REACTION_MAX_HP_DAMAGE_RATIO, SELL_REFUND, STAGES, STARTING_BASE_HP, STARTING_GOLD,
  TOWER_FIRE_RATE_MULTIPLIER, TOWER_PURCHASE_PRICE_GROWTH_CAP, TOWER_PURCHASE_PRICE_GROWTH_PER_TOWER,
  WAVE_CLEAR_REWARD_MULTIPLIER, WAVES, buildSlotIdAt, nodeCapacity, nodeInterval, resolveReaction,
  type Branch, type Element, type EnemyKind, type EnemyState, type GamePhase,
  type NodeState, type NodeType, type Payload, type ProjectileState, type ReactionKey,
  type PurchasableNodeType,
} from './definitions';

interface SlotVisual { id: number; mesh: THREE.Group; occupiedNodeId: number | null; }
interface LinkVisual { sourceId: number; targetId: number; group: THREE.Group; }
interface ChainCompletionNotice {
  readonly routeNodeIds: readonly number[];
  readonly points: readonly THREE.Vector3[];
  readonly segmentLengths: readonly number[];
  readonly totalLength: number;
  readonly group: THREE.Group;
  readonly ledCores: THREE.InstancedMesh;
  readonly ledHalos: THREE.InstancedMesh;
  readonly passDuration: number;
  readonly gapDuration: number;
  elapsed: number;
}
interface VfxState {
  group: THREE.Group; remaining: number; duration: number; rise: number;
  fadeFromAuthoredOpacity?: boolean; ballisticParticles?: boolean;
}
interface Obstacle { box: THREE.Box3; group: THREE.Group; }
interface CameraPointer { x: number; y: number; }
interface BuildDragState {
  pointerId: number; type: PurchasableNodeType; button: HTMLButtonElement;
  origin: THREE.Vector2; dragging: boolean; slotId: number | null; valid: boolean; reason: string;
}
interface SoulSkillDragState { pointerId: number; point: THREE.Vector3 | null; }
type TongueBranch = 'base' | 'suppression' | 'conduction';
interface TongueProfile {
  readonly branch: TongueBranch; readonly radius: number; readonly flatDamage: number;
  readonly maxHpRatio: number; readonly maxHpCap: number; readonly color: number;
}
interface CapturedTongueEnemy {
  readonly group: THREE.Group; readonly offset: THREE.Vector3; readonly originalScale: THREE.Vector3;
}
interface TongueStrikeState {
  readonly group: THREE.Group; readonly root: THREE.Mesh; readonly core: THREE.Mesh; readonly tip: THREE.Mesh;
  readonly start: THREE.Vector3; readonly target: THREE.Vector3; readonly profile: TongueProfile;
  readonly outbound: number; readonly hold: number; readonly retract: number; readonly captured: CapturedTongueEnemy[];
  elapsed: number; impacted: boolean;
}
type StageTwoLessonType = 'support' | 'special';
interface VictoryTravelState {
  readonly actor: THREE.Group;
  readonly route: readonly THREE.Vector3[];
  readonly segmentLengths: readonly number[];
  readonly totalLength: number;
  distance: number;
  hopHeight: number;
  maxHopHeight: number;
  landingIndex: number;
  fadeRemaining: number | null;
  navigationStarted: boolean;
}

interface MasteryNodeSnapshot {
  readonly id: number; readonly type: PurchasableNodeType; readonly slotId: number;
  readonly outputTargetId: number | null; readonly totalInvested: number; readonly branch: Branch | null;
  readonly buffer: Array<{
    id: number; physicalDamage: number; magicDamage: number; baseElement: Element | null;
    reaction: keyof typeof REACTIONS | null; reactionProcAvailable: boolean; reactionPotency: number;
    directHitEnemyIds: number[];
  }>;
  readonly reservedIncoming: number; readonly timer: number; readonly charge: number; readonly pulseCharge: number;
}

interface MasteryCheckpoint {
  readonly gold: number; readonly soul: number; readonly nodes: readonly MasteryNodeSnapshot[];
  readonly nextNodeId: number; readonly nextPayloadId: number; readonly nextProjectileId: number;
  readonly nextEnemyId: number;
  readonly currencyTutorialSeen: boolean; readonly baseTutorialSeen: boolean;
}

const TUTORIAL_NODE_SLOTS = {
  nexus: ACTIVE_STAGE.tutorial ? buildSlotIdAt(-1, 4) : BUILD_SLOTS[0].id,
  generator: ACTIVE_STAGE.tutorial ? buildSlotIdAt(-5, -4) : BUILD_SLOTS[1].id,
  fire: ACTIVE_STAGE.tutorial ? buildSlotIdAt(-5, 0) : BUILD_SLOTS[2].id,
  ice: ACTIVE_STAGE.tutorial ? buildSlotIdAt(-1, -6) : BUILD_SLOTS[3].id,
} as const;
const TUTORIAL_TYPES: Partial<Record<number, PurchasableNodeType>> = { 0: 'nexus', 1: 'generator', 4: 'fire', 8: 'ice' };
const TUTORIAL_PLACEMENT_SLOTS: Partial<Record<number, number>> = {
  0: TUTORIAL_NODE_SLOTS.nexus, 1: TUTORIAL_NODE_SLOTS.generator,
  4: TUTORIAL_NODE_SLOTS.fire, 8: TUTORIAL_NODE_SLOTS.ice,
};
const TUTORIAL_LINK_STEPS = new Set([2, 5, 6, 9, 10]);
const TUTORIAL_START_STEPS = new Set([3, 7, 11]);
const TUTORIAL_COMPLETE_STEP = 12;
const INITIAL_CAMERA_TARGET = new THREE.Vector3(...ACTIVE_STAGE.board.cameraTarget);
const INITIAL_CAMERA_POSITION = new THREE.Vector3(...ACTIVE_STAGE.board.cameraPosition);
const INITIAL_CAMERA_DIRECTION = INITIAL_CAMERA_POSITION.clone().sub(INITIAL_CAMERA_TARGET).normalize();
const INITIAL_CAMERA_YAW = Math.atan2(INITIAL_CAMERA_DIRECTION.x, INITIAL_CAMERA_DIRECTION.z);
const INITIAL_CAMERA_PITCH = Math.asin(INITIAL_CAMERA_DIRECTION.y);
const INITIAL_CAMERA_DISTANCE = INITIAL_CAMERA_POSITION.distanceTo(INITIAL_CAMERA_TARGET);
const MIN_CAMERA_DISTANCE = Math.max(20, INITIAL_CAMERA_DISTANCE * 0.62);
const MAX_CAMERA_DISTANCE = Math.max(46, INITIAL_CAMERA_DISTANCE * 1.38);
const CAMERA_MIN_PITCH = THREE.MathUtils.degToRad(28);
const CAMERA_MAX_PITCH = THREE.MathUtils.degToRad(68);
const CAMERA_MOUSE_ORBIT_SPEED = 0.006;
const CAMERA_TOUCH_ORBIT_SPEED = 0.0052;
const VICTORY_TRAVEL_SPEED = 5.8;
const VICTORY_HOP_LENGTH = 2.6;
const VICTORY_FADE_DURATION = 0.62;
const TONGUE_ROOT_RADIUS = 0.22;
const TONGUE_BODY_ROOT_RADIUS = 0.2;
const TONGUE_BODY_TIP_RADIUS = 0.34;
const TONGUE_TIP_RADIUS = 0.68;
const TONGUE_CAPTURE_SCALE = 0.62;
const TONGUE_IMPACT_DISC_OPACITY = 0.06;
const TONGUE_DIRT_PARTICLE_COUNT = 14;
const REACTION_REPEAT_COOLDOWN = 2.25;
const TUTORIAL_ENDPOINT_PULSE_DURATION = 0.46;
const TUTORIAL_ENDPOINT_PULSE_PEAK_SCALE = 1.34;
const NODE_DIM_COLOR_MULTIPLIER = 0.32;
const NODE_DIM_EMISSIVE_MULTIPLIER = 0.08;
const NODE_DIM_EMISSIVE_INTENSITY_MULTIPLIER = 0.12;
const REACTION_KEYS = Object.keys(REACTIONS) as ReactionKey[];
const REACTION_TUTORIAL_STORAGE_KEY = 'projectile-network-td:reaction-tutorial-acknowledged';

function createReactionCooldowns(): Record<ReactionKey, number> {
  return Object.fromEntries(REACTION_KEYS.map((reaction) => [reaction, 0])) as Record<ReactionKey, number>;
}

function reactionTutorialAcknowledged(): boolean {
  try { return window.sessionStorage.getItem(REACTION_TUTORIAL_STORAGE_KEY) === '1'; }
  catch { return false; }
}

function acknowledgeReactionTutorial(): void {
  try { window.sessionStorage.setItem(REACTION_TUTORIAL_STORAGE_KEY, '1'); }
  catch { /* Storage can be unavailable in privacy-restricted browser contexts. */ }
}

function clamp(value: number, min: number, max: number): number { return Math.max(min, Math.min(max, value)); }

function stableWorldNoise(index: number, salt: number): number {
  const value = Math.sin(index * 91.733 + salt * 37.719) * 43758.5453;
  return value - Math.floor(value);
}

function segmentSphereEntry(start: THREE.Vector3, end: THREE.Vector3, center: THREE.Vector3, radius: number): number | null {
  const direction = end.clone().sub(start);
  const offset = start.clone().sub(center);
  const a = direction.dot(direction);
  if (a <= 1e-8) return start.distanceToSquared(center) <= radius * radius ? 0 : null;
  const b = 2 * offset.dot(direction);
  const c = offset.dot(offset) - radius * radius;
  const discriminant = b * b - 4 * a * c;
  if (discriminant < 0) return null;
  const root = Math.sqrt(discriminant);
  const first = (-b - root) / (2 * a);
  const second = (-b + root) / (2 * a);
  if (first >= 0 && first <= 1) return first;
  if (second >= 0 && second <= 1) return second;
  return null;
}

function distanceToSegmentXZ(point: THREE.Vector3, start: THREE.Vector3, end: THREE.Vector3): number {
  const dx = end.x - start.x;
  const dz = end.z - start.z;
  const lengthSquared = dx * dx + dz * dz;
  if (lengthSquared <= 1e-8) return Math.hypot(point.x - start.x, point.z - start.z);
  const t = clamp(((point.x - start.x) * dx + (point.z - start.z) * dz) / lengthSquared, 0, 1);
  return Math.hypot(point.x - (start.x + dx * t), point.z - (start.z + dz * t));
}

function disposeObject(root: THREE.Object3D): void {
  root.traverse((object) => {
    if (!(object instanceof THREE.Mesh || object instanceof THREE.Line || object instanceof THREE.Points || object instanceof THREE.Sprite)) return;
    if (!(object instanceof THREE.Sprite)) object.geometry.dispose();
    const materials = Array.isArray(object.material) ? object.material : [object.material];
    materials.forEach((material) => {
      const mapped = material as THREE.Material & { map?: THREE.Texture | null };
      if (mapped.map && mapped.map.userData.shared !== true) mapped.map.dispose();
      material.dispose();
    });
  });
}

export class Game {
  private readonly renderer;
  private readonly scene = new THREE.Scene();
  private readonly camera = new THREE.PerspectiveCamera(43, 1, 0.1, 180);
  private readonly loop = new Loop((delta, elapsed) => this.update(delta, elapsed), () => this.render());
  private readonly materials = new MaterialLibrary();
  private readonly art = new ArtFactory(this.materials);
  private readonly audio = new AudioSystem();
  private readonly raycaster = new THREE.Raycaster();
  private readonly pointerNdc = new THREE.Vector2();
  private readonly baseNexus = this.art.createBaseNexus();
  private readonly frogActor = this.baseNexus.getObjectByName('frogActor') as THREE.Group;
  private readonly worldGroup = new THREE.Group();
  private readonly nodeGroup = new THREE.Group();
  private readonly projectileGroup = new THREE.Group();
  private readonly enemyGroup = new THREE.Group();
  private readonly linkGroup = new THREE.Group();
  private readonly selectionGroup = new THREE.Group();
  private readonly tutorialLabelGroup = new THREE.Group();
  private readonly slotMarkerGroup = new THREE.Group();
  private readonly placementPreviewGroup = new THREE.Group();
  private readonly statusIconGroup = new THREE.Group();
  private readonly effectGroup = new THREE.Group();
  private readonly slots = new Map<number, SlotVisual>();
  private readonly nodes = new Map<number, NodeState>();
  private readonly projectiles: ProjectileState[] = [];
  private readonly enemies: EnemyState[] = [];
  private readonly links: LinkVisual[] = [];
  private chainCompletionNotice: ChainCompletionNotice | null = null;
  private tongueStrike: TongueStrikeState | null = null;
  private readonly vfx: VfxState[] = [];
  private readonly obstacles: Obstacle[] = [];
  private readonly activePointers = new Map<number, CameraPointer>();
  private readonly statusIconMeshes = new Map<Element, THREE.InstancedMesh>();
  private readonly statusIconBackdrop = this.createStatusIconBackdrop();

  private readonly buildList = this.el('#build-list');
  private readonly tutorialChainReminder = this.el('#tutorial-chain-reminder');
  private readonly stageLabel = this.el('#stage-label');
  private readonly stageButtons = [...document.querySelectorAll<HTMLAnchorElement>('#stage-select [data-stage]')];
  private readonly baseValue = this.el('#base-value');
  private readonly goldValue = this.el('#gold-value');
  private readonly waveValue = this.el('#wave-value');
  private readonly enemyValue = this.el('#enemy-value');
  private readonly soulValue = this.el('#soul-value');
  private readonly phaseLabel = this.el('#phase-label');
  private readonly waveTitle = this.el('#wave-title');
  private readonly waveEnemies = this.el('#wave-enemies');
  private readonly startWaveButton = this.button('#start-wave');
  private readonly speedButtons = [...document.querySelectorAll<HTMLButtonElement>('#speed-controls [data-speed]')];
  private readonly soundButton = this.button('#sound-button');
  private readonly restartButton = this.button('#restart-button');
  private readonly soulSkillButton = this.button('#soul-skill');
  private readonly inspector = this.el('#node-inspector');
  private readonly inspectorRole = this.el('#inspector-role');
  private readonly inspectorState = this.el('#inspector-state');
  private readonly inspectorIcon = this.el('#inspector-icon');
  private readonly inspectorName = this.el('#inspector-name');
  private readonly inspectorBranch = this.el('#inspector-branch');
  private readonly inspectorDetail = this.el('#inspector-detail');
  private readonly queueMeter = this.el('#queue-meter');
  private readonly queueFill = this.el('#queue-fill');
  private readonly queueValue = this.el('#queue-value');
  private readonly chargeMeter = this.el('#charge-meter');
  private readonly chargeLabel = this.el('#charge-label');
  private readonly chargeFill = this.el('#charge-fill');
  private readonly chargeValue = this.el('#charge-value');
  private readonly branchControls = this.el('#branch-controls');
  private readonly branchA = this.button('#branch-a');
  private readonly branchB = this.button('#branch-b');
  private readonly upgradeButton = this.button('#action-upgrade');
  private readonly sellButton = this.button('#action-sell');
  private readonly tutorialHand = this.el('#tutorial-hand');
  private readonly toast = this.el('#toast');
  private readonly resultOverlay = this.el('#result-overlay');
  private readonly resultKicker = this.el('#result-kicker');
  private readonly resultTitle = this.el('#result-title');
  private readonly resultCopy = this.el('#result-copy');
  private readonly resultRestart = this.button('#result-restart');
  private readonly reactionTutorial = this.el('#reaction-tutorial-overlay');
  private readonly reactionTutorialTitle = this.el('#reaction-tutorial-title');
  private readonly reactionFormulaA = this.el('#reaction-formula-a');
  private readonly reactionFormulaB = this.el('#reaction-formula-b');
  private readonly reactionFormulaResult = this.el('#reaction-formula-result');
  private readonly reactionTutorialContinue = this.button('#reaction-tutorial-continue');
  private readonly linkDragOverlay = this.el('#link-drag-overlay');

  private phase: GamePhase = 'preparation';
  private gold = STARTING_GOLD;
  private baseHp = STARTING_BASE_HP;
  private soul = 0;
  private waveIndex = 0;
  private waveClock = 0;
  private spawnIndex = 0;
  private accumulator = 0;
  private selectedBuildType: PurchasableNodeType | null = null;
  private buildDrag: BuildDragState | null = null;
  private selectedNodeId: number | null = null;
  private linkSourceId: number | null = null;
  private linkHoverTargetId: number | null = null;
  private linkPointerId: number | null = null;
  private linkPointerWorld: THREE.Vector3 | null = null;
  private tutorialEndpointRouteComplete: boolean | null = null;
  private tutorialEndpointPulseRemaining = 0;
  private tutorialEndpointPulseTransitions = 0;
  private tutorialEndpointPulseDirection: 'connected' | 'disconnected' | null = null;
  private soulTargeting = false;
  private soulSkillDrag: SoulSkillDragState | null = null;
  private soulTargetPreview: THREE.Group | null = null;
  private soulSkillTutorial: 'button' | 'target' | 'complete' = ACTIVE_STAGE.tutorial ? 'button' : 'complete';
  private nexusNodeId: number | null = null;
  private nextNodeId = 1;
  private nextPayloadId = 1;
  private nextProjectileId = 1;
  private nextEnemyId = 1;
  private tutorialStep = ACTIVE_STAGE.tutorial ? 0 : TUTORIAL_COMPLETE_STEP;
  private tutorialReactionSeen = reactionTutorialAcknowledged();
  private reactionTutorialDelay = -1;
  private reactionTutorialVisible = false;
  private pendingReaction: keyof typeof REACTIONS | null = null;
  private masteryCheckpoint: MasteryCheckpoint | null = null;
  private currencyTutorialSeen = false;
  private baseTutorialSeen = false;
  private currencyHighlightTime = 0;
  private baseHighlightTime = 0;
  private victoryTravel: VictoryTravelState | null = null;
  private suppressVictoryNavigation = false;
  private rng = createSeededRandom(20260816);
  private cameraTarget = INITIAL_CAMERA_TARGET.clone();
  private cameraDistance = INITIAL_CAMERA_DISTANCE;
  private cameraYaw = INITIAL_CAMERA_YAW;
  private cameraPitch = INITIAL_CAMERA_PITCH;
  private panningPointerId: number | null = null;
  private panLast = new THREE.Vector2();
  private panStart = new THREE.Vector2();
  private panMoved = false;
  private orbitingPointerId: number | null = null;
  private orbitLast = new THREE.Vector2();
  private pinchDistance = 0;
  private pinchCentroid = new THREE.Vector2();
  private reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  private screenshotPaused = false;
  private gameSpeed: 1 | 2 | 3 = 1;
  private frame = 0;
  private elapsedTime = 0;
  private directHits = 0;
  private layerOneEnemyHits = 0;
  private reactionProcs = 0;
  private blockedReactionProcs = 0;
  private readonly projectileLaunchesByNode = new Map<number, number>();
  private specialPulses = 0;
  private soulCasts = 0;
  private tongueCorridorHits = 0;
  private tongueImpactHits = 0;
  private tongueCapturedKills = 0;
  private cameraShakeRemaining = 0;
  private cameraShakeDuration = 0;
  private cameraShakeStrength = 0;
  private killedEnemies = 0;
  private leakedEnemies = 0;
  private lastToastTimer = 0;
  private readonly stageTwoLessonSlots = new Map<StageTwoLessonType, number>();

  constructor(private readonly canvas: HTMLCanvasElement) {
    this.renderer = createRenderer(canvas);
    this.scene.add(this.worldGroup, this.linkGroup, this.nodeGroup, this.projectileGroup, this.enemyGroup, this.selectionGroup, this.tutorialLabelGroup, this.slotMarkerGroup, this.placementPreviewGroup, this.statusIconGroup, this.effectGroup);
    this.createStatusIconMeshes();
    this.statusIconGroup.add(this.statusIconBackdrop);
    this.createLighting();
    this.createWorld();
    this.createBuildCards();
    this.installUi();
    this.installInput();
    this.updateCamera();
    this.updateFrogSkillAnchor();
    this.validateNetwork();
    this.renderWaveRoster();
    this.updateUi(true);
    this.publishHooks();
  }

  start(): void { this.loop.start(); }

  dispose(): void {
    this.loop.stop();
    window.removeEventListener('resize', this.onResize);
    window.removeEventListener('keydown', this.onKeyDown);
    this.canvas.removeEventListener('pointerdown', this.onPointerDown);
    this.canvas.removeEventListener('pointermove', this.onPointerMove);
    this.canvas.removeEventListener('pointerup', this.onPointerUp);
    this.canvas.removeEventListener('pointercancel', this.onPointerCancel);
    this.canvas.removeEventListener('wheel', this.onWheel);
    this.canvas.removeEventListener('contextmenu', this.onContextMenu);
    this.buildList.removeEventListener('pointerdown', this.onBuildPointerDown);
    this.soulSkillButton.removeEventListener('pointerdown', this.onSoulSkillPointerDown);
    window.removeEventListener('pointermove', this.onBuildPointerMove);
    window.removeEventListener('pointermove', this.onSoulSkillPointerMove);
    window.removeEventListener('pointerup', this.onBuildPointerUp);
    window.removeEventListener('pointerup', this.onSoulSkillPointerUp);
    window.removeEventListener('pointercancel', this.onBuildPointerCancel);
    window.removeEventListener('pointercancel', this.onSoulSkillPointerCancel);
    this.clearSoulTargetPreview();
    this.clearTongueStrike();
    this.audio.dispose();
    this.disposeStatusIconMeshes();
    this.art.dispose();
    this.materials.dispose();
    this.renderer.dispose();
    delete window.__THREE_GAME_DIAGNOSTICS__;
    delete window.__THREE_GAME_TEST_HOOKS__;
  }

  private createLighting(): void {
    this.scene.background = new THREE.Color(0xe1ad63);
    this.scene.fog = new THREE.FogExp2(0xd59b59, 0.011);
    const hemisphere = new THREE.HemisphereLight(0xffdf9d, 0x6d4328, 2.05);
    this.scene.add(hemisphere);
    const sun = new THREE.DirectionalLight(0xffe0a0, 3.8);
    sun.position.set(-18, 30, 14);
    sun.castShadow = true;
    sun.shadow.mapSize.set(1536, 1536);
    const shadowExtent = Math.max(24, ACTIVE_STAGE.board.islandRadius + 3);
    sun.shadow.camera.left = -shadowExtent; sun.shadow.camera.right = shadowExtent;
    sun.shadow.camera.top = shadowExtent; sun.shadow.camera.bottom = -shadowExtent;
    this.scene.add(sun);
    const rainLight = new THREE.PointLight(0x6bd8ef, 16, 25, 2);
    rainLight.position.set(7, 7, 5);
    this.scene.add(rainLight);
  }

  private createWorld(): void {
    const underworld = new THREE.Mesh(new THREE.CircleGeometry(ACTIVE_STAGE.board.islandRadius + 9, 64), this.materials.void);
    underworld.rotation.x = -Math.PI / 2;
    underworld.position.y = -0.48;
    this.worldGroup.add(underworld);
    const ground = new THREE.Mesh(
      new THREE.BoxGeometry(MAP_BOUNDS.maxX - MAP_BOUNDS.minX, 0.72, MAP_BOUNDS.maxZ - MAP_BOUNDS.minZ),
      this.materials.ground,
    );
    ground.name = 'battlefieldGround';
    ground.position.set((MAP_BOUNDS.minX + MAP_BOUNDS.maxX) / 2, -0.36, (MAP_BOUNDS.minZ + MAP_BOUNDS.maxZ) / 2);
    ground.receiveShadow = true;
    this.worldGroup.add(ground);
    this.createPath();
    const [endX, endZ] = ENEMY_PATH[ENEMY_PATH.length - 1];
    this.baseNexus.position.set(endX - 0.8, 0, endZ);
    this.worldGroup.add(this.baseNexus);
    this.createSlots();
    this.createHighGround();
    this.createWorldProps();
    this.createOuterWorldProps();
  }

  private createPath(): void {
    const shoulder = new THREE.Mesh(this.createPathRibbonGeometry(2.28), this.materials.pathShoulder);
    shoulder.name = 'enemy-path-shoulder'; shoulder.position.y = 0.035; shoulder.receiveShadow = true;
    const border = new THREE.Mesh(this.createPathRibbonGeometry(1.98), this.materials.pathEdge);
    border.name = 'enemy-path-border'; border.position.y = 0.085; border.receiveShadow = true;
    const surface = new THREE.Mesh(this.createPathRibbonGeometry(1.66), this.materials.path);
    surface.name = 'enemy-path-surface'; surface.position.y = 0.145; surface.receiveShadow = true;
    this.worldGroup.add(shoulder, border, surface);
    this.createPathEdgeClods();
  }

  private createPathEdgeClods(): void {
    const countPerSide = Math.max(8, Math.floor(this.pathLength() / 2.65));
    const clods = new THREE.InstancedMesh(
      new THREE.DodecahedronGeometry(0.18, 0),
      this.materials.pathEdge,
      countPerSide * 2,
    );
    clods.name = 'enemy-path-edge-clods';
    clods.castShadow = false;
    clods.receiveShadow = true;
    const dummy = new THREE.Object3D();
    let instance = 0;
    for (let index = 0; index < countPerSide; index += 1) {
      const progress = (index + 0.45) / countPerSide * this.pathLength();
      for (const side of [-1, 1] as const) {
        const transform = this.pathTransform(progress + side * ((index % 3) - 1) * 0.12, side * (1.04 + (index % 4) * 0.035), 0);
        const scale = 0.58 + ((index * 7 + (side > 0 ? 3 : 0)) % 6) * 0.07;
        dummy.position.copy(transform.position).setY(0.16 + (index % 2) * 0.012);
        dummy.rotation.set(index * 0.17, transform.rotation + index * 0.49, side * 0.12);
        dummy.scale.set(scale * 1.2, scale * 0.56, scale * 0.82);
        dummy.updateMatrix();
        clods.setMatrixAt(instance++, dummy.matrix);
      }
    }
    clods.instanceMatrix.needsUpdate = true;
    this.worldGroup.add(clods);
  }

  private createPathRibbonGeometry(width: number): THREE.BufferGeometry {
    const path = ENEMY_PATH.map(([x, z]) => new THREE.Vector2(x, z));
    const halfWidth = width / 2; const positions: number[] = []; const normals: number[] = []; const uvs: number[] = []; const indices: number[] = [];
    let distance = 0;
    for (let index = 0; index < path.length; index += 1) {
      const point = path[index]; const previous = path[Math.max(0, index - 1)]; const next = path[Math.min(path.length - 1, index + 1)];
      const previousDirection = point.clone().sub(previous).normalize(); const nextDirection = next.clone().sub(point).normalize();
      const direction = index === 0 ? nextDirection : index === path.length - 1 ? previousDirection : previousDirection.clone().add(nextDirection).normalize();
      const previousNormal = new THREE.Vector2(-previousDirection.y, previousDirection.x); const nextNormal = new THREE.Vector2(-nextDirection.y, nextDirection.x);
      let miter = index === 0 ? nextNormal : index === path.length - 1 ? previousNormal : previousNormal.clone().add(nextNormal).normalize();
      if (miter.lengthSq() < 0.0001) miter = new THREE.Vector2(-direction.y, direction.x);
      const denominator = Math.max(0.42, Math.abs(miter.dot(index === 0 ? nextNormal : previousNormal)));
      const miterLength = Math.min(halfWidth * 2.2, halfWidth / denominator);
      const left = point.clone().addScaledVector(miter, miterLength); const right = point.clone().addScaledVector(miter, -miterLength);
      if (index > 0) distance += point.distanceTo(path[index - 1]);
      positions.push(left.x, 0, left.y, right.x, 0, right.y); normals.push(0, 1, 0, 0, 1, 0); uvs.push(0, distance / 5.5, 1, distance / 5.5);
      if (index < path.length - 1) { const current = index * 2; const following = current + 2; indices.push(current, following, current + 1, following, following + 1, current + 1); }
    }
    const geometry = new THREE.BufferGeometry(); geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
    geometry.setAttribute('normal', new THREE.Float32BufferAttribute(normals, 3)); geometry.setAttribute('uv', new THREE.Float32BufferAttribute(uvs, 2));
    geometry.setIndex(indices); geometry.computeBoundingSphere(); return geometry;
  }

  private createSlots(): void {
    for (const slot of BUILD_SLOTS) {
      const group = new THREE.Group();
      group.position.set(...slot.position);
      group.userData.slotId = slot.id;
      this.slotMarkerGroup.add(group);
      this.slots.set(slot.id, { id: slot.id, mesh: group, occupiedNodeId: null });
    }
    this.slotMarkerGroup.visible = false;
  }

  private createHighGround(): void {
    for (const definition of HIGH_GROUND_PLATFORMS) {
      const platform = { center: new THREE.Vector3(...definition.center), size: new THREE.Vector3(...definition.size) };
      const group = new THREE.Group();
      const block = new THREE.Mesh(new THREE.BoxGeometry(platform.size.x, platform.size.y, platform.size.z), this.materials.highGround);
      block.position.copy(platform.center);
      block.receiveShadow = true;
      const top = new THREE.Mesh(
        new THREE.BoxGeometry(platform.size.x + 0.12, 0.12, platform.size.z + 0.12),
        this.materials.pathRune,
      );
      top.position.copy(platform.center).add(new THREE.Vector3(0, platform.size.y * 0.5 + 0.06, 0));
      top.receiveShadow = true;
      group.add(block, top);
      this.worldGroup.add(group);
      this.obstacles.push({ box: new THREE.Box3().setFromCenterAndSize(platform.center, platform.size), group });
    }
  }

  private createStatusIconBackdrop(): THREE.InstancedMesh {
    const mesh = new THREE.InstancedMesh(
      new THREE.CircleGeometry(0.31, 18),
      new THREE.MeshBasicMaterial({ color: 0x0b0c18, transparent: true, opacity: 0.82, depthWrite: false }),
      256,
    );
    mesh.count = 0; mesh.frustumCulled = false; mesh.renderOrder = 14;
    return mesh;
  }

  private createElementIconGeometry(element: Element): THREE.ShapeGeometry {
    const shape = new THREE.Shape();
    if (element === 'fire') {
      shape.moveTo(0, 0.29); shape.bezierCurveTo(0.2, 0.12, 0.25, -0.07, 0.1, -0.26);
      shape.bezierCurveTo(0.02, -0.1, -0.09, -0.08, -0.13, -0.25); shape.bezierCurveTo(-0.31, -0.02, -0.2, 0.17, 0, 0.29);
    } else if (element === 'ice') {
      for (let index = 0; index < 12; index += 1) {
        const angle = Math.PI / 2 + index / 12 * Math.PI * 2; const radius = index % 2 === 0 ? 0.29 : 0.1;
        const x = Math.cos(angle) * radius; const y = Math.sin(angle) * radius;
        if (index === 0) shape.moveTo(x, y); else shape.lineTo(x, y);
      }
      shape.closePath();
    } else if (element === 'wind') {
      shape.moveTo(0.3, 0); shape.lineTo(-0.14, 0.24); shape.lineTo(-0.05, 0.07);
      shape.lineTo(-0.3, 0.07); shape.lineTo(-0.3, -0.07); shape.lineTo(-0.05, -0.07); shape.lineTo(-0.14, -0.24); shape.closePath();
    } else {
      for (let index = 0; index < 6; index += 1) {
        const angle = Math.PI / 6 + index / 6 * Math.PI * 2; const x = Math.cos(angle) * 0.27; const y = Math.sin(angle) * 0.27;
        if (index === 0) shape.moveTo(x, y); else shape.lineTo(x, y);
      }
      shape.closePath();
    }
    return new THREE.ShapeGeometry(shape, 1);
  }

  private createStatusIconMeshes(): void {
    for (const element of ['fire', 'ice', 'wind', 'earth'] as const) {
      const mesh = new THREE.InstancedMesh(
        this.createElementIconGeometry(element),
        new THREE.MeshBasicMaterial({ color: ELEMENT_COLORS[element], transparent: true, opacity: 1, depthWrite: false, toneMapped: false }),
        64,
      );
      mesh.count = 0; mesh.frustumCulled = false; mesh.renderOrder = 15;
      this.statusIconMeshes.set(element, mesh); this.statusIconGroup.add(mesh);
    }
  }

  private disposeStatusIconMeshes(): void {
    this.statusIconBackdrop.geometry.dispose();
    (this.statusIconBackdrop.material as THREE.Material).dispose();
    for (const mesh of this.statusIconMeshes.values()) {
      mesh.geometry.dispose(); (mesh.material as THREE.Material).dispose();
    }
    this.statusIconMeshes.clear();
  }

  private mergeStaticDecoration(root: THREE.Group, name: string, castShadow: boolean): number {
    root.updateMatrixWorld(true);
    const mergedParts: THREE.BufferGeometry[] = [];
    const sourceGeometries = new Set<THREE.BufferGeometry>();
    const instanceMatrix = new THREE.Matrix4();
    const worldMatrix = new THREE.Matrix4();
    root.traverse((object) => {
      if (!(object instanceof THREE.Mesh)) return;
      sourceGeometries.add(object.geometry);
      const material = (Array.isArray(object.material) ? object.material[0] : object.material) as THREE.MeshStandardMaterial;
      const instanceCount = object instanceof THREE.InstancedMesh ? object.count : 1;
      for (let index = 0; index < instanceCount; index += 1) {
        if (object instanceof THREE.InstancedMesh) {
          object.getMatrixAt(index, instanceMatrix);
          worldMatrix.multiplyMatrices(object.matrixWorld, instanceMatrix);
        } else worldMatrix.copy(object.matrixWorld);
        const part = object.geometry.index ? object.geometry.toNonIndexed() : object.geometry.clone();
        part.applyMatrix4(worldMatrix);
        const colors = new Float32Array(part.getAttribute('position').count * 3);
        const color = material.color ?? new THREE.Color(0xffffff);
        for (let vertex = 0; vertex < colors.length; vertex += 3) color.toArray(colors, vertex);
        part.setAttribute('color', new THREE.BufferAttribute(colors, 3));
        mergedParts.push(part);
      }
    });
    const mergedGeometry = mergeGeometries(mergedParts, false);
    if (!mergedGeometry) throw new Error(`Unable to merge static decoration: ${name}.`);
    const sourcePartCount = mergedParts.length;
    mergedParts.forEach((part) => part.dispose());
    sourceGeometries.forEach((geometry) => geometry.dispose());
    root.clear();
    const decoration = new THREE.Mesh(mergedGeometry, this.materials.outerDecoration);
    decoration.name = name;
    decoration.castShadow = castShadow;
    decoration.receiveShadow = true;
    root.add(decoration);
    return sourcePartCount;
  }

  private createWorldProps(): void {
    const root = new THREE.Group();
    root.name = 'battlefield-drought-decoration';
    const width = MAP_BOUNDS.maxX - MAP_BOUNDS.minX;
    const depth = MAP_BOUNDS.maxZ - MAP_BOUNDS.minZ;
    const centerX = (MAP_BOUNDS.minX + MAP_BOUNDS.maxX) * 0.5;
    const centerZ = (MAP_BOUNDS.minZ + MAP_BOUNDS.maxZ) * 0.5;
    const candidates: Array<[number, number]> = [];
    const candidateCount = 50 + ACTIVE_STAGE_INDEX * 18;
    const goldenAngle = Math.PI * (3 - Math.sqrt(5));
    for (let index = 0; index < candidateCount; index += 1) {
      const angle = index * goldenAngle + stableWorldNoise(index, 41) * 0.42;
      const normalizedRadius = Math.sqrt(0.16 + 0.82 * ((index + 0.5) / candidateCount));
      const radialJitter = 0.92 + stableWorldNoise(index, 42) * 0.14;
      candidates.push([
        centerX + Math.cos(angle) * width * 0.48 * normalizedRadius * radialJitter,
        centerZ + Math.sin(angle) * depth * 0.48 * normalizedRadius * radialJitter,
      ]);
    }
    const clearForDecoration = (x: number, z: number): boolean => {
      if (this.distanceToEnemyPath(x, z) < 2.15) return false;
      return BUILD_SLOTS.every((slot) => Math.hypot(slot.position[0] - x, slot.position[2] - z) > 0.95);
    };
    let rocks = 0;
    let grassPatches = 0;
    let twigs = 0;
    let deadTrees = 0;
    let placedProps = 0;
    candidates.forEach(([x, z], index) => {
      if (!clearForDecoration(x, z)) return;
      const placementIndex = placedProps++;
      const isTree = placementIndex % 11 === 0;
      const isTwig = !isTree && placementIndex % 4 === 0;
      const isGrass = !isTree && !isTwig && placementIndex % 3 === 0;
      const prop = isTree
        ? this.art.createDeadTree(0.95 + (index % 4) * 0.12, index)
        : isTwig
          ? this.createBattlefieldTwig(0.68 + (index % 4) * 0.1, index)
        : isGrass
          ? this.art.createWitheredGrassPatch(0.72 + (index % 5) * 0.08, index)
          : this.art.createDroughtRock(0.42 + (index % 4) * 0.09, index);
      if (isTree) deadTrees += 1;
      else if (isTwig) twigs += 1;
      else if (isGrass) grassPatches += 1;
      else rocks += 1;
      prop.position.set(x, 0, z);
      root.add(prop);
    });
    const sourceParts = this.mergeStaticDecoration(root, 'battlefieldDecorationMerged', true);
    root.userData.counts = { rocks, grassPatches, twigs, deadTrees, sourceParts, mergedMeshes: 1 };
    this.worldGroup.add(root);
  }

  private createBattlefieldTwig(size: number, variant: number): THREE.Group {
    const root = new THREE.Group();
    root.name = 'battlefield-twig';
    const stem = new THREE.Mesh(new THREE.CylinderGeometry(size * 0.045, size * 0.065, size, 5), this.materials.dryWood);
    stem.position.set(0, size * 0.075, 0);
    stem.rotation.z = Math.PI * 0.5;
    root.add(stem);
    const fork = new THREE.Mesh(new THREE.CylinderGeometry(size * 0.035, size * 0.052, size * 0.52, 5), this.materials.dryWood);
    fork.position.set(size * 0.27, size * 0.09, size * 0.13);
    fork.rotation.set(Math.PI * 0.5, 0, Math.PI * 0.26);
    root.add(fork);
    root.rotation.y = variant * 1.73;
    return root;
  }

  private createOuterWorldProps(): void {
    const root = new THREE.Group();
    root.name = 'outer-drought-decoration';
    const width = MAP_BOUNDS.maxX - MAP_BOUNDS.minX;
    const depth = MAP_BOUNDS.maxZ - MAP_BOUNDS.minZ;
    const center = new THREE.Vector2(
      (MAP_BOUNDS.minX + MAP_BOUNDS.maxX) * 0.5,
      (MAP_BOUNDS.minZ + MAP_BOUNDS.maxZ) * 0.5,
    );
    const halfWidth = width * 0.5;
    const halfDepth = depth * 0.5;
    const outerRadius = ACTIVE_STAGE.board.islandRadius + 7.4;
    const stageDensity = ACTIVE_STAGE_INDEX;
    const rockCount = 34 + stageDensity * 14;
    const grassPatchCount = 42 + stageDensity * 18;
    const grassBladeCount = grassPatchCount * 4;
    const twigCount = 24 + stageDensity * 10;
    const twigSegmentCount = twigCount * 2;
    const treeCount = 9 + stageDensity * 4;
    const branchCount = treeCount * 3;
    const groundY = -0.45;
    const up = new THREE.Vector3(0, 1, 0);
    const dummy = new THREE.Object3D();

    const perimeterPoint = (index: number, count: number, salt: number, minOffset = 1.35): THREE.Vector3 => {
      const turn = (index + 0.36 + (stableWorldNoise(index, salt) - 0.5) * 0.56) / count;
      const angle = turn * Math.PI * 2;
      const direction = new THREE.Vector2(Math.cos(angle), Math.sin(angle));
      const rectDistance = Math.min(
        Math.abs(direction.x) < 0.0001 ? Number.POSITIVE_INFINITY : halfWidth / Math.abs(direction.x),
        Math.abs(direction.y) < 0.0001 ? Number.POSITIVE_INFINITY : halfDepth / Math.abs(direction.y),
      );
      const centerAlongRay = center.dot(direction);
      const centerLengthSquared = center.lengthSq();
      const circleDistance = -centerAlongRay + Math.sqrt(Math.max(0, centerAlongRay * centerAlongRay + outerRadius * outerRadius - centerLengthSquared));
      const available = Math.max(minOffset + 0.2, circleDistance - rectDistance - 0.75);
      const offset = minOffset + stableWorldNoise(index, salt + 1) * Math.min(5.8, available - minOffset);
      return new THREE.Vector3(center.x + direction.x * (rectDistance + offset), groundY, center.y + direction.y * (rectDistance + offset));
    };

    const finalizeInstances = (mesh: THREE.InstancedMesh, count: number): void => {
      mesh.count = count;
      mesh.instanceMatrix.setUsage(THREE.StaticDrawUsage);
      mesh.instanceMatrix.needsUpdate = true;
      mesh.computeBoundingBox();
      mesh.computeBoundingSphere();
      mesh.castShadow = false;
      mesh.receiveShadow = true;
      root.add(mesh);
    };

    const darkRockCount = Math.floor(rockCount / 5) * 3 + Math.min(rockCount % 5, 3);
    const lightRockCount = rockCount - darkRockCount;
    const rockGeometry = new THREE.DodecahedronGeometry(1, 0);
    const darkRocks = new THREE.InstancedMesh(rockGeometry, this.materials.stone, darkRockCount);
    darkRocks.name = 'outerRocksDark';
    const lightRocks = new THREE.InstancedMesh(rockGeometry, this.materials.stoneLight, lightRockCount);
    lightRocks.name = 'outerRocksLight';
    let darkIndex = 0;
    let lightIndex = 0;
    for (let index = 0; index < rockCount; index += 1) {
      const position = perimeterPoint(index, rockCount, 101, 1.05);
      const size = 0.34 + stableWorldNoise(index, 103) * 0.62;
      dummy.position.copy(position).setY(groundY + size * 0.42);
      dummy.rotation.set(stableWorldNoise(index, 104) * 0.32, stableWorldNoise(index, 105) * Math.PI * 2, stableWorldNoise(index, 106) * 0.24);
      dummy.scale.set(size * (0.82 + stableWorldNoise(index, 107) * 0.55), size * (0.52 + stableWorldNoise(index, 108) * 0.34), size);
      dummy.updateMatrix();
      if (index % 5 < 3) darkRocks.setMatrixAt(darkIndex++, dummy.matrix);
      else lightRocks.setMatrixAt(lightIndex++, dummy.matrix);
    }
    finalizeInstances(darkRocks, darkIndex);
    finalizeInstances(lightRocks, lightIndex);

    const grassGeometry = new THREE.ConeGeometry(0.055, 1, 3);
    const grass = new THREE.InstancedMesh(grassGeometry, this.materials.witheredGrass, grassBladeCount);
    grass.name = 'outerWitheredGrass';
    let bladeIndex = 0;
    for (let patch = 0; patch < grassPatchCount; patch += 1) {
      const centerPoint = perimeterPoint(patch, grassPatchCount, 211, 1.15);
      const patchScale = 0.48 + stableWorldNoise(patch, 212) * 0.52;
      for (let blade = 0; blade < 4; blade += 1) {
        const angle = stableWorldNoise(patch * 4 + blade, 213) * Math.PI * 2;
        const radius = 0.08 + stableWorldNoise(patch * 4 + blade, 214) * 0.32;
        const height = patchScale * (0.58 + stableWorldNoise(patch * 4 + blade, 215) * 0.56);
        dummy.position.set(centerPoint.x + Math.cos(angle) * radius, groundY + height * 0.5, centerPoint.z + Math.sin(angle) * radius);
        dummy.rotation.set((stableWorldNoise(bladeIndex, 216) - 0.5) * 0.24, angle, (stableWorldNoise(bladeIndex, 217) - 0.5) * 0.42);
        dummy.scale.set(0.75 + stableWorldNoise(bladeIndex, 218) * 0.65, height, 0.75 + stableWorldNoise(bladeIndex, 219) * 0.4);
        dummy.updateMatrix();
        grass.setMatrixAt(bladeIndex++, dummy.matrix);
      }
    }
    finalizeInstances(grass, bladeIndex);

    const setCylinderBetween = (object: THREE.Object3D, start: THREE.Vector3, end: THREE.Vector3, radius: number): void => {
      const direction = end.clone().sub(start);
      object.position.copy(start).add(end).multiplyScalar(0.5);
      object.quaternion.setFromUnitVectors(up, direction.clone().normalize());
      object.scale.set(radius, direction.length(), radius);
      object.updateMatrix();
    };

    const twigGeometry = new THREE.CylinderGeometry(1, 1, 1, 5);
    const twigs = new THREE.InstancedMesh(twigGeometry, this.materials.dryWood, twigSegmentCount);
    twigs.name = 'outerTwigs';
    let twigSegment = 0;
    for (let index = 0; index < twigCount; index += 1) {
      const base = perimeterPoint(index, twigCount, 307, 1.25);
      const heading = stableWorldNoise(index, 308) * Math.PI * 2;
      const length = 0.55 + stableWorldNoise(index, 309) * 0.85;
      const fork = 0.32 + stableWorldNoise(index, 310) * 0.3;
      const joint = new THREE.Vector3(base.x + Math.cos(heading) * length * 0.58, groundY + 0.055, base.z + Math.sin(heading) * length * 0.58);
      const tip = new THREE.Vector3(joint.x + Math.cos(heading + 0.78) * fork, groundY + 0.07, joint.z + Math.sin(heading + 0.78) * fork);
      setCylinderBetween(dummy, new THREE.Vector3(base.x, groundY + 0.05, base.z), joint, 0.025 + stableWorldNoise(index, 311) * 0.018);
      twigs.setMatrixAt(twigSegment++, dummy.matrix);
      setCylinderBetween(dummy, joint, tip, 0.018 + stableWorldNoise(index, 312) * 0.012);
      twigs.setMatrixAt(twigSegment++, dummy.matrix);
    }
    finalizeInstances(twigs, twigSegment);

    const trunkGeometry = new THREE.CylinderGeometry(1, 1, 1, 6);
    const trunks = new THREE.InstancedMesh(trunkGeometry, this.materials.dryWood, treeCount);
    trunks.name = 'outerDeadTreeTrunks';
    const branches = new THREE.InstancedMesh(twigGeometry, this.materials.dryWood, branchCount);
    branches.name = 'outerDeadTreeBranches';
    let placedBranches = 0;
    for (let index = 0; index < treeCount; index += 1) {
      const base = perimeterPoint(index, treeCount, 401, 2.15);
      const size = 0.9 + stableWorldNoise(index, 402) * 0.85;
      const leanAngle = (stableWorldNoise(index, 403) - 0.5) * 0.22;
      const leanHeading = stableWorldNoise(index, 404) * Math.PI * 2;
      const top = new THREE.Vector3(
        base.x + Math.cos(leanHeading) * Math.sin(leanAngle) * size,
        groundY + size * 1.55,
        base.z + Math.sin(leanHeading) * Math.sin(leanAngle) * size,
      );
      setCylinderBetween(dummy, new THREE.Vector3(base.x, groundY, base.z), top, size * 0.105);
      trunks.setMatrixAt(index, dummy.matrix);
      for (let branch = 0; branch < 3; branch += 1) {
        const startFactor = 0.52 + branch * 0.17;
        const start = new THREE.Vector3().lerpVectors(new THREE.Vector3(base.x, groundY, base.z), top, startFactor);
        const direction = leanHeading + branch * 2.18 + stableWorldNoise(index * 3 + branch, 405) * 0.6;
        const length = size * (0.44 - branch * 0.045);
        const end = new THREE.Vector3(start.x + Math.cos(direction) * length, start.y + length * (0.35 + branch * 0.08), start.z + Math.sin(direction) * length);
        setCylinderBetween(dummy, start, end, size * (0.045 - branch * 0.006));
        branches.setMatrixAt(placedBranches++, dummy.matrix);
      }
    }
    finalizeInstances(trunks, treeCount);
    finalizeInstances(branches, placedBranches);

    const sourceParts = this.mergeStaticDecoration(root, 'outerDecorationMerged', false);

    root.userData.counts = {
      rocks: rockCount,
      grassPatches: grassPatchCount,
      grassBlades: grassBladeCount,
      twigs: twigCount,
      twigSegments: twigSegmentCount,
      deadTrees: treeCount,
      branches: branchCount,
      instancedGroups: 0,
      mergedMeshes: 1,
      sourceParts,
    };
    this.worldGroup.add(root);
  }

  private createBuildCards(): void {
    this.buildList.replaceChildren();
    const labels: Array<[string, PurchasableNodeType[]]> = [
      ['GỌI MƯA', ['nexus']], ['NGUỒN', ['generator']], ['NGUYÊN TỐ', ['fire', 'ice', 'wind', 'earth']], ['HỖ TRỢ', ['support', 'special']],
    ];
    labels.forEach(([label, types]) => {
      const heading = document.createElement('div');
      heading.className = 'build-group-label';
      heading.textContent = label;
      this.buildList.append(heading);
      types.forEach((type) => {
        const definition = NODE_DEFINITIONS[type];
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'build-card';
        button.dataset.type = type;
        button.style.setProperty('--node-color', `#${definition.color.toString(16).padStart(6, '0')}`);
        const tutorialEndpointName = ACTIVE_STAGE.tutorial && type === 'generator'
          ? `${definition.shortName} (ĐẦU)`
          : ACTIVE_STAGE.tutorial && type === 'nexus' ? `${definition.shortName} (CUỐI)` : definition.shortName;
        button.classList.toggle('tutorial-endpoint', ACTIVE_STAGE.tutorial && (type === 'generator' || type === 'nexus'));
        button.innerHTML = `<span class="build-icon" aria-hidden="true">${definition.icon}</span><span class="build-copy"><strong>${tutorialEndpointName}</strong><small>${definition.role}</small></span><span class="build-cost">${definition.cost}</span>`;
        this.buildList.append(button);
      });
    });
  }

  private installUi(): void {
    this.stageLabel.textContent = ACTIVE_STAGE.title.toUpperCase();
    this.stageButtons.forEach((button) => {
      const active = Number(button.dataset.stage) === ACTIVE_STAGE_INDEX;
      button.classList.toggle('active', active);
      if (active) button.setAttribute('aria-current', 'page'); else button.removeAttribute('aria-current');
    });
    this.startWaveButton.addEventListener('click', () => this.startWave());
    this.speedButtons.forEach((button) => button.addEventListener('click', () => {
      const speed = Number(button.dataset.speed);
      if (speed === 1 || speed === 2 || speed === 3) this.setGameSpeed(speed);
    }));
    this.soundButton.addEventListener('click', () => {
      const muted = this.audio.toggleMute();
      this.soundButton.textContent = muted ? '◖×' : '◖))';
    });
    this.restartButton.addEventListener('click', () => this.resetRun());
    this.resultRestart.addEventListener('click', () => {
      if (this.phase === 'won' && ACTIVE_STAGE_INDEX < 2) {
        window.location.assign(`?level=${ACTIVE_STAGE_INDEX + 2}`);
        return;
      }
      if (this.phase === 'lost' && this.masteryCheckpoint && this.isTutorialMasteryPhase()) this.restoreMasteryCheckpoint();
      else this.resetRun();
    });
    this.upgradeButton.addEventListener('click', () => this.focusBranchChoice());
    this.sellButton.addEventListener('click', () => this.sellSelectedNode());
    this.branchA.addEventListener('click', () => this.purchaseBranch(0));
    this.branchB.addEventListener('click', () => this.purchaseBranch(1));
    this.reactionTutorialContinue.addEventListener('click', () => this.dismissReactionTutorial());
  }

  private installInput(): void {
    this.canvas.addEventListener('pointerdown', this.onPointerDown);
    this.canvas.addEventListener('pointermove', this.onPointerMove);
    this.canvas.addEventListener('pointerup', this.onPointerUp);
    this.canvas.addEventListener('pointercancel', this.onPointerCancel);
    this.canvas.addEventListener('wheel', this.onWheel, { passive: false });
    this.canvas.addEventListener('contextmenu', this.onContextMenu);
    this.buildList.addEventListener('pointerdown', this.onBuildPointerDown);
    this.soulSkillButton.addEventListener('pointerdown', this.onSoulSkillPointerDown);
    window.addEventListener('pointermove', this.onBuildPointerMove, { passive: false });
    window.addEventListener('pointermove', this.onSoulSkillPointerMove, { passive: false });
    window.addEventListener('pointerup', this.onBuildPointerUp);
    window.addEventListener('pointerup', this.onSoulSkillPointerUp);
    window.addEventListener('pointercancel', this.onBuildPointerCancel);
    window.addEventListener('pointercancel', this.onSoulSkillPointerCancel);
    window.addEventListener('resize', this.onResize);
    window.addEventListener('keydown', this.onKeyDown);
  }

  private readonly onBuildPointerDown = (event: PointerEvent): void => {
    if (event.button !== 0 || this.buildDrag) return;
    const button = (event.target as HTMLElement).closest<HTMLButtonElement>('.build-card[data-type]');
    if (!button || button.disabled) return;
    const type = button.dataset.type as PurchasableNodeType;
    this.buildDrag = {
      pointerId: event.pointerId, type, button,
      origin: new THREE.Vector2(event.clientX, event.clientY),
      dragging: false, slotId: null, valid: false, reason: 'Kéo vào một ô đặt trụ.',
    };
  };

  private readonly onBuildPointerMove = (event: PointerEvent): void => {
    const drag = this.buildDrag;
    if (!drag || drag.pointerId !== event.pointerId) return;
    if (!drag.dragging) {
      if (drag.origin.distanceTo(new THREE.Vector2(event.clientX, event.clientY)) < 6) return;
      this.selectBuild(drag.type);
      if (this.selectedBuildType !== drag.type) { this.cancelBuildDrag(); return; }
      drag.dragging = true;
      drag.button.classList.add('dragging');
      document.body.classList.add('is-build-dragging');
      this.slotMarkerGroup.visible = true;
    }
    if (event.cancelable) event.preventDefault();
    drag.slotId = this.slotAt(event.clientX, event.clientY);
    const validation = drag.slotId === null
      ? { valid: false, reason: 'Thả trụ vào vùng grid đang phát sáng.' }
      : this.validatePlacement(drag.type, drag.slotId);
    drag.valid = validation.valid;
    drag.reason = validation.reason;
    this.refreshPlacementPreview(drag.type, drag.slotId, drag.valid);
  };

  private readonly onBuildPointerUp = (event: PointerEvent): void => {
    const drag = this.buildDrag;
    if (!drag || drag.pointerId !== event.pointerId) return;
    if (!drag.dragging) {
      this.buildDrag = null;
      this.selectBuild(drag.type);
      return;
    }
    const placed = drag.slotId !== null && drag.valid && this.tryPlaceSelected(drag.slotId);
    if (!placed) this.error(drag.reason || 'Không thể đặt trụ tại đây.');
    this.cancelBuildDrag();
  };

  private readonly onBuildPointerCancel = (event: PointerEvent): void => {
    if (this.buildDrag?.pointerId === event.pointerId) this.cancelBuildDrag();
  };

  private cancelBuildDrag(): void {
    this.buildDrag?.button.classList.remove('dragging');
    document.body.classList.remove('is-build-dragging');
    this.buildDrag = null;
    this.clearPlacementPreview();
    this.slotMarkerGroup.visible = this.phase === 'preparation' && this.selectedBuildType !== null;
  }

  private readonly onSoulSkillPointerDown = (event: PointerEvent): void => {
    if ((event.pointerType === 'mouse' && event.button !== 0) || this.soulSkillButton.disabled || this.soulSkillDrag) return;
    event.preventDefault();
    this.soulSkillDrag = { pointerId: event.pointerId, point: null };
    this.soulSkillButton.setPointerCapture?.(event.pointerId);
    this.soulSkillButton.classList.add('dragging');
    document.body.classList.add('is-soul-dragging');
    this.beginSoulTargeting();
  };

  private readonly onSoulSkillPointerMove = (event: PointerEvent): void => {
    const drag = this.soulSkillDrag;
    if (!drag || drag.pointerId !== event.pointerId) return;
    if (event.cancelable) event.preventDefault();
    drag.point = this.skillGroundPoint(event.clientX, event.clientY);
    this.refreshSoulTargetPreview(drag.point);
  };

  private readonly onSoulSkillPointerUp = (event: PointerEvent): void => {
    const drag = this.soulSkillDrag;
    if (!drag || drag.pointerId !== event.pointerId) return;
    const point = drag.point?.clone() ?? null;
    if (point) this.castTongueStrike(point);
    this.cancelSoulSkillDrag(point === null);
  };

  private readonly onSoulSkillPointerCancel = (event: PointerEvent): void => {
    if (this.soulSkillDrag?.pointerId === event.pointerId) this.cancelSoulSkillDrag(true);
  };

  private cancelSoulSkillDrag(cancelTargeting: boolean): void {
    if (this.soulSkillDrag && this.soulSkillButton.hasPointerCapture?.(this.soulSkillDrag.pointerId)) {
      this.soulSkillButton.releasePointerCapture(this.soulSkillDrag.pointerId);
    }
    this.soulSkillDrag = null;
    this.soulSkillButton.classList.remove('dragging');
    document.body.classList.remove('is-soul-dragging');
    this.clearSoulTargetPreview();
    if (cancelTargeting) {
      this.soulTargeting = false;
      if (this.soulSkillTutorial === 'target') this.soulSkillTutorial = 'button';
      this.refreshSelection();
      this.updateUi(true);
    }
  }

  private readonly onPointerDown = (event: PointerEvent): void => {
    this.activePointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
    this.canvas.setPointerCapture(event.pointerId);
    if (event.pointerType === 'mouse' && event.button === 2) {
      if (this.linkSourceId === null && !this.buildDrag) {
        event.preventDefault();
        this.orbitingPointerId = event.pointerId;
        this.orbitLast.set(event.clientX, event.clientY);
        this.canvas.classList.add('camera-orbiting');
      }
      return;
    }
    if (this.activePointers.size === 2) {
      const pair = this.pointerPairState();
      this.pinchDistance = pair.distance;
      this.pinchCentroid.copy(pair.centroid);
      this.panningPointerId = null;
      this.panMoved = false;
      if (this.linkSourceId !== null) this.clearLinkDrag();
      if (event.cancelable) event.preventDefault();
      return;
    }
    if (event.button !== 0 && event.pointerType === 'mouse') return;
    const nodeId = this.nodeAt(event.clientX, event.clientY);
    if (nodeId !== null) {
      if (this.phase === 'preparation' && this.nodes.get(nodeId)?.type !== 'nexus') {
        if (this.selectedNodeId !== nodeId) this.selectNode(nodeId);
        this.beginLinkDrag(nodeId, event.pointerId, event.clientX, event.clientY);
      } else {
        this.selectNode(nodeId);
      }
      return;
    }
    if (this.phase === 'preparation') {
      const slotId = this.slotAt(event.clientX, event.clientY);
      if (slotId !== null && this.tryPlaceSelected(slotId)) return;
    }
    this.panningPointerId = event.pointerId;
    this.panLast.set(event.clientX, event.clientY);
    this.panStart.copy(this.panLast);
    this.panMoved = false;
  };

  private readonly onPointerMove = (event: PointerEvent): void => {
    const previous = this.activePointers.get(event.pointerId);
    this.activePointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
    if (this.activePointers.size === 2) {
      if (event.cancelable) event.preventDefault();
      const pair = this.pointerPairState();
      if (this.pinchDistance > 0) this.cameraDistance = clamp(this.cameraDistance * (this.pinchDistance / Math.max(1, pair.distance)), MIN_CAMERA_DISTANCE, MAX_CAMERA_DISTANCE);
      const dx = pair.centroid.x - this.pinchCentroid.x;
      const dy = pair.centroid.y - this.pinchCentroid.y;
      this.cameraYaw -= dx * CAMERA_TOUCH_ORBIT_SPEED;
      this.cameraPitch = clamp(this.cameraPitch + dy * CAMERA_TOUCH_ORBIT_SPEED, CAMERA_MIN_PITCH, CAMERA_MAX_PITCH);
      this.pinchDistance = pair.distance;
      this.pinchCentroid.copy(pair.centroid);
      this.updateCamera();
      return;
    }
    if (this.orbitingPointerId === event.pointerId && previous) {
      if (event.cancelable) event.preventDefault();
      const dx = event.clientX - this.orbitLast.x;
      const dy = event.clientY - this.orbitLast.y;
      this.orbitLast.set(event.clientX, event.clientY);
      this.cameraYaw -= dx * CAMERA_MOUSE_ORBIT_SPEED;
      this.cameraPitch = clamp(this.cameraPitch + dy * CAMERA_MOUSE_ORBIT_SPEED, CAMERA_MIN_PITCH, CAMERA_MAX_PITCH);
      this.updateCamera();
      return;
    }
    if (this.linkSourceId !== null && this.linkPointerId === event.pointerId) {
      this.linkHoverTargetId = this.nodeAt(event.clientX, event.clientY);
      const source = this.nodes.get(this.linkSourceId);
      this.linkPointerWorld = source ? this.pointerWorldAtHeight(event.clientX, event.clientY, this.nodeAnchor(source).y) : null;
      this.refreshLinkHints();
      return;
    }
    if (this.panningPointerId === event.pointerId && previous) {
      const dx = event.clientX - this.panLast.x;
      const dy = event.clientY - this.panLast.y;
      this.panLast.set(event.clientX, event.clientY);
      if (this.panStart.distanceTo(this.panLast) > 6) this.panMoved = true;
      const scale = this.cameraDistance * 0.00145;
      const forward = this.cameraTarget.clone().sub(this.camera.position).setY(0).normalize();
      const right = new THREE.Vector3().crossVectors(forward, THREE.Object3D.DEFAULT_UP).normalize();
      this.cameraTarget.addScaledVector(right, -dx * scale).addScaledVector(forward, dy * scale);
      this.cameraTarget.x = clamp(this.cameraTarget.x, MAP_BOUNDS.minX * 0.55, MAP_BOUNDS.maxX * 0.55);
      this.cameraTarget.z = clamp(this.cameraTarget.z, MAP_BOUNDS.minZ * 0.55, MAP_BOUNDS.maxZ * 0.55);
      this.updateCamera();
    }
  };

  private readonly onPointerUp = (event: PointerEvent): void => {
    if (this.linkSourceId !== null && this.linkPointerId === event.pointerId) this.finishLinkDrag();
    if (this.orbitingPointerId === event.pointerId) {
      this.orbitingPointerId = null;
      this.canvas.classList.remove('camera-orbiting');
    }
    this.activePointers.delete(event.pointerId);
    if (this.panningPointerId === event.pointerId) {
      if (!this.panMoved && this.selectedNodeId !== null) {
        this.selectedNodeId = null;
        this.refreshSelection();
        this.updateUi(true);
      }
      this.panningPointerId = null;
      this.panMoved = false;
    }
    if (this.activePointers.size < 2) { this.pinchDistance = 0; this.pinchCentroid.set(0, 0); }
  };

  private readonly onPointerCancel = (event: PointerEvent): void => {
    this.activePointers.delete(event.pointerId);
    if (this.linkSourceId !== null && this.linkPointerId === event.pointerId) this.clearLinkDrag();
    if (this.orbitingPointerId === event.pointerId) {
      this.orbitingPointerId = null;
      this.canvas.classList.remove('camera-orbiting');
    }
    if (this.panningPointerId === event.pointerId) {
      this.panningPointerId = null;
      this.panMoved = false;
    }
    if (this.activePointers.size < 2) { this.pinchDistance = 0; this.pinchCentroid.set(0, 0); }
  };

  private readonly onWheel = (event: WheelEvent): void => {
    event.preventDefault();
    this.cameraDistance = clamp(this.cameraDistance + Math.sign(event.deltaY) * 2.2, MIN_CAMERA_DISTANCE, MAX_CAMERA_DISTANCE);
    this.updateCamera();
  };

  private readonly onContextMenu = (event: MouseEvent): void => { event.preventDefault(); };

  private readonly onResize = (): void => { resizeRenderer(this.renderer, this.camera, this.mobileDpr()); };
  private readonly onKeyDown = (event: KeyboardEvent): void => {
    if (event.key.toLowerCase() === 'r') this.resetRun();
    if (event.key === 'Escape') { this.soulTargeting = false; this.clearLinkDrag(); this.cancelBuildDrag(); this.selectedBuildType = null; this.slotMarkerGroup.visible = false; this.updateUi(true); }
    if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); this.startWave(); }
  };

  private nodePurchasePriceMultiplier(): number {
    const paidTowerCount = [...this.nodes.values()].filter((node) => node.type !== 'nexus' && node.group.userData.lessonGrant !== true).length;
    return 1 + Math.min(TOWER_PURCHASE_PRICE_GROWTH_CAP, paidTowerCount * TOWER_PURCHASE_PRICE_GROWTH_PER_TOWER);
  }

  private isTutorialMasteryPhase(): boolean {
    return ACTIVE_STAGE.tutorial && this.waveIndex >= 3 && this.waveIndex < WAVES.length;
  }

  private regularNodePrice(type: PurchasableNodeType): number {
    return Math.ceil(NODE_DEFINITIONS[type].cost * this.nodePurchasePriceMultiplier());
  }

  private currentNodePrice(type: PurchasableNodeType): number {
    return this.isMandatoryStageTwoLessonPurchase(type) ? 0 : this.regularNodePrice(type);
  }

  private isStageTwoLessonWave(): boolean {
    return ACTIVE_STAGE_INDEX === 1 && this.phase === 'preparation' && this.waveIndex >= 2 && this.waveIndex <= 3;
  }

  private stageTwoLessonType(): StageTwoLessonType | null {
    if (!this.isStageTwoLessonWave()) return null;
    if (this.waveIndex === 2) return 'support';
    if (this.waveIndex === 3) return 'special';
    return null;
  }

  private stageTwoLessonNode(type = this.stageTwoLessonType()): NodeState | null {
    if (!type) return null;
    return [...this.nodes.values()].find((node) => node.group.userData.stageTwoLessonType === type) ?? null;
  }

  private stageTwoRequiredNode(): PurchasableNodeType | null {
    const type = this.stageTwoLessonType();
    return type && !this.stageTwoLessonNode(type) ? type : null;
  }

  private stageTwoRequiredSlot(type: PurchasableNodeType): number | null {
    return type === 'support' || type === 'special' ? this.findStageTwoLessonSlot(type) : null;
  }

  private isMandatoryStageTwoLessonPurchase(type: PurchasableNodeType): boolean {
    return this.stageTwoRequiredNode() === type;
  }

  private stageTwoLessonSource(lesson: NodeState | null): NodeState | null {
    if (!lesson || this.nexusNodeId === null) return null;
    if (lesson.inputSourceId !== null) return this.nodes.get(lesson.inputSourceId) ?? null;
    return [...this.nodes.values()].find((node) => node.id !== lesson.id && node.outputTargetId === this.nexusNodeId) ?? null;
  }

  private stageTwoRequiredLinkPair(): { source: NodeState; target: NodeState } | null {
    const lesson = this.stageTwoLessonNode();
    if (!lesson || this.nexusNodeId === null) return null;
    const nexus = this.nodes.get(this.nexusNodeId);
    const source = this.stageTwoLessonSource(lesson);
    if (!nexus || !source) return null;
    if (lesson.inputSourceId !== source.id) return { source, target: lesson };
    if (lesson.outputTargetId !== nexus.id) return { source: lesson, target: nexus };
    return null;
  }

  private stageTwoLessonComplete(): boolean {
    const lesson = this.stageTwoLessonNode();
    return this.isStageTwoLessonWave() && lesson !== null && lesson.inputSourceId !== null
      && this.nexusNodeId !== null && lesson.outputTargetId === this.nexusNodeId;
  }

  private nodeUnlocked(type: PurchasableNodeType): boolean {
    if (ACTIVE_STAGE_INDEX === 1) {
      if (type === 'support') return this.waveIndex >= 2;
      if (type === 'special') return this.waveIndex >= 3;
      return true;
    }
    if (!ACTIVE_STAGE.tutorial) return true;
    const guided = TUTORIAL_TYPES[this.tutorialStep];
    if (guided) return type === guided;
    if (this.tutorialStep < TUTORIAL_COMPLETE_STEP) return type === 'nexus' || type === 'generator' || type === 'fire' || type === 'ice';
    if (this.isTutorialMasteryPhase()) return type === 'generator' || type === 'fire' || type === 'ice' || type === 'nexus';
    return true;
  }

  private selectBuild(type: PurchasableNodeType): void {
    if (this.phase !== 'preparation') { this.error('Chỉ xây trong giai đoạn Chuẩn bị.'); return; }
    const guided = TUTORIAL_TYPES[this.tutorialStep];
    if (guided && guided !== type) { this.audio.ui('error'); return; }
    if (!this.nodeUnlocked(type)) { this.audio.ui('error'); return; }
    if (type === 'nexus' && this.nexusNodeId !== null) { this.error('Mỗi màn chỉ có một Trống Gọi Mưa.'); return; }
    if (this.gold < this.currentNodePrice(type)) { this.error('Không đủ Vàng.'); return; }
    this.selectedBuildType = type;
    this.selectedNodeId = null;
    this.audio.ui('select');
    this.slotMarkerGroup.visible = true;
    this.refreshPlacementPreview(type, null, false);
    this.refreshSelection();
    this.updateUi(true);
  }

  private validatePlacement(type: PurchasableNodeType, slotId: number, enforceLesson = true): { valid: boolean; reason: string } {
    const slot = this.slots.get(slotId);
    if (!slot) return { valid: false, reason: 'Ngoài grid đặt trụ.' };
    if (slot.occupiedNodeId !== null) return { valid: false, reason: 'Vị trí đã có trụ.' };
    if (type === 'nexus' && this.nexusNodeId !== null) return { valid: false, reason: 'Mỗi màn chỉ có một Trống Gọi Mưa.' };
    const guidedType = TUTORIAL_TYPES[this.tutorialStep];
    if (guidedType && guidedType !== type) return { valid: false, reason: 'Hãy đặt trụ đang phát sáng.' };
    if (!this.nodeUnlocked(type)) return { valid: false, reason: 'Trụ này chưa được mở trong bài học.' };
    if (enforceLesson && this.isMandatoryStageTwoLessonPurchase(type)) {
      const lessonSlot = this.stageTwoRequiredSlot(type);
      if (lessonSlot !== slotId) return { valid: false, reason: 'Hãy đặt trụ vào ô phát sáng.' };
    }
    if (this.gold < this.currentNodePrice(type)) return { valid: false, reason: 'Không đủ Vàng.' };
    return { valid: true, reason: '' };
  }

  private tryPlaceSelected(slotId: number): boolean {
    const type = this.selectedBuildType;
    if (!type) return false;
    const validation = this.validatePlacement(type, slotId);
    if (!validation.valid) { this.error(validation.reason); return false; }
    const slot = this.slots.get(slotId)!;
    const definition = NODE_DEFINITIONS[type];
    const lessonGrant = this.isMandatoryStageTwoLessonPurchase(type) && this.stageTwoRequiredSlot(type) === slotId;
    const paidCost = lessonGrant ? 0 : this.regularNodePrice(type);
    const group = this.art.createNode(type);
    this.prepareNodeNetworkPresentation(group);
    group.userData.lessonGrant = lessonGrant;
    group.userData.stageTwoLessonType = lessonGrant && (type === 'support' || type === 'special') ? type : null;
    group.position.copy(slot.mesh.position);
    if (type === 'nexus') group.scale.setScalar(0.82);
    const node: NodeState = {
      id: this.nextNodeId++, type, group, slotId,
      outputTargetId: null, inputSourceId: null, nexusInputSourceIds: [], buffer: [], reservedIncoming: 0,
      timer: 0, charge: 0, pulseCharge: 0, totalInvested: paidCost, branch: null,
      active: false, invalidReason: 'CHƯA NỐI',
    };
    group.userData.nodeId = node.id;
    group.traverse((object) => { object.userData.nodeId = node.id; });
    this.nodeGroup.add(group);
    this.nodes.set(node.id, node);
    slot.occupiedNodeId = node.id;
    if (type === 'nexus') this.nexusNodeId = node.id;
    this.gold -= paidCost;
    if (paidCost > 0 && !this.currencyTutorialSeen) {
      this.currencyTutorialSeen = true; this.currencyHighlightTime = 2.2;
    }
    this.selectedBuildType = null;
    this.slotMarkerGroup.visible = false;
    this.clearPlacementPreview();
    this.selectedNodeId = node.id;
    if (this.tutorialStep === 0 && type === 'nexus') this.tutorialStep = 1;
    else if (this.tutorialStep === 1 && type === 'generator') this.tutorialStep = 2;
    else if (this.tutorialStep === 4 && type === 'fire') this.tutorialStep = 5;
    else if (this.tutorialStep === 8 && type === 'ice') this.tutorialStep = 9;
    this.audio.build();
    this.spawnBurst(group.position.clone().add(new THREE.Vector3(0, 1, 0)), definition.color, 1.25);
    this.validateNetwork();
    this.refreshSelection();
    this.updateUi(true);
    return true;
  }

  private selectNode(nodeId: number): void {
    if (!this.nodes.has(nodeId)) return;
    this.selectedNodeId = nodeId;
    this.selectedBuildType = null;
    this.audio.ui('select');
    this.refreshSelection();
    this.updateUi(true);
  }

  private beginLinkDrag(sourceId: number, pointerId: number, clientX: number, clientY: number): void {
    if (this.phase !== 'preparation') return;
    const source = this.nodes.get(sourceId);
    if (!source || source.type === 'nexus') return;
    this.linkSourceId = sourceId;
    this.linkHoverTargetId = null;
    this.linkPointerId = pointerId;
    this.linkPointerWorld = this.pointerWorldAtHeight(clientX, clientY, this.nodeAnchor(source).y);
    this.canvas.classList.add('link-drag');
    document.body.classList.add('is-link-dragging');
    this.refreshLinkHints();
  }

  private finishLinkDrag(): void {
    const sourceId = this.linkSourceId;
    const targetId = this.linkHoverTargetId;
    if (sourceId !== null && targetId !== null) this.connectNodes(sourceId, targetId);
    this.clearLinkDrag();
  }

  private clearLinkDrag(): void {
    this.linkSourceId = null;
    this.linkHoverTargetId = null;
    this.linkPointerId = null;
    this.linkPointerWorld = null;
    this.canvas.classList.remove('link-drag');
    this.canvas.classList.remove('link-target-valid', 'link-target-invalid');
    this.linkDragOverlay.dataset.state = 'idle';
    this.linkDragOverlay.style.width = '0px';
    document.body.classList.remove('is-link-dragging');
    this.refreshSelection();
  }

  private connectNodes(sourceId: number, targetId: number): void {
    const source = this.nodes.get(sourceId);
    const target = this.nodes.get(targetId);
    if (!source || !target) return;
    const validation = this.validateLink(source, target, false);
    if (!validation.valid) { this.error(validation.reason); return; }
    if (!this.tutorialLinkAllowed(source, target)) { this.audio.ui('error'); return; }
    const completedRoutesBefore = new Set(this.completeGeneratorRoutes().map((route) => this.routeSignature(route)));
    const affected = this.connectedComponent(new Set([source.id, target.id, ...(source.outputTargetId ? [source.outputTargetId] : [])]));
    this.clearTransport(affected);
    this.unlinkOutput(source.id);
    source.outputTargetId = target.id;
    if (target.type === 'nexus') {
      if (!target.nexusInputSourceIds.includes(source.id)) target.nexusInputSourceIds.push(source.id);
    } else target.inputSourceId = source.id;
    this.orientNodeToTarget(source, target);
    if (this.tutorialStep === 2) this.tutorialStep = 3;
    else if (this.tutorialStep === 5) this.tutorialStep = 6;
    else if (this.tutorialStep === 6) this.tutorialStep = 7;
    else if (this.tutorialStep === 9) this.tutorialStep = 10;
    else if (this.tutorialStep === 10) this.tutorialStep = 11;
    this.audio.ui('confirm');
    this.validateNetwork();
    this.refreshLinks();
    const newlyCompletedRoute = this.completeGeneratorRoutes()
      .find((route) => !completedRoutesBefore.has(this.routeSignature(route)));
    if (newlyCompletedRoute) this.startChainCompletionNotice(newlyCompletedRoute);
    this.refreshSelection();
    this.updateUi(true);
  }

  private completeGeneratorRoutes(): NodeState[][] {
    const routes: NodeState[][] = [];
    for (const generator of this.nodes.values()) {
      if (generator.type !== 'generator') continue;
      const route = [generator];
      const visited = new Set([generator.id]);
      let cursor = generator;
      while (cursor.outputTargetId !== null) {
        const next = this.nodes.get(cursor.outputTargetId);
        if (!next || visited.has(next.id)) break;
        route.push(next);
        visited.add(next.id);
        cursor = next;
        if (cursor.type === 'nexus') {
          routes.push(route);
          break;
        }
      }
    }
    return routes;
  }

  private routeSignature(route: readonly NodeState[]): string {
    return route.map((node) => node.id).join('>');
  }

  private validateLink(source: NodeState, target: NodeState, keepExisting: boolean): { valid: boolean; reason: string } {
    if (source.id === target.id) return { valid: false, reason: 'Không thể tự nối.' };
    if (source.type === 'nexus') return { valid: false, reason: 'Trống Gọi Mưa không có đầu ra.' };
    if (target.type === 'generator') return { valid: false, reason: 'Lò Đạn không có đầu vào.' };
    const sourceTier = source.slotId === null ? 'low' : BUILD_SLOTS[source.slotId]?.tier;
    const targetTier = target.slotId === null ? 'low' : BUILD_SLOTS[target.slotId]?.tier;
    if (target.type !== 'nexus' && sourceTier !== targetTier) return { valid: false, reason: 'Chỉ nối các trụ cùng tầng.' };
    if (target.type !== 'nexus' && target.inputSourceId !== null && target.inputSourceId !== source.id) return { valid: false, reason: 'Đầu vào đã được dùng.' };
    if (target.type === 'nexus' && !target.nexusInputSourceIds.includes(source.id) && target.nexusInputSourceIds.length >= 2) return { valid: false, reason: 'Trống Gọi Mưa đã đủ hai chuỗi.' };
    if (!keepExisting && source.outputTargetId === target.id) return { valid: true, reason: '' };
    const start = this.nodeAnchor(source);
    const end = this.nodeAnchor(target);
    const connectionRange = Math.min(MAX_LINK_RANGE, NODE_DEFINITIONS[source.type].connectionRange);
    if (new THREE.Vector2(start.x, start.z).distanceTo(new THREE.Vector2(end.x, end.z)) > connectionRange + 1e-6) return { valid: false, reason: 'Ngoài tầm liên kết.' };
    if (this.linkObstructed(start, end)) return { valid: false, reason: 'Địa hình chặn đường đạn.' };
    let cursor: NodeState | undefined = target;
    const visited = new Set<number>();
    while (cursor) {
      if (cursor.id === source.id) return { valid: false, reason: 'Không cho phép vòng lặp.' };
      if (visited.has(cursor.id) || cursor.outputTargetId === null) break;
      visited.add(cursor.id);
      cursor = this.nodes.get(cursor.outputTargetId);
    }
    return { valid: true, reason: '' };
  }

  private tutorialLinkAllowed(source: NodeState, target: NodeState): boolean {
    const stageTwoPair = this.stageTwoRequiredLinkPair();
    if (this.isStageTwoLessonWave() && stageTwoPair) return source.id === stageTwoPair.source.id && target.id === stageTwoPair.target.id;
    if (!TUTORIAL_LINK_STEPS.has(this.tutorialStep)) return true;
    const generator = this.nodeByType('generator'); const fire = this.nodeByType('fire');
    const ice = this.nodeByType('ice'); const nexus = this.nodeByType('nexus');
    if (this.tutorialStep === 2) return source.id === generator?.id && target.id === nexus?.id;
    if (this.tutorialStep === 5) return source.id === generator?.id && target.id === fire?.id;
    if (this.tutorialStep === 6) return source.id === fire?.id && target.id === nexus?.id;
    if (this.tutorialStep === 9) return source.id === fire?.id && target.id === ice?.id;
    return source.id === ice?.id && target.id === nexus?.id;
  }

  private unlinkOutput(sourceId: number): void {
    const source = this.nodes.get(sourceId);
    if (!source || source.outputTargetId === null) return;
    const target = this.nodes.get(source.outputTargetId);
    if (target?.type === 'nexus') target.nexusInputSourceIds = target.nexusInputSourceIds.filter((id) => id !== source.id);
    else if (target?.inputSourceId === source.id) target.inputSourceId = null;
    source.outputTargetId = null;
  }

  private validateNetwork(): void {
    this.nodes.forEach((node) => {
      node.active = node.type === 'nexus';
      node.invalidReason = node.type === 'nexus' ? '' : 'CHƯA THUỘC CHUỖI';
    });
    const generators = [...this.nodes.values()].filter((node) => node.type === 'generator');
    for (const generator of generators) {
      const path: NodeState[] = [generator];
      const visited = new Set([generator.id]);
      let cursor = generator;
      let reason = '';
      while (cursor.outputTargetId !== null) {
        const next = this.nodes.get(cursor.outputTargetId);
        if (!next) { reason = 'LIÊN KẾT HỎNG'; break; }
        if (visited.has(next.id)) { reason = 'VÒNG LẶP'; break; }
        visited.add(next.id);
        path.push(next);
        cursor = next;
        if (next.type === 'nexus') break;
      }
      if (!reason && cursor.type !== 'nexus') reason = 'THIẾU ĐIỂM KẾT';
      if (reason) {
        generator.invalidReason = reason;
        path.slice(1).forEach((node) => { if (!node.active) node.invalidReason = reason; });
        continue;
      }
      path.forEach((node) => { node.active = true; node.invalidReason = ''; });
    }
    this.refreshNodeNetworkPresentation();
  }

  private prepareNodeNetworkPresentation(group: THREE.Group): void {
    if (group.userData.networkPresentationPrepared === true) return;
    group.traverse((object) => {
      if (!(object instanceof THREE.Mesh)) return;
      const materials = Array.isArray(object.material) ? object.material : [object.material];
      const localized = materials.map((material) => {
        if (!(material instanceof THREE.MeshStandardMaterial)) return material;
        const clone = material.clone();
        clone.userData = {
          ...material.userData,
          networkBaseColor: material.color.clone(),
          networkBaseEmissive: material.emissive.clone(),
          networkBaseEmissiveIntensity: material.emissiveIntensity,
        };
        return clone;
      });
      object.material = Array.isArray(object.material) ? localized : localized[0];
    });
    group.userData.networkPresentationPrepared = true;
  }

  private refreshNodeNetworkPresentation(): void {
    this.nodes.forEach((node) => {
      const endpoint = node.type === 'generator' || node.type === 'nexus';
      const dimmed = !endpoint && !node.active;
      const wasDimmed = node.group.userData.networkVisualState === 'dimmed';
      let materialCount = 0;
      node.group.traverse((object) => {
        if (!(object instanceof THREE.Mesh)) return;
        const materials = Array.isArray(object.material) ? object.material : [object.material];
        materials.forEach((material) => {
          if (!(material instanceof THREE.MeshStandardMaterial)) return;
          const baseColor = material.userData.networkBaseColor as THREE.Color | undefined;
          const baseEmissive = material.userData.networkBaseEmissive as THREE.Color | undefined;
          const baseIntensity = material.userData.networkBaseEmissiveIntensity as number | undefined;
          if (!baseColor || !baseEmissive || baseIntensity === undefined) return;
          material.color.copy(baseColor).multiplyScalar(dimmed ? NODE_DIM_COLOR_MULTIPLIER : 1);
          material.emissive.copy(baseEmissive).multiplyScalar(dimmed ? NODE_DIM_EMISSIVE_MULTIPLIER : 1);
          material.emissiveIntensity = baseIntensity * (dimmed ? NODE_DIM_EMISSIVE_INTENSITY_MULTIPLIER : 1);
          materialCount += 1;
        });
      });
      node.group.userData.networkVisualState = dimmed ? 'dimmed' : 'full';
      node.group.userData.networkVisualReason = endpoint ? 'endpoint' : node.active ? 'complete-route' : 'incomplete-route';
      node.group.userData.networkVisualMaterialCount = materialCount;
      if (wasDimmed && !dimmed) this.spawnNodeLightUpEffect(node);
    });
  }

  private spawnNodeLightUpEffect(node: NodeState): void {
    const color = NODE_DEFINITIONS[node.type].color;
    const position = this.nodeAnchor(node);
    this.spawnPulse(position, 1.1, color);
    this.spawnBurst(position, color, 1.5);
  }

  private startWave(): void {
    if (this.phase !== 'preparation') return;
    if (this.tutorialStep < TUTORIAL_COMPLETE_STEP && !TUTORIAL_START_STEPS.has(this.tutorialStep)) { this.audio.ui('error'); return; }
    if (this.isStageTwoLessonWave() && !this.stageTwoLessonComplete()) { this.audio.ui('error'); return; }
    this.validateNetwork();
    if (![...this.nodes.values()].some((node) => node.type === 'generator' && node.active)) {
      this.error('Hãy nối Lò Đạn tới Trống Gọi Mưa.');
      return;
    }
    if (this.waveIndex >= WAVES.length) return;
    this.phase = 'wave';
    this.waveClock = 0;
    this.spawnIndex = 0;
    this.selectedBuildType = null;
    this.soulTargeting = false;
    this.audio.wave();
    this.updateUi(true);
  }

  private update(delta: number, elapsed: number): void {
    this.frame += 1;
    this.elapsedTime += delta;
    resizeRenderer(this.renderer, this.camera, this.mobileDpr());
    const clamped = Math.min(delta, 0.05);
    this.accumulator += clamped * this.gameSpeed;
    while (this.accumulator >= FIXED_STEP) {
      if (this.phase === 'wave' && !this.screenshotPaused) this.simulate(FIXED_STEP);
      else if (this.phase === 'victoryTravel' && !this.screenshotPaused) this.updateVictoryTravel(FIXED_STEP);
      this.accumulator -= FIXED_STEP;
    }
    this.animate(elapsed, clamped);
    this.updateCamera();
    this.updateFrogSkillAnchor();
    this.updateEnemyStatusPresentation();
    this.updateVfx(clamped);
    if (!this.screenshotPaused) this.updateChainCompletionNotice(clamped);
    this.updateReactionTutorial(clamped);
    this.updateTutorialCue();
    this.currencyHighlightTime = Math.max(0, this.currencyHighlightTime - clamped);
    this.baseHighlightTime = Math.max(0, this.baseHighlightTime - clamped);
    this.goldValue.closest('.metric')?.classList.toggle('tutorial-focus', this.currencyHighlightTime > 0);
    this.baseValue.closest('.metric')?.classList.toggle('tutorial-focus', this.baseHighlightTime > 0);
    if (this.lastToastTimer > 0) {
      this.lastToastTimer -= clamped;
      if (this.lastToastTimer <= 0) this.toast.classList.add('hidden');
    }
    this.updateUi(false);
    this.publishDiagnostics();
  }

  private setGameSpeed(speed: 1 | 2 | 3): void {
    this.gameSpeed = speed;
    this.speedButtons.forEach((button) => {
      const active = Number(button.dataset.speed) === speed;
      button.classList.toggle('active', active);
      button.setAttribute('aria-pressed', String(active));
    });
    this.audio.ui('select');
  }

  private simulate(delta: number): void {
    this.waveClock += delta;
    const wave = WAVES[this.waveIndex];
    while (this.spawnIndex < wave.orders.length && wave.orders[this.spawnIndex].at <= this.waveClock) {
      const order = wave.orders[this.spawnIndex++];
      this.spawnEnemy(order.kind, order.sideOffset);
    }
    this.updateSupportCharge(delta);
    this.updateNodes(delta);
    this.updateProjectiles(delta);
    this.updateEnemies(delta);
    this.updateTongueStrike(delta);
    if (this.spawnIndex >= wave.orders.length && this.enemies.length === 0 && !this.tongueStrike) this.finishWave();
  }

  private updateNodes(delta: number): void {
    const ordered = [...this.nodes.values()].sort((a, b) => a.id - b.id);
    for (const node of ordered) {
      if (!node.active || node.type === 'nexus') continue;
      node.timer = Math.max(0, node.timer - delta);
      if (node.type === 'generator') {
        if (node.timer <= 0) this.emitFromNode(node);
      } else if (node.buffer.length > 0 && node.timer <= 0) this.emitFromNode(node);
    }
  }

  private emitFromNode(source: NodeState): void {
    if (source.outputTargetId === null) return;
    const target = this.nodes.get(source.outputTargetId);
    if (!target || !target.active) return;
    if (target.type !== 'nexus' && target.buffer.length + target.reservedIncoming >= nodeCapacity(target)) return;
    const payload = source.type === 'generator' ? this.createPayload(source) : source.buffer.shift();
    if (!payload) return;
    if (target.type !== 'nexus') target.reservedIncoming += 1;
    const start = this.nodeAnchor(source);
    const end = this.nodeAnchor(target);
    const group = this.art.createProjectile(payload);
    group.scale.setScalar(PROJECTILE_VISUAL_SCALE * (1 + (payload.reaction ? 0.35 : payload.baseElement ? 0.16 : 0)));
    group.position.copy(start);
    const trailColor = payload.reaction ? REACTIONS[payload.reaction].color : payload.baseElement ? ELEMENT_COLORS[payload.baseElement] : 0xe7d998;
    const trailSampleCount = 9;
    const trailPositions = new Float32Array(trailSampleCount * 3);
    const trailColors = new Float32Array(trailSampleCount * 3);
    const baseTrailColor = new THREE.Color(trailColor);
    for (let sample = 0; sample < trailSampleCount; sample += 1) {
      trailPositions[sample * 3] = start.x;
      trailPositions[sample * 3 + 1] = start.y;
      trailPositions[sample * 3 + 2] = start.z;
      const fade = Math.pow(1 - sample / trailSampleCount, 1.45);
      trailColors[sample * 3] = baseTrailColor.r * fade;
      trailColors[sample * 3 + 1] = baseTrailColor.g * fade;
      trailColors[sample * 3 + 2] = baseTrailColor.b * fade;
    }
    const trailGeometry = new THREE.BufferGeometry();
    trailGeometry.setAttribute('position', new THREE.BufferAttribute(trailPositions, 3));
    trailGeometry.setAttribute('color', new THREE.BufferAttribute(trailColors, 3));
    const trail = new THREE.Points(trailGeometry, new THREE.PointsMaterial({
      color: 0xffffff,
      map: this.materials.projectileGlowTexture,
      size: PROJECTILE_VISUAL_SCALE * (payload.reaction ? 0.72 : payload.baseElement ? 0.58 : 0.48),
      sizeAttenuation: true,
      transparent: true,
      opacity: payload.reaction ? 0.9 : 0.72,
      vertexColors: true,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      alphaTest: 0.015,
      toneMapped: false,
    }));
    trail.name = 'projectileGlowTrail';
    trail.frustumCulled = false;
    this.projectileGroup.add(group, trail);
    this.projectiles.push({
      id: this.nextProjectileId++, payload, group, trail,
      sourceNodeId: source.id, targetNodeId: target.id, start, end, progress: 0,
      hitEnemyIds: new Set(),
    });
    this.projectileLaunchesByNode.set(source.id, (this.projectileLaunchesByNode.get(source.id) ?? 0) + 1);
    source.timer = this.effectiveInterval(source);
    this.audio.shot(payload.reaction ? 2 : payload.baseElement ? 1 : 0);
  }

  private createPayload(generator: NodeState): Payload {
    const physical = generator.branch === 'heavy' ? 26 : generator.branch === 'rapid' ? 12 : 17;
    return {
      id: this.nextPayloadId++, physicalDamage: physical, magicDamage: 0,
      baseElement: null, reaction: null, reactionProcAvailable: false, reactionPotency: 1,
      directHitEnemyIds: new Set(),
    };
  }

  private updateProjectiles(delta: number): void {
    for (let index = this.projectiles.length - 1; index >= 0; index -= 1) {
      const projectile = this.projectiles[index];
      const oldPosition = projectile.group.position.clone();
      const distance = projectile.start.distanceTo(projectile.end);
      projectile.progress = Math.min(1, projectile.progress + PROJECTILE_SPEED * delta / Math.max(0.001, distance));
      const newPosition = projectile.start.clone().lerp(projectile.end, projectile.progress);
      projectile.group.position.copy(newPosition);
      const trailPositions = projectile.trail.geometry.getAttribute('position') as THREE.BufferAttribute;
      const travelDirection = projectile.end.clone().sub(projectile.start).normalize();
      const travelledDistance = distance * projectile.progress;
      const trailLength = Math.min(
        travelledDistance,
        projectile.payload.reaction ? 3.8 : projectile.payload.baseElement ? 3.1 : 2.4,
      );
      for (let sample = 0; sample < trailPositions.count; sample += 1) {
        const fadeDistance = trailLength * sample / Math.max(1, trailPositions.count - 1);
        trailPositions.setXYZ(
          sample,
          newPosition.x - travelDirection.x * fadeDistance,
          newPosition.y - travelDirection.y * fadeDistance,
          newPosition.z - travelDirection.z * fadeDistance,
        );
      }
      trailPositions.needsUpdate = true;
      for (const enemy of [...this.enemies].sort((a, b) => a.id - b.id)) {
        if (enemy.dead || projectile.hitEnemyIds.has(enemy.id)) continue;
        const definition = ENEMY_DEFINITIONS[enemy.kind];
        const center = enemy.group.position.clone().add(new THREE.Vector3(0, definition.radius * 0.85, 0));
        const entry = segmentSphereEntry(oldPosition, newPosition, center, definition.radius + PROJECTILE_RADIUS);
        if (entry === null) continue;
        const hitPoint = oldPosition.clone().lerp(newPosition, entry);
        projectile.hitEnemyIds.add(enemy.id);
        projectile.payload.directHitEnemyIds.add(enemy.id);
        this.resolveDirectHit(projectile.payload, enemy, hitPoint);
      }
      if (projectile.progress >= 1) {
        const target = this.nodes.get(projectile.targetNodeId);
        if (target) this.receivePayload(target, projectile.payload);
        this.removeProjectile(index);
      }
    }
  }

  private receivePayload(target: NodeState, payload: Payload): void {
    if (target.type === 'nexus') {
      const { gained, actual } = this.creditRainCharge(payload.directHitEnemyIds.size);
      if (gained > actual) this.spawnBurst(this.nodeAnchor(target), 0xff7698, 1.1);
      else this.spawnBurst(this.nodeAnchor(target), NODE_DEFINITIONS.nexus.color, 0.7 + gained * 0.06);
      return;
    }
    target.reservedIncoming = Math.max(0, target.reservedIncoming - 1);
    if (NODE_DEFINITIONS[target.type].element) this.transformPayload(target, payload);
    if (target.type === 'support') target.charge = Math.min(target.branch === 'buff' || target.branch === 'debuff' ? 8 : 6, target.charge + 1);
    if (target.type === 'special') {
      target.pulseCharge += 1;
      const threshold = target.branch === 'rapidPulse' ? 3 : target.branch === 'impactPulse' ? 7 : 5;
      if (target.pulseCharge >= threshold) { target.pulseCharge = 0; this.triggerSpecialPulse(target); }
    }
    target.buffer.push(payload);
  }

  private creditRainCharge(enemyHitCount: number): { gained: number; actual: number } {
    const gained = Math.max(0, enemyHitCount) * ACTIVE_STAGE.rainChargeMultiplier;
    const actual = Math.min(gained, MAX_SOUL - this.soul);
    this.soul += actual;
    return { gained, actual };
  }

  private transformPayload(node: NodeState, payload: Payload): void {
    const element = NODE_DEFINITIONS[node.type].element;
    if (!element) return;
    const potency = node.branch === 'resonance' ? 1.35 : node.branch === 'conduit' ? 0.8 : 1;
    if (payload.reaction) {
      payload.reaction = null;
      payload.reactionProcAvailable = false;
      payload.magicDamage = 5 * potency;
    } else if (payload.baseElement) {
      const reaction = resolveReaction(payload.baseElement, element);
      if (reaction) {
        payload.reaction = reaction;
        payload.reactionProcAvailable = true;
        payload.magicDamage = REACTIONS[reaction].magicDamage * potency;
        payload.reactionPotency = potency;
        this.audio.infuse(2);
        this.spawnReactionSeal(this.nodeAnchor(node), reaction);
      } else {
        payload.reaction = null;
        payload.reactionProcAvailable = false;
        payload.magicDamage = 5 * potency;
        this.audio.infuse(1);
      }
    } else {
      payload.magicDamage = 5 * potency;
      this.audio.infuse(1);
    }
    payload.baseElement = element;
  }

  private resolveDirectHit(payload: Payload, enemy: EnemyState, hitPoint: THREE.Vector3): void {
    const definition = ENEMY_DEFINITIONS[enemy.kind];
    const debuff = this.supportDebuffAt(enemy.group.position);
    const armor = Math.max(0, definition.armor - enemy.armorBreak - debuff);
    const mr = Math.max(0, definition.mr - debuff);
    const elementMultiplier = !payload.baseElement ? 1
      : definition.immune?.includes(payload.baseElement) ? 0
        : definition.vulnerable?.includes(payload.baseElement) ? 1.35
          : definition.resist?.includes(payload.baseElement) ? 0.55 : 1;
    const physical = payload.physicalDamage * 100 / (100 + armor);
    const magic = payload.magicDamage * 100 / (100 + mr);
    const barrierMultiplier = enemy.reactionBarrier && !enemy.barrierBroken ? definition.barrierDamageMultiplier ?? 0.18 : 1;
    this.damageEnemy(enemy, (physical + magic) * elementMultiplier * barrierMultiplier, hitPoint, payload.baseElement ? ELEMENT_COLORS[payload.baseElement] : 0xe9dca5);
    this.directHits += 1;
    if (definition.layer === 1) this.layerOneEnemyHits += 1;
    if (payload.baseElement) this.applyBaseStatus(payload.baseElement, enemy);
    if (payload.reaction && payload.reactionProcAvailable) {
      payload.reactionProcAvailable = false;
      this.procReaction(payload.reaction, payload.reactionPotency, enemy, hitPoint);
    }
  }

  private applyBaseStatus(element: Element, enemy: EnemyState): void {
    if (element === 'fire') { enemy.burnDps = Math.max(enemy.burnDps, 2); enemy.burnTime = Math.max(enemy.burnTime, 3); }
    else if (element === 'ice') { enemy.slow = Math.max(enemy.slow, 0.25); enemy.slowTime = Math.max(enemy.slowTime, 2.5); }
    else if (element === 'wind') enemy.windTime = Math.max(enemy.windTime, 1.8);
    else { enemy.armorBreak = Math.max(enemy.armorBreak, 6); enemy.armorBreakTime = Math.max(enemy.armorBreakTime, 3); }
  }

  private procReaction(reaction: ReactionKey, potency: number, target: EnemyState, position: THREE.Vector3): boolean {
    if (target.reactionCooldowns[reaction] > 0) {
      this.blockedReactionProcs += 1;
      return false;
    }
    target.reactionCooldowns[reaction] = REACTION_REPEAT_COOLDOWN;
    this.reactionProcs += 1;
    const firstReaction = !this.tutorialReactionSeen;
    this.tutorialReactionSeen = true;
    if (firstReaction) {
      this.pendingReaction = reaction;
      this.reactionTutorialDelay = 0.75;
    }
    this.audio.reaction();
    this.spawnReactionSeal(position, reaction, true);
    if (target.reactionBarrier === reaction && !target.barrierBroken) {
      target.barrierBroken = true;
      const ward = target.group.getObjectByName('ward');
      if (ward) ward.visible = false;
      this.spawnPulse(target.group.position.clone(), ENEMY_DEFINITIONS[target.kind].radius * 1.8, REACTIONS[reaction].color);
    }
    this.damageEnemy(target, target.maxHp * REACTION_MAX_HP_DAMAGE_RATIO * potency, position, REACTIONS[reaction].color);
    if (reaction === 'hellfire') { target.burnDps = Math.max(target.burnDps, 4 * potency); target.burnTime = Math.max(target.burnTime, 4); }
    else if (reaction === 'deepFreeze') target.freezeTime = Math.max(target.freezeTime, (target.kind === 'boss' ? 0.4 : 0.8) * potency);
    else if (reaction === 'tempest') target.progress = Math.max(0, target.progress - (target.kind === 'boss' ? 0.35 : 0.8) * potency);
    else if (reaction === 'shatter') { target.armorBreak = Math.max(target.armorBreak, 18 * potency); target.armorBreakTime = Math.max(target.armorBreakTime, 4); }
    else if (reaction === 'firestorm') this.enemiesInRadius(position, 2).forEach((enemy) => { enemy.burnDps = Math.max(enemy.burnDps, 2.5 * potency); enemy.burnTime = Math.max(enemy.burnTime, 3); });
    else if (reaction === 'sandstorm') this.enemiesInRadius(position, 2.5).forEach((enemy) => { enemy.armorBreak = Math.max(enemy.armorBreak, 10 * potency); enemy.armorBreakTime = Math.max(enemy.armorBreakTime, 4); });
    else if (reaction === 'permafrost') this.enemiesInRadius(position, 2.5).forEach((enemy) => { enemy.slow = Math.max(enemy.slow, 0.35 * potency); enemy.slowTime = Math.max(enemy.slowTime, 4); });
    else this.enemiesInRadius(position, 2).forEach((enemy) => this.damageMagic(enemy, 10 * potency, position, REACTIONS.steamBurst.color));
    return true;
  }

  private elementGlyph(element: Element): string {
    return ({ fire: '◆', ice: '✦', wind: '➤', earth: '⬟' } as const)[element];
  }

  private updateReactionTutorial(delta: number): void {
    if (this.reactionTutorialDelay < 0 || this.reactionTutorialVisible || !this.pendingReaction) return;
    this.reactionTutorialDelay -= delta;
    if (this.reactionTutorialDelay > 0 || this.phase !== 'wave') return;
    const reaction = REACTIONS[this.pendingReaction];
    const [a, b] = reaction.elements;
    this.reactionTutorialTitle.textContent = reaction.name;
    this.reactionFormulaA.textContent = this.elementGlyph(a); this.reactionFormulaA.dataset.element = a;
    this.reactionFormulaB.textContent = this.elementGlyph(b); this.reactionFormulaB.dataset.element = b;
    this.reactionFormulaResult.textContent = reaction.icon;
    this.reactionTutorialVisible = true; this.reactionTutorialDelay = -1;
    this.reactionTutorial.classList.remove('hidden'); this.reactionTutorial.setAttribute('aria-hidden', 'false');
    this.phase = 'paused'; this.audio.setPaused(true); this.updateUi(true);
  }

  private dismissReactionTutorial(): void {
    if (!this.reactionTutorialVisible) return;
    this.tutorialReactionSeen = true;
    acknowledgeReactionTutorial();
    this.reactionTutorialVisible = false; this.pendingReaction = null;
    this.reactionTutorial.classList.add('hidden'); this.reactionTutorial.setAttribute('aria-hidden', 'true');
    if (this.tutorialStep === 11) this.tutorialStep = TUTORIAL_COMPLETE_STEP;
    if (this.phase === 'paused') this.phase = 'wave';
    this.audio.setPaused(false); this.updateUi(true);
  }

  private updateEnemies(delta: number): void {
    for (const enemy of [...this.enemies]) {
      if (enemy.dead) continue;
      if (enemy.burnTime > 0) {
        enemy.burnTime = Math.max(0, enemy.burnTime - delta);
        if (enemy.burnDps > 0) this.damageEnemy(enemy, enemy.burnDps * delta, enemy.group.position, 0xff684b, false);
      } else enemy.burnDps = 0;
      enemy.slowTime = Math.max(0, enemy.slowTime - delta); if (enemy.slowTime <= 0) enemy.slow = 0;
      enemy.freezeTime = Math.max(0, enemy.freezeTime - delta);
      enemy.windTime = Math.max(0, enemy.windTime - delta);
      for (const reaction of REACTION_KEYS) enemy.reactionCooldowns[reaction] = Math.max(0, enemy.reactionCooldowns[reaction] - delta);
      enemy.armorBreakTime = Math.max(0, enemy.armorBreakTime - delta); if (enemy.armorBreakTime <= 0) enemy.armorBreak = 0;
      enemy.hitFlash = Math.max(0, enemy.hitFlash - delta * 5);
      if (enemy.dead || enemy.freezeTime > 0) continue;
      const definition = ENEMY_DEFINITIONS[enemy.kind];
      const slow = enemy.slow;
      const bossScale = definition.boss ? 0.5 : 1;
      const speed = enemy.barrierBroken && definition.speedAfterBarrierBreak ? definition.speedAfterBarrierBreak : definition.speed;
      enemy.progress += speed * ENEMY_SPEED_MULTIPLIER * (1 - slow * bossScale) * delta;
      const transform = this.pathTransform(enemy.progress, enemy.sideOffset, definition.layer);
      enemy.group.position.copy(transform.position);
      enemy.group.rotation.y = transform.rotation;
      if (enemy.progress >= this.pathLength()) this.leakEnemy(enemy);
    }
  }

  private activeEnemyElements(enemy: EnemyState): Element[] {
    const elements: Element[] = [];
    if (enemy.burnTime > 0) elements.push('fire');
    if (enemy.slowTime > 0 || enemy.freezeTime > 0) elements.push('ice');
    if (enemy.windTime > 0) elements.push('wind');
    if (enemy.armorBreakTime > 0) elements.push('earth');
    return elements;
  }

  private updateEnemyStatusPresentation(): void {
    const iconCounts = new Map<Element, number>([['fire', 0], ['ice', 0], ['wind', 0], ['earth', 0]]);
    const matrix = new THREE.Matrix4(); const iconRotation = this.camera.quaternion.clone();
    const iconScale = new THREE.Vector3(1.25, 1.25, 1.25);
    const screenRight = new THREE.Vector3(1, 0, 0).applyQuaternion(iconRotation).normalize();
    const white = new THREE.Color(0xffffff); let backdropCount = 0;
    for (const enemy of this.enemies) {
      if (enemy.dead) continue;
      const elements = this.activeEnemyElements(enemy);
      const material = enemy.group.userData.bodyMaterial as THREE.MeshStandardMaterial | undefined;
      if (material) {
        const baseColor = material.userData.baseColor as THREE.Color;
        const baseEmissive = material.userData.baseEmissive as THREE.Color;
        const baseIntensity = material.userData.baseEmissiveIntensity as number;
        material.color.copy(baseColor); material.emissive.copy(baseEmissive); material.emissiveIntensity = baseIntensity;
        if (elements.length > 0) {
          const status = new THREE.Color(0x000000);
          elements.forEach((element) => status.add(new THREE.Color(ELEMENT_COLORS[element])));
          status.multiplyScalar(1 / elements.length);
          const hsl = { h: 0, s: 0, l: 0 }; status.getHSL(hsl);
          status.setHSL(hsl.h, Math.max(0.98, hsl.s), clamp(hsl.l, 0.48, 0.62));
          material.color.lerp(status, elements.length > 1 ? 0.88 : 0.82);
          material.emissive.copy(status); material.emissiveIntensity = baseIntensity + 1.15;
        }
        if (enemy.hitFlash > 0) {
          material.color.lerp(white, enemy.hitFlash * 0.94);
          material.emissive.copy(white); material.emissiveIntensity += 2 * enemy.hitFlash;
        }
      }
      if (elements.length === 0) continue;
      const definition = ENEMY_DEFINITIONS[enemy.kind];
      const top = enemy.group.position.clone().add(new THREE.Vector3(0, definition.radius + 1.12 + Math.sin(this.elapsedTime * 4 + enemy.id) * 0.06, 0));
      elements.forEach((element, index) => {
        const mesh = this.statusIconMeshes.get(element); const iconIndex = iconCounts.get(element) ?? 0;
        if (!mesh || iconIndex >= mesh.instanceMatrix.count || backdropCount >= this.statusIconBackdrop.instanceMatrix.count) return;
        const position = top.clone().addScaledVector(screenRight, (index - (elements.length - 1) / 2) * 0.68);
        matrix.compose(position, iconRotation, iconScale); mesh.setMatrixAt(iconIndex, matrix); this.statusIconBackdrop.setMatrixAt(backdropCount, matrix);
        iconCounts.set(element, iconIndex + 1); backdropCount += 1;
      });
    }
    for (const [element, mesh] of this.statusIconMeshes) {
      mesh.count = iconCounts.get(element) ?? 0; mesh.instanceMatrix.needsUpdate = true;
    }
    this.statusIconBackdrop.count = backdropCount; this.statusIconBackdrop.instanceMatrix.needsUpdate = true;
  }

  private spawnEnemy(kind: EnemyKind, sideOffset: number): void {
    const definition = ENEMY_DEFINITIONS[kind];
    const group = this.art.createEnemy(kind);
    const transform = this.pathTransform(0, sideOffset, definition.layer);
    group.position.copy(transform.position);
    group.rotation.y = transform.rotation;
    const enemy: EnemyState = {
      id: this.nextEnemyId++, kind, group,
      hp: definition.hp * WAVES[this.waveIndex].healthMultiplier,
      maxHp: definition.hp * WAVES[this.waveIndex].healthMultiplier,
      progress: 0, sideOffset, burnDps: 0, burnTime: 0, slow: 0, slowTime: 0,
      freezeTime: 0, windTime: 0, armorBreak: 0, armorBreakTime: 0,
      reactionCooldowns: createReactionCooldowns(),
      reactionBarrier: definition.reactionBarrier ?? null, barrierBroken: false, dead: false, hitFlash: 0,
    };
    group.userData.enemyId = enemy.id;
    this.enemyGroup.add(group);
    this.enemies.push(enemy);
  }

  private damageEnemy(enemy: EnemyState, damage: number, position: THREE.Vector3, color: number, feedback = true): void {
    if (enemy.dead || damage <= 0) return;
    enemy.hp -= damage;
    enemy.hitFlash = 1;
    if (feedback) { this.audio.hit(); this.spawnBurst(position, color, Math.min(0.8, 0.25 + damage * 0.015)); }
    if (enemy.hp <= 0) this.killEnemy(enemy);
  }

  private damageMagic(enemy: EnemyState, magic: number, position: THREE.Vector3, color: number): void {
    this.damageEnemy(enemy, this.effectiveMagicDamage(enemy, magic), position, color);
  }

  private effectiveMagicDamage(enemy: EnemyState, magic: number): number {
    const definition = ENEMY_DEFINITIONS[enemy.kind];
    const debuff = this.supportDebuffAt(enemy.group.position);
    return magic * 100 / (100 + Math.max(0, definition.mr - debuff));
  }

  private killEnemy(enemy: EnemyState): void {
    if (enemy.dead) return;
    enemy.dead = true;
    this.gold += Math.max(1, Math.floor(ENEMY_DEFINITIONS[enemy.kind].reward * ENEMY_REWARD_MULTIPLIER * ACTIVE_STAGE.killRewardMultiplier));
    this.killedEnemies += 1;
    this.audio.destroy();
    this.spawnBurst(enemy.group.position.clone().add(new THREE.Vector3(0, 0.7, 0)), ENEMY_DEFINITIONS[enemy.kind].color, ENEMY_DEFINITIONS[enemy.kind].boss ? 2.4 : 1.2);
    this.removeEnemy(enemy);
  }

  private leakEnemy(enemy: EnemyState): void {
    if (enemy.dead) return;
    enemy.dead = true;
    this.baseHp = Math.max(0, this.baseHp - ENEMY_DEFINITIONS[enemy.kind].leakDamage);
    this.leakedEnemies += 1;
    this.audio.leak();
    if (!this.baseTutorialSeen) {
      this.baseTutorialSeen = true;
      this.baseHighlightTime = 2.2;
    }
    this.baseNexus.userData.hitFlash = 0.42;
    this.spawnBurst(this.baseNexus.position.clone().add(new THREE.Vector3(0, 1.65, 0)), 0xff526d, 1.1);
    this.removeEnemy(enemy);
    if (this.baseHp <= 0) this.endRun(false);
  }

  private removeEnemy(enemy: EnemyState): void {
    const index = this.enemies.indexOf(enemy);
    if (index >= 0) this.enemies.splice(index, 1);
    this.enemyGroup.remove(enemy.group);
    disposeObject(enemy.group);
  }

  private triggerSpecialPulse(node: NodeState): void {
    const radius = node.branch === 'rapidPulse' ? 2.5 : node.branch === 'impactPulse' ? 4 : 3;
    const damage = node.branch === 'rapidPulse' ? 9 : node.branch === 'impactPulse' ? 28 : 14;
    const center = node.group.position.clone();
    this.enemiesInRadius(center, radius).forEach((enemy) => {
      this.damageMagic(enemy, damage, enemy.group.position, NODE_DEFINITIONS.special.color);
      this.spawnBurst(enemy.group.position.clone().add(new THREE.Vector3(0, 0.7, 0)), NODE_DEFINITIONS.special.color, 0.7);
      this.spawnDamageNumber(enemy.group.position, damage, NODE_DEFINITIONS.special.color);
    });
    this.specialPulses += 1;
    this.audio.special();
    this.spawnSkillImpact(center, radius, NODE_DEFINITIONS.special.color);
  }

  private updateSupportCharge(delta: number): void {
    for (const node of this.nodes.values()) {
      if (node.type !== 'support' || node.charge <= 0 || !node.active) continue;
      const radius = node.branch === 'buff' ? 4.5 : 4;
      const hasTarget = node.branch === 'debuff'
        ? this.enemies.some((enemy) => !enemy.dead && enemy.group.position.distanceTo(node.group.position) <= radius)
        : [...this.nodes.values()].some((target) => target.active && target.id !== node.id && (NODE_DEFINITIONS[target.type].element || target.type === 'special') && target.group.position.distanceTo(node.group.position) <= radius);
      if (hasTarget) node.charge = Math.max(0, node.charge - 0.75 * delta);
    }
  }

  private effectiveInterval(node: NodeState): number {
    let interval = nodeInterval(node);
    if (NODE_DEFINITIONS[node.type].element || node.type === 'special') {
      for (const support of this.nodes.values()) {
        if (support.type !== 'support' || support.branch === 'debuff' || support.charge <= 0 || !support.active) continue;
        const radius = support.branch === 'buff' ? 4.5 : 4;
        if (support.group.position.distanceTo(node.group.position) <= radius) interval *= support.branch === 'buff' ? 0.75 : 0.9;
      }
    }
    return Math.max(0.12, interval / TOWER_FIRE_RATE_MULTIPLIER);
  }

  private supportDebuffAt(position: THREE.Vector3): number {
    let strongest = 0;
    for (const support of this.nodes.values()) {
      if (support.type === 'support' && support.branch === 'debuff' && support.charge > 0 && support.active && support.group.position.distanceTo(position) <= 4) strongest = Math.max(strongest, 8);
    }
    return strongest;
  }

  private beginSoulTargeting(): void {
    if (this.phase !== 'wave' || this.soul < MAX_SOUL) return;
    this.soulTargeting = true;
    if (ACTIVE_STAGE.tutorial && this.waveIndex >= 3 && this.soulSkillTutorial === 'button') this.soulSkillTutorial = 'target';
    this.audio.ui('select');
    this.refreshSelection();
  }

  private tongueProfile(): TongueProfile {
    const nexus = this.nexusNodeId ? this.nodes.get(this.nexusNodeId) : null;
    const branch: TongueBranch = nexus?.branch === 'suppression' ? 'suppression' : nexus?.branch === 'conduction' ? 'conduction' : 'base';
    if (branch === 'suppression') return { branch, radius: 3.1, flatDamage: 190, maxHpRatio: 0.16, maxHpCap: 450, color: 0xff9c73 };
    if (branch === 'conduction') return { branch, radius: 2.5, flatDamage: 300, maxHpRatio: 0.22, maxHpCap: 700, color: 0xff426f };
    return { branch, radius: 2.7, flatDamage: 220, maxHpRatio: 0.18, maxHpCap: 500, color: 0xff668e };
  }

  private castTongueStrike(point: THREE.Vector3): void {
    if (!this.soulTargeting || this.soul < MAX_SOUL || this.tongueStrike) return;
    const profile = this.tongueProfile();
    const start = this.frogMouthWorldPosition();
    const target = point.clone().setY(0.62);
    const visual = this.createTongueStrikeVisual();
    this.effectGroup.add(visual.group);
    this.tongueStrike = {
      ...visual, start, target, profile, outbound: 0.16, hold: 0.08, retract: 0.28,
      captured: [], elapsed: 0, impacted: false,
    };
    this.positionTongueVisual(this.tongueStrike, 0);
    this.soul = 0;
    this.soulTargeting = false;
    if (ACTIVE_STAGE.tutorial && this.waveIndex >= 3) this.soulSkillTutorial = 'complete';
    this.soulCasts += 1;
    this.audio.special();
    this.updateUi(true);
  }

  private createTongueStrikeVisual(): Pick<TongueStrikeState, 'group' | 'root' | 'core' | 'tip'> {
    const group = new THREE.Group(); group.name = 'frogTongueStrike';
    const bodyMaterial = new THREE.MeshStandardMaterial({
      color: 0xff7898, roughness: 0.48, metalness: 0,
    });
    const root = new THREE.Mesh(new THREE.SphereGeometry(TONGUE_ROOT_RADIUS, 16, 10), bodyMaterial);
    root.name = 'tongueRoot'; root.castShadow = true; root.renderOrder = 34;
    const core = new THREE.Mesh(
      new THREE.CylinderGeometry(TONGUE_BODY_TIP_RADIUS, TONGUE_BODY_ROOT_RADIUS, 1, 16, 3),
      bodyMaterial,
    );
    core.name = 'tongueCore'; core.castShadow = true; core.renderOrder = 34;
    const tip = new THREE.Mesh(
      new THREE.SphereGeometry(TONGUE_TIP_RADIUS, 20, 14),
      new THREE.MeshStandardMaterial({
        color: 0xff9eb2, roughness: 0.42, metalness: 0,
      }),
    );
    tip.name = 'tongueTip'; tip.castShadow = true; tip.renderOrder = 35;
    const tipHighlight = new THREE.Mesh(
      new THREE.SphereGeometry(0.13, 10, 7),
      new THREE.MeshStandardMaterial({ color: 0xffdce6, roughness: 0.34, metalness: 0 }),
    );
    tipHighlight.name = 'tongueTipHighlight'; tipHighlight.position.set(-0.2, 0.22, 0.28); tip.add(tipHighlight);
    group.add(root, core, tip);
    return { group, root, core, tip };
  }

  private updateTongueStrike(delta: number): void {
    this.cameraShakeRemaining = Math.max(0, this.cameraShakeRemaining - delta);
    const strike = this.tongueStrike;
    if (!strike) return;
    strike.elapsed += delta;
    const retractStart = strike.outbound + strike.hold;
    const total = retractStart + strike.retract;
    const reach = strike.elapsed < strike.outbound
      ? 1 - Math.pow(1 - clamp(strike.elapsed / strike.outbound, 0, 1), 3)
      : strike.elapsed < retractStart ? 1
        : 1 - Math.pow(clamp((strike.elapsed - retractStart) / strike.retract, 0, 1), 2);
    this.positionTongueVisual(strike, reach);
    if (!strike.impacted && strike.elapsed >= strike.outbound) {
      strike.impacted = true;
      this.resolveTongueImpact(strike);
    }
    const tipPosition = strike.start.clone().lerp(strike.target, reach);
    strike.captured.forEach((captured, index) => {
      captured.group.position.copy(tipPosition).add(captured.offset);
      captured.group.rotation.y += delta * (5 + index * 0.7);
      captured.group.scale.copy(captured.originalScale).multiplyScalar(clamp(0.2 + reach * (TONGUE_CAPTURE_SCALE - 0.2), 0.2, TONGUE_CAPTURE_SCALE));
    });
    const mouth = this.frogActor.getObjectByName('frogMouthCavity');
    if (mouth) mouth.scale.y = Number(mouth.userData.baseScaleY ?? 0.16) * (1 + Math.sin(clamp(strike.elapsed / total, 0, 1) * Math.PI) * 4.6);
    if (strike.elapsed >= total) this.clearTongueStrike();
  }

  private positionTongueVisual(strike: TongueStrikeState, reach: number): void {
    const end = strike.start.clone().lerp(strike.target, reach);
    const delta = end.clone().sub(strike.start);
    const length = Math.max(0.001, delta.length());
    const midpoint = strike.start.clone().lerp(end, 0.5);
    const orientation = new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), delta.normalize());
    strike.root.position.copy(strike.start); strike.root.scale.setScalar(0.72 + reach * 0.28);
    strike.core.position.copy(midpoint); strike.core.quaternion.copy(orientation); strike.core.scale.set(1, length, 1);
    strike.tip.position.copy(end); strike.tip.scale.setScalar(0.42 + 0.58 * reach);
  }

  private resolveTongueImpact(strike: TongueStrikeState): void {
    const living = this.enemies.filter((enemy) => !enemy.dead);
    const targets = living.filter((enemy) => distanceToSegmentXZ(enemy.group.position, strike.target, strike.target) <= strike.profile.radius);
    const targetIds = new Set(targets.map((enemy) => enemy.id));
    living.filter((enemy) => !targetIds.has(enemy.id)
      && distanceToSegmentXZ(enemy.group.position, strike.start, strike.target) <= 0.46 + ENEMY_DEFINITIONS[enemy.kind].radius * 0.45)
      .forEach((enemy) => {
        const damage = this.effectiveMagicDamage(enemy, this.tongueRawDamage(enemy, strike.profile) * 0.2);
        this.damageEnemy(enemy, damage, enemy.group.position, strike.profile.color);
        this.spawnDamageNumber(enemy.group.position, damage, strike.profile.color);
        this.tongueCorridorHits += 1;
      });
    targets.forEach((enemy, index) => this.damageTongueTarget(enemy, strike, index));
    this.tongueImpactHits += targets.length;
    this.triggerCameraShake(0.18, 0.22);
    this.spawnTongueImpact(strike.target, strike.profile.radius, strike.profile.color);
  }

  private tongueRawDamage(enemy: EnemyState, profile: TongueProfile): number {
    return profile.flatDamage + Math.min(enemy.maxHp * profile.maxHpRatio, profile.maxHpCap);
  }

  private damageTongueTarget(enemy: EnemyState, strike: TongueStrikeState, index: number): void {
    const damage = this.effectiveMagicDamage(enemy, this.tongueRawDamage(enemy, strike.profile));
    const position = enemy.group.position.clone();
    this.spawnDamageNumber(position, damage, strike.profile.color);
    if (damage < enemy.hp) { this.damageEnemy(enemy, damage, position, strike.profile.color); return; }
    enemy.hp = 0; enemy.dead = true;
    this.gold += Math.max(1, Math.floor(ENEMY_DEFINITIONS[enemy.kind].reward * ENEMY_REWARD_MULTIPLIER * ACTIVE_STAGE.killRewardMultiplier));
    this.killedEnemies += 1; this.tongueCapturedKills += 1;
    this.audio.destroy();
    this.spawnBurst(position.clone().add(new THREE.Vector3(0, 0.7, 0)), strike.profile.color, ENEMY_DEFINITIONS[enemy.kind].boss ? 2.4 : 1.2);
    const enemyIndex = this.enemies.indexOf(enemy); if (enemyIndex >= 0) this.enemies.splice(enemyIndex, 1);
    this.effectGroup.attach(enemy.group);
    const captureAngle = index * 2.399963229728653;
    strike.captured.push({
      group: enemy.group,
      offset: new THREE.Vector3(
        Math.cos(captureAngle) * 0.72,
        0.18 + (index % 2) * 0.24,
        Math.sin(captureAngle) * 0.72,
      ),
      originalScale: enemy.group.scale.clone(),
    });
  }

  private finishWave(): void {
    if (this.phase !== 'wave') return;
    // Keep the tutorial wave alive long enough for the delayed first-reaction lesson to appear.
    // This matters when the reaction itself defeats the final enemy in the wave.
    if (this.pendingReaction && this.reactionTutorialDelay >= 0 && !this.reactionTutorialVisible) return;
    this.cancelSoulSkillDrag(true);
    this.clearTongueStrike();
    const reward = Math.max(0, Math.round(WAVES[this.waveIndex].clearReward * WAVE_CLEAR_REWARD_MULTIPLIER));
    this.gold += reward;
    this.waveIndex += 1;
    this.audio.waveClear();
    if (this.waveIndex >= WAVES.length) { this.endRun(true); return; }
    this.phase = 'preparation';
    if (ACTIVE_STAGE.tutorial && this.waveIndex === 3) {
      this.tutorialStep = TUTORIAL_COMPLETE_STEP;
      this.baseHp = STARTING_BASE_HP;
      this.gold = MASTERY_CHECKPOINT_GOLD;
      this.soul = MAX_SOUL;
      this.soulSkillTutorial = 'button';
      this.selectedBuildType = null;
      this.selectedNodeId = null;
      this.clearLinkDrag();
      this.captureMasteryCheckpoint();
    } else if (this.tutorialStep === 3) this.tutorialStep = 4;
    else if (this.tutorialStep === 7) this.tutorialStep = 8;
    this.validateNetwork();
    this.renderWaveRoster();
    this.refreshSelection();
    this.updateUi(true);
  }

  private captureMasteryCheckpoint(): void {
    if (this.waveIndex !== 3) return;
    this.masteryCheckpoint = {
      gold: this.gold,
      soul: this.soul,
      nodes: [...this.nodes.values()].map((node) => ({
        id: node.id,
        type: node.type,
        slotId: node.slotId ?? -1,
        outputTargetId: node.outputTargetId,
        totalInvested: node.totalInvested,
        branch: node.branch,
        buffer: node.buffer.map((payload) => ({
          id: payload.id,
          physicalDamage: payload.physicalDamage,
          magicDamage: payload.magicDamage,
          baseElement: payload.baseElement,
          reaction: payload.reaction,
          reactionProcAvailable: payload.reactionProcAvailable,
          reactionPotency: payload.reactionPotency,
          directHitEnemyIds: [...payload.directHitEnemyIds],
        })),
        reservedIncoming: node.reservedIncoming,
        timer: node.timer,
        charge: node.charge,
        pulseCharge: node.pulseCharge,
      })),
      nextNodeId: this.nextNodeId,
      nextPayloadId: this.nextPayloadId,
      nextProjectileId: this.nextProjectileId,
      nextEnemyId: this.nextEnemyId,
      currencyTutorialSeen: this.currencyTutorialSeen,
      baseTutorialSeen: this.baseTutorialSeen,
    };
  }

  private restoreMasteryCheckpoint(): void {
    const checkpoint = this.masteryCheckpoint;
    if (!checkpoint) { this.resetRun(); return; }
    this.resetRun();
    this.masteryCheckpoint = checkpoint;
    this.tutorialStep = TUTORIAL_COMPLETE_STEP;
    this.waveIndex = 3;
    this.gold = Number.MAX_SAFE_INTEGER;
    this.currencyTutorialSeen = true;
    const restoredIds = new Map<number, number>();
    for (const snapshot of checkpoint.nodes) {
      if (snapshot.slotId < 0) continue;
      const before = this.nodes.size;
      this.selectedBuildType = snapshot.type;
      if (!this.tryPlaceSelected(snapshot.slotId) || this.nodes.size === before) continue;
      const restored = this.nodes.get(this.selectedNodeId ?? -1);
      if (!restored) continue;
      restoredIds.set(snapshot.id, restored.id);
      restored.totalInvested = snapshot.totalInvested;
      restored.branch = snapshot.branch;
      restored.buffer = snapshot.buffer.map((payload) => ({
        ...payload,
        directHitEnemyIds: new Set(payload.directHitEnemyIds),
      }));
      restored.reservedIncoming = snapshot.reservedIncoming;
      restored.timer = snapshot.timer;
      restored.charge = snapshot.charge;
      restored.pulseCharge = snapshot.pulseCharge;
    }
    checkpoint.nodes.forEach((snapshot) => {
      const sourceId = restoredIds.get(snapshot.id);
      const targetId = snapshot.outputTargetId === null ? null : restoredIds.get(snapshot.outputTargetId) ?? null;
      if (sourceId !== undefined && targetId !== null) this.connectNodes(sourceId, targetId);
    });
    this.nextNodeId = checkpoint.nextNodeId;
    this.nextPayloadId = checkpoint.nextPayloadId;
    this.nextProjectileId = checkpoint.nextProjectileId;
    this.nextEnemyId = checkpoint.nextEnemyId;
    this.gold = checkpoint.gold;
    this.soul = checkpoint.soul;
    this.soulSkillTutorial = this.soul >= MAX_SOUL ? 'button' : 'complete';
    this.baseHp = STARTING_BASE_HP;
    this.currencyTutorialSeen = checkpoint.currencyTutorialSeen;
    this.baseTutorialSeen = checkpoint.baseTutorialSeen;
    this.currencyHighlightTime = 0;
    this.baseHighlightTime = 0;
    this.waveClock = 0;
    this.spawnIndex = 0;
    this.accumulator = 0;
    this.phase = 'preparation';
    this.selectedBuildType = null;
    this.selectedNodeId = null;
    this.resultOverlay.classList.add('hidden');
    this.validateNetwork();
    this.refreshLinks();
    this.refreshSelection();
    this.renderWaveRoster();
    this.updateUi(true);
  }

  private endRun(won: boolean): void {
    if (won) {
      this.beginVictoryTravel();
      return;
    }
    this.showRunResult(false);
  }

  private showRunResult(won: boolean): void {
    this.phase = won ? 'won' : 'lost';
    this.audio.setPaused(false);
    if (won) {
      if (!this.victoryTravel) this.audio.win();
    } else this.audio.lose();
    this.resultKicker.textContent = won ? 'ĐƯỜNG LÊN TRỜI THÔNG SUỐT' : 'CÓC ĐÃ KIỆT SỨC';
    this.resultTitle.textContent = won ? 'Cóc vẫn vững chân' : 'Đường kiện trời thất thủ';
    this.resultCopy.textContent = won
      ? `${WAVES.length} đợt tại ${ACTIVE_STAGE.title} đã bị đẩy lùi.`
      : 'Hãy dựng lại đường đạn và thử lần nữa.';
    this.resultRestart.textContent = won && ACTIVE_STAGE_INDEX < 2
      ? `Tới Màn ${ACTIVE_STAGE_INDEX + 2}`
      : !won && this.masteryCheckpoint && this.isTutorialMasteryPhase()
        ? 'Thử lại 3 đợt'
        : 'Chơi lại màn';
    this.resultOverlay.classList.remove('hidden');
    this.updateUi(true);
  }

  private beginVictoryTravel(): void {
    if (this.victoryTravel || this.phase === 'victoryTravel') return;
    this.cancelBuildDrag();
    this.clearLinkDrag();
    this.cancelSoulSkillDrag(false);
    this.selectedBuildType = null;
    this.selectedNodeId = null;
    this.resultOverlay.classList.add('hidden');
    document.body.classList.remove('level-transitioning');

    this.baseNexus.updateMatrixWorld(true);
    const actorWorld = this.frogActor.getWorldPosition(new THREE.Vector3());
    this.worldGroup.attach(this.frogActor);
    this.frogActor.position.copy(actorWorld).setY(0.02);
    this.frogActor.rotation.set(0, 0, 0);
    this.frogActor.scale.setScalar(1);
    const shadow = this.frogActor.getObjectByName('frogTravelShadow');
    if (shadow) shadow.visible = true;

    const route: THREE.Vector3[] = [this.frogActor.position.clone()];
    const [endX, endZ] = ENEMY_PATH[ENEMY_PATH.length - 1];
    const pathEnd = new THREE.Vector3(endX, 0.02, endZ);
    if (route[0].distanceTo(pathEnd) > 0.05) route.push(pathEnd);
    for (let index = ENEMY_PATH.length - 2; index >= 0; index -= 1) {
      const [x, z] = ENEMY_PATH[index];
      route.push(new THREE.Vector3(x, 0.02, z));
    }
    const segmentLengths = route.slice(1).map((point, index) => point.distanceTo(route[index]));
    const totalLength = segmentLengths.reduce((sum, length) => sum + length, 0);
    this.victoryTravel = {
      actor: this.frogActor,
      route,
      segmentLengths,
      totalLength,
      distance: 0,
      hopHeight: 0,
      maxHopHeight: 0,
      landingIndex: 0,
      fadeRemaining: null,
      navigationStarted: false,
    };
    this.phase = 'victoryTravel';
    this.audio.setPaused(false);
    this.audio.win();
    this.refreshSelection();
    this.updateUi(true);
  }

  private updateVictoryTravel(delta: number): void {
    const travel = this.victoryTravel;
    if (!travel || travel.navigationStarted) return;
    if (travel.fadeRemaining !== null) {
      travel.fadeRemaining = Math.max(0, travel.fadeRemaining - delta);
      if (travel.fadeRemaining <= 0) {
        travel.navigationStarted = true;
        const nextStage = ACTIVE_STAGE_INDEX + 1;
        if (nextStage < STAGES.length) {
          if (this.suppressVictoryNavigation) {
            document.body.classList.remove('level-transitioning');
            this.showRunResult(true);
            return;
          }
          const url = new URL(window.location.href);
          url.searchParams.set('level', String(nextStage + 1));
          window.location.assign(url.toString());
        }
      }
      return;
    }

    travel.distance = Math.min(travel.totalLength, travel.distance + VICTORY_TRAVEL_SPEED * delta);
    const { position, direction } = this.sampleVictoryRoute(travel, travel.distance);
    const hopPhase = travel.totalLength <= 0 ? 0 : (travel.distance % VICTORY_HOP_LENGTH) / VICTORY_HOP_LENGTH;
    travel.hopHeight = Math.sin(hopPhase * Math.PI) * (this.reducedMotion ? 0.5 : 1.18);
    travel.maxHopHeight = Math.max(travel.maxHopHeight, travel.hopHeight);
    travel.actor.position.copy(position).add(new THREE.Vector3(0, travel.hopHeight, 0));
    travel.actor.rotation.y = Math.atan2(direction.z, -direction.x);
    const stretch = Math.sin(hopPhase * Math.PI);
    const scaleY = 0.9 + stretch * 0.2;
    const scaleXZ = 1 / Math.sqrt(scaleY);
    travel.actor.scale.set(scaleXZ, scaleY, scaleXZ);
    const shadow = travel.actor.getObjectByName('frogTravelShadow');
    if (shadow) shadow.position.y = -travel.hopHeight + 0.025;

    const landingIndex = Math.floor(travel.distance / VICTORY_HOP_LENGTH);
    if (landingIndex > travel.landingIndex && travel.distance < travel.totalLength - 0.1) {
      travel.landingIndex = landingIndex;
      this.spawnBurst(position.clone().setY(0.24), 0x9a5d34, this.reducedMotion ? 0.45 : 0.72);
    }

    if (travel.distance < travel.totalLength) return;
    travel.actor.position.copy(travel.route[travel.route.length - 1]);
    travel.actor.scale.setScalar(1);
    if (shadow) shadow.position.y = 0.025;
    if (ACTIVE_STAGE_INDEX >= STAGES.length - 1) {
      travel.navigationStarted = true;
      this.showRunResult(true);
      return;
    }
    travel.fadeRemaining = VICTORY_FADE_DURATION;
    document.body.classList.add('level-transitioning');
    this.updateUi(true);
  }

  private sampleVictoryRoute(travel: VictoryTravelState, distance: number): { position: THREE.Vector3; direction: THREE.Vector3 } {
    let remaining = distance;
    for (let index = 0; index < travel.segmentLengths.length; index += 1) {
      const length = Math.max(0.001, travel.segmentLengths[index]);
      if (remaining <= length || index === travel.segmentLengths.length - 1) {
        const start = travel.route[index];
        const end = travel.route[index + 1];
        return {
          position: start.clone().lerp(end, clamp(remaining / length, 0, 1)),
          direction: end.clone().sub(start).normalize(),
        };
      }
      remaining -= length;
    }
    const last = travel.route[travel.route.length - 1];
    const previous = travel.route[Math.max(0, travel.route.length - 2)];
    return { position: last.clone(), direction: last.clone().sub(previous).normalize() };
  }

  private restoreFrogNexus(): void {
    document.body.classList.remove('level-transitioning');
    if (this.frogActor.parent !== this.baseNexus) {
      this.frogActor.removeFromParent();
      this.baseNexus.add(this.frogActor);
    }
    this.frogActor.position.set(0, 0, 0);
    this.frogActor.rotation.set(0, 0, 0);
    this.frogActor.scale.setScalar(1);
    const shadow = this.frogActor.getObjectByName('frogTravelShadow');
    if (shadow) { shadow.visible = false; shadow.position.y = 0.025; }
    this.victoryTravel = null;
  }

  private resetRun(): void {
    this.restoreFrogNexus();
    this.suppressVictoryNavigation = false;
    this.cancelBuildDrag();
    this.clearLinkDrag();
    this.cancelSoulSkillDrag(false);
    this.clearChainCompletionNotice();
    while (this.projectiles.length > 0) this.removeProjectile(this.projectiles.length - 1);
    this.enemies.slice().forEach((enemy) => this.removeEnemy(enemy));
    this.clearTongueStrike();
    this.nodes.forEach((node) => { this.nodeGroup.remove(node.group); disposeObject(node.group); });
    this.nodes.clear();
    this.slots.forEach((slot) => { slot.occupiedNodeId = null; });
    this.links.splice(0).forEach((link) => { this.linkGroup.remove(link.group); disposeObject(link.group); });
    this.selectionGroup.clear();
    this.masteryCheckpoint = null;
    this.stageTwoLessonSlots.clear();
    this.gold = STARTING_GOLD; this.baseHp = STARTING_BASE_HP; this.soul = 0; this.waveIndex = 0;
    this.waveClock = 0; this.spawnIndex = 0; this.phase = 'preparation';
    this.selectedBuildType = null; this.selectedNodeId = null; this.linkSourceId = null; this.linkHoverTargetId = null;
    this.tutorialEndpointRouteComplete = null; this.tutorialEndpointPulseRemaining = 0;
    this.tutorialEndpointPulseTransitions = 0; this.tutorialEndpointPulseDirection = null;
    this.soulTargeting = false; this.soulSkillTutorial = ACTIVE_STAGE.tutorial ? 'button' : 'complete'; this.nexusNodeId = null; this.nextNodeId = 1; this.nextPayloadId = 1;
    this.nextProjectileId = 1; this.nextEnemyId = 1; this.tutorialStep = ACTIVE_STAGE.tutorial ? 0 : TUTORIAL_COMPLETE_STEP;
    this.tutorialReactionSeen = this.tutorialReactionSeen || reactionTutorialAcknowledged();
    this.reactionTutorialDelay = -1; this.reactionTutorialVisible = false; this.pendingReaction = null;
    this.currencyTutorialSeen = false; this.baseTutorialSeen = false; this.currencyHighlightTime = 0; this.baseHighlightTime = 0;
    this.reactionTutorial.classList.add('hidden'); this.reactionTutorial.setAttribute('aria-hidden', 'true'); this.audio.setPaused(false);
    this.directHits = 0; this.layerOneEnemyHits = 0; this.reactionProcs = 0; this.blockedReactionProcs = 0; this.specialPulses = 0;
    this.projectileLaunchesByNode.clear();
    this.soulCasts = 0; this.tongueCorridorHits = 0; this.tongueImpactHits = 0; this.tongueCapturedKills = 0; this.killedEnemies = 0; this.leakedEnemies = 0; this.rng = createSeededRandom(20260816);
    this.cameraShakeRemaining = 0; this.cameraShakeDuration = 0; this.cameraShakeStrength = 0;
    this.screenshotPaused = false; this.frame = 0; this.elapsedTime = 0;
    this.slotMarkerGroup.visible = false;
    this.statusIconBackdrop.count = 0; this.statusIconMeshes.forEach((mesh) => { mesh.count = 0; });
    this.resultOverlay.classList.add('hidden');
    this.baseNexus.userData.hitFlash = 0; this.baseNexus.scale.setScalar(1);
    this.renderWaveRoster();
    this.refreshSelection();
    this.updateUi(true);
  }

  private purchaseBranch(index: 0 | 1): void {
    if (this.phase !== 'preparation' || this.selectedNodeId === null) return;
    const node = this.nodes.get(this.selectedNodeId);
    if (!node || node.branch || !NODE_DEFINITIONS[node.type].branches) return;
    const definition = NODE_DEFINITIONS[node.type];
    if (this.gold < definition.upgradeCost) { this.error('Không đủ Vàng.'); return; }
    const branches = definition.branches;
    if (!branches) return;
    const branch = branches[index];
    this.gold -= definition.upgradeCost;
    node.totalInvested += definition.upgradeCost;
    node.branch = branch;
    this.audio.ui('upgrade');
    this.spawnBurst(this.nodeAnchor(node), definition.color, 1.2);
    this.updateUi(true);
  }

  private focusBranchChoice(): void {
    if (this.phase !== 'preparation') return;
    const node = this.selectedNodeId ? this.nodes.get(this.selectedNodeId) : null;
    if (!node || node.branch || !NODE_DEFINITIONS[node.type].branches) return;
    this.branchControls.animate([{ transform: 'scale(.96)' }, { transform: 'scale(1.04)' }, { transform: 'scale(1)' }], { duration: 220 });
  }

  private sellSelectedNode(): void {
    if (this.phase !== 'preparation' || this.selectedNodeId === null) return;
    const node = this.nodes.get(this.selectedNodeId);
    if (!node || node.type === 'nexus') return;
    const affected = this.connectedComponent(new Set([node.id]));
    this.clearTransport(affected);
    this.unlinkOutput(node.id);
    if (node.inputSourceId !== null) this.unlinkOutput(node.inputSourceId);
    const slot = node.slotId !== null ? this.slots.get(node.slotId) : null;
    if (slot) slot.occupiedNodeId = null;
    this.gold += Math.floor(node.totalInvested * SELL_REFUND);
    this.nodeGroup.remove(node.group);
    disposeObject(node.group);
    this.nodes.delete(node.id);
    this.selectedNodeId = null;
    this.audio.ui('sell');
    this.validateNetwork();
    this.refreshLinks();
    this.refreshSelection();
    this.updateUi(true);
  }

  private connectedComponent(seeds: Set<number>): Set<number> {
    const result = new Set<number>();
    const queue = [...seeds];
    while (queue.length > 0) {
      const id = queue.shift()!;
      if (result.has(id)) continue;
      result.add(id);
      const node = this.nodes.get(id);
      if (!node) continue;
      if (node.outputTargetId !== null) queue.push(node.outputTargetId);
      if (node.inputSourceId !== null) queue.push(node.inputSourceId);
      node.nexusInputSourceIds.forEach((sourceId) => queue.push(sourceId));
    }
    return result;
  }

  private clearTransport(nodeIds: Set<number>): void {
    for (let index = this.projectiles.length - 1; index >= 0; index -= 1) {
      const projectile = this.projectiles[index];
      if (nodeIds.has(projectile.sourceNodeId) || nodeIds.has(projectile.targetNodeId)) this.removeProjectile(index);
    }
    nodeIds.forEach((id) => {
      const node = this.nodes.get(id);
      if (!node) return;
      node.buffer = [];
      node.reservedIncoming = 0;
      node.timer = 0;
    });
  }

  private removeProjectile(index: number): void {
    const projectile = this.projectiles[index];
    if (!projectile) return;
    const target = this.nodes.get(projectile.targetNodeId);
    if (target && target.type !== 'nexus' && projectile.progress < 1) target.reservedIncoming = Math.max(0, target.reservedIncoming - 1);
    this.projectileGroup.remove(projectile.group, projectile.trail);
    disposeObject(projectile.group);
    projectile.trail.geometry.dispose();
    (projectile.trail.material as THREE.Material).dispose();
    this.projectiles.splice(index, 1);
  }

  private clearTongueStrike(): void {
    const strike = this.tongueStrike;
    if (!strike) return;
    strike.captured.forEach((captured) => {
      captured.group.removeFromParent();
      disposeObject(captured.group);
    });
    this.effectGroup.remove(strike.group);
    disposeObject(strike.group);
    const mouth = this.frogActor.getObjectByName('frogMouthCavity');
    if (mouth) mouth.scale.y = Number(mouth.userData.baseScaleY ?? 0.16);
    this.tongueStrike = null;
  }

  private refreshSoulTargetPreview(point: THREE.Vector3 | null): void {
    if (!point) { this.clearSoulTargetPreview(); return; }
    if (!this.soulTargetPreview) {
      const { radius, color } = this.tongueProfile();
      const preview = new THREE.Group();
      const disc = this.createRangeDisc(radius, color, 0.3);
      disc.name = 'tongueImpactPreview';
      const outer = new THREE.Mesh(
        new THREE.TorusGeometry(radius, 0.15, 8, 72),
        new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.98, depthWrite: false, depthTest: false }),
      );
      outer.rotation.x = Math.PI / 2;
      const inner = new THREE.Mesh(
        new THREE.TorusGeometry(radius * 0.58, 0.055, 6, 56),
        new THREE.MeshBasicMaterial({ color: 0xfff4d8, transparent: true, opacity: 0.72, depthWrite: false, depthTest: false }),
      );
      inner.rotation.x = Math.PI / 2;
      const corridor = new THREE.Mesh(
        new THREE.CylinderGeometry(1, 1, 1, 8),
        new THREE.MeshBasicMaterial({ color: 0xff668e, transparent: true, opacity: 0.34, depthWrite: false, depthTest: false }),
      );
      corridor.name = 'tongueCorridorPreview';
      const corridorCore = new THREE.Mesh(
        new THREE.CylinderGeometry(1, 1, 1, 8),
        new THREE.MeshBasicMaterial({ color: 0xffe0b5, transparent: true, opacity: 0.78, depthWrite: false, depthTest: false }),
      );
      corridorCore.name = 'tongueCorridorCorePreview';
      preview.add(disc, outer, inner, corridor, corridorCore);
      preview.userData.corridor = corridor;
      preview.userData.corridorCore = corridorCore;
      preview.renderOrder = 45;
      this.effectGroup.add(preview);
      this.soulTargetPreview = preview;
    }
    this.soulTargetPreview.position.copy(point).setY(0.3);
    this.soulTargetPreview.updateWorldMatrix(true, false);
    const localStart = this.soulTargetPreview.worldToLocal(this.frogMouthWorldPosition());
    const localEnd = new THREE.Vector3(0, 0.2, 0);
    this.setCylinderBetween(this.soulTargetPreview.userData.corridor as THREE.Mesh, localStart, localEnd, 0.34);
    this.setCylinderBetween(this.soulTargetPreview.userData.corridorCore as THREE.Mesh, localStart, localEnd, 0.055);
  }

  private clearSoulTargetPreview(): void {
    if (!this.soulTargetPreview) return;
    this.effectGroup.remove(this.soulTargetPreview);
    disposeObject(this.soulTargetPreview);
    this.soulTargetPreview = null;
  }

  private refreshLinks(): void {
    this.links.splice(0).forEach((link) => { this.linkGroup.remove(link.group); disposeObject(link.group); });
    for (const source of this.nodes.values()) {
      if (source.outputTargetId === null) continue;
      const target = this.nodes.get(source.outputTargetId);
      if (!target) continue;
      const start = this.nodeAnchor(source);
      const end = this.nodeAnchor(target);
      const group = this.createLinkVisual(start, end, source.active);
      this.linkGroup.add(group);
      this.links.push({ sourceId: source.id, targetId: target.id, group });
      this.orientNodeToTarget(source, target);
    }
    this.refreshCompletedLinkVisibility();
  }

  private refreshCompletedLinkVisibility(): void {
    const selectedNetwork = this.selectedNodeId === null
      ? null
      : this.connectedComponent(new Set([this.selectedNodeId]));
    this.links.forEach((link) => {
      link.group.visible = selectedNetwork !== null
        && selectedNetwork.has(link.sourceId)
        && selectedNetwork.has(link.targetId);
    });
  }

  private createLinkVisual(start: THREE.Vector3, end: THREE.Vector3, active: boolean): THREE.Group {
    const group = new THREE.Group();
    const delta = end.clone().sub(start);
    const length = delta.length();
    const midpoint = start.clone().lerp(end, 0.5);
    const material = new THREE.MeshBasicMaterial({
      color: 0xffffff, transparent: true, opacity: active ? 0.42 : 0.18,
      depthWrite: false, toneMapped: false,
    });
    const beam = new THREE.Mesh(new THREE.CylinderGeometry(active ? 0.07 : 0.045, active ? 0.07 : 0.045, length, 6), material);
    beam.name = 'completedLinkWhiteBeam';
    beam.position.copy(midpoint);
    beam.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), delta.clone().normalize());
    group.add(beam);
    const arrow = new THREE.Mesh(new THREE.ConeGeometry(0.19, 0.55, 6), material.clone());
    arrow.name = 'completedLinkWhiteArrow';
    arrow.position.copy(start.clone().lerp(end, 0.62));
    arrow.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), delta.clone().normalize());
    group.add(arrow);
    return group;
  }

  private startChainCompletionNotice(route: readonly NodeState[]): void {
    if (route.length < 2) return;
    this.clearChainCompletionNotice();
    const points = route.map((node) => this.nodeAnchor(node));
    const segmentLengths = points.slice(1).map((point, index) => point.distanceTo(points[index]));
    const totalLength = segmentLengths.reduce((sum, length) => sum + length, 0);
    if (totalLength <= 0.001) return;

    const group = new THREE.Group();
    group.name = 'chainCompletionNotice';
    const ledCount = 9;
    const ledColors = [new THREE.Color(0xffd45b), new THREE.Color(0x72e8ff)];
    const ledCores = new THREE.InstancedMesh(
      new THREE.SphereGeometry(0.145, 8, 6),
      new THREE.MeshBasicMaterial({ color: 0xffffff, toneMapped: false }),
      ledCount,
    );
    ledCores.name = 'chainCompletionLedCores';
    const ledHalos = new THREE.InstancedMesh(
      new THREE.SphereGeometry(0.31, 8, 6),
      new THREE.MeshBasicMaterial({
        color: 0xffffff, transparent: true, opacity: 0.24, depthWrite: false,
        toneMapped: false, blending: THREE.AdditiveBlending,
      }),
      ledCount,
    );
    ledHalos.name = 'chainCompletionLedHalos';
    ledCores.frustumCulled = false;
    ledHalos.frustumCulled = false;
    for (let index = 0; index < ledCount; index += 1) {
      const color = ledColors[index % ledColors.length];
      ledCores.setColorAt(index, color);
      ledHalos.setColorAt(index, color);
    }
    if (ledCores.instanceColor) ledCores.instanceColor.needsUpdate = true;
    if (ledHalos.instanceColor) ledHalos.instanceColor.needsUpdate = true;
    group.add(ledHalos, ledCores);
    this.effectGroup.add(group);

    this.chainCompletionNotice = {
      routeNodeIds: route.map((node) => node.id),
      points, segmentLengths, totalLength, group, ledCores, ledHalos,
      passDuration: clamp(totalLength / 13, 0.8, 1.35), gapDuration: 0.18, elapsed: 0,
    };
    this.updateChainCompletionNotice(0);
  }

  private updateChainCompletionNotice(delta: number): void {
    const notice = this.chainCompletionNotice;
    if (!notice) return;
    notice.elapsed += delta;
    const totalDuration = notice.passDuration * 2 + notice.gapDuration;
    if (notice.elapsed >= totalDuration) {
      this.clearChainCompletionNotice();
      return;
    }
    const secondPassStart = notice.passDuration + notice.gapDuration;
    const progress = notice.elapsed <= notice.passDuration
      ? notice.elapsed / notice.passDuration
      : notice.elapsed < secondPassStart
        ? -1
        : (notice.elapsed - secondPassStart) / notice.passDuration;
    const ledCount = notice.ledCores.count;
    const transform = new THREE.Object3D();
    for (let index = 0; index < ledCount; index += 1) {
      const distance = progress < 0 ? -1 : progress * notice.totalLength - index * 0.36;
      if (distance < 0 || distance > notice.totalLength) {
        transform.position.set(0, -100, 0);
        transform.scale.setScalar(0.001);
      } else {
        transform.position.copy(this.pointAlongRoute(notice, distance));
        transform.position.y += 0.08;
        transform.scale.setScalar(1 - index * 0.055);
      }
      transform.updateMatrix();
      notice.ledCores.setMatrixAt(index, transform.matrix);
      notice.ledHalos.setMatrixAt(index, transform.matrix);
    }
    notice.ledCores.instanceMatrix.needsUpdate = true;
    notice.ledHalos.instanceMatrix.needsUpdate = true;
  }

  private pointAlongRoute(notice: ChainCompletionNotice, distance: number): THREE.Vector3 {
    let remaining = clamp(distance, 0, notice.totalLength);
    for (let index = 0; index < notice.segmentLengths.length; index += 1) {
      const length = notice.segmentLengths[index];
      if (remaining <= length || index === notice.segmentLengths.length - 1) {
        return notice.points[index].clone().lerp(notice.points[index + 1], length <= 0 ? 0 : remaining / length);
      }
      remaining -= length;
    }
    return notice.points[notice.points.length - 1].clone();
  }

  private clearChainCompletionNotice(): void {
    const notice = this.chainCompletionNotice;
    if (!notice) return;
    this.chainCompletionNotice = null;
    this.effectGroup.remove(notice.group);
    disposeObject(notice.group);
  }

  private createDragLinkVisual(start: THREE.Vector3, end: THREE.Vector3, color: number): THREE.Group {
    const group = new THREE.Group();
    const delta = end.clone().sub(start);
    const length = delta.length();
    if (length <= 0.02) return group;
    const direction = delta.clone().normalize();
    const midpoint = start.clone().lerp(end, 0.5);
    const coreMaterial = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.96, depthTest: false, depthWrite: false });
    const glowMaterial = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.28, depthTest: false, depthWrite: false });
    const highlightMaterial = new THREE.MeshBasicMaterial({ color: 0xfff8df, transparent: true, opacity: 0.78, depthTest: false, depthWrite: false });
    const core = new THREE.Mesh(new THREE.CylinderGeometry(0.14, 0.14, length, 8), coreMaterial);
    const glow = new THREE.Mesh(new THREE.CylinderGeometry(0.3, 0.3, length, 10), glowMaterial);
    const highlight = new THREE.Mesh(new THREE.CylinderGeometry(0.045, 0.045, length, 6), highlightMaterial);
    core.position.copy(midpoint); glow.position.copy(midpoint); highlight.position.copy(midpoint);
    core.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), direction);
    glow.quaternion.copy(core.quaternion); highlight.quaternion.copy(core.quaternion);
    core.renderOrder = 32; glow.renderOrder = 31; highlight.renderOrder = 33;
    const arrowMaterial = coreMaterial.clone();
    const arrow = new THREE.Mesh(new THREE.ConeGeometry(0.28, 0.72, 8), arrowMaterial);
    arrow.position.copy(start.clone().lerp(end, 0.72));
    arrow.quaternion.copy(core.quaternion);
    arrow.renderOrder = 34;
    const endpoint = new THREE.Mesh(new THREE.SphereGeometry(0.24, 12, 8), coreMaterial.clone());
    endpoint.position.copy(end); endpoint.renderOrder = 35;
    const endpointGlow = new THREE.Mesh(new THREE.SphereGeometry(0.46, 12, 8), glowMaterial.clone());
    endpointGlow.position.copy(end); endpointGlow.renderOrder = 30;
    group.add(glow, core, highlight, arrow, endpointGlow, endpoint);
    return group;
  }

  private refreshPlacementPreview(type: PurchasableNodeType, slotId: number | null, valid: boolean): void {
    this.clearPlacementPreview();
    const available = [...this.slots.values()].filter((slot) => slot.occupiedNodeId === null);
    const occupied = [...this.slots.values()].filter((slot) => slot.occupiedNodeId !== null);
    const addTiles = (slots: readonly SlotVisual[], color: number, opacity: number): void => {
      if (slots.length === 0) return;
      const mesh = new THREE.InstancedMesh(
        new THREE.PlaneGeometry(BUILD_GRID_SPACING * 0.84, BUILD_GRID_SPACING * 0.84),
        new THREE.MeshBasicMaterial({ color, transparent: true, opacity, side: THREE.DoubleSide, depthWrite: false }),
        slots.length,
      );
      const matrix = new THREE.Matrix4(); const rotation = new THREE.Quaternion().setFromEuler(new THREE.Euler(-Math.PI / 2, 0, 0));
      slots.forEach((slot, index) => {
        matrix.compose(slot.mesh.position.clone().add(new THREE.Vector3(0, 0.08, 0)), rotation, new THREE.Vector3(1, 1, 1));
        mesh.setMatrixAt(index, matrix);
      });
      mesh.instanceMatrix.needsUpdate = true; mesh.renderOrder = 2; this.placementPreviewGroup.add(mesh);
    };
    addTiles(available, 0x67e4bc, 0.2); addTiles(occupied, 0x322d42, 0.16);

    for (const node of this.nodes.values()) {
      const range = node.type === 'nexus' ? 3.5
        : node.type === 'special' ? 3
          : node.type === 'support' ? 4
            : NODE_DEFINITIONS[node.type].connectionRange;
      if (range <= 0) continue;
      const disc = this.createRangeDisc(range, NODE_DEFINITIONS[node.type].color, 0.08);
      disc.position.copy(node.group.position).add(new THREE.Vector3(0, 0.045, 0));
      this.placementPreviewGroup.add(disc);
    }

    if (slotId === null) return;
    const slot = this.slots.get(slotId);
    if (!slot) return;
    const color = valid ? NODE_DEFINITIONS[type].color : 0xff5968;
    const footprint = new THREE.Mesh(new THREE.PlaneGeometry(1.82, 1.82), new THREE.MeshBasicMaterial({
      color, transparent: true, opacity: 0.58, side: THREE.DoubleSide, depthWrite: false,
    }));
    footprint.rotation.x = -Math.PI / 2;
    footprint.position.copy(slot.mesh.position).add(new THREE.Vector3(0, 0.1, 0));
    this.placementPreviewGroup.add(footprint);
    const ghost = new THREE.Group();
    const base = new THREE.Mesh(new THREE.CylinderGeometry(0.74, 0.9, 0.34, 10), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.52, depthWrite: false }));
    base.position.y = 0.2;
    const core = new THREE.Mesh(new THREE.OctahedronGeometry(type === 'nexus' ? 0.62 : 0.42, 0), new THREE.MeshBasicMaterial({ color: valid ? 0xffffff : 0xffb4b9, transparent: true, opacity: 0.82, depthWrite: false }));
    core.position.y = type === 'nexus' ? 1.15 : 0.88;
    ghost.add(base, core);
    ghost.position.copy(slot.mesh.position);
    this.placementPreviewGroup.add(ghost);
    const range = type === 'nexus' ? 3.5 : type === 'special' ? 3 : NODE_DEFINITIONS[type].connectionRange;
    if (range > 0) {
      const disc = this.createRangeDisc(range, color, 0.15);
      disc.position.copy(slot.mesh.position).add(new THREE.Vector3(0, 0.055, 0));
      this.placementPreviewGroup.add(disc);
    }
  }

  private createRangeDisc(radius: number, color: number, opacity: number): THREE.Mesh {
    const disc = new THREE.Mesh(new THREE.CircleGeometry(radius, 56), new THREE.MeshBasicMaterial({
      color, transparent: true, opacity, side: THREE.DoubleSide, depthWrite: false,
    }));
    disc.rotation.x = -Math.PI / 2;
    return disc;
  }

  private setCylinderBetween(mesh: THREE.Mesh, start: THREE.Vector3, end: THREE.Vector3, radius: number): void {
    const delta = end.clone().sub(start);
    const length = Math.max(0.001, delta.length());
    mesh.position.copy(start).lerp(end, 0.5);
    mesh.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), delta.normalize());
    mesh.scale.set(radius, length, radius);
  }

  private clearPlacementPreview(): void {
    while (this.placementPreviewGroup.children.length > 0) {
      const child = this.placementPreviewGroup.children.pop()!;
      disposeObject(child);
    }
  }

  private refreshSelection(): void {
    while (this.selectionGroup.children.length > 0) {
      const child = this.selectionGroup.children.pop()!;
      disposeObject(child);
    }
    const selected = this.selectedNodeId ? this.nodes.get(this.selectedNodeId) : null;
    if (selected) {
      const ring = new THREE.Mesh(new THREE.TorusGeometry(selected.type === 'nexus' ? 1.38 : 1.12, 0.08, 6, 42), this.materials.valid.clone());
      ring.rotation.x = Math.PI / 2;
      ring.position.copy(selected.group.position).add(new THREE.Vector3(0, 0.08, 0));
      this.selectionGroup.add(ring);
    }
    if (this.linkSourceId !== null) this.refreshLinkHints();
    this.refreshTutorialEndpointLabels();
    this.refreshCompletedLinkVisibility();
  }

  private refreshTutorialEndpointLabels(): void {
    while (this.tutorialLabelGroup.children.length > 0) {
      const child = this.tutorialLabelGroup.children.pop()!;
      disposeObject(child);
    }
    if (!ACTIVE_STAGE.tutorial) return;
    const source = this.nodeByType('generator');
    const terminal = this.nodeByType('nexus');
    const routeComplete = Boolean(source && terminal && this.completeGeneratorRoutes().some((route) => (
      route[0]?.id === source.id && route[route.length - 1]?.id === terminal.id
    )));
    const routeChanged = this.tutorialEndpointRouteComplete !== null
      && this.tutorialEndpointRouteComplete !== routeComplete;
    if (routeChanged) {
      this.tutorialEndpointPulseRemaining = this.reducedMotion ? 0 : TUTORIAL_ENDPOINT_PULSE_DURATION;
      this.tutorialEndpointPulseTransitions += 1;
      this.tutorialEndpointPulseDirection = routeComplete ? 'connected' : 'disconnected';
    }
    this.tutorialEndpointRouteComplete = routeComplete;
    const addLabel = (node: NodeState, role: 'source' | 'terminal', labelText: 'ĐẦU' | 'CUỐI'): void => {
      const marker = new THREE.Group();
      marker.name = role === 'source' ? 'tutorialSourceLabelMarker' : 'tutorialTerminalLabelMarker';
      marker.userData.tutorialEndpointRole = role;
      marker.userData.tutorialEndpointLabel = labelText;
      marker.userData.tutorialEndpointConnected = routeComplete;
      marker.userData.tutorialEndpointColor = routeComplete ? '#65f7a4' : '#fff4c9';
      marker.position.copy(node.group.position);
      const label = this.createTutorialEndpointLabel(labelText, routeComplete);
      label.name = role === 'source' ? 'tutorialSourceLabel' : 'tutorialTerminalLabel';
      label.position.y = node.type === 'nexus' ? 3.8 : 3.55;
      label.scale.setScalar(this.tutorialEndpointPulseScale());
      marker.add(label);
      this.tutorialLabelGroup.add(marker);
    };
    if (source) addLabel(source, 'source', 'ĐẦU');
    if (terminal) addLabel(terminal, 'terminal', 'CUỐI');
  }

  private createTutorialEndpointLabel(text: 'ĐẦU' | 'CUỐI', connected: boolean): THREE.Mesh {
    const canvas = document.createElement('canvas');
    canvas.width = 256; canvas.height = 96;
    const context = canvas.getContext('2d')!;
    context.font = '900 52px Arial, sans-serif'; context.textAlign = 'center'; context.textBaseline = 'middle';
    context.lineJoin = 'round'; context.strokeStyle = 'rgba(55, 24, 12, 0.92)'; context.lineWidth = 12;
    context.strokeText(text, 128, 50);
    context.fillStyle = connected ? '#65f7a4' : '#fff4c9'; context.fillText(text, 128, 50);
    const texture = new THREE.CanvasTexture(canvas); texture.colorSpace = THREE.SRGBColorSpace;
    const label = new THREE.Mesh(
      new THREE.PlaneGeometry(2.25, 0.84),
      new THREE.MeshBasicMaterial({ map: texture, transparent: true, depthTest: false, depthWrite: false, toneMapped: false, side: THREE.DoubleSide }),
    );
    label.quaternion.copy(this.camera.quaternion); label.renderOrder = 56;
    label.userData.tutorialEndpointLabel = text;
    label.userData.tutorialEndpointConnected = connected;
    return label;
  }

  private tutorialEndpointPulseScale(): number {
    if (this.reducedMotion || this.screenshotPaused || this.tutorialEndpointPulseRemaining <= 0) return 1;
    const progress = 1 - clamp(this.tutorialEndpointPulseRemaining / TUTORIAL_ENDPOINT_PULSE_DURATION, 0, 1);
    return 1 + (TUTORIAL_ENDPOINT_PULSE_PEAK_SCALE - 1) * Math.sin(progress * Math.PI);
  }

  private updateTutorialEndpointLabelAnimation(delta: number): void {
    if (this.reducedMotion || this.screenshotPaused) this.tutorialEndpointPulseRemaining = 0;
    else this.tutorialEndpointPulseRemaining = Math.max(0, this.tutorialEndpointPulseRemaining - delta);
    const scale = this.tutorialEndpointPulseScale();
    this.tutorialLabelGroup.children.forEach((marker) => {
      const label = marker.getObjectByName(marker.userData.tutorialEndpointRole === 'source'
        ? 'tutorialSourceLabel'
        : 'tutorialTerminalLabel');
      if (!label) return;
      label.quaternion.copy(this.camera.quaternion);
      label.scale.setScalar(scale);
    });
  }

  private refreshLinkHints(): void {
    while (this.selectionGroup.children.length > 0) {
      const child = this.selectionGroup.children.pop()!;
      disposeObject(child);
    }
    const source = this.linkSourceId !== null ? this.nodes.get(this.linkSourceId) : null;
    if (!source) {
      this.canvas.classList.remove('link-target-valid', 'link-target-invalid');
      return;
    }
    const connectionRange = Math.min(MAX_LINK_RANGE, NODE_DEFINITIONS[source.type].connectionRange);
    const disc = new THREE.Mesh(new THREE.CircleGeometry(connectionRange, 64), new THREE.MeshBasicMaterial({ color: 0xa986ff, transparent: true, opacity: 0.11, depthWrite: false, side: THREE.DoubleSide }));
    disc.rotation.x = -Math.PI / 2;
    disc.position.copy(source.group.position).setY(0.16);
    this.selectionGroup.add(disc);
    const sourceRing = new THREE.Mesh(new THREE.TorusGeometry(1.2, 0.13, 8, 42), this.materials.link.clone());
    sourceRing.rotation.x = Math.PI / 2;
    sourceRing.position.copy(source.group.position).add(new THREE.Vector3(0, 0.14, 0));
    this.selectionGroup.add(sourceRing);
    for (const target of this.nodes.values()) {
      if (target.id === source.id) continue;
      const validation = this.validateLink(source, target, false);
      if (!validation.valid || !this.tutorialLinkAllowed(source, target)) continue;
      const hovered = target.id === this.linkHoverTargetId;
      const ring = new THREE.Mesh(new THREE.TorusGeometry(target.type === 'nexus' ? 1.34 : 1.18, hovered ? 0.14 : 0.08, 6, 36), this.materials.valid.clone());
      ring.rotation.x = Math.PI / 2;
      ring.position.copy(target.group.position).add(new THREE.Vector3(0, 0.12, 0));
      this.selectionGroup.add(ring);
    }

    const previewState = this.currentLinkPreviewState();
    this.canvas.classList.toggle('link-target-valid', previewState === 'valid');
    this.canvas.classList.toggle('link-target-invalid', previewState === 'invalid');
    const color = previewState === 'valid' ? 0x67f1b5 : previewState === 'invalid' ? 0xff5968 : 0xffe27a;
    if (this.linkPointerWorld) {
      const preview = this.createDragLinkVisual(this.nodeAnchor(source), this.linkPointerWorld, color);
      this.selectionGroup.add(preview);
    }
    this.refreshLinkDragOverlay(source, previewState);
    if (this.linkHoverTargetId !== null) {
      const target = this.nodes.get(this.linkHoverTargetId);
      if (target && previewState === 'invalid') {
        const ring = new THREE.Mesh(new THREE.TorusGeometry(target.type === 'nexus' ? 1.36 : 1.2, 0.15, 8, 40), this.materials.invalid.clone());
        ring.rotation.x = Math.PI / 2;
        ring.position.copy(target.group.position).add(new THREE.Vector3(0, 0.15, 0));
        this.selectionGroup.add(ring);
      }
    }
  }

  private currentLinkPreviewState(): 'idle' | 'aiming' | 'valid' | 'invalid' {
    if (this.linkSourceId === null) return 'idle';
    const source = this.nodes.get(this.linkSourceId);
    if (!source || !this.linkPointerWorld) return 'aiming';
    if (this.linkHoverTargetId !== null) {
      const target = this.nodes.get(this.linkHoverTargetId);
      if (!target) return 'invalid';
      const validation = this.validateLink(source, target, false);
      return validation.valid && this.tutorialLinkAllowed(source, target) ? 'valid' : 'invalid';
    }
    const start = this.nodeAnchor(source);
    const distance = new THREE.Vector2(start.x, start.z).distanceTo(new THREE.Vector2(this.linkPointerWorld.x, this.linkPointerWorld.z));
    const connectionRange = Math.min(MAX_LINK_RANGE, NODE_DEFINITIONS[source.type].connectionRange);
    return distance > connectionRange || this.linkObstructed(start, this.linkPointerWorld) ? 'invalid' : 'aiming';
  }

  private refreshLinkDragOverlay(source: NodeState, state: 'idle' | 'aiming' | 'valid' | 'invalid'): void {
    const start = this.worldToClient(this.nodeAnchor(source));
    const end = this.linkPointerWorld ? this.worldToClient(this.linkPointerWorld) : null;
    if (!start || !end) { this.linkDragOverlay.dataset.state = 'idle'; return; }
    this.linkDragOverlay.dataset.state = state;
    const dx = end.x - start.x; const dy = end.y - start.y;
    this.linkDragOverlay.style.width = `${Math.hypot(dx, dy).toFixed(2)}px`;
    this.linkDragOverlay.style.transform = `translate3d(${start.x.toFixed(2)}px, ${start.y.toFixed(2)}px, 0) rotate(${Math.atan2(dy, dx)}rad)`;
  }

  private renderWaveRoster(): void {
    const wave = WAVES[Math.min(this.waveIndex, WAVES.length - 1)];
    const counts = new Map<EnemyKind, number>();
    wave.orders.forEach((order) => counts.set(order.kind, (counts.get(order.kind) ?? 0) + 1));
    this.waveEnemies.replaceChildren();
    counts.forEach((count, kind) => {
      const definition = ENEMY_DEFINITIONS[kind];
      const chip = document.createElement('div');
      chip.className = 'enemy-chip';
      const barrier = definition.reactionBarrier ? ` · Lá chắn: ${REACTIONS[definition.reactionBarrier].name}` : '';
      chip.title = `${definition.name} · Mặt đất · HP ${Math.round(definition.hp * wave.healthMultiplier)} · Giáp ${definition.armor} · Kháng phép ${definition.mr} · Cóc −${definition.leakDamage}${barrier}`;
      chip.style.setProperty('--enemy-color', `#${definition.color.toString(16).padStart(6, '0')}`);
      chip.innerHTML = `${definition.icon}<b>${count}</b>`;
      this.waveEnemies.append(chip);
    });
  }

  private updateUi(force: boolean): void {
    this.baseValue.textContent = String(this.baseHp).padStart(2, '0');
    this.goldValue.textContent = String(this.gold).padStart(3, '0');
    this.waveValue.textContent = `${Math.min(this.waveIndex + 1, WAVES.length)} / ${WAVES.length}`;
    this.enemyValue.textContent = String(this.enemies.length + (this.phase === 'wave' ? WAVES[this.waveIndex].orders.length - this.spawnIndex : 0)).padStart(2, '0');
    this.soulValue.textContent = `${String(Math.floor(this.soul)).padStart(2, '0')} / ${MAX_SOUL}`;
    this.phaseLabel.textContent = this.phase === 'preparation' ? 'CHUẨN BỊ'
      : this.phase === 'wave' ? 'ĐỢT ĐANG DIỄN RA'
        : this.phase === 'victoryTravel' ? 'CÓC ĐANG LÊN TRỜI' : this.phase.toUpperCase();
    this.waveTitle.textContent = WAVES[Math.min(this.waveIndex, WAVES.length - 1)].title;
    const validChain = [...this.nodes.values()].some((node) => node.type === 'generator' && node.active);
    const tutorialAllowsStart = this.tutorialStep >= TUTORIAL_COMPLETE_STEP || TUTORIAL_START_STEPS.has(this.tutorialStep);
    const stageTwoLessonComplete = !this.isStageTwoLessonWave() || this.stageTwoLessonComplete();
    this.startWaveButton.disabled = this.phase !== 'preparation' || !validChain || !tutorialAllowsStart || !stageTwoLessonComplete;
    this.startWaveButton.classList.toggle('tutorial-focus', this.isStageTwoLessonWave() && stageTwoLessonComplete);
    this.startWaveButton.textContent = this.phase === 'wave' ? 'ĐANG CHIẾN ĐẤU'
      : this.phase === 'victoryTravel' ? 'ĐANG CHUYỂN MÀN' : 'BẮT ĐẦU';
    const soulPercent = clamp(this.soul / MAX_SOUL * 100, 0, 100);
    const soulFill = this.soulSkillButton.querySelector<HTMLElement>('i');
    if (soulFill) soulFill.style.height = `${soulPercent}%`;
    const soulReady = this.phase === 'wave' && this.soul >= MAX_SOUL;
    this.soulSkillButton.disabled = !soulReady;
    this.soulSkillButton.classList.toggle('locked', !soulReady);
    this.soulSkillButton.classList.toggle('ready', soulReady);
    this.soulSkillButton.classList.toggle('tutorial-focus', ACTIVE_STAGE.tutorial
      && this.waveIndex >= 3 && this.soulSkillTutorial !== 'complete' && soulReady
      && this.enemies.some((enemy) => !enemy.dead));
    const chainReminderVisible = ACTIVE_STAGE.tutorial && this.phase === 'preparation'
      && TUTORIAL_LINK_STEPS.has(this.tutorialStep) && !this.reactionTutorialVisible;
    this.tutorialChainReminder.classList.toggle('hidden', !chainReminderVisible);
    this.tutorialChainReminder.setAttribute('aria-hidden', String(!chainReminderVisible));
    this.buildList.querySelectorAll<HTMLButtonElement>('.build-card').forEach((button) => {
      const type = button.dataset.type as PurchasableNodeType;
      const guided = TUTORIAL_TYPES[this.tutorialStep];
      const lessonFree = this.isMandatoryStageTwoLessonPurchase(type);
      const price = this.currentNodePrice(type);
      const cost = button.querySelector<HTMLElement>('.build-cost');
      if (cost) cost.textContent = String(price);
      const unlocked = this.nodeUnlocked(type);
      button.disabled = this.phase !== 'preparation' || this.gold < price || !unlocked
        || (type === 'nexus' && this.nexusNodeId !== null) || Boolean(guided && guided !== type);
      button.classList.toggle('selected', this.selectedBuildType === type);
      button.classList.toggle('locked', !unlocked);
      button.dataset.locked = String(!unlocked);
      button.dataset.lessonFree = String(lessonFree);
      button.classList.toggle('tutorial-focus', guided === type || lessonFree);
    });
    this.inspector.classList.toggle('tutorial-link-hidden', TUTORIAL_LINK_STEPS.has(this.tutorialStep)
      || (this.isStageTwoLessonWave() && this.stageTwoRequiredLinkPair() !== null));
    this.renderInspector();
    if (force) this.renderWaveRoster();
  }

  private renderInspector(): void {
    const node = this.selectedNodeId ? this.nodes.get(this.selectedNodeId) : null;
    if (!node) {
      this.inspector.classList.add('empty');
      this.inspectorRole.textContent = 'MẠNG ĐẠN';
      this.inspectorState.textContent = '—';
      this.inspectorIcon.textContent = '◇';
      this.inspectorName.textContent = 'Chưa chọn trụ';
      this.inspectorBranch.textContent = 'Chạm một trụ để xem.';
      this.inspectorDetail.textContent = 'Nối các trụ để tạo một chuỗi hoàn chỉnh từ Lò Đạn tới Trống Gọi Mưa.';
      this.queueMeter.classList.add('hidden'); this.chargeMeter.classList.add('hidden'); this.branchControls.classList.add('hidden');
      return;
    }
    const definition = NODE_DEFINITIONS[node.type];
    this.inspector.classList.remove('empty');
    this.inspectorRole.textContent = definition.role.toUpperCase();
    this.inspectorState.textContent = node.active ? 'HOẠT ĐỘNG' : node.invalidReason;
    this.inspectorIcon.textContent = definition.icon;
    this.inspectorName.textContent = definition.name;
    this.inspectorBranch.textContent = node.branch ? BRANCH_NAMES[node.branch] : 'Chưa nâng cấp';
    this.inspectorDetail.textContent = definition.description;
    const capacity = node.type === 'special' ? nodeCapacity(node) : 0;
    this.queueMeter.classList.toggle('hidden', capacity === 0);
    if (capacity > 0) {
      this.queueFill.style.width = `${clamp((node.buffer.length + node.reservedIncoming) / capacity * 100, 0, 100)}%`;
      this.queueValue.textContent = `${node.buffer.length} + ${node.reservedIncoming} / ${capacity}`;
    }
    const chargeMax = node.type === 'support' ? (node.branch ? 8 : 6) : node.type === 'special' ? (node.branch === 'rapidPulse' ? 3 : node.branch === 'impactPulse' ? 7 : 5) : node.type === 'nexus' ? MAX_SOUL : 0;
    const charge = node.type === 'support' ? node.charge : node.type === 'special' ? node.pulseCharge : node.type === 'nexus' ? this.soul : 0;
    this.chargeMeter.classList.toggle('hidden', chargeMax === 0);
    if (chargeMax > 0) {
      this.chargeLabel.textContent = node.type === 'support' ? 'TIẾP SỨC' : node.type === 'special' ? 'SẤM LỰC' : 'SỨC CÓC';
      this.chargeFill.style.width = `${clamp(charge / chargeMax * 100, 0, 100)}%`;
      this.chargeValue.textContent = `${Math.floor(charge)} / ${chargeMax}`;
    }
    const branches = definition.branches;
    this.branchControls.classList.toggle('hidden', !branches);
    if (branches) {
      const renderBranch = (button: HTMLButtonElement, branch: Branch): void => {
        button.innerHTML = `<strong>${BRANCH_NAMES[branch]}</strong><small>${BRANCH_DESCRIPTIONS[branch]}</small>`;
        button.title = `${BRANCH_NAMES[branch]} — ${BRANCH_DESCRIPTIONS[branch]}`;
        button.setAttribute('aria-label', button.title);
      };
      renderBranch(this.branchA, branches[0]); renderBranch(this.branchB, branches[1]);
      this.branchA.classList.toggle('selected', node.branch === branches[0]);
      this.branchB.classList.toggle('selected', node.branch === branches[1]);
      const disabled = this.phase !== 'preparation' || node.branch !== null || this.gold < definition.upgradeCost;
      this.branchA.disabled = disabled; this.branchB.disabled = disabled;
    }
    this.upgradeButton.disabled = this.phase !== 'preparation' || node.branch !== null || !branches;
    this.sellButton.disabled = this.phase !== 'preparation' || node.type === 'nexus';
  }

  private updateTutorialCue(): void {
    const soulSkillCue = ACTIVE_STAGE.tutorial && this.tutorialStep >= TUTORIAL_COMPLETE_STEP
      && this.waveIndex >= 3 && this.phase === 'wave' && this.soul >= MAX_SOUL
      && this.enemies.some((enemy) => !enemy.dead)
      && this.soulSkillTutorial !== 'complete' && !this.reactionTutorialVisible;
    const stageTwoLessonCue = this.isStageTwoLessonWave();
    if ((!ACTIVE_STAGE.tutorial && !stageTwoLessonCue) || this.phase === 'victoryTravel' || this.phase === 'won' || this.phase === 'lost'
      || (ACTIVE_STAGE.tutorial && this.tutorialStep >= TUTORIAL_COMPLETE_STEP && !soulSkillCue)) {
      this.tutorialHand.classList.add('hidden'); return;
    }
    let start: { x: number; y: number } | null = null;
    let end: { x: number; y: number } | null = null;
    let dragging = false;
    const guidedType = TUTORIAL_TYPES[this.tutorialStep];
    const guidedSlot = TUTORIAL_PLACEMENT_SLOTS[this.tutorialStep];
    if (stageTwoLessonCue) {
      const required = this.stageTwoRequiredNode();
      const pair = this.stageTwoRequiredLinkPair();
      if (required) {
        const cardSelector = `.build-card[data-type="${required}"]`;
        const card = document.querySelector<HTMLElement>(cardSelector);
        card?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        start = this.elementCenter(cardSelector);
        const slotId = this.stageTwoRequiredSlot(required);
        const slot = slotId === null ? null : this.slots.get(slotId);
        end = slot ? this.worldToClient(slot.mesh.position.clone().add(new THREE.Vector3(0, 0.5, 0))) : null;
        dragging = true;
      } else if (pair) {
        start = this.worldToClient(pair.source.group.position.clone().add(new THREE.Vector3(0, 1.2, 0)));
        end = this.worldToClient(pair.target.group.position.clone().add(new THREE.Vector3(0, 1.2, 0)));
        dragging = true;
      } else {
        end = this.elementCenter('#start-wave');
      }
    } else if (soulSkillCue) {
      start = this.elementCenter('#soul-skill');
      const target = this.soulTutorialTargetPosition();
      end = target ? this.worldToClient(target) : null;
      dragging = true;
    } else if (guidedType && guidedSlot !== undefined) {
      start = this.elementCenter(`.build-card[data-type="${guidedType}"]`);
      end = this.worldToClient(this.slots.get(guidedSlot)?.mesh.position.clone().add(new THREE.Vector3(0, 0.5, 0)) ?? new THREE.Vector3());
      dragging = true;
    } else if (TUTORIAL_LINK_STEPS.has(this.tutorialStep)) {
      const pair = this.tutorialPair();
      start = pair ? this.worldToClient(pair.source.group.position.clone().add(new THREE.Vector3(0, 1.2, 0))) : null;
      end = pair ? this.worldToClient(pair.target.group.position.clone().add(new THREE.Vector3(0, 1.2, 0))) : null;
      dragging = true;
    } else if (TUTORIAL_START_STEPS.has(this.tutorialStep)) end = this.elementCenter('#start-wave');
    start ??= end;
    end ??= start;
    if (!start || !end || (!soulSkillCue && this.phase === 'wave') || this.phase === 'paused' || this.soulSkillDrag !== null) {
      this.tutorialHand.classList.add('hidden'); return;
    }
    const safe = (point: { x: number; y: number }) => ({
      x: clamp(point.x, 26, window.innerWidth - 26), y: clamp(point.y, 26, window.innerHeight - 58),
    });
    const safeStart = safe(start); const safeEnd = safe(end);
    this.tutorialHand.classList.remove('hidden');
    this.tutorialHand.classList.toggle('drag', dragging);
    this.tutorialHand.classList.toggle('tap', !dragging);
    this.tutorialHand.dataset.mode = dragging ? 'drag' : 'tap';
    this.tutorialHand.style.setProperty('--hand-start-x', `${safeStart.x}px`);
    this.tutorialHand.style.setProperty('--hand-start-y', `${safeStart.y}px`);
    this.tutorialHand.style.setProperty('--hand-end-x', `${safeEnd.x}px`);
    this.tutorialHand.style.setProperty('--hand-end-y', `${safeEnd.y}px`);
  }

  private soulTutorialTargetPosition(): THREE.Vector3 | null {
    const livingEnemies = this.enemies.filter((enemy) => !enemy.dead);
    if (livingEnemies.length === 0) return null;
    const { radius } = this.tongueProfile();
    let best = livingEnemies[0];
    let bestCount = -1;
    for (const candidate of livingEnemies) {
      const count = livingEnemies.reduce((total, enemy) => total + (enemy.group.position.distanceTo(candidate.group.position) <= radius ? 1 : 0), 0);
      if (count > bestCount) { best = candidate; bestCount = count; }
    }
    return best.group.position.clone().setY(0.2);
  }

  private findStageTwoLessonSlot(type: StageTwoLessonType): number | null {
    const cached = this.stageTwoLessonSlots.get(type);
    if (cached !== undefined && this.validatePlacement(type, cached, false).valid) return cached;
    this.stageTwoLessonSlots.delete(type);
    if (this.nexusNodeId === null) return null;
    const nexus = this.nodes.get(this.nexusNodeId);
    const source = [...this.nodes.values()].find((node) => node.outputTargetId === this.nexusNodeId);
    if (!nexus || !source || source.slotId === null) return null;
    const sourceTier = BUILD_SLOTS[source.slotId]?.tier;
    const sourceRange = Math.min(MAX_LINK_RANGE, NODE_DEFINITIONS[source.type].connectionRange);
    const outputRange = Math.min(MAX_LINK_RANGE, NODE_DEFINITIONS[type].connectionRange);
    const canvasBounds = this.canvas.getBoundingClientRect();
    let best: { slotId: number; score: number } | null = null;
    for (const slot of this.slots.values()) {
      if (!this.validatePlacement(type, slot.id, false).valid || BUILD_SLOTS[slot.id]?.tier !== sourceTier) continue;
      const sourceDistance = source.group.position.distanceTo(slot.mesh.position);
      const nexusDistance = nexus.group.position.distanceTo(slot.mesh.position);
      if (sourceDistance > sourceRange + 1e-6 || nexusDistance > outputRange + 1e-6) continue;
      const start = this.nodeAnchor(source);
      const center = slot.mesh.position.clone().add(new THREE.Vector3(0, 1, 0));
      if (this.linkObstructed(start, center) || this.linkObstructed(center, this.nodeAnchor(nexus))) continue;
      const client = this.worldToClient(slot.mesh.position);
      if (!client || client.x < canvasBounds.left + 24 || client.x > canvasBounds.right - 24
        || client.y < canvasBounds.top + 24 || client.y > canvasBounds.bottom - 24) continue;
      const laneDistance = this.distanceToEnemyPath(slot.mesh.position.x, slot.mesh.position.z);
      const score = type === 'special'
        ? 260 - laneDistance * 70 - sourceDistance * 2 - nexusDistance
        : 180 - sourceDistance * 4 - nexusDistance * 2 - laneDistance;
      if (!best || score > best.score) best = { slotId: slot.id, score };
    }
    if (best) this.stageTwoLessonSlots.set(type, best.slotId);
    return best?.slotId ?? null;
  }

  private distanceToEnemyPath(x: number, z: number): number {
    let best = Number.POSITIVE_INFINITY;
    for (let index = 1; index < ENEMY_PATH.length; index += 1) {
      const [ax, az] = ENEMY_PATH[index - 1];
      const [bx, bz] = ENEMY_PATH[index];
      const dx = bx - ax; const dz = bz - az;
      const lengthSquared = dx * dx + dz * dz;
      const t = lengthSquared <= 1e-8 ? 0 : clamp(((x - ax) * dx + (z - az) * dz) / lengthSquared, 0, 1);
      best = Math.min(best, Math.hypot(x - (ax + dx * t), z - (az + dz * t)));
    }
    return best;
  }

  private tutorialPair(): { source: NodeState; target: NodeState } | null {
    const generator = this.nodeByType('generator'); const fire = this.nodeByType('fire');
    const ice = this.nodeByType('ice'); const nexus = this.nodeByType('nexus');
    if (this.tutorialStep === 2 && generator && nexus) return { source: generator, target: nexus };
    if (this.tutorialStep === 5 && generator && fire) return { source: generator, target: fire };
    if (this.tutorialStep === 6 && fire && nexus) return { source: fire, target: nexus };
    if (this.tutorialStep === 9 && fire && ice) return { source: fire, target: ice };
    if (this.tutorialStep === 10 && ice && nexus) return { source: ice, target: nexus };
    return null;
  }

  private tutorialObjective(): string {
    if (this.isStageTwoLessonWave()) {
      const type = this.stageTwoLessonType();
      const required = this.stageTwoRequiredNode();
      if (required) return `place-${required}`;
      const pair = this.stageTwoRequiredLinkPair();
      if (pair && type) return pair.source.type === type ? `link-${type}-nexus` : `link-chain-${type}`;
      return `start-wave-${this.waveIndex + 1}`;
    }
    if (!ACTIVE_STAGE.tutorial) return 'free-play';
    return ({
      0: 'place-nexus', 1: 'place-generator', 2: 'link-generator-nexus', 3: 'start-wave-one',
      4: 'place-fire', 5: 'link-generator-fire', 6: 'link-fire-nexus', 7: 'start-wave-two',
      8: 'place-ice', 9: 'link-fire-ice', 10: 'link-ice-nexus', 11: 'start-wave-three',
    } as Record<number, string>)[this.tutorialStep] ?? 'free-play';
  }

  private animate(elapsed: number, delta: number): void {
    const hitFlash = Math.max(0, Number(this.baseNexus.userData.hitFlash ?? 0) - delta);
    this.baseNexus.userData.hitFlash = hitFlash;
    this.baseNexus.scale.setScalar(1 + hitFlash * 0.24);
    this.updateTutorialEndpointLabelAnimation(delta);
    if (this.reducedMotion || this.screenshotPaused) return;
    this.scene.traverse((object) => {
      if (object.name === 'spinner') object.rotation.y += delta * 0.8;
      if (object.name === 'windSpinner') object.rotation.z -= delta * 1.45;
      if (object.name === 'ambientSoul' || object.name === 'nexusCore' || object.name === 'baseNexusCore' || object.name === 'chargeCore') {
        const baseY = object.userData.baseY ??= object.position.y;
        object.position.y = baseY + Math.sin(elapsed * 2 + object.id) * 0.07;
      }
    });
    const baseHalo = this.baseNexus.getObjectByName('baseNexusHalo');
    if (baseHalo) baseHalo.rotation.z += delta * 0.62;
    this.links.forEach((link, index) => { link.group.children[1]?.position.addScaledVector(new THREE.Vector3(0, Math.sin(elapsed * 3 + index) * 0.0008, 0), 1); });
  }

  private spawnBurst(position: THREE.Vector3, color: number, size: number): void {
    const group = new THREE.Group();
    group.position.copy(position);
    const count = this.reducedMotion ? 4 : 10;
    for (let index = 0; index < count; index += 1) {
      const shard = new THREE.Mesh(new THREE.OctahedronGeometry(0.055 + this.rng() * 0.07, 0), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.9 }));
      const angle = index / count * Math.PI * 2 + this.rng() * 0.35;
      const radius = size * (0.35 + this.rng() * 0.65);
      shard.position.set(Math.cos(angle) * radius, this.rng() * size * 0.6, Math.sin(angle) * radius);
      group.add(shard);
    }
    this.effectGroup.add(group);
    this.vfx.push({ group, remaining: 0.5, duration: 0.5, rise: 0.55 });
  }

  private spawnPulse(position: THREE.Vector3, radius: number, color: number): void {
    const group = new THREE.Group();
    group.position.copy(position); group.position.y = Math.max(0.25, group.position.y);
    const ring = new THREE.Mesh(new THREE.TorusGeometry(radius, 0.12, 7, 64), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.86, depthWrite: false }));
    ring.rotation.x = Math.PI / 2;
    group.add(ring);
    const disc = new THREE.Mesh(new THREE.CircleGeometry(radius, 48), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.22, depthWrite: false, side: THREE.DoubleSide }));
    disc.rotation.x = -Math.PI / 2;
    group.add(disc);
    this.effectGroup.add(group);
    this.vfx.push({ group, remaining: 1.05, duration: 1.05, rise: 0.08 });
  }

  private spawnSkillImpact(position: THREE.Vector3, radius: number, color: number): void {
    this.spawnPulse(position, radius, color);
    const group = new THREE.Group();
    group.position.copy(position); group.position.y = Math.max(0.25, group.position.y);
    const glow = new THREE.Mesh(
      new THREE.CylinderGeometry(radius * 0.22, radius * 0.68, Math.max(1.8, radius * 0.9), 20, 1, true),
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.34, depthWrite: false, side: THREE.DoubleSide }),
    );
    glow.position.y = Math.max(0.9, radius * 0.45);
    const inner = new THREE.Mesh(
      new THREE.CylinderGeometry(radius * 0.06, radius * 0.24, Math.max(2.1, radius), 14, 1, true),
      new THREE.MeshBasicMaterial({ color: 0xfff6dc, transparent: true, opacity: 0.72, depthWrite: false, side: THREE.DoubleSide }),
    );
    inner.position.y = glow.position.y;
    group.add(glow, inner);
    this.effectGroup.add(group);
    this.vfx.push({ group, remaining: 1.8, duration: 1.8, rise: 0.16 });
  }

  private spawnTongueImpact(position: THREE.Vector3, radius: number, color: number): void {
    this.spawnTongueDirtBurst(position);
    const group = new THREE.Group();
    group.name = 'tongueImpact';
    group.position.copy(position).setY(0.32);
    const center = new THREE.Mesh(
      new THREE.CircleGeometry(radius * 0.46, 28),
      new THREE.MeshBasicMaterial({
        color, transparent: true, opacity: TONGUE_IMPACT_DISC_OPACITY,
        depthWrite: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide,
      }),
    );
    center.rotation.x = -Math.PI / 2; center.renderOrder = 37; group.add(center);
    const ring = new THREE.Mesh(
      new THREE.TorusGeometry(radius, 0.055, 6, 56),
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.24, depthWrite: false }),
    );
    ring.rotation.x = Math.PI / 2; ring.renderOrder = 37; group.add(ring);
    for (let index = 0; index < 8; index += 1) {
      const angle = index / 8 * Math.PI * 2;
      const splash = new THREE.Mesh(
        new THREE.ConeGeometry(0.065, 0.42 + (index % 2) * 0.1, 5),
        new THREE.MeshBasicMaterial({
          color: index % 2 === 0 ? color : 0xffe5b4,
          transparent: true, opacity: 0.3, depthWrite: false,
        }),
      );
      splash.position.set(Math.cos(angle) * radius * 0.82, 0.2, Math.sin(angle) * radius * 0.82);
      splash.rotation.z = Math.PI / 2.6;
      splash.rotation.y = -angle;
      splash.renderOrder = 38;
      group.add(splash);
    }
    this.effectGroup.add(group);
    this.vfx.push({ group, remaining: 0.42, duration: 0.42, rise: 0.1, fadeFromAuthoredOpacity: true });
  }

  private spawnTongueDirtBurst(position: THREE.Vector3): void {
    const group = new THREE.Group(); group.name = 'tongueDirtBurst';
    group.position.copy(position).setY(0.34);
    const colors = [0x6f3518, 0x9a4f20, 0xc8792f, 0xdfa14a];
    for (let index = 0; index < TONGUE_DIRT_PARTICLE_COUNT; index += 1) {
      const angle = index / TONGUE_DIRT_PARTICLE_COUNT * Math.PI * 2 + (this.rng() - 0.5) * 0.34;
      const speed = 2.1 + this.rng() * 2.2;
      const particle = new THREE.Mesh(
        new THREE.TetrahedronGeometry(0.11 + this.rng() * 0.1, 0),
        new THREE.MeshStandardMaterial({
          color: colors[index % colors.length], roughness: 1, metalness: 0,
          transparent: true, opacity: 0.88,
        }),
      );
      particle.name = 'tongueDirtParticle';
      particle.position.set(Math.cos(angle) * 0.24, 0.08 + this.rng() * 0.12, Math.sin(angle) * 0.24);
      particle.userData.velocity = new THREE.Vector3(
        Math.cos(angle) * speed,
        4.8 + this.rng() * 2.4,
        Math.sin(angle) * speed,
      );
      particle.userData.spin = new THREE.Vector3(
        (this.rng() - 0.5) * 9,
        (this.rng() - 0.5) * 9,
        (this.rng() - 0.5) * 9,
      );
      group.add(particle);
    }
    this.effectGroup.add(group);
    this.vfx.push({
      group, remaining: 0.68, duration: 0.68, rise: 0,
      fadeFromAuthoredOpacity: true, ballisticParticles: true,
    });
  }

  private spawnDamageNumber(position: THREE.Vector3, damage: number, color: number): void {
    const canvas = document.createElement('canvas'); canvas.width = 160; canvas.height = 80;
    const context = canvas.getContext('2d')!;
    context.font = '900 40px sans-serif'; context.textAlign = 'center'; context.textBaseline = 'middle';
    context.lineWidth = 9; context.strokeStyle = 'rgba(11,8,22,.9)'; context.strokeText(`−${Math.round(damage)}`, 80, 40);
    context.fillStyle = `#${color.toString(16).padStart(6, '0')}`; context.fillText(`−${Math.round(damage)}`, 80, 40);
    const texture = new THREE.CanvasTexture(canvas); texture.colorSpace = THREE.SRGBColorSpace;
    const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: texture, transparent: true, depthWrite: false, toneMapped: false }));
    sprite.position.copy(position).add(new THREE.Vector3(0, 1.65, 0));
    sprite.scale.set(2.2, 1.1, 1); sprite.renderOrder = 40;
    const group = new THREE.Group(); group.add(sprite); this.effectGroup.add(group);
    this.vfx.push({ group, remaining: 1.65, duration: 1.65, rise: 0.82 });
  }

  private spawnReactionSeal(position: THREE.Vector3, reaction: keyof typeof REACTIONS, showIcon = false): void {
    const definition = REACTIONS[reaction];
    const group = new THREE.Group();
    group.position.copy(position);
    const ring = new THREE.Mesh(new THREE.TorusGeometry(0.8, 0.1, 6, 36), new THREE.MeshBasicMaterial({ color: definition.color, transparent: true, opacity: 0.95, depthWrite: false }));
    ring.rotation.x = Math.PI / 2;
    group.add(ring);
    for (let index = 0; index < 6; index += 1) {
      const shard = new THREE.Mesh(new THREE.ConeGeometry(0.09, 0.55, 5), new THREE.MeshBasicMaterial({ color: definition.color }));
      const angle = index / 6 * Math.PI * 2;
      shard.position.set(Math.cos(angle) * 0.9, 0, Math.sin(angle) * 0.9);
      shard.rotation.z = Math.PI / 2;
      shard.rotation.y = -angle;
      group.add(shard);
    }
    if (showIcon) {
      const icon = this.createGlyphSprite(definition.icon, definition.color);
      icon.position.y = 1.25; icon.scale.setScalar(1.55); group.add(icon);
    }
    this.effectGroup.add(group);
    this.vfx.push({ group, remaining: 0.9, duration: 0.9, rise: 1.1 });
  }

  private createGlyphSprite(glyph: string, color: number): THREE.Sprite {
    const canvas = document.createElement('canvas'); canvas.width = 96; canvas.height = 96;
    const context = canvas.getContext('2d')!; context.clearRect(0, 0, 96, 96);
    context.fillStyle = 'rgba(9, 8, 20, .82)'; context.beginPath(); context.arc(48, 48, 42, 0, Math.PI * 2); context.fill();
    context.strokeStyle = `#${color.toString(16).padStart(6, '0')}`; context.lineWidth = 5; context.stroke();
    context.fillStyle = '#ffffff'; context.font = '700 50px sans-serif'; context.textAlign = 'center'; context.textBaseline = 'middle'; context.fillText(glyph, 48, 51);
    const texture = new THREE.CanvasTexture(canvas); texture.colorSpace = THREE.SRGBColorSpace;
    const sprite = new THREE.Sprite(new THREE.SpriteMaterial({ map: texture, transparent: true, depthWrite: false, toneMapped: false }));
    sprite.renderOrder = 16; return sprite;
  }

  private updateVfx(delta: number): void {
    for (let index = this.vfx.length - 1; index >= 0; index -= 1) {
      const vfx = this.vfx[index];
      vfx.remaining -= delta;
      if (vfx.ballisticParticles) {
        vfx.group.children.forEach((particle) => {
          const velocity = particle.userData.velocity;
          const spin = particle.userData.spin;
          if (velocity instanceof THREE.Vector3) {
            particle.position.addScaledVector(velocity, delta);
            velocity.y -= 7.4 * delta;
          }
          if (spin instanceof THREE.Vector3) {
            particle.rotation.x += spin.x * delta;
            particle.rotation.y += spin.y * delta;
            particle.rotation.z += spin.z * delta;
          }
        });
      } else vfx.group.position.y += vfx.rise * delta;
      const opacity = clamp(vfx.remaining / vfx.duration, 0, 1);
      vfx.group.traverse((object) => {
        if ((object instanceof THREE.Mesh || object instanceof THREE.Sprite) && 'opacity' in object.material) {
          const material = object.material as THREE.Material & { opacity: number; transparent: boolean };
          if (vfx.fadeFromAuthoredOpacity) {
            const authoredOpacity = Number(material.userData.vfxAuthoredOpacity ?? material.opacity);
            material.userData.vfxAuthoredOpacity = authoredOpacity;
            material.opacity = authoredOpacity * opacity;
          } else material.opacity = opacity;
        }
      });
      if (vfx.remaining <= 0) { this.effectGroup.remove(vfx.group); disposeObject(vfx.group); this.vfx.splice(index, 1); }
    }
  }

  private enemiesInRadius(position: THREE.Vector3, radius: number): EnemyState[] {
    return this.enemies.filter((enemy) => !enemy.dead && enemy.group.position.distanceTo(position) <= radius);
  }

  private nodeAnchor(node: NodeState): THREE.Vector3 { return node.group.position.clone().add(new THREE.Vector3(0, 0.32, 0)); }

  private orientNodeToTarget(source: NodeState, target: NodeState): void {
    const delta = target.group.position.clone().sub(source.group.position);
    source.group.rotation.y = -Math.atan2(delta.z, delta.x);
  }

  private linkObstructed(start: THREE.Vector3, end: THREE.Vector3): boolean {
    const direction = end.clone().sub(start);
    const ray = new THREE.Ray(start, direction.clone().normalize());
    const length = direction.length();
    return this.obstacles.some((obstacle) => {
      const hit = ray.intersectBox(obstacle.box, new THREE.Vector3());
      return Boolean(hit && hit.distanceTo(start) > 0.3 && hit.distanceTo(start) < length - 0.3);
    });
  }

  private pathLength(): number {
    let total = 0;
    for (let index = 0; index < ENEMY_PATH.length - 1; index += 1) total += Math.hypot(ENEMY_PATH[index + 1][0] - ENEMY_PATH[index][0], ENEMY_PATH[index + 1][1] - ENEMY_PATH[index][1]);
    return total;
  }

  private pathTransform(progress: number, sideOffset: number, layer: 0 | 1 = 0): { position: THREE.Vector3; rotation: number } {
    const height = layer === 1 ? 2.72 : 0.2;
    let remaining = progress;
    for (let index = 0; index < ENEMY_PATH.length - 1; index += 1) {
      const [ax, az] = ENEMY_PATH[index]; const [bx, bz] = ENEMY_PATH[index + 1];
      const dx = bx - ax; const dz = bz - az; const length = Math.hypot(dx, dz);
      if (remaining <= length || index === ENEMY_PATH.length - 2) {
        const t = clamp(remaining / Math.max(0.001, length), 0, 1);
        const nx = -dz / length; const nz = dx / length;
        return { position: new THREE.Vector3(ax + dx * t + nx * sideOffset, height, az + dz * t + nz * sideOffset), rotation: -Math.atan2(dz, dx) };
      }
      remaining -= length;
    }
    const last = ENEMY_PATH[ENEMY_PATH.length - 1];
    return { position: new THREE.Vector3(last[0], height, last[1]), rotation: 0 };
  }

  private nodeAt(clientX: number, clientY: number): number | null {
    const guidedPair = this.isStageTwoLessonWave() ? this.stageTwoRequiredLinkPair() : null;
    if (guidedPair) {
      const guidedNode = this.linkSourceId === guidedPair.source.id ? guidedPair.target : guidedPair.source;
      const guidedPoint = this.worldToClient(this.nodeAnchor(guidedNode));
      const guidedRadius = window.matchMedia('(pointer: coarse)').matches ? 52 : 40;
      if (guidedPoint && Math.hypot(guidedPoint.x - clientX, guidedPoint.y - clientY) <= guidedRadius) return guidedNode.id;
    }
    this.setRay(clientX, clientY);
    const intersections = this.raycaster.intersectObjects([...this.nodes.values()].map((node) => node.group), true);
    for (const intersection of intersections) {
      let object: THREE.Object3D | null = intersection.object;
      while (object) { if (typeof object.userData.nodeId === 'number') return object.userData.nodeId; object = object.parent; }
    }
    // Procedural tower silhouettes contain gaps. Keep selection/linking reliable
    // by falling back to a bounded screen-space hit area around each node.
    const radius = window.matchMedia('(pointer: coarse)').matches ? 46 : 34;
    let nearest: { id: number; distance: number } | null = null;
    for (const node of this.nodes.values()) {
      const point = this.worldToClient(this.nodeAnchor(node));
      if (!point) continue;
      const distance = Math.hypot(point.x - clientX, point.y - clientY);
      if (distance > radius || nearest && distance >= nearest.distance) continue;
      nearest = { id: node.id, distance };
    }
    return nearest?.id ?? null;
  }

  private slotAt(clientX: number, clientY: number): number | null {
    const radius = window.matchMedia('(pointer: coarse)').matches ? 50 : 38;
    let nearest: { id: number; distance: number } | null = null;
    for (const slot of this.slots.values()) {
      if (slot.occupiedNodeId !== null) continue;
      const point = this.worldToClient(slot.mesh.position);
      if (!point) continue;
      const distance = Math.hypot(point.x - clientX, point.y - clientY);
      if (distance > radius || nearest && distance >= nearest.distance) continue;
      nearest = { id: slot.id, distance };
    }
    return nearest?.id ?? null;
  }

  private groundPoint(clientX: number, clientY: number): THREE.Vector3 | null {
    const point = this.pointerWorldAtHeight(clientX, clientY, 0);
    if (!point) return null;
    point.x = clamp(point.x, MAP_BOUNDS.minX, MAP_BOUNDS.maxX);
    point.z = clamp(point.z, MAP_BOUNDS.minZ, MAP_BOUNDS.maxZ);
    return point;
  }

  private skillGroundPoint(clientX: number, clientY: number): THREE.Vector3 | null {
    const rect = this.canvas.getBoundingClientRect();
    if (clientX < rect.left || clientX > rect.right || clientY < rect.top || clientY > rect.bottom) return null;
    return this.groundPoint(clientX, clientY);
  }

  private pointerWorldAtHeight(clientX: number, clientY: number, height: number): THREE.Vector3 | null {
    this.setRay(clientX, clientY);
    return this.raycaster.ray.intersectPlane(new THREE.Plane(new THREE.Vector3(0, 1, 0), -height), new THREE.Vector3());
  }

  private setRay(clientX: number, clientY: number): void {
    const rect = this.canvas.getBoundingClientRect();
    this.pointerNdc.set((clientX - rect.left) / rect.width * 2 - 1, -((clientY - rect.top) / rect.height) * 2 + 1);
    this.raycaster.setFromCamera(this.pointerNdc, this.camera);
  }

  private pointerPairState(): { distance: number; centroid: THREE.Vector2 } {
    const pointers = [...this.activePointers.values()];
    if (pointers.length < 2) return { distance: 0, centroid: new THREE.Vector2() };
    return {
      distance: Math.hypot(pointers[1].x - pointers[0].x, pointers[1].y - pointers[0].y),
      centroid: new THREE.Vector2((pointers[0].x + pointers[1].x) * 0.5, (pointers[0].y + pointers[1].y) * 0.5),
    };
  }

  private updateCamera(): void {
    this.cameraYaw = Math.atan2(Math.sin(this.cameraYaw), Math.cos(this.cameraYaw));
    const horizontal = Math.cos(this.cameraPitch);
    const direction = new THREE.Vector3(
      Math.sin(this.cameraYaw) * horizontal,
      Math.sin(this.cameraPitch),
      Math.cos(this.cameraYaw) * horizontal,
    );
    this.camera.position.copy(this.cameraTarget).addScaledVector(direction, this.cameraDistance);
    if (!this.reducedMotion && this.cameraShakeRemaining > 0 && this.cameraShakeDuration > 0) {
      const progress = 1 - this.cameraShakeRemaining / this.cameraShakeDuration;
      const falloff = this.cameraShakeRemaining / this.cameraShakeDuration;
      this.camera.position.x += Math.sin(progress * Math.PI * 19) * this.cameraShakeStrength * falloff;
      this.camera.position.y += Math.sin(progress * Math.PI * 27) * this.cameraShakeStrength * 0.45 * falloff;
      this.camera.position.z += Math.cos(progress * Math.PI * 23) * this.cameraShakeStrength * 0.72 * falloff;
    }
    this.camera.lookAt(this.cameraTarget);
    // Tutorial projection runs before the first renderer.render(). Keep the
    // camera inverse matrix current so the hand never projects from identity.
    this.camera.updateMatrixWorld(true);
  }

  private triggerCameraShake(duration: number, strength: number): void {
    if (this.reducedMotion) return;
    this.cameraShakeDuration = duration;
    this.cameraShakeRemaining = duration;
    this.cameraShakeStrength = strength;
  }

  private frogMouthWorldPosition(): THREE.Vector3 {
    this.frogActor.updateWorldMatrix(true, false);
    return this.frogActor.localToWorld(new THREE.Vector3(-1.42, 1.08, 0));
  }

  private updateFrogSkillAnchor(): void {
    this.frogActor.updateWorldMatrix(true, false);
    const anchor = this.frogActor.localToWorld(new THREE.Vector3(-0.35, 3.7, 0));
    const projected = this.worldToClient(anchor);
    const visible = projected !== null && this.phase !== 'victoryTravel' && this.phase !== 'won' && this.phase !== 'lost';
    this.soulSkillButton.style.visibility = visible ? 'visible' : 'hidden';
    if (!projected) return;
    this.soulSkillButton.style.setProperty('--frog-skill-x', `${projected.x.toFixed(2)}px`);
    this.soulSkillButton.style.setProperty('--frog-skill-y', `${projected.y.toFixed(2)}px`);
  }

  private mobileDpr(): number { return window.matchMedia('(pointer: coarse)').matches ? 1.35 : 1.8; }
  private render(): void { this.updateFrogSkillAnchor(); this.renderer.render(this.scene, this.camera); }

  private worldToClient(position: THREE.Vector3): { x: number; y: number } | null {
    const projected = position.clone().project(this.camera);
    if (projected.z < -1 || projected.z > 1) return null;
    const rect = this.canvas.getBoundingClientRect();
    return { x: rect.left + (projected.x + 1) * 0.5 * rect.width, y: rect.top + (1 - projected.y) * 0.5 * rect.height };
  }

  private elementCenter(selector: string): { x: number; y: number } | null {
    const element = document.querySelector<HTMLElement>(selector);
    if (!element || element.classList.contains('hidden') || getComputedStyle(element).visibility === 'hidden') return null;
    const rect = element.getBoundingClientRect();
    if (rect.width <= 0 || rect.height <= 0) return null;
    return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
  }

  private nodeByType(type: NodeType): NodeState | undefined {
    return [...this.nodes.values()].find((node) => node.type === type);
  }

  private error(message: string): void { this.audio.ui('error'); this.showToast(message); }
  private showToast(message: string): void { this.toast.textContent = message; this.toast.classList.remove('hidden'); this.lastToastTimer = 2.4; }

  private el(selector: string): HTMLElement {
    const element = document.querySelector<HTMLElement>(selector);
    if (!element) throw new Error(`Missing ${selector}`);
    return element;
  }

  private button(selector: string): HTMLButtonElement {
    const element = document.querySelector<HTMLButtonElement>(selector);
    if (!element) throw new Error(`Missing ${selector}`);
    return element;
  }

  private publishHooks(): void {
    window.__THREE_GAME_TEST_HOOKS__ = {
      snapshot: () => this.snapshot(),
      reset: () => this.resetRun(),
      seed: (seed: number) => { this.rng = createSeededRandom(seed); },
      setReducedMotion: (enabled: boolean) => { this.reducedMotion = enabled; },
      setPausedForScreenshot: (paused: boolean) => { this.screenshotPaused = paused; },
      setState: (name: string) => {
        if (name === 'mastery-ready') this.createMasteryCheckpointDemo(0);
        else if (name === 'mastery-two-leaks') this.createMasteryCheckpointDemo(2);
        else if (name === 'mastery-fail') this.createMasteryCheckpointDemo(3);
        else if (name === 'mastery-baseline-final') this.createMasteryFinalDemo(false);
        else if (name === 'mastery-expanded-final') this.createMasteryFinalDemo(true);
        else if (name === 'tutorial-reaction') {
          this.autoBuildMinimumChain();
          this.waveIndex = 2;
          this.tutorialStep = 11;
          this.renderWaveRoster();
          this.updateUi(true);
        }
        else if (name === 'intro-currency') {
          this.resetRun();
          this.selectedBuildType = 'nexus'; this.tryPlaceSelected(TUTORIAL_NODE_SLOTS.nexus);
          this.selectedBuildType = 'generator'; this.tryPlaceSelected(TUTORIAL_NODE_SLOTS.generator);
        } else if (name === 'intro-nexus') {
          this.autoBuildMinimumChain();
          this.tutorialStep = TUTORIAL_COMPLETE_STEP;
          this.spawnEnemy('swarm', 0);
          const enemy = this.enemies[0];
          if (enemy) this.leakEnemy(enemy);
          this.updateUi(true);
        } else if (name === 'stage-two-wave-three') this.createStageTwoLessonDemo(2);
        else if (name === 'stage-two-wave-four') this.createStageTwoLessonDemo(3);
        else if (name === 'skill-feedback' || name === 'soul-field-damage-demo') this.createSkillFeedbackDemo();
        else if (name === 'element-models') this.createElementModelDemo();
        else if (name === 'victory-travel') this.createVictoryTravelDemo(false);
        else if (name === 'victory-transition') this.createVictoryTravelDemo(true);
        else if (name === 'dual-terminal-network') this.createDualTerminalNetworkDemo();
        else if (name === 'reaction-cooldown') this.createReactionCooldownDemo();
        else if (name === 'chain-complete-notice') this.autoBuildMinimumChain(false);
        else if (name === 'broken-chain-labels') this.createBrokenChainLabelDemo();
      },
      autoBuildMinimumChain: () => this.autoBuildMinimumChain(),
      startWave: () => this.startWave(),
      dismissReactionTutorial: () => this.dismissReactionTutorial(),
      procTestReaction: (reaction: ReactionKey) => {
        const enemy = this.enemies.find((candidate) => !candidate.dead);
        if (enemy && REACTIONS[reaction]) this.procReaction(reaction, 1, enemy, enemy.group.position.clone());
        this.publishDiagnostics();
      },
      setSoul: (value: number) => { this.soul = clamp(value, 0, MAX_SOUL); this.updateUi(true); },
      creditRainChargeHits: (hitCount: number) => { this.creditRainCharge(hitCount); this.updateUi(true); },
      setElementStatusDemo: (element: Element) => {
        this.resetRun(); this.tutorialStep = TUTORIAL_COMPLETE_STEP; this.spawnEnemy('swarm', 0);
        const enemy = this.enemies[0]; if (enemy) { enemy.progress = 4; enemy.group.position.set(-3, 0.2, -2); this.applyBaseStatus(element, enemy); }
        this.updateEnemyStatusPresentation(); this.updateUi(true); this.publishDiagnostics();
      },
      setLinkDragPointerWorld: (sourceType: string, x: number, z: number) => {
        const source = this.nodeByType(sourceType as PurchasableNodeType);
        if (!source) return;
        const sourcePoint = this.worldToClient(this.nodeAnchor(source));
        if (!sourcePoint) return;
        this.selectNode(source.id);
        this.beginLinkDrag(source.id, -1, sourcePoint.x, sourcePoint.y);
        this.linkHoverTargetId = null;
        this.linkPointerWorld = new THREE.Vector3(x, this.nodeAnchor(source).y, z);
        this.refreshLinkHints();
        this.publishDiagnostics();
      },
      advance: (seconds: number) => {
        const steps = Math.min(60 * 180, Math.max(0, Math.ceil(seconds / FIXED_STEP)));
        this.suppressVictoryNavigation = true;
        for (let index = 0; index < steps; index += 1) {
          let advancedPresentation = false;
          if (this.chainCompletionNotice) {
            this.updateChainCompletionNotice(FIXED_STEP);
            advancedPresentation = true;
          }
          if (this.phase === 'wave') {
            this.simulate(FIXED_STEP);
            this.updateReactionTutorial(FIXED_STEP);
          } else if (this.phase === 'victoryTravel') this.updateVictoryTravel(FIXED_STEP);
          else if (!advancedPresentation) break;
        }
        this.suppressVictoryNavigation = false;
        this.updateEnemyStatusPresentation();
        this.updateUi(true);
        this.publishDiagnostics();
      },
      getSlotClientPoint: (slotId: number) => {
        const slot = this.slots.get(slotId);
        return slot ? this.worldToClient(slot.mesh.position) : null;
      },
      getGridCellIdAt: (x: number, z: number, tier: 'low' | 'high' = 'low') => {
        const slot = [...this.slots.values()].find((candidate) => {
          const definition = BUILD_SLOTS[candidate.id];
          return definition?.tier === tier && Math.abs(candidate.mesh.position.x - x) < 1e-6 && Math.abs(candidate.mesh.position.z - z) < 1e-6;
        });
        return slot?.id ?? null;
      },
      getNodeClientPoint: (type: string, slotId?: number) => {
        const node = [...this.nodes.values()].find((candidate) => candidate.type === type && (slotId === undefined || candidate.slotId === slotId));
        return node ? this.worldToClient(node.group.position.clone().add(new THREE.Vector3(0, 1, 0))) : null;
      },
      getSoulSkillTargetClientPoint: () => {
        const target = this.soulTutorialTargetPosition();
        return target ? this.worldToClient(target) : null;
      },
    };
  }

  private autoBuildMinimumChain(suppressCompletionNotice = true): void {
    this.resetRun();
    this.tutorialStep = TUTORIAL_COMPLETE_STEP;
    const placements: Array<[PurchasableNodeType, number]> = ACTIVE_STAGE_INDEX === 0
      ? [
          ['nexus', TUTORIAL_NODE_SLOTS.nexus], ['generator', TUTORIAL_NODE_SLOTS.generator],
          ['fire', TUTORIAL_NODE_SLOTS.fire], ['ice', TUTORIAL_NODE_SLOTS.ice],
        ]
      : (() => {
          const types: PurchasableNodeType[] = ['generator', 'fire', 'ice', 'nexus'];
          const available = [...this.slots.values()].sort((a, b) => {
            const laneDelta = this.distanceToEnemyPath(a.mesh.position.x, a.mesh.position.z)
              - this.distanceToEnemyPath(b.mesh.position.x, b.mesh.position.z);
            return Math.abs(laneDelta) > 0.01 ? laneDelta : a.id - b.id;
          });
          const chosen: Array<[PurchasableNodeType, number]> = [];
          const search = (index: number): boolean => {
            if (index >= types.length) return true;
            const type = types[index];
            for (const slot of available) {
              if (chosen.some((entry) => entry[1] === slot.id)) continue;
              const previous = chosen[index - 1];
              if (previous) {
                const previousSlot = this.slots.get(previous[1]);
                const range = Math.min(MAX_LINK_RANGE, NODE_DEFINITIONS[previous[0]].connectionRange);
                if (!previousSlot || previousSlot.mesh.position.distanceTo(slot.mesh.position) > range + 1e-6) continue;
              }
              chosen.push([type, slot.id]);
              if (search(index + 1)) return true;
              chosen.pop();
            }
            return false;
          };
          if (!search(0)) throw new Error('Không tìm được chuỗi kiểm thử trên grid hiện tại.');
          return chosen;
        })();
    if (ACTIVE_STAGE_INDEX > 0) this.gold = Number.MAX_SAFE_INTEGER;
    for (const [type, slot] of placements) { this.selectedBuildType = type; this.tryPlaceSelected(slot); }
    if (ACTIVE_STAGE_INDEX > 0) this.gold = STARTING_GOLD;
    const generator = this.nodeByType('generator')!;
    const fire = this.nodeByType('fire')!;
    const ice = this.nodeByType('ice')!;
    const nexus = this.nodes.get(this.nexusNodeId!)!;
    this.connectNodes(generator.id, fire.id);
    this.connectNodes(fire.id, ice.id);
    this.connectNodes(ice.id, nexus.id);
    if (suppressCompletionNotice) this.clearChainCompletionNotice();
    this.tutorialStep = TUTORIAL_COMPLETE_STEP;
    this.updateUi(true);
  }

  private createBrokenChainLabelDemo(): void {
    this.autoBuildMinimumChain();
    const fire = this.nodeByType('fire');
    if (fire) this.unlinkOutput(fire.id);
    this.validateNetwork();
    this.refreshLinks();
    this.refreshSelection();
    this.updateUi(true);
  }

  private createSkillFeedbackDemo(): void {
    this.resetRun(); this.tutorialStep = TUTORIAL_COMPLETE_STEP; this.phase = 'wave';
    this.spawnIndex = WAVES[0].orders.length;
    const centerProgress = 5;
    [-0.65, 0, 0.65].forEach((offset) => {
      this.spawnEnemy('armored', offset);
      const enemy = this.enemies[this.enemies.length - 1];
      enemy.progress = centerProgress + offset * 0.3;
      const transform = this.pathTransform(enemy.progress, enemy.sideOffset, 0);
      enemy.group.position.copy(transform.position); enemy.group.rotation.y = transform.rotation;
    });
    this.spawnEnemy('warded', 0);
    const corridorEnemy = this.enemies[this.enemies.length - 1];
    corridorEnemy.progress = 9.5;
    const corridorTransform = this.pathTransform(corridorEnemy.progress, 0, 0);
    corridorEnemy.group.position.copy(corridorTransform.position); corridorEnemy.group.rotation.y = corridorTransform.rotation;
    const center = this.pathTransform(centerProgress, 0, 0).position;
    this.soul = MAX_SOUL;
    this.beginSoulTargeting();
    this.castTongueStrike(center);
    this.updateEnemyStatusPresentation(); this.updateUi(true);
  }

  private createElementModelDemo(): void {
    this.resetRun();
    this.tutorialStep = TUTORIAL_COMPLETE_STEP;
    this.gold = Number.MAX_SAFE_INTEGER;
    const types: PurchasableNodeType[] = ['fire', 'ice', 'wind', 'earth'];
    const slots = [...this.slots.values()]
      .filter((slot) => this.distanceToEnemyPath(slot.mesh.position.x, slot.mesh.position.z) < 5)
      .sort((a, b) => a.id - b.id)
      .slice(0, types.length);
    types.forEach((type, index) => {
      const slot = slots[index];
      if (!slot) return;
      this.selectedBuildType = type;
      this.tryPlaceSelected(slot.id);
    });
    this.gold = STARTING_GOLD;
    this.selectedBuildType = null;
    this.selectedNodeId = null;
    this.refreshSelection();
    this.updateUi(true);
  }

  private createVictoryTravelDemo(nearExit: boolean): void {
    this.resetRun();
    this.tutorialStep = TUTORIAL_COMPLETE_STEP;
    this.waveIndex = WAVES.length;
    this.beginVictoryTravel();
    if (nearExit && this.victoryTravel) {
      this.victoryTravel.distance = Math.max(0, this.victoryTravel.totalLength - 0.35);
      this.updateVictoryTravel(0);
    }
  }

  private createStageTwoLessonDemo(waveIndex: 2 | 3): void {
    if (ACTIVE_STAGE_INDEX !== 1) return;
    this.resetRun();
    this.tutorialStep = TUTORIAL_COMPLETE_STEP;
    this.gold = Number.MAX_SAFE_INTEGER;
    const placements: Array<[PurchasableNodeType, number]> = [
      ['generator', buildSlotIdAt(-11, -8)],
      ['fire', buildSlotIdAt(-9, -8)],
      ['ice', buildSlotIdAt(-7, -8)],
      ['nexus', buildSlotIdAt(-1, -8)],
    ];
    const placed: NodeState[] = [];
    for (const [type, slotId] of placements) {
      this.selectedBuildType = type;
      if (!this.tryPlaceSelected(slotId) || this.selectedNodeId === null) return;
      const node = this.nodes.get(this.selectedNodeId);
      if (node) placed.push(node);
    }
    for (let index = 0; index < placed.length - 1; index += 1) this.connectNodes(placed[index].id, placed[index + 1].id);
    if (waveIndex >= 3) {
      this.waveIndex = 2;
      const supportSlot = this.findStageTwoLessonSlot('support');
      if (supportSlot !== null) {
        this.selectedBuildType = 'support'; this.tryPlaceSelected(supportSlot);
        let pair = this.stageTwoRequiredLinkPair();
        for (let linkIndex = 0; pair && linkIndex < 2; linkIndex += 1) {
          this.connectNodes(pair.source.id, pair.target.id);
          pair = this.stageTwoRequiredLinkPair();
        }
      }
    }
    this.waveIndex = waveIndex;
    this.gold = waveIndex === 2 ? 45 : 55;
    this.selectedBuildType = null;
    this.selectedNodeId = null;
    this.clearLinkDrag();
    this.validateNetwork();
    this.refreshLinks();
    this.refreshSelection();
    this.renderWaveRoster();
    this.updateUi(true);
  }

  private createMasteryCheckpointDemo(leaks: number): void {
    this.autoBuildMinimumChain();
    this.tutorialStep = TUTORIAL_COMPLETE_STEP;
    this.tutorialReactionSeen = true;
    this.waveIndex = 3;
    this.gold = MASTERY_CHECKPOINT_GOLD;
    this.baseHp = STARTING_BASE_HP;
    this.soul = MAX_SOUL;
    this.soulSkillTutorial = 'button';
    this.captureMasteryCheckpoint();
    this.selectedNodeId = null;
    this.refreshSelection();
    this.renderWaveRoster();
    this.baseHp = Math.max(0, this.baseHp - Math.max(0, Math.floor(leaks)));
    if (this.baseHp <= 0) this.endRun(false);
    else this.updateUi(true);
  }

  private createDualTerminalNetworkDemo(): void {
    this.autoBuildMinimumChain();
    if (!ACTIVE_STAGE.tutorial || this.nexusNodeId === null) return;
    this.gold = Number.MAX_SAFE_INTEGER;
    this.waveIndex = 3;
    const secondGeneratorSlot = buildSlotIdAt(3, -4);
    this.selectedBuildType = 'generator';
    if (this.tryPlaceSelected(secondGeneratorSlot) && this.selectedNodeId !== null) {
      this.connectNodes(this.selectedNodeId, this.nexusNodeId);
    }
    this.gold = 0;
    this.selectedBuildType = null;
    this.selectedNodeId = null;
    this.validateNetwork();
    this.refreshLinks();
    this.refreshSelection();
    this.renderWaveRoster();
    this.updateUi(true);
  }

  private createReactionCooldownDemo(): void {
    this.resetRun();
    this.tutorialStep = TUTORIAL_COMPLETE_STEP;
    this.tutorialReactionSeen = true;
    this.waveIndex = 3;
    this.phase = 'wave';
    this.spawnIndex = WAVES[this.waveIndex].orders.length;
    this.spawnEnemy('swarm', 0);
    const enemy = this.enemies[0];
    if (!enemy) return;
    enemy.progress = 4;
    const transform = this.pathTransform(enemy.progress, enemy.sideOffset, 0);
    enemy.group.position.copy(transform.position); enemy.group.rotation.y = transform.rotation;
    this.procReaction('tempest', 1, enemy, enemy.group.position.clone());
    this.procReaction('tempest', 1, enemy, enemy.group.position.clone());
    this.procReaction('shatter', 1, enemy, enemy.group.position.clone());
    this.updateUi(true);
  }

  private createMasteryFinalDemo(expanded: boolean): void {
    this.createMasteryCheckpointDemo(0);
    if (expanded) {
      this.gold = Number.MAX_SAFE_INTEGER;
      const placements: Array<[PurchasableNodeType, number]> = [
        ['generator', buildSlotIdAt(-9, 0)],
        ['ice', buildSlotIdAt(-7, -4)],
        ['fire', buildSlotIdAt(1, 0)],
        ['ice', buildSlotIdAt(-7, 4)],
        ['fire', buildSlotIdAt(3, 0)],
      ];
      const placed: NodeState[] = [];
      for (const [type, slotId] of placements) {
        this.selectedBuildType = type;
        if (this.tryPlaceSelected(slotId) && this.selectedNodeId !== null) {
          const node = this.nodes.get(this.selectedNodeId);
          if (node) placed.push(node);
        }
      }
      const nexus = this.nexusNodeId === null ? null : this.nodes.get(this.nexusNodeId);
      for (let index = 0; index < placed.length - 1; index += 1) this.connectNodes(placed[index].id, placed[index + 1].id);
      const terminal = placed[placed.length - 1];
      if (terminal && nexus) this.connectNodes(terminal.id, nexus.id);
      this.gold = 15;
    }
    this.waveIndex = 5;
    this.baseHp = STARTING_BASE_HP;
    this.selectedNodeId = null;
    this.validateNetwork();
    this.refreshLinks();
    this.refreshSelection();
    this.renderWaveRoster();
    this.updateUi(true);
    this.startWave();
    if (expanded) {
      for (let step = 0; step < 180 && this.phase === 'wave'; step += 1) this.simulate(FIXED_STEP);
      this.soul = MAX_SOUL;
      this.beginSoulTargeting();
      this.castTongueStrike(this.pathTransform(4.4, 0, 0).position);
    }
  }

  private nodeNetworkPresentation(node: NodeState): Record<string, unknown> {
    let materialCount = 0;
    let colorRatioTotal = 0;
    let emissiveRatioTotal = 0;
    let emissiveRatioCount = 0;
    let intensityRatioTotal = 0;
    let intensityRatioCount = 0;
    node.group.traverse((object) => {
      if (!(object instanceof THREE.Mesh)) return;
      const materials = Array.isArray(object.material) ? object.material : [object.material];
      materials.forEach((material) => {
        if (!(material instanceof THREE.MeshStandardMaterial)) return;
        const baseColor = material.userData.networkBaseColor as THREE.Color | undefined;
        const baseEmissive = material.userData.networkBaseEmissive as THREE.Color | undefined;
        const baseIntensity = material.userData.networkBaseEmissiveIntensity as number | undefined;
        if (!baseColor || !baseEmissive || baseIntensity === undefined) return;
        const baseColorTotal = baseColor.r + baseColor.g + baseColor.b;
        if (baseColorTotal > 1e-6) colorRatioTotal += (material.color.r + material.color.g + material.color.b) / baseColorTotal;
        const baseEmissiveTotal = baseEmissive.r + baseEmissive.g + baseEmissive.b;
        if (baseEmissiveTotal > 1e-6) {
          emissiveRatioTotal += (material.emissive.r + material.emissive.g + material.emissive.b) / baseEmissiveTotal;
          emissiveRatioCount += 1;
        }
        if (baseIntensity > 1e-6) {
          intensityRatioTotal += material.emissiveIntensity / baseIntensity;
          intensityRatioCount += 1;
        }
        materialCount += 1;
      });
    });
    return {
      state: node.group.userData.networkVisualState ?? null,
      reason: node.group.userData.networkVisualReason ?? null,
      materialCount,
      colorRatio: materialCount > 0 ? colorRatioTotal / materialCount : null,
      emissiveRatio: emissiveRatioCount > 0 ? emissiveRatioTotal / emissiveRatioCount : null,
      emissiveIntensityRatio: intensityRatioCount > 0 ? intensityRatioTotal / intensityRatioCount : null,
    };
  }

  private snapshot(): Record<string, unknown> {
    const waveSpawnWindows = WAVES.map((wave) => {
      if (wave.orders.length < 2) return 0;
      return Math.max(0, wave.orders[wave.orders.length - 1].at - wave.orders[0].at);
    });
    const waveSpawnDensities = WAVES.map((wave, index) => wave.orders.length / Math.max(0.1, waveSpawnWindows[index]));
    const waveThreats = WAVES.map((wave) => wave.orders.reduce(
      (sum, order) => sum + ENEMY_DEFINITIONS[order.kind].hp * wave.healthMultiplier,
      0,
    ));
    const pathSegmentLengths = ENEMY_PATH.slice(1).map(([x, z], index) => Math.hypot(x - ENEMY_PATH[index][0], z - ENEMY_PATH[index][1]));
    const linkDragSource = this.linkSourceId !== null ? this.nodes.get(this.linkSourceId) : null;
    const linkPreviewLength = linkDragSource && this.linkPointerWorld
      ? this.nodeAnchor(linkDragSource).distanceTo(this.linkPointerWorld)
      : 0;
    const requiredStageTwoNode = this.stageTwoRequiredNode();
    const stageTwoLessonSlotId = requiredStageTwoNode ? this.stageTwoRequiredSlot(requiredStageTwoNode) : null;
    const stageTwoLessonSlot = stageTwoLessonSlotId === null ? null : this.slots.get(stageTwoLessonSlotId) ?? null;
    const pathClods = this.worldGroup.getObjectByName('enemy-path-edge-clods');
    const battlefieldDecoration = this.worldGroup.getObjectByName('battlefield-drought-decoration');
    const outerDecoration = this.worldGroup.getObjectByName('outer-drought-decoration');
    const frogPosition = this.frogActor.getWorldPosition(new THREE.Vector3());
    const tongueProfile = this.tongueProfile();
    let activeProjectileMeshes = 0;
    let activeProjectileSprites = 0;
    this.projectiles.forEach((projectile) => projectile.group.traverse((object) => {
      if (object instanceof THREE.Mesh) activeProjectileMeshes += 1;
      if (object instanceof THREE.Sprite) activeProjectileSprites += 1;
    }));
    const completionNotice = this.chainCompletionNotice;
    const completionSecondPassStart = completionNotice
      ? completionNotice.passDuration + completionNotice.gapDuration
      : 0;
    const completionInGap = completionNotice !== null
      && completionNotice.elapsed > completionNotice.passDuration
      && completionNotice.elapsed < completionSecondPassStart;
    const completionPass = completionNotice === null
      ? 0
      : completionNotice.elapsed <= completionNotice.passDuration ? 1 : completionInGap ? 0 : 2;
    const completionProgress = completionNotice === null || completionInGap
      ? 0
      : completionPass === 1
        ? clamp(completionNotice.elapsed / completionNotice.passDuration, 0, 1)
        : clamp((completionNotice.elapsed - completionSecondPassStart) / completionNotice.passDuration, 0, 1);
    return {
      theme: 'coc-kien-troi-drought',
      frame: this.frame, elapsed: this.elapsedTime, phase: this.phase, stageIndex: ACTIVE_STAGE_INDEX, stageTitle: ACTIVE_STAGE.title,
      waveIndex: this.waveIndex, waveClock: this.waveClock, gameSpeed: this.gameSpeed, gold: this.gold, baseHp: this.baseHp, soul: this.soul,
      tutorialStep: this.tutorialStep, tutorialReactionSeen: this.tutorialReactionSeen,
      soulSkillTutorialState: this.soulSkillTutorial,
      tutorialObjective: this.tutorialObjective(), reactionTutorialPopupVisible: this.reactionTutorialVisible,
      tutorialHandMode: this.tutorialHand.classList.contains('hidden') ? 'hidden' : this.tutorialHand.dataset.mode ?? 'hidden',
      requiredStageTwoNode,
      stageTwoLessonComplete: this.stageTwoLessonComplete(),
      stageTwoLessonSlotId,
      stageTwoLessonSlotPosition: stageTwoLessonSlot?.mesh.position.toArray() ?? null,
      stageTwoLessonSlotLaneDistance: stageTwoLessonSlot ? this.distanceToEnemyPath(stageTwoLessonSlot.mesh.position.x, stageTwoLessonSlot.mesh.position.z) : null,
      stageTwoHighGroundPlan: null,
      stageTwoHighGroundGrantedTypes: [],
      stageTwoHighGroundActive: false,
      unlockedNodeTypes: BUILD_ORDER.filter((type) => this.nodeUnlocked(type)),
      buildDrag: { active: this.buildDrag !== null, dragging: this.buildDrag?.dragging ?? false, slotId: this.buildDrag?.slotId ?? null },
      linkDrag: {
        active: this.linkSourceId !== null,
        sourceId: this.linkSourceId,
        hoverTargetId: this.linkHoverTargetId,
        state: this.currentLinkPreviewState(),
        pointerWorld: this.linkPointerWorld?.toArray() ?? null,
        pointerClient: this.linkPointerWorld ? this.worldToClient(this.linkPointerWorld) : null,
        previewLength: linkPreviewLength,
      },
      placementPreviewCount: this.placementPreviewGroup.children.length,
      tutorialChainReminder: {
        visible: !this.tutorialChainReminder.classList.contains('hidden'),
        text: this.tutorialChainReminder.textContent?.trim() ?? '',
      },
      tutorialEndpointBuildLabels: ['generator', 'nexus'].map((type) => ({
        type,
        text: this.buildList.querySelector<HTMLElement>(`.build-card[data-type="${type}"] .build-copy strong`)?.textContent ?? '',
      })),
      tutorialEndpointLabels: this.tutorialLabelGroup.children
        .filter((child) => child.userData.tutorialEndpointRole === 'source' || child.userData.tutorialEndpointRole === 'terminal')
        .map((child) => ({
          name: child.name, role: child.userData.tutorialEndpointRole, label: child.userData.tutorialEndpointLabel,
          connected: child.userData.tutorialEndpointConnected, color: child.userData.tutorialEndpointColor,
          scale: child.children[0]?.scale.x ?? 1,
          position: child.position.toArray(),
        })),
      tutorialEndpointPulse: {
        active: this.tutorialEndpointPulseRemaining > 0,
        remaining: this.tutorialEndpointPulseRemaining,
        duration: TUTORIAL_ENDPOINT_PULSE_DURATION,
        peakScale: TUTORIAL_ENDPOINT_PULSE_PEAK_SCALE,
        currentScale: this.tutorialEndpointPulseScale(),
        transitions: this.tutorialEndpointPulseTransitions,
        direction: this.tutorialEndpointPulseDirection,
        routeComplete: this.tutorialEndpointRouteComplete,
      },
      tutorialEndpointPresentation: {
        rings: this.tutorialLabelGroup.getObjectByName('tutorialEndpointGroundRing') ? 1 : 0,
        glyphs: this.tutorialLabelGroup.children.reduce((sum, marker) => sum
          + (marker.getObjectByName('tutorialSourceGlyph') ? 1 : 0)
          + (marker.getObjectByName('tutorialTerminalGlyph') ? 1 : 0), 0),
        halos: this.tutorialLabelGroup.getObjectByName('tutorialEndpointHalo') ? 1 : 0,
        sockets: this.tutorialLabelGroup.children.reduce((sum, marker) => sum
          + (marker.getObjectByName('tutorialSourceLinkSocket') ? 1 : 0)
          + (marker.getObjectByName('tutorialTerminalLinkSocket') ? 1 : 0), 0),
        linkHalves: 0,
      },
      completedLinkStyle: { color: '#ffffff', opacityActive: 0.42, opacityInactive: 0.18 },
      completedLinkVisuals: this.links.map((link) => {
        const beam = link.group.getObjectByName('completedLinkWhiteBeam') as THREE.Mesh | undefined;
        const material = beam?.material as THREE.MeshBasicMaterial | undefined;
        return {
          sourceId: link.sourceId, targetId: link.targetId,
          color: material ? `#${material.color.getHexString()}` : null,
          opacity: material?.opacity ?? null,
          transparent: material?.transparent ?? null,
          visible: link.group.visible,
        };
      }),
      chainCompletionNotice: {
        active: completionNotice !== null,
        routeNodeIds: completionNotice?.routeNodeIds ?? [],
        passesTotal: 2,
        currentPass: completionPass,
        completedPasses: completionNotice === null ? 0 : completionPass === 2 ? 1 : 0,
        progress: completionProgress,
        ledCount: completionNotice?.ledCores.count ?? 0,
        activeLedCount: completionNotice === null || completionInGap
          ? 0
          : Math.min(completionNotice.ledCores.count, Math.floor(completionProgress * completionNotice.totalLength / 0.36) + 1),
        totalLength: completionNotice?.totalLength ?? 0,
        brightSegmentCount: 0,
        beamOverlayCount: completionNotice?.group.children.filter((child) => child.name === 'chainCompletionBrightSegment').length ?? 0,
      },
      tutorialEndpointLinkHighlights: [],
      gridCellCount: this.slots.size, gridSpacing: BUILD_GRID_SPACING,
      highGroundPlatformCount: HIGH_GROUND_PLATFORMS.length,
      highGroundSlots: BUILD_SLOTS.filter((slot) => slot.tier === 'high').map((slot) => ({ id: slot.id, position: slot.position })),
      pathPoints: ENEMY_PATH.map(([x, z]) => ({ x, z })), pathSegmentLengths,
      pathVisual: {
        layers: ['shoulder', 'raised-edge', 'textured-surface'],
        edgeClodCount: pathClods instanceof THREE.InstancedMesh ? pathClods.count : 0,
        texture: 'dust-ruts-footprints-pebbles',
        spawnDirectionMarkerCount: this.worldGroup.getObjectByName('spawnDirection') ? 1 : 0,
      },
      battlefieldDecoration: battlefieldDecoration?.userData.counts ?? null,
      perimeterDecoration: outerDecoration?.userData.counts ?? null,
      maxHorizontalPathSegment: ENEMY_PATH.slice(1).reduce((max, [x, z], index) => z === ENEMY_PATH[index][1] ? Math.max(max, Math.abs(x - ENEMY_PATH[index][0])) : max, 0),
      maxVerticalPathSegment: ENEMY_PATH.slice(1).reduce((max, [x, z], index) => x === ENEMY_PATH[index][0] ? Math.max(max, Math.abs(z - ENEMY_PATH[index][1])) : max, 0),
      nodeCount: this.nodes.size, projectileCount: this.projectiles.length, enemyCount: this.enemies.length,
      projectilePresentation: {
        core: 'billboard-glow-sprite', trail: 'additive-glow-points', trailSamples: 9, uses3dModel: false,
        activeMeshes: activeProjectileMeshes, activeSprites: activeProjectileSprites,
        activeTrails: this.projectiles.filter((projectile) => projectile.trail instanceof THREE.Points).length,
      },
      selectedNodeId: this.selectedNodeId,
      selectedNetworkNodeIds: this.selectedNodeId === null ? [] : [...this.connectedComponent(new Set([this.selectedNodeId]))],
      activeChains: [...this.nodes.values()].filter((node) => node.type === 'generator' && node.active).length,
      links: [...this.nodes.values()].filter((node) => node.outputTargetId !== null).map((node) => ({ sourceId: node.id, targetId: node.outputTargetId })),
      visibleCompletedLinks: this.links.filter((link) => link.group.visible).map((link) => ({ sourceId: link.sourceId, targetId: link.targetId })),
      nodes: [...this.nodes.values()].map((node) => ({ id: node.id, type: node.type, slotId: node.slotId, position: node.group.position.toArray(), active: node.active, reason: node.invalidReason, input: node.inputSourceId, nexusInputs: [...node.nexusInputSourceIds], output: node.outputTargetId, queue: node.buffer.length, reserved: node.reservedIncoming, charge: node.charge, pulseCharge: node.pulseCharge, branch: node.branch, totalInvested: node.totalInvested, lessonGrant: node.group.userData.lessonGrant === true, launches: this.projectileLaunchesByNode.get(node.id) ?? 0, networkVisual: this.nodeNetworkPresentation(node) })),
      directHits: this.directHits, layerOneEnemyHits: this.layerOneEnemyHits, reactionProcs: this.reactionProcs, blockedReactionProcs: this.blockedReactionProcs, specialPulses: this.specialPulses,
      reactionBalance: {
        repeatCooldown: REACTION_REPEAT_COOLDOWN,
        baseWindProgressRewind: 0,
        tempestProgressRewind: { regular: 0.8, boss: 0.35 },
        activeEnemyCooldowns: this.enemies.filter((enemy) => !enemy.dead).map((enemy) => ({ id: enemy.id, cooldowns: { ...enemy.reactionCooldowns } })),
      },
      activeEnemies: this.enemies.filter((enemy) => !enemy.dead).map((enemy) => ({ id: enemy.id, kind: enemy.kind, progress: enemy.progress, windTime: enemy.windTime })),
      activeVfxCount: this.vfx.length,
      statusIcons: [...this.statusIconMeshes.values()].reduce((sum, mesh) => sum + mesh.count, 0),
      soulCasts: this.soulCasts,
      tongueSkill: {
        active: this.tongueStrike !== null,
        phase: this.tongueStrike ? (this.tongueStrike.elapsed < this.tongueStrike.outbound ? 'outbound'
          : this.tongueStrike.elapsed < this.tongueStrike.outbound + this.tongueStrike.hold ? 'impact' : 'retract') : 'idle',
        branch: tongueProfile.branch, radius: tongueProfile.radius, flatDamage: tongueProfile.flatDamage,
        maxHpRatio: tongueProfile.maxHpRatio, maxHpCap: tongueProfile.maxHpCap,
        corridorDamageRatio: 0.2, outboundDuration: 0.16, holdDuration: 0.08, retractDuration: 0.28,
        corridorHits: this.tongueCorridorHits, impactHits: this.tongueImpactHits,
        capturedKills: this.tongueCapturedKills, carrying: this.tongueStrike?.captured.length ?? 0,
        origin: this.tongueStrike?.start.toArray() ?? null,
        target: this.tongueStrike?.target.toArray() ?? null,
        visualLength: this.tongueStrike?.core.scale.y ?? 0,
        targetDistance: this.tongueStrike?.start.distanceTo(this.tongueStrike.target) ?? 0,
        presentation: {
          modelType: 'solid-3d-tapered', bodyGeometry: 'tapered-cylinder', bodyMaterial: 'MeshStandardMaterial',
          rootRadius: TONGUE_BODY_ROOT_RADIUS, bodyTipRadius: TONGUE_BODY_TIP_RADIUS,
          tipGeometry: 'SphereGeometry', tipRadius: TONGUE_TIP_RADIUS,
          tipHighlight: true, capturedEnemyScale: TONGUE_CAPTURE_SCALE,
          usesGlow: false, glowMeshCount: this.effectGroup.getObjectByName('tongueGlow') ? 1 : 0,
          impactDiscOpacity: TONGUE_IMPACT_DISC_OPACITY,
          dirtParticleCount: TONGUE_DIRT_PARTICLE_COUNT,
          activeDirtParticles: this.effectGroup.getObjectByName('tongueDirtBurst')?.children.length ?? 0,
        },
        cameraShakeRemaining: this.cameraShakeRemaining,
      },
      soulSkillDrag: {
        active: this.soulSkillDrag !== null, hasPreview: this.soulTargetPreview !== null,
        point: this.soulSkillDrag?.point?.toArray() ?? null,
        previewParts: this.soulTargetPreview?.children.map((child) => child.name).filter(Boolean) ?? [],
      },
      killedEnemies: this.killedEnemies, leakedEnemies: this.leakedEnemies,
      fixedNexus: {
        kind: 'frog',
        position: this.baseNexus.position.toArray(),
        mouthPosition: this.frogMouthWorldPosition().toArray(),
        visible: this.baseNexus.visible,
        coreVisible: this.baseNexus.getObjectByName('baseNexusCore')?.visible ?? false,
        separateFromSoulAnchor: this.baseNexus !== (this.nexusNodeId === null ? null : this.nodes.get(this.nexusNodeId)?.group),
      },
      elementalTowerModels: [...this.nodes.values()]
        .filter((node) => node.type === 'fire' || node.type === 'ice' || node.type === 'wind' || node.type === 'earth')
        .map((node) => ({ type: node.type, model: node.group.userData.elementModel ?? null })),
      towerModelProfiles: [...this.nodes.values()]
        .filter((node) => node.type === 'generator' || node.type === 'fire' || node.type === 'ice' || node.type === 'wind' || node.type === 'earth')
        .map((node) => {
          const bounds = new THREE.Box3().setFromObject(node.group);
          const size = bounds.getSize(new THREE.Vector3());
          const namedParts: string[] = [];
          node.group.traverse((object) => { if (object.name) namedParts.push(object.name); });
          return { type: node.type, profile: node.group.userData.modelProfile ?? null, size: size.toArray(), namedParts };
        }),
      victoryTravel: {
        active: this.phase === 'victoryTravel',
        progress: this.victoryTravel ? this.victoryTravel.distance / Math.max(0.001, this.victoryTravel.totalLength) : 0,
        hopHeight: this.victoryTravel?.hopHeight ?? 0,
        maxHopHeight: this.victoryTravel?.maxHopHeight ?? 0,
        frogPosition: frogPosition.toArray(),
        destination: ENEMY_PATH[0],
        fadeRemaining: this.victoryTravel?.fadeRemaining ?? null,
        navigationStarted: this.victoryTravel?.navigationStarted ?? false,
        nextStage: ACTIVE_STAGE_INDEX + 1 < STAGES.length ? ACTIVE_STAGE_INDEX + 2 : null,
      },
      currencyTutorialSeen: this.currencyTutorialSeen, currencyHighlightActive: this.currencyHighlightTime > 0,
      baseTutorialSeen: this.baseTutorialSeen, baseHighlightActive: this.baseHighlightTime > 0,
      waveCount: WAVES.length,
      waveEnemyCounts: WAVES.map((wave) => wave.orders.length),
      waveLayerOneEnemyCounts: WAVES.map((wave) => wave.orders.filter((order) => ENEMY_DEFINITIONS[order.kind].layer === 1).length),
      waveHealthMultipliers: WAVES.map((wave) => wave.healthMultiplier),
      waveSpawnWindows, waveSpawnDensities, waveThreats,
      tutorialMasteryPhase: this.isTutorialMasteryPhase(),
      masteryCheckpointCaptured: this.masteryCheckpoint !== null,
      masteryCheckpointMoney: this.masteryCheckpoint?.gold ?? null,
      tutorialStartingLives: STARTING_BASE_HP,
      tutorialLeakDamage: 1,
      masteryWaveCounts: WAVES.slice(3, 6).map((wave) => wave.orders.length),
      masteryWaveHealthMultipliers: WAVES.slice(3, 6).map((wave) => wave.healthMultiplier),
      masteryWaveSpawnDensities: waveSpawnDensities.slice(3, 6),
      masteryWaveThreats: waveThreats.slice(3, 6),
      nodePurchasePriceMultiplier: this.nodePurchasePriceMultiplier(),
      nodePurchasePrices: Object.fromEntries((Object.keys(NODE_DEFINITIONS) as PurchasableNodeType[]).map((type) => [type, this.currentNodePrice(type)])),
      camera: {
        distance: this.cameraDistance,
        target: this.cameraTarget.toArray(),
        yaw: this.cameraYaw,
        pitch: this.cameraPitch,
        minPitch: CAMERA_MIN_PITCH,
        maxPitch: CAMERA_MAX_PITCH,
        orbitEnabled: true,
        orbiting: this.orbitingPointerId !== null || this.activePointers.size >= 2,
      },
      balance: {
        projectileSpeed: PROJECTILE_SPEED, projectileRadius: PROJECTILE_RADIUS, projectileVisualScale: PROJECTILE_VISUAL_SCALE,
        towerFireRateMultiplier: TOWER_FIRE_RATE_MULTIPLIER, enemySpeedMultiplier: ENEMY_SPEED_MULTIPLIER,
        startingGold: STARTING_GOLD, startingBaseHp: STARTING_BASE_HP, sellRefund: SELL_REFUND,
        enemyRewardMultiplier: ENEMY_REWARD_MULTIPLIER, stageKillRewardMultiplier: ACTIVE_STAGE.killRewardMultiplier, waveClearRewardMultiplier: WAVE_CLEAR_REWARD_MULTIPLIER,
        rainChargeMultiplier: ACTIVE_STAGE.rainChargeMultiplier,
        networkTowerPresentation: {
          dimColorMultiplier: NODE_DIM_COLOR_MULTIPLIER,
          dimEmissiveMultiplier: NODE_DIM_EMISSIVE_MULTIPLIER,
          dimEmissiveIntensityMultiplier: NODE_DIM_EMISSIVE_INTENSITY_MULTIPLIER,
        },
        purchasePriceGrowthPerTower: TOWER_PURCHASE_PRICE_GROWTH_PER_TOWER, purchasePriceGrowthCap: TOWER_PURCHASE_PRICE_GROWTH_CAP,
        nodes: Object.fromEntries(Object.entries(NODE_DEFINITIONS).map(([type, definition]) => [type, {
          cost: definition.cost, upgradeCost: definition.upgradeCost, interval: definition.interval,
          capacity: definition.capacity, connectionRange: definition.connectionRange,
        }])),
        enemies: Object.fromEntries(Object.entries(ENEMY_DEFINITIONS).map(([kind, definition]) => [kind, {
          hp: definition.hp, speed: definition.speed, radius: definition.radius, reward: definition.reward, layer: definition.layer,
        }])),
      },
      renderer: { calls: this.renderer.info.render.calls, triangles: this.renderer.info.render.triangles, geometries: this.renderer.info.memory.geometries, textures: this.renderer.info.memory.textures },
    };
  }

  private publishDiagnostics(): void { window.__THREE_GAME_DIAGNOSTICS__ = this.snapshot(); }
}
