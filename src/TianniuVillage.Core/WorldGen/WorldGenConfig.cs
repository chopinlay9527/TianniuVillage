namespace TianniuVillage.Core;

/// <summary>世界生成的可调参数（集中一处，便于调参与测试）。</summary>
public sealed class WorldGenConfig
{
    public static readonly WorldGenConfig Default = new();

    // --- 噪声频率（FastNoiseLite 坐标为像素, 频率越小特征越大）---
    public float HeightFrequency = 0.010f;
    public float RidgeFrequency = 0.008f;
    public float MoistureFrequency = 0.017f;
    /// <summary>把噪声 [-1,1] 拉伸后的对比度（越大高低差越明显）。</summary>
    public float HeightContrast = 1.15f;

    // --- 海拔分层阈值（0..1）---
    public float DeepWaterLevel = 0.30f;
    public float WaterLevel = 0.42f;
    public float SandLevel = 0.46f;
    public float HighlandLevel = 0.70f;
    public float MountainLevel = 0.76f;
    /// <summary>山脊噪声阈值：配合海拔形成连绵山脉而非孤立斑块。</summary>
    public float RidgeMountain = 0.55f;
    public float RidgeHighland = 0.45f;
    public float ForestMoisture = 0.58f;

    // --- 海岸/河流 ---
    public int CoastSmoothPasses = 2;
    /// <summary>海岸沙滩环宽度（格）；0=不强制。官方图集水↔草/泥均有过渡，此环纯为风格。</summary>
    public int CoastalSandWidth = 1;
    public int RiverCount = 3;
    public int RiverMaxSteps = 500;
}
