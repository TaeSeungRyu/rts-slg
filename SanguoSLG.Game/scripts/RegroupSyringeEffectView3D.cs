using Godot;

namespace SanguoSLG.Game;

/// <summary>붉은 약액이 든 주사기의 피스톤이 눌리며 약액이 줄어드는 정비 GLB 효과.</summary>
public sealed partial class RegroupSyringeEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-regroup.glb";
    private Node3D? _anchor;

    public bool LoadedFromGlb { get; private set; }
    public bool PlungerPressed { get; private set; }
    public bool LiquidEmptied { get; private set; }
    public bool IsScreenAligned { get; private set; }

    public override void _Ready()
    {
        _anchor = GetParentOrNull<Node3D>();
        var anchorPosition = _anchor?.GlobalPosition ?? GlobalPosition;
        TopLevel = true;
        GlobalPosition = anchorPosition + Vector3.Up * 0.72f;
        AlignToCamera();

        var packed = GD.Load<PackedScene>(AssetPath);
        if (packed is null)
        {
            GD.PushError($"정비 GLB를 불러오지 못했습니다: {AssetPath}");
            QueueFree();
            return;
        }
        var visual = packed.Instantiate<Node3D>();
        visual.Name = "RegroupSyringeGlbVisual";
        AddChild(visual);
        visual.Scale = Vector3.One * 0.64f;
        LoadedFromGlb = true;

        var plunger = visual.FindChild("Regroup_PlungerGroup", true, false) as Node3D;
        var liquid = visual.FindChild("Regroup_Liquid", true, false) as Node3D;
        if (plunger is null || liquid is null)
        {
            GD.PushError("정비 주사기의 피스톤 또는 약액 노드를 찾지 못했습니다.");
            QueueFree();
            return;
        }

        visual.Scale = Vector3.One * 0.02f;
        var reveal = CreateTween();
        reveal.TweenProperty(visual, "scale", Vector3.One * 0.67f, 0.20f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        reveal.TweenProperty(visual, "scale", Vector3.One * 0.64f, 0.08f);

        var originalLiquidScale = liquid.Scale;
        var originalLiquidPosition = liquid.Position;
        var press = CreateTween();
        press.TweenInterval(0.34f);
        press.TweenProperty(plunger, "position", plunger.Position + Vector3.Right * 0.43f, 0.58f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        press.Parallel().TweenProperty(liquid, "scale", new Vector3(originalLiquidScale.X, originalLiquidScale.Y, 0.035f), 0.58f);
        press.Parallel().TweenProperty(liquid, "position", originalLiquidPosition + Vector3.Right * 0.22f, 0.58f);
        press.TweenCallback(Callable.From(() =>
        {
            PlungerPressed = true;
            LiquidEmptied = true;
        }));
        press.TweenInterval(0.28f);
        press.TweenProperty(visual, "scale", Vector3.One * 0.01f, 0.22f);

        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.60 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    public override void _Process(double delta)
    {
        if (IsInstanceValid(_anchor)) GlobalPosition = _anchor!.GlobalPosition + Vector3.Up * 0.72f;
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
