namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Spatial;

/// <summary>
/// 계략 정의(design-stratagem.md "계략 목록"). 수치는 data/stratagems.json에서 온다.
/// 효과 강도는 <see cref="StratagemStrength"/>가 곱해진다.
/// </summary>
/// <param name="Code">계략 코드.</param>
/// <param name="Name">한국어 이름.</param>
/// <param name="EffectKind">효과 종류.</param>
/// <param name="RequiredLevel">시전에 필요한 숙달 단계(1~10).</param>
/// <param name="Cost">모략력 소모.</param>
/// <param name="BaseValue">효과 기본값(즉발/지속 = 병력 %, 디버프 = 효과별 크기).</param>
/// <param name="Duration">지속 진행 수(즉발·정화는 0).</param>
/// <param name="Range">사거리.</param>
/// <param name="TerrainRule">발동 지형 조건.</param>
/// <param name="Status">지속 피해(DoT)가 남기는 상태 종류(그 외는 null).</param>
/// <param name="Purge">정화 계략이 제거하는 상태 범위(비정화는 None).</param>
public sealed record Stratagem(
    string Code,
    string Name,
    StratagemEffectKind EffectKind,
    int RequiredLevel,
    int Cost,
    int BaseValue,
    int Duration,
    int Range,
    StratagemTerrainRule TerrainRule,
    StatusKind? Status = null,
    PurgeScope Purge = PurgeScope.None)
{
    /// <summary>대상 타일 지형에서 발동 가능한가.</summary>
    public bool CanCastOn(TerrainType targetTerrain) => TerrainRule switch
    {
        StratagemTerrainRule.None => true,
        StratagemTerrainRule.RiverOnly => targetTerrain == TerrainType.River,
        StratagemTerrainRule.RiverForbidden => targetTerrain != TerrainType.River,
        _ => true,
    };

    /// <summary>
    /// 즉발·지속 피해 계략이 대상에게 주는 피해(강도 배율 반영). 디버프·정화는 0.
    /// 지속 피해는 진행당 tick 값이다.
    /// </summary>
    public int Damage(int targetTroops, int casterIntellect, int targetIntellect)
    {
        if (EffectKind is not (StratagemEffectKind.InstantDamage or StratagemEffectKind.DamageOverTime))
        {
            return 0;
        }

        var strength = StratagemStrength.Percent(casterIntellect, targetIntellect);
        return (int)((long)targetTroops * BaseValue * strength / 10000);
    }

    /// <summary>
    /// 지속 피해(DoT) 계략이 대상에 남기는 상태(강도 배율을 만분율에 반영). 그 외는 null.
    /// 화계는 <see cref="StatusKind.Burn"/>이라 정화 판정에서 화계 계열로 취급된다.
    /// </summary>
    public StatusEffect? MakeStatus(int casterIntellect, int targetIntellect)
    {
        if (EffectKind != StratagemEffectKind.DamageOverTime || Status is null)
        {
            return null;
        }

        var strength = StratagemStrength.Percent(casterIntellect, targetIntellect);
        return new StatusEffect(Status.Value, BaseValue * strength, Duration, IsFire: Status.Value == StatusKind.Burn);
    }
}
