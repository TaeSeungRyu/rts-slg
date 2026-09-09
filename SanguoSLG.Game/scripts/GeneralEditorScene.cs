namespace SanguoSLG.Game;

using Godot;
using SanguoSLG.Core.Data;

public partial class GeneralEditorScene : Control
{
    private readonly Dictionary<int, GeneralEditorRecord> _generalsById = new();
    private readonly List<GeneralEditorRecord> _generals = [];
    private LineEdit _search = null!;
    private OptionButton _regionFilter = null!;
    private ItemList _list = null!;
    private Label _summary = null!;
    private Label _status = null!;
    private GeneralEditorRecord? _selected;
    private string _dataDirectory = "";

    public override void _Ready()
    {
        BuildUi();
        Reload();
    }

    private void BuildUi()
    {
        var root = new VBoxContainer
        {
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = 18,
            OffsetTop = 18,
            OffsetRight = -18,
            OffsetBottom = -18,
            Theme = BuildTheme(),
        };
        AddChild(root);

        var title = new Label
        {
            Text = "개발용 장수 에디터",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 24);
        root.AddChild(title);

        var toolbar = new HBoxContainer();
        root.AddChild(toolbar);

        _search = new LineEdit
        {
            PlaceholderText = "ID 또는 이름 검색",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _search.TextChanged += _ => RefreshList();
        toolbar.AddChild(_search);

        _regionFilter = new OptionButton { CustomMinimumSize = new Vector2(180, 0) };
        _regionFilter.ItemSelected += _ => RefreshList();
        toolbar.AddChild(_regionFilter);

        var reload = new Button { Text = "재로드", CustomMinimumSize = new Vector2(110, 0) };
        reload.Pressed += Reload;
        toolbar.AddChild(reload);

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(body);

        _list = new ItemList
        {
            CustomMinimumSize = new Vector2(310, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _list.ItemSelected += OnGeneralSelected;
        body.AddChild(_list);

        var detail = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddChild(detail);

        _summary = new Label
        {
            Text = "장수를 선택하세요.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _summary.AddThemeFontSizeOverride("font_size", 18);
        detail.AddChild(_summary);

        _status = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        root.AddChild(_status);
    }

    private void Reload()
    {
        try
        {
            _dataDirectory = FindDataDirectory();
            _generals.Clear();
            _generalsById.Clear();
            foreach (var general in GeneralEditorStore.LoadGenerals(File.ReadAllText(Path.Combine(_dataDirectory, "generals.json"))))
            {
                _generals.Add(general);
                _generalsById[general.Id] = general;
            }

            RebuildRegions();
            RefreshList();
            _status.Text = $"로드 완료: {_generals.Count}명, 데이터 경로 {_dataDirectory}";
        }
        catch (Exception ex)
        {
            _status.Text = $"로드 실패: {ex.Message}";
        }
    }

    private void RebuildRegions()
    {
        var previous = _regionFilter.GetSelectedId() >= 0 ? _regionFilter.GetItemText(_regionFilter.Selected) : "전체 지역";
        _regionFilter.Clear();
        _regionFilter.AddItem("전체 지역");
        foreach (var region in _generals.Select(g => g.Region).Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().OrderBy(r => r))
        {
            _regionFilter.AddItem(region);
        }

        for (var i = 0; i < _regionFilter.ItemCount; i++)
        {
            if (_regionFilter.GetItemText(i) == previous)
            {
                _regionFilter.Select(i);
                return;
            }
        }

        _regionFilter.Select(0);
    }

    private void RefreshList()
    {
        var query = _search.Text.Trim();
        var region = _regionFilter.Selected > 0 ? _regionFilter.GetItemText(_regionFilter.Selected) : "";
        _list.Clear();

        foreach (var general in _generals.OrderBy(g => g.Id))
        {
            if (!string.IsNullOrEmpty(region) && general.Region != region)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(query)
                && !general.Id.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)
                && !general.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var index = _list.AddItem($"{general.Id,3}  {general.Name}  [{general.Region}]");
            _list.SetItemMetadata(index, general.Id);
        }

        if (_selected is not null)
        {
            ShowGeneral(_selected);
        }
    }

    private void OnGeneralSelected(long index)
    {
        var id = (int)_list.GetItemMetadata((int)index);
        if (_generalsById.TryGetValue(id, out var general))
        {
            ShowGeneral(general);
        }
    }

    private void ShowGeneral(GeneralEditorRecord general)
    {
        _selected = general;
        var aptitudes = string.Join(" / ", general.Aptitudes.Select(kv => $"{TroopLabel(kv.Key)} {kv.Value}"));
        var battleActive = string.IsNullOrWhiteSpace(general.BattleActive) ? "없음" : general.BattleActive;
        var battlePassives = general.BattlePassives.Count == 0 ? "없음" : string.Join(", ", general.BattlePassives.Select(s => $"{s.Code} Lv{s.Tier}"));
        var adminPassives = general.AdminPassives.Count == 0 ? "없음" : string.Join(", ", general.AdminPassives.Select(s => $"{s.Code} Lv{s.Tier}"));

        _summary.Text =
            $"ID {general.Id}  {general.Name}\n" +
            $"출생 {general.Birth} / 해금 {general.UnlockYear} / 지역 {general.Region}\n\n" +
            $"무력 {general.Might} / 지력 {general.Intellect} / 정치 {general.Politics}\n" +
            $"적성: {aptitudes}\n\n" +
            $"전투 액티브: {battleActive}\n" +
            $"전투 패시브: {battlePassives}\n" +
            $"내정 패시브: {adminPassives}\n\n" +
            general.Desc;
    }

    private static Theme BuildTheme()
    {
        var theme = new Theme();
        var font = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf");
        theme.DefaultFont = font;
        theme.DefaultFontSize = 15;
        return theme;
    }

    private static string FindDataDirectory()
    {
        var dir = new DirectoryInfo(ProjectSettings.GlobalizePath("res://"));
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "generals.json")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("data/generals.json 경로를 찾을 수 없습니다.");
    }

    private static string TroopLabel(string code) => code switch
    {
        "infantry" => "보병",
        "archer" => "궁병",
        "cavalry" => "기병",
        "elephant" => "상병",
        "siege" => "공성",
        "naval" => "해상",
        _ => code,
    };
}

