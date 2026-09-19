using System.Linq;
using Godot;

namespace SanguoSLG.Game;

/// <summary>Blender 제작 에셋을 사용하는 액티브 준비 연출. 발동 시 시전자 위에서 Burst로 전환한다.</summary>
public sealed partial class ActiveSkillChargeView3D : Node3D
{
    private Node3D _visual = null!;

    public override void _Ready()
    {
        Position = new Vector3(0f, 0.72f, 0f);
        _visual = GD.Load<PackedScene>("res://assets/models/effect-skill-charge.glb").Instantiate<Node3D>();
        AddChild(_visual);
        foreach (var player in FindPlayers(_visual))
        {
            var names = player.GetAnimationList();
            var name = names.FirstOrDefault(x => x != "RESET");
            if (name is not null) player.Play(name);
        }
    }

    public override void _Process(double delta)
    {
        // 가져온 애니메이션이 없는 환경에서도 준비 상태가 멈춰 보이지 않게 한다.
        if (IsInstanceValid(_visual)) _visual.RotateY((float)delta * 1.8f);
    }

    public void Complete()
    {
        if (GetParent() is Node3D caster) ActiveSkillPresentation.ShowCasterActivation(caster);
        var tween = CreateTween();
        tween.TweenProperty(this, "scale", Vector3.One * 1.65f, 0.18f);
        tween.TweenProperty(this, "scale", Vector3.Zero, 0.16f);
        tween.Finished += QueueFree;
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
