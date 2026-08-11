namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 패시브 스킬의 개별 효과 한 줄(design-skill-passives.md). 조건이 맞으면 경험 단계(1·2·3)에
/// 해당하는 퍼센트를 버킷에 더한다. 트레이드오프의 마이너스는 음수 <see cref="Tiers"/>로 표현한다.
/// </summary>
/// <param name="Bucket">공격/방어 버킷.</param>
/// <param name="Condition">발동 조건.</param>
/// <param name="Tiers">경험 단계 1·2·3의 퍼센트(음수 가능).</param>
public sealed record PassiveEffect(
    SkillBucket Bucket,
    PassiveCondition Condition,
    IReadOnlyList<int> Tiers)
{
    /// <summary>경험 단계(1~3)의 퍼센트.</summary>
    public int AmountAtTier(int tier) => Tiers[System.Math.Clamp(tier, 1, 3) - 1];
}
