using Godot;

namespace SanguoSLG.Game;

/// <summary>Blender 청철 중갑이 시전자 부대를 감쌌다가 축소 소멸하는 철벽 효과.</summary>
public sealed partial class IronWallArmorEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-iron-wall.glb";

    public bool LoadedFromGlb { get; private set; }
    public int ChestPlateCount { get; private set; }
    public bool HasProtectionRing { get; private set; }
    public bool ArmorAppeared { get; private set; }
    public bool DisplayCompleted { get; private set; }

    public override void _Ready()
    {
        Position = new Vector3(0f, 0.68f, 0f);
        var scene = GD.Load<PackedScene>(AssetPath);
        if (scene is null)
        {
            GD.PushError($"철벽 GLB를 불러오지 못했습니다: {AssetPath}");
            QueueFree();
            return;
        }

        var visual = scene.Instantiate<Node3D>();
        visual.Name = "IronWallGlbVisual";
        AddChild(visual);
        LoadedFromGlb = true;
        ChestPlateCount = visual.FindChildren("IronWall_ChestPlate_*", "", true, false).Count;
        var ring = visual.FindChild("IronWall_ProtectionRing", true, false) as Node3D;
        HasProtectionRing = ring is not null;

        var baseScale = visual.Scale;
        visual.Scale = baseScale * 0.02f;
        var armorTween = CreateTween();
        armorTween.TweenProperty(visual, "scale", baseScale, 0.26f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        armorTween.TweenInterval(0.78f);
        armorTween.TweenProperty(visual, "scale", baseScale * 1.08f, 0.12f);
        armorTween.TweenProperty(visual, "scale", baseScale * 0.01f, 0.28f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);

        if (ring is not null)
        {
            var ringScale = ring.Scale;
            var ringTween = CreateTween().SetLoops(3);
            ringTween.TweenProperty(ring, "scale", ringScale * 1.12f, 0.18f);
            ringTween.TweenProperty(ring, "scale", ringScale * 0.94f, 0.18f);
        }

        var appeared = new Godot.Timer { OneShot = true, WaitTime = 0.30 };
        AddChild(appeared);
        appeared.Timeout += () => ArmorAppeared = true;
        appeared.Start();
        var completed = new Godot.Timer { OneShot = true, WaitTime = 1.48 };
        AddChild(completed);
        completed.Timeout += () => DisplayCompleted = true;
        completed.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.62 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }
}
