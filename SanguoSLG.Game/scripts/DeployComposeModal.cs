using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 전투 부대편성 전용 모달 뷰. 게임 상태와 계산을 알지 않으며 고정 이미지 프레임과
/// 병종/설정, 편성 장수/목록, 요약/확정 영역의 배치만 소유한다.
/// </summary>
public sealed partial class DeployComposeModal : PanelContainer
{
    public readonly record struct LayoutMetrics(
        // 전체 모달 안전 영역
        int SafeLeft = 33,
        int SafeRight = 33,
        int SafeTop = 18,
        int SafeBottom = 28,
        int RootSeparation = 7,
        // 헤더 영역
        float HeaderMinHeight = 72f,
        float HeaderLeadingSpacerWidth = 46f,
        int HeaderSeparation = 10,
        int HeaderMarginLeft = 0,
        int HeaderMarginRight = 0,
        int HeaderMarginTop = 0,
        int HeaderMarginBottom = 0,
        // 바디 영역 (좌·우 컬럼)
        int BodySeparation = 12,
        int BodyMarginLeft = 0,
        int BodyMarginRight = 0,
        int BodyMarginTop = 0,
        int BodyMarginBottom = 0,
        float LeftStretchRatio = 0.92f,
        float RightStretchRatio = 1.5f,
        int LeftContentSeparation = 8,
        int RightContentSeparation = 8,
        // 푸터 영역
        float FooterMinHeight = 68f,
        int FooterContentSeparation = 5,
        int FooterMarginLeft = 12,
        int FooterMarginRight = 0,
        int FooterMarginTop = 0,
        int FooterMarginBottom = 0
    );

    public HBoxContainer Header { get; }
    public Button BackButton { get; }
    public HBoxContainer Body { get; }
    public VBoxContainer LeftContent { get; }
    public VBoxContainer RightContent { get; }
    public VBoxContainer FooterContent { get; }
    public ScrollContainer LeftScroll { get; }
    public VBoxContainer Root { get; }

    private readonly MarginContainer _safeArea;
    private readonly MarginContainer _headerContainer;
    private readonly MarginContainer _bodyContainer;
    private readonly MarginContainer _footerContainer;
    private readonly Control _headerLeadingSpacer;
    private readonly PanelContainer _leftFrame;
    private readonly PanelContainer _rightFrame;

    public DeployComposeModal(
        Vector2 viewport,
        Texture2D backgroundTexture,
        Font font,
        string title,
        LayoutMetrics metrics = default)
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
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);

        _safeArea = new MarginContainer { Name = "DeployComposeSafeArea" };
        AddChild(_safeArea);

        Root = new VBoxContainer
        {
            Name = "DeployComposeRoot",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _safeArea.AddChild(Root);

        // 1. 헤더 영역
        (_headerContainer, Header, _headerLeadingSpacer, BackButton) = BuildHeaderSection(font, title);
        Root.AddChild(_headerContainer);

        // 2. 바디 영역
        (_bodyContainer, Body, _leftFrame, LeftScroll, LeftContent, _rightFrame, RightContent) = BuildBodySection();
        Root.AddChild(_bodyContainer);

        // 3. 푸터 영역
        (_footerContainer, FooterContent) = BuildFooterSection();
        Root.AddChild(_footerContainer);

        ApplyLayoutMetrics(metrics);
    }

    public void ApplyLayoutMetrics(LayoutMetrics metrics)
    {
        // 전체 세이프에리어 & 루트 간격
        SetMargins(_safeArea, metrics.SafeLeft, metrics.SafeRight, metrics.SafeTop, metrics.SafeBottom);
        Root.AddThemeConstantOverride("separation", metrics.RootSeparation);

        // 헤더 간격 및 여백
        SetMargins(_headerContainer, metrics.HeaderMarginLeft, metrics.HeaderMarginRight, metrics.HeaderMarginTop, metrics.HeaderMarginBottom);
        Header.CustomMinimumSize = new Vector2(0, metrics.HeaderMinHeight);
        Header.AddThemeConstantOverride("separation", metrics.HeaderSeparation);
        _headerLeadingSpacer.CustomMinimumSize = new Vector2(metrics.HeaderLeadingSpacerWidth, 0);

        // 바디 간격, 여백 및 컬럼 비율
        SetMargins(_bodyContainer, metrics.BodyMarginLeft, metrics.BodyMarginRight, metrics.BodyMarginTop, metrics.BodyMarginBottom);
        Body.AddThemeConstantOverride("separation", metrics.BodySeparation);
        _leftFrame.SizeFlagsStretchRatio = metrics.LeftStretchRatio;
        _rightFrame.SizeFlagsStretchRatio = metrics.RightStretchRatio;
        LeftContent.AddThemeConstantOverride("separation", metrics.LeftContentSeparation);
        RightContent.AddThemeConstantOverride("separation", metrics.RightContentSeparation);

        // 푸터 간격 및 여백 (제목을 건드리지 않고 푸터만 좌우/상하 여백 조절 가능)
        SetMargins(_footerContainer, metrics.FooterMarginLeft, metrics.FooterMarginRight, metrics.FooterMarginTop, metrics.FooterMarginBottom);
        FooterContent.CustomMinimumSize = new Vector2(0, metrics.FooterMinHeight);
        FooterContent.AddThemeConstantOverride("separation", metrics.FooterContentSeparation);
    }

    private static (MarginContainer Container, HBoxContainer Header, Control Spacer, Button BackButton) BuildHeaderSection(Font font, string title)
    {
        var container = new MarginContainer
        {
            Name = "DeployComposeHeaderContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        var header = new HBoxContainer
        {
            Name = "DeployComposeHeader",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        container.AddChild(header);

        var spacer = new Control
        {
            Name = "DeployComposeHeaderSpacer",
            MouseFilter = MouseFilterEnum.Ignore,
        };
        header.AddChild(spacer);

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
        header.AddChild(titleLabel);

        var backButton = new Button
        {
            Name = "DeployComposeBack",
            Text = "◀ 목록",
            CustomMinimumSize = new Vector2(82, 34),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        backButton.AddThemeFontOverride("font", font);
        header.AddChild(backButton);

        return (container, header, spacer, backButton);
    }

    private static (
        MarginContainer Container,
        HBoxContainer Body,
        PanelContainer LeftFrame,
        ScrollContainer LeftScroll,
        VBoxContainer LeftContent,
        PanelContainer RightFrame,
        VBoxContainer RightContent
    ) BuildBodySection()
    {
        var container = new MarginContainer
        {
            Name = "DeployComposeBodyContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };

        var body = new HBoxContainer
        {
            Name = "DeployComposeColumns",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        container.AddChild(body);

        var leftFrame = PaneFrame("DeployComposeLeftFrame");
        body.AddChild(leftFrame);

        var leftScroll = new ScrollContainer
        {
            Name = "DeployComposeResourcesScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        leftFrame.AddChild(leftScroll);

        var leftContent = new VBoxContainer
        {
            Name = "DeployComposeResources",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        leftScroll.AddChild(leftContent);

        var rightFrame = PaneFrame("DeployComposeRightFrame");
        body.AddChild(rightFrame);

        var rightContent = new VBoxContainer
        {
            Name = "DeployComposeOfficers",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        rightFrame.AddChild(rightContent);

        return (container, body, leftFrame, leftScroll, leftContent, rightFrame, rightContent);
    }

    private static (MarginContainer Container, VBoxContainer Content) BuildFooterSection()
    {
        var container = new MarginContainer
        {
            Name = "DeployComposeFooterContainer",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        var content = new VBoxContainer
        {
            Name = "DeployComposeFooter",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        container.AddChild(content);

        return (container, content);
    }

    private static void SetMargins(MarginContainer marginContainer, int left, int right, int top, int bottom)
    {
        marginContainer.AddThemeConstantOverride("margin_left", left);
        marginContainer.AddThemeConstantOverride("margin_right", right);
        marginContainer.AddThemeConstantOverride("margin_top", top);
        marginContainer.AddThemeConstantOverride("margin_bottom", bottom);
    }

    private static PanelContainer PaneFrame(string name)
    {
        var frame = new PanelContainer
        {
            Name = name,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
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