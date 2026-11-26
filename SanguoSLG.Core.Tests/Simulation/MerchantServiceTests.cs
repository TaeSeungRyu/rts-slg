namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public sealed class MerchantServiceTests
{
    private sealed class MinimumRandom : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => minInclusive;
    }

    private static readonly FactionId Player = new(1);
    private static readonly CityId CityId = new(1);

    private static GameState State()
        => new(1, 190, [], [new City(CityId, "성", new HexCoord(5, 5), Player,
            Provisions: 1000, Gold: 10000)], []);

    [Fact]
    public void 상인은_7일마다_판정하고_14일뒤_떠나며_도시마다_한명만_있다()
    {
        var service = new MerchantService(new HexMap(0, 12, 0, 12), (_, _) => new MinimumRandom());
        var week = service.Advance(State(), 1, 8);
        var visit = Assert.Single(week.Merchants);
        Assert.Equal(7, visit.ArrivedDay);
        Assert.Equal(21, visit.ExpiresDay);
        Assert.Equal(4, visit.ArtifactCodes.Distinct().Count());
        Assert.Equal(TerrainType.Plains, new HexMap(0, 12, 0, 12).TerrainAt(visit.Position));
        Assert.Single(service.Advance(week, 8, 14).Merchants);
        Assert.Empty(service.Advance(week, 14, 21).Merchants);
    }

    [Fact]
    public void 거래는_도시_자원을_변경하고_유물은_중복_구매할_수_있다()
    {
        var service = new MerchantService(new HexMap(0, 12, 0, 12), (_, _) => new MinimumRandom());
        var state = service.Advance(State(), 1, 8) with { Day = 8 };
        var bought = MerchantService.BuyGrain(state, CityId, Player, 100, 25);
        Assert.True(bought.Ok, bought.Error);
        Assert.Equal(1100, Assert.Single(bought.State.Cities).Provisions);
        Assert.Equal(9980, Assert.Single(bought.State.Cities).Gold);
        var sold = MerchantService.SellGrain(bought.State, CityId, Player, 100, 25);
        Assert.True(sold.Ok, sold.Error);
        Assert.Equal(9990, Assert.Single(sold.State.Cities).Gold);
        var code = Assert.Single(sold.State.Merchants).ArtifactCodes[0];
        var first = MerchantService.BuyArtifact(sold.State, CityId, Player, code);
        Assert.True(first.Ok, first.Error);
        var second = MerchantService.BuyArtifact(first.State, CityId, Player, code);
        Assert.True(second.Ok, second.Error);
        Assert.Equal(2, second.State.Treasures.Count);
        Assert.Equal(2, second.State.Treasures.Select(item => item.Id).Distinct().Count());
        Assert.Equal(code, second.State.Treasures[0].Code);
    }

    [Fact]
    public void 부대가_상인_칸을_점유하면_방문이_종료된다()
    {
        var service = new MerchantService(new HexMap(0, 12, 0, 12), (_, _) => new MinimumRandom());
        var state = service.Advance(State(), 1, 8);
        var plot = Assert.Single(state.Merchants).Position;
        var unit = new CombatUnit(
            new FieldUnit(new UnitId(1), Player, plot, 2, 2, 1,
                MovementDomain.Land, UnitMode.Standby, null, 1),
            new CombatStats(500, 5, 5), new TroopPool(500, 0), UnitCombatState.Create(60));
        var occupied = state with { FieldArmies = [unit] };
        Assert.Empty(service.Advance(occupied, 8, 9).Merchants);
    }

    [Fact]
    public void 상인_재고와_가격은_저장후에도_유지된다()
    {
        var service = new MerchantService(new HexMap(0, 12, 0, 12), (_, _) => new MinimumRandom());
        var state = service.Advance(State(), 1, 8);
        var saved = SaveService.Deserialize(SaveService.Serialize(state));
        var before = Assert.Single(state.Merchants);
        var after = Assert.Single(saved.Merchants);
        Assert.Equal(before.City, after.City);
        Assert.Equal(before.Position, after.Position);
        Assert.Equal(before.ArrivedDay, after.ArrivedDay);
        Assert.Equal(before.ExpiresDay, after.ExpiresDay);
        Assert.Equal(before.BuyGrainPercent, after.BuyGrainPercent);
        Assert.Equal(before.SellGrainPercent, after.SellGrainPercent);
        Assert.Equal(before.ArtifactCodes.ToArray(), after.ArtifactCodes.ToArray());
    }
}
