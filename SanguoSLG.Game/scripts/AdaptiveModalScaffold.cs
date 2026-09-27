using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 화면 크기에 맞춰 폭을 결정하고 고정 헤더·본문·푸터를 제공하는 공용 모달 틀이다.
/// 게임 규칙을 모르며, 각 화면은 Body와 Footer에 표현 노드만 추가한다.
/// </summary>
public sealed partial class AdaptiveModalScaffold : PanelContainer
{
    public enum WidthProfile
    {
        Compact,
        Standard,
        Wide,
    }

    private const float ViewportMargin = 24f;
    private const float HeightRatio = 0.82f;

    public HBoxContainer Header { get; }
    public ScrollContainer BodyScroll { get; }
    public VBoxContainer Body { get; }
    public HBoxContainer Footer { get; }
    public Vector2 LayoutSize { get; private set; }

    public AdaptiveModalScaffold()
    {
        Name = "AdaptiveModalScaffold";
        MouseFilter = MouseFilterEnum.Stop;
        SetMeta("modal_role", "scaffold");

        var root = new VBoxContainer
        {
            Name = "ModalRegions",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);

        Header = new HBoxContainer
        {
            Name = "ModalHeader",
            CustomMinimumSize = new Vector2(0, 44),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        Header.SetMeta("modal_role", "header");
        root.AddChild(Header);

        BodyScroll = new ScrollContainer
        {
            Name = "ModalBodyScroll",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        BodyScroll.SetMeta("modal_role", "body_scroll");
        root.AddChild(BodyScroll);

        var bodyMargin = new MarginContainer
        {
            Name = "ModalBodyMargin",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        bodyMargin.AddThemeConstantOverride("margin_right", 8);
        BodyScroll.AddChild(bodyMargin);

        Body = new VBoxContainer
        {
            Name = "ModalBody",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        Body.AddThemeConstantOverride("separation", 12);
        Body.SetMeta("modal_role", "body");
        bodyMargin.AddChild(Body);

        Footer = new HBoxContainer
        {
            Name = "ModalFooter",
            Alignment = BoxContainer.AlignmentMode.End,
            CustomMinimumSize = new Vector2(0, 44),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Visible = false,
        };
        Footer.AddThemeConstantOverride("separation", 10);
        Footer.SetMeta("modal_role", "footer");
        root.AddChild(Footer);
    }

    public void Configure(Vector2 viewportSize, WidthProfile profile)
    {
        LayoutSize = CalculateLayoutSize(viewportSize, profile);
        CustomMinimumSize = LayoutSize;
        SetMeta("modal_profile", profile.ToString());
        SetMeta("modal_width", LayoutSize.X);
        SetMeta("modal_height", LayoutSize.Y);
    }

    public void SetHeader(Control title, BaseButton closeButton, Control? separator = null)
    {
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.SetMeta("modal_role", "title");
        Header.AddChild(title);

        closeButton.Name = "ModalCloseButton";
        closeButton.CustomMinimumSize = new Vector2(38, 38);
        closeButton.SizeFlagsHorizontal = SizeFlags.ShrinkEnd;
        closeButton.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        closeButton.SetMeta("modal_role", "close");
        Header.AddChild(closeButton);

        if (separator is not null)
        {
            separator.Name = "ModalHeaderSeparator";
            separator.SetMeta("modal_role", "header_separator");
            Body.AddChild(separator);
        }
    }

    public void AddFooterControl(Control control)
    {
        Footer.Visible = true;
        control.SetMeta("modal_role", "footer_action");
        Footer.AddChild(control);
    }

    public static Vector2 CalculateLayoutSize(Vector2 viewportSize, WidthProfile profile)
    {
        var availableWidth = Mathf.Max(320f, viewportSize.X - ViewportMargin * 2f);
        var availableHeight = Mathf.Max(320f, viewportSize.Y - ViewportMargin * 2f);
        var widthRatio = profile switch
        {
            WidthProfile.Compact => 0.52f,
            WidthProfile.Standard => 0.73f,
            _ => 0.90f,
        };
        var minimumWidth = profile switch
        {
            WidthProfile.Compact => 420f,
            WidthProfile.Standard => 620f,
            _ => 820f,
        };
        var width = Mathf.Min(Mathf.Max(viewportSize.X * widthRatio, minimumWidth), availableWidth);
        var height = Mathf.Min(Mathf.Max(viewportSize.Y * HeightRatio, 420f), availableHeight);
        return new Vector2(Mathf.Round(width), Mathf.Round(height));
    }
}
