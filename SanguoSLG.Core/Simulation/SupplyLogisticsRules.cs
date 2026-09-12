namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 보급부대 전용 적성 보정. 일반 전투 적성은 A+를 100% 기준으로 삼지만, 보급 적성은
/// 모든 장수의 초기 기본값인 C를 100% 기준으로 삼아 기존 보급 성능을 유지한다.
/// </summary>
public static class SupplyLogisticsRules
{
    public static int EfficiencyPercent(AptitudeGrade grade) => grade switch
    {
        AptitudeGrade.F => 70,
        AptitudeGrade.D => 85,
        AptitudeGrade.C => 100,
        AptitudeGrade.B => 110,
        AptitudeGrade.A => 120,
        AptitudeGrade.APlus => 130,
        AptitudeGrade.S => 145,
        AptitudeGrade.SS => 160,
        AptitudeGrade.SSS => 180,
        _ => throw new System.ArgumentOutOfRangeException(nameof(grade)),
    };

    public static int Apply(int value, int efficiencyPercent)
        => (int)(((long)value * efficiencyPercent + 50) / 100);
}
