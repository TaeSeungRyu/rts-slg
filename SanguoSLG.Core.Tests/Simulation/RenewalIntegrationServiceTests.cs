namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public sealed class RenewalIntegrationServiceTests
{
    private sealed class FixedRandom(int value) : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) => value;
    }
    private static RenewalAdvanceState Settlement(int day, RenewalIntegrationState integration) =>
        new(day, RenewalAdvancePhase.DaySettlement, 50, [], Integration: integration);

    [Fact]
    public void 하루정산은_같은날_재호출해도_한번만_적용된다()
    {
        var service = new RenewalIntegrationService();
        var initial = Settlement(1, new RenewalIntegrationState(7, 0));

        var first = service.ResolveDay(initial);
        var second = service.ResolveDay(first.State);

        Assert.Single(first.Events, x => x.Kind == RenewalAdvanceEventKind.DailySettlementApplied);
        Assert.Single(first.Events, x => x.Kind == RenewalAdvanceEventKind.WeeklySettlementApplied);
        Assert.Empty(second.Events);
        Assert.Equal(1, second.State.Integration!.WeeklySettlementCount);
    }

    [Fact]
    public void 내정_연구_함선_정찰은_완료일_경계에서만_완료된다()
    {
        var works = Enum.GetValues<RenewalWorkKind>()
            .Select((kind, index) => new RenewalScheduledWork(index + 1, kind, 9)).ToList();
        var service = new RenewalIntegrationService();
        var before = service.ResolveDay(Settlement(1,
            new RenewalIntegrationState(8, 0, works)));
        var due = service.ResolveDay(Settlement(2, before.State.Integration!));

        Assert.All(before.State.Integration!.Works, x => Assert.False(x.Completed));
        Assert.All(due.State.Integration!.Works, x => Assert.True(x.Completed));
        Assert.Equal(4, due.Events.Count(x =>
            x.Kind == RenewalAdvanceEventKind.ScheduledWorkCompleted));
    }

    [Fact]
    public void 일시정지처럼_단계가_정산이_아니면_날짜와작업을_바꾸지않는다()
    {
        var integration = new RenewalIntegrationState(7, 0,
            [new RenewalScheduledWork(1, RenewalWorkKind.Research, 7)]);
        var state = new RenewalAdvanceState(1, RenewalAdvancePhase.Movement, 0, [],
            Integration: integration);

        var result = new RenewalIntegrationService().ResolveDay(state);

        Assert.Same(state, result.State);
        Assert.Empty(result.Events);
    }

    [Fact]
    public void 보급부대는_같은세력_범위안의_부족한군량만_보충한다()
    {
        var target = RenewalUnitState.Create(new UnitId(1), new ContinuousPosition(0, 0),
            new ContinuousPosition(0, 0), 0) with { Owner = new FactionId(1) };
        var supply = RenewalUnitState.Create(new UnitId(2), new ContinuousPosition(2_000, 0),
            new ContinuousPosition(2_000, 0), 0) with { Owner = new FactionId(1) };
        var logistics = new Dictionary<UnitId, RenewalUnitLogistics>
        {
            [target.Id] = new(target.Id, 5, 10),
            [supply.Id] = new(supply.Id, 0, 0, true, 4, 20),
        };
        var state = new RenewalAdvanceState(1, RenewalAdvancePhase.DaySettlement, 50,
            [target, supply], Integration: new RenewalIntegrationState(1, 0,
                UnitLogistics: logistics));

        var result = new RenewalIntegrationService().ResolveDay(state);

        Assert.Equal(5, result.State.Integration!.Logistics[target.Id].Provisions);
        Assert.Equal(15, result.State.Integration.Logistics[supply.Id].SupplyStock);
        Assert.Contains(result.Events, x => x.Kind == RenewalAdvanceEventKind.SupplyTransferred
            && x.Amount == 5);
    }

    [Fact]
    public void 보루범위안에서는_일일군량소비가_40퍼센트_감소한다()
    {
        var unit = RenewalUnitState.Create(new UnitId(1), new ContinuousPosition(0, 0),
            new ContinuousPosition(0, 0), 0) with { Owner = new FactionId(1) };
        var fort = new RenewalStructureCombatState(
            new RenewalTargetId(RenewalTargetKind.Building, 10), new FactionId(1),
            new ContinuousPosition(1_000, 0), 2_500, 12, Kind: FieldBuildingKind.Fort,
            EffectRadius: 2);
        var logistics = new Dictionary<UnitId, RenewalUnitLogistics>
        {
            [unit.Id] = new(unit.Id, 100, 10),
        };
        var state = new RenewalAdvanceState(1, RenewalAdvancePhase.DaySettlement, 50, [unit],
            Structures: [fort], Integration: new RenewalIntegrationState(1, 0,
                UnitLogistics: logistics));

        var result = new RenewalIntegrationService().ResolveDay(state);

        Assert.Equal(94, result.State.Integration!.Logistics[unit.Id].Provisions);
        Assert.Contains(result.Events, x => x.Kind == RenewalAdvanceEventKind.ProvisionsConsumed
            && x.Amount == 6);
    }

    [Fact]
    public void 이동후처리에서_진법은_범위안_적병력의_1퍼센트를_부상으로바꾼다()
    {
        var unit = RenewalUnitState.Create(new UnitId(1), new ContinuousPosition(1_000, 0),
            new ContinuousPosition(1_000, 0), 0) with { Owner = new FactionId(2) };
        var formation = new RenewalStructureCombatState(
            new RenewalTargetId(RenewalTargetKind.Building, 10), new FactionId(1),
            new ContinuousPosition(0, 0), 2_000, 12, Kind: FieldBuildingKind.Formation,
            EffectRadius: 1);
        var profile = new RenewalCombatProfile(unit.Id,
            new BattleParticipant(new CombatStats(10_000, 10, 10), UnitMode.Attack,
                new TroopPool(10_000, 0)));
        var state = new RenewalAdvanceState(1, RenewalAdvancePhase.MovementAftermath, 50,
            [unit], CombatProfiles: new Dictionary<UnitId, RenewalCombatProfile>
            { [unit.Id] = profile }, Structures: [formation]);

        var result = new RenewalIntegrationService(new FixedRandom(0))
            .ResolveMovementAftermath(state);

        Assert.Equal(new TroopPool(9_900, 100),
            result.State.CombatProfiles![unit.Id].Participant.Pool);
        Assert.Single(result.Events, x => x.Kind == RenewalAdvanceEventKind.FormationTriggered);
    }

    [Fact]
    public void 정찰대는_적이같은타일에_도착하면_즉시제거된다()
    {
        var unit = RenewalUnitState.Create(new UnitId(1), new ContinuousPosition(0, 0),
            new ContinuousPosition(0, 0), 0) with { Owner = new FactionId(2) };
        var scout = new RenewalStructureCombatState(
            new RenewalTargetId(RenewalTargetKind.Building, 10), new FactionId(1),
            new ContinuousPosition(0, 0), 1, 0, Kind: FieldBuildingKind.ScoutPost);
        var state = new RenewalAdvanceState(1, RenewalAdvancePhase.MovementAftermath, 50,
            [unit], Structures: [scout]);

        var result = new RenewalIntegrationService().ResolveMovementAftermath(state);

        Assert.Empty(result.State.Structures!);
        Assert.Single(result.Events, x => x.Kind == RenewalAdvanceEventKind.ScoutPostRemoved);
    }

    [Fact]
    public void 보루와진법이_파괴되면_주둔부대가_그자리에서_복원된다()
    {
        var structureId = new RenewalTargetId(RenewalTargetKind.Building, 10);
        var unit = RenewalUnitState.Create(new UnitId(1), new ContinuousPosition(0, 0),
            new ContinuousPosition(0, 0), 0) with
        {
            Owner = new FactionId(1), IsActive = false, GarrisonStructure = structureId,
        };
        var destroyed = new RenewalStructureCombatState(structureId, new FactionId(1),
            new ContinuousPosition(2_000, 0), 0, 12, Kind: FieldBuildingKind.Fort,
            GarrisonUnit: unit.Id);
        var state = new RenewalAdvanceState(1, RenewalAdvancePhase.AttackAftermath, 50,
            [unit], Structures: [destroyed]);

        var result = new RenewalIntegrationService().ResolveAttackAftermath(state);
        var released = Assert.Single(result.State.Units);

        Assert.True(released.IsActive);
        Assert.Null(released.GarrisonStructure);
        Assert.Equal(destroyed.Position, released.Position);
        Assert.Single(result.Events, x => x.Kind == RenewalAdvanceEventKind.GarrisonReleased);
    }
}
