using TianniuVillage.Core;

public class SimulationTests
{
    [Fact]
    public void Villagers_SurviveFirstSeason()
    {
        var game = Game.NewGame(20260908);
        int targetTick = Balance.DaysPerSeason * Balance.MinutesPerDay;

        for (int i = 0; i < targetTick && game.Villagers.Any(v => v.Alive); i++)
            game.Step();

        int alive = game.Villagers.Count(v => v.Alive);
        Assert.True(alive >= Balance.InitialVillagers - 1,
            $"第一个季节后应有 {Balance.InitialVillagers - 1}+ 人存活，实际 {alive}");
    }

    [Fact]
    public void Villagers_SurviveFirstSeason_Seed2024()
    {
        var game = Game.NewGame(2024);
        game.World.AddItem("berries", 30);
        game.World.AddItem("water", 10);
        int targetTick = Balance.DaysPerSeason * Balance.MinutesPerDay;

        for (int i = 0; i < targetTick && game.Villagers.Any(v => v.Alive); i++)
            game.Step();

        int alive = game.Villagers.Count(v => v.Alive);
        Assert.True(alive >= Balance.InitialVillagers - 1,
            $"第一个季节后应有 {Balance.InitialVillagers - 1}+ 人存活，实际 {alive}");
    }

    [Fact]
    public void Satiety_DecaysOverTime()
    {
        var game = Game.NewGame(7);
        var v = game.Villagers[0];
        float before = v.Satiety;

        for (int i = 0; i < 600; i++) game.Step();

        Assert.True(v.Satiety < before, "饱食度应随时间下降");
    }

    [Fact]
    public void Villagers_GatherFood()
    {
        var game = Game.NewGame(99);
        int targetTick = 10 * Balance.MinutesPerDay;

        for (int i = 0; i < targetTick; i++) game.Step();

        Assert.True(game.CountFood() > 0, "10 天后村庄应有食物入库");
    }

    [Fact]
    public void Houses_GetBuilt()
    {
        var game = Game.NewGame(1234);
        int targetTick = 30 * Balance.MinutesPerDay;

        for (int i = 0; i < targetTick; i++) game.Step();

        Assert.True(game.World.Buildings.Any(b => b.State == BuildingState.Complete),
            "一个月内应有建筑落成");
    }
}
