using TianniuVillage.Core;

public class RoadTests
{
    [Fact]
    public void Pathfinder_PrefersRoadDetour()
    {
        var map = new TileMap(24, 24);
        for (int i = 0; i < map.Tiles.Length; i++) map.Tiles[i] = Terrain.Grass;
        for (int y = 10; y <= 14; y++)
            for (int x = 6; x <= 16; x++)
                map.Tiles[map.Index(x, y)] = Terrain.Mountain;
        for (int x = 2; x <= 20; x++) map.Roads[map.Index(x, 18)] = 3;

        var path = PathFinder.Find(map, 2, 12, 20, 12);
        Assert.NotNull(path);

        bool usedRoad = path!.Any(p => map.RoadLevel(p.x, p.y) == 3);
        Assert.True(usedRoad, "寻路应利用石板路绕行");
    }

    [Fact]
    public void RoadSpeed_ReducesMoveCost()
    {
        var map = new TileMap(4, 4);
        for (int i = 0; i < map.Tiles.Length; i++) map.Tiles[i] = Terrain.Grass;
        float plain = map.MoveCost(1, 1);
        map.Roads[map.Index(2, 1)] = 3;
        float road = map.MoveCost(2, 1);

        Assert.True(road < plain * 0.6f, "石板路代价应显著低于草地");
    }

    [Fact]
    public void Traffic_FormsDirtRoad()
    {
        var game = Game.NewGame(2024);
        var (cx, cy) = game.World.SettleCenter;
        int idx = game.World.Map.Index(cx + 1, cy);
        game.World.Traffic[idx] = 500;

        game.RunRoadPlanner();

        Assert.Equal(1, game.World.Map.Roads[idx]);
    }

    [Fact]
    public void DirtRoad_DoesNotFormUnderBuilding()
    {
        var game = Game.NewGame(2024);
        var (cx, cy) = game.World.SettleCenter;
        game.World.Buildings.Add(new Building
        {
            Id = 999, Key = "house", X = cx + 1, Y = cy,
            State = BuildingState.Complete
        });
        int idx = game.World.Map.Index(cx + 2, cy);
        game.World.Traffic[idx] = 500;

        game.RunRoadPlanner();

        Assert.True(game.World.Map.Roads[idx] == 0 || game.World.BuildingAt(cx + 2, cy) != null);
    }

    [Fact]
    public void HighTraffic_DirtRoad_UpgradesViaHauling()
    {
        var game = Game.NewGame(2024);
        var (cx, cy) = game.World.SettleCenter;
        int idx = game.World.Map.Index(cx + 1, cy);
        game.World.Traffic[idx] = 1200;
        game.World.Map.Roads[idx] = 1;
        game.World.AddItem("stone", 20);
        game.World.Buildings.Add(new Building
        {
            Id = 998, Key = "storehouse", X = cx - 2, Y = cy - 2,
            State = BuildingState.Complete
        });

        var hauler = game.Villagers[0];
        foreach (var o in game.Villagers.Where(o => o != hauler))
            o.DecisionCooldown = int.MaxValue;

        game.RunRoadPlanner();

        var seg = Assert.Single(game.World.RoadSegments.Values);
        Assert.Equal(2, seg.TargetLevel);
        Assert.Equal(1, seg.MaterialsRequired);
        Assert.Contains(game.Jobs.All, j => j.Kind == JobKind.HaulStone);

        hauler.Pos = (cx - 1, cy - 1);
        hauler.Satiety = 100; hauler.Energy = 100; hauler.Stamina = 100;
        Assert.True(game.TryClaimBestJob(hauler));
        Assert.NotNull(hauler.CurrentJobId);

        for (int i = 0; i < 200 && seg.MaterialsDelivered < seg.MaterialsRequired; i++)
        {
            if (hauler.Activity == VillagerActivity.WalkingToJob && hauler.Path is { Count: 0 })
            {
                hauler.Satiety = 100; hauler.Energy = 100; hauler.Stamina = 100;
            }
            game.Step();
        }
        Assert.True(seg.MaterialsReady, $"搬运应完成，实际 {seg.MaterialsDelivered}/{seg.MaterialsRequired}");

        game.RunRoadPlanner();
        foreach (var o in game.Villagers) o.DecisionCooldown = int.MaxValue;
        hauler.CurrentJobId = null;

        for (int i = 0; i < 400; i++)
        {
            if (game.World.Map.Roads[idx] == 2) break;
            var buildJob = game.Jobs.All.FirstOrDefault(j => j.Kind == JobKind.BuildRoad && j.SegmentId == seg.Id);
            if (hauler.CurrentJobId == null && buildJob != null && buildJob.State == JobState.Open)
            {
                hauler.Pos = (buildJob.X, buildJob.Y);
                hauler.Satiety = 100; hauler.Energy = 100; hauler.Stamina = 100;
                game.TryClaimBestJob(hauler);
            }
            game.Step();
        }
        Assert.Equal(2, game.World.Map.Roads[idx]);
    }

    [Fact]
    public void WornRoads_Degrade()
    {
        var game = Game.NewGame(2024);
        var (cx, cy) = game.World.SettleCenter;
        int idx = game.World.Map.Index(cx + 1, cy);
        game.World.Map.Roads[idx] = 1;
        game.World.Map.RoadWear[idx] = 500;

        game.DegradeRoads();

        Assert.Equal(0, game.World.Map.Roads[idx]);
        Assert.Contains(idx, game.RoadsChanged);
    }

    [Fact]
    public void RepairJob_ClearsWear()
    {
        var game = Game.NewGame(2024);
        var (cx, cy) = game.World.SettleCenter;
        int idx = game.World.Map.Index(cx + 1, cy);
        game.World.Map.Roads[idx] = 2;
        game.World.Map.RoadWear[idx] = 650;

        game.DegradeRoads();
        game.RunRoadPlanner();

        var job = Assert.Single(game.Jobs.All, j => j.Kind == JobKind.RepairRoad);
        Assert.Equal(idx, job.TileIdx);

        game.World.Map.RoadWear[idx] = 0;
        game.CompleteRoadRepairForTest(job);
        Assert.Equal(0, game.World.Map.RoadWear[idx]);
    }

    [Fact]
    public void SaveLoad_PreservesRoads()
    {
        var game = Game.NewGame(2024);
        var (cx, cy) = game.World.SettleCenter;
        game.World.Map.Roads[game.World.Map.Index(cx + 1, cy)] = 2;
        game.World.Map.RoadWear[game.World.Map.Index(cx + 1, cy)] = 42;
        game.World.Traffic[game.World.Map.Index(cx + 2, cy)] = 333;

        string path = Path.Combine(Path.GetTempPath(), $"tnv_road_{Guid.NewGuid():N}.json");
        SaveService.Save(game, path);
        var loaded = SaveService.Load(path);
        File.Delete(path);

        Assert.Equal(2, loaded.World.Map.Roads[loaded.World.Map.Index(cx + 1, cy)]);
        Assert.Equal(42, loaded.World.Map.RoadWear[loaded.World.Map.Index(cx + 1, cy)]);
        Assert.Equal(333, loaded.World.Traffic[loaded.World.Map.Index(cx + 2, cy)]);
    }
}
