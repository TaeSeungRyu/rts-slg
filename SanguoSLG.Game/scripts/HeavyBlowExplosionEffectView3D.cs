using System.Linq;
using Godot;

namespace SanguoSLG.Game;

/// <summary>검은 철제 폭탄 GLB가 적 편대 내부에서 파편·암흑 연기와 함께 터지는 강타 효과.</summary>
public sealed partial class HeavyBlowExplosionEffectView3D : Node3D
{
    private const string AssetPath = "res://assets/models/effect-heavy-blow.glb";

    public bool LoadedFromGlb { get; private set; }
    public int BombPartCount { get; private set; }
    public int ShardCount { get; private set; }
    public int SmokeCount { get; private set; }
    public bool ExplosionCompleted { get; private set; }

    public override void _Ready()
    {
        Position = new Vector3(0f, 0.18f, 0f);
        var scene = GD.Load<PackedScene>(AssetPath);
        if (scene is null)
        {
            GD.PushError($"강타 GLB를 불러오지 못했습니다: {AssetPath}");
            QueueFree();
            return;
        }

        var visual = scene.Instantiate<Node3D>();
        visual.Name = "HeavyBlowGlbVisual";
        AddChild(visual);
        LoadedFromGlb = true;

        var bombParts = visual.FindChildren("HeavyBlow_Bomb*", "", true, false).OfType<Node3D>()
            .Concat(visual.FindChildren("HeavyBlow_Fuse*", "", true, false).OfType<Node3D>()).ToList();
        BombPartCount = bombParts.Count;
        foreach (var part in bombParts)
        {
            var baseScale = part.Scale;
            part.Scale = baseScale * 0.02f;
            var tween = CreateTween();
            tween.TweenProperty(part, "scale", baseScale, 0.16f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.TweenInterval(0.18f);
            tween.TweenProperty(part, "scale", baseScale * 1.22f, 0.08f);
            tween.TweenProperty(part, "scale", baseScale * 0.01f, 0.07f);
        }

        var shards = visual.FindChildren("HeavyBlow_Shard_*", "", true, false).OfType<Node3D>().ToList();
        ShardCount = shards.Count;
        for (var index = 0; index < shards.Count; index++)
        {
            var shard = shards[index];
            var destination = shard.Position;
            var baseScale = shard.Scale;
            shard.Position = new Vector3(0f, 0.28f, 0f);
            shard.Scale = baseScale * 0.01f;
            var tween = CreateTween();
            tween.TweenInterval(0.38f + index * 0.008f);
            tween.TweenProperty(shard, "scale", baseScale, 0.07f);
            tween.Parallel().TweenProperty(shard, "position", destination, 0.38f)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(shard, "rotation", shard.Rotation + new Vector3(1.2f, 1.6f, 1.0f), 0.38f);
            tween.TweenProperty(shard, "scale", baseScale * 0.01f, 0.34f);
        }

        var smoke = visual.FindChildren("HeavyBlow_Smoke_*", "", true, false).OfType<Node3D>().ToList();
        SmokeCount = smoke.Count;
        for (var index = 0; index < smoke.Count; index++)
        {
            var puff = smoke[index];
            var destination = puff.Position;
            var baseScale = puff.Scale;
            puff.Position = new Vector3(0f, 0.27f, 0f);
            puff.Scale = baseScale * 0.01f;
            var tween = CreateTween();
            tween.TweenInterval(0.34f + index * 0.022f);
            tween.TweenProperty(puff, "scale", baseScale * 1.18f, 0.22f)
                .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(puff, "position", destination + Vector3.Up * 0.12f, 0.44f);
            tween.TweenProperty(puff, "scale", baseScale * 0.02f, 0.43f);
        }

        var ring = visual.FindChild("HeavyBlow_Shockwave", true, false) as Node3D;
        if (ring is not null)
        {
            var baseScale = ring.Scale;
            ring.Scale = baseScale * 0.01f;
            var tween = CreateTween();
            tween.TweenInterval(0.37f);
            tween.TweenProperty(ring, "scale", baseScale * 2.35f, 0.36f)
                .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(ring, "scale", baseScale * 0.01f, 0.26f);
        }

        var completed = new Godot.Timer { OneShot = true, WaitTime = 1.12 };
        AddChild(completed);
        completed.Timeout += () => ExplosionCompleted = true;
        completed.Start();
        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.48 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }
}
