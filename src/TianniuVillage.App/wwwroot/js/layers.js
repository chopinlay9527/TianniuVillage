"use strict";

const VFW = 16, VFH = 16;

const ACT_ZH = {
  0: "发呆", 1: "散步", 2: "赶路", 3: "干活", 4: "吃饭", 5: "睡觉",
  6: "休息", 7: "聊天", 8: "上学", 9: "玩耍", 10: "养病", 11: "切磋", 12: "喝水", 13: "钻研"
};

const WEATHER_ZH = ["晴", "多云", "雨", "暴风雨", "雪", "雾"];
const SEASON_ZH = ["春", "夏", "秋", "冬"];

class VillagerLayer {
  constructor(app, parent) {
    this.container = new PIXI.Container();
    this.container.sortableChildren = true;
    parent.addChild(this.container);
    this.sprites = new Map();
    this.labels = new Map();
    this.carryLabels = new Map();
    this.hoverLabel = new PIXI.Text("", {
      fontFamily: "Microsoft YaHei", fontSize: 11, fill: 0xffffff,
      stroke: 0x000000, strokeThickness: 3
    });
    this.hoverLabel.anchor.set(0.5, 1);
    this.hoverLabel.visible = false;
    parent.addChild(this.hoverLabel);
    this.animTime = 0;
    this.lastSyncTime = 0;
  }

  sync(villagers) {
    const now = performance.now();
    const syncDt = this.lastSyncTime > 0 ? Math.min(500, now - this.lastSyncTime) : 200;
    this.lastSyncTime = now;

    const seen = new Set();
    for (const v of villagers) {
      seen.add(v.id);
      let s = this.sprites.get(v.id);

      let px, py;
      if (v.mv && v.prog > 0) {
        const p = v.prog;
        switch (v.facing) {
          case 0: px = (v.x + p + 0.5) * TILE; py = (v.y + 1) * TILE; break;
          case 1: px = (v.x - p + 0.5) * TILE; py = (v.y + 1) * TILE; break;
          case 2: px = (v.x + 0.5) * TILE; py = (v.y + p + 1) * TILE; break;
          case 3: px = (v.x + 0.5) * TILE; py = (v.y - p + 1) * TILE; break;
          default: px = (v.x + 0.5) * TILE; py = (v.y + 1) * TILE;
        }
      } else {
        px = (v.x + 0.5) * TILE;
        py = (v.y + 1) * TILE;
      }

      if (!s) {
        const tex = PIXI.Texture.from(makeVillagerTexture(v.sex, v.stage, v.id));
        s = new PIXI.Sprite(tex);
        s.anchor.set(0.5, 1);
        s.roundPixels = false;
        this.container.addChild(s);
        this.sprites.set(v.id, s);
        s.x = px; s.y = py;
        s._tx = px; s._ty = py;
        s._vx = 0; s._vy = 0;
        s._dir = v.facing || 0;
        s._moving = false;
        s.texture.frame = new PIXI.Rectangle(0, 0, VFW, VFH);
      } else {
        if (v.mv && syncDt > 0) {
          const dx = px - s._tx;
          const dy = py - s._ty;
          const dist = Math.hypot(dx, dy);
          if (dist > 0.5) {
            s._vx = dx / syncDt * 1000;
            s._vy = dy / syncDt * 1000;
          } else if (s._moving && Math.hypot(s._vx, s._vy) > 0.5) {
            // keep velocity — sub-tile progress, direction unchanged
          } else {
            s._vx *= 0.3; s._vy *= 0.3;
          }
        } else {
          s._vx *= 0.1; s._vy *= 0.1;
        }

        if (Math.hypot(px - s.x, py - s.y) > TILE * 8) {
          s.x = px; s.y = py;
          s._vx = 0; s._vy = 0;
        }
      }

      s._tx = px;
      s._ty = py;
      s._dir = v.facing || 0;
      s._moving = v.mv;
      s.zIndex = s.y / TILE;
      s.visible = !v.si;

      if (v.speech) {
        let lbl = this.labels.get(v.id);
        if (!lbl) {
          lbl = new PIXI.Text("", { fontFamily: "Microsoft YaHei", fontSize: 10, fill: 0xfff3c8, stroke: 0x000000, strokeThickness: 3 });
          lbl.anchor.set(0.5, 1);
          this.container.addChild(lbl);
          this.labels.set(v.id, lbl);
        }
        lbl.text = v.speech;
        lbl.position.set(s.x, s.y - 20);
        lbl.visible = true;
      } else {
        const lbl = this.labels.get(v.id);
        if (lbl) lbl.visible = false;
      }

      if (v.carry) {
        let cl = this.carryLabels.get(v.id);
        if (!cl) {
          cl = new PIXI.Text("", { fontFamily: "Microsoft YaHei", fontSize: 9, fill: 0x9ac8e8, stroke: 0x000000, strokeThickness: 2 });
          cl.anchor.set(0.5, 0);
          this.container.addChild(cl);
          this.carryLabels.set(v.id, cl);
        }
        cl.text = v.carry;
        cl.position.set(s.x, s.y - 2);
        cl.visible = true;
      } else {
        const cl = this.carryLabels.get(v.id);
        if (cl) cl.visible = false;
      }
    }
    for (const [id, s] of this.sprites) {
      if (!seen.has(id)) {
        s.destroy();
        this.sprites.delete(id);
        const lbl = this.labels.get(id);
        if (lbl) { lbl.destroy(); this.labels.delete(id); }
        const cl = this.carryLabels.get(id);
        if (cl) { cl.destroy(); this.carryLabels.delete(id); }
      }
    }
  }

  tick(dt) {
    this.animTime += dt;
    const dtSec = dt / 1000;
    const frame = Math.floor(this.animTime / 200) % 2;

    for (const [, s] of this.sprites) {
      if (s._moving) {
        s.x += s._vx * dtSec;
        s.y += s._vy * dtSec;

        const errX = s._tx - s.x;
        const errY = s._ty - s.y;
        s.x += errX * 0.05;
        s.y += errY * 0.05;

        if (s.texture) {
          const dir = s._dir || 0;
          // 帧块布局: [idle][walkA][walkB] 每块 4 方向；行走交替 walkA/walkB
          const fBlock = 1 + frame;
          s.texture.frame = new PIXI.Rectangle(dir * VFW + fBlock * VFW * 4, 0, VFW, VFH);
          s.texture.update();
        }
      } else {
        s.x += (s._tx - s.x) * 0.1;
        s.y += (s._ty - s.y) * 0.1;
        if (s.texture && Math.abs(s._tx - s.x) < 2) {
          s.texture.frame = new PIXI.Rectangle((s._dir || 0) * VFW, 0, VFW, VFH);
          s.texture.update();
        }
      }

      s.zIndex = s.y / TILE;
    }
  }

  findAt(wx, wy) {
    let best = null, bestD = 3;
    for (const [id, s] of this.sprites) {
      const d = Math.hypot(s.x / TILE - 0.5 - wx, s.y / TILE - 1 - wy);
      if (d < bestD) { bestD = d; best = id; }
    }
    return best;
  }
}

class AnimalLayer {
  constructor(parent) {
    this.container = new PIXI.Container();
    parent.addChild(this.container);
    this.sprites = new Map();
    this.textures = {
      deer: PIXI.Texture.from(makeAnimalTexture("deer")),
      rabbit: PIXI.Texture.from(makeAnimalTexture("rabbit"))
    };
  }

  sync(animals) {
    const seen = new Set();
    for (const a of animals) {
      seen.add(a.id);
      let s = this.sprites.get(a.id);
      const tx = (a.x + 0.5) * TILE;
      const ty = (a.y + 1) * TILE;
      if (!s) {
        s = new PIXI.Sprite(this.textures[a.k] || this.textures.rabbit);
        s.anchor.set(0.5, 1);
        s.alpha = 0.95;
        this.container.addChild(s);
        this.sprites.set(a.id, s);
        s.x = tx; s.y = ty;
      }
      s._tx = tx; s._ty = ty;
    }
    for (const [id, s] of this.sprites) {
      if (!seen.has(id)) { s.destroy(); this.sprites.delete(id); }
    }
  }

  tick(dt) {
    const dtSec = dt / 1000;
    for (const [, s] of this.sprites) {
      if (s._tx !== undefined) {
        s.x += (s._tx - s.x) * Math.min(1, dtSec * 4);
        s.y += (s._ty - s.y) * Math.min(1, dtSec * 4);
      }
    }
  }
}

class BuildingLayer {
  constructor(parent) {
    this.container = new PIXI.Container();
    parent.addChild(this.container);
    this.sprites = new Map();
    this.texCache = new Map();
  }

  tex(key, state) {
    const k2 = key + ":" + state;
    if (!this.texCache.has(k2)) this.texCache.set(k2, PIXI.Texture.from(makeBuildingTexture(key, state)));
    return this.texCache.get(k2);
  }

  sync(buildings) {
    const seen = new Set();
    for (const b of buildings) {
      seen.add(b.id);
      let s = this.sprites.get(b.id);
      if (!s) {
        s = new PIXI.Sprite(this.tex(b.k, b.state));
        s.position.set(b.x * TILE, b.y * TILE);
        s.roundPixels = true;
        this.container.addChild(s);
        this.sprites.set(b.id, s);
      }
      if (s._state !== b.state) {
        s._state = b.state;
        s.texture = this.tex(b.k, b.state);
        if (s._livestockSprites) { s._livestockSprites.forEach(x => x.destroy()); s._livestockSprites = null; s._livestockKey = null; }
        if (s._cropSprite) { s._cropSprite.destroy(); s._cropSprite = null; s._crop = null; }
      }
      if (b.k === "farm") {
        const growing = (b.crop || []).some(c => c === 2);
        const ripe = (b.crop || []).some(c => c === 3);
        const stage = ripe ? 3 : growing ? 2 : 1;
        if (s._crop !== stage) {
          s._crop = stage;
          const cov = makeCropOverlayTex(stage);
          if (s._cropSprite) { s._cropSprite.destroy(); s._cropSprite = null; }
          if (cov) {
            s._cropSprite = new PIXI.Sprite(cov);
            s._cropSprite.position.set(s.x, s.y);
            this.container.addChild(s._cropSprite);
          }
        }
      }
      if ((b.k === "pen" || b.k === "ranch") && b.lt && b.lc > 0) {
        const key2 = b.lt + ":" + b.lc;
        if (s._livestockKey !== key2) {
          s._livestockKey = key2;
          if (s._livestockSprites) { s._livestockSprites.forEach(x => x.destroy()); s._livestockSprites = null; }
          s._livestockSprites = [];
          const style = BuildingStyle[b.k] || { w: 2, h: 2 };
          const cap = Math.min(b.lc, Math.max(4, style.w * style.h));
          for (let i = 0; i < cap; i++) {
            const tex2 = PIXI.Texture.from(makeLivestockTexture(b.lt, b.id * 31 + i * 17));
            const sp = new PIXI.Sprite(tex2);
            sp.anchor.set(0.5, 1);
            sp.alpha = 0.98;
            const col = i % style.w;
            const row = Math.floor(i / style.w) % 3;
            const jx = (((b.id * 13 + i * 31) % 60) / 100) - 0.3;
            sp.position.set((b.x + col + 0.5 + jx) * TILE, (b.y + style.h - 0.15 - row * 0.34) * TILE);
            this.container.addChild(sp);
            s._livestockSprites.push(sp);
          }
        }
      } else if (s._livestockKey) {
        s._livestockKey = null;
        if (s._livestockSprites) { s._livestockSprites.forEach(x => x.destroy()); s._livestockSprites = null; }
      }
    }
    for (const [id, s] of this.sprites) {
      if (!seen.has(id)) {
        if (s._cropSprite) s._cropSprite.destroy();
        if (s._livestockSprites) { s._livestockSprites.forEach(x => x.destroy()); s._livestockSprites = null; }
        s.destroy();
        this.sprites.delete(id);
      }
    }
  }
}

function makeCropOverlayTex(stage) {
  if (stage <= 0) return null;
  const c = mkCanvas(TILE, TILE);
  const ctx = c.getContext("2d");
  ctx.fillStyle = ["", "#5a8a3c", "#4a9a4c", "#d8b84a"][stage];
  for (let i = 0; i < 3; i++) {
    const x = 3 + i * 5;
    if (stage === 1) ctx.fillRect(x, 11, 2, 3);
    else if (stage === 2) { ctx.fillRect(x, 7, 2, 7); ctx.fillRect(x - 1, 9, 4, 2); }
    else { ctx.fillRect(x, 4, 2, 10); ctx.fillStyle = "#e8c85a"; ctx.fillRect(x - 1, 3, 4, 3); ctx.fillStyle = "#d8b84a"; }
  }
  return PIXI.Texture.from(c);
}

class CloudLayer {
  constructor(app) {
    this.app = app;
    this.container = new PIXI.Container();
    this.container.alpha = 0.12;
    this.clouds = [];
    this.windX = 0.008;
    this.windY = 0.002;
    this.weatherMode = 0;
    this.worldW = 256 * 16;
    this.worldH = 256 * 16;
  }
  setWeather(w) {
    this.weatherMode = w;
    this.container.alpha = w === 1 ? 0.10 : w === 2 ? 0.18 : w === 3 ? 0.28 : w === 4 ? 0.22 : 0.12;
    this.windX = w === 3 ? 0.02 : 0.008;
  }

  spawnClouds() {
    for (let i = 0; i < 10; i++) {
      const g = new PIXI.Graphics();
      const puffs = 4 + Math.floor(Math.random() * 4);
      const baseR = 80 + Math.random() * 140;
      const cx = Math.random() * this.worldW;
      const cy = Math.random() * this.worldH;

      for (let p = 0; p < puffs; p++) {
        const px = cx + (Math.random() - 0.5) * baseR * 2.5;
        const py = cy + (Math.random() - 0.5) * baseR * 0.8;
        const pr = baseR * (0.4 + Math.random() * 0.6);
        g.beginFill(0xf0f4f8, 0.6 + Math.random() * 0.3);
        g.drawEllipse(px, py, pr * 1.4, pr * 0.6);
        g.endFill();
      }

      g._vx = this.windX * (0.5 + Math.random());
      g._vy = this.windY * (Math.random() - 0.3);
      g._w = baseR * 3;
      this.container.addChild(g);
      this.clouds.push(g);
    }
  }

  tick(dt) {
    for (const c of this.clouds) {
      c.x += c._vx * dt;
      c.y += c._vy * dt;
      if (c.x > this.worldW + c._w) c.x = -c._w;
      if (c.x < -c._w * 2) c.x = this.worldW + c._w;
      if (c.y > this.worldH + 200) c.y = -200;
      if (c.y < -200) c.y = this.worldH + 200;
    }
  }
}

class WeatherLayer {
  constructor(app) {
    this.app = app;
    this.particles = [];
    this.container = new PIXI.Container();
    this.mode = -1;
    app.stage.addChild(this.container);
  }

  setWeather(w) {
    const target = w === 2 ? 60 : w === 3 ? 120 : w === 4 ? 90 : 0;
    this.mode = w;
    while (this.particles.length < target) {
      const g = new PIXI.Graphics();
      if (w === 4) g.beginFill(0xffffff, 0.85).drawCircle(0, 0, 1.5).endFill();
      else g.beginFill(0x9ac8e8, 0.55).drawRect(0, 0, 1, 7).endFill();
      g.x = Math.random() * this.app.screen.width;
      g.y = Math.random() * this.app.screen.height;
      g._spd = 4 + Math.random() * 4;
      g._drift = w === 4 ? (Math.random() - 0.5) * 0.6 : 1.5;
      this.container.addChild(g);
      this.particles.push(g);
    }
    while (this.particles.length > target) {
      const p = this.particles.pop();
      p.destroy();
    }
  }

  tick() {
    if (this.mode < 0) return;
    const w = this.app.screen.width, h = this.app.screen.height;
    for (const p of this.particles) {
      p.y += p._spd;
      p.x += p._drift;
      if (p.y > h) { p.y = -8; p.x = Math.random() * w; }
      if (p.x > w) p.x = -2;
      if (p.x < -4) p.x = w;
    }
  }
}
