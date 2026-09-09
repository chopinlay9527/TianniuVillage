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

function drawTerrainTile(ctx, terrain, tx, ty, px, py) {
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

function drawTree(ctx, px, py, seed) {
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
  const c = mkCanvas(12, 10);
  const ctx = c.getContext("2d");
  if (kind === "deer") {
    ctx.fillStyle = "#a4744a";
    ctx.fillRect(2, 3, 7, 4);
    ctx.fillRect(8, 2, 3, 3);
    ctx.fillStyle = "#8a5f3c";
    ctx.fillRect(2, 6, 6, 2);
    ctx.fillRect(3, 8, 1, 2); ctx.fillRect(6, 8, 1, 2);
    ctx.fillStyle = "#d8c8a8";
    ctx.fillRect(9, 1, 2, 1);
    ctx.fillStyle = "#4a3a28";
    ctx.fillRect(5, 3, 1, 1);
  } else {
    ctx.fillStyle = "#c9c2b4";
    ctx.fillRect(3, 4, 6, 4);
    ctx.fillRect(8, 3, 3, 3);
    ctx.fillRect(9, 1, 1, 2);
    ctx.fillStyle = "#efe9dd";
    ctx.fillRect(3, 7, 5, 2);
    ctx.fillStyle = "#3a3428";
    ctx.fillRect(10, 4, 1, 1);
  }
  return c;
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
