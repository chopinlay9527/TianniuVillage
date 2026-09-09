"use strict";

const MSG_ICONS = ["💬", "🗣", "🍵", "⚡", "❤️"];

const UI = {
  lastVillagers: [],
  selected: null,
  lastSocialSeq: 0,

  init() {
    this.$ = (id) => document.getElementById(id);
    this.mmCtx = this.$("minimap").getContext("2d");
    this.searchText = "";
    this._lastSpeed = 1;

    this.$("v-search").addEventListener("input", (e) => {
      this.searchText = e.target.value.trim();
      this.filterVillagerList();
    });

    this.$("villager-list").addEventListener("click", (e) => {
      const row = e.target.closest(".v-row");
      if (!row) return;
      const id = parseInt(row.dataset.id);
      if (isNaN(id)) return;
      this.selected = id;
      if (window.camera) window.camera.follow(id);
      const v = this.lastVillagers.find(x => x.id === id);
      if (v) this.showDetail(v);
      this.filterVillagerList();
    });

    document.querySelectorAll(".tb-btn.spd").forEach(btn => {
      btn.addEventListener("click", () => this.setSpeed(parseInt(btn.dataset.spd)));
    });
    this.$("btn-pause").addEventListener("click", () => {
      const paused = this.$("btn-pause").classList.contains("paused");
      this.setSpeed(paused ? (this._lastSpeed || 1) : 0);
    });

    this.$("btn-newgame").addEventListener("click", () => {
      if (confirm("确定要创建新世界吗？当前进度将丢失。")) this.sendCmd({ type: "newgame" });
    });
    this.$("btn-save").addEventListener("click", () => this.sendCmd({ type: "save" }));
    this.$("btn-load").addEventListener("click", () => this.sendCmd({ type: "load" }));

    document.querySelectorAll(".god-btn").forEach(btn => {
      btn.addEventListener("click", () => this.sendCmd({ type: "god", action: btn.dataset.god }));
    });

    this.$("minimap").addEventListener("click", (e) => {
      const rect = e.target.getBoundingClientRect();
      const fx = (e.clientX - rect.left) / rect.width;
      const fy = (e.clientY - rect.top) / rect.height;
      if (window.gameView && window.camera) {
        window.camera.centerOn(fx * window.gameView.mapW * TILE, fy * window.gameView.mapH * TILE);
      }
    });

    document.querySelectorAll(".ftab").forEach(btn => {
      btn.addEventListener("click", () => {
        document.querySelectorAll(".ftab").forEach(b => b.classList.remove("active"));
        document.querySelectorAll(".tabpane").forEach(p => p.classList.remove("active"));
        btn.classList.add("active");
        this.$(btn.dataset.tab + "-list").classList.add("active");
      });
    });
  },

  sendCmd(cmd) {
    if (window.chrome && window.chrome.webview)
      window.chrome.webview.postMessage(JSON.stringify(cmd));
  },

  setSpeed(mult) {
    if (mult > 0) this._lastSpeed = mult;
    const pauseBtn = this.$("btn-pause");
    const paused = mult <= 0;
    pauseBtn.textContent = paused ? "▶" : "⏸";
    pauseBtn.classList.toggle("paused", paused);
    document.querySelectorAll(".tb-btn.spd").forEach(btn => {
      btn.classList.toggle("active", parseInt(btn.dataset.spd) === mult);
    });
    this.sendCmd({ type: "speed", value: mult });
  },

  filterVillagerList() {
    const q = this.searchText;
    document.querySelectorAll("#villager-list .v-row").forEach(row => {
      row.style.display = !q || row.dataset.name.includes(q) ? "" : "none";
    });
  },

  updateStats(s) {
    const set = (id, v) => { this.$(id).textContent = v; };
    const season = SEASON_ZH[s.season] || "—";
    const dayOfYear = (s.day % 120) + 1;
    const dayOfSeason = (s.day % 30) + 1;
    set("ti-date", `第 ${s.year} 年 ${season}季 第 ${dayOfSeason} 天`);
    const hh = String(Math.floor(s.minute / 60)).padStart(2, "0");
    const mm = String(s.minute % 60).padStart(2, "0");
    set("ti-clock", `${hh}:${mm}`);
    set("ti-season", season + "季");
    set("ti-weather", WEATHER_ZH[s.weather] || "—");
    const torchEl = this.$("ti-torch");
    if (torchEl) torchEl.classList.toggle("hidden", !s.torchLit);

    set("s-pop", `${s.pop} 成${s.adults}/童${s.children}`);
    set("s-happy", `${Math.round(s.happiness)}`);
    const healthEl = this.$("s-health");
    healthEl.textContent = `${Math.round(s.health)}`;
    healthEl.classList.toggle("warn", s.health < 70);
    set("s-storage", `${s.storageUsed}/${s.storageCap}`);
    set("s-births", s.births);     set("s-deaths", s.deaths);
    set("s-chicken", s.chickens || 0); set("s-sheep", s.sheep || 0); set("s-pig", s.pigs || 0);

    set("s-food", s.food); set("s-meal", s.meal); set("s-grain", s.grain);
    set("s-water", s.water);
    set("s-log", s.logs); set("s-plank", s.planks); set("s-stone", s.stone); set("s-herb", s.herb);
    set("s-hide", s.hide); set("s-cloth", s.cloth); set("s-clothes", s.clothes);

    const techEl = this.$("s-tech");
    const techBar = this.$("b-tech");
    if (s.techName) {
      techEl.textContent = `${s.techName} ${s.techProgress}%`;
      techBar.style.width = `${Math.max(2, s.techProgress)}%`;
    } else {
      techEl.textContent = "全部领悟 ✓";
      techBar.style.width = "100%";
    }
  },

  updateVillagers(villagers) {
    this.lastVillagers = villagers;
    const list = this.$("villager-list");
    const frag = document.createDocumentFragment();
    for (const v of villagers) {
      const row = document.createElement("div");
      row.className = "v-row" + (this.selected === v.id ? " sel" : "");
      row.dataset.id = v.id;
      row.dataset.name = v.n;
      const dot = v.act === 5 ? "🌙" : v.speech ? "💬" : "";
      row.innerHTML = `<span class="vn">${v.n}</span><span class="va">${ACT_ZH[v.act] || ""}</span><span class="vd">${dot}</span>`;
      row.style.display = !this.searchText || v.n.includes(this.searchText) ? "" : "none";
      frag.appendChild(row);
    }
    list.innerHTML = "";
    list.appendChild(frag);

    if (this.selected != null) {
      const v = villagers.find(x => x.id === this.selected);
      if (v) this.showDetail(v);
      else { this.selected = null; this.$("detail-card").classList.add("hidden"); }
    }
  },

  showDetail(v) {
    const card = this.$("detail-card");
    card.classList.remove("hidden");
    this.$("dc-name").textContent = v.n;
    this.$("dc-sub").textContent = `${v.stage || ""} · ${v.sex === 0 ? "男" : "女"}`;
    const bar = (id, val) => {
      const el = this.$(id);
      el.style.width = `${Math.max(2, Math.min(100, val))}%`;
    };
    bar("b-satiety", v.sa); bar("b-thirst", v.th); bar("b-energy", v.en); bar("b-stamina", v.st);
    bar("b-mood", v.me); bar("b-happy", v.ha); bar("b-health", v.hp);
    this.$("dc-activity").textContent = "正在：" + (ACT_ZH[v.act] || "…");
  },

  addLogs(entries) {
    if (!entries || entries.length === 0) return;
    const list = this.$("log-list");
    const frag = document.createDocumentFragment();
    for (const l of entries) {
      const row = document.createElement("div");
      row.className = "l-row " + (l.sev === 0 ? "important" : "normal");
      row.innerHTML = `<span class="lt">${l.t}</span>${l.text}`;
      frag.appendChild(row);
    }
    list.prepend(frag);
    while (list.children.length > 220) list.lastChild.remove();
  },

  addSocialMsgs(entries) {
    if (!entries || entries.length === 0) return;
    const list = this.$("msg-list");
    const frag = document.createDocumentFragment();
    for (const m of entries) {
      if (m.seq <= this.lastSocialSeq) continue;
      this.lastSocialSeq = Math.max(this.lastSocialSeq, m.seq);
      const row = document.createElement("div");
      row.className = "m-row k" + m.kind;
      const icon = MSG_ICONS[m.kind] || "💬";
      row.innerHTML = `<span class="mi">${icon}</span>${m.text}`;
      if (m.actors && m.actors.length > 0) {
        row.style.cursor = "pointer";
        row.addEventListener("click", () => {
          this.selected = m.actors[0];
          if (window.camera) window.camera.follow(m.actors[0]);
          const v = this.lastVillagers.find(x => x.id === m.actors[0]);
          if (v) this.showDetail(v);
        });
      }
      frag.appendChild(row);
    }
    list.prepend(frag);
    while (list.children.length > 150) list.lastChild.remove();
  },

  drawMinimap(gameView, camera) {
    if (!gameView.mapW) return;
    const viewRect = {
      x: -camera.worldX / camera.scale / TILE,
      y: -camera.worldY / camera.scale / TILE,
      w: window.innerWidth / camera.scale / TILE,
      h: window.innerHeight / camera.scale / TILE
    };
    gameView.drawMinimap(this.mmCtx, viewRect, UI.lastVillagers);
    const mm = this.mmCtx;
    const followers = [];
    if (camera.followId != null && window.villagerLayer) {
      const s = window.villagerLayer.sprites.get(camera.followId);
      if (s) followers.push({ x: s._fx / TILE, y: s._fy / TILE });
    }
    mm.fillStyle = "#ff4a3a";
    for (const f of followers) {
      mm.fillRect(f.x - 2, f.y - 2, 4, 4);
    }
  }
};
