namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using Xunit;

public class AiCityGrowthPolicyTests
{
    [Theory]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(10, 2)]
    [InlineData(11, 3)]
    [InlineData(20, 3)]
    public void 주둔_장수_수에_따라_자동_병력생산_강도가_달라진다(int generals, int rate)
    {
        Assert.Equal(rate, AiCityGrowthPolicy.RecruitmentRate(generals));
        Assert.Equal(rate, AiCityGrowthPolicy.MaintenanceSlots(generals));
    }
}
