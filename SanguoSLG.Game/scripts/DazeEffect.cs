using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 점과 별이 원형 테두리를 이루고 빙글빙글 도는 효과(design-effect.md #6). 만화의 "어질어질".
/// 대상 머리 위에 점(작은 구)·별(저폴리 평면)을 원형으로 번갈아 배치하고, 링 전체가 매 프레임
/// 카메라를 향한 채(평면 별이 늘 별로 보이게) 화면 평면에서 회전한다.
/// 위상·자리는 인덱스에서 뽑아 난수를 쓰지 않는다.
/// </summary>
public partial class DazeEffect : Node3D
{
    public float S = 1f;

    private const int Count = 6; // 점 3 + 별 3 번갈아

    private Node3D _ring = null!;
    private float _spin;

    public override void _Ready()
    {
        _ring = new Node3D { Position = new Vector3(0f, 0.62f * S, 0f) };
        AddChild(_ring);

        var dotMat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.97f, 0.95f, 0.80f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        };
        var starMesh = StarMesh(0.058f * S, 0.025f * S, new Color(1.0f, 0.82f, 0.15f));

        for (var k = 0; k < Count; k++)
        {
            var angle = k * Mathf.Tau / Count;
            // 수평면(XZ)에 눕힌 원 — 화면 평면이 아니라 캐릭터의 3D 원근을 따르는 궤도
            var pos = new Vector3(Mathf.Cos(angle) * 0.17f * S, 0f, Mathf.Sin(angle) * 0.17f * S);

            Node3D item;
            if (k % 2 == 0)
            {
                item = new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = 0.027f * S, Height = 0.054f * S, RadialSegments = 6, Rings = 4, Material = dotMat },
                };
            }
            else
            {
                item = new MeshInstance3D { Mesh = starMesh };
            }

            item.Position = pos;
            _ring.AddChild(item);
        }
    }

    public override void _Process(double delta)
    {
        _spin += (float)delta * 2.0f;

        // 수평 링을 Y축으로 돌린다 — 캐릭터 위를 도는 3D 궤도
        _ring.Rotation = new Vector3(0f, _spin, 0f);

        // 납작한 별·점은 각자 카메라를 바라보게 해 궤도 어디서든 별로 보인다
        var cam = GetViewport().GetCamera3D();
        if (cam is null)
        {
            return;
        }

        foreach (var child in _ring.GetChildren())
        {
            if (child is Node3D item)
            {
                item.LookAt(cam.GlobalPosition, Vector3.Up);
            }
        }
    }

    // 평면 5각 별(XY 평면). 앞뒤 모두 보이도록 양면·비조명.
    private static ArrayMesh StarMesh(float outer, float inner, Color color)
    {
        var rim = new Vector3[10];
        for (var k = 0; k < 10; k++)
        {
            var r = k % 2 == 0 ? outer : inner;
            var a = k * Mathf.Pi / 5f - Mathf.Pi / 2f;
            rim[k] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
        }

        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        st.SetNormal(Vector3.Back);
        for (var k = 0; k < 10; k++)
        {
            st.AddVertex(Vector3.Zero);
            st.AddVertex(rim[k]);
            st.AddVertex(rim[(k + 1) % 10]);
        }

        var mesh = st.Commit();
        mesh.SurfaceSetMaterial(0, new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        });
        return mesh;
    }
}
