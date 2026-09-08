using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 병력이 전멸할 때 한 번만 재생하는 해골 상승 효과. Blender 제작 에셋
/// effect-rising-skulls.glb를 통째로 인스턴스하고 짧게 떠오르며 옅어지게 한다.
/// </summary>
public partial class RisingSkullsEffect : Node3D
{
    public float S = 1f;
    public bool Loop = true;

    private const float Period = 2.0f;
    private static PackedScene? _scene;

    private Node3D? _model;
    private float _t;

    public override void _Ready()
    {
        _scene ??= GD.Load<PackedScene>("res://assets/models/effect-rising-skulls.glb");
        _model = _scene.Instantiate<Node3D>();
        AddChild(_model);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        var cycle = Loop ? Mathf.PosMod(_t / Period, 1f) : _t / Period;
        if (!Loop && cycle >= 1f)
        {
            (GetParent() ?? (Node)this).QueueFree();
            SetProcess(false);
            return;
        }

        if (_model is null) { return; }

        var p = Mathf.Clamp(cycle, 0f, 1f);
        var lift = Mathf.SmoothStep(0f, 1f, p);
        var scale = (0.55f + 0.35f * Mathf.Sin(Mathf.Pi * p)) * S;
        _model.Position = new Vector3(0f, lift * 0.65f * S, 0f);
        _model.Rotation = new Vector3(0f, p * Mathf.Tau * 0.08f, 0f);
        _model.Scale = new Vector3(scale, scale, scale);

        var alpha = Mathf.Min(p / 0.18f, 1f) * Mathf.Min((1f - p) / 0.35f, 1f);
        SetAlpha(_model, Mathf.Clamp(alpha, 0f, 1f));
    }

    private static void SetAlpha(Node node, float alpha)
    {
        if (node is MeshInstance3D mi)
        {
            var mat = mi.MaterialOverride as StandardMaterial3D;
            if (mat is null)
            {
                mat = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.74f, 0.72f, 0.68f, alpha),
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                };
                mi.MaterialOverride = mat;
            }

            var c = mat.AlbedoColor;
            mat.AlbedoColor = new Color(c.R, c.G, c.B, alpha);
        }

        foreach (var child in node.GetChildren())
        {
            SetAlpha(child, alpha);
        }
    }
}
