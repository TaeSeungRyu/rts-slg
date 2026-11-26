namespace SanguoSLG.Game;

using Godot;

/// <summary>대기 버튼과 진행 중 패널의 표시·입력만 담당한다.</summary>
public partial class AdvanceButton : Control
{
    private Texture2D? _normal, _active, _disabled, _statusBackground;
    private bool _hover, _down, _busy;
    private Label _buttonCaption = null!;
    private MarginContainer _statusHost = null!;
    private ProgressBar _progress = null!;

    public Font? CaptionFont { get; set; }
    public Label DayLabel { get; private set; } = null!;
    public Label TurnLabel { get; private set; } = null!;
    private bool _disabledInput;
    public bool Disabled
    {
        get => _disabledInput;
        set { _disabledInput = value; QueueRedraw(); }
    }
    public bool Busy
    {
        get => _busy;
        set
        {
            _busy = value;
            if (value) _down = false;
            CustomMinimumSize = value
                ? new Vector2(TurnProgressLayout.StatusWidth, TurnProgressLayout.StatusHeight)
                : new Vector2(TurnProgressLayout.ButtonWidth, TurnProgressLayout.ButtonHeight);
            if (_statusHost is not null) _statusHost.Visible = value;
            if (_buttonCaption is not null) _buttonCaption.Visible = !value;
            QueueRedraw();
        }
    }
    public float Progress { get; set; }
    public System.Action? Pressed;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        _normal = GD.Load<Texture2D>(TurnProgressLayout.AssetRoot + "normal.png");
        _active = GD.Load<Texture2D>(TurnProgressLayout.AssetRoot + "active.png");
        _disabled = GD.Load<Texture2D>(TurnProgressLayout.AssetRoot + "disable.png");
        _statusBackground = GD.Load<Texture2D>(TurnProgressLayout.AssetRoot + "bg.png");

        _buttonCaption = Caption("▶▶ 진행", TurnProgressLayout.ButtonFontSize);
        AddChild(_buttonCaption);
        _buttonCaption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _statusHost = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_statusHost);
        _statusHost.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _statusHost.AddThemeConstantOverride("margin_left", TurnProgressLayout.StatusHorizontalInset);
        _statusHost.AddThemeConstantOverride("margin_right", TurnProgressLayout.StatusHorizontalInset);
        _statusHost.AddThemeConstantOverride("margin_top", TurnProgressLayout.StatusTopInset);
        _statusHost.AddThemeConstantOverride("margin_bottom", TurnProgressLayout.StatusBottomInset);
        var stack = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        stack.AddThemeConstantOverride("separation", TurnProgressLayout.StatusGap);
        _statusHost.AddChild(stack);
        DayLabel = Caption("", TurnProgressLayout.DayFontSize);
        TurnLabel = Caption("", TurnProgressLayout.TurnFontSize);
        stack.AddChild(DayLabel);
        stack.AddChild(TurnLabel);
        _progress = new ProgressBar
        {
            ShowPercentage = false, MaxValue = 100,
            CustomMinimumSize = new Vector2(0, TurnProgressLayout.ProgressHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _progress.AddThemeStyleboxOverride("background", BarStyle(TurnProgressLayout.ProgressTrack));
        _progress.AddThemeStyleboxOverride("fill", BarStyle(TurnProgressLayout.ProgressFill));
        stack.AddChild(_progress);
        stack.AddChild(Caption("진행 중", TurnProgressLayout.BusyFontSize));
        MouseEntered += () => { _hover = true; QueueRedraw(); };
        MouseExited += () => { _hover = false; _down = false; QueueRedraw(); };
        Busy = _busy;
    }

    private static StyleBoxFlat BarStyle(Color color) => new()
    {
        BgColor = color,
        CornerRadiusTopLeft = TurnProgressLayout.ProgressRadius,
        CornerRadiusTopRight = TurnProgressLayout.ProgressRadius,
        CornerRadiusBottomLeft = TurnProgressLayout.ProgressRadius,
        CornerRadiusBottomRight = TurnProgressLayout.ProgressRadius,
    };

    private Label Caption(string text, int size)
    {
        var label = new Label
        {
            Text = text, HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", TurnProgressLayout.ButtonText);
        label.AddThemeConstantOverride("outline_size", 3);
        label.AddThemeColorOverride("font_outline_color", TurnProgressLayout.TextOutline);
        if (CaptionFont is not null) label.AddThemeFontOverride("font", CaptionFont);
        return label;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (Busy || Disabled) return;
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
        {
            if (mouse.Pressed) _down = true;
            else if (_down) { _down = false; Pressed?.Invoke(); }
            QueueRedraw();
            AcceptEvent();
        }
    }

    public override void _Process(double delta)
    {
        if (_progress is not null) _progress.Value = Mathf.Clamp(Progress, 0f, 1f) * 100f;
        MouseDefaultCursorShape = Busy || Disabled ? CursorShape.Arrow : CursorShape.PointingHand;
    }

    public override void _Draw()
    {
        var texture = Busy ? _statusBackground : Disabled ? _disabled
            : _down || _hover ? _active : _normal;
        if (texture is null) texture = _disabled;
        if (texture is not null) DrawTextureRect(texture, new Rect2(Vector2.Zero, Size), false);
    }
}
