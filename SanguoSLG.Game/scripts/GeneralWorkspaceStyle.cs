namespace SanguoSLG.Game;

using Godot;

internal static class GeneralWorkspaceStyle
{
    internal static Theme Theme()
    {
        var theme = new Theme();
        theme.DefaultFont = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf");
        theme.DefaultFontSize = 15;
        var gold = new Color(0.94f, 0.78f, 0.43f);
        var parchment = new Color(0.96f, 0.91f, 0.79f);
        theme.SetColor("font_color", "Label", parchment);
        foreach (var type in new[] { "Button", "OptionButton" })
        {
            theme.SetStylebox("normal", type, Box(new Color(0.25f, 0.10f, 0.08f), gold.Darkened(0.30f), 1));
            theme.SetStylebox("hover", type, Box(new Color(0.48f, 0.20f, 0.11f), gold, 2));
            theme.SetStylebox("pressed", type, Box(new Color(0.63f, 0.38f, 0.16f), gold.Lightened(0.15f), 2));
            theme.SetStylebox("focus", type, Box(new Color(0, 0, 0, 0), gold, 1));
            theme.SetStylebox("disabled", type, Box(new Color(0.17f, 0.14f, 0.13f), new Color(gold, 0.30f), 1));
            theme.SetColor("font_color", type, parchment);
            theme.SetColor("font_hover_color", type, Color.Color8(255, 239, 190));
            theme.SetColor("font_pressed_color", type, Color.Color8(255, 248, 219));
            theme.SetColor("font_disabled_color", type, new Color(parchment, 0.45f));
            theme.SetFontSize("font_size", type, 15);
        }
        theme.SetStylebox("panel", "ItemList", Box(new Color(0.08f, 0.07f, 0.065f), new Color(gold, 0.55f), 1));
        theme.SetStylebox("selected", "ItemList", Box(new Color(0.39f, 0.20f, 0.10f), gold, 1));
        theme.SetStylebox("selected_focus", "ItemList", Box(new Color(0.46f, 0.23f, 0.11f), gold, 1));
        theme.SetStylebox("hover", "ItemList", Box(new Color(0.25f, 0.13f, 0.09f), new Color(gold, 0.45f), 1));
        theme.SetStylebox("hover_selected", "ItemList", Box(new Color(0.48f, 0.24f, 0.11f), gold, 1));
        theme.SetColor("font_color", "ItemList", parchment);
        theme.SetColor("font_selected_color", "ItemList", Color.Color8(255, 241, 195));
        theme.SetStylebox("tab_selected", "TabBar", Box(new Color(0.42f, 0.21f, 0.10f), gold, 2));
        theme.SetStylebox("tab_unselected", "TabBar", Box(new Color(0.14f, 0.09f, 0.08f), new Color(gold, 0.40f), 1));
        theme.SetStylebox("tab_hovered", "TabBar", Box(new Color(0.31f, 0.15f, 0.09f), gold, 1));
        theme.SetColor("font_selected_color", "TabBar", Color.Color8(255, 239, 190));
        theme.SetColor("font_unselected_color", "TabBar", parchment);
        return theme;
    }

    internal static void StyleOptionButton(OptionButton option, int maximumPopupHeight = 300)
    {
        var popup = option.GetPopup();
        popup.MaxSize = new Vector2I(4096, maximumPopupHeight);
        popup.AddThemeStyleboxOverride("panel", Box(new Color("211410"), new Color("a47b42"), 1));
        popup.AddThemeStyleboxOverride("hover", Box(new Color("60331f"), new Color("d4a85d"), 1));
        popup.AddThemeColorOverride("font_color", new Color("ead3ac"));
        popup.AddThemeColorOverride("font_hover_color", new Color("ffe1a8"));
        popup.AddThemeColorOverride("font_disabled_color", new Color("8f7b65"));
        popup.AddThemeColorOverride("font_accelerator_color", new Color("b78a4a"));
        popup.AddThemeConstantOverride("v_separation", 6);
        popup.AddThemeConstantOverride("item_start_padding", 10);
        popup.AddThemeConstantOverride("item_end_padding", 12);
        StylePopupScrollBars(popup);
        popup.AboutToPopup += async () =>
        {
            await option.ToSignal(option.GetTree(), SceneTree.SignalName.ProcessFrame);
            StylePopupScrollBars(popup);
        };
    }

    internal static void StyleScrollContainer(ScrollContainer scroll, int thickness = 7)
    {
        scroll.AddThemeConstantOverride("scrollbar_v_separation", 10);
        StyleScrollBar(scroll.GetVScrollBar(), true, thickness);
        StyleScrollBar(scroll.GetHScrollBar(), false, thickness);
    }

    internal static void StyleVerticalScrollBar(VScrollBar bar, int thickness = 7)
        => StyleScrollBar(bar, true, thickness);

    internal static void StyleTextInput(Control input)
    {
        var normal = Box(new Color("35211b"), new Color("765637"), 1);
        var focus = Box(new Color("3c241d"), new Color("d1a15a"), 2);
        input.AddThemeStyleboxOverride("normal", normal);
        input.AddThemeStyleboxOverride("focus", focus);
        input.AddThemeColorOverride("font_color", new Color("f0d6a8"));
        input.AddThemeColorOverride("font_placeholder_color", new Color("8f7b65"));
    }

    private static void StyleScrollBar(ScrollBar bar, bool vertical, int thickness)
    {
        bar.CustomMinimumSize = vertical ? new Vector2(thickness, 0) : new Vector2(0, thickness);
        bar.AddThemeStyleboxOverride("scroll", ScrollBox("241713"));
        bar.AddThemeStyleboxOverride("grabber", ScrollBox("8c683b", "765637"));
        bar.AddThemeStyleboxOverride("grabber_highlight", ScrollBox("c6a05b", "d1a15a"));
        bar.AddThemeStyleboxOverride("grabber_pressed", ScrollBox("d9b66e", "d1a15a"));
    }

    internal static void StylePopupScrollBars(PopupMenu popup)
    {
        foreach (var scroll in popup.FindChildren("*", "ScrollContainer", true, false).OfType<ScrollContainer>())
            scroll.AddThemeConstantOverride("scrollbar_v_separation", 8);
        foreach (var bar in popup.FindChildren("*", "VScrollBar", true, false).OfType<VScrollBar>())
        {
            bar.CustomMinimumSize = new Vector2(8, 0);
            bar.AddThemeStyleboxOverride("scroll", ScrollBox("302019"));
            bar.AddThemeStyleboxOverride("scroll_focus", ScrollBox("302019", "a47b42"));
            bar.AddThemeStyleboxOverride("grabber", ScrollBox("b78a4a", "a47b42"));
            bar.AddThemeStyleboxOverride("grabber_highlight", ScrollBox("d4a85d", "d4a85d"));
            bar.AddThemeStyleboxOverride("grabber_pressed", ScrollBox("e2ba75", "e2ba75"));
        }
    }

    private static StyleBoxFlat ScrollBox(string background, string border = "00000000")
    {
        var style = new StyleBoxFlat { BgColor = new Color(background), BorderColor = new Color(border) };
        style.SetBorderWidthAll(border == "00000000" ? 0 : 1);
        style.SetCornerRadiusAll(4);
        return style;
    }

    internal static void AddBackground(Control scene)
    {
        scene.AddChild(new ColorRect
        {
            Color = new Color(0.055f, 0.060f, 0.060f),
            AnchorRight = 1,
            AnchorBottom = 1,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        scene.AddChild(new ColorRect
        {
            Color = new Color(0.78f, 0.57f, 0.28f),
            AnchorRight = 1,
            OffsetBottom = 3,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
    }

    internal static void Header(VBoxContainer root, string title, string subtitle)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", SectionBox());
        root.AddChild(panel);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 18);
        panel.AddChild(row);
        var mark = new Label { Text = "三國", CustomMinimumSize = new Vector2(65, 0),
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        mark.AddThemeFontSizeOverride("font_size", 23);
        mark.AddThemeColorOverride("font_color", new Color("f1c878"));
        row.AddChild(mark);
        row.AddChild(new VSeparator());
        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(text);
        var heading = new Label { Text = title };
        heading.AddThemeFontSizeOverride("font_size", 23);
        text.AddChild(heading);
        var caption = new Label { Text = subtitle };
        caption.AddThemeFontSizeOverride("font_size", 13);
        caption.AddThemeColorOverride("font_color", new Color("c4ae88"));
        text.AddChild(caption);
    }

    internal static VBoxContainer Column(Container parent, string title, int minimumWidth = 0)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(minimumWidth, 0),
            SizeFlagsHorizontal = minimumWidth > 0 ? Control.SizeFlags.Fill : Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", SectionBox());
        parent.AddChild(panel);
        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 9);
        panel.AddChild(content);
        var heading = new Label { Text = title };
        heading.AddThemeFontSizeOverride("font_size", 17);
        heading.AddThemeColorOverride("font_color", new Color("f1c878"));
        content.AddChild(heading);
        content.AddChild(new HSeparator());
        return content;
    }

    internal static HBoxContainer Footer(VBoxContainer root)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", SectionBox());
        root.AddChild(panel);
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 40) };
        row.AddThemeConstantOverride("separation", 12);
        panel.AddChild(row);
        return row;
    }

    internal static HBoxContainer EditorFooter(VBoxContainer root, Label status)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", SectionBox());
        root.AddChild(panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 5);
        panel.AddChild(content);
        status.CustomMinimumSize = new Vector2(0, 22);
        status.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        status.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        content.AddChild(status);
        content.AddChild(new HSeparator());
        var actions = new HBoxContainer { Name = "EditorFooterActions",
            CustomMinimumSize = new Vector2(0, 38) };
        actions.AddThemeConstantOverride("separation", 10);
        content.AddChild(actions);
        return actions;
    }

    internal static bool EditorFooterLayoutValid(Control scene, Label status)
    {
        var actions = scene.FindChild("EditorFooterActions", true, false) as HBoxContainer;
        return actions is not null && ReferenceEquals(actions.GetParent(), status.GetParent())
            && actions.GetChildCount() >= 2
            && !actions.GetChildren().Contains(status);
    }

    private static StyleBoxFlat Box(Color background, Color border, int borderWidth)
        => new()
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = 7,
            CornerRadiusTopRight = 7,
            CornerRadiusBottomLeft = 7,
            CornerRadiusBottomRight = 7,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 9,
            ContentMarginBottom = 9,
        };

    internal static VBoxContainer Section(VBoxContainer parent, string title)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        panel.AddThemeStyleboxOverride("panel", SectionBox());
        parent.AddChild(panel);

        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 5);
        panel.AddChild(body);
        var heading = new Label { Text = title };
        heading.AddThemeFontSizeOverride("font_size", 16);
        body.AddChild(heading);
        body.AddChild(new ColorRect
        {
            Color = new Color(0.82f, 0.67f, 0.36f, 0.55f),
            CustomMinimumSize = new Vector2(0, 1),
        });
        return body;
    }

    internal static StyleBoxFlat SectionBox()
        => new()
        {
            BgColor = new Color(0.08f, 0.055f, 0.045f, 0.92f),
            BorderColor = new Color(0.82f, 0.67f, 0.36f, 0.65f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 12,
            ContentMarginTop = 10,
            ContentMarginRight = 12,
            ContentMarginBottom = 10,
        };

    internal static StyleBoxFlat DescriptionBox()
        => new()
        {
            BgColor = new Color(0.04f, 0.035f, 0.03f, 0.82f),
            BorderColor = new Color(0.82f, 0.67f, 0.36f, 0.35f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 10,
            ContentMarginTop = 8,
            ContentMarginRight = 10,
            ContentMarginBottom = 8,
        };

    internal static PanelContainer MapFrame(Control map)
    {
        var frame = new PanelContainer
        {
            Name = "ScenarioMapFrame",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        var style = new StyleBoxFlat
        {
            BgColor = new Color("120d0b"),
            BorderColor = new Color("9b7845"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            ContentMarginLeft = 5,
            ContentMarginTop = 5,
            ContentMarginRight = 5,
            ContentMarginBottom = 5,
        };
        frame.AddThemeStyleboxOverride("panel", style);
        frame.AddChild(map);
        return frame;
    }
}
