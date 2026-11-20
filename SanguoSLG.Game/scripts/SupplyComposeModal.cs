using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 보급편성 전용 모달 뷰. 전투편성과 동일한 3구획 프레임을 사용하되,
/// 보급 데이터와 명령 처리는 소유하지 않는다.
/// </summary>
public partial class SupplyComposeModal : PanelContainer
{
    public HBoxContainer Header { get; }
    public Button BackButton { get; }
    public HBoxContainer Body { get; }
    public VBoxContainer LeftContent { get; }
    public VBoxContainer RightContent { get; }
    public VBoxContainer FooterContent { get; }
    public ScrollContainer LeftScroll { get; }
    public VBoxContainer Root { get; }

    public SupplyComposeModal(Vector2 viewport, Texture2D backgroundTexture, Font font, string title)
    {
        Name = "SupplyComposeModal";
        var height = Mathf.Min(viewport.Y - 24f, 848f);
        var width = Mathf.Min(viewport.X - 24f, height * 1.5f);
        CustomMinimumSize = new Vector2(width, height);
        Size = new Vector2(width, height);
        Position = (viewport - Size) / 2f;
        MouseFilter = MouseFilterEnum.Stop;
        SetMeta("modal_root", true);
        AddThemeStyleboxOverride("panel", new StyleBoxEmpty());

        var background = new TextureRect
        {
            Name = "SupplyComposeBackground",
            Texture = backgroundTexture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var safeArea = new MarginContainer { Name = "SupplyComposeSafeArea" };
        safeArea.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetMargins(safeArea, 48, 48, 15, 15);
        AddChild(safeArea);

        Root = new VBoxContainer
        {
            Name = "SupplyComposeRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        Root.AddThemeConstantOverride("separation", 7);
        safeArea.AddChild(Root);

        var headerContainer = new MarginContainer { Name = "SupplyComposeHeaderContainer" };
        SetMargins(headerContainer, -7, 0, 8, 0);
        Root.AddChild(headerContainer);
        Header = new HBoxContainer
        {
            Name = "SupplyComposeHeader",
            CustomMinimumSize = new Vector2(0, 50),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        Header.AddThemeConstantOverride("separation", 10);
        headerContainer.AddChild(Header);
        Header.AddChild(new Control
        {
            Name = "SupplyComposeHeaderSpacer",
            CustomMinimumSize = new Vector2(46, 0),
            MouseFilter = MouseFilterEnum.Ignore,
        });
        var titleLabel = new Label
        {
            Name = "SupplyComposeTitle",
            Text = title,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        titleLabel.AddThemeFontOverride("font", font);
        titleLabel.AddThemeFontSizeOverride("font_size", 24);
        titleLabel.AddThemeColorOverride("font_color", new Color("f4d777"));
        Header.AddChild(titleLabel);
        BackButton = new Button
        {
            Name = "SupplyComposeBack",
            Text = "◀ 목록",
            CustomMinimumSize = new Vector2(82, 34),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        BackButton.AddThemeFontOverride("font", font);
        Header.AddChild(BackButton);

        var bodyContainer = new MarginContainer
        {
            Name = "SupplyComposeBodyContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        SetMargins(bodyContainer, 0, 0, 27, 0);
        Root.AddChild(bodyContainer);
        Body = new HBoxContainer
        {
            Name = "SupplyComposeColumns",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        Body.AddThemeConstantOverride("separation", 12);
        bodyContainer.AddChild(Body);

        var leftFrame = PaneFrame("SupplyComposeLeftFrame", 0.92f);
        Body.AddChild(leftFrame);
        LeftScroll = new ScrollContainer
        {
            Name = "SupplyComposeResourcesScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        leftFrame.AddChild(LeftScroll);
        LeftContent = new VBoxContainer
        {
            Name = "SupplyComposeResources",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        LeftContent.AddThemeConstantOverride("separation", 8);
        LeftScroll.AddChild(LeftContent);

        var rightFrame = PaneFrame("SupplyComposeRightFrame", 1.5f);
        Body.AddChild(rightFrame);
        RightContent = new VBoxContainer
        {
            Name = "SupplyComposeOfficers",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        RightContent.AddThemeConstantOverride("separation", 8);
        rightFrame.AddChild(RightContent);

        var footerContainer = new MarginContainer
        {
            Name = "SupplyComposeFooterContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        SetMargins(footerContainer, 12, 0, 0, 0);
        Root.AddChild(footerContainer);
        FooterContent = new VBoxContainer
        {
            Name = "SupplyComposeFooter",
            CustomMinimumSize = new Vector2(0, 68),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        FooterContent.AddThemeConstantOverride("separation", 5);
        footerContainer.AddChild(FooterContent);
    }

    private static PanelContainer PaneFrame(string name, float stretchRatio)
    {
        var frame = new PanelContainer
        {
            Name = name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = stretchRatio,
        };
        frame.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 8,
            ContentMarginTop = 8,
            ContentMarginRight = 8,
            ContentMarginBottom = 8,
        });
        return frame;
    }

    private static void SetMargins(MarginContainer container, int left, int right, int top, int bottom)
    {
        container.AddThemeConstantOverride("margin_left", left);
        container.AddThemeConstantOverride("margin_right", right);
        container.AddThemeConstantOverride("margin_top", top);
        container.AddThemeConstantOverride("margin_bottom", bottom);
    }
}
