namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class AiFieldResponsePlannerTests
{
    private static AiFieldResponsePlanner Planner() => new(
        new DeployService(new CommandBalance(),
            new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory()),
            new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
            new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory())),
        new AiConfig());

    private static GameState Scenario(HexCoord defenderPosition, HexCoord intruderPosition,
        int cavalry = 20000, bool supply = true)
    {
        var b = new City(new CityId(1), "B", new HexCoord(0, 0), new FactionId(1), 10000);
        var c = new City(new CityId(2), "C", defenderPosition, new FactionId(1), 10000);
        var hostile = new City(new CityId(3), "enemy", new HexCoord(10, 0), new FactionId(2), 10000);
        var generals = Enumerable.Range(1, 3).Select(i => new General(new GeneralId(i), $"g{i}",
            new Dictionary<TroopClass, AptitudeGrade> { [TroopClass.Cavalry] = AptitudeGrade.A },
            80, 60, 60)).ToList();
        var enemy = new CombatUnit(new FieldUnit(new UnitId(9), new FactionId(2),
                intruderPosition, 2, 3, 1, MovementDomain.Land, UnitMode.Attack,
                b.Position, 9), new CombatStats(5000, 10, 10), new TroopPool(5000, 0),
            UnitCombatState.Create(60), MaxTroops: 5000, IsSupply: supply);
        return new GameState(1, 190, [], [b, c, hostile], generals,
            Postings: generals.Select(g => new GeneralPosting(g.Id, new FactionId(1), c.Id)).ToList(),
            GarrisonForces: [new(c.Id, "cavalry", cavalry, 60)], FieldArmies: [enemy]);
    }

    [Fact]
    public void 다른_성의_기병이_위협받는_성_근처의_고립_보급부대를_요격한다()
    {
        var state = Scenario(new HexCoord(3, 0), new HexCoord(4, 0));

        var after = Planner().Plan(state, new FactionId(1));

        var response = Assert.Single(after.Armies, u => u.Field.Owner == new FactionId(1));
        Assert.Equal(new UnitId(9), response.Field.AssignedUnitTarget);
        Assert.Equal(new CityId(2), response.Field.ReturnCity);
        Assert.Equal("cavalry", response.TroopCode);
        Assert.True(after.Garrisons.Single().Troops >= 10000);
    }

    [Theory]
    [InlineData(7, 4)]
    [InlineData(3, 5)]
    public void 수비_반경_밖에서는_과잉_원정을_하지_않는다(int otherCityQ, int enemyQ)
    {
        var state = Scenario(new HexCoord(otherCityQ, 0), new HexCoord(enemyQ, 0));

        var after = Planner().Plan(state, new FactionId(1));

        Assert.Single(after.Armies);
    }

    [Fact]
    public void 예비병력이_부족하면_요격하지_않는다()
    {
        var state = Scenario(new HexCoord(3, 0), new HexCoord(4, 0), cavalry: 10000);

        var after = Planner().Plan(state, new FactionId(1));

        Assert.Single(after.Armies);
    }

    [Fact]
    public void 정찰망에서_발견한_고립_보급부대는_성_방어반경_밖이어도_기병이_견제한다()
    {
        var state = Scenario(new HexCoord(0, 0), new HexCoord(7, 0));
        state = state with
        {
            Cities = state.Cities.Select(c => c.Id == new CityId(1)
                ? c with { Position = new HexCoord(-10, 0) } : c).ToList(),
            FieldArmies = state.Armies.Append(new CombatUnit(
                new FieldUnit(new UnitId(8), new FactionId(1), new HexCoord(5, 0),
                    2, 3, 1, MovementDomain.Land, UnitMode.Standby, null, 8),
                new CombatStats(1000, 10, 10), new TroopPool(1000, 0),
                UnitCombatState.Create(60))).ToList(),
        };

        var after = Planner().Plan(state, new FactionId(1));

        Assert.Contains(after.Armies, u => u.Field.AssignedUnitTarget == new UnitId(9)
            && u.TroopCode == "cavalry");
    }
}
