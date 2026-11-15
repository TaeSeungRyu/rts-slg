using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// PNG header/footer, an opaque body and fixed-width gold side rails used by
/// modal windows that explicitly opt in.  Missing image assets are handled by
/// the caller so existing modal presentation can remain unchanged.
/// </summary>
public sealed partial class ModalChrome : PanelContainer
{
    public const string HeaderTexturePath = "res://assets/ui/game/modals/modal_header.png";
    public const string FooterTexturePath = "res://assets/ui/game/modals/modal_footer.png";

    private const float HorizontalInset = 16f;
    public const float BodyHorizontalInset = 5f;
    public const float HorizontalChromeExtras = HorizontalInset * 2f + 10f
        + BodyHorizontalInset * 2f;

    private readonly Texture2D _headerTexture;
    private readonly Texture2D _footerTexture;
    private readonly Control _headerRegion;
    private readonly Control _footerRegion;
    private readonly MarginContainer _headerContentMargin;
    private float _headerHeight;
    private float _footerHeight;

    public static bool HasRequiredAssets =>
        ResourceLoader.Exists(HeaderTexturePath) && ResourceLoader.Exists(FooterTexturePath);

    public HBoxContainer HeaderHost { get; }
    public ScrollContainer BodyScroll { get; }
    public VBoxContainer Body { get; }
    public HBoxContainer FooterHost { get; }
    public float HeaderFooterHeight => _headerHeight + _footerHeight;

    public ModalChrome(
        float contentWidth,
        float bodyTopPadding = 8f,
        float bodyBottomPadding = 8f)
    {
        Name = "ModalChrome";
        MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        SetMeta("modal_chrome", true);

        _headerTexture = GD.Load<Texture2D>(HeaderTexturePath);
        _footerTexture = GD.Load<Texture2D>(FooterTexturePath);
        var chromeWidth = contentWidth + HorizontalChromeExtras;
        _headerHeight = HeightForWidth(_headerTexture, chromeWidth);
        _footerHeight = HeightForWidth(_footerTexture, chromeWidth);
        // 원본 용 장식이 좌우 약 21%를 차지한다. 제목과 닫기 버튼은 장식 안쪽 중앙 띠에 둔다.
        var headerContentInset = HeaderContentInset(chromeWidth);

        var rootStyle = new StyleBoxFlat
        {
            BgColor = Colors.Transparent,
            BorderColor = Colors.Transparent,
            BorderWidthLeft = 0,
            BorderWidthRight = 0,
            BorderWidthTop = 0,
            BorderWidthBottom = 0,
            ContentMarginLeft = 0,
            ContentMarginRight = 0,
            ContentMarginTop = 0,
            ContentMarginBottom = 0,
        };
        AddThemeStyleboxOverride("panel", rootStyle);

        var regions = new VBoxContainer
        {
            Name = "ModalChromeRegions",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        regions.AddThemeConstantOverride("separation", 0);
        AddChild(regions);

        HeaderHost = BuildImageRegion(
            regions, "ModalChromeHeader", _headerTexture, _headerHeight, headerContentInset,
            out _headerRegion, out _headerContentMargin);

        var bodyOuter = new PanelContainer
        {
            Name = "ModalChromeBodyOuterRail",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        bodyOuter.AddThemeStyleboxOverride("panel", BuildRailStyle(new Color("dda94d"), 2, 2, new Color("190d0b")));
        var bodyFrameMargin = new MarginContainer
        {
            Name = "ModalChromeBodyFrameMargin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        bodyFrameMargin.AddThemeConstantOverride("margin_left", Mathf.RoundToInt(BodyHorizontalInset));
        bodyFrameMargin.AddThemeConstantOverride("margin_right", Mathf.RoundToInt(BodyHorizontalInset));
        regions.AddChild(bodyFrameMargin);
        bodyFrameMargin.AddChild(bodyOuter);

        var bodyMiddle = new PanelContainer
        {
            Name = "ModalChromeBodyMiddleRail",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        bodyMiddle.AddThemeStyleboxOverride("panel", BuildRailStyle(new Color("593015"), 1, 2, Colors.Transparent));
        bodyOuter.AddChild(bodyMiddle);

        var bodyInner = new PanelContainer
        {
            Name = "ModalChromeBodyInnerRail",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        bodyInner.AddThemeStyleboxOverride("panel", BuildBodyStyle(bodyTopPadding, bodyBottomPadding));
        bodyMiddle.AddChild(bodyInner);

        BodyScroll = new ScrollContainer
        {
            Name = "ModalChromeBodyScroll",
            CustomMinimumSize = new Vector2(contentWidth, 0),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        bodyInner.AddChild(BodyScroll);

        Body = new VBoxContainer
        {
            Name = "ModalChromeContentHost",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        Body.AddThemeConstantOverride("separation", 8);
        BodyScroll.AddChild(Body);

        FooterHost = BuildImageRegion(
            regions, "ModalChromeFooter", _footerTexture, _footerHeight, HorizontalInset,
            out _footerRegion, out _);
    }

    public void SetContentWidth(float contentWidth)
    {
        var safeContentWidth = Mathf.Max(320f, contentWidth);
        var chromeWidth = safeContentWidth + HorizontalChromeExtras;
        _headerHeight = HeightForWidth(_headerTexture, chromeWidth);
        _footerHeight = HeightForWidth(_footerTexture, chromeWidth);
        BodyScroll.CustomMinimumSize = new Vector2(safeContentWidth, 0f);
        _headerRegion.CustomMinimumSize = new Vector2(0f, _headerHeight);
        _footerRegion.CustomMinimumSize = new Vector2(0f, _footerHeight);
        var headerInset = Mathf.RoundToInt(HeaderContentInset(chromeWidth));
        _headerContentMargin.AddThemeConstantOverride("margin_left", headerInset);
        _headerContentMargin.AddThemeConstantOverride("margin_right", headerInset);
    }

    public float BodyHeightFor(float totalContentHeight) =>
        Mathf.Max(120f, totalContentHeight - _headerHeight - _footerHeight);

    private static HBoxContainer BuildImageRegion(
        VBoxContainer parent,
        string name,
        Texture2D texture,
        float height,
        float contentInset,
        out Control layer,
        out MarginContainer margin)
    {
        layer = new Control
        {
            Name = name,
            CustomMinimumSize = new Vector2(0, height),
            MouseFilter = MouseFilterEnum.Ignore,
            ClipContents = true,
        };
        parent.AddChild(layer);

        var image = new TextureRect
        {
            Name = name + "Image",
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        image.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layer.AddChild(image);

        margin = new MarginContainer
        {
            Name = name + "ContentMargin",
            MouseFilter = MouseFilterEnum.Pass,
        };
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", Mathf.RoundToInt(contentInset));
        margin.AddThemeConstantOverride("margin_right", Mathf.RoundToInt(contentInset));
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        layer.AddChild(margin);

        var host = new HBoxContainer
        {
            Name = name + "ContentHost",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Pass,
        };
        margin.AddChild(host);
        return host;
    }

    private static float HeightForWidth(Texture2D texture, float width)
    {
        var size = texture.GetSize();
        return size.X <= 0f ? 0f : Mathf.Ceil(width * size.Y / size.X);
    }

    private static float HeaderContentInset(float chromeWidth) =>
        Mathf.Clamp(chromeWidth * 0.22f, 96f, 300f);

    private static StyleBoxFlat BuildRailStyle(Color border, int borderWidth, int inset, Color background) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = borderWidth,
        BorderWidthRight = borderWidth,
        BorderWidthTop = 0,
        BorderWidthBottom = 0,
        ContentMarginLeft = inset,
        ContentMarginRight = inset,
        ContentMarginTop = 0,
        ContentMarginBottom = 0,
    };

    private static StyleBoxFlat BuildBodyStyle(float topPadding, float bottomPadding) => new()
    {
        BgColor = Colors.Transparent,
        BorderColor = new Color("95632b"),
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        BorderWidthTop = 0,
        BorderWidthBottom = 0,
        ContentMarginLeft = HorizontalInset,
        ContentMarginRight = HorizontalInset,
        ContentMarginTop = topPadding,
        ContentMarginBottom = bottomPadding,
    };
}
