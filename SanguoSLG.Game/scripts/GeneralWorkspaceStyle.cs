namespace SanguoSLG.Game;

using Godot;

internal static class GeneralWorkspaceStyle
{
    internal static Theme Theme()
    {
        var theme = new Theme();
        theme.DefaultFont = GD.Load<Font>("res://assets/fonts/Pretendard-SemiBold.otf");
        theme.DefaultFontSize = 15;
        return theme;
    }

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
