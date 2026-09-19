using System.Linq;
using Godot;

namespace SanguoSLG.Game;

/// <summary>무쌍 명중점에서 한 번 터지는 Blender 붉은 구름 효과.</summary>
public sealed partial class PeerlessCloudEffectView3D : Node3D
{
    public override void _Ready()
    {
        // 편대 머리 위가 아니라 병사들 사이, 발밑보다 살짝 높은 중심부에서 연속 폭발한다.
        Position = new Vector3(0f, 0.10f, 0f);
        Scale = Vector3.One * 0.72f;
        var visual = GD.Load<PackedScene>("res://assets/models/effect-peerless-red-cloud.glb").Instantiate<Node3D>();
        AddChild(visual);
        foreach (var player in FindPlayers(visual))
        {
            var animation = player.GetAnimationList().FirstOrDefault(x => x != "RESET");
            if (animation is not null) player.Play(animation);
        }
        var cleanup = GetTree().CreateTimer(1.35);
        cleanup.Timeout += QueueFree;
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
