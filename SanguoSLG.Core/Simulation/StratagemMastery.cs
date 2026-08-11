namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 계략 숙달 레벨(design-stratagem.md "숙달 레벨"). 성공 시 +1 포인트, 레벨업 5회(기본)·6→7=10·
/// 7→8=20·8→9=30·9→10=200. 장수 공유 속성이며 필요 단계 이상이어야 그 계략을 쓸 수 있다.
/// </summary>
public static class StratagemMastery
{
    // 인덱스 = 레벨(1~10)에 도달하는 누적 포인트. Lv1=0(시작).
    private static readonly int[] Cumulative = { 0, 0, 5, 10, 15, 20, 25, 35, 55, 85, 285 };

    public const int MaxLevel = 10;

    /// <summary>레벨 L에 도달하는 데 필요한 누적 성공 포인트.</summary>
    public static int PointsToReach(int level)
        => Cumulative[System.Math.Clamp(level, 1, MaxLevel)];

    /// <summary>누적 포인트에 해당하는 현재 숙달 레벨(1~10).</summary>
    public static int LevelFromPoints(int points)
    {
        var level = 1;
        for (var l = 2; l <= MaxLevel; l++)
        {
            if (points >= Cumulative[l])
            {
                level = l;
            }
        }

        return level;
    }

    /// <summary>다음 레벨로 오르는 데 필요한 성공 횟수(만렙이면 0).</summary>
    public static int NextLevelCost(int level)
        => level >= MaxLevel ? 0 : Cumulative[level + 1] - Cumulative[level];

    /// <summary>현재 숙달 레벨로 그 계략을 시전할 수 있는가(필요 단계 이상).</summary>
    public static bool IsUnlocked(int requiredLevel, int currentLevel) => currentLevel >= requiredLevel;
}
