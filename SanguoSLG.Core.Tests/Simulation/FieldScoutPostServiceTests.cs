namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed class FieldScoutPostServiceTests
{
    private static readonly FactionId Player = new(1);
    private static readonly FactionId Enemy = new(2);
    private static readonly IReadOnlyList<FieldBuildingDefinition> Definitions =
        new FieldBuildingLoader().LoadFromDirectory(TestData.DataDirectory());
    private static readonly FieldBuildingDefinition Scout = Definitions.Single(x => x.Kind == FieldBuildingKind.ScoutPost);

    private static CombatUnit Unit(int id, FactionId owner, HexCoord at, HexCoord? target = null,
        bool constructing = false)
        => new(new FieldUnit(new UnitId(id), owner, at, 3, 2, 1, MovementDomain.Land,
                UnitMode.March, target, id),
            new CombatStats(1_000, 10, 8), new TroopPool(1_000, 0), UnitCombatState.Create(60),
            MaxTroops: 10_000, Provisions: 100, CargoGold: 100, IsConstructing: constructing);

    private static FieldBuilding Building(FactionId owner, HexCoord at, int completion = 8,
        UnitId? builder = null)
        => new(new FieldBuildingId(1), Scout.Code, owner, at, Scout.MaxHitPoints,
            1, completion, completion + Scout.LifetimeDays, builder);

    [Theory]
    [InlineData(TerrainType.Forest, true)]
    [InlineData(TerrainType.Mountain, true)]
    [InlineData(TerrainType.Plains, false)]
    [InlineData(TerrainType.Desert, false)]
    public void 정찰대는_숲과_산악에만_설치한다(TerrainType terrain, bool expected)
    {
        var unit = Unit(1, Player, new HexCoord(2, 2));
        var state = new GameState(1, 190, [], [], [], FieldArmies: [unit]);
        var service = new FieldConstructionService(Definitions, _ => terrain);

        Assert.Equal(expected, service.Start(state, Player,
            new(unit.Id, Scout.Code, new HexCoord(3, 2))).Ok);
    }

    [Fact]
    public void 완공후_59일까지_반경2_시야를_제공하고_60일째_만료한다()
    {
        var troops = new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());
        var vision = new BattlefieldVision(new BalanceConfig(0), troops, Definitions);
        var building = Building(Player, default);
        var state = new GameState(67, 190, [], [], [], FieldBuildings: [building]);

        var visible = vision.VisibleTiles(state, Player, new HexMap(-5, 5, -5, 5));
        Assert.Equal(19, visible.Count);
        Assert.Contains(new HexCoord(2, 0), visible);
        Assert.Empty(vision.VisibleTiles(state with { Day = 68 }, Player, new HexMap(-5, 5, -5, 5)));
    }

    [Fact]
    public void 적이_정찰대_타일을_지나면_즉시_철수하고_아군통과는_유지한다()
    {
        var at = new HexCoord(2, 0);
        var building = Building(Player, at);
        var state = new GameState(10, 190, [], [], [], FieldBuildings: [building]);
        var service = new FieldScoutPostService(Definitions);
        IReadOnlyDictionary<FactionId, IReadOnlySet<HexCoord>> allyVisited =
            new Dictionary<FactionId, IReadOnlySet<HexCoord>> { [Player] = new HashSet<HexCoord> { at } };
        Assert.Single(service.Resolve(state, [], allyVisited, 10).State.Buildings);

        IReadOnlyDictionary<FactionId, IReadOnlySet<HexCoord>> enemyVisited =
            new Dictionary<FactionId, IReadOnlySet<HexCoord>> { [Enemy] = new HashSet<HexCoord> { at } };
        var removed = service.Resolve(state, [], enemyVisited, 10);
        Assert.Empty(removed.State.Buildings);
        Assert.Equal(building.Id, Assert.Single(removed.Removed));
    }

    [Fact]
    public void 공사중_정찰대가_철수하면_건축부대도_명령대기로_해제한다()
    {
        var at = new HexCoord(2, 0);
        var builder = Unit(1, Player, at, constructing: true);
        var building = Building(Player, at, completion: 20, builder: builder.Id);
        var state = new GameState(10, 190, [], [], [], FieldArmies: [builder], FieldBuildings: [building]);
        IReadOnlyDictionary<FactionId, IReadOnlySet<HexCoord>> visited =
            new Dictionary<FactionId, IReadOnlySet<HexCoord>> { [Enemy] = new HashSet<HexCoord> { at } };

        var result = new FieldScoutPostService(Definitions).Resolve(state, [builder], visited, 10);

        Assert.Empty(result.State.Buildings);
        Assert.False(result.Armies.Single().IsConstructing);
        Assert.Equal(UnitMode.Advance, result.Armies.Single().Field.Mode);
    }

    [Fact]
    public void 정찰대는_양측이_통과할수_있고_공격대상이_아니다()
    {
        var at = new HexCoord(1, 0);
        var scout = Building(Player, at);
        var mover = Unit(2, Enemy, default, new HexCoord(2, 0));
        var movement = new MovementSimulator(new PassabilityMap(new HexMap(0, 4, -2, 2), [], []));
        var moved = movement.Advance([mover.Field], 1, fieldBuildings: [scout],
            fieldDefinitions: Definitions, fieldDay: 10);
        Assert.Equal(new HexCoord(2, 0), moved.Units.Single().Position);

        var attacker = mover with { Field = mover.Field with { Position = default, Target = at, Mode = UnitMode.Attack } };
        var state = new GameState(10, 190, [], [], [], FieldArmies: [attacker], FieldBuildings: [scout]);
        var combat = new FieldBuildingCombat(new BattleResolver(60), Definitions).Resolve(state, [attacker]);
        Assert.Empty(combat.Exchanges);
        Assert.Single(combat.State.Buildings);
    }

    [Fact]
    public void 캠페인_이동경로의_적통과와_60일_경계에서_정찰대를_제거한다()
    {
        var at = new HexCoord(1, 0);
        var enemy = Unit(2, Enemy, default, new HexCoord(3, 0));
        var scout = Building(Player, at);
        var moving = new GameState(10, 190, [], [], [], FieldArmies: [enemy], FieldBuildings: [scout]);

        var afterMove = Campaign().AdvanceWeek(moving, out var turns);

        Assert.Empty(afterMove.Buildings);
        Assert.Contains(turns, turn => turn.RemovedScoutPosts.Contains(scout.Id));

        var beforeExpiry = new GameState(61, 190, [], [], [], FieldBuildings: [scout]);
        var afterExpiry = Campaign().AdvanceWeek(beforeExpiry, out _);
        Assert.Equal(68, afterExpiry.Day);
        Assert.Empty(afterExpiry.Buildings);
    }

    private static CampaignEngine Campaign()
    {
        var movement = new MovementSimulator(new PassabilityMap(new HexMap(0, 6, -3, 3), [], []));
        var field = new AdvanceOrchestrator(movement,
            new CombatPhaseResolver(new BattleResolver(60), woundedPercent: 70));
        return new CampaignEngine(field, new WorldEngine(new BalanceConfig(0)),
            fieldBuildingCombat: new FieldBuildingCombat(new BattleResolver(60), Definitions),
            fieldBuildingDefinitions: Definitions);
    }
}
