namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 한 부대가 이 교전에서 처한 상황(패시브 조건 판정용). 상위 계층이 이동·지형·다대일 상태에서
/// 채운다. 전부 결정론적 상태값이다.
/// </summary>
/// <param name="TargetIsBuilding">대상이 성·항구인가.</param>
/// <param name="OwnTerrainRough">내가 숲·산 지형인가.</param>
/// <param name="OwnTerrainPlainsDesert">내가 평야·사막 지형인가.</param>
/// <param name="IsMajoritySide">다대일에서 내가 다수(포위) 측인가.</param>
/// <param name="Pursuing">추격 중인가.</param>
/// <param name="EnemyMarching">상대가 행군모드인가.</param>
/// <param name="MeleeEngagement">사거리 1(인접)로 내가 공격하는가.</param>
/// <param name="IncomingMelee">사거리 1 공격을 받는가.</param>
/// <param name="IncomingRanged">사거리 2 공격을 받는가.</param>
/// <param name="HpRatioPercent">현재 병력 / 최대 병력(퍼센트, 100=만전).</param>
/// <param name="InCastle">성·항구에 주둔 중인가.</param>
/// <param name="IsSurrounded">다대일로 포위당했는가.</param>
/// <param name="InField">야전(성·항구 밖)인가.</param>
public sealed record CombatContext(
    bool TargetIsBuilding = false,
    bool OwnTerrainRough = false,
    bool OwnTerrainPlainsDesert = false,
    bool IsMajoritySide = false,
    bool Pursuing = false,
    bool EnemyMarching = false,
    bool MeleeEngagement = false,
    bool IncomingMelee = false,
    bool IncomingRanged = false,
    int HpRatioPercent = 100,
    bool InCastle = false,
    bool IsSurrounded = false,
    bool InField = true);
