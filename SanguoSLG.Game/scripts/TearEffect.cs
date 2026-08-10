using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 찢어지는 듯한 효과(design-effect.md #10). 지그재그 이빨을 맞문 두 조각이 좌우로
/// 벌어지며 틈이 열렸다 닫히길 반복한다. 이음새 3개가 위상을 어긋내 순차로 찢어진다.
/// 조각은 카메라를 향해 서고(빌보드 대신 매 프레임 정면 회전), 위상·자리는 인덱스에서
/// 뽑아 난수를 쓰지 않는다(결정적).
/// </summary>
public partial class TearEffect : Node3D
{
    public float S = 1f;

    private const int Seams = 3;
    private const float Period = 1.7f;
    private const float MaxGap = 0.07f;   // 조각이 벌어지는 최대 폭(*S)

    private Node3D _facer = null!;
    private readonly MeshInstance3D[] _left = new MeshInstance3D[Seams];
    private readonly MeshInstance3D[] _right = new MeshInstance3D[Seams];
    private float _t;

    public override void _Ready()
    {
        _facer = new Node3D();
        AddChild(_facer);

        var leftMesh = BuildRipHalf(-1f);
        var rightMesh = BuildRipHalf(+1f);

        for (var k = 0; k < Seams; k++)
        {
            var seam = new Node3D
            {
                // 이음새마다 높이·좌우를 달리해 여러 군데가 찢기게 한다
                Position = new Vector3((k - 1) * 0.14f * S, (0.16f + k * 0.13f) * S, 0f),
            };
            _facer.AddChild(seam);

            var left = new MeshInstance3D { Mesh = leftMesh, MaterialOverride = RipMaterial() };
            var right = new MeshInstance3D { Mesh = rightMesh, MaterialOverride = RipMaterial() };
            seam.AddChild(left);
            seam.AddChild(right);
            _left[k] = left;
            _right[k] = right;
        }
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;

        var cam = GetViewport()?.GetCamera3D();
        if (cam != null)
        {
            // -Z가 카메라를 향하게 세운다. 조각의 좌우 벌어짐이 화면 가로축과 맞도록.
            _facer.LookAt(cam.GlobalPosition, Vector3.Up);
        }

        for (var k = 0; k < Seams; k++)
        {
            var cycle = Mathf.PosMod(_t / Period + k * 0.33f, 1f);

            float gap, alpha;
            if (cycle < 0.15f)          // 맞물린 채 나타남
            {
                gap = 0f;
                alpha = cycle / 0.15f;
            }
            else if (cycle < 0.50f)     // 지그재그가 벌어지며 찢어진다
            {
                var f = (cycle - 0.15f) / 0.35f;
                gap = f * MaxGap;
                alpha = 1f;
            }
            else if (cycle < 0.80f)     // 벌어진 채 유지
            {
                gap = MaxGap;
                alpha = 1f;
            }
            else                        // 옅어지며 사라짐(다음 주기 전 쉼)
            {
                gap = MaxGap;
                alpha = 1f - (cycle - 0.80f) / 0.20f;
            }

            _left[k].Position = new Vector3(-gap * S, 0f, 0f);
            _right[k].Position = new Vector3(gap * S, 0f, 0f);
            SetAlpha(_left[k], alpha);
            SetAlpha(_right[k], alpha);
        }
    }

    // 지그재그 이빨을 가진 찢김 조각 한쪽. sign<0=왼쪽, >0=오른쪽. 바깥은 직선,
    // 안쪽(맞물리는 변)은 이빨이 중앙(x=0)을 향해 뾰족한 톱니.
    private static ArrayMesh BuildRipHalf(float sign)
    {
        const int steps = 8;
        var w = 0.11f;       // 조각 폭
        var amp = 0.045f;    // 톱니 깊이
        var h = 0.46f;       // 세로 길이

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        Vector3 Outer(int i) => new(sign * w, -h / 2f + i * (h / steps), 0f);
        Vector3 Inner(int i) => new(sign * (i % 2 == 0 ? 0f : amp), -h / 2f + i * (h / steps), 0f);

        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            st.SetNormal(Vector3.Back);
            st.AddVertex(a);
            st.AddVertex(b);
            st.AddVertex(c);
        }

        for (var i = 0; i < steps; i++)
        {
            Tri(Outer(i), Inner(i), Inner(i + 1));
            Tri(Outer(i), Inner(i + 1), Outer(i + 1));
        }

        return st.Commit();
    }

    private static StandardMaterial3D RipMaterial() => new()
    {
        AlbedoColor = new Color(0.92f, 0.94f, 1f, 1f),
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled, // 양면 — 뒤에서 봐도 보이게
        EmissionEnabled = true,
        Emission = new Color(0.5f, 0.55f, 0.7f),
    };

    private static void SetAlpha(MeshInstance3D node, float a)
    {
        var mat = (StandardMaterial3D)node.MaterialOverride;
        var c = mat.AlbedoColor;
        mat.AlbedoColor = new Color(c.R, c.G, c.B, Mathf.Clamp(a, 0f, 1f));
    }
}
