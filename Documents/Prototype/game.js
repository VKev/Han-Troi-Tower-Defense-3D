"use strict";

const canvas = document.getElementById("gameCanvas");
const ctx = canvas.getContext("2d");

const ui = {
  water: document.getElementById("waterValue"),
  healthBar: document.getElementById("healthBar"),
  healthFill: document.getElementById("healthFill"),
  wave: document.getElementById("waveValue"),
  enemies: document.getElementById("enemyValue"),
  waveButton: document.getElementById("waveButton"),
  levelButton: document.getElementById("levelButton"),
  tutorialButton: document.getElementById("tutorialButton"),
  pauseButton: document.getElementById("pauseButton"),
  restartButton: document.getElementById("restartButton"),
  shop: document.getElementById("towerShop"),
  hint: document.getElementById("placementHint"),
  banner: document.getElementById("battleBanner"),
  inspectorTitle: document.getElementById("inspectorTitle"),
  inspectorDescription: document.getElementById("inspectorDescription"),
  karmaReadout: document.getElementById("karmaReadout"),
  phaseLabel: document.getElementById("phaseLabel"),
  karmaText: document.getElementById("karmaText"),
  karmaFill: document.getElementById("karmaFill"),
  effectGrid: document.getElementById("effectGrid"),
  yangEffect: document.getElementById("yangEffect"),
  yinEffect: document.getElementById("yinEffect"),
  upgradePanel: document.getElementById("upgradePanel"),
  upgradeLimit: document.getElementById("upgradeLimit"),
  upgradeOptions: document.getElementById("upgradeOptions"),
  sellButton: document.getElementById("sellButton"),
  inspector: document.getElementById("inspector"),
  inspectorClose: document.getElementById("inspectorClose"),
  tutorialOverlay: document.getElementById("tutorialOverlay"),
  tutorialProgress: document.getElementById("tutorialProgress"),
  tutorialIcon: document.getElementById("tutorialIcon"),
  tutorialKicker: document.getElementById("tutorialKicker"),
  tutorialTitle: document.getElementById("tutorialTitle"),
  tutorialBody: document.getElementById("tutorialBody"),
  tutorialContinue: document.getElementById("tutorialContinue"),
  levelOverlay: document.getElementById("levelOverlay"),
  levelGrid: document.getElementById("levelGrid"),
  levelClose: document.getElementById("levelClose"),
  levelCompleteOverlay: document.getElementById("levelCompleteOverlay"),
  levelCompleteTitle: document.getElementById("levelCompleteTitle"),
  levelCompleteBody: document.getElementById("levelCompleteBody"),
  replayLevelButton: document.getElementById("replayLevelButton"),
  nextLevelButton: document.getElementById("nextLevelButton")
};

const MAX_HEALTH = 40;
const MAX_UPGRADES = 3;
const STORAGE_KEY = "toadTowerDefenseCampaignV1";
const TOWER_ORDER = ["bear", "bee", "fox", "crab", "waterTower"];

const COLORS = {
  ink: "#f7f0d8",
  muted: "#abb7c2",
  path: "#c3aa77",
  pathEdge: "#8f7046",
  yang: "#f4c85b",
  yin: "#8b77c7",
  water: "#62cde5",
  damage: "#f47f6b",
  poison: "#89c86a",
  ground: "#273d3b"
};

const PATH = [
  { x: -25, y: 112 },
  { x: 130, y: 112 },
  { x: 130, y: 245 },
  { x: 350, y: 245 },
  { x: 350, y: 92 },
  { x: 590, y: 92 },
  { x: 590, y: 380 },
  { x: 800, y: 380 },
  { x: 800, y: 250 },
  { x: 1000, y: 250 }
];

const BUILD_SPOTS = [
  { x: 80, y: 200 }, { x: 220, y: 165 }, { x: 250, y: 330 },
  { x: 435, y: 180 }, { x: 470, y: 315 }, { x: 675, y: 185 },
  { x: 690, y: 455 }, { x: 835, y: 455 }, { x: 875, y: 150 }
];

const TOWER_TYPES = {
  bear: {
    name: "Gấu", icon: "🐻", role: "Vật lý · Đất", cost: 70,
    range: 112, fireRate: 0.82, damage: 22, cycle: 200, karmaPerAttack: 10, discharge: 20,
    color: "#c68b55", projectile: "#ffd38a",
    summary: "Giữ đường cận chiến.",
    yang: "Đánh rộng, làm chậm 35%.",
    yin: "Đánh đơn; địch trong tầm chạy nhanh 45%."
  },
  bee: {
    name: "Ong", icon: "🐝", role: "Phép · Đất/Bay", cost: 90,
    range: 146, fireRate: 0.68, damage: 12, cycle: 500, karmaPerAttack: 15, discharge: 5,
    color: "#e3bd45", projectile: "#fff08c",
    summary: "Phép diện rộng, trị địch tăng tốc.",
    yang: "Đánh rộng, độc, tăng sát thương theo tốc chạy.",
    yin: "Tăng giáp vật lý và tạo khiên cho địch."
  },
  fox: {
    name: "Cáo", icon: "🦊", role: "Đặc cấp · Đất", cost: 150,
    range: 120, fireRate: 1.18, damage: 42, cycle: 50, karmaPerAttack: 2, discharge: 5,
    color: "#e37d4d", projectile: "#ffbc8a",
    summary: "Săn mục tiêu giá trị cao.",
    yang: "Đánh đơn mạnh; +2 Nghiệp mỗi đòn.",
    yin: "Ưu tiên tinh anh; cắn mạnh nhưng đánh chậm."
  },
  crab: {
    name: "Cua", icon: "🦀", role: "Hỗ trợ · Hào quang", cost: 110,
    range: 145, fireRate: 1.55, damage: 5, cycle: 300, karmaPerAttack: 12.5, discharge: 10,
    color: "#d35b55", projectile: "#ffaaa0",
    summary: "Điều nhịp cả đội hình.",
    yang: "Làm chậm; trụ gần +25% xuyên giáp.",
    yin: "Mất hỗ trợ; trụ gần +75% Nghiệp, -20% tốc đánh."
  },
  waterTower: {
    name: "Trụ Nước", icon: "💧", role: "Hỗ trợ · Kinh tế", cost: 55,
    range: 0, fireRate: 0, damage: 0, cycle: 100, karmaPerAttack: 0, karmaPerProduction: 15, discharge: 10,
    color: "#4db3cc", projectile: "#8eeaff",
    summary: "Tạo Nước để xây trụ.",
    yang: "+5 Nước mỗi 3 giây; +15 Nghiệp.",
    yin: "Tạo nhanh gấp đôi; mỗi 3 lần làm đền mất 1 máu."
  }
};

const LEVELS = [
  { name: "Bờ ruộng", icon: "🐻", unlock: "bear", waves: 3, difficulty: 0.7, water: 250 },
  { name: "Vườn mật", icon: "🐝", unlock: "bee", waves: 6, difficulty: 0.84, water: 280 },
  { name: "Đồi cáo", icon: "🦊", unlock: "fox", waves: 6, difficulty: 1, water: 320 },
  { name: "Bãi triều", icon: "🦀", unlock: "crab", waves: 6, difficulty: 1.17, water: 360 },
  { name: "Mạch nguồn", icon: "💧", unlock: "waterTower", waves: 6, difficulty: 1.34, water: 400 }
];

const BASE_WAVES = [
  { count: 6, hp: 45, speed: 46, reward: 5, interval: 0.9 },
  { count: 8, hp: 60, speed: 50, reward: 5, interval: 0.8 },
  { count: 10, hp: 78, speed: 54, reward: 6, interval: 0.7 },
  { count: 12, hp: 98, speed: 58, reward: 6, interval: 0.63 },
  { count: 14, hp: 122, speed: 62, reward: 7, interval: 0.57 },
  { count: 16, hp: 145, speed: 66, reward: 8, interval: 0.52 }
];

const TUTORIALS = {
  0: [
    { icon: "🐻", title: "Gấu giữ cửa", body: "Gấu là trụ vật lý đầu tiên: đánh địch mặt đất, Dương đánh rộng và làm chậm.", kicker: "Màn 1 · Gấu" },
    { icon: "🐾", title: "Mua Gấu", body: "Bấm Gấu ở thanh dưới để chuẩn bị xây.", waitFor: "select:bear", target: "shop:bear" },
    { icon: "◎", title: "Đặt Gấu", body: "Chạm một vòng trống trên chiến trường.", waitFor: "place:bear", target: "canvas" },
    { icon: "▲", title: "Nâng cấp", body: "Chọn một nâng cấp cho Gấu bằng Nước. Tutorial đề xuất sát thương.", waitFor: "upgrade:bear", target: "upgrade" },
    { icon: "☀", title: "Pha Dương", body: "Gấu Dương đánh AOE và làm chậm 35%. Đòn đánh cùng hạ địch sẽ tích Nghiệp.", kicker: "Âm Dương" },
    { icon: "⚔", title: "Mở đợt", body: "Bấm nút Đợt để bắt đầu. Mọi đồng hồ của trụ chỉ chạy trong wave.", waitFor: "wave:start", target: "wave" },
    { icon: "☯", title: "Nghiệp sắp đầy", body: "Tutorial sẽ nạp gần đầy Nghiệp. Đòn kế tiếp đưa Gấu vào Âm.", waitFor: "phase:bear:Yin", target: "canvas", prepare: "prime_bear_yin" },
    { icon: "🌑", title: "Pha Âm", body: "Gấu Âm chỉ đánh một mục tiêu và tăng tốc địch trong tầm. Chờ Nghiệp xả hết để trở lại Dương.", kicker: "Âm Dương" },
    { icon: "◆◆◆", title: "Giữ đủ 3 wave", body: "Tiếp tục mở wave và bảo vệ đền. Tutorial sẽ trở lại sau wave cuối.", waitFor: "level:waves_complete", target: "wave" },
    { icon: "♻", title: "Bán Gấu", body: "Bấm Bán trụ để thu hồi 60% Nước đã đầu tư.", waitFor: "sell:bear", target: "sell", prepare: "select_bear" },
    { icon: "🌧", title: "Hoàn tất bài học", body: "Bạn đã mua, đặt, nâng cấp, bán và quan sát đủ hai pha của Gấu." }
  ],
  1: [
    { icon: "🐝", title: "Mở khóa Ong", body: "Ong gây sát thương phép lên cả địch đất và bay.", kicker: "Màn 2 · Ong" },
    { icon: "🐝", title: "Mua Ong", body: "Chọn Ong ở thanh linh thú.", waitFor: "select:bee", target: "shop:bee" },
    { icon: "◎", title: "Đặt Ong", body: "Đặt Ong gần đường đi để vùng AOE bao phủ nhiều mục tiêu.", waitFor: "place:bee", target: "canvas" },
    { icon: "🐻", title: "Thêm Gấu", body: "Chọn Gấu để chuẩn bị combo Âm Dương.", waitFor: "select:bear", target: "shop:bear" },
    { icon: "◎", title: "Đặt Gấu", body: "Đặt Gấu. Tutorial sẽ cho aura Âm phủ mục tiêu của Ong trong lần minh họa này.", waitFor: "place:bear", target: "canvas" },
    { icon: "☀", title: "Ong Dương", body: "Ong Dương gây độc và tăng sát thương phép theo phần tốc độ địch được cộng thêm." },
    { icon: "⚔", title: "Mở đợt", body: "Bắt đầu wave để thử combo Ong Dương + Gấu Âm.", waitFor: "wave:start", target: "wave" },
    { icon: "☯", title: "Kích hoạt combo", body: "Tutorial đưa Gấu vào Âm. Chờ Ong Dương bắn kẻ địch đang được Gấu tăng tốc.", waitFor: "combo:bee_bear", target: "canvas", prepare: "prime_combo" },
    { icon: "🐝", title: "Combo thành công", body: "Gấu Âm làm địch chạy nhanh hơn; Ong Dương biến phần tốc độ cộng thêm đó thành sát thương phép lớn hơn." }
  ],
  2: [{ icon: "🦊", title: "Mở khóa Cáo", body: "Cáo săn mục tiêu giá trị cao. Khi Âm, Cáo ưu tiên tinh anh và cắn mạnh hơn.", kicker: "Màn 3" }],
  3: [{ icon: "🦀", title: "Mở khóa Cua", body: "Cua điều nhịp đội hình bằng aura buff/debuff; nâng cấp của Cua tăng tầm aura.", kicker: "Màn 4" }],
  4: [{ icon: "💧", title: "Mở khóa Trụ Nước", body: "Trụ Nước tạo tài nguyên trong wave. Nâng cấp sẽ tăng lượng Nước mỗi lần sinh.", kicker: "Màn 5" }]
};

let state;
let campaign = loadCampaign();
let currentLevelIndex = Math.max(0, Math.min(LEVELS.length - 1, campaign.currentLevel - 1));
let WAVES = buildLevelWaves(currentLevelIndex);
let lastTime = performance.now();
let bannerTimer = 0;

function loadCampaign() {
  const fallback = { maxUnlockedLevel: 1, currentLevel: 1, completedTutorials: {} };
  try {
    const saved = JSON.parse(localStorage.getItem(STORAGE_KEY) || "null");
    if (!saved) return fallback;
    const maxUnlockedLevel = Math.max(1, Math.min(LEVELS.length, Number(saved.maxUnlockedLevel) || 1));
    return {
      maxUnlockedLevel,
      currentLevel: Math.max(1, Math.min(maxUnlockedLevel, Number(saved.currentLevel) || 1)),
      completedTutorials: saved.completedTutorials || {}
    };
  } catch {
    return fallback;
  }
}

function saveCampaign() {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(campaign));
  } catch {
    // The game remains playable when storage is unavailable.
  }
}

function buildLevelWaves(levelIndex) {
  const level = LEVELS[levelIndex];
  return BASE_WAVES.slice(0, level.waves).map((base, waveIndex) => ({
    count: base.count + levelIndex,
    hp: Math.round(base.hp * level.difficulty),
    speed: Math.round(base.speed * (1 + levelIndex * 0.025)),
    reward: base.reward,
    interval: Math.max(0.42, base.interval - levelIndex * 0.015),
    type: levelIndex === 0 ? "ground" : waveIndex === level.waves - 1 ? "elite" : waveIndex >= 2 ? "mixed" : "ground"
  }));
}

function makeState() {
  return {
    water: LEVELS[currentLevelIndex].water,
    health: MAX_HEALTH,
    wave: 0,
    waveActive: false,
    spawnQueue: 0,
    spawnTimer: 0,
    enemies: [],
    towers: [],
    effects: [],
    selectedType: null,
    selectedTowerId: null,
    paused: false,
    pausedByTutorial: false,
    pausedByMenu: false,
    gameOver: false,
    victory: false,
    levelComplete: false,
    pendingLevelCompletion: false,
    tutorial: null,
    nextEnemyId: 1,
    nextTowerId: 1,
    elapsed: 0
  };
}

function createShop() {
  ui.shop.innerHTML = "";
  TOWER_ORDER.forEach((key, index) => {
    const type = TOWER_TYPES[key];
    const unlocked = index < campaign.maxUnlockedLevel;
    const button = document.createElement("button");
    button.type = "button";
    button.className = `tower-card${unlocked ? "" : " locked"}`;
    button.dataset.tower = key;
    button.disabled = !unlocked;
    button.innerHTML = `<span class="animal">${type.icon}</span><strong>${type.name}</strong><span class="price">💧 ${type.cost}</span>${unlocked ? "" : '<span class="lock">🔒</span>'}`;
    button.addEventListener("click", () => selectShopTower(key));
    ui.shop.appendChild(button);
  });
}

function loadLevel(levelIndex, forceTutorial = false) {
  if (levelIndex < 0 || levelIndex >= campaign.maxUnlockedLevel) return false;
  currentLevelIndex = levelIndex;
  campaign.currentLevel = levelIndex + 1;
  saveCampaign();
  WAVES = buildLevelWaves(levelIndex);
  state = makeState();
  lastTime = performance.now();
  bannerTimer = 0;
  clearTutorialFocus();
  ui.levelOverlay.classList.add("hidden");
  ui.levelCompleteOverlay.classList.add("hidden");
  ui.inspector.classList.remove("open");
  createShop();
  selectShopTower("bear");
  createLevelMenu();
  updateUI();
  showBanner(`${LEVELS[levelIndex].icon} Màn ${levelIndex + 1} · ${LEVELS[levelIndex].name}`);
  if (forceTutorial || !campaign.completedTutorials[levelIndex + 1]) beginTutorial(levelIndex);
  return true;
}

function resetGame() {
  loadLevel(currentLevelIndex);
}

function selectShopTower(typeKey) {
  if (TOWER_ORDER.indexOf(typeKey) >= campaign.maxUnlockedLevel) return;
  state.selectedType = typeKey;
  state.selectedTowerId = null;
  const type = TOWER_TYPES[typeKey];
  ui.hint.textContent = `${type.icon} ${type.name} · Chạm vòng trống · 💧 ${type.cost}`;
  inspectType(type);
  updateShop();
  notifyTutorial(`select:${typeKey}`);
}

function selectPlacedTower(tower) {
  state.selectedType = null;
  state.selectedTowerId = tower.id;
  inspectTower(tower);
  updateShop();
}

function clearTowerSelection() {
  state.selectedType = null;
  state.selectedTowerId = null;
  ui.hint.textContent = "Chọn thú → chạm vòng trống.";
  ui.inspectorTitle.textContent = "Chưa chọn trụ";
  ui.inspectorDescription.textContent = "Chạm trụ để xem tầm, Nghiệp và nâng cấp.";
  ui.effectGrid.classList.add("hidden");
  ui.karmaReadout.classList.add("hidden");
  ui.upgradePanel.classList.add("hidden");
  ui.sellButton.classList.add("hidden");
  ui.inspector.classList.remove("open");
  updateShop();
}

function inspectType(type) {
  ui.inspectorTitle.textContent = type.name;
  ui.inspectorDescription.textContent = `${type.role} · ${type.summary}`;
  ui.yangEffect.textContent = type.yang;
  ui.yinEffect.textContent = type.yin;
  ui.effectGrid.classList.remove("hidden");
  ui.karmaReadout.classList.add("hidden");
  ui.upgradePanel.classList.add("hidden");
  ui.sellButton.classList.add("hidden");
  ui.inspector.classList.add("open");
}

function inspectTower(tower) {
  const type = TOWER_TYPES[tower.type];
  ui.inspectorTitle.textContent = `${type.name} · ${phaseName(tower.phase)} · ▲${tower.upgradeCount}`;
  ui.inspectorDescription.textContent = `${type.role} · ${type.summary}`;
  ui.yangEffect.textContent = type.yang;
  ui.yinEffect.textContent = type.yin;
  ui.effectGrid.classList.remove("hidden");
  ui.karmaReadout.classList.remove("hidden");
  ui.upgradePanel.classList.remove("hidden");
  ui.sellButton.classList.remove("hidden");
  ui.inspector.classList.add("open");
  updateInspectorMeter(tower);
  renderUpgradeOptions(tower);
}

function updateInspectorMeter(tower) {
  const type = TOWER_TYPES[tower.type];
  const percent = Math.max(0, Math.min(100, tower.karma / type.cycle * 100));
  ui.phaseLabel.textContent = phaseName(tower.phase);
  ui.phaseLabel.style.color = tower.phase === "Yang" ? COLORS.yang : "#bcaaf0";
  ui.karmaText.textContent = `${Math.ceil(tower.karma)} / ${type.cycle}`;
  ui.karmaFill.style.width = `${percent}%`;
  ui.karmaFill.classList.toggle("yin", tower.phase === "Yin");
}

function createLevelMenu() {
  ui.levelGrid.innerHTML = "";
  LEVELS.forEach((level, index) => {
    const unlocked = index < campaign.maxUnlockedLevel;
    const button = document.createElement("button");
    button.type = "button";
    button.className = `level-option${index === currentLevelIndex ? " current" : ""}`;
    button.disabled = !unlocked;
    button.innerHTML = `<span>${unlocked ? level.icon : "🔒"}</span><strong>Màn ${index + 1}</strong><small>${unlocked ? `${level.name} · ${level.waves} wave` : "Chưa mở"}</small>`;
    button.addEventListener("click", () => loadLevel(index));
    ui.levelGrid.appendChild(button);
  });
}

function openLevelMenu() {
  if (state.tutorial?.overlayVisible) return;
  state.pausedByMenu = true;
  createLevelMenu();
  ui.levelOverlay.classList.remove("hidden");
}

function closeLevelMenu() {
  state.pausedByMenu = false;
  ui.levelOverlay.classList.add("hidden");
}

function beginTutorial(levelIndex) {
  const steps = TUTORIALS[levelIndex];
  if (!steps?.length) return;
  state.tutorial = { levelIndex, stepIndex: 0, awaiting: null, overlayVisible: false };
  showTutorialStep();
}

function showTutorialStep() {
  const tutorial = state.tutorial;
  if (!tutorial) return;
  const steps = TUTORIALS[tutorial.levelIndex];
  if (tutorial.stepIndex >= steps.length) {
    finishTutorial();
    return;
  }
  const step = steps[tutorial.stepIndex];
  tutorial.overlayVisible = true;
  state.pausedByTutorial = true;
  clearTutorialFocus();
  ui.tutorialProgress.textContent = `${tutorial.stepIndex + 1} / ${steps.length}`;
  ui.tutorialIcon.textContent = step.icon;
  ui.tutorialKicker.textContent = step.kicker || "Hướng dẫn";
  ui.tutorialTitle.textContent = step.title;
  ui.tutorialBody.textContent = step.body;
  ui.tutorialContinue.textContent = tutorial.stepIndex === steps.length - 1 ? "Hoàn tất →" : "Tiếp tục →";
  ui.tutorialOverlay.classList.remove("hidden");
}

function continueTutorial() {
  const tutorial = state.tutorial;
  if (!tutorial) return;
  const steps = TUTORIALS[tutorial.levelIndex];
  const step = steps[tutorial.stepIndex];
  tutorial.overlayVisible = false;
  state.pausedByTutorial = false;
  ui.tutorialOverlay.classList.add("hidden");
  if (step.prepare) prepareTutorialStep(step.prepare);
  if (step.waitFor) {
    tutorial.awaiting = step.waitFor;
    focusTutorialTarget(step.target);
    return;
  }
  tutorial.stepIndex += 1;
  showTutorialStep();
}

function notifyTutorial(eventName) {
  const tutorial = state.tutorial;
  if (!tutorial || tutorial.awaiting !== eventName) return;
  if (eventName === "combo:bee_bear") {
    const bear = state.towers.find(tower => tower.type === "bear");
    if (bear) bear.tutorialGlobalAura = false;
  }
  tutorial.awaiting = null;
  tutorial.stepIndex += 1;
  clearTutorialFocus();
  showTutorialStep();
}

function prepareTutorialStep(action) {
  if (action === "select_bear") {
    const bear = state.towers.find(tower => tower.type === "bear");
    if (bear) selectPlacedTower(bear);
  }
  if (action === "prime_bear_yin") {
    const bear = state.towers.find(tower => tower.type === "bear");
    if (bear) bear.karma = Math.max(0, TOWER_TYPES.bear.cycle - TOWER_TYPES.bear.karmaPerAttack);
  }
  if (action === "prime_combo") {
    const bear = state.towers.find(tower => tower.type === "bear");
    const bee = state.towers.find(tower => tower.type === "bee");
    if (bear) {
      bear.phase = "Yin";
      bear.karma = TOWER_TYPES.bear.cycle;
      bear.tutorialGlobalAura = true;
    }
    if (bee) {
      bee.phase = "Yang";
      bee.karma = 0;
    }
  }
}

function finishTutorial() {
  if (!state.tutorial) return;
  const levelNumber = state.tutorial.levelIndex + 1;
  campaign.completedTutorials[levelNumber] = true;
  saveCampaign();
  state.tutorial = null;
  state.pausedByTutorial = false;
  ui.tutorialOverlay.classList.add("hidden");
  clearTutorialFocus();
  if (state.pendingLevelCompletion) completeLevel();
}

function focusTutorialTarget(target) {
  const element = tutorialTargetElement(target);
  if (element) element.classList.add("tutorial-focus");
}

function clearTutorialFocus() {
  if (!document.querySelectorAll) return;
  document.querySelectorAll(".tutorial-focus").forEach(element => element.classList.remove("tutorial-focus"));
}

function tutorialTargetElement(target) {
  if (!target) return null;
  if (target.startsWith("shop:")) {
    const type = target.split(":")[1];
    return Array.from(ui.shop.children).find(button => button.dataset.tower === type) || null;
  }
  if (target === "canvas") return canvas;
  if (target === "wave") return ui.waveButton;
  if (target === "upgrade") return ui.upgradePanel;
  if (target === "sell") return ui.sellButton;
  return null;
}

function placeTower(spotIndex) {
  const typeKey = state.selectedType;
  if (!typeKey || state.gameOver || state.levelComplete) return;
  const type = TOWER_TYPES[typeKey];
  const occupied = state.towers.some(tower => tower.spotIndex === spotIndex);
  if (occupied) {
    showBanner("Vòng này đã có trụ.", "danger");
    return;
  }
  if (state.water < type.cost) {
    showBanner("Không đủ Nước.", "danger");
    return;
  }

  const spot = BUILD_SPOTS[spotIndex];
  state.water -= type.cost;
  const tower = {
    id: state.nextTowerId++, type: typeKey, spotIndex, x: spot.x, y: spot.y,
    karma: 0, phase: "Yang", cooldown: Math.random() * 0.25, productionTimer: 0,
    yinLeakCounter: 0, shieldApplied: new Set(), upgradeCount: 0, upgradeSpent: 0,
    upgrades: { damage: 0, speed: 0, range: 0, production: 0 }
  };
  state.towers.push(tower);
  selectPlacedTower(tower);
  showBanner(`${type.icon} ${type.name} đã vào trận.`);
  updateUI();
  notifyTutorial(`place:${typeKey}`);
}

function dismissSelectedTower() {
  const index = state.towers.findIndex(tower => tower.id === state.selectedTowerId);
  if (index < 0 || state.waveActive) return;
  const [tower] = state.towers.splice(index, 1);
  const refund = Math.floor((TOWER_TYPES[tower.type].cost + tower.upgradeSpent) * 0.6);
  state.water += refund;
  state.selectedTowerId = null;
  state.selectedType = tower.type;
  inspectType(TOWER_TYPES[tower.type]);
  updateUI();
  showBanner(`${TOWER_TYPES[tower.type].icon} Đã bán · +${refund} 💧`);
  notifyTutorial(`sell:${tower.type}`);
}

function upgradeChoices(tower) {
  if (tower.type === "waterTower") return [{ key: "production", label: "💧 +2/lần" }];
  if (tower.type === "crab") return [{ key: "range", label: "◎ Tầm +15%" }];
  return [
    { key: "damage", label: "⚔ Sát thương +30%" },
    { key: "speed", label: "⏱ Tốc đánh +20%" },
    { key: "range", label: "◎ Tầm +15%" }
  ];
}

function upgradeCost(tower) {
  const multiplier = 0.45 + tower.upgradeCount * 0.2;
  return Math.max(1, Math.round(TOWER_TYPES[tower.type].cost * multiplier));
}

function upgradeTower(kind) {
  const tower = state.towers.find(item => item.id === state.selectedTowerId);
  if (!tower || state.waveActive || tower.upgradeCount >= MAX_UPGRADES) return false;
  if (!upgradeChoices(tower).some(choice => choice.key === kind)) return false;
  const cost = upgradeCost(tower);
  if (state.water < cost) {
    showBanner("Không đủ Nước.", "danger");
    return false;
  }
  state.water -= cost;
  tower.upgradeCount += 1;
  tower.upgradeSpent += cost;
  tower.upgrades[kind] += 1;
  addEffect(tower.x, tower.y - 18, "▲", COLORS.water);
  showBanner(`${TOWER_TYPES[tower.type].icon} Nâng cấp ${tower.upgradeCount}/${MAX_UPGRADES}`);
  updateUI();
  notifyTutorial(`upgrade:${tower.type}`);
  return true;
}

function renderUpgradeOptions(tower) {
  const maxed = tower.upgradeCount >= MAX_UPGRADES;
  const cost = maxed ? 0 : upgradeCost(tower);
  const choices = upgradeChoices(tower);
  const signature = `${tower.type}:${tower.id}:${tower.upgradeCount}:${state.waveActive}:${state.water < cost}`;
  ui.upgradeLimit.textContent = `${tower.upgradeCount} / ${MAX_UPGRADES}`;
  if (ui.upgradeOptions.dataset.signature === signature) return;
  ui.upgradeOptions.dataset.signature = signature;
  ui.upgradeOptions.classList.toggle("one-option", choices.length === 1);
  ui.upgradeOptions.innerHTML = choices.map(choice => {
    const level = tower.upgrades[choice.key];
    const disabled = maxed || state.waveActive || state.water < cost;
    const price = maxed ? "Tối đa" : `💧 ${cost}`;
    return `<button type="button" data-upgrade="${choice.key}" ${disabled ? "disabled" : ""}>${choice.label}<br><small>+${level} · ${price}</small></button>`;
  }).join("");
}

function towerDamage(tower) {
  return TOWER_TYPES[tower.type].damage * (1 + tower.upgrades.damage * 0.3);
}

function towerFireRate(tower) {
  return TOWER_TYPES[tower.type].fireRate * (1 + tower.upgrades.speed * 0.2);
}

function towerRange(tower) {
  return TOWER_TYPES[tower.type].range * (1 + tower.upgrades.range * 0.15);
}

function waterYield(tower) {
  return 5 + tower.upgrades.production * 2;
}

function startWave() {
  if (state.waveActive || state.gameOver || state.levelComplete || state.wave >= WAVES.length) return;
  const config = WAVES[state.wave];
  state.wave += 1;
  state.waveActive = true;
  state.spawnQueue = config.count;
  state.spawnTimer = 0;
  showBanner(`⚔ Đợt ${state.wave} ${dangerPips(state.wave - 1)}`, "danger");
  updateUI();
  notifyTutorial("wave:start");
}

function spawnEnemy() {
  const config = WAVES[state.wave - 1];
  const sequence = config.count - state.spawnQueue;
  const isFlying = config.type === "mixed" && sequence % 4 === 3;
  const isElite = config.type === "elite" && (sequence % 4 === 0 || sequence === config.count - 1);
  const hpScale = isElite ? 2.1 : isFlying ? 0.78 : 1;
  const speedScale = isElite ? 0.72 : isFlying ? 1.2 : 1;
  const hp = Math.round(config.hp * hpScale);
  state.enemies.push({
    id: state.nextEnemyId++,
    kind: isElite ? "elite" : isFlying ? "flying" : "ground",
    x: PATH[0].x,
    y: PATH[0].y - (isFlying ? 23 : 0),
    segment: 0,
    progress: 0,
    hp,
    maxHp: hp,
    baseSpeed: config.speed * speedScale,
    reward: Math.round(config.reward * (isElite ? 2.4 : 1)),
    slow: 1,
    speedBonus: 0,
    physicalArmor: isElite ? 0.3 : 0,
    magicResist: isFlying ? 0.2 : isElite ? 0.15 : 0,
    shield: 0,
    armorBuff: 0,
    poison: 0,
    poisonTimer: 0,
    bearSlowTimer: 0,
    reachedEnd: false,
    dead: false
  });
}

function updateGame(dt) {
  if (state.paused || state.pausedByTutorial || state.pausedByMenu || state.gameOver || state.levelComplete) return;
  state.elapsed += dt;

  if (!state.waveActive) {
    updateEffects(dt);
    return;
  }

  if (state.spawnQueue > 0) {
    state.spawnTimer -= dt;
    if (state.spawnTimer <= 0) {
      spawnEnemy();
      state.spawnQueue -= 1;
      state.spawnTimer = WAVES[state.wave - 1].interval;
    }
  }

  resetEnemyModifiers();
  applyTowerAuras();
  updateTowers(dt);
  updateEnemies(dt);
  updateEffects(dt);
  finishWaveIfReady();
}

function resetEnemyModifiers() {
  state.enemies.forEach(enemy => {
    enemy.slow = (enemy.bearSlowTimer || 0) > 0 ? 0.65 : 1;
    enemy.speedBonus = 0;
    enemy.armorBuff = 0;
  });
}

function applyTowerAuras() {
  for (const tower of state.towers) {
    const range = towerRange(tower);
    if (tower.type === "bear" && tower.phase === "Yin") {
      const auraRange = tower.tutorialGlobalAura ? Infinity : range;
      for (const enemy of enemiesInRange(tower, auraRange, false)) enemy.speedBonus = Math.max(enemy.speedBonus, 0.45);
    }
    if (tower.type === "crab" && tower.phase === "Yang") {
      for (const enemy of enemiesInRange(tower, range, true)) enemy.slow = Math.min(enemy.slow, 0.72);
    }
    if (tower.type === "bee" && tower.phase === "Yin") {
      for (const enemy of enemiesInRange(tower, range, true)) {
        enemy.armorBuff = Math.max(enemy.armorBuff, 0.35);
        if (!tower.shieldApplied.has(enemy.id)) {
          enemy.shield += enemy.maxHp * 0.18;
          tower.shieldApplied.add(enemy.id);
          addEffect(enemy.x, enemy.y, "shield", COLORS.yin);
        }
      }
    }
  }
}

function updateTowers(dt) {
  for (const tower of state.towers) {
    const type = TOWER_TYPES[tower.type];

    if (tower.phase === "Yin") {
      let discharge = type.discharge;
      if (tower.type === "bee") discharge += 0;
      if (tower.type === "crab" && state.enemies.length > 0) discharge = 20;
      tower.karma = Math.max(0, tower.karma - discharge * dt);
      if (tower.karma <= 0) {
        tower.phase = "Yang";
        tower.yinLeakCounter = 0;
        tower.shieldApplied.clear();
        addEffect(tower.x, tower.y, "phase", COLORS.yang);
      }
    }

    if (tower.type === "waterTower") {
      updateWaterTower(tower, dt);
      continue;
    }

    tower.cooldown -= dt;
    if (tower.cooldown > 0) continue;

    const target = chooseTarget(tower);
    if (!target) continue;
    attack(tower, target);

    let attackSpeedMultiplier = 1;
    if (tower.type === "fox" && tower.phase === "Yin") attackSpeedMultiplier = 0.72;
    if (isInsideYinCrabAura(tower)) attackSpeedMultiplier *= 0.8;
    tower.cooldown = 1 / (towerFireRate(tower) * attackSpeedMultiplier);
  }
}

function updateWaterTower(tower, dt) {
  const type = TOWER_TYPES[tower.type];
  tower.productionTimer += dt;
  const interval = tower.phase === "Yin" ? 1.5 : 3;
  if (tower.productionTimer < interval) return;
  tower.productionTimer -= interval;
  const produced = waterYield(tower);
  state.water += produced;
  addEffect(tower.x, tower.y - 18, `+${produced}`, COLORS.water);

  if (tower.phase === "Yang") {
    gainKarma(tower, type.karmaPerProduction);
  } else {
    tower.yinLeakCounter += 1;
    if (tower.yinLeakCounter >= 3) {
      tower.yinLeakCounter = 0;
      state.health = Math.max(0, state.health - 1);
      showBanner("Trụ Nước Âm làm đền mất 1 máu!", "danger");
      if (state.health <= 0) endGame(false);
    }
  }
}

function attack(tower, primaryTarget) {
  const type = TOWER_TYPES[tower.type];
  const phaseAtAttack = tower.phase;
  let targets = [primaryTarget];
  let triggeredBeeBearCombo = false;

  if (tower.type === "bear" && phaseAtAttack === "Yang") {
    targets = state.enemies.filter(enemy => !enemy.dead && enemy.kind !== "flying" && distance(enemy, primaryTarget) <= 45);
  }
  if (tower.type === "bee") {
    targets = state.enemies.filter(enemy => !enemy.dead && distance(enemy, primaryTarget) <= 55);
  }

  for (const enemy of targets) {
    let damage = towerDamage(tower);
    let damageType = tower.type === "bee" ? "magic" : "physical";

    if (tower.type === "bee" && phaseAtAttack === "Yang") {
      triggeredBeeBearCombo ||= enemy.speedBonus > 0 && state.towers.some(item => item.type === "bear" && item.phase === "Yin");
      damage *= 1 + enemy.speedBonus * 1.3;
      enemy.poison = Math.max(enemy.poison, 4);
      enemy.poisonTimer = 4;
    }
    if (tower.type === "bear" && phaseAtAttack === "Yang") {
      const newlySlowed = (enemy.bearSlowTimer || 0) <= 0;
      enemy.bearSlowTimer = Math.max(enemy.bearSlowTimer || 0, 1.8);
      enemy.slow = Math.min(enemy.slow, 0.65);
      if (newlySlowed) addEffect(enemy.x, enemy.y - 18, "🐌", "#8fd7ff");
    }
    if (tower.type === "fox" && phaseAtAttack === "Yin") damage *= 1.45;

    const armorPen = damageType === "physical" && isInsideYangCrabAura(tower) ? 0.25 : 0;
    const damageDealt = applyDamage(enemy, damage, damageType, armorPen);
    addDamageNumber(enemy.x, enemy.y, damageDealt, damageType);
  }

  state.effects.push({ kind: "shot", x1: tower.x, y1: tower.y, x2: primaryTarget.x, y2: primaryTarget.y, color: type.projectile, life: 0.13, maxLife: 0.13 });
  addEffect(primaryTarget.x, primaryTarget.y, "hit", type.projectile);

  if (phaseAtAttack === "Yang") {
    gainKarma(tower, type.karmaPerAttack);
  } else if (tower.type === "bee") {
    tower.karma = Math.max(0, tower.karma - 5);
  }
  if (triggeredBeeBearCombo) notifyTutorial("combo:bee_bear");
}

function gainKarma(tower, amount) {
  if (tower.phase !== "Yang") return;
  const multiplier = isInsideYinCrabAura(tower) ? 1.75 : 1;
  const type = TOWER_TYPES[tower.type];
  tower.karma = Math.min(type.cycle, tower.karma + amount * multiplier);
  if (tower.karma >= type.cycle) {
    tower.phase = "Yin";
    tower.yinLeakCounter = 0;
    tower.shieldApplied.clear();
    addEffect(tower.x, tower.y, "phase", COLORS.yin);
    showBanner(`${type.icon} ${type.name} vào Âm!`, "danger");
    notifyTutorial(`phase:${tower.type}:Yin`);
  }
}

function chooseTarget(tower) {
  let candidates = enemiesInRange(tower, towerRange(tower), tower.type === "bee" || tower.type === "crab");
  if (!candidates.length) return null;

  if (tower.type === "fox" && tower.phase === "Yin") {
    candidates.sort((a, b) => targetValue(b) - targetValue(a));
  } else {
    candidates.sort((a, b) => pathScore(b) - pathScore(a));
  }
  return candidates[0];
}

function targetValue(enemy) {
  return (enemy.kind === "elite" ? 10000 : 0) + enemy.maxHp * 2 + pathScore(enemy);
}

function pathScore(enemy) {
  return enemy.segment * 1000 + enemy.progress;
}

function enemiesInRange(tower, range, canTargetFlying) {
  return state.enemies.filter(enemy => !enemy.dead && (canTargetFlying || enemy.kind !== "flying") && distance(tower, enemy) <= range);
}

function canTowerAffectEnemy(tower, enemy) {
  const type = TOWER_TYPES[tower.type];
  if (!type || type.range <= 0 || enemy.dead) return false;
  const canTargetFlying = tower.type === "bee" || tower.type === "crab";
  return (canTargetFlying || enemy.kind !== "flying") && distance(tower, enemy) <= towerRange(tower);
}

function isInsideYangCrabAura(tower) {
  return state.towers.some(crab => crab.type === "crab" && crab.phase === "Yang" && crab.id !== tower.id && distance(crab, tower) <= towerRange(crab));
}

function isInsideYinCrabAura(tower) {
  return state.towers.some(crab => crab.type === "crab" && crab.phase === "Yin" && crab.id !== tower.id && distance(crab, tower) <= towerRange(crab));
}

function applyDamage(enemy, amount, damageType, armorPen = 0) {
  if (damageType === "physical") amount *= 1 - physicalArmor(enemy) * (1 - armorPen);
  if (damageType === "magic") amount *= 1 - Math.max(0, Math.min(0.75, enemy.magicResist || 0));
  const damageDealt = Math.max(0, amount);
  if (enemy.shield > 0) {
    const absorbed = Math.min(enemy.shield, amount);
    enemy.shield -= absorbed;
    amount -= absorbed;
  }
  enemy.hp -= amount;
  if (enemy.hp <= 0 && !enemy.dead) killEnemy(enemy);
  return damageDealt;
}

function physicalArmor(enemy) {
  return Math.max(0, Math.min(0.75, (enemy.physicalArmor || 0) + (enemy.armorBuff || 0)));
}

function killEnemy(enemy) {
  enemy.dead = true;
  state.water += enemy.reward;
  addEffect(enemy.x, enemy.y, `+${enemy.reward}`, COLORS.water);
  for (const tower of state.towers) {
    if (tower.phase === "Yang" && tower.type !== "waterTower") gainKarma(tower, 5);
  }
}

function updateEnemies(dt) {
  for (const enemy of state.enemies) {
    if (enemy.dead || enemy.reachedEnd) continue;

    if (enemy.poisonTimer > 0) {
      enemy.poisonTimer -= dt;
      applyDamage(enemy, enemy.poison * dt, "magic");
      if (enemy.dead) continue;
    }

    enemy.bearSlowTimer = Math.max(0, (enemy.bearSlowTimer || 0) - dt);

    const speed = enemy.baseSpeed * enemy.slow * (1 + enemy.speedBonus);
    moveAlongPath(enemy, speed * dt);
  }

  for (const enemy of state.enemies) {
    if (!enemy.reachedEnd || enemy.dead) continue;
    enemy.dead = true;
    const damage = altarDamage(enemy);
    state.health = Math.max(0, state.health - damage);
    showBanner(`${enemy.kind === "elite" ? "Tinh anh" : "Địch"} lọt qua! -${damage} máu`, "danger");
    if (state.health <= 0) endGame(false);
  }

  state.enemies = state.enemies.filter(enemy => !enemy.dead);
}

function altarDamage(enemy) {
  return enemy.kind === "elite" ? 4 : 2;
}

function moveAlongPath(enemy, distanceToMove) {
  while (distanceToMove > 0 && enemy.segment < PATH.length - 1) {
    const start = PATH[enemy.segment];
    const end = PATH[enemy.segment + 1];
    const dx = end.x - start.x;
    const dy = end.y - start.y;
    const length = Math.hypot(dx, dy);
    const remaining = length - enemy.progress;

    if (distanceToMove < remaining) {
      enemy.progress += distanceToMove;
      distanceToMove = 0;
    } else {
      distanceToMove -= remaining;
      enemy.segment += 1;
      enemy.progress = 0;
    }

    if (enemy.segment >= PATH.length - 1) {
      enemy.reachedEnd = true;
      break;
    }

    const current = PATH[enemy.segment];
    const next = PATH[enemy.segment + 1];
    const segmentLength = Math.hypot(next.x - current.x, next.y - current.y);
    const t = segmentLength > 0 ? enemy.progress / segmentLength : 0;
    enemy.x = current.x + (next.x - current.x) * t;
    enemy.y = current.y + (next.y - current.y) * t - (enemy.kind === "flying" ? 23 : 0);
  }
}

function finishWaveIfReady() {
  if (!state.waveActive || state.spawnQueue > 0 || state.enemies.length > 0) return;
  state.waveActive = false;
  const bonus = 24 + state.wave * 4;
  state.water += bonus;
  if (state.wave >= WAVES.length) {
    state.pendingLevelCompletion = true;
    notifyTutorial("level:waves_complete");
    if (!state.tutorial) completeLevel();
  } else {
    showBanner(`Qua đợt ${state.wave} · +${bonus} 💧`, "success");
  }
  updateUI();
}

function completeLevel() {
  if (state.levelComplete) return;
  state.waveActive = false;
  state.levelComplete = true;
  state.pendingLevelCompletion = false;

  const isFinalLevel = currentLevelIndex === LEVELS.length - 1;
  if (!isFinalLevel) {
    campaign.maxUnlockedLevel = Math.max(campaign.maxUnlockedLevel, currentLevelIndex + 2);
    campaign.currentLevel = currentLevelIndex + 2;
  } else {
    campaign.currentLevel = LEVELS.length;
  }
  saveCampaign();
  createLevelMenu();

  ui.levelCompleteTitle.textContent = isFinalLevel ? "🌧 Gọi mưa thành công" : `Qua màn ${currentLevelIndex + 1}`;
  if (isFinalLevel) {
    ui.levelCompleteBody.textContent = "Con Cóc đã tập hợp đủ năm linh thú và gọi được mưa.";
    ui.nextLevelButton.textContent = "Về màn 1 →";
    ui.nextLevelButton.dataset.nextLevel = "0";
  } else {
    const nextLevel = LEVELS[currentLevelIndex + 1];
    const unlockedTower = TOWER_TYPES[nextLevel.unlock];
    ui.levelCompleteBody.textContent = `Mở khóa ${unlockedTower.icon} ${unlockedTower.name} · ${nextLevel.waves} wave mới.`;
    ui.nextLevelButton.textContent = `Màn ${currentLevelIndex + 2} →`;
    ui.nextLevelButton.dataset.nextLevel = String(currentLevelIndex + 1);
  }
  ui.levelCompleteOverlay.classList.remove("hidden");
  showBanner(isFinalLevel ? "Mưa đã về!" : `Mở khóa màn ${currentLevelIndex + 2}!`, "success", 8);
  updateUI();
}

function endGame(victory) {
  if (victory) {
    completeLevel();
    return;
  }
  state.waveActive = false;
  state.gameOver = true;
  showBanner("Đền mưa thất thủ!", "danger", 8);
  updateUI();
}

function updateEffects(dt) {
  state.effects.forEach(effect => effect.life -= dt);
  state.effects = state.effects.filter(effect => effect.life > 0);
  if (bannerTimer > 0) {
    bannerTimer -= dt;
    if (bannerTimer <= 0) ui.banner.classList.remove("visible");
  }
}

function addEffect(x, y, kind, color) {
  state.effects.push({ kind, x, y, color, life: kind === "phase" ? 0.75 : 0.5, maxLife: kind === "phase" ? 0.75 : 0.5 });
}

function addDamageNumber(x, y, amount, damageType) {
  state.effects.push({
    kind: "damage",
    text: `-${Math.max(0, Math.round(amount))}`,
    x,
    y,
    color: damageType === "magic" ? "#d2adff" : "#ffb083",
    life: 0.72,
    maxLife: 0.72
  });
}

function showBanner(message, tone = "", duration = 2.4) {
  ui.banner.textContent = message;
  ui.banner.className = `battle-banner visible ${tone}`.trim();
  bannerTimer = duration;
}

function updateUI() {
  ui.water.textContent = Math.floor(state.water);
  const healthRatio = Math.max(0, Math.min(1, state.health / MAX_HEALTH));
  ui.healthFill.style.width = `${healthRatio * 100}%`;
  ui.healthBar.setAttribute("aria-valuenow", String(state.health));
  ui.healthBar.classList.toggle("danger", healthRatio <= 0.25);
  ui.wave.textContent = `${state.wave} / ${WAVES.length}`;
  ui.enemies.textContent = state.enemies.length + state.spawnQueue;
  ui.levelButton.textContent = `🗺 Màn ${currentLevelIndex + 1}`;

  ui.waveButton.disabled = state.waveActive || state.gameOver || state.levelComplete;
  const shownWave = state.waveActive ? state.wave : Math.min(state.wave + 1, WAVES.length);
  ui.waveButton.textContent = state.levelComplete ? "✓ Hoàn tất" : state.gameOver ? "Đền thất thủ" : state.wave >= WAVES.length ? `Đã qua ${WAVES.length} đợt` : `⚔ Đợt ${shownWave} ${dangerPips(shownWave - 1)}`;
  ui.pauseButton.textContent = state.paused ? "▶" : "⏸";
  ui.pauseButton.setAttribute("aria-label", state.paused ? "Tiếp tục" : "Tạm dừng");
  ui.sellButton.disabled = state.waveActive;

  updateShop();
  const selected = state.towers.find(tower => tower.id === state.selectedTowerId);
  if (selected) inspectTower(selected);
}

function updateShop() {
  ui.shop.querySelectorAll(".tower-card").forEach(button => {
    const key = button.dataset.tower;
    button.classList.toggle("selected", state.selectedType === key);
    button.classList.toggle("unaffordable", state.water < TOWER_TYPES[key].cost);
    button.setAttribute("aria-pressed", String(state.selectedType === key));
  });
}

function draw() {
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  drawBackground();
  drawPath();
  drawBuildSpots();
  drawTowerRanges();
  drawTowers();
  drawEnemies();
  drawSelectedTargets();
  drawEffects();
  drawAltar();

  if (state.paused) drawOverlay("Tạm dừng", "Nghiệp đang chờ.");
  if (state.gameOver) drawOverlay("Đền mưa thất thủ", "Chạm nút ↻ để chơi lại.");
}

function drawBackground() {
  const gradient = ctx.createLinearGradient(0, 0, 0, canvas.height);
  gradient.addColorStop(0, "#2c4a4a");
  gradient.addColorStop(1, "#172b33");
  ctx.fillStyle = gradient;
  ctx.fillRect(0, 0, canvas.width, canvas.height);

  ctx.globalAlpha = 0.16;
  for (let y = 20; y < canvas.height; y += 48) {
    for (let x = (y / 48 % 2) * 22; x < canvas.width; x += 46) {
      ctx.fillStyle = (x + y) % 3 === 0 ? "#82a66f" : "#456e5b";
      ctx.beginPath();
      ctx.arc(x, y, 5 + ((x + y) % 8), 0, Math.PI * 2);
      ctx.fill();
    }
  }
  ctx.globalAlpha = 1;
}

function drawPath() {
  ctx.lineJoin = "round";
  ctx.lineCap = "round";
  ctx.beginPath();
  PATH.forEach((point, index) => index === 0 ? ctx.moveTo(point.x, point.y) : ctx.lineTo(point.x, point.y));
  ctx.strokeStyle = COLORS.pathEdge;
  ctx.lineWidth = 58;
  ctx.stroke();
  ctx.strokeStyle = COLORS.path;
  ctx.lineWidth = 48;
  ctx.stroke();
  ctx.setLineDash([8, 14]);
  ctx.strokeStyle = "rgba(255,245,210,0.2)";
  ctx.lineWidth = 2;
  ctx.stroke();
  ctx.setLineDash([]);
}

function drawBuildSpots() {
  BUILD_SPOTS.forEach((spot, index) => {
    const tower = state.towers.find(item => item.spotIndex === index);
    ctx.beginPath();
    ctx.arc(spot.x, spot.y, 26, 0, Math.PI * 2);
    ctx.fillStyle = tower ? "rgba(10,18,29,0.52)" : "rgba(238,222,178,0.12)";
    ctx.fill();
    ctx.lineWidth = tower ? 2 : 3;
    ctx.strokeStyle = tower ? "rgba(245,224,165,0.22)" : "rgba(245,224,165,0.55)";
    ctx.stroke();
    if (!tower) {
      ctx.fillStyle = "rgba(247,240,216,0.5)";
      ctx.font = "700 20px system-ui";
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      ctx.fillText("+", spot.x, spot.y - 1);
    }
  });
}

function drawTowerRanges() {
  const selected = state.towers.find(tower => tower.id === state.selectedTowerId);
  if (!selected) return;
  const range = towerRange(selected);
  if (range <= 0) return;
  const isYang = selected.phase === "Yang";
  const pulse = (Math.sin(state.elapsed * 5) + 1) / 2;
  ctx.save();
  ctx.beginPath();
  ctx.arc(selected.x, selected.y, range, 0, Math.PI * 2);
  ctx.fillStyle = isYang ? "rgba(244,200,91,0.14)" : "rgba(139,119,199,0.17)";
  ctx.fill();
  ctx.strokeStyle = isYang ? `rgba(244,200,91,${0.16 + pulse * 0.08})` : `rgba(184,165,235,${0.18 + pulse * 0.08})`;
  ctx.lineWidth = 9 + pulse * 3;
  ctx.stroke();
  ctx.beginPath();
  ctx.arc(selected.x, selected.y, range, 0, Math.PI * 2);
  ctx.setLineDash([11, 7]);
  ctx.strokeStyle = isYang ? "rgba(255,222,125,0.92)" : "rgba(207,190,255,0.94)";
  ctx.lineWidth = 3;
  ctx.stroke();
  ctx.setLineDash([]);
  ctx.restore();
}

function drawTowers() {
  state.towers.forEach(tower => {
    const type = TOWER_TYPES[tower.type];
    const selected = tower.id === state.selectedTowerId;
    ctx.save();
    ctx.translate(tower.x, tower.y);

    if (tower.phase === "Yin") {
      ctx.beginPath();
      ctx.arc(0, 0, 32 + Math.sin(state.elapsed * 4) * 2, 0, Math.PI * 2);
      ctx.fillStyle = "rgba(139,119,199,0.2)";
      ctx.fill();
    }

    ctx.beginPath();
    ctx.arc(0, 0, 23, 0, Math.PI * 2);
    ctx.fillStyle = type.color;
    ctx.fill();
    ctx.strokeStyle = selected ? COLORS.water : tower.phase === "Yang" ? COLORS.yang : "#b8a5ec";
    ctx.lineWidth = selected ? 4 : 2;
    ctx.stroke();

    ctx.font = "25px 'Segoe UI Emoji', sans-serif";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(type.icon, 0, 1);

    const ratio = tower.karma / type.cycle;
    ctx.beginPath();
    ctx.arc(0, 0, 29, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * ratio);
    ctx.strokeStyle = tower.phase === "Yang" ? COLORS.yang : "#b8a5ec";
    ctx.lineWidth = 4;
    ctx.stroke();

    if (tower.upgradeCount > 0) {
      ctx.fillStyle = COLORS.water;
      ctx.font = "bold 10px system-ui";
      ctx.fillText(`▲${tower.upgradeCount}`, 0, 38);
    }
    ctx.restore();
  });
}

function drawEnemies() {
  state.enemies.forEach(enemy => {
    ctx.save();
    ctx.translate(enemy.x, enemy.y);
    const radius = enemy.kind === "elite" ? 16 : enemy.kind === "flying" ? 11 : 12;

    if (enemy.shield > 0) {
      ctx.beginPath();
      ctx.arc(0, 0, radius + 6, 0, Math.PI * 2);
      ctx.strokeStyle = "rgba(184,165,236,0.88)";
      ctx.lineWidth = 3;
      ctx.stroke();
    }

    ctx.beginPath();
    ctx.arc(0, 0, radius, 0, Math.PI * 2);
    ctx.fillStyle = enemy.kind === "elite" ? "#9f4b61" : enemy.kind === "flying" ? "#7b75aa" : "#475161";
    ctx.fill();
    ctx.strokeStyle = physicalArmor(enemy) > 0 ? "#d5d9dc" : "rgba(255,255,255,0.35)";
    ctx.lineWidth = physicalArmor(enemy) > 0 ? 3 : 1.5;
    ctx.stroke();

    ctx.fillStyle = COLORS.ink;
    ctx.font = enemy.kind === "elite" ? "bold 16px system-ui" : "bold 13px system-ui";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(enemy.kind === "elite" ? "♛" : enemy.kind === "flying" ? "◆" : "●", 0, 0);

    const width = enemy.kind === "elite" ? 38 : 30;
    ctx.fillStyle = "rgba(0,0,0,0.58)";
    ctx.fillRect(-width / 2, -radius - 11, width, 4);
    ctx.fillStyle = enemy.poisonTimer > 0 ? COLORS.poison : COLORS.damage;
    ctx.fillRect(-width / 2, -radius - 11, width * Math.max(0, enemy.hp / enemy.maxHp), 4);
    drawEnemyBadges(enemy, radius);
    ctx.restore();
  });
}

function enemyStatusBadges(enemy) {
  const badges = [];
  if (enemy.slow < 0.99 || (enemy.bearSlowTimer || 0) > 0) badges.push({ icon: "🐌", color: "#8fd7ff" });
  if (enemy.speedBonus > 0.01) badges.push({ icon: "⚡", color: "#ffb45e" });
  if (enemy.poisonTimer > 0) badges.push({ icon: "☠", color: COLORS.poison });
  if (physicalArmor(enemy) > 0) badges.push({ icon: "⬡", color: "#e1e5e9" });
  if ((enemy.magicResist || 0) > 0) badges.push({ icon: "✦", color: "#d6b4ff" });
  if (enemy.shield > 0) badges.push({ icon: "🛡", color: "#c7b8f4" });
  return badges;
}

function drawEnemyBadges(enemy, radius) {
  const badges = enemyStatusBadges(enemy);
  if (!badges.length) return;
  const gap = 18;
  const startX = -(badges.length - 1) * gap / 2;
  badges.forEach((badge, index) => {
    const x = startX + index * gap;
    const y = -radius - 25;
    ctx.beginPath();
    ctx.arc(x, y, 8, 0, Math.PI * 2);
    ctx.fillStyle = "rgba(8,14,25,0.9)";
    ctx.fill();
    ctx.strokeStyle = badge.color;
    ctx.lineWidth = 1.5;
    ctx.stroke();
    ctx.fillStyle = badge.color;
    ctx.font = "11px 'Segoe UI Emoji', system-ui";
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    ctx.fillText(badge.icon, x, y + 0.5);
  });
}

function drawSelectedTargets() {
  const selected = state.towers.find(tower => tower.id === state.selectedTowerId);
  if (!selected || towerRange(selected) <= 0) return;
  const color = selected.phase === "Yang" ? "#ffe07d" : "#cfbeff";
  const pulse = (Math.sin(state.elapsed * 7) + 1) / 2;
  state.enemies.filter(enemy => canTowerAffectEnemy(selected, enemy)).forEach(enemy => {
    const radius = enemy.kind === "elite" ? 23 : enemy.kind === "flying" ? 18 : 19;
    ctx.beginPath();
    ctx.arc(enemy.x, enemy.y, radius + pulse * 3, 0, Math.PI * 2);
    ctx.strokeStyle = "rgba(255,255,255,0.9)";
    ctx.lineWidth = 5;
    ctx.stroke();
    ctx.beginPath();
    ctx.arc(enemy.x, enemy.y, radius + pulse * 3, 0, Math.PI * 2);
    ctx.strokeStyle = color;
    ctx.lineWidth = 2.5;
    ctx.stroke();
  });
}

function drawEffects() {
  state.effects.forEach(effect => {
    const alpha = Math.max(0, effect.life / effect.maxLife);
    ctx.save();
    ctx.globalAlpha = alpha;
    if (effect.kind === "shot") {
      ctx.beginPath();
      ctx.moveTo(effect.x1, effect.y1);
      ctx.lineTo(effect.x2, effect.y2);
      ctx.strokeStyle = effect.color;
      ctx.lineWidth = 3;
      ctx.stroke();
    } else if (effect.kind === "phase") {
      ctx.beginPath();
      ctx.arc(effect.x, effect.y, 22 + (1 - alpha) * 40, 0, Math.PI * 2);
      ctx.strokeStyle = effect.color;
      ctx.lineWidth = 4;
      ctx.stroke();
    } else if (effect.kind === "hit" || effect.kind === "shield") {
      ctx.beginPath();
      ctx.arc(effect.x, effect.y, 8 + (1 - alpha) * 13, 0, Math.PI * 2);
      ctx.strokeStyle = effect.color;
      ctx.lineWidth = 3;
      ctx.stroke();
    } else if (effect.kind === "damage") {
      const rise = (1 - alpha) * 28;
      ctx.font = "900 18px system-ui";
      ctx.textAlign = "center";
      ctx.textBaseline = "middle";
      ctx.lineWidth = 4;
      ctx.strokeStyle = "rgba(7,12,22,0.9)";
      ctx.strokeText(effect.text, effect.x, effect.y - 24 - rise);
      ctx.fillStyle = effect.color;
      ctx.fillText(effect.text, effect.x, effect.y - 24 - rise);
    } else {
      ctx.fillStyle = effect.color;
      ctx.font = "bold 15px system-ui";
      ctx.textAlign = "center";
      ctx.fillText(effect.kind, effect.x, effect.y - (1 - alpha) * 20);
    }
    ctx.restore();
  });
}

function drawAltar() {
  ctx.save();
  ctx.translate(925, 250);
  const healthRatio = Math.max(0, Math.min(1, state.health / MAX_HEALTH));
  ctx.fillStyle = "rgba(5,10,18,0.82)";
  ctx.fillRect(-39, -51, 78, 8);
  ctx.fillStyle = healthRatio <= 0.25 ? COLORS.damage : "#9dcc79";
  ctx.fillRect(-38, -50, 76 * healthRatio, 6);
  ctx.beginPath();
  ctx.arc(0, 0, 32, 0, Math.PI * 2);
  ctx.fillStyle = "rgba(98,205,229,0.2)";
  ctx.fill();
  ctx.strokeStyle = COLORS.water;
  ctx.lineWidth = 4;
  ctx.stroke();
  ctx.fillStyle = COLORS.ink;
  ctx.font = "28px 'Segoe UI Emoji', sans-serif";
  ctx.textAlign = "center";
  ctx.textBaseline = "middle";
  ctx.fillText("🐸", 0, 1);
  ctx.fillStyle = COLORS.water;
  ctx.font = "bold 11px system-ui";
  ctx.fillText("ĐỀN MƯA", 0, 48);
  ctx.restore();
}

function drawOverlay(title, subtitle) {
  ctx.fillStyle = "rgba(6,12,23,0.68)";
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  ctx.fillStyle = COLORS.ink;
  ctx.textAlign = "center";
  ctx.font = "bold 42px Georgia";
  ctx.fillText(title, canvas.width / 2, canvas.height / 2 - 10);
  ctx.fillStyle = COLORS.muted;
  ctx.font = "18px system-ui";
  ctx.fillText(subtitle, canvas.width / 2, canvas.height / 2 + 28);
}

function distance(a, b) {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

function phaseName(phase) {
  return phase === "Yang" ? "Dương" : "Âm";
}

function dangerPips(waveIndex) {
  return "◆".repeat(Math.max(1, Math.min(WAVES.length, waveIndex + 1)));
}

function pointerPosition(event) {
  const rect = canvas.getBoundingClientRect();
  return {
    x: (event.clientX - rect.left) * canvas.width / rect.width,
    y: (event.clientY - rect.top) * canvas.height / rect.height
  };
}

canvas.addEventListener("pointerdown", event => {
  event.preventDefault();
  if (state.gameOver || state.levelComplete || state.pausedByTutorial || state.pausedByMenu) return;
  const point = pointerPosition(event);
  const tower = state.towers.find(item => distance(item, point) <= 30);
  if (tower) {
    selectPlacedTower(tower);
    return;
  }
  if (state.selectedTowerId !== null) {
    clearTowerSelection();
    return;
  }
  const spotIndex = BUILD_SPOTS.findIndex(spot => distance(spot, point) <= 35);
  if (spotIndex >= 0) placeTower(spotIndex);
});

ui.waveButton.addEventListener("click", startWave);
ui.pauseButton.addEventListener("click", () => {
  if (state.gameOver || state.levelComplete || state.pausedByTutorial || state.pausedByMenu) return;
  state.paused = !state.paused;
  updateUI();
});
ui.restartButton.addEventListener("click", resetGame);
ui.levelButton.addEventListener("click", openLevelMenu);
ui.levelClose.addEventListener("click", closeLevelMenu);
ui.tutorialButton.addEventListener("click", () => loadLevel(currentLevelIndex, true));
ui.tutorialContinue.addEventListener("click", continueTutorial);
ui.inspectorClose.addEventListener("click", clearTowerSelection);
ui.replayLevelButton.addEventListener("click", () => loadLevel(currentLevelIndex));
ui.nextLevelButton.addEventListener("click", () => {
  const nextLevel = Number(ui.nextLevelButton.dataset.nextLevel);
  loadLevel(Number.isFinite(nextLevel) ? nextLevel : currentLevelIndex);
});
ui.sellButton.addEventListener("click", dismissSelectedTower);
ui.upgradeOptions.addEventListener("click", event => {
  const button = event.target.closest("button[data-upgrade]");
  if (button) upgradeTower(button.dataset.upgrade);
});

function frame(now) {
  const dt = Math.min(0.05, (now - lastTime) / 1000);
  lastTime = now;
  updateGame(dt);
  updateUI();
  draw();
  requestAnimationFrame(frame);
}

loadLevel(currentLevelIndex);
requestAnimationFrame(frame);
