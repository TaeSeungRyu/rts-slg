using System.Collections.Generic;
using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 세력색 적용(계획 3). 모델의 "red" 이름 재질 표면만 세력색으로 바꾼다 —
/// 투구술·망토·깃발·안장천 등 세력색 규약 표면이 전부 여기에 걸린다.
/// 재질은 색별로 캐시해 공유한다. 같은 색 부대끼리 재질 인스턴스가 늘지 않는다.
/// </summary>
public static class FactionColorView
{
    public const string MaterialName = "red";

    private static readonly Dictionary<Color, StandardMaterial3D> Cache = new();

    public static void Apply(Node node, Color color)
    {
        if (node is MeshInstance3D instance && instance.Mesh is not null)
        {
            for (var surface = 0; surface < instance.Mesh.GetSurfaceCount(); surface++)
            {
                if (instance.GetActiveMaterial(surface) is { ResourceName: MaterialName })
                {
                    instance.SetSurfaceOverrideMaterial(surface, Shared(color));
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            Apply(child, color);
        }
    }

    /// <summary>
    /// 모델 전체를 세력색 쪽으로 <paramref name="strength"/>(0~1)만큼 당겨 틴트한다 — 액센트뿐 아니라
    /// 온몸이 붉은/푸른 계열로 물들어 진형을 멀리서도 구분한다. 검수 하베스트용(실제 게임은 <see cref="Apply"/>).
    /// 표면 원래 색에서 lerp하므로 음영·형태는 유지된다. 결과색별로 재질을 캐시한다.
    /// </summary>
    public static void ApplyTint(Node node, Color color, float strength)
    {
        if (node is MeshInstance3D instance && instance.Mesh is not null)
        {
            for (var surface = 0; surface < instance.Mesh.GetSurfaceCount(); surface++)
            {
                var baseColor = (instance.GetActiveMaterial(surface) as BaseMaterial3D)?.AlbedoColor ?? Colors.White;
                instance.SetSurfaceOverrideMaterial(surface, Shared(baseColor.Lerp(color, strength)));
            }
        }

        foreach (var child in node.GetChildren())
        {
            ApplyTint(child, color, strength);
        }
    }

    private static StandardMaterial3D Shared(Color color)
    {
        if (!Cache.TryGetValue(color, out var material))
        {
            material = new StandardMaterial3D { AlbedoColor = color };
            Cache[color] = material;
        }

        return material;
    }
}
