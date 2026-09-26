namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public sealed class FactionTroopUnlockTests
{
    private static readonly FactionId Faction = new(1);
    private static GameState State(IReadOnlyList<FactionResearch>? research = null)
        => new(1, 190, [], [], [], ResearchTracks: research);

    [Theory]
    [InlineData("swordsman")]
    [InlineData("archer")]
    [InlineData("cavalry")]
    [InlineData("thunder_cart")]
    public void BasicTroopsAreAvailable(string code)
        => Assert.True(FactionTroopUnlock.Check(State(), Faction, code).Allowed);

    [Fact]
    public void SiegeUnitsUnlockAtThunderCartLevelFive()
    {
        Assert.False(FactionTroopUnlock.Check(State([new(Faction, "thunder_cart", 4)]), Faction, "catapult").Allowed);
        Assert.True(FactionTroopUnlock.Check(State([new(Faction, "thunder_cart", 5)]), Faction, "catapult").Allowed);
        Assert.True(FactionTroopUnlock.Check(State([new(Faction, "thunder_cart", 5)]), Faction, "siege_tower").Allowed);
    }

    [Fact]
    public void SpecialTroopExplainsRequiredRuin()
    {
        var result = FactionTroopUnlock.Check(State(), Faction, "geukbyeong");
        Assert.False(result.Allowed);
        Assert.Contains("극병 유적", result.Reason);
    }

    [Fact]
    public void RegisteredFactionCanProduceSpecialTroop()
    {
        var state = State() with
        {
            RuinDefinitions = [new("r1", "극병 유적", new(1, 1), "geukbyeong", 30_000)],
            RuinStates = [new("r1", 0, Faction, 1, 31, [Faction])],
        };
        Assert.True(FactionTroopUnlock.Check(state, Faction, "geukbyeong").Allowed);
        Assert.False(FactionTroopUnlock.Check(state, new FactionId(2), "geukbyeong").Allowed);
    }
}
