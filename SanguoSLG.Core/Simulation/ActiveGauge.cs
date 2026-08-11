namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 액티브 스킬의 야전 충전 게이지(design-skill.md·design-skill-actives.md). 부대가 성 밖에 나와 있는
/// 경과일을 세어 <see cref="ReadyDays"/>(5일)에 도달하면 발동 준비된다. 발동(1회 소비)하거나 성으로
/// 복귀하면 0으로 초기화된다. 트리거가 순수 경과일이라 결정론적이다(난수 없음). 불변 값.
/// </summary>
/// <param name="ElapsedDays">현재 누적 야전 경과일.</param>
public sealed record ActiveGauge(int ElapsedDays = 0)
{
    /// <summary>발동에 필요한 야전 경과일(design 확정 5일).</summary>
    public const int ReadyDays = 5;

    /// <summary>발동 준비됨(경과일이 문턱 이상).</summary>
    public bool IsReady => ElapsedDays >= ReadyDays;

    /// <summary>야전 하루-진행 경과: 경과일을 <paramref name="days"/>만큼 늘린다.</summary>
    public ActiveGauge Tick(int days) => days <= 0 ? this : new ActiveGauge(ElapsedDays + days);

    /// <summary>발동해 1회 소비 — 0으로 초기화.</summary>
    public ActiveGauge Fire() => new(0);

    /// <summary>성 복귀 — 0으로 초기화.</summary>
    public ActiveGauge Reset() => new(0);
}
