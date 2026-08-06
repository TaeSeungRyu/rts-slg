namespace SanguoSLG.Core.Spatial;

/// <summary>
/// 타일별 파괴 상태를 담는 가변 맵. 시뮬레이션이 갱신하고 표현 계층이 읽는다.
/// 지정되지 않은 타일은 <see cref="TileCondition.Normal"/>이므로 정상 타일은 저장하지 않는다.
/// </summary>
public sealed class TileConditionMap
{
    private readonly Dictionary<HexCoord, TileCondition> _conditions = new();

    public TileConditionMap(IReadOnlyDictionary<HexCoord, TileCondition>? initial = null)
    {
        if (initial is null)
        {
            return;
        }

        foreach (var (coord, condition) in initial)
        {
            Set(coord, condition);
        }
    }

    /// <summary>좌표의 상태. 지정되지 않았으면 정상.</summary>
    public TileCondition At(HexCoord coord) =>
        _conditions.TryGetValue(coord, out var condition) ? condition : TileCondition.Normal;

    /// <summary>상태를 지정한다. 정상으로 되돌리면 항목을 제거해 맵을 최소로 유지한다.</summary>
    public void Set(HexCoord coord, TileCondition condition)
    {
        if (condition == TileCondition.Normal)
        {
            _conditions.Remove(coord);
            return;
        }

        _conditions[coord] = condition;
    }

    /// <summary>정상이 아닌 타일을 결정론적 순서(q 바깥, r 안쪽)로 열거한다.</summary>
    public IEnumerable<KeyValuePair<HexCoord, TileCondition>> Damaged() =>
        _conditions
            .OrderBy(pair => pair.Key.Q)
            .ThenBy(pair => pair.Key.R);

    /// <summary>정상이 아닌 타일 수.</summary>
    public int DamagedCount => _conditions.Count;
}
