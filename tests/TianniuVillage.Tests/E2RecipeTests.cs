using TianniuVillage.Core;

public class E2RecipeTests
{
    private static void FastForwardPlanning(Game game)
    {
        // RunEconomyPlanner 每 5 tick 一次；跑 10 tick 确保至少触发一轮
        for (int i = 0; i < 10; i++) game.Step();
    }

    [Fact]
    public void Planner_PostsCheeseJob_WhenMilkSurplus()
    {
        var game = Game.NewGame(77);
        var (cx, cy) = game.World.SettleCenter;
        game.World.Buildings.Add(new Building
        { Id = 900, Key = "cookhouse", X = cx + 3, Y = cy + 3, State = BuildingState.Complete });
        game.World.RebuildBlocked();
        // 堵住熟食路线：无谷物 → 只能走奶酪
        game.World.Stock.Remove("grain");
        game.World.AddItem("milk", 10);

        FastForwardPlanning(game);

        var cookJob = game.Jobs.All.FirstOrDefault(j => j.Kind == JobKind.Cook && j.State != JobState.Done);
        Assert.NotNull(cookJob);
        Assert.Equal("cheese", cookJob!.ItemId);
    }

    [Fact]
    public void Planner_PostsJerkyJob_WhenMeatSurplus()
    {
        var game = Game.NewGame(78);
        var (cx, cy) = game.World.SettleCenter;
        game.World.Buildings.Add(new Building
        { Id = 901, Key = "cookhouse", X = cx + 3, Y = cy + 3, State = BuildingState.Complete });
        game.World.RebuildBlocked();
        game.World.Stock.Remove("grain");
        game.World.Stock.Remove("milk");
        game.World.AddItem("meat", 10);
        game.World.AddItem("log", 2);

        FastForwardPlanning(game);

        var cookJob = game.Jobs.All.FirstOrDefault(j => j.Kind == JobKind.Cook && j.State != JobState.Done);
        Assert.NotNull(cookJob);
        Assert.Equal("jerky", cookJob!.ItemId);
    }

    [Fact]
    public void Planner_PostsHideCoatJob_InAutumnWithHide()
    {
        var game = Game.NewGame(79);
        var (cx, cy) = game.World.SettleCenter;
        game.World.Buildings.Add(new Building
        { Id = 902, Key = "weaver", X = cx + 3, Y = cy + 3, State = BuildingState.Complete });
        game.World.RebuildBlocked();
        game.World.Stock.Remove("fiber");
        game.World.AddItem("hide", 5);
        game.Tick = 60 * Balance.MinutesPerDay + 2; // 秋季第 1 天

        FastForwardPlanning(game);

        var weaveJob = game.Jobs.All.FirstOrDefault(j => j.Kind == JobKind.Weave && j.State != JobState.Done);
        Assert.NotNull(weaveJob);
        Assert.Equal("hide_coat", weaveJob!.ItemId);
    }

    [Fact]
    public void WinterClothes_AcceptsHideCoat()
    {
        var game = Game.NewGame(80);
        game.World.Stock.Remove("clothes");
        game.World.AddItem("hide_coat", 3);
        // 推进到冬季第一天，DailyTick 会触发 DistributeWinterClothes
        while (game.Season != Season.Winter && game.Tick < 200 * Balance.MinutesPerDay) game.Step();
        int warm = game.Villagers.Count(v => v.Alive && v.WinterClothes);
        Assert.True(warm > 0, $"毛皮大衣应能充当冬衣，实际保暖 {warm} 人");
    }
}
