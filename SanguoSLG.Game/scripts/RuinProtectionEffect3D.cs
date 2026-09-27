using Godot;

namespace SanguoSLG.Game;

/// <summary>점령 직후 30일 보호기간 동안 유적을 감싸는 지속 보호막.</summary>
public sealed partial class RuinProtectionEffect3D : Node3D
{
    private Node3D _model = null!;
    private float _time;

    public override void _Ready()
    {
        var packed = GD.Load<PackedScene>("res://assets/models/effect-ruin-protection.glb");
        if (packed is null)
        {
            GD.PushError("유적 보호막 에셋을 불러오지 못했습니다.");
            SetProcess(false);
            return;
        }
        _model = packed.Instantiate<Node3D>();
        _model.Name = "ProtectionBarrierModel";
        AddChild(_model);
    }

    public override void _Process(double delta)
    {
        if (_model is null) return;
        _time += (float)delta;
        _model.Rotation = new Vector3(0f, _time * 0.42f, 0f);
        var pulse = 0.96f + Mathf.Sin(_time * 2.4f) * 0.04f;
        _model.Scale = Vector3.One * pulse;
    }
}
