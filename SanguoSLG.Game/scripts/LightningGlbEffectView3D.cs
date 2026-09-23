using Godot;

namespace SanguoSLG.Game;

/// <summary>기존 effect-lightning.glb 애니메이션을 대상 위치에서 한 번 재생하는 낙뢰 효과.</summary>
public sealed partial class LightningGlbEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-lightning.glb";
    public bool LoadedFromGlb { get; private set; }
    public bool AnimationStarted { get; private set; }
    public bool AnimationCompleted { get; private set; }
    public int WhiteZigzagCount { get; private set; }

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(AssetPath);
        if (packed is null) { GD.PushError($"낙뢰 GLB를 불러오지 못했습니다: {AssetPath}"); QueueFree(); return; }
        var visual = packed.Instantiate<Node3D>();
        visual.Name = "LightningGlbVisual";
        AddChild(visual);
        visual.Scale = Vector3.One * 1.65f;
        WhiteZigzagCount = visual.FindChildren("white_zigzag_*", "", true, false).Count;
        LoadedFromGlb = true;
        var flashes = new[] { "flash_1", "flash_2", "flash_3" };
        for (var index = 0; index < flashes.Length; index++)
        {
            var flash = visual.FindChild(flashes[index], true, false) as Node3D;
            if (flash is null) continue;
            var destination = flash.Position;
            flash.Position = destination + Vector3.Up * 0.72f;
            flash.Scale = new Vector3(1f, 0.03f, 1f);
            flash.Visible = false;
            var tween = CreateTween();
            tween.TweenInterval(index * 0.34f);
            tween.TweenCallback(Callable.From(() => flash.Visible = true));
            tween.TweenProperty(flash, "position", destination, 0.24f)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(flash, "scale", Vector3.One, 0.24f);
            tween.TweenInterval(0.22f);
            tween.TweenProperty(flash, "scale", new Vector3(0.7f, 0.05f, 0.7f), 0.10f);
            tween.TweenCallback(Callable.From(() => flash.Visible = false));
            AnimationStarted = true;
        }
        var impact = visual.FindChild("impact", true, false) as Node3D;
        if (impact is not null)
        {
            impact.Scale = Vector3.One * 0.02f;
            var impactTween = CreateTween();
            impactTween.TweenInterval(0.18f);
            impactTween.TweenProperty(impact, "scale", Vector3.One * 1.2f, 0.85f);
            impactTween.TweenProperty(impact, "scale", Vector3.One * 0.02f, 0.35f);
        }
        if (!AnimationStarted) { GD.PushError("낙뢰 GLB에서 flash_1~3 노드를 찾지 못했습니다."); QueueFree(); return; }
        var completed = new Godot.Timer { OneShot = true, WaitTime = 1.30 };
        AddChild(completed);
        completed.Timeout += () => AnimationCompleted = true;
        completed.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 2.05 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }
}
