namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 한 스텝(칸 단위 동시 이동) 이후의 스냅샷. GUI가 스텝마다 트윈으로 재생한다.
/// <paramref name="Units"/>는 UnitId 오름차순(결정론), 사건은 발생 순서대로.
/// </summary>
public sealed record MovementTick(
    int Day,
    IReadOnlyList<FieldUnit> Units,
    IReadOnlyList<TickEvent> Events);
