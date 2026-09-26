namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public sealed class RuinAiPolicyTests
{
    private static readonly RuinDefinition Definition = new("r", "극병 유적", new HexCoord(1, 1), "geukbyeong", 30_000);
    private static readonly FactionId Faction = new(1);

    [Fact]
    public void AiAttacksOnlyWithEnoughStrengthAndWithoutRegistration()
    {
        var neutral = new RuinState("r", 30_000);
        Assert.False(RuinAiPolicy.ShouldAttack(Definition, neutral, Faction, 1, 29_999));
        Assert.True(RuinAiPolicy.ShouldAttack(Definition, neutral, Faction, 1, 30_000));
        Assert.False(RuinAiPolicy.ShouldAttack(Definition,
            neutral with { RegisteredFactions = [Faction] }, Faction, 1, 50_000));
        Assert.False(RuinAiPolicy.ShouldAttack(Definition,
            neutral with { ProtectedUntilDay = 31 }, Faction, 1, 50_000));
    }
}
