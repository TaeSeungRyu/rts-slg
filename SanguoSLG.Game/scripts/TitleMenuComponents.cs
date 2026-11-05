using Godot;

namespace SanguoSLG.Game;

public sealed partial class TitleOrnatePanel : PanelContainer
{
    public TitleOrnatePanel()
    {
        var style = new StyleBoxTexture
        {
            Texture = GD.Load<Texture2D>("res://assets/ui/title/components/menu_panel.png"),
            TextureMarginLeft = 65,
            TextureMarginTop = 65,
            TextureMarginRight = 65,
            TextureMarginBottom = 65,
            ContentMarginLeft = 36,
            ContentMarginTop = 28,
            ContentMarginRight = 36,
            ContentMarginBottom = 28,
        };
        AddThemeStyleboxOverride("panel", style);
    }
}

public sealed partial class TitleMenuButton : Control
{
    private readonly TextureButton _hitArea;
    public event Action? Pressed;

    public TitleMenuButton(string text, string icon, bool primary, float width = 0)
    {
        Name = $"TitleAction_{text}";
        CustomMinimumSize = new Vector2(width, primary ? 72 : 54);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Ignore;

        var tier = primary ? "primary" : "secondary";
        _hitArea = new TextureButton
        {
            Name = "HitArea",
            TextureNormal = GD.Load<Texture2D>($"res://assets/ui/title/components/{tier}_normal.png"),
            TextureHover = GD.Load<Texture2D>($"res://assets/ui/title/components/{tier}_hover.png"),
            TexturePressed = GD.Load<Texture2D>($"res://assets/ui/title/components/{tier}_pressed.png"),
            IgnoreTextureSize = true,
            StretchMode = TextureButton.StretchModeEnum.Scale,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        _hitArea.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_hitArea);
        _hitArea.Pressed += () => Pressed?.Invoke();
        _hitArea.MouseEntered += () => CreateTween().TweenProperty(this, "scale", new Vector2(1.025f, 1.025f), 0.16);
        _hitArea.MouseExited += () => CreateTween().TweenProperty(this, "scale", Vector2.One, 0.18);

        var centered = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        centered.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(centered);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", primary ? 16 : 7);
        centered.AddChild(row);
        var iconImage = new TextureRect
        {
            Texture = GD.Load<Texture2D>($"res://assets/ui/title/components/icon_{icon}.png"),
            CustomMinimumSize = new Vector2(primary ? 38 : 24, primary ? 38 : 24),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddChild(iconImage);
        var caption = new Label { Name = "Caption", Text = text, MouseFilter = MouseFilterEnum.Ignore };
        caption.AddThemeFontOverride("font", GD.Load<FontFile>("res://assets/fonts/Pretendard-SemiBold.otf"));
        caption.AddThemeFontSizeOverride("font_size", primary ? 24 : 15);
        caption.AddThemeColorOverride("font_color", new Color("f4d693"));
        caption.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        caption.AddThemeConstantOverride("shadow_offset_y", 2);
        row.AddChild(caption);
        Resized += () => PivotOffset = Size / 2;
    }

    public void Activate() => _hitArea.EmitSignal(BaseButton.SignalName.Pressed);
}
