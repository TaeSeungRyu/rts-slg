namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 부대 이동 진입점. 목표 좌표까지 A* 경로를 찾아 부대를 이동시킨다.
/// 자유 클릭 이동 단계에서는 이동력 제약 없이 경로 끝(목표)까지 이동한다.
/// Game은 이 결과의 Path로 애니메이션을, Unit으로 최종 상태를 반영한다.
/// </summary>
public sealed class MovementService
{
    private readonly HexPathfinder _pathfinder;

    public MovementService(HexMap map)
        : this(new HexPathfinder(map))
    {
    }

    public MovementService(HexPathfinder pathfinder) => _pathfinder = pathfinder;

    /// <summary>부대를 목표 좌표로 이동시킨다. 도달 불가면 원위치·빈 경로를 반환한다.</summary>
    public MoveResult MoveTo(Unit unit, HexCoord target)
    {
        var path = _pathfinder.FindPath(unit.Position, target);
        return path.Count == 0
            ? new MoveResult(unit, path)
            : new MoveResult(unit.MoveTo(path[^1]), path);
    }
}
