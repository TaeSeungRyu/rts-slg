namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public class AiWarCandidatePolicyTests
{
    private static readonly FactionId Us = new(1);
    private static readonly FactionId Them = new(2);
    private static readonly BalanceConfig Balance = new(MonthlyTaxPerCity: 100);
    private static readonly HexMap Map = new(-3, 45, -5, 5);

    private static City City(int id, int owner, int q, int wall = 1500, int security = 70,
        PortSize port = PortSize.None) => new(new CityId(id), $"city-{id}",
        new HexCoord(q, 0), new FactionId(owner), 1000,
        Wall: wall, Security: security, Port: port, WallLevel: 4);

    private static GameState State(City source, params City[] targets)
        => new(1, 190, [], [source, .. targets], [],
            ScoutedCities: targets.Select(target => new CityIntel(Us, target.Id)).ToList());

    [Fact]
    public void 출발지의_성벽_50퍼센트와_치안_70이_모두_필요하다()
    {
        var source = City(1, 1, 0);
        var target = City(2, 2, 6);
        Assert.Single(AiWarCandidatePolicy.Evaluate(State(source, target), Us, Map, Balance, 1));
        Assert.Empty(AiWarCandidatePolicy.Evaluate(State(source with { Wall = 1499 }, target),
            Us, Map, Balance, 1));
        Assert.Empty(AiWarCandidatePolicy.Evaluate(State(source with { Security = 69 }, target),
            Us, Map, Balance, 1));
    }

    [Fact]
    public void 정찰하지_않은_적과_동맹은_공격후보가_아니다()
    {
        var source = City(1, 1, 0);
        var target = City(2, 2, 6);
        var hidden = State(source, target) with { ScoutedCities = [] };
        Assert.Empty(AiWarCandidatePolicy.Evaluate(hidden, Us, Map, Balance, 1));
        var ally = State(source, target) with
        { FactionAlliances = [new FactionAlliance(Us, Them, 100)] };
        Assert.Empty(AiWarCandidatePolicy.Evaluate(ally, Us, Map, Balance, 1));
    }

    [Fact]
    public void 병력이_없는_목표를_먼저_선택하되_육로_항구에는_선박이_필요없다()
    {
        var source = City(1, 1, 0);
        var guarded = City(2, 2, 5);
        var emptyPort = City(3, 2, 9, port: PortSize.Small);
        var state = State(source, guarded, emptyPort) with
        { GarrisonForces = [new GarrisonForce(guarded.Id, "swordsman", 3000, 50)] };

        var candidates = AiWarCandidatePolicy.Evaluate(state, Us, Map, Balance, 1);

        Assert.Contains(candidates, candidate => candidate.Target == emptyPort.Id
            && candidate.Domain == MovementDomain.Land && candidate.EmptyGarrison);
        var choice = AiWarCandidatePolicy.ChooseTargets(candidates, 1, Us, source.Id);
        Assert.Equal(emptyPort.Id, choice[0].Target);
    }

    [Fact]
    public void 경로의_예상_이동일이_30일을_넘으면_후보에서_제외한다()
    {
        var source = City(1, 1, 0);
        var near = City(2, 2, 30);
        var far = City(3, 2, 40);
        var candidates = AiWarCandidatePolicy.Evaluate(State(source, near, far),
            Us, Map, Balance, 1);
        Assert.Contains(candidates, candidate => candidate.Target == near.Id
            && candidate.TravelDays <= 30);
        Assert.DoesNotContain(candidates, candidate => candidate.Target == far.Id);
    }
}
