using Godot;

namespace SanguoSLG.Game;

public sealed partial class EditMapScene
{
    private Control? _mapConfirmation;

    private void ShowUnsavedMapConfirmation(Action transition)
    {
        ShowMapConfirmation("저장하지 않은 변경 사항",
            "변경한 지도 데이터가 아직 저장되지 않았습니다.\n어떻게 진행하시겠습니까?",
            [
                ("계속 편집", false, CloseMapConfirmation),
                ("변경 버리기", false, () =>
                {
                    _draftChanged = false;
                    if (_saveDraftButton is not null) _saveDraftButton.Text = "맵 저장";
                    CloseMapConfirmation();
                    transition();
                }),
                ("저장 후 이동", true, () =>
                {
                    CloseMapConfirmation();
                    if (TrySaveDraft(false)) transition();
                }),
            ]);
    }

    private void ShowMapConfirmation(string title, string message,
        IReadOnlyList<(string Text, bool Primary, Action Action)> actions)
    {
        if (_mapConfirmation is not null) return;
        CancelSiteDrag();
        ClearSiteHover();
        var overlay = new ColorRect
        {
            Name = "MapConfirmationOverlay",
            Color = new Color(0f, 0f, 0f, 0.68f),
            MouseFilter = MouseFilterEnum.Stop,
            FocusMode = FocusModeEnum.All,
        };
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        overlay.GuiInput += input =>
        {
            if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
            {
                CloseMapConfirmation();
                GetViewport().SetInputAsHandled();
            }
        };
        AddChild(overlay);
        MoveChild(overlay, GetChildCount() - 1);
        _mapConfirmation = overlay;

        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        center.MouseFilter = MouseFilterEnum.Ignore;
        overlay.AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(520, 0) };
        panel.AddThemeStyleboxOverride("panel", ConfirmationFrame());
        center.AddChild(panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 14);
        panel.AddChild(content);
        var header = new HBoxContainer();
        content.AddChild(header);
        var heading = new Label { Text = title, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        heading.AddThemeFontSizeOverride("font_size", 20);
        heading.AddThemeColorOverride("font_color", new Color("f1d39a"));
        header.AddChild(heading);
        var close = new Button { Text = "×", CustomMinimumSize = new Vector2(38, 34), TooltipText = "계속 편집" };
        close.Pressed += CloseMapConfirmation;
        header.AddChild(close);
        content.AddChild(new HSeparator());
        var body = new Label
        {
            Text = message,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(460, 64),
            VerticalAlignment = VerticalAlignment.Center,
        };
        body.AddThemeFontSizeOverride("font_size", 15);
        body.AddThemeColorOverride("font_color", new Color("ead8b8"));
        content.AddChild(body);
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 10);
        content.AddChild(buttons);
        foreach (var item in actions)
        {
            var button = new Button { Text = item.Text, CustomMinimumSize = new Vector2(126, 40) };
            if (item.Primary) ApplyPrimaryConfirmationStyle(button);
            button.Pressed += item.Action;
            buttons.AddChild(button);
        }
        overlay.GrabFocus();
    }

    private void CloseMapConfirmation()
    {
        _mapConfirmation?.QueueFree();
        _mapConfirmation = null;
    }

    private static StyleBoxFlat ConfirmationFrame()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color("1b0f0d"),
            BorderColor = new Color("b58b49"),
        };
        style.SetBorderWidthAll(2);
        style.SetCornerRadiusAll(8);
        style.SetContentMarginAll(22);
        return style;
    }

    private static void ApplyPrimaryConfirmationStyle(Button button)
    {
        button.AddThemeStyleboxOverride("normal", ConfirmationButtonStyle("61251c", "d7a45a"));
        button.AddThemeStyleboxOverride("hover", ConfirmationButtonStyle("873328", "ffd17a"));
        button.AddThemeStyleboxOverride("pressed", ConfirmationButtonStyle("461713", "c68d42"));
        button.AddThemeStyleboxOverride("focus", ConfirmationButtonStyle("61251c", "f1c878", 2));
        button.AddThemeColorOverride("font_color", new Color("ffdda1"));
    }

    private static StyleBoxFlat ConfirmationButtonStyle(string background, string border, int width = 1)
    {
        var style = new StyleBoxFlat { BgColor = new Color(background), BorderColor = new Color(border) };
        style.SetBorderWidthAll(width);
        style.SetCornerRadiusAll(6);
        return style;
    }
}
