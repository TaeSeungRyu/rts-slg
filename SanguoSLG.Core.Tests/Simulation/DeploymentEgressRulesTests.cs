namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class DeploymentEgressRulesTests
{
    private static City CityOf(CastleSize size) =>
        new(new CityId(1), "test", new HexCoord(10, 10), new FactionId(1), 1000, size);

    [Theory]
    [InlineData(CastleSize.Small)]
    [InlineData(CastleSize.Medium)]
    [InlineData(CastleSize.Large)]
    public void 모든_외곽타일은_겹침없이_여섯_출구군에_속한다(CastleSize size)
    {
        var city = CityOf(size);
        var exterior = DeploymentEgressRules.ExteriorTiles(city).ToHashSet();
        var grouped = DeploymentEgressRules.Directions
            .SelectMany(direction => DeploymentEgressRules.ExitGroup(city, direction))
            .ToArray();

        Assert.All(DeploymentEgressRules.Directions,
            direction => Assert.NotEmpty(DeploymentEgressRules.ExitGroup(city, direction)));
        Assert.Equal(grouped.Length, grouped.Distinct().Count());
        Assert.True(exterior.SetEquals(grouped));
    }

    [Theory]
    [InlineData(CastleSize.Small)]
    [InlineData(CastleSize.Medium)]
    [InlineData(CastleSize.Large)]
    public void 여섯_방향의_먼_목표는_같은_방향을_추천한다(CastleSize size)
    {
        var city = CityOf(size);
        foreach (var direction in DeploymentEgressRules.Directions)
        {
            var offset = DeploymentEgressRules.Offset(direction);
            var target = new HexCoord(city.Position.Q + offset.Q * 20, city.Position.R + offset.R * 20);
            Assert.Equal(direction, DeploymentEgressRules.Recommend(city, target));

            var exit = DeploymentEgressRules.RepresentativeExit(city, direction, target);
            Assert.NotNull(exit);
            Assert.Contains(exit!.Value, DeploymentEgressRules.ExitGroup(city, direction));
        }
    }

    [Fact]
    public void 사용자가_선택한_방향은_목표추천과_별개로_대표출구를_계산한다()
    {
        var city = CityOf(CastleSize.Large);
        var target = new HexCoord(30, 10);
        Assert.Equal(DeploymentDirection.East, DeploymentEgressRules.Recommend(city, target));

        var manual = DeploymentEgressRules.RepresentativeExit(city, DeploymentDirection.West, target);
        Assert.NotNull(manual);
        Assert.Contains(manual!.Value, DeploymentEgressRules.ExitGroup(city, DeploymentDirection.West));
    }
}
