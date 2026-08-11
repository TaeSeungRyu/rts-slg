namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 부대 이동 모드(doc/design-movement.md). 적을 탐지했을 때의 반응을 가른다.
/// </summary>
public enum UnitMode
{
    /// <summary>행군모드 — 탐지해도 무시하고 계속 간다(탐지 범위 안에서는 속도 −1, 최소 1). 공격·반격 안 함(받는 피해 70%).</summary>
    March,

    /// <summary>
    /// 전진모드 — 목표로 직행하되 선공·추격은 안 한다(정상 속도). 정지 시점에 사거리 안이면 정상 교전(반격 함).
    /// 이동은 행군처럼 멈추지 않지만 감속·일방피해가 없고, 전투는 공격모드처럼 정상이다.
    /// </summary>
    Advance,

    /// <summary>공격모드 — 탐지하면 목표를 버리고 추격, 사거리에 닿으면 정지한다.</summary>
    Attack,
}
