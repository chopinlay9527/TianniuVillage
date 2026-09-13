using TianniuVillage.Core;

/// <summary>
/// 邻接合法性由官方图集元数据驱动（<see cref="TerrainCoverage"/> 由官方 .tsx 生成，
/// 经 <see cref="TerrainPalette"/> 映射到游戏地形）。这些测试确保生成结果不超出素材能力，
/// 且替换/更新图集后约束自动跟随数据变化。
/// </summary>
public class TerrainEdgeTests
{
    [Theory]
    [InlineData(2024)]
    [InlineData(777)]
    [InlineData(31337)]
    public void AllAdjacencies_AreSupportedByTileset(int seed)
    {
        var map = WorldGenerator.Generate(seed).Map;
        for (int y = 0; y < map.H; y++)
            for (int x = 0; x < map.W; x++)
            {
                var a = map.Get(x, y);
                foreach (var (dx, dy) in new[] { (1, 0), (0, 1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (!map.InBounds(nx, ny)) continue;
                    var b = map.Get(nx, ny);
                    Assert.True(TerrainPalette.SupportsAdjacency(a, b),
                        $"seed={seed} ({x},{y}){a} 与 ({nx},{ny}){b} 相邻，但官方图集无此过渡画");
                }
            }
    }

    [Theory]
    [InlineData(2024)]
    [InlineData(777)]
    [InlineData(31337)]
    public void DeepWater_OnlyTouchesWaterFamily(int seed)
    {
        // 官方 Deep Water 只与 Shallow Water / Melted Ice 有过渡
        var map = WorldGenerator.Generate(seed).Map;
        for (int y = 0; y < map.H; y++)
            for (int x = 0; x < map.W; x++)
            {
                if (map.Get(x, y) != Terrain.DeepWater) continue;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = x + dx, ny = y + dy;
                    if (!map.InBounds(nx, ny)) continue;
                    Assert.True(TerrainPalette.IsWater(map.Get(nx, ny)),
                        $"seed={seed} 深水({x},{y})直连非水地形 {map.Get(nx, ny)}");
                }
            }
    }

    [Fact]
    public void World_StillContainsAllTerrainKinds()
    {
        var world = WorldGenerator.Generate(2024);
        var kinds = new HashSet<Terrain>(world.Map.Tiles);
        foreach (Terrain t in Enum.GetValues<Terrain>())
            Assert.Contains(t, kinds);
    }

    [Fact]
    public void Coverage_MatchesOfficialMetadata()
    {
        // 官方 .tsx 实测覆盖（生成文件 TerrainCoverage.g.cs 的守卫）
        Assert.True(TerrainCoverage.Supports("Grass", "Sand"));
        Assert.True(TerrainCoverage.Supports("Grass", "Shallow Water"));
        Assert.True(TerrainCoverage.Supports("Dirt", "Shallow Water"));
        Assert.True(TerrainCoverage.Supports("Shallow Water", "Deep Water"));   // 深浅水有真实过渡
        Assert.True(TerrainCoverage.Supports("Sand", "Deep Sand"));
        // 官方未提供过渡的组合（旧派生表曾臆造）
        Assert.False(TerrainCoverage.Supports("Deep Water", "Grass"));
        Assert.False(TerrainCoverage.Supports("Deep Water", "Sand"));
        Assert.False(TerrainCoverage.Supports("Mountain", "Grass"));           // 官方 Mountain 零过渡
        Assert.False(TerrainCoverage.Supports("Vine", "Sand"));
    }

    [Fact]
    public void TerrainPalette_MapsMountainToDirt()
    {
        // 官方 Mountain 色仅有透明覆盖件、无过渡画 → 游戏山归入 Dirt（渲染侧加岩石罩色）
        Assert.Equal("Dirt", TerrainPalette.ColorName[Terrain.Mountain]);
        Assert.True(TerrainPalette.SupportsAdjacency(Terrain.Mountain, Terrain.Grass));
        Assert.True(TerrainPalette.SupportsAdjacency(Terrain.Mountain, Terrain.Water));
    }
}
