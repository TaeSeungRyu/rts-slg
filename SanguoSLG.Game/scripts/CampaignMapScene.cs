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

    // 삼국지풍 팔레트(칠기 흑갈 바탕 + 주홍 강조 + 금테 + 양피지 글자).
    private static readonly Color Ink = new(0.10f, 0.055f, 0.05f, 0.97f);    // 패널 바탕(짙은 칠기 흑갈)
    private static readonly Color InkSoft = new(0.19f, 0.12f, 0.09f);        // 버튼·카드 바탕(짙은 갈)
    private static readonly Color InkHover = new(0.40f, 0.16f, 0.11f);       // hover(주홍갈)
    private static readonly Color Gold = new(0.82f, 0.67f, 0.36f);           // 테두리·제목
    private static readonly Color GoldBright = new(0.98f, 0.85f, 0.52f);     // 강조
    private static readonly Color Parchment = new(0.93f, 0.87f, 0.75f);      // 본문 글자(양피지)
    private static readonly Color AccentFill = new(0.60f, 0.16f, 0.12f);     // 선택·실행(朱)

    private Font _font = null!;

    private MapView3D _view = null!;
    private CameraController3D _camera = null!;
    private FactionAI _ai = null!;
    private DeployService _deployer = null!;
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
    private Control _infoCard = null!;
    private VBoxContainer _infoRows = null!;
    private PanelContainer _cmdMenu = null!;
    private VBoxContainer _cmdList = null!;
    private OptionButton? _paramSel;
    private ConfirmationDialog _confirm = null!;
    private System.Action? _onConfirm;
    private MeshInstance3D? _ring;
    private MeshInstance3D _hover = null!;
    private ImageTexture _blankIcon = null!;
    private ImageTexture _dotIcon = null!;

    // 명령 모달(명령 클릭 → 큰 창 + 아이콘 카드 그리드 → 카드 선택 → 장수 클릭 = 실행).
    private CanvasLayer? _modalLayer;
    private int _modalParam;
    private GridContainer _modalOfficers = null!;
    private Label _modalDetail = null!;
    private readonly List<PanelContainer> _optionCards = new();
    private readonly Dictionary<TroopClass, ImageTexture> _emblems = new();

    // 출전 모달 선택 상태.
    private string? _depTroop;
    private GeneralId? _depVan;
    private GeneralId? _depAdj;
    private readonly List<(PanelContainer Card, string Code)> _depTroopCards = new();
    private readonly List<(PanelContainer Card, GeneralId Id)> _depVanCards = new();
    private readonly List<(PanelContainer Card, GeneralId Id)> _depAdjCards = new();

    // 출전 대기열 — "진행" 시 일괄 시작(즉시 실행 아님).
    private readonly List<(DeployRequest Req, string Label)> _pendingDeploys = new();

    // 출전 모달(허브=예약 목록 / 편성 화면) + 수량/미리보기.
    private CityId _depModalCity;
    private int _depAmount;
    private int _depEditIndex = -1; // -1=신규 추가, ≥0=_pendingDeploys 해당 예약 수정
    private UnitMode _depMode = UnitMode.Advance;
    private HexCoord? _depTarget;
    private SpinBox? _depAmountSpin;
    private Label? _depPreview;
    private readonly List<(Button Btn, UnitMode Mode)> _depModeButtons = new();
    private int _depProvDays; // 출전 시 휴대할 군량 일수(슬라이더). 0이면 군량 없이 나감
    private HSlider? _depProvSlider;
    private Label? _depProvLabel;
    private int _provPer10kPerDay = 10; // 병력 1만당 하루 군량 소모(balance) — 일수↔군량 환산
    private string _dbgLog = ""; // 출전 디버그 로그 파일 경로(res://deploy-debug.log)

    private void Dbg(string msg)
    {
        try { System.IO.File.AppendAllText(_dbgLog, msg + "\n"); } catch { }
    }

    // 목표 지정 모드(지도 클릭으로 예약 부대의 목적지 설정).
    private bool _depTargeting;
    private int _depTargetIndex = -1;
    private int _depSelectedUnit = -1; // 허브에서 선택된 예약 부대(컨트롤 바 대상)
    private CanvasLayer? _targetHintLayer;

    // 모달 드래그.
    private bool _dragging;
    private Control? _dragPanel;
    private Vector2 _dragOffset;

    // 경로 프리뷰.
    private PassabilityMap _passability = null!;
    private readonly List<MeshInstance3D> _pathMarkers = new();
    private Mesh? _pathDotMesh;
    private Material? _pathDotMat;

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
        _provPer10kPerDay = balance.ProvisionsPer10kPerDay;

        _commander = new CommandService(_cb, _troops, balance);
        _deployer = new DeployService(_cb, _troops, actives, passives);
        _ai = new FactionAI(_commander, _deployer);
        _passability = new PassabilityMap(_map, [], _cities);
        var movement = new MovementSimulator(_passability);
        var world = new WorldEngine(balance, _cb);
        _engine = new CampaignEngine(
            new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), 70)),
            world,
            new CampaignSiege(new BattleResolver(60), _troops),
            new CityCapture(), new SeededRandomSource(42),
            new CityPlunder(_cb), _cb.CityResupplyRadius);
        _state = _initial;

        _dbgLog = ProjectSettings.GlobalizePath("res://deploy-debug.log");
        try { System.IO.File.WriteAllText(_dbgLog, "=== maptest deploy debug ===\n"); } catch { }
        GD.Print("[deploy-log] " + _dbgLog);

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

    // ── 아이콘(코드 생성) — 삼국지14/콜오브드래곤즈처럼 정보·명령마다 표식 ──
    private enum Sym { Sword, Coin, Book, Wall, Scroll, Grain, Flag, People, Shield, Ore, Officer }

    private readonly Dictionary<Sym, ImageTexture> _icons = new();

    // 심볼별 실제 이미지가 있으면 우선 사용(없으면 절차적). 파일: assets/icons/icon_*.png
    private static readonly Dictionary<Sym, string> SymFiles = new()
    {
        [Sym.Coin] = "res://assets/icons/icon_coin.png",
        [Sym.Sword] = "res://assets/icons/icon_sword.png",
        [Sym.Book] = "res://assets/icons/icon_book.png",
        [Sym.Wall] = "res://assets/icons/icon_wall.png",
        [Sym.Scroll] = "res://assets/icons/icon_scroll.png",
        [Sym.Grain] = "res://assets/icons/icon_grain.png",
        [Sym.People] = "res://assets/icons/icon_people.png",
        [Sym.Shield] = "res://assets/icons/icon_shield.png",
        [Sym.Ore] = "res://assets/icons/icon_ore.png",
        [Sym.Officer] = "res://assets/icons/icon_officer.png",
    };

    private ImageTexture Icon(Sym s)
    {
        if (_icons.TryGetValue(s, out var c)) { return c; }

        if (SymFiles.TryGetValue(s, out var file) && Godot.FileAccess.FileExists(file))
        {
            var loaded = Image.LoadFromFile(ProjectSettings.GlobalizePath(file));
            loaded.GenerateMipmaps();
            var lt = ImageTexture.CreateFromImage(loaded);
            _icons[s] = lt;
            return lt;
        }

        var img = NewBig();
        var steel = new Color(0.80f, 0.84f, 0.90f);
        var stone = new Color(0.66f, 0.66f, 0.68f);
        var tan = new Color(0.82f, 0.72f, 0.48f);
        var ink = new Color(0.55f, 0.45f, 0.26f);
        switch (s)
        {
            case Sym.Sword: // 칼: 강철 날 + 금색 코등이·자루
                RectU(img, 10, 2, 12, 13, steel);
                RectU(img, 6, 13, 16, 14, Gold);
                RectU(img, 10, 14, 12, 19, Gold);
                break;
            case Sym.Coin: // 금화
                DiscU(img, 11, 11, 8.5f, Gold);
                DiscU(img, 11, 11, 5.5f, GoldBright);
                GlossU(img, 8.5f, 8f, 4.5f, 0.5f);
                break;
            case Sym.Book: // 서책
                RectU(img, 4, 4, 18, 18, tan);
                RectU(img, 10, 4, 12, 18, ink);
                break;
            case Sym.Wall: // 성벽(총안)
                RectU(img, 3, 10, 19, 18, stone);
                RectU(img, 3, 5, 7, 10, stone);
                RectU(img, 10, 5, 12, 10, stone);
                RectU(img, 15, 5, 19, 10, stone);
                break;
            case Sym.Scroll: // 계략(두루마리)
                RectU(img, 5, 3, 17, 19, tan);
                RectU(img, 5, 7, 17, 8, ink);
                RectU(img, 5, 12, 17, 13, ink);
                break;
            case Sym.Grain: // 군량(낟알)
                DiamondU(img, 11, 11, 8, new Color(0.90f, 0.78f, 0.42f));
                GlossU(img, 9f, 8f, 4f, 0.4f);
                break;
            case Sym.Flag: // 성/세력(깃발)
                RectU(img, 6, 3, 7, 19, Gold);
                RectU(img, 7, 4, 17, 11, GoldBright);
                break;
            case Sym.People: // 인구(사람 둘)
                DiscU(img, 8, 8, 3.4f, tan);
                RectU(img, 5, 11, 11, 18, tan);
                DiscU(img, 15, 9, 2.8f, new Color(0.66f, 0.58f, 0.40f));
                RectU(img, 12, 12, 18, 18, new Color(0.66f, 0.58f, 0.40f));
                break;
            case Sym.Shield: // 치안(방패)
                RectU(img, 5, 3, 17, 5, new Color(0.42f, 0.62f, 0.46f));
                RectU(img, 5, 3, 7, 12, new Color(0.42f, 0.62f, 0.46f));
                RectU(img, 15, 3, 17, 12, new Color(0.42f, 0.62f, 0.46f));
                DiamondU(img, 11, 13, 6, new Color(0.42f, 0.62f, 0.46f));
                break;
            case Sym.Ore: // 광석(광물 덩이)
                DiamondU(img, 11, 12, 7, new Color(0.60f, 0.66f, 0.74f));
                DiscU(img, 9, 10, 2.2f, new Color(0.86f, 0.90f, 0.96f));
                GlossU(img, 9f, 10f, 3f, 0.5f);
                break;
            case Sym.Officer: // 장수 인물 배지(금테 원 + 인물 실루엣)
                DiscU(img, 11, 11, 10f, Gold);
                DiscU(img, 11, 11, 8.4f, new Color(0.16f, 0.15f, 0.14f));
                DiscU(img, 11, 8, 3.2f, Parchment);   // 머리
                RectU(img, 6, 12, 16, 19, Parchment);  // 어깨
                GlossU(img, 8f, 7.5f, 5f, 0.28f);
                break;
        }

        ShadeVertical(img);
        var tex = Shadowed(img);
        _icons[s] = tex;
        return tex;
    }

    // ── 코드 생성 아이콘 렌더러: 6배 슈퍼샘플(안티에일리어싱) + 음영·광택·드롭섀도우 + 밉맵 ──
    private const int IconUnits = 22;
    private const int IconScale = 6;
    private const int IconBig = IconUnits * IconScale;

    private static Image NewBig()
    {
        var img = Image.CreateEmpty(IconBig, IconBig, false, Image.Format.Rgba8);
        img.Fill(new Color(0, 0, 0, 0));
        return img;
    }

    // 알파 오버 합성.
    private static void BlendPix(Image img, int x, int y, Color src, float a)
    {
        if (a <= 0f || x < 0 || x >= IconBig || y < 0 || y >= IconBig) { return; }
        if (a > 1f) { a = 1f; }
        var d = img.GetPixel(x, y);
        var na = a + (d.A * (1f - a));
        if (na <= 0.0001f) { img.SetPixel(x, y, new Color(0, 0, 0, 0)); return; }
        var inv = d.A * (1f - a);
        img.SetPixel(x, y, new Color(
            ((src.R * a) + (d.R * inv)) / na,
            ((src.G * a) + (d.G * inv)) / na,
            ((src.B * a) + (d.B * inv)) / na, na));
    }

    private static void RectU(Image img, float x0, float y0, float x1, float y1, Color col)
    {
        var bx0 = (int)(x0 * IconScale);
        var bx1 = (int)(((x1 + 1) * IconScale) - 1);
        var by0 = (int)(y0 * IconScale);
        var by1 = (int)(((y1 + 1) * IconScale) - 1);
        for (var y = by0; y <= by1; y++)
        {
            for (var x = bx0; x <= bx1; x++) { BlendPix(img, x, y, col, 1f); }
        }
    }

    private static void DiscU(Image img, float cx, float cy, float r, Color col)
        => Radial(img, cx, cy, r, (x, y, cxB, cyB, rB) =>
        {
            var dd = System.MathF.Sqrt(((x - cxB) * (x - cxB)) + ((y - cyB) * (y - cyB)));
            return Mathf.Clamp(((rB - dd) / 1.7f) + 0.5f, 0f, 1f);
        }, col);

    private static void DiamondU(Image img, float cx, float cy, float r, Color col)
        => Radial(img, cx, cy, r, (x, y, cxB, cyB, rB) =>
        {
            var dd = System.MathF.Abs(x - cxB) + System.MathF.Abs(y - cyB);
            return Mathf.Clamp(((rB - dd) / 1.7f) + 0.5f, 0f, 1f);
        }, col);

    private static void Radial(Image img, float cx, float cy, float r,
        System.Func<int, int, float, float, float, float> coverage, Color col)
    {
        var cxB = (cx * IconScale) + (IconScale / 2f);
        var cyB = (cy * IconScale) + (IconScale / 2f);
        var rB = r * IconScale;
        var x0 = System.Math.Max(0, (int)(cxB - rB - 2));
        var x1 = System.Math.Min(IconBig - 1, (int)(cxB + rB + 2));
        var y0 = System.Math.Max(0, (int)(cyB - rB - 2));
        var y1 = System.Math.Min(IconBig - 1, (int)(cyB + rB + 2));
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++) { BlendPix(img, x, y, col, coverage(x, y, cxB, cyB, rB)); }
        }
    }

    // 둥근 표면 광택(이미 그려진 곳에만 부드러운 흰 하이라이트).
    private static void GlossU(Image img, float cx, float cy, float r, float peak)
    {
        var cxB = (cx * IconScale) + (IconScale / 2f);
        var cyB = (cy * IconScale) + (IconScale / 2f);
        var rB = r * IconScale;
        var x0 = System.Math.Max(0, (int)(cxB - rB));
        var x1 = System.Math.Min(IconBig - 1, (int)(cxB + rB));
        var y0 = System.Math.Max(0, (int)(cyB - rB));
        var y1 = System.Math.Min(IconBig - 1, (int)(cyB + rB));
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                if (img.GetPixel(x, y).A <= 0.01f) { continue; }
                var dd = System.MathF.Sqrt(((x - cxB) * (x - cxB)) + ((y - cyB) * (y - cyB)));
                if (dd >= rB) { continue; }
                var t = 1f - (dd / rB);
                BlendPix(img, x, y, new Color(1f, 1f, 1f), t * t * peak);
            }
        }
    }

    // 위→아래 밝기 기울기(입체감).
    private static void ShadeVertical(Image img)
    {
        for (var y = 0; y < IconBig; y++)
        {
            var f = Mathf.Lerp(1.14f, 0.80f, (float)y / (IconBig - 1));
            for (var x = 0; x < IconBig; x++)
            {
                var p = img.GetPixel(x, y);
                if (p.A <= 0f) { continue; }
                img.SetPixel(x, y, new Color(Mathf.Clamp(p.R * f, 0, 1), Mathf.Clamp(p.G * f, 0, 1), Mathf.Clamp(p.B * f, 0, 1), p.A));
            }
        }
    }

    // 부드러운 드롭 섀도우 합성 + 밉맵 생성(축소 표시에서도 선명).
    private ImageTexture Shadowed(Image img)
    {
        var sa = new float[IconBig * IconBig];
        const int ox = 4;
        const int oy = 7;
        for (var y = 0; y < IconBig; y++)
        {
            for (var x = 0; x < IconBig; x++)
            {
                var sxx = x - ox;
                var syy = y - oy;
                if (sxx >= 0 && sxx < IconBig && syy >= 0 && syy < IconBig)
                {
                    sa[(y * IconBig) + x] = img.GetPixel(sxx, syy).A;
                }
            }
        }

        sa = Blur(Blur(sa));

        var outImg = NewBig();
        for (var y = 0; y < IconBig; y++)
        {
            for (var x = 0; x < IconBig; x++)
            {
                var content = img.GetPixel(x, y);
                var shA = Mathf.Clamp(sa[(y * IconBig) + x] * 0.5f, 0f, 1f);
                var outA = content.A + (shA * (1f - content.A));
                if (outA <= 0.0001f) { continue; }
                var k = content.A / outA; // 섀도우 rgb=0 이므로 본체 색만 남는다
                outImg.SetPixel(x, y, new Color(content.R * k, content.G * k, content.B * k, outA));
            }
        }

        outImg.GenerateMipmaps();
        return ImageTexture.CreateFromImage(outImg);
    }

    // 분리형 박스 블러(수평→수직).
    private static float[] Blur(float[] src)
    {
        const int rad = 5;
        var tmp = new float[src.Length];
        for (var y = 0; y < IconBig; y++)
        {
            for (var x = 0; x < IconBig; x++)
            {
                float sum = 0f;
                var cnt = 0;
                for (var k = -rad; k <= rad; k++)
                {
                    var xx = x + k;
                    if (xx >= 0 && xx < IconBig) { sum += src[(y * IconBig) + xx]; cnt++; }
                }

                tmp[(y * IconBig) + x] = sum / cnt;
            }
        }

        var dst = new float[src.Length];
        for (var y = 0; y < IconBig; y++)
        {
            for (var x = 0; x < IconBig; x++)
            {
                float sum = 0f;
                var cnt = 0;
                for (var k = -rad; k <= rad; k++)
                {
                    var yy = y + k;
                    if (yy >= 0 && yy < IconBig) { sum += tmp[(yy * IconBig) + x]; cnt++; }
                }

                dst[(y * IconBig) + x] = sum / cnt;
            }
        }

        return dst;
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

        // 목표 지정 모드: 좌클릭=목적지 설정, 우클릭=취소.
        if (_depTargeting && @event is InputEventMouseButton { Pressed: true } mb)
        {
            if (mb.ButtonIndex == MouseButton.Right) { FinishTargeting(); OpenDeployHub(); return; }
            if (mb.ButtonIndex == MouseButton.Left)
            {
                ApplyTarget(RayToGround(mb.Position));
                return;
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

    // ── 목표 지정 ──
    private void BeginTargeting(int idx)
    {
        CloseModal();
        HidePanels(); // 목표 지정 중에는 성 명령 팔레트·정보 카드가 가려선 안 된다.
        _depTargetIndex = idx;
        _depTargeting = true;
        var layer = new CanvasLayer { Layer = 25 };
        AddChild(layer);
        _targetHintLayer = layer;
        var pc = new PanelContainer();
        pc.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop, Control.LayoutPresetMode.KeepSize, 16);
        pc.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 8, 10));
        layer.AddChild(pc);
        pc.AddChild(MakeLabel("목적지를 클릭하세요  ·  적 성 = 공격  ·  우클릭 취소", 15, GoldBright));
    }

    private void FinishTargeting()
    {
        _depTargeting = false;
        _depTargetIndex = -1;
        if (_targetHintLayer is not null) { _targetHintLayer.QueueFree(); _targetHintLayer = null; }
    }

    private void ApplyTarget(HexCoord? hex)
    {
        if (hex is not { } h) { return; }
        var idx = _depTargetIndex;
        if (idx >= 0 && idx < _pendingDeploys.Count)
        {
            var (req, label) = _pendingDeploys[idx];
            var enemyCity = _state.Cities.FirstOrDefault(c => c.Position == h && c.Owner != Player);
            var mode = enemyCity is not null ? UnitMode.Attack : req.Mode;
            _pendingDeploys[idx] = (req with { Target = h, Mode = mode }, label);
            Dbg($"TARGET idx={idx} -> ({h.Q},{h.R}) mode={mode}");
            var tName = _state.Cities.FirstOrDefault(c => c.Position == h)?.Name ?? $"({h.Q},{h.R})";
            _log.Text = $"목표 → {tName}{(enemyCity is not null ? " (공격모드)" : "")} · 경로 표시됨(출전으로 이어서 편성)";
        }

        // 목표를 정하면 지도 뷰로 돌아가 경로를 바로 보여준다(허브 모달로 가리지 않는다).
        // 이어서 편성하려면 성 팔레트의 '출전'을 다시 누른다.
        FinishTargeting();
        SelectCity(_depModalCity);
        Redraw(_log.Text);
    }

    private readonly List<MeshInstance3D> _supplyMarkers = new();
    private Mesh? _supplyTileMesh;
    private Material? _supplyTileMat;

    // ── 보급 영역: 아군 성 반경(city_resupply_radius) 안을 초록 타일로 표시 ──
    // 부대가 나가 있을 때(또는 출전 예약이 있을 때)만 보여, 이 영역을 벗어나면 휴대 군량으로
    // 버텨야 함을 알린다.
    private void DrawSupplyZones()
    {
        foreach (var m in _supplyMarkers) { m.QueueFree(); }
        _supplyMarkers.Clear();

        var radius = _cb.CityResupplyRadius;
        var show = radius > 0 && (_pendingDeploys.Count > 0 || _state.Armies.Any(u => u.Field.Owner == Player));
        if (!show) { return; }

        _supplyTileMesh ??= new CylinderMesh { TopRadius = 0.52f, BottomRadius = 0.52f, Height = 0.02f, RadialSegments = 6 };
        _supplyTileMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.30f, 0.78f, 0.42f, 0.16f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = new Color(0.30f, 0.78f, 0.42f),
            EmissionEnergyMultiplier = 0.5f,
        };

        var seen = new HashSet<HexCoord>();
        foreach (var city in _state.Cities.Where(c => c.Owner == Player).OrderBy(c => c.Id.Value))
        {
            for (var dq = -radius; dq <= radius; dq++)
            {
                for (var dr = System.Math.Max(-radius, -dq - radius); dr <= System.Math.Min(radius, -dq + radius); dr++)
                {
                    var hex = new HexCoord(city.Position.Q + dq, city.Position.R + dr);
                    if (!seen.Add(hex) || !_map.Contains(hex) || !_passability.CanEnter(MovementDomain.Land, hex)) { continue; }
                    var marker = new MeshInstance3D
                    {
                        Mesh = _supplyTileMesh,
                        MaterialOverride = _supplyTileMat,
                        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                        Position = _view.HexToWorld(hex) + new Vector3(0f, _view.TileTopY + 0.03f, 0f),
                    };
                    AddChild(marker);
                    _supplyMarkers.Add(marker);
                }
            }
        }
    }

    // ── 경로 프리뷰: 예약 부대의 성→목표 경로를 지도에 점으로 ──
    private void DrawDeployPaths()
    {
        foreach (var m in _pathMarkers) { m.QueueFree(); }
        _pathMarkers.Clear();

        _pathDotMesh ??= new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 0.05f, RadialSegments = 8 };
        _pathDotMat ??= new StandardMaterial3D
        {
            AlbedoColor = GoldBright,
            EmissionEnabled = true,
            Emission = Gold,
            EmissionEnergyMultiplier = 1.4f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        foreach (var (req, _) in _pendingDeploys)
        {
            if (req.Target is not { } goal) { continue; }
            var city = _state.Cities.FirstOrDefault(c => c.Id == req.City);
            if (city is null) { continue; }
            var start = city.Position;
            var pf = new HexPathfinder(c => c == start || c == goal || _passability.CanEnter(MovementDomain.Land, c));
            var path = pf.FindPath(start, goal);
            for (var i = 1; i < path.Count; i++)
            {
                var dot = new MeshInstance3D
                {
                    Mesh = _pathDotMesh,
                    MaterialOverride = _pathDotMat,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    Position = _view.HexToWorld(path[i]) + new Vector3(0f, _view.TileTopY + 0.06f, 0f),
                };
                AddChild(dot);
                _pathMarkers.Add(dot);
            }
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
            new(new FactionId(2), "촉", new GeneralId(11), 0, "#d23830"),
        },
        _cities.ToList(),
        // 테스트: 플레이어 성(장안) 장수 10명, 적 성(성도) 2명.
        new List<General>
        {
            Officer(1), Officer(2), Officer(3), Officer(4), Officer(5),
            Officer(6), Officer(7), Officer(8), Officer(9), Officer(10),
            Officer(11), Officer(12),
        },
        Postings: new List<GeneralPosting>
        {
            new(new GeneralId(1), new FactionId(1), new CityId(1)),
            new(new GeneralId(2), new FactionId(1), new CityId(1)),
            new(new GeneralId(3), new FactionId(1), new CityId(1)),
            new(new GeneralId(4), new FactionId(1), new CityId(1)),
            new(new GeneralId(5), new FactionId(1), new CityId(1)),
            new(new GeneralId(6), new FactionId(1), new CityId(1)),
            new(new GeneralId(7), new FactionId(1), new CityId(1)),
            new(new GeneralId(8), new FactionId(1), new CityId(1)),
            new(new GeneralId(9), new FactionId(1), new CityId(1)),
            new(new GeneralId(10), new FactionId(1), new CityId(1)),
            new(new GeneralId(11), new FactionId(2), new CityId(2)),
            new(new GeneralId(12), new FactionId(2), new CityId(2)),
        },
        // 테스트: 플레이어 성 대기 병력 10만(3병종), 적 성 10만.
        GarrisonForces: new List<GarrisonForce>
        {
            new(new CityId(1), "swordsman", 50000, 60),
            new(new CityId(1), "archer", 30000, 60),
            new(new CityId(1), "cavalry", 20000, 60),
            new(new CityId(2), "swordsman", 100000, 60),
        });

    private static General Officer(int id) => new(
        new GeneralId(id), $"장수{id}",
        new Dictionary<TroopClass, AptitudeGrade> { [TroopClass.Infantry] = AptitudeGrade.A },
        Might: 55 + (id * 5 % 45), Intellect: 50 + (id * 9 % 48), Politics: 60 + (id * 3 % 35));

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
        // 예약된 출전을 진행 시작 시점에 일괄 편성(대기열 → 야전).
        Dbg($"--- ADVANCE week={_week} pending={_pendingDeploys.Count} armiesBefore={_state.Armies.Count} ---");
        for (var i = 0; i < _pendingDeploys.Count; i++)
        {
            var rq = _pendingDeploys[i].Req;
            var g = _state.Garrisons.FirstOrDefault(x => x.City == rq.City && x.TroopCode == rq.TroopCode);
            var post = _state.PostingOf(rq.Vanguard);
            Dbg($"  pending[{i}] city={rq.City.Value} troop={rq.TroopCode} amt={rq.Troops} garrison={(g?.Troops.ToString() ?? "none")} van={rq.Vanguard.Value} vanLoc={(post is null ? "null-posting" : (post.Location?.Value.ToString() ?? "field"))} mode={rq.Mode} tgt={(rq.Target is { } t ? $"{t.Q},{t.R}" : "none")} prov={rq.Provisions}");
        }

        var deployNote = new List<string>();
        foreach (var (req, label) in _pendingDeploys)
        {
            var dr = _deployer.Deploy(_state, req);
            Dbg($"  deploy '{label}': ok={dr.Ok} err={dr.Error ?? "-"} armiesNow={dr.State.Armies.Count}");
            if (dr.Ok) { _state = dr.State; deployNote.Add(label); }
            else { deployNote.Add($"출전실패({dr.Error})"); }
        }

        _pendingDeploys.Clear();

        // 플레이어 세력은 직접 조작 — AI는 나머지 세력만 굴린다.
        foreach (var f in _state.Factions.Where(f => f.Id != Player).OrderBy(f => f.Id.Value))
        {
            _state = _ai.PlanWeek(_state, f.Id);
        }

        Dbg($"  afterDeploy armies={_state.Armies.Count}");
        _state = _engine.AdvanceWeek(_state, out _, out var sieges, out var captures, out var plunders);
        _week++;
        Dbg($"  afterAdvance armies={_state.Armies.Count} sieges={sieges.Count} caps={captures.Count}");
        foreach (var u in _state.Armies.OrderBy(u => u.Id.Value))
        {
            Dbg($"    army#{u.Id.Value} owner={u.Field.Owner.Value} pos=({u.Field.Position.Q},{u.Field.Position.R}) troops={u.Pool.Active} mode={u.Field.Mode} tgt={(u.Field.Target is { } t ? $"{t.Q},{t.R}" : "none")} prov={u.Provisions} morale={u.Morale}");
        }

        var note = new List<string>();
        note.AddRange(deployNote);
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

        // 우상단: 성 정보 카드 — 폭 300 고정, 높이는 내용에 맞춰 자동(하단 여백 없음).
        var infoPanel = new PanelContainer { Visible = false };
        infoPanel.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;
        infoPanel.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 8, 10));
        layer.AddChild(infoPanel);
        // 우상단 지점 앵커 → 왼쪽·아래로 자람(오른쪽 위 모서리 고정).
        infoPanel.AnchorLeft = 1f;
        infoPanel.AnchorRight = 1f;
        infoPanel.AnchorTop = 0f;
        infoPanel.AnchorBottom = 0f;
        infoPanel.GrowHorizontal = Control.GrowDirection.Begin;
        infoPanel.GrowVertical = Control.GrowDirection.End;
        infoPanel.OffsetLeft = -312f; // 폭 300 + 여백 12
        infoPanel.OffsetTop = 12f;
        infoPanel.OffsetRight = -12f;
        _infoCard = infoPanel;
        _infoRows = new VBoxContainer { CustomMinimumSize = new Vector2(276, 0) };
        _infoRows.AddThemeConstantOverride("separation", 2);
        infoPanel.AddChild(_infoRows);

        // 명령 팔레트: 클릭한 성 우측에 뜨는 아주 작은 떠있는 패널(텍스트 전용, 위치는 SelectCity).
        _cmdMenu = new PanelContainer { Visible = false, ZIndex = 50 };
        _cmdMenu.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 5, 4));
        layer.AddChild(_cmdMenu);
        var menu = new VBoxContainer();
        menu.AddThemeConstantOverride("separation", 1);
        _cmdMenu.AddChild(menu);
        _cmdList = new VBoxContainer();
        _cmdList.AddThemeConstantOverride("separation", 1);
        menu.AddChild(_cmdList);
        foreach (var (group, indices) in CmdGroups)
        {
            _cmdList.AddChild(MakeLabel($"· {group}", 10, GoldBright));
            foreach (var i in indices)
            {
                var idx = i;
                var btn = MakeButton(Cmds[i].Label);
                btn.AddThemeFontSizeOverride("font_size", 11);
                btn.Alignment = HorizontalAlignment.Center;
                btn.CustomMinimumSize = new Vector2(74, 21);
                btn.Pressed += () => OpenModal(idx);
                _cmdList.AddChild(btn);
            }
        }

        _cmdList.AddChild(MakeLabel("· 군사", 10, GoldBright));
        var deployBtn = MakeButton("출전", accent: true);
        deployBtn.AddThemeFontSizeOverride("font_size", 11);
        deployBtn.Alignment = HorizontalAlignment.Center;
        deployBtn.CustomMinimumSize = new Vector2(74, 21);
        deployBtn.Pressed += () => { if (_selected is { } c) { OpenDeployModal(c); } };
        _cmdList.AddChild(deployBtn);

        _confirm = new ConfirmationDialog { Title = "명령 확인" };
        _confirm.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 8, 16));
        _confirm.Confirmed += () => _onConfirm?.Invoke();
        layer.AddChild(_confirm);

        HidePanels();
    }


    // 아이콘 + 텍스트 정보 행.
    private Control InfoRow(Sym icon, string text)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 5);
        h.AddChild(new TextureRect
        {
            Texture = Icon(icon),
            CustomMinimumSize = new Vector2(14, 14),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        });
        h.AddChild(MakeLabel(text, 12, Parchment));
        return h;
    }

    // 라벨 셀(아이콘 + 항목명).
    private Control LabelCell(Sym icon, string name)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 5);
        h.AddChild(new TextureRect
        {
            Texture = Icon(icon),
            CustomMinimumSize = new Vector2(16, 16),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        });
        h.AddChild(MakeLabel(name, 13, Gold));
        return h;
    }

    // 표 셀 쌍: [라벨(아이콘+항목)] [값]. 4열이면 2쌍/행, 2열이면 1쌍/행.
    private void AddCell(GridContainer g, Sym icon, string name, string value)
    {
        g.AddChild(LabelCell(icon, name));
        var v = MakeLabel(value, 13, Parchment);
        v.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        v.AutowrapMode = TextServer.AutowrapMode.WordSmart; // 폭 초과 시 줄바꿈(카드 폭 300 유지)
        g.AddChild(v);
    }

    private Control Header(string text)
    {
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 3);
        v.AddChild(MakeLabel(text, 14, Gold));
        var rule = new HSeparator();
        rule.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = new Color(Gold, 0.5f), ContentMarginTop = 1, ContentMarginBottom = 1 });
        v.AddChild(rule);
        return v;
    }

    private void HidePanels()
    {
        _infoCard.Visible = false;
        _cmdMenu.Visible = false;
        if (_ring is not null) { _ring.Visible = false; }
    }

    private void SelectCity(CityId id)
    {
        _selected = id;
        _cmdIndex = -1;
        var c = _state.Cities.First(x => x.Id == id);
        var troops = _state.Garrisons.Where(g => g.City == id).Select(g => $"{g.TroopCode} {g.Troops}");
        var officers = _state.GeneralsAt(id).Select(g => _state.Generals.First(x => x.Id == g).Name);
        var pending = _state.Commands.Where(p => p.City == id).Select(p =>
            $"{KindName(p.Kind)} 남은 {p.CompletionDay - _state.Day}일");
        var facilities = $"논{c.Paddies} 밭{c.Farms} 마을{c.Villages}{(c.Workshop ? " 공방" : "")}";

        Clear(_infoRows);
        _infoRows.AddChild(MakeLabel($"《 {c.Name} 》", 15, GoldBright));

        // 짧은 수치: 4칸(라벨·값·라벨·값) 2쌍씩.
        var g4 = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        g4.AddThemeConstantOverride("h_separation", 10);
        g4.AddThemeConstantOverride("v_separation", 5);
        _infoRows.AddChild(g4);
        AddCell(g4, Sym.Coin, "금", $"{c.Gold}");
        AddCell(g4, Sym.Grain, "군량", $"{c.Provisions}");
        AddCell(g4, Sym.People, "인구", $"{c.Population}");
        AddCell(g4, Sym.Shield, "치안", $"{c.Security}");
        AddCell(g4, Sym.Coin, "세율", $"{c.TaxRate}%");
        AddCell(g4, Sym.Wall, "성벽", $"{c.Wall}");

        // 긴 값: 전체폭 2칸(라벨·값).
        var g2 = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        g2.AddThemeConstantOverride("h_separation", 10);
        g2.AddThemeConstantOverride("v_separation", 5);
        _infoRows.AddChild(g2);
        AddCell(g2, Sym.Ore, "광물", $"{c.Ore}/{c.Horses}/{c.Elephants}");
        AddCell(g2, Sym.Book, "시설", facilities);
        AddCell(g2, Sym.Sword, "대기", troops.Any() ? string.Join(",", troops) : "없음");
        AddCell(g2, Sym.Officer, "주둔", officers.Any() ? string.Join(",", officers) : "없음");
        if (pending.Any())
        {
            AddCell(g2, Sym.Scroll, "진행", string.Join(",", pending));
        }

        var depQueue = _pendingDeploys.Where(p => p.Req.City == id).Select(p => p.Label).ToList();
        if (depQueue.Count > 0)
        {
            AddCell(g2, Sym.Flag, "출전대기", string.Join(",", depQueue));
        }

        PlacePalette(c.Position);
        _infoCard.Visible = true;
        _cmdMenu.Visible = true;
        MoveRing(c.Position);
    }

    // 명령 팔레트를 성 화면좌표의 우측에 배치(화면 밖으로 안 나가게 clamp). 줌/이동 시 매 프레임 추종.
    private void PlacePalette(HexCoord at)
    {
        var world = _view.HexToWorld(at) + new Vector3(0f, _view.TileTopY, 0f);
        var screen = _camera.UnprojectPosition(world);
        var sz = _cmdMenu.GetCombinedMinimumSize();
        var vp = GetViewport().GetVisibleRect().Size;
        var px = Mathf.Clamp(screen.X + 100f, 8f, System.Math.Max(8f, vp.X - sz.X - 8f));
        var py = Mathf.Clamp(screen.Y - (sz.Y * 0.5f), 8f, System.Math.Max(8f, vp.Y - sz.Y - 8f));
        _cmdMenu.Position = new Vector2(px, py);
    }

    // 줌/이동 중에도 팔레트가 선택한 성을 따라가도록 갱신.
    public override void _Process(double delta)
    {
        if (_cmdMenu.Visible && _selected is { } sel)
        {
            var c = _state.Cities.FirstOrDefault(x => x.Id == sel);
            if (c is not null) { PlacePalette(c.Position); }
        }

        if (_dragging && _dragPanel is not null)
        {
            if (!Input.IsMouseButtonPressed(MouseButton.Left)) { _dragging = false; }
            else { _dragPanel.Position = GetViewport().GetMousePosition() - _dragOffset; }
        }
    }

    // 명령 클릭 → 큰 모달(반투명 배경 + 아이콘 카드 그리드). 카드로 대상/계략/세율을 고르고,
    // 장수 카드를 클릭하면 컨펌 후 실행한다(삼국지14/콜오브드래곤즈풍 명령 창).
    private void OpenModal(int cmdIndex)
    {
        if (_selected is not { } city)
        {
            return;
        }

        _cmdIndex = cmdIndex;
        var cmd = Cmds[cmdIndex];
        CloseModal();

        var layer = new CanvasLayer { Layer = 20 };
        AddChild(layer);
        _modalLayer = layer;

        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.62f) };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.MouseFilter = Control.MouseFilterEnum.Stop;
        backdrop.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { CloseModal(); }
        };
        layer.AddChild(backdrop);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(center);

        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 10, 14));
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        panel.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps; // 고해상 아이콘 축소 시 선명
        center.AddChild(panel);

        // 창 크기에 맞춘 반응형 모달(작은 화면에서도 넘치지 않게 상·하한 캡).
        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.66f, 460f, 778f);
        var mh = Mathf.Clamp(vp.Y * 0.80f, 374f, 620f);
        var colOpt = (int)Mathf.Clamp(Mathf.Floor((mw + 8f) / 146f), 3, 5);
        var colOff = (int)Mathf.Clamp(Mathf.Floor((mw + 8f) / 169f), 2, 4);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(mw, 0) };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        panel.AddChild(scroll);

        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(box);

        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var cityName = _state.Cities.First(x => x.Id == city).Name;
        var title = MakeLabel($"◈  {cmd.Label}   《 {cityName} 》", 26, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(46, 43);
        close.Pressed += CloseModal;
        titleRow.AddChild(close);
        box.AddChild(GoldRule());

        var cityData = _state.Cities.First(x => x.Id == city);
        var options = OptionList(cmd, cityData);
        _optionCards.Clear();
        _modalParam = cmd.Param == "tax" ? 2 : 0;
        if (options.Count > 0)
        {
            box.AddChild(MakeLabel(cmd.Param == "stratagem" ? "계략을 선택하세요" : "대상을 선택하세요", 19, GoldBright));
            var grid = new GridContainer { Columns = System.Math.Min(colOpt, options.Count) };
            grid.AddThemeConstantOverride("h_separation", 10);
            grid.AddThemeConstantOverride("v_separation", 10);
            box.AddChild(grid);
            for (var i = 0; i < options.Count; i++)
            {
                var idx = i;
                var card = OptionCard(options[i]);
                _optionCards.Add(card);
                card.GuiInput += e =>
                {
                    if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { PickOption(idx, options[idx]); }
                };
                grid.AddChild(card);
            }
        }

        _modalDetail = MakeLabel("", 17, Parchment);
        box.AddChild(_modalDetail);
        box.AddChild(GoldRule());

        box.AddChild(MakeLabel("수행 장수 (클릭 = 실행)", 19, GoldBright));
        _modalOfficers = new GridContainer { Columns = colOff };
        _modalOfficers.AddThemeConstantOverride("h_separation", 10);
        _modalOfficers.AddThemeConstantOverride("v_separation", 10);
        box.AddChild(_modalOfficers);
        BuildOfficerCards(city, cmdIndex);

        if (options.Count > 0) { PickOption(_modalParam, options[_modalParam]); }

        // 스크롤 높이를 내용에 맞추되 mh로 상한 → 짧은 명령은 아래 여백 없음, 긴 건 스크롤.
        var contentH = box.GetCombinedMinimumSize().Y;
        scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentH, mh));
    }

    private void CloseModal()
    {
        if (_modalLayer is not null)
        {
            _modalLayer.QueueFree();
            _modalLayer = null;
        }

        _optionCards.Clear();
        _depTroopCards.Clear();
        _depVanCards.Clear();
        _depAdjCards.Clear();
        _depAmountSpin = null;
        _depPreview = null;
        _depModeButtons.Clear();
        _depEditIndex = -1;
        _depTarget = null;
    }

    // ── 출전 모달: 병종 + 선봉(+부관) 선택 → 대기 병력을 야전 부대로 편성 ──
    private void OpenDeployModal(CityId city)
    {
        _depModalCity = city;
        _depSelectedUnit = -1;
        OpenDeployHub();
    }

    // ── 허브: 이 성의 출전 예약 목록(수정/삭제) + 부대 추가 ──
    private void OpenDeployHub()
    {
        var city = _depModalCity;
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        _depTroopCards.Clear();
        _depVanCards.Clear();
        _depAdjCards.Clear();
        _depAmountSpin = null;
        _depPreview = null;

        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.57f, 470f, 730f);
        var mh = Mathf.Clamp(vp.Y * 0.8f, 360f, 640f);
        var box = DeployScaffold(mw, out var scroll, out var panel);

        var cityName = _state.Cities.First(x => x.Id == city).Name;
        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var title = MakeLabel($"◈  출전 예약   《 {cityName} 》  ⠿", 19, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        titleRow.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(34, 32);
        close.Pressed += () => { CloseModal(); SelectCity(city); };
        titleRow.AddChild(close);
        box.AddChild(GoldRule());

        var mine = new List<int>();
        for (var i = 0; i < _pendingDeploys.Count; i++)
        {
            if (_pendingDeploys[i].Req.City == city) { mine.Add(i); }
        }

        box.AddChild(MakeLabel($"예약된 부대 ({mine.Count})   — 타일을 눌러 선택", 14, GoldBright));

        if (!mine.Contains(_depSelectedUnit)) { _depSelectedUnit = -1; }

        // 6열 타일 그리드 — 부대가 늘면 한 칸씩 채워지고, ＋ 타일이 그 다음 칸에.
        var grid = new GridContainer { Columns = 6 };
        grid.AddThemeConstantOverride("h_separation", 8);
        grid.AddThemeConstantOverride("v_separation", 8);
        box.AddChild(grid);
        foreach (var gi in mine)
        {
            var idx = gi;
            var rq = _pendingDeploys[gi].Req;
            var tmpl = _troops.FirstOrDefault(t => t.Code == rq.TroopCode);
            var emblem = tmpl is not null ? ClassEmblem(tmpl.Class) : Icon(Sym.Sword);
            var tname = tmpl?.Name ?? rq.TroopCode;
            var vname = _state.Generals.First(g => g.Id == rq.Vanguard).Name;

            var tile = new PanelContainer
            {
                CustomMinimumSize = new Vector2(104, 118),
                MouseFilter = Control.MouseFilterEnum.Stop,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            };
            tile.AddThemeStyleboxOverride("panel", CardBox(gi == _depSelectedUnit));
            var tv = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            tv.AddThemeConstantOverride("separation", 2);
            tile.AddChild(tv);
            tv.AddChild(new TextureRect
            {
                Texture = emblem,
                CustomMinimumSize = new Vector2(46, 46),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            });
            var n1 = MakeLabel($"{tname} {rq.Troops}", 12, GoldBright);
            n1.HorizontalAlignment = HorizontalAlignment.Center;
            tv.AddChild(n1);
            var n2 = MakeLabel(vname, 11, Parchment);
            n2.HorizontalAlignment = HorizontalAlignment.Center;
            tv.AddChild(n2);
            var n3 = MakeLabel(rq.Target is null ? $"{ModeName(rq.Mode)}·미지정" : ModeName(rq.Mode), 10, rq.Target is null ? new Color(0.85f, 0.5f, 0.4f) : GoldBright);
            n3.HorizontalAlignment = HorizontalAlignment.Center;
            tv.AddChild(n3);
            tile.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { _depSelectedUnit = idx; OpenDeployHub(); }
            };
            grid.AddChild(tile);
        }

        // ＋ 타일 — 그리드의 다음 칸에 자연스럽게 이어짐(6칸 채우면 다음 줄).
        var addTile = new PanelContainer
        {
            CustomMinimumSize = new Vector2(104, 118),
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        addTile.AddThemeStyleboxOverride("panel", CardBox(false));
        var al = MakeLabel("＋", 32, GoldBright);
        al.HorizontalAlignment = HorizontalAlignment.Center;
        al.VerticalAlignment = VerticalAlignment.Center;
        addTile.AddChild(al);
        addTile.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { OpenDeployCompose(-1); }
        };
        grid.AddChild(addTile);

        // 선택 부대 컨트롤 바(모드 3종 / 목표 / 편성 수정 / 삭제)
        if (_depSelectedUnit >= 0 && _depSelectedUnit < _pendingDeploys.Count)
        {
            var sidx = _depSelectedUnit;
            var srq = _pendingDeploys[sidx].Req;
            var stmpl = _troops.FirstOrDefault(t => t.Code == srq.TroopCode);
            var svan = _state.Generals.First(g => g.Id == srq.Vanguard).Name;
            var strain = _state.Garrisons.FirstOrDefault(g => g.City == city && g.TroopCode == srq.TroopCode)?.TrainingLevel ?? 0;
            var stgt = srq.Target is { } tg ? "→ " + (_state.Cities.FirstOrDefault(c => c.Position == tg)?.Name ?? $"({tg.Q},{tg.R})") : "목표 미지정(성 앞 대기)";
            box.AddChild(GoldRule());
            box.AddChild(MakeLabel($"◈ {stmpl?.Name ?? srq.TroopCode} {srq.Troops}명 · 주장 {svan} · 훈련 {strain} · {stgt}", 13, GoldBright));

            var modeRow = new HBoxContainer();
            modeRow.AddThemeConstantOverride("separation", 6);
            modeRow.AddChild(MakeLabel("이동 모드", 12, Parchment));
            foreach (var (mlabel, mode) in new[] { ("행군", UnitMode.March), ("전진", UnitMode.Advance), ("공격", UnitMode.Attack) })
            {
                var mm = mode;
                var sel = srq.Mode == mode;
                var mb = MakeButton(mlabel);
                mb.CustomMinimumSize = new Vector2(64, 30);
                mb.AddThemeStyleboxOverride("normal", Frame(sel ? AccentFill : InkSoft, sel ? GoldBright : Gold, sel ? 2 : 1, 5, 6));
                mb.Pressed += () => { _pendingDeploys[sidx] = (_pendingDeploys[sidx].Req with { Mode = mm }, _pendingDeploys[sidx].Label); OpenDeployHub(); };
                modeRow.AddChild(mb);
            }

            box.AddChild(modeRow);

            var actRow = new HBoxContainer();
            actRow.AddThemeConstantOverride("separation", 6);
            var tgt = MakeButton("목표 지정", accent: true);
            tgt.CustomMinimumSize = new Vector2(96, 32);
            tgt.Pressed += () => BeginTargeting(sidx);
            actRow.AddChild(tgt);
            var edit = MakeButton("편성 수정");
            edit.CustomMinimumSize = new Vector2(96, 32);
            edit.Pressed += () => OpenDeployCompose(sidx);
            actRow.AddChild(edit);
            var rm = MakeButton("삭제");
            rm.CustomMinimumSize = new Vector2(72, 32);
            rm.Pressed += () => { _pendingDeploys.RemoveAt(sidx); _depSelectedUnit = -1; SelectCity(city); OpenDeployHub(); };
            actRow.AddChild(rm);
            box.AddChild(actRow);
        }
        box.AddChild(MakeLabel("예약은 \"진행\" 시 일괄 출전합니다.  (제목줄 ⠿ 을 잡아 이동)", 11, Parchment));

        var contentH = box.GetCombinedMinimumSize().Y;
        scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentH, mh));
        CenterAndDrag(panel, titleRow, mw, mh, box);
    }

    // ── 편성 화면: 병종·수량·선봉/부관 → 저장(신규 추가 / 기존 수정) ──
    private void OpenDeployCompose(int editIndex)
    {
        var city = _depModalCity;
        _depEditIndex = editIndex;
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        _depTroopCards.Clear();
        _depVanCards.Clear();
        _depAdjCards.Clear();
        _depTroop = null;
        _depVan = null;
        _depAdj = null;
        _depAmount = 0;
        _depMode = UnitMode.Advance;
        _depTarget = null;
        _depAmountSpin = null;
        _depPreview = null;
        _depModeButtons.Clear();
        _depProvDays = 0;
        _depProvSlider = null;
        _depProvLabel = null;

        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.52f, 400f, 660f);
        var mh = Mathf.Clamp(vp.Y * 0.78f, 360f, 620f);
        var box = DeployScaffold(mw, out var scroll, out var panel);

        var cityName = _state.Cities.First(x => x.Id == city).Name;
        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var title = MakeLabel($"◈  {(editIndex >= 0 ? "부대 수정" : "부대 편성")}   《 {cityName} 》  ⠿", 18, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        titleRow.AddChild(title);
        var back = MakeButton("◀ 목록");
        back.CustomMinimumSize = new Vector2(60, 32);
        back.Pressed += OpenDeployHub;
        titleRow.AddChild(back);
        box.AddChild(GoldRule());

        var cols = (int)Mathf.Clamp(Mathf.Floor((mw + 8f) / 128f), 2, 4);

        // 다른 예약(수정 중인 것 제외)이 이미 소모한 병력·장수.
        var usedTroops = new Dictionary<string, int>();
        var usedGens = new HashSet<GeneralId>();
        for (var i = 0; i < _pendingDeploys.Count; i++)
        {
            if (i == editIndex) { continue; }
            var rq = _pendingDeploys[i].Req;
            if (rq.City != city) { continue; }
            usedTroops[rq.TroopCode] = usedTroops.GetValueOrDefault(rq.TroopCode, 0) + rq.Troops;
            usedGens.Add(rq.Vanguard);
            if (rq.Adjutant is { } a) { usedGens.Add(a); }
        }

        // 1) 병종
        box.AddChild(MakeLabel("병종 (대기 병력)", 13, GoldBright));
        var tg = new GridContainer { Columns = cols };
        tg.AddThemeConstantOverride("h_separation", 8);
        tg.AddThemeConstantOverride("v_separation", 8);
        box.AddChild(tg);
        foreach (var gar in _state.Garrisons.Where(g => g.City == city && g.Troops > 0))
        {
            var code = gar.TroopCode;
            var remaining = gar.Troops - usedTroops.GetValueOrDefault(code, 0);
            if (remaining <= 0) { continue; }
            var template = _troops.FirstOrDefault(t => t.Code == code);
            var name = template?.Name ?? code;
            var warn = gar.TrainingLevel < 50 ? "  ⚠훈련부족" : "";
            var emblem = template is not null ? ClassEmblem(template.Class) : Icon(Sym.Sword);
            var card = DeployCard(emblem, name, $"{remaining}명 · 훈{gar.TrainingLevel}{warn}");
            var cap = remaining;
            _depTroopCards.Add((card, code));
            card.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                {
                    _depTroop = code;
                    if (_depAmountSpin is { } sp) { sp.MaxValue = cap; sp.Value = cap; }
                    _depAmount = cap;
                    var capDays = System.Math.Max(1, (template?.ProvisionsCapacity ?? 300) / System.Math.Max(1, _provPer10kPerDay));
                    if (_depProvSlider is { } ps) { ps.MaxValue = capDays; ps.Value = capDays; }
                    _depProvDays = capDays;
                    RestyleDeploy();
                    UpdateProvLabel();
                    UpdateDepPreview();
                }
            };
            tg.AddChild(card);
        }

        // 2) 병력 수량
        box.AddChild(MakeLabel("병력 수량", 13, GoldBright));
        var amtRow = new HBoxContainer();
        amtRow.AddThemeConstantOverride("separation", 6);
        _depAmountSpin = new SpinBox { MinValue = 0, MaxValue = 0, Step = 100, Value = 0, CustomMinimumSize = new Vector2(130, 30) };
        _depAmountSpin.AddThemeFontOverride("font", _font);
        _depAmountSpin.AddThemeFontSizeOverride("font_size", 14);
        _depAmountSpin.ValueChanged += v => { _depAmount = (int)v; UpdateProvLabel(); UpdateDepPreview(); };
        amtRow.AddChild(_depAmountSpin);
        foreach (var (plabel, frac) in new[] { ("전량", 1.0), ("½", 0.5), ("¼", 0.25) })
        {
            var pf = frac;
            var pb = MakeButton(plabel);
            pb.CustomMinimumSize = new Vector2(48, 28);
            pb.Pressed += () => { if (_depAmountSpin is { } sp) { sp.Value = System.Math.Floor(sp.MaxValue * pf); } };
            amtRow.AddChild(pb);
        }

        box.AddChild(amtRow);

        // 2-a) 군량(일수) — 부대가 휴대할 군량을 일수로 조정(성 비축·적재 상한 안에서).
        box.AddChild(MakeLabel("군량 (일수)", 13, GoldBright));
        var provRow = new HBoxContainer();
        provRow.AddThemeConstantOverride("separation", 10);
        _depProvSlider = new HSlider
        {
            MinValue = 0,
            MaxValue = 50,
            Step = 1,
            Value = 0,
            CustomMinimumSize = new Vector2(240, 24),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        _depProvSlider.ValueChanged += v => { _depProvDays = (int)v; UpdateProvLabel(); UpdateDepPreview(); };
        provRow.AddChild(_depProvSlider);
        _depProvLabel = MakeLabel("병종을 먼저 선택", 12, Parchment);
        _depProvLabel.CustomMinimumSize = new Vector2(220, 0);
        provRow.AddChild(_depProvLabel);
        box.AddChild(provRow);

        // 2-b) 이동 모드(행군/전진/공격)
        box.AddChild(MakeLabel("이동 모드", 13, GoldBright));
        var modeRow = new HBoxContainer();
        modeRow.AddThemeConstantOverride("separation", 6);
        foreach (var (mlabel, mode) in new[] { ("행군", UnitMode.March), ("전진", UnitMode.Advance), ("공격", UnitMode.Attack) })
        {
            var mm = mode;
            var mb = MakeButton(mlabel);
            mb.CustomMinimumSize = new Vector2(72, 30);
            mb.Pressed += () => { _depMode = mm; RestyleModes(); UpdateDepPreview(); };
            _depModeButtons.Add((mb, mode));
            modeRow.AddChild(mb);
        }

        box.AddChild(modeRow);

        // 3) 선봉 / 4) 부관
        var free = _state.GeneralsAt(city).Where(g => !_state.IsGeneralBusy(g) && !usedGens.Contains(g)).OrderBy(g => g.Value).ToList();
        box.AddChild(GoldRule());
        box.AddChild(MakeLabel("선봉 장수 (필수)", 13, GoldBright));
        var vgGrid = new GridContainer { Columns = cols };
        vgGrid.AddThemeConstantOverride("h_separation", 8);
        vgGrid.AddThemeConstantOverride("v_separation", 8);
        box.AddChild(vgGrid);
        box.AddChild(MakeLabel("부관 장수 (선택 · 다시 누르면 해제)", 13, GoldBright));
        var adGrid = new GridContainer { Columns = cols };
        adGrid.AddThemeConstantOverride("h_separation", 8);
        adGrid.AddThemeConstantOverride("v_separation", 8);
        box.AddChild(adGrid);
        foreach (var gid in free)
        {
            var g = _state.Generals.First(x => x.Id == gid);
            var portrait = OfficerPortrait(gid);
            var captured = gid;
            var vg = DeployCard(portrait, g.Name, $"무{g.Might} 지{g.Intellect}");
            _depVanCards.Add((vg, gid));
            vg.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { _depVan = captured; if (_depAdj == captured) { _depAdj = null; } RestyleDeploy(); UpdateDepPreview(); }
            };
            vgGrid.AddChild(vg);

            var ad = DeployCard(portrait, g.Name, $"무{g.Might} 지{g.Intellect}");
            _depAdjCards.Add((ad, gid));
            ad.GuiInput += e =>
            {
                if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { return; }
                if (captured == _depVan)
                {
                    if (_depPreview is not null) { _depPreview.Text = "부관은 선봉과 다른 장수여야 합니다."; }
                    return; // 선봉과 같은 장수는 부관이 될 수 없다
                }

                _depAdj = _depAdj == captured ? null : captured;
                RestyleDeploy();
                UpdateDepPreview();
            };
            adGrid.AddChild(ad);
        }

        // 5) 미리보기 + 저장/뒤로
        box.AddChild(GoldRule());
        _depPreview = MakeLabel("", 12, Parchment);
        box.AddChild(_depPreview);
        var save = MakeButton(editIndex >= 0 ? "▶ 저장" : "▶ 부대 추가", accent: true);
        save.CustomMinimumSize = new Vector2(0, 34);
        save.Pressed += SaveCompose;
        box.AddChild(save);

        // 수정이면 기존 값 프리필.
        if (editIndex >= 0 && editIndex < _pendingDeploys.Count)
        {
            var rq = _pendingDeploys[editIndex].Req;
            _depTroop = rq.TroopCode;
            _depVan = rq.Vanguard;
            _depAdj = rq.Adjutant;
            var gar = _state.Garrisons.FirstOrDefault(g => g.City == city && g.TroopCode == rq.TroopCode);
            var capEdit = (gar?.Troops ?? rq.Troops) - usedTroops.GetValueOrDefault(rq.TroopCode, 0);
            _depAmountSpin.MaxValue = System.Math.Max(capEdit, rq.Troops);
            _depAmountSpin.Value = rq.Troops;
            _depAmount = rq.Troops;
            _depMode = rq.Mode;
            _depTarget = rq.Target;

            var tmpl = _troops.FirstOrDefault(t => t.Code == rq.TroopCode);
            var capDays = System.Math.Max(1, (tmpl?.ProvisionsCapacity ?? 300) / System.Math.Max(1, _provPer10kPerDay));
            _depProvDays = rq.Provisions < 0
                ? capDays
                : System.Math.Clamp(rq.Provisions * 10000 / System.Math.Max(1, rq.Troops * _provPer10kPerDay), 0, capDays);
            if (_depProvSlider is { } ps) { ps.MaxValue = capDays; ps.Value = _depProvDays; }
        }

        RestyleDeploy();
        RestyleModes();
        UpdateProvLabel();
        UpdateDepPreview();
        var contentH = box.GetCombinedMinimumSize().Y;
        scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentH, mh));
        CenterAndDrag(panel, titleRow, mw, mh, box);
    }

    // 자유 배치 패널(드래그 가능). panel은 레이어에 직접 얹혀 Position으로 움직인다.
    private VBoxContainer DeployScaffold(float mw, out ScrollContainer scroll, out PanelContainer panel)
    {
        var layer = new CanvasLayer { Layer = 20 };
        AddChild(layer);
        _modalLayer = layer;
        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.62f) };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.MouseFilter = Control.MouseFilterEnum.Stop;
        backdrop.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { CloseModal(); }
        };
        layer.AddChild(backdrop);
        panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 10, 14));
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        panel.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;
        layer.AddChild(panel);
        scroll = new ScrollContainer { CustomMinimumSize = new Vector2(mw, 0) };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        panel.AddChild(scroll);
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(box);
        return box;
    }

    // 패널을 화면 중앙에 두고, 핸들(제목줄)을 잡아 드래그할 수 있게 한다.
    private void CenterAndDrag(PanelContainer panel, Control handle, float mw, float mh, VBoxContainer box)
    {
        var vp = GetViewport().GetVisibleRect().Size;
        var sz = new Vector2(mw + 28f, Mathf.Min(box.GetCombinedMinimumSize().Y + 28f, mh + 28f));
        panel.Position = new Vector2(Mathf.Max(8f, (vp.X - sz.X) / 2f), Mathf.Max(8f, (vp.Y - sz.Y) / 2f));

        handle.MouseFilter = Control.MouseFilterEnum.Stop;
        handle.MouseDefaultCursorShape = Control.CursorShape.Move;
        handle.GuiInput += e =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mbtn && mbtn.Pressed)
            {
                _dragging = true;
                _dragPanel = panel;
                _dragOffset = GetViewport().GetMousePosition() - panel.Position;
            }
        };
    }

    private void UpdateDepPreview()
    {
        if (_depPreview is null) { return; }
        var parts = new List<string>();
        if (_depTroop is { } c) { parts.Add($"{_troops.FirstOrDefault(t => t.Code == c)?.Name ?? c} {_depAmount}"); }
        if (_depVan is { } v) { parts.Add("선봉 " + _state.Generals.First(g => g.Id == v).Name); }
        if (_depAdj is { } a) { parts.Add("부관 " + _state.Generals.First(g => g.Id == a).Name); }
        parts.Add(ModeName(_depMode) + "모드");
        parts.Add(_depTarget is { } tg2 ? "목표 " + (_state.Cities.FirstOrDefault(c => c.Position == tg2)?.Name ?? $"({tg2.Q},{tg2.R})") : "목표 미지정");
        _depPreview.Text = "현재 편성:  " + (parts.Count > 0 ? string.Join(" · ", parts) : "(병종·수량·장수 선택)");
    }

    // 슬라이더 일수 → 실제 휴대 군량(병력 비례 환산). 적재 상한·성 비축 안에서 자른다(미리보기용).
    private int ProvisionsToCarry()
    {
        if (_depTroop is not { } code) { return 0; }
        var tmpl = _troops.FirstOrDefault(t => t.Code == code);
        var capacity = (tmpl?.ProvisionsCapacity ?? 300) * _depAmount / 10000;
        var wanted = _depProvDays * _depAmount * _provPer10kPerDay / 10000;
        var cityProv = _state.Cities.First(x => x.Id == _depModalCity).Provisions;
        return System.Math.Min(System.Math.Min(wanted, capacity), cityProv);
    }

    private void UpdateProvLabel()
    {
        if (_depProvLabel is null) { return; }
        if (_depTroop is null) { _depProvLabel.Text = "병종을 먼저 선택"; return; }
        var cityProv = _state.Cities.First(x => x.Id == _depModalCity).Provisions;
        _depProvLabel.Text = $"{_depProvDays}일 · 휴대 {ProvisionsToCarry()} / 성 비축 {cityProv}";
    }

    private void SaveCompose()
    {
        // 검증 실패 메시지는 모달 안(_depPreview)에 띄운다 — 하단바(_log)는 모달에 가려 안 보인다.
        void Err(string m) { if (_depPreview is not null) { _depPreview.Text = "⚠ " + m; } }

        if (_depTroop is null) { Err("병종을 선택하세요."); return; }
        if (_depVan is not { } van) { Err("선봉 장수를 선택하세요."); return; }
        if (_depAmount <= 0) { Err("병력 수량을 정하세요."); return; }
        if (_depAdj == van) { Err("부관은 선봉과 다른 장수여야 합니다."); return; }

        var tName = _troops.FirstOrDefault(t => t.Code == _depTroop)?.Name ?? _depTroop;
        var vName = _state.Generals.First(g => g.Id == van).Name;
        var aName = _depAdj is { } a ? "+" + _state.Generals.First(g => g.Id == a).Name : "";
        var provisions = _depProvDays * _depAmount * _provPer10kPerDay / 10000;
        var req = new DeployRequest(_depModalCity, _depTroop, _depAmount, van, _depAdj, _depMode, _depTarget, provisions);
        var entry = (req, $"{tName} {_depAmount}({vName}{aName}) · {ModeName(_depMode)} · 군량{_depProvDays}일");
        if (_depEditIndex >= 0 && _depEditIndex < _pendingDeploys.Count) { _pendingDeploys[_depEditIndex] = entry; }
        else { _pendingDeploys.Add(entry); }

        Dbg($"SAVE {(_depEditIndex >= 0 ? $"edit#{_depEditIndex}" : "add")} city={req.City.Value} troop={req.TroopCode} amt={req.Troops} van={req.Vanguard.Value} adj={(req.Adjutant?.Value.ToString() ?? "-")} mode={req.Mode} tgt={(req.Target is { } t ? $"{t.Q},{t.R}" : "none")} prov={req.Provisions} -> pending={_pendingDeploys.Count}");

        SelectCity(_depModalCity);
        OpenDeployHub();
    }

    private readonly Dictionary<int, ImageTexture> _portraits = new();

    // 장수 초상: assets/portraits/general_{id}.png 있으면 그것, 없으면 공용 장수 흉상(icon_officer) 폴백.
    private ImageTexture OfficerPortrait(GeneralId id)
    {
        if (_portraits.TryGetValue(id.Value, out var cached)) { return cached; }

        var path = $"res://assets/portraits/general_{id.Value}.png";
        if (Godot.FileAccess.FileExists(path))
        {
            var img = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
            img.GenerateMipmaps();
            var t = ImageTexture.CreateFromImage(img);
            _portraits[id.Value] = t;
            return t;
        }

        return Icon(Sym.Officer);
    }

    private PanelContainer DeployCard(ImageTexture icon, string title, string sub)
    {
        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(128, 112),
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        card.AddThemeStyleboxOverride("panel", CardBox(false));
        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.AddThemeConstantOverride("separation", 3);
        card.AddChild(v);
        v.AddChild(new TextureRect
        {
            Texture = icon,
            CustomMinimumSize = new Vector2(46, 46),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        });
        var t = MakeLabel(title, 15, GoldBright);
        t.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(t);
        var s = MakeLabel(sub, 11, Parchment);
        s.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(s);
        return card;
    }

    private void RestyleDeploy()
    {
        foreach (var (card, code) in _depTroopCards) { card.AddThemeStyleboxOverride("panel", CardBox(code == _depTroop)); }

        // 선봉으로 뽑힌 장수는 부관 목록에서, 부관으로 뽑힌 장수는 선봉 목록에서 감춘다(중복 지정 방지).
        foreach (var (card, id) in _depVanCards)
        {
            card.Visible = id != _depAdj;
            card.AddThemeStyleboxOverride("panel", CardBox(_depVan == id));
        }

        foreach (var (card, id) in _depAdjCards)
        {
            card.Visible = id != _depVan;
            card.AddThemeStyleboxOverride("panel", CardBox(_depAdj == id));
        }
    }

    private void RestyleModes()
    {
        foreach (var (btn, mode) in _depModeButtons)
        {
            var sel = mode == _depMode;
            btn.AddThemeStyleboxOverride("normal", Frame(sel ? AccentFill : InkSoft, sel ? GoldBright : Gold, sel ? 2 : 1, 5, 6));
            btn.AddThemeColorOverride("font_color", sel ? GoldBright : Parchment);
        }
    }

    private static string ModeName(UnitMode m) => m switch
    {
        UnitMode.March => "행군",
        UnitMode.Advance => "전진",
        UnitMode.Attack => "공격",
        _ => m.ToString(),
    };

    // 명령별 옵션 카드 목록: (표시명, 아이콘, 부가설명).
    private List<(string Name, ImageTexture Icon, string Detail)> OptionList(
        (string Label, CommandKind Kind, string Param) cmd, City city)
    {
        var list = new List<(string, ImageTexture, string)>();
        switch (cmd.Param)
        {
            case "troop":
                foreach (var t in _troops)
                {
                    var detail = cmd.Kind == CommandKind.Research
                        ? $"연구 Lv.{_state.ResearchOf(city.Owner, t.Code)}"
                        : ClassName(t.Class);
                    list.Add((t.Name, ClassEmblem(t.Class), detail));
                }

                break;
            case "tax":
                foreach (var v in new[] { 0, 10, 20, 30, 40, 50 }) { list.Add(($"{v}%", Icon(Sym.Coin), "세율")); }
                break;
            case "stratagem":
                foreach (var s in Strats) { list.Add((s.Label, StratIcon(s.Code), StratDesc(s.Code))); }
                break;
        }

        return list;
    }

    // 아이콘 카드(큰 아이콘 + 이름 + 설명). 클릭 판정은 호출부에서 GuiInput으로.
    private PanelContainer OptionCard((string Name, ImageTexture Icon, string Detail) o)
    {
        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(138, 121),
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        card.AddThemeStyleboxOverride("panel", CardBox(false));

        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.AddThemeConstantOverride("separation", 4);
        card.AddChild(v);
        v.AddChild(new TextureRect
        {
            Texture = o.Icon,
            CustomMinimumSize = new Vector2(49, 49),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        });
        var name = MakeLabel(o.Name, 19, GoldBright);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(name);
        var det = MakeLabel(o.Detail, 14, Parchment);
        det.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(det);

        card.MouseEntered += () =>
        {
            if (!_optionCards.Contains(card) || _optionCards.IndexOf(card) != _modalParam)
            {
                card.AddThemeStyleboxOverride("panel", CardBox(false, hover: true));
            }
        };
        card.MouseExited += () =>
        {
            if (!_optionCards.Contains(card) || _optionCards.IndexOf(card) != _modalParam)
            {
                card.AddThemeStyleboxOverride("panel", CardBox(false));
            }
        };
        return card;
    }

    private void PickOption(int idx, (string Name, ImageTexture Icon, string Detail) o)
    {
        _modalParam = idx;
        for (var i = 0; i < _optionCards.Count; i++)
        {
            _optionCards[i].AddThemeStyleboxOverride("panel", CardBox(i == idx));
        }

        _modalDetail.Text = o.Detail.Length > 0 ? $"▶  {o.Name}  —  {o.Detail}" : $"▶  {o.Name}";
    }

    private void BuildOfficerCards(CityId city, int cmdIndex)
    {
        Clear(_modalOfficers);
        var cmd = Cmds[cmdIndex];
        var cityData = _state.Cities.First(x => x.Id == city);
        var free = _state.GeneralsAt(city).Where(g => !_state.IsGeneralBusy(g)).OrderBy(g => g.Value).ToList();
        if (free.Count == 0)
        {
            _modalOfficers.AddChild(MakeLabel("(가능한 장수 없음)", 14, Parchment));
            return;
        }

        foreach (var gid in free)
        {
            var g = _state.Generals.First(x => x.Id == gid);
            var home = g.Region.Length > 0 && g.Region == cityData.Region ? "  🏠" : "";
            var stat = cmd.Kind is CommandKind.Research or CommandKind.CityStratagem ? $"지 {g.Intellect}"
                : cmd.Kind == CommandKind.Train ? $"무 {g.Might}" : $"정 {g.Politics}";
            var card = OfficerCard(g.Name + home, stat);
            var captured = gid;
            card.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { AskExecute(city, cmdIndex, captured, _modalParam); }
            };
            _modalOfficers.AddChild(card);
        }
    }

    private PanelContainer OfficerCard(string name, string stat)
    {
        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(161, 107),
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
        };
        card.AddThemeStyleboxOverride("panel", CardBox(false));
        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.AddThemeConstantOverride("separation", 4);
        card.AddChild(v);
        v.AddChild(new TextureRect
        {
            Texture = Icon(Sym.Officer),
            CustomMinimumSize = new Vector2(43, 43),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        });
        var nm = MakeLabel(name, 19, Parchment);
        nm.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(nm);
        var st = MakeLabel(stat, 16, GoldBright);
        st.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(st);
        card.MouseEntered += () => card.AddThemeStyleboxOverride("panel", CardBox(false, hover: true));
        card.MouseExited += () => card.AddThemeStyleboxOverride("panel", CardBox(false));
        return card;
    }

    private StyleBoxFlat CardBox(bool selected, bool hover = false) => selected
        ? Frame(AccentFill, GoldBright, 2, 9, 9)
        : hover ? Frame(InkHover, GoldBright, 2, 9, 9) : Frame(InkSoft, Gold, 1, 9, 9);

    private Control GoldRule()
    {
        var rule = new HSeparator();
        rule.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = new Color(Gold, 0.5f), ContentMarginTop = 1, ContentMarginBottom = 1 });
        return rule;
    }

    private static string ClassName(TroopClass c) => c switch
    {
        TroopClass.Infantry => "보병",
        TroopClass.Archer => "궁병",
        TroopClass.Cavalry => "기병",
        TroopClass.Elephant => "상병",
        TroopClass.Siege => "공성",
        TroopClass.Naval => "해상",
        _ => "",
    };

    private static Color ClassColor(TroopClass c) => c switch
    {
        TroopClass.Infantry => new Color(0.62f, 0.66f, 0.72f),
        TroopClass.Archer => new Color(0.45f, 0.72f, 0.42f),
        TroopClass.Cavalry => new Color(0.80f, 0.62f, 0.36f),
        TroopClass.Elephant => new Color(0.72f, 0.72f, 0.76f),
        TroopClass.Siege => new Color(0.85f, 0.52f, 0.28f),
        TroopClass.Naval => new Color(0.36f, 0.68f, 0.78f),
        _ => Gold,
    };

    // 병종 분류 표식 — 분류색 광택 구체 + 금테 링(방향광 램버트 음영 + 스페큘러 + 드롭섀도우).
    // 병종별 실제 이미지가 있으면 그것을 우선 사용(없으면 절차적 엠블럼). 파일: assets/icons/troop_{code}.png
    private static readonly Dictionary<TroopClass, string> EmblemFiles = new()
    {
        [TroopClass.Cavalry] = "res://assets/icons/troop_cavalry.png",
        [TroopClass.Infantry] = "res://assets/icons/troop_infantry.png",
        [TroopClass.Archer] = "res://assets/icons/troop_archer.png",
        [TroopClass.Elephant] = "res://assets/icons/troop_elephant.png",
        [TroopClass.Siege] = "res://assets/icons/troop_siege.png",
        [TroopClass.Naval] = "res://assets/icons/troop_naval.png",
    };

    private ImageTexture ClassEmblem(TroopClass c)
    {
        if (_emblems.TryGetValue(c, out var cached)) { return cached; }

        if (EmblemFiles.TryGetValue(c, out var path) && Godot.FileAccess.FileExists(path))
        {
            var loaded = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
            loaded.GenerateMipmaps();
            var lt = ImageTexture.CreateFromImage(loaded);
            _emblems[c] = lt;
            return lt;
        }

        var img = NewBig();
        var col = ClassColor(c);
        var cx = IconBig / 2f;
        var cy = IconBig / 2f;
        var r = IconBig * 0.40f;
        var goldW = IconBig * 0.055f;
        const float lx = -0.5f;
        const float ly = -0.62f;
        const float lz = 0.60f;
        for (var y = 0; y < IconBig; y++)
        {
            var rimShade = Mathf.Lerp(1.18f, 0.72f, (float)y / (IconBig - 1));
            for (var x = 0; x < IconBig; x++)
            {
                var dd = System.MathF.Sqrt(((x - cx) * (x - cx)) + ((y - cy) * (y - cy)));

                var covB = Mathf.Clamp(((r - dd) / 1.7f) + 0.5f, 0f, 1f);
                if (covB > 0f)
                {
                    var nx = (x - cx) / r;
                    var ny = (y - cy) / r;
                    var nz = System.MathF.Sqrt(System.MathF.Max(0f, 1f - (nx * nx) - (ny * ny)));
                    var lambert = Mathf.Clamp((nx * lx) + (ny * ly) + (nz * lz), 0f, 1f);
                    var sh = 0.55f + (0.75f * lambert);
                    BlendPix(img, x, y, new Color(Mathf.Clamp(col.R * sh, 0, 1), Mathf.Clamp(col.G * sh, 0, 1), Mathf.Clamp(col.B * sh, 0, 1)), covB);
                }

                var rimIn = Mathf.Clamp(((dd - (r - 0.8f)) / 1.7f) + 0.5f, 0f, 1f);
                var rimOut = Mathf.Clamp((((r + goldW) - dd) / 1.7f) + 0.5f, 0f, 1f);
                var covG = rimIn * rimOut;
                if (covG > 0f)
                {
                    BlendPix(img, x, y, new Color(Mathf.Clamp(Gold.R * rimShade, 0, 1), Mathf.Clamp(Gold.G * rimShade, 0, 1), Mathf.Clamp(Gold.B * rimShade, 0, 1)), covG);
                }
            }
        }

        GlossU(img, (IconUnits * 0.5f) - 3.0f, (IconUnits * 0.5f) - 3.6f, 6.5f, 0.55f);
        var tex = Shadowed(img);
        _emblems[c] = tex;
        return tex;
    }

    private readonly Dictionary<string, ImageTexture> _stratIcons = new();

    // 계략별 실제 이미지(assets/icons/strat_{code}.png)가 있으면 우선, 없으면 기존 심볼 폴백.
    private ImageTexture StratIcon(string code)
    {
        if (_stratIcons.TryGetValue(code, out var cached)) { return cached; }

        var path = $"res://assets/icons/strat_{code}.png";
        if (Godot.FileAccess.FileExists(path))
        {
            var loaded = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
            loaded.GenerateMipmaps();
            var lt = ImageTexture.CreateFromImage(loaded);
            _stratIcons[code] = lt;
            return lt;
        }

        return Icon(code switch
        {
            "scout" => Sym.People,
            "wall_break" => Sym.Wall,
            "incite" => Sym.Shield,
            "steal" => Sym.Coin,
            "sow_discord" => Sym.Officer,
            _ => Sym.Scroll,
        });
    }

    private static string StratDesc(string code) => code switch
    {
        "scout" => "적 도시 정보 획득 (전제)",
        "wall_break" => "성벽 −10%",
        "incite" => "치안 −10",
        "arson" => "군량 −20%",
        "steal" => "금 20% 절취",
        "sow_discord" => "충성 −20",
        _ => "",
    };

    private void AskExecute(CityId city, int cmdIndex, GeneralId general, int p)
    {
        var cmd = Cmds[cmdIndex];
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
            CloseModal();
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
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, 28) };
        b.AddThemeFontOverride("font", _font);
        b.AddThemeFontSizeOverride("font_size", 13);
        b.AddThemeColorOverride("font_color", accent ? Ink : Parchment);
        b.AddThemeColorOverride("font_hover_color", accent ? Ink : GoldBright);
        b.AddThemeColorOverride("font_pressed_color", GoldBright);
        b.AddThemeStyleboxOverride("normal", Frame(accent ? AccentFill : InkSoft, Gold, 1, 5, 6));
        b.AddThemeStyleboxOverride("hover", Frame(accent ? GoldBright : InkHover, GoldBright, 1, 5, 6));
        b.AddThemeStyleboxOverride("pressed", Frame(AccentFill, Gold, 1, 5, 6));
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
        DrawSupplyZones();
        DrawDeployPaths();

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
        panel.Position = new Vector2(12, 12);
        panel.CustomMinimumSize = new Vector2(400, 0);
        panel.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 8, 10));
        layer.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        panel.AddChild(box);

        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 10);
        box.AddChild(top);
        _status = MakeLabel("", 15, Gold);
        _status.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(_status);
        var advance = MakeButton("▶ 진행 (7일)", accent: true);
        advance.CustomMinimumSize = new Vector2(110, 30);
        advance.Pressed += OnAdvance;
        top.AddChild(advance);

        _log = MakeLabel("", 12, Parchment);
        _log.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_log);
    }
}
