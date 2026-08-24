namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>등용(design-general-lifecycle §6) — 2단계 판정(정치%→이탈%), 대상 3종, 실패 시 잡힘.</summary>
public class EnlistTests
{
    private static readonly CommandBalance B = new();
    private static readonly BalanceConfig Bal = new(MonthlyTaxPerCity: 0);

    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    private static CommandService Svc() => new(B, Troops, Bal);
    private static WorldEngine World(int seed) => new(Bal, B, random: new SeededRandomSource(seed));

    private static General Gen(int id, int faction_unused, int politics = 50, int loyalty = 100) => new(
        new GeneralId(id), $"g{id}", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: 50, Intellect: 50, Politics: politics, Loyalty: loyalty);

    private static City City(int id, int owner, int q = 0) =>
        new(new CityId(id), $"c{id}", new HexCoord(q, 0), new FactionId(owner), 1000, CastleSize.Medium, Gold: 1000);

    private static GeneralPosting At(int g, int faction, int? city) =>
        new(new GeneralId(g), new FactionId(faction), city is { } c ? new CityId(c) : null);

    // 완료까지 진행해 정산시킨다.
    private static GameState RunToDone(WorldEngine w, CommandService svc, GameState s, CommandRequest req, out bool ok)
    {
        var issued = svc.Issue(s, req);
        ok = issued.Ok;
        if (!issued.Ok) { return s; }
        var days = issued.State.Commands.Single().CompletionDay - issued.State.Day;
        return w.AdvanceDays(issued.State, days);
    }

    private static CommandRequest EnlistReq(int city, int recruiter, int target) =>
        new(new CityId(city), CommandKind.Enlist, new GeneralId(recruiter), TargetGeneral: new GeneralId(target));

    [Fact]
    public void 등용_포로를_영입하면_내_세력_수행도시_주둔이_된다()
    {
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { City(1, owner: 1) },
            new List<General> { Gen(1, 1, politics: 100), Gen(9, 2, loyalty: 0) },
            Postings: new List<GeneralPosting> { At(1, 1, 1) },
            Captives: new List<Prisoner> { new(new GeneralId(9), new FactionId(1), new FactionId(2)) });

        var done = RunToDone(World(1), Svc(), s, EnlistReq(1, 1, 9), out var ok);

        Assert.True(ok);
        Assert.Empty(done.Prisoners); // 포로에서 빠짐
        var post = done.PostingOf(new GeneralId(9));
        Assert.Equal(new FactionId(1), post!.Faction);
        Assert.Equal(new CityId(1), post.Location);
    }

    [Fact]
    public void 등용_정찰된_적성_장수를_영입하면_장수만_넘어온다()
    {
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { City(1, owner: 1, q: 0), City(2, owner: 2, q: 3) },
            new List<General> { Gen(1, 1, politics: 100), Gen(9, 2, loyalty: 0) },
            Postings: new List<GeneralPosting> { At(1, 1, 1), At(9, 2, 2) },
            ScoutedCities: new List<CityIntel> { new(new FactionId(1), new CityId(2)) });

        var done = RunToDone(World(1), Svc(), s, EnlistReq(1, 1, 9), out var ok);

        Assert.True(ok);
        var post = done.PostingOf(new GeneralId(9));
        Assert.Equal(new FactionId(1), post!.Faction);
        Assert.Equal(new CityId(1), post.Location); // 수행 도시로
    }

    [Fact]
    public void 등용_정찰안된_적성_장수는_발행이_거부된다()
    {
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { City(1, owner: 1, q: 0), City(2, owner: 2, q: 3) },
            new List<General> { Gen(1, 1, politics: 100), Gen(9, 2, loyalty: 0) },
            Postings: new List<GeneralPosting> { At(1, 1, 1), At(9, 2, 2) }); // 정찰 없음

        var r = Svc().Issue(s, EnlistReq(1, 1, 9));
        Assert.False(r.Ok);
    }

    [Fact]
    public void 등용_출전중_적장수를_영입하면_부대째_전향한다()
    {
        var t = Troops.First(x => x.Code == "swordsman");
        var enemyUnit = new CombatUnit(
            new FieldUnit(new UnitId(1), new FactionId(2), new HexCoord(3, 0), t.MovementPerDay, t.Detection,
                t.RangeUnit, MovementDomain.Land, UnitMode.March, null, 1, t.RangeCastle),
            CombatStatsBuilder.BuildField(t, AptitudeGrade.A, 0, TerrainType.River, 5000),
            new TroopPool(5000, 0), UnitCombatState.Create(50), 50, 50, 5000, t.Class,
            TroopCode: "swordsman", VanguardId: new GeneralId(9));

        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { City(1, owner: 1) },
            new List<General> { Gen(1, 1, politics: 100), Gen(9, 2, loyalty: 0) },
            Postings: new List<GeneralPosting> { At(1, 1, 1), At(9, 2, null) },
            FieldArmies: new List<CombatUnit> { enemyUnit });

        var done = RunToDone(World(1), Svc(), s, EnlistReq(1, 1, 9), out var ok);

        Assert.True(ok);
        var unit = done.Armies.Single();
        Assert.Equal(new FactionId(1), unit.Field.Owner); // 부대 소유 전환
        Assert.Equal(5000, unit.Pool.Active);             // 병력째
        Assert.Equal(new FactionId(1), done.PostingOf(new GeneralId(9))!.Faction);
    }

    [Fact]
    public void 등용_충성_100이상이면_절대_넘어오지_않는다()
    {
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { City(1, owner: 1) },
            new List<General> { Gen(1, 1, politics: 100), Gen(9, 2, loyalty: 100) },
            Postings: new List<GeneralPosting> { At(1, 1, 1) },
            Captives: new List<Prisoner> { new(new GeneralId(9), new FactionId(1), new FactionId(2)) });

        // 여러 시드로 돌려도 이탈 0% → 항상 실패(포로 그대로).
        for (var seed = 0; seed < 10; seed++)
        {
            var done = RunToDone(World(seed), Svc(), s, EnlistReq(1, 1, 9), out _);
            Assert.Single(done.Prisoners);
        }
    }

    [Fact]
    public void 등용_실패해도_충성90미만_대상이면_수행장수는_안잡힌다()
    {
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { City(1, owner: 1, q: 0), City(2, owner: 2, q: 3) },
            new List<General> { Gen(1, 1, politics: 0), Gen(9, 2, loyalty: 80) }, // 정치 0 → 항상 실패
            Postings: new List<GeneralPosting> { At(1, 1, 1), At(9, 2, 2) },
            ScoutedCities: new List<CityIntel> { new(new FactionId(1), new CityId(2)) });

        for (var seed = 0; seed < 10; seed++)
        {
            var done = RunToDone(World(seed), Svc(), s, EnlistReq(1, 1, 9), out _);
            Assert.Empty(done.Prisoners); // 충성<90 → 잡힘 없음
            Assert.Equal(new FactionId(1), done.PostingOf(new GeneralId(1))!.Faction); // 수행 장수 건재
        }
    }

    [Fact]
    public void 등용_실패시_충성90이상_적장수는_수행장수를_잡을수있다()
    {
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { City(1, owner: 1, q: 0), City(2, owner: 2, q: 3) },
            new List<General> { Gen(1, 1, politics: 0), Gen(9, 2, loyalty: 200) }, // 항상 실패·충신
            Postings: new List<GeneralPosting> { At(1, 1, 1), At(9, 2, 2) },
            ScoutedCities: new List<CityIntel> { new(new FactionId(1), new CityId(2)) });

        var captured = 0;
        for (var seed = 0; seed < 20; seed++)
        {
            var done = RunToDone(World(seed), Svc(), s, EnlistReq(1, 1, 9), out _);
            if (done.PrisonerOf(new GeneralId(1)) is { } p) { Assert.Equal(new FactionId(2), p.Holder); captured++; }
        }

        Assert.True(captured is > 0 and < 20, $"50% 확률로 잡힘 — 일부만 잡혀야 한다(잡힘 {captured}/20)");
    }

    [Fact]
    public void 성공확률_공식은_정치와_이탈의_곱이다()
    {
        Assert.Equal(18, EnlistOdds.SuccessPercent(90, 80)); // 90% × 20%
        Assert.Equal(0, EnlistOdds.SuccessPercent(100, 100)); // 이탈 0%
        Assert.Equal(80, EnlistOdds.SuccessPercent(80, 0));   // 80% × 100%
    }
}
