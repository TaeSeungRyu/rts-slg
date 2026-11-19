using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 전투 부대편성 전용 모달 뷰. 게임 상태와 계산을 알지 않으며 고정 이미지 프레임과
/// 병종/설정, 편성 장수/목록, 요약/확정 영역의 배치만 소유한다.
/// </summary>
public sealed partial class DeployComposeModal : PanelContainer
{
    public HBoxContainer Header { get; }
    public Button BackButton { get; }
    public VBoxContainer LeftContent { get; }
    public VBoxContainer RightContent { get; }
    public VBoxContainer FooterContent { get; }
    public ScrollContainer LeftScroll { get; }
    public VBoxContainer Root { get; }

    public DeployComposeModal(Vector2 viewport, Texture2D backgroundTexture, Font font, string title)
    {
        Name = "DeployComposeModal";
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
            Name = "DeployComposeBackground",
            Texture = backgroundTexture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(background);

        var safeArea = new MarginContainer { Name = "DeployComposeSafeArea" };
        safeArea.AddThemeConstantOverride("margin_left", 28);
        safeArea.AddThemeConstantOverride("margin_right", 28);
        safeArea.AddThemeConstantOverride("margin_top", 12);
        safeArea.AddThemeConstantOverride("margin_bottom", 28);
        AddChild(safeArea);

        Root = new VBoxContainer
        {
            Name = "DeployComposeRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        Root.AddThemeConstantOverride("separation", 7);
        safeArea.AddChild(Root);

        Header = new HBoxContainer
        {
            Name = "DeployComposeHeader",
            CustomMinimumSize = new Vector2(0, 72),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        Header.AddThemeConstantOverride("separation", 10);
        Header.AddChild(new Control { CustomMinimumSize = new Vector2(46, 0), MouseFilter = MouseFilterEnum.Ignore });
        var titleLabel = new Label
        {
            Name = "DeployComposeTitle",
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
            Name = "DeployComposeBack",
            Text = "◀ 목록",
            CustomMinimumSize = new Vector2(82, 34),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        BackButton.AddThemeFontOverride("font", font);
        Header.AddChild(BackButton);
        Root.AddChild(Header);

        var body = new HBoxContainer
        {
            Name = "DeployComposeColumns",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        body.AddThemeConstantOverride("separation", 12);
        Root.AddChild(body);

        var leftFrame = PaneFrame("DeployComposeLeftFrame", 0.92f);
        body.AddChild(leftFrame);
        LeftScroll = new ScrollContainer
        {
            Name = "DeployComposeResourcesScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        leftFrame.AddChild(LeftScroll);
        LeftContent = new VBoxContainer { Name = "DeployComposeResources", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        LeftContent.AddThemeConstantOverride("separation", 8);
        LeftScroll.AddChild(LeftContent);

        var rightFrame = PaneFrame("DeployComposeRightFrame", 1.5f);
        body.AddChild(rightFrame);
        RightContent = new VBoxContainer
        {
            Name = "DeployComposeOfficers",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        RightContent.AddThemeConstantOverride("separation", 8);
        rightFrame.AddChild(RightContent);

        FooterContent = new VBoxContainer
        {
            Name = "DeployComposeFooter",
            CustomMinimumSize = new Vector2(0, 68),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        FooterContent.AddThemeConstantOverride("separation", 5);
        Root.AddChild(FooterContent);
    }

    private static PanelContainer PaneFrame(string name, float ratio)
    {
        var frame = new PanelContainer
        {
            Name = name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = ratio,
        };
        var style = new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = new Color("b68632"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 8,
            ContentMarginTop = 8,
            ContentMarginRight = 8,
            ContentMarginBottom = 8,
        };
        frame.AddThemeStyleboxOverride("panel", style);
        return frame;
    }
}
