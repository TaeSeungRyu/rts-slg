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
    private static readonly string[] OfficerConfirmLines =
    {
        "신에게 맡겨 주세요.",
        "반드시 성과를 올리겠습니다.",
        "주군의 뜻을 받들겠습니다.",
        "이 일은 제가 책임지겠습니다.",
        "오늘의 결단이 내일의 승세가 될 것입니다.",
        "명만 내려 주십시오.",
    };

    private Font _font = null!;
    private readonly System.Random _confirmRandom = new();

    private MapView3D _view = null!;
    private CameraController3D _camera = null!;
    private FactionAI _ai = null!;
    private DeployService _deployer = null!;
    private FieldUnitCommandService _unitCommander = null!;
    private ProductionService _producer = null!;
    private CampaignEngine _engine = null!;
    private CommandService _commander = null!;
    private IReadOnlyList<TroopTemplate> _troops = null!;
    private CommandBalance _cb = null!;
    private BalanceConfig _balance = null!;
    private GameState _state = null!;
    private int _week;

    private readonly Dictionary<int, Label3D> _cityLabels = new();
    private readonly Dictionary<int, UnitController3D> _armyTokens = new();
    private readonly Dictionary<int, Label3D> _armyLabels = new();
    private readonly Dictionary<int, UnitController3D> _productionTokens = new();
    private readonly Dictionary<int, Label3D> _productionLabels = new();
    private Label _status = null!;
    private Label _hudRuler = null!;
    private Label _hudDate = null!;
    private TextureRect _hudFace = null!;
    private PanelContainer _hudFacePanel = null!;
    private PanelContainer _reportPanel = null!;   // 좌하단 보고(삼국지11 오마주 결과창)
    private ScrollContainer _reportScroll = null!; // 보고 내용 스크롤(이전 내용 열람)
    private VBoxContainer _reportBox = null!;
    private const int ReportBoxMax = 80;           // 좌하단 스크롤 박스에 유지하는 최근 줄 수(전체는 [전체] 모달)
    private const int ReportHistoryMax = 300;      // 전체 로그 보관 상한(오래된 것부터 버림)
    private readonly List<(string Text, Color Color)> _pendingReport = new(); // 이번 진행 결과(재생 끝나면 flush)
    private readonly List<(string Text, Color Color)> _reportHistory = new(); // 전체 로그(스크롤 열람용)
    private Label _log = null!;

    // 진행 애니메이션(2.5초=하루 = 이동 1.5초 + 공격 1초, 한 칸 0.5초, 최대 3칸/일). StepSeconds를 키우면 이동이 느려진다.
    private const double DaySeconds = 2.5;
    private const double StepSeconds = 0.5;
    private const double MoveSeconds = 1.5; // 하루 2.5초 중 이동 몫(나머지 1초 = 공격)
    private const int AnimDays = 7;
    private bool _advancing;
    private double _animT;
    private int _animStepIdx;
    private readonly List<(double Time, int UnitId, HexCoord To)> _animSteps = new();
    private int _animAtkIdx;
    private readonly List<(double Time, int UnitId, Vector3 FaceTo)> _animAttacks = new(); // 교전·공성 공격 모션
    private int _animUpdIdx;
    private readonly List<(double Time, int UnitId, int Troops)> _animUpdates = new(); // 병력 갱신(라벨·편대 규모)
    private int _animKillIdx;
    private readonly List<(double Time, int UnitId)> _animKills = new(); // 전멸·입성 — 토큰 즉시 제거
    private int _animProductionKillIdx;
    private readonly List<(double Time, int OperationId)> _animProductionKills = new(); // 생산 부대 시설 도착 — 토큰 제거
    private int _animEffectIdx;
    private readonly List<(double Time, Vector3 Pos)> _animDeathEffects = new(); // 병력 전멸·생산 소실 1회성 효과
    private int _animDmgIdx;
    private readonly List<(double Time, int UnitId, int Damage)> _animDmg = new(); // 교전 피해 팝업
    private int _animSiegeDmgIdx;
    private readonly List<(double Time, Vector3 Pos, int Damage)> _animSiegeDmg = new(); // 성 피해 팝업(성벽+수비)
    private int _animArrowIdx;
    private readonly List<(double Time, Vector3 From, int TargetUnitId)> _animArrows = new(); // 성 반격 화살
    private int _animSupplyArrowIdx;
    private readonly List<(double Time, int UnitId, Vector3 Target)> _animSupplyArrows = new(); // 보급부대 공격 화살

    // 병력 → 편대원 수(design-ui §3): 9천↑=9, 7천↑=7, 5천↑=5, 3천↑=3, 그 밑=1.
    private static int FormationFor(int troops) =>
        troops >= 9000 ? 9 : troops >= 7000 ? 7 : troops >= 5000 ? 5 : troops >= 3000 ? 3 : 1;

    // 병종 코드 → UnitController3D.TroopModels 인덱스(모델 파일 순서와 일치해야 한다).
    private static readonly Dictionary<string, int> TroopModelIndex = new()
    {
        ["swordsman"] = 0, ["cavalry"] = 1, ["archer"] = 2, ["thunder_cart"] = 3,
        ["catapult"] = 4, ["siege_tower"] = 5, ["war_elephant"] = 6, ["small_boat"] = 7,
        ["medium_ship"] = 8, ["large_ship"] = 9, ["geukbyeong"] = 10, ["namman"] = 11,
        ["deunggap"] = 12, ["mudang"] = 13, ["cataphract"] = 14, ["hwarang"] = 15,
        ["horse_archer"] = 16, ["turtleship"] = 17, ["waeseon"] = 18,
    };
    private GameState _pendingState = null!;
    private string _pendingNote = "";
    private AdvanceButton _advanceBtn = null!;
    private Label _dayLabel = null!;
    private Label _dayTurnLabel = null!;               // N일차 아래 "이동턴/공격턴"
    private readonly string[] _dayKind = new string[AnimDays + 1]; // 1..AnimDays: "이동"/"공격"

    // 명령 UX(성 클릭 → 정보 카드 + 명령 목록 → 파라미터·장수 목록 → 컨펌).
    private CityId? _selected;
    private int _cmdIndex = -1;
    private Control _infoCard = null!;
    private VBoxContainer _infoRows = null!;
    private PanelContainer _cmdMenu = null!;
    private PanelContainer _cmdSubMenu = null!; // 그룹 클릭 시 팔레트 옆에 뜨는 명령 플라이아웃
    private VBoxContainer _cmdSubList = null!;
    private int _openGroup = -1;
    private VBoxContainer _cmdList = null!;
    private PanelContainer _unitMenu = null!; // 유닛 명령 팔레트(정보·이동 재지정)
    private VBoxContainer _unitCmdBox = null!; // 이동·계략 섹션 — 아군·평시에만 표시
    private int _selectedUnitId = -1;
    private int _retargetUnitId = -1;  // ≥0이면 야전 부대 이동 재지정 목표 지정 중
    private UnitMode _retargetMode;
    private string _dataDirectory = "";
    private bool _leftDown;            // 좌클릭 vs 좌드래그(맵 이동) 구분용
    private Vector2 _leftDownPos;

    // 지형 정보 카드(클릭 지점 위에 떠오름 — 상단 3D 에셋+이름, 하단 정보).
    private PanelContainer _terrainCard = null!;
    private SubViewport _terrainViewport = null!;
    private Node3D _terrainHolder = null!;
    private Camera3D _terrainCam = null!;
    private Label _terrainName = null!;
    private VBoxContainer _terrainInfo = null!;
    private HexCoord? _terrainHex;
    private OptionButton? _paramSel;
    private CanvasLayer? _confirmLayer; // 커스텀 컨펌창(시스템 다이얼로그 대체 — 게임 스타일·한글 버튼)
    private MeshInstance3D? _ring;
    private MeshInstance3D _hover = null!;
    private ImageTexture _blankIcon = null!;

    // 시설 배치(건설) — 반투명 고스트가 커서를 따라다니고, 평지·숲 유효 칸에서만 설치 컨펌이 뜬다.
    private Node3D _facilityLayer = null!;   // 완성 시설 + 공사중 모델을 담는 컨테이너(Redraw마다 재구성)
    private bool _placing;
    private string _placeCode = "";
    private CityId _placeCity;
    private int _placeCmdIndex;
    private GeneralId _placeGeneral;
    private int _placeParam;
    private Node3D? _placeGhost;
    private MeshInstance3D? _placeMarker;
    private HexCoord? _placeValidHex;
    private CanvasLayer? _placeDim;   // 배치 중 화면 전체를 살짝 어둡게
    private ImageTexture _dotIcon = null!;

    // 명령 모달(명령 클릭 → 큰 창 + 아이콘 카드 그리드 → 카드 선택 → 장수 클릭 = 실행).
    private CanvasLayer? _modalLayer;
    private int _modalParam;
    private VBoxContainer _modalOfficers = null!; // 수행 장수 표 홀더
    private int _cityDetailTab; // 성 상세 활성 탭(0=주둔·1=명령·2=예약)
    private CityId? _openCityDetailCity; // 열린 성 상세 모달이 있으면 진행 완료 후 새 수치로 다시 그린다.
    private CityId? _stratTarget; // 도시 계략 대상 도시(선택 UI)
    private int _offSortCol = -1; // -1 = 명령 관련 능력치 내림차순(기본)
    private bool _offSortAsc;
    private Label? _modalDetail;
    private readonly List<PanelContainer> _optionCards = new();
    private readonly List<PanelContainer> _autoRecruitRateCards = new();
    private readonly HashSet<int> _disabledOptions = new();
    private readonly HashSet<int> _modalMultiParams = new();
    private int _autoRecruitRateParam = 1;
    private readonly Dictionary<int, SpinBox> _researchFundingRatios = new();
    private Label? _researchFundingPreview;
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
    private readonly List<(SupplyDeployRequest Req, string Label)> _pendingSupplyDeploys = new();

    // 출전 모달(허브=예약 목록 / 편성 화면) + 수량/미리보기.
    private CityId _depModalCity;
    private int _depAmount;
    private int _depEditIndex = -1; // -1=신규 추가, ≥0=_pendingDeploys 해당 예약 수정
    private UnitMode _depMode = UnitMode.Advance;
    private HexCoord? _depTarget;
    private SpinBox? _depAmountSpin;
    private Label? _depPreview;
    private readonly List<(Button Btn, UnitMode Mode)> _depModeButtons = new();
    private Label? _depModeDesc;
    private IReadOnlyList<ActiveSkill> _activeSkills = [];
    private IReadOnlyList<PassiveSkill> _passiveSkills = [];
    private IReadOnlyList<AdminSkill> _adminSkills = [];
    private IReadOnlyDictionary<string, AdminSkill> _adminSkillMap = new Dictionary<string, AdminSkill>();
    private Tree? _vanTree;              // 장수 편성 표(선봉·부관 체크 + 정렬·내부 스크롤)
    private List<GeneralId> _composeFree = new();
    private int _vanSortCol = 2;         // 2 이름 / 3 무 / 4 지 / 5 정 / 6 적성·특성
    private bool _vanSortAsc = true;
    private int _depProvDays; // 출전 시 휴대할 군량 일수(슬라이더). 0이면 군량 없이 나감
    private HSlider? _depProvSlider;
    private Label? _depProvLabel;
    private int _provPer10kPerDay = 10; // 병력 1만당 하루 군량 소모(balance) — 일수↔군량 환산
    private readonly Dictionary<string, int> _supplyDraft = new(System.StringComparer.Ordinal);
    private readonly List<(PanelContainer Card, string Code)> _supplyTroopCards = new();
    private int _supplyEditIndex = -1;
    private bool _targetingSupplyDeploy;
    private string _dbgLog = ""; // 출전 디버그 로그 파일 경로(res://deploy-debug.log)
    private const float FacilityDisplayScale = 0.5f;

    // 진행 상세 로그 — 진행 조각(turn)별 이동/교전/소멸, 공성/함락/약탈, 주말 요약.
    // 분석 규약: u{id}=부대, city{id}=성, 피해는 -N, 획득/회복은 +N.
    private void LogAdvanceDetail(Dictionary<int, HexCoord> startHex, IReadOnlyList<AdvanceTurn> turns,
        IReadOnlyList<SiegeExchange> sieges, IReadOnlyList<CaptureReport> captures,
        IReadOnlyList<PlunderReport> plunders, GameState after)
    {
        string U(GeneralId? g) => g is { } id ? (_state.Generals.FirstOrDefault(x => x.Id == id)?.Name ?? $"G{id.Value}") : "-";
        var pos = new Dictionary<int, HexCoord>(startHex);
        for (var ti = 0; ti < turns.Count; ti++)
        {
            var t = turns[ti];
            Dbg($"  turn[{ti}] days={t.Movement.Days} stop={t.Movement.Reason}");
            foreach (var u in t.Units.OrderBy(x => x.Id.Value))
            {
                if (pos.TryGetValue(u.Id.Value, out var f) && f != u.Field.Position)
                {
                    Dbg($"    move u{u.Id.Value}: ({f.Q},{f.R}) -> ({u.Field.Position.Q},{u.Field.Position.R})");
                }

                pos[u.Id.Value] = u.Field.Position;
            }

            if (t.Combat is { } c && c.DamageDealt.Count > 0)
            {
                Dbg("    combat dealt: " + string.Join(" ", c.DamageDealt.OrderBy(k => k.Key.Value).Select(kv => $"u{kv.Key.Value}:+{kv.Value}")));
                Dbg("    combat taken: " + string.Join(" ", c.DamageTaken.OrderBy(k => k.Key.Value).Select(kv => $"u{kv.Key.Value}:-{kv.Value}")));
            }

            if (t.FiredActives.Count > 0) { Dbg("    actives: " + string.Join(" ", t.FiredActives.OrderBy(k => k.Key.Value).Select(kv => $"u{kv.Key.Value}:{kv.Value.Name}"))); }
            if (t.FiredStratagems.Count > 0) { Dbg("    strats: " + string.Join(" ", t.FiredStratagems.OrderBy(k => k.Key.Value).Select(kv => $"u{kv.Key.Value}:{kv.Value.Name}"))); }
            if (t.StatusDamage.Count > 0) { Dbg("    statusDmg: " + string.Join(" ", t.StatusDamage.OrderBy(k => k.Key.Value).Select(kv => $"u{kv.Key.Value}:-{kv.Value}"))); }
            if (t.StratagemDamage.Count > 0) { Dbg("    stratDmg: " + string.Join(" ", t.StratagemDamage.OrderBy(k => k.Key.Value).Select(kv => $"u{kv.Key.Value}:-{kv.Value}"))); }
            if (t.Starvation.Count > 0) { Dbg("    starve: " + string.Join(" ", t.Starvation.OrderBy(k => k.Key.Value).Select(kv => $"u{kv.Key.Value}:-{kv.Value}"))); }
            if (t.Reinforced.Count > 0) { Dbg("    reinforced: " + string.Join(" ", t.Reinforced.OrderBy(k => k.Key.Value).Select(kv => $"u{kv.Key.Value}:+{kv.Value}"))); }
            if (t.EnteredCastle.Count > 0) { Dbg("    entered: " + string.Join(" ", t.EnteredCastle.Select(u => $"u{u.Id.Value}(troops {u.Pool.Active})"))); }

            var ids = t.Units.Select(x => x.Id.Value).ToHashSet();
            foreach (var d in pos.Keys.Where(k => !ids.Contains(k)).OrderBy(k => k).ToList())
            {
                Dbg($"    removed: u{d} ({(t.EnteredCastle.Any(u => u.Id.Value == d) ? "입성" : "전멸")})");
                pos.Remove(d);
            }
        }

        foreach (var ex in sieges)
        {
            var counters = ex.BesiegerDamage is { } bd
                ? string.Join(" ", ex.Besiegers.Zip(bd, (b, d) => $"u{b.Value}:-{d}"))
                : "-";
            Dbg($"  siege turn[{ex.TurnIndex}] city{ex.City.Value} wall -{ex.WallDamage} -> {ex.NewWall} defTroopDmg=-{ex.TroopDamage} counter: {counters}");
        }

        foreach (var cp in captures) { Dbg($"  capture: {cp}"); }
        foreach (var pl in plunders) { Dbg($"  plunder: city{pl.City.Value} {pl.Facility} looter=u{pl.Looter.Value} gold+{pl.Gold} prov+{pl.Provisions}"); }

        Dbg($"  == week-end {after.Year}y {after.Month}m {after.DayOfMonth}d (day {after.Day}) ==");
        foreach (var c in after.Cities.OrderBy(c => c.Id.Value))
        {
            var garrison = after.Garrisons.Where(g => g.City == c.Id).Sum(g => g.Troops);
            Dbg($"  city{c.Id.Value} {c.Name} owner={c.Owner.Value} wall={c.Wall} prov={c.Provisions} gold={c.Gold} garrison={garrison} 대기병력=[{string.Join(" ", after.Garrisons.Where(g => g.City == c.Id).OrderBy(g => g.TroopCode, System.StringComparer.Ordinal).Select(g => $"{g.TroopCode}{(g.Trainee ? "*신병" : "")}:{g.Troops}(훈{g.TrainingLevel})"))}]");
        }

        foreach (var cmd2 in after.Commands.OrderBy(x => x.CompletionDay))
        {
            Dbg($"  cmd city{cmd2.City.Value} {KindName(cmd2.Kind)} 완료 day{cmd2.CompletionDay} (남은 {cmd2.CompletionDay - after.Day}일)");
        }

        foreach (var u in after.Armies.OrderBy(u => u.Id.Value))
        {
            Dbg($"  army u{u.Id.Value} owner={u.Field.Owner.Value} {u.TroopCode} pos=({u.Field.Position.Q},{u.Field.Position.R}) troops={u.Pool.Active}(wounded {u.Pool.Wounded}) mode={u.Field.Mode} tgt={(u.Field.Target is { } t2 ? $"({t2.Q},{t2.R})" : "none")} wps=[{string.Join(",", (u.Field.Waypoints ?? []).Select(w => $"({w.Q},{w.R})"))}] prov={u.Provisions} van={U(u.VanguardId)} adj={U(u.AdjutantId)}");
        }
    }

    private void Dbg(string msg)
    {
        try { System.IO.File.AppendAllText(_dbgLog, msg + "\n"); } catch { }
    }

    // 목표 지정 모드(지도 클릭으로 예약 부대의 목적지 설정).
    private bool _depTargeting;
    private int _depTargetIndex = -1;
    private int _depSelectedUnit = -1; // 허브에서 선택된 예약 부대(컨트롤 바 대상)
    private CanvasLayer? _targetHintLayer;
    private Button _targetConfirmBtn = null!;
    private readonly List<MeshInstance3D> _previewMarkers = new(); // 미확정 목적지 경로 프리뷰
    private CanvasLayer _targetEditLayer = null!;              // 경유지 취소 버튼·확인 버튼 레이어
    private readonly List<HexCoord> _targetWaypoints = new();  // 클릭 순서대로 찍은 경유지(마지막 = 최종 목표)
    private readonly List<Button> _targetCancelBtns = new();   // 경유지별 취소 버튼(각 지점 위에 추종)
    private HexCoord _targetStart;                             // 경로 시작점(성/부대 위치)

    // 모달 드래그.
    private bool _dragging;
    private Control? _dragPanel;
    private Vector2 _dragOffset;

    // 경로 프리뷰.
    private PassabilityMap _passability = null!;
    private readonly List<MeshInstance3D> _pathMarkers = new();
    private Mesh? _pathDotMesh;
    private Material? _pathDotMat;

    // 1단계 지원 명령(전투 중심 v2 팔레트에서 노출할 기존 명령만 연결).
    private static readonly (string Label, CommandKind Kind, string Param)[] Cmds =
    {
        ("모병", CommandKind.Recruit, "troop"),
        ("징병", CommandKind.Conscript, "troop"),
        ("훈련", CommandKind.Train, "garrison"),
        ("세율", CommandKind.SetTaxRate, "tax"),
        ("건설", CommandKind.Build, "facility"),
        ("주력병종", CommandKind.SelectMajorTroop, "major"),
        ("전투 교리", CommandKind.Research, "troop"),
        ("성벽 강화", CommandKind.Research, "wall"),
        ("성벽 수리", CommandKind.Repair, "wall"),
        ("시설 수리", CommandKind.Repair, "repairable"),
        ("도시 계략", CommandKind.CityStratagem, "stratagem"),
        ("동맹", CommandKind.FormAlliance, "faction"),
        ("동맹파기", CommandKind.BreakAlliance, "faction"),
        ("태수 임명", CommandKind.AppointGovernor, ""),
        ("군사 임명", CommandKind.AppointStrategist, ""),
        ("치안 담당", CommandKind.AppointSecurityOfficer, ""),
        ("내정 담당", CommandKind.AppointDomesticOfficer, ""),
        ("병력 담당", CommandKind.AppointRecruitmentOfficer, ""),
        ("훈련 담당", CommandKind.AppointTrainingOfficer, ""),
        ("위인 영입", CommandKind.RecruitHero, "hero"),
        ("탐색", CommandKind.Explore, ""),
    };

    private static readonly (string Label, string Code)[] Facilities =
    {
        ("논", "paddy"), ("밭", "farm"), ("마을", "village"), ("공방", "workshop"),
    };

    private static readonly (string Label, string Code)[] Repairables =
    {
        ("논", "paddy"), ("밭", "farm"), ("마을", "village"), ("공방", "workshop"),
        ("광산", "mine"), ("목장", "ranch"), ("상원", "elephant_garden"),
    };

    private static readonly (string Label, string Code)[] Strats =
    {
        ("정찰", "scout"), ("성벽파괴", "wall_break"), ("선동", "incite"),
        ("방화", "arson"), ("절취", "steal"),
    };

    // v2 명령 카테고리. 반복 내정(모병·징병·세율·시장·건설·등용)은 팔레트에서 숨긴다.
    private static readonly (string Group, int[] Indices)[] CmdGroups =
    {
        ("연구", new[] { 5, 6 }),
        ("성벽", new[] { 7, 8 }),
        ("계략", new[] { 10 }),
        ("외교", new[] { 11, 12 }),
        ("임명", new[] { 13, 14 }),
        ("담당자", new[] { 15, 16, 17, 18 }),
        ("인재", new[] { 19 }),
        ("탐색", new[] { 20 }),
    };

    private static readonly Sym[] CmdIcons = { Sym.Sword, Sym.Coin, Sym.Book, Sym.Wall, Sym.Scroll };

    public void Build(MapView3D view, CameraController3D camera, string dataDirectory)
    {
        _view = view;
        _camera = camera;
        _dataDirectory = dataDirectory;
        _font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf");

        _troops = new TroopTypeLoader().LoadFromDirectory(dataDirectory);
        _cb = new CommandBalanceLoader().LoadFromDirectory(dataDirectory);
        var actives = new ActiveSkillLoader().LoadFromDirectory(dataDirectory);
        var passives = new PassiveSkillLoader().LoadFromDirectory(dataDirectory);
        _activeSkills = actives;
        _passiveSkills = passives;
        _adminSkills = new AdminSkillLoader().LoadFromDirectory(dataDirectory);
        _adminSkillMap = _adminSkills.ToDictionary(s => s.Code, System.StringComparer.Ordinal);
        _balance = new BalanceConfig(MonthlyTaxPerCity: 100);
        _provPer10kPerDay = _balance.ProvisionsPer10kPerDay;

        _commander = new CommandService(_cb, _troops, _balance, _adminSkills);
        _deployer = new DeployService(_cb, _troops, actives, passives, _adminSkills);
        _ai = new FactionAI(_commander, _deployer);
        _passability = new PassabilityMap(_map, [], _cities);
        _unitCommander = new FieldUnitCommandService((domain, hex) => _passability.CanEnter(domain, hex));
        _producer = new ProductionService(_troops, h => _passability.CanEnter(MovementDomain.Land, h));
        var movement = new MovementSimulator(_passability);
        // 플레이 세션에서는 탐색·외교 결과가 매 실행 같은 초반 난수열에 묶이지 않도록 세션 시드를 쓴다.
        // Core 테스트는 WorldEngine에 고정 IRandomSource를 주입해 결정론을 유지한다.
        var sessionSeed = unchecked((int)(System.DateTime.UtcNow.Ticks ^ System.Environment.TickCount64));
        var world = new WorldEngine(_balance, _cb, random: new SeededRandomSource(sessionSeed));
        _engine = new CampaignEngine(
            new AdvanceOrchestrator(movement, new CombatPhaseResolver(new BattleResolver(60), 70)),
            world,
            new CampaignSiege(new BattleResolver(60), _troops),
            new CityCapture(), new SeededRandomSource(42),
            new CityPlunder(_cb), _cb.CityResupplyRadius,
            _cb.BuildSiteHp, _cb.BuildSiteDamagePerTurn);
        var scenario = new ScenarioLoader().LoadFromDirectory(dataDirectory);
        _vision = new BattlefieldVision(scenario.Balance, _troops);
        _fog = new BattlefieldFogView(_view);
        _fog.RegisterMap();
        _state = _initial with
        {
            StartYear = 190,
            Factions = scenario.Factions,
            Generals = scenario.Generals,
            HeroUnlockDefinitions = scenario.HeroUnlockList,
            HeroUnlockStates = scenario.HeroUnlockList
                .Select(h => new HeroUnlockState(h.General, HeroUnlockStatus.Locked))
                .ToList(),
        };

        _dbgLog = ProjectSettings.GlobalizePath("res://deploy-debug.log");
        try { System.IO.File.WriteAllText(_dbgLog, "=== maptest deploy debug ===\n"); } catch { }
        GD.Print("[deploy-log] " + _dbgLog);

        _blankIcon = SolidIcon(1, (_, _) => new Color(0, 0, 0, 0));
        _dotIcon = SolidIcon(14, (x, y) => System.Math.Abs(x - 7) + System.Math.Abs(y - 7) <= 4 ? GoldBright : new Color(0, 0, 0, 0));

        SpawnCastles();
        SpawnHover();
        BuildHud();
        BuildPanel();
        camera.Setup(_view.HexToWorld(new HexCoord(4, 2)), 14f);
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

        _facilityLayer = new Node3D();
        AddChild(_facilityLayer);
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

    // 선택적 텍스처 로더 — 파일이 있으면 싣고, 없으면 null(플레이스홀더로 대체). 향후 아트가
    // 준비되면 파일만 넣으면 UI에 그대로 반영된다.
    private readonly Dictionary<string, ImageTexture?> _optionalTextures = new();
    private ImageTexture? LoadOptionalTexture(string resPath)
    {
        if (_optionalTextures.TryGetValue(resPath, out var cached)) { return cached; }
        ImageTexture? tex = null;
        if (Godot.FileAccess.FileExists(resPath))
        {
            var img = Image.LoadFromFile(ProjectSettings.GlobalizePath(resPath));
            img.GenerateMipmaps();
            tex = ImageTexture.CreateFromImage(img);
        }

        _optionalTextures[resPath] = tex;
        return tex;
    }

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
            // 시설 배치 중: 고스트가 커서를 따라다니며 유효/무효 색을 바꾼다(일반 호버는 숨긴다).
            if (_placing)
            {
                _hover.Visible = false;
                UpdatePlacementHover(motion.Position);
                return;
            }

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

        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            if (_placing)
            {
                FinishPlacement();
                _log.Text = "설치를 취소했습니다.";
                return;
            }

            if (_depTargeting)
            {
                var wasUnit = _retargetUnitId >= 0;
                FinishTargeting();
                if (!wasUnit) { OpenDeployHub(); }
                return;
            }

            CloseAnyModalOrPanel();
            return;
        }

        if (@event is not InputEventMouseButton mb)
        {
            return;
        }

        // 우클릭: 목표 지정/시설 배치 취소.
        if (mb.ButtonIndex == MouseButton.Right && mb.Pressed)
        {
            if (_placing)
            {
                FinishPlacement();
                _log.Text = "설치를 취소했습니다.";
                return;
            }

            if (_depTargeting)
            {
                var wasUnit = _retargetUnitId >= 0;
                FinishTargeting();
                if (!wasUnit) { OpenDeployHub(); }
                return;
            }

            CloseAnyModalOrPanel();
            return;
        }

        if (mb.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        // 좌클릭 vs 좌드래그(맵 이동) 구분: 눌렀다 뗀 지점 이동량이 작으면 '클릭'.
        if (mb.Pressed)
        {
            _leftDownPos = mb.Position;
            _leftDown = true;
            return;
        }

        if (!_leftDown) { return; }
        _leftDown = false;
        if ((mb.Position - _leftDownPos).Length() >= 6f) { return; } // 드래그 = 카메라 팬(선택 아님)

        // 시설 배치 중: 유효 칸(평지·숲)이면 설치 컨펌으로, 무효 칸이면 아무것도 하지 않는다.
        if (_placing)
        {
            if (_placeValidHex is { } plot)
            {
                var (pCity, pIdx, pGen, pParam) = (_placeCity, _placeCmdIndex, _placeGeneral, _placeParam);
                FinishPlacement();
                AskExecute(pCity, pIdx, pGen, pParam, plot); // 설치 컨펌 → 발행
            }
            else
            {
                _log.Text = "평지·숲 위에만 설치할 수 있습니다.";
                ShowNotice("실행 불가", _log.Text);
            }

            return;
        }

        // ── 좌'클릭' 처리 ──
        // 목표 지정 중: 목적지를 '가리키기'만 하고, 마우스 옆 '확인'을 눌러야 확정된다.
        // 경로 프리뷰는 확인 전에도 즉시 보여준다.
        if (_depTargeting)
        {
            // 클릭할 때마다 경유지를 이어 붙인다(직전 지점에서 A* 경로가 있어야 추가).
            if (RayToGround(mb.Position) is { } th && th != LastTargetPoint())
            {
                var from = LastTargetPoint();
                if (HasPath(from, th))
                {
                    _targetWaypoints.Add(th);
                    RebuildTargetEdit();
                }
                else
                {
                    _log.Text = "그 지점까지 갈 수 있는 경로가 없습니다.";
                }
            }

            return;
        }

        if (RayToGround(mb.Position) is not { } hex)
        {
            _selected = null;
            HidePanels();
            return;
        }

        // 유닛 클릭 → 유닛 명령 팔레트(같은 칸에 겹치면 아군·id 우선). 같은 유닛 재클릭 = 닫기.
        var unit = DisplayedArmies.Where(u => u.Field.Position == hex && CanSeeUnit(u))
            .OrderBy(u => u.Field.Owner == Player ? 0 : 1).ThenBy(u => u.Id.Value)
            .FirstOrDefault();
        if (unit is not null)
        {
            if (_unitMenu.Visible && _selectedUnitId == unit.Id.Value) { HidePanels(); return; }
            OpenUnitMenu(unit);
            return;
        }

        var city = _state.Cities.FirstOrDefault(c => c.Position == hex);
        if (city is not null)
        {
            // 같은 성 재클릭 = 닫기.
            if (_infoCard.Visible && _selected == city.Id) { _selected = null; HidePanels(); return; }
            SelectCity(city.Id);
            return;
        }

        // 빈 바닥 클릭 → 맵(지형) 정보. 같은 타일 재클릭 = 닫기.
        if (_terrainCard.Visible && _terrainHex == hex) { HidePanels(); return; }
        ShowMapInfo(hex);
    }

    // 유닛 팔레트 열기 — 성 팔레트처럼 유닛 화면좌표 옆에 띄운다.
    private void OpenUnitMenu(CombatUnit u)
    {
        if (!CanSeeUnit(u)) return;
        _selectedUnitId = u.Id.Value;
        _selected = null;
        _infoCard.Visible = false;
        _cmdMenu.Visible = false;
        _terrainCard.Visible = false;
        _terrainHex = null;
        _unitCmdBox.Visible = u.Field.Owner == Player && !_advancing; // 적·재생 중엔 정보만
        PlaceMenu(_unitMenu, u.Field.Position, 60f);
        _unitMenu.Visible = true;
        MoveRing(u.Field.Position);
        DrawSupplyZones();

        // 아군 부대를 클릭했을 때만 그 부대의 이동 경로를 표시(경유지가 있으면 경유지까지 이어서).
        ClearPathMarkers();
        if (u.Field.Owner == Player && u.Field.Target is { } tgt && tgt != u.Field.Position)
        {
            AddRouteDots(u.Field.Position, u.Field.Waypoints, tgt, _pathMarkers);
        }
    }

    // 시작 → (경유지들) → 목표를 구간별로 이어 금색 점 경로를 그린다.
    private void AddRouteDots(HexCoord start, IReadOnlyList<HexCoord>? waypoints, HexCoord target, List<MeshInstance3D> into)
    {
        var prev = start;
        foreach (var wp in waypoints ?? [])
        {
            AddPathDots(prev, wp, into);
            prev = wp;
        }

        AddPathDots(prev, target, into);
    }

    // 유닛 상태를 정보 카드에 표시(팔레트 '정보').
    private void ShowUnitInfo(int unitId)
    {
        var u = DisplayedArmies.FirstOrDefault(a => a.Id.Value == unitId);
        if (u is null || !CanSeeUnit(u)) { return; }

        var tmpl = _troops.FirstOrDefault(t => t.Code == u.TroopCode);
        var faction = _state.Factions.FirstOrDefault(f => f.Id == u.Field.Owner);
        var van = u.VanguardId is { } vid ? _state.Generals.FirstOrDefault(g => g.Id == vid)?.Name : null;
        var adj = u.AdjutantId is { } aid ? _state.Generals.FirstOrDefault(g => g.Id == aid)?.Name : null;

        Clear(_infoRows);
        _infoRows.AddChild(MakeLabel($"《 {tmpl?.Name ?? u.TroopCode} 》 {faction?.Name}", 15, GoldBright));
        _infoRows.AddChild(MakeLabel($"시야 {_vision.UnitRadius(u)}칸", 13, Parchment));
        if (u.VanguardId is { } vanguardId)
        {
            var faceRow = new HBoxContainer();
            faceRow.AddThemeConstantOverride("separation", 8);
            _infoRows.AddChild(faceRow);
            if (CircularPortraitFor(vanguardId) is { } face)
            {
                var frame = new PanelContainer { CustomMinimumSize = new Vector2(74, 74) };
                frame.AddThemeStyleboxOverride("panel", Frame(new Color(0.075f, 0.06f, 0.05f), Gold, 1, 37, 0));
                frame.AddChild(new TextureRect
                {
                    Texture = face,
                    CustomMinimumSize = new Vector2(70, 70),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                });
                faceRow.AddChild(frame);
            }

            var names = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            names.AddChild(MakeLabel($"선봉 {van ?? "—"}", 13, GoldBright));
            names.AddChild(MakeLabel($"부관 {adj ?? "—"}", 12, Parchment));
            faceRow.AddChild(names);
        }

        var g = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        g.AddThemeConstantOverride("h_separation", 10);
        g.AddThemeConstantOverride("v_separation", 5);
        _infoRows.AddChild(g);

        void Row(string k, string v)
        {
            g.AddChild(MakeLabel(k, 12, Parchment));
            g.AddChild(MakeLabel(v, 12, GoldBright));
        }

        Row("병력", $"{u.Pool.Active}");
        Row("부상병", u.Pool.Wounded > 0 ? $"{u.Pool.Wounded}" : "없음");
        Row("선봉", van ?? "—");
        Row("부관", adj ?? "—");
        Row("훈련", $"{u.Training}");
        Row("모드", ModeName(u.Field.Mode));
        Row("목표", u.Field.Target is { } t ? $"({t.Q}, {t.R})" : "없음");
        Row("군량", u.TracksProvisions ? $"{u.Provisions}" : "무한");
        if (u.IsSupply)
        {
            Row("보급범위", $"{FieldSupplyRadius}칸");
            Row("전투규칙", "공격 가능 · 스킬/적성 미적용 · 공방 최하");
        }
        Row("선봉 액티브", ActiveSlotText(u.State.VanguardActive, u.State.VanguardGauge));
        Row("부관 액티브", ActiveSlotText(u.State.AdjutantActive, u.State.AdjutantGauge));

        foreach (var (role, skill) in new[] { ("선봉", u.State.VanguardActive), ("부관", u.State.AdjutantActive) })
        {
            if (skill is null) { continue; }
            var button = MakeButton($"{role} · {skill.Name}   ›");
            button.Alignment = HorizontalAlignment.Left;
            button.ClipText = true;
            button.TooltipText = SkillDescriptions.Active(skill);
            ActiveSkillIcons.Apply(button, skill.Code, 32);
            button.Pressed += () => ShowSkillDescription(skill.Name, "액티브", SkillDescriptions.Active(skill), skill.Code);
            _infoRows.AddChild(button);
        }

        _infoCard.Visible = true;
    }

    private static string ActiveSlotText(ActiveSkill? skill, ActiveGauge gauge)
    {
        if (skill is null) { return "없음"; }
        return gauge.IsReady
            ? $"{skill.Name} 준비"
            : $"{skill.Name} {gauge.ElapsedDays}/{ActiveGauge.ReadyDays}일";
    }

    // 메뉴를 지정 헥사의 화면좌표 우측에 배치(화면 밖 clamp).
    private void PlaceMenu(PanelContainer menu, HexCoord at, float offsetX)
    {
        var world = _view.HexToWorld(at) + new Vector3(0f, _view.TileTopY, 0f);
        var screen = _camera.UnprojectPosition(world);
        var sz = menu.GetCombinedMinimumSize();
        var vp = GetViewport().GetVisibleRect().Size;
        var px = Mathf.Clamp(screen.X + offsetX, 8f, System.Math.Max(8f, vp.X - sz.X - 8f));
        var py = Mathf.Clamp(screen.Y - sz.Y * 0.5f, 8f, System.Math.Max(8f, vp.Y - sz.Y - 8f));
        menu.Position = new Vector2(px, py);
    }

    // 지형 정보 카드 — 클릭 지점 위에 떠오른다. 상단 3D 에셋+이름, 하단 이동·전투 보정.
    private void ShowMapInfo(HexCoord h)
    {
        if (!_visibleTiles.Contains(h))
        {
            HidePanels();
            ShowNotice("시야 밖", "아군 성·부대를 가까이 이동시키거나 적 성을 정찰하면 주변 정보를 볼 수 있습니다.");
            return;
        }
        _selected = null;
        _cmdMenu.Visible = false;
        _unitMenu.Visible = false;
        _selectedUnitId = -1;
        _infoCard.Visible = false;
        ClearPathMarkers();
        ClearSupplyZoneMarkers();

        var inMap = _map.Contains(h);
        var terrain = inMap ? _passability.TerrainAt(h) : TerrainType.Plains;
        // 그 타일에 건설한 시설이 있으면 지형 대신 시설로 표기·미리보기(지형 데이터는 평지 그대로여도
        // 사용자에겐 논·밭·마을·공방으로 보여야 한다).
        var placement = inMap ? FacilityPlacementAt(h) : null;
        var pendingBuild = inMap ? PendingFacilityBuildAt(h) : null;
        var facility = placement?.Code ?? pendingBuild?.Facility;
        var previewTerrain = facility is { } fc ? FacilityTerrain(fc) : terrain;

        // 상단: 지형/시설 에셋 모델 미리보기(이전 모델 제거 후 교체).
        foreach (var c in _terrainHolder.GetChildren()) { c.QueueFree(); }
        _terrainHolder.Rotation = Vector3.Zero; // 프레이밍은 회전 0 기준(이후 _Process가 빙글 회전)
        if (inMap && _view.TileScene(previewTerrain) is { } scene)
        {
            var inst = scene.Instantiate<Node3D>();
            inst.Position = Vector3.Zero;
            _terrainHolder.AddChild(inst);
            FrameTerrainCamera(inst); // 모델 실제 크기(AABB)에 맞춰 카메라 배치
        }

        _terrainName.Text = !inMap ? "맵 밖" : facility is { } fn ? FacilityName(fn) : TerrainName(terrain);

        // 하단: 이동·전투 보정.
        Clear(_terrainInfo);
        void Row(string k, string v, Color? valueColor = null)
        {
            var hb = new HBoxContainer();
            hb.AddThemeConstantOverride("separation", 8);
            var kl = MakeLabel(k, 11, Parchment);
            kl.CustomMinimumSize = new Vector2(38, 0);
            hb.AddChild(kl);
            var vl = MakeLabel(v, 11, valueColor ?? GoldBright);
            vl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            vl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            hb.AddChild(vl);
            _terrainInfo.AddChild(hb);
        }

        Row("좌표", $"({h.Q}, {h.R})");
        Button? productionButton = null;
        if (facility is not null)
        {
            Row("지형", $"{TerrainName(terrain)} 위 건설");
            if (pendingBuild is not null)
            {
                Row("상태", $"건설중 · 남은 {System.Math.Max(0, pendingBuild.CompletionDay - _state.Day)}일",
                    new Color(0.98f, 0.78f, 0.42f));
            }

            if (placement is not null)
            {
                var pendingUpgrade = _state.Commands.FirstOrDefault(c => c.Kind == CommandKind.Upgrade
                    && c.City == placement.City && c.Plot == placement.Plot);
                var production = ProductionAt(placement.Plot);
                Row("효과", FacilityEffectText(placement.Code, placement.HitPoints));
                Row("체력", $"{placement.HitPoints}");
                if (pendingUpgrade is not null)
                {
                    Row("진행", $"업그레이드 · 남은 {System.Math.Max(0, pendingUpgrade.CompletionDay - _state.Day)}일");
                }
                if (production is not null)
                {
                    var gName = OfficerName(production.General) ?? $"G{production.General.Value}";
                    var troopName = TroopName(production.TroopCode);
                    var status = production.Phase switch
                    {
                        ProductionPhase.Outbound => $"이동중 · {gName} · {troopName} {production.Troops}명",
                        ProductionPhase.Gathering => $"채집중 · {gName} · {troopName} {production.Troops}명 · 남은 {production.GatherRemaining(_state.Day)}일",
                        ProductionPhase.Returning => $"복귀중 · {gName} · {troopName} {production.Troops}명",
                        _ => "",
                    };
                    Row("생산", status, GoldBright);
                }
                else if (ProductionRules.IsProductionFacility(placement.Code)
                    && _state.Cities.FirstOrDefault(c => c.Id == placement.City)?.Owner == Player)
                {
                    productionButton = MakeButton("생산");
                    productionButton.Disabled = _advancing;
                    productionButton.CustomMinimumSize = new Vector2(0, 30);
                    productionButton.Pressed += () => OpenProductionModal(placement.City, placement.Plot);
                }
            }
            else
            {
                Row("효과", FacilityEffectText(facility));
                Row("체력", "건설 완료 후 1000");
            }

            Row("방어", $"최하 방어 {FacilityHealth.Defense} · 공격/반격 없음");
        }
        Row("이동", inMap ? MoveCostText(terrain, h) : "통행 불가");
        var combat = CombatBonusText(terrain);
        Row("전투", combat.Length > 0 ? combat : "병종 보정 없음");

        // 아군 성 보급 반경 안이면 표시(초록 타일과 같은 의미 — 이 안에선 성이 군량을 채워준다).
        var supplier = _cb.CityResupplyRadius > 0 && inMap
            ? _state.Cities.Where(c => c.Owner == Player && c.Position.Distance(h) <= _cb.CityResupplyRadius)
                .OrderBy(c => c.Position.Distance(h)).ThenBy(c => c.Id.Value).FirstOrDefault()
            : null;
        if (supplier is not null)
        {
            Row("보급", $"보급지역 ({supplier.Name}) — 아군 부대 군량 자동 보충", new Color(0.45f, 0.85f, 0.52f));
        }
        if (FieldSupplierAt(h) is { } fieldSupply)
        {
            var leader = fieldSupply.VanguardId is { } vid ? OfficerName(vid) : null;
            var label = string.IsNullOrWhiteSpace(leader) ? $"U{fieldSupply.Id.Value}" : leader;
            Row("보급부대", $"{label} 보충 범위 — 아군 부대 군량 자동 보충", new Color(0.92f, 0.58f, 1.0f));
        }
        if (productionButton is not null)
        {
            _terrainInfo.AddChild(productionButton);
        }

        _terrainHex = h;
        PlaceTerrainCard(h);
        _terrainCard.Visible = true;
        MoveRing(h);
    }

    private CombatUnit? FieldSupplierAt(HexCoord h)
        => _state.Armies
            .Where(u => u.Field.Owner == Player && u.IsSupply && u.Pool.Active > 0 && u.Provisions > 0
                && u.Field.Position.Distance(h) <= FieldSupplyRadius)
            .OrderBy(u => u.Field.Position.Distance(h))
            .ThenBy(u => u.Id.Value)
            .FirstOrDefault();

    // 미리보기 카메라를 모델 AABB(월드)에 맞춰 배치 — 지형마다 native 크기가 달라도 꽉 차게.
    private void FrameTerrainCamera(Node3D model)
    {
        var bounds = ModelAabb(model);
        var center = bounds.GetCenter();
        var radius = Mathf.Max(0.35f, bounds.Size.Length() * 0.5f);
        var dist = radius / Mathf.Tan(Mathf.DegToRad(_terrainCam.Fov * 0.5f)) * 1.25f;
        var dir = new Vector3(0f, 0.8f, 1.0f).Normalized(); // 앞쪽 위에서 내려다봄
        _terrainCam.Position = center + (dir * dist);
        _terrainCam.LookAt(center, Vector3.Up);
    }

    private static Aabb ModelAabb(Node3D model)
    {
        Aabb? acc = null;
        var stack = new System.Collections.Generic.Stack<Node>();
        stack.Push(model);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (n is VisualInstance3D vi)
            {
                var box = TransformAabb(vi.GlobalTransform, vi.GetAabb());
                acc = acc is null ? box : acc.Value.Merge(box);
            }

            foreach (var c in n.GetChildren()) { stack.Push(c); }
        }

        return acc ?? new Aabb(Vector3.Zero, Vector3.One);
    }

    private static Aabb TransformAabb(Transform3D xf, Aabb a)
    {
        var min = xf * a.Position;
        var max = min;
        for (var i = 1; i < 8; i++)
        {
            var corner = a.Position + new Vector3(
                (i & 1) * a.Size.X, ((i >> 1) & 1) * a.Size.Y, ((i >> 2) & 1) * a.Size.Z);
            var w = xf * corner;
            min = min.Min(w);
            max = max.Max(w);
        }

        return new Aabb(min, max - min);
    }

    // 지형 카드를 클릭한 헥사 화면좌표 '위'에 배치(가운데 정렬, 화면 밖 clamp).
    private void PlaceTerrainCard(HexCoord h)
    {
        var world = _view.HexToWorld(h) + new Vector3(0f, _view.TileTopY + 0.3f, 0f);
        var screen = _camera.UnprojectPosition(world);
        var sz = _terrainCard.GetCombinedMinimumSize();
        var vp = GetViewport().GetVisibleRect().Size;
        var px = Mathf.Clamp(screen.X - sz.X / 2f, 8f, System.Math.Max(8f, vp.X - sz.X - 8f));
        var py = Mathf.Clamp(screen.Y - sz.Y - 14f, 8f, System.Math.Max(8f, vp.Y - sz.Y - 8f));
        _terrainCard.Position = new Vector2(px, py);
    }

    // 이동 비용 표기(design-movement 지형 패널티). 소형산·늪·소하천 = 2, 통행 불가 = 표시, 그 외 1.
    private string MoveCostText(TerrainType t, HexCoord h)
    {
        if (!_passability.CanEnter(MovementDomain.Land, h)) { return "통행 불가"; }
        return t is TerrainType.Mountain or TerrainType.Swamp or TerrainType.River
            ? "2칸 (지형 패널티)"
            : "1칸";
    }

    // 병종별 전투 보정(TerrainCombatBonus). 보정 있는 병종만 나열.
    private static string CombatBonusText(TerrainType t)
    {
        var parts = new List<string>();
        foreach (var (cls, name) in new[]
        {
            (TroopClass.Infantry, "보병"), (TroopClass.Archer, "궁병"),
            (TroopClass.Cavalry, "기병"), (TroopClass.Elephant, "상병"),
        })
        {
            var (atk, df) = TerrainCombatBonus.For(cls, t);
            if (atk == 0 && df == 0) { continue; }
            var s = name + " ";
            if (atk != 0) { s += $"공+{atk} "; }
            if (df != 0) { s += $"방+{df}"; }
            parts.Add(s.Trim());
        }

        return string.Join(", ", parts);
    }

    private static string TerrainName(TerrainType t) => t switch
    {
        TerrainType.Plains => "평지",
        TerrainType.Forest => "숲",
        TerrainType.Mountain => "산",
        TerrainType.Desert => "사막",
        TerrainType.River => "강(소하천)",
        TerrainType.Bridge => "다리",
        TerrainType.WaterShallow => "얕은 물",
        TerrainType.WaterDeep => "깊은 물",
        TerrainType.Rocks => "바위",
        TerrainType.RockHill => "돌언덕",
        TerrainType.WaterRocks => "물속 바위",
        TerrainType.Paddy => "논",
        TerrainType.Farm => "밭",
        TerrainType.Workshop => "공방",
        TerrainType.RockMountain => "바위산",
        TerrainType.Karst => "카르스트",
        TerrainType.Cliff => "절벽",
        TerrainType.IceMountain => "설산",
        TerrainType.IceWallLarge => "빙벽(대)",
        TerrainType.IceWallSmall => "빙벽(소)",
        TerrainType.Swamp => "늪",
        TerrainType.DesertCactus => "선인장 사막",
        TerrainType.PortSmall => "포구",
        TerrainType.Village1 or TerrainType.Village2 or TerrainType.Village3
            or TerrainType.Village4 or TerrainType.Village5 => "마을",
        _ => t.ToString(),
    };

    // ── 목표 지정 ──
    private void BeginTargeting(int idx)
        => BeginTargeting(idx, supply: false);

    private void BeginSupplyTargeting(int idx)
        => BeginTargeting(idx, supply: true);

    private void BeginTargeting(int idx, bool supply)
    {
        Dbg($"UI targeting-begin idx={idx} supply={supply}");
        CloseModal();
        HidePanels(); // 목표 지정 중에는 성 명령 팔레트·정보 카드가 가려선 안 된다.
        _depTargetIndex = idx;
        _targetingSupplyDeploy = supply;
        _depTargeting = true;
        _targetWaypoints.Clear();
        var reqCity = supply ? _pendingSupplyDeploys[idx].Req.City : _pendingDeploys[idx].Req.City;
        _targetStart = _state.Cities.FirstOrDefault(c => c.Id == reqCity)?.Position ?? default;
        RebuildTargetEdit();
        ShowTargetHint(supply
            ? "보급부대 목표 지정 · 지점을 순서대로 클릭 · '확인'으로 확정 · 출전 후 공격 명령 가능 · 우클릭 취소"
            : "지점을 순서대로 클릭 = 경유지 추가  ·  각 지점 위 취소로 삭제  ·  '확인'으로 확정  ·  적 성 = 공격  ·  우클릭 취소");
    }

    // 경로의 마지막 확정 지점(경유지가 없으면 시작점).
    private HexCoord LastTargetPoint() => _targetWaypoints.Count > 0 ? _targetWaypoints[^1] : _targetStart;

    // start→goal에 지형 통행 A* 경로가 존재하는가.
    private bool HasPath(HexCoord start, HexCoord goal)
    {
        var pf = new HexPathfinder(c => c == start || c == goal || _passability.CanEnter(MovementDomain.Land, c));
        return pf.FindPath(start, goal).Count > 1;
    }

    // 경유지 목록이 바뀔 때: 취소 버튼을 다시 만들고 경로 프리뷰를 다시 그린다.
    private void RebuildTargetEdit()
    {
        foreach (var b in _targetCancelBtns) { b.QueueFree(); }
        _targetCancelBtns.Clear();
        foreach (var m in _previewMarkers) { m.QueueFree(); }
        _previewMarkers.Clear();

        // 프리뷰: 시작점 → 경유지들을 구간별로 이어 그린다.
        var prev = _targetStart;
        foreach (var wp in _targetWaypoints)
        {
            AddPathDots(prev, wp, _previewMarkers);
            prev = wp;
        }

        for (var i = 0; i < _targetWaypoints.Count; i++)
        {
            var idx = i;
            var b = MakeButton("✕");
            b.AddThemeFontSizeOverride("font_size", 12);
            b.CustomMinimumSize = new Vector2(28, 24);
            b.Pressed += () =>
            {
                if (idx < _targetWaypoints.Count) { _targetWaypoints.RemoveAt(idx); }
                Dbg($"UI targeting-cancel-wp idx={idx} remain={_targetWaypoints.Count}");
                RebuildTargetEdit(); // 중간 지점을 지우면 남은 지점으로 경로가 자동 재계산된다
            };
            _targetEditLayer.AddChild(b);
            _targetCancelBtns.Add(b);
        }

        _targetConfirmBtn.Visible = _targetWaypoints.Count > 0;
        PlaceTargetEdit();
    }

    // 취소 버튼(각 경유지 위)·확인 버튼(마지막 경유지 오른쪽)을 월드→화면 투영으로 배치. _Process가 매 프레임 호출.
    private void PlaceTargetEdit()
    {
        var vp = GetViewport().GetVisibleRect().Size;
        for (var i = 0; i < _targetCancelBtns.Count && i < _targetWaypoints.Count; i++)
        {
            var world = _view.HexToWorld(_targetWaypoints[i]) + new Vector3(0f, _view.TileTopY + 0.2f, 0f);
            var s = _camera.UnprojectPosition(world);
            var sz = _targetCancelBtns[i].GetCombinedMinimumSize();
            _targetCancelBtns[i].Position = new Vector2(
                Mathf.Clamp(s.X - sz.X * 0.5f, 4f, vp.X - sz.X - 4f),
                Mathf.Clamp(s.Y - sz.Y - 22f, 4f, vp.Y - sz.Y - 4f));
        }

        if (_targetConfirmBtn.Visible && _targetWaypoints.Count > 0)
        {
            var world = _view.HexToWorld(_targetWaypoints[^1]) + new Vector3(0f, _view.TileTopY + 0.2f, 0f);
            var s = _camera.UnprojectPosition(world);
            var sz = _targetConfirmBtn.GetCombinedMinimumSize();
            _targetConfirmBtn.Position = new Vector2(
                Mathf.Clamp(s.X + 20f, 4f, vp.X - sz.X - 4f),
                Mathf.Clamp(s.Y - sz.Y * 0.5f, 4f, vp.Y - sz.Y - 4f));
        }
    }

    // 화면 상단 목표 지정 안내 배너.
    private void ShowTargetHint(string text)
    {
        var layer = new CanvasLayer { Layer = 25 };
        AddChild(layer);
        _targetHintLayer = layer;
        var pc = new PanelContainer();
        pc.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop, Control.LayoutPresetMode.KeepSize, 16);
        pc.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 8, 10));
        layer.AddChild(pc);
        pc.AddChild(MakeLabel(text, 15, GoldBright));
    }

    private void FinishTargeting()
    {
        _depTargeting = false;
        _depTargetIndex = -1;
        _targetingSupplyDeploy = false;
        _retargetUnitId = -1;
        _targetConfirmBtn.Visible = false;
        _targetWaypoints.Clear();
        foreach (var b in _targetCancelBtns) { b.QueueFree(); }
        _targetCancelBtns.Clear();
        foreach (var m in _previewMarkers) { m.QueueFree(); }
        _previewMarkers.Clear();
        if (_targetHintLayer is not null) { _targetHintLayer.QueueFree(); _targetHintLayer = null; }
    }

    // 목적지 '확인' — 여기서만 목표가 확정된다. 마지막 경유지 = 최종 목표, 나머지 = 경유지.
    private void ConfirmTarget()
    {
        if (_targetWaypoints.Count == 0) { return; }
        var target = _targetWaypoints[^1];
        var mid = _targetWaypoints.Count > 1
            ? _targetWaypoints.Take(_targetWaypoints.Count - 1).ToList()
            : null;
        if (_retargetUnitId >= 0) { ApplyUnitTarget(target, mid); }
        else { ApplyTarget(target, mid); }
    }

    private void ApplyTarget(HexCoord h, IReadOnlyList<HexCoord>? waypoints)
    {
        var idx = _depTargetIndex;
        if (_targetingSupplyDeploy && idx >= 0 && idx < _pendingSupplyDeploys.Count)
        {
            var (req, label) = _pendingSupplyDeploys[idx];
            var enemyCity = _state.Cities.FirstOrDefault(c => c.Position == h && c.Owner != Player);
            var enemyUnit = DisplayedArmies.FirstOrDefault(u => u.Field.Position == h && u.Field.Owner != Player && CanSeeUnit(u));
            var mode = enemyCity is not null || enemyUnit is not null ? UnitMode.Attack : UnitMode.March;
            _pendingSupplyDeploys[idx] = (req with { Target = h, Mode = mode }, label);
            Dbg($"SUPPLY TARGET idx={idx} -> ({h.Q},{h.R}) mode={mode}");
            var tName = _state.Cities.FirstOrDefault(c => c.Position == h)?.Name ?? $"({h.Q},{h.R})";
            _log.Text = $"보급부대 목표 → {tName}{(mode == UnitMode.Attack ? " (공격모드)" : "")} · 목표 확정";
        }
        else if (idx >= 0 && idx < _pendingDeploys.Count)
        {
            var (req, label) = _pendingDeploys[idx];
            var enemyCity = _state.Cities.FirstOrDefault(c => c.Position == h && c.Owner != Player);
            var mode = enemyCity is not null ? UnitMode.Attack : req.Mode;
            _pendingDeploys[idx] = (req with { Target = h, Mode = mode, Waypoints = waypoints }, label);
            Dbg($"TARGET idx={idx} -> ({h.Q},{h.R}) mode={mode} wps={waypoints?.Count ?? 0}");
            var tName = _state.Cities.FirstOrDefault(c => c.Position == h)?.Name ?? $"({h.Q},{h.R})";
            var wpNote = waypoints is { Count: > 0 } ? $" · 경유 {waypoints.Count}" : "";
            _log.Text = $"목표 → {tName}{(enemyCity is not null ? " (공격모드)" : "")}{wpNote} · 목표 확정";
        }

        // 목표를 정하면 지도 뷰로 돌아가 경로를 바로 보여준다(허브 모달로 가리지 않는다).
        // 이어서 편성하려면 성 팔레트의 '출전'을 다시 누른다.
        FinishTargeting();
        SelectCity(_depModalCity);
        Redraw(_log.Text);
    }

    private readonly List<MeshInstance3D> _supplyMarkers = new();
    private readonly List<(MeshInstance3D Marker, int UnitId, Vector3 Offset)> _movingSupplyMarkers = new();
    private Mesh? _supplyTileMesh;
    private Material? _supplyTileMat;
    private Material? _fieldSupplyTileMat;
    private const int FieldSupplyRadius = AdvanceOrchestrator.DefaultResupplyRadius;
    private const int SupplyExtraCarryDays = 20;

    private void ClearSupplyZoneMarkers()
    {
        foreach (var m in _supplyMarkers) { m.QueueFree(); }
        _supplyMarkers.Clear();
        _movingSupplyMarkers.Clear();
    }

    // ── 보급 영역: 아군 성 반경(city_resupply_radius) 안을 초록 타일로 표시 ──
    // 부대가 나가 있을 때(또는 출전 예약이 있을 때)만 보여, 이 영역을 벗어나면 휴대 군량으로
    // 버텨야 함을 알린다.
    private void DrawSupplyZones()
    {
        ClearSupplyZoneMarkers();
        var radius = _cb.CityResupplyRadius;
        var selectedCity = _selected is { } cityId
            ? _state.Cities.FirstOrDefault(c => c.Id == cityId && c.Owner == Player)
            : null;
        var selectedSupply = _selectedUnitId >= 0
            ? _state.Armies.FirstOrDefault(u => u.Id.Value == _selectedUnitId
                && u.Field.Owner == Player && u.IsSupply && u.Pool.Active > 0 && u.Provisions > 0)
            : null;
        if (selectedCity is null && selectedSupply is null) { return; }

        // 타일 윗면은 모서리가 깎여(bevel) 실제 육각보다 좁다 — 조금 줄여 침범처럼 보이지 않게.
        var hexR = _view.HexWorldSize * 0.86f;
        _supplyTileMesh ??= new CylinderMesh { TopRadius = hexR, BottomRadius = hexR, Height = 0.02f, RadialSegments = 6 };
        _supplyTileMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.22f, 0.72f, 0.34f, 0.40f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = new Color(0.24f, 0.80f, 0.38f),
            EmissionEnergyMultiplier = 1.0f,
            RenderPriority = -2, // 성 이름·병력 라벨(Label3D)보다 먼저 그려 글씨를 가리지 않게
        };
        _fieldSupplyTileMat ??= new StandardMaterial3D
        {
            AlbedoColor = new Color(0.82f, 0.28f, 1.0f, 0.46f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            EmissionEnabled = true,
            Emission = new Color(0.92f, 0.36f, 1.0f),
            EmissionEnergyMultiplier = 1.15f,
            RenderPriority = -1,
        };

        void AddMarker(HexCoord hex, Material mat, float yOffset)
        {
            var marker = new MeshInstance3D
            {
                Mesh = _supplyTileMesh,
                MaterialOverride = mat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = _view.HexToWorld(hex) + new Vector3(0f, _view.TileTopY + yOffset, 0f),
            };
            AddChild(marker);
            _supplyMarkers.Add(marker);
        }

        if (selectedCity is not null && radius > 0)
        {
            // 성 발자국 타일(통행 불가)도 성의 일부이므로 영역에 포함 — 성 아래가 구멍으로 보이지 않게.
            var footprint = CastleFootprint.TilesFor(selectedCity).ToHashSet();
            for (var dq = -radius; dq <= radius; dq++)
            {
                for (var dr = System.Math.Max(-radius, -dq - radius); dr <= System.Math.Min(radius, -dq + radius); dr++)
                {
                    var hex = new HexCoord(selectedCity.Position.Q + dq, selectedCity.Position.R + dr);
                    if (!_map.Contains(hex)) { continue; }
                    if (!footprint.Contains(hex) && !_passability.CanEnter(MovementDomain.Land, hex)) { continue; }
                    AddMarker(hex, _supplyTileMat, 0.02f);
                }
            }
        }

        if (selectedSupply is { } supply)
        {
            for (var dq = -FieldSupplyRadius; dq <= FieldSupplyRadius; dq++)
            {
                for (var dr = System.Math.Max(-FieldSupplyRadius, -dq - FieldSupplyRadius);
                     dr <= System.Math.Min(FieldSupplyRadius, -dq + FieldSupplyRadius);
                     dr++)
                {
                    var hex = new HexCoord(supply.Field.Position.Q + dq, supply.Field.Position.R + dr);
                    if (!_map.Contains(hex)) { continue; }
                    if (!_passability.CanEnter(MovementDomain.Land, hex)
                        && !_state.Cities.Any(c => CastleFootprint.TilesFor(c).Contains(hex))) { continue; }
                    AddMarker(hex, _fieldSupplyTileMat, 0.045f);
                    var marker = _supplyMarkers[^1];
                    var center = _view.HexToWorld(supply.Field.Position);
                    _movingSupplyMarkers.Add((marker, supply.Id.Value, marker.Position - center));
                }
            }
        }
    }

    private void ClearPathMarkers()
    {
        foreach (var m in _pathMarkers) { m.QueueFree(); }
        _pathMarkers.Clear();
    }

    // ── 경로 프리뷰: 예약 부대의 성→목표 경로 — 편성(허브/편성) 모달이 열려 있는 동안만 표시 ──
    private void DrawDeployPaths()
    {
        ClearPathMarkers();
        if (_modalLayer is null) { return; } // 편성 중이 아니면 경로를 지도에 남기지 않는다

        foreach (var (req, _) in _pendingDeploys)
        {
            if (req.Target is not { } goal) { continue; }
            var city = _state.Cities.FirstOrDefault(c => c.Id == req.City);
            if (city is null) { continue; }
            AddRouteDots(city.Position, req.Waypoints, goal, _pathMarkers);
        }

        foreach (var (req, _) in _pendingSupplyDeploys)
        {
            if (req.Target is not { } goal) { continue; }
            var city = _state.Cities.FirstOrDefault(c => c.Id == req.City);
            if (city is null) { continue; }
            AddPathDots(city.Position, goal, _pathMarkers);
        }
    }

    // start→goal A* 경로를 금색 점으로 그려 into에 담는다(공용 — 확정 경로·목표 지정 프리뷰).
    private void AddPathDots(HexCoord start, HexCoord goal, List<MeshInstance3D> into)
    {
        _pathDotMesh ??= new CylinderMesh { TopRadius = 0.12f, BottomRadius = 0.12f, Height = 0.05f, RadialSegments = 8 };
        _pathDotMat ??= new StandardMaterial3D
        {
            AlbedoColor = GoldBright,
            EmissionEnabled = true,
            Emission = Gold,
            EmissionEnergyMultiplier = 1.4f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

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
            into.Add(dot);
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

    // ── 테스트 시나리오: 지형 다양 맵, 위(성 1) vs 촉(성 2 — 성도·한중) ──
    private static readonly HexMap _map = BuildTestMap();

    /// <summary>렌더(GameRoot3D)와 시뮬(passability)이 같은 지형을 쓰도록 공유하는 맵.</summary>
    public static HexMap TestMap => _map;

    // 지형 확인용 배치(성 발자국 (1,2)(1,3)(0,3)·(8,3)(8,4)(7,4)는 평지로 비움).
    // 성↔성 이동로(대략 r2~3)는 통행 가능한 지형 위주로 둬 AI/부대가 막히지 않게 한다.
    private static HexMap BuildTestMap()
    {
        var t = new Dictionary<HexCoord, TerrainType>
        {
            [new(3, 1)] = TerrainType.Forest, [new(4, 1)] = TerrainType.Forest, [new(3, 2)] = TerrainType.Forest,
            [new(5, 0)] = TerrainType.Mountain, [new(6, 1)] = TerrainType.Mountain,
            [new(2, 4)] = TerrainType.Mountain, [new(6, 4)] = TerrainType.Mountain,
            [new(4, 4)] = TerrainType.Rocks, [new(5, 5)] = TerrainType.Rocks,
            [new(2, 0)] = TerrainType.RockHill, [new(7, 1)] = TerrainType.RockHill,
            [new(6, 2)] = TerrainType.Desert, [new(7, 2)] = TerrainType.Desert, [new(7, 3)] = TerrainType.DesertCactus,
            [new(3, 4)] = TerrainType.Swamp, [new(4, 3)] = TerrainType.Swamp,
            [new(1, 0)] = TerrainType.Karst, [new(0, 5)] = TerrainType.Cliff, [new(9, 0)] = TerrainType.RockMountain,
        };
        // 사방 +2칸 — 성 보급 반경(3칸)이 지도 안에 온전히 보이도록.
        return new HexMap(-2, 11, -2, 7, t);
    }

    private static readonly IReadOnlyList<City> _cities = new List<City>
    {
        new(new CityId(1), "장안", new HexCoord(1, 2), new FactionId(1), 3000, CastleSize.Medium,
            Gold: 2000, Security: 80, Population: 100_000, Ore: 8000, Horses: 3000, Elephants: 30,
            Paddies: 2, Farms: 2, Villages: 2, Wall: 1200),
        new(new CityId(2), "성도", new HexCoord(8, 3), new FactionId(2), 3000, CastleSize.Medium,
            Gold: 2000, Security: 80, Population: 100_000, Ore: 8000,
            Paddies: 2, Farms: 2, Villages: 2, Wall: 1200),
        new(new CityId(3), "한중", new HexCoord(4, 6), new FactionId(2), 3000, CastleSize.Medium,
            Gold: 2000, Security: 80, Population: 80_000, Ore: 6000,
            Paddies: 2, Farms: 2, Villages: 2, Wall: 1200),
    };

    private static readonly IReadOnlyList<FacilityPlacement> _initialFacilityPlacements = new List<FacilityPlacement>
    {
        new(new CityId(1), new HexCoord(0, 2), "village", FacilityHealth.Level1),
        new(new CityId(1), new HexCoord(2, 2), "village", FacilityHealth.Level1),
        new(new CityId(1), new HexCoord(0, 1), "paddy", FacilityHealth.Level1),
        new(new CityId(1), new HexCoord(1, 1), "paddy", FacilityHealth.Level1),
        new(new CityId(1), new HexCoord(2, 1), "farm", FacilityHealth.Level1),
        new(new CityId(1), new HexCoord(2, 3), "farm", FacilityHealth.Level1),

        new(new CityId(2), new HexCoord(7, 2), "village", FacilityHealth.Level1),
        new(new CityId(2), new HexCoord(8, 2), "village", FacilityHealth.Level1),
        new(new CityId(2), new HexCoord(9, 2), "paddy", FacilityHealth.Level1),
        new(new CityId(2), new HexCoord(9, 3), "paddy", FacilityHealth.Level1),
        new(new CityId(2), new HexCoord(7, 3), "farm", FacilityHealth.Level1),
        new(new CityId(2), new HexCoord(9, 4), "farm", FacilityHealth.Level1),

        new(new CityId(3), new HexCoord(3, 5), "village", FacilityHealth.Level1),
        new(new CityId(3), new HexCoord(4, 5), "village", FacilityHealth.Level1),
        new(new CityId(3), new HexCoord(5, 5), "paddy", FacilityHealth.Level1),
        new(new CityId(3), new HexCoord(5, 6), "paddy", FacilityHealth.Level1),
        new(new CityId(3), new HexCoord(3, 6), "farm", FacilityHealth.Level1),
        new(new CityId(3), new HexCoord(4, 7), "farm", FacilityHealth.Level1),
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
            Officer(11), Officer(12), Officer(13), Officer(14),
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
            new(new GeneralId(13), new FactionId(2), new CityId(3)),
            new(new GeneralId(14), new FactionId(2), new CityId(3)),
        },
        // 테스트: 플레이어 성 대기 병력 10만(3병종), 적 성 10만.
        GarrisonForces: new List<GarrisonForce>
        {
            new(new CityId(1), "swordsman", 50000, 60),
            new(new CityId(1), "archer", 30000, 60),
            new(new CityId(1), "cavalry", 20000, 60),
            new(new CityId(2), "swordsman", 100000, 60),
            new(new CityId(3), "swordsman", 30000, 60),
        },
        FacilityPlacements: _initialFacilityPlacements);

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
            _fog.Register(node, city.Position);
        }
    }

    // 진행 버튼 → 컨펌창(design-ui §4) → 확인 시 7일 재생 시작.
    private void OnAdvance()
    {
        if (_advancing) { return; } // 진행 중 재클릭 무시(버튼도 disabled)

        // 목표 지정 중 그려둔 경로가 있으면 진행 전에 자동 확정 — '✓확인' 안 눌러 조용히 버려지던 함정 방지.
        if (_depTargeting && _targetWaypoints.Count > 0) { ConfirmTarget(); }

        var deploys = _pendingDeploys.Count + _pendingSupplyDeploys.Count;
        var untargeted = _pendingDeploys.Count(p => p.Req.Target is null)
            + _pendingSupplyDeploys.Count(p => p.Req.Target is null);
        var msg = $"7일을 진행합니다. ({_state.Year}년 {_state.Month}월 {_state.DayOfMonth}일 →)";
        if (deploys > 0)
        {
            msg += $"\n출전 예약 {deploys}부대가 일괄 편성됩니다.";
            if (untargeted > 0) { msg += $"\n⚠ 목표 미지정 {untargeted}부대는 성 앞에 나와 대기합니다."; }
        }

        ShowConfirm("진행 확인", msg + "\n\n진행하시겠습니까?", StartAdvance);
    }

    private void StartAdvance()
    {
        if (_advancing) { return; }
        if (_depTargeting) { FinishTargeting(); } // 목표 지정 중 진행 = 미확정 목표 취소

        // 예약된 출전을 진행 시작 시점에 일괄 편성(대기열 → 야전).
        Dbg($"--- ADVANCE week={_week} pending={_pendingDeploys.Count} supplyPending={_pendingSupplyDeploys.Count} armiesBefore={_state.Armies.Count} ---");
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
            if (dr.Ok) { _state = dr.State; deployNote.Add($"[출전] {label} 부대가 출진했습니다."); }
            else { deployNote.Add($"[출전] 편성 실패({dr.Error})"); }
        }

        _pendingDeploys.Clear();

        foreach (var (req, label) in _pendingSupplyDeploys)
        {
            var dr = _deployer.DeploySupply(_state, req);
            Dbg($"  supply-deploy '{label}': ok={dr.Ok} err={dr.Error ?? "-"} armiesNow={dr.State.Armies.Count}");
            if (dr.Ok) { _state = dr.State; deployNote.Add($"[보급부대] {label} 부대가 출진했습니다."); }
            else { deployNote.Add($"[보급부대] 편성 실패({dr.Error})"); }
        }

        _pendingSupplyDeploys.Clear();

        // 플레이어 세력은 직접 조작 — AI는 나머지 세력만 굴린다.
        foreach (var f in _state.Factions.Where(f => f.Id != Player).OrderBy(f => f.Id.Value))
        {
            _state = _ai.PlanWeek(_state, f.Id);
        }

        Dbg($"  afterDeploy armies={_state.Armies.Count}");
        var preMove = _state; // 이동 전(편성·AI 반영) — 애니메이션 시작 위치
        var startHex = preMove.Armies.ToDictionary(u => u.Id.Value, u => u.Field.Position);
        var after = _engine.AdvanceWeek(preMove, out var turns, out var sieges, out var captures, out var plunders, out var casualties);
        _week++;
        Dbg($"  afterAdvance armies={after.Armies.Count} sieges={sieges.Count} caps={captures.Count} turns={turns.Count}");
        LogAdvanceDetail(startHex, turns, sieges, captures, plunders, after);

        var note = new List<string>();
        _pendingReport.Clear();
        void Ev(string t, Color c) { note.Add(t); _pendingReport.Add((t, c)); }

        var siegeCol = new Color(0.9f, 0.6f, 0.4f);
        foreach (var dn in deployNote) { Ev(dn, dn.Contains("실패") ? AccentFill : Parchment); }
        AddCombatReport(turns, Ev); // 교전·특기·계략·지속 피해(내 세력만)
        foreach (var bandit in preMove.Armies.Where(a => a.Field.Owner == WorldEngine.BanditFaction))
        {
            var targetCity = preMove.Cities.FirstOrDefault(c => c.Position == bandit.Field.Target);
            if (targetCity?.Owner != Player) { continue; }
            var defeated = turns.Any(t => !t.Units.Any(u => u.Id == bandit.Id && u.Pool.Active > 0))
                || sieges.Any(ex => ex.BesiegerDamage is { } damage && ex.TurnIndex >= 0
                    && ex.TurnIndex < turns.Count
                    && ex.Besiegers.Select((id, index) => (id, index)).Any(x => x.id == bandit.Id
                        && x.index < damage.Count && turns[ex.TurnIndex].Units.FirstOrDefault(u => u.Id == x.id) is { } unit
                        && damage[x.index] >= unit.Pool.Active));
            if (defeated) { Ev($"[치안] {targetCity.Name}을 노리던 도적 부대를 격퇴했습니다.", GoldBright); }
        }

        foreach (var cas in casualties)
        {
            if (!preMove.Armies.Any(u => u.Id == cas.Unit && u.Field.Owner == Player)) { continue; } // 내 부대만
            var gName = _state.Generals.FirstOrDefault(g => g.Id == cas.General)?.Name ?? $"G{cas.General.Value}";
            var text = cas.Captured
                ? $"[손실] {gName} 장수, 부대가 전멸하여 포로가 되었습니다."
                : cas.Refuge is { } rf ? $"[손실] {gName} 장수, 부대가 전멸하여 {_cities.First(c => c.Id == rf).Name}(으)로 귀환했습니다."
                : $"[손실] {gName} 장수, 부대가 전멸하여 재야가 되었습니다.";
            Ev(text, AccentFill);
            Dbg($"  casualty u{cas.Unit.Value} {gName} {(cas.Captured ? $"captured-by f{cas.Holder!.Value.Value}" : cas.Refuge is { } r2 ? $"fled-to city{r2.Value}" : "wanderer")}");
        }

        foreach (var ex in sieges)
        {
            var mine = preMove.Cities.First(c => c.Id == ex.City).Owner == Player;
            var byMe = ex.Besiegers.Any(b => preMove.Armies.Any(u => u.Id == b && u.Field.Owner == Player));
            if (!mine && !byMe) { continue; } // 내 세력 관련 공성만
            var cn = _cities.First(c => c.Id == ex.City).Name;
            if (ex.Besiegers.Any(id => preMove.Armies.Any(a => a.Id == id && a.Field.Owner == WorldEngine.BanditFaction)))
            {
                Ev($"[도적 습격] {cn} 공격 · 성벽 피해 {ex.WallDamage} · 수비 병력 피해 {ex.TroopDamage}", siegeCol);
            }
            Ev(mine
                ? (ex.WallDamage > 0 ? $"[공성] 아군 {cn}이(가) 공격받아 성벽 피해 {ex.WallDamage}(남은 {ex.NewWall})." : $"[공성] 아군 {cn}이(가) 공격받았으나 성벽은 버텼습니다.")
                : (ex.WallDamage > 0 ? $"[공성] {cn} 성벽에 피해 {ex.WallDamage}을(를) 입혔습니다(남은 {ex.NewWall})." : $"[공성] {cn} 성벽에 피해를 주지 못했습니다."),
                siegeCol);
        }

        foreach (var c in captures)
        {
            var wasMine = preMove.Cities.First(x => x.Id == c.City).Owner == Player;
            var mineNow = c.NewOwner == Player;
            if (!wasMine && !mineNow) { continue; } // 내 세력 관련만
            var name = _cities.First(x => x.Id == c.City).Name;
            var owner = after.Factions.First(f => f.Id == c.NewOwner).Name;
            Ev(mineNow
                ? $"[함락] {name}을(를) 점령했습니다!"
                : $"[함락] 아군 {name}을(를) 빼앗겼습니다(→ {owner}){(c.FactionEliminated ? " · 세력 멸망" : "")}",
                GoldBright);
        }

        // 내정/라이프사이클 사건(Core WorldEvent) — 내 세력만. 명령 완료 수치.
        foreach (var we in _engine.LastWorldEvents.Where(e => e.Faction == Player))
        {
            var gName = we.General is { } gid ? _state.Generals.FirstOrDefault(g => g.Id == gid)?.Name ?? "장수" : "";
            var cName = we.City is { } cid ? _cities.FirstOrDefault(c => c.Id == cid)?.Name ?? "성" : "";
            var troop = we.Code.Length > 0 ? TroopName(we.Code) : "";
            var targetFaction = int.TryParse(we.Code, out var factionId)
                ? _state.Factions.FirstOrDefault(f => f.Id == new FactionId(factionId))?.Name ?? $"세력 {factionId}"
                : "";
            var (text, col) = we.Kind switch
            {
                WorldEventKind.Recruit => ("", Parchment),
                WorldEventKind.Conscript => ("", Parchment),
                WorldEventKind.Train => ($"[내정] {cName}의 {troop} 훈련도가 올랐습니다(+{we.Amount}).", Parchment),
                WorldEventKind.Build => ($"[내정] {cName}에 {FacilityLabel(we.Code)} 건설을 마쳤습니다.", Parchment),
                WorldEventKind.Research => ($"[군비] {cName}에서 연구를 마쳤습니다.", Parchment),
                WorldEventKind.Repair => ($"[내정] {cName} 수리를 마쳤습니다.", Parchment),
                WorldEventKind.EnlistSuccess => ($"[인사] 등용 성공! {gName} 장수가 우리 세력에 합류했습니다.", GoldBright),
                WorldEventKind.EnlistFail => ($"[인사] {gName} 장수 등용에 실패했습니다.", Parchment),
                WorldEventKind.Explore => (ExplorationEventText(cName, gName, we.Code, we.Amount, we.ExtraAmount), we.Code is "divine_beast_trace" or "ancient_relic_clue" ? GoldBright : Parchment),
                WorldEventKind.AllianceSuccess => ($"[외교] {targetFaction} 세력과 동맹을 맺었습니다.", GoldBright),
                WorldEventKind.AllianceFail => ($"[외교] {targetFaction} 세력과의 동맹 교섭에 실패했습니다.", Parchment),
                WorldEventKind.StratagemSuccess => ($"[계략] {gName}의 {FacilityLabel(we.Code)} 성공 — {cName}에 효과가 적용되었습니다.", GoldBright),
                WorldEventKind.StratagemFail => ($"[계략] {gName}의 {FacilityLabel(we.Code)} 실패 — {cName}에는 아무 효과가 없었습니다.", Parchment),
                WorldEventKind.ProductionComplete => ($"[생산] {gName} 장수가 {cName}의 {FacilityLabel(we.Code)} 생산을 마쳤습니다. 금 +{we.Amount}, 군량 +{we.ExtraAmount}.", GoldBright),
                WorldEventKind.ProductionLost => ($"[생산] {gName} 장수의 {FacilityLabel(we.Code)} 생산 부대 {we.Amount}명이 소실되었습니다.", AccentFill),
                WorldEventKind.BanditRaid => ($"[치안] {cName} 주변에 도적 {we.Amount}명이 출현해 성을 노립니다.", AccentFill),
                WorldEventKind.SecurityFactor => ($"[치안 요인] {cName}: {(we.Code == "vacancy" ? "치안 담당 공석" : we.Code == "recruitment" ? $"병력 담당 {gName}" : $"치안 담당 {gName}")} {we.Amount:+0;-0;0} / 7일 (합산 후 0~100 적용)", Parchment),
                _ => ("", Parchment),
            };
            if (text.Length > 0) { Ev(text, col); }
        }
        AddAutoOfficerReport(preMove, after, Ev);

        _pendingState = after;
        _pendingNote = note.Count > 0 ? string.Join(" · ", note) : "—";
        BuildAnimation(startHex, turns, sieges, preMove);

        // 애니메이션 시작: 이동 전 상태(토큰=시작 위치)를 그린 뒤, _Process가 칸 단위로 이동시킨다.
        // 열려 있던 성 명령 팔레트·정보 카드는 자동으로 닫는다(진행 중 명령 불가).
        HidePanels();
        Redraw(_pendingNote);

        // 진행(재생) 동안 아군 부대의 이동 경로를 표시한다(재생이 끝나면 FinishAdvance의 Redraw가 지운다).
        ClearPathMarkers();
        foreach (var u in preMove.Armies.Where(u => u.Field.Owner == Player
            && u.Field.Target is { } t && t != u.Field.Position))
        {
            AddRouteDots(u.Field.Position, u.Field.Waypoints, u.Field.Target!.Value, _pathMarkers);
        }

        _advancing = true;
        _animT = 0;
        _animStepIdx = 0;
        _animAtkIdx = 0;
        _animUpdIdx = 0;
        _animKillIdx = 0;
        _animProductionKillIdx = 0;
        _animEffectIdx = 0;
        _animDmgIdx = 0;
        _animSiegeDmgIdx = 0;
        _animArrowIdx = 0;
        _animSupplyArrowIdx = 0;
        _advanceBtn.Busy = true;
        _advanceBtn.Progress = 0f;
        _dayLabel.Visible = true;
        _dayTurnLabel.Visible = true;
        _dayLabel.Text = "1일차";
        _dayTurnLabel.Text = "▷ 이동턴"; // 재생 시작(0초)은 항상 이동
    }

    // 진행 결과의 이동 틱을 "언제 어느 칸으로" 스텝 목록으로 편다. 한 칸 = 1초, 하루 = 4초 슬롯
    // (하루의 마지막 1초는 공격 모션 몫). 교전·공성이 벌어진 진행 조각의 끝에 공격 모션을 스케줄.
    private void BuildAnimation(Dictionary<int, HexCoord> startHex, IReadOnlyList<AdvanceTurn> turns,
        IReadOnlyList<SiegeExchange> sieges, GameState preMove)
    {
        _animationProductionPositions.Clear();
        _visionRefreshTime = 0;
        _lostProductionVision.Clear();
        _productionVisionLosses.Clear();
        foreach (var op in preMove.ProductionOps) _animationProductionPositions[op.Id] = op.Position;
        _animSteps.Clear();
        _animAttacks.Clear();
        _animUpdates.Clear();
        _animKills.Clear();
        _animProductionKills.Clear();
        _animDeathEffects.Clear();
        _animDmg.Clear();
        _animSiegeDmg.Clear();
        _animArrows.Clear();
        _animSupplyArrows.Clear();
        for (var d = 0; d <= AnimDays; d++) { _dayKind[d] = "이동"; } // 기본 이동턴, 아래서 교전·공성 있는 날만 공격턴
        var alive = new HashSet<int>(startHex.Keys);
        var deathEffectUnitIds = new HashSet<int>();
        var prev = new Dictionary<int, HexCoord>(startHex);
        var movesInDay = new Dictionary<(int, int), int>();
        var dayOffset = 0;
        for (var ti = 0; ti < turns.Count; ti++)
        {
            var turn = turns[ti];
            foreach (var tick in turn.Movement.Ticks)
            {
                var absDay = dayOffset + tick.Day;
                foreach (var fu in tick.Units)
                {
                    var id = fu.Id.Value;
                    if (!prev.TryGetValue(id, out var pv)) { prev[id] = fu.Position; continue; }
                    if (fu.Position == pv) { continue; }
                    var k = movesInDay.GetValueOrDefault((id, absDay), 0);
                    _animSteps.Add(((absDay - 1) * DaySeconds + k * StepSeconds, id, fu.Position));
                    movesInDay[(id, absDay)] = k + 1;
                    prev[id] = fu.Position;
                }
            }

            var stopDay = dayOffset + System.Math.Max(1, turn.Movement.Days);
            var atkTime = ((stopDay - 1) * DaySeconds) + MoveSeconds + 0.15; // 그날 이동(≤1.5초)이 끝난 뒤
            ScheduleAttackMotions(turn, atkTime);

            // 그 턴에 교전/공성이 있었으면 정지일(stopDay)을 '공격턴'으로 표기.
            if (stopDay >= 1 && stopDay <= AnimDays && (turn.Combat is not null || sieges.Any(s => s.TurnIndex == ti)))
            {
                _dayKind[stopDay] = "공격";
            }

            // 교전 피해 팝업 — 양측 모두, 공격 모션 직후에 뜬다.
            if (turn.Combat is { } cbt)
            {
                foreach (var (uid, dmg) in cbt.DamageTaken.OrderBy(kv => kv.Key.Value))
                {
                    if (dmg > 0) { _animDmg.Add((atkTime + 0.35, uid.Value, dmg)); }
                }
            }

            // 이 조각의 정산 반영: 병력 갱신(라벨·편대 규모) + 전멸/입성 부대 즉시 제거.
            var settleTime = atkTime + 0.55; // 공격 모션이 보인 뒤
            var survivors = new HashSet<int>();
            foreach (var u in turn.Units)
            {
                survivors.Add(u.Id.Value);
                _animUpdates.Add((settleTime, u.Id.Value, u.Pool.Active));

                // 교란 강제 후퇴(PushAway) 등 이동 틱에 안 잡히는 위치 변화 동기화.
                if (prev.TryGetValue(u.Id.Value, out var lastPos) && lastPos != u.Field.Position)
                {
                    _animSteps.Add((settleTime, u.Id.Value, u.Field.Position));
                    prev[u.Id.Value] = u.Field.Position;
                }
            }

            // 공성 교환(이 조각 소속): 성 피해(성벽+수비) 팝업 + 부대별 반격 피해 팝업.
            // 반격으로 전멸한 공성 부대는 이 시점에 병력 갱신/제거(다음 조각을 기다리지 않는다).
            foreach (var ex in sieges.Where(x => x.TurnIndex == ti))
            {
                var cityPos = _view.HexToWorld(_cities.First(c => c.Id == ex.City).Position)
                    + new Vector3(0f, _view.TileTopY, 0f);
                foreach (var uid in ex.Besiegers.Select(x => x.Value).OrderBy(x => x))
                {
                    if (turn.Units.FirstOrDefault(u => u.Id.Value == uid) is { } siegeUnit)
                    {
                        _animAttacks.Add((atkTime, uid, cityPos));
                        if (siegeUnit.IsSupply)
                        {
                            _animSupplyArrows.Add((atkTime + 0.08, uid, cityPos));
                        }
                    }
                }

                var cityDmg = ex.WallDamage + ex.TroopDamage;
                if (cityDmg > 0) { _animSiegeDmg.Add((atkTime + 0.35, cityPos, cityDmg)); }
                if (ex.BesiegerDamage is not { } counters) { continue; }
                for (var i = 0; i < ex.Besiegers.Count && i < counters.Count; i++)
                {
                    if (counters[i] <= 0) { continue; }
                    var uid = ex.Besiegers[i].Value;
                    _animArrows.Add((atkTime, cityPos + new Vector3(0f, 0.85f, 0f), uid)); // 성벽 위에서 발사
                    _animDmg.Add((atkTime + 0.35, uid, counters[i]));
                    var unit = turn.Units.FirstOrDefault(x => x.Id.Value == uid);
                    if (unit is null) { continue; }
                    var remain = unit.Pool.Active - counters[i]; // 근사 표시(부상 회수 제외)
                    if (remain <= 0)
                    {
                        if (deathEffectUnitIds.Add(uid))
                        {
                            _animDeathEffects.Add((settleTime, _view.HexToWorld(unit.Field.Position)));
                        }
                        _animKills.Add((settleTime + 0.05, uid));
                        alive.Remove(uid);
                        survivors.Remove(uid);
                    }
                    else { _animUpdates.Add((settleTime + 0.05, uid, remain)); }
                }
            }

            var enteredNow = turn.EnteredCastle.Select(u => u.Id.Value).ToHashSet();
            foreach (var id in alive.Where(id => !survivors.Contains(id)).OrderBy(id => id))
            {
                _animKills.Add((settleTime, id));
                if (!enteredNow.Contains(id) && prev.TryGetValue(id, out var deadAt))
                {
                    if (deathEffectUnitIds.Add(id))
                    {
                        _animDeathEffects.Add((settleTime - 0.05, _view.HexToWorld(deadAt)));
                    }
                }
            }

            foreach (var (_, pos) in turn.LostProduction.OrderBy(kv => kv.Key.Value))
            {
                _animDeathEffects.Add((settleTime - 0.05, _view.HexToWorld(pos)));
                foreach (var op in preMove.ProductionOps.Where(o => o.Target == pos))
                    _productionVisionLosses.Add((settleTime + 0.05, op.Id));
            }

            alive = survivors;
            dayOffset = stopDay;
        }

        ScheduleProductionAnimations(preMove);

        _animSteps.Sort((a, b) => a.Time.CompareTo(b.Time));
        _animAttacks.Sort((a, b) => a.Time.CompareTo(b.Time));
        _animUpdates.Sort((a, b) => a.Time.CompareTo(b.Time));
        _animKills.Sort((a, b) => a.Time.CompareTo(b.Time));
        _animProductionKills.Sort((a, b) => a.Time.CompareTo(b.Time));
        _animDeathEffects.Sort((a, b) => a.Time.CompareTo(b.Time));
        _animDmg.Sort((a, b) => a.Time.CompareTo(b.Time));
        _animSiegeDmg.Sort((a, b) => a.Time.CompareTo(b.Time));
        _animArrows.Sort((a, b) => a.Time.CompareTo(b.Time));
        _animSupplyArrows.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    private void ScheduleProductionAnimations(GameState preMove)
    {
        foreach (var op in preMove.ProductionOps.OrderBy(o => o.Id))
        {
            var position = op.Position;
            var phase = op.Phase;
            var path = phase == ProductionPhase.Returning ? op.BackPath : op.OutPath;
            for (var day = 1; day <= AnimDays; day++)
            {
                if (phase == ProductionPhase.Gathering) { break; }
                var idx = path.ToList().FindIndex(p => p == position);
                if (idx < 0) { break; }
                var nextIdx = System.Math.Min(path.Count - 1, idx + System.Math.Max(1, op.Speed));
                position = path[nextIdx];
                if (nextIdx > idx)
                {
                    _animSteps.Add(((day - 1) * DaySeconds, -op.Id, position));
                }

                if (phase == ProductionPhase.Outbound && nextIdx == path.Count - 1)
                {
                    _animProductionKills.Add(((day - 1) * DaySeconds + StepSeconds + 0.05, op.Id));
                    phase = ProductionPhase.Gathering;
                    break;
                }
            }
        }
    }

    // 성 반격 화살 일제사 — 성벽 위에서 대상 부대로 5발(고정 산포·비행시간 변주, 난수 없음).
    private static readonly (Vector3 Off, float Flight)[] VolleyPattern =
    {
        (new Vector3(0f, 0f, 0f), 0.42f),
        (new Vector3(0.16f, 0f, 0.10f), 0.47f),
        (new Vector3(-0.14f, 0f, 0.12f), 0.45f),
        (new Vector3(0.09f, 0f, -0.15f), 0.50f),
        (new Vector3(-0.11f, 0f, -0.11f), 0.44f),
    };

    private void SpawnCastleVolley(Vector3 from, Vector3 targetPos)
    {
        if (!IsVisibleAt(from) || !IsVisibleAt(targetPos)) return;
        foreach (var (off, flight) in VolleyPattern)
        {
            ProjectileView.SpawnArrow(this, from + (off * 0.5f), targetPos + off + new Vector3(0f, 0.15f, 0f), flight);
        }
    }

    // 보급부대 공격 화살 — 보급부대 전용 에셋 애니메이션과 별개로 씬 타임라인에서 직접 쏜다.
    // 에셋 애니메이션 이름/상태 전환이 실패해도 공격 판정이 예약되면 반드시 보이는 피드백을 제공한다.
    private void SpawnSupplyVolley(Vector3 from, Vector3 targetPos)
    {
        if (!IsVisibleAt(from) || !IsVisibleAt(targetPos)) return;
        var raisedFrom = from + new Vector3(0f, 0.85f, 0f);
        var raisedTo = targetPos + new Vector3(0f, 0.25f, 0f);
        foreach (var (off, flight) in VolleyPattern.Take(3))
        {
            ProjectileView.SpawnArrow(this, raisedFrom + (off * 0.35f), raisedTo + off, flight);
        }
    }

    // 피해 숫자 팝업 — 위로 떠오르며 사라진다(효과 연출은 후속, 우선 수치 피드백만).
    private void SpawnDamagePopup(Vector3 at, int damage)
    {
        if (!IsVisibleAt(at)) return;
        var lbl = new Label3D
        {
            Text = $"-{damage}",
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 44,
            OutlineSize = 12,
            NoDepthTest = true,
            Modulate = new Color(1f, 0.36f, 0.30f),
            Position = at + new Vector3(0f, 1.5f, 0f),
        };
        AddChild(lbl);
        var tw = CreateTween();
        tw.TweenProperty(lbl, "position", lbl.Position + new Vector3(0f, 0.9f, 0f), 1.1f);
        tw.Parallel().TweenProperty(lbl, "modulate:a", 0f, 1.1f).SetDelay(0.35f);
        tw.Finished += lbl.QueueFree;
    }

    private void PlayRisingSkulls(Vector3 at)
    {
        if (!IsVisibleAt(at)) return;
        var spot = new Node3D { Position = at + new Vector3(0f, _view.TileTopY + 0.08f, 0f) };
        AddChild(spot);
        EffectView.Attach(spot, EffectKind.RisingSkulls, 1.08f, loop: false);
        var cleanup = CreateTween();
        cleanup.TweenInterval(2.3f);
        cleanup.Finished += spot.QueueFree;
    }

    // 이 진행 조각에서 공격한 부대의 모션 예약: 야전 교전(피해를 준 부대 → 최근접 적 방향)
    // + 공성(공격모드로 적 성 사거리 안 → 성 방향).
    private void ScheduleAttackMotions(AdvanceTurn turn, double atkTime)
    {
        var fieldAttackers = new HashSet<int>();
        void AddAttack(CombatUnit unit, Vector3 target)
        {
            _animAttacks.Add((atkTime, unit.Id.Value, target));
            if (unit.IsSupply)
            {
                _animSupplyArrows.Add((atkTime + 0.08, unit.Id.Value, target));
            }
        }

        if (turn.ProductionAttackTargets is { } productionTargets)
        {
            foreach (var (id, position) in productionTargets)
            {
                _animAttacks.Add((atkTime, id.Value, _view.HexToWorld(position)));
                fieldAttackers.Add(id.Value);
            }
        }
        if (turn.Combat is { } combat)
        {
            foreach (var id in combat.DamageDealt.Keys.OrderBy(k => k.Value))
            {
                if (fieldAttackers.Contains(id.Value)) { continue; }
                var me = turn.Units.FirstOrDefault(u => u.Id == id);
                var foe = me is null ? null : turn.Units
                    .Where(u => u.Field.Owner != me.Field.Owner)
                    .OrderBy(u => u.Field.Position.Distance(me.Field.Position)).ThenBy(u => u.Id.Value)
                    .FirstOrDefault();
                if (me is null || foe is null) { continue; }
                AddAttack(me, _view.HexToWorld(foe.Field.Position));
                fieldAttackers.Add(id.Value);
            }
        }

        foreach (var u in turn.Units.Where(u => u.Field.Mode == UnitMode.Attack))
        {
            if (fieldAttackers.Contains(u.Id.Value)) { continue; }
            var castle = _pendingState.Cities.FirstOrDefault(c => c.Owner != u.Field.Owner
                && c.Position.Distance(u.Field.Position) <= u.Field.RangeCastle);
            if (castle is not null)
            {
                AddAttack(u, _view.HexToWorld(castle.Position));
            }
        }
    }

    // 애니메이션 종료 — 최종 상태를 반영하고 버튼·텍스트를 원복한다.
    private void FinishAdvance()
    {
        _advancing = false;
        _advanceBtn.Busy = false;
        _advanceBtn.Progress = 0f;
        _dayLabel.Visible = false;
        _dayTurnLabel.Visible = false;

        _state = _pendingState;
        Redraw(_pendingNote);

        // 이번 진행의 사건을 날짜 헤더와 함께 보고 패널로 flush.
        if (_pendingReport.Count > 0)
        {
            Report($"── {_state.Year}년 {_state.Month}월 {_state.DayOfMonth}일 ──", Gold);
            foreach (var (t, c) in _pendingReport) { Report(t, c); }
            _pendingReport.Clear();
            ScrollReportToBottomDeferred();
        }

        var alive = _state.Factions.Where(f => _state.CityCount(f.Id) > 0).ToList();
        if (alive.Count <= 1)
        {
            _log.Text = $"[종료] {(alive.Count == 1 ? alive[0].Name + " 통일" : "무승부")} (주 {_week})";
        }

        if (_selected is { } sel && _state.Cities.Any(c => c.Id == sel && c.Owner == Player))
        {
            var refreshDetail = _openCityDetailCity == sel;
            SelectCity(sel);
            if (refreshDetail) { OpenCityDetail(sel); }
        }
        else
        {
            _openCityDetailCity = null;
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
        // 팔레트에는 그룹 버튼만 둔다. 단일 명령인 계략은 중간 플라이아웃 없이 모달을 바로 연다.
        for (var gi = 0; gi < CmdGroups.Length; gi++)
        {
            var groupIdx = gi;
            var gbtn = MakeButton(CmdGroups[gi].Group);
            gbtn.AddThemeFontSizeOverride("font_size", 12);
            gbtn.Alignment = HorizontalAlignment.Center;
            gbtn.CustomMinimumSize = new Vector2(74, 24);
            gbtn.Pressed += () =>
            {
                var commands = CmdGroups[groupIdx].Indices;
                if (commands.Length == 1 && Cmds[commands[0]].Kind is CommandKind.CityStratagem or CommandKind.Explore)
                {
                    CloseGroupMenu();
                    OpenModal(commands[0]);
                    return;
                }

                ToggleGroup(groupIdx);
            };
            _cmdList.AddChild(gbtn);
        }

        var deployBtn = MakeButton("출전", accent: true);
        deployBtn.AddThemeFontSizeOverride("font_size", 12);
        deployBtn.Alignment = HorizontalAlignment.Center;
        deployBtn.CustomMinimumSize = new Vector2(74, 24);
        deployBtn.Pressed += () => { CloseGroupMenu(); if (_selected is { } c) { OpenDeployModal(c); } };
        _cmdList.AddChild(deployBtn);

        var supplyBtn = MakeButton("보급부대", accent: true);
        supplyBtn.AddThemeFontSizeOverride("font_size", 12);
        supplyBtn.Alignment = HorizontalAlignment.Center;
        supplyBtn.CustomMinimumSize = new Vector2(74, 24);
        supplyBtn.Pressed += () =>
        {
            CloseGroupMenu();
            if (_selected is { } c) { OpenSupplyHub(c); }
        };
        _cmdList.AddChild(supplyBtn);

        var productionBtn = MakeButton("생산", accent: true);
        productionBtn.AddThemeFontSizeOverride("font_size", 12);
        productionBtn.Alignment = HorizontalAlignment.Center;
        productionBtn.CustomMinimumSize = new Vector2(74, 24);
        productionBtn.Pressed += () => { CloseGroupMenu(); if (_selected is { } c) { OpenProductionModal(c); } };
        _cmdList.AddChild(productionBtn);
        AddV2PendingButton(_cmdList, "재편성", "부대 재편성 전용 UI는 v2 전환 후속 단계에서 구현합니다.\n현재는 출전 예약과 입성으로 병력을 정리하세요.");
        AddV2PendingButton(_cmdList, "보충", "자동 담당자 병력 생산과 연계한 보충 명령은 Phase 2~4 이후 구현합니다.");

        // 그룹 플라이아웃(팔레트 우측에 붙는 작은 패널).
        _cmdSubMenu = new PanelContainer { Visible = false, ZIndex = 51 };
        _cmdSubMenu.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 5, 4));
        layer.AddChild(_cmdSubMenu);
        _cmdSubList = new VBoxContainer();
        _cmdSubList.AddThemeConstantOverride("separation", 1);
        _cmdSubMenu.AddChild(_cmdSubList);

        BuildUnitMenu(layer);
        BuildTerrainCard(layer);

        // 목표 지정 레이어 — 경유지별 취소 버튼 + 최종 '확인' 버튼. 지점을 순서대로 찍어 경로를 만든다.
        var confirmLayer = new CanvasLayer { Layer = 26 };
        AddChild(confirmLayer);
        _targetEditLayer = confirmLayer;
        _targetConfirmBtn = MakeButton("✓ 확인", accent: true);
        _targetConfirmBtn.AddThemeFontSizeOverride("font_size", 13);
        _targetConfirmBtn.CustomMinimumSize = new Vector2(70, 30);
        _targetConfirmBtn.Visible = false;
        _targetConfirmBtn.Pressed += ConfirmTarget;
        confirmLayer.AddChild(_targetConfirmBtn);

        HidePanels();
    }

    private void AddV2PendingButton(VBoxContainer list, string label, string message)
    {
        var btn = MakeButton(label);
        btn.AddThemeFontSizeOverride("font_size", 12);
        btn.Alignment = HorizontalAlignment.Center;
        btn.CustomMinimumSize = new Vector2(74, 24);
        btn.Pressed += () =>
        {
            CloseGroupMenu();
            ShowNotice($"{label} 준비 중", message);
        };
        list.AddChild(btn);
    }

    // 게임 스타일 컨펌창(금테·잉크 + 한글 확인/취소). 배경 클릭·취소 = 닫기만.
    private void ShowConfirm(string title, string message, System.Action onOk)
        => ShowConfirmWithOfficer(title, message, null, onOk);

    private void ShowOfficerConfirm(string title, string message, GeneralId officer, System.Action onOk, string? officerLine = null)
        => ShowConfirmWithOfficer(title, message, officer, onOk, officerLine);

    private void ShowConfirmWithOfficer(string title, string message, GeneralId? officer, System.Action onOk, string? officerLine = null)
    {
        _confirmLayer?.QueueFree();
        var layer = new CanvasLayer { Layer = 40 };
        AddChild(layer);
        _confirmLayer = layer;

        void Close() { layer.QueueFree(); if (_confirmLayer == layer) { _confirmLayer = null; } }

        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.55f) };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.MouseFilter = Control.MouseFilterEnum.Stop;
        backdrop.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { Close(); }
        };
        layer.AddChild(backdrop);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(center);

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 10, 14));
        panel.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;
        center.AddChild(panel);

        var viewport = GetViewport().GetVisibleRect().Size;
        var officerTexture = officer is { } portraitId ? OfficerPortrait(portraitId) : null;
        var portraitHeight = Mathf.Min(360f, viewport.Y * 0.40f);
        var portraitWidth = officerTexture is null ? 0f
            : portraitHeight * officerTexture.GetWidth() / Mathf.Max(1f, officerTexture.GetHeight());
        portraitWidth = Mathf.Min(portraitWidth, viewport.X - 96f);
        var confirmWidth = Mathf.Min(Mathf.Max(320f, portraitWidth + 16f), viewport.X - 64f);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(confirmWidth, 0) };
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);

        var titleLbl = MakeLabel($"◈  {title}", 17, Gold);
        titleLbl.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(titleLbl);
        box.AddChild(GoldRule());

        var contentScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(confirmWidth, 0),
        };
        box.AddChild(contentScroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 10);
        contentScroll.AddChild(content);

        if (officerTexture is not null)
        {
            var officerBox = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            officerBox.AddThemeConstantOverride("separation", 6);
            content.AddChild(officerBox);

            var portraitFrame = new PanelContainer
            {
                CustomMinimumSize = new Vector2(portraitWidth + 8f, portraitHeight + 8f),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            portraitFrame.AddThemeStyleboxOverride("panel", Frame(InkSoft, Gold, 1, 6, 4));
            officerBox.AddChild(portraitFrame);

            portraitFrame.AddChild(new TextureRect
            {
                Texture = officerTexture,
                CustomMinimumSize = new Vector2(portraitWidth, portraitHeight),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            });

            var speechText = officerLine ?? $"“{OfficerConfirmLines[_confirmRandom.Next(OfficerConfirmLines.Length)]}”";
            var speech = MakeLabel(speechText, 14, GoldBright);
            speech.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            speech.CustomMinimumSize = new Vector2(confirmWidth - 20f, 0);
            speech.HorizontalAlignment = HorizontalAlignment.Center;
            officerBox.AddChild(speech);
        }

        var msg = MakeLabel(message, 14, Parchment);
        msg.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        msg.CustomMinimumSize = new Vector2(confirmWidth - 20f, 0);
        content.AddChild(msg);
        void FitConfirmContent() => contentScroll.CustomMinimumSize = new Vector2(confirmWidth,
            Mathf.Min(content.GetCombinedMinimumSize().Y, Mathf.Max(80f, viewport.Y - 180f)));
        content.MinimumSizeChanged += FitConfirmContent;
        FitConfirmContent();

        var btnRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btnRow.AddThemeConstantOverride("separation", 12);
        box.AddChild(btnRow);
        var ok = MakeButton("확인", accent: true);
        ok.CustomMinimumSize = new Vector2(110, 34);
        ok.Pressed += () => { Close(); onOk(); };
        btnRow.AddChild(ok);
        var cancel = MakeButton("취소");
        cancel.CustomMinimumSize = new Vector2(110, 34);
        cancel.Pressed += Close;
        btnRow.AddChild(cancel);
    }

    private void ShowNotice(string title, string message)
    {
        _confirmLayer?.QueueFree();
        var layer = new CanvasLayer { Layer = 42 };
        AddChild(layer);
        _confirmLayer = layer;

        void Close() { layer.QueueFree(); if (_confirmLayer == layer) { _confirmLayer = null; } }

        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.32f) };
        backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        backdrop.MouseFilter = Control.MouseFilterEnum.Stop;
        backdrop.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true }) { Close(); }
        };
        layer.AddChild(backdrop);

        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(center);

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", Frame(Ink, new Color(0.95f, 0.42f, 0.32f), 2, 10, 14));
        center.AddChild(panel);

        var box = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);

        var titleLbl = MakeLabel($"⚠ {title}", 17, new Color(1f, 0.64f, 0.48f));
        titleLbl.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(titleLbl);
        box.AddChild(GoldRule());

        var msg = MakeLabel(message, 14, Parchment);
        msg.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        msg.CustomMinimumSize = new Vector2(360, 0);
        box.AddChild(msg);

        var okRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        box.AddChild(okRow);
        var ok = MakeButton("확인", accent: true);
        ok.CustomMinimumSize = new Vector2(110, 34);
        ok.Pressed += Close;
        okRow.AddChild(ok);
    }

    // 지형 정보 카드: 상단 = 지형 3D 에셋 미리보기 + 한글 이름, 하단 = 이동·전투 보정.
    private void BuildTerrainCard(CanvasLayer layer)
    {
        _terrainCard = new PanelContainer { Visible = false, ZIndex = 60 };
        _terrainCard.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 8, 10));
        _terrainCard.TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps;
        layer.AddChild(_terrainCard);

        var box = new VBoxContainer { CustomMinimumSize = new Vector2(132, 0) };
        box.AddThemeConstantOverride("separation", 4);
        _terrainCard.AddChild(box);

        var closeRow = new HBoxContainer();
        closeRow.Alignment = BoxContainer.AlignmentMode.End;
        box.AddChild(closeRow);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(28, 24);
        close.AddThemeFontSizeOverride("font_size", 11);
        close.Pressed += () =>
        {
            _terrainCard.Visible = false;
            _terrainHex = null;
        };
        closeRow.AddChild(close);

        // 상단: 지형 에셋 3D 미리보기(자체 월드 SubViewport). 영역을 작게.
        var svc = new SubViewportContainer { Stretch = true, CustomMinimumSize = new Vector2(112, 84), MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        box.AddChild(svc);
        _terrainViewport = new SubViewport
        {
            Size = new Vector2I(112, 84),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            OwnWorld3D = true, // 자체 3D 월드 — 없으면 메인 씬 월드를 봐 빈 화면이 된다
        };
        svc.AddChild(_terrainViewport);
        _terrainViewport.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.09f, 0.07f, 0.06f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(0.78f, 0.78f, 0.82f),
                AmbientLightEnergy = 0.9f,
            },
        });
        _terrainCam = new Camera3D { Fov = 40f, Current = true };
        _terrainViewport.AddChild(_terrainCam); // LookAt은 트리에 들어간 뒤에만 가능
        _terrainCam.Position = new Vector3(0f, 1.7f, 2.1f);
        _terrainCam.LookAt(new Vector3(0f, 0.15f, 0f), Vector3.Up);
        var key = new DirectionalLight3D { LightEnergy = 1.4f };
        key.RotationDegrees = new Vector3(-55f, -35f, 0f);
        _terrainViewport.AddChild(key);
        _terrainHolder = new Node3D();
        _terrainViewport.AddChild(_terrainHolder);

        _terrainName = MakeLabel("", 15, GoldBright);
        _terrainName.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(_terrainName);
        box.AddChild(GoldRule());
        _terrainInfo = new VBoxContainer();
        _terrainInfo.AddThemeConstantOverride("separation", 2);
        box.AddChild(_terrainInfo);
    }

    // 유닛 명령 팔레트 — 성 팔레트와 같은 개념(작은 떠있는 패널). 지금은 '정보'만 동작하고
    // 이동(행군/전진/공격)·계략은 모양만(기능 미배선).
    private void BuildUnitMenu(CanvasLayer layer)
    {
        _unitMenu = new PanelContainer { Visible = false, ZIndex = 50 };
        _unitMenu.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 5, 4));
        layer.AddChild(_unitMenu);
        var menu = new VBoxContainer();
        menu.AddThemeConstantOverride("separation", 1);
        _unitMenu.AddChild(menu);

        Button Item(string label, bool accent = false)
        {
            var b = MakeButton(label, accent: accent);
            b.AddThemeFontSizeOverride("font_size", 11);
            b.Alignment = HorizontalAlignment.Center;
            b.CustomMinimumSize = new Vector2(74, 21);
            return b;
        }

        menu.AddChild(MakeLabel("· 정보", 10, GoldBright));
        var info = Item("정보", accent: true);
        info.Pressed += () => { if (_selectedUnitId >= 0) { ShowUnitInfo(_selectedUnitId); } };
        menu.AddChild(info);

        // 명령 섹션 — 아군 부대·진행 중이 아닐 때만 보인다(적/재생 중엔 정보만).
        _unitCmdBox = new VBoxContainer();
        _unitCmdBox.AddThemeConstantOverride("separation", 1);
        menu.AddChild(_unitCmdBox);

        _unitCmdBox.AddChild(MakeLabel("· 이동", 10, GoldBright));
        foreach (var (label, mode) in new[] { ("행군", UnitMode.March), ("전진", UnitMode.Advance), ("공격", UnitMode.Attack) })
        {
            var mm = mode;
            var b = Item(label);
            b.TooltipText = ModeDesc(mm);
            b.Pressed += () => BeginUnitTargeting(_selectedUnitId, mm);
            _unitCmdBox.AddChild(b);
        }

        var stop = Item("정지");
        stop.TooltipText = "목표를 지우고 그 자리에서 대기한다.";
        stop.Pressed += StopSelectedUnit;
        _unitCmdBox.AddChild(stop);

        var ret = Item("복귀");
        ret.TooltipText = "가장 가까운 아군 성으로 행군해 입성한다.";
        ret.Pressed += ReturnSelectedUnit;
        _unitCmdBox.AddChild(ret);

        _unitCmdBox.AddChild(MakeLabel("· 액티브", 10, GoldBright));
        var active = Item("슬롯 보기");
        active.TooltipText = "선봉·부관 액티브 스킬의 충전 상태를 정보 카드에서 확인한다.";
        active.Pressed += () => { if (_selectedUnitId >= 0) { ShowUnitInfo(_selectedUnitId); } };
        _unitCmdBox.AddChild(active);
    }

    // 야전 부대 이동 재지정 — 모드를 고르고 목적지를 클릭, '확인'으로 확정(출전 목표 지정과 동일 UX).
    private void BeginUnitTargeting(int unitId, UnitMode mode)
    {
        if (_advancing || unitId < 0) { return; }
        Dbg($"UI unit-retarget-begin u{unitId} mode={mode}");
        HidePanels();
        _retargetUnitId = unitId;
        _retargetMode = mode;
        _depTargeting = true;
        _targetWaypoints.Clear();
        _targetStart = _state.Armies.FirstOrDefault(a => a.Id.Value == unitId)?.Field.Position ?? default;
        RebuildTargetEdit();
        ShowTargetHint($"{ModeName(mode)}: 지점을 순서대로 클릭 = 경유지  ·  각 지점 위 취소로 삭제  ·  '확인'으로 확정  ·  적 성 = 공격  ·  자기 성 = 복귀  ·  우클릭 취소");
    }

    // 정지: 목표를 지워 그 자리에서 대기(별도 명령까지 유지).
    private void StopSelectedUnit()
    {
        if (_advancing || _selectedUnitId < 0) { return; }
        var uid = _selectedUnitId;
        var result = _unitCommander.Stop(_state, Player, new UnitId(uid));
        if (!result.Ok)
        {
            ShowNotice("명령 실패", result.Error ?? "정지할 수 없습니다.");
            return;
        }

        _state = result.State;
        Dbg($"UI unit-stop u{uid}");
        _log.Text = $"부대가 그 자리에 대기합니다.";
        Redraw(_log.Text);
        var u = _state.Armies.FirstOrDefault(a => a.Id.Value == uid);
        if (u is not null) { OpenUnitMenu(u); }
    }

    private void ReturnSelectedUnit()
    {
        if (_advancing || _selectedUnitId < 0) { return; }
        var uid = _selectedUnitId;
        var unit = _state.Armies.FirstOrDefault(a => a.Id.Value == uid && a.Field.Owner == Player);
        if (unit is null) { return; }

        var city = _state.Cities
            .Where(c => c.Owner == Player)
            .OrderBy(c => c.Position.Distance(unit.Field.Position))
            .ThenBy(c => c.Id.Value)
            .FirstOrDefault();
        if (city is null)
        {
            ShowNotice("명령 실패", "복귀할 아군 성이 없습니다.");
            return;
        }

        var result = _unitCommander.ReturnToCity(_state, Player, unit.Id, city.Id);
        if (!result.Ok)
        {
            ShowNotice("명령 실패", result.Error ?? "복귀할 수 없습니다.");
            return;
        }

        _state = result.State;
        Dbg($"UI unit-return u{uid} city={city.Id.Value}");
        _log.Text = $"부대가 {city.Name}(으)로 복귀합니다.";
        Redraw(_log.Text);
        var changed = _state.Armies.FirstOrDefault(a => a.Id.Value == uid);
        if (changed is not null) { OpenUnitMenu(changed); }
    }

    // 재지정 확정 — 적 성이면 공격모드로 전환, 자기 성이면 복귀(입성은 이동 규칙이 처리).
    private void ApplyUnitTarget(HexCoord h, IReadOnlyList<HexCoord>? waypoints)
    {
        var uid = _retargetUnitId;
        var mode = _retargetMode;
        FinishTargeting();
        var u = _state.Armies.FirstOrDefault(a => a.Id.Value == uid && a.Field.Owner == Player);
        if (u is null) { return; }

        var enemyCity = _state.Cities.FirstOrDefault(c => c.Position == h && c.Owner != Player);
        var enemyUnit = DisplayedArmies.FirstOrDefault(a => a.Field.Position == h && a.Field.Owner != Player && CanSeeUnit(a));
        if (enemyCity is not null || enemyUnit is not null) { mode = UnitMode.Attack; }
        var result = _unitCommander.Reassign(_state, Player,
            new FieldUnitCommandRequest(new UnitId(uid), mode, h, waypoints, _visibleTiles));
        if (!result.Ok)
        {
            ShowNotice("명령 실패", result.Error ?? "목표를 지정할 수 없습니다.");
            return;
        }

        _state = result.State;

        var tName = _state.Cities.FirstOrDefault(c => c.Position == h)?.Name ?? $"({h.Q},{h.R})";
        var wpNote = waypoints is { Count: > 0 } ? $" · 경유 {waypoints.Count}" : "";
        Dbg($"UI unit-retarget u{uid} -> ({h.Q},{h.R}) mode={mode} wps={waypoints?.Count ?? 0}");
        _log.Text = $"부대 → {tName} ({ModeName(mode)}모드){wpNote}";
        Redraw(_log.Text);
        OpenUnitMenu(_state.Armies.First(a => a.Id.Value == uid)); // 팔레트 복귀 + 새 경로 표시
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
        CloseGroupMenu();
        _unitMenu.Visible = false;
        _selectedUnitId = -1;
        _terrainCard.Visible = false;
        _terrainHex = null;
        ClearPathMarkers();
        ClearSupplyZoneMarkers();
        if (_ring is not null) { _ring.Visible = false; }
    }

    private void SelectCity(CityId id)
    {
        _selected = id;
        _cmdIndex = -1;
        CloseGroupMenu();
        _unitMenu.Visible = false;
        _selectedUnitId = -1;
        _terrainCard.Visible = false;
        _terrainHex = null;
        if (_modalLayer is null) { ClearPathMarkers(); }
        DrawSupplyZones();
        var c = _state.Cities.First(x => x.Id == id);
        var owned = c.Owner == Player;
        var known = CanInspectCity(c);
        var totalTroops = _state.Garrisons.Where(g => g.City == id).Sum(g => g.Troops);
        var govName = c.Governor is { } ggid ? _state.Generals.FirstOrDefault(x => x.Id == ggid)?.Name : null;
        var straName = c.Strategist is { } gsid ? _state.Generals.FirstOrDefault(x => x.Id == gsid)?.Name : null;
        var securityName = OfficerNameWithMonthlyEffect(c.SecurityOfficer, CommandKind.AppointSecurityOfficer, c);
        var domesticName = OfficerNameWithMonthlyEffect(c.DomesticOfficer, CommandKind.AppointDomesticOfficer, c);
        var recruitmentName = OfficerNameWithMonthlyEffect(c.RecruitmentOfficer, CommandKind.AppointRecruitmentOfficer, c);
        var trainingName = OfficerNameWithMonthlyEffect(c.TrainingOfficer, CommandKind.AppointTrainingOfficer, c);
        var pending = _state.Commands.Where(p => p.City == id).Select(p =>
            $"{KindName(p.Kind)} 남은 {p.CompletionDay - _state.Day}일");
        var facilities = $"논{c.Paddies} 밭{c.Farms} 마을{c.Villages}{(c.Workshop ? " 공방" : "")}";

        Clear(_infoRows);
        _infoRows.AddChild(MakeLabel($"《 {c.Name} 》", 15, GoldBright));
        _infoRows.AddChild(MakeLabel($"성 시야 {_vision.CityRadius(c.Castle)}칸", 13, Parchment));
        if (!owned && _state.IsScouted(Player, id))
            _infoRows.AddChild(MakeLabel($"정찰 남은 일수: {ScoutDaysLeft(id)}일", 13, GoldBright));
        if (!known)
        {
            _infoRows.AddChild(MakeLabel("시야 밖 — 아군 시야에 들어오거나 정찰에 성공하면 정보를 볼 수 있습니다.", 13, Parchment));
            var detailUnknown = MakeButton("▶ 상세");
            detailUnknown.AddThemeFontSizeOverride("font_size", 12);
            detailUnknown.CustomMinimumSize = new Vector2(0, 26);
            detailUnknown.Pressed += () => OpenCityInfoReadonly(id);
            _infoRows.AddChild(detailUnknown);
            PlacePalette(c.Position);
            _infoCard.Visible = true;
            _cmdMenu.Visible = false;
            MoveRing(c.Position);
            return;
        }

        // 짧은 수치: 4칸(라벨·값·라벨·값) 2쌍씩.
        var g4 = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        g4.AddThemeConstantOverride("h_separation", 10);
        g4.AddThemeConstantOverride("v_separation", 5);
        _infoRows.AddChild(g4);
        AddCell(g4, Sym.Coin, "금", $"{c.Gold}");
        AddCell(g4, Sym.Grain, "군량", $"{c.Provisions}");
        var (weeklyGold, weeklyProvisions) = WeeklyIncomePreview(c);
        AddCell(g4, Sym.Coin, "주 금", $"+{weeklyGold}");
        AddCell(g4, Sym.Grain, "주 군량", $"+{weeklyProvisions}");
        AddCell(g4, Sym.Sword, "주 병력", $"+{WeeklyRecruitPreview(c)}");
        AddCell(g4, Sym.Book, "주 훈련도", $"+{WeeklyTrainingPreview(c)}");
        AddCell(g4, Sym.Shield, "치안", $"{c.Security}");
        AddCell(g4, Sym.Wall, "성벽", $"{c.Wall}");

        // 긴 값: 전체폭 2칸(라벨·값).
        var g2 = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        g2.AddThemeConstantOverride("h_separation", 10);
        g2.AddThemeConstantOverride("v_separation", 5);
        _infoRows.AddChild(g2);
        AddCell(g2, Sym.Ore, "광물", $"{c.Ore}/{c.Horses}/{c.Elephants}");
        AddCell(g2, Sym.Book, "시설", facilities);
        AddCell(g2, Sym.Sword, "대기", totalTroops > 0 ? $"{totalTroops}명" : "없음");
        AddCell(g2, Sym.Shield, "부상병", WoundedForFaction(c.Owner));
        AddCell(g2, Sym.Officer, "태수", govName ?? "없음");
        AddCell(g2, Sym.Officer, "군사", straName ?? "없음");
        AddCell(g2, Sym.Shield, "치안담당", securityName ?? "없음");
        var securityBreakdown = MakeLabel(SecurityWeeklySummary(c), 12, Parchment);
        securityBreakdown.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _infoRows.AddChild(securityBreakdown);
        AddCell(g2, Sym.Coin, "내정담당", domesticName ?? "없음");
        AddCell(g2, Sym.Sword, "병력담당", recruitmentName ?? "없음");
        AddCell(g2, Sym.Book, "훈련담당", trainingName ?? "없음");
        if (pending.Any())
        {
            AddCell(g2, Sym.Scroll, "진행", string.Join(",", pending));
        }

        var depQueue = _pendingDeploys.Where(p => p.Req.City == id).Select(p => p.Label)
            .Concat(_pendingSupplyDeploys.Where(p => p.Req.City == id).Select(p => p.Label))
            .ToList();
        if (depQueue.Count > 0)
        {
            AddCell(g2, Sym.Flag, "출전대기", string.Join(",", depQueue));
        }

        var detailBtn = MakeButton("▶ 상세 · 진행 목록");
        detailBtn.AddThemeFontSizeOverride("font_size", 12);
        detailBtn.CustomMinimumSize = new Vector2(0, 26);
        detailBtn.Pressed += () => { if (owned) { OpenCityDetail(id); } else { OpenCityInfoReadonly(id); } };
        _infoRows.AddChild(detailBtn);

        PlacePalette(c.Position);
        _infoCard.Visible = true;
        _cmdMenu.Visible = owned && !_advancing; // 진행 중·적 성에는 명령 팔레트를 숨긴다(상태 카드는 보임)
        MoveRing(c.Position);
    }

    private string? OfficerName(GeneralId? id)
        => id is { } gid ? _state.Generals.FirstOrDefault(x => x.Id == gid)?.Name : null;

    private string WoundedForFaction(FactionId faction)
    {
        var wounded = _state.Armies.Where(u => u.Field.Owner == faction && u.Pool.Active > 0)
            .Sum(u => u.Pool.Wounded);
        return wounded > 0 ? $"{wounded}명" : "없음";
    }

    private string? OfficerNameWithMonthlyEffect(GeneralId? id, CommandKind kind, City city)
    {
        if (id is not { } gid) { return null; }
        var officer = _state.Generals.FirstOrDefault(x => x.Id == gid);
        return officer is null ? null : $"{officer.Name} ({OfficerWeeklyEffect(kind, officer, city)})";
    }

    private static bool IsAutoOfficerCommand(CommandKind kind)
        => kind is CommandKind.AppointSecurityOfficer or CommandKind.AppointDomesticOfficer
            or CommandKind.AppointRecruitmentOfficer or CommandKind.AppointTrainingOfficer;

    private static string OfficerRoleDescription(CommandKind kind) => kind switch
    {
        CommandKind.AppointSecurityOfficer => "치안을 담당합니다. 7일마다 무력에 따라 치안을 유지하거나 회복합니다.",
        CommandKind.AppointDomesticOfficer => "내정을 담당합니다. 7일마다 정치에 따라 금과 군량을 생산합니다.",
        CommandKind.AppointRecruitmentOfficer => "병력을 담당합니다. 7일마다 무력에 따라 도시 대기 병력을 생산하고 치안이 하락합니다.",
        CommandKind.AppointTrainingOfficer => "훈련을 담당합니다. 7일마다 무력에 따라 도시 대기 병력의 훈련도를 올립니다.",
        _ => "",
    };

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

    // 그룹 버튼 토글 — 같은 그룹 재클릭 = 닫기, 다른 그룹 = 교체.
    private void ToggleGroup(int groupIdx)
    {
        if (_openGroup == groupIdx) { CloseGroupMenu(); return; }
        _openGroup = groupIdx;
        Clear(_cmdSubList);
        _cmdSubList.AddChild(MakeLabel($"· {CmdGroups[groupIdx].Group}", 10, GoldBright));
        foreach (var i in CmdGroups[groupIdx].Indices)
        {
            var idx = i;
            var btn = MakeButton(Cmds[i].Label);
            btn.AddThemeFontSizeOverride("font_size", 11);
            btn.Alignment = HorizontalAlignment.Center;
            btn.CustomMinimumSize = new Vector2(84, 21);

            // 모병·징병은 같은 종류가 이 성에서 진행 중이면 중복 발행 금지(2026-08-23).
            var busy = Cmds[i].Kind is CommandKind.Recruit or CommandKind.Conscript
                && _selected is { } selCity
                && _state.Commands.Any(c => c.City == selCity && c.Kind == Cmds[i].Kind);
            if (busy)
            {
                btn.Text = Cmds[i].Label + " (진행중)";
                btn.Disabled = true;
            }
            else
            {
                btn.Pressed += () => { CloseGroupMenu(); OpenModal(idx); };
            }

            _cmdSubList.AddChild(btn);
        }

        PlaceGroupMenu();
        _cmdSubMenu.Visible = true;
    }

    private void CloseGroupMenu()
    {
        _openGroup = -1;
        _cmdSubMenu.Visible = false;
    }

    // 플라이아웃을 팔레트 바로 우측에 붙인다(화면 밖 clamp — 오른쪽이 좁으면 왼쪽에).
    private void PlaceGroupMenu()
    {
        var sz = _cmdSubMenu.GetCombinedMinimumSize();
        var vp = GetViewport().GetVisibleRect().Size;
        var px = _cmdMenu.Position.X + _cmdMenu.Size.X + 4f;
        if (px + sz.X > vp.X - 8f) { px = _cmdMenu.Position.X - sz.X - 4f; }
        var py = Mathf.Clamp(_cmdMenu.Position.Y, 8f, System.Math.Max(8f, vp.Y - sz.Y - 8f));
        _cmdSubMenu.Position = new Vector2(px, py);
    }

    // 줌/이동 중에도 팔레트가 선택한 성을 따라가도록 갱신.
    public override void _Process(double delta)
    {
        foreach (var (marker, unitId, offset) in _movingSupplyMarkers)
        {
            if (!_armyTokens.TryGetValue(unitId, out var token) || !IsInstanceValid(token)) { continue; }
            marker.Position = new Vector3(token.Position.X + offset.X, marker.Position.Y, token.Position.Z + offset.Z);
            marker.Visible = token.Visible;
        }
        AnimateScoutLabels(delta);
        // 미니 패널(플라이아웃)은 메인 팔레트와 운명을 같이한다 — 팔레트가 사라지면 함께 닫힘.
        if (_cmdSubMenu.Visible && !_cmdMenu.Visible) { CloseGroupMenu(); }

        if (_infoCard.Visible && _selected is { } sel)
        {
            var c = _state.Cities.FirstOrDefault(x => x.Id == sel);
            if (c is not null)
            {
                PlacePalette(c.Position);
                if (_cmdSubMenu.Visible) { PlaceGroupMenu(); }
            }
        }

        if (_unitMenu.Visible && _selectedUnitId >= 0)
        {
            var u = DisplayedArmies.FirstOrDefault(a => a.Id.Value == _selectedUnitId);
            if (u is not null) { PlaceMenu(_unitMenu, u.Field.Position, 60f); }
            else { _unitMenu.Visible = false; }
        }

        if (_terrainCard.Visible && _terrainHex is { } th)
        {
            PlaceTerrainCard(th);
            _terrainHolder.RotateY((float)delta * 0.6f); // 에셋을 천천히 빙글빙글
        }

        // 경유지 취소·확인 버튼은 매 프레임 각 지점 위로 추종(줌·팬 따라감).
        if (_depTargeting && _targetCancelBtns.Count > 0) { PlaceTargetEdit(); }

        if (_dragging && _dragPanel is not null)
        {
            if (!Input.IsMouseButtonPressed(MouseButton.Left)) { _dragging = false; }
            else { _dragPanel.Position = GetViewport().GetMousePosition() - _dragOffset; }
        }

        if (_advancing)
        {
            _animT += delta;
            RefreshPlaybackVision(delta);
            while (_animStepIdx < _animSteps.Count && _animSteps[_animStepIdx].Time <= _animT)
            {
                var s = _animSteps[_animStepIdx];
                if (s.UnitId < 0)
                {
                    if (_productionTokens.TryGetValue(-s.UnitId, out var tok)) { tok.DisplayStepTo(s.To, (float)StepSeconds); }
                }
                else if (_armyTokens.TryGetValue(s.UnitId, out var tok)) { tok.DisplayStepTo(s.To, (float)StepSeconds); }
                _animStepIdx++;
            }

            while (_animAtkIdx < _animAttacks.Count && _animAttacks[_animAtkIdx].Time <= _animT)
            {
                var a = _animAttacks[_animAtkIdx];
                if (_armyTokens.TryGetValue(a.UnitId, out var tok) && tok.Visible)
                { tok.PlayAttackMotionToward(a.FaceTo); }
                _animAtkIdx++;
            }

            while (_animUpdIdx < _animUpdates.Count && _animUpdates[_animUpdIdx].Time <= _animT)
            {
                var u = _animUpdates[_animUpdIdx];
                if (_armyTokens.TryGetValue(u.UnitId, out var tok)) { tok.SetFormationSize(FormationFor(u.Troops)); }
                if (_armyLabels.TryGetValue(u.UnitId, out var lbl)) { lbl.Text = $"{u.Troops}"; }
                _animUpdIdx++;
            }

            while (_animDmgIdx < _animDmg.Count && _animDmg[_animDmgIdx].Time <= _animT)
            {
                var d = _animDmg[_animDmgIdx];
                if (_armyTokens.TryGetValue(d.UnitId, out var tok)) { SpawnDamagePopup(tok.Position, d.Damage); }
                _animDmgIdx++;
            }

            while (_animSiegeDmgIdx < _animSiegeDmg.Count && _animSiegeDmg[_animSiegeDmgIdx].Time <= _animT)
            {
                var sd = _animSiegeDmg[_animSiegeDmgIdx];
                SpawnDamagePopup(sd.Pos, sd.Damage);
                _animSiegeDmgIdx++;
            }

            while (_animArrowIdx < _animArrows.Count && _animArrows[_animArrowIdx].Time <= _animT)
            {
                var ar = _animArrows[_animArrowIdx];
                if (_armyTokens.TryGetValue(ar.TargetUnitId, out var tok)) { SpawnCastleVolley(ar.From, tok.Position); }
                _animArrowIdx++;
            }

            while (_animSupplyArrowIdx < _animSupplyArrows.Count && _animSupplyArrows[_animSupplyArrowIdx].Time <= _animT)
            {
                var ar = _animSupplyArrows[_animSupplyArrowIdx];
                if (_armyTokens.TryGetValue(ar.UnitId, out var tok) && tok.Visible) { SpawnSupplyVolley(tok.Position, ar.Target); }
                _animSupplyArrowIdx++;
            }

            while (_animEffectIdx < _animDeathEffects.Count && _animDeathEffects[_animEffectIdx].Time <= _animT)
            {
                var e = _animDeathEffects[_animEffectIdx];
                PlayRisingSkulls(e.Pos);
                _animEffectIdx++;
            }

            while (_animProductionKillIdx < _animProductionKills.Count && _animProductionKills[_animProductionKillIdx].Time <= _animT)
            {
                var k = _animProductionKills[_animProductionKillIdx];
                if (_state.ProductionOps.FirstOrDefault(o => o.Id == k.OperationId) is { } arriving)
                    _animationProductionPositions[k.OperationId] = arriving.Target;
                if (_productionTokens.Remove(k.OperationId, out var tok)) { tok.QueueFree(); }
                if (_productionLabels.Remove(k.OperationId, out var lbl)) { lbl.QueueFree(); }
                _animProductionKillIdx++;
            }

            while (_animKillIdx < _animKills.Count && _animKills[_animKillIdx].Time <= _animT)
            {
                var k = _animKills[_animKillIdx];
                if (_armyTokens.Remove(k.UnitId, out var tok)) { tok.QueueFree(); }
                if (_armyLabels.Remove(k.UnitId, out var lbl)) { lbl.QueueFree(); }
                _animKillIdx++;
            }

            // 병력 라벨이 이동 중인 토큰을 따라가게(안 따라가면 라벨이 남아 적 위에 얹혀 보인다).
            foreach (var (uid, lbl) in _armyLabels)
            {
                if (_armyTokens.TryGetValue(uid, out var utok)) { lbl.Position = utok.Position + new Vector3(0f, 1.1f, 0f); }
            }

            var day = System.Math.Min(AnimDays, (int)(_animT / DaySeconds) + 1);
            _dayLabel.Text = $"{day}일차";
            // 하루는 이동 연출 뒤 공격 판정 슬롯으로 고정 분리한다.
            // 실제 교전이 없어도 플레이어가 진행 구조를 읽을 수 있게 "공격턴"은 항상 표시한다.
            var dayElapsed = _animT - (day - 1) * DaySeconds;
            var attacking = dayElapsed >= MoveSeconds;
            _dayTurnLabel.Text = attacking ? "⚔ 공격턴" : "▷ 이동턴";
            _dayTurnLabel.AddThemeColorOverride("font_color", attacking ? new Color(0.98f, 0.62f, 0.42f) : new Color(0.6f, 0.85f, 0.7f));
            _advanceBtn.Progress = (float)(_animT / (AnimDays * DaySeconds));

            if (_animT >= AnimDays * DaySeconds) { FinishAdvance(); }
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
        _modalDetail = null;

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
        var wideDoctrineModal = cmd.Kind == CommandKind.Research && cmd.Param == "troop";
        var mw = wideDoctrineModal
            ? Mathf.Clamp(vp.X * 0.82f, 620f, 980f)
            : Mathf.Clamp(vp.X * 0.66f, 460f, 778f);
        // 모달을 세로로 길게 — 장수 표 내부 스크롤과 겹치는 2중 스크롤 방지.
        var mh = Mathf.Clamp(vp.Y * 0.92f, 374f, 940f);
        var colOpt = (int)Mathf.Clamp(Mathf.Floor((mw + 8f) / (wideDoctrineModal ? 186f : 146f)), 3, 5);
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
        if (IsAutoOfficerCommand(cmd.Kind))
        {
            box.AddChild(MakeLabel(OfficerRoleDescription(cmd.Kind), 15, Parchment));
        }
        else if (cmd.Kind == CommandKind.Research && cmd.Param == "wall")
        {
            box.AddChild(MakeLabel("성벽 강화는 병종 전투 교리가 아니라 해당 도시의 최대 성벽을 올리는 방어 연구입니다.", 15, Parchment));
        }
        else if (cmd.Kind == CommandKind.Research && cmd.Param == "troop")
        {
            box.AddChild(MakeLabel("전투 교리는 세력 병종의 공격과 방어 보정을 올립니다. 일반 병종은 Lv.7, 주력병종은 Lv.10까지 연구할 수 있습니다.", 15, Parchment));
        }
        else if (cmd.Kind == CommandKind.SelectMajorTroop)
        {
            box.AddChild(MakeLabel("주력병종은 세력을 대표하는 병종입니다. 최대 2개까지 선택 가능하며, 한 번 적용하면 철회할 수 없습니다.", 15, Parchment));
        }
        else if (cmd.Kind == CommandKind.FormAlliance)
        {
            box.AddChild(MakeLabel($"대상 세력에 사절을 보내 동맹을 제안합니다. 비용 {_cb.AllianceGoldCost}금, 성공 확률은 수행 장수 정치로 정합니다.", 15, Parchment));
        }
        else if (cmd.Kind == CommandKind.BreakAlliance)
        {
            box.AddChild(MakeLabel("대상 세력과의 동맹을 즉시 파기합니다. 파기 후에는 다시 공격 대상이 될 수 있습니다.", 15, Parchment));
        }
        else if (cmd.Kind == CommandKind.Explore)
        {
            box.AddChild(MakeLabel("탐색은 인재 등용을 제외하고 신수, 고대유물, 지방호족, 소문/단서를 찾습니다. 실패해도 손실은 없습니다.", 15, Parchment));
        }

        box.AddChild(GoldRule());

        var cityData = _state.Cities.First(x => x.Id == city);
        if (IsAutoOfficerCommand(cmd.Kind))
        {
            AddClearOfficerButton(box, cityData, cmd.Kind);
        }

        if (cmd.Kind == CommandKind.RecruitHero)
        {
            box.AddChild(MakeLabel("조건을 달성해 해금된 세력형/도시형/유랑 위인을 금을 내고 영입합니다.", 15, Parchment));
            BuildHeroRecruitCards(box, cityData);
            var contentHeroH = box.GetCombinedMinimumSize().Y;
            scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentHeroH, mh));
            return;
        }

        var options = OptionList(cmd, cityData);
        _optionCards.Clear();
        _autoRecruitRateCards.Clear();
        _disabledOptions.Clear();
        _modalMultiParams.Clear();
        _modalParam = cmd.Param == "tax" ? 2 : 0;
        _autoRecruitRateParam = cityData.RecruitmentOfficer is null
            ? 1
            : System.Math.Clamp(cityData.AutoRecruitRate <= 0 ? 1 : cityData.AutoRecruitRate, 1, 3);
        if (cmd.Param == "faction" && options.Count == 0)
        {
            var empty = cmd.Kind == CommandKind.FormAlliance
                ? "동맹을 제안할 수 있는 세력이 없습니다."
                : cmd.Kind == CommandKind.BreakAlliance
                    ? "파기할 동맹 세력이 없습니다."
                    : "선택할 대상 세력이 없습니다.";
            box.AddChild(MakeLabel(empty, 17, Parchment));
            var emptyContentH = box.GetCombinedMinimumSize().Y;
            scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(emptyContentH, mh));
            return;
        }

        if (cmd.Param == "facility")
        {
            for (var i = 0; i < options.Count; i++)
            {
                if (IsFacilityBuildDisabled(cityData, Facilities[i].Code))
                {
                    _disabledOptions.Add(i);
                }
            }

            if (_disabledOptions.Contains(_modalParam))
            {
                _modalParam = Enumerable.Range(0, options.Count).FirstOrDefault(i => !_disabledOptions.Contains(i));
            }
        }
        if (cmd.Kind == CommandKind.SelectMajorTroop)
        {
            var selectedMajors = _state.MajorTroops.Where(t => t.Faction == cityData.Owner)
                .Select(t => t.TroopCode)
                .ToHashSet(System.StringComparer.Ordinal);
            var majorOptions = MajorTroopOptions(cityData);
            for (var i = 0; i < majorOptions.Count; i++)
            {
                if (!selectedMajors.Contains(majorOptions[i].Code) && selectedMajors.Count >= 2)
                {
                    _disabledOptions.Add(i);
                }
            }
        }
        if (options.Count > 0)
        {
            if (cmd.Kind == CommandKind.AppointRecruitmentOfficer)
            {
                var current = CurrentAutoRecruitTroopCodes(cityData).ToHashSet(System.StringComparer.Ordinal);
                var autoOptions = AutoRecruitTroopOptions();
                for (var i = 0; i < autoOptions.Count; i++)
                {
                    if (current.Contains(autoOptions[i].Code)) { _modalMultiParams.Add(i); }
                }

                if (_modalMultiParams.Count == 0) { _modalMultiParams.Add(0); }
            }

            var optionTitle = cmd.Kind == CommandKind.AppointRecruitmentOfficer
                ? "자동 생산 병종을 선택하세요 (여러 개 선택 가능)"
                : cmd.Kind == CommandKind.SelectMajorTroop ? "주력병종을 선택하세요 (1~2개 선택 후 적용)"
                : cmd.Param == "stratagem" ? "계략을 선택하세요" : "대상을 선택하세요";
            box.AddChild(MakeLabel(optionTitle, 19, GoldBright));
            var grid = new GridContainer { Columns = System.Math.Min(colOpt, options.Count) };
            grid.AddThemeConstantOverride("h_separation", 10);
            grid.AddThemeConstantOverride("v_separation", 10);
            box.AddChild(grid);
            for (var i = 0; i < options.Count; i++)
            {
                var idx = i;
                var disabled = _disabledOptions.Contains(idx);
                var card = OptionCard(options[i], disabled);
                _optionCards.Add(card);
                if (!disabled)
                {
                    card.GuiInput += e =>
                    {
                        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                        {
                            if (cmd.Kind == CommandKind.AppointRecruitmentOfficer || cmd.Kind == CommandKind.SelectMajorTroop) { ToggleMultiOption(idx, options[idx]); }
                            else { PickOption(idx, options[idx]); }
                        }
                    };
                }
                grid.AddChild(card);
            }
        }

        if (cmd.Kind == CommandKind.AppointRecruitmentOfficer)
        {
            AddAutoRecruitRatePicker(box, options);
        }

        if (cmd.Kind == CommandKind.Research)
        {
            AddResearchFundingPicker(box, cityData);
        }

        _modalDetail = MakeLabel("", 17, Parchment);
        box.AddChild(_modalDetail);
        box.AddChild(GoldRule());

        if (cmd.Kind == CommandKind.SelectMajorTroop)
        {
            var apply = MakeButton("적용", accent: true);
            apply.CustomMinimumSize = new Vector2(140, 38);
            apply.Pressed += () => ConfirmMajorTroops(cityData.Id);
            box.AddChild(apply);
            RefreshMultiOptionCards(options);
            var contentMajorH = box.GetCombinedMinimumSize().Y;
            scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentMajorH, mh));
            return;
        }

        // 도시 계략: 대상 도시 선택 — 수행 장수보다 먼저(계략 → 대상 → 장수 순).
        // 성이 많아도 안전하게 고정 높이 표(내부 스크롤)로 목록을 담는다.
        _stratTarget = null;
        if (cmd.Param == "stratagem")
        {
            var enemies = _state.Cities.Where(c => c.Owner != Player).OrderBy(c => c.Id.Value).ToList();
            if (enemies.Count > 0)
            {
                _stratTarget = enemies[0].Id;
                box.AddChild(MakeLabel("대상 도시 (행 클릭 = 선택)", 19, GoldBright));
                var from = _state.Cities.First(x => x.Id == city).Position;
                var cityTree = new Tree
                {
                    Columns = 3,
                    ColumnTitlesVisible = true,
                    HideRoot = true,
                    SelectMode = Tree.SelectModeEnum.Row,
                    CustomMinimumSize = new Vector2(0, System.Math.Min(46 + (enemies.Count * 34), 150)),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                };
                cityTree.AddThemeFontOverride("font", _font);
                cityTree.AddThemeFontSizeOverride("font_size", 15);
                cityTree.AddThemeFontOverride("title_button_font", _font);
                cityTree.AddThemeFontSizeOverride("title_button_font_size", 14);
                cityTree.SetColumnTitle(0, "도시");
                cityTree.SetColumnExpand(0, true);
                cityTree.SetColumnExpandRatio(0, 2);
                foreach (var (col, t) in new[] { (1, "거리"), (2, "소요일") })
                {
                    cityTree.SetColumnTitle(col, t);
                    cityTree.SetColumnExpand(col, false);
                    cityTree.SetColumnCustomMinimumWidth(col, 76);
                }

                var croot = cityTree.CreateItem();
                foreach (var enemyCity in enemies)
                {
                    var item = cityTree.CreateItem(croot);
                    item.SetText(0, enemyCity.Name);
                    item.SetText(1, $"{from.Distance(enemyCity.Position)}칸");
                    item.SetText(2, $"{CityStratagems.Days(from, enemyCity.Position, _cb)}일");
                    item.SetMetadata(0, enemyCity.Id.Value);
                    item.SetTextAlignment(1, HorizontalAlignment.Center);
                    item.SetTextAlignment(2, HorizontalAlignment.Center);
                    if (enemyCity.Id == _stratTarget) { item.Select(0); }
                }

                cityTree.ItemSelected += () =>
                {
                    var it = cityTree.GetSelected();
                    if (it is not null) { _stratTarget = new CityId(it.GetMetadata(0).AsInt32()); }
                };
                box.AddChild(cityTree);
                box.AddChild(GoldRule());
            }
        }

        box.AddChild(MakeLabel("수행 장수 (행 클릭 = 실행 · 상단 눌러 정렬)", 19, GoldBright));
        _offSortCol = -1;
        _offSortAsc = false;
        _modalOfficers = new VBoxContainer();
        box.AddChild(_modalOfficers);
        BuildOfficerCards(city, cmdIndex);

        if (options.Count > 0)
        {
            if (cmd.Kind == CommandKind.AppointRecruitmentOfficer) { RefreshMultiOptionCards(options); }
            else { PickOption(_modalParam, options[_modalParam]); }
        }

        // 스크롤 높이를 내용에 맞추되 mh로 상한 → 짧은 명령은 아래 여백 없음, 긴 건 스크롤.
        var contentH = box.GetCombinedMinimumSize().Y;
        scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentH, mh));
    }

    private void CloseModal()
    {
        _openCityDetailCity = null;
        if (_modalLayer is not null)
        {
            _modalLayer.QueueFree();
            _modalLayer = null;
        }

        _optionCards.Clear();
        _autoRecruitRateCards.Clear();
        _researchFundingRatios.Clear();
        _researchFundingPreview = null;
        _modalMultiParams.Clear();
        _depTroopCards.Clear();
        _depVanCards.Clear();
        _depAdjCards.Clear();
        _depAmountSpin = null;
        _depPreview = null;
        _depModeButtons.Clear();
        _vanTree = null;
        _depEditIndex = -1;
        _depTarget = null;
        _stratTarget = null;
        ClearPathMarkers(); // 편성이 닫히면 예약 경로도 지도에서 지운다
    }

    private bool CloseAnyModalOrPanel()
    {
        if (_confirmLayer is not null)
        {
            _confirmLayer.QueueFree();
            _confirmLayer = null;
            return true;
        }

        if (_modalLayer is not null)
        {
            CloseModal();
            return true;
        }

        if (_terrainCard.Visible || _infoCard.Visible || _cmdMenu.Visible || _unitMenu.Visible || _cmdSubMenu.Visible)
        {
            _selected = null;
            HidePanels();
            return true;
        }

        return false;
    }

    // ── 출전 모달: 병종 + 선봉(+부관) 선택 → 대기 병력을 야전 부대로 편성 ──
    private void OpenDeployModal(CityId city)
    {
        _depModalCity = city;
        _depSelectedUnit = -1;
        OpenDeployHub();
    }

    private void OpenSupplyHub(CityId city)
    {
        _depModalCity = city;
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        _depSelectedUnit = -1;

        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.52f, 440f, 680f);
        var mh = Mathf.Clamp(vp.Y * 0.78f, 350f, 620f);
        var box = DeployScaffold(mw, out var scroll, out var panel);
        var cityName = _state.Cities.First(x => x.Id == city).Name;
        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var title = MakeLabel($"◈  보급부대 편성   《 {cityName} 》  ⠿", 19, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        titleRow.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(34, 32);
        close.Pressed += () => { CloseModal(); SelectCity(city); };
        titleRow.AddChild(close);
        box.AddChild(MakeLabel("보급부대는 어떤 병종이든 편성할 수 있고, 출전 후 공격 명령도 가능합니다.\n단, 액티브·패시브·병종 적성은 적용되지 않으며 공방은 게임 최하 수치입니다.", 12, Parchment));
        box.AddChild(GoldRule());

        var mine = Enumerable.Range(0, _pendingSupplyDeploys.Count)
            .Where(i => _pendingSupplyDeploys[i].Req.City == city)
            .ToList();
        box.AddChild(MakeLabel($"예약된 보급부대 ({mine.Count})", 14, GoldBright));
        if (mine.Count == 0)
        {
            box.AddChild(MakeLabel("(없음)", 12, Parchment));
        }

        foreach (var i in mine)
        {
            var idx = i;
            var req = _pendingSupplyDeploys[idx].Req;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            var target = req.Target is { } tg
                ? _state.Cities.FirstOrDefault(c => c.Position == tg)?.Name ?? $"({tg.Q},{tg.R})"
                : "목표 미지정";
            var lbl = MakeLabel("· " + _pendingSupplyDeploys[idx].Label + $" · {target}", 12, Parchment);
            lbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(lbl);
            var targetBtn = MakeButton("목표");
            targetBtn.CustomMinimumSize = new Vector2(54, 28);
            targetBtn.Pressed += () => BeginSupplyTargeting(idx);
            row.AddChild(targetBtn);
            var edit = MakeButton("수정");
            edit.CustomMinimumSize = new Vector2(54, 28);
            edit.Pressed += () => OpenSupplyCompose(idx);
            row.AddChild(edit);
            var del = MakeButton("삭제");
            del.CustomMinimumSize = new Vector2(54, 28);
            del.Pressed += () => { _pendingSupplyDeploys.RemoveAt(idx); OpenSupplyHub(city); };
            row.AddChild(del);
            box.AddChild(row);
        }

        var add = MakeButton("＋ 보급부대 추가", accent: true);
        add.CustomMinimumSize = new Vector2(0, 36);
        add.Pressed += () => OpenSupplyCompose(-1);
        box.AddChild(add);
        box.AddChild(MakeLabel("지도에서는 병력 규모와 무관하게 전용 보급부대 모델(troop-supply.glb)로 표시됩니다.", 11, Parchment));

        var contentH = box.GetCombinedMinimumSize().Y;
        scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentH, mh));
        CenterAndDrag(panel, titleRow, mw, mh, box);
        DrawDeployPaths();
    }

    private void OpenSupplyCompose(int editIndex)
    {
        var city = _depModalCity;
        _supplyEditIndex = editIndex;
        _supplyDraft.Clear();
        _supplyTroopCards.Clear();
        _depVanCards.Clear();
        _depVan = null;
        _depPreview = null;
        _depProvDays = 0;
        _depProvSlider = null;
        _depProvLabel = null;
        _vanTree = null;
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }

        var usedTroops = ReservedTroopsByCode(city, editIndex, editingSupply: true);
        var usedGens = ReservedDeployGenerals(editIndex, editingSupply: true);
        if (editIndex >= 0 && editIndex < _pendingSupplyDeploys.Count)
        {
            var req = _pendingSupplyDeploys[editIndex].Req;
            _depVan = req.Vanguard;
            foreach (var line in req.Lines) { _supplyDraft[line.TroopCode] = line.Troops; }
            _depProvDays = SupplyProvisionDaysFromAmount(req.Provisions);
        }

        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.55f, 460f, 720f);
        var mh = Mathf.Clamp(vp.Y * 0.88f, 370f, 760f);
        var box = DeployScaffold(mw, out var scroll, out var panel);
        var cityName = _state.Cities.First(x => x.Id == city).Name;
        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var title = MakeLabel($"◈  {(editIndex >= 0 ? "보급부대 수정" : "보급부대 추가")}   《 {cityName} 》  ⠿", 18, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        titleRow.AddChild(title);
        var back = MakeButton("◀ 목록");
        back.CustomMinimumSize = new Vector2(60, 32);
        back.Pressed += () => OpenSupplyHub(city);
        titleRow.AddChild(back);
        box.AddChild(GoldRule());

        box.AddChild(MakeLabel($"보급 병력 선택 (총원 최대 {_cb.SupplyMaxTroops}명)", 13, GoldBright));
        foreach (var gar in _state.Garrisons.Where(g => g.City == city && g.Troops > 0 && !g.Trainee).OrderBy(g => g.TroopCode))
        {
            var template = _troops.FirstOrDefault(t => t.Code == gar.TroopCode);
            var available = System.Math.Max(0, gar.Troops - usedTroops.GetValueOrDefault(gar.TroopCode, 0));
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.AddChild(new TextureRect
            {
                Texture = template is not null ? ClassEmblem(template.Class) : Icon(Sym.People),
                CustomMinimumSize = new Vector2(34, 34),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            });
            var name = MakeLabel($"{template?.Name ?? gar.TroopCode} · 가능 {available} · 훈{gar.TrainingLevel}", 12, Parchment);
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(name);
            var spin = new SpinBox
            {
                MinValue = 0,
                MaxValue = System.Math.Min(available, _cb.SupplyMaxTroops),
                Step = 100,
                Value = System.Math.Min(available, _supplyDraft.GetValueOrDefault(gar.TroopCode, 0)),
                CustomMinimumSize = new Vector2(120, 30),
            };
            spin.AddThemeFontOverride("font", _font);
            spin.ValueChanged += v =>
            {
                var n = (int)v;
                var otherTotal = _supplyDraft.Where(p => p.Key != gar.TroopCode).Sum(p => p.Value);
                var maxForThis = System.Math.Max(0, System.Math.Min(available, _cb.SupplyMaxTroops - otherTotal));
                if (n > maxForThis)
                {
                    n = maxForThis;
                    spin.SetValueNoSignal(n);
                }
                if (n <= 0) { _supplyDraft.Remove(gar.TroopCode); }
                else { _supplyDraft[gar.TroopCode] = n; }
                SyncSupplyProvisionSlider();
                UpdateSupplyPreview();
            };
            row.AddChild(spin);
            box.AddChild(row);
        }

        box.AddChild(GoldRule());
        box.AddChild(MakeLabel("군량 (보급 일수)", 13, GoldBright));
        var provRow = new HBoxContainer();
        provRow.AddThemeConstantOverride("separation", 10);
        _depProvSlider = new HSlider
        {
            MinValue = 0,
            MaxValue = 50,
            Step = 1,
            Value = _depProvDays,
            CustomMinimumSize = new Vector2(260, 24),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        _depProvSlider.ValueChanged += v => { _depProvDays = (int)v; UpdateSupplyPreview(); };
        provRow.AddChild(_depProvSlider);
        _depProvLabel = MakeLabel("", 12, Parchment);
        _depProvLabel.CustomMinimumSize = new Vector2(240, 0);
        provRow.AddChild(_depProvLabel);
        box.AddChild(provRow);
        SyncSupplyProvisionSlider();

        box.AddChild(GoldRule());
        box.AddChild(MakeLabel("주장 선택 (보급부대는 부관 없음 · 상단 눌러 정렬)", 13, GoldBright));
        _composeFree = _state.GeneralsAt(city)
            .Where(g => !_state.IsGeneralBusy(g) && !usedGens.Contains(g))
            .OrderBy(g => g.Value)
            .ToList();
        _vanTree = new Tree
        {
            Columns = 6,
            ColumnTitlesVisible = true,
            HideRoot = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0, 190),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _vanTree.AddThemeFontOverride("font", _font);
        _vanTree.AddThemeFontSizeOverride("font_size", 13);
        _vanTree.AddThemeFontOverride("title_button_font", _font);
        _vanTree.AddThemeFontSizeOverride("title_button_font_size", 12);
        _vanTree.SetColumnTitle(0, "주장"); _vanTree.SetColumnExpand(0, false); _vanTree.SetColumnCustomMinimumWidth(0, 52);
        _vanTree.SetColumnTitle(1, "이름"); _vanTree.SetColumnExpand(1, true); _vanTree.SetColumnExpandRatio(1, 2);
        foreach (var (col, titleText) in new[] { (2, "무"), (3, "지"), (4, "정") })
        {
            _vanTree.SetColumnTitle(col, titleText);
            _vanTree.SetColumnExpand(col, false);
            _vanTree.SetColumnCustomMinimumWidth(col, 42);
        }
        _vanTree.SetColumnTitle(5, "현재 담당업무");
        _vanTree.SetColumnExpand(5, true);
        _vanTree.ColumnTitleClicked += (col, _) =>
        {
            var c = (int)col;
            if (c == 0) { return; }
            _vanSortCol = c;
            _vanSortAsc = !_vanSortAsc;
            CallDeferred(nameof(PopulateSupplyGeneralTreeDeferred));
        };
        _vanTree.ItemSelected += () =>
        {
            var it = _vanTree.GetSelected();
            if (it is null) { return; }
            _depVan = new GeneralId(it.GetMetadata(0).AsInt32());
            RestyleSupplyGeneralTreeSelection();
            UpdateSupplyPreview();
        };
        box.AddChild(_vanTree);
        PopulateSupplyGeneralTree();

        box.AddChild(GoldRule());
        _depPreview = MakeLabel("", 12, Parchment);
        _depPreview.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_depPreview);
        var save = MakeButton("▶ 확인", accent: true);
        save.CustomMinimumSize = new Vector2(0, 34);
        save.Pressed += SaveSupplyCompose;
        box.AddChild(save);
        UpdateSupplyPreview();

        var contentH = box.GetCombinedMinimumSize().Y;
        scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentH, mh));
        CenterAndDrag(panel, titleRow, mw, mh, box);
    }

    // 병종 코드 → 한글 이름(성벽 연구 코드 포함).
    private string TroopName(string code) => code == FactionResearch.WallCode ? "성벽"
        : code == FactionResearch.CommandTroopsCode ? "통솔 병력"
        : _troops.FirstOrDefault(t => t.Code == code)?.Name ?? code;

    // 명령 한 줄 설명(상세 모달용): 종류 · 파라미터 — 장수 · 남은 일수.
    private string CmdText(CityCommand c)
    {
        var main = _state.Generals.FirstOrDefault(g => g.Id == c.Main)?.Name ?? $"G{c.Main.Value}";
        var param = c.TroopCode.Length > 0
            ? $" · {TroopName(c.TroopCode)}{(c.TraineePool ? "(신병)" : "")}"
            : c.Facility.Length > 0
                ? $" · {FacilityLabel(c.Facility)}"
                : "";
        var target = c.TargetCity is { } tc
            ? $" → {_state.Cities.FirstOrDefault(x => x.Id == tc)?.Name ?? $"성{tc.Value}"}"
            : "";
        return $"{KindName(c.Kind)}{param}{target} — {main} · 남은 {System.Math.Max(0, c.CompletionDay - _state.Day)}일";
    }

    private static string FacilityLabel(string code)
    {
        foreach (var (label, c) in Repairables)
        {
            if (c == code) { return label; }
        }

        foreach (var (label, c) in Strats)
        {
            if (c == code) { return label; }
        }

        return code;
    }

    // ── 성 상세 모달: 도시 수치 + 진행 중 명령(취소) + 출전 예약(취소) ──
    private void OpenCityDetail(CityId city)
    {
        if (_state.Cities.FirstOrDefault(c => c.Id == city)?.Owner != Player)
        {
            OpenCityInfoReadonly(city);
            return;
        }
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        _openCityDetailCity = city;
        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.52f, 460f, 680f);
        var mh = Mathf.Clamp(vp.Y * 0.88f, 420f, 820f);
        var box = DeployScaffold(mw, out var scroll, out var panel);
        var c = _state.Cities.First(x => x.Id == city);

        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var detailGov = OfficerName(c.Governor);
        var detailStra = OfficerName(c.Strategist);
        var title = MakeLabel($"《 {c.Name} 》 · 태수 {detailGov ?? "없음"} · 군사 {detailStra ?? "없음"}", 16, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        titleRow.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(46, 34);
        close.Pressed += CloseModal;
        titleRow.AddChild(close);
        box.AddChild(GoldRule());

        // 도시 수치(정보 카드보다 상세).
        var g4 = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        g4.AddThemeConstantOverride("h_separation", 12);
        g4.AddThemeConstantOverride("v_separation", 4);
        box.AddChild(g4);
        AddCell(g4, Sym.Coin, "금", $"{c.Gold}");
        AddCell(g4, Sym.Grain, "군량", $"{c.Provisions}");
        var (weeklyGold, weeklyProvisions) = WeeklyIncomePreview(c);
        AddCell(g4, Sym.Coin, "주 금", $"+{weeklyGold}");
        AddCell(g4, Sym.Grain, "주 군량", $"+{weeklyProvisions}");
        AddCell(g4, Sym.Sword, "주 병력", WeeklyRecruitSummary(c));
        AddCell(g4, Sym.Book, "주 훈련도", $"+{WeeklyTrainingPreview(c)}");
        AddCell(g4, Sym.Shield, "치안", $"{c.Security}");
        AddCell(g4, Sym.Wall, "성벽", $"{c.Wall}");
        AddCell(g4, Sym.Shield, "부상병", WoundedForFaction(c.Owner));
        AddCell(g4, Sym.Ore, "광석", $"{c.Ore}");
        AddCell(g4, Sym.Ore, "말/코끼리", $"{c.Horses}/{c.Elephants}");

        var officers = $"치안 {OfficerName(c.SecurityOfficer) ?? "없음"} · 내정 {OfficerName(c.DomesticOfficer) ?? "없음"}\n"
            + $"병력 {OfficerName(c.RecruitmentOfficer) ?? "없음"} · 훈련 {OfficerName(c.TrainingOfficer) ?? "없음"}";
        box.AddChild(MakeLabel($"담당자: {officers}", 13, GoldBright));
        var outputPercent = _cb.LowSecurityOutputPercent(c.Security);
        var raid = _cb.BanditRaidChance(c.Security);
        var existingRaid = _state.Armies.Any(a => a.Field.Owner == WorldEngine.BanditFaction
            && a.Pool.Active > 0 && a.Field.Target == c.Position);
        var securityDetails = MakeLabel(SecurityWeeklySummary(c)
            + $"\n치안 {c.Security} · 금/군량/병력/훈련 산출 {outputPercent}% (감소 {100 - outputPercent}%)"
            + (existingRaid ? "\n도적 습격 중 · 같은 성에 추가 출현 없음"
                : raid.Percent > 0 ? $"\n진행 종료 시 도적 출현 확률 {raid.Percent}% · 병력 {raid.Troops:N0}명"
                : "\n도적 출현 위험 없음"), 13, outputPercent < 100 ? AccentFill : Parchment);
        securityDetails.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(securityDetails);

        var garr = _state.Garrisons.Where(g => g.City == city)
            .OrderBy(g => g.TroopCode, System.StringComparer.Ordinal).ThenBy(g => g.Trainee).ToList();
        box.AddChild(MakeLabel($"대기 병력  (총 {garr.Sum(g => g.Troops)}명)", 14, GoldBright));
        if (garr.Count == 0) { box.AddChild(MakeLabel("(없음)", 12, Parchment)); }
        else
        {
            // 카드가 많으면 가로 스크롤만(세로 스크롤은 모달 바깥 하나로 통일).
            var hscroll = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(0, 92),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            };
            box.AddChild(hscroll);
            var strip = new HBoxContainer();
            strip.AddThemeConstantOverride("separation", 6);
            hscroll.AddChild(strip);
            foreach (var g in garr) { strip.AddChild(GarrisonCard(g)); }
        }

        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 2) });

        // 탭 3종: 주둔 장수 / 진행 중 명령 / 출전 예약. 탭 막대와 내용 패널을 붙여(간격 0)
        // 폴더 탭처럼 — 활성 탭은 내용 패널과 같은 색·아래 테두리 없이 이어지고, 비활성은 어둡게 물러난다.
        var stationed = _state.GeneralsAt(city).OrderBy(x => x.Value)
            .Select(id => _state.Generals.First(x => x.Id == id)).ToList();
        var cmds = _state.Commands.Where(x => x.City == city).OrderBy(x => x.CompletionDay).ToList();
        var deploys = new List<int>();
        for (var i = 0; i < _pendingDeploys.Count; i++)
        {
            if (_pendingDeploys[i].Req.City == city) { deploys.Add(i); }
        }
        var supplyDeploys = new List<int>();
        for (var i = 0; i < _pendingSupplyDeploys.Count; i++)
        {
            if (_pendingSupplyDeploys[i].Req.City == city) { supplyDeploys.Add(i); }
        }

        var tabWrap = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        tabWrap.AddThemeConstantOverride("separation", 0);
        box.AddChild(tabWrap);

        var tabBar = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        tabBar.AddThemeConstantOverride("separation", 3);
        tabWrap.AddChild(tabBar);

        var contentPanel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        contentPanel.AddThemeStyleboxOverride("panel", TabContentStyle());
        tabWrap.AddChild(contentPanel);
        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 6);
        contentPanel.AddChild(content);

        var labels = new[] { $"주둔 장수 {stationed.Count}", $"진행 명령 {cmds.Count}", $"예약 {deploys.Count + supplyDeploys.Count}" };
        var tabBtns = new Button[3];
        void ShowTab(int t)
        {
            _cityDetailTab = t;
            for (var i = 0; i < 3; i++)
            {
                var on = i == t;
                tabBtns[i].AddThemeColorOverride("font_color", on ? GoldBright : new Color(Parchment, 0.6f));
                tabBtns[i].AddThemeColorOverride("font_hover_color", on ? GoldBright : Parchment);
                tabBtns[i].AddThemeStyleboxOverride("normal", TabStyle(on));
                tabBtns[i].AddThemeStyleboxOverride("hover", TabStyle(on));
                tabBtns[i].AddThemeStyleboxOverride("pressed", TabStyle(on));
            }

            Clear(content);
            switch (t)
            {
                case 0: BuildStationedTab(content, city, stationed); break;
                case 1: BuildCommandsTab(content, city, cmds); break;
                default: BuildDeployTab(content, city, deploys, supplyDeploys); break;
            }

            var h = box.GetCombinedMinimumSize().Y;
            scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(h, mh));
        }

        for (var i = 0; i < 3; i++)
        {
            var t = i;
            var b = new Button { Text = labels[i], SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            b.AddThemeFontOverride("font", _font);
            b.AddThemeFontSizeOverride("font_size", 13);
            b.AddThemeColorOverride("font_pressed_color", GoldBright);
            b.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            b.CustomMinimumSize = new Vector2(0, 30);
            b.Pressed += () => ShowTab(t);
            tabBtns[i] = b;
            tabBar.AddChild(b);
        }

        ShowTab(Mathf.Clamp(_cityDetailTab, 0, 2));
        CenterAndDrag(panel, titleRow, mw, mh, box);
    }

    // 폴더 탭 모양 — 위 모서리만 둥글고 아래는 각짐. 활성은 내용 패널과 같은 색·아래 테두리 없이 이어지고,
    // 비활성은 어둡게 물러나 아래 테두리로 닫힌다.
    private static readonly Color TabFill = new(0.17f, 0.11f, 0.085f); // 내용 패널 바탕(Ink보다 살짝 밝음)

    private StyleBoxFlat TabStyle(bool active)
    {
        var s = new StyleBoxFlat { BgColor = active ? TabFill : Ink, BorderColor = active ? Gold : new Color(Gold, 0.4f) };
        s.BorderWidthTop = s.BorderWidthLeft = s.BorderWidthRight = active ? 2 : 1;
        s.BorderWidthBottom = active ? 0 : 1; // 활성은 아래로 열려 내용 패널과 이어진다
        s.CornerRadiusTopLeft = s.CornerRadiusTopRight = 8;
        s.CornerRadiusBottomLeft = s.CornerRadiusBottomRight = 0;
        s.ContentMarginLeft = s.ContentMarginRight = 6;
        s.ContentMarginTop = active ? 7 : 5;
        s.ContentMarginBottom = active ? 7 : 5;
        return s;
    }

    private StyleBoxFlat TabContentStyle()
    {
        var s = new StyleBoxFlat { BgColor = TabFill, BorderColor = Gold };
        s.BorderWidthTop = 0; // 위는 활성 탭이 이어받는다
        s.BorderWidthLeft = s.BorderWidthRight = s.BorderWidthBottom = 2;
        s.CornerRadiusTopLeft = s.CornerRadiusTopRight = 0;
        s.CornerRadiusBottomLeft = s.CornerRadiusBottomRight = 10;
        s.ContentMarginLeft = s.ContentMarginRight = 10;
        s.ContentMarginTop = s.ContentMarginBottom = 9;
        return s;
    }

    private (int Gold, int Provisions) WeeklyIncomePreview(City city)
    {
        if (_cb.AutoOfficerSystemEnabled)
        {
            var domestic = city.DomesticOfficer is { } did
                ? _state.Generals.FirstOrDefault(g => g.Id == did)
                : null;
            return domestic is null
                ? (0, 0)
                : (WeeklyIncomeAmount(ApplyLowSecurityOutputPenalty(
                        _cb.AutoDomesticGoldBase + domestic.Politics * _cb.AutoDomesticGoldPoliticsMultiplier, city.Security)),
                    WeeklyIncomeAmount(ApplyLowSecurityOutputPenalty(
                        _cb.AutoDomesticProvisionsBase + domestic.Politics * _cb.AutoDomesticProvisionsPoliticsMultiplier, city.Security)));
        }

        var governor = city.Governor is { } gid ? _state.Generals.FirstOrDefault(g => g.Id == gid) : null;
        var effective = governor is not null && governor.Politics >= _balance.GovernorMinPolitics;
        var goldBase = GoldBase(city.Castle) + FacilityOutput(city, "village", city.Villages, _balance.VillageGold);
        var provisionsBase = ProvisionsBase(city.Castle)
            + FacilityOutput(city, "paddy", city.Paddies, _balance.PaddyProvisions)
            + FacilityOutput(city, "farm", city.Farms, _balance.FarmProvisions);
        var gold = ScaleMonthlyIncome(goldBase, city, effective, governor, effective ? AdminBonus.Bucket(governor, _adminSkillMap, "tax") : 0);
        var provisions = ScaleMonthlyIncome(provisionsBase, city, effective, governor, effective ? AdminBonus.Bucket(governor, _adminSkillMap, "harvest") : 0);
        return (gold, provisions);
    }

    private int WeeklyRecruitPreview(City city)
    {
        var officer = city.RecruitmentOfficer is { } gid ? _state.Generals.FirstOrDefault(g => g.Id == gid) : null;
        return officer is null
            ? 0
            : AutoRecruitWeeklyTroopsFor(officer, city);
    }

    private string WeeklyRecruitSummary(City city)
    {
        var officer = city.RecruitmentOfficer is { } gid ? _state.Generals.FirstOrDefault(g => g.Id == gid) : null;
        if (officer is null) { return "+0"; }

        var troopCodes = string.Join(',', CurrentAutoRecruitTroopCodes(city));
        var troops = AutoRecruitWeeklyTroopsFor(officer, city);
        var cost = AutoRecruitWeeklyCostFor(officer, troopCodes, city);
        var rate = CommandBalance.AutoRecruitRate(city.AutoRecruitRate);
        return $"+{troops} {AutoRecruitTroopNames(troopCodes)} / -{cost}금 / {rate}배";
    }

    private string AutoRecruitTroopCode(City city)
        => CurrentAutoRecruitTroopCodes(city).First();

    private IEnumerable<string> CurrentAutoRecruitTroopCodes(City city)
    {
        var codes = city.AutoRecruitTroopCodes.Split(',', System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries);
        if (codes.Length > 0) { return codes; }
        return string.IsNullOrWhiteSpace(city.AutoRecruitTroopCode) ? [_cb.AutoRecruitDefaultTroopCode] : [city.AutoRecruitTroopCode];
    }

    private int WeeklyTrainingPreview(City city)
    {
        var officer = city.TrainingOfficer is { } gid ? _state.Generals.FirstOrDefault(g => g.Id == gid) : null;
        return officer is null
            ? 0
            : ApplyLowSecurityOutputPenalty(System.Math.Max(1, OfficerMightTier(officer.Might) + 1),
                city.Security);
    }

    private int FacilityOutput(City city, string code, int intactCount, int baseOutput)
    {
        var placements = _state.Placements
            .Where(p => p.City == city.Id && p.Code == code)
            .OrderByDescending(p => FacilityHealth.OutputMultiplier(p.HitPoints))
            .ThenByDescending(p => p.HitPoints)
            .Take(intactCount)
            .ToList();
        var output = placements.Sum(p => baseOutput * FacilityHealth.OutputMultiplier(p.HitPoints));
        return output + System.Math.Max(0, intactCount - placements.Count) * baseOutput;
    }

    private int ScaleMonthlyIncome(int baseAmount, City city, bool effectiveGovernor, General? governor, int bucketPercent)
    {
        var amount = baseAmount * (100 + bucketPercent) / 100;
        var rate = System.Math.Clamp(city.TaxRate, 0, _balance.TaxRateMax);
        if (effectiveGovernor)
        {
            var span = 100 - _balance.GovernorMinPolitics;
            var amplify = span <= 0 ? 0 : System.Math.Max(0, governor!.Politics - _balance.GovernorMinPolitics)
                * _balance.GovernorTaxAmplifyAt100 / span;
            var effectiveRate = rate * (100 + amplify) / 100;
            amount = amount * effectiveRate / _balance.TaxRateBase;
        }
        else
        {
            amount = amount * rate / _balance.TaxRateBase;
            amount = amount * _balance.NoGovernorIncomePercent / 100;
        }

        amount = amount * PopulationFillPercent(city) / 100;
        if (city.Security < _balance.SecurityLowThreshold)
        {
            amount = amount * _balance.SecurityLowIncomePercent / 100;
        }

        return amount;
    }

    private int ApplyLowSecurityOutputPenalty(int amount, int security)
        => amount * _cb.LowSecurityOutputPercent(security) / 100;

    private int WeeklyIncomeAmount(int amount)
        => amount / 4 + ((_state.Day / 7 % 4 + 1) <= amount % 4 ? 1 : 0);

    private int PopulationFillPercent(City city)
    {
        var max = PopulationMax(city.Castle);
        if (max <= 0) { return 100; }
        var fill = System.Math.Min(city.Population, max);
        return _balance.PopulationIncomeFloorPercent
            + (100 - _balance.PopulationIncomeFloorPercent) * fill / max;
    }

    private int PopulationMax(CastleSize castle) => castle switch
    {
        CastleSize.Large => _balance.PopulationMaxLarge,
        CastleSize.Medium => _balance.PopulationMaxMedium,
        _ => _balance.PopulationMaxSmall,
    };

    private int GoldBase(CastleSize castle) => castle switch
    {
        CastleSize.Large => _balance.GoldBaseLarge,
        CastleSize.Medium => _balance.GoldBaseMedium,
        _ => _balance.GoldBaseSmall,
    };

    private int ProvisionsBase(CastleSize castle) => castle switch
    {
        CastleSize.Large => _balance.ProvisionsBaseLarge,
        CastleSize.Medium => _balance.ProvisionsBaseMedium,
        _ => _balance.ProvisionsBaseSmall,
    };

    // ── 상세 탭 ①: 주둔 장수 표(태수 ◆·금색, 행 클릭 = 장수 상세) ──
    private void BuildStationedTab(VBoxContainer box, CityId city, List<General> stationed)
    {
        var c = _state.Cities.First(x => x.Id == city);
        box.AddChild(MakeLabel("주둔 장수 (행 클릭 = 상세)", 14, GoldBright));
        if (stationed.Count == 0) { box.AddChild(MakeLabel("(없음)", 12, Parchment)); return; }

        var gt = new Tree
        {
            Columns = 5,
            ColumnTitlesVisible = true,
            HideRoot = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0, Mathf.Min(44 + stationed.Count * 29, 320)),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        gt.AddThemeFontOverride("font", _font);
        gt.AddThemeFontSizeOverride("font_size", 14);
        gt.AddThemeFontOverride("title_button_font", _font);
        gt.AddThemeFontSizeOverride("title_button_font_size", 13);
        gt.SetColumnTitle(0, "이름");
        gt.SetColumnExpand(0, true);
        gt.SetColumnExpandRatio(0, 3);
        foreach (var (col, t) in new[] { (1, "무"), (2, "지"), (3, "정") })
        {
            gt.SetColumnTitle(col, t);
            gt.SetColumnExpand(col, false);
            gt.SetColumnCustomMinimumWidth(col, 46);
        }

        gt.SetColumnTitle(4, "상태");
        gt.SetColumnExpand(4, false);
        gt.SetColumnCustomMinimumWidth(4, 96);
        var groot = gt.CreateItem();
        foreach (var gen in stationed)
        {
            var isGov = c.Governor == gen.Id;
            var isStra = c.Strategist == gen.Id;
            var role = isGov && isStra ? "태수·군사" : isGov ? "태수" : isStra ? "군사" : null;
            var it = gt.CreateItem(groot);
            it.SetText(0, (role is not null ? "◆ " : "") + gen.Name);
            it.SetText(1, gen.Might.ToString());
            it.SetText(2, gen.Intellect.ToString());
            it.SetText(3, gen.Politics.ToString());
            it.SetText(4, role ?? GeneralStatus(gen.Id));
            if (role is not null) { it.SetCustomColor(0, GoldBright); it.SetCustomColor(4, GoldBright); }
            it.SetMetadata(0, gen.Id.Value);
            for (var col = 1; col <= 4; col++) { it.SetTextAlignment(col, HorizontalAlignment.Center); }
        }

        gt.ItemSelected += () =>
        {
            var it = gt.GetSelected();
            if (it is null) { return; }
            OpenGeneralDetail(new GeneralId(it.GetMetadata(0).AsInt32()), city);
        };
        box.AddChild(gt);
    }

    // ── 상세 탭 ②: 진행 중 명령(시작 전만 취소) ──
    private void BuildCommandsTab(VBoxContainer box, CityId city, List<CityCommand> cmds)
    {
        box.AddChild(MakeLabel("진행 중 명령 (시작 전만 취소 가능 · 환불 없음)", 14, GoldBright));
        if (cmds.Count == 0) { box.AddChild(MakeLabel("(없음)", 12, Parchment)); return; }
        foreach (var pending in cmds)
        {
            var cmd = pending;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            // 재생 중엔 _state가 진행 전 스냅숏이라 StartDay 가드가 통과함 — _advancing도 차단
            var started = _advancing || _state.Day != cmd.StartDay;
            var lbl = MakeLabel("· " + CmdText(cmd) + (started ? "  (진행중)" : ""), 12, Parchment);
            lbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            lbl.CustomMinimumSize = new Vector2(1, 0);
            row.AddChild(lbl);
            if (started) { box.AddChild(row); continue; }
            var cancel = MakeButton("취소");
            cancel.CustomMinimumSize = new Vector2(56, 24);
            cancel.Pressed += () => ShowConfirm("명령 취소",
                $"{CmdText(cmd)}\n\n취소하면 예약된 자원·비용은 돌려받지 못합니다. 장수는 즉시 해제됩니다.",
                () =>
                {
                    _state = CommandService.Cancel(_state, cmd);
                    Dbg($"UI cancel-cmd city={city.Value} {KindName(cmd.Kind)} gen={cmd.Main.Value}");
                    _log.Text = $"명령 취소: {KindName(cmd.Kind)}";
                    SelectCity(city);
                    OpenCityDetail(city);
                });
            row.AddChild(cancel);
            box.AddChild(row);
        }
    }

    // ── 상세 탭 ③: 예약(출전 — 진행 시 수행, 그 전까진 취소) ──
    private void BuildDeployTab(VBoxContainer box, CityId city, List<int> deploys, List<int> supplyDeploys)
    {
        box.AddChild(MakeLabel("출전 예약 (진행 시 편성 — 취소 시 소모 없음)", 14, GoldBright));
        if (deploys.Count == 0 && supplyDeploys.Count == 0) { box.AddChild(MakeLabel("(없음)", 12, Parchment)); }
        foreach (var di in deploys)
        {
            var idx = di;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var lbl = MakeLabel("· " + _pendingDeploys[idx].Label, 12, Parchment);
            lbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            lbl.CustomMinimumSize = new Vector2(1, 0);
            row.AddChild(lbl);
            if (_advancing) { box.AddChild(row); continue; }
            var cancel = MakeButton("취소");
            cancel.CustomMinimumSize = new Vector2(56, 24);
            cancel.Pressed += () =>
            {
                Dbg($"UI cancel-deploy pending[{idx}] '{_pendingDeploys[idx].Label}'");
                _pendingDeploys.RemoveAt(idx);
                SelectCity(city);
                OpenCityDetail(city);
            };
            row.AddChild(cancel);
            box.AddChild(row);
        }

        foreach (var si in supplyDeploys)
        {
            var idx = si;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var lbl = MakeLabel("· [보급] " + _pendingSupplyDeploys[idx].Label, 12, Parchment);
            lbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            lbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            lbl.CustomMinimumSize = new Vector2(1, 0);
            row.AddChild(lbl);
            if (_advancing) { box.AddChild(row); continue; }
            var cancel = MakeButton("취소");
            cancel.CustomMinimumSize = new Vector2(56, 24);
            cancel.Pressed += () =>
            {
                Dbg($"UI cancel-supply pending[{idx}] '{_pendingSupplyDeploys[idx].Label}'");
                _pendingSupplyDeploys.RemoveAt(idx);
                SelectCity(city);
                OpenCityDetail(city);
            };
            row.AddChild(cancel);
            box.AddChild(row);
        }

    }

    // 대기 병력 카드 — 병종 엠블럼 + 이름(신병) + 병력·훈련도.
    private PanelContainer GarrisonCard(GarrisonForce g)
    {
        var tmpl = _troops.FirstOrDefault(t => t.Code == g.TroopCode);
        var card = new PanelContainer { CustomMinimumSize = new Vector2(96, 84) };
        card.AddThemeStyleboxOverride("panel", CardBox(false));
        var v = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        v.AddThemeConstantOverride("separation", 2);
        card.AddChild(v);
        v.AddChild(new TextureRect
        {
            Texture = tmpl is not null ? ClassEmblem(tmpl.Class) : Icon(Sym.Sword),
            CustomMinimumSize = new Vector2(34, 34),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        });
        var name = MakeLabel(TroopName(g.TroopCode) + (g.Trainee ? " (신병)" : ""), 11, GoldBright);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(name);
        var stat = MakeLabel($"{g.Troops}명 · 훈{g.TrainingLevel}", 11, Parchment);
        stat.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(stat);
        return card;
    }

    // ── 시장 모달: 이번 달 시세로 성 금고에서 자원 매입(즉시). 군량은 비상 보급용. 교역 태수면 할인 ──
    private void OpenMarketModal(CityId cityId)
    {
        if (_advancing) { return; }
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.44f, 440f, 640f);
        var mh = Mathf.Clamp(vp.Y * 0.85f, 360f, 720f);
        var box = DeployScaffold(mw, out var scroll, out var panel);
        var city = _state.Cities.First(c => c.Id == cityId);

        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var pct = _state.MarketPricePercent;
        var season = pct <= 85 ? "싼 철" : pct >= 120 ? "비싼 철" : "보통";
        var title = MakeLabel($"《 {city.Name} 》 시장 · 시세 {pct}% ({season})", 17, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        titleRow.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(40, 30);
        close.Pressed += CloseModal;
        titleRow.AddChild(close);
        box.AddChild(GoldRule());

        // 상단 배너 — 향후 시장 일러스트가 들어갈 이미지 슬롯 + 시세·보유 금 정보. 파일이 있으면
        // 그림을 싣고, 없으면 자리를 잡아두는 플레이스홀더를 보여준다(그림 준비 시 그대로 교체).
        var banner = new HBoxContainer();
        banner.AddThemeConstantOverride("separation", 12);
        box.AddChild(banner);

        var imgSlot = new PanelContainer();
        imgSlot.AddThemeStyleboxOverride("panel", Frame(new Color(Ink, 0.55f), Gold, 1, 6, 6));
        imgSlot.CustomMinimumSize = new Vector2(160, 100);
        var marketTex = LoadOptionalTexture("res://assets/ui/market.png");
        if (marketTex is not null)
        {
            imgSlot.AddChild(new TextureRect
            {
                Texture = marketTex,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            });
        }
        else
        {
            var hint = MakeLabel("시장 그림\n(준비 중)", 11, new Color(Parchment, 0.55f));
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            hint.VerticalAlignment = VerticalAlignment.Center;
            imgSlot.AddChild(hint);
        }

        banner.AddChild(imgSlot);

        var info = new VBoxContainer();
        info.AddThemeConstantOverride("separation", 4);
        info.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        info.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        banner.AddChild(info);
        info.AddChild(MakeLabel($"시세 {pct}% ({season})", 14, Gold));
        info.AddChild(MakeLabel($"보유 금 {city.Gold}", 13, GoldBright));
        info.AddChild(MakeLabel("수량을 슬라이더나 숫자로 정해 매입합니다.\n자원은 병력 생산·비상 보급에 씁니다.", 11, Parchment));

        // ImgPath: 향후 자원별 사진(있으면 로드). SymFallback: 없을 때 쓰는 절차적 아이콘(없으면 플레이스홀더).
        (MarketResource Res, string Name, string Note, int Stock, string ImgPath, Sym? SymFallback)[] items =
        {
            (MarketResource.Ore, "광석", "모든 병력 생산", city.Ore, "res://assets/ui/market_ore.png", Sym.Ore),
            (MarketResource.Horses, "말", "기병 생산", city.Horses, "res://assets/ui/market_horses.png", null),
            (MarketResource.Elephants, "코끼리", "상병 생산", city.Elephants, "res://assets/ui/market_elephants.png", null),
            (MarketResource.Grain, "군량 (비상 보급)", "약탈·보급 차단 대비", city.Provisions, "res://assets/ui/market_grain.png", Sym.Grain),
        };

        foreach (var it in items)
        {
            box.AddChild(GoldRule());
            var res = it.Res;
            var name = it.Name;
            var per100 = _commander.MarketUnitPricePer100(_state, city, res);
            const int step = 1; // 1단위로 자유롭게 조정(슬라이더/숫자 입력)
            // 예산 안에서 살 수 있는 최대 수량. 금이 부족해도 최소 1까지는 슬라이더를 연다.
            var affordable = per100 > 0 ? (int)((long)city.Gold * 100 / per100) : 0;
            var maxUnits = System.Math.Max(step, affordable);

            // 자원 행: [사진 슬롯] + [설명 + 컨트롤]. 사진은 파일 있으면 로드, 없으면 아이콘/플레이스홀더.
            var resRow = new HBoxContainer();
            resRow.AddThemeConstantOverride("separation", 10);
            box.AddChild(resRow);

            var resImg = new PanelContainer();
            resImg.AddThemeStyleboxOverride("panel", Frame(new Color(Ink, 0.55f), Gold, 1, 4, 4));
            resImg.CustomMinimumSize = new Vector2(60, 60);
            resImg.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            var resTex = LoadOptionalTexture(it.ImgPath) ?? (it.SymFallback is { } sym ? Icon(sym) : null);
            if (resTex is not null)
            {
                resImg.AddChild(new TextureRect
                {
                    Texture = resTex,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                });
            }
            else
            {
                var ph = MakeLabel(name, 10, new Color(Parchment, 0.55f));
                ph.HorizontalAlignment = HorizontalAlignment.Center;
                ph.VerticalAlignment = VerticalAlignment.Center;
                resImg.AddChild(ph);
            }

            resRow.AddChild(resImg);

            var rightV = new VBoxContainer();
            rightV.AddThemeConstantOverride("separation", 4);
            rightV.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            resRow.AddChild(rightV);

            rightV.AddChild(MakeLabel($"{name}  ·  보유 {it.Stock}  ·  단가 {per100 / 100.0:0.##}금/단위  —  {it.Note}",
                13, res == MarketResource.Grain ? GoldBright : Parchment));

            var ctrl = new HBoxContainer();
            ctrl.AddThemeConstantOverride("separation", 8);
            rightV.AddChild(ctrl);

            var slider = new HSlider { MinValue = 0, MaxValue = maxUnits, Step = step };
            slider.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            slider.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            slider.CustomMinimumSize = new Vector2(0, 24);
            slider.Value = System.Math.Min(1000, affordable); // 기본 제안값(이후 1단위로 자유 조정)
            ctrl.AddChild(slider);

            var spin = new SpinBox { MinValue = 0, MaxValue = maxUnits, Step = step, Value = slider.Value };
            spin.CustomMinimumSize = new Vector2(104, 0);
            ctrl.AddChild(spin);

            var costLbl = MakeLabel("", 13, GoldBright);
            costLbl.CustomMinimumSize = new Vector2(88, 0);
            costLbl.HorizontalAlignment = HorizontalAlignment.Right;
            ctrl.AddChild(costLbl);

            var buy = MakeButton("매입");
            buy.CustomMinimumSize = new Vector2(64, 30);
            ctrl.AddChild(buy);

            void Sync(int units)
            {
                var cst = (int)(((long)per100 * units + 99) / 100);
                costLbl.Text = $"{cst}금";
                buy.Disabled = units <= 0 || cst > city.Gold;
            }

            slider.ValueChanged += val =>
            {
                if ((int)spin.Value != (int)val) { spin.Value = val; }
                Sync((int)val);
            };
            spin.ValueChanged += val =>
            {
                if ((int)slider.Value != (int)val) { slider.Value = val; }
                Sync((int)val);
            };
            Sync((int)slider.Value);

            buy.Pressed += () =>
            {
                var units = (int)slider.Value;
                if (units <= 0) { return; }
                var cost = (int)(((long)per100 * units + 99) / 100);
                ShowConfirm("시장 매입",
                    $"{city.Name} 시장에서 {name} {units} 매입\n비용 {cost}금 (시세 {pct}%)",
                    () =>
                    {
                        var r = _commander.BuyFromMarket(_state, cityId, res, units);
                        Dbg($"UI market-buy city={cityId.Value} {res} x{units} ok={r.Ok} err={r.Error ?? "-"}");
                        if (r.Ok) { _state = r.State; }
                        _log.Text = r.Ok ? $"시장: {name} {units} 매입 (−{cost}금)" : $"실패: {r.Error}";
                        if (r.Ok) { Report($"[내정] {city.Name} 시장에서 {name} {units}을(를) {cost}금에 사들였습니다.", Parchment); }
                        else { ShowNotice("시장 매입 실패", r.Error ?? "조건에 맞지 않아 실행할 수 없습니다."); }
                        SelectCity(cityId);
                        OpenMarketModal(cityId);
                    });
            };
        }

        var contentH = box.GetCombinedMinimumSize().Y;
        scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentH, mh));
        CenterAndDrag(panel, titleRow, mw, mh, box);
    }

    // ── 등용 모달: 대상(정찰된 적 성 장수·출전중 적 장수) 선택 → 수행 장수 선택 → 군사 예측·확인 ──
    private void OpenEnlistModal(CityId cityId)
    {
        if (_advancing) { return; }
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.46f, 460f, 680f);
        var mh = Mathf.Clamp(vp.Y * 0.85f, 380f, 760f);
        var box = DeployScaffold(mw, out var scroll, out var panel);
        var city = _state.Cities.First(c => c.Id == cityId);
        var strat = city.Strategist is { } sid ? _state.Generals.FirstOrDefault(g => g.Id == sid) : null;

        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var title = MakeLabel($"《 {city.Name} 》 등용 · 군사 {strat?.Name ?? "없음"}", 17, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        titleRow.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(40, 30);
        close.Pressed += CloseModal;
        titleRow.AddChild(close);
        box.AddChild(GoldRule());

        // 후보 대상 수집: 정찰된 적 성 장수 · 출전중 적 장수.
        var targets = new List<(GeneralId Id, string Kind)>();
        foreach (var u in _state.Armies.Where(u => u.Field.Owner != Player && CanSeeUnit(u)))
        {
            if (u.VanguardId is { } v) { targets.Add((v, "출전중")); }
            if (u.AdjutantId is { } a) { targets.Add((a, "출전중")); }
        }

        foreach (var post in _state.Assignments.Where(p => p.Faction != Player && p.Location is not null))
        {
            if (_state.IsScouted(Player, post.Location!.Value)) { targets.Add((post.General, "적 성")); }
        }

        if (targets.Count == 0)
        {
            box.AddChild(MakeLabel("등용할 대상이 없습니다. (적 성 정찰·적 부대 접촉 필요)", 13, Parchment));
            var h0 = box.GetCombinedMinimumSize().Y;
            scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(h0, mh));
            CenterAndDrag(panel, titleRow, mw, mh, box);
            return;
        }

        box.AddChild(MakeLabel("대상 선택 → 수행 장수 → 확인", 13, GoldBright));
        foreach (var (tid, kind) in targets)
        {
            var targetId = tid;
            var gen = _state.Generals.FirstOrDefault(g => g.Id == targetId);
            if (gen is null) { continue; }
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var lbl = MakeLabel($"· {gen.Name}  [{kind}]  무{gen.Might} 지{gen.Intellect} 정{gen.Politics}", 13, Parchment);
            lbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(lbl);
            var pick = MakeButton("등용");
            pick.CustomMinimumSize = new Vector2(64, 26);
            pick.Pressed += () => PickEnlistRecruiter(cityId, targetId);
            row.AddChild(pick);
            box.AddChild(row);
        }

        var contentH = box.GetCombinedMinimumSize().Y;
        scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentH, mh));
        CenterAndDrag(panel, titleRow, mw, mh, box);
    }

    // 등용 수행 장수 선택(그 도시 주둔 자유 장수, 정치 기준) → 군사 예측·확인 → 발행.
    private void PickEnlistRecruiter(CityId cityId, GeneralId targetId)
    {
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.4f, 420f, 560f);
        var mh = Mathf.Clamp(vp.Y * 0.8f, 340f, 680f);
        var box = DeployScaffold(mw, out var scroll, out var panel);
        var city = _state.Cities.First(c => c.Id == cityId);
        var target = _state.Generals.First(g => g.Id == targetId);
        var strat = city.Strategist is { } sid ? _state.Generals.FirstOrDefault(g => g.Id == sid) : null;

        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var back = MakeButton("◀");
        back.CustomMinimumSize = new Vector2(40, 30);
        back.Pressed += () => OpenEnlistModal(cityId);
        titleRow.AddChild(back);
        var title = MakeLabel($"등용 대상: {target.Name}", 16, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        titleRow.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(40, 30);
        close.Pressed += CloseModal;
        titleRow.AddChild(close);
        box.AddChild(GoldRule());
        box.AddChild(MakeLabel("수행 장수 (정치 높을수록 유리 · 행 클릭)", 13, GoldBright));

        var free = _state.GeneralsAt(cityId).Where(g => !OfficerUnavailable(g))
            .Select(id => _state.Generals.First(g => g.Id == id)).OrderByDescending(g => g.Politics).ToList();
        if (free.Count == 0) { box.AddChild(MakeLabel("(가능한 수행 장수 없음)", 12, Parchment)); }
        foreach (var g in free)
        {
            var recruiter = g;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var lbl = MakeLabel($"· {recruiter.Name}  정{recruiter.Politics}", 13, Parchment);
            lbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(lbl);
            var go = MakeButton("선택");
            go.CustomMinimumSize = new Vector2(60, 26);
            go.Pressed += () => ConfirmEnlist(cityId, recruiter.Id, targetId);
            row.AddChild(go);
            box.AddChild(row);
        }

        var contentH = box.GetCombinedMinimumSize().Y;
        scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentH, mh));
        CenterAndDrag(panel, titleRow, mw, mh, box);
    }

    // 등용 확인 창 — 군사 초상 + 예측 설명 + 확인/취소. 군사가 있으면 지력% 신뢰도로 성공/실패를 아뢴다.
    private void ConfirmEnlist(CityId cityId, GeneralId recruiterId, GeneralId targetId)
    {
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.38f, 380f, 520f);
        var mh = Mathf.Clamp(vp.Y * 0.8f, 320f, 640f);
        var box = DeployScaffold(mw, out var scroll, out var panel);
        var city = _state.Cities.First(c => c.Id == cityId);
        var recruiter = _state.Generals.First(g => g.Id == recruiterId);
        var target = _state.Generals.First(g => g.Id == targetId);
        var strat = city.Strategist is { } sid ? _state.Generals.FirstOrDefault(g => g.Id == sid) : null;

        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var back = MakeButton("◀");
        back.CustomMinimumSize = new Vector2(40, 30);
        back.Pressed += () => PickEnlistRecruiter(cityId, targetId);
        titleRow.AddChild(back);
        var title = MakeLabel("등용 확인", 17, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        titleRow.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(40, 30);
        close.Pressed += CloseModal;
        titleRow.AddChild(close);
        box.AddChild(GoldRule());

        // 군사 초상 + 아룀. 군사 없으면 예측 불가 안내.
        var advisorRow = new HBoxContainer();
        advisorRow.AddThemeConstantOverride("separation", 10);
        box.AddChild(advisorRow);
        var facePanel = new PanelContainer { CustomMinimumSize = new Vector2(118, 146) };
        facePanel.AddThemeStyleboxOverride("panel", Frame(new Color(0.075f, 0.06f, 0.05f), Gold, 2, 8, 4));
        advisorRow.AddChild(facePanel);
        if (strat is not null && PortraitFor(strat.Id) is { } tex)
        {
            facePanel.AddChild(new TextureRect
            {
                Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(108, 136),
            });
        }
        else
        {
            var mark = MakeLabel("◈", 60, new Color(Gold, 0.45f));
            mark.HorizontalAlignment = HorizontalAlignment.Center;
            mark.VerticalAlignment = VerticalAlignment.Center;
            facePanel.AddChild(mark);
        }

        string saying;
        if (strat is null)
        {
            saying = "군사가 없어 성패를 가늠할 수 없습니다.\n(군사를 임명하면 예측을 들을 수 있습니다.)";
        }
        else
        {
            var odds = EnlistOdds.SuccessPercent(recruiter.Politics);
            var band = odds >= 40 ? "성공이 유력합니다" : odds >= 15 ? "반반으로 봅니다" : "어려워 보입니다";
            saying = $"군사 {strat.Name}이(가) 아룁니다:\n\"{target.Name} 등용은 {band}.\"\n(예측 신뢰도 {strat.Intellect}%)";
        }

        var sayLabel = MakeLabel(saying, 13, GoldBright);
        sayLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        sayLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        sayLabel.CustomMinimumSize = new Vector2(mw - 110, 0);
        advisorRow.AddChild(sayLabel);

        box.AddChild(GoldRule());
        box.AddChild(MakeLabel($"수행: {recruiter.Name} (정치 {recruiter.Politics})  →  대상: {target.Name}", 13, Parchment));

        var btnRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        btnRow.AddThemeConstantOverride("separation", 12);
        box.AddChild(btnRow);
        var ok = MakeButton("등용 시행", accent: true);
        ok.CustomMinimumSize = new Vector2(130, 36);
        ok.Pressed += () =>
        {
            var req = new CommandRequest(cityId, CommandKind.Enlist, recruiterId, TargetGeneral: targetId);
            var r = _commander.Issue(_state, req);
            Dbg($"UI enlist city={cityId.Value} recruiter={recruiterId.Value} target={targetId.Value} ok={r.Ok} err={r.Error ?? "-"}");
            if (r.Ok) { _state = r.State; }
            _log.Text = r.Ok ? $"등용 시도: {target.Name} ({recruiter.Name})" : $"실패: {r.Error}";
            if (r.Ok) { Report($"[인사] {recruiter.Name} 장수가 {target.Name} 등용에 나섰습니다.", GoldBright); }
            else { ShowNotice("등용 실패", r.Error ?? "조건에 맞지 않아 실행할 수 없습니다."); }
            CloseModal();
            SelectCity(cityId);
            Redraw(_log.Text);
        };
        btnRow.AddChild(ok);
        var cancel = MakeButton("취소");
        cancel.CustomMinimumSize = new Vector2(90, 36);
        cancel.Pressed += () => PickEnlistRecruiter(cityId, targetId);
        btnRow.AddChild(cancel);

        var contentH = box.GetCombinedMinimumSize().Y;
        scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentH, mh));
        CenterAndDrag(panel, titleRow, mw, mh, box);
    }

    // ── 시스템 팔레트: 좌상단 트레이(☰)에서 열림 — 전체 장수·도시·보물 목록(+저장/불러오기) ──
    private void OpenSystemPalette()
    {
        if (_advancing) { return; }
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.3f, 300f, 420f);
        var mh = Mathf.Clamp(vp.Y * 0.6f, 260f, 520f);
        var box = DeployScaffold(mw, out var scroll, out var panel);

        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var title = MakeLabel("☰  시스템", 18, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        titleRow.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(40, 30);
        close.Pressed += CloseModal;
        titleRow.AddChild(close);
        box.AddChild(GoldRule());

        void Item(string label, System.Action open)
        {
            var b = MakeButton(label, accent: true);
            b.AddThemeFontSizeOverride("font_size", 14);
            b.CustomMinimumSize = new Vector2(0, 40);
            b.Pressed += open;
            box.AddChild(b);
        }

        Item("전체 장수 목록", OpenGeneralRoster);
        Item("전체 도시 목록", OpenCityRoster);
        Item("보물 목록", OpenTreasureList);
        box.AddChild(GoldRule());
        Item("게임 저장", () => ShowConfirm("게임 저장", "현재 상태를 저장합니다(같은 슬롯 덮어쓰기).", SaveGame));
        Item("게임 불러오기", () =>
        {
            if (!System.IO.File.Exists(SavePath))
            {
                _log.Text = "세이브가 없습니다.";
                CloseModal();
                return;
            }

            ShowConfirm("게임 불러오기", "현재 진행을 버리고 저장된 게임을 불러옵니다.", LoadGame);
        });

        var contentH = box.GetCombinedMinimumSize().Y;
        scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentH, mh));
        CenterAndDrag(panel, titleRow, mw, mh, box);
    }

    // 세이브 슬롯 경로(user:// — Godot 사용자 데이터 폴더의 실제 경로).
    private static string SavePath => ProjectSettings.GlobalizePath("user://sanguo-save.json");

    private void SaveGame()
    {
        try
        {
            SaveService.Save(_state, SavePath);
            _log.Text = $"저장했습니다. ({_state.Year}년 {_state.Month}월)";
        }
        catch (System.Exception e)
        {
            _log.Text = "저장 실패: " + e.Message;
        }

        CloseModal();
        Redraw(_log.Text);
    }

    private void LoadGame()
    {
        GameState loaded;
        try { loaded = SaveService.Load(SavePath); }
        catch (System.Exception e) { _log.Text = "불러오기 실패: " + e.Message; CloseModal(); return; }

        _state = loaded;
        _pendingDeploys.Clear();
        _pendingSupplyDeploys.Clear();
        _selected = null;
        _selectedUnitId = -1;
        _week = System.Math.Max(0, (_state.Day - 1) / 7);

        // 야전 토큰·라벨을 전부 지우고 로드 상태에 맞춰 Redraw가 다시 만든다(도시 색·주둔은 Redraw가 동기화).
        foreach (var t in _armyTokens.Values) { t.QueueFree(); }
        foreach (var l in _armyLabels.Values) { l.QueueFree(); }
        _armyTokens.Clear();
        _armyLabels.Clear();

        CloseModal();
        HidePanels();
        Redraw($"게임을 불러왔습니다. ({_state.Year}년 {_state.Month}월)");
    }

    // 시스템 모달 공통 헤더(◀ 시스템으로 복귀 · ✕ 닫기).
    private VBoxContainer SystemView(string titleText, float mw, out ScrollContainer scroll, out PanelContainer panel, out HBoxContainer titleRow)
    {
        var box = DeployScaffold(mw, out scroll, out panel);
        titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var back = MakeButton("◀");
        back.CustomMinimumSize = new Vector2(40, 30);
        back.Pressed += OpenSystemPalette;
        titleRow.AddChild(back);
        var title = MakeLabel(titleText, 17, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        titleRow.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(40, 30);
        close.Pressed += CloseModal;
        titleRow.AddChild(close);
        box.AddChild(GoldRule());
        return box;
    }

    // 전체 장수 목록 — 소속·위치·능력. 행 클릭 = 장수 상세(포로/재야/야전 포함).
    private void OpenGeneralRoster()
    {
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        _state = new HeroUnlockService().Evaluate(_state);
        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.86f, 820f, 1280f);
        var mh = Mathf.Clamp(vp.Y * 0.85f, 380f, 760f);
        var box = SystemView("전체 장수 목록", mw, out var scroll, out var panel, out var titleRow);

        string FactionName(FactionId f) => _state.Factions.FirstOrDefault(x => x.Id == f)?.Name ?? "?";
        string Where(General g)
        {
            if (_state.PrisonerOf(g.Id) is { } p) { return $"{FactionName(p.Holder)} 포로"; }
            if (_state.PostingOf(g.Id) is { } post)
            {
                var loc = post.Location is { } c ? _state.Cities.FirstOrDefault(x => x.Id == c)?.Name ?? "성" : "야전";
                return $"{FactionName(post.Faction)} · {loc}";
            }

            return "재야";
        }

        string HeroType(GeneralId id)
        {
            var state = _state.HeroStates.FirstOrDefault(s => s.General == id);
            var hero = _state.HeroUnlocks.FirstOrDefault(h => h.General == id);
            if (state is null || hero is null) { return "-"; }
            return HeroTypeName(hero.Type, state.Status);
        }

        string HeroStatus(GeneralId id)
        {
            var state = _state.HeroStates.FirstOrDefault(s => s.General == id);
            if (state is null) { return "-"; }
            return state.Status switch
            {
                HeroUnlockStatus.Locked => "잠김",
                HeroUnlockStatus.Unlocked => "해금",
                HeroUnlockStatus.Recruited => "소속",
                HeroUnlockStatus.Wanderer => "유랑",
                HeroUnlockStatus.Excluded => "제외",
                _ => state.Status.ToString(),
            };
        }

        var tree = new Tree
        {
            Columns = 7, ColumnTitlesVisible = true, HideRoot = true, SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0, Mathf.Min(mh - 60, 44 + _state.Generals.Count * 28)),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        tree.AddThemeFontOverride("font", _font);
        tree.AddThemeFontSizeOverride("font_size", 14);
        tree.AddThemeFontOverride("title_button_font", _font);
        tree.AddThemeFontSizeOverride("title_button_font_size", 13);
        tree.SetColumnTitle(0, "이름"); tree.SetColumnExpand(0, true); tree.SetColumnExpandRatio(0, 2);
        tree.SetColumnCustomMinimumWidth(0, 110);
        foreach (var (col, t) in new[] { (1, "무"), (2, "지"), (3, "정") })
        {
            tree.SetColumnTitle(col, t); tree.SetColumnExpand(col, false); tree.SetColumnCustomMinimumWidth(col, 42);
        }

        tree.SetColumnTitle(4, "소속·위치"); tree.SetColumnExpand(4, true); tree.SetColumnExpandRatio(4, 3);
        tree.SetColumnCustomMinimumWidth(4, 150);
        tree.SetColumnTitle(5, "위인 유형"); tree.SetColumnExpand(5, true); tree.SetColumnExpandRatio(5, 2);
        tree.SetColumnCustomMinimumWidth(5, 150);
        tree.SetColumnTitle(6, "상태"); tree.SetColumnExpand(6, false); tree.SetColumnCustomMinimumWidth(6, 74);
        var root = tree.CreateItem();
        foreach (var g in _state.Generals.OrderBy(g => g.Id.Value))
        {
            var it = tree.CreateItem(root);
            it.SetText(0, g.Name);
            it.SetText(1, g.Might.ToString());
            it.SetText(2, g.Intellect.ToString());
            it.SetText(3, g.Politics.ToString());
            it.SetText(4, Where(g));
            it.SetText(5, HeroType(g.Id));
            it.SetText(6, HeroStatus(g.Id));
            it.SetMetadata(0, g.Id.Value);
            for (var col = 1; col <= 3; col++) { it.SetTextAlignment(col, HorizontalAlignment.Center); }
            it.SetTextAlignment(6, HorizontalAlignment.Center);
        }

        tree.ItemSelected += () =>
        {
            var it = tree.GetSelected();
            if (it is not null) { OpenGeneralCard(new GeneralId(it.GetMetadata(0).AsInt32())); }
        };
        box.AddChild(tree);
        var contentH = box.GetCombinedMinimumSize().Y;
        scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentH, mh));
        CenterAndDrag(panel, titleRow, mw, mh, box);
    }

    // 장수 상세를 시스템 목록에서 열 때 — 성이 없는(재야·포로·야전) 장수도 안전하게(◀는 상세 카드 자체 닫기).
    private void OpenGeneralCard(GeneralId gid)
    {
        var loc = _state.PostingOf(gid)?.Location ?? _state.Cities.FirstOrDefault(c => c.Owner == Player)?.Id;
        OpenGeneralDetail(gid, loc ?? new CityId(0), OpenGeneralRoster);
    }

    // 전체 도시 목록 — 이름·세력·(정찰/소유 시)수치. 행 클릭 = 읽기 전용 상세(미정찰이면 정보 가림).
    private void OpenCityRoster()
    {
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.44f, 420f, 640f);
        var mh = Mathf.Clamp(vp.Y * 0.85f, 360f, 720f);
        var box = SystemView("전체 도시 목록", mw, out var scroll, out var panel, out var titleRow);

        foreach (var c in _state.Cities.OrderBy(c => c.Id.Value))
        {
            var city = c;
            var known = CanInspectCity(c);
            var owner = _state.Factions.FirstOrDefault(f => f.Id == c.Owner)?.Name ?? "?";
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var lbl = MakeLabel($"· {c.Name}  [{owner}]  {(known ? "" : "— 시야 밖")}", 13,
                known ? Parchment : new Color(Parchment, 0.55f));
            lbl.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(lbl);
            var view = MakeButton("보기");
            view.CustomMinimumSize = new Vector2(60, 26);
            view.Pressed += () => OpenCityInfoReadonly(city.Id);
            row.AddChild(view);
            box.AddChild(row);
        }

        var contentH = box.GetCombinedMinimumSize().Y;
        scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentH, mh));
        CenterAndDrag(panel, titleRow, mw, mh, box);
    }

    // 읽기 전용 도시 정보 — 소유·정찰 시에만 수치 노출, 아니면 "정찰 필요"로 가린다.
    private void OpenCityInfoReadonly(CityId cityId)
    {
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.36f, 340f, 480f);
        var mh = Mathf.Clamp(vp.Y * 0.7f, 300f, 600f);
        var box = DeployScaffold(mw, out var scroll, out var panel);
        var c = _state.Cities.First(x => x.Id == cityId);
        var known = CanInspectCity(c);
        _intelPanel = known ? panel : null;
        _intelPanelCity = known ? cityId : null;

        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var back = MakeButton("◀");
        back.CustomMinimumSize = new Vector2(40, 30);
        back.Pressed += OpenCityRoster;
        titleRow.AddChild(back);
        var owner = _state.Factions.FirstOrDefault(f => f.Id == c.Owner)?.Name ?? "?";
        var title = MakeLabel($"《 {c.Name} 》 [{owner}]", 17, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        titleRow.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(40, 30);
        close.Pressed += CloseModal;
        titleRow.AddChild(close);
        box.AddChild(GoldRule());

        if (!known)
        {
            box.AddChild(MakeLabel("시야 밖 적 도시 — 정보를 볼 수 없습니다.\n아군 시야에 들어오거나 정찰에 성공하면 공개됩니다.", 13, Parchment));
        }
        else
        {
            if (c.Owner != Player && _state.IsScouted(Player, c.Id))
                box.AddChild(MakeLabel($"정찰 남은 일수: {ScoutDaysLeft(c.Id)}일", 13, GoldBright));
            var g4 = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            g4.AddThemeConstantOverride("h_separation", 12);
            g4.AddThemeConstantOverride("v_separation", 4);
            box.AddChild(g4);
            AddCell(g4, Sym.Coin, "금", $"{c.Gold}");
            AddCell(g4, Sym.Grain, "군량", $"{c.Provisions}");
            AddCell(g4, Sym.Shield, "치안", $"{c.Security}");
            AddCell(g4, Sym.Wall, "성벽", $"{c.Wall}");
            AddCell(g4, Sym.Ore, "광석", $"{c.Ore}");
            AddCell(g4, Sym.Shield, "부상병", WoundedForFaction(c.Owner));

            var gov = c.Governor is { } gid ? _state.Generals.FirstOrDefault(x => x.Id == gid)?.Name : null;
            var stra = c.Strategist is { } sid ? _state.Generals.FirstOrDefault(x => x.Id == sid)?.Name : null;
            box.AddChild(MakeLabel($"태수 {gov ?? "없음"} · 군사 {stra ?? "없음"}", 13, GoldBright));
            var stationed = _state.GeneralsAt(cityId).Select(id => _state.Generals.First(x => x.Id == id).Name).ToList();
            box.AddChild(MakeLabel($"주둔 장수: {(stationed.Count > 0 ? string.Join(", ", stationed) : "없음")}", 12, Parchment));
            var troops = _state.Garrisons.Where(g => g.City == cityId).Sum(g => g.Troops);
            box.AddChild(MakeLabel($"대기 병력 총 {troops}명", 12, Parchment));
        }

        var contentH = box.GetCombinedMinimumSize().Y;
        scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentH, mh));
        CenterAndDrag(panel, titleRow, mw, mh, box);
    }

    // 보물 목록 — 탐색(design-general-lifecycle §8) 미구현이라 안내만.
    private void OpenTreasureList()
    {
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.34f, 340f, 460f);
        var mh = Mathf.Clamp(vp.Y * 0.5f, 220f, 420f);
        var box = SystemView("보물 목록", mw, out var scroll, out var panel, out var titleRow);
        box.AddChild(MakeLabel("보물 시스템은 준비 중입니다.\n탐색으로 얻은 보물을 여기서 확인하고,\n능력 강화에 사용하게 됩니다.", 13, Parchment));
        var contentH = box.GetCombinedMinimumSize().Y;
        scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentH, mh));
        CenterAndDrag(panel, titleRow, mw, mh, box);
    }

    // 장수 현재 상태 한 줄: 내정 명령 잠금 > 출전 예약 > 대기.
    private string GeneralStatus(GeneralId id)
    {
        var locking = _state.Commands.FirstOrDefault(x => x.Locks(id));
        if (locking is not null)
        {
            return KindName(locking.Kind) + " 중";
        }

        return _pendingDeploys.Any(d => d.Req.Vanguard == id || d.Req.Adjutant == id) ? "출전 예약" : "대기";
    }

    private static string GradeText(AptitudeGrade g) => g == AptitudeGrade.APlus ? "A+" : g.ToString();

    // 초상 텍스처 — assets/portraits/{id}.png가 있으면 자동 표시한다.
    private static string? PortraitPathFor(GeneralId id)
    {
        var candidates = new[]
        {
            $"res://assets/portraits/{id.Value}.png",
            $"res://assets/portraits/general_{id.Value:D3}.png",
            $"res://assets/portraits/general_{id.Value}.png",
        };
        return candidates.FirstOrDefault(path => ResourceLoader.Exists(path) || Godot.FileAccess.FileExists(path));
    }

    private static Texture2D? PortraitFor(GeneralId id)
    {
        var path = PortraitPathFor(id);
        return path is not null ? GD.Load<Texture2D>(path) : null;
    }

    private Texture2D? CircularPortraitFor(GeneralId id)
    {
        var portrait = LoadPortraitMetadata(id);
        var path = portrait is not null
            ? "res://" + portrait.PortraitPath.Replace('\\', '/').Replace("SanguoSLG.Game/", "")
            : PortraitPathFor(id);
        if (path is null)
        {
            return null;
        }

        var globalPath = ProjectSettings.GlobalizePath(path);
        if (!File.Exists(globalPath))
        {
            return PortraitFor(id);
        }

        var image = Image.LoadFromFile(globalPath);
        if (image is null || image.IsEmpty())
        {
            return PortraitFor(id);
        }

        portrait ??= new GeneralPortraitRecord(id.Value, $"SanguoSLG.Game/assets/portraits/{id.Value}.png", 0.5, 0.35, 1);
        return ImageTexture.CreateFromImage(BuildCircularPortraitImage(image, portrait));
    }

    private GeneralPortraitRecord? LoadPortraitMetadata(GeneralId id)
    {
        try
        {
            var path = Path.Combine(_dataDirectory, "general-portraits.json");
            if (!File.Exists(path))
            {
                return null;
            }

            return GeneralEditorStore.LoadPortraits(File.ReadAllText(path)).FirstOrDefault(p => p.GeneralId == id.Value);
        }
        catch
        {
            return null;
        }
    }

    private static Image BuildCircularPortraitImage(Image source, GeneralPortraitRecord portrait)
    {
        const int size = 192;
        var side = Math.Max(1, Math.Min(source.GetWidth(), source.GetHeight()) / portrait.FaceZoom);
        var centerX = source.GetWidth() * portrait.FaceCenterX;
        var centerY = source.GetHeight() * portrait.FaceCenterY;
        var left = Math.Clamp(centerX - side / 2.0, 0, Math.Max(0, source.GetWidth() - side));
        var top = Math.Clamp(centerY - side / 2.0, 0, Math.Max(0, source.GetHeight() - side));
        var crop = source.GetRegion(new Rect2I((int)Math.Round(left), (int)Math.Round(top), (int)Math.Round(side), (int)Math.Round(side)));
        crop.Resize(size, size, Image.Interpolation.Lanczos);

        var radius = size / 2.0;
        var output = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5 - radius;
                var dy = y + 0.5 - radius;
                var color = crop.GetPixel(x, y);
                if (dx * dx + dy * dy > radius * radius)
                {
                    color.A = 0;
                }

                output.SetPixel(x, y, color);
            }
        }

        return output;
    }

    private void OpenGeneralDetail(GeneralId gid, CityId backCity, System.Action? backAction = null)
    {
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Min(960f, vp.X - 64f);
        var mh = Mathf.Min(vp.Y * 0.82f, mw * 0.78f);
        var portraitHeight = mh - 50f;
        var portraitWidth = mw * 0.54f;
        var detailWidth = mw - portraitWidth - 18f;
        var box = DeployScaffold(mw, out var scroll, out var panel);
        var rootBox = box;
        var g = _state.Generals.First(x => x.Id == gid);

        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var back = MakeButton("◀");
        back.CustomMinimumSize = new Vector2(40, 30);
        back.Pressed += () =>
        {
            if (backAction is not null) { backAction(); }
            else { OpenCityDetail(backCity); }
        };
        titleRow.AddChild(back);
        var title = MakeLabel($"《 {g.Name} 》", 19, GoldBright);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        titleRow.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(40, 30);
        close.Pressed += CloseModal;
        titleRow.AddChild(close);

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 18);
        rootBox.AddChild(body);
        var portrait = new PanelContainer
        {
            CustomMinimumSize = new Vector2(portraitWidth, portraitHeight),
        };
        portrait.AddThemeStyleboxOverride("panel", Frame(new Color(0.075f, 0.06f, 0.05f), Gold, 1, 8, 8));
        body.AddChild(portrait);
        if (PortraitFor(gid) is { } tex)
        {
            portrait.AddChild(new TextureRect
            {
                Texture = tex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            });
        }
        else
        {
            var ph = new VBoxContainer
            {
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            };
            ph.AddThemeConstantOverride("separation", 2);
            var mark = MakeLabel("◈", 44, new Color(Gold, 0.45f));
            mark.HorizontalAlignment = HorizontalAlignment.Center;
            ph.AddChild(mark);
            var note = MakeLabel("초상 준비 중", 11, new Color(Parchment, 0.6f));
            note.HorizontalAlignment = HorizontalAlignment.Center;
            ph.AddChild(note);
            portrait.AddChild(ph);
        }

        var detailsScroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(detailWidth, portraitHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        body.AddChild(detailsScroll);
        box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 8);
        detailsScroll.AddChild(box);

        var meta = new List<string> { $"상태 {GeneralStatus(gid)}" };
        if (g.Birth != 0) { meta.Add(g.Birth < 0 ? $"기원전 {-g.Birth}년생" : $"{g.Birth}년생"); }
        if (g.Region.Length > 0) { meta.Add($"출신 {g.Region}"); }
        var metaLbl = MakeLabel(string.Join(" · ", meta), 12, Parchment);
        metaLbl.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        metaLbl.HorizontalAlignment = HorizontalAlignment.Center;
        box.AddChild(metaLbl);

        box.AddChild(GoldRule());

        Label Cell(string text, int size, Color color)
        {
            var l = MakeLabel(text, size, color);
            l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            l.HorizontalAlignment = HorizontalAlignment.Center;
            return l;
        }

        var stat = new GridContainer { Columns = 3, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        stat.AddThemeConstantOverride("v_separation", 2);
        box.AddChild(stat);
        foreach (var name in new[] { "무력", "지력", "정치" }) { stat.AddChild(Cell(name, 12, GoldBright)); }
        foreach (var v in new[] { g.Might, g.Intellect, g.Politics }) { stat.AddChild(Cell(v.ToString(), 17, Parchment)); }

        box.AddChild(GoldRule());

        box.AddChild(MakeLabel("병종 적성", 13, GoldBright));
        var classes = new[]
        {
            TroopClass.Infantry, TroopClass.Archer, TroopClass.Cavalry,
            TroopClass.Elephant, TroopClass.Siege, TroopClass.Naval,
        };
        var apt = new GridContainer { Columns = 6, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        apt.AddThemeConstantOverride("v_separation", 1);
        box.AddChild(apt);
        foreach (var tc in classes) { apt.AddChild(Cell(ClassName(tc), 11, new Color(Parchment, 0.75f))); }
        foreach (var tc in classes)
        {
            var grade = g.AptitudeFor(tc);
            apt.AddChild(Cell(GradeText(grade), 14, grade >= AptitudeGrade.A ? GoldBright : Parchment));
        }

        box.AddChild(GoldRule());

        box.AddChild(MakeLabel("특기 · 선택하여 효과 확인", 13, GoldBright));
        var skills = new List<(string Tag, string Name, string Description, string? Icon)>();
        if (g.BattleActive is { Length: > 0 } ac)
        {
            var definition = _activeSkills.FirstOrDefault(a => a.Code == ac);
            skills.Add(("액티브", definition?.Name ?? ac, SkillDescriptions.Active(definition), ac));
        }

        foreach (var p in g.Passives)
        {
            var definition = _passiveSkills.FirstOrDefault(x => x.Code == p.Code);
            skills.Add(("패시브", $"{definition?.Name ?? p.Code} Lv{p.Tier}", SkillDescriptions.Passive(definition, p.Tier), $"passive/{p.Code}"));
        }

        foreach (var p in g.AdminPassives ?? [])
        {
            var definition = _adminSkills.FirstOrDefault(x => x.Code == p.Code);
            skills.Add(("내정 패시브", $"{definition?.Name ?? p.Code} Lv{p.Tier}", SkillDescriptions.Admin(definition, p.Tier), $"admin/{p.Code}"));
        }

        if (skills.Count == 0) { box.AddChild(MakeLabel("(없음)", 12, Parchment)); }
        else
        {
            var sg = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            sg.AddThemeConstantOverride("separation", 6);
            box.AddChild(sg);
            foreach (var (tag, name, description, iconCode) in skills)
            {
                var skillButton = MakeButton($"[{tag}]  {name}   ›");
                skillButton.Alignment = HorizontalAlignment.Left;
                skillButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                skillButton.CustomMinimumSize = new Vector2(0, 38);
                skillButton.ClipText = true;
                skillButton.TooltipText = $"{name} · 설명 보기";
                if (iconCode is not null) { ActiveSkillIcons.Apply(skillButton, iconCode); }
                skillButton.Pressed += () => ShowSkillDescription(name, tag, description, iconCode);
                sg.AddChild(skillButton);
            }
        }

        if (g.Desc.Length > 0)
        {
            box.AddChild(GoldRule());
            var desc = MakeLabel(g.Desc, 12, Parchment);
            desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            desc.CustomMinimumSize = new Vector2(Mathf.Max(0, detailWidth - 20f), 0);
            box.AddChild(desc);
        }

        var contentH = rootBox.GetCombinedMinimumSize().Y;
        scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(contentH, mh));
        CenterAndDrag(panel, titleRow, mw, mh, rootBox);
    }

    private void OpenProductionModal(CityId city, HexCoord? fixedTarget = null)
    {
        if (_advancing) { return; }
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        var cityData = _state.Cities.First(c => c.Id == city);
        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.54f, 520f, 760f);
        var mh = Mathf.Clamp(vp.Y * 0.75f, 390f, 650f);
        var box = DeployScaffold(mw, out var scroll, out var panel);

        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var title = MakeLabel($"◈  생산 작전   《 {cityData.Name} 》", 21, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(34, 32);
        close.Pressed += () => { CloseModal(); SelectCity(city); };
        titleRow.AddChild(close);
        box.AddChild(GoldRule());
        box.AddChild(MakeLabel("논·밭·마을에 장수와 500명 부대를 보내 8~14일간 채집합니다.\n명령이 시작되면 취소할 수 없고, 채집 중 피격되면 투입 병력은 모두 소실됩니다.", 13, Parchment));

        var targets = ProductionTargets(cityData).ToList();
        var generals = _state.GeneralsAt(city)
            .Where(g => !OfficerUnavailable(g) && !_state.IsGeneralInField(g))
            .Select(id => _state.Generals.First(g => g.Id == id))
            .OrderByDescending(g => g.Politics).ThenBy(g => g.Id.Value)
            .ToList();
        var garrisons = _state.Garrisons
            .Where(g => g.City == city && !g.Trainee && g.Troops >= ProductionOperation.FixedTroops)
            .Join(_troops.Where(t => t.Class != TroopClass.Naval), g => g.TroopCode, t => t.Code, (g, t) => (Garrison: g, Troop: t))
            .OrderBy(x => x.Troop.Name, System.StringComparer.Ordinal)
            .ToList();

        var firstTarget = targets.FirstOrDefault();
        var fixedOption = fixedTarget is { } ft ? targets.FirstOrDefault(t => t.Plot == ft) : default;
        var hasFixedTarget = fixedTarget is not null && !string.IsNullOrWhiteSpace(fixedOption.Code);
        HexCoord? selectedTarget = hasFixedTarget ? fixedOption.Plot
            : string.IsNullOrWhiteSpace(firstTarget.Code) ? null : firstTarget.Plot;
        string selectedFacility = hasFixedTarget ? fixedOption.Code
            : string.IsNullOrWhiteSpace(firstTarget.Code) ? string.Empty : firstTarget.Code;
        GeneralId? selectedGeneral = generals.FirstOrDefault()?.Id;
        string selectedTroop = garrisons.FirstOrDefault().Troop?.Code ?? "";
        var summary = MakeLabel("", 14, GoldBright);
        var generalButtons = new List<(Button Button, General General)>();
        var troopButtons = new List<(Button Button, GarrisonForce Garrison, TroopTemplate Troop)>();

        void StyleSelectionCard(Button button, bool selected)
        {
            button.ToggleMode = true;
            button.SetPressedNoSignal(selected);
            button.AddThemeColorOverride("font_color", selected ? Ink : Parchment);
            button.AddThemeColorOverride("font_hover_color", selected ? Ink : GoldBright);
            button.AddThemeColorOverride("font_pressed_color", Ink);
            button.AddThemeColorOverride("font_hover_pressed_color", Ink);
            button.AddThemeStyleboxOverride("normal", Frame(InkSoft, Gold, 1, 8, 10));
            button.AddThemeStyleboxOverride("pressed", Frame(Gold, GoldBright, 2, 8, 10));
            button.AddThemeStyleboxOverride("hover_pressed", Frame(GoldBright, GoldBright, 2, 8, 10));
            button.AddThemeStyleboxOverride("focus", Frame(Colors.Transparent, GoldBright, 2, 8, 10));
        }

        void RefreshSelectionButtons()
        {
            foreach (var (btn, general) in generalButtons)
            {
                var reward = string.IsNullOrWhiteSpace(selectedFacility)
                    ? (Gold: 0, Provisions: 0)
                    : ProductionRules.Reward(selectedFacility, general.Politics);
                var rewardText = reward.Gold > 0 ? $"금 {reward.Gold}" : reward.Provisions > 0 ? $"군량 {reward.Provisions}" : "보상 -";
                var selected = selectedGeneral == general.Id;
                btn.Text = $"{general.Name}{(selected ? " · 선택됨" : "")}\n현재 담당업무: {CurrentDuty(general.Id)}\n정치 {general.Politics} · {ProductionRules.GatherDays(general.Politics)}일 · {rewardText}";
                StyleSelectionCard(btn, selected);
            }

            foreach (var (btn, garrison, troop) in troopButtons)
            {
                var selected = selectedTroop == troop.Code;
                btn.Text = $"{troop.Name}{(selected ? " · 선택됨" : "")}\n대기 {garrison.Troops} · 이동 {troop.MovementPerDay}";
                StyleSelectionCard(btn, selected);
            }
        }

        void RefreshSummary()
        {
            var general = selectedGeneral is { } gid ? _state.Generals.FirstOrDefault(g => g.Id == gid) : null;
            var days = general is null ? 0 : ProductionRules.GatherDays(general.Politics);
            var reward = general is null || string.IsNullOrWhiteSpace(selectedFacility)
                ? (0, 0)
                : ProductionRules.Reward(selectedFacility, general.Politics);
            var rewardText = reward.Item1 > 0 ? $"금 +{reward.Item1}" : reward.Item2 > 0 ? $"군량 +{reward.Item2}" : "-";
            summary.Text = selectedTarget is null || general is null || string.IsNullOrWhiteSpace(selectedTroop)
                ? "대상 시설, 장수, 병종을 선택하세요."
                : $"예상: {FacilityName(selectedFacility)} · {general.Name} 정치 {general.Politics} · 채집 {days}일 · 보상 {rewardText} · 투입 500명";
        }

        box.AddChild(MakeLabel("1. 생산 대상", 15, GoldBright));
        if (targets.Count == 0)
        {
            box.AddChild(MakeLabel("(생산 가능한 논·밭·마을이 없습니다)", 13, Parchment));
        }
        else if (hasFixedTarget)
        {
            box.AddChild(MakeLabel($"{FacilityName(selectedFacility)} ({selectedTarget!.Value.Q},{selectedTarget.Value.R})\n선택한 시설에서 생산을 진행합니다.", 13, GoldBright));
        }
        else
        {
            var tree = new Tree
            {
                Columns = 4,
                ColumnTitlesVisible = true,
                HideRoot = true,
                SelectMode = Tree.SelectModeEnum.Row,
                CustomMinimumSize = new Vector2(0, Mathf.Min(130, 34 + targets.Count * 28)),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            tree.AddThemeFontOverride("font", _font);
            tree.AddThemeFontSizeOverride("font_size", 13);
            tree.AddThemeFontOverride("title_button_font", _font);
            tree.AddThemeFontSizeOverride("title_button_font_size", 12);
            tree.SetColumnTitle(0, "선택"); tree.SetColumnExpand(0, false); tree.SetColumnCustomMinimumWidth(0, 52);
            tree.SetColumnTitle(1, "대상"); tree.SetColumnExpand(1, true);
            tree.SetColumnTitle(2, "좌표"); tree.SetColumnExpand(2, false); tree.SetColumnCustomMinimumWidth(2, 72);
            tree.SetColumnTitle(3, "예상 보상"); tree.SetColumnExpand(3, true);
            var root = tree.CreateItem();
            foreach (var target in targets)
            {
                var item = tree.CreateItem(root);
                item.SetText(0, target.Plot == selectedTarget ? "◆" : "◇");
                item.SetText(1, FacilityName(target.Code));
                item.SetText(2, $"{target.Plot.Q},{target.Plot.R}");
                var sample = selectedGeneral is { } sg ? _state.Generals.FirstOrDefault(g => g.Id == sg) : null;
                var reward = sample is null ? (Gold: 0, Provisions: 0) : ProductionRules.Reward(target.Code, sample.Politics);
                item.SetText(3, reward.Gold > 0 ? $"금 +{reward.Gold}" : reward.Provisions > 0 ? $"군량 +{reward.Provisions}" : "-");
                item.SetMetadata(0, Variant.From(target.Plot.Q));
                item.SetMetadata(1, Variant.From(target.Plot.R));
                item.SetMetadata(2, Variant.From(target.Code));
            }
            tree.ItemSelected += () =>
            {
                var it = tree.GetSelected();
                if (it is null) { return; }
                selectedTarget = new HexCoord(it.GetMetadata(0).AsInt32(), it.GetMetadata(1).AsInt32());
                selectedFacility = it.GetMetadata(2).AsString();
                for (var item = root.GetFirstChild(); item is not null; item = item.GetNext())
                {
                    item.SetText(0, item == it ? "◆" : "◇");
                    var sample = selectedGeneral is { } sg ? _state.Generals.FirstOrDefault(g => g.Id == sg) : null;
                    var code = item.GetMetadata(2).AsString();
                    var reward = sample is null ? (Gold: 0, Provisions: 0) : ProductionRules.Reward(code, sample.Politics);
                    item.SetText(3, reward.Gold > 0 ? $"금 +{reward.Gold}" : reward.Provisions > 0 ? $"군량 +{reward.Provisions}" : "-");
                }
                RefreshSummary();
                RefreshSelectionButtons();
            };
            box.AddChild(tree);
        }

        box.AddChild(MakeLabel("2. 주최 장수", 15, GoldBright));
        if (generals.Count == 0)
        {
            box.AddChild(MakeLabel("(투입 가능한 장수가 없습니다)", 13, Parchment));
        }
        else
        {
            var tree = new Tree
            {
                Columns = 7,
                ColumnTitlesVisible = true,
                HideRoot = true,
                SelectMode = Tree.SelectModeEnum.Row,
                CustomMinimumSize = new Vector2(0, Mathf.Min(180, 34 + generals.Count * 28)),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            tree.AddThemeFontOverride("font", _font);
            tree.AddThemeFontSizeOverride("font_size", 13);
            tree.AddThemeFontOverride("title_button_font", _font);
            tree.AddThemeFontSizeOverride("title_button_font_size", 12);
            tree.SetColumnTitle(0, "선택"); tree.SetColumnExpand(0, false); tree.SetColumnCustomMinimumWidth(0, 52);
            tree.SetColumnTitle(1, "이름"); tree.SetColumnExpand(1, true);
            foreach (var (col, titleText) in new[] { (2, "무"), (3, "지"), (4, "정"), (5, "일수") })
            {
                tree.SetColumnTitle(col, titleText);
                tree.SetColumnExpand(col, false);
                tree.SetColumnCustomMinimumWidth(col, 42);
            }
            tree.SetColumnTitle(6, "현재 담당업무 / 예상 보상"); tree.SetColumnExpand(6, true); tree.SetColumnExpandRatio(6, 3);
            var root = tree.CreateItem();
            foreach (var general in generals)
            {
                var item = tree.CreateItem(root);
                item.SetText(0, general.Id == selectedGeneral ? "◆" : "◇");
                item.SetText(1, general.Name);
                item.SetText(2, general.Might.ToString());
                item.SetText(3, general.Intellect.ToString());
                item.SetText(4, general.Politics.ToString());
                item.SetText(5, $"{ProductionRules.GatherDays(general.Politics)}일");
                var reward = string.IsNullOrWhiteSpace(selectedFacility) ? (Gold: 0, Provisions: 0) : ProductionRules.Reward(selectedFacility, general.Politics);
                var rewardText = reward.Gold > 0 ? $"금 +{reward.Gold}" : reward.Provisions > 0 ? $"군량 +{reward.Provisions}" : "-";
                item.SetText(6, $"{CurrentDuty(general.Id)} · {rewardText}");
                item.SetMetadata(0, general.Id.Value);
                for (var col = 2; col <= 5; col++) { item.SetTextAlignment(col, HorizontalAlignment.Center); }
            }
            tree.ItemSelected += () =>
            {
                var it = tree.GetSelected();
                if (it is null) { return; }
                selectedGeneral = new GeneralId(it.GetMetadata(0).AsInt32());
                for (var item = root.GetFirstChild(); item is not null; item = item.GetNext())
                {
                    var gid = new GeneralId(item.GetMetadata(0).AsInt32());
                    var g = _state.Generals.First(x => x.Id == gid);
                    item.SetText(0, gid == selectedGeneral ? "◆" : "◇");
                    var reward = string.IsNullOrWhiteSpace(selectedFacility) ? (Gold: 0, Provisions: 0) : ProductionRules.Reward(selectedFacility, g.Politics);
                    var rewardText = reward.Gold > 0 ? $"금 +{reward.Gold}" : reward.Provisions > 0 ? $"군량 +{reward.Provisions}" : "-";
                    item.SetText(6, $"{CurrentDuty(g.Id)} · {rewardText}");
                }
                RefreshSummary();
                RefreshSelectionButtons();
            };
            box.AddChild(tree);
        }

        box.AddChild(MakeLabel("3. 투입 병종 (500명 고정)", 15, GoldBright));
        if (garrisons.Count == 0)
        {
            box.AddChild(MakeLabel("(500명 이상 대기 중인 지상 병종이 없습니다)", 13, Parchment));
        }
        else
        {
            var tree = new Tree
            {
                Columns = 5,
                ColumnTitlesVisible = true,
                HideRoot = true,
                SelectMode = Tree.SelectModeEnum.Row,
                CustomMinimumSize = new Vector2(0, Mathf.Min(170, 34 + garrisons.Count * 28)),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            tree.AddThemeFontOverride("font", _font);
            tree.AddThemeFontSizeOverride("font_size", 13);
            tree.AddThemeFontOverride("title_button_font", _font);
            tree.AddThemeFontSizeOverride("title_button_font_size", 12);
            tree.SetColumnTitle(0, "선택"); tree.SetColumnExpand(0, false); tree.SetColumnCustomMinimumWidth(0, 52);
            tree.SetColumnTitle(1, "병종"); tree.SetColumnExpand(1, true);
            foreach (var (col, titleText) in new[] { (2, "대기"), (3, "훈련"), (4, "이동") })
            {
                tree.SetColumnTitle(col, titleText);
                tree.SetColumnExpand(col, false);
                tree.SetColumnCustomMinimumWidth(col, 58);
            }
            var root = tree.CreateItem();
            foreach (var (garrison, troop) in garrisons)
            {
                var item = tree.CreateItem(root);
                item.SetText(0, selectedTroop == troop.Code ? "◆" : "◇");
                item.SetText(1, troop.Name);
                item.SetText(2, garrison.Troops.ToString());
                item.SetText(3, garrison.TrainingLevel.ToString());
                item.SetText(4, troop.MovementPerDay.ToString());
                item.SetMetadata(0, troop.Code);
                for (var col = 2; col <= 4; col++) { item.SetTextAlignment(col, HorizontalAlignment.Center); }
            }
            tree.ItemSelected += () =>
            {
                var it = tree.GetSelected();
                if (it is null) { return; }
                selectedTroop = it.GetMetadata(0).AsString();
                for (var item = root.GetFirstChild(); item is not null; item = item.GetNext())
                {
                    item.SetText(0, item.GetMetadata(0).AsString() == selectedTroop ? "◆" : "◇");
                }
                RefreshSummary();
                RefreshSelectionButtons();
            };
            box.AddChild(tree);
        }

        box.AddChild(summary);
        RefreshSelectionButtons();
        var start = MakeButton("생산 시작", accent: true);
        start.CustomMinimumSize = new Vector2(0, 38);
        start.Pressed += () =>
        {
            if (_advancing) { return; }
            if (selectedTarget is not { } target || selectedGeneral is not { } general || string.IsNullOrWhiteSpace(selectedTroop))
            {
                ShowNotice("생산 불가", "대상 시설, 장수, 병종을 모두 선택해야 합니다.");
                return;
            }

            var gName = _state.Generals.First(g => g.Id == general).Name;
            var troopName = _troops.First(t => t.Code == selectedTroop).Name;
            ShowConfirm("생산 작전 확인",
                $"{cityData.Name}에서 {FacilityName(selectedFacility)} 생산을 시작합니다.\n수행 장수: {gName}\n투입 병종: {troopName} 500명{DutyReleaseNotice(general)}\n\n시작 후 취소할 수 없습니다.",
                () =>
                {
                    if (_advancing) { return; }
                    if (OfficerUnavailable(general)) { ShowNotice("생산 불가", "장수가 다른 업무 또는 출전 예약 중입니다."); return; }
                    var result = _producer.Start(_state, city, target, selectedFacility, selectedTroop, general);
                    if (!result.Ok)
                    {
                        ShowNotice("생산 실패", result.Error ?? "조건에 맞지 않아 실행할 수 없습니다.");
                        return;
                    }

                    _state = result.State;
                    Report($"[생산] {cityData.Name}에서 {gName} 장수가 {FacilityName(selectedFacility)} 생산 작전을 시작했습니다.", Parchment);
                    CloseModal();
                    SelectCity(city);
                    Redraw("생산 작전 시작");
                });
        };
        box.AddChild(start);
        RefreshSummary();

        scroll.CustomMinimumSize = new Vector2(mw, mh);
    }

    private IEnumerable<(HexCoord Plot, string Code)> ProductionTargets(City city)
    {
        foreach (var (code, intact) in new[] { ("paddy", city.Paddies), ("farm", city.Farms), ("village", city.Villages) })
        {
            foreach (var p in _state.Placements.Where(p => p.City == city.Id && p.Code == code).Take(intact))
            {
                yield return (p.Plot, p.Code);
            }
        }
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

            var cell = new Control { CustomMinimumSize = new Vector2(104, 118) };
            var tile = new PanelContainer
            {
                MouseFilter = Control.MouseFilterEnum.Stop,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            };
            tile.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            tile.AddThemeStyleboxOverride("panel", CardBox(gi == _depSelectedUnit));
            cell.AddChild(tile);
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

            // 우측 상단 X — 이 예약을 바로 취소.
            var xbtn = MakeButton("✕");
            xbtn.AddThemeFontSizeOverride("font_size", 11);
            xbtn.CustomMinimumSize = new Vector2(20, 20);
            xbtn.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            xbtn.OffsetLeft = -22;
            xbtn.OffsetTop = 2;
            xbtn.OffsetRight = -2;
            xbtn.OffsetBottom = 22;
            xbtn.Pressed += () =>
            {
                Dbg($"UI delete pending[{idx}] '{_pendingDeploys[idx].Label}'");
                _pendingDeploys.RemoveAt(idx);
                if (_depSelectedUnit == idx) { _depSelectedUnit = -1; }
                SelectCity(_depModalCity);
                OpenDeployHub();
            };
            cell.AddChild(xbtn);
            grid.AddChild(cell);
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
            var strain = _state.Garrisons.FirstOrDefault(g => g.City == city && g.TroopCode == srq.TroopCode && !g.Trainee)?.TrainingLevel ?? 0;
            var stgt = srq.Target is { } tg ? "→ " + (_state.Cities.FirstOrDefault(c => c.Position == tg)?.Name ?? $"({tg.Q},{tg.R})") : "목표 미지정(성 앞 대기)";
            box.AddChild(GoldRule());
            box.AddChild(MakeLabel($"◈ {stmpl?.Name ?? srq.TroopCode} {srq.Troops}명 · 선봉 {svan} · 훈련 {strain} · {stgt}", 13, GoldBright));

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
                mb.Pressed += () => { Dbg($"UI mode idx={sidx} -> {mm}"); _pendingDeploys[sidx] = (_pendingDeploys[sidx].Req with { Mode = mm }, _pendingDeploys[sidx].Label); OpenDeployHub(); };
                modeRow.AddChild(mb);
            }

            box.AddChild(modeRow);
            var mdesc = MakeLabel(ModeDesc(srq.Mode), 11, Parchment);
            mdesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            box.AddChild(mdesc);

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
        DrawDeployPaths(); // 편성 중에만 예약 경로 표시(삭제·수정 즉시 반영)
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
        _depModeDesc = null;
        _vanTree = null;
        _vanSortCol = 2;
        _vanSortAsc = true;
        _depProvDays = 0;
        _depProvSlider = null;
        _depProvLabel = null;

        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.52f, 400f, 660f);
        var mh = Mathf.Clamp(vp.Y * 0.9f, 360f, 820f); // 표가 고정 높이라, 모달은 내용에 맞춰 스크롤 없이 담기게
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
        var usedTroops = ReservedTroopsByCode(city, editIndex, editingSupply: false);
        var usedGens = ReservedDeployGenerals(editIndex, editingSupply: false);

        // 1) 병종
        box.AddChild(MakeLabel("병종 (대기 병력)", 13, GoldBright));
        var tg = new GridContainer { Columns = cols };
        tg.AddThemeConstantOverride("h_separation", 8);
        tg.AddThemeConstantOverride("v_separation", 8);
        box.AddChild(tg);
        foreach (var gar in _state.Garrisons.Where(g => g.City == city && g.Troops > 0 && !g.Trainee))
        {
            var code = gar.TroopCode;
            var remaining = gar.Troops - usedTroops.GetValueOrDefault(code, 0);
            if (remaining <= 0) { continue; }
            var template = _troops.FirstOrDefault(t => t.Code == code);
            var name = template?.Name ?? code;
            var warn = gar.TrainingLevel < 50 ? "  ⚠훈련부족" : "";
            var emblem = template is not null ? ClassEmblem(template.Class) : Icon(Sym.Sword);
            var card = DeployCard(emblem, name, $"{remaining}명 · 훈{gar.TrainingLevel}{warn}");
            var cap = System.Math.Min(remaining, DeployMaxTroopsFor(city));
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
                    PopulateVanTree(); // 적성 컬럼이 선택 병종 기준으로 갱신
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
        var maxButton = MakeButton("최대");
        maxButton.CustomMinimumSize = new Vector2(58, 28);
        maxButton.Pressed += () => { if (_depAmountSpin is { } sp) { sp.Value = sp.MaxValue; } };
        amtRow.AddChild(maxButton);

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
        _depModeDesc = MakeLabel("", 11, Parchment);
        _depModeDesc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _depModeDesc.CustomMinimumSize = new Vector2(mw - 60f, 0); // autowrap 최소높이 과대 추정 → 하단 여백 방지
        box.AddChild(_depModeDesc);

        // 3) 장수 편성 표 — 선봉·부관 체크 컬럼 + 정렬(고정 높이·내부 스크롤).
        _composeFree = _state.GeneralsAt(city).Where(g => !_state.IsGeneralBusy(g) && !usedGens.Contains(g)).OrderBy(g => g.Value).ToList();
        box.AddChild(GoldRule());
        box.AddChild(MakeLabel("장수 편성 (선봉 필수 · 부관 선택 · 상단 눌러 정렬)", 13, GoldBright));
        _vanTree = new Tree
        {
            Columns = 8,
            ColumnTitlesVisible = true,
            HideRoot = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0, 200),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _vanTree.AddThemeFontOverride("font", _font);
        _vanTree.AddThemeFontSizeOverride("font_size", 13);
        _vanTree.AddThemeFontOverride("title_button_font", _font);
        _vanTree.AddThemeFontSizeOverride("title_button_font_size", 12);
        foreach (var (col, t) in new[] { (0, "선봉"), (1, "부관") })
        {
            _vanTree.SetColumnTitle(col, t);
            _vanTree.SetColumnExpand(col, false);
            _vanTree.SetColumnCustomMinimumWidth(col, 46);
        }

        _vanTree.SetColumnTitle(2, "이름");
        _vanTree.SetColumnExpand(2, true);
        _vanTree.SetColumnExpandRatio(2, 3);
        foreach (var (col, t) in new[] { (3, "무"), (4, "지"), (5, "정") })
        {
            _vanTree.SetColumnTitle(col, t);
            _vanTree.SetColumnExpand(col, false);
            _vanTree.SetColumnCustomMinimumWidth(col, 36);
        }

        _vanTree.SetColumnTitle(6, "적성·특성");
        _vanTree.SetColumnExpand(6, true);
        _vanTree.SetColumnExpandRatio(6, 2);
        _vanTree.SetColumnTitle(7, "현재 담당업무");
        _vanTree.SetColumnExpand(7, true);
        _vanTree.ItemEdited += OnRosterEdited;
        _vanTree.ColumnTitleClicked += (col, _) =>
        {
            var c = (int)col;
            if (c < 2) { return; } // 체크 컬럼은 정렬 없음
            if (_vanSortCol == c) { _vanSortAsc = !_vanSortAsc; }
            else { _vanSortCol = c; _vanSortAsc = true; }
            PopulateVanTree();
        };
        box.AddChild(_vanTree);
        PopulateVanTree();

        // 5) 미리보기 + 확인
        box.AddChild(GoldRule());
        _depPreview = MakeLabel("", 12, Parchment);
        _depPreview.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _depPreview.CustomMinimumSize = new Vector2(mw - 60f, 0);
        box.AddChild(_depPreview);
        var save = MakeButton("▶ 확인", accent: true);
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
            _depAmountSpin.MaxValue = System.Math.Min(DeployMaxTroopsFor(city), System.Math.Max(capEdit, rq.Troops));
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

            PopulateVanTree(); // 선봉·부관 체크 반영
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

    private void UpdateSupplyPreview()
    {
        if (_depPreview is null) { return; }
        var total = _supplyDraft.Values.Sum();
        var lines = _supplyDraft
            .Where(p => p.Value > 0)
            .OrderBy(p => p.Key, System.StringComparer.Ordinal)
            .Select(p => $"{_troops.FirstOrDefault(t => t.Code == p.Key)?.Name ?? p.Key} {p.Value}")
            .ToList();
        var vanguard = _depVan is { } v ? _state.Generals.First(g => g.Id == v).Name : "주장 미선택";
        var status = total > _cb.SupplyMaxTroops ? $"  ⚠ 최대 {_cb.SupplyMaxTroops}명 초과" : "";
        var provisions = SupplyProvisionsToCarry();
        var daysPer10k = _provPer10kPerDay <= 0 ? 0 : provisions / _provPer10kPerDay;
        if (_depProvLabel is not null)
        {
            var cityProv = _state.Cities.First(x => x.Id == _depModalCity).Provisions;
            _depProvLabel.Text = $"휴대 {provisions} / 성 비축 {cityProv} · 1만 기준 약 {daysPer10k}일";
        }
        _depPreview.Text = $"현재 편성: 보급부대 {total}명 · 주장 {vanguard} · 군량 {_depProvDays}일{status}\n"
            + (lines.Count == 0 ? "병종을 선택하세요." : string.Join(" · ", lines))
            + $"\n휴대 군량 {provisions} · 1만 병력 기준 약 {daysPer10k}일 보급 가능 · 스킬/적성 미적용 · 공방 최하";
    }

    private void PopulateSupplyGeneralTree()
    {
        if (_vanTree is null) { return; }
        _vanTree.Clear();
        var root = _vanTree.CreateItem();
        IEnumerable<GeneralId> ordered = _composeFree;
        ordered = _vanSortCol switch
        {
            1 => _vanSortAsc ? ordered.OrderBy(id => _state.Generals.First(g => g.Id == id).Name, System.StringComparer.Ordinal)
                : ordered.OrderByDescending(id => _state.Generals.First(g => g.Id == id).Name, System.StringComparer.Ordinal),
            2 => _vanSortAsc ? ordered.OrderBy(id => _state.Generals.First(g => g.Id == id).Might)
                : ordered.OrderByDescending(id => _state.Generals.First(g => g.Id == id).Might),
            3 => _vanSortAsc ? ordered.OrderBy(id => _state.Generals.First(g => g.Id == id).Intellect)
                : ordered.OrderByDescending(id => _state.Generals.First(g => g.Id == id).Intellect),
            4 => _vanSortAsc ? ordered.OrderBy(id => _state.Generals.First(g => g.Id == id).Politics)
                : ordered.OrderByDescending(id => _state.Generals.First(g => g.Id == id).Politics),
            _ => ordered.OrderBy(id => id.Value),
        };
        foreach (var generalId in ordered)
        {
            var general = _state.Generals.First(g => g.Id == generalId);
            var item = _vanTree.CreateItem(root);
            item.SetText(0, general.Id == _depVan ? "◆" : "◇");
            item.SetText(1, general.Name);
            item.SetText(2, general.Might.ToString());
            item.SetText(3, general.Intellect.ToString());
            item.SetText(4, general.Politics.ToString());
            item.SetText(5, CurrentDuty(general.Id));
            item.SetMetadata(0, general.Id.Value);
            for (var col = 2; col <= 4; col++) { item.SetTextAlignment(col, HorizontalAlignment.Center); }
            item.SetTextAlignment(0, HorizontalAlignment.Center);
        }
    }

    private void PopulateSupplyGeneralTreeDeferred()
        => PopulateSupplyGeneralTree();

    private void RestyleSupplyGeneralTreeSelection()
    {
        if (_vanTree?.GetRoot() is not { } root) { return; }
        for (var item = root.GetFirstChild(); item is not null; item = item.GetNext())
        {
            var id = new GeneralId(item.GetMetadata(0).AsInt32());
            item.SetText(0, id == _depVan ? "◆" : "◇");
        }
    }

    private int SupplyCapacityForDraft()
    {
        var total = _supplyDraft.Values.Sum();
        if (total <= 0) { return 0; }
        long weighted = 0;
        foreach (var (code, amount) in _supplyDraft)
        {
            if (amount <= 0) { continue; }
            var template = _troops.FirstOrDefault(t => t.Code == code);
            weighted += (long)(template?.ProvisionsCapacity ?? 300) * amount;
        }
        return (int)(weighted / total);
    }

    private int SupplyMaxCarryDays()
        => System.Math.Max(1, SupplyCapacityForDraft() / System.Math.Max(1, _provPer10kPerDay) + SupplyExtraCarryDays);

    private int SupplyProvisionsToCarry()
    {
        var total = _supplyDraft.Values.Sum();
        if (total <= 0) { return 0; }
        var capacity = SupplyMaxCarryDays() * total * _provPer10kPerDay / 10000;
        var wanted = _depProvDays * total * _provPer10kPerDay / 10000;
        var cityProv = _state.Cities.First(x => x.Id == _depModalCity).Provisions;
        return System.Math.Min(System.Math.Min(wanted, capacity), cityProv);
    }

    private int SupplyProvisionDaysFromAmount(int provisions)
    {
        var total = _supplyDraft.Values.Sum();
        if (provisions < 0 || total <= 0) { return SupplyMaxCarryDays(); }
        return System.Math.Clamp(provisions * 10000 / System.Math.Max(1, total * _provPer10kPerDay), 0,
            SupplyMaxCarryDays());
    }

    private void SyncSupplyProvisionSlider()
    {
        if (_depProvSlider is null) { return; }
        var maxDays = SupplyMaxCarryDays();
        _depProvSlider.MaxValue = maxDays;
        if (_depProvDays <= 0 || _depProvDays > maxDays) { _depProvDays = maxDays; }
        _depProvSlider.SetValueNoSignal(_depProvDays);
    }

    private Dictionary<string, int> ReservedTroopsByCode(CityId city, int editIndex, bool editingSupply)
    {
        var used = new Dictionary<string, int>(System.StringComparer.Ordinal);
        for (var i = 0; i < _pendingDeploys.Count; i++)
        {
            if (!editingSupply && i == editIndex) { continue; }
            var rq = _pendingDeploys[i].Req;
            if (rq.City != city) { continue; }
            used[rq.TroopCode] = used.GetValueOrDefault(rq.TroopCode, 0) + rq.Troops;
        }

        for (var i = 0; i < _pendingSupplyDeploys.Count; i++)
        {
            if (editingSupply && i == editIndex) { continue; }
            var rq = _pendingSupplyDeploys[i].Req;
            if (rq.City != city) { continue; }
            foreach (var line in rq.Lines)
            {
                used[line.TroopCode] = used.GetValueOrDefault(line.TroopCode, 0) + line.Troops;
            }
        }

        return used;
    }

    private HashSet<GeneralId> ReservedDeployGenerals(int editIndex, bool editingSupply)
    {
        var used = new HashSet<GeneralId>();
        for (var i = 0; i < _pendingDeploys.Count; i++)
        {
            if (!editingSupply && i == editIndex) { continue; }
            var rq = _pendingDeploys[i].Req;
            used.Add(rq.Vanguard);
            if (rq.Adjutant is { } a) { used.Add(a); }
        }

        for (var i = 0; i < _pendingSupplyDeploys.Count; i++)
        {
            if (editingSupply && i == editIndex) { continue; }
            used.Add(_pendingSupplyDeploys[i].Req.Vanguard);
        }

        return used;
    }

    private void SaveSupplyCompose()
    {
        void Err(string m) { if (_depPreview is not null) { _depPreview.Text = "⚠ " + m; } }
        var lines = _supplyDraft
            .Where(p => p.Value > 0)
            .OrderBy(p => p.Key, System.StringComparer.Ordinal)
            .Select(p => new SupplyLine(p.Key, p.Value))
            .ToList();
        var total = lines.Sum(l => l.Troops);
        if (lines.Count == 0) { Err("보급부대에 투입할 병종과 병력을 선택하세요."); return; }
        if (total > _cb.SupplyMaxTroops) { Err($"보급부대는 최대 {_cb.SupplyMaxTroops}명까지 편성할 수 있습니다."); return; }
        if (_depVan is not { } van) { Err("보급부대 주장을 선택하세요."); return; }
        var ids = new[] { van };
        var vName = _state.Generals.First(g => g.Id == van).Name;
        var lineText = string.Join(", ", lines.Select(l => $"{_troops.FirstOrDefault(t => t.Code == l.TroopCode)?.Name ?? l.TroopCode} {l.Troops}"));
        var provisions = SupplyProvisionsToCarry();
        var req = new SupplyDeployRequest(_depModalCity, lines, van, UnitMode.March, Provisions: provisions);
        var entry = (req, $"보급 {total}({vName}) · 군량{_depProvDays}일 · {lineText}");
        ShowConfirm("보급부대 예약 확인",
            $"{entry.Item2}\n휴대 군량 {provisions} · 1만 병력 기준 약 {(_provPer10kPerDay <= 0 ? 0 : provisions / _provPer10kPerDay)}일 보급 가능\n\n스킬/적성은 적용되지 않고 공방은 게임 최하 수치입니다.{DutyReleaseNotice(ids)}",
            () =>
            {
                if (_advancing || ids.Any(id => _state.IsGeneralBusy(id))
                    || _pendingSupplyDeploys.Where((_, index) => index != _supplyEditIndex).Any(p => p.Req.Vanguard == van)
                    || _pendingDeploys.Any(p => p.Req.Vanguard == van || p.Req.Adjutant == van))
                {
                    ShowNotice("보급부대 편성 불가", "선택한 장수가 다른 업무를 수행 중입니다.");
                    return;
                }

                _state = _state.ReleaseOfficerDuties(ids);
                if (_supplyEditIndex >= 0 && _supplyEditIndex < _pendingSupplyDeploys.Count) { _pendingSupplyDeploys[_supplyEditIndex] = entry; }
                else { _pendingSupplyDeploys.Add(entry); }
                Dbg($"SUPPLY SAVE city={req.City.Value} total={total} van={van.Value} lines={lines.Count}");
                SelectCity(_depModalCity);
                OpenSupplyHub(_depModalCity);
            });
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
        var maxDeploy = DeployMaxTroopsFor(_depModalCity);
        if (_depAmount > maxDeploy) { Err($"일반 부대는 최대 {maxDeploy}명까지 출전할 수 있습니다."); return; }
        if (_depAdj == van) { Err("부관은 선봉과 다른 장수여야 합니다."); return; }
        var available = AvailableDeployTroops(_depModalCity, _depTroop, _depEditIndex);
        if (_depAmount > available) { Err($"대기 병력이 부족합니다. 최대 {available}명까지 출전할 수 있습니다."); return; }

        var tName = _troops.FirstOrDefault(t => t.Code == _depTroop)?.Name ?? _depTroop;
        var vName = _state.Generals.First(g => g.Id == van).Name;
        var aName = _depAdj is { } a ? "+" + _state.Generals.First(g => g.Id == a).Name : "";
        var provisions = _depProvDays * _depAmount * _provPer10kPerDay / 10000;
        var req = new DeployRequest(_depModalCity, _depTroop, _depAmount, van, _depAdj, _depMode, _depTarget, provisions);
        var entry = (req, $"{tName} {_depAmount}({vName}{aName}) · {ModeName(_depMode)} · 군량{_depProvDays}일");
        var ids = req.Adjutant is { } adjId ? new[] { van, adjId } : new[] { van };
        ShowConfirm("출전 예약 확인", $"{entry.Item2}{DutyReleaseNotice(ids)}", () =>
        {
        if (_advancing || ids.Any(id => _state.IsGeneralBusy(id))
            || _pendingDeploys.Where((_, index) => index != _depEditIndex)
                .Any(p => ids.Contains(p.Req.Vanguard) || p.Req.Adjutant is { } adj && ids.Contains(adj))
            || _pendingSupplyDeploys.Any(p => ids.Contains(p.Req.Vanguard)))
        { ShowNotice("출전 불가", "선택한 장수가 다른 업무를 수행 중입니다."); return; }
        _state = _state.ReleaseOfficerDuties(ids);
        if (_depEditIndex >= 0 && _depEditIndex < _pendingDeploys.Count) { _pendingDeploys[_depEditIndex] = entry; }
        else { _pendingDeploys.Add(entry); }

        Dbg($"SAVE {(_depEditIndex >= 0 ? $"edit#{_depEditIndex}" : "add")} city={req.City.Value} troop={req.TroopCode} amt={req.Troops} van={req.Vanguard.Value} adj={(req.Adjutant?.Value.ToString() ?? "-")} mode={req.Mode} tgt={(req.Target is { } t ? $"{t.Q},{t.R}" : "none")} prov={req.Provisions} -> pending={_pendingDeploys.Count}");

        SelectCity(_depModalCity);
        OpenDeployHub();
        });
    }

    private int DeployMaxTroopsFor(CityId city)
    {
        var faction = _state.Cities.First(c => c.Id == city).Owner;
        return CommandEfficiency.CommandTroopDeployLimit(_state.ResearchOf(faction, FactionResearch.CommandTroopsCode), _cb);
    }

    private int AvailableDeployTroops(CityId city, string troopCode, int editIndex)
    {
        var stock = _state.Garrisons.FirstOrDefault(g => g.City == city && g.TroopCode == troopCode && !g.Trainee)?.Troops ?? 0;
        var reserved = 0;
        for (var i = 0; i < _pendingDeploys.Count; i++)
        {
            if (i == editIndex) { continue; }
            var rq = _pendingDeploys[i].Req;
            if (rq.City == city && rq.TroopCode == troopCode) { reserved += rq.Troops; }
        }

        foreach (var rq in _pendingSupplyDeploys.Where(p => p.Req.City == city).Select(p => p.Req))
        {
            reserved += rq.Lines.Where(l => l.TroopCode == troopCode).Sum(l => l.Troops);
        }

        return System.Math.Max(0, stock - reserved);
    }

    private readonly Dictionary<int, ImageTexture> _portraits = new();

    // 장수 초상: assets/portraits/{id}.png 있으면 그것, 없으면 공용 장수 흉상(icon_officer) 폴백.
    private ImageTexture OfficerPortrait(GeneralId id)
    {
        if (_portraits.TryGetValue(id.Value, out var cached)) { return cached; }

        if (PortraitPathFor(id) is { } path)
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
    }

    // 장수 편성 표 채우기(정렬 상태 반영). 0=선봉 체크, 1=부관 체크, 메타데이터에 GeneralId.
    private void PopulateVanTree()
    {
        if (_vanTree is null) { return; }
        _vanTree.Clear();
        var root = _vanTree.CreateItem();
        var gens = _composeFree.Select(id => _state.Generals.First(g => g.Id == id)).ToList();
        System.Comparison<General> cmp = _vanSortCol switch
        {
            3 => (a, b) => a.Might.CompareTo(b.Might),
            4 => (a, b) => a.Intellect.CompareTo(b.Intellect),
            5 => (a, b) => a.Politics.CompareTo(b.Politics),
            6 => (a, b) => string.Compare(AptTraitText(a), AptTraitText(b), System.StringComparison.Ordinal),
            _ => (a, b) => string.Compare(a.Name, b.Name, System.StringComparison.Ordinal),
        };
        gens.Sort(cmp);
        if (!_vanSortAsc) { gens.Reverse(); }

        foreach (var g in gens)
        {
            var item = _vanTree.CreateItem(root);
            item.SetCellMode(0, TreeItem.TreeCellMode.Check);
            item.SetEditable(0, true);
            item.SetChecked(0, _depVan is { } v && v == g.Id);
            item.SetCellMode(1, TreeItem.TreeCellMode.Check);
            item.SetEditable(1, true);
            item.SetChecked(1, _depAdj is { } a && a == g.Id);
            item.SetText(2, g.Name);
            item.SetText(3, g.Might.ToString());
            item.SetText(4, g.Intellect.ToString());
            item.SetText(5, g.Politics.ToString());
            item.SetText(6, AptTraitText(g));
            item.SetText(7, CurrentDuty(g.Id));
            item.SetTooltipText(7, CurrentDuty(g.Id));
            item.SetMetadata(0, g.Id.Value);
            for (var col = 3; col <= 5; col++) { item.SetTextAlignment(col, HorizontalAlignment.Center); }
        }
    }

    // 선봉/부관 체크 토글 처리 — 선봉은 1명(라디오처럼), 부관은 선택·선봉과 달라야 한다.
    private void OnRosterEdited()
    {
        if (_vanTree is null) { return; }
        var it = _vanTree.GetEdited();
        if (it is null) { return; }
        var col = _vanTree.GetEditedColumn();
        var id = new GeneralId(it.GetMetadata(0).AsInt32());

        if (col == 0)
        {
            if (it.IsChecked(0))
            {
                _depVan = id;
                if (_depAdj == id) { _depAdj = null; }
            }
            else if (_depVan == id) { _depVan = null; }
        }
        else if (col == 1)
        {
            if (it.IsChecked(1))
            {
                if (id == _depVan)
                {
                    it.SetChecked(1, false);
                    if (_depPreview is not null) { _depPreview.Text = "부관은 선봉과 다른 장수여야 합니다."; }
                    return;
                }

                _depAdj = id;
            }
            else if (_depAdj == id) { _depAdj = null; }
        }

        SyncRosterChecks();
        UpdateDepPreview();
    }

    // 체크 상태를 _depVan/_depAdj에 맞춰 일괄 동기화(다른 행의 중복 체크 해제).
    private void SyncRosterChecks()
    {
        var row = _vanTree?.GetRoot()?.GetFirstChild();
        while (row is not null)
        {
            var id = new GeneralId(row.GetMetadata(0).AsInt32());
            row.SetChecked(0, _depVan is { } v && v == id);
            row.SetChecked(1, _depAdj is { } a && a == id);
            row = row.GetNext();
        }
    }

    // 적성·특성 표기: 선택한 병종에 대한 적성 등급 + 전투 특기(있으면). 병종 미선택이면 등급 생략.
    private string AptTraitText(General g)
    {
        var parts = new List<string>();
        if (_depTroop is { } code && _troops.FirstOrDefault(t => t.Code == code) is { } tmpl)
        {
            parts.Add($"적성 {g.AptitudeFor(tmpl.Class)}");
        }

        if (!string.IsNullOrEmpty(g.BattleActive)) { parts.Add(g.BattleActive!); }
        if (g.Passives.Count > 0) { parts.Add($"특기 {g.Passives.Count}"); }
        return parts.Count > 0 ? string.Join(" · ", parts) : "—";
    }

    private void RestyleModes()
    {
        foreach (var (btn, mode) in _depModeButtons)
        {
            var sel = mode == _depMode;
            btn.AddThemeStyleboxOverride("normal", Frame(sel ? AccentFill : InkSoft, sel ? GoldBright : Gold, sel ? 2 : 1, 5, 6));
            btn.AddThemeColorOverride("font_color", sel ? GoldBright : Parchment);
        }

        if (_depModeDesc is not null) { _depModeDesc.Text = ModeDesc(_depMode); }
    }

    private static string ModeName(UnitMode m) => m switch
    {
        UnitMode.March => "행군",
        UnitMode.Advance => "전진",
        UnitMode.Attack => "공격",
        _ => m.ToString(),
    };

    // 이동 모드 설명(design-movement.md). 목표 지정·모드 선택 UI에 함께 노출.
    private static string ModeDesc(UnitMode m) => m switch
    {
        UnitMode.March => "행군 — 전투를 피해 빠르게 재배치. 멈추지 않고 통과하지만, 사거리를 지나는 동안 반격 없이 큰 피해를 받는다.",
        UnitMode.Advance => "전진 — 목표로 곧장 간다. 먼저 공격·추격은 하지 않되, 적이 막아서면 그 자리에서 멈춰 정상 쌍방 교전한다.",
        UnitMode.Attack => "공격 — 탐지한 적을 추격·섬멸한다(원래 목표보다 우선). 적 성은 사거리에서 멈춰 공성한다.",
        _ => "",
    };

    // 명령별 옵션 카드 목록: (표시명, 아이콘, 부가설명).
    private List<Faction> DiplomacyTargets(CommandKind kind, City city)
    {
        return _state.Factions
            .Where(f => f.Id != city.Owner)
            .Where(f => kind switch
            {
                CommandKind.FormAlliance => !_state.AreAllied(city.Owner, f.Id),
                CommandKind.BreakAlliance => _state.AreAllied(city.Owner, f.Id),
                _ => true,
            })
            .OrderBy(f => f.Id.Value)
            .ToList();
    }

    private List<(string Name, ImageTexture Icon, string Detail)> OptionList(
        (string Label, CommandKind Kind, string Param) cmd, City city)
    {
        var list = new List<(string, ImageTexture, string)>();
        if (cmd.Kind == CommandKind.AppointRecruitmentOfficer)
        {
            var previewTroops = _cb.AutoRecruitTroopsBase;
            foreach (var t in AutoRecruitTroopOptions())
            {
                var costPer100 = _cb.AutoRecruitGoldCostPer100(t.Code);
                var tickCost = _cb.AutoRecruitGoldCost(t.Code, previewTroops);
                list.Add((t.Name, ClassEmblem(t.Class), $"{ClassName(t.Class)} · 100명당 {costPer100}금\n7일 기본 비용 {tickCost}금"));
            }

            return list;
        }

        if (cmd.Kind == CommandKind.SelectMajorTroop)
        {
            var selected = _state.MajorTroops.Where(t => t.Faction == city.Owner)
                .Select(t => t.TroopCode)
                .ToHashSet(System.StringComparer.Ordinal);
            foreach (var t in MajorTroopOptions(city))
            {
                var detail = selected.Contains(t.Code)
                    ? "이미 주력병종"
                    : selected.Count >= 2 ? "선택 불가"
                    : "Lv.10 연구 가능";
                list.Add((t.Name, ClassEmblem(t.Class), detail));
            }

            return list;
        }

        switch (cmd.Param)
        {
            case "faction":
                foreach (var faction in DiplomacyTargets(cmd.Kind, city))
                {
                    var targetCity = _state.Cities.Where(c => c.Owner == faction.Id)
                        .OrderBy(c => c.Position.Distance(city.Position))
                        .ThenBy(c => c.Id.Value)
                        .FirstOrDefault();
                    var relation = _state.AreAllied(city.Owner, faction.Id) ? "동맹 중" : "외교 가능";
                    var distance = targetCity is null ? "도시 없음"
                        : $"거리 {city.Position.Distance(targetCity.Position)}칸 · {CityStratagems.Days(city.Position, targetCity.Position, _cb)}일";
                    list.Add((faction.Name, Icon(Sym.Scroll), $"{relation}\n{distance}"));
                }

                break;
            case "troop":
                if (cmd.Kind == CommandKind.Research)
                {
                    list.Add(("통솔 병력", Icon(Sym.People), CommandTroopsResearchOptionDetail(city)));
                }

                foreach (var t in _troops)
                {
                    var detail = cmd.Kind == CommandKind.Research
                        ? ResearchOptionDetail(city, t.Code)
                        : ClassName(t.Class);
                    if (cmd.Kind is CommandKind.Recruit or CommandKind.Conscript)
                    {
                        // 모집 자원 게이트를 미리 알려준다(발행 실패를 사후 로그로만 알던 문제).
                        var lack = t.Class == TroopClass.Cavalry && city.Horses <= 0 ? "⚠ 말 부족 — 모집 불가"
                            : t.Class == TroopClass.Elephant && city.Elephants <= 0 ? "⚠ 코끼리 부족 — 모집 불가"
                            : city.Ore <= 0 ? "⚠ 광석 부족 — 모집 불가"
                            : null;
                        var cost = t.Class == TroopClass.Cavalry ? "광석·인구 1/명 · 말 3명당 1"
                            : t.Class == TroopClass.Elephant ? "광석·인구 1/명 · 코끼리 1000명당 1"
                            : "광석·인구 1/명";
                        detail = lack ?? cost;
                    }

                    list.Add((t.Name, ClassEmblem(t.Class), detail));
                }

                break;
            case "tax":
                foreach (var v in new[] { 0, 10, 20, 30, 40, 50 }) { list.Add(($"{v}%", Icon(Sym.Coin), "세율")); }
                break;
            case "garrison":
                foreach (var g in _state.Garrisons.Where(g => g.City == city.Id && g.Troops > 0)
                    .OrderBy(g => g.TroopCode, System.StringComparer.Ordinal).ThenBy(g => g.Trainee))
                {
                    var t = _troops.FirstOrDefault(x => x.Code == g.TroopCode);
                    var name = (t?.Name ?? g.TroopCode) + (g.Trainee ? " (신병)" : "");
                    list.Add((name, t is null ? Icon(Sym.Sword) : ClassEmblem(t.Class),
                        $"{g.Troops}명 · 훈련 {g.TrainingLevel}"));
                }

                break;
            case "facility":
                foreach (var (label, code) in Facilities)
                {
                    var (owned, cost) = code switch
                    {
                        "paddy" => (city.Paddies, _cb.BuildCostPaddy),
                        "farm" => (city.Farms, _cb.BuildCostFarm),
                        "village" => (city.Villages, _cb.BuildCostVillage),
                        _ => (city.Workshop ? 1 : 0, _cb.BuildCostWorkshop),
                    };
                    var max = code == "workshop" ? 1 : CommandEfficiency.BuildSlots(city.Castle, _cb);
                    list.Add((label, FacilityIcon(code), $"보유 {owned} / 최대 {max}\n비용 {cost}금\n효과: {FacilityEffectText(code)}"));
                }

                break;
            case "repairable":
                foreach (var (label, code) in Repairables)
                {
                    var state = code switch
                    {
                        "paddy" => city.RuinedPaddies > 0 ? $"잔해 {city.RuinedPaddies}" : "이상 없음",
                        "farm" => city.RuinedFarms > 0 ? $"잔해 {city.RuinedFarms}" : "이상 없음",
                        "village" => city.RuinedVillages > 0 ? $"잔해 {city.RuinedVillages}" : "이상 없음",
                        "workshop" => city.WorkshopRuined ? "파손" : "이상 없음",
                        "mine" => city.MineDestroyed ? "파괴" : "이상 없음",
                        "ranch" => city.RanchDestroyed ? "파괴" : "이상 없음",
                        _ => city.ElephantGardenDestroyed ? "파괴" : "이상 없음",
                    };
                    list.Add((label, Icon(Sym.Wall), state));
                }

                break;
            case "stratagem":
                foreach (var s in Strats) { list.Add((s.Label, StratIcon(s.Code), StratDesc(s.Code))); }
                break;
        }

        return list;
    }

    private string CommandTroopsResearchOptionDetail(City city)
    {
        var level = _state.ResearchOf(city.Owner, FactionResearch.CommandTroopsCode);
        var current = CommandEfficiency.CommandTroopDeployLimit(level, _cb);
        if (level >= 10)
        {
            return $"Lv.{level}/10\n최대 편성 {current}명";
        }

        var next = level + 1;
        return $"Lv.{level} → Lv.{next}/10\n편성 {current} → {CommandEfficiency.CommandTroopDeployLimit(next, _cb)}명\n비용 {CommandEfficiency.CommandTroopResearchCost(next)}금";
    }

    private void BuildHeroRecruitCards(VBoxContainer box, City city)
    {
        _state = new HeroUnlockService().Evaluate(_state);
        var states = _state.HeroUnlocks
            .Select(h => (_state.HeroStates.FirstOrDefault(s => s.General == h.General)
                ?? new HeroUnlockState(h.General, HeroUnlockStatus.Locked), h))
            .OrderBy(x => x.Item1.CanRecruit && x.Item1.EligibleFaction == city.Owner ? 0 : 1)
            .ThenBy(x => x.h.General.Value)
            .ToList();
        if (states.Count == 0)
        {
            box.AddChild(MakeLabel("위인 해금 데이터가 없습니다.", 15, Parchment));
            return;
        }

        var grid = new GridContainer { Columns = 2, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 10);
        box.AddChild(grid);

        foreach (var (state, hero) in states)
        {
            var general = _state.Generals.FirstOrDefault(g => g.Id == state.General);
            var name = general?.Name ?? $"장수 {state.General.Value}";
            var type = HeroTypeName(hero.Type, state.Status);
            var canRecruit = state.CanRecruit && state.EligibleFaction == city.Owner && city.Gold >= hero.RecruitGold;
            var status = HeroStatusText(state, city.Owner);
            var card = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            card.AddThemeStyleboxOverride("panel", Frame(new Color(0.12f, 0.08f, 0.04f, state.CanRecruit ? 0.94f : 0.72f),
                state.CanRecruit ? Gold : new Color(Parchment, 0.35f), 1, 8, 10));
            grid.AddChild(card);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            card.AddChild(row);

            var portrait = new TextureRect
            {
                Texture = OfficerPortrait(state.General),
                CustomMinimumSize = new Vector2(62, 78),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            };
            row.AddChild(portrait);

            var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            row.AddChild(text);
            text.AddChild(MakeLabel($"{name} · {type}", 17, state.CanRecruit ? Gold : new Color(Gold, 0.65f)));
            text.AddChild(MakeLabel($"{status}\n{HeroRequirementText(hero, general)}\n비용 {hero.RecruitGold}금", 13, Parchment));

            var recruit = MakeButton(canRecruit ? "영입" : state.CanRecruit ? "금 부족" : "잠김", accent: canRecruit);
            recruit.Disabled = !canRecruit;
            recruit.CustomMinimumSize = new Vector2(72, 54);
            recruit.Pressed += () => ConfirmRecruitHero(city, hero, name, type);
            row.AddChild(recruit);
        }
    }

    private void ConfirmRecruitHero(City city, HeroUnlockDefinition hero, string name, string type)
    {
        var actor = city.Governor
            ?? _state.Assignments.FirstOrDefault(p => p.Location == city.Id && p.Faction == city.Owner)?.General
            ?? _state.Factions.FirstOrDefault(f => f.Id == city.Owner)?.Ruler
            ?? hero.General;
        ShowConfirm("위인 영입",
            $"{city.Name}에서 {name}을(를) 영입합니다.\n유형: {type}\n비용: {hero.RecruitGold}금\n\n진행하시겠습니까?",
            () =>
            {
                var request = new CommandRequest(city.Id, CommandKind.RecruitHero, actor, TargetGeneral: hero.General);
                var result = _commander.Issue(_state, request);
                if (result.Ok)
                {
                    _state = result.State;
                    _log.Text = $"위인 영입: {name}";
                    Report($"[인재] {city.Name}에서 {name}을(를) 영입했습니다.", GoldBright);
                    CloseModal();
                    SelectCity(city.Id);
                    Redraw(_log.Text);
                }
                else
                {
                    ShowNotice("위인 영입 실패", result.Error ?? "조건에 맞지 않아 영입할 수 없습니다.");
                }
            });
    }

    private static string HeroTypeName(HeroUnlockType type, HeroUnlockStatus status)
        => status == HeroUnlockStatus.Wanderer ? "유랑 위인" : type switch
        {
            HeroUnlockType.Faction => "세력형 위인",
            HeroUnlockType.Region => "도시형 위인",
            HeroUnlockType.Wanderer => "유랑 위인",
            _ => "위인",
        };

    private static string HeroStatusText(HeroUnlockState state, FactionId viewer)
        => state.Status switch
        {
            HeroUnlockStatus.Locked => "상태: 잠김",
            HeroUnlockStatus.Unlocked when state.EligibleFaction == viewer => "상태: 해금됨",
            HeroUnlockStatus.Unlocked => $"상태: 타 세력 해금({state.EligibleFaction?.Value ?? 0})",
            HeroUnlockStatus.Recruited => "상태: 소속됨",
            HeroUnlockStatus.Wanderer => "상태: 유랑",
            HeroUnlockStatus.Excluded => "상태: 제외",
            _ => $"상태: {state.Status}",
        };

    private string HeroRequirementText(HeroUnlockDefinition hero, General? general)
    {
        var requiredYear = hero.UnlockYear > 0 ? hero.UnlockYear : general?.UnlockYear ?? 0;
        var year = requiredYear > 0 ? $"해금 가능 {requiredYear}년" : "해금 가능 년도 제한 없음";
        var conditions = hero.ConditionList.Count == 0
            ? "조건 없음"
            : string.Join(", ", hero.ConditionList.Select(c => c.Text ?? HeroConditionLabel(c)));
        return $"{year}\n조건: {conditions}";
    }

    private string HeroConditionLabel(HeroUnlockCondition c) => c.Code switch
    {
        "owned_cities" => $"도시 {c.Value}개 보유",
        "owned_region_cities" => $"{c.Region} 지역 도시 {c.Value}개 보유",
        "city_security_at_least" => $"치안 {c.Value} 이상 도시 보유",
        "research_level" => $"{TroopName(c.TroopCode ?? "")} 연구 Lv.{c.Value}",
        "major_troop" => $"{TroopName(c.TroopCode ?? "")} 주력병종",
        "gold_at_least" or "monthly_gold_at_least" => $"금 {c.Value} 이상",
        _ => c.Code,
    };

    private List<TroopTemplate> AutoRecruitTroopOptions()
        => _troops.Where(t => t.Class != TroopClass.Naval && _cb.AutoRecruitGoldCostPer100(t.Code) > 0)
            .OrderBy(t => _cb.AutoRecruitGoldCostPer100(t.Code))
            .ThenBy(t => t.Code, System.StringComparer.Ordinal)
            .ToList();

    private List<TroopTemplate> MajorTroopOptions(City city)
    {
        var selected = _state.MajorTroops.Where(t => t.Faction == city.Owner)
            .Select(t => t.TroopCode)
            .ToHashSet(System.StringComparer.Ordinal);
        return _troops.Where(t => t.Class != TroopClass.Naval)
            .OrderByDescending(t => selected.Contains(t.Code))
            .ThenBy(t => t.Code, System.StringComparer.Ordinal)
            .ToList();
    }

    private string ResearchOptionDetail(City city, string troopCode)
    {
        var level = _state.ResearchOf(city.Owner, troopCode);
        var maxLevel = ResearchMaxLevelFor(city.Owner, troopCode);
        var major = _state.IsMajorTroop(city.Owner, troopCode);
        if (level >= maxLevel)
        {
            return $"{ResearchStars(level, major)}\n최대 · 공·방 +{ResearchCurve.Bonus(level)}";
        }

        var next = level + 1;
        var cost = CommandEfficiency.ResearchCost(next, _cb);
        var active = _state.Commands.Any(c => c.Kind == CommandKind.Research
            && _state.Cities.FirstOrDefault(x => x.Id == c.City)?.Owner == city.Owner);
        var gate = active ? "연구 진행중"
            : city.Gold < cost ? "금 부족"
            : $"비용 {cost}금";
        var inc = ResearchCurve.Bonus(next) - ResearchCurve.Bonus(level);
        return $"{ResearchStars(level, major)}\n공·방 +{inc}\n{gate}";
    }

    private int ResearchMaxLevelFor(FactionId faction, string troopCode)
        => _state.IsMajorTroop(faction, troopCode) ? _cb.ResearchMaxLevel : 7;

    private static string ResearchStars(int level, bool major)
    {
        var gold = "#" + GoldBright.ToHtml(false);
        var empty = "#" + Parchment.ToHtml(false);
        var locked = "#777777";
        var s = "";
        for (var i = 1; i <= 10; i++)
        {
            if (!major && i > 7)
            {
                s += $"[color={locked}]☆[/color]";
            }
            else if (i <= level)
            {
                s += $"[color={gold}]★[/color]";
            }
            else
            {
                s += $"[color={empty}]☆[/color]";
            }
        }

        return s;
    }

    private void ConfirmMajorTroops(CityId cityId)
    {
        var city = _state.Cities.First(c => c.Id == cityId);
        var options = MajorTroopOptions(city);
        var selected = _modalMultiParams.OrderBy(i => i)
            .Where(i => i >= 0 && i < options.Count)
            .Select(i => options[i].Code)
            .ToList();
        if (selected.Count == 0)
        {
            ShowNotice("주력병종 선택", "선택한 병종이 없습니다.");
            return;
        }

        var names = string.Join(", ", selected.Select(TroopName));
        var actor = _state.GeneralsAt(cityId).FirstOrDefault();
        if (actor.Value == 0)
        {
            actor = _state.Generals.First().Id;
        }

        ShowConfirm("주력병종 선택",
            $"{city.Name} 세력의 주력병종으로 {names}을(를) 선택합니다.\n\n주력병종은 철회할 수 없고, 최대 2개까지만 선택 가능합니다.\n선택한 병종은 Lv.10까지 전투 교리 연구가 가능합니다.\n\n적용하시겠습니까?",
            () =>
            {
                CommandResult? last = null;
                foreach (var code in selected)
                {
                    last = _commander.Issue(_state,
                        new CommandRequest(cityId, CommandKind.SelectMajorTroop, actor, TroopCode: code));
                    if (!last.Ok) { break; }
                    _state = last.State;
                }

                if (last?.Ok == true)
                {
                    Report($"[주력병종] {city.Name} 세력의 주력병종으로 {names}을(를) 선택했습니다.", GoldBright);
                }
                else
                {
                    ShowNotice("주력병종 선택 실패", last?.Error ?? "조건에 맞지 않아 실행할 수 없습니다.");
                }

                CloseModal();
                SelectCity(cityId);
                Redraw(last?.Ok == true ? $"주력병종 선택: {names}" : $"실패: {last?.Error}");
            });
    }

    private bool IsFacilityBuildDisabled(City city, string code)
    {
        if (code == "workshop")
        {
            return city.Workshop || _state.Commands.Any(c => c.City == city.Id
                && c.Kind == CommandKind.Build && c.Facility == "workshop");
        }

        var pending = _state.Commands.Count(c => c.City == city.Id && c.Kind == CommandKind.Build && c.Facility != "workshop");
        var used = city.Paddies + city.Farms + city.Villages
            + city.RuinedPaddies + city.RuinedFarms + city.RuinedVillages + pending;
        return used >= CommandEfficiency.BuildSlots(city.Castle, _cb);
    }

    // 아이콘 카드(큰 아이콘 + 이름 + 설명). 클릭 판정은 호출부에서 GuiInput으로.
    private PanelContainer OptionCard((string Name, ImageTexture Icon, string Detail) o, bool disabled = false)
    {
        var starDetail = o.Detail.Contains("[color=", System.StringComparison.Ordinal);
        var card = new PanelContainer
        {
            CustomMinimumSize = new Vector2(starDetail ? 186 : 148, o.Detail.Contains('\n') ? 138 : 121),
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = disabled ? Control.CursorShape.Forbidden : Control.CursorShape.PointingHand,
            Modulate = disabled ? new Color(1f, 1f, 1f, 0.42f) : Colors.White,
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
        if (starDetail)
        {
            var det = MakeRichLabel(o.Detail, 14, Parchment);
            det.HorizontalAlignment = HorizontalAlignment.Center;
            det.CustomMinimumSize = new Vector2(170, 0);
            v.AddChild(det);
        }
        else
        {
            var det = MakeLabel(o.Detail, 14, Parchment);
            det.HorizontalAlignment = HorizontalAlignment.Center;
            v.AddChild(det);
        }

        card.MouseEntered += () =>
        {
            if (disabled) { return; }
            if (!_optionCards.Contains(card) || !IsOptionSelected(_optionCards.IndexOf(card)))
            {
                card.AddThemeStyleboxOverride("panel", CardBox(false, hover: true));
            }
        };
        card.MouseExited += () =>
        {
            if (disabled) { return; }
            if (!_optionCards.Contains(card) || !IsOptionSelected(_optionCards.IndexOf(card)))
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

        var detail = PlainUiText(o.Detail);
        if (GodotObject.IsInstanceValid(_modalDetail))
        {
            _modalDetail!.Text = detail.Length > 0 ? $"▶  {o.Name}  —  {detail}" : $"▶  {o.Name}";
        }

        RefreshResearchFundingPreview();
    }

    private bool IsOptionSelected(int idx)
        => _modalMultiParams.Count > 0 ? _modalMultiParams.Contains(idx) : idx == _modalParam;

    private void AddAutoRecruitRatePicker(VBoxContainer box, List<(string Name, ImageTexture Icon, string Detail)> troopOptions)
    {
        box.AddChild(MakeLabel("생산 비율을 선택하세요", 19, GoldBright));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        box.AddChild(row);
        for (var rate = 1; rate <= 3; rate++)
        {
            var r = rate;
            var extraSecurity = r == 1 ? 0 : r;
            var detail = r == 1 ? "기본 생산\n치안 추가 부담 없음" : $"기본×{r}\n비용×{r} · 치안 추가 -{extraSecurity}";
            var card = OptionCard(($"{r}배", Icon(Sym.People), detail));
            card.CustomMinimumSize = new Vector2(168, 126);
            card.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                {
                    _autoRecruitRateParam = r;
                    RefreshAutoRecruitRateCards(troopOptions);
                }
            };
            _autoRecruitRateCards.Add(card);
            row.AddChild(card);
        }

        RefreshAutoRecruitRateCards(troopOptions);
    }

    private void AddResearchFundingPicker(VBoxContainer box, City city)
    {
        _researchFundingRatios.Clear();
        _researchFundingPreview = null;
        var cities = _state.Cities.Where(c => c.Owner == city.Owner)
            .OrderBy(c => c.Id.Value)
            .ToList();

        box.AddChild(MakeLabel("연구비 분담 도시", 19, GoldBright));
        box.AddChild(MakeLabel("보유 성별 연구비 부담 비율입니다. 비율을 0으로 두면 해당 성은 이번 연구비를 부담하지 않습니다.", 13, Parchment));
        var tree = new GridContainer { Columns = 4, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        tree.AddThemeConstantOverride("h_separation", 10);
        tree.AddThemeConstantOverride("v_separation", 6);
        box.AddChild(tree);
        foreach (var head in new[] { "도시", "보유 금", "비율", "예상 분담" })
        {
            var h = MakeLabel(head, 14, Gold);
            h.HorizontalAlignment = HorizontalAlignment.Center;
            tree.AddChild(h);
        }

        foreach (var c in cities)
        {
            tree.AddChild(MakeLabel(c.Name, 14, Parchment));
            var gold = MakeLabel($"{c.Gold}금", 14, Parchment);
            gold.HorizontalAlignment = HorizontalAlignment.Center;
            tree.AddChild(gold);
            var ratio = new SpinBox
            {
                MinValue = 0,
                MaxValue = 10,
                Step = 1,
                Value = 1,
                CustomMinimumSize = new Vector2(74, 31),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            };
            ratio.ValueChanged += _ => RefreshResearchFundingPreview();
            _researchFundingRatios[c.Id.Value] = ratio;
            tree.AddChild(ratio);
            var amount = MakeLabel("", 14, Parchment);
            amount.Name = $"ResearchFundingAmount{c.Id.Value}";
            amount.HorizontalAlignment = HorizontalAlignment.Center;
            tree.AddChild(amount);
        }

        _researchFundingPreview = MakeLabel("", 14, Parchment);
        box.AddChild(_researchFundingPreview);
        RefreshResearchFundingPreview();
    }

    private List<ResearchFundingShare> CurrentResearchFundingShares()
        => _researchFundingRatios
            .Select(kv => new ResearchFundingShare(new CityId(kv.Key), (int)kv.Value.Value))
            .Where(s => s.Ratio > 0)
            .OrderBy(s => s.City.Value)
            .ToList();

    private int CurrentResearchCost(City city)
    {
        var cmd = Cmds[_cmdIndex];
        if (cmd.Kind != CommandKind.Research)
        {
            return 0;
        }

        if (cmd.Param == "wall")
        {
            return city.WallLevel >= _cb.WallResearchMaxLevel ? 0 : _cb.WallResearchCostPerLevel * (city.WallLevel + 1);
        }

        if (cmd.Param == "troop" && _modalParam == 0)
        {
            var commandLevel = _state.ResearchOf(city.Owner, FactionResearch.CommandTroopsCode);
            return commandLevel >= 10 ? 0 : CommandEfficiency.CommandTroopResearchCost(commandLevel + 1);
        }

        var troopIndex = cmd.Kind == CommandKind.Research ? _modalParam - 1 : _modalParam;
        if (troopIndex < 0 || troopIndex >= _troops.Count)
        {
            return 0;
        }

        var troopCode = _troops[troopIndex].Code;
        var level = _state.ResearchOf(city.Owner, troopCode);
        var max = ResearchMaxLevelFor(city.Owner, troopCode);
        return level >= max ? 0 : CommandEfficiency.ResearchCost(level + 1, _cb);
    }

    private void RefreshResearchFundingPreview()
    {
        if (!GodotObject.IsInstanceValid(_researchFundingPreview) || _selected is not { } cityId)
        {
            return;
        }

        var city = _state.Cities.FirstOrDefault(c => c.Id == cityId);
        if (city is null)
        {
            return;
        }

        var cost = CurrentResearchCost(city);
        var shares = CurrentResearchFundingShares();
        var allocations = AllocateResearchFunding(cost, shares);
        var cityMap = _state.Cities.ToDictionary(c => c.Id.Value);
        foreach (var kv in _researchFundingRatios)
        {
            var label = _researchFundingPreview.GetParent()?.FindChild($"ResearchFundingAmount{kv.Key}", true, false) as Label;
            if (label is null)
            {
                continue;
            }

            var amount = allocations.FirstOrDefault(a => a.City.Value == kv.Key).Amount;
            label.Text = amount > 0 ? $"{amount}금" : "-";
            if (cityMap.TryGetValue(kv.Key, out var c) && amount > c.Gold)
            {
                label.Modulate = Red;
            }
            else
            {
                label.Modulate = Parchment;
            }
        }

        if (shares.Count == 0)
        {
            _researchFundingPreview!.Text = "※ 최소 1개 도시의 분담 비율이 필요합니다.";
            _researchFundingPreview.Modulate = Red;
            return;
        }

        var shortage = allocations.FirstOrDefault(a => cityMap.TryGetValue(a.City.Value, out var c) && a.Amount > c.Gold);
        if (shortage.City.Value != 0 && cityMap.TryGetValue(shortage.City.Value, out var sc))
        {
            _researchFundingPreview!.Text = $"※ {sc.Name} 금 부족: {shortage.Amount}금 필요 / 보유 {sc.Gold}금";
            _researchFundingPreview.Modulate = Red;
            return;
        }

        _researchFundingPreview!.Text = $"총 연구비 {cost}금 · 분담 비율 합계 {shares.Sum(s => s.Ratio)}";
        _researchFundingPreview.Modulate = Parchment;
    }

    private static List<(CityId City, int Amount)> AllocateResearchFunding(int cost, IReadOnlyList<ResearchFundingShare> shares)
    {
        var grouped = shares.Where(s => s.Ratio > 0)
            .GroupBy(s => s.City)
            .Select(g => new ResearchFundingShare(g.Key, g.Sum(s => s.Ratio)))
            .OrderBy(s => s.City.Value)
            .ToList();
        var total = grouped.Sum(s => s.Ratio);
        if (cost <= 0 || total <= 0)
        {
            return grouped.Select(s => (s.City, 0)).ToList();
        }

        var allocations = grouped.Select(s =>
        {
            var raw = (long)cost * s.Ratio;
            return (s.City, Amount: (int)(raw / total), Remainder: raw % total);
        }).ToList();
        var assigned = allocations.Sum(a => a.Amount);
        return allocations
            .OrderByDescending(a => a.Remainder)
            .ThenBy(a => a.City.Value)
            .Select((a, index) => (a.City, a.Amount + (index < cost - assigned ? 1 : 0)))
            .OrderBy(a => a.City.Value)
            .ToList();
    }

    private bool ResearchFundingCanCover(City city, int cost)
    {
        if (_researchFundingRatios.Count == 0)
        {
            return city.Gold >= cost;
        }

        var cityMap = _state.Cities.ToDictionary(c => c.Id.Value);
        var shares = CurrentResearchFundingShares();
        if (shares.Count == 0)
        {
            return false;
        }

        return AllocateResearchFunding(cost, shares)
            .All(a => cityMap.TryGetValue(a.City.Value, out var c) && c.Gold >= a.Amount);
    }

    private void RefreshAutoRecruitRateCards(List<(string Name, ImageTexture Icon, string Detail)> troopOptions)
    {
        for (var i = 0; i < _autoRecruitRateCards.Count; i++)
        {
            _autoRecruitRateCards[i].AddThemeStyleboxOverride("panel", CardBox(i + 1 == _autoRecruitRateParam));
        }

        if (GodotObject.IsInstanceValid(_modalDetail) && troopOptions.Count > 0)
        {
            RefreshMultiOptionCards(troopOptions);
        }
    }

    private void ToggleMultiOption(int idx, (string Name, ImageTexture Icon, string Detail) _)
    {
        var cmd = Cmds[_cmdIndex];
        if (_modalMultiParams.Contains(idx))
        {
            if (cmd.Kind == CommandKind.SelectMajorTroop || _modalMultiParams.Count > 1) { _modalMultiParams.Remove(idx); }
        }
        else
        {
            if (cmd.Kind == CommandKind.SelectMajorTroop)
            {
                var city = _state.Cities.First(x => x.Id == _selected);
                var options = MajorTroopOptions(city);
                if (idx >= 0 && idx < options.Count
                    && _state.MajorTroops.Any(t => t.Faction == city.Owner && t.TroopCode == options[idx].Code))
                {
                    ShowNotice("주력병종 선택", $"{TroopName(options[idx].Code)}은(는) 이미 주력병종입니다.");
                    return;
                }

                var already = _state.MajorTroops.Count(t => t.Faction == city.Owner);
                var remaining = System.Math.Max(0, 2 - already);
                if (_modalMultiParams.Count >= remaining)
                {
                    ShowNotice("주력병종 선택", $"주력병종은 최대 2개까지 선택할 수 있습니다. 남은 선택 수: {remaining}");
                    return;
                }
            }

            _modalMultiParams.Add(idx);
        }

        RefreshMultiOptionCards(OptionList(cmd, _state.Cities.First(x => x.Id == _selected)));
    }

    private void RefreshMultiOptionCards(List<(string Name, ImageTexture Icon, string Detail)> options)
    {
        for (var i = 0; i < _optionCards.Count; i++)
        {
            _optionCards[i].AddThemeStyleboxOverride("panel", CardBox(_modalMultiParams.Contains(i)));
        }

        var selected = _modalMultiParams.OrderBy(i => i).Where(i => i >= 0 && i < options.Count)
            .Select(i => options[i].Name).ToList();
        var cmd = Cmds[_cmdIndex];
        var label = cmd.Kind == CommandKind.SelectMajorTroop ? "주력병종" : "자동 생산";
        var rate = cmd.Kind == CommandKind.AppointRecruitmentOfficer ? $" · {_autoRecruitRateParam}배" : "";
        if (GodotObject.IsInstanceValid(_modalDetail))
        {
            _modalDetail!.Text = selected.Count == 0 ? "▶  선택 없음" : $"▶  {label}: {string.Join(", ", selected)}{rate}";
        }
    }

    private static string PlainUiText(string value)
    {
        var text = value.Replace("[/color]", "", System.StringComparison.Ordinal);
        while (true)
        {
            var start = text.IndexOf("[color=", System.StringComparison.Ordinal);
            if (start < 0) { return text; }
            var end = text.IndexOf(']', start);
            if (end < 0) { return text; }
            text = text.Remove(start, end - start + 1);
        }
    }

    // 수행 장수 표(정렬·내부 스크롤) — 행 클릭 = 실행(컨펌창). ★ = 이 명령의 효율 능력치.
    private void BuildOfficerCards(CityId city, int cmdIndex)
    {
        Clear(_modalOfficers);
        var cmd = Cmds[cmdIndex];
        var cityData = _state.Cities.First(x => x.Id == city);
        // 임명/담당 지정은 상주 역할이라 다른 명령에 매인 장수도 지정 가능 — 주둔 장수 전체를 보인다.
        var isAppoint = cmd.Kind is CommandKind.AppointGovernor or CommandKind.AppointStrategist
            or CommandKind.AppointSecurityOfficer or CommandKind.AppointDomesticOfficer
            or CommandKind.AppointRecruitmentOfficer or CommandKind.AppointTrainingOfficer;
        var free = (isAppoint
                ? _state.GeneralsAt(city)
                : _state.GeneralsAt(city).Where(g => !OfficerUnavailable(g) && !_state.IsGeneralInField(g)))
            .OrderBy(g => g.Value).ToList();
        if (free.Count == 0)
        {
            _modalOfficers.AddChild(MakeLabel("(가능한 장수 없음)", 14, Parchment));
            return;
        }

        // 이 명령의 효율 능력치 컬럼(1=무, 2=지, 3=정) — 기본 정렬(내림차순)과 ★ 표시에 쓴다.
        var relevant = cmd.Kind is CommandKind.Research or CommandKind.CityStratagem or CommandKind.AppointStrategist ? 2
            : cmd.Kind is CommandKind.Train or CommandKind.AppointSecurityOfficer
                or CommandKind.AppointRecruitmentOfficer or CommandKind.AppointTrainingOfficer ? 1 : 3;

        var tree = new Tree
        {
            Columns = IsAutoOfficerCommand(cmd.Kind) ? 6 : 5,
            ColumnTitlesVisible = true,
            HideRoot = true,
            SelectMode = Tree.SelectModeEnum.Row,
            // 행 수만큼 키워 내부 스크롤을 없앤다(상한 초과 시에만 내부 스크롤).
            CustomMinimumSize = new Vector2(0, Mathf.Min(46 + free.Count * 34, 420)),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        tree.AddThemeFontOverride("font", _font);
        tree.AddThemeFontSizeOverride("font_size", 15);
        tree.AddThemeFontOverride("title_button_font", _font);
        tree.AddThemeFontSizeOverride("title_button_font_size", 14);
        tree.SetColumnTitle(0, "이름");
        tree.SetColumnExpand(0, true);
        tree.SetColumnExpandRatio(0, 3);
        foreach (var (col, t) in new[] { (1, "무"), (2, "지"), (3, "정") })
        {
            tree.SetColumnTitle(col, col == relevant ? t + "★" : t);
            tree.SetColumnExpand(col, false);
            tree.SetColumnCustomMinimumWidth(col, 52);
        }
        tree.SetColumnTitle(4, "현재 담당업무");
        tree.SetColumnExpand(4, true);
        tree.SetColumnExpandRatio(4, 3);
        if (IsAutoOfficerCommand(cmd.Kind))
        {
            tree.SetColumnTitle(5, "주 예상 효과");
            tree.SetColumnExpand(5, true);
            tree.SetColumnExpandRatio(5, 3);
        }

        var gens = free.Select(id => _state.Generals.First(g => g.Id == id)).ToList();
        System.Comparison<General> cmp = _offSortCol switch
        {
            0 => (a, b) => string.Compare(a.Name, b.Name, System.StringComparison.Ordinal),
            1 => (a, b) => a.Might.CompareTo(b.Might),
            2 => (a, b) => a.Intellect.CompareTo(b.Intellect),
            3 => (a, b) => a.Politics.CompareTo(b.Politics),
            _ => (a, b) => StatFor(b).CompareTo(StatFor(a)), // 기본: 효율 능력치 내림차순
        };
        gens.Sort(cmp);
        if (_offSortCol >= 0 && !_offSortAsc) { gens.Reverse(); }

        var root = tree.CreateItem();
        foreach (var g in gens)
        {
            var item = tree.CreateItem(root);
            var home = g.Region.Length > 0 && g.Region == cityData.Region ? " 🏠" : "";
            var roleMark = IsAutoOfficerCommand(cmd.Kind) ? "" : cmd.Kind switch
            {
                CommandKind.AppointGovernor when cityData.Governor == g.Id => " ◆현태수",
                CommandKind.AppointStrategist when cityData.Strategist == g.Id => " ◆현군사",
                _ => "",
            };
            item.SetText(0, g.Name + home + roleMark);
            item.SetText(1, g.Might.ToString());
            item.SetText(2, g.Intellect.ToString());
            item.SetText(3, g.Politics.ToString());
            if (IsAutoOfficerCommand(cmd.Kind))
            {
                item.SetText(5, OfficerWeeklyEffect(cmd.Kind, g, cityData));
            }
            item.SetText(4, CurrentDuty(g.Id));
            item.SetTooltipText(4, CurrentDuty(g.Id));
            if (cmd.Kind is not (CommandKind.AppointGovernor or CommandKind.AppointStrategist)
                && OfficerUnavailable(g.Id))
            {
                for (var column = 0; column < tree.Columns; column++)
                {
                    item.SetSelectable(column, false);
                    item.SetCustomColor(column, new Color(0.5f, 0.5f, 0.5f));
                }
            }

            item.SetMetadata(0, g.Id.Value);
            for (var col = 1; col <= 3; col++) { item.SetTextAlignment(col, HorizontalAlignment.Center); }
            if (IsAutoOfficerCommand(cmd.Kind))
            {
                item.SetTextAlignment(4, HorizontalAlignment.Center);
            }
        }

        tree.ItemSelected += () =>
        {
            var it = tree.GetSelected();
            if (it is null) { return; }
            var gid = new GeneralId(it.GetMetadata(0).AsInt32());
            tree.CallDeferred(Tree.MethodName.DeselectAll); // 컨펌 취소 후 같은 행 재클릭 가능하게
            AskExecute(city, cmdIndex, gid, _modalParam);
        };
        tree.ColumnTitleClicked += (col, _) =>
        {
            var c = (int)col;
            if (_offSortCol == c) { _offSortAsc = !_offSortAsc; }
            else { _offSortCol = c; _offSortAsc = c == 0; } // 이름은 오름차순, 능력치는 내림차순부터
            BuildOfficerCards(city, cmdIndex);
        };
        _modalOfficers.AddChild(tree);
        return;

        int StatFor(General g) => relevant switch { 1 => g.Might, 2 => g.Intellect, _ => g.Politics };
    }

    private StyleBoxFlat CardBox(bool selected, bool hover = false) => selected
        ? Frame(AccentFill, GoldBright, 4, 9, 9)
        : hover ? Frame(InkHover, GoldBright, 2, 9, 9) : Frame(InkSoft, Gold, 1, 9, 9);

    private Control GoldRule()
    {
        var rule = new HSeparator();
        rule.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = new Color(Gold, 0.5f), ContentMarginTop = 1, ContentMarginBottom = 1 });
        return rule;
    }

    // 그 도시 재임 태수의 내정 스킬 버킷 합(미리보기용 — Core CommandService.GovernorBucket과 같은 규칙).
    private int GovernorAdminBucket(City city, string bucket)
    {
        if (city.Governor is not { } gid) { return 0; }
        var posting = _state.PostingOf(gid);
        if (posting is null || posting.Location != city.Id || posting.Faction != city.Owner) { return 0; }
        var gov = _state.Generals.FirstOrDefault(g => g.Id == gid);
        if (gov is null) { return 0; }

        var sum = 0;
        foreach (var held in gov.AdminPassives ?? [])
        {
            var def = _adminSkills.FirstOrDefault(a => a.Code == held.Code);
            if (def is not null && def.Bucket == bucket) { sum += def.AmountAtTier(held.Tier); }
        }

        return sum;
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
        _ => "",
    };

    private void AskExecute(CityId city, int cmdIndex, GeneralId general, int p, HexCoord? plot = null)
    {
        var cmd = Cmds[cmdIndex];
        if (_disabledOptions.Contains(p))
        {
            return;
        }

        // 건설은 위치를 지정해야 한다 — 아직 타일을 안 골랐으면 배치 모드로 넘어간다(고스트가 커서를 따라감).
        if (cmd.Kind == CommandKind.Build && plot is null)
        {
            BeginPlacement(city, cmdIndex, general, p);
            return;
        }

        var troopCode = cmd.Param switch
        {
            "troop" when cmd.Kind == CommandKind.Research && p == 0 => FactionResearch.CommandTroopsCode,
            "troop" when cmd.Kind == CommandKind.Research => _troops[p - 1].Code,
            "troop" => _troops[p].Code,
            "wall" => FactionResearch.WallCode,
            "garrison" => GarrisonAt(city, p)?.TroopCode ?? "",
            _ => "",
        };
        FactionId? targetFaction = null;
        if (cmd.Param == "faction")
        {
            var factions = DiplomacyTargets(cmd.Kind, _state.Cities.First(c => c.Id == city));
            if (p < 0 || p >= factions.Count)
            {
                ShowNotice("외교 실패", "대상 세력을 선택해야 합니다.");
                return;
            }

            targetFaction = factions[p].Id;
        }
        if (cmd.Kind == CommandKind.AppointRecruitmentOfficer)
        {
            var autoOptions = AutoRecruitTroopOptions();
            var selected = _modalMultiParams.OrderBy(i => i)
                .Where(i => i >= 0 && i < autoOptions.Count)
                .Select(i => autoOptions[i].Code).ToList();
            if (selected.Count == 0 && p >= 0 && p < autoOptions.Count) { selected.Add(autoOptions[p].Code); }
            troopCode = selected.Count == 0 ? _cb.AutoRecruitDefaultTroopCode : string.Join(',', selected);
        }
        var traineePool = cmd.Param == "garrison" && (GarrisonAt(city, p)?.Trainee ?? false);
        var facility = cmd.Param switch
        {
            "stratagem" => Strats[p].Code,
            "facility" => Facilities[p].Code,
            "repairable" => Repairables[p].Code,
            _ => "",
        };
        var value = cmd.Kind == CommandKind.AppointRecruitmentOfficer
            ? _autoRecruitRateParam
            : cmd.Param == "tax" ? p * 10 : 0;

        CityId? target = null;
        var extra = "";
        var confirmOfficer = general;
        string? confirmOfficerLine = null;
        if (cmd.Kind is CommandKind.Recruit or CommandKind.Conscript)
        {
            // 발행 시점 규칙(IssueRecruit)과 같은 식으로 예상치를 계산해 보여준다.
            var cityData = _state.Cities.First(c => c.Id == city);
            var caster = _state.Generals.First(g => g.Id == general);
            var tmpl = _troops[p];
            var eff = CommandEfficiency.Effective(caster, null, cityData, cmd.Kind, _cb);
            var capPercent = cmd.Kind == CommandKind.Recruit ? _cb.RecruitPopCapPercent : _cb.ConscriptPopCapPercent;
            var byPolitics = CommandEfficiency.RecruitTroops(cityData.Population, capPercent, eff);
            var amountBonus = GovernorAdminBucket(cityData, "recruit_amount"); // 모병관
            if (amountBonus > 0) { byPolitics = byPolitics * (100 + amountBonus) / 100; }
            var expect = System.Math.Min(byPolitics, cityData.Ore);
            if (tmpl.Class == TroopClass.Cavalry) { expect = System.Math.Min(expect, cityData.Horses * 3); }
            if (tmpl.Class == TroopClass.Elephant) { expect = System.Math.Min(expect, cityData.Elephants * 1000); }
            var costCut = GovernorAdminBucket(cityData, "recruit_cost"); // 인망
            var popCost = costCut > 0 ? expect - (expect * costCut / 100) : expect;
            extra = $"\n예상 모집 {expect}명 · 광석 −{expect} · 인구 −{popCost}";
            if (tmpl.Class == TroopClass.Cavalry) { extra += $" · 말 −{(expect + 2) / 3}"; }
            if (tmpl.Class == TroopClass.Elephant) { extra += $" · 코끼리 −{(expect + 999) / 1000}"; }
            if (cmd.Kind == CommandKind.Conscript) { extra += $" · 치안 −{expect / 1000 * _cb.ConscriptSecurityDropPer1000}"; }
        }

        if (cmd.Kind == CommandKind.Research && cmd.Param == "troop")
        {
            var cityData = _state.Cities.First(c => c.Id == city);
            var caster = _state.Generals.First(g => g.Id == general);
            var days = System.Math.Max(_cb.ResearchBaseDays - System.Math.Clamp((caster.Intellect - 50) / 5, 0, 10), 1);
            var active = _state.Commands.FirstOrDefault(c => c.Kind == CommandKind.Research
                && _state.Cities.FirstOrDefault(x => x.Id == c.City)?.Owner == cityData.Owner);
            if (troopCode == FactionResearch.CommandTroopsCode)
            {
                var level = _state.ResearchOf(cityData.Owner, troopCode);
                var next = System.Math.Min(level + 1, 10);
                var cost = level >= 10 ? 0 : CommandEfficiency.CommandTroopResearchCost(next);
                extra = "\n세력 통솔 병력"
                    + $"\nLv.{level} → Lv.{next}/10"
                    + (level >= 10
                        ? "\n※ 이미 최대 단계입니다"
                        : $"\n편성 상한 {CommandEfficiency.CommandTroopDeployLimit(level, _cb)}명 → {CommandEfficiency.CommandTroopDeployLimit(next, _cb)}명"
                            + $"\n비용 {cost}금"
                            + $"\n[소요 {days}일]")
                    + (active is null ? "" : $"\n※ 이미 연구가 진행 중입니다: {TroopName(active.TroopCode)} · 남은 {System.Math.Max(0, active.CompletionDay - _state.Day)}일")
                    + (level < 10 && !ResearchFundingCanCover(cityData, cost) ? "\n※ 연구비 분담 도시의 금이 부족합니다" : "");
            }
            else
            {
                var level = _state.ResearchOf(cityData.Owner, troopCode);
                var maxLevel = ResearchMaxLevelFor(cityData.Owner, troopCode);
                var next = System.Math.Min(level + 1, maxLevel);
                var cost = level >= maxLevel ? 0 : CommandEfficiency.ResearchCost(next, _cb);
                extra = $"\n{(_state.IsMajorTroop(cityData.Owner, troopCode) ? "주력 전투 교리" : "세력 전투 교리")}"
                    + $"\nLv.{level} → Lv.{next}/{maxLevel}"
                    + (level >= maxLevel
                        ? "\n※ 이미 최대 단계입니다"
                        : $"\n보정 +{ResearchCurve.Bonus(level)} → +{ResearchCurve.Bonus(next)}"
                            + $"\n비용 {cost}금"
                            + $"\n[소요 {days}일]")
                    + (active is null ? "" : $"\n※ 이미 연구가 진행 중입니다: {TroopName(active.TroopCode)} · 남은 {System.Math.Max(0, active.CompletionDay - _state.Day)}일")
                    + (level < maxLevel && !ResearchFundingCanCover(cityData, cost) ? "\n※ 연구비 분담 도시의 금이 부족합니다" : "");
            }
        }

        if (cmd.Kind == CommandKind.Research && cmd.Param == "wall")
        {
            var cityData = _state.Cities.First(c => c.Id == city);
            var caster = _state.Generals.First(g => g.Id == general);
            var level = cityData.WallLevel;
            var next = System.Math.Min(level + 1, _cb.WallResearchMaxLevel);
            var cost = level >= _cb.WallResearchMaxLevel ? 0 : _cb.WallResearchCostPerLevel * next;
            var days = System.Math.Max(_cb.ResearchBaseDays - System.Math.Clamp((caster.Intellect - 50) / 5, 0, 10), 1);
            var active = _state.Commands.FirstOrDefault(c => c.Kind == CommandKind.Research
                && _state.Cities.FirstOrDefault(x => x.Id == c.City)?.Owner == cityData.Owner);
            var currentMax = CastleWall.Max(cityData.Castle, _balance, level);
            var nextMax = CastleWall.Max(cityData.Castle, _balance, next);
            extra = $"\n세력 성벽 강화"
                + $"\n현재 성벽 최대 {currentMax} → {nextMax}"
                + (level >= _cb.WallResearchMaxLevel
                    ? "\n※ 이미 최대 단계입니다"
                    : $"\n비용 {cost}금"
                        + $"\n[소요 {days}일]")
                + "\n완료 시 이 도시의 최대 성벽만 상승합니다."
                + (active is null ? "" : $"\n※ 이미 연구가 진행 중입니다: {TroopName(active.TroopCode)} · 남은 {System.Math.Max(0, active.CompletionDay - _state.Day)}일")
                + (level < _cb.WallResearchMaxLevel && !ResearchFundingCanCover(cityData, cost) ? "\n※ 연구비 분담 도시의 금이 부족합니다" : "");
        }

        if (cmd.Param == "stratagem")
        {
            var enemy = _stratTarget is { } tc
                ? _state.Cities.FirstOrDefault(c => c.Id == tc && c.Owner != Player)
                : null;
            enemy ??= _state.Cities.FirstOrDefault(c => c.Owner != Player);
            if (enemy is null) { return; }
            target = enemy.Id;
            var caster = _state.Generals.First(g => g.Id == general);
            var days = CityStratagems.Days(_state.Cities.First(c => c.Id == city).Position, enemy.Position, _cb);
            var defInt = enemy.Governor is { } gid ? _state.Generals.FirstOrDefault(g => g.Id == gid)?.Intellect : null;
            var odds = CityStratagems.SuccessPercent(caster.Intellect, defInt);
            var origin = _state.Cities.First(c => c.Id == city);
            var strategist = origin.Strategist is { } sid ? _state.Generals.FirstOrDefault(g => g.Id == sid) : null;
            if (strategist is not null)
            {
                confirmOfficer = strategist.Id;
                confirmOfficerLine = $"군사 {strategist.Name} 예측: "
                    + (DiplomacyRules.AdvisorPredictsSuccess(odds, strategist.Intellect, new SeededRandomSource(_state.Day + caster.Id.Value * 31 + enemy.Id.Value * 17))
                        ? "성공할 듯합니다"
                        : "실패할 듯합니다");
            }
            extra = $"\n대상 {enemy.Name} · 소요 {days}일";
        }

        if ((cmd.Kind is CommandKind.FormAlliance or CommandKind.BreakAlliance) && targetFaction is { } tf)
        {
            var cityData = _state.Cities.First(c => c.Id == city);
            var targetFactionName = _state.Factions.FirstOrDefault(f => f.Id == tf)?.Name ?? $"세력 {tf.Value}";
            var targetCity = _state.Cities.Where(c => c.Owner == tf)
                .OrderBy(c => c.Position.Distance(cityData.Position))
                .ThenBy(c => c.Id.Value)
                .FirstOrDefault();
            var actor = _state.Generals.First(g => g.Id == general);
            if (cmd.Kind == CommandKind.FormAlliance)
            {
                var days = targetCity is null ? _cb.CommandDays : CityStratagems.Days(cityData.Position, targetCity.Position, _cb);
                var odds = DiplomacyRules.AllianceSuccessPercent(actor.Politics);
                var strategist = cityData.Strategist is { } sid ? _state.Generals.FirstOrDefault(g => g.Id == sid) : null;
                var prediction = strategist is null
                    ? "군사 없음 → 성공 여부 예측 불가"
                    : $"군사 {strategist.Name} 예측: {(DiplomacyRules.AdvisorPredictsSuccess(odds, strategist.Intellect, new SeededRandomSource(_state.Day + actor.Id.Value * 31 + tf.Value * 17)) ? "성공할 듯합니다" : "실패할 듯합니다")} · 예측 신뢰도 {strategist.Intellect}%";
                extra = $"\n대상 {targetFactionName}"
                    + $"\n비용 {_cb.AllianceGoldCost}금 · 소요 {days}일"
                    + $"\n동맹 성공 확률 {odds}%"
                    + $"\n{prediction}";
            }
            else
            {
                extra = $"\n대상 {targetFactionName}\n즉시 동맹을 파기합니다.";
            }
        }

        if (cmd.Kind == CommandKind.Explore)
        {
            var actor = _state.Generals.First(g => g.Id == general);
            var localClanOdds = 10 + System.Math.Clamp(actor.Politics, 0, 100) / 10;
            var noneOdds = 100 - 1 - 1 - localClanOdds - 5;
            extra = $"\n소요 {_cb.CommandDays}일"
                + $"\n정치 {actor.Politics} 기준 확률"
                + $"\n신수 1% · 고대유물 1% · 지방호족 {localClanOdds}% · 소문/단서 5% · 없음 {noneOdds}%";
        }

        if (cmd.Kind == CommandKind.Build)
        {
            var c = _state.Cities.First(x => x.Id == city);
            var code = Facilities[p].Code;
            var cost = code switch
            {
                "paddy" => _cb.BuildCostPaddy,
                "farm" => _cb.BuildCostFarm,
                "village" => _cb.BuildCostVillage,
                _ => _cb.BuildCostWorkshop,
            };
            var manpower = $" · 인력 {_cb.BuildSiteHp}(인구)";
            var popWarn = c.Population <= _cb.BuildSiteHp ? $"\n※ 인구가 부족합니다(인력 {_cb.BuildSiteHp} 초과 필요)" : "";
            if (code == "workshop")
            {
                extra = $"\n비용 {cost}금 · {_cb.BuildDays}일 · 성별 1개{manpower}"
                    + (c.Workshop ? "\n※ 이미 공방이 있어 지을 수 없습니다" : "") + popWarn;
            }
            else
            {
                var used = c.Paddies + c.Farms + c.Villages
                    + c.RuinedPaddies + c.RuinedFarms + c.RuinedVillages;
                var max = CommandEfficiency.BuildSlots(c.Castle, _cb);
                extra = $"\n비용 {cost}금 · {_cb.BuildDays}일 · 슬롯 {used}/{max}{manpower}"
                    + (used >= max ? "\n※ 슬롯이 가득 찼습니다(잔해는 수리로 복구)" : "") + popWarn;
            }
        }

        if (cmd.Kind == CommandKind.AppointGovernor)
        {
            var gov = _state.Generals.First(g => g.Id == general);
            var counter = System.Math.Clamp(100 + (gov.Might - 60), 50, 150);
            var incomeOk = gov.Politics >= 60;
            extra = $"\n정치 {gov.Politics} → 수입 {(incomeOk ? "정상(정치로 세율 증폭)" : "급감(정치 60 미만)")}"
                + $"\n무력 {gov.Might} → 성 반격 ×{counter / 100.0:0.0#}";
        }

        if (cmd.Kind is CommandKind.AppointSecurityOfficer or CommandKind.AppointDomesticOfficer
            or CommandKind.AppointRecruitmentOfficer or CommandKind.AppointTrainingOfficer)
        {
            var officer = _state.Generals.First(g => g.Id == general);
            extra = cmd.Kind switch
            {
                CommandKind.AppointSecurityOfficer => $"\n무력 {officer.Might} → 주 치안 {(officer.Might < 60 ? "+0" : officer.Might < 80 ? "+1" : officer.Might < 100 ? "+2" : "+3")}",
                CommandKind.AppointDomesticOfficer => $"\n정치 {officer.Politics} → 주 금 +{WeeklyIncomeAmount(ApplyLowSecurityOutputPenalty(_cb.AutoDomesticGoldBase + officer.Politics * _cb.AutoDomesticGoldPoliticsMultiplier, _state.Cities.First(c => c.Id == city).Security))}"
                    + $"\n주 군량 +{WeeklyIncomeAmount(ApplyLowSecurityOutputPenalty(_cb.AutoDomesticProvisionsBase + officer.Politics * _cb.AutoDomesticProvisionsPoliticsMultiplier, _state.Cities.First(c => c.Id == city).Security))}",
                CommandKind.AppointRecruitmentOfficer => $"\n무력 {officer.Might} → 주 병력 +{AutoRecruitWeeklyTroopsFor(officer, _state.Cities.First(c => c.Id == city), _autoRecruitRateParam)}"
                    + $"\n선택 병종 {AutoRecruitTroopNames(troopCode)}"
                    + $"\n생산 비율 {_autoRecruitRateParam}배"
                    + $"\n주 예상 비용 {AutoRecruitWeeklyCostFor(officer, troopCode, _state.Cities.First(c => c.Id == city), _autoRecruitRateParam)}금 · 치안 {_cb.AutoRecruitSecurityDeltaForRate(_autoRecruitRateParam)}"
                    + "\n도시 금 부족 시 생산 없음",
                CommandKind.AppointTrainingOfficer => $"\n무력 {officer.Might} → 7일 훈련도 +{ApplyLowSecurityOutputPenalty(System.Math.Max(1, OfficerMightTier(officer.Might) + 1), _state.Cities.First(c => c.Id == city).Security)}",
                _ => "",
            };
        }

        var replacingOfficer = IsAutoOfficerCommand(cmd.Kind)
            ? CurrentOfficerAssignment(general) is { } current
                && (current.City != city || current.Kind != cmd.Kind)
            : false;
        var researchFunding = cmd.Kind == CommandKind.Research && _researchFundingRatios.Count > 0
            ? CurrentResearchFundingShares()
            : null;
        var request = new CommandRequest(city, cmd.Kind, general, Value: value, Facility: facility,
            TroopCode: troopCode, TargetCity: target, TargetFaction: targetFaction, TraineePool: traineePool, Plot: plot,
            ReplaceOfficerAssignment: replacingOfficer, ResearchFunding: researchFunding);
        var gName = _state.Generals.First(g => g.Id == general).Name;
        var pLabel = cmd.Param switch
        {
            "troop" when troopCode == FactionResearch.CommandTroopsCode => " · 통솔 병력",
            "troop" => $" · {_troops.FirstOrDefault(t => t.Code == troopCode)?.Name ?? troopCode}",
            "garrison" => $" · {(_troops.FirstOrDefault(t => t.Code == troopCode)?.Name ?? troopCode)}{(traineePool ? "(신병)" : "")}",
            "tax" => $" · {value}%",
            "facility" => $" · {Facilities[p].Label}",
            "repairable" => $" · {Repairables[p].Label}",
            "stratagem" => $" · {Strats[p].Label}",
            "faction" => targetFaction is { } diplomTarget ? $" · {_state.Factions.FirstOrDefault(f => f.Id == diplomTarget)?.Name ?? $"세력 {diplomTarget.Value}"}" : "",
            _ => "",
        };
        if (cmd.Kind == CommandKind.AppointRecruitmentOfficer)
        {
            pLabel = $" · {AutoRecruitTroopNames(troopCode)}";
        }
        var confirmTitle = replacingOfficer ? "담당 교체 확인" : "명령 확인";
        var replaceText = replacingOfficer && CurrentOfficerAssignment(general) is { } oldRole
            ? $"\n\n※ {gName}은 현재 {oldRole.CityName}의 {KindName(oldRole.Kind)}입니다.\n동의하면 기존 담당에서 해제하고 {cmd.Label}(으)로 교체합니다."
            : "";
        var confirmMessage =
            $"{_state.Cities.First(c => c.Id == city).Name} — {cmd.Label}{pLabel}{extra}\n수행 장수: {gName}{replaceText}"
            + (cmd.Kind is CommandKind.AppointGovernor or CommandKind.AppointStrategist || IsAutoOfficerCommand(cmd.Kind)
                ? "" : DutyReleaseNotice(general)) + "\n\n실행하시겠습니까?";
        void ExecuteConfirmed()
        {
            if (cmd.Kind is not (CommandKind.AppointGovernor or CommandKind.AppointStrategist) && OfficerUnavailable(general))
            {
                ShowNotice("명령 불가", "다른 업무 또는 출전 예약 중인 장수입니다.");
                return;
            }
            var r = _commander.Issue(_state, request);
            Dbg($"UI issue {cmd.Label}{pLabel} city={city.Value} gen={general.Value} ok={r.Ok} err={r.Error ?? "-"}");
            if (r.Ok) { _state = r.State; }
            _log.Text = r.Ok ? $"발행: {cmd.Label}{pLabel} — {gName}" : $"실패: {r.Error}";
            if (r.Ok)
            {
                var category = cmd.Kind is CommandKind.FormAlliance or CommandKind.BreakAlliance ? "외교" : "내정";
                Report($"[{category}] {_state.Cities.First(c => c.Id == city).Name}에서 {gName} 장수가 {cmd.Label}{pLabel}을(를) 맡았습니다.", Parchment);
            }
            else { ShowNotice("명령 실패", r.Error ?? "조건에 맞지 않아 실행할 수 없습니다."); }
            CloseModal();
            SelectCity(city);
            Redraw(_log.Text);
        }

        if ((cmd.Kind == CommandKind.Research && cmd.Param == "troop")
            || cmd.Kind == CommandKind.CityStratagem
            || cmd.Kind is CommandKind.FormAlliance or CommandKind.BreakAlliance
            || cmd.Kind is CommandKind.AppointGovernor or CommandKind.AppointStrategist
            || IsAutoOfficerCommand(cmd.Kind)
            || cmd.Kind == CommandKind.Explore)
        {
            ShowOfficerConfirm(confirmTitle, confirmMessage, confirmOfficer, ExecuteConfirmed, confirmOfficerLine);
        }
        else
        {
            ShowConfirm(confirmTitle, confirmMessage, ExecuteConfirmed);
        }
    }

    // 시설 코드 → 지형 모델 종류(고스트·완성 모델 로드용).
    private static TerrainType FacilityTerrain(string code) => code switch
    {
        "paddy" => TerrainType.Paddy,
        "farm" => TerrainType.Farm,
        "village" => TerrainType.Village1,
        _ => TerrainType.Workshop,
    };

    // 그 타일에 건설된 시설 코드(없으면 null). 배치 목록에서 찾는다.
    private string? FacilityAt(HexCoord h) => _state.Placements.FirstOrDefault(p => p.Plot == h)?.Code;

    private FacilityPlacement? FacilityPlacementAt(HexCoord h)
        => _state.Placements.FirstOrDefault(p => p.Plot == h);

    private ProductionOperation? ProductionAt(HexCoord h)
        => _state.ProductionOps.FirstOrDefault(p => p.Target == h);

    private CityCommand? PendingFacilityBuildAt(HexCoord h)
        => _state.Commands.FirstOrDefault(c => c.Kind == CommandKind.Build && c.Plot == h);

    private static string FacilityName(string code) => code switch
    {
        "paddy" => "논",
        "farm" => "밭",
        "village" => "마을",
        _ => "공방",
    };

    private ImageTexture FacilityIcon(string code) => code switch
    {
        "village" => Icon(Sym.Coin),
        "workshop" => Icon(Sym.Book),
        _ => Icon(Sym.Grain),
    };

    private string FacilityEffectText(string code, int hitPoints = FacilityHealth.Level1)
    {
        var current = FacilityEffectValue(code, hitPoints);
        var next = FacilityHealth.NextTier(hitPoints);
        var nextText = next is { } n && code != "workshop" ? $" → +{FacilityEffectValue(code, n)}" : "";
        return code switch
        {
            "paddy" => $"월 군량 +{current}{nextText}",
            "farm" => $"월 군량 +{current}{nextText}",
            "village" => $"월 금 +{current}{nextText}",
            "workshop" => $"성벽 수리 +{_cb.WallRepairWorkshopBonus}%\n공성 병기 생산 기반",
            _ => "",
        };
    }

    private int FacilityEffectValue(string code, int hitPoints) => code switch
    {
        "paddy" => _balance.PaddyProvisions * FacilityHealth.OutputMultiplier(hitPoints),
        "farm" => _balance.FarmProvisions * FacilityHealth.OutputMultiplier(hitPoints),
        "village" => _balance.VillageGold * FacilityHealth.OutputMultiplier(hitPoints),
        _ => 0,
    };

    private int FacilityBuildCost(string code) => code switch
    {
        "paddy" => _cb.BuildCostPaddy,
        "farm" => _cb.BuildCostFarm,
        "village" => _cb.BuildCostVillage,
        "workshop" => _cb.BuildCostWorkshop,
        _ => 0,
    };

    private void OpenFacilityUpgradeModal(FacilityPlacement placement)
    {
        CloseModal();
        var city = _state.Cities.FirstOrDefault(c => c.Id == placement.City);
        if (city is null) { return; }

        var vp = GetViewport().GetVisibleRect().Size;
        var mw = Mathf.Clamp(vp.X * 0.44f, 430f, 620f);
        var mh = Mathf.Clamp(vp.Y * 0.68f, 340f, 600f);
        var box = DeployScaffold(mw, out var scroll, out var panel);
        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var title = MakeLabel($"◈  {FacilityName(placement.Code)} 업그레이드   《 {city.Name} 》  ⠿", 22, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        titleRow.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(40, 34);
        close.Pressed += CloseModal;
        titleRow.AddChild(close);
        box.AddChild(GoldRule());

        var next = FacilityHealth.NextTier(placement.HitPoints);
        var cost = FacilityBuildCost(placement.Code);
        var info = next is { } n
            ? $"체력 {placement.HitPoints} → {n}\n비용 {cost}금 · 소요 {_cb.BuildDays}일"
            : $"체력 {placement.HitPoints} · 최대 단계";
        var infoLabel = MakeLabel(info, 15, Parchment);
        infoLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(infoLabel);
        box.AddChild(MakeLabel("수행 장수 (행 클릭 = 실행 · 상단 눌러 정렬)", 17, GoldBright));

        var holder = new VBoxContainer();
        box.AddChild(holder);
        BuildFacilityUpgradeOfficerCards(holder, placement);
        scroll.CustomMinimumSize = new Vector2(mw, Mathf.Min(box.GetCombinedMinimumSize().Y, mh));
        CenterAndDrag(panel, titleRow, mw, mh, box);
    }

    private void BuildFacilityUpgradeOfficerCards(VBoxContainer holder, FacilityPlacement placement)
    {
        Clear(holder);
        var city = _state.Cities.First(c => c.Id == placement.City);
        var free = _state.GeneralsAt(city.Id).Where(g => !OfficerUnavailable(g)).OrderBy(g => g.Value).ToList();
        if (free.Count == 0)
        {
            holder.AddChild(MakeLabel("(가능한 장수 없음)", 14, Parchment));
            return;
        }

        var tree = new Tree
        {
            Columns = 5,
            ColumnTitlesVisible = true,
            HideRoot = true,
            SelectMode = Tree.SelectModeEnum.Row,
            CustomMinimumSize = new Vector2(0, Mathf.Min(46 + free.Count * 34, 420)),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        tree.AddThemeFontOverride("font", _font);
        tree.AddThemeFontSizeOverride("font_size", 15);
        tree.AddThemeFontOverride("title_button_font", _font);
        tree.AddThemeFontSizeOverride("title_button_font_size", 14);
        tree.SetColumnTitle(0, "이름");
        tree.SetColumnExpand(0, true);
        tree.SetColumnExpandRatio(0, 3);
        foreach (var (col, t) in new[] { (1, "무"), (2, "지"), (3, "정★") })
        {
            tree.SetColumnTitle(col, t);
            tree.SetColumnExpand(col, false);
            tree.SetColumnCustomMinimumWidth(col, 52);
        }

        tree.SetColumnTitle(4, "현재 담당업무");
        tree.SetColumnExpand(4, true);
        var gens = free.Select(id => _state.Generals.First(g => g.Id == id)).OrderByDescending(g => g.Politics).ToList();
        var root = tree.CreateItem();
        foreach (var g in gens)
        {
            var item = tree.CreateItem(root);
            var home = g.Region.Length > 0 && g.Region == city.Region ? " 🏠" : "";
            item.SetText(0, g.Name + home);
            item.SetText(1, g.Might.ToString());
            item.SetText(2, g.Intellect.ToString());
            item.SetText(3, g.Politics.ToString());
            item.SetText(4, CurrentDuty(g.Id));
            item.SetTooltipText(4, CurrentDuty(g.Id));
            item.SetMetadata(0, g.Id.Value);
            for (var col = 1; col <= 3; col++) { item.SetTextAlignment(col, HorizontalAlignment.Center); }
        }

        tree.ItemSelected += () =>
        {
            var it = tree.GetSelected();
            if (it is null) { return; }
            var gid = new GeneralId(it.GetMetadata(0).AsInt32());
            tree.CallDeferred(Tree.MethodName.DeselectAll);
            AskFacilityUpgrade(placement, gid);
        };
        holder.AddChild(tree);
    }

    private void AskFacilityUpgrade(FacilityPlacement placement, GeneralId general)
    {
        var city = _state.Cities.First(c => c.Id == placement.City);
        var gName = _state.Generals.First(g => g.Id == general).Name;
        var next = FacilityHealth.NextTier(placement.HitPoints);
        var cost = FacilityBuildCost(placement.Code);
        var detail = next is { } n
            ? $"{FacilityName(placement.Code)} 체력 {placement.HitPoints} → {n}\n[소요 {_cb.BuildDays}일] 비용 {cost}금"
            : $"{FacilityName(placement.Code)} 체력 {placement.HitPoints}\n※ 이미 최대 단계입니다";
        ShowConfirm("업그레이드 확인",
            $"{city.Name} — {detail}\n수행 장수: {gName}{DutyReleaseNotice(general)}\n\n실행하시겠습니까?",
            () =>
            {
                var request = new CommandRequest(city.Id, CommandKind.Upgrade, general, Plot: placement.Plot);
                var r = _commander.Issue(_state, request);
                Dbg($"UI issue 시설 업그레이드 city={city.Id.Value} plot=({placement.Plot.Q},{placement.Plot.R}) gen={general.Value} ok={r.Ok} err={r.Error ?? "-"}");
                if (r.Ok) { _state = r.State; }
                _log.Text = r.Ok ? $"발행: {FacilityName(placement.Code)} 업그레이드 — {gName}" : $"실패: {r.Error}";
                if (r.Ok) { Report($"[내정] {city.Name}에서 {gName} 장수가 {FacilityName(placement.Code)} 업그레이드를 맡았습니다.", Parchment); }
                else { ShowNotice("업그레이드 실패", r.Error ?? "조건에 맞지 않아 실행할 수 없습니다."); }
                CloseModal();
                ShowMapInfo(placement.Plot);
                Redraw(_log.Text);
            });
    }

    private (CityId City, string CityName, CommandKind Kind)? CurrentOfficerAssignment(GeneralId general)
    {
        foreach (var c in _state.Cities)
        {
            if (c.SecurityOfficer == general) { return (c.Id, c.Name, CommandKind.AppointSecurityOfficer); }
            if (c.DomesticOfficer == general) { return (c.Id, c.Name, CommandKind.AppointDomesticOfficer); }
            if (c.RecruitmentOfficer == general) { return (c.Id, c.Name, CommandKind.AppointRecruitmentOfficer); }
            if (c.TrainingOfficer == general) { return (c.Id, c.Name, CommandKind.AppointTrainingOfficer); }
        }

        return null;
    }

    private GeneralId? CurrentOfficerForRole(City city, CommandKind kind) => kind switch
    {
        CommandKind.AppointSecurityOfficer => city.SecurityOfficer,
        CommandKind.AppointDomesticOfficer => city.DomesticOfficer,
        CommandKind.AppointRecruitmentOfficer => city.RecruitmentOfficer,
        CommandKind.AppointTrainingOfficer => city.TrainingOfficer,
        _ => null,
    };

    private static City ClearOfficerRole(City city, CommandKind kind) => kind switch
    {
        CommandKind.AppointSecurityOfficer => city with { SecurityOfficer = null },
        CommandKind.AppointDomesticOfficer => city with { DomesticOfficer = null },
        CommandKind.AppointRecruitmentOfficer => city with
        {
            RecruitmentOfficer = null,
            AutoRecruitTroopCode = string.Empty,
            AutoRecruitTroopCodes = string.Empty,
        },
        CommandKind.AppointTrainingOfficer => city with { TrainingOfficer = null },
        _ => city,
    };

    private void AddClearOfficerButton(VBoxContainer box, City city, CommandKind kind)
    {
        var current = CurrentOfficerForRole(city, kind);
        if (current is null) { return; }

        var name = OfficerName(current) ?? "현재 담당자";
        var row = new HBoxContainer();
        row.AddChild(MakeLabel($"현재 {KindName(kind)}: {name}", 14, Parchment));
        var clear = MakeButton("담당 해제");
        clear.CustomMinimumSize = new Vector2(110, 30);
        clear.Pressed += () =>
        {
            ShowConfirm("담당 해제 확인",
                $"{city.Name}의 {KindName(kind)}에서 {name}을(를) 해제하시겠습니까?",
                () =>
                {
                    _state = _state with
                    {
                        Cities = _state.Cities.Select(c => c.Id == city.Id ? ClearOfficerRole(c, kind) : c).ToList(),
                    };
                    Report($"[내정] {city.Name}의 {KindName(kind)}에서 {name} 장수를 해제했습니다.", Parchment);
                    CloseModal();
                    SelectCity(city.Id);
                    Redraw($"{KindName(kind)} 해제");
                });
        };
        row.AddChild(clear);
        box.AddChild(row);
    }

    private static int OfficerMightTier(int might) => might switch
    {
        < 60 => 0,
        < 80 => 1,
        < 100 => 2,
        _ => 3,
    };

    private string OfficerWeeklyEffect(CommandKind kind, General officer, City city) => kind switch
    {
        CommandKind.AppointSecurityOfficer => $"치안 +{OfficerMightTier(officer.Might)}",
        CommandKind.AppointDomesticOfficer => $"금 +{WeeklyIncomeAmount(ApplyLowSecurityOutputPenalty(_cb.AutoDomesticGoldBase + officer.Politics * _cb.AutoDomesticGoldPoliticsMultiplier, city.Security))} / 군량 +{WeeklyIncomeAmount(ApplyLowSecurityOutputPenalty(_cb.AutoDomesticProvisionsBase + officer.Politics * _cb.AutoDomesticProvisionsPoliticsMultiplier, city.Security))}",
        CommandKind.AppointRecruitmentOfficer => $"병력 +{AutoRecruitWeeklyTroopsFor(officer, city)} / 치안 {_cb.AutoRecruitSecurityDeltaForRate(city.AutoRecruitRate)}",
        CommandKind.AppointTrainingOfficer => $"7일 훈련도 +{ApplyLowSecurityOutputPenalty(System.Math.Max(1, OfficerMightTier(officer.Might) + 1), city.Security)}",
        _ => "",
    };

    private string SecurityWeeklySummary(City city)
    {
        General? Officer(GeneralId? id) => id is { } gid
            && (_state.Assignments.Count == 0 || _state.PostingOf(gid) is { } posting
                && posting.Location == city.Id && posting.Faction == city.Owner)
                ? _state.Generals.FirstOrDefault(g => g.Id == gid) : null;
        var security = Officer(city.SecurityOfficer);
        var recruiter = Officer(city.RecruitmentOfficer);
        var recovery = security is null ? _cb.AutoSecurityNoOfficerDelta : OfficerMightTier(security.Might);
        var burden = recruiter is null ? 0 : _cb.AutoRecruitSecurityDeltaForRate(city.AutoRecruitRate);
        return $"주 치안 {recovery + burden:+0;-0;0} = {(security is null ? "공석" : security.Name)} {recovery:+0;-0;0}"
            + (recruiter is null ? "" : $" / 병력 담당 {recruiter.Name} {burden:+0;-0;0}");
    }

    private int AutoRecruitWeeklyCostFor(General officer, string troopCodes, City? city = null, int? rateOverride = null)
    {
        var codes = troopCodes.Split(',', System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries);
        if (codes.Length == 0) { codes = [_cb.AutoRecruitDefaultTroopCode]; }
        var rate = CommandBalance.AutoRecruitRate(rateOverride ?? city?.AutoRecruitRate ?? 1);
        var total = (_cb.AutoRecruitTroopsBase + officer.Might * _cb.AutoRecruitTroopsMightMultiplier) * rate;
        if (city is not null)
        {
            total = ApplyLowSecurityOutputPenalty(total, city.Security);
        }
        var sum = 0;
        for (var i = 0; i < codes.Length; i++)
        {
            var troops = total / codes.Length + (i < total % codes.Length ? 1 : 0);
            sum += _cb.AutoRecruitGoldCost(codes[i], troops);
        }

        return sum;
    }

    private int AutoRecruitWeeklyTroopsFor(General officer, City? city = null, int? rateOverride = null)
    {
        var rate = CommandBalance.AutoRecruitRate(rateOverride ?? city?.AutoRecruitRate ?? 1);
        var weekly = (_cb.AutoRecruitTroopsBase + officer.Might * _cb.AutoRecruitTroopsMightMultiplier) * rate;
        if (city is not null)
        {
            weekly = ApplyLowSecurityOutputPenalty(weekly, city.Security);
        }

        return weekly;
    }

    private string AutoRecruitTroopNames(string troopCodes)
    {
        var names = troopCodes.Split(',', System.StringSplitOptions.TrimEntries | System.StringSplitOptions.RemoveEmptyEntries)
            .Select(TroopName).ToList();
        return names.Count == 0 ? TroopName(_cb.AutoRecruitDefaultTroopCode) : string.Join(", ", names);
    }

    // 건설 배치 모드 진입 — 명령 모달을 닫고 반투명 고스트를 띄운다. 커서를 따라다니며,
    // 평지·숲 유효 칸에서만 초록, 그 외엔 빨강(클릭해도 컨펌 안 뜸).
    private void BeginPlacement(CityId city, int cmdIndex, GeneralId general, int p)
    {
        CloseModal();
        _cmdMenu.Visible = false; // 명령 팔레트(내정·군비·계략)는 배치 중 숨긴다

        // 화면 전체를 살짝 어둡게 — 배치에 집중하도록. 맵 클릭은 통과(MouseFilter=Ignore).
        _placeDim = new CanvasLayer { Layer = 40 };
        var dim = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.32f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _placeDim.AddChild(dim);
        AddChild(_placeDim);

        _placing = true;
        _placeCity = city;
        _placeCmdIndex = cmdIndex;
        _placeGeneral = general;
        _placeParam = p;
        _placeCode = Facilities[p].Code;
        _placeValidHex = null;

        _placeGhost = _view.TileScene(FacilityTerrain(_placeCode))?.Instantiate<Node3D>();
        if (_placeGhost is not null)
        {
            SetTransparency(_placeGhost, 0.5f);
            _placeGhost.Visible = false;
            AddChild(_placeGhost);
        }

        _placeMarker = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = _view.HexWorldSize * 0.96f,
                BottomRadius = _view.HexWorldSize * 0.96f,
                Height = 0.05f,
                RadialSegments = 6,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false,
            MaterialOverride = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                EmissionEnabled = true,
                NoDepthTest = true,
            },
        };
        AddChild(_placeMarker);

        _log.Text = $"{Facilities[p].Label} 설치 위치 — 평지·숲 위에서 클릭(우클릭 취소).";
    }

    // 배치 모드 종료 — 고스트·마커 정리.
    private void FinishPlacement()
    {
        _placing = false;
        _placeValidHex = null;
        _placeGhost?.QueueFree();
        _placeGhost = null;
        _placeMarker?.QueueFree();
        _placeMarker = null;
        _placeDim?.QueueFree();
        _placeDim = null;
    }

    // 커서 아래 칸으로 고스트·마커를 옮기고 유효성을 갱신한다(마우스 이동 시).
    private void UpdatePlacementHover(Vector2 screen)
    {
        if (RayToGround(screen) is not { } hex)
        {
            if (_placeGhost is not null) { _placeGhost.Visible = false; }
            if (_placeMarker is not null) { _placeMarker.Visible = false; }
            _placeValidHex = null;
            return;
        }

        var city = _state.Cities.First(c => c.Id == _placeCity);
        var valid = IsBuildablePlot(hex, city);
        _placeValidHex = valid ? hex : null;

        var world = _view.HexToWorld(hex);
        if (_placeGhost is not null)
        {
            _placeGhost.Visible = true;
            // 실제 배치와 같이 타일 윗면으로 올린다 — 지형 타일과 y=0에서 겹쳐 투명 Z-파이팅(깜빡임)이 나던 문제.
            _placeGhost.Position = world + new Vector3(0f, _view.TileTopY, 0f);
        }

        if (_placeMarker is not null)
        {
            _placeMarker.Visible = true;
            _placeMarker.Position = world + new Vector3(0f, _view.TileTopY + 0.03f, 0f);
            var mat = (StandardMaterial3D)_placeMarker.MaterialOverride;
            var col = valid ? new Color(0.35f, 0.85f, 0.4f, 0.4f) : new Color(0.9f, 0.3f, 0.28f, 0.4f);
            mat.AlbedoColor = col;
            mat.Emission = new Color(col.R * 0.6f, col.G * 0.6f, col.B * 0.6f);
        }
    }

    // 설치 가능 칸인가 — 평지·숲, 성 반경 안, 성 타일 아님, 이미 시설·공사·성이 없는 칸.
    private bool IsBuildablePlot(HexCoord hex, City city)
    {
        if (!_map.Contains(hex) || hex == city.Position) { return false; }
        if (hex.Distance(city.Position) > _cb.BuildPlotRadius) { return false; }
        var t = _passability.TerrainAt(hex);
        if (t is not (TerrainType.Plains or TerrainType.Forest)) { return false; } // 평지·숲만
        if (_state.Placements.Any(pp => pp.Plot == hex)) { return false; }
        if (_state.Commands.Any(c => c.Kind == CommandKind.Build && c.Plot == hex)) { return false; }
        if (_state.Cities.Any(c => c.Position == hex)) { return false; }
        return true;
    }

    // 노드 트리 전체 메시를 반투명하게(고스트용). Godot4 GeometryInstance3D.Transparency.
    private static void SetTransparency(Node node, float amount)
    {
        if (node is GeometryInstance3D gi) { gi.Transparency = amount; }
        foreach (var child in node.GetChildren()) { SetTransparency(child, amount); }
    }

    // 완성 시설 + 공사중 모델을 성별·시설별 온전/잔해 개수에 맞춰 다시 그린다.
    private void RedrawFacilities()
    {
        if (_facilityLayer is null) { return; }
        foreach (var ch in _facilityLayer.GetChildren()) { ch.QueueFree(); }

        var rubbleScene = GD.Load<PackedScene>("res://assets/models/rubble.glb");
        foreach (var city in _state.Cities)
        {
            foreach (var (code, intact, ruined) in new[]
                     {
                         ("paddy", city.Paddies, city.RuinedPaddies),
                         ("farm", city.Farms, city.RuinedFarms),
                         ("village", city.Villages, city.RuinedVillages),
                         ("workshop", city.Workshop ? 1 : 0, city.WorkshopRuined ? 1 : 0),
                     })
            {
                // append-only 배치 목록을 순서대로 앞에서부터 intact개만 실제 모델로 그린다
                // (나머지는 약탈로 잔해가 된 것 — 타일을 비운다).
                var placed = _state.Placements.Where(pp => pp.City == city.Id && pp.Code == code).ToList();
                for (var i = 0; i < intact && i < placed.Count; i++)
                {
                    var scene = _view.TileScene(FacilityTerrain(code));
                    if (scene is null) { continue; }
                    var node = scene.Instantiate<Node3D>();
                    // 기존 평지/숲 타일 위에 얹는다 — 타일 윗면 높이만큼 올려 바닥끼리 Z-파이팅을 피한다.
                    node.Position = _view.HexToWorld(placed[i].Plot) + new Vector3(0f, _view.TileTopY, 0f);
                    node.Scale *= FacilityDisplayScale;
                    _facilityLayer.AddChild(node);
                }

                for (var i = intact; i < intact + ruined && i < placed.Count; i++)
                {
                    var node = rubbleScene.Instantiate<Node3D>();
                    node.Position = _view.HexToWorld(placed[i].Plot) + new Vector3(0f, _view.TileTopY, 0f);
                    node.Scale *= FacilityDisplayScale;
                    _facilityLayer.AddChild(node);
                }
            }
        }

        // 공사 중(진행 중 건설 명령)에는 공사장 에셋(construction.glb)을 그 타일 위에 얹는다.
        var siteScene = GD.Load<PackedScene>("res://assets/models/construction.glb");
        foreach (var c in _state.Commands.Where(c => c.Kind == CommandKind.Build && c.Plot is not null))
        {
            var origin = _view.HexToWorld(c.Plot!.Value) + new Vector3(0f, _view.TileTopY, 0f);
            var site = siteScene.Instantiate<Node3D>();
            site.Position = origin;
            site.Scale *= FacilityDisplayScale;
            site.AddChild(BuildConstructionDust()); // 흙먼지 — 사람이 일하고 있다는 신호
            _facilityLayer.AddChild(site);

            // 남은 일수 UI(총 일수에서 감소) — 타일 위에 띄운다.
            var total = c.CompletionDay - c.StartDay;
            var remaining = System.Math.Max(0, c.CompletionDay - _state.Day);
            var lbl = new Label3D
            {
                Text = $"🏗 {remaining}/{total}일",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 30,
                OutlineSize = 10,
                NoDepthTest = true,
                Modulate = new Color(1f, 0.92f, 0.6f),
                Position = origin + new Vector3(0f, 0.7f, 0f),
            };
            _facilityLayer.AddChild(lbl);
        }

        foreach (var c in _state.Commands.Where(c => c.Kind == CommandKind.Upgrade && c.Plot is not null))
        {
            var origin = _view.HexToWorld(c.Plot!.Value) + new Vector3(0f, _view.TileTopY, 0f);
            _facilityLayer.AddChild(BuildUpgradeSmoke(origin));
            var total = c.CompletionDay - c.StartDay;
            var remaining = System.Math.Max(0, c.CompletionDay - _state.Day);
            var lbl = new Label3D
            {
                Text = $"⬆ {remaining}/{total}일",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 30,
                OutlineSize = 10,
                NoDepthTest = true,
                Modulate = new Color(0.72f, 0.92f, 1f),
                Position = origin + new Vector3(0f, 0.82f, 0f),
            };
            _facilityLayer.AddChild(lbl);
        }

        foreach (var op in _state.ProductionOps.Where(o => o.Phase == ProductionPhase.Gathering))
        {
            var origin = _view.HexToWorld(op.Target) + new Vector3(0f, _view.TileTopY, 0f);
            var gName = OfficerName(op.General) ?? $"G{op.General.Value}";
            var lbl = new Label3D
            {
                Text = $"생산중\n{gName} · {op.GatherRemaining(_state.Day)}일",
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 26,
                OutlineSize = 9,
                NoDepthTest = true,
                Modulate = new Color(0.72f, 1f, 0.72f),
                Position = origin + new Vector3(0f, 0.92f, 0f),
            };
            _facilityLayer.AddChild(lbl);
        }
    }

    // 공사 흙먼지 — 옅은 갈색 입자가 낮게 피어올라 흩어진다(작업 중 신호).
    private static CpuParticles3D BuildConstructionDust()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.72f, 0.62f, 0.44f, 0f));
        gradient.AddPoint(0.3f, new Color(0.74f, 0.64f, 0.46f, 0.5f));
        gradient.SetColor(1, new Color(0.8f, 0.72f, 0.55f, 0f));

        var mesh = new SphereMesh
        {
            Radius = 0.03f,
            Height = 0.06f,
            RadialSegments = 6,
            Rings = 3,
            Material = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };

        return new CpuParticles3D
        {
            Position = new Vector3(0f, 0.12f, 0f),
            Amount = 10,
            Lifetime = 2.2f,
            Preprocess = 2.5f,
            Mesh = mesh,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(0.28f, 0.02f, 0.28f),
            Direction = new Vector3(0.3f, 1f, 0f),
            Spread = 12f,
            InitialVelocityMin = 0.06f,
            InitialVelocityMax = 0.12f,
            Gravity = new Vector3(0.04f, 0.02f, 0.02f),
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.5f,
            ColorRamp = gradient,
        };
    }

    private static CpuParticles3D BuildUpgradeSmoke(Vector3 origin)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.55f, 0.55f, 0.52f, 0f));
        gradient.AddPoint(0.25f, new Color(0.62f, 0.60f, 0.55f, 0.48f));
        gradient.SetColor(1, new Color(0.78f, 0.76f, 0.70f, 0f));

        var mesh = new SphereMesh
        {
            Radius = 0.045f,
            Height = 0.09f,
            RadialSegments = 8,
            Rings = 4,
            Material = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };

        return new CpuParticles3D
        {
            Position = origin + new Vector3(0f, 0.45f, 0f),
            Amount = 16,
            Lifetime = 2.8f,
            Preprocess = 2.8f,
            Mesh = mesh,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.18f,
            Direction = Vector3.Up,
            Spread = 24f,
            InitialVelocityMin = 0.08f,
            InitialVelocityMax = 0.2f,
            Gravity = new Vector3(0.03f, 0.035f, 0.01f),
            ScaleAmountMin = 0.7f,
            ScaleAmountMax = 1.8f,
            ColorRamp = gradient,
        };
    }

    // 훈련 옵션과 같은 정렬(병종 → 신병 뒤)의 p번째 대기 병력.
    private GarrisonForce? GarrisonAt(CityId city, int p) => _state.Garrisons
        .Where(g => g.City == city && g.Troops > 0)
        .OrderBy(g => g.TroopCode, System.StringComparer.Ordinal).ThenBy(g => g.Trainee)
        .ElementAtOrDefault(p);

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
        CommandKind.Upgrade => "업그레이드",
        CommandKind.SetTaxRate => "세율",
        CommandKind.Research => "연구",
        CommandKind.Repair => "수리",
        CommandKind.CityStratagem => "계략",
        CommandKind.AppointGovernor => "태수 임명",
        CommandKind.AppointStrategist => "군사 임명",
        CommandKind.AppointSecurityOfficer => "치안 담당",
        CommandKind.AppointDomesticOfficer => "내정 담당",
        CommandKind.AppointRecruitmentOfficer => "병력 담당",
        CommandKind.AppointTrainingOfficer => "훈련 담당",
        CommandKind.Enlist => "등용",
        CommandKind.RecruitHero => "위인 영입",
        CommandKind.Explore => "탐색",
        _ => k.ToString(),
    };

    private static string ExplorationEventText(string cityName, string generalName, string code, int gold, int provisions) => code switch
    {
        "divine_beast_trace" => $"[탐색] {generalName} 장수가 {cityName}에서 신수의 흔적을 발견했습니다.",
        "ancient_relic_clue" => $"[탐색] {generalName} 장수가 {cityName}에서 고대유물의 단서를 발견했습니다.",
        "local_clan_support" => $"[탐색] {cityName}의 지방호족이 금 {gold}, 군량 {provisions}을(를) 지원했습니다.",
        "rumor_clue" => $"[탐색] {generalName} 장수가 {cityName}에서 소문/단서를 얻었습니다.",
        _ => $"[탐색] {generalName} 장수가 {cityName}을 탐색했지만 별다른 성과가 없었습니다.",
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

    private RichTextLabel MakeRichLabel(string text, int size, Color color)
    {
        var l = new RichTextLabel
        {
            BbcodeEnabled = true,
            Text = text,
            FitContent = true,
            ScrollActive = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        l.AddThemeFontOverride("normal_font", _font);
        l.AddThemeFontSizeOverride("normal_font_size", size);
        l.AddThemeColorOverride("default_color", color);
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
        RefreshScoutLabels();
        DrawSupplyZones();
        DrawDeployPaths();
        RedrawFacilities();
        foreach (var child in _facilityLayer.GetChildren().OfType<Node3D>())
            _fog.Register(child, _view.WorldToHex(child.Position));

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
                token.InitDisplay(_view, color,
                    army.IsSupply ? UnitController3D.SupplyModelIndex : TroopModelIndex.GetValueOrDefault(army.TroopCode, 0),
                    army.Field.Position);
                token.SetFormationSize(army.IsSupply ? 1 : FormationFor(army.Pool.Active));

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

            token.SetFormationSize(army.IsSupply ? 1 : FormationFor(army.Pool.Active)); // 보급부대는 규모와 무관하게 단일 전용 모델
            token.DisplaySyncTo(army.Field.Position, 0.3f); // 제자리면 스냅 — 보정 트윈이 방향을 뒤집지 않게
            var lblNode = _armyLabels[army.Id.Value];
            lblNode.Position = _view.HexToWorld(army.Field.Position) + new Vector3(0f, _view.TileTopY + 1.1f, 0f);
            lblNode.Text = $"{army.Pool.Active}";
            lblNode.Visible = army.Field.Owner == Player; // 병력 수는 아군만 표시(적은 편대 규모로 가늠)
        }

        var activeProduction = _state.ProductionOps
            .Where(o => o.Phase is ProductionPhase.Outbound or ProductionPhase.Returning)
            .Select(o => o.Id)
            .ToHashSet();
        foreach (var id in _productionTokens.Keys.Where(id => !activeProduction.Contains(id)).ToList())
        {
            _productionTokens[id].QueueFree();
            _productionLabels[id].QueueFree();
            _productionTokens.Remove(id);
            _productionLabels.Remove(id);
        }

        foreach (var op in _state.ProductionOps.Where(o => o.Phase is ProductionPhase.Outbound or ProductionPhase.Returning))
        {
            if (!_productionTokens.TryGetValue(op.Id, out var token))
            {
                token = new UnitController3D();
                AddChild(token);
                token.InitDisplay(_view, op.Owner == Player ? Blue : Red,
                    TroopModelIndex.GetValueOrDefault(op.TroopCode, 0), op.Position);
                token.SetFormationSize(1);
                token.Scale *= 0.72f;

                var lbl = new Label3D
                {
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                    FontSize = 28,
                    OutlineSize = 8,
                    NoDepthTest = true,
                    Modulate = op.Owner == Player ? Blue : Red,
                };
                AddChild(lbl);
                _productionTokens[op.Id] = token;
                _productionLabels[op.Id] = lbl;
            }

            token.DisplaySyncTo(op.Position, 0.3f);
            var label = _productionLabels[op.Id];
            label.Position = _view.HexToWorld(op.Position) + new Vector3(0f, _view.TileTopY + 0.82f, 0f);
            label.Text = "";
            label.Visible = false;
        }

        var counts = _state.Factions.OrderBy(f => f.Id.Value).Select(f =>
        {
            var cities = _state.CityCount(f.Id);
            var troops = _state.Garrisons.Where(g => _state.Cities.Any(c => c.Id == g.City && c.Owner == f.Id)).Sum(g => g.Troops)
                + _state.Armies.Where(u => u.Field.Owner == f.Id).Sum(u => u.Pool.Active);
            return f.Id == Player ? $"{f.Name} 성{cities} 병{troops}" : $"{f.Name} 성{cities}";
        });
        // 좌상단 HUD: 군주 얼굴·이름 / 년월일 / 세력 요약.
        var ruler = _state.Factions.FirstOrDefault(f => f.Id == Player) is { } pf
            ? _state.Generals.FirstOrDefault(g => g.Id == pf.Ruler)
            : null;
        _hudRuler.Text = ruler is not null ? $"군주 {ruler.Name}" : "군주 —";
        _hudFace.Texture = ruler is not null ? PortraitFor(ruler.Id) : null;
        _hudFacePanel.Visible = _hudFace.Texture is not null;
        _hudDate.Text = $"{_state.Year}년 {_state.Month}월 {_state.DayOfMonth}일 · 주 {_week}";

        var myCities = _state.CityCount(Player);
        var myGenerals = _state.GeneralsOf(Player).Count();
        var myGold = _state.Cities.Where(c => c.Owner == Player).Sum(c => c.Gold);
        var myTroops = _state.Garrisons.Where(g => _state.Cities.Any(c => c.Id == g.City && c.Owner == Player)).Sum(g => g.Troops)
            + _state.Armies.Where(u => u.Field.Owner == Player).Sum(u => u.Pool.Active);
        _status.Text = $"도시 {myCities} · 장수 {myGenerals} · 금 {myGold} · 병력 {myTroops}      "
            + string.Join("  |  ", counts);
        _log.Text = note;
        RefreshBattlefieldVision();
    }

    private void BuildHud()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        panel.Position = new Vector2(12, 12);
        panel.CustomMinimumSize = new Vector2(420, 0);
        panel.AddThemeStyleboxOverride("panel", Frame(Ink, Gold, 2, 8, 10));
        layer.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        panel.AddChild(box);

        // 상단: [군주 얼굴] [군주 이름 / 년월일]  ······  [트레이 아이콘 → 시스템]
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 10);
        box.AddChild(top);

        _hudFacePanel = new PanelContainer { CustomMinimumSize = new Vector2(44, 56) };
        _hudFacePanel.AddThemeStyleboxOverride("panel", Frame(new Color(0.075f, 0.06f, 0.05f), Gold, 1, 6, 2));
        top.AddChild(_hudFacePanel);
        _hudFace = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(40, 52),
        };
        _hudFacePanel.AddChild(_hudFace);

        var nameCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        nameCol.AddThemeConstantOverride("separation", 1);
        top.AddChild(nameCol);
        _hudRuler = MakeLabel("", 16, GoldBright);
        nameCol.AddChild(_hudRuler);
        _hudDate = MakeLabel("", 13, Parchment);
        nameCol.AddChild(_hudDate);

        var tray = MakeButton("☰");
        tray.AddThemeFontSizeOverride("font_size", 20);
        tray.CustomMinimumSize = new Vector2(44, 44);
        tray.TooltipText = "시스템";
        tray.Pressed += OpenSystemPalette;
        top.AddChild(tray);

        // 세력 요약(도시·병력).
        _status = MakeLabel("", 13, Gold);
        _status.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_status);

        _log = MakeLabel("", 12, Parchment);
        _log.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_log);

        BuildReportPanel();
        BuildAdvanceControl();
    }

    // 교전·특기·지속 피해를 자연 문장으로(내 세력만). "도검병을 이끄는 XX가 XX과 교전하여 피해 A를 주고 B를 받았습니다."
    private void AddCombatReport(IReadOnlyList<AdvanceTurn> turns, System.Action<string, Color> ev)
    {
        var combatCol = new Color(0.9f, 0.6f, 0.4f);
        var skillCol = new Color(0.62f, 0.82f, 1f);

        string Van(CombatUnit u) => _state.Generals.FirstOrDefault(g => g.Id == u.VanguardId)?.Name ?? $"부대{u.Id.Value}";
        string Desc(CombatUnit u) => $"{TroopName(u.TroopCode)}을(를) 이끄는 {Van(u)}";

        // 부대 메타(소유·설명)와, 각 부대가 교전한 첫 상대(가장 가까운 적) 추정.
        var meta = new Dictionary<int, (bool Player, string Desc)>();
        foreach (var t in turns)
        {
            foreach (var u in t.Units) { meta[u.Id.Value] = (u.Field.Owner == Player, Desc(u)); }
        }

        string Opponent(int uid)
        {
            foreach (var t in turns)
            {
                if (t.Combat is not { } c || !c.DamageDealt.ContainsKey(new UnitId(uid))) { continue; }
                var me = t.Units.FirstOrDefault(x => x.Id.Value == uid);
                if (me is null) { continue; }
                var foe = t.Units.Where(x => x.Field.Owner != me.Field.Owner)
                    .OrderBy(x => x.Field.Position.Distance(me.Field.Position)).ThenBy(x => x.Id.Value).FirstOrDefault();
                if (foe is not null) { return Desc(foe); }
            }

            return "적군";
        }

        // 특기 발동(내 세력).
        var skilled = new HashSet<int>();
        foreach (var t in turns)
        {
            foreach (var (uid, sk) in t.FiredActives.OrderBy(k => k.Key.Value))
            {
                if (meta.TryGetValue(uid.Value, out var m) && m.Player) { skilled.Add(uid.Value); ev($"[특기] {m.Desc}이(가) 특기 「{sk.Name}」을(를) 발동했습니다.", skillCol); }
            }

            foreach (var (uid, st) in t.FiredStratagems.OrderBy(k => k.Key.Value))
            {
                if (meta.TryGetValue(uid.Value, out var m) && m.Player) { ev($"[계략] {m.Desc}이(가) 계략 「{st.Name}」을(를) 펼쳤습니다.", skillCol); }
            }
        }

        // 교전 합산(내 세력): 가함/피해 총합 → 한 부대당 한 문장.
        var dealt = new Dictionary<int, int>();
        var taken = new Dictionary<int, int>();
        foreach (var t in turns)
        {
            if (t.Combat is not { } c) { continue; }
            foreach (var (uid, d) in c.DamageDealt) { if (meta.TryGetValue(uid.Value, out var m) && m.Player) { dealt[uid.Value] = dealt.GetValueOrDefault(uid.Value) + d; } }
            foreach (var (uid, d) in c.DamageTaken) { if (meta.TryGetValue(uid.Value, out var m) && m.Player) { taken[uid.Value] = taken.GetValueOrDefault(uid.Value) + d; } }
        }

        foreach (var uid in dealt.Keys.Union(taken.Keys).OrderBy(x => x))
        {
            var skillWord = skilled.Contains(uid) ? "스킬 " : "";
            ev($"[교전] {meta[uid].Desc}이(가) {Opponent(uid)}과(와) 교전하여 {skillWord}피해 {dealt.GetValueOrDefault(uid)}을(를) 주고 {taken.GetValueOrDefault(uid)}을(를) 받았습니다.", combatCol);
        }

        // 지속·계략 즉발 피해(내 세력).
        var dot = new Dictionary<int, int>();
        foreach (var t in turns)
        {
            foreach (var (uid, d) in t.StatusDamage) { if (meta.TryGetValue(uid.Value, out var m) && m.Player) { dot[uid.Value] = dot.GetValueOrDefault(uid.Value) + d; } }
            foreach (var (uid, d) in t.StratagemDamage) { if (meta.TryGetValue(uid.Value, out var m) && m.Player) { dot[uid.Value] = dot.GetValueOrDefault(uid.Value) + d; } }
        }

        foreach (var uid in dot.Keys.OrderBy(x => x))
        {
            ev($"[교전] {meta[uid].Desc}이(가) 계략·지속 피해 {dot[uid]}을(를) 입었습니다.", combatCol);
        }
    }

    private void AddAutoOfficerReport(GameState before, GameState after, System.Action<string, Color> ev)
    {
        if (!_cb.AutoOfficerSystemEnabled) { return; }

        var col = new Color(0.74f, 0.9f, 0.72f);
        foreach (var city in after.Cities.Where(c => c.Owner == Player).OrderBy(c => c.Id.Value))
        {
            var prev = before.Cities.FirstOrDefault(c => c.Id == city.Id);
            if (prev is null) { continue; }

            var parts = new List<string>();
            AddDelta(parts, "치안", city.Security - prev.Security);
            AddDelta(parts, "금", city.Gold - prev.Gold);
            AddDelta(parts, "군량", city.Provisions - prev.Provisions);

            var beforeTroops = before.Garrisons.Where(g => g.City == city.Id).Sum(g => g.Troops);
            var afterTroops = after.Garrisons.Where(g => g.City == city.Id).Sum(g => g.Troops);
            var troopDelta = afterTroops - beforeTroops;
            if (troopDelta != 0)
            {
                parts.Add($"병력 {(troopDelta > 0 ? "+" : "")}{troopDelta}({AutoRecruitTroopNames(string.Join(',', CurrentAutoRecruitTroopCodes(city)))})");
            }

            var beforeTraining = WeightedTraining(before.Garrisons.Where(g => g.City == city.Id));
            var afterTraining = WeightedTraining(after.Garrisons.Where(g => g.City == city.Id));
            AddDelta(parts, "훈련도", afterTraining - beforeTraining);

            if (parts.Count > 0)
            {
                ev($"[자동내정] {city.Name}: {string.Join(" · ", parts)}", col);
            }
        }
    }

    private static bool HasAnyAutoOfficer(City city)
        => city.SecurityOfficer is not null || city.DomesticOfficer is not null
            || city.RecruitmentOfficer is not null || city.TrainingOfficer is not null;

    private static int WeightedTraining(IEnumerable<GarrisonForce> garrisons)
    {
        var total = 0;
        long sum = 0;
        foreach (var g in garrisons)
        {
            total += g.Troops;
            sum += g.Troops * (long)g.TrainingLevel;
        }

        return total <= 0 ? 0 : (int)((sum + total / 2) / total);
    }

    private static void AddDelta(List<string> parts, string label, int delta)
    {
        if (delta != 0) { parts.Add($"{label} {(delta > 0 ? "+" : "")}{delta}"); }
    }

    // 좌하단 보고 패널(삼국지11 오마주) — 진행 결과·명령 발행 등 사건을 최근 순으로 쌓아 보여준다.
    private void BuildReportPanel()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        // 좌하단 고정 크기 패널 — 내용이 늘어도 패널 자체는 커지지 않는다(리사이즈로 위치가 튀던 문제
        // 방지). 이전 내용은 내부 ScrollContainer로 스크롤해서 본다.
        const float panelW = 340f, panelH = 200f;
        _reportPanel = new PanelContainer { Visible = false };
        _reportPanel.AddThemeStyleboxOverride("panel", Frame(new Color(Ink, 0.94f), Gold, 2, 8, 9));
        _reportPanel.AnchorLeft = 0f; _reportPanel.AnchorRight = 0f;
        _reportPanel.AnchorTop = 1f; _reportPanel.AnchorBottom = 1f;
        _reportPanel.OffsetLeft = 12f; _reportPanel.OffsetRight = 12f + panelW;
        _reportPanel.OffsetTop = -(panelH + 12f); _reportPanel.OffsetBottom = -12f;
        layer.AddChild(_reportPanel);

        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 2);
        _reportPanel.AddChild(v);

        var headRow = new HBoxContainer();
        headRow.AddThemeConstantOverride("separation", 8);
        v.AddChild(headRow);
        var head = MakeLabel("◈ 보고", 13, Gold);
        head.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        headRow.AddChild(head);
        var full = MakeButton("전체");
        full.AddThemeFontSizeOverride("font_size", 11);
        full.CustomMinimumSize = new Vector2(48, 22);
        full.Pressed += OpenFullLog;
        headRow.AddChild(full);

        v.AddChild(GoldRule());

        _reportScroll = new ScrollContainer();
        _reportScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        _reportScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _reportScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        v.AddChild(_reportScroll);

        _reportBox = new VBoxContainer();
        _reportBox.AddThemeConstantOverride("separation", 1);
        _reportBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _reportScroll.AddChild(_reportBox);
    }

    // 보고 한 줄 추가 — 전체 히스토리(캡 300)에 쌓고, 좌하단엔 최근 ReportMax줄만 보인다. color로 사건 성격 구분.
    private void Report(string text, Color? color = null)
    {
        if (_reportBox is null) { return; }
        var c = color ?? Parchment;
        _reportHistory.Add((text, c));
        while (_reportHistory.Count > ReportHistoryMax) { _reportHistory.RemoveAt(0); }

        var l = MakeLabel(text, 12, c);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(300, 0);
        _reportBox.AddChild(l);
        while (_reportBox.GetChildCount() > ReportBoxMax)
        {
            var old = _reportBox.GetChild(0);
            _reportBox.RemoveChild(old);
            old.QueueFree();
        }

        _reportPanel.Visible = true;
        // 새 줄이 추가되면 맨 아래(최신)로 스크롤 — 레이아웃이 갱신된 뒤 실행한다.
        _reportScroll.CallDeferred(ScrollContainer.MethodName.EnsureControlVisible, l);
        ScrollReportToBottomDeferred();
    }

    private async void ScrollReportToBottomDeferred()
    {
        Callable.From(ScrollReportToBottom).CallDeferred();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ScrollReportToBottom();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        ScrollReportToBottom();
    }

    private void ScrollReportToBottom()
    {
        if (_reportScroll is null) { return; }
        var bar = _reportScroll.GetVScrollBar();
        if (bar is null) { return; }
        bar.Value = bar.MaxValue;
        _reportScroll.ScrollVertical = (int)bar.MaxValue;
    }

    // 전체 로그 열람(스크롤) — 보고 패널의 "전체" 버튼. 최근이 아래, 오래된 것 위.
    private void OpenFullLog()
    {
        if (_advancing) { return; }
        if (_modalLayer is not null) { _modalLayer.QueueFree(); _modalLayer = null; }
        // 전체 보고는 화면 전체창으로 — 좌우·상하 여백만 남기고 최대한 넓게.
        var vp = GetViewport().GetVisibleRect().Size;
        var margin = 10f;
        var mw = Mathf.Max(480f, vp.X - margin * 2f - 28f);
        var mh = Mathf.Max(360f, vp.Y - margin * 2f - 28f);
        var box = DeployScaffold(mw, out var scroll, out var panel);
        panel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panel.OffsetLeft = margin;
        panel.OffsetTop = margin;
        panel.OffsetRight = -margin;
        panel.OffsetBottom = -margin;

        var titleRow = new HBoxContainer();
        box.AddChild(titleRow);
        var title = MakeLabel($"◈ 전체 보고 ({_reportHistory.Count})", 17, Gold);
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        title.MouseFilter = Control.MouseFilterEnum.Ignore;
        titleRow.AddChild(title);
        var close = MakeButton("✕");
        close.CustomMinimumSize = new Vector2(40, 30);
        close.Pressed += CloseModal;
        titleRow.AddChild(close);
        box.AddChild(GoldRule());

        if (_reportHistory.Count == 0) { box.AddChild(MakeLabel("(기록 없음)", 12, Parchment)); }
        foreach (var (text, color) in _reportHistory)
        {
            var l = MakeLabel(text, 12, color);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.CustomMinimumSize = new Vector2(mw - 40, 0);
            box.AddChild(l);
        }

        scroll.CustomMinimumSize = new Vector2(mw, mh);
        scroll.SetDeferred("scroll_vertical", 100000); // 최신(아래)으로
    }

    // 진행 버튼(화면 우측 하단, 100×100 원형 아이콘) + 진행 중 "N일차" 텍스트(버튼 20px 위).
    private void BuildAdvanceControl()
    {
        var layer = new CanvasLayer();
        AddChild(layer);

        var vb = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        vb.AddThemeConstantOverride("separation", 20); // 일차 텍스트가 버튼 20px 위
        vb.AnchorLeft = 1f; vb.AnchorTop = 1f; vb.AnchorRight = 1f; vb.AnchorBottom = 1f;
        vb.GrowHorizontal = Control.GrowDirection.Begin;
        vb.GrowVertical = Control.GrowDirection.Begin;
        vb.OffsetLeft = -16f; vb.OffsetTop = -16f; vb.OffsetRight = -16f; vb.OffsetBottom = -16f;
        layer.AddChild(vb);

        _dayLabel = MakeLabel("", 24, GoldBright);
        _dayLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _dayLabel.CustomMinimumSize = new Vector2(100, 0);
        _dayLabel.AddThemeConstantOverride("outline_size", 6); // 배경 대비 — 어두운 외곽선
        _dayLabel.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.03f, 0.02f, 0.95f));
        _dayLabel.Visible = false;
        vb.AddChild(_dayLabel);

        _dayTurnLabel = MakeLabel("", 15, Parchment);
        _dayTurnLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _dayTurnLabel.CustomMinimumSize = new Vector2(100, 0);
        _dayTurnLabel.AddThemeConstantOverride("outline_size", 5);
        _dayTurnLabel.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.03f, 0.02f, 0.95f));
        _dayTurnLabel.Visible = false;
        vb.AddChild(_dayTurnLabel);

        // 이미지는 교체 가능 — res://assets/icons/icon_advance.png 파일만 바꾸면 됨(없으면 ▶ 폴백).
        _advanceBtn = new AdvanceButton
        {
            CustomMinimumSize = new Vector2(100, 100),
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
            Icon = LoadAdvanceIcon(),
        };
        _advanceBtn.Pressed = OnAdvance;
        vb.AddChild(_advanceBtn);
    }

    // 진행 버튼 아이콘 로드(교체용). 파일 없으면 null → 버튼이 금색 ▶ 폴백을 그린다.
    private static Texture2D? LoadAdvanceIcon()
    {
        const string path = "res://assets/icons/icon_advance.png";
        if (!Godot.FileAccess.FileExists(path)) { return null; }
        var img = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }
}
