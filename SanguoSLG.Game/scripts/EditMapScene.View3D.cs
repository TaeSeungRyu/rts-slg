using Godot;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

public sealed partial class EditMapScene
{
    private SubViewport? _mapViewport;
    private Camera3D _mapCamera = null!;
    private MapView3D _mapView = null!;
    private Node3D _visibleTerrain = null!;
    private PackedScene _waterTile = null!;
    private readonly Dictionary<Vector2I, Node3D> _visibleTiles = [];
    private readonly Dictionary<Vector2I, Node3D> _visibleCities = [];
    private Vector2I _viewCenter;
    private Vector2 _viewCenterFraction;
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
    };

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
        Resized += () => _mapViewport.Size = new Vector2I(
            Mathf.Max(1, Mathf.RoundToInt(Size.X)), Mathf.Max(1, Mathf.RoundToInt(Size.Y)));

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
        _visibleTerrain = new Node3D();
        world.AddChild(_visibleTerrain);
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
        _viewCenterFraction = new Vector2(_viewCenter.X, _viewCenter.Y);
        PositionCamera();
        RefreshVisibleTerrain();
    }

    private HexCoord Axial(Vector2I offset) =>
        new(offset.X, offset.Y - (offset.X - (offset.X & 1)) / 2);

    private Vector2I Offset(HexCoord axial) =>
        new(axial.Q, axial.R + (axial.Q - (axial.Q & 1)) / 2);

    private void PositionCamera()
    {
        var center = _mapView.HexToWorld(Axial(_viewCenter));
        _mapCamera.Position = center + new Vector3(0f, _viewSize * 0.9f, _viewSize * 0.72f);
        _mapCamera.LookAt(center);
        _mapCamera.Size = _viewSize;
    }

    private void RefreshVisibleTerrain()
    {
        var aspect = Mathf.Max(0.5f, Size.X / Mathf.Max(1f, Size.Y));
        var spanX = Mathf.CeilToInt(_viewSize * aspect / (3f * _mapView.HexWorldSize)) + 5;
        var spanY = Mathf.CeilToInt(_viewSize * 0.75f / _mapView.HexWorldSize) + 5;
        var required = new HashSet<Vector2I>();
        for (var row = Mathf.Max(0, _viewCenter.Y - spanY); row <= Mathf.Min(_height - 1, _viewCenter.Y + spanY); row++)
        for (var col = Mathf.Max(0, _viewCenter.X - spanX); col <= Mathf.Min(_width - 1, _viewCenter.X + spanX); col++)
        {
            var tile = new Vector2I(col, row);
            required.Add(tile);
            if (_visibleTiles.ContainsKey(tile)) continue;
            var terrain = PreviewTerrain[_rows[row][col]];
            var scene = _mapView.TileScene(terrain) ??
                (terrain is TerrainType.WaterShallow or TerrainType.WaterDeep ? _waterTile :
                    _mapView.TileScene(TerrainType.Plains)!);
            var visual = scene.Instantiate<Node3D>();
            visual.Position = _mapView.HexToWorld(Axial(tile));
            _visibleTerrain.AddChild(visual);
            _visibleTiles[tile] = visual;
        }
        foreach (var tile in _visibleTiles.Keys.Where(tile => !required.Contains(tile)).ToArray())
        {
            _visibleTiles[tile].QueueFree();
            _visibleTiles.Remove(tile);
        }
        foreach (var city in _sites.Where(site => site.Kind == "city"))
        {
            var cityTile = new Vector2I(city.Column, city.Row);
            if (!required.Contains(cityTile) || _visibleCities.ContainsKey(cityTile)) continue;
            var scene = GD.Load<PackedScene>($"res://assets/models/castle-{city.Size}.glb");
            var visual = scene.Instantiate<Node3D>();
            visual.Position = _mapView.HexToWorld(Axial(cityTile)) + new Vector3(0f, _mapView.TileTopY, 0f);
            visual.RotationDegrees = new Vector3(0f, city.Direction * 60f, 0f);
            _visibleTerrain.AddChild(visual);
            _visibleCities[cityTile] = visual;
        }
        foreach (var tile in _visibleCities.Keys.Where(tile => !required.Contains(tile)).ToArray())
        {
            _visibleCities[tile].QueueFree();
            _visibleCities.Remove(tile);
        }
    }

    private void Handle3DInput(InputEvent input)
    {
        if (input is InputEventMouseButton button)
        {
            if (button.Pressed && button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                _viewSize = Mathf.Clamp(_viewSize * (button.ButtonIndex == MouseButton.WheelUp ? 0.83f : 1.2f), 7f, 48f);
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
            var difference = motion.Position - _dragLast;
            _dragLast = motion.Position;
            _viewCenterFraction = new Vector2(
                Mathf.Clamp(_viewCenterFraction.X - difference.X * _viewSize / Mathf.Max(1f, Size.Y), 0, _width - 1),
                Mathf.Clamp(_viewCenterFraction.Y - difference.Y * _viewSize / Mathf.Max(1f, Size.Y), 0, _height - 1));
            _viewCenter = new Vector2I(Mathf.RoundToInt(_viewCenterFraction.X),
                Mathf.RoundToInt(_viewCenterFraction.Y));
            PositionCamera();
            RefreshVisibleTerrain();
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
}
