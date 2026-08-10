namespace SanguoSLG.Core.Domain;

/// <summary>
/// 장수의 병종 분류별 적성 등급(design-combat.md "장수 적성"·spec-general.md). 공격에만 곱해진다.
/// SSS는 이벤트성 장수 전용.
/// </summary>
public enum AptitudeGrade
{
    F,
    D,
    C,
    B,
    A,

    /// <summary>A+ (100% = 기준).</summary>
    APlus,
    S,
    SS,
    SSS,
}
