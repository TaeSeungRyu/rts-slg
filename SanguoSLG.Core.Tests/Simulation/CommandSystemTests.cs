namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>내정 ③ 명령 계층 — 발행(검증·예약·잠김) → 완료일 정산. design-administration "명령 실행".</summary>
public class CommandSystemTests
{
    private static readonly CommandBalance B = new();

    // 정치 90(량주 출신), 보좌 정치 70, 무력 92, 정치 40(건설 불가)
    private static General Pol(int id, int politics, string region = "") => new(
        new GeneralId(id), $"g{id}",
        new Dictionary<TroopClass, AptitudeGrade>(), Might: 50, Intellect: 50, Politics: politics, Region: region);

    private static General Mig(int id, int might) => new(
        new GeneralId(id), $"m{id}",
        new Dictionary<TroopClass, AptitudeGrade>(), Might: might, Intellect: 50, Politics: 50);

    private static City Town(int id, string region = "jizhou", int gold = 1000, int ore = 5000,
        int population = 100_000, CastleSize castle = CastleSize.Medium, int troops = 0, int train = 0) =>
        new(new CityId(id), $"c{id}", new HexCoord(0, 0), new FactionId(1), 5000, castle,
            Gold: gold, Population: population, Ore: ore, Region: region, Troops: troops, TrainingLevel: train);

    private static GameState State(IEnumerable<City> cities, IEnumerable<General> generals) =>
        new(1, 1, new List<Faction>(), cities.ToList(), generals.ToList());

    // ── 효율(A 주관·보좌, B 출신지) ──

    [Fact]
    public void 효율_주관에_보좌가_반계수로_더해진다()
    {
        // 주관 정치 90 + 보좌 정치 70×50% = 90 + 35 = 125
        var eff = CommandEfficiency.Effective(Pol(1, 90), Pol(2, 70), Town(9, region: ""), CommandKind.Recruit, B);
        Assert.Equal(125, eff);
    }

    [Fact]
    public void 효율_출신지가_도시와_같으면_보너스가_붙는다()
    {
        // 정치 90, 량주 출신, 도시도 량주 → 90 × 1.2 = 108
        var home = CommandEfficiency.Effective(Pol(1, 90, "liangzhou"), null, Town(9, "liangzhou"), CommandKind.Recruit, B);
        Assert.Equal(108, home);

        // 타지면 보너스 없음
        var away = CommandEfficiency.Effective(Pol(1, 90, "liangzhou"), null, Town(9, "jizhou"), CommandKind.Recruit, B);
        Assert.Equal(90, away);
    }

    [Fact]
    public void 효율_훈련은_무력을_쓴다()
    {
        var eff = CommandEfficiency.Effective(Mig(1, 92), null, Town(9), CommandKind.Train, B);
        Assert.Equal(92, eff);
    }

    // ── 발행 검증 ──

    [Fact]
    public void 발행_잠긴_장수는_다시_명령을_받지못한다()
    {
        var svc = new CommandService(B);
        var s0 = State(new[] { Town(1) }, new[] { Pol(1, 90) });

        var first = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.SetTaxRate, new GeneralId(1), Value: 30));
        Assert.True(first.Ok);

        var second = svc.Issue(first.State, new CommandRequest(new CityId(1), CommandKind.SetTaxRate, new GeneralId(1), Value: 40));
        Assert.False(second.Ok);
        Assert.Contains("매여", second.Error);
    }

    [Fact]
    public void 발행_건설은_정치_70이하면_거부된다()
    {
        var svc = new CommandService(B);
        var s0 = State(new[] { Town(1) }, new[] { Pol(1, 70), Pol(2, 71) });

        Assert.False(svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Build, new GeneralId(1), Facility: "paddy")).Ok);
        Assert.True(svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Build, new GeneralId(2), Facility: "paddy")).Ok);
    }

    [Fact]
    public void 발행_모병은_광석과_인구를_즉시_예약한다()
    {
        // 정치 90 → 산출 1350, 인구캡 1%(=1000), 광석 5000 → min = 1000
        var svc = new CommandService(B);
        var s0 = State(new[] { Town(1, ore: 5000, population: 100_000) }, new[] { Pol(1, 90) });

        var r = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1)));
        Assert.True(r.Ok);
        var city = r.State.Cities.Single();
        Assert.Equal(4000, city.Ore);        // 5000 − 1000 예약
        Assert.Equal(99_000, city.Population); // 100000 − 1000
        Assert.Equal(0, city.Troops);         // 아직 지급 전(정산은 완료일)
    }

    // ── 전체 루프: 발행 → 7일 진행 → 정산 ──

    [Fact]
    public void 루프_모병_7일뒤_병력이_들어오고_장수가_풀린다()
    {
        var svc = new CommandService(B);
        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100), B);
        var s0 = State(new[] { Town(1, ore: 5000, population: 100_000) }, new[] { Pol(1, 90) });

        var issued = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1)));
        Assert.True(issued.State.IsGeneralBusy(new GeneralId(1)));

        var d6 = world.AdvanceDays(issued.State, 6);
        Assert.Equal(0, d6.Cities.Single().Troops);            // 아직
        Assert.True(d6.IsGeneralBusy(new GeneralId(1)));

        var d7 = world.AdvanceDays(issued.State, 7);
        Assert.Equal(1000, d7.Cities.Single().Troops);         // 지급
        Assert.Equal(B.RecruitTrainLevel, d7.Cities.Single().TrainingLevel);
        Assert.False(d7.IsGeneralBusy(new GeneralId(1)));      // 잠금 해제
        Assert.Empty(d7.Commands);
    }

    [Fact]
    public void 루프_징병은_훈련도0_병력과_치안하락으로_정산된다()
    {
        var svc = new CommandService(B);
        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100), B);
        var city = Town(1, ore: 5000, population: 100_000) with { Security = 80 };
        var s0 = State(new[] { city }, new[] { Pol(1, 100) });

        var issued = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Conscript, new GeneralId(1)));
        var done = world.AdvanceDays(issued.State, 7);
        var c = done.Cities.Single();

        // 정치 100 → 능력 산출 1500(인구 3% 캡 3000엔 못 닿아 능력 바운드)
        Assert.Equal(1500, c.Troops);
        Assert.Equal(0, c.TrainingLevel);     // 징병 훈련도 0
        Assert.Equal(80 - 1 * 5, c.Security); // 1500/1000 = 1 × 5 = 5 하락
    }

    [Fact]
    public void 루프_건설은_30일뒤_시설이_생기고_금이_예약된다()
    {
        var svc = new CommandService(B);
        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0), B);
        var s0 = State(new[] { Town(1, gold: 1000, castle: CastleSize.Medium) }, new[] { Pol(1, 90) });

        var issued = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Build, new GeneralId(1), Facility: "paddy"));
        Assert.Equal(700, issued.State.Cities.Single().Gold); // 300 예약

        var done = world.AdvanceDays(issued.State, 30);
        Assert.Equal(1, done.Cities.Single().Paddies);
    }

    [Fact]
    public void 루프_두_장수를_두_명령에_나눠_동시_진행한다()
    {
        var svc = new CommandService(B);
        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0), B);
        var s0 = State(new[] { Town(1, ore: 5000) }, new[] { Pol(1, 90), Mig(2, 80) });

        var a = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1)));
        var b = svc.Issue(a.State, new CommandRequest(new CityId(1), CommandKind.Train, new GeneralId(2)));
        Assert.True(b.Ok);
        Assert.Equal(2, b.State.Commands.Count);

        var done = world.AdvanceDays(b.State, 7);
        // 같은 날 정산: 모병(1000@50) 먼저 → 훈련 +8 → 58 (신병을 같은 주에 조련)
        Assert.Equal(1000, done.Cities.Single().Troops);
        Assert.Equal(58, done.Cities.Single().TrainingLevel);
        Assert.Empty(done.Commands);
    }
}
