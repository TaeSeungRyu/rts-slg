using System.IO;
using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 3D 진입점. Core로 시나리오를 로드해 3D 맵을 세우고 카메라·조명·환경·HUD를 구성한다.
/// Core는 렌더링을 모른다 — axial↔월드 변환은 MapView3D에서만.
/// </summary>
public partial class GameRoot3D : Node3D
{
    private TurnEngine _engine = null!;
    private GameState _state = null!;
    private Hud _hud = null!;
    private bool _capture;
    private int _frames;

    public override void _Ready()
    {
        var scenario = new ScenarioLoader().LoadFromDirectory(FindDataDirectory());

        AddChild(new WorldEnvironment { Environment = BuildEnvironment() });

        var light = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-52f, -55f, 0f),
            ShadowEnabled = true,
            LightEnergy = 1.15f,
        };
        AddChild(light);

        var mapView = new MapView3D();
        AddChild(mapView);
        mapView.Build(scenario.Map);

        var (center, radius) = MapBounds(mapView, scenario.Map);
        var camera = new Camera3D { Position = center + new Vector3(0f, radius * 1.5f, radius * 1.25f) };
        AddChild(camera);
        camera.LookAt(center, Vector3.Up);
        camera.Current = true;

        _engine = new TurnEngine(scenario.Balance);
        _state = GameState.FromScenario(scenario);
        _hud = new Hud();
        AddChild(_hud);
        _hud.NextMonthPressed += OnNextMonth;
        _hud.SetState(_state);

        _capture = OS.GetCmdlineArgs().Contains("--shot");
    }

    private void OnNextMonth()
    {
        _state = _engine.AdvanceMonth(_state);
        _hud.SetState(_state);
    }

    public override void _Process(double delta)
    {
        if (!_capture || ++_frames < 8)
        {
            return;
        }

        var projectDir = new DirectoryInfo(ProjectSettings.GlobalizePath("res://"));
        var outPath = Path.Combine(projectDir.Parent?.FullName ?? projectDir.FullName, "shot_step4.png");
        GetViewport().GetTexture().GetImage().SavePng(outPath);
        GetTree().Quit();
    }

    private static Godot.Environment BuildEnvironment()
    {
        return new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = new ProceduralSkyMaterial() },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            SsaoEnabled = true,
            GlowEnabled = true,
            GlowIntensity = 0.4f,
        };
    }

    private static (Vector3 Center, float Radius) MapBounds(MapView3D view, HexMap map)
    {
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (var tile in map.Tiles())
        {
            var p = view.HexToWorld(tile);
            minX = Mathf.Min(minX, p.X);
            minZ = Mathf.Min(minZ, p.Z);
            maxX = Mathf.Max(maxX, p.X);
            maxZ = Mathf.Max(maxZ, p.Z);
        }

        var center = new Vector3((minX + maxX) / 2f, 0f, (minZ + maxZ) / 2f);
        var radius = new Vector2(maxX - minX, maxZ - minZ).Length() / 2f;
        return (center, radius);
    }

    private static string FindDataDirectory()
    {
        var dir = new DirectoryInfo(ProjectSettings.GlobalizePath("res://"));
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "factions.json")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("data 디렉토리를 찾지 못했습니다.");
    }
}
