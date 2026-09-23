using System.Linq;
using Godot;

namespace SanguoSLG.Game;

/// <summary>기존 effect-lightning.glb 애니메이션을 대상 위치에서 한 번 재생하는 낙뢰 효과.</summary>
public sealed partial class LightningGlbEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-lightning.glb";
    public bool LoadedFromGlb { get; private set; }
    public bool AnimationStarted { get; private set; }
    public bool AnimationCompleted { get; private set; }

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>(AssetPath);
        if (packed is null) { GD.PushError($"낙뢰 GLB를 불러오지 못했습니다: {AssetPath}"); QueueFree(); return; }
        var visual = packed.Instantiate<Node3D>();
        visual.Name = "LightningGlbVisual";
        AddChild(visual);
        visual.Scale = Vector3.One * 1.35f;
        LoadedFromGlb = true;
        var player = visual.FindChildren("*", "AnimationPlayer", true, false).OfType<AnimationPlayer>().FirstOrDefault();
        if (player is not null)
        {
            var animation = player.GetAnimationList().FirstOrDefault(name => name != "RESET");
            if (!string.IsNullOrEmpty(animation)) { player.Play(animation); AnimationStarted = true; }
        }
        if (!AnimationStarted) { GD.PushError("낙뢰 GLB에서 재생할 애니메이션을 찾지 못했습니다."); QueueFree(); return; }
        var completed = new Godot.Timer { OneShot = true, WaitTime = 0.82 };
        AddChild(completed);
        completed.Timeout += () => AnimationCompleted = true;
        completed.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.05 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }
}
