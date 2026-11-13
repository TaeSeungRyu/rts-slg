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

    private readonly float _headerHeight;
    private readonly float _footerHeight;

    public static bool HasRequiredAssets =>
        ResourceLoader.Exists(HeaderTexturePath) && ResourceLoader.Exists(FooterTexturePath);

    public HBoxContainer HeaderHost { get; }
    public ScrollContainer BodyScroll { get; }
    public VBoxContainer Body { get; }
    public HBoxContainer FooterHost { get; }

    public ModalChrome(float contentWidth)
    {
        Name = "ModalChrome";
        MouseFilter = MouseFilterEnum.Stop;
        TextureFilter = TextureFilterEnum.LinearWithMipmaps;
        SetMeta("modal_chrome", true);

        var headerTexture = GD.Load<Texture2D>(HeaderTexturePath);
        var footerTexture = GD.Load<Texture2D>(FooterTexturePath);
        var chromeWidth = contentWidth + HorizontalInset * 2f + 10f;
        _headerHeight = HeightForWidth(headerTexture, chromeWidth);
        _footerHeight = HeightForWidth(footerTexture, chromeWidth);
        var headerContentInset = Mathf.Clamp(chromeWidth * 0.16f, 72f, 112f);

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
            regions, "ModalChromeHeader", headerTexture, _headerHeight, headerContentInset);

        var bodyOuter = new PanelContainer
        {
            Name = "ModalChromeBodyOuterRail",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        bodyOuter.AddThemeStyleboxOverride("panel", BuildRailStyle(new Color("dda94d"), 2, 2, new Color("190d0b")));
        regions.AddChild(bodyOuter);

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
        bodyInner.AddThemeStyleboxOverride("panel", BuildBodyStyle());
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
            regions, "ModalChromeFooter", footerTexture, _footerHeight, HorizontalInset);
    }

    public float BodyHeightFor(float totalContentHeight) =>
        Mathf.Max(120f, totalContentHeight - _headerHeight - _footerHeight);

    private static HBoxContainer BuildImageRegion(
        VBoxContainer parent,
        string name,
        Texture2D texture,
        float height,
        float contentInset)
    {
        var layer = new Control
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

        var margin = new MarginContainer
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

    private static StyleBoxFlat BuildBodyStyle() => new()
    {
        BgColor = Colors.Transparent,
        BorderColor = new Color("95632b"),
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        BorderWidthTop = 0,
        BorderWidthBottom = 0,
        ContentMarginLeft = HorizontalInset,
        ContentMarginRight = HorizontalInset,
        ContentMarginTop = 8,
        ContentMarginBottom = 8,
    };
}
