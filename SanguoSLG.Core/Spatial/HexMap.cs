namespace SanguoSLG.Core.Spatial;

/// <summary>
/// 평평한 헥사 맵. axial(q, r)의 직사각(min/max) 경계로 정의된다.
/// 지금은 지형이 없어 경계 안의 모든 타일이 통행 가능하다.
/// Core는 화면 픽셀을 모른다 — axial↔픽셀 변환은 Game 프로젝트에서만 한다.
/// </summary>
public sealed class HexMap
{
    public int MinQ { get; }
    public int MaxQ { get; }
    public int MinR { get; }
    public int MaxR { get; }

    private readonly IReadOnlyDictionary<HexCoord, TerrainType> _terrain;

    public HexMap(int minQ, int maxQ, int minR, int maxR, IReadOnlyDictionary<HexCoord, TerrainType>? terrain = null)
    {
        if (maxQ < minQ)
        {
            throw new ArgumentException("maxQ는 minQ 이상이어야 한다.", nameof(maxQ));
        }

        if (maxR < minR)
        {
            throw new ArgumentException("maxR는 minR 이상이어야 한다.", nameof(maxR));
        }

        MinQ = minQ;
        MaxQ = maxQ;
        MinR = minR;
        MaxR = maxR;
        _terrain = terrain ?? new Dictionary<HexCoord, TerrainType>();
    }

    /// <summary>좌표의 지형. 지정되지 않은 타일은 평야(Plains).</summary>
    public TerrainType TerrainAt(HexCoord coord) =>
        _terrain.TryGetValue(coord, out var terrain) ? terrain : TerrainType.Plains;

    /// <summary>맵 타일 개수.</summary>
    public int Count => (MaxQ - MinQ + 1) * (MaxR - MinR + 1);

    /// <summary>좌표가 맵 경계 안에 있는가.</summary>
    public bool Contains(HexCoord coord) =>
        coord.Q >= MinQ && coord.Q <= MaxQ && coord.R >= MinR && coord.R <= MaxR;

    /// <summary>통행 가능 여부. 평평한 필드에서는 경계 안이면 통행 가능.</summary>
    public bool IsPassable(HexCoord coord) => Contains(coord);

    /// <summary>모든 타일을 결정론적 순서(q 바깥, r 안쪽)로 열거한다.</summary>
    public IEnumerable<HexCoord> Tiles()
    {
        for (var q = MinQ; q <= MaxQ; q++)
        {
            for (var r = MinR; r <= MaxR; r++)
            {
                yield return new HexCoord(q, r);
            }
        }
    }
}
