namespace TianniuVillage.Core;

/// <summary>
/// 海岸处理：CA 平滑 → 可选沙滩带 → 深浅水平滑 → 依官方过渡覆盖修复非法邻接。
/// 邻接合法性来源于 <see cref="TerrainPalette.SupportsAdjacency"/>（即官方 .tsx 生成的数据）。
/// </summary>
public sealed class CoastProcessor
{
    private readonly WorldGenConfig _cfg;

    public CoastProcessor(WorldGenConfig cfg) => _cfg = cfg;

    public void Process(World world)
    {
        SmoothLandWater(world);
        ApplyCoastalSandBand(world);
        SmoothWaterDepth(world);
        EnforceSupportedAdjacency(world);
    }

    // --- 水陆掩码 CA 平滑（教科书 4-5 规则）：消除分形噪声的锯齿岸线 ---
    private void SmoothLandWater(World world)
    {
        var map = world.Map;
        for (int pass = 0; pass < _cfg.CoastSmoothPasses; pass++)
        {
            var snap = (Terrain[])map.Tiles.Clone();
            for (int y = 0; y < map.H; y++)
                for (int x = 0; x < map.W; x++)
                {
                    int water = 0, grassable = 0, sand = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (!map.InBounds(nx, ny)) { water++; continue; }   // 图外视为海洋
                            var n = snap[map.Index(nx, ny)];
                            if (TerrainPalette.IsWater(n)) water++;
                            if (n is Terrain.Grass or Terrain.Forest) grassable++;
                            if (n == Terrain.Sand) sand++;
                        }
                    var t = snap[map.Index(x, y)];
                    if (TerrainPalette.IsWater(t))
                    {
                        if (water <= 3) map.Tiles[map.Index(x, y)] = Terrain.Sand;      // 孤水 → 沙
                    }
                    else if (t is Terrain.Grass or Terrain.Forest)
                    {
                        if (water >= 6) map.Tiles[map.Index(x, y)] = Terrain.Water;     // 海角 → 水
                        else if (t == Terrain.Grass && sand >= 6) map.Tiles[map.Index(x, y)] = Terrain.Sand;
                    }
                    else if (t == Terrain.Sand && sand <= 3 && grassable >= 5)
                        map.Tiles[map.Index(x, y)] = Terrain.Grass;
                }
        }
    }

    // --- 可选沙滩带：草/林海岸转沙（纯风格；官方水↔草/泥亦有过渡）---
    private void ApplyCoastalSandBand(World world)
    {
        if (_cfg.CoastalSandWidth <= 0) return;
        var map = world.Map;
        for (int pass = 0; pass < _cfg.CoastalSandWidth; pass++)
        {
            var snap = (Terrain[])map.Tiles.Clone();
            for (int y = 0; y < map.H; y++)
                for (int x = 0; x < map.W; x++)
                {
                    var t = snap[map.Index(x, y)];
                    if (t is not (Terrain.Grass or Terrain.Forest)) continue;
                    if (HasWaterNeighbor(map, snap, x, y)) map.Tiles[map.Index(x, y)] = Terrain.Sand;
                }
        }
    }

    // --- 深浅水 CA 多数平滑 + 深水护岸（官方 Deep Water 只与浅水有过渡）---
    private void SmoothWaterDepth(World world)
    {
        var map = world.Map;
        for (int pass = 0; pass < 2; pass++)
        {
            var snap = (Terrain[])map.Tiles.Clone();
            for (int y = 0; y < map.H; y++)
                for (int x = 0; x < map.W; x++)
                {
                    if (!TerrainPalette.IsWater(snap[map.Index(x, y)])) continue;
                    int deep = 0, shallow = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (!map.InBounds(x + dx, y + dy)) continue;
                            var n = snap[map.Index(x + dx, y + dy)];
                            if (n == Terrain.DeepWater) deep++;
                            else if (n == Terrain.Water) shallow++;
                        }
                    var target = deep > shallow ? Terrain.DeepWater : Terrain.Water;
                    if (target == Terrain.DeepWater && HasNonWaterNeighbor(map, snap, x, y)) target = Terrain.Water;
                    map.Tiles[map.Index(x, y)] = target;
                }
        }
        // 平滑可能把深水推回岸边，补一次缓冲
        for (int pass = 0; pass < 2; pass++)
        {
            bool changed = false;
            var snap = (Terrain[])map.Tiles.Clone();
            for (int y = 0; y < map.H; y++)
                for (int x = 0; x < map.W; x++)
                    if (snap[map.Index(x, y)] == Terrain.DeepWater && HasNonWaterNeighbor(map, snap, x, y))
                    {
                        map.Tiles[map.Index(x, y)] = Terrain.Water;
                        changed = true;
                    }
            if (!changed) break;
        }
    }

    /// <summary>
    /// 依官方过渡覆盖修复非法邻接：把低优先级一侧换成能同时兼容邻里的中间地形。
    /// 当前映射下唯一会触发的是"深水直贴陆地"（Deep Water 官方只与浅水有过渡）；
    /// 循环通用（数据驱动），以便替换图集后自动适配。
    /// </summary>
    private void EnforceSupportedAdjacency(World world, int maxPasses = 8)
    {
        var map = world.Map;
        // 备选替换（按官方优先级升序）：优先用能兼容邻里的较低优先级地形
        Terrain[] candidates = [Terrain.Water, Terrain.Sand, Terrain.Grass, Terrain.Highland];
        for (int pass = 0; pass < maxPasses; pass++)
        {
            bool changed = false;
            var snap = (Terrain[])map.Tiles.Clone();
            for (int y = 0; y < map.H; y++)
                for (int x = 0; x < map.W; x++)
                {
                    var t = snap[map.Index(x, y)];
                    if (!HasUnsupportedNeighbor(map, snap, x, y, t)) continue;
                    foreach (var cand in candidates)
                    {
                        if (cand == t) continue;
                        if (SupportsAllNeighbors(map, snap, x, y, cand)) { map.Tiles[map.Index(x, y)] = cand; changed = true; break; }
                    }
                }
            if (!changed) break;
        }
    }

    private static bool HasUnsupportedNeighbor(TileMap map, Terrain[] tiles, int x, int y, Terrain t)
    {
        foreach (var (dx, dy) in Neighbors)
        {
            int nx = x + dx, ny = y + dy;
            if (!map.InBounds(nx, ny)) continue;
            if (!TerrainPalette.SupportsAdjacency(t, tiles[map.Index(nx, ny)])) return true;
        }
        return false;
    }

    private static bool SupportsAllNeighbors(TileMap map, Terrain[] tiles, int x, int y, Terrain cand)
    {
        foreach (var (dx, dy) in Neighbors)
        {
            int nx = x + dx, ny = y + dy;
            if (!map.InBounds(nx, ny)) continue;
            if (!TerrainPalette.SupportsAdjacency(cand, tiles[map.Index(nx, ny)])) return false;
        }
        return true;
    }

    private static bool HasWaterNeighbor(TileMap map, Terrain[] tiles, int x, int y)
    {
        foreach (var (dx, dy) in Neighbors)
        {
            int nx = x + dx, ny = y + dy;
            if (map.InBounds(nx, ny) && TerrainPalette.IsWater(tiles[map.Index(nx, ny)])) return true;
        }
        return false;
    }

    private static bool HasNonWaterNeighbor(TileMap map, Terrain[] tiles, int x, int y)
    {
        foreach (var (dx, dy) in Neighbors)
        {
            int nx = x + dx, ny = y + dy;
            if (map.InBounds(nx, ny) && !TerrainPalette.IsWater(tiles[map.Index(nx, ny)])) return true;
        }
        return false;
    }

    private static readonly (int, int)[] Neighbors = [(1, 0), (-1, 0), (0, 1), (0, -1)];
}
