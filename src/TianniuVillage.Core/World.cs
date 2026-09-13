namespace TianniuVillage.Core;

public sealed class World
{
    public int Seed;
    public TileMap Map = new(Balance.MapW, Balance.MapH);
    public Dictionary<int, ResourceNode> Resources = new();
    public List<Building> Buildings = [];
    public List<Animal> Animals = [];
    public Dictionary<string, int> Stock = new();
    public Dictionary<string, long> Produced = new();
    public Dictionary<string, long> Consumed = new();
    public bool WinterClothesAssigned;
    public bool TorchLit;
    public HashSet<string> Researched = new();
    public string? CurrentTech;
    public float ResearchProgress;
    public HashSet<int> Researchers = new();
    public Dictionary<int, int> Traffic = new();
    public Dictionary<int, RoadSegment> RoadSegments = new();
    public int NextSegmentId = 1;
    public int NextNodeId = 1;
    public int NextBuildingId = 1;
    public int NextVillagerId = 1;
    public int NextAnimalId = 1;
    public int NextJobId = 1;
    public (int x, int y) SettleCenter;

    public int CountItem(string id) => Stock.TryGetValue(id, out var n) ? n : 0;

    public int StorageUsed() => Stock.Values.Sum();

    public int StorageCapacity()
    {
        int cap = 0;
        foreach (var b in Buildings)
        {
            if (b.State != BuildingState.Complete) continue;
            cap += b.Key switch
            {
                "villagecenter" => Balance.StorageCapVillageCenter,
                "storehouse" => Balance.StorageCapStorehouse,
                "granary" => Balance.StorageCapGranary,
                _ => 0
            };
        }
        return cap;
    }

    public bool StorageFull => StorageUsed() >= StorageCapacity();

    public TechDef? NextTech => TechDefs.All.FirstOrDefault(t =>
        !Researched.Contains(t.Id) && (t.Prereq == null || Researched.Contains(t.Prereq)));

    public void AddItem(string id, int count)
    {
        Stock[id] = CountItem(id) + count;
        Produced[id] = Produced.GetValueOrDefault(id) + count;
    }

    public bool TryTakeItem(string id, int count)
    {
        if (CountItem(id) < count) return false;
        Stock[id] = CountItem(id) - count;
        Consumed[id] = Consumed.GetValueOrDefault(id) + count;
        return true;
    }

    public Building? BuildingAt(int x, int y)
    {
        foreach (var b in Buildings)
        {
            if (b.State == BuildingState.Ruined) continue;
            if (x >= b.X && x < b.X + b.W && y >= b.Y && y < b.Y + b.H) return b;
        }
        return null;
    }

    public IEnumerable<Building> BuildingsOf(string key, BuildingState minState = BuildingState.Complete)
    {
        foreach (var b in Buildings)
            if (b.Key == key && b.State >= minState)
                yield return b;
    }

    public void RebuildBlocked()
    {
        Array.Clear(Map.Blocked);
        foreach (var n in Resources.Values)
            if (n.Kind == ResKind.StoneOutcrop)
                Map.Blocked[Map.Index(n.X, n.Y)] = true;
        foreach (var b in Buildings)
        {
            if (b.Key == "farm") continue;
            if (b.State == BuildingState.Ruined) continue;
            BlockCells(b, true);
        }
    }

    private void BlockCells(Building b, bool blocked)
    {
        for (int dy = 0; dy < b.H; dy++)
            for (int dx = 0; dx < b.W; dx++)
            {
                int x = b.X + dx, y = b.Y + dy;
                if (Map.InBounds(x, y)) Map.Blocked[Map.Index(x, y)] = blocked;
            }
    }

    public (int x, int y) DoorOf(Building b)
    {
        for (int r = 1; r <= 4; r++)
        {
            for (int dy = -r; dy <= b.H - 1 + r; dy++)
                for (int dx = -r; dx <= b.W - 1 + r; dx++)
                {
                    bool onRing = dx == -r || dx == b.W - 1 + r || dy == -r || dy == b.H - 1 + r;
                    if (!onRing) continue;
                    int x = b.X + dx, y = b.Y + dy;
                    if (Map.Walkable(x, y) && BuildingAt(x, y) == null) return (x, y);
                }
        }
        return (b.CenterX, b.CenterY);
    }
}
