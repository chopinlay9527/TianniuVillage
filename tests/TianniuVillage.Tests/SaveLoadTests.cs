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
