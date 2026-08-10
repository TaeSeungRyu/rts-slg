using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 작은 파리 여러 마리가 대상 주위를 불규칙하게 날아다니는 효과(design-effect.md #3).
/// 파티클은 직선으로만 움직여 "붕붕" 나는 느낌이 안 나므로, 파리마다 서로 다른 주기·위상의
/// 리사주 궤도 + 고주파 버즈로 노드를 움직인다. 조명이 없어 가볍다.
/// 위상은 인덱스에서 뽑아 난수를 쓰지 않는다(표현 전용이라 결정론엔 무관하지만 단순하다).
/// <see cref="S"/>는 대상 크기 비례 스케일 — 붙이기 전에 설정한다.
/// </summary>
public partial class FliesEffect : Node3D
{
    public float S = 1f;

    private const int Count = 10;
    private readonly MeshInstance3D[] _flies = new MeshInstance3D[Count];
    private float _t;

    public override void _Ready()
    {
        var mesh = new SphereMesh
        {
            Radius = 0.0086f * S,
            Height = 0.0173f * S,
            RadialSegments = 5,
            Rings = 3,
            Material = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.06f, 0.06f, 0.07f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };

        for (var i = 0; i < Count; i++)
        {
            var fly = new MeshInstance3D { Mesh = mesh, Scale = new Vector3(1.7f, 0.7f, 1f) };
            AddChild(fly);
            _flies[i] = fly;
        }
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        for (var i = 0; i < Count; i++)
        {
            var r = (0.09f + (i % 3) * 0.03f) * S;
            var h = (0.11f + (i % 2) * 0.06f) * S;
            var buzz = Mathf.Sin(_t * 22f + i) * 0.006f * S;

            _flies[i].Position = new Vector3(
                r * Mathf.Sin(_t * (1.7f + i * 0.31f) + i * 1.3f) + buzz,
                h + 0.03f * S * Mathf.Sin(_t * (2.3f + i * 0.19f) + i * 2.1f),
                r * Mathf.Cos(_t * (1.9f + i * 0.27f) + i * 0.7f) + buzz);
        }
    }
}
