namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class ProductionServiceTests
{
    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    private static City City() => new(new CityId(1), "생산성", new HexCoord(0, 0), new FactionId(1), 1000);

    private static General General(int politics = 80) => new(
        new GeneralId(1), "생산장", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: 70, Intellect: 50, Politics: politics);

    private static GameState State(int troops = 1000, int politics = 80) => new(
        1, 1, new List<Faction>(), new List<City> { City() }, new List<General> { General(politics) },
        Postings: new List<GeneralPosting> { new(new GeneralId(1), new FactionId(1), new CityId(1)) },
        GarrisonForces: new List<GarrisonForce> { new(new CityId(1), "swordsman", troops, 50) },
        FacilityPlacements: new List<FacilityPlacement>
        {
            new(new CityId(1), new HexCoord(2, 0), ProductionRules.Village),
        });

    [Theory]
    [InlineData(59, 14)]
    [InlineData(60, 12)]
    [InlineData(80, 10)]
    [InlineData(100, 8)]
    public void 생산_기간은_정치에_따라_단축된다(int politics, int days)
    {
        Assert.Equal(days, ProductionRules.GatherDays(politics));
    }

    [Fact]
    public void 생산_작전은_병력오백과_장수를_도시에서_뺀다()
    {
        var state = State();

        var result = new ProductionService(Troops)
            .Start(state, new CityId(1), new HexCoord(2, 0), ProductionRules.Village, "swordsman", new GeneralId(1));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(500, result.State.Garrisons.Single().Troops);
        Assert.Null(result.State.PostingOf(new GeneralId(1))!.Location);
        var op = result.State.ProductionOps.Single();
        Assert.Equal(ProductionPhase.Outbound, op.Phase);
        Assert.Equal(ProductionOperation.FixedTroops, op.Troops);
        Assert.Equal(new HexCoord(2, 0), op.Target);
        Assert.Equal(10, op.GatherDays);
        Assert.True(op.OutPath.Count >= 2);
    }

    [Fact]
    public void 생산_작전은_오백명_미만이면_실패한다()
    {
        var result = new ProductionService(Troops)
            .Start(State(troops: 499), new CityId(1), new HexCoord(2, 0), ProductionRules.Village, "swordsman", new GeneralId(1));

        Assert.False(result.Ok);
        Assert.Contains("500", result.Error);
    }
}
