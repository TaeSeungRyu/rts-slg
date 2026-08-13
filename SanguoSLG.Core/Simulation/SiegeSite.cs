namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 이동 시뮬레이션이 보는 성(공성 대상 건물). 적 공격모드 부대가 자신의 공성 사거리
/// 안에 이 성을 두면 야전 접적과 같은 규칙으로 진행이 중단된다(그 날 이동 완료 후).
/// </summary>
public sealed record SiegeSite(HexCoord Position, FactionId Owner);
