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
        _maps.AddRange(_archive.ListVersions());
        if (_maps.Count == 0)
        {
            var path = ProjectSettings.GlobalizePath("res://assets/maps/east-asia-user-preset.json");
            _maps.Add(_archive.SaveVersion("default-east-asia", "기본 동아시아",
                ScenarioMapPresetStore.Load(path)));
        }
        var edited = ProjectSettings.GlobalizePath("user://map-presets/east-asia.json");
        if (File.Exists(edited))
        {
            try
            {
                var version = _archive.SaveVersion("edited-east-asia", "편집한 동아시아",
                    ScenarioMapPresetStore.Load(edited));
                if (_maps.All(map => map.Id != version.Id || map.Revision != version.Revision))
                    _maps.Add(version);
            }
            catch (Exception error) { GD.PushWarning("편집 지도 가져오기 실패: " + error.Message); }
        }
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
        AddTab(tabs, "병력·선박", ShowForces);
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
        if (!Directory.Exists(_scenarioDirectory)) return;
        foreach (var path in Directory.EnumerateFiles(_scenarioDirectory, "*.json").Order())
        {
            try
            {
                var draft = ScenarioDraftStore.Load(path);
                var index = _list.AddItem(draft.Title);
                _list.SetItemMetadata(index, path);
            }
            catch (Exception error) { GD.PushWarning($"시나리오 목록: {path}: {error.Message}"); }
        }
    }

    private void OpenScenario(string path)
    {
        try { _draft = ScenarioDraftStore.Load(path); ShowBasics(); _message.Text = "시나리오를 불러왔습니다."; }
        catch (Exception error) { _message.Text = "불러오기 실패: " + error.Message; }
    }

    private void CreateNew()
    {
        _draft = ScenarioDraftApplier.CreateSeed("scenario-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
            "새 시나리오", _source, _maps[0]);
        ShowBasics();
    }

    private void CloneCurrent()
    {
        _draft = _draft with { Id = "scenario-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"), Title = _draft.Title + " 복사본" };
        ShowBasics();
    }

    private ScenarioMapVersion CurrentMap() => _archive.Load(_draft.MapId, _draft.MapRevision);

    private void ClearBody()
    {
        EndNativeText();
        foreach (var child in _body.GetChildren()) child.QueueFree();
    }

    private void ShowBasics()
    {
        ClearBody();
        _body.AddChild(new Label { Text = "기본 정보" });
        AddText("파일 ID", _draft.Id, value => _draft = _draft with { Id = value });
        AddText("제목", _draft.Title, value => _draft = _draft with { Title = value });
        AddText("설명", _draft.Description, value => _draft = _draft with { Description = value });
        AddNumber("시작 연도", _draft.StartYear, 1, 9999, value => _draft = _draft with { StartYear = value });
        AddNumber("월", _draft.StartMonth, 1, 12, value => _draft = _draft with { StartMonth = value });
        AddNumber("일", _draft.StartDay, 1, 30, value => _draft = _draft with { StartDay = value });
        _body.AddChild(new HSeparator());
        _body.AddChild(new Label { Text = "지도 버전 선택 · 지도 교체 시 삭제된 거점의 배치를 확인하세요" });
        var choices = new OptionButton();
        var currentIndex = 0;
        for (var i = 0; i < _maps.Count; i++)
        {
            var option = _maps[i];
            choices.AddItem($"{option.Title} · {option.Revision[..8]} · 거점 {option.Preset.Sites.Count(site => site.Kind is "city" or "port" or "gate")}");
            if (option.Id == _draft.MapId && option.Revision == _draft.MapRevision) currentIndex = i;
        }
        choices.Select(currentIndex);
        choices.ItemSelected += index => SwitchMap(_maps[(int)index]);
        _body.AddChild(choices);
        var map = CurrentMap();
        _body.AddChild(new Label { Text = $"현재: {map.Title} / {map.Revision[..12]}" });
        var missing = _draft.Generals.Where(g => g.SiteId is { } id && !map.Preset.Sites.Any(site => site.Id == id))
            .Select(g => g.GeneralId).ToArray();
        _body.AddChild(new Label { Text = missing.Length == 0 ? "미배치 오류 없음" : "삭제된 거점의 장수: " + string.Join(", ", missing) });
    }

    private void SwitchMap(ScenarioMapVersion next)
    {
        var previous = CurrentMap().Preset;
        var sameMapLineage = next.Id == _draft.MapId;
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
        ShowBasics();
        _message.Text = $"지도 교체: 소유권 해제 거점 {lostCities}곳, 배치 해제 장수 {lost.Length}명. 세력·거점 및 장수 배치 탭에서 확인하세요.";
    }

    private void ShowCities()
    {
        ClearBody();
        var map = CurrentMap().Preset;
        _body.AddChild(new Label { Text = "거점 소유와 시작 자원 · 변경 즉시 초안에 반영됩니다" });
        var factionNames = _draft.Factions.ToDictionary(f => f.Id, f => f.Name);
        foreach (var city in _draft.Cities)
        {
            var site = map.Sites.FirstOrDefault(site => site.Id == city.SiteId);
            if (site is null) continue;
            var row = new HBoxContainer();
            _body.AddChild(row);
            row.AddChild(new Label { Text = site.Name + " (" + site.Kind + ")", CustomMinimumSize = new Vector2(120, 0) });
            var owner = new OptionButton { CustomMinimumSize = new Vector2(135, 0) };
            owner.AddItem("중립", 0);
            foreach (var faction in _draft.Factions) owner.AddItem(faction.Name, faction.Id);
            owner.Select(Math.Max(0, owner.GetItemIndex(city.OwnerFactionId)));
            owner.ItemSelected += index => UpdateCity(city.SiteId, c => c with { OwnerFactionId = owner.GetItemId((int)index) });
            row.AddChild(owner);
            AddCompactNumber(row, "금", city.Gold, 0, 10000000, value => UpdateCity(city.SiteId, c => c with { Gold = value }));
            AddCompactNumber(row, "군량", city.Provisions, 0, 10000000, value => UpdateCity(city.SiteId, c => c with { Provisions = value }));
            AddCompactNumber(row, "성벽", city.Wall, 0, 1000000, value => UpdateCity(city.SiteId, c => c with { Wall = value }));
            AddCompactNumber(row, "치안", city.Security, 0, 100, value => UpdateCity(city.SiteId, c => c with { Security = value }));
        }
    }

    private void ShowFactions()
    {
        ClearBody();
        var generals = _source.Generals.OrderBy(g => g.Name).ToArray();
        _body.AddChild(new Label { Text = "세력 이름, 군주, 시작 가능 여부" });
        foreach (var faction in _draft.Factions)
        {
            var row = new HBoxContainer(); _body.AddChild(row);
            row.AddChild(new Label { Text = $"세력 {faction.Id}", CustomMinimumSize = new Vector2(90, 0) });
            var name = new LineEdit { Text = faction.Name, CustomMinimumSize = new Vector2(150, 0) };
            name.TextChanged += value => UpdateFaction(faction.Id, f => f with { Name = value });
            name.FocusEntered += () => BeginNativeText(name,
                value => UpdateFaction(faction.Id, f => f with { Name = value }));
            row.AddChild(name);
            var ruler = new OptionButton { CustomMinimumSize = new Vector2(150, 0) };
            foreach (var general in generals) ruler.AddItem(general.Name, general.Id.Value);
            ruler.Select(Math.Max(0, ruler.GetItemIndex(faction.RulerGeneralId)));
            ruler.ItemSelected += index => UpdateFaction(faction.Id, f => f with { RulerGeneralId = ruler.GetItemId((int)index) });
            row.AddChild(ruler);
            var playable = new CheckBox { Text = "시작 가능", ButtonPressed = faction.Playable };
            playable.Toggled += value => UpdateFaction(faction.Id, f => f with { Playable = value });
            row.AddChild(playable);
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
            _draft = _draft with { Factions = [.. _draft.Factions, new ScenarioFactionEntry(id, "새 세력", generals[0].Id.Value, "#c9ad72")] };
            ShowFactions();
        };
        _body.AddChild(add);
    }

    private void ShowGenerals()
    {
        ClearBody();
        var map = CurrentMap().Preset;
        var names = _source.Generals.ToDictionary(g => g.Id.Value, g => g.Name);
        _body.AddChild(new Label { Text = "장수 소속과 주둔 거점 · 군주는 소속 세력의 성에 배치해야 저장할 수 있습니다" });
        foreach (var general in _draft.Generals)
        {
            var row = new HBoxContainer(); _body.AddChild(row);
            row.AddChild(new Label { Text = names.GetValueOrDefault(general.GeneralId, general.GeneralId.ToString()), CustomMinimumSize = new Vector2(120, 0) });
            var faction = new OptionButton { CustomMinimumSize = new Vector2(140, 0) };
            foreach (var entry in _draft.Factions) faction.AddItem(entry.Name, entry.Id);
            faction.Select(Math.Max(0, faction.GetItemIndex(general.FactionId)));
            faction.ItemSelected += index =>
            {
                UpdateGeneral(general.GeneralId, g => g with { FactionId = faction.GetItemId((int)index), SiteId = null });
                ShowGenerals();
            };
            row.AddChild(faction);
            var site = new OptionButton { CustomMinimumSize = new Vector2(160, 0) };
            site.AddItem("미배치", 0);
            foreach (var city in _draft.Cities.Where(c => c.OwnerFactionId == general.FactionId))
            {
                var place = map.Sites.FirstOrDefault(s => s.Id == city.SiteId);
                if (place is not null) site.AddItem(place.Name, city.SiteId);
            }
            site.Select(Math.Max(0, site.GetItemIndex(general.SiteId ?? 0)));
            site.ItemSelected += index => UpdateGeneral(general.GeneralId, g => g with { SiteId = site.GetItemId((int)index) is 0 ? null : site.GetItemId((int)index) });
            row.AddChild(site);
        }
        _body.AddChild(new HSeparator());
        _body.AddChild(new Label { Text = "미배치 장수 추가" });
        var available = _source.Generals.Where(g => _draft.Generals.All(entry => entry.GeneralId != g.Id.Value)).OrderBy(g => g.Name).ToArray();
        if (available.Length == 0 || _draft.Factions.Count == 0) return;
        var selection = new OptionButton();
        foreach (var general in available) selection.AddItem(general.Name, general.Id.Value);
        _body.AddChild(selection);
        var add = new Button { Text = "선택 장수 추가" };
        add.Pressed += () =>
        {
            _draft = _draft with { Generals = [.. _draft.Generals,
                new ScenarioGeneralEntry(selection.GetSelectedId(), _draft.Factions[0].Id, null)] };
            ShowGenerals();
        };
        _body.AddChild(add);
    }

    private void ShowForces()
    {
        ClearBody();
        var map = CurrentMap().Preset;
        _body.AddChild(new Label { Text = "병력과 선박 · 한 줄에 병종:수량 입력, 쉼표로 구분 (예: swordsman:10000, archer:5000)" });
        foreach (var city in _draft.Cities.Where(c => c.OwnerFactionId > 0))
        {
            var site = map.Sites.FirstOrDefault(s => s.Id == city.SiteId);
            if (site is null) continue;
            AddText(site.Name + " 병력", string.Join(", ", city.Garrisons.Select(g => $"{g.TroopCode}:{g.Troops}")),
                value => { if (TryParseForces(value, out var forces)) UpdateCity(city.SiteId, c => c with { Garrisons = forces }); else _message.Text = site.Name + ": 병력 입력 형식 오류"; });
            if (site.Kind == "port")
                AddText(site.Name + " 선박", string.Join(", ", city.Ships.Select(s => $"{s.ShipCode}:{s.Count}")),
                    value => { if (TryParseForces(value, out var ships)) UpdateCity(city.SiteId, c => c with { Ships = ships.Select(s => new ScenarioShipEntry(s.TroopCode, s.Troops)).ToArray() }); else _message.Text = site.Name + ": 선박 입력 형식 오류"; });
        }
    }

    private static bool TryParseForces(string text, out IReadOnlyList<ScenarioGarrisonEntry> forces)
    {
        var parsed = new List<ScenarioGarrisonEntry>();
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split(':', StringSplitOptions.TrimEntries);
            if (pair.Length != 2 || pair[0].Length == 0 || !int.TryParse(pair[1], out var count) || count <= 0)
            { forces = []; return false; }
            parsed.Add(new ScenarioGarrisonEntry(pair[0], count));
        }
        forces = parsed; return true;
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
            var errors = _draft.Validate(map, known);
            var originalDraft = _draft;
            var otherMap = _maps.FirstOrDefault(candidate => candidate.Id != _draft.MapId);
            var mapSwitchPassed = true;
            if (otherMap is not null)
            {
                SwitchMap(otherMap);
                mapSwitchPassed = _draft.Cities.All(city => city.OwnerFactionId == 0)
                    && _draft.Generals.All(general => general.SiteId is null);
                _draft = originalDraft;
            }
            ShowFactions(); ShowCities(); ShowGenerals(); ShowForces(); ShowBasics();
            var path = Path.Combine(Path.GetTempPath(), "scenario-editor-qa-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                ScenarioDraftStore.Save(path, _draft, map, known);
                var restored = ScenarioDraftStore.Load(path);
                var passed = errors.Count == 0 && mapSwitchPassed
                    && restored.MapRevision == _draft.MapRevision
                    && restored.Cities.Count == _draft.Cities.Count;
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
