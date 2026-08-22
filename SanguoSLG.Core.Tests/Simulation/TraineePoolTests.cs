namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>신병 풀 분리(design-unit-state "신병 풀 분리") — 징병은 신병 풀, 훈련 50 도달 시 승격, 출전은 정규 풀만.</summary>
public class TraineePoolTests
{
    private static readonly CommandBalance B = new();

    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    private static CommandService Service() => new(B, Troops);

    private static WorldEngine World() => new(new BalanceConfig(MonthlyTaxPerCity: 0), B);

    private static General Pol(int id, int politics) => new(
        new GeneralId(id), $"g{id}",
        new Dictionary<TroopClass, AptitudeGrade>(), Might: 50, Intellect: 50, Politics: politics);

    private static General Mig(int id, int might) => new(
        new GeneralId(id), $"m{id}",
        new Dictionary<TroopClass, AptitudeGrade>(), Might: might, Intellect: 50, Politics: 50);

    private static City Town(int id) =>
        new(new CityId(id), $"c{id}", new HexCoord(0, 0), new FactionId(1), 5000, CastleSize.Medium,
            Gold: 1000, Population: 100_000, Ore: 50_000);

    private static GameState State(IEnumerable<General> generals, IEnumerable<GarrisonForce>? garrisons = null) =>
        new(1, 1, new List<Faction>(), new List<City> { Town(1) }, generals.ToList(),
            GarrisonForces: garrisons?.ToList());

    [Fact]
    public void 징병은_신병풀로_들어가_정규풀을_희석하지_않는다()
    {
        var s0 = State(new[] { Pol(1, 100) },
            new[] { new GarrisonForce(new CityId(1), "swordsman", 2000, 60) });

        var issued = Service().Issue(s0, new CommandRequest(new CityId(1), CommandKind.Conscript, new GeneralId(1), TroopCode: "swordsman"));
        Assert.True(issued.Ok, issued.Error);
        var done = World().AdvanceDays(issued.State, 7);

        var regular = done.Garrisons.Single(g => g.TroopCode == "swordsman" && !g.Trainee);
        var trainee = done.Garrisons.Single(g => g.TroopCode == "swordsman" && g.Trainee);
        Assert.Equal(2000, regular.Troops);
        Assert.Equal(60, regular.TrainingLevel); // 희석 없음
        Assert.Equal(3000, trainee.Troops);      // 인구 3% × 동원 100%
        Assert.Equal(0, trainee.TrainingLevel);
    }

    [Fact]
    public void 신병풀_훈련이_50에_도달하면_정규풀로_승격된다()
    {
        var s0 = State(new[] { Mig(2, 80) }, new[]
        {
            new GarrisonForce(new CityId(1), "swordsman", 1000, 80),
            new GarrisonForce(new CityId(1), "swordsman", 3000, 45, Trainee: true),
        });

        var issued = Service().Issue(s0, new CommandRequest(new CityId(1), CommandKind.Train, new GeneralId(2),
            TroopCode: "swordsman", TraineePool: true));
        Assert.True(issued.Ok, issued.Error);
        var done = World().AdvanceDays(issued.State, 7);

        Assert.DoesNotContain(done.Garrisons, g => g.Trainee); // 승격 — 신병 풀 소멸
        var regular = done.Garrisons.Single(g => g.TroopCode == "swordsman");
        Assert.Equal(4000, regular.Troops);
        Assert.Equal(60, regular.TrainingLevel); // (1000×80 + 3000×53) / 4000 = 59.75 → 반올림 60
    }

    [Fact]
    public void 훈련은_지정한_풀에만_적용된다()
    {
        var s0 = State(new[] { Mig(2, 80) }, new[]
        {
            new GarrisonForce(new CityId(1), "swordsman", 1000, 60),
            new GarrisonForce(new CityId(1), "swordsman", 3000, 10, Trainee: true),
        });

        var issued = Service().Issue(s0, new CommandRequest(new CityId(1), CommandKind.Train, new GeneralId(2),
            TroopCode: "swordsman", TraineePool: false));
        var done = World().AdvanceDays(issued.State, 7);

        Assert.Equal(68, done.Garrisons.Single(g => !g.Trainee).TrainingLevel); // 정규 60+8
        Assert.Equal(10, done.Garrisons.Single(g => g.Trainee).TrainingLevel); // 신병 그대로
    }

    [Fact]
    public void 출전은_신병풀만_있으면_거부된다()
    {
        var actives = new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory());
        var passives = new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory());
        var deployer = new DeployService(B, Troops, actives, passives);
        var s0 = new GameState(1, 1, new List<Faction>(), new List<City> { Town(1) },
            new List<General> { Pol(1, 80) },
            Postings: new List<GeneralPosting> { new(new GeneralId(1), new FactionId(1), new CityId(1)) },
            GarrisonForces: new List<GarrisonForce>
            {
                new(new CityId(1), "swordsman", 3000, 0, Trainee: true),
            });

        var r = deployer.Deploy(s0, new DeployRequest(new CityId(1), "swordsman", 1000, new GeneralId(1)));

        Assert.False(r.Ok);
        Assert.Contains("신병", r.Error);
    }
}
