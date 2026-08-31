namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>경제전 B단계 — 시설 파괴·약탈(포위군 진행당 1개·노획 50%·군량 휴대 한도·입성 예치).</summary>
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
    public void 약탈_포위군이_마을을_부수고_금을_노획한다()
    {
        var city = Town(1, 2, new HexCoord(5, 0), villages: 2, paddies: 1);
        var looter = Besieger(1, 1, new HexCoord(4, 0), city.Position);

        var r = Plunder().Resolve([looter], [city]);

        var report = Assert.Single(r.Reports);
        Assert.Equal(("village", 200, 0), (report.Facility, report.Gold, report.Provisions)); // 마을 400×50%
        Assert.Equal(1, r.Cities.Single().Villages);       // 진행당 1개만
        Assert.Equal(1, r.Cities.Single().Paddies);        // 우선순위: 마을 먼저
        Assert.Equal(200, r.Armies.Single().LootGold);
    }

    [Fact]
    public void 약탈_군량_노획은_휴대_한도까지만_싣는다()
    {
        // 논 → 군량 150 노획. 휴대 여유가 30뿐이면 30만 싣고 나머지 소실.
        var city = Town(1, 2, new HexCoord(5, 0), paddies: 1);
        var looter = Besieger(1, 1, new HexCoord(4, 0), city.Position, troops: 10000, provisions: 270);
        // MaxProvisions = 300(검병 10000) → 여유 30.

        var r = Plunder().Resolve([looter], [city]);

        var report = Assert.Single(r.Reports);
        Assert.Equal(30, report.Provisions);
        Assert.Equal(300, r.Armies.Single().Provisions);
        Assert.Equal(0, r.Cities.Single().Paddies);
    }

    [Fact]
    public void 약탈_우선순위는_마을_논_밭_공방_순이다()
    {
        var city = Town(1, 2, new HexCoord(5, 0), farms: 1, workshop: true);
        var looter = Besieger(1, 1, new HexCoord(4, 0), city.Position, provisions: 0);

        var first = Plunder().Resolve([looter], [city]);
        Assert.Equal("farm", first.Reports.Single().Facility); // 마을·논 없으니 밭

        var second = Plunder().Resolve(first.Armies, first.Cities);
        Assert.Equal("workshop", second.Reports.Single().Facility); // 마지막이 공방
        Assert.False(second.Cities.Single().Workshop);
        Assert.Equal(200, second.Armies.Single().LootGold); // 공방 400×50%
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
        Assert.Equal(1000 + 120, c.Provisions);
    }

    [Fact]
    public void 캠페인_포위군이_주마다_시설을_태운다()
    {
        // 성벽·수비가 버티는 동안 포위군이 시설을 하나씩 태우는 경제전.
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

        Assert.True(plunders.Count >= 1, "포위 중 약탈이 일어난다");
        var c = after.Cities.Single();
        Assert.True(c.Villages + c.Paddies < 4, $"시설이 줄었다: 마을{c.Villages} 논{c.Paddies}");
    }
}
