namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 전투 피해 공식(design-combat.md "피해 공식"·"전투값 산출 순서 ④").
/// <code>피해 = 병력 × atk × 적성% × 기타 배수% ÷ (1,000 × df × 방어배수%)</code>
/// 정수 고정소수점 — 곱을 전부 먼저, 나눗셈은 마지막 한 번(내림, 최소 1)이라 결정론이 지켜진다.
/// 배수는 정수 퍼센트(100 = ×1.0)로 넘긴다.
/// </summary>
public static class DamageFormula
{
    /// <summary>
    /// 한 부대가 대상에 주는 피해. <paramref name="atkPercents"/>는 최소 1개(적성 등, 100=중립),
    /// <paramref name="dfPercents"/>는 방어측 배수(100=중립, 클수록 피해↓)다.
    /// 기준선: 도검 A(적성 95) 1만 vs 도검(df 10) = 병력 1만·atk 8·[95]·df 10·[] → 760.
    /// </summary>
    public static int Resolve(
        int troops,
        int atkStat,
        int dfStat,
        IReadOnlyList<int> atkPercents,
        IReadOnlyList<int> dfPercents)
    {
        if (atkPercents.Count == 0)
        {
            throw new ArgumentException("공격 배수는 최소 1개(적성 등)여야 한다.", nameof(atkPercents));
        }
        if (troops <= 0 || atkStat <= 0)
        {
            return 0;
        }
        if (dfStat <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(dfStat), "df는 1 이상이어야 한다.");
        }

        // 기준 공식 `병력 × atk × 적성 ÷ (1000 × df)`가 적성 하나를 /1000에 흡수한다.
        // 추가 공격 배수는 각각 ÷100, 방어 배수는 각각 ×100(분모로) 되도록 100의 지수로 맞춘다.
        Int128 num = (Int128)troops * atkStat;
        foreach (var p in atkPercents)
        {
            num *= p;
        }
        for (var i = 0; i < dfPercents.Count; i++)
        {
            num *= 100;
        }

        Int128 den = (Int128)1000 * dfStat;
        foreach (var p in dfPercents)
        {
            den *= p;
        }
        for (var i = 0; i < atkPercents.Count - 1; i++)
        {
            den *= 100;
        }

        var dmg = num / den; // Int128 나눗셈은 0을 향해 절삭 — 양수라 내림과 같다
        return dmg < 1 ? 1 : (int)dmg;
    }
}
