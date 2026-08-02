namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 시뮬레이션용 난수 공급자. 전역 Random이나 new Random()을 즉석 생성하지 않고
/// 항상 이 인터페이스를 주입받아 결정론(같은 시드 = 같은 결과)을 보장한다.
/// </summary>
public interface IRandomSource
{
    /// <summary>[minInclusive, maxExclusive) 범위의 정수를 반환한다.</summary>
    int Next(int minInclusive, int maxExclusive);
}
