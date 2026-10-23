namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed class AiSupportRoutePolicyTests
{
    private static readonly HexMap Map = new(-5, 50, -5, 5);
    private static readonly BalanceConfig Balance = new(MonthlyTaxPerCity: 100);
    private static readonly IReadOnlyDictionary<string, TroopTemplate> Troops =
        new Dictionary<string, TroopTemplate>
        {
            ["horse"] = new("horse", "기병", TroopClass.Cavalry, 10, 5, 5, MovementPerDay: 2),
            ["foot"] = new("foot", "보병", TroopClass.Infantry, 10, 5, 5, MovementPerDay: 1),
        };

    private static City City(int id, int q, int owner = 1) => new(new CityId(id), $"c{id}",
        new HexCoord(q, 0), new FactionId(owner), 1000, Wall: 600, Security: 80);

    [Fact]
    public void 실제_공격중인_부대만_지원_위협으로_판정한다()
    {
        var defended = City(1, 0);
        CombatUnit Enemy(int id, UnitMode mode, HexCoord target) => new(
            new FieldUnit(new UnitId(id), new FactionId(2), new HexCoord(1, 0), 1, 2, 1,
                MovementDomain.Land, mode, target, id),
            new CombatStats(10000, 10, 5), new TroopPool(10000, 0), UnitCombatState.Create(50));
        var attacking = Enemy(1, UnitMode.Attack, defended.Position);
        var passing = Enemy(2, UnitMode.March, defended.Position);
        var other = Enemy(3, UnitMode.Attack, new HexCoord(10, 0));
        var state = new GameState(1, 190, [], [defended], [],
            FieldArmies: [attacking, passing, other]);

        Assert.Equal([attacking.Id], AiSupportRoutePolicy.Attackers(state, defended)
            .Select(unit => unit.Id));
    }

    [Fact]
    public void 실제_경로가_가까운_지원성_하나를_선정하고_병력_문턱을_지킨다()
    {
        var defended = City(1, 0);
        var near = City(2, 5);
        var far = City(3, 35);
        var state = new GameState(1, 190, [], [defended, near, far], [],
            GarrisonForces: [new(near.Id, "horse", 15000, 60),
                new(far.Id, "horse", 30000, 60)]);

        var route = AiSupportRoutePolicy.Closest(state, defended, Map, Troops, Balance);
        Assert.NotNull(route);
        Assert.Equal(near.Id, route.Origin);
        Assert.InRange(route.TravelDays, 1, 14);

        var tooSmall = state with { GarrisonForces = [new(near.Id, "horse", 14999, 60)] };
        Assert.Null(AiSupportRoutePolicy.Closest(tooSmall, defended, Map, Troops, Balance));
    }
}
