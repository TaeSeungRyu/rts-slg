using Godot;

namespace SanguoSLG.Game;

public sealed partial class DoctrineResearchWorkspace : VBoxContainer
{
    private static readonly Color Gold = new("c9973f");
    private static readonly Color GoldBright = new("f1cc72");
    private static readonly Color Parchment = new("eadbb5");

    public VBoxContainer DoctrineList { get; }
    public VBoxContainer DetailHost { get; }
    public Label FundingSummary { get; }
    public Label OfficerSummary { get; }
    public Label TotalCost { get; }
    public Button FundingChangeButton { get; }
    public Button OfficerChangeButton { get; }
    public Button StartButton { get; }

    public DoctrineResearchWorkspace()
    {
        Name = "DoctrineResearchWorkspace";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 8);

        var columns = new HBoxContainer
        {
            Name = "DoctrineResearchColumns",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        columns.AddThemeConstantOverride("separation", 12);
        AddChild(columns);

        var listPanel = Panel("DoctrineResearchListPanel", new Color("100908"), new Color("765024"));
        listPanel.SizeFlagsStretchRatio = 2.05f;
        columns.AddChild(listPanel);
        var listMargin = Margin(10);
        listPanel.AddChild(listMargin);
        var listScroll = new ScrollContainer
        {
            Name = "DoctrineResearchListScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        GameUiSkin.ApplyQuickListScroll(listScroll);
        listMargin.AddChild(listScroll);
        var listContentMargin = Margin(0);
        listContentMargin.Name = "DoctrineResearchListContentMargin";
        listContentMargin.AddThemeConstantOverride("margin_right", 16);
        listScroll.AddChild(listContentMargin);
        DoctrineList = Box("DoctrineResearchList");
        listContentMargin.AddChild(DoctrineList);

        var right = Box("DoctrineResearchSummaryColumn");
        right.SizeFlagsStretchRatio = 0.78f;
        right.CustomMinimumSize = new Vector2(330f, 0f);
        columns.AddChild(right);

        var detailPanel = Panel("DoctrineResearchDetailPanel", new Color("1c0e0c"), Gold);
        detailPanel.SizeFlagsVertical = SizeFlags.ExpandFill;
        right.AddChild(detailPanel);
        var detailMargin = Margin(14);
        detailPanel.AddChild(detailMargin);
        DetailHost = Box("DoctrineResearchDetailHost");
        detailMargin.AddChild(DetailHost);

        var funding = SummaryPanel("연구 도시", "DoctrineResearchFundingPanel", out var fundingSummary, out var fundingChange);
        FundingSummary = fundingSummary;
        FundingChangeButton = fundingChange;
        right.AddChild(funding);
        var officer = SummaryPanel("수행 장수", "DoctrineResearchOfficerPanel", out var officerSummary, out var officerChange);
        OfficerSummary = officerSummary;
        OfficerChangeButton = officerChange;
        right.AddChild(officer);

        var footer = Panel("DoctrineResearchFooter", new Color("24120f"), Gold);
        footer.CustomMinimumSize = new Vector2(0f, 58f);
        AddChild(footer);
        var footerRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footerRow.AddThemeConstantOverride("separation", 12);
        footer.AddChild(footerRow);
        TotalCost = Label("총 연구비 0금", 16, GoldBright);
        TotalCost.VerticalAlignment = VerticalAlignment.Center;
        TotalCost.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        footerRow.AddChild(TotalCost);
        StartButton = ActionButton("연구 시작", true);
        StartButton.CustomMinimumSize = new Vector2(150f, 40f);
        StartButton.Disabled = true;
        footerRow.AddChild(StartButton);
    }

    private static PanelContainer SummaryPanel(string title, string name, out Label summary, out Button change)
    {
        var panel = Panel(name, new Color("1b0e0c"), new Color("725029"));
        var box = Box(name + "Content");
        panel.AddChild(box);
        var header = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        header.AddChild(Label(title, 16, GoldBright, true));
        change = ActionButton("변경", false);
        change.CustomMinimumSize = new Vector2(76f, 32f);
        header.AddChild(change);
        box.AddChild(header);
        summary = Label("선택되지 않음", 14, Parchment);
        summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(summary);
        return panel;
    }

    private static VBoxContainer Box(string name)
    {
        var box = new VBoxContainer
        {
            Name = name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        box.AddThemeConstantOverride("separation", 8);
        return box;
    }

    private static MarginContainer Margin(int value)
    {
        var margin = new MarginContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        margin.AddThemeConstantOverride("margin_left", value);
        margin.AddThemeConstantOverride("margin_top", value);
        margin.AddThemeConstantOverride("margin_right", value);
        margin.AddThemeConstantOverride("margin_bottom", value);
        return margin;
    }

    private static Label Label(string text, int size, Color color, bool expand = false)
    {
        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeFontSizeOverride("font_size", size);
        if (expand) label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return label;
    }

    private static Button ActionButton(string text, bool accent)
    {
        var button = new Button { Text = text };
        button.AddThemeColorOverride("font_color", accent ? Colors.White : GoldBright);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        button.AddThemeStyleboxOverride("normal", Style(accent ? new Color("641b15") : new Color("321410"), Gold, 1));
        button.AddThemeStyleboxOverride("hover", Style(new Color("7b241b"), GoldBright, 2));
        button.AddThemeStyleboxOverride("pressed", Style(new Color("47100d"), GoldBright, 2));
        button.AddThemeStyleboxOverride("disabled", Style(new Color("1b1210"), new Color("5c4a32"), 1));
        return button;
    }

    private static PanelContainer Panel(string name, Color background, Color border)
    {
        var panel = new PanelContainer
        {
            Name = name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", Style(background, border, 1));
        return panel;
    }

    private static StyleBoxFlat Style(Color background, Color border, int width) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
        ContentMarginLeft = 10,
        ContentMarginRight = 10,
        ContentMarginTop = 8,
        ContentMarginBottom = 8,
    };
}
