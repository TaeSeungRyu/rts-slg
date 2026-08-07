namespace SanguoSLG.Core.Tests.Spatial;

using SanguoSLG.Core.Spatial;

public class TerrainRulesTests
{
    [Theory]
    [InlineData(TerrainType.WaterShallow)]
    [InlineData(TerrainType.WaterDeep)]
    [InlineData(TerrainType.WaterRocks)]
    public void CanEnter_육지유닛이대하타일이면_거짓(TerrainType terrain)
    {
        Assert.False(TerrainRules.CanEnter(MovementDomain.Land, terrain));
    }

    [Fact]
    public void CanEnter_육지유닛이소하천이면_참()
    {
        Assert.True(TerrainRules.CanEnter(MovementDomain.Land, TerrainType.River));
    }

    [Fact]
    public void CanEnter_육지유닛이소형산이면_참()
    {
        Assert.True(TerrainRules.CanEnter(MovementDomain.Land, TerrainType.Mountain));
    }

    [Theory]
    [InlineData(TerrainType.WaterShallow)]
    [InlineData(TerrainType.WaterDeep)]
    public void CanEnter_배가대하물이면_참(TerrainType terrain)
    {
        Assert.True(TerrainRules.CanEnter(MovementDomain.DeepWater, terrain));
    }

    [Theory]
    [InlineData(TerrainType.Plains)]
    [InlineData(TerrainType.Mountain)]
    [InlineData(TerrainType.River)]
    [InlineData(TerrainType.WaterRocks)]
    public void CanEnter_배가대하아닌타일이면_거짓(TerrainType terrain)
    {
        Assert.False(TerrainRules.CanEnter(MovementDomain.DeepWater, terrain));
    }

    [Theory]
    [InlineData(TerrainType.Plains)]
    [InlineData(TerrainType.Forest)]
    [InlineData(TerrainType.Bridge)]
    [InlineData(TerrainType.Paddy)]
    [InlineData(TerrainType.Farm)]
    [InlineData(TerrainType.Workshop)]
    [InlineData(TerrainType.Village1)]
    [InlineData(TerrainType.Swamp)]
    public void CanEnter_육지유닛이농업생산마을타일이면_참(TerrainType terrain)
    {
        Assert.True(TerrainRules.CanEnter(MovementDomain.Land, terrain));
    }

    [Theory]
    [InlineData(MovementDomain.Land)]
    [InlineData(MovementDomain.LandMountain)]
    [InlineData(MovementDomain.DeepWater)]
    public void CanEnter_전병종공통_얼음지형이면_거짓(MovementDomain domain)
    {
        Assert.False(TerrainRules.CanEnter(domain, TerrainType.IceMountain));
        Assert.False(TerrainRules.CanEnter(domain, TerrainType.IceWallLarge));
        Assert.False(TerrainRules.CanEnter(domain, TerrainType.IceWallSmall));
    }

    [Theory]
    [InlineData(MovementDomain.Land)]
    [InlineData(MovementDomain.LandMountain)]
    [InlineData(MovementDomain.DeepWater)]
    public void CanEnter_전병종공통_소형항구타일이면_거짓(MovementDomain domain)
    {
        Assert.False(TerrainRules.CanEnter(domain, TerrainType.PortSmall));
    }
}
