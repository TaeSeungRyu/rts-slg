namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>경제전 C단계 — 자원 시설(광산·목장·상원) 파괴/수리 + 일반 시설 잔해 수리.</summary>
public class FacilityRepairTests
{
    private static readonly CommandBalance B = new();
    private static readonly BalanceConfig Bal = new(MonthlyTaxPerCity: 0);

    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static CommandService Service() => new(B, T.Values.ToList(), Bal);

    private static General Pol(int id) => new(
        new GeneralId(id), $"g{id}", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: 50, Intellect: 50, Politics: 80);

    private static GameState State(City city) =>
        new(1, 1, new List<Faction>(), new List<City> { city }, new List<General> { Pol(1) });

    private static CombatUnit Besieger(HexCoord pos, HexCoord target)
    {
        var t = T["swordsman"];
        var field = new FieldUnit(new UnitId(1), new FactionId(9), pos,
            t.MovementPerDay, t.Detection, t.RangeUnit, MovementDomain.Land, UnitMode.Attack, target, 1, t.RangeCastle);
        var stats = CombatStatsBuilder.BuildField(t, AptitudeGrade.A, 0, TerrainType.River, 8000);
        return new CombatUnit(field, stats, new TroopPool(8000, 0), UnitCombatState.Create(60),
            60, 60, 8000, t.Class, TroopCode: "swordsman");
    }

    // ── 자원 시설 파괴(약탈 확장) ──

    [Fact]
    public void 약탈_일반시설이_없으면_자원시설을_부수고_생산이_중단된다()
    {
        var city = new City(new CityId(1), "c1", new HexCoord(5, 0), new FactionId(1), 1000, CastleSize.Medium,
            Gold: 500, ProducesOre: true);
        var looter = Besieger(new HexCoord(4, 0), city.Position);

        var r = new CityPlunder(B).Resolve([looter], [city]);

        var report = Assert.Single(r.Reports);
        Assert.Equal(("mine", 200), (report.Facility, report.Gold)); // 정액 노획
        Assert.True(r.Cities.Single().MineDestroyed);

        // 파괴된 광산은 월말 산출이 없다.
        var world = new WorldEngine(Bal, B);
        var s = new GameState(1, 1, new List<Faction>(), r.Cities.ToList(), new List<General>());
        var afterMonth = world.AdvanceDays(s, 30);
        Assert.Equal(r.Cities.Single().Ore, afterMonth.Cities.Single().Ore); // 산출 0
    }

    [Fact]
    public void 약탈_부서진_일반시설은_잔해로_남는다()
    {
        var city = new City(new CityId(1), "c1", new HexCoord(5, 0), new FactionId(1), 1000, CastleSize.Medium,
            Paddies: 1);
        var looter = Besieger(new HexCoord(4, 0), city.Position);

        var r = new CityPlunder(B).Resolve([looter], [city]);

        var c = r.Cities.Single();
        Assert.Equal((0, 1), (c.Paddies, c.RuinedPaddies)); // 잔해로 전환
    }

    // ── 시설 수리 ──

    [Fact]
    public void 수리_잔해를_건설비_절반으로_복구한다()
    {
        var city = new City(new CityId(1), "c1", new HexCoord(0, 0), new FactionId(1), 1000, CastleSize.Medium,
            Gold: 1000, RuinedPaddies: 1);
        var world = new WorldEngine(Bal, B);

        var r = Service().Issue(State(city), new CommandRequest(new CityId(1), CommandKind.Repair, new GeneralId(1), Facility: "paddy"));
        Assert.True(r.Ok, r.Error);
        Assert.Equal(1000 - 150, r.State.Cities.Single().Gold); // 논 300×50%

        var done = world.AdvanceDays(r.State, B.RepairDays);
        var c = done.Cities.Single();
        Assert.Equal((1, 0), (c.Paddies, c.RuinedPaddies)); // 복구
    }

    [Fact]
    public void 수리_파괴된_광산을_정액으로_복구하면_생산이_재개된다()
    {
        var city = new City(new CityId(1), "c1", new HexCoord(0, 0), new FactionId(1), 1000, CastleSize.Medium,
            Gold: 1000, ProducesOre: true, MineDestroyed: true);
        var world = new WorldEngine(Bal, B);

        var r = Service().Issue(State(city), new CommandRequest(new CityId(1), CommandKind.Repair, new GeneralId(1), Facility: "mine"));
        Assert.True(r.Ok, r.Error);
        Assert.Equal(1000 - 400, r.State.Cities.Single().Gold); // 자원 시설 정액 400

        var done = world.AdvanceDays(r.State, B.RepairDays);
        Assert.False(done.Cities.Single().MineDestroyed);

        var afterMonth = world.AdvanceDays(done, 30 - done.Day % 30);
        Assert.True(afterMonth.Cities.Single().Ore > 0, "수리 후 광석 산출 재개");
    }

    [Fact]
    public void 수리_파괴시설이_없으면_거부된다()
    {
        var city = new City(new CityId(1), "c1", new HexCoord(0, 0), new FactionId(1), 1000, CastleSize.Medium, Gold: 1000);
        var r = Service().Issue(State(city), new CommandRequest(new CityId(1), CommandKind.Repair, new GeneralId(1), Facility: "farm"));
        Assert.False(r.Ok);
        Assert.Contains("파괴 시설", r.Error);
    }

    [Fact]
    public void 건설_잔해가_슬롯을_차지해_가득차면_수리를_유도한다()
    {
        // 중성 슬롯 6: 시설 3 + 잔해 3 = 가득 → 신축 거부, 수리는 가능.
        var city = new City(new CityId(1), "c1", new HexCoord(0, 0), new FactionId(1), 1000, CastleSize.Medium,
            Gold: 5000, Paddies: 2, Farms: 1, RuinedPaddies: 2, RuinedVillages: 1);
        var s = State(city);

        var build = Service().Issue(s, new CommandRequest(new CityId(1), CommandKind.Build, new GeneralId(1), Facility: "farm"));
        Assert.False(build.Ok);
        Assert.Contains("슬롯", build.Error);

        var repair = Service().Issue(s, new CommandRequest(new CityId(1), CommandKind.Repair, new GeneralId(1), Facility: "village"));
        Assert.True(repair.Ok, repair.Error);
    }
}
