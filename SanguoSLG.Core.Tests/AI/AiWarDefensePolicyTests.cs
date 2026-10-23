namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public class AiWarDefensePolicyTests
{
    private static CombatUnit Unit(int id, int q, TroopClass troopClass,
        bool supply = false) => new(new FieldUnit(new UnitId(id), new FactionId(2),
            new HexCoord(q, 0), 1, 1, 1, MovementDomain.Land, UnitMode.Attack,
            null, id), new CombatStats(1000, 10, 10), new TroopPool(1000, 0),
            UnitCombatState.Create(60), Class: troopClass, IsSupply: supply);

    [Theory]
    [InlineData(10_000, 7_000)]
    [InlineData(1_001, 700)]
    public void 성벽_붕괴_출전은_주둔병력_30퍼센트를_남긴다(int stationed, int expected)
        => Assert.Equal(expected, AiWarDefensePolicy.MaxSortieTroops(stationed));

    [Fact]
    public void 보급부대가_6칸_안에_있으면_공성기보다_우선한다()
    {
        var city = new City(new CityId(1), "수성", default, new FactionId(1), 100);
        var infantry = Unit(1, 3, TroopClass.Infantry);
        var siege = Unit(2, 4, TroopClass.Siege);
        var supply = Unit(3, 6, TroopClass.Supply, supply: true);

        var ordered = AiWarDefensePolicy.OrderTargets(city, [infantry, siege, supply]);

        Assert.Equal([supply.Id, siege.Id, infantry.Id], ordered.Select(unit => unit.Id));
    }

    [Fact]
    public void 고립되지_않은_보급이_없으면_공성기를_먼저_공격한다()
    {
        var city = new City(new CityId(1), "수성", default, new FactionId(1), 100);
        var infantry = Unit(1, 1, TroopClass.Infantry);
        var siege = Unit(2, 3, TroopClass.Siege);
        Assert.Equal(siege.Id,
            AiWarDefensePolicy.OrderTargets(city, [infantry, siege])[0].Id);
    }
}
