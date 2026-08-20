namespace SanguoSLG.Game;

using Godot;

/// <summary>
/// 진행 버튼(우하단). 원형 금테 프레임 + 교체 가능한 아이콘 + 리치 인터랙션(대기 숨쉬기·호버
/// 글로우·누름 반동·진행 중 방사형 진행 링). 이미지는 <see cref="Icon"/>만 바꾸면 교체된다
/// (없으면 금색 ▶ 폴백). 게임 규칙은 없다 — 표현·입력만.
/// </summary>
public partial class AdvanceButton : Control
{
    /// <summary>교체 가능한 아이콘. null이면 금색 재생 삼각형 폴백.</summary>
    public Texture2D? Icon { get; set; }

    /// <summary>진행 중 여부 — 입력 차단 + 방사형 진행 링 표시 + 아이콘 딤.</summary>
    public bool Busy { get; set; }

    /// <summary>진행 진척(0~1) — Busy일 때 방사형 링이 이만큼 찬다.</summary>
    public float Progress { get; set; }

    public System.Action? Pressed;

    private bool _hover;
    private bool _pressed;
    private float _t;
    private float _scale = 1f;

    private static readonly Color Gold = new(0.82f, 0.66f, 0.30f);
    private static readonly Color GoldBright = new(1.0f, 0.86f, 0.46f);
    private static readonly Color Ink = new(0.10f, 0.08f, 0.07f);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        MouseEntered += () => _hover = true;
        MouseExited += () => { _hover = false; _pressed = false; };
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (Busy) { return; }
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            if (mb.Pressed) { _pressed = true; }
            else if (_pressed) { _pressed = false; Pressed?.Invoke(); }
        }
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;

        // 목표 스케일: 누름 반동 → 호버 확대 → 대기 숨쉬기.
        var target = _pressed ? 0.90f
            : _hover && !Busy ? 1.09f
            : Busy ? 1.0f
            : 1.0f + 0.03f * Mathf.Sin(_t * 2.2f);
        _scale = Mathf.Lerp(_scale, target, Mathf.Min(1f, (float)delta * 14f));

        PivotOffset = Size / 2f;
        Scale = new Vector2(_scale, _scale);
        QueueRedraw(); // 숨쉬기·글로우·진행 링 매 프레임 갱신
    }

    public override void _Draw()
    {
        var c = Size / 2f;
        var r = Mathf.Min(Size.X, Size.Y) / 2f - 3f;
        var pulse = 0.5f + 0.5f * Mathf.Sin(_t * 3f);
        var tint = Busy ? new Color(0.55f, 0.55f, 0.55f) : Colors.White;

        // 드롭 섀도.
        DrawCircle(c + new Vector2(0f, 3f), r, new Color(0f, 0f, 0f, 0.4f));

        if (Icon is not null)
        {
            // 자체 금테를 가진 메달리온 이미지 — 버튼을 꽉 채워 그리고, 별도 프레임은 덧그리지 않는다.
            var rect = new Rect2(c - new Vector2(r, r), new Vector2(r * 2f, r * 2f));
            DrawTextureRect(Icon, rect, false, tint);
        }
        else
        {
            // 폴백: 잉크 원 + 금테 프레임 + 금색 ▶.
            var inner = r - r * 0.13f;
            DrawCircle(c, r, new Color(Ink.R, Ink.G, Ink.B, 0.92f));
            var tri = new[]
            {
                c + new Vector2(-inner * 0.34f, -inner * 0.5f),
                c + new Vector2(-inner * 0.34f, inner * 0.5f),
                c + new Vector2(inner * 0.56f, 0f),
            };
            DrawColoredPolygon(tri, GoldBright * tint);
            var ring = _hover && !Busy ? GoldBright : Gold;
            DrawArc(c, r - 1f, 0f, Mathf.Tau, 72, ring, Mathf.Max(3f, r * 0.06f), true);
        }

        if (Busy)
        {
            // 방사형 진행 링: 12시부터 시계방향으로 Progress만큼.
            var end = -Mathf.Pi / 2f + Mathf.Tau * Mathf.Clamp(Progress, 0f, 1f);
            DrawArc(c, r - r * 0.09f, -Mathf.Pi / 2f, end, 72, GoldBright, Mathf.Max(4f, r * 0.1f), true);
        }
        else if (_hover)
        {
            // 호버 글로우 링(맥동).
            var glow = new Color(GoldBright.R, GoldBright.G, GoldBright.B, 0.22f + 0.28f * pulse);
            DrawArc(c, r - 1f, 0f, Mathf.Tau, 72, glow, r * 0.12f, true);
        }
    }
}
