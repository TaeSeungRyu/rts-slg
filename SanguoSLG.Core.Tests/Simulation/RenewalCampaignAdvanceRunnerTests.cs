namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;

public sealed class RenewalCampaignAdvanceRunnerTests
{
    [Fact]
    public void 캠페인_진행기는_연속이동_결과를_기존_전투규칙에_전달한다()
    {
        var map = new HexMap(-3, 3, -3, 3);
        var legacy = new CaptureRunner();
        var runner = new RenewalCampaignAdvanceRunner(map, legacy);
        var unit = Unit(1, new HexCoord(0, 0), new HexCoord(2, 0), speed: 1);

        var result = runner.Run([unit], maxDays: 1);

        Assert.Equal(0, legacy.LastMaxDays);
        Assert.Equal(new HexCoord(1, 0), Assert.Single(result.Units).Field.Position);
        Assert.NotNull(Assert.Single(result.Units).RenewalPosition);
        Assert.Equal(50, result.Movement.Ticks.Count);
    }

    [Fact]
    public void 구모드_통행판정은_되돌리기_경로로_그대로_유지한다()
    {
        var legacy = new CaptureRunner(canEnter: false);
        var runner = new RenewalCampaignAdvanceRunner(new HexMap(-1, 1, -1, 1), legacy);

        Assert.False(runner.CanEnter(MovementDomain.Land, new HexCoord(0, 0)));
    }

    [Fact]
    public void 전투후_강제변위는_다음날_연속좌표의_새원점이된다()
    {
        var legacy = new CaptureRunner(displace: true);
        var runner = new RenewalCampaignAdvanceRunner(new HexMap(-3, 3, -3, 3), legacy);

        var result = runner.Run([Unit(1, new HexCoord(0, 0), new HexCoord(0, 0), 0)], 1);

        var unit = Assert.Single(result.Units);
        Assert.Equal(new HexCoord(1, 0), unit.Field.Position);
        Assert.Equal(RenewalHexSpace.Center(new HexCoord(1, 0)), unit.RenewalPosition);
    }

    [Fact]
    public void 전진_정지_추적은_원래목표와_적거점_추격을_구분한다()
    {
        var trace = new List<RenewalCampaignTraceEntry>();
        var runner = new RenewalCampaignAdvanceRunner(new HexMap(-2, 10, -2, 10),
            new CaptureRunner(), trace.Add);
        var origin = new HexCoord(1, 2);
        var goal = new HexCoord(1, 8);
        var hostile = new HexCoord(4, 6);
        var unit = Unit(1, origin, goal, speed: 2) with
        {
            Field = Unit(1, origin, goal, speed: 2).Field with { Mode = UnitMode.Advance },
        };

        runner.Run([unit], maxDays: 7,
            castles: [new SiegeSite(hostile, new FactionId(2))]);

        Assert.Contains(trace, entry => entry.PursuitTarget is not null
            && entry.OriginalDestination == RenewalHexSpace.Center(goal)
            && entry.EffectiveDestination == RenewalHexSpace.Center(hostile));
        Assert.Contains(trace, entry => entry.StopReason == RenewalStopReason.TargetInRange
            && entry.PursuitTarget is not null);
    }

    [Fact]
    public void 적_건축물_공격은_점유타일에_진입하지_않고_사거리에서_멈춘다()
    {
        var buildingHex = new HexCoord(2, 0);
        var building = new FieldBuilding(new FieldBuildingId(1), "palisade",
            new FactionId(2), buildingHex, 1500, 0, 0);
        var trace = new List<RenewalCampaignTraceEntry>();
        var runner = new RenewalCampaignAdvanceRunner(new HexMap(-3, 4, -3, 3),
            new CaptureRunner(), trace.Add);
        var unit = Unit(1, new HexCoord(0, 0), buildingHex, speed: 2) with
        {
            Field = Unit(1, new HexCoord(0, 0), buildingHex, speed: 2).Field
                with { Mode = UnitMode.Attack },
        };

        var result = runner.Run([unit], maxDays: 2, fieldBuildings: [building]);

        Assert.Contains(trace, entry => entry.AssignedTarget
            == new RenewalTargetId(RenewalTargetKind.Building, building.Id.Value)
            && entry.StopReason == RenewalStopReason.TargetInRange);
        Assert.DoesNotContain(trace, entry =>
            RenewalHexSpace.NearestHex(entry.Position) == buildingHex);
        Assert.NotEqual(buildingHex, Assert.Single(result.Units).Field.Position);
    }

    [Fact]
    public void 적거점이_아군으로_바뀌면_다음진행에_원래목표로_복귀한다()
    {
        var map = new HexMap(-2, 10, -2, 10);
        var runner = new RenewalCampaignAdvanceRunner(map, new CaptureRunner());
        var goal = new HexCoord(1, 8);
        var hostile = new HexCoord(4, 6);
        var unit = Unit(1, new HexCoord(1, 2), goal, speed: 2) with
        {
            Field = Unit(1, new HexCoord(1, 2), goal, speed: 2).Field
                with { Mode = UnitMode.Advance },
        };
        var first = runner.Run([unit], maxDays: 7,
            castles: [new SiegeSite(hostile, new FactionId(2))]);
        var paused = Assert.Single(first.Units);

        var resumed = runner.Run([paused], maxDays: 7,
            castles: [new SiegeSite(hostile, new FactionId(1))]);

        Assert.NotEqual(paused.RenewalPosition, Assert.Single(resumed.Units).RenewalPosition);
    }

    [Fact]
    public void 공격_명령은_지정한_대상이_우호화되면_다른_적을_추격하지_않는다()
    {
        var trace = new List<RenewalCampaignTraceEntry>();
        var runner = new RenewalCampaignAdvanceRunner(new HexMap(-2, 6, -2, 6),
            new CaptureRunner(), trace.Add);
        var target = new HexCoord(2, 0);
        var unit = Unit(1, new HexCoord(0, 0), target, speed: 2) with
        {
            Field = Unit(1, new HexCoord(0, 0), target, speed: 2).Field
                with { Mode = UnitMode.Attack },
        };

        runner.Run([unit], maxDays: 1,
            castles: [new SiegeSite(target, new FactionId(1)),
                new SiegeSite(new HexCoord(1, 1), new FactionId(2))]);

        Assert.All(trace, entry => Assert.Null(entry.PursuitTarget));
        Assert.Contains(trace, entry => entry.StopReason == RenewalStopReason.TargetLost);
    }

    [Fact]
    public void 다중타일_성은_중심이_아닌_가장_가까운_외곽에서_접적한다()
    {
        var trace = new List<RenewalCampaignTraceEntry>();
        var runner = new RenewalCampaignAdvanceRunner(new HexMap(-2, 8, -2, 5),
            new CaptureRunner(), trace.Add);
        var center = new HexCoord(4, 0);
        var edge = new HexCoord(2, 0);
        var unit = Unit(1, new HexCoord(0, 0), edge, speed: 2) with
        {
            Field = Unit(1, new HexCoord(0, 0), edge, speed: 2).Field
                with { Mode = UnitMode.Attack },
        };

        runner.Run([unit], maxDays: 2, castles:
            [new SiegeSite(center, new FactionId(2), [edge, new HexCoord(3, 0), center])]);

        Assert.Contains(trace, entry => entry.StopReason == RenewalStopReason.TargetInRange
            && entry.EffectiveDestination == RenewalHexSpace.Center(edge));
    }

    [Fact]
    public void 같은_타일의_서로_다른_연속목표를_타일중심으로_덮어쓰지_않는다()
    {
        var trace = new List<RenewalCampaignTraceEntry>();
        var runner = new RenewalCampaignAdvanceRunner(new HexMap(-2, 4, -2, 4),
            new CaptureRunner(), trace.Add);
        var targetHex = new HexCoord(1, 1);
        var exact = RenewalHexSpace.Center(targetHex) with { X = RenewalHexSpace.Center(targetHex).X + 120 };
        var unit = Unit(1, new HexCoord(0, 0), targetHex, speed: 1) with
        {
            Field = Unit(1, new HexCoord(0, 0), targetHex, speed: 1).Field
                with { ContinuousTarget = exact },
        };

        runner.Run([unit], maxDays: 1);

        Assert.All(trace, entry => Assert.Equal(exact, entry.OriginalDestination));
        Assert.NotEqual(RenewalHexSpace.Center(targetHex), exact);
    }

    private static CombatUnit Unit(int id, HexCoord position, HexCoord target, int speed)
    {
        var field = new FieldUnit(new UnitId(id), new FactionId(1), position, speed, 3, 1,
            MovementDomain.Land, UnitMode.March, target, id);
        return new CombatUnit(field, new CombatStats(1000, 10, 10), new TroopPool(1000, 0),
            UnitCombatState.Create(60));
    }

    private sealed class CaptureRunner(bool canEnter = true, bool displace = false) : IFieldAdvanceRunner
    {
        public int LastMaxDays { get; private set; } = -1;

        public bool CanEnter(MovementDomain domain, HexCoord coord) => canEnter;

        public AdvanceTurn Run(IReadOnlyList<CombatUnit> units, int maxDays = 7,
            IReadOnlyList<SiegeSite>? castles = null, IReadOnlySet<UnitId>? deployedToday = null,
            IReadOnlySet<UnitId>? constructionUnits = null,
            IReadOnlyList<FieldBuilding>? fieldBuildings = null,
            IReadOnlyList<FieldBuildingDefinition>? fieldDefinitions = null, int fieldDay = 0)
        {
            LastMaxDays = maxDays;
            var output = displace
                ? units.Select(unit => unit with
                    { Field = unit.Field with { Position = new HexCoord(1, 0) } }).ToList()
                : units;
            var movement = new AdvanceResult([], output.Select(x => x.Field).ToList(),
                StopReason.MaxDays, maxDays);
            return new AdvanceTurn(output, movement, null,
                new Dictionary<UnitId, ActiveSkill>(), new Dictionary<UnitId, Stratagem>(),
                new Dictionary<UnitId, int>(), new Dictionary<UnitId, int>());
        }
    }
}
