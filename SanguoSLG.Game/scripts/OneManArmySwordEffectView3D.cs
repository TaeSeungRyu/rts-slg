using System.Linq;
using Godot;

namespace SanguoSLG.Game;

/// <summary>일기당천 명중 대상의 하늘에서 편대 크기 약 60%의 대검이 1회 내리꽂히는 효과.</summary>
public sealed partial class OneManArmySwordEffectView3D : Node3D
{
    public int SwordCount { get; private set; }

    public override void _Ready()
    {
        var visual = GD.Load<PackedScene>("res://assets/models/effect-one-man-army-sword.glb").Instantiate<Node3D>();
        AddChild(visual);
        SwordCount = visual.FindChildren("OneManArmySword_*", "", true, false).Count;
        foreach (var player in FindPlayers(visual))
        {
            var animation = player.GetAnimationList().FirstOrDefault(x => x != "RESET");
            if (animation is not null) player.Play(animation);
        }

        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 1.35 };
        AddChild(cleanup);
        cleanup.Timeout += QueueFree;
        cleanup.Start();
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
