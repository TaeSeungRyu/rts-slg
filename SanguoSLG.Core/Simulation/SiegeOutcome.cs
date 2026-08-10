namespace SanguoSLG.Core.Simulation;

/// <summary>성 전투 1회 교환의 결과(design-combat.md "성 전투").</summary>
/// <param name="WallStanding">이 교환 시작 시 성벽이 서 있었는가(성벽 단계 vs 붕괴 단계).</param>
/// <param name="WallDamage">성벽에 흡수된 피해.</param>
/// <param name="NewWall">교환 후 남은 성벽.</param>
/// <param name="TroopDamage">수비 병력 손실(성벽 초과분 또는 붕괴 후 직격).</param>
/// <param name="CounterDamage">각 공격 부대가 받은 성의 반격(입력 순서).</param>
public sealed record SiegeOutcome(
    bool WallStanding,
    int WallDamage,
    int NewWall,
    int TroopDamage,
    IReadOnlyList<int> CounterDamage);
