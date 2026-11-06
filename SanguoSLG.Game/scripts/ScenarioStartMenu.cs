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
        var newGameScreen = BuildNewGameScreen();
        var menuCenter = new CenterContainer { Name = "TitleMenuCenter" };
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
        var gameTitle = new TextureRect
        {
            Name = "TitleGameLogo",
            Texture = new AtlasTexture { Atlas = logoImage, Region = new Rect2(145, 0, 1160, 375) },
            CustomMinimumSize = new Vector2(0, 162),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        menu.AddChild(gameTitle);
        var menuSubtitle = new Label
        {
            Text = "영웅의 시대   ·   천하의 운명",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        menuSubtitle.AddThemeColorOverride("font_color", new Color("d9b77a"));
        menuSubtitle.AddThemeFontOverride("font", new FontVariation
        {
            BaseFont = GD.Load<FontFile>("res://assets/fonts/NotoSerifKR-variable.ttf"),
            VariationOpentype = new Godot.Collections.Dictionary { ["wght"] = 500 }
        });
        menuSubtitle.AddThemeFontSizeOverride("font_size", 17);
        menu.AddChild(menuSubtitle);
        var titleRule = new CenterContainer { Name = "TitleInnerDivider", CustomMinimumSize = new Vector2(0, 24) };
        titleRule.AddChild(new ColorRect
        {
            Color = new Color(0.80f, 0.65f, 0.40f, 0.55f),
            CustomMinimumSize = new Vector2(240, 1),
            MouseFilter = MouseFilterEnum.Ignore
        });
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
        AddAction(mainActions, "새 게임 시작하기", "swords", () =>
        {
            LoadNewGameScenarios();
            menuCenter.Visible = false;
            newGameScreen.Visible = true;
        }, true);
        AddAction(mainActions, "이어하기 · 저장된 게임", "flag", () => OpenLoadSlots(menuCenter), true);
        var editorHeader = new Label { Text = "역사와 장수 편집", HorizontalAlignment = HorizontalAlignment.Center };
        editorHeader.AddThemeColorOverride("font_color", new Color("c8ad7d"));
        editorHeader.AddThemeFontOverride("font", new FontVariation
        {
            BaseFont = GD.Load<FontFile>("res://assets/fonts/NotoSerifKR-variable.ttf"),
            VariationOpentype = new Godot.Collections.Dictionary { ["wght"] = 500 }
        });
        editorHeader.AddThemeFontSizeOverride("font_size", 16);
        editorHeader.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.85f));
        editorHeader.AddThemeConstantOverride("shadow_offset_y", 2);
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
        ShowOptionalIntro(menuCenter);
        if (OS.GetCmdlineUserArgs().Contains("--startmenuqa")) CallDeferred(nameof(RunQa));
        if (OS.GetCmdlineUserArgs().Contains("--menulaunchqa"))
            Callable.From(() =>
            {
                SelectFirstFactionForQa();
                _newGameStartButton?.EmitSignal(BaseButton.SignalName.Pressed);
                var confirm = _newGameConfirmationLayer?
                    .FindChild("NewGameConfirmAccept", true, false) as Button;
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

    private async void RunQa()
    {
        if (FindChild("NewGameScreen", true, false) is Control newGameScreen)
            newGameScreen.Visible = true;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var initiallySelected = _newGameSelectedFactionId > 0
            && _newGameStartButton is { Disabled: false }
            && _newGameFactionMap?.SelectedFactionId == _newGameSelectedFactionId;
        SelectFirstFactionForQa();
        var selectedFromMap = _newGameSelectedFactionId > 0
            && _newGameStartButton is { Disabled: false }
            && _newGameFactionMap?.SelectedFactionId == _newGameSelectedFactionId;
        var scenarioReset = true;
        if (_newGameScenarios.Count > 1)
        {
            SelectNewGameScenario(1);
            scenarioReset = _newGameSelectedFactionId > 0
                && _newGameStartButton is { Disabled: false }
                && _newGameFactionMap?.SelectedFactionId == _newGameSelectedFactionId;
            SelectFirstFactionForQa();
        }
        SetNewGameDifficulty(SanguoSLG.Core.Simulation.GameDifficulty.Hard);
        var hardDifficulty = _newGameDifficulty == SanguoSLG.Core.Simulation.GameDifficulty.Hard
            && _hardDifficultyButton?.GetMeta("difficulty_selected").AsBool() == true
            && _normalDifficultyButton?.GetMeta("difficulty_selected").AsBool() == false;
        SetNewGameDifficulty(SanguoSLG.Core.Simulation.GameDifficulty.Normal);
        var originalFactionId = _newGameSelectedFactionId;
        var originalScenarioIndex = _newGameSelectedScenarioIndex;
        var confirmationCalls = 0;
        ShowStartConfirmation(() => confirmationCalls++);
        var confirmLayer = _newGameConfirmationLayer;
        var selectedFaction = _newGameScenarios[originalScenarioIndex].Draft.Factions
            .First(faction => faction.Id == originalFactionId);
        var expectedPortraitPath = $"res://assets/portraits/{selectedFaction.RulerGeneralId}.png";
        var hasOfficerConfirm = confirmLayer is not null
            && confirmLayer.FindChild("NewGameConfirmationPanel", true, false) is PanelContainer
            && confirmLayer.FindChild("NewGameConfirmPortrait", true, false) is TextureRect portrait
            && portrait.Texture?.ResourcePath == expectedPortraitPath
            && confirmLayer.FindChildren("*", "Label", true, false).OfType<Label>()
                .Any(label => label.Text.Contains(selectedFaction.Name, StringComparison.Ordinal))
            && confirmLayer.FindChildren("*", "Label", true, false).OfType<Label>()
                .Any(label => label.Text.Contains("시나리오", StringComparison.Ordinal))
            && confirmLayer.FindChild("NewGameConfirmAccept", true, false) is Button;
        var cancelButton = confirmLayer?.FindChild("NewGameConfirmCancel", true, false) as Button;
        var cancelFocusUsesGoldArt = cancelButton is { FocusMode: FocusModeEnum.All }
            && cancelButton.GetThemeStylebox("focus") is StyleBoxEmpty
            && cancelButton.GetThemeStylebox("normal") is StyleBoxTexture
            && cancelButton.GetThemeStylebox("hover") is StyleBoxTexture
            && cancelButton.GetThemeStylebox("pressed") is StyleBoxTexture
            && cancelButton.CustomMinimumSize == new Vector2(170, 52)
            && confirmLayer?.FindChild("NewGameConfirmAccept", true, false) is Button acceptButton
            && acceptButton.CustomMinimumSize == cancelButton.CustomMinimumSize;
        ShowStartConfirmation(() => confirmationCalls++);
        var noDuplicateConfirmation = ReferenceEquals(confirmLayer, _newGameConfirmationLayer);
        _Input(new InputEventKey { Keycode = Key.Escape, Pressed = true });
        var escapeKeepsSelection = _newGameConfirmationLayer is null
            && _newGameSelectedFactionId == originalFactionId
            && _newGameSelectedScenarioIndex == originalScenarioIndex
            && confirmationCalls == 0;
        var alternateFaction = _newGameScenarios[originalScenarioIndex].Draft.Factions
            .FirstOrDefault(faction => faction.Playable && !faction.Hidden
                && faction.Id != originalFactionId);
        var selectionRefreshesConfirmation = false;
        if (alternateFaction is not null)
        {
            SelectNewGameFaction(alternateFaction.Id);
            ShowStartConfirmation(() => confirmationCalls++);
            selectionRefreshesConfirmation = _newGameConfirmationLayer?
                .FindChildren("*", "Label", true, false).OfType<Label>()
                .Any(label => label.Text.Contains(alternateFaction.Name, StringComparison.Ordinal)) == true;
            (_newGameConfirmationLayer?.FindChild("NewGameConfirmCancel", true, false) as Button)?
                .EmitSignal(BaseButton.SignalName.Pressed);
        }
        if (_newGameScenarios.Count > 1)
        {
            var alternateScenarioIndex = originalScenarioIndex == 0 ? 1 : 0;
            SelectNewGameScenario(alternateScenarioIndex);
            ShowStartConfirmation(() => confirmationCalls++);
            var alternateTitle = ScenarioDisplayTitle(_newGameScenarios[alternateScenarioIndex].Draft);
            selectionRefreshesConfirmation &= _newGameConfirmationLayer?
                .FindChildren("*", "Label", true, false).OfType<Label>()
                .Any(label => label.Text.Contains(alternateTitle, StringComparison.Ordinal)) == true;
            (_newGameConfirmationLayer?.FindChild("NewGameConfirmCancel", true, false) as Button)?
                .EmitSignal(BaseButton.SignalName.Pressed);
            SelectNewGameScenario(originalScenarioIndex);
            SelectNewGameFaction(originalFactionId);
        }
        SetNewGameDifficulty(SanguoSLG.Core.Simulation.GameDifficulty.Hard);
        ShowStartConfirmation(() => confirmationCalls++);
        var updatedDifficulty = _newGameConfirmationLayer?.FindChildren("*", "Label", true, false)
            .OfType<Label>().Any(label => label.Text == "어려움") == true;
        var accept = _newGameConfirmationLayer?.FindChild("NewGameConfirmAccept", true, false) as Button;
        accept?.EmitSignal(BaseButton.SignalName.Pressed);
        accept?.EmitSignal(BaseButton.SignalName.Pressed);
        var confirmedOnce = confirmationCalls == 1 && _newGameConfirmationLayer is null;
        SetNewGameDifficulty(SanguoSLG.Core.Simulation.GameDifficulty.Normal);
        var titleButtons = FindChildren("*", "", true, false).OfType<TitleMenuButton>().ToArray();
        var titleArt = FindChild("TitleIllustration", true, false) as TextureRect;
        var titlePanel = FindChild("TitleMenuPanel", true, false) as TitleOrnatePanel;
        var panelPatch = FindChild("TitlePanelNinePatch", true, false) as NinePatchRect;
        var tipFrame = FindChild("TitleTipFrame", true, false) as PanelContainer;
        var tipInset = titlePanel is not null && tipFrame is not null
            && tipFrame.GetGlobalRect().Position.X > titlePanel.GetGlobalRect().Position.X + 20 * titlePanel.Scale.X
            && tipFrame.GetGlobalRect().End.X < titlePanel.GetGlobalRect().End.X - 20 * titlePanel.Scale.X
            && tipFrame.GetGlobalRect().End.Y < titlePanel.GetGlobalRect().End.Y - 20 * titlePanel.Scale.Y
            && tipFrame.Size.Y is >= 32 and <= 40;
        var titlePresentation = titleArt?.Texture is ImageTexture
            && titleArt.StretchMode == TextureRect.StretchModeEnum.KeepAspectCovered
            && FindChild("TitleAtmosphere", true, false) is ColorRect
            && titlePanel is not null && panelPatch is
                { PatchMarginLeft: 95, PatchMarginRight: 95, PatchMarginTop: 95, PatchMarginBottom: 95 }
            && tipInset
            && FindChild("TitleGameLogo", true, false) is TextureRect
                { Texture: AtlasTexture { Atlas: not null }, StretchMode: TextureRect.StretchModeEnum.KeepAspectCentered }
            && FindChild("TitleEmbers", true, false) is CpuParticles2D { Emitting: true }
            && FindChild("TitleGameTip", true, false) is Label tip && tip.Text.Length > 0;
        var newGameLayout = FindChild("NewGameColumns", true, false) is HBoxContainer columns
            && columns.GetChildCount() == 3
            && FindChild("ScenarioSelectionColumn", true, false) is VBoxContainer
            && FindChild("ScenarioMapColumn", true, false) is VBoxContainer
            && FindChild("FactionSelectionColumn", true, false) is VBoxContainer
            && FindChild("NewGameFooter", true, false) is HBoxContainer;
        var noFactionButtonList = !FindChildren("*", "Button", true, false).OfType<Button>()
            .Any(button => button.HasMeta("faction_id"));
        var scenarioThumbnails = FindChildren("ScenarioThumbnail", "TextureRect", true, false)
            .OfType<TextureRect>().ToArray();
        var scenarioCardsReady = scenarioThumbnails.Length == _newGameScenarios.Count
            && scenarioThumbnails.All(thumbnail => thumbnail is
                { Texture: not null, StretchMode: TextureRect.StretchModeEnum.KeepAspectCovered });
        var scenarioSelection = _newGameScenarioCards.Count(card =>
            card.FindChild("SelectionDiamond", false, false) is Label { Visible: true }) == 1;
        var footerActions = FindChildren("NewGame*Button", "TextureButton", true, false)
            .OfType<TextureButton>().Where(button =>
                button.Name.ToString() is "NewGameBackButton" or "NewGameLaunchButton").ToArray();
        var styledFooter = footerActions.Length == 2 && footerActions.All(button => button is
            { TextureNormal: not null, TextureHover: not null, TexturePressed: not null, TextureDisabled: not null,
                StretchMode: TextureButton.StretchModeEnum.KeepAspectCentered, FocusMode: FocusModeEnum.None }
            && button.TextureHover == button.TextureNormal
            && button.TexturePressed == button.TextureNormal
            && button.TextureDisabled == button.TextureNormal
            && button.FindChild("Caption", false, false) is Label
                { HorizontalAlignment: HorizontalAlignment.Center }
            && Mathf.Abs(button.CustomMinimumSize.X / button.CustomMinimumSize.Y
                - button.TextureNormal.GetWidth() / (float)button.TextureNormal.GetHeight()) < 0.02f);
        var scenarioInfo = FindChild("SelectedScenarioTitle", true, false) is Label scenarioTitle
            && !scenarioTitle.Text.Contains("년", StringComparison.Ordinal)
            && FindChild("SelectedScenarioDate", true, false) is Label scenarioDate
            && scenarioDate.Text.Contains("년", StringComparison.Ordinal)
            && FindChild("SelectedScenarioDescription", true, false) is Label scenarioDescription
            && !string.Equals(scenarioDescription.Text, scenarioTitle.Text, StringComparison.Ordinal)
            && FindChild("SelectedScenarioSummary", true, false) is Label scenarioSummary
            && scenarioSummary.Text.Contains("주요 세력", StringComparison.Ordinal)
            && scenarioSummary.Text.Contains("총 거점", StringComparison.Ordinal);
        using var unmodifiedPortrait = new TextureRect
        {
            Texture = _newGameRulerPortrait?.Texture,
            Modulate = Colors.White,
            SelfModulate = Colors.White
        };
        var portraitAncestorsUntinted = true;
        for (var ancestor = _newGameRulerPortrait?.GetParent(); ancestor is not null;
             ancestor = ancestor.GetParent())
            if (ancestor is CanvasItem canvas
                && (canvas.Modulate != Colors.White || canvas.SelfModulate != Colors.White))
                portraitAncestorsUntinted = false;
        var portraitOriginalColor = _newGameRulerPortrait is
            { Texture: not null, Modulate: var portraitModulate, SelfModulate: var portraitSelfModulate,
                Material: ShaderMaterial { Shader: not null } portraitMaterial }
            && portraitModulate == unmodifiedPortrait.Modulate
            && portraitSelfModulate == unmodifiedPortrait.SelfModulate
            && portraitAncestorsUntinted
            && ReferenceEquals(_newGameRulerPortrait.Texture, unmodifiedPortrait.Texture)
            && portraitMaterial.Shader.Code.Contains("COLOR.a *=", StringComparison.Ordinal)
            && !portraitMaterial.Shader.Code.Contains("COLOR.rgb", StringComparison.Ordinal)
            && !portraitMaterial.Shader.Code.Contains("texture(TEXTURE", StringComparison.Ordinal);
        var factionDescriptionSwitch = false;
        if (_newGameSelectedScenarioIndex >= 0)
        {
            var scenarioIndex = _newGameSelectedScenarioIndex;
            var original = _newGameScenarios[scenarioIndex];
            var playableFactions = original.Draft.Factions.Where(faction => faction.Playable && !faction.Hidden)
                .Take(2).ToArray();
            if (playableFactions.Length == 2)
            {
                _newGameScenarios[scenarioIndex] = (original.Path, original.Draft with
                {
                    Factions = original.Draft.Factions.Select(faction => faction.Id == playableFactions[0].Id
                        ? faction with { Description = "첫 세력\n소개" }
                        : faction.Id == playableFactions[1].Id
                            ? faction with { Description = "둘째 세력 소개" } : faction).ToArray()
                });
                SelectNewGameFaction(playableFactions[0].Id);
                var firstDescriptionVisible = _newGameFactionDescription?.Text == "첫 세력\n소개";
                SelectNewGameFaction(playableFactions[1].Id);
                factionDescriptionSwitch = firstDescriptionVisible
                    && _newGameFactionDescription?.Text == "둘째 세력 소개";
                _newGameScenarios[scenarioIndex] = original;
                SelectNewGameScenario(scenarioIndex);
                SelectFirstFactionForQa();
            }
        }
        var readableMap = FindChild("ScenarioMapTooltip", true, false) is PanelContainer
            && _newGameFactionMap is { HasTerrain: true };
        var difficultyButtons = FindChildren("Difficulty*", "Button", true, false).OfType<Button>().ToArray();
        var viewport = GetViewport().GetVisibleRect().Size;
        var newGamePanel = FindChild("NewGamePanel", true, false) as PanelContainer;
        var responsiveBounds = newGamePanel is not null
            && newGamePanel.Size.X <= viewport.X && newGamePanel.Size.Y <= viewport.Y;
        var targetResolutions = new[] { new Vector2(1600, 900), new Vector2(1920, 1080) };
        var resolutionLayout = targetResolutions.All(resolution =>
        {
            var size = CalculateNewGamePanelSize(resolution);
            return size.X <= resolution.X && size.Y <= resolution.Y
                && size.X / size.Y is > 1.75f and < 1.9f;
        });
        var builtInScenarioIds = Directory.EnumerateFiles(
            ProjectSettings.GlobalizePath("res://assets/scenarios"), "*.json")
            .Select(SanguoSLG.Core.Data.ScenarioDraftStore.Load).Select(draft => draft.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var customSeen = false;
        var customScenariosAtBottom = true;
        foreach (var (_, draft) in _newGameScenarios)
        {
            if (!builtInScenarioIds.Contains(draft.Id)) customSeen = true;
            else if (customSeen) customScenariosAtBottom = false;
        }
        var passed = titlePresentation && hasOfficerConfirm && cancelFocusUsesGoldArt && noDuplicateConfirmation
            && escapeKeepsSelection && selectionRefreshesConfirmation
            && updatedDifficulty && confirmedOnce
            && initiallySelected && selectedFromMap
            && scenarioReset && hardDifficulty && responsiveBounds && resolutionLayout
            && newGameLayout && noFactionButtonList && scenarioCardsReady && scenarioSelection
            && styledFooter && scenarioInfo && portraitOriginalColor && factionDescriptionSwitch && readableMap && _newGameFactionMap is { HasSelectedSite: true }
            && _newGameScenarios.Count >= 6 && customScenariosAtBottom
            && _newGameFactionMap is { HasTerrain: true, CityCount: > 0, SelectableFactionCount: > 0 }
            && difficultyButtons.Length == 2
            && titleButtons.Count(button => button.FindChild("HitArea", false, false) is TextureButton
                    { TextureNormal: not null, TextureHover: not null, TexturePressed: not null,
                        StretchMode: TextureButton.StretchModeEnum.KeepAspectCentered }
                    && button.FindChild("Caption", true, false) is Label { Text.Length: > 0 } caption
                    && caption.GetThemeFont("font").HasChar('가')) == 7;
        GD.Print($"[scenario-start-qa] passed={passed} scenarios={_newGameScenarios.Count} thumbnails={scenarioCardsReady} cardMark={scenarioSelection} info={scenarioInfo} selectable={_newGameFactionMap?.SelectableFactionCount} selected={_newGameSelectedFactionId} site={_newGameFactionMap?.HasSelectedSite} reset={scenarioReset} hard={hardDifficulty} footer={styledFooter} viewport={viewport} panel={newGamePanel?.Size} resolutionLayout={resolutionLayout} columns={newGameLayout}");
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
