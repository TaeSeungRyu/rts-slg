using System.Linq;
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
    public int CompletedStreakCount { get; private set; }

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
        var streaks = visual.FindChildren("Evasion_WindStreak_*", "", true, false)
            .OfType<Node3D>().OrderBy(node => node.Name.ToString()).ToList();
        var motes = visual.FindChildren("Evasion_WindMote_*", "", true, false)
            .OfType<Node3D>().OrderBy(node => node.Name.ToString()).ToList();
        WindStreakCount = streaks.Count;
        WindMoteCount = motes.Count;

        // 선 전체를 한 덩어리로 밀지 않고, 각 바람줄기가 서로 다른 박자와 속도로 통과한다.
        for (var index = 0; index < streaks.Count; index++)
        {
            var streak = streaks[index];
            var destination = streak.Position + Vector3.Right * (0.22f + index % 2 * 0.10f);
            var baseScale = streak.Scale;
            streak.Position -= Vector3.Right * (0.58f + index * 0.06f);
            streak.Scale = baseScale * 0.05f;
            var tween = CreateTween();
            tween.TweenInterval(index * 0.07f);
            tween.TweenProperty(streak, "scale", baseScale, 0.13f);
            tween.Parallel().TweenProperty(streak, "position", destination, 0.48f + index * 0.045f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
            tween.TweenProperty(streak, "position", destination + Vector3.Right * 0.22f, 0.18f);
            tween.Parallel().TweenProperty(streak, "scale", baseScale * 0.02f, 0.20f);
            tween.TweenCallback(Callable.From(() => CompletedStreakCount++));
        }
        for (var index = 0; index < motes.Count; index++)
        {
            var mote = motes[index];
            var origin = mote.Position;
            var baseScale = mote.Scale;
            mote.Position = origin - Vector3.Right * (0.46f + index % 3 * 0.08f);
            mote.Scale = baseScale * 0.04f;
            var tween = CreateTween();
            tween.TweenInterval(0.04f + index * 0.045f);
            tween.TweenProperty(mote, "scale", baseScale, 0.10f);
            tween.Parallel().TweenProperty(mote, "position",
                origin + Vector3.Right * 0.34f + Vector3.Up * ((index % 2 == 0 ? 1f : -1f) * 0.06f), 0.56f);
            tween.TweenProperty(mote, "scale", baseScale * 0.01f, 0.18f);
        }

        var completed = new Godot.Timer { OneShot = true, WaitTime = 1.28 };
        AddChild(completed);
        completed.Timeout += () => SweepCompleted = true;
        completed.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.58 };
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
