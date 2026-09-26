namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed class RuinCombatTests
{
    private static CombatUnit Attacker(int range = 1) => new(
        new FieldUnit(new UnitId(1), new FactionId(1), new HexCoord(1, 1), 2, 2, range,
            MovementDomain.Land, UnitMode.Attack, new HexCoord(2, 1), 1, 1),
        new CombatStats(10_000, 12, 10), new TroopPool(10_000, 0), UnitCombatState.Create(0));

    private static GameState State(int defenders = 30_000) => new(1, 190, [], [], [],
        RuinDefinitions: [new("r1", "극병 유적", new HexCoord(2, 1), "geukbyeong", 30_000)],
        RuinStates: [new("r1", defenders)]);

    [Fact]
    public void AttackedRuinAlwaysCounterattacks()
    {
        var result = new RuinCombat(new BattleResolver(60)).Resolve(State(), [Attacker()]);
        var exchange = Assert.Single(result.Exchanges);
        Assert.True(exchange.DamageToRuin > 0);
        Assert.True(exchange.CounterDamage > 0);
        Assert.True(result.Armies.Single().Pool.Active < 10_000);
    }

    [Fact]
    public void RuinNeverInitiatesAgainstUntargetingUnit()
    {
        var idle = Attacker() with { Field = Attacker().Field with { Target = new HexCoord(1, 2) } };
        var result = new RuinCombat(new BattleResolver(60)).Resolve(State(), [idle]);
        Assert.Empty(result.Exchanges);
        Assert.Equal(10_000, result.Armies.Single().Pool.Active);
    }

    [Fact]
    public void UnitThatArrivedOnRuinAttacksEvenWhenMovementClearedTarget()
    {
        var arrived = Attacker() with
        {
            Field = Attacker().Field with { Position = new HexCoord(2, 1), Target = null },
        };
        var result = new RuinCombat(new BattleResolver(60)).Resolve(State(), [arrived]);
        Assert.Single(result.Exchanges);
        Assert.True(result.State.RuinStatus.Single().Defenders < 30_000);
    }

    [Fact]
    public void LastDamagingFactionCapturesAndGetsThirtyDayProtection()
    {
        var result = new RuinCombat(new BattleResolver(60)).Resolve(State(1), [Attacker()]);
        var ruin = Assert.Single(result.State.RuinStatus);
        Assert.Equal(new FactionId(1), ruin.Owner);
        Assert.Equal(31, ruin.ProtectedUntilDay);
        Assert.Equal(new FactionId(1), ruin.Registrations.Single());
    }

    [Fact]
    public void DefenderRegeneratesOnProtectionBoundary()
    {
        var state = State() with
        {
            Day = 31,
            RuinStates = [new("r1", 0, new FactionId(1), 1, 31, [new FactionId(1)])],
        };
        var result = new RuinCombat(new BattleResolver(60)).Resolve(state, []);
        var ruin = Assert.Single(result.State.RuinStatus);
        Assert.Equal(30_000, ruin.Defenders);
        Assert.Null(ruin.ProtectedUntilDay);
        Assert.Equal(new FactionId(1), ruin.Owner);
    }
}
