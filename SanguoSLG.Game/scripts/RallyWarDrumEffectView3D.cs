using System.Linq;
using Godot;

namespace SanguoSLG.Game;

/// <summary>금빛 전고를 연타하고 사기 파동을 퍼뜨리는 고무 GLB 효과.</summary>
public sealed partial class RallyWarDrumEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-rally.glb";
    private Node3D? _anchor;

    public bool LoadedFromGlb { get; private set; }
    public int MalletCount { get; private set; }
    public int RingCount { get; private set; }
    public int CompletedDrumHits { get; private set; }
    public int CompletedRingPulses { get; private set; }
    public bool IsScreenAligned { get; private set; }

    public override void _Ready()
    {
        _anchor = GetParentOrNull<Node3D>();
        var anchorPosition = _anchor?.GlobalPosition ?? GlobalPosition;
        TopLevel = true;
        GlobalPosition = anchorPosition + Vector3.Up * 0.62f;
        AlignToCamera();

        var packed = GD.Load<PackedScene>(AssetPath);
        if (packed is null)
        {
            GD.PushError($"고무 GLB를 불러오지 못했습니다: {AssetPath}");
            QueueFree();
            return;
        }
        var visual = packed.Instantiate<Node3D>();
        visual.Name = "RallyWarDrumGlbVisual";
        AddChild(visual);
        visual.Scale = Vector3.One * 0.58f;
        LoadedFromGlb = true;

        var mallets = visual.FindChildren("Rally_MalletGroup_*", "", true, false)
            .OfType<Node3D>().OrderBy(node => node.Name.ToString()).ToList();
        var rings = visual.FindChildren("Rally_MoraleRing_*", "", true, false)
            .OfType<Node3D>().OrderBy(node => node.Name.ToString()).ToList();
        MalletCount = mallets.Count;
        RingCount = rings.Count;

        foreach (var ring in rings) ring.Scale = Vector3.One * 0.01f;
        for (var hit = 0; hit < 4; hit++)
        {
            var mallet = mallets[hit % mallets.Count];
            var origin = mallet.Position;
            var strike = CreateTween();
            strike.TweenInterval(0.22f + hit * 0.23f);
            strike.TweenProperty(mallet, "position", origin + Vector3.Down * 0.18f, 0.075f)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            strike.TweenProperty(mallet, "position", origin, 0.13f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            strike.TweenCallback(Callable.From(() => CompletedDrumHits++));
        }
        for (var index = 0; index < rings.Count; index++)
        {
            var ring = rings[index];
            var pulse = CreateTween();
            pulse.TweenInterval(0.30f + index * 0.25f);
            pulse.TweenProperty(ring, "scale", Vector3.One * 1.12f, 0.30f)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            pulse.TweenProperty(ring, "scale", Vector3.One * 0.01f, 0.18f);
            pulse.TweenCallback(Callable.From(() => CompletedRingPulses++));
        }

        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.58 };
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
        var cameraBasis = camera.GlobalBasis.Orthonormalized();
        GlobalBasis = cameraBasis;
        IsScreenAligned = Mathf.Abs(GlobalBasis.X.Dot(cameraBasis.X)) > 0.999f
            && Mathf.Abs(GlobalBasis.Y.Dot(cameraBasis.Y)) > 0.999f
            && Mathf.Abs(GlobalBasis.Z.Dot(cameraBasis.Z)) > 0.999f;
    }
}
