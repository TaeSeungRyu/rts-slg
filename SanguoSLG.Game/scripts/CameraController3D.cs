using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 피벗(지면 주시점) 궤도 카메라. 휠 줌, 중클릭 드래그 팬, Q/E 회전.
/// 목표값(yaw·거리·피벗)을 두고 매 프레임 지수 보간해 부드럽게 따라간다.
/// </summary>
public partial class CameraController3D : Camera3D
{
    [Export] public float MinDistance = 4f;
    [Export] public float MaxDistance = 50f;
    [Export] public float RotateSpeedDeg = 100f;
    [Export] public float Smoothing = 10f;

    private Vector3 _pivot;
    private Vector3 _targetPivot;
    private float _yaw;
    private float _targetYaw;
    private readonly float _pitch = Mathf.DegToRad(49f);
    private float _distance = 10f;
    private float _targetDistance = 10f;

    /// <summary>주시점과 거리로 카메라를 초기화한다(트리에 추가된 뒤 호출).</summary>
    public void Setup(Vector3 pivot, float distance)
    {
        _pivot = _targetPivot = pivot;
        _distance = _targetDistance = Mathf.Clamp(distance, MinDistance, MaxDistance);
        _yaw = _targetYaw = 0f;
        ApplyTransform();
    }

    public override void _Process(double delta)
    {
        var dt = (float)delta;

        var rotate = 0f;
        if (Input.IsKeyPressed(Key.Q))
        {
            rotate += 1f;
        }

        if (Input.IsKeyPressed(Key.E))
        {
            rotate -= 1f;
        }

        _targetYaw += rotate * Mathf.DegToRad(RotateSpeedDeg) * dt;

        var weight = 1f - Mathf.Exp(-Smoothing * dt);
        _yaw = Mathf.LerpAngle(_yaw, _targetYaw, weight);
        _distance = Mathf.Lerp(_distance, _targetDistance, weight);
        _pivot = _pivot.Lerp(_targetPivot, weight);
        ApplyTransform();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } button)
        {
            if (button.ButtonIndex == MouseButton.WheelUp)
            {
                _targetDistance = Mathf.Clamp(_targetDistance * 0.88f, MinDistance, MaxDistance);
            }
            else if (button.ButtonIndex == MouseButton.WheelDown)
            {
                _targetDistance = Mathf.Clamp(_targetDistance / 0.88f, MinDistance, MaxDistance);
            }
        }
        else if (@event is InputEventMouseMotion motion &&
                 (motion.ButtonMask & MouseButtonMask.Middle) != 0)
        {
            // 화면 드래그를 카메라 기준 지면(x-z) 팬으로 변환. 거리에 비례해 감도를 키운다.
            var sensitivity = _distance * 0.0016f;
            var forward = -GlobalTransform.Basis.Z;
            var flatForward = new Vector3(forward.X, 0f, forward.Z).Normalized();
            var flatRight = new Vector3(GlobalTransform.Basis.X.X, 0f, GlobalTransform.Basis.X.Z).Normalized();
            _targetPivot += (-flatRight * motion.Relative.X + flatForward * motion.Relative.Y) * sensitivity;
        }
    }

    private void ApplyTransform()
    {
        var offset = new Vector3(
            Mathf.Sin(_yaw) * Mathf.Cos(_pitch),
            Mathf.Sin(_pitch),
            Mathf.Cos(_yaw) * Mathf.Cos(_pitch)) * _distance;
        LookAtFromPosition(_pivot + offset, _pivot, Vector3.Up);
    }
}
