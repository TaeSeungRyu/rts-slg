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
    private readonly Dictionary<string, string> _activeDetails = new();
    private readonly Dictionary<string, string> _battleDetails = new();
    private readonly Dictionary<string, string> _adminDetails = new();
    private ItemList _list = null!;
    private LineEdit _name = null!;
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
    private Button _portraitPrevious = null!;
    private Button _portraitNext = null!;
    private Label _portraitPageLabel = null!;
    private int _portraitPage;
    private TextureRect _commonPortraitPreview = null!;
    private Label _commonName = null!;
    private Label _commonId = null!;
    private Label _commonGender = null!;
    private Label _commonBirth = null!;
    private Label _commonRegion = null!;
    private FileDialog _galleryFileDialog = null!;
    private ScrollContainer _skillDetailScroll = null!;
    private ColorRect _galleryLoading = null!;
    private Label _galleryLoadingLabel = null!;
    private string? _portraitPath;
    private string? _pendingExternalPath;
    private int? _editingId;
    private string _savedFormSnapshot = "";
    private string _dataDirectory = "";

    public override void _Ready()
    {
        BuildUi();
        foreach (var option in FindChildren("*", "OptionButton", true, false).OfType<OptionButton>())
            GeneralWorkspaceStyle.StyleOptionButton(option);
        try
        {
            _dataDirectory = FindDataDirectory();
            PopulateSkills();
            PopulateRegions();
            var path = ProjectSettings.GlobalizePath(CreatedGeneralPresets.UserPath);
            if (File.Exists(path)) _created.AddRange(CreatedGeneralStore.Load(File.ReadAllText(path)));
            PopulateRequiredGenerals();
            RefreshList();
            if (_created.Count > 0)
            {
                _list.Select(0);
                LoadSelected(0);
            }
            else NewGeneral();
        }
        catch (Exception error) { _status.Text = "불러오기 실패: " + error.Message; }
        if (OS.GetCmdlineUserArgs().Contains("--generalcreatorqa")) CallDeferred(nameof(RunQa));
    }

    private void BuildUi()
    {
        GeneralWorkspaceStyle.AddBackground(this);
        AddChild(new ColorRect { Color = new Color("160e0c"), AnchorRight = 1, AnchorBottom = 1,
            MouseFilter = MouseFilterEnum.Ignore });
        var root = new VBoxContainer { AnchorRight = 1, AnchorBottom = 1,
            OffsetLeft = 18, OffsetTop = 18, OffsetRight = -18, OffsetBottom = -18,
            Theme = GeneralWorkspaceStyle.Theme() };
        root.AddThemeConstantOverride("separation", 10);
        AddChild(root);
        GeneralWorkspaceStyle.Header(root, "신규 장수 등록", "초상부터 능력과 스킬까지 한 명의 장수를 완성합니다");
        var toolbar = new HBoxContainer();
        root.AddChild(toolbar);
        if (ScenarioStartMenu.FromStartMenu)
        {
            var back = new Button { Text = "← 메인 메뉴", CustomMinimumSize = new Vector2(140, 36) };
            back.Pressed += RequestReturnToStart;
            toolbar.AddChild(back);
        }
        toolbar.AddChild(new Label { Text = "생성 장수는 다음 새 게임부터 영입 후보에 추가됩니다.",
            SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Center });
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);
        root.AddChild(body);
        var left = GeneralWorkspaceStyle.Column(body, "장수 목록", 300);
        left.Name = "CreatorRosterColumn";
        left.CustomMinimumSize = new Vector2(300, 0);
        _list = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        _list.ItemSelected += index => LoadSelected((int)index);
        left.AddChild(_list);
        var add = new Button { Text = "+ 새 장수" };
        add.Pressed += NewGeneral;
        left.AddChild(add);
        var delete = new Button { Text = "선택 장수 삭제" };
        delete.Pressed += DeleteSelected;
        left.AddChild(delete);

        var preview = GeneralWorkspaceStyle.Column(body, "장수 미리보기", 380);
        preview.Name = "CreatorPreviewColumn";
        preview.CustomMinimumSize = new Vector2(380, 0);
        var previewFrame = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill };
        var previewStyle = new StyleBoxFlat { BgColor = new Color("211512"), BorderColor = new Color("765637") };
        previewStyle.SetBorderWidthAll(1);
        previewStyle.SetCornerRadiusAll(7);
        previewStyle.SetContentMarginAll(12);
        previewFrame.AddThemeStyleboxOverride("panel", previewStyle);
        preview.AddChild(previewFrame);
        var previewContent = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        previewContent.AddThemeConstantOverride("separation", 10);
        previewFrame.AddChild(previewContent);
        var commonPortraitFrame = new PanelContainer { CustomMinimumSize = new Vector2(250, 340),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        var commonPortraitStyle = new StyleBoxFlat { BgColor = new Color("160e0c"), BorderColor = new Color("d1a15a") };
        commonPortraitStyle.SetBorderWidthAll(1);
        commonPortraitStyle.SetCornerRadiusAll(7);
        commonPortraitStyle.SetContentMarginAll(3);
        commonPortraitFrame.AddThemeStyleboxOverride("panel", commonPortraitStyle);
        previewContent.AddChild(commonPortraitFrame);
        _commonPortraitPreview = new TextureRect { CustomMinimumSize = new Vector2(244, 334),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Modulate = Colors.White, SelfModulate = Colors.White };
        commonPortraitFrame.AddChild(_commonPortraitPreview);
        _commonName = new Label { Text = "새 장수", HorizontalAlignment = HorizontalAlignment.Center };
        _commonName.AddThemeFontSizeOverride("font_size", 22);
        _commonName.AddThemeColorOverride("font_color", new Color("f0d6a8"));
        previewContent.AddChild(_commonName);
        _commonId = PreviewLine(previewContent, "ID", "신규 발급");
        _commonGender = PreviewLine(previewContent, "성별", "남성");
        _commonBirth = PreviewLine(previewContent, "출생년도", "170년");
        _commonRegion = PreviewLine(previewContent, "출신", "미지정");

        var right = GeneralWorkspaceStyle.Column(body, "장수 편집");
        right.Name = "CreatorEditorColumn";
        right.AddThemeConstantOverride("separation", 10);
        _overview = new Label { Text = "새 장수", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _overview.Visible = false;
        right.AddChild(_overview);
        _tabs = new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill };
        right.AddChild(_tabs);
        VBoxContainer Page(string name)
        {
            var scroll = new ScrollContainer { Name = name, SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill };
            GeneralWorkspaceStyle.StyleScrollContainer(scroll);
            _tabs.AddChild(scroll);
            var inset = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            inset.AddThemeConstantOverride("margin_right", 10);
            scroll.AddChild(inset);
            var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            content.AddThemeConstantOverride("separation", 8);
            inset.AddChild(content);
            return content;
        }
        var basic = Page("기본 정보");
        var portraits = Page("초상·얼굴");
        var identity = GeneralWorkspaceStyle.Section(basic, "기본 정보");
        var basicGrid = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        basicGrid.Name = "CreatorBasicGrid";
        basicGrid.AddThemeConstantOverride("h_separation", 16);
        basicGrid.AddThemeConstantOverride("v_separation", 18);
        identity.AddChild(basicGrid);
        _name = new LineEdit { MaxLength = 20, PlaceholderText = "장수 이름", KeepEditingOnTextSubmit = true };
        AddBasicField(basicGrid, "이름", _name);
        NativeTextInputDialog.Attach(_name);
        _gender = new OptionButton();
        _gender.AddItem("남성"); _gender.AddItem("여성");
        _gender.ItemSelected += _ =>
        {
            if (_pendingExternalPath is null && _portraitPath?.StartsWith("res://assets/portraits/created/") == true)
                SelectPortrait(DefaultPortrait());
            RebuildPortraitGrid();
        };
        AddBasicField(basicGrid, "성별", _gender);
        _birth = Spin(-500, 500, 170);
        AddBasicField(basicGrid, "출생년도", _birth);
        _region = new OptionButton();
        AddBasicField(basicGrid, "출신", _region);
        _points = Spin(0, 3000, 100);
        AddBasicField(basicGrid, "영입 포인트", _points);
        _requiredGeneral = new OptionButton();
        AddBasicField(basicGrid, "영입 선행 장수", _requiredGeneral);
        identity.AddChild(new Label { Text = "선행 장수를 지정하면 해당 장수가 소속된 세력만 영입할 수 있습니다.",
            Modulate = new Color("bda581") });
        var introduction = GeneralWorkspaceStyle.Section(basic, "장수 소개");
        _description = new TextEdit { CustomMinimumSize = new Vector2(0, 115) };
        introduction.AddChild(_description);
        NativeTextInputDialog.Attach(_description, "장수 소개 편집");
        foreach (var input in new Control[] { _name, _gender, _birth, _region, _points, _requiredGeneral, _description })
            StyleCreatorInput(input);

        var portraitSection = GeneralWorkspaceStyle.Section(portraits,
            "초상 얼굴 (기본 얼굴을 고르거나 외부 PNG·JPG·WEBP 파일을 가져오세요.)");
        var portraitActions = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        portraitActions.AddThemeConstantOverride("separation", 10);
        portraitSection.AddChild(portraitActions);
        var import = new Button { Text = "+ 외부 이미지 등록", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        import.Pressed += () => _galleryFileDialog.PopupCentered(new Vector2I(850, 620));
        portraitActions.AddChild(import);
        var clear = new Button { Text = "사진 비우기", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        clear.Pressed += () => SelectPortrait(null);
        portraitActions.AddChild(clear);
        var gallery = GeneralWorkspaceStyle.Section(portraitSection, "초상 선택");
        gallery.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _portraitGrid = new GridContainer { Name = "CreatorPortraitGrid", Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _portraitGrid.AddThemeConstantOverride("h_separation", 10);
        _portraitGrid.AddThemeConstantOverride("v_separation", 12);
        gallery.AddChild(_portraitGrid);
        var portraitNavigation = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        portraitNavigation.AddThemeConstantOverride("separation", 14);
        gallery.AddChild(portraitNavigation);
        _portraitPrevious = new Button { Text = "이전", CustomMinimumSize = new Vector2(80, 34) };
        _portraitPrevious.Pressed += () => { _portraitPage--; RebuildPortraitGrid(); };
        portraitNavigation.AddChild(_portraitPrevious);
        _portraitPageLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(140, 0) };
        portraitNavigation.AddChild(_portraitPageLabel);
        _portraitNext = new Button { Text = "다음", CustomMinimumSize = new Vector2(80, 34) };
        _portraitNext.Pressed += () => { _portraitPage++; RebuildPortraitGrid(); };
        portraitNavigation.AddChild(_portraitNext);
        _galleryFileDialog = new FileDialog { Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenFiles, Title = "기본 얼굴 여러 장 등록" };
        _galleryFileDialog.AddFilter("*.png, *.jpg, *.jpeg, *.webp", "이미지 파일");
        _galleryFileDialog.FilesSelected += OnGalleryPortraitsSelected;
        AddChild(_galleryFileDialog);
        _galleryLoading = new ColorRect { Color = new Color(0, 0, 0, 0.72f), Visible = false,
            MouseFilter = MouseFilterEnum.Stop, ZIndex = 100 };
        _galleryLoading.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_galleryLoading);
        var loadingCenter = new CenterContainer();
        loadingCenter.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _galleryLoading.AddChild(loadingCenter);
        var loadingPanel = new PanelContainer { CustomMinimumSize = new Vector2(320, 90) };
        loadingCenter.AddChild(loadingPanel);
        _galleryLoadingLabel = new Label { Text = "외부 이미지 등록 중…",
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        loadingPanel.AddChild(_galleryLoadingLabel);

        var abilities = Page("능력·적성");
        var abilitySection = GeneralWorkspaceStyle.Section(abilities, "기본 능력치");
        var stats = new GridContainer { Name = "CreatorAbilityGrid", Columns = 3,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        stats.AddThemeConstantOverride("h_separation", 12);
        abilitySection.AddChild(stats);
        _might = Spin(0, 100, 50); AddCompactField(stats, "무력", _might);
        _intellect = Spin(0, 100, 50); AddCompactField(stats, "지력", _intellect);
        _politics = Spin(0, 100, 50); AddCompactField(stats, "정치", _politics);
        var aptitudeSection = GeneralWorkspaceStyle.Section(abilities, "병종 적성");
        var grid = new GridContainer { Name = "CreatorAptitudeGrid", Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 10);
        grid.AddThemeConstantOverride("v_separation", 12);
        aptitudeSection.AddChild(grid);
        foreach (var (code, name) in Troops)
        {
            var option = new OptionButton();
            foreach (var grade in Grades) option.AddItem(grade);
            option.Select(3);
            AddCompactField(grid, name, option);
            _aptitudes[code] = option;
        }

        var skills = Page("스킬");
        var activeSection = GeneralWorkspaceStyle.Section(skills, "전투 액티브");
        var activeGrid = new GridContainer { Columns = 1, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        activeSection.AddChild(activeGrid);
        _active = new OptionButton(); AddCompactField(activeGrid, "전투 액티브", _active);
        var battleSection = GeneralWorkspaceStyle.Section(skills, "전투 패시브");
        var battleGrid = new GridContainer { Name = "CreatorBattleSkillGrid", Columns = 3,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        battleGrid.AddThemeConstantOverride("h_separation", 10);
        battleSection.AddChild(battleGrid);
        for (var index = 0; index < 3; index++)
        {
            var option = new OptionButton();
            AddCompactField(battleGrid, $"전투 패시브 {index + 1}", option);
            _battlePassives.Add(option);
        }
        var adminSection = GeneralWorkspaceStyle.Section(skills, "내정 패시브");
        var adminGrid = new GridContainer { Name = "CreatorAdminSkillGrid", Columns = 3,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        adminGrid.AddThemeConstantOverride("h_separation", 10);
        adminSection.AddChild(adminGrid);
        for (var index = 0; index < 3; index++)
        {
            var option = new OptionButton();
            AddCompactField(adminGrid, $"내정 패시브 {index + 1}", option);
            _adminPassives.Add(option);
        }
        var detailSection = GeneralWorkspaceStyle.Section(skills, "선택 스킬 상세 설명");
        _skillDetailScroll = new ScrollContainer { Name = "CreatorSkillDetailScroll",
            CustomMinimumSize = new Vector2(0, 112), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto };
        GeneralWorkspaceStyle.StyleScrollContainer(_skillDetailScroll);
        detailSection.AddChild(_skillDetailScroll);
        var detailInset = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        detailInset.AddThemeConstantOverride("margin_right", 10);
        _skillDetailScroll.AddChild(detailInset);
        _activeDescription = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        detailInset.AddChild(_activeDescription);
        ConnectSkillDetail(_active, _activeDetails);
        foreach (var option in _battlePassives) ConnectSkillDetail(option, _battleDetails);
        foreach (var option in _adminPassives) ConnectSkillDetail(option, _adminDetails);
        _tabs.MoveChild(portraits.GetParent().GetParent(), 3);
        _tabs.CurrentTab = 0;
        _name.TextChanged += _ => UpdateCommonPreview();
        _gender.ItemSelected += _ => UpdateCommonPreview();
        _birth.ValueChanged += _ => UpdateCommonPreview();
        _region.ItemSelected += _ => UpdateCommonPreview();
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.Off };
        var footer = GeneralWorkspaceStyle.EditorFooter(root, _status);
        footer.AddChild(new Label { Text = "등록 내용은 다음 새 게임부터 적용됩니다.",
            SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center });
        var save = new Button { Text = "장수 저장", CustomMinimumSize = new Vector2(140, 38) };
        save.Pressed += SaveSelected;
        footer.AddChild(save);
    }

    private static SpinBox Spin(double min, double max, double value)
        => new() { MinValue = min, MaxValue = max, Value = value, Step = 1, Rounded = true,
            CustomMinimumSize = new Vector2(100, 0) };

    private static Label PreviewLine(VBoxContainer parent, string title, string value)
    {
        var label = new Label { Text = $"{title}  ·  {value}", HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 15);
        label.AddThemeColorOverride("font_color", new Color("bda581"));
        parent.AddChild(label);
        return label;
    }

    private static void AddBasicField(GridContainer grid, string label, Control input)
    {
        var field = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 68) };
        field.AddThemeConstantOverride("separation", 6);
        var heading = new Label { Text = label };
        heading.AddThemeColorOverride("font_color", new Color("bda581"));
        field.AddChild(heading);
        input.CustomMinimumSize = new Vector2(0, 40);
        input.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        field.AddChild(input);
        grid.AddChild(field);
    }

    private static void AddCompactField(GridContainer grid, string label, Control input)
    {
        var field = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 62) };
        field.AddThemeConstantOverride("separation", 5);
        var heading = new Label { Text = label };
        heading.AddThemeColorOverride("font_color", new Color("bda581"));
        field.AddChild(heading);
        input.CustomMinimumSize = new Vector2(0, 36);
        input.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        field.AddChild(input);
        grid.AddChild(field);
    }

    private static void StyleCreatorInput(Control input)
    {
        var normal = new StyleBoxFlat { BgColor = new Color("35211b"), BorderColor = new Color("765637") };
        normal.SetBorderWidthAll(1);
        normal.SetCornerRadiusAll(6);
        normal.SetContentMarginAll(9);
        var focus = (StyleBoxFlat)normal.Duplicate();
        focus.BorderColor = new Color("d1a15a");
        focus.SetBorderWidthAll(2);
        var targets = input is SpinBox spin ? new Control[] { input, spin.GetLineEdit() } : new[] { input };
        foreach (var target in targets)
        {
            target.AddThemeStyleboxOverride("normal", normal);
            target.AddThemeStyleboxOverride("read_only", normal);
            target.AddThemeStyleboxOverride("focus", focus);
            target.AddThemeColorOverride("font_color", new Color("f0d6a8"));
            target.AddThemeColorOverride("font_uneditable_color", new Color("f0d6a8"));
        }
    }

    private void UpdateCommonPreview()
    {
        if (_commonName is null) return;
        _commonName.Text = string.IsNullOrWhiteSpace(_name.Text) ? "새 장수" : _name.Text;
        _commonId.Text = $"ID  ·  {(_editingId is { } id ? id.ToString() : "저장 시 신규 발급")}";
        _commonGender.Text = $"성별  ·  {(_gender.Selected == 1 ? "여성" : "남성")}";
        _commonBirth.Text = $"출생년도  ·  {(int)_birth.Value}년";
        var region = _region.Selected >= 0 && _region.Selected < _region.ItemCount
            ? _region.GetItemText(_region.Selected) : "미지정";
        _commonRegion.Text = $"출신  ·  {region}";
    }

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

    private void ShowSkillDescription(Label label, OptionButton option, IReadOnlyDictionary<string, string> details)
    {
        var full = details.GetValueOrDefault(SelectedCode(option), "선택한 스킬이 없습니다.");
        var lines = full.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith("전투 패시브 ·", StringComparison.Ordinal)
                && !line.StartsWith("내정 패시브 ·", StringComparison.Ordinal)
                && line != "조건을 만족하면 자동 적용됩니다."
                && line != "직접 사용하는 액티브가 아닙니다.");
        label.Text = string.Join('\n', lines);
        label.TooltipText = full;
        _skillDetailScroll.CallDeferred(ScrollContainer.MethodName.SetVScroll, 0);
    }

    private void ConnectSkillDetail(OptionButton option, IReadOnlyDictionary<string, string> details)
    {
        option.ItemSelected += _ => ShowSkillDescription(_activeDescription, option, details);
        option.FocusEntered += () => ShowSkillDescription(_activeDescription, option, details);
    }

    private void UpdateSkillDescriptions()
    {
        if (!string.IsNullOrEmpty(SelectedCode(_active)))
            ShowSkillDescription(_activeDescription, _active, _activeDetails);
        else if (_battlePassives.FirstOrDefault(option => !string.IsNullOrEmpty(SelectedCode(option))) is { } battle)
            ShowSkillDescription(_activeDescription, battle, _battleDetails);
        else if (_adminPassives.FirstOrDefault(option => !string.IsNullOrEmpty(SelectedCode(option))) is { } admin)
            ShowSkillDescription(_activeDescription, admin, _adminDetails);
        else
            ShowSkillDescription(_activeDescription, _active, _activeDetails);
    }

    private static string SelectedCode(OptionButton option)
        => option.Selected < 0 ? "" : option.GetItemMetadata(option.Selected).AsString();

    private string CurrentName()
        => _name.Text.Replace("\r", "").Replace("\n", "").Trim();

    private string DefaultPortrait()
        => $"res://assets/portraits/created/{(_gender.Selected == 1 ? "women" : "man")}1.png";

    private static int PortraitPageCount(int count, int pageSize) => Math.Max(1, (count + pageSize - 1) / pageSize);

    private void RebuildPortraitGrid(string? focusPath = null)
    {
        foreach (var child in _portraitGrid.GetChildren())
        {
            _portraitGrid.RemoveChild(child);
            child.QueueFree();
        }
        var choices = new List<(string Path, string Tooltip, bool Imported)>();
        var prefix = _gender.Selected == 1 ? "women" : "man";
        for (var number = 1; number <= 10; number++)
        {
            var portrait = $"res://assets/portraits/created/{prefix}{number}.png";
            if (File.Exists(ProjectSettings.GlobalizePath(portrait)))
                choices.Add((portrait, $"{(_gender.Selected == 1 ? "여성" : "남성")} 기본 초상 {number}", false));
        }
        var galleryDirectory = ProjectSettings.GlobalizePath("user://general-presets/portraits/");
        if (Directory.Exists(galleryDirectory))
            foreach (var file in Directory.EnumerateFiles(galleryDirectory, "*.png").OrderBy(Path.GetFileName))
                choices.Add(("user://general-presets/portraits/" + Path.GetFileName(file),
                    $"외부 초상 · {Path.GetFileName(file)}", true));
        const int columns = 4;
        var rows = GetViewport().GetVisibleRect().Size.Y >= 820 ? 3 : 2;
        var pageSize = columns * rows;
        _portraitGrid.Columns = columns;
        if (focusPath is not null)
        {
            var focusIndex = choices.FindIndex(choice => choice.Path == focusPath);
            if (focusIndex >= 0) _portraitPage = focusIndex / pageSize;
        }
        var pageCount = PortraitPageCount(choices.Count, pageSize);
        _portraitPage = Math.Clamp(_portraitPage, 0, pageCount - 1);
        var pageChoices = choices.Skip(_portraitPage * pageSize).Take(pageSize).ToArray();
        for (var rowStart = 0; rowStart < pageChoices.Length; rowStart += columns)
        {
            var count = Math.Min(columns, pageChoices.Length - rowStart);
            var leftGap = (columns - count) / 2;
            for (var empty = 0; empty < leftGap; empty++) _portraitGrid.AddChild(PortraitSpacer());
            foreach (var (portrait, tooltip, imported) in pageChoices.Skip(rowStart).Take(count))
            {
                var image = LoadPortraitThumbnail(portrait);
                var cell = PortraitSpacer();
                _portraitGrid.AddChild(cell);
                if (image is null || image.IsEmpty()) continue;
                var choice = new Button { Icon = ImageTexture.CreateFromImage(image),
                    ExpandIcon = false, IconAlignment = HorizontalAlignment.Center,
                    VerticalIconAlignment = VerticalAlignment.Center, TooltipText = tooltip,
                    TextureFilter = TextureFilterEnum.Linear };
                choice.AddThemeStyleboxOverride("normal", PortraitTileStyle(_portraitPath == portrait, false));
                choice.AddThemeStyleboxOverride("hover", PortraitTileStyle(_portraitPath == portrait, true));
                choice.AddThemeStyleboxOverride("pressed", PortraitTileStyle(true, true));
                choice.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
                choice.Pressed += () => SelectPortrait(portrait);
                cell.AddChild(choice);
                if (!imported) continue;
                var remove = new Button { Text = "×", TooltipText = "등록 이미지 제거" };
                var removeStyle = new StyleBoxFlat { BgColor = new Color("291b18"),
                    BorderColor = new Color("d3a85d") };
                removeStyle.SetBorderWidthAll(1);
                removeStyle.SetCornerRadiusAll(5);
                removeStyle.SetContentMarginAll(0);
                remove.AddThemeStyleboxOverride("normal", removeStyle);
                remove.AddThemeStyleboxOverride("hover", PortraitTileStyle(true, true));
                remove.AddThemeFontSizeOverride("font_size", 12);
                remove.SetAnchorsPreset(LayoutPreset.TopRight);
                remove.OffsetLeft = -24;
                remove.OffsetRight = -5;
                remove.OffsetTop = 5;
                remove.OffsetBottom = 24;
                remove.Pressed += () => ConfirmRemoveGalleryPortrait(portrait);
                cell.AddChild(remove);
            }
            for (var empty = leftGap + count; empty < columns; empty++) _portraitGrid.AddChild(PortraitSpacer());
        }
        _portraitPageLabel.Text = $"{_portraitPage + 1} / {pageCount} · {choices.Count}장";
        _portraitPrevious.Disabled = _portraitPage == 0;
        _portraitNext.Disabled = _portraitPage >= pageCount - 1;
    }

    private void SelectPortrait(string? path)
    {
        _pendingExternalPath = null;
        _portraitPath = path;
        ShowPortrait(path is null ? null : ProjectSettings.GlobalizePath(path));
        RebuildPortraitGrid(path);
    }

    private static StyleBoxFlat PortraitTileStyle(bool selected, bool hover)
    {
        var style = new StyleBoxFlat
        {
            BgColor = hover ? new Color("573322") : new Color("211713"),
            BorderColor = selected ? new Color("f3c96d") : new Color("846840"),
        };
        style.SetBorderWidthAll(selected ? 2 : 1);
        style.SetCornerRadiusAll(7);
        style.SetContentMarginAll(4);
        return style;
    }

    private static Control PortraitSpacer() => new()
    {
        CustomMinimumSize = new Vector2(96, 124),
        SizeFlagsHorizontal = SizeFlags.ExpandFill,
        MouseFilter = MouseFilterEnum.Ignore,
    };

    private static Image? LoadPortraitThumbnail(string portrait)
    {
        var image = Image.LoadFromFile(ProjectSettings.GlobalizePath(portrait));
        if (image is null || image.IsEmpty()) return null;
        const int targetWidth = 82;
        const int targetHeight = 98;
        var scale = Math.Min((float)targetWidth / image.GetWidth(), (float)targetHeight / image.GetHeight());
        var width = Math.Max(1, Mathf.RoundToInt(image.GetWidth() * scale));
        var height = Math.Max(1, Mathf.RoundToInt(image.GetHeight() * scale));
        image.Resize(width, height, Image.Interpolation.Lanczos);
        return image;
    }

    private void ConfirmRemoveGalleryPortrait(string portrait)
    {
        var layer = new CanvasLayer { Layer = 40 };
        AddChild(layer);
        void Close() => layer.QueueFree();
        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.55f),
            MouseFilter = MouseFilterEnum.Stop };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        backdrop.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Close();
        };
        layer.AddChild(backdrop);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layer.AddChild(center);
        var frame = new StyleBoxFlat { BgColor = new Color(0.10f, 0.055f, 0.05f, 0.97f),
            BorderColor = new Color(0.82f, 0.67f, 0.36f) };
        frame.SetBorderWidthAll(2);
        frame.SetCornerRadiusAll(10);
        frame.SetContentMarginAll(14);
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", frame);
        center.AddChild(panel);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(390, 0) };
        box.AddThemeConstantOverride("separation", 12);
        panel.AddChild(box);
        var title = new Label { Text = "등록 이미지 제거", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeColorOverride("font_color", new Color(0.82f, 0.67f, 0.36f));
        title.AddThemeFontSizeOverride("font_size", 17);
        box.AddChild(title);
        box.AddChild(new HSeparator());
        box.AddChild(new Label { Text = "선택한 외부 이미지를 목록에서 제거하시겠습니까?",
            HorizontalAlignment = HorizontalAlignment.Center });
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        actions.AddThemeConstantOverride("separation", 12);
        box.AddChild(actions);
        var confirm = new Button { Text = "제거", CustomMinimumSize = new Vector2(110, 34) };
        actions.AddChild(confirm);
        var cancel = new Button { Text = "취소", CustomMinimumSize = new Vector2(110, 34) };
        cancel.Pressed += Close;
        actions.AddChild(cancel);
        confirm.Pressed += () =>
        {
            if (_created.Any(record => record.PortraitPath == portrait))
            {
                _status.Text = "등록된 장수가 사용하는 사진은 제거할 수 없습니다.";
                Close();
                return;
            }
            var directory = Path.GetFullPath(ProjectSettings.GlobalizePath("user://general-presets/portraits/"));
            var file = Path.GetFullPath(ProjectSettings.GlobalizePath(portrait));
            if (!file.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return;
            if (File.Exists(file)) File.Delete(file);
            if (_portraitPath == portrait) SelectPortrait(null);
            RebuildPortraitGrid();
            _status.Text = "외부 이미지를 제거했습니다.";
            Close();
        };
    }

    private void ShowPortrait(string? globalPath)
    {
        _commonPortraitPreview.Texture = null;
        if (globalPath is null || !File.Exists(globalPath)) return;
        var image = Image.LoadFromFile(globalPath);
        if (image is not null && !image.IsEmpty())
            _commonPortraitPreview.Texture = ImageTexture.CreateFromImage(image);
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

    private async void OnGalleryPortraitsSelected(string[] paths)
    {
        if (paths.Length == 0) return;
        _galleryLoading.Visible = true;
        _galleryLoadingLabel.Text = $"외부 이미지 등록 중… 0 / {paths.Length}";
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var added = 0;
        string? firstRegistered = null;
        var errors = new List<string>();
        foreach (var path in paths)
        {
            try { var registered = RegisterGalleryPortrait(path); firstRegistered ??= registered; added++; }
            catch (Exception error) { errors.Add($"{Path.GetFileName(path)}: {error.Message}"); }
            _galleryLoadingLabel.Text = $"외부 이미지 등록 중… {added + errors.Count} / {paths.Length}";
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        RebuildPortraitGrid(firstRegistered);
        if (firstRegistered is not null) SelectPortrait(firstRegistered);
        _galleryLoading.Visible = false;
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
        _savedFormSnapshot = "";
        UpdateCommonPreview();
    }

    private void LoadSelected(int index)
    {
        if (index < 0 || index >= _created.Count) return;
        var entry = _created[index];
        var general = entry.General;
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
        _savedFormSnapshot = FormSnapshot();
        UpdateCommonPreview();
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
        var previous = _editingId is { } editing ? _created.FirstOrDefault(item => item.General.Id == editing) : null;
        return new CreatedGeneralRecord(record, _gender.Selected == 1 ? "female" : "male", (int)_points.Value,
            portrait, _requiredGeneral.GetItemMetadata(_requiredGeneral.Selected).AsInt32() is var required && required > 0
                ? required : null, previous?.FaceCenterX ?? 0.5, previous?.FaceCenterY ?? 0.35,
            previous?.FaceZoom ?? 1);
    }

    private string FormSnapshot()
    {
        try { return System.Text.Json.JsonSerializer.Serialize(FromForm(_editingId ?? 10000)); }
        catch { return CurrentName(); }
    }

    private void RequestReturnToStart()
    {
        if (FormSnapshot() == _savedFormSnapshot) { ScenarioStartMenu.ReturnToStart(GetTree()); return; }
        var dialog = new ConfirmationDialog { Title = "저장하지 않은 신규 장수", DialogText = "장수를 저장하고 시작 화면으로 돌아갈까요?", OkButtonText = "저장 후 나가기", CancelButtonText = "계속 편집" };
        dialog.AddButton("변경 버리기", true, "discard");
        AddChild(dialog);
        dialog.Confirmed += () => { SaveSelected(); if (FormSnapshot() == _savedFormSnapshot) ScenarioStartMenu.ReturnToStart(GetTree()); };
        dialog.CustomAction += action => { if (action == "discard") ScenarioStartMenu.ReturnToStart(GetTree()); };
        dialog.PopupCentered();
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
            _savedFormSnapshot = FormSnapshot();
            RefreshList();
            PopulateRequiredGenerals(entry.RequiredGeneralId);
            _status.Text = $"{entry.General.Name} 저장 완료 · 다음 새 게임부터 영입 후보로 등장합니다.";
            UpdateCommonPreview();
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

    private async void RunQa()
    {
        var firstSelected = _created.Count == 0
            || (_editingId == _created[0].General.Id && _list.IsSelected(0));
        NewGeneral();
        NativeTextInputDialog.ApplyResult(_name, "생성 장수 QA");
        NativeTextInputDialog.ApplyResult(_description, "첫째 줄\n둘째 줄");
        var nativeBindingPassed = _name.Text == "생성 장수 QA" && _commonName.Text == _name.Text
            && _description.Text == "첫째 줄\n둘째 줄";
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
        var tabLayoutPassed = _tabs.GetTabTitle(0) == "기본 정보" && _tabs.GetTabTitle(1) == "능력·적성"
            && _tabs.GetTabTitle(2) == "스킬" && _tabs.GetTabTitle(3) == "초상·얼굴";
        var workspaceLayoutPassed = FindChild("CreatorRosterColumn", true, false) is VBoxContainer rosterColumn
            && FindChild("CreatorPreviewColumn", true, false) is VBoxContainer previewColumn
            && FindChild("CreatorEditorColumn", true, false) is VBoxContainer editorColumn
            && FindChild("CreatorBasicGrid", true, false) is GridContainer { Columns: 2 } basicGrid
            && basicGrid.GetChildCount() == 6
            && rosterColumn.CustomMinimumSize.X == 300 && previewColumn.CustomMinimumSize.X == 380
            && editorColumn.SizeFlagsHorizontal.HasFlag(SizeFlags.ExpandFill);
        var tabContentPassed = FindChild("CreatorAbilityGrid", true, false) is GridContainer { Columns: 3 }
            && FindChild("CreatorAptitudeGrid", true, false) is GridContainer { Columns: 4 }
            && FindChild("CreatorBattleSkillGrid", true, false) is GridContainer { Columns: 3 }
            && FindChild("CreatorAdminSkillGrid", true, false) is GridContainer { Columns: 3 }
            && FindChild("CreatorPortraitGrid", true, false) is GridContainer { Columns: 4 }
            && FindChild("CreatorSkillDetailScroll", true, false) is ScrollContainer;
        var tabScrollPassed = true;
        for (var tab = 0; tab < _tabs.GetTabCount(); tab++)
        {
            _tabs.CurrentTab = tab;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_tabs.GetChild(tab) is not ScrollContainer tabScroll) { tabScrollPassed = false; continue; }
            tabScrollPassed &= !tabScroll.GetHScrollBar().Visible;
            if (tab < 3) tabScrollPassed &= !tabScroll.GetVScrollBar().Visible;
        }
        _tabs.CurrentTab = 0;
        var longPopup = _battlePassives[0].GetPopup();
        longPopup.PopupCentered(new Vector2I(520, 300));
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        GeneralWorkspaceStyle.StylePopupScrollBars(longPopup);
        var popupBar = longPopup.FindChildren("*", "VScrollBar", true, false).OfType<VScrollBar>().FirstOrDefault();
        var popupScrollPassed = popupBar is { Visible: true }
            && popupBar.Size.X >= 7.5f
            && popupBar.MaxValue - popupBar.Page > popupBar.MinValue;
        if (popupBar is not null)
        {
            popupBar.Value = popupBar.MaxValue - popupBar.Page;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            popupScrollPassed &= popupBar.Value > popupBar.MinValue;
        }
        longPopup.Hide();
        var lastPassiveIndex = _battlePassives[0].ItemCount - 1;
        _battlePassives[0].Select(lastPassiveIndex);
        var lastPassiveCode = SelectedCode(_battlePassives[0]);
        var lastPassiveSaved = FromForm(10004).General.BattlePassives.FirstOrDefault()?.Code == lastPassiveCode;
        _battlePassives[0].Select(0);
        var passed = firstSelected && nativeBindingPassed
            && GeneralWorkspaceStyle.EditorFooterLayoutValid(this, _status)
            && applied.Generals.Count == scenario.Generals.Count + 2
            && tabLayoutPassed && workspaceLayoutPassed && tabContentPassed && tabScrollPassed
            && popupScrollPassed && lastPassiveSaved
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
        _battlePassives[0].Select(1);
        _adminPassives[0].Select(1);
        ShowSkillDescription(_activeDescription, _battlePassives[0], _battleDetails);
        var battleDetail = _activeDescription.Text;
        ShowSkillDescription(_activeDescription, _adminPassives[0], _adminDetails);
        var adminDetail = _activeDescription.Text;
        passed &= !battleDetail.Contains("자동 적용됩니다", StringComparison.Ordinal)
            && !adminDetail.Contains("직접 사용하는 액티브가 아닙니다", StringComparison.Ordinal)
            && battleDetail.Length > 10 && adminDetail.Length > 10;
        var imported = Path.Combine(Path.GetTempPath(), $"general-portrait-qa-{Guid.NewGuid():N}.png");
        try
        {
            ImportExternalPortrait(ProjectSettings.GlobalizePath(DefaultPortrait()), imported);
            passed &= File.Exists(imported) && new FileInfo(imported).Length > 0;
            var qaHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(imported))).ToLowerInvariant();
            var qaGalleryFile = ProjectSettings.GlobalizePath($"user://general-presets/portraits/custom_{qaHash}.png");
            var existedBeforeQa = File.Exists(qaGalleryFile);
            RegisterGalleryPortrait(imported);
            RegisterGalleryPortrait(imported);
            var registered = RegisterGalleryPortrait(imported);
            var registeredFile = ProjectSettings.GlobalizePath(registered);
            RebuildPortraitGrid();
            SelectPortrait(registered);
            var portraitButtons = _portraitGrid.GetChildren().OfType<Control>()
                .SelectMany(cell => cell.GetChildren().OfType<Button>())
                .Where(button => button.Icon is not null).ToArray();
            var portraitsCentered = portraitButtons.Length > 0
                && portraitButtons.All(button => button.IconAlignment == HorizontalAlignment.Center
                    && button.VerticalIconAlignment == VerticalAlignment.Center);
            ConfirmRemoveGalleryPortrait(registered);
            var removalLayer = GetChildren().OfType<CanvasLayer>().LastOrDefault();
            var styledRemovalConfirm = removalLayer is not null
                && removalLayer.FindChildren("*", "PanelContainer", true, false).OfType<PanelContainer>().Any()
                && removalLayer.FindChildren("*", "Button", true, false).OfType<Button>()
                    .Count(button => button.Text is "제거" or "취소") == 2;
            removalLayer?.QueueFree();
            passed &= portraitsCentered && _portraitPath == registered && _portraitGrid.GetChildCount() >= 1
                && _portraitGrid.Columns == 4 && PortraitPageCount(25, 12) == 3
                && styledRemovalConfirm
                && _galleryFileDialog.FileMode == FileDialog.FileModeEnum.OpenFiles
                && _galleryLoadingLabel is not null
                && CreatedGeneralStore.Load(CreatedGeneralStore.Save([FromForm(10002)]))[0].PortraitPath == registered;
            if (!existedBeforeQa) File.Delete(registeredFile);
        }
        finally { if (File.Exists(imported)) File.Delete(imported); }
        var framingQaId = Math.Max(10002, _created.Select(entry => entry.General.Id).DefaultIfEmpty().Max() + 1);
        _created.Add(sample with { General = sample.General with { Id = framingQaId }, FaceCenterX = 0.72, FaceCenterY = 0.24, FaceZoom = 2.1 });
        LoadSelected(_created.Count - 1);
        var retained = FromForm(framingQaId);
        passed &= retained.FaceCenterX == 0.72 && retained.FaceCenterY == 0.24 && retained.FaceZoom == 2.1;
        _created.RemoveAt(_created.Count - 1);
        GD.Print($"[generalcreatorqa] passed={passed} tabs={tabLayoutPassed} workspace={workspaceLayoutPassed} content={tabContentPassed} scroll={tabScrollPassed} popupScroll={popupScrollPassed} popupWidth={popupBar?.Size.X ?? 0} lastPassive={lastPassiveSaved} native={nativeBindingPassed} first={firstSelected} original={scenario.Generals.Count} new={applied.Generals.Count}");
        GetTree().Quit(passed ? 0 : 1);
    }
}
