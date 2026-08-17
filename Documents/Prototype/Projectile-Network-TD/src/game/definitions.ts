import * as THREE from 'three';

export const ELEMENTS = ['fire', 'ice', 'wind', 'earth'] as const;
export type Element = (typeof ELEMENTS)[number];
export type NodeType = 'generator' | Element | 'support' | 'special' | 'nexus';
export type PurchasableNodeType = NodeType;
export type EnemyKind =
  | 'swarm' | 'runner' | 'armored' | 'wisp'
  | 'warded' | 'bulwark' | 'skyWarder' | 'boss';
export type GamePhase = 'preparation' | 'wave' | 'paused' | 'victoryTravel' | 'won' | 'lost';
export type Branch =
  | 'rapid' | 'heavy' | 'conduit' | 'resonance' | 'buff' | 'debuff'
  | 'rapidPulse' | 'impactPulse' | 'suppression' | 'conduction';

export interface NodeDefinition {
  readonly type: NodeType; readonly name: string; readonly shortName: string;
  readonly role: string; readonly icon: string; readonly description: string;
  readonly cost: number; readonly upgradeCost: number; readonly color: number;
  readonly interval: number; readonly capacity: number; readonly connectionRange: number; readonly element?: Element;
  readonly branches: readonly [Branch, Branch] | null;
}

export interface SlotDefinition {
  readonly id: number; readonly position: readonly [number, number, number]; readonly tier: 'low' | 'high';
}

export interface TerrainPlatformDefinition {
  readonly center: readonly [number, number, number];
  readonly size: readonly [number, number, number];
  readonly buildPositions: readonly (readonly [number, number, number])[];
}

export type ReactionKey =
  | 'hellfire' | 'deepFreeze' | 'tempest' | 'shatter'
  | 'firestorm' | 'sandstorm' | 'permafrost' | 'steamBurst';

export interface Payload {
  readonly id: number; physicalDamage: number; magicDamage: number;
  baseElement: Element | null; reaction: ReactionKey | null;
  reactionProcAvailable: boolean; reactionPotency: number;
  readonly directHitEnemyIds: Set<number>;
}

export interface NodeState {
  readonly id: number; readonly type: NodeType; readonly group: THREE.Group;
  slotId: number | null;
  outputTargetId: number | null; inputSourceId: number | null; nexusInputSourceIds: number[];
  buffer: Payload[]; reservedIncoming: number; timer: number; charge: number; pulseCharge: number;
  totalInvested: number; branch: Branch | null; active: boolean; invalidReason: string;
}

export interface ProjectileState {
  readonly id: number; readonly payload: Payload; readonly group: THREE.Group; readonly trail: THREE.Points;
  readonly sourceNodeId: number; readonly targetNodeId: number;
  readonly start: THREE.Vector3; readonly end: THREE.Vector3; progress: number;
  readonly hitEnemyIds: Set<number>;
}

export interface EnemyDefinition {
  readonly kind: EnemyKind; readonly name: string; readonly icon: string;
  readonly hp: number; readonly armor: number; readonly mr: number; readonly speed: number;
  readonly radius: number; readonly reward: number; readonly leakDamage: number; readonly color: number;
  readonly layer: 0 | 1; readonly boss?: boolean;
  readonly resist?: readonly Element[]; readonly immune?: readonly Element[]; readonly vulnerable?: readonly Element[];
  readonly reactionBarrier?: ReactionKey; readonly barrierDamageMultiplier?: number; readonly speedAfterBarrierBreak?: number;
}

export interface EnemyState {
  readonly id: number; readonly kind: EnemyKind; readonly group: THREE.Group;
  hp: number; maxHp: number; progress: number; sideOffset: number;
  burnDps: number; burnTime: number; slow: number; slowTime: number; freezeTime: number; windTime: number;
  reactionCooldowns: Record<ReactionKey, number>;
  armorBreak: number; armorBreakTime: number; reactionBarrier: ReactionKey | null; barrierBroken: boolean;
  dead: boolean; hitFlash: number;
}

export interface SpawnOrder { readonly at: number; readonly kind: EnemyKind; readonly sideOffset: number; }
export interface WaveDefinition {
  readonly title: string; readonly clearReward: number; readonly healthMultiplier: number;
  readonly orders: readonly SpawnOrder[];
}

export interface StageDefinition {
  readonly title: string; readonly subtitle: string;
  readonly path: readonly (readonly [number, number])[];
  readonly startingGold: number; readonly startingBaseHp: number; readonly tutorial: boolean;
  readonly killRewardMultiplier: number;
  readonly rainChargeMultiplier: number;
  readonly board: {
    readonly width: number; readonly depth: number; readonly originX: number; readonly originZ: number;
    readonly buildMaxX: number; readonly islandRadius: number;
    readonly cameraPosition: readonly [number, number, number]; readonly cameraTarget: readonly [number, number, number];
  };
  readonly waves: readonly WaveDefinition[];
}

export interface ReactionDefinition {
  readonly key: ReactionKey; readonly name: string; readonly icon: string;
  readonly magicDamage: number; readonly color: number; readonly elements: readonly [Element, Element];
}

export const FIXED_STEP = 1 / 60;
export const MAX_LINK_RANGE = 12.6;
export const PROJECTILE_SPEED = 27.6;
export const PROJECTILE_RADIUS = 0.84;
export const PROJECTILE_VISUAL_SCALE = 2;
export const TOWER_FIRE_RATE_MULTIPLIER = 1.5;
export const ENEMY_SPEED_MULTIPLIER = 0.6;
export const ENEMY_REWARD_MULTIPLIER = 0.6;
export const WAVE_CLEAR_REWARD_MULTIPLIER = 0.65;
export const REACTION_MAX_HP_DAMAGE_RATIO = 0.06;
export const SELL_REFUND = 0.6;
export const MAX_SOUL = 50;
export const MASTERY_CHECKPOINT_GOLD = 340;
export const TOWER_PURCHASE_PRICE_GROWTH_PER_TOWER = 0.12;
export const TOWER_PURCHASE_PRICE_GROWTH_CAP = 1.2;

export const ELEMENT_COLORS: Record<Element, number> = {
  fire: 0xff5b46, ice: 0x6ae2ff, wind: 0x71efb5, earth: 0xe0af62,
};
export const ELEMENT_NAMES: Record<Element, string> = {
  fire: 'Hỏa', ice: 'Băng', wind: 'Phong', earth: 'Địa',
};
export const BRANCH_NAMES: Record<Branch, string> = {
  rapid: 'Dồn Dập', heavy: 'Trọng Đạn', conduit: 'Dẫn Đạn', resonance: 'Cộng Hưởng',
  buff: 'Tiếp Sức', debuff: 'Áp Lực', rapidPulse: 'Sấm Nhanh', impactPulse: 'Sấm Chấn',
  suppression: 'Quét Rộng', conduction: 'Đớp Mạnh',
};
export const BRANCH_DESCRIPTIONS: Record<Branch, string> = {
  rapid: 'Ra đạn mỗi 0,68 giây · 12 sát thương',
  heavy: 'Ra đạn mỗi 1,35 giây · 26 sát thương',
  conduit: 'Truyền nhanh hơn, uy lực nguyên tố thấp hơn',
  resonance: 'Truyền chậm hơn, uy lực phản ứng cao hơn',
  buff: 'Tăng tốc các trụ nguyên tố ở gần',
  debuff: 'Giảm giáp và kháng phép của địch ở gần',
  rapidPulse: 'Nổ nhỏ thường xuyên sau mỗi 3 đạn',
  impactPulse: 'Nổ lớn sau mỗi 7 đạn',
  suppression: 'Vùng đớp rộng 3,1 · sát thương thấp hơn',
  conduction: 'Vùng đớp 2,5 · sát thương kết liễu cao hơn',
};

export const NODE_DEFINITIONS: Record<NodeType, NodeDefinition> = {
  generator: {
    type: 'generator', name: 'Lò Đạn', shortName: 'Lò Đạn', role: 'Sinh đạn', icon: '◈',
    description: 'Đúc đạn Vật lý trung tính để truyền qua mạng trụ.', cost: 80, upgradeCost: 70,
    color: 0xe9d58a, interval: 0.92, capacity: Number.POSITIVE_INFINITY, connectionRange: 12.6, branches: ['rapid', 'heavy'],
  },
  fire: {
    type: 'fire', name: 'Trụ Hỏa', shortName: 'Hỏa', role: 'Biến đổi', icon: '◆',
    description: 'Viết lại đạn thành Hỏa và gieo Thiêu Đốt.', cost: 70, upgradeCost: 62,
    color: ELEMENT_COLORS.fire, interval: 0.72, capacity: Number.POSITIVE_INFINITY, connectionRange: 12, element: 'fire', branches: ['conduit', 'resonance'],
  },
  ice: {
    type: 'ice', name: 'Trụ Băng', shortName: 'Băng', role: 'Biến đổi', icon: '✦',
    description: 'Viết lại đạn thành Băng và làm Chậm.', cost: 70, upgradeCost: 62,
    color: ELEMENT_COLORS.ice, interval: 0.72, capacity: Number.POSITIVE_INFINITY, connectionRange: 12, element: 'ice', branches: ['conduit', 'resonance'],
  },
  wind: {
    type: 'wind', name: 'Trụ Phong', shortName: 'Phong', role: 'Biến đổi', icon: '➤',
    description: 'Viết lại đạn thành Phong và đẩy lùi.', cost: 72, upgradeCost: 64,
    color: ELEMENT_COLORS.wind, interval: 0.82, capacity: Number.POSITIVE_INFINITY, connectionRange: 12.45, element: 'wind', branches: ['conduit', 'resonance'],
  },
  earth: {
    type: 'earth', name: 'Trụ Địa', shortName: 'Địa', role: 'Biến đổi', icon: '⬟',
    description: 'Viết lại đạn thành Địa và phá Giáp.', cost: 72, upgradeCost: 64,
    color: ELEMENT_COLORS.earth, interval: 0.68, capacity: Number.POSITIVE_INFINITY, connectionRange: 11.7, element: 'earth', branches: ['conduit', 'resonance'],
  },
  support: {
    type: 'support', name: 'Cột Tiếp Sức', shortName: 'Tiếp Sức', role: 'Hỗ trợ', icon: '◉',
    description: 'Tích lực khi nhận đạn rồi chuyển tiếp nguyên trạng.', cost: 120, upgradeCost: 85,
    color: 0xb894ff, interval: 0.72, capacity: Number.POSITIVE_INFINITY, connectionRange: 8.1, branches: ['buff', 'debuff'],
  },
  special: {
    type: 'special', name: 'Trụ Sấm', shortName: 'Sấm', role: 'Nổ cục bộ', icon: '✹',
    description: 'Tích Sấm Lực khi nhận đạn và nổ quanh chính nó.', cost: 180, upgradeCost: 110,
    color: 0xff9f65, interval: 0.68, capacity: 8, connectionRange: 12, branches: ['rapidPulse', 'impactPulse'],
  },
  nexus: {
    type: 'nexus', name: 'Trống Gọi Mưa', shortName: 'Trống Mưa', role: 'Kết thúc & nạp Cóc', icon: '◉',
    description: 'Trụ duy nhất nhận tối đa hai chuỗi, tiêu thụ đạn và nạp đòn Bắt Mồi cho Cóc.', cost: 0, upgradeCost: 120,
    color: 0x68cfe8, interval: 0, capacity: Number.POSITIVE_INFINITY, connectionRange: 0, branches: ['suppression', 'conduction'],
  },
};

export const BUILD_ORDER: readonly PurchasableNodeType[] = ['nexus', 'generator', 'fire', 'ice', 'wind', 'earth', 'support', 'special'];

export const BUILD_GRID_SPACING = 2;

export const ENEMY_DEFINITIONS: Record<EnemyKind, EnemyDefinition> = {
  swarm: { kind: 'swarm', name: 'Bọ Hạn', icon: '●', hp: 54, armor: 0, mr: 0, speed: 2.05, radius: 0.5, reward: 11, leakDamage: 1, layer: 0, color: 0xc98236 },
  runner: { kind: 'runner', name: 'Gió Lào', icon: '➤', hp: 72, armor: 5, mr: 5, speed: 3.35, radius: 0.48, reward: 15, leakDamage: 1, layer: 0, color: 0xe6a23a, vulnerable: ['ice'] },
  armored: { kind: 'armored', name: 'Giáp Đất Nẻ', icon: '⬟', hp: 220, armor: 60, mr: 10, speed: 1.18, radius: 0.78, reward: 34, leakDamage: 3, layer: 0, color: 0x7f6650, resist: ['earth'], vulnerable: ['ice'] },
  wisp: { kind: 'wisp', name: 'Yêu Nắng', icon: '✦', hp: 92, armor: 0, mr: 12, speed: 2.25, radius: 0.54, reward: 20, leakDamage: 2, layer: 0, color: 0xe87842, immune: ['fire'], vulnerable: ['ice'] },
  warded: { kind: 'warded', name: 'Quan Giữ Mây', icon: '◇', hp: 420, armor: 10, mr: 60, speed: 1.05, radius: 0.92, reward: 75, leakDamage: 5, layer: 0, color: 0x687ea3, reactionBarrier: 'steamBurst' },
  bulwark: { kind: 'bulwark', name: 'Tướng Đá', icon: '▣', hp: 520, armor: 65, mr: 18, speed: 1.08, radius: 0.98, reward: 72, leakDamage: 5, layer: 0, color: 0x706557, reactionBarrier: 'steamBurst', barrierDamageMultiplier: 0.04, speedAfterBarrierBreak: 1.85 },
  skyWarder: { kind: 'skyWarder', name: 'Thiên Binh Hạn', icon: '✧', hp: 360, armor: 18, mr: 34, speed: 1.18, radius: 0.86, reward: 68, leakDamage: 4, layer: 0, color: 0xa66343, resist: ['fire'], vulnerable: ['earth'], reactionBarrier: 'sandstorm', barrierDamageMultiplier: 0.08 },
  boss: { kind: 'boss', name: 'Thiên Tướng Hạn', icon: '♛', hp: 980, armor: 35, mr: 35, speed: 0.72, radius: 1.18, reward: 165, leakDamage: 8, layer: 0, color: 0x50483a, boss: true, resist: ['fire', 'earth'], vulnerable: ['ice', 'wind'], reactionBarrier: 'permafrost', barrierDamageMultiplier: 0.12 },
};

const FORMATION_GAP_PATTERN = [0.35, 1.55, 0.12, 1.25, 0.42, 0.18, 1.65, 0.75, 1.28] as const;
const SIDE_OFFSETS = [0, -0.5, 0.45, 0.45, 0.08, -0.42, -0.42, 0.2, 0.52, -0.16] as const;
function sequence(kind: EnemyKind, start: number, count: number, interval: number): SpawnOrder[] {
  let at = start;
  return Array.from({ length: count }, (_, index) => {
    if (index > 0) at += interval * FORMATION_GAP_PATTERN[(index - 1) % FORMATION_GAP_PATTERN.length];
    return { at, kind, sideOffset: SIDE_OFFSETS[index % SIDE_OFFSETS.length] };
  });
}

const TUTORIAL_WAVES: readonly WaveDefinition[] = [
  { title: 'Đường Lên Cửa Trời', clearReward: 55, healthMultiplier: 1, orders: sequence('swarm', 0.8, 4, 1.35) },
  { title: 'Lửa Qua Đồng Hạn', clearReward: 75, healthMultiplier: 1.08, orders: sequence('swarm', 0.6, 6, 1.08) },
  { title: 'Gọi Mưa Đầu Tiên', clearReward: 105, healthMultiplier: 1.18, orders: [...sequence('swarm', 0.5, 5, 1.02), ...sequence('runner', 2.2, 2, 1.85)].sort((a, b) => a.at - b.at) },
  { title: 'Tự Dựng Mạng Đạn', clearReward: 105, healthMultiplier: 6, orders: [...sequence('swarm', 0.4, 9, 0.85), ...sequence('runner', 1.1, 3, 1.25)].sort((a, b) => a.at - b.at) },
  { title: 'Đồng Khô Dồn Ép', clearReward: 125, healthMultiplier: 10, orders: [...sequence('swarm', 0.3, 10, 0.72), ...sequence('runner', 0.8, 5, 0.9), ...sequence('armored', 2.2, 1, 1)].sort((a, b) => a.at - b.at) },
  { title: 'Cóc Tới Cửa Trời', clearReward: 150, healthMultiplier: 14, orders: [...sequence('swarm', 0.2, 12, 0.58), ...sequence('runner', 0.55, 7, 0.72), ...sequence('armored', 1.4, 2, 1.15)].sort((a, b) => a.at - b.at) },
];

const PRISMATIC_WAVES: readonly WaveDefinition[] = [
  { title: 'Đường Đất Nứt', clearReward: 50, healthMultiplier: 1.8, orders: sequence('swarm', 0.4, 10, 0.95) },
  {
    title: 'Gió Lào Dồn Ép', clearReward: 70, healthMultiplier: 2.4,
    orders: [...sequence('runner', 0.3, 8, 0.82), ...sequence('swarm', 0.8, 6, 0.96)].sort((a, b) => a.at - b.at),
  },
  {
    title: 'Cột Tiếp Sức', clearReward: 100, healthMultiplier: 3.5,
    orders: [...sequence('swarm', 0.25, 10, 0.46), ...sequence('runner', 0.75, 8, 0.66), ...sequence('wisp', 0.55, 6, 0.72)].sort((a, b) => a.at - b.at),
  },
  {
    title: 'Trụ Sấm', clearReward: 130, healthMultiplier: 5,
    orders: [...sequence('swarm', 0.15, 16, 0.34), ...sequence('runner', 0.4, 10, 0.44), ...sequence('wisp', 0.55, 8, 0.52)].sort((a, b) => a.at - b.at),
  },
  {
    title: 'Đạo Quân Khát Nước', clearReward: 170, healthMultiplier: 7.5,
    orders: [...sequence('runner', 0.12, 18, 0.29), ...sequence('armored', 0.45, 12, 0.43), ...sequence('wisp', 0.3, 16, 0.33)].sort((a, b) => a.at - b.at),
  },
  {
    title: 'Cửa Trời Khép Kín', clearReward: 240, healthMultiplier: 10.5,
    orders: [
      ...sequence('swarm', 0.08, 20, 0.22), ...sequence('runner', 0.24, 15, 0.25),
      ...sequence('armored', 0.4, 12, 0.36), ...sequence('wisp', 0.2, 12, 0.28),
      { at: 2.1, kind: 'warded', sideOffset: -0.25 } satisfies SpawnOrder,
      { at: 3.8, kind: 'warded', sideOffset: 0.3 } satisfies SpawnOrder,
    ].sort((a, b) => a.at - b.at),
  },
];

const CONVERGENCE_WAVES: readonly WaveDefinition[] = [
  {
    title: 'Đồng Khô Mở Rộng', clearReward: 80, healthMultiplier: 2,
    orders: [...sequence('swarm', 0.2, 15, 0.56), ...sequence('runner', 0.9, 5, 0.92)].sort((a, b) => a.at - b.at),
  },
  {
    title: 'Gió Hạn Xung Kích', clearReward: 105, healthMultiplier: 2.8,
    orders: [...sequence('swarm', 0.15, 18, 0.44), ...sequence('runner', 0.55, 11, 0.62)].sort((a, b) => a.at - b.at),
  },
  {
    title: 'Gọng Kìm Đất Nẻ', clearReward: 135, healthMultiplier: 4,
    orders: [
      ...sequence('runner', 0.12, 18, 0.34), ...sequence('wisp', 0.32, 12, 0.46),
      ...sequence('armored', 0.65, 12, 0.72), ...sequence('bulwark', 1.15, 4, 1.45),
    ].sort((a, b) => a.at - b.at),
  },
  {
    title: 'Thiên Binh Xuống Đất', clearReward: 175, healthMultiplier: 5.8,
    orders: [
      ...sequence('swarm', 0.08, 24, 0.29), ...sequence('wisp', 0.3, 20, 0.39),
      ...sequence('skyWarder', 0.75, 8, 0.88), ...sequence('bulwark', 0.6, 8, 0.92),
    ].sort((a, b) => a.at - b.at),
  },
  {
    title: 'Hạn Dồn Bốn Phía', clearReward: 215, healthMultiplier: 8,
    orders: [
      ...sequence('runner', 0.08, 26, 0.25), ...sequence('armored', 0.34, 18, 0.42),
      ...sequence('wisp', 0.2, 22, 0.3), ...sequence('bulwark', 0.5, 10, 0.68),
    ].sort((a, b) => a.at - b.at),
  },
  {
    title: 'Thiên Tướng Đầu Tiên', clearReward: 270, healthMultiplier: 11,
    orders: [
      ...sequence('swarm', 0.06, 34, 0.2), ...sequence('wisp', 0.18, 30, 0.25),
      ...sequence('skyWarder', 0.42, 10, 0.58), ...sequence('bulwark', 0.4, 14, 0.46),
      ...sequence('boss', 1.6, 4, 1.05),
    ].sort((a, b) => a.at - b.at),
  },
  {
    title: 'Đội Hình Cửa Trời', clearReward: 325, healthMultiplier: 14.5,
    orders: [
      ...sequence('armored', 0.16, 26, 0.28), ...sequence('wisp', 0.06, 46, 0.15),
      ...sequence('skyWarder', 0.35, 16, 0.42), ...sequence('bulwark', 0.28, 20, 0.34),
    ].sort((a, b) => a.at - b.at),
  },
  {
    title: 'Đại Quân Hạn', clearReward: 390, healthMultiplier: 19,
    orders: [
      ...sequence('runner', 0.06, 42, 0.15), ...sequence('wisp', 0.12, 42, 0.14),
      ...sequence('bulwark', 0.22, 28, 0.21), ...sequence('boss', 0.7, 12, 0.48),
    ].sort((a, b) => a.at - b.at),
  },
  {
    title: 'Ma Trận Nguyên Tố', clearReward: 470, healthMultiplier: 24.5,
    orders: [
      ...sequence('swarm', 0.04, 46, 0.11), ...sequence('armored', 0.14, 34, 0.16),
      ...sequence('skyWarder', 0.28, 21, 0.25), ...sequence('bulwark', 0.17, 35, 0.16),
    ].sort((a, b) => a.at - b.at),
  },
  {
    title: 'Cóc Kiện Trời', clearReward: 620, healthMultiplier: 31,
    orders: [
      ...sequence('runner', 0.03, 36, 0.085), ...sequence('armored', 0.1, 26, 0.12),
      ...sequence('wisp', 0.06, 32, 0.09), ...sequence('skyWarder', 0.19, 18, 0.18),
      ...sequence('bulwark', 0.14, 26, 0.13), ...sequence('boss', 0.52, 10, 0.38),
    ].sort((a, b) => a.at - b.at),
  },
];

export const STAGES: readonly StageDefinition[] = [
  {
    title: 'Đường Lên Cửa Trời', subtitle: 'Hướng dẫn mạng đạn trên đồng hạn',
    path: [[-11, -2], [-3, -2], [-3, 2], [7, 2]],
    startingGold: 420, startingBaseHp: 3, tutorial: true, killRewardMultiplier: 1, rainChargeMultiplier: 1,
    board: { width: 10, depth: 7, originX: -9, originZ: -6, buildMaxX: 6.6, islandRadius: 15.8, cameraPosition: [20.5, 24.5, -9.5], cameraTarget: [-1, 1.2, -0.4] },
    waves: TUTORIAL_WAVES,
  },
  {
    title: 'Đồng Nứt Khô Hạn', subtitle: 'Thử thách mạng đạn đường dài',
    path: [[-20, -6], [-4, -6], [-4, 5], [10, 5], [10, 0], [20, 0]],
    startingGold: 220, startingBaseHp: 20, tutorial: false, killRewardMultiplier: 1.5, rainChargeMultiplier: 0.5,
    board: { width: 12, depth: 9, originX: -11, originZ: -8, buildMaxX: 10.6, islandRadius: 24.2, cameraPosition: [25, 28, 28], cameraTarget: [0, 1.6, -0.4] },
    waves: PRISMATIC_WAVES,
  },
  {
    title: 'Sân Trời Cuối Hạn', subtitle: 'Mạng đạn trên chiến trường mở rộng',
    path: [[-28, -9], [-15, -9], [-15, 7], [-5, 7], [-5, -5], [8, -5], [8, 9], [20, 9], [20, 1], [28, 1]],
    startingGold: 220, startingBaseHp: 20, tutorial: false, killRewardMultiplier: 1.65, rainChargeMultiplier: 0.35,
    board: { width: 20, depth: 14, originX: -19, originZ: -13, buildMaxX: 19.6, islandRadius: 31.5, cameraPosition: [35, 40, 41], cameraTarget: [0, 2.1, -0.5] },
    waves: CONVERGENCE_WAVES,
  },
];

const requestedStage = Number(new URLSearchParams(window.location.search).get('level') ?? '1');
export const ACTIVE_STAGE_INDEX = Number.isFinite(requestedStage) ? Math.max(0, Math.min(STAGES.length - 1, Math.floor(requestedStage) - 1)) : 0;
export const ACTIVE_STAGE = STAGES[ACTIVE_STAGE_INDEX];
export const STARTING_GOLD = ACTIVE_STAGE.startingGold;
export const STARTING_BASE_HP = ACTIVE_STAGE.startingBaseHp;
export const ENEMY_PATH = ACTIVE_STAGE.path;
export const WAVES = ACTIVE_STAGE.waves;
export const MAP_BOUNDS = {
  minX: Math.min(ACTIVE_STAGE.board.originX - 1.2, ...ENEMY_PATH.map(([x]) => x - 1.6)),
  maxX: Math.max(ACTIVE_STAGE.board.originX + (ACTIVE_STAGE.board.width - 1) * BUILD_GRID_SPACING + 1.2, ...ENEMY_PATH.map(([x]) => x + 1.6)),
  minZ: Math.min(ACTIVE_STAGE.board.originZ - 1.2, ...ENEMY_PATH.map(([, z]) => z - 1.6)),
  maxZ: Math.max(ACTIVE_STAGE.board.originZ + (ACTIVE_STAGE.board.depth - 1) * BUILD_GRID_SPACING + 1.2, ...ENEMY_PATH.map(([, z]) => z + 1.6)),
} as const;

export const HIGH_GROUND_PLATFORMS: readonly TerrainPlatformDefinition[] = [];

function pointSegmentDistance(x: number, z: number, ax: number, az: number, bx: number, bz: number): number {
  const dx = bx - ax; const dz = bz - az;
  const lengthSquared = dx * dx + dz * dz;
  const t = lengthSquared <= 1e-8 ? 0 : Math.max(0, Math.min(1, ((x - ax) * dx + (z - az) * dz) / lengthSquared));
  return Math.hypot(x - (ax + dx * t), z - (az + dz * t));
}

function lowCellIsBuildable(x: number, z: number): boolean {
  const pathClear = ENEMY_PATH.slice(1).every(([bx, bz], index) => {
    const [ax, az] = ENEMY_PATH[index];
    return pointSegmentDistance(x, z, ax, az, bx, bz) >= 1.48;
  });
  if (!pathClear || x > ACTIVE_STAGE.board.buildMaxX) return false;
  return HIGH_GROUND_PLATFORMS.every((platform) => {
    const [px, , pz] = platform.center; const [sx, , sz] = platform.size;
    return Math.abs(x - px) > sx / 2 + 0.1 || Math.abs(z - pz) > sz / 2 + 0.1;
  });
}

function createBuildSlots(): SlotDefinition[] {
  const slots: SlotDefinition[] = [];
  for (let gz = 0; gz < ACTIVE_STAGE.board.depth; gz += 1) {
    for (let gx = 0; gx < ACTIVE_STAGE.board.width; gx += 1) {
      const x = ACTIVE_STAGE.board.originX + gx * BUILD_GRID_SPACING;
      const z = ACTIVE_STAGE.board.originZ + gz * BUILD_GRID_SPACING;
      if (!lowCellIsBuildable(x, z)) continue;
      slots.push({ id: slots.length, position: [x, 0.62, z], tier: 'low' });
    }
  }
  for (const platform of HIGH_GROUND_PLATFORMS) {
    for (const position of platform.buildPositions) slots.push({ id: slots.length, position, tier: 'high' });
  }
  return slots;
}

export const BUILD_SLOTS: readonly SlotDefinition[] = createBuildSlots();

export function buildSlotIdAt(x: number, z: number, tier: 'low' | 'high' = 'low'): number {
  const slot = BUILD_SLOTS.find((candidate) => candidate.tier === tier
    && Math.abs(candidate.position[0] - x) < 1e-6 && Math.abs(candidate.position[2] - z) < 1e-6);
  if (!slot) throw new Error(`No buildable ${tier} grid cell at (${x}, ${z}).`);
  return slot.id;
}

export const REACTIONS: Record<ReactionKey, ReactionDefinition> = {
  hellfire: { key: 'hellfire', name: 'Hỏa Ngục', icon: '♨', magicDamage: 11, color: 0xff3d2e, elements: ['fire', 'fire'] },
  deepFreeze: { key: 'deepFreeze', name: 'Băng Phong', icon: '❄', magicDamage: 9, color: 0x8fe9ff, elements: ['ice', 'ice'] },
  tempest: { key: 'tempest', name: 'Cuồng Phong', icon: '✧', magicDamage: 9, color: 0x6ff0c1, elements: ['wind', 'wind'] },
  shatter: { key: 'shatter', name: 'Toái Địa', icon: '✹', magicDamage: 9, color: 0xe3b166, elements: ['earth', 'earth'] },
  firestorm: { key: 'firestorm', name: 'Bão Lửa', icon: '☄', magicDamage: 9, color: 0xff704b, elements: ['fire', 'wind'] },
  sandstorm: { key: 'sandstorm', name: 'Bão Cát', icon: '≋', magicDamage: 8, color: 0xc9b16f, elements: ['wind', 'earth'] },
  permafrost: { key: 'permafrost', name: 'Băng Địa', icon: '✣', magicDamage: 8, color: 0x72cfe0, elements: ['earth', 'ice'] },
  steamBurst: { key: 'steamBurst', name: 'Bộc Hơi', icon: '☁', magicDamage: 12, color: 0xf1c0d0, elements: ['ice', 'fire'] },
};

export function resolveReaction(incoming: Element, receiver: Element): ReactionKey | null {
  if (incoming === receiver) return ({ fire: 'hellfire', ice: 'deepFreeze', wind: 'tempest', earth: 'shatter' } as const)[incoming];
  const pair = new Set<Element>([incoming, receiver]);
  if (pair.has('fire') && pair.has('wind')) return 'firestorm';
  if (pair.has('wind') && pair.has('earth')) return 'sandstorm';
  if (pair.has('earth') && pair.has('ice')) return 'permafrost';
  if (pair.has('ice') && pair.has('fire')) return 'steamBurst';
  return null;
}

export function nodeCapacity(node: NodeState): number {
  if (node.type !== 'special') return Number.POSITIVE_INFINITY;
  const base = NODE_DEFINITIONS[node.type].capacity;
  return node.branch === 'conduit' ? base + 1 : node.branch === 'resonance' ? Math.max(1, base - 1) : base;
}

export function nodeInterval(node: NodeState): number {
  const base = NODE_DEFINITIONS[node.type].interval;
  if (node.branch === 'rapid') return 0.68;
  if (node.branch === 'heavy') return 1.35;
  if (node.branch === 'conduit') return base * 0.72;
  if (node.branch === 'resonance') return base * 1.2;
  return base;
}
