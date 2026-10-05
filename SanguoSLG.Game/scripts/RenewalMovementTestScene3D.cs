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
    private static readonly HexCoord StartHex = new(1, 3);
    private static readonly HexCoord DestinationHex = new(7, 3);
    private const double PhasePresentationSeconds = 0.35;

    private RenewalAdvanceSimulator _simulator = null!;
    private readonly List<string> _logs = [];
    private RenewalAdvanceState _state = null!;
    private RenewalFixedStepClock _clock;
    private RenewalUnitState[] _initialUnits = [];
    private MapView3D _map = null!;
    private readonly Dictionary<long, Node3D> _tokens = [];
    private Label _status = null!;
    private Label _log = null!;
    private Button _playButton = null!;
    private bool _playing;
    private double _phaseElapsed;

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
        if (!_playing || _state.IsCompleted)
        {
            return;
        }

        if (_state.Phase == RenewalAdvancePhase.Movement)
        {
            var elapsed = Math.Max(0L, (long)Math.Round(delta * 1_000_000d));
            var result = _simulator.AdvanceElapsed(_state, _clock, elapsed);
            _state = result.State;
            _clock = result.Clock;
            Consume(result.Events);
            return;
        }

        _phaseElapsed += delta;
        if (_phaseElapsed < PhasePresentationSeconds)
        {
            return;
        }

        _phaseElapsed = 0;
        Apply(_simulator.StepPhase(_state));
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
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-58f, -35f, 0f),
            LightEnergy = 1.1f,
            ShadowEnabled = true,
        });

        var terrain = new Dictionary<HexCoord, TerrainType>
        {
            [new HexCoord(2, 3)] = TerrainType.Swamp,
            [new HexCoord(3, 2)] = TerrainType.Mountain,
            [new HexCoord(5, 1)] = TerrainType.WaterDeep,
            [new HexCoord(6, 1)] = TerrainType.WaterDeep,
        };
        var hexMap = new HexMap(0, 8, 0, 6, terrain);
        var blockedTile = new HexCoord(4, 3);
        _simulator = new RenewalAdvanceSimulator(new RenewalMovementMap(hexMap, [blockedTile]));

        _map = new MapView3D();
        AddChild(_map);
        _map.Build(hexMap, new HashSet<HexCoord>(), new TileConditionMap());

        var camera = new CameraController3D { Fov = 52f };
        AddChild(camera);
        camera.Setup(_map.HexToWorld(new HexCoord(4, 3)), 8.5f);
        camera.Current = true;

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
            OffsetBottom = 390,
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
        box.AddChild(MakeLabel("3단계 · 늪 감속 / 건물 우회 / 아군 중첩 이동 후 목적지 분산", 13,
            new Color(0.72f, 0.76f, 0.82f)));

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
        _initialUnits =
        [
            RenewalUnitState.Create(new UnitId(1), new ContinuousPosition(0, 0),
                RenewalHexSpace.Center(DestinationHex), movementPerDay: 2),
            RenewalUnitState.Create(new UnitId(2), new ContinuousPosition(0, 0),
                RenewalHexSpace.Center(DestinationHex), movementPerDay: 3),
        ];
        _initialUnits = _initialUnits.Select(x => x with
        {
            Position = RenewalHexSpace.Center(StartHex),
            Owner = new FactionId(1),
        }).ToArray();
        _state = _simulator.Start(_initialUnits);
        EnsureTokens();
        _logs.Clear();
        AppendLog("초기화 완료 — 한 틱·한 단계·하루 또는 자동 재생을 선택하세요.");
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
        if (_state.IsCompleted)
        {
            _playing = false;
        }
        Refresh();
    }

    private void Refresh()
    {
        EnsureTokens();
        foreach (var unit in _state.Units)
        {
            _tokens[unit.Id.Value].Position = ContinuousToWorld(unit.Position)
                + new Vector3(0f, _map.TileTopY + 0.20f, 0f);
        }
        var primary = _state.Units[0];
        _status.Text = $"날짜  {_state.Day}/7    단계  {PhaseName(_state.Phase)}\n"
            + $"이동 틱  {_state.MovementTick}/50    1번 {primary.Position.X},{primary.Position.Y}\n"
            + $"2번 {_state.Units[1].Position.X},{_state.Units[1].Position.Y}";
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
            var token = new Node3D();
            AddChild(token);
            token.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.13f, BottomRadius = 0.18f, Height = 0.38f },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = unit.Id.Value == 1
                        ? new Color(0.18f, 0.43f, 0.90f)
                        : new Color(0.18f, 0.75f, 0.48f),
                    Roughness = 0.55f,
                },
            });
            token.AddChild(new Label3D
            {
                Text = $"아군 {unit.Id.Value}",
                Font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf"),
                FontSize = 72,
                PixelSize = 0.0022f,
                Position = new Vector3(0f, 0.44f, 0f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true,
                OutlineSize = 18,
                OutlineModulate = new Color(0f, 0f, 0f, 0.85f),
            });
            _tokens[unit.Id.Value] = token;
        }
    }

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
        var resetStart = _simulator.Start(_initialUnits);
        var result = _simulator.RunToCompletion(resetStart);
        var attackPhases = result.Events.Count(x => x.Kind == RenewalAdvanceEventKind.PhaseChanged
            && x.Phase == RenewalAdvancePhase.Attack);
        var completedDays = result.Events.Count(x => x.Kind == RenewalAdvanceEventKind.DayCompleted);
        var positions = result.State.Units.Select(x => x.Position).Distinct().Count();
        var passed = result.State.IsCompleted && result.State.Day == 7
            && attackPhases == 7 && completedDays == 7
            && result.State.Units.All(x => x.Arrived) && positions == 2;
        GD.Print($"[renewal-movement-auto] passed={passed} days={completedDays} attacks={attackPhases} "
            + $"phase={result.State.Phase} arrived={result.State.Units.Count(x => x.Arrived)} "
            + $"distinct={positions}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
