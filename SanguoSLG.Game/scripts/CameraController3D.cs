using Godot;

namespace SanguoSLG.Game;

/// <summary>쿼터뷰 3D 카메라. 휠 줌(전후 이동) + 중클릭 드래그 팬(x-z 평면).</summary>
public partial class CameraController3D : Camera3D
{
    [Export] public float ZoomStep = 1.2f;
    [Export] public float MinHeight = 3f;
    [Export] public float MaxHeight = 40f;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp)
            {
                ApplyZoom(-ZoomStep);
            }
            else if (button.ButtonIndex == MouseButton.WheelDown)
            {
                ApplyZoom(ZoomStep);
            }
        }
        else if (@event is InputEventMouseMotion motion &&
                 (motion.ButtonMask & MouseButtonMask.Middle) != 0)
        {
            // 화면 드래그를 지면(x-z) 팬으로 변환. 높이에 비례해 감도를 키운다.
            var sensitivity = Position.Y * 0.0016f;
            var forward = -GlobalTransform.Basis.Z;
            var flatForward = new Vector3(forward.X, 0f, forward.Z).Normalized();
            var flatRight = new Vector3(GlobalTransform.Basis.X.X, 0f, GlobalTransform.Basis.X.Z).Normalized();
            Position += (-flatRight * motion.Relative.X + flatForward * motion.Relative.Y) * sensitivity;
        }
    }

    private void ApplyZoom(float amount)
    {
        var forward = -GlobalTransform.Basis.Z;
        var next = Position + forward * -amount;
        if (next.Y >= MinHeight && next.Y <= MaxHeight)
        {
            Position = next;
        }
    }
}
