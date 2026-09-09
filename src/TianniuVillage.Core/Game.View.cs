namespace TianniuVillage.Core;

public sealed record VillagerView(int id, string n, int x, int y, int act, string job, int sex, string stage,
    float hp, float sa, float en, float st, float me, float ha, float th, int facing, float prog, bool mv,
    string? carry, string? speech, bool si, string? dis, string? inj, int role, bool mb);

public sealed record BuildingView(int id, string k, int x, int y, int state, float prog, int[] crop,
    string? lt, int lc);

public sealed record ResourceView(int id, int x, int y, int k, int a, bool berries, int op);

public sealed record AnimalView(int id, string k, int x, int y);

public sealed record RoadDeltaView(int i, int l);

public sealed record LogView(int seq, string t, int sev, string text);

public sealed record StatsView(int pop, int adults, int children, int food, int meal, int logs, int planks,
    int stone, int herb, int grain, int berries, float happiness, float health, int births, int deaths,
    int weather, int minute, int day, int year, int season, int storageUsed, int storageCap, int water,
    int hide, int fiber, int cloth, int clothes, string? techName, int techProgress, int techCount,
    bool torchLit, int chickens, int sheep, int pigs);

public sealed partial class Game
{
    public string ActivityZh(Villager v)
    {
        return v.Activity switch
        {
            VillagerActivity.Sleeping => "睡觉",
            VillagerActivity.Eating => "吃饭",
            VillagerActivity.Resting => "休息",
            VillagerActivity.Recovering => "养病",
            VillagerActivity.AttendingSchool => "上学",
            VillagerActivity.Playing => "玩耍",
            VillagerActivity.Training => "切磋",
            VillagerActivity.Researching => "钻研",
            VillagerActivity.Socializing => "聊天",
            _ => v.CurrentJobId != null && Jobs.Get(v.CurrentJobId.Value) is { } job
                ? job.Describe()
                : "闲逛"
        };
    }

    public object BuildUpdate(int lastLogSeq)
    {
        var villagers = Villagers.Where(v => v.Alive).Select(v => new VillagerView(
            v.Id, v.Name, v.Pos.x, v.Pos.y, (int)v.Activity, ActivityZh(v), (int)v.Sex, StageZh(v),
            Round(v.Health), Round(v.Satiety), Round(v.Energy), Round(v.Stamina),
            Round(v.Mood), Round(v.Happiness), Round(v.Thirst),
            v.Facing, v.MoveProgress, v.Path is { Count: > 0 },
            v.CarryLoad.Count > 0 ? string.Join(",", v.CarryLoad.Select(c => $"{ItemDefs.Name(c.Key)}×{c.Value}")) : null,
            v.SpeechTicksLeft > 0 ? v.Speech : null,
            v.SleepingIndoor,
            v.Ill && v.Disease != DiseaseType.None ? DiseaseInfo.Zh(v.Disease) : null,
            v.Injury != InjuryType.None ? InjuryZh(v.Injury) : null,
            (int)v.Role,
            v.MentalBreaking)).ToList();

        var buildings = World.Buildings.Select(b => new BuildingView(
            b.Id, b.Key, b.X, b.Y, (int)b.State,
            b.Def.WorkMinutes > 0 ? (float)b.WorkDone / b.Def.WorkMinutes : 1f,
            b.CropPhase, b.LivestockType.Length > 0 ? b.LivestockType : null, b.LivestockCount)).ToList();

        var animals = World.Animals.Select(a => new AnimalView(a.Id, a.Kind, a.X, a.Y)).ToList();

        var resDelta = new List<ResourceView>();
        foreach (var id in ResRemoved)
            resDelta.Add(new ResourceView(id, 0, 0, 0, 0, false, 2));
        foreach (var id in ResChanged)
            if (World.Resources.TryGetValue(id, out var node) && !ResRemoved.Contains(id))
                resDelta.Add(ToResourceView(node, 1));
        ResRemoved.Clear();
        ResChanged.Clear();

        var roadDelta = new List<RoadDeltaView>();
        foreach (var idx in RoadsChanged)
            roadDelta.Add(new RoadDeltaView(idx, World.Map.Roads[idx]));
        RoadsChanged.Clear();

        var logs = Logs.Where(l => l.Seq > lastLogSeq)
            .Select(l => new LogView(l.Seq, l.TimeZh, (int)l.Sev, l.Text)).ToList();

        var socialMsgs = SocialFeed.TakeLast(60).Select(m => new
        {
            seq = m.Seq,
            kind = m.Kind,
            text = m.Text,
            actors = m.Actors
        }).ToList();

        return new
        {
            type = "update",
            tick = Tick,
            stats = BuildStats(),
            villagers,
            buildings,
            animals,
            res = resDelta,
            road = roadDelta,
            logs,
            msgs = socialMsgs,
            festival = CurrentFestival != null ? new { desc = CurrentFestival.Description, left = Math.Max(0, CurrentFestival.Tick + CurrentFestival.DurationTicks - Tick) } : null,
            merchant = Merchant is { Active: true } ? new { left = Math.Max(0, Merchant.DepartureTick - Tick) } : null
        };
    }

    public object BuildInit()
    {
        var tiles = new byte[World.Map.Tiles.Length];
        for (int i = 0; i < tiles.Length; i++) tiles[i] = (byte)World.Map.Tiles[i];

        var res = World.Resources.Values.Select(n => ToResourceView(n, 0)).ToList();
        var logs = Logs.TakeLast(120)
            .Select(l => new LogView(l.Seq, l.TimeZh, (int)l.Sev, l.Text)).ToList();

        return new
        {
            type = "init",
            name = VillageName,
            seed = World.Seed,
            w = World.Map.W,
            h = World.Map.H,
            tiles = Convert.ToBase64String(tiles),
            roads = Convert.ToBase64String(World.Map.Roads),
            res,
            villagers = Villagers.Where(v => v.Alive).Select(v => new VillagerView(
                v.Id, v.Name, v.Pos.x, v.Pos.y, (int)v.Activity, ActivityZh(v), (int)v.Sex, StageZh(v),
                Round(v.Health), Round(v.Satiety), Round(v.Energy), Round(v.Stamina),
                Round(v.Mood), Round(v.Happiness), Round(v.Thirst),
                v.Facing, v.MoveProgress, v.Path is { Count: > 0 }, null, (string?)null,
                v.SleepingIndoor,
                v.Ill && v.Disease != DiseaseType.None ? DiseaseInfo.Zh(v.Disease) : null,
                v.Injury != InjuryType.None ? InjuryZh(v.Injury) : null,
                (int)v.Role,
                v.MentalBreaking)).ToList(),
            buildings = World.Buildings.Select(b => new BuildingView(
                b.Id, b.Key, b.X, b.Y, (int)b.State,
                b.Def.WorkMinutes > 0 ? (float)b.WorkDone / b.Def.WorkMinutes : 1f,
                b.CropPhase, b.LivestockType.Length > 0 ? b.LivestockType : null, b.LivestockCount)).ToList(),
            animals = World.Animals.Select(a => new AnimalView(a.Id, a.Kind, a.X, a.Y)).ToList(),
            stats = BuildStats(),
            logs
        };
    }

    private static ResourceView ToResourceView(ResourceNode n, int op)
        => new(n.Id, n.X, n.Y, (int)n.Kind, n.Amount, n.HasBerries, op);

    private StatsView BuildStats()
    {
        var alive = Villagers.Where(v => v.Alive).ToList();
        return new StatsView(
            alive.Count,
            alive.Count(v => v.Stage == AgeStage.Adult),
            alive.Count(v => v.Stage is AgeStage.Child or AgeStage.Infant),
            CountFood(),
            World.CountItem("meal"),
            World.CountItem("log"),
            World.CountItem("plank"),
            World.CountItem("stone"),
            World.CountItem("herb"),
            World.CountItem("grain"),
            World.CountItem("berries"),
            Round(alive.Count > 0 ? alive.Average(v => v.Happiness) : 0f),
            Round(alive.Count > 0 ? alive.Average(v => v.Health) : 0f),
            TotalBirths, TotalDeaths,
            (int)Weather, MinuteOfDay, Day, Year + 1, (int)Season,
            World.StorageUsed(), World.StorageCapacity(),
            World.CountItem("water"),
            World.CountItem("hide"), World.CountItem("fiber"),
            World.CountItem("cloth"), World.CountItem("clothes"),
            World.NextTech != null ? TechDefs.All.First(t => t.Id == World.NextTech.Id).NameZh : null,
            World.NextTech != null ? (int)Math.Min(100, World.ResearchProgress / TechDefs.All.First(t => t.Id == World.NextTech.Id).Cost * 100) : 0,
            World.Researched.Count,
            World.TorchLit,
            World.Buildings.Where(b => b.LivestockType == "chicken").Sum(b => b.LivestockCount),
            World.Buildings.Where(b => b.LivestockType == "sheep").Sum(b => b.LivestockCount),
            World.Buildings.Where(b => b.LivestockType == "pig").Sum(b => b.LivestockCount));
    }

    private static float Round(float v) => MathF.Round(v, 1);

    private static string StageZh(Villager v) => v.Stage switch
    {
        AgeStage.Infant => "婴儿",
        AgeStage.Child => v.Age < 12 ? "孩童" : "少年",
        AgeStage.Adult => "成年",
        _ => "老年"
    };

    public static string InjuryZh(InjuryType t) => t switch
    {
        InjuryType.Bruise => "擦伤",
        InjuryType.Cut => "划伤",
        InjuryType.Fracture => "骨折",
        _ => ""
    };

    public static string RoleZh(VillageRole r) => r switch
    {
        VillageRole.Elder => "长老",
        VillageRole.HuntChief => "猎队头领",
        VillageRole.Healer => "医师",
        _ => ""
    };
}
