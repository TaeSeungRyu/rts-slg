namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Domain;

public enum RenewalWorkKind
{
    Domestic,
    Research,
    Shipbuilding,
    Reconnaissance,
}

public sealed record RenewalScheduledWork(long Id, RenewalWorkKind Kind,
    int CompletionDay, bool Completed = false);

public sealed record RenewalUnitLogistics(UnitId Unit, int Provisions,
    int DailyConsumption, bool IsSupply = false, int SupplyRadius = 0,
    int SupplyStock = 0);

public sealed record RenewalProductionOperation(long Id, UnitId Unit, int CompletionDay,
    int CommittedTroops = 500, int RewardGold = 0, int RewardProvisions = 0,
    bool Completed = false);

public sealed record RenewalIntegrationState(
    int CalendarDay,
    int LastSettledDay,
    IReadOnlyList<RenewalScheduledWork>? ScheduledWorks = null,
    IReadOnlyDictionary<UnitId, RenewalUnitLogistics>? UnitLogistics = null,
    IReadOnlyList<RenewalProductionOperation>? ProductionOperations = null,
    int WeeklySettlementCount = 0)
{
    public IReadOnlyList<RenewalScheduledWork> Works => ScheduledWorks ?? [];
    public IReadOnlyDictionary<UnitId, RenewalUnitLogistics> Logistics =>
        UnitLogistics ?? new Dictionary<UnitId, RenewalUnitLogistics>();
    public IReadOnlyList<RenewalProductionOperation> Productions => ProductionOperations ?? [];
}
