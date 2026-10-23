namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public class AiWarLogisticsPolicyTests
{
    private static CombatUnit Unit(int id, TroopClass troopClass, bool supply = false,
        int gold = 300)
        => new(new FieldUnit(new UnitId(id), new FactionId(1), default,
                1, 1, 1, MovementDomain.Land, UnitMode.Standby, null, id),
            new CombatStats(1000, 10, 10), new TroopPool(1000, 0),
            UnitCombatState.Create(60), Class: troopClass, IsSupply: supply,
            LootGold: gold);

    [Fact]
    public void 최종_편성이_1점2배일때만_보루를_요구한다()
    {
        var force = new AiWarForceAssessment(new CityId(1), new CityId(2), null,
            12_000, 0, 10_000, 12, 12_000, true);
        Assert.True(AiWarLogisticsPolicy.RequiresFort(force));
        Assert.False(AiWarLogisticsPolicy.RequiresFort(force with { RequiredRatioTenths = 13 }));
        Assert.False(AiWarLogisticsPolicy.RequiresFort(force with { DefenderTroops = 0 }));
    }

    [Fact]
    public void 보급부대가_필수라면_보병부터_보루_건축을_맡는다()
    {
        var supply = Unit(1, TroopClass.Supply, supply: true);
        var infantry = Unit(2, TroopClass.Infantry);
        var cavalry = Unit(3, TroopClass.Cavalry);
        var siege = Unit(4, TroopClass.Siege);
        var units = new[] { siege, cavalry, infantry, supply };
        Assert.Equal(supply.Id, AiWarLogisticsPolicy.ChooseFortBuilder(units,
            new HashSet<UnitId>(), 300)?.Id);
        Assert.Equal(infantry.Id, AiWarLogisticsPolicy.ChooseFortBuilder(units,
            new HashSet<UnitId> { supply.Id }, 300)?.Id);
    }

    [Fact]
    public void 출발지까지가_아닌_목표까지의_편도_군량만_요구한다()
    {
        Assert.True(AiWarLogisticsPolicy.HasOutboundProvision(10_000, 100, 10));
        Assert.False(AiWarLogisticsPolicy.HasOutboundProvision(10_000, 99, 10));
    }
}
