namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 전투 페이즈 발동(design-combat.md). 진행이 멈춘 시점의 위치로 <b>사거리 안 모든 적대 쌍을
/// 전수 검사</b>해 교전을 만든다. 공격모드·전진모드 부대가 자기 사거리 안 적을 친다(행군모드는
/// 공격하지 않음 — 지나갈 뿐). 한 부대가 여럿을 치면 다대일(주대상 100%/나머지 60%)이다.
/// 순서는 명령 순번 기반이라 결정론적이다.
/// </summary>
public static class CombatPhase
{
    public static IReadOnlyList<UnitEngagement> DetectEngagements(IReadOnlyList<CombatUnit> units)
    {
        if (units.All(unit => unit.RenewalPosition is null))
            return DetectEngagements(units.Select(unit => unit.Field).ToList());
        var intentions = new Dictionary<UnitId, List<UnitId>>();
        bool InRange(CombatUnit source, CombatUnit target) =>
            (source.RenewalPosition ?? RenewalMovement.RenewalHexSpace.Center(source.Field.Position))
                .DistanceTo(target.RenewalPosition ?? RenewalMovement.RenewalHexSpace.Center(target.Field.Position))
            <= source.Field.AttackRange * RenewalMovement.ContinuousPosition.UnitsPerTile;
        long DistanceSquared(CombatUnit source, CombatUnit target) =>
            (source.RenewalPosition ?? RenewalMovement.RenewalHexSpace.Center(source.Field.Position))
                .DistanceSquaredTo(target.RenewalPosition
                    ?? RenewalMovement.RenewalHexSpace.Center(target.Field.Position));
        foreach (var attacker in units.OrderBy(unit => unit.Field.CommandOrder).ThenBy(unit => unit.Id.Value))
        {
            if (attacker.Field.Mode is UnitMode.March or UnitMode.Standby || !attacker.CanInitiateCombat) continue;
            var targets = units.Where(target => target.Field.Owner != attacker.Field.Owner && InRange(attacker, target)
                && (attacker.Field.Mode != UnitMode.Attack
                    || (attacker.Field.AssignedUnitTarget is { } id ? target.Id == id
                        : target.Field.Position == attacker.Field.Target)))
                .OrderBy(target => DistanceSquared(attacker, target))
                .ThenBy(target => target.Field.CommandOrder).ThenBy(target => target.Id.Value)
                .Select(target => target.Id).ToList();
            if (targets.Count > 0) intentions[attacker.Id] = targets;
        }
        foreach (var defender in units.Where(unit => unit.Field.Mode == UnitMode.Standby && unit.CanInitiateCombat))
        {
            var counters = units.Where(attacker => intentions.TryGetValue(attacker.Id, out var targets)
                && targets.Contains(defender.Id) && InRange(defender, attacker))
                .OrderBy(unit => unit.Field.CommandOrder).ThenBy(unit => unit.Id.Value)
                .Select(unit => unit.Id).ToList();
            if (counters.Count > 0) intentions[defender.Id] = counters;
        }
        return units.OrderBy(unit => unit.Field.CommandOrder).ThenBy(unit => unit.Id.Value)
            .Where(unit => intentions.ContainsKey(unit.Id))
            .Select(unit => new UnitEngagement(unit.Id, intentions[unit.Id])).ToList();
    }

    /// <summary>정지 시점 위치에서 발동하는 교전 목록(공격 명령 순번, 동률이면 UnitId 오름차순).</summary>
    public static IReadOnlyList<UnitEngagement> DetectEngagements(IReadOnlyList<FieldUnit> units)
    {
        var result = new List<UnitEngagement>();

        foreach (var attacker in units.OrderBy(u => u.CommandOrder).ThenBy(u => u.Id.Value))
        {
            // 행군모드는 공격하지 않는다(전진·공격모드만 교전 개시).
            if (attacker.Mode == UnitMode.March)
            {
                continue;
            }

            var targets = units
                .Where(t => t.Owner != attacker.Owner
                    && t.Position.Distance(attacker.Position) <= attacker.AttackRange)
                .OrderBy(t => t.Position.Distance(attacker.Position))
                .ThenBy(t => t.CommandOrder)
                .ThenBy(t => t.Id.Value)
                .Select(t => t.Id)
                .ToList();

            if (targets.Count > 0)
            {
                result.Add(new UnitEngagement(attacker.Id, targets));
            }
        }

        return result;
    }
}
