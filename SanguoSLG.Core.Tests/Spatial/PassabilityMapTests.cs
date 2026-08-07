namespace SanguoSLG.Core.Tests.Spatial;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Spatial;

public class PassabilityMapTests
{
    private static PassabilityMap Build(
        IEnumerable<MapFeature>? features = null, IEnumerable<City>? cities = null)
    {
        var map = new HexMap(0, 9, 0, 9);
        return new PassabilityMap(map, features ?? [], cities ?? []);
    }

    private static City CityAt(HexCoord position, CastleSize castle) =>
        new(new CityId(1), "낙양", position, new FactionId(1), 1000, castle);

    [Theory]
    [InlineData(MovementDomain.Land)]
    [InlineData(MovementDomain.LandMountain)]
    [InlineData(MovementDomain.DeepWater)]
    public void CanEnter_성발자국이면_모든병종거짓(MovementDomain domain)
    {
        var passability = Build(cities: new[] { CityAt(new HexCoord(3, 3), CastleSize.Medium) });

        foreach (var tile in CastleFootprint.TilesFor(CityAt(new HexCoord(3, 3), CastleSize.Medium)))
        {
            Assert.False(passability.CanEnter(domain, tile));
        }
    }

    [Theory]
    [InlineData(MovementDomain.Land)]
    [InlineData(MovementDomain.LandMountain)]
    [InlineData(MovementDomain.DeepWater)]
    public void CanEnter_중형항구지물이면_모든병종거짓(MovementDomain domain)
    {
        var passability = Build(features: new[]
        {
            new MapFeature(FeatureType.PortMedium, new HexCoord(2, 2)),
        });

        Assert.False(passability.CanEnter(domain, new HexCoord(2, 2)));
        Assert.False(passability.CanEnter(domain, new HexCoord(3, 2)));
    }

    [Fact]
    public void CanEnter_산지물은_산악통행병종만참()
    {
        var passability = Build(features: new[]
        {
            new MapFeature(FeatureType.MountainLarge, new HexCoord(4, 4)),
        });

        Assert.False(passability.CanEnter(MovementDomain.Land, new HexCoord(4, 4)));
        Assert.False(passability.CanEnter(MovementDomain.DeepWater, new HexCoord(4, 4)));
        Assert.True(passability.CanEnter(MovementDomain.LandMountain, new HexCoord(4, 4)));
    }

    [Fact]
    public void CanEnter_맵밖이면_거짓()
    {
        var passability = Build();

        Assert.False(passability.CanEnter(MovementDomain.Land, new HexCoord(-1, 0)));
    }

    [Fact]
    public void FindPath_성발자국을_우회한다()
    {
        var city = CityAt(new HexCoord(2, 2), CastleSize.Small);
        var map = new HexMap(0, 9, 0, 9);
        var passability = new PassabilityMap(map, [], new[] { city });
        var pathfinder = new HexPathfinder(c => passability.CanEnter(MovementDomain.Land, c));

        var path = pathfinder.FindPath(new HexCoord(1, 2), new HexCoord(3, 2));

        Assert.NotEmpty(path);
        Assert.DoesNotContain(new HexCoord(2, 2), path);
    }
}
