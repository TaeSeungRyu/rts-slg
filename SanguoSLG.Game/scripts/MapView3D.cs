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
    private PackedScene _water = null!;
    private float _size = 1f;      // 헥사 중심~꼭짓점(월드 단위)
    private float _topY;           // 타일 윗면 높이(마커·유닛 배치 기준)
    private bool _flatTop = true;

    /// <summary>타일 윗면의 월드 y. 도시 마커·유닛을 이 높이에 얹는다.</summary>
    public float TileTopY => _topY;

    public override void _Ready()
    {
        _tiles[TerrainType.Plains] = GD.Load<PackedScene>("res://assets/models/grass.glb");
        _tiles[TerrainType.Forest] = GD.Load<PackedScene>("res://assets/models/grass-forest.glb");
        _tiles[TerrainType.Mountain] = GD.Load<PackedScene>("res://assets/models/stone-mountain.glb");
        _tiles[TerrainType.Desert] = GD.Load<PackedScene>("res://assets/models/sand.glb");
        _water = GD.Load<PackedScene>("res://assets/models/water.glb");
        MeasureTile(_tiles[TerrainType.Plains]);
    }

    public void Build(HexMap map)
    {
        foreach (var tile in map.Tiles())
        {
            var instance = _tiles[map.TerrainAt(tile)].Instantiate<Node3D>();
            instance.Position = HexToWorld(tile);
            // 숲·산의 단조로움을 깨는 결정론적 회전(좌표 해시, 60° 단위).
            instance.RotationDegrees = new Vector3(0f, ((tile.Q * 31 + tile.R * 17) % 6 + 6) % 6 * 60f, 0f);
            AddChild(instance);
        }

        BuildWaterBorder(map);
    }

    // 맵 경계 밖 2겹을 물 타일로 둘러 디오라마 느낌을 준다.
    private void BuildWaterBorder(HexMap map)
    {
        const int rings = 4;
        for (var q = map.MinQ - rings; q <= map.MaxQ + rings; q++)
        {
            for (var r = map.MinR - rings; r <= map.MaxR + rings; r++)
            {
                var coord = new HexCoord(q, r);
                if (map.Contains(coord))
                {
                    continue;
                }

                var instance = _water.Instantiate<Node3D>();
                instance.Position = HexToWorld(coord);
                AddChild(instance);
            }
        }
    }

    /// <summary>3D 월드 위치(x-z 평면) → 가장 가까운 헥사 좌표. HexToWorld의 역변환.</summary>
    public HexCoord WorldToHex(Vector3 world)
    {
        var sqrt3 = Mathf.Sqrt(3f);
        float qf, rf;
        if (_flatTop)
        {
            qf = world.X / (_size * 1.5f);
            rf = world.Z / (_size * sqrt3) - qf / 2f;
        }
        else
        {
            rf = world.Z / (_size * 1.5f);
            qf = world.X / (_size * sqrt3) - rf / 2f;
        }

        return RoundAxial(qf, rf);
    }

    private static HexCoord RoundAxial(float qf, float rf)
    {
        float x = qf, z = rf, y = -x - z;
        int rx = Mathf.RoundToInt(x), ry = Mathf.RoundToInt(y), rz = Mathf.RoundToInt(z);
        float dx = Mathf.Abs(rx - x), dy = Mathf.Abs(ry - y), dz = Mathf.Abs(rz - z);

        if (dx > dy && dx > dz)
        {
            rx = -ry - rz;
        }
        else if (dy > dz)
        {
            ry = -rx - rz;
        }
        else
        {
            rz = -rx - ry;
        }

        return new HexCoord(rx, rz);
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

            _topY = aabb.End.Y;
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
