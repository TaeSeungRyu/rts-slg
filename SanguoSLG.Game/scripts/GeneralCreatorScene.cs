namespace SanguoSLG.Game;

using Godot;
using SanguoSLG.Core.Data;

/// <summary>새 게임 전용 장수 생성기. 저장 게임과 원본 generals.json을 수정하지 않는다.</summary>
public partial class GeneralCreatorScene : Control
{
    private static readonly (string Code, string Name)[] Troops =
    [
        ("infantry", "보병"), ("archer", "궁병"), ("cavalry", "기병"),
        ("elephant", "상병"), ("siege", "공성"), ("naval", "수군"),
        ("supply", "보급"), ("defense", "수성"),
    ];
    private static readonly string[] Grades = ["F", "D", "C", "B", "A", "A+", "S", "SS", "SSS"];
    private readonly List<CreatedGeneralRecord> _created = [];
    private readonly Dictionary<string, OptionButton> _aptitudes = new();
    private readonly List<OptionButton> _battlePassives = [];
    private readonly List<OptionButton> _adminPassives = [];
    private readonly List<Label> _battleDescriptions = [];
    private readonly List<Label> _adminDescriptions = [];
    private readonly Dictionary<string, string> _activeDetails = new();
    private readonly Dictionary<string, string> _battleDetails = new();
    private readonly Dictionary<string, string> _adminDetails = new();
    private ItemList _list = null!;
    private LineEdit _name = null!;
    private NativeNameEdit? _nativeName;
    private TabContainer _tabs = null!;
    private OptionButton _region = null!;
    private OptionButton _requiredGeneral = null!;
    private TextEdit _description = null!;
    private OptionButton _gender = null!;
    private OptionButton _active = null!;
    private Label _activeDescription = null!;
    private SpinBox _birth = null!;
    private SpinBox _might = null!;
    private SpinBox _intellect = null!;
    private SpinBox _politics = null!;
    private SpinBox _points = null!;
    private Label _status = null!;
    private Label _overview = null!;
    private GridContainer _portraitGrid = null!;
    private TextureRect _portraitPreview = null!;
    private Label _portraitLabel = null!;
    private FileDialog _portraitFileDialog = null!;
    private FileDialog _galleryFileDialog = null!;
    private string? _portraitPath;
    private string? _pendingExternalPath;
    private int? _editingId;
    private string _dataDirectory = "";

    public override void _Ready()
    {
        BuildUi();
        try
        {
            _dataDirectory = FindDataDirectory();
            PopulateSkills();
            PopulateRegions();
            var path = ProjectSettings.GlobalizePath(CreatedGeneralPresets.UserPath);
            if (File.Exists(path)) _created.AddRange(CreatedGeneralStore.Load(File.ReadAllText(path)));
            PopulateRequiredGenerals();
            RefreshList();
            NewGeneral();
        }
        catch (Exception error) { _status.Text = "불러오기 실패: " + error.Message; }
        if (OS.GetCmdlineUserArgs().Contains("--generalcreatorqa")) CallDeferred(nameof(RunQa));
        else StartNativeNameInput();
    }

    public override void _Process(double delta) => _nativeName?.SyncBounds();

    public override void _ExitTree() => StopNativeNameInput();

    private void BuildUi()
    {
        GeneralWorkspaceStyle.AddBackground(this);
        var root = new VBoxContainer { AnchorRight = 1, AnchorBottom = 1,
            OffsetLeft = 18, OffsetTop = 18, OffsetRight = -18, OffsetBottom = -18,
            Theme = GeneralWorkspaceStyle.Theme() };
        root.AddThemeConstantOverride("separation", 10);
        AddChild(root);
        var title = new Label { Text = "신규 장수 생성", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 24);
        root.AddChild(title);
        var toolbar = new HBoxContainer();
        root.AddChild(toolbar);
        if (ScenarioStartMenu.FromStartMenu)
        {
            var back = new Button { Text = "시작 화면" };
            back.Pressed += () => ScenarioStartMenu.ReturnToStart(GetTree());
            toolbar.AddChild(back);
        }
        toolbar.AddChild(new Label { Text = "생성 장수는 다음 새 게임부터 영입 후보에 추가됩니다.",
            SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Center });
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(body);
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(310, 0) };
        body.AddChild(left);
        left.AddChild(new Label { Text = "생성한 장수", HorizontalAlignment = HorizontalAlignment.Center });
        _list = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        _list.ItemSelected += index => LoadSelected((int)index);
        left.AddChild(_list);
        var add = new Button { Text = "+ 새 장수" };
        add.Pressed += NewGeneral;
        left.AddChild(add);
        var delete = new Button { Text = "선택 장수 삭제" };
        delete.Pressed += DeleteSelected;
        left.AddChild(delete);

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 10);
        body.AddChild(right);
        var overview = GeneralWorkspaceStyle.Section(right, "장수 기본 내용");
        _overview = new Label { Text = "새 장수", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _overview.AddThemeFontSizeOverride("font_size", 18);
        overview.AddChild(_overview);
        _tabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill };
        right.AddChild(_tabs);
        _tabs.TabChanged += tab =>
        {
            if (tab == 1) StartNativeNameInput();
            else StopNativeNameInput();
        };
        VBoxContainer Page(string name)
        {
            var scroll = new ScrollContainer { Name = name, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill };
            _tabs.AddChild(scroll);
            var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            content.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(content);
            return content;
        }
        var portraits = Page("초상·얼굴");
        var basic = Page("기본 정보");
        var identity = GeneralWorkspaceStyle.Section(basic, "신상 정보");
        _name = new LineEdit { MaxLength = 20, PlaceholderText = "장수 이름", KeepEditingOnTextSubmit = true };
        _name.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true }) StartNativeNameInput();
        };
        AddField(identity, "이름", _name);
        _gender = new OptionButton();
        _gender.AddItem("남성"); _gender.AddItem("여성");
        _gender.ItemSelected += _ =>
        {
            if (_pendingExternalPath is null && _portraitPath?.StartsWith("res://assets/portraits/created/") == true)
                SelectPortrait(DefaultPortrait());
            RebuildPortraitGrid();
        };
        AddField(identity, "성별", _gender);
        _birth = Spin(-500, 500, 170);
        AddField(identity, "출생년도", _birth);
        _region = new OptionButton();
        AddField(identity, "출신", _region);
        var recruitment = GeneralWorkspaceStyle.Section(basic, "영입 조건");
        _points = Spin(0, 1000, 100);
        AddField(recruitment, "영입 포인트", _points);
        _requiredGeneral = new OptionButton();
        _requiredGeneral.Pressed += StopNativeNameInput;
        AddField(recruitment, "영입 선행 장수", _requiredGeneral);
        recruitment.AddChild(new Label { Text = "선행 장수를 지정하면 해당 장수가 소속된 세력만 영입할 수 있습니다." });
        var introduction = GeneralWorkspaceStyle.Section(basic, "장수 소개");
        _description = new TextEdit { CustomMinimumSize = new Vector2(0, 115) };
        introduction.AddChild(_description);

        var portraitSection = GeneralWorkspaceStyle.Section(portraits, "초상·얼굴");
        portraitSection.AddChild(new Label { Text = "기본 얼굴을 고르거나 외부 PNG·JPG·WEBP 파일을 가져오세요." });
        var portraitRow = new HBoxContainer();
        portraitRow.AddThemeConstantOverride("separation", 18);
        portraitSection.AddChild(portraitRow);
        _portraitPreview = new TextureRect { CustomMinimumSize = new Vector2(165, 220),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        portraitRow.AddChild(_portraitPreview);
        var portraitActions = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        portraitRow.AddChild(portraitActions);
        _portraitLabel = new Label { Text = "사진 없음", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        portraitActions.AddChild(_portraitLabel);
        var import = new Button { Text = "외부 이미지 가져오기" };
        import.Pressed += () => _portraitFileDialog.PopupCentered(new Vector2I(850, 620));
        portraitActions.AddChild(import);
        var clear = new Button { Text = "사진 비우기" };
        clear.Pressed += () => SelectPortrait(null);
        portraitActions.AddChild(clear);
        var gallery = GeneralWorkspaceStyle.Section(portraits, "기본 얼굴 · 클릭해서 선택");
        var register = new Button { Text = "+ 외부 이미지 등록하기", CustomMinimumSize = new Vector2(0, 40) };
        register.Pressed += () => _galleryFileDialog.PopupCentered(new Vector2I(850, 620));
        gallery.AddChild(register);
        _portraitGrid = new GridContainer { Columns = 5, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        gallery.AddChild(_portraitGrid);
        _portraitFileDialog = new FileDialog { Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenFile, Title = "장수 초상 가져오기" };
        _portraitFileDialog.AddFilter("*.png, *.jpg, *.jpeg, *.webp", "이미지 파일");
        _portraitFileDialog.FileSelected += OnExternalPortraitSelected;
        AddChild(_portraitFileDialog);
        _galleryFileDialog = new FileDialog { Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenFiles, Title = "기본 얼굴 여러 장 등록" };
        _galleryFileDialog.AddFilter("*.png, *.jpg, *.jpeg, *.webp", "이미지 파일");
        _galleryFileDialog.FilesSelected += OnGalleryPortraitsSelected;
        AddChild(_galleryFileDialog);

        var abilities = Page("능력·적성");
        var abilitySection = GeneralWorkspaceStyle.Section(abilities, "기본 능력치");
        var stats = new HBoxContainer();
        abilitySection.AddChild(stats);
        _might = Spin(0, 100, 50); AddField(stats, "무력", _might);
        _intellect = Spin(0, 100, 50); AddField(stats, "지력", _intellect);
        _politics = Spin(0, 100, 50); AddField(stats, "정치", _politics);
        var aptitudeSection = GeneralWorkspaceStyle.Section(abilities, "병종 적성");
        var grid = new GridContainer { Columns = 2 };
        aptitudeSection.AddChild(grid);
        foreach (var (code, name) in Troops)
        {
            var option = new OptionButton { CustomMinimumSize = new Vector2(105, 0) };
            foreach (var grade in Grades) option.AddItem(grade);
            option.Select(3);
            AddField(grid, name, option);
            _aptitudes[code] = option;
        }

        var skills = Page("스킬");
        var activeSection = GeneralWorkspaceStyle.Section(skills, "전투 액티브");
        activeSection.AddChild(new Label { Text = "전투 액티브 1개와 전투 패시브 최대 3개를 선택할 수 있습니다." });
        _active = new OptionButton(); AddField(activeSection, "전투 액티브", _active);
        _activeDescription = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        AddDescription(activeSection, _activeDescription);
        _active.ItemSelected += _ => ShowSkillDescription(_activeDescription, _active, _activeDetails);
        var battleSection = GeneralWorkspaceStyle.Section(skills, "전투 패시브");
        for (var index = 0; index < 3; index++)
        {
            var option = new OptionButton();
            AddField(battleSection, $"전투 패시브 {index + 1}", option);
            _battlePassives.Add(option);
            var description = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            AddDescription(battleSection, description);
            _battleDescriptions.Add(description);
            option.ItemSelected += _ => ShowSkillDescription(description, option, _battleDetails);
        }
        var adminSection = GeneralWorkspaceStyle.Section(skills, "내정 패시브");
        for (var index = 0; index < 3; index++)
        {
            var option = new OptionButton();
            AddField(adminSection, $"내정 패시브 {index + 1}", option);
            _adminPassives.Add(option);
            var description = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            AddDescription(adminSection, description);
            _adminDescriptions.Add(description);
            option.ItemSelected += _ => ShowSkillDescription(description, option, _adminDetails);
        }
        var saveSection = GeneralWorkspaceStyle.Section(right, "변경 및 저장");
        var save = new Button { Text = "저장", CustomMinimumSize = new Vector2(0, 42) };
        save.Pressed += SaveSelected;
        saveSection.AddChild(save);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        root.AddChild(_status);
    }

    private static SpinBox Spin(double min, double max, double value)
        => new() { MinValue = min, MaxValue = max, Value = value, Step = 1, Rounded = true,
            CustomMinimumSize = new Vector2(100, 0) };

    private static void AddField(Container parent, string label, Control input)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 18);
        row.CustomMinimumSize = new Vector2(0, 39);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(135, 0) });
        input.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(input);
        parent.AddChild(row);
        if (parent is VBoxContainer) parent.AddChild(new HSeparator());
    }

    private static void AddDescription(VBoxContainer parent, Label description)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", GeneralWorkspaceStyle.DescriptionBox());
        parent.AddChild(panel);
        panel.AddChild(description);
    }

    private void PopulateSkills()
    {
        void Fill(OptionButton option, IEnumerable<(string Code, string Name)> choices)
        {
            option.AddItem("없음");
            option.SetItemMetadata(0, "");
            foreach (var (code, name) in choices.OrderBy(choice => choice.Name))
            {
                var index = option.ItemCount;
                option.AddItem(name);
                option.SetItemMetadata(index, code);
            }
            option.Select(0);
        }
        var active = new ActiveSkillLoader().LoadFromDirectory(_dataDirectory);
        foreach (var skill in active) _activeDetails[skill.Code] = SkillDescriptions.Active(skill);
        Fill(_active, active.Select(s => (s.Code, s.Name)));
        var passiveSkills = new PassiveSkillLoader().LoadFromDirectory(_dataDirectory);
        foreach (var skill in passiveSkills) _battleDetails[skill.Code] = SkillDescriptions.Passive(skill, 1);
        var battle = passiveSkills.Select(s => (s.Code, s.Name)).ToList();
        foreach (var option in _battlePassives) Fill(option, battle);
        var adminSkills = new AdminSkillLoader().LoadFromDirectory(_dataDirectory);
        foreach (var skill in adminSkills) _adminDetails[skill.Code] = SkillDescriptions.Admin(skill, 1);
        var admin = adminSkills.Select(s => (s.Code, s.Name)).ToList();
        foreach (var option in _adminPassives) Fill(option, admin);
        UpdateSkillDescriptions();
    }

    private void PopulateRegions()
    {
        _region.Clear();
        foreach (var region in new RegionLoader().LoadFromDirectory(_dataDirectory).OrderBy(region => region.Name))
        {
            var index = _region.ItemCount;
            _region.AddItem(region.Name);
            _region.SetItemMetadata(index, region.Code);
        }
        if (_region.ItemCount > 0) _region.Select(0);
    }

    private void PopulateRequiredGenerals(int? selectedId = null)
    {
        _requiredGeneral.Clear();
        _requiredGeneral.AddItem("없음");
        _requiredGeneral.SetItemMetadata(0, 0);
        var originals = new GeneralLoader().LoadFromDirectory(_dataDirectory)
            .Select(general => (general.Id.Value, general.Name, Kind: "실존"));
        var created = _created.Select(entry => (entry.General.Id, entry.General.Name, Kind: "창작"));
        foreach (var (id, name, kind) in originals.Concat(created).OrderBy(general => general.Name))
        {
            var index = _requiredGeneral.ItemCount;
            _requiredGeneral.AddItem($"{name} · {kind}");
            _requiredGeneral.SetItemMetadata(index, id);
        }
        SelectRequiredGeneral(selectedId);
    }

    private void SelectRequiredGeneral(int? id)
    {
        for (var index = 0; index < _requiredGeneral.ItemCount; index++)
            if (_requiredGeneral.GetItemMetadata(index).AsInt32() == (id ?? 0))
            {
                _requiredGeneral.Select(index);
                return;
            }
        _requiredGeneral.Select(0);
    }

    private static void ShowSkillDescription(Label label, OptionButton option, IReadOnlyDictionary<string, string> details)
    {
        var full = details.GetValueOrDefault(SelectedCode(option), "선택한 스킬이 없습니다.");
        var lines = full.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(2);
        var shortText = string.Join(" · ", lines).Trim();
        label.Text = shortText.Length <= 180 ? shortText : shortText[..179] + "…";
        label.TooltipText = full;
    }

    private void UpdateSkillDescriptions()
    {
        ShowSkillDescription(_activeDescription, _active, _activeDetails);
        for (var index = 0; index < _battlePassives.Count; index++)
            ShowSkillDescription(_battleDescriptions[index], _battlePassives[index], _battleDetails);
        for (var index = 0; index < _adminPassives.Count; index++)
            ShowSkillDescription(_adminDescriptions[index], _adminPassives[index], _adminDetails);
    }

    private static string SelectedCode(OptionButton option)
        => option.Selected < 0 ? "" : option.GetItemMetadata(option.Selected).AsString();

    private void StartNativeNameInput()
    {
        if (!OperatingSystem.IsWindows() || DisplayServer.GetName() == "headless" || _name is null)
            return;
        Callable.From(() =>
        {
            if (_tabs.CurrentTab != 1 || _nativeName is not null || !_name.IsInsideTree()) return;
            _nativeName = NativeNameEdit.Create(_name, GetViewport());
        }).CallDeferred();
    }

    private void StopNativeNameInput()
    {
        if (_nativeName is null) return;
        _name.Text = _nativeName.Text;
        _nativeName.Dispose();
        _nativeName = null;
    }

    private string CurrentName()
        => (_nativeName?.Text ?? _name.Text).Replace("\r", "").Replace("\n", "").Trim();

    private string DefaultPortrait()
        => $"res://assets/portraits/created/{(_gender.Selected == 1 ? "women" : "man")}1.png";

    private void RebuildPortraitGrid()
    {
        foreach (var child in _portraitGrid.GetChildren())
        {
            _portraitGrid.RemoveChild(child);
            child.QueueFree();
        }
        var prefix = _gender.Selected == 1 ? "women" : "man";
        for (var number = 1; number <= 10; number++)
        {
            var portrait = $"res://assets/portraits/created/{prefix}{number}.png";
            var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(portrait));
            if (image is null || image.IsEmpty()) continue;
            image.Resize(80, 96);
            var choice = new Button { Text = $"{number}", Icon = ImageTexture.CreateFromImage(image),
                ExpandIcon = true, CustomMinimumSize = new Vector2(115, 106),
                TooltipText = $"{(_gender.Selected == 1 ? "여성" : "남성")} 기본 초상 {number}" };
            choice.Pressed += () => SelectPortrait(portrait);
            _portraitGrid.AddChild(choice);
        }
        var galleryDirectory = ProjectSettings.GlobalizePath("user://general-presets/portraits/");
        if (!Directory.Exists(galleryDirectory)) return;
        foreach (var file in Directory.EnumerateFiles(galleryDirectory, "*.png").OrderBy(Path.GetFileName))
        {
            var image = Image.LoadFromFile(file);
            if (image is null || image.IsEmpty()) continue;
            image.Resize(80, 96);
            var portrait = "user://general-presets/portraits/" + Path.GetFileName(file);
            var choice = new Button { Text = "등록", Icon = ImageTexture.CreateFromImage(image),
                ExpandIcon = true, CustomMinimumSize = new Vector2(115, 106),
                TooltipText = $"외부 초상 · {Path.GetFileName(file)}" };
            choice.Pressed += () => SelectPortrait(portrait);
            _portraitGrid.AddChild(choice);
        }
    }

    private void SelectPortrait(string? path)
    {
        _pendingExternalPath = null;
        _portraitPath = path;
        ShowPortrait(path is null ? null : ProjectSettings.GlobalizePath(path),
            path is null ? "사진 없음" : Path.GetFileName(path));
    }

    private void ShowPortrait(string? globalPath, string caption)
    {
        _portraitPreview.Texture = null;
        _portraitLabel.Text = caption;
        if (globalPath is null || !File.Exists(globalPath)) return;
        var image = Image.LoadFromFile(globalPath);
        if (image is not null && !image.IsEmpty())
            _portraitPreview.Texture = ImageTexture.CreateFromImage(image);
        else
            _portraitLabel.Text = "이미지를 읽을 수 없습니다.";
    }

    private void OnExternalPortraitSelected(string path)
    {
        try
        {
            var portrait = RegisterGalleryPortrait(path);
            RebuildPortraitGrid();
            SelectPortrait(portrait);
            _status.Text = "외부 초상을 기본 얼굴 목록에 등록했습니다.";
        }
        catch (Exception error) { _status.Text = "초상 등록 실패: " + error.Message; }
    }

    private static string RegisterGalleryPortrait(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length > 20 * 1024 * 1024)
            throw new InvalidDataException("20MB 이하의 이미지 파일을 선택하세요.");
        var image = Image.LoadFromFile(path);
        if (image is null || image.IsEmpty()) throw new InvalidDataException("이미지를 읽을 수 없습니다.");
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        var portrait = $"user://general-presets/portraits/custom_{hash}.png";
        var target = ProjectSettings.GlobalizePath(portrait);
        if (!File.Exists(target)) ImportExternalPortrait(path, target);
        return portrait;
    }

    private void OnGalleryPortraitsSelected(string[] paths)
    {
        var added = 0;
        var errors = new List<string>();
        foreach (var path in paths)
        {
            try { RegisterGalleryPortrait(path); added++; }
            catch (Exception error) { errors.Add($"{Path.GetFileName(path)}: {error.Message}"); }
        }
        RebuildPortraitGrid();
        _status.Text = errors.Count == 0 ? $"외부 초상 {added}장을 기본 얼굴 목록에 등록했습니다."
            : $"{added}장 등록 · 실패 {errors.Count}장: {string.Join(" / ", errors.Take(2))}";
    }

    private static void ImportExternalPortrait(string sourcePath, string target)
    {
        var image = Image.LoadFromFile(sourcePath);
        if (image is null || image.IsEmpty()) throw new InvalidDataException("초상 파일을 읽을 수 없습니다.");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        var result = image.SavePng(target);
        if (result != Error.Ok) throw new IOException($"초상 저장 실패: {result}");
    }

    private void NewGeneral()
    {
        StopNativeNameInput();
        _editingId = null;
        _overview.Text = "새 장수 · 초상과 능력을 정해 주세요.";
        _list.DeselectAll();
        _name.Text = "";
        _gender.Select(0); _birth.Value = 170; _region.Select(0); _points.Value = 100;
        SelectRequiredGeneral(null);
        _description.Text = "";
        _might.Value = 50; _intellect.Value = 50; _politics.Value = 50;
        foreach (var option in _aptitudes.Values) option.Select(3);
        _active.Select(0);
        foreach (var option in _battlePassives.Concat(_adminPassives)) option.Select(0);
        UpdateSkillDescriptions();
        SelectPortrait(DefaultPortrait());
        RebuildPortraitGrid();
        _status.Text = "새 장수를 입력하세요. 초상은 나중에 추가할 수 있습니다.";
        StartNativeNameInput();
    }

    private void LoadSelected(int index)
    {
        if (index < 0 || index >= _created.Count) return;
        var entry = _created[index];
        var general = entry.General;
        StopNativeNameInput();
        _editingId = general.Id;
        _overview.Text = $"{general.Name} · 생성 장수 ID {general.Id}";
        _name.Text = general.Name;
        _gender.Select(entry.Gender == "female" ? 1 : 0);
        _birth.Value = general.Birth;
        for (var regionIndex = 0; regionIndex < _region.ItemCount; regionIndex++)
            if (_region.GetItemMetadata(regionIndex).AsString() == general.Region)
            {
                _region.Select(regionIndex);
                break;
            }
        _points.Value = entry.RecruitPointCost;
        SelectRequiredGeneral(entry.RequiredGeneralId);
        _description.Text = general.Desc;
        _might.Value = general.Might; _intellect.Value = general.Intellect; _politics.Value = general.Politics;
        foreach (var (code, option) in _aptitudes)
            option.Select(Array.IndexOf(Grades, general.Aptitudes[code]));
        SelectCode(_active, general.BattleActive ?? "");
        for (var i = 0; i < _battlePassives.Count; i++)
            SelectCode(_battlePassives[i], general.BattlePassives.ElementAtOrDefault(i)?.Code ?? "");
        for (var i = 0; i < _adminPassives.Count; i++)
            SelectCode(_adminPassives[i], general.AdminPassives.ElementAtOrDefault(i)?.Code ?? "");
        UpdateSkillDescriptions();
        SelectPortrait(entry.PortraitPath);
        RebuildPortraitGrid();
        _status.Text = $"{general.Name} 편집 중 · ID {general.Id}";
        StartNativeNameInput();
    }

    private static void SelectCode(OptionButton option, string code)
    {
        for (var index = 0; index < option.ItemCount; index++)
            if (option.GetItemMetadata(index).AsString() == code) { option.Select(index); return; }
        option.Select(0);
    }

    private CreatedGeneralRecord FromForm(int id)
    {
        var battle = _battlePassives.Select(SelectedCode).Where(code => code.Length > 0).ToList();
        var admin = _adminPassives.Select(SelectedCode).Where(code => code.Length > 0).ToList();
        if (battle.Count != battle.Distinct().Count() || admin.Count != admin.Distinct().Count())
            throw new InvalidDataException("같은 패시브 스킬을 중복 선택할 수 없습니다.");
        if (string.IsNullOrWhiteSpace(CurrentName())) throw new InvalidDataException("이름을 입력하세요.");
        var record = new GeneralEditorRecord(id, CurrentName(),
            _aptitudes.ToDictionary(entry => entry.Key, entry => Grades[entry.Value.Selected]),
            (int)_might.Value, (int)_intellect.Value, (int)_politics.Value,
            string.IsNullOrEmpty(SelectedCode(_active)) ? null : SelectedCode(_active),
            battle.Select(code => new GeneralEditorSkill(code, 1)).ToList(),
            admin.Select(code => new GeneralEditorSkill(code, 1)).ToList(),
            (int)_birth.Value, 0, SelectedCode(_region), _description.Text.Trim());
        var portrait = _pendingExternalPath is null ? _portraitPath
            : $"user://general-presets/portraits/{id}.png";
        return new CreatedGeneralRecord(record, _gender.Selected == 1 ? "female" : "male", (int)_points.Value,
            portrait, _requiredGeneral.GetItemMetadata(_requiredGeneral.Selected).AsInt32() is var required && required > 0
                ? required : null);
    }

    private void SaveSelected()
    {
        try
        {
            var id = _editingId ?? Math.Max(9999, _created.Select(entry => entry.General.Id).DefaultIfEmpty().Max()) + 1;
            var entry = FromForm(id);
            var updated = _created.Where(item => item.General.Id != id).Append(entry).OrderBy(item => item.General.Id).ToList();
            var active = new ActiveSkillLoader().LoadFromDirectory(_dataDirectory).Select(s => s.Code).ToHashSet();
            var passive = new PassiveSkillLoader().LoadFromDirectory(_dataDirectory).Select(s => s.Code).ToHashSet();
            var admin = new AdminSkillLoader().LoadFromDirectory(_dataDirectory).Select(s => s.Code).ToHashSet();
            var valid = GeneralEditorStore.Validate(updated.Select(item => item.General).ToList(), active, passive, admin);
            if (valid.Errors.Count > 0) throw new InvalidDataException(string.Join(" / ", valid.Errors));
            CreatedGeneralStore.Apply(new ScenarioLoader().LoadFromDirectory(_dataDirectory), updated);
            if (_pendingExternalPath is not null)
                ImportExternalPortrait(_pendingExternalPath, ProjectSettings.GlobalizePath(entry.PortraitPath!));
            Write(updated);
            _created.Clear(); _created.AddRange(updated);
            _editingId = id;
            _overview.Text = $"{entry.General.Name} · 생성 장수 ID {id}";
            _pendingExternalPath = null;
            _portraitPath = entry.PortraitPath;
            RefreshList();
            PopulateRequiredGenerals(entry.RequiredGeneralId);
            _status.Text = $"{entry.General.Name} 저장 완료 · 다음 새 게임부터 영입 후보로 등장합니다.";
        }
        catch (Exception error) { _status.Text = "저장 실패: " + error.Message; }
    }

    private void DeleteSelected()
    {
        if (_editingId is not { } id) { _status.Text = "삭제할 장수를 선택하세요."; return; }
        try
        {
            var updated = _created.Where(item => item.General.Id != id).ToList();
            if (updated.Any(entry => entry.RequiredGeneralId == id))
                throw new InvalidDataException("다른 생성 장수의 영입 선행 조건입니다. 먼저 그 조건을 변경하세요.");
            Write(updated);
            _created.Clear(); _created.AddRange(updated);
            RefreshList(); PopulateRequiredGenerals(); NewGeneral();
            _status.Text = "생성 장수 목록에서 삭제했습니다. 기존 저장 게임에는 영향이 없습니다.";
        }
        catch (Exception error) { _status.Text = "삭제 실패: " + error.Message; }
    }

    private static void Write(IReadOnlyList<CreatedGeneralRecord> entries)
    {
        var path = ProjectSettings.GlobalizePath(CreatedGeneralPresets.UserPath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, CreatedGeneralStore.Save(entries));
        File.Move(temp, path, true);
    }

    private void RefreshList()
    {
        _list.Clear();
        foreach (var entry in _created)
            _list.AddItem($"{entry.General.Name}  ·  {entry.General.Id}");
    }

    private static string FindDataDirectory()
    {
        var directory = new DirectoryInfo(ProjectSettings.GlobalizePath("res://"));
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "data");
            if (File.Exists(Path.Combine(candidate, "generals.json"))) return candidate;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("data/generals.json을 찾을 수 없습니다.");
    }

    private void RunQa()
    {
        _name.Text = "생성 장수 QA";
        var scenario = new ScenarioLoader().LoadFromDirectory(_dataDirectory);
        var required = scenario.Generals[0].Id.Value;
        SelectRequiredGeneral(required);
        _active.Select(1);
        UpdateSkillDescriptions();
        var sample = FromForm(10000);
        var virtualHero = sample with
        {
            General = sample.General with { Id = 10001, Name = "창작 선행 QA" },
            RequiredGeneralId = 10000,
        };
        var applied = CreatedGeneralStore.Apply(scenario,
            CreatedGeneralStore.Load(CreatedGeneralStore.Save([sample, virtualHero])));
        var passed = applied.Generals.Count == scenario.Generals.Count + 2
            && _tabs.GetTabTitle(0) == "초상·얼굴" && _tabs.GetTabTitle(1) == "기본 정보"
            && applied.HeroUnlockList.Any(hero => hero.General.Value == 10000)
            && scenario.Generals.All(general => general.Id.Value != 10000)
            && sample.RequiredGeneralId == required
            && applied.HeroUnlockList.Single(hero => hero.General.Value == 10001).RequiredGeneral?.Value == 10000
            && _adminPassives.Count == 3 && _battlePassives.Count == 3
            && _region.ItemCount > 3 && _region.GetItemText(0) != SelectedCode(_region)
            && !string.IsNullOrWhiteSpace(_activeDescription.Text)
            && applied.Generals.Single(general => general.Id.Value == 10000).PortraitPath == DefaultPortrait()
            && Enumerable.Range(1, 10).All(number =>
                File.Exists(ProjectSettings.GlobalizePath($"res://assets/portraits/created/man{number}.png"))
                && File.Exists(ProjectSettings.GlobalizePath($"res://assets/portraits/created/women{number}.png")));
        var imported = Path.Combine(Path.GetTempPath(), $"general-portrait-qa-{Guid.NewGuid():N}.png");
        try
        {
            ImportExternalPortrait(ProjectSettings.GlobalizePath(DefaultPortrait()), imported);
            passed &= File.Exists(imported) && new FileInfo(imported).Length > 0;
            var qaHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(imported))).ToLowerInvariant();
            var qaGalleryFile = ProjectSettings.GlobalizePath($"user://general-presets/portraits/custom_{qaHash}.png");
            var existedBeforeQa = File.Exists(qaGalleryFile);
            OnGalleryPortraitsSelected([imported, imported]);
            var registered = RegisterGalleryPortrait(imported);
            var registeredFile = ProjectSettings.GlobalizePath(registered);
            RebuildPortraitGrid();
            SelectPortrait(registered);
            passed &= _portraitPath == registered && _portraitGrid.GetChildCount() >= 11
                && _galleryFileDialog.FileMode == FileDialog.FileModeEnum.OpenFiles
                && _status.Text.Contains("2장을", StringComparison.Ordinal)
                && CreatedGeneralStore.Load(CreatedGeneralStore.Save([FromForm(10002)]))[0].PortraitPath == registered;
            if (!existedBeforeQa) File.Delete(registeredFile);
        }
        finally { if (File.Exists(imported)) File.Delete(imported); }
        GD.Print($"[generalcreatorqa] passed={passed} original={scenario.Generals.Count} new={applied.Generals.Count}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
