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
        theme.SetColor("font_color", "ItemList", parchment);
        theme.SetColor("font_selected_color", "ItemList", Color.Color8(255, 241, 195));
        foreach (var type in new[] { "LineEdit", "TextEdit" })
        {
            theme.SetStylebox("normal", type, Box(new Color(0.11f, 0.09f, 0.08f), new Color(gold, 0.46f), 1));
            theme.SetStylebox("focus", type, Box(new Color(0.16f, 0.11f, 0.09f), gold, 2));
            theme.SetColor("font_color", type, parchment);
            theme.SetColor("font_placeholder_color", type, new Color(parchment, 0.48f));
        }
        theme.SetStylebox("tab_selected", "TabBar", Box(new Color(0.42f, 0.21f, 0.10f), gold, 2));
        theme.SetStylebox("tab_unselected", "TabBar", Box(new Color(0.14f, 0.09f, 0.08f), new Color(gold, 0.40f), 1));
        theme.SetStylebox("tab_hovered", "TabBar", Box(new Color(0.31f, 0.15f, 0.09f), gold, 1));
        theme.SetColor("font_selected_color", "TabBar", Color.Color8(255, 239, 190));
        theme.SetColor("font_unselected_color", "TabBar", parchment);
        return theme;
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
}
