namespace TianniuVillage.Core;

public sealed class Job
{
    public int Id;
    public JobKind Kind;
    public JobState State = JobState.Open;
    public int Priority = 50;
    public int X;
    public int Y;
    public int NodeId;
    public int BuildingId;
    public int CellIdx;
    public int AnimalId;
    public int SegmentId;
    public int TileIdx = -1;
    public int Phase;
    public string ItemId = "";
    public int Units = 1;
    public int ClaimedBy;
    public string ConsumedItem = "";

    public string SkillKey => Kind switch
    {
        JobKind.Fell => "gathering",
        JobKind.Forage => "gathering",
        JobKind.PickMushroom => "gathering",
        JobKind.GatherHerb => "medicine",
        JobKind.Mine => "building",
        JobKind.Fish => "hunting",
        JobKind.Hunt => "hunting",
        JobKind.Plow => "farming",
        JobKind.Sow => "farming",
        JobKind.Harvest => "farming",
        JobKind.Build => "building",
        JobKind.Cook => "cooking",
        JobKind.Saw => "building",
        JobKind.HaulStone => "building",
        JobKind.BuildRoad => "building",
        JobKind.RepairRoad => "building",
        JobKind.FetchWater => "gathering",
        JobKind.Weave => "building",
        JobKind.SewClothes => "building",
        JobKind.GatherFiber => "gathering",
        JobKind.MineOre => "building",
        JobKind.Smelt => "building",
        JobKind.CraftTool => "building",
        JobKind.TendLivestock => "farming",
        _ => "gathering"
    };

    public bool IsHeavy => Kind is JobKind.Fell or JobKind.Mine or JobKind.Plow;

    public string Describe(Game game)
    {
        return Kind switch
        {
            JobKind.Fell => "伐木",
            JobKind.Forage => "采集浆果",
            JobKind.PickMushroom => "采摘蘑菇",
            JobKind.Mine => "采石",
            JobKind.GatherHerb => "采集草药",
            JobKind.Fish => "捕鱼",
            JobKind.Hunt => "狩猎",
            JobKind.Plow => "开垦农田",
            JobKind.Sow => "播种",
            JobKind.Harvest => "收割庄稼",
            JobKind.Build => $"建造{BuildingName(game, BuildingId)}",
            JobKind.Cook => "烹饪",
            JobKind.Saw => "锯木板",
            JobKind.HaulStone => "搬运石料",
            JobKind.BuildRoad => "修路",
            JobKind.RepairRoad => "修补道路",
            JobKind.FetchWater => "打水",
            JobKind.Weave => "织布",
            JobKind.SewClothes => "缝制冬衣",
            JobKind.GatherFiber => "采集纤维",
            JobKind.MineOre => "采矿",
            JobKind.Smelt => "冶炼",
            JobKind.CraftTool => "打造工具",
            JobKind.TendLivestock => "照料牲畜",
            _ => "劳作"
        };
    }

    private string BuildingName(Game game, int buildingId)
    {
        var b = game.World.Buildings.FirstOrDefault(x => x.Id == buildingId);
        return b != null && BuildingDefs.All.TryGetValue(b.Key, out var def)
            ? def.NameZh
            : "建筑";
    }
}
