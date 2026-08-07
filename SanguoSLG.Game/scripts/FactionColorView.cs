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
