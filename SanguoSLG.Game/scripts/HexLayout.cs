using Godot;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// axial(q,r) ↔ 화면 픽셀 변환(flat-top). 화면 좌표는 Game에서만 다룬다(Core는 픽셀을 모른다).
/// size는 헥사 중심에서 꼭짓점까지의 거리.
/// </summary>
public static class HexLayout
{
    private static readonly float Sqrt3 = Mathf.Sqrt(3f);

    public static Vector2 ToPixel(HexCoord coord, float size) =>
        new(size * 1.5f * coord.Q, size * Sqrt3 * (coord.R + coord.Q / 2f));

    /// <summary>flat-top 헥사의 6개 꼭짓점(중심 기준).</summary>
    public static Vector2[] Corners(Vector2 center, float size)
    {
        var points = new Vector2[6];
        for (var i = 0; i < 6; i++)
        {
            var angle = Mathf.DegToRad(60f * i);
            points[i] = center + new Vector2(size * Mathf.Cos(angle), size * Mathf.Sin(angle));
        }

        return points;
    }

    /// <summary>화면 픽셀 → 가장 가까운 헥사 좌표(flat-top). ToPixel의 역변환.</summary>
    public static HexCoord FromPixel(Vector2 pixel, float size)
    {
        var qf = 2f / 3f * (pixel.X / size);
        var rf = pixel.Y / (size * Sqrt3) - qf / 2f;
        return RoundAxial(qf, rf);
    }

    // 분수 axial 좌표를 cube 반올림으로 가장 가까운 헥사에 스냅한다.
    private static HexCoord RoundAxial(float qf, float rf)
    {
        float x = qf, z = rf, y = -x - z;
        int rx = Mathf.RoundToInt(x), ry = Mathf.RoundToInt(y), rz = Mathf.RoundToInt(z);
        float dx = Mathf.Abs(rx - x), dy = Mathf.Abs(ry - y), dz = Mathf.Abs(rz - z);

        if (dx > dy && dx > dz)
        {
            rx = -ry - rz;
        }
        else if (dy > dz)
        {
            ry = -rx - rz;
        }
        else
        {
            rz = -rx - ry;
        }

        return new HexCoord(rx, rz);
    }
}
