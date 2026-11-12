using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 상태별 원본 PNG를 같은 배율로 중앙에 그리는 명령 버튼.
/// 이미지 픽셀 크기가 달라도 Control/클릭 영역은 고정된다.
/// </summary>
internal partial class CommandPaletteButton : Button
{
    private Texture2D? _normal;
    private Texture2D? _active;
    private Texture2D? _disabled;
    private float _textLeftRatio = 0.30f;
    private float _textRightRatio = 0.06f;

    internal void Configure(Texture2D normal, Texture2D active, Texture2D? disabled = null,
        float textLeftRatio = 0.30f, float textRightRatio = 0.06f)
    {
        _normal = normal;
        _active = active;
        _disabled = disabled;
        _textLeftRatio = textLeftRatio;
        _textRightRatio = textRightRatio;
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled", "focus" })
            AddThemeStyleboxOverride(state, new StyleBoxEmpty());
        AddThemeColorOverride("font_color", Colors.Transparent);
        AddThemeColorOverride("font_hover_color", Colors.Transparent);
        AddThemeColorOverride("font_pressed_color", Colors.Transparent);
        AddThemeColorOverride("font_focus_color", Colors.Transparent);
        AddThemeColorOverride("font_disabled_color", Colors.Transparent);
        MouseDefaultCursorShape = CursorShape.PointingHand;
        QueueRedraw();
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        if (_normal is null) return;
        var highlighted = ButtonPressed || IsHovered() || HasFocus();
        var texture = Disabled ? _disabled ?? _normal : highlighted ? _active ?? _normal : _normal;
        var referenceHeight = Mathf.Max(_normal.GetHeight(), _active?.GetHeight() ?? 0);
        var scale = Mathf.Min((Size.X - 2f) / _normal.GetWidth(), (Size.Y - 2f) / referenceHeight);
        var drawSize = texture.GetSize() * scale;
        var rect = new Rect2((Size - drawSize) * 0.5f, drawSize);
        var imageColor = Disabled ? new Color(1f, 1f, 1f, 0.48f) : Colors.White;
        DrawTextureRect(texture, rect, false, imageColor);

        var font = GetThemeDefaultFont();
        var fontSize = GetThemeFontSize("font_size");
        var left = Size.X * _textLeftRatio;
        var width = Size.X * (1f - _textLeftRatio - _textRightRatio);
        var textSize = font.GetStringSize(Text, HorizontalAlignment.Center, width, fontSize);
        var baseline = new Vector2(left, (Size.Y - textSize.Y) * 0.5f + font.GetAscent(fontSize));
        var textColor = Disabled ? new Color(GameUiSkin.Ivory, 0.45f)
            : highlighted ? new Color("fff0c6") : GameUiSkin.Ivory;
        DrawString(font, baseline, Text, HorizontalAlignment.Center, width, fontSize, textColor);
    }
}
