namespace SanguoSLG.Core.Simulation.RenewalMovement;

/// <summary>
/// 화면 프레임 시간을 0.1초 논리 틱으로 바꾸는 누산기.
/// 시뮬레이션 상태는 고정 틱만 소비하므로 30/60/144FPS에서 결과가 같다.
/// </summary>
public readonly record struct RenewalFixedStepClock(long PendingMicroseconds = 0)
{
    public const long TickMicroseconds = 100_000;
}
