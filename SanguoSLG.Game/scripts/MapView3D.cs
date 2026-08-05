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

    /// <summary>헥사 중심~꼭짓점 거리(월드 단위). 하이라이트 등 오버레이 크기 기준.</summary>
    public float HexWorldSize => _size;

    /// <summary>물 타일 윗면(수면)의 월드 y. 바다 평면은 이보다 낮아야 물 타일을 가리지 않는다.</summary>
    public float WaterTopY { get; private set; }

    private PackedScene _riverStraight = null!;
    private PackedScene _riverCorner = null!;
    private PackedScene _riverCornerSharp = null!;
    private PackedScene _riverEnd = null!;
    private PackedScene _bridge = null!;

    // 강 모델의 기준(회전 0) 물길 방향(도). 스크린샷으로 실측해 보정한 값.
    private const float StraightAxisAngle = 0f;
    private const float EndAngle = 180f;
    private static readonly (float A1, float A2) CornerAngles = (180f, -60f);
    private static readonly (float A1, float A2) CornerSharpAngles = (180f, -120f);

    public override void _Ready()
    {
        _tiles[TerrainType.Plains] = GD.Load<PackedScene>("res://assets/models/grass.glb");
        _tiles[TerrainType.Forest] = GD.Load<PackedScene>("res://assets/models/grass-forest.glb");
        _tiles[TerrainType.Mountain] = GD.Load<PackedScene>("res://assets/models/mountain-small.glb");
        _tiles[TerrainType.Desert] = GD.Load<PackedScene>("res://assets/models/sand.glb");
        _tiles[TerrainType.Rocks] = GD.Load<PackedScene>("res://assets/models/stone-rocks.glb");
        _tiles[TerrainType.RockHill] = GD.Load<PackedScene>("res://assets/models/stone-hill.glb");
        _tiles[TerrainType.WaterRocks] = GD.Load<PackedScene>("res://assets/models/water-rocks.glb");
        _tiles[TerrainType.Paddy] = GD.Load<PackedScene>("res://assets/models/paddy.glb");
        _tiles[TerrainType.Farm] = GD.Load<PackedScene>("res://assets/models/building-farm.glb");
        _tiles[TerrainType.Workshop] = GD.Load<PackedScene>("res://assets/models/workshop.glb");
        _tiles[TerrainType.RockMountain] = GD.Load<PackedScene>("res://assets/models/stone-mountain.glb");
        _water = GD.Load<PackedScene>("res://assets/models/water.glb");
        _riverStraight = GD.Load<PackedScene>("res://assets/models/river-straight.glb");
        _riverCorner = GD.Load<PackedScene>("res://assets/models/river-corner.glb");
        _riverCornerSharp = GD.Load<PackedScene>("res://assets/models/river-corner-sharp.glb");
        _riverEnd = GD.Load<PackedScene>("res://assets/models/river-end.glb");
        _bridge = GD.Load<PackedScene>("res://assets/models/bridge.glb");
        MeasureTile(_tiles[TerrainType.Plains]);
        WaterTopY = MeasureTopY(_water);
    }

    private static float MeasureTopY(PackedScene scene)
    {
        var probe = scene.Instantiate<Node3D>();
        var top = FindMesh(probe)?.Mesh?.GetAabb().End.Y ?? 0f;
        probe.Free();
        return top;
    }

    public void Build(HexMap map)
    {
        foreach (var tile in map.Tiles())
        {
            var terrain = map.TerrainAt(tile);
            if (terrain is TerrainType.River or TerrainType.Bridge)
            {
                BuildRiverTile(map, tile, terrain);
                continue;
            }

            if (terrain is TerrainType.WaterShallow or TerrainType.WaterDeep)
            {
                BuildBigWaterTile(tile, terrain);
                continue;
            }

            var instance = _tiles[terrain].Instantiate<Node3D>();
            instance.Position = HexToWorld(tile);
            if (terrain != TerrainType.Workshop)
            {
                // 숲·산의 단조로움을 깨는 결정론적 회전(좌표 해시, 60° 단위).
                // 공방은 굴뚝 연기 위치가 고정이어야 하므로 회전하지 않는다.
                instance.RotationDegrees = new Vector3(0f, ((tile.Q * 31 + tile.R * 17) % 6 + 6) % 6 * 60f, 0f);
            }

            AddChild(instance);

            if (terrain == TerrainType.Workshop)
            {
                AddChild(BuildChimneySmoke(HexToWorld(tile)));
            }
        }

        BuildWaterBorder(map);
    }

    // 대하(큰 강): 얕은 물은 바다와 같은 물 타일, 깊은 물은 어두운 물색으로 깊이를 표현한다.
    private void BuildBigWaterTile(HexCoord tile, TerrainType terrain)
    {
        var instance = _water.Instantiate<Node3D>();
        instance.Position = HexToWorld(tile);

        if (terrain == TerrainType.WaterDeep)
        {
            var mesh = FindMesh(instance);
            if (mesh is not null)
            {
                mesh.MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.25f, 0.48f, 0.62f),
                    Roughness = 0.25f,
                };
            }
        }

        AddChild(instance);
    }

    /// <summary>다중 타일 지물(중간산·큰산)을 발자국 중심점에 배치하고, 산 위에 구름을 흘려보낸다.</summary>
    public void BuildFeatures(System.Collections.Generic.IReadOnlyList<MapFeature> features)
    {
        var models = new System.Collections.Generic.Dictionary<FeatureType, PackedScene>
        {
            [FeatureType.MountainMedium] = GD.Load<PackedScene>("res://assets/models/mountain-medium.glb"),
            [FeatureType.MountainLarge] = GD.Load<PackedScene>("res://assets/models/mountain-large.glb"),
            [FeatureType.MountainHuge] = GD.Load<PackedScene>("res://assets/models/mountain-huge.glb"),
        };

        foreach (var feature in features)
        {
            var centroid = Vector3.Zero;
            var count = 0;
            foreach (var tile in FeatureFootprint.TilesFor(feature))
            {
                centroid += HexToWorld(tile);
                count++;
            }

            centroid /= count;

            var instance = models[feature.Type].Instantiate<Node3D>();
            instance.Position = centroid;
            AddChild(instance);

            var (peakY, halfWidth) = feature.Type switch
            {
                FeatureType.MountainHuge => (1.45f, 1.75f),
                FeatureType.MountainLarge => (1.30f, 1.15f),
                _ => (1.02f, 1.10f),
            };
            AddChild(BuildMountainClouds(centroid, peakY, halfWidth));
        }
    }

    // 산 구름: 산괴 왼쪽 끝에서 생겨나 오른쪽 끝까지 가로질러 흘러가고, 끝에서 서서히 사라진다.
    private static Node3D BuildMountainClouds(Vector3 center, float peakY, float halfWidth)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1f, 1f, 1f, 0f));
        gradient.AddPoint(0.12f, new Color(0.97f, 0.97f, 0.98f, 0.55f));
        gradient.AddPoint(0.85f, new Color(0.97f, 0.97f, 0.98f, 0.5f));
        gradient.SetColor(1, new Color(1f, 1f, 1f, 0f));

        var mesh = new SphereMesh
        {
            Radius = 0.075f,
            Height = 0.09f,
            RadialSegments = 8,
            Rings = 4,
            Material = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };

        const float speed = 0.16f;
        var lifetime = halfWidth * 2f / speed;

        return new CpuParticles3D
        {
            // 왼쪽 끝에서 발생 → +X로 폭 전체를 횡단
            Position = center + new Vector3(-halfWidth, peakY, 0f),
            Amount = 6,
            Lifetime = lifetime,
            Preprocess = lifetime,
            Mesh = mesh,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.12f,
            Direction = new Vector3(1f, 0f, 0f),
            Spread = 3f,
            InitialVelocityMin = speed * 0.95f,
            InitialVelocityMax = speed * 1.05f,
            Gravity = Vector3.Zero,
            ScaleAmountMin = 0.8f,
            ScaleAmountMax = 1.9f,
            ColorRamp = gradient,
        };
    }

    // 공방 굴뚝 연기: 회색 반투명 입자가 피어올라 바람에 흘러가며 사라진다.
    // 굴뚝 위치는 workshop.glb의 로컬 좌표(블렌더 (0.245,-0.06,0.375) → Godot (0.245,0.375,0.06)).
    private static Node3D BuildChimneySmoke(Vector3 tileOrigin)
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.72f, 0.71f, 0.69f, 0.55f));
        gradient.SetColor(1, new Color(0.80f, 0.80f, 0.79f, 0f));

        var mesh = new SphereMesh
        {
            Radius = 0.028f,
            Height = 0.056f,
            RadialSegments = 6,
            Rings = 3,
            Material = new StandardMaterial3D
            {
                VertexColorUseAsAlbedo = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };

        return new CpuParticles3D
        {
            Position = tileOrigin + new Vector3(0.245f, 0.375f, 0.06f),
            Amount = 12,
            Lifetime = 2.4f,
            Preprocess = 3f,
            Mesh = mesh,
            Direction = new Vector3(0.2f, 1f, 0f),
            Spread = 7f,
            InitialVelocityMin = 0.09f,
            InitialVelocityMax = 0.14f,
            Gravity = new Vector3(0.05f, 0.03f, 0.02f),
            ScaleAmountMin = 0.7f,
            ScaleAmountMax = 1.6f,
            ColorRamp = gradient,
        };
    }

    // 이웃한 강/다리 타일의 방향을 보고 직선·커브·끝 모델과 회전을 고른다.
    private void BuildRiverTile(HexMap map, HexCoord tile, TerrainType terrain)
    {
        var connections = new System.Collections.Generic.List<float>();
        foreach (var neighbor in tile.Neighbors())
        {
            if (!map.Contains(neighbor))
            {
                continue;
            }

            if (map.TerrainAt(neighbor) is TerrainType.River or TerrainType.Bridge)
            {
                var delta = HexToWorld(neighbor) - HexToWorld(tile);
                connections.Add(Mathf.RadToDeg(Mathf.Atan2(delta.Z, delta.X)));
            }
        }

        var (scene, yRotation) = SelectRiverModel(terrain, connections);
        var instance = scene.Instantiate<Node3D>();
        instance.Position = HexToWorld(tile);
        instance.RotationDegrees = new Vector3(0f, yRotation, 0f);
        AddChild(instance);
    }

    private (PackedScene Scene, float RotationY) SelectRiverModel(
        TerrainType terrain, System.Collections.Generic.List<float> connections)
    {
        if (connections.Count == 0)
        {
            return (_riverEnd, 0f);
        }

        if (connections.Count == 1)
        {
            return (_riverEnd, EndAngle - connections[0]);
        }

        var t1 = connections[0];
        var t2 = connections[1];
        var span = Mathf.Abs(Mathf.Wrap(t2 - t1, -180f, 180f));

        if (span > 150f)
        {
            // 반대편으로 관통 — 직선(또는 다리).
            var scene = terrain == TerrainType.Bridge ? _bridge : _riverStraight;
            return (scene, StraightAxisAngle - t1);
        }

        var (a1, a2) = span > 90f ? CornerAngles : CornerSharpAngles;
        var scene2 = span > 90f ? _riverCorner : _riverCornerSharp;

        // 모델 물길(A1→A2)의 벌어진 방향과 실제 연결(T1→T2)의 방향을 맞춘다.
        var modelSpan = Mathf.Wrap(a2 - a1, -180f, 180f);
        var connSpan = Mathf.Wrap(t2 - t1, -180f, 180f);
        return Mathf.Sign(modelSpan) == Mathf.Sign(connSpan)
            ? (scene2, a1 - t1)
            : (scene2, a1 - t2);
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
