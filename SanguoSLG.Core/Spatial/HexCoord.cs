namespace SanguoSLG.Core.Spatial;

/// <summary>
/// 헥사 그리드 좌표. flat-top 육각형, axial 좌표계(q, r)를 사용한다.
/// offset 좌표계와 섞지 않는다. Core는 화면 픽셀을 모르며,
/// 화면 좌표 변환은 Game(Godot) 프로젝트에서만 수행한다.
/// </summary>
public readonly record struct HexCoord(int Q, int R)
{
    /// <summary>cube 좌표의 세 번째 축. 항상 q + r + s = 0 을 만족한다.</summary>
    public int S => -Q - R;

    /// <summary>인접 6방향(axial 기준). 결정론을 위해 순서가 고정되어 있다.</summary>
    private static readonly HexCoord[] Directions =
    {
        new(1, 0), new(1, -1), new(0, -1),
        new(-1, 0), new(-1, 1), new(0, 1),
    };

    /// <summary>두 좌표 사이의 헥사 거리.</summary>
    public int Distance(HexCoord other)
    {
        var dq = Math.Abs(Q - other.Q);
        var dr = Math.Abs(R - other.R);
        var ds = Math.Abs(S - other.S);
        return (dq + dr + ds) / 2;
    }

    /// <summary>인접한 6개 좌표를 고정된 순서로 반환한다.</summary>
    public IEnumerable<HexCoord> Neighbors()
    {
        foreach (var d in Directions)
        {
            yield return new HexCoord(Q + d.Q, R + d.R);
        }
    }

    public static HexCoord operator +(HexCoord a, HexCoord b) => new(a.Q + b.Q, a.R + b.R);

    public static HexCoord operator -(HexCoord a, HexCoord b) => new(a.Q - b.Q, a.R - b.R);
}
