using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 헥사 맵을 지형 스프라이트(Kenney Hexagon Pack, flat-top 2.5D)로 그리고,
/// 그 위에 도시 마커·이름을 표시하는 순수 뷰. 게임 규칙은 없다.
/// </summary>
public partial class HexMapView : Node2D
{
    // 지형 스프라이트 기준값: pointy-top 헥사가 120x140 스프라이트 정중앙, 반경 70.
    private const float NativeHexSize = 70f;
    private static readonly Vector2 TileSize = new(120f, 140f);
    private static readonly Vector2 TopFaceCenter = new(60f, 70f);
    private static readonly Rect2 GrassRegion = new(610, 142, 120, 140); // grass_05 (순수 녹색)

    [Export] public float HexSize = 48f;
    [Export] public Color CityColor = new(0.85f, 0.66f, 0.28f);
    [Export] public int LabelSize = 18;
    [Export] public Color LabelColor = new(0.95f, 0.96f, 0.98f);

    private HexMap? _map;
    private IReadOnlyList<City> _cities = Array.Empty<City>();
    private Font _font = null!;
    private Texture2D _grassTile = null!;

    public override void _Ready()
    {
        _font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf");
        var sheet = GD.Load<Texture2D>("res://assets/tiles/hexagonTerrain_sheet.png");
        _grassTile = new AtlasTexture { Atlas = sheet, Region = GrassRegion };
    }

    public void SetData(HexMap map, IReadOnlyList<City> cities)
    {
        _map = map;
        _cities = cities;
        QueueRedraw();
    }

    public Vector2 CenterOf(HexCoord coord) => HexLayout.ToPixel(coord, HexSize);

    public override void _Draw()
    {
        if (_map is null)
        {
            return;
        }

        var scale = HexSize / NativeHexSize;
        var drawSize = TileSize * scale;
        var centerOffset = TopFaceCenter * scale;

        // 2.5D 깊이가 겹치도록 화면 위쪽(작은 y)부터 그린다.
        foreach (var tile in _map.Tiles().OrderBy(t => CenterOf(t).Y).ThenBy(t => CenterOf(t).X))
        {
            DrawTextureRect(_grassTile, new Rect2(CenterOf(tile) - centerOffset, drawSize), false);
        }

        foreach (var city in _cities)
        {
            var center = CenterOf(city.Position);
            DrawCircle(center, HexSize * 0.30f, CityColor);

            var textSize = _font.GetStringSize(city.Name, HorizontalAlignment.Left, -1, LabelSize);
            var labelPos = new Vector2(center.X - textSize.X / 2f, center.Y + HexSize * 0.30f + LabelSize + 4f);
            DrawStringOutline(_font, labelPos, city.Name, HorizontalAlignment.Left, -1, LabelSize, 4, new Color(0f, 0f, 0f, 0.75f));
            DrawString(_font, labelPos, city.Name, HorizontalAlignment.Left, -1, LabelSize, LabelColor);
        }
    }
}
