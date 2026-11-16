using System.Collections.Generic;
using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 명령 모달 전용 탭 작업공간. 각 페이지 노드는 계속 유지하고 표시만 전환해
/// 카드·비율·장수 선택 상태가 탭 이동 중 사라지지 않게 한다.
/// </summary>
public sealed partial class CommandModalTabHost : VBoxContainer
{
    private static readonly Color Gold = new("c9973f");
    private static readonly Color GoldBright = new("f1cc72");
    private static readonly Color Ink = new("160b09");
    private static readonly Color Wine = new("4d120f");

    private readonly HBoxContainer _tabBar;
    private readonly Control _pageViewport;
    private readonly ButtonGroup _buttonGroup = new() { AllowUnpress = false };
    private readonly List<Button> _buttons = [];
    private readonly List<ScrollContainer> _pageScrolls = [];

    public int SelectedTab { get; private set; }
    public int TabCount => _buttons.Count;

    public CommandModalTabHost()
    {
        Name = "CommandModalTabHost";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 8);
        SetMeta("command_modal_tabs", true);

        _tabBar = new HBoxContainer
        {
            Name = "CommandModalTabBar",
            CustomMinimumSize = new Vector2(0f, 42f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _tabBar.AddThemeConstantOverride("separation", 6);
        AddChild(_tabBar);

        _pageViewport = new Control
        {
            Name = "CommandModalTabPages",
            ClipContents = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        AddChild(_pageViewport);
    }

    public VBoxContainer AddPage(string title, string code)
    {
        var index = _buttons.Count;
        var button = new Button
        {
            Name = $"CommandModalTab_{code}",
            Text = title,
            ToggleMode = true,
            ButtonGroup = _buttonGroup,
            FocusMode = FocusModeEnum.All,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(120f, 40f),
        };
        ApplyTabStyle(button);
        button.Pressed += () => SelectTab(index);
        _buttons.Add(button);
        _tabBar.AddChild(button);

        var scroll = new ScrollContainer
        {
            Name = $"CommandModalTabScroll_{code}",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            Visible = index == 0,
        };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        scroll.SetMeta("command_modal_tab_page", code);
        _pageViewport.AddChild(scroll);
        _pageScrolls.Add(scroll);

        var margin = new MarginContainer
        {
            Name = $"CommandModalTabMargin_{code}",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        margin.AddThemeConstantOverride("margin_right", 8);
        scroll.AddChild(margin);

        var page = new VBoxContainer
        {
            Name = $"CommandModalTabPage_{code}",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        page.AddThemeConstantOverride("separation", 12);
        margin.AddChild(page);

        if (index == 0)
            button.ButtonPressed = true;
        return page;
    }

    public void SelectTab(int index)
    {
        if (index < 0 || index >= _pageScrolls.Count) return;
        SelectedTab = index;
        for (var i = 0; i < _pageScrolls.Count; i++)
        {
            var selected = i == index;
            _pageScrolls[i].Visible = selected;
            _buttons[i].ButtonPressed = selected;
        }
        SetMeta("selected_command_modal_tab", index);
    }

    private static void ApplyTabStyle(Button button)
    {
        button.AddThemeColorOverride("font_color", GoldBright);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeStyleboxOverride("normal", TabStyle(new Color("23120f"), Gold, 1));
        button.AddThemeStyleboxOverride("hover", TabStyle(new Color("3b1914"), GoldBright, 2));
        button.AddThemeStyleboxOverride("pressed", TabStyle(Wine, GoldBright, 2));
        button.AddThemeStyleboxOverride("focus", TabStyle(Wine, GoldBright, 2));
        button.AddThemeStyleboxOverride("disabled", TabStyle(Ink, new Color("6d5530"), 1));
    }

    private static StyleBoxFlat TabStyle(Color background, Color border, int width) => new()
    {
        BgColor = background,
        BorderColor = border,
        BorderWidthLeft = width,
        BorderWidthTop = width,
        BorderWidthRight = width,
        BorderWidthBottom = width,
        CornerRadiusTopLeft = 5,
        CornerRadiusTopRight = 5,
        CornerRadiusBottomLeft = 5,
        CornerRadiusBottomRight = 5,
        ContentMarginLeft = 12,
        ContentMarginRight = 12,
        ContentMarginTop = 8,
        ContentMarginBottom = 8,
    };
}
