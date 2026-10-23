namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public class AiWarPlannerIntegrationTests
{
    private static AiOffensePlanner Planner(HexMap map)
    {
        var directory = TestData.DataDirectory();
        var troops = new TroopTypeLoader().LoadFromDirectory(directory);
        return new AiOffensePlanner(new DeployService(new CommandBalance(), troops,
            new ActiveSkillLoader().LoadFromDirectory(directory),
            new PassiveSkillLoader().LoadFromDirectory(directory)), new AiConfig(), map: map);
    }

    [Fact]
    public void 빈_성은_정찰되었고_육로가_있으면_실제_점령부대를_출전시킨다()
    {
        var origin = new City(new CityId(1), "아군", new HexCoord(0, 0), new FactionId(1),
            10_000, Wall: 3000);
        var target = new City(new CityId(2), "빈성", new HexCoord(6, 0), new FactionId(2),
            1000, Wall: 0);
        var generals = Enumerable.Range(1, 3).Select(id => new General(new GeneralId(id),
            $"장수{id}", new Dictionary<TroopClass, AptitudeGrade>
            { [TroopClass.Infantry] = AptitudeGrade.A }, 80, 80, 80)).ToList();
        var state = new GameState(1, 190, [], [origin, target], generals,
            Postings: generals.Select(general => new GeneralPosting(general.Id,
                origin.Owner, origin.Id)).ToList(),
            GarrisonForces: [new GarrisonForce(origin.Id, "swordsman", 10_000, 60)],
            ScoutedCities: [new CityIntel(origin.Owner, target.Id)]);

        var after = Planner(new HexMap(-3, 12, -4, 4)).Plan(state, origin.Owner);

        var unit = Assert.Single(after.Armies);
        Assert.Equal(UnitMode.Attack, unit.Field.Mode);
        Assert.Equal(target.Position, unit.Field.Target);
        Assert.Equal(1000, unit.Pool.Active);
    }

    [Fact]
    public void 가까운_수비_거점에는_두_장수로_공성_전투부대를_편성한다()
    {
        var origin = new City(new CityId(1), "아군", new HexCoord(0, 0), new FactionId(1),
            50_000, Wall: 3000);
        var target = new City(new CityId(2), "적군", new HexCoord(6, 0), new FactionId(2),
            20_000, Wall: 3000);
        var generals = Enumerable.Range(1, 2).Select(id => new General(new GeneralId(id),
            $"장수{id}", new Dictionary<TroopClass, AptitudeGrade>
            { [TroopClass.Infantry] = AptitudeGrade.A,
              [TroopClass.Siege] = AptitudeGrade.A }, 80, 80, 80)).ToList();
        var state = new GameState(1, 190, [], [origin, target], generals,
            Postings: generals.Select(general => new GeneralPosting(general.Id,
                origin.Owner, origin.Id)).ToList(),
            GarrisonForces:
            [
                new GarrisonForce(origin.Id, "catapult", 10_000, 60),
                new GarrisonForce(origin.Id, "swordsman", 20_000, 60),
                new GarrisonForce(target.Id, "swordsman", 5_000, 60),
            ],
            ScoutedCities: [new CityIntel(origin.Owner, target.Id)]);

        var after = Planner(new HexMap(-3, 12, -4, 4)).Plan(state, origin.Owner);

        Assert.Equal(2, after.Armies.Count(unit => unit.Field.Owner == origin.Owner
            && !unit.IsSupply));
        Assert.DoesNotContain(after.Armies, unit => unit.IsSupply);
        Assert.All(after.Armies, unit => Assert.Equal(target.Position, unit.Field.Target));
    }
}
