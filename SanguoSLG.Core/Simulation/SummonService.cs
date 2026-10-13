namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;
using SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class SummonService(HexMap map, IReadOnlyList<EventTreasureDefinition> treasures,
    IReadOnlyList<SummonedUnitDefinition> units)
{
    public CommandResult Order(GameState state, UnitId callerId, string treasureId, HexCoord position)
    {
        var caller = state.Armies.FirstOrDefault(x => x.Id == callerId && x.Pool.Active > 0);
        var item = state.Treasures.FirstOrDefault(x => x.Id == treasureId);
        var definition = treasures.FirstOrDefault(x => x.Code == item?.Code && x.Effect == "summon");
        if (caller is null || caller.IsWaitingDeployment || caller.Field.Domain is not MovementDomain.Land
            && caller.Field.Domain is not MovementDomain.LandMountain || caller.IsSummon
            || item is null || definition?.SummonCode is null || item.Faction != caller.Field.Owner
            || (item.EquippedBy != caller.VanguardId && item.EquippedBy != caller.AdjutantId))
            return CommandResult.Fail("출전한 육상 장수의 소환 아이템이 필요합니다.", state);
        if (item.ReusableDay is { } ready && state.Day < ready || state.SummonOrders.Any(x => x.TreasureId == treasureId)
            || state.Summons.Any(x => x.TreasureId == treasureId && x.EndDay > state.Day))
            return CommandResult.Fail("소환 아이템이 사용 중이거나 재사용 대기 중입니다.", state);
        if (state.Summons.Any(x => x.Faction == caller.Field.Owner && x.Code == definition.SummonCode
                && x.EndDay > state.Day && state.Armies.Any(u => u.Id == x.Unit && u.Pool.Active > 0)))
            return CommandResult.Fail("같은 종류의 소환수가 이미 활동 중입니다.", state);
        var current = caller.RenewalPosition is { } continuous
            ? RenewalHexSpace.NearestHex(continuous) : caller.Field.Position;
        if (current.Distance(position) != 1 || !Free(state, position))
            return CommandResult.Fail("인접한 빈 육상 위치를 선택해야 합니다.", state);
        return CommandResult.Success(state with
        { PendingSummons = state.SummonOrders.Append(new(treasureId, callerId, position, state.Day)).ToList() });
    }

    public GameState AdvanceDay(GameState state, int day, out IReadOnlyList<CombatUnit> appeared)
    {
        var remaining = state.Summons.Where(x => x.EndDay > day && state.Armies.Any(u => u.Id == x.Unit
            && u.Pool.Active > 0)).ToList();
        var expired = state.Summons.Select(x => x.Unit).Except(remaining.Select(x => x.Unit)).ToHashSet();
        var armies = state.Armies.Where(x => !expired.Contains(x.Id)).ToList();
        var items = state.Treasures.ToList();
        var pending = new List<SummonOrder>();
        var spawned = new List<CombatUnit>();
        foreach (var order in state.SummonOrders.OrderBy(x => x.IssuedDay).ThenBy(x => x.Caller.Value))
        {
            if (day <= order.IssuedDay) { pending.Add(order); continue; }
            var itemIndex = items.FindIndex(x => x.Id == order.TreasureId);
            if (itemIndex < 0) continue;
            var item = items[itemIndex];
            var definition = treasures.FirstOrDefault(x => x.Code == item.Code && x.Effect == "summon");
            var summon = units.FirstOrDefault(x => x.Code == definition?.SummonCode);
            var caller = armies.FirstOrDefault(x => x.Id == order.Caller && x.Pool.Active > 0);
            var callerPosition = caller?.RenewalPosition is { } continuous
                ? RenewalHexSpace.NearestHex(continuous) : caller?.Field.Position;
            var occupied = armies.Any(x => x.Field.Position == order.Position);
            if (caller is null || summon is null || caller.Field.Owner != item.Faction
                || item.EquippedBy != caller.VanguardId && item.EquippedBy != caller.AdjutantId
                || callerPosition?.Distance(order.Position) != 1 || occupied
                || !Free(state, order.Position) || item.ReusableDay is { } ready && day < ready
                || remaining.Any(x => x.Faction == item.Faction && x.Code == summon.Code)) continue;
            var id = new UnitId(armies.Select(x => x.Id.Value).Concat(state.Summons.Select(x => x.Unit.Value))
                .Append(0).Max() + 1);
            var field = new FieldUnit(id, item.Faction, order.Position, summon.Speed, summon.Detection,
                summon.Range, MovementDomain.Land, UnitMode.Standby, null, id.Value);
            var unit = new CombatUnit(field,
                new CombatStats(summon.Troops, summon.Attack, summon.Defense, AptitudeGrade.A.Percent()),
                new TroopPool(summon.Troops, 0), UnitCombatState.Create(0), MaxTroops: summon.Troops,
                Class: TroopClass.Cavalry, Provisions: -1, TroopCode: summon.Code,
                RenewalPosition: RenewalHexSpace.Center(order.Position), IsSummon: true);
            spawned.Add(unit);
            armies.Add(unit);
            remaining.Add(new(id, item.Id, item.Faction, summon.Code, day, day + summon.ActiveDays));
            items[itemIndex] = item with { SummonedDay = day, SummonEndDay = day + summon.ActiveDays,
                ReusableDay = day + summon.ActiveDays + summon.CooldownDays };
        }
        appeared = spawned;
        return state with { FieldArmies = armies, PendingSummons = pending,
            SummonedCreatures = remaining, FactionTreasures = items };
    }

    private bool Free(GameState state, HexCoord position)
        => map.Contains(position) && TerrainRules.CanEnter(MovementDomain.Land, map.TerrainAt(position))
            && !state.Cities.SelectMany(CastleFootprint.TilesFor).Contains(position)
            && !state.Placements.Any(x => x.Plot == position)
            && !state.Buildings.Any(x => x.Position == position)
            && !state.Ruins.Any(x => x.Position == position)
            && !state.Armies.Any(x => x.Field.Position == position && x.Pool.Active > 0);
}
