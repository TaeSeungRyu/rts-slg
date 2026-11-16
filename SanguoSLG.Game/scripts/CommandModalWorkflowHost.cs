using System.Collections.Generic;
using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 단계 표시, 목록/상세 2단 작업공간, 고정 요약/이동 영역을 제공하는 명령 모달 호스트.
/// 모든 단계 노드는 유지하고 Visible만 전환해 입력 상태를 보존한다.
/// </summary>
public sealed partial class CommandModalWorkflowHost : VBoxContainer
{
    public sealed record StepPage(VBoxContainer List, VBoxContainer Detail);

    private static readonly Color Gold = new("c9973f");
    private static readonly Color GoldBright = new("f1cc72");
    private readonly HBoxContainer _steps;
    private readonly Control _pages;
    private readonly Label _summary;
    private readonly Button _previous;
    private readonly Button _next;
    private readonly ButtonGroup _group = new() { AllowUnpress = false };
    private readonly List<Button> _stepButtons = [];
    private readonly List<Control> _stepPages = [];
    private readonly List<string> _stepTitles = [];
    private readonly List<string> _stepResults = [];

    public int SelectedStep { get; private set; }
    public int StepCount => _stepButtons.Count;

    public CommandModalWorkflowHost()
    {
        Name = "CommandModalWorkflowHost";
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddThemeConstantOverride("separation", 8);
        SetMeta("command_modal_workflow", true);

        _steps = new HBoxContainer
        {
            Name = "CommandModalWorkflowSteps",
            CustomMinimumSize = new Vector2(0f, 52f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _steps.AddThemeConstantOverride("separation", 6);
        AddChild(_steps);

        _pages = new Control
        {
            Name = "CommandModalWorkflowPages",
            ClipContents = true,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        AddChild(_pages);

        var footer = new PanelContainer
        {
            Name = "CommandModalWorkflowFooter",
            CustomMinimumSize = new Vector2(0f, 54f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        footer.AddThemeStyleboxOverride("panel", PanelStyle(new Color("24120f"), Gold, 1));
        AddChild(footer);
        var footerRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        footerRow.AddThemeConstantOverride("separation", 8);
        footer.AddChild(footerRow);
        _summary = new Label
        {
            Name = "CommandModalWorkflowSummary",
            Text = "선택 내용을 확인하세요.",
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _summary.AddThemeColorOverride("font_color", new Color("eadbb5"));
        _summary.AddThemeFontSizeOverride("font_size", 14);
        footerRow.AddChild(_summary);
        _previous = NavigationButton("이전");
        _previous.Pressed += () => SelectStep(SelectedStep - 1);
        footerRow.AddChild(_previous);
        _next = NavigationButton("다음 단계");
        _next.Pressed += () => SelectStep(SelectedStep + 1);
        footerRow.AddChild(_next);
    }

    public StepPage AddStep(string title, string code, string guidance)
    {
        var index = _stepButtons.Count;
        _stepTitles.Add(title);
        _stepResults.Add("");
        var step = new Button
        {
            Name = $"CommandModalStep_{code}",
            Text = StepText(index),
            ToggleMode = true,
            ButtonGroup = _group,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(150f, 50f),
        };
        StyleStepButton(step);
        step.Pressed += () => SelectStep(index);
        _steps.AddChild(step);
        _stepButtons.Add(step);

        var columns = new HBoxContainer
        {
            Name = $"CommandModalWorkflowPage_{code}",
            Visible = index == 0,
        };
        columns.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        columns.AddThemeConstantOverride("separation", 12);
        _pages.AddChild(columns);
        _stepPages.Add(columns);

        var listPanel = new PanelContainer
        {
            Name = $"CommandModalWorkflowListPanel_{code}",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 2.2f,
        };
        listPanel.AddThemeStyleboxOverride("panel", PanelStyle(new Color("0f0908"), new Color("6f4b22"), 1));
        columns.AddChild(listPanel);
        var listScroll = new ScrollContainer
        {
            Name = $"CommandModalWorkflowListScroll_{code}",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        listPanel.AddChild(listScroll);
        var listMargin = ContentMargin(10);
        listScroll.AddChild(listMargin);
        var list = ContentBox($"CommandModalWorkflowList_{code}");
        listMargin.AddChild(list);

        var detailPanel = new PanelContainer
        {
            Name = $"CommandModalWorkflowDetailPanel_{code}",
            CustomMinimumSize = new Vector2(300f, 0f),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = 1f,
        };
        detailPanel.AddThemeStyleboxOverride("panel", PanelStyle(new Color("21110e"), Gold, 1));
        columns.AddChild(detailPanel);
        var detailMargin = ContentMargin(14);
        detailPanel.AddChild(detailMargin);
        var detail = ContentBox($"CommandModalWorkflowDetail_{code}");
        detail.AddChild(SectionLabel(title));
        var guidanceLabel = new Label
        {
            Text = guidance,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        guidanceLabel.AddThemeColorOverride("font_color", new Color("d8c9a9"));
        guidanceLabel.AddThemeFontSizeOverride("font_size", 14);
        detail.AddChild(guidanceLabel);
        detailMargin.AddChild(detail);

        if (index == 0) step.ButtonPressed = true;
        UpdateNavigation();
        return new StepPage(list, detail);
    }

    public void SetStepResult(int index, string result)
    {
        if (index < 0 || index >= _stepButtons.Count) return;
        _stepResults[index] = result;
        _stepButtons[index].Text = StepText(index);
        _stepButtons[index].TooltipText = result;
        if (index == SelectedStep)
            _summary.Text = string.IsNullOrWhiteSpace(result) ? "선택 내용을 확인하세요." : result;
    }

    public void SelectStep(int index)
    {
        if (index < 0 || index >= _stepPages.Count) return;
        SelectedStep = index;
        for (var i = 0; i < _stepPages.Count; i++)
        {
            var selected = i == index;
            _stepPages[i].Visible = selected;
            _stepButtons[i].ButtonPressed = selected;
        }
        _summary.Text = string.IsNullOrWhiteSpace(_stepResults[index])
            ? index == _stepPages.Count - 1
                ? "수행 장수를 선택하면 기존 명령 확인 절차가 시작됩니다."
                : "선택 내용을 확인한 뒤 다음 단계로 이동하세요."
            : _stepResults[index];
        UpdateNavigation();
        SetMeta("selected_command_modal_step", index);
    }

    private void UpdateNavigation()
    {
        _previous.Visible = SelectedStep > 0;
        _next.Visible = SelectedStep < _stepPages.Count - 1;
        _previous.Text = "이전";
        _next.Text = "다음 단계";
        _previous.QueueRedraw();
        _next.QueueRedraw();
    }

    private string StepText(int index) => string.IsNullOrWhiteSpace(_stepResults[index])
        ? $"{index + 1}. {_stepTitles[index]}"
        : $"{index + 1}. {_stepTitles[index]}  ✓  {_stepResults[index]}";

    private static Button NavigationButton(string text)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(120f, 38f) };
        button.AddThemeColorOverride("font_color", GoldBright);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        button.AddThemeColorOverride("font_focus_color", GoldBright);
        button.AddThemeStyleboxOverride("normal", PanelStyle(new Color("321410"), Gold, 1));
        button.AddThemeStyleboxOverride("hover", PanelStyle(new Color("531b15"), GoldBright, 2));
        button.AddThemeStyleboxOverride("pressed", PanelStyle(new Color("6b2018"), GoldBright, 2));
        return button;
    }

    private static void StyleStepButton(Button button)
    {
        button.AddThemeColorOverride("font_color", new Color("c8a86b"));
        button.AddThemeColorOverride("font_pressed_color", Colors.White);
        button.AddThemeColorOverride("font_hover_color", GoldBright);
        button.AddThemeFontSizeOverride("font_size", 15);
        button.AddThemeStyleboxOverride("normal", PanelStyle(new Color("1d0e0c"), new Color("725029"), 1));
        button.AddThemeStyleboxOverride("hover", PanelStyle(new Color("361511"), Gold, 1));
        button.AddThemeStyleboxOverride("pressed", PanelStyle(new Color("5a1712"), GoldBright, 2));
        button.AddThemeStyleboxOverride("focus", PanelStyle(new Color("5a1712"), GoldBright, 2));
    }

    private static MarginContainer ContentMargin(int value)
    {
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_left", value);
        margin.AddThemeConstantOverride("margin_top", value);
        margin.AddThemeConstantOverride("margin_right", value);
        margin.AddThemeConstantOverride("margin_bottom", value);
        return margin;
    }

    private static VBoxContainer ContentBox(string name)
    {
        var box = new VBoxContainer { Name = name, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 10);
        return box;
    }

    private static Label SectionLabel(string text)
    {
        var label = new Label { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        label.AddThemeColorOverride("font_color", GoldBright);
        label.AddThemeFontSizeOverride("font_size", 18);
        return label;
    }

    private static StyleBoxFlat PanelStyle(Color background, Color border, int width) => new()
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
        ContentMarginLeft = 10,
        ContentMarginRight = 10,
        ContentMarginTop = 8,
        ContentMarginBottom = 8,
    };
}
