using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

namespace SanguoSLG.Core.Tests.Simulation;

/// <summary>일 단위 세계 시계 + 월말 세금 틱(도시 금고) — design-administration "시간 축".</summary>
public class WorldEngineTests
{
    private static readonly BalanceConfig Balance = new(MonthlyTaxPerCity: 100);

    // 정치 60 담당관(세율 증폭 0 = 유효 담당관의 기준선). 수입 검산은 이 담당관을 붙여 단순화한다.
    private static readonly General Gov = new(new GeneralId(99), "태수",
        new Dictionary<TroopClass, AptitudeGrade>(), Might: 50, Intellect: 50, Politics: 60);

    // 모든 도시에 정치 60 담당관을 배속한 상태(수입 = 담당관 없음 페널티 없이 기준선).
    private static GameState Governed(IEnumerable<City> cities, IEnumerable<Faction>? factions = null) =>
        new(1, 1, (factions ?? new List<Faction>()).ToList(),
            cities.Select(c => c with { Governor = new GeneralId(99) }).ToList(),
            new List<General> { Gov });

    private static GameState InitialState()
    {
        var factions = new List<Faction>
        {
            new(new FactionId(1), "위", new GeneralId(1), Gold: 1000, Color: "#2d5fd0"),
            new(new FactionId(2), "촉", new GeneralId(2), Gold: 800, Color: "#2c8c46"),
        };
        // 인구를 소성 최대치(10만)로 채워 인구 충원율 배율 = 100%(수입 검산을 단순화).
        var cities = new List<City>
        {
            new(new CityId(1), "허창", new HexCoord(0, 0), new FactionId(1), 5000, Gold: 500, Population: 100_000),
            new(new CityId(2), "업", new HexCoord(1, -1), new FactionId(1), 4200, Gold: 300, Population: 100_000),
            new(new CityId(3), "성도", new HexCoord(5, 2), new FactionId(2), 6000, Gold: 400, Population: 100_000),
        };
        return Governed(cities, factions);
    }

    private static int CityGold(GameState s, int cityId) =>
        s.Cities.Single(c => c.Id == new CityId(cityId)).Gold;

    [Fact]
    public void 달력_1일은_1년1월1일이고_360일이_지나면_2년이다()
    {
        var s = InitialState();
        Assert.Equal((1, 1, 1), (s.Year, s.Month, s.DayOfMonth));

        var engine = new WorldEngine(Balance);
        var d30 = engine.AdvanceDays(s, 29);
        Assert.Equal((1, 1, 30), (d30.Year, d30.Month, d30.DayOfMonth));

        var y2 = engine.AdvanceDays(s, 360);
        Assert.Equal((2, 1, 1), (y2.Year, y2.Month, y2.DayOfMonth));
    }

    [Fact]
    public void 월말_30일에_도시_금고로_세금이_들어온다()
    {
        var engine = new WorldEngine(Balance);

        var d29 = engine.AdvanceDays(InitialState(), 28); // 1월 29일
        Assert.Equal(500, CityGold(d29, 1));              // 아직 없음

        var d30 = engine.AdvanceDays(InitialState(), 29); // 1월 30일 — 월말 틱
        Assert.Equal(600, CityGold(d30, 1));
        Assert.Equal(400, CityGold(d30, 2));
        Assert.Equal(500, CityGold(d30, 3));
    }

    [Fact]
    public void 열두달을_돌리면_도시_세금이_12번_쌓인다()
    {
        var end = new WorldEngine(Balance).AdvanceDays(InitialState(), 360);
        Assert.Equal(500 + 12 * 100, CityGold(end, 1));
    }

    [Fact]
    public void 월말에_인구가_치안_비례로_성장한다()
    {
        // 인구 100,000·치안 100 → +1% = +1,000. 치안 50 → +0.5% = +500.
        var cities = new List<City>
        {
            new(new CityId(1), "허창", new HexCoord(0, 0), new FactionId(1), 5000, CastleSize.Medium, Population: 100_000),
            new(new CityId(2), "업", new HexCoord(1, -1), new FactionId(1), 4200, CastleSize.Medium, Population: 100_000, Security: 50),
        };
        var s = new GameState(1, 1, new List<Faction>(), cities, new List<General>());

        var after = new WorldEngine(Balance).AdvanceDays(s, 30);

        Assert.Equal(101_000, after.Cities.Single(c => c.Id.Value == 1).Population);
        Assert.Equal(100_500, after.Cities.Single(c => c.Id.Value == 2).Population);
    }

    [Fact]
    public void 인구는_성곽_등급별_최대치를_넘지_않는다()
    {
        // 소성 최대 100,000 직전에서 성장해도 최대치에서 멈춘다. 대성은 500,000까지 여유.
        var cities = new List<City>
        {
            new(new CityId(1), "소성", new HexCoord(0, 0), new FactionId(1), 0, CastleSize.Small, Population: 99_900),
            new(new CityId(2), "대성", new HexCoord(1, 0), new FactionId(1), 0, CastleSize.Large, Population: 99_900),
        };
        var s = new GameState(1, 1, new List<Faction>(), cities, new List<General>());

        var after = new WorldEngine(Balance).AdvanceDays(s, 30);

        Assert.Equal(100_000, after.Cities.Single(c => c.Id.Value == 1).Population);
        Assert.Equal(100_899, after.Cities.Single(c => c.Id.Value == 2).Population);
    }

    [Fact]
    public void 수입은_성규모_기본치에_시설_가산이_붙는다()
    {
        // 대성(금 300·군량 2000) + 마을 2(금 +100) + 논 2(군량 +600) + 밭 1(군량 +150). 인구 만충(배율 100%)
        var cities = new List<City>
        {
            new(new CityId(1), "허창", new HexCoord(0, 0), new FactionId(1), 1000, CastleSize.Large,
                Gold: 0, Population: 500_000, Paddies: 2, Farms: 1, Villages: 2),
        };
        var s = Governed(cities);

        var after = new WorldEngine(Balance).AdvanceDays(s, 30);
        var city = after.Cities.Single();

        Assert.Equal(300 + 100, city.Gold);
        Assert.Equal(1000 + 2000 + 600 + 150, city.Provisions);
    }

    [Theory]
    [InlineData(1, 0, 0, 0, 300)]
    [InlineData(0, 1, 0, 0, 150)]
    [InlineData(0, 0, 1, 50, 0)]
    public void 논밭마을은_정의된_시설효과를_각각_월말수입에_더한다(
        int paddies,
        int farms,
        int villages,
        int expectedGoldBonus,
        int expectedProvisionsBonus)
    {
        var cities = new List<City>
        {
            new(new CityId(1), "시설검산", new HexCoord(0, 0), new FactionId(1), 1000, CastleSize.Small,
                Gold: 0, Population: 100_000, Paddies: paddies, Farms: farms, Villages: villages),
        };
        var s = Governed(cities);

        var after = new WorldEngine(Balance).AdvanceDays(s, 30).Cities.Single();

        Assert.Equal(Balance.GoldBaseSmall + expectedGoldBonus, after.Gold);
        Assert.Equal(1000 + Balance.ProvisionsBaseSmall + expectedProvisionsBonus, after.Provisions);
    }

    [Theory]
    [InlineData("paddy", 2, 0, 600)]
    [InlineData("paddy", 5, 0, 1500)]
    [InlineData("farm", 2, 0, 300)]
    [InlineData("farm", 5, 0, 750)]
    [InlineData("village", 2, 100, 0)]
    [InlineData("village", 5, 250, 0)]
    public void 업그레이드된_시설은_체력단계에_따라_월수입도_증가한다(
        string facility,
        int levelMultiplier,
        int expectedGoldBonus,
        int expectedProvisionsBonus)
    {
        var hitPoints = levelMultiplier == 5 ? FacilityHealth.Level3 : FacilityHealth.Level2;
        var city = new City(new CityId(1), "시설검산", new HexCoord(0, 0), new FactionId(1), 1000, CastleSize.Small,
            Gold: 0, Population: 100_000,
            Paddies: facility == "paddy" ? 1 : 0,
            Farms: facility == "farm" ? 1 : 0,
            Villages: facility == "village" ? 1 : 0);
        var s = Governed(new[] { city }) with
        {
            FacilityPlacements = new[] { new FacilityPlacement(city.Id, new HexCoord(1, 0), facility, hitPoints) },
        };

        var after = new WorldEngine(Balance).AdvanceDays(s, 30).Cities.Single();

        Assert.Equal(Balance.GoldBaseSmall + expectedGoldBonus, after.Gold);
        Assert.Equal(1000 + Balance.ProvisionsBaseSmall + expectedProvisionsBonus, after.Provisions);
    }

    [Theory]
    [InlineData("paddy", 0, 600)]
    [InlineData("farm", 0, 300)]
    [InlineData("village", 100, 0)]
    public void 업그레이드된_시설은_배치순서와_상관없이_월수입에_반영된다(
        string facility,
        int expectedGoldBonus,
        int expectedProvisionsBonus)
    {
        var city = new City(new CityId(1), "시설검산", new HexCoord(0, 0), new FactionId(1), 1000, CastleSize.Small,
            Gold: 0, Population: 100_000,
            Paddies: facility == "paddy" ? 1 : 0,
            Farms: facility == "farm" ? 1 : 0,
            Villages: facility == "village" ? 1 : 0);
        var s = Governed(new[] { city }) with
        {
            FacilityPlacements = new[]
            {
                new FacilityPlacement(city.Id, new HexCoord(1, 0), facility, FacilityHealth.Level1),
                new FacilityPlacement(city.Id, new HexCoord(2, 0), facility, FacilityHealth.Level2),
            },
        };

        var after = new WorldEngine(Balance).AdvanceDays(s, 30).Cities.Single();

        Assert.Equal(Balance.GoldBaseSmall + expectedGoldBonus, after.Gold);
        Assert.Equal(1000 + Balance.ProvisionsBaseSmall + expectedProvisionsBonus, after.Provisions);
    }

    [Fact]
    public void 자원은_산출_도시에서만_매월_는다()
    {
        var cities = new List<City>
        {
            new(new CityId(1), "산출", new HexCoord(0, 0), new FactionId(1), 0,
                Ore: 100, Horses: 10, Elephants: 1,
                ProducesOre: true, ProducesHorses: true, ProducesElephants: true),
            new(new CityId(2), "무산출", new HexCoord(1, 0), new FactionId(1), 0,
                Ore: 100, Horses: 10, Elephants: 1),
        };
        var s = new GameState(1, 1, new List<Faction>(), cities, new List<General>());

        var after = new WorldEngine(Balance).AdvanceDays(s, 30);

        var yes = after.Cities.Single(c => c.Id.Value == 1);
        Assert.Equal((600, 110, 3), (yes.Ore, yes.Horses, yes.Elephants));

        var no = after.Cities.Single(c => c.Id.Value == 2);
        Assert.Equal((100, 10, 1), (no.Ore, no.Horses, no.Elephants));
    }

    [Fact]
    public void 세율이_수입_배율과_치안_변동을_정한다()
    {
        // 인구 만충(소성 10만 → 배율 100%). 세율 배율 + 치안(자연 회복 +2 + 세율 효과):
        // 기준 20% = 1.0배·치안 +2 / 50% = 2.5배·−8(=+2−10) / 10% = 0.5배·+4(=+2+2) / 0% = 0배·+6(=+2+4)
        var cities = new List<City>
        {
            new(new CityId(1), "기준", new HexCoord(0, 0), new FactionId(1), 0, Gold: 0, Population: 100_000, Security: 80),
            new(new CityId(2), "가혹", new HexCoord(1, 0), new FactionId(1), 0, Gold: 0, Population: 100_000, Security: 80, TaxRate: 50),
            new(new CityId(3), "선정", new HexCoord(2, 0), new FactionId(1), 0, Gold: 0, Population: 100_000, Security: 80, TaxRate: 10),
            new(new CityId(4), "면세", new HexCoord(3, 0), new FactionId(1), 0, Gold: 0, Population: 100_000, Security: 80, TaxRate: 0),
        };
        var s = Governed(cities);

        var after = new WorldEngine(Balance).AdvanceDays(s, 30);
        City C(int id) => after.Cities.Single(c => c.Id.Value == id);

        Assert.Equal((100, 82), (C(1).Gold, C(1).Security));   // 소성 기본 100 × 1.0, +2
        Assert.Equal((250, 72), (C(2).Gold, C(2).Security));   // × 2.5, +2−10
        Assert.Equal((50, 84), (C(3).Gold, C(3).Security));    // × 0.5, +2+2
        Assert.Equal((0, 86), (C(4).Gold, C(4).Security));     // × 0, +2+4
    }

    [Fact]
    public void 수입은_인구_충원율에_비례한다()
    {
        // 소성 기본 금 100. 인구 만충(10만)=100%, 반충(5만)=75%, 텅빔(0)=바닥 50%.
        var cities = new List<City>
        {
            new(new CityId(1), "만충", new HexCoord(0, 0), new FactionId(1), 0, Gold: 0, Population: 100_000),
            new(new CityId(2), "반충", new HexCoord(1, 0), new FactionId(1), 0, Gold: 0, Population: 50_000),
            new(new CityId(3), "텅빔", new HexCoord(2, 0), new FactionId(1), 0, Gold: 0, Population: 0),
        };
        var s = Governed(cities);

        var after = new WorldEngine(Balance).AdvanceDays(s, 30);
        Assert.Equal(100, after.Cities.Single(c => c.Id.Value == 1).Gold); // 100%
        Assert.Equal(75, after.Cities.Single(c => c.Id.Value == 2).Gold);  // 75%
        Assert.Equal(50, after.Cities.Single(c => c.Id.Value == 3).Gold);  // 바닥 50%
    }

    [Fact]
    public void 저치안이면_수입이_감액된다()
    {
        // 치안 19(<20 임계) → 수입 ×0.7. 인구 만충·세율 20%. 소성 금 100 × 0.7 = 70.
        var cities = new List<City>
        {
            new(new CityId(1), "혼란", new HexCoord(0, 0), new FactionId(1), 0, Gold: 0, Population: 100_000, Security: 19),
        };
        var s = Governed(cities);

        var after = new WorldEngine(Balance).AdvanceDays(s, 30);
        Assert.Equal(70, after.Cities.Single().Gold);
    }

    [Fact]
    public void 나눠_진행해도_한번에_진행한_것과_같다()
    {
        var engine = new WorldEngine(Balance);
        var whole = engine.AdvanceDays(InitialState(), 90);

        var split = InitialState();
        for (var i = 0; i < 30; i++)
        {
            split = engine.AdvanceDays(split, 3);
        }

        Assert.Equal(whole.Day, split.Day);
        Assert.Equal(whole.Cities, split.Cities);
    }

    [Fact]
    public void 입력_순서가_달라도_결과와_저장순서는_동일하다()
    {
        var engine = new WorldEngine(Balance);
        var normal = InitialState();
        var reversed = normal with
        {
            Factions = normal.Factions.Reverse().ToList(),
            Cities = normal.Cities.Reverse().ToList(),
        };

        var a = engine.AdvanceDays(normal, 30);
        var b = engine.AdvanceDays(reversed, 30);

        Assert.Equal(a.Cities, b.Cities);
        Assert.Equal(new[] { 1, 2, 3 }, a.Cities.Select(c => c.Id.Value));
    }

    // ── v2 자동 담당자 — 전투 중심 리디자인 Phase 2 ──

    private static readonly BalanceConfig V2OnlyBalance = new(
        MonthlyTaxPerCity: 0,
        PopulationGrowthPercent: 0,
        GoldBaseSmall: 0,
        GoldBaseMedium: 0,
        GoldBaseLarge: 0,
        ProvisionsBaseSmall: 0,
        ProvisionsBaseMedium: 0,
        ProvisionsBaseLarge: 0,
        PaddyProvisions: 0,
        FarmProvisions: 0,
        VillageGold: 0,
        OreOutputPerMonth: 0,
        HorsesOutputPerMonth: 0,
        ElephantsOutputPerMonth: 0,
        SecurityNaturalRecovery: 0,
        GeneralSalaryPerMonth: 0);

    private static General V2Officer(int id, int might = 50, int politics = 50) => new(
        new GeneralId(id), $"담당{id}", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: might, Intellect: 50, Politics: politics);

    [Fact]
    public void v2_담당자가_월말에_치안_내정_병력_훈련을_자동_처리한다()
    {
        var city = new City(new CityId(1), "자동성", new HexCoord(0, 0), new FactionId(1), 1000,
            Gold: 0, Population: 0, Security: 50,
            SecurityOfficer: new GeneralId(1),
            DomesticOfficer: new GeneralId(2),
            RecruitmentOfficer: new GeneralId(3),
            TrainingOfficer: new GeneralId(4));
        var generals = new[]
        {
            V2Officer(1, might: 85),      // 치안 +2
            V2Officer(2, politics: 80),   // 금 +260, 군량 +700
            V2Officer(3, might: 70),      // 훈련도 50 병력 +550
            V2Officer(4, might: 100),     // 훈련 +4
        };
        var state = new GameState(1, 1, new List<Faction>(), new List<City> { city }, generals.ToList(),
            Postings: generals.Select(g => new GeneralPosting(g.Id, city.Owner, city.Id)).ToList(),
            GarrisonForces: new List<GarrisonForce> { new(city.Id, "swordsman", 1000, 40) });

        var after = new WorldEngine(V2OnlyBalance, new CommandBalance { AutoOfficerSystemEnabled = true })
            .AdvanceDays(state, 30);
        var resultCity = after.Cities.Single();
        var garrison = after.Garrisons.Single(g => g.City == city.Id && g.TroopCode == "swordsman");

        Assert.Equal(52, resultCity.Security);
        Assert.Equal(260, resultCity.Gold);
        Assert.Equal(1700, resultCity.Provisions);
        Assert.Equal(1550, garrison.Troops);
        Assert.Equal(48, garrison.TrainingLevel);
    }

    [Fact]
    public void v2_담당자가_없으면_치안만_떨어지고_나머지는_현상_유지된다()
    {
        var city = new City(new CityId(1), "공석성", new HexCoord(0, 0), new FactionId(1), 1000,
            Gold: 0, Population: 0, Security: 50);
        var state = new GameState(1, 1, new List<Faction>(), new List<City> { city }, new List<General>(),
            GarrisonForces: new List<GarrisonForce> { new(city.Id, "swordsman", 1000, 40) });

        var after = new WorldEngine(V2OnlyBalance, new CommandBalance { AutoOfficerSystemEnabled = true })
            .AdvanceDays(state, 30);
        var resultCity = after.Cities.Single();
        var garrison = after.Garrisons.Single();

        Assert.Equal(48, resultCity.Security);
        Assert.Equal(0, resultCity.Gold);
        Assert.Equal(1000, resultCity.Provisions);
        Assert.Equal(1000, garrison.Troops);
        Assert.Equal(40, garrison.TrainingLevel);
    }

    // ── 내정담당관(태수) — design-administration "내정 심화" A/담당관 ──

    private static GameState WithGovernor(City city, General? governor, IEnumerable<AdminSkill>? admin = null)
    {
        var generals = governor is null ? new List<General>() : new List<General> { governor };
        var placed = governor is null ? city : city with { Governor = governor.Id };
        return new GameState(1, 1, new List<Faction>(), new List<City> { placed }, generals);
    }

    private static General Officer(int politics, IReadOnlyList<GeneralSkill>? admin = null) => new(
        new GeneralId(50), "관리", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: 50, Intellect: 50, Politics: politics, AdminPassives: admin);

    private static City Base() =>
        new(new CityId(1), "성", new HexCoord(0, 0), new FactionId(1), 0, Gold: 0, Population: 100_000);

    [Fact]
    public void 담당관이_없으면_수입이_급감한다()
    {
        // 소성 기본 금 100, 세율 20%, 인구 만충 → 담당관 없으면 ×0.3 = 30.
        var after = new WorldEngine(Balance).AdvanceDays(WithGovernor(Base(), governor: null), 30);
        Assert.Equal(30, after.Cities.Single().Gold);
    }

    [Fact]
    public void 담당관_정치가_최소치미만이면_급감한다()
    {
        // 정치 59(<60) → 유효 담당관 아님 → ×0.3 = 30. 정치 60이면 정상 100.
        var below = new WorldEngine(Balance).AdvanceDays(WithGovernor(Base(), Officer(59)), 30);
        Assert.Equal(30, below.Cities.Single().Gold);

        var ok = new WorldEngine(Balance).AdvanceDays(WithGovernor(Base(), Officer(60)), 30);
        Assert.Equal(100, ok.Cities.Single().Gold);
    }

    [Fact]
    public void 담당관_정치100은_세율을_2배로_증폭한다()
    {
        // 세율 10%. 정치 60(증폭 0) → 100×10/20 = 50. 정치 100(증폭 +100%) → 실효 20% → 100.
        var city = Base() with { TaxRate = 10 };

        var plain = new WorldEngine(Balance).AdvanceDays(WithGovernor(city, Officer(60)), 30);
        Assert.Equal(50, plain.Cities.Single().Gold);

        var master = new WorldEngine(Balance).AdvanceDays(WithGovernor(city, Officer(100)), 30);
        Assert.Equal(100, master.Cities.Single().Gold); // 10% 세율이 20%처럼
    }

    [Fact]
    public void 담당관_상재스킬이_금수입을_올린다()
    {
        // 상재(tax 버킷) 티어2 = +12% 금. 정치 60·세율 20%·인구 만충 → 100 × 1.12 = 112.
        var merchant = new AdminSkill("merchant", "상재", Bucket: "tax", Tiers: new[] { 6, 12, 20 });
        var officer = Officer(60, new List<GeneralSkill> { new("merchant", 2) });
        var engine = new WorldEngine(Balance, adminSkills: new[] { merchant });

        var after = engine.AdvanceDays(WithGovernor(Base(), officer), 30);
        Assert.Equal(112, after.Cities.Single().Gold);
    }

    [Fact]
    public void 담당관_채광스킬이_광석산출을_올린다_비산출도시엔무효()
    {
        // 채광(ore_output) 티어2 = +20%. 기본 산출(BalanceConfig 기본 500) × 1.2 = 600. 비산출 도시는 0.
        var miner = new AdminSkill("miner", "채광", Bucket: "ore_output", Tiers: new[] { 10, 20, 30 });
        var officer = Officer(60, new List<GeneralSkill> { new("miner", 2) });
        var engine = new WorldEngine(Balance, adminSkills: new[] { miner });

        var producing = Base() with { ProducesOre = true, Ore = 0 };
        var barren = Base() with { ProducesOre = false, Ore = 0 };

        var a = engine.AdvanceDays(WithGovernor(producing, officer), 30);
        Assert.Equal(600, a.Cities.Single().Ore); // 500 × 1.2

        var b = engine.AdvanceDays(WithGovernor(barren, officer), 30);
        Assert.Equal(0, b.Cities.Single().Ore);   // 안 나는 도시엔 무효
    }

    [Fact]
    public void 담당관이_출전중이면_유령태수가_되지않는다()
    {
        // 배속이 있는 세계에서 담당관의 주둔지가 그 도시가 아니면(출전 = Location null)
        // 담당관 없음으로 취급 → 수입 급감(×0.3).
        var officer = Officer(90);
        var city = Base() with { Governor = officer.Id };

        GameState With(CityId? location) => new(1, 1, new List<Faction>(),
            new List<City> { city }, new List<General> { officer },
            Postings: new List<GeneralPosting> { new(officer.Id, new FactionId(1), location) });

        var home = new WorldEngine(Balance).AdvanceDays(With(city.Id), 30);
        Assert.True(home.Cities.Single().Gold > 100); // 주둔 중 — 정치 90 증폭

        var away = new WorldEngine(Balance).AdvanceDays(With(null), 30); // 출전 중
        Assert.Equal(30, away.Cities.Single().Gold);  // ×0.3 급감
    }
}
