namespace SanguoSLG.Core.Spatial;

/// <summary>
/// 헥사 그리드 A* 길찾기. 스텝 비용 1, 휴리스틱은 헥사 거리.
/// 통행 판정은 주입된 조건을 쓴다(평평한 필드는 HexMap.IsPassable, 이후 지형이 여기에 꽂힌다).
/// Godot의 AStarGrid2D는 쓰지 않는다(Core 규약).
/// </summary>
public sealed class HexPathfinder
{
    private readonly Func<HexCoord, bool> _isPassable;

    public HexPathfinder(HexMap map)
        : this(map.IsPassable)
    {
    }

    public HexPathfinder(Func<HexCoord, bool> isPassable) => _isPassable = isPassable;

    /// <summary>
    /// start에서 goal까지 최단 경로를 start·goal 포함 목록으로 반환한다.
    /// 도달 불가하거나 start/goal이 통행 불가면 빈 목록을 반환한다.
    /// </summary>
    public IReadOnlyList<HexCoord> FindPath(HexCoord start, HexCoord goal)
    {
        if (!_isPassable(start) || !_isPassable(goal))
        {
            return Array.Empty<HexCoord>();
        }

        var frontier = new PriorityQueue<HexCoord, int>();
        frontier.Enqueue(start, 0);
        var cameFrom = new Dictionary<HexCoord, HexCoord>();
        var costSoFar = new Dictionary<HexCoord, int> { [start] = 0 };

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (current == goal)
            {
                return Reconstruct(cameFrom, current);
            }

            foreach (var next in current.Neighbors())
            {
                if (!_isPassable(next))
                {
                    continue;
                }

                var newCost = costSoFar[current] + 1;
                if (!costSoFar.TryGetValue(next, out var existing) || newCost < existing)
                {
                    costSoFar[next] = newCost;
                    cameFrom[next] = current;
                    frontier.Enqueue(next, newCost + next.Distance(goal));
                }
            }
        }

        return Array.Empty<HexCoord>();
    }

    private static IReadOnlyList<HexCoord> Reconstruct(IReadOnlyDictionary<HexCoord, HexCoord> cameFrom, HexCoord current)
    {
        var path = new List<HexCoord> { current };
        while (cameFrom.TryGetValue(current, out var previous))
        {
            current = previous;
            path.Add(current);
        }

        path.Reverse();
        return path;
    }
}
