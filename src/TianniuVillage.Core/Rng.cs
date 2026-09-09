namespace TianniuVillage.Core;

public sealed class Rng
{
    private Random _random;

    public Rng(int seed) => _random = new Random(seed);

    public void ReSeed(int seed) => _random = new Random(seed);

    public double NextDouble() => _random.NextDouble();

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

    public int Next(int maxExclusive) => _random.Next(maxExclusive);

    public float NextFloat(float min, float max) => (float)(_random.NextDouble() * (max - min) + min);

    public bool Chance(float probability) => _random.NextDouble() < probability;

    public T Pick<T>(IReadOnlyList<T> list) => list[_random.Next(list.Count)];

    public T PickWeighted<T>(IReadOnlyList<(T item, float weight)> weighted)
    {
        float total = 0;
        foreach (var (_, w) in weighted) total += w;
        double roll = _random.NextDouble() * total;
        foreach (var (item, w) in weighted)
        {
            roll -= w;
            if (roll <= 0) return item;
        }
        return weighted[^1].item;
    }

    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
