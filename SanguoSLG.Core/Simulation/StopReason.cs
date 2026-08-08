namespace SanguoSLG.Core.Simulation;

/// <summary>한 번의 "진행"이 멈춘 이유(doc/design-movement.md 진행 중단 조건).</summary>
public enum StopReason
{
    /// <summary>공격모드 유닛의 사거리 안에 적이 들어왔다 — 전투 페이즈로.</summary>
    EnemyInRange,

    /// <summary>적끼리 정면으로 부딪혔다(같은 칸 경합·자리 맞바꾸기) — 자동 교전.</summary>
    Engaged,

    /// <summary>모든 유닛이 목표에 도착했다.</summary>
    AllArrived,

    /// <summary>길이 막혀 3일 연속 못 움직인 유닛이 생겼다.</summary>
    Blocked,

    /// <summary>7일(최대 일수)이 지났다.</summary>
    MaxDays,
}
