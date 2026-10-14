namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed class SummonedBuildingCombatTests
{
    private static readonly IReadOnlyList<SummonedUnitDefinition> Summons =
        new SummonedUnitLoader().LoadFromDirectory(TestData.DataDirectory());
    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());
    private static readonly IReadOnlyList<FieldBuildingDefinition> Buildings =
        new FieldBuildingLoader().LoadFromDirectory(TestData.DataDirectory());

    private static CombatUnit Summon(string code, HexCoord target)
    {
        var definition = Summons.Single(x => x.Code == code);
        var field = new FieldUnit(new UnitId(1), new FactionId(1), new HexCoord(0, 0),
            definition.Speed, definition.Detection, definition.Range, MovementDomain.Land,
            UnitMode.Attack, target, 1, RangeCastle: definition.Range);
        return new CombatUnit(field,
            new CombatStats(definition.Troops, definition.Attack, definition.Defense,
                AptitudeGrade.A.Percent()), new TroopPool(definition.Troops, 0),
            UnitCombatState.Create(0), MaxTroops: definition.Troops,
            TroopCode: code, IsSummon: true);
    }

    [Theory]
    [InlineData("great_tiger", false)]
    [InlineData("dragon", false)]
    [InlineData("phoenix", false)]
    [InlineData("great_tiger", true)]
    [InlineData("dragon", true)]
    [InlineData("phoenix", true)]
    public void 소환수가_성이나_항구를_공격하되_점령하지_않는다(string code, bool port)
    {
        var city = new City(new CityId(1), "목표", new HexCoord(1, 0), new FactionId(2), 0,
            Wall: 100_000, Port: port ? PortSize.Small : PortSize.None);
        var summon = Summon(code, city.Position);
        var siege = new CampaignSiege(new BattleResolver(60), Troops, summons: Summons);

        var result = siege.Resolve([summon], [city], []);

        Assert.True(Assert.Single(result.Exchanges).WallDamage > 0);
        Assert.True(Assert.Single(result.Cities).Wall < city.Wall);
        var undefended = city with { Wall = 0 };
        var state = new GameState(1, 190, [], [undefended], [], FieldArmies: [summon]);
        var after = new CityCapture().ResolveAll(state, new FixedRandom(), out var captures);
        Assert.Empty(captures);
        Assert.Equal(undefended.Owner, Assert.Single(after.Cities).Owner);
    }

    [Theory]
    [InlineData("great_tiger")]
    [InlineData("dragon")]
    [InlineData("phoenix")]
    public void 소환수는_별도_건물공격력으로_야전건축물을_공격한다(string code)
    {
        var target = new HexCoord(1, 0);
        var summon = Summon(code, target);
        var building = new FieldBuilding(new FieldBuildingId(1), "fort", new FactionId(2),
            target, 100_000, 0, 0);
        var state = new GameState(1, 190, [], [], [], FieldBuildings: [building]);
        var combat = new FieldBuildingCombat(new BattleResolver(60), Buildings, Troops, Summons);

        var result = combat.Resolve(state, [summon]);

        var definition = Summons.Single(x => x.Code == code);
        var fort = Buildings.Single(x => x.Code == "fort");
        var expected = new BattleResolver(60).Damage(
            summon.Stats with { AtkStat = definition.BuildingAttack },
            new CombatStats(building.HitPoints, 0, fort.Defense, AptitudeGrade.APlus.Percent()));
        Assert.Equal(expected, Assert.Single(result.Exchanges).Damage);
        Assert.True(expected > 0);
        Assert.Equal(building.HitPoints - expected, Assert.Single(result.State.Buildings).HitPoints);
    }

    [Fact]
    public void 소환수에게_적_건축물_공격명령을_지정할_수_있다()
    {
        var target = new HexCoord(2, 0);
        var summon = Summon("great_tiger", target) with
        { Field = Summon("great_tiger", target).Field with { Mode = UnitMode.Standby, Target = null } };
        var building = new FieldBuilding(new FieldBuildingId(1), "fort", new FactionId(2),
            target, 100_000, 0, 0);
        var state = new GameState(1, 190, [], [], [], FieldArmies: [summon], FieldBuildings: [building]);
        var commander = new FieldUnitCommandService((_, _) => false,
            (world, hex) => world.Buildings.Any(x => x.Position == hex && x.Owner != summon.Field.Owner));

        var order = commander.Reassign(state, summon.Field.Owner,
            new FieldUnitCommandRequest(summon.Id, UnitMode.Attack, target));

        Assert.True(order.Ok, order.Error);
        Assert.Equal(UnitMode.Attack, Assert.Single(order.State.Armies).Field.Mode);
        Assert.Equal(target, Assert.Single(order.State.Armies).Field.Target);
    }

    private sealed class FixedRandom : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
    }
}
