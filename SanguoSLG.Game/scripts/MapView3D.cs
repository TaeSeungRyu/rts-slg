using System.Collections.Generic;
using Godot;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 헥사 맵을 3D 타일(Kenney Hexagon Kit GLB)로 렌더한다. 게임 규칙은 없고 Core 데이터를 배치만 한다.
/// 타일 크기·방향은 모델 AABB에서 자동 측정한다.
/// </summary>
public partial class MapView3D : Node3D
{
    private readonly Dictionary<TerrainType, PackedScene> _tiles = new();
    private float _size = 1f;      // 헥사 중심~꼭짓점(월드 단위)
    private bool _flatTop = true;

    public override void _Ready()
    {
        _tiles[TerrainType.Plains] = GD.Load<PackedScene>("res://assets/models/grass.glb");
        _tiles[TerrainType.Forest] = GD.Load<PackedScene>("res://assets/models/grass-forest.glb");
        _tiles[TerrainType.Mountain] = GD.Load<PackedScene>("res://assets/models/stone-mountain.glb");
        _tiles[TerrainType.Desert] = GD.Load<PackedScene>("res://assets/models/sand.glb");
        MeasureTile(_tiles[TerrainType.Plains]);
    }

    public void Build(HexMap map)
    {
        foreach (var tile in map.Tiles())
        {
            var instance = _tiles[map.TerrainAt(tile)].Instantiate<Node3D>();
            instance.Position = HexToWorld(tile);
            AddChild(instance);
        }
    }

    /// <summary>헥사 좌표 → 3D 월드 위치(x-z 평면).</summary>
    public Vector3 HexToWorld(HexCoord coord)
    {
        var sqrt3 = Mathf.Sqrt(3f);
        return _flatTop
            ? new Vector3(_size * 1.5f * coord.Q, 0f, _size * sqrt3 * (coord.R + coord.Q / 2f))
            : new Vector3(_size * sqrt3 * (coord.Q + coord.R / 2f), 0f, _size * 1.5f * coord.R);
    }

    private void MeasureTile(PackedScene scene)
    {
        var probe = scene.Instantiate<Node3D>();
        var mesh = FindMesh(probe);
        if (mesh?.Mesh is not null)
        {
            var aabb = mesh.Mesh.GetAabb();
            if (aabb.Size.X >= aabb.Size.Z)
            {
                _flatTop = true;
                _size = aabb.Size.X / 2f;
            }
            else
            {
                _flatTop = false;
                _size = aabb.Size.Z / 2f;
            }
        }

        probe.Free();
    }

    private static MeshInstance3D? FindMesh(Node node)
    {
        if (node is MeshInstance3D mesh)
        {
            return mesh;
        }

        foreach (var child in node.GetChildren())
        {
            var found = FindMesh(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }
}
