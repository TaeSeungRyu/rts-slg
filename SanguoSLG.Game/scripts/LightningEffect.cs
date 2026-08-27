using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 번개가 내리치는 효과(design-effect.md #14) — 낙뢰(계략) 명중 연출. 하늘에서 지그재그
/// 섬광(주가지+곁가지, 코드 생성)이 대상에 내리꽂히고 명중 순간 순백 발광과 바닥 잔광 링이
/// 퍼진다. 실사용은 1회 재생 후 부착 루트째 스스로 정리하고(<see cref="Loop"/>=false),
/// 검수용은 주기마다 낙뢰 모양을 바꿔 반복한다. 지그재그는 사인 해시(표현용 시드)로 뽑아
/// System.Random을 쓰지 않는다(결정적).
/// </summary>
public partial class LightningEffect : Node3D
{
    public float S = 1f;
    public bool Loop = true;

    private const int MainSegments = 7;
    private const int BranchSegments = 2;
    private const float Period = 2.4f;
    private const float BoltEnd = 0.15f;
    private const float RingEnd = 0.5f;
    private const float Height = 2.0f;

    private readonly MeshInstance3D[] _main = new MeshInstance3D[MainSegments];
    private readonly MeshInstance3D[] _branch = new MeshInstance3D[BranchSegments];
    private StandardMaterial3D _boltMat = null!;
    private MeshInstance3D _flash = null!;
    private MeshInstance3D _ring = null!;
    private OmniLight3D _light = null!;
    private float _t;
    private int _strike = -1;

    public override void _Ready()
    {
        _boltMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.92f, 0.96f, 1f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        };
        var box = new BoxMesh { Size = Vector3.One, Material = _boltMat };
        for (var i = 0; i < MainSegments; i++)
        {
            _main[i] = new MeshInstance3D { Mesh = box, Visible = false };
            AddChild(_main[i]);
        }

        for (var i = 0; i < BranchSegments; i++)
        {
            _branch[i] = new MeshInstance3D { Mesh = box, Visible = false };
            AddChild(_branch[i]);
        }

        _flash = new MeshInstance3D
        {
            Mesh = new SphereMesh
            {
                Radius = 0.14f * S,
                Height = 0.28f * S,
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(1f, 1f, 1f),
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                },
            },
            Position = new Vector3(0f, 0.10f * S, 0f),
            Visible = false,
        };
        AddChild(_flash);

        _ring = new MeshInstance3D
        {
            Mesh = new TorusMesh
            {
                InnerRadius = 0.30f * S,
                OuterRadius = 0.36f * S,
                Material = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.75f, 0.88f, 1f),
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
            LightColor = new Color(0.80f, 0.90f, 1f),
            LightEnergy = 0f,
            OmniRange = 2.4f * S,
            ShadowEnabled = false,
            Position = new Vector3(0f, 0.3f * S, 0f),
        };
        AddChild(_light);
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        if (!Loop && _t >= Period * (RingEnd + 0.1f))
        {
            (GetParent() ?? (Node)this).QueueFree();
            SetProcess(false);
            return;
        }

        var strike = (int)(_t / Period);
        if (strike != _strike)
        {
            _strike = strike;
            LayoutBolt(strike);
        }

        var cycle = Mathf.PosMod(_t / Period, 1f);

        // 볼트: 짧게 꽂힌 뒤 잘게 깜빡이며 사그라든다
        var boltOn = cycle < BoltEnd;
        if (boltOn)
        {
            var f = cycle / BoltEnd;
            var flicker = 0.55f + 0.45f * Mathf.Sin(f * 26f + strike * 3f);
            var c = _boltMat.AlbedoColor;
            _boltMat.AlbedoColor = new Color(c.R, c.G, c.B, Mathf.Clamp((1f - f) * flicker + 0.25f, 0f, 1f));
        }

        foreach (var seg in _main)
        {
            seg.Visible = boltOn;
        }
        foreach (var seg in _branch)
        {
            seg.Visible = boltOn;
        }

        if (cycle < 0.2f)
        {
            var f = cycle / 0.2f;
            var scale = Mathf.Sin(f * Mathf.Pi) * 1.5f + 0.2f;
            _flash.Visible = true;
            _flash.Scale = new Vector3(scale, scale, scale);
            SetAlpha(_flash, 1f - f);
        }
        else
        {
            _flash.Visible = false;
        }

        if (cycle < RingEnd)
        {
            var f = cycle / RingEnd;
            var scale = 0.3f + f * 1.5f;
            _ring.Visible = true;
            _ring.Scale = new Vector3(scale, 1f, scale);
            SetAlpha(_ring, 1f - f);
        }
        else
        {
            _ring.Visible = false;
        }

        _light.LightEnergy = cycle < 0.25f ? (1f - cycle / 0.25f) * 4f : 0f;
    }

    // 주가지: 하늘에서 대상(원점)까지 지그재그. 양 끝은 고정하고 중간만 흔든다.
    // 곁가지: 상부 굴절점에서 옆으로 뻗다 허공에서 끊긴다.
    private void LayoutBolt(int strike)
    {
        var prev = new Vector3(
            (Hash(strike, 90) - 0.5f) * 0.5f * S,
            Height * S,
            (Hash(strike, 91) - 0.5f) * 0.5f * S);
        var branchFrom = prev;

        for (var i = 0; i < MainSegments; i++)
        {
            var f = (i + 1) / (float)MainSegments;
            var amp = Mathf.Sin(Mathf.Pi * f) * 0.34f * S;
            var next = i == MainSegments - 1
                ? Vector3.Zero
                : new Vector3(
                    (Hash(strike, i * 2) - 0.5f) * 2f * amp,
                    Height * S * (1f - f),
                    (Hash(strike, i * 2 + 1) - 0.5f) * 2f * amp);
            SetSegment(_main[i], prev, next, 0.035f * S);
            if (i == 1)
            {
                branchFrom = next;
            }

            prev = next;
        }

        var side = Hash(strike, 70) < 0.5f ? -1f : 1f;
        var branchEnd = branchFrom + new Vector3(
            side * (0.22f + Hash(strike, 71) * 0.14f) * S,
            -0.38f * S,
            (Hash(strike, 72) - 0.5f) * 0.3f * S);
        var branchMid = (branchFrom + branchEnd) * 0.5f
            + new Vector3((Hash(strike, 73) - 0.5f) * 0.12f * S, 0f, (Hash(strike, 74) - 0.5f) * 0.12f * S);
        SetSegment(_branch[0], branchFrom, branchMid, 0.020f * S);
        SetSegment(_branch[1], branchMid, branchEnd, 0.020f * S);
    }

    private static void SetSegment(MeshInstance3D node, Vector3 a, Vector3 b, float width)
    {
        var d = b - a;
        var len = d.Length();
        var z = d / len;
        var x = Vector3.Up.Cross(z);
        // 세그먼트가 수직에 가까우면 외적이 0에 수렴한다 — 임의의 수평축으로 대체
        x = x.LengthSquared() < 1e-6f ? Vector3.Right : x.Normalized();
        var y = z.Cross(x);
        node.Basis = new Basis(x * width, y * width, z * len);
        node.Position = (a + b) * 0.5f;
    }

    private static float Hash(int strike, int salt)
    {
        var v = Mathf.Sin(strike * 12.9898f + salt * 78.233f) * 43758.5453f;
        return Mathf.PosMod(v, 1f);
    }

    private static void SetAlpha(MeshInstance3D node, float a)
    {
        var mat = (StandardMaterial3D)((PrimitiveMesh)node.Mesh).Material;
        var c = mat.AlbedoColor;
        mat.AlbedoColor = new Color(c.R, c.G, c.B, Mathf.Clamp(a, 0f, 1f));
    }
}
