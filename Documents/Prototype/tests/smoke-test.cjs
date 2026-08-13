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
    appendChild(child) { this.children.push(child); },
    addEventListener() {},
    setAttribute(name, value) { this[name] = value; },
    querySelectorAll(selector) { return selector === ".tower-card" ? this.children : []; }
  };
  let html = "";
  Object.defineProperty(element, "innerHTML", {
    get() { return html; },
    set(value) { html = value; if (value === "") element.children = []; }
  });
  return element;
}

const elementIds = [
  "gameCanvas", "waterValue", "healthBar", "healthFill", "waveValue", "enemyValue",
  "waveButton", "levelButton", "tutorialButton", "pauseButton", "restartButton",
  "towerShop", "placementHint", "battleBanner", "inspectorTitle", "inspectorDescription",
  "karmaReadout", "phaseLabel", "karmaText", "karmaFill", "effectGrid", "yangEffect",
  "yinEffect", "upgradePanel", "upgradeLimit", "upgradeOptions", "sellButton", "inspector",
  "inspectorClose", "tutorialOverlay", "tutorialProgress", "tutorialIcon", "tutorialKicker",
  "tutorialTitle", "tutorialBody", "tutorialContinue", "levelOverlay", "levelGrid", "levelClose",
  "levelCompleteOverlay", "levelCompleteTitle", "levelCompleteBody", "replayLevelButton",
  "nextLevelButton"
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

const storage = new Map();
const localStorage = {
  getItem(key) { return storage.has(key) ? storage.get(key) : null; },
  setItem(key, value) { storage.set(key, String(value)); }
};

const documentMock = {
  getElementById(id) { return elements[id]; },
  createElement() { return createElement(); },
  querySelectorAll(selector) {
    if (selector !== ".tutorial-focus") return [];
    return Object.values(elements).concat(elements.towerShop.children)
      .filter(element => element.classList.contains("tutorial-focus"));
  }
};

const context = vm.createContext({
  console,
  document: documentMock,
  localStorage,
  performance: { now: () => 0 },
  requestAnimationFrame() {},
  Math: deterministicMath,
  Set
});

const prototypeRoot = path.join(__dirname, "..");
const html = fs.readFileSync(path.join(prototypeRoot, "index.html"), "utf8");
const css = fs.readFileSync(path.join(prototypeRoot, "styles.css"), "utf8");
const gamePath = path.join(prototypeRoot, "game.js");

assert.match(html, /class="game-shell"/, "Giao diện phải dùng một chiến trường fullscreen.");
assert.ok(html.includes('id="healthBar"'), "Giao diện phải có thanh máu Cóc.");
assert.ok(!html.includes('id="healthValue"'), "Không được hiện máu Cóc bằng số cũ.");
assert.ok(html.includes('id="tutorialOverlay"'), "Giao diện phải có tutorial theo từng bước.");
assert.ok(html.includes('id="levelOverlay"'), "Giao diện phải có chọn màn trong game.");
assert.match(css, /\.game-shell\s*\{[^}]*position:\s*fixed;[^}]*inset:\s*0;/s, "Game phải phủ toàn màn hình.");

vm.runInContext(fs.readFileSync(gamePath, "utf8"), context, { filename: gamePath });

function run(code) {
  return vm.runInContext(code, context);
}

assert.equal(run("currentLevelIndex"), 0, "Người chơi mới phải bắt đầu ở màn 1.");
assert.equal(run("WAVES.length"), 3, "Màn 1 phải có ba wave.");
assert.equal(run("state.water"), 250, "Màn 1 phải bắt đầu với 250 Nước.");
assert.equal(run("state.pausedByTutorial"), true, "Tutorial phải dừng game khi hiện lời hướng dẫn.");
assert.equal(elements.towerShop.children.filter(button => !button.disabled).length, 1, "Ban đầu chỉ Gấu được mở khóa.");

assert.deepEqual(
  Array.from(run("LEVELS.map(level => level.waves)")),
  [3, 6, 6, 6, 6],
  "Campaign phải có cấu trúc wave 3, 6, 6, 6, 6."
);
assert.deepEqual(
  Array.from(run("LEVELS.map(level => level.unlock)")),
  ["bear", "bee", "fox", "crab", "waterTower"],
  "Thứ tự mở khóa phải là Gấu, Ong, Cáo, Cua, Trụ Nước."
);
assert.ok(run("buildLevelWaves(4)[5].hp > buildLevelWaves(1)[5].hp"), "Màn sau phải khó hơn màn trước.");
assert.ok(run("buildLevelWaves(4).every((wave, index, waves) => index === 0 || wave.count > waves[index - 1].count && wave.hp > waves[index - 1].hp && wave.speed > waves[index - 1].speed && wave.interval < waves[index - 1].interval)"), "Độ khó phải tăng rõ qua từng wave.");

// Complete the guided Bear tutorial through real tutorial events.
run("continueTutorial(); continueTutorial()");
assert.equal(run("state.tutorial.awaiting"), "select:bear");
run("selectShopTower('bear')");
assert.equal(run("state.tutorial.stepIndex"), 2);
run("continueTutorial(); placeTower(0)");
assert.equal(run("state.tutorial.stepIndex"), 3);
assert.equal(run("state.selectedTowerId"), run("state.towers[0].id"), "Trụ vừa đặt phải tự được chọn để nâng cấp.");
run("continueTutorial(); upgradeTower('damage')");
assert.equal(run("state.tutorial.stepIndex"), 4);
run("continueTutorial(); continueTutorial(); startWave()");
assert.equal(run("state.tutorial.stepIndex"), 6);
assert.equal(run("state.pausedByTutorial"), true, "Mốc tutorial phải dừng wave đang chạy.");
run("continueTutorial(); gainKarma(state.towers[0], TOWER_TYPES.bear.karmaPerAttack)");
assert.equal(run("state.towers[0].phase"), "Yin", "Tutorial phải minh họa được Gấu chuyển sang Âm.");
run("continueTutorial(); continueTutorial()");
assert.equal(run("state.tutorial.awaiting"), "level:waves_complete");
run("state.wave = WAVES.length; state.waveActive = true; state.spawnQueue = 0; state.enemies = []; finishWaveIfReady()");
assert.equal(run("state.tutorial.stepIndex"), 9, "Tutorial phải trở lại sau wave cuối.");
run("continueTutorial(); dismissSelectedTower()");
assert.equal(run("state.tutorial.stepIndex"), 10, "Bán Gấu phải hoàn tất mốc bán trụ.");
run("continueTutorial()");
assert.equal(run("state.levelComplete"), true, "Hoàn tất tutorial và ba wave phải qua màn 1.");
assert.equal(run("campaign.maxUnlockedLevel"), 2, "Qua màn 1 phải mở màn 2.");
assert.equal(JSON.parse(storage.get("toadTowerDefenseCampaignV1")).maxUnlockedLevel, 2, "Tiến độ mở khóa phải được lưu vào localStorage.");

// Level 2 must teach the Bee Yang + Bear Yin interaction.
run("loadLevel(1, true); continueTutorial(); continueTutorial(); selectShopTower('bee'); continueTutorial(); placeTower(0); continueTutorial(); selectShopTower('bear'); continueTutorial(); placeTower(1); continueTutorial(); continueTutorial(); startWave(); continueTutorial()");
assert.equal(run("state.towers.find(tower => tower.type === 'bear').phase"), "Yin");
assert.equal(run("state.towers.find(tower => tower.type === 'bee').phase"), "Yang");
run("spawnEnemy(); state.spawnQueue -= 1; resetEnemyModifiers(); applyTowerAuras()");
assert.equal(run("state.enemies[0].speedBonus"), 0.45, "Gấu Âm phải tăng tốc mục tiêu minh họa.");
const comboHp = run("state.enemies[0].hp");
run("attack(state.towers.find(tower => tower.type === 'bee'), state.enemies[0])");
assert.ok(run("state.enemies[0].hp") < comboHp - run("TOWER_TYPES.bee.damage"), "Ong Dương phải tăng damage khi bắn địch được Gấu Âm tăng tốc.");
assert.equal(run("state.tutorial.stepIndex"), 8, "Combo thật phải kích hoạt mốc tutorial cuối.");
run("continueTutorial()");
assert.equal(run("campaign.completedTutorials[2]"), true, "Tutorial Ong phải được lưu là đã hoàn tất.");

// Unlock all levels for isolated mechanics checks.
run("campaign.maxUnlockedLevel = 5; campaign.currentLevel = 5; campaign.completedTutorials = {1:true,2:true,3:true,4:true,5:true}; saveCampaign(); loadLevel(4)");
assert.equal(run("WAVES.length"), 6, "Màn 5 phải có sáu wave.");
assert.equal(elements.towerShop.children.filter(button => !button.disabled).length, 5, "Màn 5 phải mở đủ năm trụ.");
run("loadLevel(0)");
assert.equal(elements.towerShop.children.filter(button => !button.disabled).length, 5, "Trụ đã mở phải còn dùng được khi chơi lại màn cũ.");
run("loadLevel(4)");

assert.deepEqual(
  Array.from(run("[TOWER_TYPES.bear.karmaPerAttack, TOWER_TYPES.bee.karmaPerAttack, TOWER_TYPES.fox.karmaPerAttack, TOWER_TYPES.crab.karmaPerAttack, TOWER_TYPES.waterTower.karmaPerProduction]")),
  [10, 15, 2, 12.5, 15],
  "Mỗi trụ phải có tốc độ tích Nghiệp riêng cao hơn."
);

run("selectShopTower('waterTower'); placeTower(0); selectShopTower('bear'); placeTower(1); state.towers[0].phase = 'Yin'; state.towers[0].karma = 100; state.towers[0].productionTimer = 1; state.towers[1].phase = 'Yin'; state.towers[1].karma = 100; state.towers[1].cooldown = 0.5");
const waitingWater = run("state.water");
run("updateGame(10)");
assert.equal(run("state.water"), waitingWater, "Trụ Nước không được sinh Nước giữa các wave.");
assert.equal(run("state.towers[0].karma"), 100, "Nghiệp không được xả giữa các wave.");
assert.equal(run("state.towers[0].productionTimer"), 1, "Đồng hồ sản xuất phải đứng yên giữa các wave.");
assert.equal(run("state.towers[1].cooldown"), 0.5, "Hồi đòn phải đứng yên giữa các wave.");

run("loadLevel(4); placeTower(0); state.water = 999");
assert.equal(run("upgradeTower('damage')"), true, "Gấu phải nâng được damage bằng Nước.");
assert.equal(run("upgradeTower('speed')"), true, "Gấu phải nâng được tốc đánh bằng Nước.");
assert.equal(run("upgradeTower('range')"), true, "Gấu phải nâng được tầm đánh bằng Nước.");
assert.equal(run("upgradeTower('damage')"), false, "Không được nâng quá ba lần.");

run("loadLevel(4); selectShopTower('crab'); placeTower(0); state.water = 999");
assert.deepEqual(Array.from(run("upgradeChoices(state.towers[0]).map(choice => choice.key)")), ["range"], "Cua chỉ được tăng tầm aura.");
assert.equal(run("upgradeTower('range')"), true);
assert.equal(run("Math.round(towerRange(state.towers[0]) * 100) / 100"), 166.75);

run("loadLevel(4); selectShopTower('bee'); placeTower(0); state.enemies = [{ hp: 100, maxHp: 100, physicalArmor: 0.5, armorBuff: 0, magicResist: 0, shield: 0, dead: false, reward: 0, x: state.towers[0].x + 10, y: state.towers[0].y, speedBonus: 0 }]; attack(state.towers[0], state.enemies[0])");
assert.equal(run("state.enemies[0].hp"), 88, "Phép của Ong phải xuyên giáp vật lý.");
assert.equal(run("state.enemies[0].physicalArmor"), 0.5, "Phép không được xóa giáp vật lý.");
assert.equal(run("state.effects.some(effect => effect.kind === 'damage' && effect.text === '-12')"), true, "Đòn bắn phải hiện damage thực tế.");
run("state.enemies = [{ hp: 100, maxHp: 100, physicalArmor: 0, armorBuff: 0, magicResist: 0.5, shield: 0, dead: false, reward: 0, x: 0, y: 0 }]; applyDamage(state.enemies[0], 10, 'physical')");
assert.equal(run("state.enemies[0].hp"), 90, "Vật lý phải xuyên kháng phép.");
run("applyDamage(state.enemies[0], 10, 'magic')");
assert.equal(run("state.enemies[0].hp"), 85, "Kháng phép chỉ giảm sát thương phép.");

assert.equal(elements.healthFill.style.width, "100%", "Máu Cóc phải bắt đầu bằng thanh đầy.");
run("state.health = 10; updateUI()");
assert.equal(elements.healthFill.style.width, "25%", "Thanh máu phải phản ánh máu hiện tại.");
assert.equal(elements.healthBar.classList.contains("danger"), true, "Thanh máu thấp phải cảnh báo rõ.");
assert.equal(run("altarDamage({ kind: 'ground' })"), 2);
assert.equal(run("altarDamage({ kind: 'elite' })"), 4);

console.log("Kiểm tra nhanh đã đạt: fullscreen, tutorial, campaign, lưu tiến độ và combat.");
