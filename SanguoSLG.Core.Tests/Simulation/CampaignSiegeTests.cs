namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>캠페인 공성(10b) — 성벽 타격·수비 손실·반격. 소유 전환·함락은 다음 단계.</summary>
public class CampaignSiegeTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static CampaignSiege Siege() =>
        new(new BattleResolver(60), T.Values.ToList());

    private static CampaignEngine Engine()
    {
        var movement = new MovementSimulator(new PassabilityMap(new HexMap(0, 30, -8, 8), [], []));
        var field = new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), 70));
        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100));
        return new CampaignEngine(field, world, Siege());
    }

    private static CombatUnit Army(int id, int owner, HexCoord pos, HexCoord target,
        int troops = 10000, string code = "swordsman")
    {
        var t = T[code];
        var field = new FieldUnit(new UnitId(id), new FactionId(owner), pos,
            t.MovementPerDay, t.Detection, t.RangeUnit, MovementDomain.Land, UnitMode.Attack, target, id, t.RangeCastle);
        var stats = CombatStatsBuilder.BuildField(t, AptitudeGrade.A, 0, TerrainType.River, troops);
        return new CombatUnit(field, stats, new TroopPool(troops, 0), UnitCombatState.Create(60),
            60, 60, troops, t.Class, TroopCode: code, Training: 50);
    }

    private static City Town(int id, int owner, HexCoord pos, int wall, CastleSize size = CastleSize.Medium) =>
        new(new CityId(id), $"c{id}", pos, new FactionId(owner), 0, size, Wall: wall);

    // ── 단위 정산(CampaignSiege) ──

    [Fact]
    public void 공성_성벽이_서있으면_성벽을_깎고_반격을_받는다()
    {
        var sword = Army(1, 1, new HexCoord(4, 0), new HexCoord(5, 0)); // 인접(사거리 1) — 반격 받음
        var city = Town(9, 2, new HexCoord(5, 0), wall: 6000);
        var garr = new List<GarrisonForce> { new(new CityId(9), "swordsman", 10000, 60) };

        var r = Siege().Resolve([sword], [city], garr);

        var ex = Assert.Single(r.Exchanges);
        Assert.True(ex.WallStanding);
        Assert.True(ex.WallDamage > 0, "성벽이 깎여야 한다");
        Assert.Equal(6000 - ex.WallDamage, r.Cities.Single().Wall);
        Assert.Equal(10000, r.Garrisons.Single().Troops); // 성벽이 버텨 수비 무손실
        Assert.True(r.Armies.Single().Pool.Active < 10000, "인접 공격 부대는 반격을 받는다");
    }

    [Fact]
    public void 공성_사거리2_공성병기는_반격을_받지않는다()
    {
        var catapult = Army(1, 1, new HexCoord(3, 0), new HexCoord(5, 0), troops: 5000, code: "catapult"); // 거리 2
        var city = Town(9, 2, new HexCoord(5, 0), wall: 6000);
        var garr = new List<GarrisonForce> { new(new CityId(9), "swordsman", 10000, 60) };

        var r = Siege().Resolve([catapult], [city], garr);

        Assert.True(r.Cities.Single().Wall < 6000, "투석기가 성벽을 깎는다");
        Assert.Equal(5000, r.Armies.Single().Pool.Active); // 반격 없음
    }

    [Fact]
    public void 공성_성벽이_무너지면_수비병력이_직격당한다()
    {
        var sword = Army(1, 1, new HexCoord(4, 0), new HexCoord(5, 0), troops: 20000);
        var city = Town(9, 2, new HexCoord(5, 0), wall: 0); // 이미 붕괴
        var garr = new List<GarrisonForce>
        {
            new(new CityId(9), "swordsman", 6000, 60),
            new(new CityId(9), "archer", 4000, 60),
        };

        var r = Siege().Resolve([sword], [city], garr);

        var ex = Assert.Single(r.Exchanges);
        Assert.False(ex.WallStanding);
        Assert.True(ex.TroopDamage > 0, "붕괴 후 수비 병력이 깎인다");
        // 손실이 병종에 병력 비례 분배: 검병(6/10)·궁병(4/10) 합이 총손실과 같다.
        var remaining = r.Garrisons.Sum(g => g.Troops);
        Assert.Equal(10000 - ex.TroopDamage, remaining);
        Assert.All(r.Garrisons, g => Assert.True(g.Troops > 0 || g.Troops == 0));
    }

    [Fact]
    public void 공성_사거리밖_행군모드_부대는_공성하지_않는다()
    {
        var marching = Army(1, 1, new HexCoord(4, 0), new HexCoord(5, 0)) with
        {
            Field = Army(1, 1, new HexCoord(4, 0), new HexCoord(5, 0)).Field with { Mode = UnitMode.March },
        };
        var far = Army(2, 1, new HexCoord(1, 0), new HexCoord(5, 0)); // 사거리 밖
        var city = Town(9, 2, new HexCoord(5, 0), wall: 6000);
        var garr = new List<GarrisonForce> { new(new CityId(9), "swordsman", 10000, 60) };

        var r = Siege().Resolve([marching, far], [city], garr);

        Assert.Empty(r.Exchanges);
        Assert.Equal(6000, r.Cities.Single().Wall);
    }

    [Fact]
    public void 공성_빈성_붕괴수비0은_교환이_없다()
    {
        var sword = Army(1, 1, new HexCoord(4, 0), new HexCoord(5, 0));
        var city = Town(9, 2, new HexCoord(5, 0), wall: 0);

        var r = Siege().Resolve([sword], [city], []);

        Assert.Empty(r.Exchanges); // 함락(점거)은 다음 단계
    }

    // ── 캠페인 통합(CampaignEngine) ──

    [Fact]
    public void 캠페인_한주진행이면_공성이_여러번_누적된다()
    {
        var sword = Army(1, 1, new HexCoord(2, 0), new HexCoord(5, 0), troops: 20000);
        var city = Town(9, 2, new HexCoord(5, 0), wall: 6000, size: CastleSize.Medium);
        var garr = new List<GarrisonForce> { new(new CityId(9), "swordsman", 10000, 60) };
        var s = new GameState(1, 1, new List<Faction>(), new List<City> { city }, new List<General>(),
            GarrisonForces: garr, FieldArmies: new List<CombatUnit> { sword });

        var after = Engine().AdvanceWeek(s, out _, out var sieges);

        Assert.True(sieges.Count >= 1, "한 주 동안 공성 교환이 일어난다");
        var c = after.Cities.Single();
        Assert.True(c.Wall < 6000, $"성벽이 깎였다: {c.Wall}");
        Assert.Equal(new CityId(9), c.Id);
    }
}
