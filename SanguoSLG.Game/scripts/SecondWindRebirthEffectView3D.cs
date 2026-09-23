using System.Linq;
using Godot;

namespace SanguoSLG.Game;

/// <summary>황금 혼백이 생명핵으로 복귀하고 심장이 다시 뛰는 불사 GLB 효과.</summary>
public sealed partial class SecondWindRebirthEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-second-wind.glb";
    private Node3D? _anchor;

    public bool LoadedFromGlb { get; private set; }
    public int SoulCount { get; private set; }
    public int ReturnedSoulCount { get; private set; }
    public int CompletedHeartBeats { get; private set; }
    public int RingCount { get; private set; }
    public int CompletedRingPulses { get; private set; }
    public bool IsScreenAligned { get; private set; }

    public override void _Ready()
    {
        _anchor = GetParentOrNull<Node3D>();
        var anchorPosition = _anchor?.GlobalPosition ?? GlobalPosition;
        TopLevel = true;
        GlobalPosition = anchorPosition + Vector3.Up * 0.66f;
        AlignToCamera();

        var packed = GD.Load<PackedScene>(AssetPath);
        if (packed is null)
        {
            GD.PushError($"불사 GLB를 불러오지 못했습니다: {AssetPath}");
            QueueFree();
            return;
        }
        var visual = packed.Instantiate<Node3D>();
        visual.Name = "SecondWindRebirthGlbVisual";
        AddChild(visual);
        visual.Scale = Vector3.One * 0.62f;
        LoadedFromGlb = true;

        var heart = visual.FindChild("SecondWind_HeartGroup", true, false) as Node3D;
        var souls = visual.FindChildren("SecondWind_SoulGroup_*", "", true, false)
            .OfType<Node3D>().OrderBy(node => node.Name.ToString()).ToList();
        var rings = visual.FindChildren("SecondWind_RebirthRing_*", "", true, false)
            .OfType<Node3D>().OrderBy(node => node.Name.ToString()).ToList();
        SoulCount = souls.Count;
        RingCount = rings.Count;
        if (heart is null || souls.Count == 0 || rings.Count == 0)
        {
            GD.PushError("불사 효과의 생명핵·혼백·부활 고리 노드를 찾지 못했습니다.");
            QueueFree();
            return;
        }

        heart.Scale = Vector3.One * 0.05f;
        foreach (var ring in rings) ring.Scale = Vector3.One * 0.01f;
        for (var index = 0; index < souls.Count; index++)
        {
            var soul = souls[index];
            var tween = CreateTween();
            tween.TweenInterval(index * 0.055f);
            tween.TweenProperty(soul, "position", Vector3.Zero, 0.42f)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(soul, "scale", Vector3.One * 0.08f, 0.42f);
            tween.TweenCallback(Callable.From(() => ReturnedSoulCount++));
        }

        var heartbeat = CreateTween();
        heartbeat.TweenInterval(0.42f);
        for (var beat = 0; beat < 3; beat++)
        {
            heartbeat.TweenProperty(heart, "scale", Vector3.One * 1.12f, 0.10f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            heartbeat.TweenProperty(heart, "scale", Vector3.One * 0.88f, 0.10f);
            heartbeat.TweenCallback(Callable.From(() => CompletedHeartBeats++));
        }
        heartbeat.TweenProperty(heart, "scale", Vector3.One, 0.08f);

        for (var index = 0; index < rings.Count; index++)
        {
            var ring = rings[index];
            var pulse = CreateTween();
            pulse.TweenInterval(0.62f + index * 0.14f);
            pulse.TweenProperty(ring, "scale", Vector3.One * 1.10f, 0.28f)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            pulse.TweenProperty(ring, "scale", Vector3.One * 0.01f, 0.16f);
            pulse.TweenCallback(Callable.From(() => CompletedRingPulses++));
        }

        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.72 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(_anchor)) GlobalPosition = _anchor!.GlobalPosition + Vector3.Up * 0.66f;
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
