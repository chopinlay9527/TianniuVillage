namespace TianniuVillage.Core;

public sealed class JobBoard
{
    private readonly Dictionary<int, Job> _jobs = new();

    public IEnumerable<Job> All => _jobs.Values;

    public Job Add(World world, JobKind kind, int priority, int x, int y,
        int nodeId = 0, int buildingId = 0, int cellIdx = 0, string itemId = "", int units = 1, int animalId = 0)
    {
        var job = new Job
        {
            Id = world.NextJobId++,
            Kind = kind,
            Priority = priority,
            X = x,
            Y = y,
            NodeId = nodeId,
            BuildingId = buildingId,
            CellIdx = cellIdx,
            ItemId = itemId,
            Units = units,
            AnimalId = animalId
        };
        _jobs[job.Id] = job;
        return job;
    }

    public Job? Get(int id) => _jobs.GetValueOrDefault(id);

    public void Complete(int id) => _jobs.Remove(id);

    public void CancelByNode(int nodeId)
    {
        List<int> toRemove = [];
        foreach (var j in _jobs.Values)
            if (j.NodeId == nodeId)
                toRemove.Add(j.Id);
        foreach (var id in toRemove) _jobs.Remove(id);
    }

    public void CancelByBuilding(int buildingId)
    {
        List<int> toRemove = [];
        foreach (var j in _jobs.Values)
            if (j.BuildingId == buildingId)
                toRemove.Add(j.Id);
        foreach (var id in toRemove) _jobs.Remove(id);
    }

    public int ClaimedCount(JobKind kind)
    {
        int n = 0;
        foreach (var j in _jobs.Values)
            if (j.Kind == kind && j.State == JobState.Claimed)
                n++;
        return n;
    }

    public int OpenCount(JobKind kind)
    {
        int n = 0;
        foreach (var j in _jobs.Values)
            if (j.Kind == kind && j.State == JobState.Open)
                n++;
        return n;
    }

    public void Restore(IEnumerable<Job> jobs)
    {
        _jobs.Clear();
        foreach (var j in jobs) _jobs[j.Id] = j;
    }

    public void PruneStale(int gameTick, Game game)
    {
        List<int> toRemove = [];
        foreach (var j in _jobs.Values)
        {
            if (j.State is not (JobState.Open or JobState.Claimed)) continue;
            if (IsStale(game, j)) toRemove.Add(j.Id);
        }
        if (toRemove.Count == 0) return;

        foreach (var id in toRemove) _jobs.Remove(id);
        foreach (var v in game.Villagers)
        {
            if (v.CurrentJobId is int jid && toRemove.Contains(jid))
            {
                v.CurrentJobId = null;
                v.Path = null;
                v.Activity = VillagerActivity.Idle;
                game.SetSelfTask(v, null, null);
                v.DecisionCooldown = 5;
            }
        }
    }

    private static bool IsStale(Game game, Job job)
    {
        if (job.NodeId > 0 && !game.World.Resources.ContainsKey(job.NodeId)) return true;
        if (job.AnimalId > 0 && !game.World.Animals.Any(a => a.Id == job.AnimalId)) return true;
        if (job.SegmentId > 0 && !game.World.RoadSegments.ContainsKey(job.SegmentId)) return true;
        if (job.BuildingId <= 0) return false;

        var b = game.World.Buildings.FirstOrDefault(x => x.Id == job.BuildingId);
        if (b == null) return true;
        return job.Kind switch
        {
            JobKind.Build or JobKind.HaulStone => b.State != BuildingState.Planned,
            JobKind.TendLivestock => b.LivestockCount <= 0,
            JobKind.Plow or JobKind.Sow or JobKind.Harvest => b.State != BuildingState.Complete,
            JobKind.Cook or JobKind.Saw or JobKind.Weave or JobKind.SewClothes
                or JobKind.Smelt or JobKind.CraftTool => b.State != BuildingState.Complete,
            _ => false
        };
    }
}
