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
    [Theory]
    [InlineData(PortSize.Small)]
    [InlineData(PortSize.Medium)]
    [InlineData(PortSize.None)]
    public void 전진중_발견한_거점은_수비전멸_즉시_점령하고_재점령할수있다(PortSize size)
    {
        var city = Fallen(1, 2, new HexCoord(5, 0)) with { Port = size };
        var first = Attacker(1, 1, new HexCoord(4, 0), new HexCoord(8, 0));
        first = first with { Field = first.Field with { Mode = UnitMode.Advance,
            PursuitTarget = new SanguoSLG.Core.Simulation.RenewalMovement.RenewalTargetId(
                SanguoSLG.Core.Simulation.RenewalMovement.RenewalTargetKind.Site, 1) } };
        var captured = new CityCapture().ResolveAll(State([city], [first]), new FixedRandom(0), out var reports);
        Assert.Single(reports);
        Assert.Equal(new FactionId(1), captured.Cities.Single().Owner);
        var second = first with { Field = first.Field with { Id = new UnitId(2), Owner = new FactionId(2) } };
        var recaptured = new CityCapture().ResolveAll(captured with { GarrisonForces = [], FieldArmies = [second] },
            new FixedRandom(0), out reports);
        Assert.Single(reports);
        Assert.Equal(new FactionId(2), recaptured.Cities.Single().Owner);
        Assert.Empty(recaptured.Armies);
    }

    [Fact]
    public void 다른거점을_추격중인_전진부대는_빈항구를_점령하지않는다()
    {
        var port = Fallen(1, 2, new HexCoord(5, 0)) with { Port = PortSize.Small };
        var army = Attacker(1, 1, new HexCoord(4, 0), new HexCoord(8, 0));
        army = army with { Field = army.Field with { Mode = UnitMode.Advance,
            PursuitTarget = new SanguoSLG.Core.Simulation.RenewalMovement.RenewalTargetId(
                SanguoSLG.Core.Simulation.RenewalMovement.RenewalTargetKind.Site, 2) } };
        new CityCapture().ResolveAll(State([port], [army]), new FixedRandom(0), out var reports);
        Assert.Empty(reports);
    }
    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    // 시드 난수 — 함락/멸망 확률 판정 순서를 고정한다.
    private sealed class FixedRandom(params int[] values) : IRandomSource
    {
        private readonly int[] _v = values;
        private int _i;
        public int Next(int minInclusive, int maxExclusive) => _v[_i++ % _v.Length];
    }

    private sealed class NoRandom : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive)
            => minInclusive == 50 && maxExclusive == 71 ? 50
                : throw new Xunit.Sdk.XunitException("세력 존속 중 함락에는 장수 합류 난수를 쓰면 안 됩니다.");
    }

    [Fact]
    public void 선전포고한_도시는_치안을_유지하고_포고기록은_점령후_소진된다()
    {
        var city = Fallen(1, 2, new HexCoord(5, 0));
        var baseState = State([city], [Attacker(1, 1, new HexCoord(4, 0), city.Position)]);
        var declared = baseState with { WarDeclarations =
            [new CityWarDeclaration(new FactionId(1), city.Id, city.Owner, baseState.Day)] };

        var after = new CityCapture().ResolveAll(declared, new NoRandom(), out _);

        Assert.Equal(80, after.Cities.Single().Security);
        Assert.Empty(after.DeclaredWars);
    }

    [Fact]
    public void 만료된_선전포고는_치안_손실을_막지_못한다()
    {
        var city = Fallen(1, 2, new HexCoord(5, 0));
        var state = State([city], [Attacker(1, 1, new HexCoord(4, 0), city.Position)]) with
        {
            Day = 101,
            WarDeclarations = [new CityWarDeclaration(new FactionId(1), city.Id, city.Owner, 1)],
        };

        var after = new CityCapture().ResolveAll(state, new FixedRandom(50), out _);

        Assert.Equal(40, after.Cities.Single().Security);
    }

    [Theory]
    [InlineData(50, 40)]
    [InlineData(70, 24)]
    public void 포고하지_않은_도시는_기존치안의_절반에서_칠십퍼센트를_잃는다(int roll, int expected)
    {
        var city = Fallen(1, 2, new HexCoord(5, 0));
        var state = State([city], [Attacker(1, 1, new HexCoord(4, 0), city.Position)]);

        var after = new CityCapture().ResolveAll(state, new FixedRandom(roll), out _);

        Assert.Equal(expected, after.Cities.Single().Security);
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
    public void 도적은_성벽과_수비가_없어도_성을_점령하지_못한다()
    {
        var city = Fallen(1, 2, new HexCoord(5, 0));
        var bandit = Attacker(1, WorldEngine.BanditFaction.Value, new HexCoord(4, 0), city.Position);
        var state = State([city], [bandit]);
        var after = new CityCapture().ResolveAll(state, new FixedRandom(0), out var reports);
        Assert.Equal(city, after.Cities.Single());
        Assert.Single(after.Armies);
        Assert.Empty(reports);

        var regular = Attacker(2, 1, new HexCoord(5, -1), city.Position);
        after = new CityCapture().ResolveAll(state with { FieldArmies = [bandit, regular] }, new FixedRandom(0), out reports);
        Assert.Equal(new FactionId(1), after.Cities.Single().Owner);
        Assert.Single(reports);
    }

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
        Assert.Equal(40, c.Security);                      // 포고 없이 점령: 기존 80의 50% 손실
        Assert.Empty(after.Armies);                        // 입성 부대는 야전에서 빠짐
        var g = after.Garrisons.Single(x => x.City == c.Id);
        Assert.Equal(("swordsman", 8000, 60), (g.TroopCode, g.Troops, g.TrainingLevel));
        var r = Assert.Single(reports);
        Assert.Equal((new FactionId(1), new FactionId(2)), (r.NewOwner, r.OldOwner));
    }

    [Fact]
    public void 중형성_발자국_상단에_접근한_공격군도_즉시_점거한다()
    {
        var city = Fallen(1, owner: 2, new HexCoord(5, 0));
        var topTile = CastleFootprint.TilesFor(city).OrderByDescending(t => t.R).First();
        var attacker = Attacker(1, owner: 1, topTile + new HexCoord(0, 1), city.Position);
        var after = new CityCapture().ResolveAll(State([city], [attacker]), new FixedRandom(0), out var reports);

        Assert.Single(reports);
        Assert.Equal(new FactionId(1), after.Cities.Single().Owner);
        Assert.Empty(after.Armies);
    }

    [Fact]
    public void 항구가_함락되면_저장된_선박은_새_소유자가_사용한다()
    {
        var port = Fallen(1, owner: 2, new HexCoord(5, 0), population: 40_000)
            with { Port = PortSize.Small };
        var captor = Attacker(1, owner: 1, new HexCoord(4, 0), port.Position, troops: 8000);
        var state = State([port], [captor]) with
        {
            PortShipStocks = [new PortShipStock(port.Id, "small_boat", 3)],
        };

        var after = new CityCapture().ResolveAll(state, new FixedRandom(0), out _);

        Assert.Equal(new FactionId(1), after.Cities.Single(c => c.Id == port.Id).Owner);
        var stock = after.PortShips.Single(s => s.City == port.Id && s.ShipCode == "small_boat");
        Assert.Equal(3, stock.Count);
    }

    [Fact]
    public void 집단군이_성을_점령하면_구성_병종별로_주둔한다()
    {
        var city = Fallen(1, owner: 2, new HexCoord(5, 0));
        var armyGroup = Attacker(1, owner: 1, new HexCoord(4, 0), city.Position, troops: 15000) with
        {
            IsArmyGroup = true,
            TroopCode = "army_group",
            SupplyCargo =
            [
                new SupplyComponent("swordsman", 5000, 60),
                new SupplyComponent("archer", 5000, 70),
                new SupplyComponent("siege_tower", 5000, 80),
            ],
        };

        var after = new CityCapture().ResolveAll(State([city], [armyGroup]), new FixedRandom(0), out var reports);

        Assert.Single(reports);
        Assert.Empty(after.Armies);
        Assert.DoesNotContain(after.Garrisons, g => g.TroopCode == "army_group");
        Assert.Equal(15000, after.Garrisons.Sum(g => g.Troops));
        Assert.Equal(["archer", "siege_tower", "swordsman"],
            after.Garrisons.Select(g => g.TroopCode).OrderBy(x => x).ToArray());
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
    public void 함락_세력이_존속하면_주둔장수는_모두_후퇴한다()
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

        var after = new CityCapture().ResolveAll(s, new FixedRandom(29, 30), out var reports);

        var r = Assert.Single(reports);
        Assert.False(r.FactionEliminated);
        Assert.Empty(r.Captured);
        Assert.Equal([new GeneralId(10), new GeneralId(11)], r.Fled);
        Assert.Equal((new FactionId(2), new CityId(9)),
            (after.PostingOf(new GeneralId(10))!.Faction, after.PostingOf(new GeneralId(10))!.Location));
        Assert.Equal(new CityId(9), after.PostingOf(new GeneralId(11))!.Location);
    }

    [Fact]
    public void 항구를_잃으면_옛_담당과_진행명령이_해제되고_재점령해도_복원되지_않는다()
    {
        var port = Fallen(1, 1, new HexCoord(5, 0)) with
        {
            Port = PortSize.Small,
            Governor = new GeneralId(10),
            Strategist = new GeneralId(11),
            SecurityOfficer = new GeneralId(10),
            DomesticOfficer = new GeneralId(11),
            RecruitmentOfficer = new GeneralId(10),
            TrainingOfficer = new GeneralId(11),
            AutoRecruitTroopCode = "swordsman",
            AutoRecruitTroopCodes = "swordsman,archer",
            AutoRecruitRate = 3,
        };
        var refuge = Fallen(9, 1, new HexCoord(12, 0));
        var enemyHome = Fallen(8, 2, new HexCoord(-5, 0));
        var exploration = new CityCommand(port.Id, CommandKind.Explore, new GeneralId(11), null,
            1, 8, 0, "", "");
        var state = State([port, refuge, enemyHome],
            [Attacker(1, 2, new HexCoord(4, 0), port.Position)],
            [Gen(10), Gen(11)],
            [new GeneralPosting(new GeneralId(10), new FactionId(1), port.Id),
                new GeneralPosting(new GeneralId(11), new FactionId(1), port.Id)],
            [exploration]);

        var lost = new CityCapture().ResolveAll(state, new FixedRandom(99), out var lossReports);

        Assert.Equal([new GeneralId(10), new GeneralId(11)], Assert.Single(lossReports).Fled);
        Assert.All(new[] { new GeneralId(10), new GeneralId(11) }, id =>
        {
            Assert.Equal(refuge.Id, lost.PostingOf(id)!.Location);
            Assert.False(lost.IsGeneralBusy(id));
        });
        Assert.Empty(lost.Commands);
        AssertVacant(lost.Cities.Single(c => c.Id == port.Id));

        var retaken = new CityCapture().ResolveAll(lost with
        {
            GarrisonForces = [],
            FieldArmies = [Attacker(2, 1, new HexCoord(4, 0), port.Position)],
        }, new FixedRandom(99), out _);

        AssertVacant(retaken.Cities.Single(c => c.Id == port.Id));
        Assert.Equal(refuge.Id, retaken.PostingOf(new GeneralId(11))!.Location);
    }

    private static void AssertVacant(City city)
    {
        Assert.Null(city.Governor);
        Assert.Null(city.Strategist);
        Assert.Null(city.SecurityOfficer);
        Assert.Null(city.DomesticOfficer);
        Assert.Null(city.RecruitmentOfficer);
        Assert.Null(city.TrainingOfficer);
        Assert.Equal("", city.AutoRecruitTroopCode);
        Assert.Equal("", city.AutoRecruitTroopCodes);
        Assert.Equal(1, city.AutoRecruitRate);
    }

    [Fact]
    public void 항구_재점령_시_양쪽_주둔장수는_각자_소속_거점으로_후퇴한다()
    {
        var port = Fallen(1, 1, new HexCoord(5, 0)) with { Port = PortSize.Small };
        var playerHome = Fallen(9, 1, new HexCoord(12, 0));
        var enemyHome = Fallen(8, 2, new HexCoord(-5, 0));
        var officer = new GeneralId(11);
        var state = State([port, playerHome, enemyHome],
            [Attacker(1, 2, new HexCoord(4, 0), port.Position)],
            [Gen(11)], [new GeneralPosting(officer, new FactionId(1), port.Id)]);

        var lost = new CityCapture().ResolveAll(state, new FixedRandom(0), out var first);
        Assert.Empty(Assert.Single(first).Captured);
        Assert.Equal([officer], Assert.Single(first).Fled);
        Assert.Equal((new FactionId(1), playerHome.Id),
            (lost.PostingOf(officer)!.Faction, lost.PostingOf(officer)!.Location));

        var retaken = new CityCapture().ResolveAll(lost with
        {
            GarrisonForces = [],
            FieldArmies = [Attacker(2, 1, new HexCoord(4, 0), port.Position)],
        }, new FixedRandom(0), out var second);

        Assert.Empty(Assert.Single(second).Captured);
        Assert.Equal((new FactionId(1), playerHome.Id),
            (retaken.PostingOf(officer)!.Faction, retaken.PostingOf(officer)!.Location));
        Assert.False(retaken.IsGeneralBusy(officer));
    }

    [Theory]
    [InlineData(CommandKind.Explore)]
    [InlineData(CommandKind.Repair)]
    [InlineData(CommandKind.Research)]
    [InlineData(CommandKind.BuildShip)]
    [InlineData(CommandKind.CityStratagem)]
    public void 함락된_도시의_진행명령_장수와_보좌는_점령세력에_합류하지_않고_후퇴한다(CommandKind kind)
    {
        var port = Fallen(1, 1, new HexCoord(5, 0)) with { Port = PortSize.Small };
        var refuge = Fallen(9, 1, new HexCoord(12, 0));
        var enemyHome = Fallen(8, 2, new HexCoord(-5, 0));
        var command = new CityCommand(port.Id, kind, new GeneralId(10), new GeneralId(11),
            1, 15, 0);
        var state = State([port, refuge, enemyHome],
            [Attacker(1, 2, new HexCoord(4, 0), port.Position)],
            [Gen(10), Gen(11)],
            [new GeneralPosting(new GeneralId(10), new FactionId(1), port.Id),
                new GeneralPosting(new GeneralId(11), new FactionId(1), port.Id)],
            [command]);

        var after = new CityCapture().ResolveAll(state, new FixedRandom(0), out var reports);

        Assert.Empty(after.Commands);
        Assert.Empty(Assert.Single(reports).Captured);
        Assert.Equal([new GeneralId(10), new GeneralId(11)], Assert.Single(reports).Fled);
        Assert.All(after.Assignments.Where(p => p.General.Value is 10 or 11), p =>
        {
            Assert.Equal(refuge.Id, p.Location);
            Assert.Equal(new FactionId(1), p.Faction);
        });
    }

    [Fact]
    public void 함락된_도시의_담당자와_생산파견_장수는_작업이_취소되고_보유성으로_돌아간다()
    {
        var port = Fallen(1, 1, new HexCoord(5, 0)) with
        {
            Port = PortSize.Small,
            SecurityOfficer = new GeneralId(10),
        };
        var refuge = Fallen(9, 1, new HexCoord(12, 0));
        var enemyHome = Fallen(8, 2, new HexCoord(-5, 0));
        var production = new ProductionOperation(1, port.Id, port.Owner, port.Position,
            new HexCoord(6, 0), "village", "swordsman", 500, 50, 2,
            new GeneralId(11), 1, 14, ProductionPhase.Gathering, 2, new HexCoord(6, 0));
        var state = State([port, refuge, enemyHome],
            [Attacker(1, 2, new HexCoord(4, 0), port.Position)],
            [Gen(10), Gen(11)],
            [new GeneralPosting(new GeneralId(10), new FactionId(1), port.Id),
                new GeneralPosting(new GeneralId(11), new FactionId(1), null)])
            with { ProductionOperations = [production] };

        var after = new CityCapture().ResolveAll(state, new FixedRandom(0), out var reports);

        Assert.Empty(after.ProductionOps);
        Assert.Null(after.Cities.Single(c => c.Id == port.Id).SecurityOfficer);
        Assert.Empty(Assert.Single(reports).Captured);
        Assert.Equal([new GeneralId(10), new GeneralId(11)], Assert.Single(reports).Fled);
        Assert.All(after.Assignments.Where(p => p.General.Value is 10 or 11), p =>
            Assert.Equal(refuge.Id, p.Location));
    }

    [Theory]
    [InlineData(PortSize.None)]
    [InlineData(PortSize.Small)]
    public void 성과_항구_함락시_비출전_업무는_취소하고_야전_장수는_이동시키지_않는다(PortSize portSize)
    {
        var fallen = Fallen(1, 1, new HexCoord(5, 0)) with
        {
            Port = portSize,
            Governor = new GeneralId(10),
            Strategist = new GeneralId(11),
            TrainingOfficer = new GeneralId(12),
        };
        var refuge = Fallen(9, 1, new HexCoord(12, 0));
        var enemyHome = Fallen(8, 2, new HexCoord(-5, 0));
        var command = new CityCommand(fallen.Id, CommandKind.FormAlliance,
            new GeneralId(13), new GeneralId(14), 1, 15, 0);
        var unrelated = new CityCommand(refuge.Id, CommandKind.Explore,
            new GeneralId(16), null, 1, 15, 0);
        var production = new ProductionOperation(1, fallen.Id, fallen.Owner, fallen.Position,
            new HexCoord(6, 0), "village", "swordsman", 500, 50, 2,
            new GeneralId(15), 1, 14, ProductionPhase.Gathering, 2, new HexCoord(6, 0));
        var field = Attacker(3, 1, new HexCoord(20, 0), new HexCoord(21, 0), vanguard: 17);
        var generals = Enumerable.Range(10, 8).Select(Gen).ToList();
        var postings = generals.Select(g => new GeneralPosting(g.Id, new FactionId(1),
            g.Id.Value is 13 or 14 or 15 ? null : fallen.Id)).ToList();
        postings[6] = postings[6] with { Location = refuge.Id };
        var state = State([fallen, refuge, enemyHome],
            [Attacker(1, 2, new HexCoord(4, 0), fallen.Position), field],
            generals, postings, [command, unrelated])
            with { ProductionOperations = [production] };

        var after = new CityCapture().ResolveAll(state, new NoRandom(), out var reports);

        Assert.Empty(Assert.Single(reports).Captured);
        Assert.Equal([new GeneralId(10), new GeneralId(11), new GeneralId(12),
            new GeneralId(13), new GeneralId(14), new GeneralId(15)],
            Assert.Single(reports).Fled);
        Assert.All(Enumerable.Range(10, 6), id =>
        {
            Assert.Equal(refuge.Id, after.PostingOf(new GeneralId(id))!.Location);
            Assert.False(after.IsGeneralBusy(new GeneralId(id)));
        });
        Assert.Equal(fallen.Id, after.PostingOf(new GeneralId(17))!.Location);
        Assert.True(after.IsGeneralInField(new GeneralId(17)));
        Assert.Equal([unrelated], after.Commands);
        Assert.Empty(after.ProductionOps);
        AssertVacant(after.Cities.Single(c => c.Id == fallen.Id));
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
        var capture = Assert.Single(caps);
        Assert.True(capture.TurnIndex >= 0);
        Assert.Equal(new UnitId(1), Assert.Single(capture.OccupyingUnits));
        Assert.Equal(new CityId(1), s.PostingOf(new GeneralId(20))!.Location); // 선봉 장수가 새 주둔
        Assert.Contains(s.Garrisons, g => g.City == new CityId(1) && g.TroopCode == "swordsman");
    }

    [Fact]
    public void 함락_마지막_도시를_잃으면_70퍼센트_확률로_점거세력에_합류한다()
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

        var after = new CityCapture().ResolveAll(s, new FixedRandom(50, 69, 70, 99), out var reports);

        var r = Assert.Single(reports);
        Assert.True(r.FactionEliminated);
        Assert.Equal(new GeneralId(10), Assert.Single(r.Captured));
        Assert.Empty(r.Fled);
        Assert.Equal((new FactionId(1), new CityId(1)), (after.PostingOf(new GeneralId(10))!.Faction, after.PostingOf(new GeneralId(10))!.Location));
        Assert.Null(after.PostingOf(new GeneralId(11)));   // 획득 실패 + 타 세력 없음 → 재야
    }

    [Fact]
    public void 세력멸망_점령세력에_합류하지_않은_30퍼센트는_타세력에_편입된다()
    {
        var last = Fallen(1, 2, new HexCoord(5, 0));       // 세력 2의 유일한 도시
        var third = new City(new CityId(9), "제3성", new HexCoord(12, 0), new FactionId(3), 0);
        var captor = Attacker(1, 1, new HexCoord(4, 0), new HexCoord(5, 0));
        var s = State([last, third], [captor],
            generals: [Gen(10)],
            postings: [new GeneralPosting(new GeneralId(10), new FactionId(2), new CityId(1))]);

        var after = new CityCapture().ResolveAll(s, new FixedRandom(70), out var reports);

        var r = Assert.Single(reports);
        Assert.True(r.FactionEliminated);
        Assert.Empty(r.Captured);
        Assert.Equal(new GeneralId(10), Assert.Single(r.Fled));
        Assert.Equal((new FactionId(3), new CityId(9)), (after.PostingOf(new GeneralId(10))!.Faction, after.PostingOf(new GeneralId(10))!.Location));
    }
}
