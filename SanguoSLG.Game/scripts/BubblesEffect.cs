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

    private const int Count = 7;
    private const float Period = 2.4f;    // 느리게 — 방울 하나의 생성~터짐 주기
    private const float TileR = 0.5774f;  // flat-top 타일 반경 — 방울을 타일 전체에 흩는다

    private readonly MeshInstance3D[] _bubbles = new MeshInstance3D[Count];
    private readonly Vector2[] _pos = new Vector2[Count];   // 타일 평면상 자리(XZ)
    private float _t;

    public override void _Ready()
    {
        var mesh = new SphereMesh
        {
            Radius = 0.055f * S,  // 기존 0.05에서 10%만 키움
            Height = 0.11f * S,
            RadialSegments = 10,
            Rings = 6,
            Material = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.06f, 0.42f, 0.10f, 0.62f), // 진한 초록
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.1f,
                EmissionEnabled = true,
                Emission = new Color(0.05f, 0.24f, 0.08f),
            },
        };

        for (var i = 0; i < Count; i++)
        {
            var bubble = new MeshInstance3D { Mesh = mesh, Visible = false };
            AddChild(bubble);
            _bubbles[i] = bubble;

            // 한 점 집중이 아니라 타일 전체에 흩는다. 황금각으로 인덱스에서 자리를 뽑아
            // 난수 없이도 겹치지 않게 퍼뜨린다(반지름은 안쪽으로 조금 당김).
            var angle = i * 2.399963f;
            var r = Mathf.Sqrt((i + 0.5f) / Count) * TileR * 0.82f * S;
            _pos[i] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
        }
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        for (var i = 0; i < Count; i++)
        {
            // 위상을 인덱스로 흩어 한꺼번에 터지지 않게 한다
            var cycle = Mathf.PosMod(_t / Period + i * 0.61803f, 1f);
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
                size = cycle / 0.18f;                          // 부풀며 나타남
            }
            else if (cycle < 0.82f)
            {
                size = 1f;                                     // 떠오르며 유지
            }
            else
            {
                size = 1f + (cycle - 0.82f) / 0.14f * 0.7f;    // 끝에 급히 부풀어 터진다
            }

            bubble.Visible = size > 0.02f;
            bubble.Position = new Vector3(_pos[i].X, 0.03f * S + cycle * 0.15f * S, _pos[i].Y);
            bubble.Scale = new Vector3(size, size, size);
        }
    }
}
