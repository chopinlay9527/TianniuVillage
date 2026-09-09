using TianniuVillage.Core;

public class HaulingTests
{
    [Fact]
    public void BuildingSite_RequiresMaterialHauls()
    {
        var game = Game.NewGame(2024);
        game.World.AddItem("plank", 20);
        game.World.AddItem("stone", 20);
        foreach (var o in game.Villagers) o.DecisionCooldown = int.MaxValue;

        game.RunEconomyPlanner();

        var site = game.World.Buildings.FirstOrDefault(b => b.State == BuildingState.Planned);
        Assert.NotNull(site);
        Assert.NotEmpty(site!.Def.Cost);

        Assert.DoesNotContain(game.Jobs.All, j => j.Kind == JobKind.Build && j.BuildingId == site.Id);
        Assert.Contains(game.Jobs.All, j => j.Kind == JobKind.HaulStone && j.BuildingId == site.Id);

        foreach (var (item, count) in site.Def.Cost)
            site.Delivered[item] = count;

        game.RunEconomyPlanner();

        Assert.True(site.MaterialsReady);
        Assert.Contains(game.Jobs.All, j => j.Kind == JobKind.Build && j.BuildingId == site.Id);
    }

    [Fact]
    public void HaulToBuilding_DeliversMaterial()
    {
        var game = Game.NewGame(2024);
        var (cx, cy) = game.World.SettleCenter;
        game.World.AddItem("plank", 10);
        var b = new Building
        {
            Id = 500, Key = "house", X = cx + 2, Y = cy,
            State = BuildingState.Planned
        };
        game.World.Buildings.Add(b);
        game.World.RebuildBlocked();

        var hauler = game.Villagers[0];
        foreach (var o in game.Villagers.Where(o => o != hauler))
            o.DecisionCooldown = int.MaxValue;

        var job = game.Jobs.Add(game.World, JobKind.HaulStone, 75, cx + 2, cy, buildingId: b.Id);
        job.ItemId = "plank";

        Assert.True(game.TryClaimBestJob(hauler));
        int before = game.World.CountItem("plank");

        for (int i = 0; i < 400 && b.Delivered.GetValueOrDefault("plank") < 1; i++)
        {
            if (hauler.Activity == VillagerActivity.WalkingToJob && hauler.Path is { Count: 0 })
            {
                hauler.Satiety = 100; hauler.Energy = 100; hauler.Stamina = 100;
            }
            game.Step();
        }

        Assert.Equal(1, b.Delivered.GetValueOrDefault("plank"));
        Assert.Equal(before - 1, game.World.CountItem("plank"));
    }
}
