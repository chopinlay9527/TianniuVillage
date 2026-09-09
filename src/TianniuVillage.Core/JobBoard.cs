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

    public void PruneStale(int gameTick, Game game) { }
}
