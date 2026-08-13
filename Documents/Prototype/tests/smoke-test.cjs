"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

function createClassList() {
  const names = new Set();
  return {
    add(...items) { items.forEach(item => names.add(item)); },
    remove(...items) { items.forEach(item => names.delete(item)); },
    toggle(item, force) {
      const shouldAdd = force === undefined ? !names.has(item) : force;
      if (shouldAdd) names.add(item); else names.delete(item);
      return shouldAdd;
    },
    contains(item) { return names.has(item); }
  };
}

function createElement(id = "") {
  const element = {
    id,
    children: [],
    className: "",
    classList: createClassList(),
    dataset: {},
    style: {},
    textContent: "",
    disabled: false,
    innerHTML: "",
    appendChild(child) { this.children.push(child); },
    addEventListener() {},
    setAttribute(name, value) { this[name] = value; },
    querySelectorAll(selector) {
      return selector === ".tower-card" ? this.children : [];
    }
  };
  return element;
}

const elementIds = [
  "gameCanvas", "waterValue", "healthBar", "healthFill", "waveValue", "enemyValue",
  "waveButton", "pauseButton", "restartButton", "towerShop", "placementHint",
  "battleBanner", "inspectorTitle", "inspectorDescription", "karmaReadout",
  "phaseLabel", "karmaText", "karmaFill", "effectGrid", "yangEffect",
  "yinEffect", "upgradePanel", "upgradeLimit", "upgradeOptions", "sellButton"
];

const elements = Object.fromEntries(elementIds.map(id => [id, createElement(id)]));
const deterministicMath = Object.create(Math);
deterministicMath.random = () => 0.5;
const drawingContext = new Proxy({}, {
  get(target, property) {
    if (property === "createLinearGradient") return () => ({ addColorStop() {} });
    if (!(property in target)) target[property] = () => {};
    return target[property];
  },
  set(target, property, value) { target[property] = value; return true; }
});

elements.gameCanvas.width = 960;
elements.gameCanvas.height = 560;
elements.gameCanvas.getContext = () => drawingContext;
elements.gameCanvas.getBoundingClientRect = () => ({ left: 0, top: 0, width: 960, height: 560 });

const context = vm.createContext({
  console,
  document: {
    getElementById(id) { return elements[id]; },
    createElement() { return createElement(); }
  },
  performance: { now: () => 0 },
  requestAnimationFrame() {},
  Math: deterministicMath,
  Set
});

const gamePath = path.join(__dirname, "..", "game.js");
const html = fs.readFileSync(path.join(__dirname, "..", "index.html"), "utf8");
assert.ok(html.includes('id="healthBar"'), "Giao diện phải có thanh máu Cóc.");
assert.ok(!html.includes('id="healthValue"'), "Không được hiện máu Cóc bằng số cũ.");
vm.runInContext(fs.readFileSync(gamePath, "utf8"), context, { filename: gamePath });

function run(code) {
  return vm.runInContext(code, context);
}

assert.equal(run("state.water"), 220, "Trò chơi phải bắt đầu với 220 Nước.");
assert.equal(run("state.selectedType"), "bear", "Gấu phải được chọn mặc định.");

assert.deepEqual(
  Array.from(run("[TOWER_TYPES.bear.karmaPerAttack, TOWER_TYPES.bee.karmaPerAttack, TOWER_TYPES.fox.karmaPerAttack, TOWER_TYPES.crab.karmaPerAttack, TOWER_TYPES.waterTower.karmaPerProduction]")),
  [10, 15, 2, 12.5, 15],
  "Mỗi trụ phải có tốc độ tích Nghiệp riêng cao hơn."
);

assert.ok(run("WAVES.every((wave, index) => index === 0 || (wave.count > WAVES[index - 1].count && wave.hp > WAVES[index - 1].hp && wave.speed > WAVES[index - 1].speed && wave.interval < WAVES[index - 1].interval))"), "Mỗi đợt phải đông hơn, khỏe hơn, nhanh hơn và ra quân dày hơn đợt trước.");
assert.ok(run("WAVES.every((wave, index) => index === 0 || wave.count * wave.hp * wave.speed > WAVES[index - 1].count * WAVES[index - 1].hp * WAVES[index - 1].speed)"), "Mức nguy hiểm tổng phải tăng rõ qua từng đợt.");
assert.equal(run("WAVES[2].type"), "mixed", "Địch bay phải xuất hiện từ đợt 3.");
assert.equal(run("WAVES[5].type"), "elite", "Đợt 6 phải có áp lực tinh anh.");
assert.equal(run("dangerPips(5)"), "◆◆◆◆◆◆", "Biểu tượng nguy hiểm phải tăng theo đợt.");
assert.deepEqual(Array.from(run("WAVES.map(wave => wave.reward)")), [5, 5, 6, 6, 7, 8], "Nước thưởng khi hạ địch phải thấp hơn trước.");
assert.equal(run("altarDamage({ kind: 'ground' })"), 2, "Quái thường lọt đền phải gây 2 máu.");
assert.equal(run("altarDamage({ kind: 'flying' })"), 2, "Quái bay lọt đền phải gây 2 máu.");
assert.equal(run("altarDamage({ kind: 'elite' })"), 4, "Tinh anh lọt đền phải gây 4 máu.");

assert.equal(elements.healthFill.style.width, "100%", "Máu Cóc phải hiện bằng thanh đầy khi bắt đầu.");
run("state.health = 10; updateUI()");
assert.equal(elements.healthFill.style.width, "25%", "Thanh máu Cóc phải phản ánh máu hiện tại.");
assert.equal(elements.healthBar.classList.contains("danger"), true, "Thanh máu thấp phải đổi sang cảnh báo.");

run("resetGame(); selectShopTower('waterTower'); placeTower(0); selectShopTower('bear'); placeTower(1); state.towers[0].phase = 'Yin'; state.towers[0].karma = 100; state.towers[0].productionTimer = 1; state.towers[1].phase = 'Yin'; state.towers[1].karma = 100; state.towers[1].cooldown = 0.5");
const waitingWater = run("state.water");
run("updateGame(10)");
assert.equal(run("state.water"), waitingWater, "Trụ Nước không được sinh Nước giữa các đợt.");
assert.equal(run("state.towers[0].karma"), 100, "Nghiệp không được xả giữa các đợt.");
assert.equal(run("state.towers[0].productionTimer"), 1, "Đồng hồ sản xuất phải đứng yên giữa các đợt.");
assert.equal(run("state.towers[1].karma"), 100, "Nghiệp của trụ chiến đấu phải đứng yên giữa các đợt.");
assert.equal(run("state.towers[1].cooldown"), 0.5, "Hồi đòn của trụ chiến đấu phải đứng yên giữa các đợt.");

run("resetGame(); placeTower(0); state.selectedTowerId = state.towers[0].id; state.water = 999");
const firstUpgradeCost = run("upgradeCost(state.towers[0])");
assert.equal(run("upgradeTower('damage')"), true, "Trụ chiến đấu phải nâng được sát thương bằng Nước.");
assert.equal(run("upgradeTower('speed')"), true, "Trụ chiến đấu phải nâng được tốc đánh bằng Nước.");
assert.equal(run("upgradeTower('range')"), true, "Trụ chiến đấu phải nâng được tầm đánh bằng Nước.");
assert.equal(run("state.towers[0].upgradeCount"), 3, "Mỗi trụ chỉ có ba lượt nâng cấp.");
assert.equal(run("upgradeTower('damage')"), false, "Không được nâng quá ba lượt.");
assert.equal(run("towerDamage(state.towers[0])"), 28.6, "Một cấp sát thương phải tăng 30%.");
assert.ok(run("towerFireRate(state.towers[0])") > run("TOWER_TYPES.bear.fireRate"), "Nâng tốc đánh phải tăng tốc độ bắn.");
assert.ok(run("towerRange(state.towers[0])") > run("TOWER_TYPES.bear.range"), "Nâng tầm phải tăng vùng tác động.");
assert.equal(firstUpgradeCost, 32, "Giá nâng đầu của Gấu phải phù hợp kinh tế tạm.");

run("resetGame(); selectShopTower('crab'); state.water = 999; placeTower(0); state.selectedTowerId = state.towers[0].id");
assert.deepEqual(Array.from(run("upgradeChoices(state.towers[0]).map(choice => choice.key)")), ["range"], "Cua chỉ được tăng tầm aura.");
assert.equal(run("upgradeTower('range')"), true, "Cua phải nâng được tầm buff/debuff.");
assert.equal(run("Math.round(towerRange(state.towers[0]) * 100) / 100"), 166.75, "Tầm aura Cua phải tăng 15% mỗi cấp.");
run("state.waveActive = true");
assert.equal(run("upgradeTower('range')"), false, "Không được nâng cấp khi đợt đang diễn ra.");

run("resetGame(); selectShopTower('bee'); placeTower(0); state.enemies = [{ hp: 100, maxHp: 100, physicalArmor: 0.5, armorBuff: 0, magicResist: 0, shield: 0, dead: false, reward: 0, x: state.towers[0].x + 10, y: state.towers[0].y, speedBonus: 0 }]; attack(state.towers[0], state.enemies[0])");
assert.equal(run("state.enemies[0].hp"), 88, "Phép của Ong phải xuyên qua giáp vật lý.");
assert.equal(run("state.enemies[0].physicalArmor"), 0.5, "Ong bắn phép không được làm mất giáp.");
assert.equal(run("state.effects.some(effect => effect.kind === 'damage' && effect.text === '-12')"), true, "Đòn bắn phải hiện sát thương thực tế.");
run("applyDamage(state.enemies[0], 10, 'physical')");
assert.equal(run("state.enemies[0].hp"), 83, "Giáp vật lý chỉ giảm sát thương vật lý.");
run("state.enemies = [{ hp: 100, maxHp: 100, physicalArmor: 0, armorBuff: 0, magicResist: 0.5, shield: 0, dead: false, reward: 0, x: 0, y: 0 }]; applyDamage(state.enemies[0], 10, 'physical')");
assert.equal(run("state.enemies[0].hp"), 90, "Vật lý phải xuyên qua kháng phép.");
run("applyDamage(state.enemies[0], 10, 'magic')");
assert.equal(run("state.enemies[0].hp"), 85, "Kháng phép chỉ giảm sát thương phép.");

run("resetGame(); state.water = 999; Object.keys(TOWER_TYPES).forEach((type, index) => { selectShopTower(type); placeTower(index); })");
assert.deepEqual(
  Array.from(run("state.towers.map(tower => tower.type)")),
  ["bear", "bee", "fox", "crab", "waterTower"],
  "Phải đặt được đủ năm loại trụ."
);

run("resetGame()");
run("placeTower(0)");
assert.equal(run("state.towers.length"), 1, "Đặt trụ phải thêm trụ vào đội hình.");
assert.equal(run("state.water"), 150, "Đặt Gấu phải tốn 70 Nước.");

run("startWave()");
assert.equal(run("state.wave"), 1, "Mở đợt phải tăng số đợt.");
assert.equal(run("state.spawnQueue"), 8, "Đợt 1 phải có tám địch.");

run("spawnEnemy(); state.spawnQueue -= 1");
assert.equal(run("state.enemies.length"), 1, "Sinh địch phải tạo một địch.");
run("state.enemies[0].x = state.towers[0].x + 20; state.enemies[0].y = state.towers[0].y; attack(state.towers[0], state.enemies[0])");
assert.ok(run("state.enemies[0].hp") < run("state.enemies[0].maxHp"), "Gấu phải gây sát thương lên địch mặt đất.");
assert.ok(run("state.enemies[0].bearSlowTimer") > 0, "Gấu Dương phải làm chậm mục tiêu.");
run("resetEnemyModifiers()");
assert.equal(run("state.enemies[0].slow"), 0.65, "Làm chậm của Gấu phải còn hiệu lực giữa các khung hình.");
assert.equal(run("enemyStatusBadges(state.enemies[0])[0].icon"), "🐌", "Địch bị chậm phải có biểu tượng phản hồi.");
assert.deepEqual(
  Array.from(run("enemyStatusBadges({ slow: 0.65, bearSlowTimer: 1, speedBonus: 0.45, poisonTimer: 2, armorBuff: 0.35, magicResist: 0.25, shield: 10 }).map(badge => badge.icon)")),
  ["🐌", "⚡", "☠", "⬡", "✦", "🛡"],
  "Đủ sáu hiệu ứng trên địch phải có biểu tượng riêng."
);
run("state.selectedTowerId = state.towers[0].id");
assert.equal(run("canTowerAffectEnemy(state.towers[0], state.enemies[0])"), true, "Địch trong tầm phải được đánh dấu khi chọn trụ.");
run("drawTowerRanges(); drawEnemies(); drawSelectedTargets()");
run("clearTowerSelection()");
assert.equal(run("state.selectedTowerId"), null, "Chạm đất phải bỏ chọn trụ hiện tại.");
assert.equal(elements.upgradePanel.classList.contains("hidden"), true, "Bỏ chọn phải ẩn bảng nâng cấp.");

run("gainKarma(state.towers[0], TOWER_TYPES.bear.cycle)");
assert.equal(run("state.towers[0].phase"), "Yin", "Đầy Nghiệp phải kích hoạt Âm.");
run("updateTowers(10)");
assert.equal(run("state.towers[0].phase"), "Yang", "Gấu xả hết Nghiệp phải trở về Dương.");

run("resetGame(); selectShopTower('crab'); placeTower(0); selectShopTower('bear'); placeTower(1)");
run("state.towers[0].phase = 'Yin'; state.towers[0].karma = 300; gainKarma(state.towers[1], 10)");
assert.equal(run("state.towers[1].karma"), 17.5, "Cua Âm phải tăng 75% Nghiệp nhận vào của trụ gần.");

run("resetGame(); selectShopTower('waterTower'); placeTower(0)");
const waterBefore = run("state.water");
run("state.towers[0].productionTimer = 3; updateWaterTower(state.towers[0], 0)");
assert.equal(run("state.water"), waterBefore + 5, "Trụ Nước Dương phải tạo 5 Nước.");
assert.equal(run("state.towers[0].karma"), 15, "Trụ Nước Dương phải nhận 15 Nghiệp mỗi lần tạo.");

run("state.water = 999; state.selectedTowerId = state.towers[0].id; upgradeTower('production')");
const upgradedWaterBefore = run("state.water");
run("state.towers[0].productionTimer = 3; updateWaterTower(state.towers[0], 0)");
assert.equal(run("state.water"), upgradedWaterBefore + 7, "Nâng Trụ Nước phải tăng lượng Nước mỗi lần tạo.");

const healthBefore = run("state.health");
run("state.towers[0].phase = 'Yin'; state.towers[0].karma = 100; for (let i = 0; i < 3; i += 1) { state.towers[0].productionTimer = 1.5; updateWaterTower(state.towers[0], 0); }");
assert.equal(run("state.health"), healthBefore - 1, "Ba lần tạo của Trụ Nước Âm phải làm đền mất 1 máu.");

run("state.wave = WAVES.length; state.waveActive = true; state.spawnQueue = 0; state.enemies = []; finishWaveIfReady()");
assert.equal(run("state.victory"), true, "Qua đợt cuối phải chiến thắng.");

run("resetGame(); selectShopTower('bear'); placeTower(0); selectShopTower('bee'); placeTower(3); selectShopTower('waterTower'); placeTower(4)");
const additions = [
  [["crab", 1]],
  [["fox", 5]],
  [["bear", 6], ["bee", 8]],
  [["crab", 7], ["bear", 2]],
  []
];

for (let wave = 0; wave < 6; wave += 1) {
  run("startWave()");
  let steps = 0;
  while (run("state.waveActive") && !run("state.gameOver") && steps < 12000) {
    run("updateGame(0.05)");
    steps += 1;
  }
  console.log(`Đợt ${wave + 1}: đền=${run("state.health")}, nước=${run("Math.floor(state.water)")}, trụ=${run("state.towers.length")}, bước=${steps}`);
  assert.equal(run("state.gameOver"), false, `Đội hình mẫu phải sống qua đợt ${wave + 1}.`);
  assert.ok(steps < 12000, `Đợt ${wave + 1} phải kết thúc trong giới hạn mô phỏng.`);
  if (wave === 0) {
    run("state.selectedTowerId = state.towers.find(tower => tower.type === 'waterTower').id");
    assert.equal(run("upgradeTower('production')"), true, "Đội hình mẫu phải dùng nâng cấp Trụ Nước giữa các đợt.");
  }
  if (wave === 3) {
    run("state.selectedTowerId = state.towers.find(tower => tower.type === 'waterTower').id; dismissSelectedTower(); selectShopTower('fox'); placeTower(4)");
  }
  if (wave === 4) {
    run("state.selectedTowerId = state.towers.find(tower => tower.spotIndex === 5).id");
    assert.equal(run("upgradeTower('damage')"), true, "Cáo phải được nâng sát thương trước đợt tinh anh.");
    run("state.selectedTowerId = state.towers.find(tower => tower.spotIndex === 3).id");
    assert.equal(run("upgradeTower('damage')"), true, "Ong phải được nâng sát thương trước đợt tinh anh.");
    run("state.selectedTowerId = state.towers.find(tower => tower.spotIndex === 6).id");
    assert.equal(run("upgradeTower('damage')"), true, "Gấu phải được nâng sát thương trước đợt tinh anh.");
  }
  if (wave < additions.length) {
    for (const [type, spot] of additions[wave]) {
      run(`selectShopTower('${type}'); placeTower(${spot})`);
    }
  }
}

assert.equal(run("state.victory"), true, "Đội hình mẫu hợp lệ phải qua đủ sáu đợt.");

console.log("Kiểm tra nhanh đã đạt.");
