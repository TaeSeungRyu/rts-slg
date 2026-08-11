namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Simulation;
using Xunit;

/// <summary>지속 상태 시스템(design-stratagem.md): DoT tick·만료·정화 범위.</summary>
public class StatusEffectTests
{
    private static readonly IReadOnlyDictionary<string, Stratagem> St =
        new StratagemLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    [Fact]
    public void Tick피해_만분율로_병력비례_내림()
    {
        var burn = new StatusEffect(StatusKind.Burn, TickBasisPoints: 420, Remaining: 6, IsFire: true);
        Assert.Equal(420, burn.TickDamage(10000)); // 4.2%
        Assert.Equal(210, burn.TickDamage(5000));
        Assert.Equal(2, burn.TickDamage(60));      // 2.52 내림
    }

    [Fact]
    public void Tick하면_남은진행이_줄고_0이면_만료()
    {
        var s = new StatusEffect(StatusKind.Poison, 200, 1, IsFire: false);
        Assert.False(s.IsExpired);
        var next = s.Tick();
        Assert.Equal(0, next.Remaining);
        Assert.True(next.IsExpired);
    }

    [Fact]
    public void 화계_MakeStatus_강도반영_화계계열()
    {
        // 화계 base 3%/진행, 지력차 +40(강도 140) → 만분율 420, 6진행, 화계 계열
        var status = St["fire_plot"].MakeStatus(casterIntellect: 100, targetIntellect: 60);
        Assert.NotNull(status);
        Assert.Equal(StatusKind.Burn, status!.Kind);
        Assert.Equal(420, status.TickBasisPoints);
        Assert.Equal(6, status.Remaining);
        Assert.True(status.IsFire);
    }

    [Fact]
    public void 독무_MakeStatus_화계아님()
    {
        var status = St["poison_mist"].MakeStatus(80, 80);
        Assert.NotNull(status);
        Assert.Equal(StatusKind.Poison, status!.Kind);
        Assert.Equal(200, status.TickBasisPoints); // 2% × 강도 100
        Assert.False(status.IsFire);
    }

    [Fact]
    public void 즉발_디버프_정화계략은_상태를_만들지않는다()
    {
        Assert.Null(St["lightning"].MakeStatus(80, 80));  // 즉발
        Assert.Null(St["confound"].MakeStatus(80, 80));   // 디버프
        Assert.Null(St["douse"].MakeStatus(80, 80));      // 정화
    }

    [Fact]
    public void AddStatus는_같은종류를_대체한다()
    {
        var state = UnitCombatState.Create(60)
            .AddStatus(new StatusEffect(StatusKind.Burn, 300, 6, true))
            .AddStatus(new StatusEffect(StatusKind.Burn, 420, 6, true)); // 갱신
        var burn = Assert.Single(state.Statuses);
        Assert.Equal(420, burn.TickBasisPoints);
    }

    [Fact]
    public void TickStatuses는_줄이고_만료된것을_뗀다()
    {
        var state = UnitCombatState.Create(60)
            .AddStatus(new StatusEffect(StatusKind.Burn, 420, 1, true))
            .AddStatus(new StatusEffect(StatusKind.Poison, 200, 3, false))
            .TickStatuses();

        var s = Assert.Single(state.Statuses);       // 화계는 1→0 만료로 제거
        Assert.Equal(StatusKind.Poison, s.Kind);
        Assert.Equal(2, s.Remaining);
    }

    [Fact]
    public void TotalTickDamage는_모든상태_합()
    {
        var state = UnitCombatState.Create(60)
            .AddStatus(new StatusEffect(StatusKind.Burn, 420, 6, true))
            .AddStatus(new StatusEffect(StatusKind.Poison, 200, 4, false));
        Assert.Equal(620, state.TotalTickDamage(10000)); // 420 + 200
    }

    [Fact]
    public void 소화는_화계만_진정은_화계외만_제거()
    {
        var state = UnitCombatState.Create(60)
            .AddStatus(new StatusEffect(StatusKind.Burn, 420, 6, true))
            .AddStatus(new StatusEffect(StatusKind.Poison, 200, 4, false));

        var doused = state.Purge(PurgeScope.Fire);
        Assert.Equal(StatusKind.Poison, Assert.Single(doused.Statuses).Kind);

        var cleansed = state.Purge(PurgeScope.NonFire);
        Assert.Equal(StatusKind.Burn, Assert.Single(cleansed.Statuses).Kind);
    }

    [Fact]
    public void 성복귀하면_지속상태도_해제된다()
    {
        var state = UnitCombatState.Create(60)
            .AddStatus(new StatusEffect(StatusKind.Burn, 420, 6, true))
            .ReturnToCastle();
        Assert.Empty(state.Statuses);
    }

    [Fact]
    public void 수공_MakeStatus_공격감소_강도반영()
    {
        // 수공 base 20%, 지력차 +40(강도 140) → 공격 감소 28%, 2진행
        var status = St["flood_plot"].MakeStatus(casterIntellect: 100, targetIntellect: 60);
        Assert.NotNull(status);
        Assert.Equal(StatusKind.AttackDown, status!.Kind);
        Assert.Equal(28, status.AtkDownPercent);
        Assert.Equal(2, status.Remaining);
        Assert.False(status.RangedOnly);
        Assert.False(status.IsFire);
    }

    [Fact]
    public void 연막_MakeStatus_원거리한정_공격감소()
    {
        // 연막 base 30%, 강도 100 → 30%, 원거리 한정
        var status = St["smokescreen"].MakeStatus(80, 80);
        Assert.NotNull(status);
        Assert.Equal(StatusKind.RangedDown, status!.Kind);
        Assert.Equal(30, status.AtkDownPercent);
        Assert.True(status.RangedOnly);
    }

    [Fact]
    public void 이간_MakeStatus_무효는_지속에_강도반영()
    {
        // 이간: 지속 2진행 × 강도 150(지력차 +50) → 3진행, 적성·패시브 무효
        var status = St["discord"].MakeStatus(casterIntellect: 100, targetIntellect: 50);
        Assert.NotNull(status);
        Assert.Equal(StatusKind.Nullify, status!.Kind);
        Assert.True(status.NullifyAptPassive);
        Assert.Equal(3, status.Remaining);
        Assert.Equal(0, status.AtkDownPercent);
    }

    [Fact]
    public void 혼란_교란은_아직_상태를_만들지않는다()
    {
        // 행동불가·강제 후퇴는 후속 증분 — status_kind 미지정이라 null
        Assert.Null(St["confound"].MakeStatus(80, 80));
        Assert.Null(St["rout"].MakeStatus(80, 80));
    }

    [Fact]
    public void 진정은_능력치디버프도_제거하고_소화는_못한다()
    {
        var state = UnitCombatState.Create(60)
            .AddStatus(new StatusEffect(StatusKind.AttackDown, 0, 2, false, AtkDownPercent: 28));

        Assert.Empty(state.Purge(PurgeScope.NonFire).Statuses); // 진정: 화계 외 제거
        Assert.Single(state.Purge(PurgeScope.Fire).Statuses);   // 소화: 화계만 → 남음
    }
}
