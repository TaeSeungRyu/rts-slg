namespace SanguoSLG.Core.Tests.Domain;

using System.Collections.Generic;
using SanguoSLG.Core.Domain;
using Xunit;

public sealed class GeneralGrowthTests
{
    [Fact]
    public void 장수_경험치는_공식에_따라_레벨업한다()
    {
        var general = new General(new GeneralId(1), "성장장수", new Dictionary<TroopClass, AptitudeGrade>(),
            Might: 70, Intellect: 70, Politics: 70, Level: 1, Experience: 120);

        var grown = GeneralGrowth.AddGeneralExperience(general, 10, out var leveledUp);

        Assert.True(leveledUp);
        Assert.Equal(2, grown.Level);
        Assert.Equal(5, grown.Experience);
    }

    [Fact]
    public void 레벨_전투보정은_1레벨_0에서_50레벨_4점9까지_오른다()
    {
        Assert.Equal(0.0d, GeneralGrowth.LevelCombatBonus(1));
        Assert.Equal(4.9d, GeneralGrowth.LevelCombatBonus(50));
    }

    [Fact]
    public void 패시브_경험치는_티어를_성장시킨다()
    {
        var skill = new GeneralSkill("drilled", 1, Experience: 90);

        var grown = GeneralGrowth.AddPassiveExperience(skill, 30, out var tierUp);

        Assert.True(tierUp);
        Assert.Equal(2, grown.Tier);
        Assert.Equal(20, grown.Experience);
    }
}
