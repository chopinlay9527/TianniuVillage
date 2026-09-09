namespace TianniuVillage.Core;

public sealed record BuildingDef(
    string Key,
    string NameZh,
    int W,
    int H,
    (string item, int count)[] Cost,
    int WorkMinutes,
    bool NeedsWaterAdjacent = false,
    bool NeedsStoneAdjacent = false);

public static class BuildingDefs
{
    public static readonly BuildingDef VillageCenter = new("villagecenter", "村庄中心", 2, 2,
        [], 0);
    public static readonly BuildingDef Hut = new("hut", "茅舍", 1, 1,
        [("log", 6)], 180);
    public static readonly BuildingDef House = new("house", "住宅", 2, 2,
        [("plank", 8), ("stone", 4)], 480);
    public static readonly BuildingDef Manor = new("manor", "大宅", 3, 3,
        [("plank", 20), ("stone", 12), ("cloth", 4)], 720);
    public static readonly BuildingDef Granary = new("granary", "粮仓", 2, 2,
        [("plank", 6), ("stone", 6)], 360);
    public static readonly BuildingDef Storehouse = new("storehouse", "仓库", 2, 2,
        [("log", 10)], 300);
    public static readonly BuildingDef Well = new("well", "水井", 1, 1,
        [("stone", 8)], 240);
    public static readonly BuildingDef Farm = new("farm", "农田", 3, 3,
        [], 0);
    public static readonly BuildingDef Cookhouse = new("cookhouse", "烹饪屋", 2, 2,
        [("plank", 6), ("stone", 6)], 360);
    public static readonly BuildingDef Sawpit = new("sawpit", "锯木坊", 2, 2,
        [("log", 8), ("stone", 4)], 300);
    public static readonly BuildingDef Quarry = new("quarry", "采石场", 2, 2,
        [("log", 8)], 300, NeedsStoneAdjacent: true);
    public static readonly BuildingDef FishingWharf = new("wharf", "渔屋", 1, 1,
        [("log", 6)], 240, NeedsWaterAdjacent: true);
    public static readonly BuildingDef HuntingLodge = new("lodge", "猎屋", 2, 2,
        [("log", 10)], 300);
    public static readonly BuildingDef HerbGarden = new("herbgarden", "药圃", 2, 2,
        [], 180);
    public static readonly BuildingDef Clinic = new("clinic", "诊所", 2, 2,
        [("plank", 10), ("stone", 10)], 480);
    public static readonly BuildingDef WeavingHut = new("weaver", "织布坊", 2, 2,
        [("plank", 8), ("stone", 2)], 360);
    public static readonly BuildingDef Study = new("study", "书斋", 2, 2,
        [("plank", 12), ("stone", 6)], 480);
    public static readonly BuildingDef Smelter = new("smelter", "熔炉", 2, 2,
        [("stone", 12), ("log", 6)], 420);
    public static readonly BuildingDef Workshop = new("workshop", "手工坊", 3, 3,
        [("plank", 12), ("stone", 6)], 540);
    public static readonly BuildingDef CopperMine = new("coppermine", "铜矿坑", 2, 2,
        [("plank", 8), ("stone", 8)], 420, NeedsStoneAdjacent: false);
    public static readonly BuildingDef IronMine = new("ironmine", "铁矿坑", 2, 2,
        [("plank", 10), ("stone", 10)], 540);
    public static readonly BuildingDef LivestockPen = new("pen", "畜栏", 2, 2,
        [("log", 8), ("fiber", 4)], 300);
    public static readonly BuildingDef Ranch = new("ranch", "畜牧场", 3, 3,
        [("plank", 10), ("log", 10)], 480);
    public static readonly BuildingDef MeetingHall = new("hall", "集会所", 3, 3,
        [("plank", 15), ("stone", 15)], 720);
    public static readonly BuildingDef School = new("school", "学校", 2, 2,
        [("plank", 12), ("stone", 8)], 600);

    public static readonly Dictionary<string, BuildingDef> All = new()
    {
        [VillageCenter.Key] = VillageCenter,
        [Hut.Key] = Hut,
        [House.Key] = House,
        [Manor.Key] = Manor,
        [Granary.Key] = Granary,
        [Storehouse.Key] = Storehouse,
        [Well.Key] = Well,
        [Farm.Key] = Farm,
        [Cookhouse.Key] = Cookhouse,
        [Sawpit.Key] = Sawpit,
        [Quarry.Key] = Quarry,
        [FishingWharf.Key] = FishingWharf,
        [HuntingLodge.Key] = HuntingLodge,
        [WeavingHut.Key] = WeavingHut,
        [Study.Key] = Study,
        [Smelter.Key] = Smelter,
        [Workshop.Key] = Workshop,
        [CopperMine.Key] = CopperMine,
        [IronMine.Key] = IronMine,
        [LivestockPen.Key] = LivestockPen,
        [Ranch.Key] = Ranch,
        [HerbGarden.Key] = HerbGarden,
        [Clinic.Key] = Clinic,
        [MeetingHall.Key] = MeetingHall,
        [School.Key] = School
    };
}
