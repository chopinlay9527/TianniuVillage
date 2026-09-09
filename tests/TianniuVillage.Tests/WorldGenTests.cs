using TianniuVillage.Core;

public class WorldGenTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalWorld()
    {
        var a = WorldGenerator.Generate(12345);
        var b = WorldGenerator.Generate(12345);

        Assert.Equal(a.Map.Tiles, b.Map.Tiles);
        Assert.Equal(a.SettleCenter, b.SettleCenter);
        Assert.Equal(a.Resources.Count, b.Resources.Count);
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentWorld()
    {
        var a = WorldGenerator.Generate(1);
        var b = WorldGenerator.Generate(2);

        Assert.NotEqual(a.Map.Tiles, b.Map.Tiles);
    }

    [Fact]
    public void SettleSite_IsWalkableAndHasWaterNearby()
    {
        var world = WorldGenerator.Generate(777);
        var (x, y) = world.SettleCenter;
        Assert.True(world.Map.Walkable(x, y));

        bool waterNear = false;
        for (int dy = -10; dy <= 10 && !waterNear; dy++)
            for (int dx = -10; dx <= 10; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (world.Map.InBounds(nx, ny) && world.Map.Get(nx, ny) is Terrain.Water or Terrain.DeepWater)
                {
                    waterNear = true;
                    break;
                }
            }
        Assert.True(waterNear, "定居点附近应有水源");
    }
}
