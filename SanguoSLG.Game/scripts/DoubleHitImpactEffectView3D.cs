using System.Linq;
using Godot;

namespace SanguoSLG.Game;

/// <summary>Blender GLB의 금빛 충격핵이 좌우에서 짧은 시간차로 두 번 폭발하는 연타 효과.</summary>
public sealed partial class DoubleHitImpactEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-double-hit.glb";

    public bool LoadedFromGlb { get; private set; }
    public int ImpactCoreCount { get; private set; }
    public int RingCount { get; private set; }
    public int RayCount { get; private set; }
    public int CompletedImpactCount { get; private set; }

    public override void _Ready()
    {
        Position = new Vector3(0f, 0.22f, 0f);
        var scene = GD.Load<PackedScene>(AssetPath);
        if (scene is null)
        {
            GD.PushError($"연타 GLB를 불러오지 못했습니다: {AssetPath}");
            QueueFree();
            return;
        }

        var visual = scene.Instantiate<Node3D>();
        visual.Name = "DoubleHitGlbVisual";
        AddChild(visual);
        LoadedFromGlb = true;

        ImpactCoreCount = visual.FindChildren("DoubleHit_Core_*", "", true, false).Count;
        RingCount = visual.FindChildren("DoubleHit_Ring_*", "", true, false).Count;
        RayCount = visual.FindChildren("DoubleHit_Ray_*", "", true, false).Count;
        AnimateImpact(visual, 1, 0.02);
        AnimateImpact(visual, 2, 0.24);

        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.28 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    private void AnimateImpact(Node3D visual, int hit, double delay)
    {
        var core = visual.FindChild($"DoubleHit_Core_{hit}", true, false) as Node3D;
        if (core is null) return;
        var corePosition = core.Position;
        var coreScale = core.Scale;
        core.Scale = coreScale * 0.01f;
        var coreTween = CreateTween();
        coreTween.TweenInterval(delay);
        coreTween.TweenProperty(core, "scale", coreScale * 1.35f, 0.12f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        coreTween.TweenProperty(core, "scale", coreScale * 0.01f, 0.20f)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);

        foreach (var ring in visual.FindChildren($"DoubleHit_Ring_{hit}_*", "", true, false).OfType<Node3D>())
        {
            var baseScale = ring.Scale;
            ring.Scale = baseScale * 0.01f;
            var tween = CreateTween();
            tween.TweenInterval(delay + 0.06);
            tween.TweenProperty(ring, "scale", baseScale * 1.65f, 0.26f)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(ring, "scale", baseScale * 0.01f, 0.22f);
        }

        var rays = visual.FindChildren($"DoubleHit_Ray_{hit}_*", "", true, false).OfType<Node3D>().ToList();
        for (var index = 0; index < rays.Count; index++)
        {
            var ray = rays[index];
            var destination = ray.Position;
            var baseScale = ray.Scale;
            ray.Position = corePosition;
            ray.Scale = baseScale * 0.01f;
            var tween = CreateTween();
            tween.TweenInterval(delay + 0.05 + index * 0.006);
            tween.TweenProperty(ray, "scale", baseScale, 0.07f);
            tween.Parallel().TweenProperty(ray, "position", destination, 0.28f)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(ray, "scale", baseScale * 0.01f, 0.20f);
        }

        var completed = new Godot.Timer { OneShot = true, WaitTime = delay + 0.62 };
        AddChild(completed);
        completed.Timeout += () => CompletedImpactCount++;
        completed.Start();
    }
}
