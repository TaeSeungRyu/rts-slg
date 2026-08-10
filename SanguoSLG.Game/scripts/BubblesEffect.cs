using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 초록색 방울이 바닥에서 하나씩 나타났다 부풀어 터지는 효과(design-effect.md #7).
/// 방울마다 위상을 어긋낸 느린 주기: 부풀며 살짝 떠오르다 끝에 급히 커진 뒤(터짐) 사라진다.
/// 위상·자리는 인덱스에서 뽑아 난수를 쓰지 않는다.
/// </summary>
public partial class BubblesEffect : Node3D
{
    public float S = 1f;

    private const int Count = 3;
    private const float Period = 2.2f; // 느리게 — 방울 하나의 생성~터짐 주기

    private readonly MeshInstance3D[] _bubbles = new MeshInstance3D[Count];
    private float _t;

    public override void _Ready()
    {
        var mesh = new SphereMesh
        {
            Radius = 0.05f * S,
            Height = 0.10f * S,
            RadialSegments = 10,
            Rings = 6,
            Material = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.26f, 0.76f, 0.32f, 0.5f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.1f,
                EmissionEnabled = true,
                Emission = new Color(0.12f, 0.40f, 0.16f),
            },
        };

        for (var i = 0; i < Count; i++)
        {
            var bubble = new MeshInstance3D { Mesh = mesh, Visible = false };
            AddChild(bubble);
            _bubbles[i] = bubble;
        }
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        for (var i = 0; i < Count; i++)
        {
            var cycle = Mathf.PosMod(_t / Period + i / (float)Count, 1f);
            var bubble = _bubbles[i];

            // 터진 뒤 잠깐 사라져 있는다(다음 주기 시작 전)
            if (cycle >= 0.96f)
            {
                bubble.Visible = false;
                continue;
            }

            float size;
            if (cycle < 0.18f)
            {
                size = cycle / 0.18f; // 방울이 부풀며 나타남
            }
            else if (cycle < 0.82f)
            {
                size = 1f;            // 떠오르며 유지
            }
            else
            {
                size = 1f + (cycle - 0.82f) / 0.14f * 0.7f; // 끝에 급히 부풀어 터진다
            }

            var laneX = (i - (Count - 1) * 0.5f) * 0.08f * S;
            var laneZ = ((i % 2) * 2 - 1) * 0.05f * S;
            bubble.Visible = size > 0.02f;
            bubble.Position = new Vector3(laneX, 0.03f * S + cycle * 0.15f * S, laneZ);
            bubble.Scale = new Vector3(size, size, size);
        }
    }
}
