namespace SanguoSLG.Core.Tests.Simulation;

using System.Collections.Generic;
using System.Linq;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

/// <summary>장수 → 부대 조립(spec-general·design-skill "부대의 장수 2명") 검증.</summary>
public class UnitAssemblerTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> T =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static readonly IReadOnlyDictionary<string, ActiveSkill> A =
        new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static readonly IReadOnlyDictionary<string, PassiveSkill> P =
        new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Code);

    private static readonly IReadOnlyDictionary<string, General> G =
        new GeneralLoader().LoadFromDirectory(TestData.DataDirectory()).ToDictionary(x => x.Name);

    private static readonly CombatContext Melee = new(MeleeEngagement: true, IncomingMelee: true, InField: true);

    private static CombatUnit Assemble(string vanguard, string? adjutant, string troop, int troops = 10000)
        => UnitAssembler.Assemble(new UnitId(1), new FactionId(1), new HexCoord(0, 0), UnitMode.Attack,
            new HexCoord(5, 0), 0, G[vanguard], adjutant is null ? null : G[adjutant], T[troop], troops, A, P, Melee);

    [Fact]
    public void 조립_적성은_선봉의_병종별통솔을_쓴다()
    {
        // 여포: 기병 SS(130%), 공성 D
        var cavalry = Assemble("여포", null, "cavalry");
        Assert.Equal(AptitudeGrade.SS.Percent(), cavalry.Stats.AptitudePercent);

        var siege = Assemble("여포", null, "catapult");
        Assert.Equal(AptitudeGrade.D.Percent(), siege.Stats.AptitudePercent);
    }

    [Fact]
    public void 조립_부관의_적성은_반영되지않는다()
    {
        // 선봉 유비(기병 A) + 부관 여포(기병 SS) → A만 적용
        var unit = Assemble("유비", "여포", "cavalry");
        Assert.Equal(AptitudeGrade.A.Percent(), unit.Stats.AptitudePercent);
    }

    [Fact]
    public void 조립_액티브는_선봉부관_각자의것을_슬롯에넣는다()
    {
        var unit = Assemble("관우", "제갈량", "swordsman");
        Assert.Equal("일섬", unit.State.VanguardActive!.Name);   // 관우 flash
        Assert.Equal("철벽", unit.State.AdjutantActive!.Name);   // 제갈량 iron_wall
    }

    [Fact]
    public void 조립_패시브는_두장수_모두_합산된다()
    {
        // 관우(맹공 T3: 공+12) 단독 vs + 부관 조조(맹공 T3 + 파죽지세 — 파죽은 조건부라 평시 0)
        var solo = Assemble("관우", null, "swordsman");
        var pair = Assemble("관우", "조조", "swordsman");
        Assert.True(pair.Stats.AtkBonusPercent > solo.Stats.AtkBonusPercent);
    }

    [Fact]
    public void 조립_무력지력은_선봉기준이다()
    {
        var unit = Assemble("유비", "여포", "swordsman");
        Assert.Equal(73, unit.Might);      // 유비 무력
        Assert.Equal(74, unit.Intellect);  // 유비 지력
    }

    [Fact]
    public void 조립_해상병종은_대하_통행영역이다()
    {
        var unit = Assemble("주유", null, "turtleship");
        Assert.Equal(MovementDomain.DeepWater, unit.Field.Domain);
        Assert.Equal(MovementDomain.Land, Assemble("주유", null, "swordsman").Field.Domain);
    }
}
