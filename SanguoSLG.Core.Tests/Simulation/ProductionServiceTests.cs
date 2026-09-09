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

    private static City City() => new(new CityId(1), "생산성", new HexCoord(0, 0), new FactionId(1), 1000,
        Villages: 1);

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

    private static CombatUnit EnemyAttacker() => new(
        new FieldUnit(new UnitId(99), new FactionId(2), new HexCoord(2, 0),
            Speed: 2, Detection: 2, AttackRange: 1, MovementDomain.Land, UnitMode.Attack,
            Target: new HexCoord(2, 0), CommandOrder: 0),
        new CombatStats(Troops: 1000, AtkStat: 10, DfStat: 10),
        new TroopPool(Active: 1000, Wounded: 0),
        UnitCombatState.Create(50));

    [Theory]
    [InlineData(59, 14)]
    [InlineData(60, 12)]
    [InlineData(80, 10)]
    [InlineData(100, 8)]
    public void 생산_기간은_정치에_따라_단축된다(int politics, int days)
    {
        Assert.Equal(days, ProductionRules.GatherDays(politics));
    }

    [Theory]
    [InlineData(ProductionPhase.Outbound)]
    [InlineData(ProductionPhase.Gathering)]
    [InlineData(ProductionPhase.Returning)]
    public void 생산_모든단계에서_새명령과_담당_중복을_금지한다(ProductionPhase phase)
    {
        var result = new ProductionService(Troops).Start(State(), new CityId(1), new HexCoord(2, 0),
            ProductionRules.Village, "swordsman", new GeneralId(1));
        Assert.True(result.Ok, result.Error);
        var state = result.State with { ProductionOperations = [result.State.ProductionOps.Single() with { Phase = phase }] };
        Assert.True(state.IsGeneralBusy(new GeneralId(1)));
        var service = new CommandService(new CommandBalance(), Troops);
        Assert.False(service.Issue(state, new CommandRequest(new CityId(1), CommandKind.AppointDomesticOfficer, new GeneralId(1))).Ok);
        Assert.False(service.Issue(state, new CommandRequest(new CityId(1), CommandKind.Explore, new GeneralId(1))).Ok);
    }

    [Fact]
    public void 생산_작전은_병력오백과_장수를_도시에서_뺀다()
    {
        var state = State();
        state = state with { Cities = [state.Cities.Single() with { DomesticOfficer = new GeneralId(1) }] };

        var result = new ProductionService(Troops)
            .Start(state, new CityId(1), new HexCoord(2, 0), ProductionRules.Village, "swordsman", new GeneralId(1));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(500, result.State.Garrisons.Single().Troops);
        Assert.Null(result.State.Cities.Single().DomesticOfficer);
        Assert.True(result.State.IsGeneralBusy(new GeneralId(1)));
        Assert.Null(result.State.PostingOf(new GeneralId(1))!.Location);
        var op = result.State.ProductionOps.Single();
        Assert.Equal(ProductionPhase.Outbound, op.Phase);
        Assert.Equal(ProductionOperation.FixedTroops, op.Troops);
        Assert.Equal(50, op.TrainingLevel);
        Assert.True(op.Speed > 0);
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

    [Fact]
    public void 생산_작전은_도착_채집_복귀후_보상을_지급한다()
    {
        var started = new ProductionService(Troops)
            .Start(State(politics: 100), new CityId(1), new HexCoord(2, 0), ProductionRules.Village, "swordsman", new GeneralId(1))
            .State;

        var after = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0))
            .AdvanceDays(started, 20);

        Assert.Empty(after.ProductionOps);
        Assert.Equal(1000, after.Garrisons.Single(g => g.TroopCode == "swordsman").Troops);
        Assert.Equal(new CityId(1), after.PostingOf(new GeneralId(1))!.Location);
        Assert.Equal(500, after.Cities.Single().Gold);
    }

    [Fact]
    public void 생산_작전은_시설_수량_변화만으로_소실되지_않는다()
    {
        var started = new ProductionService(Troops)
            .Start(State(politics: 100), new CityId(1), new HexCoord(2, 0), ProductionRules.Village, "swordsman", new GeneralId(1))
            .State;
        var damaged = started with { Cities = started.Cities.Select(c => c with { Villages = 0 }).ToList() };

        var after = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0))
            .AdvanceDays(damaged, 20);

        Assert.Empty(after.ProductionOps);
        Assert.Equal(1000, after.Garrisons.Single(g => g.TroopCode == "swordsman").Troops);
        Assert.Equal(new CityId(1), after.PostingOf(new GeneralId(1))!.Location);
    }

    [Fact]
    public void 생산_부대는_채집중_공격받으면_투입병력만_소실된다()
    {
        var started = new ProductionService(Troops)
            .Start(State(politics: 100), new CityId(1), new HexCoord(2, 0), ProductionRules.Village, "swordsman", new GeneralId(1))
            .State;
        var gathering = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0))
            .AdvanceDays(started, 1);
        Assert.Equal(ProductionPhase.Gathering, gathering.ProductionOps.Single().Phase);

        var attacked = gathering with { FieldArmies = new List<CombatUnit> { EnemyAttacker() } };
        var after = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0))
            .AdvanceDays(attacked, 1);

        Assert.Empty(after.ProductionOps);
        Assert.Equal(500, after.Garrisons.Single().Troops);
        Assert.Equal(1000, after.Armies.Single().Pool.Active);
    }
}
