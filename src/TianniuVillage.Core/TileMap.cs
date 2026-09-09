namespace TianniuVillage.Core;

public sealed class TileMap
{
    public readonly int W;
    public readonly int H;
    public readonly Terrain[] Tiles;
    public readonly byte[] Roads;
    public readonly ushort[] RoadWear;
    public readonly bool[] Blocked;
    public float WaterCostMul = 1f;

    public TileMap(int w, int h)
    {
        W = w;
        H = h;
        Tiles = new Terrain[w * h];
        Roads = new byte[w * h];
        RoadWear = new ushort[w * h];
        Blocked = new bool[w * h];
    }

    public int Index(int x, int y) => y * W + x;
    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;

    public Terrain Get(int x, int y) => Tiles[y * W + x];

    public bool IsLand(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        var t = Tiles[y * W + x];
        return t != Terrain.DeepWater && t != Terrain.Water;
    }

    public bool Walkable(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        if (Blocked[y * W + x]) return false;
        return Tiles[y * W + x] != Terrain.DeepWater;
    }

    public byte RoadLevel(int x, int y) => Roads[y * W + x];

    public float RoadSpeed(int x, int y) => Balance.RoadSpeedMult[Roads[y * W + x]];

    public float MoveCost(int x, int y)
    {
        float terrain = Tiles[y * W + x] switch
        {
            Terrain.Water => 3f * WaterCostMul,
            Terrain.Mountain => 5f,
            Terrain.Sand => 1.25f,
            Terrain.Forest => 1.6f,
            Terrain.Highland => 1.35f,
            Terrain.Grass => 1f,
            _ => 1f
        };
        return terrain / Balance.RoadSpeedMult[Roads[y * W + x]];
    }
}
