namespace TianniuVillage.Core;

/// <summary>
/// 游戏 7 类地形 ↔ 官方 LPC Wang 色名的映射（设计决策，非生成数据）。
/// 依据：官方 Mountain 色仅有一个透明覆盖件、且零过渡画（作者把山当崖壁构件），
/// 故游戏山归入 Dirt 并以罩色区分（渲染侧见 textures.js/terrain.mountain.tint）。
/// </summary>
public static class TerrainPalette
{
    public static readonly IReadOnlyDictionary<Terrain, string> ColorName = new Dictionary<Terrain, string>
    {
        [Terrain.DeepWater] = "Deep Water",
        [Terrain.Water] = "Shallow Water",
        [Terrain.Sand] = "Sand",
        [Terrain.Grass] = "Grass",
        [Terrain.Forest] = "Grass",
        [Terrain.Highland] = "Dirt",
        [Terrain.Mountain] = "Dirt",
    };

    /// <summary>两种地形相邻时，权威图集是否有对应过渡画（同色视为支持）。</summary>
    public static bool SupportsAdjacency(Terrain a, Terrain b) =>
        TerrainCoverage.Supports(ColorName[a], ColorName[b]);

    /// <summary>该地形是否属于水族（渲染/生成共用）。</summary>
    public static bool IsWater(Terrain t) => t is Terrain.Water or Terrain.DeepWater;

    /// <summary>生成器用于挑选"可支持的替代地形"：按官方优先级（水族最低）排序。</summary>
    public static int Priority(Terrain t) => t switch
    {
        Terrain.DeepWater => 0,
        Terrain.Water => 1,
        Terrain.Sand => 2,
        Terrain.Grass or Terrain.Forest => 3,
        Terrain.Highland => 4,
        Terrain.Mountain => 5,
        _ => 3,
    };
}
