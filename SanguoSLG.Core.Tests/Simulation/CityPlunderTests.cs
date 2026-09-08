namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>경제전 B단계 — 현재 시설은 포위 공격으로 파괴되지 않는다. 노획물 입성 예치는 유지한다.</summary>
public class CityPlunderTests
{
    private static readonly CommandBalance B = new();

    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static CityPlunder Plunder() => new(B);

    private static CombatUnit Besieger(int id, int owner, HexCoord pos, HexCoord target,
        int troops = 8000, int provisions = -1)
    {
        var t = T["swordsman"];
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos,
            t.MovementPerDay, t.Detection, t.RangeUnit, MovementDomain.Land, UnitMode.Attack, target, id, t.RangeCastle);
        var stats = CombatStatsBuilder.BuildField(t, AptitudeGrade.A, 0, TerrainType.River, troops);
        return new CombatUnit(field, stats, new TroopPool(troops, 0), UnitCombatState.Create(60),
            60, 60, troops, t.Class, Provisions: provisions, TroopCode: "swordsman", Training: 60);
    }

    private static City Town(int id, int owner, HexCoord pos,
        int villages = 0, int paddies = 0, int farms = 0, bool workshop = false) =>
        new(new CityId(id), $"c{id}", pos, new FactionId(owner), 1000, CastleSize.Medium,
            Gold: 2000, Paddies: paddies, Farms: farms, Villages: villages, Workshop: workshop);

    [Fact]
    public void 포위군은_마을을_파괴하지_않는다()
    {
        var city = Town(1, 2, new HexCoord(5, 0), villages: 2, paddies: 1);
        var looter = Besieger(1, 1, new HexCoord(4, 0), city.Position);

        var r = Plunder().Resolve([looter], [city]);

        Assert.Empty(r.Reports);
        Assert.Equal(2, r.Cities.Single().Villages);
        Assert.Equal(1, r.Cities.Single().Paddies);
        Assert.Equal(0, r.Armies.Single().LootGold);
    }

    [Fact]
    public void 포위군은_논밭과_공방을_파괴하지_않는다()
    {
        var city = Town(1, 2, new HexCoord(5, 0), paddies: 1, farms: 1, workshop: true);
        var looter = Besieger(1, 1, new HexCoord(4, 0), city.Position, troops: 10000, provisions: 270);

        var r = Plunder().Resolve([looter], [city]);

        Assert.Empty(r.Reports);
        var c = r.Cities.Single();
        Assert.Equal((1, 1, true), (c.Paddies, c.Farms, c.Workshop));
        Assert.Equal(270, r.Armies.Single().Provisions);
    }

    [Fact]
    public void 약탈_포위가_아니면_일어나지_않는다()
    {
        var city = Town(1, 2, new HexCoord(5, 0), villages: 1);
        var far = Besieger(1, 1, new HexCoord(2, 0), city.Position);            // 거리 3
        var marching = Besieger(2, 1, new HexCoord(4, 0), city.Position) with { };
        marching = marching with { Field = marching.Field with { Mode = UnitMode.March } };

        var r = Plunder().Resolve([far, marching], [city]);

        Assert.Empty(r.Reports);
        Assert.Equal(1, r.Cities.Single().Villages);
    }

    [Fact]
    public void 약탈_시설이_없으면_아무_일도_없다()
    {
        var city = Town(1, 2, new HexCoord(5, 0));
        var looter = Besieger(1, 1, new HexCoord(4, 0), city.Position);

        var r = Plunder().Resolve([looter], [city]);

        Assert.Empty(r.Reports);
    }

    [Fact]
    public void 예치_노획물을_지닌_부대가_아군성에_입성하면_비축에_합산된다()
    {
        // 노획 금 350·군량 120을 실은 부대가 아군 성으로 복귀 입성 → 성 금고·비축에 예치.
        var home = new City(new CityId(1), "성", new HexCoord(5, 0), new FactionId(1), 1000, Gold: 500);
        var t = T["swordsman"];
        var field = new FieldUnit(new UnitId(1), new FactionId(1), new HexCoord(3, 0),
            t.MovementPerDay, t.Detection, t.RangeUnit, MovementDomain.Land, UnitMode.March, home.Position, 1, t.RangeCastle);
        var stats = CombatStatsBuilder.BuildField(t, AptitudeGrade.A, 0, TerrainType.River, 5000);
        var returning = new CombatUnit(field, stats, new TroopPool(5000, 0), UnitCombatState.Create(60),
            60, 60, 5000, t.Class, Provisions: 120, TroopCode: "swordsman", LootGold: 350);
        var s = new GameState(1, 1, new List<Faction>(), new List<City> { home }, new List<General>(),
            FieldArmies: new List<CombatUnit> { returning });

        var movement = new MovementSimulator(new PassabilityMap(new HexMap(0, 10, -5, 5), [], []));
        var engine = new CampaignEngine(
            new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), 70)),
            new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0)));

        var after = engine.AdvanceWeek(s, out _);

        Assert.Empty(after.Armies);
        var c = after.Cities.Single();
        Assert.Equal(500 + 350, c.Gold);
        Assert.Equal(1000 + 120 + 19, c.Provisions);
    }

    [Fact]
    public void 캠페인_포위군도_시설을_태우지_않는다()
    {
        var city = Town(9, 2, new HexCoord(5, 0), villages: 2, paddies: 2) with { Wall = 99999 };
        var looter = Besieger(1, 1, new HexCoord(4, 0), city.Position, provisions: 0);
        var s = new GameState(1, 1, new List<Faction>(), new List<City> { city }, new List<General>(),
            GarrisonForces: new List<GarrisonForce> { new(new CityId(9), "swordsman", 99999, 60) },
            FieldArmies: new List<CombatUnit> { looter });

        var movement = new MovementSimulator(new PassabilityMap(new HexMap(0, 10, -5, 5), [], []));
        var engine = new CampaignEngine(
            new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), 70)),
            new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0)),
            plunder: new CityPlunder(B));

        var after = engine.AdvanceWeek(s, out _, out _, out _, out var plunders);

        Assert.Empty(plunders);
        var c = after.Cities.Single();
        Assert.Equal((2, 2), (c.Villages, c.Paddies));
    }
}
