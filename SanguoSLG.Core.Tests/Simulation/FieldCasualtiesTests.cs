namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>야전 전멸 시 장수 처리 — 포로 없이 원 성 우선, 불가능하면 최근접 아군 성 귀환.</summary>
public class FieldCasualtiesTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static General Gen(int id) => new(
        new GeneralId(id), $"g{id}",
        new Dictionary<TroopClass, AptitudeGrade> { [TroopClass.Infantry] = AptitudeGrade.A },
        Might: 70, Intellect: 60, Politics: 80);

    private static CombatUnit DeadUnit(int id, int owner, HexCoord pos, int? vanguard = 1, int? adjutant = null)
    {
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos, 2, 2, 1,
            MovementDomain.Land, UnitMode.Attack, Target: null, id);
        var stats = CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 0, TerrainType.Plains, 0);
        return new CombatUnit(field, stats, new TroopPool(0, 0), UnitCombatState.Create(60),
            60, 60, 10000, TroopClass.Infantry,
            VanguardId: vanguard is { } v ? new GeneralId(v) : null,
            AdjutantId: adjutant is { } a ? new GeneralId(a) : null);
    }

    private static GameState State(params City[] cities) => new(1, 1,
        new List<Faction>(), cities.ToList(),
        new List<General> { Gen(1), Gen(2) },
        Postings: new List<GeneralPosting>
        {
            new(new GeneralId(1), new FactionId(1), Location: null),
            new(new GeneralId(2), new FactionId(1), Location: null),
        });

    private static City Town(int id, int owner, HexCoord pos) =>
        new(new CityId(id), $"c{id}", pos, new FactionId(owner), 0);

    [Fact]
    public void 교전전멸해도_선봉과_부관은_포로없이_원성으로_귀환한다()
    {
        var s0 = State(Town(1, 1, new HexCoord(0, 0)), Town(2, 1, new HexCoord(6, 0)));
        var dead = DeadUnit(9, owner: 1, new HexCoord(5, 0), vanguard: 1, adjutant: 2);
        var reports = new List<CasualtyReport>();

        dead = dead with { OriginCity = new CityId(1) };
        var s1 = FieldCasualties.ResolveUnit(s0, dead, new HexCoord(5, 0), reports);

        Assert.Equal(2, reports.Count);
        // 전멸 지점에는 2번 성이 더 가깝지만 편성 원점 1번 성을 우선한다.
        Assert.All(reports, r => Assert.Equal(new CityId(1), r.Refuge));
        Assert.All(new[] { new GeneralId(1), new GeneralId(2) }, id =>
            Assert.Equal(new CityId(1), s1.PostingOf(id)!.Location));
        Assert.Equal(2, s1.Assignments.Select(p => p.General).Distinct().Count());
    }

    [Fact]
    public void 원성이_적에게_점령됐으면_최근접_아군도시로_귀환한다()
    {
        var s0 = State(Town(1, 1, new HexCoord(0, 0)), Town(2, 1, new HexCoord(9, 0)));
        var dead = DeadUnit(9, owner: 1, new HexCoord(7, 0), vanguard: 1);
        var reports = new List<CasualtyReport>();

        dead = dead with { OriginCity = new CityId(99) };
        var s1 = FieldCasualties.ResolveUnit(s0, dead, new HexCoord(7, 0), reports);

        Assert.Equal(new CityId(2), reports.Single().Refuge); // (9,0)이 (7,0)에서 최근접
        Assert.Equal(new CityId(2), s1.PostingOf(new GeneralId(1))!.Location);
    }

    [Fact]
    public void 기존배속이_없어도_전멸장수는_아군도시로_복구된다()
    {
        var s0 = State(Town(1, 1, new HexCoord(0, 0)));
        var dead = DeadUnit(9, owner: 1, new HexCoord(5, 0), vanguard: 1);
        var reports = new List<CasualtyReport>();

        var withoutPosting = s0 with { Postings = [] };
        var s1 = FieldCasualties.ResolveUnit(withoutPosting, dead, new HexCoord(5, 0), reports);

        Assert.Equal(new CityId(1), s1.PostingOf(new GeneralId(1))!.Location);
    }

    [Fact]
    public void 보유도시가_없으면_재야가된다()
    {
        var s0 = State(Town(1, 2, new HexCoord(0, 0))); // 도시는 적 소유뿐
        var dead = DeadUnit(9, owner: 1, new HexCoord(5, 0), vanguard: 1);
        var reports = new List<CasualtyReport>();

        var s1 = FieldCasualties.ResolveUnit(s0, dead, new HexCoord(5, 0), reports);

        Assert.Null(reports.Single().Refuge);
        Assert.Null(s1.PostingOf(new GeneralId(1))); // 배속 해제 = 재야
    }

    [Fact]
    public void 캠페인_교전전멸이_장수판정으로_이어진다()
    {
        // 병력 1 부대가 적 대군에 한 주 안에 전멸 → 선봉이 반드시 아군 도시로 귀환한다.
        var movement = new MovementSimulator(new PassabilityMap(new HexMap(0, 20, -5, 5), [], []));
        var field = new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), 70));
        var engine = new CampaignEngine(field, new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100)),
            random: new SeededRandomSource(7));

        CombatUnit Army(int id, int owner, HexCoord pos, int troops, int? vanguard)
        {
            var f = new FieldUnit(new UnitId(id), new FactionId(owner), pos, 2, 2, 1,
                MovementDomain.Land, UnitMode.Attack, pos, id);
            var stats = CombatStatsBuilder.BuildField(T["swordsman"], AptitudeGrade.A, 0, TerrainType.Plains, troops);
            return new CombatUnit(f, stats, new TroopPool(troops, 0), UnitCombatState.Create(60),
                60, 60, troops, TroopClass.Infantry,
                VanguardId: vanguard is { } v ? new GeneralId(v) : null);
        }

        var weak = Army(1, owner: 1, new HexCoord(5, 0), troops: 1, vanguard: 1);
        var strong = Army(2, owner: 2, new HexCoord(6, 0), troops: 10000, vanguard: null);
        var state = new GameState(1, 1, new List<Faction>(),
            new List<City> { Town(1, 1, new HexCoord(0, 0)) },
            new List<General> { Gen(1) },
            Postings: new List<GeneralPosting> { new(new GeneralId(1), new FactionId(1), Location: null) },
            FieldArmies: new List<CombatUnit> { weak, strong });

        var after = engine.AdvanceWeek(state, out _, out _, out _, out _, out var casualties);

        var report = casualties.Single();
        Assert.Equal(new GeneralId(1), report.General);
        Assert.Equal(new CityId(1), report.Refuge);
        Assert.Equal(new CityId(1), after.PostingOf(new GeneralId(1))!.Location);
    }
}
