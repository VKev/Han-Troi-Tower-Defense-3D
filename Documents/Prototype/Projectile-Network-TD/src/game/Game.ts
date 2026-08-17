import * as THREE from 'three';
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
  REACTION_MAX_HP_DAMAGE_RATIO, SELL_REFUND, STARTING_BASE_HP, STARTING_GOLD,
  TOWER_FIRE_RATE_MULTIPLIER, TOWER_PURCHASE_PRICE_GROWTH_CAP, TOWER_PURCHASE_PRICE_GROWTH_PER_TOWER,
  WAVE_CLEAR_REWARD_MULTIPLIER, WAVES, buildSlotIdAt, nodeCapacity, nodeInterval, resolveReaction,
  type Branch, type Element, type EnemyKind, type EnemyState, type GamePhase,
  type NodeState, type NodeType, type Payload, type ProjectileState,
  type PurchasableNodeType, type SoulField,
} from './definitions';

interface SlotVisual { id: number; mesh: THREE.Group; occupiedNodeId: number | null; }
interface LinkVisual { sourceId: number; targetId: number; group: THREE.Group; }
interface VfxState { group: THREE.Group; remaining: number; duration: number; rise: number; }
interface Obstacle { box: THREE.Box3; group: THREE.Group; }
interface CameraPointer { x: number; y: number; }
interface BuildDragState {
  pointerId: number; type: PurchasableNodeType; button: HTMLButtonElement;
  origin: THREE.Vector2; dragging: boolean; slotId: number | null; valid: boolean; reason: string;
}
interface SoulSkillDragState { pointerId: number; point: THREE.Vector3 | null; }
type StageTwoLessonType = 'support' | 'special';
type StageTwoHighGroundType = 'generator' | 'fire' | 'ice';

interface StageTwoHighGroundPlan {
  readonly slots: Readonly<Record<StageTwoHighGroundType, number>>;
  readonly crossingDistance: number;
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
  readonly nextEnemyId: number; readonly nextFieldId: number;
  readonly currencyTutorialSeen: boolean; readonly baseTutorialSeen: boolean;
}

const TUTORIAL_NODE_SLOTS = {
  nexus: ACTIVE_STAGE.tutorial ? buildSlotIdAt(-1, 4) : BUILD_SLOTS[0].id,
  generator: ACTIVE_STAGE.tutorial ? buildSlotIdAt(-5, -4) : BUILD_SLOTS[1].id,
  fire: ACTIVE_STAGE.tutorial ? buildSlotIdAt(-5, 0) : BUILD_SLOTS[2].id,
  ice: ACTIVE_STAGE.tutorial ? buildSlotIdAt(-5, -6) : BUILD_SLOTS[3].id,
} as const;
const TUTORIAL_TYPES: Partial<Record<number, PurchasableNodeType>> = { 0: 'nexus', 1: 'generator', 4: 'fire', 8: 'ice' };
const TUTORIAL_PLACEMENT_SLOTS: Partial<Record<number, number>> = {
  0: TUTORIAL_NODE_SLOTS.nexus, 1: TUTORIAL_NODE_SLOTS.generator,
  4: TUTORIAL_NODE_SLOTS.fire, 8: TUTORIAL_NODE_SLOTS.ice,
};
const TUTORIAL_LINK_STEPS = new Set([2, 5, 6, 9, 10]);
const TUTORIAL_START_STEPS = new Set([3, 7, 11]);
const TUTORIAL_COMPLETE_STEP = 12;
const STAGE_TWO_HIGH_GROUND_TYPES: readonly StageTwoHighGroundType[] = ['generator', 'fire', 'ice'];
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

function clamp(value: number, min: number, max: number): number { return Math.max(min, Math.min(max, value)); }

function pointSegmentDistanceXZ(point: THREE.Vector3, start: THREE.Vector3, end: THREE.Vector3): number {
  const dx = end.x - start.x; const dz = end.z - start.z;
  const lengthSquared = dx * dx + dz * dz;
  const t = lengthSquared <= 1e-8 ? 0 : clamp(((point.x - start.x) * dx + (point.z - start.z) * dz) / lengthSquared, 0, 1);
  return Math.hypot(point.x - (start.x + dx * t), point.z - (start.z + dz * t));
}

function segmentDistanceXZ(a: THREE.Vector3, b: THREE.Vector3, c: THREE.Vector3, d: THREE.Vector3): number {
  const cross = (p: THREE.Vector3, q: THREE.Vector3, r: THREE.Vector3) => (q.x - p.x) * (r.z - p.z) - (q.z - p.z) * (r.x - p.x);
  const abC = cross(a, b, c); const abD = cross(a, b, d);
  const cdA = cross(c, d, a); const cdB = cross(c, d, b);
  const epsilon = 1e-7;
  const touches = (value: number, point: THREE.Vector3, start: THREE.Vector3, end: THREE.Vector3) => Math.abs(value) <= epsilon
    && point.x >= Math.min(start.x, end.x) - epsilon && point.x <= Math.max(start.x, end.x) + epsilon
    && point.z >= Math.min(start.z, end.z) - epsilon && point.z <= Math.max(start.z, end.z) + epsilon;
  if ((abC * abD < 0 && cdA * cdB < 0)
    || touches(abC, c, a, b) || touches(abD, d, a, b) || touches(cdA, a, c, d) || touches(cdB, b, c, d)) return 0;
  return Math.min(
    pointSegmentDistanceXZ(a, c, d), pointSegmentDistanceXZ(b, c, d),
    pointSegmentDistanceXZ(c, a, b), pointSegmentDistanceXZ(d, a, b),
  );
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

function disposeObject(root: THREE.Object3D): void {
  root.traverse((object) => {
    if (!(object instanceof THREE.Mesh || object instanceof THREE.Line || object instanceof THREE.Sprite)) return;
    object.geometry.dispose();
    const materials = Array.isArray(object.material) ? object.material : [object.material];
    materials.forEach((material) => {
      const mapped = material as THREE.Material & { map?: THREE.Texture | null };
      mapped.map?.dispose();
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
  private readonly worldGroup = new THREE.Group();
  private readonly nodeGroup = new THREE.Group();
  private readonly projectileGroup = new THREE.Group();
  private readonly enemyGroup = new THREE.Group();
  private readonly linkGroup = new THREE.Group();
  private readonly selectionGroup = new THREE.Group();
  private readonly slotMarkerGroup = new THREE.Group();
  private readonly placementPreviewGroup = new THREE.Group();
  private readonly statusIconGroup = new THREE.Group();
  private readonly effectGroup = new THREE.Group();
  private readonly slots = new Map<number, SlotVisual>();
  private readonly nodes = new Map<number, NodeState>();
  private readonly projectiles: ProjectileState[] = [];
  private readonly enemies: EnemyState[] = [];
  private readonly links: LinkVisual[] = [];
  private readonly fields: SoulField[] = [];
  private readonly vfx: VfxState[] = [];
  private readonly obstacles: Obstacle[] = [];
  private readonly activePointers = new Map<number, CameraPointer>();
  private readonly statusIconMeshes = new Map<Element, THREE.InstancedMesh>();
  private readonly statusIconBackdrop = this.createStatusIconBackdrop();

  private readonly buildList = this.el('#build-list');
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
  private soulTargeting = false;
  private soulSkillDrag: SoulSkillDragState | null = null;
  private soulTargetPreview: THREE.Group | null = null;
  private soulSkillTutorial: 'button' | 'target' | 'complete' = ACTIVE_STAGE.tutorial ? 'button' : 'complete';
  private nexusNodeId: number | null = null;
  private nextNodeId = 1;
  private nextPayloadId = 1;
  private nextProjectileId = 1;
  private nextEnemyId = 1;
  private nextFieldId = 1;
  private tutorialStep = ACTIVE_STAGE.tutorial ? 0 : TUTORIAL_COMPLETE_STEP;
  private tutorialReactionSeen = false;
  private reactionTutorialDelay = -1;
  private reactionTutorialVisible = false;
  private pendingReaction: keyof typeof REACTIONS | null = null;
  private masteryCheckpoint: MasteryCheckpoint | null = null;
  private currencyTutorialSeen = false;
  private baseTutorialSeen = false;
  private currencyHighlightTime = 0;
  private baseHighlightTime = 0;
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
  private specialPulses = 0;
  private soulCasts = 0;
  private soulFieldDamageTicks = 0;
  private soulFieldDamageEvents = 0;
  private killedEnemies = 0;
  private leakedEnemies = 0;
  private lastToastTimer = 0;
  private readonly stageTwoLessonSlots = new Map<StageTwoLessonType, number>();
  private stageTwoHighGroundPlanCache: StageTwoHighGroundPlan | null = null;

  constructor(private readonly canvas: HTMLCanvasElement) {
    this.renderer = createRenderer(canvas);
    this.scene.add(this.worldGroup, this.linkGroup, this.nodeGroup, this.projectileGroup, this.enemyGroup, this.selectionGroup, this.slotMarkerGroup, this.placementPreviewGroup, this.statusIconGroup, this.effectGroup);
    this.createStatusIconMeshes();
    this.statusIconGroup.add(this.statusIconBackdrop);
    this.createLighting();
    this.createWorld();
    this.createBuildCards();
    this.installUi();
    this.installInput();
    this.updateCamera();
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
    this.audio.dispose();
    this.disposeStatusIconMeshes();
    this.art.dispose();
    this.materials.dispose();
    this.renderer.dispose();
    delete window.__THREE_GAME_DIAGNOSTICS__;
    delete window.__THREE_GAME_TEST_HOOKS__;
  }

  private createLighting(): void {
    this.scene.background = new THREE.Color(0x0b0d16);
    this.scene.fog = new THREE.FogExp2(0x101421, 0.018);
    const hemisphere = new THREE.HemisphereLight(0x908ad6, 0x14251f, 1.65);
    this.scene.add(hemisphere);
    const moon = new THREE.DirectionalLight(0xd7d2ff, 3.4);
    moon.position.set(-12, 26, 16);
    moon.castShadow = true;
    moon.shadow.mapSize.set(1536, 1536);
    const shadowExtent = Math.max(24, ACTIVE_STAGE.board.islandRadius + 3);
    moon.shadow.camera.left = -shadowExtent; moon.shadow.camera.right = shadowExtent;
    moon.shadow.camera.top = shadowExtent; moon.shadow.camera.bottom = -shadowExtent;
    this.scene.add(moon);
    const soulLight = new THREE.PointLight(0xa270ff, 18, 28, 2);
    soulLight.position.set(7, 8, 6);
    this.scene.add(soulLight);
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
  }

  private createPath(): void {
    const border = new THREE.Mesh(this.createPathRibbonGeometry(1.94), this.materials.pathRune);
    border.name = 'enemy-path-border'; border.position.y = 0.04; border.receiveShadow = true;
    const surface = new THREE.Mesh(this.createPathRibbonGeometry(1.66), this.materials.path);
    surface.name = 'enemy-path-surface'; surface.position.y = 0.1; surface.receiveShadow = true;
    this.worldGroup.add(border, surface);

    const [startX, startZ] = ENEMY_PATH[0]; const [nextX, nextZ] = ENEMY_PATH[1];
    const direction = new THREE.Vector2(nextX - startX, nextZ - startZ).normalize();
    const marker = new THREE.Group(); marker.name = 'spawnDirection';
    const arrowMaterial = new THREE.MeshBasicMaterial({ color: 0xff334d, transparent: true, opacity: 0.94, depthTest: false, depthWrite: false, toneMapped: false });
    const shaft = new THREE.Mesh(new THREE.BoxGeometry(1.75, 0.18, 0.56), arrowMaterial);
    shaft.position.x = -0.35; shaft.renderOrder = 30;
    const head = new THREE.Mesh(new THREE.ConeGeometry(0.76, 1.3, 4), arrowMaterial);
    head.position.x = 0.82; head.rotation.z = -Math.PI / 2; head.renderOrder = 30;
    const markerDistance = Math.min(Math.hypot(nextX - startX, nextZ - startZ) * 0.82, 14);
    marker.position.set(startX + direction.x * markerDistance, 0.78, startZ + direction.y * markerDistance);
    marker.rotation.y = Math.atan2(-direction.y, direction.x);
    marker.add(shaft, head); this.worldGroup.add(marker);
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
      positions.push(left.x, 0, left.y, right.x, 0, right.y); normals.push(0, 1, 0, 0, 1, 0); uvs.push(0, distance / 2, 1, distance / 2);
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

  private createWorldProps(): void {
    const positions = [
      [-14, -6], [-14, 3], [-8, 7], [-5, 6.7], [-0.7, -7], [3.7, 7.3], [8.4, 7], [14, -5.6], [14.4, 3.4], [6.5, -7],
    ] as const;
    positions.forEach(([x, z], index) => {
      const group = new THREE.Group();
      group.position.set(x, 0, z);
      const stone = new THREE.Mesh(new THREE.DodecahedronGeometry(0.45 + (index % 3) * 0.12, 0), this.materials.stoneLight);
      stone.position.y = 0.35;
      stone.rotation.set(index * 0.3, index * 0.7, 0);
      group.add(stone);
      // Keep one authored soul beacon as a focal accent. Repeating this mesh on
      // every second prop added four draw calls without improving navigation.
      if (index === 0) {
        const flame = new THREE.Mesh(new THREE.OctahedronGeometry(0.18, 0), this.materials.soul);
        flame.name = 'ambientSoul';
        flame.position.y = 1.05;
        group.add(flame);
      }
      this.worldGroup.add(group);
    });
  }

  private createBuildCards(): void {
    this.buildList.replaceChildren();
    const labels: Array<[string, PurchasableNodeType[]]> = [
      ['TỎA HỒN', ['nexus']], ['NGUỒN', ['generator']], ['NGUYÊN TỐ', ['fire', 'ice', 'wind', 'earth']], ['HỖ TRỢ', ['support', 'special']],
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
        button.innerHTML = `<span class="build-icon" aria-hidden="true">${definition.icon}</span><span class="build-copy"><strong>${definition.shortName}</strong><small>${definition.role}</small></span><span class="build-cost">${definition.cost}</span>`;
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
    if (point) this.castSoulField(point);
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
      if (this.phase === 'preparation' && this.selectedNodeId === nodeId && this.nodes.get(nodeId)?.type !== 'nexus') {
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
    if (this.soulTargeting && this.phase === 'wave') {
      const point = this.groundPoint(event.clientX, event.clientY);
      if (point) { this.castSoulField(point); return; }
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

  private isStageTwoHighGroundLessonWave(): boolean {
    return ACTIVE_STAGE_INDEX === 1 && this.phase === 'preparation' && this.waveIndex === 2;
  }

  private isStageTwoLessonWave(): boolean {
    return ACTIVE_STAGE_INDEX === 1 && this.phase === 'preparation' && this.waveIndex >= 2 && this.waveIndex <= 4;
  }

  private stageTwoLessonType(): StageTwoLessonType | null {
    if (!this.isStageTwoLessonWave()) return null;
    if (this.waveIndex === 3) return 'support';
    if (this.waveIndex === 4) return 'special';
    return null;
  }

  private stageTwoLessonNode(type = this.stageTwoLessonType()): NodeState | null {
    if (!type) return null;
    return [...this.nodes.values()].find((node) => node.group.userData.stageTwoLessonType === type) ?? null;
  }

  private stageTwoHighGroundNode(type: StageTwoHighGroundType): NodeState | null {
    return [...this.nodes.values()].find((node) => node.group.userData.stageTwoHighGroundType === type) ?? null;
  }

  private stageTwoHighGroundPlan(): StageTwoHighGroundPlan | null {
    if (this.stageTwoHighGroundPlanCache) return this.stageTwoHighGroundPlanCache;
    if (ACTIVE_STAGE_INDEX !== 1 || this.nexusNodeId === null) return null;
    const nexus = this.nodes.get(this.nexusNodeId);
    if (!nexus) return null;
    const highSlots = [...this.slots.values()].filter((slot) => BUILD_SLOTS[slot.id]?.tier === 'high' && slot.occupiedNodeId === null);
    const canvasBounds = this.canvas.getBoundingClientRect();
    let best: { plan: StageTwoHighGroundPlan; score: number } | null = null;
    for (const generatorSlot of highSlots) for (const fireSlot of highSlots) for (const iceSlot of highSlots) {
      if (generatorSlot.id === fireSlot.id || generatorSlot.id === iceSlot.id || fireSlot.id === iceSlot.id) continue;
      const generatorAnchor = generatorSlot.mesh.position.clone().add(new THREE.Vector3(0, 0.32, 0));
      const fireAnchor = fireSlot.mesh.position.clone().add(new THREE.Vector3(0, 0.32, 0));
      const iceAnchor = iceSlot.mesh.position.clone().add(new THREE.Vector3(0, 0.32, 0));
      const nexusAnchor = this.nodeAnchor(nexus);
      const generatorDistance = new THREE.Vector2(generatorAnchor.x, generatorAnchor.z).distanceTo(new THREE.Vector2(fireAnchor.x, fireAnchor.z));
      const fireDistance = new THREE.Vector2(fireAnchor.x, fireAnchor.z).distanceTo(new THREE.Vector2(iceAnchor.x, iceAnchor.z));
      const nexusDistance = new THREE.Vector2(iceAnchor.x, iceAnchor.z).distanceTo(new THREE.Vector2(nexusAnchor.x, nexusAnchor.z));
      if (generatorDistance > NODE_DEFINITIONS.generator.connectionRange + 1e-6
        || fireDistance > NODE_DEFINITIONS.fire.connectionRange + 1e-6
        || nexusDistance > NODE_DEFINITIONS.ice.connectionRange + 1e-6) continue;
      if (this.linkObstructed(generatorAnchor, fireAnchor) || this.linkObstructed(fireAnchor, iceAnchor) || this.linkObstructed(iceAnchor, nexusAnchor)) continue;
      const crossingDistance = Math.min(
        this.segmentDistanceToEnemyPath(generatorAnchor, fireAnchor),
        this.segmentDistanceToEnemyPath(fireAnchor, iceAnchor),
      );
      if (crossingDistance > 0.7) continue;
      const projected = [generatorSlot, fireSlot, iceSlot].map((slot) => this.worldToClient(slot.mesh.position));
      const visibleCount = projected.reduce((count, point) => count + (point && point.x >= canvasBounds.left + 20
        && point.x <= canvasBounds.right - 20 && point.y >= canvasBounds.top + 20 && point.y <= canvasBounds.bottom - 20 ? 1 : 0), 0);
      const score = visibleCount * 500 + fireDistance * 8 - crossingDistance * 300 - nexusDistance * 2 - generatorDistance;
      if (!best || score > best.score) {
        best = {
          plan: { slots: { generator: generatorSlot.id, fire: fireSlot.id, ice: iceSlot.id }, crossingDistance },
          score,
        };
      }
    }
    this.stageTwoHighGroundPlanCache = best?.plan ?? null;
    return this.stageTwoHighGroundPlanCache;
  }

  private stageTwoRequiredNode(): PurchasableNodeType | null {
    if (this.isStageTwoHighGroundLessonWave()) {
      const plan = this.stageTwoHighGroundPlan();
      if (!plan) return null;
      return STAGE_TWO_HIGH_GROUND_TYPES.find((type) => !this.stageTwoHighGroundNode(type)) ?? null;
    }
    const type = this.stageTwoLessonType();
    return type && !this.stageTwoLessonNode(type) ? type : null;
  }

  private stageTwoRequiredSlot(type: PurchasableNodeType): number | null {
    if (this.isStageTwoHighGroundLessonWave() && STAGE_TWO_HIGH_GROUND_TYPES.includes(type as StageTwoHighGroundType)) {
      return this.stageTwoHighGroundPlan()?.slots[type as StageTwoHighGroundType] ?? null;
    }
    return type === 'support' || type === 'special' ? this.findStageTwoLessonSlot(type) : null;
  }

  private isMandatoryStageTwoLessonPurchase(type: PurchasableNodeType): boolean {
    return this.stageTwoRequiredNode() === type;
  }

  private stageTwoLessonSource(lesson: NodeState | null): NodeState | null {
    if (!lesson || this.nexusNodeId === null) return null;
    if (lesson.inputSourceId !== null) return this.nodes.get(lesson.inputSourceId) ?? null;
    return [...this.nodes.values()].find((node) => node.id !== lesson.id && node.outputTargetId === this.nexusNodeId
      && node.group.userData.stageTwoHighGroundType == null) ?? null;
  }

  private stageTwoRequiredLinkPair(): { source: NodeState; target: NodeState } | null {
    if (this.isStageTwoHighGroundLessonWave()) {
      const generator = this.stageTwoHighGroundNode('generator');
      const fire = this.stageTwoHighGroundNode('fire');
      const ice = this.stageTwoHighGroundNode('ice');
      const nexus = this.nexusNodeId === null ? null : this.nodes.get(this.nexusNodeId);
      if (!generator || !fire || !ice || !nexus) return null;
      if (generator.outputTargetId !== fire.id) return { source: generator, target: fire };
      if (fire.outputTargetId !== ice.id) return { source: fire, target: ice };
      if (ice.outputTargetId !== nexus.id) return { source: ice, target: nexus };
      return null;
    }
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
    if (this.isStageTwoHighGroundLessonWave()) {
      const generator = this.stageTwoHighGroundNode('generator');
      const fire = this.stageTwoHighGroundNode('fire');
      const ice = this.stageTwoHighGroundNode('ice');
      return generator !== null && fire !== null && ice !== null && this.nexusNodeId !== null
        && generator.outputTargetId === fire.id && fire.outputTargetId === ice.id
        && ice.outputTargetId === this.nexusNodeId && generator.active;
    }
    const lesson = this.stageTwoLessonNode();
    return this.isStageTwoLessonWave() && lesson !== null && lesson.inputSourceId !== null
      && this.nexusNodeId !== null && lesson.outputTargetId === this.nexusNodeId;
  }

  private nodeUnlocked(type: PurchasableNodeType): boolean {
    if (ACTIVE_STAGE_INDEX === 1) {
      if (type === 'support') return this.waveIndex >= 3;
      if (type === 'special') return this.waveIndex >= 4;
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
    if (type === 'nexus' && this.nexusNodeId !== null) { this.error('Mỗi màn chỉ có một Tỏa Hồn.'); return; }
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
    if (type === 'nexus' && this.nexusNodeId !== null) return { valid: false, reason: 'Mỗi màn chỉ có một Tỏa Hồn.' };
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
    const highGroundLesson = this.isStageTwoHighGroundLessonWave() && STAGE_TWO_HIGH_GROUND_TYPES.includes(type as StageTwoHighGroundType);
    const lessonGrant = this.isMandatoryStageTwoLessonPurchase(type) && this.stageTwoRequiredSlot(type) === slotId;
    const paidCost = lessonGrant ? 0 : this.regularNodePrice(type);
    const group = this.art.createNode(type);
    group.userData.lessonGrant = lessonGrant;
    group.userData.stageTwoLessonType = lessonGrant && (type === 'support' || type === 'special') ? type : null;
    group.userData.stageTwoHighGroundType = lessonGrant && highGroundLesson ? type : null;
    group.position.copy(slot.mesh.position);
    if (type === 'nexus') group.scale.setScalar(0.78);
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
    this.refreshSelection();
    this.updateUi(true);
  }

  private validateLink(source: NodeState, target: NodeState, keepExisting: boolean): { valid: boolean; reason: string } {
    if (source.id === target.id) return { valid: false, reason: 'Không thể tự nối.' };
    if (source.type === 'nexus') return { valid: false, reason: 'Tỏa Hồn không có đầu ra.' };
    if (target.type === 'generator') return { valid: false, reason: 'Giếng Hồn không có đầu vào.' };
    const sourceTier = source.slotId === null ? 'low' : BUILD_SLOTS[source.slotId]?.tier;
    const targetTier = target.slotId === null ? 'low' : BUILD_SLOTS[target.slotId]?.tier;
    if (target.type !== 'nexus' && sourceTier !== targetTier) return { valid: false, reason: 'Chỉ nối các trụ cùng tầng.' };
    if (target.type !== 'nexus' && target.inputSourceId !== null && target.inputSourceId !== source.id) return { valid: false, reason: 'Đầu vào đã được dùng.' };
    if (target.type === 'nexus' && !target.nexusInputSourceIds.includes(source.id) && target.nexusInputSourceIds.length >= 2) return { valid: false, reason: 'Tỏa Hồn đã đủ hai chuỗi.' };
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
      let processors = 0;
      let reason = '';
      while (cursor.outputTargetId !== null) {
        const next = this.nodes.get(cursor.outputTargetId);
        if (!next) { reason = 'LIÊN KẾT HỎNG'; break; }
        if (visited.has(next.id)) { reason = 'VÒNG LẶP'; break; }
        visited.add(next.id);
        path.push(next);
        if (next.type !== 'nexus') processors += 1;
        cursor = next;
        if (next.type === 'nexus') break;
      }
      if (!reason && cursor.type !== 'nexus') reason = 'THIẾU ĐIỂM KẾT';
      const requiredProcessors = Math.min(2, this.waveIndex);
      if (!reason && processors < requiredProcessors) reason = requiredProcessors === 1 ? 'CẦN 1 BỘ XỬ LÝ' : 'CẦN 2 BỘ XỬ LÝ';
      if (reason) {
        generator.invalidReason = reason;
        path.slice(1).forEach((node) => { if (!node.active) node.invalidReason = reason; });
        continue;
      }
      path.forEach((node) => { node.active = true; node.invalidReason = ''; });
    }
  }

  private startWave(): void {
    if (this.phase !== 'preparation') return;
    if (this.tutorialStep < TUTORIAL_COMPLETE_STEP && !TUTORIAL_START_STEPS.has(this.tutorialStep)) { this.audio.ui('error'); return; }
    if (this.isStageTwoLessonWave() && !this.stageTwoLessonComplete()) { this.audio.ui('error'); return; }
    this.validateNetwork();
    if (![...this.nodes.values()].some((node) => node.type === 'generator' && node.active)) {
      const processors = Math.min(2, this.waveIndex);
      this.error(processors === 0 ? 'Hãy nối Giếng Hồn tới Tỏa Hồn.' : `Chuỗi hiện tại cần ${processors} trụ biến đổi.`);
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
      this.accumulator -= FIXED_STEP;
    }
    this.animate(elapsed, clamped);
    this.updateEnemyStatusPresentation();
    this.updateVfx(clamped);
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
    this.updateFields(delta);
    if (this.spawnIndex >= wave.orders.length && this.enemies.length === 0) this.finishWave();
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
    const trailGeometry = new THREE.BufferGeometry().setFromPoints([start, start]);
    const trailColor = payload.reaction ? REACTIONS[payload.reaction].color : payload.baseElement ? ELEMENT_COLORS[payload.baseElement] : 0xe7d998;
    const trail = new THREE.Line(trailGeometry, new THREE.LineBasicMaterial({ color: trailColor, transparent: true, opacity: payload.reaction ? 0.95 : 0.68 }));
    this.projectileGroup.add(group, trail);
    this.projectiles.push({
      id: this.nextProjectileId++, payload, group, trail,
      sourceNodeId: source.id, targetNodeId: target.id, start, end, progress: 0,
      hitEnemyIds: new Set(),
    });
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
      const tail = oldPosition.clone().lerp(newPosition, -2.2);
      projectile.trail.geometry.setFromPoints([newPosition, tail]);
      projectile.trail.geometry.attributes.position.needsUpdate = true;
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
      const gained = payload.directHitEnemyIds.size;
      const actual = Math.min(gained, MAX_SOUL - this.soul);
      this.soul += actual;
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
    const conduction = this.strongestConductionAt(hitPoint);
    const debuff = this.supportDebuffAt(enemy.group.position);
    const armor = Math.max(0, definition.armor - enemy.armorBreak - debuff - (conduction ? 5 : 0));
    const mr = Math.max(0, definition.mr - debuff - (conduction ? 5 : 0));
    const multiplier = conduction ? 1.3 : 1;
    const elementMultiplier = !payload.baseElement ? 1
      : definition.immune?.includes(payload.baseElement) ? 0
        : definition.vulnerable?.includes(payload.baseElement) ? 1.35
          : definition.resist?.includes(payload.baseElement) ? 0.55 : 1;
    const physical = payload.physicalDamage * multiplier * 100 / (100 + armor);
    const magic = payload.magicDamage * multiplier * 100 / (100 + mr);
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
    else if (element === 'wind') {
      enemy.progress = Math.max(0, enemy.progress - (ENEMY_DEFINITIONS[enemy.kind].boss ? 0.25 : 0.5));
      enemy.windTime = Math.max(enemy.windTime, 1.8);
    }
    else { enemy.armorBreak = Math.max(enemy.armorBreak, 6); enemy.armorBreakTime = Math.max(enemy.armorBreakTime, 3); }
  }

  private procReaction(reaction: keyof typeof REACTIONS, potency: number, target: EnemyState, position: THREE.Vector3): void {
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
    else if (reaction === 'tempest') target.progress = Math.max(0, target.progress - (target.kind === 'boss' ? 0.75 : 1.5) * potency);
    else if (reaction === 'shatter') { target.armorBreak = Math.max(target.armorBreak, 18 * potency); target.armorBreakTime = Math.max(target.armorBreakTime, 4); }
    else if (reaction === 'firestorm') this.enemiesInRadius(position, 2).forEach((enemy) => { enemy.burnDps = Math.max(enemy.burnDps, 2.5 * potency); enemy.burnTime = Math.max(enemy.burnTime, 3); });
    else if (reaction === 'sandstorm') this.enemiesInRadius(position, 2.5).forEach((enemy) => { enemy.armorBreak = Math.max(enemy.armorBreak, 10 * potency); enemy.armorBreakTime = Math.max(enemy.armorBreakTime, 4); });
    else if (reaction === 'permafrost') this.enemiesInRadius(position, 2.5).forEach((enemy) => { enemy.slow = Math.max(enemy.slow, 0.35 * potency); enemy.slowTime = Math.max(enemy.slowTime, 4); });
    else this.enemiesInRadius(position, 2).forEach((enemy) => this.damageMagic(enemy, 10 * potency, position, REACTIONS.steamBurst.color));
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
      enemy.armorBreakTime = Math.max(0, enemy.armorBreakTime - delta); if (enemy.armorBreakTime <= 0) enemy.armorBreak = 0;
      enemy.hitFlash = Math.max(0, enemy.hitFlash - delta * 5);
      if (enemy.dead || enemy.freezeTime > 0) continue;
      const definition = ENEMY_DEFINITIONS[enemy.kind];
      const fieldSlow = this.strongestSoulSlowAt(enemy.group.position);
      const slow = Math.max(enemy.slow, fieldSlow);
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
    const definition = ENEMY_DEFINITIONS[enemy.kind];
    const debuff = this.supportDebuffAt(enemy.group.position);
    const damage = magic * 100 / (100 + Math.max(0, definition.mr - debuff));
    this.damageEnemy(enemy, damage, position, color);
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

  private soulFieldProfile(): { branch: SoulField['branch']; radius: number; duration: number; color: number } {
    const nexus = this.nexusNodeId ? this.nodes.get(this.nexusNodeId) : null;
    const branch: SoulField['branch'] = nexus?.branch === 'suppression' ? 'suppression' : nexus?.branch === 'conduction' ? 'conduction' : 'base';
    return {
      branch,
      radius: branch === 'suppression' ? 4 : 3.5,
      duration: branch === 'suppression' ? 6 : branch === 'conduction' ? 5 : 4.5,
      color: branch === 'conduction' ? 0xff9b68 : branch === 'suppression' ? 0x7455dc : NODE_DEFINITIONS.nexus.color,
    };
  }

  private castSoulField(point: THREE.Vector3): void {
    if (!this.soulTargeting || this.soul < MAX_SOUL) return;
    const { branch, radius, duration, color } = this.soulFieldProfile();
    const mesh = this.art.createSoulField(branch, radius);
    mesh.position.copy(point).setY(0.24);
    this.effectGroup.add(mesh);
    this.fields.push({ id: this.nextFieldId++, mesh, position: point.clone(), branch, remaining: duration, radius, tickTimer: 0.82 });
    const impactDamage = branch === 'conduction' ? 22 : branch === 'suppression' ? 14 : 18;
    this.enemiesInRadius(point, radius).forEach((enemy) => {
      this.damageMagic(enemy, impactDamage, enemy.group.position, color);
      this.spawnDamageNumber(enemy.group.position, impactDamage, color);
    });
    this.soul = 0;
    this.soulTargeting = false;
    if (ACTIVE_STAGE.tutorial && this.waveIndex >= 3) this.soulSkillTutorial = 'complete';
    this.soulCasts += 1;
    this.audio.special();
    this.spawnBurst(point.clone().setY(0.6), color, 2);
    this.spawnSkillImpact(point, radius, color);
    this.updateUi(true);
  }

  private updateFields(delta: number): void {
    for (let index = this.fields.length - 1; index >= 0; index -= 1) {
      const field = this.fields[index];
      field.remaining -= delta;
      field.tickTimer -= delta;
      field.mesh.rotation.y += delta * 0.25;
      if (field.tickTimer <= 0 && field.remaining > 0) {
        field.tickTimer += 0.82;
        const color = field.branch === 'conduction' ? 0xff9b68 : field.branch === 'suppression' ? 0x7455dc : NODE_DEFINITIONS.nexus.color;
        const damage = field.branch === 'conduction' ? 7 : field.branch === 'suppression' ? 4 : 5;
        const targets = this.enemiesInRadius(field.position, field.radius);
        if (targets.length > 0) {
          targets.forEach((enemy, targetIndex) => {
            this.damageMagic(enemy, damage, enemy.group.position, color);
            if (targetIndex < 8) this.spawnDamageNumber(enemy.group.position, damage, color);
          });
          this.soulFieldDamageTicks += 1;
          this.soulFieldDamageEvents += targets.length;
          this.spawnPulse(field.position, field.radius * 0.94, color);
        }
      }
      if (field.remaining <= 0) {
        this.effectGroup.remove(field.mesh);
        disposeObject(field.mesh);
        this.fields.splice(index, 1);
      }
    }
  }

  private strongestSoulSlowAt(position: THREE.Vector3): number {
    let slow = 0;
    for (const field of this.fields) {
      if (field.branch === 'conduction' || field.position.distanceTo(position) > field.radius) continue;
      slow = Math.max(slow, field.branch === 'suppression' ? 0.5 : 0.35);
    }
    return slow;
  }

  private strongestConductionAt(position: THREE.Vector3): boolean {
    return this.fields.some((field) => field.branch === 'conduction' && field.position.distanceTo(position) <= field.radius);
  }

  private finishWave(): void {
    if (this.phase !== 'wave') return;
    this.cancelSoulSkillDrag(true);
    this.clearFields();
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
      nextFieldId: this.nextFieldId,
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
    this.nextFieldId = checkpoint.nextFieldId;
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
    this.phase = won ? 'won' : 'lost';
    this.audio.setPaused(false);
    if (won) this.audio.win(); else this.audio.lose();
    this.resultKicker.textContent = won ? 'NGHI THỨC HOÀN TẤT' : 'TÂM HỒN VỠ NÁT';
    this.resultTitle.textContent = won ? 'Linh mạch trụ vững' : 'Căn cứ đã thất thủ';
    this.resultCopy.textContent = won
      ? `${WAVES.length} đợt tại ${ACTIVE_STAGE.title} đã bị đẩy lùi.`
      : 'Hãy dựng lại đường đạn và thử lần nữa.';
    this.resultRestart.textContent = won && ACTIVE_STAGE_INDEX < 2
      ? `Tới Màn ${ACTIVE_STAGE_INDEX + 2}`
      : !won && this.masteryCheckpoint && this.isTutorialMasteryPhase()
        ? 'Thử lại 3 đợt'
        : 'Chơi lại nghi thức';
    this.resultOverlay.classList.remove('hidden');
    this.updateUi(true);
  }

  private resetRun(): void {
    this.cancelBuildDrag();
    this.clearLinkDrag();
    this.cancelSoulSkillDrag(false);
    while (this.projectiles.length > 0) this.removeProjectile(this.projectiles.length - 1);
    this.enemies.slice().forEach((enemy) => this.removeEnemy(enemy));
    this.clearFields();
    this.nodes.forEach((node) => { this.nodeGroup.remove(node.group); disposeObject(node.group); });
    this.nodes.clear();
    this.slots.forEach((slot) => { slot.occupiedNodeId = null; });
    this.links.splice(0).forEach((link) => { this.linkGroup.remove(link.group); disposeObject(link.group); });
    this.selectionGroup.clear();
    this.masteryCheckpoint = null;
    this.stageTwoLessonSlots.clear();
    this.stageTwoHighGroundPlanCache = null;
    this.gold = STARTING_GOLD; this.baseHp = STARTING_BASE_HP; this.soul = 0; this.waveIndex = 0;
    this.waveClock = 0; this.spawnIndex = 0; this.phase = 'preparation';
    this.selectedBuildType = null; this.selectedNodeId = null; this.linkSourceId = null; this.linkHoverTargetId = null;
    this.soulTargeting = false; this.soulSkillTutorial = ACTIVE_STAGE.tutorial ? 'button' : 'complete'; this.nexusNodeId = null; this.nextNodeId = 1; this.nextPayloadId = 1;
    this.nextProjectileId = 1; this.nextEnemyId = 1; this.nextFieldId = 1; this.tutorialStep = ACTIVE_STAGE.tutorial ? 0 : TUTORIAL_COMPLETE_STEP;
    this.tutorialReactionSeen = false; this.reactionTutorialDelay = -1; this.reactionTutorialVisible = false; this.pendingReaction = null;
    this.currencyTutorialSeen = false; this.baseTutorialSeen = false; this.currencyHighlightTime = 0; this.baseHighlightTime = 0;
    this.reactionTutorial.classList.add('hidden'); this.reactionTutorial.setAttribute('aria-hidden', 'true'); this.audio.setPaused(false);
    this.directHits = 0; this.layerOneEnemyHits = 0; this.reactionProcs = 0; this.specialPulses = 0;
    this.soulCasts = 0; this.soulFieldDamageTicks = 0; this.soulFieldDamageEvents = 0; this.killedEnemies = 0; this.leakedEnemies = 0; this.rng = createSeededRandom(20260816);
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

  private clearFields(): void {
    this.fields.splice(0).forEach((field) => { this.effectGroup.remove(field.mesh); disposeObject(field.mesh); });
  }

  private refreshSoulTargetPreview(point: THREE.Vector3 | null): void {
    if (!point) { this.clearSoulTargetPreview(); return; }
    if (!this.soulTargetPreview) {
      const { radius, color } = this.soulFieldProfile();
      const preview = new THREE.Group();
      const disc = this.createRangeDisc(radius, color, 0.3);
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
      preview.add(disc, outer, inner);
      preview.renderOrder = 45;
      this.effectGroup.add(preview);
      this.soulTargetPreview = preview;
    }
    this.soulTargetPreview.position.copy(point).setY(0.3);
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
      const group = this.createLinkVisual(start, end, source.active ? NODE_DEFINITIONS[source.type].color : 0x6e6676, source.active);
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

  private createLinkVisual(start: THREE.Vector3, end: THREE.Vector3, color: number, active: boolean): THREE.Group {
    const group = new THREE.Group();
    const delta = end.clone().sub(start);
    const length = delta.length();
    const midpoint = start.clone().lerp(end, 0.5);
    const material = new THREE.MeshBasicMaterial({ color, transparent: true, opacity: active ? 0.62 : 0.24, depthWrite: false });
    const beam = new THREE.Mesh(new THREE.CylinderGeometry(active ? 0.065 : 0.045, active ? 0.065 : 0.045, length, 6), material);
    beam.position.copy(midpoint);
    beam.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), delta.clone().normalize());
    group.add(beam);
    const arrow = new THREE.Mesh(new THREE.ConeGeometry(0.19, 0.55, 6), material.clone());
    arrow.position.copy(start.clone().lerp(end, 0.62));
    arrow.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), delta.clone().normalize());
    group.add(arrow);
    return group;
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
    this.refreshCompletedLinkVisibility();
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
      const altitude = definition.layer === 1 ? 'Bay · tầng cao 1' : 'Mặt đất';
      const barrier = definition.reactionBarrier ? ` · Lá chắn: ${REACTIONS[definition.reactionBarrier].name}` : '';
      chip.title = `${definition.name} · ${altitude} · HP ${Math.round(definition.hp * wave.healthMultiplier)} · Giáp ${definition.armor} · Kháng phép ${definition.mr} · Nexus −${definition.leakDamage}${barrier}`;
      chip.style.setProperty('--enemy-color', `#${definition.color.toString(16).padStart(6, '0')}`);
      chip.innerHTML = `${definition.icon}${definition.layer === 1 ? '<em>CAO</em>' : ''}<b>${count}</b>`;
      this.waveEnemies.append(chip);
    });
  }

  private updateUi(force: boolean): void {
    this.baseValue.textContent = String(this.baseHp).padStart(2, '0');
    this.goldValue.textContent = String(this.gold).padStart(3, '0');
    this.waveValue.textContent = `${Math.min(this.waveIndex + 1, WAVES.length)} / ${WAVES.length}`;
    this.enemyValue.textContent = String(this.enemies.length + (this.phase === 'wave' ? WAVES[this.waveIndex].orders.length - this.spawnIndex : 0)).padStart(2, '0');
    this.soulValue.textContent = `${String(Math.floor(this.soul)).padStart(2, '0')} / ${MAX_SOUL}`;
    this.phaseLabel.textContent = this.phase === 'preparation' ? 'CHUẨN BỊ' : this.phase === 'wave' ? 'ĐỢT ĐANG DIỄN RA' : this.phase.toUpperCase();
    this.waveTitle.textContent = WAVES[Math.min(this.waveIndex, WAVES.length - 1)].title;
    const validChain = [...this.nodes.values()].some((node) => node.type === 'generator' && node.active);
    const tutorialAllowsStart = this.tutorialStep >= TUTORIAL_COMPLETE_STEP || TUTORIAL_START_STEPS.has(this.tutorialStep);
    const stageTwoLessonComplete = !this.isStageTwoLessonWave() || this.stageTwoLessonComplete();
    this.startWaveButton.disabled = this.phase !== 'preparation' || !validChain || !tutorialAllowsStart || !stageTwoLessonComplete;
    this.startWaveButton.classList.toggle('tutorial-focus', this.isStageTwoLessonWave() && stageTwoLessonComplete);
    this.startWaveButton.textContent = this.phase === 'wave' ? 'ĐANG CHIẾN ĐẤU' : 'BẮT ĐẦU';
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
      this.inspectorRole.textContent = 'MẠNG HỒN';
      this.inspectorState.textContent = '—';
      this.inspectorIcon.textContent = '◇';
      this.inspectorName.textContent = 'Chưa chọn nút';
      this.inspectorBranch.textContent = 'Chạm một nút để xem.';
      this.inspectorDetail.textContent = 'Nối các trụ để tạo một chuỗi hoàn chỉnh từ Giếng Hồn tới Tỏa Hồn.';
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
      this.chargeLabel.textContent = node.type === 'support' ? 'LINH LỰC' : node.type === 'special' ? 'XUNG LỰC' : 'LINH HỒN';
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
    if ((!ACTIVE_STAGE.tutorial && !stageTwoLessonCue) || this.phase === 'won' || this.phase === 'lost'
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
    const { radius } = this.soulFieldProfile();
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
    const source = [...this.nodes.values()].find((node) => node.outputTargetId === this.nexusNodeId
      && node.group.userData.stageTwoHighGroundType == null);
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

  private segmentDistanceToEnemyPath(start: THREE.Vector3, end: THREE.Vector3): number {
    let best = Number.POSITIVE_INFINITY;
    for (let index = 1; index < ENEMY_PATH.length; index += 1) {
      const [ax, az] = ENEMY_PATH[index - 1];
      const [bx, bz] = ENEMY_PATH[index];
      best = Math.min(best, segmentDistanceXZ(start, end, new THREE.Vector3(ax, start.y, az), new THREE.Vector3(bx, start.y, bz)));
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
      if (required) return this.isStageTwoHighGroundLessonWave() ? `place-high-${required}` : `place-${required}`;
      const pair = this.stageTwoRequiredLinkPair();
      if (pair && this.isStageTwoHighGroundLessonWave()) return `link-high-${pair.source.type}-${pair.target.type}`;
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
    if (this.reducedMotion || this.screenshotPaused) return;
    this.scene.traverse((object) => {
      if (object.name === 'spinner') object.rotation.y += delta * 0.8;
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
      vfx.group.position.y += vfx.rise * delta;
      const opacity = clamp(vfx.remaining / vfx.duration, 0, 1);
      vfx.group.traverse((object) => {
        if ((object instanceof THREE.Mesh || object instanceof THREE.Sprite) && 'opacity' in object.material) {
          (object.material as THREE.Material & { opacity: number; transparent: boolean }).opacity = opacity;
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
    this.camera.lookAt(this.cameraTarget);
  }

  private mobileDpr(): number { return window.matchMedia('(pointer: coarse)').matches ? 1.35 : 1.8; }
  private render(): void { this.renderer.render(this.scene, this.camera); }

  private worldToClient(position: THREE.Vector3): { x: number; y: number } | null {
    const projected = position.clone().project(this.camera);
    if (projected.z < -1 || projected.z > 1) return null;
    const rect = this.canvas.getBoundingClientRect();
    return { x: rect.left + (projected.x + 1) * 0.5 * rect.width, y: rect.top + (1 - projected.y) * 0.5 * rect.height };
  }

  private elementCenter(selector: string): { x: number; y: number } | null {
    const element = document.querySelector<HTMLElement>(selector);
    if (!element || element.offsetParent === null) return null;
    const rect = element.getBoundingClientRect();
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
        } else if (name === 'elevated-hit-demo') this.createElevatedHitDemo();
        else if (name === 'stage-two-wave-three') this.createStageTwoLessonDemo(2);
        else if (name === 'stage-two-wave-four') this.createStageTwoLessonDemo(3);
        else if (name === 'stage-two-wave-five') this.createStageTwoLessonDemo(4);
        else if (name === 'skill-feedback' || name === 'soul-field-damage-demo') this.createSkillFeedbackDemo();
      },
      autoBuildMinimumChain: () => this.autoBuildMinimumChain(),
      startWave: () => this.startWave(),
      dismissReactionTutorial: () => this.dismissReactionTutorial(),
      setSoul: (value: number) => { this.soul = clamp(value, 0, MAX_SOUL); this.updateUi(true); },
      setElementStatusDemo: (element: Element) => {
        this.resetRun(); this.tutorialStep = TUTORIAL_COMPLETE_STEP; this.spawnEnemy('swarm', 0);
        const enemy = this.enemies[0]; if (enemy) { enemy.group.position.set(-3, 0.2, -2); this.applyBaseStatus(element, enemy); }
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
        for (let index = 0; index < steps; index += 1) {
          if (this.phase !== 'wave') break;
          this.simulate(FIXED_STEP);
          this.updateReactionTutorial(FIXED_STEP);
        }
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

  private autoBuildMinimumChain(): void {
    this.resetRun();
    this.tutorialStep = TUTORIAL_COMPLETE_STEP;
    const placements: Array<[PurchasableNodeType, number]> = [
      ['nexus', TUTORIAL_NODE_SLOTS.nexus], ['generator', TUTORIAL_NODE_SLOTS.generator],
      ['fire', TUTORIAL_NODE_SLOTS.fire], ['ice', TUTORIAL_NODE_SLOTS.ice],
    ];
    for (const [type, slot] of placements) { this.selectedBuildType = type; this.tryPlaceSelected(slot); }
    const generator = this.nodeByType('generator')!;
    const fire = this.nodeByType('fire')!;
    const ice = this.nodeByType('ice')!;
    const nexus = this.nodes.get(this.nexusNodeId!)!;
    this.connectNodes(generator.id, fire.id);
    this.connectNodes(fire.id, ice.id);
    this.connectNodes(ice.id, nexus.id);
    this.tutorialStep = TUTORIAL_COMPLETE_STEP;
    this.updateUi(true);
  }

  private createElevatedHitDemo(): void {
    if (ACTIVE_STAGE_INDEX === 0) return;
    this.resetRun();
    this.tutorialStep = TUTORIAL_COMPLETE_STEP;
    this.waveIndex = 2;
    this.gold = Number.MAX_SAFE_INTEGER;
    const positions = ACTIVE_STAGE_INDEX === 1
      ? [[-9, 0], [-7, 0], [-1, 0], [1, 0]] as const
      : [[-13, -1], [-7, -1], [-3, -1], [-1, -1]] as const;
    const types = ['generator', 'ice', 'earth', 'nexus'] as const;
    const placed: NodeState[] = [];
    positions.forEach(([x, z], index) => {
      this.selectedBuildType = types[index];
      if (!this.tryPlaceSelected(buildSlotIdAt(x, z, 'high')) || this.selectedNodeId === null) return;
      const node = this.nodes.get(this.selectedNodeId);
      if (node) placed.push(node);
    });
    for (let index = 0; index < placed.length - 1; index += 1) this.connectNodes(placed[index].id, placed[index + 1].id);
    this.gold = STARTING_GOLD;
    this.selectedNodeId = placed[1]?.id ?? null;
    this.validateNetwork(); this.refreshLinks(); this.refreshSelection(); this.renderWaveRoster(); this.updateUi(true);
    this.phase = 'wave'; this.waveClock = 0; this.spawnIndex = WAVES[this.waveIndex].orders.length;
    this.spawnEnemy('wisp', 0);
    const flyer = this.enemies[0];
    if (flyer) {
      flyer.progress = ACTIVE_STAGE_INDEX === 1 ? 19 : 44;
      const transform = this.pathTransform(flyer.progress, flyer.sideOffset, 1);
      flyer.group.position.copy(transform.position); flyer.group.rotation.y = transform.rotation;
    }
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
    const center = this.pathTransform(centerProgress, 0, 0).position;
    this.soul = MAX_SOUL;
    this.beginSoulTargeting();
    this.castSoulField(center);
    this.updateEnemyStatusPresentation(); this.updateUi(true);
  }

  private createStageTwoLessonDemo(waveIndex: 2 | 3 | 4): void {
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
      const plan = this.stageTwoHighGroundPlan();
      if (plan) {
        for (const type of STAGE_TWO_HIGH_GROUND_TYPES) {
          this.selectedBuildType = type;
          this.tryPlaceSelected(plan.slots[type]);
        }
        let pair = this.stageTwoRequiredLinkPair();
        for (let linkIndex = 0; pair && linkIndex < 3; linkIndex += 1) {
          this.connectNodes(pair.source.id, pair.target.id);
          pair = this.stageTwoRequiredLinkPair();
        }
      }
    }
    if (waveIndex >= 4) {
      this.waveIndex = 3;
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
    this.gold = waveIndex === 2 ? 35 : waveIndex === 3 ? 45 : 55;
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
    const stageTwoHighGroundPlan = this.stageTwoHighGroundPlanCache;
    return {
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
      stageTwoHighGroundPlan: stageTwoHighGroundPlan ? {
        slots: stageTwoHighGroundPlan.slots,
        crossingDistance: stageTwoHighGroundPlan.crossingDistance,
      } : null,
      stageTwoHighGroundGrantedTypes: STAGE_TWO_HIGH_GROUND_TYPES.filter((type) => this.stageTwoHighGroundNode(type) !== null),
      stageTwoHighGroundActive: this.stageTwoHighGroundNode('generator')?.active === true,
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
      gridCellCount: this.slots.size, gridSpacing: BUILD_GRID_SPACING,
      highGroundPlatformCount: HIGH_GROUND_PLATFORMS.length,
      highGroundSlots: BUILD_SLOTS.filter((slot) => slot.tier === 'high').map((slot) => ({ id: slot.id, position: slot.position })),
      pathPoints: ENEMY_PATH.map(([x, z]) => ({ x, z })), pathSegmentLengths,
      maxHorizontalPathSegment: ENEMY_PATH.slice(1).reduce((max, [x, z], index) => z === ENEMY_PATH[index][1] ? Math.max(max, Math.abs(x - ENEMY_PATH[index][0])) : max, 0),
      maxVerticalPathSegment: ENEMY_PATH.slice(1).reduce((max, [x, z], index) => x === ENEMY_PATH[index][0] ? Math.max(max, Math.abs(z - ENEMY_PATH[index][1])) : max, 0),
      nodeCount: this.nodes.size, projectileCount: this.projectiles.length, enemyCount: this.enemies.length,
      selectedNodeId: this.selectedNodeId,
      selectedNetworkNodeIds: this.selectedNodeId === null ? [] : [...this.connectedComponent(new Set([this.selectedNodeId]))],
      activeChains: [...this.nodes.values()].filter((node) => node.type === 'generator' && node.active).length,
      links: [...this.nodes.values()].filter((node) => node.outputTargetId !== null).map((node) => ({ sourceId: node.id, targetId: node.outputTargetId })),
      visibleCompletedLinks: this.links.filter((link) => link.group.visible).map((link) => ({ sourceId: link.sourceId, targetId: link.targetId })),
      nodes: [...this.nodes.values()].map((node) => ({ id: node.id, type: node.type, slotId: node.slotId, position: node.group.position.toArray(), active: node.active, reason: node.invalidReason, input: node.inputSourceId, output: node.outputTargetId, queue: node.buffer.length, reserved: node.reservedIncoming, charge: node.charge, pulseCharge: node.pulseCharge, branch: node.branch, totalInvested: node.totalInvested, lessonGrant: node.group.userData.lessonGrant === true, stageTwoHighGroundType: node.group.userData.stageTwoHighGroundType ?? null })),
      directHits: this.directHits, layerOneEnemyHits: this.layerOneEnemyHits, reactionProcs: this.reactionProcs, specialPulses: this.specialPulses,
      activeVfxCount: this.vfx.length,
      statusIcons: [...this.statusIconMeshes.values()].reduce((sum, mesh) => sum + mesh.count, 0),
      soulCasts: this.soulCasts, soulFieldDamageTicks: this.soulFieldDamageTicks, soulFieldDamageEvents: this.soulFieldDamageEvents,
      activeSoulFields: this.fields.length,
      soulSkillDrag: { active: this.soulSkillDrag !== null, hasPreview: this.soulTargetPreview !== null, point: this.soulSkillDrag?.point?.toArray() ?? null },
      killedEnemies: this.killedEnemies, leakedEnemies: this.leakedEnemies,
      fixedNexus: {
        position: this.baseNexus.position.toArray(),
        visible: this.baseNexus.visible,
        coreVisible: this.baseNexus.getObjectByName('baseNexusCore')?.visible ?? false,
        separateFromSoulAnchor: this.baseNexus !== (this.nexusNodeId === null ? null : this.nodes.get(this.nexusNodeId)?.group),
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
