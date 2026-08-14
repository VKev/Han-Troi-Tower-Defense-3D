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
    getBoundingClientRect() { return { left: 100, top: 100, width: 66, height: 58 }; },
    querySelectorAll(selector) {
      if (selector === ".tower-card") return this.children;
      if (selector === "[data-status]") return this.children.filter(child => child.dataset.status);
      return [];
    }
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
  "inspectorClose", "statusLegend", "tutorialPointer", "tutorialPointerLabel", "tutorialOverlay", "tutorialProgress", "tutorialIcon", "tutorialKicker",
  "tutorialTitle", "tutorialBody", "tutorialLevelButton", "tutorialContinue", "levelOverlay", "levelGrid", "levelClose",
  "levelCompleteOverlay", "levelCompleteTitle", "levelCompleteBody", "replayLevelButton",
  "nextLevelButton", "enemyIntroOverlay", "enemyIntroIcon", "enemyIntroTitle", "enemyIntroBody", "enemyIntroContinue"
];

const elements = Object.fromEntries(elementIds.map(id => [id, createElement(id)]));
for (const status of ["slow", "haste", "poison", "armor", "resist", "shield", "invisible"]) {
  const icon = createElement();
  icon.dataset.status = status;
  elements.statusLegend.appendChild(icon);
}
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

const storage = new Map([["toadTowerDefenseCampaignV1", JSON.stringify({ maxUnlockedLevel: 5, currentLevel: 5, completedTutorials: { 1: true } })]]);
let storageWrites = 0;
const localStorage = {
  getItem(key) { return storage.has(key) ? storage.get(key) : null; },
  setItem(key, value) { storageWrites += 1; storage.set(key, String(value)); }
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
const gameSource = fs.readFileSync(gamePath, "utf8");

assert.match(html, /class="game-shell"/, "Giao diện phải dùng một chiến trường fullscreen.");
assert.ok(html.includes('id="healthBar"'), "Giao diện phải có thanh máu Cóc.");
assert.ok(!html.includes('id="healthValue"'), "Không được hiện máu Cóc bằng số cũ.");
assert.ok(html.includes('id="tutorialOverlay"'), "Giao diện phải có tutorial theo từng bước.");
assert.ok(html.includes('id="tutorialLevelButton"'), "Tutorial phải có nút chọn màn để bỏ qua.");
assert.ok(html.includes('id="tutorialPointer"'), "Giao diện phải có chỉ dẫn click động.");
assert.ok(html.includes('id="enemyIntroOverlay"'), "Giao diện phải có phần giới thiệu kẻ địch mới.");
assert.ok(html.includes('data-status="invisible"'), "Dải hiệu ứng phải có icon tàng hình.");
assert.ok(html.includes('id="levelOverlay"'), "Giao diện phải có chọn màn trong game.");
assert.match(css, /\.game-shell\s*\{[^}]*position:\s*fixed;[^}]*inset:\s*0;/s, "Game phải phủ toàn màn hình.");
assert.match(css, /h2\s*\{[^}]*font-family:\s*"Segoe UI"/s, "Tiêu đề phải dùng font hỗ trợ đầy đủ dấu tiếng Việt.");
assert.doesNotMatch(css, /h2\s*\{[^}]*Georgia/s, "Tiêu đề tiếng Việt không được dùng Georgia.");
assert.match(css, /@keyframes tutorial-tap/, "Bàn tay chỉ dẫn phải có animation.");
assert.match(css, /@keyframes tutorial-ring/, "Mục tiêu click phải có vòng sáng động.");
assert.match(css, /\.status-icon\.active/, "Icon hiệu ứng đang xuất hiện phải sáng lên.");
assert.match(css, /\.status-icon:hover::after/, "Hover icon phải hiện giải thích.");
assert.ok(!gameSource.includes("tutorialGlobalAura"), "Tutorial không được dùng aura Gấu vô hạn.");

vm.runInContext(gameSource, context, { filename: gamePath });

function run(code) {
  return vm.runInContext(code, context);
}

assert.equal(run("currentLevelIndex"), 0, "Người chơi mới phải bắt đầu ở màn 1.");
assert.equal(run("WAVES.length"), 3, "Màn 1 phải có ba wave.");
assert.equal(run("state.water"), 250, "Màn 1 phải bắt đầu với 250 Nước.");
assert.equal(run("state.pausedByTutorial"), true, "Tutorial phải dừng game khi hiện lời hướng dẫn.");
assert.equal(elements.towerShop.children.filter(button => !button.disabled).length, 1, "Ban đầu chỉ Gấu được mở khóa.");
assert.equal(storageWrites, 0, "Game không được ghi tiến độ vào localStorage.");
run("createLevelMenu()");
assert.equal(elements.levelGrid.children.filter(button => !button.disabled).length, 5, "Cả năm màn phải luôn chọn được.");
run("openLevelMenu(true)");
assert.equal(elements.tutorialOverlay.classList.contains("hidden"), true, "Chọn màn từ tutorial phải ẩn tutorial hiện tại.");
assert.equal(elements.levelOverlay.classList.contains("hidden"), false, "Nút chọn màn phải mở menu màn.");
run("selectLevel(4)");
assert.equal(run("currentLevelIndex"), 4, "Người chơi phải chọn thẳng được màn 5.");
assert.equal(run("state.tutorial"), null, "Chọn màn từ tutorial phải bỏ qua tutorial của màn được chọn.");
assert.equal(elements.towerShop.children.filter(button => !button.disabled).length, 5, "Chọn màn 5 phải mở đủ năm tower.");
run("campaign = { maxUnlockedLevel: 1, currentLevel: 1, completedTutorials: {}, introducedEnemies: {} }; loadLevel(0, true)");
assert.equal(run("state.pausedByTutorial"), true, "Luồng mặc định vẫn phải bắt đầu tutorial Gấu.");

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
assert.deepEqual(
  Array.from(run("LEVELS.map(level => level.water)")),
  [250, 270, 290, 310, 330],
  "Nước đầu màn chỉ tăng nhẹ để màn sau vẫn khó hơn rõ rệt."
);
assert.ok(run("LEVELS.every((level, index) => index === 0 || buildLevelWaves(index)[0].count > buildLevelWaves(index - 1)[0].count && buildLevelWaves(index)[0].hp > buildLevelWaves(index - 1)[0].hp && buildLevelWaves(index)[0].speed > buildLevelWaves(index - 1)[0].speed && buildLevelWaves(index)[0].interval < buildLevelWaves(index - 1)[0].interval)"), "Mỗi màn sau phải đông hơn, trâu hơn, nhanh hơn và ra quân dày hơn màn trước.");
assert.ok(run("LEVELS.every((level, index) => index === 0 || (() => { const current = buildLevelWaves(index)[0]; const previous = buildLevelWaves(index - 1)[0]; return current.count * current.hp * current.speed / current.interval > previous.count * previous.hp * previous.speed / previous.interval * 1.2; })())"), "Mức đe dọa đầu màn phải tăng ít nhất 20% qua mỗi màn.");
assert.ok(run("buildLevelWaves(4).every((wave, index, waves) => index === 0 || wave.count > waves[index - 1].count && wave.hp > waves[index - 1].hp && wave.speed > waves[index - 1].speed && wave.interval < waves[index - 1].interval)"), "Độ khó phải tăng rõ qua từng wave.");
assert.equal(run("ENEMY_HEALTH_SCALE"), 0.6, "Máu của mọi kẻ địch phải còn 60% so với cân bằng trước.");
assert.equal(run("buildLevelWaves(0)[0].hp"), run("Math.round(BASE_WAVES[0].hp * LEVELS[0].difficulty * 0.6)"), "Wave thường phải áp dụng đúng hệ số máu 60%.");
run("createLevelMenu()");
assert.ok(elements.levelGrid.children[4].innerHTML.includes("◆◆◆◆◆"), "Menu màn phải hiển thị độ khó tăng dần bằng biểu tượng.");

// Complete the guided Bear tutorial through real tutorial events.
assert.equal(run("TUTORIALS[0][4].title"), "Wave 1 · Dương", "Tutorial phải giới thiệu Dương trong wave 1.");
assert.equal(run("TUTORIALS[0][8].title"), "Wave 2 · Âm", "Tutorial chỉ được giới thiệu Âm từ wave 2.");
run("continueTutorial(); continueTutorial()");
assert.equal(run("state.tutorial.awaiting"), "select:bear");
assert.equal(elements.tutorialPointerLabel.textContent, "Chọn Gấu", "Chỉ dẫn phải nói rõ nơi cần click.");
assert.equal(elements.tutorialPointer.classList.contains("hidden"), false, "Chỉ dẫn click phải hiện trong bước hành động.");
assert.equal(elements.towerShop.children[0].classList.contains("tutorial-focus"), true, "Nút Gấu phải được highlight.");
run("selectShopTower('bear')");
assert.equal(run("state.tutorial.stepIndex"), 2);
run("continueTutorial()");
assert.equal(elements.tutorialPointerLabel.textContent, "Đặt Gấu ở đây", "Ô xây được gợi ý phải có nhãn riêng.");
assert.equal(Math.round(parseFloat(elements.tutorialPointer.style.left)), 505, "Chỉ dẫn phải trỏ vào ô xây Gấu mới.");
assert.equal(Math.round(parseFloat(elements.tutorialPointer.style.top)), 300, "Chỉ dẫn phải bám đúng tọa độ ô xây mới.");
assert.ok(run("Math.abs(PATH[5].x - BUILD_SPOTS[4].x) < TOWER_TYPES.bear.range && BUILD_SPOTS[4].y >= PATH[5].y && BUILD_SPOTS[4].y <= PATH[6].y"), "Ô Gấu tutorial phải nằm trong tầm đánh cơ bản của lane dọc.");
run("placeTower(0)");
assert.equal(run("state.towers.length"), 0, "Tutorial không được nhận vị trí Gấu khác vòng đang sáng.");
assert.equal(run("state.tutorial.stepIndex"), 2, "Đặt sai vị trí không được làm tutorial tiến bước.");
run("placeTower(4)");
assert.equal(run("state.tutorial.stepIndex"), 3);
assert.equal(run("state.selectedTowerId"), run("state.towers[0].id"), "Trụ vừa đặt phải tự được chọn để nâng cấp.");
run("continueTutorial(); upgradeTower('damage')");
assert.equal(run("state.tutorial.stepIndex"), 4);
assert.equal(run("state.towers[0].phase"), "Yang", "Gấu phải bắt đầu bài học ở Dương.");
run("continueTutorial(); continueTutorial(); startWave()");
assert.equal(run("state.wave"), 1, "Lần mở đầu tiên phải là wave 1.");
assert.equal(run("state.tutorial.stepIndex"), 6);
assert.equal(run("state.pausedByTutorial"), true, "Mốc tutorial phải dừng wave đang chạy.");
assert.equal(run("state.towers[0].karma"), 0, "Wave 1 không được nạp sẵn Nghiệp Âm.");
run("continueTutorial(); spawnEnemy(); state.spawnQueue -= 1");
assert.equal(run("state.pausedByEnemyIntro"), true, "Kẻ địch mới đầu tiên phải dừng game để giới thiệu.");
assert.equal(elements.enemyIntroTitle.textContent, "Quái bộ", "Kẻ địch đầu tiên phải được giới thiệu đúng tên.");
assert.equal(run("campaign.introducedEnemies.ground"), true, "Kẻ địch đã giới thiệu phải được lưu.");
run("continueEnemyIntro(); state.enemies[0].x = state.towers[0].x + 20; state.enemies[0].y = state.towers[0].y; attack(state.towers[0], state.enemies[0])");
assert.equal(run("state.tutorial.stepIndex"), 7, "Đòn Dương thật phải mở phần giải thích Dương.");
assert.equal(run("state.towers[0].phase"), "Yang", "Khi quái đầu tiên vào tầm ở wave 1, Gấu phải vẫn là Dương.");
run("continueTutorial(); state.enemies = []; state.spawnQueue = 0; state.waveActive = true; finishWaveIfReady()");
assert.equal(run("state.wave"), 1);
assert.equal(run("state.tutorial.stepIndex"), 8, "Chỉ sau khi hết wave 1 mới giới thiệu Âm.");
assert.equal(run("state.towers[0].phase"), "Yang", "Gấu vẫn phải là Dương giữa wave 1 và wave 2.");
run("continueTutorial()");
assert.equal(run("state.tutorial.stepIndex"), 9);
assert.ok(run("state.towers[0].karma < TOWER_TYPES.bear.cycle - TOWER_TYPES.bear.karmaPerAttack"), "Chưa bấm mở wave 2 thì không được nạp gần đầy Nghiệp.");
run("continueTutorial()");
assert.equal(run("state.tutorial.awaiting"), "wave:start");
assert.equal(run("state.towers[0].karma"), 190, "Ngay trước wave 2 mới nạp gần đầy Nghiệp.");
run("startWave()");
assert.equal(run("state.wave"), 2, "Bài học Âm phải bắt đầu ở wave 2.");
assert.equal(run("state.towers[0].phase"), "Yang", "Đầu wave 2 Gấu vẫn Dương cho tới đòn kế tiếp.");
assert.equal(run("state.tutorial.stepIndex"), 10);
run("continueTutorial(); gainKarma(state.towers[0], TOWER_TYPES.bear.karmaPerAttack)");
assert.equal(run("state.towers[0].phase"), "Yin", "Đòn trong wave 2 phải kích hoạt Âm.");
assert.equal(run("state.tutorial.stepIndex"), 11);
run("continueTutorial(); state.enemies = []; state.spawnQueue = 0; state.waveActive = true; finishWaveIfReady()");
assert.equal(run("state.tutorial.stepIndex"), 12, "Hết wave 2 mới hướng dẫn mở wave cuối.");
run("continueTutorial(); startWave()");
assert.equal(run("state.wave"), 3);
assert.equal(run("state.tutorial.stepIndex"), 13);
run("continueTutorial(); state.waveActive = true; state.spawnQueue = 0; state.enemies = []; finishWaveIfReady()");
assert.equal(run("state.tutorial.stepIndex"), 14, "Tutorial phải trở lại sau wave cuối.");
run("continueTutorial(); dismissSelectedTower()");
assert.equal(run("state.tutorial.stepIndex"), 15, "Bán Gấu phải hoàn tất mốc bán trụ.");
run("continueTutorial()");
assert.equal(run("state.levelComplete"), true, "Hoàn tất tutorial và ba wave phải qua màn 1.");
assert.equal(run("campaign.maxUnlockedLevel"), 2, "Qua màn 1 phải mở màn 2.");
assert.equal(storageWrites, 0, "Mở khóa trong lượt chơi không được lưu sau khi refresh.");

// Level 2 must teach the Bee Yang + Bear Yin interaction.
run("loadLevel(1, true); continueTutorial(); continueTutorial(); selectShopTower('bee'); continueTutorial(); placeTower(5); continueTutorial(); selectShopTower('bear'); continueTutorial(); placeTower(3); continueTutorial(); continueTutorial(); startWave(); continueTutorial()");
assert.equal(run("TUTORIALS[1].some(step => `${step.icon} ${step.title} ${step.body} ${step.kicker || ''}`.includes('×'))"), false, "Tutorial không được ghi nhãn Gấu × Ong.");
assert.equal(run("TUTORIALS[1][0].body"), "Ong Dương gây x3 damage lên mọi kẻ địch đang được tăng tốc.", "Màn 2 phải mô tả x3 là cơ chế của Ong.");
assert.equal(run("state.tutorial.awaiting"), "combo:enemy_ready", "Tutorial phải chờ quái tới trước khi chuyển Gấu sang Âm.");
assert.equal(run("state.towers.find(tower => tower.type === 'bear').phase"), "Yang", "Gấu phải giữ Dương khi quái chưa tới vùng combo.");
assert.equal(run("state.towers.find(tower => tower.type === 'bee').phase"), "Yang");
assert.ok(run("state.towers.find(tower => tower.type === 'bear').x < state.towers.find(tower => tower.type === 'bee').x"), "Màn 2 phải đặt Gấu bên trái Ong.");
assert.equal(run("state.towers.find(tower => tower.type === 'bear').y"), run("state.towers.find(tower => tower.type === 'bee').y"), "Gấu và Ong phải nằm cùng một dải để vị trí trái/phải rõ ràng.");
assert.ok(run("distance(state.towers.find(tower => tower.type === 'bear'), { x: 590, y: 185 }) <= towerRange(state.towers.find(tower => tower.type === 'bear')) && distance(state.towers.find(tower => tower.type === 'bee'), { x: 590, y: 185 }) <= towerRange(state.towers.find(tower => tower.type === 'bee'))"), "Tầm thật của Gấu và Ong phải giao nhau trên lane.");
run("spawnEnemy(); state.spawnQueue -= 1; resetEnemyModifiers(); applyTowerAuras()");
assert.equal(run("pauseForComboEnemy()"), false, "Tutorial không được dừng khi quái còn ngoài vùng combo.");
assert.equal(run("state.towers.find(tower => tower.type === 'bear').phase"), "Yang");
run("state.enemies[0].x = 590; state.enemies[0].y = 185; resetEnemyModifiers(); applyTowerAuras(); pauseForComboEnemy()");
assert.equal(run("state.pausedByTutorial"), true, "Game phải dừng khi quái vừa tới vùng combo.");
assert.equal(run("state.tutorial.stepIndex"), 8, "Quái tới mới được mở bước hướng dẫn Gấu Âm.");
assert.equal(run("state.towers.find(tower => tower.type === 'bear').phase"), "Yang", "Gấu vẫn Dương cho tới khi người chơi tiếp tục bước mới.");
run("continueTutorial(); resetEnemyModifiers(); applyTowerAuras()");
assert.equal(run("state.towers.find(tower => tower.type === 'bear').phase"), "Yin", "Gấu chỉ chuyển Âm sau khi quái đã tới.");
assert.equal(run("state.enemies[0].speedBonus"), 0.45, "Gấu Âm phải tăng tốc địch trong tầm thật.");
assert.equal(run("state.enemies[0].bearHasteTimer"), 2, "Gấu Âm phải nạp buff tốc độ tồn tại 2 giây.");
run("state.enemies[0].hp = 200; state.enemies[0].maxHp = 200");
const comboHp = run("state.enemies[0].hp");
run("attack(state.towers.find(tower => tower.type === 'bee'), state.enemies[0])");
assert.equal(comboHp - run("state.enemies[0].hp"), run("TOWER_TYPES.bee.damage * 3"), "Ong Dương phải gây đúng x3 damage lên địch được tăng tốc.");
assert.equal(run("state.effects.some(effect => effect.kind === 'combo' && effect.text === 'ONG DƯƠNG · x3')"), true, "Ong phải hiện feedback x3 trên quái tăng tốc mà không ghi Gấu × Ong.");
assert.equal(run("Math.round(poisonDamagePerSecond(state.enemies[0]) * 100) / 100"), 7.93, "Độc Ong phải mạnh hơn 5 damage khi Gấu Âm tăng tốc địch 45%.");
const poisonedHp = run("state.enemies[0].hp");
run("updateEnemies(1)");
assert.equal(Math.round((poisonedHp - run("state.enemies[0].hp")) * 100) / 100, 7.93, "Tick độc phải dùng damage đã khuếch đại theo tốc độ.");
assert.equal(run("state.effects.some(effect => effect.kind === 'damage' && effect.color === COLORS.poison)"), true, "Damage độc phải hiện số màu xanh.");
run("state.enemies[0].x = 590; state.enemies[0].y = 185; resetEnemyModifiers(); applyTowerAuras(); state.enemies[0].x = state.towers.find(tower => tower.type === 'bear').x + towerRange(state.towers.find(tower => tower.type === 'bear')) + 1; state.enemies[0].y = state.towers.find(tower => tower.type === 'bear').y; resetEnemyModifiers(); applyTowerAuras()");
assert.equal(run("state.enemies[0].speedBonus"), 0.45, "Địch vừa rời tầm Gấu vẫn phải giữ tăng tốc.");
run("state.enemies[0].poisonTimer = 0; state.enemies[0].poisonDamageBuffer = 0; updateEnemies(1.9); resetEnemyModifiers()");
assert.equal(run("state.enemies[0].speedBonus"), 0.45, "Buff Gấu phải còn hiệu lực trước mốc 2 giây.");
run("updateEnemies(0.11); resetEnemyModifiers()");
assert.equal(run("state.enemies[0].speedBonus"), 0, "Buff Gấu phải hết sau 2 giây ngoài tầm.");
assert.equal(run("poisonDamagePerSecond(state.enemies[0])"), 5, "Độc phải trở về 5 damage khi địch hết tăng tốc.");
run("state.enemies[0].poisonTimer = 1; state.enemies[0].poisonFeedbackTimer = 1; state.enemies[0].poisonDamageBuffer = 0");
const normalPoisonHp = run("state.enemies[0].hp");
run("updateEnemies(1)");
assert.equal(Math.round((normalPoisonHp - run("state.enemies[0].hp")) * 100) / 100, 5, "Độc tốc độ thường phải gây đúng 5 damage mỗi giây.");
assert.equal(run("state.effects.some(effect => effect.kind === 'damage' && effect.text === '-5' && effect.color === COLORS.poison)"), true, "Độc tốc độ thường phải hiện rõ -5 màu xanh.");
assert.equal(run("poisonDamagePerSecond({ poison: 5, slow: 0.65, speedBonus: 0.45 })"), 5, "Làm chậm không được khiến độc mạnh hơn tốc độ cơ bản.");
assert.equal(run("state.tutorial.stepIndex"), 9, "Combo thật phải kích hoạt mốc tutorial cuối.");
run("continueTutorial()");
assert.equal(run("campaign.completedTutorials[2]"), true, "Tutorial Ong phải được đánh dấu hoàn tất trong lượt chơi.");

// Unlock all levels for isolated mechanics checks.
run("campaign.maxUnlockedLevel = 5; campaign.currentLevel = 5; campaign.completedTutorials = {1:true,2:true,3:true,4:true,5:true}; loadLevel(4)");
assert.equal(run("WAVES.length"), 6, "Màn 5 phải có sáu wave.");
assert.equal(elements.towerShop.children.filter(button => !button.disabled).length, 5, "Màn 5 phải mở đủ năm trụ.");
run("loadLevel(0)");
assert.equal(elements.towerShop.children.filter(button => !button.disabled).length, 5, "Trụ đã mở phải còn dùng được khi chơi lại màn cũ.");
run("loadLevel(4)");

// Every new enemy type pauses once, and invisible enemies require Yang Crab reveal.
run("loadLevel(1); state.wave = 3; state.spawnQueue = WAVES[2].count - 3; spawnEnemy()");
assert.equal(run("state.enemies[0].kind"), "flying", "Wave mixed phải sinh được quái bay.");
assert.equal(elements.enemyIntroTitle.textContent, "Quái bay", "Quái bay mới phải được giới thiệu.");
assert.equal(run("state.pausedByEnemyIntro"), true);
run("continueEnemyIntro(); state.enemies = []; state.wave = 6; state.spawnQueue = WAVES[5].count; spawnEnemy()");
assert.equal(run("state.enemies[0].kind"), "elite", "Wave cuối phải sinh được tinh anh.");
assert.equal(elements.enemyIntroTitle.textContent, "Tinh anh", "Tinh anh mới phải được giới thiệu.");
assert.equal(run("state.enemies[0].maxHp"), run("Math.round(Math.round(BASE_WAVES[5].hp * LEVELS[1].difficulty * 0.6) * 2.1)"), "Máu Tinh Anh cũng phải còn đúng 60% so với trước.");
run("updateUI()");
assert.equal(elements.statusLegend.children.find(icon => icon.dataset.status === "armor").classList.contains("active"), true, "Icon giáp phải sáng khi tinh anh có mặt.");
run("continueEnemyIntro(); loadLevel(3); state.wave = 2; state.spawnQueue = WAVES[1].count; spawnEnemy()");
assert.equal(run("WAVES[1].type"), "stealth", "Màn Cua wave 2 phải giới thiệu tàng hình.");
assert.equal(run("state.enemies[0].kind"), "invisible", "Wave tàng hình phải sinh quái tàng hình.");
assert.equal(elements.enemyIntroTitle.textContent, "Quái tàng hình", "Quái tàng hình mới phải được giới thiệu.");
assert.equal(run("state.pausedByEnemyIntro"), true);
run("continueEnemyIntro(); state.enemies[0].x = BUILD_SPOTS[3].x; state.enemies[0].y = BUILD_SPOTS[3].y; state.water = 999; selectShopTower('bear'); placeTower(3); resetEnemyModifiers()");
assert.equal(run("state.enemies[0].revealed"), false, "Quái tàng hình phải ẩn khi chưa có Cua Dương.");
assert.equal(run("chooseTarget(state.towers.find(tower => tower.type === 'bear'))"), null, "Gấu không được nhắm quái chưa bị soi lộ.");
run("selectShopTower('crab'); placeTower(4); resetEnemyModifiers(); applyTowerAuras(); updateUI()");
assert.equal(run("state.enemies[0].revealed"), true, "Aura Cua Dương phải soi lộ quái tàng hình.");
assert.equal(run("chooseTarget(state.towers.find(tower => tower.type === 'bear')).id"), run("state.enemies[0].id"), "Gấu phải nhắm được quái sau khi Cua soi lộ.");
assert.equal(elements.statusLegend.children.find(icon => icon.dataset.status === "invisible").classList.contains("active"), true, "Icon tàng hình phải sáng khi loại quái này có mặt.");
assert.equal(run("enemyStatusBadges(state.enemies[0]).some(badge => badge.icon === '👁')"), true, "Quái bị soi lộ phải có feedback con mắt.");
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

run("loadLevel(4); selectShopTower('crab'); placeTower(0); selectShopTower('bear'); placeTower(1); state.towers[0].phase = 'Yin'; state.towers[0].karma = TOWER_TYPES.crab.cycle; state.towers[1].phase = 'Yin'; state.towers[1].karma = TOWER_TYPES.bear.cycle; state.enemies = []; updateTowers(1)");
assert.equal(run("CRAB_YIN_DISCHARGE_MULTIPLIER"), 0.5, "Aura Cua Âm phải giảm một nửa tốc độ xả Âm.");
assert.equal(run("state.towers[1].karma"), 190, "Gấu trong aura Cua Âm chỉ được xả 10 thay vì 20 Nghiệp mỗi giây.");
run("state.towers[1].phase = 'Yang'; state.towers[1].karma = 0; state.towers[1].cooldown = 0; state.enemies = [{ id: 1, kind: 'elite', x: state.towers[1].x + 10, y: state.towers[1].y, hp: 1000, maxHp: 1000, physicalArmor: 0, armorBuff: 0, magicResist: 0, shield: 0, dead: false, reward: 0, segment: 0, progress: 0, speedBonus: 0 }]; updateTowers(0)");
assert.equal(run("state.towers[1].karma"), run("TOWER_TYPES.bear.karmaPerAttack"), "Cua Âm không còn tăng 75% tốc độ tích Nghiệp.");
assert.equal(run("Math.round(state.towers[1].cooldown * 10000)"), run("Math.round(1 / (TOWER_TYPES.bear.fireRate * 0.8) * 10000)"), "Cua Âm vẫn phải giảm 20% tốc đánh của trụ trong aura.");
assert.equal(run("TOWER_TYPES.crab.yin.includes('chậm 50%') && TOWER_TYPES.crab.yin.includes('-20%')"), true, "Mô tả Cua Âm phải hiện đủ xả chậm và giảm tốc đánh.");

run("loadLevel(4); selectShopTower('fox'); placeTower(0); state.towers[0].phase = 'Yin'; state.towers[0].karma = TOWER_TYPES.fox.cycle; state.towers[0].cooldown = 0; state.enemies = [{ id: 1, kind: 'elite', x: state.towers[0].x + 10, y: state.towers[0].y, hp: 1000, maxHp: 1000, physicalArmor: 0, armorBuff: 0, magicResist: 0, shield: 0, dead: false, reward: 0, segment: 0, progress: 0, speedBonus: 0 }]; updateTowers(0)");
assert.equal(run("FOX_YIN_ATTACK_SPEED_MULTIPLIER"), 5, "Cáo Âm phải có hệ số tốc đánh x5.");
assert.equal(run("Math.round(state.towers[0].cooldown * 10000)"), run("Math.round(1 / (TOWER_TYPES.fox.fireRate * 5) * 10000)"), "Hồi đòn của Cáo Âm phải dùng đúng tốc đánh x5.");
assert.equal(run("TOWER_TYPES.fox.yin.includes('x5')"), true, "Mô tả Cáo Âm phải hiện rõ tốc đánh x5.");

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
assert.equal(run("altarDamage({ kind: 'ground' })"), 4, "Quái thường vào đền phải trừ gấp đôi thành 4 máu.");
assert.equal(run("altarDamage({ kind: 'flying' })"), 4, "Quái bay vào đền phải trừ gấp đôi thành 4 máu.");
assert.equal(run("altarDamage({ kind: 'elite' })"), 8, "Tinh anh vào đền phải trừ gấp đôi thành 8 máu.");
run("state.health = 40; state.enemies = [{ kind: 'ground', reachedEnd: true, dead: false, baseSpeed: 0, slow: 1, speedBonus: 0, bearSlowTimer: 0, poisonTimer: 0, segment: PATH.length - 1 }]; updateEnemies(0)");
assert.equal(run("state.health"), 36, "Quái thường lọt đền phải thực sự làm thanh máu giảm 4.");
run("state.enemies = [{ kind: 'elite', reachedEnd: true, dead: false, baseSpeed: 0, slow: 1, speedBonus: 0, bearSlowTimer: 0, poisonTimer: 0, segment: PATH.length - 1 }]; updateEnemies(0)");
assert.equal(run("state.health"), 28, "Tinh anh lọt đền phải thực sự làm thanh máu giảm thêm 8.");

const wingCurves = [];
drawingContext.quadraticCurveTo = (...points) => wingCurves.push(points);
run("state.elapsed = 0; state.enemies = [{ id: 9, kind: 'flying', x: 200, y: 180, hp: 20, maxHp: 20, shield: 0, slow: 1, speedBonus: 0, poisonTimer: 0, physicalArmor: 0, armorBuff: 0 }]; drawEnemies()");
assert.equal(wingCurves.length, 4, "Quái bay phải vẽ đủ hai cánh.");
const firstWingPose = JSON.stringify(wingCurves);
wingCurves.length = 0;
run("state.elapsed = 0.2; drawEnemies()");
assert.notEqual(JSON.stringify(wingCurves), firstWingPose, "Cánh quái bay phải thay đổi tư thế theo thời gian.");
wingCurves.length = 0;
run("state.enemies[0].kind = 'ground'; drawEnemies()");
assert.equal(wingCurves.length, 0, "Quái mặt đất không được có cánh.");

console.log("Kiểm tra nhanh đã đạt: fullscreen, tutorial, campaign, reset tiến độ và combat.");
