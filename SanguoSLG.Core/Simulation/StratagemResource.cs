namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 모략력(design-stratagem.md "모략력 — 계략 자원"). 최대치 = 선봉 지력, 출전 시 가득, 계략 발동마다
/// 비용 차감, 성 복귀 시 다시 가득. 불변 값 — 소비·충전은 새 값을 돌려준다.
/// </summary>
/// <param name="Max">최대 모략력(선봉 지력).</param>
/// <param name="Current">현재 모략력.</param>
public sealed record StratagemResource(int Max, int Current)
{
    /// <summary>선봉 지력으로 가득 찬 모략력을 만든다(출전 시).</summary>
    public static StratagemResource FromIntellect(int intellect) => new(intellect, intellect);

    /// <summary>이 비용을 지불할 수 있는가.</summary>
    public bool CanSpend(int cost) => Current >= cost;

    /// <summary>비용을 차감한 새 값(부족하면 예외).</summary>
    public StratagemResource Spend(int cost)
        => Current >= cost
            ? this with { Current = Current - cost }
            : throw new System.InvalidOperationException("모략력이 부족하다.");

    /// <summary>성 복귀 — 가득 채운다.</summary>
    public StratagemResource Refill() => this with { Current = Max };
}
