using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using SanguoSLG.Game;

namespace SanguoSLG.Core.Tests.Simulation;

public class ControlledDeploymentIntegrationTests
{
    private static readonly City Changan = new(new CityId(1), "장안", new HexCoord(1, 2), new FactionId(1), 3000, CastleSize.Medium);
    private static readonly HexCoord Ruin = new(0, 4);

    private static CombatUnit Unit(int id, DeploymentDirection direction, int speed = 3, int delay = 0)
    {
        var exit = DeploymentEgressRules.RepresentativeExit(Changan, direction, Ruin,
            tile => tile != Ruin)!.Value;
        return new(new FieldUnit(new UnitId(id), Changan.Owner, Changan.Position, speed, 0, 1,
            MovementDomain.Land, UnitMode.March, Ruin, id), new CombatStats(10000, 1, 1),
            new TroopPool(10000, 0), UnitCombatState.Create(60), OriginCity: Changan.Id,
            EgressDirection: direction, EgressExit: exit, AwaitingEgress: true, DeploymentDelayDays: delay);
    }

    private static CampaignEngine Engine(IReadOnlyList<City>? cities = null)
    {
        cities ??= [Changan];
        var water = cities.Where(city => city.IsPort)
            .SelectMany(DeploymentEgressRules.ExteriorTiles).Distinct()
            .ToDictionary(tile => tile, _ => TerrainType.WaterDeep);
        return new(new AdvanceOrchestrator(new MovementSimulator(new PassabilityMap(
            new HexMap(-10, 20, -10, 20, water), [], cities, [Ruin])),
            new CombatPhaseResolver(new BattleResolver(60), 70), provisionsPer10kPerDay: 0),
            new WorldEngine(new BalanceConfig(100)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void 출격한칸도_이동력을사용하고_재생은인접칸을순서대로따른다(int speed)
    {
        var unit = Unit(1, DeploymentDirection.SouthEast, speed) with
        {
            Field = Unit(1, DeploymentDirection.SouthEast, speed).Field with { Target = new HexCoord(9, 4) },
        };
        var state = new GameState(1, 1, [], [Changan], [], FieldArmies: [unit]);
        Engine().AdvanceWeek(state, out var turns);
        var first = turns[0];
        Assert.Equal(unit.EgressExit, Assert.Single(first.Deployments).Field.Position);
        var playback = new MovementPlayback(new() { [1] = Changan.Position });
        playback.AppendDeployment(1, 1, unit.EgressExit!.Value, 2.5, 0.5);
        playback.Append(first.Movement, 0, 2.5, 0.5);
        Assert.InRange(playback.Moves.Count, 1, speed);
        Assert.All(playback.Moves.Zip(playback.Moves.Skip(1)), pair => Assert.Equal(1, pair.First.To.Distance(pair.Second.To)));
        Assert.True(playback.Moves[^1].Time + 0.5 <= 1.5);
        Assert.Equal(speed, first.Units.Single().Field.Speed);
        Assert.All(turns.Skip(1), turn => Assert.Empty(turn.Deployments));
    }

    [Fact]
    public void 장안_극병유적_남서남동_동시출격은_각각선택한출구를쓴다()
    {
        var units = new[] { Unit(1, DeploymentDirection.SouthWest), Unit(2, DeploymentDirection.SouthEast, 2) };
        var state = new GameState(1, 1, [], [Changan], [], FieldArmies: units);
        var next = Engine().AdvanceWeek(state, out var turns);
        Assert.Equal(2, turns[0].Deployments.Count);
        Assert.Equal(new HexCoord(-1, 4), turns[0].Deployments[0].Field.Position);
        Assert.Equal(new HexCoord(1, 4), turns[0].Deployments[1].Field.Position);
        Assert.All(turns.SelectMany(t => t.Movement.Ticks).SelectMany(t => t.Units),
            u => Assert.DoesNotContain(u.Position, CastleFootprint.TilesFor(Changan)));
        Engine().AdvanceWeek(next, out var second);
        Assert.All(second, t => Assert.Empty(t.Deployments));
    }

    [Fact]
    public void 육일지연후_출구가막혀도_방향과목표를보존하고_다음진행에출격한다()
    {
        var waiting = Unit(1, DeploymentDirection.SouthWest, delay: 6);
        var blocker = Unit(9, DeploymentDirection.SouthWest) with
        {
            AwaitingEgress = false,
            Field = Unit(9, DeploymentDirection.SouthWest).Field with { Position = waiting.EgressExit!.Value, Target = null },
        };
        var engine = Engine();
        var state = engine.AdvanceWeek(new GameState(1, 1, [], [Changan], [], FieldArmies: [waiting, blocker]), out var turns);
        Assert.All(turns, t => Assert.Empty(t.Deployments));
        var preserved = state.Armies.Single(u => u.Id == waiting.Id);
        Assert.True(preserved.IsWaitingEgress);
        Assert.Equal(waiting.Field.Target, preserved.Field.Target);
        Assert.Equal(waiting.EgressDirection, preserved.EgressDirection);
        engine.AdvanceWeek(state with { FieldArmies = [preserved] }, out var second);
        Assert.Equal(waiting.EgressExit, Assert.Single(second[0].Deployments).Field.Position);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void 성과항구_육일지연이끝나도_출구가점유되면_다음주까지대기열에남는다(bool port)
    {
        var origin = port ? Changan with { Port = PortSize.Small } : Changan;
        var direction = DeploymentDirection.SouthEast;
        var exit = DeploymentEgressRules.RepresentativeExit(origin, direction)!.Value;
        var waiting = Unit(1, direction, delay: 6) with
        {
            Field = Unit(1, direction, delay: 6).Field with
            {
                Position = origin.Position,
                Domain = port ? MovementDomain.DeepWater : MovementDomain.Land,
                Target = null,
            },
            EgressExit = exit,
        };
        var blocker = Unit(9, direction) with
        {
            Field = Unit(9, direction).Field with
            {
                Position = exit, Target = null,
                Domain = port ? MovementDomain.DeepWater : MovementDomain.Land,
            },
            AwaitingEgress = false,
        };
        var engine = Engine([origin]);
        var state = new GameState(1, 1, [], [origin], [], FieldArmies: [waiting, blocker]);

        for (var week = 0; week < 2; week++)
        {
            state = engine.AdvanceWeek(state, out var turns);
            Assert.All(turns, turn =>
            {
                Assert.Empty(turn.Deployments);
                Assert.Contains(turn.Units, unit => unit.Id == waiting.Id && unit.IsWaitingDeployment);
            });
            var queued = state.Armies.Single(unit => unit.Id == waiting.Id);
            Assert.Equal(0, queued.DeploymentDelayDays);
            Assert.True(queued.AwaitingEgress);
            Assert.Equal(origin.Position, queued.Field.Position);
            Assert.Equal(direction, queued.EgressDirection);
            Assert.Equal(exit, queued.EgressExit);
        }

        state = state with { FieldArmies = state.Armies.Where(unit => unit.Id != blocker.Id).ToArray() };
        engine.AdvanceWeek(state, out var releasedTurns);
        Assert.Equal(waiting.Id, Assert.Single(releasedTurns[0].Deployments).Id);
        Assert.Equal(exit, releasedTurns[0].Deployments[0].Field.Position);
    }

    [Fact]
    public void 같은방향_세부대는_서로겹치지않고_같은출구로_순서대로출격한다()
    {
        var units = Enumerable.Range(1, 3).Select(id =>
        {
            var unit = Unit(id, DeploymentDirection.SouthEast);
            return unit with { Field = unit.Field with { Target = new HexCoord(9, 4) } };
        }).ToArray();
        Engine().AdvanceWeek(new GameState(1, 1, [], [Changan], [], FieldArmies: units), out var turns);
        var released = turns.SelectMany(t => t.Deployments).ToArray();
        Assert.Equal(new[] { 1, 2, 3 }, released.Select(u => u.Id.Value));
        Assert.All(released, u => Assert.Equal(units[0].EgressExit, u.Field.Position));
        Assert.All(turns, t => Assert.InRange(t.Deployments.Count, 0, 1));
        Assert.All(turns.SelectMany(t => t.Movement.Ticks), tick =>
            Assert.Equal(tick.Units.Count, tick.Units.Select(u => u.Position).Distinct().Count()));
    }

    [Fact]
    public void 출격당일입성하여_이동결과에서사라져도_출격기록과재생이보존된다()
    {
        var destination = new City(new CityId(2), "도착", new HexCoord(3, 4), Changan.Owner, 1000);
        var unit = Unit(1, DeploymentDirection.SouthEast) with
        {
            Field = Unit(1, DeploymentDirection.SouthEast).Field with { Target = destination.Position, ReturnCity = destination.Id },
        };
        var cities = new[] { Changan, destination };
        Engine(cities).AdvanceWeek(new GameState(1, 1, [], cities, [], FieldArmies: [unit]), out var turns);
        var first = turns[0];
        Assert.Empty(first.Movement.Units);
        Assert.Single(first.EnteredCastle);
        var release = Assert.Single(first.Deployments);
        var playback = new MovementPlayback(new() { [1] = Changan.Position });
        playback.AppendDeployment(1, 1, release.Field.Position, 2.5, 0.5);
        playback.Append(first.Movement, 0, 2.5, 0.5);
        Assert.Equal(unit.EgressExit, playback.Moves[0].To);
        Assert.Equal(playback.Moves[^1].Time + 0.55, playback.Entries[1], 8);
    }
}
