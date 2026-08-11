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
public sealed record Stratagem(
    string Code,
    string Name,
    StratagemEffectKind EffectKind,
    int RequiredLevel,
    int Cost,
    int BaseValue,
    int Duration,
    int Range,
    StratagemTerrainRule TerrainRule)
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
}
