using System.Linq;
using Godot;

namespace SanguoSLG.Game;

/// <summary>보급 상자가 열리고 군량 꾸러미가 아군 진형으로 전달되는 보급 GLB 효과.</summary>
public sealed partial class ResupplyCrateEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-resupply.glb";
    private Node3D? _anchor;

    public bool LoadedFromGlb { get; private set; }
    public bool LidOpened { get; private set; }
    public int BundleCount { get; private set; }
    public int DeliveredBundleCount { get; private set; }
    public bool IsScreenAligned { get; private set; }

    public override void _Ready()
    {
        _anchor = GetParentOrNull<Node3D>();
        var anchorPosition = _anchor?.GlobalPosition ?? GlobalPosition;
        TopLevel = true;
        GlobalPosition = anchorPosition + Vector3.Up * 0.48f;
        AlignToCamera();

        var packed = GD.Load<PackedScene>(AssetPath);
        if (packed is null)
        {
            GD.PushError($"보급 GLB를 불러오지 못했습니다: {AssetPath}");
            QueueFree();
            return;
        }
        var visual = packed.Instantiate<Node3D>();
        visual.Name = "ResupplyCrateGlbVisual";
        AddChild(visual);
        visual.Scale = Vector3.One * 0.66f;
        LoadedFromGlb = true;

        var lid = visual.FindChild("Resupply_LidGroup", true, false) as Node3D;
        var bundles = visual.FindChildren("Resupply_BundleGroup_*", "", true, false)
            .OfType<Node3D>().OrderBy(node => node.Name.ToString()).ToList();
        BundleCount = bundles.Count;
        if (lid is null || bundles.Count == 0)
        {
            GD.PushError("보급 상자의 뚜껑 또는 군량 꾸러미 노드를 찾지 못했습니다.");
            QueueFree();
            return;
        }

        foreach (var bundle in bundles) bundle.Scale = Vector3.One * 0.01f;
        var open = CreateTween();
        open.TweenProperty(lid, "position", lid.Position + Vector3.Up * 0.20f, 0.20f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        open.Parallel().TweenProperty(lid, "rotation", new Vector3(-0.48f, 0f, 0f), 0.20f);
        open.TweenCallback(Callable.From(() => LidOpened = true));

        for (var index = 0; index < bundles.Count; index++)
        {
            var bundle = bundles[index];
            var origin = bundle.Position;
            var tween = CreateTween();
            tween.TweenInterval(0.18f + index * 0.10f);
            tween.TweenProperty(bundle, "scale", Vector3.One * 0.82f, 0.12f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(bundle, "position", origin + Vector3.Up * (0.40f + index % 2 * 0.08f), 0.38f);
            tween.TweenProperty(bundle, "position", origin + Vector3.Up * 0.62f, 0.18f);
            tween.Parallel().TweenProperty(bundle, "scale", Vector3.One * 0.01f, 0.18f);
            tween.TweenCallback(Callable.From(() => DeliveredBundleCount++));
        }

        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.55 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(_anchor)) GlobalPosition = _anchor!.GlobalPosition + Vector3.Up * 0.48f;
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
