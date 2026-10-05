namespace SanguoSLG.Game;

using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;

/// <summary>
/// Phase 18D 독립 검수장. 캠페인과 분리하여 고정 틱 이동과 하루 단계 전이를 확인한다.
/// 3단계까지 지형 경로·아군 분산·적/건물 연속 충돌을 연결했다.
/// </summary>
public partial class RenewalMovementTestScene3D : Node3D
{
    private enum QaScenario
    {
        TerrainPath,
        MovementSpeed,
        Collision,
        EgressAndEntry,
    }

    private static readonly HexCoord StartHex = new(1, 3);
    private static readonly HexCoord DestinationHex = new(7, 3);
    private const double PhasePresentationSeconds = 0.35;
    private const float RenewalMarchSpeedScale = 0.42f;
    private const double MovementSnapshotSeconds =
        RenewalFixedStepClock.TickMicroseconds / 1_000_000d;

    private RenewalAdvanceSimulator _simulator = null!;
    private RenewalDeploymentService _deployment = null!;
    private RenewalMovementMap _movementMap = null!;
    private City[] _sites = [];
    private IReadOnlyList<RenewalEntryOrder> _entryOrders = [];
    private readonly List<string> _logs = [];
    private RenewalAdvanceState _state = null!;
    private RenewalFixedStepClock _clock;
    private RenewalUnitState[] _initialUnits = [];
    private MapView3D _map = null!;
    private DirectionalLight3D _sun = null!;
    private readonly Dictionary<int, UnitController3D> _tokens = [];
    private readonly Dictionary<int, Vector3> _visualStarts = [];
    private readonly Dictionary<int, Vector3> _visualTargets = [];
    private readonly Dictionary<int, double> _visualElapsed = [];
    private Label _status = null!;
    private Label _log = null!;
    private Button _playButton = null!;
    private OptionButton _scenarioSelector = null!;
    private bool _playing;
    private double _phaseElapsed;
    private QaScenario _scenario;

    public override void _Ready()
    {
        BuildWorld();
        BuildHud();
        ResetSimulation();

        var args = OS.GetCmdlineArgs().Concat(OS.GetCmdlineUserArgs()).ToHashSet();
        if (args.Contains("--renewalmovementauto"))
        {
            CallDeferred(nameof(RunAutoQa));
        }
    }

    public override void _Process(double delta)
    {
        if (_playing && !_state.IsCompleted)
        {
            if (_state.Phase == RenewalAdvancePhase.Movement)
            {
                var elapsed = Math.Max(0L, (long)Math.Round(delta * 1_000_000d));
                var result = _simulator.AdvanceElapsed(_state, _clock, elapsed);
                _state = result.State;
                _clock = result.Clock;
                Consume(result.Events);
            }
            else
            {
                _phaseElapsed += delta;
                if (_phaseElapsed >= PhasePresentationSeconds)
                {
                    _phaseElapsed = 0;
                    Apply(_simulator.StepPhase(_state));
                }
            }
        }
        AnimateTokens((float)delta);
    }

    private void BuildWorld()
    {
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = new Color(0.12f, 0.16f, 0.20f),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.70f, 0.76f, 0.82f),
            AmbientLightEnergy = 0.75f,
        };
        AddChild(new WorldEnvironment { Environment = environment });
        _sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-58f, -35f, 0f),
            LightEnergy = 1.1f,
            ShadowEnabled = true,
            // 기본 그림자 범위·normal bias는 이 게임의 작은 타일/병사 축척에 비해 너무 크다.
            // 카메라 캐스케이드 재샘플링 때 발·무기 그림자가 흔들리지 않도록 캠페인 광원과
            // 같은 축척 규칙을 사용하고, 검수장 크기에 맞춰 최대 거리만 더 좁힌다.
            DirectionalShadowMaxDistance = 18f,
            ShadowBias = 0.03f,
            ShadowNormalBias = 0.05f,
            DirectionalShadowBlendSplits = true,
        };
        AddChild(_sun);

        var terrain = new Dictionary<HexCoord, TerrainType>
        {
            [new HexCoord(2, 3)] = TerrainType.Swamp,
            [new HexCoord(3, 2)] = TerrainType.Mountain,
            [new HexCoord(5, 1)] = TerrainType.WaterDeep,
            [new HexCoord(6, 1)] = TerrainType.WaterDeep,
        };
        for (var q = 0; q <= 10; q++)
        {
            terrain[new HexCoord(q, 8)] = TerrainType.WaterDeep;
        }
        _sites =
        [
            new City(new CityId(101), "출격성", new HexCoord(0, 0), new FactionId(1), 1_000),
            new City(new CityId(102), "복귀성", new HexCoord(10, 0), new FactionId(1), 1_000),
            new City(new CityId(201), "출항항", new HexCoord(0, 8), new FactionId(1), 1_000,
                Port: PortSize.Small),
            new City(new CityId(202), "입항항", new HexCoord(10, 8), new FactionId(1), 1_000,
                Port: PortSize.Small),
        ];
        var hexMap = new HexMap(0, 10, 0, 8, terrain);
        var blockedTile = new HexCoord(4, 3);
        var buildingTiles = _sites.SelectMany(CastleFootprint.TilesFor).Append(blockedTile);
        _movementMap = new RenewalMovementMap(hexMap, buildingTiles);
        _simulator = new RenewalAdvanceSimulator(_movementMap);
        _deployment = new RenewalDeploymentService(_movementMap);

        _map = new MapView3D();
        AddChild(_map);
        _map.Build(hexMap, new HashSet<HexCoord>(), new TileConditionMap());

        var camera = new CameraController3D { Fov = 52f };
        AddChild(camera);
        camera.Setup(_map.HexToWorld(new HexCoord(5, 4)), 10.5f);
        camera.Current = true;

        foreach (var site in _sites)
        {
            AddSiteModel(site);
        }

        var blocker = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.55f, 0.55f, 0.55f) },
            Position = _map.HexToWorld(blockedTile) + new Vector3(0f, _map.TileTopY + 0.28f, 0f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.45f, 0.28f, 0.16f),
                Roughness = 0.55f,
            },
        };
        AddChild(blocker);
        blocker.AddChild(new Label3D
        {
            Text = "통과 금지 건물",
            Font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf"),
            FontSize = 58,
            PixelSize = 0.002f,
            Position = new Vector3(0f, 0.5f, 0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            OutlineSize = 14,
            OutlineModulate = new Color(0f, 0f, 0f, 0.85f),
        });

        AddMarker(StartHex, "시작", new Color(0.30f, 0.60f, 1f));
        AddMarker(DestinationHex, "목적지", new Color(0.95f, 0.62f, 0.20f));
    }

    private void AddSiteModel(City site)
    {
        var file = site.IsPort
            ? "res://assets/models/port-small.glb"
            : "res://assets/models/castle-small.glb";
        var scene = GD.Load<PackedScene>(file);
        if (scene?.Instantiate<Node3D>() is not { } model)
        {
            return;
        }
        model.Position = _map.HexToWorld(site.Position) + new Vector3(0f, _map.TileTopY, 0f);
        model.Scale = Vector3.One * (site.IsPort ? 0.42f : 0.48f);
        AddChild(model);
    }

    private void AddMarker(HexCoord coord, string text, Color color)
    {
        var marker = new MeshInstance3D
        {
            Mesh = new TorusMesh
            {
                InnerRadius = 0.22f,
                OuterRadius = 0.26f,
                Rings = 32,
                RingSegments = 8,
            },
            Position = _map.HexToWorld(coord) + new Vector3(0f, _map.TileTopY + 0.025f, 0f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };
        AddChild(marker);
        marker.AddChild(new Label3D
        {
            Text = text,
            Font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf"),
            FontSize = 64,
            PixelSize = 0.002f,
            Position = new Vector3(0f, 0.06f, 0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
            OutlineSize = 16,
        });
    }

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        var panel = new PanelContainer
        {
            OffsetLeft = 18,
            OffsetTop = 18,
            OffsetRight = 520,
            OffsetBottom = 430,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.08f, 0.10f, 0.95f),
            BorderColor = new Color(0.72f, 0.55f, 0.24f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 14,
            ContentMarginBottom = 14,
        });
        layer.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 9);
        panel.AddChild(box);
        box.AddChild(MakeLabel("Phase 18D — 연속 이동 검수장", 20,
            new Color(0.92f, 0.73f, 0.34f)));
        box.AddChild(MakeLabel("4단계 · 연속 이동 / 충돌 / 성·항구 출격·입성", 13,
            new Color(0.72f, 0.76f, 0.82f)));

        _scenarioSelector = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _scenarioSelector.AddThemeFontOverride("font", GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf"));
        _scenarioSelector.AddItem("1. 지형 경로·건물 우회");
        _scenarioSelector.AddItem("2. 이동속도 1·2·3 비교");
        _scenarioSelector.AddItem("3. 아군 중첩·적군 충돌");
        _scenarioSelector.AddItem("4. 성·항구 출격·입성·복귀");
        _scenarioSelector.ItemSelected += index =>
        {
            _scenario = (QaScenario)index;
            ResetSimulation();
        };
        box.AddChild(_scenarioSelector);

        var shadowMode = new OptionButton();
        shadowMode.AddItem("그림자 비교: 켜기");
        shadowMode.AddItem("그림자 비교: 끄기 (원인 확인용)");
        shadowMode.ItemSelected += index => _sun.ShadowEnabled = index == 0;
        box.AddChild(shadowMode);

        _status = MakeLabel(string.Empty, 15, new Color(0.94f, 0.95f, 0.98f));
        box.AddChild(_status);

        var firstRow = new HBoxContainer();
        firstRow.AddThemeConstantOverride("separation", 7);
        box.AddChild(firstRow);
        _playButton = AddButton(firstRow, "자동 재생", TogglePlay);
        AddButton(firstRow, "초기화", ResetSimulation);
        AddButton(firstRow, "한 틱", () => Apply(_simulator.StepMovementTick(_state)));

        var secondRow = new HBoxContainer();
        secondRow.AddThemeConstantOverride("separation", 7);
        box.AddChild(secondRow);
        AddButton(secondRow, "한 단계", () => Apply(_simulator.StepPhase(_state)));
        AddButton(secondRow, "하루", () => Apply(_simulator.StepDay(_state)));
        AddButton(secondRow, "7일 완료", () => Apply(_simulator.RunToCompletion(_state)));

        _log = MakeLabel(string.Empty, 13, new Color(0.78f, 0.84f, 0.90f));
        _log.CustomMinimumSize = new Vector2(460, 170);
        _log.VerticalAlignment = VerticalAlignment.Top;
        box.AddChild(_log);
    }

    private static Label MakeLabel(string text, int size, Color color)
    {
        var label = new Label
        {
            Text = text,
            Modulate = color,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontOverride("font", GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf"));
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    private static Button AddButton(Container parent, string text, Action pressed)
    {
        var button = new Button
        {
            Text = text,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        button.AddThemeFontOverride("font", GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf"));
        button.Pressed += pressed;
        parent.AddChild(button);
        return button;
    }

    private void ResetSimulation()
    {
        _playing = false;
        _phaseElapsed = 0;
        _clock = new RenewalFixedStepClock();
        _initialUnits = BuildScenarioUnits(_scenario);
        _state = _simulator.Start(_initialUnits);
        EnsureTokens();
        var activeIds = _state.Units.Select(x => x.Id.Value).ToHashSet();
        foreach (var (id, token) in _tokens)
        {
            token.Visible = activeIds.Contains(id);
        }
        foreach (var unit in _state.Units)
        {
            var world = ContinuousToWorld(unit.Position) + new Vector3(0f, _map.TileTopY, 0f);
            _visualStarts[unit.Id.Value] = world;
            _visualTargets[unit.Id.Value] = world;
            _visualElapsed[unit.Id.Value] = MovementSnapshotSeconds;
            _tokens[unit.Id.Value].DisplayContinuousAt(world, false);
        }
        _logs.Clear();
        AppendLog($"{ScenarioName(_scenario)} 초기화 — 자동 재생 또는 단계 실행을 선택하세요.");
        Refresh();
    }

    private void TogglePlay()
    {
        if (_state.IsCompleted)
        {
            ResetSimulation();
        }
        _playing = !_playing;
        _phaseElapsed = 0;
        Refresh();
    }

    private void Apply(RenewalStepResult result)
    {
        _state = result.State;
        Consume(result.Events);
    }

    private void Consume(IReadOnlyList<RenewalAdvanceEvent> events)
    {
        foreach (var entry in events.Where(x => x.Kind != RenewalAdvanceEventKind.UnitMoved))
        {
            AppendLog(entry.Kind switch
            {
                RenewalAdvanceEventKind.PhaseChanged => $"{entry.Day}일차 → {PhaseName(entry.Phase)}",
                RenewalAdvanceEventKind.DayCompleted => $"{entry.Day}일차 정산 완료",
                RenewalAdvanceEventKind.AdvanceCompleted => "7일 진행 완료 · 명령 대기",
                RenewalAdvanceEventKind.UnitArrived => $"{entry.Unit} 목적지 도착",
                RenewalAdvanceEventKind.UnitDispersed => $"{entry.Unit} 목적지 근처 분산 완료",
                RenewalAdvanceEventKind.UnitBlocked => $"{entry.Unit} 이동 대기: {entry.StopReason}",
                _ => entry.Kind.ToString(),
            });
        }
        if (_scenario == QaScenario.EgressAndEntry && _entryOrders.Count > 0)
        {
            var entry = _deployment.ResolveEntries(_state.Units, _entryOrders, _sites);
            if (entry.Transfers.Count > 0)
            {
                _state = _state with { Units = entry.FieldUnits };
                _entryOrders = entry.PendingOrders;
                foreach (var transfer in entry.Transfers)
                {
                    AppendLog($"{transfer.Unit} → {SiteName(transfer.Site)} 입성 완료");
                }
            }
        }
        if (_state.IsCompleted)
        {
            _playing = false;
        }
        Refresh();
    }

    private void Refresh()
    {
        EnsureTokens();
        var activeIds = _state.Units.Select(x => x.Id.Value).ToHashSet();
        foreach (var (id, token) in _tokens)
        {
            token.Visible = activeIds.Contains(id);
        }
        foreach (var unit in _state.Units)
        {
            var world = ContinuousToWorld(unit.Position)
                + new Vector3(0f, _map.TileTopY, 0f);
            if (!_visualTargets.TryGetValue(unit.Id.Value, out var previous)
                || !previous.IsEqualApprox(world))
            {
                _visualStarts[unit.Id.Value] = _tokens[unit.Id.Value].Position;
                _visualElapsed[unit.Id.Value] = 0d;
            }
            _visualTargets[unit.Id.Value] = world;
        }
        _status.Text = $"날짜  {_state.Day}/7    단계  {PhaseName(_state.Phase)}\n"
            + $"이동 틱  {_state.MovementTick}/50    {ScenarioName(_scenario)}\n"
            + string.Join("  ", _state.Units.Select(x =>
                $"{x.Id}:{x.StopReason}{(x.Arrived ? "·도착" : string.Empty)}"));
        _playButton.Text = _playing ? "일시정지" : _state.IsCompleted ? "다시 시작" : "자동 재생";
        _log.Text = string.Join('\n', _logs.TakeLast(9));
    }

    private void EnsureTokens()
    {
        foreach (var unit in _state.Units)
        {
            if (_tokens.ContainsKey(unit.Id.Value))
            {
                continue;
            }
            var token = new UnitController3D { Name = $"RenewalUnit{unit.Id.Value}" };
            AddChild(token);
            var troopIndex = unit.Domain == MovementDomain.DeepWater
                ? 7
                : unit.Id.Value switch
            {
                1 => 0,
                2 => 2,
                _ => 1,
            };
            var color = unit.Owner.Value == 2
                ? new Color(0.86f, 0.22f, 0.18f)
                : new Color(0.18f, 0.43f, 0.90f);
            token.InitDisplay(_map, color, troopIndex, StartHex);
            token.SetDisplayMarchSpeedScale(RenewalMarchSpeedScale);
            token.TintFormation(color, 0.40f);
            _tokens[unit.Id.Value] = token;
            _visualStarts[unit.Id.Value] = token.Position;
            _visualTargets[unit.Id.Value] = token.Position;
            _visualElapsed[unit.Id.Value] = MovementSnapshotSeconds;
        }
    }

    private RenewalUnitState[] BuildScenarioUnits(QaScenario scenario)
    {
        static RenewalUnitState Make(int id, int faction, HexCoord start, HexCoord destination,
            int speed) => RenewalUnitState.Create(new UnitId(id), RenewalHexSpace.Center(start),
                RenewalHexSpace.Center(destination), speed) with { Owner = new FactionId(faction) };

        _entryOrders = [];
        if (scenario == QaScenario.EgressAndEntry)
        {
            var castleOrigin = _sites.Single(x => x.Id.Value == 101);
            var castleTarget = _sites.Single(x => x.Id.Value == 102);
            var portOrigin = _sites.Single(x => x.Id.Value == 201);
            var portTarget = _sites.Single(x => x.Id.Value == 202);
            var land = Make(1, 1, castleOrigin.Position, castleTarget.Position, 3);
            var ship = Make(5, 1, portOrigin.Position, portTarget.Position, 2) with
            {
                Domain = MovementDomain.DeepWater,
            };
            var release = _deployment.Release(1,
            [
                new RenewalDeploymentReservation(1, land, castleOrigin.Id,
                    DestinationSite: castleTarget.Id),
                new RenewalDeploymentReservation(2, ship, portOrigin.Id,
                    DestinationSite: portTarget.Id),
            ], [], _sites);
            _entryOrders = release.EntryOrders;
            return release.Released.ToArray();
        }

        return scenario switch
        {
            QaScenario.TerrainPath =>
            [
                Make(1, 1, new HexCoord(1, 3), new HexCoord(7, 3), 2),
            ],
            QaScenario.MovementSpeed =>
            [
                Make(1, 1, new HexCoord(1, 0), new HexCoord(7, 0), 1),
                Make(2, 1, new HexCoord(1, 4), new HexCoord(7, 4), 2),
                Make(3, 1, new HexCoord(1, 6), new HexCoord(7, 6), 3),
            ],
            QaScenario.Collision =>
            [
                Make(1, 1, new HexCoord(1, 5), new HexCoord(7, 5), 3),
                Make(2, 1, new HexCoord(1, 5), new HexCoord(7, 5), 3),
                Make(4, 2, new HexCoord(4, 5), new HexCoord(4, 5), 0),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
    }

    private static string ScenarioName(QaScenario scenario) => scenario switch
    {
        QaScenario.TerrainPath => "지형 경로·건물 우회",
        QaScenario.MovementSpeed => "이동속도 비교",
        QaScenario.Collision => "아군 중첩·적군 충돌",
        QaScenario.EgressAndEntry => "성·항구 출격·입성·복귀",
        _ => scenario.ToString(),
    };

    private string SiteName(CityId id) =>
        _sites.FirstOrDefault(x => x.Id == id)?.Name ?? id.ToString();

    private void AnimateTokens(float delta)
    {
        foreach (var (id, token) in _tokens)
        {
            if (!_visualTargets.TryGetValue(id, out var target))
            {
                continue;
            }
            Vector3 next;
            if (_playing && _visualStarts.TryGetValue(id, out var start))
            {
                var elapsed = _visualElapsed.GetValueOrDefault(id) + delta;
                _visualElapsed[id] = elapsed;
                next = start.Lerp(target, VisualInterpolationAlpha(elapsed));
            }
            else
            {
                next = token.Position.MoveToward(target, 2.2f * delta);
            }
            var moving = !token.Position.IsEqualApprox(next)
                || ShouldHoldMarchPose(_state.Units.FirstOrDefault(x => x.Id.Value == id),
                    _playing, _state.Phase);
            token.DisplayContinuousAt(next, moving);
        }
    }

    /// <summary>
    /// 자동 재생은 Core 위치가 0.1초마다 갱신되므로 스냅샷 사이 한두 프레임의 위치 델타가 0일 수 있다.
    /// 그때 행군 자세를 정지 자세로 초기화하면 전 편대의 발·몸통과 그림자가 함께 튄다.
    /// 실제 이동 가능 상태인 동안에는 현재 보행 위상을 보존하고, 도착/봉쇄 때만 정지 자세로 복귀한다.
    /// </summary>
    private static bool ShouldHoldMarchPose(RenewalUnitState? unit, bool playing,
        RenewalAdvancePhase phase) => playing
        && phase == RenewalAdvancePhase.Movement
        && unit is { Arrived: false, MovementPerDay: > 0, StopReason: RenewalStopReason.None };

    private static float VisualInterpolationAlpha(double elapsedSeconds) =>
        Mathf.Clamp((float)(elapsedSeconds / MovementSnapshotSeconds), 0f, 1f);

    private Vector3 ContinuousToWorld(ContinuousPosition position)
    {
        var q = position.X / 867f;
        var r = (position.Y - 500f * q) / ContinuousPosition.UnitsPerTile;
        var origin = _map.HexToWorld(new HexCoord(0, 0));
        var qAxis = _map.HexToWorld(new HexCoord(1, 0)) - origin;
        var rAxis = _map.HexToWorld(new HexCoord(0, 1)) - origin;
        return origin + qAxis * q + rAxis * r;
    }

    private void AppendLog(string text)
    {
        _logs.Add(text);
        if (_logs.Count > 40)
        {
            _logs.RemoveAt(0);
        }
    }

    private static string PhaseName(RenewalAdvancePhase phase) => phase switch
    {
        RenewalAdvancePhase.Movement => "이동턴",
        RenewalAdvancePhase.MovementAftermath => "이동 후처리",
        RenewalAdvancePhase.Attack => "공격턴",
        RenewalAdvancePhase.AttackAftermath => "공격 후처리",
        RenewalAdvancePhase.DaySettlement => "하루 정산",
        RenewalAdvancePhase.Completed => "완료",
        _ => phase.ToString(),
    };

    private void RunAutoQa()
    {
        var scaledRoot = new Node3D { Scale = Vector3.One * 0.1f };
        var rotatedParent = new Node3D { RotationDegrees = new Vector3(0f, 40f, 0f) };
        var smallPart = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = Vector3.One * 0.2f },
        };
        var largePart = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = Vector3.One * 2f },
        };
        AddChild(scaledRoot);
        scaledRoot.AddChild(rotatedParent);
        rotatedParent.AddChild(smallPart);
        rotatedParent.AddChild(largePart);
        MapView3D.TuneImportedMeshes(scaledRoot);
        var scaledCasterCheck = smallPart.CastShadow == GeometryInstance3D.ShadowCastingSetting.Off
            && largePart.CastShadow == GeometryInstance3D.ShadowCastingSetting.On;
        scaledRoot.QueueFree();
        foreach (var scenario in Enum.GetValues<QaScenario>())
        {
            _scenario = scenario;
            ResetSimulation();
        }

        var terrain = _simulator.RunToCompletion(
            _simulator.Start(BuildScenarioUnits(QaScenario.TerrainPath)));
        var attackPhases = terrain.Events.Count(x => x.Kind == RenewalAdvanceEventKind.PhaseChanged
            && x.Phase == RenewalAdvancePhase.Attack);
        var completedDays = terrain.Events.Count(x => x.Kind == RenewalAdvanceEventKind.DayCompleted);

        var speedStart = _simulator.Start(BuildScenarioUnits(QaScenario.MovementSpeed));
        var speedDay = _simulator.StepDay(speedStart);
        var distances = speedDay.State.Units.OrderBy(x => x.Id.Value)
            .Select((x, i) => BuildScenarioUnits(QaScenario.MovementSpeed)[i].Position.DistanceTo(x.Position))
            .ToArray();

        var collision = _simulator.StepDay(
            _simulator.Start(BuildScenarioUnits(QaScenario.Collision)));
        var blocked = collision.State.Units.Count(x => x.Owner.Value == 1
            && x.StopReason == RenewalStopReason.EnemyBlocked);

        var entryStart = _simulator.Start(BuildScenarioUnits(QaScenario.EgressAndEntry));
        var entryRun = _simulator.RunToCompletion(entryStart);
        var entry = _deployment.ResolveEntries(entryRun.State.Units, _entryOrders, _sites);

        var interpolationOk = Mathf.IsEqualApprox(VisualInterpolationAlpha(0d), 0f)
            && Mathf.IsEqualApprox(VisualInterpolationAlpha(MovementSnapshotSeconds / 2d), 0.5f)
            && Mathf.IsEqualApprox(VisualInterpolationAlpha(MovementSnapshotSeconds), 1f)
            && Mathf.IsEqualApprox(VisualInterpolationAlpha(MovementSnapshotSeconds * 2d), 1f);
        var shadowStable = _sun.ShadowEnabled
            && Mathf.IsEqualApprox(_sun.DirectionalShadowMaxDistance, 18f)
            && Mathf.IsEqualApprox(_sun.ShadowBias, 0.03f)
            && Mathf.IsEqualApprox(_sun.ShadowNormalBias, 0.05f)
            && _sun.DirectionalShadowBlendSplits;
        var movingSample = BuildScenarioUnits(QaScenario.TerrainPath)[0];
        var marchPoseStable = ShouldHoldMarchPose(movingSample, true,
                RenewalAdvancePhase.Movement)
            && !ShouldHoldMarchPose(movingSample with { Arrived = true }, true,
                RenewalAdvancePhase.Movement)
            && !ShouldHoldMarchPose(movingSample, true,
                RenewalAdvancePhase.Attack)
            && !ShouldHoldMarchPose(movingSample, false,
                RenewalAdvancePhase.Movement);
        var passed = terrain.State.IsCompleted && terrain.State.Units.All(x => x.Arrived)
            && attackPhases == 7 && completedDays == 7
            && distances[0] < distances[1] && distances[1] < distances[2]
            && blocked == 2 && _tokens.Count == 5
            && entry.Transfers.Count == 2 && entry.FieldUnits.Count == 0
            && interpolationOk && shadowStable && marchPoseStable && scaledCasterCheck
            && _tokens.Values.All(x => Mathf.IsEqualApprox(
                x.DisplayMarchSpeedScale, RenewalMarchSpeedScale));
        GD.Print($"[renewal-movement-auto] passed={passed} cases=4 terrain_arrived="
            + $"{terrain.State.Units.Count(x => x.Arrived)} speed={string.Join('/', distances)} "
            + $"enemy_blocked={blocked} entries={entry.Transfers.Count} interpolation={interpolationOk} "
            + $"scaled_casters={scaledCasterCheck} shadow_settings_valid={shadowStable} march_pose_contract={marchPoseStable} "
            + $"march_scale={RenewalMarchSpeedScale:0.00}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
