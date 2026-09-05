namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class DiplomacyTests
{
    private sealed class FixedRandom(params int[] values) : IRandomSource
    {
        private readonly int[] _values = values;
        private int _i;
        public int Next(int minInclusive, int maxExclusive) => _values[_i++ % _values.Length];
    }

    private static readonly CommandBalance B = new() { AllianceGoldCost = 300 };
    private static readonly BalanceConfig Bal = new(MonthlyTaxPerCity: 0);
    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());
    private static readonly IReadOnlyList<ActiveSkill> Actives =
        new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory());
    private static readonly IReadOnlyList<PassiveSkill> Passives =
        new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory());

    private static General Gen(int id, int politics = 80) => new(
        new GeneralId(id), $"g{id}", new Dictionary<TroopClass, AptitudeGrade>(),
        Might: 50, Intellect: 50, Politics: politics);

    private static City City(int id, int owner, int q, int gold = 1000) =>
        new(new CityId(id), $"c{id}", new HexCoord(q, 0), new FactionId(owner), 3000,
            CastleSize.Medium, Gold: gold);

    private static GeneralPosting At(int general, int faction, int city) =>
        new(new GeneralId(general), new FactionId(faction), new CityId(city));

    [Fact]
    public void 동맹_발행은_금을_소비하고_거리비례_소요일로_예약된다()
    {
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { City(1, 1, 0, gold: 1000), City(2, 2, 6) },
            new List<General> { Gen(1) },
            Postings: new List<GeneralPosting> { At(1, 1, 1) });

        var issued = new CommandService(B).Issue(s,
            new CommandRequest(new CityId(1), CommandKind.FormAlliance, new GeneralId(1), TargetFaction: new FactionId(2)));

        Assert.True(issued.Ok, issued.Error);
        Assert.Equal(700, issued.State.Cities.Single(c => c.Id == new CityId(1)).Gold);
        var cmd = Assert.Single(issued.State.Commands);
        Assert.Equal(CommandKind.FormAlliance, cmd.Kind);
        Assert.Equal(new FactionId(2), cmd.TargetFaction);
        Assert.Equal(11, cmd.CompletionDay - cmd.StartDay);
    }

    [Fact]
    public void 동맹_성공은_완료일에_계산되어_동맹상태와_이벤트를_남긴다()
    {
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { City(1, 1, 0), City(2, 2, 3) },
            new List<General> { Gen(1, politics: 80) },
            Postings: new List<GeneralPosting> { At(1, 1, 1) });
        var issued = new CommandService(B).Issue(s,
            new CommandRequest(new CityId(1), CommandKind.FormAlliance, new GeneralId(1), TargetFaction: new FactionId(2)));

        var world = new WorldEngine(Bal, B, random: new FixedRandom(79));
        var done = world.AdvanceDays(issued.State, issued.State.Commands.Single().CompletionDay - issued.State.Day);

        Assert.True(done.AreAllied(new FactionId(1), new FactionId(2)));
        Assert.Contains(world.LastEvents, e => e.Kind == WorldEventKind.AllianceSuccess && e.Code == "2");
    }

    [Fact]
    public void 동맹_실패는_동맹상태를_만들지_않고_실패이벤트를_남긴다()
    {
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { City(1, 1, 0), City(2, 2, 3) },
            new List<General> { Gen(1, politics: 50) },
            Postings: new List<GeneralPosting> { At(1, 1, 1) });
        var issued = new CommandService(B).Issue(s,
            new CommandRequest(new CityId(1), CommandKind.FormAlliance, new GeneralId(1), TargetFaction: new FactionId(2)));

        var world = new WorldEngine(Bal, B, random: new FixedRandom(50));
        var done = world.AdvanceDays(issued.State, issued.State.Commands.Single().CompletionDay - issued.State.Day);

        Assert.False(done.AreAllied(new FactionId(1), new FactionId(2)));
        Assert.Contains(world.LastEvents, e => e.Kind == WorldEventKind.AllianceFail && e.Code == "2");
    }

    [Fact]
    public void 동맹파기는_즉시_동맹을_해제하고_공격을_다시_허용한다()
    {
        var s = new GameState(1, 1, new List<Faction>(),
            new List<City> { City(1, 1, 0), City(2, 2, 3) },
            new List<General> { Gen(1) },
            Postings: new List<GeneralPosting> { At(1, 1, 1) },
            GarrisonForces: new List<GarrisonForce> { new(new CityId(1), "swordsman", 10000, 60) },
            FactionAlliances: new List<FactionAlliance> { FactionAlliance.Create(new FactionId(1), new FactionId(2), 1) });

        var broken = new CommandService(B).Issue(s,
            new CommandRequest(new CityId(1), CommandKind.BreakAlliance, new GeneralId(1), TargetFaction: new FactionId(2)));

        Assert.True(broken.Ok, broken.Error);
        Assert.False(broken.State.AreAllied(new FactionId(1), new FactionId(2)));

        var deploy = new DeployService(B, Troops, Actives, Passives).Deploy(broken.State,
            new DeployRequest(new CityId(1), "swordsman", 5000, new GeneralId(1),
                Mode: UnitMode.Attack, Target: new HexCoord(3, 0)));
        Assert.True(deploy.Ok, deploy.Error);
    }
}
