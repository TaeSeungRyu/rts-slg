using Godot;

namespace SanguoSLG.Game;

/// <summary>
/// 피벗(지면 주시점) 궤도 카메라. 휠 줌, 중클릭 드래그 팬, Q/E 회전.
/// 목표값(yaw·거리·피벗)을 두고 매 프레임 지수 보간해 부드럽게 따라간다.
/// </summary>
public partial class CameraController3D : Camera3D
{
    [Export] public float MinDistance = 2.2f;
    [Export] public float MaxDistance = 50f;
    [Export] public float RotateSpeedDeg = 100f;
    [Export] public float Smoothing = 10f;
    [Export] public float KeyPanSpeed = 0.9f;

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
        // 깊이 버퍼 정밀도의 근본 설정. Godot 기본값(near 0.05 / far 4000)은 범위가
        // 80,000:1이라 정밀도가 흩어지고, 맞닿은 면들이 카메라가 움직일 때마다
        // 앞뒤가 뒤바뀌며 깜빡인다(z-파이팅). 이 게임은 타일 반경 0.577,
        // 카메라 거리 2.2~50의 작은 월드라 근평면을 크게 올릴 수 있다 —
        // near를 10배 올리면 정밀도가 그만큼 좋아진다.
        Near = 0.5f;   // 최소 줌 거리 2.2보다 훨씬 작아 잘릴 위험 없음
        Far = 700f;    // 600x600 바다 평면의 먼 모서리까지 여유 있게 포함

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

        // WASD / 화살표 키 팬(카메라가 보는 방향 기준)
        var pan = Vector2.Zero;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
        {
            pan.Y += 1f;
        }

        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
        {
            pan.Y -= 1f;
        }

        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
        {
            pan.X -= 1f;
        }

        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
        {
            pan.X += 1f;
        }

        if (pan != Vector2.Zero)
        {
            var (flatForward, flatRight) = FlatBasis();
            var speed = KeyPanSpeed * Mathf.Max(_distance * 0.35f, 1f) * dt;
            _targetPivot += (flatRight * pan.X + flatForward * pan.Y) * speed;
        }

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
                 (motion.ButtonMask & (MouseButtonMask.Left | MouseButtonMask.Middle)) != 0)
        {
            // 좌클릭 또는 중클릭 드래그를 지면(x-z) 팬으로 변환(우클릭 팬 제거). 거리 비례 감도.
            var sensitivity = _distance * 0.0016f;
            var (flatForward, flatRight) = FlatBasis();
            _targetPivot += (-flatRight * motion.Relative.X + flatForward * motion.Relative.Y) * sensitivity;
        }
    }

    // 카메라가 보는 방향의 수평(지면) 기저 벡터.
    private (Vector3 Forward, Vector3 Right) FlatBasis()
    {
        var forward = -GlobalTransform.Basis.Z;
        var flatForward = new Vector3(forward.X, 0f, forward.Z).Normalized();
        var flatRight = new Vector3(GlobalTransform.Basis.X.X, 0f, GlobalTransform.Basis.X.Z).Normalized();
        return (flatForward, flatRight);
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
