using Godot;

namespace SanguoSLG.Game;

/// <summary>작은 붉은 십자가 하나가 부대 아래에서 위로 상승하는 수습 효과.</summary>
public sealed partial class PatchCrossEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-field-medic.glb";
    private Node3D? _anchor;

    public bool LoadedFromGlb { get; private set; }
    public bool RiseCompleted { get; private set; }
    public bool IsScreenAligned { get; private set; }

    public override void _Ready()
    {
        _anchor = GetParentOrNull<Node3D>();
        var anchorPosition = _anchor?.GlobalPosition ?? GlobalPosition;
        TopLevel = true;
        GlobalPosition = anchorPosition + Vector3.Up * 0.16f;
        AlignToCamera();

        var packed = GD.Load<PackedScene>(AssetPath);
        if (packed is null)
        {
            GD.PushError($"수습 십자가 GLB를 불러오지 못했습니다: {AssetPath}");
            QueueFree();
            return;
        }
        var cross = packed.Instantiate<Node3D>();
        cross.Name = "PatchSingleCrossGlbVisual";
        AddChild(cross);
        cross.Position = Vector3.Down * 0.44f;
        cross.Scale = Vector3.One * 0.04f;
        LoadedFromGlb = true;

        var tween = CreateTween();
        tween.TweenProperty(cross, "scale", Vector3.One * 0.72f, 0.14f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(cross, "position", Vector3.Up * 0.42f, 0.62f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(cross, "position", Vector3.Up * 0.58f, 0.16f);
        tween.Parallel().TweenProperty(cross, "scale", Vector3.One * 0.01f, 0.20f);
        tween.TweenCallback(Callable.From(() => RiseCompleted = true));

        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.18 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(_anchor)) GlobalPosition = _anchor!.GlobalPosition + Vector3.Up * 0.16f;
        AlignToCamera();
    }

    private void AlignToCamera()
    {
        var camera = GetViewport()?.GetCamera3D();
        if (camera is null) return;
        var cameraBasis = camera.GlobalBasis.Orthonormalized();
        GlobalBasis = cameraBasis;
        IsScreenAligned = Mathf.Abs(GlobalBasis.X.Dot(cameraBasis.X)) > 0.999f
            && Mathf.Abs(GlobalBasis.Y.Dot(cameraBasis.Y)) > 0.999f
            && Mathf.Abs(GlobalBasis.Z.Dot(cameraBasis.Z)) > 0.999f;
    }
}
