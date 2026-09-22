using System.Collections.Generic;
using System.Linq;
using Godot;

namespace SanguoSLG.Game;

/// <summary>Blender 판금 갑옷 GLB가 나타난 뒤 부위별로 깨져 흩어지는 파갑 1회성 효과.</summary>
public sealed partial class ArmorBreakEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-armor-break.glb";
    private readonly List<Node3D> _fragments = [];

    public int FragmentCount => _fragments.Count;
    public bool LoadedFromGlb { get; private set; }
    public bool ArmorAppeared { get; private set; }
    public bool BreakCompleted { get; private set; }
    public float MaxScatterDistance { get; private set; }
    public float ArmorHoldSeconds => 0.72f;

    public override void _Ready()
    {
        Position = new Vector3(0f, 0.67f, 0f);
        var scene = GD.Load<PackedScene>(AssetPath);
        if (scene is null)
        {
            GD.PushError($"파갑 GLB를 불러오지 못했습니다: {AssetPath}");
            QueueFree();
            return;
        }

        var visual = scene.Instantiate<Node3D>();
        visual.Name = "ArmorBreakGlbVisual";
        AddChild(visual);
        LoadedFromGlb = true;

        var cracks = visual.FindChildren("ArmorBreak_Crack_*", "", true, false).OfType<Node3D>().ToList();
        foreach (var crack in cracks)
        {
            var baseScale = crack.Scale;
            crack.Scale = baseScale * 0.001f;
            var crackTween = CreateTween();
            crackTween.TweenInterval(1.03f);
            crackTween.TweenProperty(crack, "scale", baseScale * 1.18f, 0.12f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            crackTween.TweenInterval(0.34f);
            crackTween.TweenProperty(crack, "scale", baseScale * 0.001f, 0.20f);
        }

        var parts = visual.FindChildren("ArmorBreak_*", "", true, false)
            .OfType<Node3D>()
            .Where(node => !node.Name.ToString().StartsWith("ArmorBreak_Crack_"))
            .ToList();
        for (var index = 0; index < parts.Count; index++) AnimateFragment(parts[index], index);

        var appeared = new Godot.Timer { OneShot = true, WaitTime = 0.32 };
        AddChild(appeared);
        appeared.Timeout += () => ArmorAppeared = true;
        appeared.Start();
        var broken = new Godot.Timer { OneShot = true, WaitTime = 1.92 };
        AddChild(broken);
        broken.Timeout += () => BreakCompleted = true;
        broken.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 2.45 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    private void AnimateFragment(Node3D fragment, int index)
    {
        _fragments.Add(fragment);
        var origin = fragment.Position;
        var baseScale = fragment.Scale;
        var horizontal = Mathf.Abs(origin.X) > 0.015f ? Mathf.Sign(origin.X) : index % 2 == 0 ? -1f : 1f;
        var vertical = origin.Y >= 0f ? 1f : -0.65f;
        var distance = 0.13f + (index % 4) * 0.018f;
        var scatter = new Vector3(horizontal * distance, vertical * distance * 0.56f, 0.055f + (index % 3) * 0.018f);
        MaxScatterDistance = Mathf.Max(MaxScatterDistance, scatter.Length());
        fragment.Scale = baseScale * 0.02f;

        var tween = CreateTween();
        tween.TweenProperty(fragment, "scale", baseScale, 0.28f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenInterval(ArmorHoldSeconds);
        tween.TweenProperty(fragment, "position", origin + scatter, 0.70f)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(fragment, "rotation", fragment.Rotation + new Vector3(scatter.Y * 4f, scatter.X * 4f, horizontal * 0.8f), 0.70f);
        tween.Parallel().TweenProperty(fragment, "scale", baseScale * 0.06f, 0.76f);
    }
}
