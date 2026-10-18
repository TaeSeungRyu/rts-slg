namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Simulation.Climate;
using SanguoSLG.Core.Spatial;
using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation.RenewalMovement;
using SanguoSLG.Core.Data;
using SanguoSLG.Core.Simulation;

public sealed class ClimateControllerTests
{
    [Fact]
    public void Advance_겨울이_끝나면_눈을_제거한다()
    {
        var controller = new ClimateController(new HexMap(0, 19, 0, 19), 0);
        var state = controller.AddTestPatch(ClimateState.Initial(17), ClimateKind.Snow,
            new HexCoord(2, 2), 10);

        state = controller.Advance(state, 1, 2);
        Assert.Equal(ClimateKind.Snow, controller.At(state, new HexCoord(2, 2)));
        state = controller.Advance(state, 2, 3);
        Assert.Empty(state.Patches);
    }

    [Fact]
    public void Advance_동일_시드와_날짜는_같은_기후를_만든다()
    {
        var controller = new ClimateController(new HexMap(0, 19, 0, 19), 100);
        var first = ClimateState.Initial(712);
        var second = ClimateState.Initial(712);
        for (var day = 1; day <= 14; day++)
        {
            first = controller.Advance(first, day, 1);
            second = controller.Advance(second, day, 1);
            Assert.Equal(first.Patches, second.Patches);
        }
        Assert.True(first.Patches.Count <= 2);
    }

    [Fact]
    public void AddTestPatch_36타일을_넘지_않고_겹치지_않는다()
    {
        var controller = new ClimateController(new HexMap(0, 11, 0, 11));
        var state = controller.AddTestPatch(ClimateState.Initial(1), ClimateKind.Rain,
            new HexCoord(1, 1));
        Assert.Equal(36, controller.Tiles(state.Patches[0]).Count());
        var blocked = controller.AddTestPatch(state, ClimateKind.Snow, new HexCoord(3, 3));
        Assert.Equal(state, blocked);
        Assert.Equal(ClimateKind.Rain, controller.At(state, new HexCoord(1, 1)));
        Assert.Equal(ClimateKind.Clear, controller.At(state, new HexCoord(10, 10)));
    }

    [Fact]
    public void Advance_이틀마다_패치가_최대_한칸_움직인다()
    {
        var controller = new ClimateController(new HexMap(0, 19, 0, 19), 0);
        var state = controller.AddTestPatch(ClimateState.Initial(1), ClimateKind.Rain,
            new HexCoord(5, 5), 6);
        state = controller.Advance(state, 1, 5);
        Assert.Equal(new HexCoord(5, 5), state.Patches[0].Origin);
        state = controller.Advance(state, 2, 5);
        Assert.Equal(1, state.Patches[0].Origin.Distance(new HexCoord(5, 5)));
    }

    [Fact]
    public void Movement_기후와_지형_감속은_각각_한번씩_곱한다()
    {
        var tile = new HexCoord(5, 5);
        var map = new HexMap(0, 12, 0, 12,
            new Dictionary<HexCoord, TerrainType> { [tile] = TerrainType.Mountain });
        var controller = new ClimateController(map);
        var rainy = controller.AddTestPatch(ClimateState.Initial(3), ClimateKind.Rain,
            new HexCoord(4, 4));
        var clear = new RenewalMovementMap(map);
        var rain = new RenewalMovementMap(map, climate: rainy);

        Assert.Equal(50, clear.SpeedPercentAt(RenewalHexSpace.Center(tile)));
        Assert.Equal(35, rain.SpeedPercentAt(RenewalHexSpace.Center(tile)));
        Assert.Equal(100, rain.SpeedPercentAt(RenewalHexSpace.Center(new HexCoord(11, 11))));
    }

    [Theory]
    [InlineData(ClimateKind.Rain, TroopClass.Archer, false, 80)]
    [InlineData(ClimateKind.Snow, TroopClass.Cavalry, false, 80)]
    [InlineData(ClimateKind.Rain, TroopClass.Cavalry, false, 100)]
    [InlineData(ClimateKind.Snow, TroopClass.Archer, false, 100)]
    [InlineData(ClimateKind.Snow, TroopClass.Cavalry, true, 100)]
    public void Combat_대상_병종에만_공격력_감소를_적용한다(
        ClimateKind climate, TroopClass troopClass, bool armyGroup, int expected)
    {
        Assert.Equal(expected, ClimateCombatRules.AttackPercent(climate, troopClass, armyGroup));
    }

    [Fact]
    public void Save_기후_패치와_난수_시드를_복원한다()
    {
        var controller = new ClimateController(new HexMap(0, 12, 0, 12));
        var climate = controller.AddTestPatch(ClimateState.Initial(87), ClimateKind.Rain,
            new HexCoord(2, 3));
        var state = new GameState(1, 190, [], [], [], Climate: climate);

        var restored = SaveService.Deserialize(SaveService.Serialize(state));

        Assert.NotNull(restored.Climate);
        Assert.Equal(climate.Seed, restored.Climate.Seed);
        Assert.Equal(climate.Patches.Single(), restored.Climate.Patches.Single());
    }
}
