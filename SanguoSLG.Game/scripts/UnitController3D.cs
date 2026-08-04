using System.Linq;
using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 3D 유닛 토큰. 좌클릭한 지점을 지면(y=0)으로 레이캐스트해 헥사를 찾고,
/// Core A* 경로를 따라 트윈으로 이동한다. 경로·이동 규칙은 Core가 소유한다.
/// </summary>
public partial class UnitController3D : Node3D
{
    [Export] public float StepSeconds = 0.16f;

    private MapView3D _view = null!;
    private HexMap _map = null!;
    private MovementService _movement = null!;
    private Unit _unit = null!;
    private Camera3D _camera = null!;
    private bool _moving;

    public void Init(HexMap map, MapView3D view, Camera3D camera, Unit unit)
    {
        _map = map;
        _view = view;
        _camera = camera;
        _movement = new MovementService(map);
        _unit = unit;
        Position = TokenPosition(unit.Position);
        BuildToken();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_moving || @event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            return;
        }

        // 마우스 → 지면(y=0) 레이캐스트.
        var origin = _camera.ProjectRayOrigin(click.Position);
        var direction = _camera.ProjectRayNormal(click.Position);
        if (Mathf.Abs(direction.Y) < 0.0001f)
        {
            return;
        }

        var t = -origin.Y / direction.Y;
        if (t <= 0f)
        {
            return;
        }

        var target = _view.WorldToHex(origin + direction * t);
        if (!_map.Contains(target))
        {
            return;
        }

        var result = _movement.MoveTo(_unit, target);
        if (result.Moved && result.Path.Count > 1)
        {
            AnimateAlong(result);
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
