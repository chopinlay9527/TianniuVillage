# 第三方素材与代码声明 (THIRD-PARTY NOTICES)

本项目游戏代码为 MIT 许可；下列第三方素材/代码各自许可不同。**再分发本项目（含构建产物）时须保留本文件及所列署名。**

---

## 1. LPC Revised 四季地形（主要地形贴图）

- **作品**：*[LPC Revised] Fully Configured 4-Seasons Tilesets for Tiled Map Editor*
- **作者**：JaidynReiman（整理 LPC Revised 素材并配置 Tiled Map Editor 图集），基于 LPC Revised 原始素材
- **来源**：https://opengameart.org/content/lpc-revised-fully-configured-4-seasons-tilesets-for-tiled-map-editor
  以及 https://github.com/ElizaWy/LPC
- **许可**：**CC-BY 3.0 与 OGA-BY 3.0 双许可**（可商用，须署名）
  - https://creativecommons.org/licenses/by/3.0/
  - https://opengameart.org/content/oga-by-30-faq
- **署名原文 (Copyright/Attribution Notice)**：
  > JaidynReiman for compiling the assets from LPC Revised and configuring the Tiled Map Editor tilesets for it. Also created a winter version of the bushes with a simple palette recolor to make the collision easier to convert between seasons. Original LPC Revised (https://github.com/ElizaWy/LPC) asset contributors include Eliza Wyatt (DeathsDarling), Lanea Zimmerman (Sharm), Stephen Challener (Redshrike), Johannes Sjölund (Wulax), BlueCarrot16, BenCreating, Durrani, YuriNikolai and Craftpix.net 2D Game Assets. https://github.com/ElizaWy/LPC/blob/main/Credits.txt
- **本项目使用文件**：
  - `src/TianniuVillage.App/wwwroot/assets/lpc/lpc-terrain-{spring,summer,autumn,winter}.png`（32×32，2048×2048，64 列）
  - `src/TianniuVillage.App/wwwroot/assets/lpc/official/*.tsx`（官方 Tiled 图集定义，含 Terrain/Walls/Fences wangset）
  - `src/TianniuVillage.App/wwwroot/assets/lpc/official/CREDITS.txt`（完整署名）
  - 由上述 tsx 生成的 `src/TianniuVillage.App/wwwroot/assets/lpc/terrain_profile.json`

## 2. Ninja Adventure Asset Pack（村民/动物）

- **作者**：pixel-boy
- **来源**：https://pixel-boy.itch.io/ninja-adventure-asset-pack
- **许可**：**CC0 1.0**（公共领域，无需署名）
- **本项目使用文件**：`src/TianniuVillage.App/wwwroot/assets/char/*.png`（25 个角色）、`src/TianniuVillage.App/wwwroot/assets/animal/*.png`（鸡/猪/牛 9 张）

## 3. Kenney Tiny Town + Tiny Farm（资源物件图集）

- **作者**：Kenney (kenney.nl)
- **来源**：https://kenney.nl/assets/tiny-town
- **许可**：**CC0 1.0**（公共领域，无需署名）
- **本项目使用文件**：`src/TianniuVillage.App/wwwroot/assets/atlas.png`（由 Tiny Town 与 Tiny Farm 合并，12 列 × 22 行，16×16/格）

## 4. FastNoiseLite（噪声库，C# 单文件）

- **作者**：Jordan Peck (Auburn)
- **来源**：https://github.com/Auburn/FastNoiseLite
- **许可**：**MIT**
- **本项目使用文件**：`src/TianniuVillage.Core/Noise/FastNoiseLite.cs`（官方 C# 单文件）

---

## 附：许可边界注意事项

- LPC 系另有 **CC-BY-SA（传染性）** 的包，如原版 *[LPC] Terrains* 与其示例 `.tmx`、LPC Crops。本项目**未**引入这些 CC-BY-SA 内容；如需引入，须单独评估传染性对整体分发的影响。
- 本项目自有的运行时生成内容（道路、部分建筑/物件绘制）不受上述第三方许可约束。
