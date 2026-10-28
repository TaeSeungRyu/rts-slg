using Godot;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public sealed partial class EditMapScene
{
    private SubViewport? _mapViewport;
    private Camera3D _mapCamera = null!;
    private MapView3D _mapView = null!;
    private Node3D _visibleTerrain = null!;
    private MeshInstance3D _selectionVisual = null!;
    private MeshInstance3D? _routeLines;
    private PackedScene _waterTile = null!;
    private PackedScene _bridgeTile = null!;
    private readonly Dictionary<Vector2I, Node3D> _visibleTiles = [];
    private readonly Dictionary<Vector2I, Node3D> _visibleCities = [];
    private readonly Dictionary<int, Node3D> _visibleStructures = [];
    private Vector2I _viewCenter;
    private Vector3 _cameraTarget;
    private float _viewSize = 30f;
    private Vector2 _pointerDown;
    private bool _panningMap;

    private static readonly Dictionary<char, TerrainType> PreviewTerrain = new()
    {
        ['P'] = TerrainType.Plains, ['F'] = TerrainType.Forest,
        ['M'] = TerrainType.Mountain, ['H'] = TerrainType.RockMountain,
        ['D'] = TerrainType.Desert, ['R'] = TerrainType.River,
        ['B'] = TerrainType.Bridge, ['W'] = TerrainType.WaterShallow,
        ['O'] = TerrainType.WaterDeep,
        ['S'] = TerrainType.Rocks, ['T'] = TerrainType.RockHill,
        ['X'] = TerrainType.Karst, ['C'] = TerrainType.Cliff,
        ['I'] = TerrainType.IceMountain, ['E'] = TerrainType.IceWallLarge,
        ['L'] = TerrainType.IceWallSmall, ['N'] = TerrainType.Swamp,
        ['Q'] = TerrainType.DesertCactus, ['V'] = TerrainType.WaterRocks,
    };

    private PackedScene TerrainScene(char code) => PreviewTerrain[code] switch
    {
        TerrainType.River => _waterTile,
        TerrainType.Bridge => _bridgeTile,
        TerrainType.WaterShallow or TerrainType.WaterDeep => _waterTile,
        var terrain => _mapView.TileScene(terrain) ?? _mapView.TileScene(TerrainType.Plains)!,
    };

    private (PackedScene Scene, float Rotation) TerrainVisualFor(Vector2I tile)
    {
        var code = _rows[tile.Y][tile.X];
        if (code == 'R') return (_waterTile, 0f);
        if (code != 'B') return (TerrainScene(code), 0f);
        var center = Axial(tile);
        var angles = center.Neighbors().Select(neighbor => new
            {
                Tile = Offset(neighbor),
                Angle = Mathf.RadToDeg(Mathf.Atan2(
                    (_mapView.HexToWorld(neighbor) - _mapView.HexToWorld(center)).Z,
                    (_mapView.HexToWorld(neighbor) - _mapView.HexToWorld(center)).X)),
            })
            .Where(item => Inside(item.Tile) && _rows[item.Tile.Y][item.Tile.X] is 'R' or 'B' or 'W' or 'O')
            .Select(item => item.Angle).ToArray();
        return _mapView.RiverModelForConnections(PreviewTerrain[code], angles);
    }

    private void Build3DView()
    {
        var container = new SubViewportContainer
        {
            Name = "MapPreview3D",
            Stretch = true,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        container.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(container);
        _mapViewport = new SubViewport
        {
            Size = new Vector2I(1280, 720),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            TransparentBg = false,
        };
        container.AddChild(_mapViewport);

        var world = new Node3D();
        _mapViewport.AddChild(world);
        world.AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color("91bcb4"),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Colors.White,
                AmbientLightEnergy = 0.8f,
            },
        });
        world.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-55f, -35f, 0f),
            LightEnergy = 1.1f,
            ShadowEnabled = false,
        });
        _mapView = new MapView3D();
        world.AddChild(_mapView);
        _waterTile = GD.Load<PackedScene>("res://assets/models/water.glb");
        _bridgeTile = GD.Load<PackedScene>("res://assets/models/bridge.glb");
        _visibleTerrain = new Node3D();
        world.AddChild(_visibleTerrain);
        _selectionVisual = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.54f, BottomRadius = 0.54f,
                Height = 0.012f, RadialSegments = 6 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.82f, 0.29f, 0.36f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
            Visible = false,
        };
        world.AddChild(_selectionVisual);
        BuildRouteLines(world);
        _mapCamera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = _viewSize,
            Near = 0.5f,
            Far = 400f,
            Current = true,
        };
        world.AddChild(_mapCamera);
        _viewCenter = new Vector2I(_width / 2, _height / 2);
        _cameraTarget = _mapView.HexToWorld(Axial(_viewCenter));
        PositionCamera();
        RefreshVisibleTerrain();
    }

    private HexCoord Axial(Vector2I offset) =>
        new(offset.X, offset.Y - (offset.X - (offset.X & 1)) / 2);

    private Vector2I Offset(HexCoord axial) =>
        new(axial.Q, axial.R + (axial.Q - (axial.Q & 1)) / 2);

    private void PositionCamera()
    {
        _mapCamera.Position = _cameraTarget + Vector3.Up * _viewSize;
        _mapCamera.RotationDegrees = new Vector3(-90f, _mapView.IsFlatTop ? 0f : 30f, 0f);
        _mapCamera.Size = _viewSize;
    }

    private Vector3 GroundPoint(Vector2 screen)
    {
        var origin = _mapCamera.ProjectRayOrigin(screen);
        var direction = _mapCamera.ProjectRayNormal(screen);
        return origin + direction * ((0f - origin.Y) / direction.Y);
    }

    private void PanMap(Vector2 previous, Vector2 current)
    {
        var difference = GroundPoint(previous) - GroundPoint(current);
        MoveMapCamera(difference);
    }

    private void MoveMapCamera(Vector3 difference)
    {
        var columnAxis = _mapView.HexToWorld(Axial(new Vector2I(2, 0))) * 0.5f;
        var rowAxis = _mapView.HexToWorld(Axial(new Vector2I(0, 1)));
        var proposed = _cameraTarget + difference;
        var column = Mathf.Clamp(proposed.Dot(columnAxis) / columnAxis.LengthSquared(), 0f, _width - 1f);
        var row = Mathf.Clamp(proposed.Dot(rowAxis) / rowAxis.LengthSquared(), 0f,
            _height - 1f + ((_width - 1) & 1) * 0.5f);
        _cameraTarget = columnAxis * column + rowAxis * row;
        PositionCamera();
        var selected = Offset(_mapView.WorldToHex(_cameraTarget));
        selected = new Vector2I(Mathf.Clamp(selected.X, 0, _width - 1),
            Mathf.Clamp(selected.Y, 0, _height - 1));
        if (selected == _viewCenter) return;
        _viewCenter = selected;
        RefreshVisibleTerrain();
    }

    public override void _Process(double delta)
    {
        _nativeNameEdit?.SyncBounds();
        if (_mapViewport is null || _editorModal is not null || GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit)
            return;
        var direction = Vector2.Zero;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) direction.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) direction.X += 1;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) direction.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) direction.Y += 1;
        if (direction == Vector2.Zero) return;
        var speed = Mathf.Max(8f, _viewSize * 0.9f) * (float)delta;
        var right = _mapCamera.GlobalTransform.Basis.X;
        var down = -_mapCamera.GlobalTransform.Basis.Y;
        MoveMapCamera((right * direction.X + down * direction.Y) * speed);
    }

    private void FocusRegion(double longitude, double latitude)
    {
        var column = Mathf.Clamp((int)((longitude - _west) / (_east - _west) * _width), 0, _width - 1);
        var row = Mathf.Clamp((int)((_north - latitude) / (_north - _south) * _height), 0, _height - 1);
        _viewCenter = new Vector2I(column, row);
        _cameraTarget = _mapView.HexToWorld(Axial(_viewCenter));
        PositionCamera();
        RefreshVisibleTerrain();
    }

    private void RefreshVisibleTerrain()
    {
        var aspect = Mathf.Max(0.5f, Size.X / Mathf.Max(1f, Size.Y));
        var spanX = Mathf.CeilToInt(_viewSize * aspect / (3f * _mapView.HexWorldSize)) + 5;
        var spanY = Mathf.CeilToInt(_viewSize * 0.75f / _mapView.HexWorldSize) + 5;
        var required = new HashSet<Vector2I>();
        var facilityTiles = _sites.Where(site => site.Kind == "facility")
            .Select(site => new Vector2I(site.Column, site.Row)).ToHashSet();
        for (var row = Mathf.Max(0, _viewCenter.Y - spanY); row <= Mathf.Min(_height - 1, _viewCenter.Y + spanY); row++)
        for (var col = Mathf.Max(0, _viewCenter.X - spanX); col <= Mathf.Min(_width - 1, _viewCenter.X + spanX); col++)
        {
            var tile = new Vector2I(col, row);
            required.Add(tile);
            if (facilityTiles.Contains(tile)) continue;
            if (_visibleTiles.ContainsKey(tile)) continue;
            var (scene, rotation) = TerrainVisualFor(tile);
            var visual = scene.Instantiate<Node3D>();
            visual.Position = _mapView.HexToWorld(Axial(tile));
            visual.RotationDegrees = new Vector3(0f, rotation, 0f);
            if (_rows[row][col] == 'R') MapView3D.TintRiverTile(visual);
            _visibleTerrain.AddChild(visual);
            _visibleTiles[tile] = visual;
            ApplyTerrainFilterToVisual(visual, _rows[row][col]);
        }
        foreach (var tile in _visibleTiles.Keys.Where(tile => !required.Contains(tile)).ToArray())
        {
            _visibleTiles[tile].QueueFree();
            _visibleTiles.Remove(tile);
        }
        if (!_showCities)
        {
            foreach (var visual in _visibleCities.Values) visual.QueueFree();
            _visibleCities.Clear();
        }
        foreach (var city in _sites.Where(site => _showCities && site.Kind == "city"))
        {
            var cityTile = new Vector2I(city.Column, city.Row);
            if (!required.Contains(cityTile) || _visibleCities.ContainsKey(cityTile)) continue;
            var scene = GD.Load<PackedScene>($"res://assets/models/castle-{city.Size}.glb");
            var visual = scene.Instantiate<Node3D>();
            var occupied = Footprint(city).ToArray();
            visual.Position = occupied.Select(cell => _mapView.HexToWorld(Axial(cell)))
                .Aggregate(Vector3.Zero, (sum, position) => sum + position) / occupied.Length
                + new Vector3(0f, _mapView.TileTopY, 0f);
            visual.RotationDegrees = new Vector3(0f, city.Direction * 60f, 0f);
            _visibleTerrain.AddChild(visual);
            _visibleCities[cityTile] = visual;
        }
        foreach (var tile in _visibleCities.Keys.Where(tile => !required.Contains(tile)).ToArray())
        {
            _visibleCities[tile].QueueFree();
            _visibleCities.Remove(tile);
        }
        foreach (var site in _sites.Where(site => site.Kind is "facility" or "port" or "landmark" or "ruin"))
        {
            var tile = new Vector2I(site.Column, site.Row);
            if (!required.Contains(tile) || _visibleStructures.ContainsKey(site.Id)) continue;
            var asset = site.Kind == "ruin" ? "ruin-common"
                : site.Kind == "landmark" ? "waterfall-cliff"
                : site.Kind == "port" ? $"port-{site.Size}" : site.Size switch
            {
                "paddy" => "paddy",
                "farm" => "building-farm",
                "village" => "village-1",
                _ => "workshop",
            };
            var scene = GD.Load<PackedScene>($"res://assets/models/{asset}.glb");
            var visual = scene.Instantiate<Node3D>();
            var occupied = Footprint(site).ToArray();
            visual.Position = occupied.Select(cell => _mapView.HexToWorld(Axial(cell)))
                .Aggregate(Vector3.Zero, (sum, position) => sum + position) / occupied.Length
                + new Vector3(0f, _mapView.TileTopY, 0f);
            visual.RotationDegrees = new Vector3(0f, site.Direction * 60f, 0f);
            _visibleTerrain.AddChild(visual);
            _visibleStructures[site.Id] = visual;
        }
        foreach (var id in _visibleStructures.Keys.Where(id =>
                     !_sites.Any(site => site.Id == id && required.Contains(new Vector2I(site.Column, site.Row)))).ToArray())
        {
            _visibleStructures[id].QueueFree();
            _visibleStructures.Remove(id);
        }
    }

    private void BuildRouteLines(Node3D world)
    {
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        foreach (var route in _landRoutes)
        for (var index = 1; index < route.Length; index++)
        {
            mesh.SurfaceAddVertex(_mapView.HexToWorld(Axial(route[index - 1]))
                + new Vector3(0f, _mapView.TileTopY + 0.07f, 0f));
            mesh.SurfaceAddVertex(_mapView.HexToWorld(Axial(route[index]))
                + new Vector3(0f, _mapView.TileTopY + 0.07f, 0f));
        }
        mesh.SurfaceEnd();
        _routeLines = new MeshInstance3D
        {
            Mesh = mesh,
            Visible = _showLandRoutes,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color("f2dfa1"),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                NoDepthTest = true,
            },
        };
        world.AddChild(_routeLines);
    }

    private void ApplyTerrainFilterVisual()
    {
        if (_mapViewport is null) return;
        foreach (var (tile, visual) in _visibleTiles)
            ApplyTerrainFilterToVisual(visual, _rows[tile.Y][tile.X]);
    }

    private void ApplyTerrainFilterToVisual(Node3D node, char code)
    {
        var alpha = _terrainFilter is null || _terrainFilter == code ? 0f : 0.66f;
        void Apply(Node child)
        {
            if (child is GeometryInstance3D geometry) geometry.Transparency = alpha;
            foreach (var grandchild in child.GetChildren()) Apply(grandchild);
        }
        Apply(node);
    }

    private void Handle3DInput(InputEvent input)
    {
        if (input is InputEventMouseButton button)
        {
            if (button.Pressed && button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                _viewSize = Mathf.Clamp(_viewSize * (button.ButtonIndex == MouseButton.WheelUp ? 0.83f : 1.2f), 7f, 85f);
                PositionCamera();
                RefreshVisibleTerrain();
                AcceptEvent();
            }
            else if (button.ButtonIndex is MouseButton.Left or MouseButton.Middle)
            {
                if (button.Pressed)
                {
                    _pointerDown = button.Position;
                    _dragLast = button.Position;
                    _panningMap = true;
                }
                else
                {
                    _panningMap = false;
                    if (button.ButtonIndex == MouseButton.Left && button.Position.DistanceTo(_pointerDown) < 5f)
                        Select3DTile(button.Position);
                }
                AcceptEvent();
            }
        }
        else if (input is InputEventMouseMotion motion && _panningMap)
        {
            PanMap(_dragLast, motion.Position);
            _dragLast = motion.Position;
            AcceptEvent();
        }
    }

    private void Select3DTile(Vector2 screen)
    {
        var origin = _mapCamera.ProjectRayOrigin(screen);
        var direction = _mapCamera.ProjectRayNormal(screen);
        if (Mathf.Abs(direction.Y) < 0.001f) return;
        var world = origin + direction * ((_mapView.TileTopY - origin.Y) / direction.Y);
        var tile = Offset(_mapView.WorldToHex(world));
        if (tile.X < 0 || tile.X >= _width || tile.Y < 0 || tile.Y >= _height) return;
        ShowEditorPalette(tile, screen);
    }

    private void ShowSelectedTile(Vector2I tile)
    {
        _selectionVisual.Position = _mapView.HexToWorld(Axial(tile))
            + new Vector3(0f, _mapView.TileTopY + 0.04f, 0f);
        _selectionVisual.Visible = true;
    }
}
