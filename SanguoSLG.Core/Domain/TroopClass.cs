namespace SanguoSLG.Core.Domain;

/// <summary>
/// 병종 분류 6종(spec-unit.md "병종 분류"). 공격·방어 능력과 장수 적성이 분류 단위로 정의된다.
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

    /// <summary>공성(투석기·공성탑·벽력거).</summary>
    Siege,

    /// <summary>해상(대하 유닛).</summary>
    Naval,
}
