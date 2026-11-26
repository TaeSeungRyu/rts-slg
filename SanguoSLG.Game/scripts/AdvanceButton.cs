namespace SanguoSLG.Game;

using Godot;

/// <summary>우하단 진행 버튼의 표시와 입력만 담당한다. 턴 규칙은 호출자가 관리한다.</summary>
public partial class AdvanceButton : Control
{
    private const string AssetRoot = "res://assets/ui/new_game/buttons/primary_";
    private Texture2D? _normal;
    private Texture2D? _hoverImage;
    private Texture2D? _pressedImage;
    private bool _hover;
    private bool _down;
    private Label _caption = null!;

    public Font? CaptionFont { get; set; }
    public bool Busy { get; set; }
    public float Progress { get; set; }
    public System.Action? Pressed;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        _normal = GD.Load<Texture2D>(AssetRoot + "normal.png");
        _hoverImage = GD.Load<Texture2D>(AssetRoot + "hover.png");
        _pressedImage = GD.Load<Texture2D>(AssetRoot + "pressed.png");
        _caption = new Label
        {
            Text = "▶▶ 진행",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(_caption);
        _caption.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _caption.AddThemeFontSizeOverride("font_size", TurnProgressLayout.ButtonFontSize);
        if (CaptionFont is not null) _caption.AddThemeFontOverride("font", CaptionFont);
        _caption.AddThemeConstantOverride("outline_size", 3);
        _caption.AddThemeColorOverride("font_outline_color", TurnProgressLayout.TextOutline);
        MouseEntered += () => _hover = true;
        MouseExited += () => { _hover = false; _down = false; };
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (Busy) return;
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mouse)
        {
            if (mouse.Pressed) _down = true;
            else if (_down)
            {
                _down = false;
                Pressed?.Invoke();
            }
            AcceptEvent();
        }
    }

    public override void _Process(double delta)
    {
        MouseDefaultCursorShape = Busy ? CursorShape.Arrow : CursorShape.PointingHand;
        _caption.AddThemeColorOverride("font_color", Busy
            ? TurnProgressLayout.DisabledText : TurnProgressLayout.ButtonText);
        QueueRedraw();
    }

    public override void _Draw()
    {
        // 상태별 PNG 크기가 달라도 버튼 슬롯과 클릭 영역은 고정한다.
        var bounds = new Rect2(Vector2.Zero, Size);
        var texture = _down ? _pressedImage : _hover && !Busy ? _hoverImage : _normal;
        if (texture is not null)
            DrawTextureRect(texture, bounds, false, Busy ? TurnProgressLayout.DisabledTint : Colors.White);
        else
        {
            DrawRect(bounds, TurnProgressLayout.BodyColor);
            DrawRect(bounds, TurnProgressLayout.Gold, false, 2f);
        }
        if (!Busy) return;
        var track = new Rect2(TurnProgressLayout.ProgressInset, TurnProgressLayout.ProgressTop,
            Size.X - TurnProgressLayout.ProgressInset * 2f, TurnProgressLayout.ProgressHeight);
        DrawRect(track, TurnProgressLayout.ProgressTrack);
        DrawRect(new Rect2(track.Position, new Vector2(track.Size.X * Mathf.Clamp(Progress, 0f, 1f), track.Size.Y)),
            TurnProgressLayout.ProgressFill);
        DrawRect(track, TurnProgressLayout.Gold, false, 1f);
    }
}
