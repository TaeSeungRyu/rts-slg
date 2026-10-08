namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed class FieldBuildingStage4Tests
{
    [Theory]
    [InlineData("palisade")]
    [InlineData("watchtower")]
    [InlineData("fort")]
    [InlineData("formation")]
    public void 건축물_교전도_5회충전후_6회째발동하고_다음은_부관차례다(string code)
    {
        var target = new HexCoord(1, 0);
        var building = Building(code, Enemy, target) with { HitPoints = 1000000 };
        var active = new ActiveSkill("crush", "분쇄", ActiveType.Strike, "high", 180, BuildingOnly: true);
        var adjutant = active with { Code = "peerless", Name = "무쌍" };
        var unit = Combat(1, Player, default, target) with { State = UnitCombatState.Create(60, active, adjutant) };
        var state = new GameState(1, 190, [], [], [], FieldBuildings: [building]);
        var service = new FieldBuildingCombat(new BattleResolver(60), Definitions);
        for (var day = 1; day <= 12; day++)
        {
            var result = service.Resolve(state, [unit]);
            if (day is 6 or 12)
            {
                Assert.Equal(day == 6 ? active : adjutant, result.FiredActives[unit.Id]);
                Assert.Equal(0, result.Armies.Single().State.SharedActiveGauge.ElapsedDays);
            }
            else Assert.Empty(result.FiredActives);
            unit = result.Armies.Single();
            state = result.State;
        }
    }

    [Fact]
    public void 분쇄는_건축물피해도_1점8배로_대체하고_이미교전한부대는_추가충전하지않는다()
    {
        var target = new HexCoord(1, 0);
        var building = Building("fort", Enemy, target) with { HitPoints = 1000000 };
        var unit = Combat(1, Player, default, target);
        var state = new GameState(1, 190, [], [], [], FieldBuildings: [building]);
        var service = new FieldBuildingCombat(new BattleResolver(60), Definitions);
        var baseline = service.Resolve(state, [unit]).Exchanges.Single().Damage;
        var active = new ActiveSkill("crush", "분쇄", ActiveType.Strike, "high", 180, BuildingOnly: true);
        var charged = unit with { State = UnitCombatState.Create(60, active).AdvanceField(5) };
        var fired = service.Resolve(state, [charged]);
        Assert.Equal(baseline * 180 / 100, fired.Exchanges.Single().Damage);
        Assert.Single(fired.FiredActives);
        var skipped = service.Resolve(state, [charged], new HashSet<UnitId> { unit.Id });
        Assert.Empty(skipped.Exchanges);
        Assert.Equal(5, skipped.Armies.Single().State.SharedActiveGauge.ElapsedDays);
    }

    private static readonly FactionId Player = new(1);
    private static readonly FactionId Enemy = new(2);
    private static readonly IReadOnlyList<FieldBuildingDefinition> Definitions =
        new FieldBuildingLoader().LoadFromDirectory(TestData.DataDirectory());
    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    [Theory]
    [InlineData("palisade", 7)]
    [InlineData("watchtower", 7)]
    [InlineData("fort", 7)]
    [InlineData("formation", 7)]
    [InlineData("scout_post", 19)]
    public void 완공건물은_부대없이도_자체시야를_제공한다(string code, int count)
    {
        var vision = new BattlefieldVision(new BalanceConfig(0), [], Definitions);
        var building = Building(code, Player, default);
        var state = new GameState(1, 190, [], [], [], FieldBuildings: [building]);
        var map = new HexMap(-5, 5, -5, 5);
        Assert.Equal(count, vision.VisibleTiles(state, Player, map).Count);
        Assert.Empty(vision.VisibleTiles(state, Enemy, map));
        Assert.Empty(vision.VisibleTiles(state with { FieldBuildings = [building with { CompletionDay = 2 }] }, Player, map));
        Assert.Empty(vision.VisibleTiles(state with { FieldBuildings = [building with { ExpiresDay = 1 }] }, Player, map));
        Assert.Empty(vision.VisibleTiles(state with { FieldBuildings = [] }, Player, map));
    }

    private static FieldBuilding Building(string code, FactionId owner, HexCoord at, int day = 1)
    {
        var definition = Definitions.First(x => x.Code == code);
        return new(new FieldBuildingId(1), code, owner, at, definition.MaxHitPoints,
            day - definition.BuildDays, day);
    }

    private static FieldUnit Field(int id, FactionId owner, HexCoord at, HexCoord? target,
        UnitMode mode = UnitMode.March, int speed = 3)
        => new(new UnitId(id), owner, at, speed, 3, 1, MovementDomain.Land, mode, target, id, RangeCastle: 1);

    private static CombatUnit Combat(int id, FactionId owner, HexCoord at, HexCoord target)
        => new(Field(id, owner, at, target, UnitMode.Attack), new CombatStats(10_000, 20, 12),
            new TroopPool(10_000, 0), UnitCombatState.Create(60), MaxTroops: 10_000);

    [Fact]
    public void 아군은_건축물_타일을_통과하고_적은_진입하지_못한다()
    {
        var sim = new MovementSimulator(new PassabilityMap(new HexMap(0, 8, -3, 3), [], []));
        var at = new HexCoord(2, 0);
        var building = Building("watchtower", Player, at);
        var ally = sim.Advance([Field(1, Player, new HexCoord(0, 0), at)], 1,
            fieldBuildings: [building], fieldDefinitions: Definitions, fieldDay: 1);
        Assert.Equal(at, ally.Units.Single().Position);

        var enemy = sim.Advance([Field(2, Enemy, new HexCoord(0, 0), at, UnitMode.Attack)], 1,
            fieldBuildings: [building], fieldDefinitions: Definitions, fieldDay: 1);
        Assert.Equal(1, enemy.Units.Single().Position.Distance(at));
    }

    [Fact]
    public void 목책_이동력감소는_범위안에서_한번만_중첩없이_적용된다()
    {
        var sim = new MovementSimulator(new PassabilityMap(new HexMap(0, 8, -3, 3), [], []));
        var palisade = Building("palisade", Player, new HexCoord(2, 0));
        var second = palisade with { Id = new FieldBuildingId(2), Position = new HexCoord(2, -1) };
        var result = sim.Advance([Field(1, Player, new HexCoord(0, 0), new HexCoord(4, 0))], 1,
            fieldBuildings: [palisade, second], fieldDefinitions: Definitions, fieldDay: 1);
        Assert.Equal(new HexCoord(2, 0), result.Units.Single().Position);
    }

    [Fact]
    public void 감시탑_범위안_부대는_시야가_한칸_늘고_중첩되지_않는다()
    {
        var troops = new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());
        var vision = new BattlefieldVision(new BalanceConfig(0), troops, Definitions);
        var unit = Combat(1, Player, default, new HexCoord(8, 0)) with { TroopCode = "swordsman" };
        var tower = Building("watchtower", Player, default);
        var second = tower with { Id = new FieldBuildingId(2), Position = new HexCoord(1, 0) };
        var state = new GameState(1, 190, [], [], [], FieldArmies: [unit], FieldBuildings: [tower, second]);
        var visible = vision.VisibleTiles(state, Player, new HexMap(-8, 8, -8, 8));
        Assert.Contains(new HexCoord(4, 0), visible);
        Assert.DoesNotContain(new HexCoord(5, 0), visible);
    }

    [Fact]
    public void 적_건축물은_공격받아_파괴되고_반격하지_않는다()
    {
        var target = new HexCoord(2, 0);
        var building = Building("palisade", Enemy, target);
        var attacker = Combat(1, Player, new HexCoord(1, 0), target);
        var state = new GameState(1, 190, [], [], [], FieldArmies: [attacker], FieldBuildings: [building]);
        var service = new FieldBuildingCombat(new BattleResolver(60), Definitions);
        var result = service.Resolve(state, [attacker]);
        var exchange = Assert.Single(result.Exchanges);
        Assert.True(exchange.Damage > 0);
        Assert.Equal(attacker.Pool, result.Armies.Single().Pool);
        Assert.True(exchange.Destroyed || result.State.Buildings.Single().HitPoints < building.HitPoints);
    }

    [Fact]
    public void 야전건축물은_대유닛이_아닌_병종별_건물공격력과_성벽방어를_사용한다()
    {
        var target = new HexCoord(2, 0);
        var building = Building("palisade", Enemy, target);
        var attacker = Combat(1, Player, new HexCoord(1, 0), target) with
        {
            TroopCode = "cavalry",
            Stats = new CombatStats(10_000, 12, 12, 130, 124),
        };
        var state = new GameState(1, 190, [], [], [], FieldArmies: [attacker], FieldBuildings: [building]);

        var result = new FieldBuildingCombat(new BattleResolver(60), Definitions, Troops).Resolve(state, [attacker]);
        var exchange = Assert.Single(result.Exchanges);

        Assert.Equal(537, exchange.Damage);
        Assert.False(exchange.Destroyed);
        Assert.Equal(463, Assert.Single(result.State.Buildings).HitPoints);
    }

    [Fact]
    public void 보루와_진법의_주둔장은_수성적성과_방어배수로_피해를_줄인다()
    {
        var target = new HexCoord(2, 0);
        var general = new General(new GeneralId(10), "수비장", new Dictionary<TroopClass, AptitudeGrade>
        {
            [TroopClass.Defense] = AptitudeGrade.S,
        }, 80, 70, 60);
        var garrison = Combat(2, Enemy, target, target) with
        {
            VanguardId = general.Id,
            Stats = new CombatStats(10_000, 8, 10, DfBonusPercent: 120),
        };
        var attacker = Combat(1, Player, new HexCoord(1, 0), target) with
        {
            TroopCode = "cavalry",
            Stats = new CombatStats(10_000, 12, 12, 130, 124),
        };
        var empty = Building("fort", Enemy, target);
        var occupied = empty with { GarrisonUnit = garrison.Id };
        var service = new FieldBuildingCombat(new BattleResolver(60), Definitions, Troops);

        var emptyResult = service.Resolve(
            new GameState(1, 190, [], [], [general], FieldArmies: [attacker], FieldBuildings: [empty]),
            [attacker]);
        var occupiedResult = service.Resolve(
            new GameState(1, 190, [], [], [general], FieldArmies: [attacker, garrison], FieldBuildings: [occupied]),
            [attacker, garrison]);

        Assert.Equal(537, Assert.Single(emptyResult.Exchanges).Damage);
        Assert.Equal(407, Assert.Single(occupiedResult.Exchanges).Damage);
    }

    [Theory]
    [InlineData("fort")]
    [InlineData("formation")]
    public void 보루와_진법이_파괴되면_내부부대가_즉시_노출되어_공방한다(string code)
    {
        var target = new HexCoord(1, 0);
        var attacker = Combat(1, Player, default, target) with
        {
            Stats = new CombatStats(10_000, 80, 12, 130, 120),
        };
        var garrison = Combat(2, Enemy, target, target) with
        {
            Stats = new CombatStats(10_000, 20, 16, 120, 130),
        };
        var building = Building(code, Enemy, target) with
        {
            HitPoints = 1,
            GarrisonUnit = garrison.Id,
        };
        var state = new GameState(1, 190, [], [], [], FieldArmies: [attacker, garrison], FieldBuildings: [building]);

        var result = new FieldBuildingCombat(new BattleResolver(60), Definitions).Resolve(state, [attacker, garrison]);
        var exchange = Assert.Single(result.Exchanges);

        Assert.True(exchange.Destroyed);
        Assert.Equal(garrison.Id, exchange.ExposedGarrison);
        Assert.True(exchange.DamageToGarrison > 0);
        Assert.True(exchange.DamageToAttacker > 0);
        Assert.Empty(result.State.Buildings);
        Assert.True(result.Armies.Single(unit => unit.Id == garrison.Id).Pool.Active < garrison.Pool.Active);
        Assert.True(result.Armies.Single(unit => unit.Id == attacker.Id).Pool.Active < attacker.Pool.Active);
    }

    [Fact]
    public void 철거는_즉시_제거하고_건축중_부대를_해제한다()
    {
        var unit = Combat(1, Player, default, new HexCoord(4, 0)) with { IsConstructing = true };
        var building = Building("palisade", Player, default) with { BuilderUnit = unit.Id, CompletionDay = 8 };
        var state = new GameState(1, 190, [], [], [], FieldArmies: [unit], FieldBuildings: [building]);
        var service = new FieldConstructionService(Definitions, _ => TerrainType.Plains);
        var result = service.Demolish(state, Player, building.Id);
        Assert.True(result.Ok);
        Assert.Empty(result.State.Buildings);
        Assert.False(result.State.Armies.Single().IsConstructing);
    }

    [Theory]
    [InlineData(UnitMode.Advance, 1)]
    [InlineData(UnitMode.Attack, 1)]
    [InlineData(UnitMode.Advance, 2)]
    [InlineData(UnitMode.Attack, 2)]
    public void 양측_전진과_공격은_사거리에서_건물을_발견해_한개만_공격한다(UnitMode mode, int faction)
    {
        var owner = new FactionId(faction);
        var opponent = faction == 1 ? Enemy : Player;
        var building = Building("watchtower", opponent, new HexCoord(2, 0));
        var other = building with { Id = new FieldBuildingId(2), Position = new HexCoord(2, -1) };
        var unit = Combat(1, owner, default, new HexCoord(6, 0));
        unit = unit with { Field = unit.Field with { Mode = mode } };
        var sim = new MovementSimulator(new PassabilityMap(new HexMap(-3, 8, -3, 3), [], []));
        var move = sim.Advance([unit.Field], 1, fieldBuildings: [building, other], fieldDefinitions: Definitions, fieldDay: 1);
        unit = unit with { Field = move.Units.Single() };
        Assert.Equal(1, unit.Field.Position.Distance(building.Position));
        var state = new GameState(1, 190, [], [], [], FieldBuildings: [building, other]);
        var result = new FieldBuildingCombat(new BattleResolver(60), Definitions).Resolve(state, [unit]);
        Assert.Single(result.Exchanges);
        Assert.True(result.Exchanges[0].Damage > 0);
    }

    [Theory]
    [InlineData("march")]
    [InlineData("transport")]
    [InlineData("construction")]
    [InlineData("waiting")]
    [InlineData("ally")]
    [InlineData("scout")]
    public void 공격불가_조건에서는_건축물을_공격하지_않는다(string condition)
    {
        var target = new HexCoord(1, 0);
        var building = Building(condition == "scout" ? "scout_post" : "fort", condition == "ally" ? Player : Enemy, target);
        var unit = Combat(1, Player, default, target);
        unit = condition switch
        {
            "march" => unit with { Field = unit.Field with { Mode = UnitMode.March } },
            "transport" => unit with { IsTransport = true },
            "construction" => unit with { IsConstructing = true },
            "waiting" => unit with { AwaitingEgress = true },
            _ => unit,
        };
        var state = new GameState(1, 190, [], [], [], FieldBuildings: [building]);
        Assert.Empty(new FieldBuildingCombat(new BattleResolver(60), Definitions).Resolve(state, [unit]).Exchanges);
    }
}
