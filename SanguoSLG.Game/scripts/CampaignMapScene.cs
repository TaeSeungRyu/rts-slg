using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.AI;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 아주 간단한 캠페인 맵 — 관전 테스트(13단계 1차). 작은 평지 맵 위 두 세력이 세력 AI로 스스로
/// 싸우는 것을 3D로 지켜본다(콘솔 <c>--watch</c>의 시각판). 성은 성 모델+라벨(세력색), 야전 부대는
/// 색 마커로 표시하고, "진행(주)" 버튼마다 <see cref="CampaignEngine"/>+<see cref="FactionAI"/>를 돌려
/// 다시 그린다. Core를 호출·반영만 한다(노드에 규칙 없음 — CLAUDE.md).
/// </summary>
public sealed partial class CampaignMapScene : Node3D
{
    private static readonly Color Blue = new(0.24f, 0.44f, 0.86f);
    private static readonly Color Red = new(0.82f, 0.22f, 0.18f);

    private MapView3D _view = null!;
    private FactionAI _ai = null!;
    private CampaignEngine _engine = null!;
    private GameState _state = null!;
    private int _week;

    private readonly Dictionary<int, Label3D> _cityLabels = new();
    private readonly Dictionary<int, UnitController3D> _armyTokens = new();
    private readonly Dictionary<int, Label3D> _armyLabels = new();
    private Label _status = null!;
    private Label _log = null!;

    public void Build(MapView3D view, CameraController3D camera, string dataDirectory)
    {
        _view = view;

        var troops = new TroopTypeLoader().LoadFromDirectory(dataDirectory);
        var commandBalance = new CommandBalanceLoader().LoadFromDirectory(dataDirectory);
        var actives = new ActiveSkillLoader().LoadFromDirectory(dataDirectory);
        var passives = new PassiveSkillLoader().LoadFromDirectory(dataDirectory);
        var balance = new BalanceConfig(MonthlyTaxPerCity: 100);

        _ai = new FactionAI(new CommandService(commandBalance, troops, balance),
            new DeployService(commandBalance, troops, actives, passives));
        var movement = new MovementSimulator(new PassabilityMap(_map, [], _cities));
        var world = new WorldEngine(balance, commandBalance);
        _engine = new CampaignEngine(
            new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), 70)),
            world,
            new CampaignSiege(new BattleResolver(60), troops),
            new CityCapture(), new SeededRandomSource(42),
            new CityPlunder(commandBalance));
        _state = _initial;

        SpawnCastles();
        BuildHud();
        camera.Setup(_view.HexToWorld(new HexCoord(4, 2)), 12f);
        Redraw("성을 세우고 진행을 눌러 관전하세요.");
    }

    // ── 아주 간단한 시나리오(코드): 평지 10x6, 두 세력, 각 성 1개·장수 2명·대기 병력 1만 ──
    private static readonly HexMap _map = new(0, 9, 0, 5);

    private static readonly IReadOnlyList<City> _cities = new List<City>
    {
        new(new CityId(1), "장안", new HexCoord(1, 2), new FactionId(1), 3000, CastleSize.Medium,
            Gold: 2000, Security: 80, Population: 100_000, Ore: 8000, Wall: 1200),
        new(new CityId(2), "성도", new HexCoord(8, 3), new FactionId(2), 3000, CastleSize.Medium,
            Gold: 2000, Security: 80, Population: 100_000, Ore: 8000, Wall: 1200),
    };

    private static readonly GameState _initial = new(1, 1,
        new List<Faction>
        {
            new(new FactionId(1), "위", new GeneralId(1), 0, "#3d70dc"),
            new(new FactionId(2), "촉", new GeneralId(3), 0, "#d23830"),
        },
        _cities.ToList(),
        new List<General>
        {
            Officer(1), Officer(2), Officer(3), Officer(4),
        },
        Postings: new List<GeneralPosting>
        {
            new(new GeneralId(1), new FactionId(1), new CityId(1)),
            new(new GeneralId(2), new FactionId(1), new CityId(1)),
            new(new GeneralId(3), new FactionId(2), new CityId(2)),
            new(new GeneralId(4), new FactionId(2), new CityId(2)),
        },
        GarrisonForces: new List<GarrisonForce>
        {
            new(new CityId(1), "swordsman", 10000, 60),
            new(new CityId(2), "swordsman", 10000, 60),
        });

    private static General Officer(int id) => new(
        new GeneralId(id), $"장수{id}",
        new Dictionary<TroopClass, AptitudeGrade> { [TroopClass.Infantry] = AptitudeGrade.A },
        Might: 70, Intellect: 60, Politics: 70);

    private void SpawnCastles()
    {
        foreach (var city in _cities)
        {
            var node = GD.Load<PackedScene>("res://assets/models/castle-small.glb").Instantiate<Node3D>();
            node.Position = _view.HexToWorld(city.Position) + new Vector3(0f, _view.TileTopY, 0f);
            AddChild(node);

            var label = new Label3D
            {
                Position = _view.HexToWorld(city.Position) + new Vector3(0f, _view.TileTopY + 1.4f, 0f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 48,
                OutlineSize = 12,
                NoDepthTest = true,
            };
            AddChild(label);
            _cityLabels[city.Id.Value] = label;
        }
    }

    private void OnAdvance()
    {
        foreach (var f in _state.Factions.OrderBy(f => f.Id.Value))
        {
            _state = _ai.PlanWeek(_state, f.Id);
        }

        _state = _engine.AdvanceWeek(_state, out _, out var sieges, out var captures, out var plunders);
        _week++;

        var note = new List<string>();
        if (sieges.Count > 0) { note.Add($"공성 {sieges.Count}"); }
        if (plunders.Count > 0) { note.Add($"약탈 {plunders.Count}"); }
        foreach (var c in captures)
        {
            var name = _cities.First(x => x.Id == c.City).Name;
            var owner = _state.Factions.First(f => f.Id == c.NewOwner).Name;
            note.Add($"★{name}→{owner}{(c.FactionEliminated ? "(멸망)" : "")}");
        }

        Redraw(note.Count > 0 ? string.Join(" · ", note) : "—");

        var alive = _state.Factions.Where(f => _state.CityCount(f.Id) > 0).ToList();
        if (alive.Count <= 1)
        {
            _log.Text = $"[종료] {(alive.Count == 1 ? alive[0].Name + " 통일" : "무승부")} (주 {_week})";
        }
    }

    private void Redraw(string note)
    {
        // 성 라벨·색 갱신.
        foreach (var city in _state.Cities)
        {
            var color = city.Owner.Value == 1 ? Blue : Red;
            var troops = _state.Garrisons.Where(g => g.City == city.Id).Sum(g => g.Troops);
            var label = _cityLabels[city.Id.Value];
            label.Text = $"{city.Name} [{_state.Factions.First(f => f.Id == city.Owner).Name}]\n성벽 {city.Wall}  병 {troops}";
            label.Modulate = color;
        }

        // 야전 부대 = 실제 유닛 모델(UnitController3D). 부대 id별로 토큰을 만들고 위치·병력 라벨을 갱신,
        // 사라진 부대(입성·전멸·함락)의 토큰은 제거한다.
        var alive = _state.Armies.Select(u => u.Id.Value).ToHashSet();
        foreach (var id in _armyTokens.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            _armyTokens[id].QueueFree();
            _armyLabels[id].QueueFree();
            _armyTokens.Remove(id);
            _armyLabels.Remove(id);
        }

        foreach (var army in _state.Armies)
        {
            var color = army.Field.Owner.Value == 1 ? Blue : Red;
            if (!_armyTokens.TryGetValue(army.Id.Value, out var token))
            {
                token = new UnitController3D();
                AddChild(token);
                token.InitDisplay(_view, color, troopIndex: 0, army.Field.Position); // 0 = 도검병

                var lbl = new Label3D
                {
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    FontSize = 36,
                    OutlineSize = 10,
                    NoDepthTest = true,
                    Modulate = color,
                };
                AddChild(lbl);
                _armyTokens[army.Id.Value] = token;
                _armyLabels[army.Id.Value] = lbl;
            }

            token.DisplayStepTo(army.Field.Position, 0.3f);
            var lblNode = _armyLabels[army.Id.Value];
            lblNode.Position = _view.HexToWorld(army.Field.Position) + new Vector3(0f, _view.TileTopY + 1.1f, 0f);
            lblNode.Text = $"{army.Pool.Active}";
        }

        var counts = _state.Factions.OrderBy(f => f.Id.Value).Select(f =>
        {
            var cities = _state.CityCount(f.Id);
            var troops = _state.Garrisons.Where(g => _state.Cities.Any(c => c.Id == g.City && c.Owner == f.Id)).Sum(g => g.Troops)
                + _state.Armies.Where(u => u.Field.Owner == f.Id).Sum(u => u.Pool.Active);
            return $"{f.Name} 성{cities} 병{troops}";
        });
        _status.Text = $"주 {_week}   {string.Join("   |   ", counts)}";
        _log.Text = note;
    }

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        layer.AddChild(panel);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        panel.AddChild(box);

        _status = new Label();
        _status.AddThemeFontSizeOverride("font_size", 20);
        box.AddChild(_status);
        _log = new Label();
        _log.AddThemeFontSizeOverride("font_size", 15);
        box.AddChild(_log);

        var advance = new Button { Text = "진행 (7일)", CustomMinimumSize = new Vector2(150, 40) };
        advance.Pressed += OnAdvance;
        box.AddChild(advance);
    }
}
