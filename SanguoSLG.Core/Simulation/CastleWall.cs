namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 성곽 등급별 성벽 최대치(spec-city "성곽 등급" — 소 3000·중 6000·대 10000). 값은 BalanceConfig에서
/// 온다. 성벽 연구 5단계(20~100%)는 후속(11단계) — 지금은 연구 완료 기준 최대치로 초기화한다.
/// </summary>
public static class CastleWall
{
    /// <summary>연구 완료(5단계) 기준 최대 성벽.</summary>
    public static int Max(CastleSize castle, BalanceConfig balance) => Max(castle, balance, WallResearchMaxLevel);

    /// <summary>성벽 연구 최대 단계(0=미연구 … 4=완료). 미연구 20% → 단계당 +20% → 4단계 100%.</summary>
    public const int WallResearchMaxLevel = 4;

    /// <summary>
    /// 성벽 연구 단계별 최대 성벽(design-combat "성벽 최대값"·11b). 완료값(소 3000/중 6000/대 10000)에
    /// 단계 비율(20+단계×20 %, 20~100)을 곱한다 — 미연구(0) 20%, 4단계 100%.
    /// </summary>
    public static int Max(CastleSize castle, BalanceConfig balance, int wallLevel)
    {
        var full = castle switch
        {
            CastleSize.Small => balance.WallMaxSmall,
            CastleSize.Medium => balance.WallMaxMedium,
            CastleSize.Large => balance.WallMaxLarge,
            _ => balance.WallMaxSmall,
        };
        var percent = System.Math.Clamp(20 + wallLevel * 20, 20, 100);
        return full * percent / 100;
    }
}
