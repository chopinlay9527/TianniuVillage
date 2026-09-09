using TianniuVillage.Core;

public class SaveLoadTests
{
    [Fact]
    public void SaveLoad_Roundtrip_PreservesState()
    {
        var game = Game.NewGame(555);
        for (int i = 0; i < 800; i++) game.Step();

        string path = Path.Combine(Path.GetTempPath(), $"tianniu_test_{Guid.NewGuid():N}.json");
        SaveService.Save(game, path);

        var loaded = SaveService.Load(path);
        File.Delete(path);

        Assert.Equal(game.Tick, loaded.Tick);
        Assert.Equal(game.Villagers.Count, loaded.Villagers.Count);
        Assert.Equal(game.World.Buildings.Count, loaded.World.Buildings.Count);
        Assert.Equal(game.World.Stock.Count, loaded.World.Stock.Count);
        Assert.Equal(game.World.Seed, loaded.World.Seed);

        var v0 = game.Villagers[0];
        var v1 = loaded.Villagers[0];
        Assert.Equal(v0.Id, v1.Id);
        Assert.Equal(v0.Name, v1.Name);
        Assert.Equal(v0.Pos, v1.Pos);
        Assert.Equal(v0.Satiety, v1.Satiety, 3);
        Assert.Equal(v0.FavoriteFood, v1.FavoriteFood);
        Assert.Equal(v0.HatedFood, v1.HatedFood);
        Assert.Equal(v0.Role, v1.Role);
        Assert.Equal(v0.Disease, v1.Disease);
        Assert.Equal(v0.Injury, v1.Injury);
        Assert.Equal(v0.Reputation, v1.Reputation, 3);
        Assert.Equal(game.LastFestivalDay, loaded.LastFestivalDay);
        Assert.Equal(game.LastMerchantDay, loaded.LastMerchantDay);
    }

    [Fact]
    public void SaveLoad_FestivalAndMerchant_Roundtrip()
    {
        var game = Game.NewGame(777);
        game.CurrentFestival = new Festival
        {
            Type = FestivalType.Harvest,
            Tick = game.Tick,
            DurationTicks = 240,
            Description = "丰收庆典"
        };
        game.Merchant = new MerchantVisit
        {
            ArrivalTick = game.Tick,
            DepartureTick = game.Tick + 300,
            Active = true,
            Offers = [("hide", 10, "stone", 8)]
        };

        string path = Path.Combine(Path.GetTempPath(), $"tianniu_test_{Guid.NewGuid():N}.json");
        SaveService.Save(game, path);
        var loaded = SaveService.Load(path);
        File.Delete(path);

        Assert.NotNull(loaded.CurrentFestival);
        Assert.Equal(FestivalType.Harvest, loaded.CurrentFestival.Type);
        Assert.Equal("丰收庆典", loaded.CurrentFestival.Description);
        Assert.Equal(240, loaded.CurrentFestival.DurationTicks);
        Assert.NotNull(loaded.Merchant);
        Assert.True(loaded.Merchant.Active);
        Assert.Single(loaded.Merchant.Offers);
        Assert.Equal("hide", loaded.Merchant.Offers[0].item);
        Assert.Equal(8, loaded.Merchant.Offers[0].wantCount);
    }

    [Fact]
    public void LoadedGame_ContinuesToSimulate()
    {
        var game = Game.NewGame(888);
        for (int i = 0; i < 500; i++) game.Step();

        string path = Path.Combine(Path.GetTempPath(), $"tianniu_test_{Guid.NewGuid():N}.json");
        SaveService.Save(game, path);
        var loaded = SaveService.Load(path);
        File.Delete(path);

        int popBefore = loaded.Villagers.Count(v => v.Alive);
        for (int i = 0; i < 100; i++) loaded.Step();
        int popAfter = loaded.Villagers.Count(v => v.Alive);

        Assert.True(popAfter <= popBefore);
    }
}
