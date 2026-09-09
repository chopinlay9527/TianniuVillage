namespace TianniuVillage.Core;

public static class WorldGenerator
{
    public static World Generate(int seed)
    {
        var world = new World { Seed = seed };
        var map = world.Map;
        var hmap = new float[map.W * map.H];
        for (int y = 0; y < map.H; y++)
        {
            for (int x = 0; x < map.W; x++)
            {
                float nx = x / 22f, ny = y / 22f;
                float edge = EdgeFalloff(x, y, map.W, map.H);
                float h = Noise.Fbm(nx, ny, seed, 5) * edge;
                // 山脊噪声：沿噪声等值线形成 1，配合海拔产生连绵山脉链而非孤立斑点
                float ridge = 1f - MathF.Abs(2f * Noise.Fbm(nx * 0.8f + 40, ny * 0.8f + 90, seed + 3312, 3) - 1f);
                float mr = ridge * edge;
                hmap[map.Index(x, y)] = h;

                float m = Noise.Fbm(nx * 1.7f + 100, ny * 1.7f - 40, seed + 777, 4);
                Terrain t;
                // 海拔分层: 深海→浅水→沙→草→高地(Dirt)→山
                // 高地(Dirt)在草和山之间产生自然过渡带(Wang 拼接)
                if (h < 0.24f) t = Terrain.DeepWater;
                else if (h < 0.34f) t = Terrain.Water;
                else if (h < 0.38f) t = Terrain.Sand;
                else if (h > 0.54f && mr > 0.85f) t = Terrain.Mountain; // 脊线 → 山
                else if (h > 0.51f && mr > 0.72f) t = Terrain.Highland; // 脊线外围 → 高地(Dirt)
                else if (h > 0.76f) t = Terrain.Mountain;                // 极高 → 山
                else if (h > 0.70f) t = Terrain.Highland;                // 高海拔 → 高地
                else if (m > 0.62f) t = Terrain.Forest;
                else t = Terrain.Grass;
                map.Tiles[map.Index(x, y)] = t;
            }
        }

        CarveRivers(world, seed, hmap);
        SpawnResources(world, seed);
        SpawnAnimals(world, seed);
        world.SettleCenter = FindSettleSite(world, seed);
        EnsureWaterNearSettle(world);
        world.RebuildBlocked();
        return world;
    }

    // 河流：从山地/高地源头沿最陡下降走到海洋/湖泊，雕刻水道
    private static void CarveRivers(World world, int seed, float[] hmap)
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
        while (made < 3 && tries < 50 && sources.Count > 0)
        {
            tries++;
            int si = sources[rng.Next(sources.Count)];
            int sx = si % map.W, sy = si / map.W;
            if (CarveOneRiver(world, hmap, rng, sx, sy)) made++;
            sources.Remove(si);
        }
    }

    private static bool CarveOneRiver(World world, float[] hmap, Rng rng, int sx, int sy)
    {
        var map = world.Map;
        int cx = sx, cy = sy, steps = 0;
        var visited = new HashSet<int> { map.Index(cx, cy) };
        while (steps < 500)
        {
            int ci = map.Index(cx, cy);
            if (map.Tiles[ci] is Terrain.Water or Terrain.DeepWater && steps > 0) return true;

            map.Tiles[ci] = Terrain.Water;
            // 弯道处偶尔加宽一格
            if (steps % 9 == 4)
            {
                int wx = cx + (rng.Chance(0.5f) ? 1 : -1);
                if (map.InBounds(wx, cy) && map.Tiles[map.Index(wx, cy)] is Terrain.Grass or Terrain.Forest or Terrain.Sand or Terrain.Highland)
                    map.Tiles[map.Index(wx, cy)] = Terrain.Water;
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
            if (bx < 0) return false; // 全邻已访问
            cx = bx; cy = by;
            visited.Add(map.Index(cx, cy));
            steps++;
        }
        return false;
    }

    private static float EdgeFalloff(int x, int y, int w, int h)
    {
        float dx = MathF.Min(x, w - 1 - x) / (w * 0.5f);
        float dy = MathF.Min(y, h - 1 - y) / (h * 0.5f);
        float d = MathF.Min(1f, MathF.Min(dx, dy) * 6f);
        return d;
    }

    private static void SpawnResources(World world, int seed)
    {
        var map = world.Map;
        var rng = new Rng(seed ^ 0x5eed);
        for (int y = 1; y < map.H - 1; y++)
        {
            for (int x = 1; x < map.W - 1; x++)
            {
                var t = map.Get(x, y);
                float r = (float)rng.NextDouble();
                switch (t)
                {
                    case Terrain.Forest:
                        if (r < 0.42f) AddNode(world, ResKind.Tree, x, y, rng.Next(80, 160));
                        else if (r < 0.45f) AddNode(world, ResKind.MushroomPatch, x, y, 3);
                        else if (r < 0.46f) AddNode(world, ResKind.HerbPatch, x, y, 2);
                        break;
                    case Terrain.Grass:
                        if (r < 0.012f) AddNode(world, ResKind.Tree, x, y, rng.Next(80, 160));
                        else if (r < 0.024f) AddNode(world, ResKind.BerryBush, x, y, 4);
                        else if (r < 0.030f) AddNode(world, ResKind.HerbPatch, x, y, 2);
                        else if (r < 0.042f) AddNode(world, ResKind.FlaxPatch, x, y, 3);
                        break;
                    case Terrain.Highland:
                        if (r < 0.07f) AddNode(world, ResKind.StoneOutcrop, x, y, rng.Next(30, 60));
                        else if (r < 0.08f) AddNode(world, ResKind.Tree, x, y, rng.Next(60, 120));
                        else if (r < 0.085f) AddNode(world, ResKind.CopperVein, x, y, rng.Next(80, 150));
                        else if (r < 0.088f) AddNode(world, ResKind.IronVein, x, y, rng.Next(60, 120));
                        break;
                    case Terrain.Mountain:
                        if (r < 0.03f) AddNode(world, ResKind.CopperVein, x, y, rng.Next(100, 200));
                        else if (r < 0.05f) AddNode(world, ResKind.IronVein, x, y, rng.Next(80, 160));
                        break;
                }
            }
        }

        for (int y = 1; y < map.H - 1; y++)
        {
            for (int x = 1; x < map.W - 1; x++)
            {
                if (map.Get(x, y) == Terrain.Water && AdjacentLand(map, x, y))
                {
                    float r2 = (float)rng.NextDouble();
                    if (r2 < 0.05f) AddNode(world, ResKind.FishSpot, x, y, 9999);
                    else if (r2 < 0.13f) AddNode(world, ResKind.WaterSpot, x, y, 9999);
                }
            }
        }
    }

    private static bool AdjacentLand(TileMap map, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (map.InBounds(nx, ny) && map.Get(nx, ny) is Terrain.Grass or Terrain.Forest or Terrain.Sand)
                    return true;
            }
        return false;
    }

    private static void AddNode(World world, ResKind kind, int x, int y, int amount)
    {
        int idx = world.Map.Index(x, y);
        if (world.Resources.ContainsKey(idx)) return;
        world.Resources[idx] = new ResourceNode
        {
            Id = world.NextNodeId++,
            Kind = kind,
            X = x,
            Y = y,
            Amount = amount
        };
    }

    private static void EnsureWaterNearSettle(World world)
    {
        var (cx, cy) = world.SettleCenter;
        bool hasSpot = world.Resources.Values.Any(n =>
            n.Kind == ResKind.WaterSpot && Math.Abs(n.X - cx) <= 25 && Math.Abs(n.Y - cy) <= 25);
        if (hasSpot) return;

        (int x, int y)? bestShore = null;
        float bestDist = float.MaxValue;
        var map = world.Map;
        for (int y = 1; y < map.H - 1; y++)
            for (int x = 1; x < map.W - 1; x++)
            {
                if (map.Get(x, y) != Terrain.Water || !AdjacentLand(map, x, y)) continue;
                float d = (x - cx) * (x - cx) + (y - cy) * (y - cy);
                if (d < bestDist) { bestDist = d; bestShore = (x, y); }
            }
        if (bestShore.HasValue)
        {
            world.Resources[map.Index(bestShore.Value.x, bestShore.Value.y)] = new ResourceNode
            {
                Id = world.NextNodeId++,
                Kind = ResKind.WaterSpot,
                X = bestShore.Value.x,
                Y = bestShore.Value.y,
                Amount = 9999
            };
        }
    }

    private static void SpawnAnimals(World world, int seed)
    {
        var map = world.Map;
        var rng = new Rng(seed ^ 0xA11a);
        for (int i = 0; i < 18; i++)
        {
            var p = RandomLandTile(map, rng, t => t is Terrain.Forest or Terrain.Grass);
            if (p.HasValue) world.Animals.Add(new Animal { Id = world.NextAnimalId++, Kind = "deer", X = p.Value.x, Y = p.Value.y });
        }
        for (int i = 0; i < 26; i++)
        {
            var p = RandomLandTile(map, rng, t => t is Terrain.Grass);
            if (p.HasValue) world.Animals.Add(new Animal { Id = world.NextAnimalId++, Kind = "rabbit", X = p.Value.x, Y = p.Value.y });
        }
    }

    public static (int x, int y)? RandomLandTile(TileMap map, Rng rng, Func<Terrain, bool> filter)
    {
        for (int attempt = 0; attempt < 200; attempt++)
        {
            int x = rng.Next(8, map.W - 8), y = rng.Next(8, map.H - 8);
            if (filter(map.Get(x, y)) && map.Walkable(x, y)) return (x, y);
        }
        return null;
    }

    private static (int x, int y) FindSettleSite(World world, int seed)
    {
        var map = world.Map;
        var rng = new Rng(seed ^ 0x51e);
        int bestScore = int.MinValue;
        int bx = map.W / 2, by = map.H / 2;

        for (int attempt = 0; attempt < 600; attempt++)
        {
            int x = rng.Next(30, map.W - 30), y = rng.Next(30, map.H - 30);
            if (map.Get(x, y) != Terrain.Grass) continue;
            int score = 0;
            bool waterNear = false;
            for (int dy = -8; dy <= 8; dy++)
                for (int dx = -8; dx <= 8; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (!map.InBounds(nx, ny)) continue;
                    var t = map.Get(nx, ny);
                    if (t is Terrain.Grass) score += 2;
                    if (t is Terrain.Forest) score += 1;
                    if (t is Terrain.Water) waterNear = true;
                }
            if (!waterNear) score -= 200;
            score -= Math.Abs(x - map.W / 2) + Math.Abs(y - map.H / 2);
            if (score > bestScore) { bestScore = score; bx = x; by = y; }
        }
        return (bx, by);
    }
}
