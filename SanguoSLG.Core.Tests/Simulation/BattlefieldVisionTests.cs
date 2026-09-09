namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public class BattlefieldVisionTests
{
    private static readonly FactionId Player = new(1);
    private static readonly FactionId Enemy = new(2);
    private static readonly HexMap Map = new(-30, 30, -30, 30);
    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());
    private static readonly BattlefieldVision Vision = new(new BalanceConfig(0), Troops);

    private static City City(int id, int q, FactionId owner, CastleSize size = CastleSize.Small)
        => new(new CityId(id), "성", new HexCoord(q, 0), owner, 0, size);

    private static CombatUnit Unit(string code, FactionId owner, int q = 0, bool supply = false)
        => new(new FieldUnit(new UnitId(1), owner, new HexCoord(q, 0), 1, 1, 1,
                MovementDomain.Land, UnitMode.Attack, null, 0),
            new CombatStats(500, 10, 10), new TroopPool(500, 0), UnitCombatState.Create(50),
            TroopCode: code, IsSupply: supply);

    [Theory]
    [InlineData(CastleSize.Small, 4)]
    [InlineData(CastleSize.Medium, 5)]
    [InlineData(CastleSize.Large, 6)]
    public void 성_크기별_헥사반경과_경계(CastleSize size, int radius)
    {
        var state = new GameState(1, 190, [], [City(1, 0, Player, size)], []);
        var visible = Vision.VisibleTiles(state, Player, Map);
        Assert.Equal(1 + 3 * radius * (radius + 1), visible.Count);
        Assert.All(visible, h => Assert.InRange(h.Distance(default), 0, radius));
        Assert.Contains(new HexCoord(radius, 0), visible);
        Assert.DoesNotContain(new HexCoord(radius + 1, 0), visible);
    }

    [Theory]
    [InlineData("swordsman", 3)]
    [InlineData("archer", 3)]
    [InlineData("war_elephant", 3)]
    [InlineData("cavalry", 4)]
    [InlineData("catapult", 2)]
    [InlineData("small_boat", 4)]
    public void 병종별_시야를_데이터에서_읽는다(string code, int radius)
        => Assert.Equal(radius, Vision.UnitRadius(Unit(code, Player)));

    [Fact]
    public void 보급부대는_기병이어도_2칸()
        => Assert.Equal(2, Vision.UnitRadius(Unit("cavalry", Player, supply: true)));

    [Fact]
    public void 정찰_60일만료와_다른세력정보_제외()
    {
        var city = City(2, 20, Enemy);
        var state = new GameState(10, 190, [], [city], [], ScoutedCities: [new CityIntel(Player, city.Id, 69)]);
        for (var day = 10; day < 70; day++)
            Assert.Contains(city.Position, Vision.VisibleTiles(state with { Day = day }, Player, Map));
        var expired = state with { Day = 70 };
        Assert.Empty(Vision.VisibleTiles(expired, Player, Map));
        Assert.False(BattlefieldVision.CanInspectCity(expired, Player, city, new HashSet<HexCoord>()));
        Assert.Empty(Vision.VisibleTiles(state, new FactionId(3), Map));
    }

    [Fact]
    public void 여러성_부대_정찰시야를_합산하고_이동사망점령을_반영()
    {
        var unit = Unit("cavalry", Player, -15);
        var enemyCity = City(2, 20, Enemy);
        var state = new GameState(1, 190, [], [City(1, 0, Player), enemyCity], [],
            FieldArmies: [unit], ScoutedCities: [new CityIntel(Player, enemyCity.Id, 60)]);
        var visible = Vision.VisibleTiles(state, Player, Map);
        Assert.Contains(new HexCoord(-19, 0), visible);
        Assert.Contains(new HexCoord(4, 0), visible);
        Assert.Contains(new HexCoord(24, 0), visible);
        var moved = state with { FieldArmies = [unit with { Field = unit.Field.MoveTo(new HexCoord(-10, 0)) }] };
        Assert.DoesNotContain(new HexCoord(-19, 0), Vision.VisibleTiles(moved, Player, Map));
        var dead = state with { FieldArmies = [unit with { Pool = new TroopPool(0, 0) }] };
        Assert.DoesNotContain(new HexCoord(-15, 0), Vision.VisibleTiles(dead, Player, Map));
        var captured = state with { Day = 100, Cities = [enemyCity with { Owner = Player }], FieldArmies = [] };
        Assert.Contains(new HexCoord(24, 0), Vision.VisibleTiles(captured, Player, Map));
    }

    [Fact]
    public void 적부대는_시야를_주지않고_시야내에서만_조회가능()
    {
        var unit = Unit("swordsman", Enemy, 5);
        var state = new GameState(1, 190, [], [City(1, 0, Player)], [], FieldArmies: [unit]);
        var visible = Vision.VisibleTiles(state, Player, Map);
        Assert.False(BattlefieldVision.CanSeeUnit(Player, unit, visible));
        Assert.True(BattlefieldVision.CanSeeUnit(Player, unit with { Field = unit.Field.MoveTo(new HexCoord(4, 0)) }, visible));
        Assert.True(BattlefieldVision.CanInspectCity(state, Player, City(2, 4, Enemy), visible));
        Assert.False(BattlefieldVision.CanInspectCity(state, Player, City(3, 5, Enemy), visible));
    }

    [Fact]
    public void 생산부대_채집중에도_병종시야를_제공하고_맵경계로_제한()
    {
        var op = new ProductionOperation(1, new CityId(1), Player, default, default, "farm", "cavalry",
            500, 50, 1, new GeneralId(1), 1, 14, ProductionPhase.Gathering);
        var state = new GameState(1, 190, [], [], [], ProductionOperations: [op]);
        var visible = Vision.VisibleTiles(state, Player, new HexMap(0, 4, 0, 4));
        Assert.Contains(new HexCoord(4, 0), visible);
        Assert.DoesNotContain(new HexCoord(4, 4), visible);
        Assert.All(visible, h => Assert.True(h.Q >= 0 && h.R >= 0));
    }

    [Fact]
    public void 병종시야_개별지정과_음수거부()
    {
        const string json = """[{"code":"dragon","class":"cavalry","vision":7}]""";
        var custom = new TroopTypeLoader().LoadFromJson(json);
        Assert.Equal(7, new BattlefieldVision(new BalanceConfig(0), custom).UnitRadius(Unit("dragon", Player)));
        Assert.Throws<InvalidDataException>(() => new TroopTypeLoader().LoadFromJson(json.Replace("7", "-1")));
    }

    [Fact]
    public void 성과보급시야_설정데이터를_로드한다()
    {
        var balance = new ScenarioLoader().LoadFromJson("[]", "[]", "[]",
            """{"vision_castle_small":7,"vision_castle_medium":8,"vision_castle_large":9,"vision_supply":1}""",
            """{"min_q":0,"max_q":1,"min_r":0,"max_r":1}""").Balance;
        var custom = new BattlefieldVision(balance, Troops);
        Assert.Equal(7, custom.CityRadius(CastleSize.Small));
        Assert.Equal(8, custom.CityRadius(CastleSize.Medium));
        Assert.Equal(9, custom.CityRadius(CastleSize.Large));
        Assert.Equal(1, custom.UnitRadius(Unit("cavalry", Player, supply: true)));
    }
}
