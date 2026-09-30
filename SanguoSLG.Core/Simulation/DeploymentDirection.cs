namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 성·항구에서 처음 야전으로 나갈 여섯 방향. 값의 순서는 <c>HexCoord</c>의
/// 시계 방향 이웃 순서와 맞추며, 세이브 데이터에 기록되므로 변경하지 않는다.
/// </summary>
public enum DeploymentDirection
{
    East = 0,
    NorthEast = 1,
    NorthWest = 2,
    West = 3,
    SouthWest = 4,
    SouthEast = 5,
}
