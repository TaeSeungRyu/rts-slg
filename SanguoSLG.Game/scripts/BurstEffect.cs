using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 폭탄이 터지는 효과(design-effect.md #8). 한 주기마다 섬광이 번쩍이고, 충격파 링이
/// 바닥으로 퍼지며, 파편이 사방으로 튀어 중력에 끌려 떨어진다. 검수용으로 반복 재생한다.
/// 파편 방향·속도는 인덱스에서 뽑아 난수를 쓰지 않는다(결정적).
/// </summary>
public partial class BurstEffect : Node3D
{
    public float S = 1f;

    private const int Shards = 14;
    private const float Period = 1.8f;    // 터짐~쉼 한 주기
    private const float ActiveEnd = 0.55f; // 이 시점 이후엔 파편이 사라져 있는다

    private MeshInstance3D _flash = null!;
    private MeshInstance3D _ring = null!;
    private OmniLight3D _light = null!;
    private readonly MeshInstance3D[] _shards = new MeshInstance3D[Shards];
    private readonly Vector3[] _dir = new Vector3[Shards];   // 파편별 튀는 방향(단위)
    private readonly float[] _speed = new float[Shards];     // 파편별 속도 배수
    private float _t;

    public override void _Ready()
    {
        // 섬광 — 터지는 순간 부풀었다 꺼지는 밝은 구체
        _flash = new MeshInstance3D
        {
            Mesh = new SphereMesh
            {
                Radius = 0.18f * S,
                Height = 0.36f * S,
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(1f, 0.85f, 0.4f),
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                },
            },
            Position = new Vector3(0f, 0.14f * S, 0f),
            Visible = false,
        };
        AddChild(_flash);

        // 충격파 — 바닥에 눕혀 퍼지는 얇은 링
        _ring = new MeshInstance3D
        {
            Mesh = new TorusMesh
            {
                InnerRadius = 0.34f * S,
                OuterRadius = 0.40f * S,
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(1f, 0.7f, 0.3f),
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                },
            },
            Position = new Vector3(0f, 0.02f * S, 0f),
            Visible = false,
        };
        AddChild(_ring);

        _light = new OmniLight3D
        {
            LightColor = new Color(1f, 0.6f, 0.25f),
            LightEnergy = 0f,
            OmniRange = 2.2f * S,
            ShadowEnabled = false,
            Position = new Vector3(0f, 0.2f * S, 0f),
        };
        AddChild(_light);

        var shardMesh = new BoxMesh
        {
            Size = new Vector3(0.035f * S, 0.035f * S, 0.035f * S),
            Material = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.55f, 0.15f),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            },
        };

        for (var i = 0; i < Shards; i++)
        {
            var shard = new MeshInstance3D { Mesh = shardMesh, Visible = false };
            AddChild(shard);
            _shards[i] = shard;

            // 위쪽으로 살짝 치우친 반구 분포(바닥이 아래를 막는다). 황금각으로 흩는다.
            var y = Mathf.Lerp(0.1f, 1f, (i + 0.5f) / Shards);
            var rxz = Mathf.Sqrt(1f - y * y);
            var phi = i * 2.399963f;
            _dir[i] = new Vector3(Mathf.Cos(phi) * rxz, y, Mathf.Sin(phi) * rxz);
            _speed[i] = 0.8f + (i % 4) * 0.35f;
        }
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        var cycle = Mathf.PosMod(_t / Period, 1f);

        // 섬광: 0에서 확 부풀었다 0.22에 꺼진다
        if (cycle < 0.22f)
        {
            var f = cycle / 0.22f;
            var scale = Mathf.Sin(f * Mathf.Pi) * 1.4f + 0.2f;
            _flash.Visible = true;
            _flash.Scale = new Vector3(scale, scale, scale);
            SetAlpha(_flash, 1f - f);
        }
        else
        {
            _flash.Visible = false;
        }

        // 충격파: 0~0.45 동안 바깥으로 퍼지며 옅어진다
        if (cycle < 0.45f)
        {
            var f = cycle / 0.45f;
            var scale = 0.3f + f * 1.6f;
            _ring.Visible = true;
            _ring.Scale = new Vector3(scale, 1f, scale);
            SetAlpha(_ring, 1f - f);
        }
        else
        {
            _ring.Visible = false;
        }

        // 빛: 터지는 순간 가장 밝고 0.3에 사그라든다
        _light.LightEnergy = cycle < 0.3f ? (1f - cycle / 0.3f) * 3.5f : 0f;

        // 파편: 방향으로 날아가며 중력에 끌려 떨어진다
        for (var i = 0; i < Shards; i++)
        {
            var shard = _shards[i];
            if (cycle >= ActiveEnd)
            {
                shard.Visible = false;
                continue;
            }

            var tt = cycle / ActiveEnd;              // 활동 구간 내 진행(0~1)
            var dist = _speed[i] * tt * 0.6f * S;
            var drop = tt * tt * 0.9f * S;           // 포물선 낙하
            shard.Visible = true;
            shard.Position = new Vector3(
                _dir[i].X * dist,
                0.14f * S + _dir[i].Y * dist - drop,
                _dir[i].Z * dist);
            var size = 1f - tt * 0.6f;               // 날아가며 조금 작아진다
            shard.Scale = new Vector3(size, size, size);
            SetAlpha(shard, 1f - tt);
        }
    }

    private static void SetAlpha(MeshInstance3D node, float a)
    {
        var mat = (StandardMaterial3D)((PrimitiveMesh)node.Mesh).Material;
        var c = mat.AlbedoColor;
        mat.AlbedoColor = new Color(c.R, c.G, c.B, Mathf.Clamp(a, 0f, 1f));
    }
}
