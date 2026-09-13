# 🏕 甜牛村 (TianniuVillage)

一个完全自主运转的 2D 像素风格虚拟村庄模拟游戏。村民拥有七维需求模型，会自发觅食、建造、婚育、研究科技、修路、饲养牲畜——你只需要观察。

## ✨ 核心特性

### 自主模拟引擎
- **七维需求** — 饱食 / 饮水 / 精力 / 体力 / 心情 / 幸福 / 健康，联动生病、冻伤、精神状态
- **智能 AI** — A* 寻路 + 效用评分抢任务 + 亚格平滑移动，村民自主决策优先级
- **经济链** — 采集 → 加工 → 制造 → 消耗完整闭环（浆果/谷物→熟食、原木→木板→建筑、铜矿→铜锭→铜工具）
- **13 项科技树** — 三阶解锁：磨制石斧 → 熔炼术 → 乡村礼制，由书斋中的学者自发钻研
- **畜牧系统** — 畜栏/牧场饲养鸡猪羊，狩猎活捉幼畜，蛋奶毛定期产出，牧场自繁衍
- **社交网络** — 婚姻/怀孕/出生/死亡，八卦传播影响好感，偶尔吵架也会和好
- **道路演化** — 踩踏成土路 → 碎石路 → 石板路，随使用磨损需维护，加速行走

### 世界模拟
- **256×256 程序化地形** — 噪声生成海洋/湖泊/沙滩/草地/森林/山地，同种子 100% 确定
- **四季更替** — 春播夏长秋收冬藏，冬季需冬衣御寒，食物腐烂与仓储损耗
- **昼夜循环** — GLSL Shader 逐像素光照，篝火/窗户/火把发出真实暖光，火焰闪烁
- **天气系统** — 晴/多云/雨/暴风雨/雪，暴风雨摧毁农田和渔屋
- **淡水系统** — 村民需要饮水，水井或河边取水，烹饪消耗淡水

### 渲染与 UI
- **WebGL 60fps** — PixiJS 渲染引擎，亚格插值平滑移动，4 向行走动画
- **游戏内 UI** — 速度控制（⏸ 1×–5×）、资源面板、村民搜索、纪事/消息双标签
- **上帝模式** — 8 种干预：天降食物/神迹治愈/丰收/天火/瘟疫/风暴/干旱/远方来客
- **消息看板** — 村民闲聊/传闻/八卦/争吵/关系动态实时推送，点击定位村民

## 🚀 运行方式

### 环境要求
- .NET 10 SDK
- Microsoft Edge WebView2 Runtime（Windows 10/11 通常自带）

### 开发运行
```bash
dotnet run --project src/TianniuVillage.App
```

### 发布
```bash
dotnet publish src/TianniuVillage.App -c Release -r win-x64 --self-contained false -o publish
```

### 无头模拟（平衡验证）
```bash
# 4 年模拟报告（人口/经济/科技/建筑 CSV）
dotnet run --project tools/TianniuVillage.Headless -- 20260908 4

# 2 年运行体检（作息/活动分布/日志统计）
dotnet run --project tools/TianniuVillage.Headless -- audit

# 关键事件追踪（婚育/死亡/科技突破）
dotnet run --project tools/TianniuVillage.Headless -- events
```

### 测试
```bash
dotnet test tests/TianniuVillage.Tests
```

## 📖 操作指南

| 操作 | 方式 |
|---|---|
| 暂停/继续 | 点击 ⏸ 或按空格键 |
| 调节速度 | 点击 1×–5× 或按数字键 1-5 |
| 移动视角 | 鼠标拖拽 |
| 缩放 | 鼠标滚轮 |
| 查看村民 | 点击地图上的村民或右侧列表中的名字 |
| 跳转位置 | 点击右下角小地图 |
| 搜索村民 | 右侧面板顶部搜索框 |
| 干预村庄 | 左侧面板"⚡ 神力"区域 |
| 存档/读档 | 左侧面板底部按钮（每 5 游戏日自动存档） |

## 🏗 项目结构

```
TianniuVillage/
├── src/
│   ├── TianniuVillage.Core/           # 纯 C# 模拟引擎（无 UI 依赖）
│   │   ├── Game.*.cs                  # 13 个 partial：需求/AI/经济/社交/科技
│   │   ├── WorldGen/                  # 地形/河溪/海岸/资源生成
│   │   ├── Noise/                     # 快速噪声（FastNoiseLite）
│   │   ├── Generated/                 # 图集生成物（TerrainCoverage.g.cs）
│   │   ├── TechDefs.cs                # 13 项三阶科技树
│   │   └── SaveService.cs             # JSON 存档
│   └── TianniuVillage.App/            # WinForms + WebView2 宿主
│       ├── MainForm.cs                # 主窗体（Designer.cs 支持）
│       └── wwwroot/                   # 前端
│           ├── js/pixels.js           # 程序化像素画
│           ├── js/layers.js           # 村民/动物/建筑/天气渲染层
│           ├── js/lightingFilter.js   # GLSL 逐像素光照 Shader
│           ├── js/main.js             # 入口·相机·桥接
│           └── vendor/pixi.min.js     # PixiJS 渲染引擎
├── tests/TianniuVillage.Tests/        # xUnit 单元测试
└── tools/
    ├── TianniuVillage.Headless/       # 无头平衡验证工具
    └── TianniuVillage.TileSetImport/  # LPC 图集(.tsx)→地形 profile 导入工具
```

## 📊 模拟数据（4 年参考）

| 指标 | 值 |
|---|---|
| 人口 | 6 → 9（4 出生 1 死亡） |
| 建筑自建 | 16 座（含铜矿坑、织布坊、粮仓） |
| 道路 | 土路 50 + 碎石 41 + 石板 51 格 |
| 科技 | 10 项（4 年内） |
| 幸福度 | 70+ 稳定 |

## 🔧 技术栈

| 层 | 技术 |
|---|---|
| 模拟引擎 | C# / .NET 10 |
| 宿主 | WinForms + WebView2 |
| 前端渲染 | PixiJS (WebGL) |
| 光照 | 自定义 GLSL Fragment Shader |
| 美术 | 地形：LPC Revised 四季地形（OGA-BY 3.0，32×32，官方 Tiled Terrain Set 驱动）；村民/动物：Ninja Adventure（CC0）16×16；物件：Kenney Tiny Town/Farm（CC0）16×16 + 程序化补充 |
| 测试 | xUnit |
| 存档 | System.Text.Json |

## 🎨 素材来源

- **地形（四季）**：LPC Revised — *[LPC Revised] Fully Configured 4-Seasons Tilesets for Tiled Map Editor*，作者 JaidynReiman（整理/配置）及 Eliza Wyatt (DeathsDarling) 等 LPC 贡献者，**[OGA-BY 3.0](https://opengameart.org/content/oga-by-30-faq) / CC-BY 3.0 双许可（需署名）**。32×32，含官方 Tiled Terrain Set 定义（`src/TianniuVillage.App/wwwroot/assets/lpc/official/*.tsx`）。署名见 `src/TianniuVillage.App/wwwroot/assets/lpc/official/CREDITS.txt`。
- **村民/动物角色**：Ninja Adventure Asset Pack — 作者 [pixel-boy](https://pixel-boy.itch.io/ninja-adventure-asset-pack)，[CC0](https://creativecommons.org/publicdomain/zero/1.0/)。25 个角色 + 鸡/猪/牛家畜。
- **资源物件（树/浆果丛/药草等）**：Kenney **Tiny Town** + **Tiny Farm** — [kenney.nl](https://kenney.nl/assets/tiny-town)，[CC0](https://creativecommons.org/publicdomain/zero/1.0/)。
- 其余（道路、部分建筑/物件、矿脉、蘑菇等）为本项目运行时程序化生成。
- 完整逐项许可与署名：见 [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md)。

## 📜 许可

MIT License（游戏代码）。**素材各自许可不同**：LPC 地形为 OGA-BY 3.0 / CC-BY 3.0（需署名），Ninja Adventure 与 Kenney 素材为 CC0。再分发时须保留署名。
