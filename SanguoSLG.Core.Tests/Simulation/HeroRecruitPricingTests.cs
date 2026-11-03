namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;

public sealed class HeroRecruitPricingTests
{
    [Theory]
    [InlineData(AptitudeGrade.SSS, 3000, 6000)]
    [InlineData(AptitudeGrade.SS, 1500, 4000)]
    [InlineData(AptitudeGrade.S, 1000, 2000)]
    [InlineData(AptitudeGrade.APlus, 500, 1250)]
    [InlineData(AptitudeGrade.A, 500, 1250)]
    [InlineData(AptitudeGrade.B, 250, 750)]
    [InlineData(AptitudeGrade.C, 100, 500)]
    [InlineData(AptitudeGrade.D, 50, 500)]
    [InlineData(AptitudeGrade.F, 50, 500)]
    public void 가장_높은_적성에_따라_기본_비용이_결정된다(AptitudeGrade grade, int points, int gold)
        => Assert.Equal((points, gold), HeroRecruitPricing.DefaultFor([AptitudeGrade.F, grade]));
}
