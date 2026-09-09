namespace SanguoSLG.Game;

using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public partial class GeneralEditorScene : Control
{
    private static readonly string[] TroopKeys = ["infantry", "archer", "cavalry", "elephant", "siege", "naval"];
    private static readonly string[] GradeValues = ["F", "D", "C", "B", "A", "A+", "S", "SS", "SSS"];

    private readonly Dictionary<int, GeneralEditorRecord> _generalsById = new();
    private readonly Dictionary<string, string> _activeNames = new();
    private readonly Dictionary<string, string> _passiveNames = new();
    private readonly Dictionary<string, string> _adminNames = new();
    private readonly Dictionary<string, ActiveSkill> _activeSkills = new();
    private readonly Dictionary<string, PassiveSkill> _passiveSkills = new();
    private readonly Dictionary<string, AdminSkill> _adminSkills = new();
    private readonly Dictionary<int, GeneralPortraitRecord> _portraitsByGeneralId = new();
    private readonly List<GeneralEditorRecord> _generals = [];
    private readonly Dictionary<string, OptionButton> _aptitudeInputs = new();
    private readonly Dictionary<string, (CheckBox Check, SpinBox Tier)> _passiveInputs = new();
    private readonly Dictionary<string, (CheckBox Check, SpinBox Tier)> _adminInputs = new();
    private LineEdit _search = null!;
    private OptionButton _regionFilter = null!;
    private ItemList _list = null!;
    private Label _summary = null!;
    private Label _status = null!;
    private SpinBox _mightInput = null!;
    private SpinBox _intellectInput = null!;
    private SpinBox _politicsInput = null!;
    private OptionButton _activeInput = null!;
    private Label _activeDescription = null!;
    private Label _changePreview = null!;
    private GridContainer _passiveGrid = null!;
    private GridContainer _adminGrid = null!;
    private TextureRect _portraitPreview = null!;
    private TextureRect _facePreview = null!;
    private HSlider _faceX = null!;
    private HSlider _faceY = null!;
    private HSlider _faceZoom = null!;
    private Label _faceLabel = null!;
    private GeneralEditorRecord? _selected;
    private string _dataDirectory = "";
    private DateTime _generalsLoadedAt;
    private DateTime _portraitsLoadedAt;

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
            AllowReselect = true,
        };
        _list.ItemSelected += OnGeneralSelected;
        _list.ItemActivated += OnGeneralSelected;
        body.AddChild(_list);

        var detail = new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddChild(detail);

        var editor = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        detail.AddChild(editor);

        _summary = new Label { Text = "장수를 선택하세요.", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _summary.AddThemeFontSizeOverride("font_size", 18);
        editor.AddChild(_summary);

        var portraitRow = new HBoxContainer();
        editor.AddChild(portraitRow);
        _portraitPreview = new TextureRect
        {
            CustomMinimumSize = new Vector2(220, 300),
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        portraitRow.AddChild(_portraitPreview);

        var faceBox = new VBoxContainer { CustomMinimumSize = new Vector2(230, 0) };
        portraitRow.AddChild(faceBox);
        _facePreview = new TextureRect
        {
            CustomMinimumSize = new Vector2(180, 180),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        faceBox.AddChild(_facePreview);
        _faceLabel = new Label { Text = "" };
        faceBox.AddChild(_faceLabel);
        _faceX = AddSlider(faceBox, "얼굴 X", 0.5, 0, 1, 0.01);
        _faceY = AddSlider(faceBox, "얼굴 Y", 0.35, 0, 1, 0.01);
        _faceZoom = AddSlider(faceBox, "확대", 1, 1, 4, 0.05);
        var resetFace = new Button { Text = "얼굴 구도 초기화" };
        resetFace.Pressed += () =>
        {
            _faceX.Value = 0.5;
            _faceY.Value = 0.35;
            _faceZoom.Value = 1;
            UpdatePortraitPreview();
        };
        faceBox.AddChild(resetFace);

        var statRow = new HBoxContainer();
        editor.AddChild(statRow);
        _mightInput = AddStat(statRow, "무력");
        _intellectInput = AddStat(statRow, "지력");
        _politicsInput = AddStat(statRow, "정치");

        editor.AddChild(SectionLabel("병종 적성"));
        var aptitudeGrid = new GridContainer { Columns = 3 };
        editor.AddChild(aptitudeGrid);
        foreach (var key in TroopKeys)
        {
            var row = new HBoxContainer();
            aptitudeGrid.AddChild(row);
            row.AddChild(new Label { Text = TroopLabel(key), CustomMinimumSize = new Vector2(54, 0) });
            var input = new OptionButton { CustomMinimumSize = new Vector2(84, 0) };
            foreach (var grade in GradeValues)
            {
                input.AddItem(grade);
            }

            input.ItemSelected += _ => UpdateChangePreview();
            row.AddChild(input);
            _aptitudeInputs[key] = input;
        }

        editor.AddChild(SectionLabel("전투 액티브"));
        _activeInput = new OptionButton();
        _activeInput.ItemSelected += _ =>
        {
            UpdateActiveDescription();
            UpdateChangePreview();
        };
        editor.AddChild(_activeInput);
        _activeDescription = new Label
        {
            Text = "액티브를 선택하면 효과가 표시됩니다.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        editor.AddChild(_activeDescription);

        editor.AddChild(SectionLabel("전투 패시브"));
        _passiveGrid = new GridContainer { Columns = 2 };
        editor.AddChild(_passiveGrid);

        editor.AddChild(SectionLabel("내정 패시브"));
        _adminGrid = new GridContainer { Columns = 2 };
        editor.AddChild(_adminGrid);

        _changePreview = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        editor.AddChild(_changePreview);

        var buttons = new HBoxContainer();
        editor.AddChild(buttons);
        var reset = new Button { Text = "선택 장수 되돌리기" };
        reset.Pressed += () =>
        {
            if (_selected is not null)
            {
                ShowGeneral(_selected);
            }
        };
        buttons.AddChild(reset);

        var save = new Button { Text = "저장" };
        save.Pressed += SaveSelected;
        buttons.AddChild(save);

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
            _portraitsByGeneralId.Clear();
            _activeNames.Clear();
            _passiveNames.Clear();
            _adminNames.Clear();
            _activeSkills.Clear();
            _passiveSkills.Clear();
            _adminSkills.Clear();
            foreach (var skill in new ActiveSkillLoader().LoadFromDirectory(_dataDirectory))
            {
                _activeNames[skill.Code] = skill.Name;
                _activeSkills[skill.Code] = skill;
            }

            foreach (var skill in new PassiveSkillLoader().LoadFromDirectory(_dataDirectory))
            {
                _passiveNames[skill.Code] = skill.Name;
                _passiveSkills[skill.Code] = skill;
            }

            foreach (var skill in new AdminSkillLoader().LoadFromDirectory(_dataDirectory))
            {
                _adminNames[skill.Code] = skill.Name;
                _adminSkills[skill.Code] = skill;
            }

            foreach (var general in GeneralEditorStore.LoadGenerals(File.ReadAllText(Path.Combine(_dataDirectory, "generals.json"))))
            {
                _generals.Add(general);
                _generalsById[general.Id] = general;
            }
            _generalsLoadedAt = File.GetLastWriteTimeUtc(Path.Combine(_dataDirectory, "generals.json"));

            var portraitPath = Path.Combine(_dataDirectory, "general-portraits.json");
            if (File.Exists(portraitPath))
            {
                foreach (var portrait in GeneralEditorStore.LoadPortraits(File.ReadAllText(portraitPath)))
                {
                    _portraitsByGeneralId[portrait.GeneralId] = portrait;
                }
            }
            _portraitsLoadedAt = File.Exists(portraitPath) ? File.GetLastWriteTimeUtc(portraitPath) : DateTime.MinValue;

            RebuildSkillOptions();
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
        var item = (int)index;
        if (item < 0 || item >= _list.ItemCount)
        {
            return;
        }

        var id = _list.GetItemMetadata(item).AsInt32();
        if (_generalsById.TryGetValue(id, out var general))
        {
            ShowGeneral(general);
            _status.Text = $"{general.Name} 선택";
        }
        else
        {
            _status.Text = $"장수 ID {id}를 찾을 수 없습니다.";
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
            $"현재: 무력 {general.Might} / 지력 {general.Intellect} / 정치 {general.Politics}\n" +
            $"현재 적성: {aptitudes}\n" +
            $"현재 전투 액티브: {battleActive}\n" +
            $"현재 전투 패시브: {battlePassives}\n" +
            $"현재 내정 패시브: {adminPassives}\n\n" +
            general.Desc;

        _mightInput.Value = general.Might;
        _intellectInput.Value = general.Intellect;
        _politicsInput.Value = general.Politics;
        foreach (var key in TroopKeys)
        {
            SelectOption(_aptitudeInputs[key], general.Aptitudes.GetValueOrDefault(key, "F"));
        }

        SelectOption(_activeInput, string.IsNullOrWhiteSpace(general.BattleActive) ? "" : general.BattleActive);
        UpdateActiveDescription();
        foreach (var (code, input) in _passiveInputs)
        {
            var held = general.BattlePassives.FirstOrDefault(s => s.Code == code);
            input.Check.ButtonPressed = held is not null;
            input.Tier.Value = held?.Tier ?? 1;
        }

        foreach (var (code, input) in _adminInputs)
        {
            var held = general.AdminPassives.FirstOrDefault(s => s.Code == code);
            input.Check.ButtonPressed = held is not null;
            input.Tier.Value = held?.Tier ?? 1;
        }

        var portrait = PortraitFor(general.Id);
        _faceX.Value = portrait.FaceCenterX;
        _faceY.Value = portrait.FaceCenterY;
        _faceZoom.Value = portrait.FaceZoom;
        UpdatePortraitPreview();

        UpdateChangePreview();
    }

    private SpinBox AddStat(HBoxContainer parent, string label)
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(96, 0) };
        box.AddChild(new Label { Text = label, HorizontalAlignment = HorizontalAlignment.Center });
        var spin = new SpinBox
        {
            MinValue = 1,
            MaxValue = 100,
            Step = 1,
            Rounded = true,
        };
        spin.ValueChanged += _ => UpdateChangePreview();
        box.AddChild(spin);
        parent.AddChild(box);
        return spin;
    }

    private HSlider AddSlider(VBoxContainer parent, string label, double value, double min, double max, double step)
    {
        parent.AddChild(new Label { Text = label });
        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
        };
        slider.ValueChanged += _ =>
        {
            UpdatePortraitPreview();
            UpdateChangePreview();
        };
        parent.AddChild(slider);
        return slider;
    }

    private static Label SectionLabel(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 18);
        return label;
    }

    private void RebuildSkillOptions()
    {
        _activeInput.Clear();
        _activeInput.AddItem("없음");
        _activeInput.SetItemMetadata(0, "");
        foreach (var (code, name) in _activeNames.OrderBy(kv => kv.Value))
        {
            var index = _activeInput.ItemCount;
            _activeInput.AddItem($"{name} ({code})");
            _activeInput.SetItemMetadata(index, code);
        }

        RebuildSkillGrid(_passiveGrid, _passiveInputs, _passiveNames);
        RebuildSkillGrid(_adminGrid, _adminInputs, _adminNames);
    }

    private void RebuildSkillGrid(GridContainer parent, Dictionary<string, (CheckBox Check, SpinBox Tier)> inputs, Dictionary<string, string> names)
    {
        foreach (var child in parent.GetChildren())
        {
            child.QueueFree();
        }

        inputs.Clear();
        foreach (var (code, name) in names.OrderBy(kv => kv.Value))
        {
            var check = new CheckBox { Text = $"{name} ({code})", ClipText = true };
            check.Toggled += _ => UpdateChangePreview();
            parent.AddChild(check);

            var tier = new SpinBox
            {
                MinValue = 1,
                MaxValue = 3,
                Step = 1,
                Rounded = true,
                CustomMinimumSize = new Vector2(70, 0),
            };
            tier.ValueChanged += _ => UpdateChangePreview();
            parent.AddChild(tier);
            inputs[code] = (check, tier);
        }
    }

    private void SaveSelected()
    {
        if (_selected is null)
        {
            _status.Text = "저장할 장수를 선택하세요.";
            return;
        }

        try
        {
            var edited = CurrentEditedRecord();
            var validation = GeneralEditorStore.Validate(
                _generals.Where(g => g.Id != edited.Id).Append(edited).ToList(),
                _activeNames.Keys.ToHashSet(),
                _passiveNames.Keys.ToHashSet(),
                _adminNames.Keys.ToHashSet());
            if (!validation.IsValid)
            {
                _status.Text = string.Join("\n", validation.Errors.Take(6));
                return;
            }

            var path = Path.Combine(_dataDirectory, "generals.json");
            if (File.GetLastWriteTimeUtc(path) != _generalsLoadedAt)
            {
                _status.Text = "generals.json이 외부에서 변경되었습니다. 재로드 후 다시 저장하세요.";
                return;
            }

            var portraitPath = Path.Combine(_dataDirectory, "general-portraits.json");
            var portraitWriteTime = File.Exists(portraitPath) ? File.GetLastWriteTimeUtc(portraitPath) : DateTime.MinValue;
            if (portraitWriteTime != _portraitsLoadedAt)
            {
                _status.Text = "general-portraits.json이 외부에서 변경되었습니다. 재로드 후 다시 저장하세요.";
                return;
            }

            BackupDataFiles();
            var saved = GeneralEditorStore.ReplaceGeneral(File.ReadAllText(path), edited);
            File.WriteAllText(path, saved);
            SavePortraitMetadata(edited.Id);
            _generalsLoadedAt = File.GetLastWriteTimeUtc(path);
            _portraitsLoadedAt = File.GetLastWriteTimeUtc(portraitPath);
            var index = _generals.FindIndex(g => g.Id == edited.Id);
            if (index >= 0)
            {
                _generals[index] = edited;
            }

            _generalsById[edited.Id] = edited;
            _selected = edited;
            RefreshList();
            ShowGeneral(edited);
            _status.Text = $"{edited.Name} 저장 완료";
        }
        catch (Exception ex)
        {
            _status.Text = $"저장 실패: {ex.Message}";
        }
    }

    private GeneralEditorRecord CurrentEditedRecord()
    {
        if (_selected is null)
        {
            throw new InvalidOperationException("선택된 장수가 없습니다.");
        }

        return _selected with
        {
            Might = (int)_mightInput.Value,
            Intellect = (int)_intellectInput.Value,
            Politics = (int)_politicsInput.Value,
            Aptitudes = TroopKeys.ToDictionary(k => k, k => SelectedText(_aptitudeInputs[k])),
            BattleActive = SelectedMetadata(_activeInput),
            BattlePassives = _passiveInputs
                .Where(kv => kv.Value.Check.ButtonPressed)
                .Select(kv => new GeneralEditorSkill(kv.Key, (int)kv.Value.Tier.Value))
                .ToList(),
            AdminPassives = _adminInputs
                .Where(kv => kv.Value.Check.ButtonPressed)
                .Select(kv => new GeneralEditorSkill(kv.Key, (int)kv.Value.Tier.Value))
                .ToList(),
        };
    }

    private void BackupDataFiles()
    {
        var backupDir = Path.Combine(RepositoryRoot(), "tools", "general-editor", "backups", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Directory.CreateDirectory(backupDir);
        File.Copy(Path.Combine(_dataDirectory, "generals.json"), Path.Combine(backupDir, "generals.json"), overwrite: true);
        var portraitsPath = Path.Combine(_dataDirectory, "general-portraits.json");
        if (File.Exists(portraitsPath))
        {
            File.Copy(portraitsPath, Path.Combine(backupDir, "general-portraits.json"), overwrite: true);
        }
    }

    private void SavePortraitMetadata(int generalId)
    {
        var edited = CurrentPortraitRecord(generalId);
        var validation = GeneralEditorStore.ValidatePortraits([edited], new HashSet<int>(_generals.Select(g => g.Id)),
            relative => File.Exists(Path.Combine(RepositoryRoot(), relative)));
        if (!validation.IsValid)
        {
            throw new InvalidDataException(string.Join("\n", validation.Errors));
        }

        var path = Path.Combine(_dataDirectory, "general-portraits.json");
        var original = File.Exists(path) ? File.ReadAllText(path) : "[]";
        File.WriteAllText(path, GeneralEditorStore.ReplacePortrait(original, edited));
        _portraitsByGeneralId[generalId] = edited;
    }

    private void UpdateChangePreview()
    {
        if (_selected is null || _changePreview is null)
        {
            return;
        }

        try
        {
            var edited = CurrentEditedRecord();
            var changes = new List<string>();
            AddChange(changes, "무력", _selected.Might, edited.Might);
            AddChange(changes, "지력", _selected.Intellect, edited.Intellect);
            AddChange(changes, "정치", _selected.Politics, edited.Politics);
            foreach (var key in TroopKeys)
            {
                AddChange(changes, TroopLabel(key), _selected.Aptitudes.GetValueOrDefault(key, "F"), edited.Aptitudes[key]);
            }

            AddChange(changes, "전투 액티브", _selected.BattleActive ?? "없음", edited.BattleActive ?? "없음");
            AddChange(changes, "전투 패시브", SkillText(_selected.BattlePassives), SkillText(edited.BattlePassives));
            AddChange(changes, "내정 패시브", SkillText(_selected.AdminPassives), SkillText(edited.AdminPassives));
            var originalPortrait = PortraitFor(_selected.Id);
            var editedPortrait = CurrentPortraitRecord(_selected.Id);
            AddChange(changes, "얼굴 X", originalPortrait.FaceCenterX.ToString("0.00"), editedPortrait.FaceCenterX.ToString("0.00"));
            AddChange(changes, "얼굴 Y", originalPortrait.FaceCenterY.ToString("0.00"), editedPortrait.FaceCenterY.ToString("0.00"));
            AddChange(changes, "얼굴 확대", originalPortrait.FaceZoom.ToString("0.00"), editedPortrait.FaceZoom.ToString("0.00"));
            _changePreview.Text = changes.Count == 0 ? "변경 없음" : "변경 예정\n" + string.Join("\n", changes);
        }
        catch (Exception ex)
        {
            _changePreview.Text = $"입력 오류: {ex.Message}";
        }
    }

    private static void AddChange<T>(List<string> changes, string label, T before, T after)
    {
        if (!EqualityComparer<T>.Default.Equals(before, after))
        {
            changes.Add($"{label}: {before} -> {after}");
        }
    }

    private static void SelectOption(OptionButton input, string value)
    {
        for (var i = 0; i < input.ItemCount; i++)
        {
            var metadata = input.GetItemMetadata(i);
            var candidate = metadata.VariantType == Variant.Type.Nil ? input.GetItemText(i) : metadata.AsString();
            if (candidate == value)
            {
                input.Select(i);
                return;
            }
        }

        input.Select(0);
    }

    private static string SelectedText(OptionButton input) => input.GetItemText(input.Selected);

    private static string? SelectedMetadata(OptionButton input)
    {
        var value = input.GetItemMetadata(input.Selected).AsString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private void UpdateActiveDescription()
    {
        var code = SelectedMetadata(_activeInput);
        if (code is null)
        {
            _activeDescription.Text = "전투 액티브 없음";
            return;
        }

        _activeDescription.Text = _activeSkills.TryGetValue(code, out var skill)
            ? $"{skill.Name} ({skill.Code})\n{SkillDescriptions.Active(skill)}"
            : $"등록되지 않은 액티브입니다: {code}";
    }

    private static string SkillText(IReadOnlyList<GeneralEditorSkill> skills)
        => skills.Count == 0 ? "없음" : string.Join(", ", skills.OrderBy(s => s.Code).Select(s => $"{s.Code} Lv{s.Tier}"));

    private GeneralPortraitRecord PortraitFor(int generalId)
        => _portraitsByGeneralId.TryGetValue(generalId, out var portrait)
            ? portrait
            : new GeneralPortraitRecord(generalId, $"SanguoSLG.Game/assets/portraits/{generalId}.png", 0.5, 0.35, 1);

    private GeneralPortraitRecord CurrentPortraitRecord(int generalId)
    {
        var current = PortraitFor(generalId);
        return current with
        {
            FaceCenterX = _faceX.Value,
            FaceCenterY = _faceY.Value,
            FaceZoom = _faceZoom.Value,
        };
    }

    private void UpdatePortraitPreview()
    {
        if (_selected is null || _portraitPreview is null)
        {
            return;
        }

        var portrait = CurrentPortraitRecord(_selected.Id);
        var path = Path.Combine(RepositoryRoot(), portrait.PortraitPath);
        if (!File.Exists(path))
        {
            _portraitPreview.Texture = null;
            _facePreview.Texture = null;
            _faceLabel.Text = "초상 파일 없음";
            return;
        }

        var image = Image.LoadFromFile(path);
        if (image is null || image.IsEmpty())
        {
            _portraitPreview.Texture = null;
            _facePreview.Texture = null;
            _faceLabel.Text = "초상 로드 실패";
            return;
        }

        _portraitPreview.Texture = ImageTexture.CreateFromImage(image);
        _facePreview.Texture = ImageTexture.CreateFromImage(BuildFaceImage(image, portrait));
        _faceLabel.Text = $"X {portrait.FaceCenterX:0.00} / Y {portrait.FaceCenterY:0.00} / 확대 {portrait.FaceZoom:0.00}";
    }

    private static Image BuildFaceImage(Image source, GeneralPortraitRecord portrait)
    {
        const int previewSize = 192;
        var side = Math.Max(1, Math.Min(source.GetWidth(), source.GetHeight()) / portrait.FaceZoom);
        var centerX = source.GetWidth() * portrait.FaceCenterX;
        var centerY = source.GetHeight() * portrait.FaceCenterY;
        var left = Math.Clamp(centerX - side / 2.0, 0, Math.Max(0, source.GetWidth() - side));
        var top = Math.Clamp(centerY - side / 2.0, 0, Math.Max(0, source.GetHeight() - side));
        var crop = source.GetRegion(new Rect2I((int)Math.Round(left), (int)Math.Round(top), (int)Math.Round(side), (int)Math.Round(side)));
        crop.Resize(previewSize, previewSize, Image.Interpolation.Lanczos);

        var radius = previewSize / 2.0;
        var output = Image.CreateEmpty(previewSize, previewSize, false, Image.Format.Rgba8);
        for (var y = 0; y < previewSize; y++)
        {
            for (var x = 0; x < previewSize; x++)
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

    private string RepositoryRoot() => Path.GetFullPath(Path.Combine(_dataDirectory, ".."));

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
