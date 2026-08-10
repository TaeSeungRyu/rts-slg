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
        // 부피가 있는 물덩이(바닥 고정, 높이만 자람) — 옆에서 얇은 선이 아니라 입체로 읽힌다.
        // 타일이 육각이라 사각 박스는 모서리가 삐져나온다 → 6분할 원기둥 = 육각 기둥으로.
        // 회전 없이 타일 방향과 맞는다(호버 하이라이트와 같은 규약: 꼭짓점 ±Z).
        _water = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = 0.55f * S,
                BottomRadius = 0.55f * S,
                Height = 1f,
                RadialSegments = 6,
            },
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

        // 반경은 메시에 이미 있으니 높이(y)만 level로 — 바닥은 y=0 고정, 윗면(수면)만 오른다
        _water.Scale = new Vector3(1f, level, 1f);
        _water.Position = new Vector3(0f, level * 0.5f, 0f);
    }
}
