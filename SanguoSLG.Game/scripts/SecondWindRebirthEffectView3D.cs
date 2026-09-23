using System.Linq;
using Godot;

namespace SanguoSLG.Game;

/// <summary>불씨가 귀환한 뒤 붉은 봉황이 솟아 날개를 펼치는 불사 GLB 효과.</summary>
public sealed partial class SecondWindRebirthEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-second-wind.glb";
    private Node3D? _anchor;

    public bool LoadedFromGlb { get; private set; }
    public int SoulCount { get; private set; }
    public int ReturnedSoulCount { get; private set; }
    public bool PhoenixRiseCompleted { get; private set; }
    public bool WingsSpreadCompleted { get; private set; }
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

        var phoenix = visual.FindChild("SecondWind_PhoenixGroup", true, false) as Node3D;
        var leftWing = visual.FindChild("SecondWind_WingGroup_Left", true, false) as Node3D;
        var rightWing = visual.FindChild("SecondWind_WingGroup_Right", true, false) as Node3D;
        var souls = visual.FindChildren("SecondWind_SoulGroup_*", "", true, false)
            .OfType<Node3D>().OrderBy(node => node.Name.ToString()).ToList();
        var rings = visual.FindChildren("SecondWind_RebirthRing_*", "", true, false)
            .OfType<Node3D>().OrderBy(node => node.Name.ToString()).ToList();
        SoulCount = souls.Count;
        RingCount = rings.Count;
        if (phoenix is null || leftWing is null || rightWing is null || souls.Count == 0 || rings.Count == 0)
        {
            GD.PushError("불사 효과의 봉황·날개·불씨·부활 고리 노드를 찾지 못했습니다.");
            QueueFree();
            return;
        }

        var phoenixDestination = phoenix.Position;
        phoenix.Position = phoenixDestination + Vector3.Down * 0.52f;
        phoenix.Scale = Vector3.One * 0.05f;
        leftWing.Scale = new Vector3(0.12f, 1f, 0.35f);
        rightWing.Scale = new Vector3(0.12f, 1f, 0.35f);
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

        var rise = CreateTween();
        rise.TweenInterval(0.42f);
        rise.TweenProperty(phoenix, "position", phoenixDestination, 0.48f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        rise.Parallel().TweenProperty(phoenix, "scale", Vector3.One * 1.08f, 0.48f);
        rise.TweenProperty(phoenix, "scale", Vector3.One, 0.12f);
        rise.TweenCallback(Callable.From(() => PhoenixRiseCompleted = true));
        rise.TweenProperty(leftWing, "scale", Vector3.One, 0.25f).SetTrans(Tween.TransitionType.Back);
        rise.Parallel().TweenProperty(rightWing, "scale", Vector3.One, 0.25f).SetTrans(Tween.TransitionType.Back);
        rise.TweenCallback(Callable.From(() => WingsSpreadCompleted = true));

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
