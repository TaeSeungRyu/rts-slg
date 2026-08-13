namespace SanguoSLG.Core.Domain;

/// <summary>
/// 내정 스킬(design-skill-admin.md). 패시브는 티어 1~3 수치, 액티브는 1회 발동량.
/// 효과 배선은 내정 시스템 구현과 함께 — 지금은 데이터 그릇.
/// </summary>
public sealed record AdminSkill(
    string Code,
    string Name,
    bool IsActive,
    string Bucket,
    IReadOnlyList<int>? Tiers = null,
    int Amount = 0)
{
    /// <summary>패시브의 티어별 수치(1~3). 액티브면 0.</summary>
    public int AmountAtTier(int tier)
        => Tiers is { Count: >= 3 } ? Tiers[System.Math.Clamp(tier, 1, 3) - 1] : 0;
}
