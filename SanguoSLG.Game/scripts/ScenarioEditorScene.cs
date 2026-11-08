using Godot;
using SanguoSLG.Core.Data;

namespace SanguoSLG.Game;

public sealed partial class ScenarioEditorScene : Control
{
    private readonly List<ScenarioMapVersion> _maps = [];
    private ScenarioMapArchive _archive = null!;
    private Scenario _source = null!;
    private ScenarioDraft _draft = null!;
    private string _savedDraftSnapshot = "";
    private ItemList _list = null!;
    private VBoxContainer _body = null!;
    private Label _message = null!;
    private Button _deleteButton = null!;
    private string _scenarioDirectory = "";
    private int _selectedCityId;
    private int _selectedGeneralCityId;
    private int _selectedDescriptionFactionId;
    private string _generalSearch = "";
    private static readonly Color[] FactionPalette = Enumerable.Range(0, 60)
        .Select(index => Color.FromHsv((index % 12) / 12f, 0.48f + (index / 12) * 0.105f,
            0.76f + (index / 12 % 2) * 0.12f)).ToArray();
    private readonly Dictionary<string, ImageTexture> _factionColorIcons = [];

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
        _draft = ScenarioDraftApplier.CreateSeed("custom-190", "작성 중", _source, _maps[0]);
        _savedDraftSnapshot = SnapshotDraft();
        BuildUi();
        RefreshList();
        if (_list.ItemCount > 0)
        {
            _list.Select(0);
            OpenScenario(_list.GetItemMetadata(0).AsString());
        }
        else ShowBasics();
        if (OS.GetCmdlineUserArgs().Contains("--scenarioeditorqa")) CallDeferred(nameof(RunQa));
    }

    public override void _ExitTree()
    {
        foreach (var icon in _factionColorIcons.Values) icon.Dispose();
        _factionColorIcons.Clear();
    }

    private void LoadMaps()
    {
        var version = ScenarioRootMapSource.OpenVersion(_archive);
        _maps.Add(version);
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
        GeneralWorkspaceStyle.AddBackground(this);
        var frame = new MarginContainer();
        frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        frame.AddThemeConstantOverride("margin_left", 20);
        frame.AddThemeConstantOverride("margin_right", 20);
        frame.AddThemeConstantOverride("margin_top", 16);
        frame.AddThemeConstantOverride("margin_bottom", 16);
        AddChild(frame);
        var vertical = new VBoxContainer();
        frame.AddChild(vertical);
        GeneralWorkspaceStyle.Header(vertical, "시나리오 에디터", "시대와 세력, 거점과 장수의 배치를 설계합니다");
        var header = new HBoxContainer();
        vertical.AddChild(header);
        header.AddChild(new Label { Text = "시나리오 작업", SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center });
        var newButton = new Button { Text = "+ 시나리오 만들기" };
        newButton.Pressed += CreateNew;
        header.AddChild(newButton);
        var cloneButton = new Button { Text = "복제" };
        cloneButton.Pressed += CloneCurrent;
        header.AddChild(cloneButton);
        _deleteButton = new Button { Text = "삭제", Disabled = true };
        _deleteButton.Pressed += ConfirmDeleteCurrent;
        header.AddChild(_deleteButton);
        var saveButton = new Button { Text = "검증 후 저장" };
        saveButton.Pressed += SaveCurrent;
        header.AddChild(saveButton);
        var closeButton = new Button { Text = ScenarioStartMenu.FromStartMenu ? "← 메인 메뉴" : "닫기",
            CustomMinimumSize = ScenarioStartMenu.FromStartMenu ? new Vector2(140, 36) : Vector2.Zero };
        closeButton.Pressed += RequestClose;
        header.AddChild(closeButton);
        vertical.AddChild(new HSeparator());
        var columns = new HSplitContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        vertical.AddChild(columns);
        var left = GeneralWorkspaceStyle.Column(columns, "시나리오 목록", 255);
        _list = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        _list.ItemSelected += index =>
        {
            var path = _list.GetItemMetadata((int)index).AsString();
            if (!string.IsNullOrEmpty(path)) OpenScenario(path);
        };
        left.AddChild(_list);
        var right = GeneralWorkspaceStyle.Column(columns, "시나리오 기록부");
        var tabs = new HBoxContainer();
        right.AddChild(tabs);
        AddTab(tabs, "기본 정보·지도", ShowBasics);
        AddTab(tabs, "세력", ShowFactions);
        AddTab(tabs, "세력·거점", ShowCities);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        right.AddChild(scroll);
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(_body);
        _message = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        var footer = GeneralWorkspaceStyle.Footer(vertical);
        footer.AddChild(new Label { Text = "시나리오를 검증한 뒤 저장하세요.",
            VerticalAlignment = VerticalAlignment.Center });
        _message.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footer.AddChild(_message);
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
        _deleteButton.Disabled = true;
        var builtIn = ProjectSettings.GlobalizePath("res://assets/scenarios");
        var builtInIds = Directory.Exists(builtIn)
            ? Directory.EnumerateFiles(builtIn, "*.json").Select(ScenarioDraftStore.Load)
                .Select(draft => draft.Id).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];
        foreach (var source in ScenarioDraftCatalog.LoadEffective(builtIn, _scenarioDirectory))
        {
            try
            {
                var isBuiltInScenario = builtInIds.Contains(source.Draft.Id);
                var index = _list.AddItem((isBuiltInScenario ? "기본 · " : "") + source.Draft.Title);
                _list.SetItemMetadata(index, source.Path);
            }
            catch (Exception error) { GD.PushWarning($"시나리오 목록: {source.Path}: {error.Message}"); }
        }
    }

    private void UpdateDeleteButton(string path)
    {
        var builtIn = ProjectSettings.GlobalizePath("res://assets/scenarios");
        _deleteButton.Disabled = string.IsNullOrEmpty(path)
            || File.Exists(Path.Combine(builtIn, Path.GetFileName(path)))
            || !File.Exists(path);
    }

    private void ConfirmDeleteCurrent()
    {
        var selected = _list.GetSelectedItems();
        if (selected.Length != 1) return;
        var path = _list.GetItemMetadata(selected[0]).AsString();
        UpdateDeleteButton(path);
        if (_deleteButton.Disabled) return;
        var dialog = new ConfirmationDialog
        {
            Title = "시나리오 삭제",
            DialogText = $"{ScenarioDraftStore.Load(path).Title} 시나리오를 삭제하시겠습니까?\n지도와 저장된 게임은 유지됩니다.",
            OkButtonText = "삭제",
        };
        AddChild(dialog);
        dialog.Confirmed += () =>
        {
            try
            {
                ScenarioDraftStore.DeleteUserScenario(path, _scenarioDirectory,
                    ProjectSettings.GlobalizePath("res://assets/scenarios"));
                RefreshList();
                _draft = ScenarioDraftApplier.CreateSeed("custom-190", "작성 중", _source, _maps[0]);
                _savedDraftSnapshot = SnapshotDraft();
                ShowBasics();
                _message.Text = "시나리오를 삭제했습니다.";
            }
            catch (Exception error) { _message.Text = "삭제 불가: " + error.Message; }
        };
        dialog.Confirmed += dialog.QueueFree;
        dialog.Canceled += dialog.QueueFree;
        dialog.PopupCentered();
    }

    private void OpenScenario(string path)
    {
        UpdateDeleteButton(path);
        var previous = _draft;
        try
        {
            RefreshCurrentRootMap();
            _draft = ScenarioDraftStore.Load(path);
            EnsureMapAvailable();
            var updated = ApplyCurrentRootMap();
            _draft = ScenarioWallDefaults.Normalize(_draft, CurrentMap().Preset, _source.Balance);
            _savedDraftSnapshot = SnapshotDraft();
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
        _maps.Insert(0, version);
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
        RefreshList();
        RefreshCurrentRootMap();
        _draft = ScenarioDraftApplier.CreateSeed("scenario-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
            "작성 중", _source, _maps[0]);
        var index = _list.AddItem("작성 중 · 저장 전");
        _list.SetItemMetadata(index, "");
        _list.Select(index);
        _deleteButton.Disabled = true;
        Callable.From(() => _list.EnsureCurrentIsVisible()).CallDeferred();
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
        foreach (var child in _body.GetChildren()) child.QueueFree();
        CallDeferred(nameof(LimitDropdownHeights));
    }

    private void LimitDropdownHeights()
    {
        foreach (var option in FindChildren("*", "OptionButton", true, false).OfType<OptionButton>())
            if (!option.IsQueuedForDeletion()) option.GetPopup().MaxSize = new Vector2I(4096, 200);
    }

    private void ShowBasics()
    {
        ClearBody();
        var basics = GeneralWorkspaceStyle.Section(_body, "시나리오 기본 정보");
        basics.AddChild(new Label { Text = "저장 식별자 · 자동 생성: " + _draft.Id,
            TooltipText = "파일을 구별하는 내부 값입니다. 제목만 바꾸어도 됩니다." });
        AddText(basics, "제목", _draft.Title, value => _draft = _draft with { Title = value });
        AddDescription(basics, "설명", _draft.Description, value => _draft = _draft with { Description = value });
        var dateRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        dateRow.AddThemeConstantOverride("separation", 16);
        basics.AddChild(dateRow);
        AddCompactNumber(dateRow, "시작 연도", _draft.StartYear, 1, 9999,
            value => _draft = _draft with { StartYear = value });
        AddCompactNumber(dateRow, "월", _draft.StartMonth, 1, 12,
            value => _draft = _draft with { StartMonth = value });
        AddCompactNumber(dateRow, "일", _draft.StartDay, 1, 30,
            value => _draft = _draft with { StartDay = value });

        var mapSection = GeneralWorkspaceStyle.Section(_body, "시나리오 지도 설정");
        var mapChoiceRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        mapChoiceRow.AddChild(new Label { Text = "사용 지도", CustomMinimumSize = new Vector2(110, 0),
            VerticalAlignment = VerticalAlignment.Center });
        var choices = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var currentIndex = 0;
        for (var i = 0; i < _maps.Count; i++)
        {
            var option = _maps[i];
            choices.AddItem($"{option.Title} · 거점 {option.Preset.Sites.Count(site => site.Kind is "city" or "port" or "gate")}");
            if (option.Id == _draft.MapId && option.Revision == _draft.MapRevision) currentIndex = i;
        }
        choices.Select(currentIndex);
        choices.ItemSelected += index => SwitchMap(_maps[(int)index]);
        GeneralWorkspaceStyle.StyleOptionButton(choices, 200);
        mapChoiceRow.AddChild(choices);
        mapSection.AddChild(mapChoiceRow);
        var map = CurrentMap();
        var displayName = map.Id == "default-east-asia" ? "기본 지도" : map.Title;
        var siteCount = map.Preset.Sites.Count(site => site.Kind is "city" or "port" or "gate");
        var modified = MapModifiedAt(map);
        mapSection.AddChild(new Label
        {
            Text = $"지도 이름  {displayName}     크기  {map.Preset.Width} × {map.Preset.Height}     거점  {siteCount}개"
                + (modified is null ? "" : $"     마지막 수정  {modified:yyyy-MM-dd HH:mm}"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var preview = new ScenarioSiteMap(map.Preset, _draft.Cities, _draft.Factions, 0, _ => { })
        {
            CustomMinimumSize = new Vector2(540, 250),
            MouseFilter = MouseFilterEnum.Ignore,
            TooltipText = "선택한 지도의 읽기 전용 미리보기",
        };
        mapSection.AddChild(preview);
        var latestDefault = _maps.FirstOrDefault(candidate => candidate.Id == "default-east-asia");
        if (latestDefault is not null && (latestDefault.Id != map.Id || latestDefault.Revision != map.Revision))
        {
            var useEdited = new Button { Text = "현재 편집 중인 기본 지도 적용" };
            useEdited.Pressed += () => SwitchMap(latestDefault);
            mapSection.AddChild(useEdited);
        }
        var missing = _draft.Generals.Where(g => g.SiteId is { } id && !map.Preset.Sites.Any(site => site.Id == id))
            .Select(g => g.GeneralId).ToArray();
        mapSection.AddChild(new Label { Text = missing.Length == 0 ? "미배치 오류 없음" : "삭제된 거점의 장수: " + string.Join(", ", missing) });
    }

    private DateTime? MapModifiedAt(ScenarioMapVersion map)
    {
        var path = map.Id == "default-east-asia"
            ? ProjectSettings.GlobalizePath("user://map-presets/east-asia.json")
            : Path.Combine(ProjectSettings.GlobalizePath("user://map-presets/custom"), map.Id + ".json");
        return File.Exists(path) ? File.GetLastWriteTime(path) : null;
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
        var details = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        details.AddThemeConstantOverride("separation", 16);
        _body.AddChild(details);
        var cityDetails = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        details.AddChild(cityDetails);
        var generalDetails = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        details.AddChild(generalDetails);
        cityDetails.AddChild(new Label { Text = $"{site.Name} · {(site.Kind == "port" ? "항구" : site.Kind == "gate" ? "관문" : "성")}" });
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
        cityDetails.AddChild(owner);
        var resources = new HBoxContainer();
        cityDetails.AddChild(resources);
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
            cityDetails.AddChild(new HSeparator());
            cityDetails.AddChild(new Label { Text = "주둔 병력·선박" });
            AddForceEditor(city, site, ShowCities, cityDetails);
        }
        ShowGeneralsForCity(generalDetails, city);
    }

    private void ShowFactions()
    {
        ClearBody();
        var generals = _source.Generals.OrderBy(g => g.Name).ToArray();
        if (_draft.Factions.All(faction => faction.Id != _selectedDescriptionFactionId))
            _selectedDescriptionFactionId = _draft.Factions.FirstOrDefault()?.Id ?? 0;

        var columns = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 14);
        _body.AddChild(columns);
        var listPanel = new VBoxContainer { CustomMinimumSize = new Vector2(285, 520),
            SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddChild(listPanel);
        var listHeader = new HBoxContainer();
        listPanel.AddChild(listHeader);
        listHeader.AddChild(new Label { Text = "세력 목록", SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center });
        var add = new Button { Text = "+ 세력 추가" };
        add.Pressed += () =>
        {
            var id = _draft.Factions.Select(f => f.Id).DefaultIfEmpty().Max() + 1;
            var assigned = _draft.Generals.Select(general => general.GeneralId).ToHashSet();
            var rulerId = generals.FirstOrDefault(general => !assigned.Contains(general.Id.Value))?.Id.Value
                ?? generals[0].Id.Value;
            _draft = _draft with { Factions = [.. _draft.Factions,
                new ScenarioFactionEntry(id, "새 세력", rulerId, "#c9ad72")] };
            _selectedDescriptionFactionId = id;
            ShowFactions();
        };
        listHeader.AddChild(add);
        var factionScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        GeneralWorkspaceStyle.StyleScrollContainer(factionScroll);
        listPanel.AddChild(factionScroll);
        var factionList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        factionScroll.AddChild(factionList);
        foreach (var entry in _draft.Factions)
        {
            var rulerName = generals.FirstOrDefault(general => general.Id.Value == entry.RulerGeneralId)?.Name ?? "군주 미지정";
            var status = entry.Hidden ? $"{Math.Max(_draft.StartYear, entry.ArrivalYear)}년 등장"
                : entry.Playable ? "시작 가능" : "시작 불가";
            var item = new Button
            {
                Text = $"{entry.Name}\n{rulerName} · {status}",
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, 58),
                ToggleMode = true,
                ButtonPressed = entry.Id == _selectedDescriptionFactionId,
            };
            item.Pressed += () => { _selectedDescriptionFactionId = entry.Id; ShowFactions(); };
            factionList.AddChild(item);
        }
        if (_draft.Factions.Count == 0)
        {
            factionList.AddChild(new Label { Text = "등록된 세력이 없습니다.\n세력을 추가하여 시작하세요.",
                HorizontalAlignment = HorizontalAlignment.Center });
            return;
        }

        var faction = _draft.Factions.First(entry => entry.Id == _selectedDescriptionFactionId);
        var detailHost = new VBoxContainer();
        var details = GeneralWorkspaceStyle.Section(detailHost, "선택한 세력 상세 편집");
        var detailPanel = details.GetParent<PanelContainer>();
        detailHost.RemoveChild(detailPanel);
        detailPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        detailPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        columns.AddChild(detailPanel);

        var nameRow = AddFactionField(details, "세력 이름");
        var name = new LineEdit { Text = faction.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        name.TextChanged += value => UpdateFaction(faction.Id, current => current with { Name = value });
        nameRow.AddChild(name);
        NativeTextInputDialog.Attach(name);

        var rulerRow = AddFactionField(details, "군주");
        var ruler = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var general in generals) ruler.AddItem(general.Name, general.Id.Value);
        ruler.Select(Math.Max(0, ruler.GetItemIndex(faction.RulerGeneralId)));
        ruler.ItemSelected += index =>
        {
            var generalId = ruler.GetItemId((int)index);
            UpdateFaction(faction.Id, current => current with { RulerGeneralId = generalId });
            if (faction.Hidden) EnsureHiddenRuler(faction.Id, generalId);
        };
        GeneralWorkspaceStyle.StyleOptionButton(ruler, 200);
        rulerRow.AddChild(ruler);

        var colorRow = AddFactionField(details, "세력 색상");
        var color = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = "60가지 세력 색 중 선택" };
        var currentColor = new Color(faction.Color);
        var selectedColor = Array.FindIndex(FactionPalette, candidate => candidate.IsEqualApprox(currentColor));
        if (selectedColor < 0) color.AddIconItem(FactionColorIcon(currentColor), "현재 색", -1);
        for (var paletteIndex = 0; paletteIndex < FactionPalette.Length; paletteIndex++)
            color.AddIconItem(FactionColorIcon(FactionPalette[paletteIndex]), $"색상 {paletteIndex + 1:00}", paletteIndex);
        color.Select(selectedColor < 0 ? 0 : selectedColor);
        color.ItemSelected += index =>
        {
            var paletteIndex = color.GetItemId((int)index);
            if (paletteIndex >= 0) UpdateFaction(faction.Id,
                current => current with { Color = "#" + FactionPalette[paletteIndex].ToHtml(false) });
        };
        GeneralWorkspaceStyle.StyleOptionButton(color, 200);
        colorRow.AddChild(color);

        var playable = new CheckBox { Text = "새 게임에서 선택 가능한 세력", ButtonPressed = faction.Playable,
            Disabled = faction.Hidden };
        playable.Toggled += value => UpdateFaction(faction.Id, current => current with { Playable = value });
        details.AddChild(playable);

        var arrivalRow = AddFactionField(details, "등장 방식");
        var arrivalMode = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        arrivalMode.AddItem("시작부터 등장", 0);
        arrivalMode.AddItem("지정 연도에 등장", 1);
        arrivalMode.Select(faction.Hidden ? 1 : 0);
        arrivalRow.AddChild(arrivalMode);
        var year = new SpinBox { MinValue = _draft.StartYear, MaxValue = 9999,
            Value = Math.Max(_draft.StartYear, faction.ArrivalYear), CustomMinimumSize = new Vector2(120, 0),
            Editable = faction.Hidden };
        year.ValueChanged += value => UpdateFaction(faction.Id, current => current with { ArrivalYear = (int)value });
        arrivalRow.AddChild(year);
        arrivalMode.ItemSelected += index => SetFactionHidden(faction, (int)index == 1);

        var targetRow = AddFactionField(details, "우선 출현 거점");
        var target = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, Disabled = !faction.Hidden };
        foreach (var site in CurrentMap().Preset.Sites.Where(site => site.Kind is "city" or "port" or "gate"))
            target.AddItem(site.Name, site.Id);
        target.Select(Math.Max(0, target.GetItemIndex(faction.ArrivalSiteId)));
        target.ItemSelected += index => UpdateFaction(faction.Id,
            current => current with { ArrivalSiteId = target.GetItemId((int)index) });
        GeneralWorkspaceStyle.StyleOptionButton(target, 200);
        targetRow.AddChild(target);

        details.AddChild(new Label { Text = "세력 설명" });
        var description = new TextEdit { Name = "FactionDescriptionEditor", Text = faction.Description,
            CustomMinimumSize = new Vector2(0, 125), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary };
        description.TextChanged += () => UpdateFaction(faction.Id,
            current => current with { Description = description.Text });
        details.AddChild(description);
        NativeTextInputDialog.Attach(description);

        var remove = new Button { Text = "선택한 세력 삭제" };
        remove.Pressed += () => ConfirmDeleteFaction(faction);
        details.AddChild(remove);
    }

    private static HBoxContainer AddFactionField(VBoxContainer parent, string label)
    {
        var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(125, 0),
            VerticalAlignment = VerticalAlignment.Center });
        parent.AddChild(row);
        return row;
    }

    private void SetFactionHidden(ScenarioFactionEntry faction, bool hidden)
    {
        var sites = CurrentMap().Preset.Sites.Where(site => site.Kind is "city" or "port" or "gate").ToArray();
        UpdateFaction(faction.Id, current => current with
        {
            Hidden = hidden,
            Playable = hidden ? false : current.Playable,
            ArrivalYear = hidden && current.ArrivalYear == 0 ? _draft.StartYear + 1 : current.ArrivalYear,
            ArrivalSiteId = hidden && current.ArrivalSiteId == 0 && sites.Length > 0 ? sites[0].Id : current.ArrivalSiteId,
        });
        if (hidden)
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
    }

    private void ConfirmDeleteFaction(ScenarioFactionEntry faction)
    {
        var dialog = new ConfirmationDialog { Title = "세력 삭제", DialogText = $"'{faction.Name}' 세력을 삭제합니까?\n거점 소유권과 소속 장수 배치도 함께 해제됩니다.",
            OkButtonText = "삭제", CancelButtonText = "취소" };
        dialog.Confirmed += () =>
        {
            _draft = _draft with
            {
                Factions = _draft.Factions.Where(entry => entry.Id != faction.Id).ToArray(),
                Cities = _draft.Cities.Select(city => city.OwnerFactionId == faction.Id
                    ? city with { OwnerFactionId = 0, Garrisons = [], Ships = [] } : city).ToArray(),
                Generals = _draft.Generals.Where(general => general.FactionId != faction.Id).ToArray(),
            };
            _selectedDescriptionFactionId = _draft.Factions.FirstOrDefault()?.Id ?? 0;
            ShowFactions();
        };
        dialog.Canceled += dialog.QueueFree;
        dialog.Confirmed += dialog.QueueFree;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(520, 220));
    }

    private ImageTexture FactionColorIcon(Color color)
    {
        var key = color.ToHtml(false);
        if (_factionColorIcons.TryGetValue(key, out var icon)) return icon;
        using var image = Image.CreateEmpty(18, 18, false, Image.Format.Rgba8);
        image.Fill(color);
        icon = ImageTexture.CreateFromImage(image);
        _factionColorIcons[key] = icon;
        return icon;
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
        NativeTextInputDialog.Attach(search);
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

    private void ShowGeneralsForCity(VBoxContainer host, ScenarioCityEntry city)
    {
        host.AddThemeConstantOverride("separation", 3);
        var posted = _draft.Generals.Where(general => general.SiteId == city.SiteId).ToArray();
        host.AddChild(new Label { Text = $"장수 배치 · {CurrentMap().Preset.Sites.First(site => site.Id == city.SiteId).Name}  ({posted.Length}명)" });
        if (city.OwnerFactionId == 0)
        {
            host.AddChild(new Label { Text = "세력을 지정하면 장수를 배치할 수 있습니다." });
            return;
        }
        var names = _source.Generals.ToDictionary(general => general.Id.Value, general => general.Name);
        var rosterScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 155),
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        host.AddChild(rosterScroll);
        var roster = new GridContainer { Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        roster.AddThemeConstantOverride("h_separation", 8);
        roster.AddThemeConstantOverride("v_separation", 2);
        rosterScroll.AddChild(roster);
        foreach (var general in posted)
        {
            var name = new Label { Text = names.GetValueOrDefault(general.GeneralId, general.GeneralId.ToString()),
                SizeFlagsHorizontal = SizeFlags.ExpandFill };
            name.AddThemeFontSizeOverride("font_size", 13);
            roster.AddChild(name);
            var remove = new Button { Text = "제거", CustomMinimumSize = new Vector2(46, 24) };
            remove.Pressed += () =>
            {
                _draft = _draft with { Generals = _draft.Generals.Where(value => value.GeneralId != general.GeneralId).ToArray() };
                ShowCities();
            };
            roster.AddChild(remove);
        }
        if (posted.Length % 2 != 0)
        {
            roster.AddChild(new Control());
            roster.AddChild(new Control());
        }
        host.AddChild(new HSeparator());
        var search = new LineEdit { PlaceholderText = "장수 검색", Text = _generalSearch };
        host.AddChild(search);
        NativeTextInputDialog.Attach(search);
        var row = new HBoxContainer();
        host.AddChild(row);
        var selection = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(selection);
        void Fill(string query)
        {
            selection.Clear();
            foreach (var general in _source.Generals.Where(general => general.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                && _draft.Generals.All(current => current.GeneralId != general.Id.Value || current.SiteId != city.SiteId))
                .OrderBy(general => general.Name))
                selection.AddItem(general.Name, general.Id.Value);
            if (selection.ItemCount > 0) selection.Select(0);
        }
        Fill(_generalSearch);
        search.TextChanged += value => { _generalSearch = value; Fill(value); };
        var add = new Button { Text = "장수 등록" };
        add.Pressed += () =>
        {
            if (selection.ItemCount == 0) return;
            var id = selection.GetSelectedId();
            if (_draft.Generals.Any(general => general.GeneralId == id))
                UpdateGeneral(id, general => general with { FactionId = city.OwnerFactionId, SiteId = city.SiteId });
            else _draft = _draft with { Generals = [.. _draft.Generals,
                new ScenarioGeneralEntry(id, city.OwnerFactionId, city.SiteId)] };
            ShowCities();
        };
        row.AddChild(add);
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

    private void AddForceEditor(ScenarioCityEntry selected, MapEditorSite site, Action refresh, VBoxContainer parent)
    {
        var data = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data"));
        var catalog = new TroopCatalogLoader().LoadFromDirectory(data);
        var table = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        parent.AddChild(table);
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
            parent.AddChild(addRow);
        }
        if (site.Kind != "port") return;
        parent.AddChild(new HSeparator());
        parent.AddChild(new Label { Text = "보유 선박" });
        var ships = new GridContainer { Columns = 3 };
        parent.AddChild(ships);
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
        parent.AddChild(addShipRow);
    }

    private void UpdateCity(int siteId, Func<ScenarioCityEntry, ScenarioCityEntry> update)
        => _draft = _draft with { Cities = _draft.Cities.Select(city => city.SiteId == siteId ? update(city) : city).ToArray() };
    private void UpdateFaction(int factionId, Func<ScenarioFactionEntry, ScenarioFactionEntry> update)
        => _draft = _draft with { Factions = _draft.Factions.Select(faction => faction.Id == factionId ? update(faction) : faction).ToArray() };
    private void UpdateGeneral(int generalId, Func<ScenarioGeneralEntry, ScenarioGeneralEntry> update)
        => _draft = _draft with { Generals = _draft.Generals.Select(general => general.GeneralId == generalId ? update(general) : general).ToArray() };

    private void AddText(string label, string value, Action<string> update)
        => AddText(_body, label, value, update);

    private static void AddText(VBoxContainer parent, string label, string value, Action<string> update)
    {
        var row = new HBoxContainer(); parent.AddChild(row);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(150, 0) });
        var field = new LineEdit { Name = "ScenarioTitleEditor", Text = value,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        field.TextChanged += text => update(text);
        row.AddChild(field);
        NativeTextInputDialog.Attach(field);
    }

    private void AddDescription(string label, string value, Action<string> update)
        => AddDescription(_body, label, value, update);

    private static void AddDescription(VBoxContainer parent, string label, string value, Action<string> update)
    {
        var row = new HBoxContainer();
        parent.AddChild(row);
        row.AddChild(new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(150, 0),
            VerticalAlignment = VerticalAlignment.Top
        });
        var field = new TextEdit
        {
            Name = "ScenarioDescriptionEditor",
            Text = value,
            CustomMinimumSize = new Vector2(0, 108),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            WrapMode = TextEdit.LineWrappingMode.Boundary
        };
        field.TextChanged += () => update(field.Text);
        row.AddChild(field);
        NativeTextInputDialog.Attach(field);
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

    private string SnapshotDraft() => System.Text.Json.JsonSerializer.Serialize(_draft);

    private void RequestClose()
    {
        void Leave()
        {
            if (ScenarioStartMenu.FromStartMenu) ScenarioStartMenu.ReturnToStart(GetTree());
            else GetTree().Quit();
        }
        if (SnapshotDraft() == _savedDraftSnapshot) { Leave(); return; }
        var dialog = new ConfirmationDialog { Title = "저장하지 않은 시나리오", DialogText = "변경한 시나리오를 저장하고 시작 화면으로 돌아갈까요?", OkButtonText = "저장 후 나가기", CancelButtonText = "계속 편집" };
        dialog.AddButton("변경 버리기", true, "discard");
        AddChild(dialog);
        dialog.Confirmed += () => { SaveCurrent(); if (SnapshotDraft() == _savedDraftSnapshot) Leave(); };
        dialog.CustomAction += action => { if (action == "discard") Leave(); };
        dialog.PopupCentered();
    }

    private void SaveCurrent()
    {
        try
        {
            var known = _source.Generals.Select(general => general.Id.Value).ToHashSet();
            var map = CurrentMap();
            _draft = ScenarioWallDefaults.Normalize(_draft, map.Preset, _source.Balance);
            var path = Path.Combine(_scenarioDirectory, _draft.Id + ".json");
            ScenarioDraftStore.Save(path, _draft, map.Preset, known);
            var saved = ScenarioDraftStore.Load(path);
            if (SnapshotDraft() != System.Text.Json.JsonSerializer.Serialize(saved))
                throw new InvalidDataException("저장된 내용이 편집 내용과 일치하지 않습니다.");
            _savedDraftSnapshot = SnapshotDraft();
            RefreshList();
            var effective = ScenarioDraftCatalog.LoadEffective(
                ProjectSettings.GlobalizePath("res://assets/scenarios"), _scenarioDirectory)
                .FirstOrDefault(source => source.Draft.Id == _draft.Id);
            if (effective is null || Path.GetFullPath(effective.Path) != Path.GetFullPath(path)
                || System.Text.Json.JsonSerializer.Serialize(effective.Draft) != _savedDraftSnapshot)
                throw new InvalidDataException("시작 화면에서 수정본을 읽지 못했습니다.");
            for (var index = 0; index < _list.ItemCount; index++)
                if (_list.GetItemMetadata(index).AsString() == path)
                {
                    _list.Select(index);
                    UpdateDeleteButton(path);
                    Callable.From(() => _list.EnsureCurrentIsVisible()).CallDeferred();
                    break;
                }
            _message.Text = "시나리오를 저장했습니다.";
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
            var originalDirectory = _scenarioDirectory;
            var qaDirectory = Path.Combine(Path.GetTempPath(), "scenario-editor-save-" + Guid.NewGuid().ToString("N"));
            var savedEditRoundTrip = false;
            var factionDescriptionRoundTrip = false;
            try
            {
                _scenarioDirectory = qaDirectory;
                _draft = ScenarioDraftStore.Load(Path.Combine(builtIn, "historical-184.json"));
                _draft = ScenarioRootMapBinding.Bind(_draft, _maps[0]);
                ShowBasics();
                var titleField = _body.FindChildren("ScenarioTitleEditor", "LineEdit", true, false)
                    .OfType<LineEdit>().Last(field => !field.IsQueuedForDeletion());
                var descriptionField = _body.FindChildren("ScenarioDescriptionEditor", "TextEdit", true, false)
                    .OfType<TextEdit>().Last(field => !field.IsQueuedForDeletion());
                NativeTextInputDialog.ApplyResult(titleField, "수정한 기존 시나리오");
                NativeTextInputDialog.ApplyResult(descriptionField, "수정한 설명\n다음 줄");
                SaveCurrent();
                var editedPath = Path.Combine(qaDirectory, _draft.Id + ".json");
                OpenScenario(editedPath);
                savedEditRoundTrip = _draft.Title == "수정한 기존 시나리오"
                    && _draft.Description == "수정한 설명\n다음 줄"
                    && ScenarioDraftCatalog.LoadEffective(builtIn, qaDirectory)
                        .Single(source => source.Draft.Id == _draft.Id).Path == editedPath;
                ShowFactions();
                var firstFactionId = _draft.Factions[0].Id;
                var secondFactionId = _draft.Factions[1].Id;
                var firstDescription = _body.FindChildren("FactionDescriptionEditor", "TextEdit", true, false)
                    .OfType<TextEdit>().Last(field => !field.IsQueuedForDeletion());
                NativeTextInputDialog.ApplyResult(firstDescription, "첫 세력\n설명");
                _selectedDescriptionFactionId = secondFactionId;
                ShowFactions();
                var secondDescription = _body.FindChildren("FactionDescriptionEditor", "TextEdit", true, false)
                    .OfType<TextEdit>().Last(field => !field.IsQueuedForDeletion());
                NativeTextInputDialog.ApplyResult(secondDescription, "둘째 세력 설명");
                SaveCurrent();
                OpenScenario(editedPath);
                factionDescriptionRoundTrip = _draft.Factions.First(faction => faction.Id == firstFactionId)
                        .Description == "첫 세력\n설명"
                    && _draft.Factions.First(faction => faction.Id == secondFactionId)
                        .Description == "둘째 세력 설명";
            }
            finally
            {
                _scenarioDirectory = originalDirectory;
                _draft = originalDraft;
                RefreshList();
                if (Directory.Exists(qaDirectory)) Directory.Delete(qaDirectory, true);
            }
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
            var hiddenFactionUi = _body.FindChildren("*", "OptionButton", true, false)
                .OfType<OptionButton>().Any(choice => choice.ItemCount == 2
                    && choice.GetItemText(1) == "지정 연도에 등장");
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
            var multilineDescription = _body.FindChildren("ScenarioDescriptionEditor", "TextEdit", true, false)
                .OfType<TextEdit>().FirstOrDefault(field => !field.IsQueuedForDeletion())
                is { WrapMode: TextEdit.LineWrappingMode.Boundary };
            var path = Path.Combine(Path.GetTempPath(), "scenario-editor-qa-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                const string descriptionWithLines = "첫 줄 설명\n둘째 줄 설명";
                ScenarioDraftStore.Save(path, _draft with { Description = descriptionWithLines }, map, known);
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
                CreateNew();
                var newScenarioAtBottom = _list.ItemCount > 0
                    && _list.GetItemText(_list.ItemCount - 1) == "작성 중 · 저장 전"
                    && _list.IsSelected(_list.ItemCount - 1);
                _draft = originalDraft;
                RefreshList();
                var passed = bundled.Length == 6 && _list.ItemCount >= 6
                    && savedEditRoundTrip && factionDescriptionRoundTrip && newScenarioAtBottom && factionColorUi && hiddenFactionUi && cityMapUi && generalSearchUi && generalCityListUi
                    && generalCrudPassed && forceTabRemoved
                    && cityForceUi && forceCrudPassed
                    && errors.Count == 0 && mapSwitchPassed && currentMapSelected
                    && historicalUsesRoot && legacyRebased
                    && factionPlacementCompatible
                    && multilineDescription && restored.Description == descriptionWithLines
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
