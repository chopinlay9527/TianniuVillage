using System.Text.Json;
using System.Text.Json.Serialization;

namespace TianniuVillage.Core;

public sealed class SaveData
{
    public int Version;
    public int Seed;
    public int Tick;
    public int Weather;
    public int WeatherTicksLeft;
    public int TotalBirths;
    public int TotalDeaths;
    public string VillageName = "";
    public int LogSeq;
    public string TilesB64 = "";
    public (int x, int y) SettleCenter;
    public List<Villager> Villagers = [];
    public List<Building> Buildings = [];
    public List<Animal> Animals = [];
    public List<ResourceNode> Resources = [];
    public List<Job> Jobs = [];
    public Dictionary<string, int> Stock = [];
    public bool WinterClothesAssigned;
    public List<string> Researched = [];
    public string? CurrentTech;
    public float ResearchProgress;
    public List<SocialMessage> SocialFeed = [];
    public int SocialSeq;
    public List<LogEntry> Logs = [];
    public string RoadsB64 = "";
    public string RoadWearB64 = "";
    public Dictionary<int, int> Traffic = [];
    public List<RoadSegment> RoadSegments = [];
    public int NextSegmentId;
    public int NextNodeId;
    public int NextBuildingId;
    public int NextVillagerId;
    public int NextAnimalId;
    public int NextJobId;
    public Festival? CurrentFestival;
    public MerchantVisit? Merchant;
    public int LastFestivalDay;
    public int LastMerchantDay;
}

public static class SaveService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        IncludeFields = true,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static void Save(Game game, string path)
    {
        var tiles = new byte[game.World.Map.Tiles.Length];
        for (int i = 0; i < tiles.Length; i++) tiles[i] = (byte)game.World.Map.Tiles[i];

        var wearBytes = new byte[game.World.Map.RoadWear.Length * 2];
        Buffer.BlockCopy(game.World.Map.RoadWear, 0, wearBytes, 0, wearBytes.Length);

        var data = new SaveData
        {
            Version = 1,
            Seed = game.World.Seed,
            Tick = game.Tick,
            Weather = (int)game.Weather,
            WeatherTicksLeft = game.WeatherTicksLeft,
            TotalBirths = game.TotalBirths,
            TotalDeaths = game.TotalDeaths,
            VillageName = game.VillageName,
            LogSeq = game.LastLogSeq,
            TilesB64 = Convert.ToBase64String(tiles),
            SettleCenter = game.World.SettleCenter,
            Villagers = game.Villagers,
            Buildings = game.World.Buildings,
            Animals = game.World.Animals,
            Resources = game.World.Resources.Values.ToList(),
            Jobs = game.Jobs.All.ToList(),
            Stock = game.World.Stock,
            WinterClothesAssigned = game.World.WinterClothesAssigned,
            Researched = game.World.Researched.ToList(),
            CurrentTech = game.World.CurrentTech,
            ResearchProgress = game.World.ResearchProgress,
            SocialFeed = game.SocialFeed.TakeLast(200).ToList(),
            SocialSeq = game.LastSocialSeq,
            Logs = game.Logs,
            RoadsB64 = Convert.ToBase64String(game.World.Map.Roads),
            RoadWearB64 = Convert.ToBase64String(wearBytes),
            Traffic = game.World.Traffic,
            RoadSegments = game.World.RoadSegments.Values.ToList(),
            NextSegmentId = game.World.NextSegmentId,
            NextNodeId = game.World.NextNodeId,
            NextBuildingId = game.World.NextBuildingId,
            NextVillagerId = game.World.NextVillagerId,
            NextAnimalId = game.World.NextAnimalId,
            NextJobId = game.World.NextJobId,
            CurrentFestival = game.CurrentFestival,
            Merchant = game.Merchant,
            LastFestivalDay = game.LastFestivalDay,
            LastMerchantDay = game.LastMerchantDay
        };

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(data, Options));
    }

    public static Game Load(string path)
    {
        var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("存档损坏");

        var game = new Game(data.Seed)
        {
            Tick = data.Tick,
            Weather = (Weather)data.Weather,
            WeatherTicksLeft = data.WeatherTicksLeft,
            TotalBirths = data.TotalBirths,
            TotalDeaths = data.TotalDeaths,
            VillageName = data.VillageName
        };
        game.Rng.ReSeed(data.Seed ^ data.Tick);

        var tiles = Convert.FromBase64String(data.TilesB64);
        for (int i = 0; i < tiles.Length && i < game.World.Map.Tiles.Length; i++)
            game.World.Map.Tiles[i] = (Terrain)tiles[i];

        if (!string.IsNullOrEmpty(data.RoadsB64))
        {
            var roads = Convert.FromBase64String(data.RoadsB64);
            Array.Copy(roads, game.World.Map.Roads, Math.Min(roads.Length, game.World.Map.Roads.Length));
        }
        if (!string.IsNullOrEmpty(data.RoadWearB64))
        {
            var wearBytes = Convert.FromBase64String(data.RoadWearB64);
            Buffer.BlockCopy(wearBytes, 0, game.World.Map.RoadWear, 0,
                Math.Min(wearBytes.Length, game.World.Map.RoadWear.Length * 2));
        }
        game.World.Traffic = data.Traffic ?? [];
        game.World.RoadSegments = data.RoadSegments.ToDictionary(s => s.Id);
        game.World.NextSegmentId = data.NextSegmentId;
        if (game.World.Buildings.All(b => b.Key != "villagecenter"))
        {
            var (scx, scy) = game.World.SettleCenter;
            game.World.Buildings.Add(new Building
            {
                Id = game.World.NextBuildingId++,
                Key = "villagecenter",
                X = scx - 1,
                Y = scy - 1,
                State = BuildingState.Complete
            });
        }
        game.World.RebuildBlocked();

        game.World.SettleCenter = data.SettleCenter;
        game.World.Resources = data.Resources.ToDictionary(n => game.World.Map.Index(n.X, n.Y));
        game.World.Buildings = data.Buildings;
        game.World.Animals = data.Animals;
        game.World.Stock = data.Stock;
        game.World.WinterClothesAssigned = data.WinterClothesAssigned;
        game.World.Researched = new HashSet<string>(data.Researched ?? []);
        game.World.CurrentTech = data.CurrentTech;
        game.World.ResearchProgress = data.ResearchProgress;
        game.SocialFeed.AddRange(data.SocialFeed ?? []);
        game.SetSocialSeq(data.SocialSeq);
        game.Villagers = data.Villagers;
        game.Jobs.Restore(data.Jobs);
        game.Logs.AddRange(data.Logs);
        game.SetLogSeq(data.LogSeq);
        game.World.NextNodeId = data.NextNodeId;
        game.World.NextBuildingId = data.NextBuildingId;
        game.World.NextVillagerId = data.NextVillagerId;
        game.World.NextAnimalId = data.NextAnimalId;
        game.World.NextJobId = data.NextJobId;
        game.CurrentFestival = data.CurrentFestival;
        game.Merchant = data.Merchant;
        game.LastFestivalDay = data.LastFestivalDay == 0 ? -100 : data.LastFestivalDay;
        game.LastMerchantDay = data.LastMerchantDay == 0 ? -100 : data.LastMerchantDay;
        return game;
    }
}
