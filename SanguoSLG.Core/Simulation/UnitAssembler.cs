namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 장수와 병종으로 부대를 조립한다(spec-general·design-skill "부대의 장수 2명").
/// 적성(병종별 통솔)은 선봉만, 스킬은 두 장수 모두 반영한다. 무력·지력(무쌍·계략)도 선봉 기준.
/// 스탯은 중립 지형(River — 어떤 분류도 보정 없음)으로 만든다 — 지형 공방 보정은 전투 시점에
/// 오케스트레이터가 이동 후 위치로 얹는다.
/// </summary>
public static class UnitAssembler
{
    public static CombatUnit Assemble(
        UnitId id, FactionId owner, HexCoord position, UnitMode mode, HexCoord? target, int commandOrder,
        General vanguard, General? adjutant, TroopTemplate template, int troops,
        IReadOnlyDictionary<string, ActiveSkill> actives,
        IReadOnlyDictionary<string, PassiveSkill> passives,
        CombatContext context, int researchLevel = 0, IReadOnlyList<HexCoord>? waypoints = null)
    {
        var grade = vanguard.AptitudeFor(template.Class);

        var held = vanguard.Passives
            .Concat(adjutant?.Passives ?? [])
            .Select(s => (passives[s.Code], s.Tier));
        var (atkBonus, dfBonus) = PassiveBucketEvaluator.Evaluate(held, context);

        var stats = CombatStatsBuilder.BuildField(template, grade, researchLevel, TerrainType.River,
            troops, atkBonusPercent: atkBonus, dfBonusPercent: dfBonus);

        var domain = template.Class == TroopClass.Naval ? MovementDomain.DeepWater : MovementDomain.Land;
        var field = new FieldUnit(id, owner, position,
            template.MovementPerDay, template.Detection, template.RangeUnit,
            domain, mode, target, commandOrder, template.RangeCastle, waypoints);

        var state = UnitCombatState.Create(
            vanguard.Intellect,
            vanguardActive: Resolve(vanguard.BattleActive, actives),
            adjutantActive: Resolve(adjutant?.BattleActive, actives));

        return new CombatUnit(field, stats, new TroopPool(troops, 0), state,
            vanguard.Might, vanguard.Intellect, troops, template.Class,
            ProvisionsCapacity: template.ProvisionsCapacity, TroopCode: template.Code,
            VanguardId: vanguard.Id, AdjutantId: adjutant?.Id);
    }

    private static ActiveSkill? Resolve(string? code, IReadOnlyDictionary<string, ActiveSkill> actives)
        => code is null ? null : actives[code];
}
