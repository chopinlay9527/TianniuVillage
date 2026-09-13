namespace TianniuVillage.Core;

public sealed record VillagerView(int id, string n, int x, int y, int act, string job, int sex, string stage,
    float hp, float sa, float en, float st, float me, float ha, float th, int facing, float prog, bool mv,
    string? carry, string? speech, bool si, string? dis, string? inj, int role, bool mb,
    float age, float rep, string? homeN, string? spouseN, string[]? kidsN);

public sealed record BuildingView(int id, string k, int x, int y, int state, float prog, int[] crop,
    string? lt, int lc, int oc, int beds, float warm, float comf, string name);

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
                ? job.Describe(this)
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
            v.MentalBreaking,
            MathF.Round(v.Age, 1), MathF.Round(v.Reputation, 1),
            HomeZh(v), SpouseZh(v), KidsNames(v))).ToList();

        var buildings = World.Buildings.Select(b => new BuildingView(
            b.Id, b.Key, b.X, b.Y, (int)b.State,
            b.Def.WorkMinutes > 0 ? (float)b.WorkDone / b.Def.WorkMinutes : 1f,
            b.CropPhase, b.LivestockType.Length > 0 ? b.LivestockType : null, b.LivestockCount,
            b.Occupants, b.Beds, MathF.Round(b.Warmth, 1), MathF.Round(b.ComfortBonus, 1),
            BuildingDefs.All[b.Key].NameZh)).ToList();

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
                v.MentalBreaking,
                MathF.Round(v.Age, 1), MathF.Round(v.Reputation, 1),
                HomeZh(v), SpouseZh(v), KidsNames(v))).ToList(),
            buildings = World.Buildings.Select(b => new BuildingView(
                b.Id, b.Key, b.X, b.Y, (int)b.State,
                b.Def.WorkMinutes > 0 ? (float)b.WorkDone / b.Def.WorkMinutes : 1f,
                b.CropPhase, b.LivestockType.Length > 0 ? b.LivestockType : null, b.LivestockCount,
                b.Occupants, b.Beds, MathF.Round(b.Warmth, 1), MathF.Round(b.ComfortBonus, 1),
                BuildingDefs.All[b.Key].NameZh)).ToList(),
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

    public static string SeasonZh(Season s) => s switch
    {
        Season.Spring => "春",
        Season.Summer => "夏",
        Season.Autumn => "秋",
        _ => "冬"
    };

    public static string WeatherZh(Weather w) => w switch
    {
        Weather.Sunny => "晴",
        Weather.Cloudy => "多云",
        Weather.Rain => "雨",
        Weather.Storm => "暴风雨",
        Weather.Snow => "雪",
        Weather.Fog => "雾",
        _ => "晴"
    };

    public static string JobKindZh(JobKind k) => k switch
    {
        JobKind.Fell => "伐木",
        JobKind.Forage => "采集",
        JobKind.PickMushroom => "采蘑菇",
        JobKind.Mine => "采石",
        JobKind.GatherHerb => "采药",
        JobKind.Fish => "捕鱼",
        JobKind.Hunt => "狩猎",
        JobKind.Plow => "犁地",
        JobKind.Sow => "播种",
        JobKind.Harvest => "收割",
        JobKind.Build => "建造",
        JobKind.Cook => "烹饪",
        JobKind.Saw => "锯木",
        JobKind.HaulStone => "搬运石料",
        JobKind.BuildRoad => "修路",
        JobKind.RepairRoad => "修整道路",
        JobKind.FetchWater => "打水",
        JobKind.Weave => "织布",
        JobKind.SewClothes => "制衣",
        JobKind.GatherFiber => "采纤维",
        JobKind.MineOre => "采矿",
        JobKind.Smelt => "冶炼",
        JobKind.CraftTool => "打造工具",
        JobKind.TendLivestock => "照料牲畜",
        _ => k.ToString()
    };

    public static string ResKindZh(ResKind k) => k switch
    {
        ResKind.Tree => "树木",
        ResKind.BerryBush => "浆果丛",
        ResKind.MushroomPatch => "蘑菇圈",
        ResKind.StoneOutcrop => "石料堆",
        ResKind.HerbPatch => "药草丛",
        ResKind.FishSpot => "鱼群",
        ResKind.WaterSpot => "水源",
        ResKind.FlaxPatch => "亚麻丛",
        ResKind.CopperVein => "铜矿脉",
        ResKind.IronVein => "铁矿脉",
        _ => k.ToString()
    };

    private string? HomeZh(Villager v) =>
        World.Buildings.FirstOrDefault(b => b.Id == v.HomeId) is { } hb
            ? BuildingDefs.All[hb.Key].NameZh
            : null;

    private string? SpouseZh(Villager v) =>
        v.SpouseId != 0 ? Villagers.FirstOrDefault(x => x.Id == v.SpouseId)?.Name : null;

    private string[]? KidsNames(Villager v) => v.ChildrenIds.Count == 0
        ? null
        : v.ChildrenIds.Select(id => Villagers.FirstOrDefault(x => x.Id == id)?.Name ?? "?").ToArray();

    public object BuildOverview()
    {
        var alive = Villagers.Where(v => v.Alive).ToList();
        var byId = Villagers.ToDictionary(v => v.Id);
        var buildingById = World.Buildings.ToDictionary(b => b.Id);

        bool IsResidentialHome(int homeId) =>
            homeId != 0 && buildingById.TryGetValue(homeId, out var hb) &&
            hb.Beds > 0 && hb.State == BuildingState.Complete;

        string? HomeZh(Villager v) =>
            buildingById.TryGetValue(v.HomeId, out var hb) ? BuildingDefs.All[hb.Key].NameZh : null;

        string? SpouseName(Villager v) =>
            v.SpouseId != 0 && byId.TryGetValue(v.SpouseId, out var s) && s.Alive ? s.Name : null;

        var profiles = alive.Select(v => new
        {
            id = v.Id,
            n = v.Name,
            sex = (int)v.Sex,
            age = MathF.Round(v.Age, 1),
            stage = StageZh(v),
            role = (int)v.Role,
            act = (int)v.Activity,
            job = ActivityZh(v),
            hp = Round(v.Health), sa = Round(v.Satiety), th = Round(v.Thirst),
            en = Round(v.Energy), st = Round(v.Stamina), me = Round(v.Mood), ha = Round(v.Happiness),
            rep = Round(v.Reputation),
            fav = ItemDefs.Name(v.FavoriteFood),
            hate = ItemDefs.Name(v.HatedFood),
            home = HomeZh(v),
            spouse = SpouseName(v),
            kids = v.ChildrenIds.Count > 0
                ? v.ChildrenIds.Select(id => byId.TryGetValue(id, out var k) ? k.Name : "?").ToArray()
                : null,
            winters = v.WinterClothes,
            preg = v.PregnantDaysLeft > 0,
            ppd = v.PostpartumDays > 0,
            dis = v.Ill && v.Disease != DiseaseType.None ? DiseaseInfo.Zh(v.Disease) : null,
            inj = v.Injury != InjuryType.None ? InjuryZh(v.Injury) : null,
            mb = v.MentalBreaking,
            sk = new
            {
                farming = (int)MathF.Round(v.SkillOf("farming")),
                building = (int)MathF.Round(v.SkillOf("building")),
                gathering = (int)MathF.Round(v.SkillOf("gathering")),
                cooking = (int)MathF.Round(v.SkillOf("cooking")),
                medicine = (int)MathF.Round(v.SkillOf("medicine")),
                hunting = (int)MathF.Round(v.SkillOf("hunting")),
                learning = (int)MathF.Round(v.SkillOf("learning"))
            }
        }).ToList();

        var homes = World.Buildings
            .Where(b => b.Beds > 0 && b.State == BuildingState.Complete)
            .Select(b => new
            {
                id = b.Id,
                k = b.Key,
                name = BuildingDefs.All[b.Key].NameZh,
                x = b.X, y = b.Y,
                oc = b.Occupants,
                beds = b.Beds,
                warm = MathF.Round(b.Warmth, 1),
                comf = MathF.Round(b.ComfortBonus, 1),
                residents = Villagers.Where(v => v.Alive && v.HomeId == b.Id).Select(v => v.Name).ToList()
            })
            .OrderBy(h => h.name).ToList();

        var buildings = World.Buildings.Select(b =>
        {
            var cost = b.Def.Cost.ToDictionary(c => c.item, c => c.count);
            var residents = b.Beds > 0
                ? Villagers.Where(v => v.Alive && v.HomeId == b.Id).Select(v => v.Name).ToList()
                : [];
            return new
            {
                id = b.Id,
                k = b.Key,
                name = BuildingDefs.All[b.Key].NameZh,
                x = b.X, y = b.Y,
                state = (int)b.State,
                prog = b.Def.WorkMinutes > 0 ? (int)Math.Min(100, b.WorkDone * 100f / b.Def.WorkMinutes) : 100,
                oc = b.Occupants,
                beds = b.Beds,
                warm = MathF.Round(b.Warmth, 1),
                comf = MathF.Round(b.ComfortBonus, 1),
                lt = b.LivestockType.Length > 0 ? b.LivestockType : null,
                lc = b.LivestockCount,
                crop = b.CropPhase.Length > 0
                    ? Enumerable.Range(0, 5).Select(ph => b.CropPhase.Count(c => c == ph)).ToArray()
                    : null,
                buffered = b.ProdBuffer.Select(kv => new { k = kv.Key, name = ItemDefs.Name(kv.Key), count = kv.Value }).ToList(),
                delivered = b.Delivered.Select(kv => new
                {
                    k = kv.Key,
                    name = ItemDefs.Name(kv.Key),
                    have = kv.Value,
                    need = cost.GetValueOrDefault(kv.Key)
                }).ToList(),
                residents
            };
        }).ToList();

        var items = ItemDefs.All.Values.Select(d => new
        {
            k = d.Id,
            name = d.NameZh,
            cat = (int)d.Category,
            stock = World.CountItem(d.Id),
            produced = World.Produced.GetValueOrDefault(d.Id),
            consumed = World.Consumed.GetValueOrDefault(d.Id)
        }).ToList();

        var jobs = new List<object>();
        foreach (var kind in Enum.GetValues<JobKind>())
        {
            int open = Jobs.OpenCount(kind), claimed = Jobs.ClaimedCount(kind);
            if (open == 0 && claimed == 0) continue;
            jobs.Add(new { k = (int)kind, name = JobKindZh(kind), open, claimed });
        }

        var roadLevels = new Dictionary<int, int>();
        long roadWear = 0;
        for (int i = 0; i < World.Map.Roads.Length; i++)
        {
            int lvl = World.Map.Roads[i];
            if (lvl <= 0) continue;
            roadLevels[lvl] = roadLevels.GetValueOrDefault(lvl) + 1;
            roadWear += World.Map.RoadWear[i];
        }

        var resources = World.Resources.Values
            .GroupBy(n => n.Kind)
            .Select(g => new
            {
                k = (int)g.Key,
                name = ResKindZh(g.Key),
                nodes = g.Count(),
                amount = g.Sum(n => n.Amount),
                berries = g.Sum(n => n.HasBerries ? n.Amount : 0)
            })
            .OrderByDescending(r => r.amount).ToList();

        var wildlife = World.Animals.GroupBy(a => a.Kind)
            .Select(g => new { k = g.Key, count = g.Count() })
            .OrderByDescending(g => g.count).ToList();

        var researched = World.Researched
            .Select(id => TechDefs.All.FirstOrDefault(t => t.Id == id)?.NameZh ?? id).ToList();

        int totalBeds = homes.Sum(h => h.beds);
        int residents = alive.Count(v => IsResidentialHome(v.HomeId));
        var homeless = alive.Where(v => !IsResidentialHome(v.HomeId)).Select(v => v.Name).ToList();

        return new
        {
            type = "overview",
            tick = Tick,
            timeZh = TimeZh(Tick),
            village = new
            {
                name = VillageName,
                seed = World.Seed,
                w = World.Map.W,
                h = World.Map.H,
                year = Year + 1,
                season = (int)Season,
                seasonZh = SeasonZh(Season),
                dayOfSeason = Day % Balance.DaysPerSeason + 1,
                weather = (int)Weather,
                weatherZh = WeatherZh(Weather),
                minute = MinuteOfDay
            },
            pop = new
            {
                total = alive.Count,
                adults = alive.Count(v => v.Stage == AgeStage.Adult),
                children = alive.Count(v => v.Stage == AgeStage.Child),
                infants = alive.Count(v => v.Stage == AgeStage.Infant),
                elders = alive.Count(v => v.Stage == AgeStage.Elder),
                avgAge = Round(alive.Count > 0 ? alive.Average(v => v.Age) : 0f),
                avgHappy = Round(alive.Count > 0 ? alive.Average(v => v.Happiness) : 0f),
                avgHealth = Round(alive.Count > 0 ? alive.Average(v => v.Health) : 0f),
                avgMood = Round(alive.Count > 0 ? alive.Average(v => v.Mood) : 0f),
                births = TotalBirths,
                deaths = TotalDeaths,
                ill = alive.Count(v => v.Ill && v.Disease != DiseaseType.None),
                injured = alive.Count(v => v.Injury != InjuryType.None)
            },
            housing = new
            {
                beds = totalBeds,
                residents,
                vacant = totalBeds - residents,
                homes,
                homeless
            },
            storage = new
            {
                used = World.StorageUsed(),
                cap = World.StorageCapacity(),
                full = World.StorageFull
            },
            food = CountFood(),
            water = World.CountItem("water"),
            items,
            flow = new
            {
                produced = World.Produced.ToDictionary(kv => kv.Key, kv => kv.Value),
                consumed = World.Consumed.ToDictionary(kv => kv.Key, kv => kv.Value)
            },
            buildings,
            animals = new
            {
                wildlife,
                livestock = new
                {
                    chickens = World.Buildings.Where(b => b.LivestockType == "chicken").Sum(b => b.LivestockCount),
                    sheep = World.Buildings.Where(b => b.LivestockType == "sheep").Sum(b => b.LivestockCount),
                    pigs = World.Buildings.Where(b => b.LivestockType == "pig").Sum(b => b.LivestockCount)
                }
            },
            jobs,
            roads = new
            {
                levels = roadLevels.Select(kv => new { lvl = kv.Key, tiles = kv.Value }).OrderBy(r => r.lvl).ToList(),
                wear = roadWear
            },
            resources,
            tech = new
            {
                count = World.Researched.Count,
                total = TechDefs.All.Length,
                researched,
                current = World.NextTech != null ? TechDefs.All.First(t => t.Id == World.NextTech.Id).NameZh : null,
                progress = World.NextTech != null
                    ? (int)Math.Min(100, World.ResearchProgress / TechDefs.All.First(t => t.Id == World.NextTech.Id).Cost * 100)
                    : 100
            },
            festival = CurrentFestival != null
                ? new
                {
                    type = (int)CurrentFestival.Type,
                    desc = CurrentFestival.Description,
                    left = Math.Max(0, CurrentFestival.Tick + CurrentFestival.DurationTicks - Tick)
                }
                : null,
            merchant = Merchant is { Active: true }
                ? new { left = Math.Max(0, Merchant.DepartureTick - Tick) }
                : null,
            villagers = profiles
        };
    }
}
