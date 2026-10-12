namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

public sealed class SupplyReplenishmentService
{
    public (int Troops, int Gold, int Provisions) Limits(GameState state, UnitId sourceId, UnitId targetId)
    {
        var source = state.Armies.FirstOrDefault(unit => unit.Id == sourceId);
        var target = state.Armies.FirstOrDefault(unit => unit.Id == targetId);
        if (source is null || target is null || !source.IsSupply || sourceId == targetId
            || source.Pool.Active <= 0 || target.Pool.Active <= 0
            || source.Field.Owner != target.Field.Owner || source.IsWaitingDeployment || target.IsWaitingDeployment
            || source.Field.Position.Distance(target.Field.Position) > 1)
            return (0, 0, 0);

        var cargo = source.Cargo.Where(component => component.TroopCode == target.TroopCode)
            .Sum(component => component.Troops);
        // MaxTroops records the troop count at departure (and the old healing ceiling),
        // not the number this unit is allowed to carry after field replenishment.
        var balance = new CommandBalance();
        var carryingLimit = target.IsArmyGroup
            ? CommandEfficiency.ArmyGroupDeployLimit(state.ResearchOf(target.Field.Owner, FactionResearch.ArmyGroupCode), balance)
            : target.IsTransport ? DeployService.TransportMaxTroops
            : target.IsSupply ? balance.SupplyMaxTroops
            : CommandEfficiency.CommandTroopDeployLimit(state.ResearchOf(target.Field.Owner, FactionResearch.CommandTroopsCode), balance);
        carryingLimit = Math.Max(carryingLimit, target.MaxTroops);
        return (Math.Max(0, Math.Min(Math.Min(cargo, source.Pool.Active), carryingLimit - target.Pool.Active)),
            Math.Max(0, source.CarryingGold),
            source.TracksProvisions && target.TracksProvisions
                ? Math.Max(0, Math.Min(source.Provisions, target.MaxProvisions() - target.Provisions)) : 0);
    }

    public CommandResult Queue(GameState state, FactionId faction, SupplyReplenishmentOrder order)
    {
        var source = state.Armies.FirstOrDefault(unit => unit.Id == order.Source);
        if (source is null || source.Field.Owner != faction)
            return CommandResult.Fail("아군 보급부대만 보충 명령을 내릴 수 있습니다.", state);
        if (state.ReplenishmentOrders.Any(existing => existing.Source == order.Source))
            return CommandResult.Fail("이 보급부대는 이미 다른 부대의 보충을 예약했습니다.", state);
        var limits = Limits(state, order.Source, order.Target);
        if (order.Troops < 0 || order.Gold < 0 || order.Provisions < 0
            || order.Troops > limits.Troops || order.Gold > limits.Gold
            || order.Provisions > limits.Provisions
            || order.Troops + order.Gold + order.Provisions == 0)
            return CommandResult.Fail("인접 부대 또는 보충할 병력·금·군량 수량을 확인하세요.", state);
        return CommandResult.Success(state with
        {
            SupplyReplenishmentOrders = state.ReplenishmentOrders.Append(order).ToList(),
        });
    }

    public (GameState State, IReadOnlyList<string> Reports) Execute(GameState state)
    {
        if (state.ReplenishmentOrders.Count == 0) return (state, []);
        var armies = state.Armies.ToDictionary(unit => unit.Id);
        var reports = new List<string>();
        foreach (var order in state.ReplenishmentOrders.OrderBy(order => order.Source.Value))
        {
            var current = state with { FieldArmies = armies.Values.ToList() };
            var limits = Limits(current, order.Source, order.Target);
            if (order.Troops > limits.Troops || order.Gold > limits.Gold
                || order.Provisions > limits.Provisions || !armies.ContainsKey(order.Source)
                || !armies.ContainsKey(order.Target))
            {
                reports.Add($"[보충] 보급부대 {order.Source.Value}의 예약은 대상 또는 물자가 부족하여 실패했습니다.");
                continue;
            }

            var source = armies[order.Source];
            var target = armies[order.Target];
            var cargo = source.Cargo.ToList();
            var remainingTroops = order.Troops;
            var weightedTraining = 0L;
            for (var i = 0; i < cargo.Count && remainingTroops > 0; i++)
            {
                if (cargo[i].TroopCode != target.TroopCode) continue;
                var given = Math.Min(cargo[i].Troops, remainingTroops);
                weightedTraining += (long)given * cargo[i].TrainingLevel;
                cargo[i] = cargo[i] with { Troops = cargo[i].Troops - given };
                remainingTroops -= given;
            }
            var targetTroops = target.Pool.Active + order.Troops;
            var training = order.Troops == 0 ? target.Training
                : (int)(((long)target.Training * target.Pool.Active + weightedTraining + targetTroops / 2) / targetTroops);
            var fromCargoGold = Math.Min(source.CargoGold, order.Gold);
            armies[order.Source] = source with
            {
                Pool = source.Pool with { Active = source.Pool.Active - order.Troops },
                SupplyCargo = cargo.Where(component => component.Troops > 0).ToList(),
                CargoGold = source.CargoGold - fromCargoGold,
                LootGold = source.LootGold - (order.Gold - fromCargoGold),
                Provisions = source.Provisions - order.Provisions,
            };
            armies[order.Target] = target with
            {
                Pool = target.Pool with { Active = targetTroops },
                Training = training,
                CargoGold = target.CargoGold + order.Gold,
                Provisions = target.Provisions + order.Provisions,
            };
            reports.Add($"[보충] 보급부대 {order.Source.Value} → 부대 {order.Target.Value}: 병력 {order.Troops:N0}, 금 {order.Gold:N0}, 군량 {order.Provisions:N0}");
        }
        return (state with { FieldArmies = armies.Values.OrderBy(unit => unit.Id.Value).ToList(),
            SupplyReplenishmentOrders = [] }, reports);
    }
}
