namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public class AiWarForcePolicyTests
{
    private static City City(int id, int owner, int q) => new(new CityId(id), $"c{id}",
        new HexCoord(q, 0), new FactionId(owner), 1000, Wall: 1500, WallLevel: 4);

    private static GameState State(int primary, int support, int defenders)
        => new(1, 190, [], [City(1, 1, 0), City(2, 1, 4), City(3, 2, 10)], [],
            GarrisonForces:
            [
                new GarrisonForce(new CityId(1), "swordsman", primary, 50),
                new GarrisonForce(new CityId(2), "swordsman", support, 50),
                new GarrisonForce(new CityId(3), "swordsman", defenders, 50),
            ]);

    private static readonly AiWarCandidate[] Routes =
    [
        new(new CityId(1), new CityId(3), 10, 10, MovementDomain.Land,
            new HexCoord(1, 0), false),
        new(new CityId(2), new CityId(3), 6, 6, MovementDomain.Land,
            new HexCoord(5, 0), false),
    ];

    [Fact]
    public void 공격_가능_병력은_원래_병력의_20퍼센트를_남긴다()
    {
        var state = State(50_000, 0, 30_000);
        Assert.Equal(40_000, AiWarForcePolicy.Available(state, state.Cities[0]));
    }

    [Fact]
    public void 단독으로_1점2배_미달이면_자격있는_지원성_하나만_합산한다()
    {
        var state = State(30_000, 30_000, 40_000);
        var result = AiWarForcePolicy.Evaluate(state, state.Cities[0], state.Cities[2],
            Routes, new BalanceConfig(MonthlyTaxPerCity: 100));
        Assert.Equal(new CityId(2), result.Support);
        Assert.Equal(24_000, result.OriginAvailable);
        Assert.Equal(24_000, result.SupportAvailable);
        Assert.True(result.CanAttack);
        Assert.Equal(12, result.RequiredRatioTenths);
    }

    [Fact]
    public void 도착_차이가_21일이면_지원성으로_사용하지_않는다()
    {
        var state = State(30_000, 30_000, 40_000);
        var routes = Routes.ToArray();
        routes[1] = routes[1] with { TravelDays = 31 };
        var result = AiWarForcePolicy.Evaluate(state, state.Cities[0], state.Cities[2],
            routes, new BalanceConfig(MonthlyTaxPerCity: 100));
        Assert.Null(result.Support);
        Assert.False(result.CanAttack);
    }

    [Fact]
    public void 빈_성도_최소_병력으로_공격후보가_된다()
    {
        var state = State(10_000, 0, 0);
        var result = AiWarForcePolicy.Evaluate(state, state.Cities[0], state.Cities[2],
            Routes, new BalanceConfig(MonthlyTaxPerCity: 100));
        Assert.True(result.CanAttack);
        Assert.Equal(1, result.PlannedTroops);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(10, 2)]
    [InlineData(11, 3)]
    public void 보유_거점_수에_따라_동시공세_상한이_달라진다(int cities, int expected)
        => Assert.Equal(expected, AiWarForcePolicy.ConcurrentTargetLimit(cities));
}
