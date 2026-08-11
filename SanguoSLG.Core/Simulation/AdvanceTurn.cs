namespace SanguoSLG.Core.Simulation;

/// <summary>한 "진행"(이동 + 전투 페이즈)의 결과.</summary>
/// <param name="Units">갱신된 부대들(위치·병력·발동 상태 반영, UnitId 오름차순).</param>
/// <param name="Movement">이동 시뮬 결과(틱·정지 사유·경과일).</param>
/// <param name="Combat">전투 페이즈 정산 결과(교전이 없었으면 null).</param>
/// <param name="FiredActives">이 진행에 액티브를 발동한 부대 → 그 액티브(선봉 우선 1개).</param>
/// <param name="FiredStratagems">이 진행에 계략을 발동한 부대 → 그 계략.</param>
/// <param name="StatusDamage">이 진행에 지속 상태(화계·독무 tick)로 병력이 깎인 부대 → 그 피해량.</param>
/// <param name="StratagemDamage">이 진행에 계략 즉발(낙뢰·폭파·교란·수공 버스트)로 병력이 깎인 부대 → 그 피해량.</param>
public sealed record AdvanceTurn(
    IReadOnlyList<CombatUnit> Units,
    AdvanceResult Movement,
    CombatPhaseResult? Combat,
    IReadOnlyDictionary<Domain.UnitId, ActiveSkill> FiredActives,
    IReadOnlyDictionary<Domain.UnitId, Stratagem> FiredStratagems,
    IReadOnlyDictionary<Domain.UnitId, int> StatusDamage,
    IReadOnlyDictionary<Domain.UnitId, int> StratagemDamage);
