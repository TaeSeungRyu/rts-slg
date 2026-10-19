namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public class AiOffensePatternTests
{
    private static readonly IReadOnlyDictionary<string, TroopTemplate> Troops = new Dictionary<string, TroopTemplate>
    {
        ["infantry"] = new("infantry", "보병", TroopClass.Infantry, 10, 10, 10),
        ["archer"] = new("archer", "궁병", TroopClass.Archer, 10, 10, 10),
        ["cavalry"] = new("cavalry", "기병", TroopClass.Cavalry, 10, 10, 10),
        ["siege"] = new("siege", "공성", TroopClass.Siege, 10, 10, 10),
    };

    private static AiOffensePatternChoice Choose(int enemy, int infantry, int archer,
        int cavalry, int siege, int distance = 10, int freeGenerals = 4)
    {
        var origin = new City(new CityId(1), "출발", default, new FactionId(1), 1000);
        var target = new City(new CityId(2), "목표", new HexCoord(distance, 0), new FactionId(2), 1000);
        var garrisons = new[]
        {
            new GarrisonForce(origin.Id, "infantry", infantry, 50),
            new GarrisonForce(origin.Id, "archer", archer, 50),
            new GarrisonForce(origin.Id, "cavalry", cavalry, 50),
            new GarrisonForce(origin.Id, "siege", siege, 50),
            new GarrisonForce(target.Id, "infantry", enemy, 50),
        };
        var state = new GameState(1, 190, [], [origin, target], [], GarrisonForces: garrisons);
        return AiOffensePatternService.Choose(state, origin, target, Troops, freeGenerals);
    }

    [Fact]
    public void 대규모_성에는_집단군_패턴을_먼저_검토한다()
    {
        var choice = Choose(50_000, 50_000, 20_000, 10_000, 20_000);
        Assert.Equal(AiOffensePattern.ArmyGroupSiege, choice.Pattern);
        Assert.True(choice.RequiresSupply);
    }

    [Fact]
    public void 장수가_부족하면_집단군을_선택하지_않는다()
    {
        var choice = Choose(50_000, 50_000, 20_000, 10_000, 20_000, freeGenerals: 3);
        Assert.Equal(AiOffensePattern.CavalrySiege, choice.Pattern);
    }

    [Fact]
    public void 단거리_기병_패턴은_공성무기와_기병을_요구한다()
    {
        Assert.Equal(AiOffensePattern.CavalrySiege,
            Choose(30_000, 30_000, 0, 20_000, 10_000).Pattern);
        Assert.Equal(AiOffensePattern.None,
            Choose(30_000, 30_000, 0, 20_000, 0).Pattern);
    }

    [Fact]
    public void 장거리에는_보급이_필수인_정규_공성_후보를_고른다()
    {
        var choice = Choose(30_000, 30_000, 0, 20_000, 10_000, distance: 15);
        Assert.Equal(AiOffensePattern.RegularSiege, choice.Pattern);
        Assert.True(choice.RequiresSupply);
    }

    [Fact]
    public void 병력이_부족하면_패턴을_고르지_않는다()
    {
        Assert.Equal(AiOffensePattern.None,
            Choose(50_000, 20_000, 0, 0, 5_000).Pattern);
    }
}
