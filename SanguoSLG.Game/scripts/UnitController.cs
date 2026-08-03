using System.Linq;
using Godot;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

namespace SanguoSLG.Game;

/// <summary>
/// 유닛 토큰을 그리고, 좌클릭한 헥사까지 Core A* 경로를 따라 이동시킨다.
/// 경로 계산·이동 규칙은 Core(MovementService), 애니메이션·입력만 여기서 처리.
/// </summary>
public partial class UnitController : Node2D
{
    [Export] public Color UnitColor = new(0.85f, 0.32f, 0.28f);
    [Export] public float StepSeconds = 0.14f;

    private HexMapView _view = null!;
    private MovementService _movement = null!;
    private Unit _unit;
    private bool _moving;

    public void Init(HexMap map, HexMapView view, Unit unit)
    {
        _view = view;
        _movement = new MovementService(map);
        _unit = unit;
        Position = _view.CenterOf(_unit.Position);
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_moving || _view is null)
        {
            return;
        }

        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            var target = HexLayout.FromPixel(GetGlobalMousePosition(), _view.HexSize);
            var result = _movement.MoveTo(_unit, target);
            if (result.Moved && result.Path.Count > 1)
            {
                AnimateAlong(result);
            }
        }
    }

    private void AnimateAlong(MoveResult result)
    {
        _moving = true;
        var tween = CreateTween();
        foreach (var step in result.Path.Skip(1))
        {
            tween.TweenProperty(this, "position", _view.CenterOf(step), StepSeconds);
        }

        tween.Finished += () =>
        {
            _unit = result.Unit;
            _moving = false;
        };
    }

    public override void _Draw()
    {
        if (_view is null)
        {
            return;
        }

        var radius = _view.HexSize * 0.32f;
        DrawCircle(Vector2.Zero, radius, UnitColor);
        DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 24, new Color(0.95f, 0.95f, 0.95f), 2f, true);
    }
}
