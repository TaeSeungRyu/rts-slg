using System.Linq;
using Godot;

namespace SanguoSLG.Game;

/// <summary>일섬 명중점의 부대 중앙을 빠르게 가로지르는 1회성 칼선과 잔상.</summary>
public sealed partial class FlashSlashEffectView3D : Node3D
{
    public int SlashCount { get; private set; }

    public override void _Ready()
    {
        var visual = GD.Load<PackedScene>("res://assets/models/effect-flash-slash.glb").Instantiate<Node3D>();
        AddChild(visual);
        SlashCount = visual.FindChildren("FlashSlash_*", "", true, false).Count;
        foreach (var player in FindPlayers(visual))
        {
            var animation = player.GetAnimationList().FirstOrDefault(x => x != "RESET");
            if (animation is not null) player.Play(animation);
        }

        var cleanup = new Godot.Timer { OneShot = true, WaitTime = 0.95 };
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
