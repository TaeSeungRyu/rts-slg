using Godot;

namespace SanguoSLG.Game;

/// <summary>휠 줌 + 중클릭 드래그 팬 카메라.</summary>
public partial class CameraController : Camera2D
{
    [Export] public float ZoomStep = 0.1f;
    [Export] public float ZoomMin = 0.3f;
    [Export] public float ZoomMax = 3.0f;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp)
            {
                ApplyZoom(ZoomStep);
            }
            else if (button.ButtonIndex == MouseButton.WheelDown)
            {
                ApplyZoom(-ZoomStep);
            }
        }
        else if (@event is InputEventMouseMotion motion &&
                 (motion.ButtonMask & MouseButtonMask.Middle) != 0)
        {
            Position -= motion.Relative / Zoom;
        }
    }

    private void ApplyZoom(float delta)
    {
        var next = Mathf.Clamp(Zoom.X + delta, ZoomMin, ZoomMax);
        Zoom = new Vector2(next, next);
    }
}
