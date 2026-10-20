namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;
using Xunit;

public class AiOffensePlannerTests
{
    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());

    private static AiOffensePlanner Planner() => new(new DeployService(new CommandBalance(), Troops,
        new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
        new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory())), new AiConfig());

    private static AiOffensePlanner PlannerWithDiplomacy() => new(
        new DeployService(new CommandBalance(), Troops,
            new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
            new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory())),
        new AiConfig(), new CommandService(new CommandBalance(), Troops));

    private static AiOffensePlanner PlannerWithMap(HexMap map) => new(
        new DeployService(new CommandBalance(), Troops,
            new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
            new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory())),
        new AiConfig(), map: map);

    private static GameState Scenario(int own, int enemy, int provisions = 30000,
        int siege = 20000, int officers = 9, int distance = 10)
    {
        var origin = new City(new CityId(1), "origin", new HexCoord(0, 0), new FactionId(1),
            provisions, CastleSize.Medium, Wall: 3000);
        var target = new City(new CityId(2), "target", new HexCoord(distance, 0), new FactionId(2),
            3000, CastleSize.Medium, Wall: 1000);
        var generals = Enumerable.Range(1, officers).Select(i => new General(new GeneralId(i), $"g{i}",
            new Dictionary<TroopClass, AptitudeGrade>
            {
                [TroopClass.Infantry] = AptitudeGrade.A,
                [TroopClass.Archer] = AptitudeGrade.A,
                [TroopClass.Siege] = AptitudeGrade.A,
                [TroopClass.Supply] = AptitudeGrade.A,
            }, 80, 80, 80)).ToList();
        return new GameState(1, 190, [], [origin, target], generals,
            Postings: generals.Select(g => new GeneralPosting(g.Id, new FactionId(1), origin.Id)).ToList(),
            ScoutedCities: [new CityIntel(new FactionId(1), target.Id)],
            GarrisonForces:
            [
                new(origin.Id, "swordsman", own - siege, 60),
                new(origin.Id, "catapult", siege, 60),
                new(target.Id, "swordsman", enemy, 60),
            ]);
    }

    public static IEnumerable<object[]> LongRunCases =>
        from seed in Enumerable.Range(0, 20)
        from horizon in new[] { 7, 30, 60 }
        select new object[] { seed, horizon };

    public static IEnumerable<object[]> ForceMatrixCases =>
        from own in new[] { 30_000, 50_000, 100_000 }
        from enemy in new[] { 30_000, 50_000, 100_000 }
        from distance in new[] { 14, 15 }
        select new object[] { own, enemy, distance };

    [Theory]
    [MemberData(nameof(ForceMatrixCases))]
    public void 병력_거리_행렬에서_단독출전과_방어예비_침범이_없다(int own, int enemy, int distance)
    {
        var state = Scenario(own, enemy, siege: Math.Min(20_000, own / 3), distance: distance);
        var after = PlannerWithMap(new HexMap(-3, 22, -5, 5)).Plan(state, new FactionId(1));
        var combat = after.Armies.Where(u => !u.IsSupply).ToList();
        var available = own - Math.Max(10_000, (int)Math.Ceiling(own * .30));
        if (available < Math.Ceiling(enemy * 1.2)) Assert.Empty(combat);
        if (combat.Count == 0) return;
        Assert.True(combat.Count >= 2);
        Assert.Contains(after.Armies, u => u.IsSupply);
        Assert.True(after.Garrisons.Where(g => g.City == new CityId(1)).Sum(g => g.Troops)
            >= own - available);
    }

    [Theory]
    [MemberData(nameof(LongRunCases))]
    public void 공세_묶음은_실제_캠페인에서_이동과_교전을_진행한다(int seed, int horizon)
    {
        var map = new HexMap(-3, 18, -5, 5);
        var deployer = new DeployService(new CommandBalance(), Troops,
            new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
            new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()));
        var ai = new FactionAI(new CommandService(new CommandBalance(), Troops), deployer,
            map: map);
        var state = ai.PlanWeek(Scenario(100000, 20000), new FactionId(1));
        var deployed = state.Armies.Where(u => !u.IsSupply).Select(u => u.Id).ToHashSet();
        Assert.True(deployed.Count >= 2);
        var movement = new MovementSimulator(new PassabilityMap(map, [], state.Cities));
        var legacy = new AdvanceOrchestrator(movement,
            new CombatPhaseResolver(new BattleResolver(60), 70));
        var engine = new CampaignEngine(new RenewalCampaignAdvanceRunner(map, legacy),
            new WorldEngine(new BalanceConfig(MonthlyTaxPerCity: 0)),
            new CampaignSiege(new BattleResolver(60), Troops), new CityCapture(),
            new SeededRandomSource(seed));
        var initialWall = state.Cities.Single(c => c.Id == new CityId(2)).Wall;
        var moved = false;
        for (var week = 0; week < (horizon + 6) / 7; week++)
        {
            state = engine.AdvanceWeek(state, out _, out var sieges, out _);
            moved |= state.Armies.Any(u => deployed.Contains(u.Id)
                && u.Field.Position.Distance(new HexCoord(0, 0)) > 1);
            moved |= sieges.Count > 0;
        }
        Assert.True(moved);
        Assert.True(state.Cities.Single(c => c.Id == new CityId(2)).Wall < initialWall
            || state.Cities.Single(c => c.Id == new CityId(2)).Owner == new FactionId(1));
    }

    [Fact]
    public void 세력_AI_주간판단도_실제_공세_묶음을_출전시킨다()
    {
        var deployer = new DeployService(new CommandBalance(), Troops,
            new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
            new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()));
        var commands = new CommandService(new CommandBalance(), Troops);
        var ai = new FactionAI(commands, deployer, map: new HexMap(-3, 18, -5, 5));
        var state = ai.PlanWeek(Scenario(100000, 20000), new FactionId(1));
        Assert.True(state.Armies.Count(u => !u.IsSupply && !u.IsTransport) >= 2);
        Assert.Contains(state.Armies, u => u.IsSupply);
    }

    [Fact]
    public void 공세_성립시_전투부대와_따라가는_보급부대를_묶어서_출전한다()
    {
        var state = Scenario(100000, 20000);

        var after = Planner().Plan(state, new FactionId(1));

        Assert.True(after.Armies.Count(u => !u.IsSupply) >= 2);
        var supply = Assert.Single(after.Armies, u => u.IsSupply);
        Assert.Equal(UnitMode.March, supply.Field.Mode);
        Assert.Contains(after.Armies, u => !u.IsSupply && u.Id == supply.Field.FollowUnitId);
        Assert.Empty(state.Armies);
        Assert.True(after.Garrisons.Where(g => g.City == new CityId(1)).Sum(g => g.Troops) >= 30000);
    }

    [Fact]
    public void 대기중인_보급부대가_있으면_중복_편성하지_않고_새_공세를_따라간다()
    {
        var deployer = new DeployService(new CommandBalance(), Troops,
            new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
            new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()));
        var prepared = deployer.DeploySupply(Scenario(100000, 20000),
            new SupplyDeployRequest(new CityId(1), [new SupplyLine("swordsman", 1000)],
                new GeneralId(9)));
        Assert.True(prepared.Ok);
        var existing = Assert.Single(prepared.State.Armies);

        var after = new AiOffensePlanner(deployer, new AiConfig()).Plan(prepared.State,
            new FactionId(1));

        var supply = Assert.Single(after.Armies, u => u.IsSupply);
        Assert.Equal(existing.Id, supply.Id);
        Assert.Contains(after.Armies, u => !u.IsSupply && u.Id == supply.Field.FollowUnitId);
    }

    [Fact]
    public void 보급_장수나_군량이_모자라면_공세_전체를_취소한다()
    {
        var state = Scenario(100000, 20000, provisions: 0);

        var after = Planner().Plan(state, new FactionId(1));

        Assert.Empty(after.Armies);
        Assert.Equal(state.Garrisons.Sum(g => g.Troops), after.Garrisons.Sum(g => g.Troops));
        Assert.Equal(state.Cities[0].Provisions, after.Cities[0].Provisions);
    }

    [Fact]
    public void 후보_병력_부족이면_예전의_단독_도검병_출전을_하지_않는다()
    {
        var state = Scenario(30000, 30000);

        var after = Planner().Plan(state, new FactionId(1));

        Assert.Empty(after.Armies);
    }

    [Fact]
    public void 집단군_조건을_충족하면_최고_적성_장수가_선봉이다()
    {
        var state = Scenario(120000, 50000, siege: 30000, officers: 9);
        state = state with
        {
            Cities = state.Cities.Select(c => c.Id == new CityId(2) ? c with { Wall = 0 } : c).ToList(),
            GarrisonForces =
            [
                new(new CityId(1), "swordsman", 60000, 60),
                new(new CityId(1), "archer", 30000, 60),
                new(new CityId(1), "catapult", 30000, 60),
                new(new CityId(2), "swordsman", 50000, 60),
            ],
            Generals = state.Generals.Select(g => g.Id == new GeneralId(8)
                ? g with { Aptitudes = new Dictionary<TroopClass, AptitudeGrade>
                    {
                        [TroopClass.Infantry] = AptitudeGrade.S,
                        [TroopClass.Archer] = AptitudeGrade.S,
                        [TroopClass.Siege] = AptitudeGrade.S,
                        [TroopClass.Supply] = AptitudeGrade.C,
                    } }
                : g).ToList(),
        };

        var after = Planner().Plan(state, new FactionId(1));

        var group = Assert.Single(after.Armies, u => u.IsArmyGroup);
        Assert.Equal(new GeneralId(8), group.VanguardId);
        Assert.Single(after.Armies, u => u.IsSupply);
    }

    [Fact]
    public void 근거리_기병_공세는_공성기와_기병으로_편성한다()
    {
        var state = Scenario(80000, 10000, siege: 20000, distance: 5);
        state = state with
        {
            GarrisonForces =
            [
                new(new CityId(1), "cavalry", 60000, 60),
                new(new CityId(1), "catapult", 20000, 60),
                new(new CityId(2), "swordsman", 10000, 60),
            ],
        };

        var after = Planner().Plan(state, new FactionId(1));

        Assert.Contains(after.Armies, u => u.Class == TroopClass.Siege);
        Assert.Contains(after.Armies, u => u.Class == TroopClass.Cavalry);
        Assert.DoesNotContain(after.Armies, u => u.IsSupply);
    }

    [Fact]
    public void AiTest_절반_수비대_상대로_공세_묶음을_편성한다()
    {
        var state = Scenario(80000, 40000, siege: 10000, officers: 8, distance: 11);
        state = state with
        {
            Cities = state.Cities.Select(c => c.Id == new CityId(2)
                ? c with { Wall = 1200 } : c).ToList(),
            GarrisonForces =
            [
                new(new CityId(1), "swordsman", 50000, 60),
                new(new CityId(1), "archer", 20000, 60),
                new(new CityId(1), "catapult", 10000, 60),
                new(new CityId(2), "swordsman", 30000, 60),
                new(new CityId(2), "archer", 10000, 60),
            ],
        };

        var after = Planner().Plan(state, new FactionId(1));

        Assert.True(after.Armies.Count >= 3);
        Assert.Contains(after.Armies, u => u.Class == TroopClass.Siege);
        Assert.Contains(after.Armies, u => u.IsSupply);
    }

    [Fact]
    public void 두_성의_병력이_합쳐져야_공세가_가능하면_같은_목표로_일괄_출전한다()
    {
        var state = Scenario(30000, 20000, siege: 20000, officers: 8);
        var secondary = new City(new CityId(3), "secondary", new HexCoord(-2, 2),
            new FactionId(1), 20000, CastleSize.Small);
        state = state with
        {
            Cities = state.Cities.Append(secondary).ToList(),
            Postings = state.Assignments.Select(p => p.General.Value >= 4
                ? p with { Location = secondary.Id } : p).ToList(),
            GarrisonForces = state.Garrisons.Append(new GarrisonForce(secondary.Id,
                "swordsman", 50000, 60)).ToList(),
        };

        var after = Planner().Plan(state, new FactionId(1));

        Assert.Contains(after.Armies, u => !u.IsSupply && u.OriginCity == new CityId(1));
        Assert.Contains(after.Armies, u => !u.IsSupply && u.OriginCity == secondary.Id);
        Assert.Single(after.Armies, u => u.IsSupply);
        Assert.All(after.Armies.Where(u => !u.IsSupply), u =>
            Assert.Equal(new HexCoord(10, 0), u.Field.Target));
    }

    [Fact]
    public void 세_성의_합산_전력으로만_가능한_공세도_원자적으로_편성한다()
    {
        var state = Scenario(30_000, 30_000, siege: 20_000, officers: 12);
        var second = new City(new CityId(3), "second", new HexCoord(-2, 1),
            new FactionId(1), 20_000, CastleSize.Small);
        var third = new City(new CityId(4), "third", new HexCoord(-4, 1),
            new FactionId(1), 30_000, CastleSize.Small);
        state = state with
        {
            Cities = state.Cities.Select(c => c.Id == new CityId(2) ? c with { Wall = 0 } : c)
                .Append(second).Append(third).ToList(),
            Postings = state.Assignments.Select(p => p with { Location = p.General.Value switch
            {
                >= 9 => third.Id,
                >= 5 => second.Id,
                _ => new CityId(1),
            } }).ToList(),
            GarrisonForces = state.Garrisons
                .Append(new GarrisonForce(second.Id, "swordsman", 20_000, 60))
                .Append(new GarrisonForce(third.Id, "swordsman", 50_000, 60)).ToList(),
        };

        var after = Planner().Plan(state, new FactionId(1));

        Assert.Contains(after.Armies, u => !u.IsSupply && u.OriginCity == new CityId(1));
        Assert.Contains(after.Armies, u => !u.IsSupply && u.OriginCity == second.Id);
        Assert.Contains(after.Armies, u => !u.IsSupply && u.OriginCity == third.Id);
        Assert.Single(after.Armies, u => u.IsSupply);
    }

    [Fact]
    public void 합동_공세의_한_성에_군량이_없으면_양쪽_출전을_모두_취소한다()
    {
        var state = Scenario(30000, 20000, siege: 20000, officers: 8);
        var secondary = new City(new CityId(3), "secondary", new HexCoord(-2, 2),
            new FactionId(1), 0, CastleSize.Small);
        state = state with
        {
            Cities = state.Cities.Append(secondary).ToList(),
            Postings = state.Assignments.Select(p => p.General.Value >= 4
                ? p with { Location = secondary.Id } : p).ToList(),
            GarrisonForces = state.Garrisons.Append(new GarrisonForce(secondary.Id,
                "swordsman", 50000, 60)).ToList(),
        };

        var after = Planner().Plan(state, new FactionId(1));

        Assert.Empty(after.Armies);
        Assert.Equal(state.Garrisons.Sum(g => g.Troops), after.Garrisons.Sum(g => g.Troops));
    }

    [Fact]
    public void 공세가_확정되면_가능한_경우_선전포고_사절을_출전전에_보낸다()
    {
        var state = Scenario(100000, 20000);

        var after = PlannerWithDiplomacy().Plan(state, new FactionId(1));

        Assert.True(after.Armies.Count >= 3);
        var declaration = Assert.Single(after.Commands, c => c.Kind == CommandKind.DeclareWar);
        Assert.Equal(new CityId(2), declaration.TargetCity);
        Assert.True(declaration.CompletionDay > state.Day);
        Assert.DoesNotContain(after.Armies, u => u.VanguardId == declaration.Main);
    }

    [Fact]
    public void 물로_끊긴_적성에는_육상_공세를_보내지_않는다()
    {
        var state = Scenario(100000, 20000);
        var water = Enumerable.Range(-3, 7).ToDictionary(r => new HexCoord(5, r),
            _ => TerrainType.WaterShallow);
        var map = new HexMap(-2, 12, -3, 3, water);

        var after = PlannerWithMap(map).Plan(state, new FactionId(1));

        Assert.Empty(after.Armies);
    }
}
