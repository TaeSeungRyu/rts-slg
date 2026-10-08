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
