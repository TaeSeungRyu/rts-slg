namespace SanguoSLG.Core.Tests.AI;

using SanguoSLG.Core.AI;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public sealed class AiSupportPlannerTests
{
    [Fact]
    public void 지원_표적이_사라지면_출발성으로_복귀하고_표적을_비운다()
    {
        var home = new City(new CityId(1), "출발", new HexCoord(0, 0),
            new FactionId(1), 10000);
        var defended = new City(new CityId(2), "수비", new HexCoord(5, 0),
            new FactionId(1), 10000);
        var unit = new CombatUnit(new FieldUnit(new UnitId(1), home.Owner,
                new HexCoord(3, 0), 2, 3, 1, MovementDomain.Land, UnitMode.Attack,
                defended.Position, 1, AssignedUnitTarget: new UnitId(9)),
            new CombatStats(7000, 10, 10), new TroopPool(7000, 0),
            UnitCombatState.Create(60), OriginCity: home.Id,
            AiSupportDefendedCity: defended.Id);
        var state = new GameState(1, 190, [], [home, defended], [], FieldArmies: [unit]);

        var recalled = Assert.Single(AiSupportPlanner.Recall(state, home.Owner).Armies);

        Assert.Equal(UnitMode.March, recalled.Field.Mode);
        Assert.Equal(home.Position, recalled.Field.Target);
        Assert.Null(recalled.Field.AssignedUnitTarget);
        Assert.Null(recalled.AiSupportDefendedCity);
    }

    [Fact]
    public void 공격받는_성에_가장_가까운_성에서_병력_사십퍼센트_이하로_한_부대만_지원한다()
    {
        var troops = new TroopTypeLoader().LoadFromDirectory(TestData.DataDirectory());
        var deployer = new DeployService(new CommandBalance(), troops,
            new ActiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()),
            new PassiveSkillLoader().LoadFromDirectory(TestData.DataDirectory()));
        var defended = new City(new CityId(1), "수비", new HexCoord(0, 0), new FactionId(1),
            10000, Wall: 600, Security: 80);
        var supporting = new City(new CityId(2), "지원", new HexCoord(5, 0),
            new FactionId(1), 10000, Wall: 600, Security: 80);
        var enemyCity = new City(new CityId(3), "적", new HexCoord(15, 0),
            new FactionId(2), 10000);
        var officer = new General(new GeneralId(1), "지원 장수",
            new Dictionary<TroopClass, AptitudeGrade> { [TroopClass.Cavalry] = AptitudeGrade.A },
            80, 60, 60);
        var enemy = new CombatUnit(new FieldUnit(new UnitId(9), new FactionId(2),
                new HexCoord(1, 0), 1, 3, 1, MovementDomain.Land, UnitMode.Attack,
                defended.Position, 9), new CombatStats(5000, 10, 10),
            new TroopPool(5000, 0), UnitCombatState.Create(60));
        var state = new GameState(1, 190, [], [defended, supporting, enemyCity], [officer],
            Postings: [new GeneralPosting(officer.Id, supporting.Owner, supporting.Id)],
            GarrisonForces: [new(supporting.Id, "cavalry", 20000, 60)],
            FieldArmies: [enemy]);
        var planner = new AiSupportPlanner(deployer, new HexMap(-3, 18, -4, 4),
            new BalanceConfig(MonthlyTaxPerCity: 100));

        var after = planner.Plan(state, supporting.Owner);

        var response = Assert.Single(after.Armies, unit => unit.Field.Owner == supporting.Owner);
        Assert.Equal(defended.Id, response.AiSupportDefendedCity);
        Assert.Equal(enemy.Id, response.Field.AssignedUnitTarget);
        Assert.Equal(supporting.Id, response.Field.ReturnCity);
        Assert.InRange(response.Pool.Active, 6000, 8000);
        Assert.Single(planner.Plan(after, supporting.Owner).Armies,
            unit => unit.Field.Owner == supporting.Owner);
    }
}
