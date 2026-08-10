using System.Collections.Generic;
using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 대상 <see cref="Node3D"/> 아래 실제 모델 메시를 <b>모델 전체 기준</b>으로 K개의 조각으로
/// 쪼갠다(Tear·Shatter 공용). 하위 <see cref="MeshInstance3D"/>들을 대상 로컬 공간으로 구운 뒤
/// 삼각형을 최원점 샘플링 시드로 Voronoi 배정해, 파트 경계와 무관하게 공간상 K덩어리로 나눈다.
/// 각 조각은 조각 무게중심을 원점으로 하는 <see cref="Node3D"/>(children = 재질별 메시)로,
/// 이동·회전이 제자리에서 일어난다. 원본은 호출자가 숨겼다 복원한다.
/// 시드·배정은 순서에만 의존해 난수를 쓰지 않는다(결정적).
/// </summary>
public static class MeshFracture
{
    public sealed class Fragment
    {
        public required Node3D Node;
        public Vector3 Rest;   // 조각 무게중심의 제자리(원본과 겹치는 위치)
        public Vector3 Dir;    // 바깥으로 튕겨나가는 방향(단위)
        public int Seed;       // 조각별 편차용 인덱스
    }

    private struct Tri
    {
        public Vector3 A, B, C;
        public Vector2 UA, UB, UC;
        public Color CA, CB, CC;
        public bool HasUv, HasColor;
        public Material Mat;
        public Vector3 Centroid;
    }

    /// <summary>
    /// <paramref name="pieces"/>개(이하)의 조각을 만들어 반환한다. pieces가 0 이하면
    /// 삼각형 수에 맞춰 6~20개로 자동 결정한다. 조각 노드는 <paramref name="host"/> 아래 붙는다.
    /// </summary>
    public static (List<MeshInstance3D> originals, List<Fragment> fragments) Build(
        Node3D host, Node3D target, int pieces)
    {
        var originals = new List<MeshInstance3D>();
        Collect(target, originals);

        var tris = new List<Tri>();
        foreach (var src in originals)
        {
            BakeTriangles(target, src, tris);
        }

        var fragments = new List<Fragment>();
        if (tris.Count == 0)
        {
            return (originals, fragments);
        }

        var k = pieces > 0
            ? Mathf.Min(pieces, tris.Count)
            : Mathf.Clamp(tris.Count / 8, 6, 20);
        k = Mathf.Max(k, 1);

        var center = Vector3.Zero;
        foreach (var t in tris)
        {
            center += t.Centroid;
        }
        center /= tris.Count;

        var seeds = PickSeeds(tris, k, center);
        var assign = new int[tris.Count];
        for (var i = 0; i < tris.Count; i++)
        {
            var best = 0;
            var bestD = tris[i].Centroid.DistanceSquaredTo(seeds[0]);
            for (var j = 1; j < k; j++)
            {
                var d = tris[i].Centroid.DistanceSquaredTo(seeds[j]);
                if (d < bestD)
                {
                    bestD = d;
                    best = j;
                }
            }
            assign[i] = best;
        }

        for (var j = 0; j < k; j++)
        {
            var members = new List<int>();
            var pieceCenter = Vector3.Zero;
            for (var i = 0; i < tris.Count; i++)
            {
                if (assign[i] == j)
                {
                    members.Add(i);
                    pieceCenter += tris[i].Centroid;
                }
            }
            if (members.Count == 0)
            {
                continue;
            }
            pieceCenter /= members.Count;

            var pieceRoot = new Node3D { Position = pieceCenter, Visible = false };
            host.AddChild(pieceRoot);

            // 재질별로 서피스를 나눠 원본 겉모습을 유지한다.
            var byMat = new Dictionary<Material, SurfaceTool>();
            foreach (var i in members)
            {
                var t = tris[i];
                var key = t.Mat ?? _fallbackMat;
                if (!byMat.TryGetValue(key, out var st))
                {
                    st = new SurfaceTool();
                    st.Begin(Mesh.PrimitiveType.Triangles);
                    byMat[key] = st;
                }

                var n = (t.B - t.A).Cross(t.C - t.A);
                n = n.LengthSquared() < 1e-9f ? Vector3.Up : n.Normalized();
                AddVert(st, t.A - pieceCenter, n, t.UA, t.CA, t.HasUv, t.HasColor);
                AddVert(st, t.B - pieceCenter, n, t.UB, t.CB, t.HasUv, t.HasColor);
                AddVert(st, t.C - pieceCenter, n, t.UC, t.CC, t.HasUv, t.HasColor);
            }

            foreach (var (mat, st) in byMat)
            {
                var mi = new MeshInstance3D { Mesh = st.Commit(), MaterialOverride = mat };
                pieceRoot.AddChild(mi);
            }

            var dir = pieceCenter - center;
            dir = dir.LengthSquared() < 1e-6f ? Vector3.Up : dir.Normalized();
            fragments.Add(new Fragment { Node = pieceRoot, Rest = pieceCenter, Dir = dir, Seed = fragments.Count });
        }

        return (originals, fragments);
    }

    private static readonly StandardMaterial3D _fallbackMat = new() { AlbedoColor = new Color(0.6f, 0.6f, 0.62f) };

    // 대상 아래 실제 모델 메시만 모은다. 다른 효과 서브트리("Effect_*")는 건너뛴다.
    private static void Collect(Node node, List<MeshInstance3D> into)
    {
        foreach (var child in node.GetChildren())
        {
            if (child.Name.ToString().StartsWith("Effect_"))
            {
                continue;
            }
            if (child is MeshInstance3D mi && mi.Mesh != null)
            {
                into.Add(mi);
            }
            Collect(child, into);
        }
    }

    // 원본 메시의 삼각형을 대상 로컬 공간으로 구워 목록에 쌓는다.
    private static void BakeTriangles(Node3D target, MeshInstance3D src, List<Tri> into)
    {
        var mesh = src.Mesh;
        var rel = target.GlobalTransform.AffineInverse() * src.GlobalTransform;

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
            var mat = src.GetActiveMaterial(s);

            var triCount = indices.Length > 0 ? indices.Length / 3 : verts.Length / 3;
            int Idx(int t, int c) => indices.Length > 0 ? indices[t * 3 + c] : t * 3 + c;

            for (var t = 0; t < triCount; t++)
            {
                int i0 = Idx(t, 0), i1 = Idx(t, 1), i2 = Idx(t, 2);
                var a = rel * verts[i0];
                var b = rel * verts[i1];
                var c = rel * verts[i2];
                into.Add(new Tri
                {
                    A = a, B = b, C = c,
                    UA = hasUv ? uvs[i0] : Vector2.Zero,
                    UB = hasUv ? uvs[i1] : Vector2.Zero,
                    UC = hasUv ? uvs[i2] : Vector2.Zero,
                    CA = hasColor ? colors[i0] : Colors.White,
                    CB = hasColor ? colors[i1] : Colors.White,
                    CC = hasColor ? colors[i2] : Colors.White,
                    HasUv = hasUv, HasColor = hasColor, Mat = mat,
                    Centroid = (a + b + c) / 3f,
                });
            }
        }
    }

    // 최원점 샘플링: 첫 시드는 중심에서 가장 먼 삼각형, 이후는 기존 시드들에서 가장 먼 것.
    private static Vector3[] PickSeeds(List<Tri> tris, int k, Vector3 center)
    {
        var seeds = new Vector3[k];
        var first = 0;
        var farD = -1f;
        for (var i = 0; i < tris.Count; i++)
        {
            var d = tris[i].Centroid.DistanceSquaredTo(center);
            if (d > farD)
            {
                farD = d;
                first = i;
            }
        }
        seeds[0] = tris[first].Centroid;

        for (var j = 1; j < k; j++)
        {
            var pick = 0;
            var best = -1f;
            for (var i = 0; i < tris.Count; i++)
            {
                var minD = float.MaxValue;
                for (var m = 0; m < j; m++)
                {
                    minD = Mathf.Min(minD, tris[i].Centroid.DistanceSquaredTo(seeds[m]));
                }
                if (minD > best)
                {
                    best = minD;
                    pick = i;
                }
            }
            seeds[j] = tris[pick].Centroid;
        }
        return seeds;
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
}
