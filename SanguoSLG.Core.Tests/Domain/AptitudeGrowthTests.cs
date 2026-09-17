namespace SanguoSLG.Core.Tests.Domain;

using SanguoSLG.Core.Domain;

public class AptitudeGrowthTests
{
    private static General General(AptitudeGrade grade) => new(
        new GeneralId(1), "숙련 장수",
        new Dictionary<TroopClass, AptitudeGrade> { [TroopClass.Infantry] = grade },
        70, 70, 70);

    [Fact]
    public void 숙련경험치가_기준에_도달하면_적성이_한단계_상승한다()
    {
        var grown = AptitudeGrowth.AddExperience(General(AptitudeGrade.D), TroopClass.Infantry,
            AptitudeGrowth.RequiredExperience, out var gradeUp);

        Assert.True(gradeUp);
        Assert.Equal(AptitudeGrade.C, grown.AptitudeFor(TroopClass.Infantry));
        Assert.Equal(0, grown.AptitudeExperienceFor(TroopClass.Infantry));
    }

    [Theory]
    [InlineData(AptitudeGrade.F, AptitudeGrade.D)]
    [InlineData(AptitudeGrade.D, AptitudeGrade.C)]
    [InlineData(AptitudeGrade.C, AptitudeGrade.B)]
    [InlineData(AptitudeGrade.B, AptitudeGrade.A)]
    public void 일반성장은_최초등급에서_한단계까지만_가능하다(AptitudeGrade start, AptitudeGrade cap)
    {
        var once = AptitudeGrowth.AddExperience(General(start), TroopClass.Infantry,
            AptitudeGrowth.RequiredExperience, out _);
        var twice = AptitudeGrowth.AddExperience(once, TroopClass.Infantry,
            AptitudeGrowth.RequiredExperience * 2, out var secondUp);

        Assert.False(secondUp);
        Assert.Equal(cap, twice.AptitudeFor(TroopClass.Infantry));
    }

    [Theory]
    [InlineData(AptitudeGrade.A)]
    [InlineData(AptitudeGrade.APlus)]
    [InlineData(AptitudeGrade.S)]
    [InlineData(AptitudeGrade.SS)]
    [InlineData(AptitudeGrade.SSS)]
    public void A이상은_일반전투만으로_상승하지_않는다(AptitudeGrade grade)
    {
        var grown = AptitudeGrowth.AddExperience(General(grade), TroopClass.Infantry,
            AptitudeGrowth.RequiredExperience * 10, out var gradeUp);

        Assert.False(gradeUp);
        Assert.Equal(grade, grown.AptitudeFor(TroopClass.Infantry));
    }
}
