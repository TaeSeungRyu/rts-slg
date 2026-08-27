namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>캠페인 진행 = 7일 고정, 이동+전투 자동 연속, 내정과 한 시계(2026-08-16 확정).</summary>
public class CampaignEngineTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static CampaignEngine Engine()
    {
        var movement = new MovementSimulator(new PassabilityMap(new HexMap(0, 30, -5, 8), [], []));
        var field = new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), 70));
        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100));
        return new CampaignEngine(field, world);
    }

    private static CombatUnit Army(int id, int owner, HexCoord pos, UnitMode mode, HexCoord? target,
        int troops = 10000, string code = "swordsman", int training = 50)
    {
        var t = T[code];
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos,
            t.MovementPerDay, t.Detection, t.RangeUnit, MovementDomain.Land, mode, target, id, t.RangeCastle);
        var stats = CombatStatsBuilder.BuildField(t, AptitudeGrade.A, 0, TerrainType.River, troops);
        return new CombatUnit(field, stats, new TroopPool(troops, 0), UnitCombatState.Create(60),
            60, 60, troops, t.Class, TroopCode: code, Training: training);
    }

    private static GameState World(params CombatUnit[] armies) =>
        new(1, 1, new List<Faction>(), new List<City>(), new List<General>(), FieldArmies: armies.ToList());

    [Fact]
    public void 공사장_인접부대는_소유무시하고_피해를_주고_체력이_다까이면_건설취소된다()
    {
        // 공사 중 시설은 병력 1000짜리 무방비 목표(체력 1000, 진행당 인접 부대 하나에 500 피해).
        // 아군·적군 가리지 않고 인접하면 피해 — 여기선 같은 세력 부대로 소유 무시를 검증한다.
        var movement = new MovementSimulator(new PassabilityMap(new HexMap(0, 30, -5, 8), [], []));
        var field = new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), 70));
        var engine = new CampaignEngine(field, new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100)),
            buildSiteHp: 1000, buildSiteDamagePerTurn: 500);

        var plot = new HexCoord(10, 0);
        var city = new City(new CityId(1), "성", new HexCoord(9, 0), new FactionId(1), Provisions: 1000);
        var cmd = new CityCommand(new CityId(1), CommandKind.Build, new GeneralId(1), null,
            StartDay: 1, CompletionDay: 100, Amount: 0, Facility: "paddy", Plot: plot);
        var ally = Army(1, 1, new HexCoord(10, 1), UnitMode.March, target: null); // 공사 타일에 인접(같은 세력)
        var s0 = new GameState(1, 1,
            new List<Faction> { new(new FactionId(1), "위", new GeneralId(1), 1000, "#0af") },
            new List<City> { city }, new List<General>(),
            PendingCommands: new List<CityCommand> { cmd },
            FieldArmies: new[] { ally });

        var after1 = engine.AdvanceWeek(s0, out _);
        var c1 = after1.Commands.SingleOrDefault(x => x.Kind == CommandKind.Build);
        Assert.NotNull(c1);
        Assert.Equal(500, c1!.SiteDamage); // 정지 부대 → 한 주 한 턴, 500 피해 누적

        var after2 = engine.AdvanceWeek(after1, out _);
        Assert.DoesNotContain(after2.Commands, x => x.Kind == CommandKind.Build); // 1000 도달 → 건설 취소
    }

    [Fact]
    public void 성보급_반경내_아군_야전부대는_성비축에서_군량을_채워_굶지않는다()
    {
        // 성문 앞 대기 부대가 자기 성 옆에서 아사하던 문제. 성 반경 안 아군 부대는 매 진행
        // 성 비축에서 군량을 채운다(성 비축은 그만큼 줄어든다).
        var movement = new MovementSimulator(new PassabilityMap(new HexMap(0, 30, -5, 8), [], []));
        var field = new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), 70));
        var engine = new CampaignEngine(field, new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100)),
            cityResupplyRadius: 3);

        var city = new City(new CityId(1), "성", new HexCoord(5, 5), new FactionId(1), Provisions: 5000);
        var unit = Army(1, 1, new HexCoord(6, 5), UnitMode.March, target: null) with { Provisions = 10 };
        var state = new GameState(1, 1, new List<Faction>(), new List<City> { city },
            new List<General>(), FieldArmies: new List<CombatUnit> { unit });

        var after = engine.AdvanceWeek(state, out _);

        Assert.True(after.Armies.Single().Provisions > 10, "성 옆 부대 군량이 채워진다");
        Assert.Equal(10000, after.Armies.Single().Pool.Active); // 굶어 이탈 없음
        Assert.True(after.Cities.Single().Provisions < 5000, "성 비축이 보급분만큼 줄어든다");
    }

    [Fact]
    public void 진행_1번은_야전이_있든_없든_정확히_7일이다()
    {
        var e = Engine();

        var idle = e.AdvanceWeek(World(), out _);
        Assert.Equal(8, idle.Day);

        // 접적해서 시뮬이 일찍 멈춰도 내정은 7일.
        var a = Army(1, 1, new HexCoord(0, 0), UnitMode.Attack, new HexCoord(10, 0));
        var b = Army(2, 2, new HexCoord(6, 0), UnitMode.Attack, new HexCoord(0, 0));
        var fight = e.AdvanceWeek(World(a, b), out _);
        Assert.Equal(8, fight.Day);
    }

    [Fact]
    public void 접적해도_7일이_찰때까지_전투가_자동으로_계속된다()
    {
        // 거리 6, 서로 공격 — 첫 시뮬이 접적으로 며칠 만에 멈추지만, 그 주 안에서 교전이
        // 자동 반복돼 한 번의 진행으로 여러 교환이 정산된다(피해가 1교환분 760보다 크다).
        var a = Army(1, 1, new HexCoord(0, 0), UnitMode.Attack, new HexCoord(10, 0));
        var b = Army(2, 2, new HexCoord(6, 0), UnitMode.Attack, new HexCoord(0, 0));

        var after = Engine().AdvanceWeek(World(a, b), out var turns);

        Assert.True(turns.Count > 1, "한 주 안에서 시뮬이 여러 번 이어진다");
        var u1 = after.Armies.Single(u => u.Id.Value == 1);
        Assert.True(10000 - u1.Pool.Active > 760, $"여러 교환 누적 피해: {10000 - u1.Pool.Active}");
    }

    [Fact]
    public void 진행을_4번_누르면_한달이_지나_월말_정산이_된다()
    {
        var cities = new List<City>
        {
            new(new CityId(1), "성", new HexCoord(20, 5), new FactionId(1), 0, Gold: 0, Population: 100_000),
        };
        var s = new GameState(1, 1, new List<Faction>(), cities, new List<General>());

        var e = Engine();
        for (var i = 0; i < 4; i++)
        {
            s = e.AdvanceWeek(s, out _);
        }

        Assert.Equal(29, s.Day);                      // 4주 = 28일 경과
        // 30일차(월말)는 아직 안 왔지만 28일까지의 수입은 0회 — 5번째 진행에서 월말이 낀다.
        var s5 = e.AdvanceWeek(s, out _);
        Assert.Equal(36, s5.Day);
        Assert.True(s5.Cities.Single().Gold > 0, "5번째 주에 월말(30일) 정산이 포함된다");
    }

    [Fact]
    public void 입성한_부대는_병종별_대기병력으로_편입된다()
    {
        // 아군 성으로 복귀하는 부대 — 입성하면 GarrisonForce(도시·병종·병력·훈련도)로.
        var home = new City(new CityId(1), "성", new HexCoord(5, 0), new FactionId(1), 0);
        var returning = Army(1, 1, new HexCoord(2, 0), UnitMode.March, new HexCoord(5, 0), troops: 8000, training: 70);
        var s = new GameState(1, 1, new List<Faction>(), new List<City> { home }, new List<General>(),
            FieldArmies: new List<CombatUnit> { returning });

        var after = Engine().AdvanceWeek(s, out _);

        Assert.Empty(after.Armies);
        var g = after.Garrisons.Single();
        Assert.Equal(("swordsman", 8000, 70), (g.TroopCode, g.Troops, g.TrainingLevel));
    }
}
