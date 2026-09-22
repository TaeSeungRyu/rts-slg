using Godot;

namespace SanguoSLG.Game;

/// <summary>시전자 진형 앞에 육각 방패가 나타나 반격 태세를 세우는 1회성 GLB 효과.</summary>
public sealed partial class RiposteFormationEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-riposte.glb";
    private Node3D? _anchor;

    public bool LoadedFromGlb { get; private set; }
    public int ShieldLayerCount { get; private set; }
    public int CounterSpikeCount { get; private set; }
    public bool DisplayCompleted { get; private set; }
    public float CameraFacingDot { get; private set; }

    public override void _Ready()
    {
        _anchor = GetParentOrNull<Node3D>();
        var anchorPosition = _anchor?.GlobalPosition ?? GlobalPosition;
        TopLevel = true;
        GlobalPosition = anchorPosition + Vector3.Up * 0.62f;
        AlignToCamera();

        var scene = GD.Load<PackedScene>(AssetPath);
        if (scene is null)
        {
            GD.PushError($"반격진 GLB를 불러오지 못했습니다: {AssetPath}");
            QueueFree();
            return;
        }

        var visual = scene.Instantiate<Node3D>();
        visual.Name = "RiposteGlbVisual";
        AddChild(visual);
        LoadedFromGlb = true;
        ShieldLayerCount = visual.FindChildren("Riposte_Shield*", "", true, false).Count;
        CounterSpikeCount = visual.FindChildren("Riposte_CounterSpike_*", "", true, false).Count;

        var baseScale = visual.Scale;
        visual.Scale = baseScale * 0.02f;
        var tween = CreateTween();
        tween.TweenProperty(visual, "scale", baseScale * 1.08f, 0.24f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(visual, "scale", baseScale, 0.10f);
        tween.TweenInterval(0.70f);
        tween.TweenProperty(visual, "scale", baseScale * 0.01f, 0.28f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);

        var completed = new Godot.Timer { OneShot = true, WaitTime = 1.08 };
        AddChild(completed);
        completed.Timeout += () => DisplayCompleted = true;
        completed.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.42 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(_anchor)) GlobalPosition = _anchor!.GlobalPosition + Vector3.Up * 0.62f;
        AlignToCamera();
    }

    private void AlignToCamera()
    {
        var camera = GetViewport()?.GetCamera3D();
        if (camera is null) return;
        var front = camera.GlobalPosition - GlobalPosition;
        front.Y = 0f;
        if (front.LengthSquared() < 0.000001f) return;
        front = front.Normalized();
        GlobalBasis = new Basis(Vector3.Up.Cross(front).Normalized(), Vector3.Up, front).Orthonormalized();
        CameraFacingDot = GlobalBasis.Z.Dot(front);
    }
}
