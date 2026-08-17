namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>10c 충성도·포로 기반 — 충성 데이터·포로 전환·세력 소멸·포로 주간 충성 하락.</summary>
public class FactionLifecycleTests
{
    private static General Gen(int id, int loyalty = 100) => new(
        new GeneralId(id), $"g{id}", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: 60, Intellect: 60, Politics: 60, Loyalty: loyalty);

    private static City Town(int id, int owner, HexCoord pos) =>
        new(new CityId(id), $"c{id}", pos, new FactionId(owner), 0);

    private static GameState State(IEnumerable<City> cities, IEnumerable<General> generals,
        IEnumerable<GeneralPosting>? postings = null, IEnumerable<Prisoner>? captives = null) =>
        new(1, 1, new List<Faction>(), cities.ToList(), generals.ToList(),
            Postings: postings?.ToList(), Captives: captives?.ToList());

    // ── 데이터 로드 ──

    [Fact]
    public void 로드_충성도는_기본_100이고_충신은_시작값이_높다()
    {
        var generals = new GeneralLoader().LoadFromDirectory(TestData.DataDirectory());

        Assert.All(generals, g => Assert.True(g.Loyalty >= 100));
        Assert.Equal(200, generals.Single(g => g.Name == "관우").Loyalty);
        Assert.Contains(generals, g => g.Loyalty == 100); // 대다수는 기본값
    }

    // ── 충성 증감 ──

    [Fact]
    public void 충성증감_더하고_빼되_하한은_0이고_완충은_유지된다()
    {
        var s = State([], [Gen(1, 200), Gen(2, 5)]);

        var down = FactionLifecycle.AdjustLoyalty(s, new GeneralId(1), -30);
        Assert.Equal(170, down.LoyaltyOf(new GeneralId(1))); // 100 초과 완충 보존

        var floored = FactionLifecycle.AdjustLoyalty(s, new GeneralId(2), -20);
        Assert.Equal(0, floored.LoyaltyOf(new GeneralId(2))); // 하한 0
    }

    // ── 포로 ──

    [Fact]
    public void 포로_배속이_해제되고_억류원세력이_기록된다()
    {
        var s = State([Town(1, 1, new HexCoord(0, 0))], [Gen(1)],
            postings: [new GeneralPosting(new GeneralId(1), new FactionId(1), new CityId(1))]);

        var after = FactionLifecycle.MakePrisoner(s, new GeneralId(1), holder: new FactionId(2), origin: new FactionId(1));

        Assert.Null(after.PostingOf(new GeneralId(1)));       // 배속 해제
        var p = after.PrisonerOf(new GeneralId(1));
        Assert.NotNull(p);
        Assert.Equal((new FactionId(2), new FactionId(1)), (p!.Holder, p.Origin));
        Assert.Single(after.PrisonersHeldBy(new FactionId(2)));
    }

    // ── 세력 소멸 ──

    [Fact]
    public void 세력소멸_도시0이면_모든_장수가_재야가_되고_억류포로도_방출된다()
    {
        var s = State(
            [Town(1, 2, new HexCoord(0, 0))], // 도시는 전부 세력 2 소유 → 세력 1은 도시 0
            [Gen(1), Gen(2), Gen(3)],
            postings:
            [
                new GeneralPosting(new GeneralId(1), new FactionId(1), null),   // 세력 1 소속(출전)
                new GeneralPosting(new GeneralId(2), new FactionId(2), new CityId(1)),
            ],
            captives: [new Prisoner(new GeneralId(3), Holder: new FactionId(1), Origin: new FactionId(2))]);

        Assert.Equal(0, s.CityCount(new FactionId(1)));
        var after = FactionLifecycle.EliminateIfNoCities(s, new FactionId(1));

        Assert.Null(after.PostingOf(new GeneralId(1)));           // 세력 1 장수 → 재야
        Assert.NotNull(after.PostingOf(new GeneralId(2)));        // 세력 2 장수는 유지
        Assert.Empty(after.PrisonersHeldBy(new FactionId(1)));    // 세력 1 억류 포로 방출
        Assert.Null(after.PrisonerOf(new GeneralId(3)));
    }

    [Fact]
    public void 세력소멸_도시가_있으면_그대로다()
    {
        var s = State([Town(1, 1, new HexCoord(0, 0))], [Gen(1)],
            postings: [new GeneralPosting(new GeneralId(1), new FactionId(1), new CityId(1))]);

        var after = FactionLifecycle.EliminateIfNoCities(s, new FactionId(1));

        Assert.NotNull(after.PostingOf(new GeneralId(1)));
    }

    // ── 포로 주간 충성 하락(CampaignEngine) ──

    [Fact]
    public void 포로_한주_진행마다_충성이_1씩_내린다()
    {
        var movement = new MovementSimulator(new PassabilityMap(new HexMap(0, 10, -5, 5), [], []));
        var field = new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), 70));
        var engine = new CampaignEngine(field, new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 100)));

        var s = State([Town(1, 1, new HexCoord(0, 0))], [Gen(9, 180)],
            captives: [new Prisoner(new GeneralId(9), Holder: new FactionId(1), Origin: new FactionId(2))]);

        s = engine.AdvanceWeek(s, out _);
        Assert.Equal(179, s.LoyaltyOf(new GeneralId(9)));
        s = engine.AdvanceWeek(s, out _);
        Assert.Equal(178, s.LoyaltyOf(new GeneralId(9)));
    }
}
