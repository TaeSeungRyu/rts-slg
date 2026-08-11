namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 부대의 병력 구성(design-combat.md "피해 구성 — 소실/부상"). 피해는 활성 병력을 깎되, 그중
/// 일부만 <see cref="Wounded"/>(부상, 회복 가능)로 쌓이고 나머지는 영구 소실된다. 회복은 부상 풀에서만
/// 되돌린다. 불변 값 — 변경은 새 값을 돌려준다.
/// </summary>
/// <param name="Active">현재 활성(전투) 병력.</param>
/// <param name="Wounded">회복 가능한 부상 병력 풀.</param>
public sealed record TroopPool(int Active, int Wounded)
{
    /// <summary>
    /// 피해 적용: 활성 병력이 <paramref name="damage"/>만큼(활성 한도 내) 줄고, 그 손실의
    /// <paramref name="woundedPercent"/>%가 부상 풀로 전환된다(나머지는 영구 소실).
    /// </summary>
    public TroopPool TakeDamage(int damage, int woundedPercent)
    {
        if (damage <= 0)
        {
            return this;
        }

        var loss = System.Math.Min(damage, Active);
        var wounded = Wounded + loss * woundedPercent / 100;
        return new TroopPool(Active - loss, wounded);
    }

    /// <summary>부상 풀에서 활성 병력으로 되돌린다(풀을 초과하면 풀까지만).</summary>
    public TroopPool Heal(int amount)
    {
        if (amount <= 0)
        {
            return this;
        }

        var moved = System.Math.Min(amount, Wounded);
        return new TroopPool(Active + moved, Wounded - moved);
    }
}
