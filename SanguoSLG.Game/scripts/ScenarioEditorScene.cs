using Godot;
using SanguoSLG.Core.Data;

namespace SanguoSLG.Game;

public sealed partial class ScenarioEditorScene : Control
{
    private readonly List<ScenarioMapVersion> _maps = [];
    private ScenarioMapArchive _archive = null!;
    private Scenario _source = null!;
    private ScenarioDraft _draft = null!;
    private ItemList _list = null!;
    private VBoxContainer _body = null!;
    private Label _message = null!;
    private string _scenarioDirectory = "";
    private NativeNameEdit? _nativeText;
    private LineEdit? _nativePlaceholder;
    private Action<string>? _nativeUpdate;
    private string _nativeLastText = "";
    private int _selectedCityId;
    private int _selectedGeneralCityId;
    private string _generalSearch = "";
    private static readonly Color[] FactionPalette = Enumerable.Range(0, 60)
        .Select(index => Color.FromHsv((index % 12) / 12f, 0.48f + (index / 12) * 0.105f,
            0.76f + (index / 12 % 2) * 0.12f)).ToArray();

    public override void _Ready()
    {
        Theme = GeneralWorkspaceStyle.Theme();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var root = ProjectSettings.GlobalizePath("user://");
        _scenarioDirectory = Path.Combine(root, "scenarios");
        _archive = new ScenarioMapArchive(Path.Combine(root, "maps"), _scenarioDirectory);
        var data = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data"));
        _source = CreatedGeneralPresets.ApplyAtNewGame(
            new ScenarioLoader().LoadFromDirectory(data));
        LoadMaps();
        _draft = ScenarioDraftApplier.CreateSeed("custom-190", "새 시나리오", _source, _maps[0]);
        BuildUi();
        RefreshList();
        ShowBasics();
        if (OS.GetCmdlineUserArgs().Contains("--scenarioeditorqa")) CallDeferred(nameof(RunQa));
    }

    public override void _Process(double delta)
    {
        if (_nativeText is null) return;
        _nativeText.SyncBounds();
        var value = _nativeText.Text;
        if (value == _nativeLastText) return;
        _nativeLastText = value;
        _nativeUpdate?.Invoke(value);
    }

    public override void _ExitTree() => EndNativeText();

    private void BeginNativeText(LineEdit field, Action<string> update)
    {
        if (!OperatingSystem.IsWindows() || DisplayServer.GetName() == "headless") return;
        Callable.From(() =>
        {
            if (!field.IsInsideTree()) return;
            EndNativeText();
            _nativePlaceholder = field;
            _nativeUpdate = update;
            _nativeLastText = field.Text;
            _nativeText = NativeNameEdit.Create(field, GetViewport());
        }).CallDeferred();
    }

    private void EndNativeText()
    {
        if (_nativeText is null) return;
        var value = _nativeText.Text;
        _nativeUpdate?.Invoke(value);
        if (_nativePlaceholder is { } field && GodotObject.IsInstanceValid(field)) field.Text = value;
        _nativeText.Dispose();
        _nativeText = null;
        _nativePlaceholder = null;
        _nativeUpdate = null;
    }

    private void LoadMaps()
    {
        var version = ScenarioRootMapSource.OpenVersion(_archive);
        _maps.Add(version with { Title = "기본 지도" });
        GD.Print($"[scenario-editor-map] root={ScenarioRootMapSource.Path} revision={version.Revision} sites={version.Preset.Sites.Count}");
        var custom = ProjectSettings.GlobalizePath("user://map-presets/custom");
        if (!Directory.Exists(custom)) return;
        foreach (var path in Directory.EnumerateFiles(custom, "*.json").Order())
            try
            {
                var id = Path.GetFileNameWithoutExtension(path);
                _maps.Add(_archive.SaveVersion(id, id, ScenarioMapPresetStore.Load(path)));
            }
            catch (Exception error) { GD.PushWarning("사용자 지도 가져오기 실패: " + error.Message); }
    }

    private void BuildUi()
    {
        var background = new ColorRect { Color = new Color("172322") };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        var frame = new MarginContainer();
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        frame.AddThemeConstantOverride("margin_left", 20);
        frame.AddThemeConstantOverride("margin_right", 20);
        frame.AddThemeConstantOverride("margin_top", 16);
        frame.AddThemeConstantOverride("margin_bottom", 16);
        AddChild(frame);
        var vertical = new VBoxContainer();
        frame.AddChild(vertical);
        var header = new HBoxContainer();
        vertical.AddChild(header);
        header.AddChild(new Label { Text = "시나리오 에디터", SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var newButton = new Button { Text = "새 시나리오" };
        newButton.Pressed += CreateNew;
        header.AddChild(newButton);
        var cloneButton = new Button { Text = "복제" };
        cloneButton.Pressed += CloneCurrent;
        header.AddChild(cloneButton);
        var saveButton = new Button { Text = "검증 후 저장" };
        saveButton.Pressed += SaveCurrent;
        header.AddChild(saveButton);
        var closeButton = new Button { Text = "닫기" };
        closeButton.Pressed += () => ScenarioStartMenu.ReturnToStart(GetTree());
        header.AddChild(closeButton);
        vertical.AddChild(new HSeparator());
        var columns = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        vertical.AddChild(columns);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(255, 0) };
        columns.AddChild(left);
        left.AddChild(new Label { Text = "시나리오 목록" });
        _list = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        _list.ItemSelected += index => OpenScenario(_list.GetItemMetadata((int)index).AsString());
        left.AddChild(_list);
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        columns.AddChild(right);
        var tabs = new HBoxContainer();
        right.AddChild(tabs);
        AddTab(tabs, "기본 정보·지도", ShowBasics);
        AddTab(tabs, "세력", ShowFactions);
        AddTab(tabs, "세력·거점", ShowCities);
        AddTab(tabs, "장수 배치", ShowGenerals);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        right.AddChild(scroll);
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(_body);
        _message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        vertical.AddChild(_message);
    }

    private static void AddTab(Container tabs, string name, Action action)
    {
        var button = new Button { Text = name };
        button.Pressed += action;
        tabs.AddChild(button);
    }

    private void RefreshList()
    {
        _list.Clear();
        var builtIn = ProjectSettings.GlobalizePath("res://assets/scenarios");
        var paths = (Directory.Exists(builtIn) ? Directory.EnumerateFiles(builtIn, "*.json") : [])
            .Concat(Directory.Exists(_scenarioDirectory) ? Directory.EnumerateFiles(_scenarioDirectory, "*.json") : [])
            .GroupBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.FirstOrDefault(path => path.StartsWith(_scenarioDirectory, StringComparison.OrdinalIgnoreCase)) ?? group.First())
            .OrderBy(path => Path.GetFileName(path).StartsWith("fictional-", StringComparison.Ordinal) ? 9999
                : ScenarioDraftStore.Load(path).StartYear).ThenBy(path => path);
        foreach (var path in paths)
        {
            try
            {
                var draft = ScenarioDraftStore.Load(path);
                var isBuiltIn = File.Exists(Path.Combine(builtIn, Path.GetFileName(path)));
                var index = _list.AddItem((isBuiltIn ? "기본 · " : "") + draft.Title);
                _list.SetItemMetadata(index, path);
            }
            catch (Exception error) { GD.PushWarning($"시나리오 목록: {path}: {error.Message}"); }
        }
    }

    private void OpenScenario(string path)
    {
        var previous = _draft;
        try
        {
            RefreshCurrentRootMap();
            _draft = ScenarioDraftStore.Load(path);
            EnsureMapAvailable();
            var updated = ApplyCurrentRootMap();
            _draft = ScenarioWallDefaults.Normalize(_draft, CurrentMap().Preset, _source.Balance);
            ShowBasics();
            _message.Text = updated ? "시나리오 배치를 현재 기본 지도에 맞추었습니다. 검증 후 저장하세요."
                : "시나리오를 불러왔습니다.";
        }
        catch (Exception error)
        {
            _draft = previous;
            ShowBasics();
            _message.Text = "불러오기 실패: " + error.Message;
        }
    }

    private void RefreshCurrentRootMap()
    {
        var version = ScenarioRootMapSource.OpenVersion(_archive);
        _maps.RemoveAll(map => map.Id == "default-east-asia");
        _maps.Insert(0, version with { Title = "기본 지도" });
    }

    private bool ApplyCurrentRootMap()
    {
        if (!ScenarioRootMapBinding.UsesRoot(_draft.MapId))
            return false;
        var root = _maps.First(map => map.Id == "default-east-asia");
        if (_draft.MapId == root.Id && _draft.MapRevision == root.Revision) return false;
        _draft = ScenarioRootMapBinding.Bind(_draft, root,
            ScenarioRootMapSource.PreviousVersion(_archive, _draft));
        return true;
    }

    private void EnsureMapAvailable()
    {
        if (_maps.Any(map => map.Id == _draft.MapId && map.Revision == _draft.MapRevision)) return;
        if (_draft.MapId is "default-east-asia" or "legacy-east-asia" or "edited-east-asia")
        {
            ScenarioRootMapSource.PreviousVersion(_archive, _draft);
            return;
        }
        _maps.Add(_archive.Load(_draft.MapId, _draft.MapRevision));
    }

    private void CreateNew()
    {
        RefreshCurrentRootMap();
        _draft = ScenarioDraftApplier.CreateSeed("scenario-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
            "새 시나리오", _source, _maps[0]);
        ShowBasics();
    }

    private void CloneCurrent()
    {
        _draft = _draft with { Id = "scenario-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), Title = _draft.Title + " 복사본" };
        ShowBasics();
    }

    private ScenarioMapVersion CurrentMap()
        => _draft.MapId == "default-east-asia" && _maps[0].Revision == _draft.MapRevision
            ? _maps[0] : _archive.Load(_draft.MapId, _draft.MapRevision);

    private void ClearBody()
    {
        EndNativeText();
        foreach (var child in _body.GetChildren()) child.QueueFree();
    }

    private void ShowBasics()
    {
        ClearBody();
        _body.AddChild(new Label { Text = "기본 정보" });
        _body.AddChild(new Label { Text = "저장 식별자 · 자동 생성: " + _draft.Id,
            TooltipText = "파일을 구별하는 내부 값입니다. 제목만 바꾸어도 됩니다." });
        AddText("제목", _draft.Title, value => _draft = _draft with { Title = value });
        AddText("설명", _draft.Description, value => _draft = _draft with { Description = value });
        AddNumber("시작 연도", _draft.StartYear, 1, 9999, value => _draft = _draft with { StartYear = value });
        AddNumber("월", _draft.StartMonth, 1, 12, value => _draft = _draft with { StartMonth = value });
        AddNumber("일", _draft.StartDay, 1, 30, value => _draft = _draft with { StartDay = value });
        _body.AddChild(new HSeparator());
        _body.AddChild(new Label { Text = "시나리오 지도 · 기본 지도 또는 별도 지도 선택" });
        var choices = new OptionButton();
        var currentIndex = 0;
        for (var i = 0; i < _maps.Count; i++)
        {
            var option = _maps[i];
            choices.AddItem($"{option.Title} · 거점 {option.Preset.Sites.Count(site => site.Kind is "city" or "port" or "gate")}");
            if (option.Id == _draft.MapId && option.Revision == _draft.MapRevision) currentIndex = i;
        }
        choices.Select(currentIndex);
        choices.ItemSelected += index => SwitchMap(_maps[(int)index]);
        _body.AddChild(choices);
        var map = CurrentMap();
        _body.AddChild(new Label { Text = $"현재 지도: {(map.Id == "default-east-asia" ? "기본 지도" : map.Title)}" });
        if (map.Id == "default-east-asia")
        {
            var sourcePath = ProjectSettings.GlobalizePath("user://map-presets/east-asia.json");
            _body.AddChild(new Label { Text = "지도 원본: " + sourcePath });
            _body.AddChild(new Label { Text = "마지막 저장: "
                + File.GetLastWriteTime(sourcePath).ToString("yyyy-MM-dd HH:mm:ss") });
        }
        var latestDefault = _maps.FirstOrDefault(candidate => candidate.Id == "default-east-asia");
        if (latestDefault is not null && (latestDefault.Id != map.Id || latestDefault.Revision != map.Revision))
        {
            var useEdited = new Button { Text = "현재 편집 중인 기본 지도 적용" };
            useEdited.Pressed += () => SwitchMap(latestDefault);
            _body.AddChild(useEdited);
        }
        var missing = _draft.Generals.Where(g => g.SiteId is { } id && !map.Preset.Sites.Any(site => site.Id == id))
            .Select(g => g.GeneralId).ToArray();
        _body.AddChild(new Label { Text = missing.Length == 0 ? "미배치 오류 없음" : "삭제된 거점의 장수: " + string.Join(", ", missing) });
    }

    private void SwitchMap(ScenarioMapVersion next)
    {
        var previous = CurrentMap().Preset;
        var sameMapLineage = next.Id == _draft.MapId
            || (new[] { "default-east-asia", "legacy-east-asia", "edited-east-asia" }.Contains(next.Id)
                && new[] { "default-east-asia", "legacy-east-asia", "edited-east-asia" }.Contains(_draft.MapId));
        if (sameMapLineage && next.Id == "default-east-asia")
        {
            _draft = ScenarioWallDefaults.Normalize(
                ScenarioRootMapBinding.Bind(_draft, next, previous), next.Preset, _source.Balance);
            ShowBasics();
            _message.Text = "현재 기본 지도에 맞춰 거점과 장수 배치를 갱신했습니다.";
            return;
        }
        var oldSiteIds = previous.Sites.Select(site => site.Id).ToHashSet();
        var oldCities = _draft.Cities.ToDictionary(city => city.SiteId);
        var newSites = next.Preset.Sites.Where(site => site.Kind is "city" or "port" or "gate").ToArray();
        var newSiteIds = newSites.Select(site => site.Id).ToHashSet();
        var newCities = newSites.Select(site =>
        {
            var old = sameMapLineage && oldCities.TryGetValue(site.Id, out var existing) ? existing : null;
            return old is null ? new ScenarioCityEntry(site.Id, 0, 0, 0, 0, 70, [], []) : old with { SiteId = site.Id };
        }).ToArray();
        var lost = _draft.Generals.Where(g => g.SiteId is { } id &&
            (!sameMapLineage || !newSiteIds.Contains(id))).ToArray();
        var lostCities = _draft.Cities.Count(c => c.OwnerFactionId > 0
            && (!sameMapLineage || !newSiteIds.Contains(c.SiteId)));
        _draft = _draft with
        {
            MapId = next.Id, MapRevision = next.Revision, Cities = newCities,
            Generals = _draft.Generals.Select(g => g.SiteId is { } id && sameMapLineage
                && oldSiteIds.Contains(id) && newSiteIds.Contains(id) ? g
                : g with { SiteId = null }).ToArray(),
        };
        _draft = ScenarioWallDefaults.Normalize(_draft, next.Preset, _source.Balance);
        ShowBasics();
        _message.Text = $"지도 교체: 소유권 해제 거점 {lostCities}곳, 배치 해제 장수 {lost.Length}명. 세력·거점 및 장수 배치 탭에서 확인하세요.";
    }

    private void ShowCities()
    {
        ClearBody();
        var map = CurrentMap().Preset;
        _body.AddChild(new Label { Text = "지도에서 거점을 선택하세요. 색은 시작 소유 세력을 나타냅니다." });
        if (!_draft.Cities.Any(city => city.SiteId == _selectedCityId))
            _selectedCityId = _draft.Cities.FirstOrDefault()?.SiteId ?? 0;
        _body.AddChild(new ScenarioSiteMap(map, _draft.Cities, _draft.Factions,
            _selectedCityId, id => { _selectedCityId = id; ShowCities(); }));
        var city = _draft.Cities.FirstOrDefault(city => city.SiteId == _selectedCityId);
        var site = map.Sites.FirstOrDefault(site => site.Id == _selectedCityId);
        if (city is null || site is null) return;
        _body.AddChild(new HSeparator());
        _body.AddChild(new Label { Text = $"{site.Name} · {(site.Kind == "port" ? "항구" : site.Kind == "gate" ? "관문" : "성")}" });
        var owner = new OptionButton();
        owner.AddItem("중립", 0);
        foreach (var faction in _draft.Factions) owner.AddItem(faction.Name, faction.Id);
        owner.Select(Math.Max(0, owner.GetItemIndex(city.OwnerFactionId)));
        owner.ItemSelected += index =>
        {
            var newOwner = owner.GetItemId((int)index);
            UpdateCity(city.SiteId, current => current with
            {
                OwnerFactionId = newOwner,
                Garrisons = newOwner == 0 ? [] : current.Garrisons,
                Ships = newOwner == 0 ? [] : current.Ships,
            });
            _draft = _draft with { Generals = _draft.Generals.Select(general =>
                general.SiteId == city.SiteId && general.FactionId != newOwner
                    ? general with { SiteId = null } : general).ToArray() };
            ShowCities();
        };
        _body.AddChild(owner);
        var resources = new HBoxContainer();
        _body.AddChild(resources);
        AddCompactNumber(resources, "금", city.Gold, 0, 10000000, value => UpdateCity(city.SiteId, c => c with { Gold = value }));
        AddCompactNumber(resources, "군량", city.Provisions, 0, 10000000, value => UpdateCity(city.SiteId, c => c with { Provisions = value }));
        resources.AddChild(new Label { Text = "성벽" });
        resources.AddChild(new LineEdit
        {
            Name = "ScenarioWallReadOnly",
            Text = ScenarioWallDefaults.ForSite(site, _source.Balance).ToString("N0"),
            Editable = false,
            CustomMinimumSize = new Vector2(100, 0),
            TooltipText = "거점 크기에 따라 시작 성벽이 자동으로 설정됩니다.",
        });
        AddCompactNumber(resources, "치안", city.Security, 0, 100, value => UpdateCity(city.SiteId, c => c with { Security = value }));
        if (city.OwnerFactionId > 0)
        {
            _body.AddChild(new HSeparator());
            _body.AddChild(new Label { Text = "주둔 병력·선박" });
            AddForceEditor(city, site, ShowCities);
        }
    }

    private void ShowFactions()
    {
        ClearBody();
        var generals = _source.Generals.OrderBy(g => g.Name).ToArray();
        _body.AddChild(new Label { Text = "세력 이름, 군주, 세력 색, 시작 가능 여부와 숨은세력 출현 설정" });
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, Mathf.Min(650, 52 + _draft.Factions.Count * 42)) };
        _body.AddChild(scroll);
        var table = new GridContainer { Columns = 9, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(table);
        foreach (var heading in new[] { "세력", "이름", "군주", "색상", "시작 가능", "숨은세력", "출현 연도", "우선 출현 거점", "관리" })
            table.AddChild(new Label { Text = heading });
        foreach (var faction in _draft.Factions)
        {
            var row = table;
            row.AddChild(new Label { Text = $"세력 {faction.Id}", CustomMinimumSize = new Vector2(90, 0) });
            var name = new LineEdit { Text = faction.Name, CustomMinimumSize = new Vector2(150, 0) };
            name.TextChanged += value => UpdateFaction(faction.Id, f => f with { Name = value });
            name.FocusEntered += () => BeginNativeText(name,
                value => UpdateFaction(faction.Id, f => f with { Name = value }));
            row.AddChild(name);
            var ruler = new OptionButton { CustomMinimumSize = new Vector2(150, 0) };
            foreach (var general in generals) ruler.AddItem(general.Name, general.Id.Value);
            ruler.Select(Math.Max(0, ruler.GetItemIndex(faction.RulerGeneralId)));
            ruler.ItemSelected += index =>
            {
                var generalId = ruler.GetItemId((int)index);
                UpdateFaction(faction.Id, f => f with { RulerGeneralId = generalId });
                if (faction.Hidden) EnsureHiddenRuler(faction.Id, generalId);
            };
            row.AddChild(ruler);
            var color = new OptionButton { CustomMinimumSize = new Vector2(105, 32), TooltipText = "60가지 세력 색 중 선택" };
            var currentColor = new Color(faction.Color);
            var selectedColor = Array.FindIndex(FactionPalette, candidate => candidate.IsEqualApprox(currentColor));
            if (selectedColor < 0) color.AddIconItem(FactionColorIcon(currentColor), "현재 색", -1);
            for (var paletteIndex = 0; paletteIndex < FactionPalette.Length; paletteIndex++)
                color.AddIconItem(FactionColorIcon(FactionPalette[paletteIndex]), $"색상 {paletteIndex + 1:00}", paletteIndex);
            color.Select(selectedColor < 0 ? 0 : selectedColor);
            color.ItemSelected += index =>
            {
                var paletteIndex = color.GetItemId((int)index);
                if (paletteIndex >= 0)
                    UpdateFaction(faction.Id, current => current with { Color = "#" + FactionPalette[paletteIndex].ToHtml(false) });
            };
            row.AddChild(color);
            var playable = new CheckBox { Text = "시작 가능", ButtonPressed = faction.Playable,
                Disabled = faction.Hidden };
            playable.Toggled += value => UpdateFaction(faction.Id, f => f with { Playable = value });
            row.AddChild(playable);
            var hidden = new CheckBox { Text = "숨은세력", ButtonPressed = faction.Hidden };
            hidden.Toggled += value =>
            {
                var sites = CurrentMap().Preset.Sites.Where(site => site.Kind is "city" or "port" or "gate").ToArray();
                UpdateFaction(faction.Id, current => current with
                {
                    Hidden = value, Playable = value ? false : current.Playable,
                    ArrivalYear = value && current.ArrivalYear == 0 ? _draft.StartYear + 1 : current.ArrivalYear,
                    ArrivalSiteId = value && current.ArrivalSiteId == 0 ? sites[0].Id : current.ArrivalSiteId,
                });
                if (value)
                {
                    _draft = _draft with
                    {
                        Cities = _draft.Cities.Select(city => city.OwnerFactionId == faction.Id
                            ? city with { OwnerFactionId = 0, Garrisons = [], Ships = [] } : city).ToArray(),
                        Generals = _draft.Generals.Select(general => general.FactionId == faction.Id
                            ? general with { SiteId = null } : general).ToArray(),
                    };
                    EnsureHiddenRuler(faction.Id, faction.RulerGeneralId);
                }
                ShowFactions();
            };
            row.AddChild(hidden);
            var year = new SpinBox { MinValue = _draft.StartYear, MaxValue = 9999,
                Value = Math.Max(_draft.StartYear, faction.ArrivalYear), CustomMinimumSize = new Vector2(100, 0), Editable = faction.Hidden };
            year.ValueChanged += value => UpdateFaction(faction.Id, f => f with { ArrivalYear = (int)value });
            row.AddChild(year);
            var target = new OptionButton { CustomMinimumSize = new Vector2(180, 0), Disabled = !faction.Hidden };
            foreach (var site in CurrentMap().Preset.Sites.Where(site => site.Kind is "city" or "port" or "gate"))
                target.AddItem(site.Name, site.Id);
            target.Select(Math.Max(0, target.GetItemIndex(faction.ArrivalSiteId)));
            target.ItemSelected += index => UpdateFaction(faction.Id,
                f => f with { ArrivalSiteId = target.GetItemId((int)index) });
            row.AddChild(target);
            var remove = new Button { Text = "삭제" };
            remove.Pressed += () =>
            {
                _draft = _draft with
                {
                    Factions = _draft.Factions.Where(f => f.Id != faction.Id).ToArray(),
                    Cities = _draft.Cities.Select(c => c.OwnerFactionId == faction.Id
                        ? c with { OwnerFactionId = 0, Garrisons = [], Ships = [] } : c).ToArray(),
                    Generals = _draft.Generals.Where(g => g.FactionId != faction.Id).ToArray(),
                };
                ShowFactions();
            };
            row.AddChild(remove);
        }
        var add = new Button { Text = "세력 추가" };
        add.Pressed += () =>
        {
            var id = _draft.Factions.Select(f => f.Id).DefaultIfEmpty().Max() + 1;
            var assigned = _draft.Generals.Select(general => general.GeneralId).ToHashSet();
            var rulerId = generals.FirstOrDefault(general => !assigned.Contains(general.Id.Value))?.Id.Value
                ?? generals[0].Id.Value;
            _draft = _draft with { Factions = [.. _draft.Factions,
                new ScenarioFactionEntry(id, "새 세력", rulerId, "#c9ad72")] };
            ShowFactions();
        };
        _body.AddChild(add);
    }

    private static ImageTexture FactionColorIcon(Color color)
    {
        using var image = Image.CreateEmpty(18, 18, false, Image.Format.Rgba8);
        image.Fill(color);
        return ImageTexture.CreateFromImage(image);
    }

    private void EnsureHiddenRuler(int factionId, int rulerId)
    {
        var generals = _draft.Generals.Where(general => general.GeneralId != rulerId).ToList();
        generals.Add(new ScenarioGeneralEntry(rulerId, factionId, null));
        _draft = _draft with { Generals = generals };
    }

    private void ShowGenerals()
    {
        ClearBody();
        var map = CurrentMap().Preset;
        var names = _source.Generals.ToDictionary(g => g.Id.Value, g => g.Name);
        var owned = _draft.Cities.Where(city => city.OwnerFactionId > 0
            && map.Sites.Any(site => site.Id == city.SiteId))
            .OrderBy(city => _draft.Factions.FirstOrDefault(faction => faction.Id == city.OwnerFactionId)?.Name)
            .ThenBy(city => map.Sites.First(site => site.Id == city.SiteId).Name).ToArray();
        _body.AddChild(new Label { Text = "먼저 세력 거점을 고른 뒤 장수를 등록·이동·제거하세요." });
        var spread = new Button { Text = "모든 세력 장수 자동 분산" };
        spread.Pressed += () =>
        {
            SpreadGeneralsAcrossOwnedSites();
            ShowGenerals();
            _message.Text = "군주는 현재 거점에 두고, 같은 세력의 나머지 장수를 소유 거점에 고르게 배치했습니다.";
        };
        _body.AddChild(spread);
        if (owned.Length == 0)
        {
            _body.AddChild(new Label { Text = "세력·거점 탭에서 소유 거점을 먼저 지정하세요." });
            return;
        }
        if (owned.All(city => city.SiteId != _selectedGeneralCityId))
            _selectedGeneralCityId = owned[0].SiteId;
        var cityList = new ItemList { CustomMinimumSize = new Vector2(0, 175),
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var city in owned)
        {
            var factionName = _draft.Factions.First(faction => faction.Id == city.OwnerFactionId).Name;
            var siteName = map.Sites.First(site => site.Id == city.SiteId).Name;
            var count = _draft.Generals.Count(general => general.SiteId == city.SiteId);
            cityList.AddItem($"{factionName} · {siteName} · 장수 {count}명");
        }
        cityList.Select(Array.FindIndex(owned, city => city.SiteId == _selectedGeneralCityId));
        cityList.ItemSelected += index =>
        {
            _selectedGeneralCityId = owned[(int)index].SiteId;
            ShowGenerals();
        };
        _body.AddChild(cityList);
        var selected = owned.First(city => city.SiteId == _selectedGeneralCityId);
        var selectedName = map.Sites.First(site => site.Id == selected.SiteId).Name;
        _body.AddChild(new HSeparator());
        _body.AddChild(new Label { Text = selectedName + " 주둔 장수" });
        var table = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddChild(table);
        foreach (var heading in new[] { "장수", "이동할 거점", "배치" }) table.AddChild(new Label { Text = heading });
        foreach (var general in _draft.Generals.Where(general => general.SiteId == selected.SiteId).ToArray())
        {
            table.AddChild(new Label { Text = names.GetValueOrDefault(general.GeneralId, general.GeneralId.ToString()),
                CustomMinimumSize = new Vector2(170, 0) });
            var destination = new OptionButton { CustomMinimumSize = new Vector2(190, 0) };
            foreach (var city in owned.Where(city => city.OwnerFactionId == selected.OwnerFactionId))
                destination.AddItem(map.Sites.First(site => site.Id == city.SiteId).Name, city.SiteId);
            destination.Select(destination.GetItemIndex(selected.SiteId));
            destination.ItemSelected += index =>
            {
                UpdateGeneral(general.GeneralId, current => current with { SiteId = destination.GetItemId((int)index) });
                ShowGenerals();
            };
            table.AddChild(destination);
            var remove = new Button { Text = "제거" };
            remove.Pressed += () =>
            {
                _draft = _draft with { Generals = _draft.Generals
                    .Where(current => current.GeneralId != general.GeneralId).ToArray() };
                ShowGenerals();
            };
            table.AddChild(remove);
        }
        _body.AddChild(new HSeparator());
        _body.AddChild(new Label { Text = "장수 등록 · 다른 거점의 장수도 이곳으로 이동할 수 있습니다." });
        var search = new LineEdit { PlaceholderText = "장수 이름 검색", Text = _generalSearch };
        _body.AddChild(search);
        var addRow = new HBoxContainer();
        _body.AddChild(addRow);
        var selection = new OptionButton { CustomMinimumSize = new Vector2(240, 0) };
        addRow.AddChild(selection);
        void FillAvailable(string query)
        {
            selection.Clear();
            foreach (var general in _source.Generals.Where(general => general.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                && _draft.Generals.All(current => current.GeneralId != general.Id.Value
                    || current.SiteId != selected.SiteId))
                .OrderBy(general => _draft.Generals.Any(current => current.GeneralId == general.Id.Value))
                .ThenBy(general => general.Name))
                selection.AddItem(general.Name, general.Id.Value);
            if (selection.ItemCount > 0) selection.Select(0);
        }
        FillAvailable(_generalSearch);
        search.TextChanged += value => { _generalSearch = value; FillAvailable(value); };
        var add = new Button { Text = "선택 장수 등록" };
        add.Pressed += () =>
        {
            if (selection.ItemCount == 0) return;
            var id = selection.GetSelectedId();
            if (_draft.Generals.Any(general => general.GeneralId == id))
                UpdateGeneral(id, general => general with { FactionId = selected.OwnerFactionId, SiteId = selected.SiteId });
            else _draft = _draft with { Generals = [.. _draft.Generals,
                new ScenarioGeneralEntry(id, selected.OwnerFactionId, selected.SiteId)] };
            ShowGenerals();
        };
        addRow.AddChild(add);
    }

    private void SpreadGeneralsAcrossOwnedSites()
    {
        var postings = _draft.Generals.ToArray();
        foreach (var faction in _draft.Factions.Where(faction => !faction.Hidden))
        {
            var sites = _draft.Cities.Where(city => city.OwnerFactionId == faction.Id)
                .Select(city => city.SiteId).OrderBy(id => id).ToArray();
            if (sites.Length == 0) continue;
            var ruler = postings.FirstOrDefault(general => general.GeneralId == faction.RulerGeneralId
                && general.FactionId == faction.Id);
            var home = ruler?.SiteId is { } id && sites.Contains(id) ? id : sites[0];
            var destinations = sites.Where(id => id != home).Append(home).ToArray();
            var number = 0;
            for (var index = 0; index < postings.Length; index++)
            {
                if (postings[index].FactionId != faction.Id
                    || postings[index].GeneralId == faction.RulerGeneralId) continue;
                postings[index] = postings[index] with { SiteId = destinations[number++ % destinations.Length] };
            }
        }
        _draft = _draft with { Generals = postings };
    }

    private void AddForceEditor(ScenarioCityEntry selected, MapEditorSite site, Action refresh)
    {
        var data = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data"));
        var catalog = new TroopCatalogLoader().LoadFromDirectory(data);
        var table = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddChild(table);
        table.AddChild(new Label { Text = "병종" });
        table.AddChild(new Label { Text = "주둔 병력" });
        table.AddChild(new Label { Text = "관리" });
        foreach (var troop in catalog.Where(troop => troop.Class != SanguoSLG.Core.Domain.TroopClass.Naval
            && selected.Garrisons.Any(force => force.TroopCode == troop.Code)))
        {
            table.AddChild(new Label { Text = troop.Name, CustomMinimumSize = new Vector2(190, 0) });
            var code = troop.Code;
            var field = new LineEdit { Text = selected.Garrisons.FirstOrDefault(force => force.TroopCode == code)?.Troops.ToString() ?? "0",
                PlaceholderText = "0", CustomMinimumSize = new Vector2(125, 0), MaxLength = 8 };
            field.TextChanged += value =>
            {
                if (!int.TryParse(value, out var count) || count < 0) return;
                UpdateCity(selected.SiteId, city => city with { Garrisons = city.Garrisons
                    .Where(force => force.TroopCode != code)
                    .Append(new ScenarioGarrisonEntry(code, count))
                    .Where(force => force.Troops > 0).ToArray() });
            };
            table.AddChild(field);
            var remove = new Button { Text = "제거" };
            remove.Pressed += () =>
            {
                UpdateCity(selected.SiteId, city => city with { Garrisons = city.Garrisons
                    .Where(force => force.TroopCode != code).ToArray() });
                refresh();
            };
            table.AddChild(remove);
        }
        var availableTroops = catalog.Where(troop => troop.Class != SanguoSLG.Core.Domain.TroopClass.Naval
            && selected.Garrisons.All(force => force.TroopCode != troop.Code)).ToArray();
        if (availableTroops.Length > 0)
        {
            var addRow = new HBoxContainer();
            var choice = new OptionButton();
            foreach (var troop in availableTroops) choice.AddItem(troop.Name);
            addRow.AddChild(choice);
            var add = new Button { Text = "병종 추가" };
            add.Pressed += () =>
            {
                var code = availableTroops[choice.Selected].Code;
                UpdateCity(selected.SiteId, city => city with { Garrisons = [.. city.Garrisons,
                    new ScenarioGarrisonEntry(code, 1000)] });
                refresh();
            };
            addRow.AddChild(add);
            _body.AddChild(addRow);
        }
        if (site.Kind != "port") return;
        _body.AddChild(new HSeparator());
        _body.AddChild(new Label { Text = "보유 선박" });
        var ships = new GridContainer { Columns = 3 };
        _body.AddChild(ships);
        ships.AddChild(new Label { Text = "선박" });
        ships.AddChild(new Label { Text = "보유 수량" });
        ships.AddChild(new Label { Text = "관리" });
        foreach (var troop in catalog.Where(troop => troop.Class == SanguoSLG.Core.Domain.TroopClass.Naval
            && selected.Ships.Any(ship => ship.ShipCode == troop.Code)))
        {
            ships.AddChild(new Label { Text = troop.Name, CustomMinimumSize = new Vector2(190, 0) });
            var code = troop.Code;
            var field = new LineEdit { Text = selected.Ships.FirstOrDefault(ship => ship.ShipCode == code)?.Count.ToString() ?? "0",
                PlaceholderText = "0", CustomMinimumSize = new Vector2(125, 0), MaxLength = 5 };
            field.TextChanged += value =>
            {
                if (!int.TryParse(value, out var count) || count < 0) return;
                UpdateCity(selected.SiteId, city => city with { Ships = city.Ships
                    .Where(ship => ship.ShipCode != code)
                    .Append(new ScenarioShipEntry(code, count))
                    .Where(ship => ship.Count > 0).ToArray() });
            };
            ships.AddChild(field);
            var remove = new Button { Text = "제거" };
            remove.Pressed += () =>
            {
                UpdateCity(selected.SiteId, city => city with { Ships = city.Ships
                    .Where(ship => ship.ShipCode != code).ToArray() });
                refresh();
            };
            ships.AddChild(remove);
        }
        var availableShips = catalog.Where(troop => troop.Class == SanguoSLG.Core.Domain.TroopClass.Naval
            && selected.Ships.All(ship => ship.ShipCode != troop.Code)).ToArray();
        if (availableShips.Length == 0) return;
        var addShipRow = new HBoxContainer();
        var shipChoice = new OptionButton();
        foreach (var troop in availableShips) shipChoice.AddItem(troop.Name);
        addShipRow.AddChild(shipChoice);
        var addShip = new Button { Text = "선박 추가" };
        addShip.Pressed += () =>
        {
            var code = availableShips[shipChoice.Selected].Code;
            UpdateCity(selected.SiteId, city => city with { Ships = [.. city.Ships,
                new ScenarioShipEntry(code, 1)] });
            refresh();
        };
        addShipRow.AddChild(addShip);
        _body.AddChild(addShipRow);
    }

    private void UpdateCity(int siteId, Func<ScenarioCityEntry, ScenarioCityEntry> update)
        => _draft = _draft with { Cities = _draft.Cities.Select(city => city.SiteId == siteId ? update(city) : city).ToArray() };
    private void UpdateFaction(int factionId, Func<ScenarioFactionEntry, ScenarioFactionEntry> update)
        => _draft = _draft with { Factions = _draft.Factions.Select(faction => faction.Id == factionId ? update(faction) : faction).ToArray() };
    private void UpdateGeneral(int generalId, Func<ScenarioGeneralEntry, ScenarioGeneralEntry> update)
        => _draft = _draft with { Generals = _draft.Generals.Select(general => general.GeneralId == generalId ? update(general) : general).ToArray() };

    private void AddText(string label, string value, Action<string> update)
    {
        var row = new HBoxContainer(); _body.AddChild(row);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(150, 0) });
        var field = new LineEdit { Text = value, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        field.TextChanged += value => update(value);
        field.FocusEntered += () => BeginNativeText(field, update);
        row.AddChild(field);
    }

    private void AddNumber(string label, int value, int min, int max, Action<int> update)
    {
        var row = new HBoxContainer(); _body.AddChild(row);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(150, 0) });
        var field = new SpinBox { MinValue = min, MaxValue = max, Value = value, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        field.ValueChanged += number => update((int)number);
        row.AddChild(field);
    }

    private static void AddCompactNumber(Container row, string label, int value, int min, int max, Action<int> update)
    {
        row.AddChild(new Label { Text = label });
        var field = new SpinBox { MinValue = min, MaxValue = max, Value = value, CustomMinimumSize = new Vector2(100, 0) };
        field.ValueChanged += number => update((int)number);
        row.AddChild(field);
    }

    private void SaveCurrent()
    {
        EndNativeText();
        try
        {
            var known = _source.Generals.Select(general => general.Id.Value).ToHashSet();
            var map = CurrentMap();
            _draft = ScenarioWallDefaults.Normalize(_draft, map.Preset, _source.Balance);
            var path = Path.Combine(_scenarioDirectory, _draft.Id + ".json");
            ScenarioDraftStore.Save(path, _draft, map.Preset, known);
            RefreshList();
            _message.Text = "저장 완료: " + path;
        }
        catch (Exception error) { _message.Text = "저장 불가: " + error.Message; }
    }

    private void RunQa()
    {
        try
        {
            var known = _source.Generals.Select(g => g.Id.Value).ToHashSet();
            var map = CurrentMap().Preset;
            var errors = _draft.Validate(map, known).ToList();
            var builtIn = ProjectSettings.GlobalizePath("res://assets/scenarios");
            var bundled = Directory.GetFiles(builtIn, "*.json");
            foreach (var scenarioPath in bundled)
            {
                var scenario = ScenarioDraftStore.Load(scenarioPath);
                errors.AddRange(scenario.Validate(_archive.Load(scenario.MapId, scenario.MapRevision).Preset, known));
            }
            var originalDraft = _draft;
            var historicalUsesRoot = true;
            foreach (var scenarioPath in bundled)
            {
                OpenScenario(scenarioPath);
                historicalUsesRoot &= _draft.MapId == "default-east-asia"
                    && _draft.MapRevision == _maps[0].Revision
                    && ReferenceEquals(CurrentMap(), _maps[0])
                    && _draft.Validate(_maps[0].Preset, known).Count == 0;
            }
            _draft = originalDraft;
            var legacy = _archive.SaveVersion("legacy-east-asia", "옛 편집 지도", map);
            _draft = originalDraft with { MapId = legacy.Id, MapRevision = legacy.Revision };
            var legacyRebased = ApplyCurrentRootMap()
                && _draft.MapId == "default-east-asia" && _draft.MapRevision == _maps[0].Revision;
            _draft = originalDraft;
            var otherMap = _maps.FirstOrDefault(candidate => candidate.Id != _draft.MapId);
            var mapSwitchPassed = true;
            if (otherMap is not null)
            {
                SwitchMap(otherMap);
                var related = new[] { "default-east-asia", "legacy-east-asia" }.Contains(originalDraft.MapId)
                    && new[] { "default-east-asia", "legacy-east-asia" }.Contains(otherMap.Id);
                mapSwitchPassed = related
                    ? originalDraft.Cities.Where(city => _draft.Cities.Any(next => next.SiteId == city.SiteId))
                        .All(city => _draft.Cities.First(next => next.SiteId == city.SiteId).OwnerFactionId == city.OwnerFactionId)
                    : _draft.Cities.All(city => city.OwnerFactionId == 0)
                        && _draft.Generals.All(general => general.SiteId is null);
                _draft = originalDraft;
            }
            ShowFactions();
            var factionColorUi = _body.FindChildren("*", "OptionButton", true, false).OfType<OptionButton>()
                .Any(choice => choice.ItemCount == 60 || choice.ItemCount == 61);
            var hiddenFactionUi = _body.FindChildren("*", "CheckBox", true, false)
                .OfType<CheckBox>().Any(check => check.Text == "숨은세력");
            ShowCities();
            var cityMapUi = _body.GetChildren().OfType<ScenarioSiteMap>().Any();
            ShowGenerals();
            var generalSearchUi = _body.GetChildren().OfType<LineEdit>()
                .Any(field => field.PlaceholderText == "장수 이름 검색");
            var generalCityListUi = _body.GetChildren().OfType<ItemList>().Any();
            var generalCrudPassed = false;
            var generalCity = _draft.Cities.FirstOrDefault(city => city.OwnerFactionId > 0);
            if (generalCity is not null)
            {
                _selectedGeneralCityId = generalCity.SiteId;
                ShowGenerals();
                var before = _draft.Generals.Count(general => general.SiteId == generalCity.SiteId);
                var addGeneral = _body.FindChildren("*", "Button", true, false).OfType<Button>()
                    .LastOrDefault(button => button.Text == "선택 장수 등록" && !button.IsQueuedForDeletion());
                addGeneral?.EmitSignal(Button.SignalName.Pressed);
                var after = _draft.Generals.Count(general => general.SiteId == generalCity.SiteId);
                var removeGeneral = _body.FindChildren("*", "Button", true, false).OfType<Button>()
                    .LastOrDefault(button => button.Text == "제거" && !button.IsQueuedForDeletion());
                removeGeneral?.EmitSignal(Button.SignalName.Pressed);
                generalCrudPassed = after == before + 1
                    && _draft.Generals.Count(general => general.SiteId == generalCity.SiteId) == before;
                _draft = originalDraft;
            }
            var forceTabRemoved = FindChildren("*", "Button", true, false).OfType<Button>()
                .All(button => button.Text != "병력·선박");
            var ownedCity = _draft.Cities.FirstOrDefault(city => city.OwnerFactionId > 0);
            var cityForceUi = false;
            var forceCrudPassed = false;
            if (ownedCity is not null)
            {
                var before = ownedCity.Garrisons.Count;
                _selectedCityId = ownedCity.SiteId;
                ShowCities();
                var add = _body.FindChildren("*", "Button", true, false).OfType<Button>()
                    .FirstOrDefault(button => button.Text == "병종 추가");
                add?.EmitSignal(Button.SignalName.Pressed);
                forceCrudPassed = _draft.Cities.First(city => city.SiteId == ownedCity.SiteId)
                    .Garrisons.Count == before + 1;
                _selectedCityId = ownedCity.SiteId;
                ShowCities();
                cityForceUi = _body.FindChildren("*", "Button", true, false).OfType<Button>()
                    .Any(button => button.Text == "병종 추가");
                _draft = originalDraft;
            }
            ShowBasics();
            var path = Path.Combine(Path.GetTempPath(), "scenario-editor-qa-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                ScenarioDraftStore.Save(path, _draft, map, known);
                var restored = ScenarioDraftStore.Load(path);
                var editedPath = ProjectSettings.GlobalizePath("user://map-presets/east-asia.json");
                var currentMapSelected = !File.Exists(editedPath)
                    || (_maps[0].Id == "default-east-asia"
                        && _maps[0].Revision == ScenarioMapArchive.RevisionOf(
                            ScenarioMapPresetStore.Load(editedPath)));
                var defaultSiteIds = _maps[0].Preset.Sites.Select(site => site.Id).ToHashSet();
                var factionPlacementCompatible = bundled.All(scenarioPath =>
                {
                    var scenario = ScenarioDraftStore.Load(scenarioPath);
                    return scenario.Cities.Where(city => city.OwnerFactionId > 0)
                        .All(city => defaultSiteIds.Contains(city.SiteId))
                        && scenario.Generals.Where(general => general.SiteId.HasValue)
                            .All(general => defaultSiteIds.Contains(general.SiteId!.Value));
                });
                var passed = bundled.Length == 6 && _list.ItemCount >= 6
                    && factionColorUi && hiddenFactionUi && cityMapUi && generalSearchUi && generalCityListUi
                    && generalCrudPassed && forceTabRemoved
                    && cityForceUi && forceCrudPassed
                    && errors.Count == 0 && mapSwitchPassed && currentMapSelected
                    && historicalUsesRoot && legacyRebased
                    && factionPlacementCompatible
                    && restored.MapRevision == _draft.MapRevision
                    && restored.Cities.Count == _draft.Cities.Count;
                if (!passed) GD.Print($"[scenario-editor-qa-detail] history={historicalUsesRoot} legacy={legacyRebased} switch={mapSwitchPassed} placement={factionPlacementCompatible} root={currentMapSelected} generals={generalCrudPassed} forces={forceCrudPassed} cityUi={cityForceUi} factionUi={factionColorUi && hiddenFactionUi} cityMapUi={cityMapUi} generalUi={generalSearchUi && generalCityListUi} errors={errors.Count}");
                GD.Print($"[scenario-editor-qa] passed={passed} maps={_maps.Count} cities={_draft.Cities.Count} errors={errors.Count}");
                GetTree().Quit(passed ? 0 : 1);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
        catch (Exception error)
        {
            GD.PushError("[scenario-editor-qa] " + error);
            GetTree().Quit(1);
        }
    }
}
