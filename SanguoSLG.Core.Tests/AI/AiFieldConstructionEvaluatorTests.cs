namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class AiFieldConstructionEvaluatorTests
{
    private static readonly IReadOnlyList<FieldBuildingDefinition> Definitions =
        new FieldBuildingLoader().LoadFromDirectory(TestData.DataDirectory());
    private static readonly HexMap Map = new(-2, 12, -4, 4);

    [Fact]
    public void AI는_시야에_들어온_접근부대가_있으면_방어건축을_평가한다()
    {
        var state = State();
        var visible = new HashSet<HexCoord> { new(6, 0) };

        var candidates = AiFieldConstructionEvaluator.Evaluate(state, new(1), Map,
            Definitions, visible);

        Assert.Contains(candidates, candidate => candidate.DefinitionCode == "palisade"
            && candidate.Unit == new UnitId(1));
        Assert.Contains(candidates, candidate => candidate.DefinitionCode == "formation");
        Assert.Equal(candidates.OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.DefinitionCode, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Position.Q).ThenBy(candidate => candidate.Position.R)
            .ThenBy(candidate => candidate.Unit.Value), candidates);
    }

    [Fact]
    public void AI는_안개속_적을_근거로_건축하지_않는다()
    {
        var candidates = AiFieldConstructionEvaluator.Evaluate(State(), new(1), Map,
            Definitions, new HashSet<HexCoord>());

        Assert.Empty(candidates);
    }

    [Fact]
    public void AI는_정찰된_전선에서_보루와_감시탑과_정찰대를_후보로_삼는다()
    {
        var state = State() with
        {
            Cities = [State().Cities[0], new City(new(2), "적성", new(8, 0), new(2), 3000)],
            ScoutedCities = [new CityIntel(new(1), new(2), 100)],
        };
        var forestMap = new HexMap(-2, 12, -4, 4,
            new Dictionary<HexCoord, TerrainType> { [new(3, 0)] = TerrainType.Forest });

        var candidates = AiFieldConstructionEvaluator.Evaluate(state, new(1), forestMap,
            Definitions, new HashSet<HexCoord>());

        Assert.Contains(candidates, candidate => candidate.DefinitionCode == "fort");
        Assert.Contains(candidates, candidate => candidate.DefinitionCode == "watchtower");
        Assert.Contains(candidates, candidate => candidate.DefinitionCode == "scout_post");
    }

    [Fact]
    public void AI는_병력_금_치안_공사상한_조건을_지킨다()
    {
        var visible = new HashSet<HexCoord> { new(6, 0) };
        var state = State();
        Assert.Empty(Evaluate(state with { FieldArmies = state.Armies.Select(unit =>
            unit.Id == new UnitId(1) ? unit with { Pool = new TroopPool(999, 0) } : unit).ToList() }));
        Assert.Empty(Evaluate(state with { FieldArmies = state.Armies.Select(unit =>
            unit.Id == new UnitId(1) ? unit with { CargoGold = 0 } : unit).ToList() }));
        Assert.Empty(Evaluate(state with { Cities = state.Cities.Select(city =>
            city with { Security = 69 }).ToList() }));
        Assert.Empty(Evaluate(state with { Cities = state.Cities.Select(city =>
            city with { Wall = 299 }).ToList() }));
        Assert.NotEmpty(Evaluate(state with { Cities = state.Cities.Select(city =>
            city with { Wall = 300 }).ToList() }));
        var sites = new[] { 10, 11 }.Select(id => new FieldBuilding(new(id), "watchtower",
            new(1), new(id, 3), 750, 1, 14)).ToList();
        Assert.Empty(Evaluate(state with { FieldBuildings = sites }));

        IReadOnlyList<AiConstructionCandidate> Evaluate(GameState value) =>
            AiFieldConstructionEvaluator.Evaluate(value, new(1), Map, Definitions, visible);
    }

    [Fact]
    public void AI는_주간판단에서_기존서비스로_실제착공하고_중복착공하지_않는다()
    {
        var original = State();
        var builder = original.Armies.Single(unit => unit.Id == new UnitId(1));
        var state = original with { FieldArmies = original.Armies.Select(unit =>
            unit.Id == builder.Id ? unit with { Field = unit.Field with { Detection = 4 } } : unit).ToList() };
        var troops = new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());
        var balance = new CommandBalance();
        var ai = new FactionAI(new CommandService(balance, troops),
            new DeployService(balance, troops,
                new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
                new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory())),
            new AiConfig(DeployTarget: int.MaxValue), map: Map,
            fieldBuildingDefinitions: Definitions);

        var after = ai.PlanWeek(state, new(1));
        var building = Assert.Single(after.Buildings);
        var paid = after.Armies.Single(unit => unit.Id == builder.Id);
        Assert.True(paid.IsConstructing);
        Assert.Equal(builder.CarryingGold - Definitions.Single(def => def.Code == building.DefinitionCode).GoldCost,
            paid.CarryingGold);
        Assert.Equal(state.Day + Definitions.Single(def => def.Code == building.DefinitionCode).BuildDays,
            building.CompletionDay);
        Assert.Single(ai.PlanWeek(after, new(1)).Buildings);
    }

    [Fact]
    public void AI는_안전한_전선성에서_건축부대를_한번만_출전시킨다()
    {
        var own = new City(new(1), "아군성", new(0, 0), new(1), 5000,
            Gold: 2000, Wall: 1000);
        var hostile = new City(new(2), "적성", new(8, 0), new(2), 5000,
            Gold: 2000, Wall: 1000);
        var officers = Enumerable.Range(1, 5).Select(id => new General(new(id), $"장수{id}",
            new Dictionary<TroopClass, AptitudeGrade> { [TroopClass.Infantry] = AptitudeGrade.A },
            Might: 70, Intellect: 60, Politics: 70)).ToList();
        var state = new GameState(1, 190, [], [own, hostile], officers,
            Postings: officers.Select(general => new GeneralPosting(general.Id, own.Owner, own.Id))
                .ToList(),
            GarrisonForces: [new GarrisonForce(own.Id, "swordsman", 20_000, 60)],
            ScoutedCities: [new CityIntel(own.Owner, hostile.Id, 100)]);
        var troops = new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());
        var balance = new CommandBalance();
        var ai = new FactionAI(new CommandService(balance, troops),
            new DeployService(balance, troops,
                new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
                new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory())),
            new AiConfig(DeployTarget: int.MaxValue, GeneralResearchReserveGold: -1,
                ExplorationReserveGold: int.MaxValue), map: Map,
            fieldBuildingDefinitions: Definitions);

        var after = ai.PlanWeek(state, own.Owner);

        var builder = Assert.Single(after.Armies);
        Assert.Equal(UnitMode.Standby, builder.Field.Mode);
        Assert.Equal(300, builder.CarryingGold);
        Assert.Equal(18_000, after.Garrisons.Single().Troops);
        Assert.Single(ai.PlanWeek(after with { Day = 8 }, own.Owner).Armies);
        var deployed = after with { Day = 8, FieldArmies = [builder with
        {
            Field = builder.Field with { Position = builder.EgressExit!.Value },
            AwaitingEgress = false,
            DeploymentDelayDays = 0,
        }] };
        var constructing = ai.PlanWeek(deployed, own.Owner);
        Assert.True(Assert.Single(constructing.Armies).IsConstructing);
        Assert.Single(constructing.Buildings);
    }

    [Fact]
    public void AI는_완공된_빈_보루로_대기부대를_행군시킨다()
    {
        var state = State() with
        {
            Day = 20,
            FieldBuildings = [new FieldBuilding(new(9), "fort", new(1),
                new(4, 0), 2500, 1, 15, BuilderUnit: new UnitId(1))],
        };
        var troops = new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());
        var balance = new CommandBalance();
        var ai = new FactionAI(new CommandService(balance, troops),
            new DeployService(balance, troops,
                new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
                new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory())),
            new AiConfig(DeployTarget: int.MaxValue), map: Map,
            fieldBuildingDefinitions: Definitions);

        var after = ai.PlanWeek(state, new(1));

        var unit = after.Armies.Single(candidate => candidate.Id == new UnitId(1));
        Assert.Equal(UnitMode.March, unit.Field.Mode);
        Assert.Equal(new HexCoord(4, 0), unit.Field.Target);
        Assert.False(unit.IsConstructing);
    }

    [Fact]
    public void AI는_감시탑이_완공되면_남은_건축부대를_귀환시킨다()
    {
        var original = State();
        var builder = original.Armies.Single(unit => unit.Id == new UnitId(1));
        var state = original with
        {
            Day = 20,
            FieldArmies = original.Armies.Select(unit => unit.Id == builder.Id
                ? unit with { OriginCity = new CityId(1),
                    Field = unit.Field with { Mode = UnitMode.Advance, Target = null } }
                : unit).ToList(),
            FieldBuildings = [new FieldBuilding(new(9), "watchtower", new(1),
                new(4, 0), 1500, 1, 8, BuilderUnit: builder.Id)],
        };
        var troops = new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());
        var balance = new CommandBalance();
        var ai = new FactionAI(new CommandService(balance, troops),
            new DeployService(balance, troops,
                new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
                new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory())),
            new AiConfig(DeployTarget: int.MaxValue), map: Map,
            fieldBuildingDefinitions: Definitions);

        var after = ai.PlanWeek(state, new(1));

        var returning = after.Armies.Single(unit => unit.Id == builder.Id);
        Assert.Equal(UnitMode.March, returning.Field.Mode);
        Assert.Equal(new HexCoord(0, 0), returning.Field.Target);
    }

    private static GameState State()
    {
        var city = new City(new(1), "아군성", new(0, 0), new(1), 3000,
            Gold: 5000, Wall: 1000);
        var builder = new CombatUnit(new FieldUnit(new(1), new(1), new(3, 0),
            2, 2, 1, MovementDomain.Land, UnitMode.Standby, null, 1),
            new CombatStats(3000, 10, 10), new TroopPool(3000, 0),
            UnitCombatState.Create(60), Provisions: 100, CargoGold: 400);
        var enemy = new CombatUnit(new FieldUnit(new(2), new(2), new(6, 0),
            2, 2, 1, MovementDomain.Land, UnitMode.Advance, null, 2),
            new CombatStats(3000, 10, 10), new TroopPool(3000, 0),
            UnitCombatState.Create(60));
        return new GameState(1, 190, [], [city], [], FieldArmies: [builder, enemy]);
    }
}
