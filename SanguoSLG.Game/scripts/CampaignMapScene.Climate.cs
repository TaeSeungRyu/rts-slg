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
    private readonly List<(MeshInstance3D Node, Vector3 Origin, float Phase)> _climateClouds = [];
    private readonly Dictionary<HexCoord, MeshInstance3D> _climateGround = [];
    private int _climateTestNonce;
    private ClimateState? _displayClimate;

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
        _displayClimate = state;
        _climateVisualRoot ??= new Node3D { Name = "ClimateVisuals" };
        if (_climateVisualRoot.GetParent() is null) AddChild(_climateVisualRoot);
        var patches = state?.Patches ?? [];
        var alive = patches.Select(patch => patch.Id).ToHashSet();
        foreach (var id in _climatePatchViews.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            var node = _climatePatchViews[id].Node;
            FadeClimatePatch(node, false);
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
                FadeClimatePatch(view.Node, false);
                var node = CreateClimatePatchVisual(patch);
                _climatePatchViews[patch.Id] = (patch, node);
                view = (patch, node);
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

    private void FadeClimatePatch(Node3D node, bool appearing)
    {
        var meshes = node.GetChildren().OfType<MeshInstance3D>().ToArray();
        foreach (var mesh in meshes)
        {
            if (appearing) mesh.Transparency = 1f;
            var tween = CreateTween();
            tween.TweenProperty(mesh, "transparency", appearing ? 0f : 1f,
                appearing ? 0.65f : 0.45f);
        }
        if (appearing) return;
        _climateFlecks.RemoveAll(entry => entry.Node.GetParent() == node);
        _climateClouds.RemoveAll(entry => entry.Node.GetParent() == node);
        var cleanup = CreateTween();
        cleanup.TweenInterval(0.47f);
        cleanup.Finished += () => { if (GodotObject.IsInstanceValid(node)) node.QueueFree(); };
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
                ? new Color(0.94f, 0.96f, 1f, 0.34f)
                : new Color(0.59f, 0.72f, 0.8f, 0.15f),
        };
        var groundMesh = new CylinderMesh { TopRadius = 0.55f, BottomRadius = 0.55f,
            Height = 0.006f, RadialSegments = 6 };
        var snowDriftMeshes = Enumerable.Range(0, 4).Select(BuildSnowDriftMesh).ToArray();
        var snowDriftMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.98f, 0.99f, 1f, 0.86f),
        };
        var originWorld = _view.HexToWorld(patch.Origin);
        var tiles = _climateController.Tiles(patch).ToList();
        foreach (var tile in tiles)
        {
            var terrain = _map.TerrainAt(tile);
            if (terrain is TerrainType.WaterDeep or TerrainType.WaterShallow
                or TerrainType.WaterRocks or TerrainType.River) continue;
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
            if (patch.Kind != ClimateKind.Snow) continue;
            for (var drift = 0; drift < 4; drift++)
            {
                var x = new[] { -0.24f, 0.18f, -0.08f, 0.27f }[drift];
                var z = new[] { 0.13f, 0.17f, -0.23f, -0.16f }[drift];
                var snowDrift = new MeshInstance3D
                {
                    Mesh = snowDriftMeshes[drift],
                    MaterialOverride = snowDriftMaterial,
                    Position = overlay.Position + new Vector3(x, 0.03f, z),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                snowDrift.SetMeta("climate_q", tile.Q - patch.Origin.Q);
                snowDrift.SetMeta("climate_r", tile.R - patch.Origin.R);
                root.AddChild(snowDrift);
            }
        }
        if (patch.Kind == ClimateKind.Rain) AddRainClouds(root, patch, tiles, originWorld);
        var fleckMesh = patch.Kind == ClimateKind.Snow
            ? (Mesh)new SphereMesh { Radius = 0.012f, Height = 0.024f }
            : new CylinderMesh { TopRadius = 0.006f, BottomRadius = 0.006f,
                Height = 0.17f, RadialSegments = 5 };
        var fleckMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = patch.Kind == ClimateKind.Snow
                ? new Color(1f, 1f, 1f, 0.55f)
                : new Color(0.8f, 0.89f, 0.96f, 0.74f),
        };
        var fleckCount = Math.Min(patch.Kind == ClimateKind.Rain ? 72 : 24,
            tiles.Count * (patch.Kind == ClimateKind.Rain ? 2 : 1));
        for (var index = 0; index < fleckCount; index++)
        {
            var tile = tiles[index % tiles.Count];
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
            _climateFlecks.Add((fleck, patch.Kind == ClimateKind.Snow ? 0.13f : 0.65f));
        }
        FadeClimatePatch(root, true);
        return root;
    }

    private static ArrayMesh BuildSnowDriftMesh(int variant)
    {
        var mesh = new ArrayMesh();
        var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var points = new Vector3[8];
        for (var index = 0; index < points.Length; index++)
        {
            var angle = Mathf.Tau * index / points.Length;
            var uneven = 0.7f + ((index * 7 + variant * 3) % 5) * 0.11f;
            points[index] = new Vector3(Mathf.Cos(angle) * (0.17f + variant * 0.014f) * uneven,
                0f, Mathf.Sin(angle) * (0.11f + (variant % 2) * 0.02f) * uneven);
        }
        for (var index = 0; index < points.Length; index++)
        {
            surface.SetNormal(Vector3.Up);
            surface.AddVertex(Vector3.Zero);
            surface.SetNormal(Vector3.Up);
            surface.AddVertex(points[(index + 1) % points.Length]);
            surface.SetNormal(Vector3.Up);
            surface.AddVertex(points[index]);
        }
        surface.Commit(mesh);
        return mesh;
    }

    private void AddRainClouds(Node3D root, ClimatePatch patch,
        IReadOnlyList<HexCoord> tiles, Vector3 originWorld)
    {
        if (tiles.Count == 0) return;
        var cloudScene = GD.Load<PackedScene>("res://assets/models/weather-rain-cloud.glb");
        var cloudModel = cloudScene.Instantiate<Node3D>();
        var cloudMesh = cloudModel.FindChildren("*", "MeshInstance3D", true, false)
            .OfType<MeshInstance3D>().First().Mesh;
        cloudModel.Free();
        var cloudMaterial = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(0.72f, 0.79f, 0.84f, 0.63f),
        };
        for (var cloud = 0; cloud < 3; cloud++)
        {
            var tile = tiles[Math.Min(tiles.Count - 1, (cloud * 2 + 1) * tiles.Count / 6)];
            var center = _view.HexToWorld(tile) - originWorld
                + new Vector3(0f, _view.TileTopY + 1.1f + cloud * 0.06f, 0f);
            var puff = new MeshInstance3D
            {
                Mesh = cloudMesh,
                MaterialOverride = cloudMaterial,
                Scale = new Vector3(1.1f + cloud * 0.06f, 1f, 1.1f),
                RotationDegrees = new Vector3(0f, cloud * 37f, 0f),
                Position = center,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            puff.SetMeta("climate_q", tile.Q - patch.Origin.Q);
            puff.SetMeta("climate_r", tile.R - patch.Origin.R);
            root.AddChild(puff);
            _climateClouds.Add((puff, puff.Position, cloud * 1.7f));
        }
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
        var time = (float)Time.GetTicksMsec() / 1000f;
        foreach (var (node, origin, phase) in _climateClouds)
            if (GodotObject.IsInstanceValid(node))
                node.Position = origin + new Vector3(Mathf.Sin(time * 0.38f + phase) * 0.06f,
                    0f, 0f);
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
            && _climatePatchViews[rain.Id].Node.GetChildCount() >= 72;
        AddRandomTestClimate(ClimateKind.Snow);
        var snow = _state.Climate?.Patches.FirstOrDefault(patch => patch.Kind == ClimateKind.Snow);
        var snowVisual = snow is not null && snow.Kind == ClimateKind.Snow
            && _climatePatchViews.ContainsKey(snow.Id)
            && _climatePatchViews[snow.Id].Node.GetChildren().OfType<MeshInstance3D>()
                .Count(child => child.Mesh is ArrayMesh) >= 4;
        var clouds = rain is not null && _climatePatchViews[rain.Id].Node
            .GetChildren().OfType<MeshInstance3D>()
            .Count(child => child.Mesh == _climateClouds.First().Node.Mesh) == 3;
        var terrainInfo = rain is not null && snow is not null
            && ClimateCombatRules.At(_displayClimate, rain.Origin) == ClimateKind.Rain
            && ClimateCombatRules.At(_displayClimate, snow.Origin) == ClimateKind.Snow;
        var moved = false;
        if (rain is not null)
        {
            var beforeNode = _climatePatchViews[rain.Id].Node;
            var shifted = rain with { Origin = rain.Origin + new HexCoord(1, 0) };
            var next = _state.Climate! with
            {
                Patches = _state.Climate.Patches.Select(patch => patch.Id == rain.Id ? shifted : patch)
                    .ToList(),
            };
            RefreshClimateVisuals(next);
            moved = _climatePatchViews[rain.Id].Node != beforeNode
                && _climatePatchViews[rain.Id].Node.Position == _view.HexToWorld(shifted.Origin);
        }
        var passed = rainVisual && snowVisual && clouds && terrainInfo && moved;
        GD.Print($"[maptestclimateqa] passed={passed} rain={rainVisual} snow={snowVisual} "
            + $"clouds={clouds} terrain={terrainInfo} moved={moved}");
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
