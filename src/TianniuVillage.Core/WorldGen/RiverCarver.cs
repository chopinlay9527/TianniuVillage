namespace TianniuVillage.Core;

/// <summary>河流：从山地/高地源头沿最陡下降走到海洋/湖泊，雕刻水道。</summary>
public static class RiverCarver
{
    public static void Carve(World world, int seed, float[] hmap, WorldGenConfig cfg)
    {
        var map = world.Map;
        var rng = new Rng(seed ^ 0x1215);

        var sources = new List<int>();
        var fallback = new List<int>();
        for (int i = 0; i < map.Tiles.Length; i++)
        {
            if (map.Tiles[i] == Terrain.Mountain) sources.Add(i);
            else if (map.Tiles[i] == Terrain.Highland) fallback.Add(i);
        }
        if (sources.Count < 3) sources.AddRange(fallback);
        if (sources.Count == 0) return;

        int made = 0, tries = 0;
        while (made < cfg.RiverCount && tries < 50 && sources.Count > 0)
        {
            tries++;
            int si = sources[rng.Next(sources.Count)];
            int sx = si % map.W, sy = si / map.W;
            if (CarveOne(world, hmap, rng, sx, sy, cfg)) made++;
            sources.Remove(si);
        }
    }

    private static bool CarveOne(World world, float[] hmap, Rng rng, int sx, int sy, WorldGenConfig cfg)
    {
        var map = world.Map;
        int cx = sx, cy = sy, steps = 0;
        var visited = new HashSet<int> { map.Index(cx, cy) };
        while (steps < cfg.RiverMaxSteps)
        {
            int ci = map.Index(cx, cy);
            if (map.Tiles[ci] is Terrain.Water or Terrain.DeepWater && steps > 0) return true;

            map.Tiles[ci] = Terrain.Water;
            if (steps % 9 == 4)   // 弯道处偶尔加宽一格
            {
                int wx = cx + (rng.Chance(0.5f) ? 1 : -1);
                if (map.InBounds(wx, cy))
                {
                    var wt = map.Get(wx, cy);
                    if (wt is not (Terrain.Water or Terrain.DeepWater)) map.Tiles[map.Index(wx, cy)] = Terrain.Water;
                }
            }

            // 选海拔最低的未访问邻居；遇洼地随机破脊继续
            int bx = -1, by = -1;
            float bh = float.MaxValue;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (!map.InBounds(nx, ny)) continue;
                    int ni = map.Index(nx, ny);
                    if (visited.Contains(ni)) continue;
                    float nh = hmap[ni] + (float)rng.NextDouble() * 0.02f;
                    if (nh < bh) { bh = nh; bx = nx; by = ny; }
                }
            if (bx < 0) return false;   // 全邻已访问
            cx = bx; cy = by;
            visited.Add(map.Index(cx, cy));
            steps++;
        }
        return false;
    }
}
