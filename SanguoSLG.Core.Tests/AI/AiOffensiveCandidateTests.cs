namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public class AiOffensiveCandidateTests
{
    private static City City(int id, int owner, int q)
        => new(new CityId(id), $"city-{id}", new HexCoord(q, 0), new FactionId(owner), 1000);

    private static GameState State(int attacker, int defender, bool trainee = false)
        => new(1, 190, [], [City(1, 1, 0), City(2, 2, 5)], [],
            GarrisonForces:
            [
                new GarrisonForce(new CityId(1), "swordsman", attacker, 50),
                new GarrisonForce(new CityId(1), "archer", trainee ? 30_000 : 0, 50, trainee),
                new GarrisonForce(new CityId(2), "swordsman", defender, 50),
            ]);

    [Theory]
    [InlineData(30_000, 30_000, false)]
    [InlineData(50_000, 30_000, true)]
    [InlineData(50_000, 50_000, false)]
    [InlineData(100_000, 50_000, true)]
    [InlineData(100_000, 100_000, false)]
    public void 수비_예비와_적_주둔병력으로_공세_후보를_거른다(int own, int enemy, bool expected)
    {
        var state = State(own, enemy);
        var assessment = AiOffensiveCandidateService.Evaluate(state, state.Cities[0], state.Cities[1]);
        Assert.Equal(expected, assessment.PassesForceGate);
        Assert.Equal((int)Math.Ceiling(own * 0.20), assessment.ReserveTroops);
        Assert.Equal(5, assessment.Distance);
    }

    [Fact]
    public void 훈련중_병력은_공세_가용병력에_포함하지_않는다()
    {
        var state = State(30_000, 30_000, trainee: true);
        Assert.False(AiOffensiveCandidateService.Evaluate(state, state.Cities[0], state.Cities[1]).PassesForceGate);
    }

    [Fact]
    public void 같은_세력의_성은_공격하지_않는다()
    {
        var state = State(100_000, 1_000);
        var friendly = state.Cities[1] with { Owner = new FactionId(1) };
        Assert.False(AiOffensiveCandidateService.Evaluate(state, state.Cities[0], friendly).PassesForceGate);
    }
}
