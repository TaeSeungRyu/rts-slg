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
    [Fact]
    public void 선전포고는_도착후_발효되며_이동중_중복을_막는다()
    {
        var state = new GameState(5, 190, [], [City(1, 1, 0), City(2, 2, 3)], [Gen(1)],
            Postings: [At(1, 1, 1)]);
        var request = new CommandRequest(new CityId(1), CommandKind.DeclareWar, new GeneralId(1),
            TargetCity: new CityId(2));
        var service = new CommandService(B);

        var issued = service.Issue(state, request);

        Assert.True(issued.Ok, issued.Error);
        Assert.Empty(issued.State.DeclaredWars);
        var pending = Assert.Single(issued.State.Commands);
        Assert.Equal(CommandKind.DeclareWar, pending.Kind);
        Assert.Single(SaveService.Deserialize(SaveService.Serialize(issued.State)).Commands);
        Assert.False(service.Issue(issued.State, request).Ok);
        var world = new WorldEngine(Bal, B);
        var before = world.AdvanceDays(issued.State, pending.CompletionDay - issued.State.Day - 1);
        Assert.Empty(before.DeclaredWars);
        var arrived = world.AdvanceDays(before, 1);
        Assert.Single(arrived.DeclaredWars);
    }

    [Fact]
    public void 선전포고는_100일간_유효하고_만료후_다시_가능하다()
    {
        var state = new GameState(5, 190, [], [City(1, 1, 0), City(2, 2, 3)], [Gen(1)],
            Postings: [At(1, 1, 1)]);
        var request = new CommandRequest(new CityId(1), CommandKind.DeclareWar, new GeneralId(1),
            TargetCity: new CityId(2));
        var service = new CommandService(B);
        var issued = service.Issue(state, request).State;
        var pending = Assert.Single(issued.Commands);
        var arrived = new WorldEngine(Bal, B).AdvanceDays(issued, pending.CompletionDay - issued.Day);
        var declaration = Assert.Single(arrived.DeclaredWars);

        Assert.True(declaration.ActiveOn(arrived.Day + 99));
        Assert.False(declaration.ActiveOn(arrived.Day + 100));
        var expired = arrived with { Day = arrived.Day + 100 };
        Assert.True(service.Issue(expired, request).Ok);
    }

    [Fact]
    public void 동맹에는_선전포고할_수_없고_사절_이동중_동맹이_맺어져도_발효되지_않는다()
    {
        var state = new GameState(5, 190, [], [City(1, 1, 0), City(2, 2, 3)], [Gen(1)],
            Postings: [At(1, 1, 1)]);
        var request = new CommandRequest(new CityId(1), CommandKind.DeclareWar, new GeneralId(1),
            TargetCity: new CityId(2));
        var alliance = FactionAlliance.Create(new FactionId(1), new FactionId(2), state.Day);
        var service = new CommandService(B);

        Assert.False(service.Issue(state with { FactionAlliances = [alliance] }, request).Ok);
        var issued = service.Issue(state, request);
        Assert.True(issued.Ok, issued.Error);
        var inTransitAllied = issued.State with { FactionAlliances = [alliance] };
        var arrived = new WorldEngine(Bal, B).AdvanceDays(inTransitAllied,
            issued.State.Commands.Single().CompletionDay - state.Day);
        Assert.Empty(arrived.DeclaredWars);
    }

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
    public void 선전포고_대상_반경4칸에_아군이_있으면_거절하고_5칸이면_허용한다()
    {
        var target = City(2, 2, 20) with { Castle = CastleSize.Small };
        var state = new GameState(1, 190, [], [City(1, 1, 0), target], [Gen(1)],
            Postings: [At(1, 1, 1)]);
        var request = new CommandRequest(new CityId(1), CommandKind.DeclareWar, new GeneralId(1),
            TargetCity: target.Id);
        CombatUnit Army(int q) => new(new FieldUnit(new UnitId(1), new FactionId(1),
                new HexCoord(q, 0), 2, 2, 1, MovementDomain.Land, UnitMode.March, null, 1),
            new CombatStats(1000, 10, 10), new TroopPool(1000, 0), UnitCombatState.Create(50));
        var service = new CommandService(B);

        var blocked = service.Issue(state with { FieldArmies = [Army(24)] }, request);
        var allowed = service.Issue(state with { FieldArmies = [Army(25)] }, request);

        Assert.False(blocked.Ok);
        Assert.Contains("4칸", blocked.Error);
        Assert.Empty(blocked.State.DeclaredWars);
        Assert.True(allowed.Ok, allowed.Error);
    }

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
    public void 동맹이_체결되면_기존_동맹_도시_공격명령은_대기로_전환된다()
    {
        var enemyCity = City(2, 2, 6);
        var field = new FieldUnit(new UnitId(1), new FactionId(1), new HexCoord(1, 0),
            2, 2, 1, MovementDomain.Land, UnitMode.Attack, enemyCity.Position, 1);
        var army = new CombatUnit(field, new CombatStats(1000, 10, 10), new TroopPool(1000, 0),
            UnitCombatState.Create(50));
        var state = new GameState(1, 190, [], [City(1, 1, 0), enemyCity], [Gen(1, politics: 100)],
            Postings: [At(1, 1, 1)], FieldArmies: [army]);
        var issued = new CommandService(B).Issue(state,
            new CommandRequest(new CityId(1), CommandKind.FormAlliance, new GeneralId(1),
                TargetFaction: new FactionId(2)));
        var arrived = new WorldEngine(Bal, B, random: new FixedRandom(0)).AdvanceDays(issued.State,
            issued.State.Commands.Single().CompletionDay - state.Day);

        Assert.True(arrived.AreAllied(new FactionId(1), new FactionId(2)));
        Assert.Equal(UnitMode.Standby, arrived.Armies.Single().Field.Mode);
        Assert.Null(arrived.Armies.Single().Field.Target);
    }

    [Fact]
    public void 동맹_성공률은_내정레벨의_유효정치를_반영한다()
    {
        var envoy = Gen(1, politics: 49) with { AdminLevel = 4 };
        var state = new GameState(1, 1, [], [City(1, 1, 0), City(2, 2, 3)], [envoy],
            Postings: [At(1, 1, 1)]);
        var issued = new CommandService(B).Issue(state,
            new CommandRequest(new CityId(1), CommandKind.FormAlliance, envoy.Id, TargetFaction: new FactionId(2)));
        var world = new WorldEngine(Bal, B, random: new FixedRandom(49));

        var done = world.AdvanceDays(issued.State, issued.State.Commands.Single().CompletionDay - issued.State.Day);

        Assert.True(done.AreAllied(new FactionId(1), new FactionId(2)));
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

    [Fact]
    public void 군사_예측은_지력_확률로_성공여부를_맞춘다()
    {
        Assert.True(DiplomacyRules.AdvisorPredictsSuccess(95, 100, new FixedRandom(99)));
        Assert.True(DiplomacyRules.AdvisorPredictsSuccess(95, 70, new FixedRandom(69)));
        Assert.False(DiplomacyRules.AdvisorPredictsSuccess(95, 70, new FixedRandom(70)));
    }
}
