using Godot;

namespace SanguoSLG.Game;

public sealed partial class ScenarioStartMenu : Control
{
    private static bool _launchRequested;
    public static bool CampaignStartedFromMenu { get; private set; }
    public static string? SelectedPresetPath { get; private set; }
    public static string? SelectedScenarioPath { get; private set; }
    public static int SelectedPlayerFactionId { get; private set; } = 1;
    public static string? SelectedScenarioTitle { get; private set; }
    public static SanguoSLG.Core.Simulation.GameDifficulty SelectedDifficulty { get; private set; }
    public static bool LoadSlotsOnStart { get; private set; }
    public static bool FromStartMenu { get; private set; }

    public static bool ConsumeLaunchRequest()
    {
        if (!_launchRequested) return false;
        _launchRequested = false;
        return true;
    }

    public static void EnterEditor(SceneTree tree, string scene)
    {
        _launchRequested = false;
        LoadSlotsOnStart = false;
        SelectedPresetPath = null;
        SelectedScenarioPath = null;
        FromStartMenu = true;
        tree.AutoAcceptQuit = true;
        tree.ChangeSceneToFile(scene);
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        AddTitleBackdrop();
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(
            Mathf.Min(1339, GetViewport().GetVisibleRect().Size.X - 28), 0) };
        var style = new StyleBoxFlat { BgColor = new Color("1e2524"), BorderColor = new Color("d1ae65") };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(12);
        style.SetContentMarginAll(25);
        panel.AddThemeStyleboxOverride("panel", style);
        center.AddChild(panel);
        center.Visible = false;
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 9);
        panel.AddChild(content);
        var title = new Label { Text = "새로운 역사를 시작합니다", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 29);
        content.AddChild(title);
        var subtitle = new Label { Text = "시대를 고르고 세력을 선택하세요", HorizontalAlignment = HorizontalAlignment.Center };
        subtitle.AddThemeColorOverride("font_color", new Color("d4bb83"));
        content.AddChild(subtitle);
        content.AddChild(new HSeparator());
        var scenarioChoice = new OptionButton();
        scenarioChoice.Visible = false;
        var scenarioPaths = new List<string?>();
        var scenarioDir = ProjectSettings.GlobalizePath("user://scenarios");
        var builtInDir = ProjectSettings.GlobalizePath("res://assets/scenarios");
        var builtInPaths = Directory.Exists(builtInDir) ? Directory.EnumerateFiles(builtInDir, "*.json") : [];
        var userPaths = Directory.Exists(scenarioDir) ? Directory.EnumerateFiles(scenarioDir, "*.json") : [];
        foreach (var path in builtInPaths.Concat(userPaths)
            .GroupBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.FirstOrDefault(path => path.StartsWith(scenarioDir, StringComparison.OrdinalIgnoreCase)) ?? group.First())
            .OrderBy(path => Path.GetFileName(path).StartsWith("fictional-", StringComparison.Ordinal) ? 9999
                : SanguoSLG.Core.Data.ScenarioDraftStore.Load(path).StartYear).ThenBy(path => path))
                try
                {
                    var draft = SanguoSLG.Core.Data.ScenarioDraftStore.Load(path);
                    if (draft.Title.Trim() == "새 시나리오") continue;
                    scenarioChoice.AddItem(draft.Title);
                    scenarioPaths.Add(path);
                }
                catch (Exception failure) { GD.PushWarning("시나리오 목록: " + failure.Message); }
        content.AddChild(scenarioChoice);
        var scenarioLayout = new HBoxContainer();
        scenarioLayout.AddThemeConstantOverride("separation", 18);
        scenarioLayout.SizeFlagsVertical = SizeFlags.ExpandFill;
        content.AddChild(scenarioLayout);
        var leftColumn = new VBoxContainer { Name = "ScenarioSelectionColumn",
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1 };
        leftColumn.AddThemeConstantOverride("separation", 9);
        scenarioLayout.AddChild(leftColumn);
        var rightColumn = new VBoxContainer { Name = "FactionSelectionColumn",
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1 };
        rightColumn.AddThemeConstantOverride("separation", 9);
        scenarioLayout.AddChild(rightColumn);
        leftColumn.AddChild(new Label { Text = "시나리오 선택" });
        var scenarioScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 160),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        leftColumn.AddChild(scenarioScroll);
        var cards = new GridContainer { Columns = 2, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        cards.AddThemeConstantOverride("h_separation", 6);
        cards.AddThemeConstantOverride("v_separation", 6);
        scenarioScroll.AddChild(cards);
        var details = new PanelContainer { CustomMinimumSize = new Vector2(0, 310),
            SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var detailsStyle = new StyleBoxFlat { BgColor = new Color("29322e"), BorderColor = new Color("826c47") };
        detailsStyle.SetBorderWidthAll(1);
        detailsStyle.SetCornerRadiusAll(8);
        detailsStyle.SetContentMarginAll(14);
        details.AddThemeStyleboxOverride("panel", detailsStyle);
        leftColumn.AddChild(details);
        var mapColumn = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        mapColumn.AddThemeConstantOverride("separation", 6);
        details.AddChild(mapColumn);
        var mapTitle = new Label { Text = "세력 배치 지도  ·  거점을 눌러 세력을 선택하세요" };
        mapTitle.AddThemeColorOverride("font_color", new Color("e5c985"));
        mapColumn.AddChild(mapTitle);
        var factionMap = new ScenarioFactionMap { CustomMinimumSize = new Vector2(0, 225),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        mapColumn.AddChild(factionMap);
        var detailText = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        detailText.AddThemeFontSizeOverride("font_size", 13);
        mapColumn.AddChild(detailText);
        var cardButtons = new List<Button>();
        StyleBoxFlat CardStyle(bool selected, bool hover)
        {
            var styleBox = new StyleBoxFlat
            {
                BgColor = selected ? new Color("66402b") : hover ? new Color("47352a") : new Color("2b302c"),
                BorderColor = selected ? new Color("f0cc7d") : hover ? new Color("c5a469") : new Color("645c48")
            };
            styleBox.SetBorderWidthAll(selected ? 2 : 1);
            styleBox.SetCornerRadiusAll(7);
            styleBox.SetContentMarginAll(12);
            return styleBox;
        }
        var factionChoice = new OptionButton { Visible = false };
        var selectedDraft = default(SanguoSLG.Core.Data.ScenarioDraft);
        Dictionary<int, string>? rulerNames = null;
        var preview = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        var factionHeader = new Label { Text = "세력 선택  ·  지도 또는 세력 이름을 누르세요" };
        factionHeader.AddThemeColorOverride("font_color", new Color("e5c985"));
        var factionScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 200),
            SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var factionButtons = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        factionButtons.AddThemeConstantOverride("h_separation", 6);
        factionButtons.AddThemeConstantOverride("v_separation", 5);
        factionScroll.AddChild(factionButtons);
        var factionSummary = new PanelContainer { CustomMinimumSize = new Vector2(0, 88) };
        factionSummary.AddThemeStyleboxOverride("panel", detailsStyle);
        var summaryRow = new HBoxContainer();
        summaryRow.AddThemeConstantOverride("separation", 14);
        factionSummary.AddChild(summaryRow);
        var rulerPortrait = new TextureRect { CustomMinimumSize = new Vector2(58, 65),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered };
        summaryRow.AddChild(rulerPortrait);
        var summaryText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        summaryRow.AddChild(summaryText);
        var factionName = new Label();
        factionName.AddThemeFontSizeOverride("font_size", 20);
        summaryText.AddChild(factionName);
        var factionNumbers = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        summaryText.AddChild(factionNumbers);
        void SelectFaction(int id)
        {
            for (var item = 0; item < factionChoice.ItemCount; item++)
                if (factionChoice.GetItemId(item) == id) { factionChoice.Select(item); break; }
            factionMap.SelectFaction(id);
            try
            {
                var draft = selectedDraft ?? SanguoSLG.Core.Data.ScenarioDraftStore.Load(scenarioPaths[scenarioChoice.Selected]!);
                var faction = draft.Factions.First(entry => entry.Id == id);
                var cities = draft.Cities.Where(entry => entry.OwnerFactionId == id).ToArray();
                rulerNames ??= new SanguoSLG.Core.Data.ScenarioLoader().LoadFromDirectory(
                    Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data")))
                    .Generals.ToDictionary(entry => entry.Id.Value, entry => entry.Name);
                factionName.Text = $"{faction.Name}  ·  군주 {rulerNames.GetValueOrDefault(faction.RulerGeneralId, "미지정")}";
                factionNumbers.Text = $"점령 거점 {cities.Length:N0}    병력 {cities.Sum(city => city.Garrisons.Sum(garrison => garrison.Troops)):N0}    장수 {draft.Generals.Count(entry => entry.FactionId == id):N0}";
                var path = $"res://assets/portraits/{faction.RulerGeneralId}.png";
                rulerPortrait.Texture = Godot.FileAccess.FileExists(path) ? GD.Load<Texture2D>(path) : null;
            }
            catch (Exception failure) { factionNumbers.Text = "세력 정보 확인 불가: " + failure.Message; }
            foreach (var button in factionButtons.GetChildren().OfType<Button>()
                .Where(button => !button.IsQueuedForDeletion()))
            {
                button.ButtonPressed = (int)button.GetMeta("faction_id") == id;
                if (button.ButtonPressed)
                    Callable.From(() =>
                    {
                        if (IsInstanceValid(button) && !button.IsQueuedForDeletion())
                            factionScroll.EnsureControlVisible(button);
                    }).CallDeferred();
            }
        }
        void SelectScenario(int index)
        {
            scenarioChoice.Select(index);
            for (var i = 0; i < cardButtons.Count; i++)
            {
                cardButtons[i].AddThemeStyleboxOverride("normal", CardStyle(i == index, false));
                cardButtons[i].AddThemeColorOverride("font_color", i == index ? new Color("f9de9c") : new Color("e1dfd5"));
            }
            RefreshFactions();
            RefreshPreview();
        }
        for (var index = 0; index < scenarioPaths.Count; index++)
        {
            var cardIndex = index;
            var draft = SanguoSLG.Core.Data.ScenarioDraftStore.Load(scenarioPaths[index]!);
            var card = new Button { Text = draft.Title, CustomMinimumSize = new Vector2(195, 45),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                Alignment = HorizontalAlignment.Left, TooltipText = draft.Description };
            card.AddThemeStyleboxOverride("normal", CardStyle(false, false));
            card.AddThemeStyleboxOverride("hover", CardStyle(false, true));
            card.AddThemeStyleboxOverride("pressed", CardStyle(true, false));
            card.AddThemeFontSizeOverride("font_size", 16);
            card.Pressed += () => SelectScenario(cardIndex);
            cards.AddChild(card);
            cardButtons.Add(card);
        }
        if (scenarioChoice.ItemCount > 0) scenarioChoice.Select(0);
        rightColumn.AddChild(factionHeader);
        rightColumn.AddChild(factionChoice);
        rightColumn.AddChild(factionScroll);
        rightColumn.AddChild(factionSummary);
        factionMap.FactionPicked += id =>
        {
            SelectFaction(id);
        };
        void RefreshFactions()
        {
            factionChoice.Clear();
            foreach (var child in factionButtons.GetChildren()) child.QueueFree();
            try
            {
                var selectedPath = scenarioPaths[scenarioChoice.Selected];
                selectedDraft = SanguoSLG.Core.Data.ScenarioDraftStore.Load(selectedPath!);
                var factions = selectedDraft.Factions
                    .Where(f => f.Playable && !f.Hidden).ToArray();
                foreach (var faction in factions)
                {
                    factionChoice.AddItem(faction.Name, faction.Id);
                    var button = new Button { Text = $"●  {faction.Name}", ToggleMode = true,
                        CustomMinimumSize = new Vector2(145, 34), SizeFlagsHorizontal = SizeFlags.ExpandFill };
                    button.SetMeta("faction_id", faction.Id);
                    button.AddThemeColorOverride("font_color", new Color(faction.Color));
                    button.Pressed += () => SelectFaction(faction.Id);
                    factionButtons.AddChild(button);
                }
                factionChoice.Select(0);
                if (factions.Length > 0) SelectFaction(factions[0].Id);
            }
            catch (Exception failure) { GD.PushWarning("세력 목록: " + failure.Message); }
        }
        scenarioChoice.ItemSelected += _ => RefreshFactions();
        if (scenarioChoice.ItemCount > 0) RefreshFactions();
        leftColumn.AddChild(preview);
        void RefreshPreview()
        {
            try
            {
                var selectedPath = scenarioPaths[scenarioChoice.Selected];
                var chosen = selectedPath is null ? null
                    : SanguoSLG.Core.Data.ScenarioDraftStore.Load(selectedPath);
                var preset = chosen is null
                    ? ScenarioRootMapSource.Load()
                    : LoadScenarioMap(chosen, scenarioDir);
                if (chosen is not null) factionMap.SetScenario(chosen, preset, factionChoice.GetSelectedId());
                detailText.Text = chosen is null ? "" : chosen.Description[..Math.Min(chosen.Description.Length, 120)];
                preview.Text = $"지도 {preset.Width}×{preset.Height}  ·  성 {preset.Sites.Count(s => s.Kind == "city")}  ·  항구 {preset.Sites.Count(s => s.Kind == "port")}";
            }
            catch (Exception failure) { preview.Text = "지도 미리보기 오류: " + failure.Message; }
        }
        scenarioChoice.ItemSelected += _ => RefreshPreview();
        if (scenarioChoice.ItemCount > 0) SelectScenario(0);
        var difficulty = new OptionButton { CustomMinimumSize = new Vector2(0, 38) };
        difficulty.AddItem("난이도: 보통", 0);
        difficulty.AddItem("난이도: 어려움", 1);
        rightColumn.AddChild(difficulty);
        var error = new Label { Text = "", Modulate = new Color("f0a486") };
        rightColumn.AddChild(error);
        var start = new Button { Text = "게임 시작", CustomMinimumSize = new Vector2(0, 48) };
        start.Pressed += () =>
        {
            if (factionChoice.ItemCount == 0)
            {
                error.Text = "시작 가능한 세력이 없습니다.";
                return;
            }
            try
            {
                var data = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data"));
                var source = CreatedGeneralPresets.ApplyAtNewGame(
                    new SanguoSLG.Core.Data.ScenarioLoader().LoadFromDirectory(data));
                var archive = new SanguoSLG.Core.Data.ScenarioMapArchive(
                    ProjectSettings.GlobalizePath("user://maps"), scenarioDir);
                var selectedPath = scenarioPaths[scenarioChoice.Selected];
                var map = selectedPath is null ? ScenarioRootMapSource.OpenVersion(archive) : null;
                var draft = selectedPath is null
                    ? SanguoSLG.Core.Data.ScenarioDraftApplier.CreateSeed("default-190", "190년 기본", source, map!)
                    : SanguoSLG.Core.Data.ScenarioDraftStore.Load(selectedPath);
                if (map is null)
                {
                    if (SanguoSLG.Core.Data.ScenarioRootMapBinding.UsesRoot(draft.MapId))
                    {
                        map = ScenarioRootMapSource.OpenVersion(archive);
                        draft = SanguoSLG.Core.Data.ScenarioRootMapBinding.Bind(draft, map,
                            ScenarioRootMapSource.PreviousVersion(archive, draft));
                    }
                    else map = archive.Load(draft.MapId, draft.MapRevision);
                }
                SanguoSLG.Core.Data.ScenarioDraftApplier.Apply(source, draft, map.Preset);
            }
            catch (Exception failure)
            {
                error.Text = "시나리오 시작 불가: " + failure.Message;
                return;
            }
            ShowStartConfirmation(() =>
            {
                SelectedPresetPath = null;
                SelectedScenarioPath = scenarioPaths[scenarioChoice.Selected];
                SelectedPlayerFactionId = factionChoice.GetSelectedId();
                SelectedScenarioTitle = scenarioChoice.GetItemText(scenarioChoice.Selected);
                SelectedDifficulty = (SanguoSLG.Core.Simulation.GameDifficulty)difficulty.GetSelectedId();
                _launchRequested = true;
                CampaignStartedFromMenu = true;
                ChangeToCampaign();
            });
        };
        rightColumn.AddChild(start);
        var menuCenter = new CenterContainer();
        menuCenter.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        menuCenter.AnchorLeft = 0.52f;
        menuCenter.AnchorRight = 0.97f;
        AddChild(menuCenter);
        var menuPanel = new TitleOrnatePanel { Name = "TitleMenuPanel" };
        menuCenter.AddChild(menuPanel);
        void FitTitleMenu()
        {
            var viewport = GetViewport().GetVisibleRect().Size;
            var scale = Mathf.Min(viewport.X / 1600f, viewport.Y / 900f);
            menuPanel.Scale = Vector2.One * scale;
        }
        menuPanel.Resized += () => menuPanel.PivotOffset = menuPanel.Size / 2;
        GetViewport().SizeChanged += FitTitleMenu;
        TreeExiting += () => GetViewport().SizeChanged -= FitTitleMenu;
        FitTitleMenu();
        var menu = new VBoxContainer();
        menu.AddThemeConstantOverride("separation", 7);
        menuPanel.Content.AddChild(menu);
        var logoImage = GD.Load<Texture2D>("res://assets/ui/title/components/title_logo.png");
        var gameTitle = new TextureRect { Name = "TitleGameLogo",
            Texture = new AtlasTexture { Atlas = logoImage, Region = new Rect2(145, 0, 1160, 375) },
            CustomMinimumSize = new Vector2(0, 162),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore };
        menu.AddChild(gameTitle);
        var menuSubtitle = new Label { Text = "영웅의 시대   ·   천하의 운명", HorizontalAlignment = HorizontalAlignment.Center };
        menuSubtitle.AddThemeColorOverride("font_color", new Color("d9b77a"));
        menuSubtitle.AddThemeFontOverride("font", new FontVariation { BaseFont = GD.Load<FontFile>("res://assets/fonts/NotoSerifKR-variable.ttf"),
            VariationOpentype = new Godot.Collections.Dictionary { ["wght"] = 500 } });
        menuSubtitle.AddThemeFontSizeOverride("font_size", 17);
        menu.AddChild(menuSubtitle);
        var titleRule = new Label { Text = "──────────  ◈  ──────────", HorizontalAlignment = HorizontalAlignment.Center };
        titleRule.AddThemeColorOverride("font_color", new Color("cba969"));
        titleRule.AddThemeFontOverride("font", GD.Load<FontFile>("res://assets/fonts/NotoSerifKR-variable.ttf"));
        menu.AddChild(titleRule);
        var mainActions = new VBoxContainer { Name = "TitlePrimaryActions" };
        mainActions.AddThemeConstantOverride("separation", 4);
        menu.AddChild(mainActions);
        void AddAction(Container group, string label, string icon, Action pressed, bool primary = false, float width = 0)
        {
            var button = new TitleMenuButton(label, icon, primary, width);
            button.Pressed += pressed;
            group.AddChild(button);
        }
        AddAction(mainActions, "새 게임 시작하기", "swords", () => { menuCenter.Visible = false; center.Visible = true; }, true);
        AddAction(mainActions, "이어하기 · 저장된 게임", "flag", () => OpenLoadSlots(menuCenter), true);
        var editorHeader = new Label { Text = "역사와 장수 편집", HorizontalAlignment = HorizontalAlignment.Center };
        editorHeader.AddThemeColorOverride("font_color", new Color("c8ad7d"));
        editorHeader.AddThemeFontOverride("font", new FontVariation { BaseFont = GD.Load<FontFile>("res://assets/fonts/NotoSerifKR-variable.ttf"),
            VariationOpentype = new Godot.Collections.Dictionary { ["wght"] = 500 } });
        editorHeader.AddThemeFontSizeOverride("font_size", 16);
        menu.AddChild(editorHeader);
        var editorActions = new VBoxContainer { Name = "TitleEditorActions" };
        editorActions.AddThemeConstantOverride("separation", 2);
        menu.AddChild(editorActions);
        var editorRow = new HBoxContainer();
        editorRow.AddThemeConstantOverride("separation", 3);
        editorActions.AddChild(editorRow);
        AddAction(editorRow, "신규장수 등록", "helmet", () => EnterEditor(GetTree(), "res://GeneralCreator.tscn"));
        AddAction(editorRow, "장수 에디터", "scroll", () => EnterEditor(GetTree(), "res://GeneralEditor.tscn"));
        AddAction(editorRow, "지형 에디터", "mountain", () => EnterEditor(GetTree(), "res://EditMap.tscn"));
        var scenarioRow = new CenterContainer();
        editorActions.AddChild(scenarioRow);
        AddAction(scenarioRow, "시나리오 에디터", "book", () => EnterEditor(GetTree(), "res://ScenarioEditor.tscn"), width: 260);
        var quitActions = new CenterContainer();
        menu.AddChild(quitActions);
        AddAction(quitActions, "게임 종료", "power", () => GetTree().Quit(), width: 230);
        AddTitleTips(menu);
        AnimateTitleEntrance(menuPanel);
        var back = new Button { Text = "← 메인 메뉴", CustomMinimumSize = new Vector2(140, 36) };
        back.Pressed += () => { center.Visible = false; menuCenter.Visible = true; };
        rightColumn.AddChild(back);
        ShowOptionalIntro(menuCenter);
        if (OS.GetCmdlineUserArgs().Contains("--startmenuqa")) CallDeferred(nameof(RunQa));
        if (OS.GetCmdlineUserArgs().Contains("--menulaunchqa"))
            Callable.From(() =>
            {
                start.EmitSignal(BaseButton.SignalName.Pressed);
                var confirm = GetChildren().OfType<CanvasLayer>().LastOrDefault()
                    ?.FindChildren("*", "Button", true, false).OfType<Button>()
                    .FirstOrDefault(button => button.Text == "확인");
                confirm?.EmitSignal(BaseButton.SignalName.Pressed);
            }).CallDeferred();
        if (OS.GetCmdlineUserArgs().Contains("--menuloadqa"))
            Callable.From(() => RunMenuLoadQa(menuCenter)).CallDeferred();
    }

    private static SanguoSLG.Core.Data.ScenarioMapPreset LoadScenarioMap(
        SanguoSLG.Core.Data.ScenarioDraft draft, string scenarioDir)
    {
        if (SanguoSLG.Core.Data.ScenarioRootMapBinding.UsesRoot(draft.MapId))
            return ScenarioRootMapSource.Load();
        return new SanguoSLG.Core.Data.ScenarioMapArchive(
            ProjectSettings.GlobalizePath("user://maps"), scenarioDir)
            .Load(draft.MapId, draft.MapRevision).Preset;
    }

    private void ShowStartConfirmation(Action onConfirmed)
    {
        // 전장에서 쓰는 장수 컨펌창과 같은 금테·초상·대사·확인/취소 구성.
        var layer = new CanvasLayer { Layer = 40 };
        AddChild(layer);
        void Close() => layer.QueueFree();
        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = MouseFilterEnum.Stop };
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
        var content = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
        content.AddThemeConstantOverride("separation", 10);
        panel.AddChild(content);
        var title = new Label { Text = "새 게임 시작 확인", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeColorOverride("font_color", new Color(0.82f, 0.67f, 0.36f));
        title.AddThemeFontSizeOverride("font_size", 17);
        content.AddChild(title);
        content.AddChild(new HSeparator());
        var texture = GD.Load<Texture2D>("res://assets/portraits/113.png");
        if (texture is not null)
            content.AddChild(new TextureRect { Texture = texture, CustomMinimumSize = new Vector2(200, 270),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        var speech = new Label { Text = "초선: 이 세력과 함께 새로운 역사를 시작하시겠습니까?",
            HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.Off,
            CustomMinimumSize = new Vector2(500, 0) };
        speech.AddThemeColorOverride("font_color", new Color(0.98f, 0.85f, 0.52f));
        content.AddChild(speech);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 12);
        content.AddChild(buttons);
        var confirm = new Button { Text = "확인", CustomMinimumSize = new Vector2(110, 34) };
        confirm.Pressed += () => { Close(); onConfirmed(); };
        buttons.AddChild(confirm);
        var cancel = new Button { Text = "취소", CustomMinimumSize = new Vector2(110, 34) };
        cancel.Pressed += Close;
        buttons.AddChild(cancel);
    }

    private void RunQa()
    {
        ShowStartConfirmation(() => { });
        var confirmLayer = GetChildren().OfType<CanvasLayer>().LastOrDefault();
        var hasOfficerConfirm = confirmLayer is not null
            && confirmLayer.FindChildren("*", "TextureRect", true, false).OfType<TextureRect>()
                .Any(portrait => portrait.Texture is not null)
            && confirmLayer.FindChildren("*", "Label", true, false).OfType<Label>()
                .Any(label => label.Text.Contains("초선:", StringComparison.Ordinal))
            && confirmLayer.FindChildren("*", "Button", true, false).OfType<Button>()
                .Count(button => button.Text is "확인" or "취소") == 2;
        var buttons = FindChildren("*", "Button", true, false).OfType<Button>().Select(button => button.Text).ToArray();
        var scenarios = FindChildren("*", "OptionButton", true, false).OfType<OptionButton>().FirstOrDefault();
        var factionMap = FindChildren("*", "", true, false).OfType<ScenarioFactionMap>().FirstOrDefault();
        var scenarioColumn = FindChild("ScenarioSelectionColumn", true, false) as VBoxContainer;
        var factionColumn = FindChild("FactionSelectionColumn", true, false) as VBoxContainer;
        var factionControls = FindChildren("*", "Button", true, false).OfType<Button>()
            .Where(button => button.HasMeta("faction_id")).ToArray();
        var titleButtons = FindChildren("*", "", true, false).OfType<TitleMenuButton>().ToArray();
        var titleArt = FindChild("TitleIllustration", true, false) as TextureRect;
        var titlePresentation = titleArt?.Texture is ImageTexture
            && titleArt.StretchMode == TextureRect.StretchModeEnum.KeepAspectCovered
            && FindChild("TitleAtmosphere", true, false) is ColorRect
            && FindChild("TitleMenuPanel", true, false) is TitleOrnatePanel
            && FindChild("TitleGameLogo", true, false) is TextureRect
                { Texture: AtlasTexture { Atlas: not null }, StretchMode: TextureRect.StretchModeEnum.KeepAspectCentered }
            && FindChild("TitleEmbers", true, false) is CpuParticles2D { Emitting: true }
            && FindChild("TitleGameTip", true, false) is Label tip && tip.Text.Length > 0;
        var passed = titlePresentation && hasOfficerConfirm
            && buttons.Contains("게임 시작")
            && scenarios?.ItemCount >= 6
            && !Enumerable.Range(0, scenarios?.ItemCount ?? 0).Any(index => scenarios!.GetItemText(index) == "새 시나리오")
            && factionMap is { HasTerrain: true, CityCount: > 0 }
            && scenarioColumn is not null && factionColumn is not null
            && ReferenceEquals(scenarioColumn.GetParent(), factionColumn.GetParent())
            && ReferenceEquals(factionMap.GetParent()?.GetParent()?.GetParent(), scenarioColumn)
            && factionColumn.FindChildren("*", "Button", true, false).OfType<Button>()
                .Any(button => button.Text == "게임 시작")
            && factionControls.Length > 0 && factionControls.Any(button => button.ButtonPressed)
            && FindChildren("*", "Label", true, false).OfType<Label>()
                .Any(label => label.Text.Contains("점령 거점") && label.Text.Contains("병력") && label.Text.Contains("장수"))
            && titleButtons.Count(button => button.FindChild("HitArea", false, false) is TextureButton
                    { TextureNormal: not null, TextureHover: not null, TexturePressed: not null,
                        StretchMode: TextureButton.StretchModeEnum.KeepAspectCentered }
                    && button.FindChild("Caption", true, false) is Label { Text.Length: > 0 } caption
                    && caption.GetThemeFont("font").HasChar('가')) == 7
            && FindChild("TitlePrimaryActions", true, false) is VBoxContainer
            && FindChild("TitleEditorActions", true, false) is VBoxContainer;
        GD.Print($"[scenario-start-qa] passed={passed} buttons={buttons.Length} scenarios={scenarios?.ItemCount} viewport={GetViewport().GetVisibleRect().Size} panel={(FindChild("TitleMenuPanel", true, false) as Control)?.Size} logo={(FindChild("TitleGameLogo", true, false) as Control)?.Size} primary={titleButtons.FirstOrDefault()?.Size}");
        GetTree().Quit(passed ? 0 : 1);
    }

    public static void ReturnToStart(SceneTree tree)
    {
        CampaignMapScene.ClearPendingLoadedState();
        _launchRequested = false;
        CampaignStartedFromMenu = false;
        FromStartMenu = false;
        SelectedPresetPath = null;
        SelectedScenarioPath = null;
        LoadSlotsOnStart = false;
        SelectedPlayerFactionId = 1;
        SelectedDifficulty = SanguoSLG.Core.Simulation.GameDifficulty.Normal;
        tree.ChangeSceneToFile("res://Main3D.tscn");
    }
}
