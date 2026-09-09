namespace TianniuVillage.Core;

public sealed class RoadSegment
{
    public int Id;
    public List<int> Tiles = [];
    public byte TargetLevel;
    public int MaterialsRequired;
    public int MaterialsDelivered;
    public int NextBuildIndex;

    public bool MaterialsReady => MaterialsDelivered >= MaterialsRequired;
}
