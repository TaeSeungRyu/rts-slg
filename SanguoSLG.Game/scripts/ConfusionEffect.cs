using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 여러 물음표가 빙글빙글 도는 효과(design-effect.md #12). 머리 위 원형 궤도를 도는
/// 물음표 4개가 위아래로 살짝 까딱이며, 각자 카메라를 향해(요값만 회전) 똑바로 선다.
/// 물음표는 `prop-question.glb`. 궤도 각·까딱임은 인덱스에서 뽑아 난수를 쓰지 않는다(결정적).
/// </summary>
public partial class ConfusionEffect : Node3D
{
    public float S = 1f;

    private const int Count = 4;
    private const float SpinSpeed = 1.4f;   // 궤도 회전(rad/s)
    private const float Radius = 0.22f;     // 궤도 반경(*S)
    private const float Height = 0.66f;     // 머리 위 높이(*S)

    private readonly Node3D[] _marks = new Node3D[Count];
    private float _t;

    public override void _Ready()
    {
        var holder = new Node3D { Position = new Vector3(0f, Height * S, 0f) };
        AddChild(holder);

        var scene = GD.Load<PackedScene>("res://assets/models/prop-question.glb");
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(1f, 0.85f, 0.22f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };

        for (var k = 0; k < Count; k++)
        {
            var mark = scene.Instantiate<Node3D>();
            mark.Scale = new Vector3(0.88f * S, 0.88f * S, 0.88f * S);
            OverrideMaterial(mark, mat);
            holder.AddChild(mark);
            _marks[k] = mark;
        }
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        var cam = GetViewport()?.GetCamera3D();

        for (var k = 0; k < Count; k++)
        {
            var angle = _t * SpinSpeed + k * (Mathf.Tau / Count);
            var bob = Mathf.Sin(_t * 2.2f + k) * 0.03f * S;
            var mark = _marks[k];
            mark.Position = new Vector3(Mathf.Cos(angle) * Radius * S, bob, Mathf.Sin(angle) * Radius * S);

            // 카메라를 향해 요값만 돌린다 — 뒤집히지 않고 똑바로 선 채 정면을 보게.
            if (cam != null)
            {
                var dir = cam.GlobalPosition - mark.GlobalPosition;
                mark.Rotation = new Vector3(0f, Mathf.Atan2(dir.X, dir.Z), 0f);
            }
        }
    }

    private static void OverrideMaterial(Node node, Material mat)
    {
        if (node is MeshInstance3D mi)
        {
            mi.MaterialOverride = mat;
        }
        foreach (var child in node.GetChildren())
        {
            OverrideMaterial(child, mat);
        }
    }
}
