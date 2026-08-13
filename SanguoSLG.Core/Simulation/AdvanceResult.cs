namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 한 번의 "진행" 결과. <paramref name="Ticks"/>는 스텝 단위 스냅샷의 순서열(GUI 재생용),
/// <paramref name="Units"/>는 최종 상태(입성 부대 제외), <paramref name="Reason"/>은 멈춘 이유,
/// <paramref name="Days"/>는 실제로 흐른 일수, <paramref name="Entered"/>는 이 진행에
/// 아군 성에 입성해 야전에서 빠진 부대(UnitId 오름차순).
/// </summary>
public sealed record AdvanceResult(
    IReadOnlyList<MovementTick> Ticks,
    IReadOnlyList<FieldUnit> Units,
    StopReason Reason,
    int Days,
    IReadOnlyList<UnitId>? Entered = null)
{
    public IReadOnlyList<UnitId> EnteredCastle => Entered ?? [];
}
