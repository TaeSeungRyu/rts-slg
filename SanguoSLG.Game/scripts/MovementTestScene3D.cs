using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 이동 시뮬레이션 GUI 검증 하베스트(doc/test/movement-cases.md). Core
/// <see cref="MovementSimulator"/>가 계산한 스텝(틱)을 "진행" 버튼으로 하나씩
/// 재생해 이동 규칙을 눈으로 확인한다. "케이스 ▶▶"로 시나리오를 전환한다.
/// 표현 전용 — 규칙 판정은 전부 Core가 소유한다.
/// </summary>
public partial class MovementTestScene3D : Node3D
{
    private static readonly Color Blue = new(0.24f, 0.44f, 0.86f);
    private static readonly Color Red = new(0.82f, 0.22f, 0.18f);
    private const float StepSeconds = 0.4f;

    // 케이스 정의: 제목·설명·맵 크기·부대 목록(색·라벨은 소속/모드에서 파생)
    private sealed record CaseDef(string Title, string Note, int MaxQ, int MaxR, FieldUnit[] Units);

    private static FieldUnit U(int id, int owner, HexCoord pos, UnitMode mode, HexCoord? target,
        int speed, int detection, int attackRange) =>
        new(new UnitId(id), new FactionId(owner), pos, speed, detection, attackRange,
            MovementDomain.Land, mode, target, CommandOrder: id);

    private static readonly CaseDef[] Cases =
    {
        new("케이스 1 — 공격모드 조우: 탐지 → 추격 → 사거리 정지",
            "A1(공격)이 먼 목표로 가다 정지한 E1을 탐지 범위(2)에서 발견하면 추격으로 전환, 사거리(1)에 닿으면 멈춘다.",
            12, 4,
            new[]
            {
                U(1, 1, new HexCoord(0, 2), UnitMode.Attack, new HexCoord(12, 2), 2, 2, 1),
                U(2, 2, new HexCoord(9, 2), UnitMode.March, null, 2, 2, 1),
            }),
        new("케이스 2 — 행군모드 통과: 무시 + 감속",
            "A1(행군, 속도3)은 E1을 무시하고 지나간다. E1이 탐지 범위(3) 안에 든 날은 속도가 2로 준다. E1 사거리(2)를 지나는 동안은 전투 시 70% 일방 피해(반격 없음) — 피해는 전투 페이즈 소관이라 여기선 통과만 표시.",
            16, 4,
            new[]
            {
                U(1, 1, new HexCoord(0, 2), UnitMode.March, new HexCoord(16, 2), 3, 3, 1),
                U(2, 2, new HexCoord(8, 3), UnitMode.March, null, 2, 2, 2),
            }),
        new("케이스 3 — 정면 자동 교전: 같은 칸 경합",
            "A1·E1(둘 다 공격)이 짝수 거리로 마주 보고 다가온다. 가운데 칸을 동시에 노리는 순간 아무도 못 들어가고 그 자리에서 자동 교전한다(사거리 진입보다 몸이 부딪힌 게 먼저).",
            8, 4,
            new[]
            {
                U(1, 1, new HexCoord(0, 2), UnitMode.Attack, new HexCoord(8, 2), 1, 2, 1),
                U(2, 2, new HexCoord(8, 2), UnitMode.Attack, new HexCoord(0, 2), 1, 2, 1),
            }),
    };

    private MapView3D _view = null!;
    private CameraController3D _camera = null!;

    private int _caseIndex;
    private readonly Dictionary<int, Node3D> _tokens = new();
    private readonly List<Node3D> _spawned = new();
    private IReadOnlyList<MovementTick> _ticks = new List<MovementTick>();
    private StopReason _reason;
    private int _index;
    private bool _animating;
    private int _lastDayLogged;
    private readonly HashSet<int> _underFire = new();

    private Button _stepButton = null!;
    private Button _caseButton = null!;
    private Label _titleLabel = null!;
    private Label _noteLabel = null!;
    private Label _logLabel = null!;
    private readonly List<string> _logLines = new();

    public void Build(MapView3D view, CameraController3D camera)
    {
        _view = view;
        _camera = camera;
        BuildHud();
        LoadCase(0);
    }

    private void LoadCase(int index)
    {
        _caseIndex = index;
        var def = Cases[index];

        foreach (var node in _spawned)
        {
            node.QueueFree();
        }

        _spawned.Clear();
        _tokens.Clear();
        _logLines.Clear();
        _index = 0;
        _animating = false;
        _lastDayLogged = 0;
        _underFire.Clear();

        var map = new HexMap(0, def.MaxQ, 0, def.MaxR);
        var result = new MovementSimulator(new PassabilityMap(map, [], [])).Advance(def.Units);
        _ticks = result.Ticks;
        _reason = result.Reason;

        foreach (var unit in def.Units)
        {
            SpawnToken(unit);
        }

        _titleLabel.Text = def.Title;
        _noteLabel.Text = def.Note;
        AppendLog("진행을 눌러 스텝을 재생하세요.");

        _camera.Setup(_view.HexToWorld(new HexCoord(def.MaxQ / 2, def.MaxR / 2)), def.MaxQ * 0.85f + 3f);
        UpdateButtons();
    }

    private void SpawnToken(FieldUnit unit)
    {
        var attacker = unit.Owner.Value == 1;
        var moving = unit.Target is not null;
        var modeKo = unit.Mode == UnitMode.Attack ? "공격" : moving ? "행군" : "정지";
        var label = $"{(attacker ? "A" : "E")}{unit.Id.Value} [{modeKo}]";
        var color = attacker ? Blue : Red;
        var faceEast = unit.Target is { } t ? t.Q > unit.Position.Q : !attacker;

        var root = new Node3D
        {
            Position = _view.HexToWorld(unit.Position) + new Vector3(0f, _view.TileTopY, 0f),
            RotationDegrees = new Vector3(0f, faceEast ? -90f : 90f, 0f),
        };
        AddChild(root);
        _spawned.Add(root);

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

        // 이동 유닛엔 탐지 범위 고리(파랑), 정지 방어자엔 사거리 고리(빨강)를 그린다
        if (moving)
        {
            AddRing(root, unit.Detection, new Color(0.30f, 0.55f, 1f, 0.6f));
        }
        else if (unit.AttackRange >= 1)
        {
            AddRing(root, unit.AttackRange, new Color(1f, 0.35f, 0.30f, 0.6f));
        }
    }

    private void AddRing(Node3D token, int tiles, Color color)
    {
        var radius = tiles * _view.HexWorldSize * Mathf.Sqrt(3f);
        token.AddChild(new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = radius - 0.02f, OuterRadius = radius + 0.02f, Rings = 48, RingSegments = 8 },
            Position = new Vector3(0f, -_view.TileTopY + 0.02f, 0f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = color,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            },
        });
    }

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        var font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf");

        var panel = new PanelContainer
        {
            AnchorTop = 1f, AnchorBottom = 1f,
            OffsetTop = -232f, OffsetLeft = 16f, OffsetBottom = -16f, OffsetRight = 470f,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.11f, 0.13f, 0.94f),
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 12, ContentMarginBottom = 12,
        });
        layer.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);

        _titleLabel = MakeLabel(font, 16, new Color(0.82f, 0.68f, 0.38f));
        box.AddChild(_titleLabel);

        _noteLabel = MakeLabel(font, 12, new Color(0.72f, 0.76f, 0.82f));
        _noteLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _noteLabel.CustomMinimumSize = new Vector2(438f, 46f);
        box.AddChild(_noteLabel);

        _logLabel = MakeLabel(font, 13, new Color(0.90f, 0.92f, 0.95f));
        _logLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _logLabel.CustomMinimumSize = new Vector2(438f, 84f);
        _logLabel.VerticalAlignment = VerticalAlignment.Top;
        box.AddChild(_logLabel);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        box.AddChild(row);

        _stepButton = new Button();
        _stepButton.AddThemeFontOverride("font", font);
        _stepButton.AddThemeFontSizeOverride("font_size", 15);
        _stepButton.Pressed += OnStep;
        _stepButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        row.AddChild(_stepButton);

        _caseButton = new Button { Text = "케이스 ▶▶" };
        _caseButton.AddThemeFontOverride("font", font);
        _caseButton.AddThemeFontSizeOverride("font_size", 15);
        _caseButton.Pressed += () => LoadCase((_caseIndex + 1) % Cases.Length);
        row.AddChild(_caseButton);
    }

    private static Label MakeLabel(Font font, int size, Color color)
    {
        var label = new Label();
        label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    private void OnStep()
    {
        if (_animating || _index >= _ticks.Count)
        {
            return;
        }

        var tick = _ticks[_index++];
        _animating = true;

        if (tick.Day != _lastDayLogged)
        {
            _lastDayLogged = tick.Day;
            AppendLog($"── {tick.Day}일차 ──");
        }

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

        AnnotateFire(tick);

        if (_index >= _ticks.Count)
        {
            AppendLog($"■ 진행 종료 — {ReasonText(_reason)}");
        }

        var clock = CreateTween();
        clock.TweenInterval(StepSeconds + 0.05f);
        clock.Finished += () =>
        {
            _animating = false;
            UpdateButtons();
        };

        UpdateButtons();
    }

    // 행군 유닛이 적 사거리 안에 들면 "일방 피해" 안내(전투 페이즈 소관 — 여기선 표시만).
    private void AnnotateFire(MovementTick tick)
    {
        foreach (var u in tick.Units.Where(u => u.Mode == UnitMode.March && u.Target is not null))
        {
            var shooter = tick.Units.FirstOrDefault(v =>
                v.Owner != u.Owner && u.Position.Distance(v.Position) <= v.AttackRange);
            var underFire = shooter is not null;
            if (underFire && _underFire.Add(u.Id.Value))
            {
                AppendLog($"▲ A{u.Id.Value} 적 사거리 통과 — 전투 시 70% 일방 피해(반격 없음)");
            }
            else if (!underFire)
            {
                _underFire.Remove(u.Id.Value);
            }
        }
    }

    private void UpdateButtons()
    {
        var done = _index >= _ticks.Count;
        _stepButton.Disabled = _animating || done;
        _stepButton.Text = done ? $"완료 ({_ticks.Count}스텝)" : $"진행 ▶ ({_index + 1}/{_ticks.Count})";
        _caseButton.Disabled = _animating;
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
