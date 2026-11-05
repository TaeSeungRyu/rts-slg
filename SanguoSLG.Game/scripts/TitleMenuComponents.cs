using Godot;

namespace SanguoSLG.Game;

public sealed partial class TitleOrnatePanel : Control
{
    public MarginContainer Content { get; }

    public TitleOrnatePanel()
    {
        CustomMinimumSize = new Vector2(620, 720);
        var background = new NinePatchRect
        {
            Texture = GD.Load<Texture2D>("res://assets/ui/title/components/menu_panel.png"),
            PatchMarginLeft = 52,
            PatchMarginTop = 52,
            PatchMarginRight = 52,
            PatchMarginBottom = 52,
            SelfModulate = new Color(1, 1, 1, 0.88f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(background);
        Content = new MarginContainer();
        Content.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Content.AddThemeConstantOverride("margin_left", 30);
        Content.AddThemeConstantOverride("margin_top", 32);
        Content.AddThemeConstantOverride("margin_right", 30);
        Content.AddThemeConstantOverride("margin_bottom", 26);
        AddChild(Content);
    }
}

public sealed partial class TitleMenuButton : Control
{
    private readonly TextureButton _hitArea;
    private static Font MenuFont(int weight) => new FontVariation
    {
        BaseFont = GD.Load<FontFile>("res://assets/fonts/NotoSerifKR-variable.ttf"),
        VariationOpentype = new Godot.Collections.Dictionary { ["wght"] = weight },
    };
    public event Action? Pressed;

    public TitleMenuButton(string text, string icon, bool primary, float width = 0)
    {
        Name = $"TitleAction_{text}";
        CustomMinimumSize = new Vector2(width, primary ? 88 : width > 0 ? width * 112f / 640f : 40);
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
            StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        _hitArea.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_hitArea);
        _hitArea.Pressed += () => Pressed?.Invoke();
        _hitArea.MouseEntered += () => CreateTween().TweenProperty(this, "modulate", new Color(1, 0.97f, 0.88f), 0.16);
        _hitArea.MouseExited += () => CreateTween().TweenProperty(this, "modulate", Colors.White, 0.18);

        var centered = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        centered.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(centered);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", primary ? 13 : 5);
        centered.AddChild(row);
        var iconImage = new TextureRect
        {
            Texture = GD.Load<Texture2D>($"res://assets/ui/title/components/icon_{icon}.png"),
            CustomMinimumSize = new Vector2(primary ? 35 : 21, primary ? 35 : 21),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddChild(iconImage);
        var caption = new Label { Name = "Caption", Text = text, MouseFilter = MouseFilterEnum.Ignore };
        caption.AddThemeFontOverride("font", MenuFont(primary ? 700 : 500));
        caption.AddThemeFontSizeOverride("font_size", primary ? 23 : 16);
        caption.AddThemeColorOverride("font_color", new Color("f4d693"));
        caption.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        caption.AddThemeConstantOverride("shadow_offset_y", 2);
        row.AddChild(caption);
    }

    public void Activate() => _hitArea.EmitSignal(BaseButton.SignalName.Pressed);
}
