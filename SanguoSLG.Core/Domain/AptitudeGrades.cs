namespace SanguoSLG.Core.Domain;

/// <summary>적성 등급 → 공격 배수(정수 퍼센트). design-combat.md "등급 → 배수" 표.</summary>
public static class AptitudeGrades
{
    /// <summary>등급의 공격 배수(퍼센트, A+ = 100 기준).</summary>
    public static int Percent(this AptitudeGrade grade) => grade switch
    {
        AptitudeGrade.F => 25,
        AptitudeGrade.D => 50,
        AptitudeGrade.C => 65,
        AptitudeGrade.B => 80,
        AptitudeGrade.A => 95,
        AptitudeGrade.APlus => 100,
        AptitudeGrade.S => 110,
        AptitudeGrade.SS => 130,
        AptitudeGrade.SSS => 200,
        _ => throw new System.ArgumentOutOfRangeException(nameof(grade)),
    };
}
