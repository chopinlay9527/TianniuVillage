namespace TianniuVillage.Core;

/// <summary>
/// 世界生成编排：FastNoiseLite 高度/山脊/湿度场 → <see cref="BiomeClassifier"/> 分层
/// → <see cref="RiverCarver"/> 河流 → <see cref="CoastProcessor"/> 海岸处理
/// → 资源/动物/定居点。
/// </summary>
public static class WorldGenerator
{
    public static World Generate(int seed) => Generate(seed, WorldGenConfig.Default);

    public static World Generate(int seed, WorldGenConfig cfg)
    {
        var world = new World { Seed = seed };
        var map = world.Map;
        var hmap = new float[map.W * map.H];

        var height = new FastNoiseLite(seed);
        height.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        height.SetFractalType(FastNoiseLite.FractalType.FBm);
        height.SetFrequency(cfg.HeightFrequency);
        height.SetFractalOctaves(5);
        height.SetFractalLacunarity(2f);
        height.SetFractalGain(0.5f);

        var ridge = new FastNoiseLite(seed + 3312);
        ridge.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        ridge.SetFractalType(FastNoiseLite.FractalType.Ridged);
        ridge.SetFrequency(cfg.RidgeFrequency);
        ridge.SetFractalOctaves(3);

        var moisture = new FastNoiseLite(seed + 777);
        moisture.SetNoiseType(FastNoiseLite.NoiseType.Perlin);
        moisture.SetFractalType(FastNoiseLite.FractalType.FBm);
        moisture.SetFrequency(cfg.MoistureFrequency);
        moisture.SetFractalOctaves(4);

        for (int y = 0; y < map.H; y++)
        {
            for (int x = 0; x < map.W; x++)
            {
                float edge = EdgeFalloff(x, y, map.W, map.H);
                float h = BiomeClassifier.Normalize(height.GetNoise(x, y), cfg.HeightContrast) * edge;
                float r = BiomeClassifier.Normalize(ridge.GetNoise(x, y), 1f) * edge;
                float m = BiomeClassifier.Normalize(moisture.GetNoise(x, y), 1f);
                hmap[map.Index(x, y)] = h;
                map.Tiles[map.Index(x, y)] = BiomeClassifier.Classify(h, r, m, cfg);
            }
        }

        RiverCarver.Carve(world, seed, hmap, cfg);
        new CoastProcessor(cfg).Process(world);

        SpawnResources(world, seed);
        SpawnAnimals(world, seed);
        world.SettleCenter = FindSettleSite(world, seed);
        EnsureWaterNearSettle(world);
        world.RebuildBlocked();
        return world;
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
