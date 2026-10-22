namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public static class SupplyFollowService
{
    public static CommandResult Assign(GameState state, UnitId supplyId, UnitId targetId)
    {
        var supply = state.Armies.FirstOrDefault(unit => unit.Id == supplyId);
        var target = state.Armies.FirstOrDefault(unit => unit.Id == targetId);
        if (supply is null || !supply.IsSupply || supply.Pool.Active <= 0 || supply.IsWaitingDeployment)
            return CommandResult.Fail("따라갈 보급부대가 야전에 없습니다.", state);
        if (target is null || target.Pool.Active <= 0 || target.IsSupply || target.IsTransport
            || target.IsWaitingDeployment || target.Field.Owner != supply.Field.Owner)
            return CommandResult.Fail("같은 세력의 야전 전투부대만 따라갈 수 있습니다.", state);
        var armies = state.Armies.Select(unit => unit.Id == supplyId
            ? unit with { Field = unit.Field with
            {
                FollowUnitId = targetId, Mode = UnitMode.March, Target = null,
                ContinuousTarget = null, Waypoints = null, ContinuousWaypoints = null,
                AssignedUnitTarget = null, PursuitTarget = null,
            } }
            : unit).ToList();
        return CommandResult.Success(state with { FieldArmies = armies });
    }

    public static IReadOnlyList<CombatUnit> PlanDay(IReadOnlyList<CombatUnit> armies,
        Func<MovementDomain, HexCoord, bool> canEnter,
        IReadOnlyList<City> cities, IReadOnlyList<FieldBuilding> buildings,
        FactionId? playerFaction = null)
    {
        var byId = armies.Where(unit => unit.Pool.Active > 0).ToDictionary(unit => unit.Id);
        var blocked = cities.SelectMany(CastleFootprint.TilesFor).ToHashSet();
        blocked.UnionWith(buildings.Select(building => building.Position));
        return armies.Select(unit =>
        {
            if (!unit.IsSupply || unit.Field.FollowUnitId is not { } followId)
                return unit;
            if (!byId.TryGetValue(followId, out var target) || target.Field.Owner != unit.Field.Owner
                || target.IsSupply || target.IsTransport || target.IsWaitingDeployment)
            {
                if (playerFaction is { } player && unit.Field.Owner != player)
                {
                    var origin = cities.FirstOrDefault(city => city.Id == unit.OriginCity
                        && city.Owner == unit.Field.Owner);
                    if (origin is not null)
                        return unit with { Field = unit.Field with { FollowUnitId = null,
                            Mode = UnitMode.March, Target = origin.Position,
                            ReturnCity = origin.Id, ContinuousTarget = null } };
                }
                return unit with { Field = unit.Field with { FollowUnitId = null,
                    Mode = UnitMode.Standby, Target = null, ContinuousTarget = null } };
            }
            if (unit.Field.Position.Distance(target.Field.Position) <= 2)
                return unit with { Field = unit.Field with { Mode = UnitMode.March,
                    Target = null, ContinuousTarget = null } };
            var destination = target.Field.Position.Neighbors()
                .Where(tile => !blocked.Contains(tile) && canEnter(unit.Field.Domain, tile))
                .OrderBy(tile => tile.Distance(unit.Field.Position))
                .ThenBy(tile => tile.Q).ThenBy(tile => tile.R)
                .Cast<HexCoord?>().FirstOrDefault();
            return unit with { Field = unit.Field with { Mode = UnitMode.March,
                Target = destination, ContinuousTarget = null, Waypoints = null,
                ContinuousWaypoints = null } };
        }).ToList();
    }
}
