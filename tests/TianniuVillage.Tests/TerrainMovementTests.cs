using TianniuVillage.Core;

public class TerrainMovementTests
{
    private static TileMap FlatMap()
    {
        var map = new TileMap(20, 20);
        for (int i = 0; i < map.Tiles.Length; i++) map.Tiles[i] = Terrain.Grass;
        return map;
    }

    [Fact]
    public void Water_IsSwimmable_AtPenalty()
    {
        var map = FlatMap();
        map.Tiles[map.Index(5, 5)] = Terrain.Water;
        float grass = map.MoveCost(6, 5);
        float swim = map.MoveCost(5, 5);
        Assert.True(map.Walkable(5, 5), "浅水可游泳");
        Assert.True(swim > grass * 2.5f, $"游泳应约 3 倍慢（{swim} vs {grass}）");
    }

    [Fact]
    public void Mountain_IsClimbable_AtPenalty()
    {
        var map = FlatMap();
        map.Tiles[map.Index(5, 5)] = Terrain.Mountain;
        float grass = map.MoveCost(6, 5);
        float climb = map.MoveCost(5, 5);
        Assert.True(map.Walkable(5, 5), "山可翻越");
        Assert.True(climb > grass * 4f, $"翻山应约 5 倍慢（{climb} vs {grass}）");
    }

    [Fact]
    public void DeepWater_IsImpassable()
    {
        var map = FlatMap();
        map.Tiles[map.Index(5, 5)] = Terrain.DeepWater;
        Assert.False(map.Walkable(5, 5));
    }

    [Fact]
    public void Buildings_BlockMovement()
    {
        var world = WorldGenerator.Generate(2024);
        var (cx, cy) = world.SettleCenter;
        var b = new Building { Id = 1, Key = "house", X = cx + 1, Y = cy, State = BuildingState.Complete };
        world.Buildings.Add(b);
        world.RebuildBlocked();

        Assert.False(world.Map.Walkable(cx + 1, cy), "住宅占格不可通行");
        var door = world.DoorOf(b);
        Assert.True(world.Map.Walkable(door.x, door.y), "门口必须可走");
        Assert.True(door.x < b.X || door.x >= b.X + b.W || door.y < b.Y || door.y >= b.Y + b.H, "门口在建筑外");
    }

    [Fact]
    public void FarmFootprint_StaysWalkable()
    {
        var world = WorldGenerator.Generate(2024);
        var (cx, cy) = world.SettleCenter;
        var b = new Building { Id = 1, Key = "farm", X = cx + 1, Y = cy, State = BuildingState.Complete, CropPhase = new int[9] };
        world.Buildings.Add(b);
        world.RebuildBlocked();

        Assert.True(world.Map.Walkable(cx + 2, cy), "农田格保持可走以便耕作");
    }

    [Fact]
    public void StoneOutcrop_BlocksUntilDepleted()
    {
        var world = WorldGenerator.Generate(2024);
        var (cx, cy) = world.SettleCenter;
        int idx = world.Map.Index(cx + 2, cy);
        world.Resources[idx] = new ResourceNode { Id = 99, Kind = ResKind.StoneOutcrop, X = cx + 2, Y = cy, Amount = 30 };
        world.RebuildBlocked();
        Assert.False(world.Map.Walkable(cx + 2, cy), "岩石露头阻挡");

        world.Resources.Remove(idx);
        world.RebuildBlocked();
        Assert.True(world.Map.Walkable(cx + 2, cy), "采空后恢复通行");
    }

    [Fact]
    public void Pathfinder_RoutesAroundBuilding()
    {
        var world = WorldGenerator.Generate(2024);
        var (cx, cy) = world.SettleCenter;
        var b = new Building { Id = 1, Key = "house", X = cx, Y = cy - 1, State = BuildingState.Complete };
        world.Buildings.Add(b);
        world.RebuildBlocked();

        var path = PathFinder.Find(world.Map, cx - 2, cy, cx + 2, cy);
        Assert.NotNull(path);
        foreach (var (x, y) in path!)
            Assert.False(x >= b.X && x < b.X + b.W && y >= b.Y && y < b.Y + b.H, "路径不得穿过建筑");
    }

    [Fact]
    public void Winter_MakesSwimmingCostlier()
    {
        var map = FlatMap();
        map.Tiles[map.Index(5, 5)] = Terrain.Water;
        map.WaterCostMul = 1f;
        float summer = map.MoveCost(5, 5);
        map.WaterCostMul = 2f;
        float winter = map.MoveCost(5, 5);
        Assert.True(winter > summer * 1.5f, "冬季游泳代价应翻倍");
    }
}
