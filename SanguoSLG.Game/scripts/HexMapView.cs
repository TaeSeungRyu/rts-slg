using System;
using System.Collections.Generic;
using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 헥사 맵과 도시를 그리는 순수 뷰. 게임 규칙은 없고 Core 데이터를 화면에 반영만 한다.
/// </summary>
public partial class HexMapView : Node2D
{
    [Export] public float HexSize = 34f;
    [Export] public Color FillColor = new(0.16f, 0.18f, 0.22f);
    [Export] public Color OutlineColor = new(0.32f, 0.36f, 0.44f);
    [Export] public Color CityColor = new(0.85f, 0.66f, 0.28f);

    private HexMap? _map;
    private IReadOnlyList<City> _cities = Array.Empty<City>();

    public void SetData(HexMap map, IReadOnlyList<City> cities)
    {
        _map = map;
        _cities = cities;
        QueueRedraw();
    }

    /// <summary>좌표의 화면 중심(다른 노드가 유닛 배치 등에 쓴다).</summary>
    public Vector2 CenterOf(HexCoord coord) => HexLayout.ToPixel(coord, HexSize);

    public override void _Draw()
    {
        if (_map is null)
        {
            return;
        }

        foreach (var tile in _map.Tiles())
        {
            var corners = HexLayout.Corners(CenterOf(tile), HexSize);
            DrawColoredPolygon(corners, FillColor);

            var outline = new Vector2[corners.Length + 1];
            corners.CopyTo(outline, 0);
            outline[^1] = corners[0];
            DrawPolyline(outline, OutlineColor, 1.5f, true);
        }

        foreach (var city in _cities)
        {
            DrawCircle(CenterOf(city.Position), HexSize * 0.42f, CityColor);
        }
    }
}
