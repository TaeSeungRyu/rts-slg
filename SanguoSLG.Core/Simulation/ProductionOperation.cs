namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>생산 작전 단계. 출발 이동 → 채집 → 복귀 이동 → 보상 지급 순서다.</summary>
public enum ProductionPhase
{
    Outbound,
    Gathering,
    Returning,
}

/// <summary>
/// 논·밭·마을 생산 작전 상태. 500명 고정 부대와 장수 1명이 시설로 이동해 채집하고 자동 복귀한다.
/// 야전 전투부대와 달리 채집 중에는 지도에서 숨겨지며, 시설 피격 시 전멸 처리할 수 있게 별도 상태로 둔다.
/// </summary>
public sealed record ProductionOperation(
    int Id,
    CityId City,
    FactionId Owner,
    HexCoord Origin,
    HexCoord Target,
    string Facility,
    string TroopCode,
    int Troops,
    int TrainingLevel,
    int Speed,
    GeneralId General,
    int StartedDay,
    int GatherDays,
    ProductionPhase Phase = ProductionPhase.Outbound,
    int PhaseStartedDay = 0,
    HexCoord Position = default,
    IReadOnlyList<HexCoord>? OutboundPath = null,
    IReadOnlyList<HexCoord>? ReturnPath = null)
{
    public const int FixedTroops = 500;

    public IReadOnlyList<HexCoord> OutPath => OutboundPath ?? [];

    public IReadOnlyList<HexCoord> BackPath => ReturnPath ?? [];

    public int GatherElapsed(int currentDay)
        => Phase == ProductionPhase.Gathering ? System.Math.Max(0, currentDay - PhaseStartedDay) : 0;

    public int GatherRemaining(int currentDay)
        => Phase == ProductionPhase.Gathering ? System.Math.Max(0, GatherDays - GatherElapsed(currentDay)) : GatherDays;
}
