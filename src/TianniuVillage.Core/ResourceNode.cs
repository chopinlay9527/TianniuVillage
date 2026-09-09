using System.Text.Json.Serialization;

namespace TianniuVillage.Core;

public sealed class ResourceNode
{
    public int Id;
    public ResKind Kind;
    public int X;
    public int Y;
    public int Amount;
    public int RegenDaysLeft;
    public bool Reserved;
    public bool HasBerries = true;

    [JsonIgnore] public bool Available => !Reserved && RegenDaysLeft <= 0 && Amount > 0;

    [JsonIgnore] public string ItemId => Kind switch
    {
        ResKind.Tree => "log",
        ResKind.BerryBush => "berries",
        ResKind.MushroomPatch => "mushroom",
        ResKind.StoneOutcrop => "stone",
        ResKind.HerbPatch => "herb",
        ResKind.FishSpot => "fish",
        ResKind.FlaxPatch => "fiber",
        ResKind.CopperVein => "copper_ore",
        ResKind.IronVein => "iron_ore",
        _ => "log"
    };

    [JsonIgnore] public int WorkMinutes => Kind switch
    {
        ResKind.Tree => 240,
        ResKind.BerryBush => 60,
        ResKind.MushroomPatch => 45,
        ResKind.StoneOutcrop => 300,
        ResKind.HerbPatch => 40,
        ResKind.FishSpot => 90,
        ResKind.FlaxPatch => 50,
        ResKind.CopperVein => 300,
        ResKind.IronVein => 360,
        _ => 60
    };

    [JsonIgnore] public bool IsHeavyWork => Kind is ResKind.Tree or ResKind.StoneOutcrop;
}
