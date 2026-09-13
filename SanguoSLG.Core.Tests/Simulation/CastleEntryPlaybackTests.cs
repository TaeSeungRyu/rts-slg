using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using SanguoSLG.Game;

namespace SanguoSLG.Core.Tests.Simulation;

public class CastleEntryPlaybackTests
{
    private static MovementSimulator Simulator() => new(new PassabilityMap(new HexMap(-10, 30, -10, 10), [], []));
    private static FieldUnit Unit(int id, int distance, int speed, UnitMode mode = UnitMode.March) =>
        new(new UnitId(id), new FactionId(1), new HexCoord(0, 0), speed, 0, 1,
            MovementDomain.Land, mode, new HexCoord(distance, 0), 0);

    [Theory]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(4, 3)]
    [InlineData(8, 1)]
    [InlineData(8, 3)]
    public void 마지막이동을빠뜨리지않고_도착일의트윈완료직후_한번만제거한다(int distance, int speed)
    {
        var unit = Unit(1, distance, speed);
        var result = Simulator().Advance([unit], 7, [new(unit.Target!.Value, unit.Owner)]);
        var playback = new MovementPlayback(new() { [1] = unit.Position });
        playback.Append(result, 0, 2.5, 0.5);

        Assert.Empty(result.Units);
        Assert.Single(result.EnteredCastle);
        Assert.Single(playback.Entries);
        Assert.Equal(distance - 1, playback.Moves.Count);
        var previous = unit.Position;
        foreach (var move in playback.Moves)
        {
            Assert.Equal(1, previous.Distance(move.To));
            previous = move.To;
        }
        Assert.Equal(1, previous.Distance(unit.Target.Value));
        Assert.Equal(playback.Moves[^1].Time + 0.55, playback.Entries[1], 8);
        var day = (distance - 2) / speed + 1;
        Assert.InRange(playback.Entries[1], (day - 1) * 2.5, (day - 1) * 2.5 + 1.55);
        Assert.Single(result.Ticks.SelectMany(t => t.EnteredUnits));
    }

    [Fact]
    public void 다른부대가7일동안이동해도_입성부대는2일차에사라진다()
    {
        var a = Unit(1, 4, 2);
        var b = Unit(2, 25, 1) with { Position = new HexCoord(0, 4), Target = new HexCoord(25, 4) };
        var result = Simulator().Advance([a, b], 7, [new(a.Target!.Value, a.Owner)]);
        var playback = new MovementPlayback(new() { [1] = a.Position, [2] = b.Position });
        playback.Append(result, 0, 2.5, 0.5);
        Assert.Equal(7, result.Days);
        Assert.Equal(3.05, playback.Entries[1], 8);
        Assert.False(playback.Entries.ContainsKey(2));
        Assert.Equal(3, playback.Moves.Count(m => m.UnitId == 1));
    }

    [Fact]
    public void 진행조각이분리돼도_입성시각과마지막이동이유지된다()
    {
        var a = Unit(1, 4, 2);
        SiegeSite[] castles = [new(a.Target!.Value, a.Owner)];
        var first = Simulator().Advance([a], 1, castles);
        Assert.Empty(first.EnteredCastle);
        Assert.Equal(new HexCoord(2, 0), Assert.Single(first.Units).Position);
        var second = Simulator().Advance(first.Units, 1, castles);
        var playback = new MovementPlayback(new() { [1] = a.Position });
        playback.Append(first, 0, 2.5, 0.5);
        playback.Append(second, 1, 2.5, 0.5);
        Assert.Equal(3, playback.Moves.Count);
        Assert.Equal(3.05, playback.Entries[1], 8);
    }

    [Theory]
    [InlineData(UnitMode.March)]
    [InlineData(UnitMode.Advance)]
    [InlineData(UnitMode.Attack)]
    public void 모든이동모드가같은입성기록을사용한다(UnitMode mode)
    {
        var a = Unit(1, 4, 2, mode);
        var result = Simulator().Advance([a], 7, [new(a.Target!.Value, a.Owner)]);
        Assert.Equal(new HexCoord(3, 0), Assert.Single(result.Ticks.SelectMany(t => t.EnteredUnits)).Position);
    }

    [Fact]
    public void 적성이나목표미지정은입성하지않는다()
    {
        var a = Unit(1, 4, 2);
        Assert.Empty(Simulator().Advance([a], 7, [new(a.Target!.Value, new FactionId(2))]).EnteredCastle);
        Assert.Empty(Simulator().Advance([a with { Target = null }], 7, [new(new HexCoord(1, 0), a.Owner)]).EnteredCastle);
    }

    [Fact]
    public void 성옆에서다른경유지로이동하거나행동불가이면조기입성하지않는다()
    {
        var a = Unit(1, 1, 1) with { Waypoints = [new HexCoord(0, 5)] };
        SiegeSite[] castles = [new(a.Target!.Value, a.Owner)];
        Assert.Empty(Simulator().Advance([a], 1, castles).EnteredCastle);
        Assert.Empty(Simulator().Advance([a with { Waypoints = null, Speed = 0 }], 1, castles).EnteredCastle);
    }
}
