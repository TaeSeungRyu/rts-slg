namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 부대 이동 모드(doc/design-movement.md). 적을 탐지했을 때의 반응을 가른다.
/// </summary>
public enum UnitMode
{
    /// <summary>행군모드 — 탐지해도 무시하고 계속 간다(탐지 범위 안에서는 속도 −1, 최소 1).</summary>
    March,

    /// <summary>공격모드 — 탐지하면 목표를 버리고 추격, 사거리에 닿으면 정지한다.</summary>
    Attack,
}
