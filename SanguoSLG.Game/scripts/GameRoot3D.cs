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
    private WorldEngine _engine = null!;
    private GameState _state = null!;
    private Hud _hud = null!;
    private bool _capture;
    private int _frames;
    private Godot.Environment _environment = null!;
    private DirectionalLight3D _sun = null!;

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

        // 이동 시뮬레이션 GUI 검증(doc/test/movement-cases.md): --movetest.
        // `--` 뒤로 넘어온 유저 인자(GetCmdlineUserArgs)도 함께 본다
        if (OS.GetCmdlineArgs().Contains("--movetest") || OS.GetCmdlineUserArgs().Contains("--movetest"))
        {
            BuildMovementTest();
            return;
        }

        // 효과 검수(doc/design-effect.md 1단계): --effecttest. 테스트 유닛에 효과를 지속표시
        if (OS.GetCmdlineArgs().Contains("--effecttest") || OS.GetCmdlineUserArgs().Contains("--effecttest"))
        {
            BuildEffectTest();
            return;
        }

        // 전투 검증(Core AdvanceOrchestrator): --combattest. 진행마다 한 라운드 교전을 재생한다
        if (OS.GetCmdlineArgs().Contains("--combattest") || OS.GetCmdlineUserArgs().Contains("--combattest"))
        {
            BuildCombatTest();
            return;
        }

        // 내정 전용 게임 씬 1단계(12b): --admin. 도시 현황 + 진행 버튼(Core AdminSession).
        if (OS.GetCmdlineArgs().Contains("--admin") || OS.GetCmdlineUserArgs().Contains("--admin"))
        {
            var admin = new AdminScene();
            AddChild(admin);
            admin.Build(FindDataDirectory());
            return;
        }

        var scenario = new ScenarioLoader().LoadFromDirectory(FindDataDirectory());

        var tone = TonePreset.FromCmdline();
        _environment = BuildEnvironment(tone);
        AddChild(new WorldEnvironment { Environment = _environment });
        _sun = BuildSunLight(tone);
        AddChild(_sun);

        var mapView = new MapView3D();
        AddChild(mapView);
        var occupied = new System.Collections.Generic.HashSet<HexCoord>(
            scenario.Features.SelectMany(FeatureFootprint.TilesFor));
        mapView.Build(scenario.Map, occupied, scenario.Conditions);
        mapView.BuildFeatures(scenario.Features, scenario.Conditions);

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
        BuildTroopReview(mapView, scenario.Map);

        // 유닛 1기를 첫 도시 성 밖(서쪽 이웃)에 스폰(슬라이스용).
        var startCity = scenario.Cities[0];
        var unitNode = new UnitController3D();
        AddChild(unitNode);
        var ownerColor = new Color(
            scenario.Factions.First(f => f.Id == startCity.Owner).Color);
        unitNode.Init(scenario.Map, mapView, camera,
            new Unit(new UnitId(1), startCity.Owner, startCity.Position + new HexCoord(-1, 0)),
            ownerColor,
            new PassabilityMap(scenario.Map, scenario.Features, scenario.Cities));

        MapView3D.TuneImportedMeshes(this);

        AddChild(BuildVignette(tone));

        _engine = new WorldEngine(scenario.Balance, adminSkills: new AdminSkillLoader().LoadFromDirectory(FindDataDirectory()));
        _state = GameState.FromScenario(scenario);
        _hud = new Hud();
        AddChild(_hud);
        _hud.NextMonthPressed += OnNextMonth;
        _hud.SetState(_state);

        _capture = OS.GetCmdlineArgs().Contains("--shot");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        switch (key.Keycode)
        {
            case Key.F2:
                _environment.SsaoEnabled = !_environment.SsaoEnabled;
                break;
            case Key.F3:
                _sun.ShadowEnabled = !_sun.ShadowEnabled;
                break;
            case Key.F4:
                _environment.GlowEnabled = !_environment.GlowEnabled;
                break;
        }
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

    // 이동 시뮬레이션 GUI 검증 씬(doc/test/movement-cases.md). 평지 맵 위에서
    // MovementTestScene3D가 부대를 세우고 "진행" 버튼으로 스텝을 재생한다.
    private void BuildMovementTest()
    {
        var tone = TonePreset.FromCmdline();
        _environment = BuildEnvironment(tone);
        AddChild(new WorldEnvironment { Environment = _environment });
        _sun = BuildSunLight(tone);
        AddChild(_sun);

        var mapView = new MapView3D();
        AddChild(mapView);
        var testMap = new HexMap(0, 12, 0, 4);
        mapView.Build(testMap, new System.Collections.Generic.HashSet<HexCoord>(), new TileConditionMap());

        var camera = new CameraController3D { Fov = 55f };
        AddChild(camera);
        camera.Current = true;

        MapView3D.TuneImportedMeshes(this);

        var test = new MovementTestScene3D();
        AddChild(test);
        test.Build(mapView, camera);
    }

    // 전투 검증 씬(Core AdvanceOrchestrator). 인접 부대가 진행마다 한 라운드씩 교전하며 병력이
    // 깎여나가는 것을 병력 수치·공격 모션으로 보여준다.
    private void BuildCombatTest()
    {
        var tone = TonePreset.FromCmdline();
        _environment = BuildEnvironment(tone);
        AddChild(new WorldEnvironment { Environment = _environment });
        _sun = BuildSunLight(tone);
        AddChild(_sun);

        var mapView = new MapView3D();
        AddChild(mapView);
        var testMap = new HexMap(0, 16, 0, 8); // 대량 전투 케이스까지 담는 판
        mapView.Build(testMap, new System.Collections.Generic.HashSet<HexCoord>(), new TileConditionMap());

        var camera = new CameraController3D { Fov = 55f };
        AddChild(camera);
        camera.Current = true;

        MapView3D.TuneImportedMeshes(this);

        var scene = new CombatTestScene3D();
        AddChild(scene);
        scene.Build(mapView, camera, FindDataDirectory());
    }

    // 효과 검수 씬(doc/design-effect.md 1단계). 평지 위 테스트 유닛마다 효과 하나를
    // 지속표시하고 이름표를 띄운다. 구현된 효과만 늘려 나간다.
    private void BuildEffectTest()
    {
        var tone = TonePreset.FromCmdline();
        _environment = BuildEnvironment(tone);
        AddChild(new WorldEnvironment { Environment = _environment });
        _sun = BuildSunLight(tone);
        AddChild(_sun);

        var mapView = new MapView3D();
        AddChild(mapView);
        var map = new HexMap(0, 24, 0, 66);
        mapView.Build(map, new System.Collections.Generic.HashSet<HexCoord>(), new TileConditionMap());

        var camera = new CameraController3D { Fov = 55f };
        AddChild(camera);
        camera.Current = true;

        var font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf");
        var swords = GD.Load<PackedScene>("res://assets/models/troop-swordsman.glb");

        // 구현된 효과마다 밴드 하나: 편대 규모별(1·3·5·7·9) + 성 3종에 지속표시.
        // 효과가 늘면 (효과, 이름) 한 줄씩 추가한다.
        // 적용 대상 범위는 EffectView.ScopeOf가 단일 출처(doc "적용 대상 제약").
        var bands = new (EffectKind Kind, string Tag)[]
        {
            (EffectKind.Fire, "Fire"), (EffectKind.Haze, "Haze"),
            (EffectKind.Flies, "Flies"), (EffectKind.Flood, "Flood"),
            (EffectKind.Skulls, "Skulls"), (EffectKind.Daze, "Daze"),
            (EffectKind.Bubbles, "Bubbles"), (EffectKind.Burst, "Burst"),
            (EffectKind.Tear, "Tear"), (EffectKind.Shatter, "Shatter"),
            (EffectKind.Confusion, "Confusion"),
        };
        for (var b = 0; b < bands.Length; b++)
        {
            var (kind, tag) = bands[b];
            BuildEffectBand(mapView, font, swords, kind, tag, EffectView.ScopeOf(kind), unitRow: b * 6 + 1, castleRow: b * 6 + 4);
        }

        MapView3D.TuneImportedMeshes(this);
        camera.Setup(mapView.HexToWorld(new HexCoord(10, bands.Length * 3)), bands.Length * 9f + 4f);
    }

    // 한 효과를 편대 규모별(1·3·5·7·9)과 성 3종에 지속표시하는 밴드 하나.
    private void BuildEffectBand(MapView3D view, Font font, PackedScene swords,
        EffectKind kind, string tag, EffectTargetScope scope, int unitRow, int castleRow)
    {
        var color = new Color(0.30f, 0.45f, 0.70f);
        var sizes = new[] { 1, 3, 5, 7, 9 };
        if (scope != EffectTargetScope.Building) // 건물 전용이면 유닛에는 표시하지 않는다
        {
            for (var i = 0; i < sizes.Length; i++)
            {
                var root = new Node3D { Position = view.HexToWorld(new HexCoord(2 + i * 4, unitRow)) + new Vector3(0f, view.TileTopY, 0f) };
                AddChild(root);
                TroopFormation.Build(root, swords, sizes[i]);
                FactionColorView.Apply(root, color);
                EffectView.Attach(root, kind, 0.5f + sizes[i] * 0.08f);
                root.AddChild(EffectLabel(font, $"{sizes[i]}기 {tag}", 0.5f));
            }
        }

        if (scope == EffectTargetScope.Unit) // 유닛 전용이면 성에는 표시하지 않는다
        {
            return;
        }

        var castles = new (CastleSize Size, string Name, string File, int Q)[]
        {
            (CastleSize.Small, "소성", "castle-small", 4),
            (CastleSize.Medium, "중성", "castle-medium", 10),
            (CastleSize.Large, "대성", "castle-large", 17),
        };
        foreach (var (size, name, file, q) in castles)
        {
            var anchor = new HexCoord(q, castleRow);
            var offsets = CastleFootprint.OffsetsFor(size);
            var centroid = Vector3.Zero;
            foreach (var off in offsets)
            {
                centroid += view.HexToWorld(anchor + off);
            }

            centroid /= offsets.Count;

            var root = new Node3D { Position = centroid + new Vector3(0f, view.TileTopY, 0f) };
            AddChild(root);
            root.AddChild(GD.Load<PackedScene>($"res://assets/models/{file}.glb").Instantiate<Node3D>());

            foreach (var off in offsets)
            {
                var spot = new Node3D { Position = view.HexToWorld(anchor + off) - centroid };
                root.AddChild(spot);
                EffectView.Attach(spot, kind, 1.0f);
            }

            root.AddChild(EffectLabel(font, $"{name} {tag}", 1.0f));
        }
    }

    private static Label3D EffectLabel(Font font, string text, float y) => new()
    {
        Text = text,
        Font = font,
        FontSize = 96,
        PixelSize = 0.0026f,
        OutlineSize = 26,
        OutlineModulate = new Color(0f, 0f, 0f, 0.85f),
        Modulate = new Color(0.97f, 0.96f, 0.92f),
        Position = new Vector3(0f, y, 0f),
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        NoDepthTest = true,
    };

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
            SsaoIntensity = 1.0f,
            SsaoRadius = 0.18f,
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

        // 그림자 설정은 월드 크기에 맞춰야 한다. Godot 기본값(bias 0.1 / normal_bias 2.0,
        // 최대 거리 60)은 사람 크기(미터) 월드 기준이라, 타일 반경이 0.577인 이 게임에서는
        // 편향이 지형보다 커진다. 그 상태에서 카메라가 움직이면 그림자 캐스케이드가
        // 매 프레임 다시 맞춰지며 샘플링이 흔들려 깜빡이고, 멈추면 가라앉는다.
        DirectionalShadowMaxDistance = 28f,   // 맵이 약 20x14라 이걸로 충분 — 분할당 해상도가 올라간다
        ShadowBias = 0.03f,
        ShadowNormalBias = 0.05f,
        DirectionalShadowBlendSplits = true,  // 분할 경계가 튀지 않게 섞는다
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
            var castleModel = castles[city.Castle].Instantiate<Node3D>();
            root.AddChild(castleModel);

            // 마을·항구와 똑같은 공통 파괴 레이어를 성에도 그대로 적용한다
            // (성 전용 코드 없음 — 명명 규칙이 같아서 성벽 여장·지붕·기둥이 함께 반응한다).
            var condition = scenario.Conditions.At(city.Position);
            AddCastleAmbience(root, city, condition);
            DamageView.Apply(castleModel, condition, DamageView.Kind.Castle,
                unchecked((ulong)(city.Position.Q * 40503L + city.Position.R * 26041L + 8171L)));

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

    // 병종 외형 검수용 배치 — doc/spec-unit.md 병종 카탈로그 순서대로 빈 행(r=4)에 세운다.
    // 모델을 만들 때마다 이 표에 한 줄씩 늘린다. 발동 규칙·편대와 무관한 임시 진열이다.
    private static readonly (string File, string Label)[] TroopReview =
    {
        ("troop-swordsman.glb", "1 도검병"),
        ("troop-cavalry.glb", "2 기병"),
        ("troop-archer.glb", "3 궁병"),
        ("troop-thunder-cart.glb", "4 벽력거"),
        ("troop-catapult.glb", "5 투석기"),
        ("troop-siege-tower.glb", "6 공성탑"),
        ("troop-war-elephant.glb", "7 상병"),
        ("troop-small-boat.glb", "8 소선"),
        ("troop-medium-ship.glb", "9 중선"),
        ("troop-large-ship.glb", "10 대선"),
        ("troop-pikeman.glb", "11 극병"),
        ("troop-nanman.glb", "12 남만병"),
        ("troop-shieldbearer.glb", "13 등갑병"),
        ("troop-wudang.glb", "14 무당비군"),
        ("troop-cataphract.glb", "15 철기병"),
        ("troop-horse-archer.glb", "16 궁기병"),
        ("troop-hwarang-archer.glb", "17 화랑궁병"),
        ("troop-turtleship.glb", "19 거북선"),
        ("troop-waeseon.glb", "20 왜선"),
        ("troop-bandit.glb", "21 도적"),
        ("troop-great-tiger.glb", "22 대호"),
        ("troop-wild-elephant.glb", "23 코끼리"),
        ("troop-eastern-dragon.glb", "24 동양풍 용"),
        ("troop-giant-squid.glb", "27 대왕오징어"),
    };

    private void BuildTroopReview(MapView3D view, HexMap map)
    {
        // r=4: 병종별로 1기씩. r=5: 첫 병종으로 편대 규모 1·3·5·7기.
        for (var i = 0; i < TroopReview.Length; i++)
        {
            PlaceTroopGroup(view, map, new HexCoord(2 + i * 2, 4),
                TroopReview[i].File, 1, TroopReview[i].Label);
        }

        for (var i = 0; i < TroopFormation.Sizes.Length; i++)
        {
            var size = TroopFormation.Sizes[i];
            PlaceTroopGroup(view, map, new HexCoord(2 + i * 3, 5),
                TroopReview[0].File, size, $"{size}기");
        }
    }

    private void PlaceTroopGroup(MapView3D view, HexMap map, HexCoord coord,
        string modelFile, int count, string label)
    {
        if (!map.Contains(coord))
        {
            return;
        }

        var root = new Node3D
        {
            Position = view.HexToWorld(coord) + new Vector3(0f, view.TileTopY, 0f),
        };
        AddChild(root);
        TroopFormation.Build(root, GD.Load<PackedScene>($"res://assets/models/{modelFile}"), count);
        root.AddChild(new Label3D
        {
            Text = label,
            Font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf"),
            FontSize = 96,
            PixelSize = 0.0022f,
            OutlineSize = 26,
            OutlineModulate = new Color(0f, 0f, 0f, 0.85f),
            Modulate = new Color(0.97f, 0.96f, 0.92f),
            Position = new Vector3(0f, 0.42f, 0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
        });
    }

    // 성 마당 주민 연출: 발자국 타일마다 4~5명씩 — 작은성 1배·중간성 3배·큰성 5배가 된다.
    // 좌표는 성 모델 로컬(클러스터 중심 기준, make_castles.py의 단위 육각 × K_XY=0.5774,
    // Blender +Y=북 → Godot -Z). 기단 윗면 높이 = PLATFORM_H 0.12 × K_Z 0.72 = 0.0864.
    private const float CastleGroundY = 0.0864f;

    private static void AddCastleAmbience(Node3D root, City city, TileCondition condition)
    {
        var tiles = CastleTileOffsets(city.Castle);
        var buildings = CastleBuildingObstacles(city.Castle);
        for (var i = 0; i < tiles.Length; i++)
        {
            var (tx, tz) = tiles[i];
            var obstacles = new Vector3[buildings.Length];
            for (var j = 0; j < buildings.Length; j++)
            {
                obstacles[j] = new Vector3(buildings[j].X - tx, buildings[j].Y - tz, buildings[j].Z);
            }

            root.AddChild(new VillagerAmbience
            {
                Position = new Vector3(tx, 0f, tz),
                GroundY = CastleGroundY,
                Seed = unchecked((ulong)(city.Position.Q * 92821L + city.Position.R * 68917L + i * 5077L + 31L)),
                MaxVillagers = 4 + i % 2,
                Obstacles = obstacles,
                SpawnEnabled = condition == TileCondition.Normal,
            });
        }
    }

    // 발자국 육각 중심(성 모델 로컬 x, z) — make_castles.py FOOTPRINTS × K_XY, y→-z
    private static (float, float)[] CastleTileOffsets(CastleSize size) => size switch
    {
        CastleSize.Small => new[] { (0f, 0f) },
        CastleSize.Medium => new[] { (0f, -0.5774f), (0.5f, 0.2887f), (-0.5f, 0.2887f) },
        _ => new[] { (-0.5f, -0.5197f), (0.5f, -0.5197f), (-1f, 0.3464f), (0f, 0.3464f), (1f, 0.3464f) },
    };

    // 성내 건물 장애물 (x, z, 반경) — make_castles.py buildings 위치 × K_XY, 반경 ≈ 폭 × 0.40
    private static Vector3[] CastleBuildingObstacles(CastleSize size) => size switch
    {
        CastleSize.Small => new[] { new Vector3(0f, 0f, 0.20f) },
        CastleSize.Medium => new[]
        {
            new Vector3(0f, 0f, 0.20f),          // 중앙 3단(공유 꼭짓점)
            new Vector3(0f, -0.5774f, 0.13f),    // 12시 1단
            new Vector3(0.5f, 0.2887f, 0.13f),   // 4시 1단
            new Vector3(-0.5f, 0.2887f, 0.13f),  // 8시 1단
        },
        _ => new[]
        {
            new Vector3(0f, 0f, 0.22f),          // 중앙 4단
            new Vector3(-0.5f, -0.5197f, 0.17f), // 북서 3단
            new Vector3(0.5f, -0.5197f, 0.16f),  // 북동 2단
            new Vector3(-1f, 0.3464f, 0.13f),    // 남서 1단
            new Vector3(1f, 0.3464f, 0.13f),     // 남동 1단
        },
    };

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
