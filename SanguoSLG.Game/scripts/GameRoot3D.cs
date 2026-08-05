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
            BuildShowcaseScene(new[] { "river-straight", "river-corner", "river-corner-sharp", "river-end", "bridge" }, topDown: true);
            _capture = true;
            return;
        }

        // 임의 모델 나열 확인용: --showcase=모델1,모델2,... (건물용 비스듬한 앵글)
        foreach (var arg in OS.GetCmdlineArgs())
        {
            if (arg.StartsWith("--showcase="))
            {
                BuildShowcaseScene(arg["--showcase=".Length..].Split(','), topDown: false);
                _capture = true;
                return;
            }
        }

        var scenario = new ScenarioLoader().LoadFromDirectory(FindDataDirectory());

        var tone = TonePreset.FromCmdline();
        AddChild(new WorldEnvironment { Environment = BuildEnvironment(tone) });
        AddChild(BuildSunLight(tone));

        var mapView = new MapView3D();
        AddChild(mapView);
        var occupied = new System.Collections.Generic.HashSet<HexCoord>(
            scenario.Features.SelectMany(FeatureFootprint.TilesFor));
        mapView.Build(scenario.Map, occupied);
        mapView.BuildFeatures(scenario.Features);

        // 물 테두리 밖 배경이 비어 보이지 않도록 맵 아래에 넓은 바다 평면을 깐다.
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(600f, 600f) },
            Position = new Vector3(0f, mapView.WaterTopY - 0.03f, 0f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.45f, 0.78f, 0.80f),
                Roughness = 0.35f,
            },
        });

        var (center, radius) = MapBounds(mapView, scenario.Map);
        var camera = new CameraController3D { Fov = 55f };
        AddChild(camera);
        camera.Setup(center, radius * 1.4f);
        camera.Current = true;

        BuildCities(mapView, scenario);

        // 유닛 1기를 첫 도시 성 밖(서쪽 이웃)에 스폰(슬라이스용).
        var startCity = scenario.Cities[0];
        var unitNode = new UnitController3D();
        AddChild(unitNode);
        unitNode.Init(scenario.Map, mapView, camera,
            new Unit(new UnitId(1), startCity.Owner, startCity.Position + new HexCoord(-1, 0)));

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

    // 모델 확인/방향 실측용 쇼케이스 씬: 회전 0으로 일렬 배치.
    // topDown=true(강 보정용): 수직 카메라 — 화면 오른쪽 = +X(0°), 아래 = +Z(90°).
    private void BuildShowcaseScene(string[] models, bool topDown)
    {
        AddChild(new WorldEnvironment { Environment = BuildEnvironment(TonePreset.FromCmdline()) });
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-65f, -30f, 0f), LightEnergy = 1.3f });

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

        var centerX = (models.Length - 1) * 3f / 2f;
        var camera = new Camera3D();
        AddChild(camera);
        if (topDown)
        {
            var height = Mathf.Max(8f, models.Length * 3.2f);
            camera.Position = new Vector3(centerX, height, 2.5f);
            camera.LookAtFromPosition(camera.Position, new Vector3(centerX, 0f, 0f), new Vector3(0f, 0f, -1f));
        }
        else
        {
            var dist = Mathf.Max(1.6f, models.Length * 1.8f);
            camera.Position = new Vector3(centerX, dist * 0.8f, dist);
            camera.LookAtFromPosition(camera.Position, new Vector3(centerX, 0.25f, 0f), Vector3.Up);
        }

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

    // 도시: 성곽 등급별 성 모델(동양풍 커스텀) + 떠 있는 한글 이름표(Label3D).
    private void BuildCities(MapView3D view, Scenario scenario)
    {
        var font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf");
        var castles = new System.Collections.Generic.Dictionary<CastleSize, PackedScene>
        {
            [CastleSize.Small] = GD.Load<PackedScene>("res://assets/models/castle-small.glb"),
            [CastleSize.Medium] = GD.Load<PackedScene>("res://assets/models/castle-medium.glb"),
            [CastleSize.Large] = GD.Load<PackedScene>("res://assets/models/castle-large.glb"),
        };
        foreach (var city in scenario.Cities)
        {
            // 성은 발자국(1/3/5타일) 전체의 중심점에 놓는다. 모델은 발자국 실치수로 제작됨.
            var centroid = Vector3.Zero;
            var count = 0;
            foreach (var tile in CastleFootprint.TilesFor(city))
            {
                centroid += view.HexToWorld(tile);
                count++;
            }

            centroid /= count;

            var root = new Node3D { Position = centroid + new Vector3(0f, view.TileTopY, 0f) };
            AddChild(root);
            root.AddChild(castles[city.Castle].Instantiate<Node3D>());

            root.AddChild(new Label3D
            {
                Text = city.Name,
                Font = font,
                FontSize = 96,
                PixelSize = 0.0042f,
                OutlineSize = 26,
                OutlineModulate = new Color(0f, 0f, 0f, 0.85f),
                Modulate = new Color(0.97f, 0.96f, 0.92f),
                Position = new Vector3(0f, 0.95f, 0f),
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
