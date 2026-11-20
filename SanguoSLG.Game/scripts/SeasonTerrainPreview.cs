using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>게임 규칙과 저장 데이터에 연결하지 않는 20×20 계절 색상 미리보기.</summary>
public partial class SeasonTerrainPreview : Node3D
{
    private MapView3D? _view;
    private HexMap _map = null!;
    private Camera3D _camera = null!;
    private OptionButton _season = null!;

    public override void _Ready()
    {
        var types = Enum.GetValues<TerrainType>();
        var terrain = new Dictionary<HexCoord, TerrainType>();
        for (var q = 0; q < 20; q++)
        for (var r = 0; r < 20; r++)
            terrain[new HexCoord(q, r)] = types[((q / 3) + (r / 3) * 7) % types.Length];
        _map = new HexMap(0, 19, 0, 19, terrain);

        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color("#29323a"),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("#d8d5c9"),
            AmbientLightEnergy = 0.65f,
        };
        AddChild(new WorldEnvironment { Environment = environment });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -30, 0), LightEnergy = 1.2f });
        _camera = new Camera3D { Current = true, Projection = Camera3D.ProjectionType.Orthogonal, Size = 32f,
            RotationDegrees = new Vector3(-65, 0, 0) };
        AddChild(_camera);

        var ui = new CanvasLayer();
        AddChild(ui);
        var panel = new PanelContainer { Position = new Vector2(16, 16), CustomMinimumSize = new Vector2(300, 0) };
        ui.AddChild(panel);
        var content = new VBoxContainer();
        panel.AddChild(content);
        content.AddChild(new Label { Text = "지형 계절 미리보기 · 20×20" });
        _season = new OptionButton();
        _season.AddItem("기본", 0);
        _season.AddItem("가을", 1);
        _season.AddItem("겨울", 2);
        _season.ItemSelected += index => Rebuild((MapView3D.PreviewSeason)index);
        content.AddChild(_season);
        content.AddChild(new Label { Text = $"지형 {types.Length}종 모두 포함 · 색상만 변경" });
        Rebuild(MapView3D.PreviewSeason.Default);

        if (OS.GetCmdlineUserArgs().Contains("--season-preview-qa"))
            CallDeferred(nameof(RunQa));
    }

    private void Rebuild(MapView3D.PreviewSeason season)
    {
        if (_view is not null)
        {
            RemoveChild(_view);
            _view.QueueFree();
        }
        _view = new MapView3D { SeasonPreview = season };
        AddChild(_view);
        _view.Build(_map, new HashSet<HexCoord>(), new TileConditionMap());
        var center = _view.HexToWorld(new HexCoord(9, 9));
        _camera.Position = center + new Vector3(0, 25, 16);
        _camera.LookAt(center);
    }

    private void RunQa()
    {
        var forestTile = _map.Tiles().First(tile => _map.TerrainAt(tile) == TerrainType.Forest);
        var mountain = _map.Tiles().First(tile => _map.TerrainAt(tile) == TerrainType.Mountain);
        var rockMountain = _map.Tiles().First(tile => _map.TerrainAt(tile) == TerrainType.RockMountain);
        var mountainColors = new List<Color>();
        var rockColors = new List<Color>();
        foreach (var season in Enum.GetValues<MapView3D.PreviewSeason>())
        {
            _season.Select((int)season);
            Rebuild(season);
            if (_view is null || _map.Count != 400 || _map.Tiles().Select(_map.TerrainAt).Distinct().Count() != Enum.GetValues<TerrainType>().Length)
            {
                GD.PushError("SEASON_PREVIEW_QA FAIL");
                GetTree().Quit(1);
                return;
            }
            mountainColors.Add(TileColor(mountain));
            rockColors.Add(TileColor(rockMountain));
            var forestMaterial = TileMaterial(forestTile);
            if (season != MapView3D.PreviewSeason.Default && forestMaterial is not ShaderMaterial)
                throw new InvalidOperationException($"{season} 숲 재질이 공통 계절 셰이더를 사용하지 않음");
        }
        if (mountainColors.Distinct().Count() != 3 || rockColors.Distinct().Count() != 3)
        {
            GD.PushError($"SEASON_PREVIEW_QA FAIL: mountain colors {string.Join(", ", mountainColors)}, rock {string.Join(", ", rockColors)}");
            GetTree().Quit(1);
            return;
        }
        GD.Print("SEASON_PREVIEW_QA PASS: 400 tiles, all terrain types, 3 palettes");
        GetTree().Quit();
    }

    private Color TileColor(HexCoord tile)
        => ((StandardMaterial3D)TileMaterial(tile)).AlbedoColor;

    private Material TileMaterial(HexCoord tile)
    {
        var position = _view!.HexToWorld(tile);
        var instance = _view.GetChildren().OfType<Node3D>()
            .First(node => node.Position.IsEqualApprox(position));
        var mesh = instance.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().First();
        return mesh.GetActiveMaterial(0);
    }
}
