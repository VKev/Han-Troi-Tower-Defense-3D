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
  | 'warder'
  | 'arcaneBulwark'
  | 'skyWarder'
  | 'colossus';

export type GamePhase = 'ready' | 'wave' | 'paused' | 'won' | 'lost';
export type InteractionMode = 'inspect' | 'build' | 'move' | 'link';
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
  readonly barrierDamageMultiplier?: number;
  readonly speedAfterBarrierBreak?: number;
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
  outputTargetId: number | null;
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
  barrierBroken: boolean;
  hitFlash: number;
}

export interface ProjectileState {
  readonly id: number;
  readonly mesh: THREE.Group;
  readonly trail: THREE.LineSegments;
  readonly round: Round;
  readonly sourceTowerId: number;
  readonly targetTowerId: number | null;
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
  readonly healthMultiplier: number;
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
  readonly board: {
    readonly width: number;
    readonly depth: number;
    readonly originX: number;
    readonly originZ: number;
    readonly buildMaxX: number;
    readonly islandRadius: number;
    readonly cameraPosition: readonly [number, number, number];
    readonly cameraTarget: readonly [number, number, number];
  };
  readonly waves: readonly WaveDefinition[];
}

export const LAYER_HEIGHTS = [0.62, 3.05, 5.48] as const;
export const BOARD_WIDTH = 12;
export const BOARD_DEPTH = 9;
export const CELL_SIZE = 2;
export const BOARD_ORIGIN_X = -11;
export const BOARD_ORIGIN_Z = -8;
export const STARTING_MONEY = 220;
export const STARTING_LIVES = 20;
export const MAX_TOWER_LEVEL = 3;
export const SELL_REFUND = 0.6;
export const FIXED_STEP = 1 / 60;
export const ENEMY_REWARD_MULTIPLIER = 0.6;
export const WAVE_CLEAR_REWARD_MULTIPLIER = 0.65;

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
    role: 'Trụ sinh đạn', description: 'Tạo đạn Arcana trung tính để truyền vào mạng liên kết.',
    cost: 80, upgradeCost: 70, moveCost: 28, footprint: [1, 1], capacity: 5,
    connectionRange: 12.6, cadence: 0.92, color: 0xf5c95e,
  },
  fire: {
    type: 'fire', name: 'Trụ Truyền Hỏa', shortName: 'Lửa', icon: '◆',
    role: 'Hỗ trợ đạn', description: 'Thêm Lửa. Đòn trúng gây thiêu đốt và tăng sức mạnh cho đạn kết hợp.',
    cost: 70, upgradeCost: 62, moveCost: 24, footprint: [1, 1], capacity: 5,
    connectionRange: 12, cadence: 0.72, element: 'fire', color: ELEMENT_COLORS.fire,
  },
  ice: {
    type: 'ice', name: 'Lăng Kính Băng', shortName: 'Băng', icon: '✦',
    role: 'Hỗ trợ đạn', description: 'Thêm Băng. Đòn trúng làm lạnh và có thể tiến tới đóng băng.',
    cost: 70, upgradeCost: 62, moveCost: 24, footprint: [1, 1], capacity: 5,
    connectionRange: 12, cadence: 0.72, element: 'ice', color: ELEMENT_COLORS.ice,
  },
  wind: {
    type: 'wind', name: 'Ống Dẫn Cuồng Phong', shortName: 'Gió', icon: '➤',
    role: 'Hỗ trợ đạn', description: 'Thêm Gió, tăng tốc độ bay và lan truyền phản ứng.',
    cost: 72, upgradeCost: 64, moveCost: 24, footprint: [1, 1], capacity: 5,
    connectionRange: 12.45, cadence: 0.82, element: 'wind', color: ELEMENT_COLORS.wind,
  },
  earth: {
    type: 'earth', name: 'Lò Rèn Địa', shortName: 'Đất', icon: '⬟',
    role: 'Hỗ trợ đạn', description: 'Thêm Đất, tăng lực va chạm và gây trạng thái Nứt Vỡ giáp.',
    cost: 72, upgradeCost: 64, moveCost: 24, footprint: [1, 1], capacity: 6,
    connectionRange: 11.7, cadence: 0.68, element: 'earth', color: ELEMENT_COLORS.earth,
  },
  amplifier: {
    type: 'amplifier', name: 'Bộ Khuếch Đại Arcana', shortName: 'Khuếch Đại', icon: '◉',
    role: 'Hỗ trợ trụ', description: 'Hỗ trợ bằng hào quang. Chọn Sức Mạnh hoặc Tốc Độ sau khi đặt.',
    cost: 120, upgradeCost: 85, moveCost: 42, footprint: [2, 2], capacity: 0,
    connectionRange: 8.1, cadence: 0, color: 0xb886ff,
  },
  lance: {
    type: 'lance', name: 'Nổ Arcana', shortName: 'Nổ', icon: '✹',
    role: 'Trụ đặc biệt', description: 'Tích đạn rồi tự động nổ trong bán kính một ô khi kẻ địch cùng tầng tiến vào.',
    cost: 180, upgradeCost: 110, moveCost: 58, footprint: [2, 1], capacity: 8,
    connectionRange: 0, cadence: 0, color: 0xff9f43,
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
  arcaneBulwark: {
    kind: 'arcaneBulwark', name: 'Vệ Binh Hợp Kim', hp: 520, speed: 1.08, radius: 0.98,
    reward: 72, nexusDamage: 5, layer: 0, color: 0x5d7898,
    reactionBarrier: 'Sốc Nhiệt', barrierDamageMultiplier: 0.04, speedAfterBarrierBreak: 1.85,
  },
  skyWarder: {
    kind: 'skyWarder', name: 'Hộ Vệ Thiên Lăng', hp: 360, speed: 1.18, radius: 0.86,
    reward: 68, nexusDamage: 4, layer: 1, color: 0xa888ff,
    resist: ['fire'], vulnerable: ['earth'], reactionBarrier: 'Bão Cát',
  },
  colossus: {
    kind: 'colossus', name: 'Cự Tượng Khe Nứt', hp: 980, speed: 0.72, radius: 1.18,
    reward: 165, nexusDamage: 8, layer: 0, color: 0x6f8fb2,
    resist: ['fire', 'earth'], vulnerable: ['ice', 'wind'], reactionBarrier: 'Vỡ Tinh Thể',
  },
};

const FORMATION_GAP_PATTERN = [0.35, 1.55, 0.12, 1.25, 0.42, 0.18, 1.65, 0.75, 1.28] as const;
const FORMATION_SIDE_PATTERN = [0, -0.5, 0.45, 0.45, 0.08, -0.42, -0.42, 0.2, 0.52, -0.16] as const;

function sequence(kind: EnemyKind, start: number, count: number, interval: number): SpawnOrder[] {
  let at = start;
  return Array.from({ length: count }, (_, index) => {
    if (index > 0) at += interval * FORMATION_GAP_PATTERN[(index - 1) % FORMATION_GAP_PATTERN.length];
    return {
      at,
      kind,
      sideOffset: FORMATION_SIDE_PATTERN[index % FORMATION_SIDE_PATTERN.length],
    };
  });
}

const TUTORIAL_WAVES: readonly WaveDefinition[] = [
  {
    title: 'Hiệu Chuẩn Đạn Trung Tính',
    hint: 'Nối Lò Đạn với trụ Lửa để tạo đoạn bay cắt qua đường tiến quân.',
    orders: sequence('riftling', 0.8, 4, 1.35),
    healthMultiplier: 1,
    clearBonus: 55,
  },
  {
    title: 'Tiếp Sức Hỏa',
    hint: 'Nối trụ Lửa với trụ Băng. Trụ cuối chỉ giữ đạn cho tới khi có liên kết đầu ra.',
    orders: sequence('riftling', 0.6, 6, 1.08),
    healthMultiplier: 1.08,
    clearBonus: 75,
  },
  {
    title: 'Thử Thách Nhiệt',
    hint: 'Khép chuỗi bằng một trụ Lửa thứ hai để đạn Lửa + Băng bay qua lane và kích hoạt Sốc Nhiệt.',
    orders: [
      ...sequence('riftling', 0.5, 5, 1.02),
      ...sequence('runner', 2.2, 2, 1.85),
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 1.18,
    clearBonus: 105,
  },
  {
    title: 'Tự Xây Mạch',
    hint: 'Tự mở rộng mạng Lửa + Băng để cắt qua đội hình dày hơn.',
    orders: [
      ...sequence('riftling', 0.4, 9, 0.85),
      ...sequence('runner', 1.1, 3, 1.25),
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 6,
    clearBonus: 105,
  },
  {
    title: 'Áp Lực Kép',
    hint: 'Phối hợp nhiều đoạn đạn nguyên tố để giữ cả bầy nhanh lẫn mục tiêu bền bỉ.',
    orders: [
      ...sequence('riftling', 0.3, 10, 0.72),
      ...sequence('runner', 0.8, 5, 0.9),
      ...sequence('brute', 2.2, 1, 1),
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 10,
    clearBonus: 125,
  },
  {
    title: 'Chứng Nhận Mạch',
    hint: 'Tự hoàn thiện mạng phản ứng trước đợt tiến công dày và trâu nhất.',
    orders: [
      ...sequence('riftling', 0.2, 12, 0.58),
      ...sequence('runner', 0.55, 7, 0.72),
      ...sequence('brute', 1.4, 2, 1.15),
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 14,
    clearBonus: 150,
  },
] as const;

const PRISMATIC_WAVES: readonly WaveDefinition[] = [
  {
    title: 'Đường Dài',
    hint: 'Một đội hình mặt đất nhẹ thử thách mạng lưới đầu tiên với ngân sách hạn chế.',
    orders: sequence('riftling', 0.4, 10, 0.95),
    healthMultiplier: 1.1,
    clearBonus: 50,
  },
  {
    title: 'Dồn Ép Mặt Đất',
    hint: 'Kẻ Chạy và Bầy Dị Linh dồn ép tuyến mặt đất trước khi kẻ địch bay xuất hiện ở đợt sau.',
    orders: [
      ...sequence('runner', 0.3, 8, 0.82),
      ...sequence('riftling', 0.8, 6, 0.96),
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 1.4,
    clearBonus: 70,
  },
  {
    title: 'Áp Lực Khuếch Đại',
    hint: 'Linh Hỏa tầng 1 xuất hiện lần đầu cùng Kẻ Chạy; hãy chuẩn bị mạng trên cao và Bộ Khuếch Đại.',
    orders: [
      ...sequence('riftling', 0.25, 10, 0.46),
      ...sequence('runner', 0.75, 8, 0.66),
      ...sequence('wisp', 0.55, 6, 0.72),
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 2,
    clearBonus: 100,
  },
  {
    title: 'Vòng Nổ Arcana',
    hint: 'Đặt Nổ cạnh đường đi, nạp đầy bằng một Lò Đạn riêng rồi chờ đội hình cùng tầng tiến vào bán kính một ô.',
    orders: [
      ...sequence('riftling', 0.15, 16, 0.34),
      ...sequence('runner', 0.4, 10, 0.44),
      ...sequence('wisp', 0.55, 8, 0.52),
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 3,
    clearBonus: 130,
  },
  {
    title: 'Ma Trận Kháng Tính',
    hint: 'Kẻ Chạy, Quái Đá và Linh Hỏa miễn nhiễm Lửa đòi hỏi giải pháp riêng cho mặt đất và tầng 1.',
    orders: [
      ...sequence('runner', 0.12, 18, 0.29),
      ...sequence('brute', 0.45, 12, 0.43),
      ...sequence('wisp', 0.3, 16, 0.33),
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 4.4,
    clearBonus: 170,
  },
  {
    title: 'Cuộc Vây Hãm Lăng Kính',
    hint: 'Mọi bài học hội tụ cùng lúc. Sốc Nhiệt phá vỡ lá chắn của Hộ Vệ.',
    orders: [
      ...sequence('riftling', 0.08, 20, 0.22),
      ...sequence('runner', 0.24, 15, 0.25),
      ...sequence('brute', 0.4, 12, 0.36),
      ...sequence('wisp', 0.2, 12, 0.28),
      { at: 2.1, kind: 'warder', sideOffset: -0.25 } satisfies SpawnOrder,
      { at: 3.8, kind: 'warder', sideOffset: 0.3 } satisfies SpawnOrder,
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 6.2,
    clearBonus: 240,
  },
] as const;

const CONVERGENCE_WAVES: readonly WaveDefinition[] = [
  {
    title: 'Biên Giới Mở Rộng',
    hint: 'Tuyến mặt đất dài hơn tạo nhiều cơ hội xuyên hàng, nhưng đòi hỏi mạng đạn phủ được cả hai cánh.',
    orders: [
      ...sequence('riftling', 0.2, 15, 0.56),
      ...sequence('runner', 0.9, 5, 0.92),
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 1.3,
    clearBonus: 80,
  },
  {
    title: 'Xung Kích Mặt Đất',
    hint: 'Bầy Dị Linh và Kẻ Chạy tiếp tục thử mạng mặt đất; kẻ địch bay bắt đầu xuất hiện từ đợt sau.',
    orders: [
      ...sequence('riftling', 0.15, 18, 0.44),
      ...sequence('runner', 0.55, 11, 0.62),
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 1.7,
    clearBonus: 105,
  },
  {
    title: 'Gọng Kìm Hợp Kim',
    hint: 'Linh Hỏa tầng 1 xuất hiện lần đầu cùng Vệ Binh Hợp Kim; giáp của chúng chỉ vỡ bởi Sốc Nhiệt.',
    orders: [
      ...sequence('runner', 0.12, 18, 0.34),
      ...sequence('wisp', 0.32, 12, 0.46),
      ...sequence('brute', 0.65, 12, 0.72),
      ...sequence('arcaneBulwark', 1.15, 4, 1.45),
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 2.4,
    clearBonus: 135,
  },
  {
    title: 'Lá Chắn Trên Mây',
    hint: 'Hộ Vệ Thiên Lăng bay tầng 1. Phản ứng Bão Cát phá lá chắn hiệu quả trước khi sát thương thường phát huy.',
    orders: [
      ...sequence('riftling', 0.08, 24, 0.29),
      ...sequence('wisp', 0.3, 20, 0.39),
      ...sequence('skyWarder', 0.75, 8, 0.88),
      ...sequence('arcaneBulwark', 0.6, 8, 0.92),
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 3.3,
    clearBonus: 175,
  },
  {
    title: 'Hai Tầng Dồn Ép',
    hint: 'Mặt đất bọc giáp và đàn bay tầng 1 cùng tới; ngân sách phải được chia giữa hai mạng không thể truyền chéo tầng.',
    orders: [
      ...sequence('runner', 0.08, 26, 0.25),
      ...sequence('brute', 0.34, 18, 0.42),
      ...sequence('wisp', 0.2, 22, 0.3),
      ...sequence('arcaneBulwark', 0.5, 10, 0.68),
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 4.5,
    clearBonus: 215,
  },
  {
    title: 'Cự Tượng Đầu Tiên',
    hint: 'Cự Tượng Khe Nứt mang lá chắn Vỡ Tinh Thể và gây thiệt hại Nexus lớn nếu lọt qua.',
    orders: [
      ...sequence('riftling', 0.06, 34, 0.2),
      ...sequence('wisp', 0.18, 30, 0.25),
      ...sequence('skyWarder', 0.42, 10, 0.58),
      ...sequence('arcaneBulwark', 0.4, 14, 0.46),
      { at: 1.6, kind: 'colossus', sideOffset: -0.25 } satisfies SpawnOrder,
      { at: 2.9, kind: 'colossus', sideOffset: 0.3 } satisfies SpawnOrder,
      { at: 4.2, kind: 'colossus', sideOffset: 0.1 } satisfies SpawnOrder,
      { at: 5.4, kind: 'colossus', sideOffset: -0.15 } satisfies SpawnOrder,
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 6,
    clearBonus: 270,
  },
  {
    title: 'Thiên Lăng Hợp Đội',
    hint: 'Nhiều Hộ Vệ tầng 1 che một đoàn bay dày, thử thách chuỗi Gió–Đất và khả năng tích Nổ.',
    orders: [
      ...sequence('brute', 0.16, 26, 0.28),
      ...sequence('wisp', 0.06, 46, 0.15),
      ...sequence('skyWarder', 0.35, 16, 0.42),
      ...sequence('arcaneBulwark', 0.28, 20, 0.34),
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 7.8,
    clearBonus: 325,
  },
  {
    title: 'Đội Cự Tượng',
    hint: 'Bốn Cự Tượng nối tiếp một đội hình tốc độ cao; một tuyến đạn bị nghẽn sẽ để lại khoảng trống chí mạng.',
    orders: [
      ...sequence('runner', 0.06, 42, 0.15),
      ...sequence('wisp', 0.12, 42, 0.14),
      ...sequence('arcaneBulwark', 0.22, 28, 0.21),
      ...sequence('colossus', 0.7, 12, 0.48),
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 10,
    clearBonus: 390,
  },
  {
    title: 'Ma Trận Phản Ứng',
    hint: 'Lá chắn Bão Cát trên không và giáp mặt đất dày yêu cầu hai bộ phản ứng đúng tầng hoạt động đồng thời.',
    orders: [
      ...sequence('riftling', 0.04, 46, 0.11),
      ...sequence('brute', 0.14, 34, 0.16),
      ...sequence('skyWarder', 0.28, 21, 0.25),
      ...sequence('arcaneBulwark', 0.17, 35, 0.16),
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 12.5,
    clearBonus: 470,
  },
  {
    title: 'Đại Hội Tụ Khe Nứt',
    hint: 'Mọi hàng phòng thủ hội tụ trong đợt cuối: đội hình dày, hai tầng, nhiều lá chắn và ba Cự Tượng.',
    orders: [
      ...sequence('runner', 0.03, 36, 0.085),
      ...sequence('brute', 0.1, 26, 0.12),
      ...sequence('wisp', 0.06, 32, 0.09),
      ...sequence('skyWarder', 0.19, 18, 0.18),
      ...sequence('arcaneBulwark', 0.14, 26, 0.13),
      ...sequence('colossus', 0.52, 10, 0.38),
    ].sort((a, b) => a.at - b.at),
    healthMultiplier: 15.5,
    clearBonus: 620,
  },
] as const;

export const STAGES: readonly StageDefinition[] = [
  {
    title: 'Mạch Đầu Tiên',
    subtitle: 'Hướng dẫn mạng lưới mặt đất',
    path: [[-11, -2], [-3, -2], [-3, 2], [7, 2]],
    hasElevation: false,
    startingMoney: 420,
    startingLives: 3,
    tutorial: true,
    killRewardMultiplier: 1,
    board: {
      width: 10, depth: 7, originX: -9, originZ: -6, buildMaxX: 6.6, islandRadius: 15.8,
      cameraPosition: [16, 19, 18], cameraTarget: [-1, 1.4, -0.4],
    },
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
    board: {
      width: 12, depth: 9, originX: -11, originZ: -8, buildMaxX: 10.6, islandRadius: 24.2,
      cameraPosition: [25, 28, 28], cameraTarget: [0, 1.6, -0.4],
    },
    waves: PRISMATIC_WAVES,
  },
  {
    title: 'Đại Địa Hợp Lưu',
    subtitle: 'Mạng hai tầng trên chiến trường mở rộng',
    path: [[-28, -9], [-15, -9], [-15, 7], [-5, 7], [-5, -5], [8, -5], [8, 9], [20, 9], [20, 1], [28, 1]],
    hasElevation: true,
    startingMoney: 220,
    startingLives: STARTING_LIVES,
    tutorial: false,
    killRewardMultiplier: 1.65,
    board: {
      width: 20, depth: 14, originX: -19, originZ: -13, buildMaxX: 19.6, islandRadius: 31.5,
      cameraPosition: [35, 40, 41], cameraTarget: [0, 2.1, -0.5],
    },
    waves: CONVERGENCE_WAVES,
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
