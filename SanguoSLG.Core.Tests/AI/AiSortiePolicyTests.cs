namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;
using Xunit;

public class AiSortiePolicyTests
{
    [Theory]
    [InlineData(300, 70, true)]
    [InlineData(299, 70, false)]
    [InlineData(300, 69, false)]
    [InlineData(1200, 100, true)]
    public void 성벽과_민심_두_조건을_동시에_만족해야_전투부대를_출전시킨다(
        int wall, int security, bool expected)
    {
        var city = new City(new CityId(1), "소성", new HexCoord(0, 0),
            new FactionId(2), 0, Wall: wall, Security: security);

        Assert.Equal(expected, AiSortiePolicy.AllowsCombatSortie(city));
    }
}
