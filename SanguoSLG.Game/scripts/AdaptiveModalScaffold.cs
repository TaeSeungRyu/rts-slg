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

    public enum ContentLayout
    {
        Fixed,
        Empty,
        Table,
        Cards,
        Mixed,
    }

    private const float ViewportMargin = 24f;
    private const float HeightRatio = 0.82f;
    // ModalChrome은 본문 폭 양쪽에 레일·패딩을 더한다.
    private const float ChromeHorizontalExtras = ModalChrome.HorizontalChromeExtras;
    private const float ScrollBarAllowance = 18f;

    private ModalChrome? _chrome;
    private Vector2 _viewportSize;
    private WidthProfile _widthProfile;
    private ContentLayout _contentLayout = ContentLayout.Fixed;
    private int _pendingFitPasses;
    private bool _fitQueued;

    public HBoxContainer Header { get; private set; } = null!;
    public ScrollContainer BodyScroll { get; private set; } = null!;
    public VBoxContainer Body { get; private set; } = null!;
    public HBoxContainer Footer { get; private set; } = null!;
    public Vector2 LayoutSize { get; private set; }
    public bool UsesImageChrome => _chrome is not null;
    public float BodyLayoutHeight => _chrome?.BodyHeightFor(LayoutSize.Y)
        ?? Mathf.Max(120f, LayoutSize.Y - 104f);

    public AdaptiveModalScaffold()
    {
        Name = "AdaptiveModalScaffold";
        MouseFilter = MouseFilterEnum.Stop;
        SetMeta("modal_role", "scaffold");
        AddThemeStyleboxOverride("panel", TransparentPanelStyle());
    }

    public override void _Ready()
    {
        RequestContentFit();
    }

    private void BuildLegacyRegions()
    {
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

    public void Configure(
        Vector2 viewportSize,
        WidthProfile profile,
        ContentLayout contentLayout = ContentLayout.Fixed)
    {
        _viewportSize = viewportSize;
        _widthProfile = profile;
        _contentLayout = contentLayout;
        LayoutSize = CalculateLayoutSize(viewportSize, profile, contentLayout);
        CustomMinimumSize = LayoutSize;
        if (Header is null)
        {
            if (ModalChrome.HasRequiredAssets)
            {
                _chrome = new ModalChrome(
                    Mathf.Max(320f, LayoutSize.X - ChromeHorizontalExtras),
                    bodyTopPadding: 4f,
                    bodyBottomPadding: 20f);
                _chrome.Name = "AdaptiveModalChrome";
                _chrome.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                _chrome.SizeFlagsVertical = SizeFlags.ExpandFill;
                AddChild(_chrome);
                Header = _chrome.HeaderHost;
                BodyScroll = _chrome.BodyScroll;
                Body = _chrome.Body;
                Footer = _chrome.FooterHost;
                Body.AddThemeConstantOverride("separation", 12);
                Header.SetMeta("modal_role", "header");
                BodyScroll.SetMeta("modal_role", "body_scroll");
                Body.SetMeta("modal_role", "body");
                Footer.SetMeta("modal_role", "footer");
                Footer.Visible = false;
            }
            else
            {
                BuildLegacyRegions();
            }
        }
        SetMeta("modal_profile", profile.ToString());
        SetMeta("modal_width", LayoutSize.X);
        SetMeta("modal_height", LayoutSize.Y);
        SetMeta("modal_image_chrome", UsesImageChrome);
        SetMeta("modal_content_layout", contentLayout.ToString());
        Body.MinimumSizeChanged += OnBodyMinimumSizeChanged;
        RequestContentFit();
    }

    public void SetContentLayout(ContentLayout contentLayout)
    {
        _contentLayout = contentLayout;
        SetMeta("modal_content_layout", contentLayout.ToString());
        RequestContentFit();
    }

    public void RequestContentFit()
    {
        if (_contentLayout == ContentLayout.Fixed || !IsInsideTree()) return;
        _pendingFitPasses = 2;
        if (_fitQueued) return;
        _fitQueued = true;
        CallDeferred(nameof(ApplyContentFit));
    }

    private void OnBodyMinimumSizeChanged()
    {
        RequestContentFit();
    }

    private void ApplyContentFit()
    {
        _fitQueued = false;
        if (_contentLayout == ContentLayout.Fixed || Body is null) return;

        var availableWidth = Mathf.Max(320f, _viewportSize.X - ViewportMargin * 2f);
        var availableHeight = Mathf.Max(320f, _viewportSize.Y - ViewportMargin * 2f);
        var bodyMinimum = Body.GetCombinedMinimumSize();
        var minimumContentWidth = _contentLayout switch
        {
            ContentLayout.Empty => 520f,
            ContentLayout.Table => _widthProfile == WidthProfile.Wide ? 920f : 700f,
            ContentLayout.Cards => 780f,
            ContentLayout.Mixed => 900f,
            _ => 700f,
        };
        var measuredContentWidth = Mathf.Max(minimumContentWidth,
            bodyMinimum.X + ScrollBarAllowance);
        var desiredWidth = Mathf.Round(Mathf.Min(availableWidth,
            measuredContentWidth + ChromeHorizontalExtras));
        if (_chrome is not null)
            _chrome.SetContentWidth(desiredWidth - ChromeHorizontalExtras);

        var chromeHeight = _chrome?.HeaderFooterHeight ?? 104f;
        var minimumBodyHeight = _contentLayout switch
        {
            ContentLayout.Empty => 84f,
            ContentLayout.Table => 176f,
            ContentLayout.Cards => 190f,
            ContentLayout.Mixed => 250f,
            _ => 220f,
        };
        var maximumBodyHeight = Mathf.Max(minimumBodyHeight,
            availableHeight - chromeHeight);
        var desiredBodyHeight = Mathf.Clamp(
            bodyMinimum.Y + 36f,
            minimumBodyHeight,
            maximumBodyHeight);
        var desiredHeight = Mathf.Min(availableHeight, chromeHeight + desiredBodyHeight);

        LayoutSize = new Vector2(desiredWidth, Mathf.Round(desiredHeight));
        CustomMinimumSize = LayoutSize;
        SetMeta("modal_width", LayoutSize.X);
        SetMeta("modal_height", LayoutSize.Y);
        SetMeta("modal_measured_body_height", bodyMinimum.Y);
        SetMeta("modal_measured_body_width", bodyMinimum.X);
        if (_pendingFitPasses-- > 0)
        {
            _fitQueued = true;
            CallDeferred(nameof(ApplyContentFit));
        }
    }

    public void SetHeader(Control title, BaseButton closeButton, Control? separator = null)
    {
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.SizeFlagsVertical = SizeFlags.ShrinkCenter;
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
        control.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        control.SetMeta("modal_role", "footer_action");
        Footer.AddChild(control);
    }

    public static Vector2 CalculateLayoutSize(
        Vector2 viewportSize,
        WidthProfile profile,
        ContentLayout contentLayout = ContentLayout.Fixed)
    {
        var availableWidth = Mathf.Max(320f, viewportSize.X - ViewportMargin * 2f);
        var availableHeight = Mathf.Max(320f, viewportSize.Y - ViewportMargin * 2f);
        var widthRatio = contentLayout switch
        {
            ContentLayout.Empty => 0.46f,
            ContentLayout.Table when profile == WidthProfile.Wide => 0.82f,
            ContentLayout.Table => 0.64f,
            ContentLayout.Cards => 0.72f,
            ContentLayout.Mixed => 0.82f,
            _ => profile switch
            {
                WidthProfile.Compact => 0.52f,
                WidthProfile.Standard => 0.73f,
                _ => 0.90f,
            },
        };
        var minimumWidth = contentLayout switch
        {
            ContentLayout.Empty => 520f,
            ContentLayout.Table when profile == WidthProfile.Wide => 960f,
            ContentLayout.Table => 720f,
            ContentLayout.Cards => 820f,
            ContentLayout.Mixed => 960f,
            _ => profile switch
            {
                WidthProfile.Compact => 420f,
                WidthProfile.Standard => 620f,
                _ => 820f,
            },
        };
        var width = Mathf.Min(Mathf.Max(viewportSize.X * widthRatio, minimumWidth), availableWidth);
        var height = Mathf.Min(Mathf.Max(viewportSize.Y * HeightRatio, 420f), availableHeight);
        return new Vector2(Mathf.Round(width), Mathf.Round(height));
    }

    private static StyleBoxFlat TransparentPanelStyle() => new()
    {
        BgColor = Colors.Transparent,
        BorderColor = Colors.Transparent,
        BorderWidthLeft = 0,
        BorderWidthTop = 0,
        BorderWidthRight = 0,
        BorderWidthBottom = 0,
        ContentMarginLeft = 0,
        ContentMarginTop = 0,
        ContentMarginRight = 0,
        ContentMarginBottom = 0,
    };
}
