namespace TianniuVillage.Core;

public sealed class LogEntry
{
    public int Seq;
    public int Tick;
    public LogSeverity Sev;
    public string TimeZh = "";
    public string Text = "";
}

public sealed partial class Game
{
    public World World;
    public List<Villager> Villagers = [];
    public JobBoard Jobs = new();
    public Rng Rng;
    public int Tick;
    public Weather Weather = Weather.Sunny;
    public List<LogEntry> Logs = [];
    public int WeatherTicksLeft;
    public int TotalBirths;
    public int TotalDeaths;
    public string VillageName = "甜牛村";
    public HashSet<string> UsedNames = [];

    public readonly HashSet<int> ResChanged = new();
    public readonly HashSet<int> ResRemoved = new();
    public readonly HashSet<int> RoadsChanged = new();

    public int Day => Tick / Balance.MinutesPerDay;
    public int MinuteOfDay => Tick % Balance.MinutesPerDay;
    public int Year => Day / (Balance.DaysPerSeason * Balance.SeasonsPerYear);
    public int DayOfYear => Day % (Balance.DaysPerSeason * Balance.SeasonsPerYear);
    public Season Season => SeasonOf(Day);
    public bool IsNight => MinuteOfDay < Balance.WakeMinute || MinuteOfDay >= Balance.SleepStartMinute;

    public static Season SeasonOf(int day) => (Season)(day / Balance.DaysPerSeason % Balance.SeasonsPerYear);

    private int _logSeq;

    public int LastLogSeq => _logSeq;
    public void SetLogSeq(int seq) => _logSeq = seq;

    public Game(int seed)
    {
        Rng = new Rng(seed);
        World = WorldGenerator.Generate(seed);
    }

    public static Game NewGame(int seed)
    {
        var g = new Game(seed);
        g.SpawnInitialVillagers();
        var (cx, cy) = g.World.SettleCenter;
        g.World.Buildings.Add(new Building
        {
            Id = g.World.NextBuildingId++,
            Key = "villagecenter",
            X = cx - 1,
            Y = cy - 1,
            State = BuildingState.Complete
        });
        g.World.RebuildBlocked();
        g.World.AddItem("berries", 40);
        g.World.AddItem("water", 15);
        g.World.AddItem("log", 20);
        g.Log($"{g.VillageName}在一片沃野上建立了。{g.Villagers.Count}位拓荒者围着村中心的篝火，一切将从零开始。", LogSeverity.Important);
        return g;
    }

    private void SpawnInitialVillagers()
    {
        var (cx, cy) = World.SettleCenter;
        string[] foods = ["berries", "mushroom", "fish", "meat", "grain", "meal", "milk", "egg"];
        for (int i = 0; i < Balance.InitialVillagers; i++)
        {
            var sex = i % 2 == 0 ? Sex.Male : Sex.Female;
            var v = new Villager
            {
                Id = World.NextVillagerId++,
                Name = "",
                Sex = sex,
                Age = Rng.NextFloat(18, 34),
                Pos = FindSpawnNear(cx, cy),
                Diligence = Rng.NextFloat(30, 90),
                Optimism = Rng.NextFloat(30, 90),
                Sociability = Rng.NextFloat(30, 90),
                FavoriteFood = foods[Rng.Next(foods.Length)],
                HatedFood = foods[Rng.Next(foods.Length)]
            };
            if (v.FavoriteFood == v.HatedFood)
                v.HatedFood = foods[(Array.IndexOf(foods, v.FavoriteFood) + 1) % foods.Length];
            foreach (var key in v.Skills.Keys.ToList())
                v.Skills[key] = Rng.NextFloat(10, 45);
            v.Name = NameGen.Next(Rng, v.Sex, null, UsedNames);
            Villagers.Add(v);
        }

        for (int i = 0; i < Villagers.Count; i++)
            for (int j = i + 1; j < Villagers.Count; j++)
            {
                float known = Rng.NextFloat(25, 50);
                Villagers[i].Friendships[Villagers[j].Id] = known;
                Villagers[j].Friendships[Villagers[i].Id] = known;
            }
    }

    public (int x, int y) FindSpawnNear(int x, int y)
    {
        for (int r = 0; r < 12; r++)
            for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (World.Map.Walkable(nx, ny) && World.BuildingAt(nx, ny) == null) return (nx, ny);
                }
        return (x, y);
    }

    public string TimeZh(int tick)
    {
        int day = tick / Balance.MinutesPerDay;
        int minute = tick % Balance.MinutesPerDay;
        int year = day / 120 + 1;
        var season = SeasonOf(day);
        string seasonZh = season switch { Season.Spring => "春", Season.Summer => "夏", Season.Autumn => "秋", _ => "冬" };
        return $"第{year}年{seasonZh}{day % 30 + 1}日 {minute / 60:00}:{minute % 60:00}";
    }

    public void Log(string text, LogSeverity sev = LogSeverity.Normal)
    {
        Logs.Add(new LogEntry { Seq = ++_logSeq, Tick = Tick, Sev = sev, TimeZh = TimeZh(Tick), Text = text });
        if (Logs.Count > 800) Logs.RemoveRange(0, 200);
    }

    public void Step()
    {
        Tick++;
        World.Map.WaterCostMul = Season == Season.Winter || Weather == Weather.Snow ? 2f : 1f;
        if (Tick % 5 == 0) RunEconomyPlanner();
        TickFestival();
        TickMerchant();
        if (MinuteOfDay % 60 == 0) HourlyTick();
        if (MinuteOfDay == 0) DailyTick();
        if (MinuteOfDay == Balance.SleepStartMinute) LightTorches();
        UpdateWeather();
        UpdateVillagers();
        UpdateAnimals();
        UpdateSocial();
    }

    private void LightTorches()
    {
        World.TorchLit = World.CountItem("log") >= Balance.TorchWoodPerNight;
        if (World.TorchLit)
            World.TryTakeItem("log", Balance.TorchWoodPerNight);
    }

    private void HourlyTick()
    {
        World.Researchers.RemoveWhere(id =>
            Villagers.FirstOrDefault(x => x.Id == id)?.Activity != VillagerActivity.Researching);
        RunRoadPlanner();
        GrowCrops();
        TreatSick();
        if (MinuteOfDay % 360 == 0) RollWeather();
    }

    private void DailyTick()
    {
        AgeAndLifecycle();
        DailyHousing();
        DailyEvents();
        DailyRumor();
        DistributeWinterClothes();
        DegradeRoads();
        DecayTraffic();
        SpoilGoods();
        LivestockDailyTick();
        RegenResources();
        ReplenishAnimals();
        EnrichmentDailyTick();
        Jobs.PruneStale(Tick, this);
        foreach (var v in Villagers)
        {
            if (!v.Alive) continue;
            if (v.MentalTodaySamples > 0)
            {
                float mentalAvg = v.MentalTodaySum / v.MentalTodaySamples;
                v.Happiness += (mentalAvg - v.Happiness) * 0.3f;
                v.MentalTodaySum = 0;
                v.MentalTodaySamples = 0;
            }
        }
    }
}
