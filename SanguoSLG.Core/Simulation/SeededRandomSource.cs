namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 시드 기반 결정론 난수 공급자. 같은 시드로 만들면 같은 순서의 값을 낸다.
/// </summary>
public sealed class SeededRandomSource : IRandomSource
{
    private readonly Random _random;

    public SeededRandomSource(int seed) => _random = new Random(seed);

    public int Next(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
}
