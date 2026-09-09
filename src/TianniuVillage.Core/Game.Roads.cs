namespace TianniuVillage.Core;

public sealed partial class Game
{
    public void RunRoadPlanner()
    {
        FormDirtRoads();
        PlanRoadSegments();
        PostHaulJobs();
        PostRoadBuildJobs();
        PostRepairJobs();
    }

    private void FormDirtRoads()
    {
        int formed = 0;
        foreach (var (idx, count) in World.Traffic)
        {
            if (formed >= Balance.RoadMaxFormPerHour) break;
            if (World.Map.Roads[idx] != 0) continue;
            int x = idx % World.Map.W, y = idx / World.Map.W;
            if (!World.Map.Walkable(x, y) || !World.Map.IsLand(x, y)) continue;
            if (World.BuildingAt(x, y) != null) continue;
            if (!IsRoadAnchored(x, y)) continue;

            // 桥接：流量未满阈值但两侧都有路（分流空洞/单格缺口）时提前成路
            bool fullTraffic = count >= Balance.RoadTrafficDirt;
            bool bridgeGap = !fullTraffic
                && count >= Balance.RoadDirtBridgeTraffic
                && RoadNeighborCount(x, y) >= 2;
            if (!fullTraffic && !bridgeGap) continue;

            World.Map.Roads[idx] = 1;
            World.Map.RoadWear[idx] = 0;
            RoadsChanged.Add(idx);
            formed++;
            if (formed == 1)
                Log("被乡亲们反复踩踏的小径，渐渐踏出了一条土路", LogSeverity.Normal);
        }
    }

    private int RoadNeighborCount(int x, int y)
    {
        int n = 0;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (World.Map.InBounds(nx, ny) && World.Map.Roads[World.Map.Index(nx, ny)] > 0) n++;
            }
        return n;
    }

    private bool IsRoadAnchored(int x, int y)
    {
        if (Math.Abs(x - World.SettleCenter.x) <= 3 && Math.Abs(y - World.SettleCenter.y) <= 3) return true;
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (!World.Map.InBounds(nx, ny)) continue;
                if (World.Map.Roads[World.Map.Index(nx, ny)] > 0) return true;
            }
        foreach (var b in World.Buildings)
        {
            if (b.State == BuildingState.Ruined) continue;
            if (x >= b.X - 1 && x <= b.X + b.W && y >= b.Y - 1 && y <= b.Y + b.H) return true;
        }
        return false;
    }

    private int NextRoadThreshold(byte level) => level switch
    {
        1 => Balance.RoadTrafficGravel,
        2 => Balance.RoadTrafficStone,
        _ => int.MaxValue
    };

    private HashSet<int> ActiveSegmentTiles()
    {
        var set = new HashSet<int>();
        foreach (var seg in World.RoadSegments.Values)
            foreach (var t in seg.Tiles) set.Add(t);
        return set;
    }

    private void PlanRoadSegments()
    {
        if (World.RoadSegments.Count >= Balance.RoadMaxActiveSegments) return;
        if (!World.BuildingsOf("storehouse").Any()) return;

        var busy = ActiveSegmentTiles();
        var seeds = new List<(int idx, int traffic)>();
        foreach (var (idx, traffic) in World.Traffic)
        {
            byte lvl = World.Map.Roads[idx];
            if (lvl is not (1 or 2)) continue;
            if (traffic < NextRoadThreshold(lvl)) continue;
            if (busy.Contains(idx)) continue;
            seeds.Add((idx, traffic));
        }
        if (seeds.Count == 0) return;
        seeds.Sort((a, b) => b.traffic.CompareTo(a.traffic));

        foreach (var (seedIdx, _) in seeds)
        {
            byte level = World.Map.Roads[seedIdx];
            var tiles = GrowRoadSegment(seedIdx, level, busy);
            if (tiles.Count == 0) continue;

            int required = level == 1 ? (tiles.Count + 1) / 2 : tiles.Count;
            if (World.CountItem("stone") < required) continue;

            var seg = new RoadSegment
            {
                Id = World.NextSegmentId++,
                Tiles = tiles,
                TargetLevel = (byte)(level + 1),
                MaterialsRequired = required
            };
            World.RoadSegments[seg.Id] = seg;
            string roadZh = seg.TargetLevel == 2 ? "碎石路" : "石板路";
            Log($"这条路人来人往，村民们决定把它铺成{roadZh}（需石料×{required}）", LogSeverity.Normal);
            return;
        }
    }

    private List<int> GrowRoadSegment(int seedIdx, byte level, HashSet<int> busy)
    {
        var tiles = new List<int> { seedIdx };
        var visited = new HashSet<int> { seedIdx };
        int threshold = NextRoadThreshold(level);
        int w = World.Map.W;

        while (tiles.Count < Balance.RoadMaxSegmentTiles)
        {
            int cur = tiles[^1];
            int cx = cur % w, cy = cur / w;
            int best = -1, bestTraffic = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = cx + dx, ny = cy + dy;
                    if (!World.Map.InBounds(nx, ny)) continue;
                    int nIdx = World.Map.Index(nx, ny);
                    if (visited.Contains(nIdx) || busy.Contains(nIdx)) continue;
                    if (!World.Map.Walkable(nx, ny)) continue;
                    if (World.BuildingAt(nx, ny) != null) continue;
                    byte nLvl = World.Map.Roads[nIdx];
                    bool ok = nLvl == level || (nLvl == 0 && World.Traffic.GetValueOrDefault(nIdx) >= threshold);
                    if (!ok) continue;
                    int t = World.Traffic.GetValueOrDefault(nIdx);
                    if (t > bestTraffic) { bestTraffic = t; best = nIdx; }
                }
            if (best < 0) break;
            tiles.Add(best);
            visited.Add(best);
        }
        return tiles;
    }

    private void PostHaulJobs()
    {
        int totalHaul = Jobs.ClaimedCount(JobKind.HaulStone) + Jobs.OpenCount(JobKind.HaulStone);
        if (totalHaul >= Balance.RoadMaxHaulJobs) return;

        foreach (var seg in World.RoadSegments.Values)
        {
            if (seg.MaterialsReady) continue;
            int missing = seg.MaterialsRequired - seg.MaterialsDelivered;
            int segHaul = CountSegmentJobs(seg.Id, JobKind.HaulStone);
            while (segHaul < Math.Min(missing, Balance.RoadHaulPerSegment)
                   && totalHaul < Balance.RoadMaxHaulJobs
                   && World.CountItem("stone") > segHaul)
            {
                int t = seg.Tiles[Math.Min(seg.NextBuildIndex, seg.Tiles.Count - 1)];
                var job = Jobs.Add(World, JobKind.HaulStone, 74, t % World.Map.W, t / World.Map.W, nodeId: -1);
                job.SegmentId = seg.Id;
                job.ItemId = "stone";
                segHaul++;
                totalHaul++;
            }
            if (totalHaul >= Balance.RoadMaxHaulJobs) return;
        }
    }

    private void PostRoadBuildJobs()
    {
        foreach (var seg in World.RoadSegments.Values)
        {
            if (!seg.MaterialsReady) continue;
            if (seg.NextBuildIndex >= seg.Tiles.Count) continue;
            if (CountSegmentJobs(seg.Id, JobKind.BuildRoad) > 0) continue;

            int t = seg.Tiles[seg.NextBuildIndex];
            var job = Jobs.Add(World, JobKind.BuildRoad, 88, t % World.Map.W, t / World.Map.W, nodeId: -1);
            job.SegmentId = seg.Id;
            job.TileIdx = t;
        }
    }

    private List<int> _roadRepairCandidates = [];

    private void PostRepairJobs()
    {
        int open = Jobs.ClaimedCount(JobKind.RepairRoad) + Jobs.OpenCount(JobKind.RepairRoad);
        int slots = Balance.RoadMaxRepairJobs - open;
        if (slots <= 0) return;

        foreach (var idx in _roadRepairCandidates)
        {
            if (slots <= 0) break;
            byte lvl = World.Map.Roads[idx];
            if (lvl == 0) continue;
            int threshold = (int)(Balance.RoadDegradeWear[lvl] * Balance.RoadRepairWearFraction);
            if (World.Map.RoadWear[idx] < threshold) continue;
            if (Jobs.All.Any(j => j.Kind == JobKind.RepairRoad && j.TileIdx == idx && j.State != JobState.Done)) continue;
            var job = Jobs.Add(World, JobKind.RepairRoad, 65, idx % World.Map.W, idx / World.Map.W, nodeId: -1);
            job.TileIdx = idx;
            slots--;
        }
    }

    private void CollectRepairCandidates()
    {
        _roadRepairCandidates.Clear();
        var roads = World.Map.Roads;
        var wearArr = World.Map.RoadWear;
        for (int i = 0; i < roads.Length; i++)
        {
            byte lvl = roads[i];
            if (lvl == 0) continue;
            int threshold = (int)(Balance.RoadDegradeWear[lvl] * Balance.RoadRepairWearFraction);
            if (wearArr[i] >= threshold) _roadRepairCandidates.Add(i);
        }
        _roadRepairCandidates.Sort((a, b) => wearArr[b].CompareTo(wearArr[a]));
    }

    private int CountSegmentJobs(int segmentId, JobKind kind)
    {
        int n = 0;
        foreach (var j in Jobs.All)
            if (j.Kind == kind && j.SegmentId == segmentId && j.State is JobState.Open or JobState.Claimed)
                n++;
        return n;
    }

    public void DegradeRoads()
    {
        var roads = World.Map.Roads;
        var wearArr = World.Map.RoadWear;
        for (int i = 0; i < roads.Length; i++)
        {
            byte lvl = roads[i];
            if (lvl == 0) continue;
            if (wearArr[i] <= Balance.RoadDegradeWear[lvl]) continue;

            roads[i] = (byte)(lvl - 1);
            wearArr[i] = (ushort)(Balance.RoadDegradeWear[lvl - 1] * 0.4f);
            RoadsChanged.Add(i);
            if (Rng.Chance(0.08f))
            {
                string zh = lvl switch
                {
                    1 => "一段土路被踩烂，重新化回了泥土",
                    2 => "一段碎石路年久失修，损坏了",
                    _ => "一段石板路终于不堪重负，碎裂了"
                };
                Log(zh, LogSeverity.Normal);
            }
        }
        CollectRepairCandidates();
    }

    public bool HasTech(string id) => World.Researched.Contains(id);

    public void DecayTraffic()
    {
        var keys = World.Traffic.Keys.ToArray();
        foreach (var k in keys)
        {
            int v = (int)(World.Traffic[k] * 0.99f);
            if (v < 10) World.Traffic.Remove(k);
            else World.Traffic[k] = v;
        }
    }

    private int _spoilAccum;
    private int _spoilWeekStart;

    public void SpoilGoods()
    {
        float granaryRelief = Math.Min(0.6f, World.BuildingsOf("granary").Count() * 0.2f);
        float foodRate = Balance.FoodSpoilBasePerDay * (1f - granaryRelief);
        int spoiled = 0;
        string[] perishable = ["berries", "mushroom", "fish", "meat", "grain", "meal", "herb"];
        foreach (var id in perishable)
        {
            int n = World.CountItem(id);
            if (n <= 0) continue;
            int loss = (int)MathF.Ceiling(n * foodRate);
            if (loss > 0)
            {
                World.TryTakeItem(id, loss);
                spoiled += loss;
            }
        }
        _spoilAccum += spoiled;
        if (Day - _spoilWeekStart >= 7)
        {
            if (_spoilAccum > 0)
                Log($"过去一周有{_spoilAccum}份存粮腐坏变质" + (granaryRelief > 0 ? "，粮仓里保存得还算完好" : "，也许该建座粮仓了"), LogSeverity.Normal);
            _spoilAccum = 0;
            _spoilWeekStart = Day;
        }
    }

    public (int x, int y) GetWaterSpotPos(Villager v)
    {
        var well = World.BuildingsOf("well").FirstOrDefault();
        if (well != null) return World.DoorOf(well);

        (int x, int y)? best = null;
        float bestDist = float.MaxValue;
        foreach (var node in World.Resources.Values)
        {
            if (node.Kind != ResKind.WaterSpot) continue;
            var spot = ResourceWorkSpot(node);
            float d = Dist2(v.Pos, spot);
            if (d < bestDist) { bestDist = d; best = spot; }
        }
        return best ?? World.SettleCenter;
    }

    public (int x, int y) ResourceWorkSpotPublic(ResourceNode n) => ResourceWorkSpot(n);

    private (int x, int y) GetStoragePos(Villager v)    {
        (int x, int y)? best = null;
        float bestDist = float.MaxValue;
        foreach (var b in World.BuildingsOf("storehouse")
                     .Concat(World.BuildingsOf("granary"))
                     .Concat(World.Buildings.Where(x => x.State == BuildingState.Complete)))
        {
            float d = Dist2(v.Pos, (b.CenterX, b.CenterY));
            if (d < bestDist) { bestDist = d; best = World.DoorOf(b); }
        }
        return best ?? World.SettleCenter;
    }

    public void CompleteRoadRepairForTest(Job job) => CompleteRoadRepair(null!, job);
}
