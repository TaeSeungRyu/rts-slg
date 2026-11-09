using Godot;

namespace SanguoSLG.Game;

public sealed partial class ScenarioEditorScene
{
    private Control? _scenarioModal;
    private bool _scenarioModalActionRunning;

    private enum ScenarioModalTone { Normal, Primary, Danger }

    private readonly record struct ScenarioModalAction(
        string Text, ScenarioModalTone Tone, Func<bool> Execute, bool SafeFocus = false);

    private void ShowScenarioModal(string title, string message, string? guidance,
        IReadOnlyList<ScenarioModalAction> actions)
    {
        if (_scenarioModal is not null) return;
        var overlay = new ColorRect
        {
            Name = "ScenarioEditorModalOverlay",
            Color = new Color(0f, 0f, 0f, 0.68f),
            MouseFilter = MouseFilterEnum.Stop,
            FocusMode = FocusModeEnum.All,
        };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(overlay);
        MoveChild(overlay, GetChildCount() - 1);
        _scenarioModal = overlay;

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        overlay.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(520, 0) };
        panel.AddThemeStyleboxOverride("panel", ScenarioModalFrame());
        center.AddChild(panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 14);
        panel.AddChild(content);

        var header = new HBoxContainer();
        content.AddChild(header);
        var heading = new Label { Text = title, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center };
        heading.AddThemeFontSizeOverride("font_size", 20);
        heading.AddThemeColorOverride("font_color", new Color("f1d39a"));
        header.AddChild(heading);
        var close = new Button { Text = "×", TooltipText = "계속 편집",
            CustomMinimumSize = new Vector2(38, 34), FocusMode = FocusModeEnum.None };
        close.Pressed += CloseScenarioModal;
        header.AddChild(close);
        content.AddChild(new HSeparator());

        var body = new Label { Text = message, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(460, 58) };
        body.AddThemeFontSizeOverride("font_size", 15);
        body.AddThemeColorOverride("font_color", new Color("ead8b8"));
        content.AddChild(body);
        if (!string.IsNullOrWhiteSpace(guidance))
        {
            var hint = new Label { Text = guidance, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            hint.AddThemeFontSizeOverride("font_size", 13);
            hint.AddThemeColorOverride("font_color", new Color("bd9d70"));
            content.AddChild(hint);
        }
        var status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        status.AddThemeColorOverride("font_color", new Color("e39a72"));
        content.AddChild(status);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 10);
        content.AddChild(buttons);
        Button? safeFocus = null;
        foreach (var action in actions)
        {
            var button = new Button { Text = action.Text, CustomMinimumSize = new Vector2(126, 40) };
            ApplyScenarioModalButtonStyle(button, action.Tone);
            button.Pressed += () =>
            {
                if (_scenarioModalActionRunning) return;
                _scenarioModalActionRunning = true;
                var completed = false;
                try { completed = action.Execute(); }
                catch (Exception error)
                {
                    status.Text = "작업 실패: " + error.Message;
                    GD.PushError(error.ToString());
                }
                finally { _scenarioModalActionRunning = false; }
                if (completed) CloseScenarioModal();
                else if (string.IsNullOrWhiteSpace(status.Text))
                    status.Text = string.IsNullOrWhiteSpace(_message.Text)
                        ? "작업을 완료하지 못했습니다." : _message.Text;
            };
            buttons.AddChild(button);
            if (action.SafeFocus) safeFocus = button;
        }

        overlay.GuiInput += input =>
        {
            if (input is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape }) return;
            CloseScenarioModal();
            GetViewport().SetInputAsHandled();
        };
        overlay.GrabFocus();
        if (safeFocus is not null) safeFocus.CallDeferred(Control.MethodName.GrabFocus);
    }

    private void CloseScenarioModal()
    {
        _scenarioModal?.QueueFree();
        _scenarioModal = null;
        _scenarioModalActionRunning = false;
    }

    private static StyleBoxFlat ScenarioModalFrame()
    {
        var style = new StyleBoxFlat { BgColor = new Color("1b0f0d"), BorderColor = new Color("b58b49") };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(8);
        style.SetContentMarginAll(22);
        return style;
    }

    private static void ApplyScenarioModalButtonStyle(Button button, ScenarioModalTone tone)
    {
        var colors = tone switch
        {
            ScenarioModalTone.Primary => ("61251c", "873328", "461713", "d7a45a", "ffd17a"),
            ScenarioModalTone.Danger => ("49201b", "702a23", "351411", "b96b55", "e49a78"),
            _ => ("241a16", "38251c", "18100e", "b48a49", "e0b86b"),
        };
        button.AddThemeStyleboxOverride("normal", ScenarioModalButtonBox(colors.Item1, colors.Item4));
        button.AddThemeStyleboxOverride("hover", ScenarioModalButtonBox(colors.Item2, colors.Item5, 2));
        button.AddThemeStyleboxOverride("pressed", ScenarioModalButtonBox(colors.Item3, colors.Item4));
        button.AddThemeStyleboxOverride("focus", ScenarioModalButtonBox(colors.Item1, colors.Item5, 2));
        button.AddThemeStyleboxOverride("disabled", ScenarioModalButtonBox("211917", "604c3a"));
        button.AddThemeColorOverride("font_color", new Color("efd5a3"));
        button.AddThemeColorOverride("font_hover_color", new Color("ffe8b5"));
        button.AddThemeColorOverride("font_disabled_color", new Color("8d7a65"));
    }

    private static StyleBoxFlat ScenarioModalButtonBox(string background, string border, int width = 1)
    {
        var style = new StyleBoxFlat { BgColor = new Color(background), BorderColor = new Color(border) };
        style.SetBorderWidthAll(width);
        style.SetCornerRadiusAll(6);
        style.SetContentMarginAll(8);
        return style;
    }
}
