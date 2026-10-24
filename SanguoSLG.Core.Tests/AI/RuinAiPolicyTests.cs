namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed class RuinAiPolicyTests
{
    private static readonly RuinDefinition Definition = new("r", "극병 유적", new HexCoord(1, 1), "geukbyeong", 30_000);
    private static readonly FactionId Faction = new(1);

    [Fact]
    public void AiAttacksOnlyWithEnoughStrengthAndWithoutRegistration()
    {
        var neutral = new RuinState("r", 30_000);
        Assert.False(RuinAiPolicy.ShouldAttack(Definition, neutral, Faction, 1, 59_999));
        Assert.True(RuinAiPolicy.ShouldAttack(Definition, neutral, Faction, 1, 60_000));
        Assert.False(RuinAiPolicy.ShouldAttack(Definition,
            neutral with { RegisteredFactions = [Faction] }, Faction, 1, 50_000));
        Assert.False(RuinAiPolicy.ShouldAttack(Definition,
            neutral with { ProtectedUntilDay = 31 }, Faction, 1, 50_000));
    }

    [Fact]
    public void 유적_판단은_전쟁_임무가_없을_때만_시작한다()
    {
        Assert.True(RuinAiPolicy.CanStart(false, false, false, false));
        Assert.False(RuinAiPolicy.CanStart(true, false, false, false));
        Assert.False(RuinAiPolicy.CanStart(false, true, false, false));
        Assert.False(RuinAiPolicy.CanStart(false, false, true, false));
        Assert.False(RuinAiPolicy.CanStart(false, false, false, true));
    }

    [Fact]
    public void 유적_후보는_성별로_약한_수비대와_실제_경로를_우선한다()
    {
        var city = new City(new CityId(1), "아군성", new HexCoord(0, 0), Faction,
            1000, Wall: 1000);
        var ruins = new[]
        {
            new RuinDefinition("strong", "강한 유적", new HexCoord(2, 2), "geukbyeong", 30_000),
            new RuinDefinition("weak", "약한 유적", new HexCoord(4, 0), "geukbyeong", 15_000),
            new RuinDefinition("registered", "등록 유적", new HexCoord(5, 0), "geukbyeong", 5_000),
        };
        var state = new GameState(1, 190, [], [city], [], RuinDefinitions: ruins,
            RuinStates:
            [
                new RuinState("strong", 30_000), new RuinState("weak", 15_000),
                new RuinState("registered", 5_000, RegisteredFactions: [Faction]),
            ]);

        var candidates = RuinAiPolicy.Candidates(state, Faction, new HexMap(-2, 7, -2, 5),
            new BalanceConfig(MonthlyTaxPerCity: 100));

        Assert.Equal(["weak", "strong"], candidates.Select(candidate => candidate.RuinId));
    }
}
