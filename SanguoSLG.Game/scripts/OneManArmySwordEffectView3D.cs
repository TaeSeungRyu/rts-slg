using System.Linq;
using Godot;

namespace SanguoSLG.Game;

/// <summary>일기당천 명중 대상의 하늘에서 편대 크기 약 60%의 대검이 1회 내리꽂히는 효과.</summary>
public sealed partial class OneManArmySwordEffectView3D : Node3D
{
    public int SwordCount { get; private set; }
    public int AnimationClipCount { get; private set; }

    public override void _Ready()
    {
        var scene = GD.Load<PackedScene>("res://assets/models/effect-one-man-army-sword.glb");
        // glTF는 Blender 오브젝트별 Action을 별도 클립으로 가져온다. 한 인스턴스의 AnimationPlayer에서
        // 클립 하나만 재생하면 첫 검만 보이므로, 검마다 독립 인스턴스를 두고 해당 클립을 시간차 재생한다.
        for (var swordIndex = 0; swordIndex < 3; swordIndex++)
        {
            var visual = scene.Instantiate<Node3D>();
            visual.Visible = false;
            AddChild(visual);
            var clips = FindPlayers(visual)
                .SelectMany(player => player.GetAnimationList()
                    .Where(name => name != "RESET" && name.ToString().Contains("OneManArmySword"))
                    .Select(name => (Player: player, Name: name)))
                .ToList();
            AnimationClipCount = System.Math.Max(AnimationClipCount, clips.Count);
            if (clips.Count == 0) continue;
            var clip = clips[Mathf.Min(swordIndex, clips.Count - 1)];
            ScheduleSword(visual, clip.Player, clip.Name, swordIndex * 0.12);
            SwordCount++;
        }

        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.60 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
    }

    private void ScheduleSword(Node3D visual, AnimationPlayer player, StringName animation, double delay)
    {
        var timer = new Godot.Timer { OneShot = true, WaitTime = System.Math.Max(0.001, delay) };
        AddChild(timer);
        timer.Timeout += () =>
        {
            visual.Visible = true;
            player.Play(animation);
            timer.QueueFree();
        };
        timer.Start();
    }

    private static System.Collections.Generic.IEnumerable<AnimationPlayer> FindPlayers(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is AnimationPlayer player) yield return player;
            foreach (var nested in FindPlayers(child)) yield return nested;
        }
    }
}
