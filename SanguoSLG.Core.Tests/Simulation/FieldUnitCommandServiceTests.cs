namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public class FieldUnitCommandServiceTests
{
    private static readonly FactionId Player = new(1);
    private static readonly FactionId Enemy = new(2);

    private static FieldUnitCommandService Service(Func<MovementDomain, HexCoord, bool>? canEnter = null)
        => new(canEnter ?? ((_, _) => true));

    private static City City(int id, FactionId owner, HexCoord pos)
        => new(new CityId(id), $"c{id}", pos, owner, 1000);

    private static CombatUnit Unit(int id, FactionId owner, HexCoord pos, bool supply = false)
        => new(new FieldUnit(new UnitId(id), owner, pos, 2, 2, 1, MovementDomain.Land, UnitMode.March, null, id),
            new CombatStats(100, 10, 10), new TroopPool(1000, 0), UnitCombatState.Create(50),
            IsSupply: supply, TroopCode: supply ? "supply" : "swordsman");

    private static CombatUnit NavalUnit(int id, FactionId owner, HexCoord pos)
        => Unit(id, owner, pos) with
        {
            Class = TroopClass.Naval,
            Field = Unit(id, owner, pos).Field with { Domain = MovementDomain.DeepWater },
            TroopCode = "ship_small",
        };

    [Fact]
    public void 이동_명령은_목표와_경유지를_바꾼다()
    {
        var unit = Unit(1, Player, default);
        var state = new GameState(1, 190, [], [], [], FieldArmies: [unit]);
        var target = new HexCoord(4, 0);
        var waypoint = new HexCoord(2, 0);

        var result = Service().Reassign(state, Player,
            new FieldUnitCommandRequest(unit.Id, UnitMode.Advance, target, [waypoint]));

        Assert.True(result.Ok, result.Error);
        var changed = result.State.Armies.Single();
        Assert.Equal(UnitMode.Advance, changed.Field.Mode);
        Assert.Equal(target, changed.Field.Target);
        Assert.Equal([waypoint], changed.Field.Waypoints);
    }

    [Fact]
    public void 적성_목표는_공격모드로_전환된다()
    {
        var enemyCity = City(2, Enemy, new HexCoord(5, 0));
        var state = new GameState(1, 190, [], [enemyCity], [], FieldArmies: [Unit(1, Player, default)]);

        var result = Service().Reassign(state, Player,
            new FieldUnitCommandRequest(new UnitId(1), UnitMode.March, enemyCity.Position,
                VisibleTiles: new HashSet<HexCoord> { enemyCity.Position }));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(UnitMode.Attack, result.State.Armies.Single().Field.Mode);
    }

    [Fact]
    public void 중형항구_발자국_타일도_아군_목표로_지정할수있다()
    {
        var port = new City(new CityId(2), "항구", new HexCoord(5, 0), Player, 1000,
            CastleSize.Medium, Port: PortSize.Medium);
        var footprintTile = new HexCoord(5, 1);
        var state = new GameState(1, 190, [], [port], [], FieldArmies: [Unit(1, Player, default)]);

        var result = Service((_, h) => h != footprintTile).Reassign(state, Player,
            new FieldUnitCommandRequest(new UnitId(1), UnitMode.March, footprintTile));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(footprintTile, result.State.Armies.Single().Field.Target);
    }

    [Fact]
    public void 시야밖_적성도_이동목표로_지정할수있다()
    {
        var enemyCity = City(2, Enemy, new HexCoord(5, 0));
        var state = new GameState(1, 190, [], [enemyCity], [], FieldArmies: [Unit(1, Player, default)]);

        var result = Service().Reassign(state, Player,
            new FieldUnitCommandRequest(new UnitId(1), UnitMode.Attack, enemyCity.Position,
                VisibleTiles: new HashSet<HexCoord>()));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(UnitMode.Attack, result.State.Armies.Single().Field.Mode);
    }

    [Fact]
    public void 시야밖_적성_발자국_타일도_공격목표로_전환된다()
    {
        var enemyCity = new City(new CityId(2), "적항", new HexCoord(5, 0), Enemy, 1000,
            CastleSize.Medium, Port: PortSize.Medium);
        var footprintTile = new HexCoord(5, 1);
        var state = new GameState(1, 190, [], [enemyCity], [], FieldArmies: [Unit(1, Player, default)]);

        var result = Service().Reassign(state, Player,
            new FieldUnitCommandRequest(new UnitId(1), UnitMode.March, footprintTile,
                VisibleTiles: new HashSet<HexCoord>()));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(UnitMode.Attack, result.State.Armies.Single().Field.Mode);
    }

    [Fact]
    public void 정찰한_적성은_시야밖이어도_목표로_지정할수있다()
    {
        var enemyCity = City(2, Enemy, new HexCoord(5, 0));
        var state = new GameState(1, 190, [], [enemyCity], [], FieldArmies: [Unit(1, Player, default)],
            ScoutedCities: [new CityIntel(Player, enemyCity.Id, 60)]);

        var result = Service().Reassign(state, Player,
            new FieldUnitCommandRequest(new UnitId(1), UnitMode.Attack, enemyCity.Position,
                VisibleTiles: new HashSet<HexCoord>()));

        Assert.True(result.Ok, result.Error);
    }

    [Fact]
    public void 보급부대도_공격명령을_받을수있다()
    {
        var state = new GameState(1, 190, [], [], [], FieldArmies: [Unit(1, Player, default, supply: true)]);

        var result = Service().Reassign(state, Player,
            new FieldUnitCommandRequest(new UnitId(1), UnitMode.Attack, new HexCoord(1, 0)));

        Assert.True(result.Ok, result.Error);
        Assert.Equal(UnitMode.Attack, result.State.Armies.Single().Field.Mode);
    }

    [Fact]
    public void 해상부대는_항구만_도시목표로_허용한다()
    {
        var land = City(2, Enemy, new HexCoord(5, 0));
        var port = land with { Id = new CityId(3), Position = new HexCoord(6, 0), Port = PortSize.Small };
        var state = new GameState(1, 190, [], [land, port], [], FieldArmies: [NavalUnit(1, Player, default)]);

        var landResult = Service().Reassign(state, Player,
            new FieldUnitCommandRequest(new UnitId(1), UnitMode.Attack, land.Position));
        Assert.False(landResult.Ok);

        var portResult = Service().Reassign(state, Player,
            new FieldUnitCommandRequest(new UnitId(1), UnitMode.Attack, port.Position));
        Assert.True(portResult.Ok, portResult.Error);
    }

    [Fact]
    public void 정지는_목표와_경유지를_지운다()
    {
        var unit = Unit(1, Player, default) with
        {
            Field = Unit(1, Player, default).Field with
            {
                Mode = UnitMode.Attack,
                Target = new HexCoord(5, 0),
                Waypoints = [new HexCoord(2, 0)],
            },
        };
        var state = new GameState(1, 190, [], [], [], FieldArmies: [unit]);

        var result = Service().Stop(state, Player, unit.Id);

        Assert.True(result.Ok, result.Error);
        Assert.Null(result.State.Armies.Single().Field.Target);
        Assert.Null(result.State.Armies.Single().Field.Waypoints);
    }

    [Fact]
    public void 복귀는_아군성으로_행군목표를_잡는다()
    {
        var home = City(1, Player, new HexCoord(5, 0));
        var state = new GameState(1, 190, [], [home], [], FieldArmies: [Unit(1, Player, default)]);

        var result = Service().ReturnToCity(state, Player, new UnitId(1), home.Id);

        Assert.True(result.Ok, result.Error);
        var changed = result.State.Armies.Single();
        Assert.Equal(UnitMode.March, changed.Field.Mode);
        Assert.Equal(home.Position, changed.Field.Target);
    }
}
