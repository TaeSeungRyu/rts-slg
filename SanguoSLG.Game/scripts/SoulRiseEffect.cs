using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 영혼 모양이 땅에서 위로 솟아오르는 효과(design-effect.md #13). <b>유닛 전용</b> —
/// 부대 전멸(병력 0) 소멸 연출. 반투명 청백색 영혼(prop-soul.glb) 셋이 발밑에서 천천히
/// 떠올라 바깥으로 흩어지며 위로 갈수록 옅어져 사라진다. 실사용은 1회 재생 후 부착 루트째
/// 스스로 정리하고(<see cref="Loop"/>=false), 검수용은 주기마다 반복한다.
/// 자리·위상은 인덱스에서 뽑아 난수를 쓰지 않는다(결정적).
/// </summary>
public partial class SoulRiseEffect : Node3D
{
    public float S = 1f;
    public bool Loop = true;

    private const int Count = 3;
    private const float Period = 2.4f;
    private const float Stagger = 0.4f;
    private const float RiseHeight = 0.85f;

    private static PackedScene? _scene;

    private readonly Node3D[] _souls = new Node3D[Count];
    private readonly StandardMaterial3D[] _mats = new StandardMaterial3D[Count];
    private float _t;

    public override void _Ready()
    {
        _scene ??= GD.Load<PackedScene>("res://assets/models/prop-soul.glb");
        for (var i = 0; i < Count; i++)
        {
            var soul = _scene.Instantiate<Node3D>();
            soul.Visible = false;
            // 알파를 영혼마다 따로 굴려야 하므로 재질을 공유하지 않는다
            _mats[i] = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.72f, 0.88f, 1f, 0f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            };
            OverrideMaterial(soul, _mats[i]);
            AddChild(soul);
            _souls[i] = soul;
        }
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (!Loop && _t >= (Count - 1) * Stagger + Period)
        {
            (GetParent() ?? (Node)this).QueueFree();
            SetProcess(false);
            return;
        }

        var cam = GetViewport()?.GetCamera3D();
        for (var i = 0; i < Count; i++)
        {
            var local = (_t - i * Stagger) / Period;
            var cycle = Loop ? Mathf.PosMod(local, 1f) : local;
            var soul = _souls[i];
            if (cycle < 0f || cycle >= 1f)
            {
                soul.Visible = false;
                continue;
            }

            var alpha = Mathf.Min(cycle / 0.15f, 1f) * Mathf.Min((1f - cycle) / 0.5f, 1f) * 0.72f;
            var c = _mats[i].AlbedoColor;
            _mats[i].AlbedoColor = new Color(c.R, c.G, c.B, Mathf.Clamp(alpha, 0f, 1f));

            var angle = i * (Mathf.Tau / Count) + 0.6f;
            var drift = (0.05f + 0.17f * cycle) * S;
            var sway = Mathf.Sin(cycle * 7f + i * 2f) * 0.025f * S;
            soul.Visible = true;
            soul.Position = new Vector3(
                Mathf.Cos(angle) * drift + sway,
                0.02f * S + cycle * RiseHeight * S,
                Mathf.Sin(angle) * drift);

            // 카메라를 향해 요값만 돌린다. LookAt은 스케일을 리셋하므로 쓰지 않는다.
            if (cam is not null)
            {
                var dir = cam.GlobalPosition - soul.GlobalPosition;
                soul.Rotation = new Vector3(0f, Mathf.Atan2(dir.X, dir.Z), 0f);
            }

            var size = (i == 0 ? 1.5f : 1.0f) * (0.85f + 0.35f * cycle) * S;
            soul.Scale = new Vector3(size, size, size);
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
