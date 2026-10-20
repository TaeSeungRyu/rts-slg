namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Data;
using Xunit;

public class EconomyBalanceTests
{
    [Fact]
    public void 실게임_성_기본수입과_내정담당_생산은_하향된_값을_사용한다()
    {
        var directory = TestData.DataDirectory();
        var balance = new ScenarioLoader().LoadFromDirectory(directory).Balance;
        var commands = new CommandBalanceLoader().LoadFromDirectory(directory);

        Assert.Equal((150, 350, 600),
            (balance.GoldBaseSmall, balance.GoldBaseMedium, balance.GoldBaseLarge));
        Assert.Equal((750, 1500, 3000),
            (balance.ProvisionsBaseSmall, balance.ProvisionsBaseMedium, balance.ProvisionsBaseLarge));
        Assert.Equal((150, 75, 25),
            (balance.PaddyProvisions, balance.FarmProvisions, balance.VillageGold));
        Assert.Equal((50, 1, 150, 2),
            (commands.AutoDomesticGoldBase, commands.AutoDomesticGoldPoliticsMultiplier,
                commands.AutoDomesticProvisionsBase, commands.AutoDomesticProvisionsPoliticsMultiplier));
    }
}
