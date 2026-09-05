namespace SanguoSLG.Core.Simulation;

/// <summary>등용 확률. v2에서는 충성·배신 축을 제거하고 수행 장수 정치만 사용한다.</summary>
public static class EnlistOdds
{
    public static int SuccessPercent(int recruiterPolitics) => System.Math.Clamp(recruiterPolitics, 0, 100);
}
