namespace SanguoSLG.Core.Simulation.RenewalMovement;

using SanguoSLG.Core.Spatial;

/// <summary>신규 이동 모드의 지형 조회·경로 후보·고정 장애물 원본.</summary>
public sealed class RenewalMovementMap
{
    private readonly HexMap _map;
    private readonly HashSet<HexCoord> _buildingTiles;
    private readonly IReadOnlyList<RenewalStaticObstacle> _obstacles;

    public RenewalMovementMap(HexMap map, IEnumerable<HexCoord>? buildingTiles = null,
        IEnumerable<RenewalStaticObstacle>? obstacles = null)
    {
        _map = map;
        _buildingTiles = buildingTiles?.ToHashSet() ?? [];
        _obstacles = obstacles is null
            ? Array.Empty<RenewalStaticObstacle>()
            : obstacles.OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
    }

    public TerrainType TerrainAt(ContinuousPosition position) =>
        _map.TerrainAt(RenewalHexSpace.NearestHex(position));

    public int SpeedPercentAt(ContinuousPosition position, long radius = 0)
    {
        var center = RenewalHexSpace.NearestHex(position);
        var percent = 100;
        foreach (var hex in center.Neighbors().Append(center))
        {
            const long tileRadius = 577;
            var reach = checked(radius + tileRadius);
            if (_map.Contains(hex)
                && position.DistanceSquaredTo(RenewalHexSpace.Center(hex)) <= checked(reach * reach)
                && _map.TerrainAt(hex) is TerrainType.Mountain or TerrainType.Swamp or TerrainType.River)
            {
                percent = 50;
            }
        }
        return percent;
    }

    public bool CanEnter(MovementDomain domain, HexCoord hex) => _map.Contains(hex)
        && !_buildingTiles.Contains(hex) && TerrainRules.CanEnter(domain, _map.TerrainAt(hex));

    public IReadOnlyList<ContinuousPosition> FindPath(MovementDomain domain,
        ContinuousPosition start, ContinuousPosition destination)
    {
        if (CanStand(domain, destination, RenewalAdvanceSimulator.UnitCollisionRadius)
            && FirstStaticCollision(start, destination,
                RenewalAdvanceSimulator.UnitCollisionRadius, domain) == RenewalStopReason.None)
        {
            return [destination];
        }
        var startHex = RenewalHexSpace.NearestHex(start);
        var destinationHex = RenewalHexSpace.NearestHex(destination);
        var path = new HexPathfinder(hex => CanEnter(domain, hex)).FindPath(startHex, destinationHex);
        if (path.Count == 0)
        {
            return Array.Empty<ContinuousPosition>();
        }

        var result = path.Skip(1).Select(RenewalHexSpace.Center).ToList();
        if (result.Count > 0 && FirstStaticCollision(start, result[0],
                RenewalAdvanceSimulator.UnitCollisionRadius, domain) != RenewalStopReason.None)
        {
            var startCenter = RenewalHexSpace.Center(startHex);
            if (startCenter != start && CanStand(domain, startCenter,
                    RenewalAdvanceSimulator.UnitCollisionRadius)
                && FirstStaticCollision(start, startCenter,
                    RenewalAdvanceSimulator.UnitCollisionRadius, domain) == RenewalStopReason.None)
                result.Insert(0, startCenter);
        }
        if (result.Count == 0 || result[^1] != destination)
        {
            result.Add(destination);
        }
        return result;
    }

    public RenewalStopReason FirstStaticCollision(ContinuousPosition from, ContinuousPosition to,
        long unitRadius, MovementDomain domain)
    {
        foreach (var obstacle in _obstacles)
        {
            var reach = checked(unitRadius + obstacle.Radius);
            if (SegmentTouchesCircle(from, to, obstacle.Center, reach)
                && !EscapingBlockedCenter(from, to, obstacle.Center, reach))
            {
                return RenewalStopReason.BuildingBlocked;
            }
        }

        var samples = Math.Max(1L, from.DistanceTo(to) / 50);
        for (var index = 1L; index <= samples; index++)
        {
            var point = new ContinuousPosition(
                from.X + (to.X - from.X) * index / samples,
                from.Y + (to.Y - from.Y) * index / samples);
            var blocked = BlockedReasonAt(point, unitRadius, domain, from);
            if (blocked != RenewalStopReason.None)
            {
                return blocked;
            }
        }
        return RenewalStopReason.None;
    }

    public bool CanStand(MovementDomain domain, ContinuousPosition position, long unitRadius) =>
        CanEnter(domain, RenewalHexSpace.NearestHex(position))
        && FirstStaticCollision(position, position, unitRadius, domain) == RenewalStopReason.None;

    private RenewalStopReason BlockedReasonAt(ContinuousPosition position, long radius,
        MovementDomain domain, ContinuousPosition from)
    {
        var center = RenewalHexSpace.NearestHex(position);
        foreach (var hex in center.Neighbors().Append(center))
        {
            const long tileRadius = 577;
            var reach = checked(radius + tileRadius);
            if (position.DistanceSquaredTo(RenewalHexSpace.Center(hex)) > checked(reach * reach))
            {
                continue;
            }
            if (_buildingTiles.Contains(hex))
            {
                if (EscapingBlockedCenter(from, position, RenewalHexSpace.Center(hex), reach)) continue;
                return RenewalStopReason.BuildingBlocked;
            }
            if (!_map.Contains(hex) || !TerrainRules.CanEnter(domain, _map.TerrainAt(hex)))
            {
                if (EscapingBlockedCenter(from, position, RenewalHexSpace.Center(hex), reach)) continue;
                return RenewalStopReason.TerrainBlocked;
            }
        }
        return RenewalStopReason.None;
    }

    private static bool EscapingBlockedCenter(ContinuousPosition from, ContinuousPosition to,
        ContinuousPosition center, long reach) =>
        from.DistanceSquaredTo(center) <= checked(reach * reach)
        && to.DistanceSquaredTo(center) > from.DistanceSquaredTo(center);

    public static bool SegmentTouchesCircle(ContinuousPosition from, ContinuousPosition to,
        ContinuousPosition center, long radius)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var lengthSquared = checked(dx * dx + dy * dy);
        if (lengthSquared == 0)
        {
            return from.DistanceSquaredTo(center) <= checked(radius * radius);
        }

        var projection = checked((center.X - from.X) * dx + (center.Y - from.Y) * dy);
        projection = Math.Clamp(projection, 0, lengthSquared);
        var closestXNumerator = checked(from.X * lengthSquared + dx * projection);
        var closestYNumerator = checked(from.Y * lengthSquared + dy * projection);
        var deltaX = checked(center.X * lengthSquared - closestXNumerator);
        var deltaY = checked(center.Y * lengthSquared - closestYNumerator);
        // 좌표 규모가 작은 검수용 논리 평면이므로 decimal로 오버플로 없이 정확 비교한다.
        var distanceNumerator = (decimal)deltaX * deltaX + (decimal)deltaY * deltaY;
        var radiusNumerator = (decimal)radius * radius * lengthSquared * lengthSquared;
        return distanceNumerator <= radiusNumerator;
    }
}
