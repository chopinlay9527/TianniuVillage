using TianniuVillage.Core;

public class StorageTests
{
    [Fact]
    public void NewGame_HasVillageCenter()
    {
        var game = Game.NewGame(2024);
        Assert.Contains(game.World.Buildings, b => b.Key == "villagecenter" && b.State == BuildingState.Complete);
    }

    [Fact]
    public void StorageCapacity_SumsBuildings()
    {
        var game = Game.NewGame(2024);
        int baseCap = game.World.StorageCapacity();
        Assert.True(baseCap >= Balance.StorageCapVillageCenter);

        var (cx, cy) = game.World.SettleCenter;
        game.World.Buildings.Add(new Building
        {
            Id = 999, Key = "storehouse", X = cx + 4, Y = cy,
            State = BuildingState.Complete
        });
        Assert.Equal(baseCap + Balance.StorageCapStorehouse, game.World.StorageCapacity());
    }

    [Fact]
    public void Carrying_DepositsIntoStorage()
    {
        var game = Game.NewGame(2024);
        var v = game.Villagers[0];
        game.BeginCarryingForTest(v, "berries", 5);
        int before = game.World.CountItem("berries");

        for (int i = 0; i < 400 && v.CarryLoad.Count > 0; i++) game.Step();

        Assert.Equal(0, v.CarryLoad.Count);
        Assert.Equal(before + 5, game.World.CountItem("berries"));
    }

    [Fact]
    public void FullStorage_StopsEconomy()
    {
        var game = Game.NewGame(2024);
        foreach (var (id, _) in game.World.Stock.ToList()) game.World.Stock[id] = 0;
        game.World.Stock["stone"] = game.World.StorageCapacity();

        game.RunEconomyPlanner();

        Assert.Equal(0, game.Jobs.All.Count(j => j.Kind is JobKind.Forage or JobKind.Fell or JobKind.Mine));
    }

    [Fact]
    public void SpoilGoods_ReducesFoodOverTime()
    {
        var game = Game.NewGame(2024);
        game.World.AddItem("berries", 1000);
        int cap = game.World.StorageCapacity();
        game.World.Stock["berries"] = Math.Min(1000, cap);

        int before = game.World.CountItem("berries");
        game.SpoilGoods();

        Assert.True(game.World.CountItem("berries") < before, "食物应每日腐坏");
    }

    [Fact]
    public void Spoil_LessWithGranary()
    {
        var game = Game.NewGame(2024);
        game.World.AddItem("berries", 1000);
        game.SpoilGoods();
        int withoutGranary = 1000 - game.World.CountItem("berries");

        var game2 = Game.NewGame(2024);
        game2.World.AddItem("berries", 1000);
        var (cx, cy) = game2.World.SettleCenter;
        game2.World.Buildings.Add(new Building
        {
            Id = 998, Key = "granary", X = cx + 4, Y = cy + 4,
            State = BuildingState.Complete
        });
        game2.SpoilGoods();
        int withGranary = 1000 - game2.World.CountItem("berries");

        Assert.True(withGranary < withoutGranary, $"粮仓应减缓腐坏（{withGranary} vs {withoutGranary}）");
    }
}
