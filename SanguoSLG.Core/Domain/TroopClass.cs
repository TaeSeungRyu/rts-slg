namespace SanguoSLG.Core.Domain;

/// <summary>
/// 병종/역할 분류 8종(spec-unit.md "병종 분류", Phase 11B/11C). 공격·방어 능력과 장수 적성이 분류 단위로 정의된다.
/// </summary>
public enum TroopClass
{
    /// <summary>보병(도검병 계열).</summary>
    Infantry,

    /// <summary>궁병.</summary>
    Archer,

    /// <summary>기병.</summary>
    Cavalry,

    /// <summary>상병(象兵, 코끼리 계열).</summary>
    Elephant,

    /// <summary>공성(투석기·공성탑·충차).</summary>
    Siege,

    /// <summary>해상(대하 유닛).</summary>
    Naval,

    /// <summary>보급(보급부대 병참 적성).</summary>
    Supply,

    /// <summary>수성(성 방어 지휘 적성).</summary>
    Defense,
}
