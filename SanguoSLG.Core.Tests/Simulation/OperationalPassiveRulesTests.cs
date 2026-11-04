namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Spatial;

public sealed class OperationalPassiveRulesTests
{
    private static General Officer(int id, params GeneralSkill[] skills) => new(
        new GeneralId(id), $"장수{id}", new Dictionary<TroopClass, AptitudeGrade>(),
        70, 70, 70, BattlePassives: skills);

    [Fact]
    public void MobilityAppliesOnlyToMatchingTroopClassAndBothOfficers()
    {
        var leader = Officer(1, new GeneralSkill(OperationalPassiveRules.InfantryMobility, 1));
        var adjutant = Officer(2, new GeneralSkill(OperationalPassiveRules.InfantryMobility, 3),
            new GeneralSkill(OperationalPassiveRules.CavalryMobility, 2));
        Assert.Equal(120, OperationalPassiveRules.MovementPercent(TroopClass.Infantry, leader, adjutant));
        Assert.Equal(110, OperationalPassiveRules.MovementPercent(TroopClass.Cavalry, leader, adjutant));
        Assert.Equal(100, OperationalPassiveRules.MovementPercent(TroopClass.Archer, leader, adjutant));
    }

    [Fact]
    public void CommandPassivesAddToFormationLimit()
    {
        var leader = Officer(1, new GeneralSkill(OperationalPassiveRules.SeniorCommand, 3));
        var adjutant = Officer(2, new GeneralSkill(OperationalPassiveRules.JuniorCommand, 2));
        Assert.Equal(4000, OperationalPassiveRules.CommandBonus(leader, adjutant));
    }

    [Fact]
    public void MobilityPercentChangesActualDailyMovement()
    {
        var origin = new ContinuousPosition(0, 0);
        var destination = new ContinuousPosition(20_000, 0);
        var normal = RenewalUnitState.Create(new UnitId(1), origin, destination, 2);
        var boosted = RenewalUnitState.Create(new UnitId(2), origin, destination, 2)
            with { SpeedPercent = 115 };
        var simulator = new RenewalAdvanceSimulator();
        var normalResult = simulator.StepPhase(simulator.Start([normal])).State;
        var boostedResult = simulator.StepPhase(simulator.Start([boosted])).State;
        Assert.Equal(2 * ContinuousPosition.UnitsPerTile, normalResult.Units[0].Position.X);
        Assert.Equal(230 * ContinuousPosition.UnitsPerTile / 100, boostedResult.Units[0].Position.X);
    }
}
