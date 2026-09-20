using System.Linq;
using Godot;

namespace SanguoSLG.Game;

/// <summary>일섬 명중점의 부대 중앙을 빠르게 가로지르는 1회성 칼선과 잔상.</summary>
public sealed partial class FlashSlashEffectView3D : Node3D
{
    public int SlashCount { get; private set; }
    public bool HasCrescentBlade { get; private set; }
    public int SparkCount { get; private set; }
    public float FadeDuration => 0.55f;

    public override void _Ready()
    {
        var visual = GD.Load<PackedScene>("res://assets/models/effect-flash-slash.glb").Instantiate<Node3D>();
        AddChild(visual);
        SlashCount = visual.FindChildren("FlashSlash_*", "", true, false).Count;
        HasCrescentBlade = visual.FindChild("FlashSlash_MainCrescent", true, false) is not null;
        SparkCount = visual.FindChildren("FlashSlash_Spark_*", "", true, false).Count;
        foreach (var player in FindPlayers(visual))
        {
            var animation = player.GetAnimationList().FirstOrDefault(x => x != "RESET");
            if (animation is not null) player.Play(animation);
        }

        // 펼쳐진 검광을 잠시 읽을 수 있게 유지한 뒤, 크기를 접지 않고 투명도로 사라지게 한다.
        foreach (var mesh in visual.FindChildren("*", "", true, false).OfType<GeometryInstance3D>())
        {
            mesh.Transparency = 0f;
            var fade = CreateTween();
            fade.TweenInterval(0.72f);
            fade.TweenProperty(mesh, "transparency", 1f, FadeDuration);
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
