using System.Collections.Generic;
using System.Linq;
using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 3D 유닛 토큰. 마우스가 가리키는 헥사를 하이라이트하고 유닛→호버 지점의 A* 경로를 미리 보여준다.
/// 좌클릭하면 그 경로를 따라 트윈으로 이동한다. 경로·이동 규칙은 Core가 소유한다.
/// </summary>
public partial class UnitController3D : Node3D
{
    [Export] public float StepSeconds = 0.16f;

    private MapView3D _view = null!;
    private HexMap _map = null!;
    private MovementService _movement = null!;
    private HexPathfinder _pathfinder = null!;
    private Unit _unit = null!;
    private Camera3D _camera = null!;
    private bool _moving;

    // 하이라이트·경로 오버레이는 유닛과 함께 움직이면 안 되므로 형제 노드에 담는다.
    private Node3D _overlay = null!;
    private MeshInstance3D _hover = null!;
    private readonly List<MeshInstance3D> _pathMarkers = new();
    private HexCoord? _hoverCoord;

    public void Init(HexMap map, MapView3D view, Camera3D camera, Unit unit)
    {
        _map = map;
        _view = view;
        _camera = camera;
        _movement = new MovementService(map);
        _pathfinder = new HexPathfinder(map);
        _unit = unit;
        Position = TokenPosition(unit.Position);
        BuildToken();
        BuildOverlay();

        if (OS.GetCmdlineArgs().Contains("--previewdemo"))
        {
            UpdateHover(new HexCoord(6, 3));
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
        {
            if (!_moving && RayToGround(motion.Position) is { } hoverHex)
            {
                UpdateHover(hoverHex);
            }

            return;
        }

        if (_moving || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            return;
        }

        if (RayToGround(click.Position) is not { } target)
        {
            return;
        }

        var result = _movement.MoveTo(_unit, target);
        if (result.Moved && result.Path.Count > 1)
        {
            ClearOverlay();
            AnimateAlong(result);
        }
    }

    // 마우스 화면 좌표 → 지면(y=0) → 맵 안의 헥사. 맵 밖이면 null.
    private HexCoord? RayToGround(Vector2 screenPosition)
    {
        var origin = _camera.ProjectRayOrigin(screenPosition);
        var direction = _camera.ProjectRayNormal(screenPosition);
        if (Mathf.Abs(direction.Y) < 0.0001f)
        {
            return null;
        }

        var t = -origin.Y / direction.Y;
        if (t <= 0f)
        {
            return null;
        }

        var coord = _view.WorldToHex(origin + direction * t);
        return _map.Contains(coord) ? coord : null;
    }

    private void UpdateHover(HexCoord coord)
    {
        if (_hoverCoord == coord)
        {
            return;
        }

        _hoverCoord = coord;
        _hover.Visible = true;
        _hover.Position = _view.HexToWorld(coord) + new Vector3(0f, _view.TileTopY + 0.02f, 0f);

        var path = _pathfinder.FindPath(_unit.Position, coord);
        ShowPathMarkers(path);
    }

    private void ShowPathMarkers(IReadOnlyList<HexCoord> path)
    {
        // 시작(유닛 위치)과 끝(호버 하이라이트)은 마커를 생략한다.
        var needed = path.Count > 2 ? path.Count - 2 : 0;
        while (_pathMarkers.Count < needed)
        {
            var marker = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.09f, BottomRadius = 0.09f, Height = 0.03f, RadialSegments = 16 },
                MaterialOverride = new StandardMaterial3D
                {
                    AlbedoColor = new Color(0.95f, 0.90f, 0.70f, 0.9f),
                    EmissionEnabled = true,
                    Emission = new Color(0.55f, 0.48f, 0.28f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                },
            };
            _overlay.AddChild(marker);
            _pathMarkers.Add(marker);
        }

        for (var i = 0; i < _pathMarkers.Count; i++)
        {
            var visible = i < needed;
            _pathMarkers[i].Visible = visible;
            if (visible)
            {
                _pathMarkers[i].Position =
                    _view.HexToWorld(path[i + 1]) + new Vector3(0f, _view.TileTopY + 0.03f, 0f);
            }
        }
    }

    private void ClearOverlay()
    {
        _hoverCoord = null;
        _hover.Visible = false;
        foreach (var marker in _pathMarkers)
        {
            marker.Visible = false;
        }
    }

    private void AnimateAlong(MoveResult result)
    {
        _moving = true;
        var tween = CreateTween();
        foreach (var step in result.Path.Skip(1))
        {
            tween.TweenProperty(this, "position", TokenPosition(step), StepSeconds)
                .SetTrans(Tween.TransitionType.Sine);
        }

        tween.Finished += () =>
        {
            _unit = result.Unit;
            _moving = false;
        };
    }

    private Vector3 TokenPosition(HexCoord coord) =>
        _view.HexToWorld(coord) + new Vector3(0f, _view.TileTopY, 0f);

    private void BuildOverlay()
    {
        _overlay = new Node3D();
        GetParent().AddChild(_overlay);

        // 타일과 같은 방향(꼭짓점 ±Z)의 납작한 육각 하이라이트.
        _hover = new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = _view.HexWorldSize * 0.94f,
                BottomRadius = _view.HexWorldSize * 0.94f,
                Height = 0.04f,
                RadialSegments = 6,
            },
            Visible = false,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(1f, 0.92f, 0.55f, 0.35f),
                EmissionEnabled = true,
                Emission = new Color(0.6f, 0.52f, 0.25f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            },
        };
        _overlay.AddChild(_hover);
    }

    private void BuildToken()
    {
        var body = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.16f, BottomRadius = 0.22f, Height = 0.5f },
            Position = new Vector3(0f, 0.25f, 0f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.78f, 0.22f, 0.19f),
                Roughness = 0.45f,
                Metallic = 0.15f,
            },
        };
        AddChild(body);

        var cap = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.17f, Height = 0.34f },
            Position = new Vector3(0f, 0.55f, 0f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.92f, 0.86f, 0.72f),
                Roughness = 0.35f,
                Metallic = 0.4f,
            },
        };
        AddChild(cap);
    }
}
