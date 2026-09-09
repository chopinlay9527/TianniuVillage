"use strict";

class WorldView {
  constructor(app) {
    this.app = app;
    this.mapW = 0;
    this.mapH = 0;
    this.tiles = null;
    this.chunkSprites = [];
    this.chunkCanvases = [];
    this.chunkRes = [];
    this.resById = new Map();
    this.worldRoot = null;
  }

  buildFromInit(msg) {
    this.mapW = msg.w;
    this.mapH = msg.h;
    const bytes = Uint8Array.from(atob(msg.tiles), ch => ch.charCodeAt(0));
    this.tiles = bytes;
    this.roadLevels = msg.roads
      ? Uint8Array.from(atob(msg.roads), ch => ch.charCodeAt(0))
      : new Uint8Array(this.mapW * this.mapH);

    const cx = Math.ceil(this.mapW / CHUNK);
    const cy = Math.ceil(this.mapH / CHUNK);
    this.cx = cx;
    this.cy = cy;
    this.chunkSprites = new Array(cx * cy);
    this.chunkCanvases = new Array(cx * cy);
    this.chunkRes = new Array(cx * cy);
    for (let i = 0; i < this.chunkRes.length; i++) this.chunkRes[i] = [];

    for (const r of msg.res) {
      this.resById.set(r.id, r);
      const ci = this.chunkIndex(r.x, r.y);
      if (ci >= 0) this.chunkRes[ci].push(r.id);
    }

    for (let cyy = 0; cyy < cy; cyy++) {
      for (let cxx = 0; cxx < cx; cxx++) {
        const idx = cyy * cx + cxx;
        const canvas = mkCanvas(CHUNK_PX, CHUNK_PX);
        this.chunkCanvases[idx] = canvas;
        this.redrawChunk(cxx, cyy);
        const tex = PIXI.Texture.from(canvas);
        const sprite = new PIXI.Sprite(tex);
        sprite.position.set(cxx * CHUNK_PX, cyy * CHUNK_PX);
        sprite.roundPixels = true;
        this.worldRoot.addChild(sprite);
        this.chunkSprites[idx] = sprite;
      }
    }
  }

  chunkIndex(x, y) {
    if (x < 0 || y < 0 || x >= this.mapW || y >= this.mapH) return -1;
    return Math.floor(y / CHUNK) * Math.ceil(this.mapW / CHUNK) + Math.floor(x / CHUNK);
  }

  redrawChunk(cxx, cyy) {
    const idx = cyy * this.cx + cxx;
    const ctx = this.chunkCanvases[idx].getContext("2d");
    ctx.clearRect(0, 0, CHUNK_PX, CHUNK_PX);
    const x0 = cxx * CHUNK, y0 = cyy * CHUNK;
    for (let ly = 0; ly < CHUNK; ly++) {
      for (let lx = 0; lx < CHUNK; lx++) {
        const wx = x0 + lx, wy = y0 + ly;
        if (wx >= this.mapW || wy >= this.mapH) continue;
        drawTerrainTile(ctx, this.tiles[wy * this.mapW + wx], wx, wy, lx * TILE, ly * TILE);
        if (this.roadLevels) {
          const rl = this.roadLevels[wy * this.mapW + wx];
          if (rl) drawRoad(ctx, rl, lx * TILE, ly * TILE, wx, wy);
        }
      }
    }
    for (const rid of this.chunkRes[idx]) {
      const r = this.resById.get(rid);
      if (!r) continue;
      drawResource(ctx, r, (r.x - x0) * TILE, (r.y - y0) * TILE);
    }
    if (this.chunkSprites[idx]) {
      this.chunkSprites[idx].texture.update();
    }
  }

  applyRoadDelta(list) {
    const dirty = new Set();
    for (const d of list) {
      if (d.i < 0 || d.i >= this.roadLevels.length) continue;
      this.roadLevels[d.i] = d.l;
      const cx = Math.floor((d.i % this.mapW) / CHUNK);
      const cy = Math.floor((d.i / this.mapW) / CHUNK);
      dirty.add(cy * this.cx + cx);
    }
    for (const idx of dirty) this.redrawChunk(idx % this.cx, Math.floor(idx / this.cx));
  }

  applyResourceDelta(list) {
    for (const d of list) {
      if (d.op === 2) {
        const old = this.resById.get(d.id);
        if (old) {
          const ci = this.chunkIndex(old.x, old.y);
          if (ci >= 0) {
            this.chunkRes[ci] = this.chunkRes[ci].filter(id => id !== d.id);
            this.redrawChunk(Math.floor(old.x / CHUNK), Math.floor(old.y / CHUNK));
          }
        }
        this.resById.delete(d.id);
      } else {
        const existing = this.resById.get(d.id);
        const r = d.op === 1 && existing ? Object.assign(existing, d) : d;
        this.resById.set(r.id, r);
        const ci = this.chunkIndex(r.x, r.y);
        if (ci >= 0 && existing) {
          const oldChunk = this.chunkRes.findIndex(arr => arr && arr.includes(r.id));
          if (oldChunk >= 0 && oldChunk !== ci) {
            this.chunkRes[oldChunk] = this.chunkRes[oldChunk].filter(id => id !== r.id);
            this.redrawChunk(oldChunk % this.cx, Math.floor(oldChunk / this.cx));
          } else {
            this.redrawChunk(Math.floor(r.x / CHUNK), Math.floor(r.y / CHUNK));
          }
        } else if (ci >= 0) {
          this.chunkRes[ci].push(r.id);
          this.redrawChunk(Math.floor(r.x / CHUNK), Math.floor(r.y / CHUNK));
        }
      }
    }
  }

  terrainAt(x, y) {
    if (x < 0 || y < 0 || x >= this.mapW || y >= this.mapH) return 0;
    return this.tiles[y * this.mapW + x];
  }

  drawMinimap(mmCtx, viewRect, villagers) {
    if (!this.mapW || !this.tiles) return;
    const mmW = mmCtx.canvas.width, mmH = mmCtx.canvas.height;
    const f = mmW / this.mapW;
    mmCtx.imageSmoothingEnabled = false;
    if (!this._mmCache || this._mmCache.width !== mmW || this._mmDirty) {
      const c = mkCanvas(mmW, mmH);
      const ctx = c.getContext("2d");
      const colors = ["#16324f", "#1d4e6b", "#d3c489", "#679b40", "#3d6a2c", "#8a8f68", "#55584c"];
      for (let y = 0; y < mmH; y++) {
        for (let x = 0; x < mmW; x++) {
          const wx = Math.min(this.mapW - 1, Math.floor(x / f));
          const wy = Math.min(this.mapH - 1, Math.floor(y / f));
          ctx.fillStyle = colors[this.terrainAt(wx, wy)];
          ctx.fillRect(x, y, 1, 1);
        }
      }
      this._mmCache = c;
      this._mmDirty = false;
    }
    mmCtx.drawImage(this._mmCache, 0, 0);
    mmCtx.fillStyle = "#ffe28a";
    for (const v of villagers) mmCtx.fillRect(v.x * f - 1, v.y * f - 1, 2, 2);
    mmCtx.strokeStyle = "#ffffffcc";
    mmCtx.strokeRect(viewRect.x * f, viewRect.y * f, viewRect.w * f, viewRect.h * f);
  }
}
