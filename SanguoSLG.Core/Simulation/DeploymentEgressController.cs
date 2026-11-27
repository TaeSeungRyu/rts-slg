namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public sealed record DeploymentEgressResult(
    IReadOnlyList<CombatUnit> Released,
    IReadOnlyList<CombatUnit> Waiting);

/// <summary>
/// 날짜 지연을 마친 부대를 거점·방향별 FIFO로 외곽 첫 칸에 배치한다.
/// 이 클래스는 두 번째 칸 이후의 야전 이동에는 관여하지 않는다.
/// </summary>
public static class DeploymentEgressController
{
    public static DeploymentEgressResult Release(
        IReadOnlyList<CombatUnit> waiting,
        IReadOnlyList<CombatUnit> fieldArmies,
        IReadOnlyList<City> cities,
        Func<MovementDomain, HexCoord, bool>? canEnter = null,
        bool allowFriendlyOverlap = false)
    {
        var citiesById = cities.ToDictionary(city => city.Id);
        var occupied = fieldArmies.Where(unit => unit.Pool.Active > 0)
            .Select(unit => unit.Field.Position).ToHashSet();
        var released = new List<CombatUnit>();
        var remain = new List<CombatUnit>();

        var valid = waiting.Where(IsControllable)
            .GroupBy(unit => (Origin: unit.OriginCity!.Value, Direction: unit.EgressDirection!.Value))
            .OrderBy(group => group.Key.Origin.Value)
            .ThenBy(group => (int)group.Key.Direction);

        foreach (var group in valid)
        {
            var queue = group.OrderBy(unit => unit.Field.CommandOrder).ThenBy(unit => unit.Id.Value).ToList();
            if (allowFriendlyOverlap)
            {
                foreach (var candidate in queue)
                {
                    if (!citiesById.TryGetValue(group.Key.Origin, out var candidateOrigin)
                        || candidateOrigin.Owner != candidate.Field.Owner
                        || PassableExit(candidateOrigin, candidate, canEnter) is not { } usable)
                    {
                        remain.Add(candidate);
                        continue;
                    }
                    var candidateExit = usable.Exit;
                    bool IsFree(HexCoord exit) => (canEnter?.Invoke(candidate.Field.Domain, exit) ?? true)
                        && !fieldArmies.Concat(released).Any(other => other.Pool.Active > 0
                            && other.Field.Owner != candidate.Field.Owner && other.Field.Position == exit);
                    if (!IsFree(candidateExit)) { remain.Add(candidate); continue; }
                    released.Add(candidate with
                    {
                        Field = candidate.Field with { Position = candidateExit },
                        RenewalPosition = RenewalMovement.RenewalHexSpace.Center(candidateExit),
                        EgressDirection = usable.Direction,
                        EgressExit = candidateExit,
                        AwaitingEgress = false,
                    });
                }
                continue;
            }
            var head = queue[0];
            // 저장된 출구가 다른 방향/성의 타일로 오염되거나 원점이 함락되어도
            // 대기 병력이 엉뚱한 위치 또는 적 성에서 출격하지 않도록 한다.
            if (citiesById.TryGetValue(group.Key.Origin, out var origin)
                && origin.Owner == head.Field.Owner
                && PassableExit(origin, head, canEnter) is { } passable
                && !occupied.Contains(passable.Exit))
            {
                released.Add(head with
                {
                    Field = head.Field with { Position = passable.Exit },
                    EgressDirection = passable.Direction,
                    EgressExit = passable.Exit,
                    AwaitingEgress = false,
                });
                occupied.Add(passable.Exit);
                remain.AddRange(queue.Skip(1));
            }
            else
            {
                remain.AddRange(queue);
            }
        }

        // 잘못되었거나 레거시인 대기 상태는 유실하지 않는다. 저장 복구 후에도 성 내부에 남긴다.
        remain.AddRange(waiting.Where(unit => !IsControllable(unit)));
        return new DeploymentEgressResult(
            released.OrderBy(unit => unit.Field.CommandOrder).ThenBy(unit => unit.Id.Value).ToArray(),
            remain.OrderBy(unit => unit.Field.CommandOrder).ThenBy(unit => unit.Id.Value).ToArray());
    }

    private static bool IsControllable(CombatUnit unit)
        => unit.AwaitingEgress
            && unit.DeploymentDelayDays <= 0
            && unit.OriginCity.HasValue
            && unit.EgressDirection.HasValue
            && unit.EgressExit.HasValue;

    private static (DeploymentDirection Direction, HexCoord Exit)? PassableExit(
        City origin, CombatUnit unit, Func<MovementDomain, HexCoord, bool>? canEnter)
    {
        var direction = unit.EgressDirection!.Value;
        var saved = unit.EgressExit!.Value;
        if (DeploymentEgressRules.ExitGroup(origin, direction).Contains(saved)
            && (canEnter?.Invoke(unit.Field.Domain, saved) ?? true))
            return (direction, saved);

        return DeploymentEgressRules.Directions
            .SelectMany(candidate => DeploymentEgressRules.ExitGroup(origin, candidate)
                .Select(exit => (Direction: candidate, Exit: exit)))
            .Where(candidate => canEnter?.Invoke(unit.Field.Domain, candidate.Exit) ?? true)
            .OrderBy(candidate => candidate.Direction == direction ? 0 : 1)
            .ThenBy(candidate => unit.Field.Target is { } target
                ? candidate.Exit.Distance(target) : candidate.Exit.Distance(saved))
            .ThenBy(candidate => (int)candidate.Direction)
            .ThenBy(candidate => candidate.Exit.Q).ThenBy(candidate => candidate.Exit.R)
            .Cast<(DeploymentDirection Direction, HexCoord Exit)?>().FirstOrDefault();
    }
}
