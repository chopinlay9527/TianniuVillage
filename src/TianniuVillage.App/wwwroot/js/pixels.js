"use strict";

const TILE = 16;
const CHUNK = 16;
const CHUNK_PX = TILE * CHUNK;

function mkCanvas(w, h) {
  const c = document.createElement("canvas");
  c.width = w; c.height = h;
  return c;
}

function hash01(a, b, seed) {
  let h = (a * 374761393 + b * 668265263 + seed * 2246822519) >>> 0;
  h = Math.imul(h ^ (h >>> 13), 1274126177) >>> 0;
  h ^= h >>> 16;
  return (h & 0xffffff) / 0xffffff;
}

const TerrainColors = {
  0: ["#16324f", "#152e49", "#173450"],  // 深水
  1: ["#1d4e6b", "#1d5470", "#1a4862"],  // 浅水
  2: ["#d3c489", "#ccbd80", "#d8c990"],  // 沙滩
  3: ["#679b40", "#5f9138", "#6ea347"],  // 草地
  4: ["#4d7f34", "#457630", "#54873a"],  // 森林地被
  5: ["#8a8f68", "#828763", "#919672"],  // 高地
  6: ["#6b6e60", "#5f6255", "#74776a"]   // 山
};

// ===== 地形 pack 贴图系统 =====
// 全部使用扫描验证的 100% 全不透明整格（off-color < 12），杜绝 autotile 碎片
const TERRAIN_PACK = {
  0: { synth: "deepwater" },                                             // 深水（程序化·Ninja深青调色板）
  1: { synth: "shallowwater" },                                          // 浅水（程序化·Ninja浅青调色板）
  2: { sheet: "Element", idx: [93, 94], alt: [{ sheet: "Relief", idx: [163] }] }, // 沙滩
  3: { sheet: "Village", idx: [141, 142, 201, 202], alt: [{ sheet: "Nature", idx: [89, 90, 93, 94, 460] }] }, // 草地
  4: { sheet: "Nature", idx: [73, 74] },                                 // 森林地被（深绿）
  5: { sheet: "Relief", idx: [25, 28, 30, 45, 48, 50] },                 // 高地（苔岩）
  6: { synth: "mountain" }                                               // 山（程序化·Ninja巨岩调色板）
};
const packCellCache = new Map();
function packCell(sheetKey, idx) {
  const key = sheetKey + ":" + idx;
  if (packCellCache.has(key)) return packCellCache.get(key);
  const sheet = TileSheets[sheetKey];
  const out = mkCanvas(16, 16);
  if (!sheet) { packCellCache.set(key, out); return out; }
  const cols = Math.floor(sheet.width / 16);
  const sx = (idx % cols) * 16, sy = Math.floor(idx / cols) * 16;
  const ctx = out.getContext("2d");
  ctx.drawImage(sheet, sx, sy, 16, 16, 0, 0, 16, 16);
  packCellCache.set(key, out);
  return out;
}
// 程序化合成地形（调色板取自 Ninja 素材实测值）
const synthCache = new Map();
function synthTerrain(kind, variant) {
  const key = kind + ":" + variant;
  if (synthCache.has(key)) return synthCache.get(key);
  const c = mkCanvas(16, 16);
  const ctx = c.getContext("2d");
  const h = (n) => hash01(kind.length * 31 + variant, n * 17 + variant * 7, 99);
  if (kind === "shallowwater" || kind === "deepwater") {
    const shallow = kind === "shallowwater";
    const base = shallow ? "#88d1da" : "#4a7886";
    const wave = shallow ? "#6fb9c6" : "#3d6373";
    const spark = shallow ? "#c8ecf0" : "#5d8b99";
    ctx.fillStyle = base;
    ctx.fillRect(0, 0, 16, 16);
    // 横向波纹（错落两道）
    const y1 = 3 + Math.floor(h(1) * 4), y2 = 9 + Math.floor(h(2) * 4);
    ctx.fillStyle = wave;
    ctx.fillRect(Math.floor(h(3) * 8), y1, 5 + Math.floor(h(4) * 3), 1);
    ctx.fillRect(Math.floor(h(5) * 8), y2, 4 + Math.floor(h(6) * 3), 1);
    ctx.fillStyle = spark;
    ctx.fillRect(Math.floor(h(7) * 13), Math.floor(h(8) * 13), 2, 1);
    ctx.fillRect(Math.floor(h(9) * 14), Math.floor(h(10) * 14), 1, 1);
  } else if (kind === "mountain") {
    const base = "#b2a09b", crack = "#8a7772", hi = "#d8ccc8", spec = "#9c8a85";
    ctx.fillStyle = base;
    ctx.fillRect(0, 0, 16, 16);
    // 裂纹（两条折线向下）
    ctx.fillStyle = crack;
    let cx = 2 + Math.floor(h(1) * 5), cy = 0;
    for (let s = 0; s < 5; s++) {
      ctx.fillRect(cx, cy, 1, 2 + Math.floor(h(20 + s) * 2));
      cx += 1 + Math.floor(h(30 + s) * 2);
      cy += 2;
    }
    let cx2 = 9 + Math.floor(h(11) * 4), cy2 = 1;
    for (let s = 0; s < 4; s++) {
      ctx.fillRect(cx2, cy2, 1, 2);
      cx2 -= 1 + Math.floor(h(40 + s) * 2);
      cy2 += 3;
    }
    // 高光（左上边缘）
    ctx.fillStyle = hi;
    ctx.fillRect(0, 0, 3 + Math.floor(h(12) * 3), 1);
    ctx.fillRect(0, 0, 1, 3 + Math.floor(h(13) * 3));
    // 碎点
    ctx.fillStyle = spec;
    for (let i = 0; i < 4; i++)
      ctx.fillRect(Math.floor(h(50 + i) * 14), Math.floor(h(60 + i) * 14), 2, 1);
  }
  synthCache.set(key, c);
  return c;
}
function packTerrainCell(terrain, wx, wy) {
  const def = TERRAIN_PACK[terrain];
  if (!def) return null;
  if (def.synth) return synthTerrain(def.synth, (wx * 31 + wy * 57) % 4);
  // 主集合 + 备选集合合并采样
  const all = [];
  for (const i of def.idx) all.push([def.sheet, i]);
  if (def.alt) for (const a of def.alt) for (const i of a.idx) all.push([a.sheet, i]);
  const pick = all[(wx * 31 + wy * 57) % all.length];
  return packCell(pick[0], pick[1]);
}

function drawTerrainTile(ctx, terrain, tx, ty, px, py) {
  // 若有 pack 素材则整铺源格；仅当对应源缺失时回退程序化
  if (TileSheets && TERRAIN_PACK[terrain] && packTerrainCell(terrain, tx, ty)) {
    ctx.drawImage(packTerrainCell(terrain, tx, ty), px, py);
    // 微噪声叠加，保留手绘纹理感
    for (let i = 0; i < 4; i++) {
      const rx = Math.floor(hash01(tx, ty, 300 + i) * 16);
      const ry = Math.floor(hash01(ty, tx, 600 + i) * 16);
      const v = hash01(tx * 7 + ry, ty * 13 + rx, 11);
      ctx.fillStyle = v < 0.5 ? "rgba(255,255,255,0.05)" : "rgba(0,0,0,0.06)";
      ctx.fillRect(px + rx, py + ry, 1, 1);
    }
    return;
  }
  const pal = TerrainColors[terrain] || TerrainColors[3];
  for (let y = 0; y < TILE; y++) {
    for (let x = 0; x < TILE; x++) {
      const r = hash01(tx * TILE + x, ty * TILE + y, 7);
      ctx.fillStyle = r < 0.16 ? pal[1] : r > 0.86 ? pal[2] : pal[0];
      ctx.fillRect(px + x, py + y, 1, 1);
    }
  }
  if (terrain === 1) {
    for (let i = 0; i < 3; i++) {
      const rx = Math.floor(hash01(tx, ty, 100 + i) * (TILE - 4));
      const ry = Math.floor(hash01(ty, tx, 200 + i) * (TILE - 2));
      ctx.fillStyle = "rgba(210,235,240,0.28)";
      ctx.fillRect(px + rx, py + ry + (ty + tx) % 3, 3, 1);
    }
  }
}

// Kenney Roguelike 树的 16x16 格（行主序 = ty*57+tx；4 变体含果子树）
const KENNEY_TREES = [9 * 57 + 13, 9 * 57 + 15, 11 * 57 + 13, 11 * 57 + 23, 11 * 57 + 16];
const kenneyTreeCache = new Map();
function kenneyTreeCell(seed) {
  const idx = KENNEY_TREES[seed % KENNEY_TREES.length];
  if (kenneyTreeCache.has(idx)) return kenneyTreeCache.get(idx);
  const out = mkCanvas(16, 16);
  if (KenneySheet.img) {
    const ctx = out.getContext("2d");
    const tx = idx % 57, ty = Math.floor(idx / 57);
    ctx.drawImage(KenneySheet.img, tx * 17, ty * 17, 16, 16, 0, 0, 16, 16);
    // 树冠重新着色：冷绿 → Ninja 橄榄绿系（与草地 154,178,56 同族）
    const pd = ctx.getImageData(0, 0, 16, 16);
    const d = pd.data;
    for (let i = 0; i < d.length; i += 4) {
      if (d[i + 3] < 40) continue;
      const r = d[i], g = d[i + 1], b = d[i + 2];
      if (g > r + 12 && g > b + 12) {
        d[i] = Math.min(255, r * 0.9 + 20);
        d[i + 1] = Math.min(255, g * 0.95);
        d[i + 2] = b * 0.45;
      }
    }
    ctx.putImageData(pd, 0, 0);
  }
  kenneyTreeCache.set(idx, out);
  return out;
}

function drawTree(ctx, px, py, seed) {
  if (KenneySheet.img) {
    // 落地阴影
    ctx.fillStyle = "rgba(30,40,20,0.25)";
    ctx.fillRect(px + 3, py + 13, 10, 2);
    ctx.drawImage(kenneyTreeCell(seed), px, py);
    return;
  }
  const trunkX = px + 7;
  ctx.fillStyle = "#6b4a2c";
  ctx.fillRect(trunkX, py + 10, 2, 5);
  const greens = ["#2e6b28", "#397a2f", "#2a5f26"];
  for (let i = 0; i < 3; i++) {
    const w = 10 - i * 3;
    const y = py + 8 - i * 4;
    const off = Math.floor(hash01(px, py, seed + i) * 2);
    ctx.fillStyle = greens[(seed + i) % 3];
    ctx.fillRect(px + 8 - w / 2 + off, y, w, 5);
  }
  ctx.fillStyle = "#4d8f3f";
  ctx.fillRect(px + 5, py + 3, 3, 2);
}

function drawBush(ctx, px, py, hasBerries) {
  ctx.fillStyle = "#3c7a2e";
  ctx.fillRect(px + 3, py + 8, 10, 6);
  ctx.fillStyle = "#4c8c3a";
  ctx.fillRect(px + 4, py + 7, 8, 4);
  if (hasBerries) {
    ctx.fillStyle = "#c4384d";
    ctx.fillRect(px + 5, py + 9, 2, 2);
    ctx.fillRect(px + 9, py + 10, 2, 2);
    ctx.fillRect(px + 7, py + 8, 2, 2);
  }
}

function drawMushroom(ctx, px, py) {
  ctx.fillStyle = "#e8ddc8";
  ctx.fillRect(px + 6, py + 11, 3, 3);
  ctx.fillStyle = "#b8503c";
  ctx.fillRect(px + 4, py + 8, 7, 3);
  ctx.fillStyle = "#e8ddc8";
  ctx.fillRect(px + 6, py + 8, 1, 1);
  ctx.fillRect(px + 9, py + 9, 1, 1);
}

function drawStone(ctx, px, py, seed) {
  ctx.fillStyle = "#8a8d80";
  ctx.fillRect(px + 3, py + 6, 10, 8);
  ctx.fillStyle = "#9da093";
  ctx.fillRect(px + 4, py + 5, 7, 4);
  ctx.fillStyle = "#767970";
  ctx.fillRect(px + 3, py + 12, 10, 2);
  if (hash01(px, py, seed) > 0.5) {
    ctx.fillStyle = "#8a8d80";
    ctx.fillRect(px + 11, py + 9, 4, 5);
  }
}

function drawHerb(ctx, px, py) {
  ctx.fillStyle = "#5aa06a";
  for (let i = 0; i < 4; i++) {
    const x = px + 3 + i * 3;
    ctx.fillRect(x, py + 10, 1, 4);
    ctx.fillRect(x - 1, py + 9, 1, 2);
    ctx.fillRect(x + 1, py + 9, 1, 2);
  }
  ctx.fillStyle = "#8fc89a";
  ctx.fillRect(px + 6, py + 8, 2, 2);
}

function drawFishSpot(ctx, px, py) {
  ctx.fillStyle = "rgba(40,70,60,0.5)";
  ctx.fillRect(px + 3, py + 6, 10, 6);
  ctx.fillStyle = "#5a8a7a";
  ctx.fillRect(px + 6, py + 8, 4, 2);
  ctx.fillRect(px + 5, py + 8, 1, 2);
  ctx.fillRect(px + 10, py + 8, 1, 2);
}

function roadAt(roadLevels, mapW, mapH, x, y) {
  if (x < 0 || y < 0 || x >= mapW || y >= mapH) return 0;
  return roadLevels[y * mapW + x] | 0;
}

function drawRoad(ctx, level, px, py, wx, wy, roadLevels, mapW, mapH) {
  const hasN = roadAt(roadLevels, mapW, mapH, wx, wy - 1) > 0;
  const hasS = roadAt(roadLevels, mapW, mapH, wx, wy + 1) > 0;
  const hasW = roadAt(roadLevels, mapW, mapH, wx - 1, wy) > 0;
  const hasE = roadAt(roadLevels, mapW, mapH, wx + 1, wy) > 0;
  const hasNE = roadAt(roadLevels, mapW, mapH, wx + 1, wy - 1) > 0;
  const hasNW = roadAt(roadLevels, mapW, mapH, wx - 1, wy - 1) > 0;
  const hasSE = roadAt(roadLevels, mapW, mapH, wx + 1, wy + 1) > 0;
  const hasSW = roadAt(roadLevels, mapW, mapH, wx - 1, wy + 1) > 0;

  // 主体：有路邻居的边延伸到格边缘，无邻居的边留草边
  const x0 = hasW ? 0 : 1;
  const x1 = hasE ? 16 : 15;
  const y0 = hasN ? 0 : 2;
  const y1 = hasS ? 16 : 14;
  const bw = x1 - x0, bh = y1 - y0;

  const base = level === 1 ? "#c9b088" : level === 2 ? "#a89878" : "#b4b0a4";
  const edge = level === 1 ? "#b9a078" : level === 2 ? "#8a7a5c" : "#8f8b80";
  const topBand = level === 3 ? "#c8c4b8" : edge;

  ctx.fillStyle = base;
  ctx.fillRect(px + x0, py + y0, bw, bh);

  // 开放边描线（仅无路邻居的边）
  ctx.fillStyle = topBand;
  if (!hasN) ctx.fillRect(px + x0, py + y0, bw, 2);
  ctx.fillStyle = edge;
  if (!hasS) ctx.fillRect(px + x0, py + y1 - 2, bw, 2);
  if (!hasW) ctx.fillRect(px + x0, py + y0, 1, bh);
  if (!hasE) ctx.fillRect(px + x1 - 1, py + y0, 1, bh);

  // 纯斜向连接：对角是路且两侧正交都不是路时，画角部阶梯连接块
  ctx.fillStyle = base;
  if (hasNE && !hasN && !hasE)
    for (let i = 0; i < 6; i++) ctx.fillRect(px + 10 + i, py + i, 6 - i, 1);
  if (hasNW && !hasN && !hasW)
    for (let i = 0; i < 6; i++) ctx.fillRect(px, py + i, 6 - i, 1);
  if (hasSE && !hasS && !hasE)
    for (let i = 0; i < 6; i++) ctx.fillRect(px + 10 + i, py + 15 - i, 6 - i, 1);
  if (hasSW && !hasS && !hasW)
    for (let i = 0; i < 6; i++) ctx.fillRect(px, py + 15 - i, 6 - i, 1);

  // 等级材质细节
  if (level === 1) {
    for (let i = 0; i < 3; i++) {
      const rx = Math.floor(hash01(wx, wy, 400 + i) * 12);
      const ry = Math.floor(hash01(wy, wx, 500 + i) * 9);
      ctx.fillStyle = "#d8c298";
      ctx.fillRect(px + 2 + rx, py + 4 + ry, 2, 1);
    }
  } else if (level === 2) {
    for (let i = 0; i < 5; i++) {
      const rx = Math.floor(hash01(wx, wy, 600 + i) * 12);
      const ry = Math.floor(hash01(wy, wx, 700 + i) * 9);
      ctx.fillStyle = i % 2 ? "#8f8064" : "#bcae8c";
      ctx.fillRect(px + 2 + rx, py + 4 + ry, 2, 2);
    }
  } else if (level === 3) {
    ctx.fillStyle = "#9d998e";
    ctx.fillRect(px + 7, py + y0, 1, bh);
    ctx.fillRect(px + x0, py + 7, bw, 1);
    ctx.fillStyle = "#a8a49a";
    ctx.fillRect(px + 3, py + 4, 3, 2);
    ctx.fillRect(px + 10, py + 9, 3, 2);
  }
}

const ResKind = { Tree: 0, BerryBush: 1, Mushroom: 2, Stone: 3, Herb: 4, FishSpot: 5 };

function drawResource(ctx, r, px, py) {
  switch (r.k) {
    case ResKind.Tree: drawTree(ctx, px, py, r.id); break;
    case ResKind.BerryBush: drawBush(ctx, px, py, r.berries); break;
    case ResKind.Mushroom: drawMushroom(ctx, px, py); break;
    case ResKind.Stone: drawStone(ctx, px, py, r.id); break;
    case ResKind.Herb: drawHerb(ctx, px, py); break;
    case ResKind.FlaxPatch:
      ctx.fillStyle = "#5a8a5a";
      ctx.fillRect(px + 3, py + 8, 10, 6);
      ctx.fillStyle = "#8aa8d0";
      ctx.fillRect(px + 4, py + 5, 2, 4);
      ctx.fillRect(px + 8, py + 4, 2, 5);
      ctx.fillRect(px + 11, py + 6, 2, 3);
      ctx.fillStyle = "#b8c8e8";
      ctx.fillRect(px + 4, py + 4, 2, 1);
      ctx.fillRect(px + 8, py + 3, 2, 1);
      break;
    case ResKind.FishSpot: drawFishSpot(ctx, px, py); break;
    case ResKind.WaterSpot:
      ctx.fillStyle = "rgba(190,230,240,0.5)";
      ctx.fillRect(px + 4, py + 6, 8, 1);
      ctx.fillRect(px + 6, py + 10, 8, 1);
      ctx.fillRect(px + 3, py + 13, 6, 1);
      break;
    case ResKind.CopperVein:
      ctx.fillStyle = "#8a7a5c";
      ctx.fillRect(px + 2, py + 5, 12, 9);
      ctx.fillStyle = "#c4854a";
      ctx.fillRect(px + 4, py + 7, 3, 2);
      ctx.fillRect(px + 8, py + 9, 4, 2);
      ctx.fillStyle = "#e0a050";
      ctx.fillRect(px + 5, py + 8, 1, 1);
      ctx.fillRect(px + 9, py + 10, 1, 1);
      break;
    case ResKind.IronVein:
      ctx.fillStyle = "#8a7a5c";
      ctx.fillRect(px + 2, py + 5, 12, 9);
      ctx.fillStyle = "#8a9aaa";
      ctx.fillRect(px + 4, py + 7, 3, 2);
      ctx.fillRect(px + 8, py + 9, 4, 2);
      ctx.fillStyle = "#b0c0d0";
      ctx.fillRect(px + 5, py + 8, 1, 1);
      ctx.fillRect(px + 9, py + 10, 1, 1);
      break;
  }
}

const FW = 14, FH = 18;

// Ninja Adventure 角色表 → 村民贴图组装
// 源表: 每格 16x16，列=方向(0下/1上/2左/3右)，行=帧
// 目标: 横向帧块(每块4方向) → [idle, walkA, walkB] x [下,上,左,右]
const CHAR_COL_BY_FACE = [3, 2, 0, 1]; // facing 0右/1左/2下/3上 → 源列

function makeVillagerTexture(sex, stage, id) {
  const key = charForVillager(sex, stage, id);
  const sheet = CharSheets[key];
  if (!sheet) {
    // 回退：旧程序化纹理按新布局重组（3帧块 x 4方向, 16x16 格）
    const legacy = makeVillagerTextureProc((id * 77) % 360, sex === 0 ? "#5a7ab8" : "#c46a8a");
    const c = mkCanvas(16 * 4 * 3, 16);
    const ctx = c.getContext("2d");
    for (let f = 0; f < 3; f++) {
      const fb = f === 2 ? 0 : f; // idle=block0, walkA=block1, walkB=block0
      for (let face = 0; face < 4; face++) {
        ctx.drawImage(legacy, face * 14 + fb * 56, 0, 14, 18, (f * 4 + face) * 16 + 1, 0, 14, 16);
      }
    }
    return c;
  }

  const rows = charRows(key);
  const idleRow = 0;
  const walkRows = rows <= 2 ? [0, 1] : [1, 3];
  const frameRows = [idleRow, walkRows[0], walkRows[1]];

  const c = mkCanvas(16 * 4 * 3, 16);
  const ctx = c.getContext("2d");
  for (let f = 0; f < 3; f++) {
    for (let face = 0; face < 4; face++) {
      const srcCol = CHAR_COL_BY_FACE[face];
      const srcRow = frameRows[f];
      // 底部对齐：计算该格不透明底行
      let bottom = -1;
      const probe = mkCanvas(16, 16);
      probe.getContext("2d").drawImage(sheet, srcCol * 16, srcRow * 16, 16, 16, 0, 0, 16, 16);
      const pd = probe.getContext("2d").getImageData(0, 0, 16, 16).data;
      for (let y = 15; y >= 0 && bottom < 0; y--) {
        let rowHas = false;
        for (let x = 0; x < 16; x++) if (pd[(y * 16 + x) * 4 + 3] > 40) { rowHas = true; break; }
        if (rowHas) bottom = y;
      }
      const dy = 15 - Math.max(0, bottom);
      ctx.drawImage(sheet, srcCol * 16, srcRow * 16, 16, 16, (f * 4 + face) * 16, dy, 16, 16);
    }
  }
  return c;
}

function makeVillagerTextureProc(hue, clothesColor) {
  const FRAMES = 8;
  const c = mkCanvas(FW * FRAMES, FH);
  const ctx = c.getContext("2d");
  const skin = "#e8b88a";
  const hair = `hsl(${hue},45%,38%)`;
  const dark = shade(clothesColor, -18);

  for (let f = 0; f < FRAMES; f++) {
    const ox = f * FW;
    const dir = f % 4;
    const frame = Math.floor(f / 4);
    const bob = frame === 1 ? 1 : 0;
    const legSplit = frame === 1 ? 1 : 0;

    if (dir === 1) {
      ctx.fillStyle = hair;
      ctx.fillRect(ox + 4, 1 + bob, 6, 4);
      ctx.fillStyle = skin;
      ctx.fillRect(ox + 5, 4 + bob, 4, 3);
    } else {
      ctx.fillStyle = hair;
      ctx.fillRect(ox + 4, 1 + bob, 6, 5);
      ctx.fillStyle = skin;
      ctx.fillRect(ox + 5, 5 + bob, 4, 3);
      ctx.fillStyle = "#3a2a20";
      if (dir === 0) { ctx.fillRect(ox + 5, 6 + bob, 1, 1); ctx.fillRect(ox + 8, 6 + bob, 1, 1); }
      else if (dir === 2) { ctx.fillRect(ox + 5, 6 + bob, 1, 1); }
      else { ctx.fillRect(ox + 8, 6 + bob, 1, 1); }
    }

    ctx.fillStyle = clothesColor;
    ctx.fillRect(ox + 4, 8 + bob, 6, 6);
    ctx.fillStyle = dark;
    ctx.fillRect(ox + 4, 12 + bob, 6, 2);

    ctx.fillStyle = "#4a3a2c";
    if (frame === 0) {
      ctx.fillRect(ox + 5, 14, 2, 4);
      ctx.fillRect(ox + 8, 14, 2, 4);
    } else {
      ctx.fillRect(ox + 4, 14, 2, 4);
      ctx.fillRect(ox + 8, 14, 2, 4);
    }
  }
  return c;
}

function shade(hex, amt) {
  const n = parseInt(hex.slice(1), 16);
  const r = Math.max(0, Math.min(255, (n >> 16) + amt));
  const g = Math.max(0, Math.min(255, ((n >> 8) & 0xff) + amt));
  const b = Math.max(0, Math.min(255, (n & 0xff) + amt));
  return `rgb(${r},${g},${b})`;
}

function makeAnimalTexture(kind) {
  const c = mkCanvas(16, 16);
  const ctx = c.getContext("2d");
  const seed = kind === "deer" ? 1 : 2;
  if (kind === "deer") {
    ctx.fillStyle = "#8a5f3c"; // 鹿身
    ctx.fillRect(4, 6, 8, 6);
    ctx.fillStyle = "#a4744a";
    ctx.fillRect(5, 5, 6, 5);
    ctx.fillRect(9, 4, 4, 4);
    ctx.fillStyle = "#4a3a28";
    ctx.fillRect(4, 6, 1, 1);
    ctx.fillRect(11, 6, 1, 1);
    ctx.fillRect(5, 5, 1, 1);
    ctx.fillRect(10, 5, 1, 1);
    ctx.fillStyle = "#d8c8a8"; // 角
    ctx.fillRect(7, 2, 1, 2); ctx.fillRect(8, 3, 1, 1);
    ctx.fillRect(10, 2, 1, 2); ctx.fillRect(9, 3, 1, 1);
    ctx.fillStyle = "#6b4a2c"; // 腿
    ctx.fillRect(5, 12, 2, 4); ctx.fillRect(9, 12, 2, 4);
  } else if (kind === "rabbit") {
    ctx.fillStyle = "#c9c2b4";
    ctx.fillRect(5, 5, 6, 7);
    ctx.fillStyle = "#efe9dd";
    ctx.fillRect(6, 7, 4, 3);
    ctx.fillStyle = "#3a3428";
    ctx.fillRect(6, 6, 1, 1); ctx.fillRect(9, 6, 1, 1);
    ctx.fillStyle = "#c9c2b4"; // 耳
    ctx.fillRect(5, 1, 2, 5); ctx.fillRect(9, 1, 2, 5);
    ctx.fillStyle = "#efe9dd";
    ctx.fillRect(5, 1, 1, 4); ctx.fillRect(10, 1, 1, 4);
    ctx.fillStyle = "#b0a898"; // 腿
    ctx.fillRect(6, 12, 2, 3); ctx.fillRect(8, 12, 2, 3);
  } else { // 家畜羊(程序化备用)
    ctx.fillStyle = "#e8e4dc"; // 羊毛团
    ctx.fillRect(3, 4, 10, 9);
    ctx.fillRect(4, 2, 4, 8);
    ctx.fillStyle = "#cfc9bd";
    ctx.fillRect(3, 12, 10, 2);
    ctx.fillStyle = "#8a7a6a"; // 头
    ctx.fillRect(10, 6, 5, 5);
    ctx.fillStyle = "#3a3428";
    ctx.fillRect(12, 7, 1, 1);
    ctx.fillStyle = "#6b5a4c";
    ctx.fillRect(11, 11, 3, 2);
    ctx.fillStyle = "#6b5a4a"; // 腿
    ctx.fillRect(4, 13, 2, 3); ctx.fillRect(7, 13, 2, 3); ctx.fillRect(10, 13, 2, 3);
  }
  void seed;
  return c;
}

const LIVESTOCK_SHEETS = {
  chicken: ["Chicken_SpriteSheetWhite.png", "Chicken_SpriteSheetBrown.png", "Chicken_SpriteSheetCute.png", "Chicken_SpriteSheetBlack.png"],
  pig: ["Pig_SpriteSheetPink.png", "Pig_SpriteSheetBlack.png", "Pig_SpriteSheetRed.png"],
  cow: ["Cow_SpriteSheetWhite.png", "Cow_SpriteSheetWhiteSide.png"]
};
const livestockTexCache = new Map();

// 家畜贴图：pack 素材取首帧底部对齐；羊/缺失时程序化
function makeLivestockTexture(kind, seed) {
  const cacheKey = kind + ":" + seed;
  if (livestockTexCache.has(cacheKey)) return livestockTexCache.get(cacheKey);

  let tex = null;
  if (kind !== "sheep") {
    const variants = LIVESTOCK_SHEETS[kind];
    if (variants) {
      const file = variants[seed % variants.length];
      const sheet = AnimalSheets[file];
      if (sheet) {
        const c = mkCanvas(16, 16);
        const ctx = c.getContext("2d");
        const probe = mkCanvas(16, 16);
        const pctx = probe.getContext("2d");
        pctx.drawImage(sheet, 0, 0, 16, 16, 0, 0, 16, 16);
        const pd = pctx.getImageData(0, 0, 16, 16).data;
        let bottom = -1;
        for (let y = 15; y >= 0 && bottom < 0; y--)
          for (let x = 0; x < 16; x++)
            if (pd[(y * 16 + x) * 4 + 3] > 40) { bottom = y; break; }
        ctx.drawImage(sheet, 0, 0, 16, 16, 0, 15 - Math.max(0, bottom), 16, 16);
        tex = c;
      }
    }
  }
  if (!tex) tex = makeAnimalTexture("sheep"); // 素材缺失时回退程序化
  livestockTexCache.set(cacheKey, tex);
  return tex;
}

const BuildingStyle = {
  villagecenter: { roof: "#8a6a3c", wall: "#b09868", w: 2, h: 2 },
  hut:       { roof: "#7a6a3c", wall: "#a89060", w: 1, h: 1 },
  manor:     { roof: "#6a4a8a", wall: "#d8c8a8", w: 3, h: 3 },
  weaver:    { roof: "#7a6a9a", wall: "#c0a890", w: 2, h: 2 },
  smelter:   { roof: "#5a4038", wall: "#8a6a4c", w: 2, h: 2 },
  workshop:  { roof: "#6a7a5a", wall: "#b8a078", w: 3, h: 3 },
  coppermine:{ roof: "#8a6a3c", wall: "#a08050", w: 2, h: 2 },
  ironmine:  { roof: "#4a5a6a", wall: "#807868", w: 2, h: 2 },
  pen:       { roof: "#8a7a4c", wall: "#b8a060", w: 2, h: 2 },
  ranch:     { roof: "#6a7a4c", wall: "#c0b080", w: 3, h: 3 },
  house:     { roof: "#a8503c", wall: "#c9a878", w: 2, h: 2 },
  granary:   { roof: "#d8a840", wall: "#d8c898", w: 2, h: 2 },
  storehouse:{ roof: "#7a7264", wall: "#a89878", w: 2, h: 2 },
  well:      { roof: "#8a8d80", wall: "#9da093", w: 1, h: 1 },
  cookhouse: { roof: "#8a5a3c", wall: "#b89868", w: 2, h: 2 },
  sawpit:    { roof: "#9a7a4a", wall: "#b8a078", w: 2, h: 2 },
  wharf:     { roof: "#6a8a9a", wall: "#b8a078", w: 1, h: 1 },
  lodge:     { roof: "#6a5a3c", wall: "#a08858", w: 2, h: 2 },
  herbgarden:{ roof: "#5a8a5a", wall: "#8aa878", w: 2, h: 2 },
  clinic:    { roof: "#c87878", wall: "#d8d0c0", w: 2, h: 2 },
  hall:      { roof: "#b8783c", wall: "#d0b888", w: 3, h: 3 },
  school:    { roof: "#5a7aa8", wall: "#d0c8a8", w: 2, h: 2 },
  farm:      { roof: "#8a6a3c", wall: "#9a7a4c", w: 3, h: 3 }
};

function makeBuildingTexture(key, state) {
  const s = BuildingStyle[key] || BuildingStyle.house;
  const W = s.w * TILE, H = s.h * TILE;
  const c = mkCanvas(W, H);
  const ctx = c.getContext("2d");

  if (state === 0 || state === 1) {
    ctx.strokeStyle = "#d8c890";
    ctx.setLineDash([4, 3]);
    ctx.strokeRect(1.5, 1.5, W - 3, H - 3);
    ctx.setLineDash([]);
    ctx.fillStyle = "rgba(200,190,150,0.18)";
    ctx.fillRect(2, 2, W - 4, H - 4);
    if (state === 1) {
      ctx.fillStyle = "#a08858";
      for (let x = 2; x < W - 3; x += 6) ctx.fillRect(x, 2, 2, H - 4);
      ctx.fillStyle = "#c9b888";
      ctx.fillRect(2, Math.floor(H / 2) - 1, W - 4, 3);
    }
    return c;
  }
  if (state === 3) {
    ctx.fillStyle = "rgba(40,36,30,0.55)";
    ctx.fillRect(2, 2, W - 4, H - 4);
    ctx.fillStyle = "#2a2622";
    ctx.fillRect(3, 3, W - 6, 3);
    return c;
  }

  if (key === "well") {
    ctx.fillStyle = "#8a8d80"; ctx.fillRect(4, 8, 8, 6);
    ctx.fillStyle = "#1d3a4a"; ctx.fillRect(6, 9, 4, 3);
    ctx.fillStyle = "#9da093"; ctx.fillRect(4, 7, 8, 2);
    ctx.fillStyle = "#6b4a2c"; ctx.fillRect(7, 3, 1, 4); ctx.fillRect(3, 3, 10, 1);
    return c;
  }
  if (key === "wharf") {
    ctx.fillStyle = "#b8a078";
    ctx.fillRect(0, 10, 16, 2);
    ctx.fillRect(2, 6, 10, 2);
    ctx.fillStyle = "#6a8a9a";
    ctx.fillRect(10, 6, 5, 6);
    ctx.fillStyle = "#8a5a3c";
    ctx.fillRect(4, 3, 1, 4); ctx.fillRect(4, 3, 6, 1);
    return c;
  }
  if (key === "herbgarden") {
    ctx.fillStyle = "#6a5a3c";
    ctx.fillRect(1, 1, W - 2, H - 2);
    ctx.fillStyle = "#7a6244";
    for (let y = 0; y < 3; y++) for (let x = 0; x < 3; x++)
      ctx.fillRect(3 + x * 6, 3 + y * 6, 5, 5);
    ctx.fillStyle = "#5aa06a";
    ctx.fillRect(4, 4, 3, 3); ctx.fillRect(10, 10, 3, 3); ctx.fillRect(16, 4, 3, 3);
    ctx.fillRect(22, 10, 3, 3); ctx.fillRect(10, 16, 3, 3);
    return c;
  }
  if (key === "farm") {
    ctx.fillStyle = "#8a6a44";
    ctx.fillRect(0, 0, W, H);
    ctx.fillStyle = "#7a5c3a";
    for (let y = 2; y < H; y += 5) ctx.fillRect(1, y, W - 2, 2);
    return c;
  }

  if (key === "villagecenter") {
    ctx.fillStyle = s.wall;
    ctx.fillRect(2, 2, W - 4, H - 4);
    ctx.fillStyle = "#6b4a2c";
    ctx.fillRect(4, 8, 2, 6); ctx.fillRect(10, 8, 2, 6);
    ctx.fillStyle = "#5a4028";
    ctx.fillRect(5, 9, 6, 2); ctx.fillRect(6, 11, 4, 2);
    ctx.fillStyle = "#e8a03c";
    ctx.fillRect(6, 13, 4, 2);
    ctx.fillStyle = "#ffd98a";
    ctx.fillRect(7, 12, 2, 1);
    ctx.fillStyle = "#d8c898";
    ctx.fillRect(2, 2, W - 4, 2);
    ctx.fillStyle = "#b09868";
    ctx.fillRect(W / 2 - 1, 4, 2, 4);
    ctx.fillStyle = "#e8ddc8";
    ctx.fillRect(W / 2 - 2, 3, 4, 2);
    return c;
  }

  const roofH = Math.floor(H * 0.45);
  ctx.fillStyle = s.wall;
  ctx.fillRect(3, roofH - 2, W - 6, H - roofH - 1);
  ctx.fillStyle = shade(s.wall, -24);
  ctx.fillRect(3, roofH - 2, W - 6, 2);
  ctx.fillStyle = s.roof;
  for (let i = 0; i < roofH; i++) {
    const inset = Math.abs(i - roofH / 2) < 1 ? 0 : Math.max(0, Math.floor((roofH / 2 - Math.abs(i - roofH / 2)) * 0.9));
    ctx.fillRect(1 + i * 0 + inset, i + 1, W - 2 - inset * 2, 1);
  }
  ctx.fillStyle = shade(s.roof, -28);
  ctx.fillRect(1, roofH - 1, W - 2, 2);
  ctx.fillStyle = "#4a3a28";
  ctx.fillRect(Math.floor(W / 2) - 2, H - 7, 4, 6);
  ctx.fillStyle = "#ffd98a";
  if (key === "cookhouse") {
    ctx.fillRect(Math.floor(W / 2) - 2, H - 7, 4, 4);
    ctx.fillStyle = "#787878";
    ctx.fillRect(W - 7, 3, 3, 5);
  } else if (key === "clinic") {
    ctx.fillRect(6, H - 9, 2, 2); ctx.fillRect(9, H - 9, 2, 2); ctx.fillRect(6, H - 6, 5, 2);
  } else if (key === "school") {
    ctx.fillRect(5, H - 10, 3, 3); ctx.fillRect(W - 8, H - 10, 3, 3);
  } else {
    ctx.fillRect(Math.floor(W / 2) - 2, H - 7, 4, 3);
  }
  if (key === "hall") {
    ctx.fillStyle = s.roof;
    ctx.fillRect(0, 0, W, 3);
    ctx.fillStyle = "#ffd98a";
    ctx.fillRect(2, 4, 3, 3); ctx.fillRect(W - 5, 4, 3, 3);
  }
  return c;
}

const CropStageColors = [null, "#5a8a3c", "#4a9a4c", "#d8b84a"];
function makeCropOverlay(stage) {
  if (stage === 0) return null;
  const c = mkCanvas(TILE, TILE);
  const ctx = c.getContext("2d");
  ctx.fillStyle = CropStageColors[stage] || "#4a9a4c";
  if (stage === 1) {
    for (let i = 0; i < 3; i++) ctx.fillRect(4 + i * 4, 11, 1, 3);
  } else if (stage === 2) {
    for (let i = 0; i < 3; i++) { ctx.fillRect(4 + i * 4, 8, 2, 6); ctx.fillRect(3 + i * 4, 10, 4, 2); }
  } else {
    for (let i = 0; i < 3; i++) {
      ctx.fillRect(4 + i * 4, 5, 2, 9);
      ctx.fillStyle = "#e8c85a"; ctx.fillRect(3 + i * 4, 4, 4, 3);
      ctx.fillStyle = "#d8b84a";
    }
  }
  return c;
}
