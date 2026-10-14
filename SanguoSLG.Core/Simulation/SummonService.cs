namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;
using SanguoSLG.Core.Simulation.RenewalMovement;

public sealed class SummonService(HexMap map, IReadOnlyList<EventTreasureDefinition> treasures,
    IReadOnlyList<SummonedUnitDefinition> units)
{
    /// <summary>AI도 장착·범위·재사용 제한을 같은 명령 검사에 통과시켜 자동 소환한다.</summary>
    public GameState PlanAiOrders(GameState state, int day, FactionId playerFaction)
    {
        foreach (var caller in state.Armies.Where(x => x.Field.Owner != playerFaction
                     && x.Field.Owner.Value > 0 && x.Pool.Active > 0 && !x.IsWaitingDeployment
                     && x.Field.Domain is MovementDomain.Land or MovementDomain.LandMountain))
        {
            var item = state.Treasures.FirstOrDefault(x => x.Faction == caller.Field.Owner
                && x.EquippedBy is not null
                && (x.EquippedBy == caller.VanguardId || x.EquippedBy == caller.AdjutantId)
                && treasures.Any(t => t.Code == x.Code && t.Effect == "summon"));
            if (item is null) continue;
            var origin = caller.RenewalPosition is { } continuous
                ? RenewalHexSpace.NearestHex(continuous) : caller.Field.Position;
            var target = state.Armies.Where(x => x.Field.Owner != caller.Field.Owner && x.Pool.Active > 0)
                .OrderBy(x => origin.Distance(x.Field.Position)).FirstOrDefault();
            if (target is null || origin.Distance(target.Field.Position) > caller.Field.Detection) continue;
            foreach (var position in origin.Neighbors().OrderBy(x => x.Distance(target.Field.Position)))
            {
                var result = Order(state with { Day = day }, caller.Id, item.Id, position);
                if (!result.Ok) continue;
                state = result.State with { Day = state.Day };
                break;
            }
        }
        return state;
    }

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
        var summon = units.FirstOrDefault(x => x.Code == definition.SummonCode);
        if (summon is null) return CommandResult.Fail("소환수 데이터를 찾을 수 없습니다.", state);
        var id = new UnitId(state.Armies.Select(x => x.Id.Value)
            .Concat(state.Summons.Select(x => x.Unit.Value)).Append(0).Max() + 1);
        var field = new FieldUnit(id, item.Faction, position, summon.Speed, summon.Detection,
            summon.Range, MovementDomain.Land, UnitMode.Standby, null, id.Value);
        var unit = new CombatUnit(field,
            new CombatStats(summon.Troops, summon.Attack, summon.Defense, AptitudeGrade.A.Percent()),
            new TroopPool(summon.Troops, 0), UnitCombatState.Create(0), MaxTroops: summon.Troops,
            Class: TroopClass.Cavalry, Provisions: -1, TroopCode: summon.Code,
            RenewalPosition: RenewalHexSpace.Center(position), IsSummon: true);
        return CommandResult.Success(state with
        {
            FieldArmies = state.Armies.Append(unit).ToList(),
            SummonedCreatures = state.Summons.Append(new(id, item.Id, item.Faction, summon.Code,
                state.Day, state.Day + summon.ActiveDays)).ToList(),
            FactionTreasures = state.Treasures.Select(x => x.Id == item.Id
                ? x with { SummonedDay = state.Day, SummonEndDay = state.Day + summon.ActiveDays,
                    ReusableDay = state.Day + summon.ActiveDays + summon.CooldownDays } : x).ToList(),
        });
    }

    public GameState AdvanceDay(GameState state, int day, out IReadOnlyList<CombatUnit> appeared)
    {
        var remaining = state.Summons.Where(x => x.EndDay > day && state.Armies.Any(u => u.Id == x.Unit
            && u.Pool.Active > 0)).ToList();
        var expired = state.Summons.Select(x => x.Unit).Except(remaining.Select(x => x.Unit)).ToHashSet();
        var armies = state.Armies.Where(x => !expired.Contains(x.Id))
            .Select(x => x.SummonLockDay is { } lockDay && lockDay < day
                ? x with { SummonLockDay = null } : x).ToList();
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
                RenewalPosition: RenewalHexSpace.Center(order.Position), IsSummon: true,
                SummonLockDay: day);
            spawned.Add(unit);
            armies.Add(unit);
            var callerIndex = armies.FindIndex(x => x.Id == caller.Id);
            armies[callerIndex] = armies[callerIndex] with { SummonLockDay = day };
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
