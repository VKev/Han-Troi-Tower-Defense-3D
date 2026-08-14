import * as THREE from 'three';

export const ELEMENTS = ['fire', 'ice', 'wind', 'earth'] as const;
export type Element = (typeof ELEMENTS)[number];

export type TowerType =
  | 'foundry'
  | 'fire'
  | 'ice'
  | 'wind'
  | 'earth'
  | 'amplifier'
  | 'lance';

export type EnemyKind =
  | 'riftling'
  | 'runner'
  | 'brute'
  | 'wisp'
  | 'frostRay'
  | 'warder';

export type GamePhase = 'ready' | 'wave' | 'paused' | 'won' | 'lost';
export type InteractionMode = 'inspect' | 'build' | 'move';
export type AmplifierBranch = 'power' | 'throughput';

export interface TowerDefinition {
  readonly type: TowerType;
  readonly name: string;
  readonly shortName: string;
  readonly icon: string;
  readonly role: string;
  readonly description: string;
  readonly cost: number;
  readonly upgradeCost: number;
  readonly moveCost: number;
  readonly footprint: readonly [number, number];
  readonly capacity: number;
  readonly connectionRange: number;
  readonly cadence: number;
  readonly element?: Element;
  readonly color: number;
}

export interface EnemyDefinition {
  readonly kind: EnemyKind;
  readonly name: string;
  readonly hp: number;
  readonly speed: number;
  readonly radius: number;
  readonly reward: number;
  readonly nexusDamage: number;
  readonly layer: 0 | 1 | 2;
  readonly color: number;
  readonly resist?: readonly Element[];
  readonly immune?: readonly Element[];
  readonly vulnerable?: readonly Element[];
  readonly reactionBarrier?: string;
}

export interface Round {
  readonly id: number;
  damage: number;
  readonly elements: Element[];
}

export interface TowerState {
  readonly id: number;
  readonly type: TowerType;
  readonly group: THREE.Group;
  readonly gx: number;
  readonly gz: number;
  readonly layer: 0 | 1 | 2;
  readonly cells: string[];
  level: number;
  totalInvested: number;
  buffer: Round[];
  aimAngle: number;
  produceTimer: number;
  outputTimer: number;
  skillTimer: number;
  blockedReason: string;
  amplifierBranch: AmplifierBranch;
  pulse: number;
}

export interface EnemyState {
  readonly id: number;
  readonly kind: EnemyKind;
  readonly group: THREE.Group;
  hp: number;
  maxHp: number;
  progress: number;
  sideOffset: number;
  speedMultiplier: number;
  dead: boolean;
  reachedNexus: boolean;
  burn: number;
  burnDps: number;
  chilled: number;
  frozen: number;
  gale: number;
  cracked: number;
  reactionBarrier: string | null;
  hitFlash: number;
}

export interface ProjectileState {
  readonly id: number;
  readonly mesh: THREE.Group;
  readonly trail: THREE.LineSegments;
  readonly round: Round;
  readonly sourceTowerId: number;
  readonly start: THREE.Vector3;
  readonly end: THREE.Vector3;
  readonly layer: 0 | 1 | 2;
  readonly hitEnemyIds: Set<number>;
  progress: number;
  speed: number;
}

export interface SpawnOrder {
  readonly at: number;
  readonly kind: EnemyKind;
  readonly sideOffset: number;
}

export interface WaveDefinition {
  readonly title: string;
  readonly hint: string;
  readonly orders: readonly SpawnOrder[];
  readonly clearBonus: number;
}

export interface StageDefinition {
  readonly title: string;
  readonly subtitle: string;
  readonly path: readonly (readonly [number, number])[];
  readonly hasElevation: boolean;
  readonly startingMoney: number;
  readonly startingLives: number;
  readonly tutorial: boolean;
  readonly killRewardMultiplier: number;
  readonly waves: readonly WaveDefinition[];
}

export const LAYER_HEIGHTS = [0.62, 3.05, 5.48] as const;
export const BOARD_WIDTH = 12;
export const BOARD_DEPTH = 9;
export const CELL_SIZE = 2;
export const BOARD_ORIGIN_X = -11;
export const BOARD_ORIGIN_Z = -8;
export const STARTING_MONEY = 160;
export const STARTING_LIVES = 20;
export const MAX_TOWER_LEVEL = 3;
export const SELL_REFUND = 0.6;
export const FIXED_STEP = 1 / 60;

export const ELEMENT_COLORS: Record<Element, number> = {
  fire: 0xff633f,
  ice: 0x61d9ff,
  wind: 0x73f0a6,
  earth: 0xd4a05c,
};

export const ELEMENT_NAMES: Record<Element, string> = {
  fire: 'Lửa',
  ice: 'Băng',
  wind: 'Gió',
  earth: 'Đất',
};

export const TOWER_DEFINITIONS: Record<TowerType, TowerDefinition> = {
  foundry: {
    type: 'foundry', name: 'Lò Đúc Đạn', shortName: 'Lò Đạn', icon: '⬡',
    role: 'Trụ sinh đạn', description: 'Tạo đạn Arcana trung tính và trữ chúng trong kho đạn riêng.',
    cost: 80, upgradeCost: 70, moveCost: 28, footprint: [1, 1], capacity: 5,
    connectionRange: 8.4, cadence: 0.92, color: 0xf5c95e,
  },
  fire: {
    type: 'fire', name: 'Trụ Truyền Hỏa', shortName: 'Lửa', icon: '◆',
    role: 'Hỗ trợ đạn', description: 'Thêm Lửa. Đòn trúng gây thiêu đốt và tăng sức mạnh cho đạn kết hợp.',
    cost: 70, upgradeCost: 62, moveCost: 24, footprint: [1, 1], capacity: 5,
    connectionRange: 8, cadence: 0.72, element: 'fire', color: ELEMENT_COLORS.fire,
  },
  ice: {
    type: 'ice', name: 'Lăng Kính Băng', shortName: 'Băng', icon: '✦',
    role: 'Hỗ trợ đạn', description: 'Thêm Băng. Đòn trúng làm lạnh và có thể tiến tới đóng băng.',
    cost: 70, upgradeCost: 62, moveCost: 24, footprint: [1, 1], capacity: 5,
    connectionRange: 8, cadence: 0.72, element: 'ice', color: ELEMENT_COLORS.ice,
  },
  wind: {
    type: 'wind', name: 'Ống Dẫn Cuồng Phong', shortName: 'Gió', icon: '➤',
    role: 'Hỗ trợ đạn', description: 'Thêm Gió, tăng tốc độ bay và lan truyền phản ứng.',
    cost: 72, upgradeCost: 64, moveCost: 24, footprint: [1, 1], capacity: 5,
    connectionRange: 8.3, cadence: 0.82, element: 'wind', color: ELEMENT_COLORS.wind,
  },
  earth: {
    type: 'earth', name: 'Lò Rèn Địa', shortName: 'Đất', icon: '⬟',
    role: 'Hỗ trợ đạn', description: 'Thêm Đất, tăng lực va chạm và gây trạng thái Nứt Vỡ giáp.',
    cost: 72, upgradeCost: 64, moveCost: 24, footprint: [1, 1], capacity: 6,
    connectionRange: 7.8, cadence: 0.68, element: 'earth', color: ELEMENT_COLORS.earth,
  },
  amplifier: {
    type: 'amplifier', name: 'Bộ Khuếch Đại Arcana', shortName: 'Khuếch Đại', icon: '◉',
    role: 'Hỗ trợ trụ', description: 'Hỗ trợ bằng hào quang. Chọn Sức Mạnh hoặc Tốc Độ sau khi đặt.',
    cost: 120, upgradeCost: 85, moveCost: 42, footprint: [2, 2], capacity: 0,
    connectionRange: 5.4, cadence: 0, color: 0xb886ff,
  },
  lance: {
    type: 'lance', name: 'Thương Nexus', shortName: 'Thương', icon: '⟿',
    role: 'Trụ đặc biệt', description: 'Tiêu thụ kho đạn đầy để tự động phóng một đòn nguyên tố diện rộng.',
    cost: 180, upgradeCost: 110, moveCost: 58, footprint: [2, 1], capacity: 8,
    connectionRange: 0, cadence: 0, color: 0xffe69b,
  },
};

export const ENEMY_DEFINITIONS: Record<EnemyKind, EnemyDefinition> = {
  riftling: {
    kind: 'riftling', name: 'Bầy Dị Linh', hp: 54, speed: 2.05, radius: 0.5,
    reward: 11, nexusDamage: 1, layer: 0, color: 0xef6fa8,
  },
  runner: {
    kind: 'runner', name: 'Kẻ Chạy Arcana', hp: 72, speed: 3.35, radius: 0.48,
    reward: 15, nexusDamage: 1, layer: 0, color: 0xffae4b, vulnerable: ['ice'],
  },
  brute: {
    kind: 'brute', name: 'Quái Đá', hp: 220, speed: 1.18, radius: 0.78,
    reward: 34, nexusDamage: 3, layer: 0, color: 0x9d806d, resist: ['earth'], vulnerable: ['ice'],
  },
  wisp: {
    kind: 'wisp', name: 'Linh Hỏa', hp: 92, speed: 2.25, radius: 0.54,
    reward: 20, nexusDamage: 2, layer: 1, color: 0xff774f, immune: ['fire'], vulnerable: ['ice'],
  },
  frostRay: {
    kind: 'frostRay', name: 'Cá Đuối Băng', hp: 130, speed: 2, radius: 0.72,
    reward: 28, nexusDamage: 2, layer: 2, color: 0x8edfff, resist: ['ice'], vulnerable: ['fire', 'wind'],
  },
  warder: {
    kind: 'warder', name: 'Hộ Vệ Lăng Kính', hp: 420, speed: 1.05, radius: 0.92,
    reward: 75, nexusDamage: 5, layer: 0, color: 0xc793ff, reactionBarrier: 'Sốc Nhiệt',
  },
};

function sequence(kind: EnemyKind, start: number, count: number, interval: number): SpawnOrder[] {
  return Array.from({ length: count }, (_, index) => ({
    at: start + index * interval,
    kind,
    sideOffset: ((index % 3) - 1) * 0.42,
  }));
}

const TUTORIAL_WAVES: readonly WaveDefinition[] = [
  {
    title: 'Hiệu Chuẩn Đạn Trung Tính',
    hint: 'Lò Đạn tự do bắn đạn trung tính theo hướng ngắm vào đường tiến quân.',
    orders: sequence('riftling', 0.8, 5, 1.35),
    clearBonus: 55,
  },
  {
    title: 'Tiếp Sức Hỏa',
    hint: 'Xoay Lò Đạn qua trụ Lửa. Đạn đi xuyên trụ sẽ nhận Lửa rồi bay theo hướng của trụ Lửa.',
    orders: sequence('riftling', 0.6, 6, 1.08),
    clearBonus: 75,
  },
  {
    title: 'Thử Thách Nhiệt',
    hint: 'Thêm Băng sau Lửa. Đạn hai nguyên tố kích hoạt Sốc Nhiệt lên nhóm cuối.',
    orders: [
      ...sequence('riftling', 0.5, 5, 1.02),
      ...sequence('runner', 2.2, 2, 1.85),
    ].sort((a, b) => a.at - b.at),
    clearBonus: 105,
  },
] as const;

const PRISMATIC_WAVES: readonly WaveDefinition[] = [
  {
    title: 'Đường Dài',
    hint: 'Một đội hình mặt đất nhẹ thử thách mạng lưới đầu tiên với ngân sách hạn chế.',
    orders: sequence('riftling', 0.5, 8, 1.05),
    clearBonus: 50,
  },
  {
    title: 'Đợt Bay Đầu Tiên',
    hint: 'Đơn vị mặt đất tốc độ cao xuất hiện cùng kẻ địch bay duy nhất: Linh Hỏa tầng 1.',
    orders: [
      ...sequence('runner', 0.4, 6, 0.9),
      ...sequence('wisp', 1.1, 4, 1.15),
    ].sort((a, b) => a.at - b.at),
    clearBonus: 70,
  },
  {
    title: 'Áp Lực Khuếch Đại',
    hint: 'Đội hình giáp dày khuyến khích xây mạng lưới gọn được Bộ Khuếch Đại hỗ trợ.',
    orders: [
      ...sequence('riftling', 0.35, 10, 0.58),
      ...sequence('brute', 1.4, 4, 1.6),
    ].sort((a, b) => a.at - b.at),
    clearBonus: 100,
  },
  {
    title: 'Đội Hình Thương',
    hint: 'Đội hình dày ở hai tầng là mục tiêu lý tưởng cho Thương Nexus đã tích đầy.',
    orders: [
      ...sequence('riftling', 0.2, 14, 0.48),
      ...sequence('runner', 0.8, 8, 0.72),
      ...sequence('wisp', 1.05, 6, 0.9),
    ].sort((a, b) => a.at - b.at),
    clearBonus: 130,
  },
  {
    title: 'Ma Trận Kháng Tính',
    hint: 'Kẻ Chạy, Quái Đá và Linh Hỏa miễn nhiễm Lửa đòi hỏi giải pháp riêng cho mặt đất và tầng 1.',
    orders: [
      ...sequence('runner', 0.35, 10, 0.68),
      ...sequence('brute', 1.2, 6, 1.72),
      ...sequence('wisp', 0.9, 9, 1.08),
    ].sort((a, b) => a.at - b.at),
    clearBonus: 170,
  },
  {
    title: 'Cuộc Vây Hãm Lăng Kính',
    hint: 'Mọi bài học hội tụ cùng lúc. Sốc Nhiệt phá vỡ lá chắn của Hộ Vệ.',
    orders: [
      ...sequence('riftling', 0.15, 14, 0.46),
      ...sequence('runner', 0.6, 10, 0.7),
      ...sequence('brute', 1.25, 8, 1.55),
      ...sequence('wisp', 0.8, 10, 0.95),
      { at: 10.5, kind: 'warder', sideOffset: 0 } satisfies SpawnOrder,
    ].sort((a, b) => a.at - b.at),
    clearBonus: 240,
  },
] as const;

export const STAGES: readonly StageDefinition[] = [
  {
    title: 'Mạch Đầu Tiên',
    subtitle: 'Hướng dẫn mạng lưới mặt đất',
    path: [[-14, -3], [2, -3], [2, 2], [14, 2]],
    hasElevation: false,
    startingMoney: 420,
    startingLives: 20,
    tutorial: true,
    killRewardMultiplier: 1,
    waves: TUTORIAL_WAVES,
  },
  {
    title: 'Khe Nứt Lăng Kính',
    subtitle: 'Thử thách mạng hai tầng đường dài',
    path: [[-20, -6], [-4, -6], [-4, 5], [10, 5], [10, 0], [20, 0]],
    hasElevation: true,
    startingMoney: STARTING_MONEY,
    startingLives: STARTING_LIVES,
    tutorial: false,
    killRewardMultiplier: 1.5,
    waves: PRISMATIC_WAVES,
  },
] as const;

export const REACTION_PAIRS: readonly {
  readonly a: Element;
  readonly b: Element;
  readonly name: string;
  readonly color: number;
}[] = [
  { a: 'fire', b: 'ice', name: 'Sốc Nhiệt', color: 0xffffff },
  { a: 'fire', b: 'wind', name: 'Hỏa Hoạn', color: 0xff9a4d },
  { a: 'fire', b: 'earth', name: 'Phun Trào', color: 0xff6b38 },
  { a: 'ice', b: 'wind', name: 'Đông Cứng Nhanh', color: 0xb9f6ff },
  { a: 'ice', b: 'earth', name: 'Vỡ Tinh Thể', color: 0xbdd3ff },
  { a: 'wind', b: 'earth', name: 'Bão Cát', color: 0xe6c17a },
] as const;

export function gridKey(gx: number, gz: number): string {
  return `${gx}:${gz}`;
}

export function gridToWorld(gx: number, gz: number, layer: number): THREE.Vector3 {
  return new THREE.Vector3(
    BOARD_ORIGIN_X + gx * CELL_SIZE,
    LAYER_HEIGHTS[layer as 0 | 1 | 2],
    BOARD_ORIGIN_Z + gz * CELL_SIZE,
  );
}

export function uniqueElements(elements: readonly Element[]): Element[] {
  return ELEMENTS.filter((element) => elements.includes(element));
}

export function towerElement(type: TowerType): Element | undefined {
  return TOWER_DEFINITIONS[type].element;
}

export function isAmmoEmitter(type: TowerType): boolean {
  return type !== 'amplifier' && type !== 'lance';
}

export function isAmmoReceiver(type: TowerType): boolean {
  return type !== 'foundry' && type !== 'amplifier';
}
