namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 한 번의 "진행" 결과. <paramref name="Ticks"/>는 스텝 단위 스냅샷의 순서열(GUI 재생용),
/// <paramref name="Units"/>는 최종 상태, <paramref name="Reason"/>은 멈춘 이유,
/// <paramref name="Days"/>는 실제로 흐른 일수.
/// </summary>
public sealed record AdvanceResult(
    IReadOnlyList<MovementTick> Ticks,
    IReadOnlyList<FieldUnit> Units,
    StopReason Reason,
    int Days);
