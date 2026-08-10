namespace SanguoSLG.Core.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

/// <summary>
/// 병종 템플릿 + 적성 + 연구 + 지형을 조립해 전투 입력(<see cref="CombatStats"/>·<see cref="SiegeAttacker"/>)을
/// 만든다(design-combat.md "전투값 산출 순서" ①~③). 스킬 가산 버킷(패시브)은 퍼센트로 주입받는다.
/// 특수유닛 판정 전환(궁기병 건물→궁병 등)은 후속 증분에서 얹는다.
/// </summary>
public static class CombatStatsBuilder
{
    /// <summary>야전 교전용. 대상이 건물이면 건물dmg를 쓴다(① 판정).</summary>
    public static CombatStats BuildField(
        TroopTemplate template,
        AptitudeGrade grade,
        int researchLevel,
        TerrainType terrain,
        int troops,
        bool targetIsBuilding = false,
        int atkBonusPercent = 100,
        int dfBonusPercent = 100)
    {
        var research = ResearchCurve.Bonus(researchLevel);
        var (terrainAtk, terrainDf) = TerrainCombatBonus.For(template.Class, terrain);
        var atkBase = targetIsBuilding ? template.AtkBuilding : template.AtkUnit;

        return new CombatStats(
            troops,
            atkBase + research + terrainAtk,
            template.Df + research + terrainDf,
            grade.Percent(),
            atkBonusPercent,
            dfBonusPercent);
    }

    /// <summary>성 공격용. 건물dmg·유닛dmg·df 모두에 연구·지형(flat)을 반영한다.</summary>
    public static SiegeAttacker BuildSiegeAttacker(
        TroopTemplate template,
        AptitudeGrade grade,
        int researchLevel,
        TerrainType terrain,
        int troops,
        bool inCounterRange = true,
        int atkBonusPercent = 100,
        int dfBonusPercent = 100)
    {
        var research = ResearchCurve.Bonus(researchLevel);
        var (terrainAtk, terrainDf) = TerrainCombatBonus.For(template.Class, terrain);

        return new SiegeAttacker(
            troops,
            template.AtkBuilding + research + terrainAtk,
            template.AtkUnit + research + terrainAtk,
            template.Df + research + terrainDf,
            grade.Percent(),
            atkBonusPercent,
            dfBonusPercent,
            inCounterRange);
    }
}
