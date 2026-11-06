using Godot;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Simulation;

namespace SanguoSLG.Game;

public sealed partial class ScenarioStartMenu
{
    private readonly List<(string Path, ScenarioDraft Draft)> _newGameScenarios = [];
    private readonly List<Button> _newGameScenarioCards = [];
    private TextureButton? _newGameStartButton;
    private ScenarioFactionMap? _newGameFactionMap;
    private Label? _newGameFactionName;
    private Label? _newGameRulerName;
    private Label? _newGameFactionSites;
    private Label? _newGameFactionTroops;
    private Label? _newGameFactionGenerals;
    private Label? _newGameFactionHint;
    private Label? _newGameFactionDescription;
    private TextureRect? _newGameRulerPortrait;
    private Label? _newGameError;
    private Button? _normalDifficultyButton;
    private Button? _hardDifficultyButton;
    private ImageTexture? _newGameFallbackThumbnail;
    private int _newGameSelectedScenarioIndex = -1;
    private int _newGameSelectedFactionId;
    private GameDifficulty _newGameDifficulty = GameDifficulty.Normal;
    private Dictionary<int, string>? _newGameRulerNames;
    private string _newGameScenarioDirectory = "";

    private static readonly Color NewGameGold = new("d7ad59");
    private static readonly Color NewGameGoldBright = new("f0cf7c");
    private static readonly Color NewGameIvory = new("eee3cc");
    private static readonly Color NewGameMuted = new("b6aa94");
    private static readonly Dictionary<(bool Selected, string State, int Width, int Height), Texture2D>
        DifficultyTextureCache = new();
    private static readonly HashSet<string> OfficialScenarioIds =
    [
        "historical-184", "historical-190", "historical-194",
        "historical-208", "historical-214", "fictional-revival"
    ];

    private static Font NewGameSerif(int weight) => new FontVariation
    {
        BaseFont = GD.Load<FontFile>("res://assets/fonts/NotoSerifKR-variable.ttf"),
        VariationOpentype = new Godot.Collections.Dictionary { ["wght"] = weight }
    };

    private static FontFile NewGameBodyFont()
        => GD.Load<FontFile>("res://assets/fonts/Pretendard-SemiBold.otf");

    private static StyleBoxFlat NewGameFrame(Color background, Color border, int width = 1,
        int radius = 7, float contentMargin = 12)
    {
        var style = new StyleBoxFlat { BgColor = background, BorderColor = border };
        style.SetBorderWidthAll(width);
        style.SetCornerRadiusAll(radius);
        style.SetContentMarginAll(contentMargin);
        return style;
    }

    private static StyleBoxFlat NewGameButtonStyle(Color background, Color border, int width = 1)
        => NewGameFrame(background, border, width, 5, 8);

    private static void NewGameStyleScrollBar(VScrollBar scroll)
    {
        scroll.CustomMinimumSize = new Vector2(6, 0);
        scroll.AddThemeStyleboxOverride("scroll", NewGameFrame(new Color("211712"),
            new Color("322518"), 1, 4, 0));
        scroll.AddThemeStyleboxOverride("grabber", NewGameFrame(new Color("9e793c"),
            new Color("d7ad59"), 1, 4, 0));
        scroll.AddThemeStyleboxOverride("grabber_highlight", NewGameFrame(new Color("bd954c"),
            new Color("f0cf7c"), 1, 4, 0));
        scroll.AddThemeStyleboxOverride("grabber_pressed", NewGameFrame(new Color("78582e"),
            new Color("d7ad59"), 1, 4, 0));
    }

    private static void NewGameRoundImage(TextureRect image, float radius)
    {
        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/ui/new_game/rounded_image.gdshader")
        };
        material.SetShaderParameter("corner_radius", radius);
        image.Material = material;
        image.Resized += () => material.SetShaderParameter("rect_size", image.Size);
    }

    private static void NewGameRulerImage(TextureRect image, float radius)
    {
        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://assets/ui/new_game/portrait_mask.gdshader")
        };
        material.SetShaderParameter("corner_radius", radius);
        image.Modulate = Colors.White;
        image.SelfModulate = Colors.White;
        image.Material = material;
        image.Resized += () => material.SetShaderParameter("rect_size", image.Size);
    }

    private static StyleBoxTexture NewGameDifficultyTexture(bool selected, string state, Vector2 size)
    {
        var width = Mathf.Max(160, Mathf.RoundToInt(size.X));
        var height = Mathf.Max(52, Mathf.RoundToInt(size.Y));
        var kind = selected ? "primary" : "secondary";
        var key = (selected, state, width, height);
        if (!DifficultyTextureCache.TryGetValue(key, out var texture))
        {
            var source = GD.Load<Texture2D>($"res://assets/ui/new_game/buttons/{kind}_normal.png");
            using var original = source.GetImage();
            var side = selected ? 108 : 91;
            var scaledSide = Mathf.RoundToInt(side * height / (float)original.GetHeight());
            var middleWidth = width - 2 * scaledSide;
            using var left = original.GetRegion(new Rect2I(0, 0, side, original.GetHeight()));
            using var middle = original.GetRegion(new Rect2I(side, 0,
                original.GetWidth() - 2 * side, original.GetHeight()));
            using var right = original.GetRegion(new Rect2I(original.GetWidth() - side, 0,
                side, original.GetHeight()));
            left.Resize(scaledSide, height, Image.Interpolation.Lanczos);
            middle.Resize(middleWidth, height, Image.Interpolation.Lanczos);
            right.Resize(scaledSide, height, Image.Interpolation.Lanczos);
            using var composed = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
            composed.BlitRect(left, new Rect2I(0, 0, scaledSide, height), Vector2I.Zero);
            composed.BlitRect(middle, new Rect2I(0, 0, middleWidth, height),
                new Vector2I(scaledSide, 0));
            composed.BlitRect(right, new Rect2I(0, 0, scaledSide, height),
                new Vector2I(width - scaledSide, 0));
            texture = ImageTexture.CreateFromImage(composed);
            DifficultyTextureCache[key] = texture;
        }
        var style = new StyleBoxTexture
        {
            Texture = texture,
            ModulateColor = state switch
            {
                "hover" => new Color(1.12f, 1.12f, 1.12f),
                "pressed" => new Color(0.85f, 0.85f, 0.85f),
                _ => Colors.White
            }
        };
        style.SetContentMarginAll(0);
        return style;
    }

    private CenterContainer BuildNewGameScreen()
    {
        var center = new CenterContainer { Name = "NewGameScreen", Visible = false };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer { Name = "NewGamePanel", MouseFilter = MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", NewGameFrame(
            new Color(0.10f, 0.038f, 0.035f, 0.982f), NewGameGold, 1, 9, 20));
        center.AddChild(panel);
        void FitPanel()
        {
            var viewport = GetViewport().GetVisibleRect().Size;
            panel.CustomMinimumSize = CalculateNewGamePanelSize(viewport);
        }
        GetViewport().SizeChanged += FitPanel;
        TreeExiting += () => GetViewport().SizeChanged -= FitPanel;
        FitPanel();

        var root = new VBoxContainer { Name = "NewGameRoot", SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddThemeConstantOverride("separation", 10);
        panel.AddChild(root);

        var title = new Label
        {
            Text = "새로운 역사를 시작하다",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontOverride("font", NewGameSerif(700));
        title.AddThemeFontSizeOverride("font_size", 29);
        title.AddThemeColorOverride("font_color", NewGameGoldBright);
        title.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        title.AddThemeConstantOverride("shadow_offset_y", 2);
        root.AddChild(title);
        var subtitle = new Label
        {
            Text = "시대를 선택하고 지도 위 거점에서 천하를 이끌 세력을 정하십시오.",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subtitle.AddThemeFontOverride("font", NewGameBodyFont());
        subtitle.AddThemeFontSizeOverride("font_size", 15);
        subtitle.AddThemeColorOverride("font_color", NewGameMuted);
        root.AddChild(subtitle);
        root.AddChild(NewGameRule());

        var columns = new HBoxContainer
        {
            Name = "NewGameColumns",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        columns.AddThemeConstantOverride("separation", 12);
        root.AddChild(columns);

        var left = NewGameColumn("ScenarioSelectionColumn", 0.82f);
        var middle = NewGameColumn("ScenarioMapColumn", 1.48f);
        var right = NewGameColumn("FactionSelectionColumn", 0.88f);
        columns.AddChild(left);
        columns.AddChild(middle);
        columns.AddChild(right);
        BuildScenarioColumn(left);
        BuildMapColumn(middle);
        BuildFactionColumn(right);
        BuildNewGameFooter(root, center);
        LoadNewGameScenarios();
        return center;
    }

    private static Vector2 CalculateNewGamePanelSize(Vector2 viewport)
        => new(Mathf.Min(1500, Mathf.Max(960, viewport.X - 32)),
            Mathf.Min(830, Mathf.Max(700, viewport.Y - 32)));

    private static VBoxContainer NewGameColumn(string name, float ratio)
    {
        var column = new VBoxContainer
        {
            Name = name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = ratio
        };
        column.AddThemeConstantOverride("separation", 8);
        return column;
    }

    private static Label NewGameSectionTitle(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontOverride("font", NewGameSerif(700));
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", NewGameGoldBright);
        return label;
    }

    private static Control NewGameRule()
    {
        var margin = new MarginContainer { CustomMinimumSize = new Vector2(0, 5) };
        margin.AddThemeConstantOverride("margin_top", 2);
        margin.AddThemeConstantOverride("margin_bottom", 2);
        var line = new ColorRect { Color = new Color(0.77f, 0.62f, 0.34f, 0.48f), CustomMinimumSize = new Vector2(0, 1) };
        margin.AddChild(line);
        return margin;
    }

    private void BuildScenarioColumn(VBoxContainer column)
    {
        column.AddChild(NewGameSectionTitle("시나리오 선택"));
        var frame = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        frame.AddThemeStyleboxOverride("panel", NewGameFrame(
            new Color(0.065f, 0.027f, 0.025f, 0.92f), new Color("846637"), 1, 6, 8));
        column.AddChild(frame);
        var scroll = new ScrollContainer
        {
            Name = "ScenarioCardScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        frame.AddChild(scroll);
        NewGameStyleScrollBar(scroll.GetVScrollBar());
        var cardInset = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        cardInset.AddThemeConstantOverride("margin_right", 11);
        scroll.AddChild(cardInset);
        var cards = new VBoxContainer { Name = "ScenarioCards", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        cards.AddThemeConstantOverride("separation", 7);
        cardInset.AddChild(cards);
    }

    private void BuildMapColumn(VBoxContainer column)
    {
        column.AddChild(NewGameSectionTitle("세력 배치 지도"));
        var mapFrame = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        mapFrame.AddThemeStyleboxOverride("panel", NewGameFrame(
            new Color(0.04f, 0.045f, 0.04f, 0.96f), NewGameGold, 1, 6, 8));
        column.AddChild(mapFrame);
        _newGameFactionMap = new ScenarioFactionMap
        {
            Name = "ScenarioFactionMap",
            CustomMinimumSize = new Vector2(0, 390),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _newGameFactionMap.FactionPicked += SelectNewGameFaction;
        mapFrame.AddChild(_newGameFactionMap);

        var infoFrame = new PanelContainer { CustomMinimumSize = new Vector2(0, 128) };
        infoFrame.AddThemeStyleboxOverride("panel", NewGameFrame(
            new Color(0.09f, 0.035f, 0.032f, 0.94f), new Color("846637"), 1, 5, 8));
        column.AddChild(infoFrame);
        var info = new VBoxContainer();
        info.AddThemeConstantOverride("separation", 3);
        infoFrame.AddChild(info);
        var heading = new HBoxContainer();
        heading.AddThemeConstantOverride("separation", 8);
        info.AddChild(heading);
        var scenarioTitle = new Label
        {
            Name = "SelectedScenarioTitle",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        scenarioTitle.AddThemeFontOverride("font", NewGameSerif(650));
        scenarioTitle.AddThemeFontSizeOverride("font_size", 21);
        scenarioTitle.AddThemeColorOverride("font_color", NewGameGoldBright);
        scenarioTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        scenarioTitle.MaxLinesVisible = 2;
        scenarioTitle.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        heading.AddChild(scenarioTitle);
        var scenarioDate = new Label
        {
            Name = "SelectedScenarioDate",
            HorizontalAlignment = HorizontalAlignment.Right
        };
        scenarioDate.AddThemeFontOverride("font", NewGameSerif(600));
        scenarioDate.AddThemeFontSizeOverride("font_size", 14);
        scenarioDate.AddThemeColorOverride("font_color", NewGameMuted);
        heading.AddChild(scenarioDate);
        info.AddChild(NewGameRule());
        var description = new Label
        {
            Name = "SelectedScenarioDescription",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MaxLinesVisible = 2,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        description.AddThemeFontOverride("font", NewGameBodyFont());
        description.AddThemeFontSizeOverride("font_size", 15);
        description.AddThemeColorOverride("font_color", NewGameIvory);
        info.AddChild(description);
        info.AddChild(NewGameRule());
        var summary = new Label { Name = "SelectedScenarioSummary", AutowrapMode = TextServer.AutowrapMode.Off };
        summary.AddThemeFontOverride("font", NewGameBodyFont());
        summary.AddThemeFontSizeOverride("font_size", 14);
        summary.AddThemeColorOverride("font_color", new Color("cdb578"));
        info.AddChild(summary);
    }

    private void BuildFactionColumn(VBoxContainer column)
    {
        column.AddChild(NewGameSectionTitle("군주와 세력"));
        var details = new PanelContainer
        {
            Name = "FactionDetails",
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 390)
        };
        details.AddThemeStyleboxOverride("panel", NewGameFrame(
            new Color(0.07f, 0.027f, 0.025f, 0.95f), new Color("846637"), 1, 6, 13));
        column.AddChild(details);
        var detailColumn = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Begin,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        detailColumn.AddThemeConstantOverride("separation", 12);
        details.AddChild(detailColumn);
        _newGameFactionHint = new Label
        {
            Text = "지도에서 플레이할 세력의\n거점을 선택하십시오.",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _newGameFactionHint.AddThemeFontOverride("font", NewGameSerif(500));
        _newGameFactionHint.AddThemeFontSizeOverride("font_size", 16);
        _newGameFactionHint.AddThemeColorOverride("font_color", NewGameMuted);
        detailColumn.AddChild(_newGameFactionHint);
        var identityRow = new HBoxContainer
        {
            Name = "FactionIdentityRow", Visible = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        identityRow.AddThemeConstantOverride("separation", 12);
        detailColumn.AddChild(identityRow);
        var portraitFrame = new PanelContainer { CustomMinimumSize = new Vector2(150, 210),
            Modulate = Colors.White, SelfModulate = Colors.White };
        portraitFrame.AddThemeStyleboxOverride("panel", NewGameFrame(
            new Color("332117"), new Color("b68a45"), 1, 8, 4));
        identityRow.AddChild(portraitFrame);
        _newGameRulerPortrait = new TextureRect
        {
            Name = "NewGameRulerPortrait",
            CustomMinimumSize = new Vector2(142, 202),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered
        };
        NewGameRulerImage(_newGameRulerPortrait, 8);
        portraitFrame.AddChild(_newGameRulerPortrait);
        var identityText = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        identityText.AddThemeConstantOverride("separation", 9);
        identityRow.AddChild(identityText);
        _newGameFactionName = new Label { HorizontalAlignment = HorizontalAlignment.Left, ClipText = true };
        _newGameFactionName.AddThemeFontOverride("font", NewGameSerif(700));
        _newGameFactionName.AddThemeFontSizeOverride("font_size", 21);
        _newGameFactionName.AddThemeColorOverride("font_color", NewGameGoldBright);
        identityText.AddChild(_newGameFactionName);
        _newGameRulerName = new Label { HorizontalAlignment = HorizontalAlignment.Left, ClipText = true };
        _newGameRulerName.AddThemeFontOverride("font", NewGameSerif(600));
        _newGameRulerName.AddThemeFontSizeOverride("font_size", 17);
        _newGameRulerName.AddThemeColorOverride("font_color", NewGameIvory);
        identityText.AddChild(_newGameRulerName);
        var statistics = new HBoxContainer
        {
            Name = "FactionStatistics", Visible = false,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        statistics.AddThemeConstantOverride("separation", 8);
        detailColumn.AddChild(statistics);
        _newGameFactionSites = NewGameFactionStat(statistics, "▥", "거점");
        _newGameFactionTroops = NewGameFactionStat(statistics, "▲", "병력");
        _newGameFactionGenerals = NewGameFactionStat(statistics, "♟", "장수");
        detailColumn.AddChild(NewGameRule());
        var descriptionScroll = new ScrollContainer
        {
            Name = "FactionDescriptionScroll", Visible = false,
            CustomMinimumSize = new Vector2(0, 86),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        detailColumn.AddChild(descriptionScroll);
        _newGameFactionDescription = new Label
        {
            Name = "FactionDescription",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _newGameFactionDescription.AddThemeFontOverride("font", NewGameBodyFont());
        _newGameFactionDescription.AddThemeFontSizeOverride("font_size", 14);
        _newGameFactionDescription.AddThemeColorOverride("font_color", NewGameIvory);
        descriptionScroll.AddChild(_newGameFactionDescription);

        column.AddChild(NewGameRule());
        column.AddChild(NewGameSectionTitle("게임 난이도"));
        var difficultyRow = new HBoxContainer { Name = "DifficultyButtons" };
        difficultyRow.AddThemeConstantOverride("separation", 8);
        column.AddChild(difficultyRow);
        _normalDifficultyButton = NewGameDifficultyButton("DifficultyNormal", "보통");
        _hardDifficultyButton = NewGameDifficultyButton("DifficultyHard", "어려움");
        _normalDifficultyButton.Pressed += () => SetNewGameDifficulty(GameDifficulty.Normal);
        _hardDifficultyButton.Pressed += () => SetNewGameDifficulty(GameDifficulty.Hard);
        difficultyRow.AddChild(_normalDifficultyButton);
        difficultyRow.AddChild(_hardDifficultyButton);
        SetNewGameDifficulty(GameDifficulty.Normal);
        var difficultyHelp = new Label
        {
            Text = "어려움에서는 다른 세력의 생산량이 증가합니다.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        difficultyHelp.AddThemeFontOverride("font", NewGameBodyFont());
        difficultyHelp.AddThemeFontSizeOverride("font_size", 12);
        difficultyHelp.AddThemeColorOverride("font_color", NewGameMuted);
        column.AddChild(difficultyHelp);
        _newGameError = new Label { Modulate = new Color("ef9b7e"), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _newGameError.AddThemeFontOverride("font", NewGameBodyFont());
        _newGameError.AddThemeFontSizeOverride("font_size", 12);
        column.AddChild(_newGameError);
    }

    private static Label NewGameFactionStat(Container parent, string icon, string title)
    {
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 1
        };
        row.AddThemeConstantOverride("separation", 5);
        parent.AddChild(row);
        var iconLabel = new Label { Text = icon, CustomMinimumSize = new Vector2(18, 0) };
        iconLabel.AddThemeFontOverride("font", NewGameBodyFont());
        iconLabel.AddThemeFontSizeOverride("font_size", 15);
        iconLabel.AddThemeColorOverride("font_color", NewGameGold);
        row.AddChild(iconLabel);
        var titleLabel = new Label { Text = title };
        titleLabel.AddThemeFontOverride("font", NewGameBodyFont());
        titleLabel.AddThemeFontSizeOverride("font_size", 14);
        titleLabel.AddThemeColorOverride("font_color", NewGameGold);
        row.AddChild(titleLabel);
        var value = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        value.AddThemeFontOverride("font", NewGameBodyFont());
        value.AddThemeFontSizeOverride("font_size", 14);
        value.AddThemeColorOverride("font_color", NewGameIvory);
        row.AddChild(value);
        return value;
    }

    private static Button NewGameDifficultyButton(string name, string text)
    {
        var button = new Button
        {
            Name = name,
            ToggleMode = false,
            CustomMinimumSize = new Vector2(0, 52),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        var caption = new Label
        {
            Name = "DifficultyCaption", Text = text, MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
        };
        caption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        caption.AddThemeFontOverride("font", NewGameSerif(600));
        caption.AddThemeFontSizeOverride("font_size", 16);
        button.AddChild(caption);
        button.SetMeta("difficulty_label", text);
        button.Resized += () => StyleDifficultyButton(button,
            button.GetMeta("difficulty_selected", false).AsBool());
        return button;
    }

    private void BuildNewGameFooter(VBoxContainer root, CenterContainer screen)
    {
        root.AddChild(NewGameRule());
        var footer = new HBoxContainer { Name = "NewGameFooter" };
        footer.AddThemeConstantOverride("separation", 12);
        root.AddChild(footer);
        var back = NewGameFooterAction("NewGameBackButton", "←  메인 메뉴로 돌아가기", false);
        back.Pressed += () =>
        {
            screen.Visible = false;
            if (FindChild("TitleMenuCenter", true, false) is Control titleMenu) titleMenu.Visible = true;
        };
        footer.AddChild(back);
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footer.AddChild(spacer);
        _newGameStartButton = NewGameFooterAction("NewGameLaunchButton", "천하에 출정하기  →", true);
        _newGameStartButton.Disabled = true;
        SetFooterActionEnabled(_newGameStartButton, false);
        _newGameStartButton.TooltipText = "지도에서 플레이할 세력을 선택하십시오.";
        _newGameStartButton.Pressed += StartSelectedNewGame;
        footer.AddChild(_newGameStartButton);
    }

    private static TextureButton NewGameFooterAction(string name, string text, bool primary)
    {
        var tier = primary ? "primary" : "secondary";
        var button = new TextureButton
        {
            Name = name,
            CustomMinimumSize = primary ? new Vector2(370, 58.14f) : new Vector2(286, 50.05f),
            TextureNormal = GD.Load<Texture2D>($"res://assets/ui/new_game/buttons/{tier}_normal.png"),
            TextureHover = GD.Load<Texture2D>($"res://assets/ui/new_game/buttons/{tier}_normal.png"),
            TexturePressed = GD.Load<Texture2D>($"res://assets/ui/new_game/buttons/{tier}_normal.png"),
            TextureDisabled = GD.Load<Texture2D>($"res://assets/ui/new_game/buttons/{tier}_normal.png"),
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
            FocusMode = FocusModeEnum.None,
            MouseDefaultCursorShape = CursorShape.PointingHand
        };
        var caption = new Label
        {
            Name = "Caption",
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        caption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        caption.AddThemeFontOverride("font", NewGameSerif(primary ? 700 : 550));
        caption.AddThemeFontSizeOverride("font_size", primary ? 18 : 15);
        caption.AddThemeColorOverride("font_color", NewGameGoldBright);
        button.AddChild(caption);
        var hovered = false;
        button.MouseEntered += () =>
        {
            hovered = true;
            button.SelfModulate = new Color(1.12f, 1.12f, 1.12f);
        };
        button.MouseExited += () =>
        {
            hovered = false;
            button.SelfModulate = Colors.White;
        };
        button.ButtonDown += () => button.SelfModulate = new Color(0.85f, 0.85f, 0.85f);
        button.ButtonUp += () => button.SelfModulate = hovered
            ? new Color(1.12f, 1.12f, 1.12f)
            : Colors.White;
        return button;
    }

    private static void SetFooterActionEnabled(TextureButton? button, bool enabled)
    {
        if (button is null) return;
        button.Disabled = !enabled;
        button.Modulate = enabled ? Colors.White : new Color(0.48f, 0.46f, 0.42f, 0.88f);
        button.MouseDefaultCursorShape = enabled ? CursorShape.PointingHand : CursorShape.Arrow;
    }

    private void LoadNewGameScenarios()
    {
        foreach (var card in _newGameScenarioCards)
        {
            card.GetParent()?.RemoveChild(card);
            card.QueueFree();
        }
        _newGameScenarioCards.Clear();
        _newGameScenarios.Clear();
        _newGameSelectedScenarioIndex = -1;
        _newGameSelectedFactionId = 0;
        _newGameScenarioDirectory = ProjectSettings.GlobalizePath("user://scenarios");
        var builtInDirectory = ProjectSettings.GlobalizePath("res://assets/scenarios");
        foreach (var source in ScenarioDraftCatalog.LoadEffective(builtInDirectory,
            _newGameScenarioDirectory))
        {
            try
            {
                _newGameScenarios.Add((source.Path, source.Draft));
            }
            catch (Exception failure)
            {
                GD.PushWarning("시나리오 목록: " + failure.Message);
            }
        }
        if (FindChild("ScenarioCards", true, false) is not VBoxContainer cards) return;
        foreach (var (entry, index) in _newGameScenarios.Select((entry, index) => (entry, index)))
        {
            var card = BuildScenarioCard(entry.Path, entry.Draft, index);
            cards.AddChild(card);
            _newGameScenarioCards.Add(card);
        }
        if (_newGameScenarios.Count > 0) SelectNewGameScenario(0);
        else if (_newGameError is not null) _newGameError.Text = "사용 가능한 시나리오가 없습니다.";
    }

    private Button BuildScenarioCard(string path, ScenarioDraft draft, int index)
    {
        var card = new Button
        {
            Name = $"ScenarioCard_{draft.Id}",
            CustomMinimumSize = new Vector2(0, 116),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Alignment = HorizontalAlignment.Left,
            TooltipText = draft.Title,
            ClipContents = true
        };
        var background = new ColorRect
        {
            Name = "ScenarioCardBackground", MouseFilter = MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/ui/new_game/scenario_card.gdshader") }
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        card.AddChild(background);
        card.Resized += () => ((ShaderMaterial)background.Material).SetShaderParameter("rect_size", card.Size);
        card.MouseEntered += () => ApplyScenarioCardStyle(card, index == _newGameSelectedScenarioIndex, true);
        card.MouseExited += () => ApplyScenarioCardStyle(card, index == _newGameSelectedScenarioIndex);
        ApplyScenarioCardStyle(card, false);
        card.Pressed += () => SelectNewGameScenario(index);
        var inset = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        inset.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        inset.AddThemeConstantOverride("margin_left", 9);
        inset.AddThemeConstantOverride("margin_top", 9);
        inset.AddThemeConstantOverride("margin_right", 9);
        inset.AddThemeConstantOverride("margin_bottom", 9);
        card.AddChild(inset);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 9);
        inset.AddChild(row);
        var thumbnail = new TextureRect
        {
            Name = "ScenarioThumbnail",
            Texture = ResolveScenarioThumbnail(path, draft),
            CustomMinimumSize = new Vector2(132, 74),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        NewGameRoundImage(thumbnail, 6);
        row.AddChild(thumbnail);
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        text.AddThemeConstantOverride("separation", 2);
        row.AddChild(text);
        var year = new Label { Text = $"{draft.StartYear}년", MouseFilter = MouseFilterEnum.Ignore };
        year.AddThemeFontOverride("font", NewGameSerif(700));
        year.AddThemeFontSizeOverride("font_size", 13);
        year.AddThemeColorOverride("font_color", NewGameGold);
        text.AddChild(year);
        var displayTitle = draft.Title.StartsWith($"{draft.StartYear}년,", StringComparison.Ordinal)
            ? draft.Title[($"{draft.StartYear}년,").Length..].Trim() : draft.Title;
        var title = new Label { Text = displayTitle,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MaxLinesVisible = 2,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = MouseFilterEnum.Ignore };
        title.AddThemeFontOverride("font", NewGameSerif(650));
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", NewGameIvory);
        text.AddChild(title);
        var rawSynopsis = ScenarioDescription(draft, displayTitle);
        var synopsis = rawSynopsis.Length > 24 ? rawSynopsis[..24] + "…" : rawSynopsis;
        var description = new Label
        {
            Text = synopsis,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = MouseFilterEnum.Ignore
        };
        description.AddThemeFontOverride("font", NewGameBodyFont());
        description.AddThemeFontSizeOverride("font_size", 12);
        description.AddThemeColorOverride("font_color", NewGameMuted);
        text.AddChild(description);
        var selectedMark = new Label
        {
            Name = "SelectionDiamond",
            Text = "◆",
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        selectedMark.SetAnchorsPreset(LayoutPreset.CenterRight);
        selectedMark.Position = new Vector2(-24, -10);
        selectedMark.Size = new Vector2(18, 20);
        selectedMark.AddThemeFontOverride("font", NewGameSerif(700));
        selectedMark.AddThemeFontSizeOverride("font_size", 15);
        selectedMark.AddThemeColorOverride("font_color", NewGameGoldBright);
        card.AddChild(selectedMark);
        return card;
    }

    private static void ApplyScenarioCardStyle(Button card, bool selected, bool hovered = false)
    {
        var empty = new StyleBoxEmpty();
        card.AddThemeStyleboxOverride("normal", empty);
        card.AddThemeStyleboxOverride("hover", empty);
        card.AddThemeStyleboxOverride("pressed", empty);
        card.AddThemeStyleboxOverride("focus", empty);
        if (card.FindChild("ScenarioCardBackground", false, false) is ColorRect
            { Material: ShaderMaterial material })
        {
            material.SetShaderParameter("upper_color", selected ? new Color("70231b")
                : hovered ? new Color("35221b") : new Color("211916"));
            material.SetShaderParameter("lower_color", selected ? new Color("370e0d")
                : hovered ? new Color("221914") : new Color("151211"));
            material.SetShaderParameter("border_color", selected ? new Color("f0c56b")
                : hovered ? new Color("a37d42") : new Color("625038"));
            material.SetShaderParameter("border_width", selected ? 2f : 1f);
        }
        if (card.FindChild("SelectionDiamond", false, false) is Label diamond) diamond.Visible = selected;
    }

    private Texture2D ResolveScenarioThumbnail(string scenarioPath, ScenarioDraft draft)
    {
        var isUserScenario = Path.GetFullPath(scenarioPath).StartsWith(
            Path.GetFullPath(_newGameScenarioDirectory), StringComparison.OrdinalIgnoreCase);
        if (!isUserScenario && OfficialScenarioIds.Contains(draft.Id))
        {
            var resourcePath = $"res://assets/ui/scenarios/scenario_{draft.Id}.png";
            if (Godot.FileAccess.FileExists(resourcePath)) return GD.Load<Texture2D>(resourcePath);
        }
        var directory = Path.GetDirectoryName(scenarioPath)!;
        if (!isUserScenario)
        {
            foreach (var candidate in new[]
            {
                Path.Combine(directory, $"scenario_{draft.Id}.png"),
                Path.ChangeExtension(scenarioPath, ".png")
            })
            {
                if (!File.Exists(candidate)) continue;
                using var image = Image.LoadFromFile(candidate);
                if (image is not null && !image.IsEmpty()) return ImageTexture.CreateFromImage(image);
            }
        }
        const string commonPath = "res://assets/ui/scenarios/custom_scenario.png";
        if (Godot.FileAccess.FileExists(commonPath)) return GD.Load<Texture2D>(commonPath);
        return _newGameFallbackThumbnail ??= CreateScenarioFallbackTexture();
    }

    private static ImageTexture CreateScenarioFallbackTexture()
    {
        using var image = Image.CreateEmpty(320, 180, false, Image.Format.Rgba8);
        for (var y = 0; y < image.GetHeight(); y++)
            for (var x = 0; x < image.GetWidth(); x++)
            {
                var vertical = y / 179f;
                var glow = Mathf.Max(0, 1f - new Vector2((x - 220) / 150f, (y - 82) / 95f).Length());
                var color = new Color(0.10f + glow * 0.20f, 0.055f + glow * 0.08f,
                    0.045f + glow * 0.025f, 1).Lerp(new Color("28130f"), vertical * 0.35f);
                image.SetPixel(x, y, color);
            }
        for (var x = 0; x < image.GetWidth(); x++)
            image.SetPixel(x, 136 + (x % 17 > 10 ? 1 : 0), new Color("9b6b32"));
        return ImageTexture.CreateFromImage(image);
    }

    private void SelectNewGameScenario(int index)
    {
        if (index < 0 || index >= _newGameScenarios.Count || _newGameFactionMap is null) return;
        _newGameSelectedScenarioIndex = index;
        _newGameSelectedFactionId = 0;
        for (var cardIndex = 0; cardIndex < _newGameScenarioCards.Count; cardIndex++)
            ApplyScenarioCardStyle(_newGameScenarioCards[cardIndex], cardIndex == index);
        try
        {
            var (path, draft) = _newGameScenarios[index];
            var preset = LoadScenarioMap(draft, _newGameScenarioDirectory);
            _newGameFactionMap.SetScenario(draft, preset, 0);
            var displayTitle = ScenarioDisplayTitle(draft);
            SetNamedLabel("SelectedScenarioTitle", displayTitle);
            if (FindChild("SelectedScenarioTitle", true, false) is Label titleLabel)
                titleLabel.TooltipText = displayTitle;
            SetNamedLabel("SelectedScenarioDate", $"{draft.StartYear}년 {draft.StartMonth}월");
            SetNamedLabel("SelectedScenarioDescription", ScenarioDescription(draft, displayTitle));
            SetNamedLabel("SelectedScenarioSummary",
                $"⚑  주요 세력  {draft.Factions.Count(faction => faction.Playable && !faction.Hidden)}      ▥  총 거점  {preset.Sites.Count(site => site.Kind is "city" or "port" or "gate")}");
            ResetNewGameFactionDetails();
            var firstFaction = draft.Factions.FirstOrDefault(faction => faction.Playable && !faction.Hidden);
            if (firstFaction is not null) SelectNewGameFaction(firstFaction.Id);
            if (_newGameError is not null) _newGameError.Text = "";
            _newGameScenarioCards[index].GrabFocus();
        }
        catch (Exception failure)
        {
            _newGameFactionMap.ClearScenario();
            ResetNewGameFactionDetails();
            if (_newGameError is not null) _newGameError.Text = "시나리오 미리보기 오류: " + failure.Message;
        }
    }

    private static string ScenarioDisplayTitle(ScenarioDraft draft)
        => draft.Title.StartsWith($"{draft.StartYear}년,", StringComparison.Ordinal)
            ? draft.Title[($"{draft.StartYear}년,").Length..].Trim()
            : draft.Title;

    private static string ScenarioDescription(ScenarioDraft draft, string displayTitle)
    {
        var description = draft.Description.Trim();
        return string.IsNullOrWhiteSpace(description)
            || string.Equals(description, displayTitle, StringComparison.OrdinalIgnoreCase)
            || string.Equals(description, draft.Title.Trim(), StringComparison.OrdinalIgnoreCase)
            ? "등록된 시나리오 설명이 없습니다."
            : description;
    }

    private void SetNamedLabel(string name, string text)
    {
        if (FindChild(name, true, false) is Label label) label.Text = text;
    }

    private void ResetNewGameFactionDetails()
    {
        _newGameSelectedFactionId = 0;
        _newGameFactionMap?.SelectFaction(0);
        if (_newGameFactionHint is not null) _newGameFactionHint.Visible = true;
        if (_newGameRulerPortrait is not null)
        {
            _newGameRulerPortrait.Visible = false;
            _newGameRulerPortrait.Texture = null;
        }
        if (_newGameFactionName is not null) _newGameFactionName.Visible = false;
        if (_newGameRulerName is not null) _newGameRulerName.Visible = false;
        if (_newGameFactionSites is not null) _newGameFactionSites.Text = "";
        if (_newGameFactionTroops is not null) _newGameFactionTroops.Text = "";
        if (_newGameFactionGenerals is not null) _newGameFactionGenerals.Text = "";
        if (_newGameFactionDescription is not null) _newGameFactionDescription.Visible = false;
        if (FindChild("FactionStatistics", true, false) is Control statistics) statistics.Visible = false;
        if (FindChild("FactionDescriptionScroll", true, false) is Control descriptionScroll) descriptionScroll.Visible = false;
        if (FindChild("FactionIdentityRow", true, false) is Control identityRow) identityRow.Visible = false;
        if (_newGameStartButton is not null)
        {
            SetFooterActionEnabled(_newGameStartButton, false);
            _newGameStartButton.TooltipText = "지도에서 플레이할 세력을 선택하십시오.";
        }
    }

    private void SelectNewGameFaction(int factionId)
    {
        if (_newGameSelectedScenarioIndex < 0 || _newGameSelectedScenarioIndex >= _newGameScenarios.Count) return;
        var draft = _newGameScenarios[_newGameSelectedScenarioIndex].Draft;
        var faction = draft.Factions.FirstOrDefault(entry => entry.Id == factionId
            && entry.Playable && !entry.Hidden);
        if (faction is null) return;
        _newGameSelectedFactionId = factionId;
        _newGameFactionMap?.SelectFaction(factionId);
        _newGameRulerNames ??= new ScenarioLoader().LoadFromDirectory(
            Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data")))
            .Generals.ToDictionary(entry => entry.Id.Value, entry => entry.Name);
        var rulerName = _newGameRulerNames.GetValueOrDefault(faction.RulerGeneralId, "미지정");
        var cities = draft.Cities.Where(entry => entry.OwnerFactionId == factionId).ToArray();
        if (_newGameFactionHint is not null) _newGameFactionHint.Visible = false;
        if (FindChild("FactionIdentityRow", true, false) is Control identityRow) identityRow.Visible = true;
        if (FindChild("FactionStatistics", true, false) is Control statistics) statistics.Visible = true;
        if (FindChild("FactionDescriptionScroll", true, false) is Control descriptionScroll) descriptionScroll.Visible = true;
        if (_newGameRulerPortrait is not null)
        {
            var portraitPath = $"res://assets/portraits/{faction.RulerGeneralId}.png";
            _newGameRulerPortrait.Texture = Godot.FileAccess.FileExists(portraitPath)
                ? GD.Load<Texture2D>(portraitPath) : null;
            _newGameRulerPortrait.Visible = true;
        }
        if (_newGameFactionName is not null)
        {
            _newGameFactionName.Text = faction.Name;
            _newGameFactionName.Visible = true;
        }
        if (_newGameRulerName is not null)
        {
            _newGameRulerName.Text = $"군주  {rulerName}";
            _newGameRulerName.Visible = true;
        }
        if (_newGameFactionSites is not null) _newGameFactionSites.Text = cities.Length.ToString("N0");
        if (_newGameFactionTroops is not null)
            _newGameFactionTroops.Text = cities.Sum(city => city.Garrisons.Sum(garrison => garrison.Troops)).ToString("N0");
        if (_newGameFactionGenerals is not null)
            _newGameFactionGenerals.Text = draft.Generals.Count(entry => entry.FactionId == factionId).ToString("N0");
        if (_newGameFactionDescription is not null)
        {
            _newGameFactionDescription.Text = "세력 설명이 등록되지 않았습니다.";
            _newGameFactionDescription.Visible = true;
        }
        if (_newGameStartButton is not null)
        {
            SetFooterActionEnabled(_newGameStartButton, true);
            _newGameStartButton.TooltipText = $"{faction.Name} 세력으로 시작합니다.";
        }
        if (_newGameError is not null) _newGameError.Text = "";
    }

    private void SetNewGameDifficulty(GameDifficulty difficulty)
    {
        _newGameDifficulty = difficulty;
        StyleDifficultyButton(_normalDifficultyButton, difficulty == GameDifficulty.Normal);
        StyleDifficultyButton(_hardDifficultyButton, difficulty == GameDifficulty.Hard);
    }

    private static void StyleDifficultyButton(Button? button, bool selected)
    {
        if (button is null) return;
        button.SetMeta("difficulty_selected", selected);
        button.AddThemeStyleboxOverride("normal", NewGameDifficultyTexture(selected, "normal", button.Size));
        button.AddThemeStyleboxOverride("hover", NewGameDifficultyTexture(selected, "hover", button.Size));
        button.AddThemeStyleboxOverride("pressed", NewGameDifficultyTexture(selected, "pressed", button.Size));
        button.AddThemeStyleboxOverride("hover_pressed", NewGameDifficultyTexture(selected, "pressed", button.Size));
        button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        if (button.FindChild("DifficultyCaption", false, false) is Label caption)
        {
            caption.Text = selected ? $"◆  {button.GetMeta("difficulty_label").AsString()}" :
                button.GetMeta("difficulty_label").AsString();
            caption.AddThemeColorOverride("font_color", selected ? NewGameGoldBright : NewGameIvory);
        }
    }

    private void StartSelectedNewGame()
    {
        if (_newGameSelectedScenarioIndex < 0 || _newGameSelectedScenarioIndex >= _newGameScenarios.Count)
        {
            if (_newGameError is not null) _newGameError.Text = "시나리오를 선택하십시오.";
            return;
        }
        if (_newGameSelectedFactionId <= 0)
        {
            if (_newGameError is not null) _newGameError.Text = "지도에서 플레이할 세력을 선택하십시오.";
            return;
        }
        var (selectedPath, selectedDraft) = _newGameScenarios[_newGameSelectedScenarioIndex];
        try
        {
            var data = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), "..", "data"));
            var source = CreatedGeneralPresets.ApplyAtNewGame(new ScenarioLoader().LoadFromDirectory(data));
            var archive = new ScenarioMapArchive(ProjectSettings.GlobalizePath("user://maps"),
                _newGameScenarioDirectory);
            ScenarioMapVersion map;
            var draft = selectedDraft;
            if (ScenarioRootMapBinding.UsesRoot(draft.MapId))
            {
                map = ScenarioRootMapSource.OpenVersion(archive);
                draft = ScenarioRootMapBinding.Bind(draft, map, ScenarioRootMapSource.PreviousVersion(archive, draft));
            }
            else map = archive.Load(draft.MapId, draft.MapRevision);
            ScenarioDraftApplier.Apply(source, draft, map.Preset);
        }
        catch (Exception failure)
        {
            if (_newGameError is not null) _newGameError.Text = "시나리오 시작 불가: " + failure.Message;
            return;
        }
        ShowStartConfirmation(() =>
        {
            SelectedPresetPath = null;
            SelectedScenarioPath = selectedPath;
            SelectedPlayerFactionId = _newGameSelectedFactionId;
            SelectedScenarioTitle = selectedDraft.Title;
            SelectedDifficulty = _newGameDifficulty;
            _launchRequested = true;
            CampaignStartedFromMenu = true;
            ChangeToCampaign();
        });
    }

    private void ShowStartConfirmation(Action onConfirmed)
    {
        var layer = new CanvasLayer { Name = "NewGameConfirmation", Layer = 40 };
        AddChild(layer);
        void Close() => layer.QueueFree();
        var backdrop = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.66f),
            MouseFilter = MouseFilterEnum.Stop
        };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        backdrop.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) Close();
        };
        layer.AddChild(backdrop);
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layer.AddChild(center);
        var panel = new PanelContainer { Name = "NewGameConfirmationPanel", MouseFilter = MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", NewGameFrame(
            new Color(0.105f, 0.04f, 0.035f, 0.985f), new Color("d1aa5b"), 1, 7, 18));
        center.AddChild(panel);
        var content = new VBoxContainer { CustomMinimumSize = new Vector2(620, 0) };
        content.AddThemeConstantOverride("separation", 10);
        panel.AddChild(content);
        var title = new Label { Text = "새 게임 시작 확인", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontOverride("font", NewGameSerif(700));
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", new Color("e8c879"));
        content.AddChild(title);
        content.AddChild(NewGameRule());
        var dialogue = new HBoxContainer();
        dialogue.AddThemeConstantOverride("separation", 18);
        content.AddChild(dialogue);
        var texture = GD.Load<Texture2D>("res://assets/portraits/113.png");
        if (texture is not null)
            dialogue.AddChild(new TextureRect
            {
                Texture = texture,
                CustomMinimumSize = new Vector2(155, 210),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            });
        var speech = new Label
        {
            Text = "초선: 이 세력과 함께\n새로운 역사를 시작하시겠습니까?",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        speech.AddThemeFontOverride("font", NewGameSerif(550));
        speech.AddThemeFontSizeOverride("font_size", 17);
        speech.AddThemeColorOverride("font_color", new Color("f2d28a"));
        dialogue.AddChild(speech);
        content.AddChild(NewGameRule());
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 10);
        content.AddChild(buttons);
        var cancel = NewGameDialogButton("NewGameConfirmCancel", "취소", false);
        cancel.CustomMinimumSize = new Vector2(120, 38);
        cancel.Pressed += Close;
        buttons.AddChild(cancel);
        var confirm = NewGameDialogButton("NewGameConfirmAccept", "확인", true);
        confirm.CustomMinimumSize = new Vector2(120, 38);
        confirm.Pressed += () => { Close(); onConfirmed(); };
        buttons.AddChild(confirm);
    }

    private static Button NewGameDialogButton(string name, string text, bool primary)
    {
        var button = new Button { Name = name, Text = text };
        button.AddThemeFontOverride("font", NewGameSerif(primary ? 700 : 550));
        button.AddThemeFontSizeOverride("font_size", primary ? 17 : 15);
        button.AddThemeStyleboxOverride("normal", NewGameButtonStyle(
            primary ? new Color("6d211a") : new Color("231915"), new Color("b58a43"), 1));
        button.AddThemeStyleboxOverride("hover", NewGameButtonStyle(
            primary ? new Color("8c3023") : new Color("37231b"), new Color("e0b95f"), 1));
        button.AddThemeStyleboxOverride("pressed", NewGameButtonStyle(
            new Color("4d1713"), new Color("f0cc73"), 2));
        button.AddThemeColorOverride("font_color", NewGameGoldBright);
        return button;
    }

    private void SelectFirstFactionForQa()
    {
        if (_newGameFactionMap is { FirstSelectableFactionId: > 0 } map)
            map.SelectFactionAt(map.FirstSelectableSitePosition);
    }
}
