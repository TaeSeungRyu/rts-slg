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

    private const float HeaderHeight = 72f;
    private const float FooterHeight = 42f;
    private const float HorizontalInset = 16f;

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

        var rootStyle = new StyleBoxFlat
        {
            BgColor = new Color("160b09"),
            BorderColor = new Color("d4aa58"),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 0,
            BorderWidthBottom = 0,
            ContentMarginLeft = 5,
            ContentMarginRight = 5,
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

        HeaderHost = BuildImageRegion(regions, "ModalChromeHeader", HeaderTexturePath, HeaderHeight);

        var bodyFrame = new PanelContainer
        {
            Name = "ModalChromeBodyFrame",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        bodyFrame.AddThemeStyleboxOverride("panel", BuildBodyStyle());
        regions.AddChild(bodyFrame);

        BodyScroll = new ScrollContainer
        {
            Name = "ModalChromeBodyScroll",
            CustomMinimumSize = new Vector2(contentWidth, 0),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        bodyFrame.AddChild(BodyScroll);

        Body = new VBoxContainer
        {
            Name = "ModalChromeContentHost",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        Body.AddThemeConstantOverride("separation", 8);
        BodyScroll.AddChild(Body);

        FooterHost = BuildImageRegion(regions, "ModalChromeFooter", FooterTexturePath, FooterHeight);
    }

    public float BodyHeightFor(float totalContentHeight) =>
        Mathf.Max(120f, totalContentHeight - HeaderHeight - FooterHeight);

    private static HBoxContainer BuildImageRegion(
        VBoxContainer parent,
        string name,
        string texturePath,
        float height)
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
            Texture = GD.Load<Texture2D>(texturePath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
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
        margin.AddThemeConstantOverride("margin_left", (int)HorizontalInset);
        margin.AddThemeConstantOverride("margin_right", (int)HorizontalInset);
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

    private static StyleBoxFlat BuildBodyStyle() => new()
    {
        BgColor = new Color("190d0b"),
        BorderColor = new Color("8e6a34"),
        BorderWidthLeft = 3,
        BorderWidthRight = 3,
        BorderWidthTop = 0,
        BorderWidthBottom = 0,
        ContentMarginLeft = HorizontalInset,
        ContentMarginRight = HorizontalInset,
        ContentMarginTop = 8,
        ContentMarginBottom = 8,
    };
}
