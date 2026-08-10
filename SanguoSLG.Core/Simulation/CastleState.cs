namespace SanguoSLG.Core.Simulation;

/// <summary>
/// 성(항구)의 전투 상태(design-combat.md "성벽 — 보호막"). 성벽 값이 방패처럼 동작한다.
/// 성벽 df 12는 성벽의 방어값이고, 붕괴 시 6으로 격하된다.
/// </summary>
/// <param name="WallCurrent">현재 성벽 값(0이면 붕괴).</param>
/// <param name="Troops">수비 병력.</param>
/// <param name="UnitDmg">성의 유닛dmg(반격에 쓰임, 기본 10).</param>
/// <param name="WallDf">성벽이 서 있을 때 df(기본 12).</param>
/// <param name="CollapsedDf">성벽 붕괴 후 df(기본 6, 공성탑 수준으로 격하).</param>
/// <param name="AptitudePercent">수성 장수 적성.</param>
public sealed record CastleState(
    int WallCurrent,
    int Troops,
    int UnitDmg = 10,
    int WallDf = 12,
    int CollapsedDf = 6,
    int AptitudePercent = 100);
