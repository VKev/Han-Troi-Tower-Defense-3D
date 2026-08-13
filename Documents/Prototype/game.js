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
  sellButton: document.getElementById("sellButton")
};

const MAX_HEALTH = 40;
const MAX_UPGRADES = 3;

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

const WAVES = [
  { count: 8, hp: 50, speed: 48, reward: 5, interval: 0.82, type: "ground" },
  { count: 10, hp: 65, speed: 52, reward: 5, interval: 0.74, type: "ground" },
  { count: 12, hp: 82, speed: 56, reward: 6, interval: 0.68, type: "mixed" },
  { count: 14, hp: 103, speed: 60, reward: 6, interval: 0.62, type: "mixed" },
  { count: 16, hp: 127, speed: 64, reward: 7, interval: 0.57, type: "mixed" },
  { count: 17, hp: 140, speed: 66, reward: 8, interval: 0.52, type: "elite" }
];

let state;
let lastTime = performance.now();
let bannerTimer = 0;

function makeState() {
  return {
    water: 220,
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
    gameOver: false,
    victory: false,
    nextEnemyId: 1,
    nextTowerId: 1,
    elapsed: 0
  };
}

function createShop() {
  ui.shop.innerHTML = "";
  Object.entries(TOWER_TYPES).forEach(([key, type]) => {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "tower-card";
    button.dataset.tower = key;
    button.innerHTML = `<span class="animal">${type.icon}</span><span><strong>${type.name}</strong><small>${type.role}</small></span><span class="price">💧 ${type.cost}</span>`;
    button.addEventListener("click", () => selectShopTower(key));
    ui.shop.appendChild(button);
  });
}

function resetGame() {
  state = makeState();
  lastTime = performance.now();
  bannerTimer = 0;
  selectShopTower("bear");
  updateUI();
  showBanner("Linh thú đang chờ lệnh.");
}

function selectShopTower(typeKey) {
  state.selectedType = typeKey;
  state.selectedTowerId = null;
  const type = TOWER_TYPES[typeKey];
  ui.hint.textContent = `${type.icon} ${type.name} · Chạm vòng trống · 💧 ${type.cost}`;
  inspectType(type);
  updateShop();
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

function placeTower(spotIndex) {
  const typeKey = state.selectedType;
  if (!typeKey || state.gameOver || state.victory) return;
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
  state.towers.push({
    id: state.nextTowerId++, type: typeKey, spotIndex, x: spot.x, y: spot.y,
    karma: 0, phase: "Yang", cooldown: Math.random() * 0.25, productionTimer: 0,
    yinLeakCounter: 0, shieldApplied: new Set(), upgradeCount: 0, upgradeSpent: 0,
    upgrades: { damage: 0, speed: 0, range: 0, production: 0 }
  });
  showBanner(`${type.icon} ${type.name} đã vào trận.`);
  updateUI();
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
  if (state.waveActive || state.gameOver || state.victory || state.wave >= WAVES.length) return;
  const config = WAVES[state.wave];
  state.wave += 1;
  state.waveActive = true;
  state.spawnQueue = config.count;
  state.spawnTimer = 0;
  showBanner(`⚔ Đợt ${state.wave} ${dangerPips(state.wave - 1)}`, "danger");
  updateUI();
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
  if (state.paused || state.gameOver || state.victory) return;
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
      for (const enemy of enemiesInRange(tower, range, false)) enemy.speedBonus = Math.max(enemy.speedBonus, 0.45);
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
    endGame(true);
  } else {
    showBanner(`Qua đợt ${state.wave} · +${bonus} 💧`, "success");
  }
  updateUI();
}

function endGame(victory) {
  state.waveActive = false;
  state.victory = victory;
  state.gameOver = !victory;
  showBanner(victory ? "Mưa đã về. Trời chịu thua!" : "Đền mưa thất thủ!", victory ? "success" : "danger", 8);
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

  ui.waveButton.disabled = state.waveActive || state.gameOver || state.victory;
  const shownWave = state.waveActive ? state.wave : Math.min(state.wave + 1, WAVES.length);
  ui.waveButton.textContent = state.victory ? "🌧 Đã gọi mưa" : state.gameOver ? "Đền thất thủ" : state.wave >= WAVES.length ? "Đã qua 6 đợt" : `⚔ Đợt ${shownWave} ${dangerPips(shownWave - 1)}`;
  ui.pauseButton.textContent = state.paused ? "▶ Tiếp" : "⏸ Dừng";
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
  if (state.gameOver) drawOverlay("Đền mưa thất thủ", "Chạm “Lại” để chơi tiếp.");
  if (state.victory) drawOverlay("Trời chịu thua", "Mưa đã về.");
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
  if (state.gameOver || state.victory) return;
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
  if (state.gameOver || state.victory) return;
  state.paused = !state.paused;
  updateUI();
});
ui.restartButton.addEventListener("click", resetGame);
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

createShop();
resetGame();
requestAnimationFrame(frame);
