namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>건물 수리 A단계 — 성벽 수리(명령당 최대치 25%·공방 +25%p·비용=회복÷5·15일).</summary>
public class WallRepairTests
{
    private static readonly CommandBalance B = new();
    private static readonly BalanceConfig Bal = new(MonthlyTaxPerCity: 0);

    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    private static CommandService Service() => new(B, Troops, Bal);

    private static General Pol(int id) => new(
        new GeneralId(id), $"g{id}", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: 50, Intellect: 50, Politics: 70);

    private static City Town(int id, int wall, bool workshop = false, int gold = 5000, int wallLevel4 = 1) =>
        new(new CityId(id), $"c{id}", new HexCoord(id, 0), new FactionId(1), 3000, CastleSize.Medium,
            Gold: gold, Wall: wall, Workshop: workshop);

    private static GameState State(City city, IEnumerable<FactionResearch>? research = null) =>
        new(1, 1, new List<Faction>(), new List<City> { city }, new List<General> { Pol(1) },
            ResearchTracks: research?.ToList());

    private static CommandRequest Req() =>
        new(new CityId(1), CommandKind.Repair, new GeneralId(1), TroopCode: FactionResearch.WallCode);

    private static readonly IReadOnlyList<AdminSkill> AdminSkills =
        new AdminSkillLoader().LoadFromDirectory(TestData.DataDirectory());

    [Fact]
    public void 축성_태수면_성벽_수리_회복량이_커진다()
    {
        // 최대 1200(레벨0). wall=0 → 손상 1200. 회복 = 기본 25% + 축성 T3 30%p = 55% → 1200×55% = 660.
        var gov = new General(new GeneralId(1), "gov", new Dictionary<TroopClass, AptitudeGrade>(),
            Might: 50, Intellect: 50, Politics: 70, AdminPassives: new[] { new GeneralSkill("builder", 3) });
        var city = Town(1, wall: 0, gold: 5000);
        var s = new GameState(1, 1, new List<Faction>(), new List<City> { city with { Governor = gov.Id } },
            new List<General> { gov },
            Postings: new List<GeneralPosting> { new(gov.Id, new FactionId(1), new CityId(1)) });

        var withGov = new CommandService(B, Troops, Bal, AdminSkills).Issue(s, Req());
        var without = Service().Issue(State(Town(1, wall: 0, gold: 5000)), Req());

        Assert.True(withGov.Ok, withGov.Error);
        Assert.Equal(1200 * 25 / 100, without.State.Commands.Single().Amount); // 축성 없음 = 300
        Assert.Equal(1200 * 55 / 100, withGov.State.Commands.Single().Amount);  // 축성 T3 = 660
    }

    [Fact]
    public void 발행_손상된_성벽을_수리예약하고_금을_차감한다()
    {
        // 성벽 연구 미설정(레벨 0) → 중성 최대 = 6000×20% = 1200. 현재 600 → 손상 600.
        // 회복 25% = 1200×25% = 300, 손상 600보다 작으니 300 회복. 비용 = 300÷5 = 60.
        var s = State(Town(1, wall: 600, gold: 5000));
        var r = Service().Issue(s, Req());

        Assert.True(r.Ok, r.Error);
        Assert.Equal(5000 - 60, r.State.Cities.Single().Gold);
        Assert.Equal(300, r.State.Commands.Single().Amount); // 회복량
    }

    [Fact]
    public void 루프_완료시_성벽이_회복되고_최대치를_넘지_않는다()
    {
        var world = new WorldEngine(Bal, B);
        var s = State(Town(1, wall: 600, gold: 5000));

        var issued = Service().Issue(s, Req());
        var done = world.AdvanceDays(issued.State, B.RepairDays);

        Assert.Equal(900, done.Cities.Single().Wall); // 600 + 300
        Assert.Empty(done.Commands);
    }

    [Fact]
    public void 발행_공방이_있으면_회복량이_커진다()
    {
        // 공방 +25%p → 회복 50%. 최대 1200×50% = 600, 손상 600과 같으니 600 회복(완전).
        var s = State(Town(1, wall: 600, workshop: true, gold: 5000));
        var r = Service().Issue(s, Req());

        Assert.True(r.Ok, r.Error);
        Assert.Equal(600, r.State.Commands.Single().Amount);
        Assert.Equal(600 / 5, 5000 - r.State.Cities.Single().Gold); // 비용 120
    }

    [Fact]
    public void 발행_손상이_없으면_거부된다()
    {
        var s = State(Town(1, wall: 1200)); // 최대치와 같음(레벨0 = 1200)
        var r = Service().Issue(s, Req());
        Assert.False(r.Ok);
        Assert.Contains("손상", r.Error);
    }

    [Fact]
    public void 발행_금이_부족하면_거부된다()
    {
        var s = State(Town(1, wall: 600, gold: 10));
        var r = Service().Issue(s, Req());
        Assert.False(r.Ok);
        Assert.Contains("금", r.Error);
    }

    [Fact]
    public void 발행_성벽연구가_높으면_최대치가_커져_더_많이_수리한다()
    {
        // 성벽 연구 4단계 → 중성 최대 6000(100%). 현재 600 → 회복 25% = 1500.
        var s = State(Town(1, wall: 600, gold: 5000),
            research: new[] { new FactionResearch(new FactionId(1), FactionResearch.WallCode, 4) });
        var r = Service().Issue(s, Req());

        Assert.True(r.Ok, r.Error);
        Assert.Equal(1500, r.State.Commands.Single().Amount);
    }
}
