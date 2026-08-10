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

    /// <summary>
    /// 특수 유닛 야전 교전용. 기반 템플릿에 판정 전환(df·건물 공격 override)과 자기 조건부 가산
    /// (철기 전체·남만 건물·무당 이동불가)을 얹는다. 상대 분류에 걸리는 가산(극병 등)은
    /// <see cref="MatchupAtkBonus"/>로 따로 더한다.
    /// </summary>
    public static CombatStats BuildFieldSpecial(
        SpecialUnit special,
        TroopTemplate baseTemplate,
        AptitudeGrade grade,
        int researchLevel,
        TerrainType terrain,
        int troops,
        bool targetIsBuilding = false,
        bool attackFromImpassable = false,
        int passiveAtkBonus = 100,
        int passiveDfBonus = 100)
    {
        var research = ResearchCurve.Bonus(researchLevel);
        var (terrainAtk, terrainDf) = TerrainCombatBonus.For(baseTemplate.Class, terrain);

        var atkBase = targetIsBuilding
            ? special.BuildingAtkOverride ?? baseTemplate.AtkBuilding
            : baseTemplate.AtkUnit;
        var dfBase = special.DfOverride ?? baseTemplate.Df;

        var selfBonus = special.AtkBonusAll
            + (targetIsBuilding ? special.AtkBonusBuilding : 0)
            + (attackFromImpassable ? special.AtkBonusImpassable : 0);

        return new CombatStats(
            troops,
            atkBase + research + terrainAtk,
            dfBase + research + terrainDf,
            grade.Percent(),
            passiveAtkBonus + selfBonus,
            passiveDfBonus);
    }

    /// <summary>
    /// 매치업 조건부 공격 가산 퍼센트. 공격자 자신의 vs-분류 가산(극병→기병)과, 방어자의 취약
    /// (극병이 궁병에게 받는 +10%)을 공격자 쪽으로 합산한다. <see cref="CombatStats.AtkBonusPercent"/>에 더해 쓴다.
    /// </summary>
    public static int MatchupAtkBonus(
        SpecialUnit? attackerSpecial,
        TroopClass attackerClass,
        SpecialUnit? defenderSpecial,
        TroopClass defenderClass)
    {
        var extra = 0;
        if (attackerSpecial is not null && attackerSpecial.AtkBonusVsClass.TryGetValue(defenderClass, out var a))
        {
            extra += a;
        }
        if (defenderSpecial is not null && defenderSpecial.AttackerBonusFromClass.TryGetValue(attackerClass, out var d))
        {
            extra += d;
        }

        return extra;
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
