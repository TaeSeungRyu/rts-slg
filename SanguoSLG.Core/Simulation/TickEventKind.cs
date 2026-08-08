namespace SanguoSLG.Core.Simulation;

/// <summary>한 스텝에서 일어난 사건의 종류. GUI 표시와 테스트 검증에 쓴다.</summary>
public enum TickEventKind
{
    /// <summary>공격모드 유닛이 적을 탐지해 목표를 버리고 추격을 시작했다.</summary>
    PursuitStarted,

    /// <summary>추격하던 적이 탐지 범위를 벗어나 원래 목표로 복귀했다.</summary>
    PursuitEnded,

    /// <summary>적끼리 정면으로 부딪혀 자동 교전이 벌어졌다.</summary>
    Engaged,

    /// <summary>공격모드 유닛이 사거리 안의 적을 만나 이동을 종료했다.</summary>
    Halted,
}
