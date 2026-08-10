using System.Collections.Generic;
using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 깨지는 듯한 효과(design-effect.md #11). 오버레이가 아니라 <b>대상의 실제 메시를 조각낸다</b>.
/// 붙는 순간 대상 아래 <see cref="MeshInstance3D"/>들을 읽어 삼각형을 공간 덩어리(Voronoi)로
/// 나눠 조각 메시를 만들고, 원본을 숨긴 뒤 조각이 바깥+위로 튕겨나가 중력에 떨어지며 회전한다.
/// 조각은 원본 재질을 그대로 물려받아 그 에셋이 깨지는 것처럼 보인다.
/// 실사용은 1회성이지만 검수용으로 주기마다 원본을 복원하고 조각을 리셋한다.
/// 파쇄 시드·방향·회전은 인덱스에서 뽑아 난수를 쓰지 않는다(결정적).
/// </summary>
public partial class ShatterEffect : Node3D
{
    public float S = 1f;
    public Node3D Target = null!;

    private const float Period = 2.6f;
    private const float ExplodeStart = 0.10f;
    private const float ExplodeEnd = 0.72f;   // 이후 원본 복원 + 조각 숨김(쉼)

    private readonly List<MeshInstance3D> _originals = new();
    private readonly List<Fragment> _fragments = new();
    private float _t;

    private sealed class Fragment
    {
        public required MeshInstance3D Node;
        public Vector3 Rest;       // 조각 중심의 제자리(원본과 겹치는 위치)
        public Vector3 Dir;        // 튕겨나가는 방향(단위)
        public Vector3 SpinAxis;
        public float Speed;
        public float SpinRate;
    }

    public override void _Ready()
    {
        if (Target == null)
        {
            return;
        }

        CollectOriginals(Target);

        foreach (var src in _originals)
        {
            FractureMesh(src);
        }
    }

    // 대상 아래 실제 모델 메시만 모은다. 다른 효과 서브트리("Effect_*")는 건너뛴다.
    private void CollectOriginals(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child.Name.ToString().StartsWith("Effect_"))
            {
                continue;
            }

            if (child is MeshInstance3D mi && mi.Mesh != null)
            {
                _originals.Add(mi);
            }

            CollectOriginals(child);
        }
    }

    private void FractureMesh(MeshInstance3D src)
    {
        var mesh = src.Mesh;
        // 조각 컨테이너를 원본과 같은 위치에 둔다(대상 기준 상대 변환).
        var rel = Target.GlobalTransform.AffineInverse() * src.GlobalTransform;
        var container = new Node3D { Transform = rel };
        AddChild(container);

        for (var s = 0; s < mesh.GetSurfaceCount(); s++)
        {
            var arrays = mesh.SurfaceGetArrays(s);
            var verts = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
            if (verts.Length < 3)
            {
                continue;
            }

            var uvs = arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
            var colors = arrays[(int)Mesh.ArrayType.Color].AsColorArray();
            var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
            var hasUv = uvs.Length == verts.Length;
            var hasColor = colors.Length == verts.Length;

            // 삼각형 목록(인덱스 없으면 정점 순서대로 3개씩)
            var triCount = indices.Length > 0 ? indices.Length / 3 : verts.Length / 3;
            if (triCount == 0)
            {
                continue;
            }

            int Idx(int t, int c) => indices.Length > 0 ? indices[t * 3 + c] : t * 3 + c;

            // 삼각형 무게중심 + 메시 중심
            var centroids = new Vector3[triCount];
            var meshCenter = Vector3.Zero;
            for (var t = 0; t < triCount; t++)
            {
                var c = (verts[Idx(t, 0)] + verts[Idx(t, 1)] + verts[Idx(t, 2)]) / 3f;
                centroids[t] = c;
                meshCenter += c;
            }
            meshCenter /= triCount;

            // 시드 = 균등 간격 삼각형 중심(메시 위에 놓여 파쇄가 고르게 퍼진다)
            var k = Mathf.Clamp(triCount / 6, 3, 12);
            if (triCount < 3)
            {
                k = 1;
            }
            var seeds = new Vector3[k];
            for (var j = 0; j < k; j++)
            {
                seeds[j] = centroids[j * triCount / k];
            }

            // 각 삼각형을 가장 가까운 시드에 배정(Voronoi 덩어리)
            var clusters = new List<int>[k];
            for (var j = 0; j < k; j++)
            {
                clusters[j] = new List<int>();
            }
            for (var t = 0; t < triCount; t++)
            {
                var best = 0;
                var bestD = centroids[t].DistanceSquaredTo(seeds[0]);
                for (var j = 1; j < k; j++)
                {
                    var d = centroids[t].DistanceSquaredTo(seeds[j]);
                    if (d < bestD)
                    {
                        bestD = d;
                        best = j;
                    }
                }
                clusters[best].Add(t);
            }

            var material = src.GetActiveMaterial(s);

            for (var j = 0; j < k; j++)
            {
                var tris = clusters[j];
                if (tris.Count == 0)
                {
                    continue;
                }

                // 조각 중심 = 덩어리 무게중심. 정점을 중심 기준으로 재배치해
                // 노드 회전이 조각 제자리에서 돌게 한다.
                var center = Vector3.Zero;
                foreach (var t in tris)
                {
                    center += centroids[t];
                }
                center /= tris.Count;

                var st = new SurfaceTool();
                st.Begin(Mesh.PrimitiveType.Triangles);
                foreach (var t in tris)
                {
                    var i0 = Idx(t, 0);
                    var i1 = Idx(t, 1);
                    var i2 = Idx(t, 2);
                    var a = verts[i0];
                    var b = verts[i1];
                    var c = verts[i2];
                    var n = (b - a).Cross(c - a).Normalized();
                    AddVert(st, a - center, n, hasUv ? uvs[i0] : Vector2.Zero, hasColor ? colors[i0] : Colors.White, hasUv, hasColor);
                    AddVert(st, b - center, n, hasUv ? uvs[i1] : Vector2.Zero, hasColor ? colors[i1] : Colors.White, hasUv, hasColor);
                    AddVert(st, c - center, n, hasUv ? uvs[i2] : Vector2.Zero, hasColor ? colors[i2] : Colors.White, hasUv, hasColor);
                }

                var frag = new MeshInstance3D
                {
                    Mesh = st.Commit(),
                    Position = center,
                    Visible = false,
                };
                if (material != null)
                {
                    frag.MaterialOverride = material;
                }
                container.AddChild(frag);

                var dir = center - meshCenter;
                if (dir.LengthSquared() < 1e-6f)
                {
                    dir = Vector3.Up;
                }
                dir = (dir.Normalized() + Vector3.Up * 0.4f).Normalized();

                var seed = _fragments.Count;
                _fragments.Add(new Fragment
                {
                    Node = frag,
                    Rest = center,
                    Dir = dir,
                    SpinAxis = new Vector3((seed % 3) - 1, (seed % 5) - 2, (seed % 2) == 0 ? 1 : -1).Normalized(),
                    Speed = 0.20f + (seed % 4) * 0.10f,
                    SpinRate = 4f + (seed % 5),
                });
            }
        }

        // 조각 준비가 끝난 원본만 실제로 다룬다(빈 메시는 원본 그대로 둔다).
    }

    private static void AddVert(SurfaceTool st, Vector3 v, Vector3 n, Vector2 uv, Color col, bool hasUv, bool hasColor)
    {
        st.SetNormal(n);
        if (hasUv)
        {
            st.SetUV(uv);
        }
        if (hasColor)
        {
            st.SetColor(col);
        }
        st.AddVertex(v);
    }

    public override void _Process(double delta)
    {
        if (_fragments.Count == 0)
        {
            return;
        }

        _t += (float)delta;
        var cycle = Mathf.PosMod(_t / Period, 1f);

        var rest = cycle >= ExplodeEnd;    // 쉼: 원본 복원, 조각 숨김
        foreach (var o in _originals)
        {
            o.Visible = rest;
        }
        foreach (var f in _fragments)
        {
            f.Node.Visible = !rest;
        }

        if (rest)
        {
            return;
        }

        // ExplodeStart 전엔 조각이 조립된 채(제자리) — 통짜로 보인다. 이후 폭발.
        var tt = Mathf.Clamp((cycle - ExplodeStart) / (ExplodeEnd - ExplodeStart), 0f, 1f);
        foreach (var f in _fragments)
        {
            var dist = f.Speed * tt * S;
            var drop = tt * tt * 0.7f * S;
            f.Node.Position = f.Rest + f.Dir * dist - Vector3.Up * drop;
            f.Node.Rotation = f.SpinAxis * (f.SpinRate * tt);
        }
    }
}
