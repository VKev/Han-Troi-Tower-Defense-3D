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
  BOARD_DEPTH,
  BOARD_WIDTH,
  ELEMENT_COLORS,
  ELEMENT_NAMES,
  ENEMY_DEFINITIONS,
  FIXED_STEP,
  LAYER_HEIGHTS,
  MAX_TOWER_LEVEL,
  REACTION_PAIRS,
  SELL_REFUND,
  STAGES,
  STARTING_LIVES,
  STARTING_MONEY,
  TOWER_DEFINITIONS,
  gridKey,
  gridToWorld,
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

const BUILD_ORDER: readonly TowerType[] = ['foundry', 'fire', 'ice', 'wind', 'earth', 'amplifier', 'lance'];
const BUILD_GROUPS: readonly { label: string; types: readonly TowerType[] }[] = [
  { label: 'Trụ sinh đạn', types: ['foundry'] },
  { label: 'Hỗ trợ đạn', types: ['fire', 'ice', 'wind', 'earth'] },
  { label: 'Hỗ trợ trụ', types: ['amplifier'] },
  { label: 'Trụ đặc biệt', types: ['lance'] },
];
const TUTORIAL_STEP_COUNT = 11;
const TUTORIAL_TOWER_CELLS: Readonly<Record<'foundry' | 'fire' | 'ice', { gx: number; gz: number }>> = {
  foundry: { gx: 8, gz: 3 },
  fire: { gx: 9, gz: 2 },
  ice: { gx: 10, gz: 3 },
};
const TUTORIAL_FOUNDRY_HEAD_ON_ANGLE = Math.atan2(-1, -8);
const TUTORIAL_FOUNDRY_TO_FIRE_ANGLE = -Math.PI / 4;
const TUTORIAL_FIRE_HEAD_ON_ANGLE = Math.atan2(1, -8);
const TUTORIAL_FIRE_TO_ICE_ANGLE = Math.PI / 4;
const TUTORIAL_ICE_HEAD_ON_ANGLE = Math.atan2(-1, -8);
const TUTORIAL_WAVE_START_STEPS = [1, 3, 9] as const;
const ROTATION_SPEED = THREE.MathUtils.degToRad(105);
const MAX_ENEMY_LANE_OFFSET = 0.28;
const GAME_SPEEDS = [1, 2] as const;
const ELEMENT_STATUS_TINT = 0.94;
const MULTI_ELEMENT_STATUS_TINT = 0.97;
const ELEMENT_STATUS_EMISSIVE_BOOST = 0.9;
const SELECTED_AIM_GUIDE_RADIUS = 0.085;
const SELECTED_AIM_GUIDE_OPACITY = 0.42;
const ENEMY_GLYPHS: Readonly<Record<EnemyKind, string>> = {
  riftling: '◆',
  runner: '➤',
  brute: '⬢',
  wisp: '✦',
  frostRay: '◇',
  warder: '⬡',
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
  private readonly camera = new THREE.PerspectiveCamera(44, 1, 0.1, 140);
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
  private tutorialDirectShots = 0;
  private stageTwoAmplifierIntroduced = false;
  private stageTwoLanceIntroduced = false;
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
  private speedIndex = 0;
  private rng = createSeededRandom(20260814);
  private pausedForScreenshot = false;
  private reducedMotion = false;
  private lastToast: ToastState = { text: '', tone: 'info' };
  private pointerMoved = false;
  private buildDrag: BuildDragState | null = null;
  private placementPreviewKey = '';
  private tutorialCueKey = '';
  private suppressBuildClickUntil = 0;
  private rotationPointerId: number | null = null;
  private rotationPointerDirection: -1 | 0 | 1 = 0;
  private readonly heldRotationKeys = new Set<'q' | 'e'>();
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
      new THREE.SphereGeometry(90, 28, 14),
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
    const expanded = this.stageIndex === 1;
    const island = new THREE.Mesh(
      new THREE.CylinderGeometry(expanded ? 24.2 : 18.8, expanded ? 26 : 20.6, 1.25, 12),
      this.materials.groundContact,
    );
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
    for (let gx = 0; gx < BOARD_WIDTH; gx += 1) {
      for (let gz = 0; gz < BOARD_DEPTH; gz += 1) {
        const layer = this.layerForCell(gx, gz);
        const logicalCenter = gridToWorld(gx, gz, layer);
        const buildable = !this.isPathPosition(logicalCenter.x, logicalCenter.z) && logicalCenter.x < 10.6;
        const visualLayer = buildable ? layer : 0;
        const center = gridToWorld(gx, gz, visualLayer);
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
    const matrix = new THREE.Matrix4();
    const quaternion = new THREE.Quaternion();
    const scale = new THREE.Vector3();
    const position = new THREE.Vector3();
    for (let index = 0; index < crystalCount; index += 1) {
      const angle = index / crystalCount * Math.PI * 2;
      const radius = 17.2 + (index % 4) * 0.55;
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

    for (let index = 0; index < 8; index += 1) {
      const pylon = new THREE.Group();
      const base = new THREE.Mesh(new THREE.CylinderGeometry(0.45, 0.62, 1.25, 7), this.materials.bodySecondary);
      base.position.y = 0.62;
      pylon.add(base);
      const beacon = new THREE.Mesh(new THREE.OctahedronGeometry(0.25, 0), index % 2 === 0 ? this.materials.element('wind') : this.materials.reward);
      beacon.position.y = 1.55;
      pylon.add(beacon);
      const angle = index / 8 * Math.PI * 2;
      pylon.position.set(Math.cos(angle) * 15.4, -0.12, Math.sin(angle) * 10.2);
      this.boardGroup.add(pylon);
    }
  }

  private layerForCell(gx: number, gz: number): 0 | 1 | 2 {
    if (!this.activeStage().hasElevation) return 0;
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
      else this.resetRun();
    });
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
    });
    this.canvas.addEventListener('pointermove', (event) => {
      if (this.pointerStart.distanceTo(new THREE.Vector2(event.clientX, event.clientY)) > 7) this.pointerMoved = true;
    });
    this.canvas.addEventListener('pointerup', (event) => {
      if (this.buildDrag?.dragging) return;
      if (this.pointerMoved) return;
      this.handleCanvasTap(event.clientX, event.clientY);
    });
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
      if (event.button !== 0 || !this.prepareHeldRotation()) return;
      event.preventDefault();
      this.rotationPointerId = event.pointerId;
      this.rotationPointerDirection = direction;
      try {
        button.setPointerCapture(event.pointerId);
      } catch {
        // Synthetic accessibility/test events may not register an active pointer.
        // Rotation still stops through pointerup, cancel, blur, or visibility loss.
      }
      button.classList.add('pressed');
    });
    const release = (event: PointerEvent) => {
      if (this.rotationPointerId !== event.pointerId) return;
      this.rotationPointerId = null;
      this.rotationPointerDirection = 0;
      button.classList.remove('pressed');
    };
    button.addEventListener('pointerup', release);
    button.addEventListener('pointercancel', release);
    button.addEventListener('lostpointercapture', release);
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
      if (!this.activeStage().tutorial) this.showToast(`Kéo ${TOWER_DEFINITIONS[drag.type].shortName} vào vùng đặt màu xanh.`, 'info');
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
      if (!this.activeStage().tutorial) this.showToast(drag.reason || 'Không thể đặt vùng trụ tại đây.', 'bad');
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
    if (drag?.dragging && !placed && !this.activeStage().tutorial) this.showToast('Đã hủy đặt trụ. Không mất Arcana.', 'info');
    drag?.button.classList.remove('dragging');
    document.body.classList.remove('is-build-dragging');
    this.buildDrag = null;
    this.controls.enabled = true;
    this.clearPlacementPreview();
  }

  private onKeyDown(event: KeyboardEvent): void {
    const rotationKey = event.key.toLowerCase();
    if (rotationKey === 'q' || rotationKey === 'e') {
      if (!event.repeat && this.prepareHeldRotation()) this.heldRotationKeys.add(rotationKey);
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
    if (this.stageIndex === 1) {
      if (type === 'amplifier') return this.waveIndex >= 2;
      if (type === 'lance') return this.waveIndex >= 3;
      return true;
    }
    if (!this.activeStage().tutorial) return true;
    if (type === 'foundry') return true;
    if (type === 'fire') return this.tutorialStep >= 2;
    if (type === 'ice') return this.tutorialStep >= 6;
    return false;
  }

  private stageTwoRequiredTower(): 'amplifier' | 'lance' | null {
    if (this.stageIndex !== 1 || this.phase !== 'ready') return null;
    if (this.waveIndex === 2 && !this.stageTwoAmplifierIntroduced) return 'amplifier';
    if (this.waveIndex === 3 && !this.stageTwoLanceIntroduced) return 'lance';
    return null;
  }

  private isStageTwoLessonWave(): boolean {
    return this.stageIndex === 1 && this.phase === 'ready' && (this.waveIndex === 2 || this.waveIndex === 3);
  }

  private isMandatoryLessonPurchase(type: TowerType): boolean {
    return this.stageTwoRequiredTower() === type;
  }

  private canPurchaseTower(type: TowerType): boolean {
    return this.money >= TOWER_DEFINITIONS[type].cost || this.isMandatoryLessonPurchase(type);
  }

  private refreshTutorialProgress(): void {
    if (!this.activeStage().tutorial || this.tutorialStep >= TUTORIAL_STEP_COUNT) return;
    let advanced = false;
    while (this.tutorialStep < TUTORIAL_STEP_COUNT) {
      const foundry = this.towers.find((tower) => tower.type === 'foundry');
      const fire = this.towers.find((tower) => tower.type === 'fire');
      const ice = this.towers.find((tower) => tower.type === 'ice');
      const complete = this.tutorialStep === 0 ? Boolean(foundry)
        : this.tutorialStep === 2 ? Boolean(fire)
          : this.tutorialStep === 4 ? Boolean(foundry && fire && this.findAimedReceiver(foundry)?.tower.id === fire.id)
              : this.tutorialStep === 6 ? Boolean(ice)
                : this.tutorialStep === 7 ? Boolean(fire && ice && this.findAimedReceiver(fire)?.tower.id === ice.id)
                  : this.tutorialStep === 8 ? Boolean(ice && this.isAngleAligned(ice.aimAngle, TUTORIAL_ICE_HEAD_ON_ANGLE))
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
    this.setRayFromClient(clientX, clientY);

    const towerHit = this.raycaster.intersectObjects(this.towerPickables, true)[0];
    if (towerHit) {
      const towerId = this.findTowerId(towerHit.object);
      if (towerId !== null) {
        this.handleTowerTap(towerId);
        return;
      }
    }

    const cellHit = this.findCellAt(clientX, clientY);
    if (cellHit) {
      this.handleCellTap(cellHit.cell, cellHit.point);
      return;
    }
    this.cancelInteraction();
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
    this.stopHeldRotation();
    this.selectedBuildType = null;
    this.inspectedBuildType = null;
    this.interactionMode = 'inspect';
    this.selectedTowerId = towerId;
    this.audio.ui('select');
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
    this.selectedTowerId = null;
    this.inspectedBuildType = null;
    this.interactionMode = 'inspect';
    this.refreshSelectionVisual();
    this.updateUi(true);
  }

  private showTowerDefinition(type: TowerType): void {
    this.stopHeldRotation();
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
    if (!this.isTowerUnlocked(type)) {
      if (!this.activeStage().tutorial) this.showToast('Hoàn thành hướng dẫn hiện tại để mở khóa trụ này.', 'bad');
      this.audio.ui('error');
      return;
    }
    if (!this.canPurchaseTower(type)) {
      this.showToast(`Cần thêm ${definition.cost - this.money} Arcana để mua ${definition.shortName}.`, 'bad');
      this.audio.ui('error');
      return;
    }
    this.stopHeldRotation();
    this.inspectedBuildType = null;
    this.selectedBuildType = type;
    this.selectedTowerId = null;
    this.interactionMode = 'build';
    if (!this.activeStage().tutorial) this.showToast(`Đặt ${definition.name} · kích thước ${definition.footprint[0]}×${definition.footprint[1]} ô.`, 'info');
    this.audio.ui('select');
    this.refreshSelectionVisual();
    this.updateUi(true);
  }

  private tryPlaceTower(type: TowerType, gx: number, gz: number): boolean {
    const definition = TOWER_DEFINITIONS[type];
    if (!this.canPurchaseTower(type)) return false;
    const paidCost = Math.min(this.money, definition.cost);
    const placement = this.validateFootprint(type, gx, gz, null);
    if (!placement.valid || placement.layer === null) {
      if (!this.activeStage().tutorial) this.showToast(placement.reason, 'bad');
      this.audio.ui('error');
      return false;
    }
    const cells = this.footprintKeys(type, gx, gz);
    const worldPositions = cells.map((key) => {
      const cell = this.cells.get(key);
      if (!cell) throw new Error(`Missing cell ${key}`);
      return gridToWorld(cell.gx, cell.gz, cell.layer);
    });
    const center = worldPositions.reduce((sum, value) => sum.add(value), new THREE.Vector3()).multiplyScalar(1 / worldPositions.length);
    const group = this.art.createTower(type);
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
      aimAngle: 0,
      produceTimer: 0.28,
      outputTimer: 0.16,
      skillTimer: 0,
      blockedReason: '',
      amplifierBranch: 'throughput',
      pulse: 0,
    };
    if (this.activeStage().tutorial && type === 'foundry') tower.aimAngle = TUTORIAL_FOUNDRY_HEAD_ON_ANGLE;
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
    if (this.stageIndex === 1 && type === 'amplifier') this.stageTwoAmplifierIntroduced = true;
    if (this.stageIndex === 1 && type === 'lance') this.stageTwoLanceIntroduced = true;
    this.selectedTowerId = tower.id;
    this.selectedBuildType = null;
    this.inspectedBuildType = null;
    this.interactionMode = 'inspect';
    this.spawnBurst(center.clone().add(new THREE.Vector3(0, 0.5, 0)), definition.color, 0.42);
    this.audio.build();
    this.showToast(`Đã đặt ${definition.name} ở tầng ${tower.layer}.`, 'good');
    this.refreshTutorialProgress();
    this.refreshNetworkVisuals();
    this.refreshSelectionVisual();
    this.updateUi(true);
    return true;
  }

  private validateFootprint(type: TowerType, gx: number, gz: number, movingTowerId: number | null): { valid: boolean; reason: string; layer: 0 | 1 | 2 | null } {
    const keys = this.footprintKeys(type, gx, gz);
    if (keys.length !== TOWER_DEFINITIONS[type].footprint[0] * TOWER_DEFINITIONS[type].footprint[1]) {
      return { valid: false, reason: 'Vùng đặt trụ vượt ra ngoài chiến trường.', layer: null };
    }
    if (this.activeStage().tutorial && movingTowerId === null && (type === 'foundry' || type === 'fire' || type === 'ice')) {
      const expected = TUTORIAL_TOWER_CELLS[type];
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
    if (this.activeStage().tutorial && movingTowerId === null && (type === 'fire' || type === 'ice')) {
      const sourceType: TowerType = type === 'fire' ? 'foundry' : 'fire';
      const source = this.towers.find((tower) => tower.type === sourceType);
      if (source) {
        const center = keys
          .map((key) => this.cells.get(key))
          .filter((cell): cell is CellState => Boolean(cell))
          .map((cell) => gridToWorld(cell.gx, cell.gz, cell.layer))
          .reduce((sum, position) => sum.add(position), new THREE.Vector3())
          .multiplyScalar(1 / keys.length);
        if (source.group.position.distanceTo(center) > this.connectionRange(source)) {
          return { valid: false, reason: `Hướng dẫn: đặt ${TOWER_DEFINITIONS[type].shortName} trong tầm bắn hiển thị.`, layer: null };
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
        if (gx + x >= BOARD_WIDTH || gz + z >= BOARD_DEPTH) continue;
        keys.push(gridKey(gx + x, gz + z));
      }
    }
    return keys;
  }

  private prepareHeldRotation(): boolean {
    const tower = this.selectedTower();
    if (!tower || (!isAmmoEmitter(tower.type) && tower.type !== 'lance')) {
      if (!this.activeStage().tutorial) this.showToast('Chọn một trụ đạn hoặc Thương Nexus để xoay hướng bắn.', 'bad');
      return false;
    }
    const tutorialType = this.tutorialRotationTowerType();
    if (this.activeStage().tutorial && tower.type !== tutorialType) {
      return false;
    }
    tower.outputTimer = Math.max(tower.outputTimer, 0.18);
    this.interactionMode = 'inspect';
    if (!this.activeStage().tutorial) this.showToast(`Giữ ↶ hoặc ↷ để xoay ${TOWER_DEFINITIONS[tower.type].shortName}.`, 'info');
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

  private tutorialRotationTowerType(): TowerType | null {
    if (this.tutorialStep === 4) return 'foundry';
    if (this.tutorialStep === 5 || this.tutorialStep === 7) return 'fire';
    if (this.tutorialStep === 8) return 'ice';
    return null;
  }

  private tutorialRotationTarget(tower: TowerState): number | null {
    if (this.tutorialStep === 4 && tower.type === 'foundry') return TUTORIAL_FOUNDRY_TO_FIRE_ANGLE;
    if (this.tutorialStep === 5 && tower.type === 'fire') return TUTORIAL_FIRE_HEAD_ON_ANGLE;
    if (this.tutorialStep === 7 && tower.type === 'fire') return TUTORIAL_FIRE_TO_ICE_ANGLE;
    if (this.tutorialStep === 8 && tower.type === 'ice') return TUTORIAL_ICE_HEAD_ON_ANGLE;
    return null;
  }

  private isTutorialFireAimComplete(): boolean {
    const fire = this.towers.find((tower) => tower.type === 'fire');
    return Boolean(fire && this.isAngleAligned(fire.aimAngle, TUTORIAL_FIRE_HEAD_ON_ANGLE));
  }

  private isAngleAligned(angle: number, target: number, tolerance = THREE.MathUtils.degToRad(1.5)): boolean {
    return Math.abs(Math.atan2(Math.sin(target - angle), Math.cos(target - angle))) <= tolerance;
  }

  private updateHeldRotation(delta: number): void {
    const keyboardDirection = (this.heldRotationKeys.has('e') ? 1 : 0) - (this.heldRotationKeys.has('q') ? 1 : 0);
    const direction = this.rotationPointerDirection || Math.sign(keyboardDirection);
    if (direction === 0) return;
    const tower = this.selectedTower();
    if (!tower || (!isAmmoEmitter(tower.type) && tower.type !== 'lance')) {
      this.stopHeldRotation();
      return;
    }
    const tutorialTarget = this.activeStage().tutorial ? this.tutorialRotationTarget(tower) : null;
    if (this.activeStage().tutorial && tutorialTarget === null) {
      this.stopHeldRotation();
      return;
    }

    const rotationDelta = direction * ROTATION_SPEED * delta;
    let nextAngle = Math.atan2(Math.sin(tower.aimAngle + rotationDelta), Math.cos(tower.aimAngle + rotationDelta));
    let tutorialAligned = false;
    if (tutorialTarget !== null) {
      const remaining = Math.atan2(
        Math.sin(tutorialTarget - tower.aimAngle),
        Math.cos(tutorialTarget - tower.aimAngle),
      );
      if (Math.sign(remaining) === direction && Math.abs(remaining) <= Math.abs(rotationDelta)) {
        nextAngle = tutorialTarget;
        tutorialAligned = true;
      }
    }
    tower.aimAngle = nextAngle;
    tower.outputTimer = Math.max(tower.outputTimer, 0.18);
    this.applyTowerAimVisual(tower);
    this.refreshNetworkVisuals();
    this.updateSelectedAimGuide(tower);

    if (tutorialAligned) {
      const completedStep = this.tutorialStep;
      this.stopHeldRotation();
      this.refreshTutorialProgress();
      if (completedStep === 5 && this.phase === 'paused') this.setPhase('wave');
      if (!this.activeStage().tutorial) {
        const receiver = this.findAimedReceiver(tower)?.tower;
        this.showToast(receiver
          ? `Đường đạn của ${TOWER_DEFINITIONS[tower.type].shortName} đi xuyên ${TOWER_DEFINITIONS[receiver.type].shortName}.`
          : `${TOWER_DEFINITIONS[tower.type].shortName} đang bắn ngược hướng di chuyển của địch.`, 'good');
      }
      this.audio.ui('confirm');
      this.updateUi(true);
    }
  }

  private beginMove(): void {
    if (this.activeStage().tutorial) {
      this.showToast('Di chuyển có phí được mở khóa ở màn 2.', 'info');
      return;
    }
    const tower = this.selectedTower();
    if (!tower) return;
    const cost = TOWER_DEFINITIONS[tower.type].moveCost;
    if (this.money < cost) {
      this.showToast(`Cần thêm ${cost - this.money} Arcana để di chuyển trụ này.`, 'bad');
      return;
    }
    this.interactionMode = 'move';
    this.showToast(`Chọn vùng đặt mới. Phí di chuyển: ${cost}. Kho đạn và hướng ngắm được giữ nguyên.`, 'info');
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
      return gridToWorld(cell.gx, cell.gz, cell.layer);
    });
    const center = positions.reduce((sum, value) => sum.add(value), new THREE.Vector3()).multiplyScalar(1 / positions.length);
    for (const key of newCells) this.occupied.set(key, tower.id);
    tower.cells.splice(0, tower.cells.length, ...newCells);
    Object.assign(tower, { gx, gz, layer: placement.layer });
    tower.group.position.copy(center);
    tower.outputTimer = 0.45;
    this.money -= definition.moveCost;
    this.interactionMode = 'inspect';
    this.spawnBurst(center.clone().add(new THREE.Vector3(0, 0.5, 0)), definition.color, 0.38);
    this.showToast(`Đã chuyển ${definition.shortName} tới tầng ${tower.layer}; đường đạn được tính lại.`, 'good');
    this.refreshNetworkVisuals();
    this.refreshSelectionVisual();
    this.updateUi(true);
  }

  private upgradeSelected(): void {
    if (this.activeStage().tutorial) {
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
    this.showToast(`Bộ Khuếch Đại đang tăng ${branch === 'power' ? 'sát thương và sức mạnh nguyên tố' : 'nhịp bắn và sức chứa kho đạn'}.`, 'good');
    this.audio.ui('confirm');
    this.updateUi(true);
  }

  private sellSelected(): void {
    if (this.activeStage().tutorial) {
      this.showToast('Bán trụ được mở khóa sau màn hướng dẫn.', 'info');
      return;
    }
    const tower = this.selectedTower();
    if (!tower) return;
    const refund = Math.floor(tower.totalInvested * SELL_REFUND);
    for (const key of tower.cells) this.occupied.delete(key);
    const index = this.towers.indexOf(tower);
    if (index >= 0) this.towers.splice(index, 1);
    tower.group.traverse((child) => {
      const pickableIndex = this.towerPickables.indexOf(child);
      if (pickableIndex >= 0) this.towerPickables.splice(pickableIndex, 1);
    });
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
    this.stopHeldRotation();
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
    if (this.activeStage().tutorial) {
      const expectedStep = TUTORIAL_WAVE_START_STEPS[this.waveIndex];
      if (expectedStep === undefined || this.tutorialStep !== expectedStep) {
        this.audio.ui('error');
        return;
      }
    }
    if (this.stageTwoRequiredTower() !== null) {
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
    if (this.activeStage().tutorial && this.waveIndex === 2 && this.tutorialStep === 9) this.tutorialStep = 10;
    if (window.matchMedia('(max-width: 700px) and (orientation: portrait)').matches) {
      this.selectedTowerId = null;
      this.refreshSelectionVisual();
    }
    this.setPhase('wave');
    this.audio.wave();
    this.showToast(`Đợt ${this.waveIndex + 1}: ${waves[this.waveIndex].title}`, 'reaction');
  }

  private togglePause(): void {
    if (this.phase === 'wave') this.setPhase('paused');
    else if (this.phase === 'paused') {
      const fireAimLesson = this.activeStage().tutorial && this.waveIndex === 1
        && (this.tutorialStep === 4 || this.tutorialStep === 5)
        && !this.isTutorialFireAimComplete();
      if (!fireAimLesson) this.setPhase('wave');
    }
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
    if (this.phase === 'wave') {
      this.fixedAccumulator += realDelta * GAME_SPEEDS[this.speedIndex];
      while (this.fixedAccumulator >= FIXED_STEP && this.phase === 'wave') {
        this.simulate(FIXED_STEP);
        this.fixedAccumulator -= FIXED_STEP;
      }
    }
    this.animateWorld(this.reducedMotion ? 0 : realDelta, this.reducedMotion ? 0 : elapsed);
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
    const group = this.art.createEnemy(kind);
    const laneOffset = THREE.MathUtils.clamp(sideOffset + (this.rng() - 0.5) * 0.06, -MAX_ENEMY_LANE_OFFSET, MAX_ENEMY_LANE_OFFSET);
    const position = this.pathPosition(0, laneOffset, definition.layer);
    group.position.copy(position);
    this.orientEnemy(group, position, this.pathPosition(0.35, laneOffset, definition.layer));
    const enemy: EnemyState = {
      id: this.nextEnemyId,
      kind,
      group,
      hp: definition.hp,
      maxHp: definition.hp,
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
      enemy.speedMultiplier = slow;
      enemy.progress += definition.speed * slow * delta;
      if (enemy.progress >= this.pathTotalLength) {
        enemy.reachedNexus = true;
        this.lives = Math.max(0, this.lives - definition.nexusDamage);
        this.audio.leak();
        this.spawnBurst(this.nexus.position.clone().add(new THREE.Vector3(0, 1.4, 0)), 0xff4f66, 0.68);
        this.showToast(`${definition.name} đã lọt vào Nexus · −${definition.nexusDamage} mạng`, 'bad');
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
        tower.produceTimer -= delta;
        if (tower.produceTimer <= 0) {
          const available = effectiveCapacity - tower.buffer.length;
          if (available > 0) {
            const count = tower.level >= 3 ? Math.min(2, available) : 1;
            for (let index = 0; index < count; index += 1) tower.buffer.push(this.createNeutralRound(tower));
            tower.produceTimer += 1.65 / throughput / (1 + (tower.level - 1) * 0.16);
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
        if (tower.buffer.length >= threshold && tower.skillTimer <= 0) this.fireLance(tower, threshold);
        continue;
      }
      if (!isAmmoEmitter(tower.type)) continue;
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
    const interval = 1 / Math.max(0.1, definition.cadence * throughput * (1 + (source.level - 1) * 0.13));
    const predictedReceiver = this.findAimedReceiver(source)?.tower;
    if (predictedReceiver && predictedReceiver.buffer.length >= this.capacity(predictedReceiver)) {
      source.blockedReason = `Kho đạn của ${TOWER_DEFINITIONS[predictedReceiver.type].shortName} đã đầy`;
      return;
    }
    const round = source.buffer.shift();
    if (!round) return;
    this.launchProjectile(source, round);
    source.outputTimer += interval;
    if (source.type === 'foundry' && this.activeStage().tutorial && this.waveIndex === 0) this.tutorialDirectShots += 1;
    if (
      source.type === 'foundry'
      && this.activeStage().tutorial
      && this.waveIndex === 1
      && this.tutorialStep === 3
      && this.enemies.some((enemy) => !enemy.dead
        && ENEMY_DEFINITIONS[enemy.kind].layer === source.layer
        && enemy.group.position.distanceTo(source.group.position) <= this.connectionRange(source))
    ) {
      this.tutorialStep = 4;
      this.setPhase('paused');
    }
  }

  private launchProjectile(source: TowerState, sourceRound: Round): void {
    const round: Round = {
      id: sourceRound.id,
      damage: sourceRound.damage * this.powerMultiplier(source),
      elements: [...sourceRound.elements],
    };
    const start = this.towerPort(source);
    const direction = new THREE.Vector3(Math.cos(source.aimAngle), 0, Math.sin(source.aimAngle));
    const end = start.clone().addScaledVector(direction, this.connectionRange(source));
    const blockerHit = this.firstBlockerHit(start, end, source.layer);
    if (blockerHit !== null) end.lerpVectors(start, end, Math.max(0, blockerHit - 0.015));
    const mesh = this.art.createProjectile(round.elements);
    if (round.elements.length > 1) mesh.scale.setScalar(1 + Math.min(0.42, round.elements.length * 0.1));
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
      start,
      end,
      layer: source.layer,
      hitEnemyIds: new Set<number>(),
      progress: 0,
      speed: (8.5 + source.level * 0.7) * speedBonus,
    });
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
      const towerHit = this.findTowerIntersection(
        projectile.sourceTowerId,
        projectile.layer,
        previousPosition,
        projectile.mesh.position,
      );
      const collisionLimit = towerHit?.entry ?? 1;

      for (const enemy of this.enemies) {
        if (enemy.dead || projectile.hitEnemyIds.has(enemy.id)) continue;
        const definition = ENEMY_DEFINITIONS[enemy.kind];
        if (definition.layer !== projectile.layer) continue;
        const enemyEntry = segmentSphereEntry(
          previousPosition,
          projectile.mesh.position,
          enemy.group.position,
          definition.radius + 0.42,
        );
        if (enemyEntry === null || enemyEntry > collisionLimit) continue;
        projectile.hitEnemyIds.add(enemy.id);
        const hitPosition = previousPosition.clone().lerp(projectile.mesh.position, enemyEntry);
        this.applyProjectileHit(projectile.round, enemy, hitPosition);
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
      this.removeProjectile(index);
    }
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
    if (enemy.reactionBarrier !== null && reaction?.name !== enemy.reactionBarrier) direct *= 0.22;

    if (reaction) {
      this.resolveReaction(reaction.name, reaction.color, round, enemy, position);
      if (enemy.reactionBarrier === reaction.name) {
        enemy.reactionBarrier = null;
        const barrier = enemy.group.getObjectByName('barrier');
        if (barrier) barrier.visible = false;
        direct *= 1.55;
        this.showToast(`${reaction.name} đã phá vỡ lá chắn Hộ Vệ!`, 'reaction');
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
        if (hadIce) enemy.frozen = Math.max(enemy.frozen, definition.kind === 'brute' || definition.kind === 'warder' ? 0.3 : 0.75);
      } else if (element === 'wind') enemy.gale = Math.max(enemy.gale, 3.2);
      else enemy.cracked = Math.max(enemy.cracked, 4.5);
    }

    if (pairCount > 0) this.applyFusionPayload(round, enemy, position);
  }

  private resolveReaction(name: string, color: number, round: Round, enemy: EnemyState, position: THREE.Vector3): void {
    this.reactionCount += 1;
    if (this.activeStage().tutorial && this.tutorialStep === 10) {
      this.tutorialStep = TUTORIAL_STEP_COUNT;
      this.showToast(name, 'reaction');
      this.updateUi(true);
    }
    const bonus = 18 + round.damage * 0.38;
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
    const reward = Math.round(definition.reward * this.activeStage().killRewardMultiplier);
    this.money += reward;
    this.spawnBurst(enemy.group.position.clone().add(new THREE.Vector3(0, 0.6, 0)), definition.color, 0.62);
    if (cause && cause !== 'Diện rộng') this.showToast(`Đã hạ ${definition.name}${cause ? ` · ${cause}` : ''} · +${reward} Arcana`, 'good');
    this.audio.destroy();
    this.scene.remove(enemy.group);
    this.disposeEnemy(enemy);
    this.enemies.splice(index, 1);
  }

  private disposeEnemy(enemy: EnemyState): void {
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

  private fireLance(tower: TowerState, threshold: number): void {
    const consumed = tower.buffer.splice(0, threshold);
    const elements = uniqueElements(consumed.flatMap((round) => round.elements));
    const averageDamage = consumed.reduce((sum, round) => sum + round.damage, 0) / Math.max(1, consumed.length);
    const round: Round = {
      id: this.nextRoundId++,
      damage: averageDamage * (2.2 + tower.level * 0.42),
      elements,
    };
    const start = this.towerPort(tower);
    const direction = new THREE.Vector3(Math.cos(tower.aimAngle), 0, Math.sin(tower.aimAngle));
    let end = start.clone().addScaledVector(direction, 19 + tower.level * 2);
    const hit = this.firstBlockerHit(start, end, tower.layer);
    if (hit !== null) end = start.clone().lerp(end, Math.max(0, hit - 0.01));
    this.createLanceVfx(start, end, elements);
    const segment = end.clone().sub(start);
    const segmentLengthSq = segment.lengthSq();
    for (const enemy of [...this.enemies]) {
      if (enemy.dead || ENEMY_DEFINITIONS[enemy.kind].layer !== tower.layer) continue;
      const toEnemy = enemy.group.position.clone().sub(start);
      const t = THREE.MathUtils.clamp(toEnemy.dot(segment) / segmentLengthSq, 0, 1);
      const closest = start.clone().addScaledVector(segment, t);
      if (closest.distanceTo(enemy.group.position) <= 1.25 + ENEMY_DEFINITIONS[enemy.kind].radius) {
        this.applyProjectileHit(round, enemy, closest);
      }
    }
    tower.skillTimer = 2.2;
    tower.pulse = 0.6;
    this.audio.special();
    this.showToast(`Thương Nexus đã phóng đạn ${elementList(elements)}.`, 'reaction');
  }

  private createLanceVfx(start: THREE.Vector3, end: THREE.Vector3, elements: readonly Element[]): void {
    const direction = end.clone().sub(start);
    const length = direction.length();
    const mid = start.clone().add(end).multiplyScalar(0.5);
    const color = this.mixedColor(elements);
    const group = new THREE.Group();
    const beam = new THREE.Mesh(
      new THREE.CylinderGeometry(0.42, 0.64, length, 12, 1, true),
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.68, blending: THREE.AdditiveBlending, depthWrite: false }),
    );
    beam.position.copy(mid);
    beam.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), direction.normalize());
    group.add(beam);
    for (let index = 0; index < 5; index += 1) {
      const ring = new THREE.Mesh(new THREE.TorusGeometry(0.7 + index * 0.08, 0.055, 6, 28), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.8, depthWrite: false }));
      ring.position.lerpVectors(start, end, (index + 0.5) / 5);
      ring.quaternion.copy(beam.quaternion);
      ring.rotateX(Math.PI / 2);
      group.add(ring);
    }
    this.effectsGroup.add(group);
    this.effects.push({ object: group, life: 0.34, maxLife: 0.34, rises: false });
  }

  private checkWaveEnd(): void {
    if (this.phase !== 'wave') return;
    const waves = this.activeStage().waves;
    const wave = waves[this.waveIndex];
    if (this.spawnCursor < wave.orders.length || this.enemies.length > 0) return;
    this.money += wave.clearBonus;
    this.waveIndex += 1;
    if (this.waveIndex >= waves.length) {
      this.endRun(true);
      return;
    }
    if (this.activeStage().tutorial) {
      this.tutorialStep = this.waveIndex === 1 ? 2 : 6;
      this.selectedTowerId = null;
      this.interactionMode = 'inspect';
      this.refreshSelectionVisual();
    }
    this.setPhase('ready');
    this.showToast(`Đã dọn sạch đợt · +${wave.clearBonus} Arcana.`, 'good');
    this.audio.waveClear();
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
    this.getButton('#result-restart').textContent = won && this.stageIndex < STAGES.length - 1 ? 'Vào màn 2' : 'Chơi lại màn';
    if (won) this.audio.win();
    else this.audio.lose();
    this.updateUi(true);
  }

  private resetRun(): void {
    this.cancelBuildDrag(true);
    for (const projectile of [...this.projectiles]) {
      const index = this.projectiles.indexOf(projectile);
      if (index >= 0) this.removeProjectile(index);
    }
    for (const enemy of this.enemies) {
      this.scene.remove(enemy.group);
      this.disposeEnemy(enemy);
    }
    this.enemies.length = 0;
    for (const tower of this.towers) this.scene.remove(tower.group);
    this.towers.length = 0;
    this.towerPickables.length = 0;
    this.occupied.clear();
    this.networkGroup.clear();
    this.selectionGroup.clear();
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
    this.impactParticleStates.length = 0;
    this.impactParticles.geometry.setDrawRange(0, 0);
    for (const mesh of this.statusIconMeshes.values()) mesh.count = 0;
    this.statusIconBackdrop.count = 0;
    this.tutorialStep = 0;
    this.tutorialDirectShots = 0;
    this.stageTwoAmplifierIntroduced = false;
    this.stageTwoLanceIntroduced = false;
    this.stopHeldRotation();
    this.stageCleared = false;
    this.selectedTowerId = null;
    this.selectedBuildType = null;
    this.inspectedBuildType = null;
    this.selectedWaveEnemyKind = null;
    this.hoveredWaveEnemyKind = null;
    this.interactionMode = 'inspect';
    this.phase = 'ready';
    this.resultElement.classList.add('hidden');
    if (this.stageIndex === 1) {
      this.controls.target.set(0, 1.6, -0.4);
      this.camera.position.set(25, 28, 28);
    } else {
      this.controls.target.set(0, 1.7, -0.8);
      this.camera.position.set(19, 22, 21);
    }
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
      if (effect.rises) effect.object.position.y += delta * 1.25;
      effect.object.scale.setScalar(1 + (1 - ratio) * 0.6);
      effect.object.traverse((child) => {
        if (!(child instanceof THREE.Mesh || child instanceof THREE.Sprite)) return;
        const material = child.material;
        if (material instanceof THREE.Material) material.opacity = ratio;
        const direction = child.userData.direction as THREE.Vector3 | undefined;
        if (direction) child.position.addScaledVector(direction, delta * 1.7);
      });
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
      if (isAmmoEmitter(tower.type) || tower.type === 'lance') this.applyTowerAimVisual(tower);
      const aimedReceiver = this.findAimedReceiver(tower);
      if (!aimedReceiver) continue;
      const target = aimedReceiver.tower;
      const start = this.towerPort(tower);
      const end = this.towerPort(target);
      const available = target.buffer.length < this.capacity(target);
      const color = available ? this.towerSignalColor(tower) : 0xff3f55;
      const geometry = new THREE.BufferGeometry().setFromPoints([start, end]);
      const material = new THREE.LineBasicMaterial({ color, transparent: true, opacity: available ? 0.58 : 0.88 });
      const line = new THREE.Line(geometry, material);
      line.name = 'tower-link-line';
      this.networkGroup.add(line);
      const arrow = new THREE.Mesh(new THREE.ConeGeometry(0.17, 0.52, 7), new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.84 }));
      arrow.name = 'tower-link-arrow';
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
      const position = gridToWorld(candidate.gx, candidate.gz, candidate.layer).add(new THREE.Vector3(0, 0.055, 0));
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
      const radius = tower.type === 'amplifier' ? this.amplifierRange(tower) : this.connectionRange(tower);
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
      const position = gridToWorld(footprintCell.gx, footprintCell.gz, footprintCell.layer).add(new THREE.Vector3(0, 0.075, 0));
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
    const previewRange = type === 'amplifier' ? definition.connectionRange : definition.connectionRange;
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
    const radius = tower.type === 'amplifier' ? this.amplifierRange(tower) : this.connectionRange(tower);
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
    if (isAmmoEmitter(tower.type) || tower.type === 'lance') {
      const aimGuide = new THREE.Mesh(
        new THREE.CylinderGeometry(SELECTED_AIM_GUIDE_RADIUS, SELECTED_AIM_GUIDE_RADIUS, 1, 10, 1, true),
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
    if (tower.id !== this.selectedTowerId) return;
    const guide = this.selectionGroup.getObjectByName('weapon-aim-selected');
    if (!(guide instanceof THREE.Mesh)) return;
    const start = this.towerPort(tower);
    const direction = new THREE.Vector3(Math.cos(tower.aimAngle), 0, Math.sin(tower.aimAngle));
    const end = start.clone().addScaledVector(direction, this.connectionRange(tower));
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
    this.getElement('#layer-value').textContent = this.selectedTower() ? String(this.selectedTower()?.layer) : '—';
    const waveButton = this.getButton('#start-wave');
    const tutorialStartStep = TUTORIAL_WAVE_START_STEPS[this.waveIndex];
    waveButton.disabled = this.phase !== 'ready'
      || (this.activeStage().tutorial && (tutorialStartStep === undefined || this.tutorialStep !== tutorialStartStep))
      || this.stageTwoRequiredTower() !== null;
    waveButton.textContent = this.phase === 'ready' ? `Bắt đầu đợt ${this.waveIndex + 1}` : this.phase === 'wave' ? 'Đợt đang diễn ra' : this.phase === 'paused' ? 'Đã tạm dừng' : 'Màn đã hoàn thành';
    this.getButton('#pause-button').textContent = this.phase === 'paused' ? '▶' : 'Ⅱ';
    this.getButton('#speed-button').textContent = `×${GAME_SPEEDS[this.speedIndex]}`;
    this.getButton('#sound-button').textContent = this.audio.isMuted() ? '🔇' : '◖))';
    this.toastElement.textContent = this.lastToast.text;
    this.toastElement.dataset.tone = this.lastToast.tone;
    this.toastElement.classList.toggle('hidden', this.lastToast.text.length === 0);
    for (const button of this.buildList.querySelectorAll<HTMLButtonElement>('[data-tower-type]')) {
      const type = button.dataset.towerType as TowerType;
      const unlocked = this.isTowerUnlocked(type);
      button.disabled = !unlocked || !this.canPurchaseTower(type);
      button.classList.toggle('locked', !unlocked);
      button.dataset.locked = unlocked ? 'false' : 'true';
      button.classList.toggle('selected', this.interactionMode === 'build' && this.selectedBuildType === type);
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
    const catalogDefinition = tower || this.inspectedBuildType === null ? null : TOWER_DEFINITIONS[this.inspectedBuildType];
    const detailStats = this.getElement('#tower-detail-stats');
    const closeDetail = this.getButton('#inspector-close-detail');
    const catalogView = catalogDefinition !== null;
    this.inspectorElement.classList.toggle('tutorial-stage', this.activeStage().tutorial);
    this.inspectorElement.classList.toggle('catalog-view', catalogView);
    this.inspectorElement.classList.toggle('empty', !tower && !catalogView);
    this.getElement('#inspector-heading').textContent = catalogView ? 'CHI TIẾT TRỤ' : 'NÚT ĐẠN';
    detailStats.classList.toggle('hidden', !catalogView);
    closeDetail.classList.toggle('hidden', !catalogView);
    if (catalogDefinition && this.inspectedBuildType) {
      const unlocked = this.isTowerUnlocked(this.inspectedBuildType);
      const range = this.inspectedBuildType === 'lance' ? 21 : catalogDefinition.connectionRange;
      const capacity = this.inspectedBuildType === 'amplifier' ? 'Hào quang' : `${catalogDefinition.capacity} ô`;
      this.getElement('#inspector-name').textContent = catalogDefinition.name;
      this.getElement('#inspector-role').textContent = `${catalogDefinition.role} · ${unlocked ? 'Đã mở' : 'Chưa mở'}`;
      detailStats.innerHTML = `<div><dt>GIÁ</dt><dd>${catalogDefinition.cost}</dd></div><div><dt>VÙNG ĐẶT</dt><dd>${catalogDefinition.footprint[0]}×${catalogDefinition.footprint[1]}</dd></div><div><dt>TẦM</dt><dd>${range.toFixed(1)}</dd></div><div><dt>KHO</dt><dd>${capacity}</dd></div>`;
      this.getElement('#inspector-detail').textContent = `${catalogDefinition.description}\n\nNâng cấp: ${this.towerUpgradeSummary(this.inspectedBuildType)}`;
      this.getElement('#branch-controls').classList.add('hidden');
      return;
    }
    if (!tower) {
      this.getElement('#inspector-name').textContent = 'Chưa chọn trụ';
      this.getElement('#inspector-role').textContent = 'Chạm vào trụ để xem đường bắn.';
      this.getElement('#buffer-fill').style.width = '0%';
      this.getElement('#buffer-text').textContent = '0 / 0';
      this.getElement('#inspector-detail').textContent = 'Có thể xây khi đợt đang diễn ra. Nhấn 1–7 để chọn nhanh trụ.';
      this.getElement('#branch-controls').classList.add('hidden');
      this.getButton('#action-left').classList.toggle('hidden', this.activeStage().tutorial);
      this.getButton('#action-right').classList.toggle('hidden', this.activeStage().tutorial);
      return;
    }
    const definition = TOWER_DEFINITIONS[tower.type];
    const capacity = this.capacity(tower);
    const occupancy = tower.buffer.length;
    this.getElement('#inspector-name').textContent = `${definition.name} · C${tower.level}`;
    this.getElement('#inspector-role').textContent = `${definition.role} · Tầng ${tower.layer}`;
    this.getElement('#buffer-fill').style.width = `${capacity === 0 ? 0 : Math.min(100, occupancy / capacity * 100)}%`;
    this.getElement('#buffer-fill').style.setProperty('--buffer-color', `#${this.towerSignalColor(tower).toString(16).padStart(6, '0')}`);
    this.getElement('#buffer-text').textContent = capacity === 0 ? 'Hào quang' : `${tower.buffer.length} / ${capacity}`;
    const aimedReceiver = this.findAimedReceiver(tower)?.tower ?? null;
    const head = tower.buffer[0];
    const state = tower.blockedReason ? `BỊ CHẶN: ${tower.blockedReason}` : this.phase === 'paused' ? 'Đã tạm dừng' : 'Luồng đạn sẵn sàng';
    const angle = Math.round(THREE.MathUtils.radToDeg(tower.aimAngle));
    const output = aimedReceiver
      ? `Đường đạn đi xuyên → ${TOWER_DEFINITIONS[aimedReceiver.type].shortName}`
      : isAmmoEmitter(tower.type) ? `Bắn tự do · ${angle}°`
        : tower.type === 'lance' ? `Hướng kỹ năng · ${angle}°` : 'Hào quang hỗ trợ';
    this.getElement('#inspector-detail').textContent = `${state}\n${output}\nĐạn: ${head ? elementList(head.elements) : 'trống'}\nTầm bắn: ${this.connectionRange(tower).toFixed(1)} · ${definition.description}`;
    const branchControls = this.getElement('#branch-controls');
    branchControls.classList.toggle('hidden', tower.type !== 'amplifier' || this.activeStage().tutorial);
    this.getButton('#branch-power').classList.toggle('active', tower.amplifierBranch === 'power');
    this.getButton('#branch-throughput').classList.toggle('active', tower.amplifierBranch === 'throughput');
    const rotateLeftButton = this.getButton('#action-left');
    const rotateRightButton = this.getButton('#action-right');
    const tutorialRotationStep = this.tutorialRotationTowerType() !== null;
    rotateLeftButton.classList.toggle('hidden', this.activeStage().tutorial && !tutorialRotationStep);
    rotateRightButton.classList.toggle('hidden', this.activeStage().tutorial && !tutorialRotationStep);
    rotateLeftButton.disabled = !isAmmoEmitter(tower.type) && tower.type !== 'lance';
    rotateRightButton.disabled = !isAmmoEmitter(tower.type) && tower.type !== 'lance';
    const upgradeCost = definition.upgradeCost + (tower.level - 1) * 28;
    this.getButton('#action-upgrade').disabled = this.activeStage().tutorial || tower.level >= MAX_TOWER_LEVEL || this.money < upgradeCost;
    this.getButton('#action-upgrade').textContent = tower.level >= MAX_TOWER_LEVEL ? 'Cấp tối đa' : `Nâng ${upgradeCost}`;
    this.getButton('#action-move').textContent = `Dời ${definition.moveCost}`;
    this.getButton('#action-move').disabled = this.activeStage().tutorial || this.money < definition.moveCost;
    this.getButton('#action-sell').textContent = `Bán ${Math.floor(tower.totalInvested * SELL_REFUND)}`;
    this.getButton('#action-sell').disabled = this.activeStage().tutorial;
  }

  private towerUpgradeSummary(type: TowerType): string {
    if (type === 'foundry') return 'tăng sát thương, tốc độ sinh đạn, tầm bắn và sức chứa; cấp 3 sinh hai viên mỗi nhịp.';
    if (type === 'amplifier') return 'mở rộng hào quang và tăng hiệu lực của nhánh Sức Mạnh hoặc Tốc Độ.';
    if (type === 'lance') return 'tăng sát thương và tầm kỹ năng, đồng thời giảm số đạn cần để kích hoạt.';
    return 'tăng tầm bắn, nhịp truyền, sức chứa và lực nguyên tố cộng vào viên đạn.';
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
    if (!preserveRoster) {
      roster.innerHTML = kinds.map((kind) => {
        const definition = ENEMY_DEFINITIONS[kind];
        const selected = detailKind === kind;
        const count = counts.get(kind) ?? 0;
        const movement = definition.layer === 0 ? 'MẶT ĐẤT' : `BAY · TẦNG ${definition.layer}`;
        const movementLabel = definition.layer === 0 ? 'mặt đất' : `bay ở tầng ${definition.layer}`;
        return `<button class="wave-enemy-chip${selected ? ' active' : ''}" type="button" data-enemy-kind="${kind}" aria-expanded="${selected}" aria-controls="wave-enemy-detail" aria-label="Xem ${definition.name}, ${count} kẻ sắp tới, ${movementLabel}"><i style="--enemy-color:#${definition.color.toString(16).padStart(6, '0')}">${ENEMY_GLYPHS[kind]}</i><span>${definition.name}</span><b>×${count}</b><em data-flight="${definition.layer === 0 ? 'ground' : 'flying'}">${movement}</em></button>`;
      }).join('');
    } else {
      for (const button of roster.querySelectorAll<HTMLButtonElement>('[data-enemy-kind]')) {
        const active = button.dataset.enemyKind === detailKind;
        button.classList.toggle('active', active);
        button.setAttribute('aria-expanded', String(active));
      }
    }

    if (detailKind === null) {
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
    if (profiles.length === 0) profiles.push('<span data-tone="neutral">Không có kháng tính nguyên tố</span>');
    const reward = Math.round(definition.reward * this.activeStage().killRewardMultiplier);
    const movementBadge = definition.layer === 0 ? 'MẶT ĐẤT' : 'BAY TRÊN KHÔNG';
    const movementDetail = definition.layer === 0 ? 'Di chuyển trên đường bộ' : `Tầng bay ${definition.layer}`;
    detail.innerHTML = `<div class="enemy-detail-title"><i style="--enemy-color:${color}">${ENEMY_GLYPHS[definition.kind]}</i><div><span class="enemy-movement-badge" data-flight="${definition.layer === 0 ? 'ground' : 'flying'}">${movementBadge}</span><strong>${definition.name}</strong><small>${movementDetail}</small></div></div><dl class="enemy-detail-stats"><div><dt>MÁU</dt><dd>${definition.hp}</dd></div><div><dt>TỐC</dt><dd>${definition.speed.toFixed(2)}</dd></div><div><dt>MẠNG</dt><dd>−${definition.nexusDamage}</dd></div><div><dt>THƯỞNG</dt><dd>+${reward}</dd></div></dl><div class="enemy-detail-profile">${profiles.join('')}</div><button class="enemy-detail-close" type="button" data-close-wave-intel aria-label="Đóng chi tiết kẻ địch">×</button>`;
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
    const foundry = this.towers.find((tower) => tower.type === 'foundry');
    const fire = this.towers.find((tower) => tower.type === 'fire');
    const ice = this.towers.find((tower) => tower.type === 'ice');
    if (this.tutorialStep === 0) {
      focusSelector = '[data-tower-type="foundry"]';
      worldPosition = this.findTutorialPlacementWorld('foundry');
      dragPlacement = true;
    } else if (this.tutorialStep === 1) {
      focusSelector = '#start-wave';
    } else if (this.tutorialStep === 2) {
      focusSelector = '[data-tower-type="fire"]';
      worldPosition = this.findTutorialPlacementWorld('fire');
      dragPlacement = true;
    } else if (this.tutorialStep === 3) {
      if (this.phase === 'ready') focusSelector = '#start-wave';
    } else if (this.tutorialStep === 4) {
      if (this.selectedTowerId !== foundry?.id) worldPosition = foundry?.group.position.clone() ?? null;
      else focusSelector = '#action-right';
    } else if (this.tutorialStep === 5) {
      if (!this.isTutorialFireAimComplete()) {
        if (this.selectedTowerId !== fire?.id) worldPosition = fire?.group.position.clone() ?? null;
        else focusSelector = '#action-right';
      }
    } else if (this.tutorialStep === 6) {
      focusSelector = '[data-tower-type="ice"]';
      worldPosition = this.findTutorialPlacementWorld('ice');
      dragPlacement = true;
    } else if (this.tutorialStep === 7) {
      if (this.selectedTowerId !== fire?.id) worldPosition = fire?.group.position.clone() ?? null;
      else focusSelector = '#action-left';
    } else if (this.tutorialStep === 8) {
      if (this.selectedTowerId !== ice?.id) worldPosition = ice?.group.position.clone() ?? null;
      else focusSelector = '#action-left';
    } else if (this.tutorialStep === 9) focusSelector = '#start-wave';

    const cueKey = worldPosition
      ? `${this.tutorialStep}:${worldPosition.x.toFixed(2)}:${worldPosition.y.toFixed(2)}:${worldPosition.z.toFixed(2)}`
      : '';
    this.presentTutorialCue(focusSelector, worldPosition, dragPlacement, cueKey);
  }

  private updateStageTwoTutorialCue(visible: boolean): void {
    if (!visible || !this.isStageTwoLessonWave()) {
      this.hideTutorialHand();
      this.clearTutorialCue();
      return;
    }
    const required = this.stageTwoRequiredTower();
    const focusSelector = required ? `[data-tower-type="${required}"]` : '#start-wave';
    const worldPosition = required ? this.findStageTwoLessonPlacementWorld(required) : null;
    const cueKey = worldPosition
      ? `stage2:${this.waveIndex}:${required}:${worldPosition.x.toFixed(2)}:${worldPosition.y.toFixed(2)}:${worldPosition.z.toFixed(2)}`
      : '';
    this.presentTutorialCue(focusSelector, worldPosition, required !== null, cueKey);
  }

  private presentTutorialCue(
    focusSelector: string,
    worldPosition: THREE.Vector3 | null,
    dragPlacement: boolean,
    cueKey: string,
  ): void {
    if (focusSelector) {
      const focus = document.querySelector<HTMLElement>(focusSelector);
      focus?.classList.add('tutorial-focus');
      focus?.setAttribute('data-tutorial-focus', 'true');
    }
    this.updateTutorialHand(focusSelector, worldPosition, dragPlacement);
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

  private findStageTwoLessonCell(type: 'amplifier' | 'lance'): CellState | null {
    let best: { cell: CellState; score: number } | null = null;
    for (let gx = 0; gx < BOARD_WIDTH; gx += 1) {
      for (let gz = 0; gz < BOARD_DEPTH; gz += 1) {
        const placement = this.validateFootprint(type, gx, gz, null);
        const cell = this.cells.get(gridKey(gx, gz));
        if (!placement.valid || placement.layer === null || !cell) continue;
        const clientPoint = this.worldToClient(gridToWorld(cell.gx, cell.gz, cell.layer));
        if (!clientPoint) continue;
        const visibleHit = this.findCellAt(clientPoint.x, clientPoint.y);
        const topElement = document.elementFromPoint(clientPoint.x, clientPoint.y);
        if (!visibleHit || visibleHit.cell.gx !== cell.gx || visibleHit.cell.gz !== cell.gz || topElement !== this.canvas) continue;
        const keys = this.footprintKeys(type, gx, gz);
        const positions = keys.map((key) => {
          const footprintCell = this.cells.get(key);
          return footprintCell ? gridToWorld(footprintCell.gx, footprintCell.gz, footprintCell.layer) : null;
        }).filter((position): position is THREE.Vector3 => position !== null);
        if (positions.length !== keys.length) continue;
        const center = positions.reduce((sum, position) => sum.add(position), new THREE.Vector3()).multiplyScalar(1 / positions.length);
        let score = -center.length() * 0.18;
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
    return best?.cell ?? null;
  }

  private findStageTwoLessonPlacementWorld(type: 'amplifier' | 'lance'): THREE.Vector3 | null {
    const cell = this.findStageTwoLessonCell(type);
    return cell ? gridToWorld(cell.gx, cell.gz, cell.layer) : null;
  }

  private findTutorialPlacementWorld(type: TowerType): THREE.Vector3 | null {
    if (type !== 'foundry' && type !== 'fire' && type !== 'ice') return null;
    const target = TUTORIAL_TOWER_CELLS[type];
    const cell = this.cells.get(gridKey(target.gx, target.gz));
    return cell ? gridToWorld(cell.gx, cell.gz, cell.layer) : null;
  }

  private updateTutorialHand(focusSelector: string, worldPosition: THREE.Vector3 | null, dragPlacement: boolean): void {
    const worldTarget = worldPosition ? this.worldToClient(worldPosition.clone().add(new THREE.Vector3(0, 0.45, 0))) : null;
    const focus = focusSelector ? document.querySelector<HTMLElement>(focusSelector) : null;
    const focusRect = focus?.getBoundingClientRect();
    const uiTarget = focusRect ? {
      x: focusRect.left + focusRect.width * 0.72,
      y: focusRect.top + focusRect.height * 0.42,
    } : null;
    const start = dragPlacement ? uiTarget : worldTarget ?? uiTarget;
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

  private aimTowerAt(source: TowerState, target: TowerState): void {
    source.aimAngle = Math.atan2(
      target.group.position.z - source.group.position.z,
      target.group.position.x - source.group.position.x,
    );
    this.applyTowerAimVisual(source);
  }

  private projectileReceiverRadius(tower: TowerState): number {
    const footprint = TOWER_DEFINITIONS[tower.type].footprint;
    return 0.72 + (Math.max(footprint[0], footprint[1]) - 1) * 0.38;
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
    const definition = TOWER_DEFINITIONS[tower.type];
    if (tower.type === 'amplifier') return 0;
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
        if (name === 'active-play') this.createDeterministicDemo(0);
        else if (name === 'stress') this.createDeterministicDemo(5);
        else if (name === 'stage-two-ready') this.switchStage(1);
        else if (name === 'stage-two-wave-three') this.createStageTwoLessonDemo(2);
        else if (name === 'stage-two-wave-four') this.createStageTwoLessonDemo(3);
        else if (name === 'tutorial-rotation') this.createTutorialDemo(false, false);
        else if (name === 'tutorial-ready') this.createTutorialDemo(false, true);
        else if (name === 'tutorial-wave') this.createTutorialDemo(true, true);
        else if (name === 'status-fire') this.createElementStatusDemo(false);
        else if (name === 'status-reaction') this.createElementStatusDemo(true);
        else if (name === 'reward-stage-one') this.createRewardDemo(0);
        else if (name === 'reward-stage-two') this.createRewardDemo(1);
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
      hideDebugUi: () => undefined,
      getCellClientPoint: (gx: number, gz: number) => this.getCellClientPoint(gx, gz),
    };
  }

  private getCellClientPoint(gx: number, gz: number): { x: number; y: number } | null {
    const cell = this.cells.get(gridKey(gx, gz));
    if (!cell) return null;
    this.camera.updateMatrixWorld();
    const projected = gridToWorld(cell.gx, cell.gz, cell.layer).project(this.camera);
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

  private createRewardDemo(stageIndex: 0 | 1): void {
    if (this.stageIndex !== stageIndex) this.switchStage(stageIndex);
    else this.resetRun();
    this.money = 0;
    this.spawnEnemy('riftling', 0);
    this.killEnemy(0, '');
    this.updateUi(true);
    this.publishDiagnostics();
  }

  private createTutorialDemo(startWave: boolean, completeRotation: boolean): void {
    if (this.stageIndex !== 0) this.switchStage(0);
    else this.resetRun();
    this.money = 9999;
    this.tryPlaceTower('foundry', TUTORIAL_TOWER_CELLS.foundry.gx, TUTORIAL_TOWER_CELLS.foundry.gz);
    this.tutorialStep = 2;
    this.tryPlaceTower('fire', TUTORIAL_TOWER_CELLS.fire.gx, TUTORIAL_TOWER_CELLS.fire.gz);
    const foundry = this.towers.find((tower) => tower.type === 'foundry');
    const fire = this.towers.find((tower) => tower.type === 'fire');
    if (foundry) {
      foundry.aimAngle = TUTORIAL_FOUNDRY_TO_FIRE_ANGLE;
      if (startWave) {
        while (foundry.buffer.length < this.capacity(foundry)) foundry.buffer.push(this.createNeutralRound(foundry));
      }
    }
    this.tutorialStep = 6;
    this.tryPlaceTower('ice', TUTORIAL_TOWER_CELLS.ice.gx, TUTORIAL_TOWER_CELLS.ice.gz);
    const ice = this.towers.find((tower) => tower.type === 'ice');
    if (fire) fire.aimAngle = TUTORIAL_FIRE_TO_ICE_ANGLE;
    if (ice) {
      ice.aimAngle = completeRotation ? TUTORIAL_ICE_HEAD_ON_ANGLE : 0;
      this.applyTowerAimVisual(ice);
      this.tutorialStep = completeRotation ? 9 : 8;
      this.selectedTowerId = completeRotation && window.matchMedia('(max-width: 700px) and (orientation: portrait)').matches ? null : ice.id;
    }
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

  private createDeterministicDemo(waveIndex: number): void {
    if (this.stageIndex !== 1) this.switchStage(1);
    else this.resetRun();
    this.money = 9999;
    const layout: readonly [TowerType, number, number][] = [
      ['foundry', 0, 2], ['fire', 2, 2], ['ice', 2, 0], ['lance', 0, 0],
      ['amplifier', 6, 1], ['foundry', 5, 4], ['wind', 7, 4],
      ['foundry', 1, 4], ['earth', 2, 4],
    ];
    for (const [type, gx, gz] of layout) this.tryPlaceTower(type, gx, gz);
    const [groundFoundry, fire, ice, lance, amplifier, airFoundry, wind, highFoundry, earth] = this.towers;
    if (groundFoundry && fire) this.aimTowerAt(groundFoundry, fire);
    if (fire && ice) this.aimTowerAt(fire, ice);
    if (ice && lance) this.aimTowerAt(ice, lance);
    if (airFoundry && wind) this.aimTowerAt(airFoundry, wind);
    if (highFoundry && earth) this.aimTowerAt(highFoundry, earth);
    if (amplifier) amplifier.amplifierBranch = 'throughput';
    if (wind) wind.aimAngle = Math.PI / 2;
    if (earth) earth.aimAngle = 0;
    if (lance) lance.aimAngle = Math.atan2(3, 6);
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
    if (foundry && fire) this.aimTowerAt(foundry, fire);
    if (fire && ice) this.aimTowerAt(fire, ice);
    if (waveIndex === 3) this.tryPlaceTower('amplifier', 6, 1);
    this.waveIndex = waveIndex;
    this.money = waveIndex === 2 ? 35 : 45;
    this.selectedTowerId = null;
    this.interactionMode = 'inspect';
    this.refreshNetworkVisuals();
    this.refreshSelectionVisual();
    this.updateUi(true);
  }

  private publishDiagnostics(): void {
    const info = this.renderer.info;
    const connections = this.towers.filter((tower) => this.findAimedReceiver(tower) !== null).length;
    const blocked = this.towers.filter((tower) => tower.blockedReason.length > 0).length;
    const elementalStatuses = this.enemies.reduce((count, enemy) => count + this.activeEnemyElements(enemy).length, 0);
    const tintedEnemies = this.enemies.filter((enemy) => this.activeEnemyElements(enemy).length > 0).length;
    const statusIcons = [...this.statusIconMeshes.values()].reduce((count, mesh) => count + mesh.count, 0);
    const tutorialTerminalType: TowerType = this.waveIndex <= 0 ? 'foundry' : this.waveIndex === 1 ? 'fire' : 'ice';
    const tutorialTerminal = this.activeStage().tutorial
      ? this.towers.find((tower) => tower.type === tutorialTerminalType) ?? null
      : null;
    const firstPathDirection = this.pathXZ.length > 1 ? this.pathXZ[1].clone().sub(this.pathXZ[0]).normalize() : null;
    const tutorialHeadOnDot = tutorialTerminal && firstPathDirection
      ? firstPathDirection.dot(new THREE.Vector2(Math.cos(tutorialTerminal.aimAngle), Math.sin(tutorialTerminal.aimAngle)))
      : null;
    const requiredTutorialTower = this.stageTwoRequiredTower();
    const lessonCell = requiredTutorialTower === 'amplifier' || requiredTutorialTower === 'lance'
      ? this.findStageTwoLessonCell(requiredTutorialTower)
      : null;
    const waveThreats = this.activeStage().waves.map((wave) => Math.round(wave.orders.reduce((total, order) => {
      const enemy = ENEMY_DEFINITIONS[order.kind];
      return total + enemy.hp * (1 + enemy.speed * 0.15) + enemy.nexusDamage * 15;
    }, 0)));
    window.__THREE_GAME_DIAGNOSTICS__ = {
      frame: this.frame,
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
      draggingTower: this.buildDrag?.dragging ?? false,
      pathRibbonMeshes: this.boardGroup.children.filter((child) => child.name.startsWith('enemy-path-')).length,
      tutorialHandVisible: !this.tutorialHandElement.classList.contains('hidden'),
      tutorialHandMode: this.tutorialHandElement.dataset.mode ?? '',
      maxTowerLayer: this.towers.reduce((max, tower) => Math.max(max, tower.layer), 0),
      layerOneTowerCount: this.towers.filter((tower) => tower.layer === 1).length,
      oppositeRaisedCellCount: [...this.cells.values()].filter((cell) => cell.layer === 1 && cell.gx <= 2).length,
      oppositeRaisedTowerCount: this.towers.filter((tower) => tower.layer === 1 && tower.gx <= 2).length,
      maxLayerOneTowerLaneDistance: this.towers.reduce((max, tower) => tower.layer === 1
        ? Math.max(max, this.distanceToEnemyPath(tower.group.position.x, tower.group.position.z))
        : max, 0),
      maxEnemyLayer: this.enemies.reduce((max, enemy) => Math.max(max, ENEMY_DEFINITIONS[enemy.kind].layer), 0),
      maxStageEnemyLayer: this.activeStage().waves.reduce((max, wave) => Math.max(max, ...wave.orders.map((order) => ENEMY_DEFINITIONS[order.kind].layer)), 0),
      maxBoardLayer: [...this.cells.values()].reduce((max, cell) => Math.max(max, cell.layer), 0),
      maxEnemyFacingError: this.enemies.reduce((max, enemy) => Math.max(max, Number(enemy.group.userData.facingError) || 0), 0),
      maxEnemyLaneOffset: this.enemies.reduce((max, enemy) => Math.max(max, Math.abs(enemy.sideOffset)), 0),
      upcomingEnemyCount: this.phase === 'ready' ? this.activeStage().waves[Math.min(this.waveIndex, this.activeStage().waves.length - 1)].orders.length : 0,
      upcomingEnemyKinds: this.phase === 'ready' ? [...new Set(this.activeStage().waves[Math.min(this.waveIndex, this.activeStage().waves.length - 1)].orders.map((order) => order.kind))] : [],
      selectedWaveEnemyKind: this.selectedWaveEnemyKind,
      inspectedBuildType: this.inspectedBuildType,
      unlockedTowers: BUILD_ORDER.filter((type) => this.isTowerUnlocked(type)).length,
      connections,
      linkGuideObjects: this.networkGroup.children.filter((child) => child.name.startsWith('tower-link-')).length,
      weaponAimGuideObjects: this.selectionGroup.children.filter((child) => child.name.startsWith('weapon-aim-')).length,
      weaponAimGuideWidth: SELECTED_AIM_GUIDE_RADIUS * 2,
      weaponAimGuideOpacity: SELECTED_AIM_GUIDE_OPACITY,
      selectedOutputAngle: this.selectedTower()?.aimAngle ?? null,
      tutorialHeadOnDot,
      blocked,
      tutorialStep: this.tutorialStep,
      tutorialDirectShots: this.tutorialDirectShots,
      elementalTintStrength: ELEMENT_STATUS_TINT,
      stageStartingMoney: this.activeStage().startingMoney,
      killRewardMultiplier: this.activeStage().killRewardMultiplier,
      pathLength: this.pathTotalLength,
      waveCount: this.activeStage().waves.length,
      waveThreats,
      requiredTutorialTower,
      lessonCell: lessonCell ? { gx: lessonCell.gx, gz: lessonCell.gz } : null,
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
