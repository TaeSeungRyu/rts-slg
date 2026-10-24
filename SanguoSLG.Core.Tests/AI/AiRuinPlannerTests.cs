namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed class AiRuinPlannerTests
{
    private static readonly FactionId Faction = new(1);
    private static readonly HexMap Map = new(-2, 9, -3, 4);
    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());
    private static readonly DeployService Deployer = new(new CommandBalance(), Troops,
        new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
        new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()));
    private static readonly BalanceConfig Balance = new(MonthlyTaxPerCity: 100);

    private static GameState State(int troops)
    {
        var city = new City(new CityId(1), "출발성", new HexCoord(0, 0), Faction,
            20_000, Gold: 5000, Wall: 1200);
        var generals = Enumerable.Range(1, 3).Select(id => new General(new GeneralId(id), $"장수{id}",
            new Dictionary<TroopClass, AptitudeGrade> { [TroopClass.Infantry] = AptitudeGrade.A },
            80, 60, 60)).ToArray();
        return new GameState(1, 190, [new Faction(Faction, "위", new GeneralId(1), 0, "blue")],
            [city], generals,
            Postings: generals.Select(general => new GeneralPosting(general.Id, Faction, city.Id)).ToArray(),
            GarrisonForces: [new GarrisonForce(city.Id, "swordsman", troops, 60)],
            RuinDefinitions: [new RuinDefinition("ruin", "극병 유적", new HexCoord(5, 0),
                "geukbyeong", 15_000)],
            RuinStates: [new RuinState("ruin", 15_000)]);
    }

    [Fact]
    public void 유적은_잔류병력을_뺀_실제_가용병력이_2배일때만_출전한다()
    {
        var planner = new AiRuinPlanner(Deployer, Map, Balance);
        var tooFew = planner.Plan(State(37_499), Faction);
        var enough = planner.Plan(State(37_500), Faction);

        Assert.Empty(tooFew.Armies);
        Assert.NotEmpty(enough.Armies);
        Assert.Equal(30_000, enough.Armies.Sum(unit => unit.Pool.Active));
        Assert.All(enough.Armies, unit => Assert.Equal("ruin", unit.AiRuinTarget));
        Assert.Equal(37_500 - 30_000, enough.Garrisons.Sum(line => line.Troops));
    }

    [Fact]
    public void 유적_편성은_부대가_부족하면_원본_상태를_유지한다()
    {
        var planner = new AiRuinPlanner(Deployer, Map, Balance);
        var state = State(40_000) with { Generals = State(40_000).Generals.Take(1).ToArray(),
            Postings = State(40_000).Assignments.Take(1).ToArray() };

        var result = planner.Plan(state, Faction);

        Assert.Empty(result.Armies);
        Assert.Equal(40_000, result.Garrisons.Sum(line => line.Troops));
    }

    [Fact]
    public void 점령된_유적_부대는_원성으로_복귀하고_임무를_정리한다()
    {
        var planner = new AiRuinPlanner(Deployer, Map, Balance);
        var deployed = planner.Plan(State(40_000), Faction);
        var captured = deployed with { RuinStates = [new RuinState("ruin", 0,
            Owner: Faction, RegisteredFactions: [Faction])] };

        var result = AiRuinPlanner.Maintain(captured, Faction);

        Assert.All(result.Armies, unit =>
        {
            Assert.Null(unit.AiRuinTarget);
            Assert.Equal(UnitMode.March, unit.Field.Mode);
            Assert.Equal(new CityId(1), unit.Field.ReturnCity);
        });
    }
}
