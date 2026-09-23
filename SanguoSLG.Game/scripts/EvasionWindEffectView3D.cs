using Godot;

namespace SanguoSLG.Game;

/// <summary>회색 바람줄기가 아군 진형을 가로질러 불어오는 회피술 GLB 효과.</summary>
public sealed partial class EvasionWindEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-evasion.glb";
    private Node3D? _anchor;

    public bool LoadedFromGlb { get; private set; }
    public int WindStreakCount { get; private set; }
    public int WindMoteCount { get; private set; }
    public bool SweepCompleted { get; private set; }
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
            GD.PushError($"회피술 GLB를 불러오지 못했습니다: {AssetPath}");
            QueueFree();
            return;
        }
        var visual = packed.Instantiate<Node3D>();
        visual.Name = "EvasionWindGlbVisual";
        AddChild(visual);
        LoadedFromGlb = true;
        WindStreakCount = visual.FindChildren("Evasion_WindStreak_*", "", true, false).Count;
        WindMoteCount = visual.FindChildren("Evasion_WindMote_*", "", true, false).Count;

        var baseScale = visual.Scale;
        visual.Position = new Vector3(-0.48f, 0f, 0f);
        visual.Scale = baseScale * 0.08f;
        var tween = CreateTween();
        tween.TweenProperty(visual, "scale", baseScale, 0.16f)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(visual, "position", new Vector3(0.16f, 0f, 0f), 0.72f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        tween.TweenProperty(visual, "position", new Vector3(0.42f, 0f, 0f), 0.28f);
        tween.Parallel().TweenProperty(visual, "scale", baseScale * 0.02f, 0.30f);

        var completed = new Godot.Timer { OneShot = true, WaitTime = 1.05 };
        AddChild(completed);
        completed.Timeout += () => SweepCompleted = true;
        completed.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.34 };
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
