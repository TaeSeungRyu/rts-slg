using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public partial class CampaignMiniMap : Control
{
    public HexMap Map { get; set; } = null!;
    public Func<HexCoord, Vector3> WorldAt { get; set; } = null!;
    public Func<IReadOnlySet<HexCoord>> VisibleTiles { get; set; } = null!;
    public Func<IEnumerable<(HexCoord Position, Color Color)>> Cities { get; set; } = null!;
    public Func<Rect2> CameraBounds { get; set; } = null!;
    public Action<Vector3> FocusCamera { get; set; } = null!;

    private float _minX;
    private float _minZ;
    private float _spanX;
    private float _spanZ;
    private double _refresh;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(280, 180);
        var points = Map.Tiles().Select(WorldAt).ToArray();
        _minX = points.Min(point => point.X);
        _minZ = points.Min(point => point.Z);
        _spanX = Math.Max(0.01f, points.Max(point => point.X) - _minX);
        _spanZ = Math.Max(0.01f, points.Max(point => point.Z) - _minZ);
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _refresh += delta;
        if (_refresh < 0.12) return;
        _refresh = 0;
        QueueRedraw();
    }

    private Vector2 Project(Vector3 world)
        => new(8 + (world.X - _minX) / _spanX * (Size.X - 16),
            8 + (world.Z - _minZ) / _spanZ * (Size.Y - 16));

    public override void _Draw()
    {
        if (Map is null) return;
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.06f, 0.09f, 0.11f));
        var visible = VisibleTiles();
        var cellWidth = Math.Max(2f, (Size.X - 16) / (Map.MaxQ - Map.MinQ + 1) * 0.75f);
        var cellHeight = Math.Max(2f, (Size.Y - 16) / (Map.MaxR - Map.MinR + 1) * 0.75f);
        foreach (var tile in Map.Tiles())
        {
            var terrain = Map.TerrainAt(tile);
            var color = terrain switch
            {
                TerrainType.WaterShallow or TerrainType.WaterDeep or TerrainType.WaterRocks
                    => new Color(0.20f, 0.39f, 0.52f),
                TerrainType.Mountain or TerrainType.RockMountain or TerrainType.Karst
                    or TerrainType.Cliff or TerrainType.IceMountain or TerrainType.IceWallLarge
                    or TerrainType.IceWallSmall => new Color(0.55f, 0.53f, 0.48f),
                TerrainType.Forest => new Color(0.30f, 0.48f, 0.34f),
                TerrainType.Desert or TerrainType.DesertCactus => new Color(0.68f, 0.56f, 0.37f),
                _ => new Color(0.46f, 0.58f, 0.45f),
            };
            if (!visible.Contains(tile)) color = color.Darkened(0.76f);
            var point = Project(WorldAt(tile));
            DrawRect(new Rect2(point - new Vector2(cellWidth, cellHeight) / 2,
                new Vector2(cellWidth, cellHeight)), color);
        }
        foreach (var city in Cities())
            DrawCircle(Project(WorldAt(city.Position)), 4.2f, city.Color);
        var bounds = CameraBounds();
        var a = Project(new Vector3(bounds.Position.X, 0, bounds.Position.Y));
        var b = Project(new Vector3(bounds.End.X, 0, bounds.End.Y));
        DrawRect(new Rect2(a, b - a).Abs(), new Color(0.96f, 0.81f, 0.45f), false, 1.5f);
    }

    public override void _GuiInput(InputEvent input)
    {
        var point = input switch
        {
            InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } button => button.Position,
            InputEventMouseMotion { ButtonMask: var mask } motion when (mask & MouseButtonMask.Left) != 0 => motion.Position,
            _ => (Vector2?)null,
        };
        if (point is null) return;
        var world = new Vector3(_minX + (point.Value.X - 8) / (Size.X - 16) * _spanX,
            0, _minZ + (point.Value.Y - 8) / (Size.Y - 16) * _spanZ);
        FocusCamera(world);
        AcceptEvent();
    }
}
