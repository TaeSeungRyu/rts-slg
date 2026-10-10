namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public sealed record FieldUnitCommandRequest(
    UnitId Unit,
    UnitMode Mode,
    HexCoord Target,
    IReadOnlyList<HexCoord>? Waypoints = null,
    IReadOnlySet<HexCoord>? VisibleTiles = null,
    CityId? ReturnCity = null,
    RenewalMovement.ContinuousPosition? ContinuousTarget = null,
    IReadOnlyList<RenewalMovement.ContinuousPosition>? ContinuousWaypoints = null);

public sealed class FieldUnitCommandService(Func<MovementDomain, HexCoord, bool> canEnter,
    Func<GameState, HexCoord, bool>? isStaticAttackTarget = null)
{
    public CommandResult Reassign(GameState state, FactionId faction, FieldUnitCommandRequest req)
    {
        var unit = state.Armies.FirstOrDefault(u => u.Id == req.Unit && u.Field.Owner == faction);
        if (unit is null || unit.Pool.Active <= 0)
        {
            return CommandResult.Fail("명령할 수 있는 아군 부대가 없습니다.", state);
        }
        if (FieldConstructionService.IsConstructing(state, unit.Id))
        {
            return CommandResult.Fail("건축 중인 부대에는 다른 명령을 내릴 수 없습니다.", state);
        }

        if (!CanTarget(state, faction, unit, req.Target, req.VisibleTiles))
        {
            return CommandResult.Fail("시야 밖이거나 이동할 수 없는 목표입니다.", state);
        }

        if (req.Waypoints is { Count: > 0 } && req.Waypoints.Any(w => !canEnter(unit.Field.Domain, w)))
        {
            return CommandResult.Fail("경유지 중 이동할 수 없는 지점이 있습니다.", state);
        }

        // A destination is not an attack order. March only fights when an enemy
        // actually blocks its route; an explicit Attack order keeps its target.
        var mode = req.Mode;
        var armies = state.Armies
            .Select(a => a.Id == req.Unit
                ? a with { Field = a.Field with { Mode = mode, Target = req.Target,
                    Waypoints = req.Waypoints, ReturnCity = req.ReturnCity,
                    ContinuousTarget = req.ContinuousTarget,
                    ContinuousWaypoints = req.ContinuousWaypoints,
                    PursuitTarget = null,
                    AssignedUnitTarget = mode == UnitMode.Attack
                        ? state.Armies.FirstOrDefault(target => target.Field.Owner != faction
                            && target.Field.Position == req.Target)?.Id : null } }
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
        if (FieldConstructionService.IsConstructing(state, unit.Id))
        {
            return CommandResult.Fail("건축 중인 부대에는 다른 명령을 내릴 수 없습니다.", state);
        }

        var armies = state.Armies
            .Select(a => a.Id == unitId ? a with { Field = a.Field with { Target = null,
                Waypoints = null, ContinuousTarget = null, ContinuousWaypoints = null,
                AssignedUnitTarget = null, PursuitTarget = null } } : a)
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

        return Reassign(state, faction, new FieldUnitCommandRequest(unitId, UnitMode.March, city.Position, ReturnCity: city.Id));
    }

    private bool CanTarget(GameState state, FactionId faction, CombatUnit unit, HexCoord target,
        IReadOnlySet<HexCoord>? visible)
    {
        var city = state.Cities.FirstOrDefault(c => CastleFootprint.TilesFor(c).Contains(target));
        if (city is not null)
        {
            // 해상 부대는 항구만 출입·공격 목표로 삼을 수 있다. 일반 성을 허용하면
            // UI에서 항구가 아닌 육지 성을 클릭했을 때도 선박 경로가 성벽으로 끝난다.
            return unit.Class != TroopClass.Naval || city.IsPort;
        }

        // 유적 등 점유 불가 고정 전투 목표는 타일에 들어가는 대신 사거리 밖에서 공격한다.
        if (isStaticAttackTarget?.Invoke(state, target) ?? false)
        {
            return true;
        }

        return canEnter(unit.Field.Domain, target);
    }
}
