"use strict";

let app, gameView, camera, villagerLayer, animalLayer, buildingLayer, weatherLayer, cloudLayer;
let selectionRing, lightingFilter;
let worldRoot, villagerRoot;

const AMBIENT_CURVE = [
  { h: 0,    r: 0.05, g: 0.07, b: 0.14 },
  { h: 4.5,  r: 0.05, g: 0.07, b: 0.14 },
  { h: 6,    r: 0.20, g: 0.25, b: 0.40 },
  { h: 7,    r: 0.65, g: 0.55, b: 0.42 },
  { h: 9,    r: 1.0,  g: 0.97, b: 0.92 },
  { h: 15,   r: 1.0,  g: 0.97, b: 0.92 },
  { h: 17,   r: 0.95, g: 0.85, b: 0.70 },
  { h: 18.5, r: 0.85, g: 0.60, b: 0.40 },
  { h: 19.5, r: 0.40, g: 0.25, b: 0.25 },
  { h: 21,   r: 0.05, g: 0.07, b: 0.14 },
  { h: 24,   r: 0.05, g: 0.07, b: 0.14 }
];

function ambientAt(hour) {
  let lo = AMBIENT_CURVE[0], hi = AMBIENT_CURVE[AMBIENT_CURVE.length - 1];
  for (let i = 0; i < AMBIENT_CURVE.length - 1; i++) {
    if (hour >= AMBIENT_CURVE[i].h && hour <= AMBIENT_CURVE[i + 1].h) {
      lo = AMBIENT_CURVE[i]; hi = AMBIENT_CURVE[i + 1]; break;
    }
  }
  const t = hi.h === lo.h ? 0 : (hour - lo.h) / (hi.h - lo.h);
  return { r: lo.r + (hi.r - lo.r) * t, g: lo.g + (hi.g - lo.g) * t, b: lo.b + (hi.b - lo.b) * t };
}

window.addEventListener("load", () => {
  app = new PIXI.Application({
    view: document.getElementById("stage"),
    resizeTo: window,
    antialias: false,
    backgroundColor: 0x0a140e
  });

  worldRoot = new PIXI.Container();
  app.stage.addChild(worldRoot);

  villagerRoot = new PIXI.Container();
  app.stage.addChild(villagerRoot);

  cloudLayer = new CloudLayer(app);
  cloudLayer.spawnClouds();
  worldRoot.addChild(cloudLayer.container);

  weatherLayer = new WeatherLayer(app);

  selectionRing = new PIXI.Graphics();
  villagerRoot.addChild(selectionRing);

  gameView = new WorldView(app);
  gameView.worldRoot = worldRoot;
  window.gameView = gameView;
  camera = new Camera(app);
  window.camera = camera;
  villagerLayer = new VillagerLayer(app, villagerRoot);
  window.villagerLayer = villagerLayer;
  animalLayer = new AnimalLayer(worldRoot);
  buildingLayer = new BuildingLayer(worldRoot);

  lightingFilter = new LightingFilter();
  worldRoot.filters = [lightingFilter];

  UI.init();
  setupInput();
  connectHost();

  window.addEventListener("resize", () => {
    lightingFilter.setResolution(app.screen.width, app.screen.height);
  });

  app.ticker.add((dt) => {
    const dtMs = dt / 60 * 1000;
    villagerLayer.tick(dtMs);
    animalLayer.tick(dtMs);
    camera.tick();
    cloudLayer.tick(dtMs);
    weatherLayer.tick();
    updateLighting();
  });
});

function connectHost() {
  if (!window.chrome || !window.chrome.webview) {
    document.getElementById("loading").textContent = "请在甜牛村桌面程序中运行";
    return;
  }
  window.chrome.webview.addEventListener("message", (e) => {
    const msg = e.data;
    if (msg.type === "init") onInit(msg);
    else if (msg.type === "update") onUpdate(msg);
  });
  window.chrome.webview.postMessage(JSON.stringify({ type: "ready" }));
}

function onInit(msg) {
  lastLogSeq = Math.max(0, ...(msg.logs || []).map(l => l.seq));
  gameView.buildFromInit(msg);
  const vc = msg.buildings.find(b => b.k === "villagecenter");
  gameView.settleCenter = vc ? { x: vc.x + 1, y: vc.y + 1 } : { x: msg.w >> 1, y: msg.h >> 1 };
  worldRoot.addChild(buildingLayer.container);
  worldRoot.addChild(animalLayer.container);
  villagerRoot.addChild(villagerLayer.container);
  camera.clampToMap(gameView.mapW * TILE, gameView.mapH * TILE);
  const anchor = msg.villagers.length > 0
    ? msg.villagers[0] : { x: gameView.mapW / 2, y: gameView.mapH / 2 };
  camera.centerOn(anchor.x * TILE, anchor.y * TILE);
  buildingLayer.sync(msg.buildings);
  villagerLayer.sync(msg.villagers);
  animalLayer.sync(msg.animals || []);
  if (msg.villagers.length > 0) camera.follow(msg.villagers[0].id);
  UI.lastStats = msg.stats;
  UI.updateStats(msg.stats);
  UI.updateVillagers(msg.villagers);
  UI.addLogs(msg.logs);
  UI.drawMinimap(gameView, camera);
  weatherLayer.setWeather(msg.stats.weather);
  cloudLayer.setWeather(msg.stats.weather);
  document.getElementById("loading").classList.add("hidden");
  gameView._mmDirty = true;
}

let lastLogSeq = 0;
function onUpdate(msg) {
  if (!gameView.mapW) return;
  villagerLayer.sync(msg.villagers);
  buildingLayer.sync(msg.buildings);
  animalLayer.sync(msg.animals || []);
  if (msg.res && msg.res.length) { gameView.applyResourceDelta(msg.res); gameView._mmDirty = true; }
  if (msg.road && msg.road.length) { gameView.applyRoadDelta(msg.road); gameView._mmDirty = true; }
  UI.lastStats = msg.stats;
  UI.updateStats(msg.stats);
  UI.updateVillagers(msg.villagers);
  if (msg.logs && msg.logs.length) {
    const fresh = msg.logs.filter(l => l.seq > lastLogSeq);
    if (fresh.length) { UI.addLogs(fresh); lastLogSeq = Math.max(lastLogSeq, ...fresh.map(l => l.seq)); }
  }
  if (msg.msgs && msg.msgs.length) UI.addSocialMsgs(msg.msgs);
  const fesEl = document.getElementById("ti-festival");
  if (fesEl) fesEl.classList.toggle("hidden", !msg.festival);
  const merEl = document.getElementById("ti-merchant");
  if (merEl) merEl.classList.toggle("hidden", !msg.merchant);
  weatherLayer.setWeather(msg.stats.weather);
  cloudLayer.setWeather(msg.stats.weather);
  UI.drawMinimap(gameView, camera);
}

function updateLighting() {
  const stats = UI.lastStats;
  if (!stats || !lightingFilter) return;

  const hour = stats.minute / 60;
  const amb = ambientAt(hour);
  const time = performance.now() * 0.001;

  const weatherDim = stats.weather === 3 ? 0.4 : stats.weather === 2 ? 0.15 : stats.weather === 4 ? 0.12 : 0;
  const dimR = Math.max(0.02, amb.r * (1 - weatherDim));
  const dimG = Math.max(0.02, amb.g * (1 - weatherDim));
  const dimB = Math.max(0.04, amb.b * (1 - weatherDim * 0.5));

  lightingFilter.setAmbient(dimR, dimG, dimB);
  lightingFilter.setCamera(camera.worldX, camera.worldY, camera.scale);
  lightingFilter.setResolution(app.screen.width, app.screen.height);
  lightingFilter.setTime(time);
  lightingFilter.clearLights();

  const flickerSlow = Math.sin(time * 6.7) * 0.08 + Math.sin(time * 11.3) * 0.05;
  const flickerFast = f => 1 + Math.sin(time * 9.7 + f * 3.7) * 0.12;
  const nightF = Math.max(0.1, amb.r < 0.5 ? 1 : 0);

  if (gameView.settleCenter) {
    const vc = gameView.settleCenter;
    lightingFilter.addLight(
      (vc.x + 0.5) * TILE, (vc.y + 0.5) * TILE, 8 * TILE,
      1.0, 0.65, 0.25, 0.5 * (1 + flickerSlow) * nightF
    );
  }

  if (buildingLayer) {
    for (const [, s] of buildingLayer.sprites) {
      if (s._state === 2 && s.width > 0) {
        lightingFilter.addLight(
          s.x + s.width / 2, s.y + s.height / 2, 2.5 * TILE,
          1.0, 0.85, 0.55, 0.18 * nightF
        );
      }
    }
  }

  if (villagerLayer) {
    for (const [, s] of villagerLayer.sprites) {
      if (s._moving) {
        const fl = flickerFast(s.x);
        lightingFilter.addLight(
          s.x, s.y - 8, 3.5 * TILE,
          1.0, 0.6, 0.2, 0.25 * fl * nightF
        );
      }
    }
  }
}

function setupInput() {
  const stage = document.getElementById("stage");
  let dragging = false, lastX = 0, lastY = 0, moved = 0;

  stage.addEventListener("pointerdown", (e) => {
    dragging = true; moved = 0;
    lastX = e.clientX; lastY = e.clientY;
    stage.classList.add("dragging");
    stage.setPointerCapture(e.pointerId);
  });
  stage.addEventListener("pointermove", (e) => {
    if (!dragging) {
      const wx = (e.clientX - camera.worldX) / camera.scale / TILE;
      const wy = (e.clientY - camera.worldY) / camera.scale / TILE;
      const id = villagerLayer.findAt(wx, wy);
      if (id != null) {
        const s = villagerLayer.sprites.get(id);
        villagerLayer.hoverLabel.position.set(s.x, s.y - 22);
        villagerLayer.hoverLabel.text = UI.lastVillagers.find(v => v.id === id)?.n || "";
        villagerLayer.hoverLabel.visible = true;
      } else {
        villagerLayer.hoverLabel.visible = false;
      }
      return;
    }
    const dx = e.clientX - lastX, dy = e.clientY - lastY;
    moved += Math.abs(dx) + Math.abs(dy);
    camera.panBy(dx, dy);
    lastX = e.clientX; lastY = e.clientY;
  });
  stage.addEventListener("pointerup", (e) => {
    dragging = false;
    stage.classList.remove("dragging");
    if (moved < 5) {
      const wx = (e.clientX - camera.worldX) / camera.scale / TILE;
      const wy = (e.clientY - camera.worldY) / camera.scale / TILE;
      const id = villagerLayer.findAt(wx, wy);
      if (id != null) {
        UI.selected = id;
        camera.follow(id);
        const v = UI.lastVillagers.find(x => x.id === id);
        if (v) UI.showDetail(v);
        UI.updateVillagers(UI.lastVillagers);
      } else {
        UI.selected = null;
        document.getElementById("detail-card").classList.add("hidden");
        camera.followId = null;
        UI.updateVillagers(UI.lastVillagers);
      }
    }
  });
  stage.addEventListener("wheel", (e) => {
    e.preventDefault();
    const factor = e.deltaY < 0 ? 1.15 : 1 / 1.15;
    camera.zoomAt(e.clientX, e.clientY, factor);
  }, { passive: false });

  window.addEventListener("keydown", (e) => {
    if (e.target.tagName === "INPUT") return;
    if (e.key === "+" || e.key === "=") camera.zoomAt(window.innerWidth / 2, window.innerHeight / 2, 1.2);
    if (e.key === "-") camera.zoomAt(window.innerWidth / 2, window.innerHeight / 2, 1 / 1.2);
    if (e.code === "Space") { e.preventDefault(); UI.$("btn-pause").click(); }
    const num = parseInt(e.key);
    if (num >= 1 && num <= 5) UI.setSpeed(num);
  });
}

class Camera {
  constructor(app) {
    this.app = app;
    this.scale = 1.6;
    this.worldX = 0;
    this.worldY = 0;
    this.followId = null;
    worldRoot.scale.set(this.scale);
    villagerRoot.scale.set(this.scale);
  }

  clampToMap(pxW, pxH) { this.mapW = pxW; this.mapH = pxH; }
  follow(id) { this.followId = id; }

  centerOn(wx, wy) {
    this.worldX = this.app.screen.width / 2 - wx * this.scale;
    this.worldY = this.app.screen.height / 2 - wy * this.scale;
    this.clamp();
  }

  panBy(dx, dy) {
    this.followId = null;
    this.worldX += dx;
    this.worldY += dy;
    this.clamp();
  }

  zoomAt(sx, sy, factor) {
    const ns = Math.min(6, Math.max(0.4, this.scale * factor));
    const k = ns / this.scale;
    this.worldX = sx - (sx - this.worldX) * k;
    this.worldY = sy - (sy - this.worldY) * k;
    this.scale = ns;
    worldRoot.scale.set(ns);
    villagerRoot.scale.set(ns);
    this.clamp();
  }

  tick() {
    if (this.followId != null) {
      const s = villagerLayer.sprites.get(this.followId);
      if (s) {
        const tx = this.app.screen.width / 2 - s.x * this.scale;
        const ty = this.app.screen.height / 2 - s.y * this.scale;
        this.worldX += (tx - this.worldX) * 0.08;
        this.worldY += (ty - this.worldY) * 0.08;
      }
    }
    this.clamp();
    worldRoot.position.set(this.worldX, this.worldY);
    villagerRoot.position.set(this.worldX, this.worldY);
    drawSelection();
  }

  clamp() {
    const vw = this.app.screen.width / this.scale;
    const vh = this.app.screen.height / this.scale;
    const mw = (this.mapW || 0) * TILE;
    const mh = (this.mapH || 0) * TILE;
    if (mw > vw) this.worldX = Math.min(0, Math.max(-(mw - vw), this.worldX));
    else this.worldX = -(vw - mw) / 2;
    if (mh > vh) this.worldY = Math.min(0, Math.max(-(mh - vh), this.worldY));
    else this.worldY = -(vh - mh) / 2;
  }
}

function drawSelection() {
  selectionRing.clear();
  if (UI.selected == null) return;
  const s = villagerLayer.sprites.get(UI.selected);
  if (!s) return;
  selectionRing.lineStyle(1.5, 0xffd98a, 0.95);
  const r = 11 + Math.sin(performance.now() / 220) * 1.5;
  selectionRing.drawCircle(s.x, s.y - 8, r);
}
