namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>야전 전멸 시 장수 처리(design-general-lifecycle §4b) — 50% 포로/50% 탈출, 무교전 100% 탈출.</summary>
public class FieldCasualtiesTests
{
    private sealed class StubRandom(int value) : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => value;
    }

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
    public void 교전전멸_난수0이면_선봉과_부관이_포로가된다()
    {
        var s0 = State(Town(1, 1, new HexCoord(0, 0)));
        var dead = DeadUnit(9, owner: 1, new HexCoord(5, 0), vanguard: 1, adjutant: 2);
        var reports = new List<CasualtyReport>();

        var s1 = FieldCasualties.ResolveUnit(s0, dead, captor: new FactionId(2), new HexCoord(5, 0),
            new StubRandom(0), reports);

        Assert.Equal(2, reports.Count);
        Assert.All(reports, r => Assert.True(r.Captured));
        Assert.Equal(2, s1.Prisoners.Count);
        Assert.All(s1.Prisoners, p => Assert.Equal(new FactionId(2), p.Holder));
        Assert.Empty(s1.Assignments); // 포로는 배속 해제
    }

    [Fact]
    public void 교전전멸_난수1이면_최근접_아군도시로_귀환한다()
    {
        var s0 = State(Town(1, 1, new HexCoord(0, 0)), Town(2, 1, new HexCoord(9, 0)));
        var dead = DeadUnit(9, owner: 1, new HexCoord(7, 0), vanguard: 1);
        var reports = new List<CasualtyReport>();

        var s1 = FieldCasualties.ResolveUnit(s0, dead, captor: new FactionId(2), new HexCoord(7, 0),
            new StubRandom(1), reports);

        Assert.False(reports.Single().Captured);
        Assert.Equal(new CityId(2), reports.Single().Refuge); // (9,0)이 (7,0)에서 최근접
        Assert.Equal(new CityId(2), s1.PostingOf(new GeneralId(1))!.Location);
        Assert.Empty(s1.Prisoners);
    }

    [Fact]
    public void 무교전전멸은_난수와_무관하게_귀환한다()
    {
        var s0 = State(Town(1, 1, new HexCoord(0, 0)));
        var dead = DeadUnit(9, owner: 1, new HexCoord(5, 0), vanguard: 1);
        var reports = new List<CasualtyReport>();

        var s1 = FieldCasualties.ResolveUnit(s0, dead, captor: null, new HexCoord(5, 0),
            new StubRandom(0), reports); // 난수 0이라도 포획 주체가 없으면 탈출

        Assert.False(reports.Single().Captured);
        Assert.Equal(new CityId(1), s1.PostingOf(new GeneralId(1))!.Location);
    }

    [Fact]
    public void 보유도시가_없으면_재야가된다()
    {
        var s0 = State(Town(1, 2, new HexCoord(0, 0))); // 도시는 적 소유뿐
        var dead = DeadUnit(9, owner: 1, new HexCoord(5, 0), vanguard: 1);
        var reports = new List<CasualtyReport>();

        var s1 = FieldCasualties.ResolveUnit(s0, dead, captor: null, new HexCoord(5, 0),
            new StubRandom(1), reports);

        Assert.False(reports.Single().Captured);
        Assert.Null(reports.Single().Refuge);
        Assert.Null(s1.PostingOf(new GeneralId(1))); // 배속 해제 = 재야
    }

    [Fact]
    public void 캠페인_교전전멸이_장수판정으로_이어진다()
    {
        // 병력 1 부대가 적 대군에 한 주 안에 전멸 → 선봉이 포로 또는 아군 도시 귀환으로 보고된다.
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

        engine.AdvanceWeek(state, out _, out _, out _, out _, out var casualties);

        var report = casualties.Single();
        Assert.Equal(new GeneralId(1), report.General);
        Assert.True(report.Captured || report.Refuge == new CityId(1)); // 포로 또는 귀환 — 방치 없음
    }
}
