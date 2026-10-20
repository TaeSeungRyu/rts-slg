namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class AiRearSupportPlannerTests
{
    private static readonly IReadOnlyList<TroopTemplate> Troops =
        new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());
    private static AiRearSupportPlanner Planner() => new(new DeployService(new CommandBalance(),
        Troops, new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
        new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory())));

    private static GameState Scenario(int rearTroops = 50_000, int rearFood = 10_000)
    {
        var front = new City(new CityId(1), "전선", new HexCoord(0, 0), new FactionId(1), 1000);
        var rear = new City(new CityId(2), "후방", new HexCoord(-8, 0), new FactionId(1),
            rearFood, Gold: 2000);
        var enemy = new City(new CityId(3), "적성", new HexCoord(5, 0), new FactionId(2), 1000);
        var officer = new General(new GeneralId(1), "수송", new Dictionary<TroopClass, AptitudeGrade>(),
            60, 60, 60);
        return new GameState(1, 190, [], [front, rear, enemy], [officer],
            Postings: [new GeneralPosting(officer.Id, new FactionId(1), rear.Id)],
            ScoutedCities: [new CityIntel(new FactionId(1), enemy.Id)],
            GarrisonForces:
            [
                new(front.Id, "swordsman", 5000, 60),
                new(rear.Id, "swordsman", rearTroops, 60),
                new(enemy.Id, "swordsman", 30_000, 60),
            ]);
    }

    [Fact]
    public void 후방_잉여병력은_전선으로_수송하고_방어예비를_남긴다()
    {
        var planner = Planner();
        var after = planner.Plan(Scenario(), new FactionId(1));
        var transport = Assert.Single(after.Armies);
        Assert.True(transport.IsTransport);
        Assert.Equal(new CityId(2), transport.OriginCity);
        Assert.Equal(new CityId(1), transport.Field.ReturnCity);
        Assert.True(after.Garrisons.Single(g => g.City == new CityId(2)).Troops >= 15_000);
    }

    [Theory]
    [InlineData(10_000, 10_000)]
    [InlineData(50_000, 0)]
    public void 예비병력이나_군량이_부족하면_수송하지_않는다(int troops, int food)
    {
        var planner = Planner();
        Assert.Empty(planner.Plan(Scenario(troops, food), new FactionId(1)).Armies);
    }

    [Fact]
    public void 이미_같은_전선에_수송중이면_추가_수송하지_않는다()
    {
        var planner = Planner();
        var first = planner.Plan(Scenario(), new FactionId(1));
        var second = planner.Plan(first, new FactionId(1));
        Assert.Single(second.Armies);
    }

    [Fact]
    public void 후방_수송은_전선의_부족한_금과_군량도_싣되_후방_예비를_남긴다()
    {
        var state = Scenario() with
        {
            Cities = Scenario().Cities.Select(c => c.Id == new CityId(1)
                ? c with { Provisions = 0, Gold = 0 } : c).ToList(),
        };

        var after = Planner().Plan(state, new FactionId(1));

        var transport = Assert.Single(after.Armies);
        Assert.Equal(1000, transport.CargoGold);
        Assert.True(transport.Provisions > 100);
        var rear = after.Cities.Single(c => c.Id == new CityId(2));
        Assert.True(rear.Gold >= 1000);
        Assert.True(rear.Provisions >= 1500);
    }

    [Fact]
    public void 수송중_도착성이_함락되면_적성에_들어가지_않고_원점으로_복귀한다()
    {
        var launched = Planner().Plan(Scenario(), new FactionId(1));
        var captured = launched with
        {
            Cities = launched.Cities.Select(city => city.Id == new CityId(1)
                ? city with { Owner = new FactionId(2) } : city).ToList(),
            FieldArmies = launched.Armies.Select(unit => unit with
            {
                AwaitingEgress = false,
                Field = unit.Field with { Position = new HexCoord(-7, 0) },
            }).ToList(),
        };

        var after = Planner().Plan(captured, new FactionId(1));

        var transport = Assert.Single(after.Armies);
        Assert.Equal(UnitMode.March, transport.Field.Mode);
        Assert.Equal(new CityId(2), transport.Field.ReturnCity);
        Assert.Equal(new HexCoord(-8, 0), transport.Field.Target);
    }
}
