namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>성 발자국 바깥 경계를 여섯 방향 출구군으로 나누는 결정론적 규칙.</summary>
public static class DeploymentEgressRules
{
    public static readonly IReadOnlyList<DeploymentDirection> Directions =
        Enum.GetValues<DeploymentDirection>();

    public static HexCoord Offset(DeploymentDirection direction) => direction switch
    {
        DeploymentDirection.East => new HexCoord(1, 0),
        DeploymentDirection.NorthEast => new HexCoord(1, -1),
        DeploymentDirection.NorthWest => new HexCoord(0, -1),
        DeploymentDirection.West => new HexCoord(-1, 0),
        DeploymentDirection.SouthWest => new HexCoord(-1, 1),
        DeploymentDirection.SouthEast => new HexCoord(0, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(direction)),
    };

    public static IReadOnlyList<HexCoord> ExteriorTiles(City city)
    {
        var footprint = CastleFootprint.TilesFor(city).ToHashSet();
        return footprint.SelectMany(tile => tile.Neighbors())
            .Where(tile => !footprint.Contains(tile))
            .Distinct()
            .OrderBy(tile => tile.Q)
            .ThenBy(tile => tile.R)
            .ToArray();
    }

    public static IReadOnlyList<HexCoord> ExitGroup(City city, DeploymentDirection direction)
    {
        var footprint = CastleFootprint.TilesFor(city).ToArray();
        var centerQ = footprint.Average(tile => tile.Q);
        var centerR = footprint.Average(tile => tile.R);
        var centerS = footprint.Average(tile => tile.S);

        return ExteriorTiles(city)
            .Where(tile => Classify(tile, centerQ, centerR, centerS) == direction)
            .OrderByDescending(tile => Projection(tile, centerQ, centerR, centerS, direction))
            .ThenBy(tile => tile.Q)
            .ThenBy(tile => tile.R)
            .ToArray();
    }

    public static HexCoord? RepresentativeExit(
        City city,
        DeploymentDirection direction,
        HexCoord? target = null,
        Func<HexCoord, bool>? isUsable = null)
    {
        var candidates = ExitGroup(city, direction)
            .Where(tile => isUsable?.Invoke(tile) ?? true);

        return target is { } goal
            ? candidates.OrderBy(tile => tile.Distance(goal)).ThenBy(tile => tile.Q).ThenBy(tile => tile.R).Cast<HexCoord?>().FirstOrDefault()
            : candidates.Cast<HexCoord?>().FirstOrDefault();
    }

    public static DeploymentDirection Recommend(
        City city,
        HexCoord target,
        Func<HexCoord, bool>? isUsable = null)
    {
        return Directions
            .Select(direction => (Direction: direction, Exit: RepresentativeExit(city, direction, target, isUsable)))
            .Where(item => item.Exit.HasValue)
            // 비대칭인 중·대형성 발자국 모양 때문에 방향 자체가 바뀌지 않도록
            // 추천은 앵커의 순수 6방향 벡터로 정하고, 대표 출구만 출구군 안에서 고른다.
            .OrderBy(item => (city.Position + Offset(item.Direction)).Distance(target))
            .ThenBy(item => (int)item.Direction)
            .Select(item => item.Direction)
            .FirstOrDefault();
    }

    private static DeploymentDirection Classify(HexCoord tile, double centerQ, double centerR, double centerS)
        => Directions
            .OrderByDescending(direction => Projection(tile, centerQ, centerR, centerS, direction))
            .ThenBy(direction => (int)direction)
            .First();

    private static double Projection(
        HexCoord tile,
        double centerQ,
        double centerR,
        double centerS,
        DeploymentDirection direction)
    {
        var offset = Offset(direction);
        return (tile.Q - centerQ) * offset.Q
            + (tile.R - centerR) * offset.R
            + (tile.S - centerS) * offset.S;
    }
}
