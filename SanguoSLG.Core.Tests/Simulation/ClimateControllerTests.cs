namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Simulation.Climate;
using SanguoSLG.Core.Spatial;

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
}
