namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 성곽 등급별 성벽 최대치(spec-city "성곽 등급" — 소 3000·중 6000·대 10000). 값은 BalanceConfig에서
/// 온다. 성벽 연구 5단계(20~100%)는 후속(11단계) — 지금은 연구 완료 기준 최대치로 초기화한다.
/// </summary>
public static class CastleWall
{
    public static int Max(CastleSize castle, BalanceConfig balance) => castle switch
    {
        CastleSize.Small => balance.WallMaxSmall,
        CastleSize.Medium => balance.WallMaxMedium,
        CastleSize.Large => balance.WallMaxLarge,
        _ => balance.WallMaxSmall,
    };
}
