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

    this.$("btn-overview").addEventListener("click", () => this.openOverview());
    this.$("ov-close").addEventListener("click", () => this.closeOverview());
    this.$("overview-modal").addEventListener("click", (e) => {
      if (e.target.id === "overview-modal") this.closeOverview();
    });
    document.querySelectorAll(".otab").forEach(btn => {
      btn.addEventListener("click", () => {
        document.querySelectorAll(".otab").forEach(b => b.classList.remove("active"));
        document.querySelectorAll(".opane").forEach(p => p.classList.remove("active"));
        btn.classList.add("active");
        this.$(btn.dataset.tab).classList.add("active");
      });
    });
  },

  sendCmd(cmd) {
    if (window.chrome && window.chrome.webview)
      window.chrome.webview.postMessage(JSON.stringify(cmd));
  },

  setSpeed(mult) {
    this.setSpeedState(mult);
    this.sendCmd({ type: "speed", value: mult });
  },

  // 由宿主按键（空格/数字键）触发时的状态同步，不发命令
  setSpeedState(mult) {
    if (mult > 0) this._lastSpeed = mult;
    const pauseBtn = this.$("btn-pause");
    const paused = mult <= 0;
    pauseBtn.textContent = paused ? "▶" : "⏸";
    pauseBtn.classList.toggle("paused", paused);
    document.querySelectorAll(".tb-btn.spd").forEach(btn => {
      btn.classList.toggle("active", parseInt(btn.dataset.spd) === mult);
    });
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
      let badge = "";
      if (v.role === 1) badge += "<span class='vb role'>长</span>";
      else if (v.role === 2) badge += "<span class='vb role'>猎</span>";
      else if (v.role === 3) badge += "<span class='vb role'>医</span>";
      if (v.dis) badge += `<span class='vb ill'>${v.dis}</span>`;
      if (v.inj) badge += `<span class='vb inj'>${v.inj}</span>`;
      if (v.mb) badge += "<span class='vb mb'>崩溃</span>";
      row.innerHTML = `<span class="vn">${v.n}${badge}</span><span class="va">${ACT_ZH[v.act] || ""}</span><span class="vd">${dot}</span>`;
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
    const ROLE_ZH = { 1: "长老", 2: "猎队头领", 3: "医师" };
    this.$("dc-name").textContent = v.n;
    this.$("dc-sub").textContent =
      `${v.stage || ""} · ${v.sex === 0 ? "男" : "女"}${v.role ? " · " + (ROLE_ZH[v.role] || "") : ""}${v.age != null ? " · " + Math.floor(v.age) + "岁" : ""}`;
    const fam = [];
    if (v.homeN) fam.push(`🏠 ${v.homeN}`);
    if (v.spouseN) fam.push(`💞 ${v.spouseN}`);
    if (v.kidsN && v.kidsN.length) fam.push(`👶 子女${v.kidsN.length}`);
    if (v.rep != null) fam.push(`⭐ 声望 ${Math.round(v.rep)}`);
    const famEl = this.$("dc-family");
    if (fam.length) { famEl.textContent = fam.join("　"); famEl.style.display = ""; }
    else famEl.style.display = "none";
    const bar = (id, val) => {
      const el = this.$(id);
      el.style.width = `${Math.max(2, Math.min(100, val))}%`;
    };
    bar("b-satiety", v.sa); bar("b-thirst", v.th); bar("b-energy", v.en); bar("b-stamina", v.st);
    bar("b-mood", v.me); bar("b-happy", v.ha); bar("b-health", v.hp);
    this.$("dc-activity").textContent = "正在：" + (ACT_ZH[v.act] || "…") + (v.carry ? `　携带：${v.carry}` : "");
    const st = this.$("dc-status");
    if (st) {
      const parts = [];
      if (v.dis) parts.push(`🤒 ${v.dis}`);
      if (v.inj) parts.push(`🩹 ${v.inj}`);
      if (v.mb) parts.push("😵 精神崩溃");
      st.textContent = parts.join("　");
      st.style.display = parts.length ? "" : "none";
    }
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
      if (s) followers.push({ x: s.x / TILE, y: s.y / TILE });
    }
    mm.fillStyle = "#ff4a3a";
    for (const f of followers) {
      mm.fillRect(f.x - 2, f.y - 2, 4, 4);
    }
  },

  /* ═══ 村庄全览 ═══ */
  openOverview() {
    this._ovOpen = true;
    this.$("overview-modal").classList.remove("hidden");
    this.requestOverview();
    if (this._ovTimer == null)
      this._ovTimer = setInterval(() => this.requestOverview(), 2000);
  },

  closeOverview() {
    this._ovOpen = false;
    this.$("overview-modal").classList.add("hidden");
    if (this._ovTimer != null) { clearInterval(this._ovTimer); this._ovTimer = null; }
  },

  requestOverview() {
    if (!this._ovOpen) return;
    this.sendCmd({ type: "overview" });
  },

  renderOverview(o) {
    if (!this._ovOpen) return;
    this.$("ov-time").textContent = o.timeZh;
    this.renderOvOverview(o);
    this.renderOvPop(o);
    this.renderOvHousing(o);
    this.renderOvStorage(o);
    this.renderOvHarvest(o);
    this.renderOvJobs(o);
    this.renderOvTech(o);
  },

  focusVillager(id) {
    this.selected = id;
    if (window.camera) window.camera.follow(id);
    const v = this.lastVillagers.find(x => x.id === id);
    if (v) this.showDetail(v);
    this.filterVillagerList();
    this.closeOverview();
  },

  ovEl(tag, cls, text) {
    const el = document.createElement(tag);
    if (cls) el.className = cls;
    if (text != null) el.textContent = text;
    return el;
  },

  ovClear(id) {
    const p = this.$(id);
    p.innerHTML = "";
    return p;
  },

  ovTitle(p, text) { p.appendChild(this.ovEl("div", "section-title", text)); },

  ovEmpty(p, text) { p.appendChild(this.ovEl("div", "empty-hint", text)); },

  ovStats(p, entries) {
    const grid = this.ovEl("div", "ovstat-grid");
    for (const [k, v, cls] of entries) {
      const cell = this.ovEl("div", "ovstat");
      cell.appendChild(this.ovEl("span", "k", k));
      cell.appendChild(this.ovEl("span", "v " + (cls || ""), String(v)));
      grid.appendChild(cell);
    }
    p.appendChild(grid);
  },

  ovTable(p, headers, rows) {
    const t = document.createElement("table");
    t.className = "ov-table";
    const thead = document.createElement("thead");
    const hr = document.createElement("tr");
    for (const h of headers) hr.appendChild(this.ovEl("th", null, h));
    thead.appendChild(hr);
    t.appendChild(thead);
    const tb = document.createElement("tbody");
    for (const cells of rows) {
      const r = document.createElement("tr");
      for (const c of cells) {
        const td = document.createElement("td");
        if (typeof c === "string") td.textContent = c;
        else {
          if (c.cls) td.className = c.cls;
          if (c.html != null) td.innerHTML = c.html;
          else if (c.txt != null) td.textContent = c.txt;
        }
        r.appendChild(td);
      }
      tb.appendChild(r);
    }
    t.appendChild(tb);
    p.appendChild(t);
  },

  ovStatusBadges(v) {
    const b = [];
    if (v.role === 1) b.push({ cls: "role", t: "长" });
    else if (v.role === 2) b.push({ cls: "role", t: "猎" });
    else if (v.role === 3) b.push({ cls: "role", t: "医" });
    if (v.dis) b.push({ cls: "ill", t: v.dis });
    if (v.inj) b.push({ cls: "inj", t: v.inj });
    if (v.mb) b.push({ cls: "mb", t: "崩溃" });
    if (v.preg) b.push({ cls: "role", t: "🤰" });
    return b.map(x => `<span class="vb ${x.cls}">${x.t}</span>`).join("");
  },

  renderOvOverview(o) {
    const p = this.ovClear("op-tab-overview");
    const v = o.village, g = o.pop, h = o.housing, s = o.storage, t = o.tech;
    this.ovStats(p, [
      ["村庄", v.name],
      ["种子/面积", `${v.seed} · ${v.w}×${v.h}`],
      ["日期/天气", `${v.year}年${v.seasonZh}季${v.dayOfSeason}日 · ${v.weatherZh}`],
      ["人口", g.total],
      ["平均年龄", g.avgAge + "岁"],
      ["幸福 / 健康", `${g.avgHappy} / ${g.avgHealth}`],
      ["平均心情", g.avgMood],
      ["食物储备", o.food],
      ["淡水", o.water],
      ["仓储", `${s.used} / ${s.cap}${s.full ? " · 已满" : ""}`],
      ["床位", `${h.residents} / ${h.beds}`],
      ["科技", `${t.count} / ${t.total}`]
    ]);
    p.appendChild(this.ovEl("div", "empty-hint",
      `累计出生 ${g.births} · 逝世 ${g.deaths}${g.births > g.deaths ? "" : " ⚠"}`));

    this.ovTitle(p, "当前事态");
    const notes = [];
    if (o.festival) notes.push(`🎉 ${o.festival.desc}（剩 ${o.festival.left} 分）`);
    if (o.merchant) notes.push(`🐴 商人到访（剩 ${o.merchant.left} 分）`);
    if (g.ill > 0) notes.push(`🤒 患病 ${g.ill} 人`);
    if (g.injured > 0) notes.push(`🩹 受伤 ${g.injured} 人`);
    if (h.homeless.length > 0) notes.push(`🏚 无房 ${h.homeless.length} 人`);
    if (o.jobs.length > 0) notes.push(`🛠 待办事宜 ${o.jobs.reduce((a, j) => a + j.open + j.claimed, 0)} 项`);
    if (notes.length > 0) p.appendChild(this.ovEl("div", null, notes.join("　")));
    else this.ovEmpty(p, "一切平静。");

    this.ovTitle(p, "牲畜 · 道路 · 野生");
    const livestock = o.animals.livestock;
    const roadTotal = o.roads.levels.reduce((a, r) => a + r.tiles, 0);
    p.appendChild(this.ovEl("div", null,
      `🐔 鸡 ${livestock.chickens}　🐑 羊 ${livestock.sheep}　🐷 猪 ${livestock.pigs}`));
    p.appendChild(this.ovEl("div", null,
      `道路 ${roadTotal} 格 · 磨损 ${o.roads.wear}` +
      (o.animals.wildlife.length > 0
        ? `　野生 ${o.animals.wildlife.map(w => `${w.k}×${w.count}`).join("　")}` : "")));

    if (t.current) {
      this.ovTitle(p, "当前研究");
      p.appendChild(this.ovEl("div", null, `${t.current}　${t.progress}%`));
      const bar = this.ovEl("div", "prog-bar");
      const fill = this.ovEl("i");
      fill.style.width = `${Math.max(2, t.progress)}%`;
      bar.appendChild(fill);
      p.appendChild(bar);
    }
  },

  renderOvPop(o) {
    const p = this.ovClear("op-tab-pop");
    this.ovTitle(p, `人口 ${o.pop.total}　·　点击 🎯 定位到世界`);
    if (!o.villagers || o.villagers.length === 0) { this.ovEmpty(p, "暂无村民"); return; }
    const ROLE = { 1: "长老", 2: "猎队头领", 3: "医师" };
    const SKILLS = [["farming", "耕"], ["building", "建"], ["gathering", "采"],
      ["cooking", "厨"], ["medicine", "医"], ["hunting", "猎"], ["learning", "学"]];
    for (const v of o.villagers) {
      const row = this.ovEl("div", "pop-row");
      const nm = this.ovEl("span", "pn");
      nm.innerHTML = v.n + this.ovStatusBadges(v);
      row.appendChild(nm);
      const meta = this.ovEl("span", "pm");
      meta.textContent =
        `${v.sex === 0 ? "男" : "女"} · ${v.age}岁 · ${v.stage}${v.role ? " · " + (ROLE[v.role] || "") : ""} · ${v.job}`;
      row.appendChild(meta);
      const fam = this.ovEl("span", "pm");
      const parts = [];
      if (v.home) parts.push(`🏠 ${v.home}`);
      if (v.spouse) parts.push(`💞 ${v.spouse}`);
      if (v.kids && v.kids.length) parts.push(`👶 ${v.kids.length}孩`);
      if (v.rep != null) parts.push(`⭐ ${Math.round(v.rep)}`);
      fam.textContent = parts.join(" ");
      row.appendChild(fam);
      const sk = this.ovEl("span", "skill-bars");
      for (const [k, name] of SKILLS) {
        const item = this.ovEl("span", "skill-item");
        item.innerHTML = `<b>${name}</b><span class="skill-bar"><i style="width:${Math.max(2, v.sk[k] || 0)}%"></i></span>`;
        sk.appendChild(item);
      }
      row.appendChild(sk);
      const goto = this.ovEl("span", "goto", "🎯");
      goto.addEventListener("click", () => this.focusVillager(v.id));
      row.appendChild(goto);
      p.appendChild(row);
    }
  },

  renderOvHousing(o) {
    const p = this.ovClear("op-tab-housing");
    const h = o.housing;
    this.ovStats(p, [
      ["总床位", h.beds],
      ["已入住", h.residents],
      ["空床", h.vacant],
      ["无房村民", h.homeless.length]
    ]);
    this.ovTitle(p, "住房明细");
    if (h.homes.length === 0) this.ovEmpty(p, "还没有建成住房");
    else this.ovTable(p, ["住房", "位置", "住户 / 床位", "温暖", "舒适", "住户"],
      h.homes.map(x => [
        `${x.name}#${x.id}`,
        { cls: "pos", txt: `${x.x},${x.y}` },
        `${x.oc} / ${x.beds}`,
        `${Math.round(x.warm * 100)}%`,
        `${Math.round(x.comf * 100)}%`,
        x.residents.length > 0 ? x.residents.join("、") : { cls: "fn", txt: "空" }
      ]));
    if (h.homeless.length > 0) {
      this.ovTitle(p, "无房村民");
      p.appendChild(this.ovEl("div", null, h.homeless.join("、")));
    }
  },

  renderOvStorage(o) {
    const p = this.ovClear("op-tab-storage");
    const s = o.storage;
    const pct = s.cap > 0 ? Math.round(s.used / s.cap * 100) : 0;
    p.appendChild(this.ovEl("div", null,
      `仓储 ${s.used} / ${s.cap}（${pct}%）${s.full ? " · 🚨已满！" : ""}`));
    const bar = this.ovEl("div", "prog-bar");
    const fill = this.ovEl("i");
    fill.style.width = `${Math.max(2, Math.min(100, pct))}%`;
    bar.appendChild(fill);
    p.appendChild(bar);
    const CAT = { 0: "食物", 1: "材料", 2: "药材" };
    this.ovTitle(p, "物资明细（含累计获得 / 消耗）");
    if (!o.items || o.items.length === 0) this.ovEmpty(p, "仓库空空如也");
    else this.ovTable(p, ["物品", "类别", "库存", "累计获得", "累计消耗", "净结余"],
      o.items.map(i => [
        i.cat === 0 ? "🍎 " + i.name : i.name,
        { cls: "fn", txt: CAT[i.cat] || "" },
        { cls: "num", txt: String(i.stock) },
        { cls: "num", txt: String(i.produced) },
        { cls: "num", txt: String(i.consumed) },
        { cls: `num ${i.produced - i.consumed > 0 ? "ok" : i.produced - i.consumed < 0 ? "warn" : ""}`,
          txt: i.produced - i.consumed > 0 ? "+" + (i.produced - i.consumed) : String(i.produced - i.consumed) }
      ]));
    this.ovTitle(p, "食物可食用储备（统计口径）");
    p.appendChild(this.ovEl("div", "empty-hint", `当前可食食物总量（不含淡水）：${o.food}`));
  },

  renderOvHarvest(o) {
    const p = this.ovClear("op-tab-harvest");
    const farms = o.buildings.filter(b => b.k === "farm" && b.state === 2);
    this.ovTitle(p, "农田 · 田块分布（空 / 犁地 / 出苗 / 抽穗 / 成熟）");
    if (farms.length === 0) this.ovEmpty(p, "还没有农田");
    else this.ovTable(p, ["农田", "位置", "田块"], farms.map(f => [
      `农田#${f.id}`,
      { cls: "pos", txt: `${f.x},${f.y}` },
      f.crop.map((n, idx) => n > 0
        ? `<span class="ov-pill ${idx === 4 ? "ready" : ""}">${["空","犁","苗","穗","熟"][idx]}×${n}</span>`
        : "").join("") || "—"
    ]));
    this.ovTitle(p, "野外资源（剩余）");
    if (o.resources.length === 0) this.ovEmpty(p, "四周已无资源");
    else this.ovTable(p, ["资源", "剩余点", "余量", "浆果待采"],
      o.resources.map(r => [
        r.name,
        { cls: "num", txt: String(r.nodes) },
        { cls: "num", txt: String(r.amount) },
        { cls: "num", txt: r.berries > 0 ? String(r.berries) : "—" }
      ]));
    this.ovTitle(p, "畜牧 · 栏舍");
    const pens = o.buildings.filter(b => b.lt && b.state === 2);
    if (pens.length === 0) this.ovEmpty(p, "还没有牲畜栏舍");
    else this.ovTable(p, ["栏舍", "牲畜", "数量", "待收产物"],
      pens.map(b => [
        `${b.name}#${b.id}`,
        b.lt === "chicken" ? "🐔 鸡" : b.lt === "sheep" ? "🐑 羊" : b.lt === "pig" ? "🐷 猪" : b.lt,
        { cls: "num", txt: String(b.lc) },
        (b.buffered || []).map(x => `${x.name}×${x.count}`).join(" ").trim() || "—"
      ]));
  },

  renderOvJobs(o) {
    const p = this.ovClear("op-tab-jobs");
    this.ovTitle(p, "当前待办工事");
    if (o.jobs.length === 0) this.ovEmpty(p, "没有待办工事，村民悠闲度日");
    else this.ovTable(p, ["工事", "待接单", "进行中"],
      o.jobs.map(j => [
        j.name,
        { cls: "num", txt: String(j.open) },
        { cls: "num", txt: String(j.claimed) }
      ]));
    this.ovTitle(p, "道路");
    const LVL = { 1: "土路", 2: "碎石路", 3: "石板路" };
    if (o.roads.levels.length === 0) this.ovEmpty(p, "尚未修路");
    else this.ovTable(p, ["等级", "格数"],
      o.roads.levels.map(r => [LVL[r.lvl] || `Lv${r.lvl}`, { cls: "num", txt: String(r.tiles) }]));
    p.appendChild(this.ovEl("div", "empty-hint", `道路总磨损：${o.roads.wear}`));
    const building = o.buildings.filter(b => b.state < 2);
    this.ovTitle(p, "在建建筑");
    if (building.length === 0) this.ovEmpty(p, "没有正在建造的建筑");
    else this.ovTable(p, ["建筑", "状态", "进度", "已交付材料"],
      building.map(b => [
        `${b.name}#${b.id}`,
        b.state === 1 ? "建造中" : "规划中",
        { cls: "num", txt: b.prog + "%" },
        (b.delivered || []).map(d => `${d.name} ${d.have}/${d.need}`).join(" ") || "—"
      ]));
  },

  renderOvTech(o) {
    const p = this.ovClear("op-tab-tech");
    const t = o.tech;
    p.appendChild(this.ovEl("div", null, `已研习 ${t.count} / ${t.total} 项`));
    if (t.current) {
      this.ovTitle(p, "当前研究");
      p.appendChild(this.ovEl("div", null, `${t.current}　${t.progress}%`));
      const bar = this.ovEl("div", "prog-bar");
      const fill = this.ovEl("i");
      fill.style.width = `${Math.max(2, t.progress)}%`;
      bar.appendChild(fill);
      p.appendChild(bar);
    }
    this.ovTitle(p, "已研习科技");
    if (!t.researched || t.researched.length === 0) this.ovEmpty(p, "尚未研习任何科技");
    else {
      const box = this.ovEl("div", null);
      for (const name of t.researched) box.appendChild(this.ovEl("span", "tech-chip done", name));
      p.appendChild(box);
    }
  }
};
