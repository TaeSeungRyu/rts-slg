using Godot;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// axial(q,r) ↔ 화면 픽셀 변환(pointy-top). 화면 좌표는 Game에서만 다룬다(Core는 픽셀을 모른다).
/// pointy-top을 쓰는 이유: 지형 아트(Kenney Hexagon Pack)가 pointy-top이기 때문.
/// size는 헥사 중심에서 꼭짓점까지의 거리.
/// </summary>
public static class HexLayout
{
    private static readonly float Sqrt3 = Mathf.Sqrt(3f);

    public static Vector2 ToPixel(HexCoord coord, float size) =>
        new(size * Sqrt3 * (coord.Q + coord.R / 2f), size * 1.5f * coord.R);

    /// <summary>pointy-top 헥사의 6개 꼭짓점(중심 기준).</summary>
    public static Vector2[] Corners(Vector2 center, float size)
    {
        var points = new Vector2[6];
        for (var i = 0; i < 6; i++)
        {
            var angle = Mathf.DegToRad(60f * i - 30f);
            points[i] = center + new Vector2(size * Mathf.Cos(angle), size * Mathf.Sin(angle));
        }

        return points;
    }

    /// <summary>화면 픽셀 → 가장 가까운 헥사 좌표(pointy-top). ToPixel의 역변환.</summary>
    public static HexCoord FromPixel(Vector2 pixel, float size)
    {
        var qf = (Sqrt3 / 3f * pixel.X - 1f / 3f * pixel.Y) / size;
        var rf = 2f / 3f * pixel.Y / size;
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
