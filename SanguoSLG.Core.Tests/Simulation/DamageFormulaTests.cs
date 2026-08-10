namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Simulation;
using Xunit;

/// <summary>피해 공식(design-combat.md)의 문서 검산값을 고정한다.</summary>
public class DamageFormulaTests
{
    [Fact]
    public void Resolve_도검A대도검_기준선760()
    {
        // 병력 1만·atk 8·적성 A(95)·df 10 → 760 (문서 기준선)
        var dmg = DamageFormula.Resolve(10000, 8, 10, new[] { 95 }, System.Array.Empty<int>());
        Assert.Equal(760, dmg);
    }

    [Fact]
    public void Resolve_벽력거건물대성_1187()
    {
        // A급 벽력거(건물dmg 15) 1만 vs 성(df 12) → 1,187 (문서 성 전투 검산)
        var dmg = DamageFormula.Resolve(10000, 15, 12, new[] { 95 }, System.Array.Empty<int>());
        Assert.Equal(1187, dmg);
    }

    [Fact]
    public void Resolve_공격가산30퍼센트_988()
    {
        // 산출 ③ 워크드 예: 적성 95 × 가산 130 → 760 × 1.3 = 988
        var dmg = DamageFormula.Resolve(10000, 8, 10, new[] { 95, 130 }, System.Array.Empty<int>());
        Assert.Equal(988, dmg);
    }

    [Fact]
    public void Resolve_방어가산24퍼센트_612()
    {
        // 방어 +24%(124) → 760 ÷ 1.24 = 612.9 → 내림 612
        var dmg = DamageFormula.Resolve(10000, 8, 10, new[] { 95 }, new[] { 124 });
        Assert.Equal(612, dmg);
    }

    [Fact]
    public void Resolve_병력0이면_피해0()
    {
        Assert.Equal(0, DamageFormula.Resolve(0, 8, 10, new[] { 95 }, System.Array.Empty<int>()));
    }

    [Fact]
    public void Resolve_결과가1미만이면_최소1()
    {
        // 1×8×95÷(1000×10) = 0.0076 → 내림 0 → 최소 1
        Assert.Equal(1, DamageFormula.Resolve(1, 8, 10, new[] { 95 }, System.Array.Empty<int>()));
    }

    [Fact]
    public void Resolve_공격배수없으면_예외()
    {
        Assert.Throws<System.ArgumentException>(
            () => DamageFormula.Resolve(10000, 8, 10, System.Array.Empty<int>(), System.Array.Empty<int>()));
    }
}
