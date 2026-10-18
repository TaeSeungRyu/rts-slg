using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.Simulation.Climate;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public sealed partial class CampaignMapScene
{
    private ClimateController _climateController = null!;
    private IReadOnlyList<ClimateState> _climatePlayback = [];
    private Node3D? _climateVisualRoot;
    private readonly Dictionary<int, (ClimatePatch Patch, Node3D Node)> _climatePatchViews = [];
    private readonly List<(Node3D Node, float Speed)> _climateFlecks = [];
    private readonly Dictionary<HexCoord, MeshInstance3D> _climateGround = [];
    private int _climateTestNonce;

    private void AddClimateTestButtons(CanvasLayer layer)
    {
        if (!OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()).Contains("--maptest")) return;
        var row = new HBoxContainer { Position = new Vector2(10, 105) };
        row.AddThemeConstantOverride("separation", 5);
        layer.AddChild(row);
        foreach (var (label, kind) in new[] { ("☂ 비", ClimateKind.Rain), ("❄ 눈", ClimateKind.Snow) })
        {
            var button = MakeButton(label);
            button.CustomMinimumSize = new Vector2(64, 28);
            button.TooltipText = "테스트용: 지도 임의의 6×6 지역에 기후를 배치합니다.";
            button.Pressed += () => AddRandomTestClimate(kind);
            row.AddChild(button);
        }
    }

    private void AddRandomTestClimate(ClimateKind kind)
    {
        if (_advancing || _modalLayer is not null) return;
        var seed = unchecked(_state.Day * 7919 + _state.StartYear);
        var state = _state.Climate ?? new ClimateState(_state.Day, seed, 1, []);
        if (state.Patches.Count >= 2)
            state = state with { Patches = state.Patches.Skip(1).ToList() };
        var width = Math.Max(1, _map.MaxQ - _map.MinQ - 4);
        var height = Math.Max(1, _map.MaxR - _map.MinR - 4);
        var visible = _visibleTiles.OrderBy(tile => tile.Q).ThenBy(tile => tile.R).ToArray();
        for (var attempt = 0; attempt < 32; attempt++)
        {
            var index = unchecked(seed + ++_climateTestNonce * 101 + attempt * 43);
            var origin = attempt < 16 && visible.Length > 0
                ? new HexCoord(
                    Math.Clamp(visible[(int)((uint)index % (uint)visible.Length)].Q - 3,
                        _map.MinQ, Math.Max(_map.MinQ, _map.MaxQ - 5)),
                    Math.Clamp(visible[(int)((uint)index % (uint)visible.Length)].R - 3,
                        _map.MinR, Math.Max(_map.MinR, _map.MaxR - 5)))
                : new HexCoord(_map.MinQ + (int)((uint)index % (uint)width),
                    _map.MinR + (int)((uint)(index * 37) % (uint)height));
            var candidate = _climateController.AddTestPatch(state, kind, origin);
            if (candidate.Patches.Count == state.Patches.Count) continue;
            state = candidate;
            break;
        }
        _state = _state with { Climate = state };
        RefreshClimateVisuals(_state.Climate);
    }

    private void RefreshClimateVisuals(ClimateState? state)
    {
        if (_climateController is null || _view is null) return;
        _climateVisualRoot ??= new Node3D { Name = "ClimateVisuals" };
        if (_climateVisualRoot.GetParent() is null) AddChild(_climateVisualRoot);
        var patches = state?.Patches ?? [];
        var alive = patches.Select(patch => patch.Id).ToHashSet();
        foreach (var id in _climatePatchViews.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            var node = _climatePatchViews[id].Node;
            foreach (var child in node.GetChildren().OfType<Node3D>())
                _climateFlecks.RemoveAll(entry => entry.Node == child);
            node.QueueFree();
            _climatePatchViews.Remove(id);
        }
        _climateGround.Clear();
        foreach (var patch in patches)
        {
            if (!_climatePatchViews.TryGetValue(patch.Id, out var view)
                || view.Patch.Kind != patch.Kind)
            {
                var node = CreateClimatePatchVisual(patch);
                _climatePatchViews[patch.Id] = (patch, node);
                view = (patch, node);
            }
            else if (view.Patch.Origin != patch.Origin)
            {
                var destination = _view.HexToWorld(patch.Origin);
                var tween = CreateTween();
                tween.TweenProperty(view.Node, "position", destination, 0.35f);
                _climatePatchViews[patch.Id] = (patch, view.Node);
            }
            foreach (var overlay in view.Node.GetChildren().OfType<MeshInstance3D>()
                .Where(child => child.HasMeta("climate_q")))
            {
                var offset = new HexCoord(overlay.GetMeta("climate_q").AsInt32(),
                    overlay.GetMeta("climate_r").AsInt32());
                _climateGround[patch.Origin + offset] = overlay;
            }
        }
        UpdateClimateVisibility();
    }

    private Node3D CreateClimatePatchVisual(ClimatePatch patch)
    {
        var root = new Node3D { Name = $"Climate_{patch.Kind}_{patch.Id}",
            Position = _view.HexToWorld(patch.Origin) };
        _climateVisualRoot!.AddChild(root);
        var groundMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = patch.Kind == ClimateKind.Snow
                ? new Color(0.94f, 0.96f, 1f, 0.23f)
                : new Color(0.49f, 0.65f, 0.75f, 0.07f),
        };
        var groundMesh = new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.55f,
            Height = 0.006f, RadialSegments = 6 };
        var originWorld = _view.HexToWorld(patch.Origin);
        var tiles = _climateController.Tiles(patch).ToList();
        foreach (var tile in tiles)
        {
            var overlay = new MeshInstance3D
            {
                Mesh = groundMesh,
                MaterialOverride = groundMaterial,
                Position = _view.HexToWorld(tile) - originWorld
                    + new Vector3(0f, _view.TileTopY + 0.018f, 0f),
            };
            overlay.SetMeta("climate_q", tile.Q - patch.Origin.Q);
            overlay.SetMeta("climate_r", tile.R - patch.Origin.R);
            root.AddChild(overlay);
        }
        var fleckMesh = patch.Kind == ClimateKind.Snow
            ? (Mesh)new SphereMesh { Radius = 0.012f, Height = 0.024f }
            : new CylinderMesh { TopRadius = 0.003f, BottomRadius = 0.003f,
                Height = 0.12f, RadialSegments = 4 };
        var fleckMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = patch.Kind == ClimateKind.Snow
                ? new Color(1f, 1f, 1f, 0.55f)
                : new Color(0.71f, 0.83f, 0.91f, 0.42f),
        };
        for (var index = 0; index < Math.Min(24, tiles.Count); index++)
        {
            var tile = tiles[index * tiles.Count / Math.Min(24, tiles.Count)];
            var offset = _view.HexToWorld(tile) - originWorld;
            var fleck = new MeshInstance3D
            {
                Mesh = fleckMesh,
                MaterialOverride = fleckMaterial,
                Position = offset + new Vector3((index % 3 - 1) * 0.13f,
                    _view.TileTopY + 0.2f + index % 5 * 0.14f, (index % 4 - 2) * 0.09f),
            };
            fleck.SetMeta("climate_q", tile.Q - patch.Origin.Q);
            fleck.SetMeta("climate_r", tile.R - patch.Origin.R);
            root.AddChild(fleck);
            _climateFlecks.Add((fleck, patch.Kind == ClimateKind.Snow ? 0.13f : 0.42f));
        }
        return root;
    }

    private void AnimateClimateVisuals(double delta)
    {
        foreach (var (node, speed) in _climateFlecks)
        {
            if (!GodotObject.IsInstanceValid(node)) continue;
            var point = node.Position;
            point.Y -= speed * (float)delta;
            if (point.Y < _view.TileTopY + 0.08f) point.Y += 0.75f;
            node.Position = point;
        }
    }

    private void UpdateClimateVisibility()
    {
        foreach (var (_, (patch, node)) in _climatePatchViews)
            foreach (var child in node.GetChildren().OfType<MeshInstance3D>())
            {
                if (!child.HasMeta("climate_q")) continue;
                var tile = patch.Origin + new HexCoord(child.GetMeta("climate_q").AsInt32(),
                    child.GetMeta("climate_r").AsInt32());
                child.Visible = _visibleTiles.Contains(tile);
            }
    }

    private void RunClimateQa()
    {
        AddRandomTestClimate(ClimateKind.Rain);
        var rain = _state.Climate?.Patches.FirstOrDefault(patch => patch.Kind == ClimateKind.Rain);
        var rainVisual = rain is not null && _climatePatchViews.ContainsKey(rain.Id)
            && _climatePatchViews[rain.Id].Node.GetChildCount() >= 36;
        AddRandomTestClimate(ClimateKind.Snow);
        var snow = _state.Climate?.Patches.FirstOrDefault(patch => patch.Kind == ClimateKind.Snow);
        var snowVisual = snow is not null && snow.Kind == ClimateKind.Snow
            && _climatePatchViews.ContainsKey(snow.Id);
        var passed = rainVisual && snowVisual;
        GD.Print($"[maptestclimateqa] passed={passed} rain={rainVisual} snow={snowVisual}");
        GetTree().Quit(passed ? 0 : 1);
    }

    private void RunClimateAdvanceQa()
    {
        var seed = new ClimateState(_state.Day, 1234, 1, []);
        var rainy = _climateController.AddTestPatch(seed, ClimateKind.Rain,
            new HexCoord(0, 0), 5);
        var before = _state with { Climate = rainy };
        var after = _engine.AdvanceWeek(before, out _);
        var days = _engine.LastClimateDays;
        var passed = days.Count == 7 && days[0].Day == before.Day
            && days[^1].Day == before.Day + 6 && after.Day == before.Day + 7
            && after.Climate?.Day == before.Day + 6
            && days.Any(day => day.Patches.Count > 0);
        GD.Print($"[maptestclimateadvanceqa] passed={passed} days={days.Count} "
            + $"from={before.Day} to={after.Day} final_patches={after.Climate?.Patches.Count ?? -1}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
