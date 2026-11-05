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

    private async void RunQa()
    {
        if (FindChild("NewGameScreen", true, false) is Control newGameScreen)
            newGameScreen.Visible = true;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var initiallyUnselected = _newGameSelectedFactionId == 0
            && _newGameStartButton is { Disabled: true };
        SelectFirstFactionForQa();
        var selectedFromMap = _newGameSelectedFactionId > 0
            && _newGameStartButton is { Disabled: false }
            && _newGameFactionMap?.SelectedFactionId == _newGameSelectedFactionId;
        var scenarioReset = true;
        if (_newGameScenarios.Count > 1)
        {
            SelectNewGameScenario(1);
            scenarioReset = _newGameSelectedFactionId == 0
                && _newGameStartButton is { Disabled: true }
                && _newGameFactionMap?.SelectedFactionId == 0;
            SelectFirstFactionForQa();
        }
        SetNewGameDifficulty(SanguoSLG.Core.Simulation.GameDifficulty.Hard);
        var hardDifficulty = _newGameDifficulty == SanguoSLG.Core.Simulation.GameDifficulty.Hard
            && _hardDifficultyButton is { ButtonPressed: true }
            && _normalDifficultyButton is { ButtonPressed: false };
        SetNewGameDifficulty(SanguoSLG.Core.Simulation.GameDifficulty.Normal);
        ShowStartConfirmation(() => { });
        var confirmLayer = GetChildren().OfType<CanvasLayer>().LastOrDefault();
        var hasOfficerConfirm = confirmLayer is not null
            && confirmLayer.FindChildren("*", "TextureRect", true, false).OfType<TextureRect>()
                .Any(portrait => portrait.Texture is not null)
            && confirmLayer.FindChildren("*", "Label", true, false).OfType<Label>()
                .Any(label => label.Text.Contains("초선:", StringComparison.Ordinal))
            && confirmLayer.FindChildren("*", "Button", true, false).OfType<Button>()
                .Count(button => button.Text is "확인" or "취소") == 2;
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
                StretchMode: TextureButton.StretchModeEnum.KeepAspectCentered }
            && button.TextureHover == button.TextureNormal
            && button.FindChild("Caption", false, false) is Label
                { HorizontalAlignment: HorizontalAlignment.Center }
            && Mathf.Abs(button.CustomMinimumSize.X / button.CustomMinimumSize.Y
                - button.TextureNormal.GetWidth() / (float)button.TextureNormal.GetHeight()) < 0.02f);
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
        var passed = titlePresentation && hasOfficerConfirm && initiallyUnselected && selectedFromMap
            && scenarioReset && hardDifficulty && responsiveBounds && resolutionLayout
            && newGameLayout && noFactionButtonList && scenarioCardsReady && scenarioSelection
            && styledFooter && readableMap && _newGameFactionMap is { HasSelectedSite: true }
            && _newGameScenarios.Count >= 6
            && _newGameFactionMap is { HasTerrain: true, CityCount: > 0, SelectableFactionCount: > 0 }
            && difficultyButtons.Length == 2
            && titleButtons.Count(button => button.FindChild("HitArea", false, false) is TextureButton
                    { TextureNormal: not null, TextureHover: not null, TexturePressed: not null,
                        StretchMode: TextureButton.StretchModeEnum.KeepAspectCentered }
                    && button.FindChild("Caption", true, false) is Label { Text.Length: > 0 } caption
                    && caption.GetThemeFont("font").HasChar('가')) == 7;
        GD.Print($"[scenario-start-qa] passed={passed} scenarios={_newGameScenarios.Count} thumbnails={scenarioCardsReady} cardMark={scenarioSelection} selectable={_newGameFactionMap?.SelectableFactionCount} selected={_newGameSelectedFactionId} site={_newGameFactionMap?.HasSelectedSite} reset={scenarioReset} hard={hardDifficulty} footer={styledFooter} viewport={viewport} panel={newGamePanel?.Size} resolutionLayout={resolutionLayout} columns={newGameLayout}");
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
