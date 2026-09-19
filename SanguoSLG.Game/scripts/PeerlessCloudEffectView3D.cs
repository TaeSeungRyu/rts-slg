using System.Linq;
using Godot;

namespace SanguoSLG.Game;

/// <summary>무쌍 명중점에서 한 번 터지는 Blender 붉은 구름 효과.</summary>
public sealed partial class PeerlessCloudEffectView3D : Node3D
{
    public override void _Ready()
    {
        Position = new Vector3(0f, 0.34f, 0f);
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
