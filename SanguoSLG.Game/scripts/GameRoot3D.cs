using System.IO;
using System.Linq;
using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 3D 진입점. Core로 시나리오를 로드해 3D 맵·도시·유닛을 세우고 카메라·조명·환경·HUD를 구성한다.
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
        if (OS.GetCmdlineArgs().Contains("--riverdebug"))
        {
            BuildRiverDebugScene();
            _capture = true;
            return;
        }

        var scenario = new ScenarioLoader().LoadFromDirectory(FindDataDirectory());

        var tone = TonePreset.FromCmdline();
        AddChild(new WorldEnvironment { Environment = BuildEnvironment(tone) });
        AddChild(BuildSunLight(tone));

        var mapView = new MapView3D();
        AddChild(mapView);
        mapView.Build(scenario.Map);

        var (center, radius) = MapBounds(mapView, scenario.Map);
        var camera = new CameraController3D { Fov = 55f };
        AddChild(camera);
        camera.Setup(center, radius * 1.4f);
        camera.Current = true;

        BuildCities(mapView, scenario);

        // 유닛 1기를 첫 도시에 스폰(슬라이스용).
        var startCity = scenario.Cities[0];
        var unitNode = new UnitController3D();
        AddChild(unitNode);
        unitNode.Init(scenario.Map, mapView, camera,
            new Unit(new UnitId(1), startCity.Owner, startCity.Position));

        AddChild(BuildVignette(tone));

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

    // 강 모델 방향 실측용 임시 디버그 씬: 회전 0으로 일렬 배치, 수직 탑다운 카메라.
    // 화면 기준 오른쪽 = +X(0°), 아래 = +Z(90°).
    private void BuildRiverDebugScene()
    {
        AddChild(new WorldEnvironment { Environment = BuildEnvironment(TonePreset.FromCmdline()) });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-80f, 0f, 0f), LightEnergy = 1.3f });

        string[] models = { "river-straight", "river-corner", "river-corner-sharp", "river-end", "bridge" };
        var font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf");
        for (var i = 0; i < models.Length; i++)
        {
            var instance = GD.Load<PackedScene>($"res://assets/models/{models[i]}.glb").Instantiate<Node3D>();
            instance.Position = new Vector3(i * 3f, 0f, 0f);
            AddChild(instance);

            AddChild(new Label3D
            {
                Text = models[i],
                Font = font,
                FontSize = 48,
                PixelSize = 0.01f,
                Position = new Vector3(i * 3f, 0.2f, 1.6f),
                RotationDegrees = new Vector3(-90f, 0f, 0f),
            });
        }

        var camera = new Camera3D { Position = new Vector3(6f, 16f, 0f) };
        AddChild(camera);
        camera.LookAtFromPosition(camera.Position, new Vector3(6f, 0f, 0f), new Vector3(0f, 0f, -1f));
        camera.Current = true;
    }

    // 화면 가장자리를 어둡게 하는 비네트 오버레이(HUD 아래).
    private static CanvasLayer BuildVignette(TonePreset tone)
    {
        var layer = new CanvasLayer();
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/vignette.gdshader") };
        material.SetShaderParameter("strength", tone.Vignette);
        layer.AddChild(new ColorRect
        {
            Material = material,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Color = new Color(0f, 0f, 0f, 0f),
        });
        return layer;
    }

    private static Godot.Environment BuildEnvironment(TonePreset tone)
    {
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = tone.SkyTop,
            SkyHorizonColor = tone.SkyHorizon,
            GroundHorizonColor = tone.SkyHorizon,
            GroundBottomColor = tone.GroundBottom,
        };

        return new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightSkyContribution = tone.Ambient,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapExposure = tone.Exposure,
            SsaoEnabled = true,
            SsaoIntensity = 1.2f,
            GlowEnabled = true,
            GlowIntensity = 0.15f,
            FogEnabled = tone.FogDensity > 0f,
            FogLightColor = tone.SkyHorizon,
            FogDensity = tone.FogDensity,
            FogSkyAffect = 0f,
            AdjustmentEnabled = true,
            AdjustmentBrightness = tone.Brightness,
            AdjustmentContrast = tone.Contrast,
            AdjustmentSaturation = tone.Saturation,
        };
    }

    private static DirectionalLight3D BuildSunLight(TonePreset tone) => new()
    {
        RotationDegrees = new Vector3(-48f, -42f, 0f),
        LightColor = tone.SunColor,
        LightEnergy = tone.SunEnergy,
        ShadowEnabled = true,
        DirectionalShadowMaxDistance = 60f,
    };

    // 도시: 금색 대좌(3D) + 떠 있는 한글 이름표(Label3D).
    private void BuildCities(MapView3D view, Scenario scenario)
    {
        var font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf");
        foreach (var city in scenario.Cities)
        {
            var root = new Node3D { Position = view.HexToWorld(city.Position) + new Vector3(0f, view.TileTopY, 0f) };
            AddChild(root);

            root.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.34f, BottomRadius = 0.42f, Height = 0.16f },
                Position = new Vector3(0f, 0.08f, 0f),
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.87f, 0.69f, 0.30f),
                    Metallic = 0.7f,
                    Roughness = 0.35f,
                },
            });

            root.AddChild(new Label3D
            {
                Text = city.Name,
                Font = font,
                FontSize = 96,
                PixelSize = 0.0042f,
                OutlineSize = 26,
                OutlineModulate = new Color(0f, 0f, 0f, 0.85f),
                Modulate = new Color(0.97f, 0.96f, 0.92f),
                Position = new Vector3(0f, 0.62f, 0f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
            });
        }
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
