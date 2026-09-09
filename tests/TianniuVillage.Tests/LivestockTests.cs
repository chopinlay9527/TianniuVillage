using TianniuVillage.Core;

public class LivestockTests
{
    [Fact]
    public void Pen_ProducesEggs_WhenChickensKept()
    {
        var game = Game.NewGame(2024);
        var (cx, cy) = game.World.SettleCenter;
        var pen = new Building
        {
            Id = 600, Key = "pen", X = cx + 4, Y = cy + 4,
            State = BuildingState.Complete,
            LivestockType = "chicken",
            LivestockCount = 4
        };
        game.World.Buildings.Add(pen);
        game.World.RebuildBlocked();
        game.World.AddItem("grain", 100);

        game.LivestockDailyTick();

        Assert.True(pen.ProdBuffer.GetValueOrDefault("egg") >= Balance.EggsPerChickenPerDay * 4);
    }

    [Fact]
    public void Ranch_ProducesMilkAndWool_ForSheep()
    {
        var game = Game.NewGame(2024);
        var (cx, cy) = game.World.SettleCenter;
        var ranch = new Building
        {
            Id = 601, Key = "ranch", X = cx + 6, Y = cy + 6,
            State = BuildingState.Complete,
            LivestockType = "sheep",
            LivestockCount = 4
        };
        game.World.Buildings.Add(ranch);
        game.World.RebuildBlocked();
        game.World.AddItem("grain", 100);

        game.LivestockDailyTick();

        Assert.True(ranch.ProdBuffer.GetValueOrDefault("milk") >= Balance.MilkPerSheepPerDay * 4);
        Assert.True(ranch.ProdBuffer.GetValueOrDefault("wool") >= 1);
    }

    [Fact]
    public void Livestock_SelfBreed_InRanch()
    {
        var game = Game.NewGame(2024);
        var (cx, cy) = game.World.SettleCenter;
        var ranch = new Building
        {
            Id = 602, Key = "ranch", X = cx + 6, Y = cy + 6,
            State = BuildingState.Complete,
            LivestockType = "sheep",
            LivestockCount = 6
        };
        game.World.Buildings.Add(ranch);
        game.World.RebuildBlocked();
        int before = ranch.LivestockCount;

        bool bred = false;
        for (int seed = 0; seed < 200 && !bred; seed++)
        {
            ranch.ProdBuffer["milk"] = 4;
            var hauler = game.Villagers[1];
            game.Rng.ReSeed(seed);
            var job = game.Jobs.Add(game.World, JobKind.TendLivestock, 78, cx, cy, buildingId: ranch.Id);
            hauler.CurrentJobId = job.Id;
            game.CompleteTendLivestockForTest(hauler, job);
            if (ranch.LivestockCount > before) bred = true;
            if (hauler.CarryLoad.Count > 0) hauler.CarryLoad.Clear();
        }

        Assert.True(bred, "牧场应能自繁衍（10% 概率 × 200 次尝试应至少成功一次）");
    }

    [Fact]
    public void BuildingView_ExposesLivestockToFrontend()
    {
        var game = Game.NewGame(2024);
        var (cx, cy) = game.World.SettleCenter;
        game.World.Buildings.Add(new Building
        {
            Id = 601, Key = "ranch", X = cx + 6, Y = cy + 6,
            State = BuildingState.Complete,
            LivestockType = "sheep",
            LivestockCount = 5
        });

        object update = game.BuildUpdate(0);
        var prop = update.GetType().GetProperty("buildings");
        Assert.NotNull(prop);
        var buildings = (System.Collections.IEnumerable)prop!.GetValue(update)!;
        bool found = false;
        foreach (var b in buildings)
        {
            if (b.GetType().GetProperty("id")!.GetValue(b) is int id && id == 601)
            {
                Assert.Equal("sheep", b.GetType().GetProperty("lt")!.GetValue(b));
                Assert.Equal(5, b.GetType().GetProperty("lc")!.GetValue(b));
                found = true;
                break;
            }
        }
        Assert.True(found, "ranch 未出现在 update 建筑列表");
    }
}
