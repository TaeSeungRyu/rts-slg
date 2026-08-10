using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 물이 차오르는 효과(design-effect.md #4). 반투명 수면 판이 대상 발밑에서 위로 차올랐다
/// 다시 빠지기를 반복한다(코사인 보간이라 끊김·튐 없이 매끈하게 오르내린다).
/// <see cref="S"/>는 대상 크기 비례 스케일 — 붙이기 전에 설정한다.
/// </summary>
public partial class FloodEffect : Node3D
{
    public float S = 1f;

    private MeshInstance3D _water = null!;
    private float _t;

    public override void _Ready()
    {
        // 평면이 아니라 부피가 있는 물덩이 — 바닥(y=0)에 붙은 채 높이만 자라야
        // 옆(쿼터뷰)에서 얇은 선이 아니라 물이 차오르는 입체로 읽힌다.
        _water = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = Vector3.One },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.18f, 0.42f, 0.66f, 0.5f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.12f,
                Metallic = 0.2f,
            },
        };
        AddChild(_water);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        // 0 → 최고(0.35) → 0 을 매끈하게. 잔물결로 수면이 살짝 떨린다
        var level = (0.5f - 0.5f * Mathf.Cos(_t * 0.9f)) * 0.35f * S
            + Mathf.Sin(_t * 3.1f) * 0.008f * S;
        level = Mathf.Max(level, 0.001f);

        // 단위 박스를 폭은 그대로, 높이만 level로 — 바닥은 y=0에 고정하고 윗면(수면)만 오른다
        _water.Scale = new Vector3(1.0f * S, level, 1.0f * S);
        _water.Position = new Vector3(0f, level * 0.5f, 0f);
    }
}
