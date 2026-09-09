"use strict";

// Ninja Adventure Asset Pack (CC0, by pixel-boy) — 村民角色
// 每个角色 64px 宽精灵表：4 列 = 4 方向（下/上/左/右），行 = 动画帧（7 行含待机，2 行纯行走）
const CHAR_DEFS = {
  Villager:   { sex: 0, group: "a" }, Villager2:  { sex: 0, group: "a" },
  Villager3:  { sex: 0, group: "a" }, Villager4:  { sex: 0, group: "a" },
  Hunter:     { sex: 0, group: "a" }, ManGreen:   { sex: 0, group: "a" },
  Caveman:    { sex: 0, group: "a" }, Caveman2:   { sex: 0, group: "a" },
  FighterWhite: { sex: 0, group: "a" }, FighterRed: { sex: 0, group: "a" },
  Master:     { sex: 0, group: "e" }, Monk:       { sex: 0, group: "e" },
  Monk2:      { sex: 0, group: "e" }, Eskimo:     { sex: 0, group: "a" },
  OldMan:     { sex: 0, group: "o" }, OldMan2:    { sex: 0, group: "o" },
  OldMan3:    { sex: 0, group: "o" },
  Woman:      { sex: 1, group: "a" }, Cavegirl:   { sex: 1, group: "a" },
  Cavegirl2:  { sex: 1, group: "a" }, OldWoman:   { sex: 1, group: "o" },
  Boy:        { sex: 0, group: "c" }, Child:      { sex: 0, group: "c" },
  EggBoy:     { sex: 0, group: "c" }, EggGirl:    { sex: 1, group: "c" }
};

// 加载的角色表: key -> img
const CharSheets = {};
// 动物/家畜表: key -> img（Ninja Adventure 同一素材包）
const AnimalSheets = {};
// Tiny Town / Tiny Farm 环境表（Kenney CC0，12x11 格整合图，行主序）
const TinySheets = { town: null, farm: null };
const ANIMAL_FILES = [
  "Chicken_SpriteSheetWhite.png", "Chicken_SpriteSheetBrown.png",
  "Chicken_SpriteSheetCute.png", "Chicken_SpriteSheetBlack.png",
  "Pig_SpriteSheetPink.png", "Pig_SpriteSheetBlack.png", "Pig_SpriteSheetRed.png",
  "Cow_SpriteSheetWhite.png", "Cow_SpriteSheetWhiteSide.png"
];
let loadPromise = null;

function loadImage(url) {
  return new Promise((resolve) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => { console.warn("素材加载失败: " + url); resolve(null); };
    img.src = url;
  });
}

async function loadCharSheets(onProgress) {
  if (loadPromise) return loadPromise;
  const total = Object.keys(CHAR_DEFS).length + ANIMAL_FILES.length + Object.keys(TinySheets).length;
  let done = 0;
  const tick = () => { done++; onProgress && onProgress(done, total); };
  loadPromise = (async () => {
    await Promise.all(Object.keys(CHAR_DEFS).map(async (k) => {
      const img = await loadImage("assets/char/" + k + ".png");
      if (img) CharSheets[k] = img;
      tick();
    }));
    await Promise.all(ANIMAL_FILES.map(async (f) => {
      const img = await loadImage("assets/animal/" + f);
      if (img) AnimalSheets[f] = img;
      tick();
    }));
    for (const k of Object.keys(TinySheets)) {
      TinySheets[k] = await loadImage("assets/tiny/" + k + ".png");
      tick();
    }
  })();
  return loadPromise;
}

function hashId(n) {
  let h = (n * 2654435761) >>> 0;
  h ^= h >>> 13; h = Math.imul(h, 1274126177) >>> 0; h ^= h >>> 16;
  return h >>> 0;
}

// 分配角色：按性别/年龄段分组，id 哈希稳定分配；stage 为中文阶段(婴儿/孩童/少年/成年/老年)
function charForVillager(sex, stage, id) {
  const groups = stage && (stage.includes("婴") || stage.includes("童") || stage.includes("少")) ? ["c"]
    : stage && stage.includes("老") ? ["o"]
    : ["a", "e"];
  const keys = Object.keys(CHAR_DEFS).filter(k => {
    const d = CHAR_DEFS[k];
    return d.sex === sex && groups.includes(d.group);
  });
  if (keys.length === 0) keys.push(...Object.keys(CHAR_DEFS).filter(k => CHAR_DEFS[k].sex === sex));
  return keys[hashId(id) % keys.length];
}

// 每角色精灵表行数（7 行: row0 待机 + 行走；2 行: 纯行走）
function charRows(key) {
  const img = CharSheets[key];
  return img ? Math.round(img.height / 16) : 0;
}
