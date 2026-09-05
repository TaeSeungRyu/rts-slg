namespace SanguoSLG.Core.Simulation;

/// <summary>최소 외교 규칙. 관계도 데이터가 들어오기 전까지는 수행 장수 정치가 성공률의 중심이다.</summary>
public static class DiplomacyRules
{
    public static int AllianceSuccessPercent(int envoyPolitics)
        => System.Math.Clamp(envoyPolitics, 0, 100);

    /// <summary>
    /// 군사 예측. 실제 성공 확률상 성공이 유력한지(50% 이상)를 기준으로 말하되,
    /// 군사 지력% 확률로 맞고 나머지는 반대로 말한다.
    /// </summary>
    public static bool AdvisorPredictsSuccess(int successPercent, int strategistIntellect, IRandomSource random)
    {
        var actualLikelySuccess = System.Math.Clamp(successPercent, 0, 100) >= 50;
        var accurate = random.Next(0, 100) < System.Math.Clamp(strategistIntellect, 0, 100);
        return accurate ? actualLikelySuccess : !actualLikelySuccess;
    }
}
