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
/// 간단한 캠페인 맵(13단계). 작은 평지 맵 위 **플레이어 세력(위)은 직접 조작**, 적(촉)은 세력 AI.
/// 자기 성을 클릭하면 내정 명령 패널(모병·세율·연구·성벽수리·도시계략 + 컨펌)이 뜨고, "진행(주)"
/// 버튼이 플레이어 명령과 적 AI를 함께 정산한다. 성은 성 모델+세력색 라벨, 야전 부대는 유닛 모델.
/// Core(<see cref="CampaignEngine"/>·<see cref="CommandService"/>·<see cref="FactionAI"/>)를 호출·반영만
/// 한다(노드에 규칙 없음 — CLAUDE.md). 출전(부대 편성)·부대 조작은 2단계.
/// </summary>
public sealed partial class CampaignMapScene : Node3D
{
    private static readonly Color Blue = new(0.24f, 0.44f, 0.86f);
    private static readonly Color Red = new(0.82f, 0.22f, 0.18f);
    private static readonly FactionId Player = new(1); // 위 = 플레이어, 나머지는 AI

    // 삼국지풍 팔레트(먹빛 패널 + 금색 테두리·글자).
    private static readonly Color Ink = new(0.07f, 0.075f, 0.09f, 0.96f);   // 패널 바탕
    private static readonly Color InkSoft = new(0.13f, 0.13f, 0.15f);        // 버튼 바탕
    private static readonly Color InkHover = new(0.22f, 0.19f, 0.14f);       // 버튼 hover
    private static readonly Color Gold = new(0.80f, 0.66f, 0.36f);           // 테두리·제목
    private static readonly Color GoldBright = new(0.96f, 0.82f, 0.48f);     // 강조
    private static readonly Color Parchment = new(0.90f, 0.88f, 0.82f);      // 본문 글자
    private static readonly Color AccentFill = new(0.46f, 0.35f, 0.15f);     // 실행 버튼 바탕

    private Font _font = null!;

    private MapView3D _view = null!;
    private CameraController3D _camera = null!;
    private FactionAI _ai = null!;
    private CampaignEngine _engine = null!;
    private CommandService _commander = null!;
    private IReadOnlyList<TroopTemplate> _troops = null!;
    private CommandBalance _cb = null!;
    private GameState _state = null!;
    private int _week;

    private readonly Dictionary<int, Label3D> _cityLabels = new();
    private readonly Dictionary<int, UnitController3D> _armyTokens = new();
    private readonly Dictionary<int, Label3D> _armyLabels = new();
    private Label _status = null!;
    private Label _log = null!;

    // 명령 UX(성 클릭 → 정보 카드 + 명령 목록 → 파라미터·장수 목록 → 컨펌).
    private CityId? _selected;
    private int _cmdIndex = -1;
    private PanelContainer _infoCard = null!;
    private Label _infoText = null!;
    private PanelContainer _cmdMenu = null!;
    private VBoxContainer _cmdList = null!;
    private PanelContainer _detail = null!;
    private VBoxContainer _detailBody = null!;
    private OptionButton? _paramSel;
    private ConfirmationDialog _confirm = null!;
    private System.Action? _onConfirm;
    private MeshInstance3D? _ring;
    private MeshInstance3D _hover = null!;
    private ImageTexture _blankIcon = null!;
    private ImageTexture _dotIcon = null!;

    // 1단계 지원 명령(내정 — 출전은 2단계). (표시명, 종류, 파라미터: troop/tax/wall/stratagem/none)
    private static readonly (string Label, CommandKind Kind, string Param)[] Cmds =
    {
        ("모병", CommandKind.Recruit, "troop"),
        ("세율", CommandKind.SetTaxRate, "tax"),
        ("병종 연구", CommandKind.Research, "troop"),
        ("성벽 수리", CommandKind.Repair, "wall"),
        ("도시 계략", CommandKind.CityStratagem, "stratagem"),
    };

    private static readonly (string Label, string Code)[] Strats =
    {
        ("정찰", "scout"), ("성벽파괴", "wall_break"), ("선동", "incite"),
        ("방화", "arson"), ("절취", "steal"), ("이간", "sow_discord"),
    };

    // 명령 카테고리 그룹(삼국지14식 분류)과 명령별 아이콘.
    private static readonly (string Group, int[] Indices)[] CmdGroups =
    {
        ("내정", new[] { 0, 1, 2, 3 }),
        ("계략", new[] { 4 }),
    };

    private static readonly Sym[] CmdIcons = { Sym.Sword, Sym.Coin, Sym.Book, Sym.Wall, Sym.Scroll };

    public void Build(MapView3D view, CameraController3D camera, string dataDirectory)
    {
        _view = view;
        _camera = camera;
        _font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf");

        _troops = new TroopTypeLoader().LoadFromDirectory(dataDirectory);
        _cb = new CommandBalanceLoader().LoadFromDirectory(dataDirectory);
        var actives = new ActiveSkillLoader().LoadFromDirectory(dataDirectory);
        var passives = new PassiveSkillLoader().LoadFromDirectory(dataDirectory);
        var balance = new BalanceConfig(MonthlyTaxPerCity: 100);

        _commander = new CommandService(_cb, _troops, balance);
        _ai = new FactionAI(_commander, new DeployService(_cb, _troops, actives, passives));
        var movement = new MovementSimulator(new PassabilityMap(_map, [], _cities));
        var world = new WorldEngine(balance, _cb);
        _engine = new CampaignEngine(
            new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), 70)),
            world,
            new CampaignSiege(new BattleResolver(60), _troops),
            new CityCapture(), new SeededRandomSource(42),
            new CityPlunder(_cb));
        _state = _initial;

        _blankIcon = SolidIcon(1, (_, _) => new Color(0, 0, 0, 0));
        _dotIcon = SolidIcon(14, (x, y) => System.Math.Abs(x - 7) + System.Math.Abs(y - 7) <= 4 ? GoldBright : new Color(0, 0, 0, 0));

        SpawnCastles();
        SpawnHover();
        BuildHud();
        BuildPanel();
        camera.Setup(_view.HexToWorld(new HexCoord(4, 2)), 12f);
        Redraw("자기 성(파란색)을 클릭해 명령을 내리세요. 적(촉)은 AI입니다.");
    }

    // 마우스 밑 타일에 금색 반투명 육각(이동/전투 씬의 호버 육각과 같은 표현).
    private void SpawnHover()
    {
        _hover = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = _view.HexWorldSize * 0.94f,
                BottomRadius = _view.HexWorldSize * 0.94f,
                Height = 0.04f,
                RadialSegments = 6,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.92f, 0.55f, 0.28f),
                EmissionEnabled = true,
                Emission = new Color(0.6f, 0.52f, 0.25f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                NoDepthTest = true,
            },
        };
        AddChild(_hover);
    }

    // 단색 아이콘 텍스처 생성(라디오 대체용) — (x,y)→색 함수로 채운다.
    private static ImageTexture SolidIcon(int size, System.Func<int, int, Color> pixel)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                img.SetPixel(x, y, pixel(x, y));
            }
        }

        return ImageTexture.CreateFromImage(img);
    }

    // ── 명령 아이콘(코드 생성) — 삼국지14/콜오브드래곤즈처럼 명령마다 표식 ──
    private enum Sym { Sword, Coin, Book, Wall, Scroll, Grain, Flag }

    private readonly Dictionary<Sym, ImageTexture> _icons = new();

    private ImageTexture Icon(Sym s)
    {
        if (_icons.TryGetValue(s, out var c)) { return c; }

        const int N = 22;
        var img = Image.CreateEmpty(N, N, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        void Rect(int x0, int y0, int x1, int y1, Color col)
        {
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    if (x >= 0 && x < N && y >= 0 && y < N) { img.SetPixel(x, y, col); }
                }
            }
        }

        void Disc(float cx, float cy, float r, Color col)
        {
            for (var y = 0; y < N; y++)
            {
                for (var x = 0; x < N; x++)
                {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) { img.SetPixel(x, y, col); }
                }
            }
        }

        void Diamond(int cx, int cy, int r, Color col)
        {
            for (var y = 0; y < N; y++)
            {
                for (var x = 0; x < N; x++)
                {
                    if (System.Math.Abs(x - cx) + System.Math.Abs(y - cy) <= r) { img.SetPixel(x, y, col); }
                }
            }
        }

        var steel = new Color(0.80f, 0.84f, 0.90f);
        var stone = new Color(0.66f, 0.66f, 0.68f);
        var tan = new Color(0.82f, 0.72f, 0.48f);
        switch (s)
        {
            case Sym.Sword: // 칼: 강철 날 + 금색 코등이·자루
                Rect(10, 2, 12, 13, steel);
                Rect(6, 13, 16, 14, Gold);
                Rect(10, 14, 12, 19, Gold);
                break;
            case Sym.Coin: // 금화
                Disc(11, 11, 8.5f, Gold);
                Disc(11, 11, 5.5f, GoldBright);
                break;
            case Sym.Book: // 서책
                Rect(4, 4, 18, 18, tan);
                Rect(10, 4, 12, 18, new Color(0.55f, 0.45f, 0.26f));
                break;
            case Sym.Wall: // 성벽(총안)
                Rect(3, 10, 19, 18, stone);
                Rect(3, 5, 7, 10, stone);
                Rect(10, 5, 12, 10, stone);
                Rect(15, 5, 19, 10, stone);
                break;
            case Sym.Scroll: // 계략(두루마리)
                Rect(5, 3, 17, 19, tan);
                Rect(5, 7, 17, 8, new Color(0.55f, 0.45f, 0.26f));
                Rect(5, 12, 17, 13, new Color(0.55f, 0.45f, 0.26f));
                break;
            case Sym.Grain: // 군량(낟알)
                Diamond(11, 11, 8, new Color(0.90f, 0.78f, 0.42f));
                break;
            case Sym.Flag: // 성/세력(깃발)
                Rect(6, 3, 7, 19, Gold);
                Rect(7, 4, 17, 11, GoldBright);
                break;
        }

        var tex = ImageTexture.CreateFromImage(img);
        _icons[s] = tex;
        return tex;
    }

    // 좌클릭 → 지면 헥사 → 그 칸의 성. 내 성이면 명령 패널, 아니면 닫는다.
    public override void _UnhandledInput(InputEvent @event)
    {
        // 마우스 오버: 밑 타일에 호버 육각.
        if (@event is InputEventMouseMotion motion)
        {
            if (RayToGround(motion.Position) is { } hoverHex)
            {
                _hover.Visible = true;
                _hover.Position = _view.HexToWorld(hoverHex) + new Vector3(0f, _view.TileTopY + 0.02f, 0f);
            }
            else
            {
                _hover.Visible = false;
            }

            return;
        }

        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            return;
        }

        var hex = RayToGround(click.Position);
        var city = hex is { } h ? _state.Cities.FirstOrDefault(c => c.Position == h) : null;
        if (city is not null && city.Owner == Player)
        {
            SelectCity(city.Id);
        }
        else
        {
            _selected = null;
            HidePanels();
        }
    }

    private HexCoord? RayToGround(Vector2 screen)
    {
        var origin = _camera.ProjectRayOrigin(screen);
        var dir = _camera.ProjectRayNormal(screen);
        if (Mathf.Abs(dir.Y) < 0.0001f)
        {
            return null;
        }

        var t = -origin.Y / dir.Y;
        if (t <= 0f)
        {
            return null;
        }

        var coord = _view.WorldToHex(origin + dir * t);
        return _map.Contains(coord) ? coord : null;
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
        // 플레이어 세력은 직접 조작 — AI는 나머지 세력만 굴린다.
        foreach (var f in _state.Factions.Where(f => f.Id != Player).OrderBy(f => f.Id.Value))
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

        // 선택 성이 아직 내 것이면 패널 갱신, 아니면 닫는다.
        if (_selected is { } sel && _state.Cities.Any(c => c.Id == sel && c.Owner == Player))
        {
            SelectCity(sel);
        }
        else
        {
            _selected = null;
            HidePanels();
        }
    }

    // ── 명령 UX(성 클릭) — 삼국지풍: 정보 카드 + 명령 목록 → 파라미터·장수 목록 ──
    private void BuildPanel()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        // 우상단: 선택 성 정보 카드.
        _infoCard = Card(layer, Control.LayoutPreset.TopRight, new Vector2(-360, 16), 344);
        var info = (VBoxContainer)_infoCard.GetChild(0);
        info.AddChild(Header("◈ 성 정보"));
        _infoText = MakeLabel("", 15, Parchment);
        _infoText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        info.AddChild(_infoText);

        // 좌하단: 명령 목록(카테고리 그룹 + 아이콘 버튼 — 삼국지14/콜오브드래곤즈풍).
        _cmdMenu = Card(layer, Control.LayoutPreset.BottomLeft, new Vector2(20, -390), 210);
        var menu = (VBoxContainer)_cmdMenu.GetChild(0);
        menu.AddChild(Header("◈ 명 령"));
        _cmdList = new VBoxContainer();
        _cmdList.AddThemeConstantOverride("separation", 5);
        menu.AddChild(_cmdList);
        foreach (var (group, indices) in CmdGroups)
        {
            _cmdList.AddChild(MakeLabel($"— {group} —", 12, GoldBright));
            foreach (var i in indices)
            {
                var idx = i;
                var btn = MakeButton("  " + Cmds[i].Label);
                btn.Icon = Icon(CmdIcons[i]);
                btn.ExpandIcon = false;
                btn.Alignment = HorizontalAlignment.Left;
                btn.AddThemeConstantOverride("h_separation", 10);
                btn.CustomMinimumSize = new Vector2(186, 40);
                btn.Pressed += () => ShowDetail(idx);
                _cmdList.AddChild(btn);
            }
        }

        // 명령 목록 오른쪽: 파라미터 + 장수 목록(클릭 = 실행).
        _detail = Card(layer, Control.LayoutPreset.BottomLeft, new Vector2(214, -360), 260);
        _detailBody = (VBoxContainer)_detail.GetChild(0);

        _confirm = new ConfirmationDialog { Title = "명령 확인" };
        _confirm.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 8, 16));
        _confirm.Confirmed += () => _onConfirm?.Invoke();
        layer.AddChild(_confirm);

        HidePanels();
    }

    // 먹빛·금테 카드(내부 VBox 반환은 GetChild(0)). 앵커·오프셋·최소폭 지정.
    private PanelContainer Card(CanvasLayer layer, Control.LayoutPreset preset, Vector2 offset, int width)
    {
        var card = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(width, 0) };
        card.SetAnchorsPreset(preset);
        card.Position = offset;
        card.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 10, 14));
        layer.AddChild(card);
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        card.AddChild(box);
        return card;
    }

    private Control Header(string text)
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 4);
        v.AddChild(MakeLabel(text, 18, Gold));
        var rule = new HSeparator();
        rule.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = new Color(Gold, 0.5f), ContentMarginTop = 1, ContentMarginBottom = 1 });
        v.AddChild(rule);
        return v;
    }

    private void HidePanels()
    {
        _infoCard.Visible = false;
        _cmdMenu.Visible = false;
        _detail.Visible = false;
        if (_ring is not null) { _ring.Visible = false; }
    }

    private void SelectCity(CityId id)
    {
        _selected = id;
        _cmdIndex = -1;
        var c = _state.Cities.First(x => x.Id == id);
        var troops = _state.Garrisons.Where(g => g.City == id)
            .Select(g => $"{g.TroopCode} {g.Troops}");
        var officers = _state.GeneralsAt(id).Select(g => _state.Generals.First(x => x.Id == g).Name);
        var pending = _state.Commands.Where(p => p.City == id).Select(p =>
            $"{KindName(p.Kind)} (남은 {p.CompletionDay - _state.Day}일)");
        var facilities = $"논{c.Paddies} 밭{c.Farms} 마을{c.Villages}{(c.Workshop ? " 공방" : "")}";

        _infoText.Text =
            $"《 {c.Name} 》\n" +
            $"금 {c.Gold}   군량 {c.Provisions}\n" +
            $"인구 {c.Population}   치안 {c.Security}   세율 {c.TaxRate}%\n" +
            $"성벽 {c.Wall}   광석 {c.Ore} 말 {c.Horses} 코끼리 {c.Elephants}\n" +
            $"시설 {facilities}\n" +
            $"대기 병력: {(troops.Any() ? string.Join(", ", troops) : "없음")}\n" +
            $"주둔 장수: {(officers.Any() ? string.Join(", ", officers) : "없음")}" +
            (pending.Any() ? $"\n진행중: {string.Join(", ", pending)}" : "");

        _infoCard.Visible = true;
        _cmdMenu.Visible = true;
        _detail.Visible = false;
        MoveRing(c.Position);
    }

    // 명령 클릭 → 파라미터 컨트롤 + 수행 장수 목록(클릭 = 실행). 계단식.
    private void ShowDetail(int cmdIndex)
    {
        if (_selected is not { } city)
        {
            return;
        }

        _cmdIndex = cmdIndex;
        var cmd = Cmds[cmdIndex];
        Clear(_detailBody);
        _paramSel = null;

        _detailBody.AddChild(Header($"◈ {cmd.Label}"));

        if (cmd.Param != "wall")
        {
            _paramSel = MakeOption(220);
            switch (cmd.Param)
            {
                case "troop": foreach (var t in _troops) { _paramSel.AddItem(t.Name); } break;
                case "tax":
                    foreach (var v in new[] { 0, 10, 20, 30, 40, 50 }) { _paramSel.AddItem($"세율 {v}%"); }
                    _paramSel.Select(2);
                    break;
                case "stratagem": foreach (var s in Strats) { _paramSel.AddItem(s.Label); } break;
            }

            _detailBody.AddChild(_paramSel);
        }

        _detailBody.AddChild(MakeLabel("수행 장수 (클릭 = 실행)", 13, GoldBright));
        var free = _state.GeneralsAt(city).Where(g => !_state.IsGeneralBusy(g)).OrderBy(g => g.Value).ToList();
        if (free.Count == 0)
        {
            _detailBody.AddChild(MakeLabel("(가능한 장수 없음)", 14, Parchment));
        }

        var cityData = _state.Cities.First(x => x.Id == city);
        foreach (var gid in free)
        {
            var g = _state.Generals.First(x => x.Id == gid);
            var home = g.Region.Length > 0 && g.Region == cityData.Region ? " 🏠" : "";
            var stat = cmd.Kind == CommandKind.Research || cmd.Kind == CommandKind.CityStratagem
                ? $"지{g.Intellect}" : cmd.Kind == CommandKind.Train ? $"무{g.Might}" : $"정{g.Politics}";
            var btn = MakeButton($"{g.Name}  {stat}{home}");
            btn.CustomMinimumSize = new Vector2(230, 32);
            var captured = gid;
            btn.Pressed += () => AskExecute(city, cmdIndex, captured);
            _detailBody.AddChild(btn);
        }

        _detail.Visible = true;
    }

    private void AskExecute(CityId city, int cmdIndex, GeneralId general)
    {
        var cmd = Cmds[cmdIndex];
        var p = System.Math.Max(0, _paramSel?.Selected ?? 0);
        var troopCode = cmd.Param == "troop" ? _troops[p].Code : cmd.Param == "wall" ? FactionResearch.WallCode : "";
        var facility = cmd.Param == "stratagem" ? Strats[p].Code : "";
        var value = cmd.Param == "tax" ? p * 10 : 0;

        CityId? target = null;
        var extra = "";
        if (cmd.Param == "stratagem")
        {
            var enemy = _state.Cities.FirstOrDefault(c => c.Owner != Player);
            if (enemy is null) { return; }
            target = enemy.Id;
            var caster = _state.Generals.First(g => g.Id == general);
            var days = CityStratagems.Days(_state.Cities.First(c => c.Id == city).Position, enemy.Position, _cb);
            var defInt = enemy.Governor is { } gid ? _state.Generals.FirstOrDefault(g => g.Id == gid)?.Intellect : null;
            extra = $"\n대상 {enemy.Name} · 소요 {days}일 · 성공률 {CityStratagems.SuccessPercent(caster.Intellect, defInt)}%";
        }

        var request = new CommandRequest(city, cmd.Kind, general, Value: value, Facility: facility,
            TroopCode: troopCode, TargetCity: target);
        var gName = _state.Generals.First(g => g.Id == general).Name;
        var pLabel = cmd.Param switch
        {
            "troop" => $" · {_troops[p].Name}",
            "tax" => $" · {value}%",
            "stratagem" => $" · {Strats[p].Label}",
            _ => "",
        };
        _confirm.DialogText = $"{_state.Cities.First(c => c.Id == city).Name} — {cmd.Label}{pLabel}{extra}\n수행 장수: {gName}\n\n실행하시겠습니까?";
        _onConfirm = () =>
        {
            var r = _commander.Issue(_state, request);
            if (r.Ok) { _state = r.State; }
            _log.Text = r.Ok ? $"발행: {cmd.Label}{pLabel} — {gName}" : $"실패: {r.Error}";
            SelectCity(city);
            Redraw(_log.Text);
        };
        _confirm.PopupCentered();
    }

    private void MoveRing(HexCoord at)
    {
        if (_ring is null)
        {
            _ring = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 0.42f, OuterRadius = 0.52f },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = GoldBright,
                    EmissionEnabled = true,
                    Emission = Gold,
                    EmissionEnergyMultiplier = 1.6f,
                },
            };
            AddChild(_ring);
        }

        _ring.Visible = true;
        _ring.Position = _view.HexToWorld(at) + new Vector3(0f, _view.TileTopY + 0.06f, 0f);
    }

    private static void Clear(Node parent)
    {
        foreach (var child in parent.GetChildren())
        {
            child.QueueFree();
        }
    }

    private static string KindName(CommandKind k) => k switch
    {
        CommandKind.Recruit => "모병",
        CommandKind.Conscript => "징병",
        CommandKind.Train => "훈련",
        CommandKind.Build => "건설",
        CommandKind.SetTaxRate => "세율",
        CommandKind.Research => "연구",
        CommandKind.Repair => "수리",
        CommandKind.CityStratagem => "계략",
        _ => k.ToString(),
    };

    // ── 게임풍 스타일 헬퍼 ──
    private static StyleBoxFlat Frame(Color bg, Color border, int borderW, int radius, int pad)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        s.SetBorderWidthAll(borderW);
        s.SetCornerRadiusAll(radius);
        s.ContentMarginLeft = s.ContentMarginRight = pad;
        s.ContentMarginTop = s.ContentMarginBottom = (int)(pad * 0.75f);
        return s;
    }

    private Label MakeLabel(string text, int size, Color color)
    {
        var l = new Label { Text = text };
        l.AddThemeFontOverride("font", _font);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color);
        return l;
    }

    private Button MakeButton(string text, bool accent = false)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, 36) };
        b.AddThemeFontOverride("font", _font);
        b.AddThemeFontSizeOverride("font_size", 15);
        b.AddThemeColorOverride("font_color", accent ? Ink : Parchment);
        b.AddThemeColorOverride("font_hover_color", accent ? Ink : GoldBright);
        b.AddThemeColorOverride("font_pressed_color", GoldBright);
        b.AddThemeStyleboxOverride("normal", Frame(accent ? AccentFill : InkSoft, Gold, 1, 5, 10));
        b.AddThemeStyleboxOverride("hover", Frame(accent ? GoldBright : InkHover, GoldBright, 1, 5, 10));
        b.AddThemeStyleboxOverride("pressed", Frame(AccentFill, Gold, 1, 5, 10));
        b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        return b;
    }

    private OptionButton MakeOption(int width)
    {
        var o = new OptionButton { CustomMinimumSize = new Vector2(width, 36) };
        o.AddThemeFontOverride("font", _font);
        o.AddThemeFontSizeOverride("font_size", 14);
        o.AddThemeColorOverride("font_color", Parchment);
        o.AddThemeColorOverride("font_hover_color", GoldBright);
        o.AddThemeStyleboxOverride("normal", Frame(InkSoft, Gold, 1, 5, 8));
        o.AddThemeStyleboxOverride("hover", Frame(InkHover, GoldBright, 1, 5, 8));
        o.AddThemeStyleboxOverride("pressed", Frame(InkHover, Gold, 1, 5, 8));
        o.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

        // 펼친 목록(팝업)도 게임풍: 먹빛·금테, 라디오 표시 제거(선택 항목은 금색 마커).
        var popup = o.GetPopup();
        popup.AddThemeFontOverride("font", _font);
        popup.AddThemeFontSizeOverride("font_size", 14);
        popup.AddThemeColorOverride("font_color", Parchment);
        popup.AddThemeColorOverride("font_hover_color", GoldBright);
        popup.AddThemeColorOverride("font_accelerator_color", Gold);
        popup.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 6, 8));
        popup.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = new Color(Gold, 0.22f) });
        popup.AddThemeIconOverride("radio_checked", _dotIcon);   // 라디오 원 → 금색 마름모
        popup.AddThemeIconOverride("radio_unchecked", _blankIcon);
        popup.AddThemeIconOverride("checked", _dotIcon);
        popup.AddThemeIconOverride("unchecked", _blankIcon);
        return o;
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
        panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        panel.Position = new Vector2(20, 16);
        panel.CustomMinimumSize = new Vector2(560, 0);
        panel.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 10, 16));
        layer.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 12);
        box.AddChild(top);
        _status = MakeLabel("", 20, Gold);
        _status.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(_status);
        var advance = MakeButton("▶ 진행 (7일)", accent: true);
        advance.CustomMinimumSize = new Vector2(150, 40);
        advance.Pressed += OnAdvance;
        top.AddChild(advance);

        _log = MakeLabel("", 15, Parchment);
        _log.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_log);
    }
}
