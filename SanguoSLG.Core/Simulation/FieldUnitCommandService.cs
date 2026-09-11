namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public sealed record FieldUnitCommandRequest(
    UnitId Unit,
    UnitMode Mode,
    HexCoord Target,
    IReadOnlyList<HexCoord>? Waypoints = null,
    IReadOnlySet<HexCoord>? VisibleTiles = null);

public sealed class FieldUnitCommandService(Func<MovementDomain, HexCoord, bool> canEnter)
{
    public CommandResult Reassign(GameState state, FactionId faction, FieldUnitCommandRequest req)
    {
        var unit = state.Armies.FirstOrDefault(u => u.Id == req.Unit && u.Field.Owner == faction);
        if (unit is null || unit.Pool.Active <= 0)
        {
            return CommandResult.Fail("명령할 수 있는 아군 부대가 없습니다.", state);
        }

        if (!CanTarget(state, faction, unit, req.Target, req.VisibleTiles))
        {
            return CommandResult.Fail("시야 밖이거나 이동할 수 없는 목표입니다.", state);
        }

        if (req.Waypoints is { Count: > 0 } && req.Waypoints.Any(w => !canEnter(unit.Field.Domain, w)))
        {
            return CommandResult.Fail("경유지 중 이동할 수 없는 지점이 있습니다.", state);
        }

        var mode = state.Cities.Any(c => c.Position == req.Target && c.Owner != faction) ? UnitMode.Attack : req.Mode;
        var armies = state.Armies
            .Select(a => a.Id == req.Unit
                ? a with { Field = a.Field with { Mode = mode, Target = req.Target, Waypoints = req.Waypoints } }
                : a)
            .ToList();
        return CommandResult.Success(state with { FieldArmies = armies });
    }

    public CommandResult Stop(GameState state, FactionId faction, UnitId unitId)
    {
        var unit = state.Armies.FirstOrDefault(u => u.Id == unitId && u.Field.Owner == faction);
        if (unit is null || unit.Pool.Active <= 0)
        {
            return CommandResult.Fail("정지할 수 있는 아군 부대가 없습니다.", state);
        }

        var armies = state.Armies
            .Select(a => a.Id == unitId ? a with { Field = a.Field with { Target = null, Waypoints = null } } : a)
            .ToList();
        return CommandResult.Success(state with { FieldArmies = armies });
    }

    public CommandResult ReturnToCity(GameState state, FactionId faction, UnitId unitId, CityId cityId)
    {
        var city = state.Cities.FirstOrDefault(c => c.Id == cityId && c.Owner == faction);
        if (city is null)
        {
            return CommandResult.Fail("복귀할 아군 성을 찾을 수 없습니다.", state);
        }

        return Reassign(state, faction, new FieldUnitCommandRequest(unitId, UnitMode.March, city.Position));
    }

    private bool CanTarget(GameState state, FactionId faction, CombatUnit unit, HexCoord target,
        IReadOnlySet<HexCoord>? visible)
    {
        var city = state.Cities.FirstOrDefault(c => c.Position == target);
        if (city is not null)
        {
            if (city.Owner == faction) { return true; }
            return visible is null || visible.Contains(target) || state.IsScouted(faction, city.Id);
        }

        return canEnter(unit.Field.Domain, target);
    }
}
