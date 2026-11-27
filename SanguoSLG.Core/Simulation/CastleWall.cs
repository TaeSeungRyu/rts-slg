namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;

/// <summary>
/// 성곽 등급별 성벽 최대치. 0~4 연구 단계에서 초기·중간·완료 값을 잇는다.
/// </summary>
public static class CastleWall
{
    /// <summary>연구 완료(5단계) 기준 최대 성벽.</summary>
    public static int Max(CastleSize castle, BalanceConfig balance) => Max(castle, balance, WallResearchMaxLevel);

    /// <summary>성벽 연구 최대 단계(0=미연구 … 4=완료).</summary>
    public const int WallResearchMaxLevel = 4;

    /// <summary>
    /// 0·2·4단계의 최대치를 각각 소형 1000·2000·3000, 중형 2000·3500·5000,
    /// 대형 3000·5000·7000으로 두고 그 사이 단계를 선형 보간한다.
    /// </summary>
    public static int Max(CastleSize castle, BalanceConfig balance, int wallLevel)
    {
        var (initial, middle, full) = castle switch
        {
            CastleSize.Small => (1000, 2000, balance.WallMaxSmall),
            CastleSize.Medium => (2000, 3500, balance.WallMaxMedium),
            CastleSize.Large => (3000, 5000, balance.WallMaxLarge),
            _ => (1000, 2000, balance.WallMaxSmall),
        };
        var level = System.Math.Clamp(wallLevel, 0, WallResearchMaxLevel);
        return level <= 2
            ? initial + (middle - initial) * level / 2
            : middle + (full - middle) * (level - 2) / 2;
    }
}
