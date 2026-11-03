using Godot;

namespace SanguoSLG.Game;

public partial class MenuLoadingEmblem : Control
{
    private float _phase;
    public int FrameCount { get; private set; }

    public override void _Process(double delta)
    {
        _phase += (float)delta * 1.8f;
        FrameCount++;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var center = Size * 0.5f;
        DrawArc(center, 31f, 0, Mathf.Tau, 48, new Color("806945"), 2f, true);
        DrawArc(center, 31f, _phase, _phase + Mathf.Pi * 0.8f, 24,
            new Color("f6da91"), 4f, true);
        for (var index = 0; index < 8; index++)
        {
            var angle = _phase + index * Mathf.Tau / 8f;
            var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 39f;
            var alpha = 0.25f + 0.75f * (index + 1) / 8f;
            DrawCircle(point, 2f + index * 0.15f, new Color(0.94f, 0.78f, 0.46f, alpha));
        }
        DrawLine(center + new Vector2(-8, 7), center + new Vector2(0, -9),
            new Color("f6da91"), 3f, true);
        DrawLine(center + new Vector2(0, -9), center + new Vector2(8, 7),
            new Color("f6da91"), 3f, true);
    }
}
