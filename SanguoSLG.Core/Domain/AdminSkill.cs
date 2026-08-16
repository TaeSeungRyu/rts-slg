namespace SanguoSLG.Core.Domain;

/// <summary>
/// 내정 스킬(design-skill-admin.md). 전부 패시브 — 담당관(태수)이 재임 중일 때 티어별 수치가
/// 도시 수입·치안·자원 산출에 상시 반영된다(액티브는 2026-08-16 폐지). 버킷이 어디에 붙는지를 정한다.
/// </summary>
public sealed record AdminSkill(
    string Code,
    string Name,
    string Bucket,
    IReadOnlyList<int>? Tiers = null)
{
    /// <summary>티어별 수치(1~3). 정의가 없으면 0.</summary>
    public int AmountAtTier(int tier)
        => Tiers is { Count: >= 3 } ? Tiers[System.Math.Clamp(tier, 1, 3) - 1] : 0;
}
