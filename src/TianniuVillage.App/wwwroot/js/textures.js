"use strict";
// ============================================================
// 甜牛村 · 贴图总配置（修改本文件即可改变全部贴图样式）
// ============================================================
//
// 素材图: assets/atlas.png（12 列 × 22 行，每格 16×16 像素）
//   上半 t0~t131  = Tiny Town    下半 f0~f131 = Tiny Farm
//   编号 = 行主序（从左到右、从上到下，从 0 起）
//   带编号的预览大图: 桌面\甜牛村贴图挑选\atlas_preview.png
//
// 引用素材格写法: "t25" / "f27"（t=上半 Town，f=下半 Farm）
// 修改后保存 → 重启游戏即生效（无需改任何其他代码）
// ------------------------------------------------------------

const TEX = {

  // ---------- 地形（7 类）----------
  // 写法 A: { color: "#91a0b8" }                      → 纯色填充
  // 写法 B: { tiles: ["t0","t1"], tint: "rgba(...)" }  → 素材格(多个则随机混铺) + 可选罩色
  // 任意写法可加 noise: 0.07 → 杂色强度（每格随机撒 6 个明暗 1px 噪点，0=关闭）
  terrain: {
    // waves: true → 水面波纹（横向短线+闪光点，哈希散布不重复）
    deepWater:    { color: "#3a6fb0", waves: true },                 // 深水（蓝）
    shallowWater: { color: "#5aa9dd", waves: true },                 // 浅水（蓝）
    // noise = 杂色强度（每格随机撒 14 个明暗 1px 噪点；0 = 关闭）
    sand:         { tiles: ["t25"], noise: 0.12 },                   // 沙滩
    grass:        { tiles: ["t0"], noise: 0.12 },                    // 草地
    forest:       { tiles: ["t0"], tint: "rgba(20,60,20,0.32)", noise: 0.12 },    // 森林
    highland:     { tiles: ["t25"], tint: "rgba(115,115,100,0.30)", noise: 0.12 }, // 高地
    mountain:     { tiles: ["t25"], tint: "rgba(100,100,115,0.48)",  // 山
                    noise: 0.12, cracks: true, crackColor: "rgba(35,35,45,0.55)" },
  },

  // ---------- 资源物件 ----------
  tree: {
    // 注意: t5 顶部/侧边在素材里被裁切（触边），不可用；以下均为完整格
    tiles: ["t3", "t4", "f39", "f54", "f78"],  // 树（多格随机分布）
    shadow: "rgba(30,60,25,0.25)",           // 树底阴影
  },
  bush: {
    withBerries: "f27",                      // 结果浆果丛
    empty: "t7",                             // 空丛
    berryColor: "#d0455a",                   // 结果时的红果点缀色
    shadow: "rgba(30,60,25,0.22)",
  },
  herb: { tile: "f15" },                     // 药草
  flax: {                                    // 亚麻（药草植株+蓝花罩）
    tile: "f15",
    tint: "rgba(90,110,210,0.35)",
    flowerColor: "rgba(200,215,255,0.8)",
  },

  // ---------- 道路（3 级，程序化绘制配色）----------
  road: {
    1: { base: "#c9b088", edge: "#b9a078", speckle: "#d8c298" },                    // 土路
    2: { base: "#a89878", edge: "#8a7a5c", speckle: ["#8f8064", "#bcae8c"] },       // 碎石路
    3: { base: "#b4b0a4", top: "#c8c4b8", edge: "#8f8b80", joint: "#9d998e", detail: "#a8a49a" }, // 石板路
  },

  // ---------- 其余资源（程序化配色）----------
  mushroom: { tiles: ["t83", "t95"],          // 蘑菇（两变体；留空 [] 则用下面的程序化配色）
              stem: "#e8ddc8", cap: "#b8503c", dot: "#e8ddc8" },
  stone:    { base: "#8a8d80", light: "#9da093", dark: "#767970" },
  copperVein: { rock: "#8a7a5c", ore: "#c4854a", oreLight: "#e0a050" },  // 铜矿
  ironVein:   { rock: "#8a7a5c", ore: "#8a9aaa", oreLight: "#b0c0d0" },  // 铁矿
  fishSpot:  { shadow: "rgba(40,70,60,0.5)", fish: "#5a8a7a" },          // 渔点
  waterSpot: { ripple: "rgba(190,230,240,0.5)" },                        // 取水点
};
