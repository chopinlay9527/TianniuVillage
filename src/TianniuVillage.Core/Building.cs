using System.Text.Json.Serialization;

namespace TianniuVillage.Core;

public sealed class Building
{
    public int Id;
    public string Key = "house";
    public int X;
    public int Y;
    public BuildingState State;
    public int WorkDone;
    public int Occupants;
    public Dictionary<string, int> Delivered = new();
    public string LivestockType = "";
    public int LivestockCount;
    public Dictionary<string, int> ProdBuffer = new();
    public int[] CropPhase = [];
    public float[] CropGrowth = [];

    public bool MaterialsReady
    {
        get
        {
            foreach (var (item, count) in Def.Cost)
                if (Delivered.GetValueOrDefault(item) < count) return false;
            return true;
        }
    }

    [JsonIgnore] public int Beds => Key switch
    {
        "hut" => 2,
        "house" => 4,
        "manor" => 8,
        _ => 0
    };

    [JsonIgnore] public float Warmth => Key switch
    {
        "hut" => 0.3f,
        "house" => 0.6f,
        "manor" => 0.9f,
        _ => 0f
    };

    [JsonIgnore] public float ComfortBonus => Key switch
    {
        "hut" => 0f,
        "house" => 0.2f,
        "manor" => 0.5f,
        _ => 0f
    };

    [JsonIgnore] public BuildingDef Def => BuildingDefs.All[Key];
    [JsonIgnore] public int W => Def.W;
    [JsonIgnore] public int H => Def.H;
    [JsonIgnore] public int CenterX => X + W / 2;
    [JsonIgnore] public int CenterY => Y + H / 2;
}
