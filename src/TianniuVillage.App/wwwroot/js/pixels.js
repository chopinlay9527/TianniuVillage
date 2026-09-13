"use strict";

const TILE = 32;          // LPC 32px 规格
const LEGACY = 16;        // 旧 16px 素材的基准单位
const CHUNK = 16;
const CHUNK_PX = TILE * CHUNK;

// 旧 16px 基准画布 → 当前规格 2× 放大（最近邻，像素风格清晰）
function up2(c) {
  const out = mkCanvas(c.width * 2, c.height * 2);
  out.getContext("2d").imageSmoothingEnabled = false;
  out.getContext("2d").drawImage(c, 0, 0, c.width, c.height, 0, 0, out.width, out.height);
  return out;
}

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

// ===== 官方 LPC Wang Set 驱动的地形系统（OGA-BY 3.0 / CC-BY 3.0，32px）=====
// 元数据来源(权威): assets/lpc/official/*.tsx
//   → tools/TianniuVillage.TileSetImport → assets/lpc/terrain_profile.js
// Tiled 语义: wangid 角位取值是 **1 基颜色 ID**，0 = 无颜色(未约束)；
//   角位顺序 [TR, BR, BL, TL]（对应 wangid 下标 1,3,5,7）

const SEASON_SHEET = ["spring", "summer", "autumn", "winter"];
let currentSeason = 0;
function setSeason(idx) { currentSeason = idx; }
function lpcSeasonName() { return SEASON_SHEET[currentSeason]; }
// 地形枚举 → TEX.terrain 键
const TERRAIN_KEY = { 0: "deepWater", 1: "shallowWater", 2: "sand", 3: "grass", 4: "forest", 5: "highland", 6: "mountain" };

// --- 权威 profile 访问层 ---
function getProfile() { return (typeof window !== "undefined" && window.TERRAIN_PROFILE) || null; }
let COLOR_ID_BY_NAME = null;
function ensureProfileDerived() {
  if (COLOR_ID_BY_NAME) return;
  COLOR_ID_BY_NAME = {};
  const P = getProfile();
  if (P) for (const c of P.colors) COLOR_ID_BY_NAME[c.name] = c.id;
}
// 游戏地形 → 官方色名。山按作者模型并入 Dirt（官方 Mountain 色只有透明覆盖件、零过渡画），
// 以 TEX.terrain.mountain.tint 罩色区分；森林同理 = Grass + 罩色
const TERRAIN_COLOR_NAME = {
  0: "Deep Water", 1: "Shallow Water", 2: "Sand", 3: "Grass", 4: "Grass", 5: "Dirt", 6: "Dirt"
};
// 权威基底 tile: 取自 profile 的"四角同色键"候选，并经像素实测确认
const BASE_TILE_BY_COLOR_NAME = {
  "Grass": 65, "Sand": 74, "Dirt": 195, "Light Till": 205, "Deep Till": 526,
  "Shallow Water": 1153, "Deep Water": 1985, "Melted Ice": 1425, "Deep Sand": 267,
  "Grassy Mountain": 402, "Vine": 724,
  "Mountain": 195,   // 官方该色仅有透明覆盖件；合成时退回 Dirt 基底
};
// 色号优先级（高优先级侧承载过渡；深水只接浅水，故水族最低）
const WANG_PRIORITY_BY_NAME = {
  "Deep Water": 1, "Melted Ice": 1, "Shallow Water": 2, "Sand": 3, "Deep Sand": 3,
  "Grass": 4, "Dirt": 5, "Mountain": 5,
};
function terrainColor(terrain) { ensureProfileDerived(); return COLOR_ID_BY_NAME[TERRAIN_COLOR_NAME[terrain]] ?? 1; }
function baseTileOf(terrain) { return BASE_TILE_BY_COLOR_NAME[TERRAIN_COLOR_NAME[terrain]] ?? 65; }
function baseTileOfColor(colorId) {
  const P = getProfile(); if (!P) return 65;
  const c = P.colors.find(x => x.id === colorId);
  return (c && BASE_TILE_BY_COLOR_NAME[c.name]) || 65;
}
function priorityOfColor(colorId) {
  const P = getProfile(); if (!P) return 3;
  const c = P.colors.find(x => x.id === colorId);
  return (c && WANG_PRIORITY_BY_NAME[c.name]) ?? 3;
}
// 当前季节的角位表（官方秋季为旧版布局，单列覆盖表）
function activeCornerTable() {
  const P = getProfile(); if (!P) return null;
  const by = P.cornerTableBySeason;
  return (by && by[lpcSeasonName()]) || P.cornerTable;
}

const lpcCellCache = new Map();
function lpcCell(id) {
  if (lpcCellCache.has(id)) return lpcCellCache.get(id);
  const out = mkCanvas(TILE, TILE);
  const sheet = LpcSheets[lpcSeasonName()];
  if (sheet) {
    const ctx = out.getContext("2d");
    ctx.drawImage(sheet, (id % 64) * TILE, Math.floor(id / 64) * TILE, TILE, TILE, 0, 0, TILE, TILE);
  }
  lpcCellCache.set(id, out);
  return out;
}
// 旋转 tile（补齐缺失键位/缺失画风: 净边过渡 tile 旋转后即得其余方向）
const lpcCellRotCache = new Map();
function lpcCellRot(id, rot) {
  if (!rot) return lpcCell(id);
  const k = id + ":" + rot;
  if (lpcCellRotCache.has(k)) return lpcCellRotCache.get(k);
  const out = mkCanvas(TILE, TILE);
  const ctx = out.getContext("2d");
  ctx.translate(TILE / 2, TILE / 2);
  ctx.rotate(rot * Math.PI / 2);
  ctx.drawImage(lpcCell(id), -TILE / 2, -TILE / 2);
  lpcCellRotCache.set(k, out);
  return out;
}
function swapSeason(idx) {
  if (idx === currentSeason) return;
  currentSeason = idx;
  lpcCellCache.clear();
  lpcCellRotCache.clear();
  edgePickCache.clear();
  bestTileCache.clear();
  baseStripCache = null;
}

// ===== 过渡贴图变体优选 =====
// 同一角点 key 在表里可能对应多种画风的 tile（实测: 1794/1731 一族朝水边是净纯水,
// 1857/1792 一族朝水边画成渐变滩——与纯水基底相邻会浮出一条 114 色差的水线）。
// 候选 = 本键 id + 旋转匹配(色 6≡8 等价)的其他键 id，按"两端角同色的边条 ≈ 该色基底"
// 打分选最一致者；净边旋转变体可补齐混合画风留下的缺口
let edgePickCache = new Map();
let baseStripCache = null;
function edgeStripOf(canvas, side) {
  const box = side === "N" ? [0, 0, TILE, 2] : side === "S" ? [0, TILE - 2, TILE, 2]
    : side === "W" ? [0, 0, 2, TILE] : [TILE - 2, 0, 2, TILE];
  const d = canvas.getContext("2d").getImageData(box[0], box[1], box[2], box[3]).data;
  let r = 0, g = 0, b = 0, n = 0;
  for (let i = 0; i < d.length; i += 4) { if (d[i + 3] > 10) { r += d[i]; g += d[i + 1]; b += d[i + 2]; n++; } }
  return [r / Math.max(1, n), g / Math.max(1, n), b / Math.max(1, n)];
}
function baseStrips() {
  if (baseStripCache) return baseStripCache;
  baseStripCache = {};
  const P = getProfile();
  if (P) {
    for (const c of P.colors) {
      baseStripCache[c.id] = null;   // 延迟到使用时按基色计算
    }
  }
  return baseStripCache;
}
function baseStripsOf(colorId) {
  const all = baseStrips();
  if (all[colorId]) return all[colorId];
  const cell = lpcCell(baseTileOfColor(colorId));
  all[colorId] = { N: edgeStripOf(cell, "N"), S: edgeStripOf(cell, "S"), E: edgeStripOf(cell, "E"), W: edgeStripOf(cell, "W") };
  return all[colorId];
}
// 每条边拆成两半, 各贴一个角: N 左半=TL 右半=TR; S 左=BL 右=BR; W 上=TL 下=BL; E 上=TR 下=BR
function halfStrips(canvas) {
  const g = canvas.getContext("2d");
  const mean = (x, y, w, h) => {
    const d = g.getImageData(x, y, w, h).data;
    let r = 0, gg = 0, b = 0, n = 0;
    for (let i = 0; i < d.length; i += 4) { if (d[i + 3] > 10) { r += d[i]; gg += d[i + 1]; b += d[i + 2]; n++; } }
    return [r / Math.max(1, n), gg / Math.max(1, n), b / Math.max(1, n)];
  };
  const H = TILE / 2;
  return {
    NL: mean(0, 0, H, 2), NR: mean(H, 0, H, 2),
    SL: mean(0, TILE - 2, H, 2), SR: mean(H, TILE - 2, H, 2),
    WT: mean(0, 0, 2, H), WB: mean(0, H, 2, H),
    ET: mean(TILE - 2, 0, 2, H), EB: mean(TILE - 2, H, 2, H),
  };
}
const colorDist = (a, b) => Math.hypot(a[0] - b[0], a[1] - b[1], a[2] - b[2]);
const rotKeyOnce = k => { const c = k.split(","); return c[3] + "," + c[0] + "," + c[1] + "," + c[2]; };

function pickTileForCorners(corners, own, table) {
  const key = corners.join(",");
  if (edgePickCache.has(key)) return edgePickCache.get(key);
  const cands = [];
  if (table[key]) for (const id of table[key]) cands.push({ id, rot: 0 });
  // 旋转补位: 作者缺失的角位方向可由同族其他键旋转得到
  for (const K of Object.keys(table)) {
    let rk = K;
    for (let r = 1; r < 4; r++) {
      rk = rotKeyOnce(rk);
      if (rk === key) { for (const id of table[K]) cands.push({ id, rot: r }); break; }
    }
  }
  let best = null, bestD = Infinity;
  // 每个角楔贴两条半边, 半边颜色应接近该角颜色族的基底（这样角部画风也被打分）
  const cornerHalf = [["NR", "ET"], ["SR", "EB"], ["SL", "WB"], ["NL", "WT"]]; // TR,BR,BL,TL
  const sideOf = { N: "N", S: "S", E: "E", W: "W", NR: "N", NL: "N", SR: "S", SL: "S", ET: "E", EB: "E", WT: "W", WB: "W" };
  for (const c of cands) {
    let d = c.rot * 0.5;
    const hs = halfStrips(lpcCellRot(c.id, c.rot));
    for (let i = 0; i < 4; i++) {
      const base = baseStripsOf(corners[i]);
      if (!base) continue;
      for (const h of cornerHalf[i]) d += colorDist(hs[h], base[sideOf[h]]);
    }
    if (d < bestD) { bestD = d; best = c; }
  }
  if (!best && table[key] && table[key].length) best = { id: table[key][0], rot: 0 };
  edgePickCache.set(key, best);
  return best;
}

// 权威角位表（来自 terrain_profile.js，由官方 tsx 生成）
function getWangTable() {
  const ct = activeCornerTable();
  return ct ? { cornerTable: ct } : null;
}
// Tiled WangFiller 式最佳匹配选块：表里没有精确角位组合（三色交点等）时，
// 对全表按角点重合数评分取最优 tile，任何配置都有解。
// 规则: 角点吻合 自身色+3 / 外来色+2，非吻合但含自身色+1（本格中心应是自身地形）；
// 候选限定含自身色的键；纯色键不参与（内部格由基底直出）；
// 官方表中 0 = 无颜色(未约束)，仅作低分兜底匹配
const bestTileCache = new Map();
function bestMatchTileIds(corners, own, table) {
  const cacheKey = corners.join(",") + "|" + own;
  if (bestTileCache.has(cacheKey)) return bestTileCache.get(cacheKey);
  let bestWithOwn = null, bestScore = -1;
  for (const k of Object.keys(table)) {
    const c = k.split(",").map(Number);
    if (c[0] === c[1] && c[1] === c[2] && c[2] === c[3]) continue;
    if (!c.includes(own)) continue;
    let score = 0;
    for (let i = 0; i < 4; i++) {
      if (c[i] === 0) score += 0;                        // 未约束角: 不加分
      else if (c[i] === corners[i]) score += corners[i] === own ? 3 : 2;
      else if (c[i] === own) score += 1;
    }
    if (score > bestScore) { bestScore = score; bestWithOwn = table[k]; }
  }
  bestTileCache.set(cacheKey, bestWithOwn);
  return bestWithOwn;
}

// 内角圆弧印章（全表像素扫描发现，未收录于 wang 表）：
// 165/171/229/235 = 透明底水色扇形楔（任意基底可用）；279/280 = 草底泥色扇形楔。
// 内角（对角接触）格用"自身基底+印章"替代表内小三角楔，与外角弧形岸线风格统一。
// 键为官方色号: 6=Shallow Water, 7=Deep Water, 8=Melted Ice, 3=Dirt
const CORNER_STAMPS = {
  6: { id: 165, corner: 3 },   // 水楔: 165 的扇形在 TL
  7: { id: 165, corner: 3 },
  8: { id: 165, corner: 3 },
  3: { id: 279, corner: 2 },   // 泥楔(草底): 279 的扇形在 BL
};
// 把印章旋转到目标角（角序 0TR 1BR 2BL 3TL; 顺时针90°: idx+1）
function stampFor(cornerIdx, foreign, own) {
  const st = CORNER_STAMPS[foreign];
  if (!st) return null;
  if (foreign === 3 && own !== 1) return null;   // 泥楔印章是草底，仅草(Grass=1)可用
  const rot = (cornerIdx - st.corner + 4) % 4;
  // corner: 绘制时只取扇形所在象限（印章其他象限可能有作者画的杂色装饰，一并裁掉）
  return { id: st.id, rot, corner: cornerIdx };
}

// 直角拐角合成贴图（三外色角 = 拐角格被水三面包围）:
// 素材里这组 tile 的自色残部仅 1-2%（"几乎全淹"画风），没有可见沙角。
// 合成 = 自身基底纹理 + 外色基底纹理按象限对角切割 + 2px 抖动边，
// 对角边界(如 TR 角: (16,0)→(32,16))与两侧岸线的半格水线精确衔接
const diagQuarterCache = new Map();
function diagQuarterTile(terrain, foreign, ownCornerIdx) {
  const key = terrain + ":" + foreign + ":" + ownCornerIdx + ":" + lpcSeasonName();
  if (diagQuarterCache.has(key)) return diagQuarterCache.get(key);
  const out = mkCanvas(TILE, TILE);
  const ctx = out.getContext("2d");
  ctx.drawImage(lpcCell(baseTileOf(terrain)), 0, 0);
  const fBase = lpcCell(baseTileOfColor(foreign)).getContext("2d").getImageData(0, 0, TILE, TILE).data;
  const img = ctx.getImageData(0, 0, TILE, TILE);
  // own 象限边界线: TR: x-y=16(own: >) / BR: x+y=48(own: >) / BL: y-x=16(own: >) / TL: x+y=16(own: <)
  const side = (x, y) => {
    if (ownCornerIdx === 0) return x - y - 16;
    if (ownCornerIdx === 1) return x + y - 48;
    if (ownCornerIdx === 2) return y - x - 16;
    return 16 - x - y;
  };
  for (let y = 0; y < TILE; y++) {
    for (let x = 0; x < TILE; x++) {
      const s = side(x, y);
      if (s > 1) continue;                                  // own 象限保留
      const dither = s > -2 && ((x + y) % 2 === 0);          // 边界 2px 抖动
      if (s <= -2 || dither) {
        const i = (y * TILE + x) * 4;
        img.data[i] = fBase[i];
        img.data[i + 1] = fBase[i + 1];
        img.data[i + 2] = fBase[i + 2];
        img.data[i + 3] = 255;
      }
    }
  }
  ctx.putImageData(img, 0, 0);
  diagQuarterCache.set(key, out);
  return out;
}

// 合成圆弧角贴图（表内无对应画风的角位）: 自身基底 + 外色基底按四分之一圆切入 + 抖动边
const synthDiscCache = new Map();
function synthDiscTile(terrain, foreign, cornerIdxs, radius) {
  const key = terrain + ":" + foreign + ":" + cornerIdxs.join("") + ":" + radius + ":" + lpcSeasonName();
  if (synthDiscCache.has(key)) return synthDiscCache.get(key);
  const out = mkCanvas(TILE, TILE);
  const ctx = out.getContext("2d");
  ctx.drawImage(lpcCell(baseTileOf(terrain)), 0, 0);
  const fBase = lpcCell(baseTileOfColor(foreign)).getContext("2d").getImageData(0, 0, TILE, TILE).data;
  const img = ctx.getImageData(0, 0, TILE, TILE);
  const cx = [TILE, TILE, 0, 0], cy = [0, TILE, TILE, 0];   // 角点坐标 TR,BR,BL,TL
  for (let y = 0; y < TILE; y++) {
    for (let x = 0; x < TILE; x++) {
      for (const ci of cornerIdxs) {
        const d = Math.hypot(x + 0.5 - cx[ci], y + 0.5 - cy[ci]);
        const rim = Math.abs(d - radius) < 2 && ((x + y) % 2 === 0);
        if (d < radius - 2 || rim) {
          const i = (y * TILE + x) * 4;
          img.data[i] = fBase[i]; img.data[i + 1] = fBase[i + 1]; img.data[i + 2] = fBase[i + 2]; img.data[i + 3] = 255;
          break;
        }
      }
    }
  }
  ctx.putImageData(img, 0, 0);
  synthDiscCache.set(key, out);
  return out;
}

function wangLookup(tx, ty, tiles, mapW, mapH) {
  const WT = getWangTable();
  if (!WT) return null;
  const idx = ty * mapW + tx;
  const terrain = tiles[idx];
  const own = terrainColor(terrain);
  function at(x, y) {
    if (x < 0 || y < 0 || x >= mapW || y >= mapH) return own;
    return terrainColor(tiles[y * mapW + x]);
  }
  const N = at(tx, ty - 1), NE = at(tx + 1, ty - 1), E = at(tx + 1, ty);
  const SE = at(tx + 1, ty + 1), S = at(tx, ty + 1), SW = at(tx - 1, ty + 1), W = at(tx - 1, ty), NW = at(tx - 1, ty - 1);
  // 角点级联（对角优先）+ 优先级过滤（只承载更低优先级地形的过渡）。
  // 对角优先让凹角（海湾/山坳内角）的对角格带上小角贴图完成圆角；
  // 对角渗漏问题由优先级过滤根治（低优先级侧恒为纯色，如水格永远纯水）
  const cornerOf = (d, a, b) => {
    const c = d !== own ? d : (a !== own ? a : (b !== own ? b : own));
    if (c !== own && priorityOfColor(c) > priorityOfColor(own)) return own;
    return c;
  };
  const TR = cornerOf(NE, N, E);
  const BR = cornerOf(SE, S, E);
  const BL = cornerOf(SW, S, W);
  const TL = cornerOf(NW, N, W);
  const corners = [TR, BR, BL, TL];

  // 纯内部（四角同色）→ 用已验证基底
  if (TR === own && BR === own && BL === own && TL === own) {
    return { id: baseTileOf(terrain), rot: 0 };
  }

  // 内角（仅一个外色角 = 对角接触）→ 自身基底 + 扇形印章，与外角弧线风格统一。
  // 对角棋盘角（两对角同色外色，拼接表无对应贴图，近似选块会把整格画成异色）
  // → 基底 + 双对角印章
  const foreignIdx = [0, 1, 2, 3].filter(i => corners[i] !== own);
  const sameForeign = foreignIdx.length > 0 &&
    foreignIdx.every(i => corners[i] === corners[foreignIdx[0]]);
  if (foreignIdx.length === 1) {
    const st = stampFor(foreignIdx[0], corners[foreignIdx[0]], own);
    if (st) return { id: baseTileOf(terrain), rot: 0, stamps: [st] };
    return { synthDisc: { terrain, foreign: corners[foreignIdx[0]], corners: [foreignIdx[0]], radius: 14 } };
  }
  if (foreignIdx.length === 2 && Math.abs(foreignIdx[0] - foreignIdx[1]) === 2 && sameForeign) {
    const st1 = stampFor(foreignIdx[0], corners[foreignIdx[0]], own);
    const st2 = stampFor(foreignIdx[1], corners[foreignIdx[1]], own);
    if (st1 && st2) return { id: baseTileOf(terrain), rot: 0, stamps: [st1, st2] };
    return { synthDisc: { terrain, foreign: corners[foreignIdx[0]], corners: foreignIdx, radius: 16 } };
  }
  if (foreignIdx.length === 3 && sameForeign) {
    // 直角拐角（三面环外色）: 合成象限对角切割，保留可见的自身色角
    return { diagQuarter: { foreign: corners[foreignIdx[0]], ownCorner: [0, 1, 2, 3].find(i => corners[i] === own) }, terrain };
  }
  if (foreignIdx.length === 4 && sameForeign) {
    return { synthDisc: { terrain, foreign: corners[0], corners: [0, 1, 2, 3], radius: 14 } };
  }

  // 过渡区 → 变体优选（精确键 + 旋转补位，按边条一致性打分）；无候选再全表最佳匹配
  const key = corners.join(",");
  if (corners.includes(own) || WT.cornerTable[key]) {
    const pick = pickTileForCorners(corners, own, WT.cornerTable);
    if (pick) return pick;
  }
  const ids = bestMatchTileIds(corners, own, WT.cornerTable);
  if (ids && ids.length > 0) return { id: ids[(tx * 31 + ty * 57) % ids.length], rot: 0 };
  return { id: baseTileOf(terrain), rot: 0 };
}

function drawTerrainTile(ctx, terrain, tx, ty, px, py, tiles, mapW, mapH) {
  const entry = TEX.terrain[TERRAIN_KEY[terrain]];
  if (entry) {
    let drawn = false;
    if (LpcSheets[lpcSeasonName()]) {
      const spec = wangLookup(tx, ty, tiles, mapW, mapH);
      if (spec && (spec.id >= 0 || spec.diagQuarter || spec.synthDisc)) {
        if (spec.diagQuarter) {
          ctx.drawImage(diagQuarterTile(spec.terrain, spec.diagQuarter.foreign, spec.diagQuarter.ownCorner), px, py);
        } else if (spec.synthDisc) {
          const sd = spec.synthDisc;
          ctx.drawImage(synthDiscTile(sd.terrain, sd.foreign, sd.corners, sd.radius), px, py);
        } else {
          ctx.drawImage(lpcCellRot(spec.id, spec.rot), px, py);
        }
        if (spec.stamps) {
          // 只绘制扇形所在的 16×16 象限，印章其余象限的杂色装饰一并裁掉
          for (const st of spec.stamps) {
            const qx = st.corner === 0 || st.corner === 1 ? TILE / 2 : 0;
            const qy = st.corner === 0 || st.corner === 3 ? 0 : TILE / 2;
            ctx.drawImage(lpcCellRot(st.id, st.rot), qx, qy, TILE / 2, TILE / 2, px + qx, py + qy, TILE / 2, TILE / 2);
          }
        }
        if (entry.tint) { ctx.fillStyle = entry.tint; ctx.fillRect(px, py, TILE, TILE); }
        drawn = true;
      }
    }
    if (!drawn && entry.color) {
      ctx.fillStyle = entry.color;
      ctx.fillRect(px, py, TILE, TILE);
      drawn = true;
    }
    if (drawn) {
      if (entry.noise) terrainNoise(ctx, entry, tx, ty, px, py);
      if (entry.waves) waterWaves(ctx, tx, ty, px, py);
      return;
    }
  }
  // 回退程序化基底
  const pal = TerrainColors[terrain] || TerrainColors[3];
  for (let y = 0; y < TILE; y++) {
    for (let x = 0; x < TILE; x++) {
      const r = hash01(tx * TILE + x, ty * TILE + y, 7);
      ctx.fillStyle = r < 0.16 ? pal[1] : r > 0.86 ? pal[2] : pal[0];
      ctx.fillRect(px + x, py + y, 1, 1);
    }
  }
}

// 地形杂色(32px)
function terrainNoise(ctx, entry, tx, ty, px, py) {
  const a = entry.noise;
  for (let i = 0; i < 14; i++) {
    const rx = Math.floor(hash01(tx, ty, 900 + i * 2) * (TILE - 4));
    const ry = Math.floor(hash01(ty, tx, 951 + i * 2) * (TILE - 4));
    ctx.fillStyle = i % 2 ? "rgba(255,255,255," + a + ")" : "rgba(0,0,0," + (a * 0.9).toFixed(3) + ")";
    ctx.fillRect(px + rx, py + ry, 2, 2);
  }
}

// 水波纹(32px)
function waterWaves(ctx, tx, ty, px, py) {
  const y1 = 4 + Math.floor(hash01(tx, ty, 700) * 22);
  const y2 = 14 + Math.floor(hash01(ty, tx, 701) * 22);
  ctx.fillStyle = "rgba(230,245,252,0.35)";
  ctx.fillRect(px + Math.floor(hash01(tx, ty, 702) * 20), py + y1, 8, 2);
  ctx.fillRect(px + Math.floor(hash01(ty, tx, 703) * 22), py + y2, 6, 2);
  if (hash01(tx, ty, 704) > 0.55) {
    ctx.fillStyle = "rgba(255,255,255,0.5)";
    ctx.fillRect(px + Math.floor(hash01(tx, ty, 705) * 28), py + Math.floor(hash01(ty, tx, 706) * 28), 2, 2);
  }
}

// ===== Tiny 物件小表（遗留：树/灌木等物件在 Phase 3 前继续使用）=====
const ATLAS_COLS = 12, ATLAS_FARM_ROW = 11;
const atlasCellCache = new Map();
function atlasCell(id) {
  if (atlasCellCache.has(id)) return atlasCellCache.get(id);
  const out = mkCanvas(LEGACY, LEGACY);
  const m = /^([tf])(\d+)$/.exec(String(id));
  if (m && Atlas.img) {
    const n = parseInt(m[2], 10);
    const row = (m[1] === "t" ? 0 : ATLAS_FARM_ROW) + Math.floor(n / ATLAS_COLS);
    const col = n % ATLAS_COLS;
    out.getContext("2d").drawImage(Atlas.img, col * LEGACY, row * LEGACY, LEGACY, LEGACY, 0, 0, LEGACY, LEGACY);
  }
  atlasCellCache.set(id, out);
  return out;
}

// 双格树：Tiny Town 的树 = 树冠格(上) + 树干格(下) 垂直拼接，合成 16x32
const treePairCache = new Map();
function atlasTreePair(topId, botId) {
  const key = topId + "+" + botId;
  if (treePairCache.has(key)) return treePairCache.get(key);
  const c = mkCanvas(16, 32);
  const ctx = c.getContext("2d");
  ctx.drawImage(atlasCell(topId), 0, 0, 16, 16, 0, 0, 16, 16);
  ctx.drawImage(atlasCell(botId), 0, 0, 16, 16, 0, 16, 16, 16);
  treePairCache.set(key, c);
  return c;
}

function drawTree(ctx, px, py, seed) {
  if (Atlas.img && TEX.tree && TEX.tree.tiles.length) {
    const entry = TEX.tree.tiles[seed % TEX.tree.tiles.length];
    ctx.fillStyle = TEX.tree.shadow;
    if (Array.isArray(entry)) {
      // 双格树：树干格在地面格，树冠向上伸出 16px
      ctx.fillRect(px + 3, py + 12, 10, 2);
      ctx.drawImage(atlasTreePair(entry[0], entry[1]), 0, 0, 16, 32, px, py - 16, 16, 32);
    } else {
      ctx.fillRect(px + 2, py + 13, 12, 2);
      ctx.drawImage(atlasCell(entry), 0, 0, 16, 16, px, py, 16, 16);
    }
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
  if (Atlas.img && TEX.bush) {
    ctx.fillStyle = TEX.bush.shadow;
    ctx.fillRect(px + 3, py + 13, 10, 2);
    ctx.drawImage(atlasCell(hasBerries ? TEX.bush.withBerries : TEX.bush.empty), 0, 0, 16, 16, px, py, 16, 16);
    if (hasBerries) {
      ctx.fillStyle = TEX.bush.berryColor;
      ctx.fillRect(px + 6, py + 8, 2, 2);
      ctx.fillRect(px + 9, py + 10, 2, 2);
    }
    return;
  }
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

function drawMushroom(ctx, px, py, seed) {
  const m = TEX.mushroom || {};
  if (m.tiles && m.tiles.length && Atlas.img) {
    ctx.fillStyle = "rgba(30,60,25,0.2)";
    ctx.fillRect(px + 4, py + 13, 8, 2);
    ctx.drawImage(atlasCell(m.tiles[(seed || 0) % m.tiles.length]), 0, 0, 16, 16, px, py, 16, 16);
    return;
  }
  ctx.fillStyle = m.stem || "#e8ddc8";
  ctx.fillRect(px + 6, py + 11, 3, 3);
  ctx.fillStyle = m.cap || "#b8503c";
  ctx.fillRect(px + 4, py + 8, 7, 3);
  ctx.fillStyle = m.dot || "#e8ddc8";
  ctx.fillRect(px + 6, py + 8, 1, 1);
  ctx.fillRect(px + 9, py + 9, 1, 1);
}

function drawStone(ctx, px, py, seed) {
  const c = TEX.stone || {};
  ctx.fillStyle = c.base || "#8a8d80";
  ctx.fillRect(px + 3, py + 6, 10, 8);
  ctx.fillStyle = c.light || "#9da093";
  ctx.fillRect(px + 4, py + 5, 7, 4);
  ctx.fillStyle = c.dark || "#767970";
  ctx.fillRect(px + 3, py + 12, 10, 2);
  if (hash01(px, py, seed) > 0.5) {
    ctx.fillStyle = c.base || "#8a8d80";
    ctx.fillRect(px + 11, py + 9, 4, 5);
  }
}

function drawHerb(ctx, px, py) {
  if (Atlas.img && TEX.herb) {
    ctx.drawImage(atlasCell(TEX.herb.tile), 0, 0, 16, 16, px, py, 16, 16);
    return;
  }
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
  const c = TEX.fishSpot || {};
  ctx.fillStyle = c.shadow || "rgba(40,70,60,0.5)";
  ctx.fillRect(px + 3, py + 6, 10, 6);
  ctx.fillStyle = c.fish || "#5a8a7a";
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

  const rc = (TEX.road || {})[level] || {};
  const base = rc.base || "#c9b088";
  const edge = rc.edge || "#b9a078";
  const topBand = rc.top || edge;

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
  const speckle = rc.speckle || "#d8c298";
  if (level === 1) {
    for (let i = 0; i < 3; i++) {
      const rx = Math.floor(hash01(wx, wy, 400 + i) * 12);
      const ry = Math.floor(hash01(wy, wx, 500 + i) * 9);
      ctx.fillStyle = speckle;
      ctx.fillRect(px + 2 + rx, py + 4 + ry, 2, 1);
    }
  } else if (level === 2) {
    for (let i = 0; i < 5; i++) {
      const rx = Math.floor(hash01(wx, wy, 600 + i) * 12);
      const ry = Math.floor(hash01(wy, wx, 700 + i) * 9);
      const sp = Array.isArray(speckle) ? speckle[i % speckle.length] : speckle;
      ctx.fillStyle = i % 2 ? (Array.isArray(speckle) ? sp : edge) : sp;
      ctx.fillRect(px + 2 + rx, py + 4 + ry, 2, 2);
    }
  } else if (level === 3) {
    ctx.fillStyle = rc.joint || "#9d998e";
    ctx.fillRect(px + 7, py + y0, 1, bh);
    ctx.fillRect(px + x0, py + 7, bw, 1);
    ctx.fillStyle = rc.detail || "#a8a49a";
    ctx.fillRect(px + 3, py + 4, 3, 2);
    ctx.fillRect(px + 10, py + 9, 3, 2);
  }
}

// 资源种类编号必须与 C# Core/Enums.cs 的 ResKind 顺序一致（ResView 下发 (int)Kind）
const ResKind = {
  Tree: 0, BerryBush: 1, Mushroom: 2, Stone: 3, Herb: 4, FishSpot: 5,
  WaterSpot: 6, FlaxPatch: 7, CopperVein: 8, IronVein: 9,
};

function drawResource(ctx, r, px, py) {
  switch (r.k) {
    case ResKind.Tree: drawTree(ctx, px, py, r.id); break;
    case ResKind.BerryBush: drawBush(ctx, px, py, r.berries); break;
    case ResKind.Mushroom: drawMushroom(ctx, px, py, r.id); break;
    case ResKind.Stone: drawStone(ctx, px, py, r.id); break;
    case ResKind.Herb: drawHerb(ctx, px, py); break;
    case ResKind.FlaxPatch:
      if (Atlas.img && TEX.flax) {
        ctx.drawImage(atlasCell(TEX.flax.tile), 0, 0, 16, 16, px, py, 16, 16);
        ctx.fillStyle = TEX.flax.tint;
        ctx.fillRect(px + 3, py + 5, 10, 9);
        ctx.fillStyle = TEX.flax.flowerColor;
        ctx.fillRect(px + 5, py + 4, 2, 2);
        ctx.fillRect(px + 9, py + 3, 2, 2);
        ctx.fillRect(px + 12, py + 6, 2, 2);
      } else {
        ctx.fillStyle = "#5a8a5a";
        ctx.fillRect(px + 3, py + 8, 10, 6);
        ctx.fillStyle = "#8aa8d0";
        ctx.fillRect(px + 4, py + 5, 2, 4);
        ctx.fillRect(px + 8, py + 4, 2, 5);
        ctx.fillRect(px + 11, py + 6, 2, 3);
        ctx.fillStyle = "#b8c8e8";
        ctx.fillRect(px + 4, py + 4, 2, 1);
        ctx.fillRect(px + 8, py + 3, 2, 1);
      }
      break;
    case ResKind.FishSpot: drawFishSpot(ctx, px, py); break;
    case ResKind.WaterSpot:
      ctx.fillStyle = (TEX.waterSpot || {}).ripple || "rgba(190,230,240,0.5)";
      ctx.fillRect(px + 4, py + 6, 8, 1);
      ctx.fillRect(px + 6, py + 10, 8, 1);
      ctx.fillRect(px + 3, py + 13, 6, 1);
      break;
    case ResKind.CopperVein: {
      const c = TEX.copperVein || {};
      ctx.fillStyle = c.rock || "#8a7a5c";
      ctx.fillRect(px + 2, py + 5, 12, 9);
      ctx.fillStyle = c.ore || "#c4854a";
      ctx.fillRect(px + 4, py + 7, 3, 2);
      ctx.fillRect(px + 8, py + 9, 4, 2);
      ctx.fillStyle = c.oreLight || "#e0a050";
      ctx.fillRect(px + 5, py + 8, 1, 1);
      ctx.fillRect(px + 9, py + 10, 1, 1);
      break;
    }
    case ResKind.IronVein: {
      const c = TEX.ironVein || {};
      ctx.fillStyle = c.rock || "#8a7a5c";
      ctx.fillRect(px + 2, py + 5, 12, 9);
      ctx.fillStyle = c.ore || "#8a9aaa";
      ctx.fillRect(px + 4, py + 7, 3, 2);
      ctx.fillRect(px + 8, py + 9, 4, 2);
      ctx.fillStyle = c.oreLight || "#b0c0d0";
      ctx.fillRect(px + 5, py + 8, 1, 1);
      ctx.fillRect(px + 9, py + 10, 1, 1);
      break;
    }
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

function rgbaFromHex(hex, alpha) {
  const n = parseInt(String(hex).replace("#", ""), 16);
  if (isNaN(n)) return "rgba(160,120,80," + alpha + ")";
  return `rgba(${(n >> 16) & 255},${(n >> 8) & 255},${n & 255},${alpha})`;
}

function makeBuildingTexture(key, state) {
  const s = BuildingStyle[key] || BuildingStyle.house;
  // 建筑纹理统一在 16px 基准网格绘制（坐标均为 16 时代参数），
  // 由 BuildingLayer 精灵 scale=2 呈现为 32px/格，避免 TILE=32 双倍错位
  const W = s.w * LEGACY, H = s.h * LEGACY;
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
    // 农田：LPC 耕土底 + 犁沟（若四季表就绪）
    const soil = LpcSheets[lpcSeasonName()] ? [2763, 2761, 2635, 2633, 2629] : null;
    if (soil) {
      for (let ty = 0; ty < s.h; ty++)
        for (let tx = 0; tx < s.w; tx++) {
          const id = soil[(tx + ty * 3) % soil.length];
          ctx.drawImage(lpcCell(id), tx * LEGACY, ty * LEGACY);
        }
      ctx.fillStyle = "rgba(70,45,25,0.45)";
      for (let y = 4; y < H; y += 8) ctx.fillRect(0, y, W, 2);
    } else {
      ctx.fillStyle = "#8a6a44";
      ctx.fillRect(0, 0, W, H);
      ctx.fillStyle = "#7a5c3a";
      for (let y = 4; y < H; y += 8) ctx.fillRect(0, y, W, 2);
    }
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
  if (Atlas.img && TEX.building) {
    // 拼接纹理：atlas 木板墙 + 砖红屋顶，叠各建筑主题色罩
    const wallC = atlasCell(TEX.building.wall);
    const roofC = atlasCell(TEX.building.roof);
    for (let ty = 0; ty < s.h; ty++)
      for (let tx = 0; tx < s.w; tx++)
        ctx.drawImage(wallC, 0, 0, LEGACY, LEGACY, tx * LEGACY, ty * LEGACY, LEGACY, LEGACY);
    ctx.fillStyle = rgbaFromHex(s.wall, 0.45);
    ctx.fillRect(0, 0, W, H);
    const roofPx = s.h === 1 ? 7 : Math.max(LEGACY, Math.round(H * 0.45));
    for (let y = 0; y < roofPx; y += LEGACY)
      for (let x = 0; x < W; x += LEGACY) {
        const hh = Math.min(LEGACY, roofPx - y);
        ctx.drawImage(roofC, 0, 0, LEGACY, hh, x, y, LEGACY, hh);
      }
    ctx.fillStyle = rgbaFromHex(s.roof, 0.5);
    ctx.fillRect(0, 0, W, roofPx);
    ctx.fillStyle = shade(s.roof, -40);
    ctx.fillRect(0, roofPx - 2, W, 2);
    ctx.strokeStyle = "rgba(30,22,14,0.45)";
    ctx.strokeRect(0.5, 0.5, W - 1, H - 1);
  } else {
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
  }
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

const CropStageColors = [null, null, "#6a9a4a", "#3f8a3a", "#d8b84a"];
function makeCropOverlay(stage) {
  if (stage <= 1) return null;
  const c = mkCanvas(LEGACY, LEGACY);
  const ctx = c.getContext("2d");
  ctx.fillStyle = CropStageColors[stage] || "#3f8a3a";
  if (stage === 2) {
    // 出苗：嫩绿小芽
    ctx.fillRect(5, 9, 1, 5); ctx.fillRect(9, 9, 1, 5);
    ctx.fillStyle = "#8fc06a";
    ctx.fillRect(4, 8, 3, 2); ctx.fillRect(8, 8, 3, 2);
  } else if (stage === 3) {
    // 抽穗：成株
    for (let i = 0; i < 4; i++) {
      const x = 3 + i * 3;
      ctx.fillRect(x, 4, 2, 11);
      ctx.fillStyle = "#5a9a44"; ctx.fillRect(x - 1, 6, 4, 2); ctx.fillStyle = "#3f8a3a";
    }
  } else {
    // 成熟：金穗
    for (let i = 0; i < 4; i++) {
      const x = 3 + i * 3;
      ctx.fillRect(x, 3, 2, 11);
      ctx.fillStyle = "#e8c85a"; ctx.fillRect(x - 1, 2, 4, 3);
      ctx.fillStyle = "#d8b84a";
    }
  }
  return c;
}
