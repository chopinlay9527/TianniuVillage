namespace TianniuVillage.Core;

/// <summary>海拔 / 山脊 / 湿度 → 7 类地形的分层规则（阈值全部来自 <see cref="WorldGenConfig"/>）。</summary>
public static class BiomeClassifier
{
    public static Terrain Classify(float height, float ridge, float moisture, WorldGenConfig c)
    {
        if (height < c.DeepWaterLevel) return Terrain.DeepWater;
        if (height < c.WaterLevel) return Terrain.Water;
        if (height < c.SandLevel) return Terrain.Sand;

        // 山脊噪声沿等值线形成连绵山脉链，而非孤立斑点
        if (height > c.MountainLevel && ridge > c.RidgeMountain) return Terrain.Mountain;
        if (height > c.HighlandLevel && ridge > c.RidgeHighland) return Terrain.Highland;
        if (height > c.MountainLevel + 0.06f) return Terrain.Mountain;
        if (height > c.HighlandLevel) return Terrain.Highland;

        return moisture > c.ForestMoisture ? Terrain.Forest : Terrain.Grass;
    }

    /// <summary>噪声 [-1,1] → 0..1（含对比度拉伸）。</summary>
    public static float Normalize(float noise, float contrast) =>
        Math.Clamp((noise + 1f) * 0.5f * contrast, 0f, 1f);
}
