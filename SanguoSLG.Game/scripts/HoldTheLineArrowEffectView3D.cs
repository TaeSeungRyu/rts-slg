using System.Linq;
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
    public int CompletedArrowCount { get; private set; }

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
        var arrows = visual.FindChildren("HoldLine_Group_*", "", true, false)
            .OfType<Node3D>().OrderBy(node => node.Name.ToString()).ToList();
        ArrowCount = arrows.Count;
        for (var index = 0; index < arrows.Count; index++)
        {
            var arrow = arrows[index];
            var destination = arrow.Position + Vector3.Right * 0.10f;
            var baseScale = arrow.Scale;
            arrow.Position -= Vector3.Right * (0.62f + index % 2 * 0.06f);
            arrow.Scale = baseScale * 0.03f;
            var tween = CreateTween();
            tween.TweenInterval(index * 0.09f);
            tween.TweenProperty(arrow, "scale", baseScale, 0.12f);
            tween.Parallel().TweenProperty(arrow, "position", destination, 0.38f)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tween.TweenInterval(0.42f);
            tween.TweenProperty(arrow, "position", destination + Vector3.Right * 0.18f, 0.18f);
            tween.Parallel().TweenProperty(arrow, "scale", baseScale * 0.01f, 0.20f);
            tween.TweenCallback(Callable.From(() => CompletedArrowCount++));
        }

        var completed = new Godot.Timer { OneShot = true, WaitTime = 1.68 };
        AddChild(completed);
        completed.Timeout += () => DisplayCompleted = true;
        completed.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.94 };
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
