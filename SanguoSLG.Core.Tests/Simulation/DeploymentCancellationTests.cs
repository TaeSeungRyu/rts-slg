namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;
using Xunit;

public class DeploymentCancellationTests
{
    [Fact]
    public void 막힌_출구_대기부대는_취소하면_병력_장수_물자가_원상복귀한다()
    {
        var city = new City(new CityId(1), "home", new HexCoord(0, 0), new FactionId(1),
            900, CastleSize.Small, Gold: 400);
        var field = new FieldUnit(new UnitId(7), city.Owner, city.Position, 2, 1, 1,
            MovementDomain.Land, UnitMode.Advance, new HexCoord(2, 0), 7);
        var unit = new CombatUnit(field, new CombatStats(1000, 10, 10),
            new TroopPool(3000, 0), UnitCombatState.Create(60), Provisions: 100,
            TroopCode: "swordsman", VanguardId: new GeneralId(1), CargoGold: 60,
            OriginCity: city.Id, AwaitingEgress: true);
        var state = new GameState(1, 190, [], [city], [],
            Postings: [new GeneralPosting(new GeneralId(1), city.Owner, null)],
            FieldArmies: [unit]);

        var result = DeploymentCancellation.Cancel(state, city.Owner, unit.Id);

        Assert.True(result.Ok, result.Error);
        Assert.Empty(result.State.Armies);
        Assert.Equal(3000, Assert.Single(result.State.Garrisons).Troops);
        Assert.Equal(1000, result.State.Cities[0].Provisions);
        Assert.Equal(460, result.State.Cities[0].Gold);
        Assert.Equal(city.Id, Assert.Single(result.State.Assignments).Location);
    }

    [Fact]
    public void 이미_출격한_부대는_출격취소할_수_없다()
    {
        var city = new City(new CityId(1), "home", new HexCoord(0, 0), new FactionId(1),
            900, CastleSize.Small);
        var field = new FieldUnit(new UnitId(7), city.Owner, city.Position, 2, 1, 1,
            MovementDomain.Land, UnitMode.Standby, null, 7);
        var unit = new CombatUnit(field, new CombatStats(1000, 10, 10),
            new TroopPool(3000, 0), UnitCombatState.Create(60), OriginCity: city.Id);
        var state = new GameState(1, 190, [], [city], [], FieldArmies: [unit]);

        Assert.False(DeploymentCancellation.Cancel(state, city.Owner, unit.Id).Ok);
    }
}
