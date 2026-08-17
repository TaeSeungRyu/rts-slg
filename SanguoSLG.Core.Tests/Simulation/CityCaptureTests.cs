namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>10d 함락 처리 — 자동 입성·소유 전환·인구 페널티·명령 드롭·주둔 장수 판정·세력 소멸.</summary>
public class CityCaptureTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    // 시드 난수 — 50% 판정 순서를 고정한다.
    private sealed class FixedRandom(params int[] values) : IRandomSource
    {
        private readonly int[] _v = values;
        private int _i;
        public int Next(int minInclusive, int maxExclusive) => _v[_i++ % _v.Length];
    }

    private static General Gen(int id) => new(
        new GeneralId(id), $"g{id}", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: 60, Intellect: 60, Politics: 60);

    private static CombatUnit Attacker(int id, int owner, HexCoord pos, HexCoord target,
        int troops = 8000, int vanguard = 0)
    {
        var t = T["swordsman"];
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos,
            t.MovementPerDay, t.Detection, t.RangeUnit, MovementDomain.Land, UnitMode.Attack, target, id, t.RangeCastle);
        var stats = CombatStatsBuilder.BuildField(t, AptitudeGrade.A, 0, TerrainType.River, troops);
        return new CombatUnit(field, stats, new TroopPool(troops, 0), UnitCombatState.Create(60),
            60, 60, troops, t.Class, TroopCode: "swordsman", Training: 60,
            VanguardId: vanguard > 0 ? new GeneralId(vanguard) : null);
    }

    private static City Fallen(int id, int owner, HexCoord pos, int population = 200_000) =>
        new(new CityId(id), $"c{id}", pos, new FactionId(owner), Provisions: 3000, CastleSize.Medium,
            Gold: 5000, Security: 80, Population: population, Wall: 0); // 성벽 0(붕괴)

    private static GameState State(IEnumerable<City> cities, IEnumerable<CombatUnit> armies,
        IEnumerable<General>? generals = null, IEnumerable<GeneralPosting>? postings = null,
        IEnumerable<CityCommand>? commands = null) =>
        new(1, 1, new List<Faction>(), cities.ToList(), (generals ?? []).ToList(),
            PendingCommands: commands?.ToList(), Postings: postings?.ToList(),
            FieldArmies: armies.ToList());

    [Fact]
    public void 함락_근접_공격군이_점거하고_자원승계_인구페널티가_적용된다()
    {
        var city = Fallen(1, owner: 2, new HexCoord(5, 0), population: 200_000);
        var captor = Attacker(1, owner: 1, new HexCoord(4, 0), new HexCoord(5, 0), troops: 8000);
        var s = State([city], [captor]);

        var after = new CityCapture().ResolveAll(s, new FixedRandom(0), out var reports);

        var c = after.Cities.Single();
        Assert.Equal(new FactionId(1), c.Owner);          // 소유 전환
        Assert.Equal(5000, c.Gold);                        // 금 전부 승계
        Assert.Equal(3000, c.Provisions);                  // 군량 전부 승계
        Assert.Equal(180_000, c.Population);               // 인구 −10%
        Assert.Equal(30, c.Security);                      // 치안 30 리셋
        Assert.Empty(after.Armies);                        // 입성 부대는 야전에서 빠짐
        var g = after.Garrisons.Single(x => x.City == c.Id);
        Assert.Equal(("swordsman", 8000, 60), (g.TroopCode, g.Troops, g.TrainingLevel));
        var r = Assert.Single(reports);
        Assert.Equal((new FactionId(1), new FactionId(2)), (r.NewOwner, r.OldOwner));
    }

    [Fact]
    public void 함락_수비가_남아있으면_점거하지_않는다()
    {
        var city = Fallen(1, 2, new HexCoord(5, 0));
        var captor = Attacker(1, 1, new HexCoord(4, 0), new HexCoord(5, 0));
        var s = State([city], [captor],
            generals: null,
            postings: null)
            with { GarrisonForces = new List<GarrisonForce> { new(new CityId(1), "spearman", 500, 50) } };

        var after = new CityCapture().ResolveAll(s, new FixedRandom(0), out var reports);

        Assert.Empty(reports);
        Assert.Equal(new FactionId(2), after.Cities.Single().Owner);
    }

    [Fact]
    public void 함락_근접_공격군이_없으면_빈성으로_남는다()
    {
        var city = Fallen(1, 2, new HexCoord(5, 0));
        var far = Attacker(1, 1, new HexCoord(1, 0), new HexCoord(5, 0)); // 거리 4
        var s = State([city], [far]);

        var after = new CityCapture().ResolveAll(s, new FixedRandom(0), out var reports);

        Assert.Empty(reports);
        Assert.Equal(new FactionId(2), after.Cities.Single().Owner);
    }

    [Fact]
    public void 함락_진행중_명령이_드롭되고_수행장수가_풀린다()
    {
        var city = Fallen(1, 2, new HexCoord(5, 0));
        var captor = Attacker(1, 1, new HexCoord(4, 0), new HexCoord(5, 0));
        var cmd = new CityCommand(new CityId(1), CommandKind.SetTaxRate, new GeneralId(50), null, 1, 8, 30, "", "");
        var s = State([city], [captor], commands: [cmd]);

        var after = new CityCapture().ResolveAll(s, new FixedRandom(0), out _);

        Assert.Empty(after.Commands);
        Assert.False(after.IsGeneralBusy(new GeneralId(50)));
    }

    [Fact]
    public void 함락_주둔장수는_50퍼센트_포로_50퍼센트_후퇴한다()
    {
        // 옛 세력(2)은 다른 도시(9)도 보유 → 소멸 안 함. 주둔 장수 2명(태수 포함).
        var fallen = Fallen(1, 2, new HexCoord(5, 0));
        var refuge = new City(new CityId(9), "본성", new HexCoord(12, 0), new FactionId(2), 0);
        var captor = Attacker(1, 1, new HexCoord(4, 0), new HexCoord(5, 0));
        var s = State([fallen, refuge], [captor],
            generals: [Gen(10), Gen(11)],
            postings:
            [
                new GeneralPosting(new GeneralId(10), new FactionId(2), new CityId(1)),
                new GeneralPosting(new GeneralId(11), new FactionId(2), new CityId(1)),
            ]);

        // Next(0,2): 첫 장수 0=포로, 둘째 1=후퇴.
        var after = new CityCapture().ResolveAll(s, new FixedRandom(0, 1), out var reports);

        var r = Assert.Single(reports);
        Assert.False(r.FactionEliminated);
        Assert.Equal(new GeneralId(10), Assert.Single(r.Captured));
        Assert.Equal(new GeneralId(11), Assert.Single(r.Fled));

        // 포로: 억류 세력 1, 원 세력 2, 배속 해제.
        var p = after.PrisonerOf(new GeneralId(10));
        Assert.NotNull(p);
        Assert.Equal((new FactionId(1), new FactionId(2)), (p!.Holder, p.Origin));
        // 후퇴: 원 세력 최근접 보유 도시(9)로 주둔 이동.
        Assert.Equal(new CityId(9), after.PostingOf(new GeneralId(11))!.Location);
    }

    [Fact]
    public void 캠페인_공성부터_함락까지_한_흐름으로_점거된다()
    {
        // 얇은 소성(성벽 600·수비 800)에 도검 15000이 진격 — 여러 주에 걸쳐 성벽·수비를 깎고 점거.
        var city = new City(new CityId(1), "소성", new HexCoord(6, 0), new FactionId(2),
            Provisions: 2000, CastleSize.Small, Gold: 3000, Security: 70, Population: 80_000, Wall: 600);
        var attacker = Attacker(1, owner: 1, new HexCoord(2, 0), new HexCoord(6, 0), troops: 15000, vanguard: 20);
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { city }, new List<General> { Gen(20) },
            Postings: new List<GeneralPosting> { new(new GeneralId(20), new FactionId(1), null) },
            GarrisonForces: new List<GarrisonForce> { new(new CityId(1), "spearman", 800, 50) },
            FieldArmies: new List<CombatUnit> { attacker });

        var movement = new MovementSimulator(new PassabilityMap(new HexMap(0, 20, -6, 6), [], []));
        var field = new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), 70));
        var engine = new CampaignEngine(field, new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100)),
            new CampaignSiege(new BattleResolver(60), T.Values.ToList()),
            new CityCapture(), new SeededRandomSource(1));

        IReadOnlyList<CaptureReport> caps = [];
        for (var w = 0; w < 8 && s.CityCount(new FactionId(1)) == 0; w++)
        {
            s = engine.AdvanceWeek(s, out _, out _, out var c);
            if (c.Count > 0)
            {
                caps = c;
            }
        }

        Assert.Equal(new FactionId(1), s.Cities.Single().Owner); // 점거 완료
        Assert.Single(caps);
        Assert.Equal(new CityId(1), s.PostingOf(new GeneralId(20))!.Location); // 선봉 장수가 새 주둔
        Assert.Contains(s.Garrisons, g => g.City == new CityId(1) && g.TroopCode == "swordsman");
    }

    [Fact]
    public void 함락_마지막_도시를_잃으면_세력이_소멸하고_전원_재야가_된다()
    {
        var last = Fallen(1, 2, new HexCoord(5, 0));       // 세력 2의 유일한 도시
        var captor = Attacker(1, 1, new HexCoord(4, 0), new HexCoord(5, 0));
        var s = State([last], [captor],
            generals: [Gen(10), Gen(11)],
            postings:
            [
                new GeneralPosting(new GeneralId(10), new FactionId(2), new CityId(1)),
                new GeneralPosting(new GeneralId(11), new FactionId(2), null), // 출전 중
            ]);

        var after = new CityCapture().ResolveAll(s, new FixedRandom(0), out var reports);

        var r = Assert.Single(reports);
        Assert.True(r.FactionEliminated);
        Assert.Null(after.PostingOf(new GeneralId(10)));   // 전원 재야
        Assert.Null(after.PostingOf(new GeneralId(11)));
        Assert.Empty(after.Prisoners);                      // 소멸이라 포로도 안 잡힘
    }
}
