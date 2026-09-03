namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>11a 병종 연구 — 세력 단위·공방 게이트·지력 기간·완료 +1단계·부대 스탯 반영.</summary>
public class ResearchSystemTests
{
    private static readonly CommandBalance B = new();

    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    private static CommandService Service() => new(B, Troops);

    private static General Wit(int id, int intellect) => new(
        new GeneralId(id), $"g{id}", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: 50, Intellect: intellect, Politics: 50);

    private static City Town(int id, bool workshop, int gold = 5000) =>
        new(new CityId(id), $"c{id}", new HexCoord(id, 0), new FactionId(1), 3000, CastleSize.Medium,
            Gold: gold, Workshop: workshop);

    private static GameState State(IEnumerable<City> cities, IEnumerable<General> generals) =>
        new(1, 1, new List<Faction>(), cities.ToList(), generals.ToList());

    [Fact]
    public void 발행_공방이_없어도_전투교리_연구를_시작할수있다()
    {
        var s = State(new[] { Town(1, workshop: false) }, new[] { Wit(1, 70) });
        var r = Service().Issue(s, new CommandRequest(new CityId(1), CommandKind.Research, new GeneralId(1), TroopCode: "swordsman"));
        Assert.True(r.Ok, r.Error);
        Assert.Single(r.State.Commands);
    }

    [Fact]
    public void 발행_금을_예약하고_지력이_높으면_기간이_짧다()
    {
        var s = State(new[] { Town(1, workshop: true, gold: 5000) }, new[] { Wit(1, 100) });
        var r = Service().Issue(s, new CommandRequest(new CityId(1), CommandKind.Research, new GeneralId(1), TroopCode: "swordsman"));

        Assert.True(r.Ok, r.Error);
        Assert.Equal(5000 - 200, r.State.Cities.Single().Gold); // 1단계 비용 = 200×1
        var cmd = r.State.Commands.Single();
        Assert.Equal(20, cmd.CompletionDay - cmd.StartDay); // 지력 100 → 30 − 10 = 20일
    }

    [Fact]
    public void 루프_완료되면_세력_병종_연구가_1단계_오른다()
    {
        var world = new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0), B);
        var s = State(new[] { Town(1, workshop: true) }, new[] { Wit(1, 50) });

        var issued = Service().Issue(s, new CommandRequest(new CityId(1), CommandKind.Research, new GeneralId(1), TroopCode: "swordsman"));
        Assert.True(issued.Ok);
        Assert.Equal(0, issued.State.ResearchOf(new FactionId(1), "swordsman"));

        var done = world.AdvanceDays(issued.State, 30); // 지력 50 → 30일

        Assert.Equal(1, done.ResearchOf(new FactionId(1), "swordsman"));
        Assert.Equal("swordsman", done.Research.Single().TroopCode);
        Assert.Empty(done.Commands);
        Assert.False(done.IsGeneralBusy(new GeneralId(1)));
    }

    [Fact]
    public void 비용_8단계부터_지수적으로_급증한다()
    {
        // base 200·급증 7 기준: 7단계 1400(선형) → 8=3200 → 9=7200 → 10=16000.
        Assert.Equal(1400, CommandEfficiency.ResearchCost(7, B));
        Assert.Equal(3200, CommandEfficiency.ResearchCost(8, B));
        Assert.Equal(7200, CommandEfficiency.ResearchCost(9, B));
        Assert.Equal(16000, CommandEfficiency.ResearchCost(10, B));
    }

    [Fact]
    public void 발행_최종단계는_한_성_금고로는_모자랄수있다()
    {
        // 9단계 도달 세력이 10단계(비용 16000)를 공방 도시 금고 8000으로 발행 → 실패(부담 증대).
        var s = State(new[] { Town(1, workshop: true, gold: 8000) }, new[] { Wit(1, 90) })
            with { ResearchTracks = new List<FactionResearch> { new(new FactionId(1), "swordsman", 9) } };

        var r = Service().Issue(s, new CommandRequest(new CityId(1), CommandKind.Research, new GeneralId(1), TroopCode: "swordsman"));
        Assert.False(r.Ok);
        Assert.Contains("금", r.Error);

        // 금고 16000이면 발행 가능.
        var rich = s with { Cities = new List<City> { Town(1, workshop: true, gold: 16000) } };
        Assert.True(Service().Issue(rich, new CommandRequest(new CityId(1), CommandKind.Research, new GeneralId(1), TroopCode: "swordsman")).Ok);
    }

    [Fact]
    public void 발행_세력은_동시에_하나의_연구만_할수있다()
    {
        // 공방 도시 2개(같은 세력). 하나 연구 걸면 다른 공방에서 두 번째 연구 불가.
        var s = State(
            new[] { Town(1, workshop: true), Town(2, workshop: true) },
            new[] { Wit(1, 60), Wit(2, 60) })
            with
            {
                Postings = new List<GeneralPosting>
                {
                    new(new GeneralId(1), new FactionId(1), new CityId(1)),
                    new(new GeneralId(2), new FactionId(1), new CityId(2)),
                },
            };

        var first = Service().Issue(s, new CommandRequest(new CityId(1), CommandKind.Research, new GeneralId(1), TroopCode: "swordsman"));
        Assert.True(first.Ok, first.Error);

        var second = Service().Issue(first.State, new CommandRequest(new CityId(2), CommandKind.Research, new GeneralId(2), TroopCode: "archer"));
        Assert.False(second.Ok);
        Assert.Contains("하나의 연구", second.Error);
    }

    [Fact]
    public void 발행_자동담당자는_전투교리_연구를_수행할수있고_담당은_유지된다()
    {
        var city = Town(1, workshop: true, gold: 5000) with
        {
            DomesticOfficer = new GeneralId(1),
        };
        var s = State(new[] { city }, new[] { Wit(1, 80) });

        var r = Service().Issue(s,
            new CommandRequest(new CityId(1), CommandKind.Research, new GeneralId(1), TroopCode: "swordsman"));

        Assert.True(r.Ok, r.Error);
        Assert.True(r.State.IsGeneralBusy(new GeneralId(1)));
        Assert.Equal(new GeneralId(1), r.State.Cities.Single().DomesticOfficer);
        Assert.Single(r.State.Commands);
    }

    [Fact]
    public void 발행_최대단계면_더_연구할수없다()
    {
        var s = State(new[] { Town(1, workshop: true) }, new[] { Wit(1, 50) })
            with { ResearchTracks = new List<FactionResearch> { new(new FactionId(1), "swordsman", 10) } };

        var r = Service().Issue(s, new CommandRequest(new CityId(1), CommandKind.Research, new GeneralId(1), TroopCode: "swordsman"));
        Assert.False(r.Ok);
        Assert.Contains("최대", r.Error);
    }

    [Fact]
    public void 출전_연구단계가_부대_공방_스탯에_반영된다()
    {
        // 같은 병종·병력이라도 연구 9단계(누적 +10)면 무연구보다 공/방이 높다.
        var actives = new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory());
        var passives = new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory());
        var deployer = new DeployService(B, Troops, actives, passives);
        var general = new General(new GeneralId(1), "g1",
            new Dictionary<TroopClass, AptitudeGrade> { [TroopClass.Infantry] = AptitudeGrade.A },
            Might: 70, Intellect: 60, Politics: 60);

        GameState Make(int level) => new(1, 1, new List<Faction>(),
            new List<City> { new(new CityId(1), "c", new HexCoord(0, 0), new FactionId(1), 5000, CastleSize.Medium) },
            new List<General> { general },
            Postings: new List<GeneralPosting> { new(new GeneralId(1), new FactionId(1), new CityId(1)) },
            GarrisonForces: new List<GarrisonForce> { new(new CityId(1), "swordsman", 10000, 60) },
            ResearchTracks: new List<FactionResearch> { new(new FactionId(1), "swordsman", level) });

        var none = deployer.Deploy(Make(0), new DeployRequest(new CityId(1), "swordsman", 10000, new GeneralId(1)));
        var researched = deployer.Deploy(Make(9), new DeployRequest(new CityId(1), "swordsman", 10000, new GeneralId(1)));

        Assert.True(researched.Ok && none.Ok);
        var a0 = none.State.Armies.Single();
        var a9 = researched.State.Armies.Single();
        Assert.Equal(a0.Stats.AtkStat + 10, a9.Stats.AtkStat); // 9단계 누적 +10
        Assert.Equal(a0.Stats.DfStat + 10, a9.Stats.DfStat);
    }

    [Fact]
    public void 전투교리_연구단계는_공성_공격자_스탯에도_반영된다()
    {
        var template = Troops.Single(t => t.Code == "swordsman");

        var none = CombatStatsBuilder.BuildSiegeAttacker(
            template, AptitudeGrade.A, researchLevel: 0, TerrainType.Plains, troops: 10000);
        var researched = CombatStatsBuilder.BuildSiegeAttacker(
            template, AptitudeGrade.A, researchLevel: 9, TerrainType.Plains, troops: 10000);

        Assert.Equal(none.AtkBuilding + 10, researched.AtkBuilding);
        Assert.Equal(none.AtkUnit + 10, researched.AtkUnit);
        Assert.Equal(none.Df + 10, researched.Df);
    }
}
