namespace SanguoSLG.Core.Simulation;

/// <summary>최소 외교 규칙. 관계도 데이터가 들어오기 전까지는 수행 장수 정치가 성공률의 중심이다.</summary>
public static class DiplomacyRules
{
    public static int AllianceSuccessPercent(int envoyPolitics)
        => System.Math.Clamp(envoyPolitics, 0, 100);
}
