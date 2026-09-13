using TianniuVillage.Core;

public class RefundTests
{
    private static Game AdjustedGame()
    {
        var g = Game.NewGame(2024);
        foreach (var key in g.World.Stock.Keys.ToList()) g.World.Stock[key] = 0;
        return g;
    }

    private static void StandOnLand(Game game, Villager v, out int x, out int y)
    {
        (x, y) = (0, 0);
        var (cx, cy) = game.World.SettleCenter;
        for (int r = 1; r < 8; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = cx + dx, ny = cy + dy;
                    if (game.World.Map.Walkable(nx, ny) && game.World.BuildingAt(nx, ny) == null)
                    {
                        (x, y) = (nx, ny);
                        v.Pos = (nx, ny);
                        v.Path = null;
                        return;
                    }
                }
    }

    [Fact]
    public void SmeltClaim_PartialStock_RefundsTakenOre()
    {
        var game = AdjustedGame();
        game.World.Stock["copper_ore"] = 2;

        var v = game.Villagers[0];
        var job = game.Jobs.Add(game.World, JobKind.Smelt, 90, v.Pos.x, v.Pos.y);
        job.ItemId = "copper_ore";

        Assert.False(game.TryClaimBestJob(v));
        Assert.Equal(2, game.World.CountItem("copper_ore"));
        Assert.DoesNotContain(game.Jobs.All, j => j.Id == job.Id);
    }

    [Fact]
    public void CraftToolClaim_PartialStock_RefundsTakenMetal()
    {
        var game = AdjustedGame();
        game.World.Stock["copper"] = 1;

        var v = game.Villagers[0];
        var job = game.Jobs.Add(game.World, JobKind.CraftTool, 90, v.Pos.x, v.Pos.y);
        job.ItemId = "copper_tool";

        Assert.False(game.TryClaimBestJob(v));
        Assert.Equal(1, game.World.CountItem("copper"));
        Assert.DoesNotContain(game.Jobs.All, j => j.Id == job.Id);
    }

    [Fact]
    public void CookClaim_ConsumesActualProteinAndRefundsSameType()
    {
        var game = AdjustedGame();
        game.World.Stock["fish"] = 2;
        game.World.Stock["grain"] = 1;
        game.World.Stock["water"] = 1;

        var v = game.Villagers[0];
        StandOnLand(game, v, out var jx, out var jy);
        var job = game.Jobs.Add(game.World, JobKind.Cook, 90, jx, jy);

        Assert.True(game.TryClaimBestJob(v));
        Assert.Equal("fish", job.ConsumedItem);
        Assert.Equal(0, game.World.CountItem("fish"));

        game.RefundCookIngredients(job);
        Assert.Equal(2, game.World.CountItem("fish"));
        Assert.Equal(1, game.World.CountItem("grain"));
        Assert.Equal(1, game.World.CountItem("water"));
        Assert.Equal(0, game.World.CountItem("berries"));
    }

    [Fact]
    public void CookRefund_FallsBackToBerries_WhenConsumedItemMissing()
    {
        var game = AdjustedGame();
        var job = game.Jobs.Add(game.World, JobKind.Cook, 90, 0, 0);

        game.RefundCookIngredients(job);

        Assert.Equal(1, game.World.CountItem("grain"));
        Assert.Equal(2, game.World.CountItem("berries"));
        Assert.Equal(1, game.World.CountItem("water"));
    }

    [Fact]
    public void WeaveClaim_AcceptsWool_AndRefundsWool()
    {
        var game = AdjustedGame();
        game.World.Stock["wool"] = 2;

        var v = game.Villagers[0];
        StandOnLand(game, v, out var jx, out var jy);
        var job = game.Jobs.Add(game.World, JobKind.Weave, 90, jx, jy);

        Assert.True(game.TryClaimBestJob(v));
        Assert.Equal("wool", job.ConsumedItem);
        Assert.Equal(0, game.World.CountItem("wool"));

        game.RefundWeaveMaterials(job);
        Assert.Equal(2, game.World.CountItem("wool"));
    }
}