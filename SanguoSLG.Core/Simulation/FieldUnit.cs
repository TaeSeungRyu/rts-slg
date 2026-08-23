namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 이동 시뮬레이션의 전술 부대(불변). 도메인 <see cref="Unit"/>은 위치·소속만 갖고,
/// 여기에 병종 스탯(속도·탐지·사거리·통행 영역)과 명령(모드·목표)을 얹는다.
/// 병종 데이터(troop-types.json)가 생기면 스탯은 그쪽에서 채운다.
/// </summary>
public sealed record FieldUnit(
    UnitId Id,
    FactionId Owner,
    HexCoord Position,
    int Speed,
    int Detection,
    int AttackRange,
    MovementDomain Domain,
    UnitMode Mode,
    HexCoord? Target,
    int CommandOrder,
    int RangeCastle = 1,
    IReadOnlyList<HexCoord>? Waypoints = null)
{
    public FieldUnit MoveTo(HexCoord position) => this with { Position = position };
}
