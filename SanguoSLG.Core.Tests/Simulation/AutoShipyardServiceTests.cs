namespace SanguoSLG.Core.Tests.Simulation;

using SanguoSLG.Core.Domain;
using SanguoSLG.Core.Simulation;
using SanguoSLG.Core.Spatial;

public class AutoShipyardServiceTests
{
    private static City Port(int day = 90) => new(new CityId(1), "항구", default,
        new FactionId(2), 1000, Gold: 1000, Port: PortSize.Small,
        AiSmallShipNextDay: day, AiMediumShipNextDay: 180);

    private static GameState State(City port, int day, int infantry, int cavalry = 0,
        int ships = 0) => new(day, 190, [], [port], [],
            GarrisonForces:
            [
                new GarrisonForce(port.Id, "swordsman", infantry, 50),
                new GarrisonForce(port.Id, "cavalry", cavalry, 50),
            ],
            PortShipStocks: ships == 0 ? [] : [new PortShipStock(port.Id, "small_boat", ships)]);

    [Fact]
    public void 병종별로_필요한_배를_별도_계산한다()
    {
        var enough = AutoShipyardService.Advance(State(Port(), 90, 3000, ships: 1));
        Assert.Equal(0, enough.Cities[0].AiSmallShipCompletionDay);
        var different = AutoShipyardService.Advance(State(Port(), 90, 3000, 300, ships: 1));
        Assert.Equal(104, different.Cities[0].AiSmallShipCompletionDay);
    }

    [Fact]
    public void 제작을_시작한_배는_병력이_이동해도_완공한다()
    {
        var started = AutoShipyardService.Advance(State(Port(), 90, 30_000, ships: 2));
        Assert.Equal(104, started.Cities[0].AiSmallShipCompletionDay);
        var moved = State(started.Cities[0], 104, 20_000, ships: 2);
        var completed = AutoShipyardService.Advance(moved);
        Assert.Equal(3, Assert.Single(completed.PortShips).Count);
        Assert.Equal(0, completed.Cities[0].AiSmallShipCompletionDay);
    }

    [Fact]
    public void 같은_날_소형과_중형을_슬롯과_장수없이_동시_제작한다()
    {
        var state = State(Port(180), 180, 30_000);
        var started = AutoShipyardService.Advance(state);
        Assert.Equal(194, started.Cities[0].AiSmallShipCompletionDay);
        Assert.Equal(210, started.Cities[0].AiMediumShipCompletionDay);
        Assert.Equal(400, started.Cities[0].Gold);
        var completed = AutoShipyardService.Advance(started with { Day = 210 });
        Assert.Equal(2, completed.PortShips.Sum(stock => stock.Count));
    }
}
