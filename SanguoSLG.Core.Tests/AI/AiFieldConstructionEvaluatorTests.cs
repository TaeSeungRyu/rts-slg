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
        var sites = new[] { 10, 11 }.Select(id => new FieldBuilding(new(id), "watchtower",
            new(1), new(id, 3), 750, 1, 14)).ToList();
        Assert.Empty(Evaluate(state with { FieldBuildings = sites }));

        IReadOnlyList<AiConstructionCandidate> Evaluate(GameState value) =>
            AiFieldConstructionEvaluator.Evaluate(value, new(1), Map, Definitions, visible);
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
