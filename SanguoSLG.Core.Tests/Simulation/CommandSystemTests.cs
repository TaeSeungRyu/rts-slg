namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>내정 ③ 명령 계층 — 발행(검증·예약·잠김) → 완료일 정산. 병종은 모집 시 지정(2026-08-16).</summary>
public class CommandSystemTests
{
    private static readonly CommandBalance B = new();

    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    private static CommandService Service() => new(B, Troops);

    private static General Pol(int id, int politics, string region = "") => new(
        new GeneralId(id), $"g{id}",
        new Dictionary<TroopClass, AptitudeGrade>(), Might: 50, Intellect: 50, Politics: politics, Region: region);

    private static General Mig(int id, int might) => new(
        new GeneralId(id), $"m{id}",
        new Dictionary<TroopClass, AptitudeGrade>(), Might: might, Intellect: 50, Politics: 50);

    private static City Town(int id, string region = "jizhou", int gold = 1000, int ore = 5000,
        int population = 100_000, CastleSize castle = CastleSize.Medium, int horses = 0, int elephants = 0) =>
        new(new CityId(id), $"c{id}", new HexCoord(0, 0), new FactionId(1), 5000, castle,
            Gold: gold, Population: population, Ore: ore, Horses: horses, Elephants: elephants, Region: region);

    private static GameState State(IEnumerable<City> cities, IEnumerable<General> generals,
        IEnumerable<GarrisonForce>? garrisons = null) =>
        new(1, 1, new List<Faction>(), cities.ToList(), generals.ToList(),
            GarrisonForces: garrisons?.ToList());

    private static GarrisonForce GarrisonOf(GameState s, int cityId, string troop) =>
        s.Garrisons.Single(g => g.City == new CityId(cityId) && g.TroopCode == troop);

    // ── 효율(A 주관·보좌, B 출신지) ──

    [Fact]
    public void 효율_주관에_보좌가_반계수로_더해진다()
    {
        var eff = CommandEfficiency.Effective(Pol(1, 90), Pol(2, 70), Town(9, region: ""), CommandKind.Recruit, B);
        Assert.Equal(125, eff);
    }

    [Fact]
    public void 효율_출신지가_도시와_같으면_보너스가_붙는다()
    {
        var home = CommandEfficiency.Effective(Pol(1, 90, "liangzhou"), null, Town(9, "liangzhou"), CommandKind.Recruit, B);
        Assert.Equal(108, home);

        var away = CommandEfficiency.Effective(Pol(1, 90, "liangzhou"), null, Town(9, "jizhou"), CommandKind.Recruit, B);
        Assert.Equal(90, away);
    }

    // ── 태수 내정 스킬(재임 시 명령에 반영) ──

    private static readonly IReadOnlyList<AdminSkill> AdminSkills =
        new AdminSkillLoader().LoadFromDirectory(TestData.DataDirectory());

    private static General GovWith(int id, string skillCode, int tier, int politics = 90, int might = 50) => new(
        new GeneralId(id), $"gov{id}", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: might, Intellect: 50, Politics: politics,
        AdminPassives: new[] { new GeneralSkill(skillCode, tier) });

    private static GameState WithGovernor(City city, General gov) => new(
        1, 1, new List<Faction>(), new List<City> { city with { Governor = gov.Id } },
        new List<General> { gov },
        Postings: new List<GeneralPosting> { new(gov.Id, city.Owner, city.Id) });

    [Fact]
    public void 모병관_태수면_모집_병력이_증가한다()
    {
        var svc = new CommandService(B, Troops, adminSkills: AdminSkills);
        var city = Town(1, ore: 1_000_000); // 광석 하드 캡에 걸리지 않게 넉넉히
        var req = new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1), TroopCode: "swordsman");

        var withGov = svc.Issue(WithGovernor(city, GovWith(1, "recruiter", 3)), req);      // 모병관 T3 = +30%
        var without = svc.Issue(WithGovernor(city, GovWith(1, "merchant", 3)), req);       // 무관 스킬 = +0%
        Assert.True(withGov.Ok, withGov.Error);

        var boosted = withGov.State.Commands.Single().Amount;
        var baseline = without.State.Commands.Single().Amount;
        Assert.Equal(baseline * 130 / 100, boosted); // 정확히 +30%
    }

    [Fact]
    public void 모병관이_없는_태수면_모집_병력이_그대로다()
    {
        var svc = new CommandService(B, Troops, adminSkills: AdminSkills);
        var city = Town(1, ore: 1_000_000);
        var req = new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1), TroopCode: "swordsman");

        var noSkill = svc.Issue(WithGovernor(city, GovWith(1, "merchant", 3)), req).State.Commands.Single().Amount;
        var noAdminData = new CommandService(B, Troops).Issue(WithGovernor(city, GovWith(1, "recruiter", 3)), req)
            .State.Commands.Single().Amount; // 스킬 데이터 미주입 → 보너스 0
        Assert.Equal(noSkill, noAdminData);
    }

    [Fact]
    public void 인망_태수면_모집_인구감소가_줄고_병력은_그대로다()
    {
        var svc = new CommandService(B, Troops, adminSkills: AdminSkills);
        var city = Town(1, ore: 1_000_000, population: 1_000_000);
        var req = new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1), TroopCode: "swordsman");

        var without = svc.Issue(WithGovernor(city, GovWith(1, "merchant", 3)), req);        // 인망 없음
        var withGov = svc.Issue(WithGovernor(city, GovWith(1, "popularity", 3)), req);      // 인망 T3 = 인구 −25%
        Assert.True(withGov.Ok, withGov.Error);

        // 병력 수는 동일(인망은 병력을 늘리지 않는다).
        Assert.Equal(without.State.Commands.Single().Amount, withGov.State.Commands.Single().Amount);

        // 인구 감소만 25% 줄었다.
        var popNoSkill = 1_000_000 - without.State.Cities.Single().Population;
        var popWithGov = 1_000_000 - withGov.State.Cities.Single().Population;
        Assert.Equal(popNoSkill - (popNoSkill * 25 / 100), popWithGov);
    }

    [Fact]
    public void 교관_태수면_훈련_상승량이_가산된다()
    {
        var svc = new CommandService(B, Troops, adminSkills: AdminSkills);
        var city = Town(1);
        var garr = new List<GarrisonForce> { new(new CityId(1), "swordsman", 2000, 30) };
        var req = new CommandRequest(new CityId(1), CommandKind.Train, new GeneralId(1), TroopCode: "swordsman");

        GameState WithGarr(General gov) => WithGovernor(city, gov) with { GarrisonForces = garr };
        var without = svc.Issue(WithGarr(GovWith(1, "merchant", 3, might: 70)), req);       // 교관 없음
        var withGov = svc.Issue(WithGarr(GovWith(1, "drillmaster", 3, might: 70)), req);    // 교관 T3 = +6
        Assert.True(withGov.Ok, withGov.Error);

        Assert.Equal(without.State.Commands.Single().Amount + 6, withGov.State.Commands.Single().Amount);
    }

    // ── 시장 매입(즉시) + 교역 ──

    private static readonly BalanceConfig Econ = new(MonthlyTaxPerCity: 0);

    private static CommandService MarketSvc() => new(B, Troops, Econ, AdminSkills);

    [Fact]
    public void 시장_금으로_광석을_사면_금은_줄고_광석은_는다()
    {
        var city = Town(1, gold: 10_000, ore: 0); // 시세 100%, 광석 1금/단위
        var s = new GameState(1, 1, new List<Faction>(), new List<City> { city }, new List<General>());

        var r = MarketSvc().BuyFromMarket(s, new CityId(1), MarketResource.Ore, 500);

        Assert.True(r.Ok, r.Error);
        Assert.Equal(500, r.State.Cities.Single().Ore);
        Assert.Equal(10_000 - 500, r.State.Cities.Single().Gold); // 500단위 × 1금
    }

    [Fact]
    public void 시장_군량은_비상용으로_금으로_메운다()
    {
        var city = Town(1, gold: 10_000) with { Provisions = 0 };
        var s = new GameState(1, 1, new List<Faction>(), new List<City> { city }, new List<General>());

        var r = MarketSvc().BuyFromMarket(s, new CityId(1), MarketResource.Grain, 1000);

        Assert.True(r.Ok, r.Error);
        Assert.Equal(1000, r.State.Cities.Single().Provisions);
        Assert.Equal(10_000 - 250, r.State.Cities.Single().Gold); // 25금/100량 × 1000 = 250
    }

    [Fact]
    public void 교역_태수면_매입가가_싸진다()
    {
        var svc = MarketSvc();
        var city = Town(1, gold: 100_000, ore: 0);
        var plain = new GameState(1, 1, new List<Faction>(), new List<City> { city }, new List<General>());
        var withGov = WithGovernor(city, GovWith(1, "trader", 3)); // 교역 T3 = −20%

        var basePrice = svc.MarketUnitPricePer100(plain, city, MarketResource.Ore);
        var govPrice = svc.MarketUnitPricePer100(withGov, withGov.Cities.Single(), MarketResource.Ore);
        Assert.Equal(basePrice * 80 / 100, govPrice);
    }

    [Fact]
    public void 시장_시세가_비싸면_같은_수량이_더_비싸다()
    {
        var svc = MarketSvc();
        var city = Town(1, gold: 100_000, ore: 0);
        var cheap = new GameState(1, 1, new List<Faction>(), new List<City> { city }, new List<General>())
            with { MarketPricePercent = 70 };  // 추수철
        var dear = cheap with { MarketPricePercent = 140 }; // 겨울

        Assert.True(svc.BuyFromMarket(cheap, new CityId(1), MarketResource.Horses, 100).State.Cities.Single().Gold
            > svc.BuyFromMarket(dear, new CityId(1), MarketResource.Horses, 100).State.Cities.Single().Gold);
    }

    [Fact]
    public void 시장_금이_부족하면_거부된다()
    {
        var city = Town(1, gold: 10, ore: 0);
        var s = new GameState(1, 1, new List<Faction>(), new List<City> { city }, new List<General>());

        var r = MarketSvc().BuyFromMarket(s, new CityId(1), MarketResource.Elephants, 10); // 3000금/마리
        Assert.False(r.Ok);
    }

    // ── 태수 임명(즉시) ──

    [Fact]
    public void 군사임명_도시에_군사가_지정되고_잠기지_않는다()
    {
        var s0 = State(new[] { Town(1) }, new[] { Pol(1, 90) });

        var r = Service().Issue(s0, new CommandRequest(new CityId(1), CommandKind.AppointStrategist, new GeneralId(1)));

        Assert.True(r.Ok, r.Error);
        Assert.Equal(new GeneralId(1), r.State.Cities.Single().Strategist);
        Assert.False(r.State.IsGeneralBusy(new GeneralId(1)));
        Assert.Empty(r.State.Commands);
    }

    [Fact]
    public void 태수임명_도시에_태수가_지정되고_잠기지_않는다()
    {
        var s0 = State(new[] { Town(1) }, new[] { Pol(1, 90) });

        var r = Service().Issue(s0, new CommandRequest(new CityId(1), CommandKind.AppointGovernor, new GeneralId(1)));

        Assert.True(r.Ok, r.Error);
        Assert.Equal(new GeneralId(1), r.State.Cities.Single().Governor);
        Assert.False(r.State.IsGeneralBusy(new GeneralId(1))); // 상주 역할 — 다른 명령과 병행 가능
        Assert.Empty(r.State.Commands);                        // 진행 명령을 만들지 않는다
    }

    [Fact]
    public void 태수임명_이미_그_도시_태수면_거부된다()
    {
        var s0 = State(new[] { Town(1) }, new[] { Pol(1, 90) });
        var once = Service().Issue(s0, new CommandRequest(new CityId(1), CommandKind.AppointGovernor, new GeneralId(1)));

        var again = Service().Issue(once.State, new CommandRequest(new CityId(1), CommandKind.AppointGovernor, new GeneralId(1)));

        Assert.False(again.Ok);
    }

    [Fact]
    public void 태수임명_다른_명령에_매인_장수도_태수로_지정된다()
    {
        var svc = Service();
        var s0 = State(new[] { Town(1) }, new[] { Pol(1, 90) });
        var busy = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.SetTaxRate, new GeneralId(1), Value: 30));
        Assert.True(busy.Ok);
        Assert.True(busy.State.IsGeneralBusy(new GeneralId(1)));

        var appoint = svc.Issue(busy.State, new CommandRequest(new CityId(1), CommandKind.AppointGovernor, new GeneralId(1)));

        Assert.True(appoint.Ok, appoint.Error);
        Assert.Equal(new GeneralId(1), appoint.State.Cities.Single().Governor);
    }

    // ── 발행 검증 ──

    [Fact]
    public void 발행_잠긴_장수는_다시_명령을_받지못한다()
    {
        var svc = Service();
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
        var svc = Service();
        var s0 = State(new[] { Town(1) }, new[] { Pol(1, 70), Pol(2, 71) });

        Assert.False(svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Build, new GeneralId(1), Facility: "paddy")).Ok);
        Assert.True(svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Build, new GeneralId(2), Facility: "paddy")).Ok);
    }

    [Fact]
    public void 발행_모집은_병종을_지정해야_한다()
    {
        var svc = Service();
        var s0 = State(new[] { Town(1) }, new[] { Pol(1, 90) });
        var r = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1)));
        Assert.False(r.Ok);
        Assert.Contains("병종", r.Error);
    }

    [Fact]
    public void 발행_모병은_광석과_인구를_즉시_예약한다()
    {
        // 정치 90 → 동원 90% × 인구 1%(=1000) = 900, 광석 5000 → min = 900 (도검병: 광석만)
        var svc = Service();
        var s0 = State(new[] { Town(1, ore: 5000, population: 100_000) }, new[] { Pol(1, 90) });

        var r = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1), TroopCode: "swordsman"));
        Assert.True(r.Ok);
        var city = r.State.Cities.Single();
        Assert.Equal(5000 - 900, city.Ore);
        Assert.Equal(100_000 - 900, city.Population);
        Assert.Empty(r.State.Garrisons); // 아직 지급 전(정산은 완료일)
    }

    [Fact]
    public void 발행_기병은_말이_하드캡이고_말을_예약한다()
    {
        // 말 100필 → 기병 최대 300명. 정치 90(동원 900)이어도 말 하드캡 300으로 잘린다.
        var svc = Service();
        var s0 = State(new[] { Town(1, ore: 5000, horses: 100) }, new[] { Pol(1, 90) });

        var r = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1), TroopCode: "cavalry"));
        Assert.True(r.Ok);
        var city = r.State.Cities.Single();
        Assert.Equal(0, city.Horses);        // 300명 = 말 100필 예약
        Assert.Equal(5000 - 300, city.Ore);
    }

    [Fact]
    public void 발행_말이_없으면_기병_모집_불가()
    {
        var svc = Service();
        var s0 = State(new[] { Town(1, horses: 0) }, new[] { Pol(1, 90) });
        var r = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1), TroopCode: "cavalry"));
        Assert.False(r.Ok);
    }

    // ── 전체 루프: 발행 → 7일 진행 → 정산 ──

    [Fact]
    public void 루프_모병_7일뒤_병종별_대기병력이_쌓이고_장수가_풀린다()
    {
        var svc = Service();
        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100), B);
        var s0 = State(new[] { Town(1, ore: 5000, population: 100_000) }, new[] { Pol(1, 90) });

        var issued = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1), TroopCode: "swordsman"));
        Assert.True(issued.State.IsGeneralBusy(new GeneralId(1)));

        var d7 = world.AdvanceDays(issued.State, 7);
        var g = GarrisonOf(d7, 1, "swordsman");
        Assert.Equal(900, g.Troops); // 정치 90 → 동원 90% × 인구 1%
        Assert.Equal(B.RecruitTrainLevel, g.TrainingLevel);
        Assert.False(d7.IsGeneralBusy(new GeneralId(1)));
        Assert.Empty(d7.Commands);
    }

    [Fact]
    public void 루프_징병은_훈련도0으로_쌓이고_치안이_내린다()
    {
        var svc = Service();
        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100), B);
        var city = Town(1, ore: 5000, population: 100_000) with { Security = 80 };
        var s0 = State(new[] { city }, new[] { Pol(1, 100) });

        var issued = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Conscript, new GeneralId(1), TroopCode: "swordsman"));
        var done = world.AdvanceDays(issued.State, 7);

        var g = GarrisonOf(done, 1, "swordsman");
        Assert.Equal(3000, g.Troops);        // 정치 100 → 동원 100% × 인구 3%(징병)
        Assert.Equal(0, g.TrainingLevel);
        Assert.Equal(80 - 3 * 5, done.Cities.Single().Security); // 3000명 → 치안 −15
    }

    [Fact]
    public void 루프_훈련은_지정_병종의_대기병력만_올린다()
    {
        var svc = Service();
        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0), B);
        var garrisons = new[]
        {
            new GarrisonForce(new CityId(1), "swordsman", 2000, 50),
            new GarrisonForce(new CityId(1), "archer", 2000, 50),
        };
        var s0 = State(new[] { Town(1) }, new[] { Mig(2, 80) }, garrisons);

        var issued = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Train, new GeneralId(2), TroopCode: "swordsman"));
        Assert.True(issued.Ok);
        var done = world.AdvanceDays(issued.State, 7);

        Assert.Equal(58, GarrisonOf(done, 1, "swordsman").TrainingLevel); // 무력 80/10 = +8
        Assert.Equal(50, GarrisonOf(done, 1, "archer").TrainingLevel);    // 다른 병종은 그대로
    }

    [Fact]
    public void 루프_같은_병종을_두번_모집하면_가중평균으로_합류한다()
    {
        var svc = Service();
        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0), B);
        var garrisons = new[] { new GarrisonForce(new CityId(1), "swordsman", 1000, 80) };
        var s0 = State(new[] { Town(1, ore: 5000) }, new[] { Pol(1, 90) }, garrisons);

        var issued = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1), TroopCode: "swordsman"));
        var done = world.AdvanceDays(issued.State, 7);

        var g = GarrisonOf(done, 1, "swordsman");
        Assert.Equal(1900, g.Troops);      // 기존 1000 + 신병 900(정치 90 동원)
        Assert.Equal(66, g.TrainingLevel); // (1000×80 + 900×50 + 950)/1900 = 66
    }

    [Fact]
    public void 취소하면_명령이_사라지고_장수가_풀린다()
    {
        var svc = Service();
        var s0 = State(new[] { Town(1) }, new[] { Pol(1, 90) });
        var issued = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1), TroopCode: "swordsman"));
        Assert.True(issued.State.IsGeneralBusy(new GeneralId(1)));

        var cancelled = CommandService.Cancel(issued.State, issued.State.Commands.Single());

        Assert.Empty(cancelled.Commands);
        Assert.False(cancelled.IsGeneralBusy(new GeneralId(1)));
    }

    [Fact]
    public void 진행이_시작된_명령은_취소되지_않는다()
    {
        var svc = Service();
        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0), B);
        var s0 = State(new[] { Town(1) }, new[] { Pol(1, 90) });
        var issued = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1), TroopCode: "swordsman"));
        var mid = world.AdvanceDays(issued.State, 3); // 진행 시작(발행일 ≠ 현재일)

        var after = CommandService.Cancel(mid, mid.Commands.Single());

        Assert.Single(after.Commands); // 취소 무시 — 완료까지 간다
        Assert.True(after.IsGeneralBusy(new GeneralId(1)));
    }

    [Fact]
    public void 취소해도_예약_자원은_환불되지_않는다()
    {
        var svc = Service();
        var s0 = State(new[] { Town(1, ore: 5000, population: 100_000) }, new[] { Pol(1, 100) });
        var issued = svc.Issue(s0, new CommandRequest(new CityId(1), CommandKind.Recruit, new GeneralId(1), TroopCode: "swordsman"));
        var reservedOre = issued.State.Cities.Single().Ore;
        var reservedPop = issued.State.Cities.Single().Population;
        Assert.True(reservedOre < 5000); // 발행 시 예약 차감 확인

        var cancelled = CommandService.Cancel(issued.State, issued.State.Commands.Single());

        Assert.Equal(reservedOre, cancelled.Cities.Single().Ore);      // 환불 없음
        Assert.Equal(reservedPop, cancelled.Cities.Single().Population);
    }
}
