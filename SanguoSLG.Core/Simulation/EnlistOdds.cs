namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 등용 확률(design-general-lifecycle §6). 2단계 판정의 최종 성공 확률 =
/// 수행 정치% × 대상 이탈%(= max(0, 100 − 충성)). 충성 100 이상은 이탈 0% → 성공 0%.
/// 군사 예측·UI 미리보기와 완료 정산이 같은 값을 쓴다.
/// </summary>
public static class EnlistOdds
{
    /// <summary>대상 이탈 확률(%) = max(0, 100 − 충성).</summary>
    public static int BetrayalPercent(int targetLoyalty) => System.Math.Max(0, 100 - targetLoyalty);

    /// <summary>최종 성공 확률(%) = 정치% × 이탈% / 100.</summary>
    public static int SuccessPercent(int recruiterPolitics, int targetLoyalty)
        => System.Math.Clamp(recruiterPolitics, 0, 100) * BetrayalPercent(targetLoyalty) / 100;
}
