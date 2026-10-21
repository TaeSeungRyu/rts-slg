using System;
using System.Linq;
using Godot;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private PanelContainer? _miniMapPanel;
    private CampaignMiniMap? _miniMapView;

    private void AddMiniMapButton(HBoxContainer top)
    {
        var button = MakeButton("");
        button.Icon = GD.Load<Texture2D>("res://assets/icons/icon_map.svg");
        button.ExpandIcon = true;
        button.CustomMinimumSize = new Vector2(42, 42);
        button.TooltipText = "지도 보기";
        button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        top.AddChild(button);

        var layer = new CanvasLayer { Layer = 16 };
        AddChild(layer);
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop, Visible = false };
        panel.AnchorLeft = 1;
        panel.AnchorRight = 1;
        panel.AnchorTop = 1;
        panel.AnchorBottom = 1;
        panel.OffsetLeft = -324;
        panel.OffsetRight = -16;
        panel.OffsetTop = -248;
        panel.OffsetBottom = -18;
        panel.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 8, 8));
        root.AddChild(panel);
        _miniMapPanel = panel;
        var layout = new VBoxContainer();
        panel.AddChild(layout);
        var heading = new HBoxContainer();
        layout.AddChild(heading);
        var title = MakeLabel("전략 지도", 16, GoldBright);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        heading.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(30, 28);
        close.Pressed += () => panel.Visible = false;
        heading.AddChild(close);
        var map = new CampaignMiniMap
        {
            Map = _map,
            WorldAt = tile => _view.HexToWorld(tile),
            VisibleTiles = () => _visibleTiles,
            Cities = () => _state.Cities.Where(IsKnownCityLocation).Select(city =>
            {
                var faction = _state.Factions.FirstOrDefault(f => f.Id == city.Owner);
                var color = city.Owner == Player
                    ? faction is null ? GoldBright : Color.FromString(faction.Color, GoldBright)
                    : ArmyFactionColor(city.Owner);
                return (city.Position, color);
            }),
            CameraBounds = MiniMapCameraBounds,
            FocusCamera = point => { if (_modalLayer is null && !_advancing) _camera.FocusOn(point); },
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _miniMapView = map;
        layout.AddChild(map);
        button.Pressed += () =>
        {
            if (_modalLayer is not null) return;
            panel.Visible = !panel.Visible;
            map.QueueRedraw();
        };
    }

    private Rect2 MiniMapCameraBounds()
    {
        var size = GetViewport().GetVisibleRect().Size;
        var corners = new[] { Vector2.Zero, new Vector2(size.X, 0), size,
            new Vector2(0, size.Y) };
        var points = corners.Select(screen =>
        {
            var origin = _camera.ProjectRayOrigin(screen);
            var ray = _camera.ProjectRayNormal(screen);
            var distance = Math.Abs(ray.Y) < 0.0001f ? 0f : (_view.TileTopY - origin.Y) / ray.Y;
            return origin + ray * Math.Max(0, distance);
        }).ToArray();
        var minX = points.Min(point => point.X);
        var maxX = points.Max(point => point.X);
        var minZ = points.Min(point => point.Z);
        var maxZ = points.Max(point => point.Z);
        return new Rect2(minX, minZ, maxX - minX, maxZ - minZ);
    }

    private void RunMiniMapUiQa()
    {
        var map = _miniMapView;
        var bounds = MiniMapCameraBounds();
        var passed = _miniMapPanel is not null && map is not null && map.Map.Count > 0
            && bounds.Size.X > 0 && bounds.Size.Y > 0;
        var enemy = _state.Cities.FirstOrDefault(city => city.Owner != Player && IsKnownCityLocation(city));
        if (enemy is not null && !_state.AreAllied(Player, enemy.Owner))
            passed &= map?.Cities().FirstOrDefault(marker => marker.Position == enemy.Position).Color
                == ArmyFactionColor(enemy.Owner);
        GD.Print($"[maptestminimapqa] tiles={map?.Map.Count ?? 0} passed={passed}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
