namespace SanguoSLG.Core.Tests.Domain;

using SanguoSLG.Core.Domain;
using Xunit;

public sealed class AdministrationGrowthTests
{
    private static General General(int level = 1, int experience = 0)
        => new(new GeneralId(1), "내정장", new Dictionary<TroopClass, AptitudeGrade>(),
            70, 80, 75, AdminLevel: level, AdminExperience: experience);

    [Fact]
    public void 내정경험치는_전투레벨과_분리되어_내정레벨을_올린다()
    {
        var original = General(experience: 120) with { Level = 7, Experience = 33 };

        var grown = AdministrationGrowth.AddExperience(original, 10, out var leveledUp);

        Assert.True(leveledUp);
        Assert.Equal(2, grown.AdminLevel);
        Assert.Equal(5, grown.AdminExperience);
        Assert.Equal(7, grown.Level);
        Assert.Equal(33, grown.Experience);
    }

    [Fact]
    public void 내정레벨은_오십을_넘지_않고_남은경험치를_비운다()
    {
        var grown = AdministrationGrowth.AddExperience(General(49), 10_000, out var leveledUp);

        Assert.True(leveledUp);
        Assert.Equal(50, grown.AdminLevel);
        Assert.Equal(0, grown.AdminExperience);
    }

    [Fact]
    public void 내정레벨은_지력과_정치에_레벨당_영점이씩_보정한다()
    {
        Assert.Equal(80d, AdministrationGrowth.EffectiveIntellect(General(1)));
        Assert.Equal(89.8d, AdministrationGrowth.EffectiveIntellect(General(50)), 6);
        Assert.Equal(84.8d, AdministrationGrowth.EffectivePolitics(General(50)), 6);
    }
}
