using System.Collections.Generic;
using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 이동 시뮬레이션 GUI 검증 하베스트(doc/test/movement-cases.md). Core
/// <see cref="MovementSimulator"/>가 계산한 스텝(틱)을 "진행" 버튼으로 하나씩
/// 재생해, 탐지→추격→사거리 정지 같은 규칙을 눈으로 확인한다.
/// 표현 전용 — 규칙 판정은 전부 Core가 소유한다.
/// </summary>
public partial class MovementTestScene3D : Node3D
{
    private static readonly Color Blue = new(0.24f, 0.44f, 0.86f);
    private static readonly Color Red = new(0.82f, 0.22f, 0.18f);

    private const float StepSeconds = 0.4f;

    private MapView3D _view = null!;
    private readonly Dictionary<int, Node3D> _tokens = new();
    private IReadOnlyList<MovementTick> _ticks = new List<MovementTick>();
    private StopReason _reason;
    private int _index;
    private bool _animating;

    private Button _stepButton = null!;
    private Label _logLabel = null!;
    private readonly List<string> _logLines = new();

    public void Build(MapView3D view, CameraController3D camera)
    {
        _view = view;

        // 케이스 1 — 공격모드 A1이 먼 목표로 가다 정지한 적 E1을 탐지·추격·사거리 정지
        var a1 = new FieldUnit(new UnitId(1), new FactionId(1), new HexCoord(0, 2),
            Speed: 2, Detection: 2, AttackRange: 1, MovementDomain.Land,
            UnitMode.Attack, new HexCoord(12, 2), CommandOrder: 0);
        var e1 = new FieldUnit(new UnitId(2), new FactionId(2), new HexCoord(9, 2),
            Speed: 2, Detection: 2, AttackRange: 1, MovementDomain.Land,
            UnitMode.March, Target: null, CommandOrder: 0);

        var map = new HexMap(0, 12, 0, 4);
        var result = new MovementSimulator(new PassabilityMap(map, [], [])).Advance(new[] { a1, e1 });
        _ticks = result.Ticks;
        _reason = result.Reason;

        SpawnToken(a1, Blue, "A1 [공격]", faceEast: true);
        SpawnToken(e1, Red, "E1 [정지]", faceEast: false);
        AddDetectionRing(a1);

        BuildHud();

        camera.Setup(_view.HexToWorld(new HexCoord(6, 2)), 9f);
    }

    private void SpawnToken(FieldUnit unit, Color color, string label, bool faceEast)
    {
        var root = new Node3D
        {
            Position = _view.HexToWorld(unit.Position) + new Vector3(0f, _view.TileTopY, 0f),
            // 모델 정면은 +Z(북). 동서로 세우려면 ±90도 돌린다
            RotationDegrees = new Vector3(0f, faceEast ? -90f : 90f, 0f),
        };
        AddChild(root);

        var model = GD.Load<PackedScene>("res://assets/models/troop-swordsman.glb").Instantiate<Node3D>();
        root.AddChild(model);
        FactionColorView.Apply(root, color);
        MapView3D.TuneImportedMeshes(root);

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

        _tokens[unit.Id.Value] = root;
    }

    // A1의 탐지 범위(2칸)를 바닥 고리로 그린다 — E1이 이 안에 들면 추격이 시작된다.
    private void AddDetectionRing(FieldUnit unit)
    {
        if (!_tokens.TryGetValue(unit.Id.Value, out var token))
        {
            return;
        }

        var spacing = _view.HexWorldSize * Mathf.Sqrt(3f);
        var radius = unit.Detection * spacing;
        var ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = radius - 0.02f, OuterRadius = radius + 0.02f, Rings = 48, RingSegments = 8 },
            Position = new Vector3(0f, -_view.TileTopY + 0.02f, 0f),
            // 토큰이 yaw로 회전해 있으므로 고리는 카메라 기준 수평이 되도록 상쇄 없이 눕힌다
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.30f, 0.55f, 1f, 0.6f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            },
        };
        token.AddChild(ring);
    }

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        var font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf");

        var panel = new PanelContainer { AnchorTop = 1f, AnchorBottom = 1f, OffsetTop = -196f, OffsetLeft = 16f, OffsetBottom = -16f, OffsetRight = 440f };
        var style = new StyleBoxFlat { BgColor = new Color(0.10f, 0.11f, 0.13f, 0.94f), ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12 };
        panel.AddThemeStyleboxOverride("panel", style);
        layer.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);

        var title = new Label { Text = "케이스 1 — 공격모드 조우: 탐지 → 추격 → 사거리 정지" };
        title.AddThemeFontOverride("font", font);
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", new Color(0.82f, 0.68f, 0.38f));
        box.AddChild(title);

        _logLabel = new Label { Text = "진행을 눌러 하루씩(스텝) 재생하세요.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _logLabel.AddThemeFontOverride("font", font);
        _logLabel.AddThemeFontSizeOverride("font_size", 13);
        _logLabel.AddThemeColorOverride("font_color", new Color(0.90f, 0.92f, 0.95f));
        _logLabel.CustomMinimumSize = new Vector2(408f, 96f);
        box.AddChild(_logLabel);

        _stepButton = new Button { Text = "진행 ▶  (스텝)" };
        _stepButton.AddThemeFontOverride("font", font);
        _stepButton.AddThemeFontSizeOverride("font_size", 15);
        _stepButton.Pressed += OnStep;
        box.AddChild(_stepButton);

        UpdateButton();
    }

    private void OnStep()
    {
        if (_animating || _index >= _ticks.Count)
        {
            return;
        }

        var tick = _ticks[_index++];
        _animating = true;

        foreach (var unit in tick.Units)
        {
            if (_tokens.TryGetValue(unit.Id.Value, out var token))
            {
                var to = _view.HexToWorld(unit.Position) + new Vector3(0f, _view.TileTopY, 0f);
                CreateTween().TweenProperty(token, "position", to, StepSeconds)
                    .SetTrans(Tween.TransitionType.Sine);
            }
        }

        foreach (var e in tick.Events)
        {
            AppendLog(Describe(e));
        }

        if (_index >= _ticks.Count)
        {
            AppendLog($"■ 진행 종료 — {ReasonText(_reason)}");
        }

        var clock = CreateTween();
        clock.TweenInterval(StepSeconds + 0.05f);
        clock.Finished += () =>
        {
            _animating = false;
            UpdateButton();
        };

        UpdateButton();
    }

    private void UpdateButton()
    {
        var done = _index >= _ticks.Count;
        _stepButton.Disabled = _animating || done;
        _stepButton.Text = done
            ? $"완료 ({_ticks.Count}스텝)"
            : $"진행 ▶  (스텝 {_index + 1}/{_ticks.Count})";
    }

    private void AppendLog(string line)
    {
        _logLines.Add(line);
        if (_logLines.Count > 6)
        {
            _logLines.RemoveAt(0);
        }

        _logLabel.Text = string.Join("\n", _logLines);
    }

    private static string Describe(TickEvent e) => e.Kind switch
    {
        TickEventKind.PursuitStarted => $"● A{e.Unit.Value} 적 탐지 → 추격 시작 (대상 U{e.Other!.Value.Value})",
        TickEventKind.PursuitEnded => $"○ A{e.Unit.Value} 시야 상실 → 원래 목표 복귀",
        TickEventKind.Halted => $"▶ A{e.Unit.Value} 사거리 도달 — 이동 종료 (전투)",
        TickEventKind.Engaged => $"✕ U{e.Unit.Value} ↔ U{e.Other!.Value.Value} 정면 교전",
        _ => e.Kind.ToString(),
    };

    private static string ReasonText(StopReason reason) => reason switch
    {
        StopReason.EnemyInRange => "사거리 안의 적 — 전투 페이즈로",
        StopReason.Engaged => "정면 자동 교전",
        StopReason.AllArrived => "전원 목표 도착",
        StopReason.Blocked => "길이 막혀 3일 정지",
        _ => "7일 경과",
    };
}
