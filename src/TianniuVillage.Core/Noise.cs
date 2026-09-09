namespace TianniuVillage.Core;

public static class Noise
{
    public static float Fbm(float x, float y, int seed, int octaves = 4, float lacunarity = 2f, float gain = 0.5f)
    {
        float sum = 0, amp = 1, freq = 1, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            sum += ValueNoise(x * freq, y * freq, seed + i * 1013) * amp;
            norm += amp;
            amp *= gain;
            freq *= lacunarity;
        }
        return sum / norm;
    }

    public static float ValueNoise(float x, float y, int seed)
    {
        int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
        float tx = x - x0, ty = y - y0;
        float sx = tx * tx * (3 - 2 * tx);
        float sy = ty * ty * (3 - 2 * ty);
        float n00 = Hash(x0, y0, seed);
        float n10 = Hash(x0 + 1, y0, seed);
        float n01 = Hash(x0, y0 + 1, seed);
        float n11 = Hash(x0 + 1, y0 + 1, seed);
        return (n00 * (1 - sx) + n10 * sx) * (1 - sy) + (n01 * (1 - sx) + n11 * sx) * sy;
    }

    private static float Hash(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2246822519);
        h = (h ^ (h >> 13)) * 1274126177;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / (float)0xFFFFFF;
    }
}
