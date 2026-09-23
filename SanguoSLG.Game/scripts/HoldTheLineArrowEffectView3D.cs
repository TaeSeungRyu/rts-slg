using Godot;

namespace SanguoSLG.Game;

/// <summary>가로로 누운 화살 여러 개가 아군 진형을 지키는 사수 GLB 효과.</summary>
public sealed partial class HoldTheLineArrowEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-hold-the-line.glb";
    private Node3D? _anchor;

    public bool LoadedFromGlb { get; private set; }
    public int ArrowCount { get; private set; }
    public bool DisplayCompleted { get; private set; }
    public bool IsScreenAligned { get; private set; }

    public override void _Ready()
    {
        _anchor = GetParentOrNull<Node3D>();
        var anchorPosition = _anchor?.GlobalPosition ?? GlobalPosition;
        TopLevel = true;
        GlobalPosition = anchorPosition + Vector3.Up * 0.58f;
        AlignToCamera();

        var packed = GD.Load<PackedScene>(AssetPath);
        if (packed is null)
        {
            GD.PushError($"사수 GLB를 불러오지 못했습니다: {AssetPath}");
            QueueFree();
            return;
        }
        var visual = packed.Instantiate<Node3D>();
        visual.Name = "HoldTheLineGlbVisual";
        AddChild(visual);
        LoadedFromGlb = true;
        ArrowCount = visual.FindChildren("HoldLine_ArrowShaft_*", "", true, false).Count;

        var baseScale = visual.Scale;
        visual.Scale = new Vector3(baseScale.X * 0.04f, baseScale.Y, baseScale.Z);
        var tween = CreateTween();
        tween.TweenProperty(visual, "scale", baseScale * 1.05f, 0.24f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(visual, "scale", baseScale, 0.10f);
        tween.TweenInterval(0.72f);
        tween.TweenProperty(visual, "scale", new Vector3(baseScale.X * 0.02f, baseScale.Y, baseScale.Z), 0.25f);

        var completed = new Godot.Timer { OneShot = true, WaitTime = 1.08 };
        AddChild(completed);
        completed.Timeout += () => DisplayCompleted = true;
        completed.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.38 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(_anchor)) GlobalPosition = _anchor!.GlobalPosition + Vector3.Up * 0.58f;
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
