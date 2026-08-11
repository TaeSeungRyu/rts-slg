namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 패시브 스킬의 발동 조건(design-skill-passives.md). 전부 결정론적으로 판정 가능한 전투 상태만.
/// </summary>
public enum PassiveCondition
{
    /// <summary>무조건.</summary>
    Always,

    /// <summary>대상이 건물(성·항구).</summary>
    TargetBuilding,

    /// <summary>대상이 유닛.</summary>
    TargetUnit,

    /// <summary>내가 숲·산 지형.</summary>
    Rough,

    /// <summary>내가 평야·사막 지형.</summary>
    PlainsDesert,

    /// <summary>다대일에서 내가 다수(포위) 측.</summary>
    Momentum,

    /// <summary>추격 중.</summary>
    Pursuit,

    /// <summary>상대가 행군모드.</summary>
    EnemyMarching,

    /// <summary>사거리 1(인접) 교전으로 내가 공격.</summary>
    Melee,

    /// <summary>사거리 1 공격을 받는 중.</summary>
    MeleeIncoming,

    /// <summary>사거리 2(원거리) 공격을 받는 중.</summary>
    RangedIncoming,

    /// <summary>병력 50% 이하.</summary>
    HpBelowHalf,

    /// <summary>병력 50% 초과.</summary>
    HpAboveHalf,

    /// <summary>성·항구에 주둔 중.</summary>
    CastleGarrison,

    /// <summary>다대일로 포위당함.</summary>
    Surrounded,

    /// <summary>야전(성·항구 밖).</summary>
    Field,
}
